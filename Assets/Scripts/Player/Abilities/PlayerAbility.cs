using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Base das habilidades com cooldown (agulha, dash, formigas aliadas).
/// Cuida da tecla, do cooldown e da HUD; a subclasse só implementa TryActivate.
/// </summary>
public abstract class PlayerAbility : MonoBehaviour, IHudAbility
{
    [SerializeField] protected InputActionReference action;
    [SerializeField, Min(0f)] protected float cooldown = 4f;

    [Header("HUD")]
    [SerializeField] int hudSlot = 1;
    [SerializeField] string hudName = "Habilidade";
    [SerializeField] string hudKey = "?";
    [SerializeField] Color hudColor = Color.white;

    public int HudSlot => hudSlot;
    public string HudName => hudName;
    public string HudKey => hudKey;
    public Color HudColor => hudColor;
    public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);
    public float CooldownDuration => cooldown;
    public bool IsReady => CooldownRemaining <= 0f;
    /// <summary>Quantas vezes foi usada (testes/depuração).</summary>
    public int Uses { get; private set; }
    public InputActionReference Action => action;

    /// <summary>
    /// Configura em código (habilidades do personagem escolhido, criadas com AddComponent).
    /// Liga a ação de input se ela ainda não estava ligada.
    /// </summary>
    public void Configure(InputActionReference inputAction, float cooldownSeconds, int slot, string displayName, string key, Color color)
    {
        action = inputAction;
        cooldown = Mathf.Max(0f, cooldownSeconds);
        hudSlot = slot;
        hudName = displayName;
        hudKey = key;
        hudColor = color;
        if (action != null && isActiveAndEnabled) action.action.Enable();
    }

    /// <summary>Muda só o que aparece na HUD e o cooldown (personagem que reaproveita a habilidade).</summary>
    public void SetHudAndCooldown(string displayName, Color color, float cooldownSeconds)
    {
        hudName = displayName;
        hudColor = color;
        cooldown = Mathf.Max(0f, cooldownSeconds);
    }

    protected Health Health { get; private set; }
    protected PlayerMovement Movement { get; private set; }
    protected CharacterStats Stats { get; private set; }
    protected Transform AimCamera { get; private set; }

    float readyAt;

    protected virtual void Awake()
    {
        Health = GetComponent<Health>();
        Movement = GetComponent<PlayerMovement>();
        Stats = GetComponent<CharacterStats>();
        if (Camera.main != null) AimCamera = Camera.main.transform;
    }

    protected virtual void OnEnable()
    {
        if (action != null) action.action.Enable();
    }

    protected virtual bool CanUse =>
        !RunManager.InMenu && (Health == null || Health.IsAlive) && (Movement == null || Movement.InputEnabled);

    void Update()
    {
        if (action != null && action.action.WasPressedThisFrame()) TryUse();
    }

    /// <summary>Usa se estiver pronta e permitida. Retorna se usou.</summary>
    public bool TryUse()
    {
        if (!IsReady || !CanUse || !TryActivate()) return false;
        readyAt = Time.time + cooldown;
        Uses++;
        return true;
    }

    /// <summary>Faz o efeito. Retorne false para não gastar o cooldown (ex.: sem espaço).</summary>
    protected abstract bool TryActivate();

    /// <summary>Frente da câmera no plano (para onde o player está mirando).</summary>
    protected Vector3 AimForward
    {
        get
        {
            Vector3 f = AimCamera != null ? AimCamera.forward : transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : transform.forward;
        }
    }
}
