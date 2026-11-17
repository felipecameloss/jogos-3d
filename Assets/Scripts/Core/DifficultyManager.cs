using System;
using UnityEngine;

/// <summary>
/// Dificuldade que só sobe com o tempo da run, como no Risk of Rain.
/// Coeficiente = 1 + scalingPerMinute * minutos. O spawner e os inimigos leem daqui.
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    [Tooltip("Quanto o coeficiente sobe por minuto de run.")]
    [SerializeField, Min(0f)] float scalingPerMinute = 0.35f;
    [Tooltip("De quanto em quanto de coeficiente muda o nível (o nome que aparece na HUD).")]
    [SerializeField, Min(0.05f)] float coefficientPerLevel = 0.75f;

    [Header("Escala dos inimigos (por ponto de coeficiente acima de 1)")]
    [SerializeField, Min(0f)] float enemyHealthPerPoint = 0.6f;
    [SerializeField, Min(0f)] float enemyDamagePerPoint = 0.3f;

    [Header("Debug")]
    [SerializeField] bool debugGui = true;

    static readonly string[] LevelNames =
        { "Fácil", "Médio", "Difícil", "Muito difícil", "Insano", "Impossível", "Vou morrer", "HAHAHAHA" };

    /// <summary>(nível novo, nome) quando o nível muda.</summary>
    public event Action<int, string> OnLevelChanged;

    public float RunTime => RunManager.Instance != null ? RunManager.Instance.RunTime : Time.timeSinceLevelLoad;
    public float Coefficient => 1f + scalingPerMinute * RunTime / 60f;
    public int Level => Mathf.FloorToInt((Coefficient - 1f) / coefficientPerLevel);
    public string LevelName => LevelNames[Mathf.Min(Level, LevelNames.Length - 1)];
    /// <summary>0..1 dentro do nível atual (barra de progresso da HUD).</summary>
    public float LevelProgress => Mathf.Repeat((Coefficient - 1f) / coefficientPerLevel, 1f);

    public float EnemyHealthMultiplier => 1f + (Coefficient - 1f) * enemyHealthPerPoint;
    public float EnemyDamageMultiplier => 1f + (Coefficient - 1f) * enemyDamagePerPoint;

    int lastLevel;

    void Awake()
    {
        if (Instance != null && Instance != this) { enabled = false; return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        int level = Level;
        if (level != lastLevel)
        {
            lastLevel = level;
            OnLevelChanged?.Invoke(level, LevelName);
        }
    }

    void OnGUI()
    {
        if (!debugGui) return;
        GUI.Label(new Rect(10, 30, 400, 25), $"Dificuldade: {LevelName} (x{Coefficient:F2})");
    }
}
