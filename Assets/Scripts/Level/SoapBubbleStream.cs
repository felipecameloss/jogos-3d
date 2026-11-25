using System.Collections;
using UnityEngine;

/// <summary>
/// De vez em quando um vizinho sopra bolhas de sabão por cima da cerca: uma leva de bolhas
/// atravessa o quintal na direção do player, na altura de um pulo. Estourar uma (tiro ou
/// encostando) solta uma migalha. A primeira leva de cada fase traz uma bolha furta-cor que
/// solta o item Bolha de sabão; as seguintes às vezes trazem outra.
/// </summary>
public class SoapBubbleStream : MonoBehaviour
{
    [Header("Recompensa")]
    [Tooltip("Item da bolha furta-cor (Bolha de sabão).")]
    [SerializeField] ItemData rewardItem;
    [SerializeField] ItemPickup pickupPrefab;
    [Tooltip("Migalhas por bolha comum estourada.")]
    [SerializeField, Min(0)] int crumbsPerBubble = 1;
    [Tooltip("Chance de uma leva depois da primeira também trazer uma bolha furta-cor.")]
    [SerializeField, Range(0f, 1f)] float laterRainbowChance = 0.2f;

    [Header("Levas")]
    [Tooltip("Segundos do começo da fase até a primeira leva.")]
    [SerializeField, Min(0f)] float firstStreamDelay = 35f;
    [Tooltip("Segundos entre uma leva e a próxima (sorteado entre os dois).")]
    [SerializeField] Vector2 streamInterval = new(70f, 100f);
    [SerializeField, Min(1)] int bubblesPerStream = 9;
    [SerializeField, Min(0.05f)] float spawnSpacing = 0.45f;

    [Header("Bolhas")]
    [Tooltip("Distância do centro do quintal até a cerca, de onde as bolhas vêm.")]
    [SerializeField, Min(5f)] float yardRadius = 50f;
    [SerializeField] Vector2 height = new(2.3f, 3.8f);
    [SerializeField] Vector2 speed = new(3f, 4.5f);
    [SerializeField] Vector2 diameter = new(0.9f, 1.4f);
    [SerializeField, Min(0.5f)] float rainbowDiameter = 1.8f;
    [SerializeField, Min(5f)] float lifetime = 40f;
    [Tooltip("Material transparente das bolhas (a cor vem do código).")]
    [SerializeField] Material bubbleMaterial;

    /// <summary>Levas sopradas nesta fase (testes).</summary>
    public int Streams { get; private set; }
    /// <summary>Bolhas furta-cor estouradas pelo player nesta fase (testes).</summary>
    public int RainbowsPopped { get; private set; }

    float nextStreamAt;
    CrumbDropper crumbs;

    void Start()
    {
        nextStreamAt = Time.time + firstStreamDelay;
        crumbs = FindAnyObjectByType<CrumbDropper>();
    }

    void Update()
    {
        if (Time.time < nextStreamAt) return;
        var run = RunManager.Instance;
        if (run != null && (run.IsRunOver || run.IsStageComplete)) return;
        BlowNow();
    }

    /// <summary>Sopra uma leva agora (e agenda a próxima).</summary>
    public void BlowNow()
    {
        nextStreamAt = Time.time + Random.Range(streamInterval.x, streamInterval.y);
        bool rainbow = rewardItem != null && (Streams == 0 || Random.value < laterRainbowChance);
        Streams++;
        StartCoroutine(Blow(rainbow));
    }

    IEnumerator Blow(bool withRainbow)
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        Vector3 target = p != null ? p.transform.position : transform.position;
        // Sai de um ponto qualquer da cerca e vai na direção de onde o player está agora
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 emitter = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * yardRadius;
        Vector3 flow = target - emitter;
        flow.y = 0f;
        flow = flow.sqrMagnitude > 0.01f ? flow.normalized : Vector3.forward;
        int rainbowAt = withRainbow ? Random.Range(bubblesPerStream / 3, bubblesPerStream - 1) : -1;

        for (int i = 0; i < bubblesPerStream; i++)
        {
            bool rainbow = i == rainbowAt;
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-14f, 14f), 0f) * flow;
            Vector3 pos = emitter + Vector3.up * Random.Range(height.x, height.y) + Random.insideUnitSphere * 0.6f;
            float d = rainbow ? rainbowDiameter : Random.Range(diameter.x, diameter.y);
            DriftingBubble.Spawn(pos, dir * Random.Range(speed.x, speed.y), d, rainbow, lifetime, bubbleMaterial, HandlePopped);
            yield return new WaitForSeconds(spawnSpacing);
        }
    }

    void HandlePopped(DriftingBubble bubble, bool byPlayer)
    {
        if (!byPlayer) return;
        Vector3 at = bubble.transform.position;
        if (bubble.IsRainbow)
        {
            RainbowsPopped++;
            DropReward(at);
            return;
        }
        if (crumbsPerBubble <= 0) return;
        if (crumbs == null) crumbs = FindAnyObjectByType<CrumbDropper>();
        if (crumbs != null) crumbs.Drop(crumbsPerBubble, at);
    }

    void DropReward(Vector3 at)
    {
        if (rewardItem == null || pickupPrefab == null) return;
        // Chão embaixo da bolha (ignora outras bolhas no caminho)
        Vector3 land = at;
        float best = float.MaxValue;
        foreach (var hit in Physics.RaycastAll(at, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponent<DriftingBubble>() != null || hit.distance >= best) continue;
            best = hit.distance;
            land = hit.point;
        }
        var drop = ItemPickup.Spawn(pickupPrefab, rewardItem, at);
        drop.Launch(land + Vector3.up, 0.7f, 0.6f);
    }
}
