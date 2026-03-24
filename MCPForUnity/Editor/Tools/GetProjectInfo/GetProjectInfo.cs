using System;
using System.Collections.Generic;
using System.Linq;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Returns project information including project name, path, Unity version,
    /// build target, scene list, tags/layers, and asset GUIDs (with pagination).
    /// </summary>
    [McpForUnityTool("get_project_info", Group = "core",
        Description = "Get information about the Unity project: name, path, Unity version, build target, scenes, tags, layers, and asset GUIDs.")]
    public static class GetProjectInfo
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var includeAssets = p.GetBool("include_assets", false);
                var assetFilter = p.GetString("asset_filter");
                var pageSize = p.GetInt("page_size", 100);
                var cursor = p.GetInt("cursor", 0);

                // Basic project info
                var projectPath = Application.dataPath.Replace("/Assets", "");
                var projectName = string.IsNullOrEmpty(Application.productName)
                    ? System.IO.Path.GetFileName(projectPath)
                    : Application.productName;

                var result = new Dictionary<string, object>
                {
                    ["project"] = new
                    {
                        name = projectName,
                        path = projectPath,
                        unity_version = Application.unityVersion,
                        platform = Application.platform.ToString(),
                        install_mode = Application.installMode.ToString(),
                    },
                    ["editor"] = new
                    {
                        is_playing = Application.isPlaying,
                        is_paused = Application.isPaused,
                        build_target = EditorUserBuildSettings.activeBuildTarget.ToString(),
                        build_target_group = EditorUserBuildSettings.selectedBuildTargetGroup.ToString(),
                    },
                    ["scenes"] = GetSceneList(),
                    ["tags"] = GetTagsList(),
                    ["layers"] = GetLayersList(),
                };

                // Asset GUIDs if requested
                if (includeAssets)
                {
                    result["assets"] = GetAssetGuids(assetFilter, pageSize, cursor, out int totalAssets);
                    result["assets_total"] = totalAssets;
                    result["assets_cursor"] = cursor;
                    result["assets_page_size"] = pageSize;
                }

                return new SuccessResponse($"Project info retrieved for '{projectName}'.", result);
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

        private static object GetSceneList()
        {
            var scenes = EditorBuildSettings.scenes;
            var sceneList = new List<object>();

            for (int i = 0; i < scenes.Length; i++)
            {
                var scene = scenes[i];
                string sceneName = string.IsNullOrEmpty(scene.path)
                    ? $"Unnamed Scene {i}"
                    : System.IO.Path.GetFileNameWithoutExtension(scene.path);

                sceneList.Add(new
                {
                    index = i,
                    name = sceneName,
                    path = scene.path,
                    enabled = scene.enabled,
                    guid = scene.guid.ToString(),
                });
            }

            // Also include open (loaded) scenes
            var openScenes = new List<object>();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                openScenes.Add(new
                {
                    name = s.name,
                    path = s.path,
                    build_index = s.buildIndex,
                    is_dirty = s.isDirty,
                    is_loaded = s.isLoaded,
                });
            }

            return new
            {
                in_build_settings = sceneList.Count,
                build_settings = sceneList,
                currently_open = openScenes,
            };
        }

        private static object GetTagsList()
        {
            var tags = new List<string>();

            // Unity's built-in tags are not directly accessible via API,
            // but we can return the known default tags
            var builtInTags = new[]
            {
                "Untagged", "Respawn", "Finish", "Player", "MainCamera",
                "GameController", "EditorOnly", "Default"
            };

            var tagList = new List<string>(builtInTags);

            // Note: Unity does not expose a public API to enumerate user-created tags.
            // We can only report known/built-in tags.
            return new
            {
                count = tagList.Count,
                tags = tagList,
                note = "Only built-in Unity tags are listed. User-created tags cannot be enumerated via the public API.",
            };
        }

        private static object GetLayersList()
        {
            var layers = new List<object>();

            for (int i = 0; i < 32; i++)
            {
                var layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    layers.Add(new
                    {
                        index = i,
                        name = layerName,
                    });
                }
            }

            // Also get tag-mask layer names from Physics/Physics2D/Rendering settings
            var builtinLayers = new List<string>
            {
                "Default", "TransparentFX", "IgnoreRaycast", "",
                "Water", "UI", "", "","", "", "", "", "", "", "", "",
            };

            return new
            {
                count = layers.Count,
                layers = layers,
            };
        }

        private static object GetAssetGuids(string filter, int pageSize, int cursor, out int total)
        {
            var guids = new List<string>();

            try
            {
                var searchInFolders = new[] { "Assets" };
                var filePaths = string.IsNullOrEmpty(filter)
                    ? AssetDatabase.FindAssets("", searchInFolders)
                    : AssetDatabase.FindAssets(filter, searchInFolders);

                total = filePaths.Length;

                var startIdx = Math.Min(cursor, total);
                var endIdx = Math.Min(cursor + pageSize, total);
                var pageGuids = filePaths.Length > 0
                    ? filePaths.Skip(startIdx).Take(endIdx - startIdx).ToList()
                    : new List<string>();

                var assetList = new List<object>();
                foreach (var guid in pageGuids)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath)) continue;

                    var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                    assetList.Add(new
                    {
                        guid = guid,
                        path = assetPath,
                        type = type?.Name ?? "Unknown",
                        size_bytes = GetAssetSize(assetPath),
                    });
                }

                return new
                {
                    total = total,
                    count = assetList.Count,
                    cursor = cursor,
                    page_size = pageSize,
                    has_more = endIdx < total,
                    next_cursor = endIdx < total ? endIdx : (int?)null,
                    assets = assetList,
                };
            }
            catch (Exception)
            {
                total = 0;
                return new
                {
                    total = 0,
                    count = 0,
                    cursor = cursor,
                    page_size = pageSize,
                    has_more = false,
                    next_cursor = (int?)null,
                    assets = new List<object>(),
                    error = "Failed to enumerate assets.",
                };
            }
        }

        private static long GetAssetSize(string assetPath)
        {
            try
            {
                var fullPath = System.IO.Path.Combine(Application.dataPath.Replace("/Assets", ""), assetPath);
                if (System.IO.File.Exists(fullPath))
                    return new System.IO.FileInfo(fullPath).Length;
            }
            catch { }
            return 0;
        }
    }
}
