using UnityEngine;
using System.Collections;

public class EnemySpawnManager : MonoBehaviour
{
    [Header("Spawn")]
    public GhostSpirit ghostPrefab;
    public Transform[] spawnPoints;
    public float spawnInterval = 30f;    // สร้างทุก ๆ 30 วิ
    public bool spawnOnStart = true;
    public int prewarmCount = 0;         // ถ้าอยากให้เกิดก่อนเริ่มวน

    [Header("Target")]
    public Transform player;             // ส่งต่อให้ผีที่เกิดใหม่

    void Start()
    {
        if (spawnOnStart)
        {
            for (int i = 0; i < prewarmCount; i++) SpawnOne();
            StartCoroutine(SpawnLoop());
        }
    }

    IEnumerator SpawnLoop()
    {
        var wait = new WaitForSeconds(spawnInterval);
        while (true)
        {
            SpawnOne();
            yield return wait;
        }
    }

    void SpawnOne()
    {
        if (!ghostPrefab || spawnPoints == null || spawnPoints.Length == 0) return;
        Transform p = spawnPoints[Random.Range(0, spawnPoints.Length)];
        var g = Instantiate(ghostPrefab, p.position, p.rotation);
        if (player) g.player = player;
    }
}

