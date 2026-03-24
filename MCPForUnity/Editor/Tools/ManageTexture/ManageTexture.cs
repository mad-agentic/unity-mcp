using System;
using System.Collections.Generic;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Texture management tool supporting create, get, set_pixels, resize, and apply.
    /// </summary>
    [McpForUnityTool("manage_texture", Group = "core",
        Description = "Manage Unity Textures: create, get, set pixels, resize, and apply changes to Texture2D assets.")]
    public static class ManageTexture
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
                        return CreateTexture(p);
                    case "get":
                        return GetTexture(p);
                    case "set_pixels":
                    case "setpixels":
                        return SetPixels(p);
                    case "resize":
                        return ResizeTexture(p);
                    case "apply":
                        return ApplyTexture(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: create, get, set_pixels, resize, apply.");
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

        private static object CreateTexture(ToolParams p)
        {
            var name = p.GetString("name", "NewTexture");
            var width = p.GetInt("width") ?? 256;
            var height = p.GetInt("height") ?? 256;
            var formatStr = p.GetString("format", "RGBA");
            var colorArray = p.GetArray("color");
            var pattern = p.GetString("pattern");
            var targetPath = p.GetString("target");

            var format = ParseTextureFormat(formatStr);
            if (format == TextureFormat.Alpha8 && !formatStr.Equals("Alpha", StringComparison.OrdinalIgnoreCase))
                format = TextureFormat.RGBA32;

            var texture = new Texture2D(width, height, format, false);
            texture.name = name;

            // Fill with color or pattern
            if (colorArray != null && colorArray.Count >= 3)
            {
                var c = ParseColor(colorArray);
                var pixels = new Color[width * height];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
                texture.SetPixels(pixels);
                texture.Apply();
            }
            else if (!string.IsNullOrEmpty(pattern))
            {
                FillPattern(texture, pattern, colorArray);
            }

            string assetPath = null;
            if (!string.IsNullOrEmpty(targetPath))
            {
                assetPath = EnsureAssetPath(targetPath);
                if (!assetPath.EndsWith(".png") && !assetPath.EndsWith(".asset"))
                    assetPath += "/" + name + ".png";
                assetPath = assetPath.TrimStart('/');
                AssetDatabase.CreateAsset(texture, assetPath);
            }

            return new SuccessResponse($"Texture '{name}' created ({width}x{height}, {format}).", new
            {
                name = texture.name,
                instance_id = texture.GetInstanceID(),
                width = texture.width,
                height = texture.height,
                format = texture.format.ToString(),
                mipmap_count = texture.mipmapCount,
                filter_mode = texture.filterMode.ToString(),
                asset_path = assetPath,
            });
        }

        private static object GetTexture(ToolParams p)
        {
            var tex = ResolveTexture(p);
            if (tex == null)
                return new ErrorResponse("TextureNotFound", "No texture found. Provide 'target' (path or instance ID) or 'name'.");

            return new SuccessResponse($"Texture '{tex.name}' info retrieved.", new
            {
                name = tex.name,
                instance_id = tex.GetInstanceID(),
                width = tex.width,
                height = tex.height,
                format = tex.format.ToString(),
                mipmap_count = tex.mipmapCount,
                filter_mode = tex.filterMode.ToString(),
                wrap_mode = tex.wrapMode.ToString(),
                aniso_level = tex.anisoLevel,
                is_readable = IsReadable(tex),
                asset_path = AssetDatabase.GetAssetPath(tex),
            });
        }

        private static object SetPixels(ToolParams p)
        {
            var tex = ResolveTexture(p);
            if (tex == null)
                return new ErrorResponse("TextureNotFound", "No texture found. Provide 'target' (path or instance ID) or 'name'.");

            var pixelsData = p.GetArray("pixels");
            if (pixelsData == null || pixelsData.Count == 0)
                return new ErrorResponse("InvalidParameters", "pixels array is required for set_pixels.");

            var offsetX = p.GetInt("offset_x") ?? 0;
            var offsetY = p.GetInt("offset_y") ?? 0;

            // Determine pixel layout: flat [r,g,b,a, r,g,b,a, ...] or list of {r,g,b,a}
            bool isFlat = pixelsData.Count > 0 && pixelsData[0].Type == JTokenType.Float;

            Color[] colors;
            if (isFlat)
            {
                int pixelCount = pixelsData.Count / 4;
                colors = new Color[pixelCount];
                for (int i = 0; i < pixelCount; i++)
                {
                    colors[i] = new Color(
                        pixelsData[i * 4].Value<float>(),
                        pixelsData[i * 4 + 1].Value<float>(),
                        pixelsData[i * 4 + 2].Value<float>(),
                        pixelsData.Count > i * 4 + 3 ? pixelsData[i * 4 + 3].Value<float>() : 1.0f
                    );
                }
            }
            else
            {
                colors = new Color[pixelsData.Count];
                for (int i = 0; i < pixelsData.Count; i++)
                {
                    var obj = pixelsData[i] as JObject;
                    if (obj != null)
                    {
                        colors[i] = new Color(
                            obj["r"] != null ? obj["r"].Value<float>() : 1f,
                            obj["g"] != null ? obj["g"].Value<float>() : 1f,
                            obj["b"] != null ? obj["b"].Value<float>() : 1f,
                            obj["a"] != null ? obj["a"].Value<float>() : 1f
                        );
                    }
                }
            }

            int regionWidth = Mathf.CeilToInt(Mathf.Sqrt(colors.Length));
            int regionHeight = Mathf.CeilToInt((float)colors.Length / regionWidth);

            offsetX = Mathf.Clamp(offsetX, 0, tex.width - 1);
            offsetY = Mathf.Clamp(offsetY, 0, tex.height - 1);

            tex.SetPixels(offsetX, offsetY, Mathf.Min(regionWidth, tex.width - offsetX), Mathf.Min(regionHeight, tex.height - offsetY), colors);
            EditorUtility.SetDirty(tex);

            return new SuccessResponse($"Set {colors.Length} pixel(s) at ({offsetX}, {offsetY}). Call 'apply' to save.", new
            {
                name = tex.name,
                instance_id = tex.GetInstanceID(),
                pixels_set = colors.Length,
                offset_x = offsetX,
                offset_y = offsetY,
                width = tex.width,
                height = tex.height,
            });
        }

        private static object ResizeTexture(ToolParams p)
        {
            var tex = ResolveTexture(p);
            if (tex == null)
                return new ErrorResponse("TextureNotFound", "No texture found. Provide 'target' (path or instance ID) or 'name'.");

            var newWidth = p.GetInt("width");
            var newHeight = p.GetInt("height");
            if (!newWidth.HasValue || !newHeight.HasValue)
                return new ErrorResponse("InvalidParameters", "Both 'width' and 'height' are required for resize.");

            tex.Resize(newWidth.Value, newHeight.Value);
            EditorUtility.SetDirty(tex);

            return new SuccessResponse($"Texture resized to {newWidth.Value}x{newHeight.Value}.", new
            {
                name = tex.name,
                instance_id = tex.GetInstanceID(),
                old_width = tex.width,
                old_height = tex.height,
                new_width = newWidth.Value,
                new_height = newHeight.Value,
            });
        }

        private static object ApplyTexture(ToolParams p)
        {
            var tex = ResolveTexture(p);
            if (tex == null)
                return new ErrorResponse("TextureNotFound", "No texture found. Provide 'target' (path or instance ID) or 'name'.");

            var mipmapBias = p.GetBool("mipmap", false);
            tex.Apply(mipmapBias);
            EditorUtility.SetDirty(tex);

            return new SuccessResponse($"Texture '{tex.name}' changes applied.", new
            {
                name = tex.name,
                instance_id = tex.GetInstanceID(),
                width = tex.width,
                height = tex.height,
            });
        }

        // --- Helpers ---

        private static Texture2D ResolveTexture(ToolParams p)
        {
            var target = p.GetString("target");
            var name = p.GetString("name");

            if (!string.IsNullOrEmpty(target))
            {
                if (int.TryParse(target, out int id))
                {
                    var obj = EditorUtility.InstanceIDToObject(id);
                    return obj as Texture2D;
                }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(target);
                if (tex != null) return tex;

                // Also try as generic Texture
                var generic = AssetDatabase.LoadAssetAtPath<Texture>(target);
                if (generic != null)
                {
                    // Try to get the Texture2D from it
                    var allTex = Resources.FindObjectsOfTypeAll<Texture2D>();
                    foreach (var t in allTex)
                    {
                        if (AssetDatabase.GetAssetPath(t) == target)
                            return t;
                    }
                }

                var guids = AssetDatabase.FindAssets("t:Texture " + target);
                if (guids.Length > 0)
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (!string.IsNullOrEmpty(name))
            {
                var guids = AssetDatabase.FindAssets("t:Texture " + name);
                foreach (var g in guids)
                {
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g));
                    if (t != null && t.name == name) return t;
                }
            }

            return null;
        }

        private static TextureFormat ParseTextureFormat(string format)
        {
            switch (format.ToUpperInvariant())
            {
                case "RGB": return TextureFormat.RGB24;
                case "RGBA": return TextureFormat.RGBA32;
                case "ARGB": return TextureFormat.ARGB32;
                case "ALPHA": return TextureFormat.Alpha8;
                case "R8": return TextureFormat.R8;
                case "R16": return TextureFormat.R16;
                case "DXT1": return TextureFormat.DXT1;
                case "DXT5": return TextureFormat.DXT5;
                case "BC4": return TextureFormat.BC4;
                case "BC5": return TextureFormat.BC5;
                case "BC7": return TextureFormat.BC7;
                case "ETC2_RGB4": return TextureFormat.ETC2_RGB4;
                case "ETC2_RGBA8": return TextureFormat.ETC2_RGBA8;
                default: return TextureFormat.RGBA32;
            }
        }

        private static Color ParseColor(JArray array)
        {
            float r = array[0].Value<float>();
            float g = array[1].Value<float>();
            float b = array[2].Value<float>();
            float a = array.Count > 3 ? array[3].Value<float>() : 1.0f;
            return new Color(r, g, b, a);
        }

        private static void FillPattern(Texture2D tex, string pattern, JArray baseColor)
        {
            var baseC = baseColor != null ? ParseColor(baseColor) : Color.white;
            var pixels = new Color[tex.width * tex.height];

            switch (pattern.ToLowerInvariant())
            {
                case "gradient":
                    for (int y = 0; y < tex.height; y++)
                    {
                        float t = (float)y / tex.height;
                        for (int x = 0; x < tex.width; x++)
                        {
                            float gray = (float)x / tex.width;
                            pixels[y * tex.width + x] = Color.Lerp(Color.black, Color.white, t * 0.5f + gray * 0.5f);
                        }
                    }
                    break;

                case "noise":
                    var random = new System.Random();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        float v = (float)random.NextDouble();
                        pixels[i] = new Color(v, v, v, 1f);
                    }
                    break;

                case "checker":
                    int size = 8;
                    for (int y = 0; y < tex.height; y++)
                    {
                        for (int x = 0; x < tex.width; x++)
                        {
                            bool light = ((x / size) + (y / size)) % 2 == 0;
                            pixels[y * tex.width + x] = light ? Color.white : Color.black;
                        }
                    }
                    break;

                default:
                    for (int i = 0; i < pixels.Length; i++)
                        pixels[i] = baseC;
                    break;
            }

            tex.SetPixels(pixels);
            tex.Apply();
        }

        private static bool IsReadable(Texture2D tex)
        {
            if (tex == null) return false;
            var path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return true; // runtime-created textures
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            return importer != null && importer.isReadable;
        }

        private static string EnsureAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "Assets";
            return path.StartsWith("Assets") ? path : "Assets/" + path;
        }
    }
}
