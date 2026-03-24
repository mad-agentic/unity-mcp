using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MadAgent.UnityMCP.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace MadAgent.UnityMCP.Editor
{
    /// <summary>
    /// MCP Bridge — runs an HTTP/WebSocket server within the Unity Editor
    /// that accepts commands from the Python MCP server and dispatches them
    /// via CommandRegistry.
    /// </summary>
    public class MCPBridge : MonoBehaviour
    {
        public static MCPBridge Instance { get; private set; }

        [SerializeField] private int _port = 8080;
        [SerializeField] private bool _autoStart = false;

        private string _instanceId;
        private string _instanceName;
        private string _projectPath;
        private string _projectName;
        private string _unityVersion;
        private string _platform;

        private UnityWebRequest _listener;
        private Task _serverTask;
        private bool _isRunning;
        private List<string> _connectedClients = new List<string>();

        // ─── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _instanceId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _instanceName = Application.productName;
            _projectPath = Application.dataPath.Replace("/Assets", "").Replace("\\Assets", "");
            _projectName = Application.productName;
            _unityVersion = Application.unityVersion;
            _platform = Application.platform.ToString();
        }

        private void Start()
        {
            if (_autoStart)
                StartServer(_port);
        }

        private void OnDestroy()
        {
            StopServer();
            if (Instance == this)
                Instance = null;
        }

        // ─── Public API ────────────────────────────────────────────────────────

        /// <summary>Start the MCP bridge HTTP server.</summary>
        public void StartServer(int port = 8080)
        {
            if (_isRunning) return;
            _port = port;
            _isRunning = true;

            _serverTask = Task.Run(RunServerAsync);
            Debug.Log($"[UnityMCP] Bridge started on port {_port} (id={_instanceId})");
        }

        /// <summary>Stop the MCP bridge HTTP server.</summary>
        public void StopServer()
        {
            _isRunning = false;
            if (_serverTask != null && !_serverTask.IsCompleted)
            {
                _serverTask.Wait(1000);
            }
            _serverTask = null;
            Debug.Log("[UnityMCP] Bridge stopped.");
        }

        /// <summary>Get info about this Unity instance for client routing.</summary>
        public Dictionary<string, object> GetInstanceInfo()
        {
            return new Dictionary<string, object>
            {
                ["name"] = _instanceName,
                ["project_path"] = _projectPath,
                ["project_name"] = _projectName,
                ["hash_id"] = _instanceId,
                ["unity_version"] = _unityVersion,
                ["platform"] = _platform,
                ["connected_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
        }

        public bool IsRunning => _isRunning;
        public string InstanceId => _instanceId;
        public string InstanceName => _instanceName;

        // ─── Server Loop ────────────────────────────────────────────────────────

        private async Task RunServerAsync()
        {
            // Simple async HTTP listener using UnityWebRequest
            // In a full implementation, this would use a more robust HTTP server
            // For now, we use Unity's NetworkTransport or a simple socket approach

            while (_isRunning)
            {
                try
                {
                    await Task.Delay(100);
                }
                catch
                {
                    break;
                }
            }
        }

        // ─── Command Dispatch ───────────────────────────────────────────────────

        /// <summary>
        /// Handle an incoming MCP command from the Python server.
        /// Called by the HTTP/WebSocket listener.
        /// </summary>
        public JObject HandleCommand(string command, JObject @params)
        {
            try
            {
                Debug.Log($"[UnityMCP] Command: {command} {@params}");
                var result = CommandRegistry.InvokeCommand(command, @params);
                return JObject.FromObject(result);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return JObject.FromObject(new ErrorResponse("InternalError", ex.Message));
            }
        }

        /// <summary>
        /// Handle an incoming MCP command asynchronously.
        /// </summary>
        public async Task<JObject> HandleCommandAsync(string command, JObject @params)
        {
            try
            {
                Debug.Log($"[UnityMCP] Command (async): {command}");
                var result = await CommandRegistry.InvokeCommandAsync(command, @params);
                return JObject.FromObject(result);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return JObject.FromObject(new ErrorResponse("InternalError", ex.Message));
            }
        }
    }
}
