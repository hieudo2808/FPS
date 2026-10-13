using System;
using System.Collections.Generic;
using UnityEngine;

namespace FPS
{
    /// <summary>Clock-sampled visuals only. Campaign/player authority is unchanged.</summary>
    [DisallowMultipleComponent]
    public sealed class HelicopterInsertionRappel : MonoBehaviour
    {
        public const float AuthoredDuration = 28f;
        private const float RopeRetractStart = 17.8f, RopeRetractEnd = 19.8f;
        [Header("Aircraft and flight markers")]
        public GameObject visuals;
        public Transform approach, hover, departure, mainRotor, tailRotor;
        public GameObject doorModel;
        public AnimationClip doorOpen, doorClose;
        [Header("Two ropes and character templates (never a fixed party)")]
        public Transform[] ropeAnchors = new Transform[2];
        public Transform[] floorAttachments = new Transform[2];
        public Transform[] ropes = new Transform[2];
        [Min(.1f)] public float descentSeconds = 1.8f;
        public HelicopterRappellerPose[] rappellers = new HelicopterRappellerPose[4];
        public PlayerCharacterId[] rappellerCharacters = { PlayerCharacterId.Brimstone, PlayerCharacterId.Clove, PlayerCharacterId.Gekko, PlayerCharacterId.Sage };
        public Transform[] arrivals = new Transform[4];
        public Camera shotCamera;
        [Tooltip("Local cabin floor positions, paired with the two sill anchors.")]
        public Vector3[] cabinStarts = { new Vector3(-.8f,.83f,-3.75f), new Vector3(-.8f,.83f,-2.95f) };

        private readonly Dictionary<Renderer, bool> hiddenPlayers = new();
        private Quaternion mainRest, tailRest;
        private Vector3 mainAxis, tailAxis;
        private bool initialized, presenting;
        private float nextVisibilityScan;
        private readonly List<CampaignInsertionParticipant> participants = new();
        private readonly List<HelicopterRappellerPose> actors = new();
        private readonly Dictionary<Camera, bool> hiddenPlayerCameras = new();
        private Vector3[] localRopeAnchors;
        private Renderer[] floorLeads;
        private Vector3 flightOffset;
        public IReadOnlyList<HelicopterRappellerPose> Actors => actors;
        public CampaignInsertionParticipant Participant(int slot) => participants[slot];

        private void Awake() { Initialize(); StopPresentation(); }
        private void Initialize()
        {
            if (initialized) return;
            // Non-Legacy clips need an Animator when sampled in a standalone player.
            if (Application.isPlaying && doorModel != null && doorModel.GetComponent<Animator>() == null)
                doorModel.AddComponent<Animator>();
            localRopeAnchors = new Vector3[ropeAnchors.Length];
            floorLeads = new Renderer[ropeAnchors.Length];
            for (int i = 0; i < ropeAnchors.Length; i++)
                if (ropeAnchors[i] != null)
                {
                    localRopeAnchors[i] = transform.InverseTransformPoint(ropeAnchors[i].position);
                    if (i < ropes.Length && ropes[i] != null)
                    {
                        var ropeMaterial = ropes[i].GetComponentInChildren<Renderer>(true).sharedMaterial;
                        foreach (var renderer in ropeAnchors[i].GetComponentsInChildren<Renderer>(true))
                            if (renderer.sharedMaterial == ropeMaterial) floorLeads[i] = renderer;
                    }
                }
            if (mainRotor != null)
            {
                mainRest = mainRotor.localRotation;
                mainAxis = mainRotor.parent.InverseTransformDirection(transform.up);
            }
            if (tailRotor != null)
            {
                tailRest = tailRotor.localRotation;
                tailAxis = tailRotor.parent.InverseTransformDirection(transform.right);
            }
            initialized = true;
        }
        private void OnDisable() => StopPresentation();
        private void OnDestroy() => ClearCast();
        private void LateUpdate()
        {
            var campaign = CampaignMissionController.Instance;
            bool active = campaign != null && campaign.IsSpawned && campaign.InsertionReady && NetworkMatchStateManager.IsGameplayActive &&
                campaign.State.chapter == CampaignChapter.Factory && campaign.State.phase == CampaignPhase.Insertion;
            if (!active) { if (presenting) StopPresentation(); return; }
            bool changed = participants.Count != campaign.InsertionPartySize;
            for (int i = 0; !changed && i < participants.Count; i++)
                changed = !participants[i].Equals(campaign.InsertionParticipant(i));
            if (changed)
            {
                var roster = new CampaignInsertionParticipant[campaign.InsertionPartySize];
                for (int i = 0; i < roster.Length; i++) roster[i] = campaign.InsertionParticipant(i);
                SetParticipants(roster);
            }
            if (Time.unscaledTime >= nextVisibilityScan)
            {
                nextVisibilityScan = Time.unscaledTime + .25f;
                foreach (var player in campaign.Players)
                {
                    foreach (var r in player.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!hiddenPlayers.ContainsKey(r)) hiddenPlayers.Add(r, r.forceRenderingOff);
                        r.forceRenderingOff = true;
                    }
                    // The cinematic camera already renders the world; player cameras would render it again underneath.
                    if (shotCamera == null) continue;
                    foreach (var camera in player.GetComponentsInChildren<Camera>(true))
                    {
                        if (!hiddenPlayerCameras.ContainsKey(camera)) hiddenPlayerCameras.Add(camera, camera.enabled);
                        camera.enabled = false;
                    }
                }
            }
            // Weapon presentation can re-enable its camera in Update between visibility scans.
            foreach (var camera in hiddenPlayerCameras.Keys) if (camera != null) camera.enabled = false;
            float seconds = (float)(campaign.Now - campaign.PhaseStarted);
            Evaluate(seconds * AuthoredDuration / Mathf.Max(.01f, campaign.settings.insertionSeconds));
        }

