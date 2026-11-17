using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Estado da run: tempo, fim da run quando o player morre, reinício.
/// A tela de fim de run de verdade vem na task "Menus e telas"; por enquanto há um OnGUI de debug.
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Tooltip("Vazio = procura o objeto com tag Player.")]
    [SerializeField] Health playerHealth;
    [Tooltip("Abaixo dessa altura o player morre (caiu do mapa).")]
    [SerializeField] float killHeight = -30f;
    [Tooltip("Mostra vida e 'você morreu' na tela até a HUD existir.")]
    [SerializeField] bool debugGui = true;

    /// <summary>Disparado uma vez quando a run acaba (player morreu).</summary>
    public event Action OnRunEnded;

    public bool IsRunOver { get; private set; }
    public float RunTime { get; private set; }
    public Health PlayerHealth => playerHealth;

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
    }

    void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnDeath -= HandlePlayerDeath;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (IsRunOver)
        {
            var kb = Keyboard.current;
            if (debugGui && kb != null && kb.rKey.wasPressedThisFrame) RestartRun();
            return;
        }

        RunTime += Time.deltaTime;
        if (playerHealth != null && playerHealth.transform.position.y < killHeight) playerHealth.Kill();
    }

    void HandlePlayerDeath(DamageInfo killingBlow)
    {
        if (IsRunOver) return;
        IsRunOver = true;
        if (playerMovement != null) playerMovement.InputEnabled = false;
        CursorLock.SetLocked(false);
        Debug.Log($"Run acabou em {RunTime:F1}s (golpe final: {killingBlow.Amount:F0} de {(killingBlow.Source != null ? killingBlow.Source.name : "ambiente")})");
        OnRunEnded?.Invoke();
    }

    public void RestartRun()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnGUI()
    {
        if (!debugGui || playerHealth == null) return;
        GUI.Label(new Rect(10, 10, 300, 25), $"Vida: {Mathf.CeilToInt(playerHealth.Current)} / {playerHealth.Max:F0}   Tempo: {RunTime:F0}s");
        if (IsRunOver)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0, Screen.height / 2f - 40, Screen.width, 80), "Você morreu — R para reiniciar", style);
        }
    }
}
