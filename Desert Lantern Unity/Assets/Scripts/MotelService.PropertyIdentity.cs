using Unity.Netcode;

namespace DesertLantern
{
    internal partial struct MotelService
    {
        private PropertyId property;
        public PropertyId Property => property;

        public PropertyRoom RoomReference(int room) => new PropertyRoom(property, room);

        public PropertyReservation ReservationReference(int index) =>
            new PropertyReservation(RoomReference(ReservationRoom(index)), index, ReservationStart(index));

        public PropertyRequest RequestReference(int room) => new PropertyRequest(RoomReference(room), GuestId(room), requestMinutes[room]);

        public PropertyDelivery DeliveryReference => new PropertyDelivery(property, DeliveryDock.SupplyBay, ActiveOrderNumber);

        public bool OwnsRoom(PropertyRoom room) => room.Property == property && RoomBuilt(room.Number);

        public bool BookRoom(PropertyRoom room, int start, int end, BookingChannel channel, BookingPreference preference) =>
            OwnsRoom(room) && Book(room.Number, start, end, channel, preference);

        public bool ChangeReservation(PropertyReservation reservation, int operation)
        {
            int index = reservation.Index;
            if (!OwnsRoom(reservation.Room) || (uint)index >= ReservationCount ||
                ReservationRoom(index) != reservation.Room.Number || ReservationStart(index) != reservation.Arrival)
                return false;
            return ModifyBooking(index, operation);
        }

        public bool ApplyGuestRequest(PropertyRequest request, bool cancel)
        {
            int room = request.Room.Number;
            if (!OwnsRoom(request.Room) || !RequestOpen(room) || (GuestId(room) != request.Guest || requestMinutes[room] != request.ScheduledMinute))
                return false;
            if (cancel)
                CancelRequest(room);
            else
                ServeRequest(room);
            return true;
        }

        public bool UpdateEmployee(PropertyEmployee employee, bool priority)
        {
            if (employee.Property != property || (uint)employee.Role >= CountyPortfolio.StaffRoles.Length)
                return false;
            ManageStaff(employee.Role, priority);
            return true;
        }

        public bool OwnsDelivery(PropertyDelivery delivery) =>
            Delivery != SupplyDelivery.None && delivery.Property == property &&
            delivery.Dock == DeliveryDock.SupplyBay && delivery.Order == ActiveOrderNumber;

        // The saved record owns the property ID. Keep the original payload readable
        // so an ordinal-keyed save can move into the new envelope without data loss.
        internal struct SavedState : INetworkSerializable
        {
            public MotelService Service;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                Service.SerializeState(serializer);
                if (serializer.IsWriter)
                {
                    Service.SerializeDecisions(serializer);
                    Service.SerializePhone(serializer);
                    Service.SerializeBookingDetails(serializer);
                }
                else
                {
                    FastBufferReader reader = serializer.GetFastBufferReader();
                    if (reader.Position < reader.Length)
                        Service.SerializeDecisions(serializer);
                    if (reader.Position < reader.Length)
                        Service.SerializePhone(serializer);
                    if (reader.Position < reader.Length)
                        Service.SerializeBookingDetails(serializer);
                    else
                        Service.stayBookingDetails.Length = RoomCount;
                }
            }

            public MotelService ForProperty(PropertyId id)
            {
                Service.property = id;
                return Service;
            }
        }
    }
}
