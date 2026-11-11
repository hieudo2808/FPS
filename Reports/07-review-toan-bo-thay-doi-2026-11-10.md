# Bot 7 — Kiểm độc lập toàn bộ thay đổi — 10/11/2026

**Phán quyết: TỪ CHỐI.** [SUY LUẬN] Các lỗi MAJOR F01–F07 bên dưới đủ để không chấp nhận đợt này; 80/80 Editor test đạt chỉ xác nhận những assert đã chạy. Không xác nhận BLOCKER gây crash hoặc soft-lock thực tế khi chưa tái hiện.

## Phạm vi, tính độc lập và cách ghi bằng chứng

- [SUY LUẬN] Phạm vi người dùng xác nhận: **“toàn bộ thay đổi”**, working tree so với HEAD `d6a1a803a9b141841438d0c265eb08d9e9a372fa`, gồm tracked và file mới. Kết quả `git diff HEAD --shortstat`: **227 files changed, 9207 insertions(+), 7911 deletions(-)**; con số này không gồm untracked. Có **37 file C# mới dưới Assets/**. Không coi 227 file là một đợt HUD thuần túy.
- [SUY LUẬN] Đầu vào nội dung: AGENTS.md hiện hành, diff, file mới như phần thêm của diff, CampaignContentCatalog theo A1, mockup được chỉ định, ảnh chụp. Không dùng báo cáo riêng hay kết quả test do bot làm cung cấp. Dùng Unity-Skills để kiểm trạng thái và chạy EditMode theo A5.
- [SUY LUẬN] **Có nhiễm nguồn cần công khai:** trong file được phép đọc `Reports/04-hud-design.md` có cả nội dung thiết kế lẫn nhật ký triển khai. Tìm kiếm của tác nhân phụ đã làm lộ phần nhật ký từ khoảng dòng 1179; phần thiết kế cũng có lời kể về triển khai. Các lời kể này được coi **không đáng tin**, không dùng để xác nhận thực thi, test, lý do ngoại lệ, lịch sử phê duyệt hay chất lượng hình ảnh. Chỉ dùng các mockup ảnh được chỉ định làm chuẩn đối chiếu.
- [SUY LUẬN] Ba phần độc lập được chia cho tác nhân phụ: chuỗi/authority, UI tĩnh, inventory ảnh. Tác nhân chính kiểm lại các dòng trọng yếu, ảnh cắt chữ 1080p/125% và Files/1024, các kết quả test và trạng thái scene. Không tác nhân nào sửa nội dung dự án.
- [SUY LUẬN] Không có local Codebase Memory MCP trong danh sách công cụ đã khám phá. Vì vậy không khẳng định đã kiểm caller/callee toàn dự án; source chỉ được đọc trong diff đã khoanh vùng. Không đọc raw scene/prefab/meta YAML.
- [SCENE E:/Unity/Project/FPS/Assets/FPS/Scenes/MainMenu.unity/isDirty] Trước và sau kiểm tra, scene đang mở là **MainMenu**, `isDirty=false`. Có dùng Editor qua REST Unity-Skills: truy trạng thái, chạy **EditMode Test Runner**, và `File/Save Project` trước/sau test để thực hiện SaveAssets. Không vào Play Mode thủ công, không load GameScene để playtest, không authoring, không import, không mở hộp thoại Save Scene cho scene tạm.
- [SUY LUẬN] Đối chiếu SHA-256 của **241 file Assets tồn tại trong tập thay đổi/file mới** trước và sau thao tác: **0 khác biệt**. Tập này gồm scene, code, asset và meta; không gồm 8 đường dẫn đã bị xóa sẵn. Đây là kiểm tra byte trong tập đã snapshot, không phải chứng minh mọi file trên máy bất biến.
- [SUY LUẬN] Chỉ tạo báo cáo này ngoài Assets/. Giữ nguyên ảnh chứng cứ: lệnh trực tiếp “chỉ đọc, KHÔNG sửa gì” của vai ưu tiên hơn yêu cầu dọn ảnh sau review trong AGENTS.md:88.

### Skill đã đọc và quy tắc đã dùng

| SKILL.md đã đọc | Quy tắc cụ thể đã áp dụng |
|---|---|
| `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md` | [SUY LUẬN] Kiểm HUD nhận event thay vì đọc state theo frame; kiểm layout/độ đọc ở nhiều độ phân giải và mức scale. F05 đối chiếu polling; F06 đối chiếu chữ bị cắt. Không dùng skill để đổi theme trái mockup. |
| `C:/Users/hieud/.agents/skills/game-feel/SKILL.md` | [SUY LUẬN] Tách feedback theo sự kiện khỏi trang trí; chỉ xác nhận âm, nhịp/thời lượng, hủy và tác động input khi có quan sát tương ứng. Không suy tiếng/hiệu ứng/chuyển marker từ ảnh tĩnh. |
| `C:/Users/hieud/.codex/skills/level-design/SKILL.md` | [SUY LUẬN] Kiểm critical path, prerequisite, hướng dẫn vị trí và gating; phân biệt có timeout/retry trong code với bằng chứng thực sự thoát bế tắc. Không thay flow hay thêm object. |
| `C:/Users/hieud/.agents/skills/unity-skills/SKILL.md` | [SUY LUẬN] Khám phá health/schema/mode trước call; dùng đúng bề mặt Unity-Skills cho scene/Editor; không đoán API. |
| `C:/Users/hieud/.agents/skills/unity-skills/skills/scene/SKILL.md` | [SUY LUẬN] Query loaded scenes/isDirty; không mở hay lưu scene tạm. |
| `C:/Users/hieud/.agents/skills/unity-skills/skills/test/SKILL.md` | [SUY LUẬN] Test async bằng jobId, chạy tuần tự, lấy kết quả thật bằng test_get_result; không khởi chạy test thứ hai khi job cũ đang chạy. |
| `C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md` | [SUY LUẬN] Dùng đúng menu API cho Save Project và query trạng thái Editor. |

## Bảng kết quả chính

| Mục | Kết quả | Bằng chứng và giới hạn |
|---|---|---|
| A1 — Chuỗi đáp án | **FAIL** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:580] Có AL-04/02:40/K6 trong nhật ký; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset:888] có trong dialogue. Không thuộc file dữ liệu đáp án chỉ phía server. |
| A2 — Primitive/texture/màu/font/TMP | **FAIL** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:114] Tạo TMP 3D; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HUDManager.cs:995] cyan hard-code. Các match được phân loại phía dưới, không coi mọi TMP là world-space. |
| A3 — HUD polling | **FAIL** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:758] đọc device state theo frame; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:789] chọn hint theo state; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HUDManager.cs:398] đọc visibility ngoài ngoại lệ marker/khoảng cách/vòng giữ. |
| A4 — Server quyết định | **PASS-code; KHÔNG KIỂM ĐƯỢC runtime host/client** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:608] RPC server → CanUse/InReach → TryComplete; chưa có phiên host và client thực tế trong lượt này. |
| A5 — Editor test liên quan | **PASS: 80/80** | [SUY LUẬN] Ba job EditMode 54 + 17 + 9, raw result ở dưới. Không suy ra visual approval. |
| B — Ba câu từ từng ảnh | **FAIL** | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hud-1920x1080.png] không nêu phản hồi sẽ thấy sau hoàn thành; bảng từng ảnh ở dưới. |
| B — Khóa/vòng giữ/nhánh | **PASS-visual trong các mẫu nêu tên** | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-locked-1920x1080.png] lock + reason, không [F]; [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hold-1920x1080.png] vòng + [F]; [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hud-1920x1080.png] Power/Logistics + T SWITCH. |
| B — Scaling, 125%/4:3 | **FAIL** | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-missions-1920x1080-125.png] chữ bị cắt đáy; [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-files-1024x768.png] thiếu cuối tài liệu. |
| C — Quy tắc bắt buộc | **FAIL tổng hợp** | [SUY LUẬN] Ma trận từng điều khoản bên dưới; không đánh PASS-visual chỉ từ code. |
| D — Diff/ảnh đủ chứng minh cùng bản và cùng luồng | **KHÔNG KIỂM ĐƯỢC đầy đủ** | [SUY LUẬN] Tên file/thời gian ảnh không ràng buộc ảnh với hash working tree; thiếu state mới của Tank, sai, hủy giữ và ba hộp điện. |

## A. Kiểm tra bằng máy

### A1. Grep chuỗi đáp án

