using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DesertLantern
{
    public sealed partial class PropertyView : MonoBehaviour
    {
        private static readonly Color Accent = new Color32(255, 209, 134, 255);
        private static readonly Color Ink = new Color32(36, 24, 16, 255);
        private static readonly string[] Guests = { "Guest-Mara", "Guest-Eli", "Guest-June", "Guest-Vic" };
        private const string BrightnessKey = "display.brightness";
        private const string LargeTextKey = "display.largeText";
        private const int NoRoom = -1;
        private const float Reach = 1.38f;
        private static int occupiedRooms;

        [SerializeField] private bool isMap;

        private MotelWorld world;
        private Camera view;
        private MotelWalker caretaker;
        private MotelWalker guest;
        private readonly Queue<Vector3> walkRoute = new Queue<Vector3>();
        private readonly Queue<Vector3> guestRoute = new Queue<Vector3>();
        private Vector3 lastBreadcrumb;
        private Vector3 previousMouse;
        private float yaw;
        private float pitch = 19;
        private float nextGuestDelay;
        private float clockFraction;
        private int guestArrivalMinute;
        private int guestDeskMinute;
        private bool escorting;
        private bool showSettings;
        private int inspectedRoom = NoRoom;
        private float brightness;
        private bool largeText;
        private GUIStyle title, detail, label, button;

        private bool AtOffice => AtOfficePosition(caretaker.Position);
        private static bool AtOfficePosition(Vector3 position) =>
            MotelWalker.Distance(position, MotelWorld.OfficeApproach) < 1.6f;
        private string CurrentGuestModel => Service.ReservationCancelled(Occupied)
            ? Guests[Occupied == 1 ? 3 : 0]
            : Guests[Occupied];
        private bool GuestWaiting => guest != null && !escorting;
        private bool Modal => showSettings || lanMenu || computerOpen || inspectedRoom != NoRoom;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            occupiedRooms = 0;
            soloService = MotelService.New();
        }

        private void Awake()
        {
            brightness = Mathf.Clamp(PlayerPrefs.GetFloat(BrightnessKey, 1f), .6f, 1f);
            largeText = PlayerPrefs.GetInt(LargeTextKey, 0) != 0;
            if (isMap)
            {
                InitializeLanMenu();
                lanMenu = !string.IsNullOrEmpty(LanSession.Notice);
            }
            else
                InitializeLan();
            world = new MotelWorld(transform, isMap);
            view = new GameObject(isMap ? "County Camera" : "Follow Camera").AddComponent<Camera>();
            view.clearFlags = CameraClearFlags.SolidColor;
            if (!isMap && (lan == null || !lan.Dedicated))
                view.gameObject.AddComponent<AudioListener>();
            view.backgroundColor = new Color(.66f, .71f, .72f);
            view.fieldOfView = 62;
            view.nearClipPlane = .09f;
            view.farClipPlane = 360;
            if (isMap)
            {
                var viewpoint = new Vector3(38, 82, -105);
                view.transform.SetPositionAndRotation(viewpoint,
                    Quaternion.LookRotation(new Vector3(0, 1, -2) - viewpoint));
                return;
            }

            if (lan == null || lan.IsHost)
            {
                if (lan == null || !lan.Dedicated)
                {
                    caretaker = new MotelWalker(transform, "Caretaker", MotelWorld.Spawn);
                    PlaceCamera(1);
                }
                SpawnGuest();
            }
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private void Start()
        {
            if (isMap)
                LanSession.StartBrowserHostFromCommandLine();
        }
#endif

        private void Update()
        {
            if (isMap)
                return;

            float delta = Time.deltaTime;
            Vector3 mouse = Input.mousePosition;
            if (!Modal && caretaker != null)
            {
                if (Input.GetMouseButton(1) && !Input.GetMouseButtonDown(1))
                {
                    Vector3 offset = mouse - previousMouse;
                    yaw += offset.x * .23f;
                    pitch = Mathf.Clamp(pitch - offset.y * .18f, 12, 61);
                }
                if (lan == null || lan.IsHost)
                    UpdateWalk(delta);
                if (lan == null)
                    UpdateGuest(delta, caretaker.Position);
                if (Input.GetKeyDown(KeyCode.E))
                    Interact();
                if (Input.GetKeyDown(KeyCode.I) && NearRoom() is int room && room != NoRoom)
                {
                    walkRoute.Clear();
                    inspectedRoom = room;
                    SetActorPause(true);
                }
            }
            else if (lan != null && lan.IsHost && caretaker != null)
            {
                caretaker.Move(Vector3.zero, delta);
            }
            if (lan == null || (lan.IsHost && (!lan.Dedicated || peers.Count > 0)))
                AdvanceMotelClock(delta);
            if (lan != null)
                UpdateLan(delta);
            else
                UpdateServiceActors(delta);
            previousMouse = mouse;
        }

        private void LateUpdate()
        {
            if (isMap)
                return;
            if (caretaker != null || (lan != null && lan.IsHost))
                UpdateServicePresentation(Time.deltaTime);
            world.ShowClock(Service.ClockMinute, view);
            if (caretaker != null)
                PlaceCamera(1 - Mathf.Exp(-11f * Time.deltaTime));
        }

        private void PlaceCamera(float blend)
        {
            Vector3 focus = caretaker.Position + Vector3.up * 1.42f;
            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0);
            Vector3 direction = orbit * Vector3.back;
            float distance = 5f;
            if (Physics.SphereCast(focus, .20f, direction, out RaycastHit hit, distance, 1 << MotelWorld.SolidLayer))
                distance = Mathf.Max(.42f, hit.distance - .12f);
            Transform cameraTransform = view.transform;
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, focus + direction * distance, blend);
            cameraTransform.rotation = orbit;
        }

        private Vector3 ReadMovement()
        {
            var keys = new Vector2(Axis(KeyCode.D, KeyCode.RightArrow, KeyCode.A, KeyCode.LeftArrow),
                Axis(KeyCode.W, KeyCode.UpArrow, KeyCode.S, KeyCode.DownArrow));
            keys = Vector2.ClampMagnitude(keys, 1);
            return Quaternion.Euler(0, yaw, 0) * new Vector3(keys.x, 0, keys.y);
        }

        private static bool ReadSprint() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private void UpdateWalk(float delta) =>
            Drive(caretaker, walkRoute, ReadMovement(), ReadSprint(), delta);

        private static void Drive(MotelWalker actor, Queue<Vector3> route, Vector3 input, bool sprint, float delta)
        {
            float speed = sprint ? MotelWalker.RunSpeed : MotelWalker.WalkSpeed;
            Vector3 movement = input * speed;
            if (input != Vector3.zero)
                route.Clear();
            else if (route.TryPeek(out Vector3 target))
            {
                if (MotelWalker.Distance(actor.Position, target) < .16f)
                    route.Dequeue();
                if (route.TryPeek(out target))
                    movement = MotelWalker.Toward(actor.Position, target, speed);
            }
            actor.Move(movement, delta);
        }

        private static int Axis(KeyCode positive, KeyCode alternatePositive, KeyCode negative, KeyCode alternateNegative)
        {
            int value = Input.GetKey(positive) || Input.GetKey(alternatePositive) ? 1 : 0;
            return value - (Input.GetKey(negative) || Input.GetKey(alternateNegative) ? 1 : 0);
        }

        private void SpawnGuest()
        {
            if (Service.Bag != LuggageStatus.None)
                return;
            int arrival = Service.NextArrival();
            if (arrival == NoRoom)
                return;
            Occupied = arrival;
            guestArrivalMinute = Service.HasReservation(arrival) ? Service.BookingStart(arrival) : Service.ClockMinute;
            guest = new MotelWalker(transform, CurrentGuestModel, MotelWorld.GuestSpawn);
            if (caretaker != null)
                Physics.IgnoreCollision(caretaker.Capsule, guest.Capsule);
            foreach (Peer peer in peers.Values)
                Physics.IgnoreCollision(peer.Walker.Capsule, guest.Capsule);
            IgnoreActorCollision(guest, bellhop);
            IgnoreActorCollision(guest, serviceVisitor);
        }

        private void StartEscort(ulong id, Vector3 position, Queue<Vector3> route)
        {
            guideId = id;
            guestDeskMinute = Service.ClockMinute;
            route.Clear();
            guestRoute.Clear();
            lastBreadcrumb = position;
            guestRoute.Enqueue(new Vector3(MotelWorld.OfficeApproach.x, 0, 4.55f));
            guestRoute.Enqueue(lastBreadcrumb);
            escorting = true;
        }

        private void UpdateGuest(float delta, Vector3 current)
        {
            if (nextGuestDelay > 0)
            {
                nextGuestDelay -= delta;
            }
            if (guest == null && nextGuestDelay <= 0)
                SpawnGuest();
            if (guest == null)
                return;

            if (!escorting)
            {
                guest.Move(Vector3.zero, delta);
                return;
            }

            Vector3 assigned = MotelWorld.RoomApproach(Occupied);
            bool atRoom = MotelWalker.Distance(current, assigned) < Reach;
            if (MotelWalker.Distance(lastBreadcrumb, current) > .52f)
            {
                lastBreadcrumb = current;
                guestRoute.Enqueue(current);
            }

            if (guestRoute.TryPeek(out Vector3 head) && MotelWalker.Distance(guest.Position, head) < .28f)
                guestRoute.Dequeue();

            Vector3 doorstep = assigned + new Vector3(.46f, 0, .21f);
            Vector3 desired = Vector3.zero;
            if (guestRoute.TryPeek(out head) && (guestRoute.Count > 2 || atRoom))
                desired = MotelWalker.Toward(guest.Position, head, 4.4f);
            else if (atRoom)
                desired = MotelWalker.Toward(guest.Position, doorstep, 2.5f);

            guest.Move(desired, delta);
            if (atRoom && guestRoute.Count == 0 && MotelWalker.Distance(guest.Position, doorstep) < .32f)
            {
                guest.Dispose();
                guest = null;
                escorting = false;
                MotelService service = Service;
                if (service.CheckIn(Occupied, guestArrivalMinute, guestDeskMinute))
                {
                    Service = service;
                }
            }
        }

        private int NearRoom()
        {
            for (int i = 0; i < Guests.Length; i++)
                if (MotelWalker.Distance(caretaker.Position, MotelWorld.RoomDoor(i)) < 1.5f)
                    return i;
            return NoRoom;
        }

        private void Interact()
        {
            if (RequestSupplyDelivery() || RequestLuggage())
                return;
            if (Service.WaitingRequest(true) != NoRoom && AtPhone(caretaker.Position))
            {
                RequestGuestAction(AnswerPhoneAction);
                return;
            }
            if (GuestToServe(caretaker.Position) != NoRoom)
            {
                RequestGuestAction(ServeGuestAction);
                return;
            }
            if (VisitorAtDesk && MotelWalker.Distance(caretaker.Position, ServiceQueue) < 1.30f &&
                (Service.KnownRequests & (1 << visitorRoom)) == 0)
            {
                RequestReception();
                return;
            }
            if (AtComputer(caretaker.Position))
            {
                SetComputerVisible(true);
                return;
            }

            if (AtOffice && (GuestWaiting || Service.CheckoutWaiting() != NoRoom))
            {
                RequestReception();
                return;
            }

            int room = NearRoom();
            if (!escorting && room != NoRoom)
            {
                RequestDoor(room);
            }
        }

        private void RequestReception()
        {
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(ReceptionAction, Vector3.zero);
            else
                HandleReception(lan?.LocalId ?? 0, caretaker.Position, walkRoute);
        }

        private void SetActorPause(bool paused)
        {
            if (lan == null)
            {
                caretaker?.SetPaused(paused);
                guest?.SetPaused(paused);
            }
        }

        private void SetDestination(Vector3 destination)
        {
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(DestinationAction, destination);
            else
                QueueDestination(walkRoute, caretaker.Position, destination);
        }

        private static void QueueDestination(Queue<Vector3> route, Vector3 origin, Vector3 destination)
        {
            route.Clear();
            bool inside = InsideOffice(origin);
            bool destinationInside = InsideOffice(destination);
            int fromRoom = MotelWorld.InteriorRoom(origin);
            int toRoom = MotelWorld.InteriorRoom(destination);
            if (fromRoom != NoRoom && fromRoom != toRoom)
            {
                route.Enqueue(MotelWorld.RoomDoor(fromRoom) + Vector3.forward * .67f);
                route.Enqueue(MotelWorld.RoomApproach(fromRoom));
            }
            Vector3 threshold = MotelWorld.OfficeApproach;
            if (inside && !destinationInside)
            {
                route.Enqueue(new Vector3(threshold.x, 0, 4.55f));
                route.Enqueue(threshold);
            }

            Vector3 start = inside && !destinationInside ? threshold :
                fromRoom != NoRoom && fromRoom != toRoom ? MotelWorld.RoomApproach(fromRoom) : origin;
            Vector3 end = !inside && destinationInside ? threshold :
                toRoom != NoRoom && fromRoom != toRoom ? MotelWorld.RoomApproach(toRoom) : destination;
            bool startWestOfOffice = start.x < -12.3f && start.z > 3.65f;
            bool endWestOfOffice = end.x < -12.3f && end.z > 3.65f;
            bool fromStorageSide = startWestOfOffice && end.x >= -12.3f;
            bool towardStorageSide = endWestOfOffice && start.x >= -12.3f;
            if (fromStorageSide)
            {
                start = new Vector3(-13.10f, 0, 3.45f);
                route.Enqueue(start);
            }

            bool leftToRooms = start.x < -7.5f && end.x > -7.05f && end.z > 3.65f;
            bool roomsToLeft = end.x < -7.5f && start.x > -7.05f && start.z > 3.65f;
            if (start.z < end.z)
                MotelWorld.RouteAcrossParking(route, start, end);

            if (leftToRooms || roomsToLeft)
                route.Enqueue(new Vector3(-6.70f, 0, 3.45f));

            if (start.z >= end.z)
                MotelWorld.RouteAcrossParking(route, start, end);

            if (towardStorageSide)
                route.Enqueue(new Vector3(-13.10f, 0, 3.45f));

            if (!inside && destinationInside)
            {
                route.Enqueue(threshold);
                route.Enqueue(new Vector3(threshold.x, 0, 4.55f));
            }
            if (toRoom != NoRoom && fromRoom != toRoom)
            {
                route.Enqueue(MotelWorld.RoomApproach(toRoom));
                route.Enqueue(MotelWorld.RoomDoor(toRoom) + Vector3.forward * .67f);
            }
            destination.x = Mathf.Clamp(destination.x, -16.2f, 16.2f);
            destination.z = Mathf.Clamp(destination.z, -13.2f, 13.15f);
            route.Enqueue(destination);
        }

        private static bool InsideOffice(Vector3 p) => p.x > -11.95f && p.x < -7.44f && p.z > 4.08f && p.z < 8.58f;

        private void HandleWorldClick()
        {
            Event current = Event.current;
            if (!GUI.enabled || current.type != EventType.MouseDown || current.button != 0)
                return;
            Vector2 p = current.mousePosition;
            var ray = view.ScreenPointToRay(new Vector3(p.x, Screen.height - p.y, 0));
            if (!Physics.Raycast(ray, out RaycastHit hit, 150,
                (1 << MotelWorld.GroundLayer) | (1 << MotelWorld.SolidLayer)))
                return;

            if (hit.collider.transform.IsChildOf(world.ReceptionComputer))
            {
                if (AtComputer(caretaker.Position))
                    SetComputerVisible(true);
                else
                    SetDestination(MotelWorld.ComputerApproach);
            }
            else if (hit.collider.gameObject.layer == MotelWorld.GroundLayer)
            {
                SetDestination(hit.point);
            }
            else if (hit.point.x > -12.1f && hit.point.x < -7.2f && hit.point.z > 3.85f)
            {
                if (AtOffice && (GuestWaiting || Service.CheckoutWaiting() != NoRoom))
                    RequestReception();
                else
                    SetDestination(MotelWorld.OfficeApproach);
            }
            else
            {
                for (int room = 0; room < Guests.Length; room++)
                {
                    if (Mathf.Abs(hit.point.x - MotelWorld.RoomX[room]) > .73f || Mathf.Abs(hit.point.z - 6.38f) > .48f)
                        continue;
                    SelectDoor(room);
                    break;
                }
            }
            current.Use();
        }

        private void SelectDoor(int room)
        {
            if (!escorting && NearRoom() == room)
                RequestDoor(room);
            else
                SetDestination(MotelWorld.RoomApproach(room));
        }

        private void OnGUI()
        {
            float width = Screen.width;
            float height = Screen.height;
            float scale = Mathf.Clamp(Mathf.Min(width / 800f, height / 540f), .6f, 2);
            var screen = new Rect(0, 0, width, height);
            PrepareStyles(scale);
            Fill(screen, new Color(0, 0, 0, 1 - brightness));

            Event input = Event.current;
            if (input.type == EventType.KeyDown && input.keyCode == KeyCode.Escape)
            {
                if (computerOpen)
                    SetComputerVisible(false);
                else if (inspectedRoom != NoRoom)
                {
                    inspectedRoom = NoRoom;
                    SetActorPause(false);
                }
                else if (lanMenu)
                {
                    LanSession.Active?.Stop("Connection cancelled.");
                    lanMenu = false;
                }
                else
                    SetSettingsVisible(!showSettings);
                input.Use();
            }

            float margin = 24 * scale;
            GUI.enabled = !Modal;
            if (DrawButton(new Rect(width - margin - 96 * scale, margin, 96 * scale, 44 * scale), "Menu"))
                SetSettingsVisible(true);

            if (isMap)
                DrawMap(screen, margin, scale);
            else
                DrawExterior(screen, margin, scale);

            GUI.enabled = true;
            if (lanMenu)
                DrawLanMenu(screen, scale);
            else if (computerOpen)
                DrawReceptionComputer(screen, scale);
            else if (showSettings)
                DrawSettings(screen, scale);
            else if (inspectedRoom != NoRoom)
                DrawRoomDetails(screen, scale);
        }

        private void DrawMap(Rect screen, float margin, float scale)
        {
            Rect header = new Rect(margin, margin + 51 * scale, Mathf.Min(383 * scale, screen.width - 2 * margin), 96 * scale);
            Fill(header, Ink);
            GUI.Label(new Rect(header.x + 12 * scale, header.y + 5 * scale, header.width, 54 * scale), "County Map", title);
            GUI.Label(new Rect(header.x + 12 * scale, header.y + 58 * scale, header.width, 28 * scale), "Select the motel to visit the property.", detail);

            if (quickJoinLan)
            {
                if (DrawButton(new Rect(margin, margin, 121 * scale, 44 * scale), "Join LAN"))
                    JoinConfiguredLan();
                if (DrawButton(new Rect(margin + 129 * scale, margin, 188 * scale, 44 * scale), "LAN settings"))
                    lanMenu = true;
            }
            else if (DrawButton(new Rect(margin, margin, 121 * scale, 44 * scale), "LAN co-op"))
            {
                lanMenu = true;
            }
            if (DrawWorldButton(new Vector3(1.6f, 6, -1), "Desert Lantern Motel", 226 * scale, scale))
                SceneManager.LoadScene("MotelExterior");
        }

        private void DrawExterior(Rect screen, float margin, float scale)
        {
            if (DrawButton(new Rect(margin, margin, 148 * scale, 44 * scale), lan == null ? "Back to map" : "Leave LAN"))
            {
                if (lan == null) SceneManager.LoadScene("CountyMap");
                else lan.Stop("Left the LAN motel.");
            }
            if (lan != null)
            {
                string status = lan.IsHost
                    ? $"{(lan.UsesWebSockets ? "Browser host" : "LAN host")}  ·  {hostAddress}:{lan.Port}"
                    : "LAN guest";
                int players = lan.State == null ? 0 : lan.State.Players.Count;
                var connectionText = new GUIContent($"{status}  ·  {players}/{LanSession.Capacity} players");
                float padding = 8 * scale;
                float connectionWidth = Mathf.Ceil(label.CalcSize(connectionText).x) + 3 * padding;
                Rect connection = new Rect(screen.xMax - margin - connectionWidth, margin + 50 * scale,
                    connectionWidth, 29 * scale);
                Fill(connection, Ink);
                GUI.Label(new Rect(connection.x + padding, connection.y, connection.width - 2 * padding,
                    connection.height), connectionText, label);
            }
            if (caretaker == null)
                return;

            if ((GuestWaiting || Service.CheckoutWaiting() != NoRoom) && DrawWorldButton(MotelWorld.OfficeSign, "Front Office", 116 * scale, scale))
            {
                if (AtOffice)
                    RequestReception();
                else
                    SetDestination(MotelWorld.OfficeApproach);
            }
            if (escorting && DrawWorldButton(MotelWorld.RoomSign(Occupied), $"Room {Occupied + 1}", 87 * scale, scale))
                SelectDoor(Occupied);

            DrawLuggageMarker(scale);
            DrawSupplyDeliveryMarker(scale);
            DrawGuestServiceMarkers(scale);
            if (InsideOffice(caretaker.Position) &&
                DrawWorldButton(MotelWorld.ComputerScreen, "Reception computer", 154 * scale, scale))
            {
                if (AtComputer(caretaker.Position))
                    SetComputerVisible(true);
                else
                    SetDestination(MotelWorld.ComputerApproach);
            }

            int room = NearRoom();
            string objective = GuestServiceObjective() ?? (AtComputer(caretaker.Position) ? "E  ·  Open the reception computer" : ServiceObjective()) ?? SupplyObjective() ?? (escorting
                ? (room == Occupied ? "Wait at the door for your guest" : $"Escort your guest to Room {Occupied + 1}")
                : AtOffice && Service.CheckoutWaiting() != NoRoom ? "E  ·  Check out the departing guest"
                : AtOffice && GuestWaiting ? (Service.HasReservation(Occupied)
                    ? "E  ·  Check in the online reservation"
                    : "E or click Front Office to check in your guest")
                : room != NoRoom ? $"E  ·  Open or close Room {room + 1}"
                : GuestWaiting ? "Meet your next guest at the Front Office" : "Waiting for the next arrival");
            float width = Mathf.Min(610 * scale, screen.width - 2 * margin);
            Rect hud = new Rect(margin, screen.height - margin - 61 * scale, width, 61 * scale);
            Fill(hud, Ink);
            GUI.Label(new Rect(hud.x + 10 * scale, hud.y + 4 * scale, hud.width - 20 * scale, 27 * scale), objective, detail);
            GUI.Label(new Rect(hud.x + 10 * scale, hud.y + 31 * scale, hud.width - 20 * scale, 25 * scale),
                $"{MotelService.DisplayTime(Service.ClockMinute)}   ·   Cash ${Service.Cash}   ·   {Service.ActiveRoomCount}/{Guests.Length} occupied", label);
            if (DrawButton(new Rect(hud.xMax - 100 * scale, hud.y + 32 * scale, 93 * scale, 24 * scale),
                $"Time x{Mathf.Max(1, (int)Service.ClockSpeed)}"))
                CycleClockSpeed();
            DrawServiceAction(hud, scale);
            if (!hud.Contains(Event.current.mousePosition))
                HandleWorldClick();
        }

        private bool DrawWorldButton(Vector3 position, string text, float width, float scale)
        {
            Vector3 projected = view.WorldToScreenPoint(position);
            if (projected.z <= 0)
                return false;
            float y = Screen.height - projected.y;
            if (projected.x < width / 2 || projected.x > Screen.width - width / 2 ||
                y < 15 * scale || y > Screen.height - 45 * scale)
                return false;
            return DrawButton(new Rect(projected.x - width / 2, y - 16 * scale, width, 32 * scale), text);
        }

        private void OnDestroy()
        {
            if (lan != null)
            {
                lan.MovementReceived = null;
                lan.ActionReceived = null;
            }
            foreach (Peer peer in peers.Values)
            {
                if (peer.Walker != caretaker)
                    peer.Walker.Dispose();
            }
            caretaker?.Dispose();
            bellhop?.Dispose();
            serviceVisitor?.Dispose();
            foreach (MotelWalker guestActor in backgroundGuests)
                guestActor?.Dispose();
            world?.DisposeDeskPhone();
            guest?.Dispose();
            world?.Dispose();
            if (view != null)
                Destroy(view.gameObject);
        }

        private void SetSettingsVisible(bool visible)
        {
            showSettings = visible;
            SetActorPause(visible);
            if (!visible)
                PlayerPrefs.Save();
        }

        private Rect DrawOverlay(Rect screen, float height, float scale)
        {
            Fill(screen, new Color(0, 0, 0, 0.55f));
            float width = Mathf.Min(410 * scale, screen.width - 32 * scale);
            Rect panel = new Rect((screen.width - width) / 2, (screen.height - height) / 2,
                width, height);
            Fill(panel, Ink);
            Fill(new Rect(panel.x, panel.y, width, 2 * scale), Accent);
            return panel;
        }

        private void DrawSettings(Rect screen, float scale)
        {
            Rect panel = DrawOverlay(screen, 248 * scale, scale);
            float left = panel.x + 20 * scale;
            float width = panel.width - 40 * scale;
            GUI.Label(new Rect(left, panel.y + 12 * scale, width, 50 * scale), "Settings", title);
            GUI.Label(new Rect(left, panel.y + 69 * scale, width, 28 * scale),
                $"Scene brightness  {Mathf.RoundToInt(brightness * 100)}%", detail);

            float next = GUI.HorizontalSlider(
                new Rect(left, panel.y + 104 * scale, width, 24 * scale), brightness, 0.6f, 1f);
            if (!Mathf.Approximately(next, brightness))
            {
                brightness = next;
                PlayerPrefs.SetFloat(BrightnessKey, brightness);
            }

            if (DrawButton(new Rect(left, panel.y + 136 * scale, width, 40 * scale),
                largeText ? "Text size: Large" : "Text size: Standard"))
            {
                largeText = !largeText;
                PlayerPrefs.SetInt(LargeTextKey, largeText ? 1 : 0);
            }

            if (DrawButton(new Rect(left, panel.y + 192 * scale, width, 40 * scale), "Resume"))
                SetSettingsVisible(false);
        }

        private void DrawRoomDetails(Rect screen, float scale)
        {
            Rect panel = DrawOverlay(screen, 166 * scale, scale);
            float left = panel.x + 20 * scale;
            float width = panel.width - 40 * scale;
            GUI.Label(new Rect(left, panel.y + 12 * scale, width, 50 * scale),
                $"Room {inspectedRoom + 1}", title);
            GUI.Label(new Rect(left, panel.y + 66 * scale, width, 30 * scale),
                inspectedRoom < Occupied ? "Occupied by a checked-in guest." : "Vacant and ready for check-in.", detail);
            if (DrawButton(new Rect(left, panel.y + 110 * scale, width, 40 * scale), "Back to exterior"))
            {
                inspectedRoom = NoRoom;
                SetActorPause(false);
            }
        }

        private void PrepareStyles(float scale)
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                detail = new GUIStyle(GUI.skin.label);
                label = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
                title.normal.textColor = Color.white;
                detail.normal.textColor = Color.white;
                label.normal.textColor = Accent;
            }

            title.fontSize = Mathf.RoundToInt((largeText ? 40 : 36) * scale);
            detail.fontSize = Mathf.RoundToInt((largeText ? 19 : 16) * scale);
            label.fontSize = Mathf.RoundToInt(14 * scale);
            button.fontSize = Mathf.RoundToInt(17 * scale);
        }

        private bool DrawButton(Rect bounds, string text)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = Accent;
            bool clicked = GUI.Button(bounds, text, button);
            GUI.backgroundColor = previous;
            return clicked;
        }

        private static void Fill(Rect bounds, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
