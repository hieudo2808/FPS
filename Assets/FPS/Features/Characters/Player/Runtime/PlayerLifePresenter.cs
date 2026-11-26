using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

namespace FPS
{
    /// <summary>
    /// Presentation of the replicated life state on every peer. Nothing here changes gameplay.
    ///   3P (seen by others)  Downed : legs give way, lands on the back, shoulders raised, sidearm aimed where the
    ///                                  player looks (Left 4 Dead style).
    ///                        Dead   : from Downed the body slumps flat; from standing a Valorant-style Death
    ///                                  clip chosen by the finishing hit (region and side), held on its last frame.
    ///                        Alive  : blends back to the normal character Animator.
    ///   1P (own camera)      Downed : the camera sinks to ground level; Dead hands over to the spectator camera.
    /// The clips play through a PlayableGraph on top of the character Animator, so the per-weapon controllers
    /// stay untouched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLifePresenter : MonoBehaviour
    {
        [SerializeField] private PlayerLifeAnimationSet animationSet;
        [Tooltip("The standing third-person sidearm hold of each character, copied onto the arms while downed.")]
        [SerializeField] private PlayerSidearmHoldPose sidearmHold;
        [Tooltip("Humanoid Avatar of this character, used while a life clip plays even if the weapon profile is Generic.")]
        [SerializeField] private Avatar humanoidAvatar;
        [SerializeField, Min(0.05f)] private float blendInSeconds = 0.15f;
        [SerializeField, Min(0.05f)] private float blendOutSeconds = 0.45f;
        [SerializeField, Min(0.05f)] private float raiseSeconds = 0.5f;
        [SerializeField, Min(0.05f)] private float slumpSeconds = 0.9f;
        [SerializeField, Range(30f, 180f)] private float aimArcDegrees = 100f;

        /// <summary>Evaluated after the character's own AnimatorController output, so the life clip wins.</summary>
        public const ushort OutputSortingOrder = 1000;

        enum Mode { None, Downed, Dead, Releasing }

        PlayerHealth health;
        WeaponManager weapons;
        PlayerVisibilityController visibility;
        MouseMovement mouse;
        NetworkObject network;
        Animator animator;
        PlayableGraph graph;
        AnimationClipPlayable playable;
        AnimationLayerMixerPlayable mixer;
        AnimationPlayableOutput output;
        AnimationClip clip;
        Mode mode;
        bool pending;
        bool presentedOnce;
        Vector3 lyingFacing;
        PlayerLifeState shown = PlayerLifeState.Alive;
        float weight, raise, arms;
        bool fromDowned;
        Avatar savedAvatar;
        bool savedRootMotion;
        Quaternion modelRestLocal;
        Quaternion lyingWorld;
        bool lyingFrozen;
        float feetYaw;
        Camera eye;
        Vector3 eyeRest;
        bool eyeCached;
        float eyeDrop;
        float eyeBlend;
        float eyeYaw;
        bool eyeDowned;
        // The 3P weapons hang from a generic bone the Humanoid clip does not drive; keep it in the right hand.
        Transform weaponMaster, rightHand;
        Vector3 weaponOffset;
        Quaternion weaponRotation;
        Vector3 barrelInRightHand;
        Vector3 sidearmWeaponOffset;
        Quaternion sidearmWeaponRotation;
        bool hasBarrelCalibration;
        Vector3 weaponRestPosition;
        Quaternion weaponRestRotation;
        bool weaponHeld;
        PlayerDownedPose downedPose;
        public const string KneeVolumeShape = "DownedKneeVolume";
        readonly System.Collections.Generic.List<(SkinnedMeshRenderer skin, int shape)> kneeMeshes = new();

        public bool IsOverridingCharacter => graph.IsValid();
        public string ActiveClipName => clip != null ? clip.name : string.Empty;
        public float RaiseWeight => raise;
        public float ArmsWeight => arms;
        public PlayerLifeAnimationSet AnimationSet => animationSet;
        public bool SlumpedFromDowned => fromDowned;
        /// <summary>How far the own first-person camera has sunk (metres).</summary>
        public float EyeDrop => eyeDrop;

        void Awake()
        {
            health = GetComponent<PlayerHealth>();
            weapons = GetComponent<WeaponManager>();
            visibility = GetComponent<PlayerVisibilityController>();
            mouse = GetComponent<MouseMovement>();
            network = GetComponent<NetworkObject>();
            CaptureBodyDefaults();
        }

        // The body's own default bone rotations, read before the Animator ever runs (the prefab values).
        readonly System.Collections.Generic.Dictionary<Transform, Quaternion> bodyDefaults = new();

        void CaptureBodyDefaults()
        {
            Animator body = weapons != null ? weapons.CharacterAnimation : null;
            var movement = GetComponent<PlayerMovement>();
            if (body == null && movement != null) body = movement.CharacterAnimation;
            if (body == null) return;
            foreach (var t in body.GetComponentsInChildren<Transform>(true))
                if (t.GetComponentInParent<Animator>(true) == body) bodyDefaults[t] = t.localRotation;
        }

        void OnEnable()
        {
            if (health != null) health.LifeStateChanged += OnLifeStateChanged;
            pending = true; // Late join or pool reuse: present whatever state is current.
        }

        void OnDisable()
        {
            if (health != null) health.LifeStateChanged -= OnLifeStateChanged;
            ReleaseImmediately();
            if (eyeCached && eye != null) eye.transform.localPosition = eyeRest;
            eyeDrop = 0f;
            eyeBlend = 0f;
            eyeDowned = false;
            mouse?.ClearLookConstraint();
        }

        void OnDestroy() => ReleaseImmediately();

        // The finishing hit is another variable of the same object; decide next LateUpdate when both arrived.
        void OnLifeStateChanged(PlayerLifeState previous, PlayerLifeState current) => pending = true;

        void LateUpdate()
        {
            if (health == null) return;
            if (pending)
            {
                pending = false;
                Present(health.LifeState);
            }
            UpdateThirdPerson(Time.deltaTime);
            UpdateFirstPerson(Time.deltaTime);
        }

        bool IsLocalView => network != null && network.IsSpawned && network.IsOwner;

        void Present(PlayerLifeState state)
        {
            PlayerLifeState previous = shown;
            bool first = !presentedOnce;
            presentedOnce = true;
            shown = state;
            if (IsLocalView || animationSet == null) return; // The own body is hidden; 1P is handled separately.
            switch (state)
            {
                case PlayerLifeState.Downed:
                    if (mode != Mode.Downed) BeginDowned(skipToEnd: first);
                    break;
                case PlayerLifeState.Dead:
                case PlayerLifeState.Spectating:
                    if (mode == Mode.Downed) { mode = Mode.Dead; fromDowned = true; } // Bled out: slump where it lies.
                    else if (mode != Mode.Dead && (state == PlayerLifeState.Dead || previous != PlayerLifeState.Alive))
                        BeginDeath(skipToEnd: first || previous == PlayerLifeState.Dead || previous == PlayerLifeState.Spectating);
                    break;
                default:
                    if (graph.IsValid()) mode = Mode.Releasing;
                    break;
            }
        }

        void BeginDowned(bool skipToEnd)
        {
            if (!Begin(animationSet.downedFall)) return;
            mode = Mode.Downed;
            fromDowned = false;
            raise = arms = 0f;
            // Turn the body so that, once it lies down, the feet point where the player looks. Measure the
            // lying pose at full weight, then go back; nothing renders in between.
            float blend = weight;
            output.SetWeight(1f);
            playable.SetTime(clip.length);
            graph.Evaluate();
            feetYaw = PlayerDownedPose.FeetYaw(animator, transform.forward);
            output.SetWeight(blend);
            playable.SetTime(skipToEnd ? clip.length : 0d);
            graph.Evaluate();
            if (skipToEnd) { raise = arms = 1f; weight = 1f; }
        }

        void BeginDeath(bool skipToEnd)
        {
            if (!Begin(animationSet.ChooseDeath(health.LifeHit))) return;
            mode = Mode.Dead;
            fromDowned = false;
            raise = arms = 0f;
            feetYaw = 0f;
            playable.SetTime(skipToEnd ? clip.length : 0d);
            if (skipToEnd) weight = 1f;
            lyingFrozen = false;
        }

        bool Begin(AnimationClip next)
        {
            animator = weapons != null ? weapons.CharacterAnimation : GetComponentInChildren<Animator>();
            if (next == null || animator == null) return false;
            if (!graph.IsValid())
            {
                CaptureWeaponGrip();
                if (visibility != null) visibility.SuspendCharacterProfile = true;
                savedAvatar = animator.avatar;
                savedRootMotion = animator.applyRootMotion;
                modelRestLocal = animator.transform.localRotation;
                animator.applyRootMotion = false;
                // Use the avatar verified to bind this body: a weapon profile may leave none (Generic) or one
                // built for a different hierarchy, and then the clip would not move the visible mesh.
                if (humanoidAvatar != null && savedAvatar != humanoidAvatar)
                {
                    animator.avatar = humanoidAvatar;
                    animator.Rebind();
                }
                CollectHelperBones();
                kneeMeshes.Clear();
                foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    int shape = skin.sharedMesh != null ? skin.sharedMesh.GetBlendShapeIndex(KneeVolumeShape) : -1;
                    if (shape >= 0) kneeMeshes.Add((skin, shape));
                }
                graph = PlayableGraph.Create(name + " life");
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                output = AnimationPlayableOutput.Create(graph, "life", animator);
                output.SetSortingOrder(OutputSortingOrder);
                mixer = AnimationLayerMixerPlayable.Create(graph, 1);
                output.SetSourcePlayable(mixer);
                mixer.SetInputWeight(0, 1f);
                ResolveSidearmHold();
                weight = 0f;
            }
            else if (playable.IsValid()) { graph.Disconnect(mixer, 0); playable.Destroy(); }
            clip = next;
            playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetDuration(clip.length);
            graph.Connect(playable, 0, mixer, 0);
            output.SetWeight(weight);
            lyingFrozen = false;
            graph.Play();
            return true;
        }

