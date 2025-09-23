using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal; // Light2D

[DisallowMultipleComponent]
public class FlashRevealSkill2D : MonoBehaviour
{
    [Header("Input")]
    public KeyCode activateKey = KeyCode.Q;

    [Header("Refs")]
    public PlayerEnergy energy;

    [SerializeField] private UnityEngine.Object playerLight2DObj; // Light2D ของผู้เล่น (optional)
    [SerializeField] private UnityEngine.Object vcamObj;          // ใส่ GO/Component ก็ได้ (CM3/CM2)
    [SerializeField] private UnityEngine.Object globalLight2DObj; // Global Light 2D (optional)

    private Light2D playerLight2D;
    private Light2D globalLight2D;

    // ---- Cinemachine lens access (ทำงานได้ทั้ง CM3/CM2) ----
    private Func<float> cmGetLensValue; // VerticalFOV / FieldOfView / OrthographicSize
    private Action<float> cmSetLensValue;
    private string lensValueName = "?";  // ไว้ debug ว่าใช้พร็อพตัวไหน

    [Header("Light Expand")]
    public float normalOuterRadius = 5f;
    public float flashOuterRadius = 18f;
    public float radiusRampUp = 0.12f;
    public float radiusHold = 1.0f;
    public float radiusRampDown = 0.25f;

    [Header("Global Flash")]
    public bool enableGlobalFlash = true;
    public float globalFlashIntensity = 1.2f;
    public float globalFlashFadeIn = 0.06f;
    public float globalFlashHold = 0.12f;
    public float globalFlashFadeOut = 0.18f;
    public Color globalFlashColor = Color.white;

    [Header("Camera Zoom (edits Lens)")]
    [Tooltip("ค่าปกติของเลนส์ (VerticalFOV / FieldOfView / OrthographicSize)")]
    public float normalLensValue = 100f;
    [Tooltip("ค่าตอนซูมออกของเลนส์")]
    public float zoomOutLensValue = 120f;
    public float zoomRampUp = 0.12f;
    public float zoomHold = 1.0f;
    public float zoomRampDown = 0.25f;
    public bool doZoomOut = true;

    [Header("FX (optional)")]
    public AudioSource sfxSource;
    public AudioClip activateSfx;

    bool isRunning;

    void Awake()
    {
        if (!energy) energy = GetComponent<PlayerEnergy>();

        playerLight2D = playerLight2DObj as Light2D;
        globalLight2D = globalLight2DObj as Light2D;
        if (!globalLight2D)
            globalLight2D = FindObjectsOfType<Light2D>(true).FirstOrDefault(l => l && l.lightType == Light2D.LightType.Global);

        if (playerLight2D && Mathf.Approximately(normalOuterRadius, 0f))
            normalOuterRadius = playerLight2D.pointLightOuterRadius;

        SetupCinemachineLensAccess();

        if (cmGetLensValue != null && Mathf.Approximately(normalLensValue, 0f))
            normalLensValue = cmGetLensValue();
    }

    void Update()
    {
        if (Input.GetKeyDown(activateKey)) TryActivate();
    }

    public void TryActivate()
    {
        if (isRunning || !energy || !energy.IsFull) return;

        if (energy.TryConsumeAll())
        {
            StartCoroutine(Co_Run());
            if (sfxSource && activateSfx) sfxSource.PlayOneShot(activateSfx);
        }
    }

    IEnumerator Co_Run()
    {
        isRunning = true;

        Coroutine lightCo = null, camCo = null, globalCo = null;

        if (playerLight2D)
            lightCo = StartCoroutine(Co_AnimateLightRadius(playerLight2D, normalOuterRadius, flashOuterRadius, radiusRampUp, radiusHold, radiusRampDown));

        if (enableGlobalFlash)
            globalCo = StartCoroutine(Co_GlobalFlash_Safe(globalFlashIntensity, globalFlashFadeIn, globalFlashHold, globalFlashFadeOut, globalFlashColor));

        if (doZoomOut && cmSetLensValue != null)
            camCo = StartCoroutine(Co_AnimateZoom(normalLensValue, zoomOutLensValue, zoomRampUp, zoomHold, zoomRampDown));

        if (lightCo != null) yield return lightCo;
        if (camCo != null) yield return camCo;
        if (globalCo != null) yield return globalCo;

        isRunning = false;
    }

