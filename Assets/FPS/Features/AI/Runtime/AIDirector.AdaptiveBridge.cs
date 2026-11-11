using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Unity.Profiling;

namespace FPS
{
    public partial class AIDirector
    {
        private static readonly ProfilerMarker PressureMarker = new ProfilerMarker("FPS.Director.Pressure");
        private static readonly ProfilerMarker DirectorMarker = new ProfilerMarker("FPS.Director.Update");
        private double lastDamageTime = double.NaN;
        public float ProtectionLatencyMilliseconds { get; private set; } = -1;
        private readonly DirectorPressure pressure = new DirectorPressure();
        private readonly List<ActorPressureSnapshot> actorPressure = new List<ActorPressureSnapshot>(4);
        private readonly Dictionary<ulong, PlayerHealth> pressureActors = new Dictionary<ulong, PlayerHealth>(4);
        private readonly string runId = Guid.NewGuid().ToString("N");
        private int attemptId = 1, encounterId;
        private double nextPressureSample;
        private bool pressureDirty, needsFreshWindow;
        private DirectorInput pressureInput = new DirectorInput(0, 0, 0, 999, 0, 0, participantCount: 0);
        private DirectorDecisionReason boundaryReason;
        public DirectorRunState RunState { get; private set; } = DirectorRunState.Suspended;
        public DirectorDecisionReason DecisionReason { get; private set; } = DirectorDecisionReason.NoActors;
        public DirectorEvaluationRecord LastEvaluation { get; private set; }
        public event Action<DirectorEvaluationRecord> EvaluationCompleted;
        public bool AcceptsAdaptiveMetrics => !needsFreshWindow && RunState != DirectorRunState.Ended;
        public bool AdaptiveSpawningBlocked => enableAdaptiveDirector && !adaptiveObserveOnly
            && (RunState != DirectorRunState.Active || adaptiveStateMachine.Phase == DirectorPhase.Relax
                || adaptiveDecision.ProtectWeakestPlayer);
        public bool AdaptiveSpecialGateOpen => !AdaptiveSpawningBlocked && adaptiveDecision.SpecialGateOpen;
        public IReadOnlyList<ActorPressureSnapshot> ActorPressure => actorPressure;
        public int DirectorServerTick => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening
            ? NetworkManager.Singleton.ServerTime.Tick : (int)(GetDirectorTime() * NetworkGameplayPolicy.SimulationHz);

        public void RecordDirectorDamage(ulong id, double time, float damage, float maxHealth)
        {
            pressure.RecordDamage(id, time, damage, maxHealth);
            lastDamageTime = time;
            pressureDirty = true;
        }
        public void RecordDirectorShot(ulong id, double time) => pressure.RecordShot(id, time);
        public void RecordDirectorDown(ulong id, double time)
        {
            pressure.RecordDown(id, time);
            pressureDirty = true;
        }

        private void UpdateOperatingContext()
        {
            InitializeAdaptiveSystems();
            double now = GetDirectorTime();
            var campaign = CampaignMissionController.Instance;
            bool terminal = campaign != null && campaign.State.phase is CampaignPhase.Failed or CampaignPhase.Completed
                || NetworkMatchStateManager.Instance != null && NetworkMatchStateManager.Instance.State == NetworkMatchState.GameOver;
            bool discard = campaign != null && campaign.State.phase is CampaignPhase.Insertion or CampaignPhase.Preparing
                or CampaignPhase.AwaitingParty or CampaignPhase.Transitioning;
            bool suspended = !NetworkMatchStateManager.IsGameplayActive
                || campaign != null && (campaign.SuppressSpawns || campaign.BlocksInput);
            if (terminal || discard)
            {
                if (!needsFreshWindow) EndCampaignEncounter();
                RunState = terminal ? DirectorRunState.Ended : DirectorRunState.Suspended;
                DecisionReason = terminal ? DirectorDecisionReason.Terminal : DirectorDecisionReason.CampaignSuspended;
                return;
            }

            if (now >= nextPressureSample || pressureDirty)
            {
                SampleActorPressure(now, !suspended);
                nextPressureSample = now + .1;
                pressureDirty = false;
            }
            bool noActors = pressureInput.ParticipantCount == 0;
            DirectorRunState nextState = noActors || suspended ? DirectorRunState.Suspended : DirectorRunState.Active;
            if (noActors && !needsFreshWindow)
            {
                CancelAdaptiveWindow();
                adaptiveStateMachine.Reset(now);
                needsFreshWindow = true;
                spawnTimer = phaseTimer = 0;
            }
            if (nextState == DirectorRunState.Active && needsFreshWindow)
            {
                adaptiveStateMachine.Reset(now);
                needsFreshWindow = false;
                if (enableAdaptiveDirector && !adaptiveObserveOnly)
                {
                    SetPhase(GamePhase.BUILD);
                    SetAdaptiveState(DirectorPhase.Calm, adaptiveDifficultyMultiplier);
                }
            }
            RunState = nextState;
            if (nextState != DirectorRunState.Active)
            {
                DecisionReason = noActors ? DirectorDecisionReason.NoActors : DirectorDecisionReason.CampaignSuspended;
                adaptiveDecision = adaptiveStateMachine.Advance(0, pressureInput, nextState, now).Decision;
            }
        }

