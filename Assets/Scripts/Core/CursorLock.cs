using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Trava e esconde o cursor durante o jogo. Se o cursor escapar (alt-tab),
/// um clique trava de novo. Pause e fim de run soltam o cursor (Esc é do PauseMenu).
/// </summary>
public class CursorLock : MonoBehaviour
{
    [SerializeField] bool lockOnStart = true;

    void Start()
    {
        if (lockOnStart) SetLocked(true);
    }

    // Em menu (pause / fim de run / vitória) o cursor fica solto para clicar nos botões
    static bool InMenu => RunManager.InMenu;

    void Update()
    {
        if (InMenu) return;
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) SetLocked(true);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && lockOnStart && !InMenu) SetLocked(true);
    }

    public static void SetLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
