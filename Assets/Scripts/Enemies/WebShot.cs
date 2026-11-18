using UnityEngine;

/// <summary>
/// Bola de teia da aranha: voa em arco até o ponto mirado. Acerto direto no player
/// prende na hora; senão vira uma teia no chão (WebPatch) que prende quem pisar.
/// </summary>
public class WebShot : MonoBehaviour
{
    [SerializeField, Min(0f)] float gravity = 20f;
    [SerializeField, Min(0.05f)] float radius = 0.4f;
    [SerializeField, Min(1f)] float maxLifetime = 4f;

    GameObject owner;
    WebPatch patchPrefab;
    float rootDuration, age;
    Vector3 velocity;
    readonly RaycastHit[] hits = new RaycastHit[8];

    /// <summary>Lança de `from` para cair em `target` depois de `flightTime` segundos.</summary>
    public void Launch(Vector3 from, Vector3 target, float flightTime, GameObject owner, WebPatch patch, float rootDuration)
    {
        transform.position = from;
        this.owner = owner; patchPrefab = patch; this.rootDuration = rootDuration;
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
        transform.rotation = Quaternion.LookRotation(step);

        int n = Physics.SphereCastNonAlloc(transform.position, radius, step / dist, hits, dist, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; RaycastHit hit = default; bool found = false;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (owner != null && c.transform.IsChildOf(owner.transform)) continue;
            if (c.GetComponentInParent<EnemyBase>() != null) continue;
            if (hits[i].distance < best) { best = hits[i].distance; hit = hits[i]; found = true; }
        }
        if (found) { Splat(hit); return; }
        transform.position += step;
    }

    void Splat(RaycastHit hit)
    {
        Vector3 point = hit.distance > 0f ? hit.point : transform.position;
        var player = hit.collider.GetComponentInParent<PlayerMovement>();
        Vector3 ground = player != null ? player.transform.position : GroundBelow(point);
        if (patchPrefab != null)
        {
            var patch = Instantiate(patchPrefab, ground + Vector3.up * 0.04f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            patch.Begin(rootDuration, owner, player); // acerto direto: já prende
        }
        Destroy(gameObject);
    }

    Vector3 GroundBelow(Vector3 point)
    {
        Vector3 ground = point;
        int n = Physics.RaycastNonAlloc(point + Vector3.up * 0.5f, Vector3.down, hits, 12f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c.GetComponentInParent<PlayerMovement>() != null || c.GetComponentInParent<EnemyBase>() != null) continue;
            if (hits[i].distance < best) { best = hits[i].distance; ground = hits[i].point; }
        }
        return ground;
    }
}
