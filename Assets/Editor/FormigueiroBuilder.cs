using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fase 5: formigueiro (issue #61). Cenário próprio (com NavMesh assado): câmaras numa grade
/// 5x5 ligadas por túneis estreitos (labirinto com alguns atalhos), paredes de terra numa malha
/// só (gerada aqui, com colisor). Bem escuro (sempre noite, neblina preta), sem céu; o player
/// ganha um vaga-lume amigo que voa junto como lanterna (PlayerLantern). Ovos de formiga,
/// cogumelos com luz fraca e sementes guardadas nas câmaras. Formigas de todo tipo, centopeias
/// e cupins; o louva-a-deus de chefe. Pode rodar de novo.
/// Menu: Jogo/Fases/Criar fase 5: formigueiro. Linha de comando: -executeMethod FormigueiroBuilder.Run
/// </summary>
public static class FormigueiroBuilder
{
    const string Env = "Assets/Art/Models/Environment/";
    const float Half = 46f;
    const float Cell = 2f;
    const int N = 46;               // células por lado (92 m)
    const float WallHeight = 5f;
    const float CorridorWidth = 5f;
    static readonly float[] RoomCenters = { -36f, -18f, 0f, 18f, 36f };

    [MenuItem("Jogo/Fases/Criar fase 5: formigueiro")]
    public static void Run()
    {
        var floor = StageBuilderUtils.Flat("FormigueiroFloor", new Color(0.3f, 0.2f, 0.12f), 0.05f);
        var wallTop = StageBuilderUtils.Flat("FormigueiroWallTop", new Color(0.16f, 0.1f, 0.06f), 0.02f);
        var wallSide = StageBuilderUtils.Flat("FormigueiroWallSide", new Color(0.46f, 0.32f, 0.2f), 0.05f);
        var egg = StageBuilderUtils.Flat("FormigueiroEgg", new Color(0.96f, 0.94f, 0.86f), 0.4f);
        var seed = StageBuilderUtils.Flat("FormigueiroSeed", new Color(0.78f, 0.64f, 0.38f), 0.3f);
        var root = StageBuilderUtils.Flat("FormigueiroRoot", new Color(0.28f, 0.18f, 0.1f), 0.1f);
        var glow = StageBuilderUtils.Flat("FormigueiroGlow", new Color(0.6f, 1f, 0.85f), 0.3f, new Color(0.25f, 0.9f, 0.7f) * 1.6f);

        var rng = new System.Random(61);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        bool[,] open = Layout(rng, out var rooms);
        var wallMesh = StageBuilderUtils.SaveMesh(WallMesh(open, rng), "FormigueiroWalls");

        var prefab = StageBuilderUtils.BuildLevel("Formigueiro", bakeNavMesh: true, level =>
        {
            StageBuilderUtils.InvisibleBounds(level, Half, 12f);

            // chão e paredes numa malha só (sem chão embaixo das paredes: nada de NavMesh dentro delas)
            var walls = new GameObject("Paredes");
            walls.transform.SetParent(level, false);
            walls.AddComponent<MeshFilter>().sharedMesh = wallMesh;
            walls.AddComponent<MeshRenderer>().sharedMaterials = new[] { wallTop, wallSide, floor };
            walls.AddComponent<MeshCollider>().sharedMesh = wallMesh;

            // ---- câmaras: ovos, cogumelos que brilham, sementes guardadas
            var decor = StageBuilderUtils.Group(level, "Camaras");
            int k = 0;
            foreach (var (c, radius) in rooms)
            {
                k++;
                if (k == 1) continue; // a câmara do começo fica limpa
                var room = StageBuilderUtils.Group(decor, $"Camara{k}");
                room.localPosition = c;
                int kind = rng.Next(5) % 3; // cogumelos que brilham aparecem mais
                if (kind == 0)
                {
                    for (int i = 0; i < 7; i++)
                    {
                        float a = R(0f, Mathf.PI * 2f), d = R(0f, radius * 0.45f);
                        StageBuilderUtils.Prim(PrimitiveType.Sphere, room, "Ovo", new Vector3(Mathf.Cos(a) * d, 0.45f, Mathf.Sin(a) * d),
                            new Vector3(0.75f, 0.55f, 1.1f), egg, collider: false, rotation: Quaternion.Euler(0f, R(0f, 360f), R(-15f, 15f)));
                    }
                }
                else if (kind == 1)
                {
                    var spot = new Vector3(R(-2f, 2f), 0f, R(-2f, 2f));
                    StageBuilderUtils.Model(Env + "MushroomCluster.glb", room, "Cogumelos", spot, R(2f, 2.6f), R(0f, 360f));
                    for (int i = 0; i < 4; i++)
                        StageBuilderUtils.Prim(PrimitiveType.Sphere, room, "Brilho", spot + new Vector3(R(-1.4f, 1.4f), R(0.6f, 1.6f), R(-1.4f, 1.4f)),
                            Vector3.one * R(0.25f, 0.45f), glow, collider: false);
                    StageBuilderUtils.PointLight(room, "LuzDosCogumelos", spot + Vector3.up * 1.5f, new Color(0.45f, 1f, 0.8f), 9f, 1.4f);
                }
                else if (kind == 2)
                {
                    for (int i = 0; i < 9; i++)
                        StageBuilderUtils.Prim(PrimitiveType.Sphere, room, "Semente", new Vector3(R(-2.5f, 2.5f), 0.3f, R(-2.5f, 2.5f)),
                            new Vector3(0.9f, 0.45f, 0.6f), seed, collider: false, rotation: Quaternion.Euler(0f, R(0f, 360f), 0f));
                }
            }

            // ---- raízes descendo pelas paredes dos túneis
            var roots = StageBuilderUtils.Group(level, "Raizes");
            int placedRoots = 0;
            for (int attempt = 0; attempt < 2000 && placedRoots < 26; attempt++)
            {
                int i = rng.Next(1, N - 1), j = rng.Next(1, N - 1);
                if (!open[i, j]) continue;
                Vector2Int wall = Vector2Int.zero;
                if (!open[i + 1, j]) wall = Vector2Int.right; else if (!open[i - 1, j]) wall = Vector2Int.left;
                else if (!open[i, j + 1]) wall = Vector2Int.up; else if (!open[i, j - 1]) wall = Vector2Int.down;
                if (wall == Vector2Int.zero) continue;
                placedRoots++;
                Vector3 c = CellCenter(i, j) + new Vector3(wall.x, 0f, wall.y) * (Cell * 0.45f);
                Vector3 top = c + Vector3.up * (WallHeight - 0.3f), bottom = c + Vector3.up * R(1.2f, 2.5f) - new Vector3(wall.x, 0f, wall.y) * 0.8f;
                StageBuilderUtils.Prim(PrimitiveType.Cylinder, roots, "Raiz", (top + bottom) * 0.5f,
                    new Vector3(0.22f, (top - bottom).magnitude * 0.5f, 0.22f), root, collider: false,
                    rotation: Quaternion.FromToRotation(Vector3.up, (top - bottom).normalized));
            }

            // ---- vermes de luz: pontinhos que brilham nas paredes dos túneis (sem luz de verdade)
            var worms = StageBuilderUtils.Group(level, "VermesDeLuz");
            int placedWorms = 0;
            for (int attempt = 0; attempt < 4000 && placedWorms < 140; attempt++)
            {
                int i = rng.Next(1, N - 1), j = rng.Next(1, N - 1);
                if (!open[i, j]) continue;
                Vector2Int wall = !open[i + 1, j] ? Vector2Int.right : !open[i - 1, j] ? Vector2Int.left
                    : !open[i, j + 1] ? Vector2Int.up : !open[i, j - 1] ? Vector2Int.down : Vector2Int.zero;
                if (wall == Vector2Int.zero) continue;
                placedWorms++;
                Vector3 p = CellCenter(i, j) + new Vector3(wall.x, 0f, wall.y) * (Cell * 0.5f - 0.05f) + Vector3.up * R(0.8f, 4.2f);
                StageBuilderUtils.Prim(PrimitiveType.Sphere, worms, "Verme", p, Vector3.one * R(0.12f, 0.22f), glow, collider: false);
            }

            var (start, _) = rooms[0];
            StageBuilderUtils.Marker(level, "PlayerSpawn", start + Vector3.up * 0.1f, 45f);
        });

        var def = StageBuilderUtils.Stage("Formigueiro", so =>
        {
            so.FindProperty("displayName").stringValue = "Formigueiro";
            so.FindProperty("description").stringValue = "Túneis estreitos e quase sem luz embaixo da terra. Um vaga-lume amigo voa com você iluminando o caminho.";
            so.FindProperty("useBackyard").boolValue = false;
            so.FindProperty("levelPrefab").objectReferenceValue = prefab;
            so.FindProperty("backyardAtmosphere").boolValue = false;
            so.FindProperty("lighting").enumValueIndex = (int)StageDefinition.LightingMode.AlwaysNight;
            so.FindProperty("dark").boolValue = true;
            so.FindProperty("sunTint").colorValue = new Color(0.8f, 0.75f, 0.7f);
            so.FindProperty("lightScale").floatValue = 0.6f;
            so.FindProperty("fogDensity").floatValue = 0.03f;
            so.FindProperty("fogColor").colorValue = new Color(0.04f, 0.028f, 0.02f);
            so.FindProperty("humanStomps").boolValue = false;
            so.FindProperty("rain").boolValue = false;
            so.FindProperty("magnifier").boolValue = false;
            so.FindProperty("soapBubbles").boolValue = false;
            StageBuilderUtils.SetEnemies(so,
                new StageBuilderUtils.Spawn("WorkerAnt", 8f, 1f, minGroup: 2, extraGroup: 2, prewarm: 14),
                new StageBuilderUtils.Spawn("SoldierAnt", 24f, 0.6f, prewarm: 6),
                new StageBuilderUtils.Spawn("Termite", 5f, 0.4f, minGroup: 3, extraGroup: 2, prewarm: 8),
                new StageBuilderUtils.Spawn("Centipede", 55f, 0.3f, 1.2f, prewarm: 2),
                new StageBuilderUtils.Spawn("Spider", 16f, 0.3f, 1.4f, prewarm: 3));
            so.FindProperty("boss").objectReferenceValue = StageBuilderUtils.Enemy("MantisBoss");
            so.FindProperty("playerLantern").boolValue = true;
            so.FindProperty("scatteredCrumbs").intValue = 0;
        });
        StageBuilderUtils.PutInSequence(def, 4);
        Debug.Log("[FormigueiroBuilder] pronto");
    }

    static Vector3 CellCenter(int i, int j) => new(-Half + (i + 0.5f) * Cell, 0f, -Half + (j + 0.5f) * Cell);

    /// <summary>Células abertas (câmaras + túneis). Labirinto entre as câmaras (DFS) com alguns atalhos.</summary>
    static bool[,] Layout(System.Random rng, out List<(Vector3 c, float r)> rooms)
    {
        int m = RoomCenters.Length;
        rooms = new List<(Vector3, float)>();
        var radius = new float[m, m];
        for (int a = 0; a < m; a++)
            for (int b = 0; b < m; b++)
                radius[a, b] = 5.5f + (float)rng.NextDouble() * 2f;
        // câmara do começo primeiro (canto), depois as outras
        for (int b = 0; b < m; b++)
            for (int a = 0; a < m; a++)
                rooms.Add((new Vector3(RoomCenters[a], 0f, RoomCenters[b]), radius[a, b]));

        var links = new List<(int, int, int, int)>();
        var seen = new bool[m, m];
        var stack = new Stack<(int, int)>();
        stack.Push((0, 0));
        seen[0, 0] = true;
        var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (stack.Count > 0)
        {
            var (a, b) = stack.Peek();
            var options = new List<(int, int)>();
            foreach (var (da, db) in dirs)
            {
                int na = a + da, nb = b + db;
                if (na >= 0 && nb >= 0 && na < m && nb < m && !seen[na, nb]) options.Add((na, nb));
            }
            if (options.Count == 0) { stack.Pop(); continue; }
            var (ca, cb) = options[rng.Next(options.Count)];
            seen[ca, cb] = true;
            links.Add((a, b, ca, cb));
            stack.Push((ca, cb));
        }
        for (int extra = 0; extra < 6; extra++)
        {
            int a = rng.Next(m), b = rng.Next(m);
            if (rng.Next(2) == 0 && a + 1 < m) links.Add((a, b, a + 1, b));
            else if (b + 1 < m) links.Add((a, b, a, b + 1));
        }

        var open = new bool[N, N];
        for (int i = 1; i < N - 1; i++)
            for (int j = 1; j < N - 1; j++)
            {
                Vector3 p = CellCenter(i, j);
                bool isOpen = false;
                for (int a = 0; a < m && !isOpen; a++)
                    for (int b = 0; b < m && !isOpen; b++)
                    {
                        float dx = p.x - RoomCenters[a], dz = p.z - RoomCenters[b];
                        if (dx * dx + dz * dz <= radius[a, b] * radius[a, b]) isOpen = true;
                    }
                foreach (var (a0, b0, a1, b1) in links)
                {
                    if (isOpen) break;
                    float x0 = RoomCenters[Mathf.Min(a0, a1)], x1 = RoomCenters[Mathf.Max(a0, a1)];
                    float z0 = RoomCenters[Mathf.Min(b0, b1)], z1 = RoomCenters[Mathf.Max(b0, b1)];
                    float w = CorridorWidth * 0.5f;
                    if (p.x >= x0 - w && p.x <= x1 + w && p.z >= z0 - w && p.z <= z1 + w) isOpen = true;
                }
                open[i, j] = isOpen;
            }
        return open;
    }

    /// <summary>Chão das células abertas + um bloco de terra por célula fechada (topo + lados que dão para o túnel), numa malha só.</summary>
    static Mesh WallMesh(bool[,] open, System.Random rng)
    {
        var heights = new float[N, N];
        for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
                heights[i, j] = open[i, j] ? 0f : WallHeight + (float)rng.NextDouble() * 0.9f;

        var verts = new List<Vector3>();
        var tops = new List<int>();
        var sides = new List<int>();
        var floors = new List<int>();
        void Quad(List<int> list, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            list.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        float hc = Cell * 0.5f;
        for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
            {
                if (open[i, j])
                {
                    Vector3 f = CellCenter(i, j);
                    Quad(floors, f + new Vector3(-hc, 0f, -hc), f + new Vector3(-hc, 0f, hc), f + new Vector3(hc, 0f, hc), f + new Vector3(hc, 0f, -hc));
                    continue;
                }
                float h = heights[i, j];
                Vector3 c = CellCenter(i, j);
                Vector3 p00 = c + new Vector3(-hc, h, -hc), p10 = c + new Vector3(hc, h, -hc);
                Vector3 p01 = c + new Vector3(-hc, h, hc), p11 = c + new Vector3(hc, h, hc);
                Quad(tops, p00, p01, p11, p10);
                // lados: virados para célula mais baixa (túnel ou parede menor)
                float H(int a, int b) => a < 0 || b < 0 || a >= N || b >= N ? h : heights[a, b];
                float hx1 = H(i + 1, j), hx0 = H(i - 1, j), hz1 = H(i, j + 1), hz0 = H(i, j - 1);
                if (hx1 < h) Quad(sides, c + new Vector3(hc, hx1, -hc), c + new Vector3(hc, h, -hc), c + new Vector3(hc, h, hc), c + new Vector3(hc, hx1, hc));
                if (hx0 < h) Quad(sides, c + new Vector3(-hc, hx0, hc), c + new Vector3(-hc, h, hc), c + new Vector3(-hc, h, -hc), c + new Vector3(-hc, hx0, -hc));
                if (hz1 < h) Quad(sides, c + new Vector3(hc, hz1, hc), c + new Vector3(hc, h, hc), c + new Vector3(-hc, h, hc), c + new Vector3(-hc, hz1, hc));
                if (hz0 < h) Quad(sides, c + new Vector3(-hc, hz0, -hc), c + new Vector3(-hc, h, -hc), c + new Vector3(hc, h, -hc), c + new Vector3(hc, hz0, -hc));
            }
        var mesh = new Mesh { name = "FormigueiroWalls" };
        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(tops, 0);
        mesh.SetTriangles(sides, 1);
        mesh.SetTriangles(floors, 2);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
