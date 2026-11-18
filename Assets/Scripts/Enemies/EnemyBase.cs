using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Base de todo inimigo: vida, NavMeshAgent, achar o player, morrer e sumir.
/// Cada inimigo herda e implementa só o comportamento em Tick().
/// Funciona colocado na cena ou vindo de um pool (OnEnable reinicia tudo).
/// </summary>
[RequireComponent(typeof(Health), typeof(NavMeshAgent))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("Base")]
    [SerializeField, Min(0f)] protected float moveSpeed = 7.2f;
    [Tooltip("Graus por segundo para virar na direção do alvo quando parado.")]
    [SerializeField, Min(0f)] protected float turnSpeed = 720f;
    [Tooltip("Tempo entre morrer e sumir (animação de morte).")]
    [SerializeField, Min(0f)] float despawnDelay = 1.2f;
    [Tooltip("Migalhas que solta ao morrer com dificuldade 1 (cresce com a dificuldade).")]
    [SerializeField, Min(0)] int crumbReward = 3;

    public int CrumbReward => crumbReward;

    /// <summary>Qualquer inimigo morreu (drop de migalhas, itens "ao matar", contador de kills).</summary>
    public static event Action<EnemyBase, DamageInfo> OnAnyEnemyDied;
    /// <summary>
    /// Pronto para sumir. Se alguém (o spawner/pool) escutar, ele cuida de devolver o objeto;
    /// senão o inimigo se destrói.
    /// </summary>
    public event Action<EnemyBase> OnDespawn;

    /// <summary>Multiplicadores para a escala de dificuldade (task do spawner).</summary>
    [HideInInspector] public float DamageMultiplier = 1f;
    [HideInInspector] public float SpeedMultiplier = 1f;
    /// <summary>Lentidão de efeitos de status (chiclete, cola). Controlado por StatusEffects.</summary>
    [HideInInspector] public float StatusSpeedMultiplier = 1f;

    /// <summary>Inimigos ativos na cena (vivos ou morrendo). Para itens acharem alvos sem FindObjects.</summary>
    public static readonly List<EnemyBase> Active = new();

    /// <summary>Inimigo vivo mais perto de um ponto (ignora `except`). Null se não houver dentro do raio.</summary>
    public static EnemyBase Nearest(Vector3 point, float maxDistance, ICollection<EnemyBase> except = null)
    {
        EnemyBase best = null;
        float bestSq = maxDistance * maxDistance;
        foreach (var e in Active)
        {
            if (e == null || !e.IsAlive || (except != null && except.Contains(e))) continue;
            float sq = (e.transform.position - point).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = e; }
        }
        return best;
    }

    /// <summary>Centro do corpo (para mirar raios e linhas).</summary>
    public Vector3 Center
    {
        get
        {
            var c = GetComponent<Collider>();
            return c != null && c.enabled ? c.bounds.center : transform.position + Vector3.up * 0.6f;
        }
    }

    /// <summary>Chefe: não morre de uma vez para perigos que matam na hora (pisão).</summary>
    public virtual bool IsBoss => false;

    public Health Health { get; private set; }
    public bool IsAlive => Health != null && Health.IsAlive;

    protected NavMeshAgent Agent { get; private set; }
    protected Transform Target { get; private set; }
    protected Health TargetHealth { get; private set; }

    Vector3 initialScale;
    float baseMaxHealth;
    Coroutine despawnRoutine;

    protected virtual void Awake()
    {
        Health = GetComponent<Health>();
        Agent = GetComponent<NavMeshAgent>();
        initialScale = transform.localScale;
        baseMaxHealth = Health.Max;
        Health.OnDeath += HandleDeath;
    }

    /// <summary>Escala de dificuldade: aplicada pelo spawner logo depois de nascer. Volta com vida cheia.</summary>
    public void ApplyScaling(float healthMultiplier, float damageMultiplier)
    {
        Health.SetMaxHealth(baseMaxHealth * Mathf.Max(0.01f, healthMultiplier), keepRatio: false);
        Health.ResetHealth();
        DamageMultiplier = Mathf.Max(0f, damageMultiplier);
    }

    protected virtual void OnEnable()
    {
        // Reinício para o pool
        if (despawnRoutine != null) { StopCoroutine(despawnRoutine); despawnRoutine = null; }
        transform.localScale = initialScale;
        Health.ResetHealth();
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = true;
        Agent.enabled = true;
        SpeedMultiplier = 1f;
        StatusSpeedMultiplier = 1f;
        Agent.speed = moveSpeed * SpeedMultiplier;
        if (!Active.Contains(this)) Active.Add(this);
        if (Agent.isOnNavMesh) Agent.isStopped = false;
        FindTarget();
    }

    protected virtual void OnDisable()
    {
        Active.Remove(this);
    }

    protected virtual void OnDestroy()
    {
        Active.Remove(this);
    }

    protected virtual void Update()
    {
        if (!IsAlive) return;
        Agent.speed = moveSpeed * SpeedMultiplier * StatusSpeedMultiplier;
        if (Target == null) FindTarget();
        if (!HasValidTarget)
        {
            StopMoving();
            return;
        }
        Tick();
    }

    /// <summary>Comportamento do inimigo, chamado a cada frame enquanto vivo e com alvo vivo.</summary>
    protected abstract void Tick();

    protected bool HasValidTarget =>
        Target != null && (TargetHealth == null || TargetHealth.IsAlive) &&
        (RunManager.Instance == null || !RunManager.Instance.IsRunOver);

    void FindTarget()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        Target = player != null ? player.transform : null;
        TargetHealth = player != null ? player.GetComponent<Health>() : null;
    }

    // ---------- Ajudantes para as subclasses ----------

    /// <summary>Distância no plano (ignora altura) até o alvo.</summary>
    protected float FlatDistanceToTarget()
    {
        Vector3 d = Target.position - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    protected void MoveTo(Vector3 destination)
    {
        if (!Agent.isOnNavMesh) return;
        Agent.isStopped = false;
        Agent.SetDestination(destination);
    }

    protected void StopMoving()
    {
        if (Agent.isOnNavMesh)
        {
            Agent.isStopped = true;
            Agent.ResetPath();
        }
        Agent.velocity = Vector3.zero;
    }

    /// <summary>Gira no próprio eixo para olhar o alvo. Retorna o quão alinhado está (1 = de frente).</summary>
    protected float FaceTarget()
    {
        Vector3 d = Target.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) return 1f;
        Quaternion want = Quaternion.LookRotation(d);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
        return Vector3.Dot(transform.forward, d.normalized);
    }

    /// <summary>O alvo está à frente (dentro de um cone) e perto o bastante.</summary>
    protected bool TargetInFront(float range, float minDot = 0.3f)
    {
        Vector3 d = Target.position - transform.position;
        d.y = 0f;
        return d.magnitude <= range && Vector3.Dot(transform.forward, d.normalized) >= minDot;
    }

    // ---------- Morte ----------

    protected virtual void HandleDeath(DamageInfo killingBlow)
    {
        StopMoving();
        Agent.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false; // não bloqueia tiros nem o player
        OnAnyEnemyDied?.Invoke(this, killingBlow);
        despawnRoutine = StartCoroutine(DespawnAfterDelay());
    }

    IEnumerator DespawnAfterDelay()
    {
        // Encolhe até sumir (placeholder até ter animação de morte)
        float t = 0f;
        while (t < despawnDelay)
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.SmoothStep(0f, 1f, t / Mathf.Max(despawnDelay, 0.01f));
            transform.localScale = initialScale * Mathf.Max(k, 0.01f);
            yield return null;
        }
        despawnRoutine = null;
        if (OnDespawn != null) OnDespawn.Invoke(this);
        else Destroy(gameObject);
    }
}
