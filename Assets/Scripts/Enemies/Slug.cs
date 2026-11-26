using UnityEngine;

/// <summary>
/// Lesma: lenta e parruda. Vai atrás do player deixando um rastro de gosma brilhante
/// (<see cref="SlimeTrail"/>); pisar na gosma faz o player escorregar (quase sem atrito:
/// demora para mudar de direção e para frear). De perto, ergue a frente do corpo (aviso)
/// e dá um bote molhado. A gosma fica um tempo no chão: lesmas mudam o terreno da luta.
/// </summary>
public class Slug : EnemyBase
{
    enum State { Crawl, WindUp, Recover }

    [Header("Bote")]
    [SerializeField, Min(0f)] float damage = 12f;
    [Tooltip("Distância (no plano) para começar o bote.")]
    [SerializeField, Min(0.1f)] float attackRange = 2.4f;
    [Tooltip("Folga no momento do bote: recuar só um pouco não salva.")]
    [SerializeField, Min(0f)] float hitRangeBonus = 0.6f;
    [SerializeField, Min(0.05f)] float windUpTime = 0.75f;
    [SerializeField, Min(0f)] float recoverTime = 0.9f;

    [Header("Rastro de gosma")]
    [Tooltip("Metros rastejados entre uma mancha de gosma e a próxima.")]
    [SerializeField, Min(0.1f)] float dropSpacing = 0.6f;
    [SerializeField, Min(0.2f)] float slimeRadius = 1f;
    [SerializeField, Min(1f)] float slimeLifetime = 10f;
    [Tooltip("Material transparente da gosma (a cor vem do código).")]
    [SerializeField] Material slimeMaterial;

    [Header("Visual")]
    [Tooltip("Body externo do prefab (pai do modelo): estica ao rastejar e ergue no aviso.")]
    [SerializeField] Transform body;
    [Tooltip("Quanto o corpo estica/encolhe ao rastejar (fração do comprimento).")]
    [SerializeField, Range(0f, 0.3f)] float crawlStretch = 0.08f;

    public int Bites { get; private set; }
    public int BitesLanded { get; private set; }
    public bool IsWindingUp => state == State.WindUp;

    State state;
    float stateStart, crawlPhase;
    Vector3 lastDrop, bodyRestPos, bodyRestScale;
    Quaternion bodyRestRot;
    bool hasDropped;

    protected override void Awake()
    {
        base.Awake();
        if (body != null) { bodyRestPos = body.localPosition; bodyRestRot = body.localRotation; bodyRestScale = body.localScale; }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        hasDropped = false;
        Enter(State.Crawl);
        ResetBody();
    }

    void Enter(State s) { state = s; stateStart = Time.time; }
    float Elapsed => Time.time - stateStart;

    protected override void Update()
    {
        base.Update();
        if (!IsAlive) return;
        if (!HasValidTarget && state != State.Crawl) { Enter(State.Crawl); ResetBody(); }
        LeaveSlime();
        AnimateCrawl();
    }

    protected override void Tick()
    {
        switch (state)
        {
            case State.Crawl:
                if (FlatDistanceToTarget() <= attackRange)
                {
                    StopMoving();
                    if (FaceTarget() > 0.7f) { Bites++; Enter(State.WindUp); }
                }
                else MoveTo(Target.position);
                break;

            case State.WindUp:
                FaceTarget();
                float k = Mathf.Clamp01(Elapsed / windUpTime);
                Rear(k);
                if (k >= 1f) { Slam(); Enter(State.Recover); }
                break;

            case State.Recover:
                Rear(1f - Mathf.Clamp01(Elapsed / 0.2f));
                if (Elapsed >= recoverTime) Enter(State.Crawl);
                break;
        }
    }

    void Slam()
    {
        // o bote também deixa uma poça de gosma na frente
        SlimeTrail.Drop(transform.position + transform.forward * attackRange * 0.6f, slimeRadius * 1.3f, slimeLifetime, slimeMaterial);
        if (TargetHealth == null || !TargetInFront(attackRange + hitRangeBonus, 0.2f)) return;
        Vector3 dir = (Target.position - transform.position).normalized;
        TargetHealth.TakeDamage(new DamageInfo(damage * DamageMultiplier, gameObject, Target.position + Vector3.up, dir));
        BitesLanded++;
    }

    void LeaveSlime()
    {
        Vector3 p = transform.position;
        if (!hasDropped) { lastDrop = p; hasDropped = true; SlimeTrail.Drop(p, slimeRadius, slimeLifetime, slimeMaterial); return; }
        Vector3 d = p - lastDrop; d.y = 0f;
        if (d.sqrMagnitude < dropSpacing * dropSpacing) return;
        lastDrop = p;
        SlimeTrail.Drop(p, slimeRadius, slimeLifetime, slimeMaterial);
    }

    // ---------- Visual ----------

    void AnimateCrawl()
    {
        if (body == null || state != State.Crawl) return;
        // rasteja em ondas: estica e encolhe no comprimento conforme anda
        float speed = Agent.enabled ? Agent.velocity.magnitude : 0f;
        crawlPhase += speed * Time.deltaTime * 4f;
        float s = Mathf.Sin(crawlPhase) * crawlStretch * Mathf.Clamp01(speed);
        body.localScale = Vector3.Scale(bodyRestScale, new Vector3(1f - s * 0.5f, 1f - s * 0.3f, 1f + s));
    }

    void Rear(float k)
    {
        if (body == null) return;
        // ergue a frente (aviso) tremendo de leve no fim
        float shake = k > 0.6f ? Mathf.Sin(Time.time * 55f) * 1.5f : 0f;
        body.localRotation = bodyRestRot * Quaternion.Euler(-24f * k + shake, 0f, 0f);
        body.localPosition = bodyRestPos + Vector3.up * (0.15f * k);
        body.localScale = Vector3.Scale(bodyRestScale, new Vector3(1f, 1f, 1f + 0.12f * k));
    }

    void ResetBody()
    {
        if (body == null) return;
        body.localPosition = bodyRestPos;
        body.localRotation = bodyRestRot;
        body.localScale = bodyRestScale;
    }

    protected override void HandleDeath(DamageInfo killingBlow)
    {
        Enter(State.Crawl);
        ResetBody();
        base.HandleDeath(killingBlow);
    }

#if UNITY_EDITOR
    public void EditorSetup(Transform bodyT, Material slime)
    {
        body = bodyT; slimeMaterial = slime;
    }
#endif
}
