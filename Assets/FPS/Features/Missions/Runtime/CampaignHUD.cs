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
        private ulong spokenObjectives;
        private CampaignChapter spokenChapter;
        private CampaignPhase spokenPhase = (CampaignPhase)255;
        private bool spokenTransmission;
        private float subtitleUntil;
        private readonly System.Collections.Generic.Queue<string> subtitleQueue = new();
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
            var c = new GameObject("CampaignCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            c.transform.SetParent(transform, false); canvas = c.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = TacticalUiTheme.CampaignOrder;
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
            Button(panel, "Đóng / J", new Vector2(.79f,.94f), new Vector2(.98f,.995f), Close);
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
            if (Campaign == null || Campaign.BlocksInput) return;
            CancelAid();
            focused=item;journal=false;first=second=0;
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
            Button(controls, "Đóng hướng dẫn", new Vector2(.2f,.2f), new Vector2(.8f,.8f), Close);
        }
        private void RenderDocument()
        {
            if(focused==null)return;
            body.text=focused.displayName.ToUpperInvariant()+"\n\n"+focused.document;
            if (focused.objectiveId is CampaignObjectiveId.FactoryRoute or CampaignObjectiveId.AsylumLift)
                body.text += "\n\n" + CampaignDialogue.AccessInstructions(Campaign.State, focused.objectiveId);
            ClearControls();
            if(focused.objectiveId==CampaignObjectiveId.LabPower)
            {
                string[] loads={"AN TOÀN · 2","THIẾT BỊ THỬ · 4","DỮ LIỆU · 2","THANG HÀNG · 2"};
                for(int i=0;i<4;i++){int bit=i;Button(controls,((first&(1<<i))!=0?"[BẬT] ":"[TẮT] ")+loads[i],new Vector2(i*.25f,.46f),new Vector2((i+1)*.25f-.01f,.98f),()=>{int mask=first^(1<<bit);if(CampaignRules.CanSelectPower(mask)){first=mask;Campaign.RequestPowerSelection(mask);RenderDocument();}else ShowMessage("Giữ AN TOÀN; tổng tải không được vượt 6.");});}
            }
            else
            {
                AddChoices(focused.choices,false,.48f,.99f);
                AddChoices(focused.secondaryChoices,true,.03f,.46f);
            }
            var confirm=Button(panel,Campaign.State.Has(focused.objectiveId)?"Đội đã ghi nhận":"Giữ phím tương tác để xác nhận",new Vector2(.04f,.01f),new Vector2(.76f,.065f),()=>{});
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
            Close();journal=true;panel.gameObject.SetActive(true);body.text="TIẾP TẾ DỰ PHÒNG\n\nĐội chỉ nhận một gói từ hộp này.\nĐạn bổ sung cho vũ khí đang dùng; bộ sơ cứu được mang theo (tối đa 2).";ClearControls();
            Button(controls,"Nhận đạn",new Vector2(0,.2f),new Vector2(.48f,.8f),()=>{Campaign.RequestSupply(supply.supplyId,false);Close();});
            Button(controls,"Nhận sơ cứu",new Vector2(.52f,.2f),new Vector2(1,.8f),()=>{Campaign.RequestSupply(supply.supplyId,true);Close();});
        }
        private void OpenJournal()
        {
            Close();journal=true;panel.gameObject.SetActive(true);ClearControls();
            string evidence="Mục tiêu: lô T9-17.\n";
            if(Campaign.State.Has(CampaignObjectiveId.FactoryShipping))evidence+="AL-04 · An Lạc · 02:40 · K6.\n";
            if(Campaign.State.Has(CampaignObjectiveId.FactoryCase))evidence+="Đội giữ kiện đối chứng; phiếu giao ghi B2.\n";
            if(Campaign.State.Has(CampaignObjectiveId.AsylumAccess))evidence+="Nhóm C12 · ca tiếp nhận AL-04 lúc 02:40.\n";
            if(Campaign.State.Has(CampaignObjectiveId.AsylumPatient))evidence+="P046 được chuyển xuống B2.\n";
            if(Campaign.State.Has(CampaignObjectiveId.AsylumTransfer))evidence+="P046: báo tử 03:05, chuyển theo dõi sống 03:50. Hai giấy mâu thuẫn.\n";
            if(Campaign.State.Has(CampaignObjectiveId.LabArchive))evidence+="E-02 liên kết T9-17 / C12 / P046. Báo cáo gửi bên thuê đã bỏ danh tính và yêu cầu dừng.\n";
            body.text="HỒ SƠ ĐỘI\n\n"+evidence+"\n"+ObjectiveText()+CampaignDialogue.Journal(Campaign.State);
            Button(controls,"Đóng hồ sơ",new Vector2(0,.08f),new Vector2(.48f,.42f),Close);
            if(Campaign.CanResumeSavedCampaign)Button(controls,"Tiếp tục checkpoint đã lưu",new Vector2(.52f,.08f),new Vector2(1,.42f),()=>{Campaign.RequestRetry(true);Close();});
            if(Campaign.State.phase==CampaignPhase.Preparing)
            {
                Button(controls,"Sẵn sàng",new Vector2(0,.5f),new Vector2(.48f,.95f),()=>{Campaign.RequestReady(true);Close();});
                Button(controls,"Hủy sẵn sàng",new Vector2(.52f,.5f),new Vector2(1,.95f),()=>{Campaign.RequestReady(false);Close();});
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
            if(result==CampaignResult.Accepted){ShowMessage("Đã ghi nhận cho cả đội.");Close();return;}
            if(result==CampaignResult.Interrupted){releaseBeforeHold=true;holding=false;mouseHeld=false;}
            ShowMessage(result switch{CampaignResult.WrongAnswer=>"Dữ kiện chưa khớp. Kiểm tra lại các cột trên phiếu.",CampaignResult.Prerequisite=>"Chưa đủ điều kiện. Xem mục tiêu trong hồ sơ đội.",CampaignResult.Busy=>"Đồng đội đang thao tác.",CampaignResult.OutOfRange=>"Đứng gần thiết bị và nhìn thấy điểm thao tác.",CampaignResult.AlreadyDone=>"Đội đã hoàn tất bước này.",_=>"Thao tác bị ngắt; các bước đã xong vẫn được giữ."});
        }
        private void ShowMessage(string text){message=text;messageUntil=Time.unscaledTime+4;}
        private void Update()
        {
            // Pause and settings own the screen above the campaign layer.
            if (canvas != null) canvas.enabled = !InGameMenuUI.IsMenuOpen;
            if (InGameMenuUI.IsMenuOpen) { CancelHold(); CancelAid(); return; }
            if(Campaign==null||!Campaign.IsSpawned)return;
            var input=InputManager.Instance;var actions=input?.ActionsAsset;
            bool open=panel.gameObject.activeSelf;
            if(actions?.FindAction("Journal")?.WasPressedThisFrame()==true){if(open)Close();else OpenJournal();}
            InputManager.CampaignInputBlocked=Campaign.BlocksInput||panel.gameObject.activeSelf;
            if(panel.gameObject.activeSelf){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            else if(!InputManager.GameplayInputBlocked){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            status.text=ObjectiveText();feedback.text=Time.unscaledTime<messageUntil?message:"";
            UpdateSubtitles();
            if(panel.gameObject.activeSelf)body.rectTransform.sizeDelta=new Vector2(0,Mathf.Max(documentViewport.rect.height,body.preferredHeight));
            var owner=Owner;if(owner==null)return;
            UpdateNavigation(owner);
            RenderPhaseControls();
            progress.text=$"{input?.GetKeyForAction("Journal")} · Hồ sơ";
            if(owner.LifeState==PlayerLifeState.Downed)progress.text=$"ĐANG NGÃ · chờ đồng đội cứu · {Mathf.CeilToInt((float)(owner.LifeStateDeadline-Campaign.Now))} giây";
            if(owner.IsDead)progress.text="Đang quan sát · trở lại ở khu kế tiếp hoặc checkpoint.";
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
                    progress.text=$"Đang thao tác {Mathf.Clamp01((Time.unscaledTime-holdStart)/Mathf.Max(.1f,focused.holdSeconds)):P0}";
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
            if(revive)progress.text=$"Giữ {input?.GetKeyForAction("Interact")} cứu đồng đội ({Campaign.settings.reviveSeconds:0} giây)";
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
            progress.text+="\n"+(hintTime<90?"Đối chiếu các trường trên tài liệu gần thiết bị.":nearbyHint.hint);
            if(hintTime>=150&&focused==nearbyHint&&!hintOffered)
            {
                hintOffered=true;
                Button(controls,"Xem hướng dẫn",new Vector2(0,0),new Vector2(1,.22f),()=>ShowMessage(Answer(hintTarget)));
            }
        }
        private uint lastDamage;
        private void UpdateSubtitles()
        {
            var state=Campaign.State;
            if(state.chapter!=spokenChapter){spokenChapter=state.chapter;subtitleQueue.Enqueue(CampaignDialogue.ChapterLine(state.chapter));}
            if(state.phase!=spokenPhase)
            {
                spokenPhase=state.phase;
                string line=CampaignDialogue.PhaseLine(state.chapter,state.phase);if(line.Length>0)subtitleQueue.Enqueue(line);
            }
            foreach(CampaignObjectiveId id in Enum.GetValues(typeof(CampaignObjectiveId)))
                if(state.Has(id)&&(spokenObjectives&CampaignState.Bit(id))==0)
                {string line=CampaignDialogue.ObjectiveLine(id);if(line.Length>0)subtitleQueue.Enqueue(line);}
            spokenObjectives=state.completed;
            if(state.evidenceTransmitted&&!spokenTransmission)subtitleQueue.Enqueue("Điều phối: Đã nhận bản sao đầy đủ ở tuyến ngoài. Giữ hai kiện và tập kết tại thang hàng.");
            spokenTransmission=state.evidenceTransmitted;
            if(Time.unscaledTime<subtitleUntil)return;
            subtitle.text=subtitleQueue.Count>0?subtitleQueue.Dequeue():"";
            subtitleUntil=Time.unscaledTime+Mathf.Max(6,subtitle.text.Length*.06f);
        }
        private bool hintOffered;
        private CampaignPhase renderedPhase=(CampaignPhase)255;
        private void RenderPhaseControls()
        {
            var phase=Campaign.State.phase;
            if(phase==CampaignPhase.Completed&&Campaign.Now-Campaign.PhaseStarted<Campaign.settings.endingSeconds){if(panel.gameObject.activeSelf)Close();return;}
            if(renderedPhase==phase)return;renderedPhase=phase;
            if(phase is CampaignPhase.Preparing or CampaignPhase.Failed or CampaignPhase.Completed) Close();
            if(phase==CampaignPhase.Preparing)
            {journal=true;panel.gameObject.SetActive(true);ClearControls();body.text="CHUẨN BỊ\n\nNhận tiếp tế, cứu đồng đội và tập kết cạnh panel.\nMỗi người xác nhận sẵn sàng; sau đó có 5 giây để hủy.\nCheckpoint lưu cả đồ vừa nhận.";Button(controls,"Sẵn sàng",new Vector2(0,.2f),new Vector2(.48f,.8f),()=>{Campaign.RequestReady(true);Close();});Button(controls,"Chưa sẵn sàng",new Vector2(.52f,.2f),new Vector2(1,.8f),()=>{Campaign.RequestReady(false);Close();});}
            if(phase==CampaignPhase.Failed)
            {journal=true;panel.gameObject.SetActive(true);ClearControls();body.text="ĐỘI KHÔNG CÒN KHẢ NĂNG TIẾP TỤC\n\nQuay lại checkpoint gần nhất. HP, đạn, thuốc và nhiệm vụ được khôi phục đúng lúc lưu.";if(Campaign.IsServer)Button(controls,"Thử lại checkpoint",new Vector2(.2f,.2f),new Vector2(.8f,.8f),()=>{Campaign.RequestRetry();Close();});}
            if(phase==CampaignPhase.Completed)
            {journal=true;panel.gameObject.SetActive(true);ClearControls();body.text="HỒ SƠ LẠNH — HOÀN TẤT\n\nThang hàng mở ra bến dịch vụ. Bản sao hồ sơ đã được gửi ra ngoài.\n\nT9-17 / C12 / P046 không còn bị xóa khỏi báo cáo.\nKhu vực vẫn phong tỏa; cuộc điều tra bắt đầu.\n\nĐội đã mang cả hai kiện niêm phong và bằng chứng ra khỏi cơ sở.";}
        }
        public string ObjectiveText()
        {
            var s=Campaign.State;
            string title=s.chapter==CampaignChapter.Factory?"NHÀ MÁY":s.chapter==CampaignChapter.Asylum?"AN LẠC":"B2 · KHU THÍ NGHIỆM";
            if(s.phase==CampaignPhase.Insertion)return title+"\nTiếp cận cơ sở. Chờ kết thúc đổ bộ.";
            if(s.phase==CampaignPhase.Preparing)return title+$"\nChuẩn bị tại panel · Sẵn sàng: {Campaign.ReadyCount}";
            if(s.phase==CampaignPhase.Encounter)return title+$"\n{(s.chapter==CampaignChapter.Factory?"Mở tuyến dịch vụ":s.chapter==CampaignChapter.Asylum?"Gọi thang B2":"Truyền hồ sơ / gọi thang")} · {Mathf.CeilToInt(Campaign.Remaining)} giây";
            if(s.phase==CampaignPhase.AwaitingParty)return title+(s.chapter switch
            {
                CampaignChapter.Asylum => "\nThang B2 cạnh phòng máy đã mở. Cả đội vào cabin và chờ 5 giây.",
                CampaignChapter.Laboratory => "\nThang xuất hàng tại F đã mở. Cả đội vào cabin và chờ 5 giây.",
                _ => "\nCổng đã mở. Cả đội vào khu đệm giữa hai cổng và chờ 5 giây."
            });
            if(s.phase==CampaignPhase.Transitioning)return s.chapter==CampaignChapter.Asylum?"THANG HÀNG · ĐANG XUỐNG B2\nChờ cửa khu tiếp nhận A mở; giữ nguyên đội hình.":"ĐANG CHUYỂN KHU\nCửa đã khép. Giữ nguyên đội hình.";
            if(s.phase==CampaignPhase.Failed)return "THẤT BẠI · chờ host khôi phục checkpoint";
            if(s.phase==CampaignPhase.Completed)return "CHIẾN DỊCH HOÀN TẤT";
            if(s.chapter==CampaignChapter.Factory){if(!CampaignRules.UtilitiesReady(s)||!CampaignRules.LogisticsReady(s))return title+"\n"+(!CampaignRules.UtilitiesReady(s)?"□ Nguồn kho lạnh\n":"✓ Nguồn kho lạnh\n")+(!CampaignRules.LogisticsReady(s)?"□ Vận đơn AL-04":"✓ Vận đơn AL-04");return title+(s.Has(CampaignObjectiveId.FactoryCase)?"\nMở tuyến dịch vụ tới An Lạc.":"\nThu hồi kiện đối chứng T9-17 trong kho lạnh.");}
            if(s.chapter==CampaignChapter.Asylum){if(!s.Has(CampaignObjectiveId.AsylumAccess))return title+"\nKiểm tra sổ ca tại phòng bảo vệ.";if(!s.Has(CampaignObjectiveId.AsylumPower))return title+"\nLấy hộp cầu chì ở kho, cấp nguồn tại phòng máy dưới hầm.";if(!s.Has(CampaignObjectiveId.AsylumPatient))return title+"\nTra nhóm C12 / 02:40 tại hồ sơ tầng trên.";if(!s.Has(CampaignObjectiveId.AsylumTransfer))return title+"\nĐối chiếu giấy chuyển P046 tại nhà xác dưới hầm.";return title+"\nQuay lại thang hàng cạnh phòng máy dưới hầm; quét thẻ tại bảng bên phải cửa B2.";}
            if(!s.Has(CampaignObjectiveId.LabPower))return title+"\nTra hồ sơ tại B/C, phân bổ nguồn tại D.";
            if(!CampaignRules.LabSecured(s))return title+"\nĐối chiếu T9-17 / C12 / P046 tại E; nhận hồ sơ và kiện.";
            return title+"\nTới F, truyền bằng chứng và gọi thang xuất hàng.";
        }
        private static string Answer(CampaignObjectiveId id)=>id switch{CampaignObjectiveId.FactoryShipping=>"AL-04 + K6.",CampaignObjectiveId.AsylumPatient=>"Chọn P046: cùng C12 và 02:40.",CampaignObjectiveId.AsylumTransfer=>"Chuyển theo dõi sống sau thời điểm báo tử.",CampaignObjectiveId.LabPower=>"Tắt THIẾT BỊ THỬ, bật DỮ LIỆU và THANG HÀNG; giữ AN TOÀN.",CampaignObjectiveId.LabArchive=>"Chọn E-02.",_=>"Nhà máy: tách dây chuyền → dự phòng kho lạnh → khởi động. Asylum: nhận thẻ và hộp cầu chì trước khi cấp nguồn."};
    }
}
