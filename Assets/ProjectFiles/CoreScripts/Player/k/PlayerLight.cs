using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PlayerLight : MonoBehaviour
{
    [Header("Link Light2D (Point)")]
    public Light2D candleLight;

    [Header("Light Radius")]
    [Tooltip("รัศมีความปลอดภัยจากความมืด")]
    public float safeRadius = 5f;

    [Header("Flicker")]
    public bool flicker = true;
    public float flickerAmp = 0.25f;
    public float flickerSpeed = 4f;

    public float CurrentRadius { get; private set; }

    void Awake()
    {
        if (!candleLight) candleLight = GetComponent<Light2D>();
        CurrentRadius = safeRadius;
        ApplyToLight();
    }

    void Update()
    {
        if (flicker)
        {
            float a = Mathf.Sin(Time.time * flickerSpeed) * flickerAmp;
            CurrentRadius = Mathf.Max(0.1f, safeRadius + a);
            ApplyToLight();
        }
    }

    void ApplyToLight()
    {
        if (!candleLight) return;
        candleLight.pointLightOuterRadius = CurrentRadius;
        candleLight.pointLightInnerRadius = Mathf.Max(0, CurrentRadius - 1.25f);
    }
}
