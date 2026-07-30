using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class PlayerMovement : NetworkBehaviour
    {
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
        [SerializeField] private float ownerCorrectionSmoothTime = 0.08f;
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
        private const int MAX_INPUT_TICKS_BEHIND = 120;
        private const int MAX_INPUT_TICKS_AHEAD = 30;
        private NetworkTimer networkTimer;
        private Vector2 cachedMove;
        private bool jumpQueued;
        private bool sprintHeld;
        private float cachedYaw;

        // ==========================================
        // CLIENT BUFFERS
        // ==========================================
        private PlayerInputPayload[] inputBuffer;
        private PlayerStatePayload[] stateBuffer;

        // ==========================================
        // SERVER STATE
        // ==========================================
        private Dictionary<int, PlayerInputPayload> pendingInputs;
        private int nextServerTick;
        private PlayerInputPayload lastInput;
        private bool hasLastInput;
        private bool hasStartedServerTicking;

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

        public int CurrentSimulationTick => networkTimer != null ? networkTimer.CurrentTick : 0;

        // ==========================================
        // INITIALIZATION
        // ==========================================

        public override void OnNetworkSpawn()
        {
            networkTimer = new NetworkTimer(TICK_DT);
            groundMask = LayerMask.GetMask("Ground");
            playerHealth = GetComponent<PlayerHealth>();

            if (IsOwner)
            {
                inputBuffer = new PlayerInputPayload[BUFFER_SIZE];
                stateBuffer = new PlayerStatePayload[BUFFER_SIZE];

                previousTickPosition = transform.position;
                currentTickPosition = transform.position;
            }

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

            cachedMove = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            );

            cachedMove = Vector2.ClampMagnitude(cachedMove, 1f);

            sprintHeld = !Input.GetKey(KeyCode.LeftShift);

            cachedYaw = mouseMovement != null
                ? mouseMovement.YRotation
                : transform.eulerAngles.y;

            if (InputManager.Instance != null
                ? InputManager.Instance.GetJumpInputDown()
                : Input.GetKeyDown(KeyCode.Space))
            {
                jumpQueued = true;
            }
        }

        private PlayerInputPayload BuildInputForTick(int tick)
        {
            var input = new PlayerInputPayload
            {
                tick = tick,
                move = cachedMove,
                jumpPressed = jumpQueued,
                sprint = sprintHeld,
                yaw = cachedYaw
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

            stateBuffer[index] = CaptureState(tick);

            if (IsServer)
            {
                // Host — đã simulate authoritative, gửi state luôn
                if (tick % STATE_SEND_EVERY_N_TICKS == 0)
                    SendStateClientRpc(CaptureState(tick));
            }
            else
            {
                SendInputServerRpc(input);
            }
        }

        // ==========================================
        // SERVER RPC — Nhận input từ client
        // ==========================================

        [ServerRpc(Delivery = RpcDelivery.Unreliable)]
        private void SendInputServerRpc(PlayerInputPayload input)
        {
            if (pendingInputs == null)
                pendingInputs = new Dictionary<int, PlayerInputPayload>();

            if (!TrySanitizeInput(input, nextServerTick, out PlayerInputPayload sanitized))
                return;

            if (hasStartedServerTicking && sanitized.tick < nextServerTick)
                return;

            pendingInputs[sanitized.tick] = sanitized;

            CleanupOldInputs();
        }

        public static bool TrySanitizeInput(PlayerInputPayload input, int nextExpectedTick, out PlayerInputPayload sanitized)
        {
            sanitized = input;

            if (!IsFinite(input.move.x) || !IsFinite(input.move.y) || !IsFinite(input.yaw))
                return false;

            if (input.tick < nextExpectedTick - MAX_INPUT_TICKS_BEHIND)
                return false;

            if (input.tick > nextExpectedTick + MAX_INPUT_TICKS_AHEAD)
                return false;

            sanitized.move = Vector2.ClampMagnitude(input.move, 1f);
            sanitized.yaw = Mathf.Repeat(input.yaw, 360f);
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

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // ==========================================
        // SERVER TICK
        // ==========================================

        private void ServerTick()
        {
            if (pendingInputs == null)
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

            if (pendingInputs.TryGetValue(nextServerTick, out var receivedInput))
            {
                input = receivedInput;
                pendingInputs.Remove(nextServerTick);
                lastInput = input;
                hasLastInput = true;
            }
            else
            {
                // Input trễ — repeat last (không jump)
                if (!hasLastInput)
                    return;

                input = lastInput;
                input.tick = nextServerTick;
                input.jumpPressed = false;
            }

            transform.rotation = Quaternion.Euler(0f, input.yaw, 0f);
            SimulateTick(input, TICK_DT);

            if (nextServerTick % STATE_SEND_EVERY_N_TICKS == 0)
                SendStateClientRpc(CaptureState(nextServerTick));

            nextServerTick++;
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

            var keysToRemove = new List<int>();
            foreach (var key in pendingInputs.Keys)
                if (key < nextServerTick - 100)
                    keysToRemove.Add(key);

            foreach (var key in keysToRemove)
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
            int index = serverState.tick % BUFFER_SIZE;
            var predicted = stateBuffer[index];

            // Bỏ qua nếu tick không khớp (state cũ hơn buffer)
            if (predicted.tick != serverState.tick)
                return;

            float error = Vector3.Distance(predicted.position, serverState.position);

            if (error < reconciliationThreshold)
                return;

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
            if (visualRoot != null)
            {
                ownerVisualCorrectionOffset += visualBefore - transform.position;

                // Cap offset để camera không bay quá xa
                if (ownerVisualCorrectionOffset.magnitude > maxVisualCorrectionOffset)
                    ownerVisualCorrectionOffset = ownerVisualCorrectionOffset.normalized * maxVisualCorrectionOffset;
            }
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

            if (groundMask.value != 0
                && Physics.CheckSphere(groundCheck.position, 0.4f, groundMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return Physics.CheckSphere(groundCheck.position, 0.4f, ~0, QueryTriggerInteraction.Ignore);
        }

        // ==========================================
        // STATE CAPTURE
        // ==========================================

        private PlayerStatePayload CaptureState(int tick)
        {
            return new PlayerStatePayload
            {
                tick = tick,
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
