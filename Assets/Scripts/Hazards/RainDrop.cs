using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gota de chuva gigante (evento de chuva, issue #33). Mesma ideia do pisão humano (HumanStomp):
/// a sombra redonda cresce e escurece no chão (aviso), a gota cai do céu e estoura no
/// impacto. Quem está dentro do círculo leva dano: o player perde uma fração da vida
/// máxima e é empurrado; inimigos perdem uma fração maior (chefes, bem menos).
/// Feita para pool: Begin reinicia tudo.
/// </summary>
public class RainDrop : MonoBehaviour
{
    [Header("Partes")]
    [Tooltip("Disco da sombra (escala em X/Z = diâmetro).")]
    [SerializeField] Transform shadow;
    [SerializeField] Renderer shadowRenderer;
    [Tooltip("A gota; o pivô fica na ponta de baixo.")]
    [SerializeField] Transform drop;
    [SerializeField] Renderer dropRenderer;

    [Header("Área")]
    [SerializeField, Min(0.5f)] float radius = 2.6f;

    [Header("Tempo")]
    [SerializeField, Min(0.1f)] float warningTime = 1.2f;
    [SerializeField, Min(0.05f)] float fallTime = 0.3f;
    [SerializeField, Min(0.05f)] float splashTime = 0.35f;
    [Tooltip("Altura da gota no começo do aviso e no começo da queda.")]
    [SerializeField] Vector2 hoverHeights = new(40f, 24f);

    [Header("Dano")]
    [Tooltip("Fração da vida máxima que o player perde.")]
    [SerializeField, Range(0f, 1f)] float playerDamageFraction = 0.15f;
    [Tooltip("Fração da vida máxima que um inimigo comum perde.")]
    [SerializeField, Range(0f, 1f)] float enemyDamageFraction = 0.4f;
    [Tooltip("Fração da vida máxima que um chefe perde.")]
    [SerializeField, Range(0f, 1f)] float bossDamageFraction = 0.04f;
    [Tooltip("Empurrão do player (horizontal, vertical).")]
    [SerializeField] Vector2 knockback = new(9f, 5f);

    [Header("Cores")]
    [SerializeField] Color shadowColor = new(0.04f, 0.1f, 0.2f, 0.65f);
    [SerializeField] Color waterColor = new(0.55f, 0.8f, 1f, 0.8f);
    [SerializeField, Range(0f, 1f)] float shadowStartScale = 0.3f;

    /// <summary>A gota bateu no chão (som, câmera).</summary>
    public event Action<RainDrop> OnImpact;

    public bool HasImpacted { get; private set; }
    public float Elapsed { get; private set; }
    public float Radius => radius;
    public float WarningTime => warningTime;
    public float ImpactTime => warningTime + fallTime;
    /// <summary>Quem foi atingido no último impacto (testes/debug).</summary>
    public readonly List<IDamageable> LastHits = new();

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    MaterialPropertyBlock block;
    Action<RainDrop> release;
    Vector3 dropRestScale = Vector3.one;
    readonly Collider[] overlap = new Collider[64];

    void Awake()
    {
        block = new MaterialPropertyBlock();
        if (drop != null) dropRestScale = drop.localScale;
    }

    public void Begin(Vector3 groundPoint, Action<RainDrop> release)
    {
        transform.SetPositionAndRotation(groundPoint, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
        this.release = release;
        Elapsed = 0f;
        HasImpacted = false;
        LastHits.Clear();
        UpdateVisuals();
    }

    void Update()
    {
        Elapsed += Time.deltaTime;
        if (!HasImpacted && Elapsed >= ImpactTime) Impact();
        UpdateVisuals();
        if (Elapsed >= ImpactTime + splashTime) Despawn();
    }

    void UpdateVisuals()
    {
        float impactAt = ImpactTime;

        // Gota: desce devagar no aviso, despenca, e no impacto achata e some (estouro)
        float h;
        if (Elapsed < warningTime) h = Mathf.Lerp(hoverHeights.x, hoverHeights.y, Elapsed / warningTime);
        else if (Elapsed < impactAt) { float k = (Elapsed - warningTime) / fallTime; h = Mathf.Lerp(hoverHeights.y, 0f, k * k); }
        else h = 0f;
        float splash = Mathf.Clamp01((Elapsed - impactAt) / splashTime);
        if (drop != null)
        {
            drop.localPosition = new Vector3(0f, h, 0f);
            // estica na queda, achata no estouro
            float stretch = Elapsed >= warningTime && Elapsed < impactAt ? 1.25f : 1f;
            drop.localScale = Elapsed < impactAt
                ? Vector3.Scale(dropRestScale, new Vector3(1f / Mathf.Sqrt(stretch), stretch, 1f / Mathf.Sqrt(stretch)))
                : Vector3.Scale(dropRestScale, new Vector3(1f + splash * 1.6f, Mathf.Max(0.05f, 1f - splash), 1f + splash * 1.6f));
        }
        if (dropRenderer != null)
        {
            dropRenderer.GetPropertyBlock(block);
            var c = waterColor;
            c.a *= 1f - splash;
            block.SetColor(BaseColorId, c);
            dropRenderer.SetPropertyBlock(block);
        }

        // Sombra: cresce e escurece no aviso, some no estouro
        float grow = Mathf.SmoothStep(shadowStartScale, 1f, Mathf.Clamp01(Elapsed / impactAt));
        float alpha = Elapsed < impactAt ? Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(Elapsed / impactAt)) : 1f - splash;
        if (shadow != null) shadow.localScale = new Vector3(radius * 2f * grow, shadow.localScale.y, radius * 2f * grow);
        if (shadowRenderer != null)
        {
            shadowRenderer.GetPropertyBlock(block);
            var c = shadowColor;
            c.a *= alpha;
            block.SetColor(BaseColorId, c);
            shadowRenderer.SetPropertyBlock(block);
        }
    }

    void Impact()
    {
        HasImpacted = true;
        LastHits.Clear();
        Vector3 center = transform.position + Vector3.up * (radius * 0.5f);
        int count = Physics.OverlapSphereNonAlloc(center, radius, overlap, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var target = overlap[i].GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive || LastHits.Contains(target)) continue;
            // dentro do círculo no chão (a esfera pega um pouco além nas bordas de cima)
            var comp = (Component)target;
            Vector3 flat = comp.transform.position - transform.position; flat.y = 0f;
            if (flat.magnitude > radius + 0.5f) continue;
            LastHits.Add(target);
            Vector3 dir = flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward;
            if (comp.TryGetComponent(out PlayerMovement movement) && target is Health ph)
            {
                ph.TakeDamage(new DamageInfo(ph.Max * playerDamageFraction, gameObject, comp.transform.position, dir) { IgnoreArmor = true });
                movement.AddKnockback(dir * knockback.x + Vector3.up * knockback.y);
            }
            else if (comp.TryGetComponent(out EnemyBase enemy) && target is Health eh)
                eh.TakeDamage(new DamageInfo(eh.Max * (enemy.IsBoss ? bossDamageFraction : enemyDamageFraction), gameObject, comp.transform.position, dir) { IgnoreArmor = true });
            else
                target.TakeDamage(new DamageInfo(20f, gameObject, comp.transform.position, dir) { IgnoreArmor = true });
        }

        ItemVfx.Explosion(transform.position + Vector3.up * 0.3f, radius, new Color(0.6f, 0.85f, 1f, 0.55f), 0.35f);
        var pl = GameObject.FindGameObjectWithTag("Player");
        float dist = pl != null ? Vector3.Distance(pl.transform.position, transform.position) : 99f;
        GameFeel.Shake(Mathf.Lerp(0.6f, 0f, Mathf.InverseLerp(3f, 25f, dist)));
        OnImpact?.Invoke(this);
    }

    void Despawn()
    {
        var r = release;
        release = null;
        if (r != null) r(this);
        else Destroy(gameObject);
    }

#if UNITY_EDITOR
    public void EditorSetup(Transform shadowT, Renderer shadowR, Transform dropT, Renderer dropR)
    {
        shadow = shadowT; shadowRenderer = shadowR; drop = dropT; dropRenderer = dropR;
    }
#endif

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
