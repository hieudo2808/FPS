using NUnit.Framework;
using UnityEngine;

namespace FPS.Tests
{
    public sealed class AmmoPickupRulesTests
    {
        [Test]
        public void WholePacksRejectFullPartialInvalidAndOverflowWithoutChangingReserve()
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            try
            {
                data.magazineSize = 10;
                data.totalAmmo = 40;
                Assert.That(data.ReserveCapacity, Is.EqualTo(30));
                data.maxReserveAmmo = 60;
                var state = new WeaponServerState();
                state.EnsureInitialized(1, data);
                Assert.That(state.AddReserveAmmo(20, data.ReserveCapacity), Is.True);
                Assert.That(state.ReserveAmmo, Is.EqualTo(50));
                foreach (int amount in new[] { -1, 0, 11, int.MaxValue })
                {
                    Assert.That(state.AddReserveAmmo(amount, data.ReserveCapacity), Is.False);
                    Assert.That(state.ReserveAmmo, Is.EqualTo(50));
                }
                Assert.That(state.AddReserveAmmo(10, data.ReserveCapacity), Is.True);
                Assert.That(state.AddReserveAmmo(1, data.ReserveCapacity), Is.False);
                Assert.That(state.AddReserveAmmo(int.MaxValue), Is.False);
                Assert.That(state.ReserveAmmo, Is.EqualTo(60));
                var restored = new WeaponServerState();
                restored.Restore(state.Capture(1, data), 1);
                Assert.That(restored.ReserveAmmo, Is.EqualTo(60));
                Assert.That(restored.CanReceiveAmmo(1, data.ReserveCapacity), Is.False);
            }
            finally { Object.DestroyImmediate(data); }
        }
    }
}
