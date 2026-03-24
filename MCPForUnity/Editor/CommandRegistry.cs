using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MadAgent.UnityMCP.Editor
{
    /// <summary>
    /// Tool parameters helper — makes it easy to extract and validate
    /// parameters from a JSON-RPC params object.
    /// </summary>
    public class ToolParams
    {
        private readonly JObject _params;

        public ToolParams(JObject @params)
        {
            _params = @params ?? new JObject();
        }

        /// <summary>Get a required string parameter. Throws if missing.</summary>
        public string RequireString(string key)
        {
            var val = GetString(key);
            if (val == null)
                throw new ArgumentException($"Missing required parameter: {key}");
            return val;
        }

        /// <summary>Get an optional string parameter.</summary>
        public string GetString(string key, string defaultValue = null)
        {
            var token = _params?[key];
            return token?.Type == JTokenType.String ? token.Value<string>() : defaultValue;
        }

        /// <summary>Get an optional string, checking multiple key aliases.</summary>
        public string GetString(string key, string altKey, string defaultValue)
        {
            var val = GetString(key);
            if (val != null) return val;
            return GetString(altKey, defaultValue);
        }

        /// <summary>Get an optional int parameter.</summary>
        public int? GetInt(string key, string altKey = null)
        {
            var token = _params?[key] ?? (altKey != null ? _params?[altKey] : null);
            if (token == null) return null;
            if (token.Type == JTokenType.Integer) return token.Value<int>();
            if (token.Type == JTokenType.String && int.TryParse(token.Value<string>(), out var v)) return v;
            return null;
        }

        /// <summary>Get an optional float parameter.</summary>
        public float? GetFloat(string key)
        {
            var token = _params?[key];
            if (token == null) return null;
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer) return token.Value<float>();
            if (token.Type == JTokenType.String && float.TryParse(token.Value<string>(), out var v)) return v;
            return null;
        }

        /// <summary>Get an optional bool parameter.</summary>
        public bool? GetBool(string key)
        {
            var token = _params?[key];
            if (token == null) return null;
            if (token.Type == JTokenType.Boolean) return token.Value<bool>();
            if (token.Type == JTokenType.String)
            {
                var s = token.Value<string>()?.ToLowerInvariant();
                if (s == "true" || s == "1" || s == "yes") return true;
                if (s == "false" || s == "0" || s == "no") return false;
            }
            return null;
        }

        /// <summary>Get a bool with default value.</summary>
        public bool GetBool(string key, bool defaultValue)
        {
            return GetBool(key) ?? defaultValue;
        }

        /// <summary>Get a JArray parameter.</summary>
        public JArray GetArray(string key)
        {
            return _params?[key] as JArray;
        }

        /// <summary>Get a JObject parameter.</summary>
        public JObject GetObject(string key)
        {
            return _params?[key] as JObject;
        }

        /// <summary>Get the raw JObject.</summary>
        public JObject Raw => _params;
    }

    /// <summary>
    /// Standard success response wrapper.
    /// </summary>
    public class SuccessResponse
    {
        public bool success = true;
        public string message;
        public object data;

        public SuccessResponse(string message = null, object data = null)
        {
            this.message = message;
            this.data = data;
        }
    }

    /// <summary>
    /// Standard error response wrapper.
    /// </summary>
    public class ErrorResponse
    {
        public bool success = false;
        public string error;
        public string message;

        public ErrorResponse(string error, string message = null)
        {
            this.error = error;
            this.message = message;
        }
    }

    /// <summary>
    /// CommandRegistry auto-discovers all classes with [McpForUnityTool] attribute
    /// via reflection and routes JSON-RPC commands to their HandleCommand methods.
    /// </summary>
    public static class CommandRegistry
    {
        private static readonly Dictionary<string, ToolHandlerInfo> s_Tools = new Dictionary<string, ToolHandlerInfo>();
        private static bool s_Initialized = false;

        private class ToolHandlerInfo
        {
            public MethodInfo SyncMethod;
            public MethodInfo AsyncMethod;
            public Type DeclaringType;
            public string Group;
            public string Description;
        }

        /// <summary>
        /// Initialize the registry by scanning all assemblies for [McpForUnityTool] classes.
        /// Safe to call multiple times.
        /// </summary>
        public static void Initialize()
        {
            if (s_Initialized) return;
            s_Initialized = true;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;
                ScanAssembly(assembly);
            }

            Debug.Log($"[UnityMCP] CommandRegistry initialized with {s_Tools.Count} tools: {string.Join(", ", s_Tools.Keys)}");
        }

        private static void ScanAssembly(Assembly assembly)
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    var attr = type.GetCustomAttribute<McpForUnityToolAttribute>();
                    if (attr == null) continue;
                    if (!attr.AutoRegister) continue;

                    var toolName = attr.Name ?? NormalizeToolName(type.Name);
                    var syncMethod = type.GetMethod("HandleCommand", new[] { typeof(JObject) });
                    var asyncMethod = type.GetMethod("HandleCommand", new[] { typeof(JObject) }, new[] { typeof(bool) });

                    if (syncMethod == null && asyncMethod == null)
                    {
                        Debug.LogWarning($"[UnityMCP] Tool class {type.FullName} has [McpForUnityTool] but no HandleCommand method found.");
                        continue;
                    }

                    s_Tools[toolName] = new ToolHandlerInfo
                    {
                        SyncMethod = syncMethod,
                        AsyncMethod = asyncMethod,
                        DeclaringType = type,
                        Group = attr.Group,
                        Description = attr.Description,
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityMCP] Failed to scan assembly {assembly.FullName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Normalize a class name like "ManageGameObject" to "manage_gameobject".
        /// </summary>
        private static string NormalizeToolName(string className)
        {
            // Remove "Manage" prefix if present
            if (className.StartsWith("Manage") && className.Length > "Manage".Length)
                className = className.Substring("Manage".Length);

            // Convert CamelCase to snake_case
            var result = new System.Text.StringBuilder();
            foreach (var c in className)
            {
                if (char.IsUpper(c) && result.Length > 0)
                    result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            return result.ToString();
        }

        /// <summary>
        /// Invoke a command by name with JSON parameters. Returns a response object.
        /// </summary>
        public static object InvokeCommand(string command, JObject @params)
        {
            Initialize();

            if (!s_Tools.TryGetValue(command, out var info))
            {
                return new ErrorResponse("UnknownCommand", $"Tool '{command}' not found in registry.");
            }

            try
            {
                // Prefer sync method
                if (info.SyncMethod != null)
                {
                    var result = info.SyncMethod.Invoke(null, new object[] { @params });
                    return result ?? new SuccessResponse("OK");
                }
                else if (info.AsyncMethod != null)
                {
                    // For async, we call synchronously using .Result
                    var task = (Task<object>)info.AsyncMethod.Invoke(null, new object[] { @params, false });
                    return task.Result;
                }
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                Debug.LogError($"[UnityMCP] Tool '{command}' threw: {inner}");
                return new ErrorResponse(inner.GetType().Name, inner.Message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityMCP] Tool '{command}' failed: {ex}");
                return new ErrorResponse(ex.GetType().Name, ex.Message);
            }

            return new ErrorResponse("NoHandler", "No suitable HandleCommand method found.");
        }

        /// <summary>
        /// Invoke a command asynchronously.
        /// </summary>
        public static async Task<object> InvokeCommandAsync(string command, JObject @params)
        {
            Initialize();

            if (!s_Tools.TryGetValue(command, out var info))
            {
                return new ErrorResponse("UnknownCommand", $"Tool '{command}' not found in registry.");
            }

            try
            {
                if (info.AsyncMethod != null)
                {
                    var task = (Task<object>)info.AsyncMethod.Invoke(null, new object[] { @params, true });
                    return await task.ConfigureAwait(false);
                }
                else if (info.SyncMethod != null)
                {
                    // Run sync on thread pool to avoid blocking
                    return await Task.Run(() => info.SyncMethod.Invoke(null, new object[] { @params })).ConfigureAwait(false);
                }
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                Debug.LogError($"[UnityMCP] Tool '{command}' threw: {inner}");
                return new ErrorResponse(inner.GetType().Name, inner.Message);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityMCP] Tool '{command}' failed: {ex}");
                return new ErrorResponse(ex.GetType().Name, ex.Message);
            }

            return new ErrorResponse("NoHandler", "No suitable HandleCommand method found.");
        }

        /// <summary>
        /// Get list of all registered tool names.
        /// </summary>
        public static IReadOnlyList<string> GetToolNames()
        {
            Initialize();
            return new List<string>(s_Tools.Keys);
        }

        /// <summary>
        /// Get tool info by name.
        /// </summary>
        public static ToolHandlerInfo GetToolInfo(string name)
        {
            Initialize();
            return s_Tools.TryGetValue(name, out var info) ? info : null;
        }

        /// <summary>
        /// Get all tools grouped by their group name.
        /// </summary>
        public static Dictionary<string, List<string>> GetToolsByGroup()
        {
            Initialize();
            var result = new Dictionary<string, List<string>>();
            foreach (var kvp in s_Tools)
            {
                var group = kvp.Value.Group;
                if (!result.ContainsKey(group))
                    result[group] = new List<string>();
                result[group].Add(kvp.Key);
            }
            return result;
        }
    }
}
