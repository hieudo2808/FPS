# Bot 6 — Kế hoạch triển khai và trạng thái

## Cập nhật triển khai — 10/11/2026

Người dùng đã chốt Inventory còn ba tab Key Items, Files, Missions, bỏ Journal và yêu cầu code theo thiết kế hiện hữu. Delta đang triển khai: giữ shell hai cột đã duyệt; Campaign Record nằm trong Files, instructions cùng tiến độ bước nằm trong Missions, Resume giữ callback cũ ở Missions. Hủy yêu cầu giữ bốn tab và mọi gate Retry cũ. Không đổi nhiệm vụ hoặc logic spawn. Kiểm và ảnh mới nằm tại `Reports/UIUX/ThreeTabInventory-2026-11-10/bao-cao.md`; chưa kết luận toàn bộ Bot 6 hoàn thành. Shipping và acceptance toàn bộ còn theo các mục tương ứng.

Bắt đầu 05/11/2026; cập nhật 06/11/2026, Asia/Saigon. **Chưa hoàn thành Bot 6. Yêu cầu mới nhất đã hủy đề xuất Retry Tank trong UI.** Những phê duyệt của flow Retry cũ không áp dụng cho hướng Missions bên dưới. Chưa sửa UI runtime hoặc logic nhiệm vụ trong lượt điều chỉnh phạm vi này.

## Phạm vi cố định

Giữ phong cách chín giao diện, shell Squad Inventory, theme, font và asset đã chọn. Shipping comparison tiếp tục trong panel hiện hữu. **Không làm card Retry Tank, không yêu cầu người chơi xử lý lỗi spawn.** Không tạo màn mới hoặc tạo lại hai ảnh Squad Inventory đã bỏ; không đổi nội dung, đáp án, điều kiện nhiệm vụ, weapon, audio, AI hoặc save logic.

Người dùng đặt lại yêu cầu: Missions cần thể hiện nhiệm vụ lớn và các bước nhỏ; có thể bỏ Journal nếu trùng chức năng. Đây chưa phải phê duyệt xóa tab hoặc chuyển dữ liệu. Cần xác định phần trùng và phần riêng trước; số tab không còn là ràng buộc đã chốt cho thiết kế kế tiếp. Resume Checkpoint giữ chức năng hiện hữu, không gộp với xử lý spawn.

Gate mới: đối chiếu cấu trúc nhiệm vụ thật → chốt flow hợp nhất nội dung → duyệt delta trên giao diện Missions hiện hữu → code. Theo AGENTS.md: “Mockup approval comes before code” và “After each gate stop and wait for an explicit approval in the current conversation.” Không tiếp tục xin duyệt ảnh Retry Tank.

## Kết luận source về Missions và Journal — 06/11/2026

- Logic đã có chapter, objective và quan hệ điều kiện trước sau. Factory có nhánh Power gồm Isolate → Backup → Generator và nhánh Logistics gồm Manifest → Shipping. Northbridge yêu cầu Access cùng Fuse trước Install, rồi Power; các bước Patient, Transfer và Lift tiếp tục có prerequisite. [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:255] [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:415]
- UI Missions hiện duyệt objective của chapter và tạo từng row ngang cấp; chưa trình bày cây nhiệm vụ nhiều tầng. Catalog objective chưa có trường parent hoặc children tổng quát. Không được báo rằng đã có sẵn hệ thống phân cấp tùy ý. [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:236] [CODE Assets/FPS/Features/Missions/Runtime/CampaignContentCatalog.cs:82]
- Các thao tác chọn và giữ xác nhận nằm trong tương tác của objective; không mặc nhiên là task con có trạng thái riêng. Server kiểm selection, thời gian giữ và điều kiện hoàn thành. [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:607] [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:647]
- Journal có phần Next trùng chỉ dẫn objective đang theo dõi, nhưng Record còn có bằng chứng và lời thoại đã nhận. Bỏ tab phải giữ phần dữ liệu riêng; chưa quyết định tự chuyển tất cả sang Files. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:627] [CODE Assets/FPS/Features/Missions/Runtime/CampaignDialogue.cs:70]
- Factory SpawnFailed hiện vẫn giữ điều kiện phải hạ Tank trước khi lấy Case. Chưa thấy đường tự phục hồi từ SpawnFailed trong UpdateTankEncounter. Bỏ UI Retry không tự sửa được điểm chặn này; cần một flow xử lý lỗi thuộc game, không giả kill hoặc tự mở Case trong lượt UI. [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.Tank.cs:44] [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:418] [INFERENCE]

