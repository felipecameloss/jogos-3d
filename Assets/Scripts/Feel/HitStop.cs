using UnityEngine;

/// <summary>
/// Congela o jogo por uma fração de segundo no impacto (sensação de peso).
/// Não mexe se o jogo já estiver pausado/em menu ou acelerado (testes).
/// </summary>
public static class HitStop
{
    const float FrozenScale = 0.05f;
    static float until;
    static bool active;

    public static bool IsActive => active;

    public static void Do(float seconds)
    {
        if (seconds <= 0f || RunManager.InMenu) return;
        if (!active)
        {
            if (!Mathf.Approximately(Time.timeScale, 1f)) return; // pausado, câmera lenta, teste acelerado
            Time.timeScale = FrozenScale;
            active = true;
        }
        until = Mathf.Max(until, Time.unscaledTime + seconds);
    }

    /// <summary>Chamado todo frame (GameFeel.Update).</summary>
    public static void Tick()
    {
        if (!active || Time.unscaledTime < until) return;
        active = false;
        // Só devolve se ninguém mudou o tempo nesse meio (pause, vitória)
        if (Mathf.Approximately(Time.timeScale, FrozenScale)) Time.timeScale = 1f;
    }

    public static void Cancel()
    {
        if (active && Mathf.Approximately(Time.timeScale, FrozenScale)) Time.timeScale = 1f;
        active = false;
    }
}
