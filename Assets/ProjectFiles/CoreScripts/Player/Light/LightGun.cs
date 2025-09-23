using UnityEngine;
using UnityEngine.InputSystem;

public class LightGun : MonoBehaviour
{
    public enum FireMode { Hold, Toggle }

    [Header("Refs")]
    public Transform firePoint;                 // จุดปล่อยลำแสง
    public Camera mainCam;                      // เว้นว่าง = Camera.main
    public LineRenderer line;                   // เส้นเลเซอร์ (optional)
    public float lineZOffset = -0.1f;           // ให้เส้นลอยหน้า sprite

    [Header("Input (Action References)")]
    public InputActionReference fireAction;         // Gameplay/Fire
    public InputActionReference toggleLaserAction;  // Gameplay/ToggleLaser (ถ้ามี)

    [Header("Laser 2D")]
    public float maxDistance = 100f;
    public LayerMask hitMask = ~0;
    public FireMode fireMode = FireMode.Hold;
    public bool autoEnableLineWhileFiring = true;
    public bool lockZPlane = true;

    [Header("Energy (Stamina)")]
    public float maxEnergy = 100f;
    public float drainPerSecond = 25f;
    public float regenPerSecond = 15f;
    public float minEnergyToFire = 1f;
    public bool regenDelayAfterStop = true;
    public float regenDelay = 0.35f;

    [Tooltip("เปิดใช้เกณฑ์ % ที่ต้องรอหลังพลังงานหมดก่อนยิงได้อีก")]
    public bool useRechargeGate = true;
    [Range(0f, 1f)] public float rechargeGatePercent = 0.30f; // 30%

    [Header("Blocking / Pass-through")]
    public LayerMask blockMask = ~0;            // Walls/Ground/Puzzle
    public string[] passThroughTags;            // Tag ที่ยอมให้ทะลุ
    public bool includeTriggers = true;

    [Header("Puzzle Sear (continuous)")]
    [Tooltip("แท็กของเป้าหมายที่จะถูกนับเวลา 'จี่' (รับหลายแท็ก)")]
    public string[] puzzleTags;                 // << เปลี่ยนเป็นหลายแท็ก
    [Tooltip(">0 = บังคับเวลาจี่จากปืน, <=0 = ใช้เวลาบนเป้าหมาย")]
    public float searSecondsOverride = 0f;
    public bool autoAddSearableIfMissing = true;

    // รองรับไฟล์เดิม/ค่าเดิมใน Inspector (จะอัปเกรดเป็น array ให้อัตโนมัติ)
    [HideInInspector] public string puzzleTag = "Puzzle";

    [Header("Hit Effect (optional)")]
    public GameObject hitVfxPrefab;
    public bool stickVfxToSurface = false;

    [Header("Debug Draw")]
    public bool debugDraw = true;
    public Color debugColor = new Color(0f, 1f, 1f, 1f);
    public float debugDuration = 0f;

    // --- runtime ---
    bool wantFire;
    bool firingActive;
    float energy;
    float nextRegenTime;

    // เมื่อพลังงานหมด จะล็อกจนกว่าจะถึง % ที่กำหนด
    bool energyLockActive;

    GameObject spawnedHitVfx;

    Vector3 lastOrigin, lastEnd;
    bool lastHadRay;

    void Awake()
    {
        if (!mainCam) mainCam = Camera.main;
        if (!line) line = GetComponent<LineRenderer>();
        energy = maxEnergy;

        // อัปเกรดค่า Puzzle Tag เดิมให้เป็น array อัตโนมัติ (ครั้งแรก)
        if ((puzzleTags == null || puzzleTags.Length == 0) && !string.IsNullOrEmpty(puzzleTag))
            puzzleTags = new[] { puzzleTag };
    }

