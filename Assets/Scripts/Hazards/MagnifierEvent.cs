using System;
using UnityEngine;

/// <summary>
/// Evento aleatório: criança com lupa (issue #34). De tempos em tempos uma criança aponta
/// uma lupa gigante para o player e o raio de sol (SunSpot) persegue ele por alguns
/// segundos. Precisa de sol: só acontece de dia e sem chuva (senão tenta de novo logo depois).
/// </summary>
public class MagnifierEvent : MonoBehaviour
{
    [SerializeField] SunSpot spotPrefab;
    [Tooltip("Primeira lupa depois desse tempo de fase (mín, máx).")]
    [SerializeField] Vector2 firstDelay = new(45f, 80f);
    [Tooltip("Intervalo entre lupas (mín, máx).")]
    [SerializeField] Vector2 interval = new(80f, 130f);
    [Tooltip("Sem sol (noite/chuva): tenta de novo depois disso.")]
    [SerializeField, Min(1f)] float retryDelay = 6f;
    [Tooltip("Luz do dia mínima (DayNightCycle.Daylight) para a lupa funcionar.")]
    [SerializeField, Range(0f, 1f)] float minDaylight = 0.55f;

    public event Action<SunSpot> OnStarted;

    public SunSpot Active { get; private set; }
    public int Count { get; private set; }
    public float NextEventTime => nextAt;

    Transform player;
    DayNightCycle dayNight;
    RainEvent rain;
    float nextAt;

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        dayNight = FindAnyObjectByType<DayNightCycle>();
        rain = FindAnyObjectByType<RainEvent>();
        nextAt = Time.time + UnityEngine.Random.Range(firstDelay.x, firstDelay.y);
    }

    /// <summary>Tem sol para a lupa: de dia e sem chuva.</summary>
    public bool HasSun =>
        (dayNight == null || dayNight.Daylight >= minDaylight) &&
        (rain == null || !rain.IsRaining) && DayNightCycle.Overcast < 0.3f;

    void Update()
    {
        if (player == null || Active != null || Time.time < nextAt) return;
        var run = RunManager.Instance;
        if (run != null && (run.IsRunOver || run.IsStageComplete)) return;
        if (!HasSun) { nextAt = Time.time + retryDelay; return; }
        Trigger();
    }

    /// <summary>Aponta a lupa para o player agora (público para testes e depuração).</summary>
    public SunSpot Trigger()
    {
        if (Active != null) return Active;
        if (spotPrefab == null || player == null) return null;
        Active = Instantiate(spotPrefab, player.position, Quaternion.identity);
        Active.OnFinished += HandleFinished;
        Active.Begin(player);
        Count++;
        OnStarted?.Invoke(Active);
        return Active;
    }

    void HandleFinished(SunSpot s)
    {
        if (s != Active) return;
        Active = null;
        nextAt = Time.time + UnityEngine.Random.Range(interval.x, interval.y);
    }

#if UNITY_EDITOR
    public void EditorSetup(SunSpot prefab) => spotPrefab = prefab;
#endif
}
