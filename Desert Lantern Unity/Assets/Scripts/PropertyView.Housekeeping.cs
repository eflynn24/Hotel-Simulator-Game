using UnityEngine;

namespace DesertLantern
{
    public sealed partial class PropertyView
    {
        private const byte CleanRoomAction = 14;
        private const byte LaundryAction = 15;
        private const float CleaningReach = 1.06f;
        private CleaningTask heldCleaningTool;
        private float cleaningToolUntil;
        private Rect housekeepingPanel;
        private int lastCleanedRoom = NoRoom;
        private float checklistVisibleUntil;

        private static readonly string[] CleaningLabels =
        {
            "Remove garbage", "Wipe surfaces", "Vacuum the carpet", "Remove used bed linens",
            "Make beds with clean linens", "Mop the hard floor", "Clean bathroom and toilet",
            "Collect dirty towels", "Restock towels and toiletries"
        };

        private static readonly CleaningTask[] ChecklistOrder =
        {
            CleaningTask.Trash, CleaningTask.Towels, CleaningTask.Strip, CleaningTask.MakeBeds,
            CleaningTask.Bathroom, CleaningTask.Wipe, CleaningTask.Vacuum, CleaningTask.Mop, CleaningTask.Restock
        };

        private static bool NearLaundry(Vector3 point) =>
            MotelWorld.NearFloor(point, MotelWorld.LaundryApproach) &&
            MotelWalker.Distance(point, MotelWorld.LaundryApproach) < .66f;

        private void HandleCleaning(ulong worker, int room, CleaningTask task, Vector3 position)
        {
            if (MotelWorld.InteriorRoom(position) != room ||
                MotelWalker.Distance(position, MotelWorld.CleaningPoint(room, task)) > CleaningReach)
                return;
            MotelService service = Service;
            service.CleanFor(room, task, worker);
            Service = service;
        }

        private void HandleLaundry(Vector3 position, ulong worker)
        {
            if (!NearLaundry(position))
                return;
            MotelService service = Service;
            if (service.CanWorkOperation(OperationKind.Laundry, 0, 0, worker))
            {
                service.StartLaundry();
                Service = service;
            }
        }

        private void RequestLaundry()
        {
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(LaundryAction, Vector3.zero);
            else
                HandleLaundry(caretaker.Position, LocalWorker);
        }

        private bool RequestClean(int room, CleaningTask task)
        {
            Vector3 position = caretaker.Position;
            if (MotelWorld.InteriorRoom(position) != room ||
                MotelWalker.Distance(position, MotelWorld.CleaningPoint(room, task)) > CleaningReach)
                return false;
            if (!Service.CanCleanFor(room, task, LocalWorker))
                return true;
            heldCleaningTool = task;
            cleaningToolUntil = Time.unscaledTime + .95f;
            lastCleanedRoom = room;
            checklistVisibleUntil = Time.unscaledTime + 6f;
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(CleanRoomAction, new Vector3(room, (int)task, 0));
            else
                HandleCleaning(LocalWorker, room, task, position);
            return true;
        }

        private bool TryNearbyCleaningTask(int room, out CleaningTask task)
        {
            MotelService service = Service;
            task = default;
            if (room == NoRoom || !service.RoomDirty(room))
                return false;
            float closest = CleaningReach;
            bool found = false;
            for (int i = 0; i < MotelService.CleaningTaskCount; i++)
            {
                var candidate = (CleaningTask)i;
                if (!service.TaskPending(room, candidate) ||
                    (candidate == CleaningTask.MakeBeds && service.TaskPending(room, CleaningTask.Strip)) ||
                    (candidate == CleaningTask.Restock && service.TaskPending(room, CleaningTask.Towels)))
                    continue;
                float distance = MotelWalker.Distance(caretaker.Position, MotelWorld.CleaningPoint(room, candidate));
                if (distance >= closest)
                    continue;
                task = candidate;
                closest = distance;
                found = true;
            }
            return found;
        }

