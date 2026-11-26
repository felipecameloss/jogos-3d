using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>Tela de fase concluída: estatísticas, Próxima fase (Enter) ou Menu principal.</summary>
[RequireComponent(typeof(UIDocument))]
public class VictoryScreen : MonoBehaviour
{
    public bool IsVisible { get; private set; }

    VisualElement doc, root;

    void OnEnable()
    {
        doc = GetComponent<UIDocument>().rootVisualElement;
        root = doc.Q("root");
        doc.Q<Button>("next").clicked += Next;
        doc.Q<Button>("main-menu").clicked += SceneLoader.LoadMainMenu;
        root.AddToClassList("hidden");
    }

    void Start()
    {
        if (RunManager.Instance != null) RunManager.Instance.OnStageCompleted += Show;
    }

    void OnDestroy()
    {
        if (RunManager.Instance != null) RunManager.Instance.OnStageCompleted -= Show;
    }

    void Show()
    {
        var rm = RunManager.Instance;
        int s = Mathf.FloorToInt(rm.RunTime);
        var def = StageDirector.Current;
        doc.Q<Label>("heading").text = def != null ? $"Fase {rm.Stage} concluída: {def.DisplayName}!" : $"Fase {rm.Stage} concluída!";
        doc.Q<Label>("time").text = $"{s / 60}:{s % 60:00}";
        doc.Q<Label>("difficulty").text = DifficultyManager.Instance != null ? DifficultyManager.Instance.LevelName : "—";
        doc.Q<Label>("kills").text = rm.Kills.ToString();
        var inv = rm.PlayerHealth != null ? rm.PlayerHealth.GetComponent<Inventory>() : null;
        doc.Q<Label>("items").text = (inv != null ? inv.TotalItems : 0).ToString();
        root.RemoveFromClassList("hidden");
        doc.Q<Button>("next").Focus();
        IsVisible = true;
    }

    void Next()
    {
        if (RunManager.Instance != null) RunManager.Instance.GoToNextStage();
    }

    void Update()
    {
        if (!IsVisible) return;
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Next();
    }
}
