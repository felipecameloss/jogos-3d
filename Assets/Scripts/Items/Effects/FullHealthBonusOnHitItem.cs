using UnityEngine;

/// <summary>
/// Dano extra contra inimigos com a vida cheia (Grafite de lápis).
/// Confere a vida antes deste golpe (Current + info.Amount >= Max * minHealthFraction).
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Itens/Dano extra vida cheia", fileName = "NovoItemGrafite")]
public class FullHealthBonusOnHitItem : ItemData
{
    [Tooltip("Dano extra por stack em fração do golpe original (0.75 = +75%).")]
    [SerializeField, Min(0f)] float bonusPerStack = 0.75f;
    [Tooltip("Fração mínima da vida máxima antes do golpe para contar como vida cheia.")]
    [SerializeField, Range(0.5f, 1f)] float minHealthFraction = 0.95f;

    public override void OnHit(Inventory inventory, int stacks, Health target, DamageInfo info)
    {
        if (target == null || !target.IsAlive) return;
        float hpBeforeHit = target.Current + info.Amount;
        if (hpBeforeHit < target.Max * minHealthFraction) return;

        float bonus = info.Amount * bonusPerStack * stacks;
        inventory.DealItemDamage(target, bonus, info.HitPoint);
    }
}
