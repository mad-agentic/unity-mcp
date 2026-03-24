using System;
using System.Collections.Generic;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Editor management tool supporting play mode controls (play, pause, stop, step),
    /// play mode state queries, selection management, window focusing, and menu execution.
    /// </summary>
    [McpForUnityTool("manage_editor", Group = "core",
        Description = "Manage Unity Editor: play/pause/stop/step, play mode status, get/set selection, focus windows, execute menu items.")]
    public static class ManageEditor
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "is_playing");

                switch (action.ToLowerInvariant())
                {
                    case "play":
                        return StartPlayMode();
                    case "pause":
                        return PausePlayMode();
                    case "stop":
                        return StopPlayMode();
                    case "step":
                        return StepPlayMode();
                    case "is_playing":
                        return GetPlayModeStatus();
                    case "set_playmode_toggle":
                    case "setplaymodetoggle":
                        return SetPlayModeToggle(p);
                    case "get_selection":
                    case "getselection":
                        return GetSelection(p);
                    case "set_selection":
                    case "setselection":
                        return SetSelection(p);
                    case "focus_window":
                    case "focuswindow":
                        return FocusWindow(p);
                    case "execute_menu_item":
                    case "executemenuitem":
                        return ExecuteMenuItem(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: play, pause, stop, step, is_playing, set_playmode_toggle, get_selection, set_selection, focus_window, execute_menu_item.");
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

        private static object StartPlayMode()
        {
            if (Application.isPlaying)
            {
                return new SuccessResponse("Already in play mode.", new { is_playing = true });
            }

            EditorApplication.isPlaying = true;
            return new SuccessResponse("Play mode started.", new { is_playing = true });
        }

        private static object PausePlayMode()
        {
            if (!Application.isPlaying)
            {
                return new ErrorResponse("InvalidContext", "Cannot pause when not in play mode.");
            }

            EditorApplication.isPaused = !EditorApplication.isPaused;
            return new SuccessResponse(
                EditorApplication.isPaused ? "Play mode paused." : "Play mode resumed.",
                new { is_playing = Application.isPlaying, is_paused = EditorApplication.isPaused });
        }

        private static object StopPlayMode()
        {
            if (!Application.isPlaying && !EditorApplication.isPaused)
            {
                return new SuccessResponse("Not in play mode.", new { is_playing = false, is_paused = false });
            }

            EditorApplication.isPlaying = false;
            EditorApplication.isPaused = false;
            return new SuccessResponse("Play mode stopped.", new { is_playing = false, is_paused = false });
        }

        private static object StepPlayMode()
        {
            if (!Application.isPlaying)
            {
                return new ErrorResponse("InvalidContext", "Cannot step when not in play mode.");
            }

            EditorApplication.Step();
            return new SuccessResponse("Stepped one frame.", new { is_playing = true, is_paused = true });
        }

        private static object GetPlayModeStatus()
        {
            return new SuccessResponse(
                Application.isPlaying
                    ? (EditorApplication.isPaused ? "Play mode (paused)." : "Play mode (running).")
                    : "Edit mode.",
                new
                {
                    is_playing = Application.isPlaying,
                    is_paused = EditorApplication.isPaused,
                    mode = Application.isPlaying
                        ? (EditorApplication.isPaused ? "PlayingPaused" : "Playing")
                        : "Edit"
                });
        }

        private static object SetPlayModeToggle(ToolParams p)
        {
            var enabled = p.RequireBool("enabled");
            EditorApplication.playModeCouldHaveChanged = true;

            // Note: The playmode auto-save toggle is a user preference.
            // We report the action was received, though actual toggle requires EditorWindow access.
            return new SuccessResponse(
                $"Play mode toggle state noted (enabled={enabled}).",
                new { playmode_could_have_changed = enabled });
        }

        private static object GetSelection(ToolParams p)
        {
            var includeScene = p.GetBool("include_scene", true);
            var includeAssets = p.GetBool("include_assets", true);

            var selected = new List<object>();

            if (includeScene)
            {
                var gameObjects = Selection.gameObjects;
                foreach (var go in gameObjects)
                {
                    selected.Add(new
                    {
                        type = "GameObject",
                        name = go.name,
                        instance_id = go.GetInstanceID(),
                        path = GetGameObjectPath(go),
                        active = go.activeSelf,
                    });
                }
            }

            if (includeAssets)
            {
                var assets = Selection.objects;
                foreach (var obj in assets)
                {
                    if (obj is GameObject) continue;
                    selected.Add(new
                    {
                        type = obj.GetType().Name,
                        name = obj.name,
                        instance_id = obj.GetInstanceID(),
                        path = AssetDatabase.GetAssetPath(obj),
                    });
                }
            }

            return new SuccessResponse(
                $"Selection contains {selected.Count} object(s).",
                new
                {
                    count = selected.Count,
                    selection = selected
                });
        }

        private static object SetSelection(ToolParams p)
        {
            var gameObjects = p.GetArray("gameobjects");

            if (gameObjects == null || gameObjects.Count == 0)
            {
                Selection.activeGameObject = null;
                Selection.activeObject = null;
                return new SuccessResponse("Selection cleared.", null);
            }

            var selected = new List<GameObject>();
            foreach (var item in gameObjects)
            {
                var identifier = item?.ToString();
                if (string.IsNullOrEmpty(identifier)) continue;

                var go = ResolveGameObject(identifier);
                if (go != null) selected.Add(go);
            }

            if (selected.Count > 0)
            {
                Selection.objects = selected.ToArray();
                Selection.activeGameObject = selected[0];
            }

            return new SuccessResponse(
                $"Selection set to {selected.Count} GameObject(s).",
                new
                {
                    count = selected.Count,
                    names = selected.ConvertAll(go => go.name)
                });
        }

        private static object FocusWindow(ToolParams p)
        {
            var windowType = p.RequireString("window_type");

            // Map common display names to actual types
            var typeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "SceneView", "UnityEditor.SceneView" },
                { "GameView", "UnityEditor.GameView" },
                { "ProjectBrowser", "UnityEditor.ProjectBrowser" },
                { "Hierarchy", "UnityEditor.SceneHierarchyWindow" },
                { "Inspector", "UnityEditor.InspectorWindow" },
                { "Console", "UnityEditor.ConsoleWindow" },
                { "Animator", "UnityEditor.AnimatorController" },
                { "Animation", "UnityEditor.AnimationWindow" },
                { "Profiler", "UnityEditor.ProfilerWindow" },
                { "FrameDebugger", "UnityEditor.FrameDebuggerWindow" },
                { "PackageManager", "UnityEditor.PackageManager.UI.PackageManagerWindow" },
                { "Lighting", "UnityEditor.LightingWindow" },
                { "OcclusionCulling", "UnityEditor.OcclusionCullingWindow" },
                { "Navigation", "UnityEditor.AINavigationNavigationWindow" },
                { "PrefabStage", "UnityEditor.Experimental.SceneManagement.PrefabStage" },
            };

            string fullTypeName;
            if (typeMap.TryGetValue(windowType, out var mapped))
                fullTypeName = mapped;
            else if (windowType.Contains("."))
                fullTypeName = windowType;
            else
                fullTypeName = "UnityEditor." + windowType;

            Type windowClass = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;
                windowClass = assembly.GetType(fullTypeName);
                if (windowClass != null) break;
            }

            if (windowClass == null)
            {
                return new ErrorResponse("WindowTypeNotFound",
                    $"Could not find EditorWindow type '{windowType}'.");
            }

            var window = EditorWindow.GetWindow(windowClass, false);
            if (window != null)
            {
                window.Show();
                window.Focus();
                return new SuccessResponse($"Window '{windowType}' focused.", new
                {
                    window_type = windowType,
                    title = window.titleContent?.text
                });
            }

            return new ErrorResponse("WindowOpenFailed", $"Failed to open window '{windowType}'.");
        }

        private static object ExecuteMenuItem(ToolParams p)
        {
            var menuPath = p.RequireString("menu_path");

            if (!UnityEditor.Menu.GetMenuItem(menuPath, false).IsValid())
            {
                return new ErrorResponse("MenuItemNotFound",
                    $"Menu item path '{menuPath}' not found. "
                    + "Note: Menu paths use '/' separators (e.g. 'File/Save', 'GameObject/Create Empty').");
            }

            try
            {
                UnityEditor.Menu.ExecuteFunction(menuPath);
                return new SuccessResponse($"Menu item '{menuPath}' executed.", new
                {
                    menu_path = menuPath
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("MenuExecutionFailed",
                    $"Failed to execute menu item '{menuPath}': {ex.Message}");
            }
        }

        private static GameObject ResolveGameObject(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return null;

            // Try as instance ID
            if (int.TryParse(identifier, out int instanceId))
            {
                var obj = EditorUtility.InstanceIDToObject(instanceId);
                return obj as GameObject;
            }

            // Try as name - search in selection first
            var selected = Selection.GetFiltered<GameObject>(SelectionMode.TopLevel);
            foreach (var go in selected)
            {
                if (go.name == identifier) return go;
            }

            // Search all GameObjects
            var all = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in all)
            {
                if (go.name == identifier && !EditorUtility.IsPersistent(go))
                    return go;
            }

            return null;
        }

        private static string GetGameObjectPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
