using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Lever2D : MonoBehaviour
{
    public enum RequireMode { Free, RequireItem }
    public enum DoorAxis { X, Y }
    public enum DoorSign { Positive, Negative } // X:+ ขวา/− ซ้าย | Y:+ ขึ้น/− ลง

    [Header("Interact")]
    public string[] interactTags = new[] { "Player" };
    public KeyCode interactKey = KeyCode.E;
    public RequireMode requireMode = RequireMode.Free;

    [Header("Item Requirement")]
    [Tooltip("รหัสไอเท็มที่ต้องใช้ (เช่น Handle)")]
    public string requiredItemId = "Handle";
    [Min(1)] public int requiredAmount = 1;          // ใช้กี่ชิ้นต่อการ 'เปิด' 1 ครั้ง
    public bool consumeItemOnUse = true;             // ใช้แล้วลบออก (เฉพาะตอนเปิดสำเร็จ)

    [Header("Lever Visual")]
    [Tooltip("ใส่เฉพาะ 'ด้ามคันโยก' (ฐานไม่ต้อง)")]
    public Transform leverHandle;
    public float handleOffAngleZ = 0f;
    public float handleOnAngleZ = -45f;
    public float handleAnimTime = 0.15f;

    [Header("Door")]
    public Transform door;
    public DoorAxis doorAxis = DoorAxis.Y;
    public DoorSign doorDirection = DoorSign.Positive;
    public float doorMoveDistance = 3f;
    public float doorMoveTime = 1f;
    public AnimationCurve doorEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("ใช้ครั้งเดียวแล้วล็อก (ปิด = toggle เปิด/ปิดได้)")]
    public bool oneShot = true;

    [Header("SFX")]
    public AudioSource sfxSource;
    public AudioClip interactSfx;

    // ---- runtime ----
    bool playerInRange;
    GameObject currentPlayer;
    bool isMoving;
    bool opened;               // สถานะประตูเปิดอยู่ไหม
    Vector3 doorClosedPos;

    void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true;
    }

    void Awake()
    {
        if (door) doorClosedPos = door.position;

        // ตั้งมุมเริ่มต้นของด้าม
        if (leverHandle)
        {
            var e = leverHandle.localEulerAngles;
            leverHandle.localEulerAngles = new Vector3(e.x, e.y, handleOffAngleZ);
        }

        // ★ ถ้าเป็น RequireItem ให้ "ซ่อนด้าม" ไว้ก่อน
        if (leverHandle && requireMode == RequireMode.RequireItem)
        {
            leverHandle.gameObject.SetActive(false);
        }
    }

    // อัปเดตใน Editor เวลาเปลี่ยนโหมด เพื่อให้เห็นผลทันที
    void OnValidate()
    {
        if (leverHandle && !Application.isPlaying)
        {
            if (requireMode == RequireMode.RequireItem)
                leverHandle.gameObject.SetActive(false);
            else
                leverHandle.gameObject.SetActive(true);
        }
    }

    void Update()
    {
        if (!playerInRange || isMoving) return;
        if (Input.GetKeyDown(interactKey)) TryInteract(currentPlayer);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsInteractTag(other.tag))
        {
            playerInRange = true;
            currentPlayer = other.gameObject;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject == currentPlayer)
        {
            playerInRange = false;
            currentPlayer = null;
        }
    }

    bool IsInteractTag(string tagStr)
    {
        if (interactTags == null || interactTags.Length == 0) return true;
        foreach (var t in interactTags)
            if (!string.IsNullOrEmpty(t) && tagStr == t) return true;
        return false;
    }

    void TryInteract(GameObject player)
    {
        // oneShot: เปิดไปแล้วไม่ให้ทำซ้ำ
        if (oneShot && opened) return;

        // ถ้าต้องใช้ไอเท็ม: เช็กและ "กิน" ตอนกำลังจะเปิดเท่านั้น
        if (requireMode == RequireMode.RequireItem && !opened)
        {
            var inv = player ? player.GetComponentInParent<InventoryLite>() : null;
            if (!inv)
            {
                Debug.LogWarning("[Lever2D] InventoryLite not found on player.");
                return;
            }

            if (!inv.HasItem(requiredItemId, requiredAmount))
            {
                // ไม่มีของพอ → ไม่ทำอะไร และยังซ่อนด้ามต่อไป
                Debug.Log($"[Lever2D] Need {requiredAmount} x {requiredItemId}");
                return;
            }

            // มีของพอ → แสดงด้ามและกินของ (ถ้าตั้งให้กิน)
            if (leverHandle && !leverHandle.gameObject.activeSelf)
            {
                // เปิดให้เห็น แล้วเซ็ตมุมเริ่มก่อนอนิเมต
                leverHandle.gameObject.SetActive(true);
                var e = leverHandle.localEulerAngles;
                leverHandle.localEulerAngles = new Vector3(e.x, e.y, handleOffAngleZ);
            }

            if (consumeItemOnUse && !inv.Consume(requiredItemId, requiredAmount))
            {
                // กันกรณีระบบอื่นแทรกแซงจนกินไม่สำเร็จ
                return;
            }
        }
        else
        {
            // โหมด Free: ถ้าเผลอซ่อนอยู่ ให้เปิดไว้ (กันเคสปรับโหมดภายหลัง)
            if (requireMode == RequireMode.Free && leverHandle && !leverHandle.gameObject.activeSelf)
                leverHandle.gameObject.SetActive(true);
        }

        // เล่นเสียง
        if (sfxSource && interactSfx) sfxSource.PlayOneShot(interactSfx);

        // อนิเมตด้าม
        if (leverHandle)
            StartCoroutine(Co_AnimateHandle(opened ? handleOffAngleZ : handleOnAngleZ));

        // ขยับประตู
        if (door)
            StartCoroutine(Co_MoveDoor());

        opened = oneShot ? true : !opened;
    }

    IEnumerator Co_AnimateHandle(float targetZ)
    {
        if (!leverHandle) yield break;

        isMoving = true;
        float t = 0f;
        float startZ = NormalizeAngle(leverHandle.localEulerAngles.z);

        while (t < 1f)
        {
            t += (handleAnimTime > 0f ? Time.deltaTime / handleAnimTime : 1f);
            float z = Mathf.LerpAngle(startZ, targetZ, t);
            var e = leverHandle.localEulerAngles;
            e.z = z;
            leverHandle.localEulerAngles = e;
            yield return null;
        }

        isMoving = false;
    }

    static float NormalizeAngle(float z)
    {
        z %= 360f;
        if (z > 180f) z -= 360f;
        return z;
    }

    IEnumerator Co_MoveDoor()
    {
        isMoving = true;

        Vector3 start = door.position;
        Vector3 end;

        if (!opened) // จะเปิด
        {
            Vector3 axis = (doorAxis == DoorAxis.Y) ? Vector3.up : Vector3.right;
            float sign = (doorDirection == DoorSign.Positive) ? 1f : -1f;
            end = doorClosedPos + axis * sign * doorMoveDistance;
        }
        else // จะปิด (สำหรับ toggle)
        {
            end = doorClosedPos;
        }

        float t = 0f;
        float dur = Mathf.Max(doorMoveTime, 0.0001f);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float k = doorEase != null ? doorEase.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);
            door.position = Vector3.Lerp(start, end, k);
            yield return null;
        }

        door.position = end;
        isMoving = false;
    }
}
