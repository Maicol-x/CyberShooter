using UnityEngine;
using UnityEngine.InputSystem;
using System;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform cameraTransform;

    [Header("Input Actions")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string sprintActionName = "Sprint";
    [SerializeField] private string dashActionName = "Dash";

    [Header("Movement Settings")]
    [SerializeField, Min(0f)] private float walkSpeed = 5f;
    [SerializeField, Min(0f)] private float sprintSpeed = 9f;

    // Public properties for other scripts to read
    public float WalkSpeed => walkSpeed;
    public float SprintSpeed => sprintSpeed;
    public bool HasMovementInput => _moveAction != null && _moveAction.ReadValue<Vector2>().sqrMagnitude > 0.01f;
    [SerializeField, Min(0f)] private float rotationSpeed = 12f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float groundedStickForce = -2f;
    [SerializeField, Min(0f)] private float coyoteTime = 0.1f;
    [SerializeField, Min(0f)] private float jumpBufferTime = 0.1f;

    [Header("Dash Settings")]
    [SerializeField, Min(0f)] private float dashSpeed = 20f;
    [SerializeField, Min(0f)] private float dashDuration = 0.2f;
    [SerializeField, Min(0f)] private float dashCooldown = 1f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayer = -1;
    [SerializeField] private float groundCheckDistance = 0.1f;

    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _sprintAction;
    private InputAction _dashAction;
    private bool _ownsActions = false;

    private Vector3 _velocity;
    private float _verticalVelocity;
    private bool _isGrounded;
    private bool _wasGrounded;
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private bool _jumpRequested;
    private float _dashTimer = 0f;
    private float _dashCooldownTimer = 0f;
    private bool _isDashing = false;
    private Vector3 _dashDirection;

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (cameraTransform == null)
            cameraTransform = Camera.main?.transform;

        ResolveActions();
    }

    private void ResolveActions()
    {
        if (inputActions != null)
        {
            _moveAction = inputActions.FindAction(moveActionName, false)
                          ?? inputActions.FindAction($"Player/{moveActionName}", false);

            _jumpAction = inputActions.FindAction(jumpActionName, false)
                          ?? inputActions.FindAction($"Player/{jumpActionName}", false);

            _sprintAction = inputActions.FindAction(sprintActionName, false)
                            ?? inputActions.FindAction($"Player/{sprintActionName}", false);

            _dashAction = inputActions.FindAction(dashActionName, false)
                           ?? inputActions.FindAction($"Player/{dashActionName}", false);
        }

        if (_moveAction == null)
        {
            _moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _ownsActions = true;
        }

        if (_jumpAction == null)
        {
            _jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _ownsActions = true;
        }

        if (_sprintAction == null)
        {
            _sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            _ownsActions = true;
        }

        if (_dashAction == null)
        {
            _dashAction = new InputAction("Dash", InputActionType.Button, "<Keyboard>/leftCtrl");
            _ownsActions = true;
        }
    }

    private void OnEnable()
    {
        _moveAction?.Enable();
        _jumpAction?.Enable();
        _sprintAction?.Enable();
        _dashAction?.Enable();

        if (_jumpAction != null)
            _jumpAction.performed += OnJumpPerformed;

        if (_dashAction != null)
            _dashAction.performed += OnDashPerformed;
    }

    private void OnDisable()
    {
        _moveAction?.Disable();
        _jumpAction?.Disable();
        _sprintAction?.Disable();
        _dashAction?.Disable();

        if (_jumpAction != null)
            _jumpAction.performed -= OnJumpPerformed;

        if (_dashAction != null)
            _dashAction.performed -= OnDashPerformed;
    }

    private void OnDestroy()
    {
        if (_ownsActions)
        {
            _moveAction?.Dispose();
            _jumpAction?.Dispose();
            _sprintAction?.Dispose();
            _dashAction?.Dispose();
        }
    }

    private void OnJumpPerformed(InputAction.CallbackContext ctx)
    {
        _jumpBufferTimer = jumpBufferTime;
        OnJump?.Invoke();
    }

    private void OnDashPerformed(InputAction.CallbackContext ctx)
    {
        if (_dashCooldownTimer <= 0f && !_isDashing)
        {
            Vector2 input = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            if (input.sqrMagnitude > 0.01f)
            {
                _isDashing = true;
                _dashTimer = dashDuration;
                _dashCooldownTimer = dashCooldown;

                Vector3 forward = Vector3.zero;
                Vector3 right = Vector3.zero;

                if (cameraTransform != null)
                {
                    forward = cameraTransform.forward;
                    right = cameraTransform.right;
                    forward.y = 0f;
                    right.y = 0f;
                    forward.Normalize();
                    right.Normalize();
                }
                else
                {
                    forward = transform.forward;
                    right = transform.right;
                }

                _dashDirection = (forward * input.y + right * input.x).normalized;
                OnDash?.Invoke();
            }
        }
    }

    private void Update()
    {
        // Red de seguridad: si por lo que sea falta el CharacterController, no reventamos.
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
                return;
        }

        HandleGroundCheck();
        HandleJumpInput();
        HandleJumpBuffer();
        HandleGravity();     // Calculamos la velocidad vertical (incluye el salto) ANTES de movernos
        HandleMovement();    // ...y nos movemos ya con la velocidad vertical de este mismo frame
        HandleRotation();
        HandleDash();
    }

    // Lee el salto con el NUEVO Input System de forma garantizada.
    // Funciona aunque el InputActionAsset no esté asignado o su acción "Jump" no tenga binding.
    private void HandleJumpInput()
    {
        bool jumpPressed = false;

        // Vía InputAction (si está configurada y con binding correcto).
        if (_jumpAction != null && _jumpAction.triggered)
            jumpPressed = true;

        // Fallback directo y a prueba de balas con el nuevo Input System.
        if (!jumpPressed && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            jumpPressed = true;

        if (jumpPressed)
            _jumpBufferTimer = jumpBufferTime;
    }

    private void HandleGroundCheck()
    {
        _wasGrounded = _isGrounded;

        // 1) El propio CharacterController: lo más fiable cuando se mueve hacia abajo.
        bool grounded = characterController.isGrounded;

        // Datos de la cápsula
        float capsuleBottomY = transform.position.y + characterController.center.y
                               - (characterController.height * 0.5f);
        float checkDist = Mathf.Max(0.1f, groundCheckDistance);

        // 2) SphereCast desde la BASE de la cápsula hacia abajo (geometría correcta,
        //    antes se lanzaba desde el centro y se quedaba corta -> isGrounded se
        //    quedaba atascado en false tras caer).
        if (!grounded)
        {
            float radius = Mathf.Max(0.05f, characterController.radius * 0.95f);
            Vector3 origin = new Vector3(transform.position.x, capsuleBottomY + radius + 0.02f, transform.position.z);
            grounded = Physics.SphereCast(origin, radius, Vector3.down, out _,
                                          radius + checkDist, groundLayer, QueryTriggerInteraction.Ignore);
        }

        // 3) Raycast de respaldo (por si el SphereCast falla con mallas no convexas).
        if (!grounded)
        {
            Vector3 rayOrigin = new Vector3(transform.position.x, capsuleBottomY + 0.05f, transform.position.z);
            grounded = Physics.Raycast(rayOrigin, Vector3.down, out _,
                                       0.05f + checkDist, groundLayer, QueryTriggerInteraction.Ignore);
        }

        _isGrounded = grounded;

        // Trigger OnLand event when landing
        if (_isGrounded && !_wasGrounded)
        {
            OnLand?.Invoke();
        }

        if (_isGrounded && _verticalVelocity <= 0f)
        {
            // Pequeña fuerza hacia abajo: hace que el CharacterController se pegue al
            // suelo y la detección de "grounded" sea fiable (evita quedar flotando).
            _verticalVelocity = groundedStickForce;
            _coyoteTimer = coyoteTime;
        }
        else
        {
            _coyoteTimer -= Time.deltaTime;
        }
    }

    private void HandleJumpBuffer()
    {
        if (_jumpBufferTimer > 0f)
        {
            _jumpBufferTimer -= Time.deltaTime;
            if (_coyoteTimer > 0f)
            {
                _jumpRequested = true;
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
            }
        }
    }

    private void HandleMovement()
    {
        Vector2 input = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;

        // Get camera-relative movement direction
        Vector3 forward = Vector3.zero;
        Vector3 right = Vector3.zero;

        if (cameraTransform != null)
        {
            forward = cameraTransform.forward;
            right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
        }
        else
        {
            forward = transform.forward;
            right = transform.right;
        }

        Vector3 moveDirection = (forward * input.y + right * input.x).normalized;

        // Determine speed
        bool isSprinting = _sprintAction != null && _sprintAction.IsPressed() && input.sqrMagnitude > 0.01f;
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        // Apply movement
        Vector3 horizontalVelocity = moveDirection * currentSpeed;
        _velocity.x = horizontalVelocity.x;
        _velocity.z = horizontalVelocity.z;

        characterController.Move(_velocity * Time.deltaTime);
    }

    private void HandleGravity()
    {
        // El SALTO tiene prioridad sobre la fuerza de "pegado" al suelo,
        // así el groundedStickForce nunca cancela el impulso del salto.
        if (_jumpRequested)
        {
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpRequested = false;
            _isGrounded = false;   // dejamos el suelo inmediatamente al saltar
            _coyoteTimer = 0f;
            OnJump?.Invoke();
        }
        else if (_isGrounded && _verticalVelocity <= 0f)
        {
            _verticalVelocity = groundedStickForce;
        }
        else
        {
            _verticalVelocity += gravity * Time.deltaTime;
        }

        _velocity.y = _verticalVelocity;
    }

    private void HandleRotation()
    {
        if (_isDashing) return;

        Vector2 input = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;

        if (input.sqrMagnitude > 0.01f)
        {
            Vector3 targetDirection = Vector3.zero;
            if (cameraTransform != null)
            {
                targetDirection = cameraTransform.forward * input.y + cameraTransform.right * input.x;
            }
            else
            {
                targetDirection = transform.forward * input.y + transform.right * input.x;
            }

            targetDirection.y = 0f;
            if (targetDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }

    private void HandleDash()
    {
        if (_dashTimer > 0f)
        {
            _dashTimer -= Time.deltaTime;
            if (_dashTimer <= 0f)
            {
                _isDashing = false;
                _dashTimer = 0f;
            }
            else
            {
                characterController.Move(_dashDirection * dashSpeed * Time.deltaTime);
            }
        }

        if (_dashCooldownTimer > 0f)
        {
            _dashCooldownTimer -= Time.deltaTime;
            if (_dashCooldownTimer < 0f)
                _dashCooldownTimer = 0f;
        }
    }

    // Phase 7: Animation Controller support
    private bool _phase7AnimationSupport = true;

    // Public properties for animation controller and other scripts
    public float CurrentSpeed => _isDashing ? dashSpeed : (_sprintAction != null && _sprintAction.IsPressed() && _moveAction != null && _moveAction.ReadValue<Vector2>().sqrMagnitude > 0.01f ? sprintSpeed : walkSpeed);
    public bool IsSprinting => _sprintAction != null && _sprintAction.IsPressed() && _moveAction != null && _moveAction.ReadValue<Vector2>().sqrMagnitude > 0.01f;
    public bool IsGrounded => _isGrounded;
    public bool IsDashing => _isDashing;
    public bool JumpRequested => _jumpRequested;
    public bool IsJumping => !_isGrounded && _verticalVelocity > 0f;
    public bool IsFalling => !_isGrounded && _verticalVelocity < 0f;
    public float VerticalVelocity => _verticalVelocity;
    public Vector3 Velocity => _velocity;
    public bool ShootRequested { get; set; }
    
    public event Action OnShoot;
    public event Action OnJump;
    public event Action OnDash;
    public event Action OnLand;

    // Force recompile v22
    private bool _forceRecompile = true;

    // Force recompile v15
    private bool _forceRecompile2 = true;

    private void OnValidate()
    {
        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (dashDuration < 0.05f) dashDuration = 0.05f;
        if (dashCooldown < 0.1f) dashCooldown = 0.1f;
        if (walkSpeed < 0.1f) walkSpeed = 0.1f;
        if (sprintSpeed < walkSpeed) sprintSpeed = walkSpeed + 1f;
    }

    // Public method for other scripts to trigger shoot event
    public void TriggerShootEvent()
    {
        OnShoot?.Invoke();
    }
    // Force recompile v20
}