[SUY LUẬN] Chạy thật trên added lines C# của diff, toàn bộ 37 C# mới và catalog. Tách dòng thêm, dòng còn tồn tại, dữ liệu nguồn, dialogue và choice; không tính dòng đã xóa là lỗi hiện tại.

```powershell
$tokens = 'AL-04','K6','T9-17','P046','C12','02:40','E-02','03:05','03:50'
git diff HEAD -U0 -- '*.cs' |
  Select-String '^[+].*(AL-04|K6|T9-17|P046|C12|02:40|E-02|03:05|03:50)'
rg -n 'AL-04|K6|T9-17|P046|C12|02:40|E-02|03:05|03:50' -- Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset
rg -n '^    (hint|hint2|hint3):.*(AL-04|K6|T9-17|P046|C12|02:40|E-02|03:05|03:50)' -- Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset
```

[SUY LUẬN] Kết quả added lines C# có hai chuỗi trình bày ở CampaignHUD (nhật ký và fallback Complete) và một assert trong CampaignRulesTests. **Assert test không phải UI nên không FAIL.** 37 C# mới: 0 dòng khớp chín token. Dòng nhật ký sửa An Lac → Northbridge vẫn giữ bộ đáp án; không gọi toàn bộ nội dung nhật ký là mới xuất hiện.

```text
+            if(Campaign.State.Has(CampaignObjectiveId.FactoryShipping))evidence+="AL-04 · Northbridge · 02:40 · K6.\n";
+            {journal=true;ShowDocument();ClearControls();body.text="COLD LEDGER — COMPLETE\n\nThe freight route opened and the evidence copy was transmitted outside.\n\nT9-17 / C12 / P046 can no longer be erased from the record.\nThe zone remains quarantined; the investigation starts now.\n\nThe squad extracted with both sealed cases.";}
+            Assert.That(shipping.secondaryChoices[1], Is.EqualTo("NORTHBRIDGE / 02:40"));
```

[SUY LUẬN] Số **dòng khớp** trong catalog, không phải số lần xuất hiện và không phải mọi dòng đều là hint:

```text
AL-04: 15 matching lines
K6: 13 matching lines
T9-17: 28 matching lines
P046: 29 matching lines
C12: 29 matching lines
02:40: 15 matching lines
E-02: 17 matching lines
03:05: 6 matching lines
03:50: 6 matching lines
hint regex exit: 1
```

| Vị trí còn tồn tại | Loại | Phán định A1 |
|---|---|---|
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:579] `Primary objective: recover T9-17 evidence.` | Campaign Record/journal | **FAIL**; ảnh Record cũng thấy chữ này. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:580] AL-04/Northbridge/02:40/K6 | Journal, dòng thay đổi | **FAIL**. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:582] C12/AL-04/02:40; :583 P046; :584 P046/03:05/03:50; :585 E-02/T9-17/C12/P046 | Journal còn tồn tại | **FAIL** theo A1; không khẳng định tất cả mới thêm. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:838] T9-17/C12/P046 | Fallback panel Complete, dòng thêm | **FAIL-code**; ảnh Complete đang xem không hiện đoạn này, nên không kết luận nhánh fallback đã chạy. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:854] `Cross-check T9-17 / C12 / P046 at E...` | Objective text Laboratory, còn tồn tại | **FAIL-code**; chưa có ảnh Laboratory. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset:888] / :890; :900/:902; :912/:914; :936/:938; :948/:950; :970/:972 | Dialogue hai ngôn ngữ, có các token AL-04, K6, T9-17, C12, P046, 02:40, 03:05, 03:50, E-02 | **FAIL** theo lệnh A1, kể cả dialogue phát sau hoàn thành. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset:1204] / :1208; :1283/:1284; :1295; :1351 | Choices có các giá trị đáp án và lựa chọn nhiễu | **FAIL** theo nghĩa đen A1; tách khỏi spoiler tự đưa ra ở tracker/hint. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset:329] và các `body/summary/title` tài liệu như :253, :319 | Dữ kiện gốc trình bày trong panel | **FAIL theo A1**, nhưng có xung đột yêu cầu dữ kiện cạnh choices; xem tự kiểm mâu thuẫn. Không coi dữ kiện gốc là lời giải tự động. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset:1291] `The same reference is listed dead before its live transfer.` | Hint không có token trong regex | **FAIL về nội dung**: nêu kết luận đối chiếu, thay vì chỉ nguồn cần đọc. Zero-match token ở hint không đủ để PASS hint. |

[SUY LUẬN] Catalog đang được HUD truy xuất để trình bày; không phải ngoại lệ “file dữ liệu đáp án phía server”. Không suy lỗi từ tổng match của source facts; F01 còn có bằng chứng nhật ký/tracker độc lập với tranh luận này.

### A2. Grep tạo hình, màu, font và TMP

[SUY LUẬN] Đã chạy regex trên **added lines UI + CampaignHUD** và đọc file C# mới trong nhóm đó; sau đó mở rộng tới CampaignInteractable vì label 3D thuộc tiêu chí vai. Pattern:

```text
CreatePrimitive|Sprite\.Create|new Texture2D|whiteTexture|new Color(?:32)?\s*\(|font mặc định|TextMesh|WorldSpace
```

[SUY LUẬN] Kết quả đếm strict UI/CampaignHUD: `CreatePrimitive=0; Sprite.Create=0; new Texture2D=1; whiteTexture=0; new Color(...)=15; new Color32(...)=8; literal "font mặc định"=0; typeof(TextMeshPro)=0; WorldSpace=0`. Đây là đếm cú pháp literal trong diff, không chứng minh không có đường tạo hình qua helper hoặc canvas world-space ở nơi khác.

[SUY LUẬN] Output thật rút vào các match liên quan màu/texture; match TMP UGUI/font được phân loại ngay sau:

```text
Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs:185:panelImage.color = new Color(1, 1, 1, Theme.PanelAlpha);
Assets/FPS/Features/UI/Runtime/HUDManager.cs:995:graphic.color = new Color(.15f, 1f, .8f, 1f);
Assets/FPS/Features/UI/Runtime/HUDManager.cs:997:outline.effectColor = new Color(.02f, .06f, .07f, 1f);
Assets/FPS/Features/UI/Runtime/TacticalUiTheme.cs:19:public static readonly Color CampaignBackdrop = new Color(0f, 0f, 0f, .52f);
Assets/FPS/Features/UI/Runtime/TacticalUiTheme.cs:20:public static readonly Color DeviceBackdrop = new Color(0f, 0f, 0f, .90f);
Assets/FPS/Features/UI/Runtime/TacticalUiTheme.cs:21:public static readonly Color Selection = new Color(1f, 1f, 1f, .10f);
Assets/FPS/Features/UI/Runtime/TacticalUiTheme.cs:22:public static readonly Color SelectionFocus = new Color(1f, 1f, 1f, .16f);
Assets/FPS/Features/UI/Runtime/TacticalUiTheme.cs:23:public static readonly Color SelectionPressed = new Color(1f, 1f, 1f, .24f);
Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:502:image.color = new Color(1, 1, 1, Theme.PanelAlpha);
Assets/FPS/Features/UI/Editor/SelectedUiThemeInstaller.cs:41:var output = new Texture2D(512, 128, TextureFormat.RGBA32, false);
Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:12:public Color Text = new Color32(243, 243, 243, 255);
Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:13:public Color Muted = new Color32(174, 178, 178, 255);
Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:14:public Color Accent = new Color32(233, 166, 76, 255);
Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:15:public Color Danger = new Color32(216, 75, 75, 255);
Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:16:public Color Background = new Color32(20, 24, 24, 255);
Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:17:public Color Border = new Color32(98, 106, 101, 255);
Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:18:public Color Paper = new Color32(212, 216, 213, 255);
Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:19:public Color DeviceSurface = new Color32(32, 38, 38, 255);
Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:20:public Color CampaignBackdrop = new Color(0, 0, 0, .39f);
Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:21:public Color DeviceBackdrop = new Color(0, 0, 0, .83f);
Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:22:public Color Selection = new Color(1, 1, 1, .10f);
Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:23:public Color SelectionFocus = new Color(1, 1, 1, .16f);
Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:24:public Color SelectionPressed = new Color(1, 1, 1, .24f);
Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:38:image.pixelsPerUnitMultiplier = 1; image.color = new Color(1, 1, 1, alpha);
```

