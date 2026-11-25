using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Centopeia: uma corrente de segmentos em que cada um segue exatamente o caminho do da frente.
/// Cada segmento é um inimigo com vida própria (e solta migalhas). A cabeça caça o player
/// serpenteando e morde com aviso (ergue a cabeça e treme); o corpo machuca quem encosta.
/// Matar um segmento do meio parte a centopeia em duas: o segmento logo atrás vira a cabeça
/// da parte de trás (para um instante, assustado) e sai caçando sozinho. Matar a cabeça só
/// passa o comando para o próximo: ela só acaba quando o último segmento morre.
/// Só a cabeça usa o NavMeshAgent; os segmentos do corpo andam pelo rastro da frente.
/// </summary>
public class Centipede : EnemyBase
{
    enum State { Hunt, BiteWindUp, BiteRecover, Startled }

    [Header("Corpo")]
    [Tooltip("Nasce como cabeça (vinda do spawner, monta o corpo) ou como segmento de corpo.")]
    [SerializeField] bool startsAsHead = true;
    [Tooltip("Segmentos que a cabeça monta atrás de si ao nascer.")]
    [SerializeField, Min(0)] int bodySegments = 9;
    [Tooltip("Prefab do segmento de corpo (o mesmo componente, com startsAsHead desligado).")]
    [SerializeField] Centipede segmentPrefab;
    [Tooltip("Distância de um segmento até o da frente, em metros (antes da escala da raiz).")]
    [SerializeField, Min(0.1f)] float spacing = 0.62f;

    [Header("Mordida (cabeça)")]
    [SerializeField, Min(0f)] float biteDamage = 12f;
    [SerializeField, Min(0.5f)] float biteRange = 2.1f;
    [SerializeField, Min(0.05f)] float biteWindUp = 0.35f;
    [SerializeField, Min(0f)] float biteRecover = 0.5f;

    [Header("Encostar no corpo")]
    [SerializeField, Min(0f)] float contactDamage = 5f;
    [Tooltip("Distância do centro do segmento em que encostar machuca (antes da escala).")]
    [SerializeField, Min(0.1f)] float contactRadius = 0.55f;
    [Tooltip("Tempo mínimo entre dois toques que machucam (vale para a centopeia toda).")]
    [SerializeField, Min(0.05f)] float contactCooldown = 0.7f;
    [SerializeField, Min(0f)] float contactKnockback = 6f;

    [Header("Caça")]
    [Tooltip("Quanto a cabeça serpenteia para os lados ao se aproximar (metros).")]
    [SerializeField, Min(0f)] float weaveAmplitude = 2.5f;
    [SerializeField, Min(0f)] float weaveFrequency = 1.6f;
    [Tooltip("Segundos parado depois de virar cabeça (a centopeia partiu).")]
    [SerializeField, Min(0f)] float startleTime = 0.45f;

    [Header("Visual")]
    [Tooltip("Body externo do prefab (pai do modelo): ergue no aviso da mordida.")]
    [SerializeField] Transform body;
    [Tooltip("Cabeça do modelo (olhos, antenas, presas): só aparece no segmento que lidera.")]
    [SerializeField] Transform headPart;
    [Tooltip("Ponta do rabo (cercos): só aparece no último segmento.")]
    [SerializeField] Transform tailPart;

    /// <summary>Segmento da frente (null = este é a cabeça).</summary>
    public Centipede Leader { get; private set; }
    /// <summary>Segmento de trás (null = este é o último).</summary>
    public Centipede Follower { get; private set; }
    public bool IsHead => isHead;
    public int Bites { get; private set; }
    public int BitesLanded { get; private set; }
    public bool IsWindingUpBite => state == State.BiteWindUp;
    /// <summary>Vezes que um segmento virou cabeça (a centopeia partiu ou perdeu a cabeça).</summary>
    public static int Promotions { get; private set; }

    /// <summary>Quantos segmentos vivos tem a corrente a partir deste (ele incluso).</summary>
    public int ChainLength
    {
        get
        {
            int n = 0;
            for (var c = this; c != null && n < 1000; c = c.Follower) n++;
            return n;
        }
    }

    public override bool CanBeElite => false; // cada segmento já é um inimigo

    // Toques valem para todas as centopeias: encostar em vários segmentos não multiplica o dano
    static float nextContactAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { nextContactAt = 0f; Promotions = 0; }

    State state;
    float stateStart, weavePhase;
    bool isHead, bodyBuilt;
    Vector3 bodyRestPos;
    Quaternion bodyRestRot;
    readonly List<Vector3> trail = new();

    float WorldSpacing => spacing * transform.lossyScale.z;

    protected override void Awake()
    {
        isHead = startsAsHead;
        UsesAgent = startsAsHead;
        base.Awake();
        if (body != null) { bodyRestPos = body.localPosition; bodyRestRot = body.localRotation; }
    }

