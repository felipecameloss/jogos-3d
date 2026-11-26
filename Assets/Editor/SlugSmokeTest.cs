using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>
/// Lesma (issue #26, Test Runner > EditMode): rasteja devagar deixando gosma e dá um bote com
/// aviso; em cima da gosma o player escorrega (demora a frear) e fora dela para na hora.
/// </summary>
public class SlugSmokeTest
{
    const string PrefabPath = "Assets/Prefabs/Enemies/Slug.prefab";

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

    static IEnumerator WaitUntil(System.Func<bool> condition, float timeout)
    {
        for (float end = Time.time + timeout; Time.time < end && !condition();) yield return null;
    }

    static void Teleport(GameObject player, Vector3 position)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = position;
        cc.enabled = true;
    }

    static float FlatSpeed(PlayerMovement m)
    {
        var v = m.Velocity;
        v.y = 0f;
        return v.magnitude;
    }

    [UnityTest]
    public IEnumerator RastejaDeixandoGosmaEDaBote()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        var health = player.GetComponent<Health>();
        health.RegenPerSecond = 0f;
        Vector3 at = player.transform.position + player.transform.forward * 7f;
        if (NavMesh.SamplePosition(at, out var hit, 3f, NavMesh.AllAreas)) at = hit.position;
        var slug = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), at, Quaternion.identity).GetComponent<Slug>();
        float d0 = Vector3.Distance(slug.transform.position, player.transform.position);
        float hp0 = health.Current;

        yield return Wait(1.5f);
        Assert.Greater(SlimeTrail.Count, 1, "deixa gosma por onde passa");
        Assert.Less(Vector3.Distance(slug.transform.position, player.transform.position), d0 - 1f, "vem atrás do player");

        yield return WaitUntil(() => slug.IsWindingUp, 6f);
        Assert.IsTrue(slug.IsWindingUp, "ergue a frente (aviso do bote)");
        yield return WaitUntil(() => slug.BitesLanded >= 1, 2f);
        Debug.Log($"[Smoke] lesma: bote {hp0:0} -> {health.Current:0}, manchas de gosma {SlimeTrail.Count}");
        Assert.AreEqual(1, slug.BitesLanded, "o bote acertou");
        Assert.Less(health.Current, hp0);

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator NaGosmaOPlayerEscorrega()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        var movement = player.GetComponent<PlayerMovement>();
        player.GetComponent<Health>().Invincible = true;

        // Lugar com uns 14 m livres na frente da câmera (para correr e deslizar sem bater em nada)
        var cam = Camera.main.transform;
        Vector3 fwd = cam.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 start = player.transform.position;
        for (int i = 0; i < 12; i++)
        {
            Vector3 p = player.transform.position + Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward * (i == 0 ? 0f : 8f);
            if (!NavMesh.SamplePosition(p, out var h, 2f, NavMesh.AllAreas)) continue;
            if (Physics.Raycast(h.position + Vector3.up, fwd, 14f, ~0, QueryTriggerInteraction.Ignore)) continue;
            start = h.position;
            break;
        }

        // Fora da gosma: corre e solta o direcional -> para quase na hora
        Teleport(player, start);
        yield return Wait(0.2f);
        movement.MoveInputOverride = new Vector2(0f, 1f);
        yield return Wait(0.8f);
        float top = FlatSpeed(movement);
        movement.MoveInputOverride = Vector2.zero;
        yield return Wait(0.3f);
        float stopOff = FlatSpeed(movement);
        Assert.IsFalse(movement.IsSlipping);

        // Na gosma: mesma corrida, solta em cima da gosma -> continua deslizando
        Teleport(player, start);
        yield return Wait(0.3f);
        movement.MoveInputOverride = new Vector2(0f, 1f);
        yield return Wait(0.8f);
        SlimeTrail.Drop(player.transform.position + fwd * 2f, 9f, 30f, null);
        yield return null;
        yield return null;
        Assert.IsTrue(movement.IsSlipping, "em cima da gosma escorrega");
        Assert.AreEqual(SlimeTrail.SlipGrip, movement.Grip, 0.001f);
        movement.MoveInputOverride = Vector2.zero;
        yield return Wait(0.3f);
        float stopOn = FlatSpeed(movement);
        Debug.Log($"[Smoke] lesma: corrida {top:0.0} m/s, 0,3 s depois de soltar: fora {stopOff:0.0}, na gosma {stopOn:0.0}");
        Assert.Less(stopOff, top * 0.2f, "fora da gosma freia logo");
        Assert.Greater(stopOn, top * 0.5f, "na gosma continua deslizando");

        // Saiu da gosma: volta ao normal
        Teleport(player, start - fwd * 25f);
        yield return Wait(0.2f);
        Assert.IsFalse(movement.IsSlipping);
        Assert.AreEqual(1f, movement.Grip, 0.001f);

        movement.MoveInputOverride = null;
        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }
}
