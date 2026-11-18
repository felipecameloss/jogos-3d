using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Esc (ou Start no controle) pausa: congela o tempo, solta o cursor e desliga
/// o controle do player. Não abre depois que a run acabou.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    VisualElement root;
    PlayerMovement movement;
    ThornSlingshot slingshot;

    void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement.Q("root");
        var doc = GetComponent<UIDocument>().rootVisualElement;
        doc.Q<Button>("resume").clicked += Resume;
        doc.Q<Button>("restart").clicked += () =>
        {
            Resume();
            if (RunManager.Instance != null) RunManager.Instance.RestartRun();
        };
        doc.Q<Button>("settings").clicked += () =>
        {
            var s = FindAnyObjectByType<SettingsScreen>();
            if (s != null) s.Open(() => root.Q<Button>("resume").Focus());
        };
        doc.Q<Button>("main-menu").clicked += SceneLoader.LoadMainMenu;
        doc.Q<Button>("quit").clicked += SceneLoader.Quit;
        root.AddToClassList("hidden");
    }

    void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            movement = player.GetComponent<PlayerMovement>();
            slingshot = player.GetComponent<ThornSlingshot>();
        }
    }

    void OnDestroy()
    {
        if (IsPaused) Time.timeScale = 1f;
        IsPaused = false;
    }

    // Fim de run ou vitória: o pause não abre
    bool RunOver => RunManager.Instance != null && (RunManager.Instance.IsRunOver || RunManager.Instance.IsStageComplete);

    void Update()
    {
        bool pressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                       || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
        if (!pressed || SettingsScreen.IsOpenOrJustClosed) return; // Esc das configurações é delas
        if (IsPaused) Resume();
        else if (!RunOver) Pause();
    }

    public void Pause()
    {
        if (IsPaused || RunOver) return;
        IsPaused = true;
        Time.timeScale = 0f;
        SetPlayerControl(false);
        CursorLock.SetLocked(false);
        root.RemoveFromClassList("hidden");
        root.Q<Button>("resume").Focus();
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = 1f;
        root.AddToClassList("hidden");
        if (!RunOver)
        {
            SetPlayerControl(true);
            CursorLock.SetLocked(true);
        }
    }

    void SetPlayerControl(bool on)
    {
        if (movement != null) movement.InputEnabled = on;
        if (slingshot != null) slingshot.enabled = on;
    }
}
