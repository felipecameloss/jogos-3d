using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Troca de cena e sair do jogo, num lugar só.</summary>
public static class SceneLoader
{
    public const string MainMenuScene = "MainMenu";
    public const string GameScene = "Main";

    /// <summary>Run nova do zero, com os modificadores marcados no menu.</summary>
    public static void LoadGame() { RunManager.ClearCarryOver(); RunModifiers.BeginRun(); Load(GameScene); }
    public static void LoadMainMenu() { RunManager.ClearCarryOver(); RunModifiers.EndRun(); Load(MainMenuScene); }

    static void Load(string scene)
    {
        Time.timeScale = 1f; // pause/fim de run não podem vazar para a próxima cena
        SceneManager.LoadScene(scene);
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
