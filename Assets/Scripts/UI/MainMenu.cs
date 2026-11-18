using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>Menu principal: Jogar, Controles, Sair. Enter também começa.</summary>
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
        root.Q<Button>("play").Focus();
    }

    void ToggleControls() => controls.ToggleInClassList("hidden");

    public bool ControlsVisible => controls != null && !controls.ClassListContains("hidden");

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) SceneLoader.LoadGame();
    }
}
