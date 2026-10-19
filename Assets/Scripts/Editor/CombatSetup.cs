using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CyberShooter.EditorTools
{
    /// <summary>
    /// Monta el ciclo de combate completo sobre la escena abierta, de forma
    /// REPETIBLE (se puede lanzar tantas veces como quieras; rehace lo suyo y
    /// no toca lo demas):
    ///
    ///   1. Municion: AmmoManager en el jugador (30 / 90, cargador de 30).
    ///   2. Arma 3D: construye un rifle (primitivas + material propio), lo guarda
    ///      como prefab en Assets/Prefabs/Rifle.prefab y lo ancla a la MANO DERECHA
    ///      del esqueleto, con un MuzzlePoint en la punta del cañon conectado a
    ///      PlayerShooting.
    ///   3. Enemigo: crea Assets/Prefabs/EnemyHumanoid.prefab con el modelo Mixamo
    ///      (rig Humanoid), su Animator (Idle/Running del controller del jugador),
    ///      CharacterController, EnemyHealth, EnemyAI y EnemyAnimation.
    ///   4. Curacion: Assets/Prefabs/HealthPickup.prefab (cubo verde, trigger, +20)
    ///      y varios repartidos por el mapa.
    ///   5. Fuera los cubos rojos: borra los enemigos-cubo de la escena y deja al
    ///      EnemySpawner generando el prefab humanoide (oleada inicial 4, tope 12).
    ///
    /// Menu: Tools > CyberShooter > Combat setup
    ///
    /// NOTA sobre el arma: en el proyecto NO hay carpeta "Guns & Explosives" ni
    /// ningun modelo de arma, asi que el rifle se construye con primitivas. Si se
    /// importa un modelo real, basta con sustituir el contenido de Assets/Prefabs/Rifle.prefab
    /// manteniendo un hijo llamado "MuzzlePoint" en la punta.
    /// </summary>
    public static class CombatSetup
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string MaterialFolder = "Assets/Materials";

        private const string RiflePrefabPath = PrefabFolder + "/Rifle.prefab";
        private const string EnemyPrefabPath = PrefabFolder + "/EnemyHumanoid.prefab";
        private const string PickupPrefabPath = PrefabFolder + "/HealthPickup.prefab";

        private const string ModelPath = "Assets/Models/Ch44_nonPBR@Breathing Idle.fbx";
        private const string PlayerControllerPath = "Assets/Models/Playeranimator.controller";

        private const string WeaponMatPath = MaterialFolder + "/Weapon.mat";
        private const string EnemyMatPath = MaterialFolder + "/EnemyHumanoid.mat";
        private const string PickupMatPath = MaterialFolder + "/HealthPickup.mat";

        // --- Ajuste fino del arma en la mano ---
        // El rifle se modela mirando a +Z y la mano de Mixamo apunta a +Y (los dedos),
        // por eso el -90 en X: alinea el cañon con los dedos. Si al probarlo se ve
        // torcido, el tercer valor (roll) es el que hay que mover.
        private static readonly Vector3 RifleLocalPosition = new Vector3(0f, 0.055f, 0.01f);
        private static readonly Vector3 RifleLocalEuler = new Vector3(-90f, 0f, 0f);

        private const int EnemyMaxHealth = 30;
        private const int MaxEnemiesAlive = 12;
        private const int InitialEnemies = 4;

        private static readonly Vector3[] PickupPositions =
        {
            new Vector3(-12f, 0.7f, 21f),
            new Vector3(6f, 0.7f, 26f),
            new Vector3(-7f, 0.7f, 33f),
            new Vector3(14f, 0.7f, 30f),
            new Vector3(9f, 0.7f, 41f),
            new Vector3(-5f, 0.7f, 49f),
            new Vector3(4f, 0.7f, 57f),
            new Vector3(0f, 0.7f, 64f),
        };

        [MenuItem("Tools/CyberShooter/Combat setup")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[CombatSetup] No se toca la escena en Play Mode. Para el juego primero.");
                return;
            }

            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            var report = new System.Text.StringBuilder();
            report.AppendLine("[CombatSetup] === Ciclo de combate ===");

            Material weaponMat = GetOrCreateMaterial(WeaponMatPath, new Color(0.10f, 0.10f, 0.12f), 0.85f, 0.55f);
            Material enemyMat = GetOrCreateMaterial(EnemyMatPath, new Color(0.62f, 0.10f, 0.10f), 0.30f, 0.40f);
            Material pickupMat = GetOrCreateMaterial(PickupMatPath, new Color(0.15f, 0.95f, 0.25f), 0f, 0.65f, new Color(0.1f, 0.55f, 0.15f));

            GameObject player = FindPlayer();
            if (player == null)
            {
                report.AppendLine("  ERROR: no encuentro al jugador (tag Player / Player12). Abortado.");
                Debug.LogError(Flatten(report));
                return;
            }
            report.AppendLine("  Jugador: " + player.name);

            // ---------- 1) Municion ----------
            AmmoManager ammo = player.GetComponent<AmmoManager>();
            if (ammo == null) ammo = player.AddComponent<AmmoManager>();
            EditorUtility.SetDirty(player);
            report.AppendLine("  AmmoManager: cargador 30, reserva 90 (tope 90)");

            // ---------- 2) Arma ----------
            GameObject rifle = AttachRifleToHand(player, weaponMat, report);

            PlayerShooting shooting = player.GetComponent<PlayerShooting>();
            if (shooting != null)
            {
                SerializedObject so = new SerializedObject(shooting);

                var muzzleProp = so.FindProperty("muzzlePoint");
                if (muzzleProp != null)
                {
                    Transform muzzle = rifle != null ? FindChildRecursive(rifle.transform, "MuzzlePoint") : null;
                    muzzleProp.objectReferenceValue = muzzle;
                    report.AppendLine("  PlayerShooting.muzzlePoint -> " + (muzzle == null ? "sin arma" : muzzle.name));
                }

                var ammoProp = so.FindProperty("ammo");
                if (ammoProp != null)
                {
                    ammoProp.objectReferenceValue = ammo;
                    report.AppendLine("  PlayerShooting.ammo -> AmmoManager");
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(shooting);
            }
            else
            {
                report.AppendLine("  AVISO: el jugador no tiene PlayerShooting");
            }

            // ---------- 3) Prefab de enemigo humanoide ----------
            GameObject enemyPrefab = BuildEnemyPrefab(enemyMat, report);

            // ---------- 4) Objetos curativos ----------
            PlacePickups(pickupMat, report);

            // ---------- 5) Cubos fuera y spawner con el prefab ----------
            if (enemyPrefab != null) ReplaceCubeEnemies(enemyPrefab, report);

            // ---------- Guardar ----------
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            report.AppendLine("  escena '" + scene.name + "' guardada = " + saved);

            // En una sola linea: la consola se lee entera de un vistazo (y la
            // herramienta externa no la corta al primer salto).
            Debug.Log(Flatten(report));
        }

        private static string Flatten(System.Text.StringBuilder report)
        {
            return report.ToString().Replace("\r\n", " | ").Replace("\n", " | ");
        }

        // ==================================================================
        //  Jugador / arma
        // ==================================================================

        private static GameObject FindPlayer()
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player == null) player = GameObject.Find("Player12");
            return player;
        }

        private static GameObject AttachRifleToHand(GameObject player, Material weaponMat, System.Text.StringBuilder report)
        {
            Transform hand = FindRightHand(player);
            if (hand == null)
            {
                report.AppendLine("  AVISO: no encontre la mano derecha (mixamorig:RightHand).");
                return null;
            }

            // Si ya habia un arma, se rehace (asi el setup es repetible).
            Transform existing = FindChildRecursive(hand, "Rifle");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                report.AppendLine("  (arma anterior descartada)");
            }

            // Construir -> guardar como prefab -> usar una instancia.
            GameObject built = BuildRifle(weaponMat);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(built, RiflePrefabPath);
            Object.DestroyImmediate(built);

            if (prefab == null)
            {
                report.AppendLine("  ERROR: no se pudo guardar " + RiflePrefabPath);
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Rifle";
            instance.transform.SetParent(hand, false);
            instance.transform.localPosition = RifleLocalPosition;
            instance.transform.localRotation = Quaternion.Euler(RifleLocalEuler);
            instance.transform.localScale = Vector3.one;
            EditorUtility.SetDirty(instance);

            report.AppendLine("  Arma: " + RiflePrefabPath + " instanciada en la mano '" + hand.name + "'");
            return instance;
        }

        // Rifle de primitivas. El origen esta en la EMPUÑADURA (asi la mano lo
        // agarra de forma natural) y el cañon apunta a +Z. Largo total ~0.85 m.
        private static GameObject BuildRifle(Material weaponMat)
        {
            GameObject rifle = new GameObject("Rifle");

            AddPart(rifle, "Receiver", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.12f), new Vector3(0.055f, 0.075f, 0.32f), Vector3.zero, weaponMat);
            AddPart(rifle, "Stock", PrimitiveType.Cube, new Vector3(0f, 0.005f, -0.13f), new Vector3(0.045f, 0.06f, 0.20f), Vector3.zero, weaponMat);
            AddPart(rifle, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.075f, 0.03f), new Vector3(0.04f, 0.10f, 0.05f), new Vector3(15f, 0f, 0f), weaponMat);
            AddPart(rifle, "Magazine", PrimitiveType.Cube, new Vector3(0f, -0.085f, 0.13f), new Vector3(0.03f, 0.11f, 0.06f), new Vector3(-10f, 0f, 0f), weaponMat);
            AddPart(rifle, "Handguard", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.30f), new Vector3(0.045f, 0.05f, 0.16f), Vector3.zero, weaponMat);
            AddPart(rifle, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0.44f), new Vector3(0.014f, 0.16f, 0.014f), new Vector3(90f, 0f, 0f), weaponMat);
            AddPart(rifle, "Sight", PrimitiveType.Cube, new Vector3(0f, 0.075f, 0.10f), new Vector3(0.02f, 0.03f, 0.05f), Vector3.zero, weaponMat);

            GameObject muzzle = new GameObject("MuzzlePoint");
            muzzle.transform.SetParent(rifle.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.035f, 0.62f);

            return rifle;
        }

        private static void AddPart(GameObject parent, string name, PrimitiveType type, Vector3 localPos,
                                    Vector3 localScale, Vector3 localEuler, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            part.transform.localRotation = Quaternion.Euler(localEuler);

            // El arma es decorativa: fuera colliders (si no, empujarian al jugador).
            Collider col = part.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            if (mat != null)
            {
                var renderer = part.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = mat;
            }
        }

        private static Transform FindRightHand(GameObject player)
        {
            Transform[] all = player.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name.ToLowerInvariant();
                if (n.Contains("righthand") || n.Contains("right_hand") || n == "hand_r") return all[i];
            }
            return null;
        }

        // ==================================================================
        //  Prefab de enemigo humanoide
        // ==================================================================

        private static GameObject BuildEnemyPrefab(Material enemyMat, System.Text.StringBuilder report)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                report.AppendLine("  ERROR: no encuentro el modelo " + ModelPath);
                return null;
            }

            GameObject root = new GameObject("EnemyHumanoid");

            // Modelo como hijo visual (asi la raiz lleva collider + scripts, igual
            // que el jugador: Player12 -> Ch44).
            GameObject visual = (GameObject)Object.Instantiate(model, root.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            // Avatar humanoid: del Animator del modelo o de los sub-assets del FBX.
            Avatar avatar = null;
            Animator modelAnimator = visual.GetComponentInChildren<Animator>(true);
            if (modelAnimator != null) avatar = modelAnimator.avatar;

            if (avatar == null)
            {
                Object[] subs = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
                for (int i = 0; i < subs.Length; i++)
                {
                    Avatar a = subs[i] as Avatar;
                    if (a != null) { avatar = a; break; }
                }
            }

            // Un unico Animator, en la raiz (dos Animators en la misma jerarquia pelean).
            Animator[] modelAnimators = visual.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < modelAnimators.Length; i++) Object.DestroyImmediate(modelAnimators[i]);

            var animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerControllerPath);
            animator.applyRootMotion = false;                                  // lo mueve la IA
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;   // con 12 enemigos se agradece

            // Colliders del modelo fuera: el collider real es el CharacterController.
            Collider[] modelColliders = visual.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < modelColliders.Length; i++) Object.DestroyImmediate(modelColliders[i]);

            // Material propio (para distinguirlos del jugador).
            SkinnedMeshRenderer skinned = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
            float visualHeight = 1.8f;
            if (skinned != null)
            {
                visualHeight = Mathf.Clamp(skinned.localBounds.size.y, 1.2f, 2.4f);

                if (enemyMat != null)
                {
                    Material[] mats = skinned.sharedMaterials;
                    if (mats.Length == 0) mats = new Material[1];
                    for (int i = 0; i < mats.Length; i++) mats[i] = enemyMat;
                    skinned.sharedMaterials = mats;
                }
            }

            // Movimiento y combate
            var controller = root.AddComponent<CharacterController>();
            controller.height = visualHeight;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, visualHeight * 0.5f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.03f;

            var health = root.AddComponent<EnemyHealth>();
            SerializedObject healthSo = new SerializedObject(health);
            SerializedProperty maxHp = healthSo.FindProperty("maxHealth");
            if (maxHp != null) maxHp.intValue = EnemyMaxHealth;
            healthSo.ApplyModifiedProperties();

            var ai = root.AddComponent<EnemyAI>();
            ai.moveSpeed = 4f;
            ai.rotationSpeed = 10f;
            ai.stopDistance = 2f;
            ai.detectionRange = 30f;
            ai.attackRange = 12f;
            ai.aimHeight = 1f;
            ai.fireRate = 1f;
            ai.damagePerShot = 10;
            ai.aimSpreadDegrees = 3f;
            ai.separationRadius = 1.6f;
            ai.separationStrength = 3f;
            ai.autoFitHitbox = false;                                   // el collider ya esta a medida
            ai.playerLayer = LayerMask.GetMask("Player");
            ai.obstacleLayer = LayerMask.GetMask("Default", "Ground", "Player", "Enemy");

            root.AddComponent<EnemyAnimation>();                            // Idle/Running segun velocidad

            root.tag = "Enemy";
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
            {
                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++) all[i].gameObject.layer = enemyLayer;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            Object.DestroyImmediate(root);

            report.AppendLine("  Enemigo: " + EnemyPrefabPath + " (alto ~" + visualHeight.ToString("F2") +
                              " m, avatar " + (avatar == null ? "SIN ASIGNAR" : avatar.name) + ")");
            return prefab;
        }

        // ==================================================================
        //  Objetos curativos
        // ==================================================================

        private static void PlacePickups(Material pickupMat, System.Text.StringBuilder report)
        {
            // Quitar los que hubiera en la escena (setup repetible).
            HealthPickup[] existing = Object.FindObjectsByType<HealthPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++) Object.DestroyImmediate(existing[i].gameObject);

            // Cubo verde + BoxCollider en Is Trigger + script.
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "HealthPickup";
            cube.transform.localScale = Vector3.one * 0.6f;

            BoxCollider box = cube.GetComponent<BoxCollider>();
            if (box != null) box.isTrigger = true;

            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null && pickupMat != null) renderer.sharedMaterial = pickupMat;

            cube.AddComponent<HealthPickup>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(cube, PickupPrefabPath);
            Object.DestroyImmediate(cube);

            int placed = 0;
            for (int i = 0; i < PickupPositions.Length; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = "HealthPickup_" + (i + 1);
                go.transform.position = PickupPositions[i];
                placed++;
            }

            report.AppendLine("  Curacion: " + PickupPrefabPath + " + " + placed + " objetos repartidos (+20 de vida cada uno)");
        }

        // ==================================================================
        //  Cubos fuera / spawner con prefab
        // ==================================================================

        private static void ReplaceCubeEnemies(GameObject enemyPrefab, System.Text.StringBuilder report)
        {
            GameObject[] old = GameObject.FindGameObjectsWithTag("Enemy");
            for (int i = 0; i < old.Length; i++) Object.DestroyImmediate(old[i]);
            report.AppendLine("  Enemigos-cubo eliminados: " + old.Length);

            EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>(FindObjectsInactive.Include);
            if (spawner == null)
            {
                report.AppendLine("  AVISO: no hay EnemySpawner en la escena (no se generan enemigos).");
                return;
            }

            SerializedObject so = new SerializedObject(spawner);

            SerializedProperty prefabsProp = so.FindProperty("enemyPrefabs");
            if (prefabsProp != null)
            {
                prefabsProp.arraySize = 1;
                prefabsProp.GetArrayElementAtIndex(0).objectReferenceValue = enemyPrefab;
            }

            SerializedProperty aliveProp = so.FindProperty("maxEnemiesAlive");
            if (aliveProp != null) aliveProp.intValue = MaxEnemiesAlive;

            SerializedProperty initialProp = so.FindProperty("initialEnemies");
            if (initialProp != null) initialProp.intValue = InitialEnemies;

            SerializedProperty avoidProp = so.FindProperty("avoidBlockedPositions");
            if (avoidProp != null) avoidProp.boolValue = true;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(spawner);

            report.AppendLine("  EnemySpawner: prefab humanoide asignado (tope " + MaxEnemiesAlive +
                              ", oleada inicial " + InitialEnemies + ")");
        }

        // ==================================================================
        //  Utilidades
        // ==================================================================

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);

            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static Material GetOrCreateMaterial(string path, Color color, float metallic, float smoothness, Color? emission = null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[CombatSetup] No encuentro el shader URP/Lit: los materiales saldran mal.");
                return null;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
