using System;
using System.Collections.Generic;
using System.IO;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

#if UNITY_2020_3_OR_NEWER
using UnityEngine.UIElements;
using UnityEditor.UIElements;
#else
using UnityEngine.Experimental.UIElements;
using UnityEditor.Experimental.UIElements;
#endif

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// UI Toolkit (UXML/USS) management tool supporting create_uxml, create_uss, attach_to_document,
    /// get_elements, and set_style operations.
    /// </summary>
    [McpForUnityTool("manage_ui", group = "ui",
        description = "Create and manage Unity UI Toolkit (UXML/USS): create templates, stylesheets, attach elements, get and style UI elements.")]
    public static class ManageUI
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "get_elements");

                switch (action.ToLowerInvariant())
                {
                    case "create_uxml":
                    case "createuxml":
                        return CreateUXML(p);
                    case "create_uss":
                    case "createuss":
                        return CreateUSS(p);
                    case "attach_to_document":
                    case "attachtodocument":
                        return AttachToDocument(p);
                    case "get_elements":
                    case "getelements":
                        return GetElements(p);
                    case "set_style":
                    case "setstyle":
                        return SetStyle(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: create_uxml, create_uss, attach_to_document, get_elements, set_style.");
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

        private static object CreateUXML(ToolParams p)
        {
            var documentPath = p.RequireString("document_path");
            var elementType = p.GetString("element_type", "VisualElement");
            var name = p.GetString("name");

            // Ensure path ends with .uxml
            var fullPath = documentPath.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase)
                ? documentPath
                : documentPath + ".uxml";

            // Ensure directory exists
            EnsureDirectoryExists(fullPath);

            try
            {
                // Generate a basic UXML template
                var rootElement = !string.IsNullOrEmpty(elementType)
                    ? elementType
                    : "UXML";
                var elementName = !string.IsNullOrEmpty(name) ? name : "RootElement";

                var uxmlContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<engine:UXML xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
    xmlns:engine=""UnityEngine.UIElements""
    xmlns:editor=""UnityEditor.UIElements""
    xsi:noNamespaceSchemaLocation=""../../UIElementsSchema/UIElements.xsd"">

    <engine:{rootElement} name=""{elementName}"" class=""container"">
        <!-- Add child elements here -->
    </engine:{rootElement}>

</engine:UXML>
";

                File.WriteAllText(fullPath, uxmlContent);
                AssetDatabase.ImportAsset(fullPath);

                return new SuccessResponse($"UXML document created at '{fullPath}'.", new
                {
                    path = fullPath,
                    element_type = rootElement,
                    name = elementName,
                    asset_type = "TextAsset"
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("CreateUXMLFailed",
                    $"Failed to create UXML document: {ex.Message}");
            }
        }

        private static object CreateUSS(ToolParams p)
        {
            var stylePath = p.RequireString("style_path");

            // Ensure path ends with .uss
            var fullPath = stylePath.EndsWith(".uss", StringComparison.OrdinalIgnoreCase)
                ? stylePath
                : stylePath + ".uss";

            EnsureDirectoryExists(fullPath);

            try
            {
                var ussContent = @"/* USS Stylesheet */
.container {
    width: 100%;
    height: 100%;
    flex-direction: row;
}

/* Custom styles */
";

                File.WriteAllText(fullPath, ussContent);
                AssetDatabase.ImportAsset(fullPath);

                return new SuccessResponse($"USS stylesheet created at '{fullPath}'.", new
                {
                    path = fullPath,
                    asset_type = "TextAsset"
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("CreateUSSFailed",
                    $"Failed to create USS stylesheet: {ex.Message}");
            }
        }

        private static object AttachToDocument(ToolParams p)
        {
            var documentPath = p.RequireString("document_path");
            var elementType = p.GetString("element_type", "VisualElement");
            var parentPath = p.GetString("parent_path");
            var name = p.GetString("name", "NewElement");
            var ussProperties = p.GetJObject("uss_properties");

            if (!File.Exists(documentPath))
            {
                return new ErrorResponse("DocumentNotFound",
                    $"UXML document not found at '{documentPath}'.");
            }

            try
            {
                // Read existing UXML
                var existingContent = File.ReadAllText(documentPath);

                // Generate new element
                var styleString = "";
                if (ussProperties != null)
                {
                    styleString = GenerateInlineStyles(ussProperties);
                }

                var newElement = $@"
    <engine:{elementType} name=""{name}""{styleString} />
";

                // Find insertion point (before closing </engine:UXML>)
                var insertMarker = "</engine:UXML>";
                if (existingContent.Contains(insertMarker))
                {
                    existingContent = existingContent.Replace(insertMarker, newElement + insertMarker);
                }
                else
                {
                    // No proper UXML structure, append
                    existingContent += newElement;
                }

                File.WriteAllText(documentPath, existingContent);
                AssetDatabase.ImportAsset(documentPath);

                return new SuccessResponse(
                    $"Element '{name}' ({elementType}) attached to document.", new
                    {
                        document_path = documentPath,
                        element_type = elementType,
                        name = name,
                        parent_path = parentPath,
                    });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("AttachToDocumentFailed",
                    $"Failed to attach element to document: {ex.Message}");
            }
        }

        private static object GetElements(ToolParams p)
        {
            var documentPath = p.GetString("document_path");
            var elementName = p.GetString("name");
            var elementType = p.GetString("element_type");

            if (string.IsNullOrEmpty(documentPath))
            {
                return new ErrorResponse("InvalidParameters",
                    "'document_path' is required for get_elements.");
            }

            if (!File.Exists(documentPath))
            {
                return new ErrorResponse("DocumentNotFound",
                    $"UXML document not found at '{documentPath}'.");
            }

            try
            {
                var content = File.ReadAllText(documentPath);
                var elements = new List<object>();

                // Simple XML parsing to extract element info
                var lines = content.Split('\n');
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (!trimmed.StartsWith("<engine:")) continue;
                    if (trimmed.StartsWith("<?xml")) continue;
                    if (trimmed.Contains("</engine:")) continue;

                    var elementInfo = ParseUXMLElement(trimmed, elementName, elementType);
                    if (elementInfo != null)
                    {
                        elements.Add(elementInfo);
                    }
                }

                return new SuccessResponse(
                    $"Found {elements.Count} element(s) in '{documentPath}'.", new
                    {
                        document_path = documentPath,
                        count = elements.Count,
                        elements = elements
                    });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("GetElementsFailed",
                    $"Failed to get elements: {ex.Message}");
            }
        }

        private static object SetStyle(ToolParams p)
        {
            var documentPath = p.GetString("document_path");
            var elementName = p.GetString("name");
            var ussProperties = p.GetJObject("uss_properties");
            var stylePath = p.GetString("style_path");

            if (ussProperties == null || ussProperties.Count == 0)
            {
                return new ErrorResponse("InvalidParameters",
                    "'uss_properties' is required for set_style.");
            }

            // If a USS file is provided, add styles to it
            if (!string.IsNullOrEmpty(stylePath))
            {
                return AddStylesToUSS(stylePath, elementName, ussProperties);
            }

            // Otherwise, apply inline styles to UXML
            if (!string.IsNullOrEmpty(documentPath))
            {
                return ApplyInlineStyles(documentPath, elementName, ussProperties);
            }

            return new ErrorResponse("InvalidParameters",
                "'style_path' or 'document_path' is required for set_style.");
        }

        private static object AddStylesToUSS(string ussPath, string selector, JObject properties)
        {
            if (!File.Exists(ussPath))
            {
                return new ErrorResponse("StyleSheetNotFound",
                    $"USS stylesheet not found at '{ussPath}'.");
            }

            try
            {
                var content = File.ReadAllText(ussPath);

                // Determine selector
                var cssSelector = string.IsNullOrEmpty(selector) ? "*" :
                    (selector.StartsWith(".") || selector.StartsWith("#") ? selector : "." + selector);

                var rules = new List<string>();
                rules.Add($"{cssSelector} {{");
                foreach (var prop in properties)
                {
                    var cssProperty = ConvertToCSSProperty(prop.Key, prop.Value);
                    if (!string.IsNullOrEmpty(cssProperty))
                    {
                        rules.Add($"    {cssProperty};");
                    }
                }
                rules.Add("}");
                rules.Add("");

                var newRule = string.Join("\n", rules);
                content += "\n" + newRule;

                File.WriteAllText(ussPath, content);
                AssetDatabase.ImportAsset(ussPath);

                return new SuccessResponse($"Style rule added to '{ussPath}'.", new
                {
                    style_path = ussPath,
                    selector = cssSelector,
                    properties = properties
                });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("SetStyleFailed",
                    $"Failed to set style: {ex.Message}");
            }
        }

        private static object ApplyInlineStyles(string documentPath, string elementName, JObject properties)
        {
            if (!File.Exists(documentPath))
            {
                return new ErrorResponse("DocumentNotFound",
                    $"UXML document not found at '{documentPath}'.");
            }

            try
            {
                var content = File.ReadAllText(documentPath);
                var styleString = GenerateInlineStyles(properties);

                // Find the element and add style attribute
                var searchPattern = $"name=\"{elementName}\"";
                if (content.Contains(searchPattern))
                {
                    content = content.Replace(searchPattern, searchPattern + styleString);
                    File.WriteAllText(documentPath, content);
                    AssetDatabase.ImportAsset(documentPath);

                    return new SuccessResponse(
                        $"Inline styles applied to element '{elementName}' in '{documentPath}'.", new
                        {
                            document_path = documentPath,
                            element_name = elementName,
                            properties = properties
                        });
                }

                return new ErrorResponse("ElementNotFound",
                    $"Element '{elementName}' not found in '{documentPath}'.");
            }
            catch (Exception ex)
            {
                return new ErrorResponse("ApplyStyleFailed",
                    $"Failed to apply inline styles: {ex.Message}");
            }
        }

        private static object ParseUXMLElement(string line, string filterName, string filterType)
        {
            // Extract element type
            var typeStart = line.IndexOf("<engine:");
            var typeEnd = line.IndexOf(" ");
            var closeStart = line.IndexOf(">");
            if (typeStart < 0) return null;

            if (typeEnd < 0 || (closeStart >= 0 && typeEnd > closeStart))
                typeEnd = closeStart;

            var elementType = line.Substring(typeStart + 8, typeEnd - typeStart - 8);

            // Apply filter
            if (!string.IsNullOrEmpty(filterType) &&
                !elementType.Equals(filterType, StringComparison.OrdinalIgnoreCase))
                return null;

            // Extract name attribute
            var nameStart = line.IndexOf("name=\"");
            string name = null;
            if (nameStart >= 0)
            {
                var nameEnd = line.IndexOf("\"", nameStart + 6);
                if (nameEnd > nameStart)
                    name = line.Substring(nameStart + 6, nameEnd - nameStart - 6);
            }

            if (!string.IsNullOrEmpty(filterName) &&
                !name.Equals(filterName, StringComparison.OrdinalIgnoreCase))
                return null;

            return new
            {
                element_type = elementType,
                name = name,
                raw_line = line.Trim()
            };
        }

        private static string GenerateInlineStyles(JObject properties)
        {
            var styles = new List<string>();
            foreach (var prop in properties)
            {
                var cssProperty = ConvertToCSSProperty(prop.Key, prop.Value);
                if (!string.IsNullOrEmpty(cssProperty))
                {
                    styles.Add(cssProperty);
                }
            }
            return styles.Count > 0 ? " style=\"" + string.Join("; ", styles) + "\"" : "";
        }

        private static string ConvertToCSSProperty(string key, JToken value)
        {
            if (value == null) return null;

            // Map common Unity style names to CSS
            var cssMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "width", "width" },
                { "height", "height" },
                { "min-width", "min-width" },
                { "min-height", "min-height" },
                { "max-width", "max-width" },
                { "max-height", "max-height" },
                { "flex-direction", "flex-direction" },
                { "flex-wrap", "flex-wrap" },
                { "justify-content", "justify-content" },
                { "align-items", "align-items" },
                { "align-self", "align-self" },
                { "margin", "margin" },
                { "margin-left", "margin-left" },
                { "margin-right", "margin-right" },
                { "margin-top", "margin-top" },
                { "margin-bottom", "margin-bottom" },
                { "padding", "padding" },
                { "padding-left", "padding-left" },
                { "padding-right", "padding-right" },
                { "padding-top", "padding-top" },
                { "padding-bottom", "padding-bottom" },
                { "border-width", "border-width" },
                { "border-color", "border-color" },
                { "border-radius", "border-radius" },
                { "background-color", "background-color" },
                { "color", "color" },
                { "font-size", "font-size" },
                { "font-style", "font-style" },
                { "text-align", "-unity-text-align" },
                { "visibility", "visibility" },
                { "display", "display" },
                { "opacity", "opacity" },
                { "-unity-font", "-unity-font" },
                { "-unity-font-style", "-unity-font-style" },
                { "-unity-text-align", "-unity-text-align" },
                { "-unity-background-image-tint-color", "-unity-background-image-tint-color" },
            };

            var cssKey = cssMap.TryGetValue(key, out var mapped) ? mapped : key;
            var strValue = value.Type == JTokenType.String ? value.ToString() : value.ToString();

            return $"{cssKey}: {strValue}";
        }

        private static void EnsureDirectoryExists(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
            {
                var parts = directory.Split('/');
                var current = "";
                for (int i = 0; i < parts.Length; i++)
                {
                    var parent = i > 0 ? string.Join("/", 0, i) : "Assets";
                    if (!AssetDatabase.IsValidFolder(current == "" ? parts[i] : current + "/" + parts[i]))
                    {
                        AssetDatabase.CreateFolder(parent.Length > 0 ? parent : "Assets", parts[i]);
                    }
                    current = current == "" ? parts[i] : current + "/" + parts[i];
                }
            }
        }
    }
}
