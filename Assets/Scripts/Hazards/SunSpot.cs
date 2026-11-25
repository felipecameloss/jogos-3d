using System;
using UnityEngine;

/// <summary>
/// Ponto de sol da lupa gigante (evento "criança com lupa", issue #34). Uma lupa de brinquedo
/// flutua no céu entre o sol e o chão e concentra um raio de sol que persegue o player:
/// 1. Mira: um círculo de luz largo e fraco aparece no player e começa a seguir (aviso).
/// 2. Foco: o círculo encolhe e fica forte; por alguns segundos persegue o player um pouco
///    mais devagar que a corrida dele (dá para fugir correndo). Dentro do círculo o player
///    queima (fração da vida máxima por segundo; armadura reduz) e inimigos pegam fogo.
/// 3. Some: o círculo apaga e a lupa sobe.
/// A lupa treme de leve, como na mão de uma criança.
/// </summary>
public class SunSpot : MonoBehaviour
{
    public enum Phase { Aim, Burn, Fade, Done }

    [Header("Partes")]
    [Tooltip("Lupa gigante (vidro no plano XZ local, normal em +Y).")]
    [SerializeField] Transform lens;
    [Tooltip("Coluna de luz da lupa até o chão (cilindro).")]
    [SerializeField] Transform beam;
    [SerializeField] Renderer beamRenderer;
    [Tooltip("Círculo de luz no chão (disco).")]
    [SerializeField] Transform spot;
    [SerializeField] Renderer spotRenderer;

    [Header("Tempo")]
    [SerializeField, Min(0.1f)] float aimTime = 1.3f;
    [SerializeField, Min(0.1f)] float burnTime = 6f;
    [SerializeField, Min(0.05f)] float fadeTime = 0.6f;

    [Header("Movimento")]
    [Tooltip("Raio do círculo na mira (largo) e no foco (queimando).")]
    [SerializeField] Vector2 radius = new(4.5f, 1.8f);
    [Tooltip("Velocidade com que o círculo segue o player na mira e queimando (m/s). A corrida do player é 13,5.")]
    [SerializeField] Vector2 followSpeed = new(16f, 10.5f);
    [SerializeField, Min(5f)] float lensHeight = 24f;
    [Tooltip("Tremida da mão da criança (metros).")]
    [SerializeField, Min(0f)] float handShake = 0.6f;

    [Header("Dano")]
    [SerializeField, Min(0.05f)] float tickInterval = 0.25f;
    [Tooltip("Fração da vida máxima do player por segundo dentro do círculo (armadura reduz).")]
    [SerializeField, Range(0f, 1f)] float playerDamagePerSecond = 0.1f;
    [Tooltip("Queimadura em inimigos dentro do círculo: fração da vida máxima, em 2 s.")]
    [SerializeField, Range(0f, 1f)] float enemyBurnFraction = 0.25f;
    [Tooltip("Chefes queimam bem menos.")]
    [SerializeField, Range(0f, 1f)] float bossBurnFraction = 0.03f;

    [Header("Cores")]
    [SerializeField] Color spotColor = new(1f, 0.93f, 0.5f, 0.85f);
    [SerializeField] Color beamColor = new(1f, 0.92f, 0.55f, 0.3f);

    public event Action<SunSpot> OnFinished;

    public Phase Current { get; private set; } = Phase.Done;
    public bool IsBurning => Current == Phase.Burn;
    public float Elapsed { get; private set; }
    public float AimTime => aimTime;
    public float BurnTime => burnTime;
    public float TotalTime => aimTime + burnTime + fadeTime;
    /// <summary>Raio atual do círculo no chão.</summary>
    public float CurrentRadius { get; private set; }
    /// <summary>Centro do círculo no chão.</summary>
    public Vector3 Center { get; private set; }
    public int PlayerHits { get; private set; }
    public int EnemiesIgnited { get; private set; }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    readonly Collider[] overlap = new Collider[64];
    MaterialPropertyBlock block;
    Transform target;
    float nextTick, seed;

    void Awake() => block = new MaterialPropertyBlock();

    /// <summary>Começa a perseguir `target` (o player).</summary>
    public void Begin(Transform followTarget)
    {
        target = followTarget;
        Elapsed = 0f;
        nextTick = aimTime;
        PlayerHits = 0;
        EnemiesIgnited = 0;
        seed = UnityEngine.Random.value * 100f;
        Center = Ground(target != null ? target.position : transform.position);
        Current = Phase.Aim;
        UpdateVisuals(0f);
    }

