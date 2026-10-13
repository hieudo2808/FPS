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
        const string Evidence = "Documents/UIUX/HUD-Evidence/HotbarKeys-2026-10-06/";
        CampaignMissionController campaign;
        CampaignHUD hud;
        PlayerHealth owner;
        Mouse mouse;
        Keyboard keyboard;
        InputActionAsset reviewActions;
        InputDevice[] originalDevices;
        InputSettings originalInputSettings, reviewInputSettings;
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
            reviewInputSettings = Object.Instantiate(originalInputSettings);
            reviewInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            reviewInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = reviewInputSettings;
            reviewActions.devices = new InputDevice[] { mouse, keyboard };
        }

        [UnityTearDown]
        public IEnumerator StopGameplay()
        {
            File.WriteAllLines(Evidence + TestContext.CurrentContext.Test.Name + ".txt", checks);
            if (testNavigation.valid) testNavigation.Remove();
            if (reviewActions != null) reviewActions.devices = originalDevices;
            if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
            if (reviewInputSettings != null) Object.Destroy(reviewInputSettings);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputManager.CinematicInputBlocked = false;
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            for (int i = 0; i < 5; i++) yield return null;
            yield return SceneManager.LoadSceneAsync("MainMenu");
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
            inventory.RequestUse(ConsumableKind.Antidote);
            yield return new WaitForSecondsRealtime(.5f);
            Check(inventory.IsUsingItem && inventory.UseProgress > 0, "Antidote progress is live");
            Check(Find<TMP_Text>("MedicalAction").enabled, "Active treatment remains visible");
            yield return Capture("treatment");
            inventory.CancelUse();
            yield return null;
            Check(!inventory.IsUsingItem, "Treatment cancellation clears progress");
            inventory.RequestUse(ConsumableKind.Medkit);
            yield return null;
            Check(inventory.IsUsingItem, "Medkit starts through the inventory API");
            inventory.CancelUse();
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
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return new WaitForSecondsRealtime(.8f);
            Check(InputManager.Instance.GetAimInput(), "Review mouse reaches the Aim action");
            Check(HudLayout.SecondaryReduced && Find<RectTransform>("MissionTracker").rect.height < 100, "ADS reduces tracker");
            CheckLayout();
            Check(!Find<TMP_Text>("SelectionDetail").enabled, "ADS hides secondary item hints");
            yield return Capture("ads");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null;
            Check(HudLayout.SecondaryReduced, "Firing reduces secondary HUD");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            Check(WeaponManager.LocalInstance.TryReplacePrimaryWeaponServer(PrimaryWeaponId.Operator), "Scope weapon can be equipped for review");
            yield return new WaitForSecondsRealtime(2.5f);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return new WaitForSecondsRealtime(1.5f);
            Check(((GameObject)Get(HUDManager.Instance, "scopeOverlayRoot")).activeSelf, "Operator ADS shows the existing scope overlay");
            yield return Capture("scope-presentation");
            InputSystem.QueueStateEvent(mouse, new MouseState());
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
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return null; yield return null;
            Check(Find<Text>("TrackedObjective").text.Contains("EXTRACTION /"), "ADS keeps countdown in the visible compact tracker");
            InputSystem.QueueStateEvent(mouse, new MouseState());
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
            var groups = names.Select(n => Find<RectTransform>(n)).Where(r => r != null && r.gameObject.activeInHierarchy).ToArray();
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
                    Check(text.fontSize * text.canvas.scaleFactor >= 15.99f, "Quick text is at least 16px: " + text.name);
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
#endif
