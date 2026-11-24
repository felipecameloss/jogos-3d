using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cogumelo de cura no chão (Esporo de cogumelo). Brota onde o inimigo morreu, cura
/// quem ficar dentro do círculo por alguns segundos e murcha. A malha é low-poly e
/// montada em código (cores chapadas, sem textura), então não precisa de prefab.
/// </summary>
public class HealingMushroom : MonoBehaviour
{
    /// <summary>Cogumelos que ainda curam (os que estão murchando saem da lista).</summary>
    public static readonly List<HealingMushroom> Active = new();

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly Color StemColor = new(0.98f, 0.93f, 0.8f);
    static readonly Color CapColor = new(0.9f, 0.2f, 0.42f);
    static readonly Color SpotColor = new(1f, 0.98f, 0.95f);
    static readonly Color AreaColor = new(1f, 0.55f, 0.78f, 1f);

    const float GrowTime = 0.35f;
    const float WitherTime = 0.6f;
    const float TickInterval = 0.25f;
    const float MaxHeightDifference = 3f;
    const float MushroomSize = 1.1f;

    static Mesh bodyMesh, areaMesh;

    Health target;
    float radius, lifetime, healFractionPerSecond;
    float age, tickTimer, puff, areaGlow;
    bool withering;
    Transform body, area;
    MeshRenderer areaRenderer;
    MaterialPropertyBlock areaBlock;
    float sizeJitter = 1f;

    public float Radius => radius;
    public bool IsWithering => withering;

    // O projeto entra no Play Mode sem recarregar o domínio: a lista estática não zera sozinha
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Active.Clear();

    /// <summary>
    /// Brota um cogumelo em <paramref name="groundPoint"/>. Cura <paramref name="target"/>
    /// em <paramref name="healFractionPerSecond"/> da vida máxima por segundo enquanto ele
    /// estiver a até <paramref name="radius"/> metros. Sem materiais, só não aparece (a cura funciona igual).
    /// </summary>
    public static HealingMushroom Spawn(Vector3 groundPoint, Health target, float radius, float lifetime,
        float healFractionPerSecond, Material bodyMaterial = null, Material areaMaterial = null)
    {
        var go = new GameObject("HealingMushroom");
        go.transform.position = groundPoint;
        var m = go.AddComponent<HealingMushroom>();
        m.target = target;
        m.radius = Mathf.Max(0.5f, radius);
        m.lifetime = Mathf.Max(GrowTime + WitherTime, lifetime);
        m.healFractionPerSecond = Mathf.Max(0f, healFractionPerSecond);
        m.sizeJitter = Random.Range(0.9f, 1.1f);
        m.BuildVisual(bodyMaterial, areaMaterial);
        m.Animate();
        return m;
    }

