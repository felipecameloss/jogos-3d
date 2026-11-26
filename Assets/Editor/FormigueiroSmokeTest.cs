using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>
/// Fase 5: formigueiro (issue #61, Test Runner > EditMode): túneis escuros com todas as câmaras
/// ligadas, o vaga-lume lanterna voando com o player, baús e cano alcançáveis.
/// </summary>
public class FormigueiroSmokeTest
{
    [UnityTest]
    public IEnumerator TuneisEscurosComVagaLumeLanterna()
    {
        StageTestUtils.OpenMain();
        yield return new EnterPlayMode();
        yield return StageTestUtils.GoToStage(5, "Formigueiro");
        StageTestUtils.Quiet();
        var player = GameObject.FindGameObjectWithTag("Player");

        var level = GameObject.Find("Formigueiro");
        Assert.IsNotNull(level, "cenário do formigueiro");
        Assert.IsTrue(GameObject.Find("Backyard") == null, "o quintal some");
        var spawn = level.transform.Find("PlayerSpawn");
        Assert.Less(Vector3.Distance(player.transform.position, spawn.position), 2f);

        Assert.IsNotNull(PlayerLantern.Current, "vaga-lume lanterna");
        Assert.IsNotNull(PlayerLantern.Current.Light);
        Assert.Greater(PlayerLantern.Current.Light.range, 10f);
        Assert.Less(DayNightCycle.Instance.LightScale, 0.7f, "escuro");
        Assert.IsTrue(DayNightCycle.Instance.IsNight);

        // todas as câmaras ligadas ao começo
        float[] rooms = { -36f, -18f, 0f, 18f, 36f };
        foreach (float x in rooms)
            foreach (float z in rooms)
                Assert.IsTrue(StageTestUtils.HasPath(spawn.position, new Vector3(x, 0f, z), 4f), $"câmara ({x}, {z}) alcançável");
        float reach = StageTestUtils.ReachableFraction(spawn.position, 44f, 2f, out int points);
        Debug.Log($"[Smoke] formigueiro: {reach:P0} de {points} pontos do chão com caminho até o começo");
        Assert.Greater(reach, 0.97f, "sem bolsões fechados");
        Assert.IsTrue(NavMesh.SamplePosition(ExitPipe.Instance.transform.position, out _, 3f, NavMesh.AllAreas), "cano no NavMesh");
        Assert.Greater(Object.FindObjectsByType<Chest>().Length, 0, "baús nos túneis");

        yield return StageTestUtils.Wait(1.2f);
        Assert.Less(Vector3.Distance(PlayerLantern.Current.transform.position, player.transform.position), 4f, "a lanterna acompanha");
        StageTestUtils.Capture("5_Formigueiro");
        yield return new ExitPlayMode();
    }
}
