using UnityEngine;

/// <summary>
/// Pastilha de menta: cura uma parte da vida máxima na hora, com um estouro de frescor
/// verde-menta. Com a vida cheia não faz nada e não gasta a recarga.
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Equipamentos/Pastilha de menta", fileName = "PastilhaDeMenta")]
public class MintEquipment : EquipmentData
{
    [Tooltip("Fração da vida máxima curada ao usar.")]
    [SerializeField, Range(0.05f, 1f)] float healFraction = 0.45f;
    [SerializeField] Color burstColor = new(0.55f, 1f, 0.8f, 0.6f);

    /// <summary>Quanto cura com essa vida máxima (testes/HUD).</summary>
    public float HealAmount(float maxHealth) => maxHealth * healFraction;

    public override bool Activate(EquipmentSlot slot)
    {
        var health = slot.Health;
        if (health == null || !health.IsAlive || health.IsFullHealth) return false;
        health.Heal(HealAmount(health.Max));
        // dois estouros: um rápido e largo, outro menor e mais demorado (o "frescor")
        Vector3 center = slot.transform.position + Vector3.up * 1f;
        ItemVfx.Explosion(center, 2.4f, burstColor, 0.35f);
        ItemVfx.Explosion(center, 1.3f, new Color(1f, 1f, 1f, 0.5f), 0.55f);
        return true;
    }
}
