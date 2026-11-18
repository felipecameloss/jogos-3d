using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Polimento visual e atmosférico do quintal:
/// 1. Gera textura procedural de grama/terra e Normal Map para o chão (micro-relevo ao sol).
/// 2. Enquadra o horizonte além da cerca com árvores, arbustos, gramado externo e casa distante.
/// 3. Espalha flora viva (trevos, margaridas, cogumelos), clutter ao redor de pedras/props,
///    tampinhas, pá de jardinagem gigante e poças d'água com reflexo e ondinhas.
/// 4. Anima nuvens no céu, estrelas noturnas, luzes-guia nos marcos, pólen/vaga-lumes e DoF tilt-shift sutil.
/// </summary>
public class BackyardAtmosphere : MonoBehaviour
{
    [Header("Chão")]
    [SerializeField] Renderer groundRenderer;
    [SerializeField] float groundTiling = 42f;
    [SerializeField, Range(0f, 1.5f)] float groundNormalStrength = 0.55f;

    [Header("Partículas do ambiente")]
    [SerializeField] Material moteMaterial;
    [SerializeField, Min(0f)] float pollenRate = 20f;
    [SerializeField, Min(0f)] float fireflyRate = 18f;

    [Header("Modelos do cenário (.glb)")]
    [SerializeField] GameObject treePrefab;
    [SerializeField] GameObject bushPrefab;
    [SerializeField] GameObject cloverPrefab;
    [SerializeField] GameObject daisyPrefab;
    [SerializeField] GameObject mushroomPrefab;
    [SerializeField] GameObject trowelPrefab;
    [SerializeField] GameObject bottleCapPrefab;
    [SerializeField] GameObject grassTuftPrefab;
    [SerializeField] GameObject pebblePrefab;
    [Tooltip("Folha seca caída (Tools/Blender/build_props.py), espalhada pelo gramado sem colisão.")]
    [SerializeField] GameObject fallenLeafPrefab;
    [Tooltip("Pedras (Tools/Blender/build_props.py) espalhadas como decoração, sem colisão.")]
    [SerializeField] GameObject[] rockDecorPrefabs;

    [Header("Opções visuais")]
    [SerializeField] bool enableTiltShiftDof = true;
    [SerializeField] bool enableHorizonFraming = true;
    [SerializeField] bool enableRainPuddles = true;

    struct CloudEntry
    {
        public Transform root;
        public Vector3 basePos;
        public float speed;
        public float phase;
    }

    struct SwayFlora
    {
        public Transform tr;
        public Quaternion baseRot;
        public float phase;
        public float amount;
    }

    struct BeaconLight
    {
        public Light light;
        public float dayIntensity;
        public float nightIntensity;
    }

    static Mesh sphereMesh;
    static Mesh quadDiscMesh;
    static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
    static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
    static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    Transform player;
    DayNightCycle dayNight;
    ParticleSystem pollenSystem;
    ParticleSystem fireflySystem;
    ParticleSystem starSystem;
    ParticleSystem.EmissionModule pollenEmission;
    ParticleSystem.EmissionModule fireflyEmission;
    ParticleSystem.EmissionModule starEmission;

    Texture2D groundTexture;
    Texture2D groundNormalMap;
    Material groundMatInstance;
    Material cloudMaterial;
    Material puddleMaterial;
    Material rippleMaterial;
    Material horizonLawnMaterial;
    Material distantHouseMaterial;

    readonly List<CloudEntry> clouds = new();
    readonly List<SwayFlora> swaying = new();
    readonly List<Transform> ripples = new();
    readonly List<BeaconLight> beacons = new();
    Transform environmentRoot;

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        dayNight = FindAnyObjectByType<DayNightCycle>();

        environmentRoot = new GameObject("[BackyardVista]").transform;

