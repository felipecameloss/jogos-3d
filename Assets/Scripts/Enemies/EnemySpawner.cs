using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// "Diretor" de spawn no estilo Risk of Rain: ganha créditos com o tempo (mais rápido
/// quanto maior a dificuldade) e gasta comprando grupos de inimigos perto do player.
/// Inimigos vêm de pools e já nascem escalados pela dificuldade.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Serializable]
    public class SpawnEntry
    {
        public EnemyBase prefab;
        [Tooltip("Créditos que custa um desses.")]
        [Min(0.1f)] public float cost = 8f;
        [Tooltip("Chance relativa de ser escolhido.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Só aparece a partir desse coeficiente de dificuldade.")]
        [Min(1f)] public float minDifficulty = 1f;
        [Tooltip("Tamanho mínimo do grupo ao nascer (0 ou 1 = normal).")]
        [Min(0)] public int minGroup = 1;
        [Tooltip("Inimigos extras permitidos no máximo do grupo (bom para enxames como cupins).")]
        [Min(0)] public int extraGroupSize = 0;
        [Min(0)] public int poolPrewarm = 10;
        [NonSerialized] public ComponentPool<EnemyBase> pool;
    }

    [SerializeField] List<SpawnEntry> enemies = new();

    [Header("Elites")]
    [SerializeField, Min(1f)] float minEliteDifficulty = 1.15f;
    [SerializeField, Range(0f, 1f)] float eliteChanceBase = 0.08f;
    [SerializeField, Min(0f)] float eliteChancePerDifficulty = 0.06f;
    [SerializeField, Range(0f, 1f)] float maxEliteChance = 0.45f;

    [Header("Créditos")]
    [SerializeField, Min(0f)] float startingCredits = 36f;
    [Tooltip("Créditos por segundo com dificuldade 1 (multiplica pelo coeficiente).")]
    [SerializeField, Min(0f)] float creditsPerSecond = 1.9f;
    [Tooltip("Limite de créditos guardados (evita uma onda gigante depois de bater o máximo de vivos).")]
    [SerializeField, Min(1f)] float maxBankedCredits = 110f;

    [Header("Ondas")]
    [SerializeField, Min(0.5f)] float waveIntervalMin = 2.5f;
    [SerializeField, Min(0.5f)] float waveIntervalMax = 4.2f;
    [Tooltip("Inimigos por onda com dificuldade 1; cresce com o coeficiente.")]
    [SerializeField, Min(1)] int baseGroupSize = 4;
    [SerializeField, Min(1)] int maxAlive = 45;

    [Header("Onde nascer")]
    [SerializeField, Min(0f)] float minDistance = 18f;
    [SerializeField, Min(0f)] float maxDistance = 35f;
    [Tooltip("Espalhamento dos inimigos de um mesmo grupo.")]
    [SerializeField, Min(0f)] float groupSpread = 3f;

    /// <summary>Disparado para cada inimigo que nasce (já posicionado e escalado).</summary>
    public event Action<EnemyBase> OnEnemySpawned;

    /// <summary>Multiplica a taxa de créditos e a frequência das ondas (ex.: 2.5 durante a ativação da saída).</summary>
    [HideInInspector] public float Intensity = 1f;

    public int AliveCount => alive.Count;
    public int TotalSpawned { get; private set; }
    public float Credits => credits;
    public int PooledObjectCount { get { int n = 0; foreach (var e in enemies) if (e.pool != null) n += e.pool.CountAll; return n; } }

    readonly HashSet<EnemyBase> alive = new();
    readonly Dictionary<EnemyBase, SpawnEntry> owners = new();
    readonly List<SpawnEntry> candidates = new();
    float credits;
    float nextWaveTime;
    // Inimigo já sorteado esperando créditos: sem isso os caros nunca saem,
    // porque cada onda gasta tudo nos baratos.
    SpawnEntry pendingEntry;
    Transform player;
    NavMeshPath path;

    void Awake()
    {
        path = new NavMeshPath();
        credits = startingCredits;
        foreach (var e in enemies)
        {
            if (e.prefab == null) continue;
            e.pool = new ComponentPool<EnemyBase>(e.prefab, e.poolPrewarm, maxSize: maxAlive * 2, rootName: $"[Pool] {e.prefab.name}");
        }
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        nextWaveTime = Time.time + 1f; // primeira onda logo no começo
    }

    float Difficulty => DifficultyManager.Instance != null ? DifficultyManager.Instance.Coefficient : 1f;

    void Update()
    {
        if (player == null || (RunManager.Instance != null && RunManager.Instance.IsRunOver)) return;

        float coeff = Difficulty;
        credits = Mathf.Min(credits + creditsPerSecond * coeff * Intensity * Time.deltaTime, maxBankedCredits);

        if (Time.time < nextWaveTime) return;
        // Ondas um pouco mais frequentes com a dificuldade
        float interval = UnityEngine.Random.Range(waveIntervalMin, waveIntervalMax) / (1f + 0.15f * (coeff - 1f)) / Mathf.Max(0.1f, Intensity);
        nextWaveTime = Time.time + interval;
        SpawnWave(coeff);
    }

    void SpawnWave(float coeff)
    {
        // o que estava esperando créditos pode ter deixado de valer (amanheceu: vaga-lume não nasce)
        if (pendingEntry != null && !pendingEntry.prefab.CanSpawnNow) pendingEntry = null;
        var entry = pendingEntry ?? PickEntry(coeff);
        if (entry == null) return;
        if (entry.cost > maxBankedCredits)
        {
            Debug.LogWarning($"{entry.prefab.name} custa mais que o máximo de créditos guardados; nunca vai nascer.", this);
            pendingEntry = null;
            return;
        }
        int room = maxAlive - alive.Count;
        if (room <= 0) return;

        int requiredCount = Mathf.Min(Mathf.Max(1, entry.minGroup), room);
        float requiredCost = entry.cost * requiredCount;
        if (requiredCost > maxBankedCredits) requiredCost = entry.cost;
        if (requiredCost > credits)
        {
            pendingEntry = entry; // junta créditos para esse
            return;
        }

        int groupMax = Mathf.Max(requiredCount, baseGroupSize + Mathf.Max(0, entry.extraGroupSize) + Mathf.FloorToInt(coeff - 1f));
        int count = Mathf.Min(Mathf.FloorToInt(credits / entry.cost), groupMax, room);
        if (count <= 0) return;
        if (!TryFindSpawnPoint(out Vector3 center)) return;
        pendingEntry = null;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = center;
            if (i > 0)
            {
                Vector2 r = UnityEngine.Random.insideUnitCircle * groupSpread;
                if (NavMesh.SamplePosition(center + new Vector3(r.x, 0f, r.y), out var hit, groupSpread, NavMesh.AllAreas)) pos = hit.position;
            }
            Spawn(entry, pos, coeff);
            credits -= entry.cost;
        }
    }

    SpawnEntry PickEntry(float coeff)
    {
        candidates.Clear();
        float total = 0f;
        foreach (var e in enemies)
        {
            if (e.pool == null || e.weight <= 0f || coeff < e.minDifficulty || !e.prefab.CanSpawnNow) continue;
            candidates.Add(e);
            total += e.weight;
        }
        if (candidates.Count == 0) return null;
        float roll = UnityEngine.Random.value * total;
        foreach (var e in candidates)
        {
            roll -= e.weight;
            if (roll <= 0f) return e;
        }
        return candidates[^1];
    }

    /// <summary>Ponto no NavMesh, longe o bastante do player e com caminho até ele (não em cima de caixa isolada).</summary>
    bool TryFindSpawnPoint(out Vector3 point)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 dir = UnityEngine.Random.insideUnitCircle.normalized;
            float dist = UnityEngine.Random.Range(minDistance, maxDistance);
            Vector3 candidate = player.position + new Vector3(dir.x, 0f, dir.y) * dist;
            if (!NavMesh.SamplePosition(candidate, out var hit, 4f, NavMesh.AllAreas)) continue;
            if ((hit.position - player.position).sqrMagnitude < minDistance * minDistance * 0.8f) continue;
            if (!NavMesh.CalculatePath(hit.position, player.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            point = hit.position;
            return true;
        }
        point = default;
        return false;
    }

    void Spawn(SpawnEntry entry, Vector3 position, float coeff)
    {
        Vector3 look = player.position - position;
        look.y = 0f;
        var rot = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity;
        var enemy = entry.pool.Get(position, rot);

        var dm = DifficultyManager.Instance;
        float hpMult = dm != null ? dm.EnemyHealthMultiplier : 1f;
        float dmgMult = dm != null ? dm.EnemyDamageMultiplier : 1f;
        if (dm != null) enemy.ApplyScaling(hpMult, dmgMult);
        enemy.SpeedMultiplier = Mathf.Min(1.35f, 1f + Mathf.Max(0f, coeff - 1f) * 0.08f);

        if (!enemy.IsBoss && enemy.CanBeElite && coeff >= minEliteDifficulty)
        {
            float chance = Mathf.Min(maxEliteChance, eliteChanceBase + (coeff - minEliteDifficulty) * eliteChancePerDifficulty);
            if (UnityEngine.Random.value < chance)
            {
                var variant = UnityEngine.Random.value < 0.5f ? EnemyElite.Variant.Giant : EnemyElite.Variant.Blazing;
                EnemyElite.Apply(enemy, variant, hpMult, dmgMult);
            }
        }

        alive.Add(enemy);
        owners[enemy] = entry;
        enemy.OnDespawn += HandleDespawn;
        TotalSpawned++;
        OnEnemySpawned?.Invoke(enemy);
    }

    void HandleDespawn(EnemyBase enemy)
    {
        enemy.OnDespawn -= HandleDespawn;
        alive.Remove(enemy);
        if (owners.Remove(enemy, out var entry)) entry.pool.Release(enemy);
        else Destroy(enemy.gameObject);
    }

    void OnDrawGizmosSelected()
    {
        if (player == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(player.position, minDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(player.position, maxDistance);
    }
}
