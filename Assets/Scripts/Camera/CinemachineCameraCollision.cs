using UnityEngine;

/// <summary>
/// Simple camera collision handler for third-person camera.
/// Attach this to the Cinemachine Virtual Camera or the Camera itself.
/// It will push the camera forward when it hits obstacles, preventing wall clipping.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CinemachineCameraCollision : MonoBehaviour
{
    [Header("Collision Settings")]
    [SerializeField] private LayerMask collisionLayers = -1;
    [SerializeField] private float cameraRadius = 0.3f;
    [SerializeField] private float minDistance = 0.5f;
    [SerializeField] private float maxDistance = 4f;
    [SerializeField] private float smoothTime = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebugRays = false;

    private Camera _camera;
    private Transform _cameraTransform;
    private float _currentDistance;
    private float _targetDistance;
    private float _velocity;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _cameraTransform = transform;
        _currentDistance = maxDistance;
        _targetDistance = maxDistance;
    }

    private void LateUpdate()
    {
        HandleCameraCollision();
    }

    private void HandleCameraCollision()
    {
        if (_cameraTransform == null || _cameraTransform.parent == null)
            return;

        // Get the desired camera position (from Cinemachine or parent)
        Vector3 desiredPosition = _cameraTransform.position;
        Vector3 targetPosition = _cameraTransform.parent.position;
        Vector3 direction = (desiredPosition - targetPosition).normalized;
        float desiredDistance = Vector3.Distance(targetPosition, desiredPosition);

        // Raycast from target to camera position
        RaycastHit hit;
        bool hasHit = Physics.SphereCast(
            targetPosition,
            cameraRadius,
            direction,
            out hit,
            desiredDistance,
            collisionLayers,
            QueryTriggerInteraction.Ignore
        );

        if (showDebugRays)
        {
            Debug.DrawRay(targetPosition, direction * desiredDistance, hasHit ? Color.red : Color.green);
            if (hasHit)
                Debug.DrawLine(hit.point, hit.point + Vector3.up * cameraRadius, Color.red);
        }

        if (hasHit)
        {
            _targetDistance = Mathf.Max(hit.distance - cameraRadius * 0.5f, minDistance);
        }
        else
        {
            _targetDistance = maxDistance;
        }

        // Smooth the distance change
        _currentDistance = Mathf.SmoothDamp(_currentDistance, _targetDistance, ref _velocity, smoothTime);

        // Apply the new position
        _cameraTransform.position = targetPosition + direction * _currentDistance;
    }

    public void SetMaxDistance(float distance)
    {
        maxDistance = distance;
        _targetDistance = distance;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugRays || _cameraTransform == null || _cameraTransform.parent == null) return;

        Vector3 desiredPosition = _cameraTransform.position;
        Vector3 targetPosition = _cameraTransform.parent.position;
        Vector3 direction = (desiredPosition - targetPosition).normalized;
        float desiredDistance = Vector3.Distance(targetPosition, desiredPosition);

        RaycastHit hit;
        bool hasHit = Physics.SphereCast(
            targetPosition,
            cameraRadius,
            direction,
            out hit,
            desiredDistance,
            collisionLayers,
            QueryTriggerInteraction.Ignore
        );

        Gizmos.color = hasHit ? Color.red : Color.green;
        Gizmos.DrawRay(targetPosition, direction * desiredDistance);
        if (hasHit)
        {
            Gizmos.DrawWireSphere(hit.point, cameraRadius);
        }
    }

    private void OnValidate()
    {
        if (_camera == null)
            _camera = GetComponent<Camera>();

        minDistance = Mathf.Max(0.1f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        cameraRadius = Mathf.Max(0.05f, cameraRadius);
    }
}