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

        [Header("Movement")]
        [SerializeField] private float speed = 5f;
        [SerializeField] private float walkMultiplier = 2f;
        [SerializeField] private float jumpHeight = 0.5f;
        [SerializeField] private float gravityScale = 1f;

        [Header("CSP Settings")]
        [SerializeField] private float reconciliationThreshold = 0.05f;

        // ==========================================
        // CONSTANTS
        // ==========================================
        private const float GRAVITY = -9.81f;
        private const float TICK_DT = 1f / 60f;                // 60 Hz simulation
        private const int BUFFER_SIZE = 1024;
        private const int STATE_SEND_EVERY_N_TICKS = 2;         // 30 Hz state broadcast

        // ==========================================
        // CORE
        // ==========================================
        private NetworkTimer networkTimer;

        // ==========================================
        // FRAME-LEVEL INPUT CACHE (written in Update)
        // ==========================================
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

        // ==========================================
        // REMOTE INTERPOLATION
        // ==========================================
        private readonly List<PlayerStatePayload> stateSnapshots = new();
        private float interpolationTimer;

        // ==========================================
        // GAMEPLAY STATE (shared by simulation)
        // ==========================================
        private float verticalVelocity;
        private bool isGrounded;
        private Transform groundCheck;
        private LayerMask groundMask;

        // ==========================================
        // VISUAL INTERPOLATION (owner only)
        // ==========================================
        private Vector3 previousTickPosition;
        private Vector3 currentTickPosition;
        private bool hasTickedOnce;

        // ==========================================
        // INITIALIZATION
        // ==========================================

        public override void OnNetworkSpawn()
        {
            networkTimer = new NetworkTimer(TICK_DT);

            if (IsOwner)
            {
                inputBuffer = new PlayerInputPayload[BUFFER_SIZE];
                stateBuffer = new PlayerStatePayload[BUFFER_SIZE];

                groundCheck = CreateGroundCheck();
                groundMask = LayerMask.GetMask("Ground");
            }

            if (IsServer && !IsOwner)
            {
                pendingInputs = new Dictionary<int, PlayerInputPayload>();
                groundCheck = CreateGroundCheck();
                groundMask = LayerMask.GetMask("Ground");
            }

            if (!IsOwner && !IsServer)
            {
                // Pure remote client: block physics from pushing ghost player off walls
                if (controller != null) controller.enabled = false;
            }
        }

        private Transform CreateGroundCheck()
        {
            var go = new GameObject("GroundCheck").transform;
            go.SetParent(transform);
            go.localPosition = new Vector3(0, -0.09f, 0);
            return go;
        }

        // ==========================================
        // UPDATE — Frame layer (input, animation, remote interpolation)
        // ==========================================

        private void Update()
        {
            // --- Frame layer: read input ---
            if (IsOwner)
                CaptureFrameInput();

            // --- Tick layer: accumulate and process ---
            networkTimer.Accumulate(Time.deltaTime);

            if (IsOwner && hasTickedOnce)
            {
                transform.position = currentTickPosition;
            }

            while (networkTimer.CanTick())
            {
                networkTimer.ConsumeTick();

                if (IsServer && !IsOwner)
                    ServerTick();

                if (IsOwner)
                {
                    // Save position before this tick
                    previousTickPosition = transform.position;

                    ClientOwnerTick();

                    // Save position after this tick
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
            // Owner: smooth visual position between ticks at full framerate
            if (IsOwner && hasTickedOnce)
            {
                float alpha = networkTimer.Alpha;
                transform.position = Vector3.Lerp(previousTickPosition, currentTickPosition, alpha);
            }
        }

        // ==========================================
        // FRAME INPUT — Read every frame, consume per tick
        // ==========================================

        private void CaptureFrameInput()
        {
            cachedMove = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            );

            sprintHeld = !Input.GetKey(KeyCode.LeftShift);
            cachedYaw = mouseMovement != null ? mouseMovement.YRotation : transform.eulerAngles.y;

            // Queue jump — consumed exactly once in BuildInputForTick
            if (Input.GetKeyDown(KeyCode.Space))
                jumpQueued = true;
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

            jumpQueued = false; // consume exactly 1 tick
            return input;
        }

        // ==========================================
        // CLIENT OWNER TICK — Predict + buffer + send
        // ==========================================

        private void ClientOwnerTick()
        {
            int tick = networkTimer.CurrentTick;
            int index = tick % BUFFER_SIZE;

            // Build input for this tick
            var input = BuildInputForTick(tick);

            // Store input
            inputBuffer[index] = input;

            // Predict: simulate with fixed dt
            SimulateTick(input, TICK_DT);

            // Store predicted state
            stateBuffer[index] = CaptureState(tick);

            // Send input to server (or broadcast if host)
            if (IsServer)
            {
                // Host: we already simulated authoritatively
                if (tick % STATE_SEND_EVERY_N_TICKS == 0)
                    SendStateClientRpc(CaptureState(tick));
            }
            else
            {
                SendInputServerRpc(input);
            }
        }

        // ==========================================
        // SERVER TICK — Process expected tick, not "drain queue"
        // ==========================================

        [ServerRpc]
        private void SendInputServerRpc(PlayerInputPayload input)
        {
            // Store by tick, not queue
            pendingInputs[input.tick] = input;

            // Prevent unbounded growth
            if (pendingInputs.Count > 200)
            {
                // Remove oldest entries
                var keysToRemove = new List<int>();
                foreach (var key in pendingInputs.Keys)
                {
                    if (key < nextServerTick - 100)
                        keysToRemove.Add(key);
                }
                foreach (var key in keysToRemove)
                    pendingInputs.Remove(key);
            }
        }

        private bool hasStartedServerTicking = false;
        private const int SERVER_INPUT_BUFFER_TICKS = 3;

        private void ServerTick()
        {
            if (pendingInputs.Count < SERVER_INPUT_BUFFER_TICKS) return;
            
            if (!hasStartedServerTicking)
            {
                if (pendingInputs.Count > 0)
                {
                    int minTick = int.MaxValue;
                    foreach (var key in pendingInputs.Keys)
                    {
                        if (key < minTick) minTick = key;
                    }
                    nextServerTick = minTick;
                    hasStartedServerTicking = true;
                }
                else
                {
                    return; // Wait for the first payload
                }
            }

            PlayerInputPayload input;

            if (pendingInputs.TryGetValue(nextServerTick, out var receivedInput))
            {
                // Got the expected input for this tick
                input = receivedInput;
                pendingInputs.Remove(nextServerTick);
                lastInput = input;
            }
            else
            {
                // Missing input — repeat last input (no jump)
                input = lastInput;
                input.tick = nextServerTick;
                input.jumpPressed = false;
            }

            // Authoritative simulation — same function, same dt
            transform.rotation = Quaternion.Euler(0f, input.yaw, 0f);
            SimulateTick(input, TICK_DT);

            // Broadcast state at reduced rate
            if (nextServerTick % STATE_SEND_EVERY_N_TICKS == 0)
                SendStateClientRpc(CaptureState(nextServerTick));

            nextServerTick++;
        }

        // ==========================================
        // CLIENT RECEIVE STATE
        // ==========================================

        [ClientRpc]
        private void SendStateClientRpc(PlayerStatePayload state)
        {
            if (IsOwner && !IsServer)
            {
                Reconcile(state);
                return;
            }

            if (!IsOwner && !IsServer)
            {
                stateSnapshots.Add(state);

                // Keep buffer bounded
                while (stateSnapshots.Count > 10)
                    stateSnapshots.RemoveAt(0);
            }
        }

        // ==========================================
        // RECONCILIATION — Exact snap + replay
        // ==========================================

        private void Reconcile(PlayerStatePayload serverState)
        {
            int index = serverState.tick % BUFFER_SIZE;
            var predicted = stateBuffer[index];

            float error = Vector3.Distance(predicted.position, serverState.position);

            if (error < reconciliationThreshold)
                return;

            // EXACT snap to authoritative state — no lerp
            ApplyAuthoritativeState(serverState);

            // Replay all buffered inputs from server tick + 1 to current tick
            ReplayFrom(serverState.tick + 1);
        }

        private void ApplyAuthoritativeState(PlayerStatePayload state)
        {
            controller.enabled = false;
            transform.position = state.position;
            controller.enabled = true;

            verticalVelocity = state.verticalVelocity;
            isGrounded = state.grounded;
        }

        private void ReplayFrom(int startTick)
        {
            for (int tick = startTick; tick < networkTimer.CurrentTick; tick++)
            {
                int index = tick % BUFFER_SIZE;

                // Same simulation, same dt
                SimulateTick(inputBuffer[index], TICK_DT);

                // Update predicted state cache
                stateBuffer[index] = CaptureState(tick);
            }
        }

        // ==========================================
        // REMOTE INTERPOLATION — Render the past smoothly
        // ==========================================

        private void InterpolateRemote()
        {
            if (stateSnapshots.Count < 2) return;

            float sendInterval = STATE_SEND_EVERY_N_TICKS * TICK_DT;
            interpolationTimer += Time.deltaTime;
            float t = interpolationTimer / sendInterval;

            var from = stateSnapshots[0];
            var to = stateSnapshots[1];

            transform.position = Vector3.Lerp(from.position, to.position, t);
            transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(0f, from.yaw, 0f),
                Quaternion.Euler(0f, to.yaw, 0f),
                t
            );

            if (t >= 1f)
            {
                stateSnapshots.RemoveAt(0);
                interpolationTimer = 0f;
            }
        }

        // ==========================================
        // SIMULATION — The ONE function. Client = Server. Same dt.
        // ==========================================

        private void SimulateTick(PlayerInputPayload input, float dt)
        {
            if (controller == null || !controller.enabled) return;

            // Ground check
            if (groundCheck != null)
                isGrounded = Physics.CheckSphere(groundCheck.position, 0.4f, groundMask);

            // Reset vertical velocity when grounded
            if (isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f;

            // Jump — must check before gravity
            if (input.jumpPressed && isGrounded)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * GRAVITY);

            // Gravity
            verticalVelocity += GRAVITY * gravityScale * dt;

            // Horizontal movement
            Quaternion yawRot = Quaternion.Euler(0f, input.yaw, 0f);
            Vector3 move = yawRot * new Vector3(input.move.x, 0f, input.move.y);
            float currentSpeed = input.sprint ? speed : speed / walkMultiplier;

            // Single Move() per tick — combine horizontal + vertical
            Vector3 totalMove = move * currentSpeed;
            totalMove.y = verticalVelocity;
            controller.Move(totalMove * dt);
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
        // ANIMATION — Frame layer only
        // ==========================================

        private void UpdateAnimation()
        {
            if (characterAnimation == null) return;

            characterAnimation.SetBool("Grounded", isGrounded);
            characterAnimation.SetBool("FreeFall", !isGrounded && verticalVelocity < -2f);

            if (IsOwner)
            {
                float moveMagnitude = cachedMove.magnitude;
                float currentSpeed = sprintHeld ? speed : speed / walkMultiplier;
                characterAnimation.SetFloat("Speed", moveMagnitude * currentSpeed);
            }
        }
    }
}

