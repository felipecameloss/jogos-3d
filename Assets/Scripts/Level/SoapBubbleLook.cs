using UnityEngine;

/// <summary>
/// Visual de bolha de sabão (a bolha do player e as que voam pelo quintal): esfera translúcida
/// com a cor mudando devagar (furta-cor) e um brilho branco do lado da câmera. Feito com
/// primitivas e um material transparente; a cor vai por MaterialPropertyBlock.
/// </summary>
public class SoapBubbleLook
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    readonly Transform shell, shine;
    readonly Renderer shellRenderer, shineRenderer;
    readonly MaterialPropertyBlock block = new();

    public Transform Root { get; }

    public SoapBubbleLook(string name, Material material, Transform parent = null)
    {
        Root = new GameObject(name).transform;
        if (parent != null) Root.SetParent(parent, false);
        shell = Sphere("Shell", material, out shellRenderer);
        shine = Sphere("Shine", material, out shineRenderer);
    }

    Transform Sphere(string name, Material material, out Renderer renderer)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // sem colisor nem por um frame
        go.transform.SetParent(Root, false);
        renderer = go.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (material != null) renderer.sharedMaterial = material;
        else renderer.enabled = false;
        return go.transform;
    }

    /// <summary>
    /// Desenha a bolha com `diameter` metros. `hue` (0..1) é a cor da vez, `alpha` a opacidade
    /// e `wobble` deforma de leve (bolha balançando).
    /// </summary>
    public void Draw(float diameter, float hue, float alpha, float wobble = 0f, float saturation = 0.4f)
    {
        float squash = 1f + wobble;
        shell.localScale = new Vector3(diameter * squash, diameter / squash, diameter * squash);
        Paint(shellRenderer, Color.HSVToRGB(Mathf.Repeat(hue, 1f), saturation, 1f), alpha);

        // Brilho no lado virado para a câmera, em cima à esquerda
        var cam = Camera.main;
        Vector3 toCam = cam != null ? (cam.transform.position - Root.position).normalized : Vector3.back;
        Vector3 left = cam != null ? -cam.transform.right : Vector3.left;
        Vector3 up = cam != null ? cam.transform.up : Vector3.up;
        float r = diameter * 0.5f;
        shine.position = Root.position + (toCam * 0.75f + left * 0.35f + up * 0.45f).normalized * (r * 0.88f);
        shine.localScale = new Vector3(0.16f, 0.1f, 0.16f) * diameter;
        shine.rotation = cam != null ? cam.transform.rotation * Quaternion.Euler(0f, 0f, -35f) : Quaternion.identity;
        Paint(shineRenderer, Color.white, Mathf.Clamp01(alpha * 2.6f));
    }

    public void SetVisible(bool visible) => Root.gameObject.SetActive(visible);

    public void Destroy()
    {
        if (Root != null) Object.Destroy(Root.gameObject);
    }

    void Paint(Renderer r, Color c, float alpha)
    {
        if (!r.enabled) return;
        c.a = alpha;
        r.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        r.SetPropertyBlock(block);
    }
}
