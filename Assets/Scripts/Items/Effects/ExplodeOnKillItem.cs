using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inimigo explode ao morrer, causando dano em área nos inimigos próximos (Casca de amendoim).
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Itens/Explosão ao matar", fileName = "NovoItemExplosao")]
public class ExplodeOnKillItem : ItemData
{
    [SerializeField, Min(0.5f)] float radius = 6f;
    [SerializeField, Min(0f)] float radiusPerExtraStack = 1.5f;
    [Tooltip("Dano em múltiplos do dano do player no 1º stack (2.5 = 250%).")]
    [SerializeField, Min(0f)] float damageCoefficient = 2.5f;
    [Tooltip("Dano extra por stack a mais.")]
    [SerializeField, Min(0f)] float damagePerExtraStack = 1.5f;
    [SerializeField] Color explosionColor = new(1f, 0.55f, 0.18f, 0.55f);

    public override void OnKill(Inventory inventory, int stacks, EnemyBase enemy, DamageInfo killingBlow)
    {
        if (enemy == null) return;
        Vector3 center = enemy.Center;
        float r = radius + radiusPerExtraStack * (stacks - 1);
        float rSq = r * r;
        float playerDamage = inventory.Stats != null ? inventory.Stats.Damage : 12f;
        float damage = playerDamage * (damageCoefficient + damagePerExtraStack * (stacks - 1));

        ItemVfx.Explosion(center, r, explosionColor, 0.28f);

        // Cópia local: dano pode matar outro inimigo e disparar novas explosões em cadeia
        var targets = new List<EnemyBase>();
        foreach (var other in EnemyBase.Active)
        {
            if (other == null || other == enemy || !other.IsAlive) continue;
            if ((other.Center - center).sqrMagnitude <= rSq) targets.Add(other);
        }

        foreach (var target in targets)
        {
            if (target != null && target.IsAlive)
                inventory.DealItemDamage(target.Health, damage, target.Center);
        }
    }
}
