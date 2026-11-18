using UnityEngine;

/// <summary>
/// Cola branca (item amaldiçoado): deixa todos os inimigos lentos continuamente,
/// enquanto os modifiers do asset reduzem a velocidade do próprio player.
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Itens/Cola branca (lentidão global)", fileName = "NovoItemColaBranca")]
public class WhiteGlueItem : ItemData
{
    [Tooltip("Lentidão nos inimigos = 1 - 1/(1 + isso x stacks). 0.55: 1 stack = 35%, 2 = 52%.")]
    [SerializeField, Min(0f)] float enemySlowPerStack = 0.55f;

    public override void OnAcquired(Inventory inventory, int newStacks) => ApplyToAll(newStacks);

    public override void OnSecond(Inventory inventory, int stacks) => ApplyToAll(stacks);

    void ApplyToAll(int stacks)
    {
        float slow = SlowOnHitItem.SlowFor(enemySlowPerStack, stacks);
        if (slow <= 0f) return;
        foreach (var enemy in EnemyBase.Active)
        {
            if (enemy != null && enemy.IsAlive)
                StatusEffects.On(enemy).ApplySlow(slow, 1.5f);
        }
    }
}
