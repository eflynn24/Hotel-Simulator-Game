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

        private static readonly string[] CleaningLabels =
            { "Bag the trash", "Wipe the table", "Vacuum the carpet", "Strip the beds", "Make the beds", "Mop the hard floor", "Disinfect the bathroom", "Collect used towels" };

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

        private void HandleLaundry(Vector3 position)
        {
            if (!NearLaundry(position))
                return;
            MotelService service = Service;
            service.StartLaundry();
            Service = service;
        }

        private void RequestLaundry()
        {
            if (lan != null && !lan.IsHost)
                lan.State?.ActionRpc(LaundryAction, Vector3.zero);
            else
                HandleLaundry(caretaker.Position);
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
                    (candidate == CleaningTask.MakeBeds && service.TaskPending(room, CleaningTask.Strip)))
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
            return task switch
            {
                CleaningTask.Trash => "Needs a trash bag",
                CleaningTask.Wipe => "Needs a reusable cloth kit",
                CleaningTask.Bathroom => "Needs disinfectant",
                CleaningTask.Vacuum => service.TaskPending(room, CleaningTask.Trash)
                    ? "Clear trash first" : "Needs a vacuum",
                CleaningTask.MakeBeds => service.TaskPending(room, CleaningTask.Strip)
                    ? "Strip beds first" : "Needs 2 clean linen sets",
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
                if (MotelWalker.Distance(caretaker.Position, MotelWorld.CartStation) < .85f)
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
            if (inside != NoRoom)
            {
                float width = Mathf.Min(277 * scale, screen.width - margin * 2);
                float x = margin + 8 * scale;
                float top = margin + (lan == null ? 53 : 89) * scale;
                housekeepingPanel = new Rect(margin, top, width, (42 + MotelService.CleaningTaskCount * 26) * scale);
                Fill(housekeepingPanel, Ink);
                GUI.Label(new Rect(x, top + 2 * scale, width, 29 * scale), $"Room {inside + 1} · {service.Cleanliness(inside)}% clean", detail);
                for (int index = 0; index < MotelService.CleaningTaskCount; index++)
                {
                    var task = (CleaningTask)index;
                    bool pending = service.TaskPending(inside, task);
                    float y = top + (34 + index * 26) * scale;
                    string text = pending ? CleaningLabels[index] : CleaningLabels[index] + (service.CleaningCompleted(inside, task) ? " · Done" : " · Clear");
                    if (pending && !service.CanCleanFor(inside, task, LocalWorker))
                        text = CleaningRequirement(inside, task);
                    bool enabled = GUI.enabled;
                    GUI.enabled &= pending && service.CanCleanFor(inside, task, LocalWorker);
                    if (DrawButton(new Rect(x, y, width - 16 * scale, 25 * scale), text) &&
                        !RequestClean(inside, task))
                        SetDestination(MotelWorld.CleaningPoint(inside, task));
                    GUI.enabled = enabled;
                }
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
