using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Base das fases (Test Runner > EditMode): a fase 1 é o quintal como sempre foi, a sequência
/// volta para o começo depois da última, pular de fase pelo debug funciona, a zona lenta
/// (água/teia) segura o player e solta aos poucos, e o vaga-lume lanterna segue o player.
/// </summary>
public class StageSetupSmokeTest
{
    [Test]
    public void SequenciaComecaNoQuintalEVoltaDepoisDaUltima()
    {
        var seq = AssetDatabase.LoadAssetAtPath<StageSequence>("Assets/ScriptableObjects/Stages/StageSequence.asset");
        Assert.IsNotNull(seq, "sequência de fases");
        Assert.Greater(seq.Count, 0);
        Assert.AreEqual("Quintal", seq.ForStage(1).DisplayName, "fase 1 = quintal");
        Assert.AreEqual(seq.ForStage(1), seq.ForStage(seq.Count + 1), "depois da última volta para a primeira");
        Assert.IsTrue(seq.IsFinal(seq.Count));
        Assert.IsTrue(seq.ForStage(1).UseBackyard && seq.ForStage(1).LevelPrefab == null, "o quintal é a cena como está");
        foreach (var s in seq.Stages)
        {
            Assert.IsNotNull(s, "fase vazia na sequência");
            Assert.IsFalse(string.IsNullOrEmpty(s.DisplayName));
            Assert.IsTrue(s.UseBackyard || s.LevelPrefab != null, $"{s.DisplayName}: sem quintal precisa de cenário");
        }
    }

    [UnityTest]
    public IEnumerator Fase1EOQuintalEPularDeFaseFunciona()
    {
        StageTestUtils.OpenMain();
        yield return new EnterPlayMode();
        for (int i = 0; i < 10; i++) yield return null;
        Assert.IsNotNull(StageDirector.Current, "StageDirector na cena");
        Assert.AreEqual("Quintal", StageDirector.Current.DisplayName);
        Assert.IsTrue(GameObject.Find("Backyard") != null, "quintal ligado");

        yield return StageTestUtils.GoToStage(1, "Quintal");
        Assert.AreEqual(1, RunManager.Instance.Stage);
        Assert.AreEqual(1f, RunManager.Instance.PlayerHealth.Current / RunManager.Instance.PlayerHealth.Max, 0.001f, "começa com vida cheia");
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ZonaLentaSeguraEDepoisSolta()
    {
        StageTestUtils.OpenMain();
        yield return new EnterPlayMode();
        for (int i = 0; i < 10; i++) yield return null;
        StageTestUtils.Quiet();
        var player = GameObject.FindGameObjectWithTag("Player");
        var movement = player.GetComponent<PlayerMovement>();
        player.GetComponent<Health>().Invincible = true;

        var zoneGo = new GameObject("ZonaDeTeste");
        zoneGo.transform.position = player.transform.position;
        var zone = zoneGo.AddComponent<SlowZone>();
        zone.Configure(SlowZone.Kind.Water, 4f, 0.6f, 0.5f);
        yield return null;
        yield return null;
        Assert.IsTrue(SlowZone.IsIn(player.transform.position, SlowZone.Kind.Water), "player dentro da água");
        Assert.AreEqual(0.5f, movement.SurfaceSpeedFactor, 0.01f, "lento na hora");

        Object.Destroy(zoneGo);
        yield return null;
        yield return null;
        Assert.Less(movement.SurfaceSpeedFactor, 0.6f, "ainda molhado logo depois de sair");
        yield return StageTestUtils.Wait(0.6f);
        Assert.AreEqual(1f, movement.SurfaceSpeedFactor, 0.01f, "seca em menos de um segundo");

        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator VagaLumeLanternaSegueOPlayer()
    {
        StageTestUtils.OpenMain();
        yield return new EnterPlayMode();
        for (int i = 0; i < 10; i++) yield return null;
        StageTestUtils.Quiet();
        var player = GameObject.FindGameObjectWithTag("Player");
        var lantern = PlayerLantern.Attach(player, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Characters/Firefly.fbx"));
        Assert.IsNotNull(lantern);
        Assert.IsNotNull(lantern.Light, "tem luz");
        Assert.IsNotNull(lantern.transform.Find("Model"), "tem o modelo do vaga-lume");
        StageTestUtils.Teleport(player, player.transform.position + player.transform.forward * 8f);
        yield return StageTestUtils.Wait(1.2f);
        Assert.Less(Vector3.Distance(lantern.transform.position, player.transform.position), 4f, "acompanha o player");
        Object.Destroy(lantern.gameObject);
        yield return new ExitPlayMode();
    }
}
