using UnityEngine;

/// <summary>Qualquer coisa que pode levar dano (player, inimigos, objetos quebráveis).</summary>
public interface IDamageable
{
    bool IsAlive { get; }
    void TakeDamage(DamageInfo info);
}

/// <summary>Dados de um golpe. Quem causou importa para itens como o espinho de rosa.</summary>
public struct DamageInfo
{
    /// <summary>Dano antes da armadura.</summary>
    public float Amount;
    /// <summary>Quem causou o dano (pode ser null, ex.: perigo do ambiente).</summary>
    public GameObject Source;
    /// <summary>Ponto do impacto, para números de dano e partículas.</summary>
    public Vector3 HitPoint;
    /// <summary>Direção do golpe (normalizada), para knockback e efeitos.</summary>
    public Vector3 Direction;
    /// <summary>Ignora a armadura (ex.: veneno, pisão humano).</summary>
    public bool IgnoreArmor;
    public bool IsCritical;

    public DamageInfo(float amount, GameObject source = null)
    {
        Amount = amount;
        Source = source;
        HitPoint = source != null ? source.transform.position : Vector3.zero;
        Direction = Vector3.zero;
        IgnoreArmor = false;
        IsCritical = false;
    }

    public DamageInfo(float amount, GameObject source, Vector3 hitPoint, Vector3 direction) : this(amount, source)
    {
        HitPoint = hitPoint;
        Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.zero;
    }
}
