#if UNITY_EDITOR
using System;
using System.Collections;
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
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    public sealed partial class SurvivalNetworkTests
    {
        const string RecoilReport = "Reports/Recoil-Implementation-2026-11-03/";

        [UnityTest, Explicit("Game-view evidence capture; run by name outside regression suites.")]
        public IEnumerator CaptureRecoilGameView()
        {
            string phase = File.ReadAllText(RecoilReport + "Tools/capture-phase.txt").Trim();
            Assert.That(phase == "Baseline" || phase == "A-r1" || phase == "P3" || phase == "A-r2", Is.True);
            EditorApplication.ExecuteMenuItem("Tools/Recoil/Save Authored Assets");
            const string mapPath = "Assets/FPS/Features/World/Content/Map_v1.unity";
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(mapPath, new LoadSceneParameters(LoadSceneMode.Additive));
            var map = SceneManager.GetSceneByPath(mapPath);
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include)) camera.enabled = false;
            actor.transform.position = teammate.transform.position = Vector3.one * -1000;
            foreach (var obj in client.SpawnManager.SpawnedObjectsList)
                foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = true;
            var view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
            var sizeProperty = view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int previousSize = (int)sizeProperty.GetValue(view);
            object group = null;
            int customSize = -1;
            float previousCapture = Time.captureDeltaTime;
            try
            {
                var assembly = typeof(Editor).Assembly;
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                object sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
                var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
                group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
                int builtIn = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
                customSize = builtIn + (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
                var sizeType = assembly.GetType("UnityEditor.GameViewSize");
                object size = Activator.CreateInstance(sizeType, Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), 1920, 1080, "Recoil evidence 1080p");
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                sizeProperty.SetValue(view, customSize);
                view.Show();
                view.Focus();
                yield return null;
                yield return new WaitForEndOfFrame();
                Assert.That(Screen.width, Is.EqualTo(1920));
                Assert.That(Screen.height, Is.EqualTo(1080));
                string selection = RecoilReport + "Tools/capture-weapons.txt";
                string[] weapons = File.Exists(selection) ? File.ReadAllLines(selection)
                    : new[] { "Vandal", "Classic", "Odin", "Operator", "Bucky", "Warden" };
                foreach (string name in weapons)
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
                    yield return Until(() => !fire.IsServerEquipping, "capture equip");
                    var weapon = manager.GetWeapon(slot);
                    yield return Until(() => !weapon.IsEquipPresentationActive, "capture owner equip");
                    weapon.enabled = false;
                    var recoil = player.GetComponent<RecoilController>();
                    SetField(movement, "isGrounded", true);
                    foreach (var obj in client.SpawnManager.SpawnedObjectsList)
                        foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = true;
                    string directory = RecoilReport + "Visual/" + phase + "/" + name;
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(directory + "/setup.txt", $"{mapPath}\npose=(0,1.7,-5); rotation=0; host owner=0; slot={slot}; sequence starts at1\nFOV={look.BodyCam.fieldOfView}; sensitivity={look.Sensitivity}\nScripted 30Hz capture cadence; not a measurement of hardware FPS. No mouse compensation.\n");
                    using (var csv = new StreamWriter(directory + "/telemetry.csv"))
                    {
                        csv.WriteLine("frame,seconds,shotSequence,seed,pitch,yaw,index,errorIndex,modelPitch,modelYaw,cameraPitch,cameraYaw,ammo,pending");
                        int shots = name == "Odin" ? 30 : name == "Operator" || name == "Bucky" ? 3 : 12;
                        shots = Mathf.Min(shots, weapon.CurrentAmmo);
                        int gap = Mathf.CeilToInt(weapon.Data.FireInterval * 30);
                        int endFrame = 15 + shots * gap + 60;
                        int shot = 0;
                        Time.captureDeltaTime = 1f / 30;
                        for (int frame = 0; frame < endFrame; frame++)
                        {
                            uint seed = 0;
                            if (frame >= 15 && (frame - 15) % gap == 0 && shot < shots)
                            {
                                int tick = host.LocalTime.Tick;
                                uint sequence = (uint)++shot;
                                var input = new PlayerInputPayload { sequence = sequence, tick = tick, yaw = look.YRotation, pitch = look.XRotation };
                                Invoke(movement, "RecordServerAim", input, tick);
                                SetField(movement, "localCommandSequence", sequence);
                                SetField(movement, "hasTickedOnce", true);
                                Field<NetworkTimer>(movement, "networkTimer").CurrentTick = tick + 1;
                                Field<PlayerInputPayload[]>(movement, "inputBuffer")[tick % 2048] = input;
                                Field<PlayerStatePayload[]>(movement, "stateBuffer")[tick % 2048] = new PlayerStatePayload { grounded = true };
                                seed = WeaponBallistics.BuildShotSeed(host.LocalClientId, (ushort)shot, (byte)slot);
                                Invoke(weapon, "FireBullet");
                            }
                            yield return new WaitForEndOfFrame();
                            foreach (var obj in client.SpawnManager.SpawnedObjectsList)
                                foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = true;
                            if (frame == 0)
                            {
                                File.WriteAllLines(directory + "/cameras.txt", Object.FindObjectsByType<Camera>(FindObjectsInactive.Include).Select(c =>
                                    $"{c.name}; enabled={c.isActiveAndEnabled}; mask={c.cullingMask}; fov={c.fieldOfView}; pos={c.transform.position}; rot={c.transform.rotation.eulerAngles}"));
                                File.WriteAllLines(directory + "/renderers.txt", player.GetComponentsInChildren<Renderer>(true).Select(r =>
                                    $"{AnimationUtility.CalculateTransformPath(r.transform, player.transform)}; active={r.gameObject.activeInHierarchy}; enabled={r.enabled}; layer={LayerMask.LayerToName(r.gameObject.layer)}; bounds={r.bounds}"));
                            }
                            var state = weapon.PredictedStability;
                            var model = weapon.Data.recoilPattern != null && weapon.Data.recoilPattern.useStabilityCurves
                                ? state.TargetAt(weapon.Data.recoilPattern, weapon.StabilityTime, weapon.Data.FireInterval)
                                : Field<Vector2>(recoil, "currentRecoil");
                            Vector3 camera = look.BodyCam.transform.localEulerAngles;
                            csv.WriteLine(FormattableString.Invariant($"{frame},{frame / 30d},{shot},{seed},{look.XRotation},{look.YRotation},{state.index},{state.firingErrorIndex},{model.x},{model.y},{Mathf.DeltaAngle(0,camera.x)},{Mathf.DeltaAngle(0,camera.y)},{weapon.CurrentAmmo},{Field<int>(weapon, "pendingServerShots")}"));
                            var texture = ScreenCapture.CaptureScreenshotAsTexture();
                            File.WriteAllBytes(directory + "/" + frame.ToString("D4", CultureInfo.InvariantCulture) + ".png", texture.EncodeToPNG());
                            Object.Destroy(texture);
                            yield return null;
                        }
                        Assert.That(shot, Is.EqualTo(shots));
                        Assert.That(weapon.CurrentAmmo, Is.LessThan(weapon.Data.magazineSize), "capture must contain actual accepted fire");
                    }
                    Time.captureDeltaTime = previousCapture;
                    player.GetComponent<NetworkObject>().Despawn();
                    yield return null;
                }
            }
            finally
            {
                Time.captureDeltaTime = previousCapture;
                sizeProperty.SetValue(view, previousSize);
                if (group != null && customSize >= 0) group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { customSize });
            }
            yield return SceneManager.UnloadSceneAsync(map);
            EditorApplication.ExecuteMenuItem("Tools/Recoil/Save Authored Assets");
        }
    }
}
#endif