## Trạng thái bằng chứng

| Kiểm tra | Kết quả mới | Giới hạn |
|---|---|---|
| Nine screens, job aeb5557d | 1/1 pass, 27 PNG ở 1080p, 4K và 4:3 | Fixture hiển thị chín trạng thái; chưa nghiệm thu Shipping hoặc Retry |
| Input, scroll, opacity, native hold, job 4322a389 | 1/1 pass | I là input event; đổi Journal tab có gọi onClick; client debrief là fixture, chưa hai tiến trình |
| Hai test cũ, bd7269d2 và 414a0cc8 | Tái hiện fail do TacticalUiTheme cũ | Không sửa runtime để khớp test |
| HudRegressionTests, job 0c7d4cd5 | 16/16 pass sau đổi ba assertion sang SelectedUiTheme | Kiểm cả assertion layout sau chỗ fail đầu |
| Shipping baseline, job f958a2ae | 1/1 pass, hai ảnh 1080p và 4K | Chỉ ảnh trước sửa, không phải accepted playtest |
| Escape và focus, job 6a7f3441 | 1/1 pass | Đóng overlay, phục hồi focus/cursor; một Escape không mở Pause |

Lịch sử riêng: job 7d0b5b37 đạt 1/1 với 27 ảnh. Không cộng kết quả cũ vào kết quả mới. Bằng chứng mới trong `Reports/UIUX/Bot6Implementation-2026-11-05/`.

## P0 — Baseline và review giao diện cũ

Đã ghi Git status, hash và bản sao các file dự kiến chạm. Codebase Memory MCP không khả dụng trong phiên; dùng source đã khoanh vùng sau query Unity-Skills. MainMenu trước đọc sạch. Đã query Canvas, CanvasScaler, EventSystem và InputSystemUIInputModule; đây là MainMenu, chưa phải readback runtime GameScene.

Tiếp tục review từng nhóm theo bảng, ghi riêng đã chạy, chỉ có source và chưa chạy:

| Nhóm | Phải kiểm | Điều kiện đạt |
|---|---|---|
| Inventory | I mở và đóng, Escape, bốn tab, mở lại | Không kẹt cursor hoặc input; một Escape không mở Pause |
| Files và Key Items | Chọn mục, trống, nguồn dài, cuộn hết | Đúng snapshot, giữ selection khi timer Tank Publish |
| Missions | Locked, Available, Tracking, Completed; đổi nhánh bằng binding | Đúng dữ liệu, không lộ đáp án hoặc tự đổi tracking |
| Journal hiện hữu | Current Record, Next, cuộn, Resume | Ghi đủ nội dung cần giữ nếu hợp nhất; không thêm Retry Tank |
| Device | Manifest, lock, hold, thả sớm, đóng mở | Lý do khóa; ring và phím; hủy có phản hồi |
| Complete và Failed | Summary, quyền retry, input kết thúc | Client không có quyền host; Retry Checkpoint đúng chức năng |
| HUD | Tracker, Pending, PartyGate, reduced HUD, NORTHBRIDGE | Không tràn hoặc chồng, hiểu được ở scale hỗ trợ |

P1 đã xác định từ source: `Refresh()` dựng lại rows mỗi revision và đặt lại focus JournalTab. Phải tách cập nhật Tank khỏi rebuild tab, giữ focus và scroll. Đây chưa phải lỗi đã sửa. [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:149]

## P1 — Thiết kế delta, asset và gate

Nguồn cấu trúc tiếp theo là Missions runtime trong Inventory, giữ phong cách mockup đã duyệt. Trước khi thiết kế, lập bảng chapter, nhóm công việc, objective và thao tác thực tế từ catalog cùng prerequisite; phân biệt bước có state với thao tác trong panel. Không dựng thêm hệ thống task tổng quát chỉ để hiển thị các quan hệ đã có.

Đã sửa script mockup để bỏ generator hai ảnh Squad Inventory và generator Tank lỗi thời. Manifest hash theo file thực; ba ảnh Tank cũ đánh dấu deprecated, không dùng triển khai. Shipping accepted trong storyboard đóng panel về gameplay, không giữ nút Hold màu cam. Đây là storyboard, không chứng minh âm, marker hoặc server accepted.