/*using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace FPS
{
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private CharacterController controller;
        [SerializeField] private float speed = 5f;
        [SerializeField] private float walkMultiplier = 2f;
        [SerializeField] private float jumpHeight = 0.5f;
        [SerializeField] private float gravityScale = 1f;
        [SerializeField] private Animator characterAnimation;

        private float gravity = -9.81f;
        private Vector3 velocity;
        private bool isGrounded;
        private Transform groundCheck;

        public override void OnNetworkSpawn() { }

        private void Start()
        {
            if (!IsOwner) return;

            if (characterAnimation != null)
                characterAnimation.SetFloat("Speed", speed);

            groundCheck = new GameObject("GroundCheck").transform;
            groundCheck.SetParent(transform);
            groundCheck.localPosition = new Vector3(0, -0.09f, 0); 
        }

        private void Update()
        {
            if (!IsOwner) return;

            isGrounded = Physics.CheckSphere(groundCheck.position, 0.4f, LayerMask.GetMask("Ground"));
            
            if (characterAnimation != null)
                characterAnimation.SetBool("Grounded", isGrounded);

            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
                if (characterAnimation != null)
                {
                    characterAnimation.SetBool("FreeFall", false);
                    characterAnimation.SetBool("Jump", false);
                }
            }

            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");

            float currentSpeed = speed;
            if (Input.GetKey(KeyCode.LeftShift))
            {
                currentSpeed /= walkMultiplier;
            }

            Vector3 move = transform.right * moveX + transform.forward * moveZ;

            if (controller != null && controller.enabled)
            {
                controller.Move(move * currentSpeed * Time.deltaTime);
            }

            float moveMagnitude = new Vector2(moveX, moveZ).magnitude;
            if (characterAnimation != null)
                characterAnimation.SetFloat("Speed", moveMagnitude * currentSpeed);

            if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                if (characterAnimation != null)
                    characterAnimation.SetBool("Jump", true);
            }

            velocity.y += gravity * Time.deltaTime * gravityScale;
            
            if (controller != null && controller.enabled)
            {
                controller.Move(velocity * Time.deltaTime);
            }

            if (!isGrounded && velocity.y < -2f)
            {
                if (characterAnimation != null)
                    characterAnimation.SetBool("FreeFall", true);
            }
        }
    }
}*/