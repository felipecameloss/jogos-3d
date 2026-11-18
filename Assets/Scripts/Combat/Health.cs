using System;
using UnityEngine;

/// <summary>
/// Vida reutilizável para player e inimigos.
/// Armadura no estilo Risk of Rain: dano * 100 / (100 + armadura).
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField, Min(1f)] float maxHealth = 100f;
    [Tooltip("Reduz o dano: 100 de armadura = metade do dano. Negativo aumenta o dano.")]
    [SerializeField] float armor = 0f;
    [Tooltip("Vida recuperada por segundo.")]
    [SerializeField, Min(0f)] float regenPerSecond = 0f;
    [Tooltip("Tempo invulnerável depois de levar dano (0 = nenhum).")]
    [SerializeField, Min(0f)] float invulnerabilityTime = 0f;

    /// <summary>Depois do dano aplicado. O DamageInfo traz o dano final em Amount.</summary>
    public event Action<DamageInfo> OnDamaged;
    /// <summary>Qualquer Health levou dano (quem bateu está em info.Source). Usado pelos itens "ao acertar".</summary>
    public static event Action<Health, DamageInfo> OnAnyDamaged;
    /// <summary>Uma vez só, quando a vida chega a zero.</summary>
    public event Action<DamageInfo> OnDeath;
    /// <summary>Quanto foi curado de fato.</summary>
    public event Action<float> OnHealed;
    /// <summary>(atual, máxima) sempre que algum dos dois muda. Bom para a HUD.</summary>
    public event Action<float, float> OnHealthChanged;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public float Normalized => Current / maxHealth;
    public bool IsAlive => Current > 0f;
    public bool IsFullHealth => Current >= maxHealth;
    public bool IsInvulnerable => Time.time < invulnerableUntil || Invincible;
    /// <summary>Liga/desliga imortalidade (debug, cutscene, dash com i-frames).</summary>
    [HideInInspector] public bool Invincible;

    public float Armor { get => armor; set => armor = value; }
    public float RegenPerSecond { get => regenPerSecond; set => regenPerSecond = Mathf.Max(0f, value); }

    float invulnerableUntil = float.NegativeInfinity;

    void Awake()
    {
        Current = maxHealth;
    }

    void Update()
    {
        if (regenPerSecond > 0f && IsAlive && !IsFullHealth) Heal(regenPerSecond * Time.deltaTime);
    }

    public void TakeDamage(DamageInfo info)
    {
        if (!IsAlive || IsInvulnerable || info.Amount <= 0f) return;

        float damage = info.IgnoreArmor ? info.Amount : ApplyArmor(info.Amount, armor);
        info.Amount = Mathf.Min(damage, Current); // dano que realmente saiu da vida
        Current -= info.Amount;
        if (Current < 0.0001f) Current = 0f;
        if (invulnerabilityTime > 0f) invulnerableUntil = Time.time + invulnerabilityTime;

        OnDamaged?.Invoke(info);
        OnHealthChanged?.Invoke(Current, maxHealth);
        OnAnyDamaged?.Invoke(this, info);
        if (!IsAlive) OnDeath?.Invoke(info);
    }

    /// <summary>Atalho para dano sem informação extra.</summary>
    public void TakeDamage(float amount, GameObject source = null) => TakeDamage(new DamageInfo(amount, source));

    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f) return;
        float healed = Mathf.Min(amount, maxHealth - Current);
        if (healed <= 0f) return;
        Current += healed;
        OnHealed?.Invoke(healed);
        OnHealthChanged?.Invoke(Current, maxHealth);
    }

    /// <summary>Mata na hora, ignorando armadura e invulnerabilidade.</summary>
    public void Kill(GameObject source = null)
    {
        if (!IsAlive) return;
        Invincible = false;
        invulnerableUntil = float.NegativeInfinity;
        TakeDamage(new DamageInfo(Current, source) { IgnoreArmor = true });
    }

    /// <summary>Muda a vida máxima (itens, escala de dificuldade). keepRatio mantém a porcentagem atual.</summary>
    public void SetMaxHealth(float value, bool keepRatio = true)
    {
        value = Mathf.Max(1f, value);
        float ratio = Normalized;
        maxHealth = value;
        Current = IsAlive ? (keepRatio ? Mathf.Max(ratio * maxHealth, 0.0001f) : Mathf.Min(Current, maxHealth)) : 0f;
        OnHealthChanged?.Invoke(Current, maxHealth);
    }

    /// <summary>Define a vida atual direto (passar de fase, debug). Não dispara dano/cura.</summary>
    public void SetCurrent(float value)
    {
        if (!IsAlive) return;
        Current = Mathf.Clamp(value, 0.0001f, maxHealth);
        OnHealthChanged?.Invoke(Current, maxHealth);
    }

    /// <summary>Volta à vida cheia (reaproveitar inimigo do pool, reiniciar run).</summary>
    public void ResetHealth()
    {
        Current = maxHealth;
        invulnerableUntil = float.NegativeInfinity;
        OnHealthChanged?.Invoke(Current, maxHealth);
    }

    public static float ApplyArmor(float damage, float armor)
    {
        // Negativo: 2 - 100/(100 - armadura), igual ao Risk of Rain
        float multiplier = armor >= 0f ? 100f / (100f + armor) : 2f - 100f / (100f - armor);
        return damage * multiplier;
    }

    void OnValidate()
    {
        if (Application.isPlaying && Current > maxHealth) Current = maxHealth;
    }
}
