using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Gafanhoto: atirador de elite do salto. Fica longe do player e, quando dá, salta de longe
/// em cima dele. Tudo com aviso:
/// - Salto: agacha nas patas traseiras (o corpo desce e treme) e um círculo vermelho marca onde
///   vai cair. O círculo segue o player no começo do aviso e trava no fim: dá para sair dele.
///   O pouso bate forte (dano em área + empurrão) e o gafanhoto fica alguns instantes
///   atordoado no chão, a janela para revidar. Depois pula para trás para abrir distância.
/// - Coice: se o player chega perto, ele vira de costas, arma as patas traseiras e dá um coice
///   que joga o player longe. Virar de costas é o aviso.
/// </summary>
public class Grasshopper : EnemyBase
{
    enum State { Stalk, LeapWindUp, Leaping, Stunned, KickWindUp, KickRecover, Hop }

    [Header("Salto de longe")]
    [Tooltip("Distância (mín, máx) em que ele decide saltar no player.")]
    [SerializeField] Vector2 leapRange = new(7f, 24f);
    [Tooltip("Distância que ele tenta manter do player entre os saltos.")]
    [SerializeField, Min(2f)] float preferredDistance = 15f;
    [SerializeField, Min(0.1f)] float leapWindUp = 0.95f;
    [Tooltip("Parte do aviso (0..1) em que o ponto de queda trava.")]
    [SerializeField, Range(0.2f, 1f)] float lockAt = 0.65f;
    [Tooltip("Velocidade horizontal do salto (m/s): saltos longos demoram mais no ar.")]
    [SerializeField, Min(1f)] float leapSpeed = 20f;
    [SerializeField, Min(0f)] float leapHeight = 5.5f;
    [SerializeField, Min(0f)] float leapDamage = 26f;
    [Tooltip("Raio do dano ao pousar (também o tamanho do círculo de aviso).")]
    [SerializeField, Min(0.5f)] float landRadius = 2.4f;
    [SerializeField] Vector2 landKnockback = new(12f, 6f);
    [SerializeField, Min(0f)] float leapCooldown = 3.2f;
    [Tooltip("Antecipa o movimento do player na hora de escolher onde cair.")]
    [SerializeField, Min(0f)] float leadTime = 0.3f;
    [Tooltip("Segundos atordoado no chão depois de pousar.")]
    [SerializeField, Min(0f)] float stunTime = 1.1f;

    [Header("Coice")]
    [SerializeField, Min(0.5f)] float kickRange = 2.8f;
    [SerializeField, Min(0.1f)] float kickWindUp = 0.5f;
    [SerializeField, Min(0f)] float kickDamage = 18f;
    [SerializeField] Vector2 kickKnockback = new(15f, 5f);
    [SerializeField, Min(0f)] float kickCooldown = 1.8f;
    [SerializeField, Min(0f)] float kickRecover = 0.45f;

    [Header("Pulinho para trás")]
    [SerializeField, Min(0.5f)] float hopDistance = 7f;
    [SerializeField, Min(0.1f)] float hopDuration = 0.5f;
    [SerializeField, Min(0f)] float hopHeight = 2.2f;

    [Header("Visual")]
    [Tooltip("Parte que agacha/inclina nos avisos (Body externo do prefab, pai do modelo).")]
    [SerializeField] Transform body;
    [Tooltip("Círculo vermelho no chão onde ele vai cair.")]
    [SerializeField] Renderer leapTelegraph;

    public int Leaps { get; private set; }
    public int LeapsLanded { get; private set; }
    public int Kicks { get; private set; }
    public int KicksLanded { get; private set; }
    public bool IsLeaping => state == State.Leaping;
    public bool IsTelegraphingLeap => state == State.LeapWindUp;
    public bool IsStunned => state == State.Stunned;
    public bool IsWindingUpKick => state == State.KickWindUp;
    /// <summary>Onde o salto vai cair (válido durante o aviso e o salto).</summary>
    public Vector3 LeapTarget => jumpTo;
    /// <summary>O ponto de queda já travou (o player pode sair do círculo).</summary>
    public bool LeapLocked => state == State.Leaping || (state == State.LeapWindUp && Elapsed / leapWindUp >= lockAt);

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly Color TelegraphRed = new(1f, 0.15f, 0.05f, 0.8f);
    static readonly Color DustColor = new(0.55f, 0.45f, 0.3f, 0.5f);

