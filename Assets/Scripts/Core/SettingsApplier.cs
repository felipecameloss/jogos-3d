using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Na cena do jogo: aplica sensibilidade, inverter Y e campo de visão na câmera
/// sempre que as configurações mudam.
/// </summary>
public class SettingsApplier : MonoBehaviour
{
    [SerializeField] CinemachineInputAxisController lookInput;
    [SerializeField] CinemachineCamera playerCamera;

    void OnEnable() => GameSettings.Changed += Apply;
    void OnDisable() => GameSettings.Changed -= Apply;
    void Start() => Apply();

    public void Apply()
    {
        if (lookInput != null)
        {
            foreach (var c in lookInput.Controllers)
            {
                if (c.Name.Contains("Look Orbit X")) c.Input.Gain = GameSettings.Sensitivity;
                else if (c.Name.Contains("Look Orbit Y")) c.Input.Gain = -GameSettings.Sensitivity * (GameSettings.InvertY ? -1f : 1f);
            }
        }
        if (playerCamera != null) playerCamera.Lens.FieldOfView = GameSettings.FieldOfView;
    }
}
