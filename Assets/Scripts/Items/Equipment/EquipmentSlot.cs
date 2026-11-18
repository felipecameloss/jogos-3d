using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// No player: o slot de equipamento ativo. Um equipamento por vez, usado com
/// Q (ou gatilho direito), com cooldown mostrado na HUD. Pegar outro troca e
/// dropa o antigo no chão. O cooldown restante continua valendo depois da
/// troca (senão dava para trocar e destrocar para zerar).
/// </summary>
public class EquipmentSlot : MonoBehaviour, IHudAbility
{
    [SerializeField] InputActionReference action;
    [Tooltip("Pickup usado para dropar o equipamento antigo na troca.")]
    [SerializeField] ItemPickup pickupPrefab;
    [Tooltip("Equipamento inicial (testes). Vazio = começa sem.")]
    [SerializeField] EquipmentData startingEquipment;

    [Header("HUD")]
    [SerializeField] int hudSlot = 4;
    [SerializeField] string hudKey = "Q";
    [SerializeField] Color emptyColor = new(0.45f, 0.45f, 0.45f);

    /// <summary>(novo, antigo) sempre que troca. Novo pode ser null (largou).</summary>
    public event Action<EquipmentData, EquipmentData> OnEquipmentChanged;
    /// <summary>Usou o equipamento.</summary>
    public event Action<EquipmentData> OnUsed;

    public EquipmentData Current { get; private set; }
    public bool HasEquipment => Current != null;
    public int Uses { get; private set; }

    public Inventory Inventory { get; private set; }
    public Health Health { get; private set; }
    public CharacterStats Stats { get; private set; }
    public PlayerMovement Movement { get; private set; }
    public ThornSlingshot Slingshot { get; private set; }

    // IHudAbility
    public int HudSlot => hudSlot;
    public string HudName => Current != null ? Current.DisplayName : "Vazio";
    public string HudKey => hudKey;
    public Color HudColor => Current != null ? Current.Color : emptyColor;
    public float CooldownRemaining => Current != null ? Mathf.Max(0f, readyAt - Time.time) : 0f;
    public float CooldownDuration => Current != null ? Mathf.Max(CooldownRemaining, Current.Cooldown) : 0f;
    public bool IsReady => Current != null && CooldownRemaining <= 0f;

    float readyAt;

    void Awake()
    {
        Inventory = GetComponent<Inventory>();
        Health = GetComponent<Health>();
        Stats = GetComponent<CharacterStats>();
        Movement = GetComponent<PlayerMovement>();
        Slingshot = GetComponent<ThornSlingshot>();
    }

    void OnEnable()
    {
        if (action != null) action.action.Enable();
    }

    void Start()
    {
        if (startingEquipment != null && Current == null) Equip(startingEquipment, drop: false);
    }

    bool CanUse => !RunManager.InMenu && (Health == null || Health.IsAlive) && (Movement == null || Movement.InputEnabled);

    void Update()
    {
        if (action != null && action.action.WasPressedThisFrame()) TryUse();
    }

    /// <summary>Usa se tiver equipamento, estiver pronto e permitido. Retorna se usou.</summary>
    public bool TryUse()
    {
        if (!IsReady || !CanUse) return false;
        var eq = Current;
        bool used;
        try { used = eq.Activate(this); }
        catch (Exception e) { Debug.LogException(e, eq); used = false; }
        if (!used) return false;
        readyAt = Time.time + eq.Cooldown;
        Uses++;
        OnUsed?.Invoke(eq);
        return true;
    }

    /// <summary>
    /// Coloca o equipamento no slot. Se já tinha outro e `drop`, o antigo cai no chão
    /// atrás do player. Retorna o antigo (null se o slot estava vazio).
    /// </summary>
    public EquipmentData Equip(EquipmentData equipment, bool drop = true)
    {
        var old = Current;
        if (equipment == old) return null;
        Current = equipment;
        if (old != null && drop) DropOnGround(old);
        OnEquipmentChanged?.Invoke(equipment, old);
        return old;
    }

    /// <summary>Volta o cooldown ao normal (passar de fase, testes).</summary>
    public void ResetCooldown() => readyAt = 0f;

    /// <summary>Continua um cooldown que vinha de antes (passar de fase).</summary>
    public void SetCooldownRemaining(float seconds) => readyAt = Time.time + Mathf.Max(0f, seconds);

    void DropOnGround(EquipmentData equipment)
    {
        if (pickupPrefab == null) return;
        Vector3 back = -transform.forward;
        back.y = 0f;
        back = back.sqrMagnitude > 0.01f ? back.normalized : Vector3.back;
        Vector3 land = transform.position + back * 1.8f;
        if (Physics.Raycast(land + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) land = hit.point;
        var p = ItemPickup.Spawn(pickupPrefab, equipment, transform.position + Vector3.up * 1.2f);
        p.Launch(land + Vector3.up);
    }

    // ---------- Ajudantes para os equipamentos ----------

    /// <summary>Ponto do peito do player (de onde saem raios, sprays...).</summary>
    public Vector3 Chest => transform.position + Vector3.up * 1.2f;

    /// <summary>Para onde o player está mirando (ponto no mundo).</summary>
    public Vector3 AimPoint
    {
        get
        {
            if (Slingshot != null) return Slingshot.GetAimPoint();
            var cam = Camera.main;
            return cam != null ? cam.transform.position + cam.transform.forward * 40f : Chest + transform.forward * 40f;
        }
    }

    public float PlayerDamage => Stats != null ? Stats.Damage : 12f;

#if UNITY_EDITOR
    public void EditorSetup(InputActionReference useAction, ItemPickup pickup)
    {
        action = useAction; pickupPrefab = pickup;
    }
#endif
}
