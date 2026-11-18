using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Torna a câmera em 3ª pessoa mais dinâmica e responsiva:
/// - FOV dinâmico por velocidade (corrida, dash/agulha, coice de tiro e foco no Ctrl)
/// - Inclinação lateral suave (Dutch tilt) ao fazer strafe/dash e afundada ao pousar
/// - Enquadramento over-the-shoulder (troca de ombro com V) + zoom com Scroll do mouse
/// - Abertura automática na luta contra o chefe e órbita cinematográfica no fim da run
/// </summary>
public class DynamicCameraFeel : MonoBehaviour
{
    [Header("Referências (auto se vazio)")]
    [SerializeField] CinemachineCamera playerCamera;
    [SerializeField] CinemachineOrbitalFollow orbitalFollow;
    [SerializeField] CinemachineRotationComposer rotationComposer;
    [SerializeField] CinemachineInputAxisController lookInput;

    [Header("Inércia e enquadramento")]
    [SerializeField] Vector3 gameplayDamping = new(0.06f, 0.10f, 0.06f);
    [SerializeField] Vector2 composerDamping = new(0.04f, 0.05f);
    [SerializeField] float shoulderOffsetX = 0.09f;

    [Header("Zoom (Scroll)")]
    [SerializeField] float minZoomScale = 0.66f;
    [SerializeField] float maxZoomScale = 1.72f;
    [SerializeField] float scrollStep = 0.08f;

    [Header("FOV dinâmico")]
    [SerializeField] float sprintFovBoost = 3.5f;
    [SerializeField] float dashFovBoost = 10.5f;
    [SerializeField] float focusFovOffset = -14f;
    [SerializeField] float shotFovKick = -1.1f;

    [Header("Dutch Tilt & Pouso")]
    [SerializeField] float strafeDutch = 1.5f;
    [SerializeField] float dashDutch = 3.2f;
    [SerializeField] float maxLandingDip = 0.24f;

    [Header("Foco (Ctrl) e Chefe")]
    [SerializeField] float focusZoomOffset = -0.36f;
    [SerializeField] float focusMaxZoomScale = 0.72f;
    [SerializeField] float focusExtraShoulder = 0.13f;
    [SerializeField] float focusScreenYOffset = -0.045f;
    [SerializeField] float focusTargetLift = 0.22f;
    [SerializeField, Range(0.3f, 1f)] float focusSensitivityScale = 0.55f;
    [SerializeField] float bossZoomOut = 0.34f;
    [SerializeField] float bossScreenYOffset = -0.05f;
    [SerializeField] float endRunOrbitSpeed = 18f;

    public bool IsFocusing { get; private set; }

    PlayerMovement movement;
    ThornSlingshot slingshot;
    Transform cameraTarget;
    Vector3 baseTargetLocalPos;
    bool hasTargetBase;

    float baseTopRadius = 2f;
    float baseCenterRadius = 4f;
    float baseBottomRadius = 2.5f;
    float baseRadius = 5f;
    Vector2 baseScreenPos = new(0f, 0.15f);
    bool capturedCameraDefaults;

    float userZoomScale = 1f;
    float currentZoomScale = 1f;
    float shoulderSign = 1f;
    float currentShoulderX;
    float currentFovOffset;
    float shotKickCurrent;
    float currentDutch;
    float landingDip;
    float landingVel;
    float currentFocusLift;
    bool wasGrounded = true;
    float minVerticalVelInAir;
    float lastBossCheck;
    MantisBoss activeBoss;
    float lastAppliedSens = -1f;

    void Awake()
    {
        if (playerCamera == null) playerCamera = GetComponent<CinemachineCamera>();
        if (orbitalFollow == null) orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
        if (rotationComposer == null) rotationComposer = GetComponent<CinemachineRotationComposer>();
        if (lookInput == null) lookInput = GetComponent<CinemachineInputAxisController>();
    }

    void OnEnable()
    {
        GameSettings.Changed += HandleSettingsChanged;
        HookPlayer();
    }

    void Start()
    {
        HookPlayer();
        CaptureDefaults();
        currentShoulderX = shoulderOffsetX * shoulderSign;
    }

    void OnDisable()
    {
        GameSettings.Changed -= HandleSettingsChanged;
        if (slingshot != null) slingshot.OnFired -= HandleShotFired;
        if (hasTargetBase && cameraTarget != null) cameraTarget.localPosition = baseTargetLocalPos;
        ApplySensitivity(1f);
    }

