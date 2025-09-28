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
    [Min(1)] public int requiredAmount = 1;
    public bool consumeItemOnUse = true;

    [Header("Lever Visual")]
    [Tooltip("ใส่เฉพาะ 'ด้ามคันโยก' (ฐานไม่ต้อง)")]
    public Transform leverHandle;
    public float handleOffAngleZ = 0f;
    public float handleOnAngleZ = -45f;
    public float handleAnimTime = 0.15f;

    [Header("Door / Platform")]
    public Transform door;
    public DoorAxis doorAxis = DoorAxis.Y;
    public DoorSign doorDirection = DoorSign.Positive;
    public float doorMoveDistance = 3f;
    public float doorMoveTime = 1f;
    public AnimationCurve doorEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("ใช้ครั้งเดียวแล้วล็อก (ปิด = toggle เปิด/ปิดได้)")]
    public bool oneShot = true;

    [Header("SFX • Interact (single click)")]
    public AudioSource sfxSource;
    public AudioClip interactSfx;

    [Header("SFX • Lever Flip (optional)")]
    public AudioSource leverSfxSource;     // ถ้าเว้นว่าง จะใช้ sfxSource
    public AudioClip flipOnSfx;
    public AudioClip flipOffSfx;
    public AudioClip flipLatchSfx;
    [Range(0f, 1f)] public float flipVolume = 1f;
    public bool playLatchAtEnd = true;

    [Header("SFX • Door Move (optional)")]
    public AudioSource doorMoveSource;     // แนะนำแปะไว้ที่วัตถุประตู/ลิฟต์
    public AudioClip doorMoveLoop;         // ควรเป็นเสียงที่ loop ได้
    [Range(0, 1f)] public float doorMoveVolume = 0.9f;
    public bool pitchWithSpeed = true;
    [Range(0.5f, 2f)] public float minPitch = 0.95f;
    [Range(0.5f, 2f)] public float maxPitch = 1.15f;

    // ----------------- NEW: Elevator Mode -----------------
    [Header("Elevator Mode (optional)")]
    [Tooltip("เปิดใช้งานเพื่อให้แพลตฟอร์มทำงานเหมือนลิฟต์")]
    public bool elevatorMode = false;

    [Tooltip("แท็กที่อนุญาตให้ 'ขึ้นลิฟต์' (จะถูกยึดติดระหว่างการเคลื่อน)")]
    public string[] riderTags = new[] { "Player" };

    [Tooltip("ถ้าเว้นว่าง จะยึดกับ 'door' ตรง ๆ")]
    public Transform riderAttachAnchor;

    [Tooltip("กันกดสแปม: ล็อกการกดจนกว่าจะถึงจุดหมาย")]
    public bool lockInteractUntilArrive = true;
    // ------------------------------------------------------

    // ---- runtime ----
    bool playerInRange;
    GameObject currentPlayer;
    bool isHandleMoving;
    bool isDoorMoving;
    bool opened;                         // ประตู/ลิฟต์อยู่ที่ปลายทางหรือยัง
    bool interactLocked;                 // ล็อกจนกว่าจะถึงจุดหมาย (กันสแปม)
    Vector3 doorClosedPos;

    // สำหรับผู้โดยสารลิฟต์
    Transform attachedRider;
    Transform riderPrevParent;

    void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true;
    }

    void Awake()
    {
        if (door) doorClosedPos = door.position;

        if (leverHandle)
        {
            var e = leverHandle.localEulerAngles;
            leverHandle.localEulerAngles = new Vector3(e.x, e.y, handleOffAngleZ);
        }

        // ถ้าเป็น RequireItem ให้ซ่อนด้ามก่อน
        if (leverHandle && requireMode == RequireMode.RequireItem)
            leverHandle.gameObject.SetActive(false);

        if (!leverSfxSource) leverSfxSource = sfxSource;
    }

    void OnValidate()
    {
        if (leverHandle && !Application.isPlaying)
        {
            leverHandle.gameObject.SetActive(requireMode != RequireMode.RequireItem);
        }
    }

    void Update()
    {
        if (!playerInRange || IsBusy()) return;
        if (Input.GetKeyDown(interactKey)) TryInteract(currentPlayer);
    }

    bool IsBusy()
    {
        // บิสซี่ถ้าด้ามกำลังขยับ หรือ ประตูกำลังเคลื่อน หรือ ล็อกกันสแปม
        return isHandleMoving || isDoorMoving || (lockInteractUntilArrive && interactLocked);
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

    bool IsRiderTag(string tagStr)
    {
        if (riderTags == null || riderTags.Length == 0) return true;
        foreach (var t in riderTags)
            if (!string.IsNullOrEmpty(t) && tagStr == t) return true;
        return false;
    }

    void TryInteract(GameObject player)
    {
        if (IsBusy()) return;            // กันสแปม: ถ้ายังไม่ถึงจุดหมาย/กำลังขยับอยู่ ห้ามกด
        if (oneShot && opened) return;   // ใช้ครั้งเดียว

        // Require item ตอนกำลัง "จะเปิด"
        if (requireMode == RequireMode.RequireItem && !opened)
        {
            var inv = player ? player.GetComponentInParent<InventoryLite>() : null;
            if (!inv) { Debug.LogWarning("[Lever2D] InventoryLite not found on player."); return; }
            if (!inv.HasItem(requiredItemId, requiredAmount))
            {
                Debug.Log($"[Lever2D] Need {requiredAmount} x {requiredItemId}");
                return;
            }
            if (leverHandle && !leverHandle.gameObject.activeSelf)
            {
                leverHandle.gameObject.SetActive(true);
                var e = leverHandle.localEulerAngles;
                leverHandle.localEulerAngles = new Vector3(e.x, e.y, handleOffAngleZ);
            }
            if (consumeItemOnUse && !inv.Consume(requiredItemId, requiredAmount)) return;
        }
        else
        {
            if (requireMode == RequireMode.Free && leverHandle && !leverHandle.gameObject.activeSelf)
                leverHandle.gameObject.SetActive(true);
        }

        // SFX คลิกครั้งเดียว
        if (sfxSource && interactSfx) sfxSource.PlayOneShot(interactSfx);

        // เสียงสับคันโยก
        bool willOpen = !opened; // กำลังจะไปทิศเปิด?
        var flipSrc = leverSfxSource ? leverSfxSource : sfxSource;
        if (flipSrc)
        {
            var clip = willOpen ? flipOnSfx : flipOffSfx;
            if (clip) flipSrc.PlayOneShot(clip, flipVolume);
        }

        // อนิเมตด้าม
        if (leverHandle)
            StartCoroutine(Co_AnimateHandle(willOpen ? handleOnAngleZ : handleOffAngleZ));

        // ---- ELEVATOR: แนบผู้เล่นก่อนเริ่มเคลื่อน ----
        if (elevatorMode && player && IsRiderTag(player.tag))
            AttachRider(player.transform);

        // เริ่มเคลื่อนแพลตฟอร์ม/ประตู
        if (door)
            StartCoroutine(Co_MoveDoor());

        opened = oneShot ? true : !opened;
    }

    IEnumerator Co_AnimateHandle(float targetZ)
    {
        if (!leverHandle) yield break;
        isHandleMoving = true;

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

        if (playLatchAtEnd && (leverSfxSource || sfxSource) && flipLatchSfx)
        {
            var src = leverSfxSource ? leverSfxSource : sfxSource;
            src.PlayOneShot(flipLatchSfx, flipVolume);
        }

        isHandleMoving = false;
    }

    static float NormalizeAngle(float z)
    {
        z %= 360f;
        if (z > 180f) z -= 360f;
        return z;
    }

    IEnumerator Co_MoveDoor()
    {
        isDoorMoving = true;
        if (lockInteractUntilArrive) interactLocked = true;

        Vector3 start = door.position;
        Vector3 end;

        if (!opened) // จะเปิด/ขึ้นลิฟต์
        {
            Vector3 axis = (doorAxis == DoorAxis.Y) ? Vector3.up : Vector3.right;
            float sign = (doorDirection == DoorSign.Positive) ? 1f : -1f;
            end = doorClosedPos + axis * sign * doorMoveDistance;
        }
        else // จะปิด/กลับฐาน
        {
            end = doorClosedPos;
        }

        float t = 0f;
        float dur = Mathf.Max(doorMoveTime, 0.0001f);

        // START door-move SFX
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

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float k = doorEase != null ? doorEase.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);
            door.position = Vector3.Lerp(start, end, k);

            // ปรับ pitch ตามความเร็วเลื่อน
            if (doorMoveSource && doorMoveSource.isPlaying && pitchWithSpeed)
            {
                float curDist = Vector3.Distance(start, door.position);
                float delta = Mathf.Max(0f, curDist - lastDist);
                lastDist = curDist;

                float baseSpeed = Vector3.Distance(start, end) / dur;
                float speed = (baseSpeed > 0f) ? (delta / Time.deltaTime) / baseSpeed : 0f;
                float speed01 = Mathf.Clamp01(speed);
                doorMoveSource.pitch = Mathf.Lerp(minPitch, maxPitch, speed01);
            }

            yield return null;
        }

        door.position = end;

        // STOP door-move SFX
        if (doorMoveSource && doorMoveSource.isPlaying)
        {
            doorMoveSource.Stop();
            doorMoveSource.pitch = 1f;
        }

        // ถึงจุดหมายแล้ว → ปลดล็อกการกด / ปล่อยผู้โดยสาร
        if (lockInteractUntilArrive) interactLocked = false;

        if (elevatorMode)
            DetachRider();

        isDoorMoving = false;
    }

    // ---------- Elevator helpers ----------
    void AttachRider(Transform rider)
    {
        if (!rider || attachedRider) return;
        riderPrevParent = rider.parent;
        var anchor = riderAttachAnchor ? riderAttachAnchor : door;
        rider.SetParent(anchor, true);   // true = keep world position
        attachedRider = rider;
    }

    void DetachRider()
    {
        if (!attachedRider) return;
        attachedRider.SetParent(riderPrevParent, true);
        attachedRider = null;
        riderPrevParent = null;
    }

    void OnDisable()
    {
        // กันค้างถ้าถูกปิดระหว่างทาง
        if (attachedRider) DetachRider();

        if (doorMoveSource && doorMoveSource.isPlaying)
        {
            doorMoveSource.Stop();
            doorMoveSource.pitch = 1f;
        }
        isDoorMoving = false;
        interactLocked = false;
    }
}
