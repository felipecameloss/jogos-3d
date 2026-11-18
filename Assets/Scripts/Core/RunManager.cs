using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Estado da run: tempo, kills, fim da run quando o player morre, reinício.
/// A tela de fim de run (EndRunScreen) escuta OnRunEnded.
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Tooltip("Vazio = procura o objeto com tag Player.")]
    [SerializeField] Health playerHealth;
    [Tooltip("Abaixo dessa altura o player morre (caiu do mapa).")]
    [SerializeField] float killHeight = -30f;
    [Tooltip("Mostra vida e tempo na tela até a HUD existir.")]
    [SerializeField] bool debugGui = true;

    /// <summary>Disparado uma vez quando a run acaba (player morreu).</summary>
    public event Action OnRunEnded;

    public bool IsRunOver { get; private set; }
    public float RunTime { get; private set; }
    public Health PlayerHealth => playerHealth;
    /// <summary>Inimigos mortos pelo player (tiros, itens dele).</summary>
    public int Kills { get; private set; }
    /// <summary>O golpe que matou o player (para o título da tela de fim de run).</summary>
    public DamageInfo KillingBlow { get; private set; }

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
        if (IsRunOver) return;

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

    public void RestartRun()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnGUI()
    {
        if (!debugGui || playerHealth == null) return;
        GUI.Label(new Rect(10, 10, 300, 25), $"Vida: {Mathf.CeilToInt(playerHealth.Current)} / {playerHealth.Max:F0}   Tempo: {RunTime:F0}s");
    }
}
