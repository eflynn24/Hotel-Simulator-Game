using System;
using Unity.Netcode;

namespace DesertLantern
{
    internal enum Expansion { Vending, Breakfast, RoomService, Pool, Restaurant, Fitness, Conference, Spa, Annex, UpperAnnex }
    internal enum ExpansionAction { Build, Buy, Alternate, Operate }
    internal readonly struct ExpansionOffer
    {
        public readonly string Name;
        public readonly int Investment;

        public ExpansionOffer(string name, int investment)
        {
            Name = name;
            Investment = investment;
        }
    }

    internal partial struct MotelService
    {
        public static readonly ExpansionOffer[] Expansions =
        {
            new ExpansionOffer("Vending machines", 80),
            new ExpansionOffer("Continental breakfast", 120),
            new ExpansionOffer("Room service", 150),
            new ExpansionOffer("Swimming pool", 260),
            new ExpansionOffer("Restaurant and bar", 320),
            new ExpansionOffer("Fitness center", 225),
            new ExpansionOffer("Conference center", 350),
            new ExpansionOffer("Spa", 310),
            new ExpansionOffer("Guest annex", 550),
            new ExpansionOffer("Annex upper floor", 450)
        };

        public const int ExpansionMinimumStays = 2;
        public const int BreakfastStockCost = 16;
        public const int RoomMealStockCost = 24;
        public const int RestaurantStaffWage = 16;
        public const int ConferenceFee = 120;
        private ushort expansionFlags;
        private int completedStays;
        private byte restaurantStaff;
        private ushort pendingMeals;
        private ushort conferenceRooms;
        public int Snacks, Drinks, BreakfastIngredients, BreakfastReady, RoomMealStock;
        public int PoolWater, PoolCondition, RestaurantFood, BarStock, GymCondition, SpaStock, SpaRequests;
        public int ConferenceStart, ConferenceEnd, EventsHosted;
        public bool ConferenceInProgress;
        public int ExpansionRevenue;

        public int CompletedStays => completedStays;
        public int RoomCapacity => BaseRoomCount + (HasExpansion(Expansion.Annex) ? 2 : 0) +
            (HasExpansion(Expansion.UpperAnnex) ? 2 : 0);

        public bool RoomBuilt(int room) => room >= 0 && room < RoomCapacity;
        public bool HasExpansion(Expansion project) => (expansionFlags & (1 << (int)project)) != 0;
        public bool HasCook => (restaurantStaff & 1) != 0;
        public bool HasServer => (restaurantStaff & 2) != 0;
        public bool RoomMealPending(int room) => (pendingMeals & (1 << room)) != 0;
        public bool ConferenceRoom(int room) => (conferenceRooms & (1 << room)) != 0;

        public bool CanExpand(Expansion project) => !HasExpansion(project) && completedStays >= ExpansionMinimumStays &&
            Cash >= Expansions[(int)project].Investment && (project != Expansion.Annex || OwnsAnnexLand) &&
            (project != Expansion.UpperAnnex || HasExpansion(Expansion.Annex));

        public bool BuildExpansion(Expansion project)
        {
            if (!CanExpand(project))
                return false;
            Post(Account.Maintenance, -Expansions[(int)project].Investment);
            expansionFlags |= (ushort)(1 << (int)project);
            if (project == Expansion.Pool)
                PoolWater = PoolCondition = 100;
            return true;
        }

        private bool PayExpansionCost(Account account, int price)
        {
            if (Cash < price)
                return false;
            Post(account, -price);
            return true;
        }

        private void EarnExpansionRevenue(int amount)
        {
            ExpansionRevenue += amount;
            Post(Account.ServiceRevenue, amount);
        }

