using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>
/// Ajudantes dos testes de fase: abrir a cena, pular para uma fase (RunManager.LoadStageForDebug),
/// desligar o que atrapalha (spawners, eventos), teleportar o player e salvar imagens da fase
/// (câmera do jogo e vista de cima) em Library/StagePreviews para conferir o cenário.
/// </summary>
public static class StageTestUtils
{
    /// <summary>Abre a cena do jogo. O EnterPlayMode vem logo depois, no próprio teste:
    /// o runner de EditMode ignora essa instrução dentro de um helper.</summary>
    public static void OpenMain()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }

    /// <summary>Já em Play Mode: recarrega a cena começando na fase pedida e espera ela montar.</summary>
    public static IEnumerator GoToStage(int stage, string expectedName)
    {
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 5; i++) yield return null;
        RunManager.LoadStageForDebug(stage);
        for (float end = Time.realtimeSinceStartup + 10f; Time.realtimeSinceStartup < end;)
        {
            yield return null;
            if (StageDirector.Current != null && StageDirector.Current.DisplayName == expectedName && RunManager.Instance != null
                && RunManager.Instance.Stage == stage) break;
        }
        for (int i = 0; i < 10; i++) yield return null;
        LogAssert.ignoreFailingMessages = true;
        Assert.IsNotNull(StageDirector.Current, "fase montada");
        Assert.AreEqual(expectedName, StageDirector.Current.DisplayName);
        Assert.AreEqual(stage, RunManager.Instance.Stage);
    }

    /// <summary>Sem inimigos nem eventos sozinhos (o teste controla o que acontece).</summary>
    public static void Quiet()
    {
        foreach (var s in Object.FindObjectsByType<EnemySpawner>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<BossSpawner>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<StompDirector>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<RainEvent>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<MagnifierEvent>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<SoapBubbleStream>()) s.enabled = false;
        foreach (var e in EnemyBase.Active.ToArray()) Object.Destroy(e.gameObject);
    }

    public static IEnumerator Wait(float seconds)
    {
        for (float end = Time.time + seconds; Time.time < end;) yield return null;
    }

    public static void Teleport(GameObject player, Vector3 position)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = position;
        cc.enabled = true;
    }

    /// <summary>Tem caminho no NavMesh entre os dois pontos (perto deles).</summary>
    public static bool HasPath(Vector3 from, Vector3 to, float snap = 3f)
    {
        if (!NavMesh.SamplePosition(from, out var a, snap, NavMesh.AllAreas)) return false;
        if (!NavMesh.SamplePosition(to, out var b, snap, NavMesh.AllAreas)) return false;
        var path = new NavMeshPath();
        return NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
    }

    /// <summary>
    /// Fração dos pontos de NavMesh no chão (grade de `step` m dentro de ±half) que têm caminho até
    /// `start`. Bolsões fechados (atrás de parede, dentro de móvel) baixam a conta.
    /// </summary>
    public static float ReachableFraction(Vector3 start, float half, float step, out int total)
    {
        total = 0;
        int ok = 0;
        if (!NavMesh.SamplePosition(start, out var s, 3f, NavMesh.AllAreas)) return 0f;
        var path = new NavMeshPath();
        for (float x = -half; x <= half; x += step)
            for (float z = -half; z <= half; z += step)
            {
                if (!NavMesh.SamplePosition(new Vector3(x, 0.5f, z), out var hit, 1f, NavMesh.AllAreas) || hit.position.y > 1f) continue;
                total++;
                if (NavMesh.CalculatePath(s.position, hit.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete) ok++;
            }
        return total > 0 ? (float)ok / total : 0f;
    }

    /// <summary>
    /// Salva a imagem da câmera do jogo e uma vista de cima (escondendo o que estiver em
    /// `hideFromTop`, ex.: o teto do deck) em Library/StagePreviews/&lt;nome&gt;_jogo.png / _cima.png
    /// (fora do git; a Temp o Unity apaga ao fechar).
    /// </summary>
    public static void Capture(string name, params string[] hideFromTop)
    {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), "Library", "StagePreviews");
        Directory.CreateDirectory(dir);
        var cam = Camera.main;
        if (cam != null) Save(cam, Path.Combine(dir, name + "_jogo.png"), 960, 540);

        var hidden = new System.Collections.Generic.List<Renderer>();
        foreach (var n in hideFromTop)
        {
            var go = GameObject.Find(n);
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hidden.Add(r); }
        }
        var top = new GameObject("TopCam").AddComponent<Camera>();
        top.orthographic = true;
        top.orthographicSize = 50f;
        top.transform.SetPositionAndRotation(new Vector3(0f, 150f, 0f), Quaternion.Euler(90f, 0f, 0f));
        top.farClipPlane = 400f;
        top.clearFlags = CameraClearFlags.SolidColor;
        top.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        bool fog = RenderSettings.fog;
        RenderSettings.fog = false; // de 150 m de altura a neblina apagaria tudo
        Save(top, Path.Combine(dir, name + "_cima.png"), 900, 900);
        RenderSettings.fog = fog;
        Object.DestroyImmediate(top.gameObject);
        foreach (var r in hidden) r.enabled = true;
    }

    static void Save(Camera cam, string path, int w, int h)
    {
        var rt = RenderTexture.GetTemporary(w, h, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        var active = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}
