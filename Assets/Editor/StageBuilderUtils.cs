using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Ferramentas dos builders de fase (horta, poça, deck, formigueiro, cozinha): materiais de cor
/// chapada, peças de caixa/cilindro, NavMesh assado dentro do prefab da fase, asset da fase
/// (StageDefinition) e a sequência. Cenários são montados numa cena temporária e salvos como
/// prefab em Assets/Prefabs/Stages; a fase liga esse prefab pelo StageDirector da cena Main.
/// </summary>
public static class StageBuilderUtils
{
    public const string PrefabFolder = "Assets/Prefabs/Stages";
    public const string MaterialFolder = "Assets/Art/Materials/Stages";
    public const string StageFolder = "Assets/ScriptableObjects/Stages";
    public const string SequencePath = StageFolder + "/StageSequence.asset";
    public const string MainScene = "Assets/Scenes/Main.unity";
    const string TransparentBase = "Assets/Art/Materials/AcidPool.mat";

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    // ---------- Materiais ----------

    /// <summary>Material URP Lit de cor chapada (cria ou atualiza; o GUID fica).</summary>
    public static Material Flat(string name, Color color, float smoothness = 0.15f, Color? emission = null)
    {
        EnsureFolder(MaterialFolder);
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor(BaseColorId, color);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Material transparente sem luz (água, teia, raio de luz), cópia do da poça de ácido.</summary>
    public static Material Transparent(string name, Color color)
    {
        EnsureFolder(MaterialFolder);
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            AssetDatabase.CopyAsset(TransparentBase, path);
            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        }
        mat.SetColor(BaseColorId, color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------- Peças ----------

    public static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale,
        Material mat, bool collider = true, Quaternion? rotation = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = rotation ?? Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        else if (type == PrimitiveType.Cylinder)
        {
            // cilindro com colisor de cápsula arredonda o topo: troca por malha (topo reto para pisar)
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.AddComponent<MeshCollider>();
        }
        return go;
    }

    /// <summary>Caixa pelo centro e tamanho (com colisor).</summary>
    public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat,
        bool collider = true, float yaw = 0f) =>
        Prim(PrimitiveType.Cube, parent, name, center, size, mat, collider, Quaternion.Euler(0f, yaw, 0f));

    /// <summary>
    /// Caixa apoiada (base em baseCenter). Entra 10 cm no que está embaixo: encostada só na
    /// superfície, o NavMesh trata o chão por dentro da caixa como livre (bolsão fechado).
    /// </summary>
    public static GameObject Block(Transform parent, string name, Vector3 baseCenter, Vector3 size, Material mat,
        bool collider = true, float yaw = 0f) =>
        Box(parent, name, baseCenter + Vector3.up * (size.y * 0.5f - 0.05f), size + Vector3.up * 0.1f, mat, collider, yaw);

    /// <summary>Cilindro em pé com base em baseCenter (diâmetro, altura).</summary>
    public static GameObject Pillar(Transform parent, string name, Vector3 baseCenter, float diameter, float height,
        Material mat, bool collider = true) =>
        Prim(PrimitiveType.Cylinder, parent, name, baseCenter + Vector3.up * height * 0.5f,
            new Vector3(diameter, height * 0.5f, diameter), mat, collider);

