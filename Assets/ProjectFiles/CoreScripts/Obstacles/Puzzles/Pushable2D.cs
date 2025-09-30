using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class Pushable2D : MonoBehaviour
{
    [Header("Materials (optional)")]
    [Tooltip("PhysicsMaterial2D ที่ใช้ตอนวางนิ่ง (เสียดทานสูง / ไม่ลื่น)")]
    public PhysicsMaterial2D idleMaterial;
    [Tooltip("PhysicsMaterial2D ที่ใช้ตอนถูกจับ (เสียดทานต่ำ / ลื่นผลักง่าย)")]
    public PhysicsMaterial2D grabbedMaterial;

    [Header("Physics toggle on Grab")]
    [Tooltip("ให้สลับเป็น Dynamic ตอนถูกจับ แล้วคืนค่าเดิมตอนปล่อย")]
    public bool enablePhysicsOnGrab = true;
    [Tooltip("ล็อกการหมุนตอนถูกจับ (กันกล่องหมุน)")]
    public bool freezeRotationWhenGrabbed = true;
    [Tooltip("แรงโน้มถ่วงระหว่างถูกจับ (ถ้าเป็น null จะไม่แตะ)")]
    public float? grabbedGravityScale = null;
    [Tooltip("Linear drag ระหว่างถูกจับ (ถ้าเป็น null จะไม่แตะ)")]
    public float? grabbedDrag = 2f;
    [Tooltip("Angular drag ระหว่างถูกจับ (ถ้าเป็น null จะไม่แตะ)")]
    public float? grabbedAngularDrag = 2f;

    [Header("Events")]
    public UnityEvent onGrabbed;
    public UnityEvent onReleased;

    // --- runtime ---
    Rigidbody2D rb;
    List<Collider2D> cols = new List<Collider2D>();

    // backups
    RigidbodyType2D origBodyType;
    RigidbodyConstraints2D origConstraints;
    float origGravity;
    float origDrag;
    float origAngularDrag;
    readonly List<PhysicsMaterial2D> origMaterials = new List<PhysicsMaterial2D>();

    public bool IsGrabbed { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        GetComponents(cols); // เก็บคอลลิเดอร์ทุกตัวบน GameObject นี้
        BackupOriginals();
        ApplyIdleMaterial();
    }

    void BackupOriginals()
    {
        if (!rb) return;
        origBodyType = rb.bodyType;
        origConstraints = rb.constraints;
        origGravity = rb.gravityScale;
        origDrag = rb.linearDamping;
        origAngularDrag = rb.angularDamping;

        origMaterials.Clear();
        for (int i = 0; i < cols.Count; i++)
        {
            // ใช้ sharedMaterial เพื่อไม่สร้างอินสแตนซ์เพิ่ม
            origMaterials.Add(cols[i] ? cols[i].sharedMaterial : null);
        }
    }

    void ApplyIdleMaterial()
    {
        if (!idleMaterial) return;
        for (int i = 0; i < cols.Count; i++)
            if (cols[i]) cols[i].sharedMaterial = idleMaterial;
    }

    void ApplyGrabbedMaterial()
    {
        if (!grabbedMaterial) return;
        for (int i = 0; i < cols.Count; i++)
            if (cols[i]) cols[i].sharedMaterial = grabbedMaterial;
    }

    // ====== Public API ======
    /// <summary>เรียกตอนผู้เล่นเริ่มจับ</summary>
    public void OnGrab(Transform grabber = null)
    {
        if (IsGrabbed) return;
        IsGrabbed = true;

        // สลับฟิสิกส์
        if (enablePhysicsOnGrab && rb)
        {
            // backup เผื่อมีการเปลี่ยนค่าไว้ก่อนหน้า
            BackupOriginals();

            rb.bodyType = RigidbodyType2D.Dynamic;

            if (freezeRotationWhenGrabbed)
                rb.freezeRotation = true; // เทียบเท่าเพิ่ม FreezeRotation ใน constraints

            if (grabbedGravityScale.HasValue)
                rb.gravityScale = grabbedGravityScale.Value;

            if (grabbedDrag.HasValue)
                rb.linearDamping = grabbedDrag.Value;

            if (grabbedAngularDrag.HasValue)
                rb.angularDamping = grabbedAngularDrag.Value;

            rb.WakeUp();
        }

        // วัสดุแบบลื่น
        ApplyGrabbedMaterial();

        onGrabbed?.Invoke();
    }

    /// <summary>เรียกตอนผู้เล่นปล่อย</summary>
    public void OnRelease()
    {
        if (!IsGrabbed) return;
        IsGrabbed = false;

        // คืนค่าฟิสิกส์เดิม
        if (enablePhysicsOnGrab && rb)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            rb.bodyType = origBodyType;
            rb.constraints = origConstraints;
            rb.gravityScale = origGravity;
            rb.linearDamping = origDrag;
            rb.angularDamping = origAngularDrag;

            rb.WakeUp();
        }

        // คืนวัสดุเดิมถ้าเคยสำรองไว้, ไม่งั้นใช้ idleMaterial
        if (origMaterials.Count == cols.Count && origMaterials.Count > 0)
        {
            for (int i = 0; i < cols.Count; i++)
            {
                if (cols[i]) cols[i].sharedMaterial = origMaterials[i];
            }
        }
        else
        {
            ApplyIdleMaterial();
        }

        onReleased?.Invoke();
    }

    // กันเผลอลบคอมโพเนนต์ตอนยังถูกจับอยู่ → คืนค่าก่อน
    void OnDisable()
    {
        if (IsGrabbed) OnRelease();
    }
}
