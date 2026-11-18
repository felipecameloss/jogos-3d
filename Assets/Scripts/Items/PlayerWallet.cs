using System;
using UnityEngine;

/// <summary>Migalhas (moeda) do player. Baús, santuários e joaninhas gastam daqui.</summary>
public class PlayerWallet : MonoBehaviour
{
    [SerializeField, Min(0)] int startingCrumbs = 0;

    /// <summary>(total, quanto mudou) — positivo ao ganhar, negativo ao gastar.</summary>
    public event Action<int, int> OnCrumbsChanged;

    public int Crumbs { get; private set; }
    /// <summary>Total juntado na run inteira (tela de fim de run).</summary>
    public int TotalCollected { get; private set; }

    void Awake()
    {
        Crumbs = startingCrumbs;
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        Crumbs += amount;
        TotalCollected += amount;
        OnCrumbsChanged?.Invoke(Crumbs, amount);
    }

    /// <summary>Restaura ao passar de fase.</summary>
    public void RestoreState(int crumbs, int totalCollected)
    {
        Crumbs = Mathf.Max(0, crumbs);
        TotalCollected = Mathf.Max(0, totalCollected);
        OnCrumbsChanged?.Invoke(Crumbs, 0);
    }

    public bool CanAfford(int cost) => cost <= Crumbs;

    /// <summary>Gasta se tiver o bastante. Retorna se conseguiu.</summary>
    public bool TrySpend(int cost)
    {
        if (cost < 0 || cost > Crumbs) return false;
        if (cost == 0) return true;
        Crumbs -= cost;
        OnCrumbsChanged?.Invoke(Crumbs, -cost);
        return true;
    }

}