| Match / nhóm match | Lý do và quyết định |
|---|---|
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HUDManager.cs:995] / :997 | **FAIL**: reticle tint cyan `(.15,1,.8,1)` và outline `(.02,.06,.07,1)` nằm ngoài theme. KHÔNG TÌM THẤY ngoại lệ được duyệt trong phần mockup hợp lệ. Sprite nhập sẵn không làm tint này trở thành theme token. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs:185]; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:502] | **FAIL theo kiểm tra cứng A2/UI Standard**: trắng + alpha được tạo tại component ngoài theme; alpha có lấy Theme nhưng RGB vẫn viết cứng. Không gọi đây là sinh artwork mới. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/TacticalUiTheme.cs:19] / :20/:21/:22/:23 | **FAIL đối với chuẩn “one theme asset”**: token static ở theme khác vẫn tồn tại bên cạnh SelectedUiTheme. Cần hợp nhất hoặc có ngoại lệ rõ. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:12]–:19 | 8 field màu opaque mặc định ở đúng lớp theme. **Không FAIL chỉ vì constructor nằm trong theme**, nhưng số mặc định vượt bốn; KHÔNG KIỂM ĐƯỢC palette serialized thực dùng, không kết luận ảnh có đúng tám màu. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:20]–:24 và :38 | Token alpha và tint ở helper theme; lý do tập trung styling phù hợp phạm vi theme. Không gán các alpha này thành năm màu semantic mới. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Editor/SelectedUiThemeInstaller.cs:41] | Texture2D 512×128 dùng ReadPixels/encode ảnh preview model Warden rồi ghi Reports ở :43. **FAIL hồ sơ ngoại lệ theo A2** vì không tìm thấy lý do được duyệt; không cáo buộc nó vẽ runtime HUD bằng texture trắng. Không chạy installer này. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:350]; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs:235]; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HUDManager.cs:343]; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/SettingsUI.cs:42] / :130/:132 | **Không phải vi phạm TMP 3D**: các match là `TextMeshProUGUI`/RectTransform. Không đánh FAIL bằng substring “TextMesh”. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Editor/TacticalUiWorkshop.cs:539]; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Editor/SelectedUiThemeInstaller.cs:76] | Tham chiếu font asset `LiberationSans.ttf`; không có literal “font mặc định”. Có đường fallback font; chỉ từ tên file chưa đủ kết luận sai font approved. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:26] / :111/:114 | **FAIL**: `TextMeshPro` 3D và `new GameObject("CampaignWorldLabel", typeof(TextMeshPro))`. :122 đặt cao hơn target 1.25 m; :138 bật khi chưa complete, :140 quay theo camera. Đây là chữ lơ lửng, không phải UGUI. |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:109] | **FAIL hồ sơ ngoại lệ**: màu cyan/orange hard-code cho đèn trình bày target; không thấy ngoại lệ approved. Đây là light trong thế giới, không gộp vào số màu HUD thực dùng. |

### A3. Polling trong Update/LateUpdate

- **FAIL** [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:758]: khi không giữ và deviceSurface active, gọi `DeviceStateChanged()` rồi `RenderDocument()` theo frame. Đọc trạng thái nguồn/availability của device vượt ngoại lệ vòng giữ.
- **FAIL** [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:789]: `UpdateHints` chọn objectives theo `CampaignObjectiveSelection.CanInspect(Campaign.State,...)` trong nhịp kiểm 0.5 s, rồi :792 `hintProgress.Tick(Campaign.State,...)` mỗi frame. Timer có thể cần tick; việc đọc/lọc objective state vẫn là polling ngoài marker/khoảng cách/hold.
- **FAIL** [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HUDManager.cs:383] / :398/:399: LateUpdate gọi kiểm `HudLayout.GameplayVisible` và `CampaignHUD.Instance?.HasInteractionMessage` để bật/tắt prompt, không thuộc ba ngoại lệ.
- [SUY LUẬN] Không đánh lỗi LateUpdate chỉ vì tên method: phần đo canvas/resize chữ ở HUDManager:384 và layout-only trong CampaignStatusUI/SelectedUiTypography được loại khỏi lỗi state polling.

### A4. Server authority và thứ tự publish

- **PASS-code** [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:608]: `HoldRpc` là `SendTo.Server`; :611 lấy SenderClientId, :614 lấy actor, :617 kiểm `CampaignRules.CanUse`, :622 kiểm `InReach`. :650 gọi `TryComplete` khi hold đủ thời gian. Check phía client không thay chỗ quyết định hoàn thành ở server.
- **PASS-code** [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:659]: `Publish()` trước :660 `ObjectiveFeedbackRpc(..., state.revision)`; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:76] giữ feedback revision mới hơn snapshot và :82 chỉ phát Accepted khi objective đã complete.
- **KHÔNG KIỂM ĐƯỢC** [SUY LUẬN]: host/client thực, mất gói/trễ đồng bộ, client tự gửi request sai; không dùng code đọc được để tuyên bố runtime authority đạt tuyệt đối.

### A5. Editor tests đã chạy thật

[SUY LUẬN] Unity **6000.5.6f1**, EditMode, chạy tuần tự bằng `test_run_by_name(testName, testMode="EditMode")` rồi `test_get_result(jobId)`. Mỗi lượt kiểm scene clean và Save Project trước/sau. Không dùng PlayMode suite hoặc kết quả báo cáo tác giả.

| Filter | Job | Passed / Total | Failed / Skipped |
|---|---|---|---|
| FPS.Tests.CampaignRulesTests | c1301b86 | 54 / 54 | 0 / 0 |
| FPS.Tests.HudRegressionTests | 02e22f57 | 17 / 17 | 0 / 0 |
| FPS.Tests.SurvivalUiRegressionTests | 9240b545 | 9 / 9 | 0 / 0 |
| **Tổng** | | **80 / 80** | **0 / 0** |

[SUY LUẬN] Dán kết quả API thật:

```json
{"status":"success","result":{"success":true,"jobId":"c1301b86","status":"completed","totalTests":54,"passedTests":54,"failedTests":0,"skippedTests":0,"inconclusiveTests":0,"otherTests":0,"failedTestNames":[],"failedTestDetails":[],"elapsedSeconds":657,"resultSummary":"Test run completed: 54/54 passed.","error":null}}
{"status":"success","result":{"success":true,"jobId":"02e22f57","status":"completed","totalTests":17,"passedTests":17,"failedTests":0,"skippedTests":0,"inconclusiveTests":0,"otherTests":0,"failedTestNames":[],"failedTestDetails":[],"elapsedSeconds":562,"resultSummary":"Test run completed: 17/17 passed.","error":null}}
{"status":"success","result":{"success":true,"jobId":"9240b545","status":"completed","totalTests":9,"passedTests":9,"failedTests":0,"skippedTests":0,"inconclusiveTests":0,"otherTests":0,"failedTestNames":[],"failedTestDetails":[],"elapsedSeconds":230,"resultSummary":"Test run completed: 9/9 passed.","error":null}}
```

[SUY LUẬN] `elapsedSeconds` là trường nguyên văn của API khi poll; không dùng nó làm phép đo thời lượng chạy vì các lần poll sau completion trả số tăng. Thời lượng âm, hold, subtitle không được đo bởi ba kết quả này.

[SUY LUẬN] Không chạy thêm HudAssetReplacementTests: test `RuntimeFontsResolveAndContainCampaignGlyphs` gọi `HasCharacters(..., tryAddCharacter:true)`, có khả năng thêm glyph vào font asset; tránh tác động asset trong vai chỉ đọc. Hai test khác trong suite này cũng chưa chạy. Không chạy lại các suite weapon/AI/save ngoài đánh giá guidance/UI của vai.

## B. Kiểm từng ảnh, chỉ từ ảnh

[SUY LUẬN] Đã xem **62 ảnh chứng cứ**: 39 After mới, 4 Before cùng thư mục đợt mới, 14 Before lịch sử và 5 Tank Review lịch sử. Đã xem riêng từng ảnh, không dùng contact sheet để thay thế. Bộ After mang mốc file 10/11/2026 khoảng 10:03–10:07 +07; Before Bot6 05/11 và Tank 06/11. Metadata không chứng minh ảnh đúng build hiện tại. Các ảnh mockup đối chiếu được liệt kê riêng, không tính thành playtest.

[SUY LUẬN] Quy tắc chấm: Q1 hành động tiếp; Q2 vị trí; Q3 phản hồi/kết quả sau làm. “KHÔNG TÌM THẤY” nghĩa là không nhìn thấy trong ảnh đó, không khẳng định chức năng không tồn tại. Với menu inventory, chấm hướng dẫn nhiệm vụ tiếp theo theo yêu cầu vai, không thay Q1 bằng thao tác chung “chọn một dòng”. PASS ba câu không đồng nghĩa PASS token/visual toàn phần.

