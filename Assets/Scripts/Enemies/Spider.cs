using UnityEngine;

/// <summary>
/// Aranha: caçadora que pula. Duas armas, ambas com aviso:
/// - Pulo: agacha (corpo desce e treme) e um círculo vermelho marca onde vai cair;
///   salta em arco e causa dano em área + empurrão ao pousar.
/// - Teia: vira de costas, a fiandeira brilha e ela cospe uma bola de teia em arco.
///   Acerto direto ou pisar na teia no chão prende o player por um instante.
/// De perto demais, recua para ter espaço de pular de novo.
/// </summary>
public class Spider : EnemyBase
{
    enum State { Hunt, LeapWindUp, Leaping, Recover, WebWindUp }

    [Header("Pulo")]
    [Tooltip("Distância (mín, máx) em que ela decide pular.")]
    [SerializeField] Vector2 leapRange = new(4f, 12f);
    [SerializeField, Min(0.1f)] float leapWindUp = 0.55f;
    [SerializeField, Min(0.1f)] float leapDuration = 0.6f;
    [SerializeField, Min(0f)] float leapHeight = 3.2f;
    [SerializeField, Min(0f)] float leapDamage = 14f;
    [Tooltip("Raio do dano ao pousar (também o tamanho do círculo de aviso).")]
    [SerializeField, Min(0.5f)] float landRadius = 2.4f;
    [SerializeField, Min(0f)] float leapCooldown = 2.8f;
    [SerializeField] Vector2 landKnockback = new(7f, 4f);
    [Tooltip("Antecipa o movimento do player na hora de escolher onde cair.")]
    [SerializeField, Min(0f)] float leadTime = 0.35f;
    [SerializeField, Min(0f)] float recoverTime = 0.6f;
    [Tooltip("Distância em que ela ronda o player entre ataques (dentro do alcance do pulo e da teia).")]
    [SerializeField, Min(1f)] float orbitDistance = 8.5f;

    [Header("Teia")]
    [SerializeField] WebShot webPrefab;
    [SerializeField] WebPatch webPatchPrefab;
    [SerializeField] Vector2 webRange = new(5f, 20f);
    [SerializeField, Min(0.1f)] float webWindUp = 0.45f;
    [SerializeField, Min(0f)] float webCooldown = 5.5f;
    [SerializeField, Min(0.1f)] float rootDuration = 1.2f;

    [Header("Visual")]
    [Tooltip("Parte que agacha/treme no aviso do pulo (filho do modelo, não a raiz).")]
    [SerializeField] Transform body;
    [Tooltip("Círculo vermelho no chão onde ela vai cair.")]
    [SerializeField] Renderer leapTelegraph;
    [Tooltip("Ponta do abdômen: de onde sai a teia e o que brilha no aviso.")]
    [SerializeField] Transform spinner;
    [SerializeField] Renderer spinnerRenderer;

    public int Leaps { get; private set; }
    public int Webs { get; private set; }
    public bool IsLeaping => state == State.Leaping;
    public bool IsTelegraphingLeap => state == State.LeapWindUp;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    // Vermelho do chefe, mas mais opaco: translúcido demais, sobre a grama vira marrom
    static readonly Color TelegraphRed = new(1f, 0.15f, 0.05f, 0.8f);

