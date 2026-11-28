using System;

namespace DesertLantern
{
    internal partial struct MotelService
    {
        public bool FullyStaffed
        {
            get
            {
                for (int role = 0; role < CountyPortfolio.StaffRoles.Length; role++)
                    if (!StaffHired(role))
                        return false;
                return true;
            }
        }

        public void TransferOperatingCash(int amount) => Post(Account.Financing, amount);

        public void WorkMotelShift(bool remote)
        {
            if (StaffHired(StaffRole.Purchasing))
                ReorderPropertySupplies();
            if (StaffHired(StaffRole.Security))
            {
                for (int room = 0; room < RoomCapacity; room++)
                    if (OpenNoiseComplaint(room))
                        quietedRooms |= (ushort)(1 << room);
                if (Incident == PropertyIncident.SuspiciousActivity)
                    ManageProperty(ManagementAction.Investigate, 0, 0);
            }
            if (StaffHired(StaffRole.Manager) && Incident != PropertyIncident.None &&
                (!StaffUrgentOnly((int)StaffRole.Manager) || Incident == PropertyIncident.UtilityEmergency))
                ManageProperty(ManagementAction.Investigate, 0, 0);

            if (StaffHired(StaffRole.Receptionist))
                WorkReception();

            if (!remote)
                return;

            if (StaffHired(StaffRole.Bellhop))
                MoveRemoteLuggage();
            int arrival = NextDeskArrival();
            if (arrival >= 0 && !BookingDue(arrival) && StaffHired(StaffRole.Receptionist))
                AcceptReservation(NextBooking(arrival), BookingStart(arrival));
            if (StaffHired(StaffRole.Receptionist) && arrival >= 0)
            {
                CheckInGuest(arrival, BookingStart(arrival), ClockMinute, true);
                if (StaffHired(StaffRole.Bellhop))
                    MoveRemoteLuggage();
            }

            WorkAttendant();
            if (StaffHired(StaffRole.Housekeeper))
                WorkHousekeeping(3);
        }

        private void WorkReception()
        {
            for (int index = 0; index < ReservationCount; index++)
                if (ReservationPending(index) && ReservationChannel(index) != BookingChannel.WalkIn &&
                    !(ManualOnlineApproval && ReservationChannel(index) == BookingChannel.Online))
                    AcceptReservation(index, ReservationStart(index));
            int checkout;
            while ((checkout = CheckoutWaiting()) >= 0)
                CompleteCheckout(checkout, true);
            if (!PhoneRinging)
                return;
            TryAnswerPhone(ReceptionPhoneWorker);
            if (!CanBookPhoneFor(ReceptionPhoneWorker))
                return;
            int room = SuggestRoom(PhoneArrival, DepartureForNights(PhoneArrival, PhoneNights));
            if (!AcceptPhoneReservation(room, NextPhoneCall, ReceptionPhoneWorker))
                FinishPhoneBooking();
        }

        private void MoveRemoteLuggage()
        {
            if (Bag == LuggageStatus.Carried && Carrier != BellhopId)
                RecoverBag(Carrier);
            if (Bag == LuggageStatus.InCar)
                PickUp(BellhopId);
            if (Bag == LuggageStatus.Carried)
                Deliver(BellhopId);
        }

        private void WorkAttendant()
        {
            if (!StaffHired(StaffRole.GuestServices))
                return;
            int target = -1;
            for (int room = 0; room < RoomCapacity; room++)
            {
                if (!RequestDue(room) || !RequestSuppliesAvailable(room) ||
                    (StaffUrgentOnly((int)StaffRole.GuestServices) && !RequestUrgent(room)))
                    continue;
                bool urgent = RequestUrgent(room);
                if (target < 0 || urgent && !RequestUrgent(target) ||
                    urgent == RequestUrgent(target) && RequestWait(room) > RequestWait(target))
                    target = room;
            }
            if (target < 0)
                return;
            AcknowledgeRequest(target);
            ServeRequest(target);
        }

        private void WorkHousekeeping(int remaining)
        {
            for (int room = 0; room < RoomCapacity && remaining > 0; room++)
                for (int task = 0; task < CleaningTaskCount && remaining > 0; task++)
                    if ((!StaffUrgentOnly((int)StaffRole.Housekeeper) || BookingDue(room) || HousekeepingRequested(room)) &&
                        CanClean(room, (CleaningTask)task))
                    {
                        Clean(room, (CleaningTask)task);
                        remaining--;
                    }
            StartLaundry();
        }

        private void ReorderPropertySupplies()
        {
            if (PendingOrderCount > 0 || Cash < 18)
                return;
            int cart = 0;
            int budget = Cash - StaffWage;
            for (int item = 0; item < Catalog.Length; item++)
            {
                int wanted = item == 4 ? 1 : item == 2 || item == 3 ? 8 : 3;
                int shortage = wanted - Stock(item);
                SupplyOffer offer = Catalog[item];
                if (shortage <= 0 || budget < offer.Price)
                    continue;
                int packs = Math.Min((shortage + offer.Quantity - 1) / offer.Quantity, budget / offer.Price);
                cart |= packs << (4 * item);
                budget -= packs * offer.Price;
            }
            if (cart != 0)
                OrderCart(cart);
            if (Disinfectant < 2)
                BuyMaintenanceStock(true);
            if (RepairParts < 2)
                BuyMaintenanceStock(false);
        }
    }
}
