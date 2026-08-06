using System.Collections.Generic;
using System.Globalization;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class PlayerMovement : NetworkBehaviour
    {
        private struct ServerAimSample
        {
            public bool valid;
            public int tick;
            public uint inputSequence;
            public Vector3 origin;
            public float yaw;
            public float pitch;
        }

        [Header("References")]
        [SerializeField] private CharacterController controller;
        [SerializeField] private Animator characterAnimation;
        [SerializeField] private MouseMovement mouseMovement;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform groundCheck;

        [Header("Movement")]
        [SerializeField] private float speed = 5f;
        [SerializeField] private float walkMultiplier = 2f;
        [SerializeField] private float jumpHeight = 0.5f;
        [SerializeField] private float gravityScale = 1f;

        [Header("Prediction / Reconciliation")]
        [SerializeField] private float reconciliationThreshold = 0.18f;
        // Thời gian smooth visual correction offset sau reconcile (giấu giật)
        [SerializeField] private float ownerCorrectionSmoothTime = 0.1f;
        // Cap tối đa của correction offset để tránh camera bay quá xa
        [SerializeField] private float maxVisualCorrectionOffset = 0.75f;

        [Header("Remote Interpolation")]
        // Số snapshot phải có trước khi bắt đầu interpolate remote player
        [SerializeField] private int remoteStartBufferSnapshots = 4;
        // Khoảng cách để teleport thay vì lerp (lag spike, respawn)
        [SerializeField] private float remoteTeleportDistance = 3f;

        // ==========================================
        // CONSTANTS
        private const float GRAVITY = -9.81f;
        public const int SimulationHz = NetworkGameplayPolicy.SimulationHz;
        public const int SnapshotHz = NetworkGameplayPolicy.SnapshotHz;
        private const float TICK_DT = 1f / SimulationHz;
        private const int BUFFER_SIZE = 2048;
        private const int STATE_SEND_EVERY_N_TICKS = NetworkGameplayPolicy.StateSendEveryNTicks;
        private const int SERVER_INPUT_BUFFER_TICKS = 3;
        private const int MAX_INPUT_TICKS_BEHIND = NetworkGameplayPolicy.MaxPastInputTicks;
        private const int MAX_INPUT_TICKS_AHEAD = NetworkGameplayPolicy.MaxFutureInputTicks;
        private const int MAX_REPEATED_INPUT_TICKS = NetworkGameplayPolicy.MaxRepeatedInputTicks;
        private NetworkTimer networkTimer;
        private Vector2 cachedMove;
        private bool jumpQueued;
        private bool sprintHeld;
        private float cachedYaw;
        private float cachedPitch;
        private uint localCommandSequence;
        private PlayerInputPayload previousSentInput1;
        private PlayerInputPayload previousSentInput2;
        private byte sentInputHistoryCount;

        // ==========================================
        // CLIENT BUFFERS
        // ==========================================
        private PlayerInputPayload[] inputBuffer;
        private PlayerStatePayload[] stateBuffer;

        // ==========================================
        // SERVER STATE
        // ==========================================
        private Dictionary<int, PlayerInputPayload> pendingInputs;
        private readonly List<int> staleInputTicks = new();
        private int nextServerTick;
        private PlayerInputPayload lastInput;
        private bool hasLastInput;
        private bool hasStartedServerTicking;
        private int repeatedInputTicks;
        private uint lastProcessedCommandSequence;
        private bool hasProcessedCommandSequence;
        private ServerAimSample[] serverAimHistory;
        private bool hasConfirmedFireReference;
        private uint confirmedFireInputSequence;
        private int confirmedFireTick;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool verificationInputEnabled;
        private Vector2 verificationMove;
#endif

        // ==========================================
        // REMOTE INTERPOLATION
        // ==========================================
        private readonly List<PlayerStatePayload> stateSnapshots = new();
        private float interpolationTimer;
        private bool remoteInterpolationStarted;

        // ==========================================
        // GAMEPLAY STATE
        // ==========================================
        private float verticalVelocity;
        private bool isGrounded;
        private LayerMask groundMask;

        // ==========================================
        // VISUAL INTERPOLATION (owner only)
        // ==========================================
        private Vector3 previousTickPosition;
        private Vector3 currentTickPosition;
        // Offset để smooth visual sau reconcile thay vì snap cứng
        private Vector3 ownerVisualCorrectionOffset;
        private bool hasTickedOnce;
        private PlayerHealth playerHealth;
        private float ownerHardSnapDistance = NetworkGameplayPolicy.OwnerHardSnapDistance;
        private int maxRepeatedInputTicks = NetworkGameplayPolicy.MaxRepeatedInputTicks;
        private bool serverReplicationStopped;

        public int CurrentSimulationTick => networkTimer != null ? networkTimer.CurrentTick : 0;

        // ==========================================
        // INITIALIZATION
        // ==========================================

        public override void OnNetworkSpawn()
        {
            serverReplicationStopped = false;
            networkTimer = new NetworkTimer(TICK_DT)
            {
                CurrentTick = NetworkManager != null && NetworkManager.IsListening
                    ? NetworkManager.ServerTime.Tick
                    : 0
            };
            groundMask = LayerMask.GetMask("Ground");
            playerHealth = GetComponent<PlayerHealth>();
            if (NetworkGameManager.Instance != null)
            {
                NetworkHardeningSettings settings = NetworkGameManager.Instance.Settings;
                ownerCorrectionSmoothTime = settings.OwnerCorrectionSmoothSeconds;
                ownerHardSnapDistance = settings.OwnerHardSnapDistance;
                maxRepeatedInputTicks = Mathf.Max(1,
                    Mathf.CeilToInt(settings.InputSilenceSeconds * SimulationHz));
            }

            if (IsOwner)
            {
                inputBuffer = new PlayerInputPayload[BUFFER_SIZE];
                stateBuffer = new PlayerStatePayload[BUFFER_SIZE];

                previousTickPosition = transform.position;
                currentTickPosition = transform.position;
            }

            if (IsServer)
                serverAimHistory = new ServerAimSample[BUFFER_SIZE];

            if (IsServer && !IsOwner)
            {
                pendingInputs = new Dictionary<int, PlayerInputPayload>();
            }

            if (!IsOwner && !IsServer)
            {
                if (controller != null)
                    controller.enabled = false;
            }
        }

        // ==========================================
        // UPDATE
        // ==========================================

        private void Update()
        {
            if (networkTimer == null)
                return;

            if (playerHealth != null && playerHealth.IsDead)
            {
                cachedMove = Vector2.zero;
                jumpQueued = false;
                UpdateAnimation();
                return;
            }

            if (playerHealth != null && !playerHealth.IsInputReady)
            {
                cachedMove = Vector2.zero;
                sprintHeld = false;
                jumpQueued = false;
                UpdateAnimation();
                return;
            }

            if (IsOwner)
                CaptureFrameInput();

            networkTimer.Accumulate(Time.deltaTime);

            while (networkTimer.CanTick())
            {
                networkTimer.ConsumeTick();

                if (IsServer && !IsOwner)
                    ServerTick();

                if (IsOwner)
                {
                    previousTickPosition = transform.position;
                    ClientOwnerTick();
                    currentTickPosition = transform.position;
                    hasTickedOnce = true;
                }

                networkTimer.CurrentTick++;
            }

            if (!IsOwner && !IsServer)
                InterpolateRemote();

            UpdateAnimation();
        }

        private void LateUpdate()
        {
            SmoothOwnerVisual();
        }

        // ==========================================
        // FRAME INPUT
        // ==========================================

        private void CaptureFrameInput()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verificationInputEnabled)
            {
                cachedMove = Vector2.ClampMagnitude(verificationMove, 1f);
                sprintHeld = false;
                jumpQueued = false;
                cachedYaw = transform.eulerAngles.y;
                cachedPitch = 0f;
                return;
            }
