using System.Threading.Tasks;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace MadAgent.UnityMCP.Editor.Tools
{
    /// <summary>
    /// Demo tool — returns Unity environment info.
    /// This serves as a reference implementation for all MCP tools.
    /// </summary>
    [McpForUnityTool("ping", group = "core", description = "Ping Unity to verify connectivity and get environment info.")]
    public static class Ping
    {
        public static object HandleCommand(JObject @params)
        {
            return new SuccessResponse("Unity is responsive.", new
            {
                status = "ok",
                unity_version = Application.unityVersion,
                platform = Application.platform.ToString(),
                product_name = Application.productName,
                data_path = Application.dataPath,
            });
        }
    }

    /// <summary>
    /// Echo tool — echoes back the parameters received.
    /// Useful for testing and debugging.
    /// </summary>
    [McpForUnityTool("echo", group = "core", description = "Echo back the received parameters. Useful for testing connectivity.")]
    public static class Echo
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);
            var message = p.GetString("message", "message", "Hello from Unity!");
            return new SuccessResponse($"Echo: {message}", new
            {
                echoed = message,
                timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            });
        }
    }

    /// <summary>
    /// Get environment info about the current Unity project.
    /// </summary>
    [McpForUnityTool("get_environment", group = "core", description = "Get information about the current Unity environment, project, and editor state.")]
    public static class GetEnvironment
    {
        public static object HandleCommand(JObject @params)
        {
            var p = new ToolParams(@params);

            return new SuccessResponse("Environment info retrieved.", new
            {
                project = new
                {
                    name = Application.productName,
                    path = Application.dataPath.Replace("/Assets", ""),
                    unity_version = Application.unityVersion,
                    platform = Application.platform.ToString(),
                    install_path = Application.installMode.ToString(),
                    is_playing = Application.isPlaying,
                    is_paused = EditorApplication.isPaused,
                },
                scenes = new
                {
                    count = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings,
                    active = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                },
                targets = new
                {
                    current = UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString(),
                }
            });
        }
    }
}
