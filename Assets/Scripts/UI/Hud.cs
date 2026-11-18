using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// HUD da run: vida, migalhas, itens com stacks, tempo e dificuldade, mira,
/// popup ao pegar item, aviso de interação e habilidades com cooldown.
/// Some quando a run acaba (a tela de fim assume).
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class Hud : MonoBehaviour
{
    [Tooltip("Quanto tempo o popup de item fica na tela.")]
    [SerializeField, Min(0.5f)] float popupDuration = 3f;
    [Tooltip("Abaixo dessa fração a barra de vida fica vermelha.")]
    [SerializeField, Range(0f, 1f)] float lowHealth = 0.3f;

    VisualElement hud, healthFill, items, crosshair, popup, abilitiesRow;
    Label healthText, crumbs, timer, difficulty, popupName, popupDesc, prompt, stage, objectiveText;
    VisualElement objective, objectiveBar, objectiveFill, boss, bossFill;
    Label bossName;
    VisualElement difficultyFill;

    Health health;
    PlayerWallet wallet;
    Inventory inventory;
    Interactor interactor;
    readonly List<(IHudAbility ability, VisualElement overlay, Label text)> abilities = new();
    float popupUntil;

    public bool IsVisible => hud != null && hud.style.display != DisplayStyle.None;
    public string PromptText => prompt != null && !prompt.ClassListContains("hidden") ? prompt.text : null;
    public bool PopupVisible => popup != null && !popup.ClassListContains("popup--hidden");

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        hud = root.Q("hud");
        healthFill = root.Q("health-fill");
        healthText = root.Q<Label>("health-text");
        crumbs = root.Q<Label>("crumbs");
        items = root.Q("items");
        timer = root.Q<Label>("timer");
        difficulty = root.Q<Label>("difficulty");
        difficultyFill = root.Q("difficulty-fill");
        crosshair = root.Q("crosshair");
        popup = root.Q("popup");
        popupName = root.Q<Label>("popup-name");
        popupDesc = root.Q<Label>("popup-desc");
        prompt = root.Q<Label>("prompt");
        abilitiesRow = root.Q("abilities");
        stage = root.Q<Label>("stage");
        objective = root.Q("objective");
        objectiveText = root.Q<Label>("objective-text");
        objectiveBar = root.Q("objective-bar");
        objectiveFill = root.Q("objective-fill");
        boss = root.Q("boss");
        bossFill = root.Q("boss-fill");
        bossName = root.Q<Label>("boss-name");
    }

    void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            health = player.GetComponent<Health>();
            wallet = player.GetComponent<PlayerWallet>();
            inventory = player.GetComponent<Inventory>();
            interactor = player.GetComponent<Interactor>();
            BuildAbilities(player);
        }
        if (inventory != null)
        {
            inventory.OnItemAdded += ShowPopup;
            inventory.OnInventoryChanged += RebuildItems;
        }
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunEnded += Hide;
            RunManager.Instance.OnStageCompleted += Hide;
        }
        RebuildItems();
    }

    void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnItemAdded -= ShowPopup;
            inventory.OnInventoryChanged -= RebuildItems;
        }
        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunEnded -= Hide;
            RunManager.Instance.OnStageCompleted -= Hide;
        }
    }

    void Hide() => hud.style.display = DisplayStyle.None;

    void Update()
    {
        if (!IsVisible) return;

        if (health != null)
        {
            float frac = Mathf.Clamp01(health.Current / health.Max);
            healthFill.style.width = Length.Percent(frac * 100f);
            healthFill.EnableInClassList("health-fill--low", frac < lowHealth);
            healthText.text = $"{Mathf.CeilToInt(health.Current)} / {Mathf.RoundToInt(health.Max)}";
        }
        if (wallet != null) crumbs.text = wallet.Crumbs.ToString();

        if (RunManager.Instance != null)
        {
            int s = Mathf.FloorToInt(RunManager.Instance.RunTime);
            timer.text = $"{s / 60}:{s % 60:00}";
            stage.text = $"Fase {RunManager.Instance.Stage}";
        }
        UpdateObjective();
        UpdateBoss();
        var dm = DifficultyManager.Instance;
        if (dm != null)
        {
            difficulty.text = dm.LevelName;
            difficultyFill.style.width = Length.Percent(dm.LevelProgress * 100f);
        }

        crosshair.style.display = UnityEngine.Cursor.lockState == CursorLockMode.Locked ? DisplayStyle.Flex : DisplayStyle.None;
        if (PopupVisible && Time.unscaledTime > popupUntil) popup.AddToClassList("popup--hidden");
        UpdatePrompt();
        UpdateAbilities();
    }

    // ---------- Itens ----------

    void ShowPopup(ItemData item, int stacks)
    {
        popupName.text = item.DisplayName;
        popupName.style.color = item.Color;
        popupDesc.text = item.Description;
        popup.RemoveFromClassList("popup--hidden");
        popupUntil = Time.unscaledTime + popupDuration;
    }

    void RebuildItems()
    {
        items.Clear();
        if (inventory == null) return;
        foreach (var item in inventory.Items)
        {
            var slot = new VisualElement { pickingMode = PickingMode.Ignore };
            slot.AddToClassList("item-slot");
            Color c = item.Color;
            slot.style.borderTopColor = slot.style.borderBottomColor = slot.style.borderLeftColor = slot.style.borderRightColor = c;
            if (item.Icon != null)
            {
                slot.style.backgroundImage = new StyleBackground(item.Icon);
            }
            else
            {
                var abbr = new Label(Abbreviation(item.DisplayName)) { pickingMode = PickingMode.Ignore };
                abbr.AddToClassList("item-abbr");
                abbr.style.color = c;
                slot.Add(abbr);
            }
            int n = inventory.GetCount(item);
            if (n > 1)
            {
                var count = new Label($"x{n}") { pickingMode = PickingMode.Ignore };
                count.AddToClassList("item-count");
                slot.Add(count);
            }
            slot.tooltip = $"{item.DisplayName}: {item.Description}";
            items.Add(slot);
        }
    }

    /// <summary>"Grão de açúcar" -> "GA" (ignora "de", "da", "do").</summary>
    public static string Abbreviation(string name)
    {
        var words = name.Split(' ').Where(w => w.Length > 2 || !(w == "de" || w == "da" || w == "do")).ToArray();
        if (words.Length == 0) return "?";
        if (words.Length == 1) return words[0].Substring(0, Mathf.Min(2, words[0].Length)).ToUpperInvariant();
        return (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpperInvariant();
    }

    public int ItemSlotCount => items != null ? items.childCount : 0;
    public string ItemSlotText(int i)
    {
        if (items == null || i >= items.childCount) return null;
        return string.Join(" ", items[i].Query<Label>().ToList().Select(l => l.text));
    }

    // ---------- Objetivo ----------

    public string ObjectiveText => objectiveText != null && objectiveText.style.display != DisplayStyle.None ? objectiveText.text : null;

    void UpdateObjective()
    {
        var pipe = ExitPipe.Instance;
        if (pipe == null || pipe.Current == ExitPipe.State.Used) { objectiveText.style.display = DisplayStyle.None; objectiveBar.AddToClassList("hidden"); return; }
        objectiveText.style.display = DisplayStyle.Flex;
        objective.style.display = DisplayStyle.Flex;
        objectiveText.text = pipe.ObjectiveText;
        bool charging = pipe.Current == ExitPipe.State.Charging;
        objectiveText.EnableInClassList("objective-text--warning", charging && (!pipe.PlayerInZone || pipe.BlockersAlive > 0 && pipe.Charge >= 1f));
        objectiveBar.EnableInClassList("hidden", !charging);
        objectiveFill.style.width = Length.Percent(pipe.Charge * 100f);
    }

    // ---------- Chefe ----------

    public bool BossBarVisible => boss != null && !boss.ClassListContains("hidden");
    public float BossBarPercent => bossFill != null ? bossFill.style.width.value.value : 0f;

    void UpdateBoss()
    {
        var b = MantisBoss.Current;
        bool show = b != null && b.IsAlive;
        boss.EnableInClassList("hidden", !show);
        if (!show) return;
        bossName.text = b.IsEnraged ? $"{b.BossName} (fúria!)" : b.BossName;
        bossName.EnableInClassList("boss-name--enraged", b.IsEnraged);
        bossFill.style.width = Length.Percent(Mathf.Clamp01(b.Health.Current / b.Health.Max) * 100f);
    }

    // ---------- Interação ----------

    void UpdatePrompt()
    {
        var target = interactor != null ? interactor.Current : null;
        if (target == null) { prompt.AddToClassList("hidden"); return; }
        prompt.text = $"[E] {target.GetPrompt(interactor, out bool available)}";
        prompt.EnableInClassList("prompt--unavailable", !available);
        prompt.RemoveFromClassList("hidden");
    }

    // ---------- Habilidades ----------

    void BuildAbilities(GameObject player)
    {
        abilitiesRow.Clear();
        abilities.Clear();
        foreach (var a in player.GetComponents<IHudAbility>().OrderBy(a => a.HudSlot))
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList("ability");
            box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor = a.HudColor;
            var name = new Label(a.HudName) { pickingMode = PickingMode.Ignore };
            name.AddToClassList("ability-name");
            var key = new Label(a.HudKey) { pickingMode = PickingMode.Ignore };
            key.AddToClassList("ability-key");
            var overlay = new VisualElement { pickingMode = PickingMode.Ignore };
            overlay.AddToClassList("ability-cooldown");
            var text = new Label { pickingMode = PickingMode.Ignore };
            text.AddToClassList("ability-cooldown-text");
            box.Add(name); box.Add(overlay); box.Add(text); box.Add(key);
            abilitiesRow.Add(box);
            abilities.Add((a, overlay, text));
        }
    }

    public int AbilityCount => abilities.Count;

    void UpdateAbilities()
    {
        foreach (var (a, overlay, text) in abilities)
        {
            bool show = a.CooldownDuration >= 1f && a.CooldownRemaining > 0f;
            overlay.style.height = Length.Percent(show ? a.CooldownRemaining / a.CooldownDuration * 100f : 0f);
            text.text = show ? Mathf.CeilToInt(a.CooldownRemaining).ToString() : "";
        }
    }
}
