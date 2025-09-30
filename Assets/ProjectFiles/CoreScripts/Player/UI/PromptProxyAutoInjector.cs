#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class PromptProxyAutoInjector : EditorWindow
{
    [MenuItem("Tools/Prompt/Auto Inject Proxy (assign to all promptUI)")]
    public static void InjectAll()
    {
        int count = 0;
        var allBehaviours = GameObject.FindObjectsOfType<MonoBehaviour>(true);

        foreach (var mb in allBehaviours)
        {
            if (!mb) continue;
            var so = new SerializedObject(mb);
            var sp = so.FindProperty("promptUI");
            if (sp == null || sp.propertyType != SerializedPropertyType.ObjectReference)
                continue;

            if (sp.objectReferenceValue != null) continue; // ข้ามถ้าตั้งไว้แล้ว

            // สร้างลูก __PromptProxy
            var parent = mb.gameObject.transform;
            var proxyGo = new GameObject("__PromptProxy");
            proxyGo.transform.SetParent(parent, false);
            proxyGo.transform.localPosition = Vector3.zero;

            var proxy = proxyGo.AddComponent<PromptUIProxy>();
            proxy.message = "[E] Interact";
            proxy.followTarget = parent;

            // เซ็ตลงฟิลด์ promptUI
            sp.objectReferenceValue = proxyGo;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ซ่อน proxy ตอนเริ่ม (ปล่อยให้สคริปต์เดิมคุมการ SetActive)
            proxyGo.SetActive(false);

            count++;
        }

        EditorUtility.DisplayDialog("Prompt Proxy Injector", $"สร้าง & ผูก Proxy สำเร็จ: {count} ตัว", "OK");
    }
}
#endif
