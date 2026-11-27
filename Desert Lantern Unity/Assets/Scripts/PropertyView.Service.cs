using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte LuggageAction = 2;
        private const byte DoorAction = 3;
        private const byte SupplyDeliveryAction = 7;
        private const byte ClockSpeedAction = 9;
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
            bool expiresAtDesk = guest != null && service.HasReservation(Occupied) &&
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
            if (!AtOfficePosition(position))
                return;
            MotelService service = Service;
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

        private MotelWalker FindCarrier(ulong worker) => worker == LocalWorker ? caretaker :
            peers.TryGetValue(worker, out Peer peer) ? peer.Walker : null;

        private void UpdateServicePresentation(float delta)
        {
            MotelService service = Service;
            world.AnimateService(service, service.Bag == LuggageStatus.Carried ? FindCarrier(service.Carrier) : null, delta);
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
                return service.Carrier != LocalWorker ? "A coworker is delivering the luggage"
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
