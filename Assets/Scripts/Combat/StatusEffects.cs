using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Efeitos de status num alvo: lentidão e fogo (dano ao longo do tempo).
/// Adicionado sob demanda com StatusEffects.On(alvo). Limpa tudo ao desativar
/// (inimigo voltando para o pool). Dano de status é NoProc e ignora armadura.
/// </summary>
[DisallowMultipleComponent]
public class StatusEffects : MonoBehaviour
{
    const float BurnTick = 0.5f;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly Color SlowTint = new(1f, 0.45f, 0.75f);   // rosa chiclete
    static readonly Color BurnGlow = new(1f, 0.45f, 0.08f);
    static readonly Color PoisonGlow = new(0.28f, 0.95f, 0.25f);

    struct Burn { public float dps; public float until; public GameObject source; public bool isPoison; }

    public bool IsSlowed => Time.time < slowUntil;
    public float SlowAmount => IsSlowed ? slowAmount : 0f;
    public bool IsBurning { get { foreach (var b in burns) if (!b.isPoison) return true; return false; } }
    public bool IsPoisoned { get { foreach (var b in burns) if (b.isPoison) return true; return false; } }
    /// <summary>Dano por segundo somado de todos os fogos/venenos ativos.</summary>
    public float BurnDps { get { float d = 0f; foreach (var b in burns) d += b.dps; return d; } }

    Health health;
    EnemyBase enemy;
    float slowAmount, slowUntil;
    readonly List<Burn> burns = new();
    float nextBurnTick;
    Renderer[] renderers;
    Color[] baseColors;
    MaterialPropertyBlock block;
    bool tinted;

    /// <summary>Pega (ou cria) o componente no alvo.</summary>
    public static StatusEffects On(Component target)
    {
        if (target == null) return null;
        return target.TryGetComponent(out StatusEffects s) ? s : target.gameObject.AddComponent<StatusEffects>();
    }

    void Awake()
    {
        health = GetComponent<Health>();
        enemy = GetComponent<EnemyBase>();
        block = new MaterialPropertyBlock();
        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
        }
    }

    /// <summary>Lentidão: 0.3 = 30% mais lento. A mais forte ativa vale; a duração renova.</summary>
    public void ApplySlow(float amount, float duration)
    {
        amount = Mathf.Clamp01(amount);
        if (!IsSlowed || amount >= slowAmount) slowAmount = amount;
        slowUntil = Mathf.Max(slowUntil, Time.time + duration);
    }

    /// <summary>Fogo ou veneno: `totalDamage` espalhado em `duration`. Vários efeitos somam.</summary>
    public void ApplyBurn(float totalDamage, float duration, GameObject source, bool isPoison = false)
    {
        if (totalDamage <= 0f || duration <= 0f || (health != null && !health.IsAlive)) return;
        if (burns.Count == 0) nextBurnTick = Time.time + BurnTick;
        burns.Add(new Burn { dps = totalDamage / duration, until = Time.time + duration, source = source, isPoison = isPoison });
    }

    public void ApplyPoison(float totalDamage, float duration, GameObject source) =>
        ApplyBurn(totalDamage, duration, source, isPoison: true);

    void Update()
    {
        if (enemy != null) enemy.StatusSpeedMultiplier = 1f - SlowAmount;

        if (burns.Count > 0 && Time.time >= nextBurnTick)
        {
            nextBurnTick += BurnTick;
            for (int i = burns.Count - 1; i >= 0; i--)
            {
                var b = burns[i];
                if (health != null && health.IsAlive)
                    health.TakeDamage(new DamageInfo(b.dps * BurnTick, b.source, transform.position + Vector3.up, Vector3.zero) { NoProc = true, IgnoreArmor = true });
                if (Time.time >= b.until - 0.001f) burns.RemoveAt(i);
            }
            if (health != null && !health.IsAlive) burns.Clear();
        }

        UpdateTint();
    }

    void UpdateTint()
    {
        bool slowed = IsSlowed, burning = IsBurning, poisoned = IsPoisoned;
        bool hasGlow = burning || poisoned;
        if (!slowed && !hasGlow && !tinted) return;
        float flicker = 0.6f + 0.4f * Mathf.Sin(Time.time * 18f);
        Color glow = burning && poisoned ? Color.Lerp(BurnGlow, PoisonGlow, 0.5f) : poisoned ? PoisonGlow : BurnGlow;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, slowed ? Color.Lerp(baseColors[i], SlowTint, 0.45f) : baseColors[i]);
            block.SetColor(EmissionId, hasGlow ? glow * (1.3f * flicker) : Color.black);
            renderers[i].SetPropertyBlock(block);
        }
        tinted = slowed || hasGlow;
    }

    public void ClearAll()
    {
        burns.Clear();
        slowAmount = 0f;
        slowUntil = 0f;
        if (enemy != null) enemy.StatusSpeedMultiplier = 1f;
        if (tinted) UpdateTint(); // sem efeitos ativos: volta a cor original
    }

    void OnDisable() => ClearAll();
}
