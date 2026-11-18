using UnityEngine;

/// <summary>Pisca o alvo de branco por um instante ao levar dano. Adicionado sob demanda.</summary>
[DisallowMultipleComponent]
public class HitFlash : MonoBehaviour
{
    const float Duration = 0.08f;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int GltfBaseColorId = Shader.PropertyToID("baseColorFactor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    Renderer[] renderers;
    Color[] originals;
    int[] colorPropIds;
    MaterialPropertyBlock block;
    float until;
    bool flashing;

    public bool IsFlashing => flashing;

    public static HitFlash On(Component target)
    {
        if (target == null) return null;
        return target.TryGetComponent(out HitFlash f) ? f : target.gameObject.AddComponent<HitFlash>();
    }

    void Awake()
    {
        block = new MaterialPropertyBlock();
        renderers = GetComponentsInChildren<Renderer>();
        originals = new Color[renderers.Length];
        colorPropIds = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            int prop = ResolveColorProp(m);
            colorPropIds[i] = prop;
            originals[i] = m != null && prop != 0 ? m.GetColor(prop) : Color.white;
        }
    }

    static int ResolveColorProp(Material m)
    {
        if (m == null) return 0;
        if (m.HasProperty(BaseColorId)) return BaseColorId;
        if (m.HasProperty(GltfBaseColorId)) return GltfBaseColorId;
        if (m.HasProperty(ColorId)) return ColorId;
        return 0;
    }

    public void Flash()
    {
        until = Time.unscaledTime + Duration;
        if (flashing) return;
        flashing = true;
        Set(true);
    }

    void LateUpdate()
    {
        if (flashing && Time.unscaledTime >= until) { flashing = false; Set(false); }
    }

    void OnDisable()
    {
        if (flashing) { flashing = false; Set(false); }
    }

    void Set(bool white)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            int prop = colorPropIds[i];
            renderers[i].GetPropertyBlock(block);
            if (prop != 0)
                block.SetColor(prop, white ? Color.Lerp(originals[i], Color.white, 0.85f) : originals[i]);
            var mat = renderers[i].sharedMaterial;
            if (mat != null && mat.HasProperty(EmissionColorId))
                block.SetColor(EmissionColorId, white ? Color.white * 0.65f : Color.black);
            renderers[i].SetPropertyBlock(block);
        }
    }
}
