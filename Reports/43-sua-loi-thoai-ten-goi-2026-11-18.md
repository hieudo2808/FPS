# Sửa thoại và tên gọi Outbreak Protocol — 18/11/2026

> Phần radio trong báo cáo này là lịch sử lượt sửa đầu. Sau phản hồi về dấu `;` và tải đọc khi chơi FPS, 28 câu đã được rút gọn và ngắt cue lại trong [báo cáo 44](44-rut-gon-radio-theo-nhip-doc-fps-2026-11-18.md). Canon và catalog hiện hành dùng bản mới.

## Kết quả

Đã viết lại 28 câu radio bằng tiếng Anh và đồng bộ 28 bản Việt trong catalog; người điều phối hiển thị **Command / Bộ chỉ huy**, không cần tên cá nhân Mira Shaw. Người dùng đã chọn rõ **Mason / Doc / Vega / Raven xuyên suốt**; màn chọn nhân vật được đồng bộ về bộ này. Vanguard vẫn là tên đội. [CODE Documents/Outbreak-Protocol-Master-Canon.md:13]

Bản sửa giữ các mốc truyện và tiến trình hiện có: điều tra chuỗi T9-17 → Northbridge → B2 → E-02; hai kiện gốc, bản sao đầy đủ nhận ở bên ngoài, phong tỏa tiếp tục. Tên Adrian Cole, Marcus Hale và Maya Reed trong chứng cứ được giữ, vì chúng xác lập danh tính và trách nhiệm; không biến họ thành NPC sống. [CODE Documents/Outbreak-Protocol-Master-Canon.md:11]

## Sửa những gì

| Bề mặt | Thay đổi |
|---|---|
| Radio | Câu nói ngắn, có phản ứng và chỉ dẫn; bỏ việc đọc tên chương và thuật ngữ HUD thành lời thoại. Command và bên thuê có giọng riêng. |
| Tính nhất quán | D06 dùng được khi người chơi hoàn tất Power hoặc Logistics trước. D09 không nói trước thẻ B2. D11 nói **hồ sơ ghi tử vong**, không khẳng định P046 đã chết. D21 xác nhận nhận dữ liệu và bảo tới thang chờ, không cho lên trước phase. |
| Nhân vật | Tên hiển thị theo asset/enum Brimstone: Mason; Sage: Doc; Gekko: Vega; Clove: Raven. Giữ nguyên enum, ID và prefab. |
| Squad HUD | Tên chính là callsign; nhãn phụ VANGUARD, chữ đại diện lấy từ nickname thay vì tên enum gốc. Chỉ thay text, giữ layout/font/theme. |
| Màn chờ | Dropdown, dòng roster và fallback text đều dùng CampaignCodename.For thay vì tên enum; giữ nguyên thứ tự và giá trị lựa chọn gửi server. |
| Vật phẩm | Kiện T9-17 và E-02 được gọi nhất quán; mô tả fuse/card/case bỏ cách nói như log giao dịch. |
| Chỉ dẫn | `all three identifiers` thành `all identifiers`; chuỗi bằng chứng có E-02/T9-17/C12/P046. Hint không nhắc “server phải xác nhận” với người chơi. |
| Canon | Đồng bộ 28 câu EN, vai trò Command, bộ nickname đã chọn và quy tắc giọng thoại. |
| Tài liệu cũ | Hai Markdown thiết kế/status được gắn banner lịch sử và dẫn canon. Ba Word đã được canon xác định là lưu trữ tham khảo; không sửa lại lịch sử trong chúng. |

Đã kiểm tra tám nhãn WeaponData qua Unity: Classic, Vandal, Operator, Odin, Bucky, Warden, AKM, Pistol. Không có yêu cầu cụ thể chọn tên mới cho từng súng; giữ các nhãn hiện có, không đoán model thật từ hình dáng và không sửa logic súng. [INFERENCE; dữ liệu gốc: Narrative-2026-11-18/weapon-names.json]

## Ví dụ thoại

Trước: `Shipping release recorded. Keep the original manifest in Files and continue the remaining branch.`

Sau: `The release checks out. Keep that manifest; it ties the case to Northbridge.`

Bản Việt: `Giấy xuất hàng khớp rồi. Giữ lại vận đơn; nó nối kiện này với Northbridge.`

Lệnh che giấu: `Keep the sample. Leave patient names and staff objections out of the export.`

Command đáp: `Vanguard, disregard that; BRT needs the whole record. Send everything, names and objections included; I'll confirm when it's through.`

