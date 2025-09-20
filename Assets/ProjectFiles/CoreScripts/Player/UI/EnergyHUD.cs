using UnityEngine;
using UnityEngine.UI;

public class EnergyHUD : MonoBehaviour
{
    public LightGun laser;
    public Slider slider;

    void Start()
    {
        if (slider) slider.minValue = 0f;
        if (slider) slider.maxValue = 1f;
    }

    void Update()
    {
        if (laser && slider)
        {
            slider.value = laser.Energy01;
        }
    }
}
