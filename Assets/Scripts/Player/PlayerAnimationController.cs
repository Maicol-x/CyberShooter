using UnityEngine;

/// <summary>
/// Controls the Animator parameters based on player state.
/// Attach this to the Player GameObject alongside PlayerMotor.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerMotor))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animation Parameters")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string isGroundedParam = "IsGrounded";
    [SerializeField] private string isSprintingParam = "IsSprinting";
    [SerializeField] private string jumpTrigger = "Jump";
    [SerializeField] private string slideTrigger = "Barrida";
    [SerializeField] private string isShootingParam = "IsShooting";
    [SerializeField] private string landTrigger = "Land";
    [SerializeField] private string shootTrigger = "Shoot";
    [SerializeField] private string dashTrigger = "Dash";

    [Header("Settings")]
    [SerializeField] private float speedDampTime = 0.1f;
    [SerializeField] private float groundedThreshold = 0.1f;

    private Animator _animator;
    private PlayerMotor _motor;
    private CharacterController _controller;
    private float _speedVelocity;
    private bool _wasDashing;

    private void Awake()
    {
        // El Animator "de verdad" (el que tiene el AnimatorController) está en el hijo
        // (Ch44), no en Player12. Buscamos en Player12 y en TODOS sus hijos y nos
        // quedamos con el primero que tenga un RuntimeAnimatorController asignado.
        _animator = FindAnimator();

        _motor = GetComponent<PlayerMotor>();
        _controller = GetComponent<CharacterController>();

        if (_animator == null)
            Debug.LogWarning("[PlayerAnimationController] No se encontró ningún Animator en Player12 ni en sus hijos.");
    }

    // Busca el Animator en el objeto y en sus hijos, priorizando el que tenga
    // un RuntimeAnimatorController (el que realmente reproduce animaciones).
    private Animator FindAnimator()
    {
        Animator[] candidates = GetComponentsInChildren<Animator>(true);
        Animator firstWithoutController = null;

        foreach (Animator a in candidates)
        {
            if (a == null) continue;
            if (a.runtimeAnimatorController != null)
                return a;
            if (firstWithoutController == null)
                firstWithoutController = a;
        }

        return firstWithoutController;
    }

    private void OnEnable()
    {
        if (_motor != null)
        {
            _motor.OnJump += HandleJumpEvent;
            _motor.OnSlide += HandleSlideEvent;
        }
    }

    private void OnDisable()
    {
        if (_motor != null)
        {
            _motor.OnJump -= HandleJumpEvent;
            _motor.OnSlide -= HandleSlideEvent;
        }
    }

    // El salto se recibe por EVENTO. Antes se sondeaba _motor.JumpRequested, pero
    // ese flag se pone a true y se consume a false dentro del mismo Update del
    // motor, asi que este script nunca llegaba a verlo: la animacion de salto
    // NUNCA se disparaba.
    private void HandleJumpEvent()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(jumpTrigger);
        }
    }

    // Barrida (tecla C): PlayerMotor avisa cuando arranca el deslizamiento.
    private void HandleSlideEvent()
    {
        if (_animator != null && !string.IsNullOrEmpty(slideTrigger))
        {
            _animator.SetTrigger(slideTrigger);
        }
    }

    private void Update()
    {
        if (_animator == null || _motor == null) return;

        // Velocidad SOLO en el plano horizontal (X y Z): ignoramos la gravedad (Y)
        // para que el parámetro "Speed" no se vea afectado por caídas ni saltos.
        // Nota: el Animator espera velocidad REAL (su valor por defecto es 5), no 0-1.
        Vector3 horizontalVelocity = _controller != null
            ? new Vector3(_controller.velocity.x, 0f, _controller.velocity.z)
            : new Vector3(_motor.Velocity.x, 0f, _motor.Velocity.z);
        float horizontalSpeed = horizontalVelocity.magnitude;

        _animator.SetFloat(speedParam, horizontalSpeed, speedDampTime, Time.deltaTime);

        // Grounded state
        bool isGrounded = _motor.IsGrounded;
        _animator.SetBool(isGroundedParam, isGrounded);

        // Sprint state
        _animator.SetBool(isSprintingParam, _motor.IsSprinting && _motor.HasMovementInput);

        // Disparo: bool en LOOP mientras se mantiene el clic (lo levanta PlayerShooting).
        _animator.SetBool(isShootingParam, _motor.IsShooting);

        // NOTA: el trigger de salto ya no se sondea aqui, llega por el evento
        // OnJump (ver HandleJumpEvent / OnEnable).

        // Shoot trigger: se CONSUME el flag (se pone a false) para que NO se dispare
        // en cada frame. Solo se anima cuando PlayerShooting lo levanta (1 vez por clic).
        if (_motor.ShootRequested)
        {
            _animator.SetTrigger(shootTrigger);
            _motor.ShootRequested = false;
        }

        // Dash trigger: solo en el instante en que EMPIEZA el dash (flanco de subida),
        // para que la animación se dispare una sola vez por dash.
        if (_motor.IsDashing && !_wasDashing)
        {
            _animator.SetTrigger(dashTrigger);
        }
        _wasDashing = _motor.IsDashing;
    }

    // Public method to trigger land animation (call from PlayerMotor when landing)
    public void TriggerLand()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(landTrigger);
        }
    }

    // Public method to trigger shoot animation
    public void TriggerShoot()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(shootTrigger);
        }
    }
}