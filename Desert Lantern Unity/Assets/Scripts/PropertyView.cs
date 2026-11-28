using System.Collections.Generic;
using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView : MonoBehaviour
    {
        private static readonly Color Accent = new Color32(255, 209, 134, 255);
        private static readonly Color Ink = new Color32(36, 24, 16, 255);
        private static readonly string[] Guests = { "Guest-Mara", "Guest-Eli", "Guest-June", "Guest-Vic",
            "Guest-Eli", "Guest-Vic", "Guest-Mara", "Guest-June", "Guest-Mara", "Guest-Vic", "Guest-June", "Guest-Eli" };
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
        private Vector3 previousMouse;
        private float yaw;
        private float pitch = 9;
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
        private string CurrentGuestModel => Guests[Occupied];
        private bool GuestWaiting => guest != null && !guestArriving && !escorting;
        private bool Modal => showSettings || lanMenu || computerOpen || budgetComputer || receptionPanel != ReceptionPanel.None || inspectedRoom != NoRoom;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            occupiedRooms = 0;
            campaign = null;
            PropertyTravelManager.Reset();
        }

        private void Awake()
        {
            brightness = Mathf.Clamp(PlayerPrefs.GetFloat(BrightnessKey, 1f), .6f, 1f);
            largeText = PlayerPrefs.GetInt(LargeTextKey, 0) != 0;
            if (isMap)
            {
                InitializeLanMenu();
                countyBackdrop = Resources.Load<Texture2D>("RiversideCounty");
                lanMenu = !string.IsNullOrEmpty(LanSession.Notice);
            }
            else
            {
                InitializeLan();
                foregroundProperty = lan == null ? PropertyTravelManager.Arrive() : PropertyId.RoadsideMotel;
                if (AtBudgetHotel)
                {
                    StartBudgetLobby();
                    return;
                }
                if (lan == null && !Campaign.Started)
                    Campaign.Visit(CountyPortfolio.MapSlot(PropertyId.RoadsideMotel));
            }
            if (!isMap || countyBackdrop == null)
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
            {
                TickCounty(Time.deltaTime);
                return;
            }

            float delta = Time.deltaTime;
            if (AtBudgetHotel)
            {
                UpdateBudgetLobby(delta);
                return;
            }
            CheckComputerDestination();
            Vector3 mouse = Input.mousePosition;
            if (!Modal && caretaker != null)
            {
                if (Input.GetMouseButton(1) && !Input.GetMouseButtonDown(1))
                {
                    Vector3 offset = mouse - previousMouse;
                    yaw += offset.x * .23f;
                    pitch = Mathf.Clamp(pitch - offset.y * .18f, 6, 61);
                }
                if (lan == null || lan.IsHost)
                    UpdateWalk(delta);
                if (lan == null)
                    UpdateGuest(delta);
                if (lan == null && Input.GetKeyDown(KeyCode.P))
                    SetReceptionPanel(ReceptionPanel.Portfolio);
                if (Input.GetKeyDown(KeyCode.N))
                    SetReceptionPanel(ReceptionPanel.Notebook);
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
            if (isMap || AtBudgetHotel)
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
            bool indoors = InsideOffice(caretaker.Position) || MotelWorld.InsideUtility(caretaker.Position) ||
                MotelWorld.InteriorRoom(caretaker.Position) != NoRoom;
            float distance = indoors ? 3.7f : 5.55f;
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

        private bool guestArriving, vehicleArriving;
        private float arrivalCarProgress;
        private Vector3 guestProgressPoint;
        private float guestStall;
        private int guestWaitStart;

        private void SpawnGuest()
        {
            if (guest != null || vehicleArriving)
                return;
            int arrival = Service.NextDeskArrival();
            if (arrival == NoRoom)
                return;
            Occupied = arrival;
            deskCheckInApproved = false;
            selectedKeys.Clear();
            guestArrivalMinute = Service.BookingStart(arrival);
            arrivalCarProgress = 0;
            vehicleArriving = true;
        }

        private void ReleaseArrivingGuest()
        {
            int arrival = Occupied;
            vehicleArriving = false;
            guestArriving = true;
            Vector3 arrivalPoint = MotelWorld.CarApproach(arrival);
            guest = new MotelWalker(transform, CurrentGuestModel, arrivalPoint);
            QueueDestination(guestRoute, arrivalPoint, MotelWorld.GuestSpawn);
            ResetGuestProgress();
            if (caretaker != null)
                Physics.IgnoreCollision(caretaker.Capsule, guest.Capsule);
            foreach (Peer peer in peers.Values)
                Physics.IgnoreCollision(peer.Walker.Capsule, guest.Capsule);
            IgnoreActorCollision(guest, bellhop);
            IgnoreActorCollision(guest, serviceVisitor);
        }

        private bool AdmitGuest()
        {
            MotelService service = Service;
            int deskMinute = deskCheckInApproved ? guestDeskMinute : service.ClockMinute;
            if (!service.CheckInWithStaff(Occupied, guestWaitStart, deskMinute))
                return false;
            Service = service;
            ResetGuestProgress();
            escorting = true;
            selectedKeys.Clear();
            Vector3 destination = MotelWorld.RoomDoor(Occupied) + Vector3.forward * 1.22f;
            QueueDestination(guestRoute, guest.Position, destination);
            return true;
        }

        private void FinishGuestArrival()
        {
            guest.Dispose();
            guest = null;
            escorting = deskCheckInApproved = false;
            selectedKeys.Clear();
        }

        private void ResetGuestProgress()
        {
            guestProgressPoint = guest.Position;
            guestStall = 0;
        }

        private bool GuestRouteStalled(float delta)
        {
            if (Vector3.Distance(guestProgressPoint, guest.Position) >= .23f)
                ResetGuestProgress();
            else
                guestStall += delta;
            return guestStall >= 10;
        }

        private void UpdateGuest(float delta)
        {
            if (nextGuestDelay > 0)
            {
                nextGuestDelay -= delta;
            }
            if (guest == null && nextGuestDelay <= 0)
                SpawnGuest();
            if (vehicleArriving)
            {
                if (!Service.DeskArrivalReady(Occupied) || Service.BookingStart(Occupied) != guestArrivalMinute)
                {
                    vehicleArriving = false;
                    return;
                }
                arrivalCarProgress = Mathf.Min(1, arrivalCarProgress + delta / 5f);
                if (arrivalCarProgress >= 1)
                    ReleaseArrivingGuest();
                return;
            }
            if (guest == null)
                return;

            if (guestArriving)
            {
                Drive(guest, guestRoute, Vector3.zero, false, delta);
                if (guestRoute.Count == 0 || GuestRouteStalled(delta))
                {
                    if (guestRoute.Count > 0)
                        guest.Warp(MotelWorld.GuestSpawn);
                    guestRoute.Clear();
                    guestArriving = false;
                    guestWaitStart = Service.ClockMinute;
                    ResetGuestProgress();
                }
                return;
            }

            if (!escorting)
            {
                if (!Service.DeskArrivalReady(Occupied) || Service.BookingStart(Occupied) != guestArrivalMinute)
                {
                    guest.Dispose();
                    guest = null;
                    deskCheckInApproved = false;
                    selectedKeys.Clear();
                    return;
                }
                if (!deskCheckInApproved && Service.StaffHired(StaffRole.Receptionist) && Service.ClockMinute - guestWaitStart >= 25 &&
                    Service.DeskArrivalReady(Occupied))
                {
                    MotelService service = Service;
                    if (service.BookingDue(Occupied) || service.AcceptReservation(FindArrivalBooking(service), guestArrivalMinute))
                    {
                        Service = service;
                        AdmitGuest();
                    }
                }
                else
                {
                    guest.Move(Vector3.zero, delta);
                    return;
                }
            }

            Drive(guest, guestRoute, Vector3.zero, false, delta);
            if (guestRoute.Count == 0 || GuestRouteStalled(delta))
                FinishGuestArrival();
        }

        private int NearRoom()
        {
            for (int i = 0; i < Guests.Length; i++)
                if (MotelWorld.NearFloor(caretaker.Position, MotelWorld.RoomDoor(i)) &&
                    MotelWalker.Distance(caretaker.Position, MotelWorld.RoomDoor(i)) < 1.5f)
                    return i;
            return NoRoom;
        }

        private void Interact()
        {
            if (InteractWithGuestTask() || RequestHousekeeping() || RequestSupplyDelivery() || RequestLuggage())
                return;
            if (AtPhone(caretaker.Position) && Service.PhoneRinging)
            {
                OpenPhone();
                return;
            }
            if (GuestToServe(caretaker.Position) != NoRoom)
            {
                OpenGuestMenu(GuestToServe(caretaker.Position));
                return;
            }
            if (!CheckoutVisitor && VisitorAtDesk && MotelWalker.Distance(caretaker.Position, ServiceQueue) < 1.30f &&
                (Service.KnownRequests & (1 << visitorRoom)) == 0)
            {
                RequestReception();
                return;
            }
            if (GuestWaiting && deskCheckInApproved && AtKeyCounter(caretaker.Position))
            {
                SetReceptionPanel(ReceptionPanel.Keys);
                return;
            }
            if (AtComputer(caretaker.Position))
            {
                OpenComputer();
                return;
            }
            if (GuestWaiting && MotelWalker.Distance(caretaker.Position, guest.Position) < 1.4f)
            {
                RequestReception();
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
            bool handingKey = deskCheckInApproved && GuestWaiting &&
                MotelWalker.Distance(caretaker.Position, guest.Position) < 1.4f;
            bool helping = !CheckoutVisitor && VisitorAtDesk && MotelWalker.Distance(caretaker.Position, ServiceQueue) < 1.30f &&
                (Service.KnownRequests & (1 << visitorRoom)) == 0;
            if (!helping && !handingKey && (GuestWaiting || Service.CheckoutWaiting() != NoRoom))
            {
                if (AtComputer(caretaker.Position))
                    OpenDeskReservations();
                else
                    SetDestination(MotelWorld.ComputerApproach);
                return;
            }
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(ReceptionAction, Vector3.zero);
            else
                HandleReception(LocalWorker, caretaker.Position);
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
            bool fromUtility = MotelWorld.InsideUtility(origin);
            bool toUtility = MotelWorld.InsideUtility(destination);
            if (fromUtility && toUtility)
            {
                route.Enqueue(destination);
                return;
            }
            if (fromUtility)
            {
                route.Enqueue(MotelWorld.UtilityThreshold);
                route.Enqueue(MotelWorld.UtilityApproach);
                origin = MotelWorld.UtilityApproach;
            }
            int fromRoom = MotelWorld.InteriorRoom(origin);
            int toRoom = MotelWorld.InteriorRoom(destination);
            if (fromRoom != NoRoom && fromRoom != toRoom)
            {
                route.Enqueue(MotelWorld.RoomDoor(fromRoom) + Vector3.forward * .67f);
                route.Enqueue(MotelWorld.RoomApproach(fromRoom));
            }
            Vector3 threshold = MotelWorld.OfficeApproach;
            bool behindDesk = inside && origin.z >= 7.98f;
            bool towardDesk = destinationInside && destination.z >= 7.98f;
            if (behindDesk && !towardDesk)
            {
                route.Enqueue(MotelWorld.DeskBackCorner);
                route.Enqueue(MotelWorld.DeskFrontCorner);
            }
            if (inside && !destinationInside)
            {
                route.Enqueue(new Vector3(threshold.x, 0, 4.55f));
                route.Enqueue(threshold);
            }

            Vector3 start = inside && !destinationInside ? threshold :
                fromRoom != NoRoom && fromRoom != toRoom ? MotelWorld.RoomApproach(fromRoom) : origin;
            Vector3 end = toUtility ? MotelWorld.UtilityApproach :
                !inside && destinationInside ? threshold :
                toRoom != NoRoom && fromRoom != toRoom ? MotelWorld.RoomApproach(toRoom) : destination;
            bool startUpstairs = start.y > MotelWorld.UpperFloor - .55f;
            bool endUpstairs = end.y > MotelWorld.UpperFloor - .55f;
            bool startOnStairs = MotelWorld.OnStairs(start);
            bool endOnStairs = MotelWorld.OnStairs(end);
            if (endUpstairs || endOnStairs)
            {
                if (startOnStairs)
                {
                    if (endUpstairs)
                        route.Enqueue(MotelWorld.StairTop);
                }
                else if (!startUpstairs)
                {
                    MotelWorld.RouteAcrossParking(route, start, MotelWorld.StairFoot);
                    route.Enqueue(MotelWorld.StairFoot);
                    if (endUpstairs)
                        route.Enqueue(MotelWorld.StairTop);
                }
                else if (endOnStairs)
                    route.Enqueue(MotelWorld.StairTop);
                if (toRoom != NoRoom && fromRoom != toRoom)
                {
                    route.Enqueue(MotelWorld.RoomApproach(toRoom));
                    route.Enqueue(MotelWorld.RoomDoor(toRoom) + Vector3.forward * .67f);
                }
                route.Enqueue(destination);
                return;
            }
            if (startUpstairs || startOnStairs)
            {
                if (!startOnStairs)
                    route.Enqueue(MotelWorld.StairTop);
                start = MotelWorld.StairFoot;
                route.Enqueue(start);
            }

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
            if (towardDesk && !behindDesk)
            {
                route.Enqueue(MotelWorld.DeskFrontCorner);
                route.Enqueue(MotelWorld.DeskBackCorner);
            }
            destination.x = Mathf.Clamp(destination.x, -16.2f, 16.2f);
            destination.z = Mathf.Clamp(destination.z, -13.2f, 13.15f);
            if (toUtility)
            {
                route.Enqueue(MotelWorld.UtilityApproach);
                route.Enqueue(MotelWorld.UtilityThreshold);
            }
            route.Enqueue(destination);
        }

        private static bool InsideOffice(Vector3 p) => p.y < .75f && p.x > -11.95f && p.x < -7.44f && p.z > 4.08f && p.z < MotelWorld.ReceptionBackWall;

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
                    OpenComputer();
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
                    if (Mathf.Abs(hit.point.y - MotelWorld.FloorHeight(room)) > 2.3f ||
                        hit.point.y >= MotelWorld.UpperFloor && MotelWorld.FloorHeight(room) == 0 ||
                        Mathf.Abs(hit.point.x - MotelWorld.RoomX[room]) > .73f || Mathf.Abs(hit.point.z - 6.38f) > .48f)
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
            if (!isMap)
                Fill(screen, new Color(0, 0, 0, 1 - brightness));

            Event input = Event.current;
            if (input.type == EventType.KeyDown && input.keyCode == KeyCode.Escape)
            {
                if (isMap && countySelection >= 0 && !showSettings && !lanMenu)
                    countySelection = -1;
                else if (receptionPanel != ReceptionPanel.None)
                    SetReceptionPanel(ReceptionPanel.None);
                else if (budgetComputer)
                    SetBudgetComputer(false);
                else if (computerOpen)
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
            if (!isMap)
            {
                bool settings = AtBudgetHotel
                    ? DrawButton(new Rect(width - margin - 96 * scale, margin, 96 * scale, 44 * scale), "Menu")
                    : MotelHudButton(new Rect(width - margin - 89 * scale, margin, 89 * scale, 34 * scale), "Menu", scale, HudIcon.Menu);
                if (settings)
                    SetSettingsVisible(true);
            }

            if (isMap)
                DrawMap(screen, margin, scale);
            else if (AtBudgetHotel)
                DrawBudgetLobby(screen, margin, scale);
            else
                DrawExterior(screen, margin, scale);

            GUI.enabled = true;
            if (lanMenu)
                DrawLanMenu(screen, scale);
            else if (receptionPanel != ReceptionPanel.None)
                DrawReceptionPanel(screen, scale);
            else if (budgetComputer)
                DrawBudgetComputer(screen, scale);
            else if (computerOpen)
                DrawReceptionComputer(screen, scale);
            else if (showSettings)
                DrawSettings(screen, scale);
            else if (inspectedRoom != NoRoom)
                DrawRoomDetails(screen, scale);
        }

        private int seenOnlineBookings = -1;
        private int newOnlineBookings;
        private float onlineNoticeUntil;

        private void DrawExterior(Rect screen, float margin, float scale)
        {
            if (MotelHudButton(new Rect(margin, margin, 137 * scale, 34 * scale),
                lan == null ? "Back to map" : "Leave LAN", scale, HudIcon.Map))
            {
                if (lan == null)
                {
                    Campaign.Motel.RecoverHousekeepingCart(LocalWorker);
                    Campaign.Motel.ReleaseGuestTasks(LocalWorker);
                    Campaign.Motel.ReleaseOperations(LocalWorker);
                    Campaign.Motel.RecoverBag(LocalWorker);
                    Campaign.Motel.RecoverSupply(LocalWorker);
                    Campaign.Save();
                    PropertyTravelManager.ReturnToCounty();
                }
                else
                {
                    SaveLanMotel();
                    lan.Stop("Left the LAN motel.");
                }
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

            if (lan == null && MotelHudButton(new Rect(screen.width - margin - 309 * scale, margin, 101 * scale, 34 * scale),
                Campaign.UnreadUpdates > 0 ? $"Phone · {Campaign.UnreadUpdates}" : "Phone", scale, HudIcon.Phone))
                SetReceptionPanel(ReceptionPanel.Portfolio);
            if (MotelHudButton(new Rect(screen.width - margin - 202 * scale, margin, 107 * scale, 34 * scale),
                "Notebook", scale, HudIcon.Notebook))
                SetReceptionPanel(ReceptionPanel.Notebook);
            DrawGuestTags(scale);
            DrawReceptionKeys(scale);
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
            DrawHousekeeping(screen, margin, scale);
            DrawMaintenanceMarkers(scale);
            if (InsideOffice(caretaker.Position) &&
                DrawWorldButton(MotelWorld.ComputerScreen, "Reception computer", 154 * scale, scale))
            {
                if (AtComputer(caretaker.Position))
                    OpenComputer();
                else
                    SetDestination(MotelWorld.ComputerApproach);
            }

            int room = NearRoom();
            string objective = HousekeepingObjective() ?? GuestServiceObjective() ?? (AtComputer(caretaker.Position) ? "E  ·  Open the reception computer" : ServiceObjective()) ?? SupplyObjective() ?? (escorting
                ? $"Guest checked in · Heading to Room {Occupied + 1}"
                : AtOffice && Service.CheckoutWaiting() != NoRoom ? "Check out the guest in Reservations"
                : GuestWaiting && InsideOffice(caretaker.Position) ? deskCheckInApproved
                    ? $"Give the guest the key for Room {Occupied + 1}" : "Check in the guest in Reservations"
                : room != NoRoom ? (Service.DoorUnlocked(room) ? $"E  ·  Open or close Room {room + 1}" : $"Room {room + 1} is locked")
                : GuestWaiting ? "Meet your next guest at the Front Office" : vehicleArriving ? "A reserved guest is arriving by car"
                : guestArriving && guest != null ? "Guest is walking from the parking lot" : "N  ·  Review your next task");
            MotelService reservations = Service;
            float width = Mathf.Min(481 * scale, screen.width - 2 * margin - 108 * scale);
            Rect hud = new Rect(margin, screen.height - margin - 34 * scale, width, 34 * scale);
            DrawMotelLedger(hud, reservations, scale);
            var speed = new Rect(screen.xMax - margin - 101 * scale, hud.y, 101 * scale, hud.height);
            if (MotelHudButton(speed, $"Time x{Mathf.Max(1, (int)reservations.ClockSpeed)}", scale, HudIcon.Play))
                CycleClockSpeed();
            DrawServiceAction(screen, scale);
            Rect objectiveRect = default;
            if (hudActionPrompt.width == 0)
            {
                objectiveRect = new Rect(hud.x, hud.y - 34 * scale, hud.width, 29 * scale);
                DeskFrame(objectiveRect, HudInk, HudEdge, 6 * scale, 1);
                DrawHudText(objectiveRect, objective, scale);
            }
            float noticeBase = objectiveRect.width > 0 ? objectiveRect.y : hud.y;
            int confirmedOnline = reservations.ConfirmedOnlineBookings;
            if (seenOnlineBookings >= 0 && confirmedOnline > seenOnlineBookings)
            {
                newOnlineBookings = (Time.unscaledTime < onlineNoticeUntil ? newOnlineBookings : 0)
                    + confirmedOnline - seenOnlineBookings;
                onlineNoticeUntil = Time.unscaledTime + 14;
            }
            seenOnlineBookings = confirmedOnline;
            int pending = reservations.PendingReservationCount;
            bool onlineNotice = Time.unscaledTime < onlineNoticeUntil;
            float noticeX = housekeepingPanel.width > 0 ? housekeepingPanel.xMax + 6 * scale : hud.x;
            float noticeWidth = Mathf.Max(0, Mathf.Min(hud.width, screen.xMax - noticeX - margin));
            var notice = new Rect(noticeX, noticeBase - 34 * scale, noticeWidth, 29 * scale);
            if (onlineNotice || pending > 0)
            {
                int first = reservations.FirstPendingReservation;
                string summary = onlineNotice ? newOnlineBookings == 1 ? "New online reservation confirmed"
                    : $"{newOnlineBookings} online reservations confirmed"
                    : pending == 1 ? $"New {reservations.ReservationChannel(first).ToString().ToLowerInvariant()} reservation request"
                    : $"{pending} reservation requests awaiting confirmation";
                if (MotelHudButton(notice, $"{summary} · Reservations", scale, HudIcon.Notebook))
                {
                    GoToComputer(ReservationsTab, onlineNotice ? ReservationView.Calendar : ReservationView.Requests);
                    onlineNoticeUntil = 0;
                }
            }
            Rect lowStock = new Rect(noticeX, noticeBase - (onlineNotice || pending > 0 ? 68 : 34) * scale, noticeWidth, 29 * scale);
            bool shortage = reservations.LowStock(1) && reservations.IncomingStock(1) == 0;
            if (shortage && MotelHudButton(lowStock, $"Low stock · {reservations.Towels} clean towels remaining · Order supplies", scale, HudIcon.Warning))
                OpenRestock(1);
            DrawFirstDayGuide(screen, scale, margin);
            DrawPinnedOperation(screen, scale, margin);
            if ((!shortage || !lowStock.Contains(Event.current.mousePosition)) &&
                (!onlineNotice && pending == 0 || !notice.Contains(Event.current.mousePosition)) && !hud.Contains(Event.current.mousePosition) && !objectiveRect.Contains(Event.current.mousePosition) &&
                !speed.Contains(Event.current.mousePosition) && !hudActionPrompt.Contains(Event.current.mousePosition) &&
                !housekeepingPanel.Contains(Event.current.mousePosition) &&
                !taskWaypointPanel.Contains(Event.current.mousePosition) && !guidePanel.Contains(Event.current.mousePosition))
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
            width = Mathf.Min(Mathf.Max(width, button.CalcSize(new GUIContent(text)).x + 12 * scale),
                Screen.width - 8 * scale);
            float x = Mathf.Clamp(projected.x - width / 2, 4 * scale, Screen.width - width - 4 * scale);
            return MotelHudButton(new Rect(x, y - 14 * scale, width, 28 * scale), text, scale);
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused)
                return;
            if (lan != null && lan.IsHost)
                SaveLanMotel();
            else if (lan == null && campaign != null)
                Campaign.Save();
        }

        private void OnApplicationQuit()
        {
            if (lan != null && lan.IsHost)
                SaveLanMotel();
            else if (lan == null && campaign != null)
                Campaign.Save();
        }

        private void OnDestroy()
        {
            if (lan != null)
            {
                SaveLanMotel();
                lan.MovementReceived = null;
                lan.ActionReceived = null;
                lan.BookingResultReceived = null;
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
            foreach (MotelWalker visitor in roomVisitors)
                visitor?.Dispose();
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

        private Rect DrawOverlay(Rect screen, float height, float scale, float preferredWidth = 410)
        {
            Fill(screen, new Color(0, 0, 0, 0.55f));
            float width = Mathf.Min(preferredWidth * scale, screen.width - 32 * scale);
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
            MotelService service = Service;
            int bit = 1 << inspectedRoom;
            bool active = (service.ActiveRooms & bit) != 0;
            Rect panel = DrawOverlay(screen, 289 * scale, scale);
            float left = panel.x + 20 * scale;
            float width = panel.width - 40 * scale;
            GUI.Label(new Rect(left, panel.y + 12 * scale, width, 50 * scale),
                $"Room {inspectedRoom + 1}", title);
            string status = service.RoomUnusable(inspectedRoom) ? "Out of service"
                : service.DoNotDisturb(inspectedRoom) ? "Do not disturb"
                : service.RoomDirty(inspectedRoom) ? "Needs housekeeping"
                : active ? $"In house · Out {MotelService.DisplayTime(service.DepartureMinute(inspectedRoom))}"
                : service.UpcomingReservation(inspectedRoom)
                    ? $"Booked · In {MotelService.DisplayTime(service.BookingStart(inspectedRoom))}"
                    : "Vacant and ready for check-in";
            GUI.Label(new Rect(left, panel.y + 66 * scale, width, 30 * scale), status, detail);
            if (active)
                DrawRoomPurchases(panel, left, width, scale, service, bit);
            else if ((service.Reviews & bit) != 0)
                GUI.Label(new Rect(left, panel.y + 97 * scale, width, 75 * scale),
                    $"{service.StayGuest(inspectedRoom)} · {service.GuestReview(inspectedRoom)}", label);

            if (AtFixture(inspectedRoom, Fixture.DoorLock, caretaker.Position) &&
                DrawButton(new Rect(left, panel.y + 178 * scale, width, 32 * scale), "Inspect door lock"))
            {
                int room = inspectedRoom;
                inspectedRoom = NoRoom;
                InspectFixture(room, Fixture.DoorLock);
            }
            if (DrawButton(new Rect(left, panel.y + 235 * scale, width, 38 * scale), "Close"))
            {
                inspectedRoom = NoRoom;
                SetActorPause(false);
            }
        }

        private void DrawRoomPurchases(Rect panel, float left, float width, float scale, MotelService service, int bit)
        {
            string request = service.RequestOpen(inspectedRoom) ? service.RequestLabel(inspectedRoom)
                : service.RequestSatisfied(inspectedRoom) ? "Request completed" : "No open requests";
            GUI.Label(new Rect(left, panel.y + 98 * scale, width, 26 * scale), request, label);
            float half = (width - 8 * scale) * .5f;
            GUI.enabled = (service.TvPackages & bit) == 0;
            if (DrawButton(new Rect(left, panel.y + 128 * scale, half, 31 * scale),
                GUI.enabled ? "TV access  $12" : "TV active"))
                RequestGuestAction(SellUpgradeAction, inspectedRoom);
            GUI.enabled = service.HasExpansion(Expansion.RoomService) && (service.MealOrders & bit) == 0 &&
                !service.RoomMealPending(inspectedRoom);
            if (DrawButton(new Rect(left + half + 8 * scale, panel.y + 128 * scale, half, 31 * scale),
                (service.MealOrders & bit) != 0 ? "Meal delivered" : service.RoomMealPending(inspectedRoom) ? "Meal pending"
                    : service.HasExpansion(Expansion.RoomService) ? "Take meal order" : "Room service locked"))
                RequestGuestAction(SellUpgradeAction, inspectedRoom + Guests.Length);
            GUI.enabled = true;
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
            if (computerTheme)
                return BookingButton(bounds, text, DeskSurface);
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = Accent;
            int fontSize = button.fontSize;
            GUIContent content = new GUIContent(text);
            FitText(bounds, content, button);
            bool clicked = GUI.Button(bounds, content, button);
            button.fontSize = fontSize;
            GUI.backgroundColor = previous;
            return clicked;
        }

        private static void FitText(Rect bounds, GUIContent content, GUIStyle style)
        {
            while (style.fontSize > 6)
            {
                Vector2 size = style.CalcSize(content);
                if (size.x <= bounds.width && size.y <= bounds.height)
                    break;
                style.fontSize--;
            }
        }

        private static void DrawFittedText(Rect bounds, string text, GUIStyle style)
        {
            int fontSize = style.fontSize;
            GUIContent content = new GUIContent(text);
            FitText(bounds, content, style);
            GUI.Label(bounds, content, style);
            style.fontSize = fontSize;
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
