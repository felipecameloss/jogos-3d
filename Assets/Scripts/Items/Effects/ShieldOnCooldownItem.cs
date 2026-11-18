using UnityEngine;

/// <summary>
/// Escudo que recarrega após ficar um tempo sem levar dano (Tampa de caneta).
/// O bônus de escudo máximo vem de modifiers (StatType.MaxShield).
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Itens/Escudo recarregável", fileName = "NovoItemEscudo")]
public class ShieldOnCooldownItem : ItemData
{
    [Tooltip("Segundos sem levar dano até o escudo começar a recarregar.")]
    [SerializeField, Min(0.5f)] float rechargeDelay = 6f;
    [Tooltip("Fração do escudo máximo recarregada por segundo (0.5 = enche em 2s).")]
    [SerializeField, Min(0.05f)] float rechargeFractionPerSecond = 0.5f;

    public override void OnSecond(Inventory inventory, int stacks)
    {
        var health = inventory.Health;
        if (health == null || health.MaxShield <= 0f || health.Shield >= health.MaxShield) return;
        if (Time.time - health.LastDamagedTime < rechargeDelay) return;

        health.RechargeShield(health.MaxShield * rechargeFractionPerSecond);
    }
}
