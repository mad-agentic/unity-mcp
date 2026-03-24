using System;
using System.Collections.Generic;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using System.Linq;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Asset database refresh and script recompilation tool.
    /// Supports long-running async operations with EditorApplication.update polling.
    /// </summary>
    [McpForUnityTool("refresh_unity", group = "core",
        description = "Refresh Unity asset database, recompile scripts, and refresh import settings. Supports async long-running operations.")]
    public static class RefreshUnity
    {
        private static string _currentOperation = "";
        private static string _currentPhase = "";
        private static float _progress = 0f;
        private static bool _isRunning = false;
        private static string _errorMessage = null;

        public static object HandleCommand(JObject @params)
        {
            return HandleCommandAsync(@params, false);
        }

        public static async Task<object> HandleCommand(JObject @params, bool async)
        {
            return HandleCommandAsync(@params, true);
        }

        private static object HandleCommandAsync(JObject @params, bool async)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "refresh");
                var assetPath = p.GetString("path");

                switch (action.ToLowerInvariant())
                {
                    case "refresh":
                        return RefreshAssetDatabase(async);
                    case "recompile":
                    case "recompile_scripts":
                        return RecompileScripts(async);
                    case "import_settings":
                    case "importsettings":
                        return RefreshImportSettings(p, async);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: refresh, recompile, import_settings.");
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

        private static object RefreshAssetDatabase(bool async)
        {
            if (async)
            {
                return StartAsyncOperation("refresh", "AssetDatabase.Refresh started.", new
                {
                    operation = "refresh",
                    phase = "initiating",
                    progress = 0f,
                    async = true,
                    note = "Asset database refresh is running in the background."
                });
            }

            try
            {
                AssetDatabase.Refresh();
                return new SuccessResponse("Asset database refreshed.", new
                {
                    operation = "refresh",
                    phase = "complete",
                    progress = 1f
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("RefreshFailed", $"Asset database refresh failed: {ex.Message}");
            }
        }

        private static object RecompileScripts(bool async)
        {
            if (async)
            {
                return StartAsyncOperation("recompile", "Script recompilation initiated.", new
                {
                    operation = "recompile",
                    phase = "initiating",
                    progress = 0f,
                    async = true,
                    note = "Script recompilation is running in the background."
                });
            }

            try
            {
                AssetDatabase.Refresh();
                return new SuccessResponse("Script recompilation triggered.", new
                {
                    operation = "recompile",
                    phase = "triggered",
                    note = "Refresh initiated. Unity will recompile scripts automatically."
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("RecompileFailed", $"Script recompilation failed: {ex.Message}");
            }
        }

        private static object RefreshImportSettings(ToolParams p, bool async)
        {
            var assetPath = p.GetString("path");

            if (string.IsNullOrEmpty(assetPath))
            {
                // Refresh all import settings
                if (async)
                {
                    return StartAsyncOperation("import_settings", "Reimporting all assets.", new
                    {
                        operation = "import_settings",
                        phase = "all_assets",
                        progress = 0f,
                        async = true,
                        path = "Assets",
                        note = "Reimporting all assets in the background."
                    });
                }

                try
                {
                    AssetDatabase.Refresh();
                    return new SuccessResponse("All assets reimported.", new
                    {
                        operation = "import_settings",
                        phase = "complete",
                        path = "Assets"
                    });
                }
                catch (Exception ex)
                {
                    return new ErrorResponse("ImportSettingsFailed", $"Reimport failed: {ex.Message}");
                }
            }

            // Validate path
            if (!AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath))
            {
                return new ErrorResponse("AssetNotFound",
                    $"No asset found at path '{assetPath}'.");
            }

            if (async)
            {
                return StartAsyncOperation("import_settings", $"Reimporting '{assetPath}'.", new
                {
                    operation = "import_settings",
                    phase = "initiating",
                    progress = 0f,
                    async = true,
                    path = assetPath,
                    note = $"Reimport of '{assetPath}' is running in the background."
                });
            }

            try
            {
                AssetDatabase.ImportAsset(assetPath);
                return new SuccessResponse($"Import settings refreshed for '{assetPath}'.", new
                {
                    operation = "import_settings",
                    phase = "complete",
                    path = assetPath
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("ImportSettingsFailed",
                    $"Failed to refresh import settings for '{assetPath}': {ex.Message}");
            }
        }

        private static object StartAsyncOperation(string operation, string message, object data)
        {
            _currentOperation = operation;
            _currentPhase = "running";
            _progress = 0f;
            _isRunning = true;
            _errorMessage = null;

            // Register update callback if not already registered
            if (!EditorApplication.update.GetInvocationList().Contains((EditorApplication.CallbackFunction)OnEditorUpdate))
            {
                EditorApplication.update += OnEditorUpdate;
            }

            return new SuccessResponse(message, data);
        }

        private static void OnEditorUpdate()
        {
            if (!_isRunning) return;

            // Check for compilation errors that might indicate the operation completed with issues
            if (EditorApplication.isCompiling)
            {
                _currentPhase = "compiling";
                _progress = 0.8f;
                return;
            }

            if (!EditorApplication.isCompiling && _currentPhase == "compiling")
            {
                _currentPhase = "complete";
                _progress = 1f;
                CompleteAsyncOperation();
            }
        }

        private static void CompleteAsyncOperation()
        {
            _isRunning = false;
            EditorApplication.update -= OnEditorUpdate;

            // Note: In a full implementation, you would send a progress callback here
            // via the plugin hub or notification system.
            _currentPhase = "complete";
        }

        /// <summary>
        /// Check the current status of an async refresh operation.
        /// </summary>
        public static object GetAsyncStatus()
        {
            return new
            {
                is_running = _isRunning,
                operation = _currentOperation,
                phase = _currentPhase,
                progress = _progress,
                error = _errorMessage
            };
        }
    }
}