        /// <summary>Consumes the replicated roster; editor preview uses the same path with explicit sample participants.</summary>
        public void SetParticipants(IReadOnlyList<CampaignInsertionParticipant> roster)
        {
            bool rebuild = roster.Count != participants.Count;
            for (int i = 0; !rebuild && i < roster.Count; i++)
                rebuild = roster[i].playerId != participants[i].playerId || roster[i].character != participants[i].character;
            if (rebuild) ClearCast();
            participants.Clear();
            for (int i = 0; i < roster.Count; i++)
            {
                var entry = roster[i];
                participants.Add(entry);
                if (!rebuild) continue;
                int template = Array.IndexOf(rappellerCharacters, entry.character);
                if (template < 0 || template >= rappellers.Length || rappellers[template] == null)
                    throw new InvalidOperationException("Missing insertion visual for " + entry.character);
                var actor = Instantiate(rappellers[template], rappellers[template].transform.parent);
                actor.name = "Insertion_Player_" + entry.playerId + "_" + entry.character;
                actor.gameObject.hideFlags = HideFlags.DontSave;
                actor.gameObject.SetActive(false);
                actors.Add(actor);
            }
            foreach (var template in rappellers) if (template != null) template.gameObject.SetActive(false);
        }

        private void ClearCast()
        {
            foreach (var actor in actors)
                if (actor != null)
                {
                    actor.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(actor.gameObject);
                    else DestroyImmediate(actor.gameObject);
                }
            actors.Clear(); participants.Clear();
        }

