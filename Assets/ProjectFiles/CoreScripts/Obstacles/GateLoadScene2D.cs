using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class GateLoadScene2D : MonoBehaviour
{
    public enum SceneRefMode { ByName, ByBuildIndex }

    [Header("Interact")]
    public string[] interactTags = new[] { "Player" };
    public KeyCode interactKey = KeyCode.E;
    public bool autoTriggerOnEnter = false;
    public GameObject promptUI;

    [Header("Scene")]
    public SceneRefMode sceneRefMode = SceneRefMode.ByName;
    public string sceneName;
    public int sceneBuildIndex = -1;
    public LoadSceneMode loadMode = LoadSceneMode.Single;

    [Header("SFX / Delay (optional)")]
    public AudioSource sfxSource;
    public AudioClip interactSfx;
    
    public float loadDelay = 0f;

    // runtime
    bool playerInRange;
    GameObject currentPlayer;
    bool isLoading;

    void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true;
    }

    void OnEnable()
    {
        if (promptUI) promptUI.SetActive(false);
        isLoading = false;
        playerInRange = false;
        currentPlayer = null;
    }

    void Update()
    {
        if (isLoading) return;
        if (!playerInRange) return;

        if (!autoTriggerOnEnter && Input.GetKeyDown(interactKey))
            StartLoad();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsAllowedTag(other.tag)) return;

        playerInRange = true;
        currentPlayer = other.gameObject;

        if (promptUI && !autoTriggerOnEnter) promptUI.SetActive(true);

        if (autoTriggerOnEnter)
            StartLoad();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject == currentPlayer)
        {
            playerInRange = false;
            currentPlayer = null;
            if (promptUI) promptUI.SetActive(false);
        }
    }

    bool IsAllowedTag(string tagStr)
    {
        if (interactTags == null || interactTags.Length == 0) return true;
        foreach (var t in interactTags)
            if (!string.IsNullOrEmpty(t) && tagStr == t) return true;
        return false;
    }

    void StartLoad()
    {
        if (isLoading) return;
        if (!ValidateSceneRef())
        {
            Debug.LogError("[GateLoadScene2D] Scene reference invalid. ตรวจชื่อซีนหรือ Build Index และเพิ่มซีนใน Build Settings.");
            return;
        }

        isLoading = true;

        if (promptUI) promptUI.SetActive(false);
        if (sfxSource && interactSfx) sfxSource.PlayOneShot(interactSfx);

        
        var col = GetComponent<Collider2D>();
        if (col) col.enabled = false;

        StartCoroutine(Co_LoadScene());
    }

    IEnumerator Co_LoadScene()
    {
        if (loadDelay > 0f) yield return new WaitForSeconds(loadDelay);

        switch (sceneRefMode)
        {
            case SceneRefMode.ByName:
                SceneManager.LoadScene(sceneName, loadMode);
                break;
            case SceneRefMode.ByBuildIndex:
                SceneManager.LoadScene(sceneBuildIndex, loadMode);
                break;
        }
    }

    bool ValidateSceneRef()
    {
        if (sceneRefMode == SceneRefMode.ByName)
            return !string.IsNullOrEmpty(sceneName);
        else
            return sceneBuildIndex >= 0;
    }

    
    void OnDisable()
    {
        if (promptUI) promptUI.SetActive(false);
    }

#if UNITY_EDITOR
    // ให้เห็นใน Scene view ว่าประตูนี้โหลดซีนอะไร
    void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        string label = sceneRefMode == SceneRefMode.ByName ? $"Load: {sceneName}" : $"Load Index: {sceneBuildIndex}";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, label);
    }
#endif
}
