using UnityEngine;

/// <summary>
/// Cupim: reaproveita a IA da formiga operária, mas nasce em grupos saindo de baixo da terra
/// com animação de emergência e partículas de terra.
/// </summary>
public class Termite : WorkerAnt
{
    [Header("Cupim - Saindo do chão")]
    [SerializeField, Min(0.1f)] float emergeDuration = 0.45f;
    [SerializeField, Min(0f)] float emergeDepth = 1.1f;
    [Tooltip("Oscilação lateral do corpo enquanto cava para fora.")]
    [SerializeField] float emergeWiggleAngle = 14f;
    [SerializeField] Material dirtMaterial;
    [SerializeField] Mesh dirtMesh;
    [Tooltip("Montinho de terra opcional na base que cresce durante a saída e some ao terminar.")]
    [SerializeField] Transform moundVisual;
    [SerializeField] ParticleSystem dirtParticles;

    public bool IsEmerging { get; private set; }

    static Mesh fallbackDirtMesh;
    float emergeTime;
    Vector3 moundRestScale = Vector3.one;

    protected override void Awake()
    {
        base.Awake();
        if (moundVisual != null) moundRestScale = moundVisual.localScale;
        SetupDirtParticles();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        BeginEmerge();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        EndEmergeVisuals();
        if (dirtParticles != null) dirtParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void BeginEmerge()
    {
        IsEmerging = true;
        emergeTime = 0f;
        StopMoving();
        ApplyEmergePose(0f);
        if (moundVisual != null)
        {
            moundVisual.gameObject.SetActive(true);
            moundVisual.localScale = Vector3.zero;
        }
        if (dirtParticles != null)
        {
            dirtParticles.Clear(true);
            dirtParticles.Play(true);
        }
    }

    protected override void Tick()
    {
        if (IsEmerging)
        {
            StopMoving();
            FaceTarget();
            emergeTime += Time.deltaTime;
            float k = Mathf.Clamp01(emergeTime / Mathf.Max(emergeDuration, 0.01f));
            ApplyEmergePose(k);
            if (k >= 1f) EndEmergeVisuals();
            return;
        }
        base.Tick();
    }

    void ApplyEmergePose(float k)
    {
        float ease = 1f - Mathf.Pow(1f - k, 2.2f);
        float yOffset = -emergeDepth * (1f - ease);
        float hop = k > 0.75f ? Mathf.Sin(((k - 0.75f) / 0.25f) * Mathf.PI) * 0.18f : 0f;
        float wiggle = Mathf.Sin(emergeTime * 28f) * emergeWiggleAngle * (1f - k);
        float pitch = Mathf.Lerp(-35f, 0f, ease);

        if (body != null)
        {
            body.localPosition = bodyRestPos + new Vector3(0f, yOffset + hop, 0f);
            body.localRotation = bodyRestRot * Quaternion.Euler(pitch, 0f, wiggle);
        }

        if (moundVisual != null)
        {
            float moundScale = k < 0.35f ? k / 0.35f : k < 0.75f ? 1f : 1f - (k - 0.75f) / 0.25f;
            moundVisual.localScale = moundRestScale * Mathf.Clamp01(moundScale);
        }
    }

    void EndEmergeVisuals()
    {
        IsEmerging = false;
        if (body != null)
        {
            body.localPosition = bodyRestPos;
            body.localRotation = bodyRestRot;
        }
        if (moundVisual != null) moundVisual.gameObject.SetActive(false);
    }

    protected override void HandleDeath(DamageInfo killingBlow)
    {
        EndEmergeVisuals();
        base.HandleDeath(killingBlow);
    }

    void SetupDirtParticles()
    {
        if (dirtParticles == null)
        {
            var go = new GameObject("DirtParticles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            dirtParticles = go.AddComponent<ParticleSystem>();
        }

        var main = dirtParticles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = emergeDuration;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 1.6f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;

        var emission = dirtParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 14),
            new ParticleSystem.Burst(emergeDuration * 0.45f, 10),
        });

        var shape = dirtParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.35f;

        var sizeOverLifetime = dirtParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 0.85f), new Keyframe(1f, 0f))
        );

        var psRenderer = dirtParticles.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        psRenderer.mesh = ResolveDirtMesh();
        if (dirtMaterial != null) psRenderer.sharedMaterial = dirtMaterial;
        psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    Mesh ResolveDirtMesh()
    {
        if (dirtMesh != null) return dirtMesh;
        if (fallbackDirtMesh == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallbackDirtMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
        }
        return fallbackDirtMesh;
    }
}
