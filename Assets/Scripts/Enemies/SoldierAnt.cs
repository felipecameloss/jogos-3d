using UnityEngine;

/// <summary>
/// Formiga-soldado: tanque lento, muita vida, mordida forte.
/// O aviso é bem visível: o corpo empina e fica vermelho até a mordida.
/// Se acertar, empurra o player. Vida, velocidade, dano e tempos vêm do prefab.
/// </summary>
public class SoldierAnt : WorkerAnt
{
    [Header("Soldado")]
    [Tooltip("Empurrão da mordida (horizontal, vertical).")]
    [SerializeField] Vector2 knockback = new(9f, 4f);
    [Tooltip("Cor que o corpo pega no fim do aviso.")]
    [SerializeField] Color telegraphColor = new(1f, 0.15f, 0.05f);

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    Renderer[] renderers;
    Color[] originalColors;
    MaterialPropertyBlock block;

    /// <summary>0..1: quão "vermelho" está agora (testes/debug).</summary>
    public float TelegraphAmount { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        block = new MaterialPropertyBlock();
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var mat = renderers[i].sharedMaterial;
            originalColors[i] = mat != null && mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
        }
    }

    protected override void OnWindUp(float progress)
    {
        // Pisca mais rápido perto da mordida
        float pulse = progress <= 0f ? 0f : progress * (0.75f + 0.25f * Mathf.Sin(progress * progress * 40f));
        SetTelegraph(pulse);
    }

    protected override void OnBiteLanded(Vector3 direction)
    {
        if (Target != null && Target.TryGetComponent(out PlayerMovement movement))
        {
            Vector3 flat = new Vector3(direction.x, 0f, direction.z).normalized;
            movement.AddKnockback(flat * knockback.x + Vector3.up * knockback.y);
        }
    }

    void OnDisable()
    {
        SetTelegraph(0f); // volta do pool com a cor normal
    }

    void SetTelegraph(float amount)
    {
        if (Mathf.Approximately(amount, TelegraphAmount) && amount > 0f) return;
        TelegraphAmount = amount;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, Color.Lerp(originalColors[i], telegraphColor, amount));
            block.SetColor(EmissionId, telegraphColor * (amount * 1.5f));
            renderers[i].SetPropertyBlock(block);
        }
    }
}
