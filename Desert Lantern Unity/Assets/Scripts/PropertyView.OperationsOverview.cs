using System.Collections.Generic;
using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const string GuidePreference = "DesertLantern.HideFirstDayGuide";
        private readonly List<OperationTask> dashboardTasks = new List<OperationTask>();
        private Vector2 dashboardScroll;
        private int queuedComputerTab = -1;
        private ReservationView queuedBookingView;
        private Rect guidePanel;
        private bool? guideHidden;

        private void GoToComputer(int tab, ReservationView reservations = ReservationView.Calendar)
        {
            if (receptionPanel != ReceptionPanel.None)
                SetReceptionPanel(ReceptionPanel.None);
            if (AtComputer(caretaker.Position))
            {
                OpenComputer(tab);
                reservationView = reservations;
                return;
            }
            queuedComputerTab = tab;
            queuedBookingView = reservations;
            SetDestination(MotelWorld.ComputerApproach);
        }

        private void CheckComputerDestination()
        {
            if (queuedComputerTab < 0 || caretaker == null || Modal || !AtComputer(caretaker.Position))
                return;
            reservationView = queuedBookingView;
            OpenComputer(queuedComputerTab);
        }

        private void DrawOperationsOverview(Rect area, float scale)
        {
            MotelService service = Service;
            service.ReadOperations(dashboardTasks);
            int ready = 0;
            int shortages = 0;
            int readinessWork = 0;
            int employeeCount = 0;
            for (int room = 0; room < service.RoomCapacity; room++)
            {
                if (service.RoomReadyForGuest(room))
                    ready++;
                if (service.RoomDirty(room) || service.RoomUnusable(room))
                    readinessWork++;
            }
            for (int item = 0; item < MotelService.Catalog.Length; item++)
                if (service.LowStock(item))
                    shortages++;
            if (service.Disinfectant < 2)
                shortages++;
            if (service.RepairParts < 2)
                shortages++;
            for (int role = 0; role < StaffNames.Length; role++)
                if (service.StaffHired(role))
                    employeeCount++;

            float w = area.width / 4;
            float h = 61 * scale;
            Rect tile = new Rect(area.x, area.y, w - 6 * scale, h);
            if (OverviewTile(tile, scale, "Today’s arrivals", CountArrivals(service).ToString(), 1))
                OpenOverviewBookings(ReservationView.Arrivals);
            tile.x += w;
            if (OverviewTile(tile, scale, "Today’s departures", CountDepartures(service).ToString(), 1))
                OpenOverviewBookings(ReservationView.Departures);
            tile.x += w;
            if (OverviewTile(tile, scale, "Rooms ready", $"{ready}/{service.RoomCapacity} · {readinessWork} need work", 4))
                OpenOverviewBookings(ReservationView.Calendar);
            tile.x += w;
            if (OverviewTile(tile, scale, "Guest requests", CountRequests(service).ToString(), 2))
                OpenFirstOperation(OperationKind.Guest);

            tile.x = area.x;
            tile.y += h + 6 * scale;
            if (OverviewTile(tile, scale, "Low-stock products", shortages.ToString(), 0))
                OpenComputer(SuppliesTab);
            tile.x += w;
            if (OverviewTile(tile, scale, "Open supply orders", $"{service.PendingOrderCount} · {service.ReadyOrderCount} ready", 0))
                OpenComputer(SuppliesTab, trackOrders: true);
            tile.x += w;
            if (OverviewTile(tile, scale, "Operating cash", $"${service.Cash:N0} · ${service.LoanBalance} loan", 3))
                OpenComputer(ReportsTab);
            tile.x += w;
            if (OverviewTile(tile, scale, "Employees", $"{employeeCount}/{StaffNames.Length}", 2))
                OpenComputer(StaffTab);

            var header = new Rect(area.x, area.y + 140 * scale, area.width, 29 * scale);
            DrawFittedText(header, "Requires attention", detail);
            if (BookingButton(new Rect(header.xMax - 125 * scale, header.y, 119 * scale, header.height), "Task queue", DeskSurface))
            {
                notebookTab = 0;
                OpenComputer(TasksTab);
            }

            Rect viewport = new Rect(area.x, area.y + 174 * scale, area.width, area.height - 219 * scale);
            int active = 0;
            foreach (OperationTask task in dashboardTasks)
                if (NeedsAttention(task, service.ClockMinute))
                    active++;
            if (active == 0)
                DrawFittedText(viewport, "Operations are up to date.", label);
            else
            {
                float rowHeight = 44 * scale;
                float width = viewport.width - 19 * scale;
                dashboardScroll = GUI.BeginScrollView(viewport, dashboardScroll, new Rect(0, 0, width, rowHeight * active));
                int row = 0;
                foreach (OperationTask task in dashboardTasks)
                {
                    if (!NeedsAttention(task, service.ClockMinute))
                        continue;
                    float y = row++ * rowHeight;
                    DeskFrame(new Rect(0, y, width, rowHeight - 4 * scale), DeskCard, DeskTrim, 5 * scale, 1);
                    DrawFittedText(new Rect(9 * scale, y + 1 * scale, width - 155 * scale, 20 * scale), task.Title, detail);
                    DrawFittedText(new Rect(9 * scale, y + 19 * scale, width - 155 * scale, 20 * scale),
                        $"{OperationStatusLabel(task.Status)} · {TaskNextStep(task)}", label);
                    if (BookingButton(new Rect(width - 135 * scale, y + 5 * scale, 128 * scale, 30 * scale), "Review",
                        task.Status == OperationStatus.Blocked ? DeskDanger : DeskSurface))
                        ReviewDashboardTask(task);
                }
                GUI.EndScrollView();
            }

            Rect goal = new Rect(area.x, area.yMax - 35 * scale, area.width - 6 * scale, 30 * scale);
            if (BookingButton(goal, employeeCount == StaffNames.Length ? "Fully staffed · Review property expansion" :
                $"County goal · Staff the motel to unlock the next property ({employeeCount}/{StaffNames.Length})", DeskSurface))
                OpenComputer(employeeCount == StaffNames.Length ? ExpansionTab : StaffTab);
        }

        private bool OverviewTile(Rect rect, float scale, string caption, string value, int icon)
        {
            DeskFrame(rect, DeskCard, DeskTrim, 5 * scale, 1);
            DrawComputerIcon(new Rect(rect.x + 9 * scale, rect.y + 11 * scale, 14 * scale, 14 * scale), icon, DeskPriority);
            DrawFittedText(new Rect(rect.x + 31 * scale, rect.y + 3 * scale, rect.width - 37 * scale, 24 * scale), caption, label);
            DrawFittedText(new Rect(rect.x + 10 * scale, rect.y + 27 * scale, rect.width - 18 * scale, 28 * scale), value, detail);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private static bool NeedsAttention(OperationTask task, int now) =>
            (task.Kind is not OperationKind.Reception and not OperationKind.Checkout || task.Deadline <= now) &&
            (task.Kind != OperationKind.Delivery || task.Deadline <= now);

        private void OpenOverviewBookings(ReservationView target)
        {
            OpenComputer(ReservationsTab);
            reservationView = target;
            rosterOffset = 0;
            reservationScroll = Vector2.zero;
        }

        private void OpenFirstOperation(OperationKind kind)
        {
            OpenComputer(TasksTab);
            notebookTab = 0;
            foreach (OperationTask task in dashboardTasks)
                if (task.Kind == kind)
                {
                    inspectedTask = task;
                    return;
                }
        }

        private void ReviewDashboardTask(OperationTask task)
        {
            if (task.Kind is OperationKind.Approval or OperationKind.Reception or OperationKind.Checkout or
                OperationKind.Decision or OperationKind.Incident or OperationKind.Meal)
                OpenNotebookTask(task, Service);
            else if (task.Kind is OperationKind.Stock or OperationKind.Delivery)
                OpenComputer(task.Kind == OperationKind.Stock && task.Target >= MotelService.Catalog.Length
                    ? ReportsTab : SuppliesTab, task.Kind == OperationKind.Delivery);
            else
            {
                OpenComputer(TasksTab);
                notebookTab = 0;
                inspectedTask = task;
            }
        }

        private void DrawFirstDayGuide(Rect screen, float scale, float margin)
        {
            guidePanel = default;
            if (Modal || Service.ClockMinute >= MotelService.MinutesPerDay)
                return;
            guideHidden ??= PlayerPrefs.GetInt(GuidePreference) != 0;
            if (guideHidden.Value)
                return;

            MotelService service = Service;
            string hint;
            if (service.PendingOrderCount > 0)
                hint = "Use the notebook to track crates and find the shelves in Housekeeping.";
            else if (service.CompletedOrderCount == 0 && service.Towels <= MotelService.MinimumStock(1))
                hint = "Start with supplies. Open the notebook to reorder and track deliveries.";
            else if (GuestWaiting)
                hint = "Check in your guest at reception. Pick up the assigned key and hand it over.";
            else if (service.VisitedRooms != 0)
                hint = "Use the notebook for guest requests and the room checklist after checkout.";
            else
                hint = "Check upcoming stays in Bookings. Press E at the reception computer.";

            float width = Mathf.Min(440 * scale, screen.width - 2 * margin);
            guidePanel = new Rect(screen.xMax - margin - width, margin + 101 * scale, width, 39 * scale);
            DeskFrame(guidePanel, DeskShell, DeskTrim, 5 * scale, 1);
            var open = new Rect(guidePanel.x + 5 * scale, guidePanel.y + 4 * scale, width - 37 * scale, 31 * scale);
            if (BookingButton(open, hint, DeskSurface))
                SetReceptionPanel(ReceptionPanel.Notebook);
            if (BookingButton(new Rect(open.xMax + 3 * scale, open.y, 27 * scale, open.height), "×", DeskSurface))
            {
                guideHidden = true;
                PlayerPrefs.SetInt(GuidePreference, 1);
            }
        }
    }
}
