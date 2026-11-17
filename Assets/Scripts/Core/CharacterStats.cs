using UnityEngine;

/// <summary>
/// Stats de combate do personagem. Os multiplicadores são para os itens
/// (a task de itens recalcula eles a partir do inventário).
/// </summary>
public class CharacterStats : MonoBehaviour
{
    [SerializeField, Min(0f)] float baseDamage = 12f;
    [Tooltip("1 = cadência base da arma. 2 = atira 2x mais rápido.")]
    [SerializeField, Min(0.01f)] float baseAttackSpeed = 1f;

    [HideInInspector] public float DamageMultiplier = 1f;
    [HideInInspector] public float AttackSpeedMultiplier = 1f;

    public float Damage => baseDamage * DamageMultiplier;
    public float AttackSpeed => Mathf.Max(0.01f, baseAttackSpeed * AttackSpeedMultiplier);
}
