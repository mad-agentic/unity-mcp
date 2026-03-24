using System;
using System.Collections.Generic;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Asset management tool supporting create, get, rename, delete, move, copy, find, and get_metadata.
    /// </summary>
    [McpForUnityTool("manage_asset", Group = "core",
        Description = "Manage Unity Assets: create, get, rename, delete, move, copy, find, and get metadata for project assets.")]
    public static class ManageAsset
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "find");

                switch (action.ToLowerInvariant())
                {
                    case "create":
                        return CreateAsset(p);
                    case "get":
                        return GetAsset(p);
                    case "rename":
                        return RenameAsset(p);
                    case "delete":
                        return DeleteAsset(p);
                    case "move":
                        return MoveAsset(p);
                    case "copy":
                        return CopyAsset(p);
                    case "find":
                        return FindAssets(p);
                    case "get_metadata":
                    case "getmetadata":
                        return GetMetadata(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: create, get, rename, delete, move, copy, find, get_metadata.");
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

        private static object CreateAsset(ToolParams p)
        {
            var name = p.RequireString("new_name");
            var typeFilter = p.GetString("type_filter", "Object");
            var targetPath = p.GetString("target_path", "Assets");

            targetPath = EnsureAssetPath(targetPath);
            if (!targetPath.EndsWith("/"))
                targetPath += "/";

            var assetType = ResolveType(typeFilter);
            UnityEngine.Object asset = null;

            if (assetType == null || assetType == typeof(UnityEngine.Object))
            {
                // Generic object — try Material, Shader, Texture based on extension
                if (name.EndsWith(".mat"))
                    asset = new Material(Shader.Find("Standard"));
                else if (name.EndsWith(".shader"))
                    asset = new Material(Shader.Find("Standard")); // placeholder
                else if (name.EndsWith(".png") || name.EndsWith(".jpg"))
                    asset = new Texture2D(256, 256);
                else
                    asset = ScriptableObject.CreateInstance("UnityEngine.Object" == typeFilter ? typeof(ScriptableObject) : typeof(UnityEngine.Object));
            }
            else
            {
                asset = CreateInstanceOfType(assetType);
            }

            if (asset == null)
                return new ErrorResponse("CreationFailed", $"Could not create asset of type '{typeFilter}'.");

            asset.name = System.IO.Path.GetFileNameWithoutExtension(name);

            var fullPath = targetPath + name;
            var created = AssetDatabase.CreateAsset(asset, fullPath);

            return new SuccessResponse($"Asset '{name}' created at '{fullPath}'.", new
            {
                name = asset.name,
                instance_id = asset.GetInstanceID(),
                type = asset.GetType().Name,
                asset_path = fullPath,
            });
        }

        private static object GetAsset(ToolParams p)
        {
            var assetPath = p.RequireString("asset_path");
            assetPath = NormalizeAssetPath(assetPath);

            if (!AssetDatabase.Contains(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath)))
                return new ErrorResponse("AssetNotFound", $"Asset not found at path: {assetPath}");

            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            var includeMetadata = p.GetBool("include_metadata", false);

            var data = new Dictionary<string, object>
            {
                { "name", obj.name },
                { "instance_id", obj.GetInstanceID() },
                { "type", obj.GetType().Name },
                { "asset_path", assetPath },
            };

            if (includeMetadata)
            {
                data["guid"] = AssetDatabase.AssetPathToGUID(assetPath);
                data["last_modified"] = System.IO.File.GetLastWriteTime(assetPath).ToString("o");
            }

            return new SuccessResponse($"Asset '{obj.name}' info retrieved.", data);
        }

        private static object RenameAsset(ToolParams p)
        {
            var assetPath = p.RequireString("asset_path");
            assetPath = NormalizeAssetPath(assetPath);
            var newName = p.RequireString("new_name");

            if (!AssetDatabase.Contains(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath)))
                return new ErrorResponse("AssetNotFound", $"Asset not found at path: {assetPath}");

            var oldName = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath).name;
            var result = AssetDatabase.RenameAsset(assetPath, newName);

            if (!string.IsNullOrEmpty(result))
                return new ErrorResponse("RenameFailed", result);

            var newPath = System.IO.Path.GetDirectoryName(assetPath).Replace("\\", "/");
            if (!newPath.EndsWith("/") && !string.IsNullOrEmpty(newPath))
                newPath += "/";
            newPath += newName + System.IO.Path.GetExtension(assetPath);

            return new SuccessResponse($"Asset renamed from '{oldName}' to '{newName}'.", new
            {
                old_name = oldName,
                new_name = newName,
                old_path = assetPath,
                new_path = newPath,
            });
        }

        private static object DeleteAsset(ToolParams p)
        {
            var assetPath = p.RequireString("asset_path");
            assetPath = NormalizeAssetPath(assetPath);

            if (!AssetDatabase.Contains(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath)))
                return new ErrorResponse("AssetNotFound", $"Asset not found at path: {assetPath}");

            var name = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath).name;
            var result = AssetDatabase.DeleteAsset(assetPath);

            if (!string.IsNullOrEmpty(result))
                return new ErrorResponse("DeleteFailed", result);

            return new SuccessResponse($"Asset '{name}' deleted.", new
            {
                asset_path = assetPath,
            });
        }

        private static object MoveAsset(ToolParams p)
        {
            var assetPath = p.RequireString("asset_path");
            assetPath = NormalizeAssetPath(assetPath);
            var targetPath = p.RequireString("target_path");

            if (!AssetDatabase.Contains(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath)))
                return new ErrorResponse("AssetNotFound", $"Asset not found at path: {assetPath}");

            targetPath = EnsureAssetPath(targetPath);
            var fileName = System.IO.Path.GetFileName(assetPath);
            var result = AssetDatabase.MoveAsset(assetPath, targetPath + "/" + fileName);

            if (!string.IsNullOrEmpty(result))
                return new ErrorResponse("MoveFailed", result);

            return new SuccessResponse($"Asset moved from '{assetPath}' to '{targetPath}/{fileName}'.", new
            {
                old_path = assetPath,
                new_path = targetPath + "/" + fileName,
            });
        }

        private static object CopyAsset(ToolParams p)
        {
            var assetPath = p.RequireString("asset_path");
            assetPath = NormalizeAssetPath(assetPath);
            var targetPath = p.RequireString("target_path");

            if (!AssetDatabase.Contains(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath)))
                return new ErrorResponse("AssetNotFound", $"Asset not found at path: {assetPath}");

            targetPath = EnsureAssetPath(targetPath);
            var fileName = System.IO.Path.GetFileName(assetPath);
            var result = AssetDatabase.CopyAsset(assetPath, targetPath + "/" + fileName);

            if (!result)
                return new ErrorResponse("CopyFailed", $"Failed to copy asset from '{assetPath}' to '{targetPath}/{fileName}'.");

            return new SuccessResponse($"Asset copied from '{assetPath}' to '{targetPath}/{fileName}'.", new
            {
                source_path = assetPath,
                target_path = targetPath + "/" + fileName,
            });
        }

        private static object FindAssets(ToolParams p)
        {
            var typeFilter = p.GetString("type_filter");
            var searchQuery = p.GetString("search_query");
            var pageSize = p.GetInt("page_size") ?? 100;
            var cursor = p.GetInt("cursor") ?? 0;

            // Build filter string for AssetDatabase.FindAssets
            var filter = new List<string>();
            if (!string.IsNullOrEmpty(typeFilter))
            {
                filter.Add("t:" + typeFilter);
            }
            if (!string.IsNullOrEmpty(searchQuery))
            {
                filter.Add(searchQuery);
            }

            var searchFilter = string.Join(" ", filter);

            string[] guids;
            if (string.IsNullOrEmpty(searchFilter))
            {
                // List all assets
                guids = AssetDatabase.FindAssets("");
            }
            else
            {
                guids = AssetDatabase.FindAssets(searchFilter);
            }

            var total = guids.Length;
            var startIdx = Math.Min(cursor, total);
            var endIdx = Math.Min(cursor + pageSize, total);
            var pageGuids = new string[endIdx - startIdx];
            Array.Copy(guids, startIdx, pageGuids, 0, endIdx - startIdx);

            var results = new List<object>();
            foreach (var guid in pageGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (obj == null) continue;

                results.Add(new
                {
                    name = obj.name,
                    instance_id = obj.GetInstanceID(),
                    type = obj.GetType().Name,
                    asset_path = path,
                    guid = guid,
                });
            }

            return new SuccessResponse(
                $"Found {total} asset(s), returning {results.Count} (cursor={cursor}, page_size={pageSize}).",
                new
                {
                    total = total,
                    page_size = pageSize,
                    cursor = cursor,
                    count = results.Count,
                    has_more = endIdx < total,
                    next_cursor = endIdx < total ? endIdx : (int?)null,
                    assets = results,
                });
        }

        private static object GetMetadata(ToolParams p)
        {
            var assetPath = p.RequireString("asset_path");
            assetPath = NormalizeAssetPath(assetPath);

            if (!AssetDatabase.Contains(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath)))
                return new ErrorResponse("AssetNotFound", $"Asset not found at path: {assetPath}");

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath);

            var fileInfo = new System.IO.FileInfo(assetPath);

            var metadata = new Dictionary<string, object>
            {
                { "name", obj.name },
                { "instance_id", obj.GetInstanceID() },
                { "type", obj.GetType().Name },
                { "asset_path", assetPath },
                { "guid", guid },
                { "file_size", GetFileSize(assetPath) },
                { "last_modified", GetLastModified(assetPath) },
                { "is_builtin", IsBuiltinAsset(assetPath) },
                { "is_directory", AssetDatabase.IsValidFolder(assetPath) },
            };

            if (importer != null)
            {
                metadata["importer_type"] = importer.GetType().Name;
            }

            return new SuccessResponse($"Metadata for '{obj.name}' retrieved.", metadata);
        }

        // --- Helpers ---

        private static UnityEngine.Object CreateInstanceOfType(Type type)
        {
            if (type == null || type == typeof(UnityEngine.Object))
                return ScriptableObject.CreateInstance<ScriptableObject>();

            if (type.IsSubclassOf(typeof(ScriptableObject)))
                return ScriptableObject.CreateInstance(type);

            // Cannot instantiate Monobehaviour or other non-ScriptableObject types at runtime
            if (!type.IsSubclassOf(typeof(Component)) && !type.IsAbstract)
            {
                try
                {
                    return Activator.CreateInstance(type) as UnityEngine.Object;
                }
                catch { }
            }

            // Return a ScriptableObject as a fallback
            return ScriptableObject.CreateInstance<ScriptableObject>();
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return typeof(UnityEngine.Object);

            // Try exact match in UnityEngine
            var unityAsm = typeof(UnityEngine.Object).Assembly;
            var t = unityAsm.GetType("UnityEngine." + typeName);
            if (t != null) return t;

            // Try full name
            t = unityAsm.GetType(typeName);
            if (t != null) return t;

            // Search loaded assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                t = asm.GetType(typeName);
                if (t != null) return t;
                t = asm.GetType("UnityEngine." + typeName);
                if (t != null) return t;
            }

            return typeof(UnityEngine.Object);
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            // Ensure Assets/ prefix
            if (!path.StartsWith("Assets"))
                path = "Assets/" + path;
            return path.Replace("\\", "/");
        }

        private static string EnsureAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "Assets";
            return path.StartsWith("Assets") ? path : "Assets/" + path;
        }

        private static long GetFileSize(string path)
        {
            try
            {
                var info = new System.IO.FileInfo(path);
                return info.Exists ? info.Length : 0;
            }
            catch { return 0; }
        }

        private static string GetLastModified(string path)
        {
            try
            {
                return System.IO.File.GetLastWriteTime(path).ToString("o");
            }
            catch { return null; }
        }

        private static bool IsBuiltinAsset(string path)
        {
            return path.StartsWith("Resources/unity_builtin") ||
                   path.StartsWith("Library/unity default resources");
        }
    }
}
