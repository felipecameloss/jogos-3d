using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Joaninhas compradas do player: orbitam em volta dele e atiram no inimigo mais perto.
/// Fica no player (criado na primeira compra, com <see cref="For"/>) e passa de fase pelo
/// RunManager. Os tiros contam como do player (kills, migalhas) mas não disparam itens "ao acertar".
/// </summary>
public class LadybugSwarm : MonoBehaviour
{
    public LadybugConfig Config { get; private set; }
    public int Count => ladybugs.Count;
    public IReadOnlyList<Ladybug> Ladybugs => ladybugs;
    public bool IsFull => Config != null && Count >= Config.maxLadybugs;
    /// <summary>Tiros dados por todas as joaninhas (testes).</summary>
    public int TotalShots { get; private set; }

    /// <summary>Quantidade mudou (compra ou fase nova).</summary>
    public event Action<int> OnCountChanged;

    readonly List<Ladybug> ladybugs = new();
    Health health;
    CharacterStats stats;
    float orbitAngle;
    ComponentPool<Projectile> shots;
    Material shotMaterial;

    /// <summary>Pega (ou cria) o enxame no player.</summary>
    public static LadybugSwarm For(GameObject player) =>
        player.TryGetComponent(out LadybugSwarm s) ? s : player.AddComponent<LadybugSwarm>();

    void Awake()
    {
        health = GetComponent<Health>();
        stats = GetComponent<CharacterStats>();
    }

    /// <summary>Uma joaninha nova voando de <paramref name="from"/> até a órbita. Null se já está cheio.</summary>
    public Ladybug Add(LadybugConfig config, Vector3 from)
    {
        if (config == null) return null;
        if (Config == null) Config = config;
        if (IsFull) return null;
        var ladybug = new GameObject($"Ladybug {Count + 1}").AddComponent<Ladybug>();
        ladybugs.Add(ladybug);
        ladybug.Init(this, from);
        OnCountChanged?.Invoke(Count);
        return ladybug;
    }

    /// <summary>Volta com as joaninhas da fase anterior (já na órbita).</summary>
    public void Restore(LadybugConfig config, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var ladybug = Add(config, transform.position + Vector3.up * (config != null ? config.orbitHeight : 1.8f));
            if (ladybug == null) break;
        }
    }

    /// <summary>Pode atirar: player vivo e run rolando.</summary>
    public bool CanShoot =>
        (health == null || health.IsAlive) &&
        (RunManager.Instance == null || (!RunManager.Instance.IsRunOver && !RunManager.Instance.IsStageComplete));

    void Update()
    {
        ladybugs.RemoveAll(l => l == null);
        if (Config != null) orbitAngle = (orbitAngle + Config.orbitSpeed * Time.deltaTime) % 360f;
    }

    void OnDestroy()
    {
        foreach (var l in ladybugs) if (l != null) Destroy(l.gameObject);
        if (shotMaterial != null) Destroy(shotMaterial);
    }

    /// <summary>Onde a joaninha deve estar agora: espaçadas por igual num círculo em volta do player.</summary>
    public Vector3 SlotPosition(Ladybug ladybug)
    {
        int i = Mathf.Max(0, ladybugs.IndexOf(ladybug));
        int n = Mathf.Max(1, ladybugs.Count);
        float radius = Config.orbitRadius + 0.12f * Mathf.Max(0, n - 3);
        float a = (orbitAngle + 360f * i / n) * Mathf.Deg2Rad;
        float bob = Mathf.Sin(Time.time * 2f + i * 1.7f) * 0.12f;
        return transform.position + new Vector3(Mathf.Cos(a) * radius, Config.orbitHeight + bob, Mathf.Sin(a) * radius);
    }

    /// <summary>Dispara da joaninha no inimigo, mirando um pouco à frente se ele estiver andando.</summary>
    internal void Fire(Ladybug from, EnemyBase target)
    {
        var pool = ShotPool();
        if (pool == null) return;
        Vector3 origin = from.transform.position + from.transform.forward * 0.3f;
        Vector3 aim = target.Center;
        if (target.TryGetComponent(out NavMeshAgent agent) && agent.enabled)
            aim += agent.velocity * (Vector3.Distance(origin, aim) / Config.projectileSpeed);
        Vector3 dir = aim - origin;
        if (dir.sqrMagnitude < 0.0001f) return;
        float damage = (stats != null ? stats.Damage : 12f) * Config.damageCoefficient;
        pool.Get().Launch(origin, dir.normalized * Config.projectileSpeed, damage, gameObject, pool.Release, null, noProc: true);
        TotalShots++;
    }

    /// <summary>Pool dos tiros, montado na primeira vez: bolinha laranja com rastro.</summary>
    ComponentPool<Projectile> ShotPool()
    {
        if (shots != null) return shots;
        if (Config == null) return null;
        var template = new GameObject("[Molde] LadybugShot");
        template.SetActive(false); // só serve de molde para o pool (fica inativo na cena)
        var projectile = template.AddComponent<Projectile>();

        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(ball.GetComponent<Collider>()); // já sai do molde sem colisor (as cópias são feitas agora)
        ball.transform.SetParent(template.transform, false);
        ball.transform.localScale = Vector3.one * 0.16f;
        var r = ball.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (Config.bodyMaterial != null)
        {
            shotMaterial = new Material(Config.bodyMaterial) { name = "LadybugShot" };
            shotMaterial.SetColor("_BaseColor", Config.shotColor);
            r.sharedMaterial = shotMaterial;
        }

        if (Config.trailMaterial != null)
        {
            var trail = template.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Config.trailMaterial;
            trail.time = 0.12f;
            trail.widthMultiplier = 0.1f;
            trail.startColor = Config.shotColor;
            trail.endColor = new Color(Config.shotColor.r, Config.shotColor.g, Config.shotColor.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        shots = new ComponentPool<Projectile>(projectile, 6, rootName: "[Pool] Ladybug shots");
        return shots;
    }
}
