using UnityEngine;

public enum ItemRarity
{
    Comum,
    Incomum,
    Lendario,
    /// <summary>Bônus forte com penalidade (café derramado, cola branca).</summary>
    Amaldicoado,
}

public static class ItemRarityExtensions
{
    /// <summary>Cor da raridade (pickup, HUD, popup).</summary>
    public static Color Color(this ItemRarity r) => r switch
    {
        ItemRarity.Comum => new Color(0.92f, 0.92f, 0.9f),
        ItemRarity.Incomum => new Color(0.45f, 0.86f, 0.35f),
        ItemRarity.Lendario => new Color(0.97f, 0.32f, 0.24f),
        ItemRarity.Amaldicoado => new Color(0.55f, 0.62f, 1f),
        _ => UnityEngine.Color.white,
    };

    public static string DisplayName(this ItemRarity r) => r switch
    {
        ItemRarity.Comum => "Comum",
        ItemRarity.Incomum => "Incomum",
        ItemRarity.Lendario => "Lendário",
        ItemRarity.Amaldicoado => "Amaldiçoado",
        _ => r.ToString(),
    };
}
