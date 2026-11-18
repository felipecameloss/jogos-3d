using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Pulgão: inofensivo, foge do player quando chega perto ou leva dano, e solta muitas migalhas ao morrer.
/// </summary>
public class Aphid : EnemyBase
{
    [Header("Fuga")]
    [Tooltip("Distância em que o pulgão percebe o player e começa a fugir.")]
    [SerializeField, Min(1f)] float fleeRange = 20f;
    [Tooltip("Só para de fugir depois de passar dessa distância.")]
    [SerializeField, Min(1f)] float safeDistance = 28f;
    [Tooltip("Passo à frente ao escolher um ponto de fuga no NavMesh.")]
    [SerializeField, Min(1f)] float fleeStepDistance = 8f;
    [SerializeField, Min(0.05f)] float repathInterval = 0.15f;
    [Tooltip("Tempo correndo depois de levar dano, mesmo se o player estiver longe.")]
    [SerializeField, Min(0f)] float panicOnHitDuration = 4f;

    [Header("Passeio (quando longe)")]
    [SerializeField, Min(0f)] float wanderRadius = 5f;
    [SerializeField, Min(0.5f)] float wanderInterval = 2.5f;

    [Header("Visual")]
    [SerializeField] Transform body;
    [SerializeField] float waddleAngle = 6f;
    [SerializeField] float bobHeight = 0.06f;

    static readonly float[] EscapeAngles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f };

    public bool IsFleeing { get; private set; }

    Vector3 bodyRestPos;
    Quaternion bodyRestRot;
    float nextRepath;
    float nextWander;
    float panicUntil;

    protected override void Awake()
    {
        base.Awake();
        if (body != null) { bodyRestPos = body.localPosition; bodyRestRot = body.localRotation; }
        Health.OnDamaged += HandleDamaged;
    }

    protected override void OnDestroy()
    {
        if (Health != null) Health.OnDamaged -= HandleDamaged;
        base.OnDestroy();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        IsFleeing = false;
        panicUntil = float.NegativeInfinity;
        nextRepath = 0f;
        nextWander = Time.time + Random.Range(0.5f, wanderInterval);
        ResetBodyPose();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        IsFleeing = false;
        ResetBodyPose();
    }

    void HandleDamaged(DamageInfo info)
    {
        if (!IsAlive) return;
        panicUntil = Time.time + panicOnHitDuration;
        nextRepath = 0f;
    }

    protected override void Tick()
    {
        float dist = FlatDistanceToTarget();
        bool shouldFlee = Time.time < panicUntil || (IsFleeing ? dist <= safeDistance : dist <= fleeRange);

        if (shouldFlee)
        {
            if (!IsFleeing)
            {
                IsFleeing = true;
                nextRepath = 0f;
            }
            if (Time.time >= nextRepath)
            {
                nextRepath = Time.time + repathInterval;
                if (TryFindFleeDestination(out Vector3 dest)) MoveTo(dest);
            }
            AnimateWaddle(speed: 18f);
        }
        else
        {
            Agent.speed *= 0.45f;
            if (IsFleeing)
            {
                IsFleeing = false;
                StopMoving();
                ResetBodyPose();
                nextWander = Time.time + Random.Range(0.5f, wanderInterval);
            }
            if (Time.time >= nextWander)
            {
                nextWander = Time.time + wanderInterval + Random.Range(0f, 1.5f);
                WanderNearby();
            }
            if (Agent.hasPath && Agent.remainingDistance > 0.2f) AnimateWaddle(speed: 9f);
            else ResetBodyPose();
        }
    }

    bool TryFindFleeDestination(out Vector3 destination)
    {
        Vector3 away = transform.position - Target.position;
        away.y = 0f;
        away = away.sqrMagnitude > 0.001f ? away.normalized : transform.forward;

        Vector3 bestPoint = transform.position;
        float bestDistSq = -1f;

        foreach (float angle in EscapeAngles)
        {
            Vector3 dir = angle == 0f ? away : Quaternion.Euler(0f, angle, 0f) * away;
            Vector3 candidate = transform.position + dir * fleeStepDistance;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3.5f, NavMesh.AllAreas)) continue;

            float dSq = (hit.position - Target.position).sqrMagnitude;
            if (dSq > bestDistSq && (hit.position - transform.position).sqrMagnitude > 1f)
            {
                bestDistSq = dSq;
                bestPoint = hit.position;
            }
        }

        destination = bestPoint;
        return bestDistSq > 0f;
    }

    void WanderNearby()
    {
        if (wanderRadius <= 0f) return;
        Vector2 r = Random.insideUnitCircle * wanderRadius;
        Vector3 candidate = transform.position + new Vector3(r.x, 0f, r.y);
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) MoveTo(hit.position);
    }

    void AnimateWaddle(float speed)
    {
        if (body == null) return;
        float wave = Mathf.Sin(Time.time * speed);
        body.localPosition = bodyRestPos + Vector3.up * (Mathf.Abs(wave) * bobHeight);
        body.localRotation = bodyRestRot * Quaternion.Euler(0f, 0f, wave * waddleAngle);
    }

    void ResetBodyPose()
    {
        if (body == null) return;
        body.localPosition = bodyRestPos;
        body.localRotation = bodyRestRot;
    }

    protected override void HandleDeath(DamageInfo killingBlow)
    {
        IsFleeing = false;
        ResetBodyPose();
        base.HandleDeath(killingBlow);
    }
}
