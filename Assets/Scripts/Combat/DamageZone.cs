using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Área que causa dano em tudo que é IDamageable dentro dela.
/// Serve para perigos do mapa, poça de ácido do besouro, fogo do elite etc.
/// Precisa de um Collider marcado como Trigger.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DamageZone : MonoBehaviour
{
    [SerializeField, Min(0f)] float damagePerSecond = 20f;
    [Tooltip("De quanto em quanto tempo o dano é aplicado.")]
    [SerializeField, Min(0.05f)] float tickInterval = 0.5f;
    [SerializeField] bool ignoreArmor = false;
    [Tooltip("Vazio = afeta todos. Ex.: 'Player' para só o jogador.")]
    [SerializeField] string onlyTag = "";

    public string OnlyTag { get => onlyTag; set => onlyTag = value; }
    [Tooltip("Quem é dono da zona (fica como Source do dano). Vazio = esta zona.")]
    [SerializeField] GameObject owner;

    // alvo -> collider que entrou (para conferir se ainda está dentro)
    readonly Dictionary<IDamageable, Collider> targets = new();
    readonly List<IDamageable> toRemove = new();
    Collider zone;
    float nextTick;

    public float DamagePerSecond { get => damagePerSecond; set => damagePerSecond = Mathf.Max(0f, value); }
    public GameObject Owner { get => owner; set => owner = value; }

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        zone = GetComponent<Collider>();
    }

    void OnDisable()
    {
        targets.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!string.IsNullOrEmpty(onlyTag) && !other.CompareTag(onlyTag)) return;
        var target = other.GetComponentInParent<IDamageable>();
        if (target != null) targets[target] = other;
    }

    void OnTriggerExit(Collider other)
    {
        var target = other.GetComponentInParent<IDamageable>();
        if (target != null) targets.Remove(target);
    }

    /// <summary>
    /// Teleporte, desativar ou destruir um collider não chama OnTriggerExit,
    /// então a cada tick confere se o alvo continua dentro da zona.
    /// </summary>
    bool StillInside(IDamageable target, Collider col)
    {
        if (target is not Component c || c == null || col == null) return false;
        if (!col.enabled || !col.gameObject.activeInHierarchy) return false;
        return zone.bounds.Intersects(col.bounds);
    }

    void Update()
    {
        if (targets.Count == 0 || Time.time < nextTick) return;
        nextTick = Time.time + tickInterval;

        float amount = damagePerSecond * tickInterval;
        GameObject source = owner != null ? owner : gameObject;
        foreach (var (target, col) in targets)
        {
            if (!StillInside(target, col))
            {
                toRemove.Add(target);
                continue;
            }
            if (!target.IsAlive) continue; // morto não sai da lista: pode ser revivido/reaproveitado
            if (target is Component c && c.TryGetComponent(out PlayerMovement mover) && mover.IsFloating) continue; // bolha de sabão por cima da poça
            Vector3 point = col.bounds.center;
            target.TakeDamage(new DamageInfo(amount, source, point, point - transform.position) { IgnoreArmor = ignoreArmor });
        }
        foreach (var t in toRemove) targets.Remove(t);
        toRemove.Clear();
    }
}
