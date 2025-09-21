using UnityEngine;

public class GroundSensor2D : MonoBehaviour
{
    [Header("นับว่าเป็นพื้นเฉพาะเลเยอร์เหล่านี้")]
    public LayerMask groundMask;   // ตั้งเป็น "Ground" เท่านั้น

    [Header("กันดีดเฟรมเดียว")]
    public float coyoteGrace = 0.02f; // เวลาผ่อนผันไม่ให้เด้ง false ชั่วคราว

    int contacts;
    float graceTimer;

    public bool IsGrounded
    {
        get
        {
            if (contacts > 0) return true;
            return graceTimer > 0f;
        }
    }

    void OnEnable() { contacts = 0; graceTimer = 0f; }
    void Update() { if (graceTimer > 0f) graceTimer -= Time.deltaTime; }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsInMask(other.gameObject.layer)) contacts++;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (IsInMask(other.gameObject.layer))
        {
            contacts = Mathf.Max(0, contacts - 1);
            graceTimer = coyoteGrace;
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // เผื่อกรณีเฟรมตก/กายภาพสั่น
        if (IsInMask(other.gameObject.layer) && contacts <= 0)
            contacts = 1;
    }

    bool IsInMask(int layer)
    {
        return (groundMask.value & (1 << layer)) != 0;
    }
}