        /// <summary>Deterministic sampling also used by the editor review; no NetworkObject is moved.</summary>
        public void Evaluate(float seconds)
        {
            Initialize();
            presenting = true;
            float t = Mathf.Clamp(seconds, 0, AuthoredDuration);
            if (visuals == null || approach == null || hover == null || departure == null) return;
            Vector3 center = Vector3.zero;
            foreach (var arrival in arrivals) center += arrival.position / arrivals.Length;
            int lanes = Mathf.Min(2, participants.Count);
            float duration = Mathf.Max(.1f, descentSeconds);
            Vector3 drop = Vector3.zero;
            for (int lane = 0; lane < lanes; lane++) drop += (hover.position + hover.rotation * localRopeAnchors[lane]) / lanes;
            flightOffset = lanes > 0 ? Vector3.ProjectOnPlane(center - drop, Vector3.up) : Vector3.zero;
            visuals.SetActive(t < AuthoredDuration);
            if (t < 5.5f) SetFlight(approach, hover, Smooth(t / 5.5f));
            else if (t > 21.2f) SetFlight(hover, departure, Smooth((t-21.2f)/6.8f));
            else transform.SetPositionAndRotation(hover.position + flightOffset + Vector3.up * (Mathf.Sin(t*2.2f)*.035f), hover.rotation);
            if (mainRotor != null) mainRotor.localRotation = Quaternion.AngleAxis(t * 1440f, mainAxis) * mainRest;
            if (tailRotor != null) tailRotor.localRotation = Quaternion.AngleAxis(t * 2280f, tailAxis) * tailRest;
            if (doorModel != null && doorOpen != null && doorClose != null)
            {
                if (t < 20f) doorOpen.SampleAnimation(doorModel, Mathf.Clamp(t-5.5f, 0, doorOpen.length));
                else doorClose.SampleAnimation(doorModel, Mathf.Clamp(t-20f, 0, doorClose.length));
            }
            for (int lane = 0; lane < ropes.Length; lane++)
            {
                if (ropes[lane] == null || ropeAnchors[lane] == null) continue;
                bool occupied = false;
                for (int slot = lane; slot < participants.Count; slot += 2) occupied |= participants[slot].connected;
                bool visible = occupied && t >= 7f && t < AuthoredDuration;
                ropes[lane].gameObject.SetActive(visible && t < RopeRetractEnd);
                ropeAnchors[lane].gameObject.SetActive(visible);
                if (floorLeads[lane] != null) floorLeads[lane].enabled = t < RopeRetractEnd;
                if (!visible || t >= RopeRetractEnd) continue;
                Vector3 top = ropeAnchors[lane].position;
                Vector3 bottom = participants[lane].arrival - Vector3.up*.11f;
                // Let the free end sway to the next landing point before that pair descends.
                for (int slot=lane+2;slot<participants.Count;slot+=2)
                {
                    float previousLanding = 8.2f + lane*.7f + (slot/2-1)*4.6f + .65f + duration + .45f;
                    bottom = Vector3.Lerp(bottom,participants[slot].arrival-Vector3.up*.11f,
                        Smooth((t-previousLanding)/Mathf.Max(.1f,4.6f-.65f-duration-.45f)));
                }
                // Reel in before the cabin door closes at 20 seconds; keep the bend on the sill.
                float amount = Smooth((t-7f)/1.1f) * (1-Smooth((t-RopeRetractStart)/(RopeRetractEnd-RopeRetractStart)));
                Vector3 end = Vector3.Lerp(top, bottom, amount);
                ropes[lane].SetPositionAndRotation(top, Quaternion.FromToRotation(Vector3.down,bottom-top));
                ropes[lane].localScale = new Vector3(1, Vector3.Distance(top, end), 1);
            }
            for (int i=0;i<actors.Count;i++)
            {
                var actor = actors[i];
                var participant = participants[i];
                if (!participant.connected) { actor.gameObject.SetActive(false); continue; }
                int lane = i%2;
                float begin = 8.2f + lane*.7f + (i/2)*4.6f;
                float elapsed = t-begin;
                actor.gameObject.SetActive(elapsed >= 0 && t < AuthoredDuration);
                if (elapsed < 0) continue;
                Vector3 top = ropeAnchors[lane].position;
                Vector3 hang = top - Vector3.up*1.95f;
                // Arrival transforms include the player capsule's 15 cm spawn lift.
                // Cosmetic feet should rest on the actual courtyard surface.
                Vector3 groundArrival = participant.arrival - Vector3.up * .15f;
                float touchdown = .65f + duration;
                float descend = Mathf.Clamp01((elapsed-.65f)/duration);
                if (elapsed < .65f)
                    actor.transform.position = Vector3.Lerp(transform.TransformPoint(cabinStarts[lane]),hang,Smooth(elapsed/.65f));
                else if (elapsed < touchdown)
                    actor.transform.position = Vector3.Lerp(hang,groundArrival,descend);
                else actor.transform.position = groundArrival;
                float landed = Smooth((elapsed-touchdown)/.45f);
                actor.transform.rotation = Quaternion.Slerp(hover.rotation*Quaternion.Euler(0,90,0),participant.rotation,landed);
                actor.Pose(1-landed, elapsed, touchdown);
                if (actor.leftHand != null && actor.rightHand != null)
                {
                    Vector3 grip = (actor.leftHand.position+actor.rightHand.position)*.5f;
                    Vector3 ropeGrip = Vector3.Lerp(groundArrival+Vector3.up*.04f,top,
                        Mathf.InverseLerp(groundArrival.y+.04f,top.y,grip.y));
                    Vector3 gripOffset = Vector3.Lerp(grip-actor.transform.position,grip-ropeGrip,Smooth(elapsed/.65f));
                    actor.transform.position -= Vector3.ProjectOnPlane(gripOffset, Vector3.up) * (1-landed);
                }
            }
            if (shotCamera != null)
            {
                shotCamera.enabled = t < AuthoredDuration;
                Vector3 eye, target;
                if (t < 5.5f) { eye=transform.position+new Vector3(8,5,-18); target=transform.position+Vector3.up*2; }
                else if (t < 8.2f) { eye=hover.position+flightOffset+new Vector3(4,2.4f,-11); target=ropeAnchors[0].position-Vector3.up*.8f; }
                else if (t < 21.2f) { eye=center+new Vector3(-7,4.0f,-10); target=center+Vector3.up*Mathf.Lerp(5.2f,1.2f,Smooth((t-10.4f)/6f)); }
                else { eye=center+new Vector3(-9,3,-15); target=Vector3.Lerp(center+Vector3.up*2,transform.position+Vector3.up*2,Smooth((t-21.2f)/2)); }
                shotCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye,Vector3.up));
            }
        }
        private void SetFlight(Transform a, Transform b, float t)
        {
            transform.SetPositionAndRotation(Vector3.Lerp(a.position,b.position,t)+flightOffset,Quaternion.Slerp(a.rotation,b.rotation,t));
        }
        private static float Smooth(float t) => Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
        public void StopPresentation()
        {
            if (visuals != null) visuals.SetActive(false);
            if (shotCamera != null) shotCamera.enabled=false;
            foreach(var actor in rappellers) if(actor!=null) actor.gameObject.SetActive(false);
            ClearCast();
            foreach(var rope in ropes) if(rope!=null) rope.gameObject.SetActive(false);
            if(floorLeads!=null)foreach(var lead in floorLeads)if(lead!=null)lead.enabled=true;
            foreach(var anchor in ropeAnchors) if(anchor!=null) anchor.gameObject.SetActive(false);
            foreach(var pair in hiddenPlayers) if(pair.Key!=null) pair.Key.forceRenderingOff=pair.Value;
            hiddenPlayers.Clear();
            foreach (var pair in hiddenPlayerCameras) if (pair.Key != null) pair.Key.enabled = pair.Value;
            hiddenPlayerCameras.Clear();
            presenting=false;
        }
    }
}
