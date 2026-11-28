using System;
using Unity.Netcode;
using Unity.Collections;

namespace DesertLantern
{
    internal enum SupplyDelivery : byte { None, InTransit, AtBay, Carried }

    internal enum BookingChannel : byte { WalkIn, Online, Telephone, Conference }
    internal enum BookingPreference : byte { None, ExtraTowels, ExtraPillow, Toiletries }

    internal enum LuggageStatus : byte { None, InCar, Carried }

    internal readonly struct SupplyOffer
    {
        public readonly string Name;
        public readonly int Price;
        public readonly int Quantity;

        public SupplyOffer(string name, int price, int quantity)
        {
            Name = name;
            Price = price;
            Quantity = quantity;
        }
    }

    internal partial struct MotelService : INetworkSerializable, IEquatable<MotelService>
    {
        public const int BaseRoomCount = 8;
        public const int RoomCount = 12;
        public const int StartingCash = 230;
        public const int RoomRate = 65;
        public const int LuggageTip = 10;
        public const int MinutesPerDay = 1440;
        public const int OpeningMinute = 12 * 60;
        public const int ScheduleStep = 15;
        public const int BellhopCost = 55;
        public const ulong BellhopId = ulong.MaxValue;
        public static readonly SupplyOffer[] Catalog =
        {
            new SupplyOffer("Cleaning kit", 12, 1),
            new SupplyOffer("Towels", 18, 4),
            new SupplyOffer("Linen sets", 26, 2),
            new SupplyOffer("Trash bags", 8, 4),
            new SupplyOffer("Vacuum", 45, 1),
            new SupplyOffer("Detergent", 12, 4),
            new SupplyOffer("Pillows", 20, 2),
            new SupplyOffer("Toiletries", 10, 4)
        };
        public int Cash;
        public int CleaningKits;
        public int Towels;
        public int LinenSets;
        public SupplyDelivery Delivery;
        public int DeliveryDue;
        public int ClockMinute;
        public byte ClockSpeed;
        public ushort ActiveRooms;
        public ushort Reviews;
        public ushort LateCheckIns;
        public ushort MissedCheckouts;
        private FixedList64Bytes<int> checkoutMinutes;
        private FixedList64Bytes<int> requestMinutes;
        public bool BellhopHired;
        public ushort KnownRequests;
        public ushort FulfilledRequests;
        public ushort TvPackages;
        public ushort MealOrders;
        private FixedList512Bytes<int> reservations;
        public int ReservationCount => reservations.Length / 4;
        public ulong SupplyCarrier;
        public ushort UnlockedDoors;
        public ushort OpenDoors;
        public ushort DeliveredBags;
        public byte BagRoom;
        public ulong Carrier;
        public LuggageStatus Bag;

        public static MotelService New()
        {
            var service = new MotelService
            {
                property = PropertyId.RoadsideMotel, Cash = StartingCash, ClockMinute = OpeningMinute, ClockSpeed = 1,
                Pillows = 2, Toiletries = 4, lastDecisionDay = -1, NextPhoneCall = OpeningMinute + 96
            };
            for (int room = 0; room < RoomCount; room++)
            {
                service.checkoutMinutes.Add(0);
                service.requestMinutes.Add(0);
                service.requestKinds.Add(0);
                service.cleaningTasks.Add(0);
                service.completedCleaning.Add(0);
            }
            service.stayBookingDetails.Length = RoomCount;
            service.InitializePropertyCare();
            service.InitializeManagement();
            return service;
        }

        public static string DisplayTime(int minute) =>
            $"Day {minute / MinutesPerDay + 1}  {minute / 60 % 24:00}:{minute % 60:00}";

        public int ReservationRoom(int index) => reservations[index * 4];
        public int ReservationStart(int index) => reservations[index * 4 + 1];
        public int ReservationEnd(int index) => reservations[index * 4 + 2];
        public BookingChannel ReservationChannel(int index) => (BookingChannel)(reservations[index * 4 + 3] & 3);
        public BookingPreference ReservationPreference(int index) => (BookingPreference)((reservations[index * 4 + 3] >> 2) & 3);
        // Keep the approval flag in the existing serialized reservation record.
        private const int PendingReservation = 1 << 30;
        private const int ManualApproval = 1 << 15; // Reserved bit in the serialized staff flags.
        private const int RateMask = 0xFFFF;
        public bool ManualOnlineApproval => (staff & ManualApproval) != 0;
        public void SetOnlineApproval(bool manual)
        {
            staff = (ushort)(manual ? staff | ManualApproval : staff & ~ManualApproval);
        }

        private static readonly string[] ReservationGuests =
        {
            "Mara Ortiz", "Eli Brooks", "June Park", "Vic Ramos", "Nora Reed", "Sam Bell",
            "Lena Ruiz", "Tess Lane", "Rae King", "Luis Webb", "Jo Chen", "Max Vega"
        };

