using UnityEngine;

/// <summary>
/// Cegueira do player (clarão do vaga-lume): a tela fica branca por um instante e volta aos
/// poucos. A HUD só lê <see cref="Amount"/>. Quanto mais o player está olhando para o clarão
/// (e mais perto), mais forte; de costas ou atrás de uma parede, não cega. Virar a câmera
/// enquanto o vaga-lume carrega é a defesa.
/// </summary>
public static class PlayerBlindness
{
    /// <summary>Parte da duração em que a tela fica no máximo antes de começar a clarear.</summary>
    const float HoldFraction = 0.3f;

    static float peak, start = float.NegativeInfinity, duration;

    /// <summary>Clarões que cegaram nesta run (testes).</summary>
    public static int Count { get; private set; }
    /// <summary>Força do último clarão que cegou (0..1, testes).</summary>
    public static float LastStrength { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Clear();

    /// <summary>Tira a cegueira na hora e zera os contadores (troca de cena, testes).</summary>
    public static void Clear()
    {
        peak = 0f;
        start = float.NegativeInfinity;
        duration = 0f;
        Count = 0;
        LastStrength = 0f;
    }

    /// <summary>Quanto a tela está branca agora (0..1).</summary>
    public static float Amount
    {
        get
        {
            float t = Time.time - start;
            if (duration <= 0f || t < 0f || t >= duration) return 0f;
            float hold = duration * HoldFraction;
            if (t <= hold) return peak;
            return peak * (1f - Mathf.SmoothStep(0f, 1f, (t - hold) / (duration - hold)));
        }
    }

    /// <summary>Cega com essa força (0..1) por `seconds` segundos. Um clarão mais fraco não apaga um mais forte.</summary>
    public static void Blind(float strength, float seconds)
    {
        strength = Mathf.Clamp01(strength);
        if (strength <= 0f || seconds <= 0f) return;
        Count++;
        LastStrength = strength;
        if (strength < Amount) return;
        peak = strength;
        start = Time.time;
        duration = seconds;
    }

    /// <summary>
    /// Força de um clarão em <paramref name="source"/> para quem olha pela câmera principal:
    /// 1 olhando direto (até <paramref name="fullAngle"/> graus do centro da tela), cai até 0 em
    /// <paramref name="zeroAngle"/>; mais fraco longe; 0 fora do alcance ou atrás de parede.
    /// </summary>
    public static float StrengthFrom(Vector3 source, float range, float fullAngle = 30f, float zeroAngle = 70f, Transform ignore = null)
    {
        var cam = Camera.main;
        if (cam == null) return 0f;
        Vector3 eye = cam.transform.position;
        Vector3 to = source - eye;
        float dist = to.magnitude;
        if (dist > range || dist < 0.01f) return 0f;
        float angle = Vector3.Angle(cam.transform.forward, to);
        float facing = 1f - Mathf.InverseLerp(fullAngle, zeroAngle, angle);
        if (facing <= 0f || Blocked(eye, source, ignore)) return 0f;
        return facing * Mathf.Lerp(1f, 0.55f, dist / range);
    }

    /// <summary>Tem parede/chão entre a câmera e o clarão (player, inimigos e gatilhos não tampam).</summary>
    static bool Blocked(Vector3 eye, Vector3 source, Transform ignore)
    {
        Vector3 d = source - eye;
        foreach (var hit in Physics.RaycastAll(eye, d.normalized, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            var t = hit.collider.transform;
            if (ignore != null && t.IsChildOf(ignore)) continue;
            if (hit.collider.GetComponentInParent<EnemyBase>() != null) continue;
            if (hit.collider.GetComponentInParent<CharacterController>() != null) continue; // o próprio player
            return true;
        }
        return false;
    }
}
