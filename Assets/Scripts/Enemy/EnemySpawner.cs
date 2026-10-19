using UnityEngine;

/// <summary>
/// Genera enemigos a partir de PREFABS configurables (antes creaba cubos rojos por
/// codigo, con el aviso de que la escala deformaba el CharacterController).
///
/// El prefab trae todo: modelo humanoide, Animator, CharacterController, salud e IA.
/// El spawner solo decide CUANDO y DONDE aparece cada uno, y mantiene el cupo.
///
/// Menu relacionado: Tools > CyberShooter > Combat setup (crea el prefab humanoide
/// y lo asigna aqui).
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("Prefabs de enemigo (se elige uno al azar)")]
    [Tooltip("Prefabs humanoides. Si se deja vacio se usa el cubo de emergencia (con aviso en consola).")]
    [SerializeField] private GameObject[] enemyPrefabs;

    [Header("Ritmo de aparicion")]
    [SerializeField] private float spawnIntervalMin = 2f;
    [SerializeField] private float spawnIntervalMax = 4f;
    [SerializeField] private int maxEnemiesAlive = 12;
    [Tooltip("Enemigos que aparecen de golpe al empezar, para que el mapa no este vacio.")]
    [SerializeField, Min(0)] private int initialEnemies = 4;

    [Header("Distancia de aparicion respecto al jugador")]
    [SerializeField] private float spawnRangeMin = 30f;
    [SerializeField] private float spawnRangeMax = 100f;
    [Tooltip("Descarta puntos ocupados (pilares, muros) con una capsula de prueba.")]
    [SerializeField] private bool avoidBlockedPositions = true;

    [Header("Ajustes opcionales del prefab (0 = respetar el prefab)")]
    [SerializeField] private float overrideMoveSpeed = 0f;
    [SerializeField] private int overrideMaxHealth = 0;

    [Header("References")]
    [SerializeField] private Transform playerTransform;

    private float _nextSpawnTime = 0f;
    private int _spawnCounter = 0;

    private void Awake()
    {
        if (playerTransform == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
        {
            Debug.LogWarning("[EnemySpawner] No hay prefabs de enemigo asignados: se usaran cubos de emergencia. " +
                             "Ejecuta 'Tools > CyberShooter > Combat setup' para crear el prefab humanoide.");
        }
    }

    private void Start()
    {
        _nextSpawnTime = Time.time + 1f;

        // Oleada inicial: el mapa no arranca vacio.
        for (int i = 0; i < initialEnemies; i++)
            SpawnEnemy();
    }

    private void Update()
    {
        if (Time.timeScale == 0f) return;
        if (playerTransform == null) return;

        if (Time.time >= _nextSpawnTime)
        {
            if (CountAlive() < maxEnemiesAlive)
                SpawnEnemy();

            _nextSpawnTime = Time.time + Random.Range(spawnIntervalMin, spawnIntervalMax);
        }
    }

    public int GetMaxEnemies() => maxEnemiesAlive;

    private int CountAlive()
    {
        return GameObject.FindGameObjectsWithTag("Enemy").Length;
    }

    private GameObject PickPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return null;

        // Cuenta los no nulos para que un hueco vacio en el array no rompa el reparto.
        int valid = 0;
        for (int i = 0; i < enemyPrefabs.Length; i++)
            if (enemyPrefabs[i] != null) valid++;

        if (valid == 0) return null;

        int target = Random.Range(0, valid);
        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            if (enemyPrefabs[i] == null) continue;
            if (target == 0) return enemyPrefabs[i];
            target--;
        }

        return enemyPrefabs[0];
    }

    private void SpawnEnemy()
    {
        if (playerTransform == null) return;

        Vector3 spawnPos = GetRandomSpawnPosition();
        if (spawnPos == Vector3.positiveInfinity)
        {
            Debug.LogWarning("[EnemySpawner] No encontre un punto libre para aparecer; se omite este spawn.");
            return;
        }

        GameObject prefab = PickPrefab();
        if (prefab == null)
        {
            SpawnEmergencyCube(spawnPos);
            return;
        }

        _spawnCounter++;

        Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject enemy = Instantiate(prefab, spawnPos, rotation);
        enemy.name = prefab.name + "_" + _spawnCounter;

        // Seguridad: tag y capa correctos aunque el prefab venga mal configurado.
        enemy.tag = "Enemy";
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) SetLayerRecursive(enemy, enemyLayer);

        ApplyOptionalOverrides(enemy);
    }

    private void ApplyOptionalOverrides(GameObject enemy)
    {
        if (enemy == null) return;
        if (overrideMoveSpeed <= 0f && overrideMaxHealth <= 0) return;

        var ai = enemy.GetComponent<EnemyAI>();
        if (ai != null && overrideMoveSpeed > 0f) ai.moveSpeed = overrideMoveSpeed;

        var health = enemy.GetComponent<EnemyHealth>();
        if (health != null && overrideMaxHealth > 0) health.SetMaxHealth(overrideMaxHealth);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        Transform[] all = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++) all[i].gameObject.layer = layer;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        if (playerTransform == null) return Vector3.zero;

        float min = Mathf.Min(spawnRangeMin, spawnRangeMax);
        float max = Mathf.Max(spawnRangeMin, spawnRangeMax);

        for (int attempt = 0; attempt < 12; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float distance = Random.Range(min, max);

            Vector3 candidate = playerTransform.position + new Vector3(
                Mathf.Cos(angle) * distance,
                0f,
                Mathf.Sin(angle) * distance
            );
            candidate.y = 0f;   // los pies apoyan en el suelo

            // Coloca los pies sobre el suelo REAL (por si esa zona no esta a y=0).
            RaycastHit groundHit;
            if (Physics.Raycast(candidate + Vector3.up * 6f, Vector3.down, out groundHit, 20f,
                                ~0, QueryTriggerInteraction.Ignore))
            {
                candidate.y = groundHit.point.y;
            }

            if (!avoidBlockedPositions) return candidate;
            if (!IsBlocked(candidate)) return candidate;
        }

        return Vector3.positiveInfinity;
    }

    // Un enemigo ocupa ~1.8 m de alto y ~0.35 de radio: comprobamos que quepa de pie.
    // La capsula empieza por encima del suelo para no contar el propio suelo.
    private bool IsBlocked(Vector3 position)
    {
        Vector3 bottom = position + Vector3.up * 0.7f;
        Vector3 top = position + Vector3.up * 1.6f;
        return Physics.CheckCapsule(bottom, top, 0.45f, ~0, QueryTriggerInteraction.Ignore);
    }

    // ------------------------------------------------------------------
    // Plan B: si no hay prefabs, se mantiene el cubo de siempre para que el
    // spawner siga funcionando (y avisando por consola).
    // ------------------------------------------------------------------
    private void SpawnEmergencyCube(Vector3 spawnPos)
    {
        GameObject enemy = new GameObject("Enemy_Cube");
        _spawnCounter++;
        enemy.transform.position = spawnPos;
        enemy.tag = "Enemy";

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) enemy.layer = enemyLayer;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(enemy.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.transform.localScale = new Vector3(1f, 2f, 1f);

        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null) Destroy(bodyCollider);

        var renderer = body.GetComponent<Renderer>();
        if (renderer != null) renderer.material.color = Color.red;

        var health = enemy.GetComponent<EnemyHealth>();
        if (health == null) health = enemy.AddComponent<EnemyHealth>();

        var ai = enemy.GetComponent<EnemyAI>();
        if (ai == null) ai = enemy.AddComponent<EnemyAI>();
        ai.autoFitHitbox = true;

        var controller = enemy.GetComponent<CharacterController>();
        if (controller == null) controller = enemy.AddComponent<CharacterController>();

        if (controller == null)
        {
            Debug.LogError("[EnemySpawner] Sin CharacterController: spawn cancelado.");
            Destroy(enemy);
            return;
        }

        if (health != null) health.SetMaxHealth(overrideMaxHealth > 0 ? overrideMaxHealth : 30);
        if (ai != null)
        {
            if (overrideMoveSpeed > 0f) ai.moveSpeed = overrideMoveSpeed;
            ai.ApplyHitbox();
        }

        Debug.LogWarning("[EnemySpawner] Spawneado el CUBO de emergencia: falta el prefab humanoide.");
    }
}
