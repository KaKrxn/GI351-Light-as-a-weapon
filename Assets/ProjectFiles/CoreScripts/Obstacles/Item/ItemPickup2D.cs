using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class ItemPickup2D : MonoBehaviour
{
    public enum PickupMode { Auto, InteractKey }

    [Header("Item")]
    public string itemId = "Handle";
    [Min(1)] public int amount = 1;

    [Header("Pickup")]
    public PickupMode mode = PickupMode.InteractKey;
    public KeyCode interactKey = KeyCode.E;
    public string[] allowedTags = new[] { "Player" };
    public bool destroyOnPickup = true;

    [Header("FX (optional)")]
    public GameObject pickupVfxPrefab;
    public AudioClip pickupSfx;
    [Range(0f, 1f)] public float sfxVolume = 0.85f;
    public GameObject promptUI;

    [Header("Events")]
    public UnityEvent onPicked;

    bool playerInRange;
    GameObject currentPlayer;

    void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true;
    }

    void OnEnable() { if (promptUI) promptUI.SetActive(false); }

    void Update()
    {
        if (mode == PickupMode.InteractKey && playerInRange && Input.GetKeyDown(interactKey))
            TryPickup(currentPlayer);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsAllowedTag(other.tag)) return;
        playerInRange = true;
        currentPlayer = other.gameObject;

        if (mode == PickupMode.Auto) TryPickup(currentPlayer);
        else if (promptUI) promptUI.SetActive(true);
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
        if (allowedTags == null || allowedTags.Length == 0) return true;
        foreach (var t in allowedTags) if (!string.IsNullOrEmpty(t) && tagStr == t) return true;
        return false;
    }

    void TryPickup(GameObject playerGO)
    {
        if (!playerGO) return;
        var inv = playerGO.GetComponentInParent<InventoryLite>();
        if (!inv)
        {
            Debug.LogWarning($"[ItemPickup2D] No InventoryLite found on {playerGO.name} or its parents.");
            return;
        }

        if (string.IsNullOrEmpty(itemId)) { Debug.LogWarning("[ItemPickup2D] itemId is empty."); return; }

        inv.AddItem(itemId, Mathf.Max(1, amount));

        if (pickupVfxPrefab) Instantiate(pickupVfxPrefab, transform.position, Quaternion.identity);
        if (pickupSfx) AudioSource.PlayClipAtPoint(pickupSfx, transform.position, sfxVolume);
        onPicked?.Invoke();
        if (promptUI) promptUI.SetActive(false);

        if (destroyOnPickup) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
