using System;
using System.Collections.Generic;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Console log reader supporting read, clear, get_errors, get_warnings, and get_logs.
    /// Uses UnityEditor.LogEntries and UnityEditor.Debug for console access.
    /// </summary>
    [McpForUnityTool("read_console", group = "core",
        description = "Read, clear, and filter Unity console output: errors, warnings, logs. Useful for debugging and CI.")]
    public static class ReadConsole
    {
        private const int DefaultCount = 100;
        private const int MaxCount = 1000;

        public static object HandleCommand(JObject @params)
        {
            try
            {
                var p = new ToolParams(@params);
                var action = p.GetString("action", "read");
                var count = Math.Min(p.GetInt("count", DefaultCount), MaxCount);
                var filter = p.GetString("filter");

                switch (action.ToLowerInvariant())
                {
                    case "read":
                        return ReadConsoleEntries(p, count, filter);
                    case "clear":
                        return ClearConsole();
                    case "get_errors":
                    case "geterrors":
                        return GetErrors(p, count, filter);
                    case "get_warnings":
                    case "getwarnings":
                        return GetWarnings(p, count, filter);
                    case "get_logs":
                    case "getlogs":
                        return GetLogs(p, count, filter);
                    default:
                        return new ErrorResponse("InvalidAction",
                            $"Unknown action '{action}'. Valid actions: read, clear, get_errors, get_warnings, get_logs.");
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

        private static object ReadConsoleEntries(ToolParams p, int count, string filter)
        {
            return GetConsoleEntries(count, filter, LogType.Log, LogType.Warning, LogType.Error, LogType.Exception, LogType.Assert);
        }

        private static object ClearConsole()
        {
            var assembly = typeof(EditorWindow).Assembly;
            var logEntriesType = assembly.GetType("UnityEditor.LogEntries");
            if (logEntriesType != null)
            {
                var clearMethod = logEntriesType.GetMethod("Clear");
                clearMethod?.Invoke(null, null);
            }

            return new SuccessResponse("Console cleared.", null);
        }

        private static object GetErrors(ToolParams p, int count, string filter)
        {
            return GetConsoleEntries(count, filter, LogType.Error, LogType.Exception, LogType.Assert);
        }

        private static object GetWarnings(ToolParams p, int count, string filter)
        {
            return GetConsoleEntries(count, filter, LogType.Warning);
        }

        private static object GetLogs(ToolParams p, int count, string filter)
        {
            return GetConsoleEntries(count, filter, LogType.Log);
        }

        private static object GetConsoleEntries(int count, string filter, params LogType[] types)
        {
            var entries = new List<object>();
            var typeSet = new HashSet<LogType>(types);

            try
            {
                var assembly = typeof(EditorWindow).Assembly;
                var logEntriesType = assembly.GetType("UnityEditor.LogEntries");
                var logEntryType = assembly.GetType("UnityEditor.LogEntry");

                if (logEntriesType == null || logEntryType == null)
                {
                    return new ErrorResponse("ConsoleAccessFailed",
                        "Could not access Unity console entries.");
                }

                var getCountMethod = logEntriesType.GetMethod("GetCount");
                var startMethod = logEntriesType.GetMethod("StartGettingEntries");
                var getEntryMethod = logEntriesType.GetMethod("GetEntryInternal");
                var endMethod = logEntriesType.GetMethod("EndGettingEntries");

                int totalCount = (int)(getCountMethod?.Invoke(null, null) ?? 0);

                // Get entries in reverse order (newest first) up to count
                int startIdx = Math.Max(0, totalCount - count);
                int entriesToRead = Math.Min(count, totalCount);

                var entry = Activator.CreateInstance(logEntryType);
                int entryIndex = 0;
                var hasEntry = (bool)startMethod.Invoke(null, null);

                int processed = 0;
                int skipped = 0;

                while (hasEntry)
                {
                    getEntryMethod?.Invoke(null, new object[] { entryIndex, entry });

                    // Only collect entries of requested types
                    var entryLogType = GetLogType(entry);
                    if (typeSet.Contains(entryLogType))
                    {
                        if (skipped < startIdx)
                        {
                            skipped++;
                        }
                        else
                        {
                            var message = GetMessage(entry);
                            var condition = GetCondition(entry);
                            var fullMessage = string.IsNullOrEmpty(condition) ? message : condition;

                            // Apply filter if provided
                            if (string.IsNullOrEmpty(filter) ||
                                message.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                condition.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                entries.Add(new
                                {
                                    message = message,
                                    condition = condition,
                                    log_type = entryLogType.ToString(),
                                    line = GetLine(entry),
                                    file = GetFile(entry),
                                    time_stamp = GetTimeStamp(entry),
                                    instance_id = GetInstanceID(entry),
                                });
                            }
                        }

                        processed++;
                        if (processed >= entriesToRead) break;
                    }

                    entryIndex++;
                    if (entryIndex >= totalCount) break;
                }

                endMethod?.Invoke(null, null);

                // Reverse to get newest first
                entries.Reverse();

                return new SuccessResponse(
                    $"Retrieved {entries.Count} console entry/entries (total available: {totalCount}, filter: '{filter ?? "none"}').",
                    new
                    {
                        count = entries.Count,
                        total_available = totalCount,
                        filter = filter,
                        log_types = Array.ConvertAll(types, t => t.ToString()),
                        entries = entries
                    });
            }
            catch (Exception ex)
            {
                return new ErrorResponse("ConsoleReadFailed",
                    $"Failed to read console entries: {ex.Message}");
            }
        }

        private static LogType GetLogType(object entry)
        {
            var typeField = entry.GetType().GetField("type");
            if (typeField != null)
            {
                var value = typeField.GetValue(entry);
                if (value is int intVal)
                {
                    return (LogType)intVal;
                }
            }
            return LogType.Log;
        }

        private static string GetMessage(object entry)
        {
            var msgField = entry.GetType().GetField("message");
            return msgField?.GetValue(entry)?.ToString() ?? "";
        }

        private static string GetCondition(object entry)
        {
            var condField = entry.GetType().GetField("condition");
            return condField?.GetValue(entry)?.ToString() ?? "";
        }

        private static int GetLine(object entry)
        {
            var lineField = entry.GetType().GetField("line");
            if (lineField != null)
            {
                var value = lineField.GetValue(entry);
                if (value is int intVal) return intVal;
            }
            return 0;
        }

        private static string GetFile(object entry)
        {
            var fileField = entry.GetType().GetField("file");
            return fileField?.GetValue(entry)?.ToString() ?? "";
        }

        private static double GetTimeStamp(object entry)
        {
            var tsField = entry.GetType().GetField("timestamp");
            if (tsField != null)
            {
                var value = tsField.GetValue(entry);
                if (value is double d) return d;
            }
            return 0.0;
        }

        private static int GetInstanceID(object entry)
        {
            var idField = entry.GetType().GetField("instanceID");
            if (idField != null)
            {
                var value = idField.GetValue(entry);
                if (value is int i) return i;
            }
            return 0;
        }
    }
}
