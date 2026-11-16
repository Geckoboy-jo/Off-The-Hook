using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GrappleController : MonoBehaviour
{
    public GameObject grapplePrefab;
    public Transform firePoint;
    [Min(0.01f)] public float grappleSpeed = 20f;
    [Min(0.01f)] public float maxGrappleDistance = 20f;
    [Min(0.01f)] public float pullSpeed = 10f;
    [Tooltip("The hook can attach to any layer selected in any of these masks.")]
    public LayerMask[] grappleLayers = { (LayerMask)(1 << 3) };
    [Tooltip("One foot in Unity units, assuming one Unity unit is one metre.")]
    [Min(0f)] public float grappleGroundedDistance = 0.3048f;
    [Tooltip("Number of connected, non-colliding rope segments.")]
    [Range(8, 80)] public int ropeSegments = 40;
    [Min(0.01f)] public float ropeWidth = 0.04f;

    private Rigidbody2D playerBody;
    private GameObject currentGrapple;
    private GrappleRope currentRope;
    private readonly Queue<GameObject> grappleHistory = new Queue<GameObject>();
    private Material ropeMaterial;
    private Collider2D attachedCollider;
    private HookPointGrapple attachedHookPoint;
    private Vector2 localAttachPoint;
    private Vector2 flightDirection;
    private float travelledDistance;
    private bool isRetracting;
    private bool isHoldingPlayer;
    private float gravityBeforeHold;
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();

    public bool IsGrappled { get; private set; }
    public bool IsGrounded => isHoldingPlayer || IsWithinAttachedHookDistance(grappleGroundedDistance);
    public bool CanRefillDash => isHoldingPlayer || IsWithinAttachedHookDistance(1f);

    private bool IsWithinAttachedHookDistance(float distance)
    {
        return attachedHookPoint == null && IsGrappled && attachedCollider != null &&
        attachedCollider.isActiveAndEnabled && currentGrapple != null &&
        Vector2.Distance(playerBody.position, AttachPoint) <= distance;
    }

    private Vector2 AttachPoint => attachedCollider.transform.TransformPoint(localAttachPoint);

    void Awake()
    {
        playerBody = GetComponentInParent<Rigidbody2D>();
        if (playerBody == null)
        {
            Debug.LogError("GrappleController requires a player Rigidbody2D on this object or a parent.", this);
            enabled = false;
            return;
        }
        Shader ropeShader = Shader.Find("Sprites/Default");
        if (ropeShader == null)
        {
            Debug.LogError("The rope requires the Sprites/Default shader.", this);
            enabled = false;
            return;
        }
        ropeMaterial = new Material(ropeShader);
    }

    void LateUpdate()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            FireGrapple();
        }
    }

    void FixedUpdate()
    {
        if (currentGrapple == null)
        {
            if (IsGrappled || isRetracting) ReleaseGrapple();
            return;
        }
        if (isRetracting) return;

        if (IsGrappled)
        {
            if (attachedCollider == null || !attachedCollider.isActiveAndEnabled)
            {
                RetractGrapple();
                return;
            }

            Vector2 point = AttachPoint;
            currentGrapple.transform.position = point;
            Vector2 offset = point - playerBody.position;
            if (attachedHookPoint != null)
            {
                Vector2 direction = offset.sqrMagnitude > 0.000001f
                    ? offset.normalized : playerBody.linearVelocity.normalized;
                playerBody.linearVelocity = direction * pullSpeed;
                if (offset.magnitude <= Mathf.Max(attachedHookPoint.releaseDistance,
                    pullSpeed * Time.fixedDeltaTime))
                {
                    HookPointGrapple consumedPoint = attachedHookPoint;
                    ReleaseGrapple();
                    consumedPoint.Consume(playerBody.GetComponent<PlayerController>());
                }
                return;
            }
            if (isHoldingPlayer || (currentRope != null && currentRope.IsFullyReeledIn))
            {
                if (!isHoldingPlayer)
                {
                    gravityBeforeHold = playerBody.gravityScale;
                    isHoldingPlayer = true;
                    playerBody.gravityScale = 0f;
                    currentRope.MarkFullyReeledIn();
                }
                playerBody.linearVelocity = Vector2.zero;
                return;
            }
            StopHoldingPlayer();
            float speed = Mathf.Min(pullSpeed, offset.magnitude / Time.fixedDeltaTime);
            playerBody.linearVelocity = offset.normalized * speed;
            return;
        }

        float distance = Mathf.Min(grappleSpeed * Time.fixedDeltaTime,
            maxGrappleDistance - travelledDistance);
        Vector2 origin = currentGrapple.transform.position;
        int layerMask = 0;
        foreach (LayerMask mask in grappleLayers) layerMask |= mask.value;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(Physics2D.AllLayers);
        filter.useTriggers = true;
        Physics2D.Raycast(origin, flightDirection, filter, hits, distance);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider.attachedRigidbody == playerBody ||
                hit.transform.IsChildOf(playerBody.transform))
            {
                continue;
            }

            HookPointGrapple hookPoint = hit.collider.GetComponentInParent<HookPointGrapple>();
            if (hit.collider.isTrigger && hookPoint == null) continue;
            currentGrapple.transform.position = hit.point;
            if (hookPoint == null && (layerMask & (1 << hit.collider.gameObject.layer)) == 0)
            {
                RetractGrapple();
                return;
            }
            attachedCollider = hit.collider;
            attachedHookPoint = hookPoint;
            if (hookPoint != null) hookPoint.RegisterHook(currentGrapple);
            localAttachPoint = attachedCollider.transform.InverseTransformPoint(hit.point);
            IsGrappled = true;
            currentRope.ReelIn = true;
            if (Soundmanager.Instance != null)
            {
                Soundmanager.Instance.StopGrappleExtend();
                Soundmanager.Instance.PlayGrappleHitTarget();
            }
            return;
        }

        currentGrapple.transform.position = origin + flightDirection * distance;
        travelledDistance += distance;
        if (travelledDistance >= maxGrappleDistance) RetractGrapple();
    }

    private void FireGrapple()
    {
        int layerMask = 0;
        if (grappleLayers != null)
        {
            foreach (LayerMask mask in grappleLayers) layerMask |= mask.value;
        }
        if (grapplePrefab == null || firePoint == null || grappleSpeed <= 0f ||
            maxGrappleDistance <= 0f || pullSpeed <= 0f || layerMask == 0 ||
            ropeSegments < 8 || ropeWidth <= 0f || ropeMaterial == null)
        {
            Debug.LogError("Assign a grapple prefab, fire point, valid grapple layers, and positive grapple speeds and range.", this);
            return;
        }
        ReleaseGrapple();
        flightDirection = firePoint.right;
        // The hook sprite's orientation is independent of its travel direction.
        Quaternion projectilerotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(flightDirection.y, flightDirection.x) * Mathf.Rad2Deg + 90f);
        currentGrapple = Instantiate(grapplePrefab, firePoint.position, projectilerotation);
        // Swept raycasts own hook motion so fast hooks cannot tunnel through surfaces.
        foreach (Rigidbody2D body in currentGrapple.GetComponentsInChildren<Rigidbody2D>())
            body.simulated = false;
        foreach (Collider2D collider in currentGrapple.GetComponentsInChildren<Collider2D>())
            collider.enabled = false;
        currentRope = currentGrapple.AddComponent<GrappleRope>();
        currentRope.Initialize(firePoint, ropeSegments, ropeWidth, ropeMaterial);
        int historyCount = grappleHistory.Count;
        for (int i = 0; i < historyCount; i++)
        {
            GameObject hook = grappleHistory.Dequeue();
            if (hook != null) grappleHistory.Enqueue(hook);
        }
        grappleHistory.Enqueue(currentGrapple);
        while (grappleHistory.Count > 10)
        {
            GameObject oldest = grappleHistory.Dequeue();
            if (oldest != null) Destroy(oldest);
        }
        travelledDistance = 0f;
        if (Soundmanager.Instance != null)
        {
            Soundmanager.Instance.PlayShoot();
            Soundmanager.Instance.StartGrappleExtend();
        }
    }

    public void ReleaseGrapple()
    {
        StopHoldingPlayer();
        bool canDetach = IsGrappled && IsValidAttachment();
        if (currentGrapple != null && !canDetach && !isRetracting)
        {
            RetractGrapple();
        }
        IsGrappled = false;
        if (currentGrapple != null)
        {
            if (canDetach)
                currentGrapple.transform.position = AttachPoint;
            foreach (Collider2D collider in currentGrapple.GetComponentsInChildren<Collider2D>())
                collider.enabled = false;
        }
        if (currentRope != null && canDetach)
        {
            currentRope.Detach();
            if (Soundmanager.Instance != null) Soundmanager.Instance.PlayDetachRope();
        }
        if (Soundmanager.Instance != null) Soundmanager.Instance.StopGrappleExtend();
        isRetracting = false;
        attachedCollider = null;
        attachedHookPoint = null;
        currentRope = null;
        currentGrapple = null;
    }

    private void RetractGrapple()
    {
        StopHoldingPlayer();
        IsGrappled = false;
        attachedCollider = null;
        attachedHookPoint = null;
        isRetracting = true;
        if (Soundmanager.Instance != null)
        {
            Soundmanager.Instance.StopGrappleExtend();
            Soundmanager.Instance.PlayGrappleRetract();
        }
        foreach (Collider2D collider in currentGrapple.GetComponentsInChildren<Collider2D>())
            collider.enabled = false;
        currentRope.BeginRetraction(grappleSpeed);
    }

    private void StopHoldingPlayer()
    {
        if (!isHoldingPlayer) return;
        if (playerBody != null) playerBody.gravityScale = gravityBeforeHold;
        isHoldingPlayer = false;
    }

    void OnDisable()
    {
        ReleaseGrapple();
    }

    private bool IsValidAttachment()
    {
        return attachedCollider != null && attachedCollider.isActiveAndEnabled;
    }

    void OnDestroy()
    {
        foreach (GameObject hook in grappleHistory)
            if (hook != null) Destroy(hook);
        if (ropeMaterial != null) Destroy(ropeMaterial);
    }
    
}

