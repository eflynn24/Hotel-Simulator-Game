using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

namespace DesertLantern
{
    internal enum OperationKind
    {
        Reception, Approval, Checkout, Phone, Guest, Cleaning, Laundry, Delivery,
        Stock, Maintenance, Luggage, LostProperty, Meal, Decision, Incident
    }

    internal enum OperationStatus { Pending, Assigned, InProgress, Blocked, Completed, Cancelled }

    internal readonly struct OperationTask
    {
        public readonly OperationKind Kind;
        public readonly int Target, Revision, Priority, Deadline;
        public readonly string Title, Location, Resources, Next;
        public readonly OperationStatus Status;
        public readonly StaffRole? Role;
        public readonly ulong? Worker;

        public OperationTask(OperationKind kind, int target, int revision, string title, string location,
            int priority = 0, int deadline = -1, string resources = "", string next = "",
            OperationStatus status = OperationStatus.Pending, StaffRole? role = null, ulong? worker = null)
        {
            Kind = kind;
            Target = target;
            Revision = revision;
            Title = title;
            Location = location;
            Priority = priority;
            Deadline = deadline;
            Resources = resources;
            Next = next;
            Worker = worker;
            Role = role;
            Status = status == OperationStatus.Pending && (role.HasValue || worker.HasValue)
                ? OperationStatus.Assigned : status;
        }

        public int Code => Encode(Kind, Target);
        public static int Encode(OperationKind kind, int target) => (int)kind * 128 + target;
        public bool Closed => Status is OperationStatus.Completed or OperationStatus.Cancelled;
        public bool Claimable => Kind is OperationKind.Cleaning or OperationKind.Maintenance or OperationKind.Laundry;

        public OperationTask UnassignEmployee() => new OperationTask(Kind, Target, Revision, Title, Location,
            Priority, Deadline, Resources, Next, Status == OperationStatus.Assigned ? OperationStatus.Pending : Status, worker: Worker);
    }

    internal partial struct MotelService
    {
        private struct OperationStamp
        {
            public int Code, Revision, Minute;
            public ulong Worker;
            public byte Status;
        }

        private ushort lateGuestRequests;
        private FixedList512Bytes<OperationStamp> operationClaims;
        private FixedList512Bytes<OperationStamp> operationHistory;
        private ulong? FindOperationOwner(int code, int revision)
        {
            foreach (var claim in operationClaims)
                if (claim.Code == code && claim.Revision == revision)
                    return claim.Worker;
            return null;
        }

        public bool CanWorkOperation(OperationKind kind, int target, int revision, ulong? worker = null)
        {
            ulong? owner = FindOperationOwner(OperationTask.Encode(kind, target), revision);
            return !owner.HasValue || owner == worker;
        }

        public bool ChangeOperationOwner(int code, int revision, ulong worker, bool take)
        {
            for (int i = 0; i < operationClaims.Length; i++)
            {
                var claim = operationClaims[i];
                if (claim.Code != code || claim.Revision != revision)
                    continue;
                if (take || claim.Worker != worker)
                    return false;
                operationClaims.RemoveAt(i);
                return true;
            }
            if (!take || operationClaims.Length >= operationClaims.Capacity)
                return false;
            var tasks = new List<OperationTask>();
            ReadOperations(tasks);
            foreach (var task in tasks)
            {
                if (!task.Claimable || task.Code != code || task.Revision != revision || task.Closed || task.Worker.HasValue)
                    continue;
                operationClaims.Add(new OperationStamp { Code = code, Revision = revision, Worker = worker });
                return true;
            }
            return false;
        }

        public void ReleaseOperations(ulong worker)
        {
            for (int i = operationClaims.Length - 1; i >= 0; i--)
                if (operationClaims[i].Worker == worker)
                    operationClaims.RemoveAt(i);
        }

        public void ResetOperationClaims() => operationClaims.Clear();

