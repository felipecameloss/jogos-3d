using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Anima por código os modelos de inseto feitos no Blender (Tools/Blender), que vêm
/// em peças separadas com pivô na articulação e sem rig:
/// - Pernas (Leg_L1, Leg_R2...): marcha em tripé (L1, R2, L3 juntas; depois R1, L2, R3),
///   balançam para frente/trás e levantam na volta; velocidade da passada segue a do agente.
/// - Antenas (Antenna_*): balançam sozinhas, mais rápido andando.
/// - Abdômen: "respira" devagar.
/// Só mexe nas peças do modelo; o Body externo continua livre para os scripts de ataque.
/// </summary>
[DisallowMultipleComponent]
public class InsectAnimator : MonoBehaviour
{
    [Tooltip("Raiz do modelo (vazio = este objeto).")]
    [SerializeField] Transform model;
    [Tooltip("Metros andados por ciclo completo de passada.")]
    [SerializeField, Min(0.05f)] float strideLength = 0.6f;
    [Tooltip("Velocidade (m/s) em que a passada chega na amplitude máxima.")]
    [SerializeField, Min(0.1f)] float fullSpeed = 3f;
    [SerializeField, Range(0f, 60f)] float swingAngle = 24f;
    [SerializeField, Range(0f, 45f)] float liftAngle = 16f;
    [SerializeField, Range(0f, 30f)] float antennaWiggle = 9f;
    [SerializeField, Range(0f, 10f)] float breathAngle = 2.5f;

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

    NavMeshAgent agent;
    Health health;
    Vector3 lastPos;
    float cycle, moveAmount, idleTime;

    /// <summary>0..1: quanto está andando (testes).</summary>
    public float MoveAmount => moveAmount;
    public int LegCount => legs.Count;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        Collect(model != null ? model : transform);
        idleTime = Random.value * 10f; // insetos não balançam em sincronia
    }

    void Collect(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n.StartsWith("Leg_") && n.Length >= 6)
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
            else if (n == "Abdomen" && !hasAbdomen && t != root)
            {
                abdomen = new Part { t = t, rest = t.localRotation };
                hasAbdomen = true;
            }
        }
    }

    void OnEnable() => lastPos = transform.position;

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        bool alive = health == null || health.IsAlive;

        Vector3 velocity = agent != null && agent.enabled ? agent.velocity : (transform.position - lastPos) / dt;
        lastPos = transform.position;
        velocity.y = 0f;
        float speed = alive ? velocity.magnitude : 0f;
        moveAmount = Mathf.MoveTowards(moveAmount, Mathf.Clamp01(speed / fullSpeed), dt * 4f);
        // passada acompanha o chão (sem patinar); parado, termina o passo devagar
        cycle += (speed / strideLength + (1f - moveAmount) * 0.2f) * Mathf.PI * 2f * dt;
        idleTime += dt;

        foreach (var leg in legs)
        {
            float a = cycle + leg.phase;
            float swing = Mathf.Sin(a) * swingAngle * moveAmount;
            float lift = Mathf.Max(0f, Mathf.Cos(a)) * liftAngle * moveAmount;
            // yaw: frente para os dois lados; roll: levanta a perna (sinal depende do lado)
            leg.t.localRotation = leg.rest * Quaternion.Euler(0f, -leg.side * swing, leg.side * lift);
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

#if UNITY_EDITOR
    public void EditorSetup(Transform modelRoot, float stride, float speedForFull)
    {
        model = modelRoot; strideLength = stride; fullSpeed = speedForFull;
    }
#endif
}
