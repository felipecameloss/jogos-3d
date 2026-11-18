using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Liga os props do quintal (Tools/Blender/build_props.py, FBX em Assets/Art/Models/Props):
/// - Baús: a tampinha (ChestSmall vermelha, ChestLarge dourada) vira o visual, com Chest.lid
///   apontando para a peça Lid do FBX.
/// - Cena Main / Backyard: troca só a MALHA visual (e os materiais) das peças de jogo e da
///   decoração por versões dos props normalizadas para o tamanho da primitiva. Colisores e
///   NavMesh ficam intactos (nada de reassar). Rochas, latas deitadas, rampas/pontes de folha,
///   pedrinhas e lâminas de grama da decoração.
/// - BackyardAtmosphere: folhas caídas e pedras espalhadas como decoração (sem colisão).
/// Idempotente. Menu: Jogo/Art/Aplicar props do quintal.
/// </summary>
public static class PropsBinder
{
    const string Props = "Assets/Art/Models/Props/";
    const string Normalized = "Assets/Art/Models/Props/Normalized/";
    const string MainScene = "Assets/Scenes/Main.unity";

    [MenuItem("Jogo/Art/Aplicar props do quintal")]
    public static void Run()
    {
        var log = new StringBuilder();
        if (!AssetDatabase.IsValidFolder(Normalized.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Art/Models/Props", "Normalized");
        log.AppendLine(ApplyChest("Assets/Prefabs/Items/ChestSmall.prefab", "BottleCapChest"));
        log.AppendLine(ApplyChest("Assets/Prefabs/Items/ChestLarge.prefab", "BottleCapChestGold"));
        log.AppendLine(ApplyScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[PropsBinder]\n" + log);
    }

    // ---------------------------------------------------------------- baús

    static string ApplyChest(string prefabPath, string fbxName)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Props + fbxName + ".fbx");
        using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
        var root = scope.prefabContentsRoot.transform;
        var col = root.GetComponent<BoxCollider>();
        float diameter = col != null ? col.size.x : 1.6f;

        // tira o visual antigo (primitivas / Model anterior); mantém brilho, preço e colisores
        var remove = new List<GameObject>();
        foreach (Transform c in root)
            if (c.name != "Glow" && c.name != "CostLabel" && c.GetComponent<Collider>() == null) remove.Add(c.gameObject);
        foreach (var g in remove) Object.DestroyImmediate(g);

        var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root);
        model.name = "Model";
        var mt = model.transform;
        mt.localPosition = Vector3.zero;
        mt.localRotation = Quaternion.identity;
        float raw = MeshBounds(mt).size.x;
        float s = diameter / raw;
        mt.localScale = Vector3.one * s;

        var lid = mt.Find("Lid");
        var chest = root.GetComponent<Chest>();
        var so = new SerializedObject(chest);
        so.FindProperty("lid").objectReferenceValue = lid;
        so.ApplyModifiedPropertiesWithoutUndo();
        return $"{prefabPath}: tampinha {fbxName}, escala {s:0.###}, lid={(lid != null)}";
    }

    // ---------------------------------------------------------------- cena

