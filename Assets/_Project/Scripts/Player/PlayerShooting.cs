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
    [SerializeField] private AmmoManager ammo;

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
    [Tooltip("Tecla de recarga. El asset de input del proyecto no tiene accion 'Reload', asi que se lee el teclado directamente (mismo criterio que el WASD y el clic).")]
    [SerializeField] private Key reloadKey = Key.R;

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
            muzzlePoint = ResolveMuzzlePoint();
        }

        if (playerMotor == null)
            playerMotor = GetComponent<PlayerMotor>();

        if (ammo == null)
            ammo = GetComponent<AmmoManager>();

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
            // actionMapName estaba sin usar (CS0414): ahora es lo que indica el mapa.
            _attackAction = inputActions.FindAction(attackActionName, false);

            if (_attackAction == null && !string.IsNullOrEmpty(actionMapName))
                _attackAction = inputActions.FindAction($"{actionMapName}/{attackActionName}", false);
        }

        if (_attackAction == null)
        {
            _attackAction = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            _ownsAttackAction = true;
        }
    }

    // Punto de salida de la trazadora. No hay un objeto "MuzzlePoint" ni modelo de
    // arma, así que usamos la MANO DERECHA del esqueleto (Mixamo/Ch44), que es donde
    // iría el arma: así la trazadora nace del personaje y no de la cámara.
    // Si no encontramos ninguna mano, devolvemos null y se usa la cámara.
    private Transform ResolveMuzzlePoint()
    {
        Transform byName = transform.Find("MuzzlePoint");
        if (byName != null) return byName;

        string[] claves = { "righthand", "hand_r", "right_hand", "muzzle" };
        Transform[] all = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;

            string nombre = all[i].name.ToLowerInvariant();
            for (int k = 0; k < claves.Length; k++)
            {
                if (nombre.Contains(claves[k]))
                {
                    Debug.Log($"[PlayerShooting] MuzzlePoint: usando el hueso '{all[i].name}' (no existía el objeto MuzzlePoint).");
                    return all[i];
                }
            }
        }

        return null;
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
        // El asset entero antes que la accion (mismo caso que en PlayerMotor): una
        // accion dentro de un mapa deshabilitado no recibe input y devuelve 0.
        inputActions?.Enable();

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
        HandleReloadInput();

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

    // R (configurable en el Inspector) para recargar.
    private void HandleReloadInput()
    {
        if (ammo == null) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        var key = kb[reloadKey];
        if (key != null && key.wasPressedThisFrame)
            TryReload();
    }

    /// <summary>Recarga. Publico para poder llamarlo desde un boton de UI o un test.</summary>
    public bool TryReload()
    {
        if (ammo == null) return false;

        bool started = ammo.TryReload();
        if (started)
            Debug.Log("[PlayerShooting] Recargando... cargador " + ammo.Magazine + " / reserva " + ammo.Reserve);
        else if (ammo.IsMagazineFull)
            Debug.Log("[PlayerShooting] El cargador ya esta lleno.");
        else if (ammo.Reserve <= 0)
            Debug.Log("[PlayerShooting] No queda reserva para recargar.");

        return started;
    }

    private void TryFire()
    {
        if (Time.time < _nextFireTime)
            return;

        // Recargando no se dispara.
        if (ammo != null && ammo.IsReloading)
            return;

        // Cada disparo VALIDO gasta exactamente una bala. Si no hay balas, no se
        // genera trazadora, ni daño, ni sacudida de camara (el disparo no existe).
        if (ammo != null && !ammo.TryConsumeRound())
            return;

        _nextFireTime = Time.time + fireRate;
        Fire();
    }

    private void Fire()
    {
        if (playerCamera == null) return;

        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        Ray ray = playerCamera.ScreenPointToRay(screenCenter);

        // El impacto se calcula DESDE LA CÁMARA (así coincide con la mira) y la
        // trazadora se dibuja DESDE EL ARMA hasta el punto de impacto. Antes se
        // lanzaba el rayo desde el muzzle, con lo que el disparo se desviaba.
        Vector3 origin = playerCamera.transform.position;
        Vector3 direction = ray.direction;

        RaycastHit hit;
        Vector3 endPoint = EvaluateShot(origin, direction, out hit);

        Vector3 tracerStart = muzzlePoint != null ? muzzlePoint.position : origin;
        ShowTracer(tracerStart, endPoint);

        // Trigger camera shake for shooting
        _cameraShake?.ShakeForShoot();

        if (hit.collider != null)
        {
            OnHit(hit);
        }
    }

    // Devuelve el punto final visible del disparo y, si toca algo, el impacto.
    // Ignora los colliders del propio jugador: en tercera persona la cámara está
    // detrás del personaje y el rayo le daba a la espalda (no se podía acertar).
    private Vector3 EvaluateShot(Vector3 origin, Vector3 direction, out RaycastHit hit)
    {
        hit = default(RaycastHit);

        RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxDistance, hitLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return origin + direction * maxDistance;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null) continue;
            if (hits[i].collider.transform.root == transform.root) continue;   // soy yo

            hit = hits[i];
            return hit.point;
        }

        return origin + direction * maxDistance;
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
}