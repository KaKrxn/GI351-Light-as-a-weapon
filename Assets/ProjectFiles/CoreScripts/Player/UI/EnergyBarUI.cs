using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT
using TMPro;
#endif

[DisallowMultipleComponent]
public class EnergyBarUI : MonoBehaviour
{
    [Header("Data")]
    public InventoryLite inventory;          // ลาก InventoryLite ของ Player มา
    public string itemId = "Energy";         // ไอเท็มที่จะนับเป็นเกจ
    [Min(1)] public int targetAmount = 5;    // เก็บครบเท่านี้ = 100%

    [Header("UI")]
    public Slider slider;                    // ถ้าไม่ใส่ จะพยายามหาในลูก
#if TMP_PRESENT
    public TMP_Text label;                   // (ออปชัน) โชว์ตัวเลข x/y
#endif
    [Tooltip("ความนุ่มนวลของเกจ (ยิ่งมากยิ่งนุ่ม)")]
    public float smooth = 10f;

    [Header("Optional: auto link from skill")]
    public FlashRevealSkill2D skill;         // ถ้าใส่ จะดึง energyItemId/requiredAmount อัตโนมัติ

    float displayValue = 0f;

    void Awake()
    {
        if (!inventory) inventory = FindObjectOfType<InventoryLite>();
        if (!slider) slider = GetComponentInChildren<Slider>();

        if (skill)
        {
            if (!string.IsNullOrEmpty(skill.energyItemId)) itemId = skill.energyItemId;
            if (skill.requiredAmount > 0) targetAmount = skill.requiredAmount;
        }

        if (slider)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
        }
    }

    void Update()
    {
        int have = inventory ? inventory.GetCount(itemId) : 0;
        float target = targetAmount > 0 ? Mathf.Clamp01(have / (float)targetAmount) : 0f;

        // smooth ใส้ไหล
        displayValue = Mathf.Lerp(displayValue, target, 1f - Mathf.Exp(-smooth * Time.deltaTime));

        if (slider) slider.value = displayValue;
#if TMP_PRESENT
        if (label)  label.text   = $"{Mathf.Min(have, targetAmount)}/{targetAmount}";
#endif
    }
}