    void OnEnable()
    {
        if (fireAction && fireAction.action != null)
        {
            if (fireMode == FireMode.Hold)
            {
                fireAction.action.performed += OnFirePerformed_Hold;
                fireAction.action.canceled += OnFireCanceled_Hold;
            }
            else
            {
                fireAction.action.performed += OnFirePerformed_Toggle;
            }
            fireAction.action.Enable();
        }
        if (toggleLaserAction && toggleLaserAction.action != null)
        {
            toggleLaserAction.action.performed += _ => wantFire = !wantFire;
            toggleLaserAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (fireAction && fireAction.action != null)
        {
            if (fireMode == FireMode.Hold)
            {
                fireAction.action.performed -= OnFirePerformed_Hold;
                fireAction.action.canceled -= OnFireCanceled_Hold;
            }
            else
            {
                fireAction.action.performed -= OnFirePerformed_Toggle;
            }
            fireAction.action.Disable();
        }
        if (toggleLaserAction && toggleLaserAction.action != null)
            toggleLaserAction.action.Disable();
    }

    void OnFirePerformed_Hold(InputAction.CallbackContext _) { wantFire = true; }
    void OnFireCanceled_Hold(InputAction.CallbackContext _) { wantFire = false; }
    void OnFirePerformed_Toggle(InputAction.CallbackContext _) { wantFire = !wantFire; }

    void Update()
    {
        // ---------- Energy Gate ----------
        float threshold = minEnergyToFire;
        if (useRechargeGate && energyLockActive)
            threshold = Mathf.Max(minEnergyToFire, rechargeGatePercent * maxEnergy);

        // ปลดล็อกอัตโนมัติเมื่อถึง % ที่กำหนด
        if (useRechargeGate && energyLockActive && energy >= rechargeGatePercent * maxEnergy)
            energyLockActive = false;

        bool canStartFire = (energy >= threshold);

        if (!wantFire) firingActive = false;
        else if (wantFire && canStartFire) firingActive = true;

        if (firingActive)
        {
            Vector3 origin3 = firePoint ? firePoint.position : transform.position;
            Vector2 origin = origin3;
            Vector2 dir = ComputeAimDirectionFromMouse2D(origin3);
            if (dir.sqrMagnitude < 1e-6f) { StopFiringVisual(); return; }

            // เลือกของแข็งชิ้นแรก (ไม่ทะลุ)
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, maxDistance, blockMask);
            RaycastHit2D? firstSolid = null;

            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hits)
                {
                    if (h.collider == null) continue;
                    if (!includeTriggers && h.collider.isTrigger) continue;

                    bool pass = false;
                    if (passThroughTags != null)
                    {
                        for (int i = 0; i < passThroughTags.Length; i++)
                        {
                            string t = passThroughTags[i];
                            if (!string.IsNullOrEmpty(t) && h.collider.CompareTag(t))
                            { pass = true; break; }
                        }
                    }
                    if (pass) continue;

                    firstSolid = h;
                    break;
                }
            }

            Vector3 endPoint3;
            Collider2D hitCol = null;
            Vector2 hitNormal = Vector2.zero;

            if (firstSolid.HasValue)
            {
                var h = firstSolid.Value;
                endPoint3 = new Vector3(h.point.x, h.point.y, origin3.z);
                hitCol = h.collider;
                hitNormal = h.normal;
            }
            else
            {
                Vector2 end2 = origin + dir * maxDistance;
                endPoint3 = new Vector3(end2.x, end2.y, origin3.z);
            }

            // วาดเส้น
            Vector3 drawOrigin = new Vector3(origin3.x, origin3.y, origin3.z + lineZOffset);
            Vector3 drawEnd = new Vector3(endPoint3.x, endPoint3.y, endPoint3.z + lineZOffset);

            if (line && autoEnableLineWhileFiring) line.enabled = true;
            if (line)
            {
                line.positionCount = 2;
                line.SetPosition(0, drawOrigin);
                line.SetPosition(1, drawEnd);
            }
            if (debugDraw) Debug.DrawLine(drawOrigin, drawEnd, debugColor, debugDuration);
            lastOrigin = drawOrigin; lastEnd = drawEnd; lastHadRay = true;