    static string ApplyScene()
    {
        var scene = EditorSceneManager.OpenScene(MainScene);
        var backyard = GameObject.Find("Backyard");
        if (backyard == null) return "Backyard não encontrado";

        var unit = new Bounds(Vector3.zero, Vector3.one);
        var rocks = new[]
        {
            Norm("RockMossy", "Mesh", unit, Matrix4x4.identity, "RockMossy_Sphere"),
            Norm("RockSlab", "Mesh", unit, Matrix4x4.identity, "RockSlab_Sphere"),
            Norm("RockPebble", "Mesh", unit, Matrix4x4.identity, "RockPebble_Sphere"),
        };
        var pebble = rocks[2];
        var blade = Norm("GrassStalk", "Blade_1", unit, Matrix4x4.identity, "GrassBlade_Cube");
        // lata: no FBX fica deitada ao longo de X; o cilindro do Unity tem o eixo em Y (altura 2)
        var can = Norm("CrushedCan", "Mesh", new Bounds(Vector3.zero, new Vector3(1f, 2f, 1f)),
                       Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, 90f)), "CrushedCan_Cylinder");
        // folha: a nervura central fica na face de cima da rampa (onde se anda); bordas sobem
        var leaf = Norm("FallenLeaf", "Mesh", new Bounds(new Vector3(0f, 0.9f, 0f), Vector3.one), Matrix4x4.identity, "FallenLeaf_Ramp");

        int nRock = 0, nCan = 0, nLeaf = 0, nPebble = 0, nBlade = 0, nHidden = 0;
        foreach (var t in backyard.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            bool topLevel = t.parent == backyard.transform;
            bool decoration = t.parent != null && t.parent.name.StartsWith("Decoration");
            if (topLevel && (n.StartsWith("BigRock") || n == "Rock" || n.StartsWith("Rock ")))
            {
                Swap(t, n.StartsWith("BigRock") ? rocks[0] : rocks[nRock % rocks.Length]); nRock++;
            }
            else if (topLevel && (n.StartsWith("LeafRamp") || n.StartsWith("LeafBridge")))
            {
                Swap(t, leaf); nLeaf++;
                var vein = t.Find("Vein");
                if (vein != null && vein.TryGetComponent(out MeshRenderer vr) && vr.enabled) { vr.enabled = false; nHidden++; }
            }
            else if (t.parent != null && t.parent.name == "CanLying")
            {
                if (n == "Body") { Swap(t, can); nCan++; }
                else if (t.TryGetComponent(out MeshRenderer er) && er.enabled) { er.enabled = false; nHidden++; } // tampa antiga
            }
            else if (decoration && n == "Pebble") { Swap(t, pebble); nPebble++; }
            else if (decoration && n == "Blade") { Swap(t, blade); nBlade++; }
        }

        // decoração espalhada (sem colisão) pelo BackyardAtmosphere
        var atmo = Object.FindAnyObjectByType<BackyardAtmosphere>();
        string decor = "sem BackyardAtmosphere";
        if (atmo != null)
        {
            var so = new SerializedObject(atmo);
            so.FindProperty("fallenLeafPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Props + "FallenLeaf.fbx");
            var arr = so.FindProperty("rockDecorPrefabs");
            string[] names = { "RockPebble", "RockSlab", "RockMossy" };
            arr.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Props + names[i] + ".fbx");
            so.ApplyModifiedPropertiesWithoutUndo();
            decor = "folhas e pedras ligadas no BackyardAtmosphere";
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"Main: rochas {nRock}, latas {nCan}, folhas-rampa {nLeaf}, pedrinhas {nPebble}, grama {nBlade}, " +
               $"visuais antigos escondidos {nHidden}; {decor}";
    }

    struct NormMesh { public Mesh mesh; public Material[] materials; }

    static void Swap(Transform t, NormMesh m)
    {
        if (!t.TryGetComponent(out MeshFilter mf) || !t.TryGetComponent(out MeshRenderer mr)) return;
        mf.sharedMesh = m.mesh;
        mr.sharedMaterials = m.materials;
        EditorUtility.SetDirty(mf);
        EditorUtility.SetDirty(mr);
    }

    /// <summary>
    /// Cópia da malha `part` do FBX `fbxName`, girada por `pre` e esticada para caber em
    /// `target` (espaço local da primitiva que ela substitui). Salva como asset.
    /// </summary>
    static NormMesh Norm(string fbxName, string part, Bounds target, Matrix4x4 pre, string assetName)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Props + fbxName + ".fbx");
        Transform pt = null;
        foreach (var tr in fbx.GetComponentsInChildren<Transform>(true)) if (tr.name == part) { pt = tr; break; }
        var src = pt.GetComponent<MeshFilter>().sharedMesh;
        var mats = pt.GetComponent<MeshRenderer>().sharedMaterials;

        var verts = src.vertices;
        for (int i = 0; i < verts.Length; i++) verts[i] = pre.MultiplyPoint3x4(verts[i]);
        var b = new Bounds(verts[0], Vector3.zero);
        foreach (var v in verts) b.Encapsulate(v);
        Vector3 scale = new(target.size.x / b.size.x, target.size.y / b.size.y, target.size.z / b.size.z);
        for (int i = 0; i < verts.Length; i++)
            verts[i] = target.center + Vector3.Scale(verts[i] - b.center, scale);

        string path = $"{Normalized}{assetName}.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        mesh.Clear();
        mesh.name = Path.GetFileNameWithoutExtension(path);
        mesh.vertices = verts;
        mesh.subMeshCount = src.subMeshCount;
        for (int s = 0; s < src.subMeshCount; s++) mesh.SetTriangles(src.GetTriangles(s), s);
        mesh.RecalculateNormals(); // vértices já vêm separados por face (sombreamento facetado)
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return new NormMesh { mesh = mesh, materials = mats };
    }

    static Bounds MeshBounds(Transform root)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mb = mf.sharedMesh.bounds;
            var m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        return b;
    }
}
