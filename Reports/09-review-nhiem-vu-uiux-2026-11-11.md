# Review thiết kế nhiệm vụ và UI/UX — 11/11/2026

Vai review: Game Designer, Lead UI/UX, Narrative Director. Đây là lượt **chỉ đọc**: không sửa code, scene, asset. Mọi mục "sửa" bên dưới là đề xuất, chưa được duyệt flow.

## Kết luận

Người chơi trả lời được "đang làm gì" và "đi đâu", nhưng **không** trả lời được "làm xong được gì" ở cấp chapter, và có một điểm chặn cứng: Tank spawn thất bại ở Factory thì HUD bảo host bấm một nút không tồn tại. Cấu trúc nhiệm vụ trong rules là hợp lý; chỗ hỏng nằm ở trình bày: cùng một tiến độ đang được vẽ ở năm nơi bằng năm bộ chữ viết tay khác nhau, và hai ngôn ngữ hình ảnh (panel "device" mới và panel cũ viền gỉ) cùng tồn tại. Việc nên làm trước tiên là **cắt**, không phải thêm.

## Phạm vi đã kiểm

- Tài liệu: `05-factory-bot6-handoff.md`, `06-bot6-ui-remaining-plan.md`, `08-fix-bot7-2026-11-10.md`, `UIUX/ThreeTabInventory-2026-11-10/bao-cao.md`.
- Source: `CampaignTypes.cs`, `CampaignMissionController.Tank.cs`, `CampaignHUD.cs`, `CampaignHUD.Navigation.cs`, `CampaignDialogue.cs`, `CampaignObjectiveSelection.cs`, `TeamInventoryUI.cs`, catalog objective trong `CampaignContentCatalog.asset`.
- Ảnh runtime 1080p: `three-tabs-missions`, `three-tabs-record`, `selected-hud`, `selected-locked`, `shipping-wrong-native`.
- [SCENE Assets/FPS/Scenes/MainMenu.unity] `isDirty=false` trước và sau; Editor không ở PlayMode.

Giới hạn: Codebase Memory MCP chưa có project nào được index (`list_projects` trả 0), nên đọc trực tiếp source đã khoanh vùng. Không chạy PlayMode, không có host + client. Một số ảnh cũ hơn code hiện tại (chụp 10:07, Bot 7 sửa lúc 15:06); chỗ nào ảnh và code lệch thì tin code và ghi rõ.

## Phát hiện theo mức ưu tiên

