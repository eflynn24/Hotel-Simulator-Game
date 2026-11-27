using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte PurchaseAction = 4;
        private bool computerOpen;
        private int computerTab = ReservationsTab;
        private const int ReservationsTab = 1;

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
            scale = Mathf.Min(scale, screen.height / 580f, screen.width / 880f);
            Fill(screen, new Color(0, 0, 0, .58f));
            var panel = new Rect((screen.width - 850 * scale) / 2, (screen.height - 554 * scale) / 2,
                850 * scale, 554 * scale);
            DeskFrame(panel, DeskShell, DeskGold, 10 * scale, 2 * scale);
            float x = panel.x + 16 * scale;
            float width = panel.width - 32 * scale;
            var heading = new Rect(x + 33 * scale, panel.y + 6 * scale, width - 275 * scale, 39 * scale);
            DrawComputerIcon(new Rect(x, panel.y + 13 * scale, 23 * scale, 23 * scale), 5, DeskPriority);
            computerHeading ??= new GUIStyle(title)
            {
                font = Resources.Load<Font>("Fonts/DejaVuSerif-Bold"),
                fontStyle = FontStyle.Normal,
                padding = new RectOffset(0, 0, 0, 0)
            };
            computerHeading.fontSize = Mathf.RoundToInt(30 * scale);
            computerHeading.normal.textColor = DeskPaper;
            DrawFittedText(heading, "Reception Computer", computerHeading);
            var property = new Rect(panel.xMax - 244 * scale, panel.y + 12 * scale, 179 * scale, 28 * scale);
            DeskFrame(property, DeskCard, DeskTrim, 5 * scale, 1);
            DrawComputerIcon(new Rect(property.x + 10 * scale, property.y + 6 * scale, 16 * scale, 16 * scale), 4, DeskPriority);
            DrawFittedText(new Rect(property.x + 34 * scale, property.y, property.width - 40 * scale, property.height),
                "Desert Lantern Motel", label);
            if (BookingButton(new Rect(panel.xMax - 51 * scale, property.y, 34 * scale, 28 * scale), "X", DeskSurface))
            {
                SetComputerVisible(false);
                return;
            }

            Fill(new Rect(x, panel.y + 48 * scale, width, scale), DeskTrim);
            float tabWidth = (width - 6 * scale * (ComputerTabs.Length - 1)) / ComputerTabs.Length;
            for (int tab = 0; tab < ComputerTabs.Length; tab++)
            {
                var tabRect = new Rect(x + (tabWidth + 6 * scale) * tab, panel.y + 57 * scale, tabWidth, 33 * scale);
                bool active = tab == computerTab;
                if (BookingButton(tabRect, ComputerTabs[tab], active ? DeskPriority : DeskSurface, tab))
                    computerTab = tab;
            }
            Fill(new Rect(x, panel.y + 98 * scale, width, scale), DeskTrim);

            computerTheme = true;
            try
            {
                if (computerTab == ReservationsTab)
                {
                    DrawReservationWorkspace(new Rect(x, panel.y + 106 * scale, width, 430 * scale), scale);
                    return;
                }

                var card = new Rect(x, panel.y + 106 * scale, width, 430 * scale);
                DeskFrame(card, DeskCard, DeskTrim, 9 * scale, 1);
                Rect body = InsetDesk(card, 13 * scale);
                DrawFittedText(new Rect(body.x, body.y, body.width, 31 * scale), ComputerTabs[computerTab], detail);
                Fill(new Rect(body.x, body.y + 35 * scale, body.width, scale), DeskTrim);
                panel.y = body.y - 61 * scale;
                switch (computerTab)
                {
                    case 0: DrawSupplyRows(panel, body.x, body.width, scale); break;
                    case 2: DrawStaff(panel, body.x, body.width, scale); break;
                    case 3: DrawFinancialReport(panel, body.x, body.width, scale); break;
                    case 4: DrawExpansionOffice(panel, body.x, body.width, scale); break;
                }
            }
            finally
            {
                computerTheme = false;
            }
        }

        private bool computerTheme;
        private GUIStyle computerHeading;
        private static readonly string[] ComputerTabs =
            { "Supplies", "Bookings", "Staff", "Reports", "Expansion" };

        private const byte BookRoomAction = 18;
        private const byte EditBookingAction = 19;
        private const byte DeskCheckInAction = 28;
        private const byte DeskCheckOutAction = 29;
        private const byte ConfirmReservationAction = 30;
        private bool deskCheckInApproved;

        private void OpenDeskReservations()
        {
            computerTab = ReservationsTab;
            MotelService service = Service;
            if (GuestWaiting && service.ArrivalReady(Occupied))
            {
                reservationView = ReservationView.Arrivals;
                SelectBooking(service, FindArrivalBooking(service));
            }
            else if (service.CheckoutWaiting() is int departing && departing != NoRoom)
            {
                reservationView = ReservationView.Departures;
                SelectStay(departing);
            }
            else if (service.FirstPendingReservation is int pending && pending >= 0)
            {
                reservationView = ReservationView.Requests;
                SelectBooking(service, pending);
            }
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

        private const int PhoneArrivalLead = 120;
        private int bookingRoom;
        private int bookingLead;
        private int bookingNights = 1;
        private BookingPreference bookingPreference;
        private int calendarOffset;
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

        private static readonly string[] StaffNames = CountyPortfolio.StaffRoles;
        private Vector2 staffScroll;

        private void DrawStaff(Rect panel, float x, float width, float scale)
        {
            MotelService service = Service;
            DrawDeskSummary(new Rect(x, panel.y + 108 * scale, width, 30 * scale), scale,
                $"Cash ${service.Cash}    ·    ${MotelService.StaffWage}/day per employee");
            var viewport = new Rect(x, panel.y + 145 * scale, width, 270 * scale);
            float rowWidth = width - 29 * scale;
            staffScroll = GUI.BeginScrollView(viewport, staffScroll, new Rect(0, 0, rowWidth, StaffNames.Length * 45 * scale));
            x = 0;
            width = rowWidth;
            for (int role = 0; role < StaffNames.Length; role++)
            {
                float y = role * 45 * scale;
                bool hired = service.StaffHired(role);
                DeskFrame(new Rect(x, y, width, 37 * scale), DeskSurface, DeskTrim, 4 * scale, scale);
                DrawFittedText(new Rect(x + 8 * scale, y, width - 280 * scale, 34 * scale), StaffNames[role], detail);
                DrawFittedText(new Rect(x + width - 272 * scale, y, 92 * scale, 34 * scale),
                    hired ? "On staff" : "Vacancy", label);
                GUI.enabled = hired;
                if (DrawButton(new Rect(x + width - 174 * scale, y, 84 * scale, 34 * scale),
                    service.StaffUrgentOnly(role) ? "Urgent only" : "All tasks"))
                    RequestOperations(StaffAction, new Vector3(role, 1, 0));
                GUI.enabled = hired || service.Cash >= MotelService.BellhopCost;
                if (BookingButton(new Rect(x + width - 84 * scale, y, 81 * scale, 34 * scale),
                    hired ? "Dismiss" : $"Hire ${MotelService.BellhopCost}", hired ? DeskDanger : DeskPriority))
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
            DrawDeskSummary(new Rect(x, panel.y + 105 * scale, width - 130 * scale, 28 * scale), scale,
                $"Cash ${service.Cash}    ·    {service.PendingOrderCount} open orders");
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
                new Rect(0, 0, width - 29 * scale, MotelService.Catalog.Length * 46 * scale));
            x = 0;
            width -= 29 * scale;
            for (int item = 0; item < MotelService.Catalog.Length; item++)
            {
                SupplyOffer offer = MotelService.Catalog[item];
                int quantity = MotelService.CartQuantity(supplyCart, item);
                int step = 1 << (4 * item);
                float y = 46 * item * scale;
                DeskFrame(new Rect(x, y, width, 44 * scale), DeskSurface, DeskTrim, 4 * scale, scale);
                DrawFittedText(new Rect(x + 8 * scale, y, width - 160 * scale, 25 * scale),
                    $"{offer.Name} ×{offer.Quantity} · ${offer.Price}", detail);
                int stock = service.Stock(item);
                DrawFittedText(new Rect(x + 8 * scale, y + 20 * scale, width - 158 * scale, 23 * scale),
                    stock <= 1 ? $"Low stock · {stock} left" : $"{stock} in stock", label);
                GUI.enabled = quantity > 0;
                if (DrawButton(new Rect(x + width - 135 * scale, y + 3 * scale, 34 * scale, 29 * scale), "-"))
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
            if (BookingButton(new Rect(x, panel.y + 380 * scale, width, 33 * scale), $"Place order · ${cost}", DeskPriority))
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
