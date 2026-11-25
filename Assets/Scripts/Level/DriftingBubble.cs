using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bolha de sabão soprada para dentro do quintal (SoapBubbleStream): flutua devagar, balança
/// e estoura com qualquer tiro (é um IDamageable sem vida) ou quando o player encosta. A bolha
/// furta-cor é maior, brilha mais e carrega um item.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class DriftingBubble : MonoBehaviour, IDamageable
{
    public static readonly List<DriftingBubble> Active = new();

    const float PopTime = 0.18f;
    const float TouchMargin = 0.7f;

    Vector3 velocity;
    float diameter, lifetime, age, phase, popTimer;
    bool popped;
    Transform player;
    SoapBubbleLook look;
    SphereCollider col;
    Action<DriftingBubble, bool> onPop;

    /// <summary>A bolha furta-cor que solta a Bolha de sabão.</summary>
    public bool IsRainbow { get; private set; }
    public bool IsAlive => !popped;

    // O projeto entra no Play Mode sem recarregar o domínio: a lista estática não zera sozinha
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Active.Clear();

    /// <param name="onPop">(bolha, estourada pelo player?) — false quando só sumiu no fim do tempo.</param>
    public static DriftingBubble Spawn(Vector3 position, Vector3 velocity, float diameter, bool rainbow, float lifetime,
        Material material, Action<DriftingBubble, bool> onPop)
    {
        var go = new GameObject(rainbow ? "Soap Bubble (furta-cor)" : "Soap Bubble");
        go.transform.position = position;
        var col = go.AddComponent<SphereCollider>(); // não é gatilho: os tiros (SphereCast) acertam
        col.radius = diameter * 0.5f;
        var b = go.AddComponent<DriftingBubble>();
        b.col = col;
        b.velocity = velocity;
        b.diameter = diameter;
        b.IsRainbow = rainbow;
        b.lifetime = lifetime;
        b.onPop = onPop;
        b.phase = UnityEngine.Random.value * 10f;
        b.look = new SoapBubbleLook("Look", material, go.transform);
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) b.player = p.transform;
        return b;
    }

    void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    void OnDisable() => Active.Remove(this);
    void OnDestroy() => Active.Remove(this);

    public void TakeDamage(DamageInfo info)
    {
        bool byPlayer = info.Source != null && player != null && info.Source.transform.IsChildOf(player);
        Pop(byPlayer);
    }

    /// <summary>Estoura agora. `byPlayer` = conta para a recompensa (tiro ou encostar).</summary>
    public void Pop(bool byPlayer)
    {
        if (popped) return;
        popped = true;
        popTimer = PopTime;
        if (col != null) col.enabled = false;
        Active.Remove(this);
        onPop?.Invoke(this, byPlayer);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float t = Time.time + phase;
        if (popped)
        {
            popTimer -= dt;
            float k = 1f - Mathf.Clamp01(popTimer / PopTime);
            look?.Draw(diameter * (1f + 0.35f * k), t * 0.2f, Alpha * (1f - k));
            if (popTimer <= 0f) Destroy(gameObject);
            return;
        }

        age += dt;
        if (age >= lifetime) { Pop(false); return; }

        // Vai com o vento, sobe devagar e faz um zigue-zague suave
        Vector3 side = Vector3.Cross(Vector3.up, velocity).normalized;
        Vector3 drift = velocity + Vector3.up * (0.12f + Mathf.Sin(t * 1.3f) * 0.35f) + side * (Mathf.Sin(t * 0.8f) * 0.6f);
        transform.position += drift * dt;

        // Encostou no player: estoura
        if (player != null)
        {
            Vector3 chest = player.position + Vector3.up * 1f;
            float reach = diameter * 0.5f + TouchMargin;
            if ((transform.position - chest).sqrMagnitude <= reach * reach) { Pop(true); return; }
        }

        float appear = Mathf.Clamp01(age / 0.4f);
        float fadeOut = Mathf.Clamp01((lifetime - age) / 1.5f);
        look?.Draw(diameter * appear, IsRainbow ? t * 0.5f : t * 0.08f, Alpha * fadeOut, Mathf.Sin(t * 5f) * 0.05f,
            IsRainbow ? 0.75f : 0.35f);
    }

    float Alpha => IsRainbow ? 0.42f : 0.26f;
}
