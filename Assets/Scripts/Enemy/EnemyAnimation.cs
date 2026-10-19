using UnityEngine;

/// <summary>
/// Da vida a los enemigos humanoides: lee la velocidad REAL del CharacterController
/// y la pasa al Animator. Sin esto el modelo Mixamo se queda en Idle y "patina" por
/// el mapa mientras la IA lo mueve.
///
/// Reutiliza el mismo AnimatorController humanoid que el jugador (Playeranimator):
/// dentro de el, Idle/walking/Running se eligen con Speed + IsSprinting, asi que
/// basta con rellenar esos parametros. Los parametros que el controller no tenga
/// no se tocan (Unity no avisa por parametros inexistentes si no los asignas).
/// </summary>
[RequireComponent(typeof(Animator))]
public class EnemyAnimation : MonoBehaviour
{
    [SerializeField] private CharacterController controller;
    [Tooltip("Por encima de esta velocidad se considera que corre (Running).")]
    [SerializeField] private float sprintThreshold = 3.5f;
    [SerializeField] private bool debugLog = false;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int SprintHash = Animator.StringToHash("IsSprinting");
    private static readonly int GroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int ShootingHash = Animator.StringToHash("IsShooting");

    private Animator _animator;
    private bool _hasSpeed, _hasSprint, _hasGrounded, _hasShooting;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (controller == null) controller = GetComponent<CharacterController>();

        // Se mira una sola vez: si el controller no tiene el parametro, no se
        // intenta asignar cada frame (evita avisos repetidos por consola).
        AnimatorControllerParameter[] ps = _animator.parameters;
        for (int i = 0; i < ps.Length; i++)
        {
            if (ps[i].nameHash == SpeedHash) _hasSpeed = true;
            else if (ps[i].nameHash == SprintHash) _hasSprint = true;
            else if (ps[i].nameHash == GroundedHash) _hasGrounded = true;
            else if (ps[i].nameHash == ShootingHash) _hasShooting = true;
        }

        if (debugLog)
        {
            Debug.Log("[EnemyAnimation] parametros -> Speed:" + _hasSpeed + " Sprint:" + _hasSprint +
                      " Grounded:" + _hasGrounded + " Shooting:" + _hasShooting);
        }
    }

    private void Update()
    {
        if (_animator == null || controller == null) return;

        // Velocidad horizontal real del enemigo (la que le da la IA).
        Vector3 velocity = controller.velocity;
        velocity.y = 0f;
        float speed = velocity.magnitude;

        if (_hasSpeed) _animator.SetFloat(SpeedHash, speed, 0.1f, Time.deltaTime);
        if (_hasSprint) _animator.SetBool(SprintHash, speed > sprintThreshold);
        if (_hasGrounded) _animator.SetBool(GroundedHash, controller.isGrounded);
        if (_hasShooting) _animator.SetBool(ShootingHash, false);   // los enemigos no usan la animacion de disparo del jugador
    }
}
