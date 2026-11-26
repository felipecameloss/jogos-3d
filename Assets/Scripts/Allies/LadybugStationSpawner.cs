using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Põe a margarida das joaninhas num ponto do mapa no início da fase: NavMesh alcançável,
/// nem colada nem longe demais do player, fora do cano de saída e longe de baús/santuários.
/// </summary>
public class LadybugStationSpawner : MonoBehaviour
{
    [SerializeField] LadybugConfig config;
    [Tooltip("Fonte do preço que flutua em cima da margarida (vazio = fonte padrão do Unity).")]
    [SerializeField] Font labelFont;
    [SerializeField, Min(0)] int count = 1;
    [SerializeField] Vector3 areaCenter = Vector3.zero;
    [SerializeField] Vector2 areaSize = new(96f, 96f);
    [SerializeField, Min(0f)] float minDistanceFromPlayer = 10f;
    [Tooltip("Longe assim, o player acha a margarida logo no começo da fase.")]
    [SerializeField, Min(1f)] float maxDistanceFromPlayer = 45f;
    [Tooltip("Distância mínima até outros interagíveis (baús, santuários).")]
    [SerializeField, Min(0f)] float minSpacing = 6f;

    public IReadOnlyList<LadybugStation> Spawned => spawned;

    readonly List<LadybugStation> spawned = new();
    Transform player;
    NavMeshPath path;

    void Start()
    {
        path = new NavMeshPath();
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        for (int i = 0; i < count; i++) TrySpawn();
    }

    public LadybugStation TrySpawn()
    {
        if (config == null) return null;
        // Primeiro tenta perto do player; se não achar, aceita qualquer ponto alcançável do mapa
        for (int attempt = 0; attempt < 80; attempt++)
        {
            bool nearOnly = attempt < 40;
            var candidate = areaCenter + new Vector3(
                Random.Range(-areaSize.x, areaSize.x) * 0.5f, 0f,
                Random.Range(-areaSize.y, areaSize.y) * 0.5f);
            if (!NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas)) continue;
            Vector3 pos = hit.position;
            if (!NavClearance.Has(pos, 2.2f)) continue; // fura o NavMesh: não fecha túnel/corredor estreito
            if (player != null)
            {
                float sq = (pos - player.position).sqrMagnitude;
                if (sq < minDistanceFromPlayer * minDistanceFromPlayer) continue;
                if (nearOnly && sq > maxDistanceFromPlayer * maxDistanceFromPlayer) continue;
                if (!NavMesh.CalculatePath(pos, player.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            }
            if (TooClose(pos)) continue;
            var station = LadybugStation.Spawn(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), config, labelFont, transform);
            spawned.Add(station);
            return station;
        }
        Debug.LogWarning("[LadybugStationSpawner] não achou lugar para a margarida das joaninhas.", this);
        return null;
    }

    bool TooClose(Vector3 pos)
    {
        var pipe = ExitPipe.Instance;
        if (pipe != null)
        {
            float keep = pipe.ZoneRadius + 2f;
            if ((pipe.transform.position - pos).sqrMagnitude < keep * keep) return true;
        }
        foreach (var i in Interactable.All)
            if (i != null && (i.transform.position - pos).sqrMagnitude < minSpacing * minSpacing) return true;
        return false;
    }
}
