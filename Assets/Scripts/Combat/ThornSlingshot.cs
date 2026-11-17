using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Habilidade primária: estilingue de espinhos.
/// Segurar o ataque atira sem parar. A mira é o centro da tela: o espinho sai
/// do estilingue e vai até o ponto que a câmera está mirando.
/// </summary>
public class ThornSlingshot : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] InputActionReference attackAction;

    [Header("Referências")]
    [SerializeField] Projectile projectilePrefab;
    [Tooltip("De onde o espinho sai.")]
    [SerializeField] Transform muzzle;
    [Tooltip("Vazio = Camera.main.")]
    [SerializeField] Transform aimCamera;

    [Header("Tiro")]
    [Tooltip("Tiros por segundo com attack speed 1.")]
    [SerializeField, Min(0.1f)] float baseShotsPerSecond = 3f;
    [SerializeField, Min(1f)] float projectileSpeed = 70f;
    [Tooltip("Multiplica o dano do CharacterStats (estilo Risk of Rain: 100% de dano).")]
    [SerializeField, Min(0f)] float damageCoefficient = 1f;
    [SerializeField] float maxAimDistance = 200f;
    [SerializeField] LayerMask aimMask = ~0;
    [SerializeField, Min(0)] int poolPrewarm = 30;

    [Header("Debug")]
    [Tooltip("Mira simples até a HUD existir.")]
    [SerializeField] bool drawCrosshair = true;

    /// <summary>Acertou algo com vida (hook "ao acertar" dos itens: ferrão, fio elétrico...).</summary>
    public event Action<IDamageable, DamageInfo> OnHitTarget;
    /// <summary>Disparou um espinho (som, recuo, partícula).</summary>
    public event Action OnFired;

    public float ShotsPerSecond => baseShotsPerSecond * (stats != null ? stats.AttackSpeed : 1f);

    CharacterStats stats;
    Health health;
    ComponentPool<Projectile> pool;
    float cooldown;
    readonly RaycastHit[] aimHits = new RaycastHit[16];

    void Awake()
    {
        stats = GetComponent<CharacterStats>();
        health = GetComponent<Health>();
        if (aimCamera == null && Camera.main != null) aimCamera = Camera.main.transform;
        if (muzzle == null) muzzle = transform;
        if (projectilePrefab != null) pool = new ComponentPool<Projectile>(projectilePrefab, poolPrewarm, rootName: "[Pool] Thorn");
        else Debug.LogError("ThornSlingshot sem prefab de projétil.", this);
    }

    void OnEnable() => attackAction?.action.Enable();

    bool CanFire =>
        pool != null && aimCamera != null &&
        (health == null || health.IsAlive) &&
        (RunManager.Instance == null || !RunManager.Instance.IsRunOver);

    void Update()
    {
        if (cooldown > 0f) cooldown -= Time.deltaTime;
        bool holding = attackAction != null && attackAction.action.IsPressed();
        if (!holding || !CanFire)
        {
            if (cooldown < 0f) cooldown = 0f; // não acumula tiros enquanto não está atirando
            return;
        }

        float interval = 1f / ShotsPerSecond;
        int safety = 0; // attack speed muito alto: vários tiros no mesmo frame, com limite
        while (cooldown <= 0f && safety++ < 10)
        {
            Fire();
            cooldown += interval;
        }
    }

    void Fire()
    {
        Vector3 origin = muzzle.position;
        Vector3 aimPoint = GetAimPoint();
        Vector3 dir = (aimPoint - origin).normalized;
        // Mira muito perto/atrás do estilingue (parede colada na câmera): atira pra frente da câmera
        if ((aimPoint - origin).sqrMagnitude < 1f || Vector3.Dot(dir, aimCamera.forward) < 0.2f) dir = aimCamera.forward;

        float damage = (stats != null ? stats.Damage : 12f) * damageCoefficient;
        var p = pool.Get();
        p.Launch(origin, dir * projectileSpeed, damage, gameObject, pool.Release, HandleHit);
        OnFired?.Invoke();
    }

    void HandleHit(IDamageable target, DamageInfo info) => OnHitTarget?.Invoke(target, info);

    /// <summary>Ponto no mundo embaixo da mira (centro da tela), ignorando o próprio player.</summary>
    public Vector3 GetAimPoint()
    {
        Vector3 from = aimCamera.position, dir = aimCamera.forward;
        int count = Physics.RaycastNonAlloc(from, dir, aimHits, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore);
        float best = maxAimDistance;
        for (int i = 0; i < count; i++)
        {
            if (aimHits[i].collider.transform.IsChildOf(transform)) continue;
            if (aimHits[i].distance < best) best = aimHits[i].distance;
        }
        return from + dir * best;
    }

    void OnGUI()
    {
        if (!drawCrosshair || Cursor.lockState != CursorLockMode.Locked) return;
        float cx = Screen.width / 2f, cy = Screen.height / 2f;
        var tex = Texture2D.whiteTexture;
        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        GUI.DrawTexture(new Rect(cx - 9, cy - 1, 6, 2), tex);
        GUI.DrawTexture(new Rect(cx + 3, cy - 1, 6, 2), tex);
        GUI.DrawTexture(new Rect(cx - 1, cy - 9, 2, 6), tex);
        GUI.DrawTexture(new Rect(cx - 1, cy + 3, 2, 6), tex);
        GUI.color = Color.white;
    }
}
