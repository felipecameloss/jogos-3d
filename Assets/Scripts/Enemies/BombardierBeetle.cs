using UnityEngine;

/// <summary>
/// Besouro-bombardeiro: fica a distância e atira ácido em arco.
/// Aviso: vira de costas para o player e a ponta do abdômen brilha antes de disparar.
/// O ácido deixa uma poça que tira vida por segundo (só do player).
/// </summary>
public class BombardierBeetle : EnemyBase
{
    [Header("Distância")]
    [Tooltip("Faixa em que ele gosta de ficar do player.")]
    [SerializeField] Vector2 preferredRange = new(8f, 16f);

    [Header("Ácido")]
    [SerializeField] AcidBlob blobPrefab;
    [SerializeField] AcidPool poolPrefab;
    [SerializeField, Min(0f)] float impactDamage = 10f;
    [SerializeField, Min(0f)] float poolDamagePerSecond = 8f;
    [SerializeField, Min(0.1f)] float windUpTime = 0.7f;
    [SerializeField, Min(0.1f)] float shotCooldown = 2.8f;
    [Tooltip("Antecipa o movimento do player por esse tempo.")]
    [SerializeField, Min(0f)] float leadTime = 0.3f;
    [Tooltip("Ponta do abdômen: de onde sai o ácido e o que brilha no aviso.")]
    [SerializeField] Transform nozzle;
    [SerializeField] Renderer nozzleRenderer;

    public bool IsWindingUp { get; private set; }
    public int Shots { get; private set; }

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    float nextShot, windStart;
    MaterialPropertyBlock block;

    protected override void Awake()
    {
        base.Awake();
        block = new MaterialPropertyBlock();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        IsWindingUp = false;
        nextShot = Time.time + Random.Range(1f, 2f);
        Glow(0f);
    }

    protected override void Tick()
    {
        float d = FlatDistanceToTarget();
        if (IsWindingUp)
        {
            FaceAway();
            float k = (Time.time - windStart) / windUpTime;
            Glow(Mathf.Clamp01(k));
            if (k >= 1f) Fire();
            return;
        }

        if (d > preferredRange.y) MoveTo(Target.position);
        else if (d < preferredRange.x) Retreat();
        else
        {
            StopMoving();
            FaceTarget();
            if (Time.time >= nextShot) { IsWindingUp = true; windStart = Time.time; StopMoving(); }
        }
    }

    protected override void Update()
    {
        base.Update();
        if (!HasValidTarget && IsWindingUp) { IsWindingUp = false; Glow(0f); }
    }

    void Retreat()
    {
        Vector3 away = transform.position - Target.position; away.y = 0f;
        Vector3 dest = transform.position + away.normalized * 6f;
        if (UnityEngine.AI.NavMesh.SamplePosition(dest, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) MoveTo(hit.position);
        else MoveTo(transform.position - transform.right * 4f); // encurralado: anda de lado
    }

    // De costas para o player (o ácido sai do abdômen, por cima)
    void FaceAway()
    {
        Vector3 d = transform.position - Target.position; d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), turnSpeed * Time.deltaTime);
    }

    void Fire()
    {
        IsWindingUp = false;
        Glow(0f);
        nextShot = Time.time + shotCooldown;
        Shots++;
        if (blobPrefab == null) return;
        Vector3 from = nozzle != null ? nozzle.position : transform.position + Vector3.up;
        Vector3 target = Target.position;
        if (Target.TryGetComponent(out PlayerMovement pm)) { var v = pm.Velocity; v.y = 0f; target += v * leadTime; }
        float dist = Vector3.Distance(from, target);
        float flight = Mathf.Clamp(dist / 14f, 0.6f, 1.4f);
        var blob = Instantiate(blobPrefab, from, Quaternion.identity);
        blob.Launch(from, target, flight, gameObject, poolPrefab, impactDamage * DamageMultiplier, poolDamagePerSecond * DamageMultiplier);
    }

    void Glow(float k)
    {
        if (nozzleRenderer == null) return;
        nozzleRenderer.GetPropertyBlock(block);
        block.SetColor(EmissionId, new Color(0.5f, 1f, 0.2f) * (3f * k));
        nozzleRenderer.SetPropertyBlock(block);
    }

    protected override void HandleDeath(DamageInfo killingBlow)
    {
        IsWindingUp = false;
        Glow(0f);
        base.HandleDeath(killingBlow);
    }
}
