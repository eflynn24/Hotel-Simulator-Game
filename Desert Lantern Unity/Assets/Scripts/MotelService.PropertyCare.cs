using System;
using Unity.Collections;
using Unity.Netcode;

namespace DesertLantern
{
    internal enum Fixture { Toilet, Sink, Shower, AirConditioner, Television, Lights, DoorLock, Fridge, Washer, Router }
    internal enum Account { RoomRevenue, ServiceRevenue, Payroll, Supplies, Maintenance, Electricity, Water, Internet, Adjustments, Financing }

    internal partial struct MotelService
    {
        public const int RoomFixtureCount = 8;
        private const int FixtureCount = RoomCount * RoomFixtureCount + 2;
        private const int AccountCount = 10;
        private const int FailedCondition = 20;
        private FixedList128Bytes<byte> condition;
        private FixedList32Bytes<byte> reviewFlags;
        private FixedList128Bytes<int> accounts;
        private ushort roomsClosed;
        private ushort repairComplaints;
        private ushort refundedGuests;
        public int RepairParts;
        public int LoanBalance;
        public bool Insured;

        private static int FixtureIndex(int room, Fixture fixture) => fixture >= Fixture.Washer
            ? RoomCount * RoomFixtureCount + fixture - Fixture.Washer : room * RoomFixtureCount + (int)fixture;

        public int Condition(int room, Fixture fixture) => condition[FixtureIndex(room, fixture)];
        public bool IsBroken(int room, Fixture fixture) => Condition(room, fixture) <= FailedCondition;
        public bool WasherBroken => IsBroken(0, Fixture.Washer);
        public bool RoomClosed(int room) => (roomsClosed & (1 << room)) != 0;

        public bool RoomUnusable(int room) =>
            RoomClosed(room) || PestRoom(room) || IsBroken(room, Fixture.DoorLock) || IsBroken(room, Fixture.Toilet);

        public void ToggleRoomClosure(int room) => roomsClosed ^= (ushort)(1 << room);

        private void InitializePropertyCare()
        {
            RepairParts = 4;
            Disinfectant = 6;
            for (int index = 0; index < FixtureCount; index++)
                condition.Add((byte)(72 + index * 19 % 29));
            for (int room = 0; room < RoomCount; room++)
                reviewFlags.Add(0);
            for (int index = 0; index < AccountCount * 3; index++)
                accounts.Add(0);
        }

        private void Wear(int room, Fixture fixture, int amount)
        {
            int index = FixtureIndex(room, fixture);
            int before = condition[index];
            condition[index] = (byte)EquipmentCondition.AfterUse(before, amount);
            if (before <= FailedCondition || condition[index] > FailedCondition)
                return;
            if (fixture == Fixture.Router)
            {
                repairComplaints |= ActiveRooms;
                return;
            }
            if (fixture == Fixture.Washer)
                return;
            int bit = 1 << room;
            if ((ActiveRooms & bit) == 0)
                return;
            repairComplaints |= (ushort)bit;
        }

        private void AdvancePropertyCare(int previous, bool invoiceUtilities)
        {
            AdvanceAccounts(previous, invoiceUtilities);
            int before = previous / 60;
            int now = ClockMinute / 60;
            for (int hour = before + 1; hour <= now; hour++)
            {
                for (int room = 0; room < RoomCount; room++)
                {
                    if ((ActiveRooms & (1 << room)) == 0)
                        continue;
                    int rotation = hour * 17 + room * 13 + GuestId(room);
                    Wear(room, (Fixture)(rotation % RoomFixtureCount), rotation % 11 == 0 ? 68 : 8);
                }
                if (LaundryRunning)
                    Wear(0, Fixture.Washer, hour % 17 == 0 ? 65 : 8);
                if (ActiveRoomCount > 0)
                    Wear(0, Fixture.Router, hour % 23 == 0 ? 68 : 5);
                if (StaffHired(4))
                    MaintainProperty();
            }
        }