        public bool ManageExpansion(Expansion project, ExpansionAction action, int option)
        {
            if (action == ExpansionAction.Build)
                return BuildExpansion(project);
            if (!HasExpansion(project))
                return false;

            switch (project)
            {
                case Expansion.Vending:
                    if (action == ExpansionAction.Buy && PayExpansionCost(Account.Supplies, 12))
                    {
                        Snacks += 6;
                        return true;
                    }
                    if (action == ExpansionAction.Alternate && PayExpansionCost(Account.Supplies, 12))
                    {
                        Drinks += 6;
                        return true;
                    }
                    break;
                case Expansion.Breakfast:
                    if (action == ExpansionAction.Buy && PayExpansionCost(Account.Supplies, BreakfastStockCost))
                    {
                        BreakfastIngredients += 6;
                        return true;
                    }
                    if (action == ExpansionAction.Operate && BreakfastIngredients > 0 && BreakfastReady < RoomCount * 2)
                    {
                        int portions = Math.Min(6, Math.Min(BreakfastIngredients, RoomCount * 2 - BreakfastReady));
                        BreakfastIngredients -= portions;
                        BreakfastReady += portions;
                        return true;
                    }
                    break;
                case Expansion.RoomService:
                    if (action == ExpansionAction.Buy && PayExpansionCost(Account.Supplies, RoomMealStockCost))
                    {
                        RoomMealStock += 4;
                        return true;
                    }
                    if (action == ExpansionAction.Operate && RoomBuilt(option) && RoomMealPending(option) && RoomMealStock > 0)
                    {
                        FinishOperation(OperationKind.Meal, option, GuestId(option));
                        pendingMeals &= (ushort)~(1 << option);
                        MealOrders |= (ushort)(1 << option);
                        RoomMealStock--;
                        EarnExpansionRevenue(18);
                        return true;
                    }
                    break;
                case Expansion.Pool:
                    if (action == ExpansionAction.Buy && PoolWater < 100 && PayExpansionCost(Account.Supplies, 18))
                    {
                        PoolWater = 100;
                        return true;
                    }
                    if (action == ExpansionAction.Operate && PoolCondition < 100 && PayExpansionCost(Account.Maintenance, 35))
                    {
                        PoolCondition = 100;
                        return true;
                    }
                    break;
                case Expansion.Restaurant:
                    if (action == ExpansionAction.Operate && option >= 0 && option < 2)
                        return ToggleRestaurantStaff(option);
                    if (action == ExpansionAction.Buy && PayExpansionCost(Account.Supplies, 22))
                    {
                        RestaurantFood += 8;
                        return true;
                    }
                    if (action == ExpansionAction.Alternate && PayExpansionCost(Account.Supplies, 16))
                    {
                        BarStock += 8;
                        return true;
                    }
                    break;
                case Expansion.Fitness:
                    if (action == ExpansionAction.Buy && GymCondition == 0 && PayExpansionCost(Account.Maintenance, 75))
                    {
                        GymCondition = 100;
                        return true;
                    }
                    if (action == ExpansionAction.Operate && GymCondition > 0 && GymCondition < 100 &&
                        PayExpansionCost(Account.Maintenance, 28))
                    {
                        GymCondition = 100;
                        return true;
                    }
                    break;
                case Expansion.Conference:
                    if (action == ExpansionAction.Operate)
                        return BookConference(option % 16, option / 16 + 1);
                    if (action == ExpansionAction.Alternate && ConferenceStart != 0 && !ConferenceInProgress)
                    {
                        CancelConference();
                        return true;
                    }
                    break;
                case Expansion.Spa:
                    if (action == ExpansionAction.Buy && PayExpansionCost(Account.Supplies, 32))
                    {
                        SpaStock += 4;
                        return true;
                    }
                    if (action == ExpansionAction.Operate && SpaStock > 0 && SpaRequests > 0)
                    {
                        SpaStock--;
                        SpaRequests--;
                        EarnExpansionRevenue(38);
                        return true;
                    }
                    break;
            }
            return false;
        }

