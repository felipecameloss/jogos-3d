using System.Collections;
using UnityEngine;

/// <summary>
/// Chefe louva-a-deus. Três ataques, todos com aviso:
/// - Garras (perto): levanta as garras (ficam vermelhas) e corta num cone à frente.
/// - Investida (média distância): faixa vermelha no chão mostra o caminho; avança em linha reta.
/// - Pulo esmagador: círculo vermelho cresce onde o player estava; cai em cima com dano em área.
/// Abaixo de metade da vida entra em fúria: avisos e recargas mais curtos, anda mais rápido.
/// Ao morrer solta um item lendário.
/// </summary>
public class MantisBoss : EnemyBase
{
    [Header("Chefe")]
    [SerializeField] string bossName = "Louva-a-deus";

    [Header("Garras")]
    [SerializeField] float slashDamage = 25f;
    [SerializeField] float slashRange = 5.5f;
    [SerializeField, Range(10f, 180f)] float slashAngle = 110f;
    [SerializeField] float slashWindUp = 0.42f;
    [SerializeField] float slashCooldown = 0.85f;

    [Header("Investida")]
    [SerializeField] float dashDamage = 30f;
    [SerializeField] Vector2 dashRange = new(8f, 26f);
    [SerializeField] float dashWindUp = 0.52f;
    [SerializeField] float dashDistance = 22f;
    [SerializeField] float dashSpeed = 44f;
    [SerializeField] float dashCooldown = 3.5f;
    [SerializeField] float dashHitRadius = 2.6f;

    [Header("Pulo esmagador")]
    [SerializeField] float leapDamage = 35f;
    [SerializeField] Vector2 leapRange = new(6f, 30f);
    [SerializeField] float leapWindUp = 0.38f;
    [SerializeField] float leapAirTime = 0.5f;
    [SerializeField] float leapRadius = 5f;
    [SerializeField] float leapCooldown = 5.2f;

    [Header("Geral")]
    [SerializeField] float recoverTime = 0.4f;
    [SerializeField, Range(0f, 1f)] float enrageAt = 0.5f;
    [Tooltip("Na fúria: tempos x isso, velocidade / isso.")]
    [SerializeField, Range(0.3f, 1f)] float enrageTimeScale = 0.62f;
    [SerializeField] Vector2 knockback = new(10f, 5f);

    [Header("Visual")]
    [SerializeField] Transform body;
    [SerializeField] Transform clawLeft, clawRight;
    [SerializeField] Renderer[] clawRenderers;
    [Tooltip("Disco no chão (aviso do pulo). Solto do chefe ao nascer.")]
    [SerializeField] Renderer leapTelegraph;
    [Tooltip("Faixa no chão (aviso da investida). Solta do chefe ao nascer.")]
    [SerializeField] Renderer dashTelegraph;

    [Header("Recompensa")]
    [SerializeField] ItemDatabase database;
    [SerializeField] ItemPickup pickupPrefab;

    public override bool IsBoss => true;
    public string BossName => bossName;
    public bool IsEnraged => Health.Current <= Health.Max * enrageAt;
    public bool IsAttacking => attacking != null;
    public string CurrentAttack { get; private set; } = "";
    // Para testes/depuração
    public int SlashesLanded { get; private set; }
    public int DashesPerformed { get; private set; }
    public int DashesLanded { get; private set; }
    public int LeapsPerformed { get; private set; }
    public int LeapsLanded { get; private set; }
    public ItemPickup DroppedItem { get; private set; }

    /// <summary>O chefe vivo agora (barra de vida da HUD).</summary>
    public static MantisBoss Current { get; private set; }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly Color TelegraphRed = new(1f, 0.15f, 0.05f, 0.45f);

    Coroutine attacking;
    float nextSlash, nextDash, nextLeap;
    Quaternion clawRestL, clawRestR, bodyRest;
    MaterialPropertyBlock block;

    float T(float t) => IsEnraged ? t * enrageTimeScale : t;

    protected override void Awake()
    {
        base.Awake();
        block = new MaterialPropertyBlock();
        if (clawLeft != null) clawRestL = clawLeft.localRotation;
        if (clawRight != null) clawRestR = clawRight.localRotation;
        if (body != null) bodyRest = body.localRotation;
        // Avisos no chão ficam soltos no mundo (o chefe se mexe, o aviso não)
        foreach (var r in new[] { leapTelegraph, dashTelegraph })
            if (r != null) { r.transform.SetParent(null, true); r.gameObject.SetActive(false); }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Current = this;
        nextDash = Time.time + 1.5f;
        nextLeap = Time.time + 3f;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (Current == this) Current = null;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (Current == this) Current = null;
        if (leapTelegraph != null) Destroy(leapTelegraph.gameObject);
        if (dashTelegraph != null) Destroy(dashTelegraph.gameObject);
    }

