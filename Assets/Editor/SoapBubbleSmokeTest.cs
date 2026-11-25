using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Bolha de sabão (issue #21, Test Runner > EditMode): segurar pulo no ar flutua e desce devagar,
/// a bolha acaba no tempo, enche de novo no chão e estoura com dano; flutuando, a poça de ácido
/// passa por baixo; e as bolhas sopradas no quintal soltam migalhas e o item (a furta-cor).
/// </summary>
public class SoapBubbleSmokeTest
{
    const string ItemPath = "Assets/ScriptableObjects/Items/BolhaDeSabao.asset";

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

    /// <summary>Teleporta o player (o CharacterController precisa estar desligado para isso).</summary>
    static void Teleport(GameObject player, Vector3 position)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = position;
        cc.enabled = true;
    }

    static IEnumerator WaitGrounded(PlayerMovement movement, float timeout = 6f)
    {
        for (float end = Time.time + timeout; Time.time < end && !movement.IsGrounded;) yield return null;
    }

    [UnityTest]
    public IEnumerator SegurarPuloFlutuaDevagarAcabaEEnchedeNovo()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        var movement = player.GetComponent<PlayerMovement>();
        var health = player.GetComponent<Health>();
        health.Invincible = true;
        Vector3 ground = player.transform.position;

        // Sem o item: segurar pulo não faz nada
        Assert.IsFalse(movement.HasFloat);
        player.GetComponent<Inventory>().Add(AssetDatabase.LoadAssetAtPath<ItemData>(ItemPath));
        Assert.AreEqual(2f, movement.FloatTime, 0.001f, "2s de bolha com 1 stack");
        var bubble = player.GetComponent<PlayerBubble>();
        Assert.IsNotNull(bubble, "o item põe a bolha no player");

        // Caindo sem segurar: cai rápido
        Teleport(player, ground + Vector3.up * 14f);
        yield return Wait(0.6f);
        float freeFall = movement.Velocity.y;
        Assert.IsFalse(movement.IsFloating);
        yield return WaitGrounded(movement); // pousa: zera a velocidade da queda antes de subir de novo

        // Segurando: flutua e desce devagar
        Teleport(player, ground + Vector3.up * 14f);
        movement.HoldJumpOverride = true;
        yield return Wait(0.6f);
        Debug.Log($"[Smoke] bolha: queda livre {freeFall:0.0} m/s, flutuando {movement.Velocity.y:0.0} m/s");
        Assert.IsTrue(movement.IsFloating, "flutuando");
        Assert.Greater(movement.Velocity.y, -2.5f, "desce devagar");
        Assert.Less(freeFall, -8f, "sem a bolha cai rápido");

        // Acaba o tempo (2s): estoura e volta a cair rápido
        yield return Wait(1.7f);
        Assert.IsFalse(movement.IsFloating, "bolha acabou");
        Assert.AreEqual(0f, movement.FloatRemaining01, 0.001f);
        Assert.GreaterOrEqual(bubble.Pops, 1);

        // Pisa no chão: a bolha enche de novo
        movement.HoldJumpOverride = false;
        yield return WaitGrounded(movement);
        Assert.IsTrue(movement.IsGrounded, "pousou");
        Assert.AreEqual(1f, movement.FloatRemaining01, 0.001f, "enche de novo ao pisar no chão");

        // Levar dano flutuando estoura a bolha
        Teleport(player, ground + Vector3.up * 14f);
        movement.HoldJumpOverride = true;
        yield return Wait(0.4f);
        Assert.IsTrue(movement.IsFloating);
        health.Invincible = false;
        health.TakeDamage(new DamageInfo(1f, null));
        health.Invincible = true;
        yield return null;
        Assert.IsFalse(movement.IsFloating, "dano estoura a bolha");
        Assert.AreEqual(1, bubble.PoppedByDamage);

        movement.HoldJumpOverride = false;
        health.Invincible = false;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator FlutuandoAPocaDeAcidoPassaPorBaixo()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        var movement = player.GetComponent<PlayerMovement>();
        var health = player.GetComponent<Health>();
        player.GetComponent<Inventory>().Add(AssetDatabase.LoadAssetAtPath<ItemData>(ItemPath));
        Vector3 ground = player.transform.position;

        var pool = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AcidPool>("Assets/Prefabs/Enemies/AcidPool.prefab"), ground + Vector3.up * 0.03f, Quaternion.identity);
        pool.Begin(30f, null);

        // Flutua baixinho por cima da poça (dentro da área de dano) por ~1,2s: não leva dano
        Teleport(player, ground + Vector3.up * 2f);
        movement.HoldJumpOverride = true;
        yield return null;
        float hp0 = health.Current;
        yield return Wait(1.1f);
        Assert.IsTrue(movement.IsFloating, "ainda flutuando");
        Assert.AreEqual(hp0, health.Current, 0.001f, "flutuando, a poça passa por baixo");

        // Solta o pulo, pousa na poça: queima
        movement.HoldJumpOverride = false;
        yield return WaitGrounded(movement);
        yield return Wait(1.2f);
        Debug.Log($"[Smoke] bolha: poça de ácido {hp0} -> {health.Current}");
        Assert.Less(health.Current, hp0, "no chão, a poça queima");

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator BolhasDoQuintalSoltamMigalhaEAFurtaCorSoltaOItem()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<Health>().Invincible = true;
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(ItemPath);
        var stream = Object.FindAnyObjectByType<SoapBubbleStream>(FindObjectsInactive.Include);
        Assert.IsNotNull(stream, "spawner de bolhas na cena");

        stream.BlowNow(); // primeira leva da fase: sempre traz a furta-cor
        DriftingBubble rainbow = null;
        for (float end = Time.time + 6f; Time.time < end && rainbow == null;)
        {
            rainbow = DriftingBubble.Active.FirstOrDefault(b => b.IsRainbow);
            yield return null;
        }
        Assert.IsNotNull(rainbow, "veio uma bolha furta-cor");
        var plain = DriftingBubble.Active.First(b => !b.IsRainbow);
        Vector3 start = plain.transform.position;
        yield return Wait(0.5f);
        Assert.Greater(Vector3.Distance(start, plain.transform.position), 0.5f, "as bolhas andam com o vento");

        // Bolha comum estourada pelo player: migalha
        int crumbs = Object.FindObjectsByType<Crumb>().Length;
        plain.TakeDamage(new DamageInfo(1f, player));
        yield return null;
        Assert.Greater(Object.FindObjectsByType<Crumb>().Length, crumbs, "bolha comum solta migalha");

        // Furta-cor estourada pelo player: cai a Bolha de sabão
        rainbow.TakeDamage(new DamageInfo(1f, player));
        yield return null;
        Assert.AreEqual(1, stream.RainbowsPopped);
        Assert.IsTrue(Object.FindObjectsByType<ItemPickup>().Any(p => p.Item == item), "soltou o item");

        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }
}