    // ----------------- LIGHT -----------------
    IEnumerator Co_AnimateLightRadius(Light2D light, float from, float to, float tUp, float tHold, float tDown)
    {
        float start = (from <= 0f) ? light.pointLightOuterRadius : from;
        float end = to;

        float t = 0f;
        while (tUp > 0f && t < 1f) { t += Time.deltaTime / tUp; light.pointLightOuterRadius = Mathf.Lerp(start, end, t); yield return null; }
        light.pointLightOuterRadius = end;
        if (tHold > 0f) yield return new WaitForSeconds(tHold);
        t = 0f;
        while (tDown > 0f && t < 1f) { t += Time.deltaTime / tDown; light.pointLightOuterRadius = Mathf.Lerp(end, start, t); yield return null; }
        light.pointLightOuterRadius = start;
    }

    IEnumerator Co_GlobalFlash_Safe(float targetIntensity, float fadeIn, float hold, float fadeOut, Color color)
    {
        Light2D light = globalLight2D;
        bool createdTemp = false;

        if (!light)
        {
            var go = new GameObject("GlobalFlash2D (temp)");
            light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            createdTemp = true;
        }

        float origIntensity = light.intensity;
        Color origColor = light.color;
        light.color = color;

        float t = 0f;
        while (fadeIn > 0f && t < 1f) { t += Time.deltaTime / fadeIn; light.intensity = Mathf.Lerp(origIntensity, targetIntensity, t); yield return null; }
        light.intensity = targetIntensity;
        if (hold > 0f) yield return new WaitForSeconds(hold);
        t = 0f;
        while (fadeOut > 0f && t < 1f) { t += Time.deltaTime / fadeOut; light.intensity = Mathf.Lerp(targetIntensity, origIntensity, t); yield return null; }
        light.intensity = origIntensity;
        light.color = origColor;

        if (createdTemp) Destroy(light.gameObject);
    }

