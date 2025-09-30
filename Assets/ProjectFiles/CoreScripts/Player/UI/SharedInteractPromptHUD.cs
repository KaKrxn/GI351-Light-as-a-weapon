using UnityEngine;
#if TMP_PRESENT
using TMPro;
#endif

public class SharedInteractPromptHUD : MonoBehaviour
{
    public static SharedInteractPromptHUD Instance { get; private set; }

    [Header("UI Refs")]
    public CanvasGroup canvasGroup;
#if TMP_PRESENT
    public TMP_Text label;
#else
    public UnityEngine.UI.Text label;
#endif
    public RectTransform rect;

    [Header("Behavior")]
    public bool followWorldTarget = true;
    public Vector3 worldOffset = new Vector3(0, 1f, 0);
    public float fadeSpeed = 15f;

    object owner;
    Transform followTarget;
    Camera cam;
    float visible = 0f;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        cam = Camera.main;
        if (!canvasGroup) canvasGroup = GetComponentInChildren<CanvasGroup>();
        if (!rect) rect = (transform as RectTransform) ?? GetComponentInChildren<RectTransform>();
        SetAlpha(0f);
    }

    void LateUpdate()
    {
        if (followWorldTarget && rect && followTarget && cam)
        {
            Vector3 w = followTarget.position + worldOffset;
            rect.position = cam.WorldToScreenPoint(w);
        }

        float target = (owner != null) ? 1f : 0f;
        visible = Mathf.MoveTowards(visible, target, fadeSpeed * Time.deltaTime);
        SetAlpha(visible);
    }

    void SetAlpha(float a)
    {
        if (!canvasGroup) return;
        canvasGroup.alpha = a;
        canvasGroup.interactable = a > 0.9f;
        canvasGroup.blocksRaycasts = canvasGroup.interactable;
    }

    // === API ===
    public void Show(object requester, string text, Transform follow = null)
    {
        owner = requester;
        if (label) label.text = text;
        followTarget = follow;
    }

    public void Hide(object requester)
    {
        if (owner == requester)
        {
            owner = null;
            followTarget = null;
        }
    }
}
