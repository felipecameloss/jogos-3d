using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>Menu principal: Jogar, Configurações, Diário de descobertas, Controles, Sair. Enter também começa.</summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenu : MonoBehaviour
{
    VisualElement controls;

    void OnEnable()
    {
        Time.timeScale = 1f;
        CursorLock.SetLocked(false);

        var root = GetComponent<UIDocument>().rootVisualElement;
        root.Q<Button>("play").clicked += SceneLoader.LoadGame;
        root.Q<Button>("quit").clicked += SceneLoader.Quit;
        controls = root.Q("controls");
        root.Q<Button>("controls-toggle").clicked += ToggleControls;
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

    void ToggleControls() => controls.ToggleInClassList("hidden");

    public bool ControlsVisible => controls != null && !controls.ClassListContains("hidden");

    void Update()
    {
        if (SettingsScreen.IsOpenOrJustClosed || DiscoveryJournalScreen.IsOpenOrJustClosed) return;
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) SceneLoader.LoadGame();
    }
}
