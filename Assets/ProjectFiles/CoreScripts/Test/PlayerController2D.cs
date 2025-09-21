using UnityEngine;
using UnityEngine.InputSystem; // << New Input System

[RequireComponent(typeof(Rigidbody2D))]
[DisallowMultipleComponent]
public class PlayerController2D : MonoBehaviour
{
    [Header("Input Actions (New Input System)")]
    public InputActionReference moveAction;   // Vector2 (ใช้ X)
    public InputActionReference jumpAction;   // Button
    public InputActionReference slideAction;  // Button

    [Header("Movement")]
    public float moveSpeed = 9f;
    public float acceleration = 45f;
    public float airControl = 0.6f;
    public float maxFallSpeed = -25f;

    [Header("Jump")]
    public float jumpForce = 14f;
    public float coyoteTime = 0.12f;
    public float jumpBuffer = 0.12f;
    public float lowJumpGravityMultiplier = 2.0f;

    [Header("Wall")]
    public float wallCheckDistance = 0.5f;
    public float wallSlideSpeed = -2.5f;
    public float wallJumpForce = 14f;
    public Vector2 wallJumpDirection = new Vector2(1.0f, 1.2f);
    public float wallJumpLockTime = 0.15f;
    public float wallStickTime = 0.08f;

    [Header("Slide (Ground)")]
    public float slideMinSpeed = 6f;
    public float slideDuration = 0.5f;
    public float slideFriction = 12f;
    public float standColliderHeight = 1.8f;
    public float slideColliderHeight = 1.0f;
    public Vector2 colliderOffsetStanding = new Vector2(0f, -0.1f);
    public Vector2 colliderOffsetSliding = new Vector2(0f, -0.45f);

    [Header("Checks & Layers")]
    public Transform groundCheck;
    public Vector2 groundCheckSize = new Vector2(0.5f, 0.1f);
    public LayerMask groundLayer = ~0;

    [Header("Debug")]
    public bool drawGizmos = true;

    Rigidbody2D rb;
    CapsuleCollider2D capsule;

    float inputX;
    bool isGrounded, isOnWall, isWallLeft, isSliding;
    float coyoteCounter, jumpBufferCounter, wallStickCounter, wallJumpLockCounter, slideTimer;
    float defaultGravity;
    float targetSpeed, currentAccel;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        defaultGravity = rb.gravityScale;