Giấy tờ vẫn giữ giọng quy trình, biên bản, thư và email thích hợp. Chúng không cần nói giống radio. Game hiện vẫn hiển thị tiếng Anh; bản Việt được đồng bộ trong field dữ liệu, không bổ sung hệ thống đổi ngôn ngữ. [CODE Assets/FPS/Features/Missions/Runtime/CampaignDialogue.cs:19]

## Phạm vi và phê duyệt

Đã đọc chat tham chiếu `Tìm skill viết narrative Unity` bằng read_thread; chat đó mới đề xuất skill, chưa cài. Đã kiểm nguồn GitHub và nội dung trước khi cài bằng skill-installer. [INFERENCE]

Reviewer độc lập `canon_audit` đã duyệt flow giữ nguyên và bản copy cụ thể trước khi áp dụng; đã yêu cầu sửa các lỗi thời điểm/ý nghĩa rồi mới chấp thuận. Chi tiết và SHA bản duyệt ở [review-flow.md](Narrative-2026-11-18/review-flow.md). Phần nickname cuối lấy theo câu trả lời trực tiếp của người dùng, thay giả định biên tập ban đầu. [INFERENCE]

Không thêm tương tác, quest, NPC, plot twist hoặc trigger. Không thay đáp án, điều kiện hoàn tất, mạng, AI, audio hay save logic. Tổng cộng chỉ năm text binding trong ba file UI: hai dòng SquadHUD, hai dòng WaitingRoomUI và một dòng PlayerRosterEntryView. Giữ giao diện đã duyệt; không tạo mockup hoặc layout mới. [INFERENCE]

## Kiểm chứng dữ liệu

Snapshot lấy từ UnitySkills SerializedObject, không đọc/sửa YAML để thao tác asset. Kết quả thật sau áp dụng:

```text
PASS: 93 text fields changed; 784 other catalog fields unchanged.
PASS: 28 unique dialogue IDs; complete EN/VI; exact reviewed copy and speakers.
PASS: 4 PlayerPrefabCatalog displayName fields changed; 15 other fields unchanged.
PASS: negative guard rejected a deliberately modified dialogue trigger.
```

Đã đối chiếu toàn bộ field của 21 FILE, 28 trigger/priority/cooldown/once/audio, choices/secondaryChoices/file của 20 objective, acquired/consumed/persist/icon của bốn key item. Tất cả giữ nguyên. Reviewer chạy kiểm tra độc lập cùng kết luận. [INFERENCE; Narrative-2026-11-18/verification.json, players-verification.json, review-flow.md]

`CampaignCodename` cuối cùng giống baseline; không cần đổi code định danh để dùng Mason/Doc/Vega/Raven. Tên enum kỹ thuật `MiraRejectsOrder` được giữ cùng ID ổn định, không hiển thị ra người chơi. [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:185]

## Kiểm chứng hiển thị và Editor

Đã chạy host đơn bằng prefab/campaign thật tới Factory/Exploring. Bản sau kiểm đúng 28 câu EN/VI và speaker so bản được duyệt. Đã chụp 16 ảnh trước/sau (8 cặp) của Radio, Journal, SquadHUD và Waiting Room ở **1920×1080 và 3840×2160**; ảnh gốc được capture Game View thật đúng độ phân giải. Ảnh tổng quan/crop chỉ hỗ trợ review, không thay thế ảnh gốc. Log chạy và mã SHA256 từng ảnh được giữ trong [after-playtest.txt](Narrative-2026-11-18/qa/after-playtest.txt) và [screenshots-manifest.json](Narrative-2026-11-18/qa/screenshots-manifest.json). [PLAYTEST]

Review không thấy lỗi cắt chữ hoặc chồng nhãn trong phần sửa. Reviewer độc lập đối chiếu 6 cặp radio/Journal/roster và ảnh Waiting Room trước sửa ở 1080p; tác nhân chính tự kiểm ba ảnh Waiting Room còn lại. Phạm vi cộng lại bao phủ 16 ảnh nguồn, không tuyên bố tất cả đều được reviewer độc lập xem. Đã xóa ảnh review gốc và crop theo AGENTS, giữ manifest, log và [biên bản review hình ảnh](Narrative-2026-11-18/qa/review-visual.md). [PLAYTEST]

SquadHUD dùng fixture thành viên mất kết nối để kiểm text thật; LobbyScene dùng bốn dòng roster tĩnh qua API tạo entry hiện có. Đây là kiểm renderer/nhãn, không phải phiên multiplayer thật hoặc hành động chọn nhân vật qua mạng. Log thực:

