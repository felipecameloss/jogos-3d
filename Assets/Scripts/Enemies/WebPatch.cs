using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Teia no chão (da aranha). Desenha uma teia de verdade (raios + espiral) gerada por
/// código. Se o player pisar, fica preso (PlayerMovement.Root) e a teia gruda nele até
/// soltar; depois some. Sem ninguém pisar, dura `lifetime` e some com fade.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class WebPatch : MonoBehaviour
{
    [SerializeField, Min(0.3f)] float radius = 1.7f;
    [SerializeField, Min(0.5f)] float lifetime = 7f;
    [SerializeField, Min(3)] int spokes = 9;
    [SerializeField, Min(1)] int rings = 4;
    [SerializeField, Min(0.005f)] float strandWidth = 0.06f;
    [SerializeField] Color color = new(1f, 1f, 1f, 0.9f);

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public bool HasCaught { get; private set; }
    public float Radius => radius;

    PlayerMovement player;
    float rootDuration;
    GameObject owner;
    MeshRenderer meshRenderer;
    MaterialPropertyBlock block;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        block = new MaterialPropertyBlock();
        GetComponent<MeshFilter>().sharedMesh = BuildWebMesh(radius, spokes, rings, strandWidth);
    }

    /// <summary>Começa a teia. `caught` != null: acerto direto, já prende esse player.</summary>
    public void Begin(float rootDuration, GameObject owner, PlayerMovement caught = null)
    {
        this.rootDuration = rootDuration;
        this.owner = owner;
        if (caught != null) TryCatch(caught);
        StartCoroutine(Life());
    }

    void Update()
    {
        if (HasCaught) return;
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.GetComponent<PlayerMovement>();
            if (player == null) return;
        }
        Vector3 d = player.transform.position - transform.position;
        if (Mathf.Abs(d.y) < 1.2f && new Vector2(d.x, d.z).sqrMagnitude <= radius * radius) TryCatch(player);
    }

    void TryCatch(PlayerMovement target)
    {
        if (HasCaught || !target.Root(rootDuration)) return;
        HasCaught = true;
        player = target;
        StopAllCoroutines();
        StartCoroutine(Stuck());
    }

    // Gruda nos pés do player enquanto ele está preso, depois some rápido
    IEnumerator Stuck()
    {
        for (float t = 0f; player != null && player.IsRooted; t += Time.deltaTime)
        {
            transform.position = player.transform.position + Vector3.up * 0.04f;
            float pop = 1f + 0.15f * Mathf.Exp(-t * 10f) * Mathf.Sin(t * 40f); // tremidinha ao prender
            transform.localScale = new Vector3(pop, 1f, pop);
            SetAlpha(1f);
            yield return null;
        }
        yield return Fade(0.35f);
        Destroy(gameObject);
    }

    IEnumerator Life()
    {
        for (float t = 0f; t < 0.15f; t += Time.deltaTime)
        {
            float k = t / 0.15f;
            transform.localScale = new Vector3(k, 1f, k);
            yield return null;
        }
        transform.localScale = Vector3.one;
        yield return new WaitForSeconds(Mathf.Max(0f, lifetime - 1.15f));
        yield return Fade(1f);
        Destroy(gameObject);
    }

    IEnumerator Fade(float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            SetAlpha(1f - t / duration);
            yield return null;
        }
    }

    void SetAlpha(float a)
    {
        meshRenderer.GetPropertyBlock(block);
        var c = color; c.a *= a;
        block.SetColor(BaseColorId, c);
        meshRenderer.SetPropertyBlock(block);
    }

    /// <summary>Teia plana: `spokes` raios do centro até a borda e `rings` anéis poligonais.</summary>
    public static Mesh BuildWebMesh(float radius, int spokes, int rings, float width)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();

        void Strand(Vector3 a, Vector3 b)
        {
            Vector3 dir = (b - a).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * (width * 0.5f);
            int i = verts.Count;
            verts.Add(a - side); verts.Add(a + side); verts.Add(b + side); verts.Add(b - side);
            tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 }); // dupla face
        }

        Vector3 Point(int spoke, float r)
        {
            float a = spoke * Mathf.PI * 2f / spokes;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
        }

        for (int s = 0; s < spokes; s++) Strand(Vector3.zero, Point(s, radius));
        for (int r = 1; r <= rings; r++)
        {
            float rr = radius * r / (rings + 0.35f);
            for (int s = 0; s < spokes; s++)
                // levemente "caído" no meio de cada trecho, como fio de teia
                Strand(Point(s, rr), Point((s + 1) % spokes, rr));
        }

        var mesh = new Mesh { name = "Web" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