        private void FinishOperation(OperationKind kind, int target, int revision, bool cancelled = false)
        {
            int code = OperationTask.Encode(kind, target);
            for (int i = operationClaims.Length - 1; i >= 0; i--)
                if (operationClaims[i].Code == code && operationClaims[i].Revision == revision)
                    operationClaims.RemoveAt(i);
            if (operationHistory.Length == operationHistory.Capacity)
                operationHistory.RemoveAt(0);
            operationHistory.Add(new OperationStamp
            {
                Code = code, Revision = revision, Minute = ClockMinute,
                Status = (byte)(cancelled ? OperationStatus.Cancelled : OperationStatus.Completed)
            });
        }

        public bool EmployeeEligible(StaffRole role, bool urgent) => StaffHired(role) &&
            (urgent || !StaffUrgentOnly((int)role));

        public int CleaningDeadline(int room) => NextConfirmedArrival(room);
        public bool PriorityCleaning(int room) => HousekeepingRequested(room) ||
            (CleaningDeadline(room) is int deadline && deadline >= 0 && deadline <= ClockMinute + 120);

        public int RecommendedRestockPacks(int item)
        {
            int available = Stock(item) + IncomingStock(item);
            if (available > MinimumStock(item))
                return 0;
            int target = item is 0 or 4 ? 1 : MinimumStock(item) * 2;
            return Math.Max(0, (target - available + Catalog[item].Quantity - 1) / Catalog[item].Quantity);
        }

        public int ExpectedLaundryUnits => washingLinens + washingTowels;

        private StaffRole? Candidate(StaffRole role, bool urgent, bool available = true) =>
            available && EmployeeEligible(role, urgent) ? role : null;

