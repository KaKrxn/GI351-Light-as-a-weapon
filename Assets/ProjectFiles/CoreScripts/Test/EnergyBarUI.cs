using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class EnergyBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerEnergy energy;   // ลาก PlayerEnergy มาวาง (ถ้าไม่ใส่จะพยายามหาให้)
    [SerializeField] private Slider slider;         // ลาก Slider มาวาง (ถ้าไม่ใส่จะดึงจากตัวเอง)

    [Header("Visual (optional)")]
    [SerializeField] private Image fillImage;       // ใส่ Image ของ Fill Area/Fill
    [SerializeField] private Gradient fillGradient; // ใส่ Gradient เพื่อไล่สีตามเปอร์เซ็นต์
    [SerializeField] private Text valueText;        // (ออปชัน) Text แสดงตัวเลข "cur/max"

    [Header("Smooth Fill")]
    [SerializeField] private bool smoothFill = true;
    [SerializeField] private float smoothSpeed = 8f; // ยิ่งมากยิ่งไว

    private Coroutine tweenCo;

    void Reset()
    {
        slider = GetComponent<Slider>();
    }

    void Awake()
    {
        if (!slider) slider = GetComponent<Slider>();
        if (!energy) energy = FindObjectOfType<PlayerEnergy>();

        if (slider)
        {
            slider.minValue = 0;
            slider.wholeNumbers = true;
        }
    }

    void OnEnable()
    {
        // สมัคร event
        if (energy)
        {
            energy.onEnergyChanged.AddListener(OnEnergyChanged);
            // init ครั้งแรก
            OnEnergyChanged(energy.Current, energy.maxEnergy);
        }
    }

    void OnDisable()
    {
        if (energy) energy.onEnergyChanged.RemoveListener(OnEnergyChanged);
        if (tweenCo != null) StopCoroutine(tweenCo);
    }

    private void OnEnergyChanged(int current, int max)
    {
        if (!slider) return;

        slider.maxValue = max;

        if (smoothFill)
        {
            if (tweenCo != null) StopCoroutine(tweenCo);
            tweenCo = StartCoroutine(SmoothSet(slider.value, current));
        }
        else
        {
            slider.value = current;
            UpdateColorAndText(current, max);
        }
    }

    IEnumerator SmoothSet(float from, float to)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * smoothSpeed;
            float v = Mathf.Lerp(from, to, t);
            slider.value = v;
            UpdateColorAndText((int)v, (int)slider.maxValue);
            yield return null;
        }
        slider.value = to;
        UpdateColorAndText((int)to, (int)slider.maxValue);
        tweenCo = null;
    }

    private void UpdateColorAndText(int current, int max)
    {
        if (fillImage && fillGradient != null)
        {
            float p = (max <= 0) ? 0f : Mathf.Clamp01((float)current / max);
            fillImage.color = fillGradient.Evaluate(p);
        }
        if (valueText)
        {
            valueText.text = $"{current}/{max}";
        }
    }
}
