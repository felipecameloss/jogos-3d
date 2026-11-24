using System.Collections;
using UnityEngine;

/// <summary>
/// Utilidade do besouro: fecha a carapaça. Por alguns segundos leva bem menos dano
/// e solta um empurrão de choque em volta (dano baixo) ao fechar.
/// </summary>
public class BeetleShell : PlayerAbility
{
    [Header("Carapaça")]
    [SerializeField, Min(0.1f)] float duration = 3.5f;
    [Tooltip("Fração do dano que ainda passa (0.3 = leva só 30%).")]
    [SerializeField, Range(0f, 1f)] float damageTaken = 0.3f;
    [SerializeField, Min(0f)] float pulseRadius = 4f;
    [Tooltip("Dano do choque ao fechar, em múltiplos do dano do player.")]
    [SerializeField, Min(0f)] float pulseCoefficient = 1f;

    public bool IsActive { get; private set; }
    public float DamageTaken => damageTaken;

    InsectPlayerVisual visual;
    readonly Collider[] overlap = new Collider[64];
    readonly System.Collections.Generic.HashSet<Health> hit = new();

    public void SetVisual(InsectPlayerVisual modelVisual) => visual = modelVisual;

    protected override bool CanUse => base.CanUse && !IsActive;

    protected override bool TryActivate()
    {
        StartCoroutine(ShellRoutine());
        return true;
    }

    IEnumerator ShellRoutine()
    {
        IsActive = true;
        if (Health != null) Health.DamageTakenMultiplier = damageTaken;
        Pulse();
        if (visual != null) visual.Kick(0.12f, 6f);
        float end = Time.time + duration;
        float nextRing = 0f;
        while (Time.time < end && (Health == null || Health.IsAlive))
        {
            // anel no chão enquanto dura (mostra que está protegido)
            if (Time.time >= nextRing)
            {
                nextRing = Time.time + 0.5f;
                ItemVfx.Explosion(transform.position + Vector3.up * 0.8f, 1.5f, new Color(1f, 0.8f, 0.3f, 0.25f), 0.45f);
            }
            yield return null;
        }
        if (Health != null) Health.DamageTakenMultiplier = 1f;
        IsActive = false;
    }

    void Pulse()
    {
        Vector3 c = transform.position + Vector3.up * 0.8f;
        ItemVfx.Explosion(c, pulseRadius, new Color(1f, 0.82f, 0.35f, 0.5f));
        GameFeel.Shake(0.3f);
        hit.Clear();
        float damage = (Stats != null ? Stats.Damage : 12f) * pulseCoefficient;
        int n = Physics.OverlapSphereNonAlloc(c, pulseRadius, overlap, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = overlap[i].GetComponentInParent<Health>();
            if (h == null || h == Health || !h.IsAlive || !hit.Add(h)) continue;
            Vector3 dir = h.transform.position - transform.position; dir.y = 0f;
            h.TakeDamage(new DamageInfo(damage, gameObject, overlap[i].bounds.center, dir));
        }
    }

    void OnDisable()
    {
        if (!IsActive) return;
        IsActive = false;
        if (Health != null) Health.DamageTakenMultiplier = 1f;
    }
}
