using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.SceneManagement;

public class CharacterController2D : MonoBehaviour
{

    public enum DeathLoadMode { ReloadCurrent, ByName, ByBuildIndex, None }


    [SerializeField] private float m_JumpForce = 400f;
    [Range(0, .3f)][SerializeField] private float m_MovementSmoothing = .05f;
    [SerializeField] private bool m_AirControl = false;
    [SerializeField] private LayerMask m_WhatIsGround;
    [SerializeField] private LayerMask m_WhatIsWall;   // ★ แยกเลเยอร์ "กำแพง"
    [SerializeField] private Transform m_GroundCheck;
    [SerializeField] private Transform m_WallCheck;

    const float k_GroundedRadius = .2f;
    private bool m_Grounded;
    private Rigidbody2D m_Rigidbody2D;
    private bool m_FacingRight = true;
    private Vector3 velocity = Vector3.zero;
    private float limitFallSpeed = 25f;

    public bool canDoubleJump = true;
    [SerializeField] private float m_DashForce = 25f;
    private bool canDash = true;
    private bool isDashing = false;
    private bool m_IsWall = false;
    private bool isWallSliding = false;
    private bool oldWallSlidding = false;
    private float prevVelocityX = 0f;
    private bool canCheck = false;

    // ===== Wall Jump options =====
    [Header("Wall Jump Limit")]
    public bool limitWallJumps = false;
    [Min(0)] public int maxWallJumps = 1;
    private int wallJumpsUsed = 0;

    [Header("Wall Jump Rule")]
    public bool requireGroundAfterWallJump = true; // ต้องลงพื้นก่อนจึงจะวอลล์จัมพ์ได้อีก
    private bool wallJumpLock = false;             // ล็อกจนลงพื้น

    // ==== Wall Cling (เกาะกำแพงก่อนสไลด์) ====
    [Header("Wall Cling")]
    [SerializeField] private float wallClingDuration = 1.0f; // เวลาที่จะ "เกาะ" ก่อนจะไหลลง
    private float wallClingTimer = 0f;
    private bool isWallClinging = false;
    private float defaultGravityScale;

    // ===== Push & Pull (Grab) =====
    [Header("Push & Pull")]
    public KeyCode grabKey = KeyCode.E;                // ปุ่มจับ/ปล่อย (Old Input)
    public Transform handPoint;                        // จุดจับ
    public Vector2 detectSize = new Vector2(0.9f, 1.2f);
    public float detectDistance = 0.5f;
    public LayerMask pushableMask;                     // เลเยอร์ "Pushable"
    [Range(0.1f, 1f)] public float grabMoveMultiplier = 0.65f; // เดินช้าลงระหว่างจับ
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


    [Header("Death / Reload Scene")]
    [SerializeField] DeathLoadMode deathLoadMode = DeathLoadMode.ReloadCurrent;
    [SerializeField] string deathSceneName = "";       // ใช้เมื่อเลือก ByName
    [SerializeField] int deathSceneBuildIndex = -1;    // ใช้เมื่อเลือก ByBuildIndex
    [SerializeField] LoadSceneMode deathLoadModeType = LoadSceneMode.Single; // Single/Additive


    private Animator animator;
    public ParticleSystem particleJumpUp;
    public ParticleSystem particleJumpDown;

    private float jumpWallStartX = 0;
    private float jumpWallDistX = 0;
    private bool limitVelOnWallJump = false;

    [Header("Events")]
    [Space] public UnityEvent OnFallEvent;
    public UnityEvent OnLandEvent;

    [System.Serializable] public class BoolEvent : UnityEvent<bool> { }

    void Awake()
    {
        m_Rigidbody2D = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        if (!handPoint) handPoint = transform;

        defaultGravityScale = m_Rigidbody2D.gravityScale; // จำค่า g เดิมไว้
    }

    void Update()
    {
        // Toggle Grab (ปุ่ม E)
        if (Input.GetKeyDown(grabKey))
        {
            if (!isGrabbing) TryGrab();
            else ReleaseGrab();
        }
    }

