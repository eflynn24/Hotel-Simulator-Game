using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte PurchaseAction = 4;
        private bool computerOpen;
        private int computerTab = ReservationsTab;
        private const int ReservationsTab = 1;
        private bool viewingCalendar;

        private static bool AtComputer(Vector3 position) =>
            InsideOffice(position) && position.z >= 7.98f &&
            MotelWalker.Distance(position, MotelWorld.ComputerApproach) < .96f;

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

        private void DrawReceptionComputer(Rect screen, float scale)
        {
            scale = Mathf.Min(scale, screen.height / 580f);
            bool reservations = computerTab == ReservationsTab;
            Rect panel = DrawOverlay(screen, (reservations ? 553 : 473) * scale, scale, 590);
            float x = panel.x + 18 * scale;
            float width = panel.width - 36 * scale;
            DrawFittedText(new Rect(x, panel.y + 9 * scale, width, 47 * scale), "Reception computer", title);
            float tabWidth = (width - 6 * scale * (ComputerTabs.Length - 1)) / ComputerTabs.Length;
            float gap = tabWidth + 6 * scale;
            for (int tab = 0; tab < ComputerTabs.Length; tab++)
            {
                Rect tabRect = new Rect(x + gap * tab, panel.y + 61 * scale,
                    tabWidth, 29 * scale);
                if (DrawButton(tabRect, ComputerTabs[tab]))
                    computerTab = tab;
            }

            if (reservations)
            {
                float half = width / 2;
                if (DrawButton(new Rect(x, panel.y + 98 * scale, half - 3 * scale, 29 * scale),
                    viewingDecisions ? "Reservations" : viewingCalendar ? "Manage reservations" : "Room calendar"))
                {
                    if (viewingDecisions)
                        viewingDecisions = false;
                    else
                        viewingCalendar = !viewingCalendar;
                }
                if (DrawButton(new Rect(x + half + 3 * scale, panel.y + 98 * scale, half - 3 * scale, 29 * scale),
                    Service.PendingDecision == ManagementDecision.None ? "Decisions" : "Decision waiting"))
                    viewingDecisions = !viewingDecisions;
                Rect body = panel;
                body.y += (viewingCalendar ? 25 : 26) * scale;
                if (viewingDecisions)
                    DrawDecisions(panel, x, width, scale);
                else if (viewingCalendar)
                    DrawRoomCalendar(body, x, width, scale);
                else
                    DrawReservationRows(body, x, width, scale);
                DrawDeskActions(new Rect(x, panel.y + 426 * scale, width, 69 * scale), scale);
            }
            else if (computerTab == 2)
                DrawStaff(panel, x, width, scale);
            else if (computerTab == 3)
                DrawFinancialReport(panel, x, width, scale);
            else if (computerTab == 4)
                DrawExpansionOffice(panel, x, width, scale);
            else
                DrawSupplyRows(panel, x, width, scale);

            if (DrawButton(new Rect(x, panel.yMax - 49 * scale, width, 35 * scale), "Close computer"))
                SetComputerVisible(false);
        }

        private static readonly string[] ComputerTabs =
            { "Supplies", "Reservations", "Staff", "Reports", "Expansion" };

        private const byte BookRoomAction = 18;
        private const byte EditBookingAction = 19;
        private const byte DeskCheckInAction = 28;
        private const byte DeskCheckOutAction = 29;
        private const byte ConfirmReservationAction = 30;
        private bool deskCheckInApproved;

        private void OpenDeskReservations()
        {
            computerTab = ReservationsTab;
            viewingCalendar = viewingDecisions = false;
            int pending = Service.FirstPendingReservation;
            if (pending >= 0)
                selectedBooking = pending;
            SetComputerVisible(true);
        }

        private void HandleDeskAction(byte action, Vector3 data, Vector3 point)
        {
            if (!AtComputer(point) || data.x < 0 || data.x >= Service.RoomCapacity ||
                data.x != Mathf.Floor(data.x))
                return;

            int room = (int)data.x;
            MotelService service = Service;
            if (action == DeskCheckInAction)
            {
                if (GuestWaiting && room == Occupied && service.ArrivalReady(room) &&
                    service.BookingStart(room) == data.y && service.BookingEnd(room) == data.z)
                {
                    deskCheckInApproved = true;
                    guestDeskMinute = service.ClockMinute;
                }
            }
            else if (action == DeskCheckOutAction && room == visitorRoom && VisitorAtDesk &&
                service.TryDeskCheckout(room, data.y, data.z))
                Service = service;
            else if (action == ConfirmReservationAction)
                for (int index = 0; index < service.ReservationCount; index++)
                    if (service.ReservationRoom(index) == room && service.ReservationStart(index) == data.y &&
                        service.ReservationEnd(index) == data.z && service.AcceptReservation(index, (int)data.y))
                    {
                        Service = service;
                        bookingFeedback = "Reservation confirmed";
                        break;
                    }
        }

        private void DrawDeskActions(Rect area, float scale)
        {
            MotelService service = Service;
            int checkout = service.CheckoutWaiting();
            float gap = 8 * scale;
            float width = (area.width - gap) / 2;
            var arrival = new Rect(area.x, area.y, width, 31 * scale);
            var departure = new Rect(arrival.xMax + gap, area.y, width, arrival.height);
            bool ready = GuestWaiting && service.ArrivalReady(Occupied);
            string arrivalText = ready ? $"Room {Occupied + 1} · {GuestNames[Occupied]}" : "No guest waiting to check in";
            bool checkoutReady = checkout != NoRoom && visitorRoom == checkout && VisitorAtDesk;
            string departureText = checkout == NoRoom ? "No checkout due" : checkoutReady
                ? $"Room {checkout + 1} · {GuestNames[checkout]}" : $"Room {checkout + 1} · Coming to reception";
            DrawFittedText(arrival, arrivalText, label);
            DrawFittedText(departure, departureText, label);
            arrival.y += 32 * scale;
            departure.y += 32 * scale;
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && ready && !deskCheckInApproved;
            if (DrawButton(arrival, deskCheckInApproved && ready ? "Give the guest their room key" : "Check in"))
                RequestBookingAction(DeskCheckInAction, new Vector3(Occupied, service.BookingStart(Occupied), service.BookingEnd(Occupied)));
            GUI.enabled = enabled && checkoutReady;
            if (DrawButton(departure, "Check out"))
                RequestBookingAction(DeskCheckOutAction, new Vector3(checkout, service.DepartureMinute(checkout), service.GuestId(checkout)));
            GUI.enabled = enabled;
        }
        private const int PhoneArrivalLead = 120;
        private int bookingRoom;
        private int bookingLead;
        private int bookingNights = 1;
        private BookingPreference bookingPreference;
        private int calendarOffset;
        private int selectedBooking;
        private string bookingFeedback;

        private void ChangeBooking(byte action, Vector3 data, Vector3 point, ulong worker)
        {
            MotelService service = Service;
            if (action == BookRoomAction)
            {
                bool phone = data.z < 0;
                if (phone ? !AtPhone(point) : !AtComputer(point))
                    return;
                int room = (int)data.x;
                int lead = (int)data.y % 8;
                var preference = (BookingPreference)((int)data.y / 8);
                int nights = Mathf.Abs((int)data.z);
                if (room < 0 || room >= Guests.Length || lead < 0 || (int)preference < 0 || (int)preference > 3 || nights < 1 || nights > 7)
                    return;
                int start = service.ClockMinute + lead * MotelService.MinutesPerDay +
                    (phone ? PhoneArrivalLead : MotelService.ScheduleStep);
                int end = start + nights * MotelService.MinutesPerDay;
                bool booked = !((guest != null || vehicleArriving) && room == Occupied) && (phone
                    ? service.QueuePhoneReservation(room, start, end, preference, worker)
                    : !service.IsBroken(0, Fixture.Router) &&
                        service.BookRoom(service.RoomReference(room), start, end, BookingChannel.Online, preference));
                bookingFeedback = booked ? phone ? "Phone request sent to Reservations" : "Reservation confirmed"
                    : "Room unavailable for those dates";
            }
            else
            {
                if (!AtComputer(point))
                    return;
                int index = (int)data.x;
                int operation = (int)data.y;
                if (operation == 6 && index >= 0 && index < Guests.Length)
                    service.ExtendStay(index);
                else if (index >= 0 && index < service.ReservationCount &&
                    operation >= 0 && operation <= 5 && service.ReservationStart(index) == (int)data.z &&
                    ((guest == null && !vehicleArriving) || (service.ReservationRoom(index) != Occupied &&
                        (operation != 5 || (service.ReservationRoom(index) + 1) % service.RoomCapacity != Occupied))))
                    service.ChangeReservation(service.ReservationReference(index), operation);
            }
            Service = service;
        }

        private void RequestBookingAction(byte action, Vector3 data)
        {
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(action, data);
            else
            {
                if (action == DeskCheckInAction || action == DeskCheckOutAction || action == ConfirmReservationAction)
                    HandleDeskAction(action, data, caretaker.Position);
                else
                    ChangeBooking(action, data, caretaker.Position, LocalWorker);
            }
        }

        private void DrawBookingForm(Rect area, float scale, bool phone)
        {
            float w = area.width / 3;
            if (DrawButton(new Rect(area.x, area.y, w - 4 * scale, 29 * scale), $"Room {bookingRoom + 1}"))
                bookingRoom = (bookingRoom + 1) % Service.RoomCapacity;
            if (DrawButton(new Rect(area.x + w, area.y, w - 4 * scale, 29 * scale), $"In {bookingLead} days"))
                bookingLead = (bookingLead + 1) % 8;
            if (DrawButton(new Rect(area.x + 2 * w, area.y, w, 29 * scale), $"{bookingNights} nights"))
                bookingNights = bookingNights % 7 + 1;
            if (DrawButton(new Rect(area.x, area.y + 35 * scale, w, 29 * scale), phone ? "Send request" : "Accept reservation"))
                RequestBookingAction(BookRoomAction, new Vector3(bookingRoom, bookingLead + 8 * (int)bookingPreference, phone ? -bookingNights : bookingNights));
            if (DrawButton(new Rect(area.x + w + 5 * scale, area.y + 35 * scale, 2 * w - 5 * scale, 29 * scale),
                MotelService.PreferenceLabel(bookingPreference)))
                bookingPreference = (BookingPreference)(((int)bookingPreference + 1) % 4);
            int arrival = Service.ClockMinute + bookingLead * MotelService.MinutesPerDay +
                (phone ? PhoneArrivalLead : MotelService.ScheduleStep);
            string status = bookingFeedback ?? $"Arrival · {MotelService.DisplayTime(arrival)}";
            DrawFittedText(new Rect(area.x, area.y + 66 * scale, area.width, 22 * scale), status, label);
        }

        private void DrawReservationRows(Rect panel, float x, float width, float scale)
        {
            DrawBookingForm(new Rect(x, panel.y + 102 * scale, width, 65 * scale), scale, false);
            MotelService service = Service;
            if (service.ReservationCount == 0)
                return;
            selectedBooking = Mathf.Clamp(selectedBooking, 0, service.ReservationCount - 1);
            int index = selectedBooking;
            float y = panel.y + 191 * scale;
            bool pending = service.ReservationPending(index);
            string status = service.ReservationChannel(index) + (pending ? " · Awaiting confirmation" : " · Confirmed");
            if (DrawButton(new Rect(x, y, width, 28 * scale),
                $"Booking {index + 1}/{service.ReservationCount} · Room {service.ReservationRoom(index) + 1} · {status}"))
                selectedBooking = (index + 1) % service.ReservationCount;
            DrawFittedText(new Rect(x, y + 31 * scale, width, 25 * scale),
                $"In {MotelService.DisplayTime(service.ReservationStart(index))} · Out {MotelService.DisplayTime(service.ReservationEnd(index))}", label);
            DrawFittedText(new Rect(x, y + 55 * scale, width, 22 * scale),
                $"{MotelService.PreferenceLabel(service.ReservationPreference(index))} · ${service.ReservationRate(index)}/night", label);
            float col = width / 3;
            if (pending && DrawButton(new Rect(x, y + 142 * scale, width, 28 * scale), "Confirm reservation"))
                RequestBookingAction(ConfirmReservationAction, new Vector3(service.ReservationRoom(index),
                    service.ReservationStart(index), service.ReservationEnd(index)));
            for (int operation = 0; operation < BookingOperations.Length; operation++)
                if (DrawButton(new Rect(x + col * (operation % 3), y + (78 + operation / 3 * 32) * scale,
                    col - 4 * scale, 28 * scale), BookingOperations[operation]))
                    RequestBookingAction(EditBookingAction, new Vector3(index, operation, service.ReservationStart(index)));
        }

        private static readonly string[] BookingOperations =
            { "Cancel", "In +15 min", "In −15 min", "Add night", "Remove night", "Next room" };

        private void DrawRoomCalendar(Rect panel, float x, float width, float scale)
        {
            MotelService service = Service;
            float y = panel.y + 100 * scale;
            if (DrawButton(new Rect(x, y, 64 * scale, 26 * scale), "‹ Week"))
                calendarOffset = Mathf.Max(0, calendarOffset - 7);
            if (DrawButton(new Rect(x + width - 64 * scale, y, 64 * scale, 26 * scale), "Week ›"))
                calendarOffset = Mathf.Min(7, calendarOffset + 7);
            DrawFittedText(new Rect(x + 77 * scale, y, width - 154 * scale, 26 * scale),
                MotelService.DisplayTime(service.ClockMinute), label);

            int first = service.ClockMinute / MotelService.MinutesPerDay + calendarOffset;
            float column = (width - 116 * scale) / 7;
            for (int day = 0; day < 7; day++)
                DrawFittedText(new Rect(x + 51 * scale + day * column, y + 33 * scale, column, 24 * scale),
                    (first + day + 1).ToString(), label);
            float roomRow = service.RoomCapacity > MotelService.BaseRoomCount ? 15 : 22;
            for (int room = 0; room < service.RoomCapacity; room++)
            {
                float rowY = y + (58 + room * roomRow) * scale;
                DrawFittedText(new Rect(x, rowY, 52 * scale, roomRow * scale), $"Rm {room + 1}", label);
                for (int day = 0; day < 7; day++)
                {
                    int start = (first + day) * MotelService.MinutesPerDay;
                    bool free = service.RoomAvailable(room, start, start + MotelService.MinutesPerDay);
                    var cell = new Rect(x + 51 * scale + day * column, rowY + 3 * scale, column - 4 * scale, (roomRow - 6) * scale);
                    Fill(cell, free ? new Color(.22f, .31f, .25f) : new Color(.66f, .38f, .16f));
                }
                if ((service.ActiveRooms & (1 << room)) != 0 &&
                    DrawButton(new Rect(x + width - 63 * scale, rowY, 63 * scale, roomRow * scale), "+1 night"))
                    RequestBookingAction(EditBookingAction, new Vector3(room, 6, 0));
            }
            DrawFittedText(new Rect(x, y + 239 * scale, width, 24 * scale), "Green · Available   Amber · Booked or occupied", label);
        }

        private static readonly string[] StaffNames = CountyPortfolio.StaffRoles;
        private Vector2 staffScroll;

        private void DrawStaff(Rect panel, float x, float width, float scale)
        {
            MotelService service = Service;
            DrawFittedText(new Rect(x, panel.y + 110 * scale, width, 30 * scale),
                $"Cash ${service.Cash} · ${MotelService.StaffWage}/day per employee", label);
            var viewport = new Rect(x, panel.y + 145 * scale, width, 270 * scale);
            float rowWidth = width - 19 * scale;
            staffScroll = GUI.BeginScrollView(viewport, staffScroll, new Rect(0, 0, rowWidth, StaffNames.Length * 45 * scale));
            x = 0;
            width = rowWidth;
            for (int role = 0; role < StaffNames.Length; role++)
            {
                float y = role * 45 * scale;
                bool hired = service.StaffHired(role);
                DrawFittedText(new Rect(x, y, width - 174 * scale, 34 * scale), StaffNames[role], detail);
                GUI.enabled = hired;
                if (DrawButton(new Rect(x + width - 174 * scale, y, 84 * scale, 34 * scale),
                    service.StaffUrgentOnly(role) ? "Urgent only" : "All tasks"))
                    RequestOperations(StaffAction, new Vector3(role, 1, 0));
                GUI.enabled = hired || service.Cash >= MotelService.BellhopCost;
                if (DrawButton(new Rect(x + width - 84 * scale, y, 84 * scale, 34 * scale),
                    hired ? "Dismiss" : $"Hire ${MotelService.BellhopCost}"))
                    RequestOperations(StaffAction, new Vector3(role, 0, 0));
                GUI.enabled = true;
            }
            GUI.EndScrollView();
        }

        private const byte CartOrderAction = 17;
        private int supplyCart;
        private bool orderHistory;
        private Vector2 orderScroll;
        private Vector2 supplyScroll;

        private void SubmitCart(int cart, Vector3 point)
        {
            MotelService service = Service;
            if ((AtComputer(point) || AtPhone(point)) && service.OrderCart(cart))
            {
                Service = service;
            }
        }

        private void DrawSupplyRows(Rect panel, float x, float width, float scale)
        {
            MotelService service = Service;
            DrawFittedText(new Rect(x, panel.y + 105 * scale, width - 125 * scale, 28 * scale),
                $"Cash ${service.Cash} · {service.PendingOrderCount} open orders", label);
            if (DrawButton(new Rect(x + width - 122 * scale, panel.y + 105 * scale, 122 * scale, 28 * scale),
                orderHistory ? "Shop" : "Track orders"))
                orderHistory = !orderHistory;

            if (orderHistory)
            {
                int lines = service.PendingOrderCount + service.CompletedOrderCount * 2;
                var viewport = new Rect(x, panel.y + 138 * scale, width, 272 * scale);
                orderScroll = GUI.BeginScrollView(viewport, orderScroll,
                    new Rect(0, 0, width - 20 * scale, Mathf.Max(viewport.height, lines * 27 * scale)));
                int line = 0;
                if (service.PendingOrderCount > 0)
                {
                    string status = service.Delivery == SupplyDelivery.InTransit
                        ? $"Due {MotelService.DisplayTime(service.DeliveryDue)}" : service.Delivery == SupplyDelivery.AtBay
                        ? "Ready at the bay" : "Being carried to storage";
                    DrawFittedText(new Rect(0, line++ * 27 * scale, width, 27 * scale),
                        $"#{service.ActiveOrderNumber} · {status}", label);
                }
                for (int order = 0; order < service.PendingOrderCount - 1; order++)
                {
                    int due = service.QueuedOrderDue(order);
                    DrawFittedText(new Rect(0, line++ * 27 * scale, width, 27 * scale),
                        $"#{service.QueuedOrderNumber(order)} · {(due <= service.ClockMinute ? "Waiting at the bay" : MotelService.DisplayTime(due))}", label);
                }
                for (int order = service.CompletedOrderCount - 1; order >= 0; order--)
                {
                    int cart = service.CompletedCart(order);
                    DrawFittedText(new Rect(0, line++ * 27 * scale, width, 27 * scale),
                        $"#{service.CompletedOrder(order)} · Stocked", label);
                    int products = 0;
                    int units = 0;
                    for (int item = 0; item < MotelService.Catalog.Length; item++)
                    {
                        int packs = MotelService.CartQuantity(cart, item);
                        if (packs > 0)
                            products++;
                        units += packs * MotelService.Catalog[item].Quantity;
                    }
                    DrawFittedText(new Rect(0, line++ * 27 * scale, width, 27 * scale),
                        $"   {products} products · {units} units", label);
                }
                GUI.EndScrollView();
                return;
            }

            var catalogArea = new Rect(x, panel.y + 138 * scale, width, 233 * scale);
            supplyScroll = GUI.BeginScrollView(catalogArea, supplyScroll,
                new Rect(0, 0, width - 20 * scale, MotelService.Catalog.Length * 39 * scale));
            x = 0;
            width -= 20 * scale;
            for (int item = 0; item < MotelService.Catalog.Length; item++)
            {
                SupplyOffer offer = MotelService.Catalog[item];
                int quantity = MotelService.CartQuantity(supplyCart, item);
                int step = 1 << (4 * item);
                float y = 39 * item * scale;
                DrawFittedText(new Rect(x, y, width - 152 * scale, 24 * scale),
                    $"{offer.Name} ×{offer.Quantity} · ${offer.Price}", detail);
                int stock = service.Stock(item);
                DrawFittedText(new Rect(x, y + 19 * scale, width - 150 * scale, 19 * scale),
                    stock <= 1 ? $"Low stock · {stock} left" : $"{stock} in stock", label);
                GUI.enabled = quantity > 0;
                if (DrawButton(new Rect(x + width - 135 * scale, y + 3 * scale, 34 * scale, 29 * scale), "−"))
                    supplyCart -= step;
                GUI.enabled = true;
                DrawFittedText(new Rect(x + width - 89 * scale, y + 3 * scale, 40 * scale, 29 * scale),
                    quantity.ToString(), label);
                GUI.enabled = item == 4 ? quantity == 0 && stock == 0 : quantity < MotelService.MaxCartQuantity;
                if (DrawButton(new Rect(x + width - 35 * scale, y + 3 * scale, 35 * scale, 29 * scale), "+"))
                    supplyCart += step;
                GUI.enabled = true;
            }

            GUI.EndScrollView();
            x = catalogArea.x;
            width = catalogArea.width;
            int cost = MotelService.CartCost(supplyCart);
            GUI.enabled = cost > 0 && cost <= service.Cash && service.PendingOrderCount < 9;
            if (DrawButton(new Rect(x, panel.y + 380 * scale, width, 33 * scale), $"Place order · ${cost}"))
            {
                if (lan != null && !lan.IsHost)
                {
                    lan.State?.ActionRpc(CartOrderAction, new Vector3(supplyCart & 65535, (uint)supplyCart >> 16, 0));
                    supplyCart = 0;
                }
                else
                {
                    int cash = Service.Cash;
                    SubmitCart(supplyCart, caretaker.Position);
                    if (Service.Cash != cash)
                        supplyCart = 0;
                }
            }
            GUI.enabled = true;
        }
    }
}
