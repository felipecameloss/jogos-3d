using UnityEngine;

/// <summary>
/// A bolha de sabão em volta do player enquanto ele flutua (PlayerMovement.IsFloating).
/// Cresce ao formar, muda de cor, pisca quando está acabando e estoura quando ele solta o
/// pulo, pisa no chão, o tempo acaba ou leva dano (levar dano estoura de verdade: só volta
/// depois de pisar no chão). Criada pelo item Bolha de sabão na primeira vez que o player pega.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerBubble : MonoBehaviour
{
    const float Diameter = 2.7f;
    const float CenterHeight = 1.05f;
    const float PopTime = 0.16f;

    PlayerMovement movement;
    Health health;
    SoapBubbleLook look;
    Material material;
    float grow, popTimer;
    bool wasFloating;

    /// <summary>Bolhas estouradas (soltou, pisou, acabou o tempo ou levou dano).</summary>
    public int Pops { get; private set; }
    /// <summary>Estouradas por dano.</summary>
    public int PoppedByDamage { get; private set; }

    public static PlayerBubble Ensure(PlayerMovement movement, Material material)
    {
        var bubble = movement.TryGetComponent(out PlayerBubble b) ? b : movement.gameObject.AddComponent<PlayerBubble>();
        if (bubble.material == null && material != null) bubble.SetMaterial(material);
        return bubble;
    }

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<Health>();
        if (health != null) health.OnDamaged += HandleDamaged;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDamaged -= HandleDamaged;
        look?.Destroy();
    }

    void SetMaterial(Material m)
    {
        material = m;
        look?.Destroy();
        // Objeto solto na cena (não filho do player): nada que junta os renderers do player pega a bolha
        look = new SoapBubbleLook("Player Soap Bubble", material);
        look.SetVisible(false);
    }

    void HandleDamaged(DamageInfo info)
    {
        if (!movement.IsFloating) return;
        movement.PopFloat();
        PoppedByDamage++;
    }

    void LateUpdate()
    {
        bool floating = movement.IsFloating;
        if (wasFloating && !floating) { popTimer = PopTime; Pops++; }
        wasFloating = floating;
        if (look == null) return;

        float dt = Time.deltaTime;
        if (floating) grow = Mathf.MoveTowards(grow, 1f, dt * 7f);
        else if (popTimer > 0f) popTimer -= dt;
        else grow = 0f;

        bool visible = floating || popTimer > 0f;
        look.SetVisible(visible);
        if (!visible) return;

        look.Root.position = transform.position + Vector3.up * CenterHeight;
        float t = Time.time;
        float wobble = Mathf.Sin(t * 9f) * 0.04f;
        if (floating)
        {
            // Pisca quando está acabando
            float left = movement.FloatRemaining01;
            float blink = left < 0.25f ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 22f)) : 1f;
            float size = Diameter * Mathf.Lerp(0.55f, 1f, 1f - (1f - grow) * (1f - grow));
            look.Draw(size, t * 0.12f, 0.24f * blink, wobble);
        }
        else
        {
            // Estouro: abre um pouco e some rápido
            float k = 1f - popTimer / PopTime;
            look.Draw(Diameter * (1f + 0.25f * k), t * 0.12f, 0.24f * (1f - k), 0f);
        }
    }
}