        private string CleaningRequirement(int room, CleaningTask task)
        {
            MotelService service = Service;
            if (service.DoNotDisturb(room))
                return "Do not disturb";
            if ((service.ActiveRooms & (1 << room)) != 0 && !service.HousekeepingRequested(room))
                return "Guest has declined housekeeping";
            if ((!service.CartInUse || service.CartCarrier != LocalWorker) &&
                task is CleaningTask.Trash or CleaningTask.Wipe or CleaningTask.Vacuum or CleaningTask.MakeBeds or
                    CleaningTask.Bathroom or CleaningTask.Restock)
                return "Load the cart in Housekeeping";
            return task switch
            {
                CleaningTask.Trash => "Needs a trash bag",
                CleaningTask.Wipe => "Needs a reusable cloth kit",
                CleaningTask.Bathroom => "Needs disinfectant",
                CleaningTask.Vacuum => service.TaskPending(room, CleaningTask.Trash)
                    ? "Clear trash first" : "Needs a vacuum",
                CleaningTask.MakeBeds => service.TaskPending(room, CleaningTask.Strip)
                    ? "Strip beds first" : "Needs 2 clean linen sets",
                CleaningTask.Restock => service.TaskPending(room, CleaningTask.Towels)
                    ? "Collect dirty towels first" : "Reload towels and toiletries",
                _ => ""
            };
        }

        private bool RequestHousekeeping()
        {
            if (NearLaundry(caretaker.Position) &&
                (Service.LaundryRunning || Service.DirtyLinens + Service.DirtyTowels > 0))
            {
                RequestLaundry();
                return true;
            }
            if (AtHousekeepingCart(caretaker.Position) && (!Service.CartInUse || Service.CartCarrier == LocalWorker))
            {
                RequestPropertyCare(7);
                return true;
            }
            int room = MotelWorld.InteriorRoom(caretaker.Position);
            return TryNearbyCleaningTask(room, out CleaningTask task) && RequestClean(room, task);
        }

        private string LaundryDescription(MotelService service)
        {
            if (service.WasherBroken)
                return "Laundry · Washer needs repair";
            if (service.LaundryRunning)
                return $"Laundry washing  ·  {service.DirtyLinens} linens and {service.DirtyTowels} towels waiting";
            if (service.DirtyLinens + service.DirtyTowels == 0)
                return "Laundry  ·  Hamper empty";
            return service.Detergent == 0 ? "Laundry  ·  Order detergent" :
                $"E  ·  Wash laundry ({service.DirtyLinens} linens, {service.DirtyTowels} towels)";
        }

        private string HousekeepingObjective()
        {
            if (NearLaundry(caretaker.Position))
                return LaundryDescription(Service);
            int room = MotelWorld.InteriorRoom(caretaker.Position);
            if (TryNearbyCleaningTask(room, out CleaningTask task))
                return Service.CanCleanFor(room, task, LocalWorker) ? $"E  ·  {CleaningLabels[(int)task]}" : CleaningRequirement(room, task);
            return room != NoRoom && Service.RoomDirty(room) ? $"Room {room + 1} needs housekeeping" : null;
        }

        private static bool AtHousekeepingCart(Vector3 position) =>
            MotelWorld.InsideUtility(position) && MotelWalker.Distance(position, MotelWorld.CartStation) < .85f;

        private void DrawChecklistRow(Rect row, string caption, bool done, float scale)
        {
            Rect icon = new Rect(row.x + 5 * scale, row.center.y - 6 * scale, 12 * scale, 12 * scale);
            DeskFrame(icon, done ? DeskAvailable : DeskCard, done ? DeskAvailable : DeskTrim, 6 * scale, 1);
            var text = new Rect(row.x + 24 * scale, row.y, row.width - 29 * scale, row.height);
            DrawFittedText(text, caption, label);
            if (done)
            {
                Fill(new Rect(icon.center.x - 3 * scale, icon.center.y, 3 * scale, 2 * scale), DeskPaper);
                Fill(new Rect(icon.center.x - scale, icon.center.y - 2 * scale, 4 * scale, 2 * scale), DeskPaper);
                float strikeWidth = Mathf.Min(text.width, label.CalcSize(new GUIContent(caption)).x);
                Fill(new Rect(text.x, text.center.y, strikeWidth, scale), DeskTrim);
            }
        }

