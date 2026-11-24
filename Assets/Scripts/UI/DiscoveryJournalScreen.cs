using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Tela do Diário de descobertas (menu principal): itens e inimigos do catálogo em cards.
/// Descoberto mostra ícone, nome e descrição; ainda oculto mostra a silhueta (o ícone pintado
/// de preto) e "???". Esc ou Voltar fecha e volta para o menu.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class DiscoveryJournalScreen : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    static int closedFrame = -1;
    /// <summary>Aberta ou fechada neste frame (o Enter/Esc que fechou não pode também agir no menu).</summary>
    public static bool IsOpenOrJustClosed => IsOpen || closedFrame == Time.frameCount;

    static readonly Color SilhouetteTint = new(0.02f, 0.03f, 0.02f, 0.92f);

    [SerializeField] DiscoveryCatalog catalog;

    VisualElement doc, root, itemGrid, enemyGrid;
    Label itemCount, enemyCount, detailName, detailTag, detailDesc;
    ScrollView scroll;
    Action onClose;

    /// <summary>Cards na tela (itens + inimigos), para testes.</summary>
    public int CardCount => (itemGrid?.childCount ?? 0) + (enemyGrid?.childCount ?? 0);
    /// <summary>Texto do contador da seção ("3/22"), para testes.</summary>
    public string ItemCountText => itemCount?.text;
    public string EnemyCountText => enemyCount?.text;
    /// <summary>Nome mostrado no card (o "???" dos ocultos), para testes.</summary>
    public string ItemCardName(int index) => itemGrid[index].Q<Label>(className: "journal-card__name").text;
    public string EnemyCardName(int index) => enemyGrid[index].Q<Label>(className: "journal-card__name").text;
    public bool IsItemCardHidden(int index) => itemGrid[index].ClassListContains("journal-card--hidden");
    public bool IsEnemyCardHidden(int index) => enemyGrid[index].ClassListContains("journal-card--hidden");

    void OnEnable()
    {
        doc = GetComponent<UIDocument>().rootVisualElement;
        root = doc.Q("root");
        scroll = doc.Q<ScrollView>("scroll");
        itemGrid = doc.Q("item-grid");
        enemyGrid = doc.Q("enemy-grid");
        itemCount = doc.Q<Label>("item-count");
        enemyCount = doc.Q<Label>("enemy-count");
        detailName = doc.Q<Label>("detail-name");
        detailTag = doc.Q<Label>("detail-tag");
        detailDesc = doc.Q<Label>("detail-desc");
        doc.Q<Button>("back").clicked += Close;
        root.AddToClassList("hidden");
        IsOpen = false;
    }

    void OnDestroy() => IsOpen = false;

    /// <summary>Abre por cima do menu; `onClose` roda ao fechar (volta o foco para o menu).</summary>
    public void Open(Action onClose = null)
    {
        this.onClose = onClose;
        Rebuild();
        root.RemoveFromClassList("hidden");
        IsOpen = true;
        scroll.scrollOffset = Vector2.zero;
        (itemGrid.childCount > 0 ? itemGrid[0] : doc.Q<Button>("back")).Focus();
    }

    public void Close()
    {
        if (!IsOpen) return;
        root.AddToClassList("hidden");
        IsOpen = false;
        closedFrame = Time.frameCount;
        onClose?.Invoke();
    }

    void Update()
    {
        if (!IsOpen) return;
        bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        if (back) Close();
    }

    // ---------- Cards ----------

    void Rebuild()
    {
        itemGrid.Clear();
        enemyGrid.Clear();
        int found = 0, total = 0;
        if (catalog != null)
        {
            foreach (var item in catalog.Items)
            {
                if (item == null) continue;
                bool known = DiscoveryJournal.IsItemDiscovered(item);
                total++;
                if (known) found++;
                AddCard(itemGrid, item.Icon, known, known ? item.DisplayName : "???", known ? item.Color : Color.clear,
                    () => ShowItem(item, known));
            }
        }
        itemCount.text = $"{found}/{total}";

        found = total = 0;
        if (catalog != null)
        {
            foreach (var enemy in catalog.Enemies)
            {
                if (enemy == null || string.IsNullOrEmpty(enemy.id)) continue;
                bool known = DiscoveryJournal.IsEnemyDiscovered(enemy.id);
                total++;
                if (known) found++;
                AddCard(enemyGrid, enemy.icon, known, known ? enemy.displayName : "???",
                    known && enemy.isBoss ? new Color(0.97f, 0.32f, 0.24f) : Color.clear,
                    () => ShowEnemy(enemy, known));
            }
        }
        enemyCount.text = $"{found}/{total}";

        ShowDetails("", "", "Passe o mouse ou navegue pelos cards para ver os detalhes.", Color.white);
    }

    void AddCard(VisualElement grid, Sprite icon, bool known, string label, Color accent, Action show)
    {
        var card = new Button();
        card.AddToClassList("journal-card");
        if (!known) card.AddToClassList("journal-card--hidden");

        var image = new VisualElement();
        image.AddToClassList("journal-card__icon");
        if (icon != null)
        {
            image.style.backgroundImage = new StyleBackground(icon);
            if (!known) image.style.unityBackgroundImageTintColor = SilhouetteTint;
        }
        else
        {
            var unknown = new Label("?");
            unknown.AddToClassList("journal-card__unknown");
            image.Add(unknown);
        }
        card.Add(image);

        var name = new Label(label);
        name.AddToClassList("journal-card__name");
        card.Add(name);

        // Faixa de baixo na cor da raridade (itens) ou vermelha (chefe)
        if (accent.a > 0f) card.style.borderBottomColor = accent;

        card.RegisterCallback<FocusInEvent>(_ => show());
        card.RegisterCallback<PointerEnterEvent>(_ => show());
        card.clicked += show;
        grid.Add(card);
    }

    void ShowItem(ItemData item, bool known)
    {
        if (known)
            ShowDetails(item.DisplayName, $"Item · {item.Rarity.DisplayName()}", item.Description, item.Color);
        else
            ShowDetails("???", "Item ainda não descoberto", "Pegue este item numa run para registrá-lo no diário.", Color.white);
    }

    void ShowEnemy(DiscoveryCatalog.EnemyEntry enemy, bool known)
    {
        string kind = enemy.isBoss ? "Chefe" : "Inimigo";
        if (known)
            ShowDetails(enemy.displayName, kind, enemy.description, Color.white);
        else
            ShowDetails("???", $"{kind} ainda não visto", "Encontre este inimigo numa run para registrá-lo no diário.", Color.white);
    }

    void ShowDetails(string name, string tag, string description, Color nameColor)
    {
        detailName.text = name;
        detailName.style.color = nameColor;
        detailTag.text = tag;
        detailDesc.text = description;
    }
}
