using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rastro de gosma das lesmas: manchas brilhantes no chão que somem com o tempo. O player em
/// cima de uma (pisando no chão) escorrega: <see cref="PlayerMovement.Grip"/> cai e ele demora
/// para mudar de direção e para frear. Flutuando na bolha de sabão ou no ar, não escorrega.
/// Um objeto só cuida de todas as manchas (criado sozinho na primeira gosma).
/// </summary>
public class SlimeTrail : MonoBehaviour
{
    /// <summary>Aderência do player em cima da gosma (1 = chão normal).</summary>
    public const float SlipGrip = 0.07f;
    const int MaxBlobs = 200;
    const float FadeTime = 1.5f;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly Color SlimeColor = new(0.74f, 0.84f, 0.98f, 0.45f);
    static readonly Color SheenColor = new(0.95f, 0.98f, 1f, 0.35f);

    struct Blob
    {
        public Transform transform;
        public Renderer renderer;
        public Vector3 position;
        public float radius, born, lifetime;
        public Color color;
    }

    static SlimeTrail instance;
    readonly List<Blob> blobs = new();
    MaterialPropertyBlock block;
    PlayerMovement player;

    /// <summary>Manchas de gosma no chão agora.</summary>
    public static int Count => instance != null ? instance.blobs.Count : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    /// <summary>Solta uma mancha de gosma no chão embaixo de <paramref name="at"/>.</summary>
    public static void Drop(Vector3 at, float radius, float lifetime, Material material)
    {
        if (instance == null) instance = new GameObject("SlimeTrail").AddComponent<SlimeTrail>();
        instance.Add(at, radius, lifetime, material);
    }

    /// <summary>O ponto (pés) está em cima de alguma mancha de gosma.</summary>
    public static bool IsOnSlime(Vector3 feet)
    {
        if (instance == null) return false;
        foreach (var b in instance.blobs)
        {
            Vector3 d = feet - b.position;
            if (Mathf.Abs(d.y) > 0.8f) continue;
            d.y = 0f;
            if (d.sqrMagnitude <= b.radius * b.radius && Time.time < b.born + b.lifetime - FadeTime * 0.5f) return true;
        }
        return false;
    }

    void Awake() => block = new MaterialPropertyBlock();

    void Add(Vector3 at, float radius, float lifetime, Material material)
    {
        // chão e inclinação embaixo do ponto (ignora inimigos, player e gatilhos)
        Vector3 ground = at, normal = Vector3.up;
        float best = float.MaxValue;
        foreach (var hit in Physics.RaycastAll(at + Vector3.up * 1.5f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.distance >= best || hit.collider.GetComponentInParent<EnemyBase>() != null) continue;
            if (hit.collider.GetComponentInParent<CharacterController>() != null) continue;
            best = hit.distance;
            ground = hit.point;
            normal = hit.normal;
        }

        if (blobs.Count >= MaxBlobs) Remove(0);
        var blob = new Blob
        {
            position = ground,
            radius = radius * Random.Range(0.85f, 1.1f),
            born = Time.time,
            lifetime = lifetime,
            color = Random.value < 0.25f ? SheenColor : SlimeColor,
        };
        if (material != null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Slime";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(ground + normal * 0.03f,
                Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            go.transform.localScale = new Vector3(blob.radius * 2f, 0.004f, blob.radius * 2f * Random.Range(0.75f, 1f));
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            blob.transform = go.transform;
            blob.renderer = r;
        }
        blobs.Add(blob);
        Paint(blob, 0f);
    }

    void Update()
    {
        for (int i = blobs.Count - 1; i >= 0; i--)
        {
            var b = blobs[i];
            float age = Time.time - b.born;
            if (age >= b.lifetime) { Remove(i); continue; }
            Paint(b, age);
        }
        UpdatePlayerGrip();
    }

    void UpdatePlayerGrip()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null || !p.TryGetComponent(out player)) return;
        }
        bool slipping = player.IsGrounded && !player.IsFloating && IsOnSlime(player.transform.position);
        player.Grip = slipping ? SlipGrip : 1f;
    }

    void Paint(Blob b, float age)
    {
        if (b.renderer == null) return;
        float grow = Mathf.Clamp01(age / 0.25f);
        float fade = Mathf.Clamp01((b.lifetime - age) / FadeTime);
        var c = b.color;
        c.a *= fade * (0.85f + 0.15f * Mathf.Sin(Time.time * 2f + b.born)); // brilho molhado
        b.renderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, c);
        b.renderer.SetPropertyBlock(block);
        float s = Mathf.Lerp(0.4f, 1f, grow);
        var scale = b.transform.localScale;
        float wanted = b.radius * 2f * s;
        if (Mathf.Abs(scale.x - wanted) > 0.001f)
            b.transform.localScale = new Vector3(wanted, scale.y, wanted * (scale.z / Mathf.Max(0.0001f, scale.x)));
    }

    void Remove(int i)
    {
        if (blobs[i].transform != null) Destroy(blobs[i].transform.gameObject);
        blobs.RemoveAt(i);
    }

    void OnDestroy()
    {
        if (player != null) player.Grip = 1f;
        if (instance == this) instance = null;
    }
}
