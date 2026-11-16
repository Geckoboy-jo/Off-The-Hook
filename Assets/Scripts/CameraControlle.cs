using UnityEngine;

public class CameraControlle : MonoBehaviour
{
    public GameObject target;
    public float followSpeed = 5f;
    [Tooltip("Rightward camera pan speed before a player first appears, in world units per second.")]
    [Min(0f)] public float introPanSpeed = 0.1f;
    [Tooltip("Base camera framing offset from the player, in world units.")]
    public Vector2 targetOffset = new Vector2(0f, 1.5f);
    [Tooltip("Maximum camera look-ahead in world units on each axis.")]
    public Vector2 lookAheadDistance = new Vector2(2f, 1f);
    [Tooltip("Target speed at which look-ahead reaches its maximum.")]
    [Min(0.01f)] public float lookAheadFullSpeed = 8f;
    [Tooltip("How quickly the look-ahead changes direction.")]
    [Min(0f)] public float lookAheadResponse = 3f;
    [Tooltip("Viewport space reserved between the player and each screen edge.")]
    [Range(0f, 0.4f)] public float screenMargin = 0.1f;

    private Camera followCamera;
    private GameObject previousTarget;
    private Rigidbody2D targetBody;
    private Collider2D[] targetColliders;
    private Renderer targetRenderer;
    private Vector2 previousPosition;
    private Vector2 lookAhead;
    private bool warnedInvalidSpeed;
    private bool playerHasAppeared;

    void Awake()
    {
        followCamera = GetComponent<Camera>();
        if (followCamera == null || !followCamera.orthographic)
        {
            Debug.LogError("CameraControlle requires an orthographic Camera on the same object.", this);
            enabled = false;
        }
    }

    void LateUpdate()
    {
        if (!playerHasAppeared &&
            (target != null || FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include) != null))
        {
            playerHasAppeared = true;
        }

        if (target == null)
        {
            previousTarget = null;
            lookAhead = Vector2.zero;
            if (!playerHasAppeared)
                transform.position += Vector3.right * (Mathf.Max(0f, introPanSpeed) * Time.deltaTime);
            return;
        }
        Vector2 position = target.transform.position;
        if (target != previousTarget)
        {
            previousTarget = target;
            targetBody = target.GetComponent<Rigidbody2D>();
            targetColliders = target.GetComponentsInChildren<Collider2D>();
            targetRenderer = target.GetComponentInChildren<SpriteRenderer>();
            previousPosition = position;
            lookAhead = Vector2.zero;
        }

        if (lookAheadFullSpeed <= 0f)
        {
            if (!warnedInvalidSpeed)
                Debug.LogError("Camera look-ahead full speed must be positive.", this);
            warnedInvalidSpeed = true;
            KeepPlayerOnScreen();
            return;
        }
        warnedInvalidSpeed = false;
        Vector2 velocity = targetBody != null ? targetBody.linearVelocity :
            (Time.deltaTime > 0f ? (position - previousPosition) / Time.deltaTime : Vector2.zero);
        previousPosition = position;
        Vector2 desiredLookAhead = new Vector2(
            Mathf.Clamp(velocity.x / lookAheadFullSpeed, -1f, 1f) * lookAheadDistance.x,
            Mathf.Clamp(velocity.y / lookAheadFullSpeed, -1f, 1f) * lookAheadDistance.y);
        float lookBlend = 1f - Mathf.Exp(-Mathf.Max(0f, lookAheadResponse) * Time.deltaTime);
        lookAhead = Vector2.Lerp(lookAhead, desiredLookAhead, lookBlend);

        Vector3 targetPosition = new Vector3(position.x + targetOffset.x + lookAhead.x,
            position.y + targetOffset.y + lookAhead.y, transform.position.z);
        float followBlend = 1f - Mathf.Exp(-Mathf.Max(0f, followSpeed) * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, followBlend);
        KeepPlayerOnScreen();
    }

    private void KeepPlayerOnScreen()
    {
        Bounds bounds = new Bounds(target.transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (Collider2D collider in targetColliders)
        {
            if (collider == null || !collider.isActiveAndEnabled || collider.isTrigger) continue;
            if (!hasBounds) bounds = collider.bounds;
            else bounds.Encapsulate(collider.bounds);
            hasBounds = true;
        }
        if (targetRenderer != null && targetRenderer.enabled)
        {
            if (hasBounds) bounds.Encapsulate(targetRenderer.bounds);
            else bounds = targetRenderer.bounds;
        }

        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
            Vector2 viewport = followCamera.WorldToViewportPoint(corner);
            min = Vector2.Min(min, viewport);
            max = Vector2.Max(max, viewport);
        }

        float margin = Mathf.Clamp(screenMargin, 0f, 0.4f);
        float shiftX = GetViewportCorrection(min.x, max.x, margin);
        float shiftY = GetViewportCorrection(min.y, max.y, margin);
        float height = followCamera.orthographicSize * 2f;
        // Apply an immediate correction after smoothing, along the camera's viewing plane.
        transform.position += transform.right * (shiftX * height * followCamera.aspect) +
            transform.up * (shiftY * height);
    }

    private static float GetViewportCorrection(float min, float max, float margin)
    {
        if (max - min > 1f - 2f * margin) return (min + max) * 0.5f - 0.5f;
        if (min < margin) return min - margin;
        if (max > 1f - margin) return max - (1f - margin);
        return 0f;
    }
}
