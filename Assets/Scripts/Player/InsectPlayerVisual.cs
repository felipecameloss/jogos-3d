using UnityEngine;

/// <summary>
/// Extras do modelo de inseto do player (formiga, besouro), que não tem clipes de animação:
/// - Kick: o corpo dá um tranco para a frente e se inclina (golpes, mordida, investida).
/// - Morte: tomba de costas, com as pernas para cima.
/// Fica na raiz do modelo; o ProceduralInsectAnimator cuida das pernas, antenas e squash.
/// </summary>
public class InsectPlayerVisual : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float kickRecover = 9f;
    [SerializeField, Min(0.05f)] float deathFlipTime = 0.45f;

    Vector3 restPos;
    Quaternion restRot;
    float kick;      // metros para a frente agora
    float kickPitch; // graus de inclinação agora
    Health health;
    float diedAt = -1f;

    /// <summary>Altura do modelo: de costas, sobe isso para não afundar no chão.</summary>
    [HideInInspector] public float FlipLift = 0.5f;

    public float CurrentKick => kick;
    public bool IsDead => diedAt >= 0f;

    void Awake()
    {
        restPos = transform.localPosition;
        restRot = transform.localRotation;
        health = GetComponentInParent<Health>();
        if (health != null) health.OnDeath += HandleDeath;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDeath -= HandleDeath;
    }

    /// <summary>Tranco para a frente (metros) com o nariz para baixo (graus).</summary>
    public void Kick(float distance, float pitch = 8f)
    {
        if (IsDead) return;
        kick = Mathf.Max(kick, distance);
        kickPitch = Mathf.Max(kickPitch, pitch);
    }

    void HandleDeath(DamageInfo _) => diedAt = Time.time;

    void LateUpdate()
    {
        if (IsDead)
        {
            float k = Mathf.SmoothStep(0f, 1f, (Time.time - diedAt) / deathFlipTime);
            transform.localRotation = restRot * Quaternion.Euler(0f, 0f, 180f * k);
            // sobe enquanto gira para não afundar no chão de costas
            transform.localPosition = restPos + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.4f + k * FlipLift);
            return;
        }
        float dt = Time.deltaTime;
        kick = Mathf.Lerp(kick, 0f, 1f - Mathf.Exp(-kickRecover * dt));
        kickPitch = Mathf.Lerp(kickPitch, 0f, 1f - Mathf.Exp(-kickRecover * dt));
        transform.localPosition = restPos + Vector3.forward * kick;
        transform.localRotation = restRot * Quaternion.Euler(kickPitch, 0f, 0f);
    }
}
