using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lista de todos os itens do jogo, para sortear em baús e drops.
/// Crie em Assets > Create > Jogo > Banco de Itens e arraste os itens nela.
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Banco de Itens", fileName = "ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] List<ItemData> items = new();

    public IReadOnlyList<ItemData> Items => items;

    readonly List<ItemData> buffer = new();

    /// <summary>Um item aleatório da raridade (null se não houver nenhum).</summary>
    public ItemData Random(ItemRarity rarity)
    {
        buffer.Clear();
        foreach (var i in items) if (i != null && i.Rarity == rarity) buffer.Add(i);
        return buffer.Count == 0 ? null : buffer[UnityEngine.Random.Range(0, buffer.Count)];
    }

    /// <summary>Sorteia a raridade pelos pesos (comum, incomum, lendário) e depois o item.</summary>
    public ItemData RandomWeighted(float common = 79f, float uncommon = 20f, float legendary = 1f)
    {
        float roll = UnityEngine.Random.value * (common + uncommon + legendary);
        var rarity = roll < common ? ItemRarity.Comum : roll < common + uncommon ? ItemRarity.Incomum : ItemRarity.Lendario;
        // Se a raridade sorteada não tiver itens ainda, cai para as outras
        return Random(rarity) ?? Random(ItemRarity.Comum) ?? Random(ItemRarity.Incomum) ?? Random(ItemRarity.Lendario);
    }

#if UNITY_EDITOR
    public void EditorSetItems(IEnumerable<ItemData> list) => items = new List<ItemData>(list);
#endif
}
