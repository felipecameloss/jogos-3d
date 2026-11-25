using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Liga os modelos FBX dos insetos (gerados em Tools/Blender, ver CLAUDE.md) nos prefabs
/// dos inimigos: troca o "Visual" antigo (primitivas) pelo FBX como filho "Model",
/// na escala do visual antigo (o colisor/NavMeshAgent continuam valendo) com os pés no
/// chão, religa as referências (bocal do besouro, garras do louva-a-deus) e adiciona o
/// ProceduralInsectAnimator (pernas, antenas e abdômen animados por código).
/// Pode rodar de novo depois de regerar um FBX: substitui o "Model" existente.
/// Menu: Jogo/Art/Aplicar modelos FBX dos insetos.
/// </summary>
public static class InsectModelBinder
{
    const string ModelName = "Model";
    const string Models = "Assets/Art/Models/Characters/";
    const string Prefabs = "Assets/Prefabs/Enemies/";

    /// <summary>(prefab, fbx). Termite e AllyAnt continuam com os .glb do ArtMeshBinder.</summary>
    public static readonly (string prefab, string fbx)[] Targets =
    {
        ("WorkerAnt", "WorkerAnt"),
        ("SoldierAnt", "SoldierAnt"),
        ("Aphid", "Aphid"),
        ("BombardierBeetle", "BombardierBeetle"),
        ("MantisBoss", "MantisBoss"),
        ("Spider", "Spider"),
        ("SpiderQueen", "SpiderQueen"),
        ("Grasshopper", "Grasshopper"),
        ("Slug", "Slug"),
        ("Firefly", "Firefly"),
    };

    const string GlowMaterials = "Assets/Art/Materials/Insects/";

    [MenuItem("Jogo/Art/Aplicar modelos FBX dos insetos")]
    public static void Run()
    {
        var log = new StringBuilder();
        foreach (var (prefab, fbx) in Targets) log.AppendLine(Apply(Prefabs + prefab + ".prefab", Models + fbx + ".fbx"));
        AssetDatabase.SaveAssets();
        Debug.Log("[InsectModelBinder]\n" + log);
    }

