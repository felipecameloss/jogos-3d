using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Todos os itens do banco carregam e têm ícone; na HUD o slot mostra o ícone em vez da sigla.</summary>
public class ItemIconsSmokeTest
{
    [Test]
    public void TodosOsItensDoBancoTemIcone()
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/ScriptableObjects/Items/ItemDatabase.asset");
        var missing = db.Items.Count(i => i == null);
        Assert.AreEqual(0, missing, "itens do banco que o Unity não carrega (.meta quebrado?)");
        var noIcon = db.Items.Where(i => i.Icon == null).Select(i => i.name).ToArray();
        Assert.IsEmpty(noIcon, "itens sem ícone: " + string.Join(", ", noIcon));
        Debug.Log($"[Smoke] banco: {db.Items.Count} itens, todos com ícone");
    }

    [UnityTest]
    public IEnumerator HudMostraIcone()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;
        var inv = GameObject.FindGameObjectWithTag("Player").GetComponent<Inventory>();
        var item = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/ScriptableObjects/Items/AsaDeMosca.asset");
        inv.Add(item, 2);
        yield return null;
        var hud = Object.FindAnyObjectByType<Hud>();
        string text = hud.ItemSlotText(hud.ItemSlotCount - 1);
        Debug.Log($"[Smoke] slot da HUD: '{text}'");
        Assert.AreEqual("x2", text.Trim(), "com ícone, o slot só mostra a contagem (sem a sigla)");
        yield return new ExitPlayMode();
    }
}
