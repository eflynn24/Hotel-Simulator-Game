using System;

namespace DesertLantern
{
    [Serializable]
    internal sealed class PropertyMastery
    {
        public int profit;
        public int observedMinutes, occupiedMinutes, roomMinutes, independentMinutes;
        public int occupancyDays, independentDays, lastOccupancy;
        public bool rewardClaimed;
    }

    internal sealed partial class CountyPortfolio
    {
        public const int MasteryReward = 300;
        public const int ProfitTarget = 10000;

        private void InitializeMastery()
        {
            foreach (Hotel hotel in registry.Values)
            {
                if (hotel.mastery != null)
                    continue;
                hotel.mastery = new PropertyMastery();
                foreach (PropertyTransaction entry in save.ledger)
                    if (entry.property == hotel.propertyId && entry.account != Account.Financing)
                        hotel.mastery.profit += entry.amount;
            }
        }

        private void SampleMastery(int minutes, MotelService motelBefore, PropertyId foreground)
        {
            for (int slot = 0; slot < Offers.Length; slot++)
            {
                if (!Owned(slot))
                    continue;
                Hotel hotel = Business(slot);
                bool roadside = hotel.propertyId == Motel.Property;
                var mastery = hotel.mastery;
                mastery.observedMinutes += minutes;
                mastery.roomMinutes += minutes * (roadside ? motelBefore.RoomCapacity : Offers[slot].Rooms);
                mastery.occupiedMinutes += minutes * (roadside ? motelBefore.ActiveRoomCount : hotel.occupied);
                if (foreground != hotel.propertyId && (roadside ? motelBefore.FullyStaffed : Staffed(slot)))
                    mastery.independentMinutes += minutes;
            }
        }

        private void CloseMasteryDay()
        {
            foreach (Hotel hotel in registry.Values)
            {
                if (!hotel.owned)
                    continue;
                PropertyMastery mastery = hotel.mastery;
                if (mastery.observedMinutes == MotelService.MinutesPerDay)
                {
                    mastery.lastOccupancy = mastery.roomMinutes == 0 ? 0 : 100 * mastery.occupiedMinutes / mastery.roomMinutes;
                    mastery.occupancyDays = mastery.lastOccupancy >= 80 ? mastery.occupancyDays + 1 : 0;
                    mastery.independentDays = mastery.independentMinutes == MotelService.MinutesPerDay
                        ? mastery.independentDays + 1 : 0;
                }
                mastery.observedMinutes = mastery.occupiedMinutes = mastery.roomMinutes = mastery.independentMinutes = 0;
            }
        }

        public float MasteryStars(int slot)
        {
            Hotel hotel = Business(slot);
            return hotel.propertyId == Motel.Property ? Motel.CustomerStars
                : hotel.completed == 0 ? 0 : 1 + hotel.reputation / 25f;
        }

        public (string Name, int Current, int Target) MasteryImprovements(int slot)
        {
            Hotel hotel = Business(slot);
            return hotel.propertyId switch
            {
                PropertyId.RoadsideMotel => ("Renovate every guest room", Motel.RenovatedRooms, Motel.RoomCapacity),
                PropertyId.BudgetHotel => ("Prepare all rooms for a shift", hotel.readyRecord, Offers[slot].Rooms),
                PropertyId.BusinessHotel => ("Host conferences", hotel.eventsHosted, 3),
                PropertyId.BoutiqueHotel => ("Refit suites", hotel.suiteLevel, 3),
                _ => ("Open guest departments", CountDepartments(hotel.luxuryDepartments), 6)
            };
        }

        private static int CountDepartments(int departments)
        {
            int count = 0;
            for (int bit = 0; bit < 6; bit++)
                if ((departments & (1 << bit)) != 0)
                    count++;
            return count;
        }

        public bool Mastered(int slot)
        {
            if (!Owned(slot))
                return false;
            Hotel business = Business(slot);
            var improvements = MasteryImprovements(slot);
            return MasteryStars(slot) >= 4 && business.mastery.occupancyDays >= 7 &&
                business.mastery.profit >= ProfitTarget && improvements.Current >= improvements.Target &&
                Staffed(slot) && business.mastery.independentDays >= 3;
        }

        public bool ClaimMastery(int slot)
        {
            if (!Mastered(slot) || Business(slot).mastery.rewardClaimed)
                return false;
            Business(slot).mastery.rewardClaimed = true;
            PostCompany(MasteryReward, PropertyName(Offers[slot].Id) + " mastery reward");
            AddNote(slot, $"Property mastery completed. ${MasteryReward} added to company funds.");
            Save();
            return true;
        }
    }
}
