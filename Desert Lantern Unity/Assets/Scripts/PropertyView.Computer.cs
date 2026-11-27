using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte PurchaseAction = 4;
        private const byte ConfirmBookingAction = 5;
        private const byte CancelBookingAction = 6;
        private bool computerOpen;
        private bool reservationsTab;

        private static bool AtComputer(Vector3 position) =>
            position.z > 5.63f && MotelWalker.Distance(position, MotelWorld.ComputerApproach) < 1.15f;

        private void SetComputerVisible(bool open)
        {
            computerOpen = open;
            if (open)
                walkRoute.Clear();
            SetActorPause(open);
        }

        private void TryPurchase(int offer, Vector3 position)
        {
            MotelService service = Service;
            if (AtComputer(position) && service.Order(offer, Time.unscaledTime))
                Service = service;
        }

        private void Purchase(int offer)
        {
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(PurchaseAction, new Vector3(offer, 0, 0));
            else
                TryPurchase(offer, caretaker.Position);
        }

        private void UpdateBooking(int room, bool confirm, Vector3 position)
        {
            if (!AtComputer(position) || (room == Occupied && guest != null))
                return;
            MotelService service = Service;
            service.UpdateReservation(room, confirm);
            Service = service;
        }

        private void RequestBooking(int room, bool confirm)
        {
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(confirm ? ConfirmBookingAction : CancelBookingAction, new Vector3(room, 0, 0));
            else
                UpdateBooking(room, confirm, caretaker.Position);
        }

        private void DrawReceptionComputer(Rect screen, float scale)
        {
            Rect panel = DrawOverlay(screen, 389 * scale, scale);
            float x = panel.x + 18 * scale;
            float width = panel.width - 36 * scale;
            GUI.Label(new Rect(x, panel.y + 9 * scale, width, 47 * scale), "Reception computer", title);
            float tabWidth = (width - 8 * scale) / 2;
            if (DrawButton(new Rect(x, panel.y + 61 * scale, tabWidth, 32 * scale), "Reservations"))
                reservationsTab = true;
            if (DrawButton(new Rect(x + tabWidth + 8 * scale, panel.y + 61 * scale, tabWidth, 32 * scale), "Supplies"))
                reservationsTab = false;

            if (reservationsTab)
                DrawReservationRows(panel, x, width, scale);
            else
                DrawSupplyRows(panel, x, width, scale);

            if (DrawButton(new Rect(x, panel.y + 339 * scale, width, 35 * scale), "Close computer"))
                SetComputerVisible(false);
        }

        private void DrawReservationRows(Rect panel, float x, float width, float scale)
        {
            MotelService service = Service;
            GUI.Label(new Rect(x, panel.y + 108 * scale, width, 29 * scale),
                $"Online requests  ·  ${MotelService.RoomRate} per night", label);
            for (int room = 1; room <= 2; room++)
            {
                float y = panel.y + (148 + (room - 1) * 85) * scale;
                bool checkedIn = (service.UnlockedDoors & (1 << room)) != 0;
                bool arrived = room == Occupied && guest != null;
                bool reserved = service.HasReservation(room);
                bool cancelled = service.ReservationCancelled(room);
                string status = checkedIn ? (reserved ? "Reservation checked in" : "Walk-in checked in")
                    : cancelled ? "Closed" : arrived ? "Guest has arrived"
                    : reserved ? "Confirmed" : "Awaiting confirmation";
                string guestName = Guests[room].Substring("Guest-".Length);
                GUI.Label(new Rect(x, y, width, 26 * scale), $"{guestName}  ·  Room {room + 1}", detail);
                GUI.Label(new Rect(x, y + 26 * scale, width, 25 * scale), status, label);

                if (checkedIn || cancelled || arrived)
                    continue;
                float right = x + width;
                if (!reserved && DrawButton(new Rect(right - 103 * scale, y + 6 * scale, 103 * scale, 34 * scale), "Confirm"))
                    RequestBooking(room, true);
                float declineY = reserved ? y + 6 * scale : y + 43 * scale;
                if (DrawButton(new Rect(right - 103 * scale, declineY, 103 * scale, 30 * scale),
                    reserved ? "Cancel" : "Decline"))
                    RequestBooking(room, false);
            }
        }

        private void DrawSupplyRows(Rect panel, float x, float width, float scale)
        {
            MotelService service = Service;
            string status = service.Delivery switch
            {
                SupplyDelivery.InTransit => "Order in transit",
                SupplyDelivery.AtBay => "Order at the delivery bay",
                SupplyDelivery.Carried => "Order being unloaded",
                _ => "Ready to order"
            };
            GUI.Label(new Rect(x, panel.y + 108 * scale, width, 31 * scale),
                $"Cash  ${service.Cash}   ·   {status}", label);
            for (int row = 0; row < MotelService.Catalog.Length; row++)
            {
                SupplyOffer offer = MotelService.Catalog[row];
                float y = panel.y + (150 + 58 * row) * scale;
                GUI.Label(new Rect(x, y, width - 91 * scale, 26 * scale),
                    $"{offer.Name} ×{offer.Quantity}", detail);
                GUI.Label(new Rect(x, y + 24 * scale, width - 91 * scale, 25 * scale),
                    $"{Service.Stock(row)} in stock", label);
                GUI.enabled = service.Delivery == SupplyDelivery.None && service.Cash >= offer.Price;
                if (DrawButton(new Rect(panel.xMax - 128 * scale, y + 5 * scale, 110 * scale, 34 * scale),
                    $"Order  ${offer.Price}"))
                    Purchase(row);
                GUI.enabled = true;
            }
        }
    }
}
