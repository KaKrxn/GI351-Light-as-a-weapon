using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.SceneManagement;

public class CharacterController2D : MonoBehaviour
{
    [SerializeField] private float m_JumpForce = 400f;
    [Range(0, .3f)][SerializeField] private float m_MovementSmoothing = .05f;
    [SerializeField] private bool m_AirControl = false;
    [SerializeField] private LayerMask m_WhatIsGround;
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
    public bool requireGroundAfterWallJump = true; 
    private bool wallJumpLock = false;             

    // ===== Push & Pull (Grab) =====
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
    [Space] public UnityEvent OnFallEvent;
    public UnityEvent OnLandEvent;

    [System.Serializable] public class BoolEvent : UnityEvent<bool> { }

    void Awake()
    {
        m_Rigidbody2D = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        if (!handPoint) handPoint = transform; 
    }

    void Update()
    {
        
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
                    wallJumpLock = false; 

                    if (!m_IsWall && !isDashing)
                    {
                        particleJumpDown.Play();
                        animator.SetBool("IsJumping", false);
                        animator.SetBool("IsDoubleJumping", false);
                        animator.SetBool("JumpUp", false);
                    }
                }
            }
        }

        m_IsWall = false;

        if (!m_Grounded)
        {
            OnFallEvent.Invoke();
            Collider2D[] collidersWall = Physics2D.OverlapCircleAll(m_WallCheck.position, k_GroundedRadius, m_WhatIsGround);
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

        
        if (isGrabbing)
        {
            move *= grabMoveMultiplier;
            dash = false;
        }

        
        if (dash && canDash && !isWallSliding)
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
            if (m_Rigidbody2D.linearVelocity.y < -limitFallSpeed)
                m_Rigidbody2D.linearVelocity = new Vector2(m_Rigidbody2D.linearVelocity.x, -limitFallSpeed);

            Vector3 targetVelocity = new Vector2(move * 10f, m_Rigidbody2D.linearVelocity.y);
            m_Rigidbody2D.linearVelocity = Vector3.SmoothDamp(m_Rigidbody2D.linearVelocity, targetVelocity, ref velocity, m_MovementSmoothing);

            
            if ((move > 0 && !m_FacingRight && !isWallSliding && !isGrabbing) ||
                (move < 0 && m_FacingRight && !isWallSliding && !isGrabbing))
            {
                Flip();
            }
        }

        
        if (!isGrabbing && m_Grounded && jump)
        {
            animator.SetBool("IsJumping", true);
            animator.SetBool("JumpUp", true);
            m_Grounded = false;
            m_Rigidbody2D.AddForce(new Vector2(0f, m_JumpForce));
            canDoubleJump = true;
            particleJumpDown.Play();
            particleJumpUp.Play();
        }
        else if (!isGrabbing && !m_Grounded && jump && canDoubleJump && !isWallSliding)
        {
            canDoubleJump = false;
            m_Rigidbody2D.linearVelocity = new Vector2(m_Rigidbody2D.linearVelocity.x, 0);
            m_Rigidbody2D.AddForce(new Vector2(0f, m_JumpForce / 1.2f));
            animator.SetBool("IsDoubleJumping", true);
        }
        // Wall logic
        else if (m_IsWall && !m_Grounded)
        {
            // เริ่มเข้าสไลด์กำแพง (กันเข้าเงื่อนไขซ้ำ)
            if ((!oldWallSlidding && m_Rigidbody2D.linearVelocity.y < 0f) || isDashing)
            {
                isWallSliding = true;
                oldWallSlidding = true;
                canCheck = false;

                m_WallCheck.localPosition = new Vector3(-m_WallCheck.localPosition.x, m_WallCheck.localPosition.y, 0);
                if (!isGrabbing) Flip(); 
                StartCoroutine(WaitToCheck(0.1f));
                canDoubleJump = true;
                animator.SetBool("IsWallSliding", true);
            }
            isDashing = false;

            if (isWallSliding)
            {
                if (m_Rigidbody2D.linearVelocity.y < -3)
                {
                    m_Rigidbody2D.linearVelocity = new Vector2(-transform.localScale.x * 2, -5);
                }
            }

            // Wall Jump —> บล็อกเมื่อจับอยู่ หรือยังไม่ลงพื้นจากครั้งก่อน / หรือครบโควตา
            if (jump && isWallSliding && !isGrabbing)
            {
                if ((requireGroundAfterWallJump && wallJumpLock) ||
                    (limitWallJumps && wallJumpsUsed >= maxWallJumps))
                {
                    // do nothing
                }
                else
                {
                    animator.SetBool("IsJumping", true);
                    animator.SetBool("JumpUp", true);
                    m_Rigidbody2D.linearVelocity = new Vector2(0f, 0f);
                    m_Rigidbody2D.AddForce(new Vector2(transform.localScale.x * m_JumpForce * 1.2f, m_JumpForce));

                    jumpWallStartX = transform.position.x;
                    limitVelOnWallJump = true;
                    canDoubleJump = true;

                    isWallSliding = false;
                    animator.SetBool("IsWallSliding", false);
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
                isWallSliding = false;
                animator.SetBool("IsWallSliding", false);
                oldWallSlidding = false;
                m_WallCheck.localPosition = new Vector3(Mathf.Abs(m_WallCheck.localPosition.x), m_WallCheck.localPosition.y, 0);
                canDoubleJump = true;
                StartCoroutine(DashCooldown());
            }
        }
        else if (isWallSliding && !m_IsWall && canCheck)
        {
            isWallSliding = false;
            animator.SetBool("IsWallSliding", false);
            oldWallSlidding = false;
            m_WallCheck.localPosition = new Vector3(Mathf.Abs(m_WallCheck.localPosition.x), m_WallCheck.localPosition.y, 0);
            canDoubleJump = true;
        }
    }

    // ===== Push & Pull - core =====
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

    void OnJointBreak2D(Joint2D j)
    {
        
        if (j == joint)
        {
            ReleaseGrab();
        }
    }

    // ===== Utilities / coroutines =====
    private void Flip()
    {
        if (isGrabbing) return; 

        m_FacingRight = !m_FacingRight;
        Vector3 theScale = transform.localScale;
        theScale.x *= -1;
        transform.localScale = theScale;
    }

    IEnumerator DashCooldown()
    {
        animator.SetBool("IsDashing", true);
        isDashing = true;
        canDash = false;
        yield return new WaitForSeconds(0.1f);
        isDashing = false;
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
            Vector2 damageDir = new Vector2(position.x - transform.position.x, position.y - transform.position.y).normalized;
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
        animator.SetBool("IsDead", true);
        canMove = false;
        invincible = true;
        GetComponent<Attack>().enabled = false;
        yield return new WaitForSeconds(0.4f);
        m_Rigidbody2D.linearVelocity = new Vector2(0, m_Rigidbody2D.linearVelocity.y);
        yield return new WaitForSeconds(1.1f);
        SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
    }

    // ===== Gizmos =====
    void OnDrawGizmosSelected()
    {
        
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
