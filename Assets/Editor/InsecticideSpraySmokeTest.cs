using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>
/// Spray de inseticida (issue #23, Test Runner > EditMode): o jato machuca e envenena só quem
/// está no cone, deixa uma nuvem no fim dele, e a nuvem machuca inimigos e o player que fica
/// dentro (mas não quem sai dela nem quem flutua na bolha de sabão).
/// </summary>
public class InsecticideSpraySmokeTest
{
    const string SprayPath = "Assets/ScriptableObjects/Items/SprayDeInseticida.asset";
    const string AntPath = "Assets/Prefabs/Enemies/WorkerAnt.prefab";

    /// <summary>Abre a cena do jogo. O EnterPlayMode vem logo depois, no próprio teste:
    /// o runner de EditMode ignora essa instrução dentro de um helper.</summary>
    static void OpenMain()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }

    /// <summary>Já em Play Mode: só o player, sem inimigos, chefe nem levas de bolhas sozinhas.</summary>
    static IEnumerator PrepareRun()
    {
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;
        foreach (var s in Object.FindObjectsByType<EnemySpawner>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<BossSpawner>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<SoapBubbleStream>()) s.enabled = false;
        foreach (var e in EnemyBase.Active.ToArray()) Object.Destroy(e.gameObject);
        yield return null;
    }

    static IEnumerator Wait(float seconds)
    {
        for (float end = Time.time + seconds; Time.time < end;) yield return null;
    }

    /// <summary>Formiga parruda que não machuca (vida x20, dano x0), em cima do NavMesh.</summary>
    static EnemyBase SpawnDummyAnt(Vector3 at)
    {
        if (NavMesh.SamplePosition(at, out var hit, 4f, NavMesh.AllAreas)) at = hit.position;
        var ant = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AntPath), at, Quaternion.identity).GetComponent<EnemyBase>();
        ant.ApplyScaling(20f, 0f);
        return ant;
    }

    static void Teleport(GameObject player, Vector3 position)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = position;
        cc.enabled = true;
    }

    [UnityTest]
    public IEnumerator JatoMachucaEEnvenenaSoQuemEstaNoConeEDeixaNuvem()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<Health>().Invincible = true;
        var slot = player.GetComponent<EquipmentSlot>();
        var spray = AssetDatabase.LoadAssetAtPath<InsecticideSprayEquipment>(SprayPath);
        Assert.IsNotNull(spray, "asset do spray");
        player.GetComponent<Inventory>().Add(spray);
        Assert.AreEqual(spray, slot.Current, "vai para o slot do Q");

        Vector3 aim = slot.AimPoint - slot.Chest;
        aim.y = 0f;
        aim.Normalize();
        var front = SpawnDummyAnt(player.transform.position + aim * 5f);
        var back = SpawnDummyAnt(player.transform.position - aim * 6f);
        yield return null;
        float front0 = front.Health.Current, back0 = back.Health.Current;

        Assert.IsTrue(slot.TryUse(), "usou");
        yield return Wait(spray.SprayDuration + 0.2f);
        Debug.Log($"[Smoke] spray: frente {front0:0} -> {front.Health.Current:0}, atrás {back0:0} -> {back.Health.Current:0}");
        Assert.Less(front.Health.Current, front0, "o jato machuca quem está no cone");
        Assert.IsTrue(StatusEffects.On(front).IsPoisoned, "e envenena");
        Assert.AreEqual(back0, back.Health.Current, 0.001f, "atrás do player não pega");
        Assert.IsFalse(slot.TryUse(), "em recarga");

        Assert.AreEqual(1, InsecticideCloud.Active.Count, "deixou a nuvem");
        var cloud = InsecticideCloud.Active[0];
        Vector3 toCloud = cloud.transform.position - player.transform.position;
        toCloud.y = 0f;
        Assert.Greater(Vector3.Dot(toCloud.normalized, aim), 0.8f, "a nuvem fica na frente, no fim do cone");
        Assert.That(toCloud.magnitude, Is.InRange(3f, 7f));

        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator NuvemMachucaInimigosEOPlayerQueFicaDentro()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        var health = player.GetComponent<Health>();
        var movement = player.GetComponent<PlayerMovement>();
        health.RegenPerSecond = 0f; // a vida só muda pela nuvem
        Vector3 ground = player.transform.position;

        // Nuvem em volta do player, com uma formiga junto
        var ant = SpawnDummyAnt(ground + Vector3.right * 1.5f);
        var cloud = InsecticideCloud.Spawn(ground, 4f, 30f, 10f, 8f, player);
        yield return null;
        float hp0 = health.Current, ant0 = ant.Health.Current;
        yield return Wait(1.3f);
        Debug.Log($"[Smoke] nuvem: player {hp0:0} -> {health.Current:0}, formiga {ant0:0} -> {ant.Health.Current:0}");
        Assert.Less(health.Current, hp0, "dentro da nuvem o player se machuca");
        Assert.Greater(cloud.DamageDealtToPlayer, 0f);
        Assert.Less(ant.Health.Current, ant0, "e os inimigos também");

        // Saiu da nuvem: para de machucar
        Teleport(player, ground + Vector3.forward * 12f);
        yield return Wait(0.2f);
        float outside = health.Current;
        yield return Wait(1.1f);
        Assert.AreEqual(outside, health.Current, 0.001f, "fora da nuvem não machuca");

        // Flutuando na bolha de sabão dentro da nuvem: não respira o veneno
        player.GetComponent<Inventory>().Add(AssetDatabase.LoadAssetAtPath<ItemData>("Assets/ScriptableObjects/Items/BolhaDeSabao.asset"));
        Teleport(player, ground + Vector3.up * 2f);
        movement.HoldJumpOverride = true;
        yield return null;
        float floating0 = health.Current;
        yield return Wait(0.9f);
        Assert.IsTrue(movement.IsFloating, "flutuando");
        Assert.IsTrue(cloud.IsInside(player.transform.position + Vector3.up * 0.5f), "dentro da nuvem");
        Assert.AreEqual(floating0, health.Current, 0.001f, "na bolha o veneno não pega");

        movement.HoldJumpOverride = false;
        yield return new ExitPlayMode();
    }
}
