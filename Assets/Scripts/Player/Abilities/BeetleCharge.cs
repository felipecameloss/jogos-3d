using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Secundária do besouro: investida. Corre para a frente (para onde mira) atropelando
/// tudo no caminho com dano alto, uma vez por alvo. Invencível durante a investida.
/// </summary>
public class BeetleCharge : PlayerAbility
{
    [Header("Investida")]
    [SerializeField, Min(0f)] float distance = 10f;
    [SerializeField, Min(0.01f)] float duration = 0.32f;
    [Tooltip("Dano em múltiplos do dano do player (4 = 400%).")]
    [SerializeField, Min(0f)] float damageCoefficient = 4f;
    [Tooltip("Área de atropelo à frente do player (largura, altura, comprimento).")]
    [SerializeField] Vector3 hitBox = new(2.8f, 2.4f, 2.6f);

    /// <summary>Alvos atropelados na última investida (testes).</summary>
    public readonly List<Health> LastHits = new();
    public bool IsCharging { get; private set; }

    InsectPlayerVisual visual;
    TrailRenderer trail;
    readonly Collider[] overlap = new Collider[64];

    protected override void Awake()
    {
        base.Awake();
        trail = GetComponent<TrailRenderer>();
    }

    public void SetVisual(InsectPlayerVisual modelVisual) => visual = modelVisual;

    protected override bool CanUse => base.CanUse && !IsCharging && (Movement == null || !Movement.IsRooted);

    protected override bool TryActivate()
    {
        StartCoroutine(ChargeRoutine(AimForward));
        return true;
    }

    IEnumerator ChargeRoutine(Vector3 dir)
    {
        IsCharging = true;
        LastHits.Clear();
        bool wasInvincible = Health != null && Health.Invincible;
        if (Health != null) Health.Invincible = true;
        if (trail != null) trail.emitting = true;
        if (Movement != null) Movement.Dash(dir, distance, duration);
        float damage = (Stats != null ? Stats.Damage : 12f) * damageCoefficient;
        for (float t = 0f; t < duration + 0.05f; t += Time.deltaTime)
        {
            if (visual != null) visual.Kick(0.35f, 10f);
            Ram(dir, damage);
            yield return null;
        }
        Ram(dir, damage);
        if (Health != null && !wasInvincible) Health.Invincible = false;
        if (trail != null) trail.emitting = false;
        if (LastHits.Count > 0) GameFeel.Shake(0.5f);
        IsCharging = false;
    }

    void Ram(Vector3 dir, float damage)
    {
        Quaternion rot = Quaternion.LookRotation(dir);
        Vector3 center = transform.position + Vector3.up * (hitBox.y * 0.5f) + dir * (hitBox.z * 0.5f);
        int n = Physics.OverlapBoxNonAlloc(center, hitBox * 0.5f, overlap, rot, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = overlap[i].GetComponentInParent<Health>();
            if (h == null || h == Health || !h.IsAlive || LastHits.Contains(h)) continue;
            LastHits.Add(h);
            h.TakeDamage(new DamageInfo(damage, gameObject, overlap[i].bounds.center, dir));
        }
    }

    void OnDisable()
    {
        // Cena trocou/componente desligado no meio da investida: não deixa o player imortal
        if (!IsCharging) return;
        IsCharging = false;
        if (Health != null) Health.Invincible = false;
        if (trail != null) trail.emitting = false;
    }
}
