using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CyberShooter.EditorTools
{
    /// <summary>
    /// Reconstruye el AnimatorController "Playeranimator".
    ///
    /// Diseño v3 (pulido):
    ///   Estados: Idle, walking, Running, Shoot, Jump, Barrida, Dash
    ///     - Idle/walking/Running se eligen con Speed + IsSprinting.
    ///     - Shoot se mantiene con el Bool IsShooting (loop mientras disparas).
    ///     - Jump sale al TOCAR SUELO (IsGrounded): el clip (0.67s) dura menos que
    ///       el aire (~1.1s), por eso ademas va a 0.7x.
    ///     - Barrida (trigger Barrida / tecla C) y Dash (trigger Dash / Ctrl izq.)
    ///       reutilizan el clip de Barrida: no existe dash.fbx.
    ///
    /// - Se auto-repara una sola vez al cargar el editor SI el controlador
    ///   está vacío o incompleto (asi no pisamos cambios hechos a mano).
    /// - Tambien se puede lanzar a mano desde el menu:
    ///       Tools > CyberShooter > Reconstruir Player Animator
    ///       Tools > CyberShooter > Reconstruir Player Animator (limpio)
    ///       Tools > CyberShooter > Diagnostico Player Animator
    ///
    /// "Limpio" borra el asset y lo crea de nuevo: es la unica forma fiable de
    /// deshacerse de sub-objetos huerfanos (transiciones que apuntan a estados
    /// inexistentes: el famoso "Broken text PPtr").
    ///
    /// No se ejecuta nunca en Play Mode.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayerAnimatorRepair
    {
        private const string ControllerPath = "Assets/_Project/Models/Playeranimator.controller";

        // Un FBX por estado, en el mismo orden que StateNames.
        private static readonly string[] ClipFbxPaths =
        {
            "Assets/_Project/Models/Ch44_nonPBR@Breathing Idle.fbx", // Idle
            "Assets/_Project/Models/Walking.fbx",                    // walking
            "Assets/_Project/Models/Running.fbx",                    // Running
            "Assets/_Project/Models/Gunplay.fbx",                    // Shoot
            "Assets/_Project/Models/Jump.fbx",                       // Jump
            "Assets/_Project/Models/Barrida.fbx",                    // Barrida (slide)
            "Assets/_Project/Models/Barrida.fbx",                    // Dash (no hay dash.fbx: reutiliza Barrida)
        };

        private static readonly string[] StateNames = { "Idle", "walking", "Running", "Shoot", "Jump", "Barrida", "Dash" };

        // Indices para que el codigo de las transiciones se lea solo.
        private const int IDLE = 0;
        private const int WALK = 1;
        private const int RUN = 2;
        private const int SHOOT = 3;
        private const int JUMP = 4;
        private const int BARRIDA = 5;
        private const int DASH = 6;

        static PlayerAnimatorRepair()
        {
            // delayCall: esperamos a que el editor termine de importar/compilar
            // antes de tocar assets, para no competir con la recarga de dominio.
            EditorApplication.delayCall += AutoRepair;
        }

        private static void AutoRepair()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!NeedsRepair()) return;
            Rebuild(true, false);
        }

        [MenuItem("Tools/CyberShooter/Reconstruir Player Animator")]
        private static void RebuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AnimRepair] No se toca el Animator en Play Mode. Para el juego primero.");
                return;
            }
            Rebuild(false, false);
        }

        // Borra el asset y lo crea de nuevo. Es la unica forma fiable de quitar
        // sub-objetos huerfanos (p.ej. la transicion del "Broken text PPtr").
        [MenuItem("Tools/CyberShooter/Reconstruir Player Animator (limpio)")]
        private static void RebuildCleanFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AnimRepair] No se toca el Animator en Play Mode. Para el juego primero.");
                return;
            }
            Rebuild(false, true);
        }

        [MenuItem("Tools/CyberShooter/Diagnostico Player Animator")]
        private static void Diagnose()
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (c == null)
            {
                Debug.LogError("[AnimRepair] No existe " + ControllerPath);
                return;
            }

            var sm = c.layers.Length > 0 ? c.layers[0].stateMachine : null;
            float weight = c.layers.Length > 0 ? c.layers[0].defaultWeight : -1f;
            int states = sm == null ? -1 : sm.states.Length;
            int anyState = sm == null ? -1 : sm.anyStateTransitions.Length;
            string def = (sm == null || sm.defaultState == null) ? "-" : sm.defaultState.name;

            Debug.Log("[AnimRepair] layers=" + c.layers.Length + " weight=" + weight +
                      " states=" + states + " anyState=" + anyState + " default=" + def);

            if (sm != null)
            {
                for (int i = 0; i < sm.states.Length; i++)
                {
                    var m = sm.states[i].state.motion;
                    Debug.Log("[AnimRepair]   estado " + sm.states[i].state.name +
                              " -> motion=" + (m == null ? "NINGUNA" : m.name));
                }
            }
        }

        private static bool NeedsRepair()
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (c == null) return false;

            var layers = c.layers;
            if (layers == null || layers.Length == 0) return true;

            var sm = layers[0].stateMachine;
            if (sm == null) return true;
            if (sm.states.Length < StateNames.Length) return true;

            for (int i = 0; i < sm.states.Length; i++)
            {
                if (sm.states[i].state.motion == null)
                {
                    Debug.LogWarning("[AnimRepair] El estado '" + sm.states[i].state.name + "' no tiene animación. Reconstruyendo...");
                    return true;
                }
            }
            return false;
        }

        private static void Rebuild(bool automatic, bool forceClean)
        {
            var old = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            // Quién usa este controlador ahora mismo (para reasignar si hay que recrear el asset).
            var users = new List<Animator>();
            var allAnimators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude);
            for (int i = 0; i < allAnimators.Length; i++)
            {
                if (old != null && allAnimators[i].runtimeAnimatorController == old) users.Add(allAnimators[i]);
            }

            // Localizamos el clip de cada FBX.
            var clips = new AnimationClip[ClipFbxPaths.Length];
            for (int i = 0; i < ClipFbxPaths.Length; i++) clips[i] = FindClip(ClipFbxPaths[i]);

            AnimatorController controller = old;
            bool recreated = false;

            if (forceClean && old != null)
            {
                // BORRAR + CREAR: los sub-objetos huerfanos se van con el asset viejo.
                AssetDatabase.DeleteAsset(ControllerPath);
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                recreated = true;
            }
            else if (old == null || old.layers == null || old.layers.Length == 0 || old.layers[0].stateMachine == null)
            {
                // State machine invalido (lienzo en blanco): recreamos el asset.
                if (old != null) AssetDatabase.DeleteAsset(ControllerPath);
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                recreated = true;
            }

            if (controller == null)
            {
                Debug.LogError("[AnimRepair] No pude crear/abrir " + ControllerPath);
                return;
            }

            var sm = controller.layers[0].stateMachine;

            // ---- 1) Limpiar el lienzo ----
            var oldStates = sm.states;
            for (int i = 0; i < oldStates.Length; i++) sm.RemoveState(oldStates[i].state);
            while (sm.anyStateTransitions.Length > 0) sm.RemoveAnyStateTransition(sm.anyStateTransitions[0]);
            while (sm.entryTransitions.Length > 0) sm.RemoveEntryTransition(sm.entryTransitions[0]);
            while (sm.stateMachines.Length > 0) sm.RemoveStateMachine(sm.stateMachines[0].stateMachine);

            // ---- 2) Parámetros ----
            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "IsSprinting", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "IsShooting", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Barrida", AnimatorControllerParameterType.Trigger);
            // Se mantienen por compatibilidad: el codigo todavia los envia aunque ya
            // no tengan estado destino. Si existieran como parametro, Unity no avisa.
            EnsureParameter(controller, "Shoot", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Dash", AnimatorControllerParameterType.Trigger);

            // ---- 3) Estados ----
            var st = new AnimatorState[StateNames.Length];
            for (int i = 0; i < StateNames.Length; i++)
            {
                st[i] = sm.AddState(StateNames[i], new Vector3(80f + i * 240f, 60f + (i % 2) * 150f, 0f));
                st[i].motion = clips[i];
                st[i].speed = 1f;
                st[i].writeDefaultValues = true;
            }
            // El personaje esta ~1.1s en el aire y el clip de salto dura 0.67s: lo
            // reproducimos mas lento para que la animacion aguante todo el salto.
            st[JUMP].speed = 0.7f;

            // El dash dura 0.2s y reutiliza el clip de Barrida (1.53s): a 2x el clip
            // efectivo dura ~0.76s y salimos a 0.26 => ~0.2s, justo lo que dura el dash.
            st[DASH].speed = 2f;

            sm.defaultState = st[IDLE]; // Idle por defecto

            // ---- 4) Transiciones ----
            // OJO con el orden: Unity evalua las transiciones EN ORDEN y usa la
            // primera cuyas condiciones se cumplan. Por eso los auto-bucles de
            // "exit time" se anaden AL FINAL.

            // --- Locomocion: Idle / walking / Running ---
            // Idle -> walking   (me muevo, sin sprint)
            var t = AddTransition(st[IDLE], st[WALK], 0.15f);
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsSprinting");

            // Idle -> Running   (me muevo en sprint)
            t = AddTransition(st[IDLE], st[RUN], 0.15f);
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            t.AddCondition(AnimatorConditionMode.If, 0f, "IsSprinting");

            // walking -> Idle   (paro)
            t = AddTransition(st[WALK], st[IDLE], 0.15f);
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // walking -> Running   (empiezo a correr)
            t = AddTransition(st[WALK], st[RUN], 0.15f);
            t.AddCondition(AnimatorConditionMode.If, 0f, "IsSprinting");

            // Running -> Idle   (paro)
            t = AddTransition(st[RUN], st[IDLE], 0.15f);
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // Running -> walking   (suelto sprint pero sigo moviendome)
            t = AddTransition(st[RUN], st[WALK], 0.15f);
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsSprinting");

            // --- Disparo: IsShooting (bool => la animacion se queda en loop) ---
            AddShootEntry(st[IDLE], st[SHOOT]);
            AddShootEntry(st[WALK], st[SHOOT]);
            AddShootEntry(st[RUN], st[SHOOT]);

            // Shoot -> locomocion al soltar el clic
            var tShoot = AddTransition(st[SHOOT], st[IDLE], 0.1f);
            tShoot.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsShooting");
            tShoot.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            tShoot = AddTransition(st[SHOOT], st[RUN], 0.1f);
            tShoot.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsShooting");
            tShoot.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tShoot.AddCondition(AnimatorConditionMode.If, 0f, "IsSprinting");

            tShoot = AddTransition(st[SHOOT], st[WALK], 0.1f);
            tShoot.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsShooting");
            tShoot.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            // --- One-shots (triggers) ---
            // Salto: NO salimos por tiempo, salimos cuando el motor avisa de que hemos
            // tocado suelo. El exitTime 0.5 es solo una red de seguridad: Unity exige
            // cumplir la condicion Y el tiempo.
            AddAnyState(sm, st[JUMP], "Jump");

            var tj = AddExitOnGrounded(st[JUMP], st[IDLE], 0.5f, 0.15f);
            tj.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            tj = AddExitOnGrounded(st[JUMP], st[RUN], 0.5f, 0.15f);
            tj.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tj.AddCondition(AnimatorConditionMode.If, 0f, "IsSprinting");

            tj = AddExitOnGrounded(st[JUMP], st[WALK], 0.5f, 0.15f);
            tj.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tj.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsSprinting");

            AddAnyState(sm, st[BARRIDA], "Barrida");
            // El clip de Barrida dura 1.53s y la barrida 0.5s: salimos al 33% (~0.5s).
            AddExitToIdle(st[BARRIDA], st[IDLE], 0.33f, 0.15f);

            // Dash: mismo clip que la barrida pero a 2x (ver st[DASH].speed) y sale
            // al 26% del clip efectivo => ~0.2s, que es lo que dura el dash.
            AddAnyState(sm, st[DASH], "Dash");
            AddExitToIdle(st[DASH], st[IDLE], 0.26f, 0.1f);

            // --- Auto-bucles (AL FINAL: solo actuan si no ha ganado otra transicion) ---
            // Los clips de ciclo NO tienen "Loop Time" en su FBX, asi que sin esto se
            // quedarian congelados en el ultimo frame.
            AddSelfLoop(st[IDLE]);
            AddSelfLoop(st[WALK]);
            AddSelfLoop(st[RUN]);
            AddSelfLoop(st[SHOOT]);

            // ---- 5) La capa Base debe pesar 1 (si no, no se ve ninguna animación) ----
            var layers = controller.layers;
            layers[0].defaultWeight = 1f;
            layers[0].blendingMode = AnimatorLayerBlendingMode.Override;
            controller.layers = layers;

            // ---- 6) Reasignar si hubo que recrear el asset ----
            if (recreated)
            {
                for (int i = 0; i < users.Count; i++)
                {
                    users[i].runtimeAnimatorController = controller;
                    EditorUtility.SetDirty(users[i]);
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            if (recreated && users.Count > 0) EditorSceneManager.SaveOpenScenes();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

            string missing = "";
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) missing += " " + StateNames[i];
            }

            Debug.Log("[AnimRepair] Playeranimator " + (automatic ? "auto-reparado" : "reconstruido") +
                      (recreated ? " (asset recreado)" : "") +
                      ": estados=" + sm.states.Length + ", anyState=" + sm.anyStateTransitions.Length +
                      ", def=" + sm.defaultState.name + ", capa weight=" + controller.layers[0].defaultWeight +
                      (missing.Length > 0 ? " | SIN CLIP:" + missing : " | todos los clips OK"));
        }

        // ---------- Helpers ----------

        private static AnimatorStateTransition AddTransition(AnimatorState from, AnimatorState to, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = duration;
            t.canTransitionToSelf = false;
            return t;
        }

        // Entrada al estado de disparo: se queda ahi mientras IsShooting sea true.
        private static void AddShootEntry(AnimatorState from, AnimatorState shoot)
        {
            var t = AddTransition(from, shoot, 0.1f);
            t.AddCondition(AnimatorConditionMode.If, 0f, "IsShooting");
        }

        // Auto-transicion para que un estado de ciclo se repita (los clips de ciclo
        // del proyecto no tienen "Loop Time" activado en el FBX).
        private static void AddSelfLoop(AnimatorState state)
        {
            var t = state.AddTransition(state);
            t.hasExitTime = true;
            t.exitTime = 1f;
            t.hasFixedDuration = true;
            t.duration = 0f;
            t.canTransitionToSelf = true;
        }

        private static void AddAnyState(AnimatorStateMachine sm, AnimatorState destination, string trigger)
        {
            var t = sm.AddAnyStateTransition(destination);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void AddExitToIdle(AnimatorState from, AnimatorState idle, float exitTime, float duration)
        {
            var t = from.AddTransition(idle);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.hasFixedDuration = true;
            t.duration = duration;
        }

        // Salida del salto: espera a que el motor diga "en el suelo" (IsGrounded).
        // El exitTime es una red de seguridad para que no salga en el primer instante.
        private static AnimatorStateTransition AddExitOnGrounded(AnimatorState from, AnimatorState to, float exitTime, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.hasFixedDuration = true;
            t.duration = duration;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            return t;
        }

        private static void EnsureParameter(AnimatorController c, string name, AnimatorControllerParameterType type)
        {
            var ps = c.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].name == name) return;
            }
            c.AddParameter(name, type);
        }

        private static AnimationClip FindClip(string fbxPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            AnimationClip best = null;
            for (int i = 0; i < assets.Length; i++)
            {
                var c = assets[i] as AnimationClip;
                if (c == null) continue;
                if (c.name.StartsWith("__preview__")) continue;
                if (best == null || c.length > best.length) best = c;
            }
            if (best == null) Debug.LogWarning("[AnimRepair] No encontré ningún clip de animación en " + fbxPath);
            return best;
        }
    }
}
