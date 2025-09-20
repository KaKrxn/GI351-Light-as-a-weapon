using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PuzzleSearable2D : MonoBehaviour
{
    [Header("Sear Settings")]
    [Tooltip("เวลาที่ต้องโดนเลเซอร์แบบต่อเนื่องจนกว่าจะถูกทำลาย")]
    public float requiredSearSeconds = 2f;
    [Tooltip("เวลาผ่อนผันเมื่อเลเซอร์หลุดเป้าเล็กน้อย (กันลื่นไถล)")]
    public float graceWindow = 0.1f;
    public bool destroyOnComplete = true;

    [Header("Visuals (optional)")]
    public Gradient progressTint;          // ไล่สีตามความคืบหน้า (0..1)
    [Range(0f, 1f)] public float tintIntensity = 1f;
    public GameObject searVfxPrefab;       // เอฟเฟกต์ตอนกำลังถูกจี่ (จะสร้างครั้งแรกที่เริ่มจี่)
    public bool attachVfxToObject = true;

    [Header("Events")]
    public UnityEvent onSearStart;
    public UnityEvent<float> onSearProgress01; // เรียกทุกเฟรม (0..1)
    public UnityEvent onSearComplete;

    // runtime
    SpriteRenderer[] srs;
    Color[] baseColors;

    GameObject vfxInstance;
    bool vfxActive;

    float accum;                 // เวลาที่สะสมจากการโดนเลเซอร์
    float sinceLastTick;         // เวลาที่ไม่ได้โดนเลเซอร์ล่าสุด
    bool searing;                // อยู่ในสถานะถูกจี่ (เพิ่งโดนครั้งแรก)
    bool completed;              // เสร็จแล้ว (กันเรียกซ้ำ)

    void Awake()
    {
        srs = GetComponentsInChildren<SpriteRenderer>(true);
        if (srs != null && srs.Length > 0)
        {
            baseColors = new Color[srs.Length];
            for (int i = 0; i < srs.Length; i++)
                baseColors[i] = srs[i].color;
        }
    }

    void Update()
    {
        if (completed) return;

        sinceLastTick += Time.deltaTime;

        // ถ้าหลุดเป้านานเกิน grace → รีเซ็ตความคืบหน้า
        if (sinceLastTick > Mathf.Max(0f, graceWindow))
        {
            if (searing) StopSearingVisuals();
            searing = false;
            accum = 0f;
            UpdateTint(0f);
        }
    }

    /// <summary>
    /// เรียกทุกเฟรมที่เลเซอร์ยังสัมผัสเป้าหมาย
    /// </summary>
    /// <param name="deltaSeconds">เวลาที่ผ่านไปของเฟรมนั้น</param>
    /// <param name="hitPoint">ตำแหน่งโดน</param>
    /// <param name="hitNormal">นอร์มัลผิว</param>
    /// <param name="overrideRequiredSeconds">>0 จะใช้แทน requiredSearSeconds</param>
    /// <param name="overrideGrace">>=0 จะใช้แทน graceWindow</param>
    public void SearTick(float deltaSeconds, Vector2 hitPoint, Vector2 hitNormal,
                         float overrideRequiredSeconds = -1f, float overrideGrace = -1f)
    {
        if (completed) return;

        float need = overrideRequiredSeconds > 0f ? overrideRequiredSeconds : requiredSearSeconds;
        float grace = overrideGrace >= 0f ? overrideGrace : graceWindow;

        // เริ่มจี่รอบแรก
        if (!searing)
        {
            searing = true;
            sinceLastTick = 0f;
            StartSearingVisuals(hitPoint, hitNormal);
            onSearStart?.Invoke();
        }
        else
        {
            sinceLastTick = 0f;
            UpdateSearingVfx(hitPoint, hitNormal);
        }

        accum += Mathf.Max(0f, deltaSeconds);
        float t = Mathf.Clamp01(accum / Mathf.Max(0.0001f, need));
        onSearProgress01?.Invoke(t);
        UpdateTint(t);

        if (accum >= need)
        {
            completed = true;
            StopSearingVisuals();
            onSearComplete?.Invoke();
            if (destroyOnComplete) Destroy(gameObject);
        }
    }

    void StartSearingVisuals(Vector2 hitPoint, Vector2 hitNormal)
    {
        if (searVfxPrefab && vfxInstance == null)
        {
            float ang = Mathf.Atan2(hitNormal.y, hitNormal.x) * Mathf.Rad2Deg;
            vfxInstance = Instantiate(searVfxPrefab, hitPoint, Quaternion.Euler(0, 0, ang));
            if (attachVfxToObject) vfxInstance.transform.SetParent(transform, worldPositionStays: true);
            vfxActive = true;
        }
    }

    void UpdateSearingVfx(Vector2 hitPoint, Vector2 hitNormal)
    {
        if (vfxInstance && !attachVfxToObject)
        {
            float ang = Mathf.Atan2(hitNormal.y, hitNormal.x) * Mathf.Rad2Deg;
            vfxInstance.transform.SetPositionAndRotation(hitPoint, Quaternion.Euler(0, 0, ang));
        }
    }

    void StopSearingVisuals()
    {
        if (vfxInstance) { Destroy(vfxInstance); vfxInstance = null; }
        vfxActive = false;
    }

    void UpdateTint(float t01)
    {
        if (srs == null || srs.Length == 0 || progressTint == null) return;
        Color target = progressTint.Evaluate(t01) * tintIntensity;
        for (int i = 0; i < srs.Length; i++)
            srs[i].color = Color.Lerp(baseColors[i], target, target.a);
    }
}
