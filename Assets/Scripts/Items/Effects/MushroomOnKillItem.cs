using UnityEngine;

/// <summary>
/// Chance de brotar um cogumelo onde o inimigo morreu (Esporo de cogumelo).
/// O cogumelo cura o player enquanto ele ficar dentro do círculo, por alguns segundos.
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Itens/Cogumelo ao matar", fileName = "NovoItemCogumelo")]
public class MushroomOnKillItem : ItemData
{
    [Tooltip("Chance por stack (0.2 = 20%). Stacks somam sem passar de 100%.")]
    [SerializeField, Range(0f, 1f)] float chancePerStack = 0.2f;
    [Tooltip("Cura por segundo, em fração da vida máxima, no 1º stack (0.03 = 3%/s).")]
    [SerializeField, Min(0f)] float healPerSecond = 0.03f;
    [Tooltip("Cura por segundo extra por stack a mais.")]
    [SerializeField, Min(0f)] float healPerSecondPerExtraStack = 0.015f;
    [SerializeField, Min(0.5f)] float radius = 3.5f;
    [SerializeField, Min(0f)] float radiusPerExtraStack = 0.5f;
    [Tooltip("Quanto tempo o cogumelo fica curando (segundos).")]
    [SerializeField, Min(1f)] float lifetime = 6f;
    [Tooltip("Máximo de cogumelos ao mesmo tempo: o mais velho murcha quando brota um novo.")]
    [SerializeField, Min(1)] int maxActive = 3;
    [Tooltip("Material opaco do cogumelo (as cores vêm do código).")]
    [SerializeField] Material mushroomMaterial;
    [Tooltip("Material transparente do círculo de cura no chão.")]
    [SerializeField] Material areaMaterial;

    public override void OnKill(Inventory inventory, int stacks, EnemyBase enemy, DamageInfo killingBlow)
    {
        if (enemy == null || inventory.Health == null || !Inventory.Roll(chancePerStack, stacks)) return;

        while (HealingMushroom.Active.Count >= maxActive && HealingMushroom.Active.Count > 0)
            HealingMushroom.Active[0].Wither();

        int extra = stacks - 1;
        Vector3 ground = HealingMushroom.GroundBelow(enemy.transform.position);
        HealingMushroom.Spawn(ground, inventory.Health,
            radius + radiusPerExtraStack * extra,
            lifetime,
            healPerSecond + healPerSecondPerExtraStack * extra,
            mushroomMaterial, areaMaterial);
    }
}