    void FixedUpdate()
    {
        bool wasGrounded = m_Grounded;
        m_Grounded = false;

        // Ground check
        Collider2D[] colliders = Physics2D.OverlapCircleAll(m_GroundCheck.position, k_GroundedRadius, m_WhatIsGround);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].gameObject != gameObject)
            {
                m_Grounded = true;
                if (!wasGrounded)
                {
                    OnLandEvent.Invoke();
                    wallJumpsUsed = 0;
                    m_Rigidbody2D.gravityScale = defaultGravityScale;
                    wallJumpLock = false;           // ปลดล็อกวอลล์จัมพ์เมื่อแตะพื้น
                    isWallClinging = false;         // กันค้างสถานะ
                    if (animator) animator.SetBool("IsWallClinging", false);

                    if (!m_IsWall && !isDashing)
                    {
                        if (animator)
                        {
                            animator.SetBool("IsJumping", false);
                            animator.SetBool("IsDoubleJumping", false);
                            animator.SetBool("JumpUp", false);
                        }
                        if (particleJumpDown) particleJumpDown.Play();
                    }
                }
            }
        }

        m_IsWall = false;

        if (!m_Grounded)
        {
            //OnFallEvent.Invoke();
            // ★ ใช้เลเยอร์กำแพง
            Collider2D[] collidersWall = Physics2D.OverlapCircleAll(m_WallCheck.position, k_GroundedRadius, m_WhatIsWall);
            for (int i = 0; i < collidersWall.Length; i++)
            {
                if (collidersWall[i].gameObject != null)
                {
                    isDashing = false;
                    m_IsWall = true;
                }
            }
            prevVelocityX = m_Rigidbody2D.linearVelocity.x;
        }

        // จำกัดความเร็วตก (คุมกลางอากาศด้วย)
        if (!m_Grounded && m_Rigidbody2D.linearVelocity.y < -limitFallSpeed)
            m_Rigidbody2D.linearVelocity = new Vector2(m_Rigidbody2D.linearVelocity.x, -limitFallSpeed);

        if (limitVelOnWallJump)
        {
            if (m_Rigidbody2D.linearVelocity.y < -0.5f)
                limitVelOnWallJump = false;

            jumpWallDistX = (jumpWallStartX - transform.position.x) * transform.localScale.x;
            if (jumpWallDistX > 0.25f)
            {
                if (m_Rigidbody2D.linearVelocity.y > 0)
                    m_Rigidbody2D.linearVelocity = new Vector2(m_Rigidbody2D.linearVelocity.x, 0);
            }
            if (jumpWallDistX > 0.4f)
            {
                limitVelOnWallJump = false;
                m_Rigidbody2D.linearVelocity = new Vector2(0, m_Rigidbody2D.linearVelocity.y);
            }
        }
    }

    public void Move(float move, bool jump, bool dash)
    {
        if (!canMove) return;

        // ขณะจับอยู่: เดินช้าลง + ปิด dash
        if (isGrabbing)
        {
            move *= grabMoveMultiplier;
            dash = false;
        }

        // Dash (กันเริ่มแดชระหว่างกำลังเกาะ/สไลด์)
        if (dash && canDash && !isWallSliding && !isWallClinging)
        {
            StartCoroutine(DashCooldown());
        }
        if (isDashing)
        {
            m_Rigidbody2D.linearVelocity = new Vector2(transform.localScale.x * m_DashForce, 0);
        }
        // Walk / Air control
        else if (m_Grounded || m_AirControl)
        {
            Vector3 targetVelocity = new Vector2(move * 10f, m_Rigidbody2D.linearVelocity.y);
            m_Rigidbody2D.linearVelocity = Vector3.SmoothDamp(m_Rigidbody2D.linearVelocity, targetVelocity, ref velocity, m_MovementSmoothing);

            // ★ ห้าม Flip ตอนกำลัง Grab
            if ((move > 0 && !m_FacingRight && !isWallSliding && !isGrabbing) ||
                (move < 0 && m_FacingRight && !isWallSliding && !isGrabbing))
            {
                Flip();
            }
        }

        // Jump / Double Jump —> บล็อกทั้งหมดเมื่อกำลังจับ
        if (!isGrabbing && m_Grounded && jump)
        {
            if (animator)
            {
                animator.SetBool("IsJumping", true);
                animator.SetBool("JumpUp", true);
            }
            m_Grounded = false;

            // ทำให้ takeoff เท่ากันทุกครั้ง
            m_Rigidbody2D.gravityScale = defaultGravityScale;
            m_Rigidbody2D.linearVelocity = new Vector2(m_Rigidbody2D.linearVelocity.x, 0f);
            m_Rigidbody2D.AddForce(Vector2.up * m_JumpForce, ForceMode2D.Impulse);
            canDoubleJump = true;
            if (particleJumpUp) particleJumpUp.Play();
        }
        else if (!isGrabbing && !m_Grounded && jump && canDoubleJump && !isWallSliding)
        {
            canDoubleJump = false;
            m_Rigidbody2D.linearVelocity = new Vector2(m_Rigidbody2D.linearVelocity.x, 0f);
            m_Rigidbody2D.AddForce(Vector2.up * (m_JumpForce / 1.2f), ForceMode2D.Impulse); // ★ ใช้ Impulse
            if (animator) animator.SetBool("IsDoubleJumping", true);
        }
        // Wall logic
        else if (m_IsWall && !m_Grounded)
        {
            // เข้าโหมด "เกาะกำแพง" ก่อน (1 วินาที) แล้วค่อยเข้าสไลด์
            if ((!oldWallSlidding && m_Rigidbody2D.linearVelocity.y < 0f) || isDashing)
            {
                isWallClinging = true;           // ★ เปลี่ยนเป็นเกาะก่อน
                isWallSliding = false;
                wallClingTimer = wallClingDuration;

                oldWallSlidding = true;
                canCheck = false;

                // หันหน้าออกจากกำแพง + ย้ายจุดเช็คฝั่งตรงข้าม
                m_WallCheck.localPosition = new Vector3(-m_WallCheck.localPosition.x, m_WallCheck.localPosition.y, 0);
                if (!isGrabbing) Flip();

                StartCoroutine(WaitToCheck(0.1f));
                canDoubleJump = true;

                if (animator)
                {
                    animator.SetBool("IsWallClinging", true);
                    animator.SetBool("IsWallSliding", false);
                }
            }

            isDashing = false;

            // ===== Wall Cling (หยุด/ชะลอตก ก่อนเข้าสไลด์) =====
            if (isWallClinging)
            {
                // หยุดตก และตรึงไว้
                m_Rigidbody2D.gravityScale = 0f;
                m_Rigidbody2D.linearVelocity = Vector2.zero; // เกาะนิ่ง ๆ

                wallClingTimer -= Time.deltaTime;            // ★ ใช้ deltaTime
                if (wallClingTimer <= 0f)
                {
                    // หมดเวลาการเกาะ -> เข้าโหมดสไลด์ตามเดิม
                    isWallClinging = false;
                    isWallSliding = true;

                    // ตั้ง g สำหรับสไลด์นุ่ม ๆ (จะคืนตอนออก)
                    m_Rigidbody2D.gravityScale = defaultGravityScale * 0.6f;

                    if (animator)
                    {
                        animator.SetBool("IsWallClinging", false);
                        animator.SetBool("IsWallSliding", true);
                    }
                }
            }

            // ขณะสไลด์
            if (isWallSliding)
            {
                // รักษาความเร็วตกไม่ให้พุ่ง
                if (m_Rigidbody2D.linearVelocity.y < -3f)
                {
                    m_Rigidbody2D.linearVelocity = new Vector2(-transform.localScale.x * 2f, -5f);
                }
            }
            else
            {
                // ถ้ายังไม่สไลด์ (กำลังเกาะ) g = 0 อยู่แล้ว
            }

            // Wall Jump —> บล็อกเมื่อจับอยู่ หรือยังไม่ลงพื้นจากครั้งก่อน / หรือครบโควตา
            if (jump && (isWallClinging || isWallSliding) && !isGrabbing)
            {
                if ((requireGroundAfterWallJump && wallJumpLock) ||
                    (limitWallJumps && wallJumpsUsed >= maxWallJumps))
                {
                    // do nothing (ล็อกไว้)
                }
                else
                {
                    if (animator)
                    {
                        animator.SetBool("IsJumping", true);
                        animator.SetBool("JumpUp", true);
                    }

                    // ออกจากสถานะกำแพงทั้งหมดก่อนออกแรง
                    isWallClinging = false;
                    isWallSliding = false;

                    // รีเซ็ตความเร็วแล้วกระโดดออกจากกำแพง
                    m_Rigidbody2D.gravityScale = defaultGravityScale;
                    m_Rigidbody2D.linearVelocity = Vector2.zero;
                    m_Rigidbody2D.AddForce(new Vector2(transform.localScale.x * m_JumpForce * 1.2f, m_JumpForce), ForceMode2D.Impulse);

                    jumpWallStartX = transform.position.x;
                    limitVelOnWallJump = true;
                    canDoubleJump = true;

                    if (animator) animator.SetBool("IsWallSliding", false);
                    oldWallSlidding = false;
                    m_WallCheck.localPosition = new Vector3(Mathf.Abs(m_WallCheck.localPosition.x), m_WallCheck.localPosition.y, 0);

                    canMove = false;
                    StartCoroutine(WaitToMove(0.1f));

                    wallJumpsUsed++;
                    if (requireGroundAfterWallJump) wallJumpLock = true;
                }
            }
            else if (dash && canDash)
            {
                // ★ แดชจากกำแพง: หลุดเกาะ/สไลด์ก่อน
                isWallClinging = false;
                isWallSliding = false;
                if (animator) animator.SetBool("IsWallSliding", false);
                oldWallSlidding = false;
                m_WallCheck.localPosition = new Vector3(Mathf.Abs(m_WallCheck.localPosition.x), m_WallCheck.localPosition.y, 0);
                canDoubleJump = true;
                m_Rigidbody2D.gravityScale = defaultGravityScale;
                StartCoroutine(DashCooldown());
            }
        }
        else if ((isWallSliding || isWallClinging) && !m_IsWall && canCheck)
        {
            // ออกจากกำแพง
            isWallClinging = false;
            isWallSliding = false;
            if (animator)
            {
                animator.SetBool("IsWallClinging", false);
                animator.SetBool("IsWallSliding", false);
            }
            oldWallSlidding = false;
            m_WallCheck.localPosition = new Vector3(Mathf.Abs(m_WallCheck.localPosition.x), m_WallCheck.localPosition.y, 0);
            canDoubleJump = true;
            m_Rigidbody2D.gravityScale = defaultGravityScale;
        }
        else
        {
            // นอกสถานะกำแพงทั้งหมด: คืน g ให้เดิม (กันค้าง)
            if (!m_Grounded && !isWallSliding && !isWallClinging && !isDashing)
                m_Rigidbody2D.gravityScale = defaultGravityScale;
        }
    }

    // ===== Push & Pull - core =====
    void TryGrab()
    {
        // ห้ามเริ่มจับกลางอากาศ (ถ้าต้องการให้จับกลางอากาศ ให้เอาบรรทัดนี้ออก)
        if (!m_Grounded) return;

        // ตรวจกล่องตรงหน้าตามด้านที่หัน
        float facingSign = m_FacingRight ? 1f : -1f;
        Vector2 center = (Vector2)handPoint.position + new Vector2(facingSign * detectDistance, 0f);

        var hit = Physics2D.OverlapBox(center, detectSize, 0f, pushableMask);
        if (!hit) return;

        grabbedRb = hit.attachedRigidbody;
        grabbedCol = hit.GetComponent<Collider2D>();
        if (!grabbedRb) return;

        // เก็บวัสดุเดิม (ไว้คืนตอนปล่อย)
        if (grabbedCol)
            grabbedColOriginalMat = grabbedCol.sharedMaterial;

        joint = gameObject.AddComponent<FixedJoint2D>();
        joint.enableCollision = false;
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedBody = grabbedRb;
        joint.anchor = transform.InverseTransformPoint(handPoint.position);
        joint.connectedAnchor = grabbedRb.transform.InverseTransformPoint(handPoint.position);
        joint.breakForce = jointBreakForce;
        joint.breakTorque = jointBreakTorque;

        isGrabbing = true;
        isWallSliding = false; // กันชนกับโหมดสไลด์
    }

    void ReleaseGrab()
    {
        if (joint) Destroy(joint);
        if (grabbedCol) grabbedCol.sharedMaterial = grabbedColOriginalMat;

        grabbedRb = null;
        grabbedCol = null;
        grabbedColOriginalMat = null;
        isGrabbing = false; // ← ปล่อยแล้วถึงจะกลับมา Flip ได้
    }

    void OnJointBreak2D(Joint2D j)
    {
        // ถ้า joint หลุด/ขาดเอง
        if (j == joint)
        {
            ReleaseGrab();
        }
    }

    // ===== Utilities / coroutines =====
    private void Flip()
    {
        if (isGrabbing) return; // ★ ล็อกทิศไว้ระหว่างจับ/ผลัก/ดึง

        m_FacingRight = !m_FacingRight;
        Vector3 theScale = transform.localScale;
        theScale.x *= -1;
        transform.localScale = theScale;
    }

    IEnumerator DashCooldown()
    {
        if (animator) animator.SetBool("IsDashing", true);
        isDashing = true;
        canDash = false;
        yield return new WaitForSeconds(0.1f);
        isDashing = false;
        if (animator) animator.SetBool("IsDashing", false);
        yield return new WaitForSeconds(0.5f);
        canDash = true;
    }

    IEnumerator Stun(float time)
    {
        canMove = false;
        yield return new WaitForSeconds(time);
        canMove = true;
    }
    IEnumerator MakeInvincible(float time)
    {
        invincible = true;
        yield return new WaitForSeconds(time);
        invincible = false;
    }
    IEnumerator WaitToMove(float time)
    {
        yield return new WaitForSeconds(time);
        canMove = true;
    }

    IEnumerator WaitToCheck(float time)
    {
        yield return new WaitForSeconds(time);
        canCheck = true;
    }

    public void ApplyDamage(float damage, Vector3 position)
    {
        if (!invincible)
        {
            life -= damage;
            // Vector2 damageDir = new Vector2(position.x - transform.position.x, position.y - transform.position.y).normalized; // ยังไม่ได้ใช้
            if (life <= 0)
            {
                StartCoroutine(WaitToDead());
            }
            else
            {
                StartCoroutine(Stun(0.25f));
                StartCoroutine(MakeInvincible(1f));
            }
        }
    }

    IEnumerator WaitToDead()
    {
        if (animator) animator.SetBool("IsDead", true);
        canMove = false;
        invincible = true;
        var atk = GetComponent<Attack>(); // ★ กัน NRE
        if (atk) atk.enabled = false;
        yield return new WaitForSeconds(0.4f);
        m_Rigidbody2D.linearVelocity = new Vector2(0, m_Rigidbody2D.linearVelocity.y);
        yield return new WaitForSeconds(1.1f);
        LoadDeathScene();
    }

    void LoadDeathScene()
    {
        switch (deathLoadMode)
        {
            case DeathLoadMode.ReloadCurrent:
                {
                    var idx = SceneManager.GetActiveScene().buildIndex;
                    if (idx >= 0) SceneManager.LoadSceneAsync(idx, deathLoadModeType);
                    else Debug.LogError("[CharacterController2D] Active scene has no valid build index.");
                    break;
                }
            case DeathLoadMode.ByName:
                {
                    if (string.IsNullOrEmpty(deathSceneName))
                    {
                        Debug.LogError("[CharacterController2D] deathSceneName is empty.");
                        return;
                    }
                    if (!IsSceneInBuild(deathSceneName))
                    {
                        Debug.LogError($"[CharacterController2D] Scene '{deathSceneName}' ไม่ได้ถูกเพิ่มไว้ใน Build Profiles / Scenes In Build.");
                        return;
                    }
                    SceneManager.LoadSceneAsync(deathSceneName, deathLoadModeType);
                    break;
                }
            case DeathLoadMode.ByBuildIndex:
                {
                    if (deathSceneBuildIndex < 0)
                    {
                        Debug.LogError("[CharacterController2D] deathSceneBuildIndex < 0");
                        return;
                    }
                    SceneManager.LoadSceneAsync(deathSceneBuildIndex, deathLoadModeType);
                    break;
                }
            case DeathLoadMode.None:
            default:
                // ไม่ทำอะไร: เผื่อคุณจะเรียก Respawn เอง
                break;
        }
    }

    bool IsSceneInBuild(string sceneName)
    {
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            var path = SceneUtility.GetScenePathByBuildIndex(i);
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName) return true;
        }
        return false;
    }


    // ===== Gizmos =====
    void OnDrawGizmosSelected()
    {
        // วาดกรอบตรวจจับกล่อง
        if (handPoint)
        {
            Gizmos.color = Color.magenta;
            float facingSign = 1f;
            if (Application.isPlaying) facingSign = m_FacingRight ? 1f : -1f;
            Vector2 center = (Vector2)handPoint.position + new Vector2(facingSign * detectDistance, 0f);
            Gizmos.DrawWireCube(center, detectSize);
        }
        // จุดเช็คพื้น/กำแพง
        if (m_GroundCheck)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(m_GroundCheck.position, k_GroundedRadius);
        }
        if (m_WallCheck)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(m_WallCheck.position, k_GroundedRadius);
        }
    }
}