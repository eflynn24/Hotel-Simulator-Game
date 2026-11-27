using System;
using Unity.Netcode;
using Unity.Collections;

namespace DesertLantern
{
    internal enum SupplyDelivery : byte { None, InTransit, AtBay, Carried }

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

    internal struct MotelService : INetworkSerializable, IEquatable<MotelService>
    {
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
            new SupplyOffer("Linen sets", 26, 2)
        };
        public int Cash;
        public int CleaningKits;
        public int Towels;
        public int LinenSets;
        public SupplyDelivery Delivery;
        public byte OrderedOffer;
        public int DeliveryDue;
        public int ClockMinute;
        public byte ClockSpeed;
        public byte ActiveRooms;
        public byte Reviews;
        public byte LateCheckIns;
        public byte MissedCheckouts;
        private FixedList32Bytes<int> checkoutMinutes;
        private FixedList32Bytes<int> requestMinutes;
        public bool BellhopHired;
        public byte KnownRequests;
        public byte FulfilledRequests;
        public byte TvPackages;
        public byte MealOrders;
        private int eliStart, eliEnd, juneStart, juneEnd;
        public ulong SupplyCarrier;
        public byte UnlockedDoors;
        public byte OpenDoors;
        public byte DeliveredBags;
        public byte BagRoom;
        private byte confirmedReservations;
        private byte cancelledReservations;
        public ulong Carrier;
        public LuggageStatus Bag;

        public static MotelService New()
        {
            var service = new MotelService
            {
                Cash = StartingCash, ClockMinute = OpeningMinute, ClockSpeed = 1,
                eliStart = 14 * 60, juneStart = 16 * 60,
                eliEnd = MinutesPerDay + 11 * 60, juneEnd = MinutesPerDay + 12 * 60
            };
            for (int room = 0; room < 4; room++)
            {
                service.checkoutMinutes.Add(0);
                service.requestMinutes.Add(0);
            }
            return service;
        }

        public static string DisplayTime(int minute) =>
            $"Day {minute / MinutesPerDay + 1}  {minute / 60 % 24:00}:{minute % 60:00}";

        public int BookingStart(int room) => room == 1 ? eliStart : juneStart;
        public int BookingEnd(int room) => room == 1 ? eliEnd : juneEnd;

        public void AdjustSchedule(int room, bool end, int delta)
        {
            if (room != 1 && room != 2 || (UnlockedDoors & (1 << room)) != 0 || ReservationCancelled(room))
                return;
            int start = BookingStart(room);
            int finish = BookingEnd(room);
            if (end)
                finish += delta;
            else
                start += delta;

            if (start < ClockMinute || start >= finish || finish > ClockMinute + 7 * MinutesPerDay)
                return;
            if (room == 1)
            {
                eliStart = start;
                eliEnd = finish;
            }
            else
            {
                juneStart = start;
                juneEnd = finish;
            }
        }

        public int ActiveRoomCount
        {
            get
            {
                int count = 0;
                for (int room = 0; room < 4; room++)
                    if ((ActiveRooms & (1 << room)) != 0)
                        count++;
                return count;
            }
        }

        public void AdvanceClock(int now)
        {
            int previous = ClockMinute;
            ClockMinute = now;
            for (int room = 1; room <= 2; room++)
                if (HasReservation(room) && previous < BookingStart(room) && BookingStart(room) <= now)
                    ClockSpeed = 1;
            for (int room = 0; room < checkoutMinutes.Length; room++)
            {
                if ((ActiveRooms & (1 << room)) == 0)
                    continue;
                int departure = checkoutMinutes[room];
                if (previous < departure && departure <= now ||
                    previous < requestMinutes[room] && requestMinutes[room] <= now)
                    ClockSpeed = 1;
                if (now >= departure + 60)
                    CompleteCheckout(room, false);
            }
        }

