using UnityEngine;

/// <summary>
/// Bola de ácido em arco: voa até o ponto mirado, acerta o que estiver no caminho
/// (dano direto no player) e deixa uma poça de ácido onde cair.
/// </summary>
public class AcidBlob : MonoBehaviour
{
    [SerializeField, Min(0f)] float gravity = 20f;
    [SerializeField, Min(0.05f)] float radius = 0.35f;
    [SerializeField, Min(1f)] float maxLifetime = 4f;

    GameObject owner;
    AcidPool poolPrefab;
    float impactDamage, poolDps, age;
    Vector3 velocity;
    readonly RaycastHit[] hits = new RaycastHit[8];

    /// <summary>Lança de `from` para cair em `target` depois de `flightTime` segundos.</summary>
    public void Launch(Vector3 from, Vector3 target, float flightTime, GameObject owner, AcidPool pool, float impactDamage, float poolDps)
    {
        transform.position = from;
        this.owner = owner; poolPrefab = pool; this.impactDamage = impactDamage; this.poolDps = poolDps;
        // Balística: v0 = (alvo - origem)/T + 0.5*g*T (para cima)
        velocity = (target - from) / flightTime + Vector3.up * (0.5f * gravity * flightTime);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age > maxLifetime) { Destroy(gameObject); return; }
        velocity += Vector3.down * (gravity * dt);
        Vector3 step = velocity * dt;
        float dist = step.magnitude;
        if (dist <= 0f) return;

        int n = Physics.SphereCastNonAlloc(transform.position, radius, step / dist, hits, dist, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; RaycastHit hit = default; bool found = false;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (owner != null && c.transform.IsChildOf(owner.transform)) continue;
            if (c.GetComponentInParent<EnemyBase>() != null) continue; // passa pelos outros inimigos
            if (hits[i].distance < best) { best = hits[i].distance; hit = hits[i]; found = true; }
        }
        if (found) { Splash(hit); return; }
        transform.position += step;
    }

    void Splash(RaycastHit hit)
    {
        Vector3 point = hit.distance > 0f ? hit.point : transform.position;
        // Acerto direto no player
        if (hit.collider.TryGetComponent(out PlayerMovement _) && hit.collider.TryGetComponent(out Health h))
            h.TakeDamage(new DamageInfo(impactDamage, owner, point, velocity));
        // Poça no chão embaixo do ponto: primeira superfície que não é o player nem inimigo
        Vector3 ground = point;
        int n = Physics.RaycastNonAlloc(point + Vector3.up * 0.5f, Vector3.down, hits, 12f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c.GetComponentInParent<PlayerMovement>() != null || c.GetComponentInParent<EnemyBase>() != null) continue;
            if (hits[i].distance < best) { best = hits[i].distance; ground = hits[i].point; }
        }
        if (poolPrefab != null)
        {
            var pool = Instantiate(poolPrefab, ground + Vector3.up * 0.03f, Quaternion.identity);
            pool.Begin(poolDps, owner);
        }
        Destroy(gameObject);
    }
}
