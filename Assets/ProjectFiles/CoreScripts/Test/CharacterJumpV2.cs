using UnityEngine;

[AddComponentMenu("Krakxn/Character Jump V2")]
public class CharacterJumpV2 : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;

    [Header("Ground / Wall Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] private LayerMask whatIsWall;
    [SerializeField] private float groundRadius = 0.15f;
    [SerializeField] private float wallRadius = 0.15f;

    [Header("Jump Forces")]
    [SerializeField] private float jumpForce = 900f;       // กระโดดหลัก (Impulse)
    [SerializeField] private float doubleJumpScale = 1.0f; // 1 = เท่ากับ jumpForce

    [Header("Double Jump")]
    [SerializeField] private int maxDoubleJumps = 1;       // จำนวนดับเบิลกลางอากาศ

    [Header("Wall Jump / Cling / Slide")]
    [SerializeField] private int wallJumpLimit = 2;        // จำนวน wall-jump ต่อช่วงลอย
    [SerializeField] private float wallClingTime = 0.25f;  // เวลาที่ "เกาะ" รอตัดสินใจ
    [SerializeField] private float wallSlideMaxFallSpeed = -8f; // จำกัดความเร็วตกตอนสไลด์ (ลบ)
    [SerializeField] private float wallJumpHorizontal = 8f;     // แนวนอนตอนดีดจากกำแพง (Impulse)
    [SerializeField] private float wallJumpVertical = 9f;     // แนวตั้งตอนดีดจากกำแพง (Impulse)

    [Header("Animator (Optional)")]
    [SerializeField] private bool useAnimator = true;
    [SerializeField] private string paramIsJumping = "IsJumping";
    [SerializeField] private string paramIsDoubleJumping = "IsDoubleJumping";
    [SerializeField] private string paramIsWallClinging = "IsWallClinging";

    [Header("Input (Optional)")]
    [Tooltip("ใช้ Input (เก่า) จาก UnityEngine.Input หรือจะปิดแล้วเรียก RequestJump() เองจากสคริปต์อินพุตของคุณก็ได้")]
    [SerializeField] private bool useLegacyInput = false;
    [SerializeField] private string jumpButton = "Jump";

    // --- runtime state ---
    public bool IsGrounded { get; private set; }
    public bool IsWall { get; private set; }
    public bool IsWallClinging { get; private set; }
    public bool IsWallSliding { get; private set; }

    [Tooltip("ถ้าคุณมีระบบ Grab แยก สามารถตั้งค่านี้จากภายนอกเพื่อบล็อกการกระโดดได้")]
    public bool IsGrabbing { get; set; }

    private int remainingDoubleJumps;
    private int remainingWallJumps;
    private float wallClingTimer;
    private int wallDir; // -1 = กำแพงซ้าย, +1 = กำแพงขวา
    private float defaultGravityScale;
    private bool jumpRequested;

    // --- helper hashes ---
    private int hIsJumping, hIsDoubleJumping, hIsWallClinging;

    void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponentInChildren<Animator>();
    }

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (useAnimator && !animator) animator = GetComponentInChildren<Animator>();

        defaultGravityScale = rb ? rb.gravityScale : 3f;
        if (rb && rb.gravityScale < 0.5f)
        {
            rb.gravityScale = 3f;
            defaultGravityScale = 3f;
            Debug.LogWarning("[JumpV2] gravityScale too low; clamped to 3");
        }

        remainingDoubleJumps = maxDoubleJumps;
        remainingWallJumps = wallJumpLimit;

        if (useAnimator && animator)
        {
            hIsJumping = Animator.StringToHash(paramIsJumping);
            hIsDoubleJumping = Animator.StringToHash(paramIsDoubleJumping);
            hIsWallClinging = Animator.StringToHash(paramIsWallClinging);
        }
    }

    void Update()
    {
        if (useLegacyInput && Input.GetButtonDown(jumpButton))
            jumpRequested = true;
    }

    void FixedUpdate()
    {
        // --- 1) Sensing: ground & wall ---
        bool wasGrounded = IsGrounded;
        IsGrounded = false;

        if (groundCheck)
        {
            var cols = Physics2D.OverlapCircleAll(groundCheck.position, groundRadius, whatIsGround);
            for (int i = 0; i < cols.Length; i++)
            {
                // กันคอลลิเดอร์ในร่างตัวเอง
                if (cols[i].transform.root == transform.root) continue;
                IsGrounded = true;
                break;
            }
        }

        // Detect wall
        IsWall = false;
        if (wallCheck)
        {
            var wallCols = Physics2D.OverlapCircleAll(wallCheck.position, wallRadius, whatIsWall);
            for (int i = 0; i < wallCols.Length; i++)
            {
                if (wallCols[i].transform.root == transform.root) continue;
                IsWall = true;
                // คำนวณทิศกำแพงจากตำแหน่ง wallCheck เทียบตัวเรา
                wallDir = (wallCheck.position.x < transform.position.x) ? -1 : +1;
                break;
            }
        }

        // Landing edge → reset counters
        if (!wasGrounded && IsGrounded)
        {
            remainingDoubleJumps = maxDoubleJumps;
            remainingWallJumps = wallJumpLimit;
            ExitWallStates();

            if (useAnimator && animator)
            {
                animator.SetBool(hIsJumping, false);
                animator.SetBool(hIsDoubleJumping, false);
            }
        }

        // --- 2) Enter wall states when airborne ---
        if (!IsGrounded)
        {
            if (IsWall)
            {
                if (remainingWallJumps <= 0)
                {
                    EnterWallSlide();
                }
                else
                {
                    if (!IsWallClinging && !IsWallSliding)
                    {
                        IsWallClinging = true;
                        wallClingTimer = wallClingTime;

                        if (rb)
                        {
                            rb.gravityScale = 0f;
                            var v = rb.linearVelocity;
                            v.y = 0f;
                            rb.linearVelocity = v;
                        }

                        if (useAnimator && animator)
                            animator.SetBool(hIsWallClinging, true);
                    }
                }
            }
            else
            {
                if (IsWallClinging || IsWallSliding) ExitWallStates();
            }
        }

        // --- 3) Tick wall cling timer / clamp slide fall speed ---
        if (IsWallClinging)
        {
            wallClingTimer -= Time.fixedDeltaTime;
            if (wallClingTimer <= 0f) EnterWallSlide();
        }

        if (IsWallSliding && rb)
        {
            var v = rb.linearVelocity;
            if (v.y < wallSlideMaxFallSpeed)
            {
                v.y = wallSlideMaxFallSpeed;
                rb.linearVelocity = v;
            }
        }

        // --- 4) Consume jump request ---
        if (jumpRequested)
        {
            HandleJump();
            jumpRequested = false;
        }
    }

    /// <summary>
    /// เรียกจากสคริปต์อินพุตของคุณ (Input System ใหม่) เมื่อผู้เล่น "กด" กระโดด
    /// </summary>
    public void RequestJump()
    {
        jumpRequested = true;
    }

    private void HandleJump()
    {
        if (IsGrabbing) return; // เคารพระบบ Grab ของคุณ ถ้ามี

        if (IsGrounded)
        {
            DoGroundJump();
            return;
        }

        if (IsWallClinging && remainingWallJumps > 0)
        {
            DoWallJump();
            return;
        }

        if (!IsGrounded && !IsWallSliding && remainingDoubleJumps > 0)
        {
            DoDoubleJump();
            return;
        }
        // ถ้าอยู่ใน slide แต่หมดสิทธิ์ wall-jump แล้ว → ไม่ทำอะไร (ไหลลง)
    }

    private void DoGroundJump()
    {
        if (!rb) return;

        var v = rb.linearVelocity;
        if (v.y < 0f) v.y = 0f; // กันแรงลงทับ
        rb.linearVelocity = v;

        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);

        remainingDoubleJumps = maxDoubleJumps;
        remainingWallJumps = wallJumpLimit;

        ExitWallStates();

        if (useAnimator && animator)
        {
            animator.SetBool(hIsJumping, true);
            animator.SetBool(hIsDoubleJumping, false);
        }
    }

    private void DoDoubleJump()
    {
        if (!rb) return;

        var v = rb.linearVelocity;
        v.y = Mathf.Max(0f, v.y);
        rb.linearVelocity = v;

        rb.AddForce(Vector2.up * (jumpForce * doubleJumpScale), ForceMode2D.Impulse);
        remainingDoubleJumps--;

        if (useAnimator && animator)
        {
            animator.SetBool(hIsDoubleJumping, true);
        }
    }

    private void DoWallJump()
    {
        if (!rb) return;

        IsWallClinging = false;
        IsWallSliding = false;
        wallClingTimer = 0f;

        // ดีดออกจากกำแพง: ถ้ากำแพงอยู่ซ้าย (wallDir=-1) -> ดีดไปทาง +X
        int pushDir = -wallDir;

        var v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        rb.gravityScale = defaultGravityScale;
        rb.AddForce(new Vector2(pushDir * wallJumpHorizontal, wallJumpVertical), ForceMode2D.Impulse);

        remainingWallJumps--;
        remainingDoubleJumps = maxDoubleJumps; // อนุญาต DJ ต่อได้หลังดีดออก

        if (useAnimator && animator)
        {
            animator.SetBool(hIsWallClinging, false);
            animator.SetBool(hIsJumping, true);
        }
    }

    private void EnterWallSlide()
    {
        IsWallClinging = false;
        IsWallSliding = true;
        if (rb) rb.gravityScale = defaultGravityScale;
        if (useAnimator && animator) animator.SetBool(hIsWallClinging, false);
    }

    private void ExitWallStates()
    {
        IsWallClinging = false;
        IsWallSliding = false;
        wallClingTimer = 0f;
        if (rb) rb.gravityScale = defaultGravityScale;
        if (useAnimator && animator) animator.SetBool(hIsWallClinging, false);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        if (groundCheck) Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
        Gizmos.color = Color.cyan;
        if (wallCheck) Gizmos.DrawWireSphere(wallCheck.position, wallRadius);
    }
#endif
}
