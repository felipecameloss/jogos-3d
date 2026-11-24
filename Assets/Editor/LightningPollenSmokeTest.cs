using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Itens ao acertar do issue #18 (Test Runner > EditMode): o raio do fio elétrico pula entre
/// inimigos próximos e o pólen deixa confuso quem está na nuvem (chefe é imune).
/// </summary>
public class LightningPollenSmokeTest
{
    /// <summary>Abre a cena do jogo. O EnterPlayMode vem logo depois, no próprio teste:
    /// o runner de EditMode ignora essa instrução dentro de um helper.</summary>
    static void OpenMain()
    {
        // Erros de outros sistemas (partículas, agentes fora do NavMesh) não devem derrubar o teste
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
    }

    /// <summary>Já em Play Mode: espera a cena assentar e deixa só o que o teste criar.</summary>
    static IEnumerator PrepareRun()
    {
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;
        // Só os inimigos que o teste criar
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

    /// <summary>Cópia do item com chance de 100% (e outros campos ajustados no teste).</summary>
    static T Copy<T>(string asset, params (string field, float value)[] overrides) where T : ItemData
    {
        var item = Object.Instantiate(AssetDatabase.LoadAssetAtPath<T>($"Assets/ScriptableObjects/Items/{asset}.asset"));
        var so = new SerializedObject(item);
        so.FindProperty("chancePerStack").floatValue = 1f;
        foreach (var (field, value) in overrides)
        {
            var p = so.FindProperty(field);
            if (p.propertyType == SerializedPropertyType.Integer) p.intValue = (int)value;
            else p.floatValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    static EnemyBase Spawn(string prefab, Vector3 position)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/{prefab}.prefab");
        return Object.Instantiate(go, position, Quaternion.identity).GetComponent<EnemyBase>();
    }

    /// <summary>Acerto do player com dano quase zero: só para disparar os itens "ao acertar".</summary>
    static void PlayerHits(GameObject player, EnemyBase enemy) =>
        enemy.Health.TakeDamage(new DamageInfo(0.01f, player, enemy.Center, Vector3.forward));

    [UnityTest]
    public IEnumerator RaioPulaEntreInimigosProximos()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        var inv = player.GetComponent<Inventory>();
        inv.Add(Copy<ChainLightningOnHitItem>("FioEletrico", ("jumps", 2f)), 1, silent: true);

        // Quatro formigas em fila (3 m entre elas) e uma longe demais para o raio pular
        Vector3 start = player.transform.position + Vector3.forward * 8f;
        var ants = Enumerable.Range(0, 4).Select(i => Spawn("WorkerAnt", start + Vector3.right * (i * 3f))).ToArray();
        var far = Spawn("WorkerAnt", start + Vector3.left * 40f);
        yield return null;
        for (int i = 0; i < ants.Length; i++) ants[i].transform.position = start + Vector3.right * (i * 3f);
        far.transform.position = start + Vector3.left * 40f;
        float[] hp = ants.Select(a => a.Health.Current).ToArray();
        float farHp = far.Health.Current;

        PlayerHits(player, ants[0]);
        yield return null;

        Debug.Log($"[Smoke] raio: hp {string.Join(", ", hp)} -> {string.Join(", ", ants.Select(a => a.Health.Current))}");
        Assert.Less(ants[0].Health.Current, hp[0] - 1f, "o alvo também leva o raio");
        Assert.Less(ants[1].Health.Current, hp[1] - 1f, "1º pulo");
        Assert.Less(ants[2].Health.Current, hp[2] - 1f, "2º pulo");
        Assert.AreEqual(hp[3], ants[3].Health.Current, 0.001f, "só 2 pulos");
        Assert.AreEqual(farHp, far.Health.Current, 0.001f, "longe demais para pular");

        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PolenConfundeQuemEstaNaNuvemEPassa()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        var inv = player.GetComponent<Inventory>();
        inv.Add(Copy<PollenOnHitItem>("Polen", ("radius", 3.5f), ("cloudDuration", 2f), ("confuseDuration", 1.5f)), 1, silent: true);

        Vector3 start = player.transform.position + Vector3.forward * 8f;
        Vector3[] spots = { start, start + Vector3.right * 2f, start + Vector3.right * 14f, start + Vector3.left * 2f };
        var target = Spawn("WorkerAnt", spots[0]);
        var inside = Spawn("WorkerAnt", spots[1]);
        var outside = Spawn("WorkerAnt", spots[2]);
        var boss = Spawn("MantisBoss", spots[3]);
        yield return null;
        var all = new[] { target, inside, outside, boss };
        for (int i = 0; i < all.Length; i++) all[i].transform.position = spots[i];

        PlayerHits(player, target);
        yield return null;
        Assert.AreEqual(1, PollenCloud.Active.Count, "soltou uma nuvem");
        Assert.IsTrue(target.IsConfused, "alvo confuso");
        Assert.IsTrue(inside.IsConfused, "vizinho dentro da nuvem confuso");
        Assert.IsFalse(outside.IsConfused, "fora da nuvem");
        Assert.IsFalse(boss.IsConfused, "chefe é imune");
        Assert.IsFalse(StatusEffects.On(boss).ApplyConfusion(5f), "chefe recusa confusão");

        // A nuvem some (2s) e a confusão passa logo depois (1.5s após o último toque da nuvem)
        yield return Wait(2f + 1.5f + 0.5f);
        Assert.AreEqual(0, PollenCloud.Active.Count, "nuvem sumiu");
        Assert.IsTrue(Object.FindObjectsByType<PollenCloud>().Length == 0, "objeto da nuvem destruído");
        Assert.IsFalse(target.IsConfused, "confusão passou");

        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }
}