### B1. 27 ảnh Selected After

| Ảnh | Q1: làm gì tiếp? | Q2: ở đâu? | Q3: xong thấy gì? | Kết quả |
|---|---|---|---|---|
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hud-1024x768.png] | VERIFY SHIPPING RELEASE | Marker bên phải, 388 m, HIGHER; không thấy thân target | KHÔNG TÌM THẤY phản hồi dự kiến | FAIL Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hud-1920x1080.png] | VERIFY SHIPPING RELEASE | Marker bên phải, 388 m, HIGHER; không thấy thân target | KHÔNG TÌM THẤY phản hồi dự kiến | FAIL Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hud-3840x2160.png] | VERIFY SHIPPING RELEASE | Marker bên phải, 388 m, HIGHER; không thấy thân target | KHÔNG TÌM THẤY phản hồi dự kiến | FAIL Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-locked-1024x768.png] | Hoàn thành Power / Isolate trước Backup | KHÔNG TÌM THẤY vị trí Isolate | Chỉ có This enables Generator cho Backup, chưa nêu kết quả Isolate | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-locked-1920x1080.png] | Hoàn thành Power / Isolate trước Backup | KHÔNG TÌM THẤY vị trí Isolate | Chỉ có This enables Generator cho Backup, chưa nêu kết quả Isolate | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-locked-3840x2160.png] | Hoàn thành Power / Isolate trước Backup | KHÔNG TÌM THẤY vị trí Isolate | Chỉ có This enables Generator cho Backup, chưa nêu kết quả Isolate | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hold-1024x768.png] | Giữ [F] HOLD TO CONFIRM | Pane CONTROL / ISOLATE bên phải | KHÔNG TÌM THẤY trạng thái/marker sau hoàn thành | FAIL Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hold-1920x1080.png] | Giữ [F] HOLD TO CONFIRM | Pane CONTROL / ISOLATE bên phải | KHÔNG TÌM THẤY trạng thái/marker sau hoàn thành | FAIL Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hold-3840x2160.png] | Giữ [F] HOLD TO CONFIRM | Pane CONTROL / ISOLATE bên phải | KHÔNG TÌM THẤY trạng thái/marker sau hoàn thành | FAIL Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-manifest-1024x768.png] | Đọc Delivery Manifest | Trang record giữa màn | FILE ADDED TO INVENTORY ở dưới phải | PASS 3 câu; FAIL A1 riêng |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-manifest-1920x1080.png] | Đọc Delivery Manifest | Trang record giữa màn | FILE ADDED TO INVENTORY ở dưới phải | PASS 3 câu; FAIL A1 riêng |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-manifest-3840x2160.png] | Đọc Delivery Manifest | Trang record giữa màn | FILE ADDED TO INVENTORY ở dưới phải | PASS 3 câu; FAIL A1 riêng |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-campaign-record-1024x768.png] | Theo mục tiêu recover T9-17 evidence | KHÔNG TÌM THẤY vị trí nhiệm vụ | KHÔNG TÌM THẤY phản hồi sau thao tác | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-campaign-record-1920x1080.png] | Theo mục tiêu recover T9-17 evidence | KHÔNG TÌM THẤY vị trí nhiệm vụ | KHÔNG TÌM THẤY phản hồi sau thao tác | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-campaign-record-3840x2160.png] | Theo mục tiêu recover T9-17 evidence | KHÔNG TÌM THẤY vị trí nhiệm vụ | KHÔNG TÌM THẤY phản hồi sau thao tác | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-files-1024x768.png] | Đọc Delivery Manifest/Campaign Record trong Files | Danh sách trái, tài liệu phải; chưa chỉ đích ngoài thế giới | KHÔNG TÌM THẤY phản hồi nhiệm vụ sau đọc lại | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-files-1920x1080.png] | Đọc Delivery Manifest/Campaign Record trong Files | Danh sách trái, tài liệu phải; chưa chỉ đích ngoài thế giới | KHÔNG TÌM THẤY phản hồi nhiệm vụ sau đọc lại | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-files-3840x2160.png] | Đọc Delivery Manifest/Campaign Record trong Files | Danh sách trái, tài liệu phải; chưa chỉ đích ngoài thế giới | KHÔNG TÌM THẤY phản hồi nhiệm vụ sau đọc lại | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-missions-1024x768.png] | Theo Verify Shipping Release; so hai nhóm với manifest | KHÔNG TÌM THẤY vị trí thân thiết bị trong thế giới | KHÔNG TÌM THẤY phản hồi xác nhận dự kiến | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-missions-1920x1080.png] | Theo Verify Shipping Release; so hai nhóm với manifest | KHÔNG TÌM THẤY vị trí thân thiết bị trong thế giới | KHÔNG TÌM THẤY phản hồi xác nhận dự kiến | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-missions-3840x2160.png] | Theo Verify Shipping Release; so hai nhóm với manifest | KHÔNG TÌM THẤY vị trí thân thiết bị trong thế giới | KHÔNG TÌM THẤY phản hồi xác nhận dự kiến | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-complete-1024x768.png] | KHÔNG TÌM THẤY điều khiển tiếp | KHÔNG TÌM THẤY | Chỉ Mission complete. / END OF SUMMARY | FAIL Q1/Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-complete-1920x1080.png] | KHÔNG TÌM THẤY điều khiển tiếp | KHÔNG TÌM THẤY | Chỉ Mission complete. / END OF SUMMARY | FAIL Q1/Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-complete-3840x2160.png] | KHÔNG TÌM THẤY điều khiển tiếp | KHÔNG TÌM THẤY | Chỉ Mission complete. / END OF SUMMARY | FAIL Q1/Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-failed-1024x768.png] | RETRY CHECKPOINT | Nút dưới phải panel | Return to the last checkpoint. | PASS 3 câu |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-failed-1920x1080.png] | RETRY CHECKPOINT | Nút dưới phải panel | Return to the last checkpoint. | PASS 3 câu |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-failed-3840x2160.png] | RETRY CHECKPOINT | Nút dưới phải panel | Return to the last checkpoint. | PASS 3 câu |

### B2. 12 ảnh Three-tabs After, 100% và 125%

| Ảnh | Q1: làm gì tiếp? | Q2: ở đâu? | Q3: xong thấy gì? | Kết quả |
|---|---|---|---|---|
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-record-1024x768-100.png] | Primary objective: recover T9-17 evidence. | KHÔNG TÌM THẤY vị trí thực của bước tiếp | KHÔNG TÌM THẤY phản hồi sau hoàn thành | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-record-1024x768-125.png] | Cold Ledger: restore power/reconcile logistics theo subtitle; chưa có chỉ dẫn cụ thể hơn trong phần record thấy được | KHÔNG TÌM THẤY vị trí thực của bước tiếp | KHÔNG TÌM THẤY phản hồi sau hoàn thành | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-record-1920x1080-100.png] | Primary objective: recover T9-17 evidence. | KHÔNG TÌM THẤY vị trí thực của bước tiếp | KHÔNG TÌM THẤY phản hồi sau hoàn thành | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-record-1920x1080-125.png] | Cold Ledger: restore power/reconcile logistics theo subtitle; chưa có chỉ dẫn cụ thể hơn trong phần record thấy được | KHÔNG TÌM THẤY vị trí thực của bước tiếp | KHÔNG TÌM THẤY phản hồi sau hoàn thành | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-record-3840x2160-100.png] | Primary objective: recover T9-17 evidence. | KHÔNG TÌM THẤY vị trí thực của bước tiếp | KHÔNG TÌM THẤY phản hồi sau hoàn thành | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-record-3840x2160-125.png] | Cold Ledger: restore power/reconcile logistics theo subtitle; chưa có chỉ dẫn cụ thể hơn trong phần record thấy được | KHÔNG TÌM THẤY vị trí thực của bước tiếp | KHÔNG TÌM THẤY phản hồi sau hoàn thành | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-missions-1024x768-100.png] | Backup Cold Store có AVAILABLE; Start Generator đang LOCKED | KHÔNG TÌM THẤY vị trí thực của bước tiếp | KHÔNG TÌM THẤY phản hồi sau hoàn thành | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-missions-1024x768-125.png] | Backup Cold Store có AVAILABLE; Start Generator đang LOCKED | KHÔNG TÌM THẤY vị trí thực của bước tiếp | KHÔNG TÌM THẤY phản hồi sau hoàn thành | FAIL Q2/Q3; thêm cắt chữ |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-missions-1920x1080-100.png] | Backup Cold Store có AVAILABLE; Start Generator đang LOCKED | KHÔNG TÌM THẤY vị trí thực của bước tiếp | KHÔNG TÌM THẤY phản hồi sau hoàn thành | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-missions-1920x1080-125.png] | Backup Cold Store có AVAILABLE; Start Generator đang LOCKED | KHÔNG TÌM THẤY vị trí thực của bước tiếp | KHÔNG TÌM THẤY phản hồi sau hoàn thành | FAIL Q2/Q3; thêm cắt chữ |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-missions-3840x2160-100.png] | Backup Cold Store có AVAILABLE; Start Generator đang LOCKED | KHÔNG TÌM THẤY vị trí thực của bước tiếp | KHÔNG TÌM THẤY phản hồi sau hoàn thành | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-missions-3840x2160-125.png] | Backup Cold Store có AVAILABLE; Start Generator đang LOCKED | KHÔNG TÌM THẤY vị trí thực của bước tiếp | KHÔNG TÌM THẤY phản hồi sau hoàn thành | FAIL Q2/Q3; thêm cắt chữ |

