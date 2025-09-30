using UnityEngine;

public interface ILightDamageable
{
    /// <summary>
    /// โดนลำแสงจากปืน (damage ต่อวินาที * Time.deltaTime)
    /// </summary>
    void ApplyLightDamage(float amount, Vector2 hitPoint);
}

