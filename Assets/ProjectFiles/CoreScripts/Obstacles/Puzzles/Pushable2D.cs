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

    // ----------------- Prompt UI (optional) -----------------
    [Header("Prompt UI (optional)")]
    [Tooltip("UI ที่จะแสดงเมื่อผู้เล่นเข้าระยะผลัก/จับ")]
    public GameObject promptUI;
    [Tooltip("แท็กที่อนุญาตให้แสดง Prompt (ส่วนมากคือ Player)")]
    public string[] promptTags = new[] { "Player" };
    [Tooltip("ซ่อน Prompt ระหว่างถูกจับอยู่")]
    public bool hidePromptWhenGrabbed = true;
    // --------------------------------------------------------

    // ----------------- NEW: Drag SFX -----------------
    [Header("SFX • Drag while grabbed (optional)")]
    [Tooltip("AudioSource ที่ใช้เล่นเสียงขณะลาก (แนะนำแปะไว้บนกล่องนี้เอง)")]
    public AudioSource dragSource;
    [Tooltip("คลิปเสียง loop ตอนลากกล่อง")]
    public AudioClip dragLoop;
    [Range(0f, 1f)] public float dragVolume = 0.9f;
    [Tooltip("ความเร็วขั้นต่ำที่ถือว่า 'กำลังลาก' (หน่วย: world units/sec)")]
    public float speedThreshold = 0.15f;
    [Tooltip("ให้ pitch ขึ้นลงตามความเร็วลาก")]
    public bool pitchWithSpeed = true;
    [Range(0.5f, 2f)] public float minPitch = 0.9f;
    [Range(0.5f, 2f)] public float maxPitch = 1.2f;
    [Tooltip("ระยะเวลาที่ใช้เฟดเสียงเข้า/ออก")]
    public float fadeTime = 0.08f;
    // -------------------------------------------------

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

    // สำหรับ Prompt
    int promptTouchCount = 0; // รองรับกรณีชน/ทริกเกอร์หลายชิ้น

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        GetComponents(cols); // เก็บคอลลิเดอร์ทุกตัวบน GameObject นี้
        BackupOriginals();
        ApplyIdleMaterial();

        SetPrompt(false);
    }

    void Update()
    {
        HandleDragSfx(); // <--- อัปเดตเสียงลากทุกเฟรม
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

        // ซ่อน Prompt ขณะถูกจับ (ตามตัวเลือก)
        if (hidePromptWhenGrabbed) SetPrompt(false);

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

        // ถ้ายังมีผู้เล่นยืนชิดอยู่ ให้โชว์ Prompt กลับ
        RefreshPrompt();

        // หยุดเสียงลากทันทีเมื่อปล่อย
        StopDragSfx(true);

        onReleased?.Invoke();
    }

    // ===== Prompt Detection (รองรับ Trigger และ Collision) =====
    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPromptTag(other.tag))
        {
            promptTouchCount++;
            RefreshPrompt();
        }
    }
    void OnTriggerExit2D(Collider2D other)
    {
        if (IsPromptTag(other.tag))
        {
            promptTouchCount = Mathf.Max(0, promptTouchCount - 1);
            RefreshPrompt();
        }
    }
    void OnCollisionEnter2D(Collision2D col)
    {
        if (IsPromptTag(col.collider.tag))
        {
            promptTouchCount++;
            RefreshPrompt();
        }
    }
    void OnCollisionExit2D(Collision2D col)
    {
        if (IsPromptTag(col.collider.tag))
        {
            promptTouchCount = Mathf.Max(0, promptTouchCount - 1);
            RefreshPrompt();
        }
    }

    bool IsPromptTag(string tagStr)
    {
        if (promptTags == null || promptTags.Length == 0) return true;
        foreach (var t in promptTags)
            if (!string.IsNullOrEmpty(t) && tagStr == t) return true;
        return false;
    }

    void RefreshPrompt()
    {
        if (!promptUI) return;
        bool show = (promptTouchCount > 0) && (!hidePromptWhenGrabbed || !IsGrabbed);
        SetPrompt(show);
    }

    void SetPrompt(bool on)
    {
        if (promptUI && promptUI.activeSelf != on)
            promptUI.SetActive(on);
    }

    // ----------------- Drag SFX logic -----------------
    void HandleDragSfx()
    {
        if (!dragSource || !dragLoop || !rb) return;

        // เล่นเฉพาะตอนกำลังถูกจับ + มีการเคลื่อนที่เกิน threshold
        float speed = rb.linearVelocity.magnitude;
        bool movingWhileGrabbed = IsGrabbed && (speed >= speedThreshold);

        // เริ่มเล่นถ้ายังไม่เล่นและกำลังลาก
        if (movingWhileGrabbed && !dragSource.isPlaying)
        {
            dragSource.clip = dragLoop;
            dragSource.loop = true;
            dragSource.volume = 0f;      // เฟดเข้า
            dragSource.pitch = 1f;
            dragSource.Play();
        }

        // ตั้งเป้าหมายเสียง (ลาก = ดัง, ไม่ลาก/ปล่อย = เบา)
        float targetVol = (movingWhileGrabbed ? dragVolume : 0f);
        float lerp = (fadeTime > 0f) ? Time.deltaTime / fadeTime : 1f;
        if (dragSource.isPlaying)
        {
            dragSource.volume = Mathf.Lerp(dragSource.volume, targetVol, lerp);

            // ปรับ pitch ตามความเร็ว (ออปชัน)
            if (pitchWithSpeed)
            {
                // นอร์มัลไลซ์ความเร็วเทียบกับ threshold*3 เพื่อให้สเกลลื่น ๆ
                float s01 = Mathf.Clamp01(speed / (speedThreshold * 3f));
                dragSource.pitch = Mathf.Lerp(minPitch, maxPitch, s01);
            }

            // ถ้าไม่ได้ลากหรือปล่อยไปแล้ว และเสียงเฟดจนเบามาก → หยุดจริง
            if (!movingWhileGrabbed && dragSource.volume <= 0.01f)
            {
                StopDragSfx(false);
            }
        }
    }

    void StopDragSfx(bool instant)
    {
        if (dragSource && dragSource.isPlaying)
        {
            if (instant || fadeTime <= 0f)
            {
                dragSource.Stop();
                dragSource.volume = 0f;
                dragSource.pitch = 1f;
            }
            else
            {
                // ปล่อยให้ HandleDragSfx เฟดลงจนหยุดเอง
            }
        }
    }
    // ---------------------------------------------------

    // กันเผลอลบคอมโพเนนต์ตอนยังถูกจับอยู่ → คืนค่าก่อน
    void OnDisable()
    {
        if (IsGrabbed) OnRelease();
        SetPrompt(false);
        StopDragSfx(true);
    }
}
