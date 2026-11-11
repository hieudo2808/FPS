# Bot 5 → Bot 6 — Hợp đồng Factory hiện có trong source

## Delta Inventory — 10/11/2026

User chốt ba tab và yêu cầu triển khai theo thiết kế. Bot 6 đang bỏ Journal, giữ record trong Files, hướng dẫn và tiến độ trong Missions, giữ Resume và input stack. Không thêm Retry Tank. Xem `Reports/UIUX/ThreeTabInventory-2026-11-10/bao-cao.md` cho kết quả mới; các đoạn lịch sử bên dưới không ghi đè phạm vi ba tab này. Shipping và test mạng hai tiến trình chưa được nghiệm thu trong delta này.

## Điều chỉnh phạm vi mới nhất — 06/11/2026

Người dùng bác bỏ Retry Tank trong UI: lỗi spawn thuộc trách nhiệm game. **Hủy phần giao nút Retry, card Journal và mọi gate duyệt ảnh Retry bên dưới.** Các mô tả API và đề xuất Retry còn lại được giữ làm lịch sử kỹ thuật, không phải việc Bot 6 phải triển khai. Chưa có UI runtime mới được code trong lượt Bot 6 này.

Đã kiểm source: Missions đang liệt kê objective của chapter ngang cấp; logic có prerequisite và hai nhánh Factory nhưng chưa có schema task tree tổng quát. Journal còn lưu bằng chứng và lời thoại; phần Next trùng chỉ dẫn Missions. Hướng hợp nhất vào Missions và khả năng bỏ tab Journal cần chốt flow cùng nơi giữ nội dung riêng trước khi thiết kế và code. [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:236] [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:415] [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:627] [CODE Assets/FPS/Features/Missions/Runtime/CampaignDialogue.cs:70]

Rủi ro game tách riêng: Factory SpawnFailed vẫn chặn Case do điều kiện factoryTankDefeated. Không tự sửa spawn, bỏ encounter, giả kill hoặc mở khóa. Shipping và review UI hiện hữu tiếp tục theo phạm vi đã duyệt. Kế hoạch hiện hành là `Reports/06-bot6-ui-remaining-plan.md`; những đoạn chờ duyệt Retry bên dưới đã hết hiệu lực. [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:418] [INFERENCE]

Cập nhật05/11/2026: đã được dùng Editor, catalog49fields đã áp/readback; CampaignRulesTests54/54, HUD14/16. Bot5 chưa làm layout/control mới; hai lỗi test visual ghi Đề xuất bên dưới. Chưa nghiệm thu runtime/visual. [CODE Reports/FactoryImplementationEvidence/resume-campaign-final-result.json:1] [CODE Reports/FactoryImplementationEvidence/resume-hud-final-result.json:1] [SUY LUẬN]

## Lịch sử tiếp nhận Bot 6 trước khi hủy Retry UI — 05/11/2026

Cập nhật triển khai 06/11/2026: baseline mới `aeb5557d` đạt 1/1, 27 ảnh; composite input, scroll, native hold `4322a389` đạt 1/1. Đã tái hiện hai lỗi regression cũ, chỉ sửa ba assertion theme; toàn bộ HudRegressionTests `0c7d4cd5` đạt 16/16, gồm assertion layout sau điểm fail. Shipping trước sửa `f958a2ae` đạt 1/1 với ảnh 1080p và 4K; Escape và focus `6a7f3441` đạt 1/1. [PLAYTEST Reports/UIUX/Bot6Implementation-2026-11-05/Baseline/nine-screens-result.json] [PLAYTEST Reports/UIUX/Bot6Implementation-2026-11-05/Baseline/hud-regression-updated-theme-result.json]

