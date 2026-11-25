using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>
/// Teste de fumaça do evento de chuva (issue #33): céu fecha, gotas gigantes com sombra de
/// aviso caem e machucam o player e os inimigos dentro do círculo, a chuva passa
/// (Window > General > Test Runner > EditMode).
/// </summary>
public class RainSmokeTest
{
    [UnityTest]
    public IEnumerator ChuvaFechaOCeuEGotasMachucam()
    {
        LogAssert.ignoreFailingMessages = true; // erros de outros sistemas não derrubam este teste
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        var player = playerGo.transform;
        var health = playerGo.GetComponent<Health>();
        foreach (var s in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None)) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<StompDirector>(FindObjectsSortMode.None)) s.enabled = false;
        foreach (var e in EnemyBase.Active.ToArray()) Object.Destroy(e.gameObject);
        yield return null;

        var rain = Object.FindAnyObjectByType<RainEvent>();
        Assert.IsNotNull(rain, "cena tem o evento de chuva");
        Assert.IsFalse(rain.IsRaining, "começa sem chuva");
        Assert.Greater(rain.NextRainTime, Time.time + 30f, "a primeira chuva demora");
        Assert.AreEqual(0f, DayNightCycle.Overcast, 0.001f);

        // ---- Começa a chover: céu fecha e partículas aparecem
        rain.StartRain(30f);
        for (float end = Time.time + 3.5f; Time.time < end;) yield return null;
        Assert.IsTrue(rain.IsRaining);
        Assert.Greater(rain.Intensity, 0.9f, "céu fechou");
        Assert.Greater(DayNightCycle.Overcast, 0.9f, "DayNightCycle escureceu");
        Assert.Greater(rain.Particles.particleCount, 50, "riscos de chuva");
        Assert.Greater(rain.DropCount, 0, "gotas começaram a cair sozinhas");

        // ---- Gota em cima do player: sombra de aviso, depois dano e empurrão
        rain.enabled = false; // só a gota do teste daqui pra frente
        for (float end = Time.time + 2.5f; Time.time < end;) yield return null; // gotas que já estavam caindo terminam
        var ant = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/WorkerAnt.prefab"),
            player.position + player.right * 1.2f, Quaternion.identity);
        yield return null;
        ant.transform.position = player.position + player.right * 1.2f;
        ant.enabled = false; // sem IA: não morde o player no meio do teste (a vida continua valendo)
        float hp0 = health.Current, antHp0 = ant.Health.Current;
        var drop = rain.DropAt(player.position);
        Assert.IsNotNull(drop, "gota nasceu");
        for (float end = Time.time + drop.WarningTime * 0.8f; Time.time < end;) yield return null;
        Assert.IsFalse(drop.HasImpacted, "aviso antes do impacto");
        Assert.AreEqual(hp0, health.Current, 0.01f, "sem dano durante o aviso");
        for (float end = Time.time + 2f; Time.time < end && !drop.HasImpacted;) yield return null;
        Assert.IsTrue(drop.HasImpacted, "gota caiu");
        Debug.Log($"[Smoke] chuva: player {hp0:0} -> {health.Current:0}, formiga {antHp0:0} -> {(ant != null ? ant.Health.Current : -1):0}");
        Assert.Less(health.Current, hp0, "player levou dano");
        Assert.Less(ant.Health.Current, antHp0, "inimigo dentro do círculo levou dano");

        // ---- A chuva passa: céu abre
        rain.enabled = true;
        rain.StopRain();
        Assert.Greater(rain.NextRainTime, Time.time + 60f, "próxima chuva só depois de um tempo seco");
        for (float end = Time.time + 4f; Time.time < end;) yield return null;
        Assert.IsFalse(rain.IsRaining);
        Assert.Less(rain.Intensity, 0.01f, "céu abriu");
        Assert.Less(DayNightCycle.Overcast, 0.01f);
        yield return new ExitPlayMode();
    }
}
