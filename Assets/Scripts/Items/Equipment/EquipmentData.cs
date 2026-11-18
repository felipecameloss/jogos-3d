using UnityEngine;

/// <summary>
/// Equipamento ativo (como o Equipment do Risk of Rain): o player carrega um só,
/// usa com Q e espera o cooldown. Sai dos baús como um item comum (raridade
/// Equipamento), mas vai para o EquipmentSlot em vez de empilhar no Inventory.
/// Para criar um novo: herde desta classe e implemente Activate.
/// </summary>
public abstract class EquipmentData : ItemData
{
    [Tooltip("Segundos entre usos.")]
    [SerializeField, Min(0.5f)] float cooldown = 45f;

    public float Cooldown => cooldown;

    /// <summary>Faz o efeito. Retorne false para não gastar o cooldown (ex.: vida cheia).</summary>
    public abstract bool Activate(EquipmentSlot slot);
}
