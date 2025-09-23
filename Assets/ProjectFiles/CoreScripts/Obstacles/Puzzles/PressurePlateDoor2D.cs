using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PressurePlateDoor2D : MonoBehaviour
{
    public enum ActivationMode
    {
        HoldToOpen,   // เหยียบค้างถึงเปิด ปล่อยแล้วปิด
        OneShotOpen   // แตะครั้งแรกแล้วเปิดถาวร
    }

    [Header("What can press this plate?")]
    [Tooltip("แท็กที่อนุญาตให้กดปุ่มได้ (ปล่อยว่าง = อนุญาตทุกแท็ก)")]
    public string[] allowedTags = new[] { "Player" };

    [Header("Plate Settings")]
    [Tooltip("โหมดทำงาน: Hold (เหยียบค้าง) หรือ OneShot (เหยียบครั้งเดียวเปิดถาวร)")]
    public ActivationMode mode = ActivationMode.HoldToOpen;

    [Header("Door Target")]
    [Tooltip("วัตถุ/ประตูที่จะถูกขยับขึ้น/ลงตามแกน Y")]
    public Transform door;             // ใส่ GameObject ก็ได้ Unity จะอ้าง Transform ให้
    [Tooltip("ระยะที่ประตูจะเลื่อนตามแกน Y (หน่วย: world space)")]
    public float moveDistanceY = 3f;
    [Tooltip("เวลาในการเลื่อน (วินาที)")]
    public float moveDuration = 0.4f;
    [Tooltip("คูณเวลาเพื่อหน่วงตอนกลับตำแหน่ง (เช่น 1 = เท่ากัน, >1 = ช้าลง)")]
    public float closeDurationMultiplier = 1f;

    [Header("Random Direction")]
    [Tooltip("สุ่มทิศทาง (+Y หรือ -Y) ทุกครั้งที่เปิด")]
    public bool randomizeEachOpen = true;
    [Tooltip("ถ้าไม่สุ่มทุกครั้ง จะสุ่มครั้งแรกและใช้ทิศเดิมในครั้งต่อ ๆ ไป")]
    public bool randomizeOnStart = true;

    [Header("Easing")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Plate Visual (optional)")]
    [Tooltip("ถ้าอยากให้ปุ่มยุบ-เด้ง ให้ใส่ทรานส์ฟอร์มชิ้นกราฟิกของปุ่ม")]
    public Transform plateGraphic;
    public float platePressOffsetY = -0.06f;
    public float platePressLerp = 15f;

    // --- runtime ---
    Vector3 doorStartPos;
    int pressCount = 0;        // จำนวนวัตถุที่กำลังกดอยู่
    bool doorOpen = false;     // สถานะประตูปัจจุบัน
    bool oneShotConsumed = false;
    int openDirectionSign = +1; // +1 = ขึ้น, -1 = ลง
    Coroutine doorRoutine;

    Vector3 plateGraphicStart;
    float plateVisualT = 0f;   // 0 = เด้ง, 1 = ยุบ

    void Awake()
    {
        if (!door)
        {
            Debug.LogWarning($"[{name}] PressurePlateDoor2D: ยังไม่ได้ตั้ง Door Target");
            enabled = true; // ยังให้ทำงานได้ (จะไม่ขยับประตู)
        }
        if (door) doorStartPos = door.position;

        if (plateGraphic) plateGraphicStart = plateGraphic.localPosition;

        if (!randomizeEachOpen && randomizeOnStart)
            openDirectionSign = Random.value < 0.5f ? +1 : -1;
    }

    void Update()
    {
        // อนิเมชันกดปุ่ม (ถ้ามีกราฟิก)
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
        pressCount++;

        if (mode == ActivationMode.HoldToOpen)
        {
            // เปิดทันทีที่มีของเหยียบ
            TryOpen();
        }
        else // OneShotOpen
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
            // ไม่มีของเหยียบแล้ว → ปิด
            TryClose();
        }
    }

    // ===== Core =====
    void TryOpen()
    {
        if (doorOpen) return;
        doorOpen = true;

        // เลือกทิศทางสุ่มถ้าต้องการ
        int sign = openDirectionSign;
        if (randomizeEachOpen)
            sign = (Random.value < 0.5f) ? +1 : -1;
        else
            openDirectionSign = sign; // จดจำทิศที่สุ่มครั้งแรก

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
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float k = ease != null ? ease.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);
            door.position = Vector3.LerpUnclamped(start, targetPos, k);
            yield return null;
        }
        door.position = targetPos;
    }

    // ===== Helpers =====
    bool IsAllowed(Collider2D col)
    {
        if (!col) return false;
        // ไม่มีการกำหนด allowedTags → อนุญาตทุกแท็ก
        if (allowedTags == null || allowedTags.Length == 0) return true;

        for (int i = 0; i < allowedTags.Length; i++)
        {
            string tag = allowedTags[i];
            if (!string.IsNullOrEmpty(tag) && col.CompareTag(tag))
                return true;
        }
        return false;
    }

    // ===== Public API (เผื่อเรียกจากสคริปต์อื่น/Timeline) =====
    public void ForceOpen()
    {
        if (mode == ActivationMode.OneShotOpen) oneShotConsumed = true;
        TryOpen();
    }

    public void ForceClose()
    {
        if (mode == ActivationMode.OneShotOpen) oneShotConsumed = true; // เปิดถาวรเป็นดีฟอลต์ แต่ถ้าอยากบังคับปิดก็ทำได้
        TryClose();
    }

    public void ResetDoorToStart()
    {
        if (door) door.position = doorStartPos;
        doorOpen = false;
        oneShotConsumed = false;
    }
}