        void UpdateThirdPerson(float dt)
        {
            if (!graph.IsValid()) { SetKneeVolume(0f); return; }
            float length = clip != null ? clip.length : 0f;
            float time = (float)playable.GetTime();
            if (time >= length) { playable.SetTime(length); playable.SetSpeed(0d); } // Hold the last frame.
            float progress = length > 0f ? Mathf.Clamp01(time / length) : 1f;

            weight = mode == Mode.Releasing
                ? Mathf.MoveTowards(weight, 0f, dt / blendOutSeconds)
                : Mathf.MoveTowards(weight, 1f, dt / blendInSeconds);
            output.SetWeight(weight);

            switch (mode)
            {
                case Mode.Downed:
                    if (progress > 0.75f) raise = Mathf.MoveTowards(raise, 1f, dt / raiseSeconds);
                    arms = raise;
                    break;
                case Mode.Dead:
                case Mode.Releasing:
                    raise = Mathf.MoveTowards(raise, 0f, dt / (mode == Mode.Dead ? slumpSeconds : blendOutSeconds));
                    arms = Mathf.MoveTowards(arms, 0f, dt / (mode == Mode.Dead ? slumpSeconds * 0.5f : blendOutSeconds));
                    break;
            }

            // The fallen body stays where it fell when the player turns; only the arms follow the aim.
            Transform model = animator.transform;
            if (mode == Mode.Releasing)
                model.localRotation = Quaternion.Slerp(modelRestLocal, model.localRotation, weight);
            else if (!lyingFrozen)
            {
                model.localRotation = Quaternion.AngleAxis(feetYaw * Mathf.SmoothStep(0f, 1f, progress), Vector3.up) * modelRestLocal;
                if (progress >= 1f)
                {
                    lyingWorld = model.rotation;
                    lyingFacing = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                    lyingFrozen = true;
                }
            }
            else model.rotation = lyingWorld;

            RestHelperBones(weight);
            if (raise > 0f || arms > 0f)
            {
                downedPose ??= new PlayerDownedPose(animator);
                downedPose.ApplyBody(raise * weight, lyingFrozen ? lyingFacing : Vector3.ProjectOnPlane(transform.forward, Vector3.up));
                // Arms exactly as this character holds the sidearm standing, then the shoulders turn it onto the aim.
                ApplySidearmHold(arms * weight);
                Vector3 barrel = hasBarrelCalibration && rightHand != null ? rightHand.rotation * barrelInRightHand : Vector3.zero;
                downedPose.AimShoulders(AimDirection(), arms * weight, barrel);
            }
            RestoreKneeCorrectives();
            // shortcut: Baked for this downed leg pose; rebake if the leg pose or knee helper strategy changes.
            SetKneeVolume(raise * weight);
            HoldWeaponInHand();

            if (mode == Mode.Releasing && weight <= 0f) ReleaseImmediately();
        }

