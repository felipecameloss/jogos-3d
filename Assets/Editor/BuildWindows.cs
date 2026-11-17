using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Build do jogo para Windows. Menu: Jogo > Build Windows.
/// Linha de comando: Unity.exe -batchmode -quit -projectPath . -executeMethod BuildWindows.Build
/// Sai em Builds/Windows/ (pasta ignorada pelo git).
/// </summary>
public static class BuildWindows
{
    const string OutputDir = "Builds/Windows";

    [MenuItem("Jogo/Build Windows")]
    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("Nenhuma cena no Build Settings.");
            return;
        }

        Directory.CreateDirectory(OutputDir);
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(OutputDir, $"{PlayerSettings.productName}.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
            Debug.Log($"BUILD OK: {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0}s)");
        else
            Debug.LogError($"BUILD FALHOU: {summary.result} ({summary.totalErrors} erros)");

        if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
