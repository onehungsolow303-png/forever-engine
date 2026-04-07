using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;

namespace ForeverEngine.Bridges
{
    /// <summary>
    /// HttpListener-backed read-only HTTP server exposing live engine state
    /// to Director Hub's game_state_tool. Serves on 127.0.0.1:7803 by default
    /// (loopback only — no admin permissions required on Windows).
    ///
    /// Single endpoint:
    ///   GET /state  ->  {ok, session_id, player, scene, ...}
    ///
    /// The server runs on a background thread and is bounded — it serves
    /// one request at a time and exits cleanly on OnDestroy. Heavy state
    /// reads are deferred to the next Unity main-thread frame via a queue
    /// so the server thread never touches Unity APIs directly.
    /// </summary>
    public class GameStateServer : UnityEngine.MonoBehaviour
    {
        public string Prefix = "http://127.0.0.1:7803/";
        public bool AutoStart = true;
        public bool IsRunning => _listener != null && _listener.IsListening;

        private HttpListener _listener;
        private Thread _thread;
        private volatile bool _stopRequested;

        // Snapshot the main thread can update from Update(); the server
        // thread reads this without touching Unity APIs.
        private string _cachedStateJson = "{\"ok\":true,\"note\":\"no state yet\"}";
        private readonly object _cacheLock = new();

        private void Awake()
        {
            if (AutoStart) StartServer();
        }

        private void OnDestroy()
        {
            StopServer();
        }

        private void Update()
        {
            // Refresh the cached snapshot from the main thread so the
            // server thread can serve it without ever touching Unity APIs.
            var snapshot = BuildSnapshot();
            string json = JsonConvert.SerializeObject(snapshot);
            lock (_cacheLock)
            {
                _cachedStateJson = json;
            }
        }

        public void StartServer()
        {
            if (_listener != null) return;
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(Prefix);
                _listener.Start();
                _stopRequested = false;
                _thread = new Thread(ServeLoop) { IsBackground = true, Name = "GameStateServer" };
                _thread.Start();
                Debug.Log($"[GameStateServer] listening on {Prefix}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameStateServer] failed to start: {e.Message}");
                _listener = null;
            }
        }

        public void StopServer()
        {
            _stopRequested = true;
            try { _listener?.Stop(); } catch { /* swallow */ }
            try { _listener?.Close(); } catch { /* swallow */ }
            _listener = null;
            // Don't Join the thread on the main thread — it's a background
            // thread and the process will tear it down on shutdown.
        }

        private void ServeLoop()
        {
            while (!_stopRequested && _listener != null && _listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (Exception)
                {
                    // Listener was closed or errored; exit cleanly.
                    return;
                }

                try
                {
                    HandleRequest(context);
                }
                catch (Exception e)
                {
                    try
                    {
                        WriteJson(context.Response, 500, $"{{\"ok\":false,\"error\":\"{e.Message.Replace("\"", "'")}\"}}");
                    }
                    catch { /* swallow */ }
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            string path = context.Request.Url?.AbsolutePath ?? "";

            if (path == "/state" && context.Request.HttpMethod == "GET")
            {
                string body;
                lock (_cacheLock)
                {
                    body = _cachedStateJson;
                }
                WriteJson(context.Response, 200, body);
                return;
            }

            if (path == "/health" && context.Request.HttpMethod == "GET")
            {
                WriteJson(context.Response, 200, "{\"ok\":true,\"service\":\"forever_engine_state\"}");
                return;
            }

            WriteJson(context.Response, 404, "{\"ok\":false,\"error\":\"not found\"}");
        }

        private static void WriteJson(HttpListenerResponse response, int status, string json)
        {
            response.StatusCode = status;
            response.ContentType = "application/json";
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        private Dictionary<string, object> BuildSnapshot()
        {
            var snapshot = new Dictionary<string, object>
            {
                ["ok"] = true,
                ["service"] = "forever_engine_state",
            };

            // Pull whatever GameManager / Player state is available. Wrapped
            // in try blocks so a missing field never crashes the snapshot
            // builder; we'd rather return partial state than no state.
            try
            {
                var gm = ForeverEngine.Demo.GameManager.Instance;
                if (gm != null)
                {
                    snapshot["session_id"] = gm.SessionId ?? "";
                    snapshot["seed"] = gm.CurrentSeed;
                    if (gm.Player != null)
                    {
                        snapshot["player"] = new Dictionary<string, object>
                        {
                            ["hp"] = gm.Player.HP,
                            ["max_hp"] = gm.Player.MaxHP,
                            ["hex_q"] = gm.Player.HexQ,
                            ["hex_r"] = gm.Player.HexR,
                            ["gold"] = gm.Player.Gold,
                            ["last_safe_location"] = gm.Player.LastSafeLocation ?? "",
                        };
                    }
                    if (!string.IsNullOrEmpty(gm.PendingEncounterId))
                        snapshot["pending_encounter"] = gm.PendingEncounterId;
                    if (!string.IsNullOrEmpty(gm.PendingLocationId))
                        snapshot["pending_location"] = gm.PendingLocationId;
                }
                else
                {
                    snapshot["note"] = "no GameManager instance yet";
                }
            }
            catch (Exception e)
            {
                snapshot["snapshot_error"] = e.Message;
            }

            return snapshot;
        }
    }
}
