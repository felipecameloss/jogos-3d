using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lupa: concentra o sol num raio que sai do player na direção da mira por
/// alguns segundos. Queima tudo que estiver na linha (atravessa inimigos, para
/// em paredes e no chão) e deixa os alvos pegando fogo. Dá para varrer a mira
/// enquanto o raio está ativo.
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Equipamentos/Lupa", fileName = "Lupa")]
public class MagnifierEquipment : EquipmentData
{
    [SerializeField, Min(0.1f)] float duration = 1.6f;
    [SerializeField, Min(0.02f)] float tickInterval = 0.1f;
    [Tooltip("Dano por tick em múltiplos do dano do player.")]
    [SerializeField, Min(0f)] float damagePerTick = 0.5f;
    [Tooltip("Queimadura aplicada a cada inimigo atingido (múltiplos do dano do player, total).")]
    [SerializeField, Min(0f)] float burnDamage = 2f;
    [SerializeField, Min(0.1f)] float burnDuration = 3f;
    [SerializeField, Min(1f)] float range = 35f;
    [Tooltip("Distância da linha do raio que ainda acerta.")]
    [SerializeField, Min(0.1f)] float beamRadius = 1.1f;
    [SerializeField] Color beamColor = new(1f, 0.9f, 0.35f, 0.9f);
    [SerializeField] Color coreColor = new(1f, 1f, 0.85f, 1f);

    static readonly RaycastHit[] hits = new RaycastHit[16];
    static readonly List<EnemyBase> buffer = new();

    public override bool Activate(EquipmentSlot slot)
    {
        slot.StartCoroutine(BeamRoutine(slot));
        return true;
    }

    IEnumerator BeamRoutine(EquipmentSlot slot)
    {
        var burned = new HashSet<EnemyBase>();
        float nextTick = 0f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (slot == null || (slot.Health != null && !slot.Health.IsAlive)) yield break;
            if (t >= nextTick)
            {
                nextTick += tickInterval;
                Tick(slot, burned);
            }
            yield return null;
        }
    }

    void Tick(EquipmentSlot slot, HashSet<EnemyBase> burned)
    {
        Vector3 from = slot.Chest;
        Vector3 dir = slot.AimPoint - from;
        if (dir.sqrMagnitude < 0.01f) dir = slot.transform.forward;
        dir.Normalize();
        Vector3 to = from + dir * BlockedDistance(slot, from, dir);

        ItemVfx.Line(from, to, beamColor, beamRadius * 0.7f, tickInterval * 1.6f);
        ItemVfx.Line(from, to, coreColor, beamRadius * 0.25f, tickInterval * 1.6f);

        float damage = slot.PlayerDamage * damagePerTick;
        buffer.Clear();
        buffer.AddRange(EnemyBase.Active); // dano pode matar e tirar da lista no meio
        foreach (var enemy in buffer)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            Vector3 c = enemy.Center;
            float along = Vector3.Dot(c - from, dir);
            if (along < 0f || along > (to - from).magnitude + beamRadius) continue;
            if ((from + dir * along - c).sqrMagnitude > beamRadius * beamRadius * 2.5f) continue;

            Deal(slot, enemy.Health, damage, c);
            if (burned.Add(enemy) && burnDamage > 0f)
                StatusEffects.On(enemy).ApplyBurn(slot.PlayerDamage * burnDamage, burnDuration, slot.gameObject);
        }
    }

    /// <summary>Até onde o raio vai antes de bater em parede/chão (ignora player e inimigos).</summary>
    float BlockedDistance(EquipmentSlot slot, Vector3 from, Vector3 dir)
    {
        float best = range;
        int n = Physics.RaycastNonAlloc(from, dir, hits, range, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = hits[i].collider;
            if (col.transform.IsChildOf(slot.transform)) continue;
            if (col.GetComponentInParent<EnemyBase>() != null) continue;
            if (hits[i].distance < best) best = hits[i].distance;
        }
        return best;
    }

    static void Deal(EquipmentSlot slot, Health target, float amount, Vector3 point)
    {
        if (target == null || !target.IsAlive || amount <= 0f) return;
        if (slot.Inventory != null) slot.Inventory.DealItemDamage(target, amount, point, ignoreArmor: true);
        else target.TakeDamage(new DamageInfo(amount, slot.gameObject, point, Vector3.zero) { NoProc = true, IgnoreArmor = true });
    }
}
