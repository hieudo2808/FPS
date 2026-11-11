using UnityEngine;
using Unity.Netcode;

namespace FPS
{
    [DefaultExecutionOrder(-300)]
    public class RecoilController : NetworkBehaviour
    {
        private RecoilPattern currentPattern;
        private Weapon boundWeapon;
        private int currentShotIndex;
        private uint spraySequence;
        private float timeSinceLastShot;
        private Vector2 currentRecoil, targetRecoil, recoverableRecoil;
        private Vector2 cameraSeparation, cameraFollow = Vector2.one;
        private MouseMovement mouseMovement;
        private double popStart;
        private float popAmount;
        private RecoilPopBuffer popBuffer;

        private void Awake() => mouseMovement = GetComponentInParent<MouseMovement>();

        public override void OnNetworkDespawn()
        {
            Clear();
        }

        private void Update()
        {
            if (!IsOwner || currentPattern == null || mouseMovement == null) return;
            timeSinceLastShot += Time.deltaTime;
            if (boundWeapon != null)
            {
                var state = boundWeapon.PredictedStability;
                targetRecoil = state.TargetAt(currentPattern, boundWeapon.StabilityTime, boundWeapon.Data.FireInterval);
            }
            else
            {
                if (timeSinceLastShot > currentPattern.resetCooldown)
                {
                    if (currentShotIndex != 0) { currentShotIndex = 0; spraySequence++; }
                    targetRecoil = Vector2.Lerp(targetRecoil, Vector2.zero,
                        1f - Mathf.Exp(-currentPattern.returnSpeed * Time.deltaTime));
                }
            }
            float snap = 1f - Mathf.Exp(-currentPattern.snappiness * Time.deltaTime);
            Vector2 smooth = Vector2.Lerp(currentRecoil, targetRecoil, snap);
            Vector2 delta = new Vector2(
                ResolveRecoilDelta(currentRecoil.x, smooth.x, recoverableRecoil.x),
                ResolveRecoilDelta(currentRecoil.y, smooth.y, recoverableRecoil.y));
            Vector2 applied = mouseMovement.ApplyRecoil(delta.x, delta.y);
            cameraSeparation = new Vector2(
                AdvanceCameraSeparation(currentRecoil.x, smooth.x, recoverableRecoil.x, applied.x, cameraSeparation.x, cameraFollow.x),
                AdvanceCameraSeparation(currentRecoil.y, smooth.y, recoverableRecoil.y, applied.y, cameraSeparation.y, cameraFollow.y));
            currentRecoil = smooth;
            recoverableRecoil += applied;

            // Cosmetic pulse never enters MouseMovement's canonical pitch or network input.
            Vector2 pop;
            if (popBuffer != null) pop = popBuffer.Sample(Time.timeAsDouble);
            else
            {
                float age = (float)(Time.timeAsDouble - popStart);
                float pulse = age < 0.01f ? Mathf.Clamp01(age / 0.01f) : Mathf.Clamp01(1f - (age - 0.01f) / 0.12f);
                pop = new Vector2(popAmount * pulse, 0f);
            }
            mouseMovement.SetCosmeticRecoil(pop - cameraSeparation);
        }

        public void Bind(Weapon weapon, bool aimed) => Bind(weapon, aimed, 0);

        public void Bind(Weapon weapon, bool aimed, uint shotSeed)
        {
            if (!IsOwner || weapon == null) return;
            if (boundWeapon != weapon) Clear();
            boundWeapon = weapon;
            currentPattern = weapon.Data.recoilPattern;
            cameraFollow = aimed ? currentPattern.adsCameraRecoilFollow : currentPattern.cameraRecoilFollow;
            // Port adaptation: the extracted crosshair-alpha keys weight new hip pitch kicks by index.
            if (!aimed && currentPattern.hipPitchCameraFollow != null && currentPattern.hipPitchCameraFollow.Length > 0)
                cameraFollow.x *= currentPattern.Evaluate(currentPattern.hipPitchCameraFollow, weapon.PredictedStability.index);
            if (currentPattern.HasCameraPopCurves)
            {
                if (popBuffer == null) popBuffer = new RecoilPopBuffer(currentPattern, weapon.Data.FireInterval);
                popBuffer.Add(Time.timeAsDouble, shotSeed);
            }
            else
            {
                popStart = Time.timeAsDouble;
                popAmount = currentPattern.cameraPop * (aimed ? 0.5f : 1f);
            }
        }

        public void Release(Weapon weapon)
        {
            if (boundWeapon == weapon) Clear();
        }

        private void Clear()
        {
            if (mouseMovement != null)
            {
                mouseMovement.ApplyRecoil(-recoverableRecoil.x, -recoverableRecoil.y);
                mouseMovement.SetCosmeticRecoil(0f);
            }
            boundWeapon = null;
            currentPattern = null;
            currentRecoil = targetRecoil = recoverableRecoil = Vector2.zero;
            cameraSeparation = Vector2.zero;
            cameraFollow = Vector2.one;
            currentShotIndex = 0;
            popAmount = 0f;
            popBuffer?.Clear();
            popBuffer = null;
        }

        public void Fire(RecoilPattern pattern)
        {
            if (!IsOwner || pattern == null) return;
            if (currentPattern != pattern) { Clear(); spraySequence++; }
            currentPattern = pattern;
            cameraFollow = pattern.cameraRecoilFollow;
            targetRecoil += pattern.GetShot(currentShotIndex++, spraySequence);
            timeSinceLastShot = 0f;
        }

        public void ConsumeLookCompensation(Vector2 look)
        {
            recoverableRecoil.x += LimitReturn(look.x, recoverableRecoil.x);
            recoverableRecoil.y += LimitReturn(look.y, recoverableRecoil.y);
        }

        public static float LimitReturn(float delta, float remaining)
        {
            if (remaining == 0f || Mathf.Sign(delta) == Mathf.Sign(remaining)) return 0f;
            return Mathf.Sign(delta) * Mathf.Min(Mathf.Abs(delta), Mathf.Abs(remaining));
        }

        public static float ResolveRecoilDelta(float previousModel, float nextModel, float recoverable)
        {
            float returning = ReturningDelta(previousModel, nextModel);
            // Crossing zero returns the old kick, then introduces a new kick on the other side.
            return LimitReturn(returning, recoverable) + (nextModel - previousModel - returning);
        }

        public static float AdvanceCameraSeparation(float before, float after, float recoverable,
            float applied, float separation, float follow)
        {
            float returning = ReturningDelta(before, after);
            float appliedReturn = LimitReturn(applied, -LimitReturn(returning, recoverable));
            float remaining = before == 0f ? 1f : 1f - Mathf.Abs(returning / before);
            // Mouse compensation consumes gameplay debt, but visual separation decays with the model.
            return separation * remaining + (applied - appliedReturn) * (1f - follow);
        }

        private static float ReturningDelta(float before, float after)
            => before == 0f ? 0f : Mathf.Sign(before) != Mathf.Sign(after) ? -before
                : Mathf.Abs(after) < Mathf.Abs(before) ? after - before : 0f;
    }
}
