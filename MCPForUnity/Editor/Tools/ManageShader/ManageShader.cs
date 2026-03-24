using System;
using System.Collections.Generic;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Shader introspection tool supporting list, get_properties, and get_keywords.
    /// </summary>
    [McpForUnityTool("manage_shader", group = "core",
        description = "Inspect Unity Shaders: list available shaders, get shader properties, and get shader keywords.")]
    public static class ManageShader
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "list");

                switch (action.ToLowerInvariant())
                {
                    case "list":
                        return ListShaders(p);
                    case "get_properties":
                    case "getproperties":
                        return GetProperties(p);
                    case "get_keywords":
                    case "getkeywords":
                        return GetKeywords(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: list, get_properties, get_keywords.");
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

        private static object ListShaders(ToolParams p)
        {
            var shaderCount = ShaderUtil.GetPropertyCount(null);
            var allShaders = Resources.FindObjectsOfTypeAll<Shader>();
            var uniqueShaders = new Dictionary<string, Shader>();

            foreach (var s in allShaders)
            {
                if (s != null && !uniqueShaders.ContainsKey(s.name))
                    uniqueShaders[s.name] = s;
            }

            var shaderList = new List<object>();
            foreach (var kvp in uniqueShaders)
            {
                shaderList.Add(new
                {
                    name = kvp.Key,
                    instance_id = kvp.Value.GetInstanceID(),
                    is_builtin = kvp.Key.StartsWith("Hidden/") || kvp.Key.StartsWith("Unity/"),
                });
            }

            shaderList.Sort((a, b) => string.Compare(
                ((dynamic)a).name, ((dynamic)b).name, StringComparison.OrdinalIgnoreCase));

            return new SuccessResponse($"Listed {shaderList.Count} shaders.", new
            {
                count = shaderList.Count,
                shaders = shaderList,
            });
        }

        private static object GetProperties(ToolParams p)
        {
            var shaderName = p.GetString("shader_name");
            var shaderPath = p.GetString("shader_path");

            Shader shader = null;

            if (!string.IsNullOrEmpty(shaderPath))
            {
                shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            }

            if (shader == null && !string.IsNullOrEmpty(shaderName))
            {
                shader = Shader.Find(shaderName);
            }

            if (shader == null)
            {
                return new ErrorResponse("ShaderNotFound",
                    $"Shader not found. Provide 'shader_name' or 'shader_path'. Tried: '{shaderName}'");
            }

            int count = ShaderUtil.GetPropertyCount(shader);
            var properties = new List<object>();

            for (int i = 0; i < count; i++)
            {
                var propName = ShaderUtil.GetPropertyName(shader, i);
                var propType = ShaderUtil.GetPropertyType(shader, i);
                var propDesc = ShaderUtil.GetPropertyDescription(shader, i);
                var propAttr = Array.Empty<string>();

                properties.Add(new
                {
                    index = i,
                    name = propName,
                    type = propType.ToString(),
                    description = propDesc,
                    attributes = propAttr != null ? new List<string>(propAttr) : new List<string>(),
                    range = GetPropertyRange(shader, i, propType),
                });
            }

            return new SuccessResponse($"Shader '{shader.name}' has {count} properties.", new
            {
                shader_name = shader.name,
                shader_instance_id = shader.GetInstanceID(),
                property_count = count,
                properties = properties,
            });
        }

        private static object GetKeywords(ToolParams p)
        {
            var shaderName = p.GetString("shader_name");
            var shaderPath = p.GetString("shader_path");

            Shader shader = null;

            if (!string.IsNullOrEmpty(shaderPath))
            {
                shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            }

            if (shader == null && !string.IsNullOrEmpty(shaderName))
            {
                shader = Shader.Find(shaderName);
            }

            if (shader == null)
            {
                return new ErrorResponse("ShaderNotFound",
                    $"Shader not found. Provide 'shader_name' or 'shader_path'. Tried: '{shaderName}'");
            }

            // Get local keywords (pragma multi_compile)
            var localKeywords = new List<string>();
            int count = ShaderUtil.GetPropertyCount(shader);

            // Unity doesn't expose a direct GetLocalKeywords API on Shader,
            // but we can use ShaderUtil to get shader compilation messages which may contain keyword info
            // For a more reliable approach, we read the shader source

            string shaderSource = null;
            var path = AssetDatabase.GetAssetPath(shader);
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    shaderSource = System.IO.File.ReadAllText(path);
                }
                catch { }
            }

            var keywords = new List<string>();
            if (!string.IsNullOrEmpty(shaderSource))
            {
                keywords = ExtractKeywords(shaderSource);
            }

            return new SuccessResponse($"Shader '{shader.name}' keywords retrieved.", new
            {
                shader_name = shader.name,
                shader_instance_id = shader.GetInstanceID(),
                keyword_count = keywords.Count,
                keywords = keywords,
            });
        }

        private static object GetPropertyRange(Shader shader, int index, ShaderUtil.ShaderPropertyType propType)
        {
            if (propType != ShaderUtil.ShaderPropertyType.Range)
                return null;

            float min = ShaderUtil.GetRangeLimits(shader, index, 0);
            float max = ShaderUtil.GetRangeLimits(shader, index, 1);
            return new { min = min, max = max };
        }

        private static List<string> ExtractKeywords(string source)
        {
            var keywords = new HashSet<string>();
            var lines = source.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // multi_compile
                if (trimmed.StartsWith("#pragma multi_compile"))
                {
                    ExtractMultiCompileKeywords(trimmed, keywords);
                }
                // skip_variants
                else if (trimmed.StartsWith("#pragma skip_variants"))
                {
                    // Format: #pragma skip_variants SHADER_KEYWORD_1 SHADER_KEYWORD_2
                    var parts = trimmed.Substring("pragma skip_variants".Length).Trim().Split(
                        new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var kw in parts)
                        if (!kw.StartsWith("_")) keywords.Add(kw.Trim());
                }
            }

            return new List<string>(keywords);
        }

        private static void ExtractMultiCompileKeywords(string line, HashSet<string> keywords)
        {
            // #pragma multi_compile A B C D
            // #pragma multi_compile A B C _FEATURE_ON
            var after = line.Substring("pragma multi_compile".Length).Trim();
            // Support both "multi_compile A B" and "multi_compile _FEATURE A B"
            var parts = after.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kw = part.Trim();
                // Skip Unity built-in keywords (starting with underscore underscore)
                if (!kw.StartsWith("__"))
                    keywords.Add(kw);
            }
        }
    }
}