        ApplyGroundDetail();
        BuildHorizonAndHouse();
        ScatterFloraAndClutter();
        SpawnRainPuddles();
        SpawnClouds();
        CreateParticles();
        StartCoroutine(AttachGuideBeaconsAfterSpawn());
        ApplySubtleTiltShift();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float t = Time.time;

        if (player != null)
        {
            Vector3 anchor = player.position + Vector3.up * 2.5f;
            if (pollenSystem != null) pollenSystem.transform.position = anchor;
            if (fireflySystem != null) fireflySystem.transform.position = anchor;
            if (starSystem != null) starSystem.transform.position = player.position + Vector3.up * 72f;
        }

        float daylight = dayNight != null ? dayNight.Daylight : 1f;
        if (pollenSystem != null) pollenEmission.rateOverTime = pollenRate * Mathf.Clamp01(daylight * 1.15f);
        if (fireflySystem != null)
        {
            float night = Mathf.Clamp01((0.55f - daylight) / 0.45f);
            fireflyEmission.rateOverTime = fireflyRate * night;
        }
        if (starSystem != null)
        {
            float nightStars = Mathf.Clamp01((0.40f - daylight) / 0.35f);
            starEmission.rateOverTime = 24f * nightStars;
        }

        // Nuvens deslizando suavemente e mudando de tom dia -> pôr do sol -> noite
        if (cloudMaterial != null)
        {
            float sunset = Mathf.Clamp01(1f - Mathf.Abs(daylight - 0.45f) * 2.5f) * Mathf.Clamp01(daylight * 2.5f);
            Color cDay = new(0.96f, 0.97f, 0.99f);
            Color cDusk = new(0.98f, 0.72f, 0.56f);
            Color cNight = new(0.18f, 0.22f, 0.34f);
            Color c = Color.Lerp(Color.Lerp(cNight, cDay, daylight), cDusk, sunset * 0.7f);
            cloudMaterial.SetColor(BaseColorId, c);
        }

        for (int i = 0; i < clouds.Count; i++)
        {
            var c = clouds[i];
            if (c.root == null) continue;
            float x = Mathf.Repeat(c.basePos.x + t * c.speed + 160f, 320f) - 160f;
            float bob = Mathf.Sin(t * 0.25f + c.phase) * 0.8f;
            c.root.position = new Vector3(x, c.basePos.y + bob, c.basePos.z);
        }

        // Vento leve nas margaridas e trevos
        for (int i = 0; i < swaying.Count; i++)
        {
            var s = swaying[i];
            if (s.tr == null) continue;
            float angle = Mathf.Sin(t * 1.6f + s.phase) * s.amount;
            s.tr.localRotation = s.baseRot * Quaternion.Euler(angle, 0f, angle * 0.6f);
        }

        // Ondinhas sutis nas poças d'água
        for (int i = 0; i < ripples.Count; i++)
        {
            var r = ripples[i];
            if (r == null) continue;
            float cycle = Mathf.Repeat(t * 0.45f + i * 0.37f, 1f);
            float scale = Mathf.Lerp(0.4f, 3.6f, cycle);
            r.localScale = new Vector3(scale, 1f, scale);
        }

