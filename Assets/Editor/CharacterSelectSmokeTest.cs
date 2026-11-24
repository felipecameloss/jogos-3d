using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// Teste de fumaça dos personagens jogáveis (issues #36/#37), também com modificadores de run ligados
/// (Window > General > Test Runner > EditMode).
/// </summary>
public class CharacterSelectSmokeTest
{
    static IEnumerator LoadMainWith(PlayerCharacterId id, RunModifier modifiers)
    {
        PlayerCharacters.Select(id);
        RunModifiers.SetActive(modifiers);
        SceneManager.LoadScene(SceneLoader.GameScene);
        for (int i = 0; i < 10; i++) yield return null;
        foreach (var s in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None)) s.enabled = false;
        foreach (var e in EnemyBase.Active.ToArray()) Object.Destroy(e.gameObject);
        yield return null;
    }

    static EnemyBase SpawnAntInFront(Transform player, float distance)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<EnemyBase>("Assets/Prefabs/Enemies/WorkerAnt.prefab");
        Vector3 dir = Camera.main.transform.forward; dir.y = 0f; dir.Normalize();
        Vector3 pos = player.position + dir * distance;
        if (NavMesh.SamplePosition(pos, out var hit, 2f, NavMesh.AllAreas)) pos = hit.position;
        return Object.Instantiate(prefab, pos, Quaternion.identity);
    }

    [UnityTest]
    public IEnumerator PersonagensEModificadores()
    {
        LogAssert.ignoreFailingMessages = true; // erros de outros sistemas não derrubam este teste
        var lastChoice = PlayerCharacters.LastChoice();
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 10; i++) yield return null;

        // ---- Cena aberta direto: Explorador, sem modificadores, kit original
        var player = GameObject.FindGameObjectWithTag("Player");
        Assert.AreEqual(PlayerCharacterId.Explorer, player.GetComponent<PlayerCharacter>().Id);
        Assert.AreEqual(RunModifier.None, RunModifiers.Active);
        Assert.IsTrue(player.GetComponent<ThornSlingshot>().enabled);
        Assert.AreEqual(5, Object.FindAnyObjectByType<Hud>().AbilityCount, "HUD do explorador");

        // ---- Besouro + inimigos rápidos + só amaldiçoados
        yield return LoadMainWith(PlayerCharacterId.Beetle, RunModifier.FastEnemies | RunModifier.CursedItemsOnly);
        player = GameObject.FindGameObjectWithTag("Player");
        var pc = player.GetComponent<PlayerCharacter>();
        var health = player.GetComponent<Health>();
        Assert.AreEqual(PlayerCharacterId.Beetle, pc.Id);
        Assert.IsNotNull(pc.Model, "modelo do besouro (Resources/PlayerBeetle)");
        Assert.IsNotNull(pc.Model.GetComponent<ProceduralInsectAnimator>());
        Assert.AreEqual(PlayerCharacters.Beetle.MaxHealth, health.Max, 0.01f);
        Assert.AreEqual(PlayerCharacters.Beetle.MaxHealth, health.Current, 0.01f);
        Assert.IsFalse(player.GetComponent<ThornSlingshot>().enabled, "besouro não usa estilingue");
        Assert.IsFalse(player.GetComponent<DashAbility>().enabled);
        var horn = player.GetComponent<BeetleHorn>();
        var charge = player.GetComponent<BeetleCharge>();
        var shell = player.GetComponent<BeetleShell>();
        var quake = player.GetComponent<BeetleQuake>();
        Assert.IsNotNull(horn); Assert.IsNotNull(charge); Assert.IsNotNull(shell); Assert.IsNotNull(quake);
        Assert.IsNotNull(charge.Action, "investida no M2");
        Assert.AreEqual(5, Object.FindAnyObjectByType<Hud>().AbilityCount, "HUD do besouro");

        // Chifrada acerta o inimigo à frente
        var ant = SpawnAntInFront(player.transform, 2f);
        yield return null;
        float hp0 = ant.Health.Current;
        horn.Swing();
        Assert.Contains(ant.Health, horn.LastHits, "chifrada acertou");
        Assert.Less(ant.Health.Current, hp0);

        // Modificador: inimigos 2x mais rápidos
        yield return null;
        var agent = ant.GetComponent<NavMeshAgent>();
        if (ant.IsAlive) Assert.Greater(agent.speed, 7.2f * 1.5f, "velocidade dobrada");

        // Carapaça reduz o dano
        Assert.IsTrue(shell.TryUse(), "carapaça");
        Assert.AreEqual(shell.DamageTaken, health.DamageTakenMultiplier, 0.001f);
        float before = health.Current;
        health.TakeDamage(new DamageInfo(20f));
        float taken = before - health.Current;
        Assert.Less(taken, 20f * 0.5f, "carapaça segurou o dano");

        // Terremoto
        if (ant != null && ant.IsAlive)
        {
            Assert.IsTrue(quake.TryUse(), "terremoto");
            Assert.Contains(ant.Health, quake.LastHits);
        }

        // Modificador: só itens amaldiçoados
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/ScriptableObjects/Items/ItemDatabase.asset");
        for (int i = 0; i < 30; i++)
            Assert.AreEqual(ItemRarity.Amaldicoado, db.RandomWeighted(76f, 19f, 1f, 4f, 7f).Rarity, "só amaldiçoados");

        // ---- Formiga, sem modificadores
        yield return LoadMainWith(PlayerCharacterId.Ant, RunModifier.None);
        player = GameObject.FindGameObjectWithTag("Player");
        pc = player.GetComponent<PlayerCharacter>();
        Assert.AreEqual(PlayerCharacterId.Ant, pc.Id);
        Assert.IsNotNull(pc.Model, "modelo da formiga (Resources/PlayerAnt)");
        Assert.AreEqual(PlayerCharacters.Ant.MaxHealth, player.GetComponent<Health>().Max, 0.01f);
        Assert.AreEqual(PlayerCharacters.Ant.WalkSpeed, player.GetComponent<PlayerMovement>().WalkSpeed, 0.01f);
        Assert.IsTrue(player.GetComponent<ThornSlingshot>().enabled);
        Assert.AreEqual(5, player.GetComponent<SummonAllies>().Count);
        Assert.AreEqual("Mordida", player.GetComponent<NeedleThrust>().HudName);
        Assert.IsNotNull(player.GetComponent<AntColonyCall>());
        Assert.AreEqual(5, Object.FindAnyObjectByType<Hud>().AbilityCount, "HUD da formiga");
        var db2 = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/ScriptableObjects/Items/ItemDatabase.asset");
        bool anyNotCursed = false;
        for (int i = 0; i < 60; i++) anyNotCursed |= db2.RandomWeighted().Rarity != ItemRarity.Amaldicoado;
        Assert.IsTrue(anyNotCursed, "sem o modificador volta ao normal");

        // Restaura a escolha do menu de quem rodou o teste
        PlayerCharacters.Select(lastChoice);
        RunModifiers.SetActive(RunModifier.None);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator MenuAbreNovaRun()
    {
        LogAssert.ignoreFailingMessages = true;
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        yield return new EnterPlayMode();
        LogAssert.ignoreFailingMessages = true;
        for (int i = 0; i < 5; i++) yield return null;

        var menu = Object.FindAnyObjectByType<MainMenu>();
        var root = menu.GetComponent<UIDocument>().rootVisualElement;
        Assert.IsFalse(menu.NewRunVisible);
        using (var submit = NavigationSubmitEvent.GetPooled())
        {
            var play = root.Q<Button>("play");
            submit.target = play;
            play.SendEvent(submit);
        }
        yield return null;
        Assert.IsTrue(menu.NewRunVisible, "Jogar abre a tela Nova run");
        Assert.AreEqual(PlayerCharacters.All.Length, menu.CharacterCardCount);
        Assert.IsNotNull(root.Q<Toggle>("mod-fast-enemies"), "modificadores continuam no menu");
        menu.Select(PlayerCharacterId.Beetle);
        Assert.AreEqual(PlayerCharacterId.Beetle, menu.SelectedCharacter);
        menu.CloseNewRun();
        Assert.IsFalse(menu.NewRunVisible);
        yield return new ExitPlayMode();
    }
}
