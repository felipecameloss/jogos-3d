using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Primária do besouro: chifrada corpo a corpo. Segurar o ataque golpeia sem parar
/// (cadência acompanha o attack speed); cada golpe acerta tudo numa caixa à frente
/// e dispara os itens "ao acertar". O 3º golpe seguido é mais forte e mais largo.
/// </summary>
public class BeetleHorn : MonoBehaviour, IHudAbility
{
    [SerializeField] InputActionReference attackAction;
    [Tooltip("Golpes por segundo com attack speed 1.")]
    [SerializeField, Min(0.1f)] float baseSwingsPerSecond = 1.9f;
    [Tooltip("Dano em múltiplos do dano do player (2.2 = 220%).")]
    [SerializeField, Min(0f)] float damageCoefficient = 2.2f;
    [SerializeField, Min(1f)] float finisherMultiplier = 1.8f;
    [Tooltip("Área do golpe à frente do player (largura, altura, comprimento).")]
    [SerializeField] Vector3 hitBox = new(3.2f, 2.6f, 3.6f);
    [Tooltip("Golpes sem atacar por mais que isso zeram o combo.")]
    [SerializeField, Min(0.1f)] float comboWindow = 0.9f;

    /// <summary>Golpeou (índice no combo: 1, 2, 3).</summary>
    public event Action<int> OnSwing;

    // HUD
    public int HudSlot => 0;
    public string HudName => "Chifrada";
    public string HudKey => "M1";
    public Color HudColor => new(0.95f, 0.72f, 0.2f);
    public float CooldownRemaining => Mathf.Max(0f, cooldown);
    public float CooldownDuration => 1f / SwingsPerSecond;

    public float SwingsPerSecond => baseSwingsPerSecond * (stats != null ? stats.AttackSpeed : 1f);
    public int Swings { get; private set; }
    /// <summary>Alvos acertados no último golpe (testes).</summary>
    public readonly List<Health> LastHits = new();

    CharacterStats stats;
    Health health;
    PlayerMovement movement;
    InsectPlayerVisual visual;
    Transform aimCamera;
    float cooldown, lastSwingTime = float.NegativeInfinity;
    int combo;
    readonly Collider[] overlap = new Collider[64];

    void Awake()
    {
        stats = GetComponent<CharacterStats>();
        health = GetComponent<Health>();
        movement = GetComponent<PlayerMovement>();
        if (Camera.main != null) aimCamera = Camera.main.transform;
    }

    void OnEnable() => attackAction?.action.Enable();

    /// <summary>Configura em código (PlayerCharacterSetup).</summary>
    public void Init(InputActionReference action, InsectPlayerVisual modelVisual)
    {
        attackAction = action;
        visual = modelVisual;
        if (attackAction != null && isActiveAndEnabled) attackAction.action.Enable();
    }

    bool CanSwing =>
        !RunManager.InMenu && (health == null || health.IsAlive) && (movement == null || movement.InputEnabled);

    void Update()
    {
        if (cooldown > 0f) cooldown -= Time.deltaTime;
        bool holding = attackAction != null && attackAction.action.IsPressed();
        if (!holding || !CanSwing)
        {
            if (cooldown < 0f) cooldown = 0f;
            return;
        }
        if (cooldown > 0f) return;
        Swing();
        cooldown += 1f / SwingsPerSecond;
    }

    /// <summary>Um golpe agora (ignora a cadência; testes).</summary>
    public void Swing()
    {
        combo = Time.time - lastSwingTime <= comboWindow ? combo % 3 + 1 : 1;
        lastSwingTime = Time.time;
        Swings++;
        bool finisher = combo == 3;

        Vector3 dir = AimForward;
        Vector3 box = finisher ? new Vector3(hitBox.x * 1.35f, hitBox.y, hitBox.z * 1.15f) : hitBox;
        float damage = (stats != null ? stats.Damage : 12f) * damageCoefficient * (finisher ? finisherMultiplier : 1f);
        Strike(dir, box, damage);

        if (visual != null) visual.Kick(finisher ? 0.55f : 0.32f, finisher ? 14f : 8f);
        // Arco do golpe: da direita para a esquerda (alterna) na frente do besouro
        Vector3 center = transform.position + Vector3.up * 0.9f;
        Vector3 side = Vector3.Cross(Vector3.up, dir) * (combo == 2 ? -1f : 1f);
        Color c = finisher ? new Color(1f, 0.85f, 0.3f, 0.9f) : new Color(1f, 0.95f, 0.75f, 0.7f);
        Vector3 prev = center + (dir * 0.6f + side * 1.4f) * box.z * 0.45f;
        for (int i = 1; i <= 5; i++)
        {
            float t = i / 5f;
            Vector3 p = center + (dir * (0.6f + Mathf.Sin(t * Mathf.PI) * 0.6f) + side * Mathf.Lerp(1.4f, -1.4f, t)) * box.z * 0.45f;
            ItemVfx.Line(prev, p, c, finisher ? 0.16f : 0.1f, 0.12f);
            prev = p;
        }
        if (finisher && LastHits.Count > 0) GameFeel.Shake(0.35f);
        OnSwing?.Invoke(combo);
    }

    void Strike(Vector3 dir, Vector3 box, float damage)
    {
        LastHits.Clear();
        Quaternion rot = Quaternion.LookRotation(dir);
        Vector3 center = transform.position + Vector3.up * (box.y * 0.5f) + dir * (box.z * 0.5f);
        int n = Physics.OverlapBoxNonAlloc(center, box * 0.5f, overlap, rot, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = overlap[i].GetComponentInParent<Health>();
            if (h == null || h == health || !h.IsAlive || LastHits.Contains(h)) continue;
            LastHits.Add(h);
            h.TakeDamage(new DamageInfo(damage, gameObject, overlap[i].bounds.center, dir));
        }
    }

    Vector3 AimForward
    {
        get
        {
            Vector3 f = aimCamera != null ? aimCamera.forward : transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : transform.forward;
        }
    }
}
