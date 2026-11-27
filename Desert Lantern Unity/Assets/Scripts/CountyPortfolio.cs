using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DesertLantern
{
    internal sealed partial class CountyPortfolio
    {
        public static readonly string[] StaffRoles =
        {
            "Bellhop", "Guest services attendant", "Housekeeper", "Stocker", "Maintenance technician",
            "Receptionist", "Purchasing manager", "Security guard", "Property manager", "Accountant"
        };

        public static readonly string[] Duties =
        {
            "Transports luggage", "Escorts guests and handles requests", "Cleans and prepares rooms",
            "Receives deliveries and restocks shelves", "Repairs worn equipment",
            "Handles bookings, the desk and the phone", "Orders supplies when stock runs low",
            "Resolves noise and security reports", "Supervises daily operations and incidents", "Pays invoices and prepares financial reports"
        };

        public readonly struct PropertyOffer
        {
            public readonly PropertyId Id, Requires;
            public readonly PropertyLayout Layout;
            public readonly string Name, Focus;
            public readonly int Rooms, Price, Rate, StartingCash;

            public PropertyOffer(PropertyId id, PropertyId requires, PropertyLayout layout, string name, int rooms, int price, int rate, int startingCash, string focus)
            {
                Id = id;
                Requires = requires;
                Layout = layout;
                Name = name;
                Rooms = rooms;
                Price = price;
                Rate = rate;
                StartingCash = startingCash;
                Focus = focus;
            }
        }

        public static readonly PropertyOffer[] Offers =
        {
            new PropertyOffer(PropertyId.RoadsideMotel, PropertyId.None, PropertyLayout.Roadside, "Roadside Motel", 8, 0, 65, 230, "Learn the desk, room keys, housekeeping and guest service."),
            new PropertyOffer(PropertyId.BudgetHotel, PropertyId.RoadsideMotel, PropertyLayout.BudgetLobby, "Budget Hotel", 20, 1400, 68, 560, "Run high turnover with bulk orders and daily room preparation."),
            new PropertyOffer(PropertyId.BusinessHotel, PropertyId.BudgetHotel, PropertyLayout.Dashboard, "Business Hotel", 40, 3400, 92, 780, "Prepare breakfast and reserve room blocks for conferences."),
            new PropertyOffer(PropertyId.BoutiqueHotel, PropertyId.BusinessHotel, PropertyLayout.Dashboard, "Boutique Hotel", 60, 6800, 118, 980, "Customize suites and fulfill individual guest preferences."),
            new PropertyOffer(PropertyId.LuxuryHotel, PropertyId.BoutiqueHotel, PropertyLayout.Dashboard, "Luxury Hotel", 60, 10500, 172, 1350, "Coordinate VIP stays, concierge, dining, pool, spa, villas and entertainment.")
        };

        [Serializable]
        internal sealed class Hotel
        {
            public PropertyId propertyId;
            public bool owned;
            public PropertyMastery mastery;
            public int readyRecord, lastDecisionNotice;
            public int reputation = 50;
            public string operationalState;
            [NonSerialized] public MotelService Detailed;
            public int cash, staff, occupied, prepared, stock, arriving, completed;
            public int breakfast, groupDay, groupRooms, groupArrivals, eventsHosted, suiteLevel, requests, luxuryDepartments, pausedDepartments, vip;
            public int revenue, payroll, bills, expenses, lastDrivenDay = -1;
            public int deliveryOrder, deliveryQuantity, deliveryDue;
            public bool deliveryNotified;
            public int luggage, serviceRequests, servedRequests, dirtyLinen, cleanLinen, noiseReports;
            public int condition = 100;
            public PropertyIncident incident;
            public int incidentDay;
        }

        [Serializable]
        internal sealed class UpdateNote
        {
            public int id, day, property, revenue, payroll, bills, expenses, closingCash;
            public PropertyId propertyId;
            public bool statement;
            public string text;
        }

        [Serializable]
        private class SaveData
        {
            public int minute = MotelService.OpeningMinute;
            public int funds;
            public PropertyId lastVisitedProperty = PropertyId.RoadsideMotel;
            public bool started;
            public int nextNote, readThrough;
            public List<Hotel> properties = new List<Hotel>();
            public List<PropertyInvoice> invoices = new List<PropertyInvoice>();
            public List<PropertyTransaction> ledger = new List<PropertyTransaction>();
            public List<UpdateNote> inbox = new List<UpdateNote>();
        }

        [Serializable]
        private sealed class LegacySave : SaveData
        {
            public int lastVisited;
            public Hotel[] hotels;
            public string motel;
        }

        private static readonly PropertyId[] LegacyOrder =
        {
            PropertyId.RoadsideMotel, PropertyId.BudgetHotel, PropertyId.BusinessHotel,
            PropertyId.BoutiqueHotel, PropertyId.LuxuryHotel
        };

        private const string SaveKey = "county.portfolio.v2";
        private const string LegacySaveKey = "county.portfolio.v1";
        public const int FullStaff = (1 << 10) - 1;
        public const int HireCost = 55;
        private readonly SaveData save;
        private int lastSavedMinute;

        private readonly Dictionary<PropertyId, Hotel> registry = new Dictionary<PropertyId, Hotel>();
        public ref MotelService Motel => ref Business(PropertyId.RoadsideMotel).Detailed;
        public int ClockMinute => save.minute;
        public int Day => ClockMinute / MotelService.MinutesPerDay;
        public int Funds => save.funds;
        public int LastVisited => Math.Max(0, Array.FindIndex(Offers, offer => offer.Id == save.lastVisitedProperty));
        public PropertyId LastVisitedProperty => save.lastVisitedProperty;
        public bool Started => save.started;
        public IReadOnlyList<UpdateNote> Inbox => save.inbox;
        public int UnreadUpdates => Math.Min(save.inbox.Count, save.nextNote - save.readThrough);

        public void ReadUpdates() => save.readThrough = save.nextNote;
        public Hotel Business(int property) => Business(Offers[property].Id);
        public Hotel Business(PropertyId property) => registry[property];
        private static int MotelSlot => MapSlot(PropertyId.RoadsideMotel);

        public static int MapSlot(PropertyId property) => Array.FindIndex(Offers, offer => offer.Id == property);

        public static string PropertyName(PropertyId property)
        {
            if (property == PropertyId.None)
                return "Company";
            foreach (PropertyOffer offer in Offers)
                if (offer.Id == property)
                    return offer.Name;
            return "Property " + (ushort)property;
        }

        public int Reputation(PropertyId property) => property == PropertyId.RoadsideMotel ? Motel.Reputation : Business(property).reputation;

        private CountyPortfolio(SaveData data)
        {
            save = data;
            save.properties ??= new List<Hotel>();
            save.invoices ??= new List<PropertyInvoice>();
            save.ledger ??= new List<PropertyTransaction>();
            save.inbox ??= new List<UpdateNote>();
            lastSavedMinute = data.minute;
            foreach (Hotel record in save.properties)
                if (record != null && record.propertyId != PropertyId.None)
                    registry.Add(record.propertyId, record);
            foreach (PropertyOffer offer in Offers)
            {
                if (registry.ContainsKey(offer.Id))
                    continue;
                var record = new Hotel { propertyId = offer.Id };
                registry.Add(offer.Id, record);
                save.properties.Add(record);
            }
            Hotel motel = Business(PropertyId.RoadsideMotel);
            motel.owned = true;
            foreach (Hotel record in registry.Values)
            {
                if (string.IsNullOrEmpty(record.operationalState))
                    continue;
                using var reader = new FastBufferReader(Convert.FromBase64String(record.operationalState), Allocator.Temp);
                reader.ReadNetworkSerializable<MotelService.SavedState>(out MotelService.SavedState state);
                record.Detailed = state.ForProperty(record.propertyId);
            }
            if (motel.Detailed.Property == PropertyId.None)
                motel.Detailed = MotelService.New();
            if (!registry.ContainsKey(save.lastVisitedProperty))
                save.lastVisitedProperty = PropertyId.RoadsideMotel;
            InitializeMastery();
            CaptureMotelBaseline();
        }

        private static SaveData UpgradeLegacy(string json)
        {
            var data = JsonUtility.FromJson<LegacySave>(json);
            if (data?.hotels == null || data.hotels.Length != LegacyOrder.Length)
                throw new InvalidOperationException("The legacy property table was not recognized.");
            data.properties = new List<Hotel>();
            for (int slot = 0; slot < LegacyOrder.Length; slot++)
            {
                Hotel hotel = data.hotels[slot] ?? new Hotel();
                hotel.propertyId = LegacyOrder[slot];
                hotel.reputation = 50;
                if (slot == 0)
                    hotel.operationalState = data.motel;
                data.properties.Add(hotel);
            }
            data.lastVisitedProperty = (uint)data.lastVisited < LegacyOrder.Length
                ? LegacyOrder[data.lastVisited] : PropertyId.RoadsideMotel;
            foreach (UpdateNote note in data.inbox)
                if ((uint)note.property < LegacyOrder.Length)
                    note.propertyId = LegacyOrder[note.property];
            return new SaveData
            {
                minute = data.minute,
                funds = data.funds,
                lastVisitedProperty = data.lastVisitedProperty,
                started = data.started,
                nextNote = data.nextNote,
                readThrough = data.readThrough,
                properties = data.properties,
                inbox = data.inbox
            };
        }

        public static CountyPortfolio Load()
        {
            try
            {
                string stored = PlayerPrefs.GetString(SaveKey, "");
                if (stored.Length > 0)
                    return new CountyPortfolio(JsonUtility.FromJson<SaveData>(stored));
                string legacy = PlayerPrefs.GetString(LegacySaveKey, "");
                if (legacy.Length > 0)
                    return new CountyPortfolio(UpgradeLegacy(legacy));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"County save could not be loaded: {exception.Message}");
                string snapshot = PlayerPrefs.GetString(SaveKey, "");
                if (snapshot.Length > 0)
                    PlayerPrefs.SetString(SaveKey + ".recovery", snapshot);
            }
            return new CountyPortfolio(new SaveData());
        }

        public void Save()
        {
            RecordMotelLedger();
            foreach (Hotel record in registry.Values)
            {
                if (record.Detailed.Property == PropertyId.None)
                    continue;
                using var writer = new FastBufferWriter(2048, Allocator.Temp, 8192);
                writer.WriteNetworkSerializable(new MotelService.SavedState { Service = record.Detailed });
                record.operationalState = Convert.ToBase64String(writer.ToArray());
            }
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
            lastSavedMinute = ClockMinute;
        }

        public bool Owned(int property) => Business(property).owned;
        public int Cash(int property) => property == MotelSlot ? Motel.Cash : Business(property).cash;
        public bool Staffed(int property) => property == MotelSlot ? Motel.FullyStaffed : Business(property).staff == FullStaff;
        public bool HasEmployee(int property, int role) => property == MotelSlot ? Motel.StaffHired(role)
            : (Business(property).staff & (1 << role)) != 0;

        public int StaffCount(int property)
        {
            int count = 0;
            for (int role = 0; role < StaffRoles.Length; role++)
                if (HasEmployee(property, role))
                    count++;
            return count;
        }

        public bool Available(int property)
        {
            if ((uint)property >= Offers.Length || Owned(property))
                return false;
            int prerequisite = MapSlot(Offers[property].Requires);
            return prerequisite >= 0 && Owned(prerequisite) && Staffed(prerequisite);
        }
        public bool BuyProperty(int property)
        {
            if (!Available(property) || Funds < Offers[property].Price)
                return false;
            PropertyOffer offer = Offers[property];
            Hotel hotel = Business(property);
            PostCompany(-offer.Price, offer.Name + " acquisition");
            hotel.owned = true;
            PostHotel(hotel, Account.Financing, offer.StartingCash, "Opening operating capital");
            hotel.prepared = offer.Rooms;
            hotel.stock = offer.Rooms;
            hotel.arriving = offer.Rooms / 3;
            if (hotel.propertyId == PropertyId.LuxuryHotel)
                hotel.vip = 4;
            AddNote(property, "Acquisition completed. Opening cash and supplies are available.");
            Save();
            return true;
        }

        public void Visit(int property)
        {
            if (!Owned(property))
                return;
            save.lastVisitedProperty = Offers[property].Id;
            save.started = true;
            Save();
        }

        public bool ToggleEmployee(int property, int role)
        {
            if (!Owned(property) || role < 0 || role >= StaffRoles.Length)
                return false;
            bool employed = HasEmployee(property, role);
            if (!employed && Cash(property) < HireCost)
                return false;
            if (property == MotelSlot)
                Motel.UpdateEmployee(new PropertyEmployee(Offers[property].Id, role), false);
            else
            {
                Hotel hotel = Business(property);
                hotel.staff ^= 1 << role;
                if (!employed)
                {
                    PostHotel(hotel, Account.Payroll, -HireCost, "Employee hire");
                }
            }
            AddNote(property, $"{StaffRoles[role]} {(employed ? "dismissed" : "hired")}{(Staffed(property) ? " · Independent operations ready" : "")}.");
            if (property != MotelSlot && Staffed(property))
                RunIndependentShift(property);
            Save();
            return true;
        }

        public int Reserve(int property) => property == MotelSlot ? 250 : 300 + Offers[property].Rooms * 3;

        public bool Transfer(int property, bool intoHotel)
        {
            if (!Owned(property))
                return false;
            int amount = intoHotel ? Math.Min(Funds, 250) : Math.Min(250, Cash(property) - Reserve(property));
            if (amount <= 0)
                return false;
            int direction = intoHotel ? amount : -amount;
            if (property == MotelSlot)
                Motel.TransferOperatingCash(direction);
            else
                PostHotel(Business(property), Account.Financing, direction, "Company transfer");
            PostCompany(-direction, "Transfer with " + Offers[property].Name);
            Save();
            return true;
        }

        public void Advance(int target, PropertyId foreground, MotelService previous)
        {
            if (target <= ClockMinute)
                return;
            bool playingMotel = foreground == Motel.Property;
            int from = ClockMinute;
            while (from < target)
            {
                int nextShift = (from / 30 + 1) * 30;
                int next = Math.Min(target, nextShift);
                bool nextDay = from / MotelService.MinutesPerDay != next / MotelService.MinutesPerDay;
                SampleMastery(next - from, playingMotel ? previous : Motel, foreground);
                if (nextDay)
                    CloseMasteryDay();
                if (nextDay)
                {
                    var statement = playingMotel ? previous : Motel;
                    MotelStatement(statement);
                }
                var motelBeforeShift = playingMotel ? previous : Motel;
                save.minute = next;
                if (nextDay)
                {
                    int occupied = motelBeforeShift.ActiveRoomCount;
                    IssueInvoice(Motel.Property, Account.Electricity, 4 + occupied);
                    IssueInvoice(Motel.Property, Account.Water, 3 + occupied);
                    IssueInvoice(Motel.Property, Account.Internet, 3);
                    if (!Motel.StaffHired(StaffRole.Accountant))
                        AddNote(MotelSlot, "Utility bills are ready to pay in Finances.");
                }
                if (!playingMotel)
                {
                    Motel.AdvanceClock(next, true);
                    Motel.AdvanceDelivery();
                }
                if (nextDay || next == nextShift)
                    RecordMotelChanges(motelBeforeShift);
                PropertyIncident pendingIncident = Motel.Incident;
                if (next == nextShift)
                    Motel.WorkMotelShift(!playingMotel);
                if (pendingIncident != PropertyIncident.None && Motel.Incident == PropertyIncident.None)
                    AddNote(MotelSlot, "Staff resolved the reported motel incident.");
                RecordMotelLedger();
                Hotel motelRecord = Business(MotelSlot);
                if (Motel.PendingDecision != ManagementDecision.None && Motel.DecisionDeadline != motelRecord.lastDecisionNotice)
                {
                    motelRecord.lastDecisionNotice = Motel.DecisionDeadline;
                    AddNote(MotelSlot, "A management decision is waiting in Bookings on the reception computer.");
                }

                if (nextDay)
                {
                    if (Staffed(MotelSlot))
                        SweepSurplus(MotelSlot);
                    for (int property = 0; property < Offers.Length; property++)
                        if (property != MotelSlot && Owned(property))
                            SettleHotelDay(property);
                }
                if (next == nextShift)
                {
                    RunAccountingShift();
                    for (int property = 0; property < Offers.Length; property++)
                        if (property != MotelSlot && Owned(property))
                            AdvanceHotelTasks(property);
                }
                from = next;
            }

            if (ClockMinute - lastSavedMinute >= 120)
                Save();
        }

        public void RecordMotelStaffChange(int role, bool hired)
        {
            AddNote(MotelSlot, $"{StaffRoles[role]} {(hired ? "hired" : "dismissed")}.");
            Save();
        }

        private void RecordMotelChanges(MotelService before)
        {
            for (int role = 0; role < StaffRoles.Length; role++)
                if (before.StaffHired(role) && !Motel.StaffHired(role))
                    AddNote(MotelSlot, $"{StaffRoles[role]} left after an unpaid shift.");
            if (before.Incident != Motel.Incident && Motel.Incident != PropertyIncident.None)
                AddNote(MotelSlot, $"{Motel.Incident switch { PropertyIncident.Pests => "Pest report", PropertyIncident.SuspiciousActivity => "Security incident", _ => "Utility emergency" }} needs attention. Open the motel management computer.");
            if (before.Delivery != SupplyDelivery.AtBay && Motel.Delivery == SupplyDelivery.AtBay)
                AddNote(MotelSlot, $"Supply order #{Motel.ActiveOrderNumber} arrived at the delivery bay.");
        }

        private void SweepSurplus(int property)
        {
            int amount = Math.Max(0, (Cash(property) - Reserve(property)) / 2);
            if (amount == 0)
                return;
            if (property == MotelSlot)
                Motel.TransferOperatingCash(-amount);
            else
                PostHotel(Business(property), Account.Financing, -amount, "Surplus to company");
            PostCompany(amount, "Surplus from " + Offers[property].Name);
            AddNote(property, $"${amount} transferred to the acquisition fund.");
        }

        private void MotelStatement(MotelService motel)
        {
            int utility = motel.ReportAmount(0, Account.Electricity) + motel.ReportAmount(0, Account.Water) + motel.ReportAmount(0, Account.Internet);
            AddStatement(MotelSlot, motel.ReportAmount(0, Account.RoomRevenue) + motel.ReportAmount(0, Account.ServiceRevenue),
                -motel.ReportAmount(0, Account.Payroll), -utility,
                -motel.ReportAmount(0, Account.Supplies) - motel.ReportAmount(0, Account.Maintenance) - motel.ReportAmount(0, Account.Adjustments), motel.Cash);
        }

        private void SettleHotelDay(int property)
        {
            Hotel hotel = Business(property);
            PropertyOffer offer = Offers[property];
            IssueHotelBills(property);
            if (!HasEmployee(property, (int)StaffRole.Accountant))
                AddNote(property, "Utility bills are ready to pay in Finances.");
            for (int role = 0; role < StaffRoles.Length; role++)
            {
                if (!HasEmployee(property, role))
                    continue;
                if (hotel.cash >= MotelService.StaffWage)
                {
                    PostHotel(hotel, Account.Payroll, -MotelService.StaffWage, StaffRoles[role] + " wages");
                }
                else
                {
                    hotel.staff &= ~(1 << role);
                    AddNote(property, $"{StaffRoles[role]} left after an unpaid shift.");
                }
            }

            AddStatement(property, hotel.revenue, hotel.payroll, hotel.bills, hotel.expenses, hotel.cash);
            if (Staffed(property))
                SweepSurplus(property);
            hotel.revenue = hotel.payroll = hotel.bills = hotel.expenses = 0;

            if (hotel.groupArrivals > 0)
            {
                AddNote(property, $"{hotel.groupArrivals} conference guests missed check-in.");
                hotel.groupArrivals = 0;
            }
            if (hotel.requests > 0)
                hotel.requests = 0;
            if (hotel.occupied > 0)
            {
                int experience = (hotel.arriving == 0 ? 1 : -1) +
                    (hotel.servedRequests > 0 ? 1 : 0) - (hotel.serviceRequests > 0 || hotel.luggage > 0 || hotel.noiseReports > 0 ? 1 : 0) -
                    (hotel.condition < 50 || hotel.incident != PropertyIncident.None ? 1 : 0);
                hotel.reputation = Math.Clamp(hotel.reputation + experience, 0, 100);
                hotel.dirtyLinen += hotel.occupied;
                hotel.condition = Math.Max(0, hotel.condition - Math.Max(1, hotel.occupied / 4));
            }
            hotel.luggage = hotel.serviceRequests = hotel.servedRequests = hotel.noiseReports = 0;
            if (hotel.incident == PropertyIncident.None && Day % 4 == (property + 1) % 4)
            {
                hotel.incident = (Day + property) % 3 == 0 ? PropertyIncident.UtilityEmergency : PropertyIncident.SuspiciousActivity;
                hotel.incidentDay = Day;
                AddNote(property, hotel.incident == PropertyIncident.UtilityEmergency ? "Utility failure reported." : "Security incident reported.");
            }
            hotel.completed += hotel.occupied;
            hotel.occupied = 0;
            hotel.arriving = Math.Min(offer.Rooms, 7 + offer.Rooms / 2 + (Day * 7 + property * 3) % (offer.Rooms / 3));
            if (hotel.propertyId == PropertyId.BusinessHotel && hotel.groupDay == Day)
            {
                hotel.arriving = Math.Min(offer.Rooms, hotel.arriving + hotel.groupRooms);
                hotel.groupArrivals = hotel.groupRooms;
                AddNote(property, $"Conference arrived with {hotel.groupRooms} rooms reserved.");
                hotel.groupDay = hotel.groupRooms = 0;
            }
            if (hotel.propertyId == PropertyId.LuxuryHotel)
                hotel.vip = Math.Min(8, 2 + Day % 5);

            if (HasEmployee(property, (int)StaffRole.Manager))
                RunIndependentShift(property);
        }

        private void RunIndependentShift(int property)
        {
            Hotel hotel = Business(property);
            if (hotel.lastDrivenDay == Day)
                return;
            hotel.lastDrivenDay = Day;
            PropertyOffer offer = Offers[property];
            if (hotel.stock < offer.Rooms)
                RunHotelAction(property, 0);
            CompleteTask(new PropertyTask(offer.Id, PropertyTaskKind.PrepareRooms), true);
            CompleteTask(new PropertyTask(offer.Id, PropertyTaskKind.CheckIn), true);
            if (hotel.propertyId == PropertyId.BusinessHotel)
            {
                if (hotel.breakfast == 0 && hotel.occupied > 0)
                    RunHotelAction(property, 4);
                if (hotel.groupDay == 0 && Day % 3 == 0)
                    RunHotelAction(property, 5);
            }
            RunSpecialty(property);
        }

        private bool Spend(Hotel hotel, Account account, int amount, string description)
        {
            if (hotel.cash < amount)
                return false;
            PostHotel(hotel, account, -amount, description);
            return true;
        }

        private bool PrepareRooms(int property)
        {
            Hotel hotel = Business(property);
            int prepared = Math.Min(Offers[property].Rooms - hotel.occupied - hotel.prepared, hotel.stock + hotel.cleanLinen);
            if (prepared <= 0)
                return false;
            hotel.prepared += prepared;
            hotel.readyRecord = Math.Max(hotel.readyRecord, hotel.prepared);
            int reused = Math.Min(prepared, hotel.cleanLinen);
            hotel.stock -= prepared - reused;
            hotel.cleanLinen -= reused;
            return true;
        }

        private bool RunFrontDesk(int property)
        {
            Hotel hotel = Business(property);
            if (hotel.incident == PropertyIncident.UtilityEmergency)
                return false;
            int admitted = Math.Min(hotel.arriving, hotel.prepared);
            if (admitted <= 0)
                return false;
            hotel.arriving -= admitted;
            hotel.prepared -= admitted;
            hotel.occupied += admitted;
            hotel.luggage += admitted;
            hotel.serviceRequests += Math.Max(1, admitted / 3);
            if (hotel.propertyId == PropertyId.BusinessHotel && hotel.groupArrivals > 0)
            {
                int admittedGroup = Math.Min(admitted, hotel.groupArrivals);
                hotel.groupArrivals -= admittedGroup;
                PostHotel(hotel, Account.ServiceRevenue, admittedGroup * 24, "Conference fees");
                if (hotel.groupArrivals == 0)
                {
                    hotel.eventsHosted++;
                    AddNote(property, "Conference check-in complete.");
                }
            }
            PostHotel(hotel, Account.RoomRevenue, admitted * (Offers[property].Rate + hotel.suiteLevel * 12), "Guest check-ins");
            if (hotel.propertyId == PropertyId.BoutiqueHotel)
                hotel.requests += admitted / 3;
            return true;
        }

        private bool RunSpecialty(int property)
        {
            Hotel hotel = Business(property);
            if (hotel.propertyId == PropertyId.BusinessHotel && hotel.breakfast > 0 && hotel.occupied > 0)
            {
                int served = Math.Min(hotel.breakfast, hotel.occupied);
                hotel.breakfast -= served;
                PostHotel(hotel, Account.ServiceRevenue, served * 9, "Breakfast service");
                return true;
            }
            if (hotel.propertyId == PropertyId.BoutiqueHotel && hotel.requests > 0 && hotel.stock > 0)
            {
                int served = Math.Min(hotel.requests, hotel.stock);
                hotel.requests -= served;
                hotel.stock -= served;
                PostHotel(hotel, Account.ServiceRevenue, served * (24 + hotel.suiteLevel * 8), "Personalized services");
                return true;
            }
            int openDepartments = hotel.luxuryDepartments & ~hotel.pausedDepartments;
            if (hotel.propertyId == PropertyId.LuxuryHotel && openDepartments != 0 && hotel.occupied > 0 && hotel.stock > 0)
            {
                int served = Math.Min(Math.Min(hotel.vip, hotel.occupied), hotel.stock);
                if (served == 0)
                    return false;
                int perGuest = 25;
                for (int department = 0; department < 6; department++)
                    if ((openDepartments & (1 << department)) != 0)
                        perGuest += 9 + department * 4;
                hotel.vip -= served;
                hotel.stock -= served;
                PostHotel(hotel, Account.ServiceRevenue, served * perGuest, "VIP services");
                return true;
            }
            return false;
        }

        public bool RunHotelAction(PropertyId property, int action) => RunHotelAction(MapSlot(property), action);

        public bool RunHotelAction(int property, int action)
        {
            if ((uint)property >= Offers.Length || property == MotelSlot || !Owned(property))
                return false;
            Hotel hotel = Business(property);
            if (action == 20)
                return RespondToHotelIncident(property);
            if (action == 21)
                return ServiceHotelEquipment(property);
            if (action == 22)
                return DeliverHotelLuggage(hotel);
            if (action == 23)
                return ServeHotelRequests(hotel);
            if (action == 24)
                return WashHotelLaundry(hotel);
            if (action == 0)
                return OrderHotelStock(property);
            if (action == 1)
                return CompleteTask(new PropertyTask(hotel.propertyId, PropertyTaskKind.PrepareRooms), false);
            if (action == 2)
                return CompleteTask(new PropertyTask(hotel.propertyId, PropertyTaskKind.CheckIn), false);
            if (action == 6 && hotel.deliveryQuantity > 0)
                return CompleteTask(new PropertyTask(hotel.propertyId, PropertyTaskKind.ReceiveSupplies, hotel.deliveryOrder), false);
            if (action == 3)
                return RunSpecialty(property);
            if (hotel.propertyId == PropertyId.BusinessHotel && action == 4 && Spend(hotel, Account.Supplies, 75, "Breakfast ingredients"))
            {
                hotel.breakfast += 25;
                return true;
            }
            if (hotel.propertyId == PropertyId.BusinessHotel && action == 5 && hotel.groupDay == 0)
            {
                hotel.groupDay = Day + 1;
                hotel.groupRooms = 12;
                AddNote(property, "Conference reserved for tomorrow with a 12-room block.");
                return true;
            }
            if (hotel.propertyId == PropertyId.BoutiqueHotel && action == 4 && hotel.suiteLevel < 3 && Spend(hotel, Account.Maintenance, 320, "Suite refit"))
            {
                hotel.suiteLevel++;
                return true;
            }
            if (hotel.propertyId == PropertyId.LuxuryHotel && action >= 4 && action < 10 && (hotel.luxuryDepartments & (1 << (action - 4))) == 0 &&
                Spend(hotel, Account.Maintenance, 230 + (action - 4) * 45, "Department setup"))
            {
                hotel.luxuryDepartments |= 1 << (action - 4);
                AddNote(property, "Another luxury department opened.");
                return true;
            }
            if (hotel.propertyId == PropertyId.LuxuryHotel && action >= 10 && action < 16 && (hotel.luxuryDepartments & (1 << (action - 10))) != 0)
            {
                hotel.pausedDepartments ^= 1 << (action - 10);
                return true;
            }
            return false;
        }

        private void AddStatement(int property, int revenue, int payroll, int bills, int expenses, int cash)
        {
            Append(new UpdateNote
            {
                day = Day, propertyId = Offers[property].Id, statement = true, revenue = revenue, payroll = payroll,
                bills = bills, expenses = expenses, closingCash = cash
            });
        }

        private void AddNote(int property, string text) => Append(new UpdateNote { day = Day, propertyId = Offers[property].Id, text = text });

        private void Append(UpdateNote note)
        {
            note.id = ++save.nextNote;
            save.inbox.Add(note);
            if (save.inbox.Count > 80)
                save.inbox.RemoveAt(0);
        }
    }
}
