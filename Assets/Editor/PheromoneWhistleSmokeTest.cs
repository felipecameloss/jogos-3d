using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

/// <summary>
/// Apito de feromônio (issue #24, Test Runner > EditMode): as formigas do mapa entram em fúria
/// e brigam entre si por alguns segundos (cupim fica de fora), depois voltam para o player; sem
/// formigas, o apito não gasta a recarga.
/// </summary>
public class PheromoneWhistleSmokeTest
{
    const string WhistlePath = "Assets/ScriptableObjects/Items/ApitoDeFeromonio.asset";

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

    static T Spawn<T>(string prefab, Vector3 at) where T : EnemyBase
    {
        if (NavMesh.SamplePosition(at, out var hit, 4f, NavMesh.AllAreas)) at = hit.position;
        return Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/{prefab}.prefab"), at, Quaternion.identity)
            .GetComponent<T>();
    }

    /// <summary>Cópia do apito com a fúria mais curta (o teste não precisa esperar 8s).</summary>
    static PheromoneWhistleEquipment ShortWhistle(float seconds)
    {
        var whistle = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PheromoneWhistleEquipment>(WhistlePath));
        var so = new SerializedObject(whistle);
        so.FindProperty("frenzyDuration").floatValue = seconds;
        so.ApplyModifiedPropertiesWithoutUndo();
        return whistle;
    }

    [UnityTest]
    public IEnumerator FormigasBrigamEntreSiEDepoisVoltamParaOPlayer()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        var health = player.GetComponent<Health>();
        health.RegenPerSecond = 0f; // a vida só muda se alguém morder o player
        var slot = player.GetComponent<EquipmentSlot>();
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<PheromoneWhistleEquipment>(WhistlePath), "asset do apito");
        var whistle = ShortWhistle(4f);
        slot.Equip(whistle, drop: false);

        // Duas operárias longe do player, perto uma da outra; um cupim (não é formiga) sem dano
        Vector3 p = player.transform.position;
        var a = Spawn<WorkerAnt>("WorkerAnt", p + Vector3.right * 16f);
        var b = Spawn<WorkerAnt>("WorkerAnt", p + Vector3.right * 16f + Vector3.forward * 3f);
        var termite = Spawn<Termite>("Termite", p - Vector3.right * 14f);
        termite.ApplyScaling(20f, 0f);
        yield return null;
        Assert.AreEqual(2, whistle.AntsInRange(p), "duas formigas no alcance (cupim não conta)");

        float hp0 = health.Current, a0 = a.Health.Current, b0 = b.Health.Current;
        Assert.IsTrue(slot.TryUse(), "apitou");
        yield return null;
        Assert.IsTrue(a.IsFrenzied && b.IsFrenzied, "as formigas entraram em fúria");
        Assert.IsFalse(termite.IsFrenzied, "o cupim não");
        yield return null;
        Assert.AreEqual(b, a.Rival, "a briga com b");
        Assert.AreEqual(a, b.Rival, "b briga com a");

        yield return Wait(2.5f);
        float lost = (a0 - (a != null ? a.Health.Current : 0f)) + (b0 - (b != null ? b.Health.Current : 0f));
        Debug.Log($"[Smoke] apito: formigas perderam {lost:0} de vida, mordidas {a.BitesLanded}+{b.BitesLanded}, player {hp0:0} -> {health.Current:0}");
        Assert.Greater(lost, 0f, "as formigas se morderam");
        Assert.Greater(a.BitesLanded + b.BitesLanded, 0);
        Assert.AreEqual(hp0, health.Current, 0.001f, "ninguém foi atrás do player");

        // Acaba a fúria: quem sobrou volta a ir atrás do player
        yield return Wait(2f);
        foreach (var ant in new EnemyBase[] { a, b })
        {
            if (ant == null || !ant.IsAlive) continue;
            Assert.IsFalse(ant.IsFrenzied, "fúria acabou");
            Assert.IsNull(ant.Rival, "sem rival");
        }

        Object.Destroy(whistle);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SemFormigasNaoGastaARecarga()
    {
        OpenMain();
        yield return new EnterPlayMode();
        yield return PrepareRun();
        var player = GameObject.FindGameObjectWithTag("Player");
        var slot = player.GetComponent<EquipmentSlot>();
        var whistle = AssetDatabase.LoadAssetAtPath<PheromoneWhistleEquipment>(WhistlePath);
        player.GetComponent<Inventory>().Add(whistle);
        Assert.AreEqual(whistle, slot.Current, "vai para o slot do Q");

        Assert.IsFalse(slot.TryUse(), "sem ninguém no mapa não apita");
        var termite = Spawn<Termite>("Termite", player.transform.position + Vector3.right * 14f);
        termite.ApplyScaling(20f, 0f);
        yield return null;
        Assert.IsFalse(slot.TryUse(), "só cupim também não");
        Assert.AreEqual(0f, slot.CooldownRemaining, 0.001f, "recarga intacta");

        Spawn<SoldierAnt>("SoldierAnt", player.transform.position + Vector3.right * 20f);
        yield return null;
        Assert.IsTrue(slot.TryUse(), "com uma formiga-soldado apita");
        Assert.Greater(slot.CooldownRemaining, 50f);

        yield return new ExitPlayMode();
    }
}
