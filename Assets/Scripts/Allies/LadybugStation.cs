using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Margarida gigante onde o player compra joaninhas aliadas com migalhas (E).
/// Cada joaninha que ele já tem encarece a próxima; com o enxame cheio, não vende mais.
/// Tem joaninhas pousadas nas pétalas: a comprada sai voando de uma delas até o player.
/// Montada em código pelo LadybugStationSpawner (não precisa de prefab).
/// </summary>
public class LadybugStation : Interactable
{
    const int Perched = 3;
    const float PerchRespawn = 4f;
    const float LabelHeight = 2.15f;

    static readonly Color Affordable = new(0.55f, 1f, 0.65f);
    static readonly Color TooExpensive = new(1f, 0.4f, 0.35f);
    static readonly Color Feedback = new(1f, 0.92f, 0.5f);

    LadybugConfig config;
    TextMesh label;
    readonly List<Transform> perched = new();
    readonly float[] perchHiddenUntil = new float[Perched];
    int nextPerch;
    GameObject player;
    PlayerWallet playerWallet;
    float feedbackUntil;
    string feedbackText;
    Coroutine shake;

    /// <summary>Compras feitas nesta margarida (testes).</summary>
    public int Purchases { get; private set; }

    public static LadybugStation Spawn(Vector3 position, Quaternion rotation, LadybugConfig config, Font font, Transform parent = null)
    {
        var go = new GameObject("LadybugDaisy");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        var station = go.AddComponent<LadybugStation>();
        station.config = config;
        station.focusHeight = 0.7f;
        station.Build(font);
        return station;
    }

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) playerWallet = player.GetComponent<PlayerWallet>();
    }

    /// <summary>Joaninhas que esse player já tem.</summary>
    static int Owned(GameObject who) => who != null && who.TryGetComponent(out LadybugSwarm s) ? s.Count : 0;

    /// <summary>Preço da próxima joaninha para esse player.</summary>
    public int CostFor(GameObject who) => config != null ? config.CostFor(Owned(who)) : int.MaxValue;

    bool IsFullFor(GameObject who) => config != null && Owned(who) >= config.maxLadybugs;

    public override bool CanInteract(Interactor who) => config != null;

    public override string GetPrompt(Interactor who, out bool available)
    {
        if (IsFullFor(who.gameObject))
        {
            available = false;
            return $"Joaninhas no máximo ({config.maxLadybugs}/{config.maxLadybugs})";
        }
        int cost = CostFor(who.gameObject);
        available = who.Wallet != null && who.Wallet.CanAfford(cost);
        return $"Comprar joaninha ({cost} migalhas)";
    }

    public override void Interact(Interactor who)
    {
        if (config == null) return;
        int cost = CostFor(who.gameObject);
        if (IsFullFor(who.gameObject) || who.Wallet == null || !who.Wallet.TrySpend(cost))
        {
            TriggerShake();
            return;
        }

        // Sai voando de uma das joaninhas pousadas (que some até "nascer" outra)
        int perch = nextPerch;
        nextPerch = (nextPerch + 1) % Perched;
        Vector3 from = perch < perched.Count && perched[perch] != null ? perched[perch].position : transform.position + Vector3.up * 1.4f;
        perchHiddenUntil[perch] = Time.time + PerchRespawn;
        if (perch < perched.Count && perched[perch] != null) perched[perch].gameObject.SetActive(false);

        LadybugSwarm.For(who.gameObject).Add(config, from);
        Purchases++;
        feedbackText = "+1 joaninha!";
        feedbackUntil = Time.time + 1.2f;
    }

    // ---------- Visual ----------

    void Build(Font font)
    {
        var material = config != null ? config.bodyMaterial : null;
        LadybugMeshes.CreateDaisy(transform, material);
        for (int i = 0; i < Perched; i++)
        {
            var pose = LadybugMeshes.PerchPose(i, Perched);
            var holder = new GameObject($"Perched {i + 1}").transform;
            holder.SetParent(transform, false);
            holder.SetLocalPositionAndRotation(pose.position, pose.rotation);
            LadybugMeshes.CreateLadybug(holder, material, 0.8f);
            perched.Add(holder);
        }

        // Colisor só no talo: dá para passar por baixo das pétalas
        var col = gameObject.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.65f, 0f);
        col.radius = 0.18f;
        col.height = 1.3f;

        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var labelGo = new GameObject("CostLabel");
        labelGo.transform.SetParent(transform, false);
        labelGo.transform.localPosition = Vector3.up * LabelHeight;
        label = labelGo.AddComponent<TextMesh>();
        label.font = font;
        label.fontSize = 64;
        label.fontStyle = FontStyle.Bold;
        label.characterSize = 0.055f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        var mr = labelGo.GetComponent<MeshRenderer>();
        if (font != null) mr.sharedMaterial = font.material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
        // Joaninhas pousadas: mexem um pouquinho e voltam depois de uma sair voando
        for (int i = 0; i < perched.Count; i++)
        {
            var p = perched[i];
            if (p == null) continue;
            if (!p.gameObject.activeSelf && Time.time >= perchHiddenUntil[i]) p.gameObject.SetActive(true);
            if (p.childCount > 0) p.GetChild(0).localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 1.3f + i * 2f) * 20f, 0f);
        }

        if (label == null) return;
        var cam = Camera.main;
        if (cam != null) label.transform.rotation = cam.transform.rotation;

        if (Time.time < feedbackUntil)
        {
            label.text = feedbackText;
            label.color = Feedback;
            return;
        }
        if (IsFullFor(player))
        {
            label.text = "Joaninhas\ncheio";
            label.color = TooExpensive;
            return;
        }
        int cost = CostFor(player);
        label.text = $"Joaninha\n{cost}";
        label.color = playerWallet == null || playerWallet.CanAfford(cost) ? Affordable : TooExpensive;
    }
}
