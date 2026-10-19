using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Enemy health system with visual damage feedback.
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 30; // 1. Variable pública maxHealth (por defecto 30)
    private int currentHealth;

    [Header("Events")]
    public UnityEvent OnDeath;

    [Header("Visual Feedback")]
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color flashColor = Color.red;

    private Renderer[] _renderers;
    private MaterialPropertyBlock _propertyBlock;
    private Color[] _originalColors;
    private bool _isFlashing = false;

    private void Awake()
    {
        currentHealth = maxHealth;
        _renderers = GetComponentsInChildren<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();
        CacheOriginalColors();
    }

    private void CacheOriginalColors()
    {
        _originalColors = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            _renderers[i].GetPropertyBlock(_propertyBlock);
            // Fallback to white if property not set
            _originalColors[i] = _renderers[i].sharedMaterial.HasProperty("_BaseColor") 
                ? _renderers[i].sharedMaterial.GetColor("_BaseColor") 
                : Color.white;
        }
    }

    /// <summary>
    /// 2. Método público TakeDamage(int amount) que reste la salud.
    /// </summary>
    /// <summary>
    /// Fija la vida maxima y reinicia la actual.
    /// Necesario porque el spawner configura al enemigo DESPUES de AddComponent,
    /// y el Awake de EnemyHealth ya habia puesto currentHealth = maxHealth.
    /// </summary>
    public void SetMaxHealth(int value)
    {
        maxHealth = Mathf.Max(1, value);
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0) return;

        currentHealth -= amount;

        // 4. Feedback visual: parpadeo rojo con Corrutina (IEnumerator)
        StartCoroutine(FlashRedRoutine());

        // 3. Si la salud llega a 0 o menos, destruye el GameObject
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator FlashRedRoutine()
    {
        if (_isFlashing) yield break;
        _isFlashing = true;

        SetAllRenderersColor(flashColor);
        yield return new WaitForSeconds(flashDuration);

        RestoreOriginalColors();
        _isFlashing = false;
    }

    private void SetAllRenderersColor(Color color)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                _renderers[i].GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", color);
                _renderers[i].SetPropertyBlock(_propertyBlock);
            }
        }
    }

    private void RestoreOriginalColors()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                _renderers[i].GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", _originalColors[i]);
                _renderers[i].SetPropertyBlock(_propertyBlock);
            }
        }
    }

    private void Die()
    {
        Debug.Log("[EnemyHealth] Enemy destroyed!");
        OnDeath?.Invoke();
        Destroy(gameObject);
    }
}