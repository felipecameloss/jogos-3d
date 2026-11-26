using UnityEngine;

/// <summary>
/// Vaga-lume amigo que voa junto do player iluminando o caminho (formigueiro, issue #61):
/// fica sobre o ombro, balança, bate as asas e leva uma luz pontual forte. Não é inimigo nem
/// leva dano; é criado pelo <see cref="StageDirector"/> quando a fase pede lanterna.
/// </summary>
public class PlayerLantern : MonoBehaviour
{
    [SerializeField] Vector3 shoulderOffset = new(-1.1f, 2.6f, -0.4f);
    [SerializeField, Min(0.01f)] float followTime = 0.25f;
    [SerializeField] Color lightColor = new(0.9f, 1f, 0.55f);
    [SerializeField, Min(1f)] float lightRange = 24f;
    [SerializeField, Min(0f)] float lightIntensity = 7f;
    [SerializeField, Min(0.1f)] float modelScale = 0.9f;

    /// <summary>A lanterna desta fase (null sem lanterna).</summary>
    public static PlayerLantern Current { get; private set; }

    public Light Light { get; private set; }
    public Transform Player { get; private set; }

    Transform wingL, wingR;
    Quaternion wingLRest, wingRRest;
    Vector3 velocity;
    float phase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Current = null;

    /// <summary>Cria o vaga-lume lanterna seguindo o player (modelo = Firefly.fbx; sem modelo, só a luz).</summary>
    public static PlayerLantern Attach(GameObject player, GameObject model)
    {
        if (player == null) return null;
        var go = new GameObject("PlayerLantern");
        var lantern = go.AddComponent<PlayerLantern>();
        lantern.Setup(player.transform, model);
        return lantern;
    }

    void Setup(Transform player, GameObject model)
    {
        Player = player;
        Current = this;
        phase = Random.value * 10f;
        transform.position = Target();

        if (model != null)
        {
            var m = Instantiate(model, transform);
            m.name = "Model";
            m.transform.localPosition = Vector3.zero;
            m.transform.localScale = Vector3.one * modelScale;
            foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var t in m.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Wing_L") { wingL = t; wingLRest = t.localRotation; }
                else if (t.name == "Wing_R") { wingR = t; wingRRest = t.localRotation; }
                else if (t.name == "Lantern" && t.TryGetComponent(out Renderer r))
                {
                    // lanterna acesa: cópia do material com emissão
                    var mat = new Material(r.sharedMaterial);
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", lightColor * 4f);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    r.material = mat;
                }
            }
        }

        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(transform, false);
        Light = lightGo.AddComponent<Light>();
        Light.type = LightType.Point;
        Light.color = lightColor;
        Light.range = lightRange;
        Light.intensity = lightIntensity;
        Light.shadows = LightShadows.Soft;
    }

    Vector3 Target()
    {
        if (Player == null) return transform.position;
        float bob = Mathf.Sin((Time.time + phase) * 2.2f) * 0.25f;
        return Player.TransformPoint(shoulderOffset) + Vector3.up * bob;
    }

    void LateUpdate()
    {
        if (Player == null) { Destroy(gameObject); return; }
        Vector3 before = transform.position;
        transform.position = Vector3.SmoothDamp(transform.position, Target(), ref velocity, followTime);
        Vector3 move = transform.position - before; move.y = 0f;
        Vector3 look = move.sqrMagnitude > 0.0004f ? move : Player.forward;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 6f);

        float flap = Mathf.Sin((Time.time + phase) * 12f * Mathf.PI * 2f) * 38f;
        if (wingL != null) wingL.localRotation = wingLRest * Quaternion.Euler(0f, 0f, -flap);
        if (wingR != null) wingR.localRotation = wingRRest * Quaternion.Euler(0f, 0f, flap);
        if (Light != null) Light.intensity = lightIntensity * (0.92f + 0.08f * Mathf.Sin((Time.time + phase) * 3f));
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}
