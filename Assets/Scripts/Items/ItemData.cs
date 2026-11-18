using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Um item. Crie em Assets > Create > Jogo > Item.
/// Itens só de stat não precisam de código: é só preencher os modificadores.
/// Itens com efeito (veneno, raio, explosão...) herdam desta classe e sobrescrevem
/// os ganchos On*. O asset é compartilhado: não guarde estado da run aqui
/// (use o Inventory ou componentes no player/inimigo).
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Item", fileName = "NovoItem")]
public class ItemData : ScriptableObject
{
    [SerializeField] string displayName = "Item";
    [Tooltip("Frase curta que aparece ao pegar (ex.: \"Atira mais rápido.\").")]
    [SerializeField, TextArea(1, 3)] string description = "";
    [SerializeField] ItemRarity rarity = ItemRarity.Comum;
    [SerializeField] Sprite icon;
    [Tooltip("Bônus por stack.")]
    [SerializeField] List<StatModifier> modifiers = new();

    public string DisplayName => displayName;
    public string Description => description;
    public ItemRarity Rarity => rarity;
    public Sprite Icon => icon;
    public Color Color => rarity.Color();
    public IReadOnlyList<StatModifier> Modifiers => modifiers;

    // ---------- Ganchos de efeito (stacks = quantos o player tem) ----------

    /// <summary>Acabou de pegar (newStacks já inclui o novo).</summary>
    public virtual void OnAcquired(Inventory inventory, int newStacks) { }

    /// <summary>O dono acertou algo com dano que pode disparar efeitos (info.NoProc == false).</summary>
    public virtual void OnHit(Inventory inventory, int stacks, Health target, DamageInfo info) { }

    /// <summary>O dono matou um inimigo.</summary>
    public virtual void OnKill(Inventory inventory, int stacks, EnemyBase enemy, DamageInfo killingBlow) { }

    /// <summary>Uma vez por segundo enquanto o dono está vivo.</summary>
    public virtual void OnSecond(Inventory inventory, int stacks) { }

    /// <summary>O dono levou dano (itens de defesa: espinho de rosa, tampa de caneta).</summary>
    public virtual void OnOwnerDamaged(Inventory inventory, int stacks, DamageInfo info) { }

#if UNITY_EDITOR
    /// <summary>Para testes e ferramentas: monta um item em código.</summary>
    public void EditorSetup(string name, string desc, ItemRarity r, params StatModifier[] mods)
    {
        displayName = name; description = desc; rarity = r;
        modifiers = new List<StatModifier>(mods);
    }
#endif
}
