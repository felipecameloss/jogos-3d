using UnityEditor;
using UnityEngine;

/// <summary>
/// Monta o inimigo centopeia (issue #28) em dois prefabs com o mesmo componente Centipede e o
/// modelo Centipede.fbx (Tools/Blender/build_centipede.py: um segmento com cabeça e rabo que o
/// jogo liga/desliga):
/// - CentipedeSegment.prefab: segmento de corpo (startsAsHead desligado, sem agente em jogo);
/// - Centipede.prefab: a cabeça que o spawner usa; monta 9 segmentos atrás de si ao nascer.
/// E a entrada no Diário. Não entra no spawner aqui: cada fase escolhe os seus inimigos.
/// Pode rodar de novo (atualiza os prefabs no lugar). Menu: Jogo/Art/Criar inimigos/Centopeia.
/// Linha de comando: Unity -batchmode -quit -projectPath . -executeMethod CentipedeBuilder.Run
/// </summary>
public static class CentipedeBuilder
{
    const string BasePrefab = "Assets/Prefabs/Enemies/WorkerAnt.prefab";
    public static readonly string HeadPath = EnemyPrefabBuilder.Prefab("Centipede");
    public static readonly string SegmentPath = EnemyPrefabBuilder.Prefab("CentipedeSegment");
    const string Fbx = EnemyPrefabBuilder.Models + "Centipede.fbx";
    /// <summary>Escala do modelo (o FBX tem ~0,6 m de cabeça + segmento).</summary>
    public const float ModelScale = 1.8f;
    public const int BodySegments = 9;
    /// <summary>Distância entre segmentos no jogo (m): quase o comprimento de uma placa.</summary>
    public const float Spacing = 0.58f;

    [MenuItem("Jogo/Art/Criar inimigos/Centopeia")]
    public static void Run()
    {
        Build(SegmentPath, "CentipedeSegment", asHead: false, health: 30f, crumbs: 1, null);
        var segment = AssetDatabase.LoadAssetAtPath<Centipede>(SegmentPath);
        Build(HeadPath, "Centipede", asHead: true, health: 45f, crumbs: 3, segment);
        EnemyPrefabBuilder.AddToCatalog(nameof(Centipede), "Centopeia",
            "Uma fila de segmentos seguindo a cabeça, cada um com vida própria. Encostar no corpo machuca e " +
            "a cabeça morde. Matar um do meio parte a centopeia em duas.");
        AssetDatabase.SaveAssets();
        Debug.Log("[CentipedeBuilder] pronto");
    }

    static void Build(string path, string name, bool asHead, float health, int crumbs, Centipede segment)
    {
        EnemyPrefabBuilder.EnsureCopy(BasePrefab, path);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
        {
            var root = scope.prefabContentsRoot;
            root.name = name;
            var c = EnemyPrefabBuilder.SwapEnemy<Centipede>(root);
            EnemyPrefabBuilder.SetBase(c, 6f, 300f, crumbs, 0.8f);
            EnemyPrefabBuilder.SetHealth(root, health);
            EnemyPrefabBuilder.SetAgent(root, 6f, 0.45f, 0.8f, 0f, 300f, 16f);
            EnemyPrefabBuilder.SetCapsule(root, 0.34f, asHead ? 1.25f : 0.9f, new Vector3(0f, 0.34f, asHead ? 0.15f : 0f), 2);
            // cabeça/rabo do modelo: o InsectModelBinder religa logo abaixo
            c.EditorSetup(asHead, asHead ? BodySegments : 0, segment, root.transform.Find("Body"), null, null);
            var so = new SerializedObject(c);
            so.FindProperty("spacing").floatValue = Spacing;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        Debug.Log($"[CentipedeBuilder] {InsectModelBinder.Apply(path, Fbx, ModelScale)}");
    }
}
