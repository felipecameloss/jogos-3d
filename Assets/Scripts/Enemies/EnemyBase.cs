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
    /// <summary>Confuso (nuvem de pólen): não ataca e anda sem rumo. Controlado por StatusEffects.</summary>
    public bool IsConfused { get; set; }
    /// <summary>
    /// Em fúria (apito de feromônio): esquece o player e ataca o inimigo em fúria mais perto.
    /// Sem nenhum por perto, anda sem rumo procurando briga. Controlado por StatusEffects.
    /// </summary>
    public bool IsFrenzied { get; set; }
    /// <summary>Inimigo que este está atacando na fúria (null fora dela).</summary>
    public EnemyBase Rival { get; private set; }

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

    /// <summary>Formiga de verdade (o apito de feromônio só mexe com elas).</summary>
    public virtual bool IsAnt => false;

    public Health Health { get; private set; }
    public bool IsAlive => Health != null && Health.IsAlive;

    protected NavMeshAgent Agent { get; private set; }
    /// <summary>Quem o inimigo persegue e ataca: o player, ou o rival durante a fúria.</summary>
    protected Transform Target { get; private set; }
    protected Health TargetHealth { get; private set; }

    Transform player;
    Health playerHealth;
    float nextRivalSearchAt;

    Vector3 initialScale;
    float baseMaxHealth, baseAcceleration;
    Coroutine despawnRoutine;

    protected virtual void Awake()
    {
        Health = GetComponent<Health>();
        Agent = GetComponent<NavMeshAgent>();
        initialScale = transform.localScale;
        baseMaxHealth = Health.Max;
        baseAcceleration = Agent.acceleration;
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
        IsConfused = false;
        IsFrenzied = false;
        Agent.speed = moveSpeed * SpeedMultiplier * RunModifiers.EnemySpeedMultiplier;
        Agent.acceleration = baseAcceleration * RunModifiers.EnemySpeedMultiplier; // modificador "2x mais rápidos"
        if (!Active.Contains(this)) Active.Add(this);
        if (Agent.isOnNavMesh) Agent.isStopped = false;
        Rival = null;
        FindPlayer();
        TargetPlayer();
        discovered = false;
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
        Agent.speed = moveSpeed * TotalSpeedMultiplier;
        if (player == null) FindPlayer();
        if (IsFrenzied) UpdateRival();
        else if (Rival != null || Target == null) TargetPlayer();
        TrackDiscovery();
        if (!HasValidTarget)
        {
            if (IsConfused || IsFrenzied) Wander();
            else StopMoving();
            return;
        }
        Tick();
    }

    float nextWanderAt;

    /// <summary>Confuso: troca de rumo a cada pouco, para pontos aleatórios perto de onde está.</summary>
    void Wander()
    {
        if (Time.time < nextWanderAt) return;
        nextWanderAt = Time.time + UnityEngine.Random.Range(0.5f, 1f);
        Vector2 r = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(2f, 5f);
        if (NavMesh.SamplePosition(transform.position + new Vector3(r.x, 0f, r.y), out var hit, 2.5f, NavMesh.AllAreas))
            MoveTo(hit.position);
    }

    // ---------- Diário de descobertas ----------

    /// <summary>Perto assim do player, o inimigo conta como "visto" no diário.</summary>
    public const float DiscoverDistance = 30f;

    /// <summary>Id do inimigo no Diário de descobertas: o nome da classe (WorkerAnt, Spider...).</summary>
    public string JournalId => GetType().Name;

    bool discovered;

    void TrackDiscovery()
    {
        if (discovered || player == null) return;
        if ((player.position - transform.position).sqrMagnitude > DiscoverDistance * DiscoverDistance) return;
        discovered = true;
        DiscoveryJournal.DiscoverEnemy(JournalId);
    }

    /// <summary>Velocidade final: dificuldade/elite x status (lentidão) x modificador de run.</summary>
    protected float TotalSpeedMultiplier => SpeedMultiplier * StatusSpeedMultiplier * RunModifiers.EnemySpeedMultiplier;

    /// <summary>Comportamento do inimigo, chamado a cada frame enquanto vivo e com alvo vivo.</summary>
    protected abstract void Tick();

    /// <summary>
    /// Pode ir atrás do alvo: player vivo, run rolando e sem confusão. Quando vira falso, as
    /// subclasses cancelam o ataque em andamento (o mesmo caminho serve para a confusão do pólen).
    /// </summary>
    protected bool HasValidTarget =>
        Target != null && (TargetHealth == null || TargetHealth.IsAlive) &&
        (RunManager.Instance == null || !RunManager.Instance.IsRunOver) && !IsConfused;

    void FindPlayer()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        player = p != null ? p.transform : null;
        playerHealth = p != null ? p.GetComponent<Health>() : null;
    }

    void TargetPlayer()
    {
        Rival = null;
        Target = player;
        TargetHealth = playerHealth;
    }

    // ---------- Fúria (apito de feromônio) ----------

    /// <summary>
    /// Fica no mesmo rival até ele morrer ou sair da fúria (trocar no meio da mordida faria
    /// errar); aí procura o inimigo em fúria mais perto. Sem nenhum, fica sem alvo (anda sem rumo).
    /// </summary>
    void UpdateRival()
    {
        bool rivalOk = Rival != null && Rival.IsAlive && Rival.IsFrenzied && Rival.isActiveAndEnabled;
        if (rivalOk && Target == Rival.transform) return;
        if (!rivalOk && Time.time < nextRivalSearchAt && Rival == null && Target == null) return;
        nextRivalSearchAt = Time.time + 0.25f;

        EnemyBase best = null;
        float bestSq = float.MaxValue;
        foreach (var e in Active)
        {
            if (e == null || e == this || !e.IsAlive || !e.IsFrenzied || !e.isActiveAndEnabled) continue;
            float sq = (e.transform.position - transform.position).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = e; }
        }
        Rival = best;
        Target = best != null ? best.transform : null;
        TargetHealth = best != null ? best.Health : null;
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
        // Morto pelo player de longe (estilingue): também conta como visto
        if (!discovered && killingBlow.Source != null && player != null && killingBlow.Source.transform.IsChildOf(player))
        {
            discovered = true;
            DiscoveryJournal.DiscoverEnemy(JournalId);
        }
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
