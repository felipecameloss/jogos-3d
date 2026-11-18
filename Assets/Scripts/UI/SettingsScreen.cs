using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Tela de configurações (abre do menu principal e do pause). Cada mudança salva na hora.
/// Esc ou Voltar fecha e volta para a tela de antes.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class SettingsScreen : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    static int closedFrame = -1;
    /// <summary>Aberta ou fechada neste frame (o Esc que fechou não pode também despausar).</summary>
    public static bool IsOpenOrJustClosed => IsOpen || closedFrame == Time.frameCount;

    VisualElement doc, root;
    Slider sensitivity, fov, shake, master, music, sfx;
    Toggle invertY, fullscreen, damageNumbers;
    System.Action onClose;
    bool binding;

    void OnEnable()
    {
        doc = GetComponent<UIDocument>().rootVisualElement;
        root = doc.Q("root");
        sensitivity = doc.Q<Slider>("sensitivity");
        fov = doc.Q<Slider>("fov");
        shake = doc.Q<Slider>("shake");
        master = doc.Q<Slider>("master");
        music = doc.Q<Slider>("music");
        sfx = doc.Q<Slider>("sfx");
        invertY = doc.Q<Toggle>("invert-y");
        fullscreen = doc.Q<Toggle>("fullscreen");
        damageNumbers = doc.Q<Toggle>("damage-numbers");

        sensitivity.RegisterValueChangedCallback(e => Set(() => GameSettings.Sensitivity = e.newValue));
        fov.RegisterValueChangedCallback(e => Set(() => GameSettings.FieldOfView = Mathf.Round(e.newValue)));
        shake.RegisterValueChangedCallback(e => Set(() => GameSettings.ScreenShake = e.newValue));
        master.RegisterValueChangedCallback(e => Set(() => GameSettings.MasterVolume = e.newValue));
        music.RegisterValueChangedCallback(e => Set(() => GameSettings.MusicVolume = e.newValue));
        sfx.RegisterValueChangedCallback(e => Set(() => GameSettings.SfxVolume = e.newValue));
        invertY.RegisterValueChangedCallback(e => Set(() => GameSettings.InvertY = e.newValue));
        fullscreen.RegisterValueChangedCallback(e => Set(() => GameSettings.Fullscreen = e.newValue));
        damageNumbers.RegisterValueChangedCallback(e => Set(() => GameSettings.DamageNumbers = e.newValue));

        doc.Q<Button>("reset").clicked += () => { GameSettings.ResetDefaults(); Refresh(); };
        doc.Q<Button>("back").clicked += Close;
        root.AddToClassList("hidden");
        IsOpen = false;
    }

    void OnDestroy() => IsOpen = false;

    void Set(System.Action change)
    {
        if (binding) return; // mudança vinda do Refresh, não do jogador
        change();
        GameSettings.Save();
        UpdateLabels();
    }

    /// <summary>Abre por cima da tela atual; `onClose` roda ao fechar (volta o foco para o menu de antes).</summary>
    public void Open(System.Action onClose = null)
    {
        this.onClose = onClose;
        Refresh();
        root.RemoveFromClassList("hidden");
        IsOpen = true;
        doc.Q<Button>("back").Focus();
    }

    public void Close()
    {
        if (!IsOpen) return;
        root.AddToClassList("hidden");
        IsOpen = false;
        closedFrame = Time.frameCount;
        onClose?.Invoke();
    }

    void Refresh()
    {
        binding = true;
        sensitivity.value = GameSettings.Sensitivity;
        fov.value = GameSettings.FieldOfView;
        shake.value = GameSettings.ScreenShake;
        master.value = GameSettings.MasterVolume;
        music.value = GameSettings.MusicVolume;
        sfx.value = GameSettings.SfxVolume;
        invertY.value = GameSettings.InvertY;
        fullscreen.value = GameSettings.Fullscreen;
        damageNumbers.value = GameSettings.DamageNumbers;
        binding = false;
        UpdateLabels();
    }

    void UpdateLabels()
    {
        doc.Q<Label>("sensitivity-value").text = GameSettings.Sensitivity.ToString("0.0");
        doc.Q<Label>("fov-value").text = $"{GameSettings.FieldOfView:0}°";
        doc.Q<Label>("shake-value").text = $"{GameSettings.ScreenShake * 100f:0}%";
        doc.Q<Label>("master-value").text = $"{GameSettings.MasterVolume * 100f:0}%";
        doc.Q<Label>("music-value").text = $"{GameSettings.MusicVolume * 100f:0}%";
        doc.Q<Label>("sfx-value").text = $"{GameSettings.SfxVolume * 100f:0}%";
    }

    void Update()
    {
        if (!IsOpen) return;
        bool back = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        if (back) Close();
    }
}
