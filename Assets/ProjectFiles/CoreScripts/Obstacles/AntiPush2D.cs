using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class AntiPush2D : MonoBehaviour
{
    [Header("Behavior")]
    [Tooltip("เปิดไว้ = วัตถุสามารถถูกผู้เล่นดัน/ชนให้ขยับได้\nปิดไว้ = กันการถูกดันตามแกนที่เลือก")]
    public bool canBePushed = false;

    [Tooltip("ล็อกเฉพาะตอนกำลังชนกับ Player เท่านั้น (ถ้าปิด = ล็อกตลอดเวลา)")]
    public bool onlyWhenTouchingPlayer = true;

    [Tooltip("แท็กของตัวละครผู้เล่นที่ใช้ตรวจชน")]
    public string[] playerTags = new[] { "Player" };

    [Header("Freeze Axes While Protected")]
    [Tooltip("แนะนำเปิด X เพื่อกันการดันด้านข้าง แต่ยังตกด้วยแรงโน้มถ่วงได้")]
    public bool freezeX = true;

    [Tooltip("กรณีต้องการกันการเด้งขึ้น/ลงจากการชน (ปกติควรปิดเพื่อให้ตกด้วยแรงโน้มถ่วง)")]
    public bool freezeY = false;

    [Tooltip("ส่วนใหญ่ควรเปิด เพื่อไม่ให้วัตถุหมุนจากแรงชน")]
    public bool freezeRotation = true;

    private Rigidbody2D rb;
    private RigidbodyConstraints2D originalConstraints;
    private int playerContactCount = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalConstraints = rb.constraints; // เก็บ constraint เดิม (เช่น FreezeRotation ที่ตั้งไว้)
    }

    void OnEnable()
    {
        playerContactCount = 0;
        ApplyFrozen(false); // เริ่มต้นด้วย constraint เดิม
    }

    void FixedUpdate()
    {
        if (!canBePushed && !onlyWhenTouchingPlayer)
        {
            // ล็อกตลอดเวลา (ใช้เป็น "หินที่หนักดันไม่ได้" แต่ยังตกได้ถ้าไม่ freezeY)
            ApplyFrozen(true);
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!canBePushed && onlyWhenTouchingPlayer && IsPlayer(col.collider.tag))
        {
            playerContactCount++;
            ApplyFrozen(true);
        }
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (!canBePushed && onlyWhenTouchingPlayer && IsPlayer(col.collider.tag))
        {
            playerContactCount = Mathf.Max(0, playerContactCount - 1);
            if (playerContactCount == 0)
                ApplyFrozen(false);
        }
    }

    bool IsPlayer(string tagStr)
    {
        if (playerTags == null || playerTags.Length == 0) return tagStr == "Player";
        foreach (var t in playerTags)
            if (!string.IsNullOrEmpty(t) && tagStr == t) return true;
        return false;
    }

    void ApplyFrozen(bool frozen)
    {
        if (frozen)
        {
            var c = originalConstraints;
            if (freezeX) c |= RigidbodyConstraints2D.FreezePositionX;
            if (freezeY) c |= RigidbodyConstraints2D.FreezePositionY;
            if (freezeRotation) c |= RigidbodyConstraints2D.FreezeRotation;
            rb.constraints = c;
        }
        else
        {
            rb.constraints = originalConstraints;
        }
    }

    // เผื่อแก้ค่าบน Inspector ตอนรัน
    void OnValidate()
    {
        if (!Application.isPlaying || rb == null) return;

        if (canBePushed)
        {
            // เปิดให้ดันได้ → คืน constraint เดิม
            ApplyFrozen(false);
        }
        else if (!onlyWhenTouchingPlayer)
        {
            // กันดันตลอดเวลา
            ApplyFrozen(true);
        }
    }
}
