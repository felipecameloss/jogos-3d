using UnityEngine;

/// <summary>
/// Promove um inimigo normal para variante Elite (Gigante ou Flamejante),
/// aumentando vida/dano/recompensa de migalhas e adicionando aura visual ou rastro de fogo.
/// </summary>
public class EnemyElite : MonoBehaviour
{
    public enum Variant { None, Giant, Blazing }

    public Variant CurrentVariant { get; private set; } = Variant.None;
    public bool IsActive { get; private set; }
    public int CrumbMultiplier => IsActive ? 3 : 1;

    EnemyBase enemy;
    GameObject auraObject;
    MeshRenderer auraRenderer;
    Light auraLight;
    Material auraMaterial;
    Material fireMaterial;
    float fireTimer;
    Vector3 lastTrailPos;
    bool hasCapturedScale;
    Vector3 baseScale;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public static int CrumbMultiplierFor(EnemyBase target) =>
        target != null && target.TryGetComponent(out EnemyElite elite) && elite.IsActive
            ? elite.CrumbMultiplier
            : 1;

    public static EnemyElite Apply(EnemyBase target, Variant variant, float baseHealthMult = 1f, float baseDamageMult = 1f)
    {
        if (target == null || variant == Variant.None) return null;
        var elite = target.GetComponent<EnemyElite>();
        if (elite == null) elite = target.gameObject.AddComponent<EnemyElite>();
        elite.Activate(target, variant, baseHealthMult, baseDamageMult);
        return elite;
    }

    void Activate(EnemyBase target, Variant variant, float baseHealthMult, float baseDamageMult)
    {
        enemy = target;
        if (!hasCapturedScale)
        {
            baseScale = target.transform.localScale;
            hasCapturedScale = true;
        }

        IsActive = true;
        CurrentVariant = variant;
        fireTimer = 0.4f;
        lastTrailPos = target.transform.position;

        float hpBonus = variant == Variant.Giant ? 2.5f : 1.85f;
        float dmgBonus = variant == Variant.Giant ? 1.5f : 1.3f;
        float scaleBonus = variant == Variant.Giant ? 1.45f : 1.15f;
        float speedBonus = variant == Variant.Blazing ? 1.2f : 1.1f;

        target.transform.localScale = baseScale * scaleBonus;
        target.ApplyScaling(baseHealthMult * hpBonus, baseDamageMult * dmgBonus);
        target.SpeedMultiplier *= speedBonus;

        EnsureAura();
        Color auraColor = variant == Variant.Giant
            ? new Color(1f, 0.72f, 0.18f, 0.9f)
            : new Color(1f, 0.3f, 0.08f, 0.9f);
        ApplyMaterialColor(auraMaterial, auraColor, emissionIntensity: 2.2f);
        if (auraLight != null)
        {
            auraLight.color = auraColor;
            auraLight.intensity = variant == Variant.Giant ? 2.4f : 2.8f;
        }
        auraObject.SetActive(true);
    }

    void EnsureAura()
    {
        if (auraObject != null) return;

        auraObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        auraObject.name = "EliteAura";
        Destroy(auraObject.GetComponent<Collider>());
        auraObject.transform.SetParent(transform, false);
        auraObject.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        auraObject.transform.localScale = new Vector3(1.55f, 0.03f, 1.55f);

        auraRenderer = auraObject.GetComponent<MeshRenderer>();
        auraRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        auraRenderer.receiveShadows = false;
        auraMaterial = new Material(auraRenderer.sharedMaterial);
        auraRenderer.sharedMaterial = auraMaterial;

        auraLight = auraObject.AddComponent<Light>();
        auraLight.type = LightType.Point;
        auraLight.range = 4.5f;
    }

    void Update()
    {
        if (!IsActive || enemy == null) return;
        if (auraObject != null)
            auraObject.transform.Rotate(Vector3.up, 70f * Time.deltaTime, Space.Self);

        if (CurrentVariant != Variant.Blazing) return;

        fireTimer -= Time.deltaTime;
        if (fireTimer > 0f) return;

        Vector3 pos = transform.position;
        if ((pos - lastTrailPos).sqrMagnitude < 0.7f * 0.7f) return;

        fireTimer = 0.55f;
        lastTrailPos = pos;
        SpawnFirePatch(pos);
    }

    void SpawnFirePatch(Vector3 pos)
    {
        var patch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        patch.name = "EliteFirePatch";
        patch.transform.position = new Vector3(pos.x, pos.y + 0.04f, pos.z);
        patch.transform.localScale = new Vector3(1.1f, 0.04f, 1.1f);

        var col = patch.GetComponent<Collider>();
        col.isTrigger = true;
        if (col is CapsuleCollider cap) cap.height = 30f;

        var mr = patch.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        if (fireMaterial == null)
        {
            fireMaterial = new Material(mr.sharedMaterial);
            ApplyMaterialColor(fireMaterial, new Color(1f, 0.32f, 0.06f, 0.85f), emissionIntensity: 2.5f);
        }
        mr.sharedMaterial = fireMaterial;

        var zone = patch.AddComponent<DamageZone>();
        zone.OnlyTag = "Player";
        zone.DamagePerSecond = 12f * (enemy != null ? enemy.DamageMultiplier : 1f);
        zone.Owner = enemy != null ? enemy.gameObject : null;

        patch.AddComponent<EliteFirePatch>().Begin(3.4f, 1.25f);
    }

    static void ApplyMaterialColor(Material mat, Color color, float emissionIntensity)
    {
        if (mat == null) return;
        if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, color);
        if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, color);
        if (mat.HasProperty(EmissionColorId))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor(EmissionColorId, color * emissionIntensity);
        }
    }

    void OnDisable() => Clear();

    void OnDestroy()
    {
        if (auraMaterial != null) Destroy(auraMaterial);
        if (fireMaterial != null) Destroy(fireMaterial);
    }

    public void Clear()
    {
        if (!IsActive) return;
        IsActive = false;
        CurrentVariant = Variant.None;
        if (auraObject != null) auraObject.SetActive(false);
        if (hasCapturedScale) transform.localScale = baseScale;
        if (enemy != null) enemy.ApplyScaling(1f, 1f);
    }

    sealed class EliteFirePatch : MonoBehaviour
    {
        float age;
        float lifetime = 3.4f;
        float radius = 1.25f;

        public void Begin(float life, float patchRadius)
        {
            lifetime = Mathf.Max(0.2f, life);
            radius = Mathf.Max(0.3f, patchRadius);
            transform.localScale = new Vector3(0.2f, 0.04f, 0.2f);
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }
            float grow = Mathf.Clamp01(age / 0.15f);
            float shrink = 1f - Mathf.Clamp01((age - (lifetime - 0.6f)) / 0.6f);
            float s = radius * Mathf.Min(grow, shrink);
            transform.localScale = new Vector3(s, 0.04f, s);
        }
    }
}