Trạng thái ghi nhận trước khi đổi phạm vi: chờ duyệt bố cục card Retry Tank, chưa sửa UI runtime. Flow Retry giữ Journal mở lúc đó đã chốt nhưng nay bị hủy. Source cấu trúc khi đó là Journal runtime, không phải PNG Journal độc lập. Handoff Terra và ảnh crop delta ở `Reports/UIUX/Bot6Implementation-2026-11-05/` chỉ còn giá trị lịch sử. Phần mô tả tên nhóm Shipping có dấu gạch chéo bên dưới là lịch sử source, kế hoạch mới thống nhất `Shipment` và `Destination`. Các lỗi theme được nêu ở phần lịch sử dưới đây đã được xử lý trong test. Hai tiến trình và acceptance Shipping sau code vẫn chưa chạy. [INFERENCE]

Người dùng đã xác nhận chín mockup được duyệt và triển khai; lượt rà mới giữ nguyên baseline đó. Kết quả mới: bài chín màn job7d0b5b37 đạt1/1,27PNG1080p/4K/4:3; không bao phủ Shipping comparison hoặc Retry Tank. Xem [báo cáo rà hiện tại](UIUX/Bot6PlanReview-2026-11-05/bao-cao.md). [CODE Reports/UIUX/Bot6PlanReview-2026-11-05/nine-screens-result.json:1]

Kế hoạch tiếp theo: [06-bot6-ui-remaining-plan.md](06-bot6-ui-remaining-plan.md). Người dùng yêu cầu session GPT6.1Terra làm mockup bổ sung Shipping/Retry Tank trước để duyệt. Trạng thái: **đang thiết kế/chờ duyệt, chưa code UI**. Các kết quả/giới hạn Bot5 bên dưới được giữ làm lịch sử kỹ thuật; không giao lại việc dựng chín màn. [INFERENCE]

## Dữ liệu và request để dùng lại

