using UnityEngine;

/// <summary>
/// Faz todo inimigo que morre soltar migalhas. A recompensa base vem do inimigo
/// (EnemyBase.CrumbReward) e cresce com a dificuldade, junto com o preço dos baús.
/// </summary>
public class CrumbDropper : MonoBehaviour
{
    [SerializeField] Crumb crumbPrefab;
    [Tooltip("No máximo quantas migalhas separadas por inimigo (o valor é dividido entre elas).")]
    [SerializeField, Min(1)] int maxPiecesPerDrop = 6;
    [Tooltip("Distância em que as migalhas começam a voar até o player.")]
    [SerializeField, Min(0f)] float attractRadius = 6f;
    [SerializeField, Min(0)] int poolPrewarm = 40;

    [Header("Pulinho ao sair do inimigo")]
    [SerializeField] float launchUp = 6f;
    [SerializeField] float launchSideways = 3f;

    /// <summary>Multiplicador do raio de atração (para itens tipo ímã).</summary>
    [HideInInspector] public float AttractRadiusMultiplier = 1f;
    public float AttractRadius => attractRadius * AttractRadiusMultiplier;

    ComponentPool<Crumb> pool;
    Transform player;

    void Awake()
    {
        if (crumbPrefab != null) pool = new ComponentPool<Crumb>(crumbPrefab, poolPrewarm, maxSize: 300, rootName: "[Pool] Crumb");
        else Debug.LogError("CrumbDropper sem prefab de migalha.", this);
    }

    void OnEnable() => EnemyBase.OnAnyEnemyDied += HandleEnemyDied;
    void OnDisable() => EnemyBase.OnAnyEnemyDied -= HandleEnemyDied;

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    /// <summary>Valor que um inimigo com essa recompensa base vale agora.</summary>
    public static int ScaledReward(int baseReward)
    {
        float coeff = DifficultyManager.Instance != null ? DifficultyManager.Instance.Coefficient : 1f;
        return Mathf.Max(baseReward > 0 ? 1 : 0, Mathf.RoundToInt(baseReward * coeff));
    }

    void HandleEnemyDied(EnemyBase enemy, DamageInfo killingBlow)
    {
        Drop(ScaledReward(enemy.CrumbReward) * EnemyElite.CrumbMultiplierFor(enemy), enemy.transform.position + Vector3.up * 0.8f);
    }

    /// <summary>Solta `total` migalhas em pedaços a partir de `position`.</summary>
    public void Drop(int total, Vector3 position)
    {
        if (pool == null || total <= 0) return;
        int pieces = Mathf.Clamp(total, 1, maxPiecesPerDrop);
        int baseValue = total / pieces, remainder = total % pieces;
        for (int i = 0; i < pieces; i++)
        {
            int value = baseValue + (i < remainder ? 1 : 0);
            Vector2 side = Random.insideUnitCircle * launchSideways;
            var velocity = new Vector3(side.x, launchUp * Random.Range(0.8f, 1.2f), side.y);
            var crumb = pool.Get();
            crumb.Launch(value, position, velocity, player, () => AttractRadius, pool.Release);
        }
    }
}
