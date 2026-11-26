using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ordem das fases da run (fase 1 = primeira da lista). Depois da última, volta para a primeira
/// com a dificuldade continuando a subir (o contador de fase não zera).
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Sequência de fases", fileName = "StageSequence")]
public class StageSequence : ScriptableObject
{
    [SerializeField] List<StageDefinition> stages = new();

    public IReadOnlyList<StageDefinition> Stages => stages;
    public int Count => stages.Count;

    /// <summary>Fase da run (1 = primeira); depois da última, recomeça a lista.</summary>
    public StageDefinition ForStage(int stage)
    {
        if (stages.Count == 0) return null;
        return stages[(Mathf.Max(1, stage) - 1) % stages.Count];
    }

    /// <summary>A fase é a última da lista (a próxima volta para o começo).</summary>
    public bool IsFinal(int stage) => stages.Count > 0 && (Mathf.Max(1, stage) - 1) % stages.Count == stages.Count - 1;
}
