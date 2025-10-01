using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.SceneManagement;

public class CharacterController2D : MonoBehaviour
{
    [Header("Move / Jump")]
    [SerializeField] private float m_JumpForce = 400f;
    [Range(0, .3f)][SerializeField] private float m_MovementSmoothing = .05f;
    [SerializeField] private bool m_AirControl = false;

    [Header("Layers & Checks")]
    [SerializeField] private LayerMask m_WhatIsGround;
    [SerializeField] private LayerMask m_WhatIsWall;
    [SerializeField] private Transform m_GroundCheck;
    [SerializeField] private Transform m_WallCheck;

    const float k_GroundedRadius = .2f;
    private bool m_Grounded;
    private Rigidbody2D m_Rigidbody2D;
    private bool m_FacingRight = true;
    private Vector3 velocity = Vector3.zero;
    private float limitFallSpeed = 25f;
    private float defaultGravityScale;

    [Header("Air / Dash")]
    public bool canDoubleJump = true;
    [SerializeField] private float m_DashForce = 25f;
    private bool canDash = true;
    private bool isDashing = false;

    [Header("Wall Input Rules")]
    [SerializeField] private bool requireTowardInputForWallJump = true; // ต้องกดทิศเข้ากำแพง + Space เพื่อกระโดด
    [SerializeField] private float wallTapImpulse = 6f;                 // แรงเดาะกำแพง (แนวนอน)
    [SerializeField] private float wallTapUpFactor = 0.2f;              // สัดส่วนแรงยกตอนเดาะ (เทียบ m_JumpForce)
    [SerializeField] private float wallTapCooldown = 0.12f;             // คูลดาวน์เดาะ
    private float wallTapTimer = 0f;

    [Header("Wall States (runtime)")]
    private bool m_IsWall = false;
    private bool isWallSliding = false;
    private bool isWallClinging = false;
    private bool oldWallSlidding = false;
    private bool canCheck = false;

    [Header("Post Wall-Jump")]
    [SerializeField] private float postWallJumpNoClingTime = 0.12f;
    private float postWallJumpTimer = 0f;

    // wall side: +1 = ขวา, -1 = ซ้าย
    private int wallSide = 0;

    [Header("Wall Cling / Slide")]
    [SerializeField] private float wallClingDuration = 0.25f;
    [SerializeField] private float wallSlideMaxFall = -5f;
    [SerializeField] private float wallStickForce = 5f;
    private float wallClingTimer = 0f;

    [Header("Anti Repeat & Coyote")]
    [SerializeField] private float sameWallRegrabCooldown = 0.12f;
    [SerializeField] private float sameWallMinSeparation = 0.2f;
    [SerializeField] private float wallCoyoteTime = 0.12f;

    private Collider2D lastWallCol = null;
    private float lastWallDetachTime = -999f;
    private Vector2 lastWallDetachPoint = Vector2.zero;
    private int lastWallSide = 0;
    private float wallCoyoteTimer = 0f;

    [Header("Wall Jump Rule")]
    public bool limitWallJumps = false;
    [Min(0)] public int maxWallJumps = 1;
    public bool requireGroundAfterWallJump = true;
    private int wallJumpsUsed = 0;
    private bool wallJumpLock = false;

    [Header("Push & Pull")]
    public KeyCode grabKey = KeyCode.E;
    public Transform handPoint;
    public Vector2 detectSize = new Vector2(0.9f, 1.2f);
    public float detectDistance = 0.5f;
    public LayerMask pushableMask;
    [Range(0.1f, 1f)] public float grabMoveMultiplier = 0.65f;
    public float jointBreakForce = 6000f;
    public float jointBreakTorque = 6000f;
    private bool isGrabbing = false;
    private FixedJoint2D joint;
    private Rigidbody2D grabbedRb;
    private Collider2D grabbedCol;
    private PhysicsMaterial2D grabbedColOriginalMat;

    public float life = 10f;
    public bool invincible = false;
    private bool canMove = true;

