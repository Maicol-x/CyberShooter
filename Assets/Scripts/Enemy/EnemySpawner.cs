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
    private int _spawnCounter = 0;

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

        // --- FIX HITBOX ---
        // La raiz se queda con escala uniforme, asi el CharacterController NO se
        // deforma (antes se escalaba la raiz (1,2,1) y Unity escalaba tambien el
        // collider: acababa 1 m por encima del cubo visible).
        // La malla visible es un hijo "Body" de 1x2x1 m, de Y=0 a Y=2.
        GameObject enemy = new GameObject("Enemy");
        _spawnCounter++;
        enemy.name = "Enemy_" + _spawnCounter;
        enemy.transform.position = spawnPos;   // los pies tocan el suelo
        enemy.tag = "Enemy";

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(enemy.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);   // centro del cubo a 1 m
        body.transform.localScale = new Vector3(1f, 2f, 1f);      // 1 x 2 x 1 m

        // El cubo es solo decorativo: el collider real es el CharacterController.
        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null) Destroy(bodyCollider);

        // Material rojo seguro
        var renderer = body.GetComponent<Renderer>();
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

        // Configurar EnemyHealth (SetMaxHealth tambien reinicia la vida actual:
        // Awake ya se ejecuto al hacer AddComponent, asi que hay que resetearla).
        if (health != null) health.SetMaxHealth(enemyMaxHealth);

        // Configurar EnemyAI (moveSpeed lo fija el spawner; el resto de valores son
        // los del Inspector de EnemyAI, asi no se pisan al spawnear).
        if (ai != null)
        {
            ai.moveSpeed = enemyMoveSpeed;
            ai.ApplyHitbox();   // collider = malla visible (center y=1, altura 2)
        }
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
        spawnPos.y = 0f;   // los pies del enemigo apoyan en el suelo (el cubo va de 0 a 2)

        return spawnPos;
    }

    public int GetMaxEnemies() => maxEnemiesAlive;
}