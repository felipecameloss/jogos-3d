using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Passiva da Formiga: "chamado da colônia". A cada N inimigos que ela mata, uma formiga
/// aliada se junta (nasce onde o inimigo caiu) por alguns segundos. Limite de aliadas
/// vindas da passiva ao mesmo tempo (as do R não contam).
/// </summary>
public class AntColonyCall : MonoBehaviour
{
    [SerializeField] AllyAnt allyPrefab;
    [SerializeField, Min(1)] int killsPerAlly = 5;
    [SerializeField, Min(1f)] float lifetime = 9f;
    [SerializeField, Min(1)] int maxAlive = 4;

    public int KillCounter { get; private set; }
    public int Summoned { get; private set; }
    public int KillsPerAlly => killsPerAlly;
    public int AliveFromPassive { get { allies.RemoveAll(a => a == null); return allies.Count; } }

    readonly List<AllyAnt> allies = new();
    Health health;

    void Awake() => health = GetComponent<Health>();

    public void Init(AllyAnt prefab) => allyPrefab = prefab;

    void OnEnable() => EnemyBase.OnAnyEnemyDied += HandleEnemyDied;
    void OnDisable() => EnemyBase.OnAnyEnemyDied -= HandleEnemyDied;

    void HandleEnemyDied(EnemyBase enemy, DamageInfo blow)
    {
        if (allyPrefab == null || enemy == null || blow.Source == null || !blow.Source.transform.IsChildOf(transform)) return;
        if (health != null && !health.IsAlive) return;
        if (++KillCounter < killsPerAlly) return;
        KillCounter = 0;
        if (AliveFromPassive >= maxAlive) return;
        if (!NavMesh.SamplePosition(enemy.transform.position, out var hit, 3f, NavMesh.AllAreas)) return;
        Vector3 look = transform.position - hit.position; look.y = 0f;
        var ally = Instantiate(allyPrefab, hit.position, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
        ally.Init(gameObject, lifetime);
        allies.Add(ally);
        Summoned++;
    }
}
