using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Menu principal: Jogar, Configurações, Diário de descobertas, Modificadores de run, Controles, Sair.
/// Jogar (ou Enter) abre a tela "Nova run" com a escolha do personagem (PlayerCharacters);
/// Começar run aplica o personagem e carrega o jogo. Os modificadores marcados no painel
/// "Modificadores de run" valem na run que começar (SceneLoader.LoadGame).
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenu : MonoBehaviour
{
    VisualElement root, mainPanel, newRun, controls, modifiers, characterRow;
    Button play;
    Label newRunModifiers;
    readonly List<(PlayerCharacterId id, Button card)> cards = new();
    PlayerCharacterId selected;
    int newRunOpenedFrame = -10;
    bool starting;

    public bool ControlsVisible => controls != null && !controls.ClassListContains("hidden");
    public bool ModifiersVisible => modifiers != null && !modifiers.ClassListContains("hidden");
    public bool NewRunVisible => newRun != null && !newRun.ClassListContains("hidden");
    /// <summary>Texto do botão Jogar (mostra quantos modificadores estão marcados), para testes.</summary>
    public string PlayText => play != null ? play.text : "";
    public PlayerCharacterId SelectedCharacter => selected;
    public int CharacterCardCount => cards.Count;

    void OnEnable()
    {
        Time.timeScale = 1f;
        CursorLock.SetLocked(false);
        starting = false;

        root = GetComponent<UIDocument>().rootVisualElement;
        mainPanel = root.Q("main-panel");
        newRun = root.Q("new-run");
        characterRow = root.Q("characters");
        newRunModifiers = root.Q<Label>("new-run-modifiers");
        play = root.Q<Button>("play");
        play.clicked += OpenNewRun;
        root.Q<Button>("quit").clicked += SceneLoader.Quit;
        controls = root.Q("controls");
        root.Q<Button>("controls-toggle").clicked += ToggleControls;
        modifiers = root.Q("modifiers");
        root.Q<Button>("modifiers-toggle").clicked += ToggleModifiers;
        BindModifier("mod-fast-enemies", RunModifier.FastEnemies);
        BindModifier("mod-cursed-items", RunModifier.CursedItemsOnly);
        BindModifier("mod-boss-start", RunModifier.BossFromStart);
        UpdatePlayLabel();
        root.Q<Button>("settings").clicked += () =>
        {
            var s = FindAnyObjectByType<SettingsScreen>();
            if (s != null) s.Open(() => root.Q<Button>("play").Focus());
        };
        root.Q<Button>("journal").clicked += () =>
        {
            var j = FindAnyObjectByType<DiscoveryJournalScreen>();
            if (j != null) j.Open(() => root.Q<Button>("journal").Focus());
        };

        // ---- Nova run (personagem)
        root.Q<Button>("start").clicked += StartRun;
        root.Q<Button>("back").clicked += CloseNewRun;
        BuildCharacterCards();
        Select(PlayerCharacters.LastChoice());

        play.Focus();
    }

    // Um painel aberto por vez (os dois juntos não cabem na tela)
    void ToggleControls()
    {
        controls.ToggleInClassList("hidden");
        if (ControlsVisible) modifiers?.AddToClassList("hidden");
    }

    void ToggleModifiers()
    {
        modifiers.ToggleInClassList("hidden");
        if (ModifiersVisible) controls.AddToClassList("hidden");
    }

    void BindModifier(string name, RunModifier modifier)
    {
        var toggle = root.Q<Toggle>(name);
        if (toggle == null) return;
        toggle.SetValueWithoutNotify(RunModifiers.IsChosen(modifier));
        toggle.RegisterValueChangedCallback(e =>
        {
            RunModifiers.SetChosen(modifier, e.newValue);
            UpdatePlayLabel();
        });
    }

    void UpdatePlayLabel()
    {
        int n = RunModifiers.Count(RunModifiers.Chosen);
        play.text = n == 0 ? "Jogar" : $"Jogar ({n} modificador{(n > 1 ? "es" : "")})";
    }

    // ---------- Nova run: escolha do personagem ----------

    void BuildCharacterCards()
    {
        characterRow.Clear();
        cards.Clear();
        foreach (var info in PlayerCharacters.All)
        {
            var id = info.Id;
            var card = new Button(() => Select(id)) { text = "" };
            card.AddToClassList("character-card");
            var swatch = new VisualElement();
            swatch.AddToClassList("character-swatch");
            swatch.style.backgroundColor = info.Color;
            card.Add(swatch);
            var name = new Label(info.DisplayName);
            name.AddToClassList("character-name");
            card.Add(name);
            var tagline = new Label(info.Tagline);
            tagline.AddToClassList("character-tagline");
            card.Add(tagline);
            foreach (var line in info.Kit)
            {
                var l = new Label(line);
                l.AddToClassList("character-kit");
                card.Add(l);
            }
            characterRow.Add(card);
            cards.Add((id, card));
        }
    }

    /// <summary>Marca o personagem (ainda não começa a run).</summary>
    public void Select(PlayerCharacterId id)
    {
        selected = id;
        foreach (var (cid, card) in cards) card.EnableInClassList("character-card--selected", cid == id);
    }

    public void OpenNewRun()
    {
        if (NewRunVisible) return;
        newRunOpenedFrame = Time.frameCount;
        if (newRunModifiers != null)
        {
            string chosen = RunModifiers.Describe(RunModifiers.Chosen);
            newRunModifiers.text = string.IsNullOrEmpty(chosen) ? "" : $"Modificadores: {chosen}";
            newRunModifiers.EnableInClassList("hidden", string.IsNullOrEmpty(chosen));
        }
        mainPanel.AddToClassList("hidden");
        newRun.RemoveFromClassList("hidden");
        // Foco no card marcado (não no "Começar"): o mesmo Enter que abriu não começa a run.
        // No próximo frame: elemento que acabou de sair do display: none ainda não pega foco.
        foreach (var (cid, card) in cards) if (cid == selected) card.schedule.Execute(() => card.Focus());
    }

    public void CloseNewRun()
    {
        if (!NewRunVisible) return;
        newRun.AddToClassList("hidden");
        mainPanel.RemoveFromClassList("hidden");
        play.schedule.Execute(() => play.Focus());
    }

    /// <summary>Aplica o personagem e carrega o jogo (run nova, com os modificadores marcados).</summary>
    public void StartRun()
    {
        if (starting) return;
        starting = true;
        PlayerCharacters.Select(selected);
        SceneLoader.LoadGame();
    }

    bool ToggleFocused => root?.focusController?.focusedElement is VisualElement f &&
                          (f is Toggle || f.GetFirstAncestorOfType<Toggle>() != null);

    void Update()
    {
        if (SettingsScreen.IsOpenOrJustClosed || DiscoveryJournalScreen.IsOpenOrJustClosed) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (NewRunVisible && kb.escapeKey.wasPressedThisFrame)
        {
            CloseNewRun();
            return;
        }
        if (ToggleFocused) return; // Enter num modificador marca/desmarca, não começa a run
        if (!kb.enterKey.wasPressedThisFrame && !kb.numpadEnterKey.wasPressedThisFrame) return;

        if (!NewRunVisible)
        {
            OpenNewRun();
            return;
        }
        // Enter no Voltar volta (o próprio botão cuida); no resto, começa
        if (Time.frameCount <= newRunOpenedFrame + 1) return;
        if (root.focusController?.focusedElement is Button b && b.name == "back") return;
        StartRun();
    }
}
