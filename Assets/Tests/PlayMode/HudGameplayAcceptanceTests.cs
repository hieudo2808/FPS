#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FPS;
using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FPS.Tests
{
    /// <summary>Real GameScene presentation with explicit transient state fixtures; never writes checkpoints or assets.</summary>
    public sealed class HudGameplayAcceptanceTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Evidence = "Documents/UIUX/HUD-Evidence/FPSSample-2026-10-07/";
        CampaignMissionController campaign;
        CampaignHUD hud;
        PlayerHealth owner;
        Mouse mouse;
        Keyboard keyboard;
        InputActionAsset reviewActions;
        InputDevice[] originalDevices;
        InputSettings originalInputSettings;
        InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
        InputSettings.BackgroundBehavior originalBackgroundBehavior;
        Sprite scopeSprite;
        readonly List<string> checks = new();
        readonly List<PlayerHealth> squadActors = new();
        NavMeshDataInstance testNavigation;

        [UnitySetUp]
        public IEnumerator StartGameplay()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            EditorApplication.ExecuteMenuItem("Tools/FPS/UI/HUD Review/Resolution/1920x1080");
            Assert.That(EditorApplication.ExecuteMenuItem("Tools/FPS/UI/HUD Review/Start Local Host"), Is.True);
            Assert.That(NetworkManager.Singleton.IsHost, Is.True, "Local review host must start before waiting for insertion.");
            float deadline = Time.realtimeSinceStartup + 120;
            while (CampaignMissionController.Instance?.State.phase != CampaignPhase.Exploring && Time.realtimeSinceStartup < deadline)
                yield return null;
            campaign = CampaignMissionController.Instance;
            Assert.That(campaign?.State.phase, Is.EqualTo(CampaignPhase.Exploring));
            hud = CampaignHUD.Instance;
            owner = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerHealth>();
            Assert.That(owner.CanUseCombat, Is.True);
            campaign.enabled = false; // Freeze authority transitions while checking each presentation state.
            Set(owner, "campaignProtectionUntil", campaign.Now + 600);
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            Directory.CreateDirectory(Evidence);
            checks.Clear();
            squadActors.Clear();
            reviewActions = InputManager.Instance.ActionsAsset;
            originalDevices = reviewActions.devices?.ToArray();
            checks.Add("FIXTURE original Aim binding: " + InputManager.Instance.GetBindingDisplayName("Aim"));
            // Queued review input must reach Play Mode even while another editor window has focus.
            originalInputSettings = InputSystem.settings;
            originalEditorInputBehavior = originalInputSettings.editorInputBehaviorInPlayMode;
            originalBackgroundBehavior = originalInputSettings.backgroundBehavior;
            originalInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            originalInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.EnableDevice(mouse);
            InputSystem.EnableDevice(keyboard);
            reviewActions.devices = new InputDevice[] { mouse, keyboard };
            scopeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FPS/Features/Weapons/Content/Operator/Scope/HUDScope.png");
            Assert.That(scopeSprite, Is.Not.Null, "Use the authored scope artwork for feedback acceptance.");
        }

        [UnityTearDown]
        public IEnumerator StopGameplay()
        {
            File.WriteAllLines(Evidence + TestContext.CurrentContext.Test.Name + ".txt", checks);
            if (testNavigation.valid) testNavigation.Remove();
            if (reviewActions != null) reviewActions.devices = originalDevices;
            if (originalInputSettings != null)
            {
                originalInputSettings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
                originalInputSettings.backgroundBehavior = originalBackgroundBehavior;
            }
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputManager.CinematicInputBlocked = false;
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            for (int i = 0; i < 5; i++) yield return null;
            yield return SceneManager.LoadSceneAsync("MainMenu");
        }

        [UnityTest]
        public IEnumerator AcceptedShotgunHitsConfirmAndRejectedShotsStaySilent()
        {
            var manager = owner.GetComponent<WeaponManager>();
            Check(manager.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Bucky), "Native shotgun candidate equips");
            yield return new WaitForSecondsRealtime(1);
            var weapon = manager.GetWeapon(0);
            var original = weapon.Data;
            var data = Object.Instantiate(original);
            data.damage = 1; data.hipSpreadAngle = data.aimedSpreadAngle = 0;
            Set(weapon, "weaponData", data);
            var movement = owner.GetComponent<PlayerMovement>();
            bool moving = movement.enabled; movement.enabled = false;
            Vector3 priorPosition = owner.transform.position;
            owner.transform.position = new Vector3(500, 20, 500);
            var rangeFloor = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(owner.transform.position - Vector3.up * .1f, Quaternion.identity, Vector3.one), size = new Vector3(30, .2f, 30) };
            testNavigation = NavMesh.AddNavMeshData(NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),
                new List<NavMeshBuildSource> { rangeFloor }, new Bounds(owner.transform.position, Vector3.one * 40), Vector3.zero, Quaternion.identity));
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FPS/Features/Characters/Content/Enemies/ZombieRig/Prefabs/ZombieRig.prefab");
            var target = Object.Instantiate(enemyPrefab, owner.transform.position + Vector3.forward * 4, Quaternion.identity);
            var ai = target.GetComponent<EnemyAI>(); ai.enabled = false;
            var agent = target.GetComponent<NavMeshAgent>(); agent.enabled = false;
            // A scene-owned host fixture avoids enabling navigation at the isolated firing range.
            var health = target.GetComponent<EnemyHealth>();
            var collider = target.GetComponent<Collider>();
            var segment = target.GetComponent<HitboxSegment>() ?? target.AddComponent<HitboxSegment>();
            segment.Configure(HitboxZone.Body, 1, target.GetComponent<NetworkObject>(), health);
            var handler = owner.GetComponent<WeaponFireHandler>();
            var marker = (Image)Get(HUDManager.Instance, "hitMarker");
            ushort sequence = 1;
            bool Fire(Vector3 direction, ulong sender, bool validate = false) => (bool)Call(handler, "TryProcessFireServer",
                owner.transform.position + Vector3.up, direction, 0, NetworkManager.Singleton.ServerTime.Time, sequence++, true, sender, validate, true, false);
            try
            {
                // EnemyHealth supports authoritative unspawned fixtures in a host; the shooter and feedback use NGO.
                health.SetMaxHealth(10000);
                Physics.SyncTransforms();
                Vector3 direction = (collider.bounds.center - owner.transform.position - Vector3.up).normalized;
                float before = health.CurrentHealth;
                Check(Fire(direction, owner.OwnerClientId), "Shotgun fire is accepted by server");
                TraceShotgunFeedback("immediate", health, before, handler, marker);
                Check(health.CurrentHealth < before, "Accepted shotgun pellets reduce target HP");
                Check(marker.enabled, "Accepted shotgun confirmation is visible on the shooter HUD");
                Check(marker.color == TacticalUiTheme.Text, "Shotgun body confirmation uses HUD text color");
                yield return null;
                TraceShotgunFeedback("next frame", health, before, handler, marker);
                Check(marker.enabled == (Time.unscaledTime < (float)Get(HUDManager.Instance, "hitMarkerUntil")),
                    "Shotgun marker follows its unscaled expiry even when an Editor frame exceeds 0.18 seconds");
                Check(data.projectileCount > 1, "Fixture uses the native shotgun pellet count");
                yield return new WaitForSecondsRealtime(data.FireInterval + .05f);
                segment.Configure(HitboxZone.Head, 1, target.GetComponent<NetworkObject>(), health);
                HUDManager.Instance.SetAimHudVisible(true, true, scopeSprite);
                Check(Fire(direction, owner.OwnerClientId), "Scoped headshot request is accepted");
                Check(marker.enabled && marker.color == (Color)GetStatic(typeof(HUDManager), "WarningColor"), "Headshot shotgun confirmation stays visible in scope");
                Check(((Image)Get(HUDManager.Instance, "scopeOverlayImage")).sprite == scopeSprite
                    && ((GameObject)Get(HUDManager.Instance, "scopeOverlayRoot")).activeInHierarchy, "Real scope artwork is visible during confirmation");
                // Queue the current frame: waiting for another Editor frame can exceed the real 0.18-second marker lifetime.
                Canvas.ForceUpdateCanvases();
                ScreenCapture.CaptureScreenshot(Evidence + "shotgun-headshot-scope.png");
                yield return null;
                yield return new WaitForSecondsRealtime(data.FireInterval + .05f);
                Check(Fire(Vector3.up, owner.OwnerClientId), "Miss still consumes an accepted shot");
                yield return null;
                Check(!marker.enabled, "Miss has no hit marker");
                yield return new WaitForSecondsRealtime(data.FireInterval + .05f);
                var filter = target.AddComponent<DamageFilter>(); Set(filter, "acceptedTypes", DamageType.Environment);
                before = health.CurrentHealth;
                Check(Fire(direction, owner.OwnerClientId), "Filtered shot request is accepted");
                yield return null;
                Check(!marker.enabled && health.CurrentHealth == before, "DamageFilter rejection has no confirmation");
                Check(!Fire(direction, owner.OwnerClientId + 1, true), "Wrong sender is rejected by server");
                yield return null;
                Check(!marker.enabled, "Rejected fire request has no marker");
                var confirm = typeof(WeaponFireHandler).GetMethod("ApplyConfirmedDamage", BindingFlags.Static | BindingFlags.NonPublic);
                Check(!(bool)confirm.Invoke(null, new object[] { owner, new DamageInfo(1, sourcePosition: Vector3.right) }), "Protected PlayerHealth does not confirm damage");
                HUDManager.Instance.SetAimHudVisible(false, false);
            }
            finally
            {
                Set(weapon, "weaponData", original); Object.Destroy(data); Object.Destroy(target);
                owner.transform.position = priorPosition; movement.enabled = moving;
                HUDManager.Instance.SetAimHudVisible(false, false);
            }
        }

        [UnityTest]
        public IEnumerator FpsSampleFeedbackTracksCombatAndPlayerLifecycle()
        {
            var manager = HUDManager.Instance;
            var marker = (Image)Get(manager, "hitMarker");
            var blood = (Image)Get(manager, "healthDangerBackground");
            var arrows = (Image[])Get(manager, "directionalDamageIndicators");
            var view = Camera.main.transform;
            var look = owner.GetComponent<MouseMovement>();
            var originalRotation = view.rotation; look.enabled = false;
            try
            {
                int imageCount = marker.transform.parent.GetComponentsInChildren<Image>(true).Length;
                manager.ShowHitConfirmed(HitboxZone.Body, 22);
                Check(marker.enabled && marker.color == TacticalUiTheme.Text, "Body hit uses a plain image in HUD text color");
                manager.SetAimHudVisible(true, false);
                Check(marker.enabled, "ADS does not hide hit feedback");
                manager.ShowHitConfirmed(HitboxZone.Head, 88);
                Check(marker.color == (Color)GetStatic(typeof(HUDManager), "WarningColor"), "Headshot changes marker color");
                manager.SetAimHudVisible(true, true, scopeSprite);
                Check(marker.enabled && marker.canvas.overrideSorting, "Scope feedback renders above scope artwork");
                float oldTimeScale = Time.timeScale; Time.timeScale = 0;
                try { yield return new WaitForSecondsRealtime(.22f); yield return null; }
                finally { Time.timeScale = oldTimeScale; }
                Check(!marker.enabled, "Hit marker expires while scaled time is paused");
                manager.SetAimHudVisible(false, false);
                Net(owner, "networkHealth", 44f);
                yield return new WaitForSecondsRealtime(.3f);
                Check(blood.enabled && blood.color.a > 0 && blood.color.a < .02f, "Vignette starts smoothly below 45 percent HP");
                Net(owner, "networkHealth", 10f);
                yield return new WaitForSecondsRealtime(.45f);
                Check(blood.color.a > .24f && blood.color.a <= .35f, "Vignette alpha rises linearly towards the 0.35 limit");
                yield return Capture("low-health-vignette");
                owner.Heal(90);
                yield return new WaitForSecondsRealtime(.7f);
                Check(!blood.enabled, "Healing above threshold fades the vignette out");
                Set(owner, "campaignProtectionUntil", 0d);
                Vector3 Source(float angle) => owner.transform.position + Quaternion.Euler(0, angle, 0)
                    * Vector3.ProjectOnPlane(view.forward, Vector3.up).normalized * 10;
                foreach (float angle in new[] { 0f, 90f, 180f, 270f }) owner.TakeDamage(new DamageInfo(1, sourcePosition: Source(angle)));
                yield return null;
                Check(arrows.Count(i => i.enabled) == 4, "Four simultaneous directions use four prebuilt images");
                owner.TakeDamage(new DamageInfo(1, sourcePosition: Source(10)));
                yield return null;
                var cues = (Array)Get(manager, "damageCues");
                Check(arrows.Count(i => i.enabled) == 4 && (Vector3)Get(cues.GetValue(0), "source") == Source(10), "Nearby direction refreshes its original slot");
                owner.TakeDamage(new DamageInfo(1, sourcePosition: Source(225)));
                yield return null;
                Check(Enumerable.Range(0, 4).Any(i => (Vector3)Get(cues.GetValue(i), "source") == Source(225)), "Fifth direction replaces the oldest cue");
                var prior = arrows[0].rectTransform.anchoredPosition;
                view.rotation = Quaternion.Euler(0, 90, 0) * view.rotation;
                yield return null;
                Check(Vector2.Distance(prior, arrows[0].rectTransform.anchoredPosition) > 100, "Directions follow owner camera yaw");
                Check(arrows.All(i => Mathf.Abs(i.rectTransform.anchoredPosition.magnitude - 128) < .01f), "Damage cues keep their 128-unit radius");
                yield return Capture("four-damage-directions");
                Call(manager, "ClearCombatFeedback");
                owner.TakeDamage(new DamageInfo(1, sourcePosition: owner.transform.position));
                owner.TakeDamage(new DamageInfo(1, sourcePosition: new Vector3(float.NaN, 0, 0)));
                owner.TakeInfectionDamage(1);
                yield return null;
                Check(arrows.All(i => !i.enabled), "Coincident source, invalid coordinates and sepsis create no fabricated direction");
                owner.TakeDamage(new DamageInfo(1, sourcePosition: Source(0)));
                yield return new WaitForSecondsRealtime(.82f); yield return null;
                Check(arrows[0].enabled && arrows[0].color.a > 0 && arrows[0].color.a < .75f, "Direction fades during the final 0.3 seconds");
                manager.ShowHitConfirmed(HitboxZone.Body, 1);
                ChangePhase(CampaignPhase.Insertion);
                yield return null; yield return null;
                Check(!marker.enabled && arrows.All(i => !i.enabled) && !blood.enabled, "Cinematic masks combat feedback");
                yield return new WaitForSecondsRealtime(.3f);
                ChangePhase(CampaignPhase.Exploring);
                yield return null; yield return null;
                Check(!marker.enabled && arrows.All(i => !i.enabled), "Feedback still expires behind an overlay");
                foreach (var state in new[] { PlayerLifeState.Downed, PlayerLifeState.Dead, PlayerLifeState.Spectating })
                {
                    owner.TakeDamage(new DamageInfo(1, sourcePosition: Source(0)));
                    manager.ShowHitConfirmed(HitboxZone.Body, 1);
                    Net(owner, "networkLifeState", state);
                    yield return null;
                    Check(!marker.enabled && !blood.enabled && arrows.All(i => !i.enabled), state + " clears feedback");
                    Net(owner, "networkLifeState", PlayerLifeState.Alive);
                }
                owner.TakeDamage(new DamageInfo(1, sourcePosition: Source(0)));
                manager.ShowHitConfirmed(HitboxZone.Body, 1);
                owner.Respawn(owner.transform.position, owner.transform.rotation);
                yield return null;
                Check(!marker.enabled && arrows.All(i => !i.enabled), "Respawn clears feedback even from the Alive state");
                Check(marker.transform.parent.GetComponentsInChildren<Image>(true).Length == imageCount, "Combat feedback creates no per-hit objects");
                var fireAction = reviewActions.FindAction("Fire");
                var aimAction = reviewActions.FindAction("Aim");
                string bindings = reviewActions.SaveBindingOverridesAsJson();
                try
                {
                    fireAction.ApplyBindingOverride(0, "<Keyboard>/f");
                    aimAction.ApplyBindingOverride(0, "<Mouse>/leftButton");
                    yield return null;
                    Check(!((Image)Get(manager, "firePromptIcon")).enabled && ((TMP_Text)Get(manager, "firePromptText")).text.Contains("F"), "Keyboard FIRE uses the selected binding label");
                    Check(((Image)Get(manager, "aimPromptIcon")).sprite == HudSkin.Get("FPSSample/UI_HUD_LMB"), "Rebound AIM uses left mouse artwork");
                    fireAction.ApplyBindingOverride(0, "");
                    yield return null;
                    Check(((TMP_Text)Get(manager, "firePromptText")).text == "-- FIRE", "Unbound FIRE shows -- without a mouse icon");
                }
                finally { reviewActions.LoadBindingOverridesFromJson(bindings); }
                var catalog = Resources.Load<PlayerPrefabCatalog>("PlayerPrefabCatalog");
                Check(catalog.TryGetPrefab(PlayerCharacterId.Brimstone, out var prefab), "Replacement owner prefab exists");
                var replacement = Object.Instantiate(prefab, owner.transform.position, owner.transform.rotation).GetComponent<PlayerHealth>();
                replacement.PrepareInitialSpawn(new SessionPlayerId(1));
                replacement.NetworkObject.SpawnAsPlayerObject(NetworkManager.Singleton.LocalClientId, true);
                yield return null;
                Check((PlayerHealth)Get(manager, "playerHealth") == replacement && !blood.enabled, "PlayerObject replacement binds new HP immediately");
                owner.TakeDamage(new DamageInfo(1, sourcePosition: owner.transform.position + Vector3.right * 10));
                yield return null;
                Check(arrows.All(i => !i.enabled), "Old owner event subscription is removed");
                Set(replacement, "campaignProtectionUntil", 0d);
                replacement.TakeDamage(new DamageInfo(1, sourcePosition: replacement.transform.position + Vector3.right * 10));
                yield return null;
                Check(arrows.Count(i => i.enabled) == 1, "New owner receives fresh feedback");
                replacement.NetworkObject.Despawn();
                yield return null;
                Check(!marker.enabled && arrows.All(i => !i.enabled) && !blood.enabled, "Owner despawn clears all temporary feedback");
                yield return Capture("feedback-lifecycle");
            }
            finally { view.rotation = originalRotation; look.enabled = true; InputManager.CinematicInputBlocked = false; }
        }

        static object GetStatic(Type type, string field) => type.GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        void TraceShotgunFeedback(string stage, EnemyHealth target, float before, WeaponFireHandler shooter, Image marker)
        {
            var manager = HUDManager.Instance;
            var bound = (PlayerHealth)Get(manager, "playerHealth");
            checks.Add($"SHOTGUN {stage}: HP={target.CurrentHealth}/{before}; enabled={marker.enabled}; active={marker.gameObject.activeInHierarchy}; color={marker.color}; expected={TacticalUiTheme.Text}; until={Get(manager, "hitMarkerUntil")}; now={Time.unscaledTime}; duration={Get(manager, "hitMarkerDuration")}; delta={Time.unscaledDeltaTime}; visible={HudLayout.GameplayVisible}; owner={owner.IsOwner}/{owner.OwnerClientId}; shooterOwner={shooter.IsOwner}/{shooter.OwnerClientId}; boundOwner={bound == owner}; life={owner.LifeState}");
        }

        void ReviewMouse(MouseState state)
        {
            // Match the existing ADS fixture: process the synthetic input frame, then consume its toggle edge.
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
            var weapon = owner.GetComponent<WeaponManager>().CurrentWeapon?.GetComponent<Weapon>();
            if (weapon != null) Call(weapon, "UpdateAimPresentation");
            checks.Add($"INPUT mouseEnabled={mouse.enabled}; RMB={mouse.rightButton.isPressed}; aim={InputManager.Instance.GetAimInput()}; down={InputManager.Instance.GetAimInputDown()}; blocked={InputManager.GameplayInputBlocked}; weapon={weapon?.name}; aiming={weapon?.IsAimRequested}");
        }

        [UnityTest]
        public IEnumerator GameplayStatesAndLayoutRemainReadable()
        {
            AddSquadFixture();
            var squad = Object.FindAnyObjectByType<SquadHUD>();
            var rows = (IList)Get(squad, "rows");
            var teammate = (PlayerHealth)Get(rows[0], "player");
            Net(teammate, "networkLifeState", PlayerLifeState.Downed);
            Net(teammate, "networkLifeStateDeadline", campaign.Now + 40);
            BindSquadFixture();
            yield return null;
            Check(((TMP_Text)Get(rows[0], "status")).text.Contains("DOWNED"), "Squad preserves downed direction and bleed-out time");
            CheckText(squad.GetComponentsInChildren<TMP_Text>());
            yield return Capture("squad-downed");
            var squadView = Camera.main.transform;
            var squadLook = owner.GetComponent<MouseMovement>();
            var squadRotation = squadView.rotation;
            squadLook.enabled = false;
            squadView.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(squadView.position - teammate.transform.position, Vector3.up));
            yield return null;
            Check(((TMP_Text)Get(rows[0], "status")).text.Contains("BEHIND DOWNED"), "Squad keeps direction when the downed teammate is behind the camera");
            CheckText(squad.GetComponentsInChildren<TMP_Text>());
            yield return Capture("squad-downed-behind");
            squadView.rotation = squadRotation;
            squadLook.enabled = true;
            teammate.ReviveCampaign(35);
            BindSquadFixture();
            yield return null;
            Check(!((TMP_Text)Get(rows[0], "status")).text.Contains("DOWNED"), "Squad updates after native revive");
            var inventory = owner.GetComponent<SurvivalInventory>();
            inventory.CycleThrowable();
            yield return null;
            Check(inventory.SelectedThrowable.Value == ThrowableKind.Incendiary, "Grenade cycle changes selected type");
            Check(Find<TMP_Text>("SelectionDetail").enabled, "Selected grenade detail appears");
            yield return new WaitForSecondsRealtime(2.1f);
            // Realtime waits can resume before the HUD Update processes the expiry frame.
            yield return null;
            Check(!Find<TMP_Text>("SelectionDetail").enabled, "Secondary detail expires after two seconds");

            owner.ReviveCampaign(35);
            owner.GetComponent<PlayerInfectionController>().SetInfectionServer(80);
            yield return null; yield return new WaitForEndOfFrame();
            Check(!Find<RectTransform>("InteractionPrompt", true).gameObject.activeInHierarchy,
                "Idle self-treatment uses hotbar keys without a duplicate SELF prompt");
            inventory.RequestUse(ConsumableKind.Antidote);
            yield return new WaitForSecondsRealtime(.5f);
            Check(inventory.IsUsingItem && inventory.UseProgress > 0, "Antidote progress is live");
            Check(Find<TMP_Text>("MedicalAction").enabled, "Active treatment remains visible");
            foreach (string medical in new[] { "MEDKIT", "ANTIDOTE" })
            {
                var ring = Find<RectTransform>(medical).Find("Progress").GetComponent<Image>();
                Check(ring.sprite == HudSkin.Get("FPSSample/GoalProgress") && ring.fillMethod == Image.FillMethod.Radial360
                    && ring.fillOrigin == (int)Image.Origin360.Top && ring.fillClockwise, "Medical progress uses the clockwise FPSSample ring: " + medical);
            }
            yield return Capture("treatment");
            inventory.CancelUse();
            yield return null;
            Check(!inventory.IsUsingItem, "Treatment cancellation clears progress");
            Check(Find<RectTransform>("ANTIDOTE").Find("Progress").GetComponent<Image>().fillAmount == 0, "Cancelled ring is empty");
            inventory.RequestUse(ConsumableKind.Medkit);
            yield return null;
            Check(inventory.IsUsingItem, "Medkit starts through the inventory API");
            inventory.CancelUse();
            yield return null;
            foreach (var kind in new[] { ConsumableKind.Medkit, ConsumableKind.Antidote })
            {
                Set(owner, "campaignProtectionUntil", campaign.Now + 600);
                float healthBefore = owner.CurrentHealth;
                var infection = owner.GetComponent<PlayerInfectionController>();
                float infectionBefore = infection.CurrentInfection;
                int stockBefore = kind == ConsumableKind.Medkit ? inventory.MedkitCount.Value : inventory.AntidoteCount.Value;
                inventory.RequestUse(kind);
                Check(inventory.IsUsingItem, kind + " completion starts through the authoritative inventory API");
                float deadline = Time.realtimeSinceStartup + SurvivalRules.UseDuration(kind, true) + 2;
                while (inventory.IsUsingItem && Time.realtimeSinceStartup < deadline) yield return null;
                yield return null;
                Check(!inventory.IsUsingItem && (kind == ConsumableKind.Medkit ? inventory.MedkitCount.Value : inventory.AntidoteCount.Value) == stockBefore - 1,
                    kind + " completion consumes exactly one item");
                Check(kind == ConsumableKind.Medkit ? owner.CurrentHealth == Mathf.Min(owner.MaxHealth, healthBefore + 50)
                    : infection.CurrentInfection == Mathf.Max(0, infectionBefore - 40), kind + " completion applies the native treatment result");
                Check(Find<RectTransform>(kind == ConsumableKind.Medkit ? "MEDKIT" : "ANTIDOTE").Find("Progress").GetComponent<Image>().fillAmount == 0,
                    kind + " completion clears its only progress display");
            }
            yield return Capture("treatment-completed");
            owner.ReviveCampaign(35);
            owner.GetComponent<PlayerInfectionController>().SetInfectionServer(80);
            inventory.FragGrenadeCount.Value = 0;
            yield return null;
            var frag = Find<RectTransform>("FRAG");
            Check(frag.Find("Count").GetComponent<TMP_Text>().text == "0" && frag.Find("Unavailable").gameObject.activeSelf,
                "Empty quick slot has zero and a geometric slash");
            var reserve = Find<TMP_Text>("ReserveAmmoText");
            string liveAmmo = reserve.text;
            reserve.text = "/ 999";
            CheckText(new[] { reserve });
            reserve.text = liveAmmo;
            owner.GetComponent<PlayerInfectionController>().SetInfectionServer(100);
            yield return null;
            Check(Find<TMP_Text>("SepsisWarning") != null, "Sepsis warning is visible at maximum infection");
            CheckLayout();
            yield return Capture("sepsis");
            owner.GetComponent<PlayerInfectionController>().SetInfectionServer(80);
            yield return null;

            var interactionController = owner.GetComponent<InteractionManager>();
            bool scanning = interactionController.enabled;
            interactionController.enabled = false; // Keep the populated target prompt while capturing the layout fixture.
            foreach (var resolution in new[] { "1280x720", "1920x1080", "3440x1440" })
                foreach (var scale in new[] { "100%", "125%" })
                {
                    EditorApplication.ExecuteMenuItem("Tools/FPS/UI/HUD Review/Resolution/" + resolution);
                    EditorApplication.ExecuteMenuItem("Tools/FPS/UI/HUD Review/Scale/" + scale);
                    yield return new WaitForSecondsRealtime(.2f);
                    hud.ShowMessage("Interaction interrupted; completed steps are preserved.");
                    hud.ResetDialogue();
                    hud.EnqueueDialogue(CampaignDialogueId.ClientSuppressionOrder);
                    HUDManager.Instance.SetInteractionPrompt("[F] Inspect the isolation panel", true);
                    float dialogueDeadline = Time.realtimeSinceStartup + 1;
                    while (string.IsNullOrEmpty(Find<Text>("MandatorySubtitles").text)
                        && Time.realtimeSinceStartup < dialogueDeadline) yield return null;
                    CheckLayout();
                    Check(!string.IsNullOrEmpty(Find<Text>("MandatorySubtitles").text), "Matrix includes populated subtitles");
                    var subtitles = Find<RectTransform>("SubtitleBand");
                    var safe = subtitles.GetComponentInParent<UiSafeArea>().GetComponent<RectTransform>();
                    Check(Bounds(subtitles).yMin <= Bounds(safe).yMin + 20 * subtitles.GetComponentInParent<Canvas>().scaleFactor,
                        "Gameplay subtitles stay at the bottom safe margin");
                    var normalPrompt = Find<RectTransform>("InteractionPrompt", true);
                    var interactionText = Find<Text>("Interaction");
                    bool updating = hud.enabled;
                    hud.enabled = false;
                    interactionText.text = "INTERACTING 50%";
                    HUDManager.Instance.SetInteractionPrompt("[F] Inspect the isolation panel", true);
                    yield return new WaitForEndOfFrame();
                    Check(!normalPrompt.gameObject.activeInHierarchy && interactionText.isActiveAndEnabled,
                        "Hold progress replaces the normal interaction prompt in their shared band");
                    CheckLayout();
                    interactionText.text = ""; hud.enabled = updating;
                    yield return null; yield return new WaitForEndOfFrame();
                    HUDManager.Instance.SetInteractionPrompt("[F] Inspect the isolation panel", true);
                    yield return Capture("stress-" + resolution + "-" + scale.Replace("%", ""));
                    TeamInventoryUI.Instance.Toggle();
                    var missions = TeamInventoryUI.Instance.GetComponentsInChildren<Button>(true).First(b => b.name == "MissionsTab");
                    missions.onClick.Invoke();
                    yield return null;
                    Check(InputManager.GameplayInputBlocked && !HudLayout.GameplayVisible, "Inventory owns gameplay input");
                    CheckText(TeamInventoryUI.Instance.GetComponentsInChildren<TMP_Text>());
                    var subtitleBounds = Bounds(Find<RectTransform>("SubtitleBand"));
                    Check(!subtitleBounds.Overlaps(Bounds(Find<RectTransform>("ListViewport")))
                        && !subtitleBounds.Overlaps(Bounds(Find<RectTransform>("Detail"))), "Overlay subtitle strip clears inventory content");
                    yield return Capture("missions-" + resolution + "-" + scale.Replace("%", ""));
                    TeamInventoryUI.Instance.Close();
                    yield return null;
                    Check(!InputManager.GameplayInputBlocked && Cursor.lockState == CursorLockMode.Locked, "Closing inventory restores input and cursor");
                    hud.ResetDialogue();
                }

            interactionController.enabled = scanning;
            EditorApplication.ExecuteMenuItem("Tools/FPS/UI/HUD Review/Scale/100%");
            EditorApplication.ExecuteMenuItem("Tools/FPS/UI/HUD Review/Resolution/1920x1080");
            var look = owner.GetComponent<MouseMovement>();
            var view = look.BodyCam.transform;
            var viewRotation = view.localRotation;
            look.enabled = false;
            foreach (var angle in new[] { -55f, 0f, 55f })
            {
                view.localRotation = Quaternion.Euler(angle, 0, 0);
                yield return Capture(angle < 0 ? "reticle-bright" : angle > 0 ? "reticle-dark" : "reticle-detail");
            }
            view.localRotation = viewRotation; look.enabled = true;
            ReviewMouse( new MouseState().WithButton(MouseButton.Right));
            yield return new WaitForSecondsRealtime(.8f);
            Check(InputManager.Instance.GetAimInput(), "Review mouse reaches the Aim action");
            Check(HudLayout.SecondaryReduced && Find<RectTransform>("MissionTracker").rect.height < 100, "ADS reduces tracker");
            CheckLayout();
            Check(!Find<TMP_Text>("SelectionDetail").enabled, "ADS hides secondary item hints");
            yield return Capture("ads");
            ReviewMouse( new MouseState());
            yield return null;
            ReviewMouse( new MouseState().WithButton(MouseButton.Left));
            yield return null;
            Check(HudLayout.SecondaryReduced, "Firing reduces secondary HUD");
            ReviewMouse( new MouseState());
            Check(WeaponManager.LocalInstance.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Operator), "Scope weapon can be equipped for review");
            yield return new WaitForSecondsRealtime(2.5f);
            ReviewMouse( new MouseState().WithButton(MouseButton.Right));
            yield return new WaitForSecondsRealtime(1.5f);
            Check(((GameObject)Get(HUDManager.Instance, "scopeOverlayRoot")).activeSelf, "Operator ADS shows the existing scope overlay");
            var scopeRoot = ((GameObject)Get(HUDManager.Instance, "scopeOverlayRoot")).transform;
            var peripheral = (RectTransform)Get(HUDManager.Instance, "peripheralRoot");
            Check(scopeRoot.parent == peripheral.parent && scopeRoot.GetSiblingIndex() < peripheral.GetSiblingIndex(),
                "Opaque scope artwork stays behind the ammo and prompt HUD");
            yield return Capture("scope-presentation");
            ReviewMouse( new MouseState());
            yield return null;

            var menu = Object.FindAnyObjectByType<InGameMenuUI>();
            for (int i = 0; i < 3; i++)
            {
                Call(menu, "TogglePauseMenu");
                yield return null;
                Check(!HudLayout.GameplayVisible && InputManager.GameplayInputBlocked, "Pause hides HUD and blocks gameplay");
                Call(menu, "OpenSettings");
                yield return null;
                Check(!HudLayout.GameplayVisible && Cursor.visible, "Settings owns focus");
                Call(menu, "CloseSettings"); menu.ResumeGame();
                yield return null;
                Check(!InputManager.GameplayInputBlocked, "Repeated pause/settings closes cleanly");
            }
            ChangePhase(CampaignPhase.Insertion);
            yield return null; yield return null;
            Check(!HudLayout.GameplayVisible, "Cinematic suppresses persistent HUD");
            Check(!Find<RectTransform>("MinimalHUDRoot", true).gameObject.activeInHierarchy, "Cinematic hides authored reticle and blood graphics");
            Check(!((Image)Get(HUDManager.Instance, "scopeOverlayImage")).isActiveAndEnabled, "Cinematic hides scope graphic");
            yield return Capture("cinematic-presentation");
            ChangePhase(CampaignPhase.Exploring);
            yield return null; yield return null;
            Net(owner, "networkLifeState", PlayerLifeState.Downed);
            Net(owner, "networkLifeStateDeadline", campaign.Now + 40);
            yield return null;
            Check(!hud.HasMissionTracker && Find<TMP_Text>("LifeState").text.Contains("DOWNED"), "Downed keeps dedicated life presentation");
            yield return Capture("downed");
            owner.SetCampaignSpectating();
            yield return null;
            Check(Find<TMP_Text>("LifeState").text.Contains("SPECTATING"), "Spectating has dedicated presentation");
            yield return Capture("spectating");
            owner.ReviveCampaign(50);
            ChangePhase(CampaignPhase.Preparing);
            yield return null;
            hud.CloseDocument();
            yield return null;
            Check(Find<Text>("CurrentAction").text.Contains("READY"), "Ready count is in the tracker");
            yield return Capture("preparation-tracker");
            Call(hud, "OpenJournal");
            ChangePhase(CampaignPhase.Encounter);
            yield return null; yield return null;
            Check(!CampaignHUD.IsDocumentOpen && hud.HasMissionTracker, "Countdown transition closes the preparation guide and keeps visible tracker");
            Check(Find<Text>("CurrentAction").text.Contains("remaining"), "Countdown is in the tracker");
            yield return Capture("countdown-tracker");
            ReviewMouse( new MouseState().WithButton(MouseButton.Right));
            yield return null; yield return null;
            Check(Find<Text>("TrackedObjective").text.Contains("EXTRACTION /"), "ADS keeps countdown in the visible compact tracker");
            ReviewMouse( new MouseState());
            ChangePhase(CampaignPhase.Failed);
            Call(campaign, "PublishRunSummary");
            yield return null; yield return null;
            Check(CampaignStatusUI.IsDebriefOpen && Cursor.visible, "Debrief opens and unlocks cursor");
            Check(Find<Button>("RetryCheckpoint").gameObject.activeInHierarchy, "Failed debrief offers checkpoint retry");
            CheckText(CampaignStatusUI.Instance.GetComponentsInChildren<TMP_Text>());
            yield return Capture("debrief");
            var restoredState = new CampaignState { phase = CampaignPhase.Exploring };
            Set(campaign, "checkpoint", new CampaignCheckpoint { state = restoredState, players = new List<CampaignPlayerSave> { CampaignPlayerSave.Capture(owner) } });
            campaign.RequestRetry();
            yield return null; yield return null;
            Check(campaign.State.phase == CampaignPhase.Exploring && !CampaignStatusUI.IsDebriefOpen, "Checkpoint retry restores gameplay presentation through the real API");
        }

        [UnityTest]
        public IEnumerator NavigationStaysActiveAndInsertionRestoresPlayerCameras()
        {
            hud.enabled = false;
            Check(hud.TryTrackObjective(CampaignObjectiveId.FactoryIsolate), "Track a live objective");
            var target = campaign.Objective(hud.TrackedObjectiveId);
            var point = new GameObject("Transient stable navigation target").transform;
            var originalPoint = target.interactionPoint;
            var rig = Object.FindAnyObjectByType<HelicopterInsertionRappel>(FindObjectsInactive.Include);
            Assert.That(rig, Is.Not.Null);
            var cameras = owner.GetComponentsInChildren<Camera>(true);
            var enabledBefore = cameras.Select(c => c.enabled).ToArray();
            try
            {
                point.position = owner.transform.position + Vector3.forward * 20;
                target.interactionPoint = point;
                Set(hud, "nextNavigationUpdate", float.MaxValue);
                Call(hud, "UpdateNavigation", owner);
                var marker = Find<RectTransform>("NavigationMarker", true);
                Assert.That(marker.gameObject.activeSelf, Is.True);
                var probe = marker.gameObject.AddComponent<NavigationActivationProbe>();
                for (int i = 0; i < 5; i++) Call(hud, "UpdateNavigation", owner);
                Assert.That(probe.Disables, Is.Zero, "Stable navigation must not deactivate and rebuild its graphics each frame.");
                ChangePhase(CampaignPhase.Failed);
                Call(hud, "UpdateNavigation", owner);
                Assert.That(marker.gameObject.activeSelf, Is.False);
                Assert.That(probe.Disables, Is.EqualTo(1), "Deactivate only when guidance becomes hidden.");
                ChangePhase(CampaignPhase.Exploring);
                Object.Destroy(probe);
                Assert.That(enabledBefore.Any(value => value), Is.True);
                ChangePhase(CampaignPhase.Insertion);
                Set(rig, "nextVisibilityScan", 0f);
                Call(rig, "LateUpdate");
                Assert.That(cameras.All(c => !c.enabled), Is.True, "Only the cinematic camera should render during insertion.");
                for (int i = 0; i < cameras.Length; i++) cameras[i].enabled = enabledBefore[i];
                Set(rig, "nextVisibilityScan", float.MaxValue);
                Call(rig, "LateUpdate");
                Assert.That(cameras.All(c => !c.enabled), Is.True, "Weapon updates must not re-enable rendering between visibility scans.");
                rig.StopPresentation();
                Assert.That(cameras.Select(c => c.enabled), Is.EqualTo(enabledBefore), "Restore each original camera state, including spectator cameras.");
                rig.StopPresentation();
                Assert.That(cameras.Select(c => c.enabled), Is.EqualTo(enabledBefore), "Stopping twice must be safe.");
                ChangePhase(CampaignPhase.Exploring);
                yield return null;
            }
            finally
            {
                target.interactionPoint = originalPoint;
                Object.Destroy(point.gameObject);
                if (rig != null) rig.StopPresentation();
                ChangePhase(CampaignPhase.Exploring);
                hud.enabled = true;
            }
        }

        [UnityTest]
        public IEnumerator NavigationUsesLiveSceneAndInvalidatesConsistently()
        {
            var camera = owner.GetComponentInChildren<Camera>();
            Check(hud.TryTrackObjective(CampaignObjectiveId.FactoryIsolate), "Available objective can be tracked manually");
            var target = campaign.Objective(hud.TrackedObjectiveId);
            for (int degrees = 0; degrees < 360; degrees += 45)
            {
                camera.transform.rotation = Quaternion.Euler(0, degrees, 0);
                Call(hud, "UpdateNavigation", owner);
                Check(hud.TrackedObjectiveId == CampaignObjectiveId.FactoryIsolate, "Camera rotation keeps tracked objective");
                var marker = Find<RectTransform>("NavigationMarker", true);
                Check(marker.gameObject.activeSelf && Screen.safeArea.Contains(marker.position), "Marker remains in safe area during 360 degree turn");
                CheckNavigation();
            }
            yield return Capture("navigation-behind");
            yield return ReviewMarkerPlacement(target, camera);
            campaign.State.completed |= CampaignState.Bit(CampaignObjectiveId.FactoryIsolate);
            campaign.State.revision++;
            yield return null;
            Check(hud.TrackedObjectiveId != CampaignObjectiveId.FactoryIsolate, "Shared completion automatically selects a valid objective");
            Check(hud.TryTrackObjective(CampaignObjectiveId.FactoryBackup), "Unlocked objective can be tracked");
            campaign.State.completed = 0; campaign.State.revision++;
            yield return null;
            Check(hud.TrackedObjectiveId != CampaignObjectiveId.FactoryBackup, "Checkpoint rollback invalidates a locked objective");
            campaign.State.chapter = CampaignChapter.Asylum; campaign.State.revision++;
            yield return null;
            Check(CampaignRules.ChapterOf(hud.TrackedObjectiveId) == CampaignChapter.Asylum, "Chapter change selects a matching objective");
            campaign.State.chapter = CampaignChapter.Factory; campaign.State.revision++;
            yield return null;
            hud.TryTrackObjective(CampaignObjectiveId.FactoryIsolate);
            target = campaign.Objective(hud.TrackedObjectiveId);

            var door = campaign.doors.First(d => d != null && d.name == "FactoryInterlock" && d.navigationBlocker != null);
            door.SetOpen(false);
            ApplyDoorFixture();
            yield return new WaitForSecondsRealtime(1.2f);
            Check(door.IsClosed && door.navigationBlocker.enabled, "Locked progression door keeps its NavMesh blocker");
            var blocker = door.navigationBlocker;
            var gateCenter = blocker.transform.TransformPoint(blocker.center);
            gateCenter.y -= blocker.size.y * blocker.transform.lossyScale.y * .5f - .15f;
            bool acrossX = blocker.size.x < blocker.size.z;
            var normal = acrossX ? blocker.transform.right : blocker.transform.forward;
            float offset = (acrossX ? blocker.size.x * blocker.transform.lossyScale.x : blocker.size.z * blocker.transform.lossyScale.z) * .5f + 1f;
            Check(NavMesh.SamplePosition(gateCenter - normal * offset, out var gateA, .8f, NavMesh.AllAreas)
                && NavMesh.SamplePosition(gateCenter + normal * offset, out _, .8f, NavMesh.AllAreas), "Gate sampling stays on the actual floor");
            NavMesh.SamplePosition(gateCenter + normal * offset, out var gateB, .8f, NavMesh.AllAreas);
            Check(NavMesh.Raycast(gateA.position, gateB.position, out _, NavMesh.AllAreas), "Closed door blocks the direct NavMesh crossing");
            door.SetOpen(true);
            ApplyDoorFixture();
            float carvingDeadline = Time.realtimeSinceStartup + 5;
            while (NavMesh.Raycast(gateA.position, gateB.position, out _, NavMesh.AllAreas)
                && Time.realtimeSinceStartup < carvingDeadline) yield return null;
            Check(door.IsOpen && !blocker.enabled, "Completed prerequisites open the door and remove its blocker");
            Check(!NavMesh.Raycast(gateA.position, gateB.position, out _, NavMesh.AllAreas), "Opening the authored door updates the live NavMesh crossing");
            campaign.State.completed = 0; campaign.State.revision++;
            door.SetOpen(false);
            ApplyDoorFixture();
            hud.TryTrackObjective(CampaignObjectiveId.FactoryIsolate);

            owner.GetComponent<MouseMovement>().enabled = false;
            var controller = owner.GetComponent<CharacterController>(); controller.enabled = false;
            owner.GetComponent<PlayerMovement>().enabled = false;
            owner.transform.position = target.Point - Vector3.up * 1.55f + Vector3.right * .5f;
            yield return null;
            if (target.InReach(owner, campaign.settings.interactionRange))
            {
                Call(hud, "UpdateNavigation", owner);
                Check(!Find<RectTransform>("NavigationMarker", true).gameObject.activeSelf, "An interaction in reach with line of sight suppresses its marker");
            }
            else Assert.Fail("The authored isolation point must remain reachable for the proximity review.");
            var previousObjectives = campaign.Current.objectives;
            campaign.Current.objectives = previousObjectives.Where(o => o != target).ToArray();
            Call(hud, "UpdateNavigation", owner);
            Check(!Find<RectTransform>("NavigationMarker", true).gameObject.activeSelf && hud.HasMissionTracker, "Missing target hides marker while tracker survives");
            campaign.Current.objectives = previousObjectives;

            // Two temporary disconnected walkable islands exercise partial and invalid paths on Unity's real NavMesh.
            var settings = NavMesh.GetSettingsByIndex(0);
            var sources = new List<NavMeshBuildSource>();
            foreach (float x in new[] { 10000f, 10012f }) sources.Add(new NavMeshBuildSource
            { shape = NavMeshBuildSourceShape.Box, size = new Vector3(6, .2f, 6), transform = Matrix4x4.TRS(new Vector3(x, 0, 10000), Quaternion.identity, Vector3.one) });
            sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(6, .2f, 6), transform = Matrix4x4.TRS(new Vector3(10000, 4, 10000), Quaternion.identity, Vector3.one) });
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(new Vector3(10006, 2, 10000), new Vector3(26, 12, 12)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            testNavigation = NavMesh.AddNavMeshData(data);
            owner.transform.position = new Vector3(10000, .2f, 10000);
            var originalPoint = target.interactionPoint;
            var point = new GameObject("Transient HUD navigation target").transform;
            target.interactionPoint = point;
            try
            {
                point.position = new Vector3(10012, 1.55f, 10000);
                Net(campaign, "phaseStarted", campaign.Now);
                Set(hud, "nextNavigationUpdate", 0f);
                yield return null;
                Check(!(bool)Get(hud, "routeComplete"), "Disconnected islands cannot claim a complete route");
                Check(Find<Text>("Distance").text.Contains("TO TARGET") && !Find<Text>("Distance").text.Contains("ROUTE"), "Partial path labels straight distance explicitly");
                Check(Find<Text>("CurrentAction").text.Contains("Route unavailable"), "Partial path reports unavailable route");
                point.position = new Vector3(10000, 5.55f, 10000);
                Set(hud, "nextNavigationUpdate", 0f);
                yield return null;
                Check(!(bool)Get(hud, "routeComplete") && Find<Text>("Distance").text.Contains("HIGHER"), "Sampling an upper floor does not invent a route from the lower floor");
                point.position += Vector3.up * 20;
                Set(hud, "nextNavigationUpdate", 0f);
                yield return null;
                Check(!(bool)Get(hud, "routeComplete") && Find<Text>("Distance").text.Contains("HIGHER"), "Invalid elevated target preserves objective and height cue");
                yield return Capture("navigation-invalid");
                point.position = new Vector3(10012, -10, 10000);
                Set(hud, "nextNavigationUpdate", 0f);
                yield return null;
                Check(Find<Text>("Distance").text.Contains("LOWER"), "Lower destinations use a height cue without guessing storey numbers");
                camera.gameObject.SetActive(false);
                Call(hud, "UpdateNavigation", owner);
                Check(!Find<RectTransform>("NavigationMarker", true).gameObject.activeSelf && hud.HasMissionTracker, "Missing camera hides marker while tracker survives");
                camera.gameObject.SetActive(true);
            }
            finally { target.interactionPoint = originalPoint; testNavigation.Remove(); Object.Destroy(point.gameObject); Object.Destroy(data); }
            Check(!campaign.State.Has(CampaignObjectiveId.FactoryIsolate), "Navigation never completes an objective by proximity");
        }

        IEnumerator ReviewMarkerPlacement(CampaignInteractable target, Camera camera)
        {
            var look = owner.GetComponent<MouseMovement>();
            bool wasLooking = look.enabled;
            var rotation = camera.transform.rotation;
            var originalPoint = target.interactionPoint;
            var point = new GameObject("Transient marker bearing target").transform;
            GameObject obstruction = null;
            look.enabled = false;
            target.interactionPoint = point;
            try
            {
                point.position = camera.transform.position + rotation * Vector3.forward * 20;
                Set(hud, "routeComplete", false); Set(hud, "routeComputed", true);
                Set(hud, "nextNavigationUpdate", float.MaxValue);
                foreach (string resolution in new[] { "1280x720", "1920x1080", "3440x1440" })
                    foreach (string scale in new[] { "100%", "125%" })
                    {
                        EditorApplication.ExecuteMenuItem("Tools/FPS/UI/HUD Review/Resolution/" + resolution);
                        EditorApplication.ExecuteMenuItem("Tools/FPS/UI/HUD Review/Scale/" + scale);
                        yield return new WaitForSecondsRealtime(.2f);
                        foreach (float pitch in new[] { -65f, 0f, 65f })
                            for (int yaw = 0; yaw < 360; yaw += 5)
                            {
                                camera.transform.rotation = rotation * Quaternion.Euler(pitch, yaw, 0);
                                Call(hud, "UpdateNavigation", owner);
                                CheckNavigation();
                                var marker = Find<RectTransform>("NavigationMarker");
                                var projected = camera.WorldToScreenPoint(point.position);
                                if (pitch == 0 && yaw == 0)
                                    Check(Vector2.Distance(marker.position, projected) <= .5f, "Marker stays on a target at the reticle without jumping sideways");
                                var arrow = Find<RectTransform>("RouteCorner", true);
                                if (arrow.gameObject.activeSelf)
                                {
                                    var clip = camera.projectionMatrix * (camera.worldToCameraMatrix * new Vector4(point.position.x, point.position.y, point.position.z, 1));
                                    var bearing = new Vector2(clip.x * camera.pixelWidth, clip.y * camera.pixelHeight);
                                    if (bearing.sqrMagnitude < .0001f) bearing = Vector2.down;
                                    float expected = Mathf.Atan2(bearing.y, bearing.x) * Mathf.Rad2Deg;
                                    Check(Mathf.Abs(Mathf.DeltaAngle(arrow.localEulerAngles.z + 90, expected)) <= .1f,
                                        "Arrow keeps the target bearing while its label avoids the HUD");
                                }
                            }
                        camera.transform.rotation = rotation;
                        Call(hud, "UpdateNavigation", owner); Call(hud, "UpdateTracker", owner);
                        CheckLayout();
                        var tracker = Find<RectTransform>("MissionTracker");
                        foreach (var label in tracker.GetComponentsInChildren<Text>()) CheckPadding(label.rectTransform, tracker, 32, 24);
                        Check(tracker.GetComponent<Image>().sprite == null, "Tracker uses a thin border instead of the heavy decorative frame");
                        yield return Capture("tracker-marker-" + resolution + "-" + scale.Replace("%", ""));
                    }

                EditorApplication.ExecuteMenuItem("Tools/FPS/UI/HUD Review/Scale/100%");
                EditorApplication.ExecuteMenuItem("Tools/FPS/UI/HUD Review/Resolution/1920x1080");
                yield return null;
                InputState.Change(mouse, new MouseState().WithButton(MouseButton.Right));
                Call(hud, "UpdateTracker", owner);
                Check(HudLayout.SecondaryReduced, "Direct review input enables compact tracker");
                var compact = Find<RectTransform>("MissionTracker");
                CheckPadding(Find<Text>("TrackedObjective").rectTransform, compact, 32, 24);
                yield return Capture("tracker-compact");
                InputState.Change(mouse, new MouseState());

                // A sampled start below the player's root must never become a waypoint. Occluders cannot swap the selected turn.
                point.position = camera.transform.position + rotation * Vector3.forward * 50;
                var turn = owner.transform.position + rotation * new Vector3(6, 0, 15);
                Set(hud, "routeCorners", new[] { owner.transform.position - Vector3.up * 2, turn, point.position });
                Set(hud, "nextRouteCorner", 1);
                Set(hud, "routeDistance", 24f); Set(hud, "routeComplete", true);
                Call(hud, "UpdateNavigation", owner);
                var expectedTurn = camera.WorldToScreenPoint(turn + Vector3.up * .65f);
                Check(Vector2.Distance(Find<RectTransform>("NavigationMarker").position, expectedTurn) <= .5f, "Guidance follows the next turn rather than the sampled start");
                obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstruction.name = "Transient marker occluder";
                obstruction.transform.position = Vector3.Lerp(camera.transform.position, point.position, .5f);
                obstruction.transform.localScale = Vector3.one * 3;
                Physics.SyncTransforms();
                Call(hud, "UpdateNavigation", owner);
                Check(Vector2.Distance(Find<RectTransform>("NavigationMarker").position, expectedTurn) <= .5f, "Camera occlusion does not switch between waypoint and destination");
                yield return Capture("navigation-next-turn");
                var controller = owner.GetComponent<CharacterController>();
                bool wasControlling = controller.enabled;
                var playerPosition = owner.transform.position;
                controller.enabled = false;
                try
                {
                    owner.transform.position = turn;
                    Call(hud, "UpdateNavigation", owner);
                    owner.transform.position = turn + rotation * Vector3.forward * 4;
                    Call(hud, "UpdateNavigation", owner);
                    var destination = camera.WorldToScreenPoint(point.position);
                    Check(Vector2.Distance(Find<RectTransform>("NavigationMarker").position, destination) <= .5f,
                        "Guidance does not return to a passed turn between path refreshes");
                }
                finally { owner.transform.position = playerPosition; controller.enabled = wasControlling; }
            }
            finally
            {
                target.interactionPoint = originalPoint;
                camera.transform.rotation = rotation; look.enabled = wasLooking;
                InputState.Change(mouse, new MouseState());
                Set(hud, "nextNavigationUpdate", 0f); Set(hud, "routeComputed", false);
                if (obstruction != null) Object.Destroy(obstruction);
                Object.Destroy(point.gameObject);
            }
            yield return null;
        }

        void AddSquadFixture()
        {
            var squad = Object.FindAnyObjectByType<SquadHUD>();
            var roster = (NetworkList<CampaignInsertionParticipant>)Get(campaign, "insertionRoster");
            for (int i = 0; i < 3; i++)
            {
                var catalog = Resources.Load<PlayerPrefabCatalog>("PlayerPrefabCatalog");
                Assert.That(catalog.TryGetPrefab((PlayerCharacterId)(i + 1), out var prefab), Is.True);
                var actor = Object.Instantiate(prefab, owner.transform.position + Vector3.right * (10 + i * 3), Quaternion.identity).GetComponent<PlayerHealth>();
                actor.PrepareInitialSpawn(new SessionPlayerId((ulong)i + 2));
                actor.NetworkObject.SpawnWithOwnership((ulong)i + 100, true); actor.ReviveCampaign(i == 0 ? 35 : 80);
                actor.GetComponent<PlayerInfectionController>().SetInfectionServer(i == 1 ? 80 : 20);
                roster.Add(new CampaignInsertionParticipant { playerId = (ulong)i + 2, character = (PlayerCharacterId)(i + 1), connected = true });
                squadActors.Add(actor);
            }
            Call(squad, "RebuildRoster");
            BindSquadFixture();
        }

        void BindSquadFixture()
        {
            // Native roster updates only bind connected peers; these review actors have transient ownership IDs.
            var squad = Object.FindAnyObjectByType<SquadHUD>();
            var rows = (IList)Get(squad, "rows");
            for (int i = 0; i < 3; i++)
            { Set(rows[i], "player", squadActors[i]); Set(rows[i], "infectionSource", squadActors[i].GetComponent<PlayerInfectionController>()); }
            Set(squad, "nextBindingRefresh", Time.unscaledTime + 600);
        }

        void CheckLayout()
        {
            Canvas.ForceUpdateCanvases();
            Check(HUDManager.Instance.ValidateCrosshairAlignment(out float error) && error <= .5f, "Reticle stays within 0.5px of viewport center");
            string[] names = { "MissionTracker", "SquadPanel", "HealthPanel", "InfectionPanel", "WeaponPanel", "SurvivalHotbar", "InteractionPrompt", "Interaction", "Feedback", "MandatorySubtitles" };
            var groups = names.Select(n => Find<RectTransform>(n)).Where(r => r != null && r.gameObject.activeInHierarchy
                && r.GetComponentsInChildren<Graphic>().Any(g => g.isActiveAndEnabled
                    && (g is not Text text || !string.IsNullOrEmpty(text.text))
                    && (g is not TMP_Text tmp || !string.IsNullOrEmpty(tmp.text)))).ToArray();
            for (int i = 0; i < groups.Length; i++)
                for (int j = i + 1; j < groups.Length; j++)
                    Check(!Bounds(groups[i]).Overlaps(Bounds(groups[j])), "HUD groups remain separate: " + groups[i].name + " / " + groups[j].name);
            CheckText(Object.FindObjectsByType<TMP_Text>().Where(t => t.canvas != null && t.canvas.renderMode != RenderMode.WorldSpace));
            foreach (var text in Object.FindObjectsByType<Text>())
                if (text.isActiveAndEnabled && text.canvas != null && !string.IsNullOrEmpty(text.text))
                {
                    Check(text.preferredHeight <= text.rectTransform.rect.height + .5f, "Legacy text has enough height: " + text.name);
                    Check(text.fontSize * text.canvas.scaleFactor >= 15.99f, "Quick legacy text is at least 16px: " + text.name);
                }
            foreach (string name in new[] { "HealthPanel", "InfectionPanel", "WeaponPanel", "MissionTracker" })
            {
                var panel = Find<RectTransform>(name);
                if (panel == null || !panel.gameObject.activeInHierarchy) continue;
                foreach (var text in panel.GetComponentsInChildren<TMP_Text>()) CheckPadding(text.rectTransform, panel, 16, 16);
                foreach (var text in panel.GetComponentsInChildren<Text>()) CheckPadding(text.rectTransform, panel, 16, 16);
            }
            var squad = Object.FindAnyObjectByType<SquadHUD>();
            foreach (var text in squad.GetComponentsInChildren<TMP_Text>())
                CheckPadding(text.rectTransform, (RectTransform)text.transform.parent, 16, 12);
            var infectionIcon = Find<Image>("InfectionIcon");
            if (infectionIcon.isActiveAndEnabled)
            {
                CheckPadding(infectionIcon.rectTransform, (RectTransform)infectionIcon.transform.parent, 16, 16);
                Check(!Bounds(infectionIcon.rectTransform).Overlaps(Bounds(Find<TMP_Text>("InfectionStageText").rectTransform)),
                    "Infection icon clears its stage label");
            }
            foreach (var text in Object.FindAnyObjectByType<SurvivalHotbar>().GetComponentsInChildren<TMP_Text>())
                if (text.name is "Count" or "Key")
                {
                    var slot = (RectTransform)text.transform.parent;
                    CheckPadding(text.rectTransform, slot, 12, 12);
                    if (text.name == "Key")
                    {
                        Check(slot.Find("Keycap") == null, "Hotbar shortcut has no panel: " + slot.name);
                        Check(text.alignment == TextAlignmentOptions.TopRight, "Hotbar shortcut sits at the top right: " + slot.name);
                    }
                }
            CheckNavigation();
        }

        void CheckPadding(RectTransform text, RectTransform panel, float horizontal, float vertical)
        {
            var inner = Bounds(text); var outer = Bounds(panel);
            float scale = text.GetComponentInParent<Canvas>().scaleFactor;
            Check(inner.xMin >= outer.xMin + horizontal * scale - .5f
                && inner.xMax <= outer.xMax - horizontal * scale + .5f
                && inner.yMin >= outer.yMin + vertical * scale - .5f
                && inner.yMax <= outer.yMax - vertical * scale + .5f,
                "Text clears its decorative frame: " + panel.name + " / " + text.name);
        }

        void CheckNavigation()
        {
            var marker = Find<RectTransform>("NavigationMarker", true);
            Check(marker.Find("DistancePlate") == null, "Navigation distance has no panel");
            var distance = marker.Find("Distance") as RectTransform;
            Check(distance != null && distance.GetComponent<Image>() == null, "Distance text sits directly under the navigation marker");
            if (!marker.gameObject.activeInHierarchy) return;
            var safe = Screen.safeArea;
            float margin = HudLayout.Margin * distance.GetComponentInParent<Canvas>().scaleFactor;
            var label = Bounds(distance);
            foreach (string name in new[] { "MandatorySubtitles", "Interaction", "Feedback" })
            {
                var text = Find<Text>(name);
                if (text != null && text.isActiveAndEnabled && !string.IsNullOrWhiteSpace(text.text))
                    Check(!label.Overlaps(Bounds(text.rectTransform)), "Navigation distance clears lower text: " + name);
            }
            Check(label.xMin >= safe.xMin + margin - .5f && label.xMax <= safe.xMax - margin + .5f
                && label.yMin >= safe.yMin + margin - .5f && label.yMax <= safe.yMax - margin + .5f,
                "Navigation text keeps its full rectangle away from screen edges");
        }

        void CheckText(IEnumerable<TMP_Text> texts)
        {
            foreach (var text in texts)
                if (text.isActiveAndEnabled && text.canvas != null && text.canvas.enabled && !string.IsNullOrEmpty(text.text))
                {
                    text.ForceMeshUpdate();
                    Check(!text.isTextTruncated && !text.isTextOverflowing, "Text renders without truncation: " + text.name + " / " + text.text);
                    bool weaponPrompt = text.transform.parent.name is "FirePrompt" or "AimPrompt";
                    Check(weaponPrompt ? Mathf.Approximately(text.fontSize, 16) : text.fontSize * text.canvas.scaleFactor >= 15.99f,
                        "Text uses its required size: " + text.name);
                }
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Evidence + name + ".png");
            yield return null;
        }
        void ChangePhase(CampaignPhase phase) { campaign.State.phase = phase; campaign.State.revision++; }
        void ApplyDoorFixture()
        {
            ulong mask = 0;
            for (int i = 0; i < campaign.doors.Length && i < 64; i++)
                if (campaign.doors[i] != null && campaign.doors[i].DesiredOpen(campaign.State)) mask |= 1UL << i;
            Net(campaign, "openDoorMask", mask);
        }
        void Check(bool condition, string message) { checks.Add((condition ? "PASS " : "FAIL ") + message); Assert.That(condition, Is.True, message); }
        static T Find<T>(string name, bool inactive = false) where T : Component => Object.FindObjectsByType<T>(inactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude).FirstOrDefault(c => c.name == name);
        static object Get(object target, string field) => target.GetType().GetField(field, Private | BindingFlags.Public).GetValue(target);
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Private | BindingFlags.Public).SetValue(target, value);
        static void Net<T>(object target, string field, T value) => ((NetworkVariable<T>)Get(target, field)).Value = value;
        static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        static Rect Bounds(RectTransform rect) { var corners = new Vector3[4]; rect.GetWorldCorners(corners); return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y); }
    }
}
public sealed class NavigationActivationProbe : MonoBehaviour
{
    public int Disables { get; private set; }
    private void OnDisable() => Disables++;
}
#endif