    protected override void OnEnable()
    {
        isHead = startsAsHead;
        UsesAgent = isHead;
        Leader = null;
        Follower = null;
        trail.Clear();
        base.OnEnable();
        bodyBuilt = !isHead || bodySegments <= 0 || segmentPrefab == null;
        weavePhase = Random.value * 10f;
        state = State.Hunt;
        ResetVisuals();
        RefreshParts();
    }

    protected override void OnDisable()
    {
        // só solta: se o de trás continuar ativo, ele mesmo assume no próximo frame
        // (desligar a cena inteira não fica tentando criar agentes sem NavMesh)
        Unlink(promoteFollower: false);
        base.OnDisable();
    }

    void Enter(State s) { state = s; stateStart = Time.time; }
    float Elapsed => Time.time - stateStart;

    protected override void Update()
    {
        if (!IsAlive) return;
        if (!isHead)
        {
            // o da frente sumiu sem morrer (cena limpando inimigos): assume o comando
            if (Leader == null || !Leader.IsAlive || !Leader.isActiveAndEnabled) Promote();
            else { TouchPlayer(); return; }
        }
        if (!bodyBuilt) BuildBody();
        base.Update();
        if (!HasValidTarget && state != State.Hunt && state != State.Startled) { Enter(State.Hunt); ResetVisuals(); }
        if (state == State.Startled) UpdateStartle();
    }

    /// <summary>A cabeça move o corpo todo depois de andar (ordem fixa: da frente para trás).</summary>
    void LateUpdate()
    {
        if (!isHead || !IsAlive) return;
        RecordTrail();
        int guard = 0;
        for (var c = Follower; c != null && guard++ < 1000; c = c.Follower)
        {
            c.FollowLeader();
            c.RecordTrail();
        }
    }

    protected override void Tick()
    {
        switch (state)
        {
            case State.Hunt:
                float d = FlatDistanceToTarget();
                if (d <= biteRange * transform.lossyScale.z)
                {
                    StopMoving();
                    if (FaceTarget() > 0.6f) { Bites++; Enter(State.BiteWindUp); }
                    return;
                }
                Vector3 to = Target.position - transform.position; to.y = 0f;
                Vector3 side = Vector3.Cross(Vector3.up, to.normalized);
                float amp = weaveAmplitude * Mathf.Clamp01((d - 3f) / 8f);
                MoveTo(Target.position + side * (Mathf.Sin((Time.time + weavePhase) * weaveFrequency) * amp));
                break;

            case State.BiteWindUp:
                FaceTarget();
                float k = Mathf.Clamp01(Elapsed / biteWindUp);
                RearUp(k);
                if (k >= 1f) { Bite(); Enter(State.BiteRecover); }
                break;

            case State.BiteRecover:
                RearUp(1f - Mathf.Clamp01(Elapsed / 0.15f));
                if (Elapsed >= biteRecover) Enter(State.Hunt);
                break;
        }
    }

    void Bite()
    {
        if (TargetHealth == null || !TargetInFront((biteRange + 0.6f) * transform.lossyScale.z, 0.25f)) return;
        Vector3 dir = (Target.position - transform.position).normalized;
        TargetHealth.TakeDamage(new DamageInfo(biteDamage * DamageMultiplier, gameObject, Target.position + Vector3.up, dir));
        BitesLanded++;
    }

    // ---------- Corrente ----------

    /// <summary>Monta o corpo atrás da cabeça (primeiro frame em jogo: o pool não monta no prewarm).</summary>
    void BuildBody()
    {
        bodyBuilt = true;
        var front = this;
        Vector3 back = -transform.forward;
        for (int i = 0; i < bodySegments; i++)
        {
            Vector3 pos = transform.position + back * (WorldSpacing * (i + 1));
            var seg = Instantiate(segmentPrefab, pos, transform.rotation);
            seg.name = $"{segmentPrefab.name} {i + 1}";
            seg.SetBaseScale(transform.localScale);
            seg.ApplyScaling(HealthMultiplier, DamageMultiplier);
            seg.SpeedMultiplier = SpeedMultiplier;
            seg.Leader = front;
            front.Follower = seg;
            seg.trail.Clear();
            // pernas em onda: cada segmento um pouco adiantado em relação ao de trás
            if (seg.TryGetComponent(out ProceduralInsectAnimator anim)) anim.OffsetGait(-(i + 1) * 0.9f);
            front = seg;
        }
        for (var c = this; c != null; c = c.Follower) c.RefreshParts();
    }

    void RecordTrail()
    {
        Vector3 p = transform.position;
        if (trail.Count > 0 && (p - trail[^1]).sqrMagnitude < 0.0016f) return;
        trail.Add(p);
        if (trail.Count > 96) trail.RemoveRange(0, trail.Count - 96);
    }

