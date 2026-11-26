using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chão que deixa o player lento enquanto ele está dentro: água da poça, teia embaixo do deck.
/// A área é um cilindro em volta do transform (raio no plano; de um pouco abaixo da base até
/// <c>height</c> acima). Só o player sente: o <see cref="PlayerMovement"/> pergunta
/// <see cref="FactorAt"/> a cada frame, e a lentidão passa aos poucos depois de sair.
/// Em cima de uma folha boiando ou dentro da bolha de sabão, o player fica acima da água.
/// </summary>
public class SlowZone : MonoBehaviour
{
    public enum Kind { Water, Web }

    [SerializeField] Kind kind = Kind.Water;
    [SerializeField, Min(0.1f)] float radius = 3f;
    [Tooltip("Altura da zona acima do transform (água: a profundidade; teia: rente ao chão).")]
    [SerializeField, Min(0.05f)] float height = 0.6f;
    [Tooltip("Velocidade do player dentro (0.5 = metade).")]
    [SerializeField, Range(0.05f, 1f)] float speedFactor = 0.5f;

    /// <summary>Zonas ligadas na cena.</summary>
    public static readonly List<SlowZone> Active = new();

    public Kind ZoneKind => kind;
    public float Radius => radius * transform.lossyScale.x;
    public float SpeedFactor => speedFactor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Active.Clear();

    void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    void OnDisable() => Active.Remove(this);

    /// <summary>Os pés estão dentro da zona.</summary>
    public bool Contains(Vector3 feet)
    {
        Vector3 d = feet - transform.position;
        if (d.y < -0.6f || d.y > height) return false;
        d.y = 0f;
        float r = Radius;
        return d.sqrMagnitude <= r * r;
    }

    /// <summary>Velocidade (0..1) nos pés: a menor das zonas em que estão; 1 fora de todas.</summary>
    public static float FactorAt(Vector3 feet)
    {
        float f = 1f;
        foreach (var z in Active)
            if (z != null && z.speedFactor < f && z.Contains(feet)) f = z.speedFactor;
        return f;
    }

    /// <summary>Os pés estão dentro de alguma zona desse tipo (água, teia).</summary>
    public static bool IsIn(Vector3 feet, Kind kind)
    {
        foreach (var z in Active)
            if (z != null && z.kind == kind && z.Contains(feet)) return true;
        return false;
    }

    public void Configure(Kind k, float r, float h, float factor)
    {
        kind = k; radius = r; height = h; speedFactor = factor;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = kind == Kind.Water ? new Color(0.3f, 0.6f, 1f) : Color.white;
        Gizmos.DrawWireSphere(transform.position, Radius);
    }
}
