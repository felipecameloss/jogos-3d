using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Quando o player ativa o cano de saída, o chefe nasce perto dele, escalado pela
/// dificuldade, e vira "bloqueador": o cano só conclui depois que ele morrer.
/// </summary>
public class BossSpawner : MonoBehaviour
{
    [SerializeField] EnemyBase bossPrefab;
    [Tooltip("Distância do cano em que o chefe nasce.")]
    [SerializeField] Vector2 distanceFromPipe = new(14f, 20f);
    [Tooltip("Espera depois de ativar antes de o chefe aparecer.")]
    [SerializeField, Min(0f)] float delay = 2f;

    public EnemyBase Spawned { get; private set; }

    ExitPipe pipe;

    void Start()
    {
        pipe = ExitPipe.Instance;
        if (pipe != null) pipe.OnActivated += HandleActivated;
    }

    void OnDestroy()
    {
        if (pipe != null) pipe.OnActivated -= HandleActivated;
    }

    void HandleActivated(ExitPipe p)
    {
        if (bossPrefab == null) return;
        if (delay > 0f) Invoke(nameof(SpawnBoss), delay);
        else SpawnBoss();
    }

    public void SpawnBoss()
    {
        if (pipe == null || Spawned != null) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        Vector3 to = player != null ? player.transform.position : pipe.transform.position;
        var path = new NavMeshPath();
        Vector3 pos = pipe.transform.position;
        for (int i = 0; i < 30; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            var c = pipe.transform.position + new Vector3(dir.x, 0f, dir.y) * Random.Range(distanceFromPipe.x, distanceFromPipe.y);
            if (!NavMesh.SamplePosition(c, out var hit, 3f, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(hit.position, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            pos = hit.position;
            break;
        }
        Vector3 look = to - pos; look.y = 0f;
        Spawned = Instantiate(bossPrefab, pos, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
        var dm = DifficultyManager.Instance;
        if (dm != null) Spawned.ApplyScaling(dm.EnemyHealthMultiplier, dm.EnemyDamageMultiplier);
        pipe.RegisterBlocker(Spawned);
    }
}