        public string ReservationGuest(int index) => ReservationGuests[ReservationGuestIndex(index)];
        private int ReservationGuestIndex(int index)
        {
            int stored = (reservations[index * 4 + 3] >> 20) & 15;
            return stored > 0 ? stored - 1 : GuestIndex(ReservationRoom(index), ReservationStart(index));
        }

        private static int GuestIndex(int room, int start) =>
            (start / ScheduleStep + room) % ReservationGuests.Length;

        private FixedList32Bytes<byte> stayBookingDetails;
        private int confirmedOnlineBookings;
        public int ConfirmedOnlineBookings => confirmedOnlineBookings;
        public string StayGuest(int room) => stayBookingDetails.Length > room && stayBookingDetails[room] != 0
            ? ReservationGuests[((stayBookingDetails[room] >> 2) & 15) - 1] : ReservationGuests[room];
        public string StaySource(int room) => stayBookingDetails.Length > room && stayBookingDetails[room] != 0
            ? ((BookingChannel)(stayBookingDetails[room] & 3)).ToString() : "Reservation";
        public string StayRequest(int room) =>
            NotebookRequest(room) ?? (stayBookingDetails.Length > room
                ? PreferenceLabel((BookingPreference)(stayBookingDetails[room] >> 6))
                : PreferenceLabel(BookingPreference.None));

        public static int StayNights(int start, int end) => Math.Max(1, end / MinutesPerDay - start / MinutesPerDay);
        public static int DepartureForNights(int start, int nights) =>
            (start / MinutesPerDay + nights) * MinutesPerDay + 11 * 60;

        public int SuggestRoom(int start, int end, int after = -1, int ignore = -1, bool readyNow = false)
        {
            for (int offset = 1; offset <= RoomCapacity; offset++)
            {
                int room = (after + offset) % RoomCapacity;
                if (RoomAvailable(room, start, end, ignore) && (!readyNow || RoomReadyForGuest(room)))
                    return room;
            }
            return -1;
        }

