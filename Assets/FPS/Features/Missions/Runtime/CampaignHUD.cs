using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FPS
{
    public sealed partial class CampaignHUD : MonoBehaviour
    {
        public static CampaignHUD Instance { get; private set; }
        private Canvas canvas;
        private RectTransform panel, controls;
        private GameObject confirmation;
        private Text status, body, feedback, progress, subtitle;
        private RectTransform documentViewport;
        private float subtitleUntil;
        private readonly System.Collections.Generic.List<CampaignDialogueDefinition> pendingDialogue = new();
        private AudioSource dialogueAudio;
        private CampaignInteractable focused;
        private bool journal, aidHeld, holding, mouseHeld;
        private bool releaseBeforeHold;
        private int first, second;
        private float holdStart, nextHeartbeat, nextAidHeartbeat, messageUntil, hintTime;
        private CampaignObjectiveId hintTarget;
        private CampaignInteractable nearbyHint;
        private float nextThreatCheck, combatUntil;
        private bool nearbyThreat;
        private ulong hintCompleted;
        private Font font;
        private string message = "";
        private PlayerHealth Owner => NetworkManager.Singleton?.LocalClient?.PlayerObject?.GetComponent<PlayerHealth>();
        private CampaignMissionController Campaign => CampaignMissionController.Instance;
        private void Awake() { Instance = this; Build(); }
        private void OnDestroy() { if (Instance == this) Instance = null; InputManager.CampaignInputBlocked = false; }
        private void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var c = new GameObject("CampaignCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(AudioSource));
            c.transform.SetParent(transform, false); canvas = c.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = TacticalUiTheme.CampaignOrder;
            dialogueAudio = c.GetComponent<AudioSource>(); dialogueAudio.playOnAwake = false; dialogueAudio.spatialBlend = 0;
            var scaler = c.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600,900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            status = Label(c.transform, "Objective", new Vector2(.61f,.78f), new Vector2(.98f,.97f), 22);
            navigation = Label(c.transform, "RouteGuidance", new Vector2(.61f,.70f), new Vector2(.98f,.78f), 19);
            feedback = Label(c.transform, "Feedback", new Vector2(.2f,.2f), new Vector2(.8f,.28f), 22); feedback.alignment = TextAnchor.MiddleCenter;
            progress = Label(c.transform, "Interaction", new Vector2(.26f,.10f), new Vector2(.74f,.19f), 21); progress.alignment = TextAnchor.MiddleCenter;
            subtitle = Label(c.transform,"MandatorySubtitles",new Vector2(.17f,.29f),new Vector2(.83f,.39f),22);subtitle.alignment=TextAnchor.MiddleCenter;
            var shadow=subtitle.gameObject.AddComponent<Shadow>();shadow.effectColor=Color.black;shadow.effectDistance=new Vector2(1,-1);
            panel = Box(c.transform, "JournalAndDocument", new Vector2(.18f,.12f), new Vector2(.82f,.87f));
            documentViewport=Box(panel,"DocumentViewport",new Vector2(.04f,.36f),new Vector2(.96f,.93f));
            documentViewport.GetComponent<Image>().color=Color.clear;documentViewport.gameObject.AddComponent<RectMask2D>();
            body = Label(documentViewport, "Document", Vector2.zero, Vector2.one, 23);
            body.rectTransform.anchorMin=new Vector2(0,1);body.rectTransform.anchorMax=Vector2.one;body.rectTransform.pivot=new Vector2(0,1);
            var scroll=documentViewport.gameObject.AddComponent<ScrollRect>();scroll.content=body.rectTransform;scroll.viewport=documentViewport;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            controls = Box(panel, "Controls", new Vector2(.04f,.07f), new Vector2(.96f,.36f)); controls.GetComponent<Image>().color = Color.clear;
            Button(panel, "CLOSE / I", new Vector2(.79f,.94f), new Vector2(.98f,.995f), Close);
            panel.gameObject.SetActive(false);
        }
        private RectTransform Box(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent,false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin=min; r.anchorMax=max; r.offsetMin=r.offsetMax=Vector2.zero;
            go.GetComponent<Image>().color = TacticalUiTheme.Surface; return r;
        }
        private Text Label(Transform parent, string name, Vector2 min, Vector2 max, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
            var t=go.GetComponent<Text>();t.font=font;t.fontSize=size;t.color=TacticalUiTheme.Text;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
        }
        private UnityEngine.UI.Button Button(Transform parent,string label,Vector2 min,Vector2 max,Action clicked)
        {
            var r=Box(parent,label,min,max);r.GetComponent<Image>().color=Color.white;
            var b=r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.colors=TacticalUiTheme.ButtonColors(); b.onClick.AddListener(()=>clicked());
            var t=Label(r,"Label",new Vector2(.02f,0),new Vector2(.98f,1),19);t.alignment=TextAnchor.MiddleCenter;t.text=label;return b;
        }
        private void ClearControls()
        {
            mouseHeld = false;
            hintOffered = false;
            body.rectTransform.anchoredPosition=Vector2.zero;
            if (confirmation != null) { confirmation.SetActive(false); Destroy(confirmation); confirmation = null; }
            foreach (Transform c in controls) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
        }
        public void Open(CampaignInteractable item)
        {
            if (item == null || Campaign == null || Campaign.BlocksInput) return;
            if (item != null && item.documentOnly)
            {
                Campaign.RequestDiscoverFile(item.fileId);
                return;
            }
            CancelAid();
            focused=item;journal=false;first=second=0;
            CampaignFileDefinition linkedFile = item.LinkedFile;
            if (linkedFile != null && !Campaign.State.HasFile(linkedFile.id))
                Campaign.RequestDiscoverFile(linkedFile.id);
            if(hintTarget!=item.objectiveId){hintTime=0;hintOffered=false;hintTarget=item.objectiveId;}
            if(item.objectiveId==CampaignObjectiveId.LabPower)first=Campaign.State.powerSelection;
            panel.gameObject.SetActive(true);RenderDocument();
        }
        public void OpenGate(CampaignDoor door)
        {
            if (Campaign == null || Campaign.BlocksInput) return;
            Close(); journal = true; panel.gameObject.SetActive(true);
            body.text = door.displayName.ToUpperInvariant() + "\n\n"
                + CampaignDialogue.AccessInstructions(Campaign.State, door.accessObjective)
                + "\n\n" + ObjectiveText();
            Button(controls, "CLOSE GUIDE", new Vector2(.2f,.2f), new Vector2(.8f,.8f), Close);
        }
        private void RenderDocument()
        {
            if(focused==null)return;
            body.text=focused.Title.ToUpperInvariant()+"\n\n"+focused.Instructions;
            CampaignFileDefinition file = focused.LinkedFile;
            if (file != null && !string.IsNullOrWhiteSpace(file.body))
                body.text += "\n\nEVIDENCE REFERENCE\n" + file.body;
            if (focused.objectiveId is CampaignObjectiveId.FactoryRoute or CampaignObjectiveId.AsylumLift)
                body.text += "\n\n" + CampaignDialogue.AccessInstructions(Campaign.State, focused.objectiveId);
            ClearControls();
            if(focused.objectiveId==CampaignObjectiveId.LabPower)
            {
                string[] loads={"SAFETY · 2","TEST RIG · 4","DATA · 2","FREIGHT LIFT · 2"};
                for(int i=0;i<4;i++){int bit=i;Button(controls,((first&(1<<i))!=0?"[ON] ":"[OFF] ")+loads[i],new Vector2(i*.25f,.46f),new Vector2((i+1)*.25f-.01f,.98f),()=>{int mask=first^(1<<bit);if(CampaignRules.CanSelectPower(mask)){first=mask;Campaign.RequestPowerSelection(mask);RenderDocument();}else ShowMessage("Keep SAFETY online; total load cannot exceed 6.");});}
            }
            else
            {
                AddChoices(focused.Choices,false,.48f,.99f);
                AddChoices(focused.SecondaryChoices,true,.03f,.46f);
            }
            var confirm=Button(panel,Campaign.State.Has(focused.objectiveId)?"RECORDED BY SQUAD":"HOLD INTERACT TO CONFIRM",new Vector2(.04f,.01f),new Vector2(.76f,.065f),()=>{});
            confirmation = confirm.gameObject;
            var trigger=confirm.gameObject.AddComponent<EventTrigger>();
            var down=new EventTrigger.Entry{eventID=EventTriggerType.PointerDown};down.callback.AddListener(_=>mouseHeld=true);trigger.triggers.Add(down);
            var up=new EventTrigger.Entry{eventID=EventTriggerType.PointerUp};up.callback.AddListener(_=>mouseHeld=false);trigger.triggers.Add(up);
        }
        private void AddChoices(string[] choices,bool secondary,float min,float max)
        {
            if(choices==null||choices.Length==0)return;
            for(int i=0;i<choices.Length;i++){int index=i;float w=1f/choices.Length;Button(controls,((secondary?second:first)==i?"● ":"○ ")+choices[i],new Vector2(i*w,min),new Vector2((i+1)*w-.01f,max),()=>{if(secondary)second=index;else first=index;CancelHold();RenderDocument();});}
        }
        public void OpenSupply(CampaignSupply supply)
        {
            Close();journal=true;panel.gameObject.SetActive(true);body.text=$"FIELD SUPPLY\n\nThe squad can claim one package from this cache.\n{supply.AmmoLabel} +{supply.ammo}.\n"+(supply.ammoWeapon!=null?"Carry the matching weapon and make room for the whole pack. Otherwise the cache stays available.\n":"")+"Or take one medkit if you have space.";ClearControls();
            Button(controls,"TAKE " + supply.AmmoLabel,new Vector2(0,.2f),new Vector2(.48f,.8f),()=>{Campaign.RequestSupply(supply.supplyId,false);Close();});
            Button(controls,"TAKE MEDKIT",new Vector2(.52f,.2f),new Vector2(1,.8f),()=>{Campaign.RequestSupply(supply.supplyId,true);Close();});
        }
        private void OpenJournal()
        {
            Close();journal=true;panel.gameObject.SetActive(true);ClearControls();
            string evidence="Primary objective: recover T9-17 evidence.\n";
            if(Campaign.State.Has(CampaignObjectiveId.FactoryShipping))evidence+="AL-04 · An Lac · 02:40 · K6.\n";
            if(Campaign.State.Has(CampaignObjectiveId.FactoryCase))evidence+="Factory case secured; manifest points to B2.\n";
            if(Campaign.State.Has(CampaignObjectiveId.AsylumAccess))evidence+="Group C12 · AL-04 receiving time 02:40.\n";
            if(Campaign.State.Has(CampaignObjectiveId.AsylumPatient))evidence+="P046 transfer route points to B2.\n";
            if(Campaign.State.Has(CampaignObjectiveId.AsylumTransfer))evidence+="P046: death report 03:05, live observation 03:50.\n";
            if(Campaign.State.Has(CampaignObjectiveId.LabArchive))evidence+="E-02 links T9-17 / C12 / P046; identities were removed from the client copy.\n";
            body.text="TEAM JOURNAL\n\n"+evidence+"\n"+ObjectiveText()+CampaignDialogue.Journal(Campaign.State);
            Button(controls,"CLOSE JOURNAL",new Vector2(0,.08f),new Vector2(.48f,.42f),Close);
            if(Campaign.CanResumeSavedCampaign)Button(controls,"RESUME CHECKPOINT",new Vector2(.52f,.08f),new Vector2(1,.42f),()=>{Campaign.RequestRetry(true);Close();});
            if(Campaign.State.phase==CampaignPhase.Preparing)
            {
                Button(controls,"READY",new Vector2(0,.5f),new Vector2(.48f,.95f),()=>{Campaign.RequestReady(true);Close();});
                Button(controls,"CANCEL READY",new Vector2(.52f,.5f),new Vector2(1,.95f),()=>{Campaign.RequestReady(false);Close();});
            }
        }
        private void Close()
        {CancelHold();CancelAid();focused=null;journal=false;panel.gameObject.SetActive(false);ClearControls();}
        private void CancelHold()
        {if(holding&&focused!=null)Campaign?.RequestHold(focused.objectiveId,first,second,false);holding=false;mouseHeld=false;}
        private void CancelAid()
        {
            if(aidHeld) Campaign?.RequestAid(0,false,false);
            aidHeld=false;
        }
        public void ShowResult(CampaignResult result)
        {
            if(result==CampaignResult.AmmoUnavailable){ShowMessage("AMMO NOT TAKEN · Carry the matching weapon and make room for the whole pack.");return;}
            if(result==CampaignResult.Accepted){ShowMessage("Recorded for the squad.");Close();return;}
            if(result==CampaignResult.Interrupted){releaseBeforeHold=true;holding=false;mouseHeld=false;}
            ShowMessage(result switch{CampaignResult.WrongAnswer=>"The evidence does not match. Check the fields again.",CampaignResult.Prerequisite=>"Prerequisites are incomplete. Check the team objective.",CampaignResult.Busy=>"Another operator is using this.",CampaignResult.OutOfRange=>"Move closer and keep the interaction point visible.",CampaignResult.AlreadyDone=>"The squad already completed this step.",_=>"Interaction interrupted; completed steps are preserved."});
        }
        public void ShowMessage(string text){message=text;messageUntil=Time.unscaledTime+4;}

        public void EnqueueDialogue(CampaignDialogueId id)
        {
            CampaignDialogueDefinition definition = Campaign?.Dialogue(id);
            if (definition == null || string.IsNullOrWhiteSpace(definition.english)) return;
            pendingDialogue.Add(definition);
        }

        public void ResetDialogue()
        {
            pendingDialogue.Clear();
            subtitleUntil = 0;
            if (subtitle != null) subtitle.text = string.Empty;
            if (dialogueAudio != null) dialogueAudio.Stop();
        }

        public bool CanAutoOpenFile()
        {
            var owner = Owner;
            if (owner == null || !owner.CanUseCombat || Campaign == null || Campaign.BlocksInput
                || InGameMenuUI.IsMenuOpen || panel.gameObject.activeSelf) return false;
            nextThreatCheck = 0;
            UpdateHints(owner, InputManager.Instance?.ActionsAsset?.FindAction("Fire")?.IsPressed() == true);
            return !nearbyThreat && Time.unscaledTime >= combatUntil;
        }
        private void Update()
        {
            if (Campaign != null && Campaign.IsSpawned) UpdateSubtitles();
            // Pause and settings own the screen above the campaign layer.
            if (canvas != null) canvas.enabled = !InGameMenuUI.IsMenuOpen;
            if (InGameMenuUI.IsMenuOpen) { CancelHold(); CancelAid(); return; }
            if(Campaign==null||!Campaign.IsSpawned)return;
            var input=InputManager.Instance;var actions=input?.ActionsAsset;
            bool open=panel.gameObject.activeSelf;
            if(actions?.FindAction("Journal")?.WasPressedThisFrame()==true)
            {
                if (TeamInventoryUI.IsOpen) TeamInventoryUI.Instance.Close();
                else if(open) Close();
                else if (TeamInventoryUI.Instance != null) TeamInventoryUI.Instance.Toggle();
                else OpenJournal();
            }
            if (TeamInventoryUI.IsOpen)
            {
                InputManager.CampaignInputBlocked = true;
                CancelHold(); CancelAid();
                return;
            }
            InputManager.CampaignInputBlocked=Campaign.BlocksInput||panel.gameObject.activeSelf;
            if(panel.gameObject.activeSelf){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            else if(!InputManager.GameplayInputBlocked){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            status.text=ObjectiveText();feedback.text=Time.unscaledTime<messageUntil?message:"";
            if(panel.gameObject.activeSelf)body.rectTransform.sizeDelta=new Vector2(0,Mathf.Max(documentViewport.rect.height,body.preferredHeight));
            var owner=Owner;if(owner==null)return;
            UpdateNavigation(owner);
            RenderPhaseControls();
            progress.text=$"{input?.GetKeyForAction("Journal")} · INVENTORY";
            if(owner.LifeState==PlayerLifeState.Downed)progress.text=$"DOWNED · WAIT FOR REVIVE · {Mathf.CeilToInt((float)(owner.LifeStateDeadline-Campaign.Now))}s";
            if(owner.IsDead)progress.text="SPECTATING · FIRE / AIM TO CHANGE OPERATOR";
            bool fire=actions?.FindAction("Fire")?.WasPressedThisFrame()==true;
            bool anyHold=mouseHeld||actions?.FindAction("Interact")?.IsPressed()==true;
            if(releaseBeforeHold&&!anyHold){releaseBeforeHold=false;Campaign.RequestAid(0,false,false);}
            if(fire&&!mouseHeld){releaseBeforeHold=true;CancelHold();CancelAid();}
            UpdateHints(owner,fire&&!mouseHeld);
            if(focused!=null)
            {
                if(!owner.CanUseCombat || Campaign.BlocksInput) { Close(); return; }
                bool pressed=!releaseBeforeHold&&(mouseHeld||actions?.FindAction("Interact")?.IsPressed()==true);
                if(fire&&!mouseHeld)pressed=false;
                if(!focused.InReach(owner,Campaign.settings.interactionRange)){CancelHold();return;}
                if(pressed&&!Campaign.State.Has(focused.objectiveId))
                {
                    if(!holding){holding=true;holdStart=Time.unscaledTime;nextHeartbeat=0;}
                    if(Time.unscaledTime>=nextHeartbeat){Campaign.RequestHold(focused.objectiveId,first,second,true);nextHeartbeat=Time.unscaledTime+.2f;}
                    progress.text=$"INTERACTING {Mathf.Clamp01((Time.unscaledTime-holdStart)/Mathf.Max(.1f,focused.holdSeconds)):P0}";
                }
                else CancelHold();
                PresentHint();
                return;
            }
            if(panel.gameObject.activeSelf||Campaign.BlocksInput||!owner.CanUseCombat){CancelAid();return;}
            bool heartbeat=Time.unscaledTime>=nextAidHeartbeat;
            var cam=owner.GetComponentInChildren<Camera>();
            PlayerHealth downed=null;
            if(cam!=null&&Physics.Raycast(cam.transform.position,cam.transform.forward,out var hit,Campaign.settings.interactionRange,~0,QueryTriggerInteraction.Ignore))downed=hit.collider.GetComponentInParent<PlayerHealth>();
            bool revive=downed!=null&&downed!=owner&&downed.LifeState==PlayerLifeState.Downed;
            bool aid=!releaseBeforeHold&&owner.GetComponent<SurvivalInventory>()?.IsUsingItem!=true&&revive&&actions?.FindAction("Interact")?.IsPressed()==true&&!fire;
            if(revive)progress.text=$"HOLD {input?.GetKeyForAction("Interact")} TO REVIVE ({Campaign.settings.reviveSeconds:0}s)";
            if((aid&&heartbeat)||aidHeld!=aid){Campaign.RequestAid(downed!=null?downed.NetworkObjectId:0,false,aid);aidHeld=aid;nextAidHeartbeat=Time.unscaledTime+.2f;}
            PresentHint();
            RenderPhaseControls();
        }
        private void UpdateHints(PlayerHealth owner,bool fired)
        {
            if(owner.DamageRevision!=lastDamage||fired)combatUntil=Time.unscaledTime+10;
            lastDamage=owner.DamageRevision;
            if(Time.unscaledTime>=nextThreatCheck)
            {
                nextThreatCheck=Time.unscaledTime+.5f;nearbyThreat=false;
                foreach(var collider in Physics.OverlapSphere(owner.transform.position,16,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    var enemy=collider.GetComponentInParent<EnemyAI>();
                    if(enemy==null||!enemy.isActiveAndEnabled||Mathf.Abs(enemy.transform.position.y-owner.transform.position.y)>4)continue;
                    Vector3 eye=owner.transform.position+Vector3.up*1.5f;
                    if(!Physics.Linecast(eye,enemy.transform.position+Vector3.up,out var hit,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                        ||hit.collider.GetComponentInParent<EnemyAI>()==enemy){nearbyThreat=true;break;}
                }
                nearbyHint=Campaign.Current.objectives.Where(i=>i!=null&&CampaignRules.CanUse(Campaign.State,i.objectiveId)==CampaignResult.Accepted
                    &&!CampaignRules.IsEncounter(i.objectiveId)&&(i.Point-owner.transform.position).sqrMagnitude<36)
                    .OrderBy(i=>(i.Point-owner.transform.position).sqrMagnitude).FirstOrDefault();
            }
            if(hintCompleted!=Campaign.State.completed)
            {hintCompleted=Campaign.State.completed;hintTime=0;hintOffered=false;}
            if(nearbyHint==null||!owner.CanUseCombat||Campaign.State.phase!=CampaignPhase.Exploring||nearbyThreat||Time.unscaledTime<combatUntil)return;
            if(hintTarget!=nearbyHint.objectiveId){hintTarget=nearbyHint.objectiveId;hintTime=0;hintOffered=false;}
            hintTime+=Time.unscaledDeltaTime;
        }
        private void PresentHint()
        {
            if(nearbyHint==null||nearbyThreat||Time.unscaledTime<combatUntil||hintTime<45)return;
            progress.text+="\n"+(hintTime<90?"Cross-check the fields on the nearby document.":nearbyHint.Hint);
            if(hintTime>=150&&focused==nearbyHint&&!hintOffered)
            {
                hintOffered=true;
                Button(controls,"SHOW HINT",new Vector2(0,0),new Vector2(1,.22f),()=>ShowMessage(Answer(hintTarget)));
            }
        }
        private uint lastDamage;
        private void UpdateSubtitles()
        {
            if(Time.unscaledTime<subtitleUntil)return;
            if (pendingDialogue.Count == 0)
            {
                subtitle.text = "";
                subtitleUntil = Time.unscaledTime + .25f;
                return;
            }
            CampaignDialogueDefinition definition = CampaignDialogue.TakeNext(pendingDialogue);
            subtitle.text = CampaignDialogue.Format(definition);
            if (dialogueAudio != null && definition.audio != null) dialogueAudio.PlayOneShot(definition.audio);
            subtitleUntil = Time.unscaledTime + Mathf.Max(6, subtitle.text.Length * .06f,
                definition.audio != null ? definition.audio.length : 0);
        }
        private bool hintOffered;
        private CampaignPhase renderedPhase=(CampaignPhase)255;
        private CampaignTankStage renderedTankStage;
        private void RenderPhaseControls()
        {
            var phase=Campaign.State.phase;
            if(phase==CampaignPhase.Completed&&Campaign.Now-Campaign.PhaseStarted<Campaign.settings.endingSeconds){if(panel.gameObject.activeSelf)Close();return;}
            if(renderedPhase==phase && renderedTankStage==Campaign.State.tankStage)return;
            renderedPhase=phase;renderedTankStage=Campaign.State.tankStage;
            if(phase is CampaignPhase.Preparing or CampaignPhase.Failed or CampaignPhase.Completed) Close();
            if (phase == CampaignPhase.Preparing && Campaign.State.tankStage is CampaignTankStage.Pending or CampaignTankStage.Active) return;
            if(phase==CampaignPhase.Preparing)
            {journal=true;panel.gameObject.SetActive(true);ClearControls();body.text="PREPARATION\n\nTake supplies, revive teammates and gather at the panel.\nEach living operator confirms ready; the five-second countdown can be cancelled.\nThe checkpoint includes supplies claimed before countdown.";Button(controls,"READY",new Vector2(0,.2f),new Vector2(.48f,.8f),()=>{Campaign.RequestReady(true);Close();});Button(controls,"NOT READY",new Vector2(.52f,.2f),new Vector2(1,.8f),()=>{Campaign.RequestReady(false);Close();});}
            if(phase==CampaignPhase.Failed)
            {journal=true;panel.gameObject.SetActive(true);ClearControls();body.text="MISSION FAILED\n\nRestore the nearest checkpoint. HP, ammo, medicine and objectives return to the saved state.";if(Campaign.IsServer)Button(controls,"RETRY CHECKPOINT",new Vector2(.2f,.2f),new Vector2(.8f,.8f),()=>{Campaign.RequestRetry();Close();});}
            if(phase==CampaignPhase.Completed)
            {journal=true;panel.gameObject.SetActive(true);ClearControls();body.text="COLD LEDGER — COMPLETE\n\nThe freight route opened and the evidence copy was transmitted outside.\n\nT9-17 / C12 / P046 can no longer be erased from the record.\nThe zone remains quarantined; the investigation starts now.\n\nThe squad extracted with both sealed cases.";}
        }
        public string ObjectiveText()
        {
            var s=Campaign.State;
            string title=s.chapter==CampaignChapter.Factory?"FACTORY":s.chapter==CampaignChapter.Asylum?"AN LAC":"B2 · LABORATORY";
            if(s.phase==CampaignPhase.Insertion)return title+"\nReach the facility. Waiting for insertion.";
            if(s.phase==CampaignPhase.Preparing)return title+(s.tankStage==CampaignTankStage.Active
                ? "\nEVADE TANK · Gather every living operator at the lift panel; countdown starts automatically."
                : s.tankStage==CampaignTankStage.Pending ? "\nTANK THREAT · Keep the lift approach clear."
                : $"\nPrepare at the panel · READY: {Campaign.ReadyCount}");
            if(s.phase==CampaignPhase.Encounter)return title+$"\n{(s.chapter==CampaignChapter.Factory?"OPEN SERVICE ROUTE":s.chapter==CampaignChapter.Asylum?"CALL B2 LIFT":"TRANSMIT EVIDENCE / CALL LIFT")} · {Mathf.CeilToInt(Campaign.Remaining)}s";
            if(s.phase==CampaignPhase.AwaitingParty && s.tankStage==CampaignTankStage.Pending)return title+"\nTANK THREAT · Awaiting a clear approach; regroup in open space.";
            if(s.phase==CampaignPhase.AwaitingParty)return title+(s.chapter switch
            {
                CampaignChapter.Asylum => "\nB2 lift is open beside the machine room. Enter the cabin and hold 5 seconds.",
                CampaignChapter.Laboratory => "\nFreight lift at F is open. Enter the cabin and hold 5 seconds.",
                _ => "\nThe gate is open. Enter the staging zone and hold 5 seconds."
            });
            if(s.phase==CampaignPhase.Transitioning)return s.chapter==CampaignChapter.Asylum?"FREIGHT LIFT · DESCENDING TO B2\nWait for receiving door A; hold formation.":"TRANSITIONING\nThe door is sealed. Hold formation.";
            if(s.phase==CampaignPhase.Failed)return "FAILED · waiting for host checkpoint restore";
            if(s.phase==CampaignPhase.Completed)return "CAMPAIGN COMPLETE";
            if(s.chapter==CampaignChapter.Factory){if(!CampaignRules.UtilitiesReady(s)||!CampaignRules.LogisticsReady(s))return title+"\n"+(!CampaignRules.UtilitiesReady(s)?"[ ] Cold-store power\n":"[DONE] Cold-store power\n")+(!CampaignRules.LogisticsReady(s)?"[ ] AL-04 manifest":"[DONE] AL-04 manifest");if(!s.factoryTankDefeated&&!s.Has(CampaignObjectiveId.FactoryCase))return title+"\nTANK THREAT · Regroup in open space and eliminate the Tank before securing the case.";return title+(s.Has(CampaignObjectiveId.FactoryCase)?"\nOpen the service route to An Lac.":"\nRecover the T9-17 case in cold storage.");}
            if(s.chapter==CampaignChapter.Asylum){if(!s.Has(CampaignObjectiveId.AsylumAccess))return title+"\nRead the security desk log.";if(!s.Has(CampaignObjectiveId.AsylumPower))return title+"\nRecover the fuse and power the lower service panel.";if(!s.Has(CampaignObjectiveId.AsylumPatient))return title+"\nFind C12 / 02:40 in the upper records.";if(!s.Has(CampaignObjectiveId.AsylumTransfer))return title+"\nCross-check P046 in the mortuary.";return title+"\nReturn to the freight lift and scan the B2 card.";}
            if(!s.Has(CampaignObjectiveId.LabPower))return title+"\nRead the B/C records and allocate power at D.";
            if(!CampaignRules.LabSecured(s))return title+"\nCross-check T9-17 / C12 / P046 at E; secure the archive and case.";
            return title+"\nReach F, transmit evidence and call the freight lift.";
        }
        private static string Answer(CampaignObjectiveId id)=>id switch{CampaignObjectiveId.FactoryShipping=>"AL-04 + K6.",CampaignObjectiveId.AsylumPatient=>"Select P046: group C12, receiving time 02:40.",CampaignObjectiveId.AsylumTransfer=>"Live observation was logged after the death report.",CampaignObjectiveId.LabPower=>"Turn TEST RIG off. Keep DATA, FREIGHT LIFT and SAFETY online.",CampaignObjectiveId.LabArchive=>"Select E-02.",_=>"Factory: isolate → backup cold store → generator. Asylum: secure the card and fuse before powering the lift."};
    }
}
