using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movimento em terceira pessoa com CharacterController:
/// andar/correr relativo à câmera, pulo com coyote time e buffer, gravidade própria
/// e o player sempre virado para onde a câmera mira. Com a bolha de sabão (FloatTime),
/// segurar pulo caindo faz ele flutuar descendo devagar até o tempo da bolha acabar.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] InputActionReference moveAction;
    [SerializeField] InputActionReference jumpAction;
    [SerializeField] InputActionReference sprintAction;

    [Header("Referências")]
    [Tooltip("Câmera usada como referência de direção. Vazio = Camera.main.")]
    [SerializeField] Transform cameraTransform;

    [Header("Movimento")]
    [SerializeField] float walkSpeed = 8f;
    [SerializeField] float sprintSpeed = 13.5f;
    [Tooltip("Ligado: corre por padrão e segurar Ctrl/Sprint anda mais devagar.")]
    [SerializeField] bool sprintByDefault = true;
    [Tooltip("Quão rápido a velocidade chega no alvo (maior = mais responsivo).")]
    [SerializeField] float acceleration = 20f;
    [Tooltip("Multiplicador da aceleração no ar.")]
    [SerializeField, Range(0f, 1f)] float airControl = 0.6f;
    [Tooltip("Velocidade com que o player gira para a direção da câmera.")]
    [SerializeField] float turnSpeed = 26f;

    [Header("Pulo e gravidade")]
    [SerializeField] float jumpHeight = 2f;
    [SerializeField] float gravity = -34f;
    [Tooltip("Gravidade extra ao cair, para o pulo ficar menos 'flutuante'.")]
    [SerializeField] float fallGravityMultiplier = 1.75f;
    [SerializeField] float maxFallSpeed = 40f;
    [Tooltip("Tempo depois de sair do chão em que ainda dá para pular.")]
    [SerializeField] float coyoteTime = 0.12f;
    [Tooltip("Tempo que um pulo apertado cedo demais fica guardado.")]
    [SerializeField] float jumpBufferTime = 0.12f;

    [Header("Bolha de sabão")]
    [Tooltip("Velocidade de queda dentro da bolha (m/s).")]
    [SerializeField, Min(0f)] float floatFallSpeed = 1.6f;
    [Tooltip("Quão rápido a bolha freia a queda ao se formar (m/s²).")]
    [SerializeField, Min(1f)] float floatBrake = 55f;

    [Header("Empurrão")]
    [Tooltip("Quão rápido o empurrão (knockback) perde força.")]
    [SerializeField, Min(0.1f)] float knockbackDrag = 5f;

    [Header("Detecção de chão")]
    [SerializeField] LayerMask groundMask = ~0;
    [SerializeField] float groundCheckDistance = 0.08f;

    // ---- Hooks para itens/stats (ex.: asa de mosca, perna de gafanhoto, dente-de-leão) ----
    [HideInInspector] public float SpeedMultiplier = 1f;
    [HideInInspector] public float JumpHeightMultiplier = 1f;
    [HideInInspector] public int ExtraJumps = 0;
    /// <summary>Segundos de bolha por vez no ar (0 = sem bolha). Controlado pelo Inventory.</summary>
    [HideInInspector] public float FloatTime = 0f;
    /// <summary>Segura o pulo por código, sem teclado (testes).</summary>
    [HideInInspector] public bool HoldJumpOverride;
    /// <summary>Testes: anda como se o direcional estivesse nessa posição (null = lê o controle).</summary>
    public Vector2? MoveInputOverride { get; set; }
    /// <summary>
    /// Aderência ao chão (1 = normal). A gosma da lesma baixa para quase zero: o player demora para
    /// mudar de direção e para frear (escorrega). Quem controla é o SlimeTrail, a cada frame.
    /// </summary>
    [HideInInspector] public float Grip = 1f;
    /// <summary>No chão e escorregando (gosma).</summary>
    public bool IsSlipping => IsGrounded && Grip < 0.5f;
    /// <summary>
    /// Lentidão do chão (água da poça, teia: SlowZone). 1 = normal; pega na hora ao entrar e
    /// passa aos poucos depois de sair (ainda molhado/grudento).
    /// </summary>
    public float SurfaceSpeedFactor { get; private set; } = 1f;
    /// <summary>Desligado = ignora o input mas a gravidade continua (morte, cutscene, menu).</summary>
    [HideInInspector] public bool InputEnabled = true;

    public bool IsGrounded { get; private set; }
    public bool IsSprinting { get; private set; }
    /// <summary>True quando o jogador segura Ctrl para caminhar devagar / focar a mira.</summary>
    public bool IsSlowWalkHeld => InputEnabled && sprintAction != null && sprintAction.action.IsPressed();
    /// <summary>Velocidade real (no chão, ignora a força que cola o player no chão).</summary>
    public Vector3 Velocity => horizontalVelocity + Vector3.up * (IsGrounded ? 0f : verticalVelocity);
    /// <summary>Velocidade horizontal normalizada pela velocidade de andar (útil para animação).</summary>
    public float NormalizedSpeed => horizontalVelocity.magnitude / Mathf.Max(walkSpeed, 0.01f);
    public float WalkSpeed => walkSpeed;
    /// <summary>Dentro da bolha de sabão agora (no ar, segurando pulo).</summary>
    public bool IsFloating { get; private set; }
    /// <summary>Quanto da bolha ainda sobra neste pulo (1 = cheia, 0 = estourou).</summary>
    public float FloatRemaining01 => FloatTime > 0f ? Mathf.Clamp01(1f - floatUsed / FloatTime) : 0f;
    public bool HasFloat => FloatTime > 0f;

    /// <summary>Estoura a bolha agora (levou dano): só volta depois de pisar no chão.</summary>
    public void PopFloat()
    {
        floatUsed = FloatTime;
        IsFloating = false;
    }
    public float SprintSpeed => sprintSpeed;

    /// <summary>Velocidades base do personagem escolhido (PlayerCharacterSetup). Itens multiplicam por SpeedMultiplier.</summary>
    public void SetBaseSpeeds(float walk, float sprint)
    {
        walkSpeed = Mathf.Max(0.1f, walk);
        sprintSpeed = Mathf.Max(walkSpeed, sprint);
    }

    CharacterController controller;
    Vector3 horizontalVelocity;
    Vector3 knockbackVelocity;
    Vector3 dashVelocity;
    float dashUntil;
    float rootedUntil = float.NegativeInfinity, rootImmuneUntil = float.NegativeInfinity;
    float verticalVelocity;
    float lastGroundedTime = float.NegativeInfinity;
    float lastJumpPressedTime = float.NegativeInfinity;
    int jumpsUsed;
    float floatUsed;
    readonly Collider[] groundHits = new Collider[8];

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    void OnEnable()
    {
        moveAction?.action.Enable();
        jumpAction?.action.Enable();
        sprintAction?.action.Enable();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        UpdateGrounded();

        // ---------- Input ----------
        Vector2 input = !InputEnabled ? Vector2.zero
            : MoveInputOverride ?? (moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero);
        input = Vector2.ClampMagnitude(input, 1f);
        if (InputEnabled && jumpAction != null && jumpAction.action.WasPressedThisFrame()) lastJumpPressedTime = Time.time;

        // ---------- Direção relativa à câmera ----------
        Vector3 camForward = Vector3.forward, camRight = Vector3.right;
        if (cameraTransform != null)
        {
            camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            camRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        }
        Vector3 moveDir = camForward * input.y + camRight * input.x;
        MoveDirection = moveDir;

        // ---------- Dash: ignora input e gravidade enquanto dura ----------
        if (IsDashing)
        {
            controller.Move(dashVelocity * dt);
            horizontalVelocity = dashVelocity * 0.3f; // sai do dash com um pouco de embalo
            verticalVelocity = 0f;
            IsFloating = false;
            FaceCamera(camForward, dt);
            return;
        }

        // ---------- Velocidade horizontal ----------
        bool sprintKeyPressed = sprintAction != null && sprintAction.action.IsPressed();
        IsSprinting = InputEnabled && input.sqrMagnitude > 0.01f && (sprintByDefault ? !sprintKeyPressed : sprintKeyPressed);
        SurfaceSpeedFactor = Mathf.Min(SlowZone.FactorAt(transform.position), Mathf.MoveTowards(SurfaceSpeedFactor, 1f, dt * 1.25f));
        float speed = (IsSprinting ? sprintSpeed : walkSpeed) * SpeedMultiplier * SurfaceSpeedFactor;
        Vector3 targetVelocity = IsRooted ? Vector3.zero : moveDir * speed;
        float accel = acceleration * (IsGrounded ? Mathf.Clamp01(Grip) : airControl);
        horizontalVelocity = Vector3.Lerp(horizontalVelocity, targetVelocity, 1f - Mathf.Exp(-accel * dt));

        // ---------- Pulo ----------
        bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool canGroundJump = Time.time - lastGroundedTime <= coyoteTime && jumpsUsed == 0;
        // Cair de uma borda sem pular gasta o pulo do chão: no ar só sobram os ExtraJumps.
        bool canAirJump = !canGroundJump && Mathf.Max(jumpsUsed, 1) < 1 + ExtraJumps;
        if (jumpBuffered && !IsRooted && (canGroundJump || canAirJump))
        {
            jumpsUsed = canGroundJump ? 1 : Mathf.Max(jumpsUsed, 1) + 1;
            verticalVelocity = Mathf.Sqrt(2f * jumpHeight * JumpHeightMultiplier * -gravity);
            lastJumpPressedTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            IsGrounded = false;
        }

        // ---------- Bolha de sabão: segurando pulo enquanto cai ----------
        bool jumpHeld = InputEnabled && (HoldJumpOverride || (jumpAction != null && jumpAction.action.IsPressed()));
        IsFloating = FloatTime > 0f && !IsGrounded && !IsRooted && jumpHeld && verticalVelocity <= 0f && floatUsed < FloatTime;
        if (IsFloating) floatUsed += dt;

        // ---------- Gravidade ----------
        // Deslocamento pela média da velocidade no frame: a altura do pulo fica igual em qualquer FPS.
        float verticalStart = verticalVelocity;
        if (IsGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = verticalStart = -2f; // mantém o controller colado no chão (rampas/degraus)
        }
        else if (IsFloating)
        {
            // A bolha freia a queda rápido e depois desce devagar
            verticalVelocity = Mathf.MoveTowards(verticalVelocity, -floatFallSpeed, floatBrake * dt);
        }
        else
        {
            float g = gravity * (verticalVelocity < 0f ? fallGravityMultiplier : 1f);
            verticalVelocity = Mathf.Max(verticalVelocity + g * dt, -maxFallSpeed);
        }
        float verticalDelta = (verticalStart + verticalVelocity) * 0.5f * dt;

        // ---------- Aplicar ----------
        knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, 1f - Mathf.Exp(-knockbackDrag * dt));
        CollisionFlags flags = controller.Move((horizontalVelocity + knockbackVelocity) * dt + Vector3.up * verticalDelta);
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f; // bateu a cabeça

        FaceCamera(camForward, dt);
    }

    // Girar para onde a câmera mira
    void FaceCamera(Vector3 camForward, float dt)
    {
        if (camForward.sqrMagnitude <= 0.001f) return;
        Quaternion targetRot = Quaternion.LookRotation(camForward, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-turnSpeed * dt));
    }

    /// <summary>Direção de movimento pedida pelo input agora (relativa à câmera; zero se parado).</summary>
    public Vector3 MoveDirection { get; private set; }
    public bool IsDashing => Time.time < dashUntil;

    /// <summary>Preso (teia da aranha): não anda, não pula nem dá dash. Ainda mira e atira.</summary>
    public bool IsRooted => Time.time < rootedUntil;

    /// <summary>
    /// Prende o player por `duration` segundos. Depois de soltar, fica imune por `immunity`
    /// segundos (várias aranhas não prendem em sequência). Retorna se prendeu.
    /// </summary>
    public bool Root(float duration, float immunity = 1.5f)
    {
        if (IsRooted || Time.time < rootImmuneUntil || duration <= 0f) return false;
        rootedUntil = Time.time + duration;
        rootImmuneUntil = rootedUntil + immunity;
        horizontalVelocity = Vector3.zero;
        dashUntil = 0f;
        return true;
    }

    /// <summary>Avança `distance` metros em `duration` segundos na horizontal, sem gravidade.</summary>
    public void Dash(Vector3 direction, float distance, float duration)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
        dashVelocity = direction.normalized * (distance / Mathf.Max(0.01f, duration));
        dashUntil = Time.time + duration;
        verticalVelocity = 0f;
        knockbackVelocity = Vector3.zero;
    }

    /// <summary>Empurra o player (golpe forte, explosão). A parte vertical tira ele do chão.</summary>
    public void AddKnockback(Vector3 velocity)
    {
        knockbackVelocity += new Vector3(velocity.x, 0f, velocity.z);
        if (velocity.y > 0f)
        {
            verticalVelocity = Mathf.Max(verticalVelocity, velocity.y);
            IsGrounded = false;
        }
    }

    void UpdateGrounded()
    {
        // isGrounded do CharacterController falha às vezes em rampas; reforça com uma esfera nos pés.
        bool grounded = controller.isGrounded;
        if (!grounded && verticalVelocity <= 0f)
        {
            GetFeetSphere(controller, out Vector3 feet, out float radius);
            int count = Physics.OverlapSphereNonAlloc(feet, radius, groundHits, groundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (groundHits[i] != controller) { grounded = true; break; }
            }
        }

        IsGrounded = grounded && verticalVelocity <= 0f;
        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
            jumpsUsed = 0;
            floatUsed = 0f; // a bolha enche de novo ao pisar no chão
            IsFloating = false;
        }
    }

    /// <summary>
    /// Esfera um pouco menor que a base da cápsula, descida pelo skinWidth + groundCheckDistance,
    /// para encostar no chão mesmo com o controller "flutuando" na skin.
    /// </summary>
    void GetFeetSphere(CharacterController cc, out Vector3 center, out float radius)
    {
        radius = cc.radius * 0.95f;
        Vector3 bottomSphere = transform.position + cc.center + Vector3.down * (cc.height * 0.5f - cc.radius);
        center = bottomSphere + Vector3.down * (cc.skinWidth + groundCheckDistance);
    }

    void OnDrawGizmosSelected()
    {
        var cc = GetComponent<CharacterController>();
        if (cc == null) return;
        GetFeetSphere(cc, out Vector3 feet, out float radius);
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(feet, radius);
    }
}