| Phần | API/field thật và quy tắc dùng |
|---|---|
| Nguồn snapshot | `CampaignMissionController.State` có revision/chapter/phase/completed/discoveredFiles; sự kiện Changed để đọc lại. Client chỉ trình bày/request. Không dùng counter HUD để hoàn tất bước. [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:47] |
| Khóa | `CampaignRules.Availability(state,id)` trả Result, IsLocked, Reason, RequiredObjectiveIds, RequiredFileId, RequiresTankDefeat; `CanUse` vẫn chốt ở server. Hiện icon Locked + `Cần: <Reason>`, không F. AlreadyDone là đã ghi, không khóa. [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:432] |
| Hai nhánh | `FactoryProgress(state,Power)` /3; Logistics /2; `FactoryNext`, `CanSwitchFactoryBranch`; active từ `CampaignHUD.TrackedObjectiveId`. `CycleTrackedBranch()` dùng binding Track, mặc định T. Manifest hoàn tất theo HasFile, không chờ Shipping. [CODE Assets/FPS/Features/Missions/Runtime/CampaignObjectiveSelection.cs:9] [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:257] |
| Tên/chỉ dẫn | Catalog objective.title/instructions là việc/nguồn/kết quả; file tên Delivery Manifest, nhánh Power/Logistics, bước Verify Shipping Release; địa điểm Northbridge / Northbridge Care Institute. ID Asylum giữ nguyên. Catalog asset đã áp49fields và kiểm0unexpected/mismatch ngày05/11. [CODE Reports/FactoryImplementationEvidence/factory-catalog-patch.json:2] [SUY LUẬN] |
| Shipping facts | Guard `CanOpenShipping(State)`; chỉ lấy `FindFile(FactoryManifest).body` cho comparison khi HasFile. `first/second` khởi tạo -1, giữ choices/secondaryChoices và thứ tự hiện tại. Nhóm: `SHIPMENT / SEAL`, `DESTINATION / RECEIVING TIME`. Nguồn và các lựa chọn phải đọc đồng thời; không thêm text chỉ đáp án đúng. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:446] [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:480] |
| Shipping lỗi | `CampaignObjectiveFeedback.MissingGroups` / MismatchedGroups do server chấm request: bit ShipmentSeal=1, DestinationReceivingTime=2. `Select: <nhóm>` hoặc `Mismatch: <nhóm>`, marker `CHECK THIS GROUP`; giữ 4s hoặc xóa khi chọn/đóng/files/completed/phase đổi. Không suy lỗi bằng selectedIndex local khi nhận RPC. Late error sau hoàn tất bị bỏ. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:707] [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:70] |
| Completion | `ShowObjectiveResult(receipt)` chỉ được gọi sau buffer xác nhận applied revision và completion. Chữ `Recorded: <title>. / Next: <action>` 4s ở feedback có sẵn; cue completionClip .25; pulse marker 1.1s. Không phát cue khi đổi nhánh T hoặc restore lịch sử. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:707] |
| Marker | `CampaignInteractable.TargetPosition` là interactionBody.bounds.center, fallback interactionPoint. NavMesh complete dùng đường đi, thiếu route ghi `m TO TARGET` theo thẳng. Tank chỉ lấy `TryGetTankTarget(out position)` khi `TankIsNext(State)` và stage Active; Pending/SpawnFailed không có marker tới điểm spawn ứng viên. [CODE Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:61] [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.Tank.cs:25] |
| Hint | `CampaignHintProgress.Seconds` dừng khi combat, reset target/chapter/files/completed; `CampaignRules.HintText(state,definition,seconds)` cho ba mức 45/90/135s từ hint/hint2/hint3. Khóa ghép Requires + prerequisite; không có gate 6m/Accepted. Baseline chỉ dẫn có ngay, không đợi hint. [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:88] [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:844] |
| Tank chờ | State.tankStage, tankWaitReason, tankSpawnRemaining, tankFallback, factoryTankDefeated. `CampaignRules.TankStatus(state)` trả lý do thật: no actor/reading/protection/special busy/no service/no safe position/rejected. Thử mỗi ≥1s; nửa budget còn lại dùng anchors đảo thứ tự nhưng giữ mọi guard. Default setting 20s, chưa xác minh giá trị asset hiện tại. [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.Tank.cs:44] [CODE Assets/FPS/Features/Missions/Runtime/CampaignSettings.cs:19] |
| Retry Tank | `CampaignMissionController.CanRetryTank`; nút host gọi `RequestRetryTank()`. RPC kiểm server host + Factory SpawnFailed. Client chỉ đọc status; retry giữ completed/files/items/lastFactoryBranch. Không dùng RequestRetry/RETRY CHECKPOINT; không gán kill hoặc mở Case khi lỗi spawn. [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.Tank.cs:13] |
| Party gate | `State.partyGate`: reason, eligibleCount, insideCount, readyCount, timeoutRemaining, countdownRemaining, overrideActive, canCountdown. `PartyGateText(gate, preparation)` là adapter text. Eligible là living kể cả downed; inside/qualified cần điều khiển được. Timeout không tự cho skip: vẫn cần qualifier trong vùng, Preparation cần READY trừ solo/evade. Mất qualifier hủy countdown; Tank vẫn chặn. Client không chuyển phase bằng timer local. [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:284] [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:400] |

## Phần đồ họa còn thiếu cần Bot 6 xử lý

1. Shipping: bố trí dữ kiện nguồn cạnh hai nhóm lựa chọn trong panel hiện có; hiện body nguồn nằm trên choices. Giữ hai nhóm có tên, không chọn sẵn, nhìn ra riêng nhóm sai và đọc được dữ kiện khi thử lại. Chỉ bổ sung vùng này theo mockup được duyệt, không mở lại chín màn. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:480] [SUY LUẬN]
2. Tank: control `Retry Tank` cho host khi CanRetryTank=true; client thấy host cần thử lại. Hiện source đã có API và chữ trạng thái, **chưa có nút**, nên chưa đủ đường phục hồi qua UI. Giữ Pending/Active/SpawnFailed phân biệt bằng nội dung, không marker Tank tưởng tượng. [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.Tank.cs:13] [SUY LUẬN]
3. Kiểm chữ Pending/PartyGate khá dài có vừa vùng tracker/reduced HUD không; kiểm glyph NORTHBRIDGE, feedback4s và nhóm lỗi bằng theme/font/sprite hiện tại. Không lấy compile pass làm bằng chứng đọc được ở 1080p/4K. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:172] [SUY LUẬN]