    private Animator animator;
    public ParticleSystem particleJumpUp;
    public ParticleSystem particleJumpDown;

    private float jumpWallStartX = 0;
    private float jumpWallDistX = 0;
    private bool limitVelOnWallJump = false;

    [Header("Events")]
    public UnityEvent OnFallEvent;
    public UnityEvent OnLandEvent;
    bool wasGrounded = false;

    // === Animator Param Names (ตั้งให้ชื่อใน Animator ตรงกับนี้) ===
    const string ANIM_SPEED = "Speed";
    const string ANIM_SPEEDX = "SpeedX";
    const string ANIM_SPEEDY = "SpeedY";
    const string ANIM_IDLE = "Idle";
    const string ANIM_GROUNDED = "IsOnGround";
    const string ANIM_JUMPING = "IsJumping";
    const string ANIM_DBLJUMP = "IsDoubleJumping";
    const string ANIM_JUMPUP = "JumpUp";
    const string ANIM_WCLING = "IsWallClinging";
    const string ANIM_WSLIDE = "IsWallSliding";
    const string ANIM_DASH = "IsDashing";
    const string ANIM_ATTACK = "IsAttacking";
    const string ANIM_HIT = "Hit";
    const string ANIM_DEAD = "IsDead";

    void Awake()
    {
        m_Rigidbody2D = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        if (!handPoint) handPoint = transform;
        defaultGravityScale = m_Rigidbody2D.gravityScale;
    }

    void Update()
    {
        // Toggle Grab (E)
        if (Input.GetKeyDown(grabKey))
        {
            if (!isGrabbing) TryGrab();
            else ReleaseGrab();
        }

        // (ตัวอย่าง) โจมตีด้วย Mouse0
        if (Input.GetKeyDown(KeyCode.Mouse0) && animator) animator.SetBool(ANIM_ATTACK, true);
        if (Input.GetKeyUp(KeyCode.Mouse0) && animator)
        {
            animator.SetBool(ANIM_ATTACK, false);
            animator.SetBool(ANIM_GROUNDED, m_Grounded);
        }

        // อัปเดตค่าความเร็ว/สถานะทั่วไปเข้ากับ Animator ทุกเฟรม
        if (animator)
        {
            Vector2 v = m_Rigidbody2D.velocity;
            animator.SetFloat(ANIM_SPEED, v.magnitude);
            animator.SetFloat(ANIM_SPEEDX, Mathf.Abs(v.x));
            animator.SetFloat(ANIM_SPEEDY, v.y);
            animator.SetBool(ANIM_IDLE, Mathf.Abs(v.x) < 0.01f && m_Grounded);
            animator.SetBool(ANIM_GROUNDED, m_Grounded);
        }
    }

    void FixedUpdate()
    {
        GroundCheck();
        WallProbe();

        if (postWallJumpTimer > 0f) postWallJumpTimer -= Time.fixedDeltaTime;
        if (wallTapTimer > 0f) wallTapTimer -= Time.fixedDeltaTime;

        // จำกัดความเร็วตกทั่วไป
        if (!m_Grounded && m_Rigidbody2D.velocity.y < -limitFallSpeed)
            m_Rigidbody2D.velocity = new Vector2(m_Rigidbody2D.velocity.x, -limitFallSpeed);

        // limiter แกน Y ชั่วครู่หลังวอลล์จัมพ์เพื่อไม่ให้พุ่งแปลก
        if (limitVelOnWallJump)
        {
            if (m_Rigidbody2D.velocity.y < -0.5f) limitVelOnWallJump = false;

            jumpWallDistX = (jumpWallStartX - transform.position.x) * transform.localScale.x;
            if (jumpWallDistX > 0.25f && m_Rigidbody2D.velocity.y > 0)
                m_Rigidbody2D.velocity = new Vector2(m_Rigidbody2D.velocity.x, 0);
            if (jumpWallDistX > 0.4f)
            {
                limitVelOnWallJump = false;
                m_Rigidbody2D.velocity = new Vector2(0, m_Rigidbody2D.velocity.y);
            }
        }

        if (wallCoyoteTimer > 0f) wallCoyoteTimer -= Time.fixedDeltaTime;
    }

