using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Santuário interagível (Tampinha da sorte e Santuário de sangue).
/// </summary>
public class Shrine : Interactable
{
    public enum ShrineType { Chance, Blood }

    [SerializeField] ShrineType shrineType = ShrineType.Chance;
    [SerializeField] string displayName = "Tampinha da sorte";
    [Tooltip("Para sorte: máximo de prêmios; para sangue: máximo de usos.")]
    [SerializeField, Min(1)] int maxSuccesses = 2;

    [Header("Tampinha da sorte")]
    [SerializeField, Min(1)] int baseCost = 20;
    [SerializeField, Min(1f)] float costScalePerUse = 1.4f;
    [SerializeField, Range(0f, 1f)] float winChance = 0.55f;
    [SerializeField] ItemDatabase database;
    [SerializeField] ItemPickup pickupPrefab;

    [Header("Santuário de sangue")]
    [SerializeField, Range(0.05f, 0.9f)] float healthCostFraction = 0.45f;
    [SerializeField, Min(1)] int baseCrumbReward = 36;

    [Header("Visual")]
    [SerializeField] Transform crystal;
    [SerializeField] Renderer crystalRenderer;
    [SerializeField] TextMesh costLabel;

    public static event Action<Shrine, bool> OnAnyUsed;

    public ShrineType Type => shrineType;
    public int Uses { get; private set; }
    public int Successes { get; private set; }
    public bool IsExhausted => Successes >= maxSuccesses;

    public int CurrentCost
    {
        get
        {
            float coeff = DifficultyManager.Instance != null ? DifficultyManager.Instance.Coefficient : 1f;
            return Mathf.RoundToInt(baseCost * coeff * Mathf.Pow(costScalePerUse, Uses));
        }
    }

    PlayerWallet playerWallet;
    Health playerHealth;
    CrumbDropper crumbDropper;
    Coroutine shake;
    float feedbackUntil;
    string feedbackText;
    Vector3 crystalRestPos;

    void Awake()
    {
        if (crystal != null) crystalRestPos = crystal.localPosition;
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            playerWallet = p.GetComponent<PlayerWallet>();
            playerHealth = p.GetComponent<Health>();
        }
        crumbDropper = FindAnyObjectByType<CrumbDropper>();
    }

    public override bool CanInteract(Interactor who) => !IsExhausted;

    public override string GetPrompt(Interactor who, out bool available)
    {
        if (shrineType == ShrineType.Chance)
        {
            int cost = CurrentCost;
            available = who.Wallet != null && who.Wallet.CanAfford(cost);
            return $"Tentar {displayName} ({cost} migalhas)";
        }

        var h = who.GetComponent<Health>();
        float hpCost = h != null ? h.Max * healthCostFraction : 0f;
        available = h != null && h.IsAlive && h.Current > hpCost + 1f;
        return $"Oferecer {Mathf.RoundToInt(healthCostFraction * 100f)}% vida ({displayName})";
    }

    public override void Interact(Interactor who)
    {
        if (IsExhausted) return;
        if (shrineType == ShrineType.Chance) UseChance(who);
        else UseBlood(who);
    }

    void UseChance(Interactor who)
    {
        int cost = CurrentCost;
        if (who.Wallet == null || !who.Wallet.TrySpend(cost))
        {
            TriggerShake();
            return;
        }

        Uses++;
        bool won = UnityEngine.Random.value < winChance;
        if (won)
        {
            Successes++;
            DropRewardItem(who.transform.position);
            feedbackText = "Sorte!";
        }
        else
        {
            TriggerShake();
            feedbackText = "Nada...";
        }
        feedbackUntil = Time.time + 1.2f;
        if (IsExhausted) Extinguish();
        OnAnyUsed?.Invoke(this, won);
    }

    void UseBlood(Interactor who)
    {
        var h = who.GetComponent<Health>();
        if (h == null || !h.IsAlive) return;
        float hpCost = h.Max * healthCostFraction;
        if (h.Current <= hpCost + 1f)
        {
            TriggerShake();
            return;
        }

        h.TakeDamage(new DamageInfo(hpCost, gameObject, who.transform.position + Vector3.up, Vector3.zero)
        {
            IgnoreArmor = true,
            NoProc = true,
        });

        Uses++;
        Successes++;
        float coeff = DifficultyManager.Instance != null ? DifficultyManager.Instance.Coefficient : 1f;
        int reward = Mathf.RoundToInt(baseCrumbReward * coeff * (1f + 0.45f * (Uses - 1)));
        if (crumbDropper == null) crumbDropper = FindAnyObjectByType<CrumbDropper>();
        if (crumbDropper != null) crumbDropper.Drop(reward, transform.position + Vector3.up * 1.4f);
        else if (who.Wallet != null) who.Wallet.Add(reward);

        feedbackText = $"+{reward}";
        feedbackUntil = Time.time + 1.2f;
        if (IsExhausted) Extinguish();
        OnAnyUsed?.Invoke(this, true);
    }

    void DropRewardItem(Vector3 towards)
    {
        if (database == null || pickupPrefab == null) return;
        var item = database.RandomWeighted(65f, 22f, 3f, 10f);
        if (item == null) return;

        Vector3 dir = towards - transform.position;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : transform.forward;
        Vector3 land = transform.position + dir * 1.8f;
        if (Physics.Raycast(land + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) land = hit.point;
        var drop = ItemPickup.Spawn(pickupPrefab, item, transform.position + Vector3.up * 1.3f);
        drop.Launch(land + Vector3.up);
    }

    void Extinguish()
    {
        if (crystal != null) crystal.gameObject.SetActive(false);
        if (costLabel != null) costLabel.gameObject.SetActive(false);
    }

    void TriggerShake()
    {
        if (shake == null) shake = StartCoroutine(ShakeRoutine());
    }

    IEnumerator ShakeRoutine()
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
        if (IsExhausted) return;

        if (crystal != null)
        {
            crystal.Rotate(0f, 55f * Time.deltaTime, 0f, Space.World);
            crystal.localPosition = crystalRestPos + Vector3.up * (Mathf.Sin(Time.time * 2.4f) * 0.09f);
        }

        if (costLabel == null) return;
        var cam = Camera.main;
        if (cam != null) costLabel.transform.rotation = cam.transform.rotation;

        if (Time.time < feedbackUntil)
        {
            costLabel.text = feedbackText;
            costLabel.color = new Color(1f, 0.92f, 0.5f);
            return;
        }

        if (shrineType == ShrineType.Chance)
        {
            int cost = CurrentCost;
            costLabel.text = $"{cost}";
            costLabel.color = playerWallet == null || playerWallet.CanAfford(cost)
                ? new Color(0.55f, 1f, 0.65f)
                : new Color(1f, 0.4f, 0.35f);
        }
        else
        {
            float hpCost = playerHealth != null ? playerHealth.Max * healthCostFraction : 0f;
            costLabel.text = $"-{Mathf.RoundToInt(healthCostFraction * 100f)}% HP";
            costLabel.color = playerHealth == null || playerHealth.Current > hpCost + 1f
                ? new Color(1f, 0.35f, 0.35f)
                : new Color(0.6f, 0.35f, 0.35f);
        }
    }
}
