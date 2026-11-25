using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nuvem de inseticida (Spray de inseticida): fica alguns segundos onde o jato terminou e
/// machuca tudo que estiver dentro: inimigos (dano do player, sem disparar itens "ao acertar")
/// e o próprio player, se entrar ou ficar nela. Dentro da bolha de sabão o player não respira
/// o veneno. O visual são bolas verde-acinzentadas translúcidas, baixas e rolando devagar;
/// não precisa de prefab.
/// </summary>
public class InsecticideCloud : MonoBehaviour
{
    /// <summary>Nuvens que ainda machucam (as que estão sumindo saem da lista).</summary>
    public static readonly List<InsecticideCloud> Active = new();

    /// <summary>Altura da nuvem acima do chão: pular não tira o player dela.</summary>
    public const float Height = 2.6f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly Color CloudColor = new(0.68f, 0.84f, 0.46f, 1f);

    const float GrowTime = 0.4f;
    const float FadeTime = 0.8f;
    const float TickInterval = 0.5f;
    const int PuffCount = 9;

    float radius, lifetime, enemyDps, playerDps, age, tickTimer;
    bool fading;
    GameObject owner;
    Inventory ownerInventory;
    Health playerHealth;
    PlayerMovement playerMovement;

    struct Puff { public Transform transform; public Renderer renderer; public Vector3 offset; public float size, phase; }
    readonly List<Puff> puffs = new();
    MaterialPropertyBlock block;

    public float Radius => radius;
    public bool IsFading => fading;
    /// <summary>Dano que a nuvem já tentou dar no player (testes).</summary>
    public float DamageDealtToPlayer { get; private set; }

    // O projeto entra no Play Mode sem recarregar o domínio: a lista estática não zera sozinha
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Active.Clear();

    /// <summary>
    /// Solta uma nuvem com a base em <paramref name="ground"/>. Inimigos dentro levam
    /// <paramref name="enemyDps"/> por segundo (como dano de item do <paramref name="owner"/>);
    /// o player dentro leva <paramref name="playerDps"/>. Sem material, só não aparece.
    /// </summary>
    public static InsecticideCloud Spawn(Vector3 ground, float radius, float lifetime, float enemyDps, float playerDps,
        GameObject owner, Material material = null)
    {
        var go = new GameObject("InsecticideCloud");
        go.transform.position = ground;
        var cloud = go.AddComponent<InsecticideCloud>();
        cloud.radius = Mathf.Max(0.5f, radius);
        cloud.lifetime = Mathf.Max(GrowTime + FadeTime, lifetime);
        cloud.enemyDps = Mathf.Max(0f, enemyDps);
        cloud.playerDps = Mathf.Max(0f, playerDps);
        cloud.owner = owner;
        var player = owner != null ? owner : GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            player.TryGetComponent(out cloud.ownerInventory);
            player.TryGetComponent(out cloud.playerHealth);
            player.TryGetComponent(out cloud.playerMovement);
        }
        cloud.BuildVisual(material);
        cloud.Animate();
        return cloud;
    }

    /// <summary>O ponto está dentro da nuvem (cilindro baixo sobre o chão).</summary>
    public bool IsInside(Vector3 point)
    {
        Vector3 d = point - transform.position;
        if (d.y < -0.6f || d.y > Height) return false;
        d.y = 0f;
        return d.sqrMagnitude <= radius * radius;
    }

    /// <summary>Começa a sumir agora (sai de <see cref="Active"/> e para de machucar).</summary>
    public void FadeOut()
    {
        if (fading) return;
        fading = true;
        Active.Remove(this);
        age = Mathf.Max(age, lifetime - FadeTime);
    }

    void OnEnable()
    {
        if (!fading && !Active.Contains(this)) Active.Add(this);
    }

    void OnDisable() => Active.Remove(this);

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime) { Destroy(gameObject); return; }
        if (!fading && age >= lifetime - FadeTime) FadeOut();

        // O primeiro tick vem meio segundo depois: dá tempo de sair se nasceu em cima do player
        if (!fading)
        {
            tickTimer += Time.deltaTime;
            if (tickTimer >= TickInterval)
            {
                tickTimer -= TickInterval;
                Tick();
            }
        }
        Animate();
    }

    void Tick()
    {
        float enemyAmount = enemyDps * TickInterval;
        if (enemyAmount > 0f)
        {
            // de trás para frente: o dano pode matar e tirar o inimigo da lista
            for (int i = EnemyBase.Active.Count - 1; i >= 0; i--)
            {
                if (i >= EnemyBase.Active.Count) continue;
                var e = EnemyBase.Active[i];
                if (e == null || !e.IsAlive || !IsInside(e.transform.position + Vector3.up * 0.3f)) continue;
                Vector3 p = e.Center;
                if (ownerInventory != null) ownerInventory.DealItemDamage(e.Health, enemyAmount, p, ignoreArmor: true);
                else e.Health.TakeDamage(new DamageInfo(enemyAmount, owner, p, Vector3.zero) { NoProc = true, IgnoreArmor = true });
            }
        }

        float playerAmount = playerDps * TickInterval;
        if (playerAmount <= 0f || playerHealth == null || !playerHealth.IsAlive) return;
        if (!IsInside(playerHealth.transform.position + Vector3.up * 0.5f)) return;
        if (playerMovement != null && playerMovement.IsFloating) return; // bolha de sabão: não respira o veneno
        Vector3 at = playerHealth.transform.position + Vector3.up;
        playerHealth.TakeDamage(new DamageInfo(playerAmount, gameObject, at, Vector3.zero) { IgnoreArmor = true, NoProc = true });
        DamageDealtToPlayer += playerAmount;
    }

    // ---------- Visual ----------

    void BuildVisual(Material material)
    {
        if (material == null) return;
        block = new MaterialPropertyBlock();
        for (int i = 0; i < PuffCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Puff";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Vector2 flat = Random.insideUnitCircle * 0.62f;
            puffs.Add(new Puff
            {
                transform = go.transform,
                renderer = r,
                offset = new Vector3(flat.x, 0f, flat.y),
                size = Random.Range(0.7f, 1.05f),
                phase = Random.Range(0f, Mathf.PI * 2f),
            });
        }
    }

    void Animate()
    {
        if (puffs.Count == 0) return;
        float g = Mathf.Clamp01(age / GrowTime);
        float grow = 1f - (1f - g) * (1f - g);
        float fade = fading ? Mathf.Clamp01((lifetime - age) / FadeTime) : 1f;
        float spin = age * 9f;
        foreach (var p in puffs)
        {
            if (p.transform == null) continue;
            float bob = Mathf.Sin(age * 1.6f + p.phase);
            Vector3 flat = Quaternion.Euler(0f, spin, 0f) * p.offset * radius;
            float d = radius * p.size * grow * (1f + 0.06f * bob) * (fading ? 1f + 0.25f * (1f - fade) : 1f);
            p.transform.localPosition = flat + Vector3.up * (d * 0.28f + 0.15f + bob * 0.08f);
            p.transform.localScale = new Vector3(d, d * 0.6f, d);
            var c = CloudColor;
            c.a = 0.26f * fade * (0.8f + 0.2f * Mathf.Sin(age * 2.4f + p.phase));
            p.renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c);
            p.renderer.SetPropertyBlock(block);
        }
    }
}