    void Update()
    {
        if (Current == Phase.Done) return;
        float dt = Time.deltaTime;
        Elapsed += dt;
        Current = Elapsed < aimTime ? Phase.Aim : Elapsed < aimTime + burnTime ? Phase.Burn : Elapsed < TotalTime ? Phase.Fade : Phase.Done;
        if (Current == Phase.Done) { Finish(); return; }

        // Segue o player pelo chão (no foco, mais devagar que a corrida)
        if (target != null && Current != Phase.Fade)
        {
            Vector3 want = Ground(target.position);
            float speed = Current == Phase.Aim ? followSpeed.x : followSpeed.y;
            Center = Vector3.MoveTowards(Center, want, speed * dt);
        }

        float focus = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Elapsed / aimTime)); // 0 = mira, 1 = foco
        float fade = Current == Phase.Fade ? Mathf.Clamp01((Elapsed - aimTime - burnTime) / fadeTime) : 0f;
        CurrentRadius = Mathf.Lerp(radius.x, radius.y, focus) * (1f - 0.5f * fade);

        if (Current == Phase.Burn && Elapsed >= nextTick)
        {
            nextTick += tickInterval;
            Burn();
        }
        UpdateVisuals(fade);
    }

    void Burn()
    {
        int n = Physics.OverlapSphereNonAlloc(Center + Vector3.up * 1f, CurrentRadius + 1f, overlap, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = overlap[i].GetComponentInParent<Health>();
            if (h == null || !h.IsAlive) continue;
            Vector3 flat = h.transform.position - Center; flat.y = 0f;
            if (flat.magnitude > CurrentRadius) continue;
            if (h.TryGetComponent(out PlayerMovement _))
            {
                // um tick por player (os colisores dele podem aparecer mais de uma vez)
                if (overlapPlayerTick == Time.frameCount) continue;
                overlapPlayerTick = Time.frameCount;
                h.TakeDamage(new DamageInfo(h.Max * playerDamagePerSecond * tickInterval, gameObject, h.transform.position + Vector3.up, Vector3.up));
                PlayerHits++;
            }
            else if (h.TryGetComponent(out EnemyBase enemy))
            {
                var status = StatusEffects.On(enemy);
                if (status.IsBurning) continue;
                float frac = enemy.IsBoss ? bossBurnFraction : enemyBurnFraction;
                status.ApplyBurn(h.Max * frac, 2f, gameObject);
                EnemiesIgnited++;
            }
        }
    }

    int overlapPlayerTick = -1;

    void UpdateVisuals(float fade)
    {
        float focus = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Elapsed / aimTime));
        float t = Time.time + seed;
        // tremida da mão: o círculo e a lupa balançam juntos
        Vector3 shake = new Vector3(Mathf.PerlinNoise(t * 1.7f, 0.3f) - 0.5f, 0f, Mathf.PerlinNoise(0.7f, t * 1.7f) - 0.5f) * (handShake * 2f);
        Vector3 c = Center + shake;
        transform.position = c;

        if (spot != null)
        {
            spot.position = c + Vector3.up * 0.06f;
            spot.localScale = new Vector3(CurrentRadius * 2f, spot.localScale.y, CurrentRadius * 2f);
        }
        if (spotRenderer != null)
        {
            spotRenderer.GetPropertyBlock(block);
            var col = spotColor;
            // aviso fraco piscando na mira; forte e pulsando no foco
            float pulse = Current == Phase.Burn ? 0.85f + 0.15f * Mathf.Sin(Time.time * 18f) : 0.35f + 0.25f * Mathf.Sin(Time.time * 10f);
            col.a *= Mathf.Lerp(0.3f, 1f, focus) * pulse * (1f - fade);
            block.SetColor(BaseColorId, col);
            spotRenderer.SetPropertyBlock(block);
        }

        // Lupa: no céu, na linha do sol até o círculo, com o vidro virado para o chão
        Vector3 sunDir = RenderSettings.sun != null ? RenderSettings.sun.transform.forward : Vector3.down;
        if (sunDir.y > -0.35f) { sunDir.y = -0.35f; } // sol baixo: não deixa a lupa deitar demais
        sunDir.Normalize();
        float rise = fade * 12f; // sobe ao sumir
        Vector3 lensPos = c - sunDir * (lensHeight + rise);
        if (lens != null)
        {
            lens.position = lensPos;
            lens.rotation = Quaternion.FromToRotation(Vector3.up, -sunDir) * Quaternion.Euler(0f, t * 4f, Mathf.Sin(t * 1.3f) * 4f);
        }
        if (beam != null)
        {
            Vector3 top = lensPos, bottom = c;
            beam.position = (top + bottom) * 0.5f;
            beam.rotation = Quaternion.FromToRotation(Vector3.up, top - bottom);
            float w = CurrentRadius * Mathf.Lerp(1.6f, 0.9f, focus);
            beam.localScale = new Vector3(w, Vector3.Distance(top, bottom) * 0.5f, w); // cilindro: altura 2
        }
        if (beamRenderer != null)
        {
            beamRenderer.GetPropertyBlock(block);
            var col = beamColor;
            col.a *= Mathf.Lerp(0.3f, 1f, focus) * (1f - fade);
            block.SetColor(BaseColorId, col);
            beamRenderer.SetPropertyBlock(block);
        }
    }

    static Vector3 Ground(Vector3 p) => StompDirector.FindGround(p, out var g) ? g : p;

    void Finish()
    {
        Current = Phase.Done;
        OnFinished?.Invoke(this);
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    public void EditorSetup(Transform lensT, Transform beamT, Renderer beamR, Transform spotT, Renderer spotR)
    {
        lens = lensT; beam = beamT; beamRenderer = beamR; spot = spotT; spotRenderer = spotR;
    }
#endif
}
