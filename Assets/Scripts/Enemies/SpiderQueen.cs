using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Chefe aranha-rainha (issue #32). Quatro ataques, todos com aviso:
/// - Rajada de teia: vira de costas, a fiandeira brilha e cospe um leque de bolas de teia
///   (WebShot da aranha comum). Acerto direto ou pisar na teia prende o player.
/// - Ninhada: ergue o abdômen pulsando e invoca aranhinhas (a aranha comum, menor e mais
///   fraca), até um limite vivas ao mesmo tempo.
/// - Pulo esmagador: círculo vermelho marca onde cai; dano em área + empurrão.
/// - Mordida: de perto, golpe num cone à frente.
/// Abaixo de metade da vida entra em fúria: chama uma ninhada na hora, ataques mais rápidos
/// e leques maiores. Ao morrer a ninhada morre junto e ela solta um item lendário.
/// Barra de vida na HUD via BossTracker.
/// </summary>
public class SpiderQueen : EnemyBase, IBoss
{
    [Header("Chefe")]
    [SerializeField] string bossName = "Aranha-rainha";
    [SerializeField, Range(0f, 1f)] float enrageAt = 0.5f;
    [Tooltip("Na fúria: tempos de aviso e recarga x isso.")]
    [SerializeField, Range(0.3f, 1f)] float enrageTimeScale = 0.7f;

    [Header("Rajada de teia")]
    [SerializeField] WebShot webPrefab;
    [SerializeField] WebPatch webPatchPrefab;
    [SerializeField] Vector2 webRange = new(6f, 30f);
    [SerializeField, Min(1)] int webCount = 3;
    [SerializeField, Min(1)] int enragedWebCount = 5;
    [Tooltip("Abertura do leque, em graus entre as bolas.")]
    [SerializeField, Range(0f, 45f)] float webSpread = 16f;
    [SerializeField, Min(0.1f)] float webWindUp = 0.6f;
    [SerializeField, Min(0f)] float webCooldown = 4.5f;
    [SerializeField, Min(0.1f)] float rootDuration = 1.6f;

    [Header("Ninhada")]
    [Tooltip("Aranha comum: nasce menor e mais fraca.")]
    [SerializeField] EnemyBase spiderlingPrefab;
    [SerializeField, Min(1)] int broodCount = 3;
    [SerializeField, Min(1)] int enragedBroodCount = 4;
    [SerializeField, Min(1)] int maxBrood = 6;
    [SerializeField, Range(0.2f, 1f)] float spiderlingScale = 0.55f;
    [SerializeField, Range(0.05f, 1f)] float spiderlingHealth = 0.35f;
    [SerializeField, Range(0.05f, 1f)] float spiderlingDamage = 0.5f;
    [SerializeField, Min(0.1f)] float broodWindUp = 0.9f;
    [SerializeField, Min(0f)] float broodCooldown = 11f;

    [Header("Pulo esmagador")]
    [SerializeField] Vector2 leapRange = new(6f, 18f);
    [SerializeField, Min(0.1f)] float leapWindUp = 0.7f;
    [SerializeField, Min(0.1f)] float leapDuration = 0.75f;
    [SerializeField, Min(0f)] float leapHeight = 5f;
    [SerializeField, Min(0.5f)] float landRadius = 4.5f;
    [SerializeField, Min(0f)] float leapDamage = 32f;
    [SerializeField, Min(0f)] float leapCooldown = 5f;
    [SerializeField] Vector2 landKnockback = new(12f, 6f);

    [Header("Mordida")]
    [SerializeField, Min(0.5f)] float biteRange = 4f;
    [SerializeField, Range(10f, 180f)] float biteAngle = 100f;
    [SerializeField, Min(0f)] float biteDamage = 22f;
    [SerializeField, Min(0.05f)] float biteWindUp = 0.35f;
    [SerializeField, Min(0f)] float biteCooldown = 1.2f;

    [Header("Geral")]
    [SerializeField, Min(0f)] float recoverTime = 0.5f;

    [Header("Visual")]
    [Tooltip("Parte que agacha/ergue nos avisos (Body externo, não a raiz).")]
    [SerializeField] Transform body;
    [Tooltip("Círculo vermelho no chão (aviso do pulo). Solto da rainha ao nascer.")]
    [SerializeField] Renderer leapTelegraph;
    [Tooltip("Ponta do abdômen: de onde sai a teia e o que brilha nos avisos.")]
    [SerializeField] Transform spinner;
    [SerializeField] Renderer spinnerRenderer;

    [Header("Recompensa")]
    [SerializeField] ItemDatabase database;
    [SerializeField] ItemPickup pickupPrefab;

    public override bool IsBoss => true;
    public string BossName => bossName;
    public bool IsEnraged => Health.Current <= Health.Max * enrageAt;
    public bool IsAttacking => attacking != null;
    public string CurrentAttack { get; private set; } = "";
    // Para testes/depuração
    public int WebVolleys { get; private set; }
    public int BroodsSummoned { get; private set; }
    public int LeapsPerformed { get; private set; }
    public int LeapsLanded { get; private set; }
    public int BitesLanded { get; private set; }
    public ItemPickup DroppedItem { get; private set; }
    public IReadOnlyList<EnemyBase> Brood => brood;
    public int BroodAlive { get { brood.RemoveAll(b => b == null || !b.IsAlive); return brood.Count; } }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly Color TelegraphRed = new(1f, 0.15f, 0.05f, 0.8f);

    readonly List<EnemyBase> brood = new();
    Coroutine attacking;
    float nextWeb, nextBrood, nextLeap, nextBite;
    bool enragedBroodDone, leaping;
    Vector3 bodyRestPos;
    Quaternion bodyRestRot;
    MaterialPropertyBlock block;

    float T(float t) => IsEnraged ? t * enrageTimeScale : t;

    protected override void Awake()
    {
        base.Awake();
        block = new MaterialPropertyBlock();
        if (body != null) { bodyRestPos = body.localPosition; bodyRestRot = body.localRotation; }
        // Aviso no chão fica solto no mundo (a rainha se mexe, o aviso não)
        if (leapTelegraph != null) { leapTelegraph.transform.SetParent(null, true); leapTelegraph.gameObject.SetActive(false); }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        BossTracker.Register(this);
        Agent.updatePosition = true;
        Agent.updateRotation = true;
        leaping = false;
        enragedBroodDone = false;
        nextWeb = Time.time + 1.2f;
        nextBrood = Time.time + 4f;
        nextLeap = Time.time + 2.5f;
        nextBite = Time.time + 0.5f;
        ResetVisuals();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        BossTracker.Unregister(this);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        BossTracker.Unregister(this);
        if (leapTelegraph != null) Destroy(leapTelegraph.gameObject);
    }

    protected override void Update()
    {
        base.Update();
        if (IsAlive && !HasValidTarget && attacking != null) CancelAttack();
    }

    protected override void Tick()
    {
        if (attacking != null) return;
        float d = FlatDistanceToTarget();

        // Fúria: a ninhada vem na hora (uma vez)
        if (IsEnraged && !enragedBroodDone && spiderlingPrefab != null)
        {
            enragedBroodDone = true;
            Begin(BroodRoutine());
            return;
        }
        if (d <= biteRange && Time.time >= nextBite) { Begin(BiteRoutine()); return; }
        if (spiderlingPrefab != null && Time.time >= nextBrood && BroodAlive < maxBrood) { Begin(BroodRoutine()); return; }
        if (d >= leapRange.x && d <= leapRange.y && Time.time >= nextLeap) { Begin(LeapRoutine()); return; }
        if (webPrefab != null && d >= webRange.x && d <= webRange.y && Time.time >= nextWeb) { Begin(WebRoutine()); return; }

        if (d > biteRange * 0.85f) MoveTo(Target.position);
        else { StopMoving(); FaceTarget(); }
    }

    void Begin(IEnumerator routine)
    {
        StopMoving();
        attacking = StartCoroutine(Run(routine));
    }

    IEnumerator Run(IEnumerator routine)
    {
        yield return routine;
        ResetVisuals();
        CurrentAttack = "";
        attacking = null;
    }

    void CancelAttack()
    {
        if (attacking != null) StopCoroutine(attacking);
        attacking = null;
        CurrentAttack = "";
        if (leaping) EndLeap(transform.position);
        ResetVisuals();
    }

    IEnumerator Recover()
    {
        for (float t = 0f; t < T(recoverTime); t += Time.deltaTime) { FaceTarget(); yield return null; }
    }

    // ---------- Mordida ----------

    IEnumerator BiteRoutine()
    {
        CurrentAttack = "Mordida";
        nextBite = Time.time + T(biteCooldown);
        float windUp = T(biteWindUp);
        for (float t = 0f; t < windUp; t += Time.deltaTime)
        {
            FaceTarget();
            float k = t / windUp;
            if (body != null) body.localRotation = bodyRestRot * Quaternion.Euler(-12f * k, 0f, 0f); // ergue a frente
            Glow(k * 0.5f);
            yield return null;
        }
        if (body != null) body.localRotation = bodyRestRot * Quaternion.Euler(10f, 0f, 0f); // dá o bote
        Vector3 d = Target.position - transform.position; d.y = 0f;
        float reach = biteRange * Mathf.Max(1f, transform.lossyScale.x * 0.5f);
        if (d.magnitude <= reach && Vector3.Angle(transform.forward, d) <= biteAngle * 0.5f && TargetHealth != null)
        {
            Vector3 dir = d.sqrMagnitude > 0.01f ? d.normalized : transform.forward;
            TargetHealth.TakeDamage(new DamageInfo(biteDamage * DamageMultiplier, gameObject, Target.position + Vector3.up, dir));
            if (Target.TryGetComponent(out PlayerMovement pm)) pm.AddKnockback(dir * 6f + Vector3.up * 2f);
            BitesLanded++;
        }
        yield return Recover();
    }

    // ---------- Rajada de teia ----------

    IEnumerator WebRoutine()
    {
        CurrentAttack = "Teia";
        nextWeb = Time.time + T(webCooldown);
        float windUp = T(webWindUp);
        for (float t = 0f; t < windUp; t += Time.deltaTime)
        {
            FaceAway();
            float k = t / windUp;
            Glow(k);
            if (body != null) body.localRotation = bodyRestRot * Quaternion.Euler(-16f * k, 0f, 0f); // ergue o abdômen
            yield return null;
        }
        Glow(0f);
        FireVolley();
        WebVolleys++;
        yield return Recover();
    }

    void FireVolley()
    {
        if (webPrefab == null) return;
        Vector3 from = spinner != null ? spinner.position : transform.position + Vector3.up * 1.5f;
        Vector3 target = Target.position;
        if (Target.TryGetComponent(out PlayerMovement pm)) { var v = pm.Velocity; v.y = 0f; target += v * 0.35f; }
        Vector3 flat = target - transform.position; flat.y = 0f;
        float dist = Mathf.Max(2f, flat.magnitude);
        int count = IsEnraged ? enragedWebCount : webCount;
        for (int i = 0; i < count; i++)
        {
            // leque em volta do player (a do meio vai nele)
            float angle = (i - (count - 1) * 0.5f) * webSpread;
            Vector3 aim = transform.position + Quaternion.Euler(0f, angle, 0f) * flat.normalized * dist;
            aim.y = target.y;
            float flight = Mathf.Clamp(Vector3.Distance(from, aim) / 20f, 0.4f, 1.1f);
            var shot = Instantiate(webPrefab, from, Quaternion.identity);
            shot.Launch(from, aim, flight, gameObject, webPatchPrefab, rootDuration);
        }
    }

    void FaceAway()
    {
        Vector3 d = transform.position - Target.position; d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), turnSpeed * Time.deltaTime);
    }

    // ---------- Ninhada ----------

    IEnumerator BroodRoutine()
    {
        CurrentAttack = "Ninhada";
        nextBrood = Time.time + T(broodCooldown);
        float windUp = T(broodWindUp);
        for (float t = 0f; t < windUp; t += Time.deltaTime)
        {
            float k = t / windUp;
            // abdômen pulsa e brilha: "vai botar os filhotes"
            Glow(0.5f + 0.5f * Mathf.Sin(t * 18f));
            if (body != null) body.localPosition = bodyRestPos + Vector3.up * (Mathf.Sin(t * 18f) * 0.05f * k);
            yield return null;
        }
        Glow(0f);
        SummonBrood(IsEnraged ? enragedBroodCount : broodCount);
        BroodsSummoned++;
        yield return Recover();
    }

    /// <summary>Invoca até `count` aranhinhas em volta (respeitando o limite vivas). Retorna quantas nasceram.</summary>
    public int SummonBrood(int count)
    {
        if (spiderlingPrefab == null) return 0;
        int room = Mathf.Min(count, maxBrood - BroodAlive);
        var dm = DifficultyManager.Instance;
        float hpMult = (dm != null ? dm.EnemyHealthMultiplier : 1f) * spiderlingHealth;
        float dmgMult = (dm != null ? dm.EnemyDamageMultiplier : 1f) * spiderlingDamage;
        int spawned = 0;
        for (int i = 0; i < room; i++)
        {
            float a = (i / (float)Mathf.Max(1, room)) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            Vector3 around = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(2.5f, 4.5f);
            if (!NavMesh.SamplePosition(around, out var hit, 3f, NavMesh.AllAreas)) continue;
            Vector3 look = Target != null ? Target.position - hit.position : transform.forward; look.y = 0f;
            var s = Instantiate(spiderlingPrefab, hit.position, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
            s.name = "Aranhinha";
            s.SetBaseScale(s.transform.localScale * spiderlingScale);
            s.ApplyScaling(hpMult, dmgMult);
            brood.Add(s);
            spawned++;
            ItemVfx.Explosion(hit.position + Vector3.up * 0.3f, 1.2f, new Color(0.75f, 0.3f, 1f, 0.5f));
        }
        return spawned;
    }

    // ---------- Pulo esmagador ----------

    IEnumerator LeapRoutine()
    {
        CurrentAttack = "Pulo";
        nextLeap = Time.time + T(leapCooldown);
        float windUp = T(leapWindUp);
        Vector3 landing = PickLanding();
        for (float t = 0f; t < windUp; t += Time.deltaTime)
        {
            FaceTarget();
            float k = t / windUp;
            if (k < 0.7f) landing = PickLanding(); // segue o player no começo do aviso, depois trava
            ShowTelegraph(landing, k);
            Crouch(k);
            yield return null;
        }
        Crouch(0f);

        // No ar: o agente fica parado e quem move é o pulo
        leaping = true;
        LeapsPerformed++;
        Agent.updatePosition = false;
        Agent.updateRotation = false;
        Vector3 from = transform.position;
        float duration = T(leapDuration);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            Vector3 dir = landing - from; dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
            transform.position = Vector3.Lerp(from, landing, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * leapHeight);
            ShowTelegraph(landing, 1f);
            yield return null;
        }
        EndLeap(landing);
        GameFeel.Shake(0.8f);
        ItemVfx.Explosion(landing + Vector3.up * 0.3f, landRadius, new Color(0.7f, 0.3f, 1f, 0.45f));

        if (TargetHealth != null && TargetHealth.IsAlive)
        {
            Vector3 d = Target.position - landing;
            if (Mathf.Abs(d.y) <= 3f)
            {
                d.y = 0f;
                if (d.magnitude <= landRadius)
                {
                    Vector3 dir = d.sqrMagnitude > 0.01f ? d.normalized : transform.forward;
                    TargetHealth.TakeDamage(new DamageInfo(leapDamage * DamageMultiplier, gameObject, Target.position + Vector3.up, dir));
                    if (Target.TryGetComponent(out PlayerMovement pm)) pm.AddKnockback(dir * landKnockback.x + Vector3.up * landKnockback.y);
                    LeapsLanded++;
                }
            }
        }
        yield return Recover();
    }

    void EndLeap(Vector3 at)
    {
        leaping = false;
        HideTelegraph();
        if (NavMesh.SamplePosition(at, out var hit, 6f, NavMesh.AllAreas)) at = hit.position;
        if (Agent.enabled) Agent.Warp(at);
        else transform.position = at;
        Agent.updatePosition = true;
        Agent.updateRotation = true;
    }

    Vector3 PickLanding()
    {
        Vector3 p = Target.position;
        if (Target.TryGetComponent(out PlayerMovement pm)) { var v = pm.Velocity; v.y = 0f; p += v * 0.4f; }
        Vector3 flat = p - transform.position; flat.y = 0f;
        if (flat.magnitude > leapRange.y) p = transform.position + flat.normalized * leapRange.y;
        if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) p = hit.position;
        return p;
    }

    // ---------- Visual ----------

    void Crouch(float k)
    {
        if (body == null) return;
        float shake = k > 0.5f ? Mathf.Sin(Time.time * 70f) * 0.05f * k : 0f;
        body.localPosition = bodyRestPos + new Vector3(shake, -0.3f * k, 0f);
        body.localRotation = bodyRestRot * Quaternion.Euler(8f * k, 0f, 0f);
    }

    void ShowTelegraph(Vector3 at, float k)
    {
        if (leapTelegraph == null) return;
        if (!leapTelegraph.gameObject.activeSelf) leapTelegraph.gameObject.SetActive(true);
        leapTelegraph.enabled = true;
        var t = leapTelegraph.transform;
        t.position = at + Vector3.up * 0.05f;
        t.rotation = Quaternion.identity;
        float size = landRadius * 2f * Mathf.Lerp(0.3f, 1f, k);
        t.localScale = new Vector3(size, 0.01f, size); // solto no mundo: escala = tamanho
        leapTelegraph.GetPropertyBlock(block);
        block.SetColor(BaseColorId, TelegraphRed * new Color(1f, 1f, 1f, 0.3f + 0.7f * k));
        leapTelegraph.SetPropertyBlock(block);
    }

    void HideTelegraph()
    {
        if (leapTelegraph != null) leapTelegraph.gameObject.SetActive(false);
    }

    void Glow(float k)
    {
        if (spinnerRenderer == null) return;
        spinnerRenderer.GetPropertyBlock(block);
        block.SetColor(EmissionId, new Color(1f, 0.45f, 0.9f) * (3f * Mathf.Clamp01(k)));
        spinnerRenderer.SetPropertyBlock(block);
    }

    void ResetVisuals()
    {
        HideTelegraph();
        Glow(0f);
        if (body != null) { body.localPosition = bodyRestPos; body.localRotation = bodyRestRot; }
    }

    // ---------- Morte ----------

    protected override void HandleDeath(DamageInfo killingBlow)
    {
        CancelAttack();
        base.HandleDeath(killingBlow);
        // A ninhada morre junto (o cano não fica esperando aranhinha perdida)
        foreach (var s in brood.ToArray())
            if (s != null && s.IsAlive) s.Health.Kill();
        brood.Clear();
        DropLegendary();
    }

    void DropLegendary()
    {
        if (database == null || pickupPrefab == null) return;
        // Modificador "só amaldiçoados": o banco já sorteia só amaldiçoado
        var item = RunModifiers.Is(RunModifier.CursedItemsOnly)
            ? database.RandomWeighted()
            : database.Random(ItemRarity.Lendario) ?? database.RandomWeighted(0f, 1f, 0f);
        if (item == null) return;
        Vector3 land = transform.position + transform.forward * 2.5f;
        if (NavMesh.SamplePosition(land, out var hit, 4f, NavMesh.AllAreas)) land = hit.position;
        DroppedItem = ItemPickup.Spawn(pickupPrefab, item, transform.position + Vector3.up * 2f);
        DroppedItem.Launch(land + Vector3.up, 0.8f, 3f);
    }

#if UNITY_EDITOR
    public void EditorSetup(WebShot web, WebPatch patch, EnemyBase spiderling, Transform bodyT, Renderer telegraph,
                            Transform spinnerT, Renderer spinnerR, ItemDatabase db, ItemPickup pickup)
    {
        webPrefab = web; webPatchPrefab = patch; spiderlingPrefab = spiderling; body = bodyT; leapTelegraph = telegraph;
        spinner = spinnerT; spinnerRenderer = spinnerR; database = db; pickupPrefab = pickup;
    }
#endif
}