| # | Mức | Vấn đề | Bằng chứng | Tác hại | Cắt hoặc sửa tối thiểu |
|---|---|---|---|---|---|
| 1 | P0 | Tank spawn thất bại ở Factory là ngõ cụt, HUD chỉ dẫn tới nút không có | [CODE CampaignTypes.cs:393] chữ "Host: Retry Tank". [CODE CampaignMissionController.Tank.cs:14] `RequestRetryTank` không có caller nào trong `Assets/`. [CODE CampaignTypes.cs:371] Factory vào `SpawnFailed`, hai chapter kia tự `Resolved`. [CODE CampaignTypes.cs:443] Case đòi `factoryTankDefeated`. [CODE CampaignMissionController.Tank.cs:67] vòng lặp thoát nếu không còn `Pending` | Sau 20 giây spawn hụt, chapter 1 không thể hoàn thành. Người chơi được bảo đi dọn lỗi kỹ thuật bằng một nút không tồn tại | Server tự đưa `SpawnFailed` về `Pending` sau một nhịp nghỉ: gọi lại đúng phần thân của `RetryFactoryTank` từ `UpdateTankEncounter`. Xóa RPC, `CanRetryTank`, `RequestRetryTank` và câu "Host: Retry Tank". Cần duyệt flow vì chạm logic Tank |
| 2 | P1 | Chữ kỹ thuật của hệ spawn lọt lên tracker giữa lúc chiến đấu | [CODE CampaignTypes.cs:394-405] "director protection is active", "spawn request rejected", "no safe reachable spawn anchor", "20s attempt budget". [CODE CampaignHUD.Navigation.cs:161,174] đưa thẳng lên tracker. [CODE CampaignHUD.cs:720] cũng nối vào "Next: …" sau khi xong nhánh | Phá bối cảnh, vô nghĩa với người chơi, đổi mỗi giây | Giữ enum cho log. Người chơi chỉ thấy hai trạng thái: "Something is coming" khi Pending, "Eliminate the Tank" khi Active. Bỏ đếm ngược budget khỏi HUD |
| 3 | P1 | Tiến độ nhiệm vụ được vẽ ở năm nơi, mỗi nơi một bộ chữ viết tay | Rules: [CODE CampaignTypes.cs:430]. Tracker: [CODE CampaignHUD.Navigation.cs:245]. Tab Missions: [CODE TeamInventoryUI.cs:274]. Popup cửa: [CODE CampaignDialogue.cs:86-110] cộng [CODE CampaignHUD.cs:902-918]. Fallback tên: [CODE CampaignHUD.Navigation.cs:220-243] | Cùng một bước có ba tên: "VERIFY SHIPPING RELEASE", "Cross-check the shipping record", "Read the logistics source, then verify the shipment record". Sửa một chỗ thì bốn chỗ kia lệch | Xóa `ObjectiveText()`, `AccessInstructions()`, popup `OpenGate`, và bảng fallback trong `ObjectiveAction`. Cửa khóa chỉ hiện toast "Requires: …" như Shipping đang làm ở [CODE CampaignHUD.cs:432] |
| 4 | P1 | Tab Missions là danh sách phẳng; cây nhiệm vụ bị đổ thành một khối chữ trùng với cột trái | [PLAYTEST three-tabs-missions-1920x1080-100.png] Power và Logistics nằm lẫn, không tiêu đề nhóm; khối "POWER 1 OF 3 COMPLETE / COMPLETE · ISOLATE…" lặp đúng ba dòng bên trái. [CODE TeamInventoryUI.cs:310-320]. Asylum và Lab: "CHAPTER PROGRESS" liệt kê lại toàn bộ danh sách | Không thấy quan hệ "hai nhánh song song dẫn tới Case". Mục tiêu chapter (Case, Route) nằm dưới nếp cuộn vì viewport chỉ vừa năm dòng [INFERENCE từ hàng 92 px trong viewport 470 px] | Bỏ khối danh sách bước trong phần chi tiết. Thêm dòng tiêu đề nhóm không bấm được vào cột trái bằng `AddRow` sẵn có: "POWER 1/3", "LOGISTICS 1/2", "EXTRACTION". Không cần schema cây mới |
| 5 | P1 | Hai ngôn ngữ hình ảnh cho cùng loại thao tác | [PLAYTEST selected-locked-1920x1080.png] panel device mới. [PLAYTEST shipping-wrong-native-1920x1080.png] panel cũ viền gỉ, font stencil, nút lệch lưới. [CODE CampaignHUD.cs:448] chỉ bốn objective Factory dùng device; Shipping, toàn bộ Asylum, Lab, Supply, Preparation dùng panel cũ | Người chơi không biết hai thứ là cùng một hệ. Vi phạm chuẩn "một theme" trong AGENTS.md | Không vẽ panel thứ ba. Cho mọi objective đi qua `SetDeviceLayout(true)` và xóa nhánh panel cũ; đây là mở rộng điều kiện ở dòng 448 cộng dời vùng choices, không phải màn mới. Cần mockup duyệt trước |
| 6 | P1 | Trạng thái khóa được nói bảy lần trên một màn, và màn đó lẽ ra không cần mở | [CODE CampaignHUD.cs:539-546, 577, 602-620] `deviceState`, `deviceNoticeHeading`, `deviceNoticeValue`, `deviceNoticeHint`, `deviceAction`, nút confirm, `deviceHint`. [PLAYTEST selected-locked-1920x1080.png] | Mở cả màn hình chỉ để đọc "LOCKED". Shipping khóa thì chỉ hiện toast, Backup khóa thì mở full panel: không nhất quán | Áp guard của Shipping cho mọi objective bị khóa: toast "Requires: …", không mở panel. Sau đó xóa toàn bộ nhánh locked trong `RenderDeviceNotice` và icon khóa |
| 7 | P1 | "Làm xong được gì" chỉ có ở cấp bước và viết bằng giọng hệ thống | [CODE CampaignHUD.Navigation.cs:204-217] dòng "Then: …". Catalog: "Backup control unlocks", "Power branch is recorded", "Logistics branch is recorded" | Người chơi không biết Power cộng Logistics dẫn tới cái gì. Case không track được cho tới khi hạ Tank nên mục tiêu chapter không bao giờ lên tracker | Thay nhãn vị trí "FACTORY" bằng mục tiêu chapter: "FACTORY — Recover the evidence case". Bỏ dòng "Then:". Không thêm phần tử HUD nào |
| 8 | P2 | Files chứa mục giả "Campaign Record" trùng với Missions | [CODE CampaignHUD.cs:645-654] các câu "Shipping release verified…" là trạng thái COMPLETE viết lại. [PLAYTEST three-tabs-record-1920x1080-100.png] hai câu Mira Shaw gần như giống nhau đứng liền. [CODE TeamInventoryUI.cs:201 so với :252] chuỗi "NO FILES RECORDED" không còn đường tới | Journal bị bỏ về tên nhưng nội dung trùng vẫn còn, chỉ đổi chỗ | Xóa phần evidence trong `JournalRecordText`, chỉ giữ `CampaignDialogue.Journal`. Đổi tên mục thành "Transmissions". Bỏ dòng briefing lặp |
| 9 | P2 | Modal Preparation bật lên giữa trận, có nút thừa | [CODE CampaignHUD.cs:895-896] khối chữ năm câu, có "The server reports timeout…", hai nút READY và NOT READY | Chiếm màn hình đúng lúc chuyển pha. NOT READY là trạng thái mặc định, bấm hay đóng đều như nhau | Bỏ modal. READY thành prompt giữ phím trong vùng chuẩn bị, dùng binding Interact sẵn có. Bỏ NOT READY |
| 10 | P2 | Chữ cổng party quá dài và lộ thuật ngữ server | [CODE CampaignTypes.cs:305-322] "Enter the preparation area / In zone 1/2 / READY 0/2 / Timeout override in 43s", "one eligible operator must stay in zone" | Bốn mệnh đề trong khung 421 px khi đang bị Tank đuổi | Một dòng hành động cộng một dòng đếm: "Enter the preparation area" và "1/2 in zone". Bỏ "Timeout override" khỏi HUD |

