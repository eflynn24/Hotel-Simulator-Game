using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private enum ReservationView { Calendar, All, Arrivals, Departures, Requests, New, Decisions }

        private static readonly Color DeskShell = new Color32(43, 29, 21, 255);
        private static readonly Color DeskGold = new Color32(177, 126, 71, 255);
        private static readonly Color DeskTrim = new Color32(111, 81, 54, 255);
        private static readonly Color DeskSurface = new Color32(65, 48, 35, 255);
        private static readonly Color DeskPriority = new Color32(235, 184, 91, 255);
        private static readonly Color DeskAvailable = new Color32(48, 91, 57, 255);
        private static readonly Color DeskReserved = new Color32(129, 76, 34, 255);
        private static readonly Color DeskOccupied = new Color32(44, 82, 130, 255);
        private static readonly Color DeskMaintenance = new Color32(88, 84, 80, 255);
        private static readonly Color DeskDanger = new Color32(117, 58, 43, 255);
        private static readonly Color DeskCard = new Color32(52, 39, 29, 255);
        private static readonly Color DeskPaper = new Color32(233, 214, 184, 255);
        private static readonly string[] DeskFilters = { "All rooms", "Available", "Reserved", "Occupied", "Unavailable", "Dirty" };
        private static readonly Color[] DeskStates = { DeskCard, DeskAvailable, DeskReserved, DeskOccupied, DeskMaintenance, DeskDanger };
        private ReservationView reservationView;
        private int calendarFilter;
        private int selectedRoom = NoRoom;
        private int selectedArrival = -1;
        private bool editingReservation;
        private int rosterOffset;
        private bool confirmingCancellation;
        private Vector2 reservationScroll;
        private GUIStyle deskButton;
        private GUIStyle deskPaperText;

        private bool BookingButton(Rect rect, string text, Color surface, int icon = -1)
        {
            if (deskButton == null)
                deskButton = new GUIStyle(GUIStyle.none)
                {
                    alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                    padding = new RectOffset(5, 5, 2, 2), clipping = TextClipping.Clip
                };

            bool accent = surface == DeskPriority;
            bool hovered = GUI.enabled && rect.Contains(Event.current.mousePosition);
            Color fill = !GUI.enabled ? Color.Lerp(surface, DeskShell, .55f) :
                hovered ? Color.Lerp(surface, Color.white, .14f) : surface;
            bool calendarCell = rect.height < 17 && icon < 0;
            if (calendarCell)
                DeskRound(rect, fill, 2, 0);
            else
                DeskFrame(rect, fill, accent ? DeskGold : DeskTrim, Mathf.Min(6, rect.height * .2f), 1);
            deskButton.fontSize = button.fontSize;
            Color color = !GUI.enabled ? new Color32(199, 178, 151, 255) : accent ? Ink : Color.white;
            deskButton.normal.textColor = deskButton.hover.textColor = deskButton.active.textColor =
                deskButton.focused.textColor = color;
            var content = new GUIContent(text);
            int iconWidth = Mathf.RoundToInt(rect.height * .54f);
            deskButton.padding.left = icon >= 0 ? iconWidth + 14 : 5;
            FitText(rect, content, deskButton);
            if (icon >= 0)
                DrawComputerIcon(new Rect(rect.x + 9, rect.center.y - iconWidth / 2f, iconWidth, iconWidth), icon, color);
            return GUI.Button(rect, content, deskButton);
        }

        private static void DeskRound(Rect rect, Color color, float radius, float border) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, border, radius);

        private static void DeskFrame(Rect rect, Color color, Color trim, float radius, float border)
        {
            DeskRound(rect, color, radius, 0);
            DeskRound(rect, trim, radius, border);
        }

        private void DrawDeskSummary(Rect rect, float scale, string text)
        {
            DeskFrame(rect, DeskPaper, DeskGold, 6 * scale, 1);
            deskPaperText ??= new GUIStyle(label) { padding = new RectOffset(0, 0, 0, 0) };
            deskPaperText.fontSize = detail.fontSize;
            deskPaperText.normal.textColor = Ink;
            DrawFittedText(new Rect(rect.x + 9 * scale, rect.y, rect.width - 18 * scale, rect.height), text, deskPaperText);
        }

        // Small silhouettes use the same palette as the tab labels.
        private static readonly byte[,] ComputerIcons =
        {
            { 0x18, 0x3C, 0x7E, 0xFF, 0xDB, 0xDB, 0x7E, 0x3C },
            { 0x42, 0xFF, 0x81, 0xFF, 0x81, 0xA5, 0x81, 0xFF },
            { 0x24, 0x76, 0x76, 0x24, 0xDE, 0xFF, 0xFF, 0x7E },
            { 0x03, 0x03, 0x1B, 0x1B, 0xDB, 0xDB, 0xDB, 0xFF },
            { 0x3C, 0x7E, 0x66, 0x7E, 0x66, 0x7E, 0x5A, 0xFF },
            { 0x08, 0x1C, 0x3E, 0x3E, 0x3E, 0x7F, 0xFF, 0x18 }
        };

        private static void DrawComputerIcon(Rect rect, int icon, Color color)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            float unit = rect.width / 8;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    if ((ComputerIcons[icon, y] & (128 >> x)) != 0)
                        Fill(new Rect(rect.x + x * unit, rect.y + y * unit, unit, unit), color);
        }

        private void SelectBooking(MotelService service, int index)
        {
            if (index < 0)
                return;
            reservationEditFeedback = null;
            selectedRoom = service.ReservationRoom(index);
            selectedArrival = service.ReservationStart(index);
            editingReservation = confirmingCancellation = false;
        }

        private void SelectStay(int room)
        {
            reservationEditFeedback = null;
            selectedRoom = room;
            selectedArrival = -1;
            editingReservation = confirmingCancellation = false;
        }

        private int FindArrivalBooking(MotelService service)
        {
            if (!GuestWaiting)
                return -1;
            for (int index = 0; index < service.ReservationCount; index++)
                if (service.ReservationRoom(index) == Occupied && service.ReservationStart(index) == guestArrivalMinute)
                    return index;
            return -1;
        }

        private int SelectedBooking(MotelService service)
        {
            if (selectedArrival >= 0)
                for (int index = 0; index < service.ReservationCount; index++)
                    if (service.ReservationRoom(index) == selectedRoom && service.ReservationStart(index) == selectedArrival)
                        return index;
            return -1;
        }

        private void DrawReservationWorkspace(Rect area, float scale)
        {
            MotelService service = Service;
            var sidebar = new Rect(area.x, area.y, 155 * scale, area.height);
            var main = new Rect(sidebar.xMax + 10 * scale, area.y, area.xMax - sidebar.xMax - 10 * scale, area.height);
            DeskFrame(sidebar, DeskCard, DeskTrim, 7 * scale, scale);
            DrawReservationSidebar(sidebar, scale, service);
            if (reservationView == ReservationView.New)
            {
                DeskFrame(main, DeskCard, DeskTrim, 7 * scale, scale);
                Rect form = InsetDesk(main, 14 * scale);
                DrawFittedText(new Rect(form.x, form.y, form.width, 34 * scale), "New reservation", detail);
                DrawBookingForm(new Rect(form.x, form.y + 41 * scale, form.width, 200 * scale), scale, service);
                return;
            }
            if (reservationView == ReservationView.Decisions)
            {
                DeskFrame(main, DeskCard, DeskTrim, 7 * scale, scale);
                var anchor = new Rect(main.x, main.y - 127 * scale, main.width, main.height);
                DrawDecisions(anchor, main.x + 14 * scale, main.width - 28 * scale, scale);
                return;
            }

            var overview = new Rect(main.x, main.y, main.width, 258 * scale);
            DeskFrame(overview, DeskCard, DeskTrim, 7 * scale, scale);
            if (reservationView == ReservationView.Calendar)
                DrawReservationCalendar(InsetDesk(overview, 9 * scale), scale, service);
            else
                DrawReservationList(InsetDesk(overview, 9 * scale), scale, service);

            var details = new Rect(main.x, overview.yMax + 8 * scale, main.width, main.yMax - overview.yMax - 8 * scale);
            DeskFrame(details, DeskCard, DeskTrim, 7 * scale, scale);
            DrawReservationDetails(InsetDesk(details, 10 * scale), scale, service);
        }

        private static Rect InsetDesk(Rect rect, float margin) =>
            new Rect(rect.x + margin, rect.y + margin, rect.width - 2 * margin, rect.height - 2 * margin);

        private void DrawReservationSidebar(Rect panel, float scale, MotelService service)
        {
            Rect row = new Rect(panel.x + 7 * scale, panel.y + 7 * scale, panel.width - 14 * scale, 30 * scale);
            if (BookingButton(row, "+ New reservation", DeskPriority))
            {
                BeginBooking(service, service.ClockMinute / MotelService.MinutesPerDay);
            }

            int today = service.ClockMinute / MotelService.MinutesPerDay;
            int arrivals = 0;
            int departures = 0;
            for (int index = 0; index < service.ReservationCount; index++)
                if (IsExpectedArrival(service, index) && service.ReservationStart(index) / MotelService.MinutesPerDay == today)
                    arrivals++;
            for (int room = 0; room < service.RoomCapacity; room++)
                if ((service.ActiveRooms & (1 << room)) != 0 && service.DepartureMinute(room) / MotelService.MinutesPerDay <= today)
                    departures++;
            row.y += 40 * scale;
            DrawReservationNavigation(ref row, scale, ReservationView.Calendar, "Room calendar");
            DrawReservationNavigation(ref row, scale, ReservationView.All, "All reservations");
            DrawReservationNavigation(ref row, scale, ReservationView.Arrivals, $"Arrivals today · {arrivals}");
            DrawReservationNavigation(ref row, scale, ReservationView.Departures, $"Departures today · {departures}");
            DrawReservationNavigation(ref row, scale, ReservationView.Requests, $"Requests · {service.PendingReservationCount}");
            DrawReservationNavigation(ref row, scale, ReservationView.Decisions,
                service.PendingDecision == ManagementDecision.None ? "Decisions" : "Decisions · 1");
            if (reservationView != ReservationView.Calendar)
                return;

            var approval = new Rect(row.x, panel.yMax - 30 * scale, row.width, 23 * scale);
            if (BookingButton(approval, service.ManualOnlineApproval ? "Online: Manual" : "Online: Auto-confirm", DeskSurface))
                RequestBookingAction(OnlineApprovalAction, new Vector3(service.ManualOnlineApproval ? 0 : 1, 0, 0));

            row.y += 5 * scale;
            DrawFittedText(row, "Filter rooms", label);
            row.y += 23 * scale;
            row.height = 20 * scale;
            for (int filter = 0; filter < DeskFilters.Length; filter++)
            {
                if (BookingButton(row, DeskFilters[filter], filter == calendarFilter ? DeskPriority : DeskStates[filter]))
                    calendarFilter = filter;
                row.y += 22 * scale;
            }
        }

        private void DrawReservationNavigation(ref Rect row, float scale, ReservationView view, string caption)
        {
            if (BookingButton(row, caption, reservationView == view ? DeskPriority : DeskSurface) && reservationView != view)
            {
                reservationView = view;
                reservationScroll = Vector2.zero;
                rosterOffset = 0;
                editingReservation = confirmingCancellation = false;
                MotelService service = Service;
                if (view == ReservationView.Requests)
                    SelectBooking(service, service.FirstPendingReservation);
                else if (view == ReservationView.Arrivals)
                    SelectBooking(service, FindArrivalBooking(service));
                else if (view == ReservationView.Departures && service.CheckoutWaiting() != NoRoom)
                    SelectStay(service.CheckoutWaiting());
            }
            row.y += 34 * scale;
        }

        private static int BookingForDay(MotelService service, int room, int start)
        {
            int arrival = CalendarArrival(start / MotelService.MinutesPerDay, service.ClockMinute);
            int departure = MotelService.DepartureForNights(start, 1);
            for (int index = 0; index < service.ReservationCount; index++)
                if (service.ReservationRoom(index) == room && service.ReservationStart(index) < departure &&
                    service.ReservationEnd(index) > arrival)
                    return index;
            return -1;
        }

        private static int CalendarState(MotelService service, int room, int start, int booking)
        {
            if (service.RoomUnusable(room))
                return 4;
            int arrival = CalendarArrival(start / MotelService.MinutesPerDay, service.ClockMinute);
            if ((service.ActiveRooms & (1 << room)) != 0 &&
                service.DepartureMinute(room) > (start <= service.ClockMinute ? service.ClockMinute : arrival))
                return 3;
            if (booking >= 0)
                return 2;
            return start / MotelService.MinutesPerDay == service.ClockMinute / MotelService.MinutesPerDay &&
                service.RoomDirty(room) ? 5 : 1;
        }

        private void DrawReservationCalendar(Rect area, float scale, MotelService service)
        {
            int first = service.ClockMinute / MotelService.MinutesPerDay + calendarOffset;
            float header = 27 * scale;
            DrawFittedText(new Rect(area.x, area.y, area.width - 230 * scale, header), "Room calendar", detail);
            var week = new Rect(area.xMax - 224 * scale, area.y, 224 * scale, header);
            if (BookingButton(new Rect(week.x, week.y, header, header), "<", DeskSurface))
                calendarOffset = Mathf.Max(0, calendarOffset - 7);
            DrawFittedText(new Rect(week.x + 33 * scale, week.y, 153 * scale, header),
                $"Days {first + 1} to {first + 7}", label);
            if (BookingButton(new Rect(week.xMax - header, week.y, header, header), ">", DeskSurface))
                calendarOffset = Mathf.Min(7, calendarOffset + 7);

            float gridY = area.y + 31 * scale;
            float gutter = 62 * scale;
            float column = (area.width - gutter) / 7;
            float row = Mathf.Min(19 * scale, (area.height - 69 * scale) / service.RoomCapacity);
            DrawFittedText(new Rect(area.x, gridY, gutter, 19 * scale), "Room", label);
            for (int day = 0; day < 7; day++)
                DrawFittedText(new Rect(area.x + gutter + day * column + 3 * scale, gridY, column - 3 * scale, 19 * scale),
                    $"Day {first + day + 1}", label);

            float y = gridY + 20 * scale;
            int shown = 0;
            for (int room = 0; room < service.RoomCapacity; room++)
            {
                bool visible = calendarFilter == 0;
                for (int day = 0; !visible && day < 7; day++)
                {
                    int start = (first + day) * MotelService.MinutesPerDay;
                    visible = CalendarState(service, room, start, BookingForDay(service, room, start)) == calendarFilter;
                }
                if (!visible)
                    continue;

                shown++;
                DrawFittedText(new Rect(area.x, y, gutter, row), $"Room {room + 1}", detail);
                int selected = SelectedBooking(service);
                for (int day = 0; day < 7; day++)
                {
                    int start = (first + day) * MotelService.MinutesPerDay;
                    int booking = BookingForDay(service, room, start);
                    int state = CalendarState(service, room, start, booking);
                    var cell = new Rect(area.x + gutter + day * column, y, column - 2 * scale, row - 2 * scale);
                    bool highlighted = state == 3 && selectedRoom == room && selectedArrival == -1 || booking >= 0 && booking == selected;
                    string caption = state == 4 ? "Unavailable" : state == 3 ? "Occupied" :
                        booking >= 0 ? service.ReservationPending(booking) ? "Request" : "Reserved" : state == 5 ? "Dirty" : "";
                    if (BookingButton(cell, caption, highlighted ? DeskPriority : DeskStates[state]))
                    {
                        if (state == 3)
                            SelectStay(room);
                        else if (booking >= 0)
                            SelectBooking(service, booking);
                        else if (state == 1 || state == 5)
                            BeginBooking(service, first + day, room);
                    }
                }
                y += row;
            }
            if (shown == 0)
                DrawFittedText(new Rect(area.x, y, area.width, 28 * scale), "No rooms match this filter.", detail);
            float cellWidth = area.width / 5;
            for (int state = 1; state <= 5; state++)
            {
                float x = area.x + (state - 1) * cellWidth;
                Fill(new Rect(x, area.yMax - 13 * scale, 10 * scale, 10 * scale), DeskStates[state]);
                DrawFittedText(new Rect(x + 14 * scale, area.yMax - 18 * scale, cellWidth - 16 * scale, 18 * scale),
                    DeskFilters[state], label);
            }
        }

        private void DrawReservationList(Rect area, float scale, MotelService service)
        {
            int today = service.ClockMinute / MotelService.MinutesPerDay;
            bool daily = reservationView == ReservationView.Arrivals || reservationView == ReservationView.Departures;
            if (daily && BookingButton(new Rect(area.xMax - 66 * scale, area.y, 30 * scale, 26 * scale), "<", DeskSurface))
                rosterOffset = Mathf.Max(0, rosterOffset - 1);
            if (daily && BookingButton(new Rect(area.xMax - 32 * scale, area.y, 30 * scale, 26 * scale), ">", DeskSurface))
                rosterOffset = Mathf.Min(13, rosterOffset + 1);
            int day = today + rosterOffset;
            string heading = reservationView switch
            {
                ReservationView.Arrivals => $"Arrivals · Day {day + 1}",
                ReservationView.Departures => $"Departures · Day {day + 1}",
                ReservationView.Requests => "Reservation requests",
                _ => "All reservations"
            };
            DrawFittedText(new Rect(area.x, area.y, area.width - (daily ? 72 * scale : 0), 28 * scale), heading, detail);
            float row = 28 * scale;
            int entries = 0;
            for (int index = 0; index < service.ReservationCount; index++)
                if (ShowReservation(service, index, day))
                    entries++;
            for (int room = 0; room < service.RoomCapacity; room++)
                if (ShowStay(service, room, day))
                    entries++;
            if (entries == 0)
            {
                DrawFittedText(new Rect(area.x, area.y + 33 * scale, area.width, row), "Nothing scheduled here.", label);
                return;
            }
            var viewport = new Rect(area.x, area.y + 32 * scale, area.width, area.height - 32 * scale);
            float width = area.width - 16 * scale;
            reservationScroll = GUI.BeginScrollView(viewport, reservationScroll, new Rect(0, 0, width, entries * row));
            float y = 0;
            int selected = SelectedBooking(service);
            for (int index = 0; index < service.ReservationCount; index++)
            {
                if (!ShowReservation(service, index, day))
                    continue;
                int room = service.ReservationRoom(index);
                int start = service.ReservationStart(index);
                int end = service.ReservationEnd(index);
                string name = $"{service.ReservationGuest(index)} · Room {room + 1} · {service.ReservationChannel(index)} · " +
                    $"{MotelService.StayNights(start, end)}n · {MotelService.DisplayTime(reservationView == ReservationView.Departures ? end : start)}";
                if (BookingButton(new Rect(0, y, width, row - 3 * scale), name, index == selected ? DeskPriority : DeskReserved))
                    SelectBooking(service, index);
                y += row;
            }
            for (int room = 0; room < service.RoomCapacity; room++)
            {
                if (!ShowStay(service, room, day))
                    continue;
                string name = $"{service.StayGuest(room)} · Room {room + 1} · {service.StaySource(room)} · " +
                    $"{MotelService.StayNights(service.StayStart(room), service.DepartureMinute(room))}n · Out {MotelService.DisplayTime(service.DepartureMinute(room))}";
                bool current = room == selectedRoom && selectedArrival < 0;
                if (BookingButton(new Rect(0, y, width, row - 3 * scale), name, current ? DeskPriority : DeskOccupied))
                    SelectStay(room);
                y += row;
            }
            GUI.EndScrollView();
        }

        private static bool IsExpectedArrival(MotelService service, int index) =>
            !service.ReservationPending(index) || service.ReservationChannel(index) == BookingChannel.WalkIn;

        private bool ShowReservation(MotelService service, int index, int day) => reservationView switch
        {
            ReservationView.Requests => service.ReservationPending(index) && service.ReservationChannel(index) != BookingChannel.WalkIn,
            ReservationView.Arrivals => IsExpectedArrival(service, index) &&
                (service.ReservationStart(index) / MotelService.MinutesPerDay == day ||
                    day == service.ClockMinute / MotelService.MinutesPerDay && index == FindArrivalBooking(service)),
            ReservationView.Departures => !service.ReservationPending(index) &&
                service.ReservationEnd(index) / MotelService.MinutesPerDay == day,
            _ => true
        };

        private bool ShowStay(MotelService service, int room, int day)
        {
            int departure = service.DepartureMinute(room) / MotelService.MinutesPerDay;
            return (service.ActiveRooms & (1 << room)) != 0 && (reservationView == ReservationView.All ||
                reservationView == ReservationView.Departures && (departure == day ||
                    departure < day && day == service.ClockMinute / MotelService.MinutesPerDay));
        }

        private void DrawReservationDetails(Rect area, float scale, MotelService service)
        {
            DrawFittedText(new Rect(area.x, area.y, area.width, 27 * scale), reservationEditFeedback ?? "Reservation details", detail);
            int index = SelectedBooking(service);
            bool stay = selectedArrival < 0 && selectedRoom >= 0 && (service.ActiveRooms & (1 << selectedRoom)) != 0;
            if (index < 0 && !stay)
            {
                DrawFittedText(new Rect(area.x, area.y + 32 * scale, area.width, 27 * scale),
                    "Select a reservation or an occupied room to see its details.", label);
                return;
            }

            int room = selectedRoom;
            bool pending = index >= 0 && service.ReservationPending(index);
            bool atReception = index >= 0 && index == FindArrivalBooking(service) && service.DeskArrivalReady(room);
            bool checkout = stay && room == visitorRoom && CheckoutVisitor && VisitorAtDesk;
            int start = stay ? service.StayStart(room) : service.ReservationStart(index);
            int end = stay ? service.DepartureMinute(room) : service.ReservationEnd(index);
            int rate = stay ? service.StayRate(room) : service.ReservationRate(index);
            int nights = MotelService.StayNights(start, end);
            bool walkIn = pending && service.ReservationChannel(index) == BookingChannel.WalkIn;
            string status = atReception ? deskCheckInApproved ? "Key handoff pending" : walkIn ? $"Walk-in requests {nights} nights" : "Waiting at reception" :
                pending ? "Awaiting confirmation" : stay ? checkout ? "Ready to check out" : service.ClockMinute >= end ? "Coming to reception" : "Occupied" : "Reserved";
            string name = stay ? service.StayGuest(room) : service.ReservationGuest(index);
            string source = stay ? service.StaySource(room) : service.ReservationChannel(index).ToString();
            Rect summary = new Rect(area.x, area.y + 28 * scale, area.width, 75 * scale);
            DeskFrame(summary, DeskPaper, DeskGold, 6 * scale, 1);
            Rect contents = InsetDesk(summary, 6 * scale);
            deskPaperText ??= new GUIStyle(label) { padding = new RectOffset(0, 0, 0, 0) };
            deskPaperText.fontSize = detail.fontSize;
            deskPaperText.normal.textColor = Ink;
            float split = contents.width / 2;
            DrawFittedText(new Rect(contents.x, contents.y, split, 22 * scale), $"{name} · Room {room + 1} · {source}", deskPaperText);
            string condition = service.RoomUnusable(room) ? " · Repairs needed" :
                service.RoomDirty(room) ? " · Prepare room" : "";
            DrawFittedText(new Rect(contents.x + split, contents.y, split, 22 * scale), status + condition, deskPaperText);
            DrawFittedText(new Rect(contents.x, contents.y + 21 * scale, split, 22 * scale),
                $"Check-in  {MotelService.DisplayTime(start)}", deskPaperText);
            DrawFittedText(new Rect(contents.x + split, contents.y + 21 * scale, split, 22 * scale),
                $"Check-out  {MotelService.DisplayTime(end)}", deskPaperText);
            DrawFittedText(new Rect(contents.x, contents.y + 42 * scale, split, 22 * scale),
                $"${rate}/night · {nights} {(nights == 1 ? "night" : "nights")} · ${rate * nights} total", deskPaperText);
            DrawFittedText(new Rect(contents.x + split, contents.y + 42 * scale, split, 22 * scale),
                stay ? service.StayRequest(room) : MotelService.PreferenceLabel(service.ReservationPreference(index)), deskPaperText);

            var actions = new Rect(area.x, area.yMax - 29 * scale, area.width, 29 * scale);
            if (stay)
            {
                if (checkout && DeskActionButton(actions, 0, 2, "Settle bill and collect key", DeskPriority, scale))
                    RequestBookingAction(DeskCheckOutAction, new Vector3(room, end, service.GuestId(room)));
                if (DeskActionButton(actions, 1, 2, "Extend stay", DeskSurface, scale))
                    RequestBookingAction(EditBookingAction, new Vector3(room + 6 * MotelService.RoomCount, end, service.GuestId(room)));
                return;
            }
            if (atReception)
            {
                if (deskCheckInApproved)
                    DrawFittedText(actions, "Pick up the room key and hand it to the guest.", label);
                else if (BookingButton(actions, walkIn ? "Confirm room and prepare key" : "Prepare room key", DeskPriority))
                    RequestBookingAction(DeskCheckInAction, new Vector3(room, start, end));
                return;
            }
            if (walkIn)
                return;
            if (editingReservation)
            {
                string[] changes = { "Arrival +15 min", "Arrival -15 min", "Add night", "Remove night", "Next free room", "Done" };
                for (int item = 0; item < changes.Length; item++)
                    if (DeskActionButton(actions, item, changes.Length, changes[item], DeskSurface, scale))
                    {
                        if (item < 5)
                        {
                            RequestBookingAction(EditBookingAction,
                                new Vector3(room + (item + 1) * MotelService.RoomCount, start, end));
                        }
                        editingReservation = item < 5;
                    }
                return;
            }
            if (confirmingCancellation)
            {
                if (DeskActionButton(actions, 0, 2, "Confirm cancellation", DeskDanger, scale))
                {
                    confirmingCancellation = false;
                    RequestBookingAction(EditBookingAction, new Vector3(room, start, end));
                }
                if (DeskActionButton(actions, 1, 2, "Keep reservation", DeskSurface, scale))
                    confirmingCancellation = false;
                return;
            }
            if (pending && DeskActionButton(actions, 0, 3, "Confirm reservation", DeskPriority, scale))
                RequestBookingAction(ConfirmReservationAction, new Vector3(room, start, end));
            if (DeskActionButton(actions, pending ? 1 : 0, pending ? 3 : 2, "Edit reservation", DeskSurface, scale))
                editingReservation = true;
            if (DeskActionButton(actions, pending ? 2 : 1, pending ? 3 : 2, pending ? "Decline request" : "Cancel reservation", DeskDanger, scale))
                confirmingCancellation = true;
        }

        private bool DeskActionButton(Rect strip, int index, int count, string text, Color color, float scale)
        {
            float width = strip.width / count;
            return BookingButton(new Rect(strip.x + index * width, strip.y, width - 4 * scale, strip.height), text, color);
        }
    }
}
