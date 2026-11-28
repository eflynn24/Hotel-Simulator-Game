using Unity.Netcode;

namespace DesertLantern
{
    internal enum GuestActivity { InRoom, AtAttractions, AtVending, HostingVisitor, Walking, Socializing, Showering, WatchingTelevision, SeekingHelp }
    internal enum GuestIssue { None, ExtraNight, LostKey, Equipment, Charges, LateDeparture, Noise }

    internal partial struct MotelService
    {
        private ushort requestedExtension;
        private ushort lostKeys;
        private ushort unattendedProperty;
        private ushort heldCheckout;
        private ushort chargeDisputes;
        private ushort returnGuests;
        private ushort guestRecommendations;

        private int StayAge(int room) => ClockMinute - checkInMinutes[room];
        private int StayProfile(int room) => (requestMinutes[room] / 30 + room * 3) % 12;

        public GuestActivity Activity(int room)
        {
            if (RequestOpen(room) && RequestWait(room) >= 20)
                return GuestActivity.SeekingHelp;
            int age = StayAge(room) % MinutesPerDay;
            int start = 120 + 30 * (StayProfile(room) % 4);
            if (age >= 30 && age < 65 && !IsBroken(room, Fixture.Shower))
                return GuestActivity.Showering;
            if (age >= 75 && age < start)
                return GuestActivity.Walking;
            if (age >= start && age < start + 120 && room % 3 != 2)
                return GuestActivity.AtAttractions;
            if (HasExpansion(Expansion.Vending) && age >= start + 145 && age < start + 185)
                return GuestActivity.AtVending;
            if (age >= start + 200 && age < start + 340 && room % 3 == 1)
                return GuestActivity.HostingVisitor;
            if (age >= start + 340 && age < start + 475)
                return GuestActivity.Socializing;
            if (age >= start + 485 && age < start + 635 &&
                (TvPackages & (1 << room)) != 0 && !IsBroken(room, Fixture.Television))
                return GuestActivity.WatchingTelevision;
            return GuestActivity.InRoom;
        }

        public GuestIssue OutstandingIssue(int room)
        {
            int bit = 1 << room;
            if ((heldCheckout & bit) != 0) return GuestIssue.LateDeparture;
            if ((repairComplaints & bit) != 0 && (MaterialIssues(room) > 0 || IsBroken(0, Fixture.Router))) return GuestIssue.Equipment;
            if (OpenNoiseComplaint(room)) return GuestIssue.Noise;
            if ((lostKeys & bit) != 0) return GuestIssue.LostKey;
            if ((chargeDisputes & bit) != 0) return GuestIssue.Charges;
            if ((requestedExtension & bit) != 0) return GuestIssue.ExtraNight;
            return GuestIssue.None;
        }

        public bool ForgottenItem(int room) => (unattendedProperty & (1 << room)) != 0;
        public void CollectForgottenItem(int room)
        {
            if (!ForgottenItem(room))
                return;
            int bit = 1 << room;
            FinishOperation(OperationKind.LostProperty, room, GuestId(room));
            unattendedProperty &= (ushort)~bit;
            if (!RoomDirty(room) && (ActiveRooms & bit) == 0)
                UnlockedDoors &= (ushort)~bit;
        }
        public bool ReturningGuest(int room) => (returnGuests & (1 << room)) != 0;

        public string GuestMemory(int room) => (reviewFlags[room] & 2) != 0
            ? "Remembers the help you gave on their last stay."
            : "Remembers their previous stay at the motel.";

