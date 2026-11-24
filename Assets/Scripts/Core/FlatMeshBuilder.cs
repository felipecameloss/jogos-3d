using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Monta malhas low-poly em código, com sombreado chapado (cada triângulo com seus vértices),
/// no estilo dos modelos do Blender. Cada submalha vira um material/cor no MeshRenderer.
/// Usado pelo cogumelo de cura, pelas joaninhas e pela margarida das joaninhas.
/// </summary>
public class FlatMeshBuilder
{
    readonly List<Vector3> verts = new();
    readonly List<int>[] tris;

    public FlatMeshBuilder(int submeshes)
    {
        tris = new List<int>[submeshes];
        for (int i = 0; i < submeshes; i++) tris[i] = new List<int>();
    }

    /// <summary>Gira o perfil (raio, altura) em volta do eixo Y. Perfil de baixo para cima deixa a face para fora.</summary>
    public void Lathe(int submesh, Vector2[] profile, int segments, Matrix4x4 m)
    {
        for (int i = 0; i < profile.Length - 1; i++)
        {
            for (int j = 0; j < segments; j++)
            {
                Vector3 a = Point(profile[i], j, segments), b = Point(profile[i + 1], j, segments);
                Vector3 c = Point(profile[i], j + 1, segments), d = Point(profile[i + 1], j + 1, segments);
                if (profile[i].x > 0f) Tri(submesh, m.MultiplyPoint3x4(a), m.MultiplyPoint3x4(b), m.MultiplyPoint3x4(c));
                if (profile[i + 1].x > 0f) Tri(submesh, m.MultiplyPoint3x4(b), m.MultiplyPoint3x4(d), m.MultiplyPoint3x4(c));
            }
        }
    }

    /// <summary>Perfil de uma esfera de raio 1 centrada na origem (para Lathe), de baixo para cima.</summary>
    public static Vector2[] SphereProfile(int rings)
    {
        var p = new Vector2[rings + 1];
        for (int i = 0; i <= rings; i++)
        {
            float a = Mathf.PI * i / rings - Mathf.PI * 0.5f;
            p[i] = new Vector2(i == 0 || i == rings ? 0f : Mathf.Cos(a), Mathf.Sin(a));
        }
        return p;
    }

    /// <summary>Triângulo solto (a face aparece de quem vê a, b, c em sentido horário).</summary>
    public void Tri(int submesh, Vector3 a, Vector3 b, Vector3 c)
    {
        int start = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        tris[submesh].Add(start); tris[submesh].Add(start + 1); tris[submesh].Add(start + 2);
    }

    public Mesh ToMesh(string name)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.subMeshCount = tris.Length;
        for (int i = 0; i < tris.Length; i++) mesh.SetTriangles(tris[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Vector3 Point(Vector2 p, int j, int segments)
    {
        float a = j * Mathf.PI * 2f / segments;
        return new Vector3(Mathf.Cos(a) * p.x, p.y, Mathf.Sin(a) * p.x);
    }
}