        public int CheckoutWaiting()
        {
            for (int room = 0; room < checkoutMinutes.Length; room++)
                if ((ActiveRooms & (1 << room)) != 0 && ClockMinute >= checkoutMinutes[room])
                    return room;
            return -1;
        }

        public void CompleteCheckout(int room, bool assisted)
        {
            int bit = 1 << room;
            ActiveRooms &= (byte)~bit;
            OpenDoors &= (byte)~bit;
            Reviews |= (byte)bit;
            if (!assisted)
                MissedCheckouts |= (byte)bit;
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
            bool selectedReservation = false;
            for (int room = 0; room < 4; room++)
            {
                if (!ArrivalReady(room))
                    continue;
                bool reserved = HasReservation(room);
                int when = reserved ? BookingStart(room) : ClockMinute;
                if (when < earliest || (when == earliest && reserved && !selectedReservation))
                {
                    next = room;
                    earliest = when;
                    selectedReservation = reserved;
                }
            }
            return next;
        }

        public bool ArrivalReady(int room) =>
            (UnlockedDoors & (1 << room)) == 0 &&
            ((room != 1 && room != 2) || ReservationCancelled(room) || ClockMinute >= BookingStart(room));

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

        public bool RequestOpen(int room) =>
            (ActiveRooms & KnownRequests & ~FulfilledRequests & (1 << room)) != 0;

        public void AcknowledgeRequest(int room) => KnownRequests |= (byte)(1 << room);

        public void GiveTowel(int room)
        {
            if (!RequestOpen(room) || Towels <= 0)
                return;
            Towels--;
            FulfilledRequests |= (byte)(1 << room);
            Cash += 5;
        }

        public void HireBellhop()
        {
            if (BellhopHired || Cash < BellhopCost)
                return;
            Cash -= BellhopCost;
            BellhopHired = true;
        }

        public void SellUpgrade(int room, bool meal)
        {
            int bit = 1 << room;
            if ((ActiveRooms & bit) == 0 || ((meal ? MealOrders : TvPackages) & bit) != 0)
                return;
            if (meal)
                MealOrders |= (byte)bit;
            else
                TvPackages |= (byte)bit;
            Cash += meal ? 18 : 12;
        }

        public void ExpireBooking(int room)
        {
            if ((UnlockedDoors & (1 << room)) == 0 && !ReservationCancelled(room) && ClockMinute >= BookingEnd(room))
                UpdateReservation(room, false);
        }

        public bool CheckIn(int room, int arrivedAt, int deskMinute)
        {
            int bit = 1 << room;
            if (Bag != LuggageStatus.None || (UnlockedDoors & bit) != 0)
                return false;
            UnlockedDoors |= (byte)bit;
            ActiveRooms |= (byte)bit;
            requestMinutes[room] = ClockMinute + 75;
            if (deskMinute - arrivedAt > 90)
                LateCheckIns |= (byte)bit;
            checkoutMinutes[room] = HasReservation(room) ? BookingEnd(room)
                : (ClockMinute / MinutesPerDay + 1) * MinutesPerDay + 11 * 60;
            OpenDoors |= (byte)bit;
            BagRoom = (byte)room;
            Bag = LuggageStatus.InCar;
            Cash += RoomRate;
            return true;
        }

        public bool HasReservation(int room) => (confirmedReservations & (1 << room)) != 0;

        public bool ReservationCancelled(int room) => (cancelledReservations & (1 << room)) != 0;

