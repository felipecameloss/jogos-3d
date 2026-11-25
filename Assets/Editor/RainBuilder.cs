using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monta o evento de chuva (issue #33):
/// - Material Assets/Art/Materials/RainWater.mat: cópia do VfxBeam (transparente), azul-água.
/// - Prefab Assets/Prefabs/Hazards/RainDrop.prefab: a gota (RainDrop.fbx, de
///   Tools/Blender/build_raindrop.py) + a sombra de aviso (o mesmo disco/material da sombra
///   do pisão humano).
/// - Cena Main: objeto "RainEvent" com o RainEvent (riscos de chuva com o material Particle).
/// Pode rodar de novo (atualiza no lugar). Menu: Jogo/Art/Criar evento de chuva.
/// Linha de comando: Unity -batchmode -quit -projectPath . -executeMethod RainBuilder.Run
/// </summary>
public static class RainBuilder
{
    const string StompPrefab = "Assets/Prefabs/Hazards/HumanStomp.prefab";
    public const string DropPrefab = "Assets/Prefabs/Hazards/RainDrop.prefab";
    const string DropFbx = "Assets/Art/Models/Props/RainDrop.fbx";
    const string BeamMaterial = "Assets/Art/Materials/VfxBeam.mat";
    const string WaterMaterial = "Assets/Art/Materials/RainWater.mat";
    const string ParticleMaterial = "Assets/Art/Materials/Particle.mat";
    const string MainScene = "Assets/Scenes/Main.unity";

    [MenuItem("Jogo/Art/Criar evento de chuva (gota, material e cena)")]
    public static void Run()
    {
        var water = BuildMaterial();
        BuildPrefab(water);
        AddToScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[RainBuilder] pronto");
    }

    static Material BuildMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterial);
        if (mat == null)
        {
            var beam = AssetDatabase.LoadAssetAtPath<Material>(BeamMaterial);
            if (beam == null) throw new System.Exception($"Material base não encontrado: {BeamMaterial}");
            mat = new Material(beam) { name = "RainWater" };
            AssetDatabase.CreateAsset(mat, WaterMaterial);
        }
        mat.SetColor("_BaseColor", new Color(0.55f, 0.8f, 1f, 0.8f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void BuildPrefab(Material water)
    {
        // Sombra: o mesmo disco (malha + material) da sombra do pisão
        var stomp = AssetDatabase.LoadAssetAtPath<GameObject>(StompPrefab);
        var stompDisc = FindDeep(stomp.transform, "Disc");
        if (stompDisc == null) throw new System.Exception("HumanStomp sem o disco da sombra (Disc)");
        var discMesh = stompDisc.GetComponent<MeshFilter>().sharedMesh;
        var discMat = stompDisc.GetComponent<MeshRenderer>().sharedMaterial;

        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(DropFbx);
        if (fbx == null) throw new System.Exception($"FBX da gota não encontrado: {DropFbx} (rode Tools/Blender/build_raindrop.py)");

        var root = new GameObject("RainDrop");
        try
        {
            var shadow = new GameObject("Shadow").transform;
            shadow.SetParent(root.transform, false);
            shadow.localPosition = new Vector3(0f, 0.04f, 0f);
            var disc = new GameObject("Disc");
            disc.transform.SetParent(shadow, false);
            disc.transform.localScale = stompDisc.localScale;
            disc.AddComponent<MeshFilter>().sharedMesh = discMesh;
            var discR = disc.AddComponent<MeshRenderer>();
            discR.sharedMaterial = discMat;
            discR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            model.name = "Drop";
            var dropR = model.GetComponentInChildren<MeshRenderer>();
            dropR.sharedMaterial = water;
            dropR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var rain = root.AddComponent<RainDrop>();
            rain.EditorSetup(shadow, discR, model.transform, dropR);
            PrefabUtility.SaveAsPrefabAsset(root, DropPrefab); // mesmo caminho: atualiza e mantém o GUID
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static void AddToScene()
    {
        var scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
        AssetDatabase.ImportAsset(DropPrefab, ImportAssetOptions.ForceSynchronousImport);
        var drop = AssetDatabase.LoadAssetAtPath<GameObject>(DropPrefab).GetComponent<RainDrop>();
        var particle = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterial);
        var rain = Object.FindAnyObjectByType<RainEvent>();
        if (rain == null)
        {
            var go = new GameObject("RainEvent");
            // junto dos outros perigos, se houver
            var stompDirector = Object.FindAnyObjectByType<StompDirector>();
            if (stompDirector != null && stompDirector.transform.parent != null) go.transform.SetParent(stompDirector.transform.parent, false);
            rain = go.AddComponent<RainEvent>();
        }
        rain.EditorSetup(drop, particle);
        EditorUtility.SetDirty(rain);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }
}