```text
CALLSIGN Clove = RAVEN
CALLSIGN Brimstone = MASON
CALLSIGN Sage = DOC
CALLSIGN Gekko = VEGA
PASS: 28 unique IDs, complete EN/VI, exact reviewed copy and speakers
PASS roster rendering fixture: DOC / VANGUARD / D / RECONNECTING
PASS waiting room static label: option=RAVEN; row=RAVEN  /  SQUAD LEADER
PASS waiting room static label: option=MASON; row=MASON  /  OPERATOR
PASS waiting room static label: option=DOC; row=DOC  /  OPERATOR
PASS waiting room static label: option=VEGA; row=VEGA  /  OPERATOR
STOP: original authored scene and checkpoint restored; dirty=False
```

Sau khi tháo script QA tạm và import lại: **isPlaying=false, isCompiling=false, console errors=0, GameScene isDirty=false**. Scene đã được lưu và File/Save Project gọi trước/sau. SHA256 scene khớp baseline `032C97D52F0DD0F291E92A70B9D09177FFE39876308414F18926F81146DB0D87`; checkpoint chính/cũ khớp hash bản backup. [SCENE; Narrative-2026-11-18/final-editor-check.json]

Code diff được reviewer độc lập xác nhận chỉ năm text binding. `git diff --check` cho các file của lượt này pass. Log playtest đầy đủ và harness tái lập giữ ngoài Assets trong `Narrative-2026-11-18/qa/`; source QA không còn trong Editor.

## Skills đã đọc và quy tắc áp dụng

| Skill | Quy tắc đã áp dụng |
|---|---|
| find-skills | Đọc nguồn skill thay vì chọn chỉ theo kết quả tìm kiếm; kiểm tra repo/lượt cài. |
| skill-installer | Cài từ path GitHub bằng helper; không ghi skill vào Assets hoặc thêm dependency Unity. |
| creative-writing-craft + scene-construction/prose-modes | Phân biệt giọng, giữ kiến thức đúng thời điểm, sửa bề mặt nhỏ nhất giải quyết câu thoại đơ. |
| writing-principles + antipatterns | Tránh thoại chỉ báo cáo, kết luận đạo đức quá đầy đủ và câu nhịp đều vô cảm; không làm mất thông tin người chơi cần. |
| story-planning + creative-direction | Kiểm canon và quyết định đã có; không tự thêm diễn biến trong lượt sửa copy. |
| dialogue-systems | “Keep what exists”; giữ runner/catalog, stable IDs và biến trạng thái ngoài UI; không thêm Ink/Yarn. |
| unity-skills, scene/editor/scriptableobject | GET health/meta/schema, dryRun trước lệnh chưa chắc tham số; setter SerializedObject cho array/nested field, đọc lại sau ghi. |
| game-ui-ux, unity-mcp-ui-layout (mục verification/repair) | Giữ UI hiện có, kiểm text thật ở 1080p và 4K; phân biệt ảnh thật với fixture và kiểm capability reviewer nhìn ảnh. |
| code-review-and-quality | Reviewer độc lập kiểm tính đúng, phạm vi, dữ liệu ngoài whitelist và kết quả test. |
| debugging-and-error-recovery | Lưu lỗi compile harness, xác định trùng tên kiểu, sửa alias rồi xác minh compile sạch trước chạy tiếp. |
| ponytail | Tận dụng catalog/callsign sẵn có; không thêm hệ thống thoại hoặc dependency, script QA chỉ tồn tại tạm trong Editor. |

Đã cài tại `C:/Users/hieud/.codex/skills/`: [creative-writing-craft](https://github.com/haowjy/creative-writing-skills/tree/main/skills/creative-writing-craft), [story-planning](https://github.com/haowjy/creative-writing-skills/tree/main/skills/story-planning), [writing-principles](https://github.com/haowjy/creative-writing-skills/tree/main/skills/writing-principles), [dialogue-systems](https://github.com/gamedev-skills/awesome-gamedev-agent-skills/tree/main/skills/disciplines/dialogue-systems). Skills đã được đọc và áp dụng trong lượt này; tự động xuất hiện trong catalog skill ở lượt tiếp theo.

## Giới hạn

Không playtest toàn chiến dịch hoặc build host/client mới. Fixture roster chỉ kiểm cách hiển thị thành viên mất kết nối, không chứng minh multiplayer thật. Không viết lại lore/Word lưu trữ, không thu giọng mới; 28 audio reference hiện đều null và giữ nguyên.
