using System.Collections.Generic;
using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte LuggageAction = 2;
        private const byte DoorAction = 3;
        private const byte SupplyDeliveryAction = 7;
        private const byte ClockSpeedAction = 9;
        private const byte AnswerPhoneAction = 10;
        private const byte DeclinePhoneAction = 26;
        private const byte ReturnPhoneAction = 27;
        private const byte ServeGuestAction = 11;
        private const byte HireBellhopAction = 12;
        private const byte SellUpgradeAction = 13;
        private static readonly Vector3 BellhopStation = new Vector3(-6.8f, 0, 2.5f);
        private static readonly Vector3 ServiceQueue = new Vector3(-8.86f, 0, 5.01f);
        private readonly Queue<Vector3> bellhopRoute = new Queue<Vector3>();
        private readonly Queue<Vector3> visitorRoute = new Queue<Vector3>();
        private readonly MotelWalker[] backgroundGuests = new MotelWalker[MotelService.RoomCount];
        private readonly MotelWalker[] roomVisitors = new MotelWalker[MotelService.RoomCount];
        private MotelWalker bellhop;
        private MotelWalker serviceVisitor;
        private int visitorRoom = NoRoom;
        private Vector3 bellhopTarget, bellhopProgress;
        private float bellhopStall;

        private void UpdateServiceActors(float delta)
        {
            MotelService service = Service;
            if (service.BellhopHired)
            {
                if (bellhop == null)
                {
                    bellhop = CreateServiceWalker("Guest-Vic", BellhopStation);
                    bellhopTarget = bellhopProgress = BellhopStation;
                    bellhopStall = 0;
                }
                bool collecting = service.Bag == LuggageStatus.InCar &&
                    (!service.StaffUrgentOnly(0) || service.CheckoutWaiting() == NoRoom);
                bool bringing = service.Bag == LuggageStatus.Carried && service.Carrier == MotelService.BellhopId;
                Vector3 target = collecting ? MotelWorld.CarApproach(service.BagRoom)
                    : bringing ? MotelWorld.BagDrop(service.BagRoom) : BellhopStation;
                if (target != bellhopTarget)
                {
                    bellhopTarget = target;
                    bellhopProgress = bellhop.Position;
                    bellhopStall = 0;
                    QueueDestination(bellhopRoute, bellhop.Position, target);
                }
                if (bringing && service.DoorUnlocked(service.BagRoom) &&
                    (service.OpenDoors & (1 << service.BagRoom)) == 0 &&
                    MotelWorld.NearFloor(bellhop.Position, MotelWorld.RoomDoor(service.BagRoom)) &&
                    MotelWalker.Distance(bellhop.Position, MotelWorld.RoomDoor(service.BagRoom)) < 1.35f)
                {
                    service.ToggleDoor(service.BagRoom);
                    Service = service;
                }
                Drive(bellhop, bellhopRoute, Vector3.zero, true, delta);
                if ((collecting || bringing) && !TryLuggage(MotelService.BellhopId, bellhop.Position))
                {
                    if (Vector3.Distance(bellhopProgress, bellhop.Position) >= .28f)
                    {
                        bellhopProgress = bellhop.Position;
                        bellhopStall = 0;
                    }
                    else
                        bellhopStall += delta;
                    if (bellhopStall >= 12 && (!bringing || !service.IsBroken(service.BagRoom, Fixture.DoorLock)))
                    {
                        bellhop.Warp(target);
                        bellhopRoute.Clear();
                        bellhopStall = 0;
                        TryLuggage(MotelService.BellhopId, bellhop.Position);
                    }
                }
            }

            if (!service.BellhopHired && bellhop != null)
            {
                bellhop.Dispose();
                bellhop = null;
                bellhopRoute.Clear();
            }

            int nextVisitor = service.CheckoutWaiting();
            if (nextVisitor == NoRoom && visitorRoom != NoRoom && service.RequestOpen(visitorRoom))
                nextVisitor = visitorRoom;
            if (nextVisitor == NoRoom)
                for (int room = 1; room < Guests.Length; room += 2)
                    if (service.RequestOpen(room))
                    {
                        nextVisitor = room;
                        break;
                    }
            if (nextVisitor == NoRoom)
                nextVisitor = service.WaitingRequest(false);
            if (nextVisitor != visitorRoom)
            {
                serviceVisitor?.Dispose();
                serviceVisitor = null;
                visitorRoom = nextVisitor;
                if (visitorRoom != NoRoom)
                {
                    serviceVisitor = CreateServiceWalker(Guests[visitorRoom], MotelWorld.RoomApproach(visitorRoom));
                    QueueDestination(visitorRoute, serviceVisitor.Position, ServiceQueue);
                }
            }
            if (serviceVisitor != null)
                Drive(serviceVisitor, visitorRoute, Vector3.zero, false, delta);
        }

        private MotelWalker CreateServiceWalker(string model, Vector3 position)
        {
            var walker = new MotelWalker(transform, model, position);
            IgnoreActorCollision(walker, caretaker);
            IgnoreActorCollision(walker, guest);
            IgnoreActorCollision(walker, bellhop);
            IgnoreActorCollision(walker, serviceVisitor);
            foreach (Peer peer in peers.Values)
                IgnoreActorCollision(walker, peer.Walker);
            return walker;
        }

        private static void IgnoreActorCollision(MotelWalker first, MotelWalker second)
        {
            if (first != null && second != null && first != second)
                Physics.IgnoreCollision(first.Capsule, second.Capsule);
        }

        private void ApplyServiceActors(LanMotel shared, float delta)
        {
            if (shared.Service.BellhopHired)
            {
                if (bellhop == null)
                    bellhop = new MotelWalker(transform, "Guest-Vic", shared.Bellhop.Position);
                bellhop.ApplyReplica(shared.Bellhop, delta);
            }
            else if (bellhop != null)
            {
                bellhop.Dispose();
                bellhop = null;
            }
            if (shared.VisitorRoom != visitorRoom)
            {
                serviceVisitor?.Dispose();
                serviceVisitor = null;
                visitorRoom = shared.VisitorRoom;
                if (visitorRoom != NoRoom)
                    serviceVisitor = new MotelWalker(transform, Guests[visitorRoom], shared.Visitor.Position);
            }
            serviceVisitor?.ApplyReplica(shared.Visitor, delta);
        }

        private static readonly Vector3 GuestExit = new Vector3(-6.9f, 0, -11.8f);
        private readonly Queue<Vector3>[] residentRoutes = new Queue<Vector3>[MotelService.RoomCount];
        private readonly Vector3[] residentTargets = new Vector3[MotelService.RoomCount];
        private readonly bool[] residentsAway = new bool[MotelService.RoomCount];

        private static Vector3 ResidentPoint(int room, GuestActivity activity)
        {
            float x = MotelWorld.RoomX[room], floor = MotelWorld.FloorHeight(room);
            return activity switch
            {
                GuestActivity.AtAttractions => GuestExit,
                GuestActivity.AtVending => new Vector3(-7.24f + .29f * (room % 4), 0, 3.78f),
                GuestActivity.Walking => new Vector3(-5.6f + (room % 4) * 2.1f, 0, 3.91f),
                GuestActivity.Socializing => new Vector3(-5.8f + .65f * (room % 3), 0, 3.24f + .75f * (room / 3 % 2)),
                GuestActivity.SeekingHelp => ServiceQueue - Vector3.right * .66f,
                GuestActivity.Showering => new Vector3(x + 1.81f, floor, 11.84f),
                GuestActivity.WatchingTelevision => new Vector3(x + .38f, floor, 9.12f),
                GuestActivity.HostingVisitor => new Vector3(x + .63f, floor, 8.51f),
                _ => new Vector3(x + .55f, floor, 8.52f)
            };
        }

        private void WalkResident(int room, Vector3 target, GuestActivity activity, float delta)
        {
            var walker = backgroundGuests[room];
            Queue<Vector3> route = residentRoutes[room] ??= new Queue<Vector3>();
            if (residentTargets[room] != target)
            {
                residentTargets[room] = target;
                QueueDestination(route, walker.Position, target);
            }
            while (route.TryPeek(out Vector3 next) && Vector3.Distance(walker.Position, next) < .20f)
                route.Dequeue();
            if (route.TryPeek(out Vector3 waypoint))
            {
                Vector3 direction = waypoint - walker.Position;
                Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up);
                Quaternion rotation = forward.sqrMagnitude > .001f ? Quaternion.LookRotation(forward) : walker.Orientation;
                const float speed = 2.65f;
                walker.ApplyReplica(new LanPose
                {
                    Position = Vector3.MoveTowards(walker.Position, waypoint, speed * delta / Mathf.Max(.001f, 1 - Mathf.Exp(-19f * delta))),
                    Rotation = rotation,
                    Speed = speed
                }, delta);
                return;
            }
            Quaternion idle = activity == GuestActivity.WatchingTelevision ? Quaternion.Euler(0, 90f, 0)
                : activity == GuestActivity.Socializing ? Quaternion.Euler(0, room % 2 == 0 ? 55f : 225f, 0)
                : Quaternion.Euler(0, 180f, 0);
            walker.ApplyReplica(new LanPose { Position = target, Rotation = idle }, delta);
        }

        private void UpdateVisibleGuests(float delta)
        {
            MotelService service = Service;
            for (int room = 0; room < Guests.Length; room++)
            {
                bool hosting = (service.ActiveRooms & (1 << room)) != 0 && service.Activity(room) == GuestActivity.HostingVisitor;
                if (hosting)
                {
                    if (roomVisitors[room] == null)
                    {
                        Vector3 chair = new Vector3(MotelWorld.RoomX[room] + .85f, MotelWorld.FloorHeight(room), 7.91f);
                        roomVisitors[room] = CreateServiceWalker(Guests[(room + 3) % Guests.Length], chair);
                    }
                }
                else if (roomVisitors[room] != null)
                {
                    roomVisitors[room].Dispose();
                    roomVisitors[room] = null;
                }

                bool staying = (service.ActiveRooms & (1 << room)) != 0 && room != visitorRoom;
                GuestActivity activity = staying ? service.Activity(room) : GuestActivity.InRoom;
                bool leaving = staying && activity == GuestActivity.AtAttractions;
                if (leaving && (backgroundGuests[room] == null || Vector3.Distance(backgroundGuests[room].Position, GuestExit) < .35f))
                {
                    backgroundGuests[room]?.Dispose();
                    backgroundGuests[room] = null;
                    residentsAway[room] = true;
                    continue;
                }
                if (!staying)
                {
                    backgroundGuests[room]?.Dispose();
                    backgroundGuests[room] = null;
                    residentRoutes[room]?.Clear();
                    residentsAway[room] = false;
                    continue;
                }
                Vector3 point = ResidentPoint(room, activity);
                if (backgroundGuests[room] == null)
                {
                    Vector3 initial = residentsAway[room] ? GuestExit : ResidentPoint(room, GuestActivity.InRoom);
                    backgroundGuests[room] = new MotelWalker(transform, Guests[room], initial);
                    backgroundGuests[room].ApplyReplica(new LanPose { Position = initial, Rotation = Quaternion.Euler(0, 55f, 0) }, delta);
                    residentTargets[room] = new Vector3(float.PositiveInfinity, 0, 0);
                    residentsAway[room] = false;
                }
                WalkResident(room, point, activity, delta);
            }
        }

        private static bool AtPhone(Vector3 point) => InsideOffice(point) &&
            MotelWalker.Distance(point, MotelWorld.PhoneApproach) < .88f;

        private bool VisitorAtDesk => serviceVisitor != null &&
            MotelWalker.Distance(serviceVisitor.Position, ServiceQueue) < .55f;

        private bool CheckoutVisitor => visitorRoom != NoRoom && visitorRoom == Service.CheckoutWaiting();

        private int GuestToServe(Vector3 point)
        {
            if (visitorRoom != NoRoom && !CheckoutVisitor && VisitorAtDesk && Service.RequestOpen(visitorRoom) &&
                MotelWalker.Distance(point, ServiceQueue) < 1.30f)
                return visitorRoom;
            int room = MotelWorld.InteriorRoom(point);
            if (room != NoRoom && room != visitorRoom && Service.RequestOpen(room))
                return room;
            return NoRoom;
        }

        private void HandleGuestAction(byte action, int room, Vector3 point, ulong worker)
        {
            MotelService service = Service;
            if (action == AnswerPhoneAction && AtPhone(point))
                service.TryAnswerPhone(worker);
            else if (action == DeclinePhoneAction && AtPhone(point))
                service.DeclinePhone(worker);
            else if (action == ReturnPhoneAction && AtPhone(point))
                service.ReturnBookingCall(worker);
            else if (action == ServeGuestAction)
            {
                int guestRoom = GuestToServe(point);
                if (guestRoom != NoRoom)
                    service.ServeRequest(guestRoom);
            }
            else if (action == HireBellhopAction && AtComputer(point))
                service.HireBellhop();
            else if (action == SellUpgradeAction)
            {
                int target = room % Guests.Length;
                Vector3 door = MotelWorld.RoomDoor(target);
                if (MotelWorld.NearFloor(point, door) && MotelWalker.Distance(point, door) < 1.5f)
                    service.SellUpgrade(target, room >= Guests.Length);
            }
            Service = service;
        }

        private void RequestGuestAction(byte action, int room = 0)
        {
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(action, new Vector3(room, 0, 0));
            else
                HandleGuestAction(action, room, caretaker.Position, LocalWorker);
        }

        private void DrawGuestServiceMarkers(float scale)
        {
            if ((InsideOffice(caretaker.Position) || Service.PhoneRinging) &&
                DrawWorldButton(MotelWorld.PhonePosition + Vector3.up * .55f,
                    Service.PhoneRinging ? "Answer phone" : "Telephone", 127 * scale, scale))
            {
                if (AtPhone(caretaker.Position))
                    OpenPhone();
                else
                    SetDestination(MotelWorld.PhoneApproach);
            }
            if (VisitorAtDesk && (CheckoutVisitor || (Service.KnownRequests & (1 << visitorRoom)) == 0) &&
                DrawWorldButton(ServiceQueue + Vector3.up * 1.82f,
                    CheckoutVisitor ? $"Room {visitorRoom + 1} · Ready to check out" : "Talk to guest", 150 * scale, scale))
            {
                if (CheckoutVisitor || MotelWalker.Distance(caretaker.Position, ServiceQueue) < 1.30f)
                    RequestReception();
                else
                    SetDestination(ServiceQueue + Vector3.left * .62f);
            }
        }

        private string GuestServiceObjective()
        {
            if (Service.PhoneRinging)
                return AtPhone(caretaker.Position) ? "E  ·  Answer the phone" :
                    Service.BookingCallRinging ? "Incoming reservation call · Reception phone" : "Incoming guest call · Reception phone";
            if (GuestToServe(caretaker.Position) != NoRoom)
                return $"E  ·  {Service.RequestLabel(GuestToServe(caretaker.Position))}";
            return VisitorAtDesk && !CheckoutVisitor && (Service.KnownRequests & (1 << visitorRoom)) == 0
                ? "A guest is waiting to ask for supplies" : null;
        }

        private MotelService lanService = MotelService.New();

        private MotelService Service
        {
            get => lan == null ? Campaign.Motel : lanService;
            set
            {
                if (lan == null) Campaign.UpdateMotel(value);
                else lanService = value;
            }
        }

        private ulong LocalWorker => lan?.LocalId ?? 0;

        private static bool AtTrunk(int room, Vector3 position) =>
            MotelWalker.Distance(position, MotelWorld.CarApproach(room)) < 1.45f;

        private static bool AtRack(int room, Vector3 position) =>
            MotelWorld.InteriorRoom(position) == room && MotelWalker.Distance(position, MotelWorld.BagDrop(room)) < 1.35f;

        private bool TryLuggage(ulong player, Vector3 position)
        {
            MotelService service = Service;
            if (service.Bag == LuggageStatus.InCar && AtTrunk(service.BagRoom, position) && service.PickUp(player))
            {
                Service = service;
                return true;
            }
            if (service.Bag == LuggageStatus.Carried && AtRack(service.BagRoom, position) && service.Deliver(player))
            {
                Service = service;
                nextGuestDelay = Mathf.Lerp(5.5f, 1.12f, service.Demand / 100f);
                return true;
            }
            return false;
        }

        private bool RequestLuggage()
        {
            MotelService service = Service;
            bool eligible = (service.Bag == LuggageStatus.InCar && AtTrunk(service.BagRoom, caretaker.Position)) ||
                (service.Bag == LuggageStatus.Carried && service.Carrier == LocalWorker &&
                 AtRack(service.BagRoom, caretaker.Position));
            if (!eligible)
                return false;

            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(LuggageAction, Vector3.zero);
            else
                TryLuggage(LocalWorker, caretaker.Position);
            return true;
        }

        private void ToggleDoor(int room, Vector3 position)
        {
            if (room < 0 || room >= Guests.Length || !MotelWorld.NearFloor(position, MotelWorld.RoomDoor(room)) ||
                MotelWalker.Distance(position, MotelWorld.RoomDoor(room)) > 1.5f)
                return;
            MotelService service = Service;
            if (escorting && Occupied == room)
                return;
            service.ToggleDoor(room);
            Service = service;
        }

        private void RequestDoor(int room)
        {
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(DoorAction, new Vector3(room, 0, 0));
            else
                ToggleDoor(room, caretaker.Position);
        }

        private void AdvanceMotelClock(float delta)
        {
            clockFraction += delta * 6f * Mathf.Max(1, (int)Service.ClockSpeed);
            int minutes = (int)clockFraction;
            if (minutes == 0)
                return;
            clockFraction -= minutes;
            MotelService previous = Service;
            MotelService service = previous;
            service.AdvanceClock(service.ClockMinute + minutes, lan == null);
            service.AdvanceDelivery();
            if (lan != null && previous.ClockMinute / 30 != service.ClockMinute / 30)
                service.WorkMotelShift(false);
            Service = service;
            if (lan == null)
                Campaign.Advance(service.ClockMinute, service.Property, previous);
            service = Service;
            if (guest != null && !escorting && (!service.DeskArrivalReady(Occupied) || service.BookingStart(Occupied) != guestArrivalMinute))
            {
                guest.Dispose();
                guest = null;
                escorting = false;
                guestRoute.Clear();
            }
        }

        private void SetClockSpeed(byte speed)
        {
            MotelService service = Service;
            service.ClockSpeed = speed;
            Service = service;
        }

        private void CycleClockSpeed()
        {
            byte speed = Service.ClockSpeed switch
            {
                1 => 2,
                2 => 4,
                4 => 8,
                _ => 1
            };
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(ClockSpeedAction, new Vector3(speed, 0, 0));
            else
                SetClockSpeed(speed);
        }

        private void HandleReception(ulong worker, Vector3 position, System.Collections.Generic.Queue<Vector3> route)
        {
            bool talkingToVisitor = VisitorAtDesk && !CheckoutVisitor && MotelWalker.Distance(position, ServiceQueue) < 1.30f;
            bool handingKey = GuestWaiting && MotelWalker.Distance(position, guest.Position) < 1.4f;
            if (!AtOfficePosition(position) && !talkingToVisitor && !handingKey)
                return;
            MotelService service = Service;
            if (talkingToVisitor && (service.KnownRequests & (1 << visitorRoom)) == 0)
            {
                service.AcknowledgeRequest(visitorRoom);
                Service = service;
                return;
            }
            if (handingKey && deskCheckInApproved && selectedKeys.TryGetValue(worker, out int key) && key == Occupied &&
                service.ArrivalReady(key) && service.Bag == LuggageStatus.None)
            {
                selectedKeys.Remove(worker);
                StartEscort(worker, position, route);
            }
        }

        private static bool AtDeliveryBay(Vector3 position) =>
            MotelWalker.Distance(position, MotelWorld.DeliveryBay) < 1.25f;

        private static bool AtSupplyStorage(Vector3 position) =>
            InsideOffice(position) && MotelWalker.Distance(position, MotelWorld.SupplyStorage) < 1.10f;

        private void HandleSupplyDelivery(ulong worker, Vector3 position, PropertyDelivery delivery)
        {
            MotelService service = Service;
            if (!service.OwnsDelivery(delivery))
                return;
            if (AtDeliveryBay(position))
                service.CollectSupply(worker);
            else if (AtSupplyStorage(position))
                service.StoreSupply(worker);
            Service = service;
        }

        private bool RequestSupplyDelivery()
        {
            MotelService service = Service;
            bool pickup = service.Delivery == SupplyDelivery.AtBay && AtDeliveryBay(caretaker.Position) &&
                (service.Bag != LuggageStatus.Carried || service.Carrier != LocalWorker);
            bool store = service.Delivery == SupplyDelivery.Carried && service.SupplyCarrier == LocalWorker &&
                AtSupplyStorage(caretaker.Position);
            if (!pickup && !store)
                return false;
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(SupplyDeliveryAction, new Vector3((int)service.Property, service.ActiveOrderNumber, (int)DeliveryDock.SupplyBay));
            else
                HandleSupplyDelivery(LocalWorker, caretaker.Position, service.DeliveryReference);
            return true;
        }

        private MotelWalker FindCarrier(ulong worker) => worker == MotelService.BellhopId ? bellhop : worker == LocalWorker ? caretaker :
            peers.TryGetValue(worker, out Peer peer) ? peer.Walker : null;

        private void UpdateServicePresentation(float delta)
        {
            MotelService service = Service;
            world.ShowExpansion(service);
            world.ShowGuestCars(service, vehicleArriving || guest != null ? Occupied : NoRoom,
                vehicleArriving ? arrivalCarProgress : 1);
            world.AnimateService(service, service.Bag == LuggageStatus.Carried ? FindCarrier(service.Carrier) : null, delta);
            UpdateVisibleGuests(delta);
            if (caretaker != null)
                world.ShowDeskPhone(service.PhoneRinging);
            world.ShowReceptionChecklist(CountArrivals(service), CountDepartures(service), CountRequests(service), service.PendingOrderCount);
            bool hasKey = GuestWaiting && selectedKeys.ContainsKey(LocalWorker);
            int missingKeys = service.UnlockedDoors | (escorting ? 1 << Occupied : 0) | ~((1 << service.RoomCapacity) - 1);
            if (hasKey)
                missingKeys |= 1 << selectedKeys[LocalWorker];
            world.ShowReceptionKeys(missingKeys, hasKey ? caretaker : null);
            world.ShowPurchasedServices(service);
            world.ShowHousekeeping(service, service.CartInUse ? FindCarrier(service.CartCarrier) : null);
            world.ShowCleaningTool(heldCleaningTool, caretaker, Time.unscaledTime < cleaningToolUntil);
            world.ShowSupplyDelivery(service, service.Delivery == SupplyDelivery.Carried
                ? FindCarrier(service.SupplyCarrier) : null);
        }

        private string SupplyObjective()
        {
            MotelService service = Service;
            return service.Delivery switch
            {
                SupplyDelivery.AtBay => AtDeliveryBay(caretaker.Position)
                    ? "E  ·  Pick up the supply crate" : "Pick up the order at the delivery bay",
                SupplyDelivery.Carried when service.SupplyCarrier == LocalWorker => AtSupplyStorage(caretaker.Position)
                    ? "E  ·  Store the supplies" : "Carry the crate to supply storage",
                _ => null
            };
        }

        private void DrawSupplyDeliveryMarker(float scale)
        {
            MotelService service = Service;
            bool pickup = service.Delivery == SupplyDelivery.AtBay;
            bool store = service.Delivery == SupplyDelivery.Carried && service.SupplyCarrier == LocalWorker;
            if (!pickup && !store)
                return;
            Vector3 destination = pickup ? MotelWorld.DeliveryBay : MotelWorld.SupplyStorage;
            string caption = pickup ? "Collect supply crate" : "Store supply crate";
            if (DrawWorldButton(destination + Vector3.up * 1.12f, caption, 170 * scale, scale) &&
                !RequestSupplyDelivery())
                SetDestination(destination);
        }

        private string ServiceObjective()
        {
            MotelService service = Service;
            if (service.Bag == LuggageStatus.InCar)
                return AtTrunk(service.BagRoom, caretaker.Position) ? "E  ·  Pick up the suitcase"
                    : $"Room {service.BagRoom + 1} paid · Collect luggage from their car";
            if (service.Bag == LuggageStatus.Carried)
                return service.Carrier == MotelService.BellhopId ? "The bellhop is carrying the luggage" :
                    service.Carrier != LocalWorker ? "A coworker is delivering the luggage"
                    : AtRack(service.BagRoom, caretaker.Position) ? "E  ·  Set luggage on the rack"
                    : $"Carry the suitcase inside Room {service.BagRoom + 1}";
            return null;
        }

        private void DrawServiceAction(Rect hud, float scale)
        {
            MotelService service = Service;
            bool fetching = service.Bag == LuggageStatus.InCar;
            bool delivering = service.Bag == LuggageStatus.Carried && service.Carrier == LocalWorker;
            if (hud.width < 440 * scale)
                return;

            Rect buttonRect = new Rect(hud.xMax - 115 * scale, hud.y + 3 * scale, 108 * scale, 26 * scale);
            if (fetching || delivering)
            {
                Vector3 target = fetching ? MotelWorld.CarApproach(service.BagRoom) : MotelWorld.BagDrop(service.BagRoom);
                bool near = fetching ? AtTrunk(service.BagRoom, caretaker.Position) : AtRack(service.BagRoom, caretaker.Position);
                string text = fetching ? (near ? "Pick up" : "Go to car") : (near ? "Set down" : "Go to room");
                if (DrawButton(buttonRect, text) && !RequestLuggage())
                    SetDestination(target);
                return;
            }

            if (service.Delivery == SupplyDelivery.AtBay)
            {
                if (DrawButton(buttonRect, AtDeliveryBay(caretaker.Position) ? "Pick up" : "Go to bay") &&
                    !RequestSupplyDelivery())
                    SetDestination(MotelWorld.DeliveryBay);
            }
            else if (service.Delivery == SupplyDelivery.Carried && service.SupplyCarrier == LocalWorker &&
                DrawButton(buttonRect, AtSupplyStorage(caretaker.Position) ? "Store" : "Go to shelf") &&
                !RequestSupplyDelivery())
            {
                SetDestination(MotelWorld.SupplyStorage);
            }
        }

        private void DrawLuggageMarker(float scale)
        {
            MotelService service = Service;
            if (service.Bag == LuggageStatus.InCar &&
                DrawWorldButton(MotelWorld.CarApproach(service.BagRoom) + Vector3.up * 1.25f, "Guest luggage", 133 * scale, scale))
            {
                if (!RequestLuggage())
                    SetDestination(MotelWorld.CarApproach(service.BagRoom));
            }
            if (service.Bag == LuggageStatus.Carried && service.Carrier == LocalWorker &&
                DrawWorldButton(MotelWorld.RoomSign(service.BagRoom), "Deliver luggage", 140 * scale, scale))
            {
                if (!RequestLuggage())
                    SetDestination(MotelWorld.BagDrop(service.BagRoom));
            }
        }
    }
}