    /// <summary>Primeiro chão embaixo do ponto (ignora player e inimigos); sem chão, o próprio ponto.</summary>
    public static Vector3 GroundBelow(Vector3 point)
    {
        var hits = Physics.RaycastAll(point + Vector3.up * 0.5f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
        Vector3 ground = point;
        float best = float.MaxValue;
        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<PlayerMovement>() != null || h.collider.GetComponentInParent<EnemyBase>() != null) continue;
            if (h.distance < best) { best = h.distance; ground = h.point; }
        }
        return ground;
    }

    /// <summary>Começa a murchar agora (sai de <see cref="Active"/> e para de curar).</summary>
    public void Wither()
    {
        if (withering) return;
        withering = true;
        Active.Remove(this);
        age = Mathf.Max(age, lifetime - WitherTime);
    }

    /// <summary>O alvo está dentro do círculo (distância no chão + pouca diferença de altura).</summary>
    public bool IsInside(Vector3 position)
    {
        Vector3 d = position - transform.position;
        if (Mathf.Abs(d.y) > MaxHeightDifference) return false;
        d.y = 0f;
        return d.sqrMagnitude <= radius * radius;
    }

    void OnEnable()
    {
        if (!withering && !Active.Contains(this)) Active.Add(this);
    }

    void OnDisable() => Active.Remove(this);

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age >= lifetime) { Destroy(gameObject); return; }
        if (!withering && age >= lifetime - WitherTime) Wither();

        bool inside = !withering && target != null && target.IsAlive && IsInside(target.transform.position);
        if (inside && age >= GrowTime * 0.5f)
        {
            tickTimer += dt;
            while (tickTimer >= TickInterval)
            {
                tickTimer -= TickInterval;
                if (target.IsFullHealth) continue;
                target.Heal(target.Max * healFractionPerSecond * TickInterval);
                puff = 1f;
            }
        }
        else tickTimer = 0f;

        areaGlow = Mathf.MoveTowards(areaGlow, inside ? 1f : 0f, dt * 4f);
        puff = Mathf.MoveTowards(puff, 0f, dt * 5f);
        Animate();
    }

    void Animate()
    {
        float grow = Mathf.Clamp01(age / GrowTime);
        float k = withering ? Mathf.Clamp01((lifetime - age) / WitherTime) : EaseOutBack(grow);
        if (body != null)
        {
            // Balança de leve e "estufa" a cada cura
            float wobble = Mathf.Sin(age * 5f) * 0.04f + puff * 0.12f;
            float s = MushroomSize * sizeJitter * k;
            body.localScale = new Vector3(s * (1f + wobble * 0.5f), s * (1f - wobble * 0.3f + puff * 0.1f), s * (1f + wobble * 0.5f));
        }
        if (area != null)
        {
            float r = radius * (withering ? k : Mathf.SmoothStep(0f, 1f, grow));
            area.localScale = new Vector3(r, 1f, r);
            if (areaRenderer != null)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(age * 4f);
                float alpha = (withering ? k : 1f) * pulse * (0.55f + 0.45f * areaGlow);
                SetAreaColor(0, alpha * 0.35f);
                SetAreaColor(1, alpha * 0.9f);
            }
        }
    }

    void SetAreaColor(int index, float alpha)
    {
        var c = AreaColor;
        c.a = alpha;
        areaRenderer.GetPropertyBlock(areaBlock, index);
        areaBlock.SetColor(BaseColorId, c);
        areaRenderer.SetPropertyBlock(areaBlock, index);
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.7f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ---------- Visual ----------

    void BuildVisual(Material bodyMaterial, Material areaMaterial)
    {
        if (bodyMaterial != null)
        {
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            body.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-8f, 8f));
            body.gameObject.AddComponent<MeshFilter>().sharedMesh = bodyMesh != null ? bodyMesh : bodyMesh = BuildBodyMesh();
            var r = body.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { bodyMaterial, bodyMaterial, bodyMaterial };
            var block = new MaterialPropertyBlock();
            Color[] colors = { StemColor, CapColor, SpotColor };
            for (int i = 0; i < colors.Length; i++)
            {
                r.GetPropertyBlock(block, i);
                block.SetColor(BaseColorId, colors[i]);
                r.SetPropertyBlock(block, i);
            }
        }
        if (areaMaterial != null)
        {
            area = new GameObject("Area").transform;
            area.SetParent(transform, false);
            area.localPosition = Vector3.up * 0.04f;
            area.gameObject.AddComponent<MeshFilter>().sharedMesh = areaMesh != null ? areaMesh : areaMesh = BuildAreaMesh();
            areaRenderer = area.gameObject.AddComponent<MeshRenderer>();
            areaRenderer.sharedMaterials = new[] { areaMaterial, areaMaterial };
            areaRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            areaRenderer.receiveShadows = false;
            areaBlock = new MaterialPropertyBlock();
        }
    }

    /// <summary>Cogumelo low-poly: talo (submalha 0), chapéu (1) e bolinhas brancas (2). Pivô na base.</summary>
    static Mesh BuildBodyMesh()
    {
        var mb = new FlatMeshBuilder(3);
        // Perfis (raio, altura) de baixo para cima, girados em volta do eixo Y
        mb.Lathe(0, new Vector2[] { new(0f, 0f), new(0.17f, 0f), new(0.14f, 0.22f), new(0.12f, 0.5f), new(0f, 0.5f) }, 7, Matrix4x4.identity);
        Vector2[] cap = { new(0f, 0.44f), new(0.5f, 0.4f), new(0.58f, 0.5f), new(0.52f, 0.66f), new(0.32f, 0.8f), new(0f, 0.86f) };
        mb.Lathe(1, cap, 9, Matrix4x4.identity);
        // Bolinhas em cima do chapéu, encostadas na superfície
        Vector2[] spot = { new(0f, -0.02f), new(0.075f, -0.02f), new(0.06f, 0.03f), new(0f, 0.045f) };
        (int seg, float t, float angle)[] spots = { (3, 0.5f, 20f), (3, 0.4f, 140f), (3, 0.55f, 255f), (2, 0.6f, 80f), (2, 0.55f, 200f), (2, 0.5f, 320f), (4, 0.2f, 0f) };
        foreach (var (seg, t, angle) in spots)
        {
            Vector2 a = cap[seg], b = cap[seg + 1];
            Vector2 p = Vector2.Lerp(a, b, t);
            Vector2 n = new Vector2(b.y - a.y, -(b.x - a.x)).normalized; // normal para fora do perfil
            float rad = angle * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            Vector3 pos = dir * p.x + Vector3.up * p.y;
            Vector3 normal = (dir * n.x + Vector3.up * n.y).normalized;
            var trs = Matrix4x4.TRS(pos, Quaternion.FromToRotation(Vector3.up, normal), Vector3.one);
            mb.Lathe(2, spot, 6, trs);
        }
        return mb.ToMesh("HealingMushroomBody");
    }

    /// <summary>Círculo de raio 1 no chão: miolo translúcido (submalha 0) e borda mais forte (1).</summary>
    static Mesh BuildAreaMesh()
    {
        var mb = new FlatMeshBuilder(2);
        mb.Lathe(0, new Vector2[] { new(0.93f, 0f), new(0f, 0f) }, 40, Matrix4x4.identity);
        mb.Lathe(1, new Vector2[] { new(1f, 0f), new(0.93f, 0f) }, 40, Matrix4x4.identity);
        return mb.ToMesh("HealingMushroomArea");
    }
}
