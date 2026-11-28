using Unity.Collections;
using Unity.Netcode;

namespace DesertLantern
{
    internal readonly struct SupplyOrder
    {
        public readonly int Number, Due;
        public readonly SupplyDelivery Status;
        public readonly ulong Carrier;

        public SupplyOrder(int number, int due, SupplyDelivery status, ulong carrier = 0)
        {
            Number = number;
            Due = due;
            Status = status;
            Carrier = carrier;
        }
    }

    internal partial struct MotelService
    {
        private struct SupplyParcel
        {
            public int Number, Cart, Due;
            public ulong Carrier;
            public bool Carried;
        }

        private FixedList128Bytes<int> queuedOrderIds;
        private FixedList512Bytes<SupplyParcel> detachedParcels;
        private int PrimaryDeliveryCount => Delivery == SupplyDelivery.None ? 0 : 1;

        public SupplyOrder SupplyOrderAt(int index)
        {
            if (PrimaryDeliveryCount != 0 && index == 0)
                return new SupplyOrder(ActiveOrderNumber, DeliveryDue, Delivery, SupplyCarrier);
            index -= PrimaryDeliveryCount;
            if (index < queuedCarts.Length)
                return new SupplyOrder(QueuedOrderNumber(index), queuedDue[index],
                    queuedDue[index] <= ClockMinute ? SupplyDelivery.AtBay : SupplyDelivery.InTransit);
            SupplyParcel parcel = detachedParcels[index - queuedCarts.Length];
            return new SupplyOrder(parcel.Number, parcel.Due, parcel.Carried ? SupplyDelivery.Carried : SupplyDelivery.AtBay, parcel.Carrier);
        }

        public int CarriedSupplyOrder(ulong worker)
        {
            if (Delivery == SupplyDelivery.Carried && SupplyCarrier == worker)
                return ActiveOrderNumber;
            foreach (var parcel in detachedParcels)
                if (parcel.Carried && parcel.Carrier == worker)
                    return parcel.Number;
            return 0;
        }

        public int NextSupplyPickup(ulong worker)
        {
            if (CarriedSupplyOrder(worker) != 0 || Bag == LuggageStatus.Carried && Carrier == worker)
                return 0;
            int oldest = int.MaxValue;
            for (int i = 0; i < PendingOrderCount; i++)
            {
                SupplyOrder order = SupplyOrderAt(i);
                if (order.Status == SupplyDelivery.AtBay && order.Number < oldest)
                    oldest = order.Number;
            }
            return oldest == int.MaxValue ? 0 : oldest;
        }

        public bool CollectSupplyOrder(int order, ulong worker)
        {
            if (order <= 0 || CarriedSupplyOrder(worker) != 0 || Bag == LuggageStatus.Carried && Carrier == worker)
                return false;
            if (order == ActiveOrderNumber && Delivery == SupplyDelivery.AtBay)
            {
                SupplyCarrier = worker;
                Delivery = SupplyDelivery.Carried;
                return true;
            }
            for (int index = 0; index < detachedParcels.Length; index++)
            {
                var parcel = detachedParcels[index];
                if (parcel.Number != order || parcel.Carried)
                    continue;
                parcel.Carried = true;
                parcel.Carrier = worker;
                detachedParcels[index] = parcel;
                return true;
            }
            for (int index = 0; index < queuedCarts.Length; index++)
            {
                if (QueuedOrderNumber(index) != order || queuedDue[index] > ClockMinute ||
                    detachedParcels.Length >= detachedParcels.Capacity)
                    continue;
                detachedParcels.Add(new SupplyParcel
                {
                    Number = order, Cart = queuedCarts[index], Due = queuedDue[index], Carried = true, Carrier = worker
                });
                RemoveQueuedSupply(index);
                return true;
            }
            return false;
        }

        private bool StoreDetachedSupply(int order, ulong worker)
        {
            for (int index = 0; index < detachedParcels.Length; index++)
            {
                SupplyParcel parcel = detachedParcels[index];
                if (parcel.Number != order || !parcel.Carried || parcel.Carrier != worker)
                    continue;
                StockSupplyOrder(parcel.Number, parcel.Cart);
                detachedParcels.RemoveAt(index);
                return true;
            }
            return false;
        }

        private void ReleaseDetachedSupply(ulong worker, bool all = false)
        {
            for (int index = 0; index < detachedParcels.Length; index++)
            {
                SupplyParcel parcel = detachedParcels[index];
                if (!parcel.Carried || !all && parcel.Carrier != worker)
                    continue;
                parcel.Carrier = 0;
                parcel.Carried = false;
                detachedParcels[index] = parcel;
            }
        }

        public void RecoverSupplyCarriers()
        {
            RecoverSupply(SupplyCarrier);
            ReleaseDetachedSupply(0, all: true);
        }

        private void EnsureQueuedSupplyIds()
        {
            if (queuedOrderIds.Length == queuedCarts.Length)
                return;
            queuedOrderIds.Clear();
            for (int index = 0; index < queuedCarts.Length; index++)
                queuedOrderIds.Add(orderSequence - queuedCarts.Length + index + 1);
        }

        private void RemoveQueuedSupply(int index)
        {
            EnsureQueuedSupplyIds();
            queuedCarts.RemoveAt(index);
            queuedDue.RemoveAt(index);
            queuedOrderIds.RemoveAt(index);
        }

        private void SerializeParallelDeliveries<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            SerializeOrderList(serializer, ref queuedOrderIds);
            byte count = (byte)detachedParcels.Length;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader)
                detachedParcels.Length = count;
            for (int index = 0; index < count; index++)
            {
                SupplyParcel parcel = detachedParcels[index];
                serializer.SerializeValue(ref parcel.Number);
                serializer.SerializeValue(ref parcel.Cart);
                serializer.SerializeValue(ref parcel.Due);
                serializer.SerializeValue(ref parcel.Carrier);
                serializer.SerializeValue(ref parcel.Carried);
                if (serializer.IsReader)
                    detachedParcels[index] = parcel;
            }
        }

        private bool ParallelDeliveriesEqual(MotelService other) =>
            queuedOrderIds.Equals(other.queuedOrderIds) && detachedParcels.Equals(other.detachedParcels);
    }
}
