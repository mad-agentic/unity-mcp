using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Tool for managing Unity C# scripts: create, get, rename, delete, and introspect.
    /// </summary>
    [McpForUnityTool("manage_script", group = "core",
        description = "Manage Unity C# scripts: create, get, rename, delete, and introspect methods/properties.")]
    public static class ManageScript
    {
        private const string DEFAULT_SCRIPTS_FOLDER = "Assets/Scripts";

        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "get");

                switch (action.ToLowerInvariant())
                {
                    case "create":
                        return CreateScript(p);
                    case "get":
                        return GetScript(p);
                    case "rename":
                        return RenameScript(p);
                    case "delete":
                        return DeleteScript(p);
                    case "get_methods":
                        return GetMethods(p);
                    case "get_properties":
                        return GetProperties(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: create, get, rename, delete, get_methods, get_properties.");
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

        private static object CreateScript(ToolParams p)
        {
            var name = p.RequireString("name");
            var className = p.GetString("class_name", name);
            var template = p.GetString("template", "MonoBehaviour");
            var ns = p.GetString("namespace");
            var outputPath = p.GetString("script_path");

            // Resolve output path
            var folder = string.IsNullOrEmpty(outputPath) ? DEFAULT_SCRIPTS_FOLDER : outputPath;
            if (!folder.StartsWith("Assets"))
                folder = Path.Combine(DEFAULT_SCRIPTS_FOLDER, folder);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var fileName = name.EndsWith(".cs") ? name : name + ".cs";
            var filePath = Path.Combine(folder, fileName).Replace("\\", "/");

            if (File.Exists(filePath) && !p.GetBool("overwrite", false))
            {
                return new ErrorResponse("FileExists", $"Script already exists at '{filePath}'. Use overwrite=true to replace.");
            }

            var content = GenerateScriptContent(className, template, ns);
            File.WriteAllText(filePath, content);

            // Import the new script so Unity picks it up
            AssetDatabase.ImportAsset(filePath);

            return new SuccessResponse($"Script '{className}' created at '{filePath}'.", new
            {
                name = className,
                file_name = fileName,
                file_path = filePath,
                template = template,
                @namespace = ns,
            });
        }

        private static object GetScript(ToolParams p)
        {
            var scriptPath = p.RequireString("script_path");
            var fullPath = ResolveScriptPath(scriptPath);

            if (!File.Exists(fullPath))
                return new ErrorResponse("ScriptNotFound", $"Script not found at '{fullPath}'.");

            var content = File.ReadAllText(fullPath);
            var className = ExtractClassName(content);
            var ns = ExtractNamespace(content);
            var template = DetectTemplate(content);

            return new SuccessResponse($"Script info for '{scriptPath}'.", new
            {
                script_path = scriptPath,
                full_path = fullPath,
                class_name = className,
                @namespace = ns,
                template = template,
                line_count = content.Split('\n').Length,
                byte_size = content.Length,
            });
        }

        private static object RenameScript(ToolParams p)
        {
            var target = p.RequireString("target");
            var newName = p.RequireString("name");
            var newClassName = p.GetString("class_name", newName);

            var fullPath = ResolveScriptPath(target);
            if (!File.Exists(fullPath))
                return new ErrorResponse("ScriptNotFound", $"Script not found at '{fullPath}'.");

            var content = File.ReadAllText(fullPath);
            var oldClassName = ExtractClassName(content);
            var oldFileName = Path.GetFileName(fullPath);

            // Rename the file
            var newFileName = newName.EndsWith(".cs") ? newName : newName + ".cs";
            var newFilePath = Path.Combine(Path.GetDirectoryName(fullPath), newFileName).Replace("\\", "/");
            File.Move(fullPath, newFilePath);

            // Update class name in content
            content = File.ReadAllText(newFilePath);
            if (!string.IsNullOrEmpty(oldClassName) && oldClassName != newClassName)
            {
                content = Regex.Replace(content, $@"\bclass\s+{Regex.Escape(oldClassName)}\b", $"class {newClassName}");
            }
            File.WriteAllText(newFilePath, content);

            AssetDatabase.Refresh();

            return new SuccessResponse($"Script renamed from '{oldFileName}' to '{newFileName}'.", new
            {
                old_path = fullPath,
                new_path = newFilePath,
                old_class_name = oldClassName,
                new_class_name = newClassName,
                old_file_name = oldFileName,
                new_file_name = newFileName,
            });
        }

        private static object DeleteScript(ToolParams p)
        {
            var target = p.RequireString("target");
            var fullPath = ResolveScriptPath(target);

            if (!File.Exists(fullPath))
                return new ErrorResponse("ScriptNotFound", $"Script not found at '{fullPath}'.");

            var className = ExtractClassName(File.ReadAllText(fullPath));
            var fileName = Path.GetFileName(fullPath);

            File.Delete(fullPath);

            // Also delete .meta if it exists
            var metaPath = fullPath + ".meta";
            if (File.Exists(metaPath))
                File.Delete(metaPath);

            AssetDatabase.Refresh();

            return new SuccessResponse($"Script '{fileName}' deleted.", new
            {
                file_name = fileName,
                class_name = className,
                path = fullPath,
            });
        }

        private static object GetMethods(ToolParams p)
        {
            var scriptPath = p.RequireString("script_path");
            var fullPath = ResolveScriptPath(scriptPath);

            if (!File.Exists(fullPath))
                return new ErrorResponse("ScriptNotFound", $"Script not found at '{fullPath}'.");

            var content = File.ReadAllText(fullPath);
            var methods = ExtractMethods(content);
            var className = ExtractClassName(content);

            return new SuccessResponse($"Found {methods.Count} method(s) in '{className}'.", new
            {
                class_name = className,
                script_path = scriptPath,
                count = methods.Count,
                methods = methods,
            });
        }

        private static object GetProperties(ToolParams p)
        {
            var scriptPath = p.RequireString("script_path");
            var fullPath = ResolveScriptPath(scriptPath);

            if (!File.Exists(fullPath))
                return new ErrorResponse("ScriptNotFound", $"Script not found at '{fullPath}'.");

            var content = File.ReadAllText(fullPath);
            var properties = ExtractProperties(content);
            var className = ExtractClassName(content);

            return new SuccessResponse($"Found {properties.Count} property/properties in '{className}'.", new
            {
                class_name = className,
                script_path = scriptPath,
                count = properties.Count,
                properties = properties,
            });
        }

        // ─── Template Generation ───────────────────────────────────────────────────

        private static string GenerateScriptContent(string className, string template, string ns)
        {
            var sb = new StringBuilder();
            var indent = "    ";

            // Header
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();

            string classDeclaration;
            string baseClass = "";
            List<string> interfaces = new List<string>();
            List<string> additionalUsings = new List<string>();

            switch (template.ToLowerInvariant())
            {
                case "networkbehaviour":
                    additionalUsings.Add("using Unity.Netcode;");
                    baseClass = "NetworkBehaviour";
                    break;

                case "editorwindow":
                    additionalUsings.Add("using UnityEditor;");
                    baseClass = "EditorWindow";
                    break;

                case "statemachine":
                    baseClass = "StateMachine";
                    break;

                case "singleton":
                    baseClass = "MonoBehaviour";
                    break;

                case "scriptableobject":
                    baseClass = "ScriptableObject";
                    break;

                case "monobehaviour":
                default:
                    baseClass = "MonoBehaviour";
                    break;
            }

            // Additional usings
            foreach (var u in additionalUsings)
            {
                if (!sb.ToString().Contains(u.Trim()))
                    sb.AppendLine(u);
            }

            if (!string.IsNullOrEmpty(ns))
            {
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
            }

            // Class declaration
            classDeclaration = $"{indent}public class {className} : {baseClass}";
            if (interfaces.Count > 0)
                classDeclaration += ", " + string.Join(", ", interfaces);

            sb.AppendLine(classDeclaration);
            sb.AppendLine($"{indent}{{");

            // Template-specific members
            switch (template.ToLowerInvariant())
            {
                case "editorwindow":
                    sb.AppendLine($"{indent}{indent}[MenuItem(\"Window/{className}\")]");
                    sb.AppendLine($"{indent}{indent}public static void ShowWindow()");
                    sb.AppendLine($"{indent}{indent}{{");
                    sb.AppendLine($"{indent}{indent}{indent}GetWindow<{className}>(\"{className}\");");
                    sb.AppendLine($"{indent}{indent}}}");
                    sb.AppendLine();
                    sb.AppendLine($"{indent}{indent}public void OnGUI()");
                    sb.AppendLine($"{indent}{indent}{{");
                    sb.AppendLine($"{indent}{indent}{indent}GUILayout.Label(\"Welcome to {className}\", EditorStyles.boldLabel);");
                    sb.AppendLine($"{indent}{indent}}}");
                    break;

                case "networkbehaviour":
                    sb.AppendLine($"{indent}{indent}// Network variables");
                    sb.AppendLine($"{indent}{indent}// [ServerRpc] public void MyServerRpc() {{ }}");
                    sb.AppendLine($"{indent}{indent}// [ClientRpc] public void MyClientRpc() {{ }}");
                    break;

                case "singleton":
                    sb.AppendLine($"{indent}{indent}private static {className} _instance;");
                    sb.AppendLine($"{indent}{indent}public static {className} Instance");
                    sb.AppendLine($"{indent}{indent}{{");
                    sb.AppendLine($"{indent}{indent}{indent}get");
                    sb.AppendLine($"{indent}{indent}{indent}{{");
                    sb.AppendLine($"{indent}{indent}{indent}{indent}if (_instance == null)");
                    sb.AppendLine($"{indent}{indent}{indent}{indent}{indent}_instance = FindObjectOfType<{className}>();");
                    sb.AppendLine($"{indent}{indent}{indent}{indent}return _instance;");
                    sb.AppendLine($"{indent}{indent}{indent}}}");
                    sb.AppendLine($"{indent}{indent}}}");
                    break;

                case "scriptableobject":
                    sb.AppendLine($"{indent}{indent}[CreateAssetMenu(menuName = \"{className}\")]");
                    sb.AppendLine($"{indent}{indent}public class {className}Data : ScriptableObject");
                    sb.AppendLine($"{indent}{indent}{{");
                    sb.AppendLine($"{indent}{indent}}}");
                    // Override with inner class approach
                    break;

                case "monobehaviour":
                default:
                    sb.AppendLine($"{indent}{indent}private void Awake()");
                    sb.AppendLine($"{indent}{indent}{{");
                    sb.AppendLine($"{indent}{indent}}}");
                    sb.AppendLine();
                    sb.AppendLine($"{indent}{indent}private void Start()");
                    sb.AppendLine($"{indent}{indent}{{");
                    sb.AppendLine($"{indent}{indent}}}");
                    sb.AppendLine();
                    sb.AppendLine($"{indent}{indent}private void Update()");
                    sb.AppendLine($"{indent}{indent}{{");
                    sb.AppendLine($"{indent}{indent}}}");
                    break;
            }

            sb.AppendLine($"{indent}}}");

            if (!string.IsNullOrEmpty(ns))
                sb.AppendLine("}");

            return sb.ToString();
        }

        // ─── Introspection Helpers ────────────────────────────────────────────────

        private static string ExtractClassName(string content)
        {
            // Match: public class ClassName : BaseClass
            var match = Regex.Match(content, @"public\s+class\s+(\w+)", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string ExtractNamespace(string content)
        {
            var match = Regex.Match(content, @"namespace\s+([\w.]+)", RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string DetectTemplate(string content)
        {
            if (content.Contains(": NetworkBehaviour")) return "NetworkBehaviour";
            if (content.Contains(": EditorWindow")) return "EditorWindow";
            if (content.Contains(": StateMachine")) return "StateMachine";
            if (content.Contains(": MonoBehaviour")) return "MonoBehaviour";
            if (content.Contains(": ScriptableObject")) return "ScriptableObject";
            return "MonoBehaviour";
        }

        private static List<object> ExtractMethods(string content)
        {
            var methods = new List<object>();
            // Match method declarations: access type? return_type name(params)
            var regex = new Regex(
                @"^\s*(public|private|protected|internal)?\s*" +
                @"(static)?\s*" +
                @"(\w+(?:<[^>]+>)?(?:\[\])?)\s+" +  // return type
                @"(\w+)\s*\(" +                       // method name + opening paren
                @"([^)]*)" +                          // parameters
                @"\)" +
                @"(?:\s*:\s*base\([^)]*\))?" +       // base() call
                @"\s*" +
                @"\{?",
                RegexOptions.Multiline);

            foreach (Match match in regex.Matches(content))
            {
                var access = match.Groups[1].Value.Trim();
                var isStatic = match.Groups[2].Success;
                var returnType = match.Groups[3].Value.Trim();
                var methodName = match.Groups[4].Value;
                var parameters = match.Groups[5].Value.Trim();

                // Skip constructors and property accessors
                if (methodName == ExtractClassName(content)) continue;
                if (parameters.Contains("{")) continue;

                methods.Add(new
                {
                    name = methodName,
                    return_type = returnType,
                    access_modifier = string.IsNullOrEmpty(access) ? "private" : access,
                    is_static = isStatic,
                    parameters = ParseParameters(parameters),
                });
            }

            return methods;
        }

        private static List<object> ExtractProperties(string content)
        {
            var properties = new List<object>();
            // Match auto-properties and full property declarations
            var regex = new Regex(
                @"^\s*(public|private|protected|internal)?\s*" +
                @"(static)?\s*" +
                @"(\w+(?:<[^>]+>)?)\s+" +  // type
                @"(\w+)\s*" +              // property name
                @"\{?\s*" +
                @"(?:get;\s*set;|get\s*\{[^}]*\}\s*set\s*\{[^}]*\}|\{[^}]*get;[^}]*set;\})?",
                RegexOptions.Multiline);

            // Simple field detection for serialized fields
            var fieldRegex = new Regex(
                @"^\s*\[(SerializeField|Header|Range|Space|Tooltip)\]\s*\n?\s*" +
                @"(public|private)?\s*" +
                @"(static)?\s*" +
                @"(\w+(?:<[^>]+>)?(?:\[\])?)\s+" +
                @"(\w+)\s*(?:=|;)",
                RegexOptions.Multiline);

            foreach (Match match in regex.Matches(content))
            {
                var access = match.Groups[1].Value.Trim();
                var isStatic = match.Groups[2].Success;
                var propType = match.Groups[3].Value.Trim();
                var propName = match.Groups[4].Value.Trim();

                if (string.IsNullOrEmpty(propName) || propName.Length < 2) continue;
                // Skip if it looks like a method
                if (propName == "if" || propName == "for" || propName == "while" || propName == "foreach") continue;

                properties.Add(new
                {
                    name = propName,
                    type = propType,
                    access_modifier = string.IsNullOrEmpty(access) ? "private" : access,
                    is_static = isStatic,
                    is_auto_property = match.Value.Contains("get;") || match.Value.Contains("set;"),
                });
            }

            return properties;
        }

        private static List<object> ParseParameters(string paramStr)
        {
            var result = new List<object>();
            if (string.IsNullOrWhiteSpace(paramStr)) return result;

            var parts = paramStr.Split(',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var tokens = trimmed.Split(' ');
                if (tokens.Length >= 2)
                {
                    result.Add(new
                    {
                        type = tokens[0],
                        name = tokens[tokens.Length - 1].TrimEnd(' ', '*', '&'),
                    });
                }
            }
            return result;
        }

        private static string ResolveScriptPath(string path)
        {
            if (File.Exists(path))
                return path;
            if (File.Exists("Assets/" + path))
                return "Assets/" + path;
            if (File.Exists(Path.Combine(Application.dataPath, path).Replace("\\", "/")))
                return Path.Combine(Application.dataPath, path).Replace("\\", "/");
            if (File.Exists(Path.Combine(Application.dataPath, path + ".cs").Replace("\\", "/")))
                return Path.Combine(Application.dataPath, path + ".cs").Replace("\\", "/");
            return path;
        }
    }
}
