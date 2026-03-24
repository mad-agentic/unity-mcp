using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Tool for managing Unity components: add, remove, get, set/get properties, list types.
    /// </summary>
    [McpForUnityTool("manage_components", group = "core",
        description = "Manage Unity components: add, remove, get info, set/get properties dynamically, list types, check existence.")]
    public static class ManageComponents
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "get");

                switch (action.ToLowerInvariant())
                {
                    case "add":
                        return AddComponent(p);
                    case "remove":
                        return RemoveComponent(p);
                    case "get":
                        return GetComponents(p);
                    case "set_property":
                    case "setproperty":
                        return SetProperty(p);
                    case "get_property":
                    case "getproperty":
                        return GetProperty(p);
                    case "list_types":
                    case "listtypes":
                        return ListComponentTypes(p);
                    case "has":
                        return HasComponent(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: add, remove, get, set_property, get_property, list_types, has.");
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

        private static object AddComponent(ToolParams p)
        {
            var gameObjectId = p.RequireString("gameobject");
            var componentTypeName = p.RequireString("component_type");

            var go = ResolveGameObject(gameObjectId);
            if (go == null)
                return new ErrorResponse("GameObjectNotFound", $"GameObject '{gameObjectId}' not found.");

            var componentType = ResolveComponentType(componentTypeName);
            if (componentType == null)
                return new ErrorResponse("ComponentTypeNotFound", $"Component type '{componentTypeName}' not found.");

            // Check if already exists
            var existing = go.GetComponent(componentType);
            if (existing != null)
                return new ErrorResponse("ComponentExists", $"Component '{componentTypeName}' already exists on '{go.name}'.");

            var component = go.AddComponent(componentType);

            return new SuccessResponse($"Component '{componentTypeName}' added to '{go.name}'.", new
            {
                gameobject = go.name,
                instance_id = go.GetInstanceID(),
                component_type = componentTypeName,
                component_instance_id = component.GetInstanceID(),
            });
        }

        private static object RemoveComponent(ToolParams p)
        {
            var gameObjectId = p.RequireString("gameobject");
            var componentTypeName = p.RequireString("component_type");

            var go = ResolveGameObject(gameObjectId);
            if (go == null)
                return new ErrorResponse("GameObjectNotFound", $"GameObject '{gameObjectId}' not found.");

            var componentType = ResolveComponentType(componentTypeName);
            if (componentType == null)
                return new ErrorResponse("ComponentTypeNotFound", $"Component type '{componentTypeName}' not found.");

            var component = go.GetComponent(componentType);
            if (component == null)
                return new ErrorResponse("ComponentNotFound", $"Component '{componentTypeName}' not found on '{go.name}'.");

            var removed = component;
            UnityEngine.Object.DestroyImmediate(component);

            return new SuccessResponse($"Component '{componentTypeName}' removed from '{go.name}'.", new
            {
                gameobject = go.name,
                component_type = componentTypeName,
            });
        }

        private static object GetComponents(ToolParams p)
        {
            var gameObjectId = p.RequireString("gameobject");

            var go = ResolveGameObject(gameObjectId);
            if (go == null)
                return new ErrorResponse("GameObjectNotFound", $"GameObject '{gameObjectId}' not found.");

            var components = go.GetComponents<Component>();
            var result = new List<object>();

            foreach (var comp in components)
            {
                if (comp == null) continue;
                result.Add(new
                {
                    type = comp.GetType().Name,
                    full_type = comp.GetType().FullName,
                    instance_id = comp.GetInstanceID(),
                });
            }

            return new SuccessResponse($"Found {result.Count} component(s) on '{go.name}'.", new
            {
                gameobject = go.name,
                instance_id = go.GetInstanceID(),
                count = result.Count,
                components = result,
            });
        }

        private static object SetProperty(ToolParams p)
        {
            var gameObjectId = p.RequireString("gameobject");
            var componentTypeName = p.RequireString("component_type");
            var propertyName = p.RequireString("property_name");
            var valueToken = p.GetObject("property_value");
            var rawValue = valueToken?.ToString();

            var go = ResolveGameObject(gameObjectId);
            if (go == null)
                return new ErrorResponse("GameObjectNotFound", $"GameObject '{gameObjectId}' not found.");

            var componentType = ResolveComponentType(componentTypeName);
            if (componentType == null)
                return new ErrorResponse("ComponentTypeNotFound", $"Component type '{componentTypeName}' not found.");

            var component = go.GetComponent(componentType);
            if (component == null)
                return new ErrorResponse("ComponentNotFound", $"Component '{componentTypeName}' not found on '{go.name}'.");

            var propInfo = FindProperty(componentType, propertyName);
            var fieldInfo = FindField(componentType, propertyName);

            if (propInfo == null && fieldInfo == null)
                return new ErrorResponse("PropertyNotFound", $"Property or field '{propertyName}' not found on '{componentTypeName}'.");

            object convertedValue = null;
            Type targetType = propInfo?.PropertyType ?? fieldInfo?.FieldType;

            if (propInfo != null)
            {
                convertedValue = ConvertValue(rawValue, propInfo.PropertyType);
                propInfo.SetValue(component, convertedValue);
            }
            else if (fieldInfo != null)
            {
                convertedValue = ConvertValue(rawValue, fieldInfo.FieldType);
                fieldInfo.SetValue(component, convertedValue);
            }

            return new SuccessResponse($"Property '{propertyName}' set on '{componentTypeName}' of '{go.name}'.", new
            {
                gameobject = go.name,
                component_type = componentTypeName,
                property_name = propertyName,
                value = rawValue,
                converted_value = convertedValue,
            });
        }

        private static object GetProperty(ToolParams p)
        {
            var gameObjectId = p.RequireString("gameobject");
            var componentTypeName = p.RequireString("component_type");
            var propertyName = p.RequireString("property_name");

            var go = ResolveGameObject(gameObjectId);
            if (go == null)
                return new ErrorResponse("GameObjectNotFound", $"GameObject '{gameObjectId}' not found.");

            var componentType = ResolveComponentType(componentTypeName);
            if (componentType == null)
                return new ErrorResponse("ComponentTypeNotFound", $"Component type '{componentTypeName}' not found.");

            var component = go.GetComponent(componentType);
            if (component == null)
                return new ErrorResponse("ComponentNotFound", $"Component '{componentTypeName}' not found on '{go.name}'.");

            var propInfo = FindProperty(componentType, propertyName);
            var fieldInfo = FindField(componentType, propertyName);

            if (propInfo == null && fieldInfo == null)
                return new ErrorResponse("PropertyNotFound", $"Property or field '{propertyName}' not found on '{componentTypeName}'.");

            object value = null;
            Type valueType = null;

            if (propInfo != null)
            {
                value = propInfo.GetValue(component);
                valueType = propInfo.PropertyType;
            }
            else if (fieldInfo != null)
            {
                value = fieldInfo.GetValue(component);
                valueType = fieldInfo.FieldType;
            }

            return new SuccessResponse($"Property '{propertyName}' on '{componentTypeName}' of '{go.name}': {value}.", new
            {
                gameobject = go.name,
                component_type = componentTypeName,
                property_name = propertyName,
                property_type = valueType?.Name,
                value = value != null ? value.ToString() : null,
            });
        }

        private static object ListComponentTypes(ToolParams p)
        {
            var searchNamespace = p.GetString("namespace");
            var searchPattern = p.GetString("pattern");
            var pageSize = p.GetInt("page_size", 50);
            var cursor = p.GetInt("cursor", 0);

            var componentTypes = new List<string>();

            var assemblies = new[]
            {
                "UnityEngine",
                "UnityEngine.UI",
                "UnityEngine.AudioModule",
                "UnityEngine.PhysicsModule",
                "UnityEngine.Rendering",
            };

            foreach (var asmName in assemblies)
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => !a.IsDynamic && a.GetName().Name == asmName);
                if (asm == null) continue;

                foreach (var type in asm.GetTypes())
                {
                    if (!typeof(Component).IsAssignableFrom(type)) continue;
                    if (type.IsAbstract && type.IsSealed) continue;
                    if (type.Name.StartsWith("<")) continue;

                    if (!string.IsNullOrEmpty(searchNamespace) && type.Namespace != searchNamespace) continue;
                    if (!string.IsNullOrEmpty(searchPattern) && !type.Name.Contains(searchPattern)) continue;

                    componentTypes.Add(type.FullName ?? type.Name);
                }
            }

            var total = componentTypes.Count;
            var page = componentTypes.Skip(cursor).Take(pageSize).ToList();

            return new SuccessResponse($"Found {total} component type(s), returning {page.Count}.", new
            {
                total = total,
                page_size = pageSize,
                cursor = cursor,
                count = page.Count,
                types = page,
            });
        }

        private static object HasComponent(ToolParams p)
        {
            var gameObjectId = p.RequireString("gameobject");
            var componentTypeName = p.RequireString("component_type");

            var go = ResolveGameObject(gameObjectId);
            if (go == null)
                return new ErrorResponse("GameObjectNotFound", $"GameObject '{gameObjectId}' not found.");

            var componentType = ResolveComponentType(componentTypeName);
            if (componentType == null)
                return new ErrorResponse("ComponentTypeNotFound", $"Component type '{componentTypeName}' not found.");

            var has = go.GetComponent(componentType) != null;

            return new SuccessResponse($"'{go.name}' {(has ? "has" : "does not have")} component '{componentTypeName}'.", new
            {
                gameobject = go.name,
                instance_id = go.GetInstanceID(),
                component_type = componentTypeName,
                has = has,
            });
        }

        // ─── Reflection Helpers ───────────────────────────────────────────────────

        private static GameObject ResolveGameObject(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return null;

            if (int.TryParse(identifier, out int instanceId))
            {
                var obj = EditorUtility.InstanceIDToObject(instanceId);
                return obj as GameObject;
            }

            var gos = Resources.FindObjectsOfTypeAll<GameObject>();
            // Try exact name
            var go = gos.FirstOrDefault(g => g.name == identifier);
            if (go != null) return go;
            // Try partial
            return gos.FirstOrDefault(g => g.name.Contains(identifier));
        }

        private static Type ResolveComponentType(string typeName)
        {
            Type type = null;

            // Try built-in Unity assemblies
            var assemblies = new[]
            {
                "UnityEngine",
                "UnityEngine.UI",
                "UnityEngine.AudioModule",
                "UnityEngine.PhysicsModule",
            };

            foreach (var asmName in assemblies)
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => !a.IsDynamic && a.GetName().Name == asmName);
                if (asm == null) continue;

                type = asm.GetType($"UnityEngine.{typeName}");
                if (type != null) break;
            }

            if (type == null)
            {
                // Search all loaded assemblies
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.IsDynamic) continue;
                    type = asm.GetTypes().FirstOrDefault(t => t.Name == typeName && typeof(Component).IsAssignableFrom(t));
                    if (type != null) break;
                }
            }

            return type;
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (prop == null)
                prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return prop;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field == null)
                field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            return field;
        }

        private static object ConvertValue(string value, Type targetType)
        {
            if (string.IsNullOrEmpty(value) || targetType == null)
                return value;

            if (targetType == typeof(string))
                return value;
            if (targetType == typeof(bool))
                return value == "true" || value == "1" || value.ToLower() == "yes";
            if (targetType == typeof(int))
                return int.TryParse(value, out var i) ? i : 0;
            if (targetType == typeof(float))
                return float.TryParse(value, out var f) ? f : 0f;
            if (targetType == typeof(double))
                return double.TryParse(value, out var d) ? d : 0.0;
            if (targetType == typeof(Vector2))
            {
                // Support "x,y" format
                var parts = value.Split(',');
                if (parts.Length >= 2)
                    return new Vector2(float.Parse(parts[0]), float.Parse(parts[1]));
                return Vector2.zero;
            }
            if (targetType == typeof(Vector3))
            {
                var parts = value.Split(',');
                if (parts.Length >= 3)
                    return new Vector3(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
                return Vector3.zero;
            }
            if (targetType == typeof(Color))
            {
                var parts = value.Split(',');
                if (parts.Length >= 4)
                    return new Color(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
                if (parts.Length >= 3)
                    return new Color(float.Parse(parts[0]), float.Parse(parts[1]), float.Parse(parts[2]));
                return Color.white;
            }

            // Try the TypeConverter approach for other types
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return value;
            }
        }
    }
}
