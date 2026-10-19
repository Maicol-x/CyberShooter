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

    [Header("Invulnerabilidad (i-frames)")]
    [Tooltip("Tiempo sin recibir daño tras un golpe. Sin esto, con varios enemigos disparando a la vez te vacían los 100 de vida en 3 frames.")]
    [SerializeField] private float invulnerabilityTime = 0.6f;
    [Tooltip("Intervalo del parpadeo del modelo mientras eres invulnerable (aviso visual).")]
    [SerializeField] private float blinkInterval = 0.08f;

    [Header("Events")]
    public UnityEvent<int, int> OnHealthChanged; // current, max
    public UnityEvent OnDeath;
    public UnityEvent OnDamaged;                 // daño REAL aplicado (no en i-frames): lo usa el HUD para el flash rojo

    private PlayerMotor _playerMotor;
    private CameraShake _cameraShake;
    private bool _isDead = false;

    private float _invulnerableUntil = 0f;
    private Renderer[] _renderers;
    private bool _renderersVisible = true;
    private float _blinkTimer = 0f;

    private void Awake()
    {
        currentHealth = maxHealth;
        _playerMotor = GetComponent<PlayerMotor>();
        _cameraShake = Camera.main?.GetComponent<CameraShake>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void Update()
    {
        if (_renderers == null || _renderers.Length == 0) return;

        bool invulnerable = !_isDead && Time.time < _invulnerableUntil;

        if (invulnerable)
        {
            // Parpadeo: dice "estas en la ventana, este golpe no cuenta".
            _blinkTimer -= Time.deltaTime;
            if (_blinkTimer <= 0f)
            {
                _blinkTimer = Mathf.Max(0.02f, blinkInterval);
                ApplyRenderers(!_renderersVisible);
            }
        }
        else if (!_renderersVisible)
        {
            ApplyRenderers(true);
        }
    }

    private void ApplyRenderers(bool visible)
    {
        _renderersVisible = visible;
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null) _renderers[i].enabled = visible;
        }
    }

    /// <summary>
    /// Apply damage to the player.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (_isDead) return;

        // i-frames: los golpes dentro de la ventana se ignoran. Con 12 enemigos
        // disparando a la vez, sin esto el daño se acumulaba en el mismo frame.
        if (Time.time < _invulnerableUntil) return;

        _invulnerableUntil = Time.time + invulnerabilityTime;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamaged?.Invoke();

        // Trigger camera shake on damage
        _cameraShake?.ShakeForDamage();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Cura al jugador. Devuelve cuanta vida se ha recuperado DE VERDAD (0 si ya
    /// estaba al maximo): lo usan los objetos curativos para no gastarse en balde.
    /// </summary>
    public int Heal(int amount)
    {
        if (_isDead) return 0;
        if (amount <= 0) return 0;

        int before = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        int healed = currentHealth - before;
        if (healed <= 0) return 0;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        return healed;
    }

    private void Die()
    {
        _isDead = true;
        Debug.Log("[PlayerHealth] GAME OVER!");

        // Si murio en mitad de un parpadeo, el cadaver debe quedar visible.
        ApplyRenderers(true);

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
        ThirdPersonCameraController cam = Object.FindAnyObjectByType<ThirdPersonCameraController>();
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
    public bool IsInvulnerable => !_isDead && Time.time < _invulnerableUntil;
}