        public void ReadOperations(List<OperationTask> tasks, bool includeHistory = false)
        {
            tasks.Clear();
            for (int index = 0; index < ReservationCount; index++)
            {
                int room = ReservationRoom(index);
                int start = ReservationStart(index);
                bool pending = ReservationPending(index);
                bool walkIn = ReservationChannel(index) == BookingChannel.WalkIn;
                string guest = ReservationGuest(index);
                if (pending && !walkIn)
                {
                    tasks.Add(new OperationTask(OperationKind.Approval, room, start, $"Review {guest}’s booking",
                        "Reception computer", 1, start, "Available room", "Open Bookings and confirm or decline"));
                    continue;
                }

                string blocker = !UtilityOutage && RoomUnusable(room) ? "Complete required maintenance" :
                    UtilityOutage ? "Resolve the utility outage" : RoomDirty(room) ? "Finish the housekeeping checklist" :
                    (ActiveRooms & (1 << room)) != 0 ? "Check out the current guest before preparing the room" : "";
                tasks.Add(new OperationTask(OperationKind.Reception, room, start, $"Check in {guest}", "Reception",
                    start <= ClockMinute ? 2 : 0, start, $"Room {room + 1} key",
                    blocker != "" ? blocker : start > ClockMinute ? "Guest has yet to arrive" : "Prepare and hand over the room key",
                    blocker != "" ? OperationStatus.Blocked : OperationStatus.Pending,
                    Candidate(StaffRole.Receptionist, true, start <= ClockMinute && blocker == "")));
            }

            for (int room = 0; room < RoomCapacity; room++)
                ReadRoomOperations(tasks, room);

            if (BookingCallRinging || CanBookPhone || CanReturnBookingCall)
                tasks.Add(new OperationTask(OperationKind.Phone, 0, CanReturnBookingCall ? missedBookingMinute : NextPhoneCall,
                    CanReturnBookingCall ? "Return a missed booking call" : "Take a telephone reservation", "Reception telephone",
                    2, CanReturnBookingCall ? -1 : NextPhoneCall + PhonePatienceMinutes, "Available room", "Use the reception phone",
                    CanBookPhone ? OperationStatus.InProgress : OperationStatus.Pending,
                    Candidate(StaffRole.Receptionist, true, BookingCallRinging),
                    CanBookPhone && bookingPhoneOwner != ReceptionPhoneWorker ? bookingPhoneOwner : null));

            foreach (Fixture fixture in new[] { Fixture.Washer, Fixture.Router })
                ReadMaintenance(tasks, 0, fixture);
            if (DirtyLinens + DirtyTowels > 0 || LaundryRunning)
            {
                ulong? owner = FindOperationOwner(OperationTask.Encode(OperationKind.Laundry, 0), 0);
                string reason = WasherBroken ? "Repair the washer" : Detergent == 0 ? "Order detergent from Supplies" : "";
                bool blocked = !LaundryRunning && reason != "";
                tasks.Add(new OperationTask(OperationKind.Laundry, 0, 0, "Wash linens and towels", "Housekeeping",
                    0, LaundryRunning ? laundryDue : -1, "Washer · 1 detergent",
                    LaundryRunning ? "Clean laundry returns to the shelves automatically" : blocked ? reason : "Start a load at the washer",
                    LaundryRunning ? OperationStatus.InProgress : blocked ? OperationStatus.Blocked : OperationStatus.Pending,
                    Candidate(StaffRole.Housekeeper, false, !blocked && !owner.HasValue), owner));
            }

            for (int index = 0; index < PendingOrderCount; index++)
            {
                SupplyOrder order = SupplyOrderAt(index);
                bool carried = order.Status == SupplyDelivery.Carried;
                bool arrived = order.Status != SupplyDelivery.InTransit;
                tasks.Add(new OperationTask(OperationKind.Delivery, 0, order.Number, carried ? $"Stock order #{order.Number}" : $"Receive order #{order.Number}",
                    carried ? "Housekeeping shelves" : "Delivery bay", arrived ? 1 : 0, order.Due, "Supply crate",
                    carried ? "Carry the crate to the shelves" : arrived ? "Collect the crate at the bay" : "The crate is in transit",
                    carried ? OperationStatus.InProgress : OperationStatus.Pending,
                    Candidate(StaffRole.Stocker, PendingOrderCount >= 2, arrived && !carried), carried ? order.Carrier : null));
            }

            for (int item = 0; item < Catalog.Length; item++)
            {
                int packs = RecommendedRestockPacks(item);
                if (!LowStock(item))
                    continue;
                SupplyOffer offer = Catalog[item];
                bool blocked = packs > 0 && (Cash < offer.Price || PendingOrderCount >= 9);
                tasks.Add(new OperationTask(OperationKind.Stock, item, 0, $"Restock {offer.Name.ToLowerInvariant()}",
                    $"Housekeeping · {Stock(item)} on shelves", Stock(item) == 0 ? 1 : 0, -1,
                    packs == 0 ? $"{IncomingStock(item)} incoming" : $"{packs} recommended {(packs == 1 ? "pack" : "packs")} · ${offer.Price} each",
                    blocked ? Cash < offer.Price ? "Earn enough cash to buy a pack" : "Stock a delivery to free an order slot" :
                    packs == 0 ? "Receive and stock the incoming delivery" : "Open Supplies or place a quick order",
                    blocked ? OperationStatus.Blocked : packs == 0 ? OperationStatus.InProgress : OperationStatus.Pending,
                    Candidate(StaffRole.Purchasing, true, packs > 0 && !blocked)));
            }

            ReadMaintenanceStock(tasks, Catalog.Length, Disinfectant, 12, "disinfectant");
            ReadMaintenanceStock(tasks, Catalog.Length + 1, RepairParts, 18, "repair parts");

            if (Bag != LuggageStatus.None)
                tasks.Add(new OperationTask(OperationKind.Luggage, BagRoom, GuestId(BagRoom), "Help with guest luggage",
                    Bag == LuggageStatus.InCar ? "Guest’s car" : $"Room {BagRoom + 1}", 0, DepartureMinute(BagRoom),
                    "Guest suitcase", Bag == LuggageStatus.InCar ? "Pick up the suitcase at the trunk" : "Set it on the luggage rack",
                    Bag == LuggageStatus.Carried ? OperationStatus.InProgress : OperationStatus.Pending,
                    Candidate(StaffRole.Bellhop, true, Bag == LuggageStatus.InCar || Carrier == BellhopId),
                    Bag == LuggageStatus.Carried && Carrier != BellhopId ? Carrier : null));
            if (PendingDecision != ManagementDecision.None)
                tasks.Add(new OperationTask(OperationKind.Decision, 0, DecisionDeadline, "Review management decision",
                    "Reception computer", 1, DecisionDeadline, next: "Open Bookings and review Decisions"));
            if (Incident != PropertyIncident.None)
                tasks.Add(new OperationTask(OperationKind.Incident, 0, IncidentDue, "Investigate property incident", "Reception computer",
                    2, IncidentDue, next: "Open Reports and investigate", role: Candidate(StaffRole.Manager, Incident == PropertyIncident.UtilityEmergency)));

            tasks.Sort(CompareOperations);
            int busyRoles = 0;
            for (int index = 0; index < tasks.Count; index++)
            {
                OperationTask task = tasks[index];
                if (!task.Role.HasValue || task.Worker.HasValue)
                    continue;
                int role = 1 << (int)task.Role.Value;
                if ((busyRoles & role) != 0)
                    tasks[index] = task.UnassignEmployee();
                else
                    busyRoles |= role;
            }
            if (!includeHistory)
                return;
            for (int i = operationHistory.Length - 1; i >= 0; i--)
            {
                OperationStamp stamp = operationHistory[i];
                var kind = (OperationKind)(stamp.Code / 128);
                var status = (OperationStatus)stamp.Status;
                int target = stamp.Code % 128;
                tasks.Add(new OperationTask(kind, target, stamp.Revision, HistoryLabel(kind, target), DisplayTime(stamp.Minute),
                    status: status));
            }
        }

