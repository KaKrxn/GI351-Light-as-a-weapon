using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PressurePlateDoor2D : MonoBehaviour
{
    public enum ActivationMode { HoldToOpen, OneShotOpen }
    public enum DoorAxis { X, Y }
    public enum Direction { Positive, Negative }

    [Header("What can press this plate?")]
    public string[] allowedTags = new[] { "Player" };

    [Header("Plate Settings")]
    public ActivationMode mode = ActivationMode.HoldToOpen;

    [Header("Global Easing (fallback)")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Plate Visual (optional)")]
    public Transform plateGraphic;
    public float platePressOffsetY = -0.06f;
    public float platePressLerp = 15f;

    // ================= SFX =================
    [Header("SFX • Plate (optional)")]
    [Tooltip("AudioSource ที่จะใช้เล่นเสียงของแผ่นปุ่ม (กด/ปล่อย)")]
    public AudioSource plateSfxSource;
    public AudioClip pressDownSfx;
    public AudioClip releaseSfx;
    [Range(0f, 1f)] public float plateVolume = 1f;
    // ======================================

    [System.Serializable]
    public class DoorLink
    {
        [Header("Door / Platform")]
        public Transform door;

        [Tooltip("แกนที่ใช้ขยับสำหรับประตูนี้")]
        public DoorAxis axis = DoorAxis.Y;

        [Tooltip("ทิศของการเปิด: + (ขึ้น/ขวา) หรือ − (ลง/ซ้าย)")]
        public Direction direction = Direction.Positive;

        [Tooltip("ระยะที่จะขยับตามแกนที่เลือก")]
        public float moveDistance = 3f;

        [Tooltip("เวลาเปิด")]
        public float openDuration = 0.4f;

        [Tooltip("ตัวคูณเวลา 'ปิด' (openDuration * ค่านี้)")]
        public float closeDurationMultiplier = 1f;

        [Tooltip("ถ้าเว้นว่างจะใช้ Global Easing ข้างบน")]
        public AnimationCurve customEase;

        [Header("SFX • Door Move (optional)")]
        [Tooltip("AudioSource สำหรับเสียงประตูนี้ (แนะนำแปะไว้ที่วัตถุประตู)")]
        public AudioSource moveSource;

        [Tooltip("คลิปเสียง loop ตอนประตูกำลังเลื่อน")]
        public AudioClip moveLoop;

        [Range(0f, 1f)] public float moveVolume = 0.9f;

        public bool pitchWithSpeed = true;
        [Range(0.5f, 2f)] public float minPitch = 0.95f;
        [Range(0.5f, 2f)] public float maxPitch = 1.15f;

        // ---- runtime ----
        [HideInInspector] public Vector3 startPos;
        [HideInInspector] public Coroutine routine;
        [HideInInspector] public bool isOpen; // สำหรับราย-door (ใช้กำกับสถานะเวลาบังคับด้วย API)
    }

    [Header("Doors (multi-target)")]
    public List<DoorLink> doors = new List<DoorLink>();

    // --- runtime (plate) ---
    Vector3 plateGraphicStart;
    float plateVisualT = 0f;

    int pressCount = 0;
    bool anyOpen = false;          // สถานะรวม (อย่างน้อย 1 ประตูเปิด) สำหรับ HoldToOpen/OneShotOpen
    bool oneShotConsumed = false;  // ใช้ไปแล้วในโหมด OneShotOpen

    void Awake()
    {
        if (plateGraphic) plateGraphicStart = plateGraphic.localPosition;

        // จดตำแหน่งเริ่มของแต่ละประตู
        for (int i = 0; i < doors.Count; i++)
        {
            var d = doors[i];
            if (!d.door) continue;
            d.startPos = d.door.position;
            d.isOpen = false;
        }
    }

    void Update()
    {
        // plate visual squash
        if (plateGraphic)
        {
            float target = pressCount > 0 ? 1f : 0f;
            plateVisualT = Mathf.MoveTowards(plateVisualT, target, platePressLerp * Time.deltaTime);
            Vector3 lp = plateGraphicStart;
            lp.y += platePressOffsetY * plateVisualT;
            plateGraphic.localPosition = lp;
        }
    }

    // ===== Trigger =====
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsAllowed(other)) return;

        bool wasIdle = (pressCount <= 0);
        pressCount++;

        // SFX: กดลงครั้งแรก
        if (wasIdle && plateSfxSource && pressDownSfx)
            plateSfxSource.PlayOneShot(pressDownSfx, plateVolume);

        if (mode == ActivationMode.HoldToOpen)
        {
            OpenAll();
        }
        else // OneShotOpen
        {
            if (!oneShotConsumed)
            {
                oneShotConsumed = true;
                OpenAll();
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsAllowed(other)) return;

        pressCount = Mathf.Max(0, pressCount - 1);

        if (mode == ActivationMode.HoldToOpen && pressCount == 0)
        {
            // SFX: ปล่อยปุ่ม
            if (plateSfxSource && releaseSfx)
                plateSfxSource.PlayOneShot(releaseSfx, plateVolume);

            CloseAll();
        }
    }

    // ===== Core control =====
    void OpenAll()
    {
        if (anyOpen) return;
        anyOpen = true;

        foreach (var d in doors)
        {
            if (!d.door) continue;

            Vector3 axis = (d.axis == DoorAxis.Y) ? Vector3.up : Vector3.right;
            float sign = (d.direction == Direction.Positive) ? 1f : -1f;
            Vector3 target = d.startPos + axis * sign * d.moveDistance;

            StartMove(d, target, d.openDuration, true);
        }
    }

    void CloseAll()
    {
        if (!anyOpen) return;
        anyOpen = false;

        foreach (var d in doors)
        {
            if (!d.door) continue;

            float dur = Mathf.Max(0.0001f, d.openDuration * Mathf.Max(0.01f, d.closeDurationMultiplier));
            StartMove(d, d.startPos, dur, false);
        }
    }

    void StartMove(DoorLink link, Vector3 targetPos, float duration, bool toOpen)
    {
        if (!link.door) return;
        if (link.routine != null) StopCoroutine(link.routine);
        link.routine = StartCoroutine(MoveDoorCo(link, targetPos, duration, toOpen));
    }

    IEnumerator MoveDoorCo(DoorLink d, Vector3 targetPos, float duration, bool toOpen)
    {
        Vector3 start = d.door.position;

        if (duration <= 0f)
        {
            d.door.position = targetPos;
            d.isOpen = toOpen;
            StopDoorSfx(d);
            d.routine = null;
            yield break;
        }

        // === START door-move SFX (per-door) ===
        float totalDist = Vector3.Distance(start, targetPos);
        float lastDist = 0f;
        if (d.moveSource && d.moveLoop)
        {
            d.moveSource.clip = d.moveLoop;
            d.moveSource.loop = true;
            d.moveSource.volume = d.moveVolume;
            d.moveSource.pitch = 1f;
            d.moveSource.Play();
            lastDist = 0f;
        }
        // ======================================

        float t = 0f;
        var curve = (d.customEase != null && d.customEase.length > 0) ? d.customEase : ease;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float k = curve != null ? curve.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);
            d.door.position = Vector3.LerpUnclamped(start, targetPos, k);

            // pitch ตามความเร็วของประตูนี้
            if (d.moveSource && d.moveSource.isPlaying && d.pitchWithSpeed && totalDist > 0.0001f)
            {
                float curDist = Vector3.Distance(start, d.door.position);
                float delta = Mathf.Max(0f, curDist - lastDist);
                lastDist = curDist;

                float baseSpeed = totalDist / duration; // หน่วย/วินาที
                float speed = (baseSpeed > 0f) ? (delta / Time.deltaTime) / baseSpeed : 0f;
                float s01 = Mathf.Clamp01(speed);
                d.moveSource.pitch = Mathf.Lerp(d.minPitch, d.maxPitch, s01);
            }

            yield return null;
        }

        d.door.position = targetPos;
        d.isOpen = toOpen;

        StopDoorSfx(d);
        d.routine = null;
    }

    void StopDoorSfx(DoorLink d)
    {
        if (d.moveSource && d.moveSource.isPlaying)
        {
            d.moveSource.Stop();
            d.moveSource.pitch = 1f;
        }
    }

    // ===== Helpers =====
    bool IsAllowed(Collider2D col)
    {
        if (!col) return false;
        if (allowedTags == null || allowedTags.Length == 0) return true;

        for (int i = 0; i < allowedTags.Length; i++)
        {
            string tag = allowedTags[i];
            if (!string.IsNullOrEmpty(tag) && col.CompareTag(tag))
                return true;
        }
        return false;
    }

    // ===== Public API =====
    public void ForceOpenAll()
    {
        if (mode == ActivationMode.OneShotOpen) oneShotConsumed = true;
        OpenAll();
    }

    public void ForceCloseAll()
    {
        if (mode == ActivationMode.OneShotOpen) oneShotConsumed = true;
        CloseAll();
    }

    public void ResetAllToStart()
    {
        foreach (var d in doors)
        {
            if (!d.door) continue;
            d.door.position = d.startPos;
            d.isOpen = false;
            StopDoorSfx(d);
            if (d.routine != null) StopCoroutine(d.routine);
            d.routine = null;
        }
        anyOpen = false;
        oneShotConsumed = false;
    }
}
