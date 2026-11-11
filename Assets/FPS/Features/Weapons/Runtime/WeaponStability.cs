using System;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public struct WeaponStabilityContext
    {
        public float normalizedSpeed;
        public bool grounded, sprinting, aimed;
    }

    // BR-Port-v2: project equations with recovered tuning; the native update was unavailable.
    public struct WeaponStability : INetworkSerializable, IEquatable<WeaponStability>
    {
        public float index, firingErrorIndex;
        public float pitchTarget, yawMagnitude;
        public double lastShotTime, yawSwitchStartTime;
        public int acceptedShots, yawFromSign, yawToSign;
        public uint burstSeed;
        public bool hasShot;

        public float RecoveryFraction(RecoilPattern profile, double now, float fireInterval)
        {
            if (!hasShot) return 1f;
            float duration = profile.Evaluate(profile.recoveryTime, index);
            double elapsed = now - (lastShotTime + fireInterval);
            return duration == 0f ? (elapsed < 0 ? 0f : 1f) : Mathf.Clamp01((float)(elapsed / duration));
        }

        public float IndexAt(RecoilPattern profile, double now, float fireInterval)
            => index * (1f - RecoveryFraction(profile, now, fireInterval));

        public Vector2 TargetAt(RecoilPattern profile, double now, float fireInterval)
            => new Vector2(pitchTarget, yawMagnitude * YawDirectionAt(profile, now))
                * (1f - RecoveryFraction(profile, now, fireInterval));

        public float YawDirectionAt(RecoilPattern profile, double now)
            => Mathf.Lerp(yawFromSign, yawToSign,
                Mathf.Clamp01((float)((now - yawSwitchStartTime) / profile.yawSwitchDuration)));

        public float Fire(RecoilPattern profile, double now, float fireInterval,
            WeaponStabilityContext context, uint shotSeed)
        {
            float recovery = RecoveryFraction(profile, now, fireInterval);
            float previous = index * (1f - recovery);
            float previousError = firingErrorIndex * (1f - recovery);
            if (recovery >= 1f)
            {
                burstSeed = shotSeed;
                yawFromSign = yawToSign = Noise(shotSeed) < 0.5f ? -1 : 1;
                yawSwitchStartTime = now;
                acceptedShots = 0;
            }
            acceptedShots++;
            bool transitioning = yawFromSign != yawToSign
                && now < yawSwitchStartTime + profile.yawSwitchDuration;
            // shortcut: eligible-shot RNG replaces the unknown native yaw accumulator; tune against recorded sprays.
            if (acceptedShots > profile.protectedShots && !transitioning
                && Noise(shotSeed ^ burstSeed ^ 0xA511E9B3u) < profile.yawSwitchChance)
            {
                yawFromSign = yawToSign;
                yawToSign = -yawFromSign;
                yawSwitchStartTime = now;
            }
            float next = Mathf.Min(previous + 1f, profile.MaximumStabilityIndex);
            Vector2 multiplier = Vector2.Scale(context.sprinting ? profile.sprintRecoilMultiplier : Vector2.one,
                context.aimed ? profile.adsRecoilMultiplier : Vector2.one);
            pitchTarget = pitchTarget * (1f - recovery)
                + (profile.Evaluate(profile.pitchEnvelope, next) - profile.Evaluate(profile.pitchEnvelope, previous)) * multiplier.x;
            yawMagnitude = Mathf.Max(0f, yawMagnitude * (1f - recovery)
                + (profile.Evaluate(profile.yawEnvelope, next) - profile.Evaluate(profile.yawEnvelope, previous)) * multiplier.y);
            float spread = profile.GetSpread(previousError, context);
            float errorStep = recovery >= 1f ? 1f : (1f - recovery) + recovery / profile.tapEfficiency;
            firingErrorIndex = Mathf.Min(previousError + errorStep, profile.MaximumStabilityIndex);
            index = next;
            lastShotTime = now;
            hasShot = true;
            return spread;
        }

        private static float Noise(uint value)
        {
            value ^= value >> 16; value *= 0x7FEB352Du;
            value ^= value >> 15; value *= 0x846CA68Bu; value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777216f;
        }

        public bool Equals(WeaponStability other) => index.Equals(other.index)
            && firingErrorIndex.Equals(other.firingErrorIndex) && pitchTarget.Equals(other.pitchTarget)
            && yawMagnitude.Equals(other.yawMagnitude) && lastShotTime.Equals(other.lastShotTime)
            && yawSwitchStartTime.Equals(other.yawSwitchStartTime) && acceptedShots == other.acceptedShots
            && yawFromSign == other.yawFromSign && yawToSign == other.yawToSign
            && burstSeed == other.burstSeed && hasShot == other.hasShot;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref index);
            serializer.SerializeValue(ref firingErrorIndex);
            serializer.SerializeValue(ref pitchTarget);
            serializer.SerializeValue(ref yawMagnitude);
            serializer.SerializeValue(ref lastShotTime);
            serializer.SerializeValue(ref yawSwitchStartTime);
            serializer.SerializeValue(ref acceptedShots);
            serializer.SerializeValue(ref yawFromSign);
            serializer.SerializeValue(ref yawToSign);
            serializer.SerializeValue(ref burstSeed);
            serializer.SerializeValue(ref hasShot);
        }
    }
}