        private void DrawHousekeeping(Rect screen, float margin, float scale)
        {
            housekeepingPanel = default;
            MotelService service = Service;
            int inside = MotelWorld.InteriorRoom(caretaker.Position);
            bool nearStation = MotelWalker.Distance(caretaker.Position, MotelWorld.CartStation) < 7f;
            if (nearStation && (!service.CartInUse || service.CartCarrier == LocalWorker) &&
                DrawWorldButton(MotelWorld.CartStation + Vector3.up * .93f,
                    service.CartInUse ? "Return housekeeping cart" : "Load housekeeping cart", 178 * scale, scale))
            {
                if (AtHousekeepingCart(caretaker.Position))
                    RequestPropertyCare(7);
                else
                    SetDestination(MotelWorld.CartStation);
            }
            if (nearStation && (service.LaundryRunning || service.DirtyLinens + service.DirtyTowels > 0) &&
                DrawWorldButton(MotelWorld.LaundryApproach + Vector3.up * 1.43f, "Laundry", 104 * scale, scale))
            {
                if (NearLaundry(caretaker.Position))
                    RequestLaundry();
                else
                    SetDestination(MotelWorld.LaundryApproach);
            }
            if (inside != NoRoom && (service.RoomDirty(inside) ||
                inside == lastCleanedRoom && Time.unscaledTime < checklistVisibleUntil))
            {
                float width = Mathf.Min(320 * scale, screen.width - margin * 2);
                float top = margin + (lan == null ? 53 : 89) * scale;
                housekeepingPanel = new Rect(margin, top, width, (126 + ChecklistOrder.Length * 25) * scale);
                DeskFrame(housekeepingPanel, DeskShell, DeskTrim, 7 * scale, 1);
                Rect content = InsetDesk(housekeepingPanel, 10 * scale);
                int cleanliness = service.Cleanliness(inside);
                DrawFittedText(new Rect(content.x, content.y, content.width, 23 * scale),
                    $"Room {inside + 1} · Housekeeping", detail);
                int arrival = service.NextConfirmedArrival(inside);
                DrawFittedText(new Rect(content.x, content.y + 23 * scale, content.width, 20 * scale),
                    arrival < 0 ? "No upcoming arrival" : $"Next arrival · {MotelService.DisplayTime(arrival)}", label);
                DrawFittedText(new Rect(content.x, content.y + 44 * scale, content.width, 19 * scale),
                    cleanliness == 100 ? "Room ready" : $"Room preparation · {cleanliness}%", label);
                Rect bar = new Rect(content.x, content.y + 66 * scale, content.width, 5 * scale);
                DeskRound(bar, DeskTrim, 2 * scale, 0);
                bar.width *= cleanliness / 100f;
                if (bar.width > 0)
                    DeskRound(bar, DeskPriority, 2 * scale, 0);
                for (int index = 0; index < ChecklistOrder.Length; index++)
                {
                    CleaningTask task = ChecklistOrder[index];
                    bool pending = service.TaskPending(inside, task);
                    Rect row = new Rect(content.x, content.y + (77 + index * 25) * scale, content.width, 23 * scale);
                    bool canClean = pending && service.CanCleanFor(inside, task, LocalWorker);
                    if (canClean && row.Contains(Event.current.mousePosition))
                        DeskRound(row, DeskSurface, 3 * scale, 0);
                    DrawChecklistRow(row, CleaningLabels[(int)task], !pending, scale);
                    if (canClean && GUI.Button(row, GUIContent.none, GUIStyle.none) && !RequestClean(inside, task))
                        SetDestination(MotelWorld.CleaningPoint(inside, task));
                }

                string status = TryNearbyCleaningTask(inside, out CleaningTask nearby) &&
                    !service.CanCleanFor(inside, nearby, LocalWorker) ? CleaningRequirement(inside, nearby) : "";
                if (status.Length == 0 && !service.CartInUse && service.RoomDirty(inside))
                    status = "Load the cart in Housekeeping";
                DrawFittedText(new Rect(content.x, content.yMax - 24 * scale, content.width, 22 * scale), status, label);
                return;
            }

            for (int room = 0; room < Guests.Length; room++)
            {
                if (!service.RoomDirty(room) || (escorting && room == Occupied))
                    continue;
                if (DrawWorldButton(MotelWorld.RoomSign(room), service.DoNotDisturb(room) ? "Do not disturb" : service.HousekeepingRequested(room) ? "Housekeeping requested" : $"Room {room + 1} dirty", 132 * scale, scale))
                {
                    if (MotelWorld.NearFloor(caretaker.Position, MotelWorld.RoomDoor(room)) &&
                        MotelWalker.Distance(caretaker.Position, MotelWorld.RoomDoor(room)) <= 1.5f &&
                        (service.OpenDoors & (1 << room)) == 0)
                        RequestDoor(room);
                    else
                        SetDestination((service.OpenDoors & (1 << room)) == 0
                            ? MotelWorld.RoomApproach(room) : MotelWorld.CleaningPoint(room, CleaningTask.Wipe));
                }
            }
        }
    }
}
