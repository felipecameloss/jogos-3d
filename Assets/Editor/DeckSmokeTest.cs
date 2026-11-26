using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>
/// Fase 4: embaixo do deck (issue #30, Test Runner > EditMode): cenário próprio no lugar do
/// quintal, escuro (luz fixa pelas frestas; vaga-lumes nascem), sem pisão/chuva/lupa/bolhas, corredores todos ligados,
/// teias que deixam lento, baús e o cano em lugares alcançáveis.
/// </summary>
public class DeckSmokeTest
{
    [UnityTest]
    public IEnumerator DeckEscuroComCorredoresETeias()
    {
        StageTestUtils.OpenMain();
        yield return new EnterPlayMode();
        yield return StageTestUtils.GoToStage(4, "Embaixo do deck");
        StageTestUtils.Quiet();
        var player = GameObject.FindGameObjectWithTag("Player");
        var movement = player.GetComponent<PlayerMovement>();
        player.GetComponent<Health>().Invincible = true;

        Assert.IsTrue(GameObject.Find("Backyard") == null, "o quintal some");
        var level = GameObject.Find("Deck");
        Assert.IsNotNull(level, "cenário do deck");
        var spawn = level.transform.Find("PlayerSpawn");
        Assert.Less(Vector3.Distance(player.transform.position, spawn.position), 2f, "player começa no PlayerSpawn");
        Assert.IsTrue(NavMesh.SamplePosition(player.transform.position, out _, 2f, NavMesh.AllAreas), "NavMesh do deck");

        Assert.IsFalse(DayNightCycle.Instance.enabled, "luz fixa (entrando pelas frestas), sem ciclo");
        Assert.IsTrue(StageDirector.Current.Dark, "lugar escuro");
        Assert.IsTrue(AssetDatabase.LoadAssetAtPath<Firefly>("Assets/Prefabs/Enemies/Firefly.prefab").CanSpawnNow, "vaga-lumes podem nascer (escuro)");
        Assert.IsFalse(Object.FindAnyObjectByType<StompDirector>(FindObjectsInactive.Include).enabled, "sem pisão");
        Assert.IsFalse(Object.FindAnyObjectByType<RainEvent>(FindObjectsInactive.Include).enabled, "sem chuva");
        Assert.IsFalse(Object.FindAnyObjectByType<MagnifierEvent>(FindObjectsInactive.Include).enabled, "sem lupa");

        float reach = StageTestUtils.ReachableFraction(spawn.position, 44f, 4f, out int points);
        Debug.Log($"[Smoke] deck: {reach:P0} de {points} pontos do chão com caminho até o começo");
        Assert.Greater(reach, 0.97f, "corredores todos ligados");
        Assert.IsTrue(StageTestUtils.HasPath(spawn.position, new Vector3(-40f, 0f, 40f), 5f), "até o canto do fundo");
        Assert.IsTrue(NavMesh.SamplePosition(ExitPipe.Instance.transform.position, out _, 3f, NavMesh.AllAreas), "cano no NavMesh");
        Assert.Greater(Object.FindObjectsByType<Chest>().Length, 0, "baús no deck");

        var webs = SlowZone.Active.Where(z => z.ZoneKind == SlowZone.Kind.Web).ToList();
        Assert.GreaterOrEqual(webs.Count, 10, "teias pelo caminho");
        StageTestUtils.Teleport(player, webs[0].transform.position + Vector3.up * 0.2f);
        yield return StageTestUtils.Wait(0.4f);
        Assert.AreEqual(DeckBuilder.WebSlow, movement.SurfaceSpeedFactor, 0.02f, "teia deixa lento");

        StageTestUtils.Teleport(player, spawn.position);
        yield return StageTestUtils.Wait(1f);
        StageTestUtils.Capture("4_Deck", "Teto");
        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }
}
