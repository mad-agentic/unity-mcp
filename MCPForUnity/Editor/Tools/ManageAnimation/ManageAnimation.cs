using System;
using System.Collections.Generic;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Animation management tool supporting create_controller, add_state, add_transition,
    /// set_keyframe, and create_clip operations.
    /// </summary>
    [McpForUnityTool("manage_animation", group = "animation",
        description = "Manage Unity Animator controllers, animation clips, states, transitions, and keyframes.")]
    public static class ManageAnimation
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "create_controller");

                switch (action.ToLowerInvariant())
                {
                    case "create_controller":
                    case "createcontroller":
                        return CreateController(p);
                    case "add_state":
                    case "addstate":
                        return AddState(p);
                    case "add_transition":
                    case "addtransition":
                        return AddTransition(p);
                    case "set_keyframe":
                    case "setkeyframe":
                        return SetKeyframe(p);
                    case "create_clip":
                    case "createclip":
                        return CreateClip(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: create_controller, add_state, add_transition, set_keyframe, create_clip.");
                }
            }
            catch (ArgumentException ex)
            {
                return new ErrorResponse("InvalidParameters", ex.Message);
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.GetType().Name, ex.Message);
            }
        }

        private static object CreateController(ToolParams p)
        {
            var controllerPath = p.RequireString("controller_path");

            // Ensure path ends with .controller
            var fullPath = controllerPath.EndsWith(".controller", StringComparison.OrdinalIgnoreCase)
                ? controllerPath
                : controllerPath + ".controller";

            // Ensure directory exists
            EnsureDirectoryExists(fullPath);

            try
            {
                var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(fullPath);

                if (controller == null)
                {
                    return new ErrorResponse("ControllerCreationFailed",
                        $"Failed to create animator controller at '{fullPath}'.");
                }

                // Add a default empty state
                var defaultState = controller.layers[0].stateMachine.AddState("Entry");
                controller.layers[0].stateMachine.defaultState = defaultState;

                return new SuccessResponse($"Animator Controller created at '{fullPath}'.", new
                {
                    path = fullPath,
                    name = controller.name,
                    layer_count = controller.layers.Length,
                    state_count = controller.layers[0].stateMachine.states.Length,
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("ControllerCreationFailed",
                    $"Failed to create animator controller: {ex.Message}");
            }
        }

        private static object AddState(ToolParams p)
        {
            var controllerPath = p.RequireString("controller_path");
            var stateName = p.RequireString("state_name");
            var clipPath = p.GetString("clip_path");
            var speed = p.GetFloat("speed", 1.0f) ?? 1.0f;

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
            if (controller == null)
            {
                return new ErrorResponse("ControllerNotFound",
                    $"Animator Controller not found at '{controllerPath}'.");
            }

            try
            {
                // Find the state machine in the base layer (layer 0)
                var stateMachine = controller.layers[0].stateMachine;

                // Check if state already exists
                foreach (var state in stateMachine.states)
                {
                    if (state.state.name == stateName)
                    {
                        return new ErrorResponse("StateExists",
                            $"State '{stateName}' already exists in the controller.");
                    }
                }

                // Create the state
                var newState = stateMachine.AddState(stateName);
                newState.speed = speed;

                // Assign animation clip if provided
                if (!string.IsNullOrEmpty(clipPath))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip != null)
                    {
                        newState.motion = clip;
                    }
                    else
                    {
                        return new ErrorResponse("ClipNotFound",
                            $"Animation clip not found at '{clipPath}'.");
                    }
                }

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return new SuccessResponse($"State '{stateName}' added to controller.", new
                {
                    controller_path = controllerPath,
                    state_name = stateName,
                    clip_path = clipPath,
                    speed = speed,
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("AddStateFailed",
                    $"Failed to add state: {ex.Message}");
            }
        }

        private static object AddTransition(ToolParams p)
        {
            var controllerPath = p.RequireString("controller_path");
            var fromState = p.RequireString("from_state");
            var toState = p.RequireString("to_state");
            var hasExitTime = p.GetBool("has_exit_time", false);
            var transitionDuration = p.GetFloat("transition_duration", 0.25f) ?? 0.25f;

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
            if (controller == null)
            {
                return new ErrorResponse("ControllerNotFound",
                    $"Animator Controller not found at '{controllerPath}'.");
            }

            try
            {
                var stateMachine = controller.layers[0].stateMachine;
                var sourceState = FindState(stateMachine, fromState);
                var destinationState = FindState(stateMachine, toState);

                if (sourceState == null)
                {
                    return new ErrorResponse("StateNotFound",
                        $"Source state '{fromState}' not found.");
                }
                if (destinationState == null)
                {
                    return new ErrorResponse("StateNotFound",
                        $"Destination state '{toState}' not found.");
                }

                var transition = sourceState.AddTransition(destinationState);
                transition.hasExitTime = hasExitTime;
                transition.duration = transitionDuration;

                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return new SuccessResponse(
                    $"Transition added from '{fromState}' to '{toState}'.", new
                    {
                        controller_path = controllerPath,
                        from_state = fromState,
                        to_state = toState,
                        has_exit_time = hasExitTime,
                        transition_duration = transitionDuration,
                    });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("AddTransitionFailed",
                    $"Failed to add transition: {ex.Message}");
            }
        }

        private static object SetKeyframe(ToolParams p)
        {
            var gameObjectIdentifier = p.RequireString("gameobject");
            var propertyPath = p.RequireString("property_path");
            var value = p.RequireFloat("value");
            var time = p.RequireFloat("time");
            var clipPath = p.GetString("clip_path");

            var go = ResolveGameObject(gameObjectIdentifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound",
                    $"GameObject '{gameObjectIdentifier}' not found.");
            }

            try
            {
                // Find or create animation clip
                AnimationClip clip;
                if (!string.IsNullOrEmpty(clipPath))
                {
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip == null)
                    {
                        return new ErrorResponse("ClipNotFound",
                            $"Animation clip not found at '{clipPath}'.");
                    }
                }
                else
                {
                    // Get the existing animation clip from the Animator or Animation component
                    clip = GetAnimationClip(go);
                    if (clip == null)
                    {
                        return new ErrorResponse("NoAnimationClip",
                            $"No animation clip found on '{go.name}'. Provide 'clip_path' to specify a clip.");
                    }
                }

                // Record the animation for undo
                Undo.RecordObject(clip, "Set Keyframe");

                var curvePath = AnimationUtility.CalculateTransformPath(go.transform, null);

                // Get existing curve or create new one
                var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(
                    curvePath, typeof(Transform), propertyPath));

                if (curve == null)
                {
                    curve = new AnimationCurve();
                }

                // Add the keyframe
                var keyframe = new Keyframe(time, value);
                curve.AddKey(keyframe);

                // Sort the curve by time
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(
                    curvePath, typeof(Transform), propertyPath), curve);

                EditorUtility.SetDirty(clip);

                return new SuccessResponse(
                    $"Keyframe set at time={time}s for '{propertyPath}' on '{go.name}'.", new
                    {
                        gameobject = go.name,
                        property_path = propertyPath,
                        value = value,
                        time = time,
                        clip_path = AssetDatabase.GetAssetPath(clip),
                    });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("SetKeyframeFailed",
                    $"Failed to set keyframe: {ex.Message}");
            }
        }

        private static object CreateClip(ToolParams p)
        {
            var clipPath = p.RequireString("clip_path");

            // Ensure path ends with .anim
            var fullPath = clipPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase)
                ? clipPath
                : clipPath + ".anim";

            EnsureDirectoryExists(fullPath);

            try
            {
                var clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, fullPath);

                return new SuccessResponse($"Animation clip created at '{fullPath}'.", new
                {
                    path = fullPath,
                    name = clip.name,
                    duration = clip.length,
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("CreateClipFailed",
                    $"Failed to create animation clip: {ex.Message}");
            }
        }

        private static AnimatorState FindState(AnimatorStateMachine sm, string name)
        {
            foreach (var state in sm.states)
            {
                if (state.state.name == name) return state.state;
            }

            // Search in sub-state machines
            foreach (var childSm in sm.stateMachines)
            {
                var found = FindState(childSm.stateMachine, name);
                if (found != null) return found;
            }

            return null;
        }

        private static GameObject ResolveGameObject(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return null;

            if (int.TryParse(identifier, out int instanceId))
            {
                var obj = EditorUtility.InstanceIDToObject(instanceId);
                return obj as GameObject;
            }

            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in all)
            {
                if (go.name == identifier && !EditorUtility.IsPersistent(go))
                    return go;
            }
            return null;
        }

        private static AnimationClip GetAnimationClip(GameObject go)
        {
            var animator = go.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                if (controller != null && controller.layers.Length > 0)
                {
                    var sm = controller.layers[0].stateMachine;
                    if (sm.defaultState != null && sm.defaultState.motion != null)
                    {
                        return sm.defaultState.motion as AnimationClip;
                    }
                }
            }

            var legacyAnim = go.GetComponent<Animation>();
            if (legacyAnim != null)
            {
                foreach (AnimationState state in legacyAnim)
                {
                    return state.clip;
                }
            }

            return null;
        }

        private static void EnsureDirectoryExists(string path)
        {
            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                var parts = directory.Split('/');
                var current = "";
                for (int i = 0; i < parts.Length; i++)
                {
                    var parent = i > 0 ? string.Join("/", 0, i) : "Assets";
                    var folderName = parts[i];
                    var fullPath = current == "" ? folderName : current + "/" + folderName;
                    if (!AssetDatabase.IsValidFolder(fullPath))
                    {
                        AssetDatabase.CreateFolder(parent, folderName);
                    }
                    current = fullPath;
                }
            }
        }
    }
}
