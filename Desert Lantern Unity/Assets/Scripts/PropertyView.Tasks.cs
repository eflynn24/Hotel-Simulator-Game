using System.Collections.Generic;
using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte GuestTaskAction = 33;
        private const byte QuickOrderAction = 34;
        private const int AcceptGuestTask = 0;
        private const int CollectGuestItem = 1;
        private const int DeliverGuestItem = 2;
        private const int ReleaseGuestTask = 3;

        private const byte OperationOwnerAction = 35;
        private static readonly string[] TaskFilters = { "Active", "Mine", "Staff", "Upcoming", "History" };
        private readonly List<OperationTask> notebookTasks = new List<OperationTask>();
        private readonly List<OperationTask> waypointTasks = new List<OperationTask>();
        private int notebookTab;
        private Vector2 notebookScroll;
        private OperationTask? inspectedTask;
        private OperationTask? pinnedTask;
        private bool showTaskWaypoint = true;
        private Rect taskWaypointPanel;
        private int reorderItem;
        private bool quickOrderPending;
        private string quickOrderStatus;

        private bool CanReachGuestTask(int room, Vector3 point) => NearActiveGuest(room, point) ||
            room == visitorRoom && VisitorAtDesk && !CheckoutVisitor && MotelWorld.NearFloor(point, ServiceQueue) &&
            MotelWalker.Distance(point, ServiceQueue) < 1.3f;

        private void RequestGuestTask(int room, int operation)
        {
            PropertyRequest request = Service.RequestReference(room);
            var data = new Vector3(room + operation * MotelService.RoomCount, request.Guest, request.ScheduledMinute);
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(GuestTaskAction, data);
            else
                HandleGuestTask(LocalWorker, data, caretaker.Position);
        }

        private void HandleGuestTask(ulong worker, Vector3 data, Vector3 point)
        {
            if (!(data.x >= 0 && data.x < 4 * MotelService.RoomCount) || data.x != Mathf.Floor(data.x) ||
                !Finite(data.y) || !Finite(data.z))
                return;

            MotelService service = Service;
            int room = (int)data.x % MotelService.RoomCount;
            int operation = (int)data.x / MotelService.RoomCount;
            if (!service.RoomBuilt(room) || service.GuestId(room) != data.y ||
                service.RequestReference(room).ScheduledMinute != data.z)
                return;

            PropertyRequest request = service.RequestReference(room);
            bool changed = operation switch
            {
                AcceptGuestTask => service.ClaimGuestTask(request, worker),
                CollectGuestItem when AtSupplyStorage(point) => service.CollectGuestTask(request, worker),
                DeliverGuestItem when CanReachGuestTask(room, point) => service.DeliverGuestTask(request, worker),
                ReleaseGuestTask => service.UnassignGuestTask(request, worker),
                _ => false
            };
            if (changed)
                Service = service;
        }

        private bool InteractWithGuestTask()
        {
            MotelService service = Service;
            for (int room = 0; room < service.RoomCapacity; room++)
            {
                if (!service.RequestDue(room) || !service.OwnsGuestTask(room, LocalWorker))
                    continue;
                bool needsPickup = service.RequestNeedsItem(room) && !service.RequestItemCollected(room);
                if (needsPickup && AtSupplyStorage(caretaker.Position) && service.RequestSuppliesAvailable(room))
                {
                    RequestGuestTask(room, CollectGuestItem);
                    return true;
                }
                if (!needsPickup && CanReachGuestTask(room, caretaker.Position))
                {
                    RequestGuestTask(room, DeliverGuestItem);
                    return true;
                }
            }
            return false;
        }

        private string GuestTaskAssignee(MotelService service, int room) =>
            service.OwnsGuestTask(room, LocalWorker) ? "Player" :
            service.RequestClaimed(room) ? "Coworker" :
            service.RequestSuppliesAvailable(room) && service.EmployeeEligible(StaffRole.GuestServices, service.RequestUrgent(room))
                ? StaffNames[(int)StaffRole.GuestServices] : "Unassigned";

        private string OperationAssignee(OperationTask task) => task.Worker.HasValue ?
            task.Worker == LocalWorker ? "Player" : "Coworker" :
            task.Role.HasValue ? StaffNames[(int)task.Role.Value] : "Unassigned";

        private bool ShowTask(OperationTask task, int now)
        {
            if (notebookTab == 4)
                return task.Closed;
            if (task.Closed)
                return false;
            bool upcoming = (task.Kind is OperationKind.Reception or OperationKind.Checkout) && task.Deadline > now;
            return notebookTab switch
            {
                1 => task.Worker == LocalWorker,
                2 => task.Role.HasValue,
                3 => upcoming || task.Kind == OperationKind.Delivery && task.Deadline > now,
                _ => !upcoming
            };
        }

        private static string OperationStatusLabel(OperationStatus status) => status == OperationStatus.InProgress
            ? "In progress" : status.ToString();

        private void RequestOperationOwner(OperationTask task, bool take)
        {
            Vector3 data = new Vector3(task.Code, task.Revision, take ? 1 : 0);
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(OperationOwnerAction, data);
            else
                HandleOperationOwner(LocalWorker, data);
        }

        private void HandleOperationOwner(ulong worker, Vector3 data)
        {
            if (!(data.x >= 0 && data.x < ((int)OperationKind.Incident + 1) * 128 &&
                data.y >= 0 && data.y <= 16777216 && (data.z == 0 || data.z == 1)) ||
                data.x != Mathf.Floor(data.x) || data.y != Mathf.Floor(data.y))
                return;

            MotelService service = Service;
            if (service.ChangeOperationOwner((int)data.x, (int)data.y, worker, data.z == 1))
                Service = service;
        }

        private static bool SameTask(OperationTask left, OperationTask right) =>
            left.Code == right.Code && left.Revision == right.Revision && left.Closed == right.Closed;

        private void DrawNotebook(Rect area, float scale)
        {
            MotelService service = Service;
            service.ReadOperations(notebookTasks, notebookTab == 4);
            if (inspectedTask.HasValue)
            {
                OperationTask selected = inspectedTask.Value;
                foreach (OperationTask candidate in notebookTasks)
                    if (SameTask(candidate, selected))
                    {
                        DrawOperationDetails(area, scale, service, candidate);
                        return;
                    }
                inspectedTask = null;
            }

            float tabWidth = area.width / TaskFilters.Length;
            for (int tab = 0; tab < TaskFilters.Length; tab++)
                if (BookingButton(new Rect(area.x + tab * tabWidth, area.y, tabWidth - 4 * scale, 29 * scale),
                    TaskFilters[tab], notebookTab == tab ? DeskPriority : DeskSurface))
                {
                    notebookTab = tab;
                    notebookScroll = Vector2.zero;
                }

            int count = 0;
            foreach (OperationTask entry in notebookTasks)
                if (ShowTask(entry, service.ClockMinute))
                    count++;
            if (count == 0)
            {
                DrawFittedText(new Rect(area.x, area.y + 42 * scale, area.width, 30 * scale), "No tasks in this view.", label);
                return;
            }

            float height = 64 * scale;
            float width = area.width - 19 * scale;
            var viewport = new Rect(area.x, area.y + 39 * scale, area.width, area.height - 39 * scale);
            notebookScroll = GUI.BeginScrollView(viewport, notebookScroll, new Rect(0, 0, width, count * height));
            int row = 0;
            foreach (OperationTask entry in notebookTasks)
            {
                if (!ShowTask(entry, service.ClockMinute))
                    continue;
                float y = row++ * height;
                DeskFrame(new Rect(0, y, width, height - 5 * scale), DeskSurface, DeskTrim, 5 * scale, 1);
                DrawFittedText(new Rect(8 * scale, y + 2 * scale, width - 110 * scale, 22 * scale), entry.Title, detail);
                DrawFittedText(new Rect(8 * scale, y + 24 * scale, width - 110 * scale, 16 * scale), entry.Location, label);
                string urgency = entry.Priority == 2 ? "Urgent" : entry.Priority == 1 ? "High" : "Normal";
                string summary = entry.Closed ? OperationStatusLabel(entry.Status) :
                    $"{OperationStatusLabel(entry.Status)} · {OperationAssignee(entry)} · {urgency}";
                DrawFittedText(new Rect(8 * scale, y + 40 * scale, width - 110 * scale, 16 * scale), summary, label);
                if (BookingButton(new Rect(width - 89 * scale, y + 13 * scale, 81 * scale, 30 * scale), "Details",
                    entry.Status == OperationStatus.Blocked ? DeskDanger : DeskCard))
                    inspectedTask = entry;
            }
            GUI.EndScrollView();
        }

        private void DrawOperationDetails(Rect area, float scale, MotelService service, OperationTask task)
        {
            Rect row = new Rect(area.x, area.y, area.width, 29 * scale);
            if (BookingButton(new Rect(row.x, row.y, 80 * scale, row.height), "Back", DeskSurface))
                inspectedTask = null;
            DrawFittedText(new Rect(row.x + 91 * scale, row.y, row.width - 91 * scale, row.height),
                OperationStatusLabel(task.Status), label);
            row.y += 32 * scale;
            DrawFittedText(row, task.Title, detail);
            row.y += 27 * scale;
            DrawFittedText(row, task.Location, label);
            row.y += 26 * scale;
            if (task.Closed)
                return;

            string assignment = task.Worker.HasValue || task.Role.HasValue ? $"Assigned to {OperationAssignee(task)}" : "Unassigned";
            DrawFittedText(row, $"{assignment} · {(task.Priority == 2 ? "Urgent" : task.Priority == 1 ? "High" : "Normal")} priority", label);
            row.y += 25 * scale;
            DrawFittedText(row, task.Deadline < 0 ? "No deadline" : $"Scheduled · {MotelService.DisplayTime(task.Deadline)}", label);
            row.y += 25 * scale;
            DrawFittedText(row, task.Resources, label);
            row.y += 25 * scale;
            DrawDeskSummary(row, scale, TaskNextStep(task));
            row.y += 39 * scale;
            float column = row.width / 3;
            if (BookingButton(new Rect(row.x, row.y, column - 4 * scale, row.height), "Open task", DeskPriority))
                OpenNotebookTask(task, service);
            bool isPinned = pinnedTask.HasValue && SameTask(pinnedTask.Value, task);
            if (BookingButton(new Rect(row.x + column, row.y, column - 4 * scale, row.height), isPinned ? "Unpin" : "Pin task", DeskSurface))
                pinnedTask = isPinned ? null : task;
            bool guestSupply = task.Kind == OperationKind.Guest && service.RequestDue(task.Target) &&
                service.RequestReference(task.Target).ScheduledMinute == task.Revision;
            if ((task.Claimable || guestSupply) && (!task.Worker.HasValue || task.Worker == LocalWorker))
            {
                bool take = !task.Worker.HasValue;
                if (BookingButton(new Rect(row.x + 2 * column, row.y, column, row.height), take ? task.Role.HasValue ? "Take over" : "Take task" : "Return to queue", DeskSurface))
                {
                    if (guestSupply)
                        RequestGuestTask(task.Target, take ? AcceptGuestTask : ReleaseGuestTask);
                    else
                        RequestOperationOwner(task, take);
                }
            }
            row.y += 34 * scale;
            if (isPinned && BookingButton(new Rect(row.x, row.y, row.width, row.height),
                showTaskWaypoint ? "Hide waypoint" : "Show waypoint", DeskSurface))
                showTaskWaypoint = !showTaskWaypoint;
        }

        private string TaskNextStep(OperationTask task)
        {
            if (task.Kind == OperationKind.Reception && GuestWaiting && task.Target == Occupied && deskCheckInApproved)
                return selectedKeys.ContainsKey(LocalWorker) ? "Hand the key to the waiting guest" : "Pick up the assigned key at reception";
            return task.Next;
        }

        private Vector3 TaskWaypoint(OperationTask task, MotelService service)
        {
            int room = task.Target;
            switch (task.Kind)
            {
                case OperationKind.Reception when GuestWaiting && room == Occupied && deskCheckInApproved:
                    return selectedKeys.ContainsKey(LocalWorker) ? guest.Position : KeyPickupPoint;
                case OperationKind.Phone: return MotelWorld.PhoneApproach;
                case OperationKind.Guest:
                    return service.RequestDue(room) && service.RequestNeedsItem(room) && !service.RequestItemCollected(room)
                        ? MotelWorld.SupplyStorage : GuestTaskDestination(room);
                case OperationKind.Cleaning:
                    return service.CartInUse && service.CartCarrier == LocalWorker ? MotelWorld.RoomApproach(room) : MotelWorld.CartStation;
                case OperationKind.Delivery:
                    return task.Status == OperationStatus.InProgress ? MotelWorld.SupplyStorage : MotelWorld.DeliveryBay;
                case OperationKind.Laundry: return MotelWorld.LaundryApproach;
                case OperationKind.Maintenance: return InspectionPoint(room % MotelService.RoomCount, (Fixture)(room / MotelService.RoomCount));
                case OperationKind.Luggage: return service.Bag == LuggageStatus.InCar ? MotelWorld.CarApproach(room) : MotelWorld.BagDrop(room);
                case OperationKind.LostProperty: return MotelWorld.RoomApproach(room);
                default: return MotelWorld.ComputerApproach;
            }
        }

        private void DrawPinnedOperation(Rect screen, float scale, float margin)
        {
            taskWaypointPanel = default;
            if (!pinnedTask.HasValue)
                return;
            MotelService service = Service;
            service.ReadOperations(waypointTasks);
            foreach (OperationTask task in waypointTasks)
            {
                if (!SameTask(task, pinnedTask.Value))
                    continue;
                Vector3 destination = TaskWaypoint(task, service);
                if (showTaskWaypoint && DrawWorldButton(destination + Vector3.up * 1.10f, "Pinned task", 112 * scale, scale))
                    OpenNotebookTask(task, service);
                float width = Mathf.Min(335 * scale, screen.width - 2 * margin);
                float y = guidePanel.height > 0 ? guidePanel.yMax + 6 * scale : margin + (lan == null ? 53 : 89) * scale;
                taskWaypointPanel = new Rect(screen.xMax - margin - width, y, width, 55 * scale);
                DeskFrame(taskWaypointPanel, DeskShell, DeskGold, 5 * scale, 1);
                DrawFittedText(new Rect(taskWaypointPanel.x + 8 * scale, taskWaypointPanel.y + 2 * scale, width - 16 * scale, 24 * scale),
                    $"{task.Title} · {Mathf.RoundToInt(Vector3.Distance(caretaker.Position, destination))} m", detail);
                if (BookingButton(new Rect(taskWaypointPanel.x + 6 * scale, taskWaypointPanel.y + 27 * scale, width - 12 * scale, 23 * scale),
                    TaskNextStep(task), DeskSurface))
                    OpenNotebookTask(task, service);
                return;
            }
            pinnedTask = null;
        }

        private void TaskDestination(Vector3 destination)
        {
            SetReceptionPanel(ReceptionPanel.None);
            if (computerOpen)
                SetComputerVisible(false);
            SetDestination(destination);
        }

        private void OpenNotebookTask(OperationTask task, MotelService service)
        {
            int room = task.Target;
            switch (task.Kind)
            {
                case OperationKind.Reception:
                case OperationKind.Approval:
                case OperationKind.Checkout:
                    if (task.Kind == OperationKind.Reception && GuestWaiting && task.Target == Occupied && deskCheckInApproved)
                    {
                        if (!selectedKeys.ContainsKey(LocalWorker))
                        {
                            if (AtKeyCounter(caretaker.Position))
                                SetReceptionPanel(ReceptionPanel.Keys);
                            else
                                TaskDestination(KeyPickupPoint);
                        }
                        else
                            TaskDestination(guest.Position + Vector3.left * .60f);
                    }
                    else if (AtComputer(caretaker.Position))
                    {
                        SetReceptionPanel(ReceptionPanel.None);
                        OpenDeskReservations();
                        if (task.Kind == OperationKind.Checkout)
                        {
                            reservationView = ReservationView.Departures;
                            SelectStay(room);
                        }
                        else
                        {
                            reservationView = task.Kind == OperationKind.Approval ? ReservationView.Requests : ReservationView.Arrivals;
                            for (int index = 0; index < service.ReservationCount; index++)
                                if (service.ReservationRoom(index) == task.Target && service.ReservationStart(index) == task.Revision)
                                {
                                    SelectBooking(service, index);
                                    break;
                                }
                        }
                    }
                    else
                        TaskDestination(MotelWorld.ComputerApproach);
                    break;
                case OperationKind.Phone:
                    if (AtPhone(caretaker.Position))
                        OpenPhone();
                    else
                        TaskDestination(MotelWorld.PhoneApproach);
                    break;
                case OperationKind.Guest:
                    OpenGuestMenu(room);
                    break;
                case OperationKind.Cleaning:
                    if (MotelWorld.InteriorRoom(caretaker.Position) == room)
                        SetReceptionPanel(ReceptionPanel.None);
                    else
                        TaskDestination(TaskWaypoint(task, service));
                    break;
                case OperationKind.Laundry:
                    TaskDestination(MotelWorld.LaundryApproach);
                    break;
                case OperationKind.Delivery:
                    if (task.Deadline <= service.ClockMinute)
                    {
                        if (!RequestSupplyDelivery(task.Revision))
                            TaskDestination(TaskWaypoint(task, service));
                    }
                    else if (AtComputer(caretaker.Position))
                        OpenComputer(SuppliesTab, trackOrders: true);
                    else
                        TaskDestination(MotelWorld.ComputerApproach);
                    break;
                case OperationKind.Stock:
                    if (task.Target >= MotelService.Catalog.Length)
                        GoToComputer(ReportsTab);
                    else
                        OpenRestock(task.Target);
                    break;
                case OperationKind.Maintenance:
                    var fixture = (Fixture)(room / MotelService.RoomCount);
                    room %= MotelService.RoomCount;
                    if (AtFixture(room, fixture, caretaker.Position))
                        InspectFixture(room, fixture);
                    else
                        TaskDestination(InspectionPoint(room, fixture));
                    break;
                case OperationKind.Luggage:
                    TaskDestination(service.Bag == LuggageStatus.InCar ? MotelWorld.CarApproach(room) : MotelWorld.BagDrop(room));
                    break;
                case OperationKind.LostProperty:
                    TaskDestination(MotelWorld.RoomApproach(room));
                    break;
                case OperationKind.Decision:
                case OperationKind.Incident:
                case OperationKind.Meal:
                    if (!AtComputer(caretaker.Position))
                    {
                        TaskDestination(MotelWorld.ComputerApproach);
                        break;
                    }
                    SetReceptionPanel(ReceptionPanel.None);
                    OpenComputer(task.Kind == OperationKind.Decision ? ReservationsTab : task.Kind == OperationKind.Meal ? ExpansionTab : ReportsTab);
                    if (task.Kind == OperationKind.Decision)
                        reservationView = ReservationView.Decisions;
                    break;
            }
        }

        private static string GuestTaskTitle(MotelService service, int room) => service.KindOfRequest(room) switch
        {
            GuestRequestKind.Towel => $"Deliver {service.RequiredRequestUnits(room)} clean {(service.RequiredRequestUnits(room) == 1 ? "towel" : "towels")}",
            GuestRequestKind.Pillow => "Deliver an extra pillow",
            GuestRequestKind.Toiletries => "Deliver toiletries",
            _ => service.RequestLabel(room)
        };

        private void OpenRestock(int item)
        {
            if (computerOpen)
                SetComputerVisible(false);
            reorderItem = item;
            quickOrderPending = false;
            quickOrderStatus = null;
            SetReceptionPanel(ReceptionPanel.Restock);
        }

        private void HandleQuickOrder(ulong worker, float itemNumber)
        {
            if (!(itemNumber >= 0 && itemNumber < MotelService.Catalog.Length) || itemNumber != Mathf.Floor(itemNumber))
                return;
            MotelService service = Service;
            bool success = service.Order((int)itemNumber);
            if (success)
                Service = service;
            SendBookingResult(worker, QuickOrderAction, success, new Vector3(itemNumber, service.ActiveOrderNumber, 0));
        }

        private void DrawQuickRestock(Rect area, float scale)
        {
            MotelService service = Service;
            SupplyOffer offer = MotelService.Catalog[reorderItem];
            DrawDeskSummary(new Rect(area.x, area.y, area.width, 43 * scale), scale,
                $"{offer.Name} ×{offer.Quantity} · ${offer.Price}");
            DrawFittedText(new Rect(area.x, area.y + 52 * scale, area.width, 27 * scale),
                $"{service.Stock(reorderItem)} on shelves · {service.IncomingStock(reorderItem)} incoming", detail);
            DrawFittedText(new Rect(area.x, area.y + 82 * scale, area.width, 27 * scale),
                $"Delivery in {MotelService.StandardDeliveryMinutes} motel minutes · {service.PendingOrderCount} open orders", label);
            GUI.enabled = !quickOrderPending && service.Cash >= offer.Price && service.PendingOrderCount < 9 &&
                (reorderItem != 4 || service.Stock(4) + service.IncomingStock(4) == 0);
            if (BookingButton(new Rect(area.x, area.y + 118 * scale, area.width, 34 * scale),
                quickOrderPending ? "Placing order…" : $"Order one pack · ${offer.Price}", DeskPriority))
            {
                quickOrderPending = true;
                if (lan != null && !lan.IsHost)
                    lan.State?.ActionRpc(QuickOrderAction, new Vector3(reorderItem, 0, 0));
                else
                    HandleQuickOrder(LocalWorker, reorderItem);
            }
            GUI.enabled = true;
            if (quickOrderStatus != null)
                DrawFittedText(new Rect(area.x, area.y + 159 * scale, area.width, 28 * scale), quickOrderStatus, label);
            if (BookingButton(new Rect(area.x, area.y + 196 * scale, area.width, 31 * scale), "Back to tasks", DeskSurface))
                SetReceptionPanel(ReceptionPanel.Notebook);
        }

        private void DrawGuestTask(Rect area, float scale, MotelService service, int room)
        {
            bool due = service.RequestDue(room);
            bool mine = service.OwnsGuestTask(room, LocalWorker);
            bool item = service.RequestNeedsItem(room);
            bool collected = service.RequestItemCollected(room);
            bool shortage = item && !service.RequestSuppliesAvailable(room);

            DeskFrame(area, DeskCard, DeskTrim, 7 * scale, 1);
            Rect body = InsetDesk(area, 11 * scale);
            DrawFittedText(new Rect(body.x, body.y, body.width, 26 * scale),
                due ? GuestTaskTitle(service, room) : service.RequestSatisfied(room) ? "Request completed" : "No open supply request", detail);
            DrawFittedText(new Rect(body.x, body.y + 28 * scale, body.width, 22 * scale),
                $"{service.StayGuest(room)} · Room {room + 1}", label);

            if (due)
            {
                DrawChecklistRow(new Rect(body.x, body.y + 57 * scale, body.width, 23 * scale),
                    "Accept customer request", service.RequestClaimed(room), scale);
                if (item)
                    DrawChecklistRow(new Rect(body.x, body.y + 82 * scale, body.width, 23 * scale),
                        $"Collect {service.RequiredRequestUnits(room)} from housekeeping storage", collected, scale);
                DrawChecklistRow(new Rect(body.x, body.y + (item ? 107 : 82) * scale, body.width, 23 * scale),
                    item ? $"Deliver to {service.StayGuest(room)}" : "Talk to the guest", false, scale);

                string status = $"Waiting {service.RequestWait(room)} min · Assigned to: {GuestTaskAssignee(service, room)}";
                DrawFittedText(new Rect(body.x, body.y + 141 * scale, body.width, 21 * scale), status, label);
                if (item)
                    DrawFittedText(new Rect(body.x, body.y + 164 * scale, body.width, 21 * scale),
                        shortage ? $"Waiting for supplies · {service.RequestStock(room)} in stock" :
                            $"{service.RequestStock(room)} in stock", label);
            }

            var actions = new Rect(body.x, body.y + 194 * scale, body.width, 29 * scale);
            if (due && !service.RequestClaimed(room) && BookingButton(actions, "Accept request", DeskPriority))
                RequestGuestTask(room, AcceptGuestTask);
            else if (due && mine)
            {
                Rect next = new Rect(actions.x, actions.y, actions.width - 99 * scale, actions.height);
                if (BookingButton(next, item && !collected ? shortage ? "Order supplies" : "Collect from shelves" :
                    item ? "Deliver to guest" : "Talk to guest", DeskPriority))
                    AdvanceGuestTaskFromPanel(service, room);
                if (BookingButton(new Rect(next.xMax + 4 * scale, actions.y, 95 * scale, actions.height),
                    "Unassign", DeskSurface))
                    RequestGuestTask(room, ReleaseGuestTask);
            }

            GuestIssue issue = service.OutstandingIssue(room);
            if (issue != GuestIssue.None)
            {
                Rect action = new Rect(body.x, body.y + 230 * scale, body.width, 29 * scale);
                if (BookingButton(action, IssueActionLabel(issue), DeskSurface))
                {
                    if (NearActiveGuest(room, caretaker.Position))
                        RequestPropertyCare(8, room, (Fixture)issue);
                    else
                        TaskDestination(GuestTaskDestination(room));
                }
            }
        }

        private Vector3 GuestTaskDestination(int room)
        {
            MotelWalker actor = visitorRoom == room ? serviceVisitor : backgroundGuests[room];
            if (actor == null || MotelWorld.InteriorRoom(actor.Position) == room &&
                MotelWorld.InteriorRoom(caretaker.Position) != room && (Service.OpenDoors & (1 << room)) == 0)
                return MotelWorld.RoomApproach(room);
            return actor.Position;
        }

        private void AdvanceGuestTaskFromPanel(MotelService service, int room)
        {
            if (service.RequestNeedsItem(room) && !service.RequestItemCollected(room))
            {
                if (!service.RequestSuppliesAvailable(room))
                {
                    int item = service.KindOfRequest(room) == GuestRequestKind.Towel ? 1 :
                        service.KindOfRequest(room) == GuestRequestKind.Pillow ? 6 : 7;
                    OpenRestock(item);
                }
                else if (AtSupplyStorage(caretaker.Position))
                    RequestGuestTask(room, CollectGuestItem);
                else
                    TaskDestination(MotelWorld.SupplyStorage);
            }
            else if (CanReachGuestTask(room, caretaker.Position))
                RequestGuestTask(room, DeliverGuestItem);
            else
                TaskDestination(GuestTaskDestination(room));
        }

    }
}
