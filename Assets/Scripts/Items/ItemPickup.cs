using UnityEngine;

/// <summary>
/// Item no chão: gira, flutua, brilha na cor da raridade. O player pega encostando.
/// Equipamento só é pego encostando se o slot estiver vazio; com o slot ocupado
/// vira um interagível (E) que troca e dropa o antigo.
/// Baús e drops criam com ItemPickup.Spawn(prefab, item, posição).
/// </summary>
public class ItemPickup : Interactable
{
    [SerializeField] ItemData item;
    [Tooltip("Partes que pegam a cor da raridade.")]
    [SerializeField] Renderer[] tinted;
    [SerializeField, Min(0.1f)] float pickupRadius = 1.4f;
    [SerializeField] float spinSpeed = 90f;
    [SerializeField] float bobHeight = 0.2f;
    [Tooltip("Tempo depois de aparecer antes de poder pegar (para ver saindo do baú).")]
    [SerializeField, Min(0f)] float pickupDelay = 0.4f;

    public ItemData Item => item;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    Transform player;
    Inventory inventory;
    Vector3 basePos;
    float age;
    // Lançamento em arco (saindo do baú)
    bool launching;
    Vector3 launchFrom, launchTo;
    float launchTime, launchDuration;

    public bool IsLaunching => launching;

    /// <summary>Salta em arco da posição atual até `to` (pode pegar só depois de cair).</summary>
    public void Launch(Vector3 to, float duration = 0.6f, float arcHeight = 1.8f)
    {
        launchFrom = transform.position;
        launchTo = to;
        launchDuration = Mathf.Max(0.05f, duration);
        launchTime = 0f;
        launchArc = arcHeight;
        launching = true;
        age = 0f;
    }
    float launchArc;

    EquipmentData Equipment => item as EquipmentData;
    EquipmentSlot Slot => inventory != null ? inventory.Equipment : null;
    /// <summary>Equipamento com o slot já ocupado: não pega encostando, só com E.</summary>
    bool IsSwapOnly => Equipment != null && Slot != null && Slot.HasEquipment;

    public override bool CanInteract(Interactor who) =>
        IsSwapOnly && !launching && age >= pickupDelay && Slot.Current != item;

    public override string GetPrompt(Interactor who, out bool available)
    {
        available = true;
        return $"Trocar {Slot.Current.DisplayName} por {item.DisplayName}";
    }

    public override void Interact(Interactor who)
    {
        if (!CanInteract(who) || !inventory.Health.IsAlive) return;
        inventory.Add(item); // equipa e dropa o antigo
        Destroy(gameObject);
    }

    public static ItemPickup Spawn(ItemPickup prefab, ItemData item, Vector3 position)
    {
        var p = Instantiate(prefab, position, Quaternion.identity);
        p.SetItem(item);
        return p;
    }

    public void SetItem(ItemData data)
    {
        item = data;
        name = data != null ? $"Pickup ({data.DisplayName})" : "Pickup (vazio)";
        ApplyColor();
    }

    void Start()
    {
        basePos = transform.position;
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { player = p.transform; inventory = p.GetComponent<Inventory>(); }
        ApplyColor();
    }

    void ApplyColor()
    {
        if (item == null || tinted == null) return;
        var block = new MaterialPropertyBlock();
        Color c = item.Color;
        foreach (var r in tinted)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            block.SetColor(EmissionId, c * 1.2f);
            r.SetPropertyBlock(block);
        }
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        if (launching)
        {
            launchTime += Time.deltaTime;
            float k = Mathf.Clamp01(launchTime / launchDuration);
            transform.position = Vector3.Lerp(launchFrom, launchTo, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * launchArc);
            if (k < 1f) return;
            launching = false;
            basePos = launchTo;
            age = 0f;
        }
        age += Time.deltaTime;
        transform.position = basePos + Vector3.up * (Mathf.Sin(age * 2.5f) * bobHeight);

        if (item == null || inventory == null || age < pickupDelay) return;
        if (IsSwapOnly) return; // espera o E
        Vector3 d = player.position + Vector3.up - transform.position;
        if (d.sqrMagnitude <= pickupRadius * pickupRadius && inventory.Health.IsAlive)
        {
            inventory.Add(item);
            Destroy(gameObject);
        }
    }
}
