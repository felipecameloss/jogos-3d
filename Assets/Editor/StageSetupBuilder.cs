using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Base das fases: o asset da fase 1 (Quintal, que deixa a cena como está), a sequência de fases
/// (Assets/ScriptableObjects/Stages/StageSequence.asset) e o objeto StageDirector na cena Main
/// ligado ao quintal e ao modelo do vaga-lume lanterna. Os builders de cada fase depois põem a
/// sua fase na sequência. Pode rodar de novo. Menu: Jogo/Fases/Preparar fases (quintal + sequência).
/// Linha de comando: Unity -batchmode -quit -projectPath . -executeMethod StageSetupBuilder.Run
/// </summary>
public static class StageSetupBuilder
{
    const string LanternModel = "Assets/Art/Models/Characters/Firefly.fbx";

    [MenuItem("Jogo/Fases/Preparar fases (quintal + sequência)")]
    public static void Run()
    {
        var quintal = StageBuilderUtils.Stage("Quintal", so =>
        {
            so.FindProperty("displayName").stringValue = "Quintal";
            so.FindProperty("description").stringValue = "O gramado do quintal, com o ciclo de dia e noite, o pisão, a chuva, a lupa e as bolhas.";
            so.FindProperty("useBackyard").boolValue = true;
            so.FindProperty("levelPrefab").objectReferenceValue = null;
            so.FindProperty("backyardAtmosphere").boolValue = true;
            so.FindProperty("lighting").enumValueIndex = (int)StageDefinition.LightingMode.DayNight;
            so.FindProperty("enemies").arraySize = 0; // a tabela da cena
            so.FindProperty("boss").objectReferenceValue = null; // o rodízio do BossSpawner
        });
        StageBuilderUtils.PutInSequence(quintal, 0);
        WireScene();
        Debug.Log("[StageSetupBuilder] pronto");
    }

    static void WireScene()
    {
        var scene = EditorSceneManager.OpenScene(StageBuilderUtils.MainScene, OpenSceneMode.Single);
        var director = Object.FindAnyObjectByType<StageDirector>(FindObjectsInactive.Include);
        if (director == null)
        {
            director = new GameObject("StageDirector").AddComponent<StageDirector>();
            SceneManager_MoveToRoot(director.gameObject);
        }
        var sequence = AssetDatabase.LoadAssetAtPath<StageSequence>(StageBuilderUtils.SequencePath);
        var backyard = GameObject.Find("Backyard");
        if (backyard == null) throw new System.Exception("Cena Main sem o objeto Backyard");
        director.EditorSetup(sequence, backyard, AssetDatabase.LoadAssetAtPath<GameObject>(LanternModel));
        EditorUtility.SetDirty(director);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void SceneManager_MoveToRoot(GameObject go)
    {
        go.transform.SetParent(null);
        go.transform.SetSiblingIndex(0);
    }
}
