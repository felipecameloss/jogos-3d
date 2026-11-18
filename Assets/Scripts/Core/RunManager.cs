using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Estado da run: tempo, kills, fase, fim da run quando o player morre, reinício
/// e passagem para a próxima fase levando itens, migalhas, vida e tempo.
/// A tela de fim de run (EndRunScreen) escuta OnRunEnded; a de vitória, OnStageCompleted.
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    /// <summary>O que passa de uma fase para a outra. Null = run nova.</summary>
    class CarryOver
    {
        public float runTime;
        public int kills, stage, crumbs, crumbsTotal;
        public float healthFraction;
        public readonly List<(ItemData item, int count)> items = new();
        public EquipmentData equipment;
        public float equipmentCooldown;
    }
    static CarryOver carry;

    [Tooltip("Vazio = procura o objeto com tag Player.")]
    [SerializeField] Health playerHealth;
    [Tooltip("Abaixo dessa altura o player morre (caiu do mapa).")]
    [SerializeField] float killHeight = -30f;

    /// <summary>Disparado uma vez quando a run acaba (player morreu).</summary>
    public event Action OnRunEnded;
    /// <summary>Disparado quando o player entra na saída da fase.</summary>
    public event Action OnStageCompleted;

    public bool IsRunOver { get; private set; }
    /// <summary>Fase concluída, na tela de vitória (jogo congelado).</summary>
    public bool IsStageComplete { get; private set; }
    public float RunTime { get; private set; }
    /// <summary>1 na primeira fase; sobe a cada saída.</summary>
    public int Stage { get; private set; } = 1;
    public Health PlayerHealth => playerHealth;
    /// <summary>Inimigos mortos pelo player (tiros, itens dele).</summary>
    public int Kills { get; private set; }
    /// <summary>O golpe que matou o player (para o título da tela de fim de run).</summary>
    public DamageInfo KillingBlow { get; private set; }

    /// <summary>Em menu (pause, fim de run, vitória): sem controle do player nem cursor travado.</summary>
    public static bool InMenu =>
        PauseMenu.IsPaused || (Instance != null && (Instance.IsRunOver || Instance.IsStageComplete));

    PlayerMovement playerMovement;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Mais de um RunManager na cena; desativando o extra.", this);
            enabled = false;
            return;
        }
        Instance = this;

        if (playerHealth == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerHealth = player.GetComponent<Health>();
        }
        if (playerHealth == null)
        {
            Debug.LogError("RunManager: player sem componente Health.", this);
            return;
        }
        playerMovement = playerHealth.GetComponent<PlayerMovement>();
        playerHealth.OnDeath += HandlePlayerDeath;
        EnemyBase.OnAnyEnemyDied += HandleEnemyDied;

        if (carry != null)
        {
            RunTime = carry.runTime;
            Kills = carry.kills;
            Stage = carry.stage;
        }
    }

    void Start()
    {
        // Depois dos Awake de Inventory/Wallet/Health: devolve o que veio da fase anterior
        if (carry == null || playerHealth == null) return;
        var inventory = playerHealth.GetComponent<Inventory>();
        if (inventory != null) foreach (var (item, count) in carry.items) inventory.Add(item, count, silent: true);
        if (carry.equipment != null && playerHealth.TryGetComponent(out EquipmentSlot slot))
        {
            slot.Equip(carry.equipment, drop: false);
            slot.SetCooldownRemaining(carry.equipmentCooldown);
        }
        var wallet = playerHealth.GetComponent<PlayerWallet>();
        if (wallet != null) wallet.RestoreState(carry.crumbs, carry.crumbsTotal);
        playerHealth.SetCurrent(playerHealth.Max * carry.healthFraction);
        carry = null;
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnDeath -= HandlePlayerDeath;
        EnemyBase.OnAnyEnemyDied -= HandleEnemyDied;
        if (Instance == this) Instance = null;
    }

    void HandleEnemyDied(EnemyBase enemy, DamageInfo blow)
    {
        if (IsRunOver || playerHealth == null || blow.Source == null) return;
        if (blow.Source.transform.IsChildOf(playerHealth.transform)) Kills++;
    }

    void Update()
    {
        if (IsRunOver || IsStageComplete) return;

        RunTime += Time.deltaTime;
        if (playerHealth != null && playerHealth.transform.position.y < killHeight) playerHealth.Kill();
    }

    void HandlePlayerDeath(DamageInfo killingBlow)
    {
        if (IsRunOver) return;
        IsRunOver = true;
        KillingBlow = killingBlow;
        if (playerMovement != null) playerMovement.InputEnabled = false;
        CursorLock.SetLocked(false);
        Debug.Log($"Run acabou em {RunTime:F1}s (golpe final: {killingBlow.Amount:F0} de {(killingBlow.Source != null ? killingBlow.Source.name : "ambiente")})");
        OnRunEnded?.Invoke();
    }

    /// <summary>O player entrou na saída: congela o jogo e mostra a vitória.</summary>
    public void CompleteStage()
    {
        if (IsRunOver || IsStageComplete) return;
        IsStageComplete = true;
        if (playerMovement != null) playerMovement.InputEnabled = false;
        CursorLock.SetLocked(false);
        Time.timeScale = 0f;
        OnStageCompleted?.Invoke();
    }

    /// <summary>Vai para a próxima fase levando itens, migalhas, vida, tempo e kills.</summary>
    public void GoToNextStage()
    {
        var c = new CarryOver
        {
            runTime = RunTime, kills = Kills, stage = Stage + 1,
            healthFraction = playerHealth != null ? Mathf.Clamp01(playerHealth.Current / playerHealth.Max) : 1f,
        };
        var wallet = playerHealth != null ? playerHealth.GetComponent<PlayerWallet>() : null;
        if (wallet != null) { c.crumbs = wallet.Crumbs; c.crumbsTotal = wallet.TotalCollected; }
        var inventory = playerHealth != null ? playerHealth.GetComponent<Inventory>() : null;
        if (inventory != null) foreach (var item in inventory.Items) c.items.Add((item, inventory.GetCount(item)));
        if (playerHealth != null && playerHealth.TryGetComponent(out EquipmentSlot slot))
        {
            c.equipment = slot.Current;
            c.equipmentCooldown = slot.CooldownRemaining;
        }
        carry = c;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Run nova do zero (esquece o que vinha da fase anterior).</summary>
    public static void ClearCarryOver() => carry = null;

    public void RestartRun()
    {
        ClearCarryOver();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