// Verlet particles joined by distance constraints provide rope physics without colliders.
public class GrappleRope : MonoBehaviour
{
    private Transform shootPoint;
    private Vector2[] points;
    private Vector2[] previousPoints;
    private LineRenderer line;
    private float segmentLength;
    private bool connected;
    private bool retracting;
    private float retractSpeed;
    public bool ReelIn { get; set; }
    private bool fullyReeledIn;
    public bool IsFullyReeledIn => connected && ReelIn && shootPoint != null &&
        (fullyReeledIn || Vector2.Distance(transform.position, shootPoint.position) <= 0.3048f);

    public void MarkFullyReeledIn()
    {
        fullyReeledIn = true;
    }

    public void Initialize(Transform endpoint, int segments, float width, Material material)
    {
        shootPoint = endpoint;
        connected = true;
        points = new Vector2[segments + 1];
        previousPoints = new Vector2[points.Length];
        Vector2 hookPosition = transform.position;
        Vector2 playerPosition = shootPoint.position;
        segmentLength = Mathf.Max(0.02f,
            Vector2.Distance(hookPosition, playerPosition) / segments);
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = Vector2.Lerp(hookPosition, playerPosition, (float)i / segments);
            previousPoints[i] = points[i];
        }
        line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = points.Length;
        line.sharedMaterial = material;
        line.startColor = line.endColor = new Color(0.36f, 0.18f, 0.07f, 1f);
        line.startWidth = line.endWidth = width;
        line.numCapVertices = 2;
        SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null)
        {
            line.sortingLayerID = sprite.sortingLayerID;
            line.sortingOrder = sprite.sortingOrder - 1;
        }
    }

    public void Detach()
    {
        fullyReeledIn = false;
        connected = false;
        shootPoint = null;
    }

    private void StopRetractSound()
    {
        if (Soundmanager.Instance != null) Soundmanager.Instance.StopGrappleRetract();
    }

    public void BeginRetraction(float speed)
    {
        retracting = true;
        retractSpeed = speed;
        ReelIn = true;
    }

    void FixedUpdate()
    {
        if (points == null) return;
        if (retracting)
        {
            if (shootPoint == null)
            {
                StopRetractSound();
                Destroy(gameObject);
                return;
            }
            Vector2 target = shootPoint.position;
            Vector2 position = Vector2.MoveTowards(transform.position, target,
                retractSpeed * Time.fixedDeltaTime);
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            if (Vector2.Distance(position, target) <= 0.01f)
            {
                StopRetractSound();
                Destroy(gameObject);
                return;
            }
        }
        if (connected && shootPoint == null) Detach();
        Vector2 hookPosition = transform.position;
        int last = points.Length - 1;
        if (connected)
        {
            float endpointLength = Mathf.Max(0.001f,
                Vector2.Distance(hookPosition, shootPoint.position) / last);
            // Pay out during flight, reel in when lodged, and freeze length on release.
            segmentLength = ReelIn ? endpointLength : Mathf.Max(segmentLength, endpointLength);
        }
        float step = Time.fixedDeltaTime;
        for (int i = 1; i < points.Length; i++)
        {
            if (connected && i == last) continue;
            Vector2 position = points[i];
            points[i] += (position - previousPoints[i]) * 0.99f +
                Physics2D.gravity * (step * step);
            previousPoints[i] = position;
        }
        for (int iteration = 0; iteration < 20; iteration++)
        {
            points[0] = hookPosition;
            if (connected) points[last] = shootPoint.position;
            for (int i = 0; i < last; i++)
            {
                Vector2 delta = points[i + 1] - points[i];
                float length = delta.magnitude;
                if (length <= 0.000001f) continue;
                Vector2 correction = delta * ((length - segmentLength) / length);
                bool firstPinned = i == 0;
                bool secondPinned = connected && i + 1 == last;
                if (firstPinned) points[i + 1] -= correction;
                else if (secondPinned) points[i] += correction;
                else
                {
                    points[i] += correction * 0.5f;
                    points[i + 1] -= correction * 0.5f;
                }
            }
        }
        points[0] = previousPoints[0] = hookPosition;
        if (connected)
            points[last] = previousPoints[last] = (Vector2)shootPoint.position;
    }

    void LateUpdate()
    {
        if (line == null) return;
        line.enabled = !IsFullyReeledIn;
        points[0] = transform.position;
        if (connected && shootPoint != null) points[points.Length - 1] = shootPoint.position;
        for (int i = 0; i < points.Length; i++)
            line.SetPosition(i, new Vector3(points[i].x, points[i].y, transform.position.z));
    }
}
