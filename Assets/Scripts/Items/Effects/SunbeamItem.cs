using UnityEngine;

/// <summary>
/// De tempos em tempos, um raio de sol concentrado queima o inimigo mais perto da mira (Caco de lente).
/// Mais stacks = mais dano.
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Itens/Raio de sol", fileName = "NovoItemRaioDeSol")]
public class SunbeamItem : ItemData
{
    [SerializeField, Min(1)] int intervalSeconds = 4;
    [Tooltip("Dano em múltiplos do dano do player no 1º stack.")]
    [SerializeField, Min(0f)] float damageCoefficient = 4f;
    [Tooltip("Dano extra por stack a mais.")]
    [SerializeField, Min(0f)] float damagePerExtraStack = 3f;
    [Tooltip("Alcance a partir do player.")]
    [SerializeField, Min(1f)] float range = 40f;
    [Tooltip("Raio em volta do ponto da mira para escolher o alvo.")]
    [SerializeField, Min(1f)] float aimRadius = 12f;
    [SerializeField] Color beamColor = new(1f, 0.92f, 0.45f, 0.75f);

    class State { public int seconds; }

    /// <summary>Dano do raio para tantos stacks, com o dano atual do player.</summary>
    public float DamageFor(Inventory inventory, int stacks)
    {
        float playerDamage = inventory.Stats != null ? inventory.Stats.Damage : 12f;
        return playerDamage * (damageCoefficient + damagePerExtraStack * (stacks - 1));
    }

    public override void OnSecond(Inventory inventory, int stacks)
    {
        var state = inventory.GetState<State>(this);
        if (++state.seconds < intervalSeconds) return;

        var target = PickTarget(inventory);
        if (target == null) return; // ninguém por perto: fica carregado e tenta no próximo segundo
        state.seconds = 0;

        Vector3 ground = target.transform.position;
        ItemVfx.Beam(ground, beamColor, 1f, 40f, 0.45f);
        inventory.DealItemDamage(target.Health, DamageFor(inventory, stacks), target.Center, ignoreArmor: true);
    }

    EnemyBase PickTarget(Inventory inventory)
    {
        Vector3 origin = inventory.transform.position;
        // Prefere o inimigo perto da mira; senão, o mais perto do player
        if (inventory.TryGetComponent(out ThornSlingshot sling))
        {
            var aimed = EnemyBase.Nearest(sling.GetAimPoint(), aimRadius);
            if (aimed != null && (aimed.transform.position - origin).sqrMagnitude <= range * range) return aimed;
        }
        return EnemyBase.Nearest(origin, range);
    }
}
