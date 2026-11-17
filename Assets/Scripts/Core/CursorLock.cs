using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Trava e esconde o cursor durante o jogo. Esc solta, clique trava de novo.
/// (O menu de pause vai assumir isso depois.)
/// </summary>
public class CursorLock : MonoBehaviour
{
    [SerializeField] bool lockOnStart = true;

    void Start()
    {
        if (lockOnStart) SetLocked(true);
    }

    // Depois que a run acaba o cursor fica solto para usar a tela de fim de run
    static bool RunOver => RunManager.Instance != null && RunManager.Instance.IsRunOver;

    void Update()
    {
        if (RunOver) return;
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) SetLocked(false);
        else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) SetLocked(true);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && lockOnStart && !RunOver) SetLocked(true);
    }

    public static void SetLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