Đầu ra flow cần duyệt: cách người chơi xem mục tiêu lớn, nhóm bước, bước đang làm, bước đã xong và lý do khóa ngay trong Missions; xác định nơi đọc lại bằng chứng và lời thoại hiện thuộc Journal. Nếu bỏ Journal, phải rà caller, binding, Resume, focus và test điều hướng trước code. Các ảnh Retry trong Review chỉ giữ làm lịch sử, không dùng làm chuẩn triển khai.

Sau khi flow được duyệt mới làm ảnh delta cần thiết: mỗi trạng thái một màn ngang, chỉ có UI, không chú thích biên tập; chữ nút và thanh nhấn căn chính xác. Tái sử dụng handoff Terra nếu cần, không tạo thêm bộ ảnh Squad Inventory.

## P2 — Shipping comparison, owner CampaignHUD

1. Dùng panel và layout hiện hữu; nguồn Manifest trái, hai nhóm phải; source có viewport cuộn riêng.
2. Heading và lỗi thống nhất **Shipment**, **Destination**. Giữ catalog, thứ tự, dữ kiện, answer index; trình bày trường bằng khoảng cách hoặc xuống dòng. Chỉ sửa cụ thể chuỗi trong scope, không thay ký tự toàn cục.
3. Ban đầu hai nhóm chưa chọn; mỗi nhóm một lựa chọn. Sprite ô, Check, Cross thật; reuse Check, Cross từ nguồn trong dự án khi cần, ghi nguồn và license. Tham chiếu icon vào theme hiện hữu, không thêm dependency hoặc theme mới.
4. Lấy màu, font, ba cỡ chữ từ theme, không thêm xanh. Thiếu nhóm thì disable confirm và nêu tên nhóm thiếu.
5. Hold dùng binding và tiến trình hiện có; thả sớm hủy, giữ lựa chọn. MissingGroups và MismatchedGroups đọc receipt server; tối đa bốn giây, xóa theo hành vi khi selection hoặc state liên quan đổi.
6. Accepted chỉ sau revision hợp lệ; đóng panel, dùng text, âm và marker pulse sẵn có. Không thêm cue hoặc sửa audio.

## P3 — Hợp nhất phần trùng trong Missions, chờ duyệt flow

1. Dùng mục tiêu lớn và các nhóm bước có bằng chứng trong source; hiển thị tiến độ, bước đang theo dõi, bước đã xong và lý do khóa. Không thêm nội dung nhiệm vụ hoặc dựng các bước a, b, c, d không tồn tại.
2. Bỏ nội dung Next trùng lặp nếu flow hợp nhất được duyệt. Giữ bằng chứng, lời thoại và đường Resume hiện có; chốt vị trí hiển thị trước khi bỏ tab Journal.
3. Lập phạm vi caller, tab enum, binding, input ownership, focus, navigation, fixture và screenshot bị ảnh hưởng. Không xóa API hoặc dữ liệu chỉ vì một tab biến mất.
4. Cập nhật snapshot không dựng lại tab hoặc đưa focus và scroll về đầu khi dữ liệu đang xem không đổi. Chỉ đặt focus mặc định lúc mở hoặc đổi tab; nếu control biến mất, chuyển focus về control còn tồn tại theo input stack hiện hữu.
5. Không sửa RPC, schema checkpoint hoặc logic hoàn thành để phục vụ bố cục. Không thêm manager, abstraction hoặc dependency.

**Đã hủy:** card, callback Retry, chỉ dẫn mở Journal để Retry, các ảnh trạng thái Retry và các acceptance riêng cho nút này. API retry đang có không bị xóa trong lượt UI; không còn là yêu cầu triển khai control.

**Việc thuộc game cần flow riêng:** tái hiện spawn fail và chốt cách game tự phục hồi hoặc kết thúc encounter khi không thể triển khai. Chưa chọn phương án bỏ encounter, đổi điều kiện Case hoặc ghi nhận kill. Chỉ báo risk hiện hữu, không tự sửa AI và objective trong phạm vi Bot 6.

## P4 — Đồng bộ chữ trong vùng chạm

