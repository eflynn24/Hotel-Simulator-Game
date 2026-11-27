using Unity.Netcode;

namespace DesertLantern
{
    internal enum CleaningTask : byte { Trash, Wipe, Vacuum, Strip, MakeBeds }

    internal partial struct MotelService
    {
        internal const int CleaningTaskCount = 5;
        private const uint FullCleaningMask = (1u << CleaningTaskCount) - 1;
        public byte VisitedRooms;
        public byte ActiveReservations;
        public int TrashBags;
        public int Vacuums;
        public int Detergent;
        public int DirtyLinens;
        public int DirtyTowels;
        private int washingLinens, washingTowels, laundryDue;
        private uint cleaningTasks;

        public bool LaundryRunning => laundryDue != 0;
        public bool RoomDirty(int room) => (cleaningTasks & (FullCleaningMask << (room * CleaningTaskCount))) != 0;

        private static uint TaskBit(int room, CleaningTask task) => 1u << (room * CleaningTaskCount + (int)task);

        public bool TaskPending(int room, CleaningTask task) => (cleaningTasks & TaskBit(room, task)) != 0;

        public bool CanClean(int room, CleaningTask task)
        {
            if ((ActiveRooms & (1 << room)) != 0 || !TaskPending(room, task))
                return false;
            return task switch
            {
                CleaningTask.Trash => TrashBags > 0,
                CleaningTask.Wipe => CleaningKits > 0,
                CleaningTask.Vacuum => Vacuums > 0 && !TaskPending(room, CleaningTask.Trash),
                CleaningTask.MakeBeds => LinenSets >= 2 && !TaskPending(room, CleaningTask.Strip),
                CleaningTask.Strip => true,
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
                case CleaningTask.Wipe: CleaningKits--; break;
                case CleaningTask.Strip: DirtyLinens += 2; break;
                case CleaningTask.MakeBeds: LinenSets -= 2; break;
            }
            cleaningTasks &= ~TaskBit(room, task);
            if (!RoomDirty(room))
                UnlockedDoors &= (byte)~(1 << room);
        }

        private void MarkRoomDirty(int room)
        {
            cleaningTasks |= FullCleaningMask << (room * CleaningTaskCount);
            if ((FulfilledRequests & (1 << room)) != 0)
                DirtyTowels++;
        }

        public void StartLaundry()
        {
            if (LaundryRunning || Detergent <= 0 || DirtyLinens + DirtyTowels == 0)
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
            if (!LaundryRunning || ClockMinute < laundryDue)
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
            serializer.SerializeValue(ref cleaningTasks);
        }

        private bool HousekeepingEquals(MotelService other) =>
            VisitedRooms == other.VisitedRooms && ActiveReservations == other.ActiveReservations && TrashBags == other.TrashBags && Vacuums == other.Vacuums &&
            Detergent == other.Detergent && DirtyLinens == other.DirtyLinens && DirtyTowels == other.DirtyTowels &&
            washingLinens == other.washingLinens && washingTowels == other.washingTowels &&
            laundryDue == other.laundryDue && cleaningTasks == other.cleaningTasks;
    }
}
