using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ForeverEngine.Core.Messages;
using UnityEngine;

namespace ForeverEngine.Network
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
    }

    public class NetworkClient : MonoBehaviour
    {
        public static NetworkClient Instance { get; private set; }

        public event Action OnConnected;
        public event Action OnDisconnected;

        public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

        private Socket _socket;
        private Thread _readThread;
        private volatile bool _running;

        private readonly ConcurrentQueue<ServerMessage> _messageQueue = new ConcurrentQueue<ServerMessage>();
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        private readonly MessageDispatcher _dispatcher = new MessageDispatcher();

        // Reconnect settings
        private bool _reconnectEnabled;
        private string _lastHost;
        private int _lastPort;
        private float _reconnectDelay;
        private float _reconnectTimer;
        private const float ReconnectDelayMin = 1f;
        private const float ReconnectDelayMax = 10f;

        // Read buffer
        private const int ReadBufferSize = 65536;
        private byte[] _frameBuffer = new byte[0];

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            Disconnect();
        }

        private void Update()
        {
            // Drain main-thread action queue (connection/disconnection events)
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NetworkClient] Main-thread action error: {ex}");
                }
            }

            // Drain message queue and dispatch
            while (_messageQueue.TryDequeue(out var msg))
            {
                try
                {
                    _dispatcher.Dispatch(msg);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NetworkClient] Dispatch error for {msg.GetType().Name}: {ex}");
                }
            }

            // Handle reconnection
            if (_reconnectEnabled && State == ConnectionState.Disconnected && _lastHost != null)
            {
                _reconnectTimer -= Time.unscaledDeltaTime;
                if (_reconnectTimer <= 0f)
                {
                    Debug.Log($"[NetworkClient] Attempting reconnect to {_lastHost}:{_lastPort} (delay was {_reconnectDelay:F1}s)");
                    Connect(_lastHost, _lastPort);
                    // Exponential backoff: double the delay, cap at max
                    _reconnectDelay = _reconnectDelay * 2f;
                    if (_reconnectDelay > ReconnectDelayMax)
                        _reconnectDelay = ReconnectDelayMax;
                    _reconnectTimer = _reconnectDelay;
                }
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void Connect(string host, int port)
        {
            if (State == ConnectionState.Connecting || State == ConnectionState.Connected)
            {
                Debug.LogWarning("[NetworkClient] Already connected or connecting.");
                return;
            }

            _lastHost = host;
            _lastPort = port;
            State = ConnectionState.Connecting;

            try
            {
                var addresses = Dns.GetHostAddresses(host);
                if (addresses.Length == 0)
                {
                    Debug.LogError($"[NetworkClient] Could not resolve host: {host}");
                    State = ConnectionState.Disconnected;
                    return;
                }

                _socket = new Socket(addresses[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _socket.NoDelay = true;
                _socket.Connect(new IPEndPoint(addresses[0], port));

                _frameBuffer = new byte[0];
                _running = true;

                _readThread = new Thread(ReadLoop)
                {
                    IsBackground = true,
                    Name = "NetworkClient-ReadLoop"
                };
                _readThread.Start();

                State = ConnectionState.Connected;
                _reconnectDelay = ReconnectDelayMin;

                _mainThreadQueue.Enqueue(() => OnConnected?.Invoke());

                Debug.Log($"[NetworkClient] Connected to {host}:{port}");
            }
            catch (SocketException ex)
            {
                Debug.LogError($"[NetworkClient] Connect failed: {ex.Message}");
                CleanupSocket();
                State = ConnectionState.Disconnected;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkClient] Connect error: {ex}");
                CleanupSocket();
                State = ConnectionState.Disconnected;
            }
        }

        public void Disconnect()
        {
            if (State == ConnectionState.Disconnected)
                return;

            _running = false;
            CleanupSocket();

            if (_readThread != null && _readThread.IsAlive)
            {
                _readThread.Join(2000);
                _readThread = null;
            }

            State = ConnectionState.Disconnected;
            _mainThreadQueue.Enqueue(() => OnDisconnected?.Invoke());

            Debug.Log("[NetworkClient] Disconnected.");
        }

        public void EnableReconnect()
        {
            _reconnectEnabled = true;
            _reconnectDelay = ReconnectDelayMin;
            _reconnectTimer = _reconnectDelay;
        }

        public void DisableReconnect()
        {
            _reconnectEnabled = false;
        }

        public void Send(ClientMessage message)
        {
            if (State != ConnectionState.Connected || _socket == null)
            {
                Debug.LogWarning("[NetworkClient] Cannot send: not connected.");
                return;
            }

            try
            {
                var payload = MessageSerializer.Serialize(message);
                var frame = MessageSerializer.Frame(payload);
                _socket.Send(frame);
            }
            catch (SocketException ex)
            {
                Debug.LogError($"[NetworkClient] Send failed: {ex.Message}");
                HandleDisconnect();
            }
            catch (ObjectDisposedException)
            {
                Debug.LogError("[NetworkClient] Send failed: socket disposed.");
                HandleDisconnect();
            }
        }

        public void RegisterHandler<T>(Action<T> handler) where T : ServerMessage
        {
            _dispatcher.RegisterHandler(handler);
        }

        public void UnregisterHandler<T>() where T : ServerMessage
        {
            _dispatcher.UnregisterHandler<T>();
        }

        // ── Read loop (background thread) ──────────────────────────────────────

        private void ReadLoop()
        {
            var buffer = new byte[ReadBufferSize];

            while (_running)
            {
                try
                {
                    // Check for graceful disconnect
                    if (_socket.Poll(0, SelectMode.SelectRead) && _socket.Available == 0)
                    {
                        Debug.Log("[NetworkClient] Server closed connection.");
                        HandleDisconnect();
                        return;
                    }

                    if (_socket.Available > 0)
                    {
                        int bytesRead = _socket.Receive(buffer, 0, buffer.Length, SocketFlags.None);
                        if (bytesRead == 0)
                        {
                            HandleDisconnect();
                            return;
                        }

                        // Append to frame buffer
                        var newFrameBuffer = new byte[_frameBuffer.Length + bytesRead];
                        if (_frameBuffer.Length > 0)
                            Buffer.BlockCopy(_frameBuffer, 0, newFrameBuffer, 0, _frameBuffer.Length);
                        Buffer.BlockCopy(buffer, 0, newFrameBuffer, _frameBuffer.Length, bytesRead);
                        _frameBuffer = newFrameBuffer;

                        // Extract complete frames
                        while (MessageSerializer.TryUnframe(_frameBuffer, out var payload, out int consumed))
                        {
                            // Trim consumed bytes from frame buffer
                            var remaining = new byte[_frameBuffer.Length - consumed];
                            if (remaining.Length > 0)
                                Buffer.BlockCopy(_frameBuffer, consumed, remaining, 0, remaining.Length);
                            _frameBuffer = remaining;

                            // Deserialize and enqueue
                            try
                            {
                                var msg = MessageSerializer.DeserializeServer(payload);
                                if (msg != null)
                                {
                                    _messageQueue.Enqueue(msg);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"[NetworkClient] Deserialize error: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (SocketException ex)
                {
                    if (_running)
                    {
                        Debug.LogError($"[NetworkClient] Read error: {ex.Message}");
                        HandleDisconnect();
                    }
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        // ── Internal helpers ───────────────────────────────────────────────────

        private void HandleDisconnect()
        {
            _running = false;
            CleanupSocket();

            // Must set state before enqueuing, in case Update picks it up immediately
            State = ConnectionState.Disconnected;
            _reconnectTimer = _reconnectDelay;

            _mainThreadQueue.Enqueue(() => OnDisconnected?.Invoke());
        }

        private void CleanupSocket()
        {
            if (_socket != null)
            {
                try
                {
                    if (_socket.Connected)
                        _socket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }

                try
                {
                    _socket.Close();
                }
                catch (Exception) { }

                _socket = null;
            }
        }
    }
}
