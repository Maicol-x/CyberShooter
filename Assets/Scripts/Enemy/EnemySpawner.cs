using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private float spawnIntervalMin = 2f;
    [SerializeField] private float spawnIntervalMax = 4f;
    [SerializeField] private int maxEnemiesAlive = 15;
    [SerializeField] private float spawnRange = 200f;

    [Header("Enemy Settings")]
    [SerializeField] private float enemyMoveSpeed = 4f;
    [SerializeField] private int enemyMaxHealth = 30;

    [Header("References")]
    [SerializeField] private Transform playerTransform;

    private float _nextSpawnTime = 0f;

    private void Awake()
    {
        if (playerTransform == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
        }
    }

    private void Start()
    {
        _nextSpawnTime = Time.time + 1f;
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;
        if (playerTransform == null) return;

        if (Time.time >= _nextSpawnTime)
        {
            int enemiesAlive = GameObject.FindGameObjectsWithTag("Enemy").Length;
            if (enemiesAlive < maxEnemiesAlive)
            {
                SpawnEnemy();
            }
            _nextSpawnTime = Time.time + Random.Range(spawnIntervalMin, spawnIntervalMax);
        }
    }

    private void SpawnEnemy()
    {
        if (playerTransform == null) return;

        Vector3 spawnPos = GetRandomSpawnPosition();

        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        enemy.name = $"Enemy_{Time.time:F0}";
        enemy.transform.position = spawnPos;
        enemy.transform.localScale = new Vector3(1f, 2f, 1f);
        enemy.tag = "Enemy";

        // EL FIX: Destruimos el BoxCollider por defecto para que no choque con el CharacterController
        BoxCollider defaultCol = enemy.GetComponent<BoxCollider>();
        if (defaultCol != null) Destroy(defaultCol);

        // Material rojo seguro
        var renderer = enemy.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.red; 
        }

        // Añadir componentes de forma SEGURA.
        // IMPORTANTE: EnemyAI tiene [RequireComponent(typeof(CharacterController))],
        // así que al añadirlo Unity YA crea el CharacterController automáticamente.
        // Por eso usamos GetComponent primero: si volviéramos a llamar AddComponent
        // fallaría y devolvería null -> NullReferenceException en la línea 109.
        var health = enemy.GetComponent<EnemyHealth>();
        if (health == null) health = enemy.AddComponent<EnemyHealth>();

        var ai = enemy.GetComponent<EnemyAI>();
        if (ai == null) ai = enemy.AddComponent<EnemyAI>();

        var controller = enemy.GetComponent<CharacterController>();
        if (controller == null) controller = enemy.AddComponent<CharacterController>();

        if (controller == null)
        {
            Debug.LogError("[EnemySpawner] No se pudo obtener/crear el CharacterController del enemigo. Spawn cancelado.");
            Destroy(enemy);
            return;
        }

        // Configurar EnemyHealth
        if (health != null) health.maxHealth = enemyMaxHealth;

        // Configurar EnemyAI
        if (ai != null)
        {
            ai.moveSpeed = enemyMoveSpeed;
            ai.rotationSpeed = 10f;
            ai.stopDistance = 2f;
            ai.detectionRange = 30f;
            ai.attackRange = 25f;
            ai.fireRate = 1f;
            ai.damagePerShot = 10;
            ai.tracerDuration = 0.06f;
            ai.tracerWidth = 0.08f;
            ai.tracerColor = Color.yellow;
        }

        // Configurar Controller
        controller.radius = 0.5f;
        controller.height = 2f;
        controller.center = new Vector3(0, 1f, 0);
        controller.slopeLimit = 45f;
        controller.stepOffset = 0.3f;
        controller.skinWidth = 0.08f;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        if (playerTransform == null) return Vector3.zero;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(30f, 100f);

        Vector3 offset = new Vector3(
            Mathf.Cos(angle) * distance,
            0f,
            Mathf.Sin(angle) * distance
        );

        Vector3 spawnPos = playerTransform.position + offset;
        spawnPos.y = 1f;

        return spawnPos;
    }

    public int GetMaxEnemies() => maxEnemiesAlive;
}