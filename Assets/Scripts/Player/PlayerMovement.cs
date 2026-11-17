using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movimento em terceira pessoa com CharacterController:
/// andar/correr relativo à câmera, pulo com coyote time e buffer, gravidade própria
/// e o player sempre virado para onde a câmera mira.
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
    [SerializeField] float walkSpeed = 6f;
    [SerializeField] float sprintSpeed = 10f;
    [Tooltip("Quão rápido a velocidade chega no alvo (maior = mais responsivo).")]
    [SerializeField] float acceleration = 14f;
    [Tooltip("Multiplicador da aceleração no ar.")]
    [SerializeField, Range(0f, 1f)] float airControl = 0.4f;
    [Tooltip("Velocidade com que o player gira para a direção da câmera.")]
    [SerializeField] float turnSpeed = 20f;

    [Header("Pulo e gravidade")]
    [SerializeField] float jumpHeight = 1.8f;
    [SerializeField] float gravity = -30f;
    [Tooltip("Gravidade extra ao cair, para o pulo ficar menos 'flutuante'.")]
    [SerializeField] float fallGravityMultiplier = 1.6f;
    [SerializeField] float maxFallSpeed = 40f;
    [Tooltip("Tempo depois de sair do chão em que ainda dá para pular.")]
    [SerializeField] float coyoteTime = 0.12f;
    [Tooltip("Tempo que um pulo apertado cedo demais fica guardado.")]
    [SerializeField] float jumpBufferTime = 0.12f;

    [Header("Detecção de chão")]
    [SerializeField] LayerMask groundMask = ~0;
    [SerializeField] float groundCheckDistance = 0.08f;

    // ---- Hooks para itens/stats (ex.: asa de mosca, perna de gafanhoto, dente-de-leão) ----
    [HideInInspector] public float SpeedMultiplier = 1f;
    [HideInInspector] public float JumpHeightMultiplier = 1f;
    [HideInInspector] public int ExtraJumps = 0;
    /// <summary>Desligado = ignora o input mas a gravidade continua (morte, cutscene, menu).</summary>
    [HideInInspector] public bool InputEnabled = true;

    public bool IsGrounded { get; private set; }
    public bool IsSprinting { get; private set; }
    /// <summary>Velocidade real (no chão, ignora a força que cola o player no chão).</summary>
    public Vector3 Velocity => horizontalVelocity + Vector3.up * (IsGrounded ? 0f : verticalVelocity);
    /// <summary>Velocidade horizontal normalizada pela velocidade de andar (útil para animação).</summary>
    public float NormalizedSpeed => horizontalVelocity.magnitude / Mathf.Max(walkSpeed, 0.01f);

    CharacterController controller;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    float lastGroundedTime = float.NegativeInfinity;
    float lastJumpPressedTime = float.NegativeInfinity;
    int jumpsUsed;
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
        Vector2 input = InputEnabled && moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
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

        // ---------- Velocidade horizontal ----------
        IsSprinting = InputEnabled && sprintAction != null && sprintAction.action.IsPressed() && input.sqrMagnitude > 0.01f;
        float speed = (IsSprinting ? sprintSpeed : walkSpeed) * SpeedMultiplier;
        Vector3 targetVelocity = moveDir * speed;
        float accel = acceleration * (IsGrounded ? 1f : airControl);
        horizontalVelocity = Vector3.Lerp(horizontalVelocity, targetVelocity, 1f - Mathf.Exp(-accel * dt));

        // ---------- Pulo ----------
        bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool canGroundJump = Time.time - lastGroundedTime <= coyoteTime && jumpsUsed == 0;
        // Cair de uma borda sem pular gasta o pulo do chão: no ar só sobram os ExtraJumps.
        bool canAirJump = !canGroundJump && Mathf.Max(jumpsUsed, 1) < 1 + ExtraJumps;
        if (jumpBuffered && (canGroundJump || canAirJump))
        {
            jumpsUsed = canGroundJump ? 1 : Mathf.Max(jumpsUsed, 1) + 1;
            verticalVelocity = Mathf.Sqrt(2f * jumpHeight * JumpHeightMultiplier * -gravity);
            lastJumpPressedTime = float.NegativeInfinity;
            lastGroundedTime = float.NegativeInfinity;
            IsGrounded = false;
        }

        // ---------- Gravidade ----------
        // Deslocamento pela média da velocidade no frame: a altura do pulo fica igual em qualquer FPS.
        float verticalStart = verticalVelocity;
        if (IsGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = verticalStart = -2f; // mantém o controller colado no chão (rampas/degraus)
        }
        else
        {
            float g = gravity * (verticalVelocity < 0f ? fallGravityMultiplier : 1f);
            verticalVelocity = Mathf.Max(verticalVelocity + g * dt, -maxFallSpeed);
        }
        float verticalDelta = (verticalStart + verticalVelocity) * 0.5f * dt;

        // ---------- Aplicar ----------
        CollisionFlags flags = controller.Move(horizontalVelocity * dt + Vector3.up * verticalDelta);
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f; // bateu a cabeça

        // ---------- Girar para onde a câmera mira ----------
        if (camForward.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(camForward, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 1f - Mathf.Exp(-turnSpeed * dt));
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
