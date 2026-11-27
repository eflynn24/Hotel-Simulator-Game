using System;
using Unity.Collections;
using Unity.Netcode;

namespace DesertLantern
{
    internal enum GuestPersonality { Practical, ComfortSeeker, Business, LightSleeper }
    internal enum PropertyIncident { None, Pests, SuspiciousActivity, UtilityEmergency }
    internal enum ManagementAction { AdjustRate, Promotion, Advertise, PlaceFurniture, RemoveFurniture, Investigate, PurchaseLand }
    internal static class EquipmentCondition
    {
        public const int Full = 100;
        public static int AfterUse(int condition, int wear) => Math.Max(0, condition - wear);
        public static int AfterRepair(int condition, int repair) => Math.Min(Full, condition + repair);
    }

    internal readonly struct FurnitureOffer
    {
        public readonly string RoomName;
        public readonly string ReceptionName;
        public readonly int Price;
        public readonly int Quality;

        public FurnitureOffer(string roomName, string receptionName, int price, int quality)
        {
            RoomName = roomName;
            ReceptionName = receptionName;
            Price = price;
            Quality = quality;
        }
    }

    internal partial struct MotelService
    {
        public static readonly FurnitureOffer[] FurnitureCatalog =
        {
            new FurnitureOffer("Premium bedding", "Upholstered lobby bench", 34, 14),
            new FurnitureOffer("Reading lamp", "Reception artwork", 29, 9),
            new FurnitureOffer("Guest chair", "Reception display cabinet", 42, 12)
        };
        public const int ReceptionFurniture = RoomCount;
        public const int MarketingCost = 40;
        public const int AnnexLandCost = 250;
        private FixedList32Bytes<byte> furnishings;
        private FixedList64Bytes<int> checkInMinutes;
        private FixedList32Bytes<byte> stayQuality;
        private FixedList32Bytes<byte> stayRates;
        private int legacyWalkInMinute; // Retained in saved state for compatibility.
        private ushort quietedRooms;
        private int reputation;
        private int listedRate;
        private int advertisingUntil;
        private bool promotion;
        private bool annexLand;
        private ushort noiseComplaints;
        private PropertyIncident incident;
        private byte incidentRoom;
        private int incidentDue;
        private int nextIncidentDay;

        public int Reputation => reputation;
        public int ListedRate => listedRate;
        public bool Promotion => promotion;
        public int AdvertisingUntil => advertisingUntil;
        public bool OwnsAnnexLand => annexLand;
        public PropertyIncident Incident => incident;
        public int IncidentRoom => incidentRoom;
        public int IncidentDue => incidentDue;
        public int Demand => Math.Clamp(reputation + (advertisingUntil > ClockMinute ? 18 : 0) +
            (promotion ? 10 : 0) + (RoomRate - listedRate) / 2 + ReceptionQuality / 8, 10, 100);

        private void InitializeManagement()
        {
            reputation = 50;
            listedRate = RoomRate;
            nextIncidentDay = 3;
            for (int room = 0; room <= RoomCount; room++)
                furnishings.Add(0);
            for (int room = 0; room < RoomCount; room++)
            {
                checkInMinutes.Add(0);
                stayQuality.Add(0);
                stayRates.Add(0);
            }
        }

        public bool HasFurniture(int location, int item) => (furnishings[location] & (1 << item)) != 0;

        private int FurnitureQuality(int location)
        {
            int quality = 0;
            for (int item = 0; item < FurnitureCatalog.Length; item++)
                if (HasFurniture(location, item))
                    quality += FurnitureCatalog[item].Quality;
            return quality;
        }

        public int ReceptionQuality => 45 + FurnitureQuality(ReceptionFurniture);

        public int RoomQuality(int room) => Math.Clamp(48 + FurnitureQuality(room) -
            (100 - Cleanliness(room)) / 3 - 8 * MaterialIssues(room), 0, 100);

        public int NightlyRate(int room) => Math.Max(30, listedRate + FurnitureQuality(room) / 2 - (promotion ? 10 : 0));

