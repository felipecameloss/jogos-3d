using UnityEditor;
using UnityEngine;

/// <summary>
/// Monta o inimigo vaga-lume (issue #25): Assets/Prefabs/Enemies/Firefly.prefab copiado da
/// formiga operária, voando (NavMeshAgent com baseOffset), com o componente Firefly, o modelo
/// Firefly.fbx (Tools/Blender/build_firefly.py), uma luz pontual na lanterna e a entrada no
/// Diário. Só nasce à noite (Firefly.CanSpawnNow, respeitado pelo EnemySpawner).
/// Não entra no spawner aqui: cada fase escolhe os seus inimigos.
/// Pode rodar de novo (atualiza o prefab no lugar). Menu: Jogo/Art/Criar inimigos/Vaga-lume.
/// Linha de comando: Unity -batchmode -quit -projectPath . -executeMethod FireflyBuilder.Run
/// </summary>
public static class FireflyBuilder
{
    const string BasePrefab = "Assets/Prefabs/Enemies/WorkerAnt.prefab";
    public static readonly string PrefabPath = EnemyPrefabBuilder.Prefab("Firefly");
    const string Fbx = EnemyPrefabBuilder.Models + "Firefly.fbx";
    /// <summary>Escala do modelo (o FBX tem ~0,6 m de corpo).</summary>
    public const float ModelScale = 1.8f;
    /// <summary>Altura do voo acima do chão (NavMeshAgent.baseOffset).</summary>
    public const float HoverHeight = 2.4f;

    [MenuItem("Jogo/Art/Criar inimigos/Vaga-lume")]
    public static void Run()
    {
        EnemyPrefabBuilder.EnsureCopy(BasePrefab, PrefabPath);
        using (var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath))
        {
            var root = scope.prefabContentsRoot;
            root.name = "Firefly";
            var firefly = EnemyPrefabBuilder.SwapEnemy<Firefly>(root);
            EnemyPrefabBuilder.SetBase(firefly, 5.5f, 360f, 4);
            EnemyPrefabBuilder.SetHealth(root, 28f);
            EnemyPrefabBuilder.SetAgent(root, 5.5f, 0.5f, 1f, HoverHeight, 360f, 14f);
            EnemyPrefabBuilder.SetCapsule(root, 0.5f, 1.1f, new Vector3(0f, 0.55f, 0f), 1);

            // luz da lanterna (o Firefly acende/pisca e leva junto com a lanterna)
            var lightT = root.transform.Find("GlowLight");
            if (lightT == null)
            {
                lightT = new GameObject("GlowLight").transform;
                lightT.SetParent(root.transform, false);
            }
            lightT.localPosition = new Vector3(0f, 0.45f, -0.7f);
            var light = lightT.GetComponent<Light>();
            if (light == null) light = lightT.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 1.2f;
            light.color = new Color(0.85f, 1f, 0.3f);
            light.shadows = LightShadows.None;
        }
        // lanterna, emissão, luz e Body: o InsectModelBinder religa (também quando o FBX é regerado)
        Debug.Log("[FireflyBuilder] " + InsectModelBinder.Apply(PrefabPath, Fbx, ModelScale));
        EnemyPrefabBuilder.AddToCatalog(nameof(Firefly), "Vaga-lume",
            "Só aparece à noite. Voa em volta e carrega a luz piscando cada vez mais rápido: " +
            "olhando para ele na hora do clarão, a tela fica branca. Vire a câmera!");
        AssetDatabase.SaveAssets();
        Debug.Log("[FireflyBuilder] pronto");
    }
}
