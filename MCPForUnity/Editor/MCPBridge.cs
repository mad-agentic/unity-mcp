using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace MadAgent.UnityMCP.Editor
{
    /// <summary>
    /// MCP Bridge — WebSocket client that connects to the Python MCP server.
    /// Receives JSON-RPC commands, dispatches them via CommandRegistry on the
    /// Unity main thread, and returns responses.
    /// </summary>
    public sealed class MCPBridge
    {
        private static readonly Lazy<MCPBridge> _lazyInstance = new Lazy<MCPBridge>(() => new MCPBridge());
        public static MCPBridge Instance => _lazyInstance.Value;

        private int _port = 6060;

        private string _instanceId;
        private string _instanceName;
        private string _projectPath;
        private string _projectName;
        private string _unityVersion;
        private string _platform;

        private Task _serverTask;
        private CancellationTokenSource _cts;
        private bool _isRunning;
        private bool _isConnected;

        // Thread-safe queue for dispatching commands to the Unity main thread
        private static readonly ConcurrentQueue<PendingCommand> _commandQueue = new ConcurrentQueue<PendingCommand>();
        private static bool _updateRegistered;

        private class PendingCommand
        {
            public string Method;
            public JObject Params;
            public TaskCompletionSource<JObject> Tcs;
        }

        private MCPBridge()
        {
            _instanceId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _instanceName = Application.productName;
            _projectPath = Application.dataPath.Replace("/Assets", "").Replace("\\Assets", "");
            _projectName = Application.productName;
            _unityVersion = Application.unityVersion;
            _platform = Application.platform.ToString();

            EnsureUpdateRegistered();
        }

        // ─── Main Thread Dispatch ────────────────────────────────────────────────

        private static void EnsureUpdateRegistered()
        {
            if (!_updateRegistered)
            {
                EditorApplication.update += ProcessCommandQueue;
                _updateRegistered = true;
            }
        }

        private static void ProcessCommandQueue()
        {
            while (_commandQueue.TryDequeue(out var cmd))
            {
                try
                {
                    var result = CommandRegistry.InvokeCommand(cmd.Method, cmd.Params);
                    cmd.Tcs.TrySetResult(JObject.FromObject(result));
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    cmd.Tcs.TrySetResult(JObject.FromObject(new ErrorResponse("InternalError", ex.Message)));
                }
            }
        }

        // ─── Public API ────────────────────────────────────────────────────────

        /// <summary>Connect to the Python MCP server WebSocket hub.</summary>
        public void StartServer(int port = 6060)
        {
            if (_isRunning) return;
            _port = port;
            _isRunning = true;
            _cts = new CancellationTokenSource();
            EnsureUpdateRegistered();
            _serverTask = Task.Run(() => RunClientLoopAsync(_cts.Token));
            Debug.Log($"[UnityMCP] Connecting to Python server on ws://127.0.0.1:{_port}/hub/plugin");
        }

        /// <summary>Disconnect from the Python MCP server.</summary>
        public void StopServer()
        {
            _isRunning = false;
            _isConnected = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
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
        public bool IsConnected => _isConnected;
        public string InstanceId => _instanceId;
        public string InstanceName => _instanceName;

        // ─── WebSocket Client Loop ──────────────────────────────────────────────

        private async Task RunClientLoopAsync(CancellationToken ct)
        {
            var uri = new Uri($"ws://127.0.0.1:{_port}/hub/plugin");
            var retryDelay = 2000; // ms

            while (_isRunning && !ct.IsCancellationRequested)
            {
                using var ws = new ClientWebSocket();
                try
                {
                    await ws.ConnectAsync(uri, ct);
                    _isConnected = true;
                    Debug.Log($"[UnityMCP] Connected to Python server at {uri}");

                    // Send registration
                    var regPayload = JsonConvert.SerializeObject(new
                    {
                        type = "register",
                        info = new
                        {
                            name = _instanceName,
                            id = _instanceId,
                            projectName = _projectName,
                            projectPath = _projectPath,
                            unityVersion = _unityVersion,
                            platform = _platform,
                        }
                    });
                    await SendTextAsync(ws, regPayload, ct);

                    // Receive loop
                    var receiveBuffer = new byte[1024 * 64];
                    while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                    {
                        var result = await ws.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                            break;
                        }

                        var json = Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
                        _ = Task.Run(() => HandleMessageAsync(ws, json, ct), ct);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UnityMCP] Connection failed: {ex.Message}. Retrying in {retryDelay / 1000}s...");
                }
                finally
                {
                    _isConnected = false;
                }

                if (_isRunning && !ct.IsCancellationRequested)
                    await Task.Delay(retryDelay, ct).ContinueWith(_ => { }); // swallow cancel
            }

            _isRunning = false;
            _isConnected = false;
        }

        private async Task HandleMessageAsync(ClientWebSocket ws, string json, CancellationToken ct)
        {
            JObject request;
            try { request = JObject.Parse(json); }
            catch { return; }

            var msgId = request["id"]?.ToString();
            var method = request["method"]?.ToString();
            if (string.IsNullOrEmpty(method)) return;

            var @params = request["params"] as JObject ?? new JObject();

            // Dispatch to main thread via queue
            var tcs = new TaskCompletionSource<JObject>();
            _commandQueue.Enqueue(new PendingCommand { Method = method, Params = @params, Tcs = tcs });

            JObject resultObj;
            try
            {
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));
                if (completed != tcs.Task)
                    throw new TimeoutException($"Command '{method}' timed out");
                resultObj = await tcs.Task;
            }
            catch (Exception ex)
            {
                resultObj = JObject.FromObject(new ErrorResponse("InternalError", ex.Message));
            }

            var response = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = msgId,
                ["result"] = resultObj,
            };

            try
            {
                await SendTextAsync(ws, response.ToString(Formatting.None), ct);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityMCP] Failed to send response: {ex.Message}");
            }
        }

        private static async Task SendTextAsync(ClientWebSocket ws, string text, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }
    }
}