        private void ReadMaintenanceStock(List<OperationTask> tasks, int item, int stock, int price, string name)
        {
            if (stock >= 2)
                return;
            bool blocked = Cash < price;
            tasks.Add(new OperationTask(OperationKind.Stock, item, 0, $"Restock {name}", $"Maintenance cabinet · {stock} left",
                1, resources: $"1 pack · ${price}", next: blocked ? "Earn operating cash to restock" : "Open Reports and buy maintenance stock",
                status: blocked ? OperationStatus.Blocked : OperationStatus.Pending,
                role: Candidate(StaffRole.Purchasing, true, !blocked)));
        }

        private static int CompareOperations(OperationTask left, OperationTask right)
        {
            int blocked = (left.Status == OperationStatus.Blocked).CompareTo(right.Status == OperationStatus.Blocked);
            if (blocked != 0 && left.Priority == right.Priority)
                return blocked;
            int priority = right.Priority.CompareTo(left.Priority);
            if (priority != 0)
                return priority;
            int due = (left.Deadline < 0 ? int.MaxValue : left.Deadline).CompareTo(right.Deadline < 0 ? int.MaxValue : right.Deadline);
            return due != 0 ? due : left.Code.CompareTo(right.Code);
        }

        public static string FixtureTitle(Fixture fixture) => fixture switch
        {
            Fixture.AirConditioner => "air conditioner", Fixture.DoorLock => "door lock",
            Fixture.Television => "television", _ => fixture.ToString().ToLowerInvariant()
        };

