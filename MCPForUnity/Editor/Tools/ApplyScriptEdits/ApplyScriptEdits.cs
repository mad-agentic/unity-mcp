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
    /// Tool for applying text edits to Unity C# scripts: replace text, methods, insert, add using.
    /// </summary>
    [McpForUnityTool("script_apply_edits", group = "core",
        description = "Apply text edits to Unity C# scripts: replace text, replace method bodies, insert code after methods, add using statements.")]
    public static class ApplyScriptEdits
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "replace_text");

                switch (action.ToLowerInvariant())
                {
                    case "replace_text":
                        return ReplaceText(p);
                    case "replace_method":
                        return ReplaceMethod(p);
                    case "insert_after":
                    case "insertafter":
                        return InsertAfter(p);
                    case "add_using":
                    case "addusing":
                        return AddUsing(p);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: replace_text, replace_method, insert_after, add_using.");
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

        private static object ReplaceText(ToolParams p)
        {
            var scriptPath = p.RequireString("script_path");
            var oldText = p.RequireString("old_text");
            var newText = p.GetString("new_text", "");

            var fullPath = ResolveScriptPath(scriptPath);
            if (!File.Exists(fullPath))
                return new ErrorResponse("ScriptNotFound", $"Script not found at '{fullPath}'.");

            var content = File.ReadAllText(fullPath);

            if (!content.Contains(oldText))
                return new ErrorResponse("TextNotFound", $"Old text not found in script: '{oldText}'.");

            var occurrences = CountOccurrences(content, oldText);
            content = content.Replace(oldText, newText);

            File.WriteAllText(fullPath, content);
            AssetDatabase.ImportAsset(fullPath);

            return new SuccessResponse($"Replaced {occurrences} occurrence(s) of text in '{Path.GetFileName(fullPath)}'.", new
            {
                script_path = scriptPath,
                full_path = fullPath,
                old_text = oldText,
                new_text = newText,
                occurrences = occurrences,
            });
        }

        private static object ReplaceMethod(ToolParams p)
        {
            var scriptPath = p.RequireString("script_path");
            var methodName = p.RequireString("method_name");
            var newMethodBody = p.GetString("new_method_body", "");

            var fullPath = ResolveScriptPath(scriptPath);
            if (!File.Exists(fullPath))
                return new ErrorResponse("ScriptNotFound", $"Script not found at '{fullPath}'.");

            var content = File.ReadAllText(fullPath);

            // Find the method declaration and its body
            var methodPattern = $@"(?<declaration>[^\{{}}]*(?:public|private|protected|internal)?\s*(?:static)?\s*\w+(?:<[^>]+>)?\s+{Regex.Escape(methodName)}\s*\([^)]*\)(?:\s*:\s*base\([^)]*\))?[^\{{}}]*)\{{[^\}}]*\}}";
            var match = Regex.Match(content, methodPattern, RegexOptions.Singleline);

            if (!match.Success)
                return new ErrorResponse("MethodNotFound", $"Method '{methodName}' not found in script.");

            var declaration = match.Groups["declaration"].Value.TrimEnd();
            var newMethod = declaration + "\n{\n" + IndentText(newMethodBody, "        ") + "\n    }";

            content = Regex.Replace(content, methodPattern, newMethod, RegexOptions.Singleline);

            File.WriteAllText(fullPath, content);
            AssetDatabase.ImportAsset(fullPath);

            return new SuccessResponse($"Method body of '{methodName}' replaced in '{Path.GetFileName(fullPath)}'.", new
            {
                script_path = scriptPath,
                method_name = methodName,
                new_body_preview = newMethodBody.Length > 200 ? newMethodBody.Substring(0, 200) + "..." : newMethodBody,
            });
        }

        private static object InsertAfter(ToolParams p)
        {
            var scriptPath = p.RequireString("script_path");
            var afterMethod = p.RequireString("after_method");
            var newText = p.GetString("new_text", "");

            var fullPath = ResolveScriptPath(scriptPath);
            if (!File.Exists(fullPath))
                return new ErrorResponse("ScriptNotFound", $"Script not found at '{fullPath}'.");

            var content = File.ReadAllText(fullPath);

            // Find the closing brace of the target method
            var methodPattern = $@"(?<declaration>[^\{{}}]*(?:public|private|protected|internal)?\s*(?:static)?\s*\w+(?:<[^>]+>)?\s+{Regex.Escape(afterMethod)}\s*\([^)]*\)(?:\s*:\s*base\([^)]*\))?[^\{{}}]*)\{{";
            var match = Regex.Match(content, methodPattern, RegexOptions.Singleline);

            if (!match.Success)
                return new ErrorResponse("MethodNotFound", $"Method '{afterMethod}' not found in script.");

            var declaration = match.Groups["declaration"].Value;
            var startIndex = match.Index + match.Length;

            // Find the matching closing brace
            var braceCount = 1;
            var i = startIndex;
            while (i < content.Length && braceCount > 0)
            {
                if (content[i] == '{') braceCount++;
                else if (content[i] == '}') braceCount--;
                i++;
            }

            var beforeBrace = content.Substring(0, i - 1);
            var afterBrace = content.Substring(i - 1);

            var insertText = "\n" + IndentText(newText, "    ");
            var newContent = beforeBrace + insertText + afterBrace;

            File.WriteAllText(fullPath, newContent);
            AssetDatabase.ImportAsset(fullPath);

            return new SuccessResponse($"Inserted text after method '{afterMethod}' in '{Path.GetFileName(fullPath)}'.", new
            {
                script_path = scriptPath,
                after_method = afterMethod,
                inserted_text_preview = newText.Length > 200 ? newText.Substring(0, 200) + "..." : newText,
            });
        }

        private static object AddUsing(ToolParams p)
        {
            var scriptPath = p.RequireString("script_path");
            var usingStatement = p.RequireString("using_statement");

            var fullPath = ResolveScriptPath(scriptPath);
            if (!File.Exists(fullPath))
                return new ErrorResponse("ScriptNotFound", $"Script not found at '{fullPath}'.");

            var content = File.ReadAllText(fullPath);

            // Extract namespace from using statement
            var nsMatch = Regex.Match(usingStatement, @"using\s+([\w.]+)\s*;");
            if (!nsMatch.Success)
                return new ErrorResponse("InvalidUsingStatement", $"Invalid using statement: '{usingStatement}'.");

            var ns = nsMatch.Groups[1].Value;

            // Check if already exists
            if (content.Contains(usingStatement.Trim()))
                return new ErrorResponse("UsingExists", $"Using statement '{usingStatement}' already exists.");

            // Check if namespace already imported under different form
            var alreadyImported = Regex.IsMatch(content, $@"using\s+{Regex.Escape(ns)}\s*;");
            if (alreadyImported)
                return new ErrorResponse("UsingExists", $"Namespace '{ns}' is already imported.");

            // Find the last using statement and insert after it
            var lastUsingMatch = Regex.Match(content, @"using\s+[\w.]+\s*;\s*(?=using|namespace|class|public|internal)", RegexOptions.Multiline);
            int insertIndex;

            if (lastUsingMatch.Success)
            {
                insertIndex = lastUsingMatch.Index + lastUsingMatch.Length;
            }
            else
            {
                // Insert at the beginning after any file-level attributes
                var firstNonUsing = Regex.Match(content, @"^[^\s]", RegexOptions.Multiline);
                insertIndex = firstNonUsing.Success ? firstNonUsing.Index : 0;
            }

            var newContent = content.Insert(insertIndex, usingStatement.Trim() + "\n");

            File.WriteAllText(fullPath, newContent);
            AssetDatabase.ImportAsset(fullPath);

            return new SuccessResponse($"Added using statement '{usingStatement}' to '{Path.GetFileName(fullPath)}'.", new
            {
                script_path = scriptPath,
                using_statement = usingStatement,
                namespace_added = ns,
            });
        }

        // ─── Helpers ───────────────────────────────────────────────────────────────

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

        private static int CountOccurrences(string content, string substring)
        {
            int count = 0;
            int index = 0;
            while ((index = content.IndexOf(substring, index, StringComparison.Ordinal)) != -1)
            {
                count++;
                index += substring.Length;
            }
            return count;
        }

        private static string IndentText(string text, string indent)
        {
            if (string.IsNullOrEmpty(text)) return indent + "// (empty)";
            var lines = text.Split('\n');
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (i == 0 && string.IsNullOrWhiteSpace(line)) continue;
                sb.AppendLine(indent + line.TrimEnd());
            }
            return sb.ToString().TrimEnd();
        }
    }
}
