using System.Collections;
using UnityEngine;

/// <summary>
/// Utilidade: dash rápido na direção em que o player anda (ou para a frente, se parado).
/// Invencível durante o dash. Funciona no ar.
/// </summary>
public class DashAbility : PlayerAbility
{
    [Header("Dash")]
    [SerializeField, Min(0f)] float distance = 11.5f;
    [SerializeField, Min(0.01f)] float duration = 0.18f;
    [Tooltip("Rastro ligado só durante o dash (opcional).")]
    [SerializeField] TrailRenderer trail;

    protected override void Awake()
    {
        base.Awake();
        if (trail != null) trail.emitting = false;
    }

    protected override bool TryActivate()
    {
        Vector3 dir = Movement != null && Movement.MoveDirection.sqrMagnitude > 0.01f ? Movement.MoveDirection : AimForward;
        StartCoroutine(DashRoutine(dir));
        return true;
    }

    IEnumerator DashRoutine(Vector3 dir)
    {
        if (Movement != null) Movement.Dash(dir, distance, duration);
        bool wasInvincible = Health != null && Health.Invincible;
        if (Health != null) Health.Invincible = true;
        if (trail != null) trail.emitting = true;
        yield return new WaitForSeconds(duration + 0.05f);
        if (Health != null && !wasInvincible) Health.Invincible = false;
        if (trail != null) trail.emitting = false;
    }
}
