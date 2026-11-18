using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Números de dano que sobem e somem em cima de quem levou o golpe (UI Toolkit, nítido em qualquer resolução).
/// Pool de labels; no máximo `maxNumbers` na tela.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class DamageNumbers : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float lifetime = 0.75f;
    [SerializeField, Min(0f)] float risePixels = 60f;
    [SerializeField, Min(4)] int maxNumbers = 40;

    class Number { public Label label; public Vector3 world; public float born; public float drift; public bool alive; }

    readonly List<Number> numbers = new();
    VisualElement root;

    public int ActiveCount { get { int n = 0; foreach (var x in numbers) if (x.alive) n++; return n; } }
    public string LastText { get; private set; }

    void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.style.position = Position.Absolute;
        root.style.left = 0; root.style.right = 0; root.style.top = 0; root.style.bottom = 0;
    }

    public void Spawn(Vector3 worldPos, float amount, Color color, float scale = 1f)
    {
        if (root == null || root.panel == null || !GameSettings.DamageNumbers) return;
        var n = Get();
        string text = amount >= 10f ? Mathf.RoundToInt(amount).ToString() : amount.ToString("0.#");
        n.label.text = text;
        n.label.style.color = color;
        n.label.style.fontSize = 22f * scale;
        n.world = worldPos;
        n.born = Time.unscaledTime;
        n.drift = Random.Range(-18f, 18f);
        n.alive = true;
        n.label.style.display = DisplayStyle.Flex;
        LastText = text;
        Place(n, 0f);
    }

    Number Get()
    {
        foreach (var x in numbers) if (!x.alive) return x;
        if (numbers.Count >= maxNumbers)
        {
            // Recicla o mais velho
            Number oldest = numbers[0];
            foreach (var x in numbers) if (x.born < oldest.born) oldest = x;
            return oldest;
        }
        var label = new Label { pickingMode = PickingMode.Ignore };
        label.style.position = Position.Absolute;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.textShadow = new TextShadow { offset = new Vector2(1.5f, 1.5f), color = new Color(0, 0, 0, 0.85f) };
        root.Add(label);
        var n = new Number { label = label };
        numbers.Add(n);
        return n;
    }

    void LateUpdate()
    {
        foreach (var n in numbers)
        {
            if (!n.alive) continue;
            float k = (Time.unscaledTime - n.born) / lifetime;
            if (k >= 1f) { n.alive = false; n.label.style.display = DisplayStyle.None; continue; }
            Place(n, k);
        }
    }

    void Place(Number n, float k)
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 vp = cam.WorldToViewportPoint(n.world);
        if (vp.z <= 0f) { n.label.style.display = DisplayStyle.None; return; } // atrás da câmera
        n.label.style.display = DisplayStyle.Flex;
        Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, n.world, cam);
        float ease = 1f - (1f - k) * (1f - k);
        n.label.style.left = panelPos.x - 40f + n.drift * ease;
        n.label.style.top = panelPos.y - 14f - risePixels * ease;
        n.label.style.width = 80f;
        n.label.style.opacity = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
        float pop = k < 0.12f ? 1f + 0.4f * (1f - k / 0.12f) : 1f;
        n.label.style.scale = new Scale(new Vector3(pop, pop, 1f));
    }
}
