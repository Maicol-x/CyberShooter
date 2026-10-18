using UnityEngine;

/// <summary>
/// Camera shake effect for game feel.
/// Triggers on shooting and taking damage.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraShake : MonoBehaviour
{
    [Header("Shake Settings")]
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 0.05f;
    [SerializeField] private float shakeDecay = 1f;

    [Header("Shoot Shake")]
    [SerializeField] private float shootShakeMagnitude = 0.03f;
    [SerializeField] private float shootShakeDuration = 0.08f;

    [Header("Damage Shake")]
    [SerializeField] private float damageShakeMagnitude = 0.12f;
    [SerializeField] private float damageShakeDuration = 0.25f;

    private float _currentDuration = 0f;
    private float _currentMagnitude = 0f;
    private ThirdPersonCameraController _camController;

    private void Awake()
    {
        _camController = GetComponent<ThirdPersonCameraController>();
        if (_camController == null)
        {
            Debug.LogError("[CameraShake] No ThirdPersonCameraController found on this GameObject!");
        }

        // Phase 7 marker reload
        bool _markerCheck = true;
    }

    private void LateUpdate()
    {
        if (_currentDuration > 0f && _camController != null)
        {
            _currentDuration -= Time.deltaTime;

            // Apply shake offset to the camera controller (not direct transform)
            float x = Random.Range(-1f, 1f) * _currentMagnitude;
            float y = Random.Range(-1f, 1f) * _currentMagnitude;
            _camController.SetShakeOffset(new Vector3(x, y, 0f));

            // Decay magnitude
            _currentMagnitude = Mathf.Lerp(_currentMagnitude, 0f, shakeDecay * Time.deltaTime);

            if (_currentDuration <= 0f)
            {
                _camController.SetShakeOffset(Vector3.zero);
                _currentMagnitude = 0f;
            }
        }
        else
        {
            // Ensure we return to no shake
            if (_camController != null)
            {
                _camController.SetShakeOffset(Vector3.zero);
            }
        }
    }

    /// <summary>
    /// Trigger light shake for shooting.
    /// </summary>
    public void ShakeForShoot()
    {
        TriggerShake(shootShakeMagnitude, shootShakeDuration);
    }

    /// <summary>
    /// Trigger stronger shake for taking damage.
    /// </summary>
    public void ShakeForDamage()
    {
        TriggerShake(damageShakeMagnitude, damageShakeDuration);
    }

    private void TriggerShake(float magnitude, float duration)
    {
        // Only override if new shake is stronger or current shake ended
        if (_currentDuration <= 0f || magnitude > _currentMagnitude)
        {
            _currentMagnitude = magnitude;
            _currentDuration = duration;
        }
        else if (duration > _currentDuration)
        {
            _currentDuration = duration;
        }
    }
}