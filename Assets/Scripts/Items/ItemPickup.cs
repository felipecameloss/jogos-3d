using UnityEngine;

/// <summary>
/// Item no chão: gira, flutua, brilha na cor da raridade. O player pega encostando.
/// Baús e drops criam com ItemPickup.Spawn(prefab, item, posição).
/// </summary>
public class ItemPickup : MonoBehaviour
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
        age += Time.deltaTime;
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.position = basePos + Vector3.up * (Mathf.Sin(age * 2.5f) * bobHeight);

        if (item == null || inventory == null || age < pickupDelay) return;
        Vector3 d = player.position + Vector3.up - transform.position;
        if (d.sqrMagnitude <= pickupRadius * pickupRadius && inventory.Health.IsAlive)
        {
            inventory.Add(item);
            Destroy(gameObject);
        }
    }
}
