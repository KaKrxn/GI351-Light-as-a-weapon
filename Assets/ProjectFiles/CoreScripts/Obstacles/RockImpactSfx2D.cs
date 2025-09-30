using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class RockImpactSfx2D : MonoBehaviour
{
    [Header("Detect Ground")]
    [Tooltip("เลเยอร์ที่ถือว่าเป็นพื้น")]
    public LayerMask groundLayer = 1 << 6;  // ปรับเป็นเลเยอร์ Ground ของโปรเจกต์คุณ
    [Tooltip("ต้องเป็นการชนที่มี normal ชี้ขึ้นอย่างน้อยเท่านี้ ถึงจะนับว่า 'ตกใส่พื้น'")]
    [Range(0f, 1f)] public float minUpNormal = 0.5f;

    [Header("Trigger Condition")]
    [Tooltip("ความเร็วแนวดิ่งตอนชนขั้นต่ำ (หน่วย: ยูนิต/วินาที)")]
    public float minVerticalImpactSpeed = 1.0f;
    [Tooltip("เล่นครั้งเดียวต่อชิ้นหิน")]
    public bool playOnlyOnce = true;

    [Header("SFX")]
    [Tooltip("คลิปเสียงตอนหินตกกระแทกพื้น")]
    public AudioClip impactClip;
    [Tooltip("ถ้าใส่ จะใช้แหล่งเสียงนี้ (แนะนำแปะบนหิน)")]
    public AudioSource impactSource;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("ถ้าไม่ใส่ AudioSource หรืออยากให้เสียงไม่ถูกตัด ใช้ PlayClipAtPoint")]
    public bool usePlayClipAtPointFallback = true;

    [Header("Debug (optional)")]
    public bool debugLog = false;

    Rigidbody2D rb;
    bool played;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!enabled || impactClip == null) return;
        if (playOnlyOnce && played) return;
        if (!IsInLayerMask(col.collider.gameObject.layer, groundLayer)) return;

        // ต้องเป็นการชนแบบ "ลงบนพื้น": normal ชี้ขึ้น
        bool validNormal = false;
        foreach (var cp in col.contacts)
        {
            if (cp.normal.y >= minUpNormal) { validNormal = true; break; }
        }
        if (!validNormal) return;

        // ใช้ความเร็ว "ลง" ตอนชนเป็นเกณฑ์
        float verticalDownSpeed = Mathf.Max(0f, -col.relativeVelocity.y); // เฉพาะคอมโพเนนต์ลง
        if (verticalDownSpeed < minVerticalImpactSpeed) return;

        if (debugLog)
            Debug.Log($"[RockImpactSfx2D] hit ground: vDown={verticalDownSpeed:0.00}");

        PlayImpactSfx();
        if (playOnlyOnce) played = true;
    }

    void PlayImpactSfx()
    {
        if (impactSource != null)
        {
            impactSource.PlayOneShot(impactClip, volume);
        }
        else if (usePlayClipAtPointFallback)
        {
            AudioSource.PlayClipAtPoint(impactClip, transform.position, volume);
        }
    }

    static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
