using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// "Juice": números de dano, tremida de tela, hit stop, flash branco e partículas de morte.
/// Escuta os eventos de dano/morte; outros scripts chamam GameFeel.Shake para impactos (pisão, pulo do chefe).
/// </summary>
public class GameFeel : MonoBehaviour
{
    public static GameFeel Instance { get; private set; }

    [SerializeField] CinemachineImpulseSource impulse;
    [SerializeField] DamageNumbers numbers;
    [SerializeField] ParticleSystem deathPuffPrefab;

    [Header("Cores dos números")]
    [SerializeField] Color normalColor = Color.white;
    [SerializeField] Color bigHitColor = new(1f, 0.85f, 0.25f);
    [SerializeField] Color itemColor = new(1f, 0.6f, 0.25f);
    [SerializeField] Color playerHurtColor = new(1f, 0.3f, 0.25f);

    [Header("Intensidade")]
    [Tooltip("Golpe do player com pelo menos esse múltiplo do dano dele conta como forte (amarelo, tremida, hit stop).")]
    [SerializeField] float bigHitMultiple = 3f;
    [SerializeField] float bigHitStop = 0.05f;
    [SerializeField] float bossDeathStop = 0.18f;

    /// <summary>Contadores para testes/depuração.</summary>
    public int Shakes { get; private set; }
    public int Puffs { get; private set; }

    Transform player;
    Health playerHealth;
    CharacterStats playerStats;
    Hud hud;

    void Awake() => Instance = this;

    void OnEnable()
    {
        Health.OnAnyDamaged += HandleDamaged;
        EnemyBase.OnAnyEnemyDied += HandleEnemyDied;
    }

    void OnDisable()
    {
        Health.OnAnyDamaged -= HandleDamaged;
        EnemyBase.OnAnyEnemyDied -= HandleEnemyDied;
        HitStop.Cancel();
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { player = p.transform; playerHealth = p.GetComponent<Health>(); playerStats = p.GetComponent<CharacterStats>(); }
        hud = FindAnyObjectByType<Hud>();
    }

    void Update() => HitStop.Tick();

    /// <summary>Tremida de tela. 0.3 = leve, 1 = forte, 2 = enorme.</summary>
    public static void Shake(float force)
    {
        if (Instance == null || Instance.impulse == null || force <= 0f) return;
        Instance.impulse.GenerateImpulseWithForce(force);
        Instance.Shakes++;
    }

    bool FromPlayer(DamageInfo info) => player != null && info.Source != null && info.Source.transform.IsChildOf(player);

    void HandleDamaged(Health victim, DamageInfo info)
    {
        if (info.Amount <= 0f) return;
        Vector3 at = info.HitPoint != Vector3.zero ? info.HitPoint : victim.transform.position + Vector3.up;

        if (victim == playerHealth)
        {
            if (numbers != null) numbers.Spawn(at + Vector3.up * 0.5f, info.Amount, playerHurtColor, 1.1f);
            float frac = info.Amount / Mathf.Max(1f, victim.Max);
            Shake(0.25f + frac * 3f);
            if (hud != null) hud.FlashDamage(Mathf.Clamp01(0.55f + frac * 2f));
            if (frac >= 0.15f) HitStop.Do(0.06f);
            return;
        }

        if (!FromPlayer(info)) return;
        HitFlash.On(victim).Flash();
        float baseDamage = playerStats != null ? playerStats.Damage : 12f;
        bool big = !info.NoProc && info.Amount >= baseDamage * bigHitMultiple;
        Color c = info.NoProc ? itemColor : big ? bigHitColor : normalColor;
        if (numbers != null) numbers.Spawn(at, info.Amount, c, big ? 1.5f : 1f);
        if (big) { Shake(0.35f); HitStop.Do(bigHitStop); }
    }

    void HandleEnemyDied(EnemyBase enemy, DamageInfo blow)
    {
        if (deathPuffPrefab != null)
        {
            var puff = Instantiate(deathPuffPrefab, enemy.Center, Quaternion.identity);
            var main = puff.main;
            main.startColor = MainColor(enemy);
            if (enemy.IsBoss) { puff.transform.localScale = Vector3.one * 3f; main.startSizeMultiplier *= 2.5f; }
            puff.Play();
            Puffs++;
        }
        if (enemy.IsBoss) { Shake(1.4f); HitStop.Do(bossDeathStop); }
    }

    static Color MainColor(EnemyBase enemy)
    {
        var r = enemy.GetComponentInChildren<Renderer>();
        var m = r != null ? r.sharedMaterial : null;
        return m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : new Color(0.4f, 0.25f, 0.15f);
    }
}