### B3. 4 ảnh Before cùng đợt inventory

| Ảnh | Q1: làm gì tiếp? | Q2: ở đâu? | Q3: xong thấy gì? | Kết quả |
|---|---|---|---|---|
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/Before/inventory-file-hierarchy-1920x1080.png] | Đọc Delivery Manifest trong Files | Tài liệu trong pane phải, chưa chỉ đích thế giới | KHÔNG TÌM THẤY phản hồi bước tiếp | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/Before/inventory-file-hierarchy-3840x2160.png] | Đọc Delivery Manifest trong Files | Tài liệu trong pane phải, chưa chỉ đích thế giới | KHÔNG TÌM THẤY phản hồi bước tiếp | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/Before/inventory-missions-hierarchy-1920x1080.png] | VERIFY SHIPPING RELEASE; mô tả lặp tên mục tiêu | KHÔNG TÌM THẤY vị trí thiết bị | KHÔNG TÌM THẤY phản hồi bước tiếp | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/Before/inventory-missions-hierarchy-3840x2160.png] | VERIFY SHIPPING RELEASE; mô tả lặp tên mục tiêu | KHÔNG TÌM THẤY vị trí thiết bị | KHÔNG TÌM THẤY phản hồi bước tiếp | FAIL Q2/Q3 |

### B4. 14 ảnh Before lịch sử Bot6

| Ảnh | Q1: làm gì tiếp? | Q2: ở đâu? | Q3: xong thấy gì? | Kết quả |
|---|---|---|---|---|
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/selected-hud-1920x1080.png] | VERIFY SHIPPING RELEASE | Marker bên phải, 388 m, HIGHER; không thấy thân target | KHÔNG TÌM THẤY phản hồi dự kiến | FAIL Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/selected-hud-3840x2160.png] | VERIFY SHIPPING RELEASE | Marker bên phải, 388 m, HIGHER; không thấy thân target | KHÔNG TÌM THẤY phản hồi dự kiến | FAIL Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/selected-locked-1920x1080.png] | Hoàn thành Power / Isolate trước Backup | KHÔNG TÌM THẤY vị trí Isolate | Chỉ có This enables Generator cho Backup, chưa nêu kết quả Isolate | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/selected-locked-3840x2160.png] | Hoàn thành Power / Isolate trước Backup | KHÔNG TÌM THẤY vị trí Isolate | Chỉ có This enables Generator cho Backup, chưa nêu kết quả Isolate | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/selected-hold-1920x1080.png] | Giữ [F] HOLD TO CONFIRM | Pane CONTROL / ISOLATE bên phải | KHÔNG TÌM THẤY trạng thái/marker sau hoàn thành | FAIL Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/selected-hold-3840x2160.png] | Giữ [F] HOLD TO CONFIRM | Pane CONTROL / ISOLATE bên phải | KHÔNG TÌM THẤY trạng thái/marker sau hoàn thành | FAIL Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/selected-manifest-1920x1080.png] | Đọc Delivery Manifest | Trang record giữa màn | FILE ADDED TO INVENTORY ở dưới phải | PASS 3 câu; FAIL A1 riêng |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/selected-manifest-3840x2160.png] | Đọc Delivery Manifest | Trang record giữa màn | FILE ADDED TO INVENTORY ở dưới phải | PASS 3 câu; FAIL A1 riêng |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/selected-complete-1920x1080.png] | KHÔNG TÌM THẤY điều khiển tiếp | KHÔNG TÌM THẤY | Chỉ Mission complete. / END OF SUMMARY | FAIL Q1/Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/selected-complete-3840x2160.png] | KHÔNG TÌM THẤY điều khiển tiếp | KHÔNG TÌM THẤY | Chỉ Mission complete. / END OF SUMMARY | FAIL Q1/Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/selected-failed-1920x1080.png] | RETRY CHECKPOINT | Nút dưới phải panel | Return to the last checkpoint. | PASS 3 câu |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/selected-failed-3840x2160.png] | RETRY CHECKPOINT | Nút dưới phải panel | Return to the last checkpoint. | PASS 3 câu |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/bot6-shipping-before-1920x1080.png] | So manifest, chọn hai nhóm, HOLD F TO CONFIRM | Hai hàng lựa chọn đáy panel | Chỉ verify Logistics; KHÔNG TÌM THẤY phản hồi hoàn thành | FAIL Q3; FAIL A1 riêng |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/bot6-shipping-before-3840x2160.png] | So manifest, chọn hai nhóm, HOLD F TO CONFIRM | Hai hàng lựa chọn đáy panel | Chỉ verify Logistics; KHÔNG TÌM THẤY phản hồi hoàn thành | FAIL Q3; FAIL A1 riêng |

### B5. 5 ảnh Tank Review lịch sử

| Ảnh | Q1: làm gì tiếp? | Q2: ở đâu? | Q3: xong thấy gì? | Kết quả |
|---|---|---|---|---|
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Review/journal-tank-active.png] | Press I to close Journal and face the Tank; NEXT Defeat the Tank | KHÔNG TÌM THẤY vị trí Tank | KHÔNG TÌM THẤY phản hồi đánh bại | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Review/journal-tank-client-failed.png] | Wait for host to retry Tank deployment | KHÔNG TÌM THẤY vị trí Tank/đích tiếp | Power 3 of 3 · Logistics 2 of 2 retained; chưa nêu kết quả lần retry | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Review/journal-tank-host-failed.png] | Retry Tank deployment | Nút RETRY TANK bên phải khối trạng thái | KHÔNG TÌM THẤY trạng thái sẽ hiện sau retry | FAIL Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Review/journal-tank-pending.png] | Wait for Tank deployment | KHÔNG TÌM THẤY đích spawn | No safe position available. Checking... 08s attempt budget.; chưa nêu sau hết 08s | FAIL Q2/Q3 |
| [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Review/journal-tank-reading-grace.png] | Wait for Tank deployment | KHÔNG TÌM THẤY đích spawn | Reading in progress. 08s attempt budget paused.; chưa nêu kết quả tiếp | FAIL Q2/Q3 |

[SUY LUẬN] Bảng Tank không xác nhận timeout, nút retry hoặc dừng bộ đếm thực thi. Chữ “08s” là nội dung thấy trong ảnh lịch sử, không phải phép đo 8 giây trong lượt này.

### B6. Tiêu chí hình ảnh cụ thể và đối chiếu mockup

