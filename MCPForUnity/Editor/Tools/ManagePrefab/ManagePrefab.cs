using System;
using System.Collections.Generic;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Prefab management tool supporting create, instantiate, unpack, get_info, apply, and revert.
    /// </summary>
    [McpForUnityTool("manage_prefab", Group = "core",
        Description = "Manage Unity Prefabs: create, instantiate, unpack, get info, apply overrides, and revert.")]
    public static class ManagePrefab
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "get_info");

                switch (action.ToLowerInvariant())
                {
                    case "create":
                        return CreatePrefab(p);
                    case "instantiate":
                        return InstantiatePrefab(p);
                    case "unpack":
                        return UnpackPrefab(p);
                    case "get_info":
                    case "getinfo":
                        return GetPrefabInfo(p);
                    case "apply":
                        return ApplyPrefab(p);
                    case "revert":
                        return RevertPrefab(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: create, instantiate, unpack, get_info, apply, revert.");
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

        private static object CreatePrefab(ToolParams p)
        {
            var prefabPath = p.RequireString("prefab_path");
            var targetIdentifier = p.RequireString("target");

            var sourceGo = ResolveGameObject(targetIdentifier);
            if (sourceGo == null)
            {
                return new ErrorResponse("GameObjectNotFound",
                    $"GameObject '{targetIdentifier}' not found.");
            }

            // Ensure the path ends with .prefab
            var fullPath = prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                ? prefabPath
                : prefabPath + ".prefab";

            // Ensure directory exists
            var directory = System.IO.Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                var dirs = directory.Split('/');
                var current = "";
                foreach (var dir in dirs)
                {
                    current += dir;
                    if (!AssetDatabase.IsValidFolder(current))
                    {
                        AssetDatabase.CreateFolder(
                            dirs.Length > 1 ? string.Join("/", 0, dirs.Length - 1) : "Assets",
                            dir);
                    }
                    current += "/";
                }
            }

            try
            {
                // Check if already a prefab instance
                var prefabStatus = PrefabUtility.GetPrefabAssetType(sourceGo);
                if (prefabStatus != PrefabAssetType.NotAPrefab)
                {
                    return new ErrorResponse("AlreadyPrefab",
                        $"GameObject '{sourceGo.name}' is already a prefab instance.");
                }

                // Create the prefab
                var prefab = PrefabUtility.SaveAsPrefabAsset(sourceGo, fullPath, out var success);
                if (!success || prefab == null)
                {
                    return new ErrorResponse("PrefabCreationFailed",
                        $"Failed to create prefab at '{fullPath}'.");
                }

                return new SuccessResponse($"Prefab created at '{fullPath}'.", new
                {
                    prefab_path = fullPath,
                    name = prefab.name,
                    asset_type = "Prefab",
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("PrefabCreationFailed",
                    $"Failed to create prefab: {ex.Message}");
            }
        }

        private static object InstantiatePrefab(ToolParams p)
        {
            var prefabPath = p.RequireString("prefab_path");
            var parentIdentifier = p.GetString("parent");
            var positionArray = p.GetArray("position");
            var sceneName = p.GetString("scene_name");

            // Load the prefab asset
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return new ErrorResponse("PrefabNotFound",
                    $"No prefab found at path '{prefabPath}'.");
            }

            Vector3 position = Vector3.zero;
            if (positionArray != null && positionArray.Count >= 3)
            {
                position = new Vector3(
                    positionArray[0].Value<float>(),
                    positionArray[1].Value<float>(),
                    positionArray[2].Value<float>());
            }

            GameObject instance;

            // Determine target scene
            Scene targetScene;
            if (!string.IsNullOrEmpty(sceneName))
            {
                targetScene = SceneManager.GetSceneByName(sceneName);
                if (!targetScene.IsValid())
                {
                    return new ErrorResponse("SceneNotFound",
                        $"Scene '{sceneName}' not found.");
                }
            }
            else
            {
                targetScene = SceneManager.GetActiveScene();
            }

            // Handle parent
            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(parentIdentifier))
            {
                var parentGo = ResolveGameObject(parentIdentifier);
                if (parentGo == null)
                {
                    return new ErrorResponse("ParentNotFound",
                        $"Parent GameObject '{parentIdentifier}' not found.");
                }
                parentTransform = parentGo.transform;
            }

            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab, targetScene);

                if (parentTransform != null)
                {
                    instance.transform.SetParent(parentTransform, false);
                }

                if (positionArray != null)
                {
                    instance.transform.position = position;
                }

                return new SuccessResponse(
                    $"Prefab '{prefab.name}' instantiated into scene '{targetScene.name}'.",
                    new
                    {
                        name = instance.name,
                        instance_id = instance.GetInstanceID(),
                        prefab_path = prefabPath,
                        scene = targetScene.name,
                        position = new[] { instance.transform.position.x, instance.transform.position.y, instance.transform.position.z },
                        parent = parentTransform != null ? parentTransform.name : null,
                    });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("InstantiationFailed",
                    $"Failed to instantiate prefab: {ex.Message}");
            }
        }

        private static object UnpackPrefab(ToolParams p)
        {
            var targetIdentifier = p.RequireString("target");

            var go = ResolveGameObject(targetIdentifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound",
                    $"GameObject '{targetIdentifier}' not found.");
            }

            var prefabStatus = PrefabUtility.GetPrefabAssetType(go);
            if (prefabStatus == PrefabAssetType.NotAPrefab)
            {
                return new ErrorResponse("NotPrefabInstance",
                    $"GameObject '{go.name}' is not a prefab instance.");
            }

            try
            {
                var prefabPath = PrefabUtility.GetPrefabAssetPathOfLatestInstance(go);

                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

                return new SuccessResponse(
                    $"Prefab instance '{go.name}' unpacked.",
                    new
                    {
                        original_prefab = prefabPath,
                        unpacked_name = go.name,
                        instance_id = go.GetInstanceID(),
                    });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("UnpackFailed",
                    $"Failed to unpack prefab: {ex.Message}");
            }
        }

        private static object GetPrefabInfo(ToolParams p)
        {
            var targetIdentifier = p.GetString("target");
            var prefabPath = p.GetString("prefab_path");

            if (string.IsNullOrEmpty(targetIdentifier) && string.IsNullOrEmpty(prefabPath))
            {
                return new ErrorResponse("InvalidParameters",
                    "Either 'target' or 'prefab_path' is required for get_info.");
            }

            if (!string.IsNullOrEmpty(prefabPath))
            {
                return GetPrefabAssetInfo(prefabPath);
            }
            else
            {
                return GetPrefabInstanceInfo(targetIdentifier);
            }
        }

        private static object GetPrefabAssetInfo(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return new ErrorResponse("PrefabNotFound",
                    $"No prefab found at path '{prefabPath}'.");
            }

            var assetType = PrefabUtility.GetPrefabAssetType(prefab);
            var variants = PrefabUtility.GetAssetDefinition(prefab) != null
                ? GetPrefabVariants(prefabPath)
                : new List<object>();

            return new SuccessResponse($"Prefab info for '{prefab.name}'.", new
            {
                name = prefab.name,
                path = prefabPath,
                asset_type = assetType.ToString(),
                variants = variants,
                guid = AssetDatabase.AssetPathToGUID(prefabPath),
            });
        }

        private static object GetPrefabInstanceInfo(string targetIdentifier)
        {
            var go = ResolveGameObject(targetIdentifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound",
                    $"GameObject '{targetIdentifier}' not found.");
            }

            var assetType = PrefabUtility.GetPrefabAssetType(go);
            if (assetType == PrefabAssetType.NotAPrefab)
            {
                return new ErrorResponse("NotPrefabInstance",
                    $"GameObject '{go.name}' is not a prefab instance.");
            }

            var prefabPath = PrefabUtility.GetPrefabAssetPathOfLatestInstance(go);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
            var hasOverrides = PrefabUtility.HasPrefabInstanceAnyOverrides(go, false);

            return new SuccessResponse($"Prefab info for instance '{go.name}'.", new
            {
                name = go.name,
                instance_id = go.GetInstanceID(),
                prefab_path = prefabPath,
                prefab_name = source?.name ?? System.IO.Path.GetFileNameWithoutExtension(prefabPath),
                asset_type = assetType.ToString(),
                has_overrides = hasOverrides,
                scene = go.scene.name,
                is_model = assetType == PrefabAssetType.Model,
                is_regular = assetType == PrefabAssetType.Regular,
                is_variant = assetType == PrefabAssetType.Variant,
            });
        }

        private static object ApplyPrefab(ToolParams p)
        {
            var targetIdentifier = p.RequireString("target");

            var go = ResolveGameObject(targetIdentifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound",
                    $"GameObject '{targetIdentifier}' not found.");
            }

            try
            {
                PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
                return new SuccessResponse(
                    $"Overrides applied from '{go.name}' to its prefab.",
                    new { name = go.name, instance_id = go.GetInstanceID() });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("ApplyFailed",
                    $"Failed to apply prefab: {ex.Message}");
            }
        }

        private static object RevertPrefab(ToolParams p)
        {
            var targetIdentifier = p.RequireString("target");

            var go = ResolveGameObject(targetIdentifier);
            if (go == null)
            {
                return new ErrorResponse("GameObjectNotFound",
                    $"GameObject '{targetIdentifier}' not found.");
            }

            try
            {
                PrefabUtility.RevertPrefabInstance(go, InteractionMode.AutomatedAction);
                return new SuccessResponse(
                    $"'{go.name}' reverted to prefab defaults.",
                    new { name = go.name, instance_id = go.GetInstanceID() });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("RevertFailed",
                    $"Failed to revert prefab: {ex.Message}");
            }
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

        private static List<object> GetPrefabVariants(string prefabPath)
        {
            var variants = new List<object>();
            var guid = AssetDatabase.AssetPathToGUID(prefabPath);
            var allGuids = AssetDatabase.FindAssets("t:Prefab");

            foreach (var variantGuid in allGuids)
            {
                var variantPath = AssetDatabase.GUIDToAssetPath(variantGuid);
                if (variantPath == prefabPath) continue;

                var variant = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
                if (variant == null) continue;

                var source = PrefabUtility.GetCorrespondingObjectFromSource(variant);
                if (source != null && AssetDatabase.GetAssetPath(source) == prefabPath)
                {
                    variants.Add(new { path = variantPath, name = variant.name });
                }
            }

            return variants;
        }
    }
}
