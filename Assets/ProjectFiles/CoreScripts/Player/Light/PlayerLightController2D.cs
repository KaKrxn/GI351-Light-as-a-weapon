using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public class PlayerLightController2D : MonoBehaviour
{
    [Header("References")]
    public Light2D playerLight;

    [Header("Radius")]
    public float minOuter = 3.0f;
    public float maxOuter = 8.0f;
    public float innerRatio = 0.5f;   // inner = outer * ratio
    public float drainPerSec = 0.2f;  // ลดรัศมีเมื่อกดโหมดสว่าง
    public float regenPerSec = 0.1f;  // ฟื้นรัศมีเมื่อไม่บูสต์

    [Header("Input (New Input System)")]
    public InputActionReference toggleLight; // Button (เปิด/ปิดไฟฉาย)
    public InputActionReference boostLight;  // Button (กดค้างเพื่อสว่างขึ้น)

    bool enabledLight = true;
    float targetOuter;

    void OnEnable()
    {
        toggleLight?.action.Enable();
        boostLight?.action.Enable();
        if (toggleLight) toggleLight.action.performed += OnToggle;
    }

    void OnDisable()
    {
        if (toggleLight) toggleLight.action.performed -= OnToggle;
        toggleLight?.action.Disable();
        boostLight?.action.Disable();
    }

    void Start()
    {
        if (!playerLight) playerLight = GetComponentInChildren<Light2D>();
        targetOuter = Mathf.Clamp(playerLight.pointLightOuterRadius, minOuter, maxOuter);
        ApplyInner();
    }

    void Update()
    {
        if (!playerLight) return;

        bool boosting = boostLight && boostLight.action.IsPressed();
        float delta = (boosting ? -drainPerSec : regenPerSec) * Time.deltaTime;
        targetOuter = Mathf.Clamp(targetOuter + delta, minOuter, maxOuter);

        // เปิด/ปิดไฟจริง
        float desiredOuter = enabledLight ? targetOuter : 0f;

        // ไล่นุ่ม ๆ
        playerLight.pointLightOuterRadius = Mathf.Lerp(playerLight.pointLightOuterRadius, desiredOuter, 10f * Time.deltaTime);
        ApplyInner();
    }

    void OnToggle(InputAction.CallbackContext _)
    {
        enabledLight = !enabledLight;
        // ดับไฟทันทีถ้าปิด
        if (!enabledLight) playerLight.pointLightOuterRadius = 0f;
    }

    void ApplyInner()
    {
        playerLight.pointLightInnerRadius = Mathf.Clamp(playerLight.pointLightOuterRadius * innerRatio, 0f, playerLight.pointLightOuterRadius - 0.05f);
    }
}
