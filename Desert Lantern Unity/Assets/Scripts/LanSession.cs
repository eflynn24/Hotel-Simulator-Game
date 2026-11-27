using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DesertLantern
{
    public sealed class LanSession : MonoBehaviour
    {
        internal const ushort DefaultPort = 7777;
        internal const ushort WebSocketPort = 7778;
        private const int BrowserConnectTimeout = 30;
        internal const int Capacity = 4;
        internal static LanSession Active { get; private set; }
        internal static string Notice { get; private set; }
        internal readonly Dictionary<ulong, byte> Slots = new Dictionary<ulong, byte>();
        internal Action<ulong, Vector2, bool> MovementReceived;
        internal Action<ulong, byte, Vector3> ActionReceived;
        internal LanWorldState State { get; private set; }
        private NetworkManager manager;
        private UnityTransport transport;
        private bool hosting;
        private bool entered;
        private bool stopping;
        private bool shutdownRequested;
        private string exitNotice;
        private float deadline;
        internal bool IsHost => hosting;
        internal ulong LocalId => manager.LocalClientId;
        internal bool Connected => entered && !stopping;
        internal ushort Port { get; private set; }
        internal bool UsesWebSockets => transport.UseWebSockets;

#if !UNITY_WEBGL || UNITY_EDITOR
        private static bool checkedBrowserHostArgument;

        internal static void StartBrowserHostFromCommandLine()
        {
            if (checkedBrowserHostArgument)
                return;
            checkedBrowserHostArgument = true;
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-motelBrowserHost");
            if (option < 0)
                return;
            string port = option + 1 < args.Length && !args[option + 1].StartsWith("-")
                ? args[option + 1]
                : WebSocketPort.ToString();
            if (Application.isBatchMode)
                Application.targetFrameRate = 60;
            if (StartLan(true, null, port, webSocket: true))
                Debug.Log($"Browser LAN host listening on TCP port {port}.");
            else
            {
                Debug.LogError(Notice);
                if (Application.isBatchMode)
                    Application.Quit(1);
            }
        }
#endif
        internal bool HasPeer(ulong id) => manager.ConnectedClients.ContainsKey(id);

        internal static bool StartLan(bool host, string address, string portText, bool webSocket = false)
        {
            if (Active != null)
                return false;
            if (!ushort.TryParse(portText, out ushort port) || port == 0)
            {
                Notice = "Enter a port between 1 and 65535.";
                return false;
            }
            string endpoint = address?.Trim();
            if (!host && (!IPAddress.TryParse(endpoint, out IPAddress ip) ||
                ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.Any.Equals(ip)))
            {
                Notice = "Enter the host's IPv4 address.";
                return false;
            }

            var prefab = Resources.Load<GameObject>("LanWorldState");
            if (prefab == null)
            {
                Notice = "LAN setup is missing the motel state prefab.";
                return false;
            }

            var session = new GameObject("LAN Session").AddComponent<LanSession>();
            Active = session;
            DontDestroyOnLoad(session.gameObject);
            Application.runInBackground = true;
            session.hosting = host;
            session.Port = port;
            session.transport = session.gameObject.AddComponent<UnityTransport>();
            session.transport.UseWebSockets = webSocket;
            // A cold browser can take several seconds to load the motel after joining.
            session.transport.DisconnectTimeoutMS = webSocket ? BrowserConnectTimeout * 1000 : 8000;
            session.manager = session.gameObject.AddComponent<NetworkManager>();
            session.manager.NetworkConfig = new NetworkConfig
            {
                ProtocolVersion = 3,
                EnableSceneManagement = false,
                ConnectionApproval = true,
                NetworkTransport = session.transport,
                ClientConnectionBufferTimeout = webSocket ? BrowserConnectTimeout : 10,
                TickRate = 30
            };
            session.manager.AddNetworkPrefab(prefab);
            session.manager.ConnectionApprovalCallback = session.Approve;
            session.manager.OnClientConnectedCallback += session.OnConnected;
            session.manager.OnClientDisconnectCallback += session.OnDisconnected;
            session.manager.OnTransportFailure += session.OnTransportFailure;
            session.transport.SetConnectionData(host ? "127.0.0.1" : endpoint, port, "0.0.0.0");
            Notice = host ? "Starting LAN host…" : "Connecting…";
            session.deadline = Time.unscaledTime + (webSocket ? BrowserConnectTimeout : 12f);

            if (!(host ? session.manager.StartHost() : session.manager.StartClient()))
            {
                session.Stop(host ? $"Could not host on {(webSocket ? "TCP" : "UDP")} port {port}." : "Could not start the connection.");
                return false;
            }

            if (host && !session.stopping)
            {
                session.Slots[NetworkManager.ServerClientId] = 0;
                var state = Instantiate(prefab).GetComponent<LanWorldState>();
                state.NetworkObject.Spawn(destroyWithScene: false);
                session.Enter();
            }
            return !session.stopping;
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            ulong id = request.ClientNetworkId;
            byte first = id == NetworkManager.ServerClientId ? (byte)0 : (byte)1;
            for (byte slot = first; slot < Capacity; slot++)
            {
                if (Slots.ContainsValue(slot))
                    continue;
                Slots.Add(id, slot);
                response.Approved = true;
                response.CreatePlayerObject = false;
                return;
            }
            response.Reason = "This motel is full.";
        }

        private void OnConnected(ulong id)
        {
            if (!hosting && id == LocalId)
                Enter();
        }

        private void Enter()
        {
            if (entered || stopping)
                return;
            entered = true;
            Notice = null;
            SceneManager.LoadScene("MotelExterior");
        }

        internal void Attach(LanWorldState state) => State = state;

        private void OnDisconnected(ulong id)
        {
            Slots.Remove(id);
            if (id == LocalId)
                Stop(string.IsNullOrEmpty(manager.DisconnectReason) ? "Disconnected from the LAN host." : manager.DisconnectReason);
        }

        private void OnTransportFailure() =>
            Stop(hosting ? $"Could not host on {(UsesWebSockets ? "TCP" : "UDP")} port {Port}." : "LAN connection failed.");

        private void Update()
        {
            if (!entered && !stopping && Time.unscaledTime >= deadline)
                Stop("No response from the LAN host.");
            if (!stopping)
                return;

            if (!shutdownRequested)
            {
                shutdownRequested = true;
                manager.OnClientConnectedCallback -= OnConnected;
                manager.OnClientDisconnectCallback -= OnDisconnected;
                manager.OnTransportFailure -= OnTransportFailure;
                manager.Shutdown();
                return;
            }

            if (manager.ShutdownInProgress)
                return;

            Active = null;
            Notice = exitNotice;
            if (entered)
                SceneManager.LoadScene("CountyMap");
            Destroy(gameObject);
        }

        internal void Stop(string message)
        {
            if (stopping)
                return;
            stopping = true;
            exitNotice = message;
        }

        internal static string FindAddress()
        {
            try
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up ||
                        adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                        continue;
                    var properties = adapter.GetIPProperties();
                    if (properties.GatewayAddresses.Count == 0)
                        continue;
                    foreach (var address in properties.UnicastAddresses)
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                            return address.Address.ToString();
                }
            }
            catch (NetworkInformationException) { }
            return "127.0.0.1";
        }
    }
}
