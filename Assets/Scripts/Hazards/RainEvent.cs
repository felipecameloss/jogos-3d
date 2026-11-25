using System;
using UnityEngine;

/// <summary>
/// Evento aleatório: chuva (issue #33). De tempos em tempos cai uma pancada de chuva:
/// o céu fecha (DayNightCycle.Overcast), aparecem os riscos de chuva em volta do player
/// e um chiado, e gotas gigantes (RainDrop) caem perto dele com uma sombra de aviso no
/// chão, como o pisão humano. Na maior parte das vezes a gota mira onde o player vai
/// estar; às vezes cai perto, ao acaso. Gotas mais frequentes com a dificuldade.
/// </summary>
public class RainEvent : MonoBehaviour
{
    [SerializeField] RainDrop dropPrefab;
    [Tooltip("Material de partícula dos riscos de chuva (Particles/Unlit).")]
    [SerializeField] Material rainMaterial;

    [Header("Quando chove")]
    [Tooltip("Primeira chuva depois desse tempo de fase (mín, máx).")]
    [SerializeField] Vector2 firstRainDelay = new(60f, 100f);
    [Tooltip("Tempo seco entre uma chuva e outra (mín, máx).")]
    [SerializeField] Vector2 dryInterval = new(90f, 150f);
    [Tooltip("Duração de cada chuva (mín, máx).")]
    [SerializeField] Vector2 rainDuration = new(18f, 26f);
    [Tooltip("Segundos para o céu fechar/abrir.")]
    [SerializeField, Min(0.1f)] float fadeTime = 3f;

    [Header("Gotas")]
    [Tooltip("Intervalo entre gotas (mín, máx) com dificuldade 1.")]
    [SerializeField] Vector2 dropInterval = new(0.7f, 1.4f);
    [Tooltip("Quanto mais frequente fica por ponto de dificuldade acima de 1.")]
    [SerializeField, Min(0f)] float frequencyPerPoint = 0.15f;
    [Tooltip("Chance de mirar no player (senão, ponto aleatório perto dele).")]
    [SerializeField, Range(0f, 1f)] float aimAtPlayerChance = 0.5f;
    [Tooltip("Antecipa o movimento do player por esse tempo.")]
    [SerializeField, Min(0f)] float leadTime = 0.6f;
    [SerializeField, Min(0f)] float randomRadius = 14f;

    [Header("Visual e som")]
    [SerializeField, Min(0)] int maxRainParticles = 900;
    [SerializeField, Range(0f, 1f)] float rainVolume = 0.35f;

    public event Action OnRainStarted;
    public event Action OnRainEnded;

    public bool IsRaining { get; private set; }
    /// <summary>0..1: quanto o céu está fechado agora (sobe/desce em fadeTime).</summary>
    public float Intensity { get; private set; }
    public int DropCount { get; private set; }
    public int RainCount { get; private set; }
    public float NextRainTime => nextRainAt;
    public ParticleSystem Particles => particles;

    ComponentPool<RainDrop> pool;
    Transform player;
    PlayerMovement playerMovement;
    ParticleSystem particles;
    ParticleSystem.EmissionModule emission;
    AudioSource audioSource;
    float nextRainAt, rainEndsAt, nextDropAt;

    void Awake()
    {
        if (dropPrefab != null) pool = new ComponentPool<RainDrop>(dropPrefab, 4, maxSize: 16, rootName: "[Pool] RainDrop");
        else Debug.LogError("RainEvent sem prefab da gota.", this);
        BuildParticles();
        BuildAudio();
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { player = p.transform; playerMovement = p.GetComponent<PlayerMovement>(); }
        nextRainAt = Time.time + UnityEngine.Random.Range(firstRainDelay.x, firstRainDelay.y);
    }

    void OnDestroy()
    {
        DayNightCycle.Overcast = 0f;
    }

    void Update()
    {
        bool runOver = RunManager.Instance != null && (RunManager.Instance.IsRunOver || RunManager.Instance.IsStageComplete);

        if (!IsRaining && !runOver && Time.time >= nextRainAt) StartRain(UnityEngine.Random.Range(rainDuration.x, rainDuration.y));
        if (IsRaining && (Time.time >= rainEndsAt || runOver)) StopRain();

        Intensity = Mathf.MoveTowards(Intensity, IsRaining ? 1f : 0f, Time.deltaTime / fadeTime);
        DayNightCycle.Overcast = Intensity;
        UpdateEffects();

        // Gotas só depois do céu fechar um pouco (dá tempo de perceber que começou)
        if (IsRaining && !runOver && Intensity > 0.5f && player != null && Time.time >= nextDropAt)
        {
            float coeff = DifficultyManager.Instance != null ? DifficultyManager.Instance.Coefficient : 1f;
            float div = 1f + frequencyPerPoint * (coeff - 1f);
            nextDropAt = Time.time + UnityEngine.Random.Range(dropInterval.x, dropInterval.y) / div;
            DropNear(player.position);
        }
    }

