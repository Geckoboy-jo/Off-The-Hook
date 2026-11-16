using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float speed;
    public Animator animator;
    public bool IsMoving { get; private set; }
    private static readonly int IsMovingParameter = Animator.StringToHash("IsMoving");
    private bool speedAnimationReady;
    public Transform respawnPoint;
    public bool IsDead { get; private set; }
    private float respawnTimer;
    private Vector3 respawnPosition;
    private bool simulatedBeforeDeath;
    private bool grappleEnabledBeforeDeath;

    private Renderer[] deathRenderers;
    private bool[] rendererStates;
    private readonly HashSet<HookPointGrapple> consumedHookPoints = new HashSet<HookPointGrapple>();
    [Tooltip("Grounded acceleration toward maximum movement speed, in units per second squared.")]
    [Min(0f)] public float groundAcceleration = 40f;
    [Tooltip("Horizontal air steering acceleration; releasing input preserves momentum.")]
    [Min(0f)] public float airAcceleration = 12f;
    [Tooltip("Grounded reversal braking relative to movement speed; lower values give longer skids.")]
    [Min(0f)] public float skidBraking = 4f;
    public Transform GroundCheck;
    public float groundCheckRadius;
    public LayerMask groundLayer;
    public float jumpForce;
    private Rigidbody2D rb;
    public float fastFallForce;
    [Tooltip("Number of rendered frames after leaving ground during which a jump is still allowed.")]
    [Min(0)] public int coyoteFrames = 6;
    private int lastGroundedFrame;
    private bool hasGroundedFrame;
    private bool mustLeaveGround;
    private GrappleController grapple;
    public Transform armPivot;
    public Transform shootPoint;
    private Vector3 localShootDirection;
    public bool hasDash;
    public float dashCooldown;
    public float dashDuration = 0.15f;
    public float dashSpeed = 14f;
    private float dashTimer;
    private float dashTimeRemaining;
    private Vector2 dashDirection;
    private float dashJumpVelocity;
    private bool isDashing;
    private float gravityScaleBeforeDash;
    private int facingDirection = 1;
    private bool wasGrounded;
    [Tooltip("Non-looping particle system prefab spawned at the start of each dash.")]
    public ParticleSystem dashBubbles;


    private bool IsGrounded()
    {
        return (grapple != null && grapple.IsGrounded) ||
            Physics2D.OverlapCircle(GroundCheck.position, groundCheckRadius, groundLayer);
    }
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        grapple = GetComponentInChildren<GrappleController>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (dashBubbles == null)
            Debug.LogWarning("Assign a dash bubbles particle prefab to show dash effects.", this);
        else if (dashBubbles.main.loop)
            Debug.LogError("Dash bubbles must be non-looping so the effect can clean itself up.", this);
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            if (!animator.isInitialized && animator.runtimeAnimatorController != null &&
                animator.isActiveAndEnabled)
            {
                animator.Rebind();
            }
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Bool &&
                    parameter.nameHash == IsMovingParameter)
                {
                    speedAnimationReady = true;
                    break;
                }
            }
            if (!speedAnimationReady)
            {
                string controllerName = animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.name : "(none)";
                Debug.LogError($"Player Animator on '{animator.gameObject.name}' uses controller " +
                    $"'{controllerName}', which must have a bool parameter named IsMoving.", this);
            }
        }
        hasDash = true;
        wasGrounded = IsGrounded();
        if (armPivot != null && shootPoint != null)
        {
            localShootDirection = armPivot.InverseTransformVector(
                shootPoint.TransformVector(Vector3.right));
        }
    }

    void LateUpdate()
    {
        IsMoving = !IsDead && rb.linearVelocity.sqrMagnitude > 0f;
        if (!speedAnimationReady || animator == null) return;
        animator.SetBool(IsMovingParameter, IsMoving);
    }

    // Update is called once per frame
    void Update()
    {
        if (IsDead)
        {
            respawnTimer -= Time.unscaledDeltaTime;
            if (respawnTimer <= 0f) Respawn();
            return;
        }
        dashTimer = Mathf.Max(0f, dashTimer - Time.deltaTime);
        float moveInput = 0;
        if (Keyboard.current.aKey.isPressed)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
            moveInput = -1;
        }
        if (Keyboard.current.dKey.isPressed)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
            moveInput = 1;
        }
        if (moveInput != 0f) facingDirection = moveInput > 0f ? 1 : -1;
        bool isGrounded = IsGrounded();
        if (!isGrounded) mustLeaveGround = false;
        if (isGrounded && !mustLeaveGround)
        {
            lastGroundedFrame = Time.frameCount;
            hasGroundedFrame = true;
        }
        if (isGrounded && !wasGrounded)
        {
            hasDash = true;
        }
        if (grapple != null && grapple.CanRefillDash)
        {
            hasDash = true;
            dashTimer = 0f;
        }
        wasGrounded = isGrounded;
        UpdateAim();
        if(isGrounded== true)
        {
            hasDash = true;
        }
        if (isDashing)
        {
            bool withinDashCoyoteWindow = hasGroundedFrame &&
                Time.frameCount - lastGroundedFrame <= Mathf.Max(0, coyoteFrames);
            bool canJumpDuringDash = !mustLeaveGround && (isGrounded || withinDashCoyoteWindow);
            if (Keyboard.current.spaceKey.wasPressedThisFrame && canJumpDuringDash)
            {
                EndDash();
                if (grapple != null) grapple.ReleaseGrapple();
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                hasGroundedFrame = false;
                mustLeaveGround = true;
                return;
            }
            dashTimeRemaining -= Time.deltaTime;
            if (dashTimeRemaining <= 0f)
            {
                EndDash();
            }
            else
            {
                rb.linearVelocity = dashDirection * dashSpeed + Vector2.up * dashJumpVelocity;
                return;
            }
        }
        if(Keyboard.current.escapeKey.wasPressedThisFrame)
            Application.Quit();
        if (Keyboard.current.shiftKey.wasPressedThisFrame && hasDash && dashTimer <= 0f)
        {
            if (dashDuration <= 0f || dashSpeed <= 0f)
            {
                Debug.LogError("Dash duration and speed must be positive.", this);
            }
            else
            {
                float verticalInput = 0f;
                if (Keyboard.current.wKey.isPressed) verticalInput += 1f;
                if (Keyboard.current.sKey.isPressed) verticalInput -= 1f;
                Vector2 dashDirection = new Vector2(moveInput, verticalInput).normalized;
                if (dashDirection == Vector2.zero)
                    dashDirection = facingDirection > 0 ? Vector2.right : Vector2.left;

                bool dashWithinCoyoteWindow = hasGroundedFrame &&
                    Time.frameCount - lastGroundedFrame <= Mathf.Max(0, coyoteFrames);
                bool dashCanJumpFromGrapple = grapple != null && grapple.CanRefillDash;
                bool dashJump = Keyboard.current.spaceKey.wasPressedThisFrame &&
                    ((!mustLeaveGround && (isGrounded || dashWithinCoyoteWindow)) || dashCanJumpFromGrapple);
                hasDash = false;
                dashTimer = dashCooldown;
                if (dashJump)
                {
                    hasGroundedFrame = false;
                    mustLeaveGround = true;
                }
                if (grapple != null) grapple.ReleaseGrapple();
                if (dashJump)
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                StartDash(dashDirection, dashJump);
                return;
            }
        }

        bool jumpPressed = Keyboard.current.spaceKey.wasPressedThisFrame;
        bool canJumpFromGrapple = grapple != null && grapple.CanRefillDash;
        // Sample grounding before releasing so a nearby hook can be jumped from.
        if (jumpPressed && grapple != null)
        {
            grapple.ReleaseGrapple();
        }
        if (isGrounded && (grapple == null || !grapple.IsGrappled))
        {
            float horizontalVelocity = moveInput * speed;
            if (moveInput != 0f)
            {
                horizontalVelocity = Mathf.MoveTowards(rb.linearVelocity.x,
                    horizontalVelocity, groundAcceleration * Time.deltaTime);
            }
            if (isGrounded && moveInput != 0f && rb.linearVelocity.x * moveInput < 0f)
            {
                horizontalVelocity = Mathf.MoveTowards(rb.linearVelocity.x, 0f,
                    Mathf.Abs(speed) * skidBraking * Time.deltaTime);
            }
            rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);
        }
        else if (!isGrounded && moveInput != 0f &&
            (grapple == null || !grapple.IsGrappled))
        {
            float targetVelocity = moveInput * Mathf.Abs(speed);
            // Keep faster dash/grapple momentum when steering in the same direction.
            if (rb.linearVelocity.x * moveInput < Mathf.Abs(speed))
            {
                float horizontalVelocity = Mathf.MoveTowards(rb.linearVelocity.x,
                    targetVelocity, airAcceleration * Time.deltaTime);
                rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);
            }
        }
        bool withinCoyoteWindow = hasGroundedFrame &&
            Time.frameCount - lastGroundedFrame <= Mathf.Max(0, coyoteFrames);
        if (jumpPressed && ((!mustLeaveGround && (isGrounded || withinCoyoteWindow)) ||
            canJumpFromGrapple))
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            hasGroundedFrame = false;
            mustLeaveGround = true;
        }
        //fastfall implementation
        if (Keyboard.current.sKey.isPressed && !isGrounded &&
            (grapple == null || !grapple.IsGrappled))
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y - fastFallForce);
        }
    }

    private void StartDash(Vector2 direction, bool dashJump)
    {
        if (dashBubbles != null && !dashBubbles.main.loop)
        {
            ParticleSystem bubbles = Instantiate(dashBubbles, transform.position,
                dashBubbles.transform.rotation);
            ParticleSystem.MainModule main = bubbles.main;
            main.stopAction = ParticleSystemStopAction.Destroy;
            bubbles.Play();
        }
        dashDirection = direction;
        dashJumpVelocity = dashJump ? jumpForce : 0f;
        dashTimeRemaining = dashDuration;
        gravityScaleBeforeDash = rb.gravityScale;
        rb.gravityScale = 0f;
        isDashing = true;
        rb.linearVelocity = rb.linearVelocity + dashDirection * dashSpeed;
    }

    private void EndDash()
    {
        isDashing = false;
        dashJumpVelocity = 0f;
        rb.gravityScale = gravityScaleBeforeDash;
    }

    private void UpdateAim()
    {
        if (Mouse.current == null) return;
        Camera aimCamera = Camera.main;
        if (aimCamera == null)
        {
            Debug.LogError("Player aiming requires a camera tagged MainCamera.", this);
            return;
        }

        Ray mouseRay = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane aimPlane = new Plane(Vector3.forward, transform.position);
        if (!aimPlane.Raycast(mouseRay, out float distance)) return;
        Vector3 mouseWorld = mouseRay.GetPoint(distance);

        if (armPivot != null && shootPoint != null)
        {
            Vector3 direction = mouseWorld - armPivot.position;
            if (armPivot.parent != null)
                direction = armPivot.parent.InverseTransformVector(direction);
            Vector3 barrelDirection = Vector3.Scale(armPivot.localScale, localShootDirection);
            if ((Vector2)direction != Vector2.zero)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                float barrelAngle = Mathf.Atan2(barrelDirection.y, barrelDirection.x) * Mathf.Rad2Deg;
                armPivot.localRotation = Quaternion.Euler(0f, 0f, angle - barrelAngle);
            }
        }

        if (shootPoint != null)
        {
            Vector2 direction = mouseWorld - shootPoint.position;
            if (direction.sqrMagnitude > 0.000001f)
            {
                // World rotation keeps the launch axis correct even under a mirrored parent.
                shootPoint.rotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
        }

        
        
    }
    internal void TrackConsumedHookPoint(HookPointGrapple point)
    {
        consumedHookPoints.Add(point);
    }

    public void die()
    {
        if (IsDead) return;
        if (respawnPoint == null || rb == null)
        {
            Debug.LogError("Player death requires an assigned respawn point and Rigidbody2D.", this);
            return;
        }

        IsDead = true;
        respawnTimer = 3f;
        respawnPosition = respawnPoint.position;
        if (isDashing) EndDash();
        if (grapple != null)
        {
            grappleEnabledBeforeDeath = grapple.enabled;
            grapple.ReleaseGrapple();
            grapple.enabled = false;
        }
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        simulatedBeforeDeath = rb.simulated;
        rb.simulated = false;
        deathRenderers = GetComponentsInChildren<Renderer>(true);
        rendererStates = new bool[deathRenderers.Length];
        for (int i = 0; i < deathRenderers.Length; i++)
        {
            rendererStates[i] = deathRenderers[i].enabled;
            deathRenderers[i].enabled = false;
        }
        foreach (HookPointGrapple point in consumedHookPoints)
            if (point != null) point.Restore();
        consumedHookPoints.Clear();
    }

    private void Respawn()
    {
        transform.position = respawnPosition;
        rb.position = respawnPosition;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = simulatedBeforeDeath;
        hasDash = true;
        dashTimer = 0f;
        dashTimeRemaining = 0f;
        hasGroundedFrame = false;
        mustLeaveGround = false;
        wasGrounded = false;
        for (int i = 0; i < deathRenderers.Length; i++)
            if (deathRenderers[i] != null) deathRenderers[i].enabled = rendererStates[i];
        if (grapple != null) grapple.enabled = grappleEnabledBeforeDeath;
        IsDead = false;
    }
}