| Thành phần | Điều nhìn thấy và đối chiếu | Kết quả |
|---|---|---|
| HUD/tracker | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hud-1920x1080.png] TRACKING ở góc trên trái, VERIFY SHIPPING RELEASE; Power 0/3, mũi tên Logistics 1/2, T SWITCH. Khớp vị trí tương đối [04-hud-selected.png](E:/Unity/Project/FPS/Reports/mockups/04-hud-selected.png). | **PASS-visual** hai nhánh + dấu active + key trong mẫu này; Q3 vẫn FAIL. |
| Vitals/loadout | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hud-3840x2160.png] một khối health/infection dưới trái; đồ dùng và hai súng trong một cụm dưới phải, theo mockup HUD. | **PASS-visual** bố cục ở mẫu HUD. Không xác nhận cập nhật trị số runtime. |
| Marker ngoài viewmodel | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hud-1920x1080.png] marker phía phải ghi 388 m/HIGHER, nằm phía trên silhouette súng. | **PASS-visual vị trí màn hình mẫu này**; KHÔNG KIỂM ĐƯỢC trỏ đúng thân collider, mọi khoảng cách/góc nhìn hoặc fallback NavMesh. |
| Locked device | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-locked-1920x1080.png] Backup Cold Store, lock icon + Requires: complete Power / Isolate; nút LOCKED, không [F]. So [04-device-locked.png](E:/Unity/Project/FPS/Reports/mockups/04-device-locked.png). | **PASS-visual khóa**; vị trí Isolate chưa được chỉ trong ảnh. |
| Hold device | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hold-3840x2160.png] vòng quanh [F], nút cam, RELEASE EARLY TO CANCEL/RELEASE TO CANCEL. So [04-device-hold.png](E:/Unity/Project/FPS/Reports/mockups/04-device-hold.png). | **PASS-visual vòng + phím**; KHÔNG KIỂM ĐƯỢC hủy sớm, thời gian giữ, âm hoặc completion pulse. |
| Source ở Hold | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hold-1920x1080.png] SOURCE NOT RECORDED đồng thời ACTION IN PROGRESS; mockup Hold ghi Source READY. | **FAIL đối chiếu chữ trạng thái**, F09; chưa suy vi phạm server prerequisite vì Isolate có thể không đòi tài liệu. |
| Manifest | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-manifest-1920x1080.png] trang Delivery Manifest và khối metadata/ack FILE ADDED TO INVENTORY, close I; so [04-device-manifest.png](E:/Unity/Project/FPS/Reports/mockups/04-device-manifest.png). | **PASS-visual cấu trúc/ack**; **FAIL A1** vì nội dung AL-04/K6/T9-17/02:40/C12 trong panel. |
| Files/Record | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-files-1920x1080.png] ba tab KEY ITEMS/FILES/MISSIONS; Delivery Manifest và Campaign Record ở list trái, nội dung phải; so [04-campaign-files.png](E:/Unity/Project/FPS/Reports/mockups/04-campaign-files.png). | **PASS-visual cấu trúc**. [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-files-1024x768.png] phần cuối body bị mask cắt, không thấy cue cuộn ở mép dưới: **FAIL F06**. Không suy rằng không có ScrollRect. |
| Missions | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-missions-1920x1080.png] list trạng thái trái, chi tiết Verify Shipping Release phải; so [04-campaign-missions.png](E:/Unity/Project/FPS/Reports/mockups/04-campaign-missions.png). Sau thay mô tả lặp bằng chỉ dẫn so hai nhóm manifest. | **PASS-visual cấu trúc ở 100%**; thiếu vị trí/Q3. |
| Missions 125% | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-missions-1920x1080-125.png] dòng LOCKED · START GENERATOR ở pane phải bị cắt qua chữ; dòng Verify Shipping Release/TRACKING không nằm trong phần list nhìn thấy. Cùng dạng cắt ở 1024 và 4K/125%. | **FAIL F06**. Không thấy scrollbar/cue cuộn trong ảnh, không kết luận chức năng cuộn bị xóa. |
| Subtitle | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-record-1920x1080-100.png] Mira Shaw: Cold Ledger. Restore cold-store power and reconcile the logistics release. Both branches are required; take them in either order. Chữ trắng có viền đen ở dưới panel; 100% 1080/4K một dòng, 125% hai dòng. | **PASS-visual độ đọc trong 12 ảnh Three-tabs**; 1024 dùng hai dòng. Không đo thời gian lưu subtitle, không xác nhận có voice. |
| Complete | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-complete-1920x1080.png] Mission complete., TIME 00:35, 1/1, DOWNS 0, ELIMINATED 0, FILES 0/12, CASES NOT SECURED, END OF SUMMARY; khớp cấu trúc [04-campaign-complete.png](E:/Unity/Project/FPS/Reports/mockups/04-campaign-complete.png). | **PASS-visual cấu trúc**, **FAIL điều khiển tiếp F08**. Mockup cũng thiếu điều khiển; không kết luận game cho hoàn tất sai từ dữ liệu fixture này. |
| Failed | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-failed-1920x1080.png] Mission failed. màu đỏ; RETRY CHECKPOINT dưới phải, Return to the last checkpoint.; so [04-campaign-failed.png](E:/Unity/Project/FPS/Reports/mockups/04-campaign-failed.png). | **PASS-visual cấu trúc và ba câu**; chưa bấm retry. |
| Chữ lơ lửng | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:114] code tạo CampaignWorldLabel. [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hud-1920x1080.png] ảnh ở xa không cho thấy chữ đó. | **FAIL-code**; **KHÔNG KIỂM ĐƯỢC visual** đã hết chữ trên tất cả object. Không dùng “ảnh không thấy” để phủ định code. |
| Ba hộp điện | [SUY LUẬN] KHÔNG TÌM THẤY bộ ảnh cùng góc/tiếp cận thể hiện cả ba thân object và dấu phân biệt trước khi aim. Các panel Isolate/Backup không đủ chứng minh. | **KHÔNG KIỂM ĐƯỢC** viền/đèn/nhãn thật và phân biệt ba vật. |
| Phản hồi sai | [SUY LUẬN] KHÔNG TÌM THẤY ảnh After của sai từng nhóm Shipment/Seal và Destination/Receiving time. | **KHÔNG KIỂM ĐƯỢC** lời báo sai thực nhìn thấy và vị trí/thời lượng. |

## C. Đối chiếu từng điều khoản AGENTS.md

[SUY LUẬN] PASS ở đây chỉ áp dụng đúng bằng chứng/phạm vi ghi trong ô. Một ảnh không đủ PASS mọi chapter, mọi object hay mọi runtime state.

### Quest & Player-Guidance Rules

| Điều khoản | Kết quả | Bằng chứng |
|---|---|---|
| 1. 5-second test: làm gì/ở đâu/kết quả | **FAIL** | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hud-1920x1080.png] có Verify Shipping Release và marker nhưng không cho Q3; các bảng B chấm từng ảnh. |
| 2. Locked có reason | **PASS-visual mẫu Backup** | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-locked-1920x1080.png] lock + Requires: complete Power / Isolate, không [F]. Chưa kiểm tất cả locked object. |
| 3. Complete có text+sound+visual, chuyển waypoint sau publish | **KHÔNG KIỂM ĐƯỢC đầy đủ** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:659] publish trước feedback PASS-code. [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-manifest-1920x1080.png] chỉ xác nhận FILE ADDED TO INVENTORY. KHÔNG TÌM THẤY chuỗi ảnh/âm của complete step → marker mới. |
| 4. Đọc dữ kiện trước choice panel; dữ kiện cạnh choice | **FAIL-code** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:399] chỉ chặn FactoryShipping. :436 kiểm HasFile để hiện body; :509 vẫn AddChoices, :525 không ràng buộc HasFile; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:422] / :425 không đòi file ở AsylumPatient/LabArchive. Chưa tái hiện runtime. |
| 5. Không đáp án trong tracker/hint/panel; hint chỉ nguồn | **FAIL** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:579] / :580 / :854; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset:1291] hint nêu kết luận; ảnh Record thấy T9-17. |
| 6. Tracker đủ nhánh/song song/active/key | **PASS-visual Factory mẫu** | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hud-1920x1080.png] Power 0/3, → Logistics 1/2, T SWITCH. Chưa bấm đổi nhánh. |
| 7. Waypoint đúng thân, tên+khoảng cách, mọi range/fallback | **KHÔNG KIỂM ĐƯỢC đầy đủ** | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hud-1920x1080.png] tên/388 m/HIGHER nhìn thấy, thân target ngoài ảnh. Không đo tuyến hoặc kiểm NavMesh thất bại. |
| 8. Vòng giữ+key; nhả sớm hủy thấy được | **KHÔNG KIỂM ĐƯỢC đầy đủ** | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hold-1920x1080.png] vòng+[F]+copy cancel PASS-visual; không có ảnh/clip sau nhả sớm. |
| 9. Gating có timeout/fallback chống soft-lock | **KHÔNG KIỂM ĐƯỢC runtime** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignMissionController.Tank.cs:93] timeout → SpawnFailed; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:418] Factory vẫn cần factoryTankDefeated. Ảnh retry là lịch sử; chưa kiểm khi mọi lần spawn đều thất bại. |
| 10. Đo khoảng cách đi bộ NavMesh giữa objectives | **KHÔNG KIỂM ĐƯỢC** | [SUY LUẬN] KHÔNG TÌM THẤY số đo trong input được phép. 388 m trên marker không phải bằng chứng số đo đường đi NavMesh giữa hai objective liên tiếp. |

