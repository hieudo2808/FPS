using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FPS.Tests
{
    public class PlayerMovementInputSecurityTests
    {
        [Test]
        public void SanitizeInput_ClampsMoveMagnitude()
        {
            var input = new PlayerInputPayload
            {
                tick = 10,
                move = new Vector2(5f, 0f),
                yaw = 45f
            };

            bool accepted = PlayerMovement.TrySanitizeInput(input, 10, out PlayerInputPayload sanitized);

            Assert.True(accepted);
            Assert.LessOrEqual(sanitized.move.magnitude, 1.001f);
        }

        [Test]
        public void SanitizeInput_RejectsNaNAndInfinity()
        {
            var badMove = new PlayerInputPayload { tick = 10, move = new Vector2(float.NaN, 0f), yaw = 0f };
            var badYaw = new PlayerInputPayload { tick = 10, move = Vector2.zero, yaw = float.PositiveInfinity };

            Assert.False(PlayerMovement.TrySanitizeInput(badMove, 10, out _));
            Assert.False(PlayerMovement.TrySanitizeInput(badYaw, 10, out _));
        }

        [Test]
        public void SanitizeInput_RejectsTicksOutsideServerWindow()
        {
            var oldInput = new PlayerInputPayload { tick = -200, move = Vector2.zero, yaw = 0f };
            var farFutureInput = new PlayerInputPayload { tick = 5000, move = Vector2.zero, yaw = 0f };

            Assert.False(PlayerMovement.TrySanitizeInput(oldInput, 100, out _));
            Assert.False(PlayerMovement.TrySanitizeInput(farFutureInput, 100, out _));
        }

        [Test]
        public void SanitizeInput_NormalizesYaw()
        {
            var input = new PlayerInputPayload
            {
                tick = 10,
                move = Vector2.zero,
                yaw = 725f
            };

            bool accepted = PlayerMovement.TrySanitizeInput(input, 10, out PlayerInputPayload sanitized);

            Assert.True(accepted);
            Assert.GreaterOrEqual(sanitized.yaw, 0f);
            Assert.Less(sanitized.yaw, 360f);
        }

        [Test]
        public void InputPayload_RoundTripPreservesAimBit()
        {
            var expected = new PlayerInputPayload
            {
                sequence = 42,
                tick = 99,
                move = new Vector2(0.25f, -0.5f),
                aim = true,
                yaw = 127f,
                pitch = -13f
            };

            using var writer = new FastBufferWriter(64, Allocator.Temp);
            writer.WriteNetworkSerializable(expected);

            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out PlayerInputPayload actual);

            Assert.True(actual.aim);
            Assert.AreEqual(expected.sequence, actual.sequence);
            Assert.AreEqual(expected.tick, actual.tick);
        }

        [Test]
        public void StatePayload_RoundTripPreservesRemoteAimBit()
        {
            var expected = new PlayerStatePayload
            {
                tick = 101,
                lastProcessedCommand = 43,
                position = new Vector3(1f, 2f, 3f),
                grounded = true,
                aiming = true,
                yaw = 215f
            };

            using var writer = new FastBufferWriter(64, Allocator.Temp);
            writer.WriteNetworkSerializable(expected);

            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out PlayerStatePayload actual);

            Assert.True(actual.aiming);
            Assert.AreEqual(expected.tick, actual.tick);
            Assert.AreEqual(expected.lastProcessedCommand, actual.lastProcessedCommand);
        }
    }
}
