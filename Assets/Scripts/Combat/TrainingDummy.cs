using System.Collections;
using UnityEngine;

/// <summary>
/// Boneco de treino: leva dano, some ao morrer e volta com vida cheia.
/// Para testar armas e itens sem precisar de IA.
/// </summary>
[RequireComponent(typeof(Health))]
public class TrainingDummy : MonoBehaviour
{
    [SerializeField, Min(0f)] float respawnDelay = 2f;

    public int HitCount { get; private set; }
    public float TotalDamage { get; private set; }

    Health health;
    Renderer[] renderers;
    Collider[] colliders;

    void Awake()
    {
        health = GetComponent<Health>();
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
        health.OnDamaged += info => { HitCount++; TotalDamage += info.Amount; };
        health.OnDeath += _ => StartCoroutine(Respawn());
    }

    IEnumerator Respawn()
    {
        SetVisible(false);
        yield return new WaitForSeconds(respawnDelay);
        health.ResetHealth();
        SetVisible(true);
    }

    void SetVisible(bool visible)
    {
        foreach (var r in renderers) r.enabled = visible;
        foreach (var c in colliders) c.enabled = visible;
    }
}