## 1. Trải nghiệm người chơi

**Đang làm gì:** đạt. Tracker hiện tên bước ngay khi vào chapter. [PLAYTEST selected-hud-1920x1080.png]

**Đi đâu:** đạt có điều kiện. Marker có khoảng cách và HIGHER/LOWER. Khi NavMesh không ra đường, marker chỉ còn "m TO TARGET" theo đường thẳng [CODE CampaignHUD.Navigation.cs:356]; bằng chứng B2 đã ghi một số cửa trả `PathPartial`, nên trường hợp này sẽ xảy ra thật.

**Được gì:** không đạt, xem mục 7.

**Dọn rác kỹ thuật:** có, mục 1 và 2. Ngoài ra chữ dành cho người chơi còn lộ kiến trúc mạng: "the server must confirm the Tank defeat" (hint của Case trong catalog), "FAILED · waiting for host checkpoint restore" [CODE CampaignHUD.cs:911], "Tracking is local; progress is shared by the squad" [CODE TeamInventoryUI.cs:308].

**Nhiễu khi chiến đấu:**
- Ở Asylum và Lab, tracker in tên bước **hai lần**: `status` và `actionLabel` cùng lấy `ObjectiveAction` [CODE CampaignHUD.Navigation.cs:150,167]; chỉ Factory ghi đè dòng dưới bằng hai nhánh.
- Dòng "T SWITCH" chiếm một hàng tracker suốt chapter Factory.
- Hint ba bậc 45/90/135 giây của nhánh Power nói cùng một ý ba lần ("Read Utilities Procedure…"), trong khi ba bước đó chỉ là giữ F theo thứ tự và UI đã tự báo thứ tự. Asylum và Lab để trống `hint2`, `hint3`, nên hệ ba bậc chỉ chạy cho đúng năm objective.

## 2. Cấu trúc nhiệm vụ

Cấu trúc thật trong rules, đối chiếu thang bốn tầng:

| Tầng | Factory | Northbridge | Laboratory |
|---|---|---|---|
| Nhiệm vụ lớn | Lấy case, mở đường | Lấy thẻ B2, gọi thang | Niêm phong bản gốc, truyền bằng chứng |
| Nhóm song song | Power ‖ Logistics | Access ‖ Fuse | không có |
| Bước | 3 + 2, rồi Tank, Case, Route | Install → Power → Patient → Transfer → Lift | Power → Archive → Case → Transmit |
| Thao tác vật lý | giữ F, chọn hai nhóm | giữ F, chọn một | bật công tắc, chọn một |

Điểm bất hợp lý:

