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
        private const byte ServeGuestAction = 11;
        private const byte HireBellhopAction = 12;
        private const byte SellUpgradeAction = 13;
        private static readonly Vector3 BellhopStation = new Vector3(-6.8f, 0, 2.5f);
        private static readonly Vector3 ServiceQueue = new Vector3(-8.86f, 0, 5.01f);
        private readonly Queue<Vector3> bellhopRoute = new Queue<Vector3>();
        private readonly Queue<Vector3> visitorRoute = new Queue<Vector3>();
        private readonly MotelWalker[] backgroundGuests = new MotelWalker[4];
        private MotelWalker bellhop;
        private MotelWalker serviceVisitor;
        private int visitorRoom = NoRoom;
        private Vector3 bellhopTarget;

        private void UpdateServiceActors(float delta)
        {
            MotelService service = Service;
            if (service.BellhopHired)
            {
                if (bellhop == null)
                {
                    bellhop = CreateServiceWalker("Guest-Vic", BellhopStation);
                    bellhopTarget = BellhopStation;
                }
                bool collecting = service.Bag == LuggageStatus.InCar;
                bool bringing = service.Bag == LuggageStatus.Carried && service.Carrier == MotelService.BellhopId;
                Vector3 target = collecting ? MotelWorld.CarApproach(service.BagRoom)
                    : bringing ? MotelWorld.BagDrop(service.BagRoom) : BellhopStation;
                if (target != bellhopTarget)
                {
                    bellhopTarget = target;
                    QueueDestination(bellhopRoute, bellhop.Position, target);
                }
                Drive(bellhop, bellhopRoute, Vector3.zero, true, delta);
                if (collecting || bringing)
                    TryLuggage(MotelService.BellhopId, bellhop.Position);
            }

            int nextVisitor = NoRoom;
            if (visitorRoom != NoRoom && service.RequestOpen(visitorRoom))
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

        private void UpdateVisibleGuests(float delta)
        {
            MotelService service = Service;
            int queued = 0;
            for (int room = 0; room < Guests.Length; room++)
            {
                bool staying = (service.ActiveRooms & (1 << room)) != 0 && room != visitorRoom;
                bool waiting = guest != null && room != Occupied && service.ArrivalReady(room);
                if (!staying && !waiting)
                {
                    backgroundGuests[room]?.Dispose();
                    backgroundGuests[room] = null;
                    continue;
                }
                Vector3 point = staying
                    ? new Vector3(MotelWorld.RoomX[room] + .55f, 0, 8.52f)
                    : new Vector3(-10.96f, 0, 5.11f + .62f * queued++);
                if (backgroundGuests[room] == null)
                    backgroundGuests[room] = new MotelWalker(transform, Guests[room], point);
                backgroundGuests[room].ApplyReplica(new LanPose
                {
                    Position = point,
                    Rotation = Quaternion.Euler(0, staying ? 180f : 55f, 0)
                }, delta);
            }
        }

        private static bool AtPhone(Vector3 point) => InsideOffice(point) &&
            MotelWalker.Distance(point, MotelWorld.PhoneApproach) < .88f;

        private bool VisitorAtDesk => serviceVisitor != null &&
            MotelWalker.Distance(serviceVisitor.Position, ServiceQueue) < .55f;

        private int GuestToServe(Vector3 point)
        {
            if (visitorRoom != NoRoom && VisitorAtDesk && Service.RequestOpen(visitorRoom) &&
                MotelWalker.Distance(point, ServiceQueue) < 1.30f)
                return visitorRoom;
            int room = MotelWorld.InteriorRoom(point);
            if (room != NoRoom && room != visitorRoom && Service.RequestOpen(room))
                return room;
            return NoRoom;
        }

        private void HandleGuestAction(byte action, int room, Vector3 point)
        {
            MotelService service = Service;
            if (action == AnswerPhoneAction && AtPhone(point))
            {
                int caller = service.WaitingRequest(true);
                if (caller != NoRoom)
                    service.AcknowledgeRequest(caller);
            }
            else if (action == ServeGuestAction)
            {
                int guestRoom = GuestToServe(point);
                if (guestRoom != NoRoom)
                    service.GiveTowel(guestRoom);
            }
            else if (AtComputer(point))
            {
                if (action == HireBellhopAction)
                    service.HireBellhop();
                else if (action == SellUpgradeAction)
                    service.SellUpgrade(room % Guests.Length, room >= Guests.Length);
            }
            Service = service;
        }

        private void RequestGuestAction(byte action, int room = 0)
        {
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(action, new Vector3(room, 0, 0));
            else
                HandleGuestAction(action, room, caretaker.Position);
        }

        private void DrawGuestServiceMarkers(float scale)
        {
            bool ringing = Service.WaitingRequest(true) != NoRoom;
            if (ringing && DrawWorldButton(MotelWorld.PhonePosition + Vector3.up * .75f, "Answer phone", 150 * scale, scale))
            {
                if (AtPhone(caretaker.Position))
                    RequestGuestAction(AnswerPhoneAction);
                else
                    SetDestination(MotelWorld.PhoneApproach);
            }
            if (VisitorAtDesk && (Service.KnownRequests & (1 << visitorRoom)) == 0 &&
                DrawWorldButton(ServiceQueue + Vector3.up * 1.82f, "Talk to guest", 150 * scale, scale))
            {
                if (MotelWalker.Distance(caretaker.Position, ServiceQueue) < 1.30f)
                    RequestReception();
                else
                    SetDestination(ServiceQueue + Vector3.left * .62f);
            }
        }

        private string GuestServiceObjective()
        {
            if (Service.WaitingRequest(true) != NoRoom)
                return AtPhone(caretaker.Position) ? "E  ·  Answer the phone" : "The reception phone is ringing";
            if (GuestToServe(caretaker.Position) != NoRoom)
                return Service.Towels > 0 ? "E  ·  Give the guest a towel" : "Order towels on the reception computer";
            return VisitorAtDesk && (Service.KnownRequests & (1 << visitorRoom)) == 0
                ? "A guest is waiting to ask for supplies" : null;
        }

        private static MotelService soloService = MotelService.New();
        private MotelService lanService = MotelService.New();

        private MotelService Service
        {
            get => lan == null ? soloService : lanService;
            set
            {
                if (lan == null) soloService = value;
                else lanService = value;
            }
        }

        private ulong LocalWorker => lan?.LocalId ?? 0;

        private static bool AtTrunk(int room, Vector3 position) =>
            MotelWalker.Distance(position, MotelWorld.CarApproach(room)) < 1.45f;

        private static bool AtRack(int room, Vector3 position) =>
            position.z > 6.8f && MotelWalker.Distance(position, MotelWorld.BagDrop(room)) < 1.35f;

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
                nextGuestDelay = 1.12f;
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
            if (room < 0 || room >= Guests.Length ||
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
            MotelService service = Service;
            bool expiresAtDesk = guest != null && service.UpcomingReservation(Occupied) &&
                service.ClockMinute + minutes >= service.BookingEnd(Occupied);
            service.AdvanceClock(service.ClockMinute + minutes);
            service.ExpireBooking(1);
            service.ExpireBooking(2);
            service.AdvanceDelivery();
            Service = service;
            if (expiresAtDesk)
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
            bool talkingToVisitor = VisitorAtDesk && MotelWalker.Distance(position, ServiceQueue) < 1.30f;
            if (!AtOfficePosition(position) && !talkingToVisitor)
                return;
            MotelService service = Service;
            if (talkingToVisitor && (service.KnownRequests & (1 << visitorRoom)) == 0)
            {
                service.AcknowledgeRequest(visitorRoom);
                Service = service;
                return;
            }
            int checkout = service.CheckoutWaiting();
            if (checkout != NoRoom)
            {
                service.CompleteCheckout(checkout, true);
                Service = service;
            }
            else if (GuestWaiting)
                StartEscort(worker, position, route);
        }

        private static bool AtDeliveryBay(Vector3 position) =>
            MotelWalker.Distance(position, MotelWorld.DeliveryBay) < 1.25f;

        private static bool AtSupplyStorage(Vector3 position) =>
            InsideOffice(position) && MotelWalker.Distance(position, MotelWorld.SupplyStorage) < 1.10f;

        private void HandleSupplyDelivery(ulong worker, Vector3 position)
        {
            MotelService service = Service;
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
                lan.State?.ActionRpc(SupplyDeliveryAction, Vector3.zero);
            else
                HandleSupplyDelivery(LocalWorker, caretaker.Position);
            return true;
        }

        private MotelWalker FindCarrier(ulong worker) => worker == MotelService.BellhopId ? bellhop : worker == LocalWorker ? caretaker :
            peers.TryGetValue(worker, out Peer peer) ? peer.Walker : null;

        private void UpdateServicePresentation(float delta)
        {
            MotelService service = Service;
            world.AnimateService(service, service.Bag == LuggageStatus.Carried ? FindCarrier(service.Carrier) : null, delta);
            UpdateVisibleGuests(delta);
            if (caretaker != null)
                world.ShowDeskPhone(service.WaitingRequest(true) != NoRoom);
            world.ShowPurchasedServices(service);
            world.ShowHousekeeping(service);
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