            // VFX ปลายลำแสง
            if (hitCol != null && hitVfxPrefab)
            {
                float ang = Mathf.Atan2(hitNormal.y, hitNormal.x) * Mathf.Rad2Deg;
                Quaternion rot = Quaternion.Euler(0, 0, ang);
                if (!spawnedHitVfx) spawnedHitVfx = Instantiate(hitVfxPrefab, endPoint3, rot);
                else spawnedHitVfx.transform.SetPositionAndRotation(endPoint3, rot);
                if (stickVfxToSurface) spawnedHitVfx.transform.SetParent(hitCol.transform);
            }
            else if (spawnedHitVfx)
            {
                Destroy(spawnedHitVfx);
                spawnedHitVfx = null;
            }

            // ถ้าโดน Puzzle ใด ๆ ในรายการ → นับเวลา "จี่" แบบต่อเนื่อง
            if (hitCol != null && HasAnyTag(hitCol, puzzleTags))
            {
                var sear = hitCol.GetComponent<PuzzleSearable2D>();
                if (!sear && autoAddSearableIfMissing)
                    sear = hitCol.gameObject.AddComponent<PuzzleSearable2D>();

                if (sear)
                {
                    float overrideRequired = searSecondsOverride > 0f ? searSecondsOverride : -1f;
                    sear.SearTick(Time.deltaTime,
                                  new Vector2(endPoint3.x, endPoint3.y),
                                  hitNormal,
                                  overrideRequired,
                                  -1f); // ใช้ grace ของเป้าหมาย
                }
            }

            // Drain energy
            energy = Mathf.Max(0f, energy - drainPerSecond * Time.deltaTime);
            nextRegenTime = Time.time + (regenDelayAfterStop ? regenDelay : 0f);

            // หมดพลังงาน → ดับ และเปิดล็อก
            if (energy <= 0f)
            {
                firingActive = false;
                if (useRechargeGate) energyLockActive = true;
            }
        }
        else
        {
            StopFiringVisual();
            lastHadRay = false;

            if (Time.time >= nextRegenTime)
                energy = Mathf.Min(maxEnergy, energy + regenPerSecond * Time.deltaTime);
        }
    }

    Vector2 ComputeAimDirectionFromMouse2D(Vector3 firePoint3)
    {
        if (!mainCam) return Vector2.right;

        Vector2 mouseScreen = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

        // ป้องกัน z=0 (หลุด frustum) – ใช้ depth อย่างน้อย nearClipPlane
        float __depth = Mathf.Abs(mainCam.transform.position.z - firePoint3.z);
        if (__depth < mainCam.nearClipPlane) __depth = mainCam.nearClipPlane;

        Vector3 mouseWorld3 = mainCam.ScreenToWorldPoint(new Vector3(
            mouseScreen.x, mouseScreen.y,
            __depth
        ));
        if (lockZPlane) mouseWorld3.z = firePoint3.z;

        Vector2 from = firePoint3;
        Vector2 to = new Vector2(mouseWorld3.x, mouseWorld3.y);
        return (to - from).normalized;
    }

    void StopFiringVisual()
    {
        if (line && autoEnableLineWhileFiring) line.enabled = false;
        if (spawnedHitVfx) { Destroy(spawnedHitVfx); spawnedHitVfx = null; }
    }

    void OnDrawGizmos()
    {
        if (!debugDraw) return;
        if (lastHadRay)
        {
            Gizmos.color = debugColor;
            Gizmos.DrawLine(lastOrigin, lastEnd);
            Gizmos.DrawSphere(lastEnd, 0.05f);
        }
    }

    public float Energy01 => maxEnergy > 0f ? energy / maxEnergy : 0f;
    public void AddEnergy(float amount) { energy = Mathf.Clamp(energy + amount, 0f, maxEnergy); }

    // -------- helpers --------
    static bool HasAnyTag(Collider2D col, string[] tags)
    {
        if (!col || tags == null) return false;
        for (int i = 0; i < tags.Length; i++)
        {
            string t = tags[i];
            if (!string.IsNullOrEmpty(t) && col.CompareTag(t)) return true;
        }
        return false;
    }
}
