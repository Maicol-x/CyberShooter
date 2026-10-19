using UnityEngine;

/// <summary>
/// Objeto curativo recogible (cubo verde con BoxCollider marcado como Is Trigger).
///
/// Reglas:
///   - Solo cura al JUGADOR (por tag): los enemigos u otros objetos no lo activan.
///   - Si el jugador esta al maximo de vida, el objeto NO se gasta: sigue ahi
///     hasta que pueda aportar una curacion efectiva.
///   - Aplica la curacion real (nunca pasa del maximo) y se destruye al usarse.
///
/// Para otros objetos curativos basta con duplicar el prefab y cambiar healAmount
/// (por ejemplo 10, 20, 50): todo lo demas es igual.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HealthPickup : MonoBehaviour
{
    [Header("Curacion")]
    [SerializeField, Min(1)] private int healAmount = 20;

    [Header("Solo el jugador")]
    [SerializeField] private string playerTag = "Player";

    [Header("Animacion (opcional)")]
    [SerializeField] private float spinSpeed = 90f;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobSpeed = 2f;

    private Vector3 _startPosition;
    private PlayerHealth _playerHealth;
    private bool _used = false;

    private void Awake()
    {
        _startPosition = transform.position;

        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Update()
    {
        if (_used) return;

        // Gira y flota: se ve que es recogible.
        if (spinSpeed != 0f)
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        if (bobAmplitude != 0f)
        {
            float y = _startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.position = new Vector3(_startPosition.x, y, _startPosition.z);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTrigger(other);
    }

    // Stay cubre el caso "estaba al maximo al tocarlo y se cura despues sin salir".
    private void OnTriggerStay(Collider other)
    {
        HandleTrigger(other);
    }

    private void HandleTrigger(Collider other)
    {
        if (_used || other == null) return;

        // Solo el jugador.
        if (!other.CompareTag(playerTag)) return;

        if (_playerHealth == null)
            _playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (_playerHealth == null) return;
        if (_playerHealth.IsDead) return;

        // Sin vida que recuperar: el objeto se queda disponible (no se gasta).
        if (_playerHealth.CurrentHealth >= _playerHealth.MaxHealth) return;

        int healed = _playerHealth.Heal(healAmount);
        if (healed <= 0) return;

        _used = true;
        Debug.Log("[HealthPickup] Recogido: +" + healed + " de vida (pedido " + healAmount + ").");

        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }
}