        private void SerializeBookingDetails<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsReader)
                stayBookingDetails.Length = RoomCount;
            for (int room = 0; room < RoomCount; room++)
            {
                byte details = stayBookingDetails.Length > room ? stayBookingDetails[room] : (byte)0;
                serializer.SerializeValue(ref details);
                if (serializer.IsReader)
                    stayBookingDetails[room] = details;
            }
            serializer.SerializeValue(ref confirmedOnlineBookings);
        }
        public int ReservationRate(int index) => (reservations[index * 4 + 3] >> 4) & RateMask;
        public bool ReservationPending(int index) => (reservations[index * 4 + 3] & PendingReservation) != 0;

        public int FirstPendingReservation => FindPendingReservations(out _);

        public int PendingReservationCount
        {
            get
            {
                FindPendingReservations(out int count);
                return count;
            }
        }

        private int FindPendingReservations(out int count)
        {
            count = 0;
            int first = -1;
            for (int index = 0; index < ReservationCount; index++)
                if (ReservationPending(index) && ReservationChannel(index) != BookingChannel.WalkIn)
                {
                    if (first < 0)
                        first = index;
                    count++;
                }
            return first;
        }

        public bool AcceptReservation(int index, int start)
        {
            if (index < 0 || index >= ReservationCount || !ReservationPending(index) ||
                ReservationStart(index) != start || ReservationEnd(index) <= ClockMinute ||
                (ReservationChannel(index) == BookingChannel.WalkIn
                    ? ClockMinute >= start + WalkInPatience || !RoomReadyForGuest(ReservationRoom(index))
                    : start < ClockMinute) ||
                !RoomAvailable(ReservationRoom(index), start, ReservationEnd(index), index))
                return false;
            reservations[index * 4 + 3] &= ~PendingReservation;
            return true;
        }

        public static string PreferenceLabel(BookingPreference preference) => preference switch
        {
            BookingPreference.ExtraTowels => "Extra towels",
            BookingPreference.ExtraPillow => "Extra pillow",
            BookingPreference.Toiletries => "Toiletries",
            _ => "No special request"
        };


        private int NextBooking(int room)
        {
            int next = -1;
            for (int i = 0; i < ReservationCount; i++)
                if (ReservationRoom(i) == room && (next < 0 || ReservationStart(i) < ReservationStart(next)))
                    next = i;
            return next;
        }

        public int BookingStart(int room) => ReservationStart(NextBooking(room));
        public int BookingEnd(int room) => ReservationEnd(NextBooking(room));
        public bool UpcomingReservation(int room) => NextBooking(room) >= 0;

        public bool RoomAvailable(int room, int start, int end, int ignore = -1, bool extending = false)
        {
            if (!RoomBuilt(room) || start >= end || RoomUnusable(room) ||
                !extending && (ActiveRooms & (1 << room)) != 0 && start < checkoutMinutes[room])
                return false;
            for (int index = 0; index < ReservationCount; index++)
                if (index != ignore && ReservationRoom(index) == room &&
                    start < ReservationEnd(index) && ReservationStart(index) < end)
                    return false;
            return true;
        }

        public bool Book(int room, int start, int end, BookingChannel channel, BookingPreference preference = BookingPreference.None) =>
            BookAtRate(room, start, end, channel, preference, RoomBuilt(room) ? NightlyRate(room) : 0);

        public bool QueuePhoneReservation(int room, int start, int end, BookingPreference preference, ulong worker)
        {
            if (!CanBookPhoneFor(worker) || !RoomBuilt(room) ||
                !BookAtRate(room, start, end, BookingChannel.Telephone, preference, NightlyRate(room), pending: true))
                return false;
            FinishPhoneBooking();
            return true;
        }

        private bool BookAtRate(int room, int start, int end, BookingChannel channel, BookingPreference preference, int rate, bool pending = false)
        {
            if (reservations.Length + 4 > reservations.Capacity || start < ClockMinute ||
                end > ClockMinute + 14 * MinutesPerDay || !RoomAvailable(room, start, end))
                return false;
            reservations.Add(room);
            reservations.Add(start);
            reservations.Add(end);
            reservations.Add((int)channel | ((int)preference << 2) | ((rate & RateMask) << 4) | ((GuestIndex(room, start) + 1) << 20) | (pending ? PendingReservation : 0));
            return true;
        }

        public bool ModifyBooking(int index, int operation)
        {
            if (index < 0 || index >= ReservationCount)
                return false;
            if (ReservationChannel(index) == BookingChannel.Conference && ReservationStart(index) == ConferenceStart)
            {
                if (operation != 0 || ConferenceInProgress)
                    return false;
                CancelConference();
                return true;
            }
            if (operation == 0)
            {
                if (!ReservationPending(index) && ReservationStart(index) - ClockMinute <= 120)
                    Post(Account.Adjustments, 10);
                RemoveBooking(index);
                return true;
            }

            int room = ReservationRoom(index);
            int start = ReservationStart(index);
            int end = ReservationEnd(index);
            switch (operation)
            {
                case 1: start += ScheduleStep; break;
                case 2: start -= ScheduleStep; break;
                case 3: end += MinutesPerDay; break;
                case 4: end -= MinutesPerDay; break;
                case 5:
                    room = SuggestRoom(start, end, room, index);
                    if (room == ReservationRoom(index))
                        return false;
                    break;
                default: return false;
            }
            if (start < ClockMinute || end > ClockMinute + 14 * MinutesPerDay || !RoomAvailable(room, start, end, index))
                return false;
            int guest = ReservationGuestIndex(index);
            reservations[index * 4 + 3] = (reservations[index * 4 + 3] & ~(15 << 20)) | ((guest + 1) << 20);
            reservations[index * 4] = room;
            reservations[index * 4 + 1] = start;
            reservations[index * 4 + 2] = end;
            return true;
        }

        public bool ExtendStay(int room) => TryExtendStay(room);

        private bool TryExtendStay(int room)
        {
            if ((ActiveRooms & (1 << room)) == 0)
                return false;
            int departure = checkoutMinutes[room];
            if (!RoomAvailable(room, departure, departure + MinutesPerDay, extending: true))
                return false;
            checkoutMinutes[room] += MinutesPerDay;
            Post(Account.RoomRevenue, stayRates[room]);
            return true;
        }

        private void RemoveBooking(int index)
        {
            for (int field = 0; field < 4; field++)
                reservations.RemoveAt(index * 4);
        }

        public int ActiveRoomCount
        {
            get
            {
                int count = 0;
                for (int room = 0; room < RoomCount; room++)
                    if ((ActiveRooms & (1 << room)) != 0)
                        count++;
                return count;
            }
        }

        public void AdvanceClock(int now) => AdvanceClock(now, false);

        public void AdvanceClock(int now, bool invoiceUtilities)
        {
            int previous = ClockMinute;
            if (now <= previous)
                return;
            ClockMinute = now;
            AdvanceRoomWear(previous);
            AdvancePropertyCare(previous, invoiceUtilities);
            AdvanceLaundry();
            AdvanceOperations(previous);
            AdvanceGuestLife(previous);
            AdvanceManagement(previous);
            AdvanceExpansion(previous);
            AdvanceDecisions(previous);
            GenerateWalkIn();
            if (!IsBroken(0, Fixture.Router) && previous / MinutesPerDay != now / MinutesPerDay)
            {
                int day = now / MinutesPerDay;
                int arrival = (day + 1) * MinutesPerDay + 15 * 60;
                int departure = arrival + (1 + day % 3) * MinutesPerDay - 4 * 60;
                int preferred = day % RoomCapacity;
                int referredRoom = -1;
                for (int room = 0; room < RoomCapacity; room++)
                    if ((guestRecommendations & (1 << room)) != 0)
                    {
                        preferred = referredRoom = room;
                        break;
                    }
                int expectedBookings = (Demand >= 85 ? 3 : Demand >= 60 ? 2 : 1) + (referredRoom >= 0 ? 1 : 0);
                bool referred = false;
                for (int offset = 0; offset < RoomCapacity && expectedBookings > 0; offset++)
                {
                    int room = (preferred + offset) % RoomCapacity;
                    if (BookAtRate(room, arrival, departure, BookingChannel.Online,
                        (BookingPreference)(1 + day % 3), NightlyRate(room), pending: ManualOnlineApproval))
                    {
                        expectedBookings--;
                        referred = true;
                        if (!ManualOnlineApproval)
                            confirmedOnlineBookings++;
                    }
                }
                if (referred && referredRoom >= 0)
                    guestRecommendations &= (ushort)~(1 << referredRoom);
            }
            for (int index = ReservationCount - 1; index >= 0; index--)
            {
                if (ReservationEnd(index) <= now || ReservationPending(index) && ReservationStart(index) +
                    (ReservationChannel(index) == BookingChannel.WalkIn ? WalkInPatience - 1 : 0) < now)
                    RemoveBooking(index);
                else if (previous < ReservationStart(index) && ReservationStart(index) <= now)
                    ClockSpeed = 1;
            }
            for (int room = 0; room < checkoutMinutes.Length; room++)
            {
                if ((ActiveRooms & (1 << room)) == 0)
                    continue;
                int departure = checkoutMinutes[room];
                if (previous < departure && departure <= now ||
                    previous < requestMinutes[room] && requestMinutes[room] <= now)
                    ClockSpeed = 1;
                if (now >= departure + ((heldCheckout & (1 << room)) != 0 ? 240 : 180))
                    CompleteCheckout(room, false);
            }
        }

        private const int WalkInPatience = 120;

        private void GenerateWalkIn()
        {
            int hour = ClockMinute % MinutesPerDay / 60;
            if (hour < 9 || hour >= 20 || ClockMinute < Math.Max(OpeningMinute + 120, nextWalkInMinute))
                return;
            nextWalkInMinute = ClockMinute + Math.Max(180, 480 - Demand * 3);
            int nights = 1 + ClockMinute / 60 % 3;
            int end = DepartureForNights(ClockMinute, nights);
            int room = SuggestRoom(ClockMinute, end, readyNow: true);
            if (room >= 0)
                BookAtRate(room, ClockMinute, end, BookingChannel.WalkIn, BookingPreference.None,
                    NightlyRate(room), pending: true);
        }

        public bool DeskArrivalReady(int room)
        {
            if (!RoomReadyForGuest(room))
                return false;
            int index = NextBooking(room);
            return index >= 0 && (BookingDue(room) || ReservationPending(index) &&
                ReservationChannel(index) == BookingChannel.WalkIn && ReservationStart(index) <= ClockMinute &&
                ClockMinute < ReservationStart(index) + WalkInPatience);
        }

        public int NextDeskArrival()
        {
            int room = NextArrival();
            if (room >= 0)
                return room;
            for (int candidate = 0; candidate < RoomCapacity; candidate++)
                if (DeskArrivalReady(candidate))
                    return candidate;
            return -1;
        }

        public int StayStart(int room) => checkInMinutes[room];
        public int StayRate(int room) => stayRates[room];
        public int DepartureMinute(int room) => checkoutMinutes[room];

        public int CheckoutWaiting()
        {
            for (int room = 0; room < checkoutMinutes.Length; room++)
                if ((ActiveRooms & (1 << room)) != 0 && ClockMinute >= checkoutMinutes[room])
                    return room;
            return -1;
        }

        public bool TryDeskCheckout(int room, float departure, float guestId)
        {
            if (!RoomBuilt(room) || (ActiveRooms & (1 << room)) == 0 ||
                ClockMinute < DepartureMinute(room) || DepartureMinute(room) != departure || GuestId(room) != guestId)
                return false;
            if (OutstandingIssue(room) == GuestIssue.LateDeparture)
                ResolveGuestIssue(room, GuestIssue.LateDeparture);
            else
                CompleteCheckout(room, true);
            return true;
        }

        public void CompleteCheckout(int room, bool assisted)
        {
            int bit = 1 << room;
            if ((ActiveRooms & bit) == 0)
                return;
            completedStays++;
            RoomServiceCheckout(room);
            RecordReview(room, assisted);
            SettleGuest(room);
            CancelRequest(room);
            ActiveRooms &= (ushort)~bit;
            ActiveReservations &= (ushort)~bit;
            MarkRoomDirty(room);
            OpenDoors &= (ushort)~bit;
            Reviews |= (ushort)bit;
            if (!assisted)
                MissedCheckouts |= (ushort)bit;
            if (Bag != LuggageStatus.None && BagRoom == room)
            {
                Bag = LuggageStatus.None;
                Carrier = 0;
            }
        }

        public int NextArrival()
        {
            int next = -1;
            int earliest = int.MaxValue;
            for (int room = 0; room < RoomCapacity; room++)
            {
                if (!ArrivalReady(room))
                    continue;
                int when = BookingStart(room);
                if (when < earliest)
                {
                    next = room;
                    earliest = when;
                }
            }
            return next;
        }

        public bool BookingDue(int room)
        {
            int index = NextBooking(room);
            return index >= 0 && !ReservationPending(index) &&
                ReservationStart(index) <= ClockMinute && ClockMinute < ReservationEnd(index);
        }

        private int NextMorningDeparture => (ClockMinute / MinutesPerDay + 1) * MinutesPerDay + 11 * 60;

        public bool RoomReadyForGuest(int room) =>
            RoomBuilt(room) && ((ActiveRooms | UnlockedDoors) & (1 << room)) == 0 &&
            !RoomDirty(room) && !RoomUnusable(room) && !ForgottenItem(room);

        public bool ArrivalReady(int room) => RoomReadyForGuest(room) && BookingDue(room);

        public int WaitingRequest(bool phone)
        {
            for (int room = 0; room < requestMinutes.Length; room++)
            {
                int bit = 1 << room;
                if ((ActiveRooms & bit) != 0 && (KnownRequests & bit) == 0 &&
                    ((room & 1) == 0) == phone && ClockMinute >= requestMinutes[room])
                    return room;
            }
            return -1;
        }

        public bool RequestDue(int room) =>
            (uint)room < RoomCapacity && (ActiveRooms & ~FulfilledRequests & (1 << room)) != 0 &&
            ClockMinute >= requestMinutes[room];

        public string NotebookRequest(int room)
        {
            if (!RequestDue(room))
                return null;
            string status = !RequestSuppliesAvailable(room) ? " · Waiting for supplies"
                : RequestUrgent(room) ? " · Urgent" : "";
            return $"Room {room + 1} · {RequestLabel(room)} · {RequestWait(room)}m{status}";
        }

        public bool RequestOpen(int room) =>
            (ActiveRooms & KnownRequests & ~FulfilledRequests & (1 << room)) != 0;

        public void AcknowledgeRequest(int room) => KnownRequests |= (ushort)(1 << room);

        public bool RequestSuppliesAvailable(int room) => KindOfRequest(room) switch
        {
            GuestRequestKind.Towel => Towels > 0,
            GuestRequestKind.Pillow => Pillows > 0,
            GuestRequestKind.Toiletries => Toiletries > 0,
            _ => true
        };

        public void ServeRequest(int room) => TryServeRequest(room, KindOfRequest(room), 1);

        public bool TryServeRequest(int room, GuestRequestKind item, int quantity)
        {
            if ((uint)room >= RoomCapacity || !RequestOpen(room) || item != KindOfRequest(room) || quantity != 1)
                return false;
            switch (KindOfRequest(room))
            {
                case GuestRequestKind.Towel:
                    if (Towels <= 0) return false;
                    Towels--;
                    break;
                case GuestRequestKind.Pillow:
                    if (Pillows <= 0) return false;
                    Pillows--;
                    break;
                case GuestRequestKind.Toiletries:
                    if (Toiletries <= 0) return false;
                    Toiletries--;
                    break;
            }
            FulfilledRequests |= (ushort)(1 << room);
            satisfiedRequests |= (ushort)(1 << room);
            Post(Account.ServiceRevenue, 5);
            return true;
        }

        public void HireBellhop()
        {
            if (BellhopHired || Cash < BellhopCost)
                return;
            Post(Account.Payroll, -BellhopCost);
            BellhopHired = true;
        }

        public void SellUpgrade(int room, bool meal)
        {
            int bit = 1 << room;
            if ((ActiveRooms & bit) == 0 || (!meal && IsBroken(room, Fixture.Television)) ||
                ((meal ? MealOrders : TvPackages) & bit) != 0)
                return;
            if (meal)
            {
                QueueRoomMeal(room);
                return;
            }
            TvPackages |= (ushort)bit;
            Post(Account.ServiceRevenue, 12);
        }

        public bool CheckIn(int room, int arrivedAt, int deskMinute) => CheckInGuest(room, arrivedAt, deskMinute, false);

        public bool CheckInWithStaff(int room, int arrivedAt, int deskMinute) => CheckInGuest(room, arrivedAt, deskMinute, true);

        private bool CheckInGuest(int room, int arrivedAt, int deskMinute, bool allowSelfLuggage)
        {
            int bit = 1 << room;
            if ((!allowSelfLuggage && Bag != LuggageStatus.None) || !ArrivalReady(room))
                return false;
            int booking = NextBooking(room);
            int departure = ReservationEnd(booking);
            int priceStarts = ReservationStart(booking);
            int rate = ReservationRate(booking);
            BookingPreference preference = ReservationPreference(booking);
            stayBookingDetails.Length = RoomCount;
            stayBookingDetails[room] = (byte)(((int)preference << 6) |
                ((ReservationGuestIndex(booking) + 1) << 2) | (int)ReservationChannel(booking));
            InitializeGuestProfile(room, preference, rate);
            ActiveReservations |= (ushort)bit;
            RemoveBooking(booking);
            VisitedRooms |= (ushort)bit;
            UnlockedDoors |= (ushort)bit;
            ActiveRooms |= (ushort)bit;
            Reviews &= (ushort)~bit;
            LateCheckIns &= (ushort)~bit;
            MissedCheckouts &= (ushort)~bit;
            DeliveredBags &= (ushort)~bit;
            KnownRequests &= (ushort)~bit;
            FulfilledRequests &= (ushort)~bit;
            satisfiedRequests &= (ushort)~bit;
            TvPackages &= (ushort)~bit;
            MealOrders &= (ushort)~bit;
            requestMinutes[room] = ClockMinute + (preference == BookingPreference.None ? 75 : 10);
            if (deskMinute - arrivedAt > 90)
                LateCheckIns |= (ushort)bit;
            checkoutMinutes[room] = departure;
            OpenDoors |= (ushort)bit;
            if (Bag == LuggageStatus.None)
            {
                BagRoom = (byte)room;
                Bag = LuggageStatus.InCar;
            }
            Post(Account.RoomRevenue, rate * StayNights(priceStarts, departure));
            Post(Account.Financing, 20);
            return true;
        }

        public bool PickUp(ulong player)
        {
            if (Bag != LuggageStatus.InCar || (Delivery == SupplyDelivery.Carried && SupplyCarrier == player))
                return false;
            Carrier = player;
            Bag = LuggageStatus.Carried;
            return true;
        }

        public bool Deliver(ulong player)
        {
            if (Bag != LuggageStatus.Carried || Carrier != player)
                return false;
            DeliveredBags |= (ushort)(1 << BagRoom);
            Post(Account.ServiceRevenue, LuggageTip);
            Bag = LuggageStatus.None;
            Carrier = 0;
            return true;
        }

        public void RecoverBag(ulong player)
        {
            if (Bag == LuggageStatus.Carried && Carrier == player)
            {
                Bag = LuggageStatus.InCar;
                Carrier = 0;
            }
        }

        public int Stock(int index) => index switch
        {
            0 => CleaningKits,
            1 => Towels,
            2 => LinenSets,
            3 => TrashBags,
            4 => Vacuums,
            5 => Detergent,
            6 => Pillows,
            7 => Toiletries,
            _ => 0
        };

        public const int MaxCartQuantity = 9;
        private int deliveryCart;
        private int activeOrderNumber;
        private int orderSequence;
        private FixedList128Bytes<int> queuedCarts;
        private FixedList128Bytes<int> queuedDue;
        private FixedList128Bytes<int> completedOrders;
        private FixedList128Bytes<int> completedCarts;
        public int PendingOrderCount => queuedCarts.Length + (Delivery == SupplyDelivery.None ? 0 : 1);
        public int CompletedOrderCount => completedOrders.Length;
        public int ActiveOrderNumber => activeOrderNumber;
        public int CompletedOrder(int index) => completedOrders[index];
        public int CompletedCart(int index) => completedCarts[index];
        public int ReadyOrderCount
        {
            get
            {
                int count = Delivery == SupplyDelivery.AtBay ? 1 : 0;
                foreach (int due in queuedDue)
                    if (due <= ClockMinute)
                        count++;
                return count;
            }
        }
        public int QueuedOrderDue(int index) => queuedDue[index];
        public int QueuedOrderNumber(int index) => activeOrderNumber + index + 1;

        public static int CartQuantity(int cart, int item) => (cart >> (4 * item)) & 15;

        public static int CartCost(int cart)
        {
            int total = 0;
            for (int item = 0; item < Catalog.Length; item++)
                total += CartQuantity(cart, item) * Catalog[item].Price;
            return total;
        }

        public bool Order(int index) => index >= 0 && index < Catalog.Length && OrderCart(1 << (4 * index));

        public bool OrderCart(int cart) => OrderCartPriced(cart, CartCost(cart), 72);

        private bool OrderCartPriced(int cart, int price, int minutes)
        {
            if (cart == 0 || queuedCarts.Length >= 8 || price > Cash)
                return false;
            for (int item = 0; item < Catalog.Length; item++)
                if (CartQuantity(cart, item) > MaxCartQuantity)
                    return false;
            if (CartQuantity(cart, 4) > 0)
            {
                if (CartQuantity(cart, 4) > 1 || Vacuums > 0 || CartQuantity(deliveryCart, 4) > 0)
                    return false;
                foreach (int queued in queuedCarts)
                    if (CartQuantity(queued, 4) > 0)
                        return false;
            }

            Post(Account.Supplies, -price);
            orderSequence++;
            queuedCarts.Add(cart);
            queuedDue.Add(ClockMinute + minutes);
            LoadNextDelivery();
            return true;
        }

        private void LoadNextDelivery()
        {
            if (Delivery != SupplyDelivery.None || queuedCarts.Length == 0)
                return;
            activeOrderNumber = orderSequence - queuedCarts.Length + 1;
            deliveryCart = queuedCarts[0];
            DeliveryDue = queuedDue[0];
            queuedCarts.RemoveAt(0);
            queuedDue.RemoveAt(0);
            Delivery = SupplyDelivery.InTransit;
            AdvanceDelivery();
        }

        public void AdvanceDelivery()
        {
            if (Delivery == SupplyDelivery.InTransit && ClockMinute >= DeliveryDue)
            {
                Delivery = SupplyDelivery.AtBay;
                ClockSpeed = 1;
            }
        }

        public void CollectSupply(ulong worker)
        {
            if (Delivery != SupplyDelivery.AtBay || (Bag == LuggageStatus.Carried && Carrier == worker))
                return;
            SupplyCarrier = worker;
            Delivery = SupplyDelivery.Carried;
        }

        public void StoreSupply(ulong worker)
        {
            if (Delivery != SupplyDelivery.Carried || SupplyCarrier != worker)
                return;
            CleaningKits += CartQuantity(deliveryCart, 0) * Catalog[0].Quantity;
            Towels += CartQuantity(deliveryCart, 1) * Catalog[1].Quantity;
            LinenSets += CartQuantity(deliveryCart, 2) * Catalog[2].Quantity;
            TrashBags += CartQuantity(deliveryCart, 3) * Catalog[3].Quantity;
            Vacuums += CartQuantity(deliveryCart, 4) * Catalog[4].Quantity;
            Detergent += CartQuantity(deliveryCart, 5) * Catalog[5].Quantity;
            Pillows += CartQuantity(deliveryCart, 6) * Catalog[6].Quantity;
            Toiletries += CartQuantity(deliveryCart, 7) * Catalog[7].Quantity;

            if (completedOrders.Length == 12)
            {
                completedOrders.RemoveAt(0);
                completedCarts.RemoveAt(0);
            }
            completedOrders.Add(activeOrderNumber);
            completedCarts.Add(deliveryCart);
            Delivery = SupplyDelivery.None;
            deliveryCart = 0;
            SupplyCarrier = 0;
            DeliveryDue = 0;
            LoadNextDelivery();
        }

        public void RecoverSupply(ulong worker)
        {
            if (Delivery != SupplyDelivery.Carried || SupplyCarrier != worker)
                return;
            SupplyCarrier = 0;
            Delivery = SupplyDelivery.AtBay;
        }

        private void SerializeReservations<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            byte count = (byte)reservations.Length;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader)
                reservations.Length = count;
            for (int i = 0; i < count; i++)
            {
                int value = reservations[i];
                serializer.SerializeValue(ref value);
                if (serializer.IsReader)
                    reservations[i] = value;
            }
        }

        private static void SerializeOrderList<T>(BufferSerializer<T> serializer, ref FixedList128Bytes<int> list)
            where T : IReaderWriter
        {
            byte count = (byte)list.Length;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader)
                list.Length = count;
            for (int i = 0; i < count; i++)
            {
                int value = list[i];
                serializer.SerializeValue(ref value);
                if (serializer.IsReader)
                    list[i] = value;
            }
        }

        public bool DoorUnlocked(int room) => (UnlockedDoors & (1 << room)) != 0;

        public void ToggleDoor(int room)
        {
            int bit = 1 << room;
            if ((DoorUnlocked(room) && !IsBroken(room, Fixture.DoorLock)) || (OpenDoors & bit) != 0)
                OpenDoors ^= (ushort)bit;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref property);
            SerializeState(serializer);
            SerializeDecisions(serializer);
            SerializePhone(serializer);
            SerializeBookingDetails(serializer);
        }

        private void SerializeState<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Cash);
            serializer.SerializeValue(ref ClockMinute);
            serializer.SerializeValue(ref ClockSpeed);
            serializer.SerializeValue(ref ActiveRooms);
            serializer.SerializeValue(ref Reviews);
            serializer.SerializeValue(ref LateCheckIns);
            serializer.SerializeValue(ref MissedCheckouts);
            serializer.SerializeValue(ref BellhopHired);
            serializer.SerializeValue(ref KnownRequests);
            serializer.SerializeValue(ref FulfilledRequests);
            serializer.SerializeValue(ref TvPackages);
            serializer.SerializeValue(ref MealOrders);
            if (serializer.IsReader)
            {
                checkoutMinutes.Length = RoomCount;
                requestMinutes.Length = RoomCount;
            }
            for (int room = 0; room < RoomCount; room++)
            {
                int minute = room < checkoutMinutes.Length ? checkoutMinutes[room] : 0;
                serializer.SerializeValue(ref minute);
                int request = room < requestMinutes.Length ? requestMinutes[room] : 0;
                serializer.SerializeValue(ref request);
                if (serializer.IsReader)
                {
                    checkoutMinutes[room] = minute;
                    requestMinutes[room] = request;
                }
            }
            serializer.SerializeValue(ref CleaningKits);
            serializer.SerializeValue(ref Towels);
            serializer.SerializeValue(ref LinenSets);
            serializer.SerializeValue(ref UnlockedDoors);
            serializer.SerializeValue(ref OpenDoors);
            serializer.SerializeValue(ref DeliveredBags);
            serializer.SerializeValue(ref BagRoom);
            serializer.SerializeValue(ref Carrier);
            serializer.SerializeValue(ref Bag);
            serializer.SerializeValue(ref Delivery);
            serializer.SerializeValue(ref deliveryCart);
            serializer.SerializeValue(ref activeOrderNumber);
            serializer.SerializeValue(ref orderSequence);
            SerializeOrderList(serializer, ref queuedCarts);
            SerializeOrderList(serializer, ref queuedDue);
            SerializeOrderList(serializer, ref completedOrders);
            SerializeOrderList(serializer, ref completedCarts);
            serializer.SerializeValue(ref DeliveryDue);
            serializer.SerializeValue(ref SupplyCarrier);
            SerializeReservations(serializer);
            SerializeOperations(serializer);
            SerializePropertyCare(serializer);
            SerializeGuestLife(serializer);
            SerializeExpansion(serializer);
            SerializeManagement(serializer);
            SerializeHousekeeping(serializer);
        }

        public bool Equals(MotelService other) =>
            property == other.property && Cash == other.Cash && ClockMinute == other.ClockMinute && ClockSpeed == other.ClockSpeed && ActiveRooms == other.ActiveRooms &&
            Reviews == other.Reviews && LateCheckIns == other.LateCheckIns && MissedCheckouts == other.MissedCheckouts &&
            BellhopHired == other.BellhopHired && KnownRequests == other.KnownRequests &&
            FulfilledRequests == other.FulfilledRequests && TvPackages == other.TvPackages && MealOrders == other.MealOrders &&
            requestMinutes.Equals(other.requestMinutes) && checkoutMinutes.Equals(other.checkoutMinutes) && reservations.Equals(other.reservations) && CleaningKits == other.CleaningKits && Towels == other.Towels &&
            LinenSets == other.LinenSets && UnlockedDoors == other.UnlockedDoors && OpenDoors == other.OpenDoors &&
            DeliveredBags == other.DeliveredBags && BagRoom == other.BagRoom &&
            Carrier == other.Carrier && Bag == other.Bag && Delivery == other.Delivery &&
            deliveryCart == other.deliveryCart && activeOrderNumber == other.activeOrderNumber && orderSequence == other.orderSequence &&
            queuedCarts.Equals(other.queuedCarts) && queuedDue.Equals(other.queuedDue) && completedOrders.Equals(other.completedOrders) && completedCarts.Equals(other.completedCarts) && DeliveryDue.Equals(other.DeliveryDue) && SupplyCarrier == other.SupplyCarrier && HousekeepingEquals(other) && OperationsEqual(other) && PropertyCareEquals(other) && GuestLifeEquals(other) && ExpansionEquals(other) && ManagementEquals(other) && DecisionsEqual(other) && PhoneEquals(other) &&
            stayBookingDetails.Equals(other.stayBookingDetails) && confirmedOnlineBookings == other.confirmedOnlineBookings;
    }
}
