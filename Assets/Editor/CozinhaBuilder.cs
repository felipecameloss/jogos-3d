using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fase final: cozinha (issue #40). Cenário próprio (com NavMesh assado) dentro de casa: piso
/// xadrez, armários altos no fundo e na lateral, geladeira e fogão; o balcão no meio (7 m) com
/// pia (funda, com uma poça) e coisas em cima, e dois jeitos de subir: a tábua de carne
/// inclinada (rampa: inimigos também sobem) e uma escada de caixas (só pulando). Migalhas por
/// todo lado: gigantes de enfeite e de verdade espalhadas pelo chão (StageDefinition.scatteredCrumbs).
/// Luz de lâmpada fixa (Indoor). A tabela de inimigos mais pesada e a aranha-rainha de chefe.
/// Pode rodar de novo. Menu: Jogo/Fases/Criar fase final: cozinha. Linha de comando: -executeMethod CozinhaBuilder.Run
/// </summary>
public static class CozinhaBuilder
{
    const float Half = 46f;
    /// <summary>Altura do tampo do balcão.</summary>
    public const float CounterTop = 7.1f;
    const float Tile = 4f;

    [MenuItem("Jogo/Fases/Criar fase final: cozinha")]
    public static void Run()
    {
        var tileLight = StageBuilderUtils.Flat("CozinhaTileLight", new Color(0.9f, 0.88f, 0.82f), 0.45f);
        var tileDark = StageBuilderUtils.Flat("CozinhaTileDark", new Color(0.32f, 0.36f, 0.42f), 0.45f);
        var cabinet = StageBuilderUtils.Flat("CozinhaCabinet", new Color(0.88f, 0.82f, 0.7f), 0.25f);
        var cabinetDoor = StageBuilderUtils.Flat("CozinhaCabinetDoor", new Color(0.8f, 0.72f, 0.58f), 0.25f);
        var metal = StageBuilderUtils.Flat("CozinhaMetal", new Color(0.72f, 0.74f, 0.78f), 0.75f);
        var granite = StageBuilderUtils.Flat("CozinhaGranite", new Color(0.24f, 0.24f, 0.27f), 0.55f);
        var white = StageBuilderUtils.Flat("CozinhaWhite", new Color(0.95f, 0.95f, 0.96f), 0.5f);
        var board = StageBuilderUtils.Flat("CozinhaBoard", new Color(0.74f, 0.53f, 0.3f), 0.2f);
        var box1 = StageBuilderUtils.Flat("CozinhaCerealBox", new Color(0.9f, 0.35f, 0.12f), 0.2f);
        var box2 = StageBuilderUtils.Flat("CozinhaBook", new Color(0.2f, 0.42f, 0.7f), 0.2f);
        var box3 = StageBuilderUtils.Flat("CozinhaMug", new Color(0.85f, 0.2f, 0.25f), 0.4f);
        var crumb = StageBuilderUtils.Flat("CozinhaCrumb", new Color(0.83f, 0.6f, 0.32f), 0.1f);
        var crumbDark = StageBuilderUtils.Flat("CozinhaCrumbDark", new Color(0.55f, 0.33f, 0.14f), 0.1f);
        var cookie = StageBuilderUtils.Flat("CozinhaCookie", new Color(0.78f, 0.55f, 0.28f), 0.1f);
        var chip = StageBuilderUtils.Flat("CozinhaChocolate", new Color(0.25f, 0.13f, 0.06f), 0.2f);
        var cereal = StageBuilderUtils.Flat("CozinhaCereal", new Color(0.98f, 0.78f, 0.2f), 0.2f);
        var wood = StageBuilderUtils.Flat("CozinhaChairWood", new Color(0.48f, 0.3f, 0.16f), 0.2f);
        var flour = StageBuilderUtils.Transparent("CozinhaFlour", new Color(1f, 0.98f, 0.94f, 0.75f));
        var water = StageBuilderUtils.Transparent("CozinhaWater", new Color(0.55f, 0.75f, 0.95f, 0.5f));
        var floorMesh = StageBuilderUtils.SaveMesh(CheckerMesh(), "CozinhaFloor");

        var prefab = StageBuilderUtils.BuildLevel("Cozinha", bakeNavMesh: true, root =>
        {
            var rng = new System.Random(40);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // ---- piso xadrez (uma malha) com colisor de caixa
            var floor = new GameObject("Piso");
            floor.transform.SetParent(root, false);
            floor.AddComponent<MeshFilter>().sharedMesh = floorMesh;
            floor.AddComponent<MeshRenderer>().sharedMaterials = new[] { tileLight, tileDark };
            var fc = floor.AddComponent<BoxCollider>();
            fc.center = new Vector3(0f, -0.5f, 0f);
            fc.size = new Vector3(Half * 2f + 2f, 1f, Half * 2f + 2f);
            StageBuilderUtils.InvisibleBounds(root, Half, 30f);

            // ---- armários altos no fundo (norte) e na lateral (oeste)
            var cabinets = StageBuilderUtils.Group(root, "Armarios");
            Cabinets(cabinets, new Vector3(0f, 0f, Half - 3f), new Vector3(Half * 2f, 24f, 6f), Vector3.back, cabinet, cabinetDoor, metal);
            Cabinets(cabinets, new Vector3(-Half + 3f, 0f, 0f), new Vector3(6f, 24f, Half * 2f - 12f), Vector3.right, cabinet, cabinetDoor, metal);
            // geladeira e fogão na lateral leste
            StageBuilderUtils.Block(cabinets, "Geladeira", new Vector3(Half - 5f, 0f, 26f), new Vector3(10f, 40f, 16f), white);
            StageBuilderUtils.Block(cabinets, "PuxadorGeladeira", new Vector3(Half - 10.4f, 12f, 20f), new Vector3(0.6f, 10f, 0.8f), metal);
            StageBuilderUtils.Block(cabinets, "Fogao", new Vector3(Half - 5f, 0f, 6f), new Vector3(10f, 22f, 14f), white);
            StageBuilderUtils.Block(cabinets, "PortaDoForno", new Vector3(Half - 10.3f, 4f, 6f), new Vector3(0.5f, 9f, 11f), granite);
            StageBuilderUtils.Block(cabinets, "PuxadorDoForno", new Vector3(Half - 10.8f, 13.6f, 6f), new Vector3(0.5f, 0.6f, 9f), metal);
            foreach (float z in new[] { -6f, -20f, -34f })
                StageBuilderUtils.Block(cabinets, "Armario", new Vector3(Half - 4f, 0f, z), new Vector3(8f, 18f, 12.5f), cabinet);
            for (int i = 0; i < 5; i++) // luzes embaixo dos armários do fundo
                StageBuilderUtils.PointLight(cabinets, "LuzDoArmario", new Vector3(-32f + i * 16f, 10f, Half - 9f), new Color(1f, 0.88f, 0.7f), 18f, 1.1f);

            // ---- o balcão (ilha no meio) com pia funda
            var counter = StageBuilderUtils.Group(root, "Balcao");
            var cc = new Vector3(0f, 0f, 8f);
            Vector3 size = new(30f, CounterTop - 0.6f, 11f);
            StageBuilderUtils.Block(counter, "Corpo", cc, size, cabinet);
            for (float x = -12f; x <= 12f; x += 8f)
                StageBuilderUtils.Block(counter, "Porta", cc + new Vector3(x, 0.6f, -size.z * 0.5f - 0.15f), new Vector3(7f, size.y - 1.4f, 0.3f), cabinetDoor);
            // tampo em pedaços em volta da pia (x 6..14, z 6..10)
            float topY = size.y;
            var top = new Vector3(32f, 0.6f, 13f);
            Vector3 tc = cc + Vector3.up * topY;
            StageBuilderUtils.Block(counter, "Tampo", tc + new Vector3(-6f, 0f, 0f), new Vector3(20f, 0.6f, 13f), granite);
            StageBuilderUtils.Block(counter, "Tampo", tc + new Vector3(13f, 0f, 0f), new Vector3(6f, 0.6f, 13f), granite);
            StageBuilderUtils.Block(counter, "Tampo", tc + new Vector3(8f, 0f, -4.75f), new Vector3(4f, 0.6f, 3.5f), granite);
            StageBuilderUtils.Block(counter, "Tampo", tc + new Vector3(8f, 0f, 4.75f), new Vector3(4f, 0.6f, 3.5f), granite);
            // cuba da pia: rebaixo de metal no buraco do tampo, com uma poça (molha e deixa lento)
            StageBuilderUtils.Block(counter, "Cuba", tc + new Vector3(8f, 0f, 0f), new Vector3(4f, 0.1f, 7f), metal);
            StageBuilderUtils.Prim(PrimitiveType.Cylinder, counter, "AguaDaPia", tc + new Vector3(8f, 0.35f, 0f),
                new Vector3(3.4f, 0.01f, 3.4f), water, collider: false);
            var zone = StageBuilderUtils.Group(counter, "PocaDaPia");
            zone.localPosition = tc + new Vector3(8f, 0f, 0f);
            zone.gameObject.AddComponent<SlowZone>().Configure(SlowZone.Kind.Water, 2f, 0.7f, 0.55f);
            // torneira
            StageBuilderUtils.Pillar(counter, "Torneira", tc + new Vector3(8f, 0.6f, 4.5f), 0.9f, 5f, metal);
            StageBuilderUtils.Box(counter, "BicoDaTorneira", tc + new Vector3(8f, 5.3f, 2.8f), new Vector3(0.7f, 0.7f, 3.8f), metal);
            // coisas no tampo
            StageBuilderUtils.Pillar(counter, "Pote", tc + new Vector3(-11f, 0.6f, 2.5f), 3.2f, 5f, StageBuilderUtils.Transparent("CozinhaJar", new Color(0.75f, 0.9f, 0.95f, 0.45f)));
            StageBuilderUtils.Pillar(counter, "TampaDoPote", tc + new Vector3(-11f, 5.6f, 2.5f), 3.4f, 0.6f, box3);
            for (int i = 0; i < 4; i++)
                StageBuilderUtils.Pillar(counter, "Prato", tc + new Vector3(-3f, 0.6f + i * 0.25f, 3f), 7f - i * 0.2f, 0.25f, white);
            for (int i = 0; i < 6; i++)
                StageBuilderUtils.Block(counter, "CuboDeAcucar", tc + new Vector3(R(-14f, -6f), 0.6f, R(-4.5f, -1f)), Vector3.one * 1.3f, white, yaw: R(0f, 90f));

            // ---- subidas: tábua de carne inclinada (rampa) e escada de caixas (pulando)
            var ramp = StageBuilderUtils.Group(root, "Subidas");
            Vector3 bottom = new(-9f, 0f, cc.z - 5.5f - 12.5f), topEdge = new(-9f, CounterTop, cc.z - 5.5f + 0.4f);
            Vector3 mid = (bottom + topEdge) * 0.5f;
            float len = Vector3.Distance(bottom, topEdge);
            var tilt = Quaternion.LookRotation(topEdge - bottom);
            StageBuilderUtils.Prim(PrimitiveType.Cube, ramp, "TabuaDeCarne", mid + tilt * Vector3.down * 0.25f,
                new Vector3(5f, 0.5f, len + 0.6f), board, collider: true, rotation: tilt);
            float[] steps = { 1.8f, 3.6f, 5.4f, CounterTop };
            Material[] stepMats = { box3, box2, box1, box2 };
            for (int i = 0; i < steps.Length; i++)
            {
                float h = steps[i];
                StageBuilderUtils.Block(ramp, "Degrau", new Vector3(15.5f + 0f, 0f, cc.z - 5.5f - 3f - (steps.Length - 1 - i) * 3.4f),
                    new Vector3(5f, h, 3.4f), stepMats[i]);
            }

            // ---- migalhas gigantes, açúcar, biscoito, cereal, colher, farinha e pés de cadeira
            var floorStuff = StageBuilderUtils.Group(root, "ChaoDaCozinha");
            var chairAt = new Vector3(-24f, 0f, -24f);
            var taken = new List<(Vector3, float)>
            {
                (cc, 17f), (new Vector3(0f, 0f, -38f), 6f),            // balcão e começo da fase
                (new Vector3(-9f, 0f, -5f), 8f), (new Vector3(15.5f, 0f, -6f), 7.5f), // rampa e escada
                (chairAt, 8f),                                          // pés da cadeira
            };
            bool Free(Vector3 c, float r)
            {
                if (Mathf.Abs(c.x) > 36f - r || c.z > 34f - r || c.z < -42f + r) return false;
                foreach (var (tc2, tr) in taken) if ((tc2 - c).sqrMagnitude < (tr + r) * (tr + r)) return false;
                return true;
            }
            Vector3 Spot(float r)
            {
                for (int a = 0; a < 300; a++)
                {
                    var c = new Vector3(R(-36f, 36f), 0f, R(-42f, 34f));
                    if (Free(c, r)) { taken.Add((c, r)); return c; }
                }
                return new Vector3(0f, -100f, 0f);
            }
            for (int i = 0; i < 14; i++) // migalhas grandes de enfeite (sem colisor: o player passa por cima)
            {
                var c = Spot(1.5f);
                StageBuilderUtils.Prim(PrimitiveType.Cube, floorStuff, "Migalhona", c + Vector3.up * 0.4f, new Vector3(R(1f, 2.2f), R(0.6f, 1.2f), R(1f, 2f)),
                    rng.NextDouble() < 0.3 ? crumbDark : crumb, collider: false, rotation: Quaternion.Euler(R(-20f, 20f), R(0f, 360f), R(-20f, 20f)));
            }
            for (int i = 0; i < 8; i++)
                StageBuilderUtils.Block(floorStuff, "CuboDeAcucar", Spot(1.8f), Vector3.one * R(1.4f, 1.9f), white, yaw: R(0f, 90f));
            var cookieAt = Spot(5f);
            StageBuilderUtils.Pillar(floorStuff, "Biscoito", cookieAt, 9f, 0.9f, cookie);
            for (int i = 0; i < 7; i++)
                StageBuilderUtils.Block(floorStuff, "GotaDeChocolate", cookieAt + new Vector3(R(-3f, 3f), 0.9f, R(-3f, 3f)), new Vector3(0.9f, 0.5f, 0.9f), chip, collider: false);
            for (int i = 0; i < 10; i++)
            {
                var c = Spot(1.6f);
                StageBuilderUtils.Pillar(floorStuff, "Cereal", c, 2.6f, 0.7f, cereal, collider: false);
                StageBuilderUtils.Pillar(floorStuff, "FuroDoCereal", c + Vector3.up * 0.71f, 0.9f, 0.02f, crumbDark, collider: false);
            }
            var flourAt = Spot(5f);
            StageBuilderUtils.Prim(PrimitiveType.Cylinder, floorStuff, "Farinha", flourAt + Vector3.up * 0.03f, new Vector3(10f, 0.01f, 7f), flour, collider: false);
            var spoonAt = Spot(5f);
            StageBuilderUtils.Box(floorStuff, "CaboDaColher", spoonAt + new Vector3(0f, 0.35f, -2f), new Vector3(1.2f, 0.5f, 9f), metal, yaw: 20f);
            StageBuilderUtils.Prim(PrimitiveType.Sphere, floorStuff, "ConchaDaColher", spoonAt + new Vector3(1.6f, 0.4f, 2.6f), new Vector3(3f, 0.8f, 4.2f), metal,
                rotation: Quaternion.Euler(0f, 20f, 0f));
            // pés de cadeira (a cadeira fica lá em cima, fora da tela)
            foreach (var o in new[] { new Vector3(-5f, 0f, -5f), new Vector3(5f, 0f, -5f), new Vector3(-5f, 0f, 5f), new Vector3(5f, 0f, 5f) })
                StageBuilderUtils.Pillar(floorStuff, "PeDaCadeira", chairAt + o, 1.6f, 30f, wood);

            StageBuilderUtils.Marker(root, "PlayerSpawn", new Vector3(0f, 0.1f, -38f));
        });

        var def = StageBuilderUtils.Stage("Cozinha", so =>
        {
            so.FindProperty("displayName").stringValue = "Cozinha";
            so.FindProperty("description").stringValue = "A fase final, dentro de casa: piso de azulejo, migalhas por todo lado e o balcão para subir (pela tábua de carne ou pulando as caixas).";
            so.FindProperty("useBackyard").boolValue = false;
            so.FindProperty("levelPrefab").objectReferenceValue = prefab;
            so.FindProperty("backyardAtmosphere").boolValue = false;
            so.FindProperty("lighting").enumValueIndex = (int)StageDefinition.LightingMode.Indoor;
            so.FindProperty("sunTint").colorValue = Color.white;
            so.FindProperty("lightScale").floatValue = 1f;
            so.FindProperty("fogDensity").floatValue = 0.01f;
            so.FindProperty("fogColor").colorValue = new Color(0.36f, 0.31f, 0.26f);
            so.FindProperty("indoorSky").colorValue = new Color(0.85f, 0.78f, 0.66f);
            so.FindProperty("indoorEquator").colorValue = new Color(0.62f, 0.55f, 0.45f);
            so.FindProperty("indoorGround").colorValue = new Color(0.34f, 0.3f, 0.26f);
            so.FindProperty("humanStomps").boolValue = true;
            so.FindProperty("rain").boolValue = false;
            so.FindProperty("magnifier").boolValue = false;
            so.FindProperty("soapBubbles").boolValue = true;
            StageBuilderUtils.SetEnemies(so,
                new StageBuilderUtils.Spawn("WorkerAnt", 8f, 0.8f, minGroup: 2, extraGroup: 2, prewarm: 12),
                new StageBuilderUtils.Spawn("SoldierAnt", 24f, 0.5f, prewarm: 5),
                new StageBuilderUtils.Spawn("BombardierBeetle", 14f, 0.5f, prewarm: 4),
                new StageBuilderUtils.Spawn("Grasshopper", 16f, 0.5f, prewarm: 4),
                new StageBuilderUtils.Spawn("Spider", 16f, 0.5f, prewarm: 4),
                new StageBuilderUtils.Spawn("Centipede", 55f, 0.3f, 1.2f, prewarm: 2),
                new StageBuilderUtils.Spawn("Slug", 18f, 0.3f, prewarm: 3));
            so.FindProperty("boss").objectReferenceValue = StageBuilderUtils.Enemy("SpiderQueen");
            so.FindProperty("playerLantern").boolValue = false;
            so.FindProperty("scatteredCrumbs").intValue = 45;
        });
        StageBuilderUtils.PutInSequence(def, 5);
        Debug.Log("[CozinhaBuilder] pronto");
    }

    /// <summary>Fileira de armários: corpo, portas com puxador e rodapé escuro, virada para `facing`.</summary>
    static void Cabinets(Transform parent, Vector3 baseCenter, Vector3 size, Vector3 facing, Material body, Material door, Material handle)
    {
        StageBuilderUtils.Block(parent, "Armarios", baseCenter, size, body);
        bool alongX = Mathf.Abs(facing.z) > 0.5f;
        float length = alongX ? size.x : size.z;
        float depth = alongX ? size.z : size.x;
        Vector3 along = alongX ? Vector3.right : Vector3.forward;
        Vector3 front = baseCenter + facing * (depth * 0.5f + 0.15f);
        for (float t = -length * 0.5f + 5f; t <= length * 0.5f - 4f; t += 9f)
        {
            var p = front + along * t;
            var doorSize = alongX ? new Vector3(8f, size.y - 4f, 0.3f) : new Vector3(0.3f, size.y - 4f, 8f);
            StageBuilderUtils.Block(parent, "Porta", p + Vector3.up * 2f, doorSize, door);
            var hs = alongX ? new Vector3(0.5f, 3f, 0.5f) : new Vector3(0.5f, 3f, 0.5f);
            StageBuilderUtils.Block(parent, "Puxador", p + along * 3f + facing * 0.3f + Vector3.up * (size.y * 0.5f - 1f), hs, handle, collider: false);
        }
    }

    /// <summary>Piso xadrez: uma malha com dois materiais (claro e escuro).</summary>
    static Mesh CheckerMesh()
    {
        var verts = new List<Vector3>();
        var light = new List<int>();
        var dark = new List<int>();
        int n = Mathf.CeilToInt((Half * 2f + 8f) / Tile);
        float start = -n * Tile * 0.5f;
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                float x0 = start + i * Tile, z0 = start + j * Tile;
                int v = verts.Count;
                verts.Add(new Vector3(x0, 0f, z0));
                verts.Add(new Vector3(x0, 0f, z0 + Tile));
                verts.Add(new Vector3(x0 + Tile, 0f, z0 + Tile));
                verts.Add(new Vector3(x0 + Tile, 0f, z0));
                ((i + j) % 2 == 0 ? light : dark).AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
            }
        var mesh = new Mesh { name = "CozinhaFloor" };
        mesh.SetVertices(verts);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(light, 0);
        mesh.SetTriangles(dark, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
