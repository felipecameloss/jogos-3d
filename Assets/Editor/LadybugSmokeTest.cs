using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Joaninhas aliadas (issue #15, Test Runner > EditMode): a margarida vende com migalhas até o
/// máximo, as joaninhas orbitam o player e atiram no inimigo (dano do player, sem itens
/// "ao acertar"), e passam de fase.
/// </summary>
public class LadybugSmokeTest
{
    const string ConfigPath = "Assets/ScriptableObjects/LadybugConfig.asset";

    /// <summary>Abre a cena do jogo. O EnterPlayMode vem logo depois, no próprio teste:
    /// o runner de EditMode ignora essa instrução dentro de um helper.</summary>
    static void OpenMain()
    {
        // Erros de outros sistemas (partículas, agentes fora do NavMesh) não devem derrubar o teste
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }

    /// <summary>Já em Play Mode: espera a cena assentar e deixa só os inimigos que o teste criar.</summary>
    static IEnumerator PrepareRun()
    {
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;
        foreach (var s in Object.FindObjectsByType<EnemySpawner>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<BossSpawner>()) s.enabled = false;
        foreach (var e in EnemyBase.Active.ToArray()) Object.Destroy(e.gameObject);
        yield return null;
        GameObject.FindGameObjectWithTag("Player").GetComponent<Health>().Invincible = true;
    }

    static IEnumerator Wait(float seconds)
    {
        for (float end = Time.time + seconds; Time.time < end;) yield return null;
    }

    [UnityTest]
    public IEnumerator MargaridaVendeJoaninhasComMigalhasAteOMaximo()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var config = AssetDatabase.LoadAssetAtPath<LadybugConfig>(ConfigPath);
        var player = GameObject.FindGameObjectWithTag("Player");
        var interactor = player.GetComponent<Interactor>();
        var wallet = player.GetComponent<PlayerWallet>();
        var station = Object.FindAnyObjectByType<LadybugStation>();
        Assert.IsNotNull(station, "a margarida nasceu no mapa");

        // Sem migalhas: não vende
        wallet.TrySpend(wallet.Crumbs);
        station.Interact(interactor);
        Assert.AreEqual(0, player.TryGetComponent(out LadybugSwarm none) ? none.Count : 0, "sem migalhas, sem joaninha");
        Assert.AreEqual(0, station.Purchases);

        // Com migalhas: cobra o preço e o próximo fica mais caro
        wallet.Add(5000);
        int before = wallet.Crumbs, cost1 = station.CostFor(player);
        Assert.IsTrue(station.GetPrompt(interactor, out bool available).Contains(cost1.ToString()) && available);
        station.Interact(interactor);
        var swarm = player.GetComponent<LadybugSwarm>();
        Assert.IsNotNull(swarm);
        Assert.AreEqual(1, swarm.Count);
        Assert.AreEqual(before - cost1, wallet.Crumbs, "gastou o preço");
        Assert.Greater(station.CostFor(player), cost1, "a próxima é mais cara");

        // Até encher; cheio não cobra
        for (int i = 0; i < config.maxLadybugs + 3; i++) station.Interact(interactor);
        Assert.AreEqual(config.maxLadybugs, swarm.Count, "para no máximo");
        int crumbsFull = wallet.Crumbs;
        station.Interact(interactor);
        Assert.AreEqual(crumbsFull, wallet.Crumbs, "cheio não cobra");
        station.GetPrompt(interactor, out available);
        Assert.IsFalse(available, "aviso vermelho com o enxame cheio");
        Debug.Log($"[Smoke] joaninhas: {swarm.Count} compradas, 1ª custou {cost1}");

        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator JoaninhasOrbitamEAtiramNoInimigoMaisPerto()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var config = AssetDatabase.LoadAssetAtPath<LadybugConfig>(ConfigPath);
        var player = GameObject.FindGameObjectWithTag("Player");
        var swarm = LadybugSwarm.For(player);
        swarm.Add(config, player.transform.position + Vector3.up * 3f);
        swarm.Add(config, player.transform.position + Vector3.up * 3f);

        // Voam até a órbita em volta do player
        yield return Wait(1.5f);
        foreach (var l in swarm.Ladybugs)
        {
            Vector3 d = l.transform.position - player.transform.position;
            float flat = new Vector2(d.x, d.z).magnitude;
            Assert.Less(Mathf.Abs(flat - config.orbitRadius), 1f, "em volta do player");
            Assert.Less(Mathf.Abs(d.y - config.orbitHeight), 1f, "na altura da órbita");
        }

        // Inimigo perto: atiram nele; o dano é do player e não dispara itens "ao acertar"
        Vector3 spot = player.transform.position + Vector3.forward * 12f;
        var antPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/WorkerAnt.prefab");
        var ant = Object.Instantiate(antPrefab, spot, Quaternion.identity).GetComponent<EnemyBase>();
        yield return null;
        ant.transform.position = spot;
        float hp0 = ant.Health.Current;
        DamageInfo? shot = null;
        void Capture(Health victim, DamageInfo info) { if (ant != null && victim == ant.Health) shot = info; }
        Health.OnAnyDamaged += Capture;
        for (float end = Time.time + 4f; Time.time < end && shot == null;) yield return null;
        Health.OnAnyDamaged -= Capture;

        Debug.Log($"[Smoke] joaninhas: {swarm.TotalShots} tiros, formiga {hp0} -> {(ant != null ? ant.Health.Current : 0f)}");
        Assert.Greater(swarm.TotalShots, 0, "atiraram");
        Assert.IsTrue(shot.HasValue, "acertaram a formiga");
        Assert.AreEqual(player, shot.Value.Source, "o dano conta como do player");
        Assert.IsTrue(shot.Value.NoProc, "não dispara itens ao acertar");
        Assert.IsTrue(swarm.Ladybugs.Any(l => l.Target == ant), "miram no inimigo mais perto");

        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator JoaninhasPassamDeFase()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var config = AssetDatabase.LoadAssetAtPath<LadybugConfig>(ConfigPath);
        var player = GameObject.FindGameObjectWithTag("Player");
        var swarm = LadybugSwarm.For(player);
        for (int i = 0; i < 3; i++) swarm.Add(config, player.transform.position);

        RunManager.Instance.GoToNextStage();
        for (int i = 0; i < 15; i++) yield return null;
        LogAssert.ignoreFailingMessages = true;

        var newPlayer = GameObject.FindGameObjectWithTag("Player");
        Assert.AreNotEqual(player, newPlayer, "cena nova");
        Assert.AreEqual(2, RunManager.Instance.Stage);
        Assert.IsTrue(newPlayer.TryGetComponent(out LadybugSwarm restored), "enxame voltou");
        Assert.AreEqual(3, restored.Count, "mesmas 3 joaninhas");
        Assert.AreEqual(3, Object.FindObjectsByType<Ladybug>().Length, "sem joaninhas da fase anterior sobrando");

        RunManager.ClearCarryOver();
        yield return new ExitPlayMode();
    }
}