        private void MaintainProperty()
        {
            int bestRoom = -1;
            int bestScore = int.MaxValue;
            Fixture bestFixture = default;
            for (int room = 0; room < RoomCapacity; room++)
            {
                bool occupied = (ActiveRooms & (1 << room)) != 0;
                for (int fixture = 0; fixture < RoomFixtureCount; fixture++)
                {
                    var target = (Fixture)fixture;
                    bool broken = IsBroken(room, target);
                    int condition = Condition(room, target);
                    bool urgent = broken && (occupied || target is Fixture.DoorLock or Fixture.Toilet);
                    if (!EmployeeEligible(StaffRole.Technician, urgent) ||
                        !CanWorkOperation(OperationKind.Maintenance, room + fixture * RoomCount, 0))
                        continue;
                    if (!broken && (StaffUrgentOnly((int)StaffRole.Technician) || RepairParts < 4 || condition >= 55))
                        continue;
                    int score = (broken ? 0 : 200) + (occupied ? 0 : 100) + condition;
                    if (score < bestScore)
                    {
                        bestRoom = room;
                        bestFixture = target;
                        bestScore = score;
                    }
                }
            }
            if (bestRoom >= 0 && IsBroken(bestRoom, bestFixture) && Repair(bestRoom, bestFixture, false))
                return;
            if (WasherBroken && CanWorkOperation(OperationKind.Maintenance, (int)Fixture.Washer * RoomCount, 0))
                Repair(0, Fixture.Washer, false);
            else if (IsBroken(0, Fixture.Router) && CanWorkOperation(OperationKind.Maintenance, (int)Fixture.Router * RoomCount, 0))
                Repair(0, Fixture.Router, false);
            else if (bestRoom >= 0)
                Repair(bestRoom, bestFixture, false);
        }

        public bool Repair(int room, Fixture fixture, bool replace)
        {
            if (Condition(room, fixture) == 100)
                return false;
            if (replace)
            {
                int cost = Insured && IsBroken(room, fixture) ? 24 : 65;
                if (Cash < cost)
                    return false;
                Post(Account.Maintenance, -cost);
            }
            else
            {
                if (RepairParts <= 0)
                    return false;
                RepairParts--;
            }
            int index = FixtureIndex(room, fixture);
            bool tracked = IsBroken(room, fixture) || condition[index] < 55;
            condition[index] = replace ? (byte)100 : (byte)EquipmentCondition.AfterRepair(condition[index], 65);
            if (tracked && condition[index] >= 55)
                FinishOperation(OperationKind.Maintenance, room + (int)fixture * RoomCount, 0);
            return true;
        }

        public void BuyMaintenanceStock(bool disinfectant)
        {
            int cost = disinfectant ? 12 : 18;
            if (Cash < cost)
                return;
            if (disinfectant)
                Disinfectant += 4;
            else
                RepairParts += 3;
            Post(disinfectant ? Account.Supplies : Account.Maintenance, -cost);
        }

        public void RefundGuest(int room)
        {
            int bit = 1 << room;
            if ((ActiveRooms & bit) != 0 && RefundAllowed(room) && (refundedGuests & bit) == 0 && Cash >= 15)
            {
                refundedGuests |= (ushort)bit;
                Post(Account.Adjustments, -15);
            }
        }

        private bool RefundAllowed(int room) => MaterialIssues(room) > 0 || IsBroken(0, Fixture.Router) || (LateCheckIns & (1 << room)) != 0;

        public int MaterialIssues(int room)
        {
            int issues = 0;
            for (int fixture = 0; fixture < RoomFixtureCount; fixture++)
                if (IsBroken(room, (Fixture)fixture))
                    issues++;
            return issues;
        }

        private void RecordReview(int room, bool assisted)
        {
            int bit = 1 << room;
            int outcome = 0;
            if ((LateCheckIns & bit) != 0) outcome |= 1;
            if (RequestSatisfied(room)) outcome |= 2;
            if ((lateGuestRequests & bit) != 0 || (!RequestSatisfied(room) || RequestDue(room)) && RequestWait(room) >= 45) outcome |= 4;
            if ((repairComplaints & bit) != 0 && (MaterialIssues(room) > 0 || IsBroken(0, Fixture.Router))) outcome |= 8;
            if (stayQuality[room] >= Expectations(room) && Cleanliness(room) >= 35) outcome |= 16;
            if ((DeliveredBags & bit) != 0) outcome |= 32;
            if (assisted) outcome |= 64;
            if (OpenNoiseComplaint(room)) outcome |= 128;
            RecordCustomerStars(ReviewRating(outcome));
            reviewFlags[room] = (byte)outcome;
            reputation = Math.Clamp(reputation + (ReviewRating(outcome) - 3) * 2, 0, 100);
            repairComplaints &= (ushort)~bit;
            refundedGuests &= (ushort)~bit;
        }

