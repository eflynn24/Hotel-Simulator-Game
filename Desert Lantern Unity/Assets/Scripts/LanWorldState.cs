using System;
using Unity.Netcode;
using UnityEngine;

namespace DesertLantern
{
    internal struct LanPose : INetworkSerializable, IEquatable<LanPose>
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Speed;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Speed);
        }

        public bool Equals(LanPose other) =>
            Position.Equals(other.Position) && Rotation.Equals(other.Rotation) && Speed.Equals(other.Speed);
    }

    internal struct LanMotel : INetworkSerializable, IEquatable<LanMotel>
    {
        public int Occupied;
        public bool GuestPresent;
        public bool Escorting;
        public LanPose Guest;
        public MotelService Service;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Occupied);
            serializer.SerializeValue(ref GuestPresent);
            serializer.SerializeValue(ref Escorting);
            Guest.NetworkSerialize(serializer);
            Service.NetworkSerialize(serializer);
        }

        public bool Equals(LanMotel other) => Occupied == other.Occupied && GuestPresent == other.GuestPresent &&
            Escorting == other.Escorting && Guest.Equals(other.Guest) && Service.Equals(other.Service);
    }

    internal struct LanPlayer : INetworkSerializable, IEquatable<LanPlayer>
    {
        public ulong Id;
        public byte Slot;
        public LanPose Pose;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Slot);
            Pose.NetworkSerialize(serializer);
        }

        public bool Equals(LanPlayer other) => Id == other.Id && Slot == other.Slot && Pose.Equals(other.Pose);
    }

    public sealed class LanWorldState : NetworkBehaviour
    {
        internal NetworkList<LanPlayer> Players;
        internal readonly NetworkVariable<LanMotel> Motel = new NetworkVariable<LanMotel>(new LanMotel { Service = MotelService.New() });

        private void Awake() => Players = new NetworkList<LanPlayer>();

        public override void OnNetworkSpawn()
        {
            DontDestroyOnLoad(gameObject);
            LanSession.Active?.Attach(this);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        internal void MoveRpc(Vector2 direction, bool run, RpcParams rpc = default) =>
            LanSession.Active?.MovementReceived?.Invoke(rpc.Receive.SenderClientId, direction, run);

        [Rpc(SendTo.Server)]
        internal void ActionRpc(byte action, Vector3 position, RpcParams rpc = default) =>
            LanSession.Active?.ActionReceived?.Invoke(rpc.Receive.SenderClientId, action, position);
    }
}
