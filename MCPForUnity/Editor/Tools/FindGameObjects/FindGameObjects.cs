using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Search tool that finds GameObjects by name (with wildcard pattern support),
    /// tag, layer, path, component type, or InstanceID. Supports pagination.
    /// </summary>
    [McpForUnityTool("find_gameobjects", group = "core",
        description = "Find GameObjects by name pattern, tag, layer, path, component, or InstanceID. Supports wildcard patterns and pagination.")]
    public static class FindGameObjects
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);

                var name = p.GetString("name");
                var tag = p.GetString("tag");
                var layer = p.GetString("layer");
                var path = p.GetString("path");
                var component = p.GetString("component");
                var instanceId = p.GetInt("instance_id");
                var sceneName = p.GetString("scene");
                var activeOnly = p.GetBool("active_only", false);
                var pageSize = p.GetInt("page_size", 100);
                var cursor = p.GetInt("cursor", 0);

                // Validate at least one search criterion is provided
                if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(tag) &&
                    string.IsNullOrEmpty(layer) && string.IsNullOrEmpty(path) &&
                    string.IsNullOrEmpty(component) && !instanceId.HasValue)
                {
                    return new ErrorResponse("InvalidParameters",
                        "At least one search criterion is required: name, tag, layer, path, component, or instance_id.");
                }

                // Determine search scope
                IEnumerable<Scene> searchScenes;
                if (!string.IsNullOrEmpty(sceneName))
                {
                    var scene = SceneManager.GetSceneByName(sceneName);
                    if (!scene.IsValid())
                    {
                        return new ErrorResponse("SceneNotFound", $"Scene '{sceneName}' not found.");
                    }
                    searchScenes = new[] { scene };
                }
                else
                {
                    searchScenes = Enumerable.Range(0, SceneManager.sceneCount)
                        .Select(i => SceneManager.GetSceneAt(i))
                        .Where(s => s.isLoaded);
                }

                // Perform search
                List<GameObject> results;
                if (instanceId.HasValue)
                {
                    var obj = EditorUtility.InstanceIDToObject(instanceId.Value);
                    var go = obj as GameObject;
                    results = go != null ? new List<GameObject> { go } : new List<GameObject>();
                }
                else if (!string.IsNullOrEmpty(path))
                {
                    results = FindByPath(path, searchScenes);
                }
                else if (!string.IsNullOrEmpty(tag))
                {
                    results = FindByTag(tag, searchScenes, activeOnly);
                }
                else if (!string.IsNullOrEmpty(layer))
                {
                    results = FindByLayer(layer, searchScenes, activeOnly);
                }
                else if (!string.IsNullOrEmpty(component))
                {
                    results = FindByComponent(component, searchScenes, activeOnly);
                }
                else
                {
                    results = FindByName(name, searchScenes, activeOnly);
                }

                // Apply active filter if not already filtered by specific criteria
                if (activeOnly && !string.IsNullOrEmpty(name))
                {
                    results = results.Where(go => go.activeInHierarchy).ToList();
                }

                var total = results.Count;
                var startIdx = Math.Min(cursor, total);
                var endIdx = Math.Min(cursor + pageSize, total);
                var page = results.GetRange(startIdx, endIdx - startIdx);

                var pageData = page.Select(go => new
                {
                    name = go.name,
                    instance_id = go.GetInstanceID(),
                    scene = go.scene.name,
                    scene_path = GetScenePath(go),
                    tag = go.tag,
                    layer = go.layer,
                    layer_name = LayerMask.LayerToName(go.layer),
                    active_self = go.activeSelf,
                    active_in_hierarchy = go.activeInHierarchy,
                    is_static = go.isStatic,
                    transform = new
                    {
                        position = new[] { go.transform.position.x, go.transform.position.y, go.transform.position.z },
                        child_count = go.transform.childCount,
                    },
                    components = GetComponentNames(go),
                }).ToList<object>();

                return new SuccessResponse(
                    $"Found {total} GameObject(s), returning {pageData.Count} (cursor={cursor}, page_size={pageSize}).",
                    new
                    {
                        total = total,
                        page_size = pageSize,
                        cursor = cursor,
                        count = pageData.Count,
                        has_more = endIdx < total,
                        next_cursor = endIdx < total ? endIdx : (int?)null,
                        game_objects = pageData,
                    });
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

        private static List<GameObject> FindByName(string namePattern, IEnumerable<Scene> scenes, bool activeOnly)
        {
            var results = new List<GameObject>();

            bool isWildcard = namePattern.Contains("*") || namePattern.Contains("?");

            if (isWildcard)
            {
                var regex = WildcardToRegex(namePattern);
                var regexObj = new Regex(regex, RegexOptions.IgnoreCase);

                foreach (var scene in scenes)
                {
                    FindInScene(scene, go =>
                    {
                        if (regexObj.IsMatch(go.name))
                        {
                            if (!activeOnly || go.activeInHierarchy)
                                results.Add(go);
                        }
                        return false;
                    });
                }
            }
            else
            {
                foreach (var scene in scenes)
                {
                    FindInScene(scene, go =>
                    {
                        if (go.name.IndexOf(namePattern, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (!activeOnly || go.activeInHierarchy)
                                results.Add(go);
                        }
                        return false;
                    });
                }
            }

            return results;
        }

        private static List<GameObject> FindByPath(string path, IEnumerable<Scene> scenes)
        {
            var results = new List<GameObject>();
            foreach (var scene in scenes)
            {
                var rootObjects = scene.GetRootGameObjects();
                var found = FindByPathRecursive(path, rootObjects);
                if (found != null)
                    results.Add(found);
            }
            return results;
        }

        private static GameObject FindByPathRecursive(string path, GameObject[] roots)
        {
            var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            foreach (var root in roots)
            {
                if (root.name != parts[0]) continue;

                if (parts.Length == 1)
                    return root;

                var current = root.transform;
                var found = true;

                for (int i = 1; i < parts.Length; i++)
                {
                    var childFound = false;
                    for (int j = 0; j < current.childCount; j++)
                    {
                        if (current.GetChild(j).name == parts[i])
                        {
                            current = current.GetChild(j);
                            childFound = true;
                            break;
                        }
                    }
                    if (!childFound) { found = false; break; }
                }

                if (found) return current.gameObject;
            }

            return null;
        }

        private static List<GameObject> FindByTag(string tag, IEnumerable<Scene> scenes, bool activeOnly)
        {
            var results = new List<GameObject>();
            try
            {
                var tagged = GameObject.FindGameObjectsWithTag(tag);
                var sceneSet = scenes.Select(s => s.handle).ToHashSet();

                foreach (var go in tagged)
                {
                    if (!sceneSet.Contains(go.scene.handle))
                        continue;
                    if (activeOnly && !go.activeInHierarchy)
                        continue;
                    results.Add(go);
                }
            }
            catch (UnityException)
            {
                // Tag not defined
            }
            return results;
        }

        private static List<GameObject> FindByLayer(string layerValue, IEnumerable<Scene> scenes, bool activeOnly)
        {
            int layer;
            if (int.TryParse(layerValue, out layer))
            {
                // layer number provided
            }
            else
            {
                layer = LayerMask.NameToLayer(layerValue);
                if (layer == -1)
                    return new List<GameObject>();
            }

            var results = new List<GameObject>();
            foreach (var scene in scenes)
            {
                FindInScene(scene, go =>
                {
                    if (go.layer == layer && (!activeOnly || go.activeInHierarchy))
                        results.Add(go);
                    return false;
                });
            }
            return results;
        }

        private static List<GameObject> FindByComponent(string componentTypeName, IEnumerable<Scene> scenes, bool activeOnly)
        {
            var results = new List<GameObject>();

            // Find the component type across loaded assemblies
            Type componentType = FindType(componentTypeName);
            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
                return results;

            // Use FindObjectsOfTypeAll to find objects with the component
            var allWithComponent = Resources.FindObjectsOfTypeAll(componentType);
            var sceneSet = scenes.Select(s => s.handle).ToHashSet();

            foreach (var comp in allWithComponent)
            {
                if (comp == null) continue;
                var component = comp as Component;
                if (component == null) continue;
                var go = component.gameObject;
                if (!sceneSet.Contains(go.scene.handle))
                    continue;
                if (activeOnly && !go.activeInHierarchy)
                    continue;
                // Skip prefab assets (not in a scene)
                if (string.IsNullOrEmpty(go.scene.name) && EditorUtility.IsPersistent(go))
                    continue;
                results.Add(go);
            }

            return results;
        }

        private static Type FindType(string typeName)
        {
            // Search in common Unity assemblies
            foreach (var assemblyName in new[]
            {
                "UnityEngine", "UnityEngine.CoreModule", "UnityEngine.UI",
                "UnityEngine.UIModule", "UnityEngine.PhysicsModule",
                "UnityEngine.AnimationModule"
            })
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => !a.IsDynamic && a.GetName().Name == assemblyName);
                if (assembly != null)
                {
                    // Try full name first
                    var t = assembly.GetType($"UnityEngine.{typeName}");
                    if (t != null) return t;

                    // Try name match
                    t = assembly.GetTypes().FirstOrDefault(type => type.Name == typeName);
                    if (t != null) return t;
                }
            }

            // Broader search
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;
                var t = assembly.GetTypes().FirstOrDefault(type => type.Name == typeName);
                if (t != null && typeof(Component).IsAssignableFrom(t))
                    return t;
            }

            return null;
        }

        private static void FindInScene(Scene scene, Func<GameObject, bool> predicate)
        {
            if (!scene.isLoaded) return;
            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                if (predicate(root))
                    return;
                TraverseChildren(root.transform, predicate);
            }
        }

        private static void TraverseChildren(Transform parent, Func<GameObject, bool> predicate)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (predicate(child.gameObject))
                    return;
                TraverseChildren(child, predicate);
            }
        }

        private static string GetScenePath(GameObject go)
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

        private static List<string> GetComponentNames(GameObject go)
        {
            var components = go.GetComponents<Component>();
            var names = new List<string>();
            foreach (var c in components)
            {
                if (c != null)
                    names.Add(c.GetType().Name);
            }
            return names;
        }

        private static string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
        }
    }
}