    void HookPlayer()
    {
        if (movement == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                movement = p.GetComponent<PlayerMovement>();
                slingshot = p.GetComponent<ThornSlingshot>();
                if (slingshot != null)
                {
                    slingshot.OnFired -= HandleShotFired;
                    slingshot.OnFired += HandleShotFired;
                }
            }
        }

        if (!hasTargetBase && playerCamera != null && playerCamera.Target.TrackingTarget != null)
        {
            cameraTarget = playerCamera.Target.TrackingTarget;
            baseTargetLocalPos = cameraTarget.localPosition;
            hasTargetBase = true;
        }
    }

    void CaptureDefaults()
    {
        if (capturedCameraDefaults) return;
        capturedCameraDefaults = true;
        if (orbitalFollow != null)
        {
            baseTopRadius = orbitalFollow.Orbits.Top.Radius;
            baseCenterRadius = orbitalFollow.Orbits.Center.Radius;
            baseBottomRadius = orbitalFollow.Orbits.Bottom.Radius;
            baseRadius = orbitalFollow.Radius;
            var tracker = orbitalFollow.TrackerSettings;
            tracker.PositionDamping = gameplayDamping;
            orbitalFollow.TrackerSettings = tracker;
        }
        if (rotationComposer != null)
        {
            baseScreenPos = rotationComposer.Composition.ScreenPosition;
            rotationComposer.Damping = composerDamping;
        }
    }

    void HandleSettingsChanged() => lastAppliedSens = -1f;

    void HandleShotFired()
    {
        shotKickCurrent = shotFovKick * (IsFocusing ? 0.45f : 1f);
        GameFeel.Shake(IsFocusing ? 0.03f : 0.06f);
    }

    void LateUpdate()
    {
        if (PauseMenu.IsPaused || playerCamera == null) return;
        if (!capturedCameraDefaults) CaptureDefaults();
        if (movement == null) HookPlayer();

        var rm = RunManager.Instance;
        bool runFinished = rm != null && (rm.IsRunOver || rm.IsStageComplete);
        if (runFinished)
        {
            IsFocusing = false;
            if (orbitalFollow != null)
                orbitalFollow.HorizontalAxis.Value += endRunOrbitSpeed * Time.unscaledDeltaTime;
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        HandleInput();
        UpdateBossLookup();

        bool dashing = movement != null && movement.IsDashing;
        bool sprinting = movement != null && movement.IsSprinting && !dashing;
        bool focusing = movement != null && movement.IsSlowWalkHeld && !dashing;
        IsFocusing = focusing;
        bool bossNear = IsBossActive();

        // 1. FOV dinâmico + coice elástico de tiro
        shotKickCurrent = Mathf.Lerp(shotKickCurrent, 0f, 1f - Mathf.Exp(-18f * dt));
        float targetFovOffset = 0f;
        if (dashing) targetFovOffset += dashFovBoost;
        else if (focusing) targetFovOffset += focusFovOffset;
        else if (sprinting) targetFovOffset += sprintFovBoost;

        float fovSpeed = focusing ? 14f : 10f;
        currentFovOffset = Mathf.Lerp(currentFovOffset, targetFovOffset, 1f - Mathf.Exp(-fovSpeed * dt));

        // 2. Dutch tilt proporcional ao strafe lateral relativo à câmera
        float lateralSpeed = 0f;
        if (movement != null)
        {
            Vector3 vel = movement.Velocity;
            vel.y = 0f;
            float maxRef = dashing ? 25f : 10f;
            lateralSpeed = Mathf.Clamp(Vector3.Dot(vel, transform.right) / maxRef, -1f, 1f);
        }
        float targetDutch = -lateralSpeed * (dashing ? dashDutch : strafeDutch) * (focusing ? 0.4f : 1f);
        currentDutch = Mathf.Lerp(currentDutch, targetDutch, 1f - Mathf.Exp(-11f * dt));

        var lens = playerCamera.Lens;
        lens.FieldOfView = Mathf.Clamp(GameSettings.FieldOfView + currentFovOffset + shotKickCurrent, 36f, 115f);
        lens.Dutch = currentDutch;
        playerCamera.Lens = lens;

        // 3. Zoom suave (Scroll + Dash + Foco Ctrl + Chefe)
        float targetZoom = userZoomScale;
        if (dashing) targetZoom += 0.12f;
        else if (focusing) targetZoom = Mathf.Min(targetZoom + focusZoomOffset, focusMaxZoomScale);
        if (bossNear) targetZoom += bossZoomOut;
        targetZoom = Mathf.Clamp(targetZoom, 0.42f, 2.2f);
        float zoomSpeed = focusing ? 12f : 8f;
        currentZoomScale = Mathf.Lerp(currentZoomScale, targetZoom, 1f - Mathf.Exp(-zoomSpeed * dt));

        if (orbitalFollow != null)
        {
            var orbits = orbitalFollow.Orbits;
            orbits.Top.Radius = baseTopRadius * currentZoomScale;
            orbits.Center.Radius = baseCenterRadius * currentZoomScale;
            orbits.Bottom.Radius = baseBottomRadius * currentZoomScale;
            orbitalFollow.Orbits = orbits;
            orbitalFollow.Radius = baseRadius * currentZoomScale;
        }

        // 4. Over-the-shoulder & enquadramento do chefe/foco
        float extraShoulder = focusing ? focusExtraShoulder : 0f;
        float targetShoulderX = (shoulderOffsetX + extraShoulder) * shoulderSign;
        float shoulderSpeed = focusing ? 13f : 10f;
        currentShoulderX = Mathf.Lerp(currentShoulderX, targetShoulderX, 1f - Mathf.Exp(-shoulderSpeed * dt));
        if (rotationComposer != null)
        {
            var comp = rotationComposer.Composition;
            float targetY = baseScreenPos.y + (bossNear ? bossScreenYOffset : 0f) + (focusing ? focusScreenYOffset : 0f);
            Vector2 targetScreen = new(currentShoulderX, targetY);
            comp.ScreenPosition = Vector2.Lerp(comp.ScreenPosition, targetScreen, 1f - Mathf.Exp(-10f * dt));
            rotationComposer.Composition = comp;
        }

        // 5. Afundada de pouso elástica + elevação sutil pro ombro no foco
        if (movement != null && hasTargetBase && cameraTarget != null)
        {
            if (!movement.IsGrounded)
            {
                if (wasGrounded) minVerticalVelInAir = 0f;
                minVerticalVelInAir = Mathf.Min(minVerticalVelInAir, movement.Velocity.y);
            }
            else if (!wasGrounded && minVerticalVelInAir < -4.5f)
            {
                float impact = Mathf.InverseLerp(-4.5f, -22f, minVerticalVelInAir);
                landingDip = Mathf.Max(landingDip, impact * maxLandingDip);
                landingVel = 0f;
            }
            wasGrounded = movement.IsGrounded;

            landingDip = Mathf.SmoothDamp(landingDip, 0f, ref landingVel, 0.11f, Mathf.Infinity, dt);
            currentFocusLift = Mathf.Lerp(currentFocusLift, focusing ? focusTargetLift : 0f, 1f - Mathf.Exp(-12f * dt));
            cameraTarget.localPosition = baseTargetLocalPos + Vector3.up * (currentFocusLift - landingDip);
        }

        // 6. Ajuste fino de sensibilidade durante foco com Ctrl
        ApplySensitivity(focusing ? focusSensitivityScale : 1f);
    }

    void HandleInput()
    {
        if (RunManager.InMenu) return;

        var kb = Keyboard.current;
        if (kb != null && kb.vKey.wasPressedThisFrame)
            shoulderSign = -shoulderSign;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                float ticks = Mathf.Sign(scrollY) * Mathf.Clamp(Mathf.Abs(scrollY) / 120f, 0.5f, 3f);
                userZoomScale = Mathf.Clamp(userZoomScale - ticks * scrollStep, minZoomScale, maxZoomScale);
            }
        }
    }

    void UpdateBossLookup()
    {
        if (activeBoss != null && activeBoss.IsAlive) return;
        if (Time.unscaledTime - lastBossCheck < 0.35f) return;
        lastBossCheck = Time.unscaledTime;
        activeBoss = FindAnyObjectByType<MantisBoss>();
    }

    bool IsBossActive()
    {
        if (activeBoss == null || !activeBoss.IsAlive || !activeBoss.isActiveAndEnabled) return false;
        if (movement == null) return true;
        return (activeBoss.transform.position - movement.transform.position).sqrMagnitude <= 50f * 50f;
    }

    void ApplySensitivity(float mult)
    {
        if (lookInput == null) return;
        float target = GameSettings.Sensitivity * mult;
        if (Mathf.Approximately(target, lastAppliedSens)) return;
        lastAppliedSens = target;

        foreach (var c in lookInput.Controllers)
        {
            if (c.Name.Contains("Look Orbit X")) c.Input.Gain = target;
            else if (c.Name.Contains("Look Orbit Y")) c.Input.Gain = -target * (GameSettings.InvertY ? -1f : 1f);
        }
    }
}
