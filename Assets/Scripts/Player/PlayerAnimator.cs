using UnityEngine;

/// <summary>
/// Escolhe a animação do modelo do player (Animation legacy importada do glTF):
/// parado, andar, correr (velocidade da animação acompanha a velocidade real),
/// rolamento no dash, soco na agulha, corrida lenta no ar e morte.
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] Animation anim;
    [Tooltip("Acima dessa velocidade (m/s) usa a corrida em vez da caminhada.")]
    [SerializeField] float runThreshold = 9.5f;
    [Tooltip("Velocidade (m/s) em que a caminhada/corrida tocam na velocidade normal.")]
    [SerializeField] float walkReferenceSpeed = 5.5f, runReferenceSpeed = 11.5f;
    [SerializeField] float fade = 0.15f;

    public string CurrentClip { get; private set; } = "";

    PlayerMovement movement;
    Health health;
    NeedleThrust needle;
    string idle, walk, run, roll, death, punch;
    int needleUses;
    float punchUntil;
    bool dead;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<Health>();
        needle = GetComponent<NeedleThrust>();
        if (anim == null) anim = GetComponentInChildren<Animation>();
        if (anim == null) { enabled = false; return; }

        idle = Find("Idle"); walk = Find("Walk"); run = Find("Run");
        roll = Find("Roll"); death = Find("Death"); punch = Find("Punch");
        foreach (var c in new[] { idle, walk, run }) if (c != null) anim[c].wrapMode = WrapMode.Loop;
        foreach (var c in new[] { roll, punch }) if (c != null) anim[c].wrapMode = WrapMode.Once;
        if (death != null) anim[death].wrapMode = WrapMode.ClampForever;
        if (health != null) health.OnDeath += HandleDeath;
    }

    void OnDestroy()
    {
        if (health != null) health.OnDeath -= HandleDeath;
    }

    void HandleDeath(DamageInfo _) => Die();

    /// <summary>Nome do clipe "X" ou "Armature|X" (o glTF pode trazer os dois).</summary>
    string Find(string name)
    {
        string fallback = null;
        foreach (AnimationState s in anim)
        {
            if (s.name == name) return s.name;
            if (fallback == null && s.name.EndsWith("|" + name)) fallback = s.name;
        }
        return fallback;
    }

    void Play(string clip, float speed = 1f, float fadeTime = -1f)
    {
        if (clip == null) return;
        anim[clip].speed = speed;
        if (CurrentClip == clip) return;
        anim.CrossFade(clip, fadeTime < 0f ? fade : fadeTime);
        CurrentClip = clip;
    }

    void Die()
    {
        dead = true;
        // Personagem sem esse modelo (formiga, besouro): o componente fica desligado
        if (anim == null || !isActiveAndEnabled) return;
        if (death != null) { anim[death].speed = 1f; anim.CrossFade(death, 0.1f); CurrentClip = death; }
    }

    void Update()
    {
        if (dead || anim == null) return;

        if (needle != null && needle.Uses != needleUses)
        {
            needleUses = needle.Uses;
            punchUntil = Time.time + 0.35f;
            if (punch != null) { anim[punch].time = 0f; Play(punch, 1.6f, 0.05f); }
        }
        if (Time.time < punchUntil) return;

        if (movement == null) { Play(idle); return; }
        if (movement.IsDashing)
        {
            if (roll != null && CurrentClip != roll) { anim[roll].time = 0f; }
            Play(roll, roll != null ? anim[roll].length / 0.35f : 1f, 0.05f);
            return;
        }

        Vector3 v = movement.Velocity; v.y = 0f;
        float speed = v.magnitude;
        if (!movement.IsGrounded) Play(run, 0.5f);
        else if (speed < 0.4f) Play(idle);
        else if (speed < runThreshold) Play(walk, Mathf.Clamp(speed / walkReferenceSpeed, 0.6f, 1.8f));
        else Play(run, Mathf.Clamp(speed / runReferenceSpeed, 0.7f, 1.6f));
    }
}
