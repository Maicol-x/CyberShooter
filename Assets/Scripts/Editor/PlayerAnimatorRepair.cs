using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CyberShooter.EditorTools
{
    /// <summary>
    /// Reconstruye el AnimatorController "Playeranimator" con los 5 estados
    /// (Idle, Running, Shoot, Jump, Dash), sus clips y todas las transiciones.
    ///
    /// - Se auto-repara una sola vez al cargar el editor SI el controlador
    ///   está vacío o incompleto (asi arreglamos el lienzo en blanco).
    /// - Tambien se puede lanzar a mano en cualquier momento desde el menu:
    ///       Tools > CyberShooter > Reconstruir Player Animator
    ///       Tools > CyberShooter > Diagnostico Player Animator
    ///
    /// No se ejecuta nunca en Play Mode.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayerAnimatorRepair
    {
        private const string ControllerPath = "Assets/Models/Playeranimator.controller";

        // One FBX per state, in the same order as StateNames.
        private static readonly string[] ClipFbxPaths =
        {
            "Assets/Models/Ch44_nonPBR@Breathing Idle.fbx", // Idle
            "Assets/Models/Ch44_nonPBR@Running.fbx",        // Running
            "Assets/Models/Gunplay.fbx",                    // Shoot
            "Assets/Models/Jump.fbx",                       // Jump
            "Assets/Models/dash.fbx",                       // Dash
        };

        private static readonly string[] StateNames = { "Idle", "Running", "Shoot", "Jump", "Dash" };

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
            Rebuild(true);
        }

        [MenuItem("Tools/CyberShooter/Reconstruir Player Animator")]
        private static void RebuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[AnimRepair] No se toca el Animator en Play Mode. Para el juego primero.");
                return;
            }
            Rebuild(false);
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

        private static void Rebuild(bool automatic)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError("[AnimRepair] No encontré " + ControllerPath);
                return;
            }

            // Quién usa este controlador ahora mismo (para reasignar si hay que recrear el asset).
            var users = new List<Animator>();
            var allAnimators = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
            for (int i = 0; i < allAnimators.Length; i++)
            {
                if (allAnimators[i].runtimeAnimatorController == controller) users.Add(allAnimators[i]);
            }

            // Localizamos el clip de cada FBX.
            var clips = new AnimationClip[ClipFbxPaths.Length];
            for (int i = 0; i < ClipFbxPaths.Length; i++) clips[i] = FindClip(ClipFbxPaths[i]);

            // Si el state machine no es válido, recreamos el asset (mismo path => mismo GUID/meta).
            bool recreated = false;
            if (controller.layers == null || controller.layers.Length == 0 || controller.layers[0].stateMachine == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                recreated = true;
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
            EnsureParameter(controller, "Shoot", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
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
            sm.defaultState = st[0]; // Idle por defecto

            // ---- 4) Transiciones ----
            // Idle -> Running   (Speed > 0.1)
            var t = st[0].AddTransition(st[1]);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0.15f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            // Running -> Idle   (Speed < 0.1)
            t = st[1].AddTransition(st[0]);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0.15f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // AnyState -> Shoot / Jump / Dash  (triggers)
            AddAnyState(sm, st[2], "Shoot");
            AddAnyState(sm, st[3], "Jump");
            AddAnyState(sm, st[4], "Dash");

            // Shoot / Jump / Dash -> Idle  (con Has Exit Time)
            AddExitToIdle(st[2], st[0]);
            AddExitToIdle(st[3], st[0]);
            AddExitToIdle(st[4], st[0]);

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

        private static void AddAnyState(AnimatorStateMachine sm, AnimatorState destination, string trigger)
        {
            var t = sm.AddAnyStateTransition(destination);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0.1f;
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        }

        private static void AddExitToIdle(AnimatorState from, AnimatorState idle)
        {
            var t = from.AddTransition(idle);
            t.hasExitTime = true;
            t.exitTime = 0.9f;
            t.hasFixedDuration = true;
            t.duration = 0.2f;
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