        // Luzes-guia nos marcos intensificam ao escurecer
        float nightFactor = 1f - daylight;
        for (int i = 0; i < beacons.Count; i++)
        {
            var b = beacons[i];
            if (b.light != null)
                b.light.intensity = Mathf.Lerp(b.dayIntensity, b.nightIntensity, nightFactor);
        }
    }

    void OnDestroy()
    {
        if (groundTexture != null) Destroy(groundTexture);
        if (groundNormalMap != null) Destroy(groundNormalMap);
        if (groundMatInstance != null) Destroy(groundMatInstance);
        if (cloudMaterial != null) Destroy(cloudMaterial);
        if (puddleMaterial != null) Destroy(puddleMaterial);
        if (rippleMaterial != null) Destroy(rippleMaterial);
        if (horizonLawnMaterial != null) Destroy(horizonLawnMaterial);
        if (distantHouseMaterial != null) Destroy(distantHouseMaterial);
        if (environmentRoot != null) Destroy(environmentRoot.gameObject);
    }

    void ApplyGroundDetail()
    {
        if (groundRenderer == null)
        {
            var groundObj = GameObject.Find("Ground");
            if (groundObj != null) groundRenderer = groundObj.GetComponent<Renderer>();
        }
        if (groundRenderer == null) return;

        const int size = 512;
        groundTexture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 4,
            name = "ProceduralTurfAlbedo",
        };
        groundNormalMap = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true, linear: true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 4,
            name = "ProceduralTurfNormal",
        };

        var heights = new float[size * size];
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            float v = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float macro = PeriodicNoise(u, v, 5, 17);
                float medium = PeriodicNoise(u, v, 16, 53);
                float cloverPatch = PeriodicNoise(u, v, 10, 89);
                float fine = PeriodicNoise(u, v, 44, 131);
                float micro = Hash(x, y, 97);

                float h = macro * 0.40f + medium * 0.30f + fine * 0.22f + micro * 0.08f;
                heights[y * size + x] = h;

                float shade = Mathf.Lerp(0.76f, 1.06f, h);
                float cloverTint = Mathf.SmoothStep(0.55f, 0.85f, cloverPatch);
                float dirtHint = Mathf.SmoothStep(0.32f, 0.08f, macro) * 0.14f;

                float rCol = shade + (medium - 0.5f) * 0.04f + dirtHint * 0.45f - cloverTint * 0.04f;
                float gCol = shade + 0.03f + cloverTint * 0.05f - dirtHint * 0.10f;
                float bCol = shade - 0.02f - (medium - 0.5f) * 0.03f - dirtHint * 0.25f;

                pixels[y * size + x] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(rCol * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(gCol * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(bCol * 255f), 0, 255),
                    255);
            }
        }

        // Deriva normal map em tangent space a partir do campo de altura
        var normPixels = new Color32[size * size];
        const float strength = 3.2f;
        for (int y = 0; y < size; y++)
        {
            int ym = (y - 1 + size) % size;
            int yp = (y + 1) % size;
            for (int x = 0; x < size; x++)
            {
                int xm = (x - 1 + size) % size;
                int xp = (x + 1) % size;
                float dx = (heights[y * size + xp] - heights[y * size + xm]) * strength;
                float dy = (heights[yp * size + x] - heights[ym * size + x]) * strength;
                Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                normPixels[y * size + x] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 0, 255),
                    255);
            }
        }

        groundTexture.SetPixels32(pixels);
        groundTexture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
        groundNormalMap.SetPixels32(normPixels);
        groundNormalMap.Apply(updateMipmaps: true, makeNoLongerReadable: true);

        groundMatInstance = groundRenderer.material;
        groundMatInstance.mainTexture = groundTexture;
        groundMatInstance.mainTextureScale = new Vector2(groundTiling, groundTiling);
        if (groundMatInstance.HasProperty(BumpMapId))
        {
            groundMatInstance.SetTexture(BumpMapId, groundNormalMap);
            groundMatInstance.SetTextureScale(BumpMapId, new Vector2(groundTiling, groundTiling));
            groundMatInstance.SetFloat(BumpScaleId, groundNormalStrength);
            groundMatInstance.EnableKeyword("_NORMALMAP");
        }
        if (groundMatInstance.HasProperty(SmoothnessId))
            groundMatInstance.SetFloat(SmoothnessId, 0.08f);
    }

    void BuildHorizonAndHouse()
    {
        if (!enableHorizonFraming) return;

        // Anel externo de gramado estendido (sem colisão) para fechar a linha do horizonte além da cerca
        Shader litShader = groundRenderer != null ? groundRenderer.sharedMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
        horizonLawnMaterial = new Material(litShader)
        {
            color = new Color(0.24f, 0.42f, 0.17f, 1f),
        };
        if (horizonLawnMaterial.HasProperty(SmoothnessId)) horizonLawnMaterial.SetFloat(SmoothnessId, 0.06f);

        var outerLawn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        outerLawn.name = "OuterBackyardSkirt";
        Destroy(outerLawn.GetComponent<Collider>());
        outerLawn.transform.SetParent(environmentRoot, false);
        outerLawn.transform.position = new Vector3(0f, -0.62f, 0f);
        outerLawn.transform.localScale = new Vector3(360f, 0.1f, 360f);
        var lawnR = outerLawn.GetComponent<MeshRenderer>();
        lawnR.sharedMaterial = horizonLawnMaterial;
        lawnR.shadowCastingMode = ShadowCastingMode.Off;

        var rng = new System.Random(2026);
        if (treePrefab != null)
        {
            for (int i = 0; i < 28; i++)
            {
                float ang = (i / 28f) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.18f;
                float dist = Mathf.Lerp(82f, 122f, (float)rng.NextDouble());
                float sc = Mathf.Lerp(2.0f, 3.4f, (float)rng.NextDouble());
                Vector3 pos = new Vector3(Mathf.Cos(ang) * dist, -0.5f, Mathf.Sin(ang) * dist);
                SpawnDecor(treePrefab, pos, (float)rng.NextDouble() * 360f, Vector3.one * sc, castShadows: true);
            }
        }

        if (bushPrefab != null)
        {
            for (int i = 0; i < 48; i++)
            {
                float ang = (i / 48f) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.12f;
                float dist = Mathf.Lerp(73f, 88f, (float)rng.NextDouble());
                float sc = Mathf.Lerp(1.5f, 2.8f, (float)rng.NextDouble());
                Vector3 pos = new Vector3(Mathf.Cos(ang) * dist, -0.25f, Mathf.Sin(ang) * dist);
                SpawnDecor(bushPrefab, pos, (float)rng.NextDouble() * 360f, Vector3.one * sc, castShadows: true);
            }
        }

        // Silhueta distante de varanda/casa ao fundo norte para dar escala de quintal
        distantHouseMaterial = new Material(litShader) { color = new Color(0.55f, 0.50f, 0.44f, 1f) };
        if (distantHouseMaterial.HasProperty(SmoothnessId)) distantHouseMaterial.SetFloat(SmoothnessId, 0.15f);
        var house = GameObject.CreatePrimitive(PrimitiveType.Cube);
        house.name = "DistantHouseSilhouette";
        Destroy(house.GetComponent<Collider>());
        house.transform.SetParent(environmentRoot, false);
        house.transform.position = new Vector3(12f, 14f, 134f);
        house.transform.localScale = new Vector3(95f, 30f, 24f);
        var hr = house.GetComponent<MeshRenderer>();
        hr.sharedMaterial = distantHouseMaterial;
        hr.shadowCastingMode = ShadowCastingMode.Off;

        var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "DistantHouseRoof";
        Destroy(roof.GetComponent<Collider>());
        roof.transform.SetParent(house.transform, false);
        roof.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        roof.transform.localScale = new Vector3(1.06f, 0.25f, 1.1f);
        var roofMr = roof.GetComponent<MeshRenderer>();
        roofMr.sharedMaterial = distantHouseMaterial;
        roofMr.shadowCastingMode = ShadowCastingMode.Off;
    }

    void ScatterFloraAndClutter()
    {
        var rng = new System.Random(1337);
        var pipe = ExitPipe.Instance;
        Vector3 pipePos = pipe != null ? pipe.transform.position : new Vector3(0f, 0f, 65f);

        bool ValidLawnSpot(Vector3 p, float minPipeDist = 7f)
        {
            if (Mathf.Abs(p.x) > 62f || Mathf.Abs(p.z) > 62f) return false;
            return (p - pipePos).sqrMagnitude >= minPipeDist * minPipeDist;
        }

        // 1. Flores, trevos, cogumelos e tampinhas pelo gramado
        if (cloverPrefab != null)
        {
            for (int i = 0; i < 130; i++)
            {
                Vector3 p = new(Mathf.Lerp(-58f, 58f, (float)rng.NextDouble()), 0f, Mathf.Lerp(-58f, 58f, (float)rng.NextDouble()));
                if (!ValidLawnSpot(p)) continue;
                float sc = Mathf.Lerp(0.85f, 1.45f, (float)rng.NextDouble());
                var go = SpawnDecor(cloverPrefab, p, (float)rng.NextDouble() * 360f, Vector3.one * sc, castShadows: false);
                swaying.Add(new SwayFlora { tr = go.transform, baseRot = go.transform.localRotation, phase = (float)rng.NextDouble() * 6.28f, amount = 2.6f });
            }
        }

        if (daisyPrefab != null)
        {
            for (int i = 0; i < 95; i++)
            {
                Vector3 p = new(Mathf.Lerp(-58f, 58f, (float)rng.NextDouble()), 0f, Mathf.Lerp(-58f, 58f, (float)rng.NextDouble()));
                if (!ValidLawnSpot(p)) continue;
                float sc = Mathf.Lerp(0.8f, 1.35f, (float)rng.NextDouble());
                var go = SpawnDecor(daisyPrefab, p, (float)rng.NextDouble() * 360f, Vector3.one * sc, castShadows: false);
                swaying.Add(new SwayFlora { tr = go.transform, baseRot = go.transform.localRotation, phase = (float)rng.NextDouble() * 6.28f, amount = 4.2f });
            }
        }

        if (mushroomPrefab != null)
        {
            for (int i = 0; i < 34; i++)
            {
                Vector3 p = new(Mathf.Lerp(-58f, 58f, (float)rng.NextDouble()), 0f, Mathf.Lerp(-58f, 58f, (float)rng.NextDouble()));
                if (!ValidLawnSpot(p, 8f)) continue;
                float sc = Mathf.Lerp(0.8f, 1.65f, (float)rng.NextDouble());
                SpawnDecor(mushroomPrefab, p, (float)rng.NextDouble() * 360f, Vector3.one * sc, castShadows: true);
            }
        }

        if (bottleCapPrefab != null)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector3 p = new(Mathf.Lerp(-52f, 52f, (float)rng.NextDouble()), 0f, Mathf.Lerp(-52f, 52f, (float)rng.NextDouble()));
                if (!ValidLawnSpot(p, 10f)) continue;
                float sc = Mathf.Lerp(0.75f, 1.15f, (float)rng.NextDouble());
                SpawnDecor(bottleCapPrefab, p, (float)rng.NextDouble() * 360f, Vector3.one * sc, castShadows: true);
            }
        }

        // 2. Pá de jardinagem gigante como marco visual perto da borda oeste
        if (trowelPrefab != null)
            SpawnDecor(trowelPrefab, new Vector3(-52f, -0.35f, 26f), 38f, Vector3.one * 1.15f, castShadows: true);

        // 2b. Folhas secas e pedras dos props do Blender (semente própria: não mexe no resto)
        ScatterBlenderProps(ValidLawnSpot);

        // 3. Contact clutter ao redor de pedras, vaso, latas e cano de saída
        var backyard = GameObject.Find("Backyard");
        if (backyard != null && grassTuftPrefab != null)
        {
            foreach (Transform tr in backyard.transform)
            {
                string n = tr.name;
                bool anchor = n.StartsWith("BigRock") || n.StartsWith("Rock") || n == "FlowerPotFlipped" || n.StartsWith("Can");
                if (!anchor) continue;

                float ringRadius = n.StartsWith("BigRock") || n == "FlowerPotFlipped" ? 5.6f : 3.4f;
                int count = n.StartsWith("BigRock") ? 10 : 6;
                for (int k = 0; k < count; k++)
                {
                    float a = (k / (float)count) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.4f;
                    float rad = ringRadius + (float)rng.NextDouble() * 1.4f;
                    Vector3 p = new Vector3(tr.position.x + Mathf.Cos(a) * rad, 0.35f, tr.position.z + Mathf.Sin(a) * rad);
                    float sc = Mathf.Lerp(0.7f, 1.25f, (float)rng.NextDouble());
                    SpawnDecor(grassTuftPrefab, p, (float)rng.NextDouble() * 360f, new Vector3(sc, sc * 1.1f, sc), castShadows: false);
                    if (pebblePrefab != null && k % 2 == 0)
                        SpawnDecor(pebblePrefab, new Vector3(p.x + 0.3f, 0.1f, p.z - 0.25f), (float)rng.NextDouble() * 360f, Vector3.one * Mathf.Lerp(0.35f, 0.65f, (float)rng.NextDouble()), castShadows: false);
                }
                if (mushroomPrefab != null)
                {
                    Vector3 mp = new Vector3(tr.position.x + ringRadius * 0.85f, 0f, tr.position.z - ringRadius * 0.65f);
                    SpawnDecor(mushroomPrefab, mp, (float)rng.NextDouble() * 360f, Vector3.one * 1.25f, castShadows: true);
                }
            }
        }
    }

    /// <summary>
    /// Folhas caídas e pedras soltas pelo gramado. Sem colisão; não encostam nas peças de jogo
    /// (rochas, latas, rampas...) para não parecer que atravessam.
    /// </summary>
    void ScatterBlenderProps(System.Func<Vector3, float, bool> validSpot)
    {
        var rng = new System.Random(4242);
        bool Clear(Vector3 p, float radius) =>
            // caixa acima do chão: pega colisores de peças, não o próprio chão
            !Physics.CheckBox(p + Vector3.up * 2.5f, new Vector3(radius, 2f, radius), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

        if (fallenLeafPrefab != null)
        {
            for (int i = 0; i < 46; i++)
            {
                Vector3 p = new(Mathf.Lerp(-56f, 56f, (float)rng.NextDouble()), 0.02f, Mathf.Lerp(-56f, 56f, (float)rng.NextDouble()));
                float sc = Mathf.Lerp(1.1f, 2.1f, (float)rng.NextDouble());
                if (!validSpot(p, 8f) || !Clear(p, 2.2f * sc)) continue;
                var go = SpawnDecor(fallenLeafPrefab, p, (float)rng.NextDouble() * 360f, Vector3.one * sc, castShadows: true);
                // levemente inclinada, como se tivesse caído
                go.transform.rotation *= Quaternion.Euler(Mathf.Lerp(-4f, 4f, (float)rng.NextDouble()), 0f, Mathf.Lerp(-4f, 4f, (float)rng.NextDouble()));
            }
        }

        if (rockDecorPrefabs != null && rockDecorPrefabs.Length > 0)
        {
            for (int i = 0; i < 30; i++)
            {
                var prefab = rockDecorPrefabs[rng.Next(rockDecorPrefabs.Length)];
                Vector3 p = new(Mathf.Lerp(-56f, 56f, (float)rng.NextDouble()), -0.05f, Mathf.Lerp(-56f, 56f, (float)rng.NextDouble()));
                float sc = Mathf.Lerp(0.45f, 0.95f, (float)rng.NextDouble());
                if (prefab == null || !validSpot(p, 8f) || !Clear(p, 1.6f * sc)) continue;
                SpawnDecor(prefab, p, (float)rng.NextDouble() * 360f, Vector3.one * sc, castShadows: true);
            }
        }
    }

    void SpawnRainPuddles()
    {
        if (!enableRainPuddles) return;

        Shader litShader = groundRenderer != null ? groundRenderer.sharedMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
        puddleMaterial = new Material(litShader)
        {
            color = new Color(0.24f, 0.45f, 0.58f, 0.86f),
        };
        if (puddleMaterial.HasProperty(SmoothnessId)) puddleMaterial.SetFloat(SmoothnessId, 0.94f);
        if (puddleMaterial.HasProperty("_Metallic")) puddleMaterial.SetFloat("_Metallic", 0.18f);

        rippleMaterial = new Material(litShader)
        {
            color = new Color(0.72f, 0.90f, 1f, 0.65f),
        };
        if (rippleMaterial.HasProperty(SmoothnessId)) rippleMaterial.SetFloat(SmoothnessId, 0.9f);

        Vector3[] positions = { new Vector3(13f, 0.025f, 15f), new Vector3(-16f, 0.025f, -14f) };
        Vector2[] sizes = { new Vector2(5.2f, 3.8f), new Vector2(4.4f, 3.2f) };

        for (int i = 0; i < positions.Length; i++)
        {
            var puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            puddle.name = $"RainPuddle_{i}";
            Destroy(puddle.GetComponent<Collider>());
            puddle.transform.SetParent(environmentRoot, false);
            puddle.transform.position = positions[i];
            puddle.transform.rotation = Quaternion.Euler(0f, i * 42f + 15f, 0f);
            puddle.transform.localScale = new Vector3(sizes[i].x, 0.015f, sizes[i].y);
            var mr = puddle.GetComponent<MeshRenderer>();
            mr.sharedMaterial = puddleMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            var ripple = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ripple.name = "Ripple";
            Destroy(ripple.GetComponent<Collider>());
            ripple.transform.SetParent(environmentRoot, false);
            ripple.transform.position = positions[i] + Vector3.up * 0.012f;
            ripple.transform.localScale = new Vector3(1f, 0.008f, 1f);
            var rmr = ripple.GetComponent<MeshRenderer>();
            rmr.sharedMaterial = rippleMaterial;
            rmr.shadowCastingMode = ShadowCastingMode.Off;
            ripples.Add(ripple.transform);
        }
    }

    void SpawnClouds()
    {
        Shader litShader = groundRenderer != null ? groundRenderer.sharedMaterial.shader : Shader.Find("Universal Render Pipeline/Lit");
        cloudMaterial = new Material(litShader) { color = Color.white };
        if (cloudMaterial.HasProperty(SmoothnessId)) cloudMaterial.SetFloat(SmoothnessId, 0.1f);

        var rng = new System.Random(777);
        for (int i = 0; i < 14; i++)
        {
            var root = new GameObject($"SkyCloud_{i}").transform;
            root.SetParent(environmentRoot, false);
            Vector3 pos = new Vector3(
                Mathf.Lerp(-140f, 140f, (float)rng.NextDouble()),
                Mathf.Lerp(46f, 64f, (float)rng.NextDouble()),
                Mathf.Lerp(-130f, 130f, (float)rng.NextDouble()));
            root.position = pos;

            int puffs = 3 + rng.Next(3);
            for (int p = 0; p < puffs; p++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(puff.GetComponent<Collider>());
                puff.transform.SetParent(root, false);
                puff.transform.localPosition = new Vector3((p - puffs * 0.5f) * 3.8f, ((p % 2 == 0) ? 1.0f : 0f), (float)(rng.NextDouble() - 0.5) * 2.5f);
                float r = Mathf.Lerp(4.2f, 7.6f, (float)rng.NextDouble());
                puff.transform.localScale = new Vector3(r * 1.4f, r * 0.65f, r);
                var mr = puff.GetComponent<MeshRenderer>();
                mr.sharedMaterial = cloudMaterial;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            clouds.Add(new CloudEntry
            {
                root = root,
                basePos = pos,
                speed = Mathf.Lerp(0.8f, 1.8f, (float)rng.NextDouble()),
                phase = (float)rng.NextDouble() * 6.28f,
            });
        }
    }

    System.Collections.IEnumerator AttachGuideBeaconsAfterSpawn()
    {
        yield return null;
        var pipe = ExitPipe.Instance;
        if (pipe != null)
        {
            AddBeacon(pipe.transform, new Vector3(0f, 2.0f, 1.2f), new Color(0.45f, 1f, 0.65f), 14f, 0.6f, 3.2f);
        }
        foreach (var shrine in FindObjectsByType<Shrine>(FindObjectsSortMode.None))
        {
            if (shrine == null) continue;
            Color c = shrine.Type == Shrine.ShrineType.Chance
                ? new Color(0.55f, 1f, 0.55f)
                : new Color(1f, 0.28f, 0.28f);
            AddBeacon(shrine.transform, new Vector3(0f, 1.9f, 0f), c, 9f, 0.4f, 2.2f);
        }
    }

    void AddBeacon(Transform parent, Vector3 localOffset, Color color, float range, float dayInt, float nightInt)
    {
        var go = new GameObject("GuideBeaconLight");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localOffset;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = dayInt;
        l.shadows = LightShadows.None;
        beacons.Add(new BeaconLight { light = l, dayIntensity = dayInt, nightIntensity = nightInt });
    }

    void ApplySubtleTiltShift()
    {
        if (!enableTiltShiftDof) return;
        var volume = FindAnyObjectByType<Volume>();
        if (volume == null || volume.profile == null) return;
        if (!volume.profile.TryGet(out DepthOfField dof))
            dof = volume.profile.Add<DepthOfField>(overrides: true);

        dof.active = true;
        dof.mode.overrideState = true;
        dof.mode.value = DepthOfFieldMode.Gaussian;
        dof.gaussianStart.overrideState = true;
        dof.gaussianStart.value = 42f;
        dof.gaussianEnd.overrideState = true;
        dof.gaussianEnd.value = 120f;
        dof.gaussianMaxRadius.overrideState = true;
        dof.gaussianMaxRadius.value = 0.52f;
    }

    GameObject SpawnDecor(GameObject prefab, Vector3 pos, float yaw, Vector3 scale, bool castShadows)
    {
        var go = Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), environmentRoot);
        go.transform.localScale = scale;
        foreach (var col in go.GetComponentsInChildren<Collider>(true))
            Destroy(col);
        if (!castShadows)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = ShadowCastingMode.Off;
        }
        return go;
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
            sizeRange: new Vector2(0.08f, 0.15f),
            lifetimeRange: new Vector2(3.5f, 6f),
            speedRange: new Vector2(0.35f, 0.85f),
            boxSize: new Vector3(28f, 5.5f, 28f),
            maxParticles: 70,
            pulseCurve: new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.18f, 1f),
                new Keyframe(0.42f, 0.2f),
                new Keyframe(0.68f, 1f),
                new Keyframe(1f, 0f)));
        fireflyEmission = fireflySystem.emission;
        fireflyEmission.rateOverTime = 0f;

        starSystem = BuildMoteSystem(
            "BackyardNightStars",
            new Color(0.92f, 0.95f, 1f, 0.95f),
            new Color(0.76f, 0.88f, 1f, 0.8f),
            sizeRange: new Vector2(0.18f, 0.36f),
            lifetimeRange: new Vector2(5f, 9f),
            speedRange: new Vector2(0f, 0.04f),
            boxSize: new Vector3(160f, 14f, 160f),
            maxParticles: 120,
            pulseCurve: new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.3f, 1f),
                new Keyframe(0.7f, 0.45f),
                new Keyframe(1f, 0f)));
        starEmission = starSystem.emission;
        starEmission.rateOverTime = 0f;
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
        go.transform.SetParent(environmentRoot, false);
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
        r.shadowCastingMode = ShadowCastingMode.Off;
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
