using System.Collections;
using UnityEngine;

/// <summary>
/// Poça de ácido do besouro: cresce, causa dano por segundo (DamageZone) e some com fade.
/// </summary>
[RequireComponent(typeof(DamageZone))]
public class AcidPool : MonoBehaviour
{
    [SerializeField, Min(0.5f)] float radius = 2.4f;
    [SerializeField, Min(0.5f)] float lifetime = 5f;
    [SerializeField] Renderer visual;
    [SerializeField] Color color = new(0.45f, 0.95f, 0.2f, 0.55f);

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public float Lifetime => lifetime;

    /// <summary>Começa a poça com esse dano por segundo; quem criou vira a fonte do dano.</summary>
    public void Begin(float damagePerSecond, GameObject owner)
    {
        var zone = GetComponent<DamageZone>();
        zone.DamagePerSecond = damagePerSecond;
        zone.Owner = owner;
        StartCoroutine(Life());
    }

    IEnumerator Life()
    {
        var block = new MaterialPropertyBlock();
        for (float t = 0f; t < lifetime; t += Time.deltaTime)
        {
            float grow = Mathf.Clamp01(t / 0.2f);
            float fade = 1f - Mathf.Clamp01((t - (lifetime - 1f)) / 1f);
            transform.localScale = new Vector3(radius * 2f * grow, 1f, radius * 2f * grow);
            if (visual != null)
            {
                visual.GetPropertyBlock(block);
                var c = color; c.a *= fade * (0.85f + 0.15f * Mathf.Sin(t * 6f));
                block.SetColor(BaseColorId, c);
                visual.SetPropertyBlock(block);
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}
