using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Fase final: cozinha (issue #40, Test Runner > EditMode): dentro de casa (sem ciclo de dia),
/// migalhas espalhadas pelo chão, dá para subir no balcão pela tábua de carne (os inimigos também),
/// é a última da sequência e depois dela a run volta para o quintal.
/// </summary>
public class CozinhaSmokeTest
{
    [UnityTest]
    public IEnumerator CozinhaFinalComBalcaoEMigalhas()
    {
        StageTestUtils.OpenMain();
        yield return new EnterPlayMode();
        yield return StageTestUtils.GoToStage(6, "Cozinha");
        StageTestUtils.Quiet();
        var player = GameObject.FindGameObjectWithTag("Player");

        var level = GameObject.Find("Cozinha");
        Assert.IsNotNull(level, "cenário da cozinha");
        Assert.IsTrue(StageDirector.IsFinalStage, "fase final");
        Assert.IsFalse(DayNightCycle.Instance.enabled, "dentro de casa: sem ciclo de dia/noite");
        Assert.AreEqual(CameraClearFlags.SolidColor, Camera.main.clearFlags, "sem céu");

        var spawn = level.transform.Find("PlayerSpawn");
        var counterTop = new Vector3(-4f, CozinhaBuilder.CounterTop, 8f);
        Assert.IsTrue(StageTestUtils.HasPath(spawn.position, counterTop, 1.5f), "dá para subir no balcão andando (tábua de carne)");
        Assert.IsTrue(StageTestUtils.HasPath(spawn.position, new Vector3(30f, 0f, -30f), 3f));

        yield return StageTestUtils.Wait(1f);
        int crumbs = Object.FindObjectsByType<Crumb>().Length;
        Debug.Log($"[Smoke] cozinha: {crumbs} migalhas no chão");
        Assert.GreaterOrEqual(crumbs, 30, "migalhas por todo lado");
        StageTestUtils.Capture("6_Cozinha");

        // depois da cozinha a run volta para o quintal (fase 7)
        yield return StageTestUtils.GoToStage(7, "Quintal");
        Assert.IsTrue(GameObject.Find("Backyard") != null);
        yield return new ExitPlayMode();
    }
}
