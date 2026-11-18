using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Saída da fase (um cano gigante). Fluxo:
/// 1. Inativo: o player acha e aperta E para ativar.
/// 2. Carregando: só carrega com o player dentro da área; o spawner fica mais intenso.
///    Inimigos "bloqueadores" (chefe) precisam morrer antes de concluir.
/// 3. Pronto: o player aperta E para entrar → vitória da fase.
/// </summary>
[DefaultExecutionOrder(-50)] // escolhe o lugar antes do ChestSpawner (que evita a área do cano)
public class ExitPipe : Interactable
{
    public enum State { Idle, Charging, Ready, Used }

    public static ExitPipe Instance { get; private set; }

    [Header("Carregamento")]
    [Tooltip("Segundos dentro da área para carregar 100%.")]
    [SerializeField, Min(1f)] float chargeTime = 60f;
    [Tooltip("Raio da área em volta do cano.")]
    [SerializeField, Min(2f)] float zoneRadius = 10f;
    [Tooltip("Quanto o spawner acelera durante a ativação.")]
    [SerializeField, Min(1f)] float spawnIntensity = 2.5f;

    [Header("Posição")]
    [Tooltip("Escolhe um lugar aleatório do mapa ao começar.")]
    [SerializeField] bool randomizePosition = true;
    [SerializeField] Vector2 areaSize = new(80f, 80f);
    [Tooltip("Distância mínima do spawn do player.")]
    [SerializeField, Min(0f)] float minDistanceFromPlayer = 30f;

    [Header("Visual")]
    [Tooltip("Disco no chão mostrando a área (cor muda dentro/fora).")]
    [SerializeField] Renderer zoneRenderer;
    [SerializeField] Color zoneColorOutside = new(1f, 0.4f, 0.25f, 0.28f);
    [SerializeField] Color zoneColorInside = new(0.5f, 1f, 0.4f, 0.28f);
    [SerializeField] Color zoneColorReady = new(1f, 0.9f, 0.4f, 0.35f);

    /// <summary>Ativou (o chefe nasce aqui).</summary>
    public event Action<ExitPipe> OnActivated;
    /// <summary>Carga completa e bloqueadores mortos.</summary>
    public event Action<ExitPipe> OnReady;

    public State Current { get; private set; } = State.Idle;
    /// <summary>0..1</summary>
    public float Charge { get; private set; }
    public bool PlayerInZone { get; private set; }
    public float ZoneRadius => zoneRadius;
    public int BlockersAlive { get { int n = 0; foreach (var b in blockers) if (b != null && b.IsAlive) n++; return n; } }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    readonly List<EnemyBase> blockers = new();
    Transform player;
    EnemySpawner spawner;
    MaterialPropertyBlock block;

    void Awake()
    {
        Instance = this;
        block = new MaterialPropertyBlock();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
        spawner = FindAnyObjectByType<EnemySpawner>();
        if (randomizePosition) PlaceRandomly();
        if (zoneRenderer != null)
        {
            zoneRenderer.transform.localScale = new Vector3(zoneRadius * 2f, zoneRenderer.transform.localScale.y, zoneRadius * 2f);
            zoneRenderer.enabled = false;
        }
    }

    void PlaceRandomly()
    {
        var path = new NavMeshPath();
        Vector3 origin = player != null ? player.position : Vector3.zero;
        for (int i = 0; i < 60; i++)
        {
            var c = new Vector3(UnityEngine.Random.Range(-areaSize.x, areaSize.x) * 0.5f, 0f, UnityEngine.Random.Range(-areaSize.y, areaSize.y) * 0.5f);
            if (!NavMesh.SamplePosition(c, out var hit, 3f, NavMesh.AllAreas)) continue;
            if ((hit.position - origin).sqrMagnitude < minDistanceFromPlayer * minDistanceFromPlayer) continue;
            if (!NavMesh.CalculatePath(hit.position, origin, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
            transform.position = hit.position;
            // Boca do cano virada para o centro do mapa
            Vector3 toCenter = -hit.position; toCenter.y = 0f;
            if (toCenter.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toCenter);
            return;
        }
    }

    /// <summary>Inimigo que precisa morrer antes da saída concluir (chefe).</summary>
    public void RegisterBlocker(EnemyBase enemy)
    {
        if (enemy != null && !blockers.Contains(enemy)) blockers.Add(enemy);
    }

    // ---------- Interação ----------

    public override bool CanInteract(Interactor who) => Current == State.Idle || Current == State.Ready;

    public override string GetPrompt(Interactor who, out bool available)
    {
        available = true;
        return Current == State.Ready ? "Entrar no cano" : "Ativar o cano";
    }

    public override void Interact(Interactor who)
    {
        if (Current == State.Idle) Activate();
        else if (Current == State.Ready) Enter();
    }

    public void Activate()
    {
        if (Current != State.Idle) return;
        Current = State.Charging;
        Charge = 0f;
        if (zoneRenderer != null) zoneRenderer.enabled = true;
        if (spawner != null) spawner.Intensity = spawnIntensity;
        OnActivated?.Invoke(this);
    }

    void Enter()
    {
        Current = State.Used;
        if (RunManager.Instance != null) RunManager.Instance.CompleteStage();
    }

    void Update()
    {
        if (Current != State.Charging || player == null) { UpdateZoneColor(); return; }

        Vector3 d = player.position - transform.position;
        d.y = 0f;
        var health = player.GetComponent<Health>();
        PlayerInZone = d.sqrMagnitude <= zoneRadius * zoneRadius && (health == null || health.IsAlive);
        if (PlayerInZone) Charge = Mathf.Min(1f, Charge + Time.deltaTime / chargeTime);

        if (Charge >= 1f && BlockersAlive == 0)
        {
            Current = State.Ready;
            if (spawner != null) spawner.Intensity = 1f;
            OnReady?.Invoke(this);
        }
        UpdateZoneColor();
    }

    void UpdateZoneColor()
    {
        if (zoneRenderer == null || !zoneRenderer.enabled) return;
        Color c = Current == State.Ready || Current == State.Used ? zoneColorReady : PlayerInZone ? zoneColorInside : zoneColorOutside;
        zoneRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        zoneRenderer.SetPropertyBlock(block);
    }

    /// <summary>Texto do objetivo para a HUD.</summary>
    public string ObjectiveText => Current switch
    {
        State.Idle => "Encontre o cano e ative-o",
        State.Charging when Charge >= 1f && BlockersAlive > 0 => "Derrote o chefe!",
        State.Charging when !PlayerInZone => $"Volte para perto do cano! {Mathf.FloorToInt(Charge * 100f)}%",
        State.Charging => $"Ativando o cano: {Mathf.FloorToInt(Charge * 100f)}%",
        State.Ready => "Cano pronto! Entre nele (E)",
        _ => "",
    };

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, zoneRadius);
    }
}