        /// <summary>Where the player looks, kept inside the arc in front of the fallen body.</summary>
        Vector3 AimDirection()
        {
            Vector3 look = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 feet = lyingFrozen ? lyingFacing : look;
            float angle = Mathf.Clamp(Vector3.SignedAngle(feet, look, Vector3.up), -aimArcDegrees * 0.5f, aimArcDegrees * 0.5f);
            return Quaternion.AngleAxis(angle, Vector3.up) * feet + Vector3.up * 0.12f;
        }

        static AvatarMask armsMask;

        /// <summary>Arms and fingers only: the sidearm hold must not touch the lying legs, hips or spine.</summary>
        public static AvatarMask ArmsMask()
        {
            if (armsMask != null) return armsMask;
            armsMask = new AvatarMask();
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                armsMask.SetHumanoidBodyPartActive(part, part is AvatarMaskBodyPart.LeftArm or AvatarMaskBodyPart.RightArm
                    or AvatarMaskBodyPart.LeftFingers or AvatarMaskBodyPart.RightFingers);
            return armsMask;
        }

        readonly System.Collections.Generic.List<(Transform bone, Vector3 position, Quaternion hold)> holdBones = new();

        /// <summary>Look up this character's standing sidearm hold (captured from its Classic controller).</summary>
        void ResolveSidearmHold()
        {
            holdBones.Clear();
            hasBarrelCalibration = false;
            if (sidearmHold == null || visibility == null) return;
            string controller = null;
            foreach (var presentation in visibility.ThirdPersonWeaponPresentations)
                if (presentation != null && presentation.WeaponData != null && presentation.WeaponData.usableWhileDowned && presentation.CharacterController != null)
                { controller = presentation.CharacterController.name; break; }
            var entry = controller != null ? sidearmHold.FindEntry(controller) : default;
            var bones = entry.bones;
            if (bones == null) return;
            barrelInRightHand = entry.barrelInRightHand;
            hasBarrelCalibration = barrelInRightHand.sqrMagnitude > 1e-6f;
            sidearmWeaponOffset = entry.weaponPositionInRightHand;
            sidearmWeaponRotation = entry.weaponRotationInRightHand;
            var byName = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var t in animator.GetComponentsInChildren<Transform>(true))
                if (t.GetComponentInParent<Animator>(true) == animator && !byName.ContainsKey(t.name)) byName[t.name] = t;
            foreach (var bone in bones)
                if (byName.TryGetValue(bone.name, out var t)) holdBones.Add((t, bone.localPosition, bone.localRotation));
        }

        void ApplySidearmHold(float amount)
        {
            if (amount <= 0f) return;
            if (hasBarrelCalibration && weaponHeld)
            {
                weaponOffset = sidearmWeaponOffset;
                weaponRotation = sidearmWeaponRotation;
            }
            foreach (var (bone, position, hold) in holdBones)
                if (bone != null)
                {
                    if (hasBarrelCalibration) bone.localPosition = Vector3.Lerp(bone.localPosition, position, amount);
                    bone.localRotation = Quaternion.Slerp(bone.localRotation, hold, amount);
                }
        }

        public int SidearmHoldBoneCount => holdBones.Count;

        readonly System.Collections.Generic.List<(Transform bone, Quaternion rest)> helperBones = new();
        readonly System.Collections.Generic.List<(Transform knee, Transform corrective, Quaternion kneeRest, Quaternion correctiveRest)> kneeCorrectives = new();

        /// <summary>
        /// The weapon profile's controller keeps animating the body's helper bones (twist, corrective, IK) for a
        /// standing pose; the Humanoid life clip only writes the human bones. Left alone the helpers bend the skin
        /// of a lying body. Collect every non-human body bone with its rest rotation from the avatar.
        /// </summary>
        void CollectHelperBones()
        {
            helperBones.Clear();
            kneeCorrectives.Clear();
            var avatar = animator.avatar;
            if (avatar == null || !avatar.isHuman) return;
            var description = avatar.humanDescription;
            var human = new System.Collections.Generic.HashSet<string>();
            foreach (var bone in description.human) human.Add(bone.boneName);
            var rest = new System.Collections.Generic.Dictionary<string, Quaternion>();
            foreach (var bone in description.skeleton) rest[bone.name] = bone.rotation;
            foreach (var t in animator.GetComponentsInChildren<Transform>(true))
            {
                if (t == animator.transform || human.Contains(t.name)) continue;
                // Prefer this body's own default (how the prefab looks at rest); the avatar's rest pose belongs to
                // the model the avatar was built from and flattens joints such as the knee.
                if (!bodyDefaults.TryGetValue(t, out var rotation) && !rest.TryGetValue(t.name, out rotation)) continue;
                if (t.GetComponentInParent<Animator>(true) != animator) continue; // Weapon models have their own Animator.
                if (weaponMaster != null && (t == weaponMaster || t.IsChildOf(weaponMaster))) continue;
                helperBones.Add((t, rotation));
            }
            foreach (var humanKnee in new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg })
            {
                Transform knee = animator.GetBoneTransform(humanKnee);
                if (knee == null || !bodyDefaults.TryGetValue(knee, out var kneeRest)) continue;
                Transform corrective = knee.Find(knee.name + "_Ndl");
                if (corrective != null && bodyDefaults.TryGetValue(corrective, out var correctiveRest))
                    kneeCorrectives.Add((knee, corrective, kneeRest, correctiveRest));
            }
        }

        /// <summary>Keep the knee corrective halfway between its authored rest and the bent knee.</summary>
        void RestoreKneeCorrectives()
        {
            foreach (var (knee, corrective, kneeRest, correctiveRest) in kneeCorrectives)
                if (knee != null && corrective != null)
                    corrective.localRotation = Quaternion.Inverse(knee.localRotation)
                        * Quaternion.Slerp(kneeRest, knee.localRotation, 0.5f) * correctiveRest;
        }

        void RestHelperBones(float amount)
        {
            if (amount <= 0f) return;
            int bent = 0;
            foreach (var (bone, rest) in helperBones)
            {
                if (bone == null) continue;
                if (Quaternion.Angle(bone.localRotation, rest) > 5f) bent++;
                bone.localRotation = Quaternion.Slerp(bone.localRotation, rest, amount);
            }
            HelperBonesBentByAnimator = bent;
        }

        void SetKneeVolume(float amount)
        {
            foreach (var (skin, shape) in kneeMeshes)
                if (skin != null) skin.SetBlendShapeWeight(shape, Mathf.Clamp01(amount) * 100f);
        }

        /// <summary>Helper bones the character Animator had bent more than 5 degrees off rest this frame (diagnostics).</summary>
        public int HelperBonesBentByAnimator { get; private set; }

        public int HelperBoneCount => helperBones.Count;

        /// <summary>Remember where the weapon bone sits relative to the right hand in the normal pose.</summary>
        void CaptureWeaponGrip()
        {
            weaponHeld = false;
            if (weaponMaster == null)
                foreach (var t in animator.GetComponentsInChildren<Transform>(true))
                    if (t.name == "R_WeaponMaster") { weaponMaster = t; break; }
            rightHand = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (rightHand == null && humanoidAvatar != null)
            {
                // Generic weapon profile: no humanoid binding yet, find the hand by the avatar's bone name.
                string handName = null;
                foreach (var bone in humanoidAvatar.humanDescription.human)
                    if (bone.humanName == "RightHand") { handName = bone.boneName; break; }
                if (handName != null)
                    foreach (var t in animator.GetComponentsInChildren<Transform>(true))
                        if (t.name == handName) { rightHand = t; break; }
            }
            if (weaponMaster == null || rightHand == null || weaponMaster == rightHand) return;
            weaponOffset = Quaternion.Inverse(rightHand.rotation) * (weaponMaster.position - rightHand.position);
            weaponRotation = Quaternion.Inverse(rightHand.rotation) * weaponMaster.rotation;
            weaponRestPosition = weaponMaster.localPosition;
            weaponRestRotation = weaponMaster.localRotation;
            weaponHeld = true;
        }

        void HoldWeaponInHand()
        {
            if (!weaponHeld || weaponMaster == null) return;
            // The avatar may have changed when the clip started; look the hand up on the live binding.
            Transform hand = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand == null) return;
            weaponMaster.SetPositionAndRotation(hand.position + hand.rotation * weaponOffset, hand.rotation * weaponRotation);
        }

        void ReleaseImmediately()
        {
            SetKneeVolume(0f);
            kneeMeshes.Clear();
            downedPose?.Dispose();
            downedPose = null;
            if (graph.IsValid()) graph.Destroy();
            if (weaponHeld && weaponMaster != null)
            {
                weaponMaster.localPosition = weaponRestPosition;
                weaponMaster.localRotation = weaponRestRotation;
            }
            weaponHeld = false;
            if (animator != null && (mode != Mode.None || clip != null))
            {
                animator.transform.localRotation = modelRestLocal;
                animator.applyRootMotion = savedRootMotion;
                if (animator.avatar != savedAvatar)
                {
                    animator.avatar = savedAvatar;
                    animator.Rebind();
                }
            }
            mode = Mode.None;
            clip = null;
            raise = arms = weight = 0f;
            lyingFrozen = false;
            if (visibility != null)
            {
                visibility.SuspendCharacterProfile = false;
                if (weapons != null) visibility.RefreshWeaponPresentation(weapons.CurrentWeaponIndex);
            }
        }

        void UpdateFirstPerson(float dt)
        {
            if (!IsLocalView || animationSet == null) return;
            if (!eyeCached)
            {
                eye = mouse != null ? mouse.BodyCam : null;
                if (eye == null) return;
                eyeRest = mouse != null ? mouse.BodyCamRestLocalPosition : eye.transform.localPosition;
                eyeCached = true;
            }
            bool downed = health.LifeState == PlayerLifeState.Downed;
            if (downed && !eyeDowned)
            {
                // Lie down facing the way the player looked; from now on they cannot turn their back.
                eyeDowned = true;
                eyeYaw = mouse != null ? mouse.YRotation : transform.eulerAngles.y;
                mouse?.SetLookConstraint(eyeYaw, animationSet.downedYawHalfArc, animationSet.downedPitchUp, animationSet.downedPitchDown);
            }
            eyeBlend = Mathf.MoveTowards(eyeBlend, downed ? 1f : 0f, dt / (downed ? 0.6f : 0.45f));
            float k = Mathf.SmoothStep(0f, 1f, eyeBlend);

            Transform parent = eye.transform.parent;
            Vector3 headBobLocal = mouse != null ? mouse.HeadBobCameraPositionOffset * (1f - k) : Vector3.zero;
            Vector3 standing = parent != null ? parent.TransformPoint(eyeRest + headBobLocal) : (eyeRest + headBobLocal);
            Vector3 lyingFacing = Quaternion.Euler(0f, eyeYaw, 0f) * Vector3.forward;
            Vector3 lying = transform.position + Vector3.up * animationSet.downedEyeHeight - lyingFacing * animationSet.downedHeadBack;
            // Local, so the camera still follows its parent if that moves later this frame.
            eye.transform.localPosition = parent != null ? parent.InverseTransformPoint(Vector3.Lerp(standing, lying, k)) : Vector3.Lerp(standing, lying, k);
            Vector3 standingRest = parent != null ? parent.TransformPoint(eyeRest) : eyeRest;
            eyeDrop = standingRest.y - eye.transform.position.y;

            // Head resting on one side, chin up a little, slow laboured breathing.
            float t = Time.time;
            mouse?.SetLookOffset(new Vector3(
                (-6f + Mathf.Sin(t * 1.7f) * 0.9f) * k,
                Mathf.Sin(t * 0.6f) * 0.6f * k,
                (animationSet.downedRoll + Mathf.Sin(t * 0.9f) * 1.2f) * k));

            if (!downed && eyeBlend <= 0f && eyeDowned)
            {
                eyeDowned = false;
                eye.transform.localPosition = eyeRest;
                mouse?.ClearLookConstraint();
            }
        }
    }
}
