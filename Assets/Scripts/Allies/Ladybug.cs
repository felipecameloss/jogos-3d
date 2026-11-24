using UnityEngine;

/// <summary>
/// Uma joaninha aliada: voa em órbita em volta do player e atira no inimigo vivo mais perto.
/// Quem cria, posiciona na órbita e dispara os tiros é o LadybugSwarm do player.
/// </summary>
public class Ladybug : MonoBehaviour
{
    const float RetargetInterval = 0.25f;

    LadybugSwarm swarm;
    Transform body;
    Vector3 velocity;
    float nextShot, nextRetarget, phase;

    /// <summary>Inimigo na mira agora (null se nenhum perto).</summary>
    public EnemyBase Target { get; private set; }
    public int Shots { get; private set; }

    internal void Init(LadybugSwarm owner, Vector3 startPosition)
    {
        swarm = owner;
        transform.position = startPosition;
        phase = Random.value * 10f;
        nextShot = Time.time + Random.Range(0.3f, swarm.Config.fireInterval); // não atiram todas juntas
        body = LadybugMeshes.CreateLadybug(transform, swarm.Config.bodyMaterial, 1f).transform;
    }

    // Depois do player andar: a órbita acompanha sem tremer
    void LateUpdate()
    {
        if (swarm == null) { Destroy(gameObject); return; }
        var cfg = swarm.Config;
        float dt = Time.deltaTime;

        transform.position = Vector3.SmoothDamp(transform.position, swarm.SlotPosition(this), ref velocity, 0.2f);

        if (Target == null || !Target.IsAlive || Time.time >= nextRetarget)
        {
            Target = EnemyBase.Nearest(transform.position, cfg.range);
            nextRetarget = Time.time + RetargetInterval;
        }

        // Olha para o alvo; sem alvo, para onde está voando
        Vector3 look = Target != null ? Target.Center - transform.position : velocity;
        look.y *= 0.4f;
        if (look.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 1f - Mathf.Exp(-10f * dt));

        // Balança no ar (as asas estão "batendo")
        if (body != null)
        {
            float t = Time.time + phase;
            body.localPosition = Vector3.up * (Mathf.Sin(t * 7f) * 0.04f);
            body.localRotation = Quaternion.Euler(Mathf.Sin(t * 3f) * 6f, 0f, Mathf.Sin(t * 5f) * 8f);
        }

        if (Target != null && Time.time >= nextShot && swarm.CanShoot)
        {
            swarm.Fire(this, Target);
            Shots++;
            nextShot = Time.time + cfg.fireInterval;
        }
    }
}
