using System.Collections.Generic;
using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte SelectKeyAction = 16;
        private readonly Dictionary<ulong, int> selectedKeys = new Dictionary<ulong, int>();
        private static readonly Vector3 KeyPickupPoint = new Vector3(-9.69f, 0, 6.66f);

        private static bool AtKeyCounter(Vector3 point) =>
            InsideOffice(point) && MotelWalker.Distance(point, KeyPickupPoint) < .48f;

        private void PickReceptionKey(ulong worker, int room, Vector3 point)
        {
            if (GuestWaiting && deskCheckInApproved && AtKeyCounter(point) && (Service.UnlockedDoors & (1 << room)) == 0)
                selectedKeys[worker] = room;
        }

        private void DrawReceptionKeys(float scale)
        {
            if (!GuestWaiting || !InsideOffice(caretaker.Position))
                return;

            if (deskCheckInApproved && DrawWorldButton(new Vector3(-9.43f, 1.99f, 8.39f), "Room keys", 90 * scale, scale))
            {
                if (AtKeyCounter(caretaker.Position))
                    SetReceptionPanel(ReceptionPanel.Keys);
                else
                    SetDestination(KeyPickupPoint);
            }

            string name = GuestNames[Occupied];
            string caption = $"{name} · Room {Occupied + 1}";
            if (Service.ReturningGuest(Occupied))
                caption += " · Returning";
            int waiting = Mathf.Max(0, Service.ClockMinute - guestWaitStart);
            if (waiting >= 45)
                caption += $" · Impatient ({waiting}m)";
            if (selectedKeys.TryGetValue(LocalWorker, out int key))
                caption += $" · Give key {key + 1}";
            if (DrawWorldButton(guest.Position + Vector3.up * 1.75f, caption, 200 * scale, scale))
            {
                if (MotelWalker.Distance(caretaker.Position, guest.Position) < 1.4f)
                {
                    RequestReception();
                    if (lan != null && !lan.IsHost && key == Occupied)
                        selectedKeys.Remove(LocalWorker);
                }
                else
                    SetDestination(guest.Position + Vector3.left * .7f);
            }
        }

        private enum ReceptionPanel { None, Phone, Notebook, Guest, Keys, Maintenance, Portfolio }
        private ReceptionPanel receptionPanel;
        private int inspectedGuest;
        private int pinnedRoom = NoRoom;
        private int pinnedGuestId;
        private static readonly string[] GuestNames =
            { "Mara Ortiz", "Eli Brooks", "June Park", "Vic Ramos", "Nora Reed", "Sam Bell", "Lena Ruiz", "Tess Lane", "Rae King", "Luis Webb", "Jo Chen", "Max Vega" };
        private int notebookTab;
        private Vector2 notebookScroll;
        private const byte StaffAction = 20;
        private const byte GuestMenuAction = 21;

        private void SetReceptionPanel(ReceptionPanel panel)
        {
            receptionPanel = panel;
            if (panel == ReceptionPanel.Portfolio)
                Campaign.ReadUpdates();
            if (panel != ReceptionPanel.None)
                walkRoute.Clear();
            SetActorPause(panel != ReceptionPanel.None);
        }

        private void OpenPhone()
        {
            SetReceptionPanel(ReceptionPanel.Phone);
        }

        private void OpenGuestMenu(int room)
        {
            inspectedGuest = room;
            SetReceptionPanel(ReceptionPanel.Guest);
        }

        private void RequestOperations(byte action, Vector3 data)
        {
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(action, data);
            else
                HandleOperations(action, data, caretaker.Position);
        }

        private void HandleOperations(byte action, Vector3 data, Vector3 position)
        {
            MotelService service = Service;
            if (action == StaffAction && AtComputer(position) && data.x >= 0 && data.x < StaffNames.Length &&
                data.x == Mathf.Floor(data.x) && (data.y == 0 || data.y == 1))
            {
                int role = (int)data.x;
                bool hired = service.StaffHired(role);
                service.UpdateEmployee(new PropertyEmployee(service.Property, role), data.y == 1);
                if (lan == null && hired != service.StaffHired(role))
                {
                    Service = service;
                    Campaign.RecordMotelStaffChange(role, !hired);
                }
            }
            else if (action == GuestMenuAction && (data.y == 0 || data.y == 1))
            {
                if (data.x < 0 || data.x >= Guests.Length || data.x != Mathf.Floor(data.x))
                    return;
                int room = (int)data.x;
                if (GuestToServe(position) != room && !NearActiveGuest(room, position))
                    return;
                service.ApplyGuestRequest(service.RequestReference(room), data.y == 1);
            }
            Service = service;
        }

        private static int CountArrivals(MotelService service)
        {
            int count = 0;
            for (int index = 0; index < service.ReservationCount; index++)
                if (service.ReservationStart(index) / MotelService.MinutesPerDay == service.ClockMinute / MotelService.MinutesPerDay)
                    count++;
            return count;
        }

        private static int CountDepartures(MotelService service)
        {
            int count = 0;
            for (int room = 0; room < Guests.Length; room++)
                if ((service.ActiveRooms & (1 << room)) != 0 &&
                    service.DepartureMinute(room) / MotelService.MinutesPerDay == service.ClockMinute / MotelService.MinutesPerDay)
                    count++;
            return count;
        }

        private static int CountRequests(MotelService service)
        {
            int count = 0;
            for (int room = 0; room < Guests.Length; room++)
                if (service.RequestOpen(room))
                    count++;
            return count;
        }

        private void DrawGuestTags(float scale)
        {
            MotelService service = Service;
            for (int room = 0; room < Guests.Length; room++)
            {
                MotelWalker actor = room == visitorRoom ? serviceVisitor : backgroundGuests[room];
                if (actor == null || !MotelWorld.NearFloor(caretaker.Position, actor.Position) ||
                    MotelWalker.Distance(caretaker.Position, actor.Position) > 3f)
                    continue;
                string caption = $"{GuestNames[room]} · Room {room + 1}";
                if (service.RequestOpen(room) || service.OutstandingIssue(room) != GuestIssue.None)
                    caption += " · !";
                if (DrawWorldButton(actor.Position + Vector3.up * 1.73f, caption, 154 * scale, scale))
                {
                    if (room == GuestToServe(caretaker.Position) ||
                        ((service.ActiveRooms & (1 << room)) != 0 && MotelWalker.Distance(caretaker.Position, actor.Position) < 1.4f))
                        OpenGuestMenu(room);
                    else if (room == visitorRoom && VisitorAtDesk && MotelWalker.Distance(caretaker.Position, ServiceQueue) < 1.30f)
                        RequestReception();
                    else
                        SetDestination(actor.Position + Vector3.left * .65f);
                }
            }
        }

        private void DrawReceptionPanel(Rect screen, float scale)
        {
            Rect panel = DrawOverlay(screen, 420 * scale, scale);
            float left = panel.x + 18 * scale;
            float width = panel.width - 36 * scale;
            string heading = receptionPanel == ReceptionPanel.Notebook ? "Manager’s notebook"
                : receptionPanel == ReceptionPanel.Phone ? "Reception telephone"
                : receptionPanel == ReceptionPanel.Keys ? "Room keys"
                : receptionPanel == ReceptionPanel.Maintenance ? "Maintenance"
                : receptionPanel == ReceptionPanel.Portfolio ? "Management phone" : GuestNames[inspectedGuest];
            DrawFittedText(new Rect(left, panel.y + 12 * scale, width, 49 * scale), heading, title);
            var area = new Rect(left, panel.y + 68 * scale, width, 292 * scale);
            if (receptionPanel == ReceptionPanel.Notebook)
                DrawNotebook(area, scale);
            else if (receptionPanel == ReceptionPanel.Phone)
                DrawPhone(area, scale);
            else if (receptionPanel == ReceptionPanel.Portfolio)
                DrawPortfolioFinances(area, scale);
            else if (receptionPanel == ReceptionPanel.Maintenance)
                DrawMaintenance(area, scale);
            else if (receptionPanel == ReceptionPanel.Keys)
                DrawKeyRack(area, scale);
            else
                DrawGuestMenu(area, scale);
            if (DrawButton(new Rect(left, panel.y + 370 * scale, width, 34 * scale), "Close"))
                SetReceptionPanel(ReceptionPanel.None);
        }

        private void DrawKeyRack(Rect area, float scale)
        {
            float width = area.width / 4;
            DrawFittedText(new Rect(area.x, area.y, area.width, 40 * scale),
                $"Pick up the key for Room {Occupied + 1}", detail);
            for (int room = 0; room < Guests.Length; room++)
            {
                GUI.enabled = (Service.UnlockedDoors & (1 << room)) == 0;
                if (Service.RoomBuilt(room) && DrawButton(new Rect(area.x + room % 4 * width, area.y + (58 + room / 4 * 58) * scale,
                    width - 5 * scale, 50 * scale), $"Room {room + 1}"))
                {
                    if (lan != null && !lan.IsHost)
                    {
                        lan.State?.ActionRpc(SelectKeyAction, new Vector3(room, 0, 0));
                        selectedKeys[LocalWorker] = room;
                    }
                    else
                        PickReceptionKey(LocalWorker, room, caretaker.Position);
                    SetReceptionPanel(ReceptionPanel.None);
                }
                GUI.enabled = true;
            }
        }

        private void DrawPhone(Rect area, float scale)
        {
            MotelService service = Service;
            if (service.CanBookPhoneFor(LocalWorker))
            {
                DrawFittedText(new Rect(area.x, area.y, area.width, 27 * scale), "Caller wants to book a room", detail);
                DrawBookingForm(new Rect(area.x, area.y + 34 * scale, area.width, 65 * scale), scale, true);
            }
            else
            {
                string status = service.CanBookPhone ? "A coworker is handling the booking call." :
                    service.BookingCallRinging ? "Incoming reservation call" : service.PhoneRinging ? "Incoming guest call" :
                    bookingFeedback == "Phone request sent to Reservations" ? bookingFeedback : "No caller on the line";
                DrawFittedText(new Rect(area.x, area.y, area.width, 27 * scale), status, label);
                if (service.PhoneRinging && DrawButton(new Rect(area.x, area.y + 34 * scale, area.width, 30 * scale), "Answer call"))
                    RequestGuestAction(AnswerPhoneAction);
                if (service.CanReturnBookingCall &&
                    DrawButton(new Rect(area.x, area.y + 73 * scale, area.width, 30 * scale), "Return missed booking call"))
                    RequestGuestAction(ReturnPhoneAction);
            }
            float half = (area.width - 6 * scale) / 2;
            if ((service.PhoneRinging || service.CanBookPhoneFor(LocalWorker)) &&
                DrawButton(new Rect(area.x + half + 6 * scale, area.y + 128 * scale, half, 32 * scale), "Decline call"))
                RequestGuestAction(DeclinePhoneAction);
            if (DrawButton(new Rect(area.x, area.y + 128 * scale, half, 32 * scale), "Call supplier"))
            {
                SetReceptionPanel(ReceptionPanel.None);
                computerTab = 0;
                SetComputerVisible(true);
            }
            DrawFittedText(new Rect(area.x, area.y + 165 * scale, area.width, 26 * scale), $"Missed calls · {service.MissedCalls}", detail);
            for (int row = 0; row < Mathf.Min(4, service.MissedHistoryCount); row++)
                DrawFittedText(new Rect(area.x, area.y + (192 + row * 24) * scale, area.width, 24 * scale),
                    MotelService.DisplayTime(service.MissedCallTime(service.MissedHistoryCount - 1 - row)), label);
        }

        private void DrawGuestMenu(Rect area, float scale)
        {
            MotelService service = Service;
            DrawFittedText(new Rect(area.x, area.y, area.width, 30 * scale), $"Room {inspectedGuest + 1}", detail);
            DrawFittedText(new Rect(area.x, area.y + 152 * scale, area.width, 28 * scale),
                $"{service.Personality(inspectedGuest)} · {service.GuestSatisfaction(inspectedGuest)}", label);
            DrawFittedText(new Rect(area.x, area.y + 250 * scale, area.width, 26 * scale),
                $"Expected quality {service.Expectations(inspectedGuest)}% · Room quality {service.RoomQuality(inspectedGuest)}%", label);
            if (service.ReturningGuest(inspectedGuest))
                DrawFittedText(new Rect(area.x, area.y + 274 * scale, area.width, 25 * scale), service.GuestMemory(inspectedGuest), label);
            bool open = service.RequestOpen(inspectedGuest);
            string status = open ? $"{service.RequestLabel(inspectedGuest)} · Waiting {service.RequestWait(inspectedGuest)} min"
                : service.RequestSatisfied(inspectedGuest) ? "Request completed" : "No outstanding request";
            DrawFittedText(new Rect(area.x, area.y + 35 * scale, area.width, 32 * scale), status, label);
            GuestIssue issue = service.OutstandingIssue(inspectedGuest);
            if (issue != GuestIssue.None)
            {
                DrawFittedText(new Rect(area.x, area.y + 184 * scale, area.width, 26 * scale), IssueLabel(issue), label);
                if (DrawButton(new Rect(area.x, area.y + 215 * scale, area.width, 32 * scale), IssueActionLabel(issue)))
                    RequestPropertyCare(8, inspectedGuest, (Fixture)issue);
            }
            if (open)
            {
                if (DrawButton(new Rect(area.x, area.y + 76 * scale, area.width, 32 * scale), service.RequestSuppliesAvailable(inspectedGuest)
                    ? service.RequestLabel(inspectedGuest) : "Waiting for supplies") && service.RequestSuppliesAvailable(inspectedGuest))
                    RequestOperations(GuestMenuAction, new Vector3(inspectedGuest, 0, 0));
                if (DrawButton(new Rect(area.x, area.y + 116 * scale, area.width, 32 * scale), "Cancel request"))
                    RequestOperations(GuestMenuAction, new Vector3(inspectedGuest, 1, 0));

            }
        }

        private void DrawNotebook(Rect area, float scale)
        {
            string[] tabs = { "Guests", "Requests", "Schedule", "Deliveries" };
            float tabWidth = area.width / tabs.Length;
            for (int tab = 0; tab < tabs.Length; tab++)
                if (DrawButton(new Rect(area.x + tabWidth * tab, area.y, tabWidth - 3 * scale, 30 * scale), tabs[tab]))
                {
                    notebookTab = tab;
                    notebookScroll = Vector2.zero;
                }

            MotelService service = Service;
            if (pinnedRoom != NoRoom && (!service.RequestDue(pinnedRoom) || service.GuestId(pinnedRoom) != pinnedGuestId))
                pinnedRoom = NoRoom;
            var rows = new List<(string Caption, int Room)>();
            if (notebookTab == 0 || notebookTab == 1)
            {
                for (int room = 0; room < Guests.Length; room++)
                {
                    string request = service.NotebookRequest(room);
                    if (notebookTab == 0 && (service.ActiveRooms & (1 << room)) != 0)
                        rows.Add(($"{GuestNames[room]} · Room {room + 1} · {ActivityLabel(service.Activity(room))}", NoRoom));
                    else if (notebookTab == 1 && service.HousekeepingRequested(room))
                        rows.Add(($"Room {room + 1} · Housekeeping requested", NoRoom));
                    if (notebookTab == 1 && request != null)
                    {
                        var entry = (request, room);
                        if (room == pinnedRoom)
                            rows.Insert(0, entry);
                        else
                            rows.Add(entry);
                    }
                }
            }
            else if (notebookTab == 2)
            {
                for (int i = 0; i < service.ReservationCount; i++)
                    rows.Add(($"Arrive · Room {service.ReservationRoom(i) + 1} · {MotelService.DisplayTime(service.ReservationStart(i))}", NoRoom));
                for (int room = 0; room < Guests.Length; room++)
                    if ((service.ActiveRooms & (1 << room)) != 0)
                        rows.Add(($"Depart · Room {room + 1} · {MotelService.DisplayTime(service.DepartureMinute(room))}", NoRoom));
            }
            else if (service.PendingOrderCount > 0)
            {
                string state = service.Delivery == SupplyDelivery.InTransit ? MotelService.DisplayTime(service.DeliveryDue)
                    : service.Delivery == SupplyDelivery.AtBay ? "At the delivery bay" : "Carried to storage";
                rows.Add(($"Order #{service.ActiveOrderNumber} · {state}", NoRoom));
                for (int i = 0; i < service.PendingOrderCount - 1; i++)
                    rows.Add(($"Order #{service.QueuedOrderNumber(i)} · {MotelService.DisplayTime(service.QueuedOrderDue(i))}", NoRoom));
            }

            if (notebookTab == 1)
                for (int room = 0; room < Guests.Length; room++)
                {
                    GuestIssue issue = service.OutstandingIssue(room);
                    if (issue != GuestIssue.None)
                        rows.Add(($"Room {room + 1} · {IssueLabel(issue)}", NoRoom));
                }
            if (notebookTab == 3)
                for (int item = 0; item < MotelService.Catalog.Length; item++)
                    if (item != 4 && service.Stock(item) <= 1)
                        rows.Add(($"Low stock · {MotelService.Catalog[item].Name} · {service.Stock(item)} left", NoRoom));
            if (rows.Count == 0)
                rows.Add(("Nothing scheduled here", NoRoom));
            var viewport = new Rect(area.x, area.y + 40 * scale, area.width, area.height - 40 * scale);
            float width = area.width - 20 * scale;
            notebookScroll = GUI.BeginScrollView(viewport, notebookScroll, new Rect(0, 0, width, rows.Count * 31 * scale));
            for (int row = 0; row < rows.Count; row++)
            {
                var entry = rows[row];
                float y = row * 31 * scale;
                DrawFittedText(new Rect(0, y, width - (entry.Room == NoRoom ? 0 : 62 * scale), 30 * scale), entry.Caption, label);
                if (entry.Room != NoRoom && DrawButton(new Rect(width - 60 * scale, y, 60 * scale, 28 * scale),
                    pinnedRoom == entry.Room ? "Unpin" : "Pin"))
                {
                    pinnedRoom = pinnedRoom == entry.Room ? NoRoom : entry.Room;
                    if (pinnedRoom != NoRoom)
                        pinnedGuestId = service.GuestId(pinnedRoom);
                }
            }
            GUI.EndScrollView();
        }
    }
}
