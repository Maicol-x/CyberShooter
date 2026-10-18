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
    public float gravity = -20f;            // gravedad propia: antes flotaban en el aire
    public float groundedStickForce = -2f;  // los mantiene pegados al suelo

    [Header("Detection")]
    public float detectionRange = 30f;
    public LayerMask playerLayer;
    public LayerMask obstacleLayer;

    [Header("Attack")]
    public float attackRange = 12f;   // antes 25 m: ahora se acercan de verdad
    public float aimHeight = 1.0f;    // apunta al pecho del jugador, no a los pies
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
    private float _verticalVelocity = 0f;
    private static Material _sharedTracerMaterial;
    private LineRenderer _tracer;
    private float _tracerHideTime = 0f;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        ApplyHitbox();
    }

    /// <summary>
    /// Ajusta el CharacterController para que coincida EXACTAMENTE con la malla
    /// visible (el cubo "Body"): en mundo queda de Y=0 a Y=2 (center y=1, altura 2).
    /// Es idempotente: se puede llamar al spawnear o desde el Inspector.
    /// </summary>
    public void ApplyHitbox()
    {
        if (_controller == null) _controller = GetComponent<CharacterController>();
        if (_controller == null) return;

        Renderer visual = GetComponentInChildren<Renderer>();
        float worldHeight = 2f;
        float worldRadius = 0.5f;

        if (visual != null && visual.bounds.size.y > 0.01f)
        {
            worldHeight = visual.bounds.size.y;
            worldRadius = Mathf.Min(visual.bounds.size.x, visual.bounds.size.z) * 0.5f;
        }

        Vector3 s = transform.lossyScale;
        float sy = Mathf.Max(0.01f, s.y);
        float sxz = Mathf.Max(0.01f, Mathf.Max(s.x, s.z));

        // El CharacterController escala "center" y "height" con el lossyScale,
        // por eso dividimos aqui: el resultado en mundo mide worldHeight metros.
        float worldCenterY = visual != null
            ? visual.bounds.center.y
            : transform.position.y + worldHeight * 0.5f;

        _controller.height = Mathf.Max(0.2f, worldHeight / sy);
        _controller.radius = Mathf.Max(0.05f, worldRadius / sxz);
        _controller.center = new Vector3(0f, (worldCenterY - transform.position.y) / sy, 0f);
        _controller.slopeLimit = 45f;
        _controller.stepOffset = 0.35f;
    }

    private void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
            _playerHealth = player.GetComponent<PlayerHealth>();
        }

        // Cacheamos la camara: Camera.main es una busqueda por tag y no debe
        // llamarse en cada disparo.
        Camera cam = Camera.main;
        if (cam != null) _cameraShake = cam.GetComponent<CameraShake>();

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
        if (_playerTransform == null || _playerHealth == null) return;

        // Tracer caducado (sin Invoke -> sin basura para el GC)
        if (_tracer != null && _tracer.enabled && Time.time >= _tracerHideTime)
            _tracer.enabled = false;

        if (_playerHealth.IsDead)
        {
            ApplyGravityOnly();
            return;
        }

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
        else
        {
            // Aunque no persiga, la gravedad sigue aplicandose (antes flotaban).
            ApplyGravityOnly();
        }
    }

    private void MoveTowardsPlayer()
    {
        Vector3 direction = _playerTransform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f) direction.Normalize();

        if (_controller != null)
        {
            UpdateVerticalVelocity();
            Vector3 move = direction * moveSpeed;
            move.y = _verticalVelocity;
            _controller.Move(move * Time.deltaTime);
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

        // Apuntamos al PECHO del jugador: su transform esta en los pies, por eso
        // los tiros se iban al suelo o se quedaban cortos.
        Vector3 direction = (GetPlayerAimPoint() - muzzlePoint.position).normalized;
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
            }

            // Camera shake cacheada en Start (Camera.main busca por tag)
            if (_cameraShake != null) _cameraShake.ShakeForDamage();
        }
    }

    private Vector3 GetPlayerAimPoint()
    {
        return _playerTransform.position + Vector3.up * aimHeight;
    }

    private void UpdateVerticalVelocity()
    {
        if (_controller != null && _controller.isGrounded && _verticalVelocity <= 0f)
            _verticalVelocity = groundedStickForce;
        else
            _verticalVelocity += gravity * Time.deltaTime;
    }

    private void ApplyGravityOnly()
    {
        if (_controller == null) return;
        UpdateVerticalVelocity();
        _controller.Move(new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);
    }

    /// <summary>
    /// Un unico LineRenderer por enemigo (reutilizado) + un material COMPARTIDO
    /// para todos. Antes se creaba GameObject + LineRenderer + Material en CADA
    /// disparo: basura para el GC y fuga de materiales.
    /// </summary>
    private void SetupTracer()
    {
        if (_tracer != null) return;

        GameObject tracerObj = new GameObject("EnemyBulletTracer");
        tracerObj.transform.SetParent(transform, false);
        _tracer = tracerObj.AddComponent<LineRenderer>();
        _tracer.positionCount = 2;
        _tracer.startWidth = tracerWidth;
        _tracer.endWidth = tracerWidth * 0.5f;
        _tracer.material = GetSharedTracerMaterial();
        _tracer.startColor = tracerColor;
        _tracer.endColor = tracerColor;
        _tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _tracer.receiveShadows = false;
        _tracer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        _tracer.enabled = false;
    }

    private void ShowTracer(Vector3 start, Vector3 end)
    {
        if (_tracer == null) SetupTracer();
        if (_tracer == null) return;

        _tracer.SetPosition(0, start);
        _tracer.SetPosition(1, end);
        _tracer.enabled = true;
        _tracerHideTime = Time.time + tracerDuration;
    }

    private static Material GetSharedTracerMaterial()
    {
        if (_sharedTracerMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            _sharedTracerMaterial = new Material(shader);
            _sharedTracerMaterial.color = Color.yellow;
            _sharedTracerMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
        return _sharedTracerMaterial;
    }

    private bool CanSeePlayer()
    {
        if (_playerTransform == null) return false;

        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, _playerTransform.position);

        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 targetPos = GetPlayerAimPoint();

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