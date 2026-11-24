using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// Diário de descobertas: salva e recarrega, catálogo cobre todos os inimigos, tela do menu mostra
/// descobertos e ocultos, e o jogo registra item pego e inimigo visto (Test Runner > EditMode).
/// Guarda o diário de quem roda o teste e devolve no fim.
/// </summary>
public class DiscoveryJournalSmokeTest
{
    const string CatalogPath = "Assets/ScriptableObjects/DiscoveryCatalog.asset";
    string backup;

    [SetUp]
    public void Backup()
    {
        backup = PlayerPrefs.HasKey(DiscoveryJournal.PrefsKey) ? PlayerPrefs.GetString(DiscoveryJournal.PrefsKey) : null;
    }

    [TearDown]
    public void Restore()
    {
        if (backup == null) PlayerPrefs.DeleteKey(DiscoveryJournal.PrefsKey);
        else PlayerPrefs.SetString(DiscoveryJournal.PrefsKey, backup);
        PlayerPrefs.Save();
        DiscoveryJournal.Load();
    }

    static DiscoveryCatalog Catalog => AssetDatabase.LoadAssetAtPath<DiscoveryCatalog>(CatalogPath);

    [Test]
    public void DiarioSalvaERecarrega()
    {
        var item = Catalog.Items[0];
        DiscoveryJournal.Clear();
        Assert.IsFalse(DiscoveryJournal.IsItemDiscovered(item));
        Assert.IsTrue(DiscoveryJournal.DiscoverItem(item), "novo");
        Assert.IsFalse(DiscoveryJournal.DiscoverItem(item), "repetido não conta de novo");
        var copy = Object.Instantiate(item); // "Nome(Clone)"
        Assert.IsFalse(DiscoveryJournal.DiscoverItem(copy), "cópia conta como o mesmo item");
        Object.DestroyImmediate(copy);
        Assert.AreEqual(1, DiscoveryJournal.ItemCount);
        DiscoveryJournal.DiscoverEnemy("Spider");

        DiscoveryJournal.Load(); // relê do PlayerPrefs
        Assert.IsTrue(DiscoveryJournal.IsItemDiscovered(item));
        Assert.IsTrue(DiscoveryJournal.IsEnemyDiscovered("Spider"));
        Assert.IsFalse(DiscoveryJournal.IsEnemyDiscovered("WorkerAnt"));
        StringAssert.Contains("Spider", PlayerPrefs.GetString(DiscoveryJournal.PrefsKey));

        // Salvo corrompido: começa vazio sem quebrar
        PlayerPrefs.SetString(DiscoveryJournal.PrefsKey, "{isso não é json");
        LogAssert.ignoreFailingMessages = true;
        DiscoveryJournal.Load();
        Assert.AreEqual(0, DiscoveryJournal.ItemCount + DiscoveryJournal.EnemyCount);
    }

    [Test]
    public void CatalogoTemTodosOsInimigosEItens()
    {
        var catalog = Catalog;
        Assert.IsNotNull(catalog, "catálogo carrega");
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/ScriptableObjects/Items/ItemDatabase.asset");
        Assert.AreEqual(db.Items.Count, catalog.Items.Count, "itens vêm do banco de itens");

        var ids = catalog.Enemies.Select(e => e.id).ToArray();
        Assert.AreEqual(ids.Length, ids.Distinct().Count(), "ids repetidos");
        var enemyTypes = TypeCache.GetTypesDerivedFrom<EnemyBase>().Where(t => !t.IsAbstract).Select(t => t.Name).ToArray();
        var missing = enemyTypes.Except(ids).ToArray();
        Assert.IsEmpty(missing, "inimigos fora do diário: " + string.Join(", ", missing));
        var noIcon = catalog.Enemies.Where(e => e.icon == null).Select(e => e.id).ToArray();
        Assert.IsEmpty(noIcon, "inimigos sem retrato: " + string.Join(", ", noIcon));
        Assert.IsTrue(catalog.Enemies.All(e => !string.IsNullOrEmpty(e.displayName) && !string.IsNullOrEmpty(e.description)));
    }

    [UnityTest]
    public IEnumerator TelaDoMenuMostraDescobertosEOcultos()
    {
        var catalog = Catalog;
        DiscoveryJournal.Clear();
        DiscoveryJournal.DiscoverItem(catalog.Items[0]);
        DiscoveryJournal.DiscoverEnemy("WorkerAnt");

        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 5; i++) yield return null;

