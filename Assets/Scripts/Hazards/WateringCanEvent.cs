using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Regador gigante (perigo da horta, issue #29): de tempos em tempos aparece no céu perto do
/// player, inclina e despeja um jato d'água que corre por uma faixa do chão.
/// Aviso: a faixa fica azul no chão antes (por onde a água vai passar) e o regador desce
/// inclinando. Quem o jato pega leva dano e é empurrado (inimigos também); onde a água cai
/// ficam poças (<see cref="SlowZone"/>) que deixam o player lento por alguns segundos.
/// Fica mais frequente com a dificuldade.
/// </summary>
public class WateringCanEvent : MonoBehaviour
{
    [Header("Visual")]
    [Tooltip("Modelo do regador (WateringCan.fbx, com a peça Rose = crivo).")]
    [SerializeField] GameObject canModel;
    [SerializeField, Min(0.1f)] float canScale = 2f;
    [Tooltip("Material transparente da faixa de aviso e das poças (a cor vem do código).")]
    [SerializeField] Material waterMaterial;

    [Header("Frequência")]
    [SerializeField, Min(0f)] float firstDelay = 20f;
    [SerializeField] Vector2 interval = new(18f, 28f);
    [SerializeField, Min(0f)] float frequencyPerPoint = 0.3f;
    [SerializeField, Min(1f)] float minInterval = 9f;
    [Tooltip("Chance de mirar onde o player vai estar (senão, perto dele ao acaso).")]
    [SerializeField, Range(0f, 1f)] float aimAtPlayerChance = 0.65f;

    [Header("Jato")]
    [SerializeField, Min(0.1f)] float warningTime = 1.4f;
    [SerializeField, Min(0.1f)] float pourTime = 2f;
    [Tooltip("Comprimento da faixa por onde o jato corre.")]
    [SerializeField, Min(1f)] float stripLength = 10f;
    [SerializeField, Min(0.3f)] float streamRadius = 1.7f;
    [Tooltip("Altura do crivo acima do chão.")]
    [SerializeField, Min(4f)] float height = 15f;

    [Header("Dano")]
    [SerializeField, Range(0f, 1f)] float playerDamageFraction = 0.12f;
    [SerializeField, Range(0f, 1f)] float enemyDamageFraction = 0.35f;
    [SerializeField] Vector2 knockback = new(9f, 5f);

    [Header("Poças")]
    [SerializeField, Min(0.5f)] float puddleRadius = 2.4f;
    [SerializeField, Min(0.5f)] float puddleLifetime = 7f;
    [SerializeField, Range(0.05f, 1f)] float puddleSlow = 0.55f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly Color WarnColor = new(0.3f, 0.65f, 1f, 0.55f);
    static readonly Color WaterColor = new(0.45f, 0.75f, 1f, 0.5f);
    static readonly Color SplashColor = new(0.75f, 0.9f, 1f, 0.6f);

    public int Pours { get; private set; }
    public int PlayerHits { get; private set; }
    public int EnemyHits { get; private set; }
    public bool IsWarning { get; private set; }
    public bool IsPouring { get; private set; }
    /// <summary>Onde o jato está batendo agora (durante o despejo).</summary>
    public Vector3 Impact { get; private set; }
    public Vector3 StripStart { get; private set; }
    public Vector3 StripEnd { get; private set; }

    Transform player;
    PlayerMovement playerMovement;
    Health playerHealth;
    float nextPour;
    bool busy;
    MaterialPropertyBlock block;

    void Start()
    {
        block = new MaterialPropertyBlock();
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { player = p.transform; p.TryGetComponent(out playerMovement); p.TryGetComponent(out playerHealth); }
        nextPour = Time.time + firstDelay;
    }

    void Update()
    {
        if (busy || player == null || Time.time < nextPour) return;
        var run = RunManager.Instance;
        if (run != null && (run.IsRunOver || run.IsStageComplete)) return;
        float coeff = DifficultyManager.Instance != null ? DifficultyManager.Instance.Coefficient : 1f;
        float mean = Mathf.Max(minInterval, (interval.x + interval.y) * 0.5f / (1f + frequencyPerPoint * (coeff - 1f)));
        nextPour = Time.time + mean * Random.Range(0.8f, 1.2f);
        PourNow(PickTarget());
    }

    Vector3 PickTarget()
    {
        Vector3 t = player.position;
        if (Random.value < aimAtPlayerChance && playerMovement != null)
        {
            var v = playerMovement.Velocity; v.y = 0f;
            t += v * 0.6f;
        }
        else
        {
            Vector2 r = Random.insideUnitCircle * 8f;
            t += new Vector3(r.x, 0f, r.y);
        }
        return t;
    }

    /// <summary>Começa um despejo com a faixa passando por esse ponto (testes chamam direto).</summary>
    public void PourNow(Vector3 target)
    {
        if (busy) return;
        float yaw = Random.Range(0f, 360f);
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        StartCoroutine(Pour(target - dir * stripLength * 0.5f, target + dir * stripLength * 0.5f));
    }

    IEnumerator Pour(Vector3 a, Vector3 b)
    {
        busy = true;
        Pours++;
        a = Ground(a);
        b = Ground(b);
        StripStart = a;
        StripEnd = b;
        Vector3 dir = b - a; dir.y = 0f;
        float length = dir.magnitude;
        dir = length > 0.01f ? dir / length : Vector3.forward;

        // ---- aviso: faixa azul no chão e o regador descendo
        var strip = Disc("WateringCanWarning", (a + b) * 0.5f, new Vector3(streamRadius * 2f, 0.01f, length + streamRadius * 2f),
            Quaternion.LookRotation(dir));
        var can = SpawnCan(out Transform rose);
        IsWarning = true;
        for (float t = 0f; t < warningTime; t += Time.deltaTime)
        {
            float k = t / warningTime;
            Paint(strip, WarnColor * new Color(1f, 1f, 1f, 0.35f + 0.65f * k * (0.8f + 0.2f * Mathf.Sin(t * 25f))));
            PlaceCan(can, rose, a, dir, Mathf.Lerp(height + 18f, height, Mathf.SmoothStep(0f, 1f, k)), Mathf.Lerp(0f, 55f, k));
            yield return null;
        }
        IsWarning = false;

        // ---- despejo: o jato corre de a até b
        IsPouring = true;
        var stream = Stream(can.transform);
        var hitEnemies = new HashSet<EnemyBase>();
        bool hitPlayer = false;
        float nextSplash = 0f, nextPuddle = 0f;
        for (float t = 0f; t < pourTime; t += Time.deltaTime)
        {
            float k = t / pourTime;
            Impact = Ground(Vector3.Lerp(a, b, k));
            PlaceCan(can, rose, Impact, dir, height, 55f + Mathf.Sin(t * 9f) * 3f);
            if (rose != null && stream != null)
            {
                stream.SetPosition(0, rose.position);
                stream.SetPosition(1, Impact);
            }
            Paint(strip, WarnColor * new Color(1f, 1f, 1f, 0.5f * (1f - k)));
            if (t >= nextSplash) { nextSplash = t + 0.1f; ItemVfx.Explosion(Impact + Vector3.up * 0.3f, streamRadius * 0.8f, SplashColor, 0.25f); }
            if (t >= nextPuddle) { nextPuddle = t + pourTime / 4f; StartCoroutine(Puddle(Impact)); }
            if (!hitPlayer) hitPlayer = TryHitPlayer();
            HitEnemies(hitEnemies);
            yield return null;
        }
        IsPouring = false;
        if (stream != null) Destroy(stream.gameObject);
        Destroy(strip);

        // ---- vai embora subindo
        Vector3 from = can.transform.position;
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            can.transform.position = from + Vector3.up * (25f * t * t);
            yield return null;
        }
        Destroy(can);
        busy = false;
    }

    bool TryHitPlayer()
    {
        if (playerHealth == null || !playerHealth.IsAlive) return false;
        Vector3 d = player.position - Impact;
        if (d.y > 4f) return false;
        d.y = 0f;
        if (d.magnitude > streamRadius + 0.4f) return false;
        Vector3 push = d.sqrMagnitude > 0.01f ? d.normalized : -player.forward;
        playerHealth.TakeDamage(new DamageInfo(playerHealth.Max * playerDamageFraction, gameObject, player.position + Vector3.up, push));
        if (playerMovement != null) playerMovement.AddKnockback(push * knockback.x + Vector3.up * knockback.y);
        PlayerHits++;
        return true;
    }

    void HitEnemies(HashSet<EnemyBase> done)
    {
        for (int i = EnemyBase.Active.Count - 1; i >= 0; i--)
        {
            if (i >= EnemyBase.Active.Count) continue;
            var e = EnemyBase.Active[i];
            if (e == null || !e.IsAlive || done.Contains(e)) continue;
            Vector3 d = e.transform.position - Impact; d.y = 0f;
            if (d.magnitude > streamRadius + 0.6f) continue;
            done.Add(e);
            float frac = e.IsBoss ? enemyDamageFraction * 0.15f : enemyDamageFraction;
            e.Health.TakeDamage(new DamageInfo(e.Health.Max * frac, gameObject, e.Center, d) { IgnoreArmor = true });
            EnemyHits++;
        }
    }

    IEnumerator Puddle(Vector3 at)
    {
        var go = Disc("WateringPuddle", at + Vector3.up * 0.03f, new Vector3(puddleRadius * 2f, 0.01f, puddleRadius * 2f), Quaternion.identity, round: true);
        var zone = go.AddComponent<SlowZone>();
        zone.Configure(SlowZone.Kind.Water, puddleRadius, 1f, puddleSlow);
        for (float t = 0f; t < puddleLifetime; t += Time.deltaTime)
        {
            float fade = Mathf.Clamp01((puddleLifetime - t) / 1.2f);
            Paint(go, WaterColor * new Color(1f, 1f, 1f, fade));
            yield return null;
        }
        Destroy(go);
    }

    // ---------- Peças ----------

    GameObject SpawnCan(out Transform rose)
    {
        rose = null;
        GameObject can;
        if (canModel != null)
        {
            can = Instantiate(canModel);
            can.transform.localScale = Vector3.one * canScale;
            foreach (var c in can.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var t in can.GetComponentsInChildren<Transform>()) if (t.name == "Rose") rose = t;
        }
        else can = new GameObject("WateringCan");
        can.name = "WateringCan";
        if (rose == null) rose = can.transform;
        return can;
    }

    /// <summary>Põe o regador com o crivo acima do ponto, inclinado na direção do jato.</summary>
    void PlaceCan(GameObject can, Transform rose, Vector3 groundPoint, Vector3 dir, float h, float tilt)
    {
        var t = can.transform;
        t.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(tilt, 0f, 0f);
        Vector3 roseOffset = rose != null ? rose.position - t.position : Vector3.zero;
        t.position = groundPoint + Vector3.up * h - roseOffset;
    }

    LineRenderer Stream(Transform parent)
    {
        if (waterMaterial == null) return null;
        var go = new GameObject("WaterStream");
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = waterMaterial;
        lr.positionCount = 2;
        lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.9f), new Keyframe(1f, streamRadius * 1.1f));
        lr.startColor = lr.endColor = WaterColor;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return lr;
    }

    GameObject Disc(string name, Vector3 at, Vector3 scale, Quaternion rot, bool round = false)
    {
        var go = GameObject.CreatePrimitive(round ? PrimitiveType.Cylinder : PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetPositionAndRotation(at + Vector3.up * 0.04f, rot);
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        if (waterMaterial != null) r.sharedMaterial = waterMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    void Paint(GameObject go, Color c)
    {
        if (go == null || !go.TryGetComponent(out Renderer r)) return;
        r.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        r.SetPropertyBlock(block);
    }

    static Vector3 Ground(Vector3 p) => StompDirector.FindGround(p, out var g) ? g : new Vector3(p.x, 0f, p.z);

#if UNITY_EDITOR
    public void EditorSetup(GameObject model, Material water) { canModel = model; waterMaterial = water; }
#endif
}
