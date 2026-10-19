using UnityEngine;

/// <summary>
/// Enemy deals damage to player when within range.
/// </summary>
public class EnemyAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private int damagePerSecond = 10;
    [SerializeField] private LayerMask playerLayer;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private PlayerHealth _targetHealth;
    private float _damageTimer = 0f;
    private Transform _playerTransform;

    private void Start()
    {
        // Find player
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            _targetHealth = player.GetComponent<PlayerHealth>();
            _playerTransform = player.transform;
        }

        // Default to Player layer if not set
        if (playerLayer == 0)
        {
            playerLayer = LayerMask.GetMask("Player");
        }
    }

    private void Update()
    {
        if (_targetHealth == null || _targetHealth.IsDead || _playerTransform == null)
            return;

        float distance = Vector3.Distance(transform.position, _playerTransform.position);

        if (distance <= attackRange)
        {
            _damageTimer += Time.deltaTime;

            if (_damageTimer >= 1f)
            {
                _targetHealth.TakeDamage(damagePerSecond);
                Debug.Log($"[EnemyAttack] {name} dealt {damagePerSecond} damage to player! Distance: {distance:F2}m");
                _damageTimer = 0f;
            }
        }
        else
        {
            _damageTimer = 0f; // Reset timer when out of range
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}