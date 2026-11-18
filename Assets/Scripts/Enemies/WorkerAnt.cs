using UnityEngine;

/// <summary>
/// Formiga operária: persegue o player e morde.
/// A mordida tem aviso (a formiga recua o corpo antes) e só acerta se o player
/// continuar perto e na frente quando ela fecha a mandíbula. Dá para desviar.
/// Base das outras formigas corpo a corpo (ex.: formiga-soldado): os valores vêm
/// do Inspector e os métodos On* deixam a subclasse reagir ao aviso e à mordida.
/// </summary>
public class WorkerAnt : EnemyBase
{
    [Header("Mordida")]
    [SerializeField, Min(0f)] float damage = 8f;
    [Tooltip("Distância (centro a centro, no plano) para começar a morder.")]
    [SerializeField, Min(0.1f)] float attackRange = 2.2f;
    [Tooltip("Folga extra no momento do golpe: andar só um pouco pra trás não salva.")]
    [SerializeField, Min(0f)] float hitRangeBonus = 0.65f;
    [Tooltip("Aviso antes da mordida.")]
    [SerializeField, Min(0f)] float windUpTime = 0.28f;
    [Tooltip("Parada depois da mordida (janela para o player bater).")]
    [SerializeField, Min(0f)] float recoverTime = 0.35f;
    [Tooltip("Avanço rápido no NavMesh ao fechar a mordida para pressionar o jogador.")]
    [SerializeField, Min(0f)] float biteStepDistance = 0.45f;

    [Header("Visual")]
    [Tooltip("Corpo que inclina no aviso e avança na mordida. Vazio = sem animação.")]
    [SerializeField] protected Transform body;
    [Tooltip("Graus que o corpo empina no aviso.")]
    [SerializeField] float windUpTilt = 20f;
    [Tooltip("Quanto o corpo recua no aviso.")]
    [SerializeField] float windUpRecoil = 0.25f;
    [Tooltip("Quanto o corpo avança na mordida.")]
    [SerializeField] float lungeDistance = 0.35f;

    enum State { Chase, WindUp, Recover }
    State state;
    float stateTime;
    protected Vector3 bodyRestPos;
    protected Quaternion bodyRestRot;

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
                    if (FaceTarget() > 0.65f) SetState(State.WindUp);
                }
                else
                {
                    MoveTo(Target.position);
                }
                break;

            case State.WindUp:
                FaceTarget();
                float progress = Mathf.Clamp01(stateTime / Mathf.Max(windUpTime, 0.01f));
                AnimateBody(windUp: progress, lunge: 0f);
                OnWindUp(progress);
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
        OnWindUp(0f);
        if (biteStepDistance > 0f && Agent != null && Agent.enabled && Agent.isOnNavMesh)
            Agent.Move(transform.forward * biteStepDistance);
        if (TargetHealth == null || !TargetInFront(attackRange + hitRangeBonus)) return;
        Vector3 dir = (Target.position - transform.position).normalized;
        TargetHealth.TakeDamage(new DamageInfo(damage * DamageMultiplier, gameObject, Target.position + Vector3.up, dir));
        BitesLanded++;
        OnBiteLanded(dir);
    }

    void SetState(State s)
    {
        if (state == State.WindUp && s != State.WindUp) OnWindUp(0f); // aviso interrompido
        state = s;
        stateTime = 0f;
        if (s == State.Chase) AnimateBody(0f, 0f);
    }

    void AnimateBody(float windUp, float lunge)
    {
        if (body == null) return;
        // Aviso: empina a cabeça e recua. Mordida: avança.
        body.localRotation = bodyRestRot * Quaternion.Euler(-windUpTilt * windUp, 0f, 0f);
        body.localPosition = bodyRestPos + Vector3.forward * (-windUpRecoil * windUp + lungeDistance * lunge);
    }

    /// <summary>A cada frame do aviso (0..1); 0 quando o aviso termina ou é interrompido.</summary>
    protected virtual void OnWindUp(float progress) { }

    /// <summary>A mordida acertou o alvo (dir = da formiga para o alvo).</summary>
    protected virtual void OnBiteLanded(Vector3 direction) { }

    protected override void HandleDeath(DamageInfo killingBlow)
    {
        SetState(State.Chase);
        base.HandleDeath(killingBlow);
    }
}
