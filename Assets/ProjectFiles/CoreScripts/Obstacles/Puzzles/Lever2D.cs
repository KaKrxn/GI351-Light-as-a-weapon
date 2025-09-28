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

    [Header("Door")]
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

    // === NEW: Lever flip SFX ===
    [Header("SFX • Lever Flip (optional)")]
    [Tooltip("AudioSource สำหรับเสียงคันโยก (ถ้าเว้นว่าง จะใช้ sfxSource)")]
    public AudioSource leverSfxSource;
    [Tooltip("เสียงตอนสับไปตำแหน่ง ON")]
    public AudioClip flipOnSfx;
    [Tooltip("เสียงตอนสับกลับตำแหน่ง OFF")]
    public AudioClip flipOffSfx;
    [Tooltip("เสียงกระทบ/ล็อก ตอนคันโยกขยับเสร็จ")]
    public AudioClip flipLatchSfx;
    [Range(0f, 1f)] public float flipVolume = 1f;
    public bool playLatchAtEnd = true;

    
    [Header("SFX • Door Move (optional)")]
    [Tooltip("AudioSource ที่จะใช้เล่นเสียงประตู (แนะนำแปะไว้ที่วัตถุประตู)")]
    public AudioSource doorMoveSource;
    [Tooltip("คลิปเสียงตอนประตูกำลังขยับ (ควรเป็นเสียงที่ loop ได้)")]
    public AudioClip doorMoveLoop;
    [Range(0, 1f)] public float doorMoveVolume = 0.9f;
    [Tooltip("ปรับ pitch ตามความเร็วการเคลื่อน (เล็กน้อย)")]
    public bool pitchWithSpeed = true;
    [Range(0.5f, 2f)] public float minPitch = 0.95f;
    [Range(0.5f, 2f)] public float maxPitch = 1.15f;

    // ---- runtime ----
    bool playerInRange;
    GameObject currentPlayer;
    bool isMoving;
    bool opened;               
    Vector3 doorClosedPos;

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

        
        if (leverHandle && requireMode == RequireMode.RequireItem)
        {
            leverHandle.gameObject.SetActive(false);
        }

       
        if (!leverSfxSource) leverSfxSource = sfxSource;
    }

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
        if (oneShot && opened) return; 

        
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
                Debug.Log($"[Lever2D] Need {requiredAmount} x {requiredItemId}");
                return;
            }

            if (leverHandle && !leverHandle.gameObject.activeSelf)
            {
                leverHandle.gameObject.SetActive(true);
                var e = leverHandle.localEulerAngles;
                leverHandle.localEulerAngles = new Vector3(e.x, e.y, handleOffAngleZ);
            }

            if (consumeItemOnUse && !inv.Consume(requiredItemId, requiredAmount))
                return;
        }
        else
        {
            if (requireMode == RequireMode.Free && leverHandle && !leverHandle.gameObject.activeSelf)
                leverHandle.gameObject.SetActive(true);
        }

        
        if (sfxSource && interactSfx) sfxSource.PlayOneShot(interactSfx);

        
        bool willOpen = !opened; 
        var flipSrc = leverSfxSource ? leverSfxSource : sfxSource;
        if (flipSrc)
        {
            var clip = willOpen ? flipOnSfx : flipOffSfx;
            if (clip) flipSrc.PlayOneShot(clip, flipVolume);
        }

        
        if (leverHandle)
            StartCoroutine(Co_AnimateHandle(willOpen ? handleOnAngleZ : handleOffAngleZ, willOpen));

        
        if (door)
            StartCoroutine(Co_MoveDoor());

        opened = oneShot ? true : !opened;
    }

    IEnumerator Co_AnimateHandle(float targetZ, bool flippingToOn)
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

        
        if (playLatchAtEnd && (leverSfxSource || sfxSource) && flipLatchSfx)
        {
            var src = leverSfxSource ? leverSfxSource : sfxSource;
            src.PlayOneShot(flipLatchSfx, flipVolume);
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

        if (!opened) 
        {
            Vector3 axis = (doorAxis == DoorAxis.Y) ? Vector3.up : Vector3.right;
            float sign = (doorDirection == DoorSign.Positive) ? 1f : -1f;
            end = doorClosedPos + axis * sign * doorMoveDistance;
        }
        else 
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

            if (doorMoveSource && doorMoveSource.isPlaying && pitchWithSpeed)
            {
                float curDist = Vector3.Distance(start, door.position);
                float delta = Mathf.Max(0f, curDist - lastDist);
                lastDist = curDist;

                float baseSpeed = doorMoveDistance / dur;
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

        isMoving = false;
    }
}
