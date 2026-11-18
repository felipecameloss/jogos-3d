using UnityEngine;

/// <summary>
/// Chance de pôr fogo ao acertar (Cabeça de fósforo). Serve também para veneno
/// (Ferrão de abelha): é o mesmo dano ao longo do tempo com outra cor/nome.
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Itens/Fogo ao acertar", fileName = "NovoItemFogo")]
public class BurnOnHitItem : ItemData
{
    [Tooltip("Chance por stack (0.1 = 10%). Stacks somam sem passar de 100%.")]
    [SerializeField, Range(0f, 1f)] float chancePerStack = 0.1f;
    [Tooltip("Dano total do fogo em múltiplos do dano do player (2 = 200%).")]
    [SerializeField, Min(0f)] float damageCoefficient = 2f;
    [SerializeField, Min(0.1f)] float duration = 3f;

    public override void OnHit(Inventory inventory, int stacks, Health target, DamageInfo info)
    {
        if (!Inventory.Roll(chancePerStack, stacks)) return;
        float playerDamage = inventory.Stats != null ? inventory.Stats.Damage : info.Amount;
        StatusEffects.On(target).ApplyBurn(playerDamage * damageCoefficient, duration, inventory.gameObject);
    }
}