        public GuestPersonality Personality(int room) => PersonalityFor(room, checkInMinutes[room]);

        private static GuestPersonality PersonalityFor(int room, int minute) =>
            (GuestPersonality)((minute / 19 + 3 * room) % 4);

        public int Expectations(int room) => Personality(room) switch
        {
            GuestPersonality.ComfortSeeker => 66,
            GuestPersonality.Business => 60,
            GuestPersonality.LightSleeper => 53,
            _ => 42
        };

        private int Patience(int room) => Personality(room) switch
        {
            GuestPersonality.Practical => 135,
            GuestPersonality.ComfortSeeker => 94,
            GuestPersonality.Business => 72,
            _ => 100
        };

        private void InitializeGuestProfile(int room, BookingPreference preference, int rate)
        {
            checkInMinutes[room] = ClockMinute;
            stayQuality[room] = (byte)RoomQuality(room);
            stayRates[room] = (byte)rate;
            noiseComplaints &= (ushort)~(1 << room);
            quietedRooms &= (ushort)~(1 << room);
            requestKinds[room] = (byte)(preference == BookingPreference.None
                ? Personality(room) switch
                {
                    GuestPersonality.ComfortSeeker => GuestRequestKind.Pillow,
                    GuestPersonality.Business => GuestRequestKind.Toiletries,
                    GuestPersonality.LightSleeper => GuestRequestKind.Complaint,
                    _ => GuestRequestKind.Towel
                }
                : (GuestRequestKind)((int)preference - 1));
        }

        public bool NoiseComplaint(int room) => (noiseComplaints & (1 << room)) != 0;
        public bool OpenNoiseComplaint(int room) => ((noiseComplaints & ~quietedRooms) & (1 << room)) != 0;

        private void AdvanceManagement(int previous)
        {
            for (int room = 0; room < RoomCapacity; room++)
            {
                if ((ActiveRooms & (1 << room)) == 0 || Personality(room) != GuestPersonality.LightSleeper)
                    continue;
                int quietHours = ClockMinute % MinutesPerDay;
                int neighbor = room % 2 == 0 ? room + 1 : room - 1;
                if (quietHours >= 20 * 60 && neighbor < RoomCapacity && (ActiveRooms & (1 << neighbor)) != 0 &&
                    Activity(neighbor) == GuestActivity.HostingVisitor)
                    noiseComplaints |= (ushort)(1 << room);
            }

            int day = ClockMinute / MinutesPerDay;
            if (incident != PropertyIncident.None)
            {
                if (ClockMinute >= incidentDue)
                {
                    reputation = Math.Max(0, reputation - 5);
                    Post(Account.Maintenance, -(incident == PropertyIncident.UtilityEmergency ? 32 : 18));
                    incident = PropertyIncident.None;
                }
                return;
            }
            if (day < nextIncidentDay || previous / MinutesPerDay == day)
                return;

            incident = (PropertyIncident)(1 + day % 3);
            nextIncidentDay = day + 2 + day % 3;
            incidentRoom = (byte)(day % RoomCapacity);
            incidentDue = ClockMinute + 9 * 60;
            ClockSpeed = 1;
        }

        public bool PestRoom(int room) => incident == PropertyIncident.Pests && room == incidentRoom;
        public bool UtilityOutage => incident == PropertyIncident.UtilityEmergency;

