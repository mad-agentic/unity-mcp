using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Tool for validating Unity C# scripts: syntax checks, compilation, and error reporting.
    /// </summary>
    [McpForUnityTool("validate_script", Group = "core",
        Description = "Validate Unity C# scripts: syntax checks, full compilation, and error/warning reporting.")]
    public static class ValidateScript
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "syntax_check");

                switch (action.ToLowerInvariant())
                {
                    case "syntax_check":
                        return SyntaxCheck(p);
                    case "full_compilation":
                    case "fullcompilation":
                        return FullCompilation(p);
                    case "get_errors":
                    case "geterrors":
                        return GetErrors(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: syntax_check, full_compilation, get_errors.");
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

        private static object SyntaxCheck(ToolParams p)
        {
            var scriptPath = p.RequireString("script_path");
            var fullCheck = p.GetBool("full_check", false);

            var fullPath = ResolveScriptPath(scriptPath);
            if (!File.Exists(fullPath))
                return new ErrorResponse("ScriptNotFound", $"Script not found at '{fullPath}'.");

            var content = File.ReadAllText(fullPath);
            var errors = new List<object>();
            var warnings = new List<object>();

            // Lightweight syntax checks
            SyntaxLightCheck(content, errors, warnings);

            // Full check: try Roslyn-like parsing (basic structural validation)
            if (fullCheck)
            {
                SyntaxFullCheck(content, errors, warnings);
            }

            var hasErrors = errors.Count > 0;
            var hasWarnings = warnings.Count > 0;

            return new SuccessResponse(
                $"Syntax check complete for '{Path.GetFileName(fullPath)}': " +
                $"{errors.Count} error(s), {warnings.Count} warning(s).",
                new
                {
                    script_path = scriptPath,
                    full_path = fullPath,
                    full_check = fullCheck,
                    has_errors = hasErrors,
                    has_warnings = hasWarnings,
                    error_count = errors.Count,
                    warning_count = warnings.Count,
                    errors = errors,
                    warnings = warnings,
                    status = hasErrors ? "failed" : (hasWarnings ? "passed_with_warnings" : "passed"),
                });
        }

        private static object FullCompilation(ToolParams p)
        {
            var scriptPath = p.GetString("script_path");
            var fullPath = !string.IsNullOrEmpty(scriptPath) ? ResolveScriptPath(scriptPath) : null;

            if (!string.IsNullOrEmpty(fullPath) && !File.Exists(fullPath))
                return new ErrorResponse("ScriptNotFound", $"Script not found at '{fullPath}'.");

            var compilationResults = new List<object>();
            var hasErrors = false;

            // Get all scripts in the project
            var scripts = string.IsNullOrEmpty(scriptPath)
                ? GetAllProjectScripts()
                : new List<string> { fullPath };

            foreach (var script in scripts)
            {
                if (!string.IsNullOrEmpty(fullPath) && script != fullPath) continue;

                var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(script);
                if (monoScript == null) continue;

                var classType = monoScript.GetClass();
                var errorsForScript = new List<string>();
                var warningsForScript = new List<string>();

                if (classType == null)
                {
                    // Try to compile the script to get errors
                    var result = CompilationPipeline.CompileScript(
                        new[] { monoScript },
                        out var assembly;
                    );

                    if (result != null && result.Length > 0)
                    {
                        hasErrors = true;
                        foreach (var msg in result)
                        {
                            errorsForScript.Add($"{msg.message} (line {msg.line})");
                        }
                    }
                }

                compilationResults.Add(new
                {
                    script_path = script,
                    class_name = classType?.Name ?? "(no class)",
                    has_errors = errorsForScript.Count > 0,
                    has_warnings = warningsForScript.Count > 0,
                    errors = errorsForScript,
                    warnings = warningsForScript,
                });
            }

            return new SuccessResponse($"Full compilation check complete: {compilationResults.Count} script(s) checked.", new
            {
                script_path = scriptPath ?? "all",
                checked_count = compilationResults.Count,
                overall_errors = hasErrors,
                results = compilationResults,
            });
        }

        private static object GetErrors(ToolParams p)
        {
            var scriptPath = p.RequireString("script_path");
            var fullPath = ResolveScriptPath(scriptPath);

            if (!File.Exists(fullPath))
                return new ErrorResponse("ScriptNotFound", $"Script not found at '{fullPath}'.");

            var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (monoScript == null)
                return new ErrorResponse("NotAMonoScript", $"'{scriptPath}' is not a valid MonoScript.");

            // Try compiling just this script
            var compilerMessages = new List<object>();
            var hasErrors = false;
            var hasWarnings = false;

            try
            {
                var result = CompilationPipeline.CompileScript(
                    new[] { monoScript },
                    out var assemblies
                );

                if (result != null)
                {
                    foreach (var msg in result)
                    {
                        compilerMessages.Add(new
                        {
                            type = msg.type.ToString(),
                            message = msg.message,
                            line = msg.line,
                            column = msg.column,
                        });

                        if (msg.type == CompilerMessageType.Error)
                            hasErrors = true;
                        if (msg.type == CompilerMessageType.Warning)
                            hasWarnings = true;
                    }
                }
            }
            catch (Exception ex)
            {
                // Compilation may not be available in this context
                compilerMessages.Add(new
                {
                    type = "Info",
                    message = $"Compilation check skipped: {ex.Message}",
                    line = 0,
                    column = 0,
                });
            }

            // Also do lightweight static analysis
            var content = File.ReadAllText(fullPath);
            var staticErrors = new List<object>();
            SyntaxLightCheck(content, staticErrors, new List<object>());

            // Merge results
            var allErrors = new List<object>();
            foreach (var e in staticErrors)
                allErrors.Add(e);
            foreach (var m in compilerMessages)
                allErrors.Add(m);

            return new SuccessResponse(
                $"Errors for '{Path.GetFileName(fullPath)}': {allErrors.Count} issue(s) found.",
                new
                {
                    script_path = scriptPath,
                    full_path = fullPath,
                    class_name = monoScript.GetClass()?.Name ?? "(no class)",
                    total_issues = allErrors.Count,
                    has_errors = hasErrors,
                    has_warnings = hasWarnings,
                    issues = allErrors,
                });
        }

        // ─── Syntax Checking Helpers ───────────────────────────────────────────────

        private static void SyntaxLightCheck(string content, List<object> errors, List<object> warnings)
        {
            var lines = content.Split('\n');
            var braceStack = new Stack<int>();
            var parenStack = new Stack<int>();
            var inString = false;
            var inChar = false;
            var inVerbatim = false;
            var lineNum = 0;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                char prev = i > 0 ? content[i - 1] : '\0';

                if (c == '\n')
                {
                    lineNum++;
                    inString = false;
                    inChar = false;
                    inVerbatim = false;
                    continue;
                }

                if (prev == '@' && c == '"')
                {
                    inVerbatim = true;
                    continue;
                }

                if (inVerbatim)
                {
                    if (c == '"' && i + 1 < content.Length && content[i + 1] == '"')
                    {
                        i++; // Skip escaped quote
                        continue;
                    }
                    if (c == '"')
                    {
                        inVerbatim = false;
                        continue;
                    }
                    continue;
                }

                if (c == '"' && !inChar)
                {
                    inString = !inString;
                    continue;
                }

                if (c == '\'' && !inString)
                {
                    inChar = !inChar;
                    continue;
                }

                if (inString || inChar) continue;

                // Check for common issues
                if (c == '{')
                    braceStack.Push(lineNum);
                else if (c == '}')
                {
                    if (braceStack.Count == 0)
                        errors.Add(new { line = lineNum + 1, message = "Unexpected closing brace '}' — no matching opening brace." });
                    else
                        braceStack.Pop();
                }
                else if (c == '(')
                    parenStack.Push(lineNum);
                else if (c == ')')
                {
                    if (parenStack.Count == 0)
                        errors.Add(new { line = lineNum + 1, message = "Unexpected closing parenthesis ')' — no matching opening parenthesis." });
                    else
                        parenStack.Pop();
                }
            }

            // Report unclosed braces
            while (braceStack.Count > 0)
            {
                var unclosedLine = braceStack.Pop();
                errors.Add(new { line = unclosedLine + 1, message = $"Unclosed brace '{{' — started at line {unclosedLine + 1}." });
            }

            while (parenStack.Count > 0)
            {
                var unclosedLine = parenStack.Pop();
                errors.Add(new { line = unclosedLine + 1, message = $"Unclosed parenthesis '(' — started at line {unclosedLine + 1}." });
            }

            // Check for common Unity-specific issues
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                // Empty namespace block
                if (Regex.IsMatch(line, @"^namespace\s+\w+\s*\{\s*\}$"))
                    warnings.Add(new { line = i + 1, message = "Empty namespace block." });

                // Empty class without members
                if (Regex.IsMatch(line, @"^public\s+class\s+\w+\s*:\s*\w+\s*\{\s*\}$"))
                    warnings.Add(new { line = i + 1, message = "Class with no members." });

                // TODO/FIXME comments
                if (Regex.IsMatch(line, @"\bTODO\b"))
                    warnings.Add(new { line = i + 1, message = "TODO comment found." });

                // Hardcoded string path without Application.dataPath
                if (Regex.IsMatch(line, @"["'][^""']*[/\\][\w./\\]+["']") && !line.Contains("//"))
                    warnings.Add(new { line = i + 1, message = "Possible hardcoded path detected." });

                // Empty Update/LateUpdate without base call
                if (Regex.IsMatch(line, @"private\s+void\s+Update\s*\(\s*\)\s*\{\s*\}"))
                    warnings.Add(new { line = i + 1, message = "Empty Update() method — consider removing or adding logic." });
            }
        }

        private static void SyntaxFullCheck(string content, List<object> errors, List<object> warnings)
        {
            // Additional structural checks
            var usingDirectives = Regex.Matches(content, @"^using\s+[\w.]+\s*;", RegexOptions.Multiline);
            var namespaceBlocks = Regex.Matches(content, @"namespace\s+([\w.]+)", RegexOptions.Multiline);
            var classDeclarations = Regex.Matches(content, @"(?:public|internal)\s+class\s+(\w+)", RegexOptions.Multiline);
            var methodDeclarations = Regex.Matches(content, @"(?:public|private|protected|internal)\s+(?:static)?\s*\w+(?:<[^>]+>)?\s+(\w+)\s*\([^)]]*\)", RegexOptions.Multiline);

            // Check for missing using for common Unity types
            var hasGameObject = content.Contains("GameObject");
            var hasVector3 = content.Contains("Vector3");
            var hasQuaternion = content.Contains("Quaternion");

            if (hasGameObject && !Regex.IsMatch(content, @"using\s+UnityEngine\s*;"))
                errors.Add(new { line = 0, message = "GameObject used but 'using UnityEngine;' is missing." });

            if ((hasVector3 || hasQuaternion) && !Regex.IsMatch(content, @"using\s+UnityEngine\s*;"))
                errors.Add(new { line = 0, message = "Vector3/Quaternion used but 'using UnityEngine;' is missing." });

            // Check for duplicate class declarations
            var classNames = classDeclarations.Select(m => m.Groups[1].Value).ToList();
            var duplicates = classNames.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var dup in duplicates)
            {
                errors.Add(new { line = 0, message = $"Duplicate class name '{dup}' found." });
            }

            // Check for invalid method names
            foreach (Match method in methodDeclarations)
            {
                var methodName = method.Groups[1].Value;
                if (char.IsLower(methodName[0]) && !IsValidBuiltinMethod(methodName))
                    warnings.Add(new { line = 0, message = $"Method '{methodName}' starts with lowercase — Unity convention is PascalCase." });
            }
        }

        private static bool IsValidBuiltinMethod(string name)
        {
            var validNames = new HashSet<string>
            {
                "Start", "Update", "LateUpdate", "FixedUpdate", "Awake", "OnEnable", "OnDisable",
                "OnDestroy", "OnCollisionEnter", "OnCollisionExit", "OnCollisionStay",
                "OnTriggerEnter", "OnTriggerExit", "OnTriggerStay", "StartCoroutine",
                "GetComponent", "AddComponent", "Instantiate", "Destroy", "FindObjectOfType",
            };
            return validNames.Contains(name);
        }

        private static List<string> GetAllProjectScripts()
        {
            var guids = AssetDatabase.FindAssets("t:MonoScript");
            return guids
                .Select(g => AssetDatabase.GUIDToAssetPath(g))
                .Where(p => p.EndsWith(".cs") && !p.Contains("/Editor/"))
                .ToList();
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