    protected override void Update()
    {
        base.Update();
        if (IsAlive && !HasValidTarget && attacking != null) CancelAttack();
    }

    protected override void Tick()
    {
        Agent.speed = moveSpeed * SpeedMultiplier * StatusSpeedMultiplier / (IsEnraged ? enrageTimeScale : 1f);
        if (attacking != null) return;

        float d = FlatDistanceToTarget();
        if (d <= slashRange && Time.time >= nextSlash) { Begin(SlashRoutine()); return; }
        if (d >= dashRange.x && d <= dashRange.y && Time.time >= nextDash) { Begin(DashRoutine()); return; }
        if (d >= leapRange.x && d <= leapRange.y && Time.time >= nextLeap) { Begin(LeapRoutine()); return; }
        if (d <= slashRange * 0.7f) { StopMoving(); FaceTarget(); }
        else MoveTo(Target.position);
    }

    void Begin(IEnumerator routine)
    {
        StopMoving();
        attacking = StartCoroutine(Run(routine));
    }

    IEnumerator Run(IEnumerator routine)
    {
        yield return routine;
        CurrentAttack = "";
        yield return new WaitForSeconds(T(recoverTime));
        attacking = null;
    }

    void CancelAttack()
    {
        if (attacking != null) StopCoroutine(attacking);
        attacking = null;
        CurrentAttack = "";
        ResetPose();
        HideTelegraphs();
        if (!Agent.enabled) { Agent.enabled = true; }
    }

    // ---------- Garras ----------

    IEnumerator SlashRoutine()
    {
        CurrentAttack = "garras";
        float wind = T(slashWindUp);
        for (float t = 0f; t < wind; t += Time.deltaTime)
        {
            FaceTarget();
            float k = t / wind;
            PoseClaws(-70f * k);                   // levanta
            ClawGlow(k);
            yield return null;
        }
        // Corte rápido
        for (float t = 0f; t < 0.12f; t += Time.deltaTime) { PoseClaws(Mathf.Lerp(-70f, 60f, t / 0.12f)); yield return null; }
        ClawGlow(0f);
        if (TargetInCone(slashRange + 0.5f, slashAngle)) { Hit(slashDamage); SlashesLanded++; }
        yield return new WaitForSeconds(0.25f);
        ResetPose();
        nextSlash = Time.time + T(slashCooldown);
    }

    bool TargetInCone(float range, float angle)
    {
        Vector3 d = Target.position - transform.position; d.y = 0f;
        return d.magnitude <= range && Vector3.Angle(transform.forward, d) <= angle * 0.5f;
    }

    // ---------- Investida ----------

    IEnumerator DashRoutine()
    {
        CurrentAttack = "investida";
        DashesPerformed++;
        float wind = T(dashWindUp);
        Vector3 dir = transform.forward;
        dashTelegraph.gameObject.SetActive(true);
        for (float t = 0f; t < wind; t += Time.deltaTime)
        {
            float k = t / wind;
            if (k < 0.7f) { FaceTarget(); dir = transform.forward; } // trava a direção no fim do aviso
            if (body != null) body.localRotation = bodyRest * Quaternion.Euler(15f * k, 0f, 0f); // abaixa
            PlaceDashTelegraph(dir, k);
            yield return null;
        }
        dashTelegraph.gameObject.SetActive(false);

        bool hit = false;
        float traveled = 0f;
        while (traveled < dashDistance)
        {
            float step = dashSpeed * Time.deltaTime;
            Vector3 before = transform.position;
            Agent.Move(dir * step); // respeita as bordas do NavMesh
            float moved = (transform.position - before).magnitude;
            traveled += step;
            if (!hit && FlatDistanceToTarget() <= dashHitRadius) { Hit(dashDamage); hit = true; DashesLanded++; }
            if (moved < step * 0.2f) break; // bateu em algo
            yield return null;
        }
        ResetPose();
        nextDash = Time.time + T(dashCooldown);
    }

    void PlaceDashTelegraph(Vector3 dir, float k)
    {
        var tr = dashTelegraph.transform;
        tr.position = transform.position + dir * (dashDistance * 0.5f) + Vector3.up * 0.05f;
        tr.rotation = Quaternion.LookRotation(dir);
        tr.localScale = new Vector3(dashHitRadius * 1.6f, 0.01f, dashDistance);
        SetColor(dashTelegraph, TelegraphRed * new Color(1f, 1f, 1f, 0.4f + 0.6f * k));
    }

