using Unity.Collections;
using Unity.Netcode;

namespace DesertLantern
{
    internal enum CleaningTask : byte { Trash, Wipe, Vacuum, Strip, MakeBeds, Mop, Bathroom, Towels }

    internal partial struct MotelService
    {
        internal const int CleaningTaskCount = 8;
        private const byte FullCleaningMask = byte.MaxValue;
        public ushort VisitedRooms;
        public ushort ActiveReservations;
        public int TrashBags;
        public int Vacuums;
        public int Detergent;
        public int DirtyLinens;
        public int DirtyTowels;
        private int washingLinens, washingTowels, laundryDue;
        private FixedList32Bytes<byte> cleaningTasks;
        private FixedList32Bytes<byte> completedCleaning;
        private ushort doNotDisturb;
        private ushort housekeepingRequests;
        public int Disinfectant;
        public bool CartInUse;
        public ulong CartCarrier;
        public byte CartBags, CartLinen, CartDisinfectant;

        public void ToggleHousekeepingCart(ulong worker)
        {
            if (CartInUse)
            {
                RecoverHousekeepingCart(worker);
                return;
            }

            CartCarrier = worker;
            CartInUse = true;
            CartBags = (byte)System.Math.Min(2, TrashBags);
            CartLinen = (byte)System.Math.Min(4, LinenSets);
            CartDisinfectant = (byte)System.Math.Min(2, Disinfectant);
            TrashBags -= CartBags;
            LinenSets -= CartLinen;
            Disinfectant -= CartDisinfectant;
        }

        public void RecoverHousekeepingCart(ulong worker)
        {
            if (!CartInUse || CartCarrier != worker)
                return;
            TrashBags += CartBags;
            LinenSets += CartLinen;
            Disinfectant += CartDisinfectant;
            CartBags = CartLinen = CartDisinfectant = 0;
            CartCarrier = 0;
            CartInUse = false;
        }

        private int CartUnits(CleaningTask task, ulong worker)
        {
            if (!CartInUse || CartCarrier != worker)
                return 0;
            return task switch
            {
                CleaningTask.Trash when CartBags > 0 => 1,
                CleaningTask.Bathroom when CartDisinfectant > 0 => 1,
                CleaningTask.MakeBeds when CartLinen >= 2 => 2,
                _ => 0
            };
        }

        private void TransferCartUnits(CleaningTask task, int units)
        {
            switch (task)
            {
                case CleaningTask.Trash:
                    CartBags = (byte)(CartBags - units);
                    TrashBags += units;
                    break;
                case CleaningTask.MakeBeds:
                    CartLinen = (byte)(CartLinen - units);
                    LinenSets += units;
                    break;
                case CleaningTask.Bathroom:
                    CartDisinfectant = (byte)(CartDisinfectant - units);
                    Disinfectant += units;
                    break;
            }
        }

        public bool CanCleanFor(int room, CleaningTask task, ulong worker)
        {
            MotelService available = this;
            available.TransferCartUnits(task, CartUnits(task, worker));
            return available.CanClean(room, task);
        }

        public void CleanFor(int room, CleaningTask task, ulong worker)
        {
            if (!CanCleanFor(room, task, worker))
                return;
            TransferCartUnits(task, CartUnits(task, worker));
            Clean(room, task);
        }
        public bool DoNotDisturb(int room) => (doNotDisturb & (1 << room)) != 0;
        public bool HousekeepingRequested(int room) => (housekeepingRequests & (1 << room)) != 0;
        public bool CleaningCompleted(int room, CleaningTask task) => (completedCleaning[room] & TaskBit(task)) != 0;
        public int Cleanliness(int room)
        {
            int dirty = 0;
            for (int task = 0; task < CleaningTaskCount; task++)
                if (TaskPending(room, (CleaningTask)task))
                    dirty++;
            return 100 - 100 * dirty / CleaningTaskCount;
        }

        private void AdvanceRoomWear(int previous)
        {
            const int interval = 60;
            int first = previous / interval;
            int current = ClockMinute / interval;
            if (first == current)
                return;
            for (int room = 0; room < RoomCount; room++)
            {
                int bit = 1 << room;
                if ((ActiveRooms & bit) == 0)
                    continue;
                for (int hour = first + 1; hour <= current; hour++)
                {
                    int cycle = (hour + room) % 8;
                    CleaningTask task = cycle switch
                    {
                        0 => CleaningTask.Trash, 1 => CleaningTask.Wipe, 2 => CleaningTask.Vacuum,
                        3 => CleaningTask.Mop, 4 => CleaningTask.Bathroom, 5 => CleaningTask.Towels,
                        _ => CleaningTask.Strip
                    };
                    byte pending = TaskBit(task);
                    if (task == CleaningTask.Strip)
                        pending |= TaskBit(CleaningTask.MakeBeds);
                    cleaningTasks[room] |= pending;
                    completedCleaning[room] &= (byte)~pending;
                    if (cycle == 3)
                    {
                        housekeepingRequests |= (ushort)bit;
                        doNotDisturb &= (ushort)~bit;
                    }
                    else if (cycle == 7 && (room & 1) != 0)
                    {
                        doNotDisturb |= (ushort)bit;
                        housekeepingRequests &= (ushort)~bit;
                    }
                }
            }
        }

        public bool LaundryRunning => laundryDue != 0;
        public bool RoomDirty(int room) => cleaningTasks[room] != 0;

