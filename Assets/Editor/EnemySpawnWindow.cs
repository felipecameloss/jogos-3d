using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Janela de debug (menu Jogo/Debug/Spawnar inimigos): em Play Mode, cria qualquer inimigo de
/// Assets/Prefabs/Enemies perto do player, para ver inimigos que ainda não estão no spawner de
/// nenhuma fase. Também força noite/dia no ciclo (o vaga-lume só aparece à noite e vai embora
/// quando amanhece).
/// </summary>
public class EnemySpawnWindow : EditorWindow
{
    const string Folder = "Assets/Prefabs/Enemies";

    GameObject[] prefabs = new GameObject[0];
    float distance = 12f;
    Vector2 scroll;

    [MenuItem("Jogo/Debug/Spawnar inimigos")]
    static void Open() => GetWindow<EnemySpawnWindow>("Spawnar inimigos");

    void OnEnable() => Refresh();

    void Refresh()
    {
        prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { Folder })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => p != null && p.GetComponent<EnemyBase>() != null)
            .OrderBy(p => p.name)
            .ToArray();
    }

    void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Entre no Play Mode (cena Main) para spawnar.", MessageType.Info);
            return;
        }
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            EditorGUILayout.HelpBox("Sem player na cena.", MessageType.Warning);
            return;
        }

        distance = EditorGUILayout.Slider("Distância do player", distance, 3f, 30f);
        var cycle = DayNightCycle.Instance;
        using (new EditorGUI.DisabledScope(cycle == null))
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(cycle == null ? "Sem ciclo de dia/noite" : cycle.IsNight ? "Agora: noite" : "Agora: dia");
            if (GUILayout.Button("Forçar noite")) SetPhase(cycle, 0.75f);
            if (GUILayout.Button("Forçar dia")) SetPhase(cycle, 0.25f);
        }

        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var prefab in prefabs)
        {
            var enemy = prefab.GetComponent<EnemyBase>();
            string label = enemy.CanSpawnNow ? prefab.name : $"{prefab.name} (o spawner não sortearia agora)";
            if (GUILayout.Button(label)) Spawn(prefab, player.transform);
        }
        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("Recarregar lista")) Refresh();
    }

    void Spawn(GameObject prefab, Transform player)
    {
        Vector3 fwd = player.forward; fwd.y = 0f;
        Vector3 at = player.position + fwd.normalized * distance;
        if (NavMesh.SamplePosition(at, out var hit, 6f, NavMesh.AllAreas)) at = hit.position;
        Vector3 look = player.position - at; look.y = 0f;
        var go = Instantiate(prefab, at, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
        go.name = prefab.name + " (debug)";
        Selection.activeGameObject = go;
    }

    /// <summary>Muda a hora do ciclo (0.25 = meio-dia, 0.75 = meia-noite).</summary>
    static void SetPhase(DayNightCycle cycle, float phase)
    {
        var so = new SerializedObject(cycle);
        float now = cycle.Phase;
        float start = so.FindProperty("startPhase").floatValue;
        so.FindProperty("startPhase").floatValue = Mathf.Repeat(start + phase - now, 1f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
