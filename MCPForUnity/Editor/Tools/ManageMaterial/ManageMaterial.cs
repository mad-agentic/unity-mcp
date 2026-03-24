using System;
using System.Collections.Generic;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Material management tool supporting create, get, set_color, set_texture,
    /// set_shader, set_float, set_vector, set_keyword, duplicate, and delete.
    /// </summary>
    [McpForUnityTool("manage_material", group = "core",
        description = "Manage Unity Materials: create, get, set color/texture/shader/float/vector/keyword, duplicate, and delete.")]
    public static class ManageMaterial
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "get");

                switch (action.ToLowerInvariant())
                {
                    case "create":
                        return CreateMaterial(p);
                    case "get":
                        return GetMaterial(p);
                    case "set_color":
                        return SetColor(p);
                    case "set_texture":
                        return SetTexture(p);
                    case "set_shader":
                        return SetShader(p);
                    case "set_float":
                        return SetFloat(p);
                    case "set_vector":
                        return SetVector(p);
                    case "set_keyword":
                        return SetKeyword(p);
                    case "duplicate":
                        return DuplicateMaterial(p);
                    case "delete":
                        return DeleteMaterial(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: create, get, set_color, set_texture, set_shader, set_float, set_vector, set_keyword, duplicate, delete.");
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

        private static object CreateMaterial(ToolParams p)
        {
            var name = p.GetString("name", "NewMaterial");
            var shaderName = p.GetString("shader_name", "Standard");
            var colorArray = p.GetArray("color");
            var targetPath = p.GetString("target");

            var material = new Material(Shader.Find(shaderName));
            material.name = name;

            if (colorArray != null && colorArray.Count >= 3)
            {
                var c = ParseColor(colorArray);
                material.color = c;
            }

            string assetPath = null;
            if (!string.IsNullOrEmpty(targetPath))
            {
                assetPath = EnsureAssetPath(targetPath);
                if (!assetPath.EndsWith(".mat"))
                    assetPath += "/" + name + ".mat";
                assetPath = assetPath.TrimStart('/');
                AssetDatabase.CreateAsset(material, assetPath);
            }

            return new SuccessResponse($"Material '{name}' created.", new
            {
                name = material.name,
                instance_id = material.GetInstanceID(),
                shader_name = material.shader != null ? material.shader.name : shaderName,
                color = new[] { material.color.r, material.color.g, material.color.b, material.color.a },
                asset_path = assetPath,
            });
        }

        private static object GetMaterial(ToolParams p)
        {
            var mat = ResolveMaterial(p);
            if (mat == null)
                return new ErrorResponse("MaterialNotFound", "No material found. Provide 'target' (path or instance ID) or 'name'.");

            return new SuccessResponse($"Material '{mat.name}' info retrieved.", new
            {
                name = mat.name,
                instance_id = mat.GetInstanceID(),
                shader = mat.shader != null ? mat.shader.name : null,
                shader_instance_id = mat.shader != null ? mat.shader.GetInstanceID() : 0,
                color = new[] { mat.color.r, mat.color.g, mat.color.b, mat.color.a },
                render_queue = mat.renderQueue,
                double_sidedGI = mat.doubleSidedGI,
                enabled_keywords = GetEnabledKeywords(mat),
                properties = GetMaterialProperties(mat),
                asset_path = AssetDatabase.GetAssetPath(mat),
            });
        }

        private static object SetColor(ToolParams p)
        {
            var mat = ResolveMaterial(p);
            if (mat == null)
                return new ErrorResponse("MaterialNotFound", "No material found. Provide 'target' (path or instance ID) or 'name'.");

            var colorArray = p.GetArray("color");
            if (colorArray == null || colorArray.Count < 3)
                return new ErrorResponse("InvalidParameters", "Color requires at least 3 values [r, g, b] or 4 values [r, g, b, a].");

            var c = ParseColor(colorArray);
            mat.color = c;
            MarkDirty(mat);

            return new SuccessResponse($"Color set to ({c.r:F3}, {c.g:F3}, {c.b:F3}, {c.a:F3}).", new
            {
                name = mat.name,
                instance_id = mat.GetInstanceID(),
                color = new[] { mat.color.r, mat.color.g, mat.color.b, mat.color.a },
            });
        }

        private static object SetTexture(ToolParams p)
        {
            var mat = ResolveMaterial(p);
            if (mat == null)
                return new ErrorResponse("MaterialNotFound", "No material found. Provide 'target' (path or instance ID) or 'name'.");

            var texturePath = p.RequireString("texture_path");
            var propertyName = p.GetString("property_name", "_MainTex");

            var texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
            if (texture == null)
                return new ErrorResponse("TextureNotFound", $"Texture not found at path: {texturePath}");

            mat.SetTexture(propertyName, texture);
            MarkDirty(mat);

            return new SuccessResponse($"Texture '{texture.name}' assigned to '{propertyName}'.", new
            {
                name = mat.name,
                instance_id = mat.GetInstanceID(),
                property_name = propertyName,
                texture_name = texture.name,
                texture_instance_id = texture.GetInstanceID(),
                texture_path = texturePath,
            });
        }

        private static object SetShader(ToolParams p)
        {
            var mat = ResolveMaterial(p);
            if (mat == null)
                return new ErrorResponse("MaterialNotFound", "No material found. Provide 'target' (path or instance ID) or 'name'.");

            var shaderName = p.RequireString("shader_name");
            var shader = Shader.Find(shaderName);
            if (shader == null)
                return new ErrorResponse("ShaderNotFound", $"Shader '{shaderName}' not found in project.");

            var oldShader = mat.shader;
            mat.shader = shader;
            MarkDirty(mat);

            return new SuccessResponse($"Shader changed from '{oldShader?.name}' to '{shaderName}'.", new
            {
                name = mat.name,
                instance_id = mat.GetInstanceID(),
                old_shader = oldShader?.name,
                new_shader = shaderName,
            });
        }

        private static object SetFloat(ToolParams p)
        {
            var mat = ResolveMaterial(p);
            if (mat == null)
                return new ErrorResponse("MaterialNotFound", "No material found. Provide 'target' (path or instance ID) or 'name'.");

            var propertyName = p.RequireString("property_name");
            var value = p.GetFloat("property_value");
            if (!value.HasValue)
                return new ErrorResponse("InvalidParameters", "property_value is required for set_float.");

            mat.SetFloat(propertyName, value.Value);
            MarkDirty(mat);

            return new SuccessResponse($"Float '{propertyName}' set to {value.Value}.", new
            {
                name = mat.name,
                instance_id = mat.GetInstanceID(),
                property_name = propertyName,
                value = value.Value,
            });
        }

        private static object SetVector(ToolParams p)
        {
            var mat = ResolveMaterial(p);
            if (mat == null)
                return new ErrorResponse("MaterialNotFound", "No material found. Provide 'target' (path or instance ID) or 'name'.");

            var propertyName = p.RequireString("property_name");
            var vecArray = p.GetArray("property_value");
            var vecObject = p.GetObject("property_value");
            var hasVector = false;
            var vec = Vector4.zero;
            if (vecArray != null)
            {
                vec = ParseVector(vecArray);
                hasVector = true;
            }
            else if (vecObject != null)
            {
                vec = ParseVector(vecObject);
                hasVector = true;
            }
            if (!hasVector)
                return new ErrorResponse("InvalidParameters", "property_value is required for set_vector as [x,y,z,w] or {x,y,z,w}.");

            mat.SetVector(propertyName, vec);
            MarkDirty(mat);

            return new SuccessResponse($"Vector '{propertyName}' set to {vec}.", new
            {
                name = mat.name,
                instance_id = mat.GetInstanceID(),
                property_name = propertyName,
                value = new[] { vec.x, vec.y, vec.z, vec.w },
            });
        }

        private static object SetKeyword(ToolParams p)
        {
            var mat = ResolveMaterial(p);
            if (mat == null)
                return new ErrorResponse("MaterialNotFound", "No material found. Provide 'target' (path or instance ID) or 'name'.");

            var keyword = p.RequireString("keyword");
            var enabled = p.GetBool("keyword_enabled", true);

            if (enabled)
                mat.EnableKeyword(keyword);
            else
                mat.DisableKeyword(keyword);

            MarkDirty(mat);

            return new SuccessResponse($"Keyword '{keyword}' {(enabled ? "enabled" : "disabled")}.", new
            {
                name = mat.name,
                instance_id = mat.GetInstanceID(),
                keyword = keyword,
                enabled = enabled,
            });
        }

        private static object DuplicateMaterial(ToolParams p)
        {
            var mat = ResolveMaterial(p);
            if (mat == null)
                return new ErrorResponse("MaterialNotFound", "No material found. Provide 'target' (path or instance ID) or 'name'.");

            var newName = p.GetString("name");
            var newMaterial = new Material(mat);
            newMaterial.name = string.IsNullOrEmpty(newName) ? mat.name + " (Copy)" : newName;

            var sourcePath = AssetDatabase.GetAssetPath(mat);
            string newPath = null;
            if (!string.IsNullOrEmpty(sourcePath))
            {
                var dir = System.IO.Path.GetDirectoryName(sourcePath);
                var ext = System.IO.Path.GetExtension(sourcePath);
                newPath = System.IO.Path.Combine(dir, newMaterial.name + ext).Replace("\\", "/");
                AssetDatabase.CreateAsset(newMaterial, newPath);
            }

            return new SuccessResponse($"Material '{mat.name}' duplicated as '{newMaterial.name}'.", new
            {
                original_name = mat.name,
                original_instance_id = mat.GetInstanceID(),
                duplicate_name = newMaterial.name,
                duplicate_instance_id = newMaterial.GetInstanceID(),
                asset_path = newPath,
            });
        }

        private static object DeleteMaterial(ToolParams p)
        {
            var mat = ResolveMaterial(p);
            if (mat == null)
                return new ErrorResponse("MaterialNotFound", "No material found. Provide 'target' (path or instance ID) or 'name'.");

            var assetPath = AssetDatabase.GetAssetPath(mat);
            var name = mat.name;

            if (!string.IsNullOrEmpty(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(mat);
            }

            return new SuccessResponse($"Material '{name}' deleted.", new
            {
                name = name,
                asset_path = assetPath ?? "(instance only)",
            });
        }

        // --- Helpers ---

        private static Material ResolveMaterial(ToolParams p)
        {
            var target = p.GetString("target");
            var name = p.GetString("name");

            if (!string.IsNullOrEmpty(target))
            {
                // Try instance ID
                if (int.TryParse(target, out int id))
                {
                    var obj = EditorUtility.InstanceIDToObject(id);
                    if (obj is Material m) return m;
                }

                // Try asset path
                var mat = AssetDatabase.LoadAssetAtPath<Material>(target);
                if (mat != null) return mat;

                // Try name search
                var guids = AssetDatabase.FindAssets("t:Material " + target);
                if (guids.Length > 0)
                    return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (!string.IsNullOrEmpty(name))
            {
                var guids = AssetDatabase.FindAssets("t:Material " + name);
                foreach (var g in guids)
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                    if (m != null && m.name == name) return m;
                }
                if (guids.Length > 0)
                    return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            return null;
        }

        private static Color ParseColor(JArray array)
        {
            float r = array[0].Value<float>();
            float g = array[1].Value<float>();
            float b = array[2].Value<float>();
            float a = array.Count > 3 ? array[3].Value<float>() : 1.0f;
            return new Color(r, g, b, a);
        }

        private static Vector4 ParseVector(JArray array)
        {
            if (array == null) return Vector4.zero;
            float x = array.Count > 0 ? array[0].Value<float>() : 0f;
            float y = array.Count > 1 ? array[1].Value<float>() : 0f;
            float z = array.Count > 2 ? array[2].Value<float>() : 0f;
            float w = array.Count > 3 ? array[3].Value<float>() : 0f;
            return new Vector4(x, y, z, w);
        }

        private static Vector4 ParseVector(JObject obj)
        {
            if (obj == null) return Vector4.zero;
            float x = obj["x"]?.Value<float>() ?? 0f;
            float y = obj["y"]?.Value<float>() ?? 0f;
            float z = obj["z"]?.Value<float>() ?? 0f;
            float w = obj["w"]?.Value<float>() ?? 0f;
            return new Vector4(x, y, z, w);
        }

        private static void MarkDirty(Material mat)
        {
            var path = AssetDatabase.GetAssetPath(mat);
            if (!string.IsNullOrEmpty(path))
                EditorUtility.SetDirty(mat);
        }

        private static List<string> GetEnabledKeywords(Material mat)
        {
            var keywords = new List<string>();
            foreach (var kw in mat.shaderKeywords)
                keywords.Add(kw);
            return keywords;
        }

        private static List<object> GetMaterialProperties(Material mat)
        {
            var props = new List<object>();
            if (mat.shader == null) return props;

            int count = ShaderUtil.GetPropertyCount(mat.shader);
            for (int i = 0; i < count; i++)
            {
                var propName = ShaderUtil.GetPropertyName(mat.shader, i);
                var propType = ShaderUtil.GetPropertyType(mat.shader, i);

                switch (propType)
                {
                    case ShaderUtil.ShaderPropertyType.Color:
                        var col = mat.GetColor(propName);
                        props.Add(new { name = propName, type = "Color", value = new[] { col.r, col.g, col.b, col.a } });
                        break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        var vec = mat.GetVector(propName);
                        props.Add(new { name = propName, type = "Vector", value = new[] { vec.x, vec.y, vec.z, vec.w } });
                        break;
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        props.Add(new { name = propName, type = propType.ToString(), value = mat.GetFloat(propName) });
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        var tex = mat.GetTexture(propName);
                        props.Add(new { name = propName, type = "Texture", value = tex != null ? tex.name : null });
                        break;
                }
            }
            return props;
        }

        private static string EnsureAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "Assets";
            return path.StartsWith("Assets") ? path : "Assets/" + path;
        }
    }
}
