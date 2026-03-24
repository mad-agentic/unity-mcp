using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Helper methods for resolving GameObjects by various identifiers (name, path, instanceID).
    /// </summary>
    public static class GameObjectHelpers
    {
        /// <summary>
        /// Resolve a GameObject from a flexible identifier:
        /// - If parseable as int, treat as InstanceID
        /// - If contains '/', treat as scene path
        /// - Otherwise, treat as name (with exact and partial match fallback)
        /// </summary>
        public static GameObject ResolveGameObject(string identifier, SceneManagement.Scene? restrictToScene = null)
        {
            if (string.IsNullOrEmpty(identifier))
                return null;

            // Try InstanceID first
            if (int.TryParse(identifier, out int instanceId))
            {
                var obj = EditorUtility.InstanceIDToObject(instanceId);
                return obj as GameObject;
            }

            // Try by path
            var go = ResolveByPath(identifier, restrictToScene);
            if (go != null) return go;

            // Try exact name match
            go = ResolveByName(identifier, exact: true, restrictToScene: restrictToScene);
            if (go != null) return go;

            // Try partial name match
            return ResolveByName(identifier, exact: false, restrictToScene: restrictToScene);
        }

        /// <summary>
        /// Resolve a GameObject by its scene hierarchy path (e.g., "GameObject/Child/GrandChild").
        /// </summary>
        public static GameObject ResolveByPath(string path, SceneManagement.Scene? restrictToScene = null)
        {
            if (string.IsNullOrEmpty(path) || !path.Contains("/"))
                return null;

            var scenes = SceneManagement.SceneManager.scenes;
            foreach (var scene in scenes)
            {
                if (restrictToScene.HasValue && scene != restrictToScene.Value)
                    continue;

                var rootObjects = scene.GetRootGameObjects();
                var result = FindByPath(path, rootObjects);
                if (result != null) return result;
            }
            return null;
        }

        private static GameObject FindByPath(string path, GameObject[] rootObjects)
        {
            var parts = path.Split('/');
            GameObject current = null;

            foreach (var root in rootObjects)
            {
                if (root.name == parts[0])
                {
                    current = root;
                    break;
                }
            }

            if (current == null) return null;

            for (int i = 1; i < parts.Length; i++)
            {
                var childName = parts[i];
                Transform child = null;
                for (int j = 0; j < current.transform.childCount; j++)
                {
                    if (current.transform.GetChild(j).name == childName)
                    {
                        child = current.transform.GetChild(j);
                        break;
                    }
                }
                if (child == null) return null;
                current = child.gameObject;
            }

            return current;
        }

        /// <summary>
        /// Resolve GameObject(s) by name. If exact is true, returns first exact match.
        /// </summary>
        public static GameObject ResolveByName(string name, bool exact, SceneManagement.Scene? restrictToScene = null)
        {
            var results = FindByName(name, exact, restrictToScene);
            return results.FirstOrDefault();
        }

        /// <summary>
        /// Find all GameObjects matching a name (partial match by default).
        /// </summary>
        public static List<GameObject> FindByName(string name, bool exact = false, SceneManagement.Scene? restrictToScene = null)
        {
            var results = new List<GameObject>();

            if (restrictToScene.HasValue)
            {
                FindInScene(results, restrictToScene.Value, go =>
                    exact ? go.name == name : go.name.Contains(name));
            }
            else
            {
                for (int i = 0; i < SceneManagement.SceneManager.sceneCount; i++)
                {
                    var scene = SceneManagement.SceneManager.GetSceneAt(i);
                    FindInScene(results, scene, go =>
                        exact ? go.name == name : go.name.Contains(name));
                }
            }

            return results;
        }

        /// <summary>
        /// Find GameObjects by tag.
        /// </summary>
        public static List<GameObject> FindByTag(string tag, SceneManagement.Scene? restrictToScene = null)
        {
            var results = new List<GameObject>();

            try
            {
                GameObject[] tagged = GameObject.FindGameObjectsWithTag(tag);
                foreach (var go in tagged)
                {
                    if (restrictToScene.HasValue && go.scene != restrictToScene.Value)
                        continue;
                    results.Add(go);
                }
            }
            catch (UnityException)
            {
                // Tag not defined — return empty
            }

            return results;
        }

        /// <summary>
        /// Find GameObjects by layer number or name.
        /// </summary>
        public static List<GameObject> FindByLayer(int layer, SceneManagement.Scene? restrictToScene = null)
        {
            var results = new List<GameObject>();

            if (restrictToScene.HasValue)
            {
                FindInScene(results, restrictToScene.Value, go => go.layer == layer);
            }
            else
            {
                for (int i = 0; i < SceneManagement.SceneManager.sceneCount; i++)
                {
                    var scene = SceneManagement.SceneManager.GetSceneAt(i);
                    FindInScene(results, scene, go => go.layer == layer);
                }
            }

            return results;
        }

        /// <summary>
        /// Find GameObjects by layer name.
        /// </summary>
        public static List<GameObject> FindByLayerName(string layerName, SceneManagement.Scene? restrictToScene = null)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer == -1)
                return new List<GameObject>();
            return FindByLayer(layer, restrictToScene);
        }

        /// <summary>
        /// Find GameObjects that have a component of the specified type name.
        /// </summary>
        public static List<GameObject> FindByComponent(string componentTypeName, SceneManagement.Scene? restrictToScene = null)
        {
            var results = new List<GameObject>();
            var assembly = typeof(Component).Assembly;

            Type componentType = null;
            foreach (var assemblyName in new[] { "UnityEngine", "UnityEngine.UI", "UnityEngine.UIElementsModule" })
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => !a.IsDynamic && a.GetName().Name == assemblyName);
                if (asm != null)
                {
                    componentType = asm.GetType($"UnityEngine.{componentTypeName}");
                    if (componentType != null) break;
                    componentType = asm.GetTypes().FirstOrDefault(t => t.Name == componentTypeName);
                    if (componentType != null) break;
                }
            }

            if (componentType == null)
            {
                foreach (var assembly2 in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly2.IsDynamic) continue;
                    componentType = assembly2.GetTypes().FirstOrDefault(t => t.Name == componentTypeName);
                    if (componentType != null) break;
                }
            }

            if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
                return results;

            var finderType = typeof(GameObjectFinder<>).MakeGenericType(componentType);
            var method = finderType.GetMethod("Find");
            if (method != null)
            {
                var found = method.Invoke(null, new object[] { restrictToScene }) as IEnumerable<GameObject>;
                if (found != null)
                    results.AddRange(found);
            }

            return results;
        }

        private static void FindInScene(List<GameObject> results, SceneManagement.Scene scene, Predicate<GameObject> predicate)
        {
            if (!scene.isLoaded) return;
            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                if (predicate(root))
                    results.Add(root);
                TraverseChildren(root.transform, predicate, results);
            }
        }

        private static void TraverseChildren(Transform parent, Predicate<GameObject> predicate, List<GameObject> results)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (predicate(child.gameObject))
                    results.Add(child.gameObject);
                TraverseChildren(child, predicate, results);
            }
        }

        /// <summary>
        /// Get the scene path of a GameObject (relative to its scene root).
        /// </summary>
        public static string GetScenePath(GameObject go)
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

        /// <summary>
        /// Get all component names attached to a GameObject.
        /// </summary>
        public static List<string> GetComponentNames(GameObject go)
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

        /// <summary>
        /// Try to parse a primitive type string into a Unity PrimitiveType.
        /// </summary>
        public static bool TryParsePrimitiveType(string value, out PrimitiveType primitiveType)
        {
            return Enum.TryParse(value, true, out primitiveType);
        }
    }

    /// <summary>
    /// Generic helper to find GameObjects with a specific component type.
    /// Uses reflection-based invocation via GameObjectHelpers.FindByComponent.
    /// </summary>
    public static class GameObjectFinder<T> where T : Component
    {
        public static IEnumerable<GameObject> Find(SceneManagement.Scene? restrictToScene)
        {
            var results = new List<GameObject>();

            if (restrictToScene.HasValue)
            {
                var objects = SceneManagement.SceneManager.GetSceneByHandle(restrictToScene.Value.handle)
                    .GetRootGameObjects();
                foreach (var obj in objects)
                {
                    CollectWithComponent(obj, results);
                }
            }
            else
            {
                var allObjects = Resources.FindObjectsOfTypeAll<T>();
                foreach (var comp in allObjects)
                {
                    if (comp != null && comp.gameObject != null)
                    {
                        // Skip prefab objects that are not in a scene
                        if (string.IsNullOrEmpty(comp.gameObject.scene.name) &&
                            !EditorUtility.IsPersistent(comp.gameObject))
                        {
                            results.Add(comp.gameObject);
                        }
                    }
                }
            }

            return results;
        }

        private static void CollectWithComponent(GameObject go, List<GameObject> results)
        {
            if (go.GetComponent<T>() != null)
                results.Add(go);
            for (int i = 0; i < go.transform.childCount; i++)
                CollectWithComponent(go.transform.GetChild(i).gameObject, results);
        }
    }
}
