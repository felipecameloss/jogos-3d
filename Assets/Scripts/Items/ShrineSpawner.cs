using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Espalha santuários aleatoriamente pelo mapa no início da run (NavMesh alcançável,
/// longe do cano de saída e dos outros santuários/baús).
/// </summary>
public class ShrineSpawner : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public Shrine prefab;
        [Min(0f)] public float weight = 1f;
    }

    [SerializeField] List<Entry> shrines = new();
    [SerializeField, Min(0)] int initialCount = 5;
    [SerializeField] Vector3 areaCenter = Vector3.zero;
    [SerializeField] Vector2 areaSize = new(96f, 96f);
    [SerializeField, Min(0f)] float minSpacing = 12f;
    [SerializeField, Min(0f)] float minDistanceFromPlayer = 8f;

    public IReadOnlyList<Shrine> Spawned => spawned;

    readonly List<Shrine> spawned = new();
    Transform player;
    NavMeshPath path;

    void Start()
    {
        path = new NavMeshPath();
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        for (int i = 0; i < initialCount; i++) TrySpawn();
    }

    public Shrine TrySpawn()
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
            var shrine = Instantiate(prefab, pos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f), transform);
            spawned.Add(shrine);
            return shrine;
        }
        return null;
    }

    bool TooClose(Vector3 pos)
    {
        var pipe = ExitPipe.Instance;
        if (pipe != null)
        {
            float keep = pipe.ZoneRadius + 1.5f;
            if ((pipe.transform.position - pos).sqrMagnitude < keep * keep) return true;
        }
        foreach (var s in spawned)
            if (s != null && (s.transform.position - pos).sqrMagnitude < minSpacing * minSpacing) return true;
        foreach (var i in Interactable.All)
            if (i != null && (i.transform.position - pos).sqrMagnitude < 5f * 5f) return true;
        return false;
    }

    Shrine PickPrefab()
    {
        float total = 0f;
        foreach (var e in shrines) if (e.prefab != null) total += e.weight;
        if (total <= 0f) return null;
        float roll = UnityEngine.Random.value * total;
        foreach (var e in shrines)
        {
            if (e.prefab == null) continue;
            roll -= e.weight;
            if (roll <= 0f) return e.prefab;
        }
        return shrines[^1].prefab;
    }
}
