using Unity.Collections;
using Unity.Netcode;

namespace DesertLantern
{
    internal partial struct MotelService
    {
        private ushort claimedGuestTasks, collectedGuestItems;
        private FixedList128Bytes<ulong> guestTaskOwners;

        public bool RequestClaimed(int room) => (claimedGuestTasks & (1 << room)) != 0;
        public bool RequestItemCollected(int room) => (collectedGuestItems & (1 << room)) != 0;
        public bool OwnsGuestTask(int room, ulong worker) => RequestClaimed(room) && guestTaskOwners[room] == worker;
        public bool RequestNeedsItem(int room) => KindOfRequest(room) != GuestRequestKind.Complaint;
        public int RequiredRequestUnits(int room) => KindOfRequest(room) == GuestRequestKind.Towel &&
            (BookingPreference)(stayBookingDetails[room] >> 6) == BookingPreference.ExtraTowels ? 2 : 1;

        public int RequestStock(int room) => KindOfRequest(room) switch
        {
            GuestRequestKind.Towel => Towels,
            GuestRequestKind.Pillow => Pillows,
            GuestRequestKind.Toiletries => Toiletries,
            _ => 1
        };

        private void ChangeRequestStock(GuestRequestKind item, int units)
        {
            switch (item)
            {
                case GuestRequestKind.Towel: Towels += units; break;
                case GuestRequestKind.Pillow: Pillows += units; break;
                case GuestRequestKind.Toiletries: Toiletries += units; break;
            }
        }

        private bool MatchesGuestTask(PropertyRequest request) => OwnsRoom(request.Room) &&
            RequestDue(request.Room.Number) && RequestReference(request.Room.Number).Guest == request.Guest &&
            RequestReference(request.Room.Number).ScheduledMinute == request.ScheduledMinute;

        public bool ClaimGuestTask(PropertyRequest request, ulong worker)
        {
            int room = request.Room.Number;
            if (!MatchesGuestTask(request) || RequestClaimed(room))
                return false;
            claimedGuestTasks |= (ushort)(1 << room);
            guestTaskOwners[room] = worker;
            AcknowledgeRequest(room);
            return true;
        }

        public bool CollectGuestTask(PropertyRequest request, ulong worker)
        {
            int room = request.Room.Number;
            if (!MatchesGuestTask(request) || !OwnsGuestTask(room, worker) || !RequestNeedsItem(room) ||
                RequestItemCollected(room) || !RequestSuppliesAvailable(room))
                return false;
            ChangeRequestStock(KindOfRequest(room), -RequiredRequestUnits(room));
            collectedGuestItems |= (ushort)(1 << room);
            return true;
        }

        public bool DeliverGuestTask(PropertyRequest request, ulong worker)
        {
            int room = request.Room.Number;
            if (!MatchesGuestTask(request) || !OwnsGuestTask(room, worker) || RequestNeedsItem(room) && !RequestItemCollected(room))
                return false;
            ClearGuestTask(room);
            CompleteGuestRequest(room);
            return true;
        }

        public bool UnassignGuestTask(PropertyRequest request, ulong worker)
        {
            if (!MatchesGuestTask(request) || !OwnsGuestTask(request.Room.Number, worker))
                return false;
            ReleaseGuestTask(request.Room.Number);
            return true;
        }

        public void ReleaseGuestTasks(ulong worker)
        {
            for (int room = 0; room < RoomCapacity; room++)
                if (OwnsGuestTask(room, worker))
                    ReleaseGuestTask(room);
        }

        public void ResetGuestTaskAssignments()
        {
            for (int room = 0; room < RoomCapacity; room++)
                if (RequestClaimed(room))
                    ReleaseGuestTask(room);
        }

        private void ReleaseGuestTask(int room)
        {
            if (RequestItemCollected(room))
                ChangeRequestStock(KindOfRequest(room), RequiredRequestUnits(room));
            ClearGuestTask(room);
        }

        private void ClearGuestTask(int room)
        {
            claimedGuestTasks &= (ushort)~(1 << room);
            collectedGuestItems &= (ushort)~(1 << room);
            guestTaskOwners[room] = 0;
        }

        private void SerializeServiceTasks<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref restockRooms);
            serializer.SerializeValue(ref restockedRooms);
            serializer.SerializeValue(ref cartTowels);
            serializer.SerializeValue(ref cartToiletries);
            serializer.SerializeValue(ref claimedGuestTasks);
            serializer.SerializeValue(ref collectedGuestItems);
            if (serializer.IsReader)
                guestTaskOwners.Length = RoomCount;
            for (int room = 0; room < RoomCount; room++)
            {
                ulong worker = guestTaskOwners.Length > room ? guestTaskOwners[room] : 0;
                serializer.SerializeValue(ref worker);
                if (serializer.IsReader)
                    guestTaskOwners[room] = worker;
            }
        }

        private bool ServiceTasksEqual(MotelService other) =>
            restockRooms == other.restockRooms && restockedRooms == other.restockedRooms &&
            cartTowels == other.cartTowels && cartToiletries == other.cartToiletries &&
            claimedGuestTasks == other.claimedGuestTasks && collectedGuestItems == other.collectedGuestItems &&
            guestTaskOwners.Equals(other.guestTaskOwners);
    }
}
