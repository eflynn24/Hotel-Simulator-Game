using System.Collections.Generic;
using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte SelectKeyAction = 16;
        private readonly Dictionary<ulong, int> selectedKeys = new Dictionary<ulong, int>();
        private static readonly Vector3 KeyPickupPoint = new Vector3(-9.43f, 0, MotelWorld.ReceptionBackWall - .52f);

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

            if (deskCheckInApproved && DrawWorldButton(new Vector3(-9.43f, 1.99f, 8.39f + MotelWorld.ReceptionExtension), "Room keys", 90 * scale, scale))
            {
                if (AtKeyCounter(caretaker.Position))
                    SetReceptionPanel(ReceptionPanel.Keys);
                else
                    SetDestination(KeyPickupPoint);
            }

            MotelService service = Service;
            int index = FindArrivalBooking(service);
            if (index < 0)
                return;
            string name = service.ReservationGuest(index);
            string caption = service.ReservationPending(index) && service.ReservationChannel(index) == BookingChannel.WalkIn
                ? $"{name} · Wants {MotelService.StayNights(service.ReservationStart(index), service.ReservationEnd(index))} nights"
                : $"{name} · Room {Occupied + 1}";
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

        private enum ReceptionPanel { None, Phone, Notebook, Guest, Keys, Maintenance, Portfolio, Restock }
        private ReceptionPanel receptionPanel;
        private int inspectedGuest;
        private const byte StaffAction = 20;

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
                if (service.RequestDue(room))
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
                string caption = $"{service.StayGuest(room)} · Room {room + 1}";
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
                : receptionPanel == ReceptionPanel.Restock ? "Order supplies"
                : receptionPanel == ReceptionPanel.Portfolio ? "Management phone" : Service.StayGuest(inspectedGuest);
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
            else if (receptionPanel == ReceptionPanel.Restock)
                DrawQuickRestock(area, scale);
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
                int start = service.PhoneArrival;
                int end = MotelService.DepartureForNights(start, service.PhoneNights);
                int room = service.SuggestRoom(start, end);
                DrawFittedText(new Rect(area.x, area.y, area.width, 27 * scale),
                    $"I'd like {service.PhoneNights} nights from {MotelService.DisplayTime(start)}.", detail);
                DrawFittedText(new Rect(area.x, area.y + 28 * scale, area.width, 25 * scale),
                    MotelService.PreferenceLabel(service.PhonePreference), label);
                string quote = room == NoRoom ? "No rooms available for this stay" :
                    $"Room {room + 1} · ${service.NightlyRate(room)}/night · ${service.NightlyRate(room) * service.PhoneNights} total";
                DrawFittedText(new Rect(area.x, area.y + 55 * scale, area.width, 25 * scale), quote, label);
                GUI.enabled = room != NoRoom;
                if (DrawButton(new Rect(area.x, area.y + 86 * scale, area.width, 32 * scale), "Accept telephone reservation"))
                    RequestBookingAction(AcceptPhoneAction, new Vector3(room, service.NextPhoneCall, 0));
                GUI.enabled = true;
            }
            else
            {
                string status = service.CanBookPhone ? "A coworker is handling the booking call." :
                    service.BookingCallRinging ? "Incoming reservation call" : service.PhoneRinging ? "Incoming guest call" :
                    bookingFeedback == "Telephone reservation confirmed" ? bookingFeedback : "No caller on the line";
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
                OpenComputer(SuppliesTab);
            }
            DrawFittedText(new Rect(area.x, area.y + 165 * scale, area.width, 26 * scale), $"Missed calls · {service.MissedCalls}", detail);
            for (int row = 0; row < Mathf.Min(4, service.MissedHistoryCount); row++)
                DrawFittedText(new Rect(area.x, area.y + (192 + row * 24) * scale, area.width, 24 * scale),
                    MotelService.DisplayTime(service.MissedCallTime(service.MissedHistoryCount - 1 - row)), label);
        }

        private void DrawGuestMenu(Rect area, float scale) => DrawGuestTask(area, scale, Service, inspectedGuest);
    }
}