    State state;
    float stateStart, nextLeap, nextKick, jumpDuration, jumpHeight;
    float circleSign = 1f;
    Vector3 jumpFrom, jumpTo, bodyRestPos;
    Quaternion bodyRestRot;
    MaterialPropertyBlock block;

    protected override void Awake()
    {
        base.Awake();
        block = new MaterialPropertyBlock();
        if (body != null) { bodyRestPos = body.localPosition; bodyRestRot = body.localRotation; }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Agent.updatePosition = true;
        Agent.updateRotation = true;
        state = State.Stalk;
        nextLeap = Time.time + Random.Range(1f, 2f);
        nextKick = Time.time;
        circleSign = Random.value < 0.5f ? -1f : 1f;
        ResetVisuals();
    }

    void Enter(State s) { state = s; stateStart = Time.time; }
    float Elapsed => Time.time - stateStart;
    bool Airborne => state == State.Leaping || state == State.Hop;

    protected override void Update()
    {
        // No ar quem move é o salto: o agente fica parado e a base não mexe nele
        if (Airborne && IsAlive) { UpdateJump(); return; }
        base.Update();
        if (!HasValidTarget && state != State.Stalk && state != State.Stunned) { Enter(State.Stalk); ResetVisuals(); }
        if (state == State.Stunned && IsAlive) UpdateStun();
    }

    protected override void Tick()
    {
        float d = FlatDistanceToTarget();
        switch (state)
        {
            case State.Stalk:
                if (d <= kickRange && Time.time >= nextKick) { StartKick(); return; }
                if (Time.time >= nextLeap && d >= leapRange.x && d <= leapRange.y && CanSeeTarget()) { StartLeapWindUp(); return; }
                if (d > preferredDistance + 3f) MoveTo(Target.position);
                else if (d < leapRange.x && Time.time >= nextKick + 0.6f) StartHop();
                else Circle();
                break;

            case State.LeapWindUp:
                FaceTarget();
                float k = Mathf.Clamp01(Elapsed / leapWindUp);
                if (k < lockAt) jumpTo = PickLanding(); // segue o player no começo do aviso, depois trava
                ShowTelegraph(jumpTo, k);
                Crouch(k);
                if (k >= 1f) StartJump(State.Leaping, jumpTo, leapHeight, Mathf.Clamp(FlatDistance(jumpTo) / leapSpeed, 0.55f, 1.25f));
                break;

            case State.KickWindUp:
                FaceAway();
                float w = Mathf.Clamp01(Elapsed / kickWindUp);
                CockLegs(w);
                if (w >= 1f) Kick();
                break;

            case State.KickRecover:
                CockLegs(1f - Mathf.Clamp01(Elapsed / 0.15f));
                if (Elapsed >= kickRecover) Enter(State.Stalk);
                break;
        }
    }

    // ---------- Salto ----------

    void StartLeapWindUp()
    {
        StopMoving();
        jumpTo = PickLanding();
        Enter(State.LeapWindUp);
        ShowTelegraph(jumpTo, 0f); // o círculo aparece junto com o aviso
    }

