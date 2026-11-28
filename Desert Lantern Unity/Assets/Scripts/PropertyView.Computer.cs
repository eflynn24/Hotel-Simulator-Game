using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte PurchaseAction = 4;
        private bool computerOpen;
        private const int OverviewTab = 0, ReservationsTab = 1, SuppliesTab = 2, TasksTab = 3,
            StaffTab = 4, ReportsTab = 5, ExpansionTab = 6;
        private int computerTab = OverviewTab;

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
                if (BookingButton(tabRect, ComputerTabs[tab], active ? DeskPriority : DeskSurface, TabIcons[tab]))
                {
                    computerTab = tab;
                    if (tab == TasksTab)
                        inspectedTask = null;
                }
            }
            Fill(new Rect(x, panel.y + 98 * scale, width, scale), DeskTrim);

            computerTheme = true;
            try
            {
                var workspace = new Rect(x, panel.y + 106 * scale, width, 430 * scale);
                if (computerTab == OverviewTab)
                {
                    DrawOperationsOverview(workspace, scale);
                    return;
                }
                if (computerTab == ReservationsTab)
                {
                    DrawReservationWorkspace(workspace, scale);
                    return;
                }
                if (computerTab == TasksTab)
                {
                    DrawNotebook(InsetDesk(workspace, 8 * scale), scale);
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
                    case SuppliesTab: DrawSupplyRows(panel, body.x, body.width, scale); break;
                    case StaffTab: DrawStaff(panel, body.x, body.width, scale); break;
                    case ReportsTab: DrawFinancialReport(panel, body.x, body.width, scale); break;
                    case ExpansionTab: DrawExpansionOffice(panel, body.x, body.width, scale); break;
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
            { "Overview", "Bookings", "Supplies", "Tasks", "Staff", "Reports", "Expansion" };
        private static readonly int[] TabIcons = { 5, 1, 0, 5, 2, 3, 4 };

        private const byte BookRoomAction = 18;
        private const byte EditBookingAction = 19;
        private const byte DeskCheckInAction = 28;
        private const byte DeskCheckOutAction = 29;
        private const byte ConfirmReservationAction = 30;
        private const byte OnlineApprovalAction = 31;
        private const byte AcceptPhoneAction = 32;
        private bool deskCheckInApproved;

        private void OpenComputer(int tab = OverviewTab, bool trackOrders = false)
        {
            queuedComputerTab = -1;
            computerTab = tab;
            orderHistory = trackOrders;
            if (tab == TasksTab)
                inspectedTask = null;
            SetComputerVisible(true);
        }

        private void OpenDeskReservations()
        {
            OpenComputer(ReservationsTab);
            MotelService service = Service;
            if (GuestWaiting && service.DeskArrivalReady(Occupied))
            {
                reservationView = ReservationView.Arrivals;
                SelectBooking(service, FindArrivalBooking(service));
            }
            else if (service.CheckoutWaiting() is int departing && departing != NoRoom)
            {
                reservationView = ReservationView.Departures;
                SelectStay(departing);
            }
            else
                reservationView = ReservationView.Calendar;
        }

        private void HandleDeskAction(byte action, Vector3 data, Vector3 point)
        {
            if (action == OnlineApprovalAction)
            {
                if (AtComputer(point) && (data.x == 0 || data.x == 1))
                {
                    MotelService settings = Service;
                    settings.SetOnlineApproval(data.x == 1);
                    Service = settings;
                }
                return;
            }

            if (!AtComputer(point) || data.x < 0 || data.x >= Service.RoomCapacity ||
                data.x != Mathf.Floor(data.x))
                return;

            int room = (int)data.x;
            MotelService service = Service;
            if (action == DeskCheckInAction)
            {
                if (GuestWaiting && room == Occupied && service.DeskArrivalReady(room) &&
                    service.BookingStart(room) == data.y && service.BookingEnd(room) == data.z &&
                    (service.BookingDue(room) || service.AcceptReservation(FindArrivalBooking(service), (int)data.y)))
                {
                    Service = service;
                    deskCheckInApproved = true;
                    guestDeskMinute = service.ClockMinute;
                }
            }
            else if (action == DeskCheckOutAction && room == visitorRoom && VisitorAtDesk &&
                service.TryDeskCheckout(room, data.y, data.z))
                Service = service;
            else if (action == ConfirmReservationAction)
                for (int index = 0; index < service.ReservationCount; index++)
                    if (service.ReservationChannel(index) != BookingChannel.WalkIn && service.ReservationRoom(index) == room && service.ReservationStart(index) == data.y &&
                        service.ReservationEnd(index) == data.z && service.AcceptReservation(index, (int)data.y))
                    {
                        Service = service;
                        bookingFeedback = "Reservation confirmed";
                        break;
                    }
        }

        private int bookingRoom = NoRoom;
        private int bookingStart = -1;
        private int bookingNights = 1;
        private BookingPreference bookingPreference;
        private int calendarOffset;
        private string bookingFeedback;
        private string reservationEditFeedback;

        private void ReceiveBookingResult(byte action, bool success, Vector3 selection)
        {
            if (action == QuickOrderAction)
            {
                quickOrderPending = false;
                if (reorderItem == (int)selection.x)
                    quickOrderStatus = success ? "Order placed · Pick up at the delivery bay" : "Order could not be placed";
            }
            else if (action == BookRoomAction)
            {
                bookingFeedback = success ? "Reservation confirmed" : "Room unavailable for those dates";
                if (success && reservationView == ReservationView.New)
                {
                    reservationView = ReservationView.Calendar;
                    selectedRoom = (int)selection.x;
                    selectedArrival = (int)selection.y;
                    editingReservation = confirmingCancellation = false;
                }
            }
            else if (action == AcceptPhoneAction)
                bookingFeedback = success ? "Telephone reservation confirmed" : "Room unavailable for those dates";
            else if (action == EditBookingAction)
            {
                if (success && selection.x >= 0 && selection.y >= 0)
                {
                    selectedRoom = (int)selection.x;
                    selectedArrival = (int)selection.y;
                }
                else if (success && selection.x < 0)
                    SelectStay(NoRoom);
                reservationEditFeedback = success ? "Reservation updated" : "Change could not be made. Check dates and availability.";
            }
        }

        private void SendBookingResult(ulong worker, byte action, bool success, Vector3 selection)
        {
            if (worker == LocalWorker)
                ReceiveBookingResult(action, success, selection);
            else
                lan?.State?.SendBookingResult(worker, action, success, selection);
        }

        private void ChangeBooking(byte action, Vector3 data, Vector3 point, ulong worker)
        {
            if (!Finite(data.x) || !Finite(data.y) || !Finite(data.z) ||
                data.x != Mathf.Floor(data.x) || data.y != Mathf.Floor(data.y) || data.z != Mathf.Floor(data.z) ||
                data.x < 0 || data.x >= 8 * MotelService.RoomCount || data.y < 0 || data.y > int.MaxValue / 2 ||
                data.z < 0 || data.z > int.MaxValue / 2)
                return;

            MotelService service = Service;
            int room = (int)data.x % MotelService.RoomCount;
            int option = (int)data.x / MotelService.RoomCount;
            bool success = false;
            Vector3 selection = new Vector3(room, data.y, data.z);
            if (action == AcceptPhoneAction)
            {
                if (AtPhone(point) && option == 0)
                    success = service.AcceptPhoneReservation(room, (int)data.y, worker);
            }
            else if (AtComputer(point))
            {
                if (action == BookRoomAction && option <= (int)BookingPreference.Toiletries)
                    success = service.BookRoom(service.RoomReference(room), (int)data.y, (int)data.z,
                        BookingChannel.Online, (BookingPreference)option);
                else if (action == EditBookingAction && option == 6 && service.RoomBuilt(room) &&
                    service.DepartureMinute(room) == data.y && service.GuestId(room) == data.z)
                {
                    selection.y = -1;
                    success = service.ExtendStay(room);
                }
                else if (action == EditBookingAction && option <= 5)
                {
                    for (int index = 0; index < service.ReservationCount; index++)
                    {
                        if (service.ReservationRoom(index) != room || service.ReservationStart(index) != data.y ||
                            service.ReservationEnd(index) != data.z || ((guest != null || vehicleArriving) && room == Occupied))
                            continue;

                        success = service.ChangeReservation(service.ReservationReference(index), option);
                        if (success)
                            selection = option == 0 ? new Vector3(NoRoom, -1, 0) :
                                new Vector3(service.ReservationRoom(index), service.ReservationStart(index), service.ReservationEnd(index));
                        break;
                    }
                }
            }
            if (success)
                Service = service;
            SendBookingResult(worker, action, success, selection);
        }

        private void RequestBookingAction(byte action, Vector3 data)
        {
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(action, data);
            else if (action == DeskCheckInAction || action == DeskCheckOutAction || action == ConfirmReservationAction ||
                action == OnlineApprovalAction)
                HandleDeskAction(action, data, caretaker.Position);
            else
                ChangeBooking(action, data, caretaker.Position, LocalWorker);
        }

        private static int CalendarArrival(int day, int now)
        {
            int afternoon = day * MotelService.MinutesPerDay + 15 * 60;
            return day == now / MotelService.MinutesPerDay
                ? Mathf.Max(afternoon, (now / MotelService.ScheduleStep + 2) * MotelService.ScheduleStep)
                : afternoon;
        }

        private void BeginBooking(MotelService service, int day, int room = NoRoom)
        {
            bookingStart = CalendarArrival(day, service.ClockMinute);
            bookingNights = 1;
            bookingPreference = BookingPreference.None;
            bookingRoom = room;
            bookingFeedback = null;
            reservationView = ReservationView.New;
        }

        private void DrawBookingForm(Rect area, float scale, MotelService service)
        {
            int today = service.ClockMinute / MotelService.MinutesPerDay;
            if (bookingStart < service.ClockMinute && bookingStart / MotelService.MinutesPerDay == today)
                bookingStart = CalendarArrival(today, service.ClockMinute);
            float width = area.width / 3;
            var row = new Rect(area.x, area.y, area.width, 30 * scale);
            if (DrawButton(new Rect(row.x, row.y, width - 5 * scale, row.height), "Previous day") &&
                bookingStart / MotelService.MinutesPerDay > service.ClockMinute / MotelService.MinutesPerDay)
                bookingStart = CalendarArrival(bookingStart / MotelService.MinutesPerDay - 1, service.ClockMinute);
            DrawFittedText(new Rect(row.x + width, row.y, width, row.height), MotelService.DisplayTime(bookingStart), detail);
            if (DrawButton(new Rect(row.x + 2 * width, row.y, width, row.height), "Next day") &&
                bookingStart / MotelService.MinutesPerDay < service.ClockMinute / MotelService.MinutesPerDay + 12)
                bookingStart = CalendarArrival(bookingStart / MotelService.MinutesPerDay + 1, service.ClockMinute);
            if (DrawButton(new Rect(row.x, row.y + 37 * scale, width - 5 * scale, row.height), $"{bookingNights} nights"))
                bookingNights = bookingNights % 7 + 1;
            int end = MotelService.DepartureForNights(bookingStart, bookingNights);
            if (bookingStart < service.ClockMinute || end > service.ClockMinute + 14 * MotelService.MinutesPerDay)
            {
                bookingRoom = NoRoom;
                DrawFittedText(new Rect(row.x + width, row.y + 37 * scale, width - 5 * scale, row.height),
                    "Dates outside booking window", label);
            }
            else
            {
                if (!service.RoomAvailable(bookingRoom, bookingStart, end))
                    bookingRoom = service.SuggestRoom(bookingStart, end);
                GUI.enabled = bookingRoom != NoRoom;
                if (DrawButton(new Rect(row.x + width, row.y + 37 * scale, width - 5 * scale, row.height),
                    bookingRoom == NoRoom ? "No room available" : $"Room {bookingRoom + 1}"))
                    bookingRoom = service.SuggestRoom(bookingStart, end, bookingRoom);
                GUI.enabled = true;
            }
            if (DrawButton(new Rect(row.x + 2 * width, row.y + 37 * scale, width, row.height),
                MotelService.PreferenceLabel(bookingPreference)))
                bookingPreference = (BookingPreference)(((int)bookingPreference + 1) % 4);
            string quote = bookingRoom == NoRoom ? "Choose dates with availability." :
                $"Check-out {MotelService.DisplayTime(end)} · ${service.NightlyRate(bookingRoom)}/night · ${service.NightlyRate(bookingRoom) * bookingNights} total";
            DrawFittedText(new Rect(row.x, row.y + 75 * scale, row.width, row.height), quote, detail);
            GUI.enabled = bookingRoom != NoRoom;
            if (BookingButton(new Rect(row.x, row.y + 112 * scale, row.width, row.height), "Confirm reservation", DeskPriority))
                RequestBookingAction(BookRoomAction, new Vector3(bookingRoom + (int)bookingPreference * MotelService.RoomCount, bookingStart, end));
            GUI.enabled = true;
            if (bookingFeedback != null)
                DrawFittedText(new Rect(row.x, row.y + 150 * scale, row.width, row.height), bookingFeedback, label);
        }

        private static readonly string[] StaffNames = CountyPortfolio.StaffRoles;
        private Vector2 staffScroll;
        private readonly System.Collections.Generic.List<OperationTask> employeeTasks = new System.Collections.Generic.List<OperationTask>();

        private void DrawStaff(Rect panel, float x, float width, float scale)
        {
            MotelService service = Service;
            service.ReadOperations(employeeTasks);
            DrawDeskSummary(new Rect(x, panel.y + 108 * scale, width, 30 * scale), scale,
                $"Cash ${service.Cash}    ·    ${MotelService.StaffWage}/day per employee");
            var viewport = new Rect(x, panel.y + 145 * scale, width, 270 * scale);
            float rowWidth = width - 29 * scale;
            staffScroll = GUI.BeginScrollView(viewport, staffScroll, new Rect(0, 0, rowWidth, StaffNames.Length * 56 * scale));
            x = 0;
            width = rowWidth;
            for (int role = 0; role < StaffNames.Length; role++)
            {
                float y = role * 56 * scale;
                bool hired = service.StaffHired(role);
                DeskFrame(new Rect(x, y, width, 51 * scale), DeskSurface, DeskTrim, 4 * scale, scale);
                DrawFittedText(new Rect(x + 8 * scale, y, width - 280 * scale, 28 * scale), StaffNames[role], detail);
                string assignment = hired ? "Available for work" : "";
                foreach (OperationTask task in employeeTasks)
                    if ((int?)task.Role == role)
                    {
                        assignment = $"{task.Title} · {OperationStatusLabel(task.Status)}";
                        break;
                    }
                DrawFittedText(new Rect(x + 8 * scale, y + 27 * scale, width - 280 * scale, 20 * scale), assignment, label);
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
                $"Cash ${service.Cash}   ·   {service.PendingOrderCount} open orders   ·   {MotelService.StandardDeliveryMinutes}m delivery");
            if (DrawButton(new Rect(x + width - 122 * scale, panel.y + 105 * scale, 122 * scale, 28 * scale),
                orderHistory ? "Shop" : "Track orders"))
                orderHistory = !orderHistory;

            DrawFittedText(new Rect(x, panel.y + 137 * scale, width, 26 * scale),
                $"Clean: {service.Towels} towels · {service.LinenSets} linen sets   |   Dirty: {service.DirtyTowels} towels · {service.DirtyLinens} linen sets   |   Washing: {service.ExpectedLaundryUnits}", label);
            if (orderHistory)
            {
                int lines = service.PendingOrderCount + service.CompletedOrderCount * 2;
                var viewport = new Rect(x, panel.y + 166 * scale, width, 243 * scale);
                orderScroll = GUI.BeginScrollView(viewport, orderScroll,
                    new Rect(0, 0, width - 20 * scale, Mathf.Max(viewport.height, lines * 27 * scale)));
                int line = 0;
                for (int index = 0; index < service.PendingOrderCount; index++)
                {
                    SupplyOrder order = service.SupplyOrderAt(index);
                    string status = order.Status switch
                    {
                        SupplyDelivery.AtBay => "Ready at the bay",
                        SupplyDelivery.Carried => "Being carried to storage",
                        _ => $"Due {MotelService.DisplayTime(order.Due)}"
                    };
                    DrawFittedText(new Rect(0, line++ * 27 * scale, width, 27 * scale),
                        $"#{order.Number} · {status}", label);
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

            var catalogArea = new Rect(x, panel.y + 166 * scale, width, 205 * scale);
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
                int packs = service.RecommendedRestockPacks(item);
                string kind = item is 1 or 2 ? "clean" : item is 0 or 4 ? "reusable" : "consumable";
                DrawFittedText(new Rect(x + 8 * scale, y + 20 * scale, width - 158 * scale, 23 * scale),
                    $"{stock} {kind} · {service.IncomingStock(item)} incoming" + (packs > 0 ? $" · Recommend {packs} {(packs == 1 ? "pack" : "packs")}" : ""), label);
                GUI.enabled = quantity > 0;
                if (DrawButton(new Rect(x + width - 135 * scale, y + 3 * scale, 34 * scale, 29 * scale), "-"))
                    supplyCart -= step;
                GUI.enabled = true;
                DrawFittedText(new Rect(x + width - 89 * scale, y + 3 * scale, 40 * scale, 29 * scale),
                    quantity.ToString(), label);
                GUI.enabled = item == 4 ? quantity == 0 && stock == 0 && service.IncomingStock(item) == 0 : quantity < MotelService.MaxCartQuantity;
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
