using UnityEngine;

/// <summary>
/// Enemy AI: chases player, stops at attack range, shoots at player with tracer effect.
/// Only shoots if there is direct Line of Sight (no walls/cubes blocking).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class EnemyAI : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4f;
    public float rotationSpeed = 10f;
    public float stopDistance = 2f;

    [Header("Detection")]
    public float detectionRange = 30f;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Attack")]
    public float attackRange = 25f;
    public float fireRate = 1f;
    public int damagePerShot = 10;
    public Transform muzzlePoint;
    public float tracerDuration = 0.06f;
    public float tracerWidth = 0.08f;
    public Color tracerColor = Color.yellow;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private CharacterController _controller;
    private Transform _playerTransform;
    private PlayerHealth _playerHealth;
    private CameraShake _cameraShake;
    private float _fireTimer = 0f;
    private bool _canSeePlayer = false;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
            _playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (playerLayer == 0)
            playerLayer = LayerMask.GetMask("Player");
        if (obstacleLayer == 0)
            obstacleLayer = LayerMask.GetMask("Default", "Ground");

        // Create muzzle point if not assigned
        if (muzzlePoint == null)
        {
            GameObject mp = new GameObject("MuzzlePoint");
            mp.transform.SetParent(transform);
            mp.transform.localPosition = new Vector3(0, 1f, 0.6f);
            muzzlePoint = mp.transform;
        }
    }

    private void Update()
    {
        if (_playerTransform == null || _playerHealth == null || _playerHealth.IsDead)
            return;

        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        _canSeePlayer = CanSeePlayer();

        if (distanceToPlayer <= detectionRange && _canSeePlayer)
        {
            // Chase player
            MoveTowardsPlayer();

            // Stop at attack range, face player and shoot
            if (distanceToPlayer <= attackRange)
            {
                FacePlayer();
                ShootAtPlayer();
            }
        }
    }

    private void MoveTowardsPlayer()
    {
        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        direction.y = 0f;

        if (_controller != null)
        {
            _controller.Move(direction * moveSpeed * Time.deltaTime);
        }

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void FacePlayer()
    {
        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        direction.y = 0f;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void ShootAtPlayer()
    {
        _fireTimer += Time.deltaTime;

        if (_fireTimer >= (1f / fireRate))
        {
            FireAtPlayer();
            _fireTimer = 0f;
        }
    }

    private void FireAtPlayer()
    {
        if (_playerTransform == null || muzzlePoint == null) return;

        Vector3 direction = (_playerTransform.position - muzzlePoint.position).normalized;
        Vector3 origin = muzzlePoint.position;

        // Raycast hacia el jugador - detecta si hay algo entre medio
        RaycastHit hit;
        bool hasHit = Physics.Raycast(origin, direction, out hit, attackRange, ~playerLayer);

        Vector3 endPoint = hasHit ? hit.point : origin + direction * attackRange;

        // Show tracer (same as player: LineRenderer, yellow, 0.06s)
        ShowTracer(origin, endPoint);

        // Solo aplicar daño si el raycast impacta DIRECTAMENTE al jugador
        // (si choca con una pared/cubo antes, no hay daño)
        if (hasHit && hit.collider.CompareTag("Player"))
        {
            PlayerHealth health = hit.collider.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.TakeDamage(damagePerShot);
                Debug.Log($"[EnemyAI] {name} shot player for {damagePerShot} damage!");
            }

            // Trigger camera shake on player when enemy successfully hits
            CameraShake camShake = Camera.main?.GetComponent<CameraShake>();
            camShake?.ShakeForDamage();
        }
    }

    private void ShowTracer(Vector3 start, Vector3 end)
    {
        GameObject tracerObj = new GameObject("EnemyBulletTracer");
        LineRenderer lr = tracerObj.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = tracerWidth;
        lr.endWidth = tracerWidth * 0.5f;
        lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lr.material.color = tracerColor;
        lr.startColor = tracerColor;
        lr.endColor = tracerColor;
        lr.enabled = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        Destroy(tracerObj, tracerDuration);
    }

    private bool CanSeePlayer()
    {
        if (_playerTransform == null) return false;

        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, _playerTransform.position);

        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 targetPos = _playerTransform.position + Vector3.up * 1f;

        RaycastHit hit;
        if (Physics.Raycast(eyePos, targetPos - eyePos, out hit, distance, obstacleLayer))
        {
            // Si hay algo entre medio, solo vemos al jugador si el raycast impacta directamente al jugador
            return hit.collider.CompareTag("Player");
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}