        // Abre pelo botão do menu (Enter/A no botão focado = NavigationSubmit)
        var menuRoot = Object.FindAnyObjectByType<MainMenu>().GetComponent<UIDocument>().rootVisualElement;
        var button = menuRoot.Q<Button>("journal");
        Assert.IsNotNull(button, "botão do diário no menu");
        using (var submit = NavigationSubmitEvent.GetPooled())
        {
            submit.target = button;
            button.SendEvent(submit);
        }
        yield return null;
        var screen = Object.FindAnyObjectByType<DiscoveryJournalScreen>();
        Assert.IsTrue(DiscoveryJournalScreen.IsOpen, "abriu pelo botão");

        int items = catalog.Items.Count(i => i != null), enemies = catalog.Enemies.Count;
        Assert.AreEqual(items + enemies, screen.CardCount);
        Assert.AreEqual($"1/{items}", screen.ItemCountText);
        Assert.AreEqual($"1/{enemies}", screen.EnemyCountText);
        Assert.IsFalse(screen.IsItemCardHidden(0));
        Assert.AreEqual(catalog.Items[0].DisplayName, screen.ItemCardName(0));
        Assert.IsTrue(screen.IsItemCardHidden(1));
        Assert.AreEqual("???", screen.ItemCardName(1));
        int worker = catalog.Enemies.ToList().FindIndex(e => e.id == "WorkerAnt");
        int spider = catalog.Enemies.ToList().FindIndex(e => e.id == "Spider");
        Assert.IsFalse(screen.IsEnemyCardHidden(worker));
        Assert.IsTrue(screen.IsEnemyCardHidden(spider));
        Assert.AreEqual("???", screen.EnemyCardName(spider));
        Debug.Log($"[Smoke] diário: itens {screen.ItemCountText}, inimigos {screen.EnemyCountText}");

        screen.Close();
        Assert.IsFalse(DiscoveryJournalScreen.IsOpen);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator JogoRegistraItemPegoEInimigoVisto()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;

        // Sem spawner e sem inimigos: só os que o teste criar
        foreach (var s in Object.FindObjectsByType<EnemySpawner>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<BossSpawner>()) s.enabled = false;
        foreach (var e in EnemyBase.Active.ToArray()) Object.Destroy(e.gameObject);
        yield return null;
        DiscoveryJournal.Clear();

        var player = GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<Health>().Invincible = true;

        // Item: conta ao pegar
        var item = Catalog.Items.First(i => i is not EquipmentData);
        player.GetComponent<Inventory>().Add(item);
        Assert.IsTrue(DiscoveryJournal.IsItemDiscovered(item), "item pego entra no diário");

        // Inimigo perto: visto
        var worker = Spawn("WorkerAnt", player.transform.position + Vector3.forward * 6f);
        // Inimigo longe: não visto; morto por outra coisa continua não visto
        var spider = Spawn("Spider", player.transform.position + Vector3.back * (EnemyBase.DiscoverDistance + 15f));
        // Inimigo longe morto pelo player: visto
        var aphid = Spawn("Aphid", player.transform.position + Vector3.right * (EnemyBase.DiscoverDistance + 15f));
        for (int i = 0; i < 3; i++) yield return null;
        Assert.IsTrue(DiscoveryJournal.IsEnemyDiscovered("WorkerAnt"), "perto do player");
        Assert.IsFalse(DiscoveryJournal.IsEnemyDiscovered("Spider"), "longe do player");
        spider.Health.Kill(null);
        aphid.Health.Kill(player);
        yield return null;
        Assert.IsFalse(DiscoveryJournal.IsEnemyDiscovered("Spider"), "morreu longe, sem ser pelo player");
        Assert.IsTrue(DiscoveryJournal.IsEnemyDiscovered("Aphid"), "morto pelo player de longe");
        Assert.AreEqual(worker.JournalId, "WorkerAnt");

        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }

    static EnemyBase Spawn(string prefab, Vector3 position)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/{prefab}.prefab");
        var enemy = Object.Instantiate(go, position, Quaternion.identity).GetComponent<EnemyBase>();
        enemy.transform.position = position;
        return enemy;
    }
}