        public bool ManageProperty(ManagementAction action, int location, int option)
        {
            switch (action)
            {
                case ManagementAction.AdjustRate:
                    if (option != 0 && option != 1)
                        return false;
                    listedRate = Math.Clamp(listedRate + (option == 0 ? -5 : 5), 40, 110);
                    return true;
                case ManagementAction.Promotion:
                    promotion = !promotion;
                    return true;
                case ManagementAction.Advertise:
                    if (advertisingUntil <= ClockMinute && PayExpansionCost(Account.Maintenance, MarketingCost))
                    {
                        advertisingUntil = ClockMinute + 3 * MinutesPerDay;
                        return true;
                    }
                    break;
                case ManagementAction.PlaceFurniture:
                case ManagementAction.RemoveFurniture:
                    if ((location == ReceptionFurniture || RoomBuilt(location) && (ActiveRooms & (1 << location)) == 0) &&
                        option >= 0 && option < FurnitureCatalog.Length)
                        return ManageFurniture(action, location, option);
                    break;
                case ManagementAction.Investigate:
                    if (incident == PropertyIncident.None)
                        return false;
                    int cost = incident == PropertyIncident.SuspiciousActivity ? 0
                        : incident == PropertyIncident.Pests ? 22 : 38;
                    if (PayExpansionCost(Account.Maintenance, cost))
                    {
                        incident = PropertyIncident.None;
                        return true;
                    }
                    break;
                case ManagementAction.PurchaseLand:
                    if (!annexLand && completedStays >= ExpansionMinimumStays &&
                        PayExpansionCost(Account.Maintenance, AnnexLandCost))
                    {
                        annexLand = true;
                        return true;
                    }
                    break;
            }
            return false;
        }

        private bool ManageFurniture(ManagementAction action, int location, int item)
        {
            bool placed = HasFurniture(location, item);
            byte bit = (byte)(1 << item);
            if (action == ManagementAction.RemoveFurniture && placed)
            {
                furnishings[location] &= (byte)~bit;
                Post(Account.Maintenance, FurnitureCatalog[item].Price / 4);
                return true;
            }
            if (action == ManagementAction.PlaceFurniture && !placed &&
                PayExpansionCost(Account.Maintenance, FurnitureCatalog[item].Price))
            {
                furnishings[location] |= bit;
                return true;
            }
            return false;
        }

        private void SerializeManagement<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref reputation);
            serializer.SerializeValue(ref legacyWalkInMinute);
            serializer.SerializeValue(ref quietedRooms);
            serializer.SerializeValue(ref listedRate);
            serializer.SerializeValue(ref advertisingUntil);
            serializer.SerializeValue(ref promotion);
            serializer.SerializeValue(ref annexLand);
            serializer.SerializeValue(ref noiseComplaints);
            serializer.SerializeValue(ref incident);
            serializer.SerializeValue(ref incidentRoom);
            serializer.SerializeValue(ref incidentDue);
            serializer.SerializeValue(ref nextIncidentDay);
            if (serializer.IsReader)
            {
                furnishings.Length = RoomCount + 1;
                checkInMinutes.Length = RoomCount;
                stayQuality.Length = RoomCount;
                stayRates.Length = RoomCount;
            }
            for (int room = 0; room <= RoomCount; room++)
            {
                byte furniture = furnishings.Length > room ? furnishings[room] : (byte)0;
                serializer.SerializeValue(ref furniture);
                if (serializer.IsReader)
                    furnishings[room] = furniture;
                if (room == RoomCount)
                    continue;
                int checkIn = checkInMinutes.Length > room ? checkInMinutes[room] : 0;
                byte quality = stayQuality.Length > room ? stayQuality[room] : (byte)0;
                serializer.SerializeValue(ref checkIn);
                serializer.SerializeValue(ref quality);
                byte rate = stayRates.Length > room ? stayRates[room] : (byte)0;
                serializer.SerializeValue(ref rate);
                if (serializer.IsReader)
                {
                    checkInMinutes[room] = checkIn;
                    stayQuality[room] = quality;
                    stayRates[room] = rate;
                }
            }
        }

        private bool ManagementEquals(MotelService other) =>
            legacyWalkInMinute == other.legacyWalkInMinute && quietedRooms == other.quietedRooms &&
            reputation == other.reputation && listedRate == other.listedRate && advertisingUntil == other.advertisingUntil &&
            promotion == other.promotion && annexLand == other.annexLand && noiseComplaints == other.noiseComplaints &&
            incident == other.incident && incidentRoom == other.incidentRoom && incidentDue == other.incidentDue &&
            nextIncidentDay == other.nextIncidentDay && furnishings.Equals(other.furnishings) &&
            checkInMinutes.Equals(other.checkInMinutes) && stayQuality.Equals(other.stayQuality) && stayRates.Equals(other.stayRates);
    }
}
