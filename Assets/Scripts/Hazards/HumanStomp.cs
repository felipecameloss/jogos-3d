using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Um pisão de pé humano: a sombra da sola cresce e escurece no chão (aviso),
/// a perna desce do céu, pisa, fica um instante e sobe.
/// No impacto: inimigos (e qualquer outra coisa com vida) morrem; o player perde
/// uma fração da vida máxima. Quem fica na beirada é empurrado.
/// Feito para pool: Begin reinicia tudo.
/// </summary>
public class HumanStomp : MonoBehaviour
{
    [Header("Partes")]
    [SerializeField] Transform shadow;
    [SerializeField] Renderer shadowRenderer;
    [Tooltip("Pé + perna; o pivô fica na sola.")]
    [SerializeField] Transform foot;

    [Header("Área")]
    [Tooltip("Largura x comprimento da sola (e da área de dano).")]
    [SerializeField] Vector2 footprint = new(3.5f, 7f);
    [Tooltip("Altura da área de dano a partir do chão.")]
    [SerializeField, Min(0.5f)] float hitHeight = 3f;

    [Header("Tempo")]
    [SerializeField, Min(0.1f)] float warningTime = 2.2f;
    [SerializeField, Min(0.05f)] float dropTime = 0.25f;
    [SerializeField, Min(0f)] float holdTime = 0.6f;
    [SerializeField, Min(0.05f)] float liftTime = 0.7f;
    [Tooltip("Altura do pé no começo do aviso e no começo da descida.")]
    [SerializeField] Vector2 hoverHeights = new(45f, 22f);

    [Header("Dano")]
    [Tooltip("Fração da vida máxima que o player perde (inimigos morrem).")]
    [SerializeField, Range(0f, 1f)] float playerDamageFraction = 0.45f;
    [Tooltip("Fração da vida máxima que um chefe perde (chefes não morrem de uma vez).")]
    [SerializeField, Range(0f, 1f)] float bossDamageFraction = 0.15f;
    [Tooltip("Distância fora da sola em que o player ainda é empurrado.")]
    [SerializeField, Min(0f)] float edgeKnockbackRange = 1.5f;
    [Tooltip("Empurrão (horizontal, vertical).")]
    [SerializeField] Vector2 edgeKnockback = new(10f, 5f);

    [Header("Sombra")]
    [SerializeField] Color shadowColor = new(0f, 0f, 0f, 0.7f);
    [SerializeField, Range(0f, 1f)] float shadowStartScale = 0.35f;

    /// <summary>Pisou (tremer a câmera, som, poeira).</summary>
    public event Action<HumanStomp> OnImpact;

    public bool HasImpacted { get; private set; }
    public float Elapsed { get; private set; }
    public float WarningTime => warningTime;
    public Vector2 Footprint => footprint;
    /// <summary>0..1 do aviso (quão cheia está a sombra).</summary>
    public float WarningProgress => Mathf.Clamp01(Elapsed / warningTime);
    /// <summary>Quem foi atingido no último impacto (testes/debug).</summary>
    public readonly List<IDamageable> LastHits = new();

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    MaterialPropertyBlock block;
    Action<HumanStomp> release;
    readonly Collider[] overlap = new Collider[64];

    void Awake()
    {
        block = new MaterialPropertyBlock();
    }

    public void Begin(Vector3 groundPoint, float yaw, Action<HumanStomp> release)
    {
        transform.SetPositionAndRotation(groundPoint, Quaternion.Euler(0f, yaw, 0f));
        this.release = release;
        Elapsed = 0f;
        HasImpacted = false;
        LastHits.Clear();
        UpdateVisuals();
    }

    void Update()
    {
        Elapsed += Time.deltaTime;
        if (!HasImpacted && Elapsed >= warningTime + dropTime) Impact();
        UpdateVisuals();
        if (Elapsed >= warningTime + dropTime + holdTime + liftTime) Despawn();
    }

