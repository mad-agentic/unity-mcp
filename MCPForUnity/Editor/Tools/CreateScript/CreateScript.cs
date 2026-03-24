using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Tool for creating Unity C# scripts with templates and method stubs.
    /// More focused than ManageScript for script creation scenarios.
    /// </summary>
    [McpForUnityTool("create_script", Group = "core",
        Description = "Create Unity C# scripts from templates or from scratch, with optional method stubs.")]
    public static class CreateScript
    {
        private const string DEFAULT_SCRIPTS_FOLDER = "Assets/Scripts";

        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "from_scratch");

                switch (action.ToLowerInvariant())
                {
                    case "from_template":
                        return FromTemplate(p);
                    case "from_scratch":
                        return FromScratch(p);
                    case "with_methods":
                        return WithMethods(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: from_template, from_scratch, with_methods.");
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

        private static object FromTemplate(ToolParams p)
        {
            var name = p.RequireString("name");
            var className = p.GetString("class_name", name);
            var template = p.GetString("template", "MonoBehaviour");
            var ns = p.GetString("namespace");
            var outputPath = p.GetString("output_path");

            return CreateFile(className, template, ns, outputPath, p, null);
        }

        private static object FromScratch(ToolParams p)
        {
            var name = p.RequireString("name");
            var className = p.GetString("class_name", name);
            var ns = p.GetString("namespace");
            var outputPath = p.GetString("output_path");

            return CreateFile(className, "Scratch", ns, outputPath, p, null);
        }

        private static object WithMethods(ToolParams p)
        {
            var name = p.RequireString("name");
            var className = p.GetString("class_name", name);
            var ns = p.GetString("namespace");
            var outputPath = p.GetString("output_path");
            var methodsArray = p.GetArray("methods");

            var methodStubs = new List<string>();
            if (methodsArray != null)
            {
                foreach (var item in methodsArray)
                {
                    var sig = item?.Value<string>();
                    if (!string.IsNullOrEmpty(sig))
                        methodStubs.Add(sig);
                }
            }

            if (methodStubs.Count == 0)
                return new ErrorResponse("InvalidParameters", "At least one method signature is required for 'with_methods' action.");

            return CreateFile(className, "WithMethods", ns, outputPath, p, methodStubs);
        }

        private static object CreateFile(string className, string template, string ns, string outputPath, ToolParams p, List<string> methodStubs)
        {
            // Resolve output folder
            var folder = string.IsNullOrEmpty(outputPath) ? DEFAULT_SCRIPTS_FOLDER : outputPath;
            if (!folder.StartsWith("Assets"))
                folder = Path.Combine(DEFAULT_SCRIPTS_FOLDER, folder);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var fileName = className + ".cs";
            var filePath = Path.Combine(folder, fileName).Replace("\\", "/");

            if (File.Exists(filePath) && !p.GetBool("overwrite", false))
            {
                return new ErrorResponse("FileExists", $"Script already exists at '{filePath}'. Use overwrite=true to replace.");
            }

            var content = GenerateContent(className, template, ns, methodStubs);
            File.WriteAllText(filePath, content);
            AssetDatabase.ImportAsset(filePath);

            return new SuccessResponse($"Script '{className}' created at '{filePath}'.", new
            {
                name = className,
                file_name = fileName,
                file_path = filePath,
                template = template,
                namespace = ns,
                method_count = methodStubs?.Count ?? 0,
            });
        }

        private static string GenerateContent(string className, string template, string ns, List<string> methodStubs)
        {
            var sb = new StringBuilder();
            var indent = "    ";

            // Standard usings
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");

            // Template-specific usings
            switch (template.ToLowerInvariant())
            {
                case "networkbehaviour":
                    sb.AppendLine("using Unity.Netcode;");
                    break;
                case "editorwindow":
                    sb.AppendLine("using UnityEditor;");
                    break;
            }

            sb.AppendLine();

            if (!string.IsNullOrEmpty(ns))
            {
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
            }

            // Class declaration
            var baseClass = GetBaseClass(template);
            sb.AppendLine($"{indent}public class {className} : {baseClass}");
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

                case "scriptableobject":
                    sb.AppendLine($"{indent}{indent}[CreateAssetMenu(menuName = \"ScriptableObjects/{className}\")]");
                    sb.AppendLine($"{indent}{indent}public class {className}Data : {baseClass}");
                    sb.AppendLine($"{indent}{indent}{{");
                    sb.AppendLine($"{indent}{indent}}}");
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
                    sb.AppendLine();
                    if (!string.IsNullOrEmpty(methodStubs?.FirstOrDefault()))
                    {
                        foreach (var method in methodStubs)
                            GenerateMethodStub(sb, indent, method);
                    }
                    break;

                case "withmethods":
                    if (methodStubs != null)
                    {
                        foreach (var method in methodStubs)
                            GenerateMethodStub(sb, indent, method);
                    }
                    break;

                case "scratch":
                    sb.AppendLine($"{indent}{indent}// Add your code here");
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

        private static void GenerateMethodStub(StringBuilder sb, string indent, string signature)
        {
            if (string.IsNullOrWhiteSpace(signature)) return;

            var trimmed = signature.Trim().TrimEnd(';', '{', '}');

            // Parse the signature to extract parts
            // e.g., "void Update()", "public void MyMethod(string arg)", "private IEnumerator StartCoroutine()"
            var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string returnType = "void";
            string methodName = "";
            string parameters = "";

            if (parts.Length >= 2)
            {
                returnType = parts[0];
                // Find the method name and parameters
                var lastSpaceIdx = trimmed.LastIndexOf(' ');
                var parenOpen = trimmed.IndexOf('(');
                if (parenOpen > 0)
                {
                    methodName = trimmed.Substring(lastSpaceIdx + 1, parenOpen - lastSpaceIdx - 1).Trim();
                    var parenClose = trimmed.IndexOf(')', parenOpen);
                    if (parenClose > parenOpen)
                        parameters = trimmed.Substring(parenOpen + 1, parenClose - parenOpen - 1).Trim();
                }
                else
                {
                    methodName = parts[parts.Length - 1];
                }
            }
            else
            {
                methodName = signature;
            }

            sb.AppendLine();
            sb.AppendLine($"{indent}{indent}/// <summary>");
            sb.AppendLine($"{indent}{indent}/// TODO: Implement {methodName}");
            sb.AppendLine($"{indent}{indent}/// </summary>");
            sb.AppendLine($"{indent}{indent}private {returnType} {methodName}({parameters})");
            sb.AppendLine($"{indent}{indent}{{");
            sb.AppendLine($"{indent}{indent}{indent}// TODO: Implement {methodName}");
            sb.AppendLine($"{indent}{indent}}}");
        }

        private static string GetBaseClass(string template)
        {
            switch (template.ToLowerInvariant())
            {
                case "networkbehaviour": return "NetworkBehaviour";
                case "editorwindow": return "EditorWindow";
                case "scriptableobject": return "ScriptableObject";
                case "statemachine": return "StateMachine";
                default: return "MonoBehaviour";
            }
        }
    }
}
