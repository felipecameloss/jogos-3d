using System.Collections.Generic;
using UnityEngine;

/// <summary>Chefe: o que a barra de vida da HUD e a câmera precisam saber.</summary>
public interface IBoss
{
    string BossName { get; }
    /// <summary>Fúria (segunda fase): a HUD mostra "(fúria!)".</summary>
    bool IsEnraged { get; }
    Health Health { get; }
    bool IsAlive { get; }
    Transform transform { get; }
}

/// <summary>
/// Chefes vivos na cena (louva-a-deus, aranha-rainha...). Cada chefe se registra ao
/// ativar e sai ao desativar; a HUD (barra de vida) e a DynamicCameraFeel (enquadramento)
/// leem daqui, então chefe novo não precisa mexer nelas.
/// </summary>
public static class BossTracker
{
    static readonly List<IBoss> bosses = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => bosses.Clear();

    public static void Register(IBoss boss)
    {
        if (boss != null && !bosses.Contains(boss)) bosses.Add(boss);
    }

    public static void Unregister(IBoss boss) => bosses.Remove(boss);

    /// <summary>O chefe vivo mais recente (null se nenhum).</summary>
    public static IBoss Current
    {
        get
        {
            for (int i = bosses.Count - 1; i >= 0; i--)
            {
                var b = bosses[i];
                // destruído sem desregistrar (cena trocou): limpa
                if (b == null || (b is Object o && o == null)) { bosses.RemoveAt(i); continue; }
                if (b.IsAlive) return b;
            }
            return null;
        }
    }
}
