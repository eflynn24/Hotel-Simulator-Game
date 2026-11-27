using System;
using Unity.Collections;
using Unity.Netcode;

namespace DesertLantern
{
    internal enum GuestRequestKind { Towel, Pillow, Toiletries, Complaint }

    internal partial struct MotelService
    {
        public const int StaffWage = 25;
        public int Pillows, Toiletries;
        public int NextPhoneCall;
        public int MissedCalls;
        private int answeredCall;
        private ushort satisfiedRequests;
        private FixedList32Bytes<byte> requestKinds;
        private ushort staff;
        private ushort urgentOnly;
        private int lastStaffWork;
        private FixedList128Bytes<int> missedCallTimes;

        public bool BookingCallRinging => ClockMinute >= NextPhoneCall && answeredCall != NextPhoneCall;
        public bool CanBookPhone => answeredCall == NextPhoneCall;
        public bool PhoneRinging => BookingCallRinging || WaitingRequest(true) >= 0;
        public int MissedHistoryCount => missedCallTimes.Length;
        public int MissedCallTime(int index) => missedCallTimes[index];

        public void FinishPhoneBooking()
        {
            NextPhoneCall = ClockMinute + Math.Max(105, 340 - Demand * 2);
            bookingPhoneOwner = 0;
        }

        public GuestRequestKind KindOfRequest(int room) => (GuestRequestKind)requestKinds[room];
        public int GuestId(int room) => checkInMinutes[room] * RoomCount + room;
        public int RequestWait(int room) => Math.Max(0, ClockMinute - requestMinutes[room]);
        public bool RequestUrgent(int room) => RequestWait(room) >= 45 || KindOfRequest(room) == GuestRequestKind.Complaint;
        public bool RequestSatisfied(int room) => (satisfiedRequests & (1 << room)) != 0;

        public string GuestSatisfaction(int room)
        {
            int bit = 1 << room;
            if (RequestOpen(room) && RequestWait(room) >= 45)
                return "Impatient";
            if (RequestSatisfied(room))
                return (LateCheckIns & bit) != 0 ? "Satisfied · late check-in" : "Satisfied";
            if ((FulfilledRequests & bit) != 0)
                return "Unhappy · request cancelled";
            return (LateCheckIns & bit) != 0 ? "Unhappy · late check-in" : "Comfortable";
        }

        public string RequestLabel(int room) => KindOfRequest(room) switch
        {
            GuestRequestKind.Towel => "Deliver towels",
            GuestRequestKind.Pillow => "Deliver a pillow",
            GuestRequestKind.Toiletries => "Deliver toiletries",
            GuestRequestKind.Complaint when NoiseComplaint(room) => "Resolve a noise complaint",
            _ => "Resolve complaint"
        };

        public void CancelRequest(int room)
        {
            if ((ActiveRooms & (1 << room)) == 0)
                return;
            KnownRequests |= (ushort)(1 << room);
            FulfilledRequests |= (ushort)(1 << room);
        }

        public bool StaffHired(StaffRole role) => StaffHired((int)role);

        public bool StaffHired(int role) => role == 0 ? BellhopHired : (staff & (1 << role)) != 0;
        public bool StaffUrgentOnly(int role) => (urgentOnly & (1 << role)) != 0;

        public void ManageStaff(int role, bool priority)
        {
            if (priority)
                urgentOnly ^= (ushort)(1 << role);
            else if (StaffHired(role))
            {
                if (role == 0)
                {
                    RecoverBag(BellhopId);
                    BellhopHired = false;
                }
                else
                    staff &= (ushort)~(1 << role);
            }
            else if (Cash >= BellhopCost)
            {
                if (role == 0)
                    HireBellhop();
                else
                {
                    Post(Account.Payroll, -BellhopCost);
                    staff |= (ushort)(1 << role);
                }
            }
        }

        private void RecordMissed(int minute)
        {
            MissedCalls++;
            if (missedCallTimes.Length == missedCallTimes.Capacity)
                missedCallTimes.RemoveAt(0);
            missedCallTimes.Add(minute);
        }

        private void AdvanceOperations(int previous)
        {
            if (ClockMinute >= NextPhoneCall + 55)
            {
                if (BookingCallRinging || CanBookPhone)
                    MissBookingCall();
            }

            for (int room = 0; room < RoomCount; room++)
            {
                int bit = 1 << room;
                if ((ActiveRooms & bit) == 0)
                    continue;
                if ((FulfilledRequests & bit) != 0)
                {
                    if (ClockMinute >= requestMinutes[room] + 330 && checkoutMinutes[room] > ClockMinute + 120)
                    {
                        requestKinds[room] = (byte)((requestKinds[room] + 1) % 3);
                        requestMinutes[room] = ClockMinute + 20;
                        KnownRequests &= (ushort)~bit;
                        FulfilledRequests &= (ushort)~bit;
                    }
                    continue;
                }
                if (RequestWait(room) >= Patience(room))
                {
                    if ((room & 1) == 0 && (KnownRequests & (1 << room)) == 0)
                        RecordMissed(requestMinutes[room]);
                    CancelRequest(room);
                }
            }

            if (previous / MinutesPerDay != ClockMinute / MinutesPerDay)
                for (int role = 0; role < CountyPortfolio.StaffRoles.Length; role++)
                    if (StaffHired(role))
                    {
                        if (Cash >= StaffWage)
                            Post(Account.Payroll, -StaffWage);
                        else
                            ManageStaff(role, false);
                    }

            if (ClockMinute - lastStaffWork < 35)
                return;
            lastStaffWork = ClockMinute;
            if (StaffHired(3) && Delivery == SupplyDelivery.AtBay && (!StaffUrgentOnly(3) || PendingOrderCount >= 2))
            {
                const ulong stocker = ulong.MaxValue - 1;
                CollectSupply(stocker);
                StoreSupply(stocker);
            }
            WorkAttendant();
            if (StaffHired(StaffRole.Housekeeper))
                WorkHousekeeping(1);
        }

        private void SerializeOperations<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Pillows);
            serializer.SerializeValue(ref Toiletries);
            serializer.SerializeValue(ref NextPhoneCall);
            serializer.SerializeValue(ref MissedCalls);
            serializer.SerializeValue(ref answeredCall);
            serializer.SerializeValue(ref satisfiedRequests);
            serializer.SerializeValue(ref staff);
            serializer.SerializeValue(ref urgentOnly);
            serializer.SerializeValue(ref lastStaffWork);
            SerializeOrderList(serializer, ref missedCallTimes);
            if (serializer.IsReader)
                requestKinds.Length = RoomCount;
            for (int room = 0; room < RoomCount; room++)
            {
                byte kind = requestKinds.Length > room ? requestKinds[room] : (byte)0;
                serializer.SerializeValue(ref kind);
                if (serializer.IsReader)
                    requestKinds[room] = kind;
            }
        }

        private bool OperationsEqual(MotelService other) =>
            Pillows == other.Pillows && Toiletries == other.Toiletries && NextPhoneCall == other.NextPhoneCall &&
            MissedCalls == other.MissedCalls && answeredCall == other.answeredCall &&
            satisfiedRequests == other.satisfiedRequests && staff == other.staff && urgentOnly == other.urgentOnly &&
            requestKinds.Equals(other.requestKinds) && lastStaffWork == other.lastStaffWork && missedCallTimes.Equals(other.missedCallTimes);
    }
}
