using System;
using UnityEngine;

/// <summary>
/// Uma migalha no chão. Sai pulando do inimigo, cai, e quando o player chega perto
/// voa até ele cada vez mais rápido. Sem Rigidbody (centenas delas não pesam).
/// Feita para pool: Launch reinicia tudo.
/// </summary>
public class Crumb : MonoBehaviour
{
    [SerializeField, Min(0f)] float gravity = 25f;
    [Tooltip("Raio da migalha para encostar no chão.")]
    [SerializeField, Min(0.01f)] float radius = 0.1f;
    [Tooltip("Tempo depois de cair antes de poder ser puxada (para ver ela saindo do inimigo).")]
    [SerializeField, Min(0f)] float attractDelay = 0.35f;
    [SerializeField, Min(0f)] float attractAcceleration = 60f;
    [SerializeField, Min(0f)] float maxAttractSpeed = 30f;
    [Tooltip("Distância do centro do player em que conta como pega.")]
    [SerializeField, Min(0.1f)] float collectDistance = 0.7f;
    [Tooltip("Some sozinha depois desse tempo (segundos).")]
    [SerializeField, Min(1f)] float lifetime = 90f;
    [SerializeField] LayerMask groundMask = ~0;

    public int Value { get; private set; }
    public bool IsAttracted { get; private set; }
    public bool IsGrounded { get; private set; }

    Vector3 velocity;
    float age;
    float spinSpeed;
    Transform player;
    PlayerWallet wallet;
    Health playerHealth;
    Func<float> attractRadius;
    Action<Crumb> release;

    public void Launch(int value, Vector3 position, Vector3 velocity, Transform player, Func<float> attractRadius, Action<Crumb> release)
    {
        Value = value;
        transform.position = position;
        this.velocity = velocity;
        this.player = player;
        this.attractRadius = attractRadius;
        this.release = release;
        wallet = player != null ? player.GetComponent<PlayerWallet>() : null;
        playerHealth = player != null ? player.GetComponent<Health>() : null;
        age = 0f;
        IsAttracted = false;
        IsGrounded = false;
        spinSpeed = UnityEngine.Random.Range(90f, 240f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age > lifetime) { Despawn(); return; }

        transform.Rotate(0f, spinSpeed * dt, 0f, Space.World);

        bool playerAlive = player != null && (playerHealth == null || playerHealth.IsAlive);
        Vector3 target = playerAlive ? player.position + Vector3.up : Vector3.zero;

        if (!IsAttracted && playerAlive && age > attractDelay)
        {
            float r = attractRadius != null ? attractRadius() : 6f;
            if ((target - transform.position).sqrMagnitude <= r * r) IsAttracted = true;
        }

        if (IsAttracted && playerAlive)
        {
            Vector3 to = target - transform.position;
            float dist = to.magnitude;
            if (dist <= collectDistance) { Collect(); return; }
            // Puxa na direção do player, sem gravidade, cada vez mais rápido
            float speed = Mathf.Min(velocity.magnitude + attractAcceleration * dt, maxAttractSpeed);
            velocity = to / dist * speed;
            Vector3 step = velocity * dt;
            if (step.sqrMagnitude >= to.sqrMagnitude) { Collect(); return; }
            transform.position += step;
            return;
        }

        if (IsGrounded) return;
        velocity += Vector3.down * (gravity * dt);
        Vector3 move = velocity * dt;
        // Cai até encostar em algo (chão, caixa, rampa)
        if (move.y < 0f && Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, -move.y + radius, groundMask, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<EnemyBase>() == null)
        {
            transform.position = new Vector3(transform.position.x + move.x, hit.point.y + radius, transform.position.z + move.z);
            velocity = Vector3.zero;
            IsGrounded = true;
            return;
        }
        transform.position += move;
        if (transform.position.y < -50f) Despawn(); // caiu do mapa
    }

    void Collect()
    {
        if (wallet != null) wallet.Add(Value);
        Despawn();
    }

    void Despawn()
    {
        var r = release;
        release = null;
        if (r != null) r(this);
        else Destroy(gameObject);
    }
}
