using UnityEngine;
public class PlayerHealth : MonoBehaviour
{
    public float maxHP = 100f;
    public float currentHP;

    public System.Action OnDead;

    void Awake() => currentHP = maxHP;

    public void TakeDamage(float dmgPerSecond)
    {
        currentHP -= dmgPerSecond * Time.deltaTime;
        if (currentHP <= 0)
        {
            currentHP = 0;
            OnDead?.Invoke();
            
        }
    }
}