    /// <summary>Instancia um modelo (FBX/GLB) como filho, sem colisores do modelo.</summary>
    public static GameObject Model(string assetPath, Transform parent, string name, Vector3 position, float scale,
        float yaw = 0f)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null) throw new Exception($"Modelo não encontrado: {assetPath}");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        foreach (var c in go.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
        return go;
    }

    public static Transform Group(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    /// <summary>Marcador vazio (PlayerSpawn...).</summary>
    public static Transform Marker(Transform parent, string name, Vector3 position, float yaw = 0f)
    {
        var t = Group(parent, name);
        t.localPosition = position;
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    /// <summary>Luz pontual sem sombra.</summary>
    public static Light PointLight(Transform parent, string name, Vector3 position, Color color, float range, float intensity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        return l;
    }

    /// <summary>Paredes invisíveis em volta (x/z de -half a +half), altura h.</summary>
    public static void InvisibleBounds(Transform parent, float half, float height = 20f)
    {
        var g = Group(parent, "InvisibleWalls");
        foreach (var (pos, size) in new[]
                 {
                     (new Vector3(0, height / 2, half + 1), new Vector3(half * 2 + 4, height, 2)),
                     (new Vector3(0, height / 2, -half - 1), new Vector3(half * 2 + 4, height, 2)),
                     (new Vector3(half + 1, height / 2, 0), new Vector3(2, height, half * 2 + 4)),
                     (new Vector3(-half - 1, height / 2, 0), new Vector3(2, height, half * 2 + 4)),
                 })
        {
            var w = new GameObject("InvisibleWall");
            w.transform.SetParent(g, false);
            w.transform.localPosition = pos;
            w.AddComponent<BoxCollider>().size = size;
        }
    }

    // ---------- Montagem e NavMesh ----------

    /// <summary>
    /// Monta o cenário numa cena vazia (build preenche a raiz), assa o NavMesh se pedir, salva o
    /// prefab (o GUID do prefab e do NavMeshData continuam os mesmos ao refazer) e devolve o prefab.
    /// </summary>
    public static GameObject BuildLevel(string name, bool bakeNavMesh, Action<Transform> build)
    {
        EnsureFolder(PrefabFolder);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject(name);
        build(root.transform);

        if (bakeNavMesh)
        {
            var surface = root.AddComponent<NavMeshSurface>();
            surface.agentTypeID = 0;
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
            surface.minRegionArea = 2f;
            surface.BuildNavMesh();
            string dataPath = $"{PrefabFolder}/{name}_NavMesh.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(dataPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(surface.navMeshData, existing);
                surface.navMeshData = existing;
                EditorUtility.SetDirty(existing);
            }
            else AssetDatabase.CreateAsset(surface.navMeshData, dataPath);
            ReportConnectivity(root.transform);
        }

        string prefabPath = $"{PrefabFolder}/{name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[StageBuilder] {prefabPath}: {CountObjects(root)} objetos{(bakeNavMesh ? ", NavMesh assado" : "")}");
        return prefab;
    }

    static int CountObjects(GameObject root) => root.GetComponentsInChildren<Transform>(true).Length;

    /// <summary>
    /// Confere se todo o NavMesh assado se liga ao PlayerSpawn (grade de 4 m): bolsões fechados
    /// viram lugar onde baú/cano/inimigo nasce sem caminho. Lista no log os pontos sem caminho.
    /// </summary>
    static void ReportConnectivity(Transform root)
    {
        Transform spawn = null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == "PlayerSpawn") spawn = t;
        if (spawn == null || !NavMesh.SamplePosition(spawn.position, out var start, 3f, NavMesh.AllAreas)) return;
        var path = new NavMeshPath();
        var lost = new List<Vector3>();
        int total = 0;
        for (float x = -46f; x <= 46f; x += 4f)
            for (float z = -46f; z <= 46f; z += 4f)
            {
                if (!NavMesh.SamplePosition(new Vector3(x, 0.5f, z), out var hit, 1.5f, NavMesh.AllAreas)) continue;
                total++;
                if (!NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    lost.Add(hit.position);
            }
        string list = string.Join(" ", lost.ConvertAll(p => $"({p.x:0},{p.y:0.0},{p.z:0})"));
        Debug.Log($"[StageBuilder] {root.name}: {total - lost.Count}/{total} pontos ligados ao começo" + (lost.Count > 0 ? $"; sem caminho: {list}" : ""));
    }

    /// <summary>Salva uma malha gerada em código como asset (o GUID fica ao refazer).</summary>
    public static Mesh SaveMesh(Mesh mesh, string name)
    {
        EnsureFolder(PrefabFolder);
        string path = $"{PrefabFolder}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    // ---------- Fase e sequência ----------

    /// <summary>Cria ou atualiza o asset da fase; edit mexe nos campos (SerializedObject).</summary>
    public static StageDefinition Stage(string fileName, Action<SerializedObject> edit)
    {
        EnsureFolder(StageFolder);
        string path = $"{StageFolder}/{fileName}.asset";
        var def = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<StageDefinition>();
            AssetDatabase.CreateAsset(def, path);
        }
        var so = new SerializedObject(def);
        edit(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(def);
        return def;
    }

    /// <summary>Uma linha da tabela de inimigos da fase.</summary>
    public struct Spawn
    {
        public string prefab;
        public float cost, weight, minDifficulty;
        public int minGroup, extraGroup, prewarm;

        public Spawn(string prefab, float cost, float weight, float minDifficulty = 1f, int minGroup = 1, int extraGroup = 0, int prewarm = 4)
        {
            this.prefab = prefab; this.cost = cost; this.weight = weight; this.minDifficulty = minDifficulty;
            this.minGroup = minGroup; this.extraGroup = extraGroup; this.prewarm = prewarm;
        }
    }

    public static void SetEnemies(SerializedObject so, params Spawn[] table)
    {
        var list = so.FindProperty("enemies");
        list.arraySize = table.Length;
        for (int i = 0; i < table.Length; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            var prefab = AssetDatabase.LoadAssetAtPath<EnemyBase>($"Assets/Prefabs/Enemies/{table[i].prefab}.prefab");
            if (prefab == null) throw new Exception($"Inimigo não encontrado: {table[i].prefab}");
            e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            e.FindPropertyRelative("cost").floatValue = table[i].cost;
            e.FindPropertyRelative("weight").floatValue = table[i].weight;
            e.FindPropertyRelative("minDifficulty").floatValue = table[i].minDifficulty;
            e.FindPropertyRelative("minGroup").intValue = table[i].minGroup;
            e.FindPropertyRelative("extraGroupSize").intValue = table[i].extraGroup;
            e.FindPropertyRelative("poolPrewarm").intValue = table[i].prewarm;
        }
    }

    public static EnemyBase Enemy(string name) => AssetDatabase.LoadAssetAtPath<EnemyBase>($"Assets/Prefabs/Enemies/{name}.prefab");

    /// <summary>Põe a fase na posição `index` da sequência (0 = fase 1), criando a sequência se faltar.</summary>
    public static void PutInSequence(StageDefinition def, int index)
    {
        var seq = AssetDatabase.LoadAssetAtPath<StageSequence>(SequencePath);
        if (seq == null)
        {
            EnsureFolder(StageFolder);
            seq = ScriptableObject.CreateInstance<StageSequence>();
            AssetDatabase.CreateAsset(seq, SequencePath);
        }
        var so = new SerializedObject(seq);
        var list = so.FindProperty("stages");
        if (list.arraySize <= index) list.arraySize = index + 1;
        list.GetArrayElementAtIndex(index).objectReferenceValue = def;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(seq);
        AssetDatabase.SaveAssets();
    }

    /// <summary>Lugares livres do quintal (para enfeitar a horta/poça sem cair em cima de pedra/lata).</summary>
    public static List<Bounds> BackyardObstacles()
    {
        var scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
        var list = new List<Bounds>();
        var backyard = GameObject.Find("Backyard");
        if (backyard != null)
            foreach (var c in backyard.GetComponentsInChildren<Collider>())
            {
                if (c.name == "Ground" || c.name.StartsWith("Invisible") || c.bounds.size.x > 60f || c.bounds.size.z > 60f) continue;
                list.Add(c.bounds);
            }
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) list.Add(new Bounds(player.transform.position, new Vector3(12f, 6f, 12f))); // começo da fase livre
        return list;
    }

    public static bool Overlaps(List<Bounds> obstacles, Vector3 center, float radius)
    {
        foreach (var b in obstacles)
        {
            Vector3 p = b.ClosestPoint(new Vector3(center.x, b.center.y, center.z));
            p.y = center.y;
            if ((p - center).sqrMagnitude < radius * radius) return true;
        }
        return false;
    }
}
