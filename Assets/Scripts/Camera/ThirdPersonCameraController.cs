using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Standalone third-person camera controller (no Cinemachine required).
/// Handles ALL camera rotation (yaw + pitch) via mouse.
/// PlayerMotor handles body rotation toward movement direction only.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 offset = new Vector3(0.5f, 1.5f, -3f); // Over-the-shoulder

    [Header("Rotation")]
    [SerializeField, Min(0.1f)] private float yawSensitivity = 2.5f;    // Sensibilidad horizontal (mouse X)
    [SerializeField, Min(0.1f)] private float pitchSensitivity = 2.5f;  // Sensibilidad vertical (mouse Y)
    [SerializeField] private float minPitch = -40f;  // Límite mirar abajo
    [SerializeField] private float maxPitch = 70f;   // Límite mirar arriba
    [SerializeField] private bool invertY = false;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionLayers = -1;
    [SerializeField] private float cameraRadius = 0.3f;
    [SerializeField] private float minDistance = 0.5f;
    [SerializeField] private float collisionSmoothTime = 0.1f;

    [Header("Input")]
    public InputActionAsset inputActions;
    public string lookActionName = "Look";

    [Header("Debug")]
    public bool showDebugRays = false;

    [Header("Camera Shake")]
    [SerializeField] private Vector3 shakeOffset = Vector3.zero;

    private InputAction _lookAction;
    private bool _ownsLookAction = false;
    private float _currentYaw = 0f;
    private float _currentPitch = 0f;
    private float _currentDistance;
    private float _targetDistance;
    private float _distanceVelocity;

    private void Awake()
    {
        if (target == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        _currentDistance = offset.magnitude;
        _targetDistance = _currentDistance;

        ResolveLookAction();
    }

    private void ResolveLookAction()
    {
        if (inputActions != null)
        {
            _lookAction = inputActions.FindAction(lookActionName, false)
                          ?? inputActions.FindAction($"Player/{lookActionName}", false);
        }

        if (_lookAction == null)
        {
            _lookAction = new InputAction("Look", InputActionType.Value, expectedControlType: "Vector2");
            _lookAction.AddBinding("<Mouse>/delta");
            _ownsLookAction = true;
        }
    }

    private void OnEnable()
    {
        _lookAction?.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Initialize yaw/pitch from current transform
        Vector3 angles = transform.eulerAngles;
        _currentYaw = angles.y;
        _currentPitch = angles.x > 180f ? angles.x - 360f : angles.x;
    }

    private void OnDisable()
    {
        _lookAction?.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        if (_ownsLookAction)
            _lookAction?.Dispose();
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        HandleRotation();
        HandlePosition();
        HandleCollision();
    }

    private void HandleRotation()
    {
        Vector2 lookInput = _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;

        // Use raw mouse delta for more precise control
        if (Mouse.current != null)
        {
            lookInput = Mouse.current.delta.ReadValue();
        }

        // YAW (horizontal) - orbita alrededor del target
        _currentYaw += lookInput.x * yawSensitivity * 0.01f; // 0.01f para convertir delta a grados razonables

        // PITCH (vertical) - rota la cámara arriba/abajo
        float pitchDelta = lookInput.y * pitchSensitivity * 0.01f * (invertY ? 1f : -1f);
        _currentPitch = Mathf.Clamp(_currentPitch + pitchDelta, minPitch, maxPitch);

        // Apply rotation: orbit around target
        Quaternion targetRotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
        transform.rotation = targetRotation;
    }

    private void HandlePosition()
    {
        // Calculate desired position based on rotation and offset
        Vector3 rotatedOffset = transform.rotation * offset;
        Vector3 desiredPosition = target.position + rotatedOffset + shakeOffset;

        transform.position = desiredPosition;
    }

    private void HandleCollision()
    {
        if (target == null) return;

        Vector3 direction = (transform.position - target.position).normalized;
        float currentDist = Vector3.Distance(target.position, transform.position);

        RaycastHit hit;
        bool hasHit = Physics.SphereCast(
            target.position,
            cameraRadius,
            direction,
            out hit,
            currentDist,
            collisionLayers,
            QueryTriggerInteraction.Ignore
        );

        if (showDebugRays)
        {
            Debug.DrawRay(target.position, direction * currentDist, hasHit ? Color.red : Color.green);
            if (hasHit)
                Debug.DrawLine(hit.point, hit.point + Vector3.up * cameraRadius, Color.red);
        }

        if (hasHit)
        {
            _targetDistance = Mathf.Max(hit.distance - cameraRadius * 0.5f, minDistance);
        }
        else
        {
            _targetDistance = offset.magnitude;
        }

        _currentDistance = Mathf.SmoothDamp(_currentDistance, _targetDistance, ref _distanceVelocity, collisionSmoothTime);
        transform.position = target.position + direction * _currentDistance;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugRays || target == null) return;

        Vector3 direction = (transform.position - target.position).normalized;
        float currentDist = Vector3.Distance(target.position, transform.position);

        RaycastHit hit;
        bool hasHit = Physics.SphereCast(
            target.position,
            cameraRadius,
            direction,
            out hit,
            currentDist,
            collisionLayers,
            QueryTriggerInteraction.Ignore
        );

        Gizmos.color = hasHit ? Color.red : Color.green;
        Gizmos.DrawRay(target.position, direction * currentDist);
        if (hasHit)
        {
            Gizmos.DrawWireSphere(hit.point, cameraRadius);
        }
    }

    public void SetShakeOffset(Vector3 offset)
    {
        shakeOffset = offset;
    }

    // Phase 7 update
    private void OnValidate()
    {
        minDistance = Mathf.Max(0.1f, minDistance);
        cameraRadius = Mathf.Max(0.05f, cameraRadius);
        
        // Clamp pitch limits
        minPitch = Mathf.Clamp(minPitch, -89f, 0f);
        maxPitch = Mathf.Clamp(maxPitch, 0f, 89f);
        if (minPitch >= maxPitch) maxPitch = minPitch + 1f;
    }
}