#endif
            if (InputManager.GameplayInputBlocked)
            {
                cachedMove = Vector2.zero;
                sprintHeld = false;
                jumpQueued = false;
                cachedYaw = mouseMovement != null
                    ? mouseMovement.YRotation
                    : transform.eulerAngles.y;
                return;
            }

            InputManager inputManager = InputManager.Instance;
            cachedMove = inputManager != null ? inputManager.GetMove() : Vector2.zero;
            cachedMove = Vector2.ClampMagnitude(cachedMove, 1f);

            sprintHeld = inputManager != null && inputManager.GetSprintInput();

            cachedYaw = mouseMovement != null
                ? mouseMovement.YRotation
                : transform.eulerAngles.y;
            cachedPitch = mouseMovement != null ? mouseMovement.XRotation : 0f;

            if (inputManager != null && inputManager.GetJumpInputDown())
            {
                jumpQueued = true;
            }
        }

        private PlayerInputPayload BuildInputForTick(int tick)
        {
            var input = new PlayerInputPayload
            {
                sequence = unchecked(++localCommandSequence),
                tick = tick,
                move = cachedMove,
                jumpPressed = jumpQueued,
                sprint = sprintHeld,
                yaw = cachedYaw,
                pitch = cachedPitch
            };

            jumpQueued = false;
            return input;
        }

        // ==========================================
        // CLIENT OWNER TICK
        // ==========================================

        private void ClientOwnerTick()
        {
            int tick = networkTimer.CurrentTick;
            int index = tick % BUFFER_SIZE;

            var input = BuildInputForTick(tick);

            inputBuffer[index] = input;

            transform.rotation = Quaternion.Euler(0f, input.yaw, 0f);
            SimulateTick(input, TICK_DT);

            if (IsServer)
            {
                RecordServerAim(input, tick);
                confirmedFireInputSequence = input.sequence;
                confirmedFireTick = tick;
                hasConfirmedFireReference = true;
            }

            stateBuffer[index] = CaptureState(tick);

            if (IsServer)
            {
                // Host — đã simulate authoritative, gửi state luôn
                if (tick % STATE_SEND_EVERY_N_TICKS == 0)
                    SendStateClientRpc(CaptureState(tick));
            }
            else
            {
                var packet = new PlayerCommandPacket
                {
                    commandCount = (byte)Mathf.Min(3, sentInputHistoryCount + 1),
                    latest = input,
                    previous1 = previousSentInput1,
                    previous2 = previousSentInput2
                };
                if (IsSpawned && NetworkManager != null && NetworkManager.IsListening)
                {
                    SendInputServerRpc(packet);
                }
                previousSentInput2 = previousSentInput1;
                previousSentInput1 = input;
                sentInputHistoryCount = (byte)Mathf.Min(2, sentInputHistoryCount + 1);
            }
        }

        // ==========================================
        // SERVER RPC — Nhận input từ client
        // ==========================================

        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        private void SendInputServerRpc(PlayerCommandPacket packet, ServerRpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId)
                return;

            if (pendingInputs == null)
                pendingInputs = new Dictionary<int, PlayerInputPayload>();

            int count = Mathf.Clamp(packet.commandCount, 0, 3);
            for (int i = count - 1; i >= 0; i--)
            {
                PlayerInputPayload input = packet.GetCommand(i);
                int expectedTick = hasStartedServerTicking
                    ? nextServerTick
                    : NetworkManager.ServerTime.Tick;
                if (!TrySanitizeInput(input, expectedTick, out PlayerInputPayload sanitized))
                {
                    EmitCommandDrop("invalid_or_tick_window");
                    continue;
                }

                if (hasStartedServerTicking && sanitized.tick < nextServerTick)
                {
                    EmitCommandDrop("stale_tick");
                    continue;
                }

                if (pendingInputs.Count >= 256 && !pendingInputs.ContainsKey(sanitized.tick))
                {
                    EmitCommandDrop("queue_full");
                    continue;
                }

                if (pendingInputs.TryGetValue(sanitized.tick, out PlayerInputPayload queued)
                    && !NetworkSequence.IsNewer(sanitized.sequence, queued.sequence))
                {
                    continue;
                }

                pendingInputs[sanitized.tick] = sanitized;
            }

            CleanupOldInputs();
        }

        public static bool TrySanitizeInput(PlayerInputPayload input, int nextExpectedTick, out PlayerInputPayload sanitized)
        {
            sanitized = input;

            if (!IsFinite(input.move.x) || !IsFinite(input.move.y) || !IsFinite(input.yaw) || !IsFinite(input.pitch))
                return false;

            NetworkHardeningSettings settings = NetworkHardeningRuntime.Current;
            if (input.tick < nextExpectedTick - settings.MaxPastInputTicks)
                return false;

            if (input.tick > nextExpectedTick + settings.MaxFutureInputTicks)
                return false;

            sanitized.move = Vector2.ClampMagnitude(input.move, 1f);
            sanitized.yaw = Mathf.Repeat(input.yaw, 360f);
            sanitized.pitch = Mathf.Clamp(input.pitch, -90f, 90f);
            return true;
        }

        public bool SimulateInputForTests(PlayerInputPayload input, float dt, int nextExpectedTick = 0)
        {
            if (!TrySanitizeInput(input, nextExpectedTick, out PlayerInputPayload sanitized))
                return false;

            transform.rotation = Quaternion.Euler(0f, sanitized.yaw, 0f);
            SimulateTick(sanitized, dt);
            return true;
        }

        public bool TryBuildServerAim(
            int inputTick,
            uint inputSequence,
            out Vector3 origin,
            out Vector3 direction)
        {
            origin = default;
            direction = default;
            if (!IsServer)
            {
                Debug.LogWarning($"[DIAGNOSTIC][ServerAim] TryBuildServerAim FAILED: IsServer is false!");
                return false;
            }

            // 1. Try exact match in serverAimHistory
            if (serverAimHistory != null)
            {
                int index = PositiveModulo(inputTick, BUFFER_SIZE);
                ServerAimSample sample = serverAimHistory[index];
                if (sample.valid && sample.tick == inputTick && sample.inputSequence == inputSequence)
                {
                    origin = sample.origin;
                    direction = Quaternion.Euler(sample.pitch, sample.yaw, 0f) * Vector3.forward;
                    return direction.sqrMagnitude > 0.999f;
                }

                // 2. Fallback: Search for closest valid sample in history
                int newestTick = networkTimer != null ? networkTimer.CurrentTick : nextServerTick;
                int oldestTick = Mathf.Max(0, newestTick - BUFFER_SIZE + 1);
                for (int t = newestTick; t >= oldestTick; t--)
                {
                    ServerAimSample s = serverAimHistory[PositiveModulo(t, BUFFER_SIZE)];
                    if (s.valid && (s.inputSequence == inputSequence || Mathf.Abs(s.tick - inputTick) <= 10))
                    {
                        origin = s.origin;
                        direction = Quaternion.Euler(s.pitch, s.yaw, 0f) * Vector3.forward;
                        return direction.sqrMagnitude > 0.999f;
                    }
                }
            }

            // 3. Ultimate Fallback: Use current server transform position and eye height
            float eyeHeight = controller != null ? Mathf.Max(0.5f, controller.height * 0.8f) : 1.6f;
            origin = transform.position + Vector3.up * eyeHeight;
            direction = transform.forward;
            bool ok = direction.sqrMagnitude > 0.999f;
            if (!ok)
            {
                Debug.LogWarning($"[DIAGNOSTIC][ServerAim] TryBuildServerAim FAILED: direction sqrMagnitude={direction.sqrMagnitude} <= 0.999!");
            }
            return ok;
        }

        public bool TryGetLatestFireReference(out uint inputSequence, out int inputTick)
        {
            if (IsOwner)
            {
                inputSequence = localCommandSequence;
                int netTick = NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Tick : 0;
                inputTick = netTick > 0 ? netTick : (networkTimer != null ? networkTimer.CurrentTick : 0);
                return true;
            }

            inputSequence = confirmedFireInputSequence;
            inputTick = confirmedFireTick;
            return hasConfirmedFireReference;
        }

        public bool TryGetConfirmedFireReference(out uint inputSequence, out int inputTick)
        {
            return TryGetLatestFireReference(out inputSequence, out inputTick);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void SetVerificationInput(Vector2 move)
        {
            verificationInputEnabled = true;
            verificationMove = move;
        }
#endif

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // ==========================================
        // SERVER TICK
        // ==========================================

        private void ServerTick()
        {
            if (serverReplicationStopped || pendingInputs == null)
                return;

            if (!hasStartedServerTicking)
            {
                // FIX 2: Chờ đủ buffer trước khi bắt đầu simulate
                // Tránh server chạy sớm quá, repeat lastInput liên tục khi input chưa kịp đến
                if (pendingInputs.Count < SERVER_INPUT_BUFFER_TICKS)
                    return;

                nextServerTick = GetMinPendingTick();
                hasStartedServerTicking = true;
            }

            PlayerInputPayload input;

            bool hasReceivedInput = pendingInputs.TryGetValue(nextServerTick, out var receivedInput);
            if (hasReceivedInput)
                pendingInputs.Remove(nextServerTick);

            bool sequenceAccepted = hasReceivedInput
                && (!hasProcessedCommandSequence
                    || NetworkSequence.IsNewer(receivedInput.sequence, lastProcessedCommandSequence));
            if (hasReceivedInput && !sequenceAccepted)
                EmitCommandDrop("duplicate_or_out_of_order_sequence");

            if (sequenceAccepted)
            {
                input = receivedInput;
                lastInput = input;
                hasLastInput = true;
                repeatedInputTicks = 0;
                lastProcessedCommandSequence = input.sequence;
                hasProcessedCommandSequence = true;
            }
            else
            {
                // Input trễ — repeat last (không jump)
                if (!hasLastInput)
                    return;

                input = lastInput;
                input.tick = nextServerTick;
                input.jumpPressed = false;
                repeatedInputTicks++;
                if (NetworkGameplayPolicy.ShouldNeutralizeInput(repeatedInputTicks, maxRepeatedInputTicks))
                {
                    input.move = Vector2.zero;
                    input.sprint = false;
                    if (repeatedInputTicks == maxRepeatedInputTicks + 1)
                        EmitCommandDrop("input_silence_neutralized");
                }
            }

            transform.rotation = Quaternion.Euler(0f, input.yaw, 0f);
            SimulateTick(input, TICK_DT);
            RecordServerAim(input, nextServerTick);

            if (nextServerTick % STATE_SEND_EVERY_N_TICKS == 0)
                SendStateClientRpc(CaptureState(nextServerTick));

            nextServerTick++;
        }

        internal void StopServerReplicationForDisconnect()
        {
            serverReplicationStopped = true;
            pendingInputs?.Clear();
        }

        private int GetMinPendingTick()
        {
            int minTick = int.MaxValue;
            foreach (var key in pendingInputs.Keys)
                if (key < minTick) minTick = key;
            return minTick;
        }

        private void CleanupOldInputs()
        {
            if (pendingInputs.Count <= 200)
                return;

            staleInputTicks.Clear();
            foreach (var key in pendingInputs.Keys)
                if (key < nextServerTick - 100)
                    staleInputTicks.Add(key);

            foreach (var key in staleInputTicks)
                pendingInputs.Remove(key);
        }

        // ==========================================
        // CLIENT RPC — Nhận state từ server
        // ==========================================

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void SendStateClientRpc(PlayerStatePayload state)
        {
            if (IsOwner && !IsServer)
            {
                Reconcile(state);
                return;
            }

            if (!IsOwner && !IsServer)
            {
                AddRemoteSnapshot(state);
            }
        }

        // ==========================================
        // RECONCILIATION
        // ==========================================

        private void Reconcile(PlayerStatePayload serverState)
        {
            CaptureConfirmedFireReference(serverState.lastProcessedCommand);
            int index = serverState.tick % BUFFER_SIZE;
            var predicted = stateBuffer[index];

            // Bỏ qua nếu tick không khớp (state cũ hơn buffer)
            if (predicted.tick != serverState.tick)
                return;

            float error = Vector3.Distance(predicted.position, serverState.position);

            if (error < reconciliationThreshold)
                return;

            PlayerHealth health = GetComponent<PlayerHealth>();
            NetworkDiagnostics.Emit(
                "movement_correction",
                NetworkGameManager.Instance != null ? NetworkGameManager.Instance.State : SessionState.InMatch,
                error.ToString("F3", CultureInfo.InvariantCulture),
                health != null ? health.StablePlayerId : default);

            // Lưu vị trí visual trước khi snap để tạo correction offset
            Vector3 visualBefore = visualRoot != null
                ? visualRoot.position
                : transform.position;

            ApplyAuthoritativeState(serverState);
            ReplayFrom(serverState.tick + 1);

            // Cập nhật tick positions sau replay
            previousTickPosition = transform.position;
            currentTickPosition = transform.position;

            // FIX 4: Thay vì snap visual cứng, tạo offset rồi smooth về 0
            // Người chơi thấy correction mượt thay vì giật mạnh
            if (visualRoot != null && error <= ownerHardSnapDistance)
            {
                ownerVisualCorrectionOffset += visualBefore - transform.position;

                // Cap offset để camera không bay quá xa
                if (ownerVisualCorrectionOffset.magnitude > maxVisualCorrectionOffset)
                    ownerVisualCorrectionOffset = ownerVisualCorrectionOffset.normalized * maxVisualCorrectionOffset;
            }
            else if (error > ownerHardSnapDistance)
            {
                ownerVisualCorrectionOffset = Vector3.zero;
            }
        }

        private void CaptureConfirmedFireReference(uint acknowledgedSequence)
        {
            if (inputBuffer == null || networkTimer == null)
                return;

            int newestTick = networkTimer.CurrentTick;
            int oldestTick = Mathf.Max(0, newestTick - BUFFER_SIZE + 1);
            for (int tick = newestTick; tick >= oldestTick; tick--)
            {
                PlayerInputPayload input = inputBuffer[PositiveModulo(tick, BUFFER_SIZE)];
                if (input.tick != tick || input.sequence != acknowledgedSequence)
                    continue;

                confirmedFireInputSequence = acknowledgedSequence;
                confirmedFireTick = tick;
                hasConfirmedFireReference = true;
                return;
            }
        }

        private void RecordServerAim(PlayerInputPayload input, int tick)
        {
            if (serverAimHistory == null)
                return;

            float eyeHeight = controller != null ? Mathf.Max(0.5f, controller.height * 0.8f) : 1.6f;
            Vector3 bodyOrigin = transform.position + Vector3.up * 0.5f;
            Vector3 desiredOrigin = transform.position + Vector3.up * eyeHeight;
            Vector3 offset = desiredOrigin - bodyOrigin;
            if (Physics.Raycast(bodyOrigin, offset.normalized, out RaycastHit obstruction, offset.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && obstruction.collider.GetComponentInParent<NetworkObject>() != NetworkObject)
            {
                desiredOrigin = obstruction.point - offset.normalized * 0.02f;
            }

            serverAimHistory[PositiveModulo(tick, BUFFER_SIZE)] = new ServerAimSample
            {
                valid = true,
                tick = tick,
                inputSequence = input.sequence,
                origin = desiredOrigin,
                yaw = input.yaw,
                pitch = input.pitch
            };
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }

        private void EmitCommandDrop(string reason)
        {
            PlayerHealth health = playerHealth != null ? playerHealth : GetComponent<PlayerHealth>();
            NetworkDiagnostics.Emit(
                "command_drop",
                NetworkGameManager.Instance != null ? NetworkGameManager.Instance.State : SessionState.InMatch,
                reason,
                health != null ? health.StablePlayerId : default);
        }

        private void ApplyAuthoritativeState(PlayerStatePayload state)
        {
            if (controller != null)
                controller.enabled = false;

            transform.position = state.position;
            transform.rotation = Quaternion.Euler(0f, state.yaw, 0f);

            if (controller != null)
                controller.enabled = true;

            verticalVelocity = state.verticalVelocity;
            isGrounded = state.grounded;
        }

        public void TeleportForRespawn(Vector3 position, Quaternion rotation)
        {
            bool controllerWasEnabled = controller != null && controller.enabled;
            if (controller != null)
                controller.enabled = false;

            transform.SetPositionAndRotation(position, rotation);

            if (controller != null)
                controller.enabled = controllerWasEnabled;

            verticalVelocity = 0f;
            isGrounded = false;
            previousTickPosition = position;
            currentTickPosition = position;
            ownerVisualCorrectionOffset = Vector3.zero;
            stateSnapshots.Clear();
            interpolationTimer = 0f;
            remoteInterpolationStarted = false;

            if (visualRoot != null)
                visualRoot.position = position;
        }

        private void ReplayFrom(int startTick)
        {
            for (int tick = startTick; tick < networkTimer.CurrentTick; tick++)
            {
                int index = tick % BUFFER_SIZE;
                var input = inputBuffer[index];

                // Bỏ qua nếu buffer slot không đúng tick
                if (input.tick != tick)
                    continue;

                transform.rotation = Quaternion.Euler(0f, input.yaw, 0f);
                SimulateTick(input, TICK_DT);

                stateBuffer[index] = CaptureState(tick);
            }
        }

        // ==========================================
        // REMOTE INTERPOLATION
        // ==========================================

        private void AddRemoteSnapshot(PlayerStatePayload state)
        {
            // Bỏ qua snapshot cũ hơn snapshot cuối (out-of-order packet)
            if (stateSnapshots.Count > 0)
            {
                var last = stateSnapshots[stateSnapshots.Count - 1];
                if (state.tick <= last.tick)
                    return;
            }

            stateSnapshots.Add(state);

            // Giữ buffer có giới hạn
            while (stateSnapshots.Count > 32)
                stateSnapshots.RemoveAt(0);
        }

        private void InterpolateRemote()
        {
            if (stateSnapshots.Count < 2)
                return;

            // FIX 3: Chờ đủ buffer snapshot trước khi bắt đầu interpolate
            // Tránh hết snapshot giữa chừng → player remote đứng khựng
            if (!remoteInterpolationStarted)
            {
                if (stateSnapshots.Count < remoteStartBufferSnapshots)
                    return;

                remoteInterpolationStarted = true;
                interpolationTimer = 0f;
            }

            interpolationTimer += Time.deltaTime;

            // Tiêu thụ các snapshot đã qua theo tick delta thực tế
            // Không dùng sendInterval cố định vì packet không đến đều
            while (stateSnapshots.Count >= 2)
            {
                var from = stateSnapshots[0];
                var to = stateSnapshots[1];
                float duration = Mathf.Max((to.tick - from.tick) * TICK_DT, TICK_DT);

                if (interpolationTimer <= duration)
                    break;

                interpolationTimer -= duration;
                stateSnapshots.RemoveAt(0);
            }

            if (stateSnapshots.Count < 2)
            {
                // Hết snapshot — reset, chờ buffer lại
                remoteInterpolationStarted = false;
                return;
            }

            var a = stateSnapshots[0];
            var b = stateSnapshots[1];

            float snapshotDuration = Mathf.Max((b.tick - a.tick) * TICK_DT, TICK_DT);
            float t = Mathf.Clamp01(interpolationTimer / snapshotDuration);

            // Teleport nếu lệch quá xa (lag spike, respawn) thay vì lerp xuyên map
            if (Vector3.Distance(a.position, b.position) > remoteTeleportDistance)
            {
                transform.position = b.position;
                transform.rotation = Quaternion.Euler(0f, b.yaw, 0f);
                stateSnapshots.RemoveAt(0);
                interpolationTimer = 0f;
                return;
            }

            transform.position = Vector3.Lerp(a.position, b.position, t);
            transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(0f, a.yaw, 0f),
                Quaternion.Euler(0f, b.yaw, 0f),
                t
            );

            verticalVelocity = Mathf.Lerp(a.verticalVelocity, b.verticalVelocity, t);
            isGrounded = b.grounded;
        }

        // ==========================================
        // VISUAL SMOOTH — Owner only, smooth VisualRoot
        // ==========================================

        private void SmoothOwnerVisual()
        {
            if (!IsOwner || visualRoot == null || !hasTickedOnce)
                return;

            float alpha = Mathf.Clamp01(networkTimer.Alpha);

            // Lerp VisualRoot giữa 2 tick positions — PlayerRoot không bị đụng tới
            Vector3 smoothPosition = Vector3.Lerp(
                previousTickPosition,
                currentTickPosition,
                alpha
            );

            // Smooth correction offset về 0 sau reconcile
            // Người chơi thấy correction mượt dần thay vì giật cứng
            if (ownerCorrectionSmoothTime > 0f)
            {
                float decay = 1f - Mathf.Exp(-Time.deltaTime / ownerCorrectionSmoothTime);
                ownerVisualCorrectionOffset = Vector3.Lerp(
                    ownerVisualCorrectionOffset,
                    Vector3.zero,
                    decay
                );
            }
            else
            {
                ownerVisualCorrectionOffset = Vector3.zero;
            }

            visualRoot.position = smoothPosition + ownerVisualCorrectionOffset;
        }

        // ==========================================
        // SIMULATION — Client = Server, same dt
        // ==========================================

        private void SimulateTick(PlayerInputPayload input, float dt)
        {
            if (controller == null || !controller.enabled)
                return;

            isGrounded = CheckGrounded();

            if (isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            if (input.jumpPressed && isGrounded)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * GRAVITY);

            verticalVelocity += GRAVITY * gravityScale * dt;

            Quaternion yawRot = Quaternion.Euler(0f, input.yaw, 0f);
            Vector3 move = yawRot * new Vector3(input.move.x, 0f, input.move.y);

            float currentSpeed = input.sprint
                ? speed
                : speed / walkMultiplier;

            Vector3 totalMove = move * currentSpeed;
            totalMove.y = verticalVelocity;

            controller.Move(totalMove * dt);
        }

        private bool CheckGrounded()
        {
            if (controller != null && controller.isGrounded)
            {
                return true;
            }

            if (groundCheck == null)
            {
                return false;
            }

            int maskToUse = groundMask.value != 0 ? groundMask.value : LayerMask.GetMask("Ground");
            if (maskToUse == 0)
            {
                maskToUse = 1 << LayerMask.NameToLayer("Default");
            }

            return Physics.CheckSphere(groundCheck.position, 0.4f, maskToUse, QueryTriggerInteraction.Ignore);
        }

        // ==========================================
        // STATE CAPTURE
        // ==========================================

        private PlayerStatePayload CaptureState(int tick)
        {
            return new PlayerStatePayload
            {
                tick = tick,
                lastProcessedCommand = lastProcessedCommandSequence,
                position = transform.position,
                verticalVelocity = verticalVelocity,
                grounded = isGrounded,
                yaw = transform.eulerAngles.y
            };
        }

        // ==========================================
        // ANIMATION
        // ==========================================

        private void UpdateAnimation()
        {
            if (characterAnimation == null)
                return;

            characterAnimation.SetBool("Grounded", isGrounded);
            characterAnimation.SetBool("FreeFall", !isGrounded && verticalVelocity < -2f);

            if (IsOwner)
            {
                float moveMagnitude = cachedMove.magnitude;
                float currentSpeed = sprintHeld
                    ? speed
                    : speed / walkMultiplier;

                characterAnimation.SetFloat("Speed", moveMagnitude * currentSpeed);
            }
        }
    }
}
