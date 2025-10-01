using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT
using TMPro;
#endif

[AddComponentMenu("Game/UI/Player Life Slider UI")]
public class PlayerLifeSliderUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("UI Slider ที่จะแสดงพลังชีวิต (ไม่ใส่จะหาในลูกของ GameObject นี้)")]
    public Slider slider;

    [Tooltip("อ็อบเจ็กต์ผู้เล่นที่มี CharacterController2D (ไม่ใส่จะค้นหาอัตโนมัติ)")]
    public CharacterController2D player;

    [Header("Auto Find Player")]
    public bool autoFindPlayer = true;
    public string playerTag = "Player"; // จะลอง tag นี้ก่อน แล้วค่อยลองชื่อ "Player"

    [Header("Behaviour")]
    [Tooltip("ปรับค่าบาร์แบบนุ่ม (ค่าเยอะ = นุ่ม)")]
    public float smooth = 12f;

    [Tooltip("ถ้าผู้เล่นฮีลเกินค่าสูงสุดที่จับไว้ ให้ขยาย max ตามจริง")]
    public bool expandMaxIfHealed = true;

#if TMP_PRESENT
    [Header("Text (optional)")]
    [Tooltip("TMP_Text สำหรับแสดงตัวเลข x/y")]
    public TMP_Text label;
#endif

    // runtime
    float maxLife = 1f;
    float display01 = 1f;
    bool initialized = false;

    void Awake()
    {
        if (!slider) slider = GetComponentInChildren<Slider>();
        if (slider)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
        }
    }

    void Start()
    {
        if (!player && autoFindPlayer)
        {
            var go = !string.IsNullOrEmpty(playerTag)
                        ? GameObject.FindGameObjectWithTag(playerTag)
                        : null;
            if (!go) go = GameObject.Find("Player");
            if (go) player = go.GetComponent<CharacterController2D>();
        }

        if (player)
        {
            maxLife = Mathf.Max(1f, player.life); // จับค่าสูงสุดจากค่าตอนเริ่ม
            initialized = true;
        }
    }

    void Update()
    {
        if (!slider || !player) return;

        if (!initialized)
        {
            maxLife = Mathf.Max(1f, player.life);
            initialized = true;
        }

        if (expandMaxIfHealed && player.life > maxLife)
            maxLife = player.life;

        float target01 = Mathf.Clamp01(player.life / Mathf.Max(1f, maxLife));
        display01 = Mathf.Lerp(display01, target01, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        slider.value = display01;

#if TMP_PRESENT
        if (label) label.text = $"{Mathf.CeilToInt(player.life)}/{Mathf.CeilToInt(maxLife)}";
#endif
    }
}
