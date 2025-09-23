using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PlayerEnergy : MonoBehaviour
{
    [Header("Energy")]
    public int maxEnergy = 100;
    [Tooltip("พลังเริ่มต้นตอนเกิด")]
    public int startEnergy = 0;

    [Header("Events")]
    public UnityEvent<int, int> onEnergyChanged; // current, max
    public UnityEvent onEnergyFull;
    public UnityEvent onEnergyEmptied;

    public int Current { get; private set; }
    public bool IsFull => Current >= maxEnergy;

    void Awake()
    {
        Current = Mathf.Clamp(startEnergy, 0, maxEnergy);
        onEnergyChanged?.Invoke(Current, maxEnergy);
        if (IsFull) onEnergyFull?.Invoke();
    }

    public void AddEnergy(int amount)
    {
        if (amount <= 0) return;
        int before = Current;
        Current = Mathf.Clamp(Current + amount, 0, maxEnergy);
        onEnergyChanged?.Invoke(Current, maxEnergy);
        if (!IsFull && Current >= maxEnergy) onEnergyFull?.Invoke();
    }

    public bool TryConsumeAll()   // ใช้ตอนกดสกิล
    {
        if (!IsFull) return false;
        Current = 0;
        onEnergyChanged?.Invoke(Current, maxEnergy);
        onEnergyEmptied?.Invoke();
        return true;
    }

    public float Percent01() => maxEnergy <= 0 ? 0f : (float)Current / maxEnergy;
}