        private bool ToggleRestaurantStaff(int role)
        {
            byte bit = (byte)(1 << role);
            if ((restaurantStaff & bit) != 0)
            {
                restaurantStaff &= (byte)~bit;
                return true;
            }
            if (!PayExpansionCost(Account.Payroll, 38))
                return false;
            restaurantStaff |= bit;
            return true;
        }

        public bool QueueRoomMeal(int room)
        {
            int bit = 1 << room;
            if (!HasExpansion(Expansion.RoomService) || (ActiveRooms & bit) == 0 ||
                ((MealOrders | pendingMeals) & bit) != 0)
                return false;
            pendingMeals |= (ushort)bit;
            return true;
        }

        private void RoomServiceCheckout(int room)
        {
            pendingMeals &= (ushort)~(1 << room);
        }

        private void ServeVendingGuest(int room)
        {
            if (!HasExpansion(Expansion.Vending))
                return;
            if (Snacks > 0 && ((room & 1) == 0 || Drinks == 0))
            {
                Snacks--;
                EarnExpansionRevenue(5);
            }
            else if (Drinks > 0)
            {
                Drinks--;
                EarnExpansionRevenue(5);
            }
        }

        private bool BookConference(int roomCount, int daysAhead)
        {
            if (ConferenceStart != 0 || roomCount < 2 || roomCount > 4 || daysAhead < 1 || daysAhead > 7 ||
                reservations.Length + roomCount * 4 > reservations.Capacity)
                return false;
            int start = (ClockMinute / MinutesPerDay + daysAhead) * MinutesPerDay + 10 * 60;
            int end = start + MinutesPerDay;
            ushort selected = 0;
            int found = 0;
            for (int room = 0; room < RoomCapacity && found < roomCount; room++)
                if (RoomAvailable(room, start, end) && !RoomUnusable(room))
                {
                    selected |= (ushort)(1 << room);
                    found++;
                }
            if (found != roomCount)
                return false;
            for (int room = 0; room < RoomCapacity; room++)
                if ((selected & (1 << room)) != 0)
                    Book(room, start, end, BookingChannel.Conference);
            ConferenceStart = start;
            ConferenceEnd = end;
            conferenceRooms = selected;
            return true;
        }

        private void CancelConference()
        {
            for (int index = ReservationCount - 1; index >= 0; index--)
            {
                if (ReservationChannel(index) == BookingChannel.Conference && ReservationStart(index) == ConferenceStart)
                    RemoveBooking(index, cancelled: true);
            }
            ConferenceStart = ConferenceEnd = 0;
            conferenceRooms = 0;
        }

