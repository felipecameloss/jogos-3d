using UnityEngine;

/// <summary>
/// Malhas low-poly feitas em código para as joaninhas e para a margarida onde elas são
/// compradas (cores chapadas por submalha, como os modelos do Blender). As malhas são
/// criadas uma vez e compartilhadas.
/// </summary>
public static class LadybugMeshes
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    // Joaninha: casco vermelho, preto (cabeça, pintas, risca, barriga), branco (olhos)
    static readonly Color[] LadybugColors = { new(0.9f, 0.1f, 0.1f), new(0.08f, 0.07f, 0.07f), Color.white };
    // Margarida: verde (talo, folhas), branco (pétalas), amarelo (miolo)
    static readonly Color[] DaisyColors = { new(0.3f, 0.68f, 0.2f), new(0.98f, 0.97f, 0.94f), new(1f, 0.8f, 0.15f) };

    const float ShellLength = 1.2f; // o casco é esticado na frente/trás

    static Mesh ladybugMesh, daisyMesh;

    /// <summary>Joaninha (frente para +Z, pivô no meio do corpo, ~0,6 m de comprimento na escala 1).</summary>
    public static GameObject CreateLadybug(Transform parent, Material material, float scale)
    {
        if (ladybugMesh == null) ladybugMesh = BuildLadybug();
        return CreatePart("Body", parent, ladybugMesh, material, LadybugColors, scale);
    }

    /// <summary>Margarida gigante (~1,4 m, pivô no chão).</summary>
    public static GameObject CreateDaisy(Transform parent, Material material)
    {
        if (daisyMesh == null) daisyMesh = BuildDaisy();
        return CreatePart("Daisy", parent, daisyMesh, material, DaisyColors, 1f);
    }

    /// <summary>Onde uma joaninha pousa na pétala `index` de `count` (local à margarida).</summary>
    public static Pose PerchPose(int index, int count)
    {
        float angle = 360f * index / count + 15f;
        var tilt = Quaternion.Euler(-12f, angle, 0f);
        Vector3 pos = new Vector3(0f, PetalHeight, 0f) + tilt * new Vector3(0f, 0.09f, 0.62f);
        return new Pose(pos, tilt); // olhando para a ponta da pétala, no mesmo declive
    }

    static GameObject CreatePart(string name, Transform parent, Mesh mesh, Material material, Color[] colors, float scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        if (material != null)
        {
            var mats = new Material[colors.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = material;
            r.sharedMaterials = mats;
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < colors.Length; i++)
            {
                r.GetPropertyBlock(block, i);
                block.SetColor(BaseColorId, colors[i]);
                r.SetPropertyBlock(block, i);
            }
        }
        else r.enabled = false;
        return go;
    }

    // ---------- Joaninha ----------

    static Mesh BuildLadybug()
    {
        var mb = new FlatMeshBuilder(3);
        var shellScale = Matrix4x4.Scale(new Vector3(1f, 1f, ShellLength));
        // Casco em domo (de baixo para cima) e a barriga preta
        Vector2[] shell = { new(0.24f, -0.06f), new(0.235f, 0f), new(0.2f, 0.07f), new(0.12f, 0.12f), new(0f, 0.14f) };
        mb.Lathe(0, shell, 12, shellScale);
        mb.Lathe(1, new Vector2[] { new(0f, -0.061f), new(0.24f, -0.061f) }, 12, shellScale);

        // Pintas pretas encostadas no casco (pares esquerda/direita)
        Vector2[] spot = { new(0f, -0.012f), new(0.042f, -0.012f), new(0.034f, 0.01f), new(0f, 0.016f) };
        (int seg, float t, float angle)[] spots = { (2, 0.6f, 42f), (2, 0.2f, 98f), (2, 0.55f, 148f) };
        foreach (var (seg, t, angle) in spots)
            foreach (float side in new[] { 1f, -1f })
                AddOnShell(mb, 1, shell, seg, t, angle * side, spot, 6);

        // Risca preta no meio do casco, de trás até o topo
        for (int i = 0; i < shell.Length - 1; i++)
        {
            Vector3 a = ShellPoint(shell[i], 180f, 0.003f), b = ShellPoint(shell[i + 1], 180f, 0.003f);
            Ribbon(mb, 1, a, b, 0.014f);
        }

        // Cabeça preta grande e olhos brancos com pupila
        var sphere = FlatMeshBuilder.SphereProfile(6);
        mb.Lathe(1, sphere, 10, Matrix4x4.TRS(new Vector3(0f, 0.03f, 0.27f), Quaternion.identity, new Vector3(0.11f, 0.095f, 0.1f)));
        foreach (float side in new[] { 1f, -1f })
        {
            mb.Lathe(2, sphere, 8, Matrix4x4.TRS(new Vector3(0.055f * side, 0.075f, 0.345f), Quaternion.identity, Vector3.one * 0.05f));
            mb.Lathe(1, sphere, 6, Matrix4x4.TRS(new Vector3(0.058f * side, 0.08f, 0.39f), Quaternion.identity, Vector3.one * 0.024f));
        }
        return mb.ToMesh("Ladybug");
    }

    /// <summary>Ponto do perfil do casco girado para o ângulo (0 = frente), já esticado, afastado `lift` da superfície.</summary>
    static Vector3 ShellPoint(Vector2 p, float angleDeg, float lift)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        var pos = new Vector3(Mathf.Sin(a) * p.x, p.y, Mathf.Cos(a) * p.x * ShellLength);
        return pos + ShellNormal(pos) * lift;
    }

    static Vector3 ShellNormal(Vector3 pos) =>
        new Vector3(pos.x, (pos.y + 0.06f) * 1.6f, pos.z / (ShellLength * ShellLength)).normalized;

    static void AddOnShell(FlatMeshBuilder mb, int submesh, Vector2[] shell, int seg, float t, float angleDeg, Vector2[] profile, int segments)
    {
        Vector2 p = Vector2.Lerp(shell[seg], shell[seg + 1], t);
        Vector3 pos = ShellPoint(p, angleDeg, 0f);
        var rot = Quaternion.FromToRotation(Vector3.up, ShellNormal(pos));
        mb.Lathe(submesh, profile, segments, Matrix4x4.TRS(pos, rot, Vector3.one));
    }

    /// <summary>Fita estreita de a até b, com a face para fora do casco.</summary>
    static void Ribbon(FlatMeshBuilder mb, int submesh, Vector3 a, Vector3 b, float halfWidth)
    {
        Vector3 side = Vector3.right * halfWidth;
        Vector3 a0 = a - side, a1 = a + side, b0 = b - side, b1 = b + side;
        Vector3 outward = ShellNormal((a + b) * 0.5f);
        if (Vector3.Dot(Vector3.Cross(b0 - a0, a1 - a0), outward) >= 0f)
        {
            mb.Tri(submesh, a0, b0, a1);
            mb.Tri(submesh, a1, b0, b1);
        }
        else
        {
            mb.Tri(submesh, a0, a1, b0);
            mb.Tri(submesh, a1, b1, b0);
        }
    }

    // ---------- Margarida ----------

    const float PetalHeight = 1.27f;

    static Mesh BuildDaisy()
    {
        var mb = new FlatMeshBuilder(3);
        var sphere = FlatMeshBuilder.SphereProfile(5);
        // Talo
        mb.Lathe(0, new Vector2[] { new(0f, 0f), new(0.075f, 0f), new(0.06f, 0.7f), new(0.055f, 1.25f), new(0f, 1.25f) }, 7, Matrix4x4.identity);
        // Duas folhas saindo do talo
        foreach (var (height, yaw, pitch) in new[] { (0.35f, 90f, -25f), (0.62f, -125f, -20f) })
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            var pos = new Vector3(0f, height, 0f) + rot * new Vector3(0f, 0f, 0.36f);
            mb.Lathe(0, sphere, 8, Matrix4x4.TRS(pos, rot, new Vector3(0.15f, 0.03f, 0.36f)));
        }
        // Pétalas brancas em volta, levemente levantadas nas pontas
        const int petals = 11;
        for (int i = 0; i < petals; i++)
        {
            var rot = Quaternion.Euler(-12f, 360f * i / petals, 0f);
            var pos = new Vector3(0f, PetalHeight, 0f) + rot * new Vector3(0f, 0f, 0.5f);
            mb.Lathe(1, sphere, 8, Matrix4x4.TRS(pos, rot, new Vector3(0.15f, 0.035f, 0.38f)));
        }
        // Miolo amarelo
        mb.Lathe(2, new Vector2[] { new(0f, 1.2f), new(0.27f, 1.22f), new(0.25f, 1.31f), new(0.14f, 1.37f), new(0f, 1.39f) }, 10, Matrix4x4.identity);
        return mb.ToMesh("LadybugDaisy");
    }
}