        public void UpdateReservation(int room, bool confirm)
        {
            int bit = 1 << room;
            if ((UnlockedDoors & bit) != 0 || ReservationCancelled(room))
                return;
            if (confirm)
                confirmedReservations |= (byte)bit;
            else
            {
                confirmedReservations &= (byte)~bit;
                cancelledReservations |= (byte)bit;
            }
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
            DeliveredBags |= (byte)(1 << BagRoom);
            Cash += LuggageTip;
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

        public int Stock(int index) => index == 0 ? CleaningKits : index == 1 ? Towels : LinenSets;

        public bool Order(int index)
        {
            if (Delivery != SupplyDelivery.None || index < 0 || index >= Catalog.Length)
                return false;
            SupplyOffer offer = Catalog[index];
            if (Cash < offer.Price)
                return false;

            Cash -= offer.Price;
            OrderedOffer = (byte)index;
            Delivery = SupplyDelivery.InTransit;
            DeliveryDue = ClockMinute + 72;
            return true;
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
            SupplyOffer offer = Catalog[OrderedOffer];
            switch (OrderedOffer)
            {
                case 0: CleaningKits += offer.Quantity; break;
                case 1: Towels += offer.Quantity; break;
                case 2: LinenSets += offer.Quantity; break;
            }

            Delivery = SupplyDelivery.None;
            OrderedOffer = 0;
            SupplyCarrier = 0;
            DeliveryDue = 0;
        }

        public void RecoverSupply(ulong worker)
        {
            if (Delivery != SupplyDelivery.Carried || SupplyCarrier != worker)
                return;
            SupplyCarrier = 0;
            Delivery = SupplyDelivery.AtBay;
        }

        public void ToggleDoor(int room) => OpenDoors ^= (byte)(1 << room);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
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
                checkoutMinutes.Length = 4;
                requestMinutes.Length = 4;
            }
            for (int room = 0; room < 4; room++)
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
            serializer.SerializeValue(ref eliStart);
            serializer.SerializeValue(ref eliEnd);
            serializer.SerializeValue(ref juneStart);
            serializer.SerializeValue(ref juneEnd);
            serializer.SerializeValue(ref CleaningKits);
            serializer.SerializeValue(ref Towels);
            serializer.SerializeValue(ref LinenSets);
            serializer.SerializeValue(ref UnlockedDoors);
            serializer.SerializeValue(ref OpenDoors);
            serializer.SerializeValue(ref DeliveredBags);
            serializer.SerializeValue(ref BagRoom);
            serializer.SerializeValue(ref confirmedReservations);
            serializer.SerializeValue(ref cancelledReservations);
            serializer.SerializeValue(ref Carrier);
            serializer.SerializeValue(ref Bag);
            serializer.SerializeValue(ref Delivery);
            serializer.SerializeValue(ref OrderedOffer);
            serializer.SerializeValue(ref DeliveryDue);
            serializer.SerializeValue(ref SupplyCarrier);
        }

        public bool Equals(MotelService other) =>
            Cash == other.Cash && ClockMinute == other.ClockMinute && ClockSpeed == other.ClockSpeed && ActiveRooms == other.ActiveRooms &&
            Reviews == other.Reviews && LateCheckIns == other.LateCheckIns && MissedCheckouts == other.MissedCheckouts &&
            BellhopHired == other.BellhopHired && KnownRequests == other.KnownRequests &&
            FulfilledRequests == other.FulfilledRequests && TvPackages == other.TvPackages && MealOrders == other.MealOrders &&
            requestMinutes.Equals(other.requestMinutes) && checkoutMinutes.Equals(other.checkoutMinutes) && eliStart == other.eliStart && eliEnd == other.eliEnd && juneStart == other.juneStart && juneEnd == other.juneEnd && CleaningKits == other.CleaningKits && Towels == other.Towels &&
            LinenSets == other.LinenSets && UnlockedDoors == other.UnlockedDoors && OpenDoors == other.OpenDoors &&
            DeliveredBags == other.DeliveredBags && BagRoom == other.BagRoom &&
            confirmedReservations == other.confirmedReservations && cancelledReservations == other.cancelledReservations &&
            Carrier == other.Carrier && Bag == other.Bag && Delivery == other.Delivery &&
            OrderedOffer == other.OrderedOffer && DeliveryDue.Equals(other.DeliveryDue) && SupplyCarrier == other.SupplyCarrier;
    }
}