        private void AdvanceExpansion(int previous)
        {
            for (int hour = previous / 60 + 1; hour <= ClockMinute / 60; hour++)
            {
                int guests = ActiveRoomCount;
                int localHour = hour % 24;
                if (localHour == 0 && restaurantStaff != 0)
                    for (int role = 0; role < 2; role++)
                        if ((restaurantStaff & (1 << role)) != 0 &&
                            !PayExpansionCost(Account.Payroll, RestaurantStaffWage))
                            restaurantStaff &= (byte)~(1 << role);

                if (localHour == 8 && HasExpansion(Expansion.Breakfast))
                {
                    int served = Math.Min(guests, BreakfastReady);
                    if (served > 0)
                        EarnExpansionRevenue(served * 7);
                    BreakfastReady = 0;
                }

                if (localHour == 11 && HasExpansion(Expansion.Pool))
                {
                    if (!UtilityOutage && PoolWater >= 50 && PoolCondition >= 40 && guests > 0)
                        EarnExpansionRevenue(guests * 4);
                    PoolWater = EquipmentCondition.AfterUse(PoolWater, 15 + guests);
                    PoolCondition = EquipmentCondition.AfterUse(PoolCondition, 5 + guests);
                }

                if (localHour == 7 && HasExpansion(Expansion.Fitness) && GymCondition >= 25 && guests > 0)
                {
                    EarnExpansionRevenue(guests * 6);
                    GymCondition = Math.Max(1, EquipmentCondition.AfterUse(GymCondition, 7 + guests));
                }

                if ((localHour == 12 || localHour == 19) && HasExpansion(Expansion.Restaurant) &&
                    HasCook && HasServer && guests > 0)
                {
                    int meals = Math.Min(guests, RestaurantFood);
                    int beverages = Math.Min(guests, BarStock);
                    RestaurantFood -= meals;
                    BarStock -= beverages;
                    EarnExpansionRevenue(meals * 15 + beverages * 8);
                }

                if (localHour == 17 && guests > 0)
                {
                    for (int room = 0; room < RoomCapacity; room++)
                        QueueRoomMeal(room);
                    if (HasExpansion(Expansion.Spa))
                        SpaRequests = Math.Min(RoomCapacity, SpaRequests + Math.Max(1, guests / 2));
                }
            }

            if (ConferenceStart != 0 && ClockMinute >= ConferenceStart && !ConferenceInProgress)
            {
                int rooms = 0;
                for (int room = 0; room < RoomCapacity; room++)
                    if (ConferenceRoom(room))
                        rooms++;
                EventsHosted++;
                EarnExpansionRevenue(ConferenceFee + rooms * 25);
                ConferenceInProgress = true;
            }
            if (ConferenceInProgress && ClockMinute >= ConferenceEnd)
            {
                ConferenceInProgress = false;
                ConferenceStart = ConferenceEnd = 0;
                conferenceRooms = 0;
            }
        }

        private void SerializeExpansion<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref expansionFlags);
            serializer.SerializeValue(ref completedStays);
            serializer.SerializeValue(ref restaurantStaff);
            serializer.SerializeValue(ref pendingMeals);
            serializer.SerializeValue(ref conferenceRooms);
            serializer.SerializeValue(ref Snacks);
            serializer.SerializeValue(ref Drinks);
            serializer.SerializeValue(ref BreakfastIngredients);
            serializer.SerializeValue(ref BreakfastReady);
            serializer.SerializeValue(ref RoomMealStock);
            serializer.SerializeValue(ref PoolWater);
            serializer.SerializeValue(ref PoolCondition);
            serializer.SerializeValue(ref RestaurantFood);
            serializer.SerializeValue(ref BarStock);
            serializer.SerializeValue(ref GymCondition);
            serializer.SerializeValue(ref SpaStock);
            serializer.SerializeValue(ref SpaRequests);
            serializer.SerializeValue(ref ConferenceStart);
            serializer.SerializeValue(ref ConferenceEnd);
            serializer.SerializeValue(ref EventsHosted);
            serializer.SerializeValue(ref ConferenceInProgress);
            serializer.SerializeValue(ref ExpansionRevenue);
        }

        private bool ExpansionEquals(MotelService other) =>
            expansionFlags == other.expansionFlags && completedStays == other.completedStays &&
            restaurantStaff == other.restaurantStaff && pendingMeals == other.pendingMeals &&
            conferenceRooms == other.conferenceRooms && Snacks == other.Snacks && Drinks == other.Drinks &&
            BreakfastIngredients == other.BreakfastIngredients && BreakfastReady == other.BreakfastReady &&
            RoomMealStock == other.RoomMealStock && PoolWater == other.PoolWater && PoolCondition == other.PoolCondition &&
            RestaurantFood == other.RestaurantFood && BarStock == other.BarStock && GymCondition == other.GymCondition &&
            SpaStock == other.SpaStock && SpaRequests == other.SpaRequests &&
            ConferenceStart == other.ConferenceStart && ConferenceEnd == other.ConferenceEnd &&
            EventsHosted == other.EventsHosted && ConferenceInProgress == other.ConferenceInProgress && ExpansionRevenue == other.ExpansionRevenue;
    }
}
