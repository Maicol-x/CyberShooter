using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles player shooting with raycast and bright neon tracer effect.
/// Damages enemies with "Enemy" tag and triggers camera shake.
/// </summary>
[RequireComponent(typeof(PlayerMotor))]
public class PlayerShooting : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private PlayerMotor playerMotor;

    [Header("Shooting Settings")]
    [SerializeField, Min(0.01f)] private float fireRate = 0.12f; // Faster fire rate
    [SerializeField, Min(1f)] private float maxDistance = 100f;
    [SerializeField] private LayerMask hitLayers = -1;
    [SerializeField] private int damagePerShot = 10;
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private float tracerDuration = 0.06f;
    [SerializeField] private float tracerWidth = 0.12f;  // Thicker tracer
    [SerializeField] private Color tracerColor = Color.yellow; // Neon yellow

    [Header("Input")]
    [SerializeField] private string actionMapName = "Player";
    [SerializeField] private string attackActionName = "Attack";

    private InputAction _attackAction;
    private bool _ownsAttackAction = false;
    private bool _attackActionWarned = false;
    private float _nextFireTime = 0f;
    private LineRenderer _tracerRenderer;
    private CameraShake _cameraShake;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (muzzlePoint == null)
        {
            muzzlePoint = transform.Find("MuzzlePoint");
        }

        if (playerMotor == null)
            playerMotor = GetComponent<PlayerMotor>();

        // Find CameraShake on the camera
        if (playerCamera != null)
            _cameraShake = playerCamera.GetComponent<CameraShake>();

        ResolveAttackAction();
        SetupTracerRenderer();
    }

    private void ResolveAttackAction()
    {
        if (inputActions != null)
        {
            _attackAction = inputActions.FindAction(attackActionName, false)
                          ?? inputActions.FindAction($"Player/{attackActionName}", false);
        }

        if (_attackAction == null)
        {
            _attackAction = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            _ownsAttackAction = true;
        }
    }

    private void SetupTracerRenderer()
    {
        GameObject tracerObj = new GameObject("BulletTracer");
        tracerObj.transform.SetParent(transform);
        _tracerRenderer = tracerObj.AddComponent<LineRenderer>();
        _tracerRenderer.positionCount = 2;
        _tracerRenderer.startWidth = tracerWidth;
        _tracerRenderer.endWidth = tracerWidth * 0.5f; // Tapered end
        _tracerRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        _tracerRenderer.material.color = tracerColor;
        _tracerRenderer.startColor = tracerColor;
        _tracerRenderer.endColor = tracerColor;
        _tracerRenderer.enabled = false;
        _tracerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _tracerRenderer.receiveShadows = false;
        _tracerRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private void OnEnable()
    {
        _attackAction?.Enable();
    }

    private void OnDisable()
    {
        _attackAction?.Disable();

        // Evita que la animacion de disparo se quede en loop si nos desactivan disparando.
        if (playerMotor != null) playerMotor.IsShooting = false;
    }

    private void OnDestroy()
    {
        if (_ownsAttackAction)
            _attackAction?.Dispose();
    }

    private void Update()
    {
        // Clic izquierdo: accion o lectura DIRECTA del raton. Mismo criterio que el
        // WASD en PlayerMotor (y que la camara, que ya lee Mouse.current.delta):
        // en este proyecto la lectura directa del dispositivo es la fiable.
        bool attackPressed = _attackAction != null && _attackAction.IsPressed();
        bool mousePressed = Mouse.current != null && Mouse.current.leftButton.isPressed;

        if (!attackPressed && mousePressed && !_attackActionWarned)
        {
            _attackActionWarned = true;
            Debug.LogWarning("[PlayerShooting] La accion 'Attack' no entrega input; usando Mouse.current directamente.");
        }

        // Mientras se mantiene el clic, el Animator recibe IsShooting = true y la
        // animacion de disparo queda en LOOP. El ritmo real de disparo lo marca fireRate.
        bool shooting = attackPressed || mousePressed;

        if (playerMotor != null)
            playerMotor.IsShooting = shooting;

        if (shooting)
            TryFire();
    }

    private void TryFire()
    {
        if (Time.time < _nextFireTime)
            return;

        _nextFireTime = Time.time + fireRate;
        Fire();
    }

    private void Fire()
    {
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        Ray ray = playerCamera.ScreenPointToRay(screenCenter);

        Vector3 origin = muzzlePoint != null ? muzzlePoint.position : playerCamera.transform.position;
        Vector3 direction = ray.direction;

        RaycastHit hit;
        bool hasHit = Physics.Raycast(origin, direction, out hit, maxDistance, hitLayers, QueryTriggerInteraction.Ignore);

        Vector3 endPoint = hasHit ? hit.point : origin + direction * maxDistance;

        ShowTracer(origin, endPoint);

        // Trigger camera shake for shooting
        _cameraShake?.ShakeForShoot();

        if (hasHit)
        {
            OnHit(hit);
        }
    }

    private void ShowTracer(Vector3 start, Vector3 end)
    {
        if (_tracerRenderer == null) return;

        _tracerRenderer.SetPosition(0, start);
        _tracerRenderer.SetPosition(1, end);
        _tracerRenderer.enabled = true;

        CancelInvoke(nameof(HideTracer));
        Invoke(nameof(HideTracer), tracerDuration);
    }

    private void HideTracer()
    {
        if (_tracerRenderer != null)
            _tracerRenderer.enabled = false;
    }

    private void OnHit(RaycastHit hit)
    {
        if (!hit.collider.CompareTag(enemyTag)) return;

        EnemyHealth enemyHealth = hit.collider.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(damagePerShot);
        }
    }

    private void OnValidate()
    {
        if (fireRate < 0.01f) fireRate = 0.01f;
        if (maxDistance < 1f) maxDistance = 1f;
        if (damagePerShot < 1) damagePerShot = 1;
        if (tracerDuration < 0f) tracerDuration = 0f;
        if (tracerWidth < 0f) tracerWidth = 0f;
    }

    // Phase 7 marker
    private bool _phase7 = true;
}