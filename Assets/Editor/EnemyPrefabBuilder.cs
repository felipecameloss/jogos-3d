using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Ajudantes dos builders de inimigos novos (gafanhoto, lesma, vaga-lume, centopeia):
/// copiar um prefab base, trocar o componente de inimigo, acertar vida/agente/colisor,
/// ligar o modelo FBX (InsectModelBinder) e cadastrar no Diário de descobertas.
/// Os prefabs ficam prontos em Assets/Prefabs/Enemies; entrar no spawner de cada fase é à parte.
/// </summary>
public static class EnemyPrefabBuilder
{
    public const string Enemies = "Assets/Prefabs/Enemies/";
    public const string Models = "Assets/Art/Models/Characters/";
    public const string Icons = "Assets/Art/Icons/Enemies/";
    const string CatalogPath = "Assets/ScriptableObjects/DiscoveryCatalog.asset";

    /// <summary>Caminho do prefab de um inimigo pelo nome.</summary>
    public static string Prefab(string name) => Enemies + name + ".prefab";

    /// <summary>Copia o prefab base se o novo ainda não existe; se existe, atualiza no lugar (mantém o GUID).</summary>
    public static void EnsureCopy(string basePrefab, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        if (!AssetDatabase.CopyAsset(basePrefab, path)) throw new System.Exception($"Não deu para copiar {basePrefab} para {path}");
    }

    /// <summary>Tira o inimigo que veio do prefab base e garante um componente T.</summary>
    public static T SwapEnemy<T>(GameObject root) where T : EnemyBase
    {
        foreach (var e in root.GetComponents<EnemyBase>())
            if (e is not T) Object.DestroyImmediate(e);
        var enemy = root.GetComponent<T>();
        return enemy != null ? enemy : root.AddComponent<T>();
    }

    /// <summary>Campos da base (EnemyBase): velocidade, giro, migalhas e tempo até sumir.</summary>
    public static void SetBase(EnemyBase enemy, float moveSpeed, float turnSpeed, int crumbs, float despawnDelay = 1.2f)
    {
        var so = new SerializedObject(enemy);
        so.FindProperty("moveSpeed").floatValue = moveSpeed;
        so.FindProperty("turnSpeed").floatValue = turnSpeed;
        so.FindProperty("crumbReward").intValue = crumbs;
        so.FindProperty("despawnDelay").floatValue = despawnDelay;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetHealth(GameObject root, float max, float armor = 0f)
    {
        var so = new SerializedObject(root.GetComponent<Health>());
        so.FindProperty("maxHealth").floatValue = max;
        so.FindProperty("armor").floatValue = armor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetAgent(GameObject root, float speed, float radius, float height, float baseOffset = 0f, float angularSpeed = 720f, float acceleration = 20f)
    {
        var agent = root.GetComponent<NavMeshAgent>();
        agent.speed = speed;
        agent.radius = radius;
        agent.height = height;
        agent.baseOffset = baseOffset;
        agent.angularSpeed = angularSpeed;
        agent.acceleration = acceleration;
    }

    /// <summary>Colisor cápsula da raiz (direction: 0 = X, 1 = Y, 2 = Z).</summary>
    public static void SetCapsule(GameObject root, float radius, float height, Vector3 center, int direction = 1)
    {
        var c = root.GetComponent<CapsuleCollider>();
        if (c == null) c = root.AddComponent<CapsuleCollider>();
        c.radius = radius;
        c.height = height;
        c.center = center;
        c.direction = direction;
    }

    public static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    /// <summary>Cria ou atualiza a entrada do inimigo no Diário de descobertas (id = nome da classe).</summary>
    public static void AddToCatalog(string id, string displayName, string description, bool isBoss = false)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<DiscoveryCatalog>(CatalogPath);
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(Icons + id + ".png");
        if (icon == null) Debug.LogWarning($"[EnemyPrefabBuilder] retrato não encontrado em {Icons}{id}.png (rode Tools/Blender/build_enemy_icons.py -- {id})");
        var so = new SerializedObject(catalog);
        var enemies = so.FindProperty("enemies");
        SerializedProperty entry = null;
        for (int i = 0; i < enemies.arraySize; i++)
            if (enemies.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == id)
                entry = enemies.GetArrayElementAtIndex(i);
        if (entry == null)
        {
            enemies.arraySize++;
            entry = enemies.GetArrayElementAtIndex(enemies.arraySize - 1);
        }
        entry.FindPropertyRelative("id").stringValue = id;
        entry.FindPropertyRelative("displayName").stringValue = displayName;
        entry.FindPropertyRelative("description").stringValue = description;
        entry.FindPropertyRelative("icon").objectReferenceValue = icon;
        entry.FindPropertyRelative("isBoss").boolValue = isBoss;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }
}
