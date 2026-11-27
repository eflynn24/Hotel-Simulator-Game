using System;
using Unity.Netcode;

namespace DesertLantern
{
    internal enum ManagementDecision : byte { None, UnexpectedArrival, MaintenanceQuote, SupplierQuote }

    internal partial struct MotelService
    {
        private ManagementDecision decision;
        private int decisionRoom, decisionArrival, decisionDeadline, lastDecisionDay;
        private Fixture decisionFixture;
        private int reviewCount, reviewStars;

        public ManagementDecision PendingDecision => decision;
        public int DecisionRoom => decisionRoom;
        public int DecisionDeadline => decisionDeadline;
        public Fixture DecisionFixture => decisionFixture;
        public int DecisionQuote => decision == ManagementDecision.UnexpectedArrival
            ? ReservationRateIfPending() : Insured && IsBroken(decisionRoom, decisionFixture) ? 24 : 65;

        public float CustomerStars
        {
            get
            {
                if (reviewCount > 0)
                    return (float)reviewStars / reviewCount;
                int total = 0, count = 0;
                for (int room = 0; room < RoomCapacity; room++)
                    if ((Reviews & (1 << room)) != 0)
                    {
                        count++;
                        total += ReviewRating(reviewFlags[room]);
                    }
                return count == 0 ? 0 : (float)total / count;
            }
        }

        public int RenovatedRooms
        {
            get
            {
                int count = 0;
                for (int room = 0; room < RoomCapacity; room++)
                    if (FurnitureQuality(room) > 0)
                        count++;
                return count;
            }
        }

        private void RecordCustomerStars(int rating)
        {
            if (reviewCount == 0)
                for (int room = 0; room < RoomCapacity; room++)
                    if ((Reviews & (1 << room)) != 0)
                    {
                        reviewCount++;
                        reviewStars += ReviewRating(reviewFlags[room]);
                    }
            reviewCount++;
            reviewStars += rating;
        }

        private void AdvanceDecisions(int previous)
        {
            if (decision != ManagementDecision.None && (ClockMinute >= decisionDeadline ||
                decision == ManagementDecision.UnexpectedArrival && DecisionBookingIndex() < 0 ||
                decision == ManagementDecision.MaintenanceQuote && Condition(decisionRoom, decisionFixture) == 100))
                decision = ManagementDecision.None;
            int day = ClockMinute / MinutesPerDay;
            if (decision != ManagementDecision.None || day == lastDecisionDay || ClockMinute / 15 == previous / 15 ||
                ClockMinute % MinutesPerDay < 13 * 60 || ClockMinute % MinutesPerDay >= 21 * 60)
                return;

            int options = 0;
            for (int room = 0; room < RoomCapacity; room++)
                if (FreeForDecision(room))
                    options++;
            if (options <= 1)
                for (int index = 0; index < ReservationCount; index++)
                {
                    int room = ReservationRoom(index);
                    int arrival = ReservationStart(index);
                    if (ReservationPending(index) || ReservationChannel(index) == BookingChannel.Conference || arrival <= ClockMinute + 30 ||
                        arrival / MinutesPerDay != day || !RoomAvailable(room, ClockMinute, NextMorningDeparture, index) ||
                        !RoomReadyForDecision(room))
                        continue;
                    decision = ManagementDecision.UnexpectedArrival;
                    decisionRoom = room;
                    decisionArrival = arrival;
                    decisionDeadline = Math.Min(ClockMinute + 120, arrival);
                    lastDecisionDay = day;
                    return;
                }

            for (int room = 0; room < RoomCapacity; room++)
                for (int fixture = 0; fixture < RoomFixtureCount; fixture++)
                {
                    var target = (Fixture)fixture;
                    if (Condition(room, target) >= 65)
                        continue;
                    decision = ManagementDecision.MaintenanceQuote;
                    decisionFixture = target;
                    decisionRoom = room;
                    decisionDeadline = ClockMinute + 240;
                    lastDecisionDay = day;
                    return;
                }
            if (Towels < 4 && PendingOrderCount == 0)
            {
                decision = ManagementDecision.SupplierQuote;
                decisionDeadline = ClockMinute + 180;
                lastDecisionDay = day;
            }
        }

