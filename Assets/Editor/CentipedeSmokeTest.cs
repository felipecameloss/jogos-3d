using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>
/// Centopeia (issue #28, Test Runner > EditMode): a cabeça monta o corpo e os segmentos seguem
/// em fila (só a cabeça usa o agente); cada segmento tem vida própria e solta migalha; matar um do
/// meio parte em duas (o de trás vira cabeça) e matar a cabeça passa o comando; encostar no corpo machuca.
/// </summary>
public class CentipedeSmokeTest
{
    const string PrefabPath = "Assets/Prefabs/Enemies/Centipede.prefab";

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
        yield return null;
    }

    static IEnumerator Wait(float seconds)
    {
        for (float end = Time.time + seconds; Time.time < end;) yield return null;
    }

    static void Teleport(GameObject player, Vector3 position)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = position;
        cc.enabled = true;
    }

    static Centipede Spawn(Transform player, float distance)
    {
        Vector3 at = player.position + player.forward * distance;
        if (NavMesh.SamplePosition(at, out var hit, 4f, NavMesh.AllAreas)) at = hit.position;
        Vector3 look = player.position - at; look.y = 0f;
        return Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), at, Quaternion.LookRotation(look))
            .GetComponent<Centipede>();
    }

    static List<Centipede> Chain(Centipede head)
    {
        var list = new List<Centipede>();
        for (var c = head; c != null && list.Count < 100; c = c.Follower) list.Add(c);
        return list;
    }

    static void Kill(Centipede c, GameObject by) =>
        c.Health.TakeDamage(new DamageInfo(99999f, by) { IgnoreArmor = true });

    [UnityTest]
    public IEnumerator CorpoSegueACabecaEmFila()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<Health>().Invincible = true;
        var head = Spawn(player.transform, 14f);
        yield return null;
        yield return null;

        var links = Chain(head);
        Assert.AreEqual(10, links.Count, "cabeça + 9 segmentos");
        Assert.IsTrue(head.IsHead);
        Assert.IsTrue(links.Skip(1).All(l => !l.IsHead), "o resto é corpo");
        Assert.IsTrue(head.GetComponent<NavMeshAgent>().enabled, "a cabeça anda pelo NavMesh");
        Assert.IsTrue(links.Skip(1).All(l => !l.GetComponent<NavMeshAgent>().enabled), "o corpo segue o rastro, sem agente");
        Assert.AreEqual(10, EnemyBase.Active.Count(e => e is Centipede), "cada segmento é um inimigo");
        Assert.AreEqual(10, links.Select(l => l.Health).Distinct().Count(), "cada um com vida própria");

        Vector3 start = head.transform.position;
        yield return Wait(1.5f);
        Assert.Greater(Vector3.Distance(start, head.transform.position), 2f, "a cabeça anda");
        for (int i = 1; i < links.Count; i++)
        {
            Vector3 d = links[i - 1].transform.position - links[i].transform.position; d.y = 0f;
            Assert.That(d.magnitude, Is.InRange(0.4f, 0.8f), $"segmento {i} colado no da frente");
        }

        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator MatarUmDoMeioParteEmDuasEACabecaPassaOComando()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<Health>().Invincible = true;
        var head = Spawn(player.transform, 16f);
        yield return null;
        yield return Wait(0.5f);
        var links = Chain(head);
        Assert.AreEqual(10, links.Count);
        int promotions = Centipede.Promotions;
        int crumbs = Object.FindObjectsByType<Crumb>().Length;

        // Mata o 6º: a frente fica com 5, o 7º vira cabeça de uma centopeia de 4
        Kill(links[5], player);
        yield return null;
        Assert.AreEqual(5, head.ChainLength, "a parte da frente encolheu");
        Assert.IsTrue(links[6].IsHead, "o de trás virou cabeça");
        Assert.AreEqual(4, links[6].ChainLength);
        Assert.AreEqual(promotions + 1, Centipede.Promotions);
        Assert.IsTrue(links[6].GetComponent<NavMeshAgent>().enabled, "a nova cabeça anda sozinha");
        yield return null;
        Assert.Greater(Object.FindObjectsByType<Crumb>().Length, crumbs, "o segmento soltou migalha");

        Vector3 before = links[6].transform.position;
        yield return Wait(1.3f);
        Assert.Greater(Vector3.Distance(before, links[6].transform.position), 0.5f, "depois do susto, sai caçando");

        // Mata a cabeça: o próximo assume
        Kill(head, player);
        yield return null;
        Assert.IsTrue(links[1].IsHead, "o segundo virou cabeça");
        Assert.AreEqual(4, links[1].ChainLength);
        Assert.AreEqual(promotions + 2, Centipede.Promotions);

        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator EncostarNoCorpoMachuca()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        var health = player.GetComponent<Health>();
        health.RegenPerSecond = 0f;
        var head = Spawn(player.transform, 18f);
        yield return null;
        yield return Wait(0.4f);
        var links = Chain(head);
        float hp0 = health.Current;

        // Encosta num segmento do meio, longe da cabeça (que morderia)
        var mid = links[7];
        Teleport(player, mid.transform.position + mid.transform.right * 0.5f);
        yield return Wait(0.4f);
        Debug.Log($"[Smoke] centopeia: encostou no corpo {hp0:0} -> {health.Current:0}");
        Assert.Less(health.Current, hp0, "encostar no corpo machuca");

        yield return new ExitPlayMode();
    }
}
