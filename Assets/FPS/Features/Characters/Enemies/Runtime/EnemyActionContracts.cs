using System;
using UnityEngine;

namespace FPS
{
    public enum EnemyActionType : byte
    {
        None,
        Melee,
        Scream,
        HeavySwing,
        Slam,
        Implant,
        Stagger,
        Death
    }

    [Serializable]
    public struct EnemyActionTiming
    {
        [SerializeField] private EnemyActionType actionType;
        [SerializeField, Min(0f)] private float impactSeconds;
        [SerializeField, Min(0f)] private float motionLockSeconds;
        [SerializeField, Min(0f)] private float presentationSeconds;

        public EnemyActionTiming(
            EnemyActionType actionType,
            float impactSeconds,
            float motionLockSeconds,
            float presentationSeconds)
        {
            this.actionType = actionType;
            this.impactSeconds = Mathf.Max(0f, impactSeconds);
            this.motionLockSeconds = Mathf.Max(this.impactSeconds, motionLockSeconds);
            this.presentationSeconds = Mathf.Max(this.motionLockSeconds, presentationSeconds);
        }

        public EnemyActionType ActionType => actionType;
        public float ImpactSeconds => Mathf.Max(0f, impactSeconds);
        public float MotionLockSeconds => Mathf.Max(ImpactSeconds, motionLockSeconds);
        public float PresentationSeconds => Mathf.Max(MotionLockSeconds, presentationSeconds);
    }

    public readonly struct EnemyMotionLockHandle : IEquatable<EnemyMotionLockHandle>
    {
        internal EnemyMotionLockHandle(uint id) => Id = id;

        internal uint Id { get; }
        public bool IsValid => Id != 0;

        public bool Equals(EnemyMotionLockHandle other) => Id == other.Id;
        public override bool Equals(object obj) => obj is EnemyMotionLockHandle other && Equals(other);
        public override int GetHashCode() => (int)Id;
    }

    public readonly struct EnemyActionState
    {
        public EnemyActionState(
            ushort actionId,
            EnemySpecialActionKind networkKind,
            int startServerTick,
            int endServerTick)
        {
            ActionId = actionId;
            NetworkKind = networkKind;
            StartServerTick = startServerTick;
            EndServerTick = endServerTick;
        }

        public ushort ActionId { get; }
        public EnemySpecialActionKind NetworkKind { get; }
        public int StartServerTick { get; }
        public int EndServerTick { get; }
        public bool IsActiveAt(int serverTick) => NetworkKind != EnemySpecialActionKind.None && serverTick < EndServerTick;
    }

    [Serializable]
    public struct EnemyLocomotionCalibration
    {
        [SerializeField, Min(0.01f)] private float clipStrideSpeed;
        [SerializeField, Min(0.01f)] private float minimumPlaybackRate;
        [SerializeField, Min(0.01f)] private float maximumPlaybackRate;
        [SerializeField, Min(0f)] private float stationarySpeedThreshold;

        public EnemyLocomotionCalibration(
            float clipStrideSpeed,
            float minimumPlaybackRate = 0.75f,
            float maximumPlaybackRate = 1.35f,
            float stationarySpeedThreshold = 0.05f)
        {
            this.clipStrideSpeed = Mathf.Max(0.01f, clipStrideSpeed);
            this.minimumPlaybackRate = Mathf.Max(0.01f, minimumPlaybackRate);
            this.maximumPlaybackRate = Mathf.Max(this.minimumPlaybackRate, maximumPlaybackRate);
            this.stationarySpeedThreshold = Mathf.Max(0f, stationarySpeedThreshold);
        }

        public float ResolvePlaybackRate(float planarSpeed)
        {
            if (planarSpeed <= Mathf.Max(0f, stationarySpeedThreshold))
                return 1f;

            float stride = Mathf.Max(0.01f, clipStrideSpeed);
            float min = Mathf.Max(0.01f, minimumPlaybackRate);
            float max = Mathf.Max(min, maximumPlaybackRate);
            return Mathf.Clamp(planarSpeed / stride, min, max);
        }
    }
}
