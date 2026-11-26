using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>
/// Gafanhoto (issue #27, Test Runner > EditMode): salta de longe com aviso (círculo vermelho no
/// ponto de queda) e bate forte; o ponto trava no fim do aviso e dá para sair dele; de perto,
/// vira de costas e dá um coice.
/// </summary>
public class GrasshopperSmokeTest
{
    const string PrefabPath = "Assets/Prefabs/Enemies/Grasshopper.prefab";

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

    /// <summary>Ponto no NavMesh a `distance` m do player com visão livre até ele (só salta vendo o player).</summary>
    static Vector3 ClearSpot(Transform player, float distance)
    {
        for (int a = 0; a < 360; a += 20)
        {
            Vector3 p = player.position + Quaternion.Euler(0f, a, 0f) * Vector3.forward * distance;
            if (!NavMesh.SamplePosition(p, out var hit, 2f, NavMesh.AllAreas)) continue;
            Vector3 from = hit.position + Vector3.up * 1.2f, to = player.position + Vector3.up;
            bool blocked = Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore)
                .Any(h => !h.collider.transform.IsChildOf(player) && h.collider.GetComponentInParent<EnemyBase>() == null);
            if (!blocked) return hit.position;
        }
        Assert.Fail("nenhum lugar com visão livre para o gafanhoto");
        return default;
    }

    static Grasshopper Spawn(Vector3 at) =>
        Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), at, Quaternion.identity).GetComponent<Grasshopper>();

    static IEnumerator WaitUntil(System.Func<bool> condition, float timeout)
    {
        for (float end = Time.time + timeout; Time.time < end && !condition();) yield return null;
    }

    [UnityTest]
    public IEnumerator SaltaDeLongeComAvisoEBateForte()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        var health = player.GetComponent<Health>();
        health.RegenPerSecond = 0f;
        var g = Spawn(ClearSpot(player.transform, 14f));
        float hp0 = health.Current;

        yield return WaitUntil(() => g.IsTelegraphingLeap, 5f);
        Assert.IsTrue(g.IsTelegraphingLeap, "começou o aviso do salto");
        var telegraph = (Renderer)new SerializedObject(g).FindProperty("leapTelegraph").objectReferenceValue;
        Assert.IsTrue(telegraph != null && telegraph.enabled, "círculo vermelho aparece");
        Assert.Less(Vector3.Distance(g.LeapTarget, player.transform.position), 3f, "mira no player");

        yield return WaitUntil(() => g.Leaps >= 1 && !g.IsLeaping, 4f);
        Debug.Log($"[Smoke] gafanhoto: player {hp0:0} -> {health.Current:0}, pousos que acertaram {g.LeapsLanded}");
        Assert.AreEqual(1, g.LeapsLanded, "o pouso acertou");
        Assert.Less(health.Current, hp0 - 15f, "bate forte");
        Assert.IsTrue(g.IsStunned, "fica tonto depois de pousar");
        Assert.IsFalse(telegraph.enabled, "círculo some");

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PontoDeQuedaTravaEDaParaSair()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        var health = player.GetComponent<Health>();
        health.RegenPerSecond = 0f;
        var g = Spawn(ClearSpot(player.transform, 14f));

        yield return WaitUntil(() => g.IsTelegraphingLeap && g.LeapLocked, 6f);
        Assert.IsTrue(g.LeapLocked, "o ponto de queda travou");
        Vector3 locked = g.LeapTarget;
        float hp0 = health.Current;
        // sai do círculo para o lado
        Vector3 side = Vector3.Cross(Vector3.up, (locked - g.transform.position).normalized);
        Vector3 escape = player.transform.position + side * 8f;
        if (NavMesh.SamplePosition(escape, out var hit, 3f, NavMesh.AllAreas)) escape = hit.position;
        Teleport(player, escape);

        yield return WaitUntil(() => g.Leaps >= 1 && !g.IsLeaping && !g.IsTelegraphingLeap, 4f);
        Assert.AreEqual(0, g.LeapsLanded, "não acertou quem saiu");
        Assert.AreEqual(hp0, health.Current, 0.001f);
        Vector3 landed = g.transform.position; landed.y = locked.y;
        Assert.Less(Vector3.Distance(landed, locked), 1.5f, "caiu onde tinha travado");

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator DePertoViraDeCostasEDaCoice()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        var health = player.GetComponent<Health>();
        health.RegenPerSecond = 0f;
        Vector3 near = player.transform.position + player.transform.forward * 2f;
        if (NavMesh.SamplePosition(near, out var hit, 2f, NavMesh.AllAreas)) near = hit.position;
        var g = Spawn(near);
        float hp0 = health.Current;

        yield return WaitUntil(() => g.IsWindingUpKick, 3f);
        Assert.IsTrue(g.IsWindingUpKick, "armou o coice");
        yield return WaitUntil(() => g.KicksLanded >= 1, 2f);
        Debug.Log($"[Smoke] gafanhoto coice: player {hp0:0} -> {health.Current:0}");
        Assert.AreEqual(1, g.KicksLanded, "coice acertou");
        Assert.Less(health.Current, hp0 - 10f);

        yield return new ExitPlayMode();
    }
}
