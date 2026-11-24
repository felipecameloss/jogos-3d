using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Esporo de cogumelo: cogumelo cura só quem está no círculo, some no fim e brota ao matar (Test Runner > EditMode).</summary>
public class MushroomSmokeTest
{
    const string ItemPath = "Assets/ScriptableObjects/Items/EsporoDeCogumelo.asset";

    static IEnumerator EnterMain()
    {
        // Erros de outros sistemas (partículas etc.) não devem derrubar este teste
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;
        foreach (var e in EnemyBase.Active.ToArray()) Object.Destroy(e.gameObject);
        foreach (var m in Object.FindObjectsByType<HealingMushroom>()) Object.Destroy(m.gameObject);
        yield return null;
    }

    static IEnumerator Wait(float seconds)
    {
        for (float end = Time.time + seconds; Time.time < end;) yield return null;
    }

    [UnityTest]
    public IEnumerator CogumeloCuraSoQuemEstaDentroESome()
    {
        yield return EnterMain();
        var player = GameObject.FindGameObjectWithTag("Player");
        var health = player.GetComponent<Health>();
        health.Invincible = true; // inimigos que nascerem no meio do teste não atrapalham a conta
        health.SetCurrent(health.Max * 0.5f);
        float hp0 = health.Current;
        var body = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/ItemOrb.mat");
        var area = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/AcidPool.mat");

        // Longe: não cura
        var far = HealingMushroom.Spawn(player.transform.position + Vector3.right * 20f, health, 3f, 10f, 0.1f, body, area);
        Assert.AreEqual(2, far.GetComponentsInChildren<MeshRenderer>().Length, "cogumelo + círculo");
        yield return Wait(0.8f);
        Assert.AreEqual(hp0, health.Current, 0.01f, "cogumelo longe não cura");

        // No pé do player: cura enquanto dura e depois some
        var near = HealingMushroom.Spawn(player.transform.position, health, 3f, 1.5f, 0.1f, body, area);
        Assert.IsTrue(near.IsInside(player.transform.position));
        yield return Wait(1f);
        Debug.Log($"[Smoke] cogumelo: hp {hp0} -> {health.Current}");
        Assert.Greater(health.Current, hp0 + 1f, "cogumelo perto cura");
        yield return Wait(0.8f);
        Assert.IsTrue(near == null, "cogumelo some no fim do tempo");
        Assert.IsFalse(HealingMushroom.Active.Contains(near));

        health.Invincible = false;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ItemBrotaCogumeloAoMatarERespeitaOMaximo()
    {
        yield return EnterMain();
        var player = GameObject.FindGameObjectWithTag("Player");
        var inv = player.GetComponent<Inventory>();
        player.GetComponent<Health>().Invincible = true;

        // Cópia do item com 100% de chance
        var item = Object.Instantiate(AssetDatabase.LoadAssetAtPath<MushroomOnKillItem>(ItemPath));
        var so = new SerializedObject(item);
        so.FindProperty("chancePerStack").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();
        int max = so.FindProperty("maxActive").intValue;
        inv.Add(item, 1, silent: true);

        var antPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/WorkerAnt.prefab");
        for (int i = 0; i < max + 1; i++)
        {
            Vector3 pos = player.transform.position + Quaternion.Euler(0f, i * 70f, 0f) * Vector3.forward * 6f;
            var ant = Object.Instantiate(antPrefab, pos, Quaternion.identity).GetComponent<EnemyBase>();
            yield return null;
            ant.transform.position = pos;
            ant.Health.Kill(player);
            yield return null;
            int expected = Mathf.Min(i + 1, max);
            Assert.AreEqual(expected, HealingMushroom.Active.Count, $"cogumelos ativos depois de {i + 1} kills");
            var newest = HealingMushroom.Active[HealingMushroom.Active.Count - 1];
            Vector3 d = newest.transform.position - ant.transform.position; d.y = 0f;
            Assert.Less(d.magnitude, 0.5f, "brota onde o inimigo morreu");
        }
        int alive = Object.FindObjectsByType<HealingMushroom>().Length;
        Debug.Log($"[Smoke] cogumelos: {HealingMushroom.Active.Count} ativos, {alive} na cena (o mais velho murchando)");
        Assert.IsTrue(Object.FindObjectsByType<HealingMushroom>().Any(m => m.IsWithering), "o mais velho murcha");

        inv.Remove(item);
        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }
}
