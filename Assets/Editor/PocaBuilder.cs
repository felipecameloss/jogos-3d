using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fase 3: poça d'água (issue #31). Depois da chuva, o gramado do quintal (terreno e NavMesh
/// continuam) ganha uma poça grande no lugar mais aberto e duas menores. Água rasa e
/// transparente com <see cref="SlowZone"/>: quem cai nela fica lento. Folhas verdes boiando
/// (<see cref="FloatingLeaf"/>) servem de plataforma acima da água. Borda de barro molhado e
/// capim na beira. Luz mais fria, tabela de inimigos de lugar úmido e a aranha-rainha de chefe.
/// Pode rodar de novo. Menu: Jogo/Fases/Criar fase 3: poça d'água.
/// Linha de comando: -executeMethod PocaBuilder.Run
/// </summary>
public static class PocaBuilder
{
    const string Props = "Assets/Art/Models/Props/";
    const string LeafModel = "Assets/Art/Models/Environment/GiantLeaf.glb";
    /// <summary>Altura da superfície da água (rasa: pela canela do inseto).</summary>
    public const float WaterLevel = 0.45f;
    /// <summary>Tamanho do modelo no espaço da raiz (sem rotação), já com a escala dele.</summary>
    static Vector3 LocalSize(GameObject go)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            var mb = mf.sharedMesh.bounds;
            var m = go.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
            }
        }
        return Vector3.Scale(b.size, go.transform.localScale);
    }

    /// <summary>Lentidão de quem está na água.</summary>
    public const float WaterSlow = 0.5f;

    [MenuItem("Jogo/Fases/Criar fase 3: poça d'água")]
    public static void Run()
    {
        var obstacles = StageBuilderUtils.BackyardObstacles();
        var water = StageBuilderUtils.Transparent("PocaWater", new Color(0.32f, 0.58f, 0.82f, 0.55f));
        var mud = StageBuilderUtils.Flat("PocaMud", new Color(0.24f, 0.18f, 0.11f), 0.6f);

        var prefab = StageBuilderUtils.BuildLevel("Poca", bakeNavMesh: false, root =>
        {
            var rng = new System.Random(31);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // Lugares mais abertos do quintal: distância até a pedra/lata mais perto
            var spots = new List<(Vector3 c, float room)>();
            for (float x = -34f; x <= 34f; x += 2f)
                for (float z = -34f; z <= 34f; z += 2f)
                {
                    var c = new Vector3(x, 0f, z);
                    float room = 18f;
                    while (room > 3f && StageBuilderUtils.Overlaps(obstacles, c, room)) room -= 1f;
                    room = Mathf.Min(room, 44f - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)));
                    spots.Add((c, room));
                }
            spots.Sort((a, b) => b.room.CompareTo(a.room));

            var puddles = new List<(Vector3 c, float r)>();
            foreach (var (c, room) in spots)
            {
                if (puddles.Count == 3) break;
                float r = puddles.Count == 0 ? Mathf.Min(15f, room - 1.5f) : Mathf.Min(7.5f, room - 1.5f);
                if (r < 4f) continue;
                bool far = true;
                foreach (var (pc, pr) in puddles) if ((pc - c).magnitude < pr + r + 6f) far = false;
                if (far) puddles.Add((c, r));
            }

            var waterRoot = StageBuilderUtils.Group(root, "Agua");
            var leaves = StageBuilderUtils.Group(root, "FolhasBoiando");
            var shore = StageBuilderUtils.Group(root, "Beira");
            int leafCount = 0;
            for (int i = 0; i < puddles.Count; i++)
            {
                var (c, r) = puddles[i];
                var puddle = StageBuilderUtils.Group(waterRoot, $"Poca{i + 1}");
                puddle.localPosition = c;
                // contorno irregular: um disco grande + discos menores em volta
                var discs = new List<(Vector3 offset, float radius)> { (Vector3.zero, r) };
                for (int k = 0; k < 5; k++)
                {
                    float a = k * Mathf.PI * 2f / 5f + R(-0.3f, 0.3f);
                    discs.Add((new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r * R(0.55f, 0.75f), r * R(0.4f, 0.55f)));
                }
                foreach (var (offset, dr) in discs)
                {
                    StageBuilderUtils.Prim(PrimitiveType.Cylinder, puddle, "Barro", offset + Vector3.up * 0.02f,
                        new Vector3(dr * 2f + 2.2f, 0.02f, dr * 2f + 2.2f), mud, collider: false);
                    var surface = StageBuilderUtils.Prim(PrimitiveType.Cylinder, puddle, "Agua", offset + Vector3.up * WaterLevel,
                        new Vector3(dr * 2f, 0.01f, dr * 2f), water, collider: false);
                    surface.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var zoneGo = StageBuilderUtils.Group(puddle, "SlowZone");
                    zoneGo.localPosition = offset;
                    zoneGo.gameObject.AddComponent<SlowZone>().Configure(SlowZone.Kind.Water, dr, WaterLevel + 0.1f, WaterSlow);
                }

                // folhas boiando, sem encostar uma na outra
                int want = i == 0 ? 7 : 2;
                var placed = new List<Vector3>();
                for (int attempt = 0; attempt < 200 && placed.Count < want; attempt++)
                {
                    Vector2 d = Random2(rng) * (r - 3.2f);
                    var p = new Vector3(d.x, 0f, d.y);
                    bool ok = true;
                    foreach (var q in placed) if ((q - p).magnitude < 7f) ok = false;
                    if (!ok) continue;
                    placed.Add(p);
                    leafCount++;
                    Leaf(leaves, LeafModel, $"Folha{leafCount}", c + p + Vector3.up * (WaterLevel + 0.05f), R(0f, 360f), R(5.5f, 6.8f));
                }

                // capim na beira
                for (int k = 0; k < (i == 0 ? 10 : 5); k++)
                {
                    float a = R(0f, Mathf.PI * 2f);
                    var p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (r + R(0.8f, 2.2f));
                    if (StageBuilderUtils.Overlaps(obstacles, p, 1f)) continue;
                    StageBuilderUtils.Model(Props + "GrassStalk.fbx", shore, "Capim", p, R(1.2f, 1.7f), R(0f, 360f));
                }
            }
        });

        var def = StageBuilderUtils.Stage("Poca", so =>
        {
            so.FindProperty("displayName").stringValue = "Poça d'água";
            so.FindProperty("description").stringValue = "Depois da chuva: uma poça grande no meio do gramado. Na água fica lento; as folhas boiando são plataformas.";
            so.FindProperty("useBackyard").boolValue = true;
            so.FindProperty("levelPrefab").objectReferenceValue = prefab;
            so.FindProperty("backyardAtmosphere").boolValue = true;
            so.FindProperty("lighting").enumValueIndex = (int)StageDefinition.LightingMode.DayNight;
            so.FindProperty("sunTint").colorValue = new Color(0.88f, 0.95f, 1.05f);
            so.FindProperty("lightScale").floatValue = 0.92f;
            so.FindProperty("humanStomps").boolValue = true;
            so.FindProperty("rain").boolValue = true;
            so.FindProperty("magnifier").boolValue = true;
            so.FindProperty("soapBubbles").boolValue = true;
            StageBuilderUtils.SetEnemies(so,
                new StageBuilderUtils.Spawn("Slug", 18f, 0.8f, prewarm: 5),
                new StageBuilderUtils.Spawn("WorkerAnt", 8f, 0.65f, prewarm: 8),
                new StageBuilderUtils.Spawn("BombardierBeetle", 14f, 0.5f, 1.15f, prewarm: 4),
                new StageBuilderUtils.Spawn("Grasshopper", 16f, 0.45f, 1.2f, prewarm: 4),
                new StageBuilderUtils.Spawn("Firefly", 12f, 0.6f, minGroup: 2, extraGroup: 2, prewarm: 6),
                new StageBuilderUtils.Spawn("Aphid", 10f, 0.3f, prewarm: 3));
            so.FindProperty("boss").objectReferenceValue = StageBuilderUtils.Enemy("SpiderQueen");
            so.FindProperty("playerLantern").boolValue = false;
            so.FindProperty("scatteredCrumbs").intValue = 0;
        });
        StageBuilderUtils.PutInSequence(def, 2);
        Debug.Log("[PocaBuilder] pronto");
    }

    static Vector2 Random2(System.Random rng)
    {
        double a = rng.NextDouble() * System.Math.PI * 2, r = System.Math.Sqrt(rng.NextDouble());
        return new Vector2((float)(System.Math.Cos(a) * r), (float)(System.Math.Sin(a) * r));
    }

    /// <summary>Folha verde boiando (~`length` m): modelo + colisor de caixa fino no topo + FloatingLeaf.</summary>
    static void Leaf(Transform parent, string model, string name, Vector3 position, float yaw, float length)
    {
        var go = StageBuilderUtils.Model(model, parent, name, position, 1f, yaw);
        var t = go.transform;
        var raw = LocalSize(go);
        t.localScale = Vector3.one * (length / Mathf.Max(0.01f, Mathf.Max(raw.x, raw.z)));
        // limites no espaço da folha (a caixa do mundo cresce com a rotação)
        bool any = false;
        var local = new Bounds();
        float topWorld = float.MinValue;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            var mb = mf.sharedMesh.bounds;
            var m = t.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = m.MultiplyPoint3x4(corner);
                if (!any) { local = new Bounds(p, Vector3.zero); any = true; } else local.Encapsulate(p);
                topWorld = Mathf.Max(topWorld, mf.transform.TransformPoint(corner).y);
            }
        }
        // a folha encosta na água: topo um pouco acima da superfície
        t.position += Vector3.up * (WaterLevel + 0.3f - topWorld);
        float thin = 0.25f / t.lossyScale.y;
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(local.size.x * 0.85f, thin, local.size.z * 0.85f);
        col.center = new Vector3(local.center.x, local.max.y - thin * 0.5f, local.center.z);
        go.AddComponent<FloatingLeaf>();
    }
}
