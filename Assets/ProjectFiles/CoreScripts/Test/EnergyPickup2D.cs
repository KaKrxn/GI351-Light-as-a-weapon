using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnergyPickup2D : MonoBehaviour
{
    [Header("Energy")]
    public int energyValue = 10;

    [Header("Who can pick up?")]
    [Tooltip("ปล่อยว่าง = อนุญาตทุกแท็ก")]
    public string[] allowedTags = new[] { "Player" };

    [Header("FX (optional)")]
    public GameObject pickupVfxPrefab;
    public AudioClip pickupSfx;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    void Reset()
    {
        var c = GetComponent<Collider2D>();
        if (c) c.isTrigger = true;
    }

    bool IsAllowedTag(Collider2D other)
    {
        if (allowedTags == null || allowedTags.Length == 0) return true;
        foreach (var t in allowedTags)
        {
            if (!string.IsNullOrEmpty(t) && other.CompareTag(t)) return true;
        }
        return false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsAllowedTag(other)) return;

        var energy = other.GetComponentInParent<PlayerEnergy>();
        if (!energy) energy = other.GetComponent<PlayerEnergy>();
        if (!energy) return;

        energy.AddEnergy(energyValue);

        if (pickupVfxPrefab) Instantiate(pickupVfxPrefab, transform.position, Quaternion.identity);
        if (pickupSfx) AudioSource.PlayClipAtPoint(pickupSfx, transform.position, sfxVolume);

        Destroy(gameObject);
    }
}
