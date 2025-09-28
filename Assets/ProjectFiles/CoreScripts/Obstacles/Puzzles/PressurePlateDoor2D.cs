using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PressurePlateDoor2D : MonoBehaviour
{
    public enum ActivationMode { HoldToOpen, OneShotOpen }

    [Header("What can press this plate?")]
    public string[] allowedTags = new[] { "Player" };

    [Header("Plate Settings")]
    public ActivationMode mode = ActivationMode.HoldToOpen;

    [Header("Door Target")]
    public Transform door;
    public float moveDistanceY = 3f;
    public float moveDuration = 0.4f;
    public float closeDurationMultiplier = 1f;

    [Header("Random Direction")]
    public bool randomizeEachOpen = true;
    public bool randomizeOnStart = true;

    [Header("Easing")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Plate Visual (optional)")]
    public Transform plateGraphic;
    public float platePressOffsetY = -0.06f;
    public float platePressLerp = 15f;

    // ================= NEW: SFX =================
    [Header("SFX • Plate (optional)")]
    [Tooltip("AudioSource ที่จะใช้เล่นเสียงของแผ่นปุ่ม (กด/ปล่อย)")]
    public AudioSource plateSfxSource;
    public AudioClip pressDownSfx;
    public AudioClip releaseSfx;
    [Range(0f, 1f)] public float plateVolume = 1f;

    [Header("SFX • Door Move (optional)")]
    [Tooltip("AudioSource ที่ใช้เล่นเสียงตอนประตูกำลังเลื่อน (แนะนำแปะไว้ที่วัตถุประตู)")]
    public AudioSource doorMoveSource;
    [Tooltip("คลิปเสียง loop ตอนประตูกำลังเลื่อน")]
    public AudioClip doorMoveLoop;
    [Range(0f, 1f)] public float doorMoveVolume = 0.9f;
    public bool pitchWithSpeed = true;
    [Range(0.5f, 2f)] public float minPitch = 0.95f;
    [Range(0.5f, 2f)] public float maxPitch = 1.15f;
    // ============================================

    // --- runtime ---
    Vector3 doorStartPos;
    int pressCount = 0;
    bool doorOpen = false;
    bool oneShotConsumed = false;
    int openDirectionSign = +1;
    Coroutine doorRoutine;

    Vector3 plateGraphicStart;
    float plateVisualT = 0f;

    void Awake()
    {
        if (!door)
        {
            Debug.LogWarning($"[{name}] PressurePlateDoor2D: ยังไม่ได้ตั้ง Door Target");
            enabled = true;
        }
        if (door) doorStartPos = door.position;

        if (plateGraphic) plateGraphicStart = plateGraphic.localPosition;

        if (!randomizeEachOpen && randomizeOnStart)
            openDirectionSign = Random.value < 0.5f ? +1 : -1;
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

        
        if (wasIdle && plateSfxSource && pressDownSfx)
            plateSfxSource.PlayOneShot(pressDownSfx, plateVolume);

        if (mode == ActivationMode.HoldToOpen)
        {
            TryOpen();
        }
        else 
        {
            if (!oneShotConsumed)
            {
                oneShotConsumed = true;
                TryOpen();
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!IsAllowed(other)) return;

        pressCount = Mathf.Max(0, pressCount - 1);

        if (mode == ActivationMode.HoldToOpen && pressCount == 0)
        {
            
            if (plateSfxSource && releaseSfx)
                plateSfxSource.PlayOneShot(releaseSfx, plateVolume);

            TryClose();
        }
    }

    // ===== Core =====
    void TryOpen()
    {
        if (doorOpen) return;
        doorOpen = true;

        int sign = openDirectionSign;
        if (randomizeEachOpen)
            sign = (Random.value < 0.5f) ? +1 : -1;
        else
            openDirectionSign = sign;

        Vector3 target = door ? doorStartPos + new Vector3(0f, sign * moveDistanceY, 0f) : Vector3.zero;
        StartMove(target, moveDuration);
    }

    void TryClose()
    {
        if (!doorOpen) return;
        doorOpen = false;

        if (!door) return;
        StartMove(doorStartPos, moveDuration * Mathf.Max(0.01f, closeDurationMultiplier));
    }

    void StartMove(Vector3 targetPos, float duration)
    {
        if (!door) return;
        if (doorRoutine != null) StopCoroutine(doorRoutine);
        doorRoutine = StartCoroutine(MoveDoorCo(targetPos, duration));
    }

    IEnumerator MoveDoorCo(Vector3 targetPos, float duration)
    {
        Vector3 start = door.position;
        if (duration <= 0f)
        {
            door.position = targetPos;
            
            if (doorMoveSource && doorMoveSource.isPlaying)
            {
                doorMoveSource.Stop();
                doorMoveSource.pitch = 1f;
            }
            yield break;
        }

        // === START door-move SFX ===
        float totalDist = Vector3.Distance(start, targetPos);
        float lastDist = 0f;
        if (doorMoveSource && doorMoveLoop)
        {
            doorMoveSource.clip = doorMoveLoop;
            doorMoveSource.loop = true;
            doorMoveSource.volume = doorMoveVolume;
            doorMoveSource.pitch = 1f;
            doorMoveSource.Play();
            lastDist = 0f;
        }
        // ===========================

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float k = ease != null ? ease.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);
            door.position = Vector3.LerpUnclamped(start, targetPos, k);

            
            if (doorMoveSource && doorMoveSource.isPlaying && pitchWithSpeed && totalDist > 0.0001f)
            {
                float curDist = Vector3.Distance(start, door.position);
                float delta = Mathf.Max(0f, curDist - lastDist);
                lastDist = curDist;

                float baseSpeed = totalDist / duration; 
                float speed = (baseSpeed > 0f) ? (delta / Time.deltaTime) / baseSpeed : 0f;
                float s01 = Mathf.Clamp01(speed);
                doorMoveSource.pitch = Mathf.Lerp(minPitch, maxPitch, s01);
            }

            yield return null;
        }

        door.position = targetPos;

        // === STOP door-move SFX ===
        if (doorMoveSource && doorMoveSource.isPlaying)
        {
            doorMoveSource.Stop();
            doorMoveSource.pitch = 1f;
        }
        // ===========================

        doorRoutine = null;
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
    public void ForceOpen()
    {
        if (mode == ActivationMode.OneShotOpen) oneShotConsumed = true;
        TryOpen();
    }

    public void ForceClose()
    {
        if (mode == ActivationMode.OneShotOpen) oneShotConsumed = true;
        TryClose();
    }

    public void ResetDoorToStart()
    {
        if (door) door.position = doorStartPos;
        doorOpen = false;
        oneShotConsumed = false;

        
        if (doorMoveSource && doorMoveSource.isPlaying)
        {
            doorMoveSource.Stop();
            doorMoveSource.pitch = 1f;
        }
    }
}
