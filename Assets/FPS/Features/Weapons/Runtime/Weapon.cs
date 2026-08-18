using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FPS
{
    public class Weapon : MonoBehaviour
    {
        [Header("Instance References")]
        [SerializeField] private Transform bulletSpawnPoint;
        [SerializeField] private GameObject muzzleEffect;
        [Tooltip("Animator for FPS arms (FirstPersonArms).")]
        [SerializeField] private Animator fpsArmsAnimator;
        [Tooltip("Animator on the weapon model itself.")]
        [SerializeField] private Animator weaponAnimator;

        [Header("Physical ADS")]
        [Tooltip("Authored sight socket on the weapon. Do not derive this from the muzzle.")]
        [SerializeField] private Transform aimSight;
        [Tooltip("End marker defining the authored sight axis. Used for validation only; ADS never rotates Hand at runtime.")]
        [SerializeField] private Transform aimSightEnd;
        [Tooltip("Where the sight point sits relative to the body camera while aimed.")]
        [SerializeField] private Vector3 aimedSightCameraLocalPosition = new Vector3(0f, 0f, 0.12f);

        [Header("Weapon Data")]
        [SerializeField] private WeaponData weaponData;

        private RecoilController recoilController;
        private PlayerCombatTelemetry combatTelemetry;
        private WeaponFireHandler cachedFireHandler;
        private WeaponManager cachedWeaponManager;
        private Camera cachedCamera;
        private PlayerMovement cachedPlayerMovement;
        private PlayerHealth cachedPlayerHealth;
        private MouseMovement cachedMouseMovement;
        private Camera cachedWeaponCamera;
        private ushort fireSequence;

        [Header("Bullet Pooling")]
        [Tooltip("Optional pool for bulletPrefab. Empty uses Instantiate/Destroy fallback.")]
        [SerializeField] private ObjectPooling bulletPool;
        [SerializeField] private bool allowInstantiateFallbackInDevelopment = true;

        [Header("Magazine Visuals")]
        [Tooltip("Magazine currently attached to the gun.")]
        [SerializeField] private GameObject magazineOnGun;
        [Tooltip("Magazine shown in hand during reload.")]
        [SerializeField] private GameObject magazineInHand;

        private int currentAmmo;
        private int reservedAmmo;

        private bool canShoot = true;
        private bool isReloading = false;
        private bool isOwner = false;
        private Coroutine burstCoroutine;
        private Coroutine reloadCoroutine;
        private Coroutine fpsAnimationCoroutine;
        private readonly List<AnimatorClipInfo> gunClipInfoBuffer = new List<AnimatorClipInfo>(1);
        private readonly List<AnimatorClipInfo> armsClipInfoBuffer = new List<AnimatorClipInfo>(1);
        private readonly Queue<GameObject> liveSurfaceImpacts = new Queue<GameObject>(64);
        private double authoritativeEquipCompleteTime = -1.0;
        private float nextReloadRequestTime;
        private bool isInspecting;
        private int remainingPerShellPresentationInserts;
        private bool continuousFirePresentationActive;
        private bool aimPresentationInitialized;
        private bool scopePresentationVisible;
        private bool aimRequested;
        private bool aimButtonWasPressed;
        private float aimBlend;
        private Transform viewmodelRoot;
        private bool viewmodelHipPoseCached;
        private Vector3 hipViewmodelLocalPosition;
        private Quaternion hipViewmodelLocalRotation;
        private Vector3 aimedViewmodelLocalPosition;
        private Quaternion aimedViewmodelLocalRotation;
        private float unscopedWorldFov = 60f;
        // Fire requests are server-authoritative. Keep a small local reservation
        // so holding the trigger cannot visually overrun the magazine while the
        // acknowledgement is in flight, but do not mutate authoritative ammo
        // until WeaponOwnerState confirms the shot.
        private int pendingServerShots;

        public int CurrentAmmo => currentAmmo;
        public int ReservedAmmo => reservedAmmo;
        public Sprite WeaponIcon => weaponData != null ? weaponData.weaponIcon : null;
        public WeaponData Data => weaponData;
        public Transform BulletSpawnPoint => bulletSpawnPoint;
        public Animator FpsArmsAnimator => fpsArmsAnimator;
        public Animator WeaponAnimator => weaponAnimator;
        public bool IsAiming => scopePresentationVisible;
        public bool IsAimRequested => aimRequested;
        public Transform AimSight => aimSight;
        public Transform AimSightEnd => aimSightEnd;

        private void Start()
        {
            if (weaponData == null)
            {
                currentAmmo = 0;
                reservedAmmo = 0;
                canShoot = false;
                ReportCombatTelemetry();
                return;
            }

            currentAmmo  = weaponData.magazineSize;
            reservedAmmo = weaponData.totalAmmo - currentAmmo;
            ReportCombatTelemetry();
        }

        /// <summary>
        /// Called by WeaponManager.OnNetworkSpawn() to avoid Start()/OnNetworkSpawn race conditions.
        /// </summary>
        public void SetOwner(bool owner)
        {
            bool lostOwnership = isOwner && !owner;
            isOwner = owner;
            if (lostOwnership)
                ForceExitAimPresentation();
            if (isOwner)
            {
                CacheOwnerDependencies();
                aimButtonWasPressed = InputManager.Instance != null
                    && InputManager.Instance.GetAimInput();
                ConfigureFpLayer();
                ReportCombatTelemetry();
            }
        }

        public void PrepareFirstPersonAnimation()
        {
            PrepareFirstPersonPresentation(false);
        }

        public void BindFirstPersonPresentation(Camera aimCamera, Animator armsAnimator)
        {
            if (aimCamera != null)
                cachedCamera = aimCamera;
            if (armsAnimator != null)
            {
                fpsArmsAnimator = armsAnimator;
                Transform candidateRoot = armsAnimator.transform.parent;
                if (candidateRoot != null && candidateRoot != viewmodelRoot)
                {
                    viewmodelRoot = candidateRoot;
                    CacheViewmodelHipPose();
                }
            }
        }

        public void PrepareFirstPersonPresentation(bool playEquip, float normalizedEquipTime = 0f)
        {
            ConfigureFpLayer(true);
            isInspecting = false;
            if (playEquip)
                PlayEquipAtNormalizedTime(normalizedEquipTime);
        }

        private void OnEnable()
        {
            canShoot    = weaponData != null;
            isReloading = false;
            burstCoroutine = null;
            reloadCoroutine = null;
            pendingServerShots = 0;
            remainingPerShellPresentationInserts = 0;
            continuousFirePresentationActive = false;

            if (isOwner)
            {
                CacheOwnerDependencies();
                aimButtonWasPressed = InputManager.Instance != null
                    && InputManager.Instance.GetAimInput();
            }
        }

        private void OnDisable()
        {
            ForceExitAimPresentation();
            StopAllCoroutines();
            fpsAnimationCoroutine = null;
            reloadCoroutine = null;
            isReloading = false;
            canShoot    = true;
            pendingServerShots = 0;
            remainingPerShellPresentationInserts = 0;
            continuousFirePresentationActive = false;
            ReportCombatTelemetry();
        }

        private void Update()
        {
            if (!isOwner) return;
            if (weaponData == null) return;

            UpdateAimPresentation();

            if (TryInterruptPerShellReloadWithFire())
            {
                UpdatePerShellReloadPresentation();
                return;
            }

            if (canShoot && !isReloading && !IsEquipping())
            {
                if (pendingServerShots == 0 && currentAmmo <= 0 && reservedAmmo > 0)
                    ReloadWeapon();

                HandleFire();
            }

            UpdatePerShellReloadPresentation();
        }

        private bool TryInterruptPerShellReloadWithFire()
        {
            if (!isReloading
                || weaponData.reloadMode != ReloadMode.PerShell
                || IsEquipping()
                || currentAmmo - pendingServerShots <= 0
                || !NetworkMatchStateManager.IsGameplayActive
                || InputManager.Instance == null
                || !InputManager.Instance.GetFireInputDown())
                return false;

            if (reloadCoroutine != null)
            {
                StopCoroutine(reloadCoroutine);
                reloadCoroutine = null;
            }

            // This is prediction only. The authoritative server performs the
            // same reload cancellation atomically with ammo consumption.
            isReloading = false;
            remainingPerShellPresentationInserts = 0;
            canShoot = true;
            InsertMagazine();
            StartCoroutine(ShootCooldown());
            ReportCombatTelemetry();
            return true;
        }

        private void LateUpdate()
        {
            if (!isOwner || weaponData == null)
                return;

            UpdateContinuousFirePresentation();
        }

        private void UpdateContinuousFirePresentation()
        {
            if (weaponData == null || weaponData.restartFireAnimationPerShot)
                return;

            bool shouldPlay = !isReloading
                && !IsEquipping()
                && currentAmmo - pendingServerShots > 0
                && NetworkMatchStateManager.IsGameplayActive
                && InputManager.Instance != null
                && InputManager.Instance.GetFireInput();
            int fpLayer = fpsArmsAnimator != null
                ? fpsArmsAnimator.GetLayerIndex(GetFirstPersonLayerName())
                : -1;
            bool gunIsFiring = IsAnimatorInState(weaponAnimator, 0, "Fire");
            bool handsAreFiring = IsAnimatorInState(fpsArmsAnimator, fpLayer, "Fire");

            if (shouldPlay)
            {
                if (!gunIsFiring || !handsAreFiring)
                    PlayContinuousFireAtStart(fpLayer);
                continuousFirePresentationActive = true;
                SynchronizeContinuousFireFromGun(fpLayer);
                return;
            }

            if ((continuousFirePresentationActive || gunIsFiring) && weaponAnimator != null)
                weaponAnimator.Play("Idle", 0, 0f);
            if ((continuousFirePresentationActive || handsAreFiring) && fpsArmsAnimator != null && fpLayer >= 0)
                fpsArmsAnimator.Play("Idle", fpLayer, 0f);
            continuousFirePresentationActive = false;
        }

        private void PlayContinuousFireAtStart(int fpLayer)
        {
            if (weaponAnimator != null)
                weaponAnimator.Play("Fire", 0, 0f);
            if (fpsArmsAnimator != null && fpLayer >= 0)
                fpsArmsAnimator.Play("Fire", fpLayer, 0f);
            PlayStateAtSourceFrame(
                weaponAnimator, 0, "Fire", weaponData.fireLoopStartFrame, gunClipInfoBuffer);
            PlayStateAtSourceFrame(
                fpsArmsAnimator, fpLayer, "Fire", weaponData.fireLoopStartFrame, armsClipInfoBuffer);
        }

        private void SynchronizeContinuousFireFromGun(int fpLayer)
        {
            if (!TryGetStateClip(weaponAnimator, 0, "Fire", gunClipInfoBuffer,
                    out AnimatorStateInfo gunState, out AnimationClip gunClip))
                return;
            if (!TryGetStateClip(fpsArmsAnimator, fpLayer, "Fire", armsClipInfoBuffer,
                    out _, out AnimationClip handsClip))
                return;

            int gunLastFrame = Mathf.Max(1, Mathf.RoundToInt(gunClip.length * gunClip.frameRate));
            int gunStart = Mathf.Clamp(weaponData.fireLoopStartFrame, 0, gunLastFrame - 1);
            int gunEnd = weaponData.fireLoopEndFrame > gunStart
                ? Mathf.Clamp(weaponData.fireLoopEndFrame, gunStart + 1, gunLastFrame)
                : gunLastFrame;
            float gunSourceFrame = gunState.normalizedTime * gunLastFrame;
            if (gunSourceFrame >= gunEnd)
            {
                PlayContinuousFireAtStart(fpLayer);
                return;
            }

            float phase = Mathf.InverseLerp(gunStart, gunEnd, gunSourceFrame);
            int handsLastFrame = Mathf.Max(1, Mathf.RoundToInt(handsClip.length * handsClip.frameRate));
            int handsStart = Mathf.Clamp(weaponData.fireLoopStartFrame, 0, handsLastFrame - 1);
            int handsEnd = weaponData.fireLoopEndFrame > handsStart
                ? Mathf.Clamp(weaponData.fireLoopEndFrame, handsStart + 1, handsLastFrame)
                : handsLastFrame;
            float handsSourceFrame = Mathf.Lerp(handsStart, handsEnd, phase);
            fpsArmsAnimator.Play("Fire", fpLayer, handsSourceFrame / handsLastFrame);
        }

        private void UpdatePerShellReloadPresentation()
        {
            if (weaponData == null || weaponData.reloadMode != ReloadMode.PerShell
                || !isReloading || remainingPerShellPresentationInserts <= 0)
                return;

            int fpLayer = fpsArmsAnimator != null
                ? fpsArmsAnimator.GetLayerIndex(GetFirstPersonLayerName())
                : -1;
            bool gunReachedEnd = HasReachedSourceFrame(
                weaponAnimator, 0, "Reload", weaponData.reloadLoopStartFrame,
                weaponData.reloadLoopEndFrame, gunClipInfoBuffer);
            bool armsReachedEnd = HasReachedSourceFrame(
                fpsArmsAnimator, fpLayer, "Reload", weaponData.reloadLoopStartFrame,
                weaponData.reloadLoopEndFrame, armsClipInfoBuffer);
            if (!gunReachedEnd && !armsReachedEnd)
                return;

            remainingPerShellPresentationInserts--;
            if (remainingPerShellPresentationInserts <= 0)
                return;

            PlayStateAtSourceFrame(
                weaponAnimator, 0, "Reload", weaponData.reloadLoopStartFrame, gunClipInfoBuffer);
            PlayStateAtSourceFrame(
                fpsArmsAnimator, fpLayer, "Reload", weaponData.reloadLoopStartFrame, armsClipInfoBuffer);
        }

        private static bool HasReachedSourceFrame(
            Animator animator,
            int layer,
            string stateName,
            int startFrame,
            int endFrame,
            List<AnimatorClipInfo> clipBuffer)
        {
            if (!TryGetStateClip(animator, layer, stateName, clipBuffer,
                    out AnimatorStateInfo stateInfo, out AnimationClip clip))
                return false;

            int lastFrame = Mathf.Max(1, Mathf.RoundToInt(clip.length * clip.frameRate));
            int clampedStart = Mathf.Clamp(startFrame, 0, lastFrame - 1);
            int clampedEnd = endFrame > clampedStart
                ? Mathf.Clamp(endFrame, clampedStart + 1, lastFrame)
                : lastFrame;
            return stateInfo.normalizedTime * lastFrame >= clampedEnd;
        }

        private static void PlayStateAtSourceFrame(
            Animator animator,
            int layer,
            string stateName,
            int sourceFrame,
            List<AnimatorClipInfo> clipBuffer)
        {
            if (!TryGetStateClip(animator, layer, stateName, clipBuffer,
                    out _, out AnimationClip clip))
                return;

            int lastFrame = Mathf.Max(1, Mathf.RoundToInt(clip.length * clip.frameRate));
            float normalizedTime = Mathf.Clamp(sourceFrame, 0, lastFrame) / (float)lastFrame;
            animator.Play(stateName, layer, normalizedTime);
        }

        private static bool TryGetStateClip(
            Animator animator,
            int layer,
            string stateName,
            List<AnimatorClipInfo> clipBuffer,
            out AnimatorStateInfo stateInfo,
            out AnimationClip clip)
        {
            stateInfo = default;
            clip = null;
            if (animator == null || layer < 0 || layer >= animator.layerCount)
                return false;

            stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            if (!stateInfo.IsName(stateName))
                return false;

            clipBuffer.Clear();
            animator.GetCurrentAnimatorClipInfo(layer, clipBuffer);
            float greatestWeight = float.MinValue;
            for (int index = 0; index < clipBuffer.Count; index++)
            {
                AnimatorClipInfo candidate = clipBuffer[index];
                if (candidate.clip != null && candidate.weight > greatestWeight)
                {
                    greatestWeight = candidate.weight;
                    clip = candidate.clip;
                }
            }

            return clip != null;
        }

        private void HandleFire()
        {
            if (weaponData == null) return;
            if (isReloading) return;
            if (IsEquipping()) return;
            if (!NetworkMatchStateManager.IsGameplayActive) return;

            if (currentAmmo - pendingServerShots <= 0 && reservedAmmo == 0)
            {
                canShoot = false;
                return;
            }

            if (InputManager.Instance != null && InputManager.Instance.GetReloadInput() && currentAmmo < weaponData.magazineSize && reservedAmmo > 0 && !isReloading)
            {
                ReloadWeapon();
                return;
            }

            switch (weaponData.fireMode)
            {
                case FireMode.Single:
                    if (InputManager.Instance != null && InputManager.Instance.GetFireInputDown() && canShoot)
                        StartCoroutine(ShootCooldown());
                    break;

                case FireMode.Auto:
                    if (InputManager.Instance != null && InputManager.Instance.GetFireInput() && canShoot)
                        StartCoroutine(ShootCooldown());
                    break;

                case FireMode.Burst:
                    if (InputManager.Instance != null && InputManager.Instance.GetFireInputDown() && burstCoroutine == null)
                        burstCoroutine = StartCoroutine(FireBurst());
                    break;
            }
        }

        private IEnumerator ShootCooldown()
        {
            if (weaponData == null || currentAmmo <= 0)
            {
                canShoot = true;
                yield break;
            }

            canShoot = false;
            try
            {
                FireBullet();
            }
            catch (System.Exception ex)
            {
                GameLog.Error($"[Weapon] Exception during FireBullet: {ex.Message}\n{ex.StackTrace}");
            }

            yield return new WaitForSeconds(weaponData.FireInterval);
            canShoot = true;
        }

        private IEnumerator FireBurst()
        {
            if (weaponData == null)
                yield break;

            canShoot = false;
            int bulletsLeft = weaponData.burstCount;

            while (bulletsLeft > 0 && currentAmmo > 0)
            {
                try
                {
                    FireBullet();
                }
                catch (System.Exception ex)
                {
                    GameLog.Error($"[Weapon] Exception during FireBurst bullet: {ex.Message}\n{ex.StackTrace}");
                }
                bulletsLeft--;

                if (bulletsLeft > 0)
                    yield return new WaitForSeconds(weaponData.FireInterval);
            }

            yield return new WaitForSeconds(weaponData.FireInterval);
            burstCoroutine = null;
            canShoot = true;
        }

        private void FireBullet()
        {
            if (weaponData == null) return;
            if (IsEquipping() || isReloading) return;
            if (currentAmmo - pendingServerShots <= 0) return;

            PlayerMovement movement = cachedFireHandler != null
                ? cachedFireHandler.GetComponent<PlayerMovement>()
                : null;
            if (movement == null || !movement.TryGetConfirmedFireReference(
                    out uint inputSequence, out int serverTick))
            {
                return;
            }

            if (recoilController != null && weaponData.recoilPattern != null)
                recoilController.Fire(weaponData.recoilPattern);

            Camera cam = ResolveAimCamera();
            if (cam == null) return;

            // The shot keeps the scoped accuracy of the input sample that fired it,
            // then starts lowering the physical scope so the owner can see the
            // weapon/arms Fire (bolt/round) animation without a transform snap.
            bool aimed = aimRequested;
            if (aimed && weaponData.exitAimAfterShot)
                BeginExitAimAfterShot();

            PlayMuzzleEffect();
            isInspecting = false;
            if (weaponData.restartFireAnimationPerShot)
                TriggerAnimation("Fire");
            else if (!continuousFirePresentationActive)
            {
                int fpLayer = fpsArmsAnimator != null
                    ? fpsArmsAnimator.GetLayerIndex(GetFirstPersonLayerName())
                    : -1;
                PlayContinuousFireAtStart(fpLayer);
                continuousFirePresentationActive = true;
            }
            if (weaponData.shootSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(weaponData.shootSound);

            Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            ushort sequence = unchecked(++fireSequence);
            ulong ownerClientId = cachedFireHandler != null ? cachedFireHandler.OwnerClientId : 0;
            byte weaponSlot = (byte)Mathf.Clamp(
                cachedWeaponManager != null ? cachedWeaponManager.CurrentWeaponIndex : 0,
                0,
                byte.MaxValue);
            SpawnVisualProjectiles(
                centerRay.origin,
                centerRay.direction,
                sequence,
                ownerClientId,
                weaponSlot,
                aimed);

            bool networkRequest = cachedFireHandler != null
                && cachedFireHandler.IsSpawned
                && cachedFireHandler.NetworkManager != null
                && cachedFireHandler.NetworkManager.IsListening;
            if (networkRequest)
            {
                pendingServerShots++;
                cachedFireHandler.RequestFireServerRpc(new FireCommand
                {
                    sequence = sequence,
                    estimatedServerTick = serverTick,
                    inputSequence = inputSequence,
                    weaponSlot = weaponSlot,
                });
            }
            else
            {
                // Offline/editor fallback has no server to acknowledge the shot.
                currentAmmo = Mathf.Max(0, currentAmmo - 1);
                ReportCombatTelemetry();
                if (HUDManager.HasInstance)
                    HUDManager.Instance.UpdateAmmoInfo();
            }
        }

        public void SpawnVisualBullet(Vector3 position, Vector3 direction)
        {
            if (weaponData == null || weaponData.bulletPrefab == null) return;
            if (direction.sqrMagnitude <= 0.0001f) return;

            GameObject bulletInstance;
            if (bulletPool != null)
            {
                bulletInstance = bulletPool.GetObject();
                if (bulletInstance == null) return;
            }
            else if (CanUseInstantiateFallback())
            {
                GameLog.Warning(() => $"[Weapon] Bullet pool is missing for {name}; using editor/development Instantiate fallback.");
                bulletInstance = Instantiate(weaponData.bulletPrefab);
            }
            else
            {
                GameLog.Warning(() => $"[Weapon] Bullet pool is missing for {name}; visual bullet skipped in release path.");
                return;
            }

            VisualBulletProjectile projectile = bulletInstance.GetComponent<VisualBulletProjectile>();
            if (projectile == null)
            {
                GameLog.Error($"[Weapon] Visual bullet prefab '{weaponData.bulletPrefab.name}' is missing {nameof(VisualBulletProjectile)}.");
                if (bulletPool != null)
                    bulletPool.ReturnObject(bulletInstance);
                else
                    Destroy(bulletInstance);
                return;
            }

            projectile.Launch(
                position,
                direction,
                weaponData.bulletSpeed,
                weaponData.bulletLiveTime,
                bulletPool);
        }

        public void SpawnVisualProjectiles(
            Vector3 aimOrigin,
            Vector3 aimDirection,
            ushort shotSequence,
            ulong ownerClientId,
            byte weaponSlot,
            bool aimed)
        {
            if (weaponData == null || weaponData.bulletPrefab == null)
                return;
            if (aimDirection.sqrMagnitude <= 0.0001f)
                return;

            Vector3 muzzlePosition = bulletSpawnPoint != null
                ? bulletSpawnPoint.position
                : transform.position;
            float maximumRange = Mathf.Max(0.01f, weaponData.maximumRange);
            float spreadAngle = weaponData.GetSpreadAngle(aimed);
            uint shotSeed = WeaponBallistics.BuildShotSeed(ownerClientId, shotSequence, weaponSlot);
            int projectileCount = Mathf.Max(1, weaponData.projectileCount);
            int hitMask = GetVisualHitMask();

            for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
            {
                Vector3 gameplayDirection = WeaponBallistics.GetProjectileDirection(
                    aimDirection,
                    spreadAngle,
                    shotSeed,
                    projectileIndex);
                var gameplayRay = new Ray(aimOrigin, gameplayDirection);
                bool hitFound = Physics.Raycast(
                    gameplayRay,
                    out RaycastHit hit,
                    maximumRange,
                    hitMask,
                    QueryTriggerInteraction.Ignore);
                Vector3 targetPoint = hitFound
                    ? hit.point
                    : gameplayRay.GetPoint(maximumRange);
                Vector3 visualDirection = targetPoint - muzzlePosition;
                if (visualDirection.sqrMagnitude <= 0.0001f)
                    visualDirection = gameplayDirection;

                SpawnVisualBullet(muzzlePosition, visualDirection.normalized);
                if (hitFound)
                    SpawnSurfaceImpact(hit, shotSeed, projectileIndex);
            }
        }

        private void SpawnSurfaceImpact(RaycastHit hit, uint shotSeed, int projectileIndex)
        {
            if (weaponData == null || weaponData.surfaceImpactPrefab == null || hit.collider == null)
                return;

            // Character feedback is handled by hit markers/reactions. A stone decal
            // on a living hitbox looks wrong and can reveal rewound server poses.
            if (hit.collider.GetComponentInParent<HitboxSegment>() != null ||
                hit.collider.GetComponentInParent<IDamageable>() != null)
                return;

            Vector3 normal = hit.normal.sqrMagnitude > 0.0001f
                ? hit.normal.normalized
                : -transform.forward;
            uint rollSeed = shotSeed + (uint)(projectileIndex * 73);
            float roll = rollSeed % 360u;
            Quaternion rotation = Quaternion.LookRotation(normal) *
                Quaternion.AngleAxis(roll, Vector3.forward);
            GameObject impact = Instantiate(
                weaponData.surfaceImpactPrefab,
                hit.point + normal * 0.003f,
                rotation);
            impact.name = $"{weaponData.weaponName}_SurfaceImpact";

            while (liveSurfaceImpacts.Count > 0 && liveSurfaceImpacts.Peek() == null)
                liveSurfaceImpacts.Dequeue();
            liveSurfaceImpacts.Enqueue(impact);

            int cap = Mathf.Max(1, weaponData.maxConcurrentSurfaceImpacts);
            while (liveSurfaceImpacts.Count > cap)
            {
                GameObject oldest = liveSurfaceImpacts.Dequeue();
                if (oldest != null)
                    Destroy(oldest);
            }
        }

        private IEnumerator ReturnBulletToPool(GameObject bullet, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (bulletPool != null)
                bulletPool.ReturnObject(bullet);
            else if (bullet != null)
                Destroy(bullet);
        }

        public void PlayMuzzleEffect()
        {
            GameObject effect = EnsureMuzzleEffect();
            if (effect == null)
                return;

            ParticleSystem[] particleSystems = effect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particleSystem in particleSystems)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Play(true);
            }
        }

        private GameObject EnsureMuzzleEffect()
        {
            if (muzzleEffect != null)
                return muzzleEffect;
            if (weaponData == null || weaponData.muzzleFlashPrefab == null || bulletSpawnPoint == null)
                return null;

            muzzleEffect = Instantiate(weaponData.muzzleFlashPrefab, bulletSpawnPoint, false);
            muzzleEffect.name = $"{weaponData.weaponName}_MuzzleFlash";
            muzzleEffect.transform.localPosition = Vector3.zero;
            muzzleEffect.transform.localRotation = Quaternion.Euler(
                weaponData.muzzleFlashLocalEulerAngles);
            muzzleEffect.transform.localScale = Vector3.one * weaponData.muzzleFlashScale;
            SetLayerRecursively(muzzleEffect.transform, bulletSpawnPoint.gameObject.layer);
            return muzzleEffect;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
                SetLayerRecursively(child, layer);
        }

        private void TriggerAnimation(string triggerName)
        {
            bool suppressPerShotRestart = triggerName == "Fire"
                && weaponData != null
                && !weaponData.restartFireAnimationPerShot;
            if (weaponAnimator != null
                && HasAnimatorParameter(weaponAnimator, triggerName, AnimatorControllerParameterType.Trigger)
                && (!suppressPerShotRestart || !IsAnimatorInState(weaponAnimator, 0, "Fire")))
                weaponAnimator.SetTrigger(triggerName);
            if (fpsArmsAnimator == null)
                return;

            if (fpsArmsAnimator.runtimeAnimatorController != null &&
                fpsArmsAnimator.runtimeAnimatorController.name == "FPAnim")
            {
                ConfigureFpLayer(false);
                int fpLayer = fpsArmsAnimator.GetLayerIndex(GetFirstPersonLayerName());
                if (HasAnimatorParameter(fpsArmsAnimator, triggerName, AnimatorControllerParameterType.Trigger)
                    && (!suppressPerShotRestart || !IsAnimatorInState(fpsArmsAnimator, fpLayer, "Fire")))
                {
                    fpsArmsAnimator.ResetTrigger(triggerName);
                    fpsArmsAnimator.SetTrigger(triggerName);
                }
                return;
            }

            if (HasAnimatorParameter(fpsArmsAnimator, triggerName, AnimatorControllerParameterType.Trigger))
                fpsArmsAnimator.SetTrigger(triggerName);
        }

        private static bool IsAnimatorInState(Animator animator, int layer, string stateName)
        {
            if (animator == null || layer < 0 || layer >= animator.layerCount)
                return false;

            if (animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName))
                return true;
            return animator.IsInTransition(layer)
                && animator.GetNextAnimatorStateInfo(layer).IsName(stateName);
        }

        private void ConfigureFpLayer(bool playIdle = false)
        {
            if (fpsArmsAnimator == null || fpsArmsAnimator.runtimeAnimatorController == null ||
                fpsArmsAnimator.runtimeAnimatorController.name != "FPAnim")
                return;

            string selected = GetFirstPersonLayerName();
            for (int i = 0; i < fpsArmsAnimator.layerCount; i++)
            {
                string layerName = fpsArmsAnimator.GetLayerName(i);
                fpsArmsAnimator.SetLayerWeight(i, i == 0 || layerName == selected ? 1f : 0f);
            }

            int layer = fpsArmsAnimator.GetLayerIndex(selected);
            if (layer >= 0 && HasAnimatorParameter(fpsArmsAnimator, "ActiveWeaponLayer", AnimatorControllerParameterType.Int))
                fpsArmsAnimator.SetInteger("ActiveWeaponLayer", layer);
            if (playIdle && layer >= 0)
                fpsArmsAnimator.Play("Idle", layer, 0f);
        }

        private string GetFirstPersonLayerName()
        {
            if (weaponData != null && !string.IsNullOrWhiteSpace(weaponData.firstPersonAnimatorLayer))
                return weaponData.firstPersonAnimatorLayer;
            return weaponData != null ? weaponData.name : string.Empty;
        }

        private static bool HasAnimatorParameter(Animator animator, string name, AnimatorControllerParameterType type)
        {
            foreach (var parameter in animator.parameters)
                if (parameter.name == name && parameter.type == type)
                    return true;
            return false;
        }

        private IEnumerator ReturnFpAnimationToIdle(int layer, string idleState)
        {
            // Wait until the one-shot state has entered and report its actual
            // clip length. The fallback keeps reload usable if an imported clip
            // reports zero length while the asset database is refreshing.
            yield return null;
            float timeout = 2f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                var state = fpsArmsAnimator.GetCurrentAnimatorStateInfo(layer);
                if (state.IsName("Fire") || state.IsName("Reload") || state.IsName("Equip"))
                {
                    float wait = Mathf.Max(0.05f, state.length * Mathf.Max(0.01f, 1f - state.normalizedTime));
                    yield return new WaitForSeconds(wait);
                    break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (fpsArmsAnimator != null)
                fpsArmsAnimator.Play(idleState, layer, 0f);
            fpsAnimationCoroutine = null;
        }

        public void PlayShootSound()
        {
            if (weaponData == null) return;
            if (weaponData.restartFireAnimationPerShot)
                TriggerAnimation("Fire");
            else if (!continuousFirePresentationActive)
            {
                int fpLayer = fpsArmsAnimator != null
                    ? fpsArmsAnimator.GetLayerIndex(GetFirstPersonLayerName())
                    : -1;
                PlayContinuousFireAtStart(fpLayer);
                continuousFirePresentationActive = true;
            }
            if (weaponData.shootSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(weaponData.shootSound);
        }

        private void ReloadWeapon()
        {
            if (weaponData == null) return;
            if (isReloading) return;
            if (IsEquipping() || Time.unscaledTime < nextReloadRequestTime) return;

            ForceExitAimPresentation();

            if (cachedFireHandler != null && cachedFireHandler.IsSpawned && cachedFireHandler.NetworkManager != null && cachedFireHandler.NetworkManager.IsListening)
            {
                cachedFireHandler.RequestReloadServerRpc();
                nextReloadRequestTime = Time.unscaledTime + 0.25f;
                return;
            }

            canShoot    = false;
            isReloading = true;
            remainingPerShellPresentationInserts = weaponData.reloadMode == ReloadMode.PerShell
                ? weaponData.GetPerShellRoundsToLoad(currentAmmo, reservedAmmo)
                : 0;
            ReportCombatTelemetry();

            reloadCoroutine = StartCoroutine(ReloadCoroutine());
        }

        private IEnumerator ReloadCoroutine()
        {
            if (weaponData == null)
                yield break;

            if (weaponData.reloadSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFXSound(weaponData.reloadSound);

            isInspecting = false;
            cachedWeaponManager?.TriggerAnimation("Reload");

            if (weaponData.reloadMode == ReloadMode.PerShell)
            {
                TriggerAnimation("Reload");
                yield return new WaitForSeconds(weaponData.PerShellOpeningDuration);
                while (currentAmmo < weaponData.magazineSize && reservedAmmo > 0)
                {
                    yield return new WaitForSeconds(weaponData.PerShellInterval);
                    currentAmmo++;
                    reservedAmmo--;
                    ReportCombatTelemetry();
                }

                yield return new WaitForSeconds(weaponData.PerShellClosingDuration);

                isReloading = false;
                canShoot = true;
                reloadCoroutine = null;
                InsertMagazine();
                ReportCombatTelemetry();
                yield break;
            }

            TriggerAnimation("Reload");
            yield return new WaitForSeconds(weaponData.ReloadAmmoCommitDuration);
            CommitMagazineAmmo();
            yield return new WaitForSeconds(Mathf.Max(
                0f, weaponData.ReloadDuration - weaponData.ReloadAmmoCommitDuration));
            FinishReload();
            reloadCoroutine = null;
        }

        private void CommitMagazineAmmo()
        {
            if (weaponData == null) return;

            int bulletsNeeded   = weaponData.magazineSize - currentAmmo;
            int bulletsToReload = Mathf.Min(bulletsNeeded, reservedAmmo);

            reservedAmmo -= bulletsToReload;
            currentAmmo  += bulletsToReload;

            InsertMagazine();
            ReportCombatTelemetry();
        }

        private void FinishReload()
        {
            isReloading = false;
            canShoot    = true;
            ReportCombatTelemetry();
        }

        public void GrabMagazine()
        {
            if (magazineOnGun != null)  magazineOnGun.SetActive(false);
            if (magazineInHand != null) magazineInHand.SetActive(true);
        }

        public void InsertMagazine()
        {
            if (magazineOnGun != null)  magazineOnGun.SetActive(true);
            if (magazineInHand != null) magazineInHand.SetActive(false);
        }

        public void AddReserveAmmo(int amount)
        {
            reservedAmmo += amount;
            ReportCombatTelemetry();
        }

        public void SetLocalAmmoState(int magazineAmmo, int reserveAmmo, bool reloading)
        {
            int previousMagazineAmmo = currentAmmo;
            bool reloadStarted = !isReloading && reloading;
            bool reloadFinished = isReloading && !reloading;
            pendingServerShots = 0;
            currentAmmo = Mathf.Max(0, magazineAmmo);
            reservedAmmo = Mathf.Max(0, reserveAmmo);
            isReloading = reloading;
            if (reloadStarted)
            {
                ForceExitAimPresentation();
                canShoot = false;
                isInspecting = false;
                remainingPerShellPresentationInserts = weaponData != null
                    ? weaponData.GetPerShellRoundsToLoad(currentAmmo, reservedAmmo)
                    : 0;
                TriggerAnimation("Reload");
            }
            else if (reloadFinished)
            {
                canShoot = true;
                remainingPerShellPresentationInserts = 0;
                InsertMagazine();
            }
            else if (isReloading && reloading && currentAmmo > previousMagazineAmmo)
            {
                InsertMagazine();
            }
            // Network reconciliation owns ammo/reload state only.  The local
            // fire gate is owned by ShootCooldown/FireBurst; resetting it here
            // lets every server response bypass the animator-baked fire interval.
            ReportCombatTelemetry();
        }

        public void ApplyAuthoritativePresentation(double equipCompleteTime, bool reloading)
        {
            bool changed = System.Math.Abs(authoritativeEquipCompleteTime - equipCompleteTime) > 0.0001;
            authoritativeEquipCompleteTime = equipCompleteTime;
            if (!gameObject.activeInHierarchy || !IsEquipping() || !changed)
                return;

            ForceExitAimPresentation();
            double duration = weaponData != null ? System.Math.Max(0.0001, weaponData.EquipDuration) : 0.0001;
            float normalized = Mathf.Clamp01(1f - (float)((equipCompleteTime - GetPresentationTime()) / duration));
            PrepareFirstPersonPresentation(true, normalized);
        }

        public bool TryPlayInspect()
        {
            if (!isOwner || weaponData == null || isReloading || IsEquipping())
                return false;

            ForceExitAimPresentation();
            isInspecting = true;
            TriggerAnimation("Inspect");
            return true;
        }

        private bool IsEquipping()
        {
            return authoritativeEquipCompleteTime >= 0.0 && GetPresentationTime() < authoritativeEquipCompleteTime;
        }

        private double GetPresentationTime()
        {
            return cachedFireHandler != null && cachedFireHandler.NetworkManager != null && cachedFireHandler.NetworkManager.IsListening
                ? cachedFireHandler.NetworkManager.ServerTime.Time
                : Time.timeAsDouble;
        }

        private void PlayEquipAtNormalizedTime(float normalizedTime)
        {
            ConfigureFpLayer(false);
            int layer = fpsArmsAnimator != null ? fpsArmsAnimator.GetLayerIndex(GetFirstPersonLayerName()) : -1;
            if (layer >= 0)
                fpsArmsAnimator.Play("Equip", layer, Mathf.Clamp01(normalizedTime));
            if (weaponAnimator != null && weaponAnimator.runtimeAnimatorController != null)
                weaponAnimator.Play("Equip", 0, Mathf.Clamp01(normalizedTime));
        }

        public void ReportCombatTelemetry()
        {
            if (!isOwner) return;
            if (weaponData == null) return;

            if (combatTelemetry == null)
                combatTelemetry = GetComponentInParent<PlayerCombatTelemetry>();

            combatTelemetry?.ReportWeaponState(isReloading, currentAmmo, weaponData.magazineSize);
        }

        private void CacheOwnerDependencies()
        {
            recoilController = GetComponentInParent<RecoilController>();
            if (recoilController == null)
                recoilController = FindAnyObjectByType<RecoilController>();

            cachedFireHandler = GetComponentInParent<WeaponFireHandler>();
            cachedWeaponManager = GetComponentInParent<WeaponManager>();
            cachedPlayerMovement = GetComponentInParent<PlayerMovement>();
            cachedPlayerHealth = GetComponentInParent<PlayerHealth>();
            cachedMouseMovement = GetComponentInParent<MouseMovement>();
            if (cachedCamera == null)
                cachedCamera = cachedMouseMovement != null ? cachedMouseMovement.BodyCam : null;
            cachedWeaponCamera = cachedMouseMovement != null ? cachedMouseMovement.WeaponCam : null;
            if (!aimPresentationInitialized && cachedCamera != null)
            {
                unscopedWorldFov = cachedCamera.fieldOfView;
                aimPresentationInitialized = true;
            }
            combatTelemetry = GetComponentInParent<PlayerCombatTelemetry>();
        }

        private void UpdateAimPresentation()
        {
            if (weaponData == null || !weaponData.supportsAim)
            {
                if (aimRequested || aimBlend > 0f || scopePresentationVisible)
                    ForceExitAimPresentation();
                return;
            }

            CacheOwnerDependencies();
            if (cachedCamera == null || cachedMouseMovement == null)
                return;

            if (cachedPlayerHealth != null
                && (cachedPlayerHealth.IsDead || !cachedPlayerHealth.IsInputReady))
            {
                ForceExitAimPresentation();
                return;
            }

            InputManager input = InputManager.Instance;
            bool aimButtonPressed = input != null && input.GetAimInput();
            bool aimButtonPressedThisSample = ConsumeAimToggleEdge(aimButtonPressed);

            if (!NetworkMatchStateManager.IsGameplayActive || InputManager.GameplayInputBlocked)
            {
                ForceExitAimPresentation();
                return;
            }

            if (aimButtonPressedThisSample)
            {
                if (aimRequested)
                {
                    aimRequested = false;
                }
                else if (!isReloading && !IsEquipping())
                {
                    aimRequested = true;
                    if (!TryCalculateAimedViewmodelPose())
                    {
                        aimRequested = false;
                        GameLog.Error($"[Weapon] {name} cannot enter physical ADS: assign an authored aimSight and first-person arms Animator.");
                    }
                }
            }

            ApplyAimPresentation(aimRequested, Time.unscaledDeltaTime);
        }

        private bool ConsumeAimToggleEdge(bool isPressed)
        {
            bool pressedThisSample = isPressed && !aimButtonWasPressed;
            aimButtonWasPressed = isPressed;
            return pressedThisSample;
        }

        private void ApplyAimPresentation(bool wantsAim, float unscaledDeltaTime)
        {
            if (weaponData == null || cachedCamera == null)
                return;

            if (wantsAim && !aimRequested)
            {
                aimRequested = true;
                if (!TryCalculateAimedViewmodelPose())
                    aimRequested = false;
            }
            else if (!wantsAim)
            {
                aimRequested = false;
            }

            float duration = Mathf.Max(0.01f, weaponData.aimTransitionDuration);
            aimBlend = Mathf.MoveTowards(
                aimBlend,
                aimRequested ? 1f : 0f,
                Mathf.Max(0f, unscaledDeltaTime) / duration);
            float easedBlend = Mathf.SmoothStep(0f, 1f, aimBlend);

            cachedCamera.fieldOfView = Mathf.Lerp(
                unscopedWorldFov,
                weaponData.aimedWorldFov,
                easedBlend);
            if (cachedMouseMovement != null)
            {
                cachedMouseMovement.SetLookSensitivityMultiplier(Mathf.Lerp(
                    1f,
                    weaponData.aimedSensitivityMultiplier,
                    easedBlend));
            }

            ApplyViewmodelAimPose(easedBlend);
            bool reachedFullScope = aimRequested && aimBlend >= 0.999f;
            bool retainScopeWhileLowering = !aimRequested
                && scopePresentationVisible
                && aimBlend > 0.55f;
            SetScopePresentationVisible(reachedFullScope || retainScopeWhileLowering);

            // Keep the physical raise-to-scope animation visible. Once the
            // Operator HUD takes over, stop only the viewmodel camera so the
            // opaque 3D optic cannot block the target behind the transparent HUD.
            bool scopeHudOwnsView = weaponData.showScopeOverlay && scopePresentationVisible;
            if (cachedWeaponCamera != null)
                cachedWeaponCamera.enabled = isOwner && !scopeHudOwnsView;
            if (HUDManager.HasInstance)
            {
                bool aimHudActive = aimRequested || scopePresentationVisible;
                HUDManager.Instance.SetAimHudVisible(
                    aimHudActive,
                    weaponData.showScopeOverlay && scopePresentationVisible,
                    weaponData.scopeOverlaySprite);
            }
        }

        private void SetScopePresentationVisible(bool visible)
        {
            if (scopePresentationVisible == visible)
                return;

            scopePresentationVisible = visible;
        }

        private void ForceExitAimPresentation()
        {
            bool ownedScopePresentation = isOwner || aimRequested || aimBlend > 0f || scopePresentationVisible;
            aimRequested = false;
            aimBlend = 0f;
            if (cachedCamera != null && aimPresentationInitialized)
                cachedCamera.fieldOfView = unscopedWorldFov;
            if (cachedMouseMovement != null)
                cachedMouseMovement.SetLookSensitivityMultiplier(1f);
            if (cachedWeaponCamera != null)
                cachedWeaponCamera.enabled = isOwner;
            ApplyViewmodelAimPose(0f);
            scopePresentationVisible = false;
            if (ownedScopePresentation && HUDManager.HasInstance)
                HUDManager.Instance.SetAimHudVisible(false, false);
        }

        private void BeginExitAimAfterShot()
        {
            // Do not swap HUD -> 3D viewmodel at the fully-aimed pose. That
            // exposes the opaque optic in the center for one frame and looks
            // like camera recoil. ApplyAimPresentation keeps the HUD until the
            // lowering blend has moved the weapon clear of the target.
            aimRequested = false;
        }

        private void CacheViewmodelHipPose()
        {
            if (viewmodelRoot == null)
                return;

            hipViewmodelLocalPosition = viewmodelRoot.localPosition;
            hipViewmodelLocalRotation = viewmodelRoot.localRotation;
            aimedViewmodelLocalPosition = hipViewmodelLocalPosition;
            aimedViewmodelLocalRotation = hipViewmodelLocalRotation;
            viewmodelHipPoseCached = true;
        }

        private bool TryCalculateAimedViewmodelPose()
        {
            if (cachedCamera == null
                || aimSight == null
                || aimSightEnd == null
                || !aimSightEnd.IsChildOf(aimSight)
                || viewmodelRoot == null)
                return false;
            if (!viewmodelHipPoseCached)
                CacheViewmodelHipPose();

            // Always solve from the authored hip pose. This prevents repeated RMB
            // toggles from accumulating transform error.
            viewmodelRoot.SetLocalPositionAndRotation(
                hipViewmodelLocalPosition,
                hipViewmodelLocalRotation);

            Vector3 targetSightPosition = cachedCamera.transform.TransformPoint(
                aimedSightCameraLocalPosition);
            Vector3 targetRootPosition = viewmodelRoot.position
                + (targetSightPosition - aimSight.position);

            Transform parent = viewmodelRoot.parent;
            aimedViewmodelLocalPosition = parent != null
                ? parent.InverseTransformPoint(targetRootPosition)
                : targetRootPosition;
            // The weapon and hands have already been authored/calibrated together.
            // Runtime ADS translates that intact composition only. Rotating Hand
            // from a socket transform is invalid because imported sight axes use
            // local +Y, not Unity Transform.forward (+Z).
            aimedViewmodelLocalRotation = hipViewmodelLocalRotation;
            return true;
        }

        private void ApplyViewmodelAimPose(float blend)
        {
            if (!viewmodelHipPoseCached || viewmodelRoot == null)
                return;

            viewmodelRoot.SetLocalPositionAndRotation(
                Vector3.LerpUnclamped(
                    hipViewmodelLocalPosition,
                    aimedViewmodelLocalPosition,
                    blend),
                Quaternion.SlerpUnclamped(
                    hipViewmodelLocalRotation,
                    aimedViewmodelLocalRotation,
                    blend));
        }

        private void OnDestroy()
        {
            ForceExitAimPresentation();
        }

        private Camera ResolveAimCamera()
        {
            if (cachedCamera != null && cachedCamera.isActiveAndEnabled)
                return cachedCamera;

            if (cachedMouseMovement == null)
                cachedMouseMovement = GetComponentInParent<MouseMovement>();
            cachedCamera = cachedMouseMovement != null ? cachedMouseMovement.BodyCam : null;
            return cachedCamera != null && cachedCamera.isActiveAndEnabled ? cachedCamera : null;
        }

        private int GetVisualHitMask()
        {
            if (weaponData != null && weaponData.hitMask.value != 0)
                return weaponData.hitMask.value;

            return Physics.DefaultRaycastLayers;
        }

        private bool CanUseInstantiateFallback()
        {
            return allowInstantiateFallbackInDevelopment && (Application.isEditor || Debug.isDebugBuild);
        }

    }
}
