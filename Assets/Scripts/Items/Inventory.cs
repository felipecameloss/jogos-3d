using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Itens do player com contagem de stacks. Sempre que muda, recalcula os stats
/// (dano, attack speed, velocidade, pulo, vida, armadura, regen...) e dispara os
/// ganchos dos itens: ao acertar, ao matar, por segundo, ao levar dano.
/// É o dono dos multiplicadores de CharacterStats/PlayerMovement/Health: não mexa
/// neles por fora, senão o próximo recálculo sobrescreve.
/// </summary>
public class Inventory : MonoBehaviour
{
    /// <summary>(item, stacks agora) quando ganha um item.</summary>
    public event Action<ItemData, int> OnItemAdded;
    /// <summary>Qualquer mudança (ganhar/perder).</summary>
    public event Action OnInventoryChanged;

    /// <summary>Itens na ordem em que foram pegos (para a HUD).</summary>
    public IReadOnlyList<ItemData> Items => order;
    public int TotalItems { get; private set; }

    public Health Health { get; private set; }
    public CharacterStats Stats { get; private set; }
    public PlayerMovement Movement { get; private set; }
    public EquipmentSlot Equipment { get; private set; }

    readonly Dictionary<ItemData, int> stacks = new();
    readonly List<ItemData> order = new();

    // Valores base (sem itens), lidos no Awake
    float baseMaxHealth, baseArmor, baseRegen, baseMaxShield, baseFlatReduction;
    float secondTimer;
    CrumbDropper crumbDropper;

    void Awake()
    {
        Health = GetComponent<Health>();
        Stats = GetComponent<CharacterStats>();
        Movement = GetComponent<PlayerMovement>();
        Equipment = GetComponent<EquipmentSlot>();
        ReadBaseStats();
    }

    void ReadBaseStats()
    {
        if (Health == null) return;
        baseMaxHealth = Health.Max;
        baseArmor = Health.Armor;
        baseRegen = Health.RegenPerSecond;
        baseMaxShield = Health.MaxShield;
        baseFlatReduction = Health.FlatDamageReduction;
    }

    /// <summary>
    /// O personagem escolhido mudou a vida/armadura base do Health depois do Awake:
    /// relê os valores base (sem itens) e recalcula. Chame antes de ganhar itens.
    /// </summary>
    public void RefreshBaseStats()
    {
        if (TotalItems > 0) Debug.LogWarning("Inventory.RefreshBaseStats com itens: a base lida já inclui os bônus deles.", this);
        ReadBaseStats();
        RecalculateStats();
    }

    void OnEnable()
    {
        Health.OnAnyDamaged += HandleAnyDamaged;
        EnemyBase.OnAnyEnemyDied += HandleEnemyDied;
        if (Health != null) Health.OnDamaged += HandleOwnerDamaged;
    }

    void OnDisable()
    {
        Health.OnAnyDamaged -= HandleAnyDamaged;
        EnemyBase.OnAnyEnemyDied -= HandleEnemyDied;
        if (Health != null) Health.OnDamaged -= HandleOwnerDamaged;
    }

    void Start()
    {
        crumbDropper = FindAnyObjectByType<CrumbDropper>();
    }

    // ---------- Inventário ----------

    public int GetCount(ItemData item) => item != null && stacks.TryGetValue(item, out int n) ? n : 0;

    /// <param name="silent">Sem popup de "pegou" (restaurar itens ao passar de fase).</param>
    public void Add(ItemData item, int count = 1, bool silent = false)
    {
        if (item == null || count <= 0) return;
        DiscoveryJournal.DiscoverItem(item);
        // Equipamento não empilha: vai para o slot do Q (troca e dropa o antigo)
        if (item is EquipmentData equipment)
        {
            if (Equipment == null) Equipment = GetComponent<EquipmentSlot>();
            if (Equipment == null) return;
            Equipment.Equip(equipment);
            if (!silent) OnItemAdded?.Invoke(item, 1);
            return;
        }
        int now = GetCount(item) + count;
        if (!stacks.ContainsKey(item)) order.Add(item);
        stacks[item] = now;
        TotalItems += count;
        RecalculateStats();
        item.OnAcquired(this, now);
        if (!silent) OnItemAdded?.Invoke(item, now);
        OnInventoryChanged?.Invoke();
    }

    public bool Remove(ItemData item, int count = 1)
    {
        int have = GetCount(item);
        if (have <= 0 || count <= 0) return false;
        count = Mathf.Min(count, have);
        if (have - count == 0) { stacks.Remove(item); order.Remove(item); }
        else stacks[item] = have - count;
        TotalItems -= count;
        RecalculateStats();
        OnInventoryChanged?.Invoke();
        return true;
    }

    // ---------- Stats ----------

    /// <summary>Soma (fixo, percentual) de um stat em todos os itens x stacks.</summary>
    public (float flat, float percent) Sum(StatType stat)
    {
        float flat = 0f, percent = 0f;
        foreach (var (item, n) in stacks)
        {
            foreach (var m in item.Modifiers)
            {
                if (m.stat != stat) continue;
                if (m.mode == ModifierMode.Fixo) flat += m.value * n;
                else percent += m.value * n;
            }
        }
        return (flat, percent);
    }

