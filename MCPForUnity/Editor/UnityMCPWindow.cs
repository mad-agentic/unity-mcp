using System;
using UnityEditor;
using UnityEngine;

namespace MadAgent.UnityMCP.Editor
{
    [InitializeOnLoad]
    internal static class UnityMCPAutoStartHandler
    {
        private const string AutoStartPrefKey = "MadAgent.UnityMCP.AutoStartOnLoad";
        private const string PortPrefKey = "MadAgent.UnityMCP.Port";
        private const string SessionInitKey = "MadAgent.UnityMCP.AutoStart.SessionInitialized";

        static UnityMCPAutoStartHandler()
        {
            if (!EditorPrefs.GetBool(AutoStartPrefKey, false))
                return;

            if (SessionState.GetBool(SessionInitKey, false))
                return;

            SessionState.SetBool(SessionInitKey, true);
            EditorApplication.delayCall += AutoStartOnEditorLoad;
        }

        private static void AutoStartOnEditorLoad()
        {
            if (!EditorPrefs.GetBool(AutoStartPrefKey, false))
                return;

            var bridge = MCPBridge.Instance;

            if (bridge.IsRunning)
                return;

            var port = Mathf.Clamp(EditorPrefs.GetInt(PortPrefKey, 6060), 1, 65535);
            bridge.StartServer(port);
            Debug.Log($"[UnityMCP] Auto-started bridge on editor load at port {port}.");
        }
    }

    public class UnityMCPWindow : EditorWindow
    {
        private const string PortPrefKey = "MadAgent.UnityMCP.Port";
        private const string ClientPrefKey = "MadAgent.UnityMCP.Client";
        private const string AutoStartPrefKey = "MadAgent.UnityMCP.AutoStartOnLoad";
        private const string AutoConfigurePrefKey = "MadAgent.UnityMCP.AutoConfigureOnStart";

        private static readonly string[] Clients =
        {
            "Claude Desktop",
            "Claude Code",
            "Cursor",
            "VS Code Copilot",
            "Other"
        };

        private int _port;
        private int _clientIndex;
        private bool _autoStartOnLoad;
        private bool _autoConfigureOnStart;
        private MCPBridge _bridge;

        [MenuItem("Window/Unity MCP")]
        public static void OpenWindow()
        {
            var window = GetWindow<UnityMCPWindow>("Unity MCP");
            window.minSize = new Vector2(420f, 280f);
            window.Show();
        }

        private void OnEnable()
        {
            _port = EditorPrefs.GetInt(PortPrefKey, 6060);
            _clientIndex = Mathf.Clamp(EditorPrefs.GetInt(ClientPrefKey, 0), 0, Clients.Length - 1);
            _autoStartOnLoad = EditorPrefs.GetBool(AutoStartPrefKey, false);
            _autoConfigureOnStart = EditorPrefs.GetBool(AutoConfigurePrefKey, true);
            _bridge = GetOrCreateBridge();

            if (_autoConfigureOnStart && _bridge != null && _bridge.IsConnected)
            {
                EditorApplication.delayCall += CopyClientConfigToClipboardSilent;
            }
        }

        private void OnDisable()
        {
            EditorPrefs.SetInt(PortPrefKey, _port);
            EditorPrefs.SetInt(ClientPrefKey, _clientIndex);
            EditorPrefs.SetBool(AutoStartPrefKey, _autoStartOnLoad);
            EditorPrefs.SetBool(AutoConfigurePrefKey, _autoConfigureOnStart);
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField("Unity MCP Connection", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _bridge = _bridge != null ? _bridge : GetOrCreateBridge();

            DrawStatus();
            EditorGUILayout.Space(8);

            _port = EditorGUILayout.IntField("Port", Mathf.Clamp(_port, 1, 65535));
            _clientIndex = EditorGUILayout.Popup("Client", _clientIndex, Clients);
            _autoStartOnLoad = EditorGUILayout.ToggleLeft("Auto-Start on Editor Load", _autoStartOnLoad);
            _autoConfigureOnStart = EditorGUILayout.ToggleLeft("Auto-Copy Config when Connected", _autoConfigureOnStart);

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _bridge != null && !_bridge.IsRunning;
                if (GUILayout.Button("Start Server", GUILayout.Height(28)))
                {
                    _bridge.StartServer(_port);
                    if (_autoConfigureOnStart)
                    {
                        CopyClientConfigToClipboardSilent();
                    }
                }

                GUI.enabled = _bridge != null && _bridge.IsRunning;
                if (GUILayout.Button("Stop Server", GUILayout.Height(28)))
                {
                    _bridge.StopServer();
                }

                GUI.enabled = true;
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Configure", GUILayout.Height(24)))
            {
                CopyClientConfigToClipboard();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(GetClientHint(), MessageType.None);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Endpoint", EditorStyles.boldLabel);
            var endpoint = $"http://localhost:{_port}/mcp";
            EditorGUILayout.SelectableLabel(endpoint, EditorStyles.textField, GUILayout.Height(18));

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Open README", GUILayout.Height(22)))
            {
                Application.OpenURL("https://github.com/mad-agent/unity-mcp#quick-start");
            }
        }

        private void DrawStatus()
        {
            var isConnected = _bridge != null && _bridge.IsConnected;
            var isRunning = _bridge != null && _bridge.IsRunning;
            var color = GUI.color;
            GUI.color = isConnected ? new Color(0.45f, 0.9f, 0.45f)
                       : isRunning  ? new Color(1f, 0.9f, 0.4f)
                                    : new Color(0.95f, 0.6f, 0.45f);

            var status = isConnected ? "Connected ✓"
                        : isRunning  ? "Connecting..."
                                     : "Disconnected";
            var msgType = isConnected ? MessageType.Info
                         : isRunning  ? MessageType.Warning
                                      : MessageType.Warning;
            EditorGUILayout.HelpBox($"Status: {status}", msgType);

            GUI.color = color;
        }

        private MCPBridge GetOrCreateBridge() => MCPBridge.Instance;

        private void CopyClientConfigToClipboard()
        {
            var endpoint = $"http://localhost:{_port}/mcp";
            var config =
$"{{\n  \"mcpServers\": {{\n    \"unityMCP\": {{\n      \"url\": \"{endpoint}\"\n    }}\n  }}\n}}";

            EditorGUIUtility.systemCopyBuffer = config;
            EditorUtility.DisplayDialog(
                "Unity MCP",
                $"Config for '{Clients[_clientIndex]}' đã được copy vào clipboard.\n\nDán config vào MCP settings của client.",
                "OK");
        }

        private void CopyClientConfigToClipboardSilent()
        {
            var endpoint = $"http://localhost:{_port}/mcp";
            var config =
$"{{\n  \"mcpServers\": {{\n    \"unityMCP\": {{\n      \"url\": \"{endpoint}\"\n    }}\n  }}\n}}";

            EditorGUIUtility.systemCopyBuffer = config;
        }

        private string GetClientHint()
        {
            var client = Clients[Mathf.Clamp(_clientIndex, 0, Clients.Length - 1)];
            switch (client)
            {
                case "Claude Desktop":
                case "Claude Code":
                    return "Sau khi Configure, Claude thường tự nhận MCP server (auto-connect) sau khi reload client.";
                case "Cursor":
                case "VS Code Copilot":
                    return "Sau khi Configure, nhớ bật MCP server trong phần settings/extensions của client nếu đang tắt.";
                default:
                    return "Dùng nút Configure để copy JSON rồi dán vào file cấu hình MCP của client bạn.";
            }
        }
    }
}