    /// <summary>
    /// Troca o modelo do prefab pelo FBX. <paramref name="fixedScale"/>: escala do modelo pedida
    /// pelo builder de um inimigo novo (senão mantém a do Model anterior ou mede pelo Visual antigo).
    /// </summary>
    public static string Apply(string prefabPath, string fbxPath, float? fixedScale = null)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbx == null) return $"{prefabPath}: FBX não encontrado ({fbxPath})";

        using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
        var root = scope.prefabContentsRoot.transform;

        // Visual antigo (primitivas) ou um Model de uma aplicação anterior
        var old = FindChild(root, "Visual") ?? FindChild(root, ModelName);
        if (old == null) return $"{prefabPath}: sem Visual/Model para substituir";
        var parent = old.parent;
        Bounds oldBounds = LocalBounds(root, old);
        // Referência de tamanho: o Model anterior tem a mesma escala que já foi calculada
        bool replacingModel = old.name == ModelName;
        float previousScale = old.localScale.x;
        Object.DestroyImmediate(old.gameObject);

        var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, parent);
        model.name = ModelName;
        var mt = model.transform;
        mt.localPosition = Vector3.zero;
        mt.localRotation = Quaternion.identity;
        mt.localScale = Vector3.one;
        Bounds raw = LocalBounds(root, mt);
        float scale = fixedScale ?? (replacingModel ? previousScale : oldBounds.size.z / raw.size.z);
        mt.localScale = Vector3.one * scale;
        Bounds scaled = LocalBounds(root, mt);
        // pés no chão (o NavMeshAgent põe a raiz no chão)
        mt.localPosition = new Vector3(0f, -scaled.min.y, 0f);

        var notes = new List<string>();
        Rewire(root, mt, notes);

        var anim = root.GetComponent<ProceduralInsectAnimator>() ?? root.gameObject.AddComponent<ProceduralInsectAnimator>();
        float speed = root.TryGetComponent(out NavMeshAgent agent) ? agent.speed : 3f;
        anim.EditorSetup(mt, Mathf.Max(0.2f, scaled.size.z * 0.55f), Mathf.Max(0.5f, speed));

        return $"{prefabPath}: escala {scale:0.###}, tamanho {scaled.size}, " +
               $"antes {oldBounds.size} {string.Join(" ", notes)}";
    }

    /// <summary>Religa campos que apontavam para peças do Visual antigo.</summary>
    static void Rewire(Transform root, Transform model, List<string> notes)
    {
        if (root.TryGetComponent(out BombardierBeetle beetle))
        {
            var nozzle = FindDeep(model, "Nozzle");
            var so = new SerializedObject(beetle);
            so.FindProperty("nozzle").objectReferenceValue = nozzle;
            so.FindProperty("nozzleRenderer").objectReferenceValue = nozzle != null ? GlowRenderer(nozzle, "BeetleNozzleGlow") : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            notes.Add($"nozzle={(nozzle != null)}");
        }
        if (root.TryGetComponent(out MantisBoss boss))
        {
            var left = FindDeep(model, "Claw_L");
            var right = FindDeep(model, "Claw_R");
            var so = new SerializedObject(boss);
            so.FindProperty("clawLeft").objectReferenceValue = left;
            so.FindProperty("clawRight").objectReferenceValue = right;
            var rends = new List<Renderer>();
            if (left != null) rends.AddRange(left.GetComponentsInChildren<Renderer>());
            if (right != null) rends.AddRange(right.GetComponentsInChildren<Renderer>());
            var arr = so.FindProperty("clawRenderers");
            arr.arraySize = rends.Count;
            for (int i = 0; i < rends.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = rends[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            notes.Add($"garras={rends.Count}");
        }
        if (root.TryGetComponent(out Spider spider))
        {
            var spinner = FindDeep(model, "Spinner");
            var so = new SerializedObject(spider);
            so.FindProperty("spinner").objectReferenceValue = spinner;
            so.FindProperty("spinnerRenderer").objectReferenceValue = spinner != null ? GlowRenderer(spinner, "SpiderSpinnerGlow") : null;
            so.FindProperty("body").objectReferenceValue = model.parent; // Body externo (agacha no aviso)
            so.ApplyModifiedPropertiesWithoutUndo();
            notes.Add($"fiandeira={(spinner != null)}");
        }
        if (root.TryGetComponent(out Grasshopper grasshopper))
        {
            var so = new SerializedObject(grasshopper);
            so.FindProperty("body").objectReferenceValue = model.parent; // Body externo (agacha nos avisos)
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (root.TryGetComponent(out Slug slug))
        {
            var so = new SerializedObject(slug);
            so.FindProperty("body").objectReferenceValue = model.parent; // Body externo (estica ao rastejar)
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (root.TryGetComponent(out Firefly firefly))
        {
            var lantern = FindDeep(model, "Lantern");
            var so = new SerializedObject(firefly);
            so.FindProperty("body").objectReferenceValue = model.parent;
            so.FindProperty("lantern").objectReferenceValue = lantern;
            so.FindProperty("lanternRenderer").objectReferenceValue = lantern != null ? GlowRenderer(lantern, "FireflyLanternGlow") : null;
            var light = root.Find("GlowLight");
            so.FindProperty("glowLight").objectReferenceValue = light != null ? light.GetComponent<Light>() : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            notes.Add($"lanterna={(lantern != null)} luz={(light != null)}");
        }
        if (root.TryGetComponent(out SpiderQueen queen))
        {
            var spinner = FindDeep(model, "Spinner");
            var so = new SerializedObject(queen);
            so.FindProperty("spinner").objectReferenceValue = spinner;
            so.FindProperty("spinnerRenderer").objectReferenceValue = spinner != null ? GlowRenderer(spinner, "SpiderQueenSpinnerGlow") : null;
            so.FindProperty("body").objectReferenceValue = model.parent; // Body externo (agacha/ergue nos avisos)
            so.ApplyModifiedPropertiesWithoutUndo();
            notes.Add($"fiandeira da rainha={(spinner != null)}");
        }
    }

    /// <summary>
    /// Os materiais embutidos no FBX não têm emissão ligada, e o aviso (bocal do besouro,
    /// fiandeira da aranha) brilha via _EmissionColor. Troca por uma cópia com emissão.
    /// </summary>
    static Renderer GlowRenderer(Transform part, string materialName)
    {
        var r = part.GetComponent<Renderer>();
        if (r == null) return null;
        string path = GlowMaterials + materialName + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            if (!AssetDatabase.IsValidFolder(GlowMaterials.TrimEnd('/')))
                AssetDatabase.CreateFolder("Assets/Art/Materials", "Insects");
            mat = new Material(r.sharedMaterial) { name = materialName };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(mat, path);
        }
        var mats = r.sharedMaterials;
        mats[0] = mat;
        r.sharedMaterials = mats;
        return r;
    }

    /// <summary>Bounds das malhas de `t` no espaço da raiz do prefab.</summary>
    static Bounds LocalBounds(Transform root, Transform t)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            var m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    static Transform FindChild(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name && t != root && (t.parent == root || t.parent.parent == root)) return t;
        return null;
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }
}