        private void SampleActorPressure(double now, bool countExposure)
        {
            using (PressureMarker.Auto())
            {
                actorPressure.Clear();
                float weakest = 1, team = 0;
                bool protect = false, recoverySafe = true;
                var network = NetworkManager.Singleton;
                var sessions = NetworkGameManager.Instance;
                if (network != null && network.IsServer && sessions != null)
                {
                    foreach (var client in network.ConnectedClientsList)
                    {
                        if (client.PlayerObject == null) continue;
                        if (!pressureActors.TryGetValue(client.ClientId, out PlayerHealth health)
                            || health == null || health.gameObject != client.PlayerObject.gameObject)
                        {
                            health = client.PlayerObject.GetComponent<PlayerHealth>();
                            pressureActors[client.ClientId] = health;
                        }
                        if (health == null || !sessions.TryGetRegisteredActor(health.StablePlayerId, out var registered)
                            || registered != health) continue;
                        bool participating = !health.IsDead && health.LifeState != PlayerLifeState.Dead
                            && (health.IsInputReady || health.LifeState == PlayerLifeState.Downed);
                        int threats = 0;
                        foreach (EnemyAI enemy in EnemyAI.PressureRoster)
                        {
                            if (enemy != null && enemy.TryGetPressureThreat(out Transform target)
                                && (target == health.transform || target.IsChildOf(health.transform))
                                && (enemy.transform.position - health.transform.position).sqrMagnitude <= 144)
                                threats++;
                            if (threats >= 4) break;
                        }
                        var sample = pressure.Sample(health.StablePlayerId.Value, now,
                            health.CurrentHealth / Mathf.Max(1, health.MaxHealth), threats,
                            health.LifeState == PlayerLifeState.Downed, participating, countExposure);
                        if (!participating) continue;
                        actorPressure.Add(sample);
                        weakest = Mathf.Min(weakest, sample.Health01);
                        team = Mathf.Max(team, sample.Intensity);
                        protect |= sample.Protect;
                        recoverySafe &= sample.RecoverySafe;
                        sessions.AdaptiveMetrics?.RecordExposure(sample.PlayerId, DirectorServerTick, sample.ExposureSeconds);
                    }
                }
                pressureInput = new DirectorInput(actorPressure.Count > 0 ? weakest : 0, 0, 0, 999,
                    ZombiesAlive, team, protect, recoverySafe, actorPressure.Count);
                if (enableAdaptiveDirector && !adaptiveObserveOnly) intensity = team;
            }
        }

        private void CancelAdaptiveWindow()
        {
            NetworkGameManager.Instance?.AdaptiveMetrics?.ResetEncounter(DirectorServerTick);
            pressure.Reset();
            pressureActors.Clear();
            actorPressure.Clear();
            nextPressureSample = 0;
            pressureDirty = false;
            lastDamageTime = double.NaN;
            ProtectionLatencyMilliseconds = -1;
        }

        public void BeginAdaptiveAttempt()
        {
            if (!CanRunServerLogic()) return;
            EndCampaignEncounter();
            attemptId++;
        }

        public void FinalizeAdaptiveBoundary(int sealedThroughTick)
        {
            if (!CanRunServerLogic() || !enableAdaptiveDirector) return;
            var metrics = NetworkGameManager.Instance?.AdaptiveMetrics;
            if (metrics == null || !metrics.PendingBoundaryTick.HasValue
                || sealedThroughTick < metrics.PendingBoundaryTick.Value) return;
            var campaign = CampaignMissionController.Instance;
            if (needsFreshWindow || RunState == DirectorRunState.Ended
                || campaign != null && campaign.State.phase is CampaignPhase.Failed or CampaignPhase.Completed)
            {
                metrics.ResetEncounter(DirectorServerTick);
                return;
            }
            var samples = metrics.BuildSamples();
            float previous = adaptiveDifficultyEvaluator.CurrentMultiplier;
            var evaluation = adaptiveDifficultyEvaluator.Evaluate(DifficultyManager.Instance != null
                ? DifficultyManager.Instance.CurrentDifficulty.Value : DifficultyLevel.Medium, samples, true);
            LastEvaluation = new DirectorEvaluationRecord(runId, attemptId, ++encounterId,
                metrics.PendingBoundaryTick.Value, boundaryReason, samples, evaluation, previous, !adaptiveObserveOnly,
                adaptiveDifficultyPolicy != null ? adaptiveDifficultyPolicy.ToPolicy() : DynamicDifficultyPolicy.Default);
            if (!adaptiveObserveOnly)
            {
                adaptiveDifficultyMultiplier = evaluation.Multiplier;
                SetAdaptiveState(adaptiveStateMachine.Phase, adaptiveDifficultyMultiplier);
            }
            // Publish the immutable record while the evaluated window still exists.
            try { EvaluationCompleted?.Invoke(LastEvaluation); }
            finally { metrics.CompleteBoundary(); }
        }
    }
}
