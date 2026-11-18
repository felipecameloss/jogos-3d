using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>Teste de fumaça da aranha (issue #56): pulo com dano e teia que prende o player.</summary>
public class SpiderSmokeTest
{
    static Spider Spawn(Transform player, float distance)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<Spider>("Assets/Prefabs/Enemies/Spider.prefab");
        Vector3 pos = player.position + player.forward * distance;
        if (NavMesh.SamplePosition(pos, out var hit, 6f, NavMesh.AllAreas)) pos = hit.position;
        return Object.Instantiate(prefab, pos, Quaternion.identity);
    }

    [UnityTest]
    public IEnumerator AranhaPulaEPrendeComTeia()
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
        foreach (var s in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None)) s.enabled = false;
        foreach (var e in EnemyBase.Active.ToArray()) Object.Destroy(e.gameObject);
        yield return null;

        // ---- Pulo: a 8 m ela agacha (aviso), salta e pousa no player
        var spider = Spawn(player, 8f);
        Assert.IsNotNull(spider.GetComponent<ProceduralInsectAnimator>());
        float hp0 = health.Current;
        bool sawTelegraph = false, sawAir = false;
        float maxHeight = 0f, groundY = spider.transform.position.y;
        for (float end = Time.time + 5f; Time.time < end && spider.Leaps == 0 || (spider.IsLeaping && Time.time < end + 2f);)
        {
            yield return null;
            sawTelegraph |= spider.IsTelegraphingLeap;
            if (spider.IsLeaping) { sawAir = true; maxHeight = Mathf.Max(maxHeight, spider.transform.position.y - groundY); }
        }
        for (int i = 0; i < 30; i++) yield return null;
        Debug.Log($"[Smoke] pulos={spider.Leaps} aviso={sawTelegraph} noAr={sawAir} altura={maxHeight:0.0} vida {hp0} -> {health.Current}");
        Assert.IsTrue(sawTelegraph, "aviso antes do pulo");
        Assert.IsTrue(sawAir && spider.Leaps >= 1, "pulou");
        Assert.Greater(maxHeight, 1f, "subiu no pulo");
        Assert.Less(health.Current, hp0, "pouso causou dano");
        Object.Destroy(spider.gameObject);
        health.Heal(1000f);
        for (float end = Time.time + 2f; Time.time < end;) yield return null; // passa a imunidade de teia

        // ---- Teia: a 16 m (longe para pular) ela cospe teia; o player parado fica preso
        spider = Spawn(player, 16f);
        bool rooted = false;
        for (float end = Time.time + 8f; Time.time < end && !rooted;)
        {
            yield return null;
            rooted |= movement.IsRooted;
        }
        Debug.Log($"[Smoke] teias={spider.Webs} preso={rooted}");
        Assert.GreaterOrEqual(spider.Webs, 1, "cuspiu teia");
        Assert.IsTrue(rooted, "teia prendeu o player");
        Assert.IsNotNull(Object.FindAnyObjectByType<WebPatch>(), "teia no chão");
        // imune logo depois de soltar
        for (float end = Time.time + 1.5f; Time.time < end && movement.IsRooted;) yield return null;
        Assert.IsFalse(movement.IsRooted, "solta depois de um instante");
        Assert.IsFalse(movement.Root(1f), "imunidade logo depois de soltar");

        yield return new ExitPlayMode();
    }
}
