using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Espalha baús pelo mapa no começo da run e coloca mais de tempos em tempos.
/// Só em pontos do NavMesh com caminho até o player (nada em cima de caixa isolada),
/// longe uns dos outros e um pouco longe do spawn.
/// </summary>
public class ChestSpawner : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public Chest prefab;
        [Min(0f)] public float weight = 1f;
    }

    [SerializeField] List<Entry> chests = new();

    [Header("Quantidade")]
    [SerializeField, Min(0)] int initialCount = 10;
    [Tooltip("Coloca mais um baú a cada X segundos de run (0 = nunca).")]
    [SerializeField, Min(0f)] float extraEvery = 90f;
    [Tooltip("Não passa disso de baús fechados ao mesmo tempo.")]
    [SerializeField, Min(1)] int maxClosed = 12;

    [Header("Onde")]
    [SerializeField] Vector3 areaCenter = Vector3.zero;
    [SerializeField] Vector2 areaSize = new(90f, 90f);
    [SerializeField, Min(0f)] float minSpacing = 8f;
    [SerializeField, Min(0f)] float minDistanceFromPlayer = 6f;

    public IReadOnlyList<Chest> Spawned => spawned;
    public int ClosedCount { get { int n = 0; foreach (var c in spawned) if (c != null && !c.IsOpen) n++; return n; } }

    readonly List<Chest> spawned = new();
    Transform player;
    NavMeshPath path;
    float nextExtra;

    void Start()
    {
        path = new NavMeshPath();
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        for (int i = 0; i < initialCount; i++) TrySpawn();
        nextExtra = Time.time + extraEvery;
    }

    void Update()
    {
        if (extraEvery <= 0f || Time.time < nextExtra) return;
        nextExtra = Time.time + extraEvery;
        if (RunManager.Instance != null && RunManager.Instance.IsRunOver) return;
        if (ClosedCount < maxClosed) TrySpawn();
    }

    public Chest TrySpawn()
    {
        var prefab = PickPrefab();
        if (prefab == null) return null;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            var candidate = areaCenter + new Vector3(
                UnityEngine.Random.Range(-areaSize.x, areaSize.x) * 0.5f, 0f,
                UnityEngine.Random.Range(-areaSize.y, areaSize.y) * 0.5f);
            if (!NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas)) continue;
            Vector3 pos = hit.position;
            if (player != null)
            {
                if ((pos - player.position).sqrMagnitude < minDistanceFromPlayer * minDistanceFromPlayer) continue;
                if (!NavMesh.CalculatePath(pos, player.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            }
            if (TooClose(pos)) continue;
            var chest = Instantiate(prefab, pos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), transform);
            spawned.Add(chest);
            return chest;
        }
        return null;
    }

    bool TooClose(Vector3 pos)
    {
        foreach (var c in spawned)
            if (c != null && (c.transform.position - pos).sqrMagnitude < minSpacing * minSpacing) return true;
        return false;
    }

    Chest PickPrefab()
    {
        float total = 0f;
        foreach (var e in chests) if (e.prefab != null) total += e.weight;
        if (total <= 0f) return null;
        float roll = UnityEngine.Random.value * total;
        foreach (var e in chests)
        {
            if (e.prefab == null) continue;
            roll -= e.weight;
            if (roll <= 0f) return e.prefab;
        }
        return chests[^1].prefab;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawWireCube(areaCenter, new Vector3(areaSize.x, 1f, areaSize.y));
    }
}
