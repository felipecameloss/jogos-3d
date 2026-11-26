using UnityEditor;
using UnityEngine;

/// <summary>
/// Monta o inimigo lesma (issue #26): Assets/Prefabs/Enemies/Slug.prefab copiado da formiga
/// operária, com o componente Slug, o modelo Slug.fbx (Tools/Blender/build_slug.py), a gosma
/// com o material transparente da poça de ácido e a entrada no Diário.
/// Não entra no spawner aqui: cada fase escolhe os seus inimigos.
/// Pode rodar de novo (atualiza o prefab no lugar). Menu: Jogo/Art/Criar inimigos/Lesma.
/// Linha de comando: Unity -batchmode -quit -projectPath . -executeMethod SlugBuilder.Run
/// </summary>
public static class SlugBuilder
{
    const string BasePrefab = "Assets/Prefabs/Enemies/WorkerAnt.prefab";
    public static readonly string PrefabPath = EnemyPrefabBuilder.Prefab("Slug");
    const string Fbx = EnemyPrefabBuilder.Models + "Slug.fbx";
    const string SlimeMaterial = "Assets/Art/Materials/AcidPool.mat";
    /// <summary>Escala do modelo (o FBX tem ~1,1 m de corpo).</summary>
    public const float ModelScale = 2f;

    [MenuItem("Jogo/Art/Criar inimigos/Lesma")]
    public static void Run()
    {
        EnemyPrefabBuilder.EnsureCopy(BasePrefab, PrefabPath);
        var slime = AssetDatabase.LoadAssetAtPath<Material>(SlimeMaterial);
        if (slime == null) Debug.LogWarning($"[SlugBuilder] material da gosma não encontrado ({SlimeMaterial})");
        using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
        {
            var root = scope.prefabContentsRoot;
            root.name = "Slug";
            var slug = EnemyPrefabBuilder.SwapEnemy<Slug>(root);
            EnemyPrefabBuilder.SetBase(slug, 2.4f, 240f, 7, 1.4f);
            EnemyPrefabBuilder.SetHealth(root, 120f, 10f);
            EnemyPrefabBuilder.SetAgent(root, 2.4f, 0.6f, 1f, 0f, 240f, 10f);
            EnemyPrefabBuilder.SetCapsule(root, 0.45f, 2.1f, new Vector3(0f, 0.4f, -0.1f), 2);
            slug.EditorSetup(root.transform.Find("Body"), slime);
        }
        Debug.Log("[SlugBuilder] " + InsectModelBinder.Apply(PrefabPath, Fbx, ModelScale));
        EnemyPrefabBuilder.AddToCatalog(nameof(Slug), "Lesma",
            "Lenta e parruda. Deixa um rastro de gosma que faz você escorregar. De perto, ergue a frente e dá um bote.");
        AssetDatabase.SaveAssets();
        Debug.Log("[SlugBuilder] pronto");
    }
}