1. **Tank là một bước nhưng không phải objective.** Case ghi "Requires: defeat the Tank" nhưng danh sách không có dòng Tank; tracker phải ghi đè bằng nhánh riêng [CODE CampaignHUD.Navigation.cs:159-161]. Đây là nguồn gốc của mọi chữ đặc biệt ở mục 2.
2. **Thao tác vật lý bị nâng thành bước.** "Read Delivery Manifest" là nhặt một file nhưng được đếm 1/2 nhánh Logistics, kéo theo ba ngoại lệ trong rules [CODE CampaignTypes.cs:252, 435, 536]. Tương tự "Read Security Desk Log" và "Read Archive Index".
3. **Hai bước trùng nhau ở Lab.** "Trace B2 Transfer" và "Read Archive Index" có cùng kết quả "Archive index appears in Files", không bước nào phụ thuộc Trace, và cả hai bị tự đánh dấu xong khi Archive xong [CODE CampaignTypes.cs:537]. Trace là dòng độn.
4. **Một thao tác bị tách đôi ở Northbridge.** "Install Service Fuse" và "Restore Service Power" cùng ở lower service panel; chính checklist của game gộp chúng thành một dòng [CODE CampaignDialogue.cs:102].
5. **Pha sau Route không có trong Missions.** Preparation, Encounter, Boarding chỉ sống trên tracker; mở tab Missions lúc đó thấy toàn COMPLETE và không có việc tiếp theo.

Các mục 2 đến 4 là đổi nội dung nhiệm vụ: chỉ nêu làm đề xuất, cần flow được duyệt.

**Ranh giới Missions và Files** (thay cho Journal đã bỏ):

| Câu hỏi của người chơi | Nơi duy nhất trả lời |
|---|---|
| Phải làm gì, còn bao nhiêu, vì sao bị khóa | Missions |
| Tài liệu tôi đã nhặt nói gì | Files |
| Ai đã nói gì qua radio | Files, một mục "Transmissions" |
| Tôi đã hoàn thành gì | Missions, trạng thái COMPLETE; không viết lại thành câu văn ở Files |

Quy tắc kiểm: nếu một dòng chữ đổi khi objective hoàn thành thì nó thuộc Missions. Files chỉ đổi khi nhặt tài liệu hoặc nhận thoại.

Phụ: bấm một dòng trong Missions vừa xem vừa **đổi tracking** [CODE TeamInventoryUI.cs:289-292]. Duyệt danh sách không nên có tác dụng phụ; và sau khi đã track, phần chi tiết vẫn ghi "Select this objective to follow it".

## 3. UI/UX và tính nhất quán

Vẽ thừa, ngoài các mục trong bảng:

- Panel device lặp tên hệ thống năm lần trên một màn: breadcrumb "POWER SYSTEM / BACKUP", tiêu đề, "BACKUP / INSTRUCTIONS", "CONTROL / BACKUP", nhãn hàng "Backup". [PLAYTEST selected-locked-1920x1080.png]
- Hàng "Backup  -" luôn hiện dấu gạch: `systemValue` được gán "-" một lần và không bao giờ đổi [CODE CampaignHUD.cs:317-318]. Hàng "Source: NOT REQUIRED" không mang thông tin với ba bước Power.
- Squad Inventory có hai chỉ dẫn đóng (góc trên phải và footer) và footer "SELECT AN ENTRY TO VIEW DETAILS" vẫn hiện khi đã chọn mục. Eyebrow "CAMPAIGN" là chữ tĩnh. [PLAYTEST three-tabs-missions-1920x1080-100.png]
- Shipping: nhãn nhóm bị lặp trên cả sáu nút, khi sai thì "— CHECK THIS GROUP" lặp ba lần [CODE CampaignHUD.cs:624-628]. Một tiêu đề mỗi hàng là đủ.
- Nhánh Failed và Completed trong `RenderPhaseControls` là code chết khi có `CampaignStatusUI` [CODE CampaignHUD.cs:887 so với :897-900].

Phản hồi im lặng hoặc tối nghĩa:

- "Prerequisites are incomplete. Check the team objective." [CODE CampaignHUD.cs:737] trong khi `Availability().Reason` đã có lý do cụ thể.
- "Interaction interrupted; completed steps are preserved." là câu mặc định cho mọi kết quả còn lại, gồm sai chapter, sai pha, actor không hợp lệ.
- `Open()` thoát không báo gì khi `BlocksInput` [CODE CampaignHUD.cs:431].
- "RESUME CHECKPOINT" chỉ hiện trong footer tab Missions, chỉ với host, chỉ khi chưa có tiến độ hoặc đã Failed [CODE CampaignMissionController.cs:57], bấm một lần là nạp save, không xác nhận. Người chơi muốn chơi tiếp save cũ sẽ không tìm ra.

