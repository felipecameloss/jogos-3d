using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monta o evento "criança com lupa" (issue #34):
/// - Materiais Assets/Art/Materials/SunGlow.mat (luz do sol concentrada) e LensGlass.mat
///   (vidro translúcido): cópias do VfxBeam (transparente).
/// - Prefab Assets/Prefabs/Hazards/SunSpot.prefab: lupa gigante (GiantMagnifier.fbx, de
///   Tools/Blender/build_giant_magnifier.py), coluna de luz e círculo no chão (o mesmo
///   disco da sombra do pisão humano).
/// - Cena Main: objeto "MagnifierEvent" com o MagnifierEvent.
/// Pode rodar de novo (atualiza no lugar). Menu: Jogo/Art/Criar evento da lupa.
/// Linha de comando: Unity -batchmode -quit -projectPath . -executeMethod MagnifierBuilder.Run
/// </summary>
public static class MagnifierBuilder
{
    const string StompPrefab = "Assets/Prefabs/Hazards/HumanStomp.prefab";
    public const string SpotPrefab = "Assets/Prefabs/Hazards/SunSpot.prefab";
    const string LensFbx = "Assets/Art/Models/Props/GiantMagnifier.fbx";
    const string BeamMaterial = "Assets/Art/Materials/VfxBeam.mat";
    const string GlowMaterialPath = "Assets/Art/Materials/SunGlow.mat";
    const string GlassMaterialPath = "Assets/Art/Materials/LensGlass.mat";
    const string MainScene = "Assets/Scenes/Main.unity";

    [MenuItem("Jogo/Art/Criar evento da lupa (lupa gigante, raio e cena)")]
    public static void Run()
    {
        var glow = CopyBeam(GlowMaterialPath, "SunGlow", new Color(1f, 0.93f, 0.5f, 0.85f));
        var glass = CopyBeam(GlassMaterialPath, "LensGlass", new Color(0.7f, 0.9f, 1f, 0.35f));
        BuildPrefab(glow, glass);
        AddToScene();
        AssetDatabase.SaveAssets();
        Debug.Log("[MagnifierBuilder] pronto");
    }

    static Material CopyBeam(string path, string name, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var beam = AssetDatabase.LoadAssetAtPath<Material>(BeamMaterial);
            if (beam == null) throw new System.Exception($"Material base não encontrado: {BeamMaterial}");
            mat = new Material(beam) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void BuildPrefab(Material glow, Material glass)
    {
        var stomp = AssetDatabase.LoadAssetAtPath<GameObject>(StompPrefab);
        Transform stompDisc = null;
        foreach (var t in stomp.GetComponentsInChildren<Transform>(true)) if (t.name == "Disc") stompDisc = t;
        if (stompDisc == null) throw new System.Exception("HumanStomp sem o disco da sombra (Disc)");
        var cylinder = stompDisc.GetComponent<MeshFilter>().sharedMesh; // cilindro padrão (altura 2, diâmetro 1)

        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(LensFbx);
        if (fbx == null) throw new System.Exception($"FBX da lupa não encontrado: {LensFbx} (rode Tools/Blender/build_giant_magnifier.py)");

        var root = new GameObject("SunSpot");
        try
        {
            var lens = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            lens.name = "Lens";
            foreach (var r in lens.GetComponentsInChildren<MeshRenderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // não tampa o próprio sol
                if (r.name == "Glass") r.sharedMaterial = glass;
            }

            var beam = NewCylinder("Beam", root.transform, cylinder, glow, Vector3.one);
            var spot = NewCylinder("Spot", root.transform, cylinder, glow, new Vector3(1f, 0.02f, 1f));

            var sun = root.AddComponent<SunSpot>();
            sun.EditorSetup(lens.transform, beam.transform, beam, spot.transform, spot);
            PrefabUtility.SaveAsPrefabAsset(root, SpotPrefab); // mesmo caminho: atualiza e mantém o GUID
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static MeshRenderer NewCylinder(string name, Transform parent, Mesh mesh, Material mat, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    static void AddToScene()
    {
        var scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
        AssetDatabase.ImportAsset(SpotPrefab, ImportAssetOptions.ForceSynchronousImport);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpotPrefab).GetComponent<SunSpot>();
        var ev = Object.FindAnyObjectByType<MagnifierEvent>();
        if (ev == null) ev = new GameObject("MagnifierEvent").AddComponent<MagnifierEvent>();
        ev.EditorSetup(prefab);
        EditorUtility.SetDirty(ev);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
