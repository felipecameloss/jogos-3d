using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Aplica automaticamente os modelos 3D estilizados (.glb gerados no Blender) nos prefabs
/// de inimigos, itens, santuários, cano de saída e props do quintal na cena Main.
/// Preserva colisores, NavMeshAgents e referências de script existentes.
/// </summary>
[InitializeOnLoad]
public static class ArtMeshBinder
{
    const string StampPath = "ProjectSettings/ArtGLBBinder_v2.stamp";
    const string MarkerName = "__ArtGLB_v1";
    const string MainScenePath = "Assets/Scenes/Main.unity";

    static readonly string[] RequiredGlbs =
    {
        "Assets/Art/Models/Characters/WorkerAnt.glb",
        "Assets/Art/Models/Characters/SoldierAnt.glb",
        "Assets/Art/Models/Characters/Aphid.glb",
        "Assets/Art/Models/Characters/BombardierBeetle.glb",
        "Assets/Art/Models/Characters/Termite.glb",
        "Assets/Art/Models/Characters/MantisBoss.glb",
        "Assets/Art/Models/Characters/AllyAnt.glb",
        "Assets/Art/Models/Items/ChestSmall.glb",
        "Assets/Art/Models/Items/ChestLarge.glb",
        "Assets/Art/Models/Items/ChanceShrine.glb",
        "Assets/Art/Models/Items/BloodShrine.glb",
        "Assets/Art/Models/Items/Crumb.glb",
        "Assets/Art/Models/Items/ItemPickup.glb",
        "Assets/Art/Models/Environment/ExitPipe.glb",
        "Assets/Art/Models/Environment/TermiteMound.glb",
        "Assets/Art/Models/Environment/GardenRock.glb",
        "Assets/Art/Models/Environment/GiantLeaf.glb",
        "Assets/Art/Models/Environment/TerracottaPot.glb",
        "Assets/Art/Models/Environment/SodaCan.glb",
        "Assets/Art/Models/Environment/GrassTuft.glb",
        "Assets/Art/Models/Environment/Pebble.glb",
        "Assets/Art/Models/Environment/ToyBrickBase.glb",
        "Assets/Art/Models/Environment/FencePlank.glb",
        "Assets/Art/Models/Environment/TreeSilhouette.glb",
        "Assets/Art/Models/Environment/BushCluster.glb",
        "Assets/Art/Models/Environment/CloverPatch.glb",
        "Assets/Art/Models/Environment/DaisyFlower.glb",
        "Assets/Art/Models/Environment/MushroomCluster.glb",
        "Assets/Art/Models/Environment/TrowelProp.glb",
        "Assets/Art/Models/Environment/BottleCapProp.glb",
    };

    static ArtMeshBinder()
    {
        EditorApplication.delayCall += TryAutoApply;
    }

    public static void TryAutoApplyPublic() => TryAutoApply();

