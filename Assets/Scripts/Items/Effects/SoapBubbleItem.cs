using UnityEngine;

/// <summary>
/// Bolha de sabão (item de movimento): segurando pulo enquanto cai, o player flutua numa bolha
/// e desce devagar. O tempo de bolha por vez no ar vem dos modifiers (stat FloatTime, soma por
/// stack) e enche de novo ao pisar no chão. Dentro da bolha, ondas de choque no chão (pulo do
/// louva-a-deus, pouso da aranha), poça de ácido e teia passam por baixo; levar dano estoura a bolha.
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Itens/Bolha de sabão (flutuar)", fileName = "NovoItemBolha")]
public class SoapBubbleItem : ItemData
{
    [Tooltip("Material transparente da bolha em volta do player (a cor vem do código).")]
    [SerializeField] Material bubbleMaterial;

    public override void OnAcquired(Inventory inventory, int newStacks)
    {
        if (inventory.Movement != null) PlayerBubble.Ensure(inventory.Movement, bubbleMaterial);
    }
}
