using UnityEngine;

/// <summary>Pisca o alvo de branco por um instante ao levar dano. Adicionado sob demanda.</summary>
[DisallowMultipleComponent]
public class HitFlash : MonoBehaviour
{
    const float Duration = 0.08f;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    Renderer[] renderers;
    Color[] originals;
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
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            originals[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
        }
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
            renderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, white ? Color.Lerp(originals[i], Color.white, 0.85f) : originals[i]);
            renderers[i].SetPropertyBlock(block);
        }
    }
}
