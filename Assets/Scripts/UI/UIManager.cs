using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// UI del juego: mira, paneles de fin de partida, barra de vida (abajo izquierda),
/// contador de municion (abajo derecha) y flash rojo en los bordes al recibir daño.
///
/// Todo se crea por codigo si no esta asignado en el Inspector, y se alimenta de
/// los sistemas REALES (PlayerHealth y AmmoManager): el HUD no guarda contadores
/// propios, solo pinta lo que le llega por eventos.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Image _crosshairImage;
    [SerializeField] private GameObject _gameOverPanel;
    [SerializeField] private Text _gameOverText;
    [SerializeField] private GameObject _levelCompletePanel;
    [SerializeField] private Text _levelCompleteText;
    [SerializeField] private string _restartHint = "Pulsa R o Enter para reiniciar";

    [Header("HUD - Vida (inferior izquierda)")]
    [SerializeField] private Image _healthBarBackground;
    [SerializeField] private Image _healthBarFill;
    [SerializeField] private Text _healthText;
    [SerializeField] private Color _healthHighColor = new Color(0.25f, 0.9f, 0.3f);
    [SerializeField] private Color _healthMidColor = new Color(0.95f, 0.8f, 0.2f);
    [SerializeField] private Color _healthLowColor = new Color(0.95f, 0.2f, 0.15f);

    [Header("HUD - Municion (inferior derecha)")]
    [SerializeField] private Text _ammoText;
    [SerializeField] private Color _ammoNormalColor = Color.white;
    [SerializeField] private Color _ammoLowColor = new Color(1f, 0.3f, 0.25f);
    [SerializeField] private Color _ammoEmptyColor = new Color(1f, 0.55f, 0.1f);
    [SerializeField] private Color _ammoReloadingColor = new Color(1f, 0.85f, 0.3f);
    [Tooltip("Por debajo de esta municion el contador se pone en rojo.")]
    [SerializeField, Min(1)] private int _lowAmmoThreshold = 5;

    [Header("HUD - Daño en pantalla")]
    [SerializeField] private Image _damageVignette;
    [SerializeField] private Color _damageColor = new Color(0.85f, 0f, 0f, 1f);
    [SerializeField, Range(0f, 1f)] private float _damageFlashIntensity = 0.6f;
    [SerializeField, Min(0.1f)] private float _damageFlashFadeSpeed = 2.5f;

    [Header("Crosshair Settings (Center - Small)")]
    [SerializeField] private Sprite _crosshairSprite;
    [SerializeField] private Color _crosshairColor = Color.white;
    [SerializeField] private float _crosshairSize = 24f;

    private PlayerHealth _playerHealth;
    private AmmoManager _ammoManager;
    private bool _restartArmed = false;
    private float _damageFlash = 0f;
    private Sprite _whiteSprite;

    private void Awake()
    {
        SetupCanvas();
        SetupDamageVignette();
        SetupCrosshair();
        SetupHealthBar();
        SetupAmmoCounter();
        SetupGameOverPanel();
        SetupLevelCompletePanel();
        FindPlayerHealth();
    }

    private void Start()
    {
        // Los eventos de Awake (PlayerHealth y AmmoManager avisan en su propio Awake)
        // pueden haber pasado antes de que nos suscribieramos: aqui se pinta el
        // estado inicial real, sin depender del orden de ejecucion.
        if (_playerHealth != null)
            UpdateHealthUI(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
        else
            UpdateHealthUI(100, 100);

        if (_ammoManager != null)
            UpdateAmmoUI(_ammoManager.Magazine, _ammoManager.Reserve);
    }

    private void SetupCanvas()
    {
        if (_canvas == null)
        {
            _canvas = FindAnyObjectByType<Canvas>();
        }

        if (_canvas == null)
        {
            GameObject canvasObj = new GameObject("UICanvas");
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            _canvas.pixelPerfect = false;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920f, 1080f);   // HUD estable en cualquier resolucion
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            Debug.Log("[UIManager] Created independent Canvas");
        }
        else
        {
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
        }
    }

    private void SetupCrosshair()
    {
        if (_crosshairImage != null) return;

        GameObject crosshairObj = new GameObject("Crosshair");
        crosshairObj.transform.SetParent(_canvas.transform, false);

        _crosshairImage = crosshairObj.AddComponent<Image>();
        _crosshairImage.color = _crosshairColor;
        _crosshairImage.raycastTarget = false;

        if (_crosshairSprite == null)
        {
            _crosshairSprite = CreateCrosshairTexture();
        }

        _crosshairImage.sprite = _crosshairSprite;
        _crosshairImage.preserveAspect = true;

        RectTransform rt = _crosshairImage.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(_crosshairSize, _crosshairSize);

        Debug.Log("[UIManager] Crosshair at CENTER (24px)");
    }

    private Sprite CreateCrosshairTexture()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color transparent = new Color(0, 0, 0, 0);
        Color white = Color.white;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                tex.SetPixel(x, y, transparent);
            }
        }

        int center = size / 2;
        int lineLength = 12;
        int lineThickness = 2;
        int gap = 6;

        // Horizontal line
        for (int x = center - lineLength; x <= center + lineLength; x++)
        {
            for (int y = center - lineThickness / 2; y <= center + lineThickness / 2; y++)
            {
                if (x < center - gap || x > center + gap)
                    tex.SetPixel(x, y, white);
            }
        }

        // Vertical line
        for (int y = center - lineLength; y <= center + lineLength; y++)
        {
            for (int x = center - lineThickness / 2; x <= center + lineThickness / 2; x++)
            {
                if (y < center - gap || y > center + gap)
                    tex.SetPixel(x, y, white);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void SetupGameOverPanel()
    {
        _gameOverPanel = new GameObject("GameOverPanel");
        _gameOverPanel.transform.SetParent(_canvas.transform, false);

        RectTransform panelRect = _gameOverPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = _gameOverPanel.AddComponent<Image>();
        panelBg.color = new Color(0, 0, 0, 0.85f);
        panelBg.raycastTarget = false;

        GameObject textObj = new GameObject("GameOverText");
        textObj.transform.SetParent(_gameOverPanel.transform, false);

        _gameOverText = textObj.AddComponent<Text>();
        _gameOverText.text = "GAME OVER";
        _gameOverText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _gameOverText.fontSize = 72;
        _gameOverText.fontStyle = FontStyle.Bold;
        _gameOverText.color = Color.red;
        _gameOverText.alignment = TextAnchor.MiddleCenter;
        _gameOverText.raycastTarget = false;

        RectTransform textRect = _gameOverText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        CreateHint(_gameOverPanel, _restartHint);

        _gameOverPanel.SetActive(false);

        Debug.Log("[UIManager] Game Over panel created (hidden)");
    }

    private void SetupLevelCompletePanel()
    {
        _levelCompletePanel = new GameObject("LevelCompletePanel");
        _levelCompletePanel.transform.SetParent(_canvas.transform, false);

        RectTransform panelRect = _levelCompletePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;
        panelRect.anchoredPosition = Vector2.zero;

        Image panelBg = _levelCompletePanel.AddComponent<Image>();
        panelBg.color = new Color(0, 0.5f, 0, 0.85f);
        panelBg.raycastTarget = false;

        GameObject textObj = new GameObject("LevelCompleteText");
        textObj.transform.SetParent(_levelCompletePanel.transform, false);

        _levelCompleteText = textObj.AddComponent<Text>();
        _levelCompleteText.text = "NIVEL COMPLETADO";
        _levelCompleteText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _levelCompleteText.fontSize = 72;
        _levelCompleteText.fontStyle = FontStyle.Bold;
        _levelCompleteText.color = Color.green;
        _levelCompleteText.alignment = TextAnchor.MiddleCenter;
        _levelCompleteText.raycastTarget = false;

        RectTransform textRect = _levelCompleteText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        textRect.anchoredPosition = Vector2.zero;

        CreateHint(_levelCompletePanel, _restartHint);

        _levelCompletePanel.SetActive(false);

        Debug.Log("[UIManager] Level Complete panel created (hidden)");
    }

    private void FindPlayerHealth()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[UIManager] Player not found!");
            return;
        }

        _playerHealth = player.GetComponent<PlayerHealth>();
        if (_playerHealth != null)
        {
            _playerHealth.OnDeath.AddListener(ShowGameOver);
            _playerHealth.OnHealthChanged.AddListener(OnPlayerHealthChanged);
            _playerHealth.OnDamaged.AddListener(OnPlayerDamaged);
            Debug.Log("[UIManager] Connected to PlayerHealth (vida, daño y Game Over)");
        }
        else
        {
            Debug.LogWarning("[UIManager] PlayerHealth not found on Player!");
        }

        _ammoManager = player.GetComponent<AmmoManager>();
        if (_ammoManager != null)
        {
            _ammoManager.OnAmmoChanged.AddListener(OnAmmoChanged);
            _ammoManager.OnReloadStarted.AddListener(OnReloadStarted);
            _ammoManager.OnReloadFinished.AddListener(OnReloadFinished);
            Debug.Log("[UIManager] Connected to AmmoManager (municion y recarga)");
        }
        else
        {
            Debug.LogWarning("[UIManager] AmmoManager not found on Player!");
        }
    }

    private void ShowGameOver()
    {
        if (_gameOverPanel != null)
        {
            _gameOverPanel.SetActive(true);
            if (_gameOverText != null)
            {
                _gameOverText.text = "GAME OVER";
                _gameOverText.color = Color.red;
                _gameOverText.fontSize = 72;
                _gameOverText.fontStyle = FontStyle.Bold;
            }
            Debug.Log("[UIManager] GAME OVER shown!");
            FreezeAndArmRestart();
        }
    }

    public void ShowLevelComplete()
    {
        if (_levelCompletePanel != null)
        {
            _levelCompletePanel.SetActive(true);
            if (_levelCompleteText != null)
            {
                _levelCompleteText.text = "NIVEL COMPLETADO";
                _levelCompleteText.color = Color.green;
                _levelCompleteText.fontSize = 72;
                _levelCompleteText.fontStyle = FontStyle.Bold;
            }
            Debug.Log("[UIManager] NIVEL COMPLETADO shown!");
            FreezeAndArmRestart();
        }
    }

    // ---------- Reinicio ----------
    // Congela el tiempo y deja armada la tecla de reinicio. Lo usan la MUERTE y la
    // EXTRACCIÓN: antes la extracción congelaba el tiempo y ahí te quedabas, sin
    // ninguna forma de volver a jugar (había que parar el Play Mode a mano).
    private void FreezeAndArmRestart()
    {
        Time.timeScale = 0f;
        _restartArmed = true;
        Debug.Log("[UIManager] Reinicio armado: pulsa R o Enter para volver a empezar.");
    }

    private void Update()
    {
        // El flash de daño se anima siempre: tambien con el juego congelado (muerte).
        UpdateDamageFlash();

        if (!_restartArmed) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // Con Time.timeScale = 0 Update() sigue corriendo, así que la tecla responde.
        if (kb.rKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
        {
            RestartLevel();
        }
    }

    public void RestartLevel()
    {
        if (!_restartArmed) return;

        _restartArmed = false;

        // LoadScene NO resetea Time.timeScale: si no lo devolvemos a 1, el nivel
        // reiniciado arrancaría congelado (que es justo el bug que había).
        Time.timeScale = 1f;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
        {
            Debug.Log($"[UIManager] Reiniciando '{scene.name}' (buildIndex {scene.buildIndex})");
            SceneManager.LoadScene(scene.buildIndex);
        }
        else
        {
            // Escena no incluida en Build Settings pero abierta en el editor.
            Debug.Log($"[UIManager] Reiniciando '{scene.name}' por nombre");
            SceneManager.LoadScene(scene.name);
        }
    }

    // Texto pequeño bajo el título con la tecla de reinicio.
    private Text CreateHint(GameObject panel, string text)
    {
        GameObject hintObj = new GameObject("RestartHint");
        hintObj.transform.SetParent(panel.transform, false);

        Text hint = hintObj.AddComponent<Text>();
        hint.text = text;
        hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hint.fontSize = 28;
        hint.fontStyle = FontStyle.Normal;
        hint.color = Color.white;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.raycastTarget = false;

        RectTransform rt = hint.rectTransform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 80f);
        rt.anchoredPosition = new Vector2(0f, -70f);

        return hint;
    }

    // ==================================================================
    //  HUD: barra de vida, municion y flash de daño
    // ==================================================================

    private Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;

        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        return _whiteSprite;
    }

    private void SetupHealthBar()
    {
        if (_healthBarBackground != null && _healthBarFill != null) return;   // ya asignada en el Inspector

        GameObject container = new GameObject("HealthBar");
        container.transform.SetParent(_canvas.transform, false);

        RectTransform rect = container.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(36f, 36f);
        rect.sizeDelta = new Vector2(420f, 34f);

        // Fondo de la barra.
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(container.transform, false);
        _healthBarBackground = bgObj.AddComponent<Image>();
        _healthBarBackground.sprite = GetWhiteSprite();
        _healthBarBackground.color = new Color(0f, 0f, 0f, 0.65f);
        _healthBarBackground.raycastTarget = false;
        StretchToParent(_healthBarBackground.rectTransform, 0f);

        // Relleno proporcional a la vida.
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(container.transform, false);
        _healthBarFill = fillObj.AddComponent<Image>();
        _healthBarFill.sprite = GetWhiteSprite();
        _healthBarFill.color = _healthHighColor;
        _healthBarFill.type = Image.Type.Filled;
        _healthBarFill.fillMethod = Image.FillMethod.Horizontal;
        _healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _healthBarFill.fillAmount = 1f;
        _healthBarFill.raycastTarget = false;
        StretchToParent(_healthBarFill.rectTransform, 4f);

        // Indicador numerico.
        GameObject textObj = new GameObject("HealthText");
        textObj.transform.SetParent(_canvas.transform, false);
        _healthText = textObj.AddComponent<Text>();
        _healthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _healthText.fontSize = 26;
        _healthText.fontStyle = FontStyle.Bold;
        _healthText.color = Color.white;
        _healthText.alignment = TextAnchor.LowerLeft;
        _healthText.text = "100 / 100";
        _healthText.raycastTarget = false;

        RectTransform textRect = _healthText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(0f, 0f);
        textRect.pivot = new Vector2(0f, 0f);
        textRect.anchoredPosition = new Vector2(36f, 76f);
        textRect.sizeDelta = new Vector2(420f, 34f);

        Debug.Log("[UIManager] Barra de vida creada (inferior izquierda)");
    }

    private void SetupAmmoCounter()
    {
        if (_ammoText != null) return;

        GameObject textObj = new GameObject("AmmoText");
        textObj.transform.SetParent(_canvas.transform, false);

        _ammoText = textObj.AddComponent<Text>();
        _ammoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _ammoText.fontSize = 32;
        _ammoText.fontStyle = FontStyle.Bold;
        _ammoText.color = _ammoNormalColor;
        _ammoText.alignment = TextAnchor.LowerRight;
        _ammoText.text = "BALAS: 30 / 90";
        _ammoText.raycastTarget = false;

        RectTransform rect = _ammoText.rectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-36f, 36f);
        rect.sizeDelta = new Vector2(600f, 44f);

        Debug.Log("[UIManager] Contador de municion creado (inferior derecha)");
    }

    // Vignette: sprite generado (transparente en el centro, opaco en los bordes).
    private void SetupDamageVignette()
    {
        if (_damageVignette != null) return;

        GameObject obj = new GameObject("DamageVignette");
        obj.transform.SetParent(_canvas.transform, false);

        _damageVignette = obj.AddComponent<Image>();
        _damageVignette.sprite = CreateVignetteSprite();
        _damageVignette.color = new Color(_damageColor.r, _damageColor.g, _damageColor.b, 0f);
        _damageVignette.raycastTarget = false;
        StretchToParent(_damageVignette.rectTransform, 0f);

        // Detras del resto del HUD (el canvas pinta por orden de hermanos).
        _damageVignette.transform.SetAsFirstSibling();

        Debug.Log("[UIManager] Efecto de daño creado (vineta roja)");
    }

    private Sprite CreateVignetteSprite(int size = 256)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxDistance = size * 0.5f;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / maxDistance;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, d));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static void StretchToParent(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    // ---------- Vida ----------
    private void OnPlayerHealthChanged(int current, int max)
    {
        UpdateHealthUI(current, max);
    }

    private void UpdateHealthUI(int current, int max)
    {
        float ratio = max <= 0 ? 0f : Mathf.Clamp01((float)current / max);

        if (_healthBarFill != null)
        {
            _healthBarFill.fillAmount = ratio;
            _healthBarFill.color = ratio > 0.5f ? _healthHighColor
                                 : ratio > 0.25f ? _healthMidColor
                                 : _healthLowColor;
        }

        if (_healthText != null)
            _healthText.text = current + " / " + max;
    }

    // ---------- Municion ----------
    private void OnAmmoChanged(int magazine, int reserve)
    {
        UpdateAmmoUI(magazine, reserve);
    }

    private void OnReloadStarted()
    {
        if (_ammoManager != null) UpdateAmmoUI(_ammoManager.Magazine, _ammoManager.Reserve);
    }

    private void OnReloadFinished()
    {
        if (_ammoManager != null) UpdateAmmoUI(_ammoManager.Magazine, _ammoManager.Reserve);
    }

    private void UpdateAmmoUI(int magazine, int reserve)
    {
        if (_ammoText == null) return;

        bool reloading = _ammoManager != null && _ammoManager.IsReloading;

        string text = "BALAS: " + magazine + " / " + reserve;
        Color color = _ammoNormalColor;

        if (reloading)
        {
            text += "   RECARGANDO...";
            color = _ammoReloadingColor;
        }
        else if (magazine <= 0)
        {
            // Cargador vacio: bien visible.
            text += reserve > 0 ? "   SIN BALAS - PULSA R" : "   SIN MUNICION";
            color = _ammoEmptyColor;
        }
        else if (magazine < _lowAmmoThreshold)
        {
            color = _ammoLowColor;
        }

        _ammoText.text = text;
        _ammoText.color = color;
    }

    // ---------- Daño en pantalla ----------
    private void OnPlayerDamaged()
    {
        // Impactos sucesivos: se reinicia al maximo (no se acumula ni parpadea raro).
        _damageFlash = 1f;
    }

    private void UpdateDamageFlash()
    {
        if (_damageVignette == null) return;

        if (_damageFlash > 0f)
            _damageFlash = Mathf.Max(0f, _damageFlash - Time.unscaledDeltaTime * _damageFlashFadeSpeed);

        Color color = _damageColor;
        color.a = _damageFlashIntensity * _damageFlash;
        _damageVignette.color = color;
    }

    private void OnDestroy()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnDeath.RemoveListener(ShowGameOver);
            _playerHealth.OnHealthChanged.RemoveListener(OnPlayerHealthChanged);
            _playerHealth.OnDamaged.RemoveListener(OnPlayerDamaged);
        }

        if (_ammoManager != null)
        {
            _ammoManager.OnAmmoChanged.RemoveListener(OnAmmoChanged);
            _ammoManager.OnReloadStarted.RemoveListener(OnReloadStarted);
            _ammoManager.OnReloadFinished.RemoveListener(OnReloadFinished);
        }
    }
}