        public void ResolveGuestIssue(int room, GuestIssue issue)
        {
            if ((ActiveRooms & (1 << room)) == 0 || OutstandingIssue(room) != issue)
                return;
            int bit = 1 << room;
            switch (issue)
            {
                case GuestIssue.ExtraNight:
                    if (TryExtendStay(room))
                        requestedExtension &= (ushort)~bit;
                    break;
                case GuestIssue.Noise:
                    quietedRooms |= (ushort)bit;
                    break;
                case GuestIssue.LostKey:
                    lostKeys &= (ushort)~bit;
                    break;
                case GuestIssue.Charges:
                    if (Cash >= 12)
                    {
                        Post(Account.Adjustments, -12);
                        chargeDisputes &= (ushort)~bit;
                    }
                    break;
                case GuestIssue.Equipment:
                    RefundGuest(room);
                    break;
                case GuestIssue.LateDeparture:
                    heldCheckout &= (ushort)~bit;
                    CompleteCheckout(room, true);
                    break;
            }
            if (issue != GuestIssue.LateDeparture && OutstandingIssue(room) != issue)
                FinishOperation(OperationKind.Guest, room, GuestId(room));
        }

        private void AdvanceGuestLife(int previous)
        {
            for (int room = 0; room < RoomCount; room++)
            {
                int bit = 1 << room;
                if ((ActiveRooms & bit) == 0)
                    continue;
                int age = StayAge(room);
                int priorAge = age - (ClockMinute - previous);
                bool Reached(int milestone) => age >= milestone && priorAge < milestone;
                int profile = StayProfile(room);
                if (Reached(300) && profile % 3 == 0)
                    requestedExtension |= (ushort)bit;
                if (Reached(420) && profile % 7 == 0)
                    lostKeys |= (ushort)bit;
                if (Reached(520) && profile % 5 == 0)
                    chargeDisputes |= (ushort)bit;
                if (Reached(620) && profile == 4 && checkoutMinutes[room] > ClockMinute + 30)
                    checkoutMinutes[room] = ClockMinute + 30;
                if (profile == 8 && previous < checkoutMinutes[room] && ClockMinute >= checkoutMinutes[room])
                    heldCheckout |= (ushort)bit;
                int vend = 265 + 30 * (profile % 4);
                if (CrossedDailyActivity(priorAge, age, vend))
                    ServeVendingGuest(room);
                if (CrossedDailyActivity(priorAge, age, 30))
                    Wear(room, Fixture.Shower, 3);
                if ((TvPackages & bit) != 0 && CrossedDailyActivity(priorAge, age, 605 + 30 * (profile % 4)))
                    Wear(room, Fixture.Television, 3);
            }
        }

        private static bool CrossedDailyActivity(int before, int after, int minute)
        {
            if (after < minute)
                return false;
            int lastOccurrence = minute + (after - minute) / MinutesPerDay * MinutesPerDay;
            return before < lastOccurrence;
        }

        private void SettleGuest(int room)
        {
            int bit = 1 << room;
            int profile = StayProfile(room);
            int damage = profile == 6 && TaskPending(room, CleaningTask.Bathroom) ? 10 : 0;
            Post(Account.Financing, -20);
            if (damage > 0)
                Post(Account.Adjustments, damage);
            if (profile % 5 == 1)
                unattendedProperty |= (ushort)bit;
            if (RequestSatisfied(room) && (LateCheckIns & bit) == 0 && MaterialIssues(room) == 0)
            {
                returnGuests |= (ushort)bit;
                guestRecommendations |= (ushort)bit;
                Post(Account.ServiceRevenue, 7);
            }
            requestedExtension &= (ushort)~bit;
            lostKeys &= (ushort)~bit;
            heldCheckout &= (ushort)~bit;
            chargeDisputes &= (ushort)~bit;
        }

        private void SerializeGuestLife<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref requestedExtension);
            serializer.SerializeValue(ref lostKeys);
            serializer.SerializeValue(ref unattendedProperty);
            serializer.SerializeValue(ref heldCheckout);
            serializer.SerializeValue(ref chargeDisputes);
            serializer.SerializeValue(ref returnGuests);
            serializer.SerializeValue(ref guestRecommendations);
        }

        private bool GuestLifeEquals(MotelService other) =>
            requestedExtension == other.requestedExtension && lostKeys == other.lostKeys &&
            unattendedProperty == other.unattendedProperty && heldCheckout == other.heldCheckout &&
            chargeDisputes == other.chargeDisputes && returnGuests == other.returnGuests &&
            guestRecommendations == other.guestRecommendations;
    }
}
