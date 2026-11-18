using UnityEngine;

/// <summary>
/// Habilidade que aparece na HUD com ícone e cooldown. A HUD acha sozinha
/// todos os componentes do player que implementam isso e ordena por Slot.
/// </summary>
public interface IHudAbility
{
    /// <summary>0 = primária, 1 = secundária, 2 = utilidade, 3 = especial.</summary>
    int HudSlot { get; }
    string HudName { get; }
    /// <summary>Tecla mostrada no canto (ex.: "M1", "M2", "Shift", "R").</summary>
    string HudKey { get; }
    Color HudColor { get; }
    /// <summary>Segundos até poder usar de novo (0 = pronta).</summary>
    float CooldownRemaining { get; }
    /// <summary>Cooldown cheio. Abaixo de ~1s a HUD não mostra (ex.: cadência do tiro).</summary>
    float CooldownDuration { get; }
}
