using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Vaga-lume: só aparece à noite (<see cref="CanSpawnNow"/>; quando amanhece, sobe e vai
/// embora sem soltar migalhas). Voa em volta do player na altura da cabeça, com a lanterna
/// acesa (dá para ver de longe no escuro). De tempos em tempos carrega a luz (pisca cada vez
/// mais rápido) e solta um clarão que cega a tela por um instante se o player estiver olhando
/// para ele (<see cref="PlayerBlindness"/>). Virar a câmera durante a carga evita. Não morde:
/// cega para os outros inimigos baterem.
/// </summary>
public class Firefly : EnemyBase
{
    enum State { Fly, Charge, Leave }

    [Header("Voo")]
    [Tooltip("Distância (mín, máx) em que ele ronda o player.")]
    [SerializeField] Vector2 orbitDistance = new(6f, 10f);
    [Tooltip("Sobe e desce enquanto voa (metros).")]
    [SerializeField, Min(0f)] float bobAmplitude = 0.3f;

    [Header("Clarão")]
    [SerializeField, Min(0.1f)] float chargeTime = 1.2f;
    [Tooltip("Segundos entre um clarão e o próximo (sorteado entre os dois).")]
    [SerializeField] Vector2 flashCooldown = new(5f, 8f);
    [SerializeField, Min(1f)] float flashRange = 18f;
    [Tooltip("Duração da cegueira de um clarão em cheio (segundos).")]
    [SerializeField, Min(0.1f)] float blindDuration = 1.8f;
    [Tooltip("Ângulo do centro da tela em que o clarão cega em cheio.")]
    [SerializeField, Range(5f, 90f)] float fullAngle = 30f;
    [Tooltip("Ângulo a partir do qual não cega mais (olhou para o lado).")]
    [SerializeField, Range(10f, 120f)] float zeroAngle = 70f;
    [Tooltip("Abaixo dessa força o clarão só incomoda e não conta como cegar.")]
    [SerializeField, Range(0f, 1f)] float minStrength = 0.12f;

    [Header("Visual")]
    [Tooltip("Body externo do prefab (balança no voo).")]
    [SerializeField] Transform body;
    [Tooltip("Lanterna (ponta do abdômen): de onde sai o clarão.")]
    [SerializeField] Transform lantern;
    [SerializeField] Renderer lanternRenderer;
    [SerializeField] Light glowLight;
    [SerializeField] Color glowColor = new(0.85f, 1f, 0.3f);
    [SerializeField, Min(0f)] float idleGlow = 1.6f;
    [SerializeField, Min(0f)] float chargeGlow = 6f;
    [SerializeField, Min(0f)] float lightIntensity = 1.2f;
    [SerializeField, Min(0f)] float flashLightIntensity = 14f;
    [Tooltip("Batidas de asa por segundo e o ângulo de cada uma.")]
    [SerializeField, Min(0f)] float flapRate = 12f;
    [SerializeField, Range(0f, 80f)] float flapAngle = 38f;

    [Header("Amanhecer")]
    [SerializeField, Min(0.1f)] float leaveTime = 1.4f;
    [SerializeField, Min(0f)] float leaveRise = 7f;

    public int Flashes { get; private set; }
    public int Blinds { get; private set; }
    public bool IsCharging => state == State.Charge;
    public bool IsLeaving => state == State.Leave;

    /// <summary>Noite agora (sem ciclo de dia/noite na cena, ou numa fase escura, conta como noite).</summary>
    public static bool IsNight => DayNightCycle.Instance == null || DayNightCycle.Instance.IsNight
                                  || (StageDirector.Current != null && StageDirector.Current.Dark);
    public override bool CanSpawnNow => IsNight;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    State state;
    float stateStart, nextFlash, circleSign = 1f, flashGlow, bobPhase;
    Vector3 bodyRestPos, leaveFrom;
    MaterialPropertyBlock block;
    Transform wingL, wingR;
    Quaternion wingLRest, wingRRest;

    protected override void Awake()
    {
        base.Awake();
        block = new MaterialPropertyBlock();
        if (body != null) bodyRestPos = body.localPosition;
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Wing_L") { wingL = t; wingLRest = t.localRotation; }
            else if (t.name == "Wing_R") { wingR = t; wingRRest = t.localRotation; }
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Agent.updatePosition = true;
        state = State.Fly;
        nextFlash = Time.time + Random.Range(2f, 3.5f);
        circleSign = Random.value < 0.5f ? -1f : 1f;
        bobPhase = Random.value * 10f;
        flashGlow = 0f;
        SetGlow(idleGlow, lightIntensity);
    }

    void Enter(State s) { state = s; stateStart = Time.time; }
    float Elapsed => Time.time - stateStart;

    protected override void Update()
    {
        if (!IsAlive) return;
        if (state == State.Leave) { UpdateLeave(); return; }
        if (!IsNight) { StartLeave(); return; }
        base.Update();
        if (!HasValidTarget && state == State.Charge) Enter(State.Fly);
        Bob();
        if (state == State.Fly) Glow(idleGlow);
    }

    protected override void Tick()
    {
        switch (state)
        {
            case State.Fly:
                if (Time.time >= nextFlash && FlatDistanceToTarget() <= flashRange * 0.85f) { StopMoving(); Enter(State.Charge); return; }
                Orbit();
                break;

            case State.Charge:
                FaceTarget();
                float k = Mathf.Clamp01(Elapsed / chargeTime);
                // pisca cada vez mais rápido e mais forte
                float blink = 0.5f + 0.5f * Mathf.Sin(Elapsed * Mathf.Lerp(8f, 40f, k * k));
                SetGlow(Mathf.Lerp(idleGlow, chargeGlow, k) * (0.6f + 0.4f * blink), Mathf.Lerp(lightIntensity, lightIntensity * 4f, k * blink));
                if (k >= 1f) FlashNow();
                break;
        }
    }

    /// <summary>Solta o clarão agora (fim da carga; testes chamam direto).</summary>
    public void FlashNow()
    {
        Flashes++;
        nextFlash = Time.time + Random.Range(flashCooldown.x, flashCooldown.y);
        Enter(State.Fly);
        flashGlow = 1f;
        Vector3 at = lantern != null ? lantern.position : Center;
        ItemVfx.Explosion(at, 3f, new Color(1f, 1f, 0.75f, 0.85f), 0.3f);

        float strength = PlayerBlindness.StrengthFrom(at, flashRange, fullAngle, zeroAngle, transform);
        if (strength < minStrength) return;
        PlayerBlindness.Blind(strength, blindDuration * Mathf.Lerp(0.5f, 1f, strength));
        Blinds++;
    }

    void Orbit()
    {
        Vector3 away = transform.position - Target.position; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        float dist = Mathf.Clamp(away.magnitude, orbitDistance.x, orbitDistance.y);
        Vector3 around = Quaternion.Euler(0f, circleSign * 35f, 0f) * away.normalized;
        Vector3 dest = Target.position + around * dist;
        if (NavMesh.SamplePosition(dest, out var hit, 3f, NavMesh.AllAreas)) MoveTo(hit.position);
        else circleSign = -circleSign;
    }

    // ---------- Amanhecer ----------

    void StartLeave()
    {
        StopMoving();
        Agent.enabled = false;
        leaveFrom = transform.position;
        Enter(State.Leave);
    }

    void UpdateLeave()
    {
        float k = Mathf.Clamp01(Elapsed / leaveTime);
        transform.position = leaveFrom + Vector3.up * (leaveRise * k * k) + transform.forward * (2f * k);
        SetGlow(idleGlow * (1f - k), lightIntensity * (1f - k));
        Bob();
        if (k >= 1f) Vanish();
    }

    // ---------- Visual ----------

    void Bob()
    {
        if (body != null)
            body.localPosition = bodyRestPos + Vector3.up * (Mathf.Sin((Time.time + bobPhase) * 2.4f) * bobAmplitude / Mathf.Max(0.01f, transform.lossyScale.y));
        // asas batendo (sobem e descem girando no eixo da frente)
        float flap = Mathf.Sin((Time.time + bobPhase) * flapRate * Mathf.PI * 2f) * flapAngle;
        if (wingL != null) wingL.localRotation = wingLRest * Quaternion.Euler(0f, 0f, -flap);
        if (wingR != null) wingR.localRotation = wingRRest * Quaternion.Euler(0f, 0f, flap);
        if (glowLight != null && lantern != null) glowLight.transform.position = lantern.position;
    }

    void Glow(float baseGlow)
    {
        flashGlow = Mathf.MoveTowards(flashGlow, 0f, Time.deltaTime * 3f);
        float pulse = 0.85f + 0.15f * Mathf.Sin((Time.time + bobPhase) * 3f);
        SetGlow(baseGlow * pulse + chargeGlow * 2f * flashGlow, lightIntensity * pulse + flashLightIntensity * flashGlow);
    }

    void SetGlow(float emission, float light)
    {
        if (lanternRenderer != null)
        {
            lanternRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionId, glowColor * emission);
            lanternRenderer.SetPropertyBlock(block);
        }
        if (glowLight != null)
        {
            glowLight.color = glowColor;
            glowLight.intensity = light;
        }
    }

    protected override void HandleDeath(DamageInfo killingBlow)
    {
        state = State.Fly;
        SetGlow(0f, 0f);
        base.HandleDeath(killingBlow);
    }

#if UNITY_EDITOR
    public void EditorSetup(Transform bodyT, Transform lanternT, Renderer lanternR, Light light)
    {
        body = bodyT; lantern = lanternT; lanternRenderer = lanternR; glowLight = light;
    }
#endif
}