    void UpdateVisuals()
    {
        float impactAt = warningTime + dropTime;
        float liftStart = impactAt + holdTime;

        // Pé: desce devagar durante o aviso, cai rápido, fica, sobe
        float h;
        if (Elapsed < warningTime) h = Mathf.Lerp(hoverHeights.x, hoverHeights.y, Elapsed / warningTime);
        else if (Elapsed < impactAt) { float k = (Elapsed - warningTime) / dropTime; h = Mathf.Lerp(hoverHeights.y, 0f, k * k); }
        else if (Elapsed < liftStart) h = 0f;
        else { float k = Mathf.Clamp01((Elapsed - liftStart) / liftTime); h = Mathf.Lerp(0f, hoverHeights.x, k * k); }
        if (foot != null) foot.localPosition = new Vector3(0f, h, 0f);

        // Sombra: cresce e escurece no aviso, some quando o pé sobe
        float grow = Mathf.SmoothStep(shadowStartScale, 1f, Mathf.Clamp01(Elapsed / impactAt));
        float alpha = Elapsed < liftStart
            ? Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(Elapsed / impactAt))
            : 1f - Mathf.Clamp01((Elapsed - liftStart) / liftTime);
        if (shadow != null) shadow.localScale = new Vector3(footprint.x * grow, 1f, footprint.y * grow);
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
        Vector3 center = transform.position + Vector3.up * (hitHeight * 0.5f);
        Vector3 half = new Vector3(footprint.x * 0.5f, hitHeight * 0.5f, footprint.y * 0.5f) * 0.95f;
        int count = Physics.OverlapBoxNonAlloc(center, half, overlap, transform.rotation, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            var target = overlap[i].GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive || LastHits.Contains(target)) continue;
            LastHits.Add(target);
            var comp = (Component)target;
            Vector3 dir = comp.transform.position - transform.position;
            if (comp.TryGetComponent(out PlayerMovement _) && target is Health hp)
                hp.TakeDamage(new DamageInfo(hp.Max * playerDamageFraction, gameObject, comp.transform.position, dir) { IgnoreArmor = true });
            else if (comp.TryGetComponent(out EnemyBase boss) && boss.IsBoss && target is Health bh)
                bh.TakeDamage(new DamageInfo(bh.Max * bossDamageFraction, gameObject, comp.transform.position, dir) { IgnoreArmor = true });
            else if (target is Health h)
                h.Kill(gameObject);
            else
                target.TakeDamage(new DamageInfo(99999f, gameObject, comp.transform.position, dir) { IgnoreArmor = true });
        }

        PushPlayerAtEdge(half);
        // Tremida forte perto, fraca longe
        var pl = GameObject.FindGameObjectWithTag("Player");
        float dist = pl != null ? Vector3.Distance(pl.transform.position, transform.position) : 99f;
        GameFeel.Shake(Mathf.Lerp(1.6f, 0.2f, Mathf.InverseLerp(4f, 35f, dist)));
        OnImpact?.Invoke(this);
    }

    void PushPlayerAtEdge(Vector3 half)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || !player.TryGetComponent(out PlayerMovement movement)) return;
        // Distância da sola no espaço local do pé
        Vector3 local = transform.InverseTransformPoint(player.transform.position);
        float dx = Mathf.Max(0f, Mathf.Abs(local.x) - half.x);
        float dz = Mathf.Max(0f, Mathf.Abs(local.z) - half.z);
        float edgeDist = Mathf.Sqrt(dx * dx + dz * dz);
        if (edgeDist <= 0f || edgeDist > edgeKnockbackRange || local.y > hitHeight) return; // dentro (já levou dano) ou longe
        Vector3 away = player.transform.position - transform.position;
        away.y = 0f;
        // Forte perto da sola, metade da força no limite do alcance
        float strength = 1f - 0.5f * edgeDist / Mathf.Max(edgeKnockbackRange, 0.01f);
        movement.AddKnockback(away.normalized * (edgeKnockback.x * strength) + Vector3.up * (edgeKnockback.y * strength));
    }

    void Despawn()
    {
        var r = release;
        release = null;
        if (r != null) r(this);
        else Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.5f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position + Vector3.up * (hitHeight * 0.5f), transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(footprint.x, hitHeight, footprint.y));
    }
}
