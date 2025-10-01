using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[AddComponentMenu("Game/Spawning/Spawn Manager 2D")]
public class SpawnManager2D : MonoBehaviour
{
    [Header("Prefab(s) to spawn")]
    [Tooltip("ใส่พรีแฟบที่จะเกิด (เลือกสุ่มตัวใดตัวหนึ่ง)")]
    public GameObject[] prefabs;

    [Header("Spawn Points")]
    [Tooltip("จุดเกิด (เลือกสุ่มหนึ่งจุดต่อการเกิด)")]
    public Transform[] points;

    [Tooltip("ถ้าใส่ root ตรงนี้ จะ auto-collect ลูกทั้งหมดมาเป็นจุดเกิด (ยกเว้นตัว root เอง)")]
    public Transform pointsRoot;

    [Tooltip("เก็บลูกจาก root อัตโนมัติทุกครั้งที่แก้ค่าบน Inspector")]
    public bool autoCollectFromRoot = true;

    [Tooltip("ให้เก็บลูกที่ inactive ด้วยหรือไม่")]
    public bool includeInactiveChildren = true;

    [Header("Timing")]
    [Tooltip("ดีเลย์ก่อนเริ่มสปาวน์ครั้งแรก")]
    public float firstDelay = 0f;

    [Tooltip("ใช้ช่วงเวลาสุ่ม (Min/Max) แทนการตั้งค่าคงที่")]
    public bool useIntervalRange = false;

    [Tooltip("ช่วงเวลาเกิด (คงที่ ถ้าไม่ติ๊ก useIntervalRange)")]
    public float interval = 2f;

    [Tooltip("ช่วงสุ่มเวลาเกิด (Min, Max) เมื่อเปิด useIntervalRange")]
    public Vector2 intervalRange = new Vector2(1f, 3f);

    [Header("Limits")]
    [Tooltip("จำนวนรวมที่จะเกิดทั้งหมด (-1 = ไม่จำกัด)")]
    public int totalToSpawn = -1;

    [Tooltip("จำนวนที่มีชีวิตอยู่พร้อมกันสูงสุด (-1 = ไม่จำกัด)")]
    public int maxAlive = -1;

    [Header("Placement")]
    [Tooltip("กระจายตำแหน่งแบบสุ่มรอบจุดเกิด (หน่วย world)")]
    public Vector2 positionJitter = Vector2.zero;

    [Tooltip("ให้หมุนสุ่มรอบแกน Z (2D)")]
    public bool randomizeRotationZ = false;

    public Vector2 randomRotationZ = new Vector2(0f, 360f);

    [Tooltip("ให้พาเรนต์วัตถุที่เกิดไว้ใต้จุดเกิด")]
    public bool parentUnderPoint = false;

    [Header("Behaviour")]
    [Tooltip("เริ่มสปาวน์ทันทีตอน Start")]
    public bool spawnOnStart = true;

    // -------- NEW: Player Target Assignment --------
    [Header("Player Target Assignment")]
    [Tooltip("เปิดไว้เพื่อกำหนด Player target ให้พรีแฟบที่เพิ่งเกิด")]
    public bool assignPlayerTarget = true;

    [Tooltip("ถ้าตั้งค่านี้ไว้ จะใช้เป็นเป้าหมายโดยตรง (ไม่ค้นหา)")]
    public Transform playerTargetOverride;

    [Tooltip("ถ้าไม่กำหนด Override จะค้นหาโดย Tag นี้ (ปล่อยว่าง = ข้ามขั้นนี้)")]
    public string playerTag = "Player";

    [Tooltip("ถ้ามีผู้เล่นหลายตัว ให้เลือกตัวที่ใกล้ 'จุดเกิด' ที่ใช้จริงในครั้งนั้น")]
    public bool pickClosestPlayerIfMany = true;
    // ------------------------------------------------

    // ===== runtime =====
    int spawnedCount = 0;
    readonly List<GameObject> alive = new List<GameObject>();
    Coroutine loopCo;

    void Start()
    {
        if (spawnOnStart) Begin();
    }

