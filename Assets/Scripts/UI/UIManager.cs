using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// UI Manager simplificado - Solo Crosshair y Game Over
/// Barra de vida REMOVIDA por petición del usuario.
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

    [Header("Crosshair Settings (Center - Small)")]
    [SerializeField] private Sprite _crosshairSprite;
    [SerializeField] private Color _crosshairColor = Color.white;
    [SerializeField] private float _crosshairSize = 24f;

    private PlayerHealth _playerHealth;
    private bool _restartArmed = false;

    private void Awake()
    {
        SetupCanvas();
        SetupCrosshair();
        SetupGameOverPanel();
        SetupLevelCompletePanel();
        FindPlayerHealth();
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
        if (player != null)
        {
            _playerHealth = player.GetComponent<PlayerHealth>();
            if (_playerHealth != null)
            {
                _playerHealth.OnDeath.AddListener(ShowGameOver);
                Debug.Log("[UIManager] Connected to PlayerHealth for Game Over");
            }
            else
            {
                Debug.LogWarning("[UIManager] PlayerHealth not found on Player!");
            }
        }
        else
        {
            Debug.LogWarning("[UIManager] Player not found!");
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

    private void OnDestroy()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnDeath.RemoveListener(ShowGameOver);
        }
    }
}