### Asset & Art Rules

| Điều khoản | Kết quả | Bằng chứng |
|---|---|---|
| 1. Inspect Assets trước; tái dùng asset | **KHÔNG KIỂM ĐƯỢC trình tự** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HUDManager.cs:994] dùng HudSkin.Get("ADSReflexDot"); [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:29] có sprite references. Diff không chứng minh đã khảo sát Assets trước khi tạo art mới. |
| 2. Không vẽ frame/icon/prop bằng code/primitives/flat color; greybox có nhãn | **KHÔNG KIỂM ĐƯỢC toàn bộ asset** | [SUY LUẬN] Grep diff UI: zero CreatePrimitive/Sprite.Create/whiteTexture; Texture2D ở installer là capture preview, không đủ kết luận procedural runtime frame. Chưa kiểm nguồn toàn bộ sprite mới. A2 vẫn FAIL vì màu/ngoại lệ riêng. |
| 3. Không imagegen nếu chưa duyệt | **KHÔNG KIỂM ĐƯỢC** | [SUY LUẬN] Không có lịch sử công cụ tạo asset của tác giả trong input hợp lệ; không suy đã/không gọi imagegen từ PNG. |
| 4. Không chữ 3D lơ lửng | **FAIL-code** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:114] tạo TextMeshPro; :122 cao hơn object 1.25 m, :140 billboard camera. |
| 5. Mỗi asset mới có nguồn/license | **KHÔNG KIỂM ĐƯỢC** | [SUY LUẬN] KHÔNG TÌM THẤY hồ sơ nguồn/license đáng tin cho từng asset mới trong tập input được phép; không đọc báo cáo asset của bot làm để bù. |

### UI Visual Standard

| Điều khoản | Kết quả | Bằng chứng |
|---|---|---|
| 1. Một theme asset, ≤4 màu, font/size/panel chuẩn, không hard-code ngoài theme | **FAIL** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HUDManager.cs:995] / :997 cyan hard-code; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/TacticalUiTheme.cs:19] thêm token static bên cạnh SelectedTheme. [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs:25] ba size 18/24/36 và :37 sliced image là phần phù hợp; không xóa lỗi còn lại. |
| 2. Chỉ info cần lúc đó; merge health/infection | **KHÔNG KIỂM ĐƯỢC mọi state; PASS-visual phần merge** | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hud-1920x1080.png] một cụm bar health/infection. Bộ ảnh không phủ mọi state để kết luận toàn bộ thông tin đều cần. |
| 3. Duyệt mockup trước code | **KHÔNG KIỂM ĐƯỢC trình tự duyệt** | [SUY LUẬN] Người dùng chỉ định Reports/04-hud-design.md là mockup đã duyệt, nên dùng hình làm chuẩn. Không có mốc phê duyệt người dùng độc lập đối với từng thay đổi trong cả working tree; không dùng nhật ký tác giả làm chứng lịch sử. |

### Evidence & Done Criteria

| Điều khoản | Kết quả | Bằng chứng |
|---|---|---|
| 1. Gắn nhãn claim | **PASS cho báo cáo Bot7** | [SUY LUẬN] Kết luận gắn CODE/SCENE/PLAYTEST/SUY LUẬN; ảnh tĩnh không được nâng thành âm/thời gian/runtime. Không đánh giá nhãn ở báo cáo bot khác vì không đọc. |
| 2. Before/After Game view 1080p+4K và playtest bước đổi | **FAIL hồ sơ Done toàn đợt** | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Before/bot6-shipping-before-1920x1080.png] Before có; [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hold-3840x2160.png] After có nhưng thiếu chuỗi bước thành công/sai/hủy/marker kế, Tank mới, ba hộp điện và host/client; không liên kết với hash diff. |
| 3. Scene isDirty=false; dọn ảnh theo yêu cầu | **PASS trạng thái scene; không dọn trong vai chỉ đọc** | [SCENE E:/Unity/Project/FPS/Assets/FPS/Scenes/MainMenu.unity/isDirty] cuối lượt false. [SUY LUẬN] Chỉ dẫn trực tiếp vai cấm sửa/xóa được ưu tiên, nên giữ ảnh làm chứng cứ. |

[SUY LUẬN] Ngoài bốn nhóm bắt buộc: quy tắc Networking & Authority đạt phần đọc code ở A4 nhưng host/client chưa kiểm; quy tắc Tests có ba nhóm Editor thật đạt, không phải chứng minh test cho mọi thay đổi AI/weapon/save.

## D. Mâu thuẫn hoặc khoảng trống diff–ảnh

| Đối chiếu | Kết luận |
|---|---|
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:114] tạo world label, ảnh HUD không thấy label | **Khoảng trống chứng cứ**, không kết luận code không chạy hoặc label đã hết. Cần ảnh gần target chưa complete. [SUY LUẬN] |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:659] publish trước feedback; ảnh không có bước marker kế | **PASS thứ tự code**, **KHÔNG KIỂM ĐƯỢC** chuyển marker thực và feedback text/âm/hình. [SUY LUẬN] |
| [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:838] fallback Complete có đoạn T9-17/C12/P046; [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-complete-1920x1080.png] chỉ có summary | Hai đường trình bày có thể khác nhau. **Không gọi thiếu đoạn text trong ảnh là bug** khi chưa xác định nhánh đang dùng. [SUY LUẬN] |
| Mockup Hold Source READY; [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hold-1920x1080.png] Source NOT RECORDED khi ACTION IN PROGRESS | **Lệch chữ trạng thái nhìn thấy**, F09; không tự suy server chấp nhận thiếu dữ kiện. [SUY LUẬN] |
| Tank lịch sử bốn tab, latest ba tab/Campaign Record dưới Files | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/Bot6Implementation-2026-11-05/Review/journal-tank-host-failed.png] có RETRY TANK, [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-campaign-record-1920x1080.png] không ở state Tank fail. **KHÔNG KIỂM ĐƯỢC** control retry đã được đưa vào luồng mới hay bị mất; không nói đã bị xóa. |
| Before/After Files và Missions | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/Before/inventory-missions-hierarchy-1920x1080.png] bốn tab; [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-missions-1920x1080.png] ba tab, chỉ dẫn Shipping chi tiết hơn. Đây là cải thiện nhìn thấy, không khắc phục F01/F06/F07. |
| TIME/NEW/EVIDENCE khác giữa ảnh | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-files-1920x1080.png] NEW khác EVIDENCE ở 4K; Complete TIME 00:35/00:38. **Chỉ ghi khác state ảnh**, không kết luận lỗi sync hoặc lưu game. [SUY LUẬN] |

## FAIL theo mức độ và đề xuất sửa

[SUY LUẬN] Chưa có BLOCKER được tái hiện. Có **7 MAJOR**, **2 MINOR** dưới đây. Mức độ xếp theo vi phạm yêu cầu và tác động tới đọc/hướng dẫn; không tuyên bố crash/soft-lock khi chưa chạy luồng.