    // ==== Public API ====
    public void Move(float move, bool jump, bool dash)
    {
        if (!canMove) return;

        // จับของ: ลดความเร็วและปิดแดช
        if (isGrabbing) { move *= grabMoveMultiplier; dash = false; }

        // เริ่มแดช
        if (dash && canDash && !isWallSliding && !isWallClinging)
            StartCoroutine(DashCooldown());

        if (isDashing)
        {
            m_Rigidbody2D.velocity = new Vector2(transform.localScale.x * m_DashForce, 0);
            return;
        }

        // เดิน/คุมอากาศ
        if (m_Grounded || m_AirControl)
        {
            Vector3 target = new Vector2(move * 10f, m_Rigidbody2D.velocity.y);
            m_Rigidbody2D.velocity = Vector3.SmoothDamp(m_Rigidbody2D.velocity, target, ref velocity, m_MovementSmoothing);

            if ((move > 0 && !m_FacingRight && !isWallSliding && !isGrabbing) ||
                (move < 0 && m_FacingRight && !isWallSliding && !isGrabbing))
                Flip();
        }

        // กระโดดพื้น
        if (!isGrabbing && m_Grounded && jump)
        {
            if (animator) { animator.SetBool(ANIM_JUMPING, true); animator.SetBool(ANIM_JUMPUP, true); }
            m_Grounded = false;
            m_Rigidbody2D.gravityScale = defaultGravityScale;
            m_Rigidbody2D.velocity = new Vector2(m_Rigidbody2D.velocity.x, 0f);
            m_Rigidbody2D.AddForce(Vector2.up * m_JumpForce, ForceMode2D.Impulse);
            canDoubleJump = true;
            if (particleJumpUp) particleJumpUp.Play();
            return;
        }

        // ดับเบิลจัมพ์ (ไม่อยู่บนกำแพง)
        if (!isGrabbing && !m_Grounded && jump && canDoubleJump && !isWallSliding && !isWallClinging)
        {
            canDoubleJump = false;
            m_Rigidbody2D.velocity = new Vector2(m_Rigidbody2D.velocity.x, 0f);
            m_Rigidbody2D.AddForce(Vector2.up * (m_JumpForce / 1.2f), ForceMode2D.Impulse);
            if (animator) animator.SetBool(ANIM_DBLJUMP, true);
            return;
        }

        // ลอจิกกำแพง
        HandleWallStates(move, jump, dash);
    }

    // ======================= Ground / Wall Checks =======================