    // ---------- Pulo ----------

    IEnumerator LeapRoutine()
    {
        CurrentAttack = "pulo";
        LeapsPerformed++;
        Vector3 targetPoint = Target.position;
        Vector3 start = transform.position;
        float wind = T(leapWindUp), air = leapAirTime;
        leapTelegraph.gameObject.SetActive(true);
        leapTelegraph.transform.position = targetPoint + Vector3.up * 0.05f;

        for (float t = 0f; t < wind; t += Time.deltaTime)
        {
            FaceTarget();
            float k = t / wind;
            if (body != null) body.localRotation = bodyRest * Quaternion.Euler(-12f * k, 0f, 0f);
            GrowLeapTelegraph(0.5f * k);
            yield return null;
        }

        Agent.enabled = false; // voa por cima de tudo
        for (float t = 0f; t < air; t += Time.deltaTime)
        {
            float k = t / air;
            transform.position = Vector3.Lerp(start, targetPoint, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 7f);
            GrowLeapTelegraph(0.5f + 0.5f * k);
            yield return null;
        }
        transform.position = targetPoint;
        Agent.enabled = true;
        Agent.Warp(targetPoint);
        leapTelegraph.gameObject.SetActive(false);

        Vector3 d = Target.position - targetPoint; d.y = 0f;
        GameFeel.Shake(0.9f);
        if (d.magnitude <= leapRadius) { Hit(leapDamage); LeapsLanded++; }
        ResetPose();
        nextLeap = Time.time + T(leapCooldown);
    }

    void GrowLeapTelegraph(float k)
    {
        float size = leapRadius * 2f * Mathf.Lerp(0.3f, 1f, k);
        leapTelegraph.transform.localScale = new Vector3(size, 0.01f, size);
        SetColor(leapTelegraph, TelegraphRed * new Color(1f, 1f, 1f, 0.3f + 0.7f * k));
    }

    // ---------- Ajudantes ----------

    void Hit(float baseDamage)
    {
        if (TargetHealth == null) return;
        Vector3 dir = (Target.position - transform.position); dir.y = 0f; dir.Normalize();
        TargetHealth.TakeDamage(new DamageInfo(baseDamage * DamageMultiplier, gameObject, Target.position + Vector3.up, dir));
        if (Target.TryGetComponent(out PlayerMovement pm)) pm.AddKnockback(dir * knockback.x + Vector3.up * knockback.y);
    }

    void PoseClaws(float angle)
    {
        if (clawLeft != null) clawLeft.localRotation = clawRestL * Quaternion.Euler(angle, 0f, 0f);
        if (clawRight != null) clawRight.localRotation = clawRestR * Quaternion.Euler(angle, 0f, 0f);
    }

    void ClawGlow(float k)
    {
        if (clawRenderers == null) return;
        foreach (var r in clawRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(EmissionId, new Color(1f, 0.15f, 0.05f) * (2f * k));
            r.SetPropertyBlock(block);
        }
    }

    void ResetPose()
    {
        PoseClaws(0f);
        ClawGlow(0f);
        if (body != null) body.localRotation = bodyRest;
    }

    void HideTelegraphs()
    {
        if (leapTelegraph != null) leapTelegraph.gameObject.SetActive(false);
        if (dashTelegraph != null) dashTelegraph.gameObject.SetActive(false);
    }

    void SetColor(Renderer r, Color c)
    {
        r.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        r.SetPropertyBlock(block);
    }

    protected override void HandleDeath(DamageInfo killingBlow)
    {
        CancelAttack();
        base.HandleDeath(killingBlow);
        DropLegendary();
    }

    void DropLegendary()
    {
        if (database == null || pickupPrefab == null) return;
        var item = database.Random(ItemRarity.Lendario) ?? database.RandomWeighted(0f, 1f, 0f);
        if (item == null) return;
        Vector3 land = transform.position + transform.forward * 2f;
        if (UnityEngine.AI.NavMesh.SamplePosition(land, out var hit, 4f, UnityEngine.AI.NavMesh.AllAreas)) land = hit.position;
        DroppedItem = ItemPickup.Spawn(pickupPrefab, item, transform.position + Vector3.up * 2f);
        DroppedItem.Launch(land + Vector3.up, 0.8f, 3f);
    }
}
