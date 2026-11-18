using UnityEngine;

/// <summary>Acertos deixam o alvo lento (Pedaço de chiclete). Mais stacks = mais lento, sem chegar a parar.</summary>
[CreateAssetMenu(menuName = "Jogo/Itens/Lentidão ao acertar", fileName = "NovoItemLentidao")]
public class SlowOnHitItem : ItemData
{
    [Tooltip("Lentidão = 1 - 1/(1 + isso x stacks). 0.25: 1 stack = 20%, 2 = 33%, 4 = 50%.")]
    [SerializeField, Min(0f)] float slowPerStack = 0.25f;
    [SerializeField, Min(0.1f)] float duration = 2f;

    public static float SlowFor(float perStack, int stacks) => 1f - 1f / (1f + perStack * stacks);

    public override void OnHit(Inventory inventory, int stacks, Health target, DamageInfo info)
    {
        StatusEffects.On(target).ApplySlow(SlowFor(slowPerStack, stacks), duration);
    }
}