        private static int ReviewRating(int flags) => Math.Max(1, 5 - ((flags & 1) != 0 ? 1 : 0)
            - ((flags & 4) != 0 ? 1 : 0) - ((flags & 8) != 0 ? 2 : 0)
            - ((flags & 16) == 0 ? 1 : 0) - ((flags & 128) != 0 ? 1 : 0));

        public string GuestReview(int room)
        {
            int flags = reviewFlags[room];
            int rating = ReviewRating(flags);
            string service = (flags & 2) != 0 ? (flags & 4) != 0 ? "Staff helped after a long wait." : "Staff helped with my request."
                : (flags & 4) != 0 ? "I waited for help and my request went unresolved."
                : (flags & 32) != 0 ? "My luggage was brought to my room." : "Check-in was straightforward.";
            if ((flags & 1) != 0)
                service = "I had to wait at check-in. " + service;
            string conditionNote = (flags & 8) != 0 ? "Something broke in the room and stayed broken."
                : (flags & 16) != 0 ? "The room was tidy." : "The room needed more cleaning during my stay.";
            if ((flags & 128) != 0)
                conditionNote += " Noise kept me awake.";
            return $"{rating}/5 · {service} {conditionNote}";
        }

        private void Post(Account account, int amount)
        {
            Cash += amount;
            for (int period = 0; period < 3; period++)
                accounts[period * AccountCount + (int)account] += amount;
        }

        internal bool PayUtility(Account account, int amount)
        {
            if (amount <= 0 || Cash < amount)
                return false;
            Post(account, -amount);
            return true;
        }

        public int ReportAmount(int period, Account account) => accounts[period * AccountCount + (int)account];

        private void AdvanceAccounts(int previous, bool invoiceUtilities)
        {
            int day = ClockMinute / MinutesPerDay;
            int prior = previous / MinutesPerDay;
            if (day == prior)
                return;
            for (int index = 0; index < AccountCount; index++)
            {
                accounts[index] = 0;
                if (day / 7 != prior / 7)
                    accounts[AccountCount + index] = 0;
                if (day / 30 != prior / 30)
                    accounts[AccountCount * 2 + index] = 0;
            }
            if (!invoiceUtilities)
            {
                Post(Account.Electricity, -(4 + ActiveRoomCount));
                Post(Account.Water, -(3 + ActiveRoomCount));
                Post(Account.Internet, -3);
            }
            if (Insured)
                Post(Account.Maintenance, -6);
            if (LoanBalance > 0)
            {
                int principal = Math.Min(18, LoanBalance);
                LoanBalance -= principal;
                Post(Account.Financing, -(principal + 2));
            }
        }

        public void ManageFinances(int action)
        {
            if (action == 0 && LoanBalance == 0)
            {
                LoanBalance = 180;
                Post(Account.Financing, 180);
            }
            else if (action == 1)
                Insured = !Insured;
        }

        private void SerializePropertyCare<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref RepairParts);
            serializer.SerializeValue(ref LoanBalance);
            serializer.SerializeValue(ref Insured);
            serializer.SerializeValue(ref roomsClosed);
            serializer.SerializeValue(ref repairComplaints);
            serializer.SerializeValue(ref refundedGuests);
            if (serializer.IsReader)
            {
                condition.Length = FixtureCount;
                reviewFlags.Length = RoomCount;
            }
            for (int index = 0; index < FixtureCount; index++)
            {
                byte value = condition.Length > index ? condition[index] : (byte)100;
                serializer.SerializeValue(ref value);
                if (serializer.IsReader)
                    condition[index] = value;
            }
            for (int room = 0; room < RoomCount; room++)
            {
                byte review = reviewFlags.Length > room ? reviewFlags[room] : (byte)0;
                serializer.SerializeValue(ref review);
                if (serializer.IsReader)
                    reviewFlags[room] = review;
            }
            SerializeOrderList(serializer, ref accounts);
        }

        private bool PropertyCareEquals(MotelService other) =>
            condition.Equals(other.condition) && reviewFlags.Equals(other.reviewFlags) && accounts.Equals(other.accounts) &&
            RepairParts == other.RepairParts && LoanBalance == other.LoanBalance && Insured == other.Insured &&
            roomsClosed == other.roomsClosed && repairComplaints == other.repairComplaints && refundedGuests == other.refundedGuests;
    }
}
