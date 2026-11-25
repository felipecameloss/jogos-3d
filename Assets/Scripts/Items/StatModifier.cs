using System;
using UnityEngine;

/// <summary>Stats que os itens podem mudar.</summary>
public enum StatType
{
    /// <summary>Dano das armas. Só percentual.</summary>
    Damage,
    /// <summary>Velocidade de ataque. Só percentual.</summary>
    AttackSpeed,
    /// <summary>Velocidade de andar/correr. Só percentual.</summary>
    MoveSpeed,
    /// <summary>Altura do pulo. Só percentual.</summary>
    JumpHeight,
    /// <summary>Pulos extras no ar. Só fixo (1 = pulo duplo).</summary>
    ExtraJumps,
    /// <summary>Vida máxima. Fixo e/ou percentual.</summary>
    MaxHealth,
    /// <summary>Armadura (100 = metade do dano). Só fixo.</summary>
    Armor,
    /// <summary>Vida por segundo. Só fixo.</summary>
    Regen,
    /// <summary>Raio em que as migalhas voam até o player. Só percentual.</summary>
    CrumbAttractRadius,
    /// <summary>Escudo máximo (absorve dano antes da vida). Só fixo.</summary>
    MaxShield,
    /// <summary>Redução fixa de dano por golpe (mínimo 1). Só fixo.</summary>
    DamageReduction,
    /// <summary>Segundos flutuando numa bolha a cada vez no ar, segurando pulo (bolha de sabão). Só fixo.</summary>
    FloatTime,
}

public enum ModifierMode
{
    /// <summary>Soma direto (ex.: +25 de vida, +1 pulo).</summary>
    Fixo,
    /// <summary>Soma porcentagem (0.15 = +15%). Negativo diminui.</summary>
    Percentual,
}

/// <summary>Um bônus de stat por stack do item.</summary>
[Serializable]
public struct StatModifier
{
    public StatType stat;
    public ModifierMode mode;
    [Tooltip("Por stack. Percentual: 0.15 = +15%.")]
    public float value;

    public StatModifier(StatType stat, ModifierMode mode, float value)
    {
        this.stat = stat;
        this.mode = mode;
        this.value = value;
    }

    public override string ToString() =>
        mode == ModifierMode.Percentual ? $"{(value >= 0 ? "+" : "")}{value * 100f:0.#}% {stat}" : $"{(value >= 0 ? "+" : "")}{value:0.##} {stat}";
}
