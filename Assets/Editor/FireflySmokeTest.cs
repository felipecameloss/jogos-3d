using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>
/// Vaga-lume (issue #25, Test Runner > EditMode): só nasce à noite, voa na altura da cabeça,
/// carrega e pisca sozinho; o clarão cega (tela branca na HUD) quem está olhando para ele e
/// não quem virou a câmera; quando amanhece vai embora sem soltar migalhas nem contar kill.
/// </summary>
public class FireflySmokeTest
{
    const string PrefabPath = "Assets/Prefabs/Enemies/Firefly.prefab";

    /// <summary>Abre a cena do jogo. O EnterPlayMode vem logo depois, no próprio teste:
    /// o runner de EditMode ignora essa instrução dentro de um helper.</summary>
    static void OpenMain()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }

    /// <summary>Já em Play Mode: só o player, sem inimigos, chefe, pisão, chuva, lupa nem bolhas.</summary>
    static IEnumerator PrepareRun()
    {
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;
        foreach (var s in Object.FindObjectsByType<EnemySpawner>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<BossSpawner>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<StompDirector>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<RainEvent>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<MagnifierEvent>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<SoapBubbleStream>()) s.enabled = false;
        foreach (var e in EnemyBase.Active.ToArray()) Object.Destroy(e.gameObject);
        PlayerBlindness.Clear();
        yield return null;
    }

    static IEnumerator Wait(float seconds)
    {
        for (float end = Time.time + seconds; Time.time < end;) yield return null;
    }

    static IEnumerator WaitUntil(System.Func<bool> condition, float timeout)
    {
        for (float end = Time.time + timeout; Time.time < end && !condition();) yield return null;
    }

    /// <summary>Põe o ciclo nessa hora (0.25 = meio-dia, 0.75 = meia-noite).</summary>
    static void SetPhase(float phase)
    {
        var so = new SerializedObject(Object.FindAnyObjectByType<DayNightCycle>());
        so.FindProperty("startPhase").floatValue = phase;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Firefly Spawn(Vector3 near)
    {
        if (NavMesh.SamplePosition(near, out var hit, 4f, NavMesh.AllAreas)) near = hit.position;
        return Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), near, Quaternion.identity).GetComponent<Firefly>();
    }

    /// <summary>Leva o vaga-lume (pelo agente) para o chão mais perto desse ponto.</summary>
    static void Warp(Firefly f, Vector3 point)
    {
        Assert.IsTrue(NavMesh.SamplePosition(point, out var hit, 6f, NavMesh.AllAreas), "chão perto do ponto");
        f.GetComponent<NavMeshAgent>().Warp(hit.position);
    }

    [UnityTest]
    public IEnumerator SoDeNoiteVoaPiscaEVaiEmboraQuandoAmanhece()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var prefab = AssetDatabase.LoadAssetAtPath<Firefly>(PrefabPath);
        Assert.IsNotNull(DayNightCycle.Instance, "ciclo de dia/noite na cena");

        SetPhase(0.25f);
        yield return null;
        Assert.IsFalse(DayNightCycle.Instance.IsNight);
        Assert.IsFalse(prefab.CanSpawnNow, "de dia o spawner não sorteia vaga-lume");
        SetPhase(0.75f);
        yield return null;
        Assert.IsTrue(prefab.CanSpawnNow, "de noite sim");

        var player = GameObject.FindGameObjectWithTag("Player");
        var f = Spawn(player.transform.position + player.transform.forward * 7f);
        yield return Wait(1f);
        Assert.IsTrue(f.IsAlive);
        float groundY = player.transform.position.y;
        Assert.That(f.transform.position.y - groundY, Is.InRange(1.6f, 3.4f), "voa na altura da cabeça");

        // carrega piscando e solta o clarão sozinho
        bool charged = false;
        for (float end = Time.time + 8f; Time.time < end && f.Flashes == 0;)
        {
            charged |= f.IsCharging;
            yield return null;
        }
        Assert.IsTrue(charged, "carregou a luz (aviso)");
        Assert.GreaterOrEqual(f.Flashes, 1, "piscou");

        // amanhece: vai embora sem migalhas e sem contar kill
        int crumbs = Object.FindObjectsByType<Crumb>().Length;
        int kills = RunManager.Instance != null ? RunManager.Instance.Kills : 0;
        SetPhase(0.25f);
        yield return Wait(0.2f);
        Assert.IsTrue(f == null || f.IsLeaving, "sobe e vai embora");
        yield return Wait(2.5f);
        Assert.IsTrue(f == null || !EnemyBase.Active.Contains(f), "sumiu");
        Assert.AreEqual(crumbs, Object.FindObjectsByType<Crumb>().Length, "sem migalhas");
        if (RunManager.Instance != null) Assert.AreEqual(kills, RunManager.Instance.Kills, "não conta kill");

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ClaraoCegaQuemOlhaENaoQuemVira()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        SetPhase(0.75f);
        var player = GameObject.FindGameObjectWithTag("Player");
        var hud = Object.FindAnyObjectByType<Hud>();
        var f = Spawn(player.transform.position + player.transform.forward * 7f);
        yield return Wait(0.3f);

        var cam = Camera.main.transform;
        Vector3 fwd = cam.forward; fwd.y = 0f; fwd.Normalize();

        // Na frente da câmera: cega
        Warp(f, cam.position + fwd * 10f);
        yield return null;
        yield return null;
        float strength = PlayerBlindness.StrengthFrom(f.Center, 18f, 30f, 70f, f.transform);
        Debug.Log($"[Smoke] vaga-lume: força do clarão na frente da câmera {strength:0.00}");
        Assert.Greater(strength, 0.3f, "na frente da câmera o clarão é forte");
        f.FlashNow();
        Assert.AreEqual(1, f.Blinds, "cegou");
        Assert.Greater(PlayerBlindness.Amount, 0.3f);
        yield return null;
        yield return null;
        Assert.Greater(hud.BlindFlashAmount, 0.2f, "tela branca na HUD");

        // Atrás da câmera (o player virou): não cega
        yield return Wait(2.2f);
        Assert.AreEqual(0f, PlayerBlindness.Amount, 0.001f, "a tela volta ao normal");
        Warp(f, cam.position - fwd * 9f);
        yield return null;
        yield return null;
        int count = PlayerBlindness.Count;
        f.FlashNow();
        Assert.AreEqual(1, f.Blinds, "de costas não cega");
        Assert.AreEqual(count, PlayerBlindness.Count);
        Assert.AreEqual(0f, PlayerBlindness.Amount, 0.001f);

        yield return new ExitPlayMode();
    }
}
