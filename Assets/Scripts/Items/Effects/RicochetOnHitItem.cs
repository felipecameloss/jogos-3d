using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chance de o acerto ricochetear em inimigos próximos, um depois do outro (Clipe de papel).
/// O fio elétrico (ChainLightningOnHitItem) pula do mesmo jeito, mas com dano do player e acertando o alvo também.
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Itens/Ricochete ao acertar", fileName = "NovoItemRicochete")]
public class RicochetOnHitItem : ItemData
{
    [SerializeField, Range(0f, 1f)] float chancePerStack = 0.25f;
    [Tooltip("Quantos inimigos o ricochete atinge (+1 a cada stack extra).")]
    [SerializeField, Min(1)] int bounces = 2;
    [Tooltip("Dano de cada ricochete em relação ao acerto original (0.6 = 60%).")]
    [SerializeField, Min(0f)] float damageFraction = 0.6f;
    [SerializeField, Min(1f)] float range = 12f;
    [SerializeField] Color lineColor = new(0.85f, 0.88f, 0.95f, 0.9f);

    readonly List<EnemyBase> hit = new();

    public override void OnHit(Inventory inventory, int stacks, Health target, DamageInfo info)
    {
        if (!Inventory.Roll(chancePerStack, stacks)) return;
        hit.Clear();
        if (target.TryGetComponent(out EnemyBase first)) hit.Add(first);

        Vector3 from = info.HitPoint;
        float damage = info.Amount * damageFraction;
        int total = bounces + (stacks - 1);
        for (int i = 0; i < total; i++)
        {
            var next = EnemyBase.Nearest(from, range, hit);
            if (next == null) break;
            hit.Add(next);
            Vector3 to = next.Center;
            ItemVfx.Line(from, to, lineColor, 0.07f, 0.15f);
            inventory.DealItemDamage(next.Health, damage, to);
            from = to;
        }
    }
}