        private int DecisionBookingIndex()
        {
            for (int index = 0; index < ReservationCount; index++)
                if (ReservationRoom(index) == decisionRoom && ReservationStart(index) == decisionArrival)
                    return index;
            return -1;
        }

        private int ReservationRateIfPending()
        {
            int index = DecisionBookingIndex();
            return index < 0 ? 0 : ReservationRate(index);
        }

        private bool RoomReadyForDecision(int room) =>
            RoomReadyForGuest(room);

        private bool FreeForDecision(int room) =>
            RoomReadyForDecision(room) && RoomAvailable(room, ClockMinute, NextMorningDeparture);

        public int UpgradedAlternative()
        {
            for (int room = 0; room < RoomCapacity; room++)
                if (room != decisionRoom && FurnitureQuality(room) >= 14 && FreeForDecision(room))
                    return room;
            return -1;
        }

        public bool CanResolveDecision(int choice)
        {
            if (decision == ManagementDecision.None || ClockMinute >= decisionDeadline || (uint)choice > 2)
                return false;
            if (decision == ManagementDecision.SupplierQuote)
                return choice == 2 || PendingOrderCount == 0 && Cash >= (choice == 0 ? 18 : 11);
            if (decision == ManagementDecision.MaintenanceQuote)
                return Condition(decisionRoom, decisionFixture) < 100 &&
                    (choice == 2 || (choice == 0 ? RepairParts > 0 && Cash >= 18 : Cash >= DecisionQuote));

            int index = DecisionBookingIndex();
            if (index < 0)
                return false;
            if (choice == 0)
                return true;
            if (choice == 2)
                return UpgradedAlternative() >= 0;
            return RoomReadyForDecision(decisionRoom) && RoomAvailable(decisionRoom, ClockMinute, NextMorningDeparture, index);
        }

        public bool ResolveDecision(int token, int choice)
        {
            if (token != decisionDeadline || !CanResolveDecision(choice))
                return false;
            if (decision == ManagementDecision.SupplierQuote)
            {
                if (choice != 2 && !OrderCartPriced(1 << 4, choice == 0 ? 18 : 11, choice == 0 ? 72 : 180))
                    return false;
            }
            else if (decision == ManagementDecision.MaintenanceQuote)
            {
                if (choice == 0)
                {
                    if (!Repair(decisionRoom, decisionFixture, false))
                        return false;
                    Post(Account.Maintenance, -18);
                }
                else if (choice == 1)
                {
                    if (!Repair(decisionRoom, decisionFixture, true))
                        return false;
                }
                else
                    Wear(decisionRoom, decisionFixture, 12);
            }
            else if (choice != 0)
            {
                MotelService proposed = this;
                int room = choice == 1 ? decisionRoom : UpgradedAlternative();
                if (choice == 1)
                    proposed.RemoveBooking(DecisionBookingIndex());
                int arrival = ClockMinute + ScheduleStep;
                if (!proposed.BookAtRate(room, arrival, NextMorningDeparture, BookingChannel.Online, BookingPreference.None, choice == 1 ? 250 : 175))
                    return false;
                if (choice == 1)
                {
                    proposed.Post(Account.Adjustments, -35);
                    proposed.reputation = Math.Max(0, proposed.reputation - 9);
                }
                this = proposed;
            }
            else
                reputation = Math.Min(100, reputation + 1);
            decision = ManagementDecision.None;
            return true;
        }

        private void SerializeDecisions<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref decision);
            serializer.SerializeValue(ref decisionRoom);
            serializer.SerializeValue(ref decisionArrival);
            serializer.SerializeValue(ref decisionDeadline);
            serializer.SerializeValue(ref lastDecisionDay);
            serializer.SerializeValue(ref decisionFixture);
            serializer.SerializeValue(ref reviewCount);
            serializer.SerializeValue(ref reviewStars);
        }

        private bool DecisionsEqual(MotelService other) => decision == other.decision &&
            decisionRoom == other.decisionRoom && decisionArrival == other.decisionArrival &&
            decisionDeadline == other.decisionDeadline && lastDecisionDay == other.lastDecisionDay &&
            decisionFixture == other.decisionFixture && reviewCount == other.reviewCount && reviewStars == other.reviewStars;
    }
}