| ID / mức | FAIL có bằng chứng | Đề xuất sửa (không thực hiện trong vai này) |
|---|---|---|
| **F01 — MAJOR** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:580] journal có bộ AL-04/02:40/K6; :579 T9-17, :854 tracker Laboratory; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset:1291] hint nêu kết luận. [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-campaign-record-1920x1080.png] thấy token T9-17. | Bỏ lời giải khỏi tracker/hint/journal tự tổng kết; hint chỉ dẫn nguồn cần đọc. Chốt rõ ngoại lệ cho tài liệu nguồn/choices để giữ yêu cầu facts-before-comparison. |
| **F02 — MAJOR** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:436] chỉ ẩn source khi chưa HasFile nhưng :509 vẫn tạo choices; :525 không gate file. [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:422] / :425 không prerequisite file cho AsylumPatient/LabArchive. | Áp dụng source gate theo objective cho mọi panel so sánh; chỉ mở choices khi có file cần thiết và luôn đặt dữ kiện cạnh choices. Thêm test Editor cho thiếu file ngoài Factory. |
| **F03 — MAJOR** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:114] CampaignWorldLabel TMP 3D, :122 offset lên 1.25 m, :140 billboard. | Dùng nhãn/decal trên thân hoặc screen-space tag khi aim theo AGENTS; cung cấp ảnh gần cả ba hộp điện trước khi aim. |
| **F04 — MAJOR** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HUDManager.cs:995] / :997 màu ngoài theme; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/TacticalUiTheme.cs:19] token theme thứ hai. A2 còn các constructor ngoài theme và preview Texture2D chưa có ngoại lệ được duyệt. | Chuyển màu/tint sang theme asset duy nhất, kiểm serialized palette/font/size. Ghi ngoại lệ cụ thể cho editor preview nếu được duyệt; không coi capture preview là artwork procedural. |
| **F05 — MAJOR** | [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:758] device-state polling; :789 đọc objective state theo nhịp; [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HUDManager.cs:398] visibility polling. | Đẩy cập nhật state/availability/visibility qua event; giữ tick cho marker/khoảng cách/progress theo ngoại lệ đã duyệt. |
| **F06 — MAJOR** | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/three-tabs-missions-1920x1080-125.png] cắt LOCKED · START GENERATOR ở đáy; [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-files-1024x768.png] cắt cuối body sau “attached. Neither the seal number nor the destination…”. | Đảm bảo viewport/scroll affordance và độ cao nội dung theo text scale; chụp lại 1024, 1080p, 4K ở 100/125% để thấy nội dung đọc trọn hoặc cue cuộn rõ. |
| **F07 — MAJOR** | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hud-1920x1080.png] không cho biết phản hồi sau làm; nhiều ảnh B thiếu nơi đến/kết quả. [SUY LUẬN] Không có chứng cứ After đủ success/error/cancel/marker kế/host/client/ba hộp. | Cho từng bước chỉ dẫn hành động, nơi và kết quả cụ thể; cung cấp chuỗi ảnh/âm thực cho thay đổi và test host/client. Ràng buộc ảnh với bản diff được kiểm. |
| **F08 — MINOR** | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-complete-1920x1080.png] END OF SUMMARY nhưng không thấy điều khiển tiếp/đóng. Mockup Complete cũng có thiếu sót này. | Đề xuất bổ sung hành động tiếp hoặc phím đóng vào mockup rồi duyệt; không tự sửa flow từ Bot7. |
| **F09 — MINOR** | [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/ThreeTabInventory-2026-11-10/After/selected-hold-1920x1080.png] SOURCE NOT RECORDED khi ACTION IN PROGRESS; mockup Source READY. | Thể hiện nguồn đúng ý nghĩa thao tác: nếu không cần tài liệu, không báo thiếu như một lỗi chặn; nếu cần, chặn trước giữ. Cập nhật mockup/state copy được duyệt. |

**Phán quyết cuối: TỪ CHỐI.** [SUY LUẬN] Các lỗi F01–F07 chưa được giải quyết. Không chấp nhận có điều kiện chỉ dựa trên 80 test đạt: có vi phạm code trực tiếp và clipping nhìn thấy. Đây là kết quả review, không sửa và không tự duyệt thay đổi.

## Đề xuất ngoài phạm vi vai

- [SUY LUẬN] Toàn bộ working tree còn có weapon/recoil, AI/director, networking telemetry, checkpoint/save, animation và nhiều ảnh tài liệu cũ. Cần vai chuyên trách đánh giá hành vi/cân bằng/độ tương thích và test tương ứng; Bot7 không gọi kết quả UI này là chấp nhận toàn bộ các hệ thống đó.
- [SUY LUẬN] Cần phép đo NavMesh giữa objective liên tiếp; chưa đo bằng Editor vì input vai không gồm scene/runtime khảo sát level. Không dùng khoảng cách marker thay phép đo.
- [SUY LUẬN] Cần kiểm nguồn/license từng sprite/font mới bằng hồ sơ được phép; không lấy báo cáo tác giả làm chứng. Không thêm asset hoặc gọi imagegen.
- [SUY LUẬN] Cần chốt ngoại lệ A1 cho dữ kiện gốc/choices nếu muốn giữ panel so sánh có dữ kiện; chưa có ngoại lệ thì kiểm literal vẫn FAIL. Việc này không gỡ lỗi spoiler trong tracker/hint/journal.

## Tự kiểm mâu thuẫn

1. [SUY LUẬN] **PASS test ≠ PASS visual.** Báo cáo giữ riêng 80/80 assert và các FAIL hình/chữ; không tuyên bố test bao phủ clipping, âm hay toàn flow.
2. [SUY LUẬN] **A1 và facts-before-comparison xung đột ở dữ kiện gốc/choices.** Lệnh trực tiếp A1 mới nhất ưu tiên, nên vẫn ghi FAIL literal. Phân biệt với spoiler tự đưa ra, không đề xuất xóa sạch dữ kiện khiến người chơi không còn cơ sở so sánh.
3. [SUY LUẬN] **Tài liệu Manifest PASS ba câu vẫn FAIL A1.** Hai phép kiểm khác nhau. Failed có nút retry rõ trong ảnh nhưng chưa xác nhận nút thực thi.
4. [SUY LUẬN] **World label FAIL-code dù ảnh không thấy.** Ảnh ở xa không phủ định đường tạo TMP 3D. Không đồng thời viết “đã hết chữ lơ lửng”.
5. [SUY LUẬN] **State trước marker PASS-code, runtime chưa kiểm.** Không nâng thứ tự Publish/RPC thành bằng chứng âm/animation hoặc marker đã đổi.
6. [SUY LUẬN] **Không gán lỗi nguồn trước choice cho Isolate từ ảnh Source NOT RECORDED.** F02 dùng code Asylum/Lab; F09 chỉ là copy lệch mockup.
7. [SUY LUẬN] **Cắt chữ không chứng minh không có scroll.** Bảng chỉ nói nội dung bị mask cắt và cue cuộn không nhìn thấy, không nói ScrollRect bị xóa.
8. [SUY LUẬN] **Ảnh Tank cũ không chứng minh trạng thái mới.** Chỉ mô tả chữ/control lịch sử; không khẳng định retry vẫn tồn tại hoặc bị mất trong Files/Record mới.
9. [SUY LUẬN] **Màu mặc định không phải palette runtime.** Có tám field opaque trong source; chưa đọc serialized theme qua Editor nên không khẳng định runtime hiển thị tám màu. Texture2D preview được tách khỏi sinh artwork HUD.
10. [SCENE E:/Unity/Project/FPS/Assets/FPS/Scenes/MainMenu.unity/isDirty] Read-only không lưu scene dirty; scene thực tế clean trước/sau. [SUY LUẬN] Save Project để đáp ứng SaveAssets không làm đổi byte trong 241 Assets đã hash. Không xóa ảnh vì vai cấm sửa/xóa.

## Chưa kiểm được

- [SUY LUẬN] Playtest thực các bước thay đổi, success/wrong/missing-selection/nhả F, chuyển waypoint sau snapshot; âm gì, hiệu ứng nào, ở đâu, kéo dài bao lâu: **KHÔNG TÌM THẤY chứng cứ runtime đủ**.
- [SUY LUẬN] Host/client thực, RPC bị trễ, spoof request; Tank khi thiếu actor/spawn service/safe position nhiều lần, retry hết hạn và khả năng tránh soft-lock chapter; các ảnh Tank hiện có là lịch sử.
- [SUY LUẬN] Ba hộp điện nhận diện từ xa/trước aim, body collider waypoint, mọi khoảng cách/độ cao/tỉ lệ màn hình, tuyến không có NavMesh và số đo đi bộ objective → objective.
- [SUY LUẬN] Asset provenance/license, lịch sử imagegen và lịch sử phê duyệt từng gate cho toàn working tree; serialized palette/font/panel thực dùng. KHÔNG TÌM THẤY trong tập input độc lập được phép.
- [SUY LUẬN] Chưa xem từng ảnh weapon/animation/AI cũ ngoài các luồng guidance đã liệt kê; chưa review hành vi các hệ thống ngoài vai; chưa chạy toàn bộ Editor/PlayMode suite. Các ảnh 4K đã mở ở chế độ công cụ có thể thu xuống 2048×1152, nên chưa kết luận chất lượng glyph từng pixel ở native 4K.
- [SUY LUẬN] Không có hash build/commit gắn vào từng screenshot; metadata và tên After không bảo đảm tất cả ảnh cùng working tree đang kiểm.
- [SCENE E:/Unity/Project/FPS/Assets/FPS/Scenes/MainMenu.unity/isDirty] Kết quả cuối: **isDirty=false**; Editor **isPlaying=false, isCompiling=false**. Query thật:

```json
{"status":"success","result":{"success":true,"count":1,"scenes":[{"name":"MainMenu","path":"Assets/FPS/Scenes/MainMenu.unity","isLoaded":true,"isDirty":false,"isActive":true,"rootCount":7}]}}
```

