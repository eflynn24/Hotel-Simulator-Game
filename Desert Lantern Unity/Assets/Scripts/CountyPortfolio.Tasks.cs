namespace DesertLantern
{
    internal enum PropertyTaskKind { PrepareRooms, CheckIn, ReceiveSupplies }

    internal readonly struct PropertyTask
    {
        public readonly PropertyId Property;
        public readonly PropertyTaskKind Kind;
        public readonly int WorkId;

        public PropertyTask(PropertyId property, PropertyTaskKind kind, int workId = 0)
        {
            Property = property;
            Kind = kind;
            WorkId = workId;
        }

        public int StaffRole => Kind switch
        {
            PropertyTaskKind.PrepareRooms => 2,
            PropertyTaskKind.CheckIn => 5,
            _ => 3
        };
    }

    internal sealed partial class CountyPortfolio
    {
        public bool CompleteTask(PropertyTask task, bool assignedEmployee)
        {
            int slot = MapSlot(task.Property);
            if (slot < 0 || slot == MotelSlot || !Owned(slot) || (assignedEmployee && !HasEmployee(slot, task.StaffRole)))
                return false;
            return task.Kind switch
            {
                PropertyTaskKind.PrepareRooms => PrepareRooms(slot),
                PropertyTaskKind.CheckIn => RunFrontDesk(slot),
                PropertyTaskKind.ReceiveSupplies => ReceiveHotelDelivery(slot, new PropertyDelivery(task.Property, DeliveryDock.SupplyBay, task.WorkId)),
                _ => false
            };
        }

        public bool HotelDeliveryReady(PropertyId property)
        {
            Hotel hotel = Business(property);
            return hotel.deliveryQuantity > 0 && hotel.deliveryDue <= ClockMinute;
        }

        private bool ReceiveHotelDelivery(int slot, PropertyDelivery delivery)
        {
            Hotel hotel = Business(slot);
            if (delivery.Property != hotel.propertyId || delivery.Dock != DeliveryDock.SupplyBay ||
                delivery.Order != hotel.deliveryOrder || !HotelDeliveryReady(hotel.propertyId))
                return false;
            hotel.stock += hotel.deliveryQuantity;
            hotel.deliveryQuantity = hotel.deliveryDue = 0;
            hotel.deliveryNotified = false;
            AddNote(slot, $"Bulk supply order #{delivery.Order} received.");
            return true;
        }

        private bool OrderHotelStock(int slot)
        {
            Hotel hotel = Business(slot);
            int packs = Offers[slot].Rooms;
            int price = 5 * (packs / 2);
            if (hotel.deliveryQuantity > 0 || !Spend(hotel, Account.Supplies, price, "Bulk order placed"))
                return false;
            hotel.deliveryQuantity = packs;
            hotel.deliveryDue = ClockMinute + 60;
            hotel.deliveryOrder++;
            return true;
        }

        private void AdvanceHotelTasks(int slot)
        {
            Hotel hotel = Business(slot);
            if (HotelDeliveryReady(hotel.propertyId) && !hotel.deliveryNotified)
            {
                AddNote(slot, $"Bulk supply order #{hotel.deliveryOrder} arrived at the supply bay.");
                hotel.deliveryNotified = true;
            }
            CompleteTask(new PropertyTask(hotel.propertyId, PropertyTaskKind.ReceiveSupplies, hotel.deliveryOrder), true);
            if (HasEmployee(slot, (int)StaffRole.Purchasing) && hotel.stock < Offers[slot].Rooms / 2)
                OrderHotelStock(slot);
            if (HasEmployee(slot, (int)StaffRole.Housekeeper))
                WashHotelLaundry(hotel);
            CompleteTask(new PropertyTask(hotel.propertyId, PropertyTaskKind.PrepareRooms), true);
            CompleteTask(new PropertyTask(hotel.propertyId, PropertyTaskKind.CheckIn), true);
            if (HasEmployee(slot, (int)StaffRole.Bellhop))
                DeliverHotelLuggage(hotel);
            if (HasEmployee(slot, (int)StaffRole.GuestServices))
                ServeHotelRequests(hotel);
            if (ClockMinute % MotelService.MinutesPerDay == 20 * 60 && hotel.occupied >= 8 &&
                (Day + slot) % 3 == 0)
            {
                hotel.noiseReports++;
                AddNote(slot, "Guests reported late-evening noise.");
            }
            if (hotel.noiseReports > 0 && HasEmployee(slot, (int)StaffRole.Security))
            {
                hotel.noiseReports = 0;
                AddNote(slot, "Security resolved the noise complaints.");
            }
            if (HasEmployee(slot, (int)StaffRole.Technician) && hotel.condition < 55)
                ServiceHotelEquipment(slot);
            if ((hotel.incident == PropertyIncident.SuspiciousActivity && HasEmployee(slot, (int)StaffRole.Security)) ||
                (hotel.incident == PropertyIncident.UtilityEmergency && HasEmployee(slot, (int)StaffRole.Technician)))
                RespondToHotelIncident(slot);
            if (HasEmployee(slot, (int)StaffRole.Manager))
                RunSpecialty(slot);
        }

        private bool RespondToHotelIncident(int slot)
        {
            Hotel hotel = Business(slot);
            if (hotel.incident == PropertyIncident.None && hotel.noiseReports > 0)
            {
                hotel.noiseReports = 0;
                AddNote(slot, "Noise complaints resolved.");
                return true;
            }

            if (hotel.incident == PropertyIncident.None ||
                (hotel.incident == PropertyIncident.UtilityEmergency &&
                    !Spend(hotel, Account.Maintenance, 26, "Emergency utility repair")))
                return false;
            hotel.incident = PropertyIncident.None;
            AddNote(slot, "Incident resolved.");
            return true;
        }

        private bool ServiceHotelEquipment(int slot)
        {
            Hotel hotel = Business(slot);
            if (hotel.condition >= 90 || !Spend(hotel, Account.Maintenance, 24, "Equipment inspection and repair"))
                return false;
            hotel.condition = 100;
            return true;
        }

        private static bool WashHotelLaundry(Hotel hotel)
        {
            int washed = System.Math.Min(8, hotel.dirtyLinen);
            hotel.cleanLinen += washed;
            hotel.dirtyLinen -= washed;
            return washed > 0;
        }

        private static bool DeliverHotelLuggage(Hotel hotel)
        {
            int delivered = System.Math.Min(6, hotel.luggage);
            hotel.luggage -= delivered;
            return delivered > 0;
        }

        private static bool ServeHotelRequests(Hotel hotel)
        {
            int completed = System.Math.Min(4, System.Math.Min(hotel.stock, hotel.serviceRequests));
            hotel.stock -= completed;
            hotel.serviceRequests -= completed;
            hotel.servedRequests += completed;
            return completed > 0;
        }
    }
}
