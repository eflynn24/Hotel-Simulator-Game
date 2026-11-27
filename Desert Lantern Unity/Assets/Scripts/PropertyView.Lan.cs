using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte DestinationAction = 0;
        private const byte ReceptionAction = 1;
        private const float InputInterval = 1f / 30;
        private const float SnapshotInterval = 1f / 15;
        private const float InputTimeout = .30f;

        private sealed class Peer
        {
            public readonly MotelWalker Walker;
            public readonly Queue<Vector3> Route = new Queue<Vector3>();
            public Vector2 Direction;
            public bool Run;
            public float LastInput;
            public float NextDestination;

            public Peer(MotelWalker walker) => Walker = walker;
        }

        private LanSession lan;
        private int lanOccupied;
        private ulong guideId;
        private bool lanMenu;
        private string joinAddress = "127.0.0.1";
#if UNITY_WEBGL && !UNITY_EDITOR
        private const bool CanHostLan = false;
#else
        private const bool CanHostLan = true;
#endif
        private const string PublishedLanAddress = "192.168.4.43";
        private bool publishedLan;
        private bool browserLan = !CanHostLan;
        private bool secureBrowserLan;
        private string portInput;

        private void InitializeLanMenu()
        {
            portInput = (browserLan ? LanSession.WebSocketPort : LanSession.DefaultPort).ToString();
            if (CanHostLan || !System.Uri.TryCreate(Application.absoluteURL, System.UriKind.Absolute, out var url))
                return;

            publishedLan = url.Host == "eflynn24.github.io" &&
                url.AbsolutePath.StartsWith("/Hotel-Simulator/", System.StringComparison.Ordinal);
            if (publishedLan)
                joinAddress = PublishedLanAddress;
            else if (IPAddress.TryParse(url.Host, out var address) && address.AddressFamily == AddressFamily.InterNetwork)
                joinAddress = url.Host;
        }

        private void JoinPublishedLan()
        {
            lanMenu = true;
            joinAddress = PublishedLanAddress;
            secureBrowserLan = false;
            portInput = LanSession.WebSocketPort.ToString();
            LanSession.StartLan(false, joinAddress, portInput, webSocket: true);
        }

        private string hostAddress;
        private float nextInput, nextSnapshot;
        private readonly Dictionary<ulong, Peer> peers = new Dictionary<ulong, Peer>();
        private readonly List<ulong> departing = new List<ulong>();

        private int Occupied
        {
            get => lan == null ? occupiedRooms : lanOccupied;
            set
            {
                if (lan == null) occupiedRooms = value;
                else lanOccupied = value;
            }
        }

        private void InitializeLan()
        {
            lan = LanSession.Active;
            if (lan == null || !lan.IsHost)
                return;

            hostAddress = LanSession.FindAddress();
            lan.MovementReceived = ReceiveMovement;
            lan.ActionReceived = ReceiveAction;
        }

        private static Vector3 SlotSpawn(byte slot) =>
            MotelWorld.Spawn + new Vector3(slot * 1.45f, 0, -.35f * slot);

        private MotelWalker SpawnPeer(byte slot, Vector3 position) =>
            new MotelWalker(transform, slot == 0 ? "Caretaker" : Guests[slot], position);

        private void UpdateLan(float delta)
        {
            if (!lan.Connected || lan.State == null)
                return;

            if (!lan.IsHost)
            {
                ApplySnapshots(delta);
                if (Time.unscaledTime >= nextInput && caretaker != null)
                {
                    nextInput = Time.unscaledTime + InputInterval;
                    Vector3 input = Modal ? Vector3.zero : ReadMovement();
                    lan.State.MoveRpc(new Vector2(input.x, input.z), !Modal && ReadSprint());
                }
                return;
            }

            ReconcileHostPeers();
            foreach (var entry in peers)
            {
                if (entry.Key == lan.LocalId)
                    continue;
                Peer peer = entry.Value;
                bool fresh = Time.unscaledTime - peer.LastInput <= InputTimeout;
                Vector3 movement = fresh ? new Vector3(peer.Direction.x, 0, peer.Direction.y) : Vector3.zero;
                Drive(peer.Walker, peer.Route, movement, fresh && peer.Run, delta);
            }

            if (escorting && peers.TryGetValue(guideId, out Peer guide))
                UpdateGuest(delta, guide.Walker.Position);
            else
                UpdateGuest(delta, caretaker.Position);

            if (Time.unscaledTime >= nextSnapshot)
            {
                nextSnapshot = Time.unscaledTime + SnapshotInterval;
                PublishSnapshots();
            }
        }

        private void ReconcileHostPeers()
        {
            departing.Clear();
            foreach (var entry in peers)
                if (!lan.Slots.ContainsKey(entry.Key) || !lan.HasPeer(entry.Key))
                    departing.Add(entry.Key);

            foreach (ulong id in departing)
            {
                if (escorting && guideId == id)
                {
                    guest?.Dispose();
                    guest = null;
                    guestRoute.Clear();
                    escorting = false;
                    SpawnGuest();
                }
                MotelService service = Service;
                service.RecoverBag(id);
                Service = service;
                peers[id].Walker.Dispose();
                peers.Remove(id);
            }

            foreach (var entry in lan.Slots)
            {
                if (!lan.HasPeer(entry.Key) || peers.ContainsKey(entry.Key))
                    continue;
                bool local = entry.Key == lan.LocalId;
                MotelWalker actor = local ? caretaker : SpawnPeer(entry.Value, SlotSpawn(entry.Value));
                foreach (Peer existing in peers.Values)
                    Physics.IgnoreCollision(actor.Capsule, existing.Walker.Capsule);
                if (guest != null)
                    Physics.IgnoreCollision(actor.Capsule, guest.Capsule);
                peers.Add(entry.Key, new Peer(actor));
            }
        }

        private void PublishSnapshots()
        {
            var list = lan.State.Players;
            for (int i = list.Count - 1; i >= 0; i--)
                if (!peers.ContainsKey(list[i].Id))
                    list.RemoveAt(i);

            foreach (var entry in lan.Slots)
            {
                if (!peers.TryGetValue(entry.Key, out Peer peer))
                    continue;
                var state = new LanPlayer { Id = entry.Key, Slot = entry.Value, Pose = peer.Walker.Pose };
                int index = 0;
                while (index < list.Count && list[index].Id != state.Id)
                    index++;
                if (index == list.Count)
                    list.Add(state);
                else if (!list[index].Equals(state))
                    list[index] = state;
            }

            lan.State.Motel.Value = new LanMotel
            {
                Occupied = Occupied, Escorting = escorting, GuestPresent = guest != null,
                Guest = guest == null ? default : guest.Pose, Service = Service
            };
        }

        private void ApplySnapshots(float delta)
        {
            departing.Clear();
            foreach (ulong id in peers.Keys)
                departing.Add(id);

            foreach (LanPlayer state in lan.State.Players)
            {
                departing.Remove(state.Id);
                if (!peers.TryGetValue(state.Id, out Peer existing))
                {
                    existing = new Peer(SpawnPeer(state.Slot, state.Pose.Position));
                    peers.Add(state.Id, existing);
                    if (state.Id == lan.LocalId)
                    {
                        caretaker = existing.Walker;
                        PlaceCamera(1);
                    }
                }
                existing.Walker.ApplyReplica(state.Pose, delta);
            }
            foreach (ulong id in departing)
            {
                if (id == lan.LocalId)
                    caretaker = null;
                peers[id].Walker.Dispose();
                peers.Remove(id);
            }

            LanMotel shared = lan.State.Motel.Value;
            if (guest != null && (!shared.GuestPresent || Occupied != shared.Occupied))
            {
                guest.Dispose();
                guest = null;
            }
            Occupied = shared.Occupied;
            Service = shared.Service;
            escorting = shared.Escorting;
            if (shared.GuestPresent && Occupied < Guests.Length)
            {
                if (guest == null)
                    guest = new MotelWalker(transform, Guests[Occupied], shared.Guest.Position);
                guest.ApplyReplica(shared.Guest, delta);
            }
        }

        private void ReceiveMovement(ulong id, Vector2 direction, bool run)
        {
            if (!peers.TryGetValue(id, out Peer peer) || !Finite(direction.x) || !Finite(direction.y))
                return;
            peer.Direction = Vector2.ClampMagnitude(direction, 1);
            peer.Run = run;
            peer.LastInput = Time.unscaledTime;
        }

        private void ReceiveAction(ulong id, byte action, Vector3 position)
        {
            if (!peers.TryGetValue(id, out Peer peer))
                return;
            if (action == ReceptionAction && GuestWaiting && AtOfficePosition(peer.Walker.Position))
                StartEscort(id, peer.Walker.Position, peer.Route);
            else if (action == LuggageAction)
                TryLuggage(id, peer.Walker.Position);
            else if (action == PurchaseAction && Finite(position.x) &&
                position.x >= 0 && position.x < MotelService.Catalog.Length)
                TryPurchase((int)position.x, peer.Walker.Position);
            else if (action == DoorAction && Finite(position.x) && position.x >= 0 && position.x < Guests.Length)
                ToggleDoor((int)position.x, peer.Walker.Position);
            else if (action == DestinationAction && ValidDestination(position) &&
                Time.unscaledTime >= peer.NextDestination)
            {
                peer.NextDestination = Time.unscaledTime + .08f;
                peer.Direction = Vector2.zero;
                QueueDestination(peer.Route, peer.Walker.Position, position);
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool ValidDestination(Vector3 position) =>
            Finite(position.x) && Finite(position.y) && Finite(position.z) &&
            Mathf.Abs(position.x) <= 17.1f && position.z >= -15f && position.z <= 13.35f && Mathf.Abs(position.y) <= 1;

        private void DrawLanMenu(Rect screen, float scale)
        {
            Rect panel = DrawOverlay(screen, 411 * scale, scale);
            float left = panel.x + 20 * scale;
            float width = panel.width - 40 * scale;
            GUI.Label(new Rect(left, panel.y + 9 * scale, width, 48 * scale), "LAN co-op", title);
            bool connecting = LanSession.Active != null;
            GUI.enabled = !connecting;
            if (CanHostLan)
            {
                bool selected = GUI.Toggle(new Rect(left, panel.y + 62 * scale, width, 30 * scale),
                    browserLan, "Allow browser players (WebSocket)");
                if (selected != browserLan)
                {
                    browserLan = selected;
                    portInput = (browserLan ? LanSession.WebSocketPort : LanSession.DefaultPort).ToString();
                }
            }
            else
            {
                bool secure = GUI.Toggle(new Rect(left, panel.y + 62 * scale, width, 30 * scale),
                    secureBrowserLan, "Secure WebSocket (WSS)");
                if (secure != secureBrowserLan)
                {
                    secureBrowserLan = secure;
                    portInput = secure ? "443" : LanSession.WebSocketPort.ToString();
                }
            }

            GUI.Label(new Rect(left, panel.y + 110 * scale, width, 25 * scale), secureBrowserLan ? "Host DNS name" : "Host IPv4 address", detail);
            joinAddress = GUI.TextField(new Rect(left, panel.y + 137 * scale, width, 33 * scale), joinAddress, secureBrowserLan ? 253 : 15);
            GUI.Label(new Rect(left, panel.y + 179 * scale, width, 24 * scale), browserLan ? "TCP port" : "UDP port", detail);
            portInput = GUI.TextField(new Rect(left, panel.y + 205 * scale, width, 33 * scale), portInput, 5);
            float half = (width - 8 * scale) / 2;
            if (CanHostLan && DrawButton(new Rect(left, panel.y + 259 * scale, half, 41 * scale), "Host motel"))
                LanSession.StartLan(true, null, portInput, browserLan);
            float joinX = CanHostLan ? left + half + 8 * scale : left;
            if (DrawButton(new Rect(joinX, panel.y + 259 * scale, CanHostLan ? half : width, 41 * scale), "Join motel"))
                LanSession.StartLan(false, joinAddress, portInput, browserLan, secureBrowserLan);
            GUI.enabled = true;
            if (DrawButton(new Rect(left, panel.y + 313 * scale, width, 37 * scale), connecting ? "Cancel" : "Back"))
            {
                LanSession.Active?.Stop("Connection cancelled.");
                lanMenu = false;
            }
            GUI.Label(new Rect(left, panel.y + 359 * scale, width, 42 * scale),
                LanSession.Notice ?? (CanHostLan ? "Up to four players on one local network."
                    : "Allow local network access if your browser asks."), detail);
        }
    }
}
