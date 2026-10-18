using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Player health system with UI integration and camera shake on damage.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth;

    [Header("Events")]
    public UnityEvent<int, int> OnHealthChanged; // current, max
    public UnityEvent OnDeath;

    private PlayerMotor _playerMotor;
    private CameraShake _cameraShake;
    private bool _isDead = false;

    private void Awake()
    {
        currentHealth = maxHealth;
        _playerMotor = GetComponent<PlayerMotor>();
        _cameraShake = Camera.main?.GetComponent<CameraShake>();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Apply damage to the player.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (_isDead) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Debug.Log($"[PlayerHealth] Player took {amount} damage. Health: {currentHealth}/{maxHealth}");

        // Trigger camera shake on damage
        _cameraShake?.ShakeForDamage();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Heal the player.
    /// </summary>
    public void Heal(int amount)
    {
        if (_isDead) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Die()
    {
        _isDead = true;
        Debug.Log("[PlayerHealth] GAME OVER!");

        // Disable player movement
        if (_playerMotor != null)
        {
            _playerMotor.enabled = false;
        }

        // Disable shooting
        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null)
        {
            shooting.enabled = false;
        }

        // Disable camera look
        ThirdPersonCameraController cam = Object.FindObjectOfType<ThirdPersonCameraController>();
        if (cam != null)
        {
            cam.enabled = false;
        }

        OnDeath?.Invoke();
    }

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float HealthPercent => (float)currentHealth / maxHealth;
    public bool IsDead => _isDead;
}