    /// <summary>Começa a chover agora por `duration` segundos (público para testes e depuração).</summary>
    public void StartRain(float duration)
    {
        if (IsRaining) { rainEndsAt = Mathf.Max(rainEndsAt, Time.time + duration); return; }
        IsRaining = true;
        RainCount++;
        rainEndsAt = Time.time + duration;
        nextDropAt = Time.time + 0.5f;
        OnRainStarted?.Invoke();
    }

    public void StopRain()
    {
        if (!IsRaining) return;
        IsRaining = false;
        nextRainAt = Time.time + UnityEngine.Random.Range(dryInterval.x, dryInterval.y);
        OnRainEnded?.Invoke();
    }

    /// <summary>Uma gota perto de um ponto: mira no player (com antecipação) ou cai ao acaso.</summary>
    public RainDrop DropNear(Vector3 around)
    {
        Vector3 target;
        if (UnityEngine.Random.value < aimAtPlayerChance)
        {
            Vector3 lead = playerMovement != null ? playerMovement.Velocity * leadTime : Vector3.zero;
            lead.y = 0f;
            target = around + lead;
        }
        else
        {
            Vector2 r = UnityEngine.Random.insideUnitCircle * randomRadius;
            target = around + new Vector3(r.x, 0f, r.y);
        }
        return DropAt(target);
    }

    /// <summary>Uma gota exatamente aqui (no chão embaixo do ponto).</summary>
    public RainDrop DropAt(Vector3 target)
    {
        if (pool == null || !StompDirector.FindGround(target, out Vector3 ground)) return null;
        var d = pool.Get();
        d.Begin(ground, pool.Release);
        DropCount++;
        return d;
    }

    // ---------- Visual e som ----------

    void BuildParticles()
    {
        var go = new GameObject("RainParticles");
        go.transform.SetParent(transform, false);
        // A caixa emite para o +Z local: girado 90° em X, as gotas saem para baixo
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.loop = true;
        main.startLifetime = 0.75f;
        main.startSpeed = 34f;
        main.startSize = 0.07f;
        main.startColor = new Color(0.78f, 0.88f, 1f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(1, maxRainParticles);
        main.gravityModifier = 0f;

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(46f, 46f, 1f);

        emission = particles.emission;
        emission.rateOverTime = 0f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.lengthScale = 3f;
        r.velocityScale = 0.04f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (rainMaterial != null) r.sharedMaterial = rainMaterial;
        particles.Play();
    }

    void BuildAudio()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = BuildRainClip();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = 0f;
    }

    /// <summary>Chiado de chuva: ruído filtrado (grave + um pouco de agudo), em loop.</summary>
    static AudioClip BuildRainClip()
    {
        const int rate = 22050;
        const float seconds = 2f;
        int n = Mathf.RoundToInt(rate * seconds);
        var data = new float[n];
        var rng = new System.Random(33);
        float low = 0f, high = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            low += (white - low) * 0.08f;          // grave: o "shhh" da chuva
            high += (white - high) * 0.55f;        // agudo: os pingos
            data[i] = low * 0.9f + (white - high) * 0.25f;
        }
        // emenda sem estalo: fade curto nas pontas
        int fade = rate / 20;
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            data[i] *= k;
            data[n - 1 - i] *= k;
        }
        var clip = AudioClip.Create("Rain", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void UpdateEffects()
    {
        if (particles != null)
        {
            if (player != null) particles.transform.position = player.position + Vector3.up * 16f;
            emission.rateOverTime = Intensity * maxRainParticles / 0.75f; // cheio = maxParticles vivas
        }
        if (audioSource != null)
        {
            float v = Intensity * rainVolume * Mathf.Clamp01(GameSettings.SfxVolume);
            audioSource.volume = v;
            if (v > 0.001f && !audioSource.isPlaying) audioSource.Play();
            else if (v <= 0.001f && audioSource.isPlaying) audioSource.Stop();
        }
    }

#if UNITY_EDITOR
    public void EditorSetup(RainDrop drop, Material material)
    {
        dropPrefab = drop; rainMaterial = material;
    }
#endif
}
