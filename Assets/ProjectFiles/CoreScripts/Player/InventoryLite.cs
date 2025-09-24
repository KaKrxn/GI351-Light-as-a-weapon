using System.Collections.Generic;
using UnityEngine;

public class InventoryLite : MonoBehaviour
{
    [SerializeField] private Dictionary<string, int> counts = new Dictionary<string, int>();

    // เพิ่มจำนวนไอเท็ม (ดีฟอลต์ 1)
    public void AddItem(string id, int amount = 1)
    {
        if (string.IsNullOrEmpty(id) || amount <= 0) return;
        if (!counts.ContainsKey(id)) counts[id] = 0;
        counts[id] += amount;
    }

    // มีไอเท็มอย่างน้อย amount ชิ้นไหม (ดีฟอลต์ 1)
    public bool HasItem(string id, int amount = 1)
    {
        if (string.IsNullOrEmpty(id) || amount <= 0) return false;
        return counts.TryGetValue(id, out var c) && c >= amount;
    }

    // ใช้/ลบไอเท็ม amount ชิ้น (ดีฟอลต์ 1) — สำเร็จ/ล้มเหลว
    public bool Consume(string id, int amount = 1)
    {
        if (!HasItem(id, amount)) return false;
        counts[id] -= amount;
        if (counts[id] <= 0) counts.Remove(id);
        return true;
    }

    public int GetCount(string id) => counts.TryGetValue(id, out var c) ? c : 0;
}
