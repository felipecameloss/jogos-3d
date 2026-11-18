using UnityEngine;

/// <summary>
/// Orbe de cura no chão (Gotinha de mel). Salta ao cair do inimigo, repousa no chão
/// e voa até o player para curá-lo quando ele chega perto e precisa de vida.
/// </summary>
public class HealingOrb : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    float healAmount;
    float attractRadius;
    float lifetime;
    float age;
    bool isGrounded;
    bool isAttracted;
    Vector3 velocity;
    Vector3 restPos;
    Transform player;
    Health playerHealth;

    public static HealingOrb Spawn(Vector3 position, float healAmount, Color color, Material material = null, float attractRadius = 5f, float lifetime = 20f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "HealingOrb";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.42f;

        var r = go.GetComponent<MeshRenderer>();
        if (material != null) r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(EmissionId, color * 1.3f);
        r.SetPropertyBlock(block);

        var orb = go.AddComponent<HealingOrb>();
        orb.Init(healAmount, attractRadius, lifetime);
        return orb;
    }

    void Init(float heal, float attract, float life)
    {
        healAmount = heal;
        attractRadius = attract;
        lifetime = life;
        Vector2 side = Random.insideUnitCircle * 2.2f;
        velocity = new Vector3(side.x, 5.5f, side.y);
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { player = p.transform; playerHealth = p.GetComponent<Health>(); }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        if (age >= lifetime) { Destroy(gameObject); return; }

        bool canHeal = player != null && playerHealth != null && playerHealth.IsAlive && !playerHealth.IsFullHealth;
        Vector3 target = player != null ? player.position + Vector3.up : Vector3.zero;

        if (!isAttracted && canHeal && age >= 0.35f)
        {
            if ((target - transform.position).sqrMagnitude <= attractRadius * attractRadius)
                isAttracted = true;
        }

        if (isAttracted && canHeal)
        {
            Vector3 to = target - transform.position;
            float dist = to.magnitude;
            if (dist <= 0.75f)
            {
                playerHealth.Heal(healAmount);
                Destroy(gameObject);
                return;
            }
            float speed = Mathf.Min(velocity.magnitude + 45f * dt, 26f);
            velocity = to / dist * speed;
            Vector3 step = velocity * dt;
            if (step.sqrMagnitude >= to.sqrMagnitude)
            {
                playerHealth.Heal(healAmount);
                Destroy(gameObject);
                return;
            }
            transform.position += step;
            return;
        }

        if (isGrounded)
        {
            transform.position = restPos + Vector3.up * (Mathf.Sin(age * 3f) * 0.08f);
            return;
        }

        velocity += Vector3.down * (22f * dt);
        Vector3 move = velocity * dt;
        const float radius = 0.25f;
        if (move.y < 0f && Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, -move.y + radius, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<EnemyBase>() == null
            && hit.collider.GetComponentInParent<PlayerMovement>() == null)
        {
            restPos = new Vector3(transform.position.x + move.x, hit.point.y + radius, transform.position.z + move.z);
            transform.position = restPos;
            velocity = Vector3.zero;
            isGrounded = true;
            return;
        }

        transform.position += move;
        if (transform.position.y < -50f) Destroy(gameObject);
    }
}
