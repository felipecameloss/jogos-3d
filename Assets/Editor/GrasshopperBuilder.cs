using UnityEditor;
using UnityEngine;

/// <summary>
/// Monta o inimigo gafanhoto (issue #27): Assets/Prefabs/Enemies/Grasshopper.prefab copiado
/// da aranha (aproveita o círculo vermelho do aviso de pulo), com o componente Grasshopper,
/// o modelo Grasshopper.fbx (Tools/Blender/build_grasshopper.py) e a entrada no Diário.
/// Não entra no spawner aqui: cada fase escolhe os seus inimigos.
/// Pode rodar de novo (atualiza o prefab no lugar). Menu: Jogo/Art/Criar inimigos/Gafanhoto.
/// Linha de comando: Unity -batchmode -quit -projectPath . -executeMethod GrasshopperBuilder.Run
/// </summary>
public static class GrasshopperBuilder
{
    const string BasePrefab = "Assets/Prefabs/Enemies/Spider.prefab";
    public static readonly string PrefabPath = EnemyPrefabBuilder.Prefab("Grasshopper");
    const string Fbx = EnemyPrefabBuilder.Models + "Grasshopper.fbx";
    /// <summary>Escala do modelo (o FBX tem ~1 m de corpo).</summary>
    public const float ModelScale = 2f;

    [MenuItem("Jogo/Art/Criar inimigos/Gafanhoto")]
    public static void Run()
    {
        EnemyPrefabBuilder.EnsureCopy(BasePrefab, PrefabPath);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
        {
            var root = scope.prefabContentsRoot;
            root.name = "Grasshopper";
            var telegraphT = EnemyPrefabBuilder.FindDeep(root.transform, "LeapTelegraph");
            var telegraph = telegraphT != null ? telegraphT.GetComponent<Renderer>() : null;
            if (telegraph != null) telegraph.enabled = false;
            var g = EnemyPrefabBuilder.SwapEnemy<Grasshopper>(root);
            EnemyPrefabBuilder.SetBase(g, 6.5f, 540f, 6);
            EnemyPrefabBuilder.SetHealth(root, 70f);
            EnemyPrefabBuilder.SetAgent(root, 6.5f, 0.55f, 1.2f, 0f, 540f, 24f);
            EnemyPrefabBuilder.SetCapsule(root, 0.5f, 2.2f, new Vector3(0f, 0.65f, -0.2f), 2);
            g.EditorSetup(root.transform.Find("Body"), telegraph);
        }
        Debug.Log("[GrasshopperBuilder] " + InsectModelBinder.Apply(PrefabPath, Fbx, ModelScale));
        EnemyPrefabBuilder.AddToCatalog(nameof(Grasshopper), "Gafanhoto",
            "Fica longe e salta em cima de você: o círculo vermelho marca onde cai e trava no fim (dá para sair). " +
            "Pousa tonto. De perto, vira de costas e dá um coice.");
        AssetDatabase.SaveAssets();
        Debug.Log("[GrasshopperBuilder] pronto");
    }
}
