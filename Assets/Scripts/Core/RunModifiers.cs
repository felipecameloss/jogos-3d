using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Modificadores de run (desafios), escolhidos no menu principal.</summary>
[Flags]
public enum RunModifier
{
    None = 0,
    /// <summary>Inimigos andam 2x mais rápido.</summary>
    FastEnemies = 1 << 0,
    /// <summary>Baús, santuários e chefe só dão itens amaldiçoados.</summary>
    CursedItemsOnly = 1 << 1,
    /// <summary>O chefe nasce no começo de cada fase (e o cano não chama outro).</summary>
    BossFromStart = 1 << 2,
}

/// <summary>
/// Modificadores de run. <see cref="Chosen"/> é o que está marcado no menu principal (salvo no
/// PlayerPrefs); <see cref="Active"/> é o que vale na run atual: copiado do escolhido ao clicar
/// em Jogar, mantido ao passar de fase/reiniciar e zerado ao voltar para o menu. Abrir a cena
/// do jogo direto no editor (ou nos testes) começa sem modificadores.
/// </summary>
public static class RunModifiers
{
    public const string PrefsKey = "run.modifiers";
    public const float FastEnemySpeed = 2f;

    public static readonly RunModifier[] All = { RunModifier.FastEnemies, RunModifier.CursedItemsOnly, RunModifier.BossFromStart };

    static RunModifier chosen;
    static bool loaded;

    /// <summary>Valendo na run atual.</summary>
    public static RunModifier Active { get; private set; }

    // O projeto entra no Play Mode sem recarregar o domínio: começa sem modificadores e relê o salvo
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Active = RunModifier.None;
        loaded = false;
    }

    /// <summary>Marcados no menu principal (salvo ao mudar).</summary>
    public static RunModifier Chosen
    {
        get
        {
            if (!loaded)
            {
                chosen = (RunModifier)PlayerPrefs.GetInt(PrefsKey, 0) & Mask;
                loaded = true;
            }
            return chosen;
        }
        set
        {
            chosen = value & Mask;
            loaded = true;
            PlayerPrefs.SetInt(PrefsKey, (int)chosen);
            PlayerPrefs.Save();
        }
    }

    static RunModifier Mask => RunModifier.FastEnemies | RunModifier.CursedItemsOnly | RunModifier.BossFromStart;

    public static bool IsChosen(RunModifier m) => (Chosen & m) != 0;

    public static void SetChosen(RunModifier m, bool on) => Chosen = on ? Chosen | m : Chosen & ~m;

    /// <summary>Esse modificador vale na run atual.</summary>
    public static bool Is(RunModifier m) => (Active & m) != 0;

    /// <summary>Run nova (Jogar no menu): passa a valer o que está marcado.</summary>
    public static void BeginRun() => Active = Chosen;

    /// <summary>Voltou para o menu: nada vale até a próxima run.</summary>
    public static void EndRun() => Active = RunModifier.None;

    /// <summary>Liga direto na run atual (testes e depuração).</summary>
    public static void SetActive(RunModifier m) => Active = m & Mask;

    /// <summary>Multiplicador de velocidade dos inimigos na run atual.</summary>
    public static float EnemySpeedMultiplier => Is(RunModifier.FastEnemies) ? FastEnemySpeed : 1f;

    public static int Count(RunModifier set)
    {
        int n = 0;
        foreach (var m in All) if ((set & m) != 0) n++;
        return n;
    }

    /// <summary>Nome curto para o menu e a HUD.</summary>
    public static string DisplayName(RunModifier m) => m switch
    {
        RunModifier.FastEnemies => "Inimigos 2x mais rápidos",
        RunModifier.CursedItemsOnly => "Só itens amaldiçoados",
        RunModifier.BossFromStart => "Chefe desde o início de cada fase",
        _ => m.ToString(),
    };

    /// <summary>"Inimigos 2x mais rápidos · Só itens amaldiçoados" (vazio se nenhum).</summary>
    public static string Describe(RunModifier set)
    {
        var names = new List<string>();
        foreach (var m in All) if ((set & m) != 0) names.Add(DisplayName(m));
        return string.Join(" · ", names);
    }
}
