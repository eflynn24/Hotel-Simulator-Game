namespace DesertLantern
{
    // These values are part of the save format. Map order can change independently.
    internal enum PropertyId : ushort
    {
        None = 0,
        RoadsideMotel = 101,
        BudgetHotel = 205,
        BusinessHotel = 310,
        BoutiqueHotel = 420,
        LuxuryHotel = 550
    }

    internal enum PropertyLayout { Roadside, BudgetLobby, Dashboard }

    internal readonly struct PropertyRoom
    {
        public readonly PropertyId Property;
        public readonly int Number;

        public PropertyRoom(PropertyId property, int number)
        {
            Property = property;
            Number = number;
        }
    }

    internal readonly struct PropertyReservation
    {
        public readonly PropertyRoom Room;
        public readonly int Index;
        public readonly int Arrival;

        public PropertyReservation(PropertyRoom room, int index, int arrival)
        {
            Room = room;
            Index = index;
            Arrival = arrival;
        }
    }

    internal readonly struct PropertyRequest
    {
        public readonly PropertyRoom Room;
        public readonly int Guest, ScheduledMinute;

        public PropertyRequest(PropertyRoom room, int guest, int scheduledMinute)
        {
            Room = room;
            Guest = guest;
            ScheduledMinute = scheduledMinute;
        }
    }

    internal readonly struct PropertyEmployee
    {
        public readonly PropertyId Property;
        public readonly int Role;

        public PropertyEmployee(PropertyId property, int role)
        {
            Property = property;
            Role = role;
        }
    }

    internal enum DeliveryDock : byte { SupplyBay }

    internal readonly struct PropertyDelivery
    {
        public readonly PropertyId Property;
        public readonly DeliveryDock Dock;
        public readonly int Order;

        public PropertyDelivery(PropertyId property, DeliveryDock dock, int order)
        {
            Property = property;
            Dock = dock;
            Order = order;
        }
    }
}