        private static byte TaskBit(CleaningTask task) => (byte)(1 << (int)task);

        public bool TaskPending(int room, CleaningTask task) => (cleaningTasks[room] & TaskBit(task)) != 0;

        public bool CanClean(int room, CleaningTask task)
        {
            if (!TaskPending(room, task) ||
                ((ActiveRooms & (1 << room)) != 0 && (!HousekeepingRequested(room) || DoNotDisturb(room))))
                return false;
            return task switch
            {
                CleaningTask.Trash => TrashBags > 0,
                CleaningTask.Wipe => CleaningKits > 0,
                CleaningTask.Vacuum => Vacuums > 0 && !TaskPending(room, CleaningTask.Trash),
                CleaningTask.MakeBeds => LinenSets >= 2 && !TaskPending(room, CleaningTask.Strip),
                CleaningTask.Strip => true,
                CleaningTask.Bathroom => Disinfectant > 0,
                CleaningTask.Mop or CleaningTask.Towels => true,
                _ => false
            };
        }

        public void Clean(int room, CleaningTask task)
        {
            if (!CanClean(room, task))
                return;
            switch (task)
            {
                case CleaningTask.Trash: TrashBags--; break;
                case CleaningTask.Bathroom: Disinfectant--; break;
                case CleaningTask.Towels: DirtyTowels++; break;
                case CleaningTask.Strip: DirtyLinens += 2; break;
                case CleaningTask.MakeBeds: LinenSets -= 2; break;
            }
            cleaningTasks[room] &= (byte)~TaskBit(task);
            completedCleaning[room] |= TaskBit(task);
            if (!RoomDirty(room))
            {
                housekeepingRequests &= (ushort)~(1 << room);
                if ((ActiveRooms & (1 << room)) == 0 && !ForgottenItem(room))
                    UnlockedDoors &= (ushort)~(1 << room);
            }
        }

        private void MarkRoomDirty(int room)
        {
            int bit = 1 << room;
            doNotDisturb &= (ushort)~bit;
            housekeepingRequests &= (ushort)~bit;
            cleaningTasks[room] = FullCleaningMask;
            completedCleaning[room] = 0;
        }

        public void StartLaundry()
        {
            if (WasherBroken || LaundryRunning || Detergent <= 0 || DirtyLinens + DirtyTowels == 0)
                return;
            washingLinens = System.Math.Min(DirtyLinens, 4);
            washingTowels = System.Math.Min(DirtyTowels, 4 - washingLinens);
            DirtyLinens -= washingLinens;
            DirtyTowels -= washingTowels;
            Detergent--;
            laundryDue = ClockMinute + 90;
        }

        private void AdvanceLaundry()
        {
            if (WasherBroken || !LaundryRunning || ClockMinute < laundryDue)
                return;
            LinenSets += washingLinens;
            Towels += washingTowels;
            washingLinens = washingTowels = laundryDue = 0;
            ClockSpeed = 1;
        }

        private void SerializeHousekeeping<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref VisitedRooms);
            serializer.SerializeValue(ref ActiveReservations);
            serializer.SerializeValue(ref TrashBags);
            serializer.SerializeValue(ref Vacuums);
            serializer.SerializeValue(ref Detergent);
            serializer.SerializeValue(ref DirtyLinens);
            serializer.SerializeValue(ref DirtyTowels);
            serializer.SerializeValue(ref washingLinens);
            serializer.SerializeValue(ref washingTowels);
            serializer.SerializeValue(ref laundryDue);
            if (serializer.IsReader)
            {
                cleaningTasks.Length = RoomCount;
                completedCleaning.Length = RoomCount;
            }
            for (int room = 0; room < RoomCount; room++)
            {
                byte pending = cleaningTasks.Length > room ? cleaningTasks[room] : (byte)0;
                byte completed = completedCleaning.Length > room ? completedCleaning[room] : (byte)0;
                serializer.SerializeValue(ref pending);
                serializer.SerializeValue(ref completed);
                if (serializer.IsReader)
                {
                    cleaningTasks[room] = pending;
                    completedCleaning[room] = completed;
                }
            }
            serializer.SerializeValue(ref housekeepingRequests);
            serializer.SerializeValue(ref doNotDisturb);
            serializer.SerializeValue(ref Disinfectant);
            serializer.SerializeValue(ref CartInUse);
            serializer.SerializeValue(ref CartCarrier);
            serializer.SerializeValue(ref CartBags);
            serializer.SerializeValue(ref CartLinen);
            serializer.SerializeValue(ref CartDisinfectant);
        }

        private bool HousekeepingEquals(MotelService other) =>
            VisitedRooms == other.VisitedRooms && ActiveReservations == other.ActiveReservations && TrashBags == other.TrashBags && Vacuums == other.Vacuums &&
            Detergent == other.Detergent && DirtyLinens == other.DirtyLinens && DirtyTowels == other.DirtyTowels &&
            washingLinens == other.washingLinens && washingTowels == other.washingTowels &&
            laundryDue == other.laundryDue && cleaningTasks.Equals(other.cleaningTasks) && completedCleaning.Equals(other.completedCleaning) &&
            housekeepingRequests == other.housekeepingRequests && doNotDisturb == other.doNotDisturb && Disinfectant == other.Disinfectant &&
            CartInUse == other.CartInUse && CartCarrier == other.CartCarrier && CartBags == other.CartBags &&
            CartLinen == other.CartLinen && CartDisinfectant == other.CartDisinfectant;
    }
}
