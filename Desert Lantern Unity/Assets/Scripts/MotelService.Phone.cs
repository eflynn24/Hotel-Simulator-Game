using Unity.Netcode;

namespace DesertLantern
{
    internal partial struct MotelService
    {
        internal const ulong ReceptionPhoneWorker = ulong.MaxValue - 3;
        private ulong bookingPhoneOwner;
        private int missedBookingMinute;

        public bool CanBookPhoneFor(ulong worker) => CanBookPhone && bookingPhoneOwner == worker;
        public bool CanReturnBookingCall => missedBookingMinute > 0 && !CanBookPhone && !BookingCallRinging;

        public int PhoneArrival => (NextPhoneCall / MinutesPerDay + 1 + NextPhoneCall / 15 % 3) * MinutesPerDay + 15 * 60;
        public int PhoneNights => 1 + NextPhoneCall / 30 % 3;
        public BookingPreference PhonePreference => (BookingPreference)(NextPhoneCall / 15 % 4);

        public bool AcceptPhoneReservation(int room, int callMinute, ulong worker)
        {
            if (!CanBookPhoneFor(worker) || callMinute != NextPhoneCall ||
                !Book(room, PhoneArrival, DepartureForNights(PhoneArrival, PhoneNights), BookingChannel.Telephone, PhonePreference))
                return false;
            FinishPhoneBooking();
            return true;
        }

        public bool TryAnswerPhone(ulong worker)
        {
            if (CanBookPhone)
                return false;

            int room = WaitingRequest(true);
            if (BookingCallRinging && (room < 0 || NextPhoneCall <= requestMinutes[room]))
            {
                answeredCall = NextPhoneCall;
                bookingPhoneOwner = worker;
                return true;
            }
            if (room < 0)
                return false;
            AcknowledgeRequest(room);
            return true;
        }

        public bool DeclinePhone(ulong worker)
        {
            if (CanBookPhone && !CanBookPhoneFor(worker))
                return false;
            int room = WaitingRequest(true);
            if (CanBookPhone || BookingCallRinging && (room < 0 || NextPhoneCall <= requestMinutes[room]))
            {
                MissBookingCall();
                return true;
            }
            if (room < 0)
                return false;
            RecordMissed(requestMinutes[room]);
            AcknowledgeRequest(room);
            return true;
        }

        public bool ReturnBookingCall(ulong worker)
        {
            if (!CanReturnBookingCall)
                return false;
            missedBookingMinute = 0;
            answeredCall = NextPhoneCall = ClockMinute;
            bookingPhoneOwner = worker;
            return true;
        }

        public void ReleasePhone(ulong worker)
        {
            if (CanBookPhoneFor(worker))
                MissBookingCall();
        }

        public void RecoverReceptionSession()
        {
            if (CanBookPhone)
                MissBookingCall();
        }

        private void MissBookingCall()
        {
            missedBookingMinute = NextPhoneCall;
            RecordMissed(NextPhoneCall);
            FinishPhoneBooking();
        }

        private void SerializePhone<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref bookingPhoneOwner);
            serializer.SerializeValue(ref missedBookingMinute);
        }

        private bool PhoneEquals(MotelService other) =>
            bookingPhoneOwner == other.bookingPhoneOwner && missedBookingMinute == other.missedBookingMinute;
    }
}
