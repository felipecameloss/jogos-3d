using UnityEngine;

/// <summary>
/// Polimento visual do quintal:
/// 1. Gera uma textura procedural sem emendas para o chão (evita o aspecto liso).
/// 2. Solta partículas leves de pólen ao sol (dia) e vaga-lumes brilhantes (noite) ao redor do player.
/// </summary>
public class BackyardAtmosphere : MonoBehaviour
{
    [Header("Chão")]
    [SerializeField] Renderer groundRenderer;
    [SerializeField] float groundTiling = 45f;

    [Header("Partículas do ambiente")]
    [SerializeField] Material moteMaterial;
    [SerializeField, Min(0f)] float pollenRate = 18f;
    [SerializeField, Min(0f)] float fireflyRate = 16f;

    static Mesh sphereMesh;
    Transform player;
    DayNightCycle dayNight;
    ParticleSystem pollenSystem;
    ParticleSystem fireflySystem;
    ParticleSystem.EmissionModule pollenEmission;
    ParticleSystem.EmissionModule fireflyEmission;
    Texture2D groundTexture;
    Material groundMatInstance;

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        dayNight = FindAnyObjectByType<DayNightCycle>();

        ApplyGroundDetail();
        CreateParticles();
    }

    void Update()
    {
        if (player != null)
        {
            Vector3 anchor = player.position + Vector3.up * 2.5f;
            if (pollenSystem != null) pollenSystem.transform.position = anchor;
            if (fireflySystem != null) fireflySystem.transform.position = anchor;
        }

        float daylight = dayNight != null ? dayNight.Daylight : 1f;
        if (pollenSystem != null) pollenEmission.rateOverTime = pollenRate * Mathf.Clamp01(daylight * 1.15f);
        if (fireflySystem != null)
        {
            float night = Mathf.Clamp01((0.55f - daylight) / 0.45f);
            fireflyEmission.rateOverTime = fireflyRate * night;
        }
    }

    void OnDestroy()
    {
        if (groundTexture != null) Destroy(groundTexture);
        if (groundMatInstance != null) Destroy(groundMatInstance);
    }

    void ApplyGroundDetail()
    {
        if (groundRenderer == null)
        {
            var groundObj = GameObject.Find("Ground");
            if (groundObj != null) groundRenderer = groundObj.GetComponent<Renderer>();
        }
        if (groundRenderer == null) return;

        const int size = 256;
        groundTexture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 4,
            name = "ProceduralTurfDetail",
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float macro = PeriodicNoise(u, v, 6, 17);
                float medium = PeriodicNoise(u, v, 18, 53);
                float micro = Hash(x, y, 97);

                // Variação sutil em torno do branco para multiplicar _BaseColor sem escurecer demais
                float shade = Mathf.Lerp(0.78f, 1.04f, macro * 0.52f + medium * 0.34f + micro * 0.14f);
                float warmShift = (medium - 0.5f) * 0.05f;
                byte r = (byte)Mathf.Clamp(Mathf.RoundToInt((shade + warmShift) * 255f), 0, 255);
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt((shade + 0.02f) * 255f), 0, 255);
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt((shade - warmShift) * 255f), 0, 255);
                pixels[y * size + x] = new Color32(r, g, b, 255);
            }
        }
        groundTexture.SetPixels32(pixels);
        groundTexture.Apply(updateMipmaps: true, makeNoLongerReadable: true);

        groundMatInstance = groundRenderer.material;
        groundMatInstance.mainTexture = groundTexture;
        groundMatInstance.mainTextureScale = new Vector2(groundTiling, groundTiling);
    }

    void CreateParticles()
    {
        pollenSystem = BuildMoteSystem(
            "BackyardPollen",
            new Color(1f, 0.94f, 0.72f, 0.7f),
            new Color(0.95f, 0.9f, 0.6f, 0.45f),
            sizeRange: new Vector2(0.035f, 0.075f),
            lifetimeRange: new Vector2(4f, 7f),
            speedRange: new Vector2(0.15f, 0.45f),
            boxSize: new Vector3(22f, 6f, 22f),
            maxParticles: 90,
            pulseCurve: new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.2f, 1f),
                new Keyframe(0.8f, 1f),
                new Keyframe(1f, 0f)));
        pollenEmission = pollenSystem.emission;

        fireflySystem = BuildMoteSystem(
            "BackyardFireflies",
            new Color(0.75f, 1f, 0.35f, 1f),
            new Color(1f, 0.85f, 0.25f, 1f),
            sizeRange: new Vector2(0.07f, 0.13f),
            lifetimeRange: new Vector2(3.5f, 6f),
            speedRange: new Vector2(0.35f, 0.85f),
            boxSize: new Vector3(26f, 5.5f, 26f),
            maxParticles: 60,
            pulseCurve: new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.18f, 1f),
                new Keyframe(0.42f, 0.2f),
                new Keyframe(0.68f, 1f),
                new Keyframe(1f, 0f)));
        fireflyEmission = fireflySystem.emission;
        fireflyEmission.rateOverTime = 0f;
    }

    ParticleSystem BuildMoteSystem(
        string objName,
        Color colorA,
        Color colorB,
        Vector2 sizeRange,
        Vector2 lifetimeRange,
        Vector2 speedRange,
        Vector3 boxSize,
        int maxParticles,
        AnimationCurve pulseCurve)
    {
        var go = new GameObject(objName);
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.playOnAwake = true;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeRange.x, lifetimeRange.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedRange.x, speedRange.y);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxParticles;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = boxSize;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.55f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.2f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, pulseCurve);

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = ResolveSphereMesh();
        if (moteMaterial != null) r.sharedMaterial = moteMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
        return ps;
    }

    static Mesh ResolveSphereMesh()
    {
        if (sphereMesh == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
        }
        return sphereMesh;
    }

    static float PeriodicNoise(float u, float v, int period, int seed)
    {
        float x = u * period;
        float y = v * period;
        int x0 = Mathf.FloorToInt(x) % period;
        int y0 = Mathf.FloorToInt(y) % period;
        int x1 = (x0 + 1) % period;
        int y1 = (y0 + 1) % period;
        float fx = Smooth(x - Mathf.Floor(x));
        float fy = Smooth(y - Mathf.Floor(y));

        float a = Hash(x0, y0, seed);
        float b = Hash(x1, y0, seed);
        float c = Hash(x0, y1, seed);
        float d = Hash(x1, y1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Smooth(float t) => t * t * (3f - 2f * t);

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }
}
