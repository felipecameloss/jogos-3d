using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monta o chefe aranha-rainha (issue #32) a partir da aranha comum:
/// - Prefab Assets/Prefabs/Enemies/SpiderQueen.prefab: cópia do Spider.prefab com o componente
///   SpiderQueen no lugar do Spider (mesma teia, mesmo aviso de pulo, a aranha comum como
///   aranhinha), maior, com vida/recompensa de chefe e o modelo SpiderQueen.fbx
///   (Tools/Blender/build_spider_queen.py) ligado pelo InsectModelBinder.
/// - Diário: entrada "SpiderQueen" no DiscoveryCatalog com o retrato de Assets/Art/Icons/Enemies.
/// - Cena Main: rodízio de chefes do BossSpawner = louva-a-deus (fase 1), aranha-rainha (fase 2)...
/// Pode rodar de novo (atualiza o prefab no lugar, mantendo o GUID). Menu: Jogo/Art/Criar aranha-rainha.
/// Linha de comando: Unity -batchmode -quit -projectPath . -executeMethod SpiderQueenBuilder.Run
/// </summary>
public static class SpiderQueenBuilder
{
    const string SpiderPrefab = "Assets/Prefabs/Enemies/Spider.prefab";
    const string MantisPrefab = "Assets/Prefabs/Enemies/MantisBoss.prefab";
    public const string QueenPrefab = "Assets/Prefabs/Enemies/SpiderQueen.prefab";
    const string QueenFbx = "Assets/Art/Models/Characters/SpiderQueen.fbx";
    const string QueenIcon = "Assets/Art/Icons/Enemies/SpiderQueen.png";
    const string CatalogPath = "Assets/ScriptableObjects/DiscoveryCatalog.asset";
    const string MainScene = "Assets/Scenes/Main.unity";

    /// <summary>Tamanho da rainha em relação à aranha comum.</summary>
    public const float QueenScale = 1.9f;

    [MenuItem("Jogo/Art/Criar aranha-rainha (prefab, diário e rodízio de chefes)")]
    public static void Run()
    {
        BuildPrefab();
        AddToCatalog();
        AddToBossRotation();
        AssetDatabase.SaveAssets();
        Debug.Log("[SpiderQueenBuilder] pronto");
    }

    static void BuildPrefab()
    {
        // Já existe: atualiza no lugar (mantém o GUID que a cena referencia); senão copia da aranha
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(QueenPrefab) != null;
        if (!exists && !AssetDatabase.CopyAsset(SpiderPrefab, QueenPrefab))
            throw new System.Exception($"Não deu para copiar {SpiderPrefab}");

        var spiderling = AssetDatabase.LoadAssetAtPath<EnemyBase>(SpiderPrefab);
        var mantis = AssetDatabase.LoadAssetAtPath<MantisBoss>(MantisPrefab);
        var mantisSo = new SerializedObject(mantis);
        var database = mantisSo.FindProperty("database").objectReferenceValue as ItemDatabase;
        var pickup = mantisSo.FindProperty("pickupPrefab").objectReferenceValue as ItemPickup;

        using (var scope = new PrefabUtility.EditPrefabContentsScope(QueenPrefab))
        {
            var root = scope.prefabContentsRoot;
            root.name = "SpiderQueen";
            // Teia, poça de teia e aviso do pulo: os da aranha comum (ou os que a rainha já tem)
            // (sem ?? : no Editor, GetComponent ausente devolve um "falso nulo" do Unity)
            Component source = root.GetComponent<Spider>();
            if (source == null) source = root.GetComponent<SpiderQueen>();
            var sourceSo = new SerializedObject(source);
            var web = sourceSo.FindProperty("webPrefab").objectReferenceValue as WebShot;
            var patch = sourceSo.FindProperty("webPatchPrefab").objectReferenceValue as WebPatch;
            var telegraph = sourceSo.FindProperty("leapTelegraph").objectReferenceValue as Renderer;
            if (source is Spider) Object.DestroyImmediate(source);

            var queen = root.GetComponent<SpiderQueen>();
            if (queen == null) queen = root.AddComponent<SpiderQueen>();
            // body/spinner: o InsectModelBinder liga quando troca o modelo (logo abaixo)
            queen.EditorSetup(web, patch, spiderling, null, telegraph, null, null, database, pickup);
            var qso = new SerializedObject(queen);
            qso.FindProperty("moveSpeed").floatValue = 7.6f;
            qso.FindProperty("crumbReward").intValue = 60;
            qso.FindProperty("despawnDelay").floatValue = 1.6f;
            qso.ApplyModifiedPropertiesWithoutUndo();

            var hso = new SerializedObject(root.GetComponent<Health>());
            hso.FindProperty("maxHealth").floatValue = 1400f;
            hso.FindProperty("armor").floatValue = 15f;
            hso.ApplyModifiedPropertiesWithoutUndo();

            root.transform.localScale = Vector3.one * QueenScale;
        }

        Debug.Log("[SpiderQueenBuilder] " + InsectModelBinder.Apply(QueenPrefab, QueenFbx));
    }

    static void AddToCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DiscoveryCatalog>(CatalogPath);
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(QueenIcon);
        if (icon == null) Debug.LogWarning($"[SpiderQueenBuilder] retrato não encontrado em {QueenIcon} (rode Tools/Blender/build_enemy_icons.py -- SpiderQueen)");
        var so = new SerializedObject(catalog);
        var enemies = so.FindProperty("enemies");
        SerializedProperty entry = null;
        for (int i = 0; i < enemies.arraySize; i++)
            if (enemies.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == nameof(SpiderQueen))
                entry = enemies.GetArrayElementAtIndex(i);
        if (entry == null)
        {
            enemies.arraySize++;
            entry = enemies.GetArrayElementAtIndex(enemies.arraySize - 1);
        }
        entry.FindPropertyRelative("id").stringValue = nameof(SpiderQueen);
        entry.FindPropertyRelative("displayName").stringValue = "Aranha-rainha";
        entry.FindPropertyRelative("description").stringValue =
            "Chefe. Cospe leques de teia que prendem, invoca aranhinhas e esmaga com pulos (o círculo vermelho marca onde cai).";
        entry.FindPropertyRelative("icon").objectReferenceValue = icon;
        entry.FindPropertyRelative("isBoss").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    static void AddToBossRotation()
    {
        var scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
        AssetDatabase.ImportAsset(QueenPrefab, ImportAssetOptions.ForceSynchronousImport);
        var queenGo = AssetDatabase.LoadAssetAtPath<GameObject>(QueenPrefab);
        var queen = queenGo != null ? queenGo.GetComponent<EnemyBase>() : null;
        if (queen == null) throw new System.Exception($"Prefab da rainha não carregou ({QueenPrefab})");
        var spawner = Object.FindAnyObjectByType<BossSpawner>();
        if (spawner == null) throw new System.Exception("Cena Main sem BossSpawner");
        var so = new SerializedObject(spawner);
        var mantis = so.FindProperty("bossPrefab").objectReferenceValue;
        var rotation = so.FindProperty("bossRotation");
        rotation.arraySize = 2;
        rotation.GetArrayElementAtIndex(0).objectReferenceValue = mantis;
        rotation.GetArrayElementAtIndex(1).objectReferenceValue = queen;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