        capsule = GetComponent<CapsuleCollider2D>();
        if (!capsule) capsule = gameObject.AddComponent<CapsuleCollider2D>();
        capsule.direction = CapsuleDirection2D.Vertical;
        SetStandingCollider();
    }

    void OnEnable()
    {
        // Enable input actions
        moveAction?.action.Enable();
        jumpAction?.action.Enable();
        slideAction?.action.Enable();
    }

    void OnDisable()
    {
        moveAction?.action.Disable();
        jumpAction?.action.Disable();
        slideAction?.action.Disable();
    }

    void Update()
    {
        // ----- READ INPUT (New Input System) -----
        // Move expects Vector2; เราใช้แค่แกน X
        Vector2 move = moveAction ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        inputX = Mathf.Clamp(move.x, -1f, 1f);

        bool jumpPressed = jumpAction && jumpAction.action.WasPressedThisFrame();
        bool jumpHeld = jumpAction && jumpAction.action.IsPressed();
        bool slidePressed = slideAction && slideAction.action.WasPressedThisFrame();

        // ----- TIMERS -----
        if (IsGrounded()) coyoteCounter = coyoteTime; else coyoteCounter -= Time.deltaTime;
        if (jumpPressed) jumpBufferCounter = jumpBuffer; else jumpBufferCounter -= Time.deltaTime;

        // ----- JUMP / WALL JUMP -----
        if (jumpBufferCounter > 0f)
        {
            if (coyoteCounter > 0f) { DoJump(); }
            else if (IsOnWall()) { DoWallJump(); }
        }

        HandleSlideInput(slidePressed);
        HandleVariableJumpGravity(jumpHeld);
    }

    void FixedUpdate()
    {
        DetectWalls();

        // Horizontal move
        float desiredX = inputX * moveSpeed;
        bool onGround = IsGrounded();
        currentAccel = onGround ? acceleration : acceleration * airControl;

        if (wallJumpLockCounter > 0f)
        {
            wallJumpLockCounter -= Time.fixedDeltaTime;
            desiredX = rb.linearVelocity.x; // lock
        }
        else
        {
            desiredX = Mathf.MoveTowards(rb.linearVelocity.x, desiredX, currentAccel * Time.fixedDeltaTime);
        }

        rb.linearVelocity = new Vector2(desiredX, rb.linearVelocity.y);

        // Fall clamp
        if (rb.linearVelocity.y < maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, maxFallSpeed);

        // Wall slide
        if (!onGround && IsOnWall() && rb.linearVelocity.y < wallSlideSpeed)
            wallStickCounter = wallStickTime;

        if (!onGround && IsOnWall())
        {
            if (wallStickCounter > 0f)
            {
                rb.linearVelocity = new Vector2(0f, Mathf.Max(rb.linearVelocity.y, wallSlideSpeed));
                wallStickCounter -= Time.fixedDeltaTime;
            }
        }

        // Ground slide update
        if (isSliding)
        {
            slideTimer -= Time.fixedDeltaTime;
            float vx = Mathf.MoveTowards(rb.linearVelocity.x, 0f, slideFriction * Time.fixedDeltaTime);
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
            if (slideTimer <= 0f || !IsGrounded()) EndSlide();
        }
    }

    // ===== Actions =====
    void DoJump()
    {
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    void DoWallJump()
    {
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;

        float dir = isWallLeft ? 1f : -1f; // wall on left → jump right
        Vector2 force = new Vector2(wallJumpDirection.x * dir, wallJumpDirection.y).normalized * wallJumpForce;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(force, ForceMode2D.Impulse);
        wallJumpLockCounter = wallJumpLockTime;
    }

    void HandleVariableJumpGravity(bool jumpHeld)
    {
        if (!jumpHeld && rb.linearVelocity.y > 0f)
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpGravityMultiplier - 1f) * Time.deltaTime;
    }

    void HandleSlideInput(bool slidePressed)
    {
        if (!isSliding && slidePressed && IsGrounded() && Mathf.Abs(rb.linearVelocity.x) >= slideMinSpeed)
        {
            isSliding = true;
            slideTimer = slideDuration;
            SetSlidingCollider();
        }
    }

    void EndSlide()
    {
        if (!isSliding) return;
        isSliding = false;
        SetStandingCollider();
    }

    // ===== Detection =====
    bool IsGrounded()
    {
        Collider2D hit = Physics2D.OverlapBox(
            groundCheck ? groundCheck.position : (Vector2)transform.position + Vector2.down * 0.9f,
            groundCheckSize, 0f, groundLayer);
        return isGrounded = (hit != null);
    }

    void DetectWalls()
    {
        Vector2 pos = transform.position;
        RaycastHit2D left = Physics2D.Raycast(pos, Vector2.left, wallCheckDistance, groundLayer);
        RaycastHit2D right = Physics2D.Raycast(pos, Vector2.right, wallCheckDistance, groundLayer);

        isWallLeft = left.collider != null;
        bool wallRight = right.collider != null;
        isOnWall = (isWallLeft || wallRight) && !IsGrounded() && rb.linearVelocity.y <= 0.1f;
    }

    bool IsOnWall() => isOnWall;

    void SetStandingCollider()
    {
        if (!capsule) return;
        capsule.size = new Vector2(capsule.size.x <= 0f ? 0.9f : capsule.size.x, standColliderHeight);
        capsule.offset = colliderOffsetStanding;
    }

    void SetSlidingCollider()
    {
        if (!capsule) return;
        capsule.size = new Vector2(capsule.size.x <= 0f ? 0.9f : capsule.size.x, slideColliderHeight);
        capsule.offset = colliderOffsetSliding;
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = Color.yellow;
        Vector3 gcPos = groundCheck ? groundCheck.position : transform.position + Vector3.down * 0.9f;
        Gizmos.DrawWireCube(gcPos, groundCheckSize);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.left * wallCheckDistance);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * wallCheckDistance);
    }
}
