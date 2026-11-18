using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Teste de fumaça do slot de equipamento + lupa (Window > General > Test Runner > EditMode).</summary>
public class EquipmentSmokeTest
{
    [UnityTest]
    public IEnumerator LupaEquipaUsaETroca()
    {
        // Erros de outros sistemas (partículas etc.) não devem derrubar este teste
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;

        var lupa = AssetDatabase.LoadAssetAtPath<MagnifierEquipment>("Assets/ScriptableObjects/Items/Lupa.asset");
        var player = GameObject.FindGameObjectWithTag("Player");
        var slot = player.GetComponent<EquipmentSlot>();
        var inv = player.GetComponent<Inventory>();
        var interactor = player.GetComponent<Interactor>();
        Assert.IsNotNull(slot, "slot");
        Assert.IsNull(slot.Current);
        Assert.IsTrue(player.GetComponents<IHudAbility>().Contains(slot));
        Assert.AreEqual(5, Object.FindAnyObjectByType<Hud>().AbilityCount, "HUD com 5 slots");

        // Equipa pelo inventário (como baú)
        inv.Add(lupa);
        Assert.AreEqual(lupa, slot.Current);
        Assert.AreEqual(0, inv.TotalItems, "não empilha");

        // Inimigo na linha da mira
        foreach (var e in EnemyBase.Active.ToArray()) Object.Destroy(e.gameObject);
        yield return null;
        var antPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/WorkerAnt.prefab");
        Vector3 dir = slot.AimPoint - slot.Chest; dir.y = 0f; dir.Normalize();
        var ant = Object.Instantiate(antPrefab, player.transform.position + dir * 7f, Quaternion.identity).GetComponent<EnemyBase>();
        yield return null;
        ant.transform.position = player.transform.position + dir * 7f;
        var cam = Camera.main.transform;
        float hp0 = ant.Health.Current;
        Assert.IsTrue(slot.TryUse(), "usou");
        for (float t = 0; t < 0.5f; t += Time.deltaTime) { cam.LookAt(ant.Center); yield return null; }
        Debug.Log($"[Smoke] ant hp {hp0} -> {(ant != null ? ant.Health.Current : -1)}");
        Assert.IsTrue(ant == null || ant.Health.Current < hp0, "lupa causou dano");
        Assert.IsFalse(slot.TryUse(), "em cooldown");
        Assert.Greater(slot.CooldownRemaining, 30f);

        // Troca: pega outro equipamento, o antigo cai no chão
        var lupa2 = Object.Instantiate(lupa); lupa2.name = "Lupa2";
        inv.Add(lupa2);
        Assert.AreEqual(lupa2, slot.Current);
        for (float end = Time.time + 1.3f; Time.time < end;) yield return null;
        var dropped = Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).FirstOrDefault(p => p.Item == lupa);
        Assert.IsNotNull(dropped, "antigo dropado");
        Assert.Less(Vector3.Distance(dropped.transform.position, player.transform.position), 4f);
        Assert.IsTrue(dropped.CanInteract(interactor), "pode trocar com E");
        Debug.Log($"[Smoke] prompt: {dropped.GetPrompt(interactor, out _)}");
        dropped.Interact(interactor);
        Assert.AreEqual(lupa, slot.Current, "trocou de volta");
        Assert.Greater(slot.CooldownRemaining, 25f, "cooldown não zera na troca");
        yield return null;
        Assert.IsTrue(Object.FindObjectsByType<ItemPickup>(FindObjectsSortMode.None).Any(p => p.Item == lupa2), "lupa2 dropada");

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PastilhaCuraENaoGastaComVidaCheia()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;

        var mint = AssetDatabase.LoadAssetAtPath<MintEquipment>("Assets/ScriptableObjects/Items/PastilhaDeMenta.asset");
        Assert.IsNotNull(mint, "asset da pastilha");
        Assert.IsNotNull(mint.Icon, "ícone da pastilha");
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/ScriptableObjects/Items/ItemDatabase.asset");
        CollectionAssert.Contains(db.Items, mint, "pastilha no banco (sai dos baús)");

        var player = GameObject.FindGameObjectWithTag("Player");
        var slot = player.GetComponent<EquipmentSlot>();
        var health = player.GetComponent<Health>();
        player.GetComponent<Inventory>().Add(mint);
        Assert.AreEqual(mint, slot.Current);

        // vida cheia: não usa e não gasta a recarga
        Assert.IsTrue(health.IsFullHealth);
        Assert.IsFalse(slot.TryUse(), "vida cheia: não usa");
        Assert.AreEqual(0f, slot.CooldownRemaining, "vida cheia: não gasta recarga");

        // ferido: cura 45% da vida máxima na hora
        health.SetCurrent(health.Max * 0.3f);
        float before = health.Current;
        Assert.IsTrue(slot.TryUse(), "ferido: usa");
        float healed = health.Current - before;
        Debug.Log($"[Smoke] menta curou {healed:0} de {health.Max:0} (recarga {slot.CooldownRemaining:0}s)");
        Assert.That(healed, Is.EqualTo(health.Max * 0.45f).Within(0.5f), "cura 45%");
        Assert.Greater(slot.CooldownRemaining, 40f, "recarga de 45s");
        Assert.IsFalse(slot.TryUse(), "em recarga");
        yield return new ExitPlayMode();
    }
}
