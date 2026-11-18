using UnityEngine;

/// <summary>
/// Chance de dropar uma gota de cura ao matar um inimigo (Gotinha de mel).
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Itens/Cura ao matar", fileName = "NovoItemCuraAoMatar")]
public class HealOrbOnKillItem : ItemData
{
    [Tooltip("Chance por stack (0.25 = 25%). Stacks somam sem passar de 100%.")]
    [SerializeField, Range(0f, 1f)] float chancePerStack = 0.25f;
    [Tooltip("Cura fixa no 1º stack.")]
    [SerializeField, Min(1f)] float baseHeal = 16f;
    [Tooltip("Cura extra por stack a mais.")]
    [SerializeField, Min(0f)] float healPerExtraStack = 10f;
    [SerializeField, Min(1f)] float attractRadius = 5f;
    [SerializeField, Min(1f)] float orbLifetime = 20f;
    [SerializeField] Material orbMaterial;
    [SerializeField] Color orbColor = new(1f, 0.78f, 0.15f, 1f);

    public override void OnKill(Inventory inventory, int stacks, EnemyBase enemy, DamageInfo killingBlow)
    {
        if (enemy == null || !Inventory.Roll(chancePerStack, stacks)) return;
        float heal = baseHeal + healPerExtraStack * (stacks - 1);
        HealingOrb.Spawn(enemy.Center, heal, orbColor, orbMaterial, attractRadius, orbLifetime);
    }
}
