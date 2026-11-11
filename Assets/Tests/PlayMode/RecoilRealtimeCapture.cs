#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public sealed partial class SurvivalNetworkTests
    {
        [UnityTest, Explicit("Real-clock rendered playtest and external video capture; run by name.")]
        public IEnumerator CaptureRealtimeRecoilGameView()
        {
            string phase = File.ReadAllText(RecoilReport + "Tools/capture-phase.txt").Trim();
            bool baseline = phase == "Baseline-r2";
            bool reconstructR3 = phase == "A-r3-reconstructed";
            Assert.That(baseline || phase == "A-r3" || phase == "A-r4" || phase == "A-r4b" || phase == "A-r5" || phase == "A-r5b" || phase == "A-r6" || phase == "A-r7" || reconstructR3, Is.True);
            string scenario = File.Exists(RecoilReport + "Tools/capture-case.txt")
                ? File.ReadAllText(RecoilReport + "Tools/capture-case.txt").Trim() : "hip";
            Assert.That(new[] { "hip", "ads", "partial", "full", "clamp", "held" }.Contains(scenario), Is.True);
            bool held = scenario == "held";
            using var heldInput = held ? new RecoilHeldInput() : null;
            int targetFps = int.Parse(File.ReadAllText(RecoilReport + "Tools/capture-fps.txt"), CultureInfo.InvariantCulture);
            Assert.That(new[] { 30, 60, 144 }.Contains(targetFps), Is.True);
            string[] names = File.ReadAllLines(RecoilReport + "Tools/capture-weapons.txt");
            bool video = File.Exists(RecoilReport + "Tools/capture-video.txt")
                && File.ReadAllText(RecoilReport + "Tools/capture-video.txt").Trim() == "1";
            const string mapPath = "Assets/FPS/Features/World/Content/Map_v1.unity";
            // This capture renders the host; transport tests cover the client separately.
            client.Shutdown();
            yield return Until(() => !client.IsListening && host.ConnectedClients.Count == 1, "capture client shutdown");
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(mapPath, new LoadSceneParameters(LoadSceneMode.Additive));
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)) camera.enabled = false;
            teammate.transform.position = Vector3.one * -1000;
            int previousTarget = Application.targetFrameRate, previousVsync = QualitySettings.vSyncCount;
            float previousCapture = Time.captureDeltaTime;
            Application.targetFrameRate = targetFps;
            QualitySettings.vSyncCount = 0;
            Time.captureDeltaTime = 0;
            var rows = new List<string>(4096);
            using var view = new RecoilCaptureView();
            try
            {
                foreach (string name in names)
                {
                    var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath("Clove")), new Vector3(0, 1.7f, -5), Quaternion.identity);
                    var movement = player.GetComponent<PlayerMovement>();
                    movement.enabled = false;
                    var look = player.GetComponent<MouseMovement>();
                    look.enabled = false;
                    player.GetComponent<NetworkObject>().Spawn();
                    var manager = player.GetComponent<WeaponManager>();
                    if (name != "Classic") Assert.That(manager.TryReplacePrimaryWeaponServer(Enum.Parse<PrimaryWeaponId>(name)), Is.True);
                    int slot = name == "Classic" ? 1 : 0;
                    manager.SetEquippedWeaponServer(slot);
                    var fire = player.GetComponent<WeaponFireHandler>();
                    var weapon = manager.GetWeapon(slot);
                    yield return Until(() => !fire.IsServerEquipping && !weapon.IsEquipPresentationActive, "real-clock equip");
                    WeaponData data = null;
                    RecoilPattern profile = null;
                    if (reconstructR3)
                    {
                        data = Object.Instantiate(weapon.Data);
                        profile = Object.Instantiate(data.recoilPattern);
                        profile.cameraPopScale = Vector2.one;
                        profile.cameraRecoilFollow = profile.adsCameraRecoilFollow = Vector2.one;
                        if (name == "Warden")
                        {
                            profile.cameraPopPitchCurve = profile.cameraPopYawCurve = null;
                            profile.cameraPop = .15f;
                        }
                        data.recoilPattern = profile;
                        SetField(weapon, "weaponData", data);
                    }
                    else if (baseline && name != "Warden")
                    {
                        data = Object.Instantiate(weapon.Data);
                        if (name == "Bucky") data.recoilPattern = null;
                        else
                        {
                            profile = Object.Instantiate(data.recoilPattern);
                            profile.useStabilityCurves = false;
                            data.recoilPattern = profile;
                        }
                        SetField(weapon, "weaponData", data);
                    }
                    SetField(movement, "isGrounded", true);
                    if (scenario == "ads" && weapon.Data.supportsAim)
                        Invoke(weapon, "ApplyAimPresentation", true, 1f);
                    if (scenario == "clamp") look.ApplyRecoil(89.95f, 0);
                    yield return new WaitForSeconds(1);
                    string directory = RecoilReport + $"Visual/{phase}/{targetFps}/{name}/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + scenario;
                    Directory.CreateDirectory(directory);
                    File.WriteAllLines(directory + "/cameras.txt", Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)
                        .Select(c => $"{AnimationUtility.CalculateTransformPath(c.transform, null)}; enabled={c.isActiveAndEnabled}; mask={c.cullingMask}; owner={c.GetComponentInParent<NetworkObject>()?.OwnerClientId}"));
                    File.WriteAllText(directory + "/setup.txt", $"{mapPath}\npose=(0,1.7,-5); host owner0; slot={slot}; seed sequence1; scenario={scenario}\nFOV={look.BodyCam.fieldOfView}; sensitivity={look.Sensitivity}; targetFPS={targetFps}\nReal time; captureDeltaTime=0; vSync=0; screenshots excluded from frame-rate sample. Baseline is reconstructed with in-memory legacy flag, never asset writes. ADS only when supported; Operator exits scope after shooting. Compensation is scripted canonical look input.\n" + (held ? "Held: synthetic Mouse leftButton only through InputManager -> Weapon.Update -> HandleFire. Zero mouse delta, no ADS; stationary confirmed aim samples are supplied by fixture. Hold until magazine empty or 14 seconds; no ammo refill.\n" : "Scheduled FireBullet calls, not held input.\n"));
                    rows.Clear();
                    rows.Add("seconds,serverClock,renderFrame,frameMs,shotSequence,seed,pitch,yaw,index,errorIndex,modelPitch,modelYaw,cameraPitch,cameraYaw,shotSpread,lastResult,ammo,pending,popPitch,popYaw,separationPitch,separationYaw,rayX,rayY,cameraWorldPitch,cameraWorldYaw,shotClock,triggerHeld,yawFrom,yawTo,yawSwitchStart,burstShots");
                    int shots = held ? weapon.CurrentAmmo : name == "Odin" ? 30 : name == "Operator" || name == "Bucky" ? 2 : 12;
                    int initialAmmo = weapon.CurrentAmmo;
                    if (!held) shots = Mathf.Min(shots, initialAmmo - 1);
                    int minimumAmmo = initialAmmo;
                    bool heldStopped = false;
                    File.WriteAllLines(directory + "/nearby-renderers.txt", Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                        .Where(r => r.enabled && Vector3.Distance(r.bounds.center, look.BodyCam.transform.position) < 15)
                        .Select(r => $"{r.gameObject.scene.name}/{AnimationUtility.CalculateTransformPath(r.transform, null)}; hidden={r.forceRenderingOff}; layer={LayerMask.LayerToName(r.gameObject.layer)}; center={r.bounds.center}"));
                    int shot = 0;
                    double shotClock = 0;
                    Vector2 previousDebt = Vector2.zero;
                    float startingPitch = look.XRotation;
                    double start = Time.realtimeSinceStartupAsDouble, nextShot = weapon.StabilityTime + .5;
                    double finish = start + .5 + shots * (weapon.Data.FireInterval + .002) + 2;
                    if (held) finish = start + .5 + 14 + 2;
                    uint sampleSequence = 0;
                    var frameTimes = new List<double>(2048);
                    using var encoder = video ? new RecoilVideoWriter(directory + "/gameplay.mp4") : null;
                    while (Time.realtimeSinceStartupAsDouble < finish)
                    {
                        uint seed = 0;
                        double now = Time.realtimeSinceStartupAsDouble;
                        if (held && (weapon.CurrentAmmo <= 0 || now >= start + .5 + 14)) heldStopped = true;
                        bool trigger = held && now >= start + .5 && !heldStopped;
                        if (held)
                        {
                            heldInput.Set(trigger);
                            if (now >= start + .5 && !trigger) finish = Math.Min(finish, now + 2);
                        }
                        if (held || (weapon.StabilityTime >= nextShot && shot < shots))
                        {
                            int tick = host.LocalTime.Tick;
                            uint sequence = held ? ++sampleSequence : (uint)++shot;
                            bool aimed = weapon.IsAimRequested;
                            SetField(movement, "aimHeld", aimed);
                            var input = new PlayerInputPayload { sequence = sequence, tick = tick, yaw = look.YRotation, pitch = look.XRotation, aim = aimed };
                            Invoke(movement, "RecordServerAim", input, tick);
                            SetField(movement, "localCommandSequence", sequence);
                            SetField(movement, "hasTickedOnce", true);
                            Field<NetworkTimer>(movement, "networkTimer").CurrentTick = tick + 1;
                            Field<PlayerInputPayload[]>(movement, "inputBuffer")[tick % 2048] = input;
                            Field<PlayerStatePayload[]>(movement, "stateBuffer")[tick % 2048] = new PlayerStatePayload { grounded = true, aiming = aimed };
                            if (!held)
                            {
                                seed = WeaponBallistics.BuildShotSeed(host.LocalClientId, (ushort)shot, (byte)slot);
                                shotClock = weapon.StabilityTime;
                                Invoke(weapon, "FireBullet");
                                nextShot = weapon.StabilityTime + weapon.Data.FireInterval + .002;
                                finish = now + (shots - shot) * (weapon.Data.FireInterval + .002) + 2;
                            }
                        }
                        var recoil = player.GetComponent<RecoilController>();
                        var debt = Field<Vector2>(recoil, "recoverableRecoil");
                        if (scenario == "partial" || scenario == "full")
                        {
                            Vector2 kick = new Vector2(NewKick(previousDebt.x, debt.x), NewKick(previousDebt.y, debt.y));
                            Vector2 compensation = -kick * (scenario == "full" ? 1f : .5f);
                            Vector2 applied = look.ApplyRecoil(compensation.x, compensation.y);
                            recoil.ConsumeLookCompensation(applied);
                        }
                        previousDebt = Field<Vector2>(recoil, "recoverableRecoil");
                        Invoke(look, "RenderRotation");
                        yield return new WaitForEndOfFrame();
                        var state = weapon.PredictedStability;
                        minimumAmmo = Mathf.Min(minimumAmmo, weapon.CurrentAmmo);
                        if (held)
                        {
                            shot = Field<ushort>(weapon, "fireSequence");
                            shotClock = state.lastShotTime;
                            seed = WeaponBallistics.BuildShotSeed(host.LocalClientId, (ushort)shot, (byte)slot);
                            Assert.That(weapon.IsAimRequested, Is.False, "held capture must stay hip-fire");
                        }
                        var pattern = weapon.Data.recoilPattern;
                        var model = pattern != null && pattern.useStabilityCurves
                            ? state.TargetAt(pattern, weapon.StabilityTime, weapon.Data.FireInterval)
                            : Field<Vector2>(player.GetComponent<RecoilController>(), "currentRecoil");
                        float spread = Field<Dictionary<int, WeaponServerState>>(fire, "serverStates")[slot].LastShotSpread;
                        var acknowledged = Field<NetworkVariable<WeaponOwnerState>>(fire, "ownerWeaponState").Value;
                        Vector3 camera = look.BodyCam.transform.localEulerAngles;
                        Vector3 worldCamera = look.BodyCam.transform.eulerAngles;
                        Vector2 separation = Field<Vector2>(recoil, "cameraSeparation");
                        Vector2 pop = Field<Vector2>(look, "cosmeticRecoil") + separation;
                        Vector3 ray = look.BodyCam.WorldToViewportPoint(look.BodyCam.transform.position + look.AimDirection * 100);
                        double time = Time.realtimeSinceStartupAsDouble;
                        frameTimes.Add(time);
                        rows.Add(FormattableString.Invariant($"{time-start},{weapon.StabilityTime},{Time.frameCount},{Time.unscaledDeltaTime*1000},{shot},{seed},{look.XRotation},{look.YRotation},{state.index},{state.firingErrorIndex},{model.x},{model.y},{Mathf.DeltaAngle(0,camera.x)},{Mathf.DeltaAngle(0,camera.y)},{spread},{acknowledged.lastFireResult},{weapon.CurrentAmmo},{Field<int>(weapon, "pendingServerShots")},{pop.x},{pop.y},{separation.x},{separation.y},{ray.x},{ray.y},{Mathf.DeltaAngle(0,worldCamera.x)},{Mathf.DeltaAngle(0,worldCamera.y)},{shotClock},{(trigger ? 1 : 0)},{state.yawFromSign},{state.yawToSign},{state.yawSwitchStartTime},{state.acceptedShots}"));
                        encoder?.Frame();
                        yield return null;
                    }
                    File.WriteAllLines(directory + "/telemetry.csv", rows);
                    double actualFps = (frameTimes.Count - 1) / (frameTimes[frameTimes.Count - 1] - frameTimes[0]);
                    int accepted = initialAmmo - (held ? minimumAmmo : weapon.CurrentAmmo);
                    File.WriteAllText(directory + "/result.txt", FormattableString.Invariant($"targetFPS={targetFps}; sampledFPS={actualFps:F3}; samples={frameTimes.Count}; attempted={shot}; accepted={accepted}; pending={Field<int>(weapon, "pendingServerShots")}; pitch={look.XRotation}; yaw={look.YRotation}; GPU={SystemInfo.graphicsDeviceName}; CPU={SystemInfo.processorType}\n"));
                    Assert.That(shot, Is.EqualTo(shots));
                    Assert.That(accepted, Is.EqualTo(shots), "real host must accept every shot in this capture");
                    Assert.That(Field<int>(weapon, "pendingServerShots"), Is.Zero);
                    if (!baseline)
                    {
                        Assert.That(Mathf.Abs(look.XRotation - startingPitch), Is.LessThan(.01), "real-clock recovery pitch");
                        Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, look.YRotation)), Is.LessThan(.01), "real-clock recovery yaw");
                    }
                    yield return new WaitForEndOfFrame();
                    var texture = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(directory + "/settled.png", texture.EncodeToPNG());
                    Object.Destroy(texture);
                    if (!video && scenario == "hip")
                    {
                        using var detail = new RecoilCaptureView(3840, 2160);
                        yield return null;
                        yield return new WaitForEndOfFrame();
                        var detailTexture = ScreenCapture.CaptureScreenshotAsTexture();
                        Assert.That(detailTexture.width, Is.EqualTo(3840));
                        Assert.That(detailTexture.height, Is.EqualTo(2160));
                        File.WriteAllBytes(directory + "/settled-4k.png", detailTexture.EncodeToPNG());
                        Object.Destroy(detailTexture);
                    }
                    player.GetComponent<NetworkObject>().Despawn();
                    if (profile != null) Object.Destroy(profile);
                    if (data != null) Object.Destroy(data);
                    yield return null;
                }
            }
            finally
            {
                Application.targetFrameRate = previousTarget;
                QualitySettings.vSyncCount = previousVsync;
                Time.captureDeltaTime = previousCapture;
            }
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(mapPath));

            static float NewKick(float before, float after) => Mathf.Sign(before) != Mathf.Sign(after)
                ? after : Mathf.Abs(after) > Mathf.Abs(before) ? after - before : 0f;
        }

        sealed class RecoilHeldInput : IDisposable
        {
            readonly InputManager input;
            readonly GameObject root;
            readonly Mouse mouse;
            readonly string bindings;
            readonly UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices;
            readonly InputSettings.BackgroundBehavior background;
            readonly InputSettings.EditorInputBehaviorInPlayMode editorInput;
            readonly bool blocked;

            public RecoilHeldInput()
            {
                root = new GameObject("Recoil held-input capture");
                input = InputManager.Instance != null ? InputManager.Instance : root.AddComponent<InputManager>();
                bindings = input.ActionsAsset.SaveBindingOverridesAsJson();
                devices = input.ActionsAsset.devices;
                background = InputSystem.settings.backgroundBehavior;
                editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                blocked = InputManager.GameplayInputBlocked;
                mouse = InputSystem.AddDevice<Mouse>();
                var fire = input.ActionsAsset.FindAction("Fire", true);
                for (int i = 0; i < fire.bindings.Count; i++) fire.ApplyBindingOverride(i, "");
                fire.ApplyBindingOverride(0, "<Mouse>/leftButton");
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                input.ActionsAsset.devices = new InputDevice[] { mouse };
                InputSystem.EnableDevice(mouse);
                fire.Enable();
                InputManager.GameplayInputBlocked = false;
                Assert.That(fire.controls.Count, Is.EqualTo(1));
                Assert.That(fire.controls[0], Is.SameAs(mouse.leftButton));
            }

            public void Set(bool pressed)
            {
                InputSystem.QueueStateEvent(mouse, pressed ? new MouseState().WithButton(MouseButton.Left) : new MouseState());
                InputSystem.Update();
                Assert.That(input.GetFireInput(), Is.EqualTo(pressed));
                Assert.That(input.GetAimInput(), Is.False);
                Assert.That(mouse.delta.ReadValue(), Is.EqualTo(Vector2.zero));
            }

            public void Dispose()
            {
                input.ActionsAsset.LoadBindingOverridesFromJson(bindings);
                input.ActionsAsset.devices = devices;
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
                InputManager.GameplayInputBlocked = blocked;
                InputSystem.RemoveDevice(mouse);
                Object.Destroy(root);
            }
        }

        sealed class RecoilCaptureView : IDisposable
        {
            readonly EditorWindow view;
            readonly PropertyInfo size;
            readonly object group;
            readonly int previous, added;

            public RecoilCaptureView(int width = 1920, int height = 1080)
            {
                var assembly = typeof(Editor).Assembly;
                view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
                size = view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                previous = (int)size.GetValue(view);
                var sizes = assembly.GetType("UnityEditor.GameViewSizes");
                object instance = sizes.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
                group = sizes.GetMethod("GetGroup").Invoke(instance, new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
                added = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null)
                    + (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
                object resolution = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),
                    Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), width, height, $"Recoil realtime {width}x{height}");
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { resolution });
                size.SetValue(view, added);
                view.Show();
                view.Focus();
            }

            public void Dispose()
            {
                size.SetValue(view, previous);
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { added });
            }
        }

        sealed class RecoilVideoWriter : IDisposable
        {
            readonly System.Diagnostics.Process process;
            readonly RenderTexture target;
            readonly byte[] buffer = new byte[1920 * 1080 * 4];
            readonly Action<AsyncGPUReadbackRequest> completed;
            readonly System.Text.StringBuilder errors = new System.Text.StringBuilder();
            readonly string output;
            int pending, frames, dropped;
            bool readbackFailed;

            public RecoilVideoWriter(string output)
            {
                this.output = output;
                string executable = File.ReadAllText(RecoilReport + "Tools/ffmpeg-path.txt").Trim();
                Assert.That(File.Exists(executable), Is.True, "explicit capture requires the existing external media encoder");
                process = new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo {
                    FileName = executable, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardError = true,
                    Arguments = $"-hide_banner -loglevel warning -n -use_wallclock_as_timestamps 1 -f rawvideo -pixel_format rgba -video_size 1920x1080 -framerate 30 -i pipe:0 -c:v libx264 -preset ultrafast -crf 20 -fps_mode vfr -pix_fmt yuv420p \"{Path.GetFullPath(output)}\"" } };
                process.ErrorDataReceived += (_, message) => { if (message.Data != null) errors.AppendLine(message.Data); };
                Assert.That(process.Start(), Is.True);
                process.BeginErrorReadLine();
                target = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
                target.Create();
                completed = WriteFrame;
            }

            public void Frame()
            {
                Assert.That(Screen.width == 1920 && Screen.height == 1080, Is.True);
                if (pending >= 3) { dropped++; return; }
                ScreenCapture.CaptureScreenshotIntoRenderTexture(target);
                pending++;
                AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32, completed);
            }

            void WriteFrame(AsyncGPUReadbackRequest request)
            {
                try
                {
                    if (request.hasError) { readbackFailed = true; return; }
                    request.GetData<byte>().CopyTo(buffer);
                    process.StandardInput.BaseStream.Write(buffer, 0, buffer.Length);
                    frames++;
                }
                catch (Exception error) { readbackFailed = true; errors.AppendLine(error.ToString()); }
                finally { pending--; }
            }

            public void Dispose()
            {
                AsyncGPUReadback.WaitAllRequests();
                process.StandardInput.Close();
                bool finished = process.WaitForExit(10000);
                if (!finished) process.Kill();
                File.WriteAllText(output + ".encoder.txt", $"frames={frames}; capacityDrops={dropped}; readbackFailed={readbackFailed}\n" + errors);
                Object.Destroy(target);
                Assert.That(finished && process.ExitCode == 0 && !readbackFailed && frames > 0, Is.True, errors.ToString());
                process.Dispose();
            }
        }
    }
}
#endif
