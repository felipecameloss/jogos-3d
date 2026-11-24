using System;
using UnityEngine;

/// <summary>
/// Projétil sem Rigidbody: anda por conta própria e faz SphereCast entre a posição
/// do frame anterior e a nova, então não atravessa nada mesmo rápido.
/// Feito para ser usado com ComponentPool (Launch reinicia tudo).
/// </summary>
public class Projectile : MonoBehaviour
{
    [SerializeField, Min(0f)] float radius = 0.08f;
    [SerializeField] LayerMask hitMask = ~0;
    [SerializeField, Min(0.1f)] float maxLifetime = 3f;
    [Tooltip("Queda do projétil (0 = reto).")]
    [SerializeField] float gravity = 0f;

    Vector3 velocity;
    float damage;
    float timeAlive;
    GameObject owner;
    bool noProc;
    Action<Projectile> release;
    Action<IDamageable, DamageInfo> onHit;
    TrailRenderer trail;
    readonly RaycastHit[] hits = new RaycastHit[16];

    public GameObject Owner => owner;

    void Awake()
    {
        trail = GetComponentInChildren<TrailRenderer>();
    }

    /// <param name="release">Como devolver ao pool. Null = Destroy.</param>
    /// <param name="onHit">Chamado quando acerta algo com vida (hook "ao acertar" dos itens).</param>
    /// <param name="noProc">O dano conta como do dono (kills, migalhas) mas não dispara itens "ao acertar" (aliados).</param>
    public void Launch(Vector3 position, Vector3 velocity, float damage, GameObject owner,
                       Action<Projectile> release = null, Action<IDamageable, DamageInfo> onHit = null, bool noProc = false)
    {
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(velocity));
        this.velocity = velocity;
        this.damage = damage;
        this.owner = owner;
        this.noProc = noProc;
        this.release = release;
        this.onHit = onHit;
        timeAlive = 0f;
        if (trail != null) trail.Clear();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        timeAlive += dt;
        if (timeAlive >= maxLifetime) { Despawn(); return; }

        velocity += Vector3.down * (gravity * dt);
        Vector3 step = velocity * dt;
        float distance = step.magnitude;
        if (distance <= 0f) return;
        Vector3 dir = step / distance;

        if (TryFindHit(transform.position, dir, distance, out RaycastHit hit))
        {
            HandleHit(hit, dir);
            return;
        }

        transform.SetPositionAndRotation(transform.position + step, Quaternion.LookRotation(dir));
    }

    bool TryFindHit(Vector3 from, Vector3 dir, float distance, out RaycastHit best)
    {
        best = default;
        int count = Physics.SphereCastNonAlloc(from, radius, dir, hits, distance, hitMask, QueryTriggerInteraction.Ignore);
        float bestDistance = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            var h = hits[i];
            if (owner != null && h.collider.transform.IsChildOf(owner.transform)) continue; // não acerta quem atirou
            // SphereCast que já começa encostando volta distance 0 e point zero
            if (h.distance <= 0f) h.point = from;
            if (h.distance < bestDistance) { bestDistance = h.distance; best = h; found = true; }
        }
        return found;
    }

    void HandleHit(RaycastHit hit, Vector3 dir)
    {
        var target = hit.collider.GetComponentInParent<IDamageable>();
        if (target != null && target.IsAlive)
        {
            var info = new DamageInfo(damage, owner, hit.point, dir) { NoProc = noProc };
            target.TakeDamage(info);
            onHit?.Invoke(target, info);
        }
        Despawn();
    }

    void Despawn()
    {
        var r = release;
        release = null;
        onHit = null;
        if (r != null) r(this);
        else Destroy(gameObject);
    }
}