## Dữ liệu đã áp và kiểm tiếp

- Bản hợp nhất49fields đã áp qua serialized API; so trước toàn bộ49khớp, đọc lại857properties,0unexpected/0mismatch. Giữ icons/audio/link/IDs/answer indices, không full import. [CODE Reports/FactoryImplementationEvidence/resume-catalog-verification.json:1]
- CampaignRulesTests eeda09c8:54/54,36s; HUD04e897f9:14/16,50s. Manifest fixture cập nhật EVIDENCE và FILE ADDED TO INVENTORY; hint/tracking cases không fail. MainMenu cuối sạch, ngoài PlayMode/compile, debug_get_errors0. [CODE Reports/FactoryImplementationEvidence/resume-campaign-final-result.json:1] [CODE Reports/FactoryImplementationEvidence/resume-hud-final-result.json:1] [SCENE Assets/FPS/Scenes/MainMenu.unity / isDirty=false]
- B8 đã sửa capture checkpoint Downed→Dead/deadline0 chỉ trong snapshot; test mới save/load temp và live state không đổi, không thêm bypass gate. [CODE Assets/FPS/Features/Missions/Runtime/CampaignCheckpoint.cs:42] [CODE Assets/Tests/Editor/CampaignRulesTests.cs:365]
- Body/NavMesh B2:7marker→body0m; vài route cửa authored PathPartial. Chưa suy route runtime sau mở cửa. [CODE Reports/FactoryImplementationEvidence/B2-scene-measurements.md:5] [SUY LUẬN]

## Đề xuất

- Bot6 hoàn thiện Shipping facts cạnh choices và Retry Tank host control theo mục phần đồ họa; giữ chín màn/theme, không gọi Retry Checkpoint. [SUY LUẬN]
- Bot6 xử lý HudRegressionTests.DebriefShowsSummaryValuesAndResultState: expected TacticalUiTheme.Danger cũ, runtime SelectedUiTheme; và DeviceLayoutRestoresDocumentLayoutWhenClosed: alphaexpected.9/actual.975, có thể còn assertiongeometry cũ sau điểm fail. Không đổi giao diện đã duyệt để hợp test cũ; đối chiếu theme/mockup rồi cập nhật đúng test. Bot5 chưa sửa hai case ngoài Factory. [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:163] [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:210] [CODE Reports/FactoryImplementationEvidence/resume-hud-final-result.json:1]

## Tự kiểm mâu thuẫn

- Source/catalog đã kiểm Editor54/54; UI/runtime chưa nghiệm thu. Retry API không có nghĩa người chơi đã có nút. Northbridge đã đồng bộ catalog/source/canon; chưa rà hết scene/prefab override. [SUY LUẬN]
- Nguồn/choices/Journal đã ghi vẫn có mã dữ kiện để đối chiếu; tracker/hint không được chỉ đáp án. Case đã biết theo briefing khác đáp án Shipping. Lỗi cũ không được ghi đè completion mới. [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:70] [SUY LUẬN]

## Chưa kiểm được

- Host/client thật, fault injection spawn/retry trên NavMesh, input T/READY, âm/pulse và ảnh1080p/4K/người mới. Không tự PlayMode/campaign; test rules không chứng minh đường điều khiển runtime. [SUY LUẬN]
- Scene/prefab override ngoài catalog chưa rà lại toàn bộ; hai hồi quy visual test còn fail. Nút Retry Tank và Shipping side-by-side thuộc Bot6, chưa làm. [CODE Reports/FactoryImplementationEvidence/resume-hud-final-result.json:1] [SUY LUẬN]
