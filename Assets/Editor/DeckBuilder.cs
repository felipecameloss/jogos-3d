using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fase 4: embaixo do deck (issue #30). Cenário próprio (com NavMesh assado): chão de terra,
/// mourões até o teto de tábuas (a 11 m, com frestas por onde entram raios de luz), tábuas
/// velhas deitadas de lado formando corredores com passagens e teias no chão dos corredores
/// (<see cref="SlowZone"/> do tipo teia: o player fica lento). Escuro (luz fixa entrando pelas frestas,
/// conta como noite para os vaga-lumes), sem pisão,
/// chuva, lupa nem bolhas; aranhas, centopeias, cupins e vaga-lumes; a aranha-rainha de chefe.
/// Pode rodar de novo. Menu: Jogo/Fases/Criar fase 4: embaixo do deck.
/// Linha de comando: -executeMethod DeckBuilder.Run
/// </summary>
public static class DeckBuilder
{
    const string Props = "Assets/Art/Models/Props/";
    const string Env = "Assets/Art/Models/Environment/";
    const float Half = 46f;
    const float CeilingY = 11f;
    public const float WebSlow = 0.45f;

    [MenuItem("Jogo/Fases/Criar fase 4: embaixo do deck")]
    public static void Run()
    {
        var dirt = StageBuilderUtils.Flat("DeckDirt", new Color(0.3f, 0.22f, 0.14f), 0.05f);
        var wood = StageBuilderUtils.Flat("DeckWood", new Color(0.55f, 0.4f, 0.25f), 0.1f);
        var woodDark = StageBuilderUtils.Flat("DeckWoodDark", new Color(0.27f, 0.19f, 0.12f), 0.1f);
        var woodOld = StageBuilderUtils.Flat("DeckWoodOld", new Color(0.47f, 0.43f, 0.37f), 0.1f);
        var concrete = StageBuilderUtils.Flat("DeckConcrete", new Color(0.45f, 0.44f, 0.42f), 0.1f);
        var web = StageBuilderUtils.Transparent("DeckWeb", new Color(0.95f, 0.95f, 1f, 0.6f));
        var webMesh = StageBuilderUtils.SaveMesh(WebMesh(), "DeckWebMesh");

        var prefab = StageBuilderUtils.BuildLevel("Deck", bakeNavMesh: true, root =>
        {
            var rng = new System.Random(30);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            StageBuilderUtils.Box(root, "Chao", new Vector3(0f, -0.5f, 0f), new Vector3(Half * 2f + 2f, 1f, Half * 2f + 2f), dirt);
            StageBuilderUtils.InvisibleBounds(root, Half, CeilingY);

            // ---- teto: tábuas do deck com frestas; vigas embaixo
            var ceiling = StageBuilderUtils.Group(root, "Teto");
            var gaps = new List<float>();
            for (float x = -Half - 2f; x < Half + 2f; x += 2.75f)
            {
                StageBuilderUtils.Box(ceiling, "Tabua", new Vector3(x + 1.2f, CeilingY + 0.25f, 0f), new Vector3(2.4f, 0.5f, Half * 2f + 8f),
                    rng.NextDouble() < 0.3 ? woodOld : wood, collider: false);
                gaps.Add(x + 2.575f);
            }
            for (float z = -Half; z <= Half; z += 8f)
                StageBuilderUtils.Box(ceiling, "Viga", new Vector3(0f, CeilingY - 0.8f, z), new Vector3(Half * 2f + 8f, 1.6f, 0.8f), woodDark, collider: false);

            // ---- a luz do dia entra pelas frestas do teto (o "sol" da fase fica alto e as tábuas
            // fazem as faixas de sombra); luzes fracas e quentes rebatendo pelo chão
            var light = StageBuilderUtils.Group(root, "LuzRebatida");
            for (float x = -30f; x <= 30f; x += 20f)
                for (float z = -30f; z <= 30f; z += 30f)
                    StageBuilderUtils.PointLight(light, "Luz", new Vector3(x + R(-4f, 4f), 2.2f, z + R(-4f, 4f)), new Color(1f, 0.88f, 0.68f), 18f, 2.2f);

            // ---- mourões (com base de concreto)
            var posts = StageBuilderUtils.Group(root, "Mouroes");
            for (float x = -32f; x <= 32f; x += 16f)
                for (float z = -32f; z <= 32f; z += 16f)
                {
                    var c = new Vector3(x + R(-1f, 1f), 0f, z + R(-1f, 1f));
                    // base baixa (dá para subir andando: sem ilha de NavMesh em cima)
                    StageBuilderUtils.Block(posts, "Base", c, new Vector3(3f, 0.35f, 3f), concrete);
                    StageBuilderUtils.Block(posts, "Mourao", c + Vector3.up * 0.35f, new Vector3(1.8f, CeilingY - 0.35f, 1.8f), woodDark);
                }

            // ---- corredores: tábuas velhas de lado, com passagens
            var walls = StageBuilderUtils.Group(root, "Corredores");
            var webSpots = new List<Vector3>();
            float[] rows = { -30f, -18f, -6f, 6f, 18f, 30f };
            foreach (float z in rows)
            {
                // duas passagens por parede em lugares diferentes a cada linha
                float g1 = R(-34f, -6f), g2 = R(6f, 34f);
                float x = -Half;
                foreach (float g in new[] { g1, g2, Half + 4f })
                {
                    float end = Mathf.Min(g - 3.5f, Half);
                    if (end - x > 1f)
                        StageBuilderUtils.Block(walls, "Tabua", new Vector3((x + end) * 0.5f, 0f, z), new Vector3(end - x, R(3f, 3.8f), 0.7f),
                            rng.NextDouble() < 0.5 ? woodOld : wood, yaw: R(-1.5f, 1.5f));
                    x = g + 3.5f;
                }
                // teia no meio de cada passagem e no meio do corredor
                webSpots.Add(new Vector3(g1, 0f, z));
                webSpots.Add(new Vector3(R(-30f, 30f), 0f, z + 6f));
            }
            // algumas tábuas cruzadas para quebrar a reta dos corredores
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3(R(-36f, 36f), 0f, rows[rng.Next(rows.Length - 1)] + 6f);
                StageBuilderUtils.Block(walls, "TabuaCruzada", c, new Vector3(0.7f, R(2.6f, 3.4f), R(5f, 7f)), woodOld, yaw: R(-10f, 10f));
            }

            // ---- teias no chão (lentidão)
            var webs = StageBuilderUtils.Group(root, "Teias");
            int n = 0;
            foreach (var p in webSpots)
            {
                n++;
                float r = R(2.2f, 3.2f);
                var go = new GameObject($"Teia{n}");
                go.transform.SetParent(webs, false);
                go.transform.localPosition = p + Vector3.up * 0.06f;
                go.transform.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                go.transform.localScale = Vector3.one * r;
                go.AddComponent<MeshFilter>().sharedMesh = webMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = web;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.AddComponent<SlowZone>().Configure(SlowZone.Kind.Web, 1f, 0.8f, WebSlow);
            }

            // ---- tralha: folhas secas, pedrinhas e cogumelos perto dos mourões
            var clutter = StageBuilderUtils.Group(root, "Tralha");
            for (int i = 0; i < 18; i++)
                StageBuilderUtils.Model(Props + "FallenLeaf.fbx", clutter, "Folha", new Vector3(R(-42f, 42f), 0.02f, R(-42f, 42f)), R(1.2f, 1.8f), R(0f, 360f));
            for (int i = 0; i < 14; i++)
                StageBuilderUtils.Model(Props + "RockPebble.fbx", clutter, "Pedrinha", new Vector3(R(-42f, 42f), 0f, R(-42f, 42f)), R(1.2f, 2f), R(0f, 360f));
            for (float x = -32f; x <= 32f; x += 32f)
                for (float z = -32f; z <= 32f; z += 32f)
                    StageBuilderUtils.Model(Env + "MushroomCluster.glb", clutter, "Cogumelos", new Vector3(x + 2.2f, 0f, z + 2.2f), R(2f, 2.8f), R(0f, 360f));

            StageBuilderUtils.Marker(root, "PlayerSpawn", new Vector3(0f, 0.1f, -40f));
        });

        var def = StageBuilderUtils.Stage("Deck", so =>
        {
            so.FindProperty("displayName").stringValue = "Embaixo do deck";
            so.FindProperty("description").stringValue = "Escuro e empoeirado: tábuas formam corredores, a luz entra pelas frestas e as teias deixam lento.";
            so.FindProperty("useBackyard").boolValue = false;
            so.FindProperty("levelPrefab").objectReferenceValue = prefab;
            so.FindProperty("backyardAtmosphere").boolValue = false;
            so.FindProperty("lighting").enumValueIndex = (int)StageDefinition.LightingMode.Indoor;
            so.FindProperty("dark").boolValue = true;
            so.FindProperty("sunTint").colorValue = new Color(1f, 0.98f, 0.92f);
            so.FindProperty("lightScale").floatValue = 1.4f;
            so.FindProperty("fogDensity").floatValue = 0.02f;
            so.FindProperty("fogColor").colorValue = new Color(0.08f, 0.065f, 0.05f);
            so.FindProperty("indoorSky").colorValue = new Color(0.46f, 0.41f, 0.35f);
            so.FindProperty("indoorEquator").colorValue = new Color(0.37f, 0.31f, 0.25f);
            so.FindProperty("indoorGround").colorValue = new Color(0.2f, 0.16f, 0.12f);
            so.FindProperty("humanStomps").boolValue = false;
            so.FindProperty("rain").boolValue = false;
            so.FindProperty("magnifier").boolValue = false;
            so.FindProperty("soapBubbles").boolValue = false;
            StageBuilderUtils.SetEnemies(so,
                new StageBuilderUtils.Spawn("Spider", 16f, 0.8f, prewarm: 6),
                new StageBuilderUtils.Spawn("Termite", 5f, 0.8f, minGroup: 3, extraGroup: 3, prewarm: 12),
                new StageBuilderUtils.Spawn("Firefly", 12f, 0.6f, minGroup: 2, extraGroup: 2, prewarm: 6),
                new StageBuilderUtils.Spawn("Centipede", 55f, 0.35f, 1.2f, prewarm: 2),
                new StageBuilderUtils.Spawn("WorkerAnt", 8f, 0.4f, prewarm: 6));
            so.FindProperty("boss").objectReferenceValue = StageBuilderUtils.Enemy("SpiderQueen");
            so.FindProperty("playerLantern").boolValue = false;
            so.FindProperty("scatteredCrumbs").intValue = 0;
        });
        StageBuilderUtils.PutInSequence(def, 3);
        Debug.Log("[DeckBuilder] pronto");
    }

    /// <summary>Teia deitada (raio 1): 10 raios do centro e uma espiral, em fitas finas dos dois lados.</summary>
    static Mesh WebMesh()
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        void Strip(Vector3 a, Vector3 b, float w)
        {
            Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized) * w * 0.5f;
            int i = verts.Count;
            verts.Add(a - side); verts.Add(a + side); verts.Add(b + side); verts.Add(b - side);
            tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 }); // os dois lados
        }
        const int spokes = 10;
        for (int s = 0; s < spokes; s++)
        {
            float a = s * Mathf.PI * 2f / spokes;
            Strip(Vector3.zero, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), 0.035f);
        }
        Vector3 prev = Vector3.zero;
        for (int k = 1; k <= 60; k++)
        {
            float a = k * Mathf.PI * 2f / spokes;
            float r = 0.12f + 0.85f * k / 60f;
            var p = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            if (k > 1) Strip(prev, p, 0.025f);
            prev = p;
        }
        var mesh = new Mesh { name = "DeckWebMesh" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
