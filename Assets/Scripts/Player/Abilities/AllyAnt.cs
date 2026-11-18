using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Formiga aliada: caça o inimigo mais perto e morde; sem inimigos, segue o player.
/// Não leva dano (os inimigos só miram o player). Some depois do tempo de vida.
/// O dano conta como do player (kills, migalhas) mas não dispara itens "ao acertar".
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class AllyAnt : MonoBehaviour
{
    [SerializeField, Min(0f)] float damageCoefficient = 1.5f;
    [SerializeField, Min(0.1f)] float biteInterval = 0.8f;
    [SerializeField, Min(0.5f)] float biteRange = 2.4f;
    [SerializeField, Min(1f)] float aggroRange = 25f;
    [SerializeField, Min(0.5f)] float followDistance = 4f;

    public static readonly List<AllyAnt> Active = new();
    public int Bites { get; private set; }

    NavMeshAgent agent;
    GameObject owner;
    CharacterStats ownerStats;
    EnemyBase target;
    float dieAt, nextBite, nextRetarget;
    bool leaving;

    void Awake() => agent = GetComponent<NavMeshAgent>();
    void OnEnable() => Active.Add(this);
    void OnDisable() => Active.Remove(this);

    public void Init(GameObject owner, float lifetime)
    {
        this.owner = owner;
        ownerStats = owner != null ? owner.GetComponent<CharacterStats>() : null;
        dieAt = Time.time + lifetime;
    }

    void Update()
    {
        if (leaving) return;
        if (Time.time >= dieAt || owner == null) { StartCoroutine(Leave()); return; }

        if (Time.time >= nextRetarget || target == null || !target.IsAlive)
        {
            target = EnemyBase.Nearest(transform.position, aggroRange);
            nextRetarget = Time.time + 0.5f;
        }

        if (target != null)
        {
            Vector3 d = target.transform.position - transform.position; d.y = 0f;
            if (d.magnitude <= biteRange)
            {
                agent.ResetPath();
                if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 720f * Time.deltaTime);
                if (Time.time >= nextBite) Bite();
            }
            else agent.SetDestination(target.transform.position);
        }
        else if ((owner.transform.position - transform.position).sqrMagnitude > followDistance * followDistance)
        {
            agent.SetDestination(owner.transform.position);
        }
        else agent.ResetPath();
    }

    void Bite()
    {
        nextBite = Time.time + biteInterval;
        float damage = (ownerStats != null ? ownerStats.Damage : 12f) * damageCoefficient;
        target.Health.TakeDamage(new DamageInfo(damage, owner, target.Center, transform.forward) { NoProc = true });
        Bites++;
    }

    IEnumerator Leave()
    {
        leaving = true;
        if (agent.isOnNavMesh) agent.ResetPath();
        Vector3 s = transform.localScale;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime) { transform.localScale = s * (1f - t / 0.3f); yield return null; }
        Destroy(gameObject);
    }
}