        private void ReadRoomOperations(List<OperationTask> tasks, int room)
        {
            string location = $"Room {room + 1}";
            bool occupied = (ActiveRooms & (1 << room)) != 0;
            if (occupied)
            {
                int departure = DepartureMinute(room);
                tasks.Add(new OperationTask(OperationKind.Checkout, room, GuestId(room), $"Check out {StayGuest(room)}",
                    "Reception", departure <= ClockMinute ? 2 : 0, departure, "Room key · guest bill",
                    departure <= ClockMinute ? "Settle the bill when the guest reaches reception" : "The guest is still staying",
                    role: Candidate(StaffRole.Receptionist, true, departure <= ClockMinute)));
            }
            if (RoomDirty(room) && (!occupied || HousekeepingRequested(room) || DoNotDisturb(room)))
            {
                ulong? owner = FindOperationOwner(OperationTask.Encode(OperationKind.Cleaning, room), GuestId(room));
                bool workable = false;
                for (int step = 0; step < CleaningTaskCount && !workable; step++)
                    workable = owner.HasValue ? CanCleanFor(room, (CleaningTask)step, owner.Value) || CanClean(room, (CleaningTask)step) :
                        CanClean(room, (CleaningTask)step) || CartInUse && CanCleanFor(room, (CleaningTask)step, CartCarrier);
                bool blocked = DoNotDisturb(room) || !workable;
                string next = blocked ? HousekeepingBlocker(room) :
                    owner.HasValue && (!CartInUse || CartCarrier != owner) ? "Load the cart in Housekeeping" : "Complete the room checklist";
                tasks.Add(new OperationTask(OperationKind.Cleaning, room, GuestId(room), $"Prepare {location.ToLowerInvariant()}", location,
                    PriorityCleaning(room) ? 2 : 1, CleaningDeadline(room), "Cart · trash bags · linens · towels · toiletries",
                    $"{Cleanliness(room)}% ready · {next}", blocked ? OperationStatus.Blocked :
                    Cleanliness(room) > 0 ? OperationStatus.InProgress : OperationStatus.Pending,
                    Candidate(StaffRole.Housekeeper, PriorityCleaning(room), !blocked && !owner.HasValue), owner));
            }
            if (RequestDue(room))
            {
                bool stocked = RequestSuppliesAvailable(room);
                int units = RequiredRequestUnits(room);
                string resource = KindOfRequest(room) switch
                {
                    GuestRequestKind.Towel => $"{units} clean {(units == 1 ? "towel" : "towels")}",
                    GuestRequestKind.Pillow => "1 pillow", GuestRequestKind.Toiletries => "1 set of toiletries", _ => "Speak to guest"
                };
                ulong? worker = RequestClaimed(room) ? guestTaskOwners[room] : null;
                tasks.Add(new OperationTask(OperationKind.Guest, room, requestMinutes[room], RequestLabel(room), $"{StayGuest(room)} · {location}",
                    RequestUrgent(room) ? 2 : 1, requestMinutes[room] + Patience(room), resource,
                    !stocked ? "Order or wash supplies, then collect them from the shelves" :
                    RequestItemCollected(room) || !RequestNeedsItem(room) ? "Speak to the guest" :
                    worker.HasValue ? "Pick up the item at Housekeeping" : "Accept the request",
                    !stocked ? OperationStatus.Blocked : RequestItemCollected(room) ? OperationStatus.InProgress : OperationStatus.Pending,
                    Candidate(StaffRole.GuestServices, RequestUrgent(room), stocked && !worker.HasValue), worker));
            }
            else if (OutstandingIssue(room) is GuestIssue issue && issue != GuestIssue.None)
                tasks.Add(new OperationTask(OperationKind.Guest, room, GuestId(room), "Help a guest with their stay", $"{StayGuest(room)} · {location}", 1,
                    next: "Open the guest’s request to see the issue"));
            if (ForgottenItem(room))
                tasks.Add(new OperationTask(OperationKind.LostProperty, room, GuestId(room), "Collect forgotten property", location, next: "Collect the item from the luggage rack"));
            if (RoomMealPending(room))
                tasks.Add(new OperationTask(OperationKind.Meal, room, GuestId(room), "Prepare a room service meal", location,
                    1, DepartureMinute(room), "1 meal pack", RoomMealStock == 0 ? "Buy meal stock in Expansion" : "Open room service in Expansion",
                    RoomMealStock == 0 ? OperationStatus.Blocked : OperationStatus.Pending));
            for (int fixture = 0; fixture < RoomFixtureCount; fixture++)
                ReadMaintenance(tasks, room, (Fixture)fixture);
        }

        private string HousekeepingBlocker(int room)
        {
            if (DoNotDisturb(room))
                return "Wait for the guest to remove Do not disturb";
            if (TaskPending(room, CleaningTask.Trash) && TrashBags == 0)
                return "Order trash bags or refill the housekeeping cart";
            if (TaskPending(room, CleaningTask.Wipe) && CleaningKits == 0)
                return "Order a reusable cleaning kit";
            if (TaskPending(room, CleaningTask.MakeBeds) && LinenSets < 2)
                return "Wash or order two clean linen sets";
            if (TaskPending(room, CleaningTask.Bathroom) && Disinfectant == 0)
                return "Buy disinfectant in Reports and refill the cart";
            if (TaskPending(room, CleaningTask.Vacuum) && Vacuums == 0)
                return "Order a reusable vacuum";
            if (TaskPending(room, CleaningTask.Restock) && (Towels == 0 || Toiletries == 0))
                return Towels == 0 ? "Wash or order clean towels" : "Order toiletries";
            return "Complete the prerequisites in the room checklist";
        }