    static void TryAutoApply()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(StampPath)) return;
        if (!AllGlbsImported()) return;
        ApplyAll(interactive: false);
    }

    [MenuItem("Jogo/Art/Aplicar Modelos 3D (.glb)")]
    public static void MenuApplyAll() => ApplyAll(interactive: true);

    static bool AllGlbsImported()
    {
        foreach (string p in RequiredGlbs)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(p) == null)
                return false;
        return true;
    }

    public static void ApplyAll(bool interactive)
    {
        if (!AllGlbsImported())
        {
            if (interactive)
                Debug.LogWarning("[ArtMeshBinder] Nem todos os .glb foram importados ainda. Aguarde a importação do glTFast e tente novamente.");
            return;
        }

        int updated = 0;

        // 1. Aliado (Body sob Visual)
        // Operária, soldado, pulgão, besouro-bombardeiro e louva-a-deus usam os FBX de
        // Tools/Blender, ligados pelo InsectModelBinder (já salvos nos prefabs): não aplicar .glb neles.
        updated += ApplySimpleBug("Assets/Prefabs/Player/AllyAnt.prefab", "Assets/Art/Models/Characters/AllyAnt.glb");

        // 2. Cupim (+ BurrowMound)
        updated += ApplyTermite();

        // 5. Baús (+ lid)
        // Baús: tampinha de Tools/Blender/build_props.py, ligada pelo PropsBinder (não aplicar .glb).

        // 6. Santuários (+ crystal & crystalRenderer)
        updated += ApplyShrine("Assets/Prefabs/Items/ChanceShrine.prefab", "Assets/Art/Models/Items/ChanceShrine.glb");
        updated += ApplyShrine("Assets/Prefabs/Items/BloodShrine.prefab", "Assets/Art/Models/Items/BloodShrine.glb");

        // 7. Pickups & Cano de saída
        updated += ApplyCrumb();
        updated += ApplyItemPickup();
        updated += ApplyExitPipe();

        // 8. Props do quintal na cena Main
        updated += ApplyMainSceneEnvironment();

        AssetDatabase.SaveAssets();
        File.WriteAllText(StampPath, DateTime.UtcNow.ToString("O"));
        Debug.Log($"[ArtMeshBinder] Modelos 3D (.glb) aplicados com sucesso ({updated} alvos atualizados).");
    }

    static int ApplySimpleBug(string prefabPath, string glbPath)
    {
        var glb = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
        if (glb == null) return 0;

        using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
        var root = scope.prefabContentsRoot;
        if (HasMarker(root)) return 0;

        var body = FindDeep(root.transform, "Body") ?? FindDeep(root.transform, "Visual") ?? root.transform;
        DisablePrimitiveVisualChildren(body);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(glb, body);
        inst.name = MarkerName;
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = CompensateParentScale(root.transform, body);
        return 1;
    }

    static int ApplyTermite()
    {
        const string prefabPath = "Assets/Prefabs/Enemies/Termite.prefab";
        var glb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Characters/Termite.glb");
        var moundGlb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/TermiteMound.glb");
        if (glb == null) return 0;

        using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
        var root = scope.prefabContentsRoot;
        if (HasMarker(root)) return 0;

        var body = FindDeep(root.transform, "Body") ?? root.transform;
        DisablePrimitiveVisualChildren(body);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(glb, body);
        inst.name = MarkerName;
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = CompensateParentScale(root.transform, body);

        if (moundGlb != null)
        {
            var mound = FindDeep(root.transform, "BurrowMound");
            if (mound != null && TryGetFirstMesh(moundGlb, out var mMesh, out var mMats))
            {
                if (mound.TryGetComponent<MeshFilter>(out var mf)) mf.sharedMesh = mMesh;
                if (mound.TryGetComponent<MeshRenderer>(out var mr)) mr.sharedMaterials = mMats;
                mound.localScale = Vector3.one;
            }
        }
        return 1;
    }

    static int ApplyChest(string prefabPath, string glbPath)
    {
        var glb = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
        if (glb == null) return 0;

        using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
        var root = scope.prefabContentsRoot;
        if (HasMarker(root)) return 0;

        var keep = new HashSet<Transform>();
        if (FindDeep(root.transform, "Glow") is Transform glow) keep.Add(glow);
        if (FindDeep(root.transform, "CostLabel") is Transform lbl) keep.Add(lbl);

        foreach (Transform ch in root.transform)
        {
            if (keep.Contains(ch)) continue;
            if (ch.GetComponent<Collider>() == null && ch.GetComponent<UnityEngine.AI.NavMeshObstacle>() == null)
                ch.gameObject.SetActive(false);
        }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(glb, root.transform);
        inst.name = MarkerName;
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        var newLid = FindDeep(inst.transform, "Lid");
        if (newLid != null && root.TryGetComponent<Chest>(out var chest))
        {
            var so = new SerializedObject(chest);
            so.FindProperty("lid").objectReferenceValue = newLid;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return 1;
    }

    static int ApplyShrine(string prefabPath, string glbPath)
    {
        var glb = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
        if (glb == null) return 0;

        using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
        var root = scope.prefabContentsRoot;
        if (HasMarker(root)) return 0;

        var lbl = FindDeep(root.transform, "CostLabel");
        foreach (Transform ch in root.transform)
        {
            if (ch == lbl) continue;
            ch.gameObject.SetActive(false);
        }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(glb, root.transform);
        inst.name = MarkerName;
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        var newCrystal = FindDeep(inst.transform, "Crystal");
        if (newCrystal != null && root.TryGetComponent<Shrine>(out var shrine))
        {
            var so = new SerializedObject(shrine);
            so.FindProperty("crystal").objectReferenceValue = newCrystal;
            so.FindProperty("crystalRenderer").objectReferenceValue = newCrystal.GetComponentInChildren<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return 1;
    }

    static int ApplyCrumb()
    {
        const string prefabPath = "Assets/Prefabs/Items/Crumb.prefab";
        var glb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Items/Crumb.glb");
        if (glb == null || !TryGetFirstMesh(glb, out var mesh, out var mats)) return 0;

        using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
        var root = scope.prefabContentsRoot;
        var visual = FindDeep(root.transform, "Visual") ?? root.transform;
        var mf = visual.GetComponentInChildren<MeshFilter>(true);
        if (mf == null) return 0;
        mf.sharedMesh = mesh;
        if (mf.TryGetComponent<MeshRenderer>(out var mr)) mr.sharedMaterials = mats;
        visual.localScale = Vector3.one * 0.28f;
        return 1;
    }

    static int ApplyItemPickup()
    {
        const string prefabPath = "Assets/Prefabs/Items/ItemPickup.prefab";
        var glb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Items/ItemPickup.glb");
        if (glb == null || !TryGetFirstMesh(glb, out var mesh, out var mats)) return 0;

        using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
        var root = scope.prefabContentsRoot;
        var core = FindDeep(root.transform, "Core");
        var orb = FindDeep(root.transform, "Orb");
        if (core == null) return 0;

        if (core.TryGetComponent<MeshFilter>(out var mf)) mf.sharedMesh = mesh;
        if (core.TryGetComponent<MeshRenderer>(out var mr) && mats != null && mats.Length > 0)
            mr.sharedMaterials = mats;
        core.localScale = Vector3.one * 0.78f;
        if (orb != null) orb.gameObject.SetActive(false);
        return 1;
    }

    static int ApplyExitPipe()
    {
        const string prefabPath = "Assets/Prefabs/Level/ExitPipe.prefab";
        var glb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/ExitPipe.glb");
        if (glb == null) return 0;

        using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
        var root = scope.prefabContentsRoot;
        if (HasMarker(root)) return 0;

        var zone = FindDeep(root.transform, "Zone");
        foreach (Transform ch in root.transform)
        {
            if (ch == zone) continue;
            ch.gameObject.SetActive(false);
        }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(glb, root.transform);
        inst.name = MarkerName;
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
        return 1;
    }

    static int ApplyMainSceneEnvironment()
    {
        var rockGlb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/GardenRock.glb");
        var leafGlb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/GiantLeaf.glb");
        var potGlb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/TerracottaPot.glb");
        var canGlb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/SodaCan.glb");
        var grassGlb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/GrassTuft.glb");
        var pebbleGlb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/Pebble.glb");
        var brickGlb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/ToyBrickBase.glb");
        var fenceGlb = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/FencePlank.glb");

        if (!TryGetFirstMesh(rockGlb, out var rockMesh, out var rockMats)) return 0;
        if (!TryGetFirstMesh(leafGlb, out var leafMesh, out var leafMats)) return 0;
        if (!TryGetFirstMesh(potGlb, out var potMesh, out var potMats)) return 0;
        if (!TryGetFirstMesh(canGlb, out var canMesh, out var canMats)) return 0;
        if (!TryGetFirstMesh(grassGlb, out var grassMesh, out var _)) return 0;
        if (!TryGetFirstMesh(pebbleGlb, out var pebbleMesh, out var _)) return 0;
        if (!TryGetFirstMesh(brickGlb, out var brickMesh, out var _)) return 0;
        TryGetFirstMesh(fenceGlb, out var fenceMesh, out var fenceMats);

        var activeScene = SceneManager.GetActiveScene();
        string prevPath = activeScene.path;
        bool switched = prevPath != MainScenePath;

        Scene targetScene = activeScene;
        if (switched)
        {
            if (!activeScene.isDirty)
                targetScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            else
                return 0;
        }

        int changed = 0;
        foreach (var go in targetScene.GetRootGameObjects())
        {
            if (go.name == "_Systems" && go.TryGetComponent<BackyardAtmosphere>(out var atmo))
            {
                var so = new SerializedObject(atmo);
                so.FindProperty("treePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/TreeSilhouette.glb");
                so.FindProperty("bushPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/BushCluster.glb");
                so.FindProperty("cloverPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/CloverPatch.glb");
                so.FindProperty("daisyPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/DaisyFlower.glb");
                so.FindProperty("mushroomPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/MushroomCluster.glb");
                so.FindProperty("trowelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/TrowelProp.glb");
                so.FindProperty("bottleCapPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Environment/BottleCapProp.glb");
                so.FindProperty("grassTuftPrefab").objectReferenceValue = grassGlb;
                so.FindProperty("pebblePrefab").objectReferenceValue = pebbleGlb;
                so.ApplyModifiedPropertiesWithoutUndo();
                changed++;
            }

            if (go.name != "Backyard") continue;
            foreach (var tr in go.GetComponentsInChildren<Transform>(true))
            {
                string n = tr.name;
                var mf = tr.GetComponent<MeshFilter>();
                if (mf == null) continue;
                var mr = tr.GetComponent<MeshRenderer>();

                // Rochas, rampas/pontes de folha, latas deitadas, pedrinhas e grama da decoração usam
                // os props de Tools/Blender/build_props.py, ligados pelo PropsBinder: não trocar aqui.
                if (n == "FlowerPotFlipped")
                {
                    mf.sharedMesh = potMesh;
                    if (mr != null) mr.sharedMaterials = potMats;
                    changed++;
                }
                else if (n == "ToyBrick")
                {
                    mf.sharedMesh = brickMesh;
                    changed++;
                }
                else if (n == "Plank" && fenceMesh != null && tr.parent != null && tr.parent.name == "Fence")
                {
                    mf.sharedMesh = fenceMesh;
                    if (mr != null && fenceMats != null && fenceMats.Length > 0) mr.sharedMaterials = fenceMats;
                    changed++;
                }
                else if (tr.parent != null && tr.parent.name == "CanStanding")
                {
                    if (tr.GetSiblingIndex() == 0)
                    {
                        mf.sharedMesh = canMesh;
                        if (mr != null) mr.sharedMaterials = canMats;
                        changed++;
                    }
                    else if (tr.GetComponent<Collider>() == null)
                    {
                        tr.gameObject.SetActive(false);
                    }
                }
            }
        }

        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(targetScene);
            EditorSceneManager.SaveScene(targetScene);
        }

        if (switched && !string.IsNullOrEmpty(prevPath))
            EditorSceneManager.OpenScene(prevPath, OpenSceneMode.Single);

        return changed > 0 ? 1 : 0;
    }

    static bool HasMarker(GameObject root) => FindDeep(root.transform, MarkerName) != null;

    static Transform FindDeep(Transform root, string exactName)
    {
        if (root == null) return null;
        if (root.name == exactName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDeep(root.GetChild(i), exactName);
            if (found != null) return found;
        }
        return null;
    }

    static void DisablePrimitiveVisualChildren(Transform container, params Transform[] keep)
    {
        var keepSet = new HashSet<Transform>();
        if (keep != null) foreach (var k in keep) if (k != null) keepSet.Add(k);

        for (int i = 0; i < container.childCount; i++)
        {
            var ch = container.GetChild(i);
            if (keepSet.Contains(ch) || ch.name == MarkerName) continue;
            if (ch.GetComponent<Collider>() != null || ch.GetComponent<ParticleSystem>() != null || ch.GetComponent<TextMesh>() != null)
                continue;
            if (ch.GetComponentInChildren<Renderer>(true) != null)
                ch.gameObject.SetActive(false);
        }
    }

    static Vector3 CompensateParentScale(Transform root, Transform targetParent)
    {
        Vector3 total = Vector3.one;
        for (var t = targetParent; t != null && t != root; t = t.parent)
        {
            var s = t.localScale;
            total.x *= Mathf.Abs(s.x) > 1e-4f ? s.x : 1f;
            total.y *= Mathf.Abs(s.y) > 1e-4f ? s.y : 1f;
            total.z *= Mathf.Abs(s.z) > 1e-4f ? s.z : 1f;
        }
        return new Vector3(1f / total.x, 1f / total.y, 1f / total.z);
    }

    static bool TryGetFirstMesh(GameObject go, out Mesh mesh, out Material[] mats)
    {
        mesh = null;
        mats = null;
        if (go == null) return false;
        var mf = go.GetComponentInChildren<MeshFilter>(true);
        var mr = go.GetComponentInChildren<MeshRenderer>(true);
        if (mf != null && mf.sharedMesh != null)
        {
            mesh = mf.sharedMesh;
            mats = mr != null ? mr.sharedMaterials : Array.Empty<Material>();
            return true;
        }
        return false;
    }
}

sealed class ArtGlbPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string p in importedAssets)
        {
            if (p.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
            {
                EditorApplication.delayCall += () => ArtMeshBinder.TryAutoApplyPublic();
                return;
            }
        }
    }
}
