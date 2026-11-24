using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chance de o acerto soltar um raio que eletrocuta o alvo e pula para os inimigos mais
/// próximos, um depois do outro (Fio elétrico desencapado). Parecido com o ricochete do
/// clipe, mas o dano vem do dano do player (não do acerto) e o raio acerta o alvo também.
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Itens/Raio em cadeia ao acertar", fileName = "NovoItemRaio")]
public class ChainLightningOnHitItem : ItemData
{
    [Tooltip("Chance por stack (0.15 = 15%). Stacks somam sem passar de 100%.")]
    [SerializeField, Range(0f, 1f)] float chancePerStack = 0.15f;
    [Tooltip("Quantas vezes o raio pula depois do alvo (+1 a cada stack extra).")]
    [SerializeField, Min(0)] int jumps = 3;
    [Tooltip("Dano em cada inimigo, em múltiplos do dano do player (0.8 = 80%).")]
    [SerializeField, Min(0f)] float damageCoefficient = 0.8f;
    [Tooltip("Distância máxima de um pulo (metros).")]
    [SerializeField, Min(1f)] float jumpRange = 10f;
    [SerializeField] Color boltColor = new(0.55f, 0.85f, 1f, 1f);

    public override void OnHit(Inventory inventory, int stacks, Health target, DamageInfo info)
    {
        if (target == null || !target.TryGetComponent(out EnemyBase first) || !Inventory.Roll(chancePerStack, stacks)) return;

        float playerDamage = inventory.Stats != null ? inventory.Stats.Damage : info.Amount;
        float damage = playerDamage * damageCoefficient;
        int total = jumps + (stacks - 1);

        // Escolhe a cadeia toda antes de causar dano: o dano pode matar e disparar itens "ao matar"
        var chain = new List<EnemyBase> { first };
        Vector3 from = first.Center;
        for (int i = 0; i < total; i++)
        {
            var next = EnemyBase.Nearest(from, jumpRange, chain);
            if (next == null) break;
            chain.Add(next);
            from = next.Center;
        }

        // Faísca de cima no primeiro e zigue-zague entre os seguintes
        ItemVfx.Lightning(first.Center + Vector3.up * 1.6f, first.Center, boltColor, 0.07f, 0.18f);
        for (int i = 0; i < chain.Count; i++)
        {
            var enemy = chain[i];
            if (enemy == null) continue;
            if (i > 0 && chain[i - 1] != null) ItemVfx.Lightning(chain[i - 1].Center, enemy.Center, boltColor);
            if (enemy.IsAlive) inventory.DealItemDamage(enemy.Health, damage, enemy.Center);
        }
    }
}
