using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nuvem de pólen (item Pólen): fica alguns segundos onde o acerto aconteceu e deixa confuso
/// quem estiver dentro (anda sem rumo e não ataca; chefes são imunes). O visual são bolas
/// amarelas translúcidas que crescem, flutuam devagar e somem; não precisa de prefab.
/// </summary>
public class PollenCloud : MonoBehaviour
{
    /// <summary>Nuvens que ainda confundem (as que estão sumindo saem da lista).</summary>
    public static readonly List<PollenCloud> Active = new();

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly Color CloudColor = new(1f, 0.88f, 0.32f, 1f);

    const float GrowTime = 0.25f;
    const float FadeTime = 0.5f;
    const float TickInterval = 0.2f;
    const int PuffCount = 7;

    float radius, lifetime, confuseDuration, age, tickTimer;
    bool fading;

    struct Puff { public Transform transform; public Renderer renderer; public Vector3 offset; public float size, phase; }
    readonly List<Puff> puffs = new();
    MaterialPropertyBlock block;

    public float Radius => radius;
    public bool IsFading => fading;

    // O projeto entra no Play Mode sem recarregar o domínio: a lista estática não zera sozinha
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Active.Clear();

    /// <summary>
    /// Solta uma nuvem em <paramref name="center"/>. Inimigos a até <paramref name="radius"/> metros
    /// ficam confusos por <paramref name="confuseDuration"/> segundos (renovado enquanto estiverem dentro).
    /// Sem material, só não aparece (a confusão funciona igual).
    /// </summary>
    public static PollenCloud Spawn(Vector3 center, float radius, float lifetime, float confuseDuration, Material material = null)
    {
        var go = new GameObject("PollenCloud");
        go.transform.position = center;
        var cloud = go.AddComponent<PollenCloud>();
        cloud.radius = Mathf.Max(0.5f, radius);
        cloud.lifetime = Mathf.Max(GrowTime + FadeTime, lifetime);
        cloud.confuseDuration = Mathf.Max(0f, confuseDuration);
        cloud.BuildVisual(material);
        cloud.ConfuseInside(); // já pega quem está dentro no mesmo frame do acerto
        cloud.Animate();
        return cloud;
    }

    /// <summary>Começa a sumir agora (sai de <see cref="Active"/> e para de confundir).</summary>
    public void FadeOut()
    {
        if (fading) return;
        fading = true;
        Active.Remove(this);
        age = Mathf.Max(age, lifetime - FadeTime);
    }

    /// <summary>O ponto está dentro da nuvem.</summary>
    public bool IsInside(Vector3 point) => (point - transform.position).sqrMagnitude <= radius * radius;

    void OnEnable()
    {
        if (!fading && !Active.Contains(this)) Active.Add(this);
    }

    void OnDisable() => Active.Remove(this);

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime) { Destroy(gameObject); return; }
        if (!fading && age >= lifetime - FadeTime) FadeOut();

        if (!fading)
        {
            tickTimer += Time.deltaTime;
            if (tickTimer >= TickInterval)
            {
                tickTimer -= TickInterval;
                ConfuseInside();
            }
        }
        Animate();
    }

    void ConfuseInside()
    {
        for (int i = EnemyBase.Active.Count - 1; i >= 0; i--)
        {
            var enemy = EnemyBase.Active[i];
            if (enemy == null || !enemy.IsAlive || enemy.IsBoss || !IsInside(enemy.Center)) continue;
            StatusEffects.On(enemy).ApplyConfusion(confuseDuration);
        }
    }

    // ---------- Visual ----------

    void BuildVisual(Material material)
    {
        if (material == null) return;
        block = new MaterialPropertyBlock();
        for (int i = 0; i < PuffCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Puff";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Vector2 flat = Random.insideUnitCircle * 0.55f;
            puffs.Add(new Puff
            {
                transform = go.transform,
                renderer = r,
                offset = new Vector3(flat.x, Random.Range(-0.2f, 0.25f), flat.y),
                size = Random.Range(0.75f, 1.1f),
                phase = Random.Range(0f, Mathf.PI * 2f),
            });
        }
    }

    void Animate()
    {
        if (puffs.Count == 0) return;
        float grow = 1f - (1f - Mathf.Clamp01(age / GrowTime)) * (1f - Mathf.Clamp01(age / GrowTime));
        float fade = fading ? Mathf.Clamp01((lifetime - age) / FadeTime) : 1f;
        float spin = age * 20f;
        foreach (var p in puffs)
        {
            if (p.transform == null) continue;
            float bob = Mathf.Sin(age * 2.2f + p.phase);
            Vector3 offset = Quaternion.Euler(0f, spin, 0f) * p.offset;
            p.transform.localPosition = (offset + Vector3.up * (bob * 0.06f + age * 0.08f)) * radius;
            float d = radius * p.size * grow * (1f + 0.05f * bob) * (fading ? 1f + 0.2f * (1f - fade) : 1f);
            p.transform.localScale = new Vector3(d, d * 0.75f, d);
            var c = CloudColor;
            c.a = 0.22f * fade * (0.85f + 0.15f * Mathf.Sin(age * 3f + p.phase));
            p.renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            p.renderer.SetPropertyBlock(block);
        }
    }
}
