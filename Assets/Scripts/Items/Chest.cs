using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Baú (tampinha de garrafa gigante virada para baixo). Interagir gasta migalhas,
/// a tampa sobe girando e um item sorteado pela raridade salta para fora.
/// O preço acompanha a dificuldade, igual à recompensa das migalhas.
/// </summary>
public class Chest : Interactable
{
    [SerializeField] string displayName = "Tampinha";
    [Tooltip("Preço com dificuldade 1 (multiplica pelo coeficiente).")]
    [SerializeField, Min(0)] int baseCost = 25;

    [Header("Pesos da raridade")]
    [SerializeField, Min(0f)] float commonWeight = 76f;
    [SerializeField, Min(0f)] float uncommonWeight = 19f;
    [SerializeField, Min(0f)] float legendaryWeight = 1f;
    [SerializeField, Min(0f)] float cursedWeight = 4f;
    [Tooltip("Equipamento ativo (lupa...).")]
    [SerializeField, Min(0f)] float equipmentWeight = 7f;

    [Header("Referências")]
    [SerializeField] ItemDatabase database;
    [SerializeField] ItemPickup pickupPrefab;
    [Tooltip("Parte que sobe e gira ao abrir.")]
    [SerializeField] Transform lid;
    [SerializeField] TextMesh costLabel;

    /// <summary>Abriu (baú, item sorteado).</summary>
    public event Action<Chest, ItemData> OnOpened;
    public static event Action<Chest, ItemData> OnAnyOpened;

    public bool IsOpen { get; private set; }
    public string DisplayName => displayName;
    public ItemPickup LastDrop { get; private set; }

    public int CurrentCost
    {
        get
        {
            float coeff = DifficultyManager.Instance != null ? DifficultyManager.Instance.Coefficient : 1f;
            return Mathf.RoundToInt(baseCost * coeff);
        }
    }

    Vector3 lidRestPos;
    Quaternion lidRestRot;
    Coroutine shake;
    PlayerWallet playerWallet;
    EquipmentSlot playerEquipment;

    void Awake()
    {
        if (lid != null) { lidRestPos = lid.localPosition; lidRestRot = lid.localRotation; }
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { playerWallet = p.GetComponent<PlayerWallet>(); playerEquipment = p.GetComponent<EquipmentSlot>(); }
    }

    public override bool CanInteract(Interactor who) => !IsOpen;

    public override string GetPrompt(Interactor who, out bool available)
    {
        int cost = CurrentCost;
        available = who.Wallet != null && who.Wallet.CanAfford(cost);
        return $"Abrir {displayName} ({cost} migalhas)";
    }

    public override void Interact(Interactor who)
    {
        if (IsOpen) return;
        if (who.Wallet == null || !who.Wallet.TrySpend(CurrentCost))
        {
            if (shake == null) shake = StartCoroutine(Shake()); // sem migalhas: só treme
            return;
        }
        Open(who.transform.position);
    }

    /// <summary>Sorteia um item pelos pesos deste baú (nunca o equipamento que o player já tem).</summary>
    public ItemData RollItem()
    {
        if (database == null) return null;
        var item = database.RandomWeighted(commonWeight, uncommonWeight, legendaryWeight, cursedWeight, equipmentWeight);
        if (item is EquipmentData && playerEquipment != null && playerEquipment.Current == item)
            item = database.RandomWeighted(commonWeight, uncommonWeight, legendaryWeight, cursedWeight);
        return item;
    }

    void Open(Vector3 towards)
    {
        IsOpen = true;
        if (costLabel != null) costLabel.gameObject.SetActive(false);
        var item = RollItem();
        StartCoroutine(OpenRoutine(item, towards));
        OnOpened?.Invoke(this, item);
        OnAnyOpened?.Invoke(this, item);
    }

    IEnumerator OpenRoutine(ItemData item, Vector3 towards)
    {
        // Tampa sobe girando e cai de lado
        const float up = 0.35f;
        for (float t = 0f; t < up; t += Time.deltaTime)
        {
            float k = t / up;
            if (lid != null)
            {
                lid.localPosition = lidRestPos + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 1.6f, -k * 1.2f);
                lid.localRotation = lidRestRot * Quaternion.Euler(-200f * k, 0f, 0f);
            }
            yield return null;
        }
        if (lid != null)
        {
            lid.localPosition = lidRestPos + new Vector3(0f, 0.05f, -1.2f);
            lid.localRotation = lidRestRot * Quaternion.Euler(-180f, 0f, 0f);
        }

        if (item == null || pickupPrefab == null) yield break;
        // Item salta na direção do player e cai na frente do baú
        Vector3 dir = towards - transform.position;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
        Vector3 land = transform.position + dir * 1.8f;
        if (Physics.Raycast(land + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) land = hit.point;
        LastDrop = ItemPickup.Spawn(pickupPrefab, item, transform.position + Vector3.up * 0.6f);
        LastDrop.Launch(land + Vector3.up * 1f);
    }

    IEnumerator Shake()
    {
        Quaternion rest = transform.rotation;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            transform.rotation = rest * Quaternion.Euler(0f, Mathf.Sin(t * 60f) * 6f * (1f - t / 0.3f), 0f);
            yield return null;
        }
        transform.rotation = rest;
        shake = null;
    }

    void LateUpdate()
    {
        if (costLabel == null || IsOpen) return;
        var cam = Camera.main;
        if (cam != null) costLabel.transform.rotation = cam.transform.rotation; // sempre de frente pra câmera
        int cost = CurrentCost;
        costLabel.text = cost.ToString();
        costLabel.color = playerWallet == null || playerWallet.CanAfford(cost) ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.4f, 0.35f);
    }

#if UNITY_EDITOR
    public void EditorSetup(string name, int cost, float c, float u, float l, ItemDatabase db, ItemPickup pickup, Transform lidT, TextMesh label)
    {
        displayName = name; baseCost = cost; commonWeight = c; uncommonWeight = u; legendaryWeight = l;
        database = db; pickupPrefab = pickup; lid = lidT; costLabel = label;
    }
#endif
}
