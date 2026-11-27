using System;
using Unity.Netcode;

namespace DesertLantern
{
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
        public byte UnlockedDoors;
        public byte OpenDoors;
        public byte DeliveredBags;
        public byte BagRoom;
        private byte confirmedReservations;
        private byte cancelledReservations;
        public ulong Carrier;
        public LuggageStatus Bag;

        public static MotelService New() => new MotelService { Cash = StartingCash };

        public bool CheckIn(int room)
        {
            int bit = 1 << room;
            if (Bag != LuggageStatus.None || (UnlockedDoors & bit) != 0)
                return false;
            UnlockedDoors |= (byte)bit;
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
            if (Bag != LuggageStatus.InCar)
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

        public bool Buy(int index)
        {
            if (index < 0 || index >= Catalog.Length)
                return false;
            SupplyOffer offer = Catalog[index];
            if (Cash < offer.Price)
                return false;

            Cash -= offer.Price;
            switch (index)
            {
                case 0: CleaningKits += offer.Quantity; break;
                case 1: Towels += offer.Quantity; break;
                case 2: LinenSets += offer.Quantity; break;
            }

            return true;
        }

        public void ToggleDoor(int room) => OpenDoors ^= (byte)(1 << room);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Cash);
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
        }

        public bool Equals(MotelService other) =>
            Cash == other.Cash && CleaningKits == other.CleaningKits && Towels == other.Towels &&
            LinenSets == other.LinenSets && UnlockedDoors == other.UnlockedDoors && OpenDoors == other.OpenDoors &&
            DeliveredBags == other.DeliveredBags && BagRoom == other.BagRoom &&
            confirmedReservations == other.confirmedReservations && cancelledReservations == other.cancelledReservations &&
            Carrier == other.Carrier && Bag == other.Bag;
    }
}