Thuận tiện: phần đã làm tốt và nên giữ nguyên là source-first ở comparison, lỗi Shipping chỉ đúng nhóm sai, giữ focus và scroll khi snapshot đổi, hủy giữ có phản hồi.

## 4. Cắt bỏ và giữ nguyên

**Bỏ ngay, không cần thiết kế mới** (thuần xóa, mỗi mục làm codebase nhỏ đi):

1. Câu "Host: Retry Tank" và API retry không caller, sau khi mục 1 có flow.
2. `ObjectiveText()`, `AccessInstructions()`, popup `OpenGate` và nút "CLOSE GUIDE".
3. Bảng fallback 20 dòng trong `ObjectiveAction`.
4. Khối danh sách bước trong `ShowMission`.
5. Phần evidence trong `JournalRecordText`; chuỗi "NO FILES RECORDED".
6. Nhánh Failed và Completed chết trong `RenderPhaseControls`.
7. Footer "SELECT AN ENTRY TO VIEW DETAILS", chỉ dẫn đóng thứ hai, eyebrow "CAMPAIGN".
8. Hàng "System -" và "Source: NOT REQUIRED" trên device.
9. Nút NOT READY.
10. `hint2`, `hint3` và `HintTier`; giữ một hint.
11. Dòng "Then:" trên tracker; dòng tên bước lặp ở Asylum và Lab.
12. Mọi chữ "server", "host", "local", "attempt budget", "timeout override" trong text người chơi đọc.

**Giữ nguyên:** shell hai cột Squad Inventory, ba tab, tracker góc trái, marker có khoảng cách, device panel làm chuẩn hình ảnh, source-first, receipt lỗi theo nhóm, hold có hủy, authority ở server.

**Thứ tự đề xuất, ít rủi ro trước:**

1. Lượt xóa thuần (mục 2 đến 8, 10 đến 12): không đổi hành vi, chỉ cần cập nhật test đang assert các chuỗi bị xóa.
2. Tank tự phục hồi phía server: cần flow duyệt, cần test Editor mới cho `SpawnFailed → Pending`.
3. Toast thay cho panel khóa và popup cửa: một guard dùng chung.
4. Tiêu đề nhóm trong Missions và mục tiêu chapter trên tracker: cần mockup duyệt.
5. Hợp nhất panel cũ vào device: cần mockup duyệt, việc lớn nhất, làm sau cùng.

Thay đổi đơn lẻ có hiệu quả lớn nhất: **mục 1**. Mọi thứ khác là khó chịu; mục này là không qua được màn.

## Chưa kiểm được

- Chưa tái hiện `SpawnFailed` trong PlayMode; kết luận ngõ cụt dựa trên đọc code. Restore checkpoint có thể gỡ được vì `ResetUnverifiedFactoryTank` đưa stage về `Dormant` [INFERENCE], chưa chạy thử.
- Chưa xác nhận Case và Route nằm dưới nếp cuộn bằng ảnh; suy từ kích thước hàng.
- Không có ảnh Asylum, Lab, Preparation, popup cửa; các nhận xét ở đó chỉ từ code.
- Chưa kiểm host cùng client hai tiến trình.
- `LabTrace` có interactable trong scene hay không chưa truy vấn.

## Skills đã đọc và quy tắc áp dụng

- `unity-skills`: `/health` trước, chỉ gọi skill đọc (`scene_get_loaded`, `editor_get_state`); không ghi.
- `audit-ai-design-slop`: mỗi phát hiện có vị trí cụ thể; ưu tiên xóa trước khi thay; không kê font, palette hay layout mới; phân mức P0 đến P3.
- `game-ui-ux`: HUD theo sự kiện, một stack màn hình, luôn có focus; dùng để đánh giá modal Preparation và popup cửa.
- `level-design`: critical path và gating có chủ đích, kiểm soft-lock; dùng cho mục 1 và phần cấu trúc.
- AGENTS.md: báo cáo tiếng Việt trong `Reports/`; nhãn bằng chứng; không tự duyệt; phần ngoài phạm vi chỉ nêu làm đề xuất; scene sạch trước và sau.
