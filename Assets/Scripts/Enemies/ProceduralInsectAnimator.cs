using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Anima por código qualquer inseto que siga a convenção de nomes do CLAUDE.md
/// (modelos de Tools/Blender: peças separadas com pivô na articulação, sem rig):
///   Body (tórax, pai de tudo) > Leg_L1, Leg_R1, Leg_L2... / Antenna_L, Antenna_R / Abdomen
/// - Pernas: marcha em tripé (L1, R2, L3 juntas; depois R1, L2, R3; com 4 pares segue
///   a mesma alternância), balançam para frente/trás e levantam na volta. A passada
///   acompanha a velocidade do NavMeshAgent (ou o deslocamento, sem agente).
/// - Corpo: bob leve a cada passo (e respiração parado); antenas balançam; abdômen respira.
/// - Dano: squash elástico no modelo + flash branco (usa o HitFlash do jogo, o mesmo do
///   GameFeel, para os dois não disputarem a cor dos materiais).
/// Só mexe nas peças do modelo: o Body externo do prefab e a raiz continuam livres para
/// os scripts de ataque (inclinar, avançar, encolher ao morrer, escala de elite).
/// </summary>
[DisallowMultipleComponent]
public class ProceduralInsectAnimator : MonoBehaviour
{
    [Tooltip("Raiz do modelo (pai do Body com as pernas). Vazio = procura sozinho.")]
    [SerializeField] Transform model;

    [Header("Pernas")]
    [Tooltip("Metros andados por ciclo completo de passada.")]
    [SerializeField, Min(0.05f)] float strideLength = 0.6f;
    [Tooltip("Velocidade (m/s) em que a passada chega na amplitude máxima.")]
    [SerializeField, Min(0.1f)] float fullSpeed = 3f;
    [SerializeField, Range(0f, 60f)] float swingAngle = 24f;
    [SerializeField, Range(0f, 45f)] float liftAngle = 16f;

    [Header("Corpo")]
    [Tooltip("Altura do bob a cada passo, em fração do tamanho do modelo.")]
    [SerializeField, Range(0f, 0.1f)] float bobHeight = 0.025f;
    [SerializeField, Range(0f, 30f)] float antennaWiggle = 9f;
    [SerializeField, Range(0f, 10f)] float breathAngle = 2.5f;

    [Header("Dano")]
    [Tooltip("Achata/estica o modelo ao levar dano (0 = desliga).")]
    [SerializeField, Range(0f, 0.5f)] float squashAmount = 0.22f;
    [SerializeField, Min(0.05f)] float squashDuration = 0.3f;
    [SerializeField] bool flashOnHit = true;

    struct Part
    {
        public Transform t;
        public Quaternion rest;
        public float phase;
        public float side; // -1 esquerda, +1 direita
    }

    readonly List<Part> legs = new();
    readonly List<Part> antennae = new();
    Part abdomen;
    bool hasAbdomen;
    Transform body;
    Vector3 bodyRestPos, modelRestScale;
    float modelSize = 1f;

    NavMeshAgent agent;
    Health health;
    Vector3 lastPos;
    float cycle, moveAmount, idleTime, squashStart = float.NegativeInfinity, squashPower;