        private void ReadMaintenance(List<OperationTask> tasks, int room, Fixture fixture)
        {
            bool broken = IsBroken(room, fixture);
            if (!broken && (fixture is Fixture.Washer or Fixture.Router || Condition(room, fixture) >= 55))
                return;
            int target = room + (int)fixture * RoomCount;
            ulong? owner = FindOperationOwner(OperationTask.Encode(OperationKind.Maintenance, target), 0);
            bool urgent = broken && (fixture is Fixture.Washer or Fixture.Router or Fixture.DoorLock or Fixture.Toilet ||
                (ActiveRooms & (1 << room)) != 0);
            bool blocked = RepairParts == 0 && Cash < (Insured && broken ? 24 : 65);
            tasks.Add(new OperationTask(OperationKind.Maintenance, target, 0, $"Repair {FixtureTitle(fixture)}",
                fixture == Fixture.Washer ? "Housekeeping" : fixture == Fixture.Router ? "Reception" : $"Room {room + 1}",
                urgent ? 2 : broken ? 1 : 0, fixture is Fixture.Washer or Fixture.Router ? -1 : CleaningDeadline(room),
                $"1 repair part or replacement · {Condition(room, fixture)}% condition",
                blocked ? "Buy repair parts in Reports or earn enough to replace it" : "Inspect the equipment and repair or replace it",
                blocked ? OperationStatus.Blocked : OperationStatus.Pending,
                Candidate(StaffRole.Technician, urgent, !owner.HasValue && RepairParts > 0 && (broken || RepairParts >= 4)), owner));
        }

        private static string HistoryLabel(OperationKind kind, int target) => kind switch
        {
            OperationKind.Reception => $"Check-in · Room {target + 1}", OperationKind.Checkout => $"Checkout · Room {target + 1}",
            OperationKind.Cleaning => $"Housekeeping · Room {target + 1}", OperationKind.Approval => $"Booking review · Room {target + 1}",
            OperationKind.Guest => $"Guest request · Room {target + 1}", OperationKind.Phone => "Telephone reservation",
            OperationKind.Laundry => "Laundry", OperationKind.Delivery => "Supply delivery",
            OperationKind.Luggage => $"Luggage · Room {target + 1}", OperationKind.Maintenance => $"Repair {FixtureTitle((Fixture)(target / RoomCount))}",
            OperationKind.Meal => $"Room service · Room {target + 1}", OperationKind.LostProperty => $"Lost property · Room {target + 1}",
            OperationKind.Decision => "Management decision", OperationKind.Incident => "Property incident", _ => "Restock supplies"
        };

        private void SerializeOperationQueue<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            SerializeOperationStamps(serializer, ref operationClaims);
            SerializeOperationStamps(serializer, ref operationHistory);
            serializer.SerializeValue(ref lateGuestRequests);
        }

        private static void SerializeOperationStamps<T>(BufferSerializer<T> serializer, ref FixedList512Bytes<OperationStamp> list)
            where T : IReaderWriter
        {
            byte count = (byte)list.Length;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader)
                list.Length = count;
            for (int i = 0; i < count; i++)
            {
                OperationStamp stamp = list[i];
                serializer.SerializeValue(ref stamp.Code);
                serializer.SerializeValue(ref stamp.Revision);
                serializer.SerializeValue(ref stamp.Minute);
                serializer.SerializeValue(ref stamp.Worker);
                serializer.SerializeValue(ref stamp.Status);
                if (serializer.IsReader)
                    list[i] = stamp;
            }
        }

        private bool OperationQueueEquals(MotelService other) =>
            operationClaims.Equals(other.operationClaims) && operationHistory.Equals(other.operationHistory) &&
            lateGuestRequests == other.lateGuestRequests;
    }
}
