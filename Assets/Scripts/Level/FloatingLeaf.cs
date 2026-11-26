using UnityEngine;

/// <summary>
/// Folha boiando na poça (fase da poça d'água, issue #31): plataforma que sobe e desce devagar
/// e gira um pouco na água. Em cima dela o player fica acima da água (sem a lentidão da
/// <see cref="SlowZone"/>); inimigos que andam pelo chão não chegam (só os que pulam ou atiram).
/// O balanço é pequeno e lento para o CharacterController ficar firme em cima.
/// </summary>
public class FloatingLeaf : MonoBehaviour
{
    [SerializeField, Min(0f)] float bobHeight = 0.08f;
    [SerializeField, Min(0.05f)] float bobSpeed = 0.9f;
    [Tooltip("Quanto a folha gira para lá e para cá (graus).")]
    [SerializeField, Min(0f)] float swayAngle = 4f;

    Vector3 restPosition;
    Quaternion restRotation;
    float phase;
    Rigidbody body;

    /// <summary>Altura do topo da folha (testes: o player em cima fica acima da água).</summary>
    public float TopY => TryGetComponent(out Collider c) ? c.bounds.max.y : transform.position.y;

    void Awake()
    {
        restPosition = transform.position;
        restRotation = transform.rotation;
        phase = Random.value * 10f;
        // cinemático: mexer colisor sem Rigidbody custa caro na física
        if (!TryGetComponent(out body)) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void FixedUpdate()
    {
        float t = Time.time * bobSpeed + phase;
        body.MovePosition(restPosition + Vector3.up * (Mathf.Sin(t) * bobHeight));
        body.MoveRotation(restRotation * Quaternion.Euler(0f, Mathf.Sin(t * 0.37f) * swayAngle, 0f));
    }
}
