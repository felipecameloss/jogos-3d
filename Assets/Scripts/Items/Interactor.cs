using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// No player: acha o interagível mais perto e usa com Interagir (E / botão Norte).
/// A HUD mostra o aviso de Current.
/// </summary>
public class Interactor : MonoBehaviour
{
    [SerializeField] InputActionReference interactAction;
    [SerializeField, Min(0.5f)] float range = 2.8f;

    /// <summary>O interagível em foco agora (null se nenhum).</summary>
    public Interactable Current { get; private set; }
    public PlayerWallet Wallet { get; private set; }
    public Inventory Inventory { get; private set; }

    Health health;

    void Awake()
    {
        Wallet = GetComponent<PlayerWallet>();
        Inventory = GetComponent<Inventory>();
        health = GetComponent<Health>();
    }

    void OnEnable() => interactAction?.action.Enable();

    bool Blocked =>
        PauseMenu.IsPaused || (health != null && !health.IsAlive) ||
        (RunManager.Instance != null && RunManager.Instance.IsRunOver);

    void Update()
    {
        Current = Blocked ? null : FindNearest();
        if (Current != null && interactAction != null && interactAction.action.WasPressedThisFrame())
            Current.Interact(this);
    }

    Interactable FindNearest()
    {
        Vector3 me = transform.position + Vector3.up;
        Interactable best = null;
        float bestSq = range * range;
        foreach (var i in Interactable.All)
        {
            if (i == null || !i.CanInteract(this)) continue;
            float sq = (i.FocusPoint - me).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = i; }
        }
        return best;
    }

    /// <summary>Usa o interagível em foco como se tivesse apertado E (testes, atalhos).</summary>
    public bool TryInteract()
    {
        Current = Blocked ? null : FindNearest();
        if (Current == null) return false;
        Current.Interact(this);
        return true;
    }

}