    /// <summary>Ponto `distance` metros para trás no caminho que este segmento fez.</summary>
    Vector3 PointBehind(float distance)
    {
        Vector3 prev = transform.position;
        float remaining = distance;
        for (int i = trail.Count - 1; i >= 0; i--)
        {
            Vector3 p = trail[i];
            float seg = Vector3.Distance(prev, p);
            if (seg >= remaining && seg > 0.0001f) return Vector3.Lerp(prev, p, remaining / seg);
            remaining -= seg;
            prev = p;
        }
        // caminho curto (acabou de nascer): continua reto para trás
        Vector3 back = -transform.forward; back.y = 0f;
        return prev + (back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.back) * remaining;
    }

    void FollowLeader()
    {
        if (Leader == null || !IsAlive) return;
        Vector3 target = Leader.PointBehind(WorldSpacing);
        Vector3 look = Leader.transform.position - target; look.y = 0f;
        transform.position = target;
        if (look.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(look);
    }

    /// <summary>Vira cabeça: liga o agente, mostra a cabeça e para um instante antes de caçar.</summary>
    void Promote()
    {
        if (Leader != null && Leader.Follower == this) Leader.Follower = null;
        Leader = null;
        if (isHead || !IsAlive) return;
        isHead = true;
        bodyBuilt = true;
        UsesAgent = true;
        if (isActiveAndEnabled && NavMesh.SamplePosition(transform.position, out var hit, 3f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            Agent.enabled = true;
            Agent.Warp(hit.position);
            Agent.speed = moveSpeed * TotalSpeedMultiplier;
        }
        Promotions++;
        RefreshParts();
        Enter(State.Startled);
    }

    void UpdateStartle()
    {
        if (body != null)
        {
            float shake = Mathf.Sin(Time.time * 50f) * 3f * (1f - Mathf.Clamp01(Elapsed / startleTime));
            body.localRotation = bodyRestRot * Quaternion.Euler(-12f, shake, 0f);
        }
        if (Agent.isOnNavMesh) StopMoving();
        if (Elapsed < startleTime) return;
        ResetVisuals();
        Enter(State.Hunt);
    }

    void Unlink(bool promoteFollower = true)
    {
        var front = Leader;
        var back = Follower;
        Leader = null;
        Follower = null;
        if (front != null && front.Follower == this) { front.Follower = null; front.RefreshParts(); }
        if (back != null && back.Leader == this)
        {
            if (promoteFollower) back.Promote();
            else back.Leader = null;
        }
    }

    void TouchPlayer()
    {
        if (contactDamage <= 0f || Time.time < nextContactAt || Target == null || TargetHealth == null || !TargetHealth.IsAlive) return;
        Vector3 d = Target.position - transform.position;
        if (Mathf.Abs(d.y) > 1.6f) return;
        d.y = 0f;
        if (d.magnitude > contactRadius * transform.lossyScale.z + 0.45f) return;
        if (Target.TryGetComponent(out PlayerMovement pm) && pm.IsFloating) return; // bolha de sabão: passa por cima
        nextContactAt = Time.time + contactCooldown;
        Vector3 dir = d.sqrMagnitude > 0.0001f ? d.normalized : transform.right;
        TargetHealth.TakeDamage(new DamageInfo(contactDamage * DamageMultiplier, gameObject, Target.position + Vector3.up, dir));
        if (pm != null) pm.AddKnockback(dir * contactKnockback + Vector3.up * 2.5f);
    }

    // ---------- Visual ----------

    void RefreshParts()
    {
        if (headPart != null) headPart.gameObject.SetActive(isHead);
        if (tailPart != null) tailPart.gameObject.SetActive(Follower == null);
    }

    void RearUp(float k)
    {
        if (body == null) return;
        float shake = k > 0.5f ? Mathf.Sin(Time.time * 60f) * 2f : 0f;
        body.localPosition = bodyRestPos + Vector3.up * (0.12f * k);
        body.localRotation = bodyRestRot * Quaternion.Euler(-22f * k + shake, 0f, 0f);
    }

    void ResetVisuals()
    {
        if (body == null) return;
        body.localPosition = bodyRestPos;
        body.localRotation = bodyRestRot;
    }

    protected override void HandleDeath(DamageInfo killingBlow)
    {
        Unlink();
        state = State.Hunt;
        ResetVisuals();
        RefreshParts();
        base.HandleDeath(killingBlow);
    }

#if UNITY_EDITOR
    public void EditorSetup(bool asHead, int segments, Centipede segment, Transform bodyT, Transform headT, Transform tailT)
    {
        startsAsHead = asHead; bodySegments = segments; segmentPrefab = segment; body = bodyT; headPart = headT; tailPart = tailT;
    }
#endif
}
