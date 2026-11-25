using UnityEngine;

/// <summary>
/// Ciclo dia/noite ligado ao tempo da run: o sol nasce, vai até o alto, se põe,
/// e de noite entra uma "lua" azulada fraca. Vai na Directional Light.
/// A troca sol/lua acontece com as duas luzes apagadas, então a sombra não pula.
/// </summary>
[RequireComponent(typeof(Light))]
public class DayNightCycle : MonoBehaviour
{
    [Tooltip("Duração de um dia inteiro (dia + noite) em segundos.")]
    [SerializeField, Min(10f)] float cycleLength = 480f;
    [Tooltip("Onde o ciclo começa: 0 = nascer do sol, 0.25 = meio-dia, 0.5 = pôr do sol, 0.75 = meia-noite.")]
    [SerializeField, Range(0f, 1f)] float startPhase = 0.08f;
    [SerializeField, Range(5f, 90f)] float maxSunElevation = 65f;
    [SerializeField, Range(5f, 90f)] float moonElevation = 40f;
    [Tooltip("Direção (yaw) de onde o sol vem.")]
    [SerializeField] float sunYaw = -30f;

    [Header("Luz")]
    [Tooltip("0 = usa a intensidade que a luz já tem na cena.")]
    [SerializeField, Min(0f)] float dayIntensity = 0f;
    [SerializeField, Min(0f)] float moonIntensity = 0.35f;
    [SerializeField] Gradient sunColor = DefaultSunColor();
    [SerializeField] Color moonColor = new(0.55f, 0.65f, 1f);
    [SerializeField, Range(0f, 1f)] float nightAmbient = 0.35f;
    [Tooltip("Cor da neblina à noite (de dia usa a cor da cena).")]
    [SerializeField] Color nightFog = new(0.06f, 0.08f, 0.13f);

    /// <summary>O ciclo da cena (null sem ciclo). Para quem só pergunta se é noite (vaga-lume).</summary>
    public static DayNightCycle Instance { get; private set; }

    public float Phase { get; private set; }
    /// <summary>Sol abaixo do horizonte (vaga-lume só aparece à noite).</summary>
    public bool IsNight => Mathf.Sin(Phase * Mathf.PI * 2f) < 0f;
    /// <summary>1 = dia claro, 0 = noite.</summary>
    public float Daylight { get; private set; } = 1f;

    /// <summary>
    /// Céu fechado (0 = limpo, 1 = tempestade): escurece o sol e puxa a luz ambiente e a
    /// neblina para um cinza-azulado. Quem liga é o evento de chuva (RainEvent).
    /// </summary>
    public static float Overcast;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Overcast = 0f; Instance = null; }

    Light sun;
    float dayAmbient;
    Color dayFog;

    static Gradient DefaultSunColor()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.55f, 0.3f), 0f),   // horizonte: laranja
                new GradientColorKey(new Color(1f, 0.9f, 0.75f), 0.35f),
                new GradientColorKey(new Color(1f, 0.98f, 0.92f), 1f),  // alto: quase branco
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    void Awake()
    {
        Instance = this;
        sun = GetComponent<Light>();
        if (dayIntensity <= 0f) dayIntensity = sun.intensity;
        dayAmbient = RenderSettings.ambientIntensity;
        dayFog = RenderSettings.fogColor;
        RenderSettings.sun = sun; // o skybox procedural segue essa luz
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        float time = RunManager.Instance != null ? RunManager.Instance.RunTime : Time.timeSinceLevelLoad;
        Apply(Mathf.Repeat(startPhase + time / cycleLength, 1f));
    }

    void Apply(float phase)
    {
        Phase = phase;
        float s = Mathf.Sin(phase * Mathf.PI * 2f); // >0 dia, <0 noite
        float sunUp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.2f, s));
        float moonUp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, -0.2f, s));
        Daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.1f, 0.3f, s));

        if (s >= 0f)
        {
            // Sol atravessa o céu: nasce de um lado (yaw) e se põe do outro
            float elevation = Mathf.Max(s * maxSunElevation, 2f);
            float travel = Mathf.Lerp(-80f, 80f, phase * 2f);
            transform.rotation = Quaternion.Euler(elevation, sunYaw + travel, 0f);
            sun.intensity = dayIntensity * sunUp;
            sun.color = sunColor.Evaluate(Mathf.Clamp01(s));
        }
        else
        {
            transform.rotation = Quaternion.Euler(moonElevation, sunYaw + 180f, 0f);
            sun.intensity = moonIntensity * moonUp;
            sun.color = moonColor;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        float sunset = Mathf.Clamp01(1f - Mathf.Abs(Daylight - 0.45f) * 2.5f) * Mathf.Clamp01(Daylight * 2.5f);
        Color daySky = new(0.56f, 0.72f, 0.88f);
        Color dayEq = new(0.54f, 0.60f, 0.56f);
        Color dayGnd = new(0.24f, 0.30f, 0.18f);

        Color duskSky = new(0.76f, 0.46f, 0.36f);
        Color duskEq = new(0.84f, 0.56f, 0.36f);
        Color duskGnd = new(0.28f, 0.20f, 0.16f);

        Color nightSky = new(0.10f, 0.14f, 0.26f);
        Color nightEq = new(0.14f, 0.18f, 0.30f);
        Color nightGnd = new(0.06f, 0.08f, 0.11f);

        Color baseSky = Color.Lerp(nightSky, daySky, Daylight);
        Color baseEq = Color.Lerp(nightEq, dayEq, Daylight);
        Color baseGnd = Color.Lerp(nightGnd, dayGnd, Daylight);

        RenderSettings.ambientSkyColor = Color.Lerp(baseSky, duskSky, sunset * 0.75f);
        RenderSettings.ambientEquatorColor = Color.Lerp(baseEq, duskEq, sunset * 0.75f);
        RenderSettings.ambientGroundColor = Color.Lerp(baseGnd, duskGnd, sunset * 0.75f);
        RenderSettings.ambientIntensity = Mathf.Lerp(dayAmbient * nightAmbient, dayAmbient, Daylight);

        Color duskFog = new(0.86f, 0.66f, 0.54f);
        Color baseFog = Color.Lerp(nightFog, dayFog, Daylight);
        if (RenderSettings.fog) RenderSettings.fogColor = Color.Lerp(baseFog, duskFog, sunset * 0.65f);

        // Chuva: sol mais fraco, céu e neblina acinzentados
        float o = Mathf.Clamp01(Overcast);
        if (o > 0f)
        {
            Color storm = new(0.36f, 0.42f, 0.5f);
            sun.intensity *= 1f - 0.65f * o;
            RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, storm, 0.6f * o);
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, storm * 0.9f, 0.5f * o);
            RenderSettings.ambientIntensity *= 1f - 0.3f * o;
            if (RenderSettings.fog) RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, storm, 0.7f * o);
        }
    }
}
