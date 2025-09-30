using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class GhostSpirit : MonoBehaviour, ILightDamageable
{
    public enum GhostState { Chasing, Fleeing, Dead }

    [Header("Target")]
    public Transform player;               // อิงผู้เล่น
    public float detectRadius = 50f;       // ระยะเจอผู้เล่น (กัน null/ไกลเกิน)

    [Header("Move")]
    public float chaseSpeed = 4.0f;        // ความเร็วไล่
    public float fleeSpeed = 6.0f;        // ความเร็วหนี
    public float turnLerp = 12f;         // คม/นุ่มเวลาเลี้ยว
    public float stopDistance = 0.5f;      // ไม่จี้ประชิดเกินไป

    [Header("Dynamic Swing")]
    public float swingAmplitude = 0.75f;   // ระยะส่าย
    public float swingFrequency = 2.0f;    // ความถี่ส่าย (Hz)
    public float swingSpeedScale = 0.35f;  // สเกลแอมป์ตามความเร็ว

    [Header("Health / Behavior")]
    public int maxHealth = 2;              // เลือด = 2 หน่วย
    public float fleeDuration = 10f;       // หนี 10 วิ หลังเสียเลือดครั้งแรก
    public bool destroyOnDeath = true;
    public GameObject deathVfx;

    [Header("FX (optional)")]
    public SpriteRenderer sprite;
    public Color hitFlashColor = new Color(1f, 0.7f, 0.7f, 1f);
    public float hitFlashTime = 0.1f;

    private GhostState state = GhostState.Chasing;
    private int health;
    private float fleeTimer;

    private Rigidbody2D rb;
    private float swingPhase;

    private Color _origColor;
    private bool _flashing;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.isKinematic = true;                 // ให้ขยับเอง (ใช้ MovePosition)
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite) _origColor = sprite.color;

        health = Mathf.Max(1, maxHealth);
    }

    void Update()
    {
        if (!player) return;

        switch (state)
        {
            case GhostState.Chasing:
                TickChase(Time.deltaTime);
                break;

            case GhostState.Fleeing:
                TickFlee(Time.deltaTime);
                break;

            case GhostState.Dead:
                // no-op
                break;
        }
    }

    void TickChase(float dt)
    {
        Vector2 toPlayer = (player.position - transform.position);
        float dist = toPlayer.magnitude;
        if (dist > detectRadius || dist <= 1e-3f) return;

        // ทิศหลักไปหาเป้าหมาย
        Vector2 dir = toPlayer / dist;

        // เวกเตอร์ตั้งฉากเพื่อเอาไว้ส่าย
        Vector2 perp = new Vector2(-dir.y, dir.x);

        // อัปเดตเฟสส่าย
        swingPhase += (Mathf.PI * 2f) * swingFrequency * dt;

        float amp = swingAmplitude * (1f + swingSpeedScale * chaseSpeed * 0.1f);
        Vector2 sway = perp * (Mathf.Sin(swingPhase) * amp);

        // เวกเตอร์รวมสำหรับ Move
        Vector2 desiredVel = (dir * chaseSpeed) + sway;

        // หมุนหน้าตามความเร็ว (นุ่ม)
        if (desiredVel.sqrMagnitude > 1e-4f)
        {
            float ang = Mathf.Atan2(desiredVel.y, desiredVel.x) * Mathf.Rad2Deg;
            Quaternion targetRot = Quaternion.Euler(0, 0, ang);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, dt * turnLerp);
        }

        // ไม่ให้จี้เกินไป
        if (dist > stopDistance)
        {
            Vector2 nextPos = rb.position + desiredVel * dt;
            rb.MovePosition(nextPos);
        }
    }

    void TickFlee(float dt)
    {
        if (fleeTimer <= 0f)
        {
            state = GhostState.Chasing;
            return;
        }

        fleeTimer -= dt;

        Vector2 away = (transform.position - player.position).normalized;
        Vector2 perp = new Vector2(-away.y, away.x);

        swingPhase += (Mathf.PI * 2f) * swingFrequency * dt;
        float amp = swingAmplitude * (1f + swingSpeedScale * fleeSpeed * 0.1f);
        Vector2 sway = perp * (Mathf.Sin(swingPhase) * amp);

        Vector2 desiredVel = (away * fleeSpeed) + sway;

        if (desiredVel.sqrMagnitude > 1e-4f)
        {
            float ang = Mathf.Atan2(desiredVel.y, desiredVel.x) * Mathf.Rad2Deg;
            Quaternion targetRot = Quaternion.Euler(0, 0, ang);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, dt * turnLerp);
        }

        Vector2 nextPos = rb.position + desiredVel * dt;
        rb.MovePosition(nextPos);
    }

    public void ApplyLightDamage(float amount, Vector2 hitPoint)
    {
        if (state == GhostState.Dead) return;

        // สะสมดาเมจน้อย ๆ ต่อเฟรมก็ถือว่าโดน
        if (amount <= 0f) return;

        // ทำ Flash สั้น ๆ
        if (sprite && !_flashing) StartCoroutine(Flash());

        // เมื่อโดนพอ (ปืนส่งมาทีละเล็กด้วย Time.deltaTime) เราจะคิดเป็น "โดน 1 ครั้ง"
        // เพื่อให้สะดวก: แค่โดนลำแสง "ต่อเนื่องชั่วขณะ" เราจะนับเป็น 1 HP
        // → คุณจะปรับ logic นี้เป็นสะสมครบเกณฑ์แล้วค่อย -1 ก็ได้
        TakeOneHit();
    }

    void TakeOneHit()
    {
        if (health <= 0) return;

        health -= 1;

        if (health <= 0)
        {
            state = GhostState.Dead;
            if (deathVfx) Instantiate(deathVfx, transform.position, Quaternion.identity);
            if (destroyOnDeath) Destroy(gameObject);
            else gameObject.SetActive(false);
            return;
        }

        // เสียเลือดครั้งแรก → หนี 10 วิ
        state = GhostState.Fleeing;
        fleeTimer = Mathf.Max(2f, fleeDuration);
    }

    System.Collections.IEnumerator Flash()
    {
        _flashing = true;
        if (sprite)
        {
            sprite.color = hitFlashColor;
            yield return new WaitForSeconds(hitFlashTime);
            sprite.color = _origColor;
        }
        _flashing = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
}

