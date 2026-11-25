using UnityEngine;

/// <summary>
/// Apito de feromônio: um assobio que espalha cheiro de formigueiro inimigo pelo quintal.
/// Toda formiga no alcance (operária e soldado) entra em fúria por alguns segundos: esquece
/// o player e briga com a formiga em fúria mais perto. Formiga que morre na briga solta
/// migalhas como sempre. Sem formigas no alcance, não gasta a recarga.
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Equipamentos/Apito de feromônio", fileName = "ApitoDeFeromonio")]
public class PheromoneWhistleEquipment : EquipmentData
{
    [Tooltip("Segundos que as formigas brigam entre si.")]
    [SerializeField, Min(0.5f)] float frenzyDuration = 8f;
    [Tooltip("Alcance do apito em metros (o padrão cobre o quintal todo).")]
    [SerializeField, Min(1f)] float range = 80f;
    [SerializeField] Color pheromoneColor = new(1f, 0.35f, 0.65f, 0.5f);

    public float FrenzyDuration => frenzyDuration;

    /// <summary>O apito só mexe com formigas (operária e soldado; cupim e chefe não).</summary>
    public static bool IsAnt(EnemyBase enemy) => enemy != null && enemy.IsAnt && !enemy.IsBoss;

    /// <summary>Formigas vivas no alcance agora (HUD/testes).</summary>
    public int AntsInRange(Vector3 origin)
    {
        int n = 0;
        foreach (var e in EnemyBase.Active)
            if (Affects(e, origin)) n++;
        return n;
    }

    bool Affects(EnemyBase e, Vector3 origin) =>
        e != null && e.IsAlive && IsAnt(e) && (e.transform.position - origin).sqrMagnitude <= range * range;

    public override bool Activate(EquipmentSlot slot)
    {
        Vector3 origin = slot.transform.position;
        int count = 0;
        for (int i = EnemyBase.Active.Count - 1; i >= 0; i--)
        {
            var e = EnemyBase.Active[i];
            if (!Affects(e, origin) || !StatusEffects.On(e).ApplyFrenzy(frenzyDuration)) continue;
            count++;
            ItemVfx.Explosion(e.Center + Vector3.up * 0.4f, 1.1f, pheromoneColor, 0.55f); // "sentiu o cheiro"
        }
        if (count == 0) return false;

        // O assobio: ondas rosadas saindo do player
        Vector3 chest = slot.Chest;
        ItemVfx.Explosion(chest, 2.2f, pheromoneColor, 0.3f);
        ItemVfx.Explosion(chest, 4.5f, pheromoneColor * new Color(1f, 1f, 1f, 0.6f), 0.5f);
        ItemVfx.Explosion(chest, 7.5f, pheromoneColor * new Color(1f, 1f, 1f, 0.3f), 0.7f);
        return true;
    }
}
