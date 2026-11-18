using System;
using UnityEngine;

/// <summary>
/// Configurações do jogador, salvas entre sessões (PlayerPrefs).
/// Mudou algo: chame Save() (que também aplica e avisa quem escuta Changed).
/// </summary>
public static class GameSettings
{
    // Padrões
    public const float DefaultSensitivity = 2.5f;
    public const float DefaultFov = 60f;
    public const float DefaultMaster = 1f, DefaultMusic = 0.7f, DefaultSfx = 0.8f;
    public const float DefaultShake = 1f;

    public static float Sensitivity = DefaultSensitivity;   // ganho do mouse/analógico na câmera
    public static bool InvertY;
    public static float FieldOfView = DefaultFov;
    public static bool Fullscreen;
    public static float ScreenShake = DefaultShake;         // 0 = sem tremida
    public static bool DamageNumbers = true;
    public static float MasterVolume = DefaultMaster;
    public static float MusicVolume = DefaultMusic;         // usado pelo sistema de áudio
    public static float SfxVolume = DefaultSfx;             // usado pelo sistema de áudio

    /// <summary>Alguma configuração mudou (câmera, FOV, volume...).</summary>
    public static event Action Changed;

    static bool loaded;
    static bool? appliedFullscreen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        loaded = false;
        appliedFullscreen = null;
        Load();
    }

    public static void Load()
    {
        Sensitivity = PlayerPrefs.GetFloat("cfg.sensitivity", DefaultSensitivity);
        InvertY = PlayerPrefs.GetInt("cfg.invertY", 0) == 1;
        FieldOfView = PlayerPrefs.GetFloat("cfg.fov", DefaultFov);
        Fullscreen = PlayerPrefs.GetInt("cfg.fullscreen", 0) == 1;
        ScreenShake = PlayerPrefs.GetFloat("cfg.shake", DefaultShake);
        DamageNumbers = PlayerPrefs.GetInt("cfg.damageNumbers", 1) == 1;
        MasterVolume = PlayerPrefs.GetFloat("cfg.master", DefaultMaster);
        MusicVolume = PlayerPrefs.GetFloat("cfg.music", DefaultMusic);
        SfxVolume = PlayerPrefs.GetFloat("cfg.sfx", DefaultSfx);
        loaded = true;
        Apply();
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("cfg.sensitivity", Sensitivity);
        PlayerPrefs.SetInt("cfg.invertY", InvertY ? 1 : 0);
        PlayerPrefs.SetFloat("cfg.fov", FieldOfView);
        PlayerPrefs.SetInt("cfg.fullscreen", Fullscreen ? 1 : 0);
        PlayerPrefs.SetFloat("cfg.shake", ScreenShake);
        PlayerPrefs.SetInt("cfg.damageNumbers", DamageNumbers ? 1 : 0);
        PlayerPrefs.SetFloat("cfg.master", MasterVolume);
        PlayerPrefs.SetFloat("cfg.music", MusicVolume);
        PlayerPrefs.SetFloat("cfg.sfx", SfxVolume);
        PlayerPrefs.Save();
        Apply();
    }

    public static void ResetDefaults()
    {
        Sensitivity = DefaultSensitivity; InvertY = false; FieldOfView = DefaultFov; Fullscreen = false;
        ScreenShake = DefaultShake; DamageNumbers = true;
        MasterVolume = DefaultMaster; MusicVolume = DefaultMusic; SfxVolume = DefaultSfx;
        Save();
    }

    /// <summary>Aplica o que é global (volume, tela cheia) e avisa o resto.</summary>
    public static void Apply()
    {
        if (!loaded) return;
        AudioListener.volume = Mathf.Clamp01(MasterVolume);
        if (appliedFullscreen != Fullscreen && !Application.isEditor)
        {
            Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (!Fullscreen) Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
        }
        appliedFullscreen = Fullscreen;
        Changed?.Invoke();
    }
}
