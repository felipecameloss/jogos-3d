using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Menu principal: Jogar, Configurações, Diário de descobertas, Modificadores de run, Controles, Sair.
/// Enter também começa. Os modificadores marcados aqui valem na run que o Jogar começar.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenu : MonoBehaviour
{
    VisualElement root, controls, modifiers;
    Button play;

    void OnEnable()
    {
        Time.timeScale = 1f;
        CursorLock.SetLocked(false);

        root = GetComponent<UIDocument>().rootVisualElement;
        play = root.Q<Button>("play");
        play.clicked += SceneLoader.LoadGame;
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
        root.Q<Button>("play").Focus();
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

    public bool ControlsVisible => controls != null && !controls.ClassListContains("hidden");
    public bool ModifiersVisible => modifiers != null && !modifiers.ClassListContains("hidden");
    /// <summary>Texto do botão Jogar (mostra quantos modificadores estão marcados), para testes.</summary>
    public string PlayText => play != null ? play.text : "";

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

    bool ToggleFocused => root?.focusController?.focusedElement is VisualElement f &&
                          (f is Toggle || f.GetFirstAncestorOfType<Toggle>() != null);

    void Update()
    {
        if (SettingsScreen.IsOpenOrJustClosed || DiscoveryJournalScreen.IsOpenOrJustClosed) return;
        if (ToggleFocused) return; // Enter num modificador marca/desmarca, não começa a run
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) SceneLoader.LoadGame();
    }
}
