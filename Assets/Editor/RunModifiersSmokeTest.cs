using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// Modificadores de run (Test Runner > EditMode): escolha salva e só vale ao começar a run,
/// só itens amaldiçoados, toggles no menu, inimigos 2x mais rápidos e chefe desde o início
/// da fase. Guarda a escolha de quem roda o teste e devolve no fim.
/// </summary>
public class RunModifiersSmokeTest
{
    int? backup;

    [SetUp]
    public void Backup()
    {
        backup = PlayerPrefs.HasKey(RunModifiers.PrefsKey) ? PlayerPrefs.GetInt(RunModifiers.PrefsKey) : null;
    }

    [TearDown]
    public void Restore()
    {
        RunModifiers.Chosen = (RunModifier)(backup ?? 0);
        if (backup == null) PlayerPrefs.DeleteKey(RunModifiers.PrefsKey);
        RunModifiers.SetActive(RunModifier.None);
        RunManager.ClearCarryOver();
    }

    [Test]
    public void EscolhaSalvaESoValeAoComecarARun()
    {
        RunModifiers.EndRun();
        RunModifiers.Chosen = RunModifier.None;
        RunModifiers.SetChosen(RunModifier.FastEnemies, true);
        RunModifiers.SetChosen(RunModifier.BossFromStart, true);
        Assert.AreEqual((int)(RunModifier.FastEnemies | RunModifier.BossFromStart), PlayerPrefs.GetInt(RunModifiers.PrefsKey), "salvo");
        Assert.IsFalse(RunModifiers.Is(RunModifier.FastEnemies), "marcar no menu não muda a run atual");

        RunModifiers.BeginRun();
        Assert.IsTrue(RunModifiers.Is(RunModifier.FastEnemies));
        Assert.IsTrue(RunModifiers.Is(RunModifier.BossFromStart));
        Assert.IsFalse(RunModifiers.Is(RunModifier.CursedItemsOnly));
        Assert.AreEqual(2f, RunModifiers.EnemySpeedMultiplier);

        RunModifiers.EndRun();
        Assert.AreEqual(RunModifier.None, RunModifiers.Active, "voltar ao menu desliga");
        Assert.AreEqual(1f, RunModifiers.EnemySpeedMultiplier);
        StringAssert.Contains("2x", RunModifiers.Describe(RunModifier.FastEnemies | RunModifier.CursedItemsOnly));
    }

    [Test]
    public void SoItensAmaldicoadosNosSorteios()
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/ScriptableObjects/Items/ItemDatabase.asset");
        RunModifiers.SetActive(RunModifier.CursedItemsOnly);
        for (int i = 0; i < 40; i++)
        {
            Assert.AreEqual(ItemRarity.Amaldicoado, db.RandomWeighted().Rarity, "baú comum");
            Assert.AreEqual(ItemRarity.Amaldicoado, db.RandomWeighted(0f, 0f, 1f).Rarity, "drop lendário");
        }
        RunModifiers.SetActive(RunModifier.None);
        for (int i = 0; i < 40; i++)
            Assert.AreNotEqual(ItemRarity.Amaldicoado, db.RandomWeighted().Rarity, "sem o modificador, o baú comum não dá amaldiçoado");
    }

    [UnityTest]
    public IEnumerator MenuMarcaModificadoresEMostraNoJogar()
    {
        RunModifiers.Chosen = RunModifier.None;
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 5; i++) yield return null;

        var menu = Object.FindAnyObjectByType<MainMenu>();
        var root = menu.GetComponent<UIDocument>().rootVisualElement;
        Assert.AreEqual("Jogar", menu.PlayText);
        Assert.IsFalse(menu.ModifiersVisible, "painel começa fechado");
        using (var submit = NavigationSubmitEvent.GetPooled())
        {
            var button = root.Q<Button>("modifiers-toggle");
            submit.target = button;
            button.SendEvent(submit);
        }
        yield return null;
        Assert.IsTrue(menu.ModifiersVisible, "botão abre o painel");

        root.Q<Toggle>("mod-fast-enemies").value = true;
        Assert.IsTrue(RunModifiers.IsChosen(RunModifier.FastEnemies));
        Assert.AreEqual("Jogar (1 modificador)", menu.PlayText);
        root.Q<Toggle>("mod-cursed-items").value = true;
        Assert.AreEqual("Jogar (2 modificadores)", menu.PlayText);
        root.Q<Toggle>("mod-fast-enemies").value = false;
        Assert.AreEqual(RunModifier.CursedItemsOnly, RunModifiers.Chosen);

        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator InimigosAndamDuasVezesMaisRapido()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;
        foreach (var s in Object.FindObjectsByType<EnemySpawner>()) s.enabled = false;
        foreach (var s in Object.FindObjectsByType<BossSpawner>()) s.enabled = false;
        foreach (var e in EnemyBase.Active.ToArray()) Object.Destroy(e.gameObject);
        var player = GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<Health>().Invincible = true;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/WorkerAnt.prefab");
        var ant = Object.Instantiate(prefab, player.transform.position + Vector3.forward * 20f, Quaternion.identity);
        var agent = ant.GetComponent<NavMeshAgent>();
        yield return null;
        yield return null;
        float normal = agent.speed;

        RunModifiers.SetActive(RunModifier.FastEnemies);
        yield return null;
        yield return null;
        Debug.Log($"[Smoke] modificador: velocidade da formiga {normal} -> {agent.speed}");
        Assert.AreEqual(normal * 2f, agent.speed, 0.01f, "2x mais rápida");

        RunModifiers.SetActive(RunModifier.None);
        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator ChefeNasceNoComecoDaFaseECanoNaoChamaOutro()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 5; i++) yield return null;

        // Liga o modificador e recomeça a run (como se tivesse vindo do menu)
        RunModifiers.SetActive(RunModifier.BossFromStart);
        RunManager.Instance.RestartRun();
        for (int i = 0; i < 10; i++) yield return null;
        LogAssert.ignoreFailingMessages = true;
        foreach (var s in Object.FindObjectsByType<EnemySpawner>()) s.enabled = false;
        foreach (var e in EnemyBase.Active.ToArray()) Object.Destroy(e.gameObject);
        var player = GameObject.FindGameObjectWithTag("Player");
        player.GetComponent<Health>().Invincible = true;
        StringAssert.Contains("Chefe", Object.FindAnyObjectByType<Hud>().ModifiersText, "HUD mostra o modificador");

        for (float end = Time.time + 8f; Time.time < end && MantisBoss.Current == null;) yield return null;
        var boss = MantisBoss.Current;
        Assert.IsNotNull(boss, "chefe nasceu sem ativar o cano");
        Assert.AreEqual(ExitPipe.State.Idle, ExitPipe.Instance.Current, "cano nem foi ativado");
        Assert.AreEqual(1, ExitPipe.Instance.BlockersAlive, "o cano espera o chefe morrer");
        float dist = Vector3.Distance(boss.transform.position, player.transform.position);
        Debug.Log($"[Smoke] modificador: chefe nasceu a {dist:0.0} m do player");
        Assert.Less(dist, 45f, "perto do player");

        ExitPipe.Instance.Activate();
        yield return null;
        for (float end = Time.time + 3f; Time.time < end;) yield return null;
        Assert.AreEqual(1, Object.FindObjectsByType<MantisBoss>().Length, "ativar o cano não chama outro chefe");

        player.GetComponent<Health>().Invincible = false;
        yield return new ExitPlayMode();
    }
}
