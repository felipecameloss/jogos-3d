using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Secundária: estocada com agulha. Avança um pouco e acerta tudo num retângulo
/// à frente com dano alto. Dispara os itens "ao acertar".
/// </summary>
public class NeedleThrust : PlayerAbility
{
    [Header("Estocada")]
    [Tooltip("Dano em múltiplos do dano do player (5 = 500%).")]
    [SerializeField, Min(0f)] float damageCoefficient = 5f;
    [SerializeField, Min(0f)] float lungeDistance = 4.2f;
    [SerializeField, Min(0.01f)] float lungeTime = 0.1f;
    [Tooltip("Área do golpe à frente do player (largura, altura, comprimento).")]
    [SerializeField] Vector3 hitBox = new(2.6f, 2.4f, 4.6f);
    [Tooltip("Agulha que aparece durante o golpe (opcional).")]
    [SerializeField] GameObject needleVisual;

    /// <summary>Alvos acertados no último golpe (testes).</summary>
    public readonly List<Health> LastHits = new();

    readonly Collider[] overlap = new Collider[64];

    protected override void Awake()
    {
        base.Awake();
        if (needleVisual != null) needleVisual.SetActive(false);
    }

    protected override bool TryActivate()
    {
        StartCoroutine(Thrust(AimForward));
        return true;
    }

    IEnumerator Thrust(Vector3 dir)
    {
        if (needleVisual != null) needleVisual.SetActive(true);
        if (Movement != null) Movement.Dash(dir, lungeDistance, lungeTime);
        yield return new WaitForSeconds(lungeTime);
        Strike(dir);
        yield return new WaitForSeconds(0.12f);
        if (needleVisual != null) needleVisual.SetActive(false);
    }

    void Strike(Vector3 dir)
    {
        LastHits.Clear();
        Quaternion rot = Quaternion.LookRotation(dir);
        Vector3 center = transform.position + Vector3.up * (hitBox.y * 0.5f) + dir * (hitBox.z * 0.5f);
        int n = Physics.OverlapBoxNonAlloc(center, hitBox * 0.5f, overlap, rot, ~0, QueryTriggerInteraction.Ignore);
        float damage = (Stats != null ? Stats.Damage : 12f) * damageCoefficient;
        for (int i = 0; i < n; i++)
        {
            var h = overlap[i].GetComponentInParent<Health>();
            if (h == null || h == Health || !h.IsAlive || LastHits.Contains(h)) continue;
            LastHits.Add(h);
            h.TakeDamage(new DamageInfo(damage, gameObject, overlap[i].bounds.center, dir));
        }
    }
}