    void GroundCheck()
    {
        bool was = m_Grounded;
        m_Grounded = false;

        var colliders = Physics2D.OverlapCircleAll(m_GroundCheck.position, k_GroundedRadius, m_WhatIsGround);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].gameObject != gameObject)
            {
                m_Grounded = true;

                if (!was)
                {
                    OnLandEvent?.Invoke();
                    wallJumpsUsed = 0;
                    wallJumpLock = false;
                    isWallClinging = false;
                    isWallSliding = false;
                    if (animator)
                    {
                        animator.SetBool(ANIM_WCLING, false);
                        animator.SetBool(ANIM_WSLIDE, false);
                        animator.SetBool(ANIM_JUMPING, false);
                        animator.SetBool(ANIM_DBLJUMP, false);
                        animator.SetBool(ANIM_JUMPUP, false);
                        animator.SetBool(ANIM_GROUNDED, true);
                        animator.SetBool(ANIM_IDLE, true);
                    }
                    if (particleJumpDown) particleJumpDown.Play();
                }
                break;
            }
        }
    }

    // Raycast ตรวจผนังทั้งซ้าย-ขวา (ไม่ย้ายตำแหน่ง WallCheck)
    void WallProbe()
    {
        m_IsWall = false;
        wallSide = 0;
        if (m_Grounded) return;

        float probeDist = k_GroundedRadius * 1.25f;
        Vector2 origin = m_WallCheck ? (Vector2)m_WallCheck.position : (Vector2)transform.position;

        RaycastHit2D hitR = Physics2D.Raycast(origin, Vector2.right, probeDist, m_WhatIsWall);
        RaycastHit2D hitL = Physics2D.Raycast(origin, Vector2.left, probeDist, m_WhatIsWall);

        RaycastHit2D hit;
        if (hitR && hitL) hit = (hitR.distance <= hitL.distance) ? hitR : hitL;
        else if (hitR) hit = hitR;
        else if (hitL) hit = hitL;
        else return;

        if (!CanAttachThisWall(hit)) return;

        m_IsWall = true;
        wallSide = hit.normal.x > 0 ? -1 : 1;
    }

    bool CanAttachThisWall(RaycastHit2D hit)
    {
        if (lastWallCol == null) return true;
        if (hit.collider != lastWallCol) return true;

        float dt = Time.time - lastWallDetachTime;
        float dist = Vector2.Distance(hit.point, lastWallDetachPoint);

        if (dt >= (sameWallRegrabCooldown * 0.5f)) return true;
        if (dist >= (sameWallMinSeparation * 0.5f)) return true;

        return false;
    }

    // =============== WALL CORE ===============
    void HandleWallStates(float move, bool jump, bool dash)
    {
        // กันกลับไปเกาะกำแพงทันทีหลังวอลล์จัมพ์/เดาะ
        if (postWallJumpTimer > 0f) return;

        // ออกจากสถานะกำแพงถ้าไม่ได้ชนแล้ว (พร้อม coyote)
        if ((isWallSliding || isWallClinging) && !m_IsWall)
        {
            ExitWallStatesForCoyote();
            return;
        }
        if (!m_IsWall) return;

        // อินพุตทิศจาก Move (-1 ซ้าย, 0 ไม่กด, +1 ขวา)
        int inputDir = (move > 0.1f) ? 1 : (move < -0.1f ? -1 : 0);

        // เข้าสู่ Cling ก่อน แล้วค่อย Slide
        if ((!oldWallSlidding && m_Rigidbody2D.velocity.y < 0f) || isDashing)
        {
            isWallClinging = true;
            isWallSliding = false;
            wallClingTimer = wallClingDuration;
            oldWallSlidding = true;
            canCheck = false;

            StartCoroutine(WaitToCheck(0.08f));
            canDoubleJump = true;

            if (animator)
            {
                animator.SetBool(ANIM_WCLING, true);
                animator.SetBool(ANIM_WSLIDE, false);
            }
        }

        isDashing = false;

        // CLING: กดลงเบา ๆ ไม่ลอยค้าง
        if (isWallClinging)
        {
            m_Rigidbody2D.gravityScale = 0f;
            Vector2 v = m_Rigidbody2D.velocity;
            v.x = Mathf.MoveTowards(v.x, 0f, 20f * Time.fixedDeltaTime);
            if (v.y > -0.25f) v.y = -0.25f;
            m_Rigidbody2D.velocity = v;

            wallClingTimer -= Time.fixedDeltaTime;
            if (wallClingTimer <= 0f)
            {
                isWallClinging = false;
                isWallSliding = true;
                m_Rigidbody2D.gravityScale = defaultGravityScale * 0.6f;
                if (animator)
                {
                    animator.SetBool(ANIM_WCLING, false);
                    animator.SetBool(ANIM_WSLIDE, true);
                }
            }
        }

        // SLIDE: จำกัดความเร็วตก + ดันเข้ากำแพงเบา ๆ
        if (isWallSliding)
        {
            Vector2 v = m_Rigidbody2D.velocity;
            if (v.y < wallSlideMaxFall) v.y = wallSlideMaxFall;
            v.x = Mathf.Lerp(v.x, -wallSide * 2f, Time.fixedDeltaTime * wallStickForce);
            m_Rigidbody2D.velocity = v;
        }

        // ===== อินพุตตามที่ต้องการ =====
        bool pressingTowardWall = (inputDir != 0 && inputDir == wallSide);
        bool allowedByLock = !(requireGroundAfterWallJump && wallJumpLock);
        bool allowedByCount = !(limitWallJumps && wallJumpsUsed >= maxWallJumps);
        bool inWallStateOrCoyote = (isWallClinging || isWallSliding || wallCoyoteTimer > 0f);

        // 1) A/D + Space → วอลล์จัมพ์ (ถ้าตั้งให้ต้องกดเข้ากำแพง)
        if (jump && inWallStateOrCoyote && !isGrabbing && allowedByLock && allowedByCount)
        {
            if (!requireTowardInputForWallJump || pressingTowardWall)
            {
                DoWallJump();
                return;
            }
        }

        // 2) A/D อย่างเดียว → เดาะกำแพง (ดีดออกเล็กน้อย)
        if (!jump && (isWallClinging || isWallSliding) && inputDir != 0 && wallTapTimer <= 0f)
        {
            DoWallTap();
            return;
        }

        // 3) Dash จากกำแพง
        if (dash && canDash)
        {
            ExitWallStates();
            StartCoroutine(DashCooldown());
        }
    }

    void DoWallTap()
    {
        // ออกจากสถานะกำแพงก่อนเดาะ
        ExitWallStates();

        float xImpulse = -wallSide * wallTapImpulse;
        float yImpulse = m_JumpForce * wallTapUpFactor;

        m_Rigidbody2D.gravityScale = defaultGravityScale;
        m_Rigidbody2D.velocity = Vector2.zero;
        m_Rigidbody2D.AddForce(new Vector2(xImpulse, yImpulse), ForceMode2D.Impulse);

        // กันกลับไปเกาะทันที + คูลดาวน์เดาะ
        postWallJumpTimer = Mathf.Max(postWallJumpNoClingTime * 0.6f, 0.08f);
        wallTapTimer = wallTapCooldown;

        // เดาะไม่กินโควตา Wall Jump และยัง Double Jump ได้
        canDoubleJump = true;

        if (animator)
        {
            animator.SetBool(ANIM_WCLING, false);
            animator.SetBool(ANIM_WSLIDE, false);
            animator.SetBool(ANIM_JUMPING, true);
            animator.SetBool(ANIM_JUMPUP, true);
        }

        lastWallDetachTime = Time.time;
        lastWallSide = wallSide;
        lastWallCol = ProbeWallColliderNearCheckPoint();
        lastWallDetachPoint = m_WallCheck ? (Vector2)m_WallCheck.position : (Vector2)transform.position;

        wallCoyoteTimer = Mathf.Max(wallCoyoteTime * 0.8f, 0.06f);
    }

    void DoWallJump()
    {
        if (animator) { animator.SetBool(ANIM_JUMPING, true); animator.SetBool(ANIM_JUMPUP, true); }

        ExitWallStates();

        m_Rigidbody2D.gravityScale = defaultGravityScale;
        m_Rigidbody2D.velocity = Vector2.zero;

        float xForce = wallSide * m_JumpForce * 1.15f;
        m_Rigidbody2D.AddForce(new Vector2(xForce, m_JumpForce), ForceMode2D.Impulse);

        jumpWallStartX = transform.position.x;
        limitVelOnWallJump = true;
        canDoubleJump = true;

        postWallJumpTimer = postWallJumpNoClingTime;
        canMove = false; StartCoroutine(WaitToMove(0.1f));

        wallJumpsUsed++;
        if (requireGroundAfterWallJump) wallJumpLock = true;

        lastWallDetachTime = Time.time;
        lastWallSide = wallSide;
        lastWallCol = ProbeWallColliderNearCheckPoint();
        lastWallDetachPoint = m_WallCheck ? (Vector2)m_WallCheck.position : (Vector2)transform.position;

        wallCoyoteTimer = wallCoyoteTime;
    }

    void ExitWallStatesForCoyote()
    {
        ExitWallStates();
        wallCoyoteTimer = wallCoyoteTime;
    }

    void ExitWallStates()
    {
        isWallClinging = false;
        isWallSliding = false;
        if (animator)
        {
            animator.SetBool(ANIM_WCLING, false);
            animator.SetBool(ANIM_WSLIDE, false);
        }
        oldWallSlidding = false;
        canDoubleJump = true;
        m_Rigidbody2D.gravityScale = defaultGravityScale;

        Collider2D col = ProbeWallColliderNearCheckPoint();
        if (col != null)
        {
            lastWallCol = col;
            lastWallDetachTime = Time.time;
            lastWallDetachPoint = m_WallCheck ? (Vector2)m_WallCheck.position : (Vector2)transform.position;
            lastWallSide = wallSide;
        }
    }

    Collider2D ProbeWallColliderNearCheckPoint()
    {
        if (!m_WallCheck) return null;
        return Physics2D.OverlapCircle(m_WallCheck.position, k_GroundedRadius * 1.1f, m_WhatIsWall);
    }

    // ======================= Grab =======================

    void TryGrab()
    {
        if (!m_Grounded) return;

        float facingSign = m_FacingRight ? 1f : -1f;
        Vector2 center = (Vector2)handPoint.position + new Vector2(facingSign * detectDistance, 0f);

        var hit = Physics2D.OverlapBox(center, detectSize, 0f, pushableMask);
        if (!hit) return;

        grabbedRb = hit.attachedRigidbody;
        grabbedCol = hit.GetComponent<Collider2D>();
        if (!grabbedRb) return;

        if (grabbedCol) grabbedColOriginalMat = grabbedCol.sharedMaterial;

        joint = gameObject.AddComponent<FixedJoint2D>();
        joint.enableCollision = false;
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedBody = grabbedRb;
        joint.anchor = transform.InverseTransformPoint(handPoint.position);
        joint.connectedAnchor = grabbedRb.transform.InverseTransformPoint(handPoint.position);
        joint.breakForce = jointBreakForce;
        joint.breakTorque = jointBreakTorque;

        isGrabbing = true;
        isWallSliding = false;
    }

    void ReleaseGrab()
    {
        if (joint) Destroy(joint);
        if (grabbedCol) grabbedCol.sharedMaterial = grabbedColOriginalMat;

        grabbedRb = null;
        grabbedCol = null;
        grabbedColOriginalMat = null;
        isGrabbing = false;
    }

    void OnJointBreak2D(Joint2D j) { if (j == joint) ReleaseGrab(); }

    // ======================= Utils / Coroutines =======================

    private void Flip()
    {
        if (isGrabbing) return;
        m_FacingRight = !m_FacingRight;
        Vector3 s = transform.localScale; s.x *= -1; transform.localScale = s;
    }

    IEnumerator DashCooldown()
    {
        if (animator) animator.SetBool(ANIM_DASH, true);
        isDashing = true; canDash = false;
        yield return new WaitForSeconds(0.1f);
        isDashing = false;
        if (animator) animator.SetBool(ANIM_DASH, false);
        yield return new WaitForSeconds(0.5f);
        canDash = true;
    }

    IEnumerator Stun(float t) { canMove = false; yield return new WaitForSeconds(t); canMove = true; }
    IEnumerator MakeInvincible(float t) { invincible = true; yield return new WaitForSeconds(t); invincible = false; }
    IEnumerator WaitToMove(float t) { yield return new WaitForSeconds(t); canMove = true; }
    IEnumerator WaitToCheck(float t) { yield return new WaitForSeconds(t); canCheck = true; }

    public void ApplyDamage(float damage, Vector3 position)
    {
        if (!invincible)
        {
            if (animator) animator.SetBool(ANIM_HIT, true); // แนะนำปลดใน Animation Event
            life -= damage;
        }
    }
}