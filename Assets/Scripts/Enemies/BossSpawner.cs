using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Quando o player ativa o cano de saída, o chefe nasce perto dele, escalado pela
/// dificuldade, e vira "bloqueador": o cano só conclui depois que ele morrer.
/// Com o modificador de run "chefe desde o início", ele nasce logo no começo da fase, perto
/// do player, e ativar o cano não chama outro.
/// Com rodízio (bossRotation), o chefe muda por fase: fase 1 = primeiro da lista, fase 2 = segundo...
/// </summary>
public class BossSpawner : MonoBehaviour
{
    [SerializeField] EnemyBase bossPrefab;
    [Tooltip("Chefe de cada fase, em ordem (fase 1, fase 2...; depois repete). Vazio = sempre o bossPrefab.")]
    [SerializeField] System.Collections.Generic.List<EnemyBase> bossRotation = new();
    [Tooltip("Distância do cano em que o chefe nasce.")]
    [SerializeField] Vector2 distanceFromPipe = new(14f, 20f);
    [Tooltip("Espera depois de ativar antes de o chefe aparecer.")]
    [SerializeField, Min(0f)] float delay = 2f;
    [Header("Modificador: chefe desde o início")]
    [Tooltip("Segundos depois do começo da fase até o chefe aparecer.")]
    [SerializeField, Min(0f)] float startDelay = 4f;
    [Tooltip("Distância do player em que o chefe nasce no começo da fase.")]
    [SerializeField] Vector2 distanceFromPlayer = new(25f, 35f);

    public EnemyBase Spawned { get; private set; }
    /// <summary>Chefe desta fase: o do rodízio pela fase atual, ou o bossPrefab.</summary>
    public EnemyBase CurrentBossPrefab => BossForStage(RunManager.Instance != null ? RunManager.Instance.Stage : 1);

    /// <summary>Chefe de uma fase (1 = primeira).</summary>
    public EnemyBase BossForStage(int stage)
    {
        int n = bossRotation != null ? bossRotation.Count : 0;
        if (n == 0) return bossPrefab;
        var b = bossRotation[(Mathf.Max(1, stage) - 1) % n];
        return b != null ? b : bossPrefab;
    }
    /// <summary>O chefe desta fase já nasceu (mesmo que já tenha morrido).</summary>
    public bool HasSpawned { get; private set; }

    ExitPipe pipe;

    void Start()
    {
        pipe = ExitPipe.Instance;
        if (pipe != null) pipe.OnActivated += HandleActivated;
        if (RunModifiers.Is(RunModifier.BossFromStart) && CurrentBossPrefab != null) Invoke(nameof(SpawnBossAtStart), startDelay);
    }

    void OnDestroy()
    {
        if (pipe != null) pipe.OnActivated -= HandleActivated;
    }

    void HandleActivated(ExitPipe p)
    {
        if (CurrentBossPrefab == null || RunModifiers.Is(RunModifier.BossFromStart)) return; // já veio no começo
        if (delay > 0f) Invoke(nameof(SpawnBoss), delay);
        else SpawnBoss();
    }

    /// <summary>Chefe perto do cano (quando o player ativa a saída).</summary>
    public void SpawnBoss()
    {
        if (pipe == null) return;
        SpawnAround(pipe.transform.position, distanceFromPipe, pipe.transform.position);
    }

    /// <summary>Chefe perto do player no começo da fase (modificador de run).</summary>
    public void SpawnBossAtStart()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        Vector3 fallback = pipe != null ? pipe.transform.position : transform.position; // nunca em cima do player
        SpawnAround(player != null ? player.transform.position : fallback, distanceFromPlayer, fallback);
    }

    /// <param name="fallback">Onde nascer se nenhum ponto em volta tiver caminho até o player.</param>
    void SpawnAround(Vector3 center, Vector2 distance, Vector3 fallback)
    {
        var prefab = CurrentBossPrefab;
        if (HasSpawned || prefab == null) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        Vector3 to = player != null ? player.transform.position : center;
        var path = new NavMeshPath();
        Vector3 pos = fallback;
        for (int i = 0; i < 30; i++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            var c = center + new Vector3(dir.x, 0f, dir.y) * Random.Range(distance.x, distance.y);
            if (!NavMesh.SamplePosition(c, out var hit, 3f, NavMesh.AllAreas)) continue;
            if (!NavMesh.CalculatePath(hit.position, to, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            pos = hit.position;
            break;
        }
        Vector3 look = to - pos; look.y = 0f;
        Spawned = Instantiate(prefab, pos, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
        HasSpawned = true;
        var dm = DifficultyManager.Instance;
        if (dm != null) Spawned.ApplyScaling(dm.EnemyHealthMultiplier, dm.EnemyDamageMultiplier);
        if (pipe != null) pipe.RegisterBlocker(Spawned);
    }
}
