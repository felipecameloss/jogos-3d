using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>
/// Teste de fumaça do chefe aranha-rainha (issue #32): rodízio de chefes, teia que prende,
/// ninhada de aranhinhas, barra de chefe e drop lendário
/// (Window > General > Test Runner > EditMode).
/// </summary>
public class SpiderQueenSmokeTest
{
    static SpiderQueen Spawn(Transform player, float distance)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<SpiderQueen>(SpiderQueenBuilder.QueenPrefab);
        Vector3 pos = player.position + player.forward * distance;
        if (NavMesh.SamplePosition(pos, out var hit, 8f, NavMesh.AllAreas)) pos = hit.position;
        return Object.Instantiate(prefab, pos, Quaternion.identity);
    }

    [UnityTest]
    public IEnumerator RainhaPrendeInvocaENaFaseDois()
    {
        LogAssert.ignoreFailingMessages = true; // erros de outros sistemas não derrubam este teste
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;

        var playerGo = GameObject.FindGameObjectWithTag("Player");
        var player = playerGo.transform;
        var movement = playerGo.GetComponent<PlayerMovement>();
        var health = playerGo.GetComponent<Health>();
        health.Invincible = true; // o teste olha a teia, não a morte
        foreach (var s in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None)) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<StompDirector>(FindObjectsSortMode.None)) s.enabled = false;
        foreach (var e in EnemyBase.Active.ToArray()) Object.Destroy(e.gameObject);
        // Bolhas de sabão têm colisor sólido (os tiros estouram): uma no caminho segura a teia
        foreach (var s in Object.FindObjectsByType<SoapBubbleStream>(FindObjectsSortMode.None)) s.gameObject.SetActive(false);
        foreach (var b in Object.FindObjectsByType<DriftingBubble>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);
        yield return null;

        // ---- Rodízio: fase 1 louva-a-deus, fase 2 aranha-rainha
        var spawner = Object.FindAnyObjectByType<BossSpawner>();
        Assert.IsNotNull(spawner);
        Assert.IsInstanceOf<MantisBoss>(spawner.BossForStage(1), "fase 1 = louva-a-deus");
        Assert.IsInstanceOf<SpiderQueen>(spawner.BossForStage(2), "fase 2 = aranha-rainha");
        Assert.IsInstanceOf<MantisBoss>(spawner.BossForStage(3), "repete");

        // ---- Nasce: chefe na HUD (BossTracker), modelo animado, peças ligadas
        // A 24 m: dentro do alcance da teia (30), fora do pulo (18). Parada, ela só cospe teia,
        // então o teste não depende de onde o empurrão de um pulo joga o player.
        var queen = Spawn(player, 24f);
        queen.SpeedMultiplier = 0f;
        yield return null;
        Assert.IsTrue(queen.IsBoss);
        Assert.AreSame(queen, BossTracker.Current, "barra de chefe mostra a rainha");
        Assert.IsNotNull(queen.GetComponent<ProceduralInsectAnimator>());
        Assert.AreEqual(8, queen.GetComponent<ProceduralInsectAnimator>().LegCount, "8 pernas animadas");

        // ---- Teia: de longe ela cospe o leque e o player fica preso (15 s cobrem 3 rajadas)
        bool rooted = false;
        for (float end = Time.time + 15f; Time.time < end && !rooted; )
        {
            rooted |= movement.IsRooted;
            yield return null;
        }
        Debug.Log($"[Smoke] rainha: teias={queen.WebVolleys} pulos={queen.LeapsPerformed} ninhadas={queen.BroodsSummoned}");
        Assert.Greater(queen.WebVolleys, 0, "cuspiu teia");
        Assert.IsTrue(rooted, "teia prendeu o player");

        // ---- Ninhada: aranhinhas menores, com limite
        foreach (var s in queen.Brood.ToArray()) if (s != null) Object.Destroy(s.gameObject);
        yield return null;
        int born = queen.SummonBrood(3);
        Assert.AreEqual(3, born, "invocou 3 aranhinhas");
        var spiderPrefab = AssetDatabase.LoadAssetAtPath<Spider>("Assets/Prefabs/Enemies/Spider.prefab");
        foreach (var s in queen.Brood)
        {
            Assert.IsInstanceOf<Spider>(s);
            Assert.Less(s.transform.localScale.x, spiderPrefab.transform.localScale.x, "aranhinha é menor");
            Assert.Less(s.Health.Max, spiderPrefab.GetComponent<Health>().Max, "aranhinha é mais fraca");
        }
        queen.SummonBrood(10);
        Assert.LessOrEqual(queen.BroodAlive, 6, "respeita o máximo de aranhinhas vivas");

        // ---- Morte: ninhada morre junto, solta lendário, barra some
        var brood = queen.Brood.ToArray();
        queen.Health.Kill(playerGo);
        yield return null;
        foreach (var s in brood) Assert.IsTrue(s == null || !s.IsAlive, "ninhada morreu junto");
        Assert.IsNotNull(queen.DroppedItem, "soltou item");
        Assert.AreEqual(ItemRarity.Lendario, queen.DroppedItem.Item.Rarity, "item lendário");
        Assert.IsNull(BossTracker.Current, "sem chefe vivo");
        yield return new ExitPlayMode();
    }
}
