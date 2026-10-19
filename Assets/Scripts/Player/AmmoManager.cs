using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Municion del jugador: cargador + reserva, con recarga temporizada.
///
/// Es la UNICA fuente de verdad: el HUD lee de aqui (no guarda sus propios
/// contadores) para que no puedan desincronizarse.
///
/// Ejemplos de recarga (los del enunciado):
///   10 / 90 -> 30 / 70     (transfiere 20 de la reserva)
///    0 / 12 -> 12 / 0      (la reserva se queda a 0)
///   30 / 90 -> 30 / 90     (no hace nada: cargador lleno)
///    0 /  0 -> no recarga   (no hay nada que meter)
/// </summary>
public class AmmoManager : MonoBehaviour
{
    [Header("Municion")]
    [SerializeField, Min(1)] private int magazineSize = 30;   // capacidad del cargador
    [SerializeField, Min(0)] private int magazine = 30;       // balas en el cargador
    [SerializeField, Min(0)] private int reserve = 90;        // balas de reserva
    [SerializeField, Min(0)] private int maxReserve = 90;     // tope de reserva (municion futura)

    [Header("Recarga")]
    [Tooltip("Duracion de la recarga en segundos. 0 = instantanea. El hueco es para la animacion/sonido.")]
    [SerializeField, Min(0f)] private float reloadTime = 1f;

    [Header("Eventos (HUD, sonidos, animaciones)")]
    public UnityEvent<int, int> OnAmmoChanged;   // cargador, reserva
    public UnityEvent OnReloadStarted;
    public UnityEvent OnReloadFinished;

    private bool _isReloading = false;
    private Coroutine _reloadRoutine = null;

    // ---------- Consultas ----------
    public int Magazine => magazine;
    public int Reserve => reserve;
    public int MagazineSize => magazineSize;
    public int MaxReserve => maxReserve;
    public bool IsReloading => _isReloading;
    public bool IsMagazineEmpty => magazine <= 0;
    public bool IsCompletelyEmpty => magazine <= 0 && reserve <= 0;
    public bool IsMagazineFull => magazine >= magazineSize;
    public bool CanReload => !_isReloading && magazine < magazineSize && reserve > 0;
    public float MagazinePercent => magazineSize <= 0 ? 0f : (float)magazine / magazineSize;

    private void Awake()
    {
        ClampAll();
        NotifyAmmo();
    }

    private void OnValidate()
    {
        magazineSize = Mathf.Max(1, magazineSize);
        ClampAll();
    }

    // Mantiene los valores siempre coherentes: ni negativos, ni cargador por encima
    // de su capacidad, ni reserva por encima de su tope.
    private void ClampAll()
    {
        magazine = Mathf.Clamp(magazine, 0, magazineSize);
        reserve = Mathf.Clamp(reserve, 0, Mathf.Max(0, maxReserve));
    }

    /// <summary>
    /// Gasta una bala para un disparo valido. Devuelve false si no se puede
    /// disparar (cargador vacio o recargando), en cuyo caso NO se gasta nada.
    /// </summary>
    public bool TryConsumeRound()
    {
        if (_isReloading) return false;
        if (magazine <= 0) return false;

        magazine--;
        NotifyAmmo();
        return true;
    }

    /// <summary>
    /// Recarga (tecla R). Si reloadTime es 0 es inmediata; si no, transfiere las
    /// balas al terminar la recarga. Devuelve false si la recarga no tiene sentido
    /// (cargador lleno, sin reserva o ya recargando).
    /// </summary>
    public bool TryReload()
    {
        if (_isReloading) return false;          // evita recargas duplicadas
        if (magazine >= magazineSize) return false;
        if (reserve <= 0) return false;

        if (reloadTime <= 0f)
        {
            CompleteReload();
            return true;
        }

        _isReloading = true;
        OnReloadStarted?.Invoke();
        _reloadRoutine = StartCoroutine(ReloadRoutine());
        return true;
    }

    private IEnumerator ReloadRoutine()
    {
        float elapsed = 0f;
        while (elapsed < reloadTime)
        {
            // deltaTime (escalado): si el juego se congela al morir, la recarga se congela.
            elapsed += Time.deltaTime;
            yield return null;
        }

        _reloadRoutine = null;
        CompleteReload();
    }

    private void CompleteReload()
    {
        int needed = magazineSize - magazine;
        int moved = Mathf.Clamp(needed, 0, reserve);   // nunca mueve mas de lo que hay

        magazine += moved;
        reserve -= moved;
        ClampAll();

        _isReloading = false;
        OnReloadFinished?.Invoke();
        NotifyAmmo();
    }

    /// <summary>Corta la recarga en curso (muerte, desactivacion, cambio de arma).</summary>
    public void CancelReload()
    {
        if (_reloadRoutine != null) StopCoroutine(_reloadRoutine);
        _reloadRoutine = null;
        _isReloading = false;
    }

    /// <summary>Recoge municion (para futuros pickups de balas).</summary>
    public void AddReserve(int amount)
    {
        if (amount <= 0) return;

        reserve = Mathf.Clamp(reserve + amount, 0, Mathf.Max(0, maxReserve));
        NotifyAmmo();
    }

    /// <summary>Reinicio de nivel / reaparicion.</summary>
    public void RefillAll()
    {
        CancelReload();
        magazine = magazineSize;
        reserve = maxReserve;
        NotifyAmmo();
    }

    private void NotifyAmmo()
    {
        OnAmmoChanged?.Invoke(magazine, reserve);
    }

    private void OnDisable()
    {
        CancelReload();
    }
}
