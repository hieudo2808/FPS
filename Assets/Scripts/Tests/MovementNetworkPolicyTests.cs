using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;

namespace FPS.Tests
{
    public class MovementNetworkPolicyTests
    {
        [Test]
        public void PlayerMovement_DeclaresSimulationAndSnapshotRates()
        {
            Assert.AreEqual(NetworkGameplayPolicy.SimulationHz, PlayerMovement.SimulationHz);
            Assert.AreEqual(NetworkGameplayPolicy.SnapshotHz, PlayerMovement.SnapshotHz);
            Assert.LessOrEqual(NetworkGameplayPolicy.SimulationHz, 60);
            Assert.AreEqual(30, NetworkGameplayPolicy.HitboxHistoryHz);
            Assert.AreEqual(0.35f, NetworkGameplayPolicy.HitboxHistorySeconds, 0.001f);
            Assert.AreEqual(0.25f, NetworkGameplayPolicy.MaxRewindSeconds, 0.001f);

            var sendEvery = typeof(PlayerMovement).GetField(
                "STATE_SEND_EVERY_N_TICKS",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(sendEvery);
            Assert.AreEqual(NetworkGameplayPolicy.StateSendEveryNTicks, sendEvery.GetRawConstantValue(),
                "Movement should simulate at 60Hz but send snapshots at 30Hz.");
        }

        [Test]
        public void PlayerMovement_HighFrequencyMovementRpcsUseUnreliableDelivery()
        {
            var inputRpc = typeof(PlayerMovement).GetMethod(
                "SendInputServerRpc",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var stateRpc = typeof(PlayerMovement).GetMethod(
                "SendStateClientRpc",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(inputRpc);
            Assert.NotNull(stateRpc);
            Assert.AreEqual(RpcDelivery.Unreliable, inputRpc.GetCustomAttribute<ServerRpcAttribute>().Delivery);
            Assert.AreEqual(RpcDelivery.Unreliable, stateRpc.GetCustomAttribute<ClientRpcAttribute>().Delivery);
        }
    }
}