Shipping, Missions, tracker liên quan và footer Inventory trong vùng được duyệt dùng một tên cho một ý nghĩa. Footer chỉ một chỉ dẫn đóng, Escape vẫn Back. Bỏ dấu gạch chéo ngăn từ ở đúng chuỗi; giữ mã dữ kiện và tỷ lệ số có nghĩa. Không sửa hàng loạt campaign hoặc màn ngoài scope. Chữ lỗi kỹ thuật Tank hiện có phải được xử lý cùng flow game riêng, không thay bằng nút Retry.

## P5 — Kiểm thử sau code

Editor tests sau code được duyệt: Shipping guard thiếu Manifest và sau ghi nguồn; no preselect; selection và icon; missing và mismatch đúng nhóm; receipt cũ không ghi đè completion. Missions trình bày đúng state và prerequisite; không mất record, lời thoại, Resume nếu hợp nhất; snapshot giữ focus và scroll. Reuse test rules có sẵn, không viết lại test mirror implementation.

PlayMode: input events thật từ mouse, keyboard và gamepad giả lập; navigation, tracking, đổi nhóm, đọc nguồn dài và đóng Inventory không kẹt input. UI không đổi completed, files, items hoặc phát completion cue khi chỉ chọn mục. Kiểm receipt và accepted Shipping qua server. Các test retry API hiện có là regression game, không phải acceptance của control mới.

Mạng: một phiên host và client **hai tiến trình** kiểm replication, quyền, UI và input. Test transport một tiến trình ghi riêng, không thay cho acceptance này.

Responsive: 1920×1080, 3840×2160, 1024×768 tại UI scale 100% và 125%; nguồn và lý do dài, binding đổi, cuộn cuối, focus đổi state. Chạy lại nine screens, input, scroll, native hold, navigation và Editor tests bị ảnh hưởng. Test Runner tuần tự; lưu job ID, pass, fail và log thật. Chỉ lặp lại sau thay đổi, lỗi hoặc nghi vấn mới.

Ảnh trước và sau Shipping cùng phần Missions thực sự được duyệt ở 1080p và 4K thuộc revision cuối. Quan sát completion text, âm và marker đúng thứ tự; không lấy ảnh success fixture thay playtest. Không đo lại NavMesh nếu chỉ đổi trình bày và không đổi objective hoặc vị trí; giữ bằng chứng lịch sử và kiểm marker bước chịu ảnh hưởng.

## P6 — Review và bàn giao

Main giữ scope, tích hợp, độc quyền Unity. Reviewer độc lập chỉ đọc diff, tests và ảnh; không tự gán user visual approval. Terra chỉ làm delta đã chốt sau flow, không làm tiếp Retry hoặc tạo bộ ảnh Squad.

Reports tiếng Việt và ảnh ngoài Assets. Trước và sau compile, test, PlayMode, load scene và chuỗi sửa Editor: save authored scene có path và gọi AssetDatabase.SaveAssets; bỏ Untitled và temporary Test Runner. Nếu scene dirty trước bước chỉ đọc thì báo và dừng, không tự save. Kết thúc phải readback active authored scene `isDirty == false`.

Hoàn thành chỉ khi gate được duyệt, required tests pass, hai tiến trình đạt, ảnh cuối được review, Console không lỗi mới, scene sạch. Bất kỳ kiểm tra chưa chạy, fail hoặc bị chặn đều giữ **chưa hoàn thành**.

## Skills đã đọc và áp dụng

- unity-mcp-ui-layout: owner hiện hữu, contract layout trước code, scroll owner, phân biệt mockup với runtime evidence.
- game-ui-ux: input stack, focus, responsive và cập nhật theo sự kiện.
- audit-ai-design-slop: dùng evidence, giữ phong cách đã chọn, bỏ phần dựng thừa.
- unity-skills, scene, editor, test: query trước, readback, test bất đồng bộ tuần tự và Save Gate.
- debugging-and-error-recovery: tái hiện hai lỗi trước sửa; phân biệt kỳ vọng cũ với runtime.
- code-review-and-quality: kiểm correctness, authority, lifecycle, hiệu năng và bằng chứng.
- ponytail full: reuse API, component, asset; không thêm abstraction hoặc dependency.
- level-design: đối chiếu prerequisite và nguy cơ chặn tiến trình; phân biệt logic nhiệm vụ đang có với cách trình bày đề xuất, không tự thay objective.
