using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Algo que o player usa apertando Interagir (E): baús, santuários, joaninhas...
/// O Interactor do player acha o mais perto e mostra o texto de GetPrompt.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    /// <summary>Todos os interagíveis ativos (o Interactor procura aqui).</summary>
    public static readonly List<Interactable> All = new();

    [Tooltip("Altura do ponto usado para medir a distância até o player.")]
    [SerializeField] protected float focusHeight = 0.5f;

    public Vector3 FocusPoint => transform.position + Vector3.up * focusHeight;

    protected virtual void OnEnable() => All.Add(this);
    protected virtual void OnDisable() => All.Remove(this);

    /// <summary>Pode ser usado agora (ex.: baú ainda fechado).</summary>
    public abstract bool CanInteract(Interactor who);

    /// <summary>Texto do aviso. `available` = false pinta de vermelho (ex.: sem migalhas).</summary>
    public abstract string GetPrompt(Interactor who, out bool available);

    public abstract void Interact(Interactor who);
}
