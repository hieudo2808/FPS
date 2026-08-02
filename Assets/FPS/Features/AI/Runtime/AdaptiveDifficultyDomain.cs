using System;
using System.Collections.Generic;

namespace FPS
{
    /// <summary>
    /// The five pacing states described by the GDD. This type has no Unity or networking
    /// dependency so the transition rules can be tested deterministically.
    /// </summary>
    public enum DirectorPhase
    {
        Calm,
        BuildUp,
        Combat,
        Peak,
        Relax
    }

    public readonly struct DirectorInput
    {
        public readonly float WeakestHealth01;
        public readonly float TeamSeparation01;
        public readonly float IdleSeconds;
        public readonly float RecentDownedSeconds;
        public readonly int CurrentAlive;
        public readonly float Intensity;

        public DirectorInput(
            float weakestHealth01,
            float teamSeparation01,
            float idleSeconds,
            float recentDownedSeconds,
            int currentAlive,
            float intensity)
        {
            WeakestHealth01 = Clamp01(weakestHealth01);
            TeamSeparation01 = Clamp01(teamSeparation01);
            IdleSeconds = Math.Max(0f, idleSeconds);
            RecentDownedSeconds = recentDownedSeconds;
            CurrentAlive = Math.Max(0, currentAlive);
            Intensity = Math.Max(0f, intensity);
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }

    public readonly struct DirectorPolicy
    {
        public readonly float CalmDurationSeconds;
        public readonly float BuildUpDurationSeconds;
        public readonly float CombatDurationSeconds;
        public readonly float PeakDurationSeconds;
        public readonly float RelaxDurationSeconds;
        public readonly float PeakIntensityThreshold;
        public readonly float WeakestHealthFloor01;
        public readonly float RecentDownedGraceSeconds;
        public readonly float SpecialCooldownSeconds;
        public readonly float IdleBuildUpDelay;
        public readonly float SeparationBuildUpDelay;

        public DirectorPolicy(
            float calmDurationSeconds,
            float buildUpDurationSeconds,
            float combatDurationSeconds,
            float peakDurationSeconds,
            float relaxDurationSeconds,
            float peakIntensityThreshold,
            float weakestHealthFloor01,
            float recentDownedGraceSeconds,
            float specialCooldownSeconds,
            float idleBuildUpDelay,
            float separationBuildUpDelay)
        {
            CalmDurationSeconds = Math.Max(0f, calmDurationSeconds);
            BuildUpDurationSeconds = Math.Max(0f, buildUpDurationSeconds);
            CombatDurationSeconds = Math.Max(0f, combatDurationSeconds);
            PeakDurationSeconds = Math.Max(0f, peakDurationSeconds);
            RelaxDurationSeconds = Math.Max(0f, relaxDurationSeconds);
            PeakIntensityThreshold = Math.Max(0f, peakIntensityThreshold);
            WeakestHealthFloor01 = Clamp01(weakestHealthFloor01);
            RecentDownedGraceSeconds = Math.Max(0f, recentDownedGraceSeconds);
            SpecialCooldownSeconds = Math.Max(0f, specialCooldownSeconds);
            IdleBuildUpDelay = Math.Max(0f, idleBuildUpDelay);
            SeparationBuildUpDelay = Math.Max(0f, separationBuildUpDelay);
        }

        public static DirectorPolicy Default => new DirectorPolicy(
            calmDurationSeconds: 3f,
            buildUpDurationSeconds: 15f,
            combatDurationSeconds: 60f,
            peakDurationSeconds: 15f,
            relaxDurationSeconds: 20f,
            peakIntensityThreshold: 80f,
            weakestHealthFloor01: 0.2f,
            recentDownedGraceSeconds: 20f,
            specialCooldownSeconds: 15f,
            idleBuildUpDelay: 0.25f,
            separationBuildUpDelay: 0.25f);

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }

    public readonly struct DirectorDecision
    {
        public readonly DirectorPhase Phase;
        public readonly float SpawnRateMultiplier;
        public readonly bool SpecialGateOpen;
        public readonly bool ProtectWeakestPlayer;
        public readonly float TeamSeparation01;
        public readonly float IdleSeconds;

        public DirectorDecision(
            DirectorPhase phase,
            float spawnRateMultiplier,
            bool specialGateOpen,
            bool protectWeakestPlayer,
            float teamSeparation01,
            float idleSeconds)
        {
            Phase = phase;
            SpawnRateMultiplier = spawnRateMultiplier;
            SpecialGateOpen = specialGateOpen;
            ProtectWeakestPlayer = protectWeakestPlayer;
            TeamSeparation01 = teamSeparation01;
            IdleSeconds = idleSeconds;
        }
    }

    public readonly struct DirectorStepResult
    {
        public readonly DirectorPhase Phase;
        public readonly bool PhaseChanged;
        public readonly bool EnteredRelax;
        public readonly DirectorDecision Decision;

        public DirectorStepResult(
            DirectorPhase phase,
            bool phaseChanged,
            bool enteredRelax,
            DirectorDecision decision)
        {
            Phase = phase;
            PhaseChanged = phaseChanged;
            EnteredRelax = enteredRelax;
            Decision = decision;
        }
    }

    public sealed class DirectorStateMachine
    {
        private readonly DirectorPolicy policy;
        private DirectorPhase phase;
        private float phaseElapsed;
        private DirectorInput lastInput;

        public DirectorStateMachine(DirectorPolicy policy)
        {
            this.policy = policy;
            phase = DirectorPhase.Calm;
            phaseElapsed = 0f;
        }

        public DirectorPhase Phase => phase;
        public float PhaseElapsedSeconds => phaseElapsed;

        public DirectorStepResult Advance(float deltaSeconds, DirectorInput input)
        {
            lastInput = input;
            phaseElapsed += Math.Max(0f, deltaSeconds);

            bool phaseChanged = false;
            bool enteredRelax = false;

            if (ShouldProtectTeam(input) && phase != DirectorPhase.Calm && phase != DirectorPhase.Relax)
            {
                phaseChanged = true;
                enteredRelax = true;
                TransitionTo(DirectorPhase.Relax);
            }
            else
            {
                switch (phase)
                {
                    case DirectorPhase.Calm:
                        if (phaseElapsed >= policy.CalmDurationSeconds)
                        {
                            phaseChanged = true;
                            TransitionTo(DirectorPhase.BuildUp);
                        }
                        break;

                    case DirectorPhase.BuildUp:
                        if (phaseElapsed >= GetBuildUpDuration(input))
                        {
                            phaseChanged = true;
                            TransitionTo(DirectorPhase.Combat);
                        }
                        break;

                    case DirectorPhase.Combat:
                        if (input.Intensity >= policy.PeakIntensityThreshold
                            || phaseElapsed >= policy.CombatDurationSeconds)
                        {
                            phaseChanged = true;
                            TransitionTo(DirectorPhase.Peak);
                        }
                        break;

                    case DirectorPhase.Peak:
                        if (phaseElapsed >= policy.PeakDurationSeconds)
                        {
                            phaseChanged = true;
                            enteredRelax = true;
                            TransitionTo(DirectorPhase.Relax);
                        }
                        break;

                    case DirectorPhase.Relax:
                        if (phaseElapsed >= policy.RelaxDurationSeconds)
                        {
                            phaseChanged = true;
                            TransitionTo(DirectorPhase.Calm);
                        }
                        break;
                }
            }

            return new DirectorStepResult(
                phase,
                phaseChanged,
                enteredRelax,
                GetDecision());
        }

        public DirectorDecision GetDecision()
        {
            float spawnRateMultiplier;
            switch (phase)
            {
                case DirectorPhase.Calm:
                    spawnRateMultiplier = 0.05f;
                    break;
                case DirectorPhase.BuildUp:
                    spawnRateMultiplier = 0.5f;
                    break;
                case DirectorPhase.Peak:
                    spawnRateMultiplier = 1.5f;
                    break;
                case DirectorPhase.Relax:
                    spawnRateMultiplier = 0f;
                    break;
                default:
                    spawnRateMultiplier = 1f;
                    break;
            }

            return new DirectorDecision(
                phase,
                spawnRateMultiplier,
                phase == DirectorPhase.Peak && phaseElapsed >= policy.SpecialCooldownSeconds,
                lastInput.WeakestHealth01 <= policy.WeakestHealthFloor01,
                lastInput.TeamSeparation01,
                lastInput.IdleSeconds);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void SetStateForTests(DirectorPhase nextPhase, float elapsedSeconds)
        {
            phase = nextPhase;
            phaseElapsed = Math.Max(0f, elapsedSeconds);
        }
#endif

        private bool ShouldProtectTeam(DirectorInput input)
        {
            return input.WeakestHealth01 <= policy.WeakestHealthFloor01
                || (input.RecentDownedSeconds >= 0f
                    && input.RecentDownedSeconds <= policy.RecentDownedGraceSeconds);
        }

        private float GetBuildUpDuration(DirectorInput input)
        {
            float idle01 = Math.Min(1f, input.IdleSeconds / 20f);
            return policy.BuildUpDurationSeconds
                * (1f + idle01 * policy.IdleBuildUpDelay + input.TeamSeparation01 * policy.SeparationBuildUpDelay);
        }

        private void TransitionTo(DirectorPhase nextPhase)
        {
            phase = nextPhase;
            phaseElapsed = 0f;
        }
    }

    public readonly struct PlayerPerformanceSample
    {
        public readonly ulong StablePlayerId;
        public readonly float HeadshotRatio;
        public readonly float DamageTakenNorm;
        public readonly float DownedCountNorm;
        public readonly float AmmoEfficiency;
        public readonly int ShotsFired;
        public readonly int Kills;
        public readonly int DownedCount;

        private PlayerPerformanceSample(
            ulong stablePlayerId,
            float headshotRatio,
            float damageTakenNorm,
            float downedCountNorm,
            float ammoEfficiency,
            int shotsFired,
            int kills,
            int downedCount)
        {
            StablePlayerId = stablePlayerId;
            HeadshotRatio = Clamp01(headshotRatio);
            DamageTakenNorm = Clamp01(damageTakenNorm);
            DownedCountNorm = Clamp01(downedCountNorm);
            AmmoEfficiency = Clamp01(ammoEfficiency);
            ShotsFired = Math.Max(0, shotsFired);
            Kills = Math.Max(0, kills);
            DownedCount = Math.Max(0, downedCount);
        }

        public static PlayerPerformanceSample Create(
            ulong stablePlayerId,
            float headshotRatio,
            float damageTakenNorm,
            float downedCountNorm,
            float ammoEfficiency,
            int shotsFired,
            int kills,
            int downedCount)
        {
            return new PlayerPerformanceSample(
                stablePlayerId,
                headshotRatio,
                damageTakenNorm,
                downedCountNorm,
                ammoEfficiency,
                shotsFired,
                kills,
                downedCount);
        }

        public bool HasEvidence(DynamicDifficultyPolicy policy)
        {
            return ShotsFired >= policy.MinimumShots
                && Kills >= policy.MinimumKills;
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }

    public readonly struct DynamicDifficultyPolicy
    {
        public readonly float HeadshotWeight;
        public readonly float DamageTakenWeight;
        public readonly float DownedWeight;
        public readonly float AmmoEfficiencyWeight;
        public readonly float NeutralScore;
        public readonly float Gain;
        public readonly float GlobalMinMultiplier;
        public readonly float GlobalMaxMultiplier;
        public readonly float MaxStepPerRelax;
        public readonly int MinimumShots;
        public readonly int MinimumKills;
        public readonly float WeakestPlayerWeight;
        public readonly float MedianPlayerWeight;
        public readonly float WeakestPlayerCeilingOffset;

        public DynamicDifficultyPolicy(
            float headshotWeight,
            float damageTakenWeight,
            float downedWeight,
            float ammoEfficiencyWeight,
            float neutralScore,
            float gain,
            float globalMinMultiplier,
            float globalMaxMultiplier,
            float maxStepPerRelax,
            int minimumShots,
            int minimumKills,
            float weakestPlayerWeight,
            float medianPlayerWeight,
            float weakestPlayerCeilingOffset = 0.25f)
        {
            HeadshotWeight = Math.Max(0f, headshotWeight);
            DamageTakenWeight = Math.Max(0f, damageTakenWeight);
            DownedWeight = Math.Max(0f, downedWeight);
            AmmoEfficiencyWeight = Math.Max(0f, ammoEfficiencyWeight);
            NeutralScore = Clamp01(neutralScore);
            Gain = Math.Max(0f, gain);
            GlobalMinMultiplier = Math.Min(globalMinMultiplier, globalMaxMultiplier);
            GlobalMaxMultiplier = Math.Max(globalMinMultiplier, globalMaxMultiplier);
            MaxStepPerRelax = Math.Max(0f, maxStepPerRelax);
            MinimumShots = Math.Max(0, minimumShots);
            MinimumKills = Math.Max(0, minimumKills);
            WeakestPlayerWeight = Clamp01(weakestPlayerWeight);
            MedianPlayerWeight = Clamp01(medianPlayerWeight);
            WeakestPlayerCeilingOffset = Math.Max(0f, weakestPlayerCeilingOffset);
        }

        public static DynamicDifficultyPolicy Default => new DynamicDifficultyPolicy(
            headshotWeight: 0.20f,
            damageTakenWeight: 0.25f,
            downedWeight: 0.30f,
            ammoEfficiencyWeight: 0.25f,
            neutralScore: 0.5f,
            gain: 0.30f,
            globalMinMultiplier: 0.6f,
            globalMaxMultiplier: 1.5f,
            maxStepPerRelax: 0.10f,
            minimumShots: 20,
            minimumKills: 8,
            weakestPlayerWeight: 0.40f,
            medianPlayerWeight: 0.60f,
            weakestPlayerCeilingOffset: 0.25f);

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }

    public readonly struct DynamicDifficultyEvaluation
    {
        public readonly float Multiplier;
        public readonly float TargetMultiplier;
        public readonly float TeamPerformanceScore;
        public readonly bool HasEvidence;
        public readonly bool Updated;

        public DynamicDifficultyEvaluation(
            float multiplier,
            float targetMultiplier,
            float teamPerformanceScore,
            bool hasEvidence,
            bool updated)
        {
            Multiplier = multiplier;
            TargetMultiplier = targetMultiplier;
            TeamPerformanceScore = teamPerformanceScore;
            HasEvidence = hasEvidence;
            Updated = updated;
        }
    }

    public sealed class DynamicDifficultyEvaluator
    {
        private readonly DynamicDifficultyPolicy policy;
        private float currentMultiplier = 1f;

        public DynamicDifficultyEvaluator(DynamicDifficultyPolicy policy)
        {
            this.policy = policy;
        }

        public float CurrentMultiplier => currentMultiplier;

        public DynamicDifficultyEvaluation Evaluate(
            DifficultyLevel difficulty,
            IReadOnlyList<PlayerPerformanceSample> samples,
            bool relaxBoundary)
        {
            if (!relaxBoundary)
                return new DynamicDifficultyEvaluation(currentMultiplier, currentMultiplier, 0.5f, false, false);

            List<float> playerScores = new List<float>();
            if (samples != null)
            {
                for (int i = 0; i < samples.Count; i++)
                {
                    PlayerPerformanceSample sample = samples[i];
                    if (sample.HasEvidence(policy))
                        playerScores.Add(GetPlayerScore(sample));
                }
            }

            if (playerScores.Count == 0)
                return new DynamicDifficultyEvaluation(currentMultiplier, currentMultiplier, 0.5f, false, false);

            playerScores.Sort();
            float weakest = playerScores[0];
            float median = GetMedian(playerScores);
            float weightedTeamScore = Clamp01(
                median * policy.MedianPlayerWeight
                + weakest * policy.WeakestPlayerWeight);
            float teamScore = Clamp01(Math.Min(
                weightedTeamScore,
                weakest + policy.WeakestPlayerCeilingOffset));

            float rawTarget = 1f + policy.Gain * (teamScore - policy.NeutralScore);
            GetTierEnvelope(difficulty, out float tierMin, out float tierMax);
            float target = Clamp(rawTarget,
                Math.Max(policy.GlobalMinMultiplier, tierMin),
                Math.Min(policy.GlobalMaxMultiplier, tierMax));

            currentMultiplier = Clamp(currentMultiplier,
                Math.Max(policy.GlobalMinMultiplier, tierMin),
                Math.Min(policy.GlobalMaxMultiplier, tierMax));
            float next = MoveTowards(currentMultiplier, target, policy.MaxStepPerRelax);
            bool updated = Math.Abs(next - currentMultiplier) > 0.00001f;
            currentMultiplier = next;

            return new DynamicDifficultyEvaluation(
                currentMultiplier,
                target,
                teamScore,
                hasEvidence: true,
                updated: updated);
        }

        public void Reset()
        {
            currentMultiplier = 1f;
        }

        private float GetPlayerScore(PlayerPerformanceSample sample)
        {
            float score = sample.HeadshotRatio * policy.HeadshotWeight
                + (1f - sample.DamageTakenNorm) * policy.DamageTakenWeight
                + (1f - sample.DownedCountNorm) * policy.DownedWeight
                + sample.AmmoEfficiency * policy.AmmoEfficiencyWeight;

            float totalWeight = policy.HeadshotWeight
                + policy.DamageTakenWeight
                + policy.DownedWeight
                + policy.AmmoEfficiencyWeight;
            return totalWeight > 0f ? Clamp01(score / totalWeight) : policy.NeutralScore;
        }

        private static float GetMedian(List<float> sortedValues)
        {
            int middle = sortedValues.Count / 2;
            if ((sortedValues.Count & 1) == 1)
                return sortedValues[middle];

            return (sortedValues[middle - 1] + sortedValues[middle]) * 0.5f;
        }

        private static void GetTierEnvelope(DifficultyLevel level, out float min, out float max)
        {
            switch (level)
            {
                case DifficultyLevel.Easy:
                    min = 0.6f;
                    max = 1f;
                    break;
                case DifficultyLevel.Hard:
                    min = 0.9f;
                    max = 1.25f;
                    break;
                case DifficultyLevel.Pandemonium:
                    min = 1f;
                    max = 1.5f;
                    break;
                default:
                    min = 0.85f;
                    max = 1.15f;
                    break;
            }
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
                return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        private static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }
    }
}