    void OnValidate()
    {
        if (autoCollectFromRoot && pointsRoot != null)
            CollectPointsFromRoot();
        if (useIntervalRange)
        {
            if (intervalRange.x < 0f) intervalRange.x = 0f;
            if (intervalRange.y < intervalRange.x) intervalRange.y = intervalRange.x;
        }
        else
        {
            if (interval < 0f) interval = 0f;
        }
    }

    void CollectPointsFromRoot()
    {
        var list = new List<Transform>();
        var all = pointsRoot.GetComponentsInChildren<Transform>(includeInactiveChildren);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == pointsRoot) continue; // ข้าม root
            list.Add(all[i]);
        }
        points = list.ToArray();
    }

    // ===== public API =====
    public void Begin()
    {
        if (loopCo != null) return;
        loopCo = StartCoroutine(SpawnLoop());
    }

    public void Stop()
    {
        if (loopCo != null)
        {
            StopCoroutine(loopCo);
            loopCo = null;
        }
    }

    public GameObject SpawnOne()
    {
        // เก็บกวาดรายการที่ตายแล้ว
        alive.RemoveAll(go => go == null);

        if (!HasPrefabAndPoint()) return null;
        if (totalToSpawn >= 0 && spawnedCount >= totalToSpawn) return null;
        if (maxAlive >= 0 && alive.Count >= maxAlive) return null;

        var prefab = PickPrefab();
        var point = PickPoint();
        if (!prefab || !point) return null;

        Vector3 pos = point.position;
        if (positionJitter != Vector2.zero)
            pos += new Vector3(Random.Range(-positionJitter.x, positionJitter.x),
                               Random.Range(-positionJitter.y, positionJitter.y), 0f);

        Quaternion rot = point.rotation;
        if (randomizeRotationZ)
            rot = Quaternion.Euler(0f, 0f, Random.Range(randomRotationZ.x, randomRotationZ.y));

        Transform parent = parentUnderPoint ? point : null;
        var obj = Instantiate(prefab, pos, rot, parent);

        // ---- NEW: assign target to components on the spawned object ----
        if (assignPlayerTarget)
        {
            var playerTf = GetPlayerTransformForPoint(point);
            if (playerTf)
                AssignPlayerTargetToSpawned(obj, playerTf);
        }
        // ----------------------------------------------------------------

        alive.Add(obj);
        spawnedCount++;
        return obj;
    }

    // ===== core loop =====
    IEnumerator SpawnLoop()
    {
        if (firstDelay > 0f) yield return new WaitForSeconds(firstDelay);

        while (true)
        {
            alive.RemoveAll(go => go == null);

            // quota หมด → จบลูป
            if (totalToSpawn >= 0 && spawnedCount >= totalToSpawn)
            {
                loopCo = null;
                yield break;
            }

            // จำกัด alive
            if (maxAlive >= 0 && alive.Count >= maxAlive)
            {
                yield return null; // รอจนมีตัวตายก่อนค่อยเกิดต่อ
                continue;
            }

            // spawn
            SpawnOne();

            // รอรอบถัดไป
            float wait = useIntervalRange
                ? Random.Range(intervalRange.x, intervalRange.y)
                : interval;

            yield return new WaitForSeconds(Mathf.Max(0.01f, wait));
        }
    }

    // ===== helpers =====
    bool HasPrefabAndPoint()
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning("[SpawnManager2D] Prefabs is empty.");
            return false;
        }
        if (points == null || points.Length == 0)
        {
            Debug.LogWarning("[SpawnManager2D] Points is empty.");
            return false;
        }
        return true;
    }

    GameObject PickPrefab()
    {
        // สุ่มหนึ่งตัวจากรายการ
        int idx = Random.Range(0, prefabs.Length);
        return prefabs[idx];
    }

    Transform PickPoint()
    {
        int idx = Random.Range(0, points.Length);
        return points[idx];
    }

    // วาดจุดช่วยดูใน Scene
    void OnDrawGizmosSelected()
    {
        if (points == null) return;
        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
        foreach (var p in points)
        {
            if (!p) continue;
            Gizmos.DrawSphere(p.position, 0.12f);
            if (positionJitter != Vector2.zero)
            {
                // วาดกรอบ jitter
                var c = Gizmos.color;
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.2f);
                Gizmos.DrawCube(p.position, new Vector3(positionJitter.x * 2f, positionJitter.y * 2f, 0.01f));
                Gizmos.color = c;
            }
        }
    }

    // -------- NEW: assign player target to spawned object --------

    /// <summary>เลือก Player transform ที่เหมาะกับ "จุดเกิด" นี้</summary>
    Transform GetPlayerTransformForPoint(Transform spawnPoint)
    {
        if (playerTargetOverride) return playerTargetOverride;

        Transform candidate = null;

        // 1) หาโดย Tag
        if (!string.IsNullOrEmpty(playerTag))
        {
            var go = GameObject.FindGameObjectWithTag(playerTag);
            if (go) candidate = go.transform;
        }

        // 2) หาโดยคอมโพเนนต์ CharacterController2D (รองรับหลายตัว)
        if (!candidate)
        {
            var players = FindObjectsOfType<CharacterController2D>(includeInactive: false);
            if (players != null && players.Length > 0)
            {
                if (pickClosestPlayerIfMany && spawnPoint && players.Length > 1)
                {
                    float best = float.PositiveInfinity;
                    foreach (var p in players)
                    {
                        if (!p) continue;
                        float d = (p.transform.position - spawnPoint.position).sqrMagnitude;
                        if (d < best) { best = d; candidate = p.transform; }
                    }
                }
                else
                {
                    candidate = players[0].transform;
                }
            }
        }

        // 3) ชื่อ GameObject = "Player"
        if (!candidate)
        {
            var byName = GameObject.Find("Player");
            if (byName) candidate = byName.transform;
        }

        return candidate;
    }

    /// <summary>
    /// เซ็ต target ให้กับคอมโพเนนต์บนพรีแฟบที่รองรับ:
    /// 1) อินเทอร์เฟซ IPlayerTargetReceiver
    /// 2) ฟิลด์/พร็อพเพอร์ตี Transform ชื่อ player / target / playerTarget
    /// </summary>
    void AssignPlayerTargetToSpawned(GameObject obj, Transform playerTf)
    {
        if (!obj || !playerTf) return;

        // 1) อินเทอร์เฟซ (ดีที่สุด)
        var receivers = obj.GetComponentsInChildren<IPlayerTargetReceiver>(true);
        foreach (var r in receivers)
        {
            try { r.SetPlayerTarget(playerTf); } catch { /* ignore */ }
        }

        // 2) รีเฟลกชันเซ็ตฟิลด์/พร็อพฯ ทั่วไป
        var comps = obj.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var c in comps)
        {
            if (!c) continue;
            var t = c.GetType();

            // ฟิลด์
            TrySetTransformField(t, c, "player", playerTf);
            TrySetTransformField(t, c, "target", playerTf);
            TrySetTransformField(t, c, "playerTarget", playerTf);

            // พร็อพเพอร์ตี
            TrySetTransformProperty(t, c, "player", playerTf);
            TrySetTransformProperty(t, c, "target", playerTf);
            TrySetTransformProperty(t, c, "playerTarget", playerTf);
        }
    }

    static bool TrySetTransformField(System.Type type, object instance, string fieldName, Transform value)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var f = type.GetField(fieldName, flags);
        if (f != null && typeof(Transform).IsAssignableFrom(f.FieldType))
        {
            try { f.SetValue(instance, value); return true; } catch { }
        }
        return false;
    }

    static bool TrySetTransformProperty(System.Type type, object instance, string propName, Transform value)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var p = type.GetProperty(propName, flags);
        if (p != null && p.CanWrite && typeof(Transform).IsAssignableFrom(p.PropertyType))
        {
            try { p.SetValue(instance, value); return true; } catch { }
        }
        return false;
    }
}

/// <summary>
/// ถ้าคอมโพเนนต์ของคุณรองรับอินเทอร์เฟซนี้
/// SpawnManager2D จะเรียก SetPlayerTarget ให้ทันทีตอนเกิด
/// </summary>
public interface IPlayerTargetReceiver
{
    void SetPlayerTarget(Transform player);
}
