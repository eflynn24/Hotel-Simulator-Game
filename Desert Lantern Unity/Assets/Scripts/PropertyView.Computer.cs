using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte PurchaseAction = 4;
        private const byte ConfirmBookingAction = 5;
        private const byte CancelBookingAction = 6;
        private bool computerOpen;
        private const byte AdjustScheduleAction = 8;
        private int computerTab;

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
            if (AtComputer(position) && service.Order(offer))
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

        private void UpdateSchedule(int room, bool end, int direction, Vector3 position)
        {
            if (!AtComputer(position) || (room == Occupied && guest != null))
                return;
            MotelService service = Service;
            service.AdjustSchedule(room, end, direction * MotelService.ScheduleStep);
            Service = service;
        }

        private void RequestSchedule(int room, bool end, int direction)
        {
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(AdjustScheduleAction, new Vector3(room, end ? 1 : 0, direction));
            else
                UpdateSchedule(room, end, direction, caretaker.Position);
        }

        private void DrawReceptionComputer(Rect screen, float scale)
        {
            Rect panel = DrawOverlay(screen, 473 * scale, scale);
            float x = panel.x + 18 * scale;
            float width = panel.width - 36 * scale;
            GUI.Label(new Rect(x, panel.y + 9 * scale, width, 47 * scale), "Reception computer", title);
            float tabWidth = (width - 12 * scale) / 3;
            float gap = tabWidth + 6 * scale;
            for (int tab = 0; tab < ComputerTabs.Length; tab++)
            {
                Rect tabRect = new Rect(x + gap * (tab % 3), panel.y + (61 + 32 * (tab / 3)) * scale,
                    tabWidth, 29 * scale);
                if (DrawButton(tabRect, ComputerTabs[tab]))
                    computerTab = tab;
            }

            Rect body = panel;
            body.y += 34 * scale;
            switch (computerTab)
            {
                case 1: DrawReservationRows(body, x, width, scale, false); break;
                case 2: DrawReservationRows(body, x, width, scale, true); break;
                case 3: DrawGuestRequests(body, x, width, scale); break;
                case 4: DrawStaff(body, x, width, scale); break;
                case 5: DrawReviews(body, x, width, scale); break;
                default: DrawSupplyRows(body, x, width, scale); break;
            }

            if (DrawButton(new Rect(x, panel.y + 424 * scale, width, 35 * scale), "Close computer"))
                SetComputerVisible(false);
        }

        private static readonly string[] ComputerTabs =
            { "Supplies", "Reservations", "Booked", "Guests", "Staff", "Reviews" };

        private void DrawReservationRows(Rect panel, float x, float width, float scale, bool booked)
        {
            MotelService service = Service;
            GUI.Label(new Rect(x, panel.y + 105 * scale, width, 27 * scale),
                $"{MotelService.DisplayTime(service.ClockMinute)}  ·  ${MotelService.RoomRate} per night", label);
            int rows = 0;
            for (int room = 1; room <= 2; room++)
            {
                int bit = 1 << room;
                bool checkedIn = (service.UnlockedDoors & bit) != 0;
                bool arrived = room == Occupied && guest != null;
                bool reserved = service.HasReservation(room);
                bool cancelled = service.ReservationCancelled(room);
                if (booked != reserved || cancelled || (!booked && checkedIn))
                    continue;
                float y = panel.y + (136 + rows++ * 121) * scale;
                string status = checkedIn ? ((service.ActiveRooms & bit) != 0 ? "Checked in" : "Checked out")
                    : arrived ? "At reception" : service.ClockMinute >= service.BookingStart(room) ? "Due now"
                    : reserved ? "Booked" : "Awaiting confirmation";
                bool editable = !checkedIn && !arrived;
                string guestName = Guests[room].Substring("Guest-".Length);
                GUI.Label(new Rect(x, y, width, 28 * scale), $"{guestName}  ·  Room {room + 1}", detail);
                GUI.Label(new Rect(x, y + 29 * scale, width, 21 * scale), status, label);
                DrawBookingTime(room, false, service.BookingStart(room), editable,
                    new Rect(x, y + 53 * scale, width, 27 * scale), scale);
                DrawBookingTime(room, true, service.BookingEnd(room), editable,
                    new Rect(x, y + 84 * scale, width, 27 * scale), scale);

                float right = x + width;
                if (editable && !reserved &&
                    DrawButton(new Rect(right - 183 * scale, y + 4 * scale, 90 * scale, 32 * scale), "Confirm"))
                    RequestBooking(room, true);
                if (editable && DrawButton(new Rect(right - 88 * scale, y + 4 * scale, 88 * scale, 32 * scale),
                    reserved ? "Cancel" : "Decline"))
                    RequestBooking(room, false);
            }
            if (rows == 0)
                GUI.Label(new Rect(x, panel.y + 145 * scale, width, 32 * scale),
                    booked ? "No booked stays." : "No reservation requests.", detail);
        }

        private void DrawGuestRequests(Rect panel, float x, float width, float scale)
        {
            MotelService service = Service;
            float y = panel.y + 108 * scale;
            if (service.ActiveRooms == 0)
                GUI.Label(new Rect(x, y, width, 35 * scale), "Guest requests appear during a stay.", detail);
            for (int room = 0; room < Guests.Length; room++)
            {
                int bit = 1 << room;
                if ((service.ActiveRooms & bit) == 0)
                    continue;
                string request = service.RequestOpen(room) ? "Needs a towel"
                    : (service.FulfilledRequests & bit) != 0 ? "Towel delivered" : "No open requests";
                GUI.Label(new Rect(x, y, width, 26 * scale), $"Room {room + 1}  ·  {request}", detail);
                GUI.enabled = (service.TvPackages & bit) == 0;
                if (DrawButton(new Rect(x, y + 27 * scale, 135 * scale, 29 * scale),
                    GUI.enabled ? "TV access  $12" : "TV active"))
                    RequestGuestAction(SellUpgradeAction, room);
                GUI.enabled = (service.MealOrders & bit) == 0;
                if (DrawButton(new Rect(x + 144 * scale, y + 27 * scale, 170 * scale, 29 * scale),
                    GUI.enabled ? "Room service  $18" : "Meal ordered"))
                    RequestGuestAction(SellUpgradeAction, room + Guests.Length);
                GUI.enabled = true;
                y += 67 * scale;
            }
        }

        private void DrawStaff(Rect panel, float x, float width, float scale)
        {
            MotelService service = Service;
            GUI.Label(new Rect(x, panel.y + 111 * scale, width, 30 * scale), "Bellhop", detail);
            GUI.Label(new Rect(x, panel.y + 144 * scale, width, 48 * scale),
                "Collects suitcases from guest cars and delivers them to the assigned rooms.", label);
            GUI.enabled = !service.BellhopHired && service.Cash >= MotelService.BellhopCost;
            if (DrawButton(new Rect(x, panel.y + 205 * scale, 166 * scale, 38 * scale),
                service.BellhopHired ? "Bellhop on duty" : $"Hire  ${MotelService.BellhopCost}"))
                RequestGuestAction(HireBellhopAction);
            GUI.enabled = true;
        }

        private void DrawBookingTime(int room, bool end, int minute, bool editable, Rect row, float scale)
        {
            string text = $"{(end ? "Departure" : "Arrival")}  ·  {MotelService.DisplayTime(minute)}";
            GUI.Label(new Rect(row.x, row.y, row.width - 131 * scale, row.height), text, label);
            if (!editable)
                return;
            if (DrawButton(new Rect(row.xMax - 122 * scale, row.y, 59 * scale, row.height), "-15m"))
                RequestSchedule(room, end, -1);
            if (DrawButton(new Rect(row.xMax - 59 * scale, row.y, 59 * scale, row.height), "+15m"))
                RequestSchedule(room, end, 1);
        }

        private void DrawReviews(Rect panel, float x, float width, float scale)
        {
            MotelService service = Service;
            if (service.Reviews == 0)
            {
                GUI.Label(new Rect(x, panel.y + 108 * scale, width, 45 * scale),
                    "Reviews appear after checkout.", detail);
                return;
            }

            float y = panel.y + 110 * scale;
            for (int room = 0; room < Guests.Length; room++)
            {
                int bit = 1 << room;
                if ((service.Reviews & bit) == 0)
                    continue;
                bool punctual = (service.LateCheckIns & bit) == 0;
                bool luggage = (service.DeliveredBags & bit) != 0;
                bool checkout = (service.MissedCheckouts & bit) == 0;
                bool serviceSatisfied = (service.KnownRequests & bit) == 0 ||
                    (service.FulfilledRequests & bit) != 0;
                int rating = 1 + (punctual ? 1 : 0) + (luggage ? 1 : 0) + (checkout ? 1 : 0) +
                    (serviceSatisfied ? 1 : 0);
                GUI.Label(new Rect(x, y, width, 23 * scale), $"Room {room + 1}  ·  {rating}/5", detail);
                string text = $"{(punctual ? "Quick check-in." : "I waited to check in.")} " +
                    $"{(luggage ? "My luggage arrived." : "I handled my luggage.")} " +
                    $"{(checkout ? "Checkout was on time." : "I checked out on my own.")}" +
                    (serviceSatisfied ? "" : " My request went unanswered.");
                GUI.Label(new Rect(x, y + 23 * scale, width, 38 * scale), text, label);
                y += 65 * scale;
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