    Vector3 PickLanding()
    {
        Vector3 p = Target.position;
        if (Target.TryGetComponent(out PlayerMovement pm)) { var v = pm.Velocity; v.y = 0f; p += v * leadTime; }
        Vector3 flat = p - transform.position; flat.y = 0f;
        if (flat.magnitude > leapRange.y) p = transform.position + flat.normalized * leapRange.y;
        if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) p = hit.position;
        return p;
    }

    float FlatDistance(Vector3 p)
    {
        Vector3 d = p - transform.position; d.y = 0f;
        return d.magnitude;
    }

    void StartJump(State kind, Vector3 to, float height, float duration)
    {
        StopMoving();
        Agent.updatePosition = false;
        Agent.updateRotation = false;
        jumpFrom = transform.position;
        jumpTo = to;
        jumpHeight = height;
        jumpDuration = duration;
        Crouch(0f);
        if (kind == State.Leaping) Leaps++;
        Enter(kind);
    }

    void UpdateJump()
    {
        float k = Mathf.Clamp01(Elapsed / jumpDuration);
        Vector3 pos = Vector3.Lerp(jumpFrom, jumpTo, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * jumpHeight);
        Vector3 dir = jumpTo - jumpFrom; dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
        {
            // pulinho para trás: continua olhando o player
            Vector3 look = state == State.Hop && Target != null ? Target.position - transform.position : dir;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
        }
        transform.position = pos;
        // corpo acompanha o arco: sobe de nariz para cima, desce de nariz para baixo
        if (body != null) body.localRotation = bodyRestRot * Quaternion.Euler(Mathf.Lerp(-25f, 30f, k), 0f, 0f);
        if (state == State.Leaping) ShowTelegraph(jumpTo, 1f);
        if (k >= 1f) Land();
    }

    void Land()
    {
        bool wasLeap = state == State.Leaping;
        Agent.Warp(jumpTo);
        Agent.updatePosition = true;
        Agent.updateRotation = true;
        HideTelegraph();
        if (body != null) body.localRotation = bodyRestRot;

        if (!wasLeap)
        {
            Enter(State.Stalk);
            return;
        }
        nextLeap = Time.time + leapCooldown;
        Enter(State.Stunned);
        ItemVfx.Explosion(jumpTo + Vector3.up * 0.3f, landRadius, DustColor, 0.35f);

        if (TargetHealth == null || !TargetHealth.IsAlive) return;
        Vector3 d = Target.position - jumpTo;
        if (Mathf.Abs(d.y) > 2.5f) return;
        if (Target.TryGetComponent(out PlayerMovement floatMove) && floatMove.IsFloating) return; // bolha de sabão: passa por baixo
        d.y = 0f;
        if (d.magnitude > landRadius) return;
        Vector3 dir = d.sqrMagnitude > 0.01f ? d.normalized : transform.forward;
        TargetHealth.TakeDamage(new DamageInfo(leapDamage * DamageMultiplier, gameObject, Target.position + Vector3.up, dir));
        if (Target.TryGetComponent(out PlayerMovement pm)) pm.AddKnockback(dir * landKnockback.x + Vector3.up * landKnockback.y);
        LeapsLanded++;
    }

    void UpdateStun()
    {
        // pousou: fica tonto (corpo achatado balançando) e depois pula para longe
        float k = Mathf.Clamp01(Elapsed / Mathf.Max(0.01f, stunTime));
        if (body != null)
        {
            float wobble = Mathf.Sin(Elapsed * 18f) * 6f * (1f - k);
            body.localPosition = bodyRestPos + Vector3.down * (0.2f * (1f - k));
            body.localRotation = bodyRestRot * Quaternion.Euler(0f, 0f, wobble);
        }
        if (k < 1f) return;
        ResetVisuals();
        if (HasValidTarget && FlatDistanceToTarget() < leapRange.x) StartHop();
        else Enter(State.Stalk);
    }

    // ---------- Pulinho para trás ----------

    void StartHop()
    {
        Vector3 away = transform.position - Target.position; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        away = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * away.normalized;
        Vector3 to = transform.position + away * hopDistance;
        if (!NavMesh.SamplePosition(to, out var hit, 3f, NavMesh.AllAreas))
        {
            Enter(State.Stalk);
            nextKick = Time.time; // encurralado: deixa o coice resolver
            return;
        }
        StartJump(State.Hop, hit.position, hopHeight, hopDuration);
    }

    // ---------- Coice ----------

    void StartKick()
    {
        StopMoving();
        Kicks++;
        Enter(State.KickWindUp);
    }

    void Kick()
    {
        nextKick = Time.time + kickCooldown;
        Enter(State.KickRecover);
        if (TargetHealth == null || !TargetHealth.IsAlive) return;
        // de costas: acerta quem está atrás dele
        Vector3 d = Target.position - transform.position; d.y = 0f;
        if (d.magnitude > kickRange + 0.8f || Vector3.Dot(-transform.forward, d.normalized) < 0.2f) return;
        Vector3 dir = d.sqrMagnitude > 0.01f ? d.normalized : -transform.forward;
        TargetHealth.TakeDamage(new DamageInfo(kickDamage * DamageMultiplier, gameObject, Target.position + Vector3.up, dir));
        if (Target.TryGetComponent(out PlayerMovement pm)) pm.AddKnockback(dir * kickKnockback.x + Vector3.up * kickKnockback.y);
        KicksLanded++;
    }

    // ---------- Movimento ----------

    bool CanSeeTarget()
    {
        Vector3 from = transform.position + Vector3.up * 1.2f;
        Vector3 to = Target.position + Vector3.up * 1f;
        foreach (var hit in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(Target)) continue;
            if (hit.collider.GetComponentInParent<EnemyBase>() != null) continue;
            return false;
        }
        return true;
    }

    void Circle()
    {
        Vector3 away = transform.position - Target.position; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        Vector3 around = Quaternion.Euler(0f, circleSign * 30f, 0f) * away.normalized;
        Vector3 dest = Target.position + around * preferredDistance;
        if (NavMesh.SamplePosition(dest, out var hit, 3f, NavMesh.AllAreas)) MoveTo(hit.position);
        else circleSign = -circleSign;
    }

    void FaceAway()
    {
        Vector3 d = transform.position - Target.position; d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), turnSpeed * Time.deltaTime);
    }

    // ---------- Visual ----------

    void Crouch(float k)
    {
        if (body == null) return;
        float shake = k > 0.4f ? Mathf.Sin(Time.time * 75f) * 0.05f * k : 0f;
        body.localPosition = bodyRestPos + new Vector3(shake, -0.4f * k, 0f);
        body.localRotation = bodyRestRot * Quaternion.Euler(-18f * k, 0f, 0f); // nariz para cima, pronto para saltar
    }

    void CockLegs(float k)
    {
        if (body == null) return;
        // de costas, abaixa a frente e ergue a traseira: as patas armam o coice
        float shake = k > 0.5f ? Mathf.Sin(Time.time * 60f) * 0.03f : 0f;
        body.localPosition = bodyRestPos + new Vector3(shake, 0.1f * k, 0f);
        body.localRotation = bodyRestRot * Quaternion.Euler(22f * k, 0f, 0f);
    }

    void ShowTelegraph(Vector3 at, float k)
    {
        if (leapTelegraph == null) return;
        leapTelegraph.enabled = true;
        var t = leapTelegraph.transform;
        t.position = at + Vector3.up * 0.05f;
        t.rotation = Quaternion.identity;
        float size = landRadius * 2f * Mathf.Lerp(0.3f, 1f, k);
        Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(size / parent.x, 0.01f / parent.y, size / parent.z);
        leapTelegraph.GetPropertyBlock(block);
        // travou: pisca mais forte para avisar que dá para sair
        float locked = k >= lockAt ? 0.85f + 0.15f * Mathf.Sin(Time.time * 30f) : 0.3f + 0.6f * k;
        block.SetColor(BaseColorId, TelegraphRed * new Color(1f, 1f, 1f, locked));
        leapTelegraph.SetPropertyBlock(block);
    }

    void HideTelegraph()
    {
        if (leapTelegraph != null) leapTelegraph.enabled = false;
    }

    void ResetVisuals()
    {
        HideTelegraph();
        if (body != null) { body.localPosition = bodyRestPos; body.localRotation = bodyRestRot; }
    }

    protected override void HandleDeath(DamageInfo killingBlow)
    {
        if (Airborne)
        {
            // morreu no ar: cai onde está
            Agent.updatePosition = true;
            Agent.updateRotation = true;
            if (NavMesh.SamplePosition(transform.position, out var hit, 8f, NavMesh.AllAreas)) Agent.Warp(hit.position);
        }
        state = State.Stalk;
        ResetVisuals();
        base.HandleDeath(killingBlow);
    }

#if UNITY_EDITOR
    public void EditorSetup(Transform bodyT, Renderer telegraph)
    {
        body = bodyT; leapTelegraph = telegraph;
    }
#endif
}