    // ----------------- CINEMACHINE (CM3/CM2) -----------------
    void SetupCinemachineLensAccess()
    {
        // 1) แปลงอินพุตให้เป็น "คอมโพเนนต์กล้อง" เสมอ
        var camComp = ResolveCinemachineComponent(vcamObj);
        if (camComp == null)
        {
            // หาในซีนอัตโนมัติ
            camComp = ResolveCinemachineComponent(
                FindFirstTypeInstance("Cinemachine.CinemachineCamera") ??
                FindFirstTypeInstance("Cinemachine.CinemachineVirtualCamera")
            );
        }
        if (camComp == null) { Debug.LogWarning("[FlashRevealSkill2D] No Cinemachine camera found."); return; }
        vcamObj = camComp; // เก็บตัวที่ถูกต้องกลับไว้

        // 2) ดึง "Lens" (CM3: property Lens, CM2: field m_Lens, หรือสมาชิกชนิด LensSettings)
        var t = vcamObj.GetType();
        Func<object> getLens = null;
        Action<object> setLens = null;

        var lensField = t.GetField("m_Lens", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var lensProp = t.GetProperty("Lens", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (lensField != null) { getLens = () => lensField.GetValue(vcamObj); setLens = v => lensField.SetValue(vcamObj, v); }
        else if (lensProp != null) { getLens = () => lensProp.GetValue(vcamObj, null); setLens = v => lensProp.SetValue(vcamObj, v, null); }
        else
        {
            // หา member ใด ๆ ที่ type ชื่อมี "LensSettings"
            var anyLensMember = t
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m =>
                {
                    var mt = GetMemberType(m);
                    return mt != null && mt.Name.Contains("LensSettings");
                });

            if (anyLensMember != null)
            {
                getLens = () => GetMemberValue(anyLensMember, vcamObj);
                setLens = v => SetMemberValue(anyLensMember, vcamObj, v);
            }
        }

        if (getLens == null || setLens == null)
        {
            Debug.LogWarning("[FlashRevealSkill2D] Lens not found on Cinemachine component.");
            return;
        }

        // 3) เลือกพร็อพของเลนส์ที่เป็นค่าซูม: VerticalFOV / FieldOfView / OrthographicSize
        var lens = getLens();
        var lt = lens.GetType();

        var fovVertProp = lt.GetProperty("VerticalFOV");
        var fovProp = lt.GetProperty("FieldOfView");
        var orthoProp = lt.GetProperty("OrthographicSize");

        PropertyInfo chosen = fovVertProp ?? fovProp ?? orthoProp;
        if (chosen == null)
        {
            Debug.LogWarning("[FlashRevealSkill2D] Lens has no VerticalFOV/FieldOfView/OrthographicSize.");
            return;
        }

        lensValueName = chosen.Name;

        cmGetLensValue = () => Convert.ToSingle(chosen.GetValue(getLens()));
        cmSetLensValue = (v) => { var l = getLens(); chosen.SetValue(l, v); setLens(l); };
        // Debug.Log($"[FlashRevealSkill2D] Using lens value: {lensValueName}");
    }

    static Type GetMemberType(MemberInfo m)
    {
        if (m is FieldInfo fi) return fi.FieldType;
        if (m is PropertyInfo pi) return pi.PropertyType;
        return null;
    }
    static object GetMemberValue(MemberInfo m, object target)
    {
        if (m is FieldInfo fi) return fi.GetValue(target);
        if (m is PropertyInfo pi) return pi.GetValue(target, null);
        return null;
    }
    static void SetMemberValue(MemberInfo m, object target, object val)
    {
        if (m is FieldInfo fi) fi.SetValue(target, val);
        else if (m is PropertyInfo pi) pi.SetValue(target, val, null);
    }

    // รับอะไรมาก็พยายามคืน "คอมโพเนนต์กล้อง" ของ Cinemachine
    UnityEngine.Object ResolveCinemachineComponent(UnityEngine.Object obj)
    {
        if (!obj) return null;

        // ถ้าเป็น GameObject/Transform → ดูคอมโพเนนต์ข้างใน
        var go = (obj as GameObject) ?? (obj as Component ? (obj as Component).gameObject : null);
        if (go)
        {
            var cm3 = go.GetComponent("Cinemachine.CinemachineCamera");
            if (cm3) return cm3 as Component;
            var cm2 = go.GetComponent("Cinemachine.CinemachineVirtualCamera");
            if (cm2) return cm2 as Component;
        }

        // ถ้าเป็นคอมโพเนนต์อยู่แล้วก็โอเค
        var type = obj.GetType();
        if (type.FullName == "Cinemachine.CinemachineCamera" ||
            type.FullName == "Cinemachine.CinemachineVirtualCamera")
            return obj;

        return null;
    }

    UnityEngine.Object FindFirstTypeInstance(string fullTypeName)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullTypeName))
            .FirstOrDefault(t => t != null);
        if (type == null) return null;

        var all = Resources.FindObjectsOfTypeAll(type);
        foreach (var o in all)
        {
            var go = (o as Component)?.gameObject ?? (o as GameObject);
            if (!go) continue;
            if (go.hideFlags == HideFlags.None) return o;
        }
        return null;
    }

    IEnumerator Co_AnimateZoom(float from, float to, float tUp, float tHold, float tDown)
    {
        if (cmGetLensValue == null || cmSetLensValue == null) yield break;

        float start = (from <= 0f) ? cmGetLensValue() : from;
        float end = to;

        float t = 0f;
        while (tUp > 0f && t < 1f)
        {
            t += Time.deltaTime / tUp;
            cmSetLensValue(Mathf.Lerp(start, end, t));
            yield return null;
        }
        cmSetLensValue(end);

        if (tHold > 0f) yield return new WaitForSeconds(tHold);

        t = 0f;
        while (tDown > 0f && t < 1f)
        {
            t += Time.deltaTime / tDown;
            cmSetLensValue(Mathf.Lerp(end, start, t));
            yield return null;
        }
        cmSetLensValue(start);
    }
}