    /// <summary>0..1: quanto está andando (testes).</summary>
    public float MoveAmount => moveAmount;
    public int LegCount => legs.Count;
    /// <summary>Quanto o squash está deformando agora (0 = nada; testes).</summary>
    public float CurrentSquash { get; private set; }
    public Transform Model => model;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponentInParent<Health>();
        if (model == null) model = FindModelRoot(transform);
        if (model == null) { enabled = false; return; }
        modelRestScale = model.localScale;
        Collect(model);
        if (body != null) bodyRestPos = body.localPosition;
        modelSize = MeasureSize(model);
        idleTime = Random.value * 10f; // insetos não balançam em sincronia
    }

    void OnEnable()
    {
        lastPos = transform.position;
        if (health != null) health.OnDamaged += HandleDamaged;
        squashStart = float.NegativeInfinity;
        if (model != null) model.localScale = modelRestScale;
    }

    void OnDisable()
    {
        if (health != null) health.OnDamaged -= HandleDamaged;
    }

    /// <summary>Raiz do modelo = pai do "Body" que tem pernas (Leg_*) como filhos diretos.</summary>
    static Transform FindModelRoot(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "Body") continue;
            foreach (Transform c in t)
                if (c.name.StartsWith("Leg_")) return t.parent != null ? t.parent : t;
        }
        return null;
    }

    void Collect(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n.StartsWith("Leg_") && n.Length >= 6 && char.IsDigit(n[5]))
            {
                float side = n[4] == 'L' ? -1f : 1f;
                int index = n[5] - '0';
                // tripé: pernas ímpares de um lado andam com as pares do outro
                int group = (index + (side < 0f ? 0 : 1)) % 2;
                legs.Add(new Part { t = t, rest = t.localRotation, phase = group * Mathf.PI, side = side });
            }
            else if (n.StartsWith("Antenna_"))
            {
                float side = n.EndsWith("L") ? -1f : 1f;
                antennae.Add(new Part { t = t, rest = t.localRotation, phase = side * 1.3f, side = side });
            }
            else if (n == "Abdomen" && !hasAbdomen)
            {
                abdomen = new Part { t = t, rest = t.localRotation };
                hasAbdomen = true;
            }
            else if (n == "Body" && body == null && t != root)
            {
                body = t;
            }
        }
    }

    static float MeasureSize(Transform model)
    {
        var b = new Bounds(model.position, Vector3.zero);
        bool any = false;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return any ? Mathf.Max(b.size.x, b.size.y, b.size.z) : 1f;
    }

    void HandleDamaged(DamageInfo info)
    {
        if (info.Amount <= 0f) return;
        // golpe maior = squash maior (até 2x), relativo à vida máxima
        float frac = health != null ? info.Amount / Mathf.Max(1f, health.Max) : 0.1f;
        squashPower = Mathf.Clamp(0.6f + frac * 4f, 0.6f, 2f);
        squashStart = Time.time;
        if (flashOnHit) HitFlash.On(health != null ? (Component)health : this).Flash();
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || model == null) return;
        bool alive = health == null || health.IsAlive;

        Vector3 velocity = agent != null && agent.enabled ? agent.velocity : (transform.position - lastPos) / dt;
        lastPos = transform.position;
        velocity.y = 0f;
        float speed = alive ? velocity.magnitude : 0f;
        moveAmount = Mathf.MoveTowards(moveAmount, Mathf.Clamp01(speed / fullSpeed), dt * 4f);
        // passada acompanha o chão (sem patinar); parado, termina o passo devagar
        cycle += (speed / strideLength + (1f - moveAmount) * 0.2f) * Mathf.PI * 2f * dt;
        idleTime += dt;

        AnimateLegs();
        AnimateBody();
        AnimateSquash();
    }

    void AnimateLegs()
    {
        foreach (var leg in legs)
        {
            float a = cycle + leg.phase;
            float swing = Mathf.Sin(a) * swingAngle * moveAmount;
            float lift = Mathf.Max(0f, Mathf.Cos(a)) * liftAngle * moveAmount;
            // yaw: frente para os dois lados; roll: levanta a perna (sinal depende do lado)
            leg.t.localRotation = leg.rest * Quaternion.Euler(0f, -leg.side * swing, leg.side * lift);
        }
    }

    void AnimateBody()
    {
        if (body != null)
        {
            // um "pulinho" por passo (2 por ciclo: cada tripé apoia uma vez) + respiração parado
            float step = Mathf.Abs(Mathf.Sin(cycle)) * moveAmount;
            float breath = Mathf.Sin(idleTime * 1.6f) * 0.25f * (1f - moveAmount);
            float h = bobHeight * modelSize / Mathf.Max(0.0001f, model.lossyScale.y);
            body.localPosition = bodyRestPos + Vector3.up * (h * (step + breath));
        }

        float wiggleSpeed = 2.2f + moveAmount * 6f;
        foreach (var ant in antennae)
        {
            float w = idleTime * wiggleSpeed + ant.phase;
            ant.t.localRotation = ant.rest * Quaternion.Euler(Mathf.Sin(w) * antennaWiggle,
                ant.side * Mathf.Sin(w * 0.7f) * antennaWiggle * 0.6f, 0f);
        }

        if (hasAbdomen)
            abdomen.t.localRotation = abdomen.rest * Quaternion.Euler(Mathf.Sin(idleTime * 1.6f) * breathAngle, 0f, 0f);
    }

    void AnimateSquash()
    {
        float t = Time.time - squashStart;
        if (squashAmount <= 0f || t > squashDuration)
        {
            if (CurrentSquash != 0f) { CurrentSquash = 0f; model.localScale = modelRestScale; }
            return;
        }
        // mola amortecida: achata, estica de volta, assenta
        float k = t / squashDuration;
        float s = squashAmount * squashPower * Mathf.Cos(k * Mathf.PI * 2.5f) * (1f - k) * (1f - k);
        CurrentSquash = s;
        // conserva volume: achata em Y, alarga em X/Z
        model.localScale = Vector3.Scale(modelRestScale, new Vector3(1f + s * 0.5f, 1f - s, 1f + s * 0.5f));
    }

    /// <summary>Adianta/atrasa a passada (radianos): segmentos da centopeia andam em onda.</summary>
    public void OffsetGait(float radians) => cycle += radians;

    /// <summary>Ajusta a passada em runtime (modelo do player, criado em código).</summary>
    public void ConfigureGait(float stride, float speedForFull)
    {
        strideLength = Mathf.Max(0.05f, stride);
        fullSpeed = Mathf.Max(0.1f, speedForFull);
    }

#if UNITY_EDITOR
    public void EditorSetup(Transform modelRoot, float stride, float speedForFull)
    {
        model = modelRoot; strideLength = stride; fullSpeed = speedForFull;
    }
#endif
}
