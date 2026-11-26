using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Fase 2: horta (issue #29). Reaproveita o terreno do gramado (o quintal continua ligado, com o
/// NavMesh dele) e põe por cima, longe das pedras/latas do quintal: canteiros de terra com
/// fileiras de alface e cenoura, pés de tomate com estaca, tomates e abóboras gigantes caídos
/// (obstáculos que furam o NavMesh) e o regador (WateringCanEvent). Luz mais quente, tabela de
/// inimigos de horta (pulgão, lesma, gafanhoto...) e o louva-a-deus de chefe.
/// Modelos de Tools/Blender/build_garden_props.py. Pode rodar de novo.
/// Menu: Jogo/Fases/Criar fase 2: horta. Linha de comando: -executeMethod HortaBuilder.Run
/// </summary>
public static class HortaBuilder
{
    const string Props = "Assets/Art/Models/Props/";
    const float S = 1.8f; // escala dos props (insetos ~1,8x no jogo)

    [MenuItem("Jogo/Fases/Criar fase 2: horta")]
    public static void Run()
    {
        var obstacles = StageBuilderUtils.BackyardObstacles();
        var soil = StageBuilderUtils.Flat("HortaSoil", new Color(0.3f, 0.18f, 0.09f), 0.05f);
        var furrow = StageBuilderUtils.Flat("HortaFurrow", new Color(0.2f, 0.11f, 0.05f), 0.05f);
        var stake = StageBuilderUtils.Flat("HortaStake", new Color(0.62f, 0.45f, 0.26f), 0.1f);
        var vine = StageBuilderUtils.Flat("HortaVine", new Color(0.18f, 0.5f, 0.1f), 0.2f);
        var water = StageBuilderUtils.Transparent("HortaWater", new Color(0.45f, 0.75f, 1f, 0.5f));

        var prefab = StageBuilderUtils.BuildLevel("Horta", bakeNavMesh: false, root =>
        {
            var rng = new System.Random(29);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var used = new List<(Vector3 c, float r)>();
            bool Free(Vector3 c, float r)
            {
                if (Mathf.Abs(c.x) > 42f - r || Mathf.Abs(c.z) > 42f - r) return false;
                if (StageBuilderUtils.Overlaps(obstacles, c, r)) return false;
                foreach (var (uc, ur) in used) if ((uc - c).sqrMagnitude < (ur + r) * (ur + r)) return false;
                return true;
            }

            // ---- canteiros: faixas de terra com sulcos e fileiras de verdura
            var beds = StageBuilderUtils.Group(root, "Canteiros");
            int bedCount = 0;
            for (int attempt = 0; attempt < 400 && bedCount < 6; attempt++)
            {
                var c = new Vector3(R(-36f, 36f), 0f, R(-36f, 36f));
                float yaw = rng.NextDouble() < 0.5 ? 0f : 90f;
                Vector3 along = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                bool ok = true;
                for (float t = -11f; t <= 11f && ok; t += 3.5f) ok = Free(c + along * t, 3.6f);
                if (!ok) continue;
                for (float t = -11f; t <= 11f; t += 3.5f) used.Add((c + along * t, 3.2f));
                bedCount++;
                var bed = StageBuilderUtils.Group(beds, $"Canteiro{bedCount}");
                bed.localPosition = c;
                bed.localRotation = Quaternion.Euler(0f, yaw, 0f);
                StageBuilderUtils.Block(bed, "Terra", Vector3.zero, new Vector3(5.5f, 0.22f, 25f), soil, collider: false);
                for (int f = -1; f <= 1; f += 2)
                    StageBuilderUtils.Block(bed, "Sulco", new Vector3(f * 1.4f, 0.2f, 0f), new Vector3(0.35f, 0.06f, 24f), furrow, collider: false);
                bool lettuce = bedCount % 2 == 1;
                for (float z = -10.5f; z <= 10.5f; z += 3f)
                    for (int f = -1; f <= 1; f += 2)
                        StageBuilderUtils.Model(Props + (lettuce ? "Lettuce.fbx" : "Carrot.fbx"), bed, lettuce ? "Alface" : "Cenoura",
                            new Vector3(f * 1.4f + R(-0.2f, 0.2f), 0.22f, z + R(-0.4f, 0.4f)), S * R(0.75f, 1f), R(0f, 360f));
            }

            // ---- pés de tomate: estaca, rama e tomatinhos pendurados
            var plants = StageBuilderUtils.Group(root, "PesDeTomate");
            int plantCount = 0;
            for (int attempt = 0; attempt < 300 && plantCount < 7; attempt++)
            {
                var c = new Vector3(R(-38f, 38f), 0f, R(-38f, 38f));
                if (!Free(c, 2.6f)) continue;
                used.Add((c, 2.6f));
                plantCount++;
                var p = StageBuilderUtils.Group(plants, $"PeDeTomate{plantCount}");
                p.localPosition = c;
                StageBuilderUtils.Pillar(p, "Estaca", Vector3.zero, 0.45f, 10f, stake);
                for (int i = 0; i < 5; i++)
                {
                    float h = 1.5f + i * 1.7f;
                    float a = i * 1.9f;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    StageBuilderUtils.Prim(PrimitiveType.Cylinder, p, "Rama", new Vector3(0f, h, 0f) + dir * 0.9f,
                        new Vector3(0.25f, 1.1f, 0.25f), vine, collider: false, rotation: Quaternion.FromToRotation(Vector3.up, (dir + Vector3.up * 0.6f).normalized));
                    StageBuilderUtils.Prim(PrimitiveType.Sphere, p, "Folha", new Vector3(0f, h + 0.6f, 0f) + dir * 1.8f,
                        new Vector3(1.6f, 0.25f, 1f), vine, collider: false, rotation: Quaternion.LookRotation(dir));
                    if (i % 2 == 0)
                        StageBuilderUtils.Model(Props + "Tomato.fbx", p, "Tomatinho", new Vector3(0f, h - 0.6f, 0f) + dir * 1.6f, S * 0.32f, R(0f, 360f));
                }
            }

            // ---- tomates e abóboras gigantes caídos: obstáculos (furam o NavMesh)
            var big = StageBuilderUtils.Group(root, "Obstaculos");
            Obstacles(big, Props + "Pumpkin.fbx", "Abobora", 4, 5.2f, S, R, Free, used);
            Obstacles(big, Props + "Tomato.fbx", "Tomate", 6, 3.1f, S, R, Free, used);

            // ---- cerquinha de estacas em volta de um canto da horta
            var fence = StageBuilderUtils.Group(root, "Estacas");
            for (int i = 0; i < 18; i++)
            {
                var c = new Vector3(-40f + i * 2.2f, 0f, 41f + R(-0.2f, 0.2f));
                if (StageBuilderUtils.Overlaps(obstacles, c, 0.8f)) continue;
                StageBuilderUtils.Block(fence, "Estaca", c, new Vector3(0.5f, R(3.2f, 4f), 0.5f), stake, yaw: R(-6f, 6f));
            }

            // ---- regador (o perigo da fase)
            var canGo = new GameObject("Regador");
            canGo.transform.SetParent(root, false);
            canGo.AddComponent<WateringCanEvent>().EditorSetup(AssetDatabase.LoadAssetAtPath<GameObject>(Props + "WateringCan.fbx"), water);
        });

        var def = StageBuilderUtils.Stage("Horta", so =>
        {
            so.FindProperty("displayName").stringValue = "Horta";
            so.FindProperty("description").stringValue = "A horta do quintal: canteiros, tomates e abóboras gigantes e um regador que despeja água do céu.";
            so.FindProperty("useBackyard").boolValue = true;
            so.FindProperty("levelPrefab").objectReferenceValue = prefab;
            so.FindProperty("backyardAtmosphere").boolValue = true;
            so.FindProperty("lighting").enumValueIndex = (int)StageDefinition.LightingMode.DayNight;
            so.FindProperty("sunTint").colorValue = new Color(1.05f, 0.98f, 0.86f);
            so.FindProperty("lightScale").floatValue = 1f;
            so.FindProperty("humanStomps").boolValue = true;
            so.FindProperty("rain").boolValue = true;
            so.FindProperty("magnifier").boolValue = true;
            so.FindProperty("soapBubbles").boolValue = true;
            StageBuilderUtils.SetEnemies(so,
                new StageBuilderUtils.Spawn("WorkerAnt", 8f, 0.7f, prewarm: 10),
                new StageBuilderUtils.Spawn("Aphid", 10f, 0.8f, minGroup: 2, extraGroup: 1, prewarm: 8),
                new StageBuilderUtils.Spawn("Slug", 18f, 0.55f, prewarm: 4),
                new StageBuilderUtils.Spawn("Grasshopper", 16f, 0.55f, 1.15f, prewarm: 4),
                new StageBuilderUtils.Spawn("Termite", 5f, 0.3f, minGroup: 3, extraGroup: 2, prewarm: 8),
                new StageBuilderUtils.Spawn("SoldierAnt", 24f, 0.25f, 1.5f, prewarm: 3));
            so.FindProperty("boss").objectReferenceValue = StageBuilderUtils.Enemy("MantisBoss");
            so.FindProperty("playerLantern").boolValue = false;
            so.FindProperty("scatteredCrumbs").intValue = 0;
        });
        StageBuilderUtils.PutInSequence(def, 1);
        Debug.Log("[HortaBuilder] pronto");
    }

    /// <summary>Props grandes com colisor esférico e NavMeshObstacle que fura o NavMesh do quintal.</summary>
    static void Obstacles(Transform parent, string model, string name, int count, float size, float scale,
        System.Func<float, float, float> R, System.Func<Vector3, float, bool> free, List<(Vector3, float)> used)
    {
        float radius = size * scale * 0.5f;
        int placed = 0;
        for (int attempt = 0; attempt < 400 && placed < count; attempt++)
        {
            var c = new Vector3(R(-40f, 40f), 0f, R(-40f, 40f));
            if (!free(c, radius + 1.5f)) continue;
            used.Add((c, radius + 1f));
            placed++;
            var go = StageBuilderUtils.Model(model, parent, $"{name}{placed}", c, scale * R(0.85f, 1.1f), R(0f, 360f));
            var bounds = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var col = go.AddComponent<SphereCollider>();
            col.center = go.transform.InverseTransformPoint(bounds.center);
            col.radius = Mathf.Max(bounds.extents.x, bounds.extents.z) / go.transform.lossyScale.x;
            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.center = col.center;
            obstacle.radius = col.radius;
            obstacle.height = bounds.size.y / go.transform.lossyScale.y;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }
    }
}