    State state;
    float stateStart, nextLeap, nextWeb;
    float circleSign = 1f;
    Vector3 leapFrom, leapTo, bodyRestPos;
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
        state = State.Hunt;
        nextLeap = Time.time + Random.Range(0.6f, 1.4f);
        nextWeb = Time.time + Random.Range(0.8f, 2f);
        circleSign = Random.value < 0.5f ? -1f : 1f;
        ResetVisuals();
    }

    void Enter(State s) { state = s; stateStart = Time.time; }
    float Elapsed => Time.time - stateStart;

    protected override void Update()
    {
        // No ar o agente fica parado e quem move é o pulo: não deixa a base mexer nele
        if (state == State.Leaping && IsAlive) { UpdateLeap(); return; }
        base.Update();
        if (!HasValidTarget && state != State.Hunt) { Enter(State.Hunt); ResetVisuals(); }
    }

    protected override void Tick()
    {
        float d = FlatDistanceToTarget();
        switch (state)
        {
            case State.Hunt:
                bool leapReady = Time.time >= nextLeap && d >= leapRange.x && d <= leapRange.y;
                bool webReady = Time.time >= nextWeb && d >= webRange.x && d <= webRange.y && webPrefab != null;
                // teia quando o pulo não está pronto ou o player está longe; senão pula
                if (webReady && (!leapReady || d > leapRange.y * 0.75f)) { StartWebWindUp(); return; }
                if (leapReady) { StartLeapWindUp(); return; }
                if (d > leapRange.y) MoveTo(Target.position);
                else if (d < leapRange.x * 0.9f) Retreat();
                else Circle(); // entre ataques, ronda o player (abre distância para a teia)
                break;

            case State.LeapWindUp:
                FaceTarget();
                float k = Mathf.Clamp01(Elapsed / leapWindUp);
                if (k < 0.7f) leapTo = PickLanding(); // segue o player no começo do aviso, depois trava
                ShowTelegraph(leapTo, k);
                Crouch(k);
                if (k >= 1f) StartLeap();
                break;

            case State.Recover:
                FaceTarget();
                if (Elapsed >= recoverTime) Enter(State.Hunt);
                break;

            case State.WebWindUp:
                FaceAway();
                float w = Mathf.Clamp01(Elapsed / webWindUp);
                Glow(w);
                if (body != null) body.localRotation = bodyRestRot * Quaternion.Euler(-14f * w, 0f, 0f); // ergue o abdômen
                if (w >= 1f) FireWeb();
                break;
        }
    }

    // ---------- Pulo ----------

    void StartLeapWindUp()
    {
        StopMoving();
        leapTo = PickLanding();
        Enter(State.LeapWindUp);
    }

    Vector3 PickLanding()
    {
        Vector3 p = Target.position;
        if (Target.TryGetComponent(out PlayerMovement pm)) { var v = pm.Velocity; v.y = 0f; p += v * leadTime; }
        // não pula mais longe que o alcance máximo
        Vector3 flat = p - transform.position; flat.y = 0f;
        if (flat.magnitude > leapRange.y) p = transform.position + flat.normalized * leapRange.y;
        if (UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) p = hit.position;
        return p;
    }

    void StartLeap()
    {
        StopMoving();
        Agent.updatePosition = false;
        Agent.updateRotation = false;
        leapFrom = transform.position;
        Leaps++;
        Crouch(0f);
        Enter(State.Leaping);
    }

    void UpdateLeap()
    {
        float k = Mathf.Clamp01(Elapsed / leapDuration);
        Vector3 pos = Vector3.Lerp(leapFrom, leapTo, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * leapHeight);
        Vector3 dir = leapTo - leapFrom; dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
        transform.position = pos;
        ShowTelegraph(leapTo, 1f);
        if (k >= 1f) Land();
    }

    void Land()
    {
        Agent.Warp(leapTo);
        Agent.updatePosition = true;
        Agent.updateRotation = true;
        HideTelegraph();
        nextLeap = Time.time + leapCooldown;
        Enter(State.Recover);

        if (TargetHealth == null || !TargetHealth.IsAlive) return;
        Vector3 d = Target.position - leapTo;
        if (Mathf.Abs(d.y) > 2.5f) return;
        d.y = 0f;
        if (d.magnitude > landRadius) return;
        Vector3 dir = d.sqrMagnitude > 0.01f ? d.normalized : transform.forward;
        TargetHealth.TakeDamage(new DamageInfo(leapDamage * DamageMultiplier, gameObject, Target.position + Vector3.up, dir));
        if (Target.TryGetComponent(out PlayerMovement pm)) pm.AddKnockback(dir * landKnockback.x + Vector3.up * landKnockback.y);
    }

    void Circle()
    {
        Vector3 away = transform.position - Target.position; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        Vector3 around = Quaternion.Euler(0f, circleSign * 25f, 0f) * away.normalized;
        Vector3 dest = Target.position + around * orbitDistance;
        if (UnityEngine.AI.NavMesh.SamplePosition(dest, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) MoveTo(hit.position);
        else circleSign = -circleSign; // bateu em algo: inverte o sentido
    }

    void Retreat()
    {
        Vector3 away = transform.position - Target.position; away.y = 0f;
        Vector3 dest = transform.position + away.normalized * 5f;
        if (UnityEngine.AI.NavMesh.SamplePosition(dest, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) MoveTo(hit.position);
        else MoveTo(transform.position + transform.right * 4f);
    }

    // ---------- Teia ----------

    void StartWebWindUp()
    {
        StopMoving();
        Enter(State.WebWindUp);
    }

    void FireWeb()
    {
        Glow(0f);
        if (body != null) body.localRotation = bodyRestRot;
        nextWeb = Time.time + webCooldown;
        nextLeap = Mathf.Max(nextLeap, Time.time + 0.8f);
        Webs++;
        Enter(State.Recover);
        if (webPrefab == null) return;
        Vector3 from = spinner != null ? spinner.position : transform.position + Vector3.up;
        Vector3 target = Target.position;
        if (Target.TryGetComponent(out PlayerMovement pm)) { var v = pm.Velocity; v.y = 0f; target += v * leadTime; }
        float flight = Mathf.Clamp(Vector3.Distance(from, target) / 20f, 0.4f, 1f);
        var shot = Instantiate(webPrefab, from, Quaternion.identity);
        shot.Launch(from, target, flight, gameObject, webPatchPrefab, rootDuration);
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
        float shake = k > 0.5f ? Mathf.Sin(Time.time * 70f) * 0.04f * k : 0f;
        body.localPosition = bodyRestPos + new Vector3(shake, -0.35f * k, 0f);
        body.localRotation = bodyRestRot * Quaternion.Euler(8f * k, 0f, 0f);
    }

    void ShowTelegraph(Vector3 at, float k)
    {
        if (leapTelegraph == null) return;
        leapTelegraph.enabled = true;
        var t = leapTelegraph.transform;
        t.position = at + Vector3.up * 0.05f;
        t.rotation = Quaternion.identity;
        float size = landRadius * 2f * Mathf.Lerp(0.3f, 1f, k);
        // escala no mundo, compensando a escala da raiz
        Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(size / parent.x, 0.01f / parent.y, size / parent.z);
        leapTelegraph.GetPropertyBlock(block);
        block.SetColor(BaseColorId, TelegraphRed * new Color(1f, 1f, 1f, 0.3f + 0.7f * k));
        leapTelegraph.SetPropertyBlock(block);
    }

    void HideTelegraph()
    {
        if (leapTelegraph != null) leapTelegraph.enabled = false;
    }

    void Glow(float k)
    {
        if (spinnerRenderer == null) return;
        spinnerRenderer.GetPropertyBlock(block);
        block.SetColor(EmissionId, new Color(0.9f, 0.85f, 1f) * (3f * k));
        spinnerRenderer.SetPropertyBlock(block);
    }

    void ResetVisuals()
    {
        HideTelegraph();
        Glow(0f);
        if (body != null) { body.localPosition = bodyRestPos; body.localRotation = bodyRestRot; }
    }

    protected override void HandleDeath(DamageInfo killingBlow)
    {
        if (state == State.Leaping)
        {
            // morreu no ar: cai onde está
            Agent.updatePosition = true;
            Agent.updateRotation = true;
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out var hit, 6f, UnityEngine.AI.NavMesh.AllAreas))
                Agent.Warp(hit.position);
        }
        state = State.Hunt;
        ResetVisuals();
        base.HandleDeath(killingBlow);
    }

#if UNITY_EDITOR
    public void EditorSetup(WebShot web, WebPatch patch, Transform bodyT, Renderer telegraph, Transform spinnerT, Renderer spinnerR)
    {
        webPrefab = web; webPatchPrefab = patch; body = bodyT; leapTelegraph = telegraph; spinner = spinnerT; spinnerRenderer = spinnerR;
    }
#endif
}
