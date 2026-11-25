using System;
using UnityEngine;

/// <summary>
/// Decide quando e onde o humano pisa. Fica mais frequente com a dificuldade.
/// Na maior parte das vezes mira onde o player vai estar; às vezes pisa perto, ao acaso.
/// </summary>
public class StompDirector : MonoBehaviour
{
    [SerializeField] HumanStomp stompPrefab;

    [Header("Frequência")]
    [Tooltip("Primeiro pisão depois desse tempo de run.")]
    [SerializeField, Min(0f)] float firstStompDelay = 12f;
    [Tooltip("Intervalo entre pisões (mín, máx) com dificuldade 1.")]
    [SerializeField] Vector2 interval = new(11f, 18f);
    [Tooltip("Quanto mais frequente fica por ponto de dificuldade acima de 1.")]
    [SerializeField, Min(0f)] float frequencyPerPoint = 0.45f;
    [SerializeField, Min(0.5f)] float minInterval = 3.2f;

    [Header("Mira")]
    [Tooltip("Chance de mirar no player (senão, ponto aleatório perto dele).")]
    [SerializeField, Range(0f, 1f)] float aimAtPlayerChance = 0.6f;
    [Tooltip("Antecipa o movimento do player por esse tempo.")]
    [SerializeField, Min(0f)] float leadTime = 0.6f;
    [SerializeField, Min(0f)] float randomRadius = 10f;

    /// <summary>Um pisão começou (aviso apareceu).</summary>
    public event Action<HumanStomp> OnStompStarted;
    public int StompCount { get; private set; }
    public float NextStompTime => nextStomp;

    ComponentPool<HumanStomp> pool;
    Transform player;
    PlayerMovement playerMovement;
    float nextStomp;
    static readonly RaycastHit[] groundHits = new RaycastHit[16];

    void Awake()
    {
        if (stompPrefab != null) pool = new ComponentPool<HumanStomp>(stompPrefab, 2, maxSize: 8, rootName: "[Pool] HumanStomp");
        else Debug.LogError("StompDirector sem prefab.", this);
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { player = p.transform; playerMovement = p.GetComponent<PlayerMovement>(); }
        nextStomp = Time.time + firstStompDelay;
    }

    /// <summary>Intervalo médio atual (diminui com a dificuldade).</summary>
    public float CurrentInterval(float coeff) =>
        Mathf.Max(minInterval, (interval.x + interval.y) * 0.5f / (1f + frequencyPerPoint * (coeff - 1f)));

    void Update()
    {
        if (pool == null || player == null || (RunManager.Instance != null && RunManager.Instance.IsRunOver)) return;
        if (Time.time < nextStomp) return;

        float coeff = DifficultyManager.Instance != null ? DifficultyManager.Instance.Coefficient : 1f;
        float div = 1f + frequencyPerPoint * (coeff - 1f);
        nextStomp = Time.time + Mathf.Max(minInterval, UnityEngine.Random.Range(interval.x, interval.y) / div);
        StompNear(player.position);
    }

    /// <summary>Pisa perto de um ponto (público para testes e eventos).</summary>
    public HumanStomp StompNear(Vector3 around)
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
        return StompAt(target, UnityEngine.Random.Range(0f, 360f));
    }

    public HumanStomp StompAt(Vector3 target, float yaw)
    {
        if (pool == null || !FindGround(target, out Vector3 ground)) return null;
        var stomp = pool.Get();
        stomp.Begin(ground, yaw, pool.Release);
        StompCount++;
        OnStompStarted?.Invoke(stomp);
        return stomp;
    }

    /// <summary>
    /// Primeira superfície fixa embaixo do ponto (ignora inimigos, o player e bonecos de treino).
    /// Compartilhada com outros perigos que caem do céu (gotas de chuva do RainEvent).
    /// </summary>
    public static bool FindGround(Vector3 target, out Vector3 ground)
    {
        int n = Physics.RaycastNonAlloc(target + Vector3.up * 50f, Vector3.down, groundHits, 120f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.PositiveInfinity;
        ground = default;
        for (int i = 0; i < n; i++)
        {
            var c = groundHits[i].collider;
            if (c.GetComponentInParent<EnemyBase>() != null || c.GetComponentInParent<PlayerMovement>() != null || c.GetComponentInParent<TrainingDummy>() != null) continue;
            if (groundHits[i].distance < best) { best = groundHits[i].distance; ground = groundHits[i].point; }
        }
        return best < float.PositiveInfinity;
    }
}