    float Multiplier(StatType stat) => Mathf.Max(0.05f, 1f + Sum(stat).percent);

    public void RecalculateStats()
    {
        if (Stats != null)
        {
            Stats.DamageMultiplier = Multiplier(StatType.Damage);
            Stats.AttackSpeedMultiplier = Multiplier(StatType.AttackSpeed);
        }
        if (Movement != null)
        {
            Movement.SpeedMultiplier = Multiplier(StatType.MoveSpeed);
            Movement.JumpHeightMultiplier = Multiplier(StatType.JumpHeight);
            Movement.ExtraJumps = Mathf.Max(0, Mathf.RoundToInt(Sum(StatType.ExtraJumps).flat));
            Movement.FloatTime = Mathf.Max(0f, Sum(StatType.FloatTime).flat);
        }
        if (Health != null)
        {
            var (hpFlat, hpPercent) = Sum(StatType.MaxHealth);
            float newMax = Mathf.Max(1f, (baseMaxHealth + hpFlat) * (1f + hpPercent));
            float oldMax = Health.Max;
            if (!Mathf.Approximately(newMax, oldMax) && Health.IsAlive)
            {
                // Ganhar vida máxima também cura o que ganhou; perder só corta o excesso
                Health.SetMaxHealth(newMax, keepRatio: false);
                if (newMax > oldMax) Health.Heal(newMax - oldMax);
            }
            Health.Armor = baseArmor + Sum(StatType.Armor).flat;
            Health.RegenPerSecond = baseRegen + Sum(StatType.Regen).flat;
            Health.SetMaxShield(baseMaxShield + Sum(StatType.MaxShield).flat);
            Health.FlatDamageReduction = baseFlatReduction + Sum(StatType.DamageReduction).flat;
        }
        if (crumbDropper == null) crumbDropper = FindAnyObjectByType<CrumbDropper>();
        if (crumbDropper != null) crumbDropper.AttractRadiusMultiplier = Multiplier(StatType.CrumbAttractRadius);
    }

    // ---------- Ganchos ----------

    bool IsMine(GameObject source) => source != null && source.transform.IsChildOf(transform);

    void ForEachItem(Action<ItemData, int> action)
    {
        // Cópia própria por chamada: um efeito pode disparar outro gancho no meio do loop
        // (raio do caco mata um inimigo -> "ao matar") ou dar/tirar item.
        var snapshot = order.ToArray();
        foreach (var item in snapshot)
        {
            int n = GetCount(item);
            if (n <= 0) continue;
            try { action(item, n); }
            catch (Exception e) { Debug.LogException(e, item); } // um item com bug não derruba os outros
        }
    }

    void HandleAnyDamaged(Health victim, DamageInfo info)
    {
        if (info.NoProc || victim == Health || !IsMine(info.Source)) return;
        ForEachItem((item, n) => item.OnHit(this, n, victim, info));
    }

    void HandleEnemyDied(EnemyBase enemy, DamageInfo blow)
    {
        if (!IsMine(blow.Source)) return;
        ForEachItem((item, n) => item.OnKill(this, n, enemy, blow));
    }

    void HandleOwnerDamaged(DamageInfo info)
    {
        ForEachItem((item, n) => item.OnOwnerDamaged(this, n, info));
    }

    void Update()
    {
        if (order.Count == 0 || (Health != null && !Health.IsAlive)) return;
        secondTimer += Time.deltaTime;
        while (secondTimer >= 1f)
        {
            secondTimer -= 1f;
            ForEachItem((item, n) => item.OnSecond(this, n));
        }
    }

    // ---------- Ajudantes para efeitos de item ----------

    readonly Dictionary<ItemData, object> itemStates = new();

    /// <summary>
    /// Estado da run de um item (cronômetro, contador...). O ItemData é um asset
    /// compartilhado, então estado fica aqui, um por item, criado na primeira chamada.
    /// </summary>
    public T GetState<T>(ItemData item) where T : class, new()
    {
        if (!itemStates.TryGetValue(item, out var s) || s is not T typed)
        {
            typed = new T();
            itemStates[item] = typed;
        }
        return typed;
    }

    /// <summary>Dano causado por um item: não dispara outros efeitos "ao acertar".</summary>
    public void DealItemDamage(IDamageable target, float amount, Vector3 point, bool ignoreArmor = false)
    {
        if (target == null || !target.IsAlive || amount <= 0f) return;
        target.TakeDamage(new DamageInfo(amount, gameObject, point, Vector3.zero) { NoProc = true, IgnoreArmor = ignoreArmor });
    }

    /// <summary>Chance que escala com stacks: 1 - (1-p)^stacks (nunca passa de 100%).</summary>
    public static bool Roll(float chancePerStack, int stacks) =>
        UnityEngine.Random.value < 1f - Mathf.Pow(1f - Mathf.Clamp01(chancePerStack), stacks);

}
