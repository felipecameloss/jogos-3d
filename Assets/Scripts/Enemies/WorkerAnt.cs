using UnityEngine;

/// <summary>
/// Formiga operária: persegue o player e morde.
/// A mordida tem aviso (a formiga recua o corpo antes) e só acerta se o player
/// continuar perto e na frente quando ela fecha a mandíbula. Dá para desviar.
/// </summary>
public class WorkerAnt : EnemyBase
{
    [Header("Mordida")]
    [SerializeField, Min(0f)] float damage = 8f;
    [Tooltip("Distância (centro a centro, no plano) para começar a morder.")]
    [SerializeField, Min(0.1f)] float attackRange = 1.9f;
    [Tooltip("Folga extra no momento do golpe: andar só um pouco pra trás não salva.")]
    [SerializeField, Min(0f)] float hitRangeBonus = 0.5f;
    [Tooltip("Aviso antes da mordida.")]
    [SerializeField, Min(0f)] float windUpTime = 0.45f;
    [Tooltip("Parada depois da mordida (janela para o player bater).")]
    [SerializeField, Min(0f)] float recoverTime = 0.8f;

    [Header("Visual")]
    [Tooltip("Corpo que inclina no aviso e avança na mordida. Vazio = sem animação.")]
    [SerializeField] Transform body;

    enum State { Chase, WindUp, Recover }
    State state;
    float stateTime;
    Vector3 bodyRestPos;
    Quaternion bodyRestRot;

    /// <summary>Para testes/debug: quantas mordidas acertaram.</summary>
    public int BitesLanded { get; private set; }
    public bool IsWindingUp => state == State.WindUp;

    protected override void Awake()
    {
        base.Awake();
        if (body != null) { bodyRestPos = body.localPosition; bodyRestRot = body.localRotation; }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SetState(State.Chase);
    }

    protected override void Tick()
    {
        stateTime += Time.deltaTime;
        switch (state)
        {
            case State.Chase:
                if (FlatDistanceToTarget() <= attackRange)
                {
                    StopMoving();
                    if (FaceTarget() > 0.8f) SetState(State.WindUp);
                }
                else
                {
                    MoveTo(Target.position);
                }
                break;

            case State.WindUp:
                FaceTarget();
                AnimateBody(windUp: Mathf.Clamp01(stateTime / Mathf.Max(windUpTime, 0.01f)), lunge: 0f);
                if (stateTime >= windUpTime)
                {
                    Bite();
                    SetState(State.Recover);
                }
                break;

            case State.Recover:
                AnimateBody(windUp: 0f, lunge: 1f - Mathf.Clamp01(stateTime / 0.15f));
                if (stateTime >= recoverTime) SetState(State.Chase);
                break;
        }
    }

    protected override void Update()
    {
        base.Update();
        // Alvo morreu/run acabou no meio do ataque: volta o corpo ao normal
        if (!HasValidTarget && state != State.Chase) SetState(State.Chase);
    }

    void Bite()
    {
        if (TargetHealth == null || !TargetInFront(attackRange + hitRangeBonus)) return;
        Vector3 dir = (Target.position - transform.position).normalized;
        TargetHealth.TakeDamage(new DamageInfo(damage * DamageMultiplier, gameObject, Target.position + Vector3.up, dir));
        BitesLanded++;
    }

    void SetState(State s)
    {
        state = s;
        stateTime = 0f;
        if (s == State.Chase) AnimateBody(0f, 0f);
    }

    void AnimateBody(float windUp, float lunge)
    {
        if (body == null) return;
        // Aviso: empina a cabeça e recua. Mordida: avança.
        body.localRotation = bodyRestRot * Quaternion.Euler(-20f * windUp, 0f, 0f);
        body.localPosition = bodyRestPos + Vector3.forward * (-0.25f * windUp + 0.35f * lunge);
    }

    protected override void HandleDeath(DamageInfo killingBlow)
    {
        SetState(State.Chase);
        base.HandleDeath(killingBlow);
    }
}
