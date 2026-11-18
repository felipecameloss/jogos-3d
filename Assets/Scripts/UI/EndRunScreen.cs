using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Tela de fim de run: aparece um pouco depois do player morrer, com tempo,
/// dificuldade, kills, migalhas e itens. Jogar de novo (R / Enter) ou voltar ao menu.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class EndRunScreen : MonoBehaviour
{
    [Tooltip("Espera antes de mostrar (para ver a morte).")]
    [SerializeField, Min(0f)] float showDelay = 1.2f;

    public bool IsVisible { get; private set; }

    VisualElement doc, root;

    void OnEnable()
    {
        doc = GetComponent<UIDocument>().rootVisualElement;
        root = doc.Q("root");
        doc.Q<Button>("restart").clicked += Restart;
        doc.Q<Button>("main-menu").clicked += SceneLoader.LoadMainMenu;
        root.AddToClassList("hidden");
    }

    void Start()
    {
        if (RunManager.Instance != null) RunManager.Instance.OnRunEnded += HandleRunEnded;
    }

    void OnDestroy()
    {
        if (RunManager.Instance != null) RunManager.Instance.OnRunEnded -= HandleRunEnded;
    }

    void HandleRunEnded() => StartCoroutine(ShowAfterDelay());

    IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSecondsRealtime(showDelay);
        Show();
    }

    void Show()
    {
        var rm = RunManager.Instance;
        int seconds = rm != null ? Mathf.FloorToInt(rm.RunTime) : 0;
        doc.Q<Label>("heading").text = DeathTitle(rm != null ? rm.KillingBlow : default);
        doc.Q<Label>("time").text = $"{seconds / 60}:{seconds % 60:00}";
        doc.Q<Label>("difficulty").text = DifficultyManager.Instance != null ? DifficultyManager.Instance.LevelName : "—";
        doc.Q<Label>("kills").text = (rm != null ? rm.Kills : 0).ToString();
        var wallet = rm != null && rm.PlayerHealth != null ? rm.PlayerHealth.GetComponent<PlayerWallet>() : null;
        doc.Q<Label>("crumbs").text = (wallet != null ? wallet.TotalCollected : 0).ToString();
        doc.Q<Label>("items").text = "0"; // TODO: contar do inventário quando o sistema de itens existir (#44)

        root.RemoveFromClassList("hidden");
        doc.Q<Button>("restart").Focus();
        CursorLock.SetLocked(false);
        IsVisible = true;
    }

    /// <summary>Título conforme o que matou o player.</summary>
    static string DeathTitle(DamageInfo blow)
    {
        var src = blow.Source;
        if (src == null) return "Você caiu do mundo";
        if (src.GetComponentInParent<HumanStomp>() != null) return "Você foi esmagado";
        if (src.GetComponentInParent<SoldierAnt>() != null) return "Derrotado por uma soldado";
        if (src.GetComponentInParent<WorkerAnt>() != null) return "Virou comida de formiga";
        return "Você foi derrotado";
    }

    void Restart()
    {
        if (RunManager.Instance != null) RunManager.Instance.RestartRun();
    }

    void Update()
    {
        if (!IsVisible) return;
        var kb = Keyboard.current;
        if (kb != null && (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) Restart();
    }
}
