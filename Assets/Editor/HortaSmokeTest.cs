using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>
/// Fase 2: horta (issue #29, Test Runner > EditMode): monta por cima do quintal, com tomates e
/// abóboras que furam o NavMesh, tabela de inimigos de horta e o louva-a-deus; o regador avisa,
/// despeja água, machuca quem está na faixa e deixa poça.
/// </summary>
public class HortaSmokeTest
{
    [UnityTest]
    public IEnumerator HortaMontaPorCimaDoQuintal()
    {
        StageTestUtils.OpenMain();
        yield return new EnterPlayMode();
        yield return StageTestUtils.GoToStage(2, "Horta");
        StageTestUtils.Quiet();
        foreach (var w in Object.FindObjectsByType<WateringCanEvent>()) w.enabled = false;

        Assert.IsTrue(GameObject.Find("Backyard") != null, "o terreno do quintal continua");
        Assert.IsTrue(GameObject.Find("Horta") != null, "enfeites da horta");
        int obstacles = Object.FindObjectsByType<NavMeshObstacle>().Count(o => o.carving);
        Assert.GreaterOrEqual(obstacles, 8, "tomates e abóboras gigantes furam o NavMesh");
        var spawner = Object.FindAnyObjectByType<EnemySpawner>();
        var names = spawner.Entries.Select(e => e.prefab.name).ToArray();
        Debug.Log("[Smoke] horta: inimigos " + string.Join(", ", names));
        CollectionAssert.IsSubsetOf(new[] { "Slug", "Grasshopper", "Aphid" }, names, "tabela da horta");
        Assert.AreEqual("MantisBoss", Object.FindAnyObjectByType<BossSpawner>().CurrentBossPrefab.name);
        Assert.IsNotNull(Object.FindAnyObjectByType<WateringCanEvent>(), "o regador");
        yield return StageTestUtils.Wait(1f);
        StageTestUtils.Capture("2_Horta");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RegadorAvisaDespejaEDeixaPoca()
    {
        StageTestUtils.OpenMain();
        yield return new EnterPlayMode();
        yield return StageTestUtils.GoToStage(2, "Horta");
        StageTestUtils.Quiet();
        var can = Object.FindAnyObjectByType<WateringCanEvent>();
        can.enabled = false; // sem despejos sozinhos: só o do teste
        var player = GameObject.FindGameObjectWithTag("Player");
        var health = player.GetComponent<Health>();
        health.RegenPerSecond = 0f;
        float hp0 = health.Current;
        Vector3 target = player.transform.position;

        can.PourNow(target);
        yield return null;
        Assert.IsTrue(can.IsWarning, "primeiro o aviso (faixa azul)");
        for (float end = Time.time + 8f; Time.time < end && (can.IsWarning || can.IsPouring);) yield return null;
        Debug.Log($"[Smoke] regador: player {hp0:0} -> {health.Current:0}, acertos {can.PlayerHits}");
        Assert.AreEqual(1, can.Pours);
        Assert.AreEqual(1, can.PlayerHits, "o jato pegou o player uma vez");
        Assert.Less(health.Current, hp0);
        bool puddle = SlowZone.Active.Any(z => z.ZoneKind == SlowZone.Kind.Water && Vector3.Distance(z.transform.position, target) < 4f);
        Assert.IsTrue(puddle, "ficou uma poça onde a água caiu");
        yield return new ExitPlayMode();
    }
}
