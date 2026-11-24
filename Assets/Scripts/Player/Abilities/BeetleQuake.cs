using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Especial do besouro: terremoto. Bate no chão e acerta todo mundo em volta com dano
/// alto, deixando os inimigos lentos por alguns segundos.
/// </summary>
public class BeetleQuake : PlayerAbility
{
    [Header("Terremoto")]
    [SerializeField, Min(0.5f)] float radius = 7.5f;
    [Tooltip("Dano em múltiplos do dano do player (5 = 500%).")]
    [SerializeField, Min(0f)] float damageCoefficient = 5f;
    [SerializeField, Range(0f, 1f)] float slowAmount = 0.5f;
    [SerializeField, Min(0f)] float slowDuration = 2.5f;

    /// <summary>Alvos acertados no último terremoto (testes).</summary>
    public readonly List<Health> LastHits = new();

    InsectPlayerVisual visual;
    readonly Collider[] overlap = new Collider[128];

    public void SetVisual(InsectPlayerVisual modelVisual) => visual = modelVisual;

    protected override bool TryActivate()
    {
        LastHits.Clear();
        Vector3 c = transform.position + Vector3.up * 0.5f;
        float damage = (Stats != null ? Stats.Damage : 12f) * damageCoefficient;
        int n = Physics.OverlapSphereNonAlloc(c, radius, overlap, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = overlap[i].GetComponentInParent<Health>();
            if (h == null || h == Health || !h.IsAlive || LastHits.Contains(h)) continue;
            LastHits.Add(h);
            Vector3 dir = h.transform.position - transform.position; dir.y = 0f;
            h.TakeDamage(new DamageInfo(damage, gameObject, overlap[i].bounds.center, dir));
            if (h.IsAlive && h.TryGetComponent(out EnemyBase enemy)) StatusEffects.On(enemy).ApplySlow(slowAmount, slowDuration);
        }
        ItemVfx.Explosion(transform.position + Vector3.up * 0.2f, radius, new Color(0.75f, 0.55f, 0.3f, 0.55f), 0.4f);
        GameFeel.Shake(1.1f);
        if (visual != null) visual.Kick(0.2f, 18f);
        return true;
    }
}
