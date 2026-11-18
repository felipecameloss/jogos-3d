using System.Collections;
using UnityEngine;

/// <summary>
/// Efeitos visuais simples dos itens (linha de ricochete, coluna de raio de sol).
/// Fica no _Systems com os materiais configurados; itens chamam ItemVfx.Line / Beam.
/// Se não estiver na cena, os efeitos só não aparecem (o dano funciona igual).
/// </summary>
public class ItemVfx : MonoBehaviour
{
    public static ItemVfx Instance { get; private set; }

    [Tooltip("Material para linhas (Particles/Unlit).")]
    [SerializeField] Material lineMaterial;
    [Tooltip("Material para colunas de luz (Unlit transparente).")]
    [SerializeField] Material beamMaterial;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Linha que some rápido (ricochete, raio elétrico).</summary>
    public static void Line(Vector3 from, Vector3 to, Color color, float width = 0.08f, float duration = 0.15f)
    {
        if (Instance == null || Instance.lineMaterial == null) return;
        var go = new GameObject("VFX Line");
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = Instance.lineMaterial;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.widthMultiplier = width;
        lr.startColor = lr.endColor = color;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Instance.StartCoroutine(FadeLine(lr, color, duration));
    }

    static IEnumerator FadeLine(LineRenderer lr, Color color, float duration)
    {
        for (float t = 0f; t < duration && lr != null; t += Time.deltaTime)
        {
            var c = color;
            c.a *= 1f - t / duration;
            lr.startColor = lr.endColor = c;
            yield return null;
        }
        if (lr != null) Destroy(lr.gameObject);
    }

    /// <summary>Coluna de luz vinda do céu sobre um ponto (raio de sol).</summary>
    public static void Beam(Vector3 groundPoint, Color color, float radius = 0.9f, float height = 40f, float duration = 0.4f)
    {
        if (Instance == null || Instance.beamMaterial == null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "VFX Beam";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = groundPoint + Vector3.up * (height * 0.5f);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = Instance.beamMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Instance.StartCoroutine(FadeBeam(go.transform, r, color, radius, height, duration));
    }

    static IEnumerator FadeBeam(Transform tr, Renderer r, Color color, float radius, float height, float duration)
    {
        var block = new MaterialPropertyBlock();
        for (float t = 0f; t < duration && tr != null; t += Time.deltaTime)
        {
            float k = t / duration;
            float w = radius * 2f * (k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f); // abre rápido, fecha devagar
            tr.localScale = new Vector3(w, height * 0.5f, w);
            var c = color;
            c.a *= 1f - k * 0.6f;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(block);
            yield return null;
        }
        if (tr != null) Destroy(tr.gameObject);
    }

    /// <summary>Esfera que expande e some rápido (explosão ao matar).</summary>
    public static void Explosion(Vector3 center, float radius, Color color, float duration = 0.28f)
    {
        if (Instance == null || Instance.beamMaterial == null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "VFX Explosion";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = center;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = Instance.beamMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Instance.StartCoroutine(FadeExplosion(go.transform, r, color, radius, duration));
    }

    static IEnumerator FadeExplosion(Transform tr, Renderer r, Color color, float radius, float duration)
    {
        var block = new MaterialPropertyBlock();
        for (float t = 0f; t < duration && tr != null; t += Time.deltaTime)
        {
            float k = t / duration;
            float d = radius * 2f * Mathf.Lerp(0.25f, 1f, 1f - (1f - k) * (1f - k));
            tr.localScale = Vector3.one * d;
            var c = color;
            c.a *= 1f - k;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(block);
            yield return null;
        }
        if (tr != null) Destroy(tr.gameObject);
    }
}
