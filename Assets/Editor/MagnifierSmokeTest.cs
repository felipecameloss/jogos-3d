using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Teste de fumaça do evento "criança com lupa" (issue #34): o raio de sol mira, foca,
/// persegue o player, queima ele e incendeia inimigos dentro do círculo, e some no fim;
/// sem sol (chuva) a lupa não aparece (Window > General > Test Runner > EditMode).
/// </summary>
public class MagnifierSmokeTest
{
    [UnityTest]
    public IEnumerator RaioDeSolPersegueEQueima()
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
        var rain = Object.FindAnyObjectByType<RainEvent>();
        if (rain != null) rain.enabled = false;
        DayNightCycle.Overcast = 0f;
        yield return null;

        var ev = Object.FindAnyObjectByType<MagnifierEvent>();
        Assert.IsNotNull(ev, "cena tem o evento da lupa");
        Assert.IsNull(ev.Active, "começa sem lupa");
        Assert.Greater(ev.NextEventTime, Time.time + 20f, "a primeira lupa demora");

        // ---- Mira: aparece no player, largo, sem queimar ainda
        var ant = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/WorkerAnt.prefab"),
            player.position + player.right * 0.8f, Quaternion.identity);
        yield return null;
        ant.transform.position = player.position + player.right * 0.8f;
        ant.enabled = false; // sem IA: fica parada dentro do círculo e não morde o player
        float hp0 = health.Current;
        var spot = ev.Trigger();
        Assert.IsNotNull(spot, "lupa apareceu");
        yield return null;
        Assert.AreEqual(SunSpot.Phase.Aim, spot.Current);
        for (float end = Time.time + spot.AimTime * 0.7f; Time.time < end;) yield return null;
        Assert.AreEqual(hp0, health.Current, 0.01f, "sem dano na mira (aviso)");

        // ---- Foco: queima o player e incendeia o inimigo dentro do círculo
        for (float end = Time.time + spot.AimTime * 0.3f + 1.2f; Time.time < end;) yield return null;
        Assert.IsTrue(spot.IsBurning, "focou");
        Assert.Less(spot.CurrentRadius, 2.5f, "círculo encolheu");
        Assert.Less(health.Current, hp0, "player queimou");
        Assert.Greater(spot.EnemiesIgnited, 0, "inimigo pegou fogo");
        Assert.IsTrue(StatusEffects.On(ant).IsBurning);
        Debug.Log($"[Smoke] lupa: player {hp0:0} -> {health.Current:0}, hits={spot.PlayerHits}");

        // ---- Persegue: o player anda e o círculo vai atrás
        Vector3 before = spot.Center;
        playerGo.GetComponent<CharacterController>().enabled = false;
        player.position += player.forward * 6f;
        playerGo.GetComponent<CharacterController>().enabled = true;
        for (float end = Time.time + 1.2f; Time.time < end;) yield return null;
        Vector3 moved = spot.Center - before; moved.y = 0f;
        Assert.Greater(moved.magnitude, 4f, "o raio seguiu o player");

        // ---- Some no fim e agenda a próxima
        for (float end = Time.time + spot.TotalTime + 1f; Time.time < end && ev.Active != null;) yield return null;
        Assert.IsNull(ev.Active, "lupa sumiu");
        Assert.Greater(ev.NextEventTime, Time.time + 60f, "próxima lupa só depois de um tempo");

        // ---- Sem sol (chuva): não aparece
        DayNightCycle.Overcast = 1f;
        Assert.IsFalse(ev.HasSun, "chuva tapa o sol");
        DayNightCycle.Overcast = 0f;
        yield return new ExitPlayMode();
    }
}
