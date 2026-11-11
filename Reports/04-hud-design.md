# Bot 4 — thiết kế HUD, giai đoạn 1 và bản phác giai đoạn 2

> [SUY LUẬN] Mockup A v4 và kết quả kiểm HTML nằm ở mục cuối; chưa được duyệt, các bản trước lưu lịch sử.

> [SUY LUẬN] Kế hoạch tạo mockup mới ở mục cuối; bản v3 đã bị chê và chưa được duyệt.

> [SUY LUẬN] Bản v3 đã bị người dùng chê, chưa được duyệt; nghiên cứu thẩm mỹ mới nằm ở cuối tài liệu.

> [SUY LUẬN] Bản A cũ và bản revision đều đã bị bác. Thiết kế đang chờ xem xét là mục “Bản A v3” cuối tài liệu; các đoạn cũ chỉ để truy vết.

> [SUY LUẬN] Các phương án và ảnh A cũ trong phần đầu là lịch sử; bản A cũ đã bị từ chối. Xem mục “Bản A chỉnh sửa theo phản hồi ngày 29/10/2026” ở cuối cho thiết kế hiện hành chưa được duyệt.

## Phạm vi và bằng chứng

- [SUY LUẬN] **Giai đoạn 1:** khi viết phần ba hướng bên dưới, chưa có lựa chọn và chưa tạo mockup. **Giai đoạn 2:** người dùng trả lời `A`, chọn hướng Tối giản công nghiệp; lựa chọn hướng không phải duyệt mockup. Các ảnh, cây và hợp đồng trong phần giai đoạn 2 là đề xuất chờ người dùng duyệt, không phải quyền triển khai.
- [SUY LUẬN] Không sửa code, scene, prefab hay asset; không import, không gọi imagegen, không vào Play Mode. Trong giai đoạn 2 chỉ đọc file, dùng trình duyệt headless để chụp HTML và ghi kết quả trong `E:/Unity/Project/FPS/Reports/`; không mở hoặc điều khiển Unity Editor.
- [SUY LUẬN] Đầu vào gồm `E:/Unity/Project/FPS/Reports/02-ui-assets.md`, ảnh HUD người dùng được báo cáo đó dẫn tại `C:/Users/hieud/AppData/Local/Temp/codex-clipboard-d1ecc882-e61b-4256-9832-4c15bdf7c309.png`, và `E:/Unity/Project/FPS/AGENTS.md`. Ảnh tạm đã được xem lúc khảo sát nhưng **KHÔNG TÌM THẤY ở lần kiểm tra cuối**; không biết nguyên nhân, vì vậy đường dẫn này hiện không còn tái kiểm được. KHÔNG TÌM THẤY ảnh tham chiếu phong cách khác trong yêu cầu hiện tại. Ảnh đã xem không chứng minh runtime ở thời điểm báo cáo này.
- [SUY LUẬN] Trong ảnh đầu vào: tracker ở góc trên trái ghi `FACTORY`, `Read the shipping manifest`, `POWER 0/3 · LOGISTICS 0/2 [T]`; giữa màn hình có chữ 3D `POWER / ISOLATE` bị lật. Góc dưới trái tách `INFECTION 0%` khỏi `100 / 100`; góc dưới phải có bốn ô đồ, `WARDEN`, `15 / 45`, `FIRE`, `AIM`, `CLASSIC`; marker bên phải ghi `144m ROUTE`. Một ảnh tĩnh không cho biết thời lượng hiển thị hay âm thanh.
- [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/AssetReplacement/Gameplay/tracker-marker-1920x1080-100.png] Ảnh bằng chứng cũ ngày 28/10/2026 có tracker, dòng `21m TO TARGET`, phụ đề, hai panel máu/nhiễm và panel đạn. Đây là ảnh của phiên trước, không phải playtest hoặc xác nhận runtime của phiên này.
- [SCENE Assets/FPS/Scenes/GameScene.unity/isDirty] Unity-Skills chỉ đọc báo scene đang active, `isDirty == false` trước khi khảo sát và ở lần kiểm thành công gần nhất. Hai lượt truy vấn trong lúc làm trả `rootCount` lần lượt 8 và 14; KHÔNG BIẾT nguyên nhân, vì vậy không coi scene hay ảnh là snapshot bất biến. **Kiểm tra cuối sau khi viết báo cáo không thành công** vì Unity-Skills từ chối kết nối hoặc timeout; trạng thái kết thúc chưa xác minh. Không lưu scene hoặc gọi `AssetDatabase.SaveAssets()`.
- [SUY LUẬN] Workspace Git đã có nhiều thay đổi trước lượt này, trong đó có `GameScene.unity` và các file HUD; không dùng trạng thái Git hay ảnh cũ để suy ra runtime hiện tại. Codebase Memory MCP không công bố resource/template trong phiên; không tự kết luận rằng toàn bộ máy không có index.

## Skill đã đọc và quy tắc áp dụng

- [SUY LUẬN] `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md` và `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/references/ui-planning-workflow.md`: kiểm tra ảnh và asset trước; phân biệt yêu cầu đã có với lựa chọn còn mở; hỏi một quyết định lớn tại một thời điểm; giai đoạn 1 không tạo cây layout hay đối tượng Unity trước lựa chọn.
- [SUY LUẬN] `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/references/execution-contract.md`, `mockup-decomposition.md`, `mockup-resolution.md` và `mockup-safe-area-mapping.md`: giai đoạn 2 lập cây parent-owner, đo rect từ ảnh 1920×1080 và tỷ lệ theo toàn ảnh; giữ mọi candidate `hold` vì cổng duyệt của người dùng chưa qua; không tạo object hay crop asset.
- [SUY LUẬN] `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`: đề xuất neo vào vùng màn hình, chừa safe area, dự kiến kiểm chữ dài/khả năng đọc ở nhiều tỉ lệ và không coi tọa độ cố định 1080p là layout runtime.
- [SUY LUẬN] `C:/Users/hieud/.agents/skills/unity-skills/SKILL.md`: khám phá `health`, schema và chỉ gọi các truy vấn được khai báo read-only; không thực hiện lệnh ghi.
- [SUY LUẬN] `C:/Users/hieud/.agents/skills/unity-skills/skills/scene/SKILL.md`: dùng `scene_get_loaded`/`scene_get_info` để kiểm tra `isDirty`, không gọi `scene_save`, `scene_load` hay `scene_screenshot`.
- [SUY LUẬN] `C:/Users/hieud/.agents/skills/unity-skills/skills/project/SKILL.md`: dùng `project_get_info` để đối chiếu project đang kết nối là `E:/Unity/Project/FPS` và không chạy Play Mode.
- [SUY LUẬN] `C:/Users/hieud/.agents/skills/unity-skills/skills/asset/SKILL.md`: tìm sprite/font bằng `asset_find`, `asset_get_info` trước khi xem hình gốc và manifest; không gọi import, refresh hay reimport.
- [SUY LUẬN] `C:/Users/hieud/.agents/skills/unity-skills/skills/importer/SKILL.md`: phân biệt getter với thao tác sửa importer. Getter bị ngắt kết nối trong lúc kiểm tra; không khẳng định đã kiểm border đang cấu hình trong Unity, không đổi importer.
- [SUY LUẬN] `create-game-assets/SKILL.md`: KHÔNG TÌM THẤY trong các root skill đã đăng ký; chưa đọc và không tuyên bố áp dụng skill này. Quy tắc kiểm tra `Assets/` trước khi đề xuất tài nguyên được thực hiện theo `AGENTS.md`, độc lập với skill còn thiếu.

## Ràng buộc chung cho cả ba hướng

- [SUY LUẬN] Cả ba dùng tối đa bốn token màu: trắng `#F2F2F2` cho chữ chính; cam `#F2A436` chỉ cho việc cần làm ngay; đỏ `#D84B4B` chỉ khi logic báo nguy hiểm; xám `#25282A` cho nền/khung phụ. Chữ phụ là trắng giảm alpha theo theme trên panel xám, không thêm token màu riêng. Không dùng cam để trang trí chữ `FACTORY`, ô đang chọn hay thông tin không cần hành động ngay.
- [SUY LUẬN] Tài nguyên ảnh có sắc độ xám sẵn trong pixel: cần kiểm thực tế xem chúng có phù hợp cách hiểu giới hạn bốn màu của dự án hay không. Không khẳng định texture/tint đã tuân thủ trước khi có mockup và kiểm màu. Màu, alpha và ba cỡ chữ đề xuất đều phải thuộc cùng một theme, không viết rải rác ngoài theme.
- [SUY LUẬN] Ba cấp chữ dự kiến ở khung tham chiếu 1920×1080: lớn 36, vừa 26, nhỏ 20; dùng cùng ba cấp trên mọi thành phần và scale theo màn hình. Đây là kích thước thiết kế, chưa phải số đo đọc được ở 1080p, 4K hay màn hình khác.
- [SCENE AssetDatabase/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset] Cả ba đề xuất font chính `E:/Unity/Project/FPS/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset`; nguồn TTF `E:/Unity/Project/FPS/Assets/TextMesh Pro/Fonts/LiberationSans.ttf`, giấy phép SIL OFL 1.1 tại `E:/Unity/Project/FPS/Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`.
- [SCENE AssetDatabase/Assets/FPS/Features/UI/Content/Resources/HudSkin/Fonts/Teko-Bold SDF.asset] Cả ba đề xuất một font số `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Fonts/Teko-Bold SDF.asset`; TTF cùng thư mục, SIL OFL 1.1 theo `E:/Unity/Project/FPS/Reports/UIUX/AssetReplacement/Teko-OFL.txt` và `E:/Unity/Project/FPS/Documents/UIUX/hud-skin-assets.json`. Không cần tìm font ngoài ở giai đoạn này; glyph tiếng Việt trong TMP/SDF và fallback vẫn chưa kiểm.
- [SUY LUẬN] Các hướng chỉ đổi cách trình bày; không thêm nhiệm vụ, đáp án, vật tương tác, cốt truyện hay quy tắc gameplay. Mục tiêu hiện tại, hai nhánh và lý do khóa phải lấy từ logic; hint chỉ chỉ nguồn cần đọc, không chứa đáp án.

## A — Tối giản công nghiệp: đề xuất ưu tiên

- [SUY LUẬN] Mức tối giản **cao**: tracker gọn ở trên trái; máu/nhiễm dưới trái; đạn và hotbar dưới phải; prompt, vòng giữ và phụ đề chỉ mở theo ngữ cảnh. Khoảng trống giữa màn hình được ưu tiên cho tâm ngắm và quan sát cảnh.
- [SUY LUẬN] Một kiểu panel 9-slice tối, viền mảnh cho tracker, prompt, nền phụ đề và panel đạn: ứng viên `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png`. Manifest `E:/Unity/Project/FPS/Documents/UIUX/curated-assets.json` ghi biên `(12,12,12,12)` và hash hiện tại trùng manifest; **chưa xác minh border importer đang hoạt động**. Dùng cùng một kiểu panel, không đưa thêm khung trang trí.
- [SUY LUẬN] Bộ asset cho tracker/prompt/marker/thanh/đạn/hotbar/phụ đề/vòng giữ là bộ dùng chung trong bảng bên dưới; màu và hai font theo mục ràng buộc chung. Chữ `▶` trắng chỉ nhánh đang chọn; phím đổi nhánh có ô riêng, không dùng màu cam để đánh dấu nhánh.
- [SUY LUẬN] Hợp cảnh container và cửa kim loại đang thấy trong ảnh, đồng thời không biến thông tin nhiễm bệnh thành trang trí; độ mòn ít nên gợi bối cảnh xác chết/sinh tồn yếu hơn B. Đây là nhận định thiết kế, không phải đánh giá đã playtest.

## B — Hồ sơ sinh tồn cũ

- [SUY LUẬN] Mức tối giản **trung bình**: tracker và hai nhánh được nhóm như một phiếu theo dõi; panel máu/nhiễm, đạn và phụ đề có cùng khung, khoảng thở rộng hơn A; chất bề mặt cũ chỉ nằm trong panel chứ không phủ tâm ngắm.
- [SUY LUẬN] Một kiểu khung 9-slice là `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png`; có thể đặt lớp mòn đơn sắc `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Distress.png` bên trong cùng panel nếu kiểm được tương phản và giới hạn màu. Lớp mòn không phải kiểu khung 9-slice thứ hai; không sửa hai sprite trong giai đoạn này.
- [SUY LUẬN] Bộ asset chức năng, bốn token màu và hai font giống bảng dùng chung; khác A ở mức độ mòn và cách nhóm thông tin, không thêm dữ liệu hay câu chuyện. Biên panel hiện hành và độ tương phản khi thêm texture đều chưa kiểm.
- [SUY LUẬN] Mặt panel cũ gợi hồ sơ nhiễm bệnh và bối cảnh sinh tồn/xác chết rõ hơn A; nhược điểm là vết mòn có thể làm chữ phụ khó đọc hoặc chiếm nền, cần mockup trên cảnh sáng và tối trước khi kết luận.

## C — Bảng điều phối nhà máy

- [SUY LUẬN] Mức tối giản **trung bình-thấp**: các vùng tracker, sinh tồn và đạn được xếp theo hàng/cột rõ, keycap tách khỏi nhãn; các cụm được căn mép nhất quán như bảng điều khiển công nghiệp. Không thêm đồng hồ, đồ thị, mã số hay nhãn trang trí.
- [SUY LUẬN] Một kiểu panel 9-slice ứng viên `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Controls/Control.png`, tài liệu `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/README.md` mô tả đây là control plate 9-sliced. Ảnh gốc sáng; chỉ đề xuất thử tint xám thuộc theme để đặt chữ trắng, chưa chứng minh đủ tương phản hoặc border importer hiện tại.
- [SUY LUẬN] Bộ asset chức năng, bốn token màu và hai font giống bảng dùng chung; khác A/B ở hình panel vát cạnh và cách căn các cụm. Nếu tint không đạt tương phản mà không sửa asset, phải dùng placeholder và chờ phương án được duyệt, không tự tạo asset thay.
- [SUY LUẬN] Ngôn ngữ bảng điều phối phù hợp cửa và thiết bị kim loại trong ảnh hơn cảm giác hồ sơ của B; nhược điểm là nhiều mép/ô hơn, dễ làm HUD có vẻ giống giao diện điều khiển hơn game sinh tồn nếu không giữ nội dung tối giản.

## Bộ asset ứng viên theo thành phần

| Thành phần | Ứng viên trong `Assets/` và giới hạn bằng chứng |
|---|---|
| Tracker, prompt, phụ đề, panel đạn, ô phím | [SCENE AssetDatabase/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png] A/B: `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png`; C: `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Controls/Control.png`. Chỉ chọn **một** kiểu panel cho hướng được chọn. Nguồn SunGraphica; điều khoản và yêu cầu credit ở `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Licenses/SunGraphica-Creepy-UI-FREE.txt`. |
| Khóa và hoàn tất | [SCENE AssetDatabase/Assets/FPS/Features/UI/Content/Resources/HudSkin/Locked.png] `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Locked.png` và `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Complete.png`; manifest ghi SunGraphica, cần credit; chưa kiểm hiển thị các trạng thái này. |
| Marker trong/ngoài màn hình | [SCENE AssetDatabase/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/GoalMarker.png] `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/GoalMarkerCentre.png`, `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/GoalMarker.png`; mũi tên khác để cân nhắc `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Arrow.png`. FPSSample theo Unity Companion License tại `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/LICENSE.md`; Arrow theo manifest SunGraphica. Chưa xác nhận hình xoay/đọc được ở mép màn hình hoặc ngoài vùng súng. |
| Máu và phần nhiễm | [SCENE AssetDatabase/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Bars/HealthTrack.png] `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Bars/HealthTrack.png`, `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Bars/HealthFill.png` và ứng viên `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Fill.png`. Nguồn Lotus Garden theo `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Licenses/Lotus-Post-Apocalypse-UI.txt`; chưa kiểm màu/tỉ lệ khi chồng hai giá trị. |
| Vòng giữ | [SCENE AssetDatabase/Assets/FPS/Features/Survival/Content/Textures/ProgressRing.png] `E:/Unity/Project/FPS/Assets/FPS/Features/Survival/Content/Textures/ProgressRing.png` có hình vòng trong ảnh gốc; nguồn/giấy phép gốc **KHÔNG TÌM THẤY trong phạm vi kiểm tra**, cần xác minh trước khi tái sử dụng chính thức. `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/GoalProgress.png` là ảnh tròn đặc; không coi nó tự thân là vòng rỗng. |
| Đạn và hotbar | [SCENE AssetDatabase/Assets/FPS/Features/UI/Content/Resources/HudSkin/Slot.png] Ô `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Slot.png`; số dùng Teko, chữ dùng Liberation Sans. Biểu tượng hiện có gồm `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Frag.png`, `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Medkit.png`, `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Antidote.png`: chúng có nhiều màu và giấy phép gốc chưa xác minh theo manifest. Không khẳng định chúng tuân thủ bốn màu. |
| Nhãn mục tiêu trong thế giới | [SUY LUẬN] Asset decal/biển phù hợp từng vật: **KHÔNG TÌM THẤY trong phạm vi kiểm tra**; ứng viên nhãn màn hình khi ngắm có thể dùng cùng panel đã chọn. Dấu nhận biết vật mục tiêu trước khi ngắm vẫn cần asset/giải pháp riêng: **GREYBOX PLACEHOLDER**, chưa tạo và chưa xác minh. |

- [SUY LUẬN] Ô phím là cùng kiểu panel đã chọn, không phải kiểu panel 9-slice thứ hai. Một số icon vật phẩm hiện có dùng xanh, vàng và đỏ: nếu quy tắc bốn màu áp dụng cả pixel icon thì không thể vừa giữ nguyên các icon vừa tuyên bố tuân thủ. Trong mockup tương lai phải đánh dấu **GREYBOX PLACEHOLDER** cho icon đơn sắc chưa có hoặc xin quyết định riêng; không tự sửa hay tạo icon lúc này.
- [SUY LUẬN] Không dùng nguyên trạng `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Panel.png` làm panel mới: ảnh có vệt nâu/gỉ và phải kiểm giới hạn màu trước. Không suy ra tint Unity sẽ tự chuyển sprite nhiều màu thành đơn sắc.

## Phân tầng thông tin đề xuất

| Thành phần HUD đã thấy hoặc có trong báo cáo đầu vào | Tầng đề xuất | Nội dung và giới hạn |
|---|---|---|
| Tracker và hai nhánh Factory | [SUY LUẬN] Luôn hiện khi có objective | Một dòng việc hiện tại; `POWER` và `LOGISTICS` mỗi nhánh một dòng cùng tiến độ; chỉ nhánh đang theo dõi có `▶`; ô phím `[T]` riêng nếu binding thực tế là T. Không ẩn nhánh chưa chọn hoặc ghi đáp án. |
| Marker tên và khoảng cách | [SUY LUẬN] Luôn hiện khi có mục tiêu hợp lệ | Tên thân tương tác + khoảng cách ở mọi cự ly; mục tiêu ngoài màn hình có mũi tên mép. Không hứa rằng ảnh hiện tại đã có tên mục tiêu hoặc mũi tên đúng; đường thay thế khi không có NavMesh là yêu cầu logic cần xác minh. |
| Tâm ngắm/ADS | [SUY LUẬN] Luôn hiện theo trạng thái điều khiển | Giữ tâm ngắm cho trạng thái ngắm/bắn phù hợp; không tự thay đổi cơ chế ADS hay vũ khí. |
| Máu và nhiễm | [SUY LUẬN] Luôn hiện khi gameplay | Một thanh chung, phần nhiễm là lớp phủ phân biệt bằng vị trí/hình và nhãn; ghi riêng ví dụ `HP 100/100 · Nhiễm 0%`. Hai đại lượng có thang riêng: không suy ra 100% nhiễm tương đương 0 máu hoặc tự trừ máu. Màu đỏ chỉ khi logic báo nguy hiểm. |
| Đạn/vũ khí | [SUY LUẬN] Luôn hiện khi đang dùng vũ khí | Số đạn hiện tại lớn; đạn dự trữ và tên súng nhỏ; bỏ riêng chữ `FIRE`, `AIM`, `CLASSIC` khỏi HUD gameplay theo yêu cầu, không xóa vũ khí hay binding. |
| Hotbar bốn ô trong ảnh | [SUY LUẬN] Ẩn được | Cho phép thu gọn khi không cần, nhưng không ẩn thông tin đang cần hành động; khi hiện phải tách phím và số lượng khỏi icon. Cách người chơi ẩn/hiện chưa được chọn, không tự thêm phím. |
| Prompt mở được/khóa | [SUY LUẬN] Mở khi cần | Khi ngắm vật mở được: `[F]` + tên vật nếu phím thực tế là F. Khi khóa: icon khóa + `Cần: ...`, không có `[F]`; phím theo dõi là ô riêng, binding cụ thể **CHƯA BIẾT**. Không tự viết lý do khóa. |
| Giữ tương tác | [SUY LUẬN] Mở khi cần | Vòng quanh tâm + phím thực tế, thể hiện tiến trình và thả sớm; chỉ đề xuất trạng thái, chưa kiểm chuyển động hoặc thời lượng. |
| Phụ đề/hint | [SUY LUẬN] Mở khi cần | Phụ đề ở dưới giữa trên nền panel, giới hạn bề rộng để không chạm panel đạn; hint lấy từ `CampaignContentCatalog` theo objective, không chứa đáp án. Thời lượng/âm lời thoại hiện tại **CHƯA KIỂM**. |
| Journal/tài liệu/đối chiếu | [SUY LUẬN] Mở khi cần | Giữ là overlay theo yêu cầu tương tác, không phủ thường trực gameplay; không tạo thêm nội dung hay tự mở panel lựa chọn trước khi có dữ liệu. |
| Dòng `Next: ...` trong ảnh phiên trước | [SUY LUẬN] Đề xuất bỏ phần trùng | Nếu dòng này lặp việc đã có trong tracker thì bỏ bản lặp, không bỏ tracker hoặc marker; cần xác minh trường hợp nó mang thông tin khác trước khi triển khai. |
| Chữ 3D `POWER / ISOLATE` lơ lửng | [SUY LUẬN] Đề xuất bỏ cách hiển thị | Không bỏ mục tiêu. Chuyển sang biển/decal gắn vật hoặc nhãn màn hình khi ngắm; vật proxy mục tiêu vẫn phải được nhận diện trước khi ngắm. Asset biển/decal phù hợp chưa xác minh. |

- [SUY LUẬN] Phản hồi hoàn tất cần đủ ba phần: ví dụ chữ `Đã hoàn tất` cạnh vị trí prompt, icon `Complete` đổi trạng thái, và âm hoàn tất **CHƯA KIỂM/TÊN ÂM KHÔNG TÌM THẤY**; không coi tiếng click đơn thuần là đủ. Giai đoạn 1 không đặt thời lượng; số trong giai đoạn 2 chỉ là đề xuất chưa đo runtime. Sau khi logic công bố mục tiêu kế tiếp mới cập nhật marker.
- [SUY LUẬN] Khi sai cần nêu chính xác trường lệch từ logic, không đoán trường hoặc đáp án; nội dung và thời lượng của phản hồi lỗi **CHƯA KIỂM**.

## Hợp đồng dữ liệu sơ bộ cho Bot 5

- [SUY LUẬN] Đây chỉ là danh sách thông tin HUD cần nhận, không khẳng định API/field đã tồn tại hay cho phép sửa logic: objective hiện tại, tên/vị trí thân tương tác, nguồn hint từ catalog, và trạng thái mục tiêu mới đã được công bố trước khi marker đổi.
- [SUY LUẬN] Cần trạng thái hai nhánh `POWER`/`LOGISTICS`, nhánh đang theo dõi, tiến độ từng nhánh và binding đổi nhánh; mọi nhánh vẫn hiển thị, chỉ nhánh theo dõi có `▶`.
- [SUY LUẬN] Cần trạng thái mở/khóa, lý do khóa theo mục tiêu, binding tương tác và binding theo dõi riêng; khi giữ cần tiến trình, phím, hủy khi thả sớm, hoàn tất và thông tin trường sai nếu có.
- [SUY LUẬN] Cần dữ liệu vị trí/đích marker, khoảng cách, trạng thái có hoặc không có đường NavMesh; không yêu cầu Bot 4 tính hay khẳng định khoảng cách đi bộ. Server quyết định hoàn tất/điều kiện/pha; client hiển thị và gửi yêu cầu.
- [SUY LUẬN] Cần HP hiện tại/tối đa, mức nhiễm và cờ nguy hiểm độc lập; đạn hiện tại/dự trữ, tên súng, icon/phím/số lượng hotbar, phụ đề và thời điểm ẩn theo nguồn có thẩm quyền. Không suy ra quan hệ giữa HP và nhiễm.

## Câu hỏi giai đoạn 1 — đã trả lời

1. [SUY LUẬN] Câu hỏi trước: chọn A, B hay C. Người dùng đã trả lời `A`; chỉ lựa chọn hướng A được xác nhận, không suy ra mockup đã được duyệt.

## Giai đoạn 2 — style guide một trang cho A

- [SUY LUẬN] **Trạng thái duyệt:** A được chọn; bộ mockup A dưới đây là **một đích thị giác** có nhiều ảnh trạng thái, chưa được duyệt. Không diễn giải sự lựa chọn A là duyệt bố cục, asset, thời lượng hoặc gameplay.
- [SUY LUẬN] **Bốn token màu:** chữ chính trắng `#F2F2F2`; cam `#F2A436` chỉ cho hành động ngay như phím mở và vòng đang giữ; đỏ `#D84B4B` chỉ khi dữ liệu báo nguy hiểm; xám `#25282A` cho nền/viền/chữ phụ. Vòng sau khi hủy là trắng/xám, không còn cam. Token trong HTML chỉ là bản phác, không phải theme asset Unity.
- [SUY LUẬN] **Ba cỡ duy nhất tại 1920×1080:** lớn 36 px cho một dòng việc và đạn hiện tại; vừa 26 px cho tên vật, nhánh, khoảng cách, lý do khóa, kết quả và subtitle; nhỏ 20 px cho nhãn phụ, ô phím, số lượng và caption. Tỷ lệ tại thiết bị khác chưa được kiểm; không dùng số pixel này như giá trị runtime cố định.
- [SCENE AssetDatabase/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset] **Font chữ:** `E:/Unity/Project/FPS/Assets/TextMesh Pro/Fonts/LiberationSans.ttf`, dùng nguồn `E:/Unity/Project/FPS/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` cho Unity; SIL OFL 1.1 tại `E:/Unity/Project/FPS/Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`.
- [SCENE AssetDatabase/Assets/FPS/Features/UI/Content/Resources/HudSkin/Fonts/Teko-Bold SDF.asset] **Font số:** `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Fonts/Teko-Bold.ttf`, tương ứng `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Fonts/Teko-Bold SDF.asset`; SIL OFL 1.1 tại `E:/Unity/Project/FPS/Reports/UIUX/AssetReplacement/Teko-OFL.txt`. Font TTF đã hiển thị trong ảnh trình duyệt; glyph/fallback của TMP trong Unity chưa kiểm.
- [SUY LUẬN] **Một kiểu panel 9-slice:** `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png` cho tracker, nền marker, ô phím, prompt, subtitle, máu, hotbar và đạn. Manifest `E:/Unity/Project/FPS/Documents/UIUX/curated-assets.json` ghi border `12/12/12/12`; importer hiện hành chưa kiểm. Nguồn SunGraphica và yêu cầu credit tại `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Licenses/SunGraphica-Creepy-UI-FREE.txt`.
- [SUY LUẬN] **Quy tắc bố cục:** một owner safe area; trong khung phác inset tham chiếu 64 px; tracker neo trên trái, máu dưới trái, hotbar/đạn dưới phải, subtitle dưới giữa, prompt/vòng theo tâm và marker theo mục tiêu. UGUI dự kiến dùng anchor và layout group; tọa độ ở đây chỉ là số đo ảnh, không phải `anchoredPosition` cuối.
- [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HudLayout.cs:20] `HudLayout.ConfigureCanvas` hiện cấu hình `ScaleWithScreenSize` và match theo chiều cao `1`; đây là bằng chứng về code campaign hiện có, không chứng minh toàn HUD đã dùng cùng Canvas. Đề xuất giữ tham chiếu 1920×1080 và kiểm thêm 4K, tỉ lệ khác, safe area và chữ dài trước triển khai.
- [SUY LUẬN] Ảnh panel, icon và nền đã có nhiều sắc độ/alpha trong pixel; giới hạn bốn **token** không chứng minh mọi pixel ảnh chỉ có bốn giá trị RGB. Nếu dự án hiểu quy tắc là giới hạn pixel tuyệt đối, các sprite này chưa đạt xác minh và cần quyết định asset riêng, không tự sửa.

## Giai đoạn 2 — bộ ảnh phác

- [SUY LUẬN] `E:/Unity/Project/FPS/Reports/mockups/04-hud-a.html` và PNG bên dưới là **mockup không phải asset để import**, không phải Game view hay playtest. Nền tối lấy từ `E:/Unity/Project/FPS/Documents/LevelDesignAudit/2026-10-22/screenshots/audit-generator.png`; nền sáng có viewmodel lấy nguyên ảnh lưu trữ `E:/Unity/Project/FPS/Reports/UIUX/AssetReplacement/Gameplay/reticle-bright.png`. Lớp HUD chỉ chồng bằng trình duyệt, không chứng minh vị trí vật hoặc camera hiện tại trong scene.
- [SUY LUẬN] Tổng quan nền tối, mở được: `E:/Unity/Project/FPS/Reports/mockups/04-a-normal.png`; cùng bố cục trên nền sáng có súng: `E:/Unity/Project/FPS/Reports/mockups/04-a-normalbright.png`.
- [SUY LUẬN] Khóa và mũi tên cạnh trái: `E:/Unity/Project/FPS/Reports/mockups/04-a-locked.png`; không có ô `F` trong prompt khóa, `?` là **placeholder binding theo dõi chưa biết**, không phải phím đã xác nhận.
- [SUY LUẬN] Giữ đầy: `E:/Unity/Project/FPS/Reports/mockups/04-a-hold100.png`; hai ảnh thả sớm `E:/Unity/Project/FPS/Reports/mockups/04-a-release65.png` và `E:/Unity/Project/FPS/Reports/mockups/04-a-release30.png` biểu diễn hai mốc lùi 65% → 30%, không chứng minh animation hoặc thời gian chạy.
- [SUY LUẬN] Sau xác nhận hoàn tất và công bố mục tiêu kế: `E:/Unity/Project/FPS/Reports/mockups/04-a-complete.png`; lỗi cần tên trường từ logic: `E:/Unity/Project/FPS/Reports/mockups/04-a-error.png`. Các từ trong ngoặc vuông, số 80/100, 20%, 21 m, 15 m và số lượng hotbar chỉ là dữ liệu phác; không phải nội dung nhiệm vụ, đáp án hay trạng thái thật mới.
- [SUY LUẬN] Hai dòng chú thích `BẢN PHÁC · KHÔNG PHẢI PLAYTEST` và `GREYBOX PLACEHOLDER · ICON ĐƠN SẮC` chỉ phục vụ duyệt ảnh, không phải thành phần HUD đề xuất để phát hành.
- [SUY LUẬN] Kiểm ảnh/DOM trình duyệt ở tám trạng thái: ảnh đều 1920×1080, nguồn ảnh HTML không báo lỗi tải; biên phải subtitle `x=1285` và biên trái panel đạn `x=1591`, cách nhau 306 px; hàng phím/số lượng của cả bốn slot không giao hình chữ nhật icon. Đây chỉ là kiểm mockup tĩnh 1080p, không phải kiểm tương phản định lượng, 4K, tương tác hay Unity.
- [SUY LUẬN] Trong `04-a-normalbright.png`, marker mẫu ở `(1160,330)`, kích thước `408×89`, nằm phía trên vùng súng nhìn thấy; trong `04-a-locked.png`, mũi tên mẫu nằm cạnh trái và chỉ sang trái. Hai ảnh không chứng minh tránh viewmodel ở các hướng nhìn, vũ khí hay animation khác.

## Giai đoạn 2 — layer-to-layout tree

- [SUY LUẬN] Kế hoạch máy đọc `E:/Unity/Project/FPS/Reports/mockups/04-hud-layout-plan.yaml` theo `mockup-layout-plan/v2`, stack dự kiến UGUI; 18 node là **cây đề xuất**, không phải hierarchy scene hiện tại. Gốc rect là góc trên trái ảnh, `x` sang phải, `y` xuống dưới; `geometry_ratios` chia theo 1920×1080 toàn ảnh.
- [SUY LUẬN] 11 candidate đều `review_source: agent`, `review_decision: hold` vì người dùng chưa duyệt; `item_rect_plan: []` và `asset_plan: []` cố ý để trống theo cổng duyệt của skill, không có nghĩa asset đã biến mất. Rect dự kiến nằm trong candidate ledger; danh sách asset ứng viên nằm ở báo cáo này, chưa tạo runtime node hoặc asset.
- [SUY LUẬN] Parent owner và lý do tách/giữ của cây:

```text
HUD / CanvasScaler dự kiến
└─ SafeArea — một owner safe area
   ├─ Tracker — neo trên trái; một panel giữ nguyên
   │  ├─ Objective — chuỗi một dòng thay đổi theo logic
   │  ├─ Branches → Branch × 2 — nhãn, tiến độ, ▶ theo nhánh
   │  └─ Switch — ô binding [T] tách khỏi nhãn
   ├─ OnscreenMarker / EdgeMarker — hai trạng thái loại trừ của cùng đích
   ├─ CenterFeedback → Ring — vòng, binding, caption giữ/hủy
   ├─ Interaction — mở được / khóa / hoàn tất / lỗi; nhãn màn hình khi ngắm
   ├─ Vitals → Bar — track và hai lớp máu/nhiễm độc lập
   ├─ Hotbar → Slot × 4 — hàng phím/số lượng tách hàng icon
   ├─ Ammo — tên súng, đạn hiện tại và dự trữ
   └─ Subtitle — nền mờ, viền chữ; neo dưới giữa
```

- [SUY LUẬN] Marker cần bám thân tương tác và xuất hiện mọi cự ly; vị trí `(1160,330)` chỉ là ví dụ. Khi hình chiếu lọt vùng viewmodel, phương án đề xuất là chuyển chỉ dẫn ra vùng an toàn bằng mũi tên hướng về đích, không làm người chơi hiểu điểm bị dời là vị trí thân vật; cách xử lý che khuất, phía sau camera và chuyển giữa các cạnh còn phải kiểm trong Unity.
- [SUY LUẬN] Validator Ruby đi kèm skill chưa chạy vì KHÔNG TÌM THẤY Ruby trong PATH và các vị trí đã kiểm. Kiểm giới hạn bằng script đối chiếu chín khóa gốc, 18 parent-path, 11 candidate `hold`, bounds trong ảnh và hai danh sách rỗng; chưa chạy parser YAML hoặc validator chính thức, không tuyên bố kế hoạch đã qua validator.

## Giai đoạn 2 — asset có và thiếu

- [SUY LUẬN] `P`: panel 9-slice `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png`; ô phím dùng cùng `P`, không thêm kiểu khung khác.
- [SUY LUẬN] `LOCK`, `DONE`, `ARROW`: `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Locked.png`, `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Complete.png`, `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Arrow.png`. Manifest `E:/Unity/Project/FPS/Documents/UIUX/hud-skin-assets.json` ghi nguồn SunGraphica; yêu cầu credit trong license nêu ở style guide.
- [SUY LUẬN] `MARK`, `AIM`: `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/GoalMarkerCentre.png`, `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/ADSReflexDot.png`. `MARK` thuộc FPSSample, license tại `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/LICENSE.md`; nguồn pack và giấy phép gốc của `AIM` **KHÔNG TÌM THẤY trong phạm vi kiểm**, không coi là đã xác minh.
- [SUY LUẬN] `TRACK`, `FILL`: `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Bars/HealthTrack.png` và `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Bars/HealthFill.png`; nguồn Lotus Garden, license `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Licenses/Lotus-Post-Apocalypse-UI.txt`.
- [SUY LUẬN] `RING`: `E:/Unity/Project/FPS/Assets/FPS/Features/Survival/Content/Textures/ProgressRing.png`; ảnh phác dùng chính silhouette sprite và mask tiến trình, không vẽ icon mới. Nguồn và giấy phép gốc **KHÔNG TÌM THẤY trong phạm vi kiểm**; không coi là sẵn sàng phát hành.
- [SUY LUẬN] `ITEMS`: `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Frag.png`, `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Fire.png`, `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Medkit.png`, `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Antidote.png`. Ảnh phác chỉ lọc xám các icon kế thừa để diễn tả chỗ đặt: **GREYBOX PLACEHOLDER**, không phải asset đơn sắc được duyệt; license gốc các icon này **KHÔNG TÌM THẤY trong phạm vi kiểm**.
- [SUY LUẬN] Biển/decal gắn đúng từng vật, tín hiệu nhận diện proxy trước khi ngắm, binding theo dõi, âm hoàn tất và tên trường lỗi thật: **KHÔNG TÌM THẤY/CHƯA XÁC MINH**. Biển/decal và nhận diện proxy là **GREYBOX PLACEHOLDER**, không vẽ thêm prop, không import, không suy ra asset không tồn tại ở toàn bộ kho.

## Giai đoạn 2 — đặc tả từng thành phần

- [SUY LUẬN] Ký hiệu trạng thái trong bảng: B = bình thường; K = khóa; G = đang giữ/thả sớm; H = hoàn tất; L = lỗi. `N/A` nghĩa là trạng thái tương tác đó không làm thành phần đổi riêng, không phải trường phản hồi bị bỏ trống. Màu và cỡ đều lấy trong style guide; thời lượng đánh dấu dự kiến chưa phải hành vi runtime đã kiểm.

| Thành phần / asset | Cỡ và màu | B / K / G / H / L |
|---|---|---|
| Tracker; `P` | [SUY LUẬN] Objective 36 trắng; hai nhánh 26 trắng, tiến độ và phím 20 trắng/xám. | [SUY LUẬN] B: một dòng việc, `▶ POWER ○ ○ ○ 0/3`, `LOGISTICS ○ ○ 0/2` và ô `T` riêng; K/G: vẫn hiện cả hai nhánh nếu objective còn hiệu lực; H: thay dữ liệu sau công bố trạng thái mới; L: không tự tăng tiến độ. Hiện liên tục khi có objective. |
| Prompt / nhãn màn hình; `P`, `LOCK`, `DONE` | [SUY LUẬN] Tên/lý do/kết quả 26 trắng; ô binding 20, cam chỉ ở `F` mở được; nền xám. | [SUY LUẬN] B: `F` + `Bản kê vận chuyển`; K: icon khóa + tên vật + `Cần: [điều kiện từ logic]`, không `F`, ô `?` theo dõi tách riêng; G: prompt nhường chỗ vòng; H: icon dấu tick + `ĐÃ HOÀN TẤT`; L: `Sai: [trường từ logic]`. B/K chỉ hiện khi ngắm mục tiêu phù hợp; H dự kiến 1,5 giây, L tối thiểu 3 giây hoặc theo nguồn thông báo. |
| Marker trong/ngoài màn; `P`, `MARK`, `ARROW` | [SUY LUẬN] Tên và khoảng cách 26 trắng, nền xám. | [SUY LUẬN] B/K: icon + tên thân mục tiêu + khoảng cách, hoặc mũi tên tại cạnh nếu ngoài màn; G: giữ đích hợp lệ; H: chỉ chuyển sang đích kế sau khi dữ liệu mới được công bố; L: giữ đích nếu logic còn xác nhận hợp lệ. Hiện mọi cự ly khi có mục tiêu hợp lệ; đường đi rỗng cần fallback đường thẳng có nhãn đúng loại khoảng cách. |
| Máu/nhiễm; `P`, `TRACK`, `FILL` | [SUY LUẬN] Nhãn 20 trắng/xám, số 26 trắng; đỏ chỉ khi logic báo nguy hiểm. | [SUY LUẬN] B: `80 / 100`, `NHIỄM 20%` minh họa; fill máu 80% chiều rộng track, dải nhiễm 20% chiều rộng cùng track đè ở mép dưới. K/G/H/L: N/A; dữ liệu hai chỉ số vẫn cập nhật độc lập, không tự trừ máu vì nhiễm. Hiện trong gameplay. |
| Đạn; `P` | [SUY LUẬN] Đạn đang có 36 trắng; dự trữ 26 và `WARDEN` 20 trắng/xám. | [SUY LUẬN] B: `15 / 45`, tên súng; K/G/H/L: N/A theo trạng thái tương tác; hiển thị khi đang dùng vũ khí. Không đưa `FIRE`, `AIM`, `CLASSIC` vào panel này. |
| Hotbar; `P`, `ITEMS` là placeholder | [SUY LUẬN] Phím và số lượng 20 trắng, nền xám; không dùng cam trang trí. | [SUY LUẬN] B: bốn ô minh họa, hàng `1 … ×2` riêng phía trên icon; K/G/H/L: N/A theo trạng thái tương tác. Có thể thu gọn nhưng cách ẩn/hiện chưa chọn; không tự thêm binding. |
| Vòng giữ và tâm; `RING`, `AIM` | [SUY LUẬN] Phím giữa 26 trắng, caption 20 trắng; vòng giữ cam, vòng sau hủy trắng/xám. | [SUY LUẬN] B/K: không hiện vòng; G: vòng `F`, `ĐANG GIỮ · 100%`; thả sớm hiện `ĐÃ HỦY · ĐANG LÙI` và vòng 65% → 30% → 0%, dự kiến lùi trong 0,4 giây; H: ẩn vòng và hiện dấu tick trong prompt; L: ẩn vòng, hiện trường sai. `100%` không tự chứng minh server đã xác nhận. |
| Subtitle; `P` | [SUY LUẬN] 26 trắng, viền xám, nền panel alpha 0,8, rộng tối đa 675 px tại ảnh 1080p. | [SUY LUẬN] B/K/G/H/L: hiện chuỗi được nguồn subtitle gửi khi có nội dung; ảnh B ghi `Nguồn cần đọc: bản kê vận chuyển.`. Không chạm panel đạn ở ảnh mẫu; chữ dài, hai dòng và animation chưa kiểm. |
| Nhãn gắn vật / screen tag; `P`; biển/decal thiếu | [SUY LUẬN] Nhãn màn hình 26 trắng trên xám; không dùng chữ 3D lơ lửng. | [SUY LUẬN] B: tên vật trong prompt khi ngắm; K: lý do khóa tại cùng vùng; G/H/L: lần lượt dùng vòng, kết quả hoặc tên trường sai. Biển gắn vật và nhận diện proxy trước khi ngắm là **GREYBOX PLACEHOLDER**, chưa chứng minh đã có asset phù hợp hoặc điểm neo trên vật. |

- [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:462] Code subtitle hiện có đặt `subtitleUntil` theo giá trị lớn nhất của 6 giây, độ dài chữ × 0,06 giây và thời lượng audio nếu có; chưa playtest hoặc chứng minh mockup HTML điều khiển thời lượng này.
- [SUY LUẬN] Phản hồi âm cho B/K/G/hủy/H/L: tên âm và asset âm **KHÔNG TÌM THẤY trong phạm vi Bot 4**. Riêng H đang thiếu âm hoàn tất bắt buộc: ảnh dấu tick và chữ không thể thay thế âm; Bot feedback cần chỉ định, Bot 4 không tự tạo/sửa âm. Thời lượng H/L và 0,4 giây lùi ở bảng chỉ là đề xuất để duyệt, không phải số đo.
- [SUY LUẬN] Ảnh hiện mô tả việc và nơi đến nhưng chưa có câu đã được flow duyệt về **kết quả người chơi sẽ thấy sau khi làm**; vì vậy chưa thể tuyên bố đạt 5-second test. Cần chuỗi kết quả quan sát được từ logic/flow, không tự bịa hoặc đưa đáp án vào tracker/hint.

## Giai đoạn 2 — hợp đồng dữ liệu cho Bot 5

- [SUY LUẬN] Đây là hợp đồng **đề xuất**, không khẳng định field/API đã có và không cho phép Bot 4 sửa logic. Server quyết định điều kiện, pha và hoàn tất; client hiển thị trạng thái được công bố và gửi yêu cầu qua binding thực tế.
- [SUY LUẬN] `objective`: mã/phiên bản, tên một dòng đủ ngắn, phase, trạng thái, `hintSourceId` để lấy hint từ `CampaignContentCatalog`, và mô tả kết quả có thể quan sát đã được flow duyệt. Hint chỉ nêu nguồn cần đọc, không chứa đáp án; nếu chuỗi một dòng quá dài phải giải quyết không làm mất chỉ dẫn, chưa coi dấu `…` trong HTML là giải pháp phát hành.
- [SUY LUẬN] `branches[2]`: mã/tên từng nhánh, `current`, `total`, trạng thái khả dụng và `activeBranchId`; `switchBranchBinding` lấy từ cấu hình input. Cần công bố cả hai nhánh trong cùng phiên bản; chỉ nhánh đang theo dõi có `▶`.
- [SUY LUẬN] `interaction`: mã/tên vật, trạng thái mở được/khóa/đang giữ/hoàn tất/lỗi, `lockReason` theo dữ liệu thật, `interactBinding`, `trackBinding` riêng và điều kiện hiển thị. Khi khóa không gửi gợi ý `F`; binding `?` chỉ là placeholder. Lỗi cần `mismatchedFieldLabel` cụ thể, không suy ra hay hiển thị đáp án.
- [SUY LUẬN] `hold`: binding, thời lượng yêu cầu, tiến trình `0..1`, sự kiện bắt đầu/giữ/thả sớm/hủy, kết quả server và phiên bản mục tiêu. Khi thả sớm HUD chạy hiệu ứng lùi đã duyệt nhưng không coi vòng 100% là xác nhận; tên âm và thời lượng feedback vẫn chờ Bot phụ trách.
- [SUY LUẬN] `target`: id và vị trí **thân tương tác**, tên, khoảng cách số + đơn vị + loại đường đi/đường thẳng, trạng thái route có/không NavMesh, vị trí mục tiêu so với camera và trạng thái có/không mục tiêu hợp lệ. Khi không có route phải có fallback đường thẳng; HUD không được gắn nhãn `ROUTE` cho khoảng cách đường thẳng.
- [SUY LUẬN] `nextTarget`: trạng thái chờ/đã công bố/không có, id/phiên bản mới và dữ liệu mục tiêu mới; công bố xong mới chuyển marker và objective. Nếu chưa có đích kế, không dựng tên/vị trí đích bằng suy đoán; dữ liệu cũ hết hiệu lực thì không tiếp tục chỉ đích cũ như thể còn hợp lệ.
- [SUY LUẬN] `vitals`, `weapon`, `hotbar`, `subtitle`: HP hiện tại/tối đa, nhiễm độc lập và cờ nguy hiểm; đạn hiện tại/dự trữ/tên súng; mỗi slot gồm icon, binding, số lượng và trạng thái hiện; chuỗi subtitle, thời điểm bắt đầu/kết thúc hoặc thời lượng. Update theo sự kiện khi dữ liệu đổi; không khẳng định logic nguồn đã có các sự kiện này.
- [SUY LUẬN] `viewmodelExclusion`/thông tin camera cần đủ để kiểm marker tránh vùng súng ở tư thế ngắm/bắn khác nhau; định dạng, owner tính chiếu và xử lý che khuất chưa thống nhất. Không coi rect ví dụ tĩnh trong ảnh là hợp đồng cho mọi vũ khí.

## Duyệt mockup

- [SUY LUẬN] Người dùng cần duyệt trực tiếp bộ mockup A hoặc yêu cầu chỉnh; sau đó mới xét cổng flow đã duyệt, item rect/asset plan và triển khai. Bot 4 không tự tuyên bố duyệt thị giác và không bắt đầu code.

## Đề xuất ngoài phạm vi

- [SUY LUẬN] Bot phụ trách asset/giấy phép nên xác minh nguồn `ProgressRing.png` và icon vật phẩm kế thừa, đồng thời tìm icon đơn sắc có sẵn; nếu thiếu thì trình phương án riêng trước khi tạo/sửa asset. Không tự gọi imagegen.
- [SUY LUẬN] Bot phụ trách feedback/âm thanh nên xác định tên âm hoàn tất, hiệu ứng và thời lượng cho hoàn tất/hủy/sai; Bot 4 không sửa audio. Việc xác minh marker, lý do khóa và authority host/client thuộc bước triển khai được duyệt sau này.
- [SUY LUẬN] Bot phụ trách flow/logic nên cung cấp kết quả người chơi sẽ thấy, lý do khóa và tên trường lệch từ flow được duyệt; nếu thiếu các dữ liệu này, không thể chứng minh 5-second test hay hoàn tất đặc tả lỗi.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] `isDirty == false` khi truy vấn không chứng minh Git sạch, asset không chờ lưu, nội dung scene không thay đổi giữa hai lượt, hay ảnh cũ phản ánh phiên hiện tại.
- [SUY LUẬN] Bảng màu là đề xuất token, không phải bằng chứng toàn bộ pixel sprite hiện có chỉ gồm bốn màu. Icon đa sắc và border importer được ghi rõ là chưa đạt xác minh, không đồng thời tuyên bố tuân thủ.
- [SUY LUẬN] Font TTF và TMP asset tồn tại, giấy phép đã dẫn; không vì vậy khẳng định TMP/SDF đủ mọi glyph tiếng Việt. `GoalProgress.png` có sẵn không đồng nghĩa nó là ảnh vòng rỗng hoặc đã playtest.
- [SUY LUẬN] Bỏ chữ 3D lơ lửng hoặc bản lặp `Next: ...` là đề xuất trình bày, không phải xóa objective, marker hay thông tin độc nhất. Không nhầm phản hồi được đề xuất với phản hồi đã chạy.
- [SUY LUẬN] Câu “chưa chọn/chưa có mockup” được giới hạn vào thời điểm giai đoạn 1; người dùng đã chọn A và giai đoạn 2 đã có bản phác, nhưng mockup chưa được duyệt. 1,5 giây/3 giây/0,4 giây là đề xuất, không mâu thuẫn với việc chưa đo runtime.
- [SUY LUẬN] Ảnh nền sáng/tối chỉ được kiểm trực quan trong trình duyệt; không nhầm khoảng cách 306 px hay vị trí marker trong ảnh tĩnh với xác minh tương phản định lượng, mọi góc súng, Game view hoặc playtest.

## Chưa kiểm được

- [SUY LUẬN] Không có Game view mới hoặc Play Mode; chưa xác minh thời lượng, âm thanh, hiệu ứng, binding, host/client, NavMesh, tương phản định lượng hoặc bố cục Unity ở 1080p, 4K và tỉ lệ khác.
- [SUY LUẬN] Ảnh HUD tạm đã biến mất sau khi được xem. Unity-Skills không trả lời ở lần kiểm tra cuối của giai đoạn 1; trong giai đoạn 2 không phát hiện tiến trình Unity Editor dự án và không mở Editor, nên không thể xác nhận `isDirty` tại thời điểm kết thúc, dù lần truy vấn thành công gần nhất báo `false`. **Save Gate chưa được xác minh cuối**; cần kiểm khi Unity-Skills của dự án sẵn sàng, nếu scene dirty thì dừng và không lưu.
- [SUY LUẬN] Chưa kiểm border 9-slice thực tế trong importer, glyph tiếng Việt/fallback, pixel icon so với giới hạn màu, giấy phép `ProgressRing.png`, `ADSReflexDot.png` và icon vật phẩm kế thừa; truy vấn asset chỉ bao phủ nhóm đã nêu, không chứng minh toàn bộ kho thiếu asset khác.
- [SUY LUẬN] Chưa có binding theo dõi, lý do khóa thật, tên trường lỗi, tín hiệu nhận biết proxy trước khi ngắm, âm hoàn tất hoặc mô tả kết quả đã duyệt; ảnh minh họa không thay các dữ liệu đó. Chưa kiểm marker tránh vùng súng ở mọi góc nhìn hay đi bộ giữa các mục tiêu.
- [SUY LUẬN] Chưa chạy Ruby validator hoặc parser YAML; chưa có duyệt mockup, chưa xác nhận cổng flow và chưa có quyền triển khai code. Phân tích rect bằng mẫu văn bản không chứng minh file đúng toàn bộ schema hoặc Unity sẽ dựng đúng bố cục.


---

# Bản A chỉnh sửa theo phản hồi ngày 29/10/2026

- [SUY LUẬN] Người dùng không duyệt bản A cũ vì prompt và waypoint có panel che màn hình, viền dày, chưa có điều chỉnh opacity và chữ tiếng Việt. Bản mới tiếp tục chờ người dùng duyệt; không được coi là phê duyệt flow, mockup hoặc triển khai.
- [SUY LUẬN] Phần trước dấu phân cách là lịch sử phương án cũ, giữ để truy vết: các ảnh, kích thước, bảng trạng thái, cây YAML và kiểu panel cũ không mô tả bản sửa. Bảng hợp đồng dữ liệu ở phần cũ vẫn là đề xuất cho Bot 5, trừ cách hiển thị đã thay ở đây.
- [SUY LUẬN] Đã đọc lại `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md` và `references/ui-planning-workflow.md`: giữ cây theo parent/owner, tách việc đã chọn với lựa chọn cần duyệt và không chuyển mockup thành Unity objects. Đã đọc `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`: đề xuất neo theo góc và safe area, không coi tọa độ 1080p là layout runtime. `create-game-assets/SKILL.md` KHÔNG TÌM THẤY; không nói đã áp dụng skill này. Kiểm asset trước theo AGENTS.md.

## Tham khảo internet

- [SUY LUẬN] Đã xem trang kết quả ảnh tìm kiếm cho Resident Evil Requiem, Genshin Impact, Left 4 Dead 2 và Valorant. Các thumbnail Requiem cho thấy một số khung gameplay gần như không có panel thông tin lớn, nhưng ảnh quá nhỏ và lẫn bản chỉnh sửa; không đủ chứng minh bố cục HUD chuẩn. Kết quả L4D2 lẫn nhiều bản mod và Valorant có cả ảnh spectator/shop; không lấy làm bằng chứng HUD mặc định. Kết quả Genshin lẫn teleport map với quest marker; chưa kiểm được cách waypoint chính xác. Không sao chép asset, không khẳng định phiên bản mới có thiết kế giống các game này.
- [SUY LUẬN] Trang tìm kiếm để tái kiểm: https://www.google.com/search?tbm=isch&q=Resident+Evil+Requiem+gameplay+HUD+screenshots ; https://www.google.com/search?tbm=isch&q=Genshin+Impact+quest+navigation+waypoint+distance+gameplay+HUD ; https://www.google.com/search?tbm=isch&q=left+4+dead+2+gameplay+hud+ammo+inventory ; https://www.google.com/search?tbm=isch&q=valorant+gameplay+hud+ammo+weapons+inventory . Cần mở nguồn gameplay rõ ràng để đối chiếu trước khi quy thiết kế cụ thể cho từng game.

## Hướng A sửa và ảnh

- [SUY LUẬN] `E:/Unity/Project/FPS/Reports/mockups/04-hud-a-revision.html` là ảnh phác bằng HTML, không phải asset import hoặc gameplay. `E:/Unity/Project/FPS/Reports/mockups/04-a-revision-bright.png` là nền trời sáng có viewmodel; `E:/Unity/Project/FPS/Reports/mockups/04-a-revision-normal.png` là nền nhà máy tối không có viewmodel. Vùng tối sát mép trên trái ảnh sáng thuộc ảnh nền lưu trữ, không phải panel mới.
- [SUY LUẬN] Trong ảnh mới: prompt chỉ có `[F] Shipping Manifest` dưới tâm; waypoint chỉ có icon, tên và `21 m`, không nền panel; tracker trên trái, máu/nhiễm dưới trái, item và đạn dưới phải. Không dùng panel 9-slice ở các cụm này; panel `Panel.png` là ứng viên cho màn hình khác khi thật cần, chưa xác minh border importer và chưa chọn để sử dụng trong HUD mới.
- [SUY LUẬN] Bản phác dùng chữ trắng `#F2F2F2`, cam `#F2A436` chỉ cho `[F]` và vòng hành động đang giữ, xám `#25282A` cho bóng chữ; đỏ `#D84B4B` dự trữ cho nguy hiểm, không hiện trong ảnh. Ba cỡ tham chiếu: 36/26/20 px; chữ Liberation Sans và số Teko Bold là font có sẵn, giấy phép như phần lịch sử. Pixel hình nền và icon chưa được chứng minh chỉ có bốn màu token.
- [SUY LUẬN] Ví dụ trượt opacity tại `E:/Unity/Project/FPS/Reports/mockups/04-a-revision-opacity70.png` và HTML với `?background=bright&controls=1`: thử 70–100%, mặc định 100%, làm mờ toàn bộ nhóm HUD (gồm cả chữ và icon), không phải setting trong game. Giảm alpha chữ có thể gây mất tương phản: 70% chỉ là giới hạn thử, chưa xác định giá trị an toàn hoặc chính sách opacity riêng cho thông tin nguy hiểm; phần trượt là công cụ duyệt, không xuất hiện khi gameplay.
- [SUY LUẬN] Không vẽ lời thoại trong các ảnh mới theo yêu cầu; không kết luận xóa hệ thống subtitle. Đặc tả nền mờ/viền chữ trong yêu cầu gốc còn chờ thống nhất khi thực sự cần subtitle, không coi ảnh cũ là đề xuất hiện tại.
- [SUY LUẬN] `E:/Unity/Project/FPS/Reports/mockups/04-a-revision-locked.png` có khóa + tên + `Requires: [condition from logic]`, không có `[F]`; `[?]` là placeholder binding theo dõi chưa xác minh. Marker trái là minh họa ngoài màn hình, không phải phép chiếu runtime.
- [SUY LUẬN] `04-a-revision-hold100.png`, `04-a-revision-release65.png` và `04-a-revision-release30.png` trong `E:/Unity/Project/FPS/Reports/mockups/` là ba khung tĩnh vòng giữ/thả, không xác minh thời lượng hay animation. `04-a-revision-complete.png` và `04-a-revision-error.png` minh họa xác nhận và trường sai; chữ trong ngoặc là dữ liệu giả, không phải nội dung nhiệm vụ được duyệt. Âm hoàn tất KHÔNG TÌM THẤY trong phạm vi đã kiểm.
- [SUY LUẬN] Các icon vật phẩm hiện có được khử màu để minh họa và đánh dấu GREYBOX PLACEHOLDER; nguồn/giấy phép icon và vòng vẫn chưa đủ căn cứ phát hành. Tên súng, số đạn, HP, nhiễm, tiến độ và khoảng cách là ví dụ, không phải số liệu gameplay. Bóng chữ không bảo đảm tương phản cho mọi khung hình.

## Cây bố cục sửa — đề xuất, chưa duyệt

```text
HUD / SafeArea
├─ Tracker: Objective / Branch × 2 / [T] Switch
├─ Marker: Onscreen icon + name + distance | Edge arrow
├─ CenterFeedback: Reticle | Hold ring + key + status
├─ Interaction: Open | Locked + reason + track key | Result
├─ Vitals: Health track + infection overlay
├─ Hotbar: Slot × 4 (key/count above icon)
└─ Ammo: weapon name / current + reserve
```

- [SUY LUẬN] Cây trên thay thế ý định hiển thị trong `E:/Unity/Project/FPS/Reports/mockups/04-hud-layout-plan.yaml`; YAML cũ giữ nguyên làm lịch sử, rect và candidate của nó KHÔNG dùng cho bản sửa. Chưa có ledger/rect mới theo template, không coi cây này đã qua validator hoặc đủ để thực thi.
- [SUY LUẬN] Các phần tương ứng ở góc màn hình cần owner safe area và scaling; marker cần dữ liệu vị trí thân tương tác, khoảng cách, hướng ở ngoài màn và vùng tránh viewmodel. Marker chỉ đổi sau khi mục tiêu mới được công bố. Hợp đồng Bot 5 ở phần lịch sử vẫn cần lý do khóa, hai nhánh/nhánh active, mục tiêu kế, khoảng cách và progress giữ.

## Đề xuất

- [SUY LUẬN] Bot triển khai sau khi flow và mockup được duyệt cần kiểm opacity trên cảnh sáng, tối và lúc nguy hiểm; đề xuất bảo vệ độ đọc của thông tin thiết yếu thay vì mặc nhiên cho mọi thành phần mờ tới mức khó thấy. Bot 4 không sửa Settings hoặc logic.
- [SUY LUẬN] Bot phụ trách asset/feedback cần xác minh license các icon/vòng, chọn âm và hiệu ứng hoàn tất, binding theo dõi và biển/decal hoặc tín hiệu nhận biết proxy; hiện các mục chưa có bằng chứng là GREYBOX PLACEHOLDER hoặc KHÔNG TÌM THẤY.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Ảnh A cũ có panel và subtitle; bản hiện hành bỏ panel khỏi prompt/marker và không hiển thị subtitle. Hai mô tả thuộc hai phiên bản khác nhau, không phải hai yêu cầu đồng thời. Slider trong HTML không đồng nghĩa chức năng trong game đã tồn tại.
- [SUY LUẬN] A đã được chọn là hướng phong cách, nhưng mockup cũ bị từ chối và bản sửa chưa được duyệt; chưa có quyền triển khai.

## Chưa kiểm được

- [SUY LUẬN] Không mở Unity hay Play Mode, không sửa scene/code/asset, không import hoặc gọi imagegen. Lần truy vấn Unity thành công gần nhất ở lượt trước báo `GameScene.isDirty == false`; Editor không chạy ở lượt này nên KHÔNG KIỂM ĐƯỢC `isDirty` lúc kết thúc, Save Gate chưa xác minh.
- [SUY LUẬN] Chưa kiểm Game view 1080p/4K, tỉ lệ khác, độ tương phản định lượng, chữ dài, animation, thời lượng, âm thanh, host/client, NavMesh hay né viewmodel trong chuyển động. Ảnh phác không phải playtest; chưa xác minh hiệu lực 9-slice hoặc tạo rect/asset plan mới.
- [SUY LUẬN] Các ảnh tìm kiếm nhỏ, lẫn mod/spectator nên chưa xác nhận bố cục bốn game ở nguồn gameplay gốc; chưa tìm được bằng chứng đủ rõ để trình bày so sánh chi tiết từng game.


---

# Bản A v3 — nghiên cứu và điều chỉnh ngày 30/10/2026

- [SUY LUẬN] Xin lỗi: phản hồi trước đã hiểu sai. Người dùng chỉ yêu cầu bỏ panel đen lớn ở **prompt tương tác và waypoint**, không yêu cầu xóa panel của tracker, máu, hotbar và đạn. Bản `04-hud-a-revision` bị bác; phần trước chỉ lưu lịch sử, không phải đặc tả hiện hành. Mockup v3 chưa được duyệt.
- [SUY LUẬN] Đã đọc `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md`: kiểm ảnh/asset trước, phân biệt mockup và xác minh Unity, đề xuất cây ownership và safe area; đọc `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`: neo theo cạnh, dự kiến thử kích thước và độ đọc. `create-game-assets/SKILL.md`: KHÔNG TÌM THẤY tại các root đã kiểm, không khẳng định đã đọc hoặc áp dụng. Kiểm tài nguyên hiện có theo `AGENTS.md` và `Reports/02-ui-assets.md`.

## Tư liệu đã xem trực tiếp

- [SUY LUẬN] **Resident Evil Requiem — HUD**: https://interfaceingame.com/screenshots/resident-evil-requiem-hud/ . Ảnh nhân vật trong hành lang có cụm súng/đạn nhỏ ở góc dưới phải, nhiều khoảng trống ở giữa. **Tương tác**: https://interfaceingame.com/screenshots/resident-evil-requiem-interact/ ; chữ `F` và `Infected Blood (70)` nằm cạnh vật trên dải tối rất nhẹ, không có panel lớn. Học cách ưu tiên ngữ cảnh và không chiếm tâm nhìn; không kết luận prompt gốc hoàn toàn không có nền.
- [SUY LUẬN] **Requiem — inventory**, https://interfaceingame.com/screenshots/resident-evil-requiem-inventory/ : ảnh menu kho có lưới item tối ở giữa, ô đang chọn nổi hơn, khu mô tả bên dưới; đây là overlay kho chứ không phải HUD liên tục. Nó cho thấy dùng panel theo ngữ cảnh không mâu thuẫn với prompt gọn. Không sao chép nguyên lưới vào HUD gameplay.
- [SUY LUẬN] **Left 4 Dead 2 — damage**, https://interfaceingame.com/screenshots/left-4-dead-2-damage/ : ảnh trạng thái bị hạ có cụm vật phẩm dọc sát mép phải, icon súng và `32`/`603` gần đầu cụm, dải đồng đội ở mép dưới. **Healing**, https://interfaceingame.com/screenshots/left-4-dead-2-healing-yourself/ : `HEALING YOURSELF` kèm thanh tiến trình nhỏ gần giữa. Học gom item và tách số đạn; không coi ảnh bị hạ là bố cục mặc định mọi trạng thái.
- [SUY LUẬN] **Valorant — headshot**, https://interfaceingame.com/screenshots/valorant-headshot/ : `14` lớn cạnh `39` nhỏ ở dưới lệch phải, kỹ năng thành dãy dưới giữa, máu `100` dưới lệch trái; ảnh là spectator có `SWITCH PLAYER` và `COMBAT REPORT`, không phải bằng chứng HUD người chơi bình thường. **Ability**, https://interfaceingame.com/screenshots/valorant-ability/ : lúc dùng kỹ năng, bản đồ khả năng tròn chiếm giữa; máu và dãy kỹ năng vẫn ở mép dưới. Chỉ mượn thứ bậc số và nhóm chức năng, không mang overlay kỹ năng sang game này.
- [SUY LUẬN] **Genshin Impact**, https://www.heypoorplayer.com/2023/08/18/how-to-complete-the-aqueous-tidemarks-quest-in-genshin-impact/ : đã mở ảnh `genshinaqueoustidemarkspicture14.jpg` và `genshinaqueoustidemarks2.jpg`; tên và mục tiêu quest nằm sát trái dưới minimap, văn bản nhỏ không có panel lớn; ảnh 14 có các vệt sáng đứng trong cảnh, bài viết gọi các điểm quan tâm là golden beacons. Ảnh `genshinaqueoustidemarkspicture10.jpg` cho thấy dòng mục tiêu và `Quest area reached` gần minimap. Các ảnh này **không chứng minh rõ** cách marker quest hiển thị số mét hay mũi tên ngoài màn hình; KHÔNG TÌM THẤY ảnh đủ rõ cho hai trạng thái đó, không coi teleport waypoint là quest marker.
- [SUY LUẬN] Đây là ảnh bên thứ ba, từng thời điểm; chưa xác nhận cài đặt/mod, ngữ cảnh đầy đủ, thời gian hiển thị hay hành vi chuyển động. Không sao chép tài sản từ các game, không suy từ ảnh tĩnh ra toàn bộ HUD hoặc các phiên bản game.

## Style guide và bố cục v3 — chưa duyệt

- [SUY LUẬN] Màn 1920×1080: tracker góc trên trái có panel; máu/nhiễm góc dưới trái có panel; hotbar và đạn có hai panel riêng góc dưới phải. Bốn panel cùng nguồn 9-slice `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png`, CSS phác biên 4 px thay vì viền trắng dày; border importer Unity chưa kiểm. Prompt dưới tâm và marker trong cảnh **không có panel**, có bóng chữ để thử đọc.
- [SUY LUẬN] Slider trong HTML thử opacity của **nền bốn panel**, từ 0–100%, mặc định minh họa 65%; chữ và icon vẫn 100%. Đây không phải cài đặt trong game, không chứng minh các mức thấp an toàn. Ô phím `[T]` và `[?]` dùng cùng sprite panel, không phải panel lớn bao prompt; nếu ô này vẫn gây chắn phải chờ nhận xét.
- [SUY LUẬN] Ba cỡ 36/26/20 px tham chiếu; chữ chính Liberation Sans, số Teko Bold, giấy phép OFL theo các đường dẫn ở phần đầu. Bốn token trắng `#F2F2F2`, cam `#F2A436` cho hành động ngay, đỏ `#D84B4B` dành nguy hiểm, xám `#25282A` phụ. Màu pixel của ảnh nền và sprite chưa được chứng minh thỏa giới hạn theme. Không hiện subtitle trong ảnh phác thường, không xóa chức năng subtitle; cách hiển thị khi có thoại chưa được thống nhất.
- [SUY LUẬN] `E:/Unity/Project/FPS/Reports/mockups/04-hud-a-v3.html` và các ảnh `04-v3-*.png` là mockup HTML ngoài Assets, không phải asset để import và không phải Game view. Ảnh `normal`, `dark`, `locked`, `hold100`, `release65`, `release30`, `complete`, `error` và `opacity30` là các khung tĩnh; không chứng minh animation hay âm. Chữ trên HUD là tiếng Anh; tên/số và dữ liệu trong ngoặc chỉ là ví dụ.
- [SUY LUẬN] Icon item đã khử màu chỉ minh họa **GREYBOX PLACEHOLDER**; khóa, vòng, mũi tên và thanh dùng asset ứng viên ghi ở bảng asset lịch sử; license icon và vòng chưa xác minh đầy đủ. Nhãn gắn vật/decal phù hợp từng mục tiêu **KHÔNG TÌM THẤY** trong phạm vi đã kiểm, **GREYBOX PLACEHOLDER**; cần nhận diện vật trước khi ngắm.

## Cây bố cục và trạng thái

`HUD/SafeArea` → `Tracker/Panel/Objective/Branch[2]/SwitchKey`; `Marker/OnScreen(icon,name,distance) | Edge(arrow,name,distance)`; `Center/Reticle/HoldRing/BoundKey`; `Interaction/Open | Locked(reason,trackKey) | Complete | Error(field)`; `Vitals/Panel/Health/InfectionOverlay`; `Hotbar/Panel/Slot[4](key,icon,count)`; `Ammo/Panel/Weapon/Current/Reserve`. [SUY LUẬN]

- [SUY LUẬN] Mở được: `[F] Shipping Manifest`; khóa: icon khóa + `Requires: [condition from logic]`, không `[F]`, `[?]` là phím theo dõi chưa biết. Giữ: vòng + `F`; hai ảnh thả minh họa lùi 65% rồi 30%; hoàn tất: `COMPLETED` và marker ví dụ sang mục tiêu kế; lỗi: `Mismatch: [field from logic]`. Chưa biết thời lượng, âm hoàn tất hay hiệu ứng runtime; không tự gán nội dung/đáp án.
- [SUY LUẬN] Tracker hai nhánh, chỉ nhánh active có `▶`, `[T]` riêng; máu và nhiễm chung thanh nhưng hai đại lượng độc lập; số đạn hiện tại lớn, dự trữ và tên nhỏ; không hiển thị `FIRE/AIM/CLASSIC`; số lượng và phím tách khỏi icon. Marker offscreen là hình minh họa, chưa chứng minh thuật toán tránh súng.
- [SUY LUẬN] Cây này là sơ đồ đề xuất, dựa trên template `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/templates/mockup-layout-plan.yaml`, chưa có ledger/rect v3 theo schema. `04-hud-layout-plan.yaml` thuộc bản cũ, không dùng để triển khai v3. Cần candidate review, rect và asset plan sau khi có cơ sở và duyệt; không nhận là đã qua validator.

## Hợp đồng dữ liệu đề xuất cho Bot 5

- [SUY LUẬN] Cần objective hiện tại, nội dung từng nhánh và tiến độ, ID nhánh active, binding đổi nhánh; nội dung hint chỉ tên nguồn cần đọc từ `CampaignContentCatalog`, không chứa đáp án.
- [SUY LUẬN] Cần trạng thái mở/khóa, tên vật, lý do khóa từ logic, binding interact/theo dõi; trạng thái hold, tiến trình và hủy; trạng thái hoàn tất/lỗi cùng trường sai, tín hiệu âm/hình. Marker cần mục tiêu mới được công bố trước, vị trí thân tương tác, tên, khoảng cách và hướng khi ngoài màn; route fallback khi thiếu NavMesh là trách nhiệm logic. Số máu, nhiễm, đạn, tên súng, hotbar key/count và opacity cũng cần nguồn dữ liệu rõ ràng; đây là đề xuất hợp đồng, không chứng minh đã tồn tại.

## Đề xuất

- [SUY LUẬN] Tìm thêm ảnh/video quest marker Genshin đang theo dõi, có số mét và mục tiêu ngoài màn; đối chiếu theo thời điểm, không áp một ngưỡng khoảng cách chưa xác minh. Đo độ đọc panel ở nhiều opacity trên nền sáng/tối và trong chuyển động trước khi quyết định giới hạn thực tế.
- [SUY LUẬN] Sau khi người dùng duyệt flow/mockup, nhóm phụ trách mới xác minh license, phím, âm hoàn tất, proxy/decal và thử 1080p/4K; Bot 4 không sửa hệ thống Settings, gameplay hoặc asset.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Mô tả cũ bỏ mọi panel đã bị bác và không còn hiệu lực; v3 chỉ bỏ panel của prompt và marker, giữ bốn cụm panel. Slider v3 tác động nền panel, không làm mờ toàn HUD như bản bị bác.
- [SUY LUẬN] `A` là lựa chọn phong cách, không phải duyệt mockup; ảnh HTML là minh họa, không phải playtest hay bằng chứng chức năng trong Unity.

## Chưa kiểm được

- [SUY LUẬN] Chưa xác minh marker Genshin có khoảng cách/mũi tên offscreen bằng ảnh rõ; chưa kiểm gameplay gốc, thời gian, độ tương phản định lượng, chữ dài, tỷ lệ khác hay né súng trong chuyển động. Đã chụp HTML 1080p, chưa chụp Game view 1080p/4K hay playtest.
- [SUY LUẬN] Không mở Unity Editor/Play Mode, không sửa scene/code/prefab/asset, không import và không gọi imagegen. Lần đọc scene cũ ghi `isDirty == false`, nhưng không kiểm lại được lúc kết thúc vì Unity không chạy; Save Gate cuối **chưa xác minh**.


---

# Nghiên cứu thẩm mỹ HUD — 30/10/2026

- [SUY LUẬN] Bản A v3 đã bị người dùng đánh giá xấu; không xem là bản đề xuất hiện hành hay bản được duyệt. Lần này nghiên cứu thị giác, không đưa thêm mockup vội và không sửa code, scene, prefab hoặc asset. Bố cục tiếp theo cần được người dùng xem và duyệt trước khi triển khai.
- [SUY LUẬN] Đã đọc `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md` và `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/references/ui-planning-workflow.md`: đối chiếu hình và tài nguyên trước, xem quan hệ nhóm/ownership và giữ cổng duyệt mockup. Đã đọc `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`: xem safe area, khả năng đọc và nhiều tỷ lệ màn hình, không coi tọa độ một ảnh là layout runtime. `create-game-assets/SKILL.md` KHÔNG TÌM THẤY trong các root đã kiểm; không tuyên bố đã đọc hay áp dụng. Kiểm tài nguyên theo `AGENTS.md` và `Reports/02-ui-assets.md`.
- [SUY LUẬN] Phương pháp: mở ảnh gameplay/overlay ở trang nguồn, quan sát cả khung và vùng HUD; tách **thấy trong ảnh** khỏi **cách diễn giải/đề xuất**. Tỷ lệ nêu dưới đây là ước lượng quan sát trên ảnh đang hiển thị, không phải đo source pixel hay thông số gốc. Ảnh web là của bên thứ ba, chưa kiểm bản game/cài đặt/mod; không sao chép tài nguyên.

## 1. Resident Evil Requiem — im lặng thị giác và nhóm theo ngữ cảnh

- [SUY LUẬN] Ảnh HUD: https://interfaceingame.com/screenshots/resident-evil-requiem-hud/ . Nhân vật ở nửa trái, hành lang và điểm quan sát ở giữa; một cụm súng/đạn/đường cong nhỏ ở dưới phải, gần sát góc. Trong ảnh hiển thị, cụm này rộng cỡ 1/10 khung hình; đa số diện tích ảnh để cho môi trường. Các con số `7` và `30` nhỏ sát biểu tượng súng, không có thẻ vuông lớn bao trọn khu vực.
- [SUY LUẬN] Phân tích: các thành phần chạm nhau thành một khối nhỏ có quan hệ hình học (đường cong, súng và số), không phải nhiều hộp rời có viền ngang nhau; do đó ít cạnh cạnh tranh với cảnh. Đây là diễn giải của người thiết kế, không phải tuyên bố về ý đồ của Capcom hoặc luật mọi HUD Requiem.
- [SUY LUẬN] Ảnh interact: https://interfaceingame.com/screenshots/resident-evil-requiem-interact/ . Tâm hình gần vật là một nút `F` có hình, các biểu tượng nhỏ và chữ `Infected Blood (70)` xếp trên một trục ngang; nền tối mảnh gần chữ, không chiếm thành một bảng lớn. Icon và key nổi hơn nhãn; chữ nhỏ nằm gần vật, khoảng trống bốn phía vẫn là ảnh hiện trường. Trong ví dụ này chi tiết màu đỏ là của ảnh tham khảo, không tự mang đỏ sang game nếu logic không báo nguy hiểm.
- [SUY LUẬN] Ảnh inventory: https://interfaceingame.com/screenshots/resident-evil-requiem-inventory/ . Khi mở kho, một mặt tối lớn chiếm giữa, lưới ô có đường mảnh, ô đang chọn sáng hơn, mô tả/thuộc tính ở dưới; panel đầy đủ có lý do vì đang ở tác vụ quản lý. Không suy từ màn kho sang HUD thường, nhưng nó phản bác ý tưởng rằng Requiem luôn bỏ mọi panel.
- [SUY LUẬN] Áp dụng dự kiến: giữ panel ở tracker/vitals/hotbar/ammo theo yêu cầu, nhưng cho từng khối bám sát nội dung, giảm cạnh tương phản cao; không dán nền panel sau prompt/marker. Không bắt chước vòng HUD của Requiem: dự án yêu cầu một thanh máu phủ nhiễm, và không có bằng chứng vòng là lựa chọn phù hợp.

## 2. Genshin Impact — phân lớp mục tiêu và cảnh

- [SUY LUẬN] Ảnh nhiệm vụ từ https://www.heypoorplayer.com/2023/08/18/how-to-complete-the-aqueous-tidemarks-quest-in-genshin-impact/ : ảnh `genshinaqueoustidemarkspicture14.jpg` có quest `Aqueous Tidemarks`, `Find the secret treasure!`, `Press [V] to navigate` thành một cụm nhỏ bên trái dưới minimap; dải thông báo vật phẩm ở thấp hơn và tách khỏi mục tiêu. Giữa là nhân vật và môi trường; trong cảnh xuất hiện các vệt sáng thẳng đứng. Trang mô tả các điểm đó là các điểm quan tâm phát sáng, không đủ xác định toàn bộ cách hoạt động của marker.
- [SUY LUẬN] Ảnh `genshinaqueoustidemarks2.jpg` cho thấy cụm quest cùng phía trái và một dòng thoại ở phía dưới giữa; các nhóm không cùng bị nhét vào một thẻ chung. Chữ được nhóm bằng căn trái và khoảng cách, mục tiêu không cần khung chữ nhật lớn quanh nó. Ảnh `genshinaqueoustidemarkspicture10.jpg` có `Find a way to open the path forward` và `Quest area reached` dưới minimap; không được coi câu này là quy tắc chỉ đường của game dự án.
- [SUY LUẬN] Áp dụng dự kiến: một tracker panel duy nhất nhưng bên trong phân cấp bằng vị trí và độ đậm thay vì thêm viền cho từng dòng; marker có icon nhỏ, tên và khoảng cách theo yêu cầu dự án, bám mục tiêu trên cảnh, không là thẻ đen lớn. Không sao chép màu vàng, hình dáng fantasy, minimap, hay giả định marker dự án chỉ hoạt động trong một bán kính.
- [SUY LUẬN] Giới hạn quan trọng: chưa có ảnh Genshin đủ rõ đồng thời chứng minh marker quest hiển thị số mét và trạng thái ngoài màn. Nguồn bài `Our Chenyu Vale Trek` tại https://itemlevel.net/genshin-impact-our-chenyu-vale-trek-world-quests-puzzles-guide/ nói có ảnh theo marker nhưng file ảnh gốc trả 404 lúc kiểm; KHÔNG TÌM THẤY bằng chứng hình đủ để phân tích chính xác mũi tên mép hoặc số mét. Không nhầm teleport waypoint với quest marker.

## 3. Left 4 Dead 2 — nhịp cột và nhận diện nhanh

- [SUY LUẬN] Ảnh `Damage`: https://interfaceingame.com/screenshots/left-4-dead-2-damage/ . Đây là trạng thái bị hạ: cột nhỏ sát mép phải xếp số đạn `32`/`603`, silhouette súng và các icon vật phẩm theo chiều dọc, mỗi nhóm có ô nền tối nhỏ; lựa chọn thứ hai có nhấn màu xanh. Sát mép dưới là các thẻ trạng thái nhân vật có chân dung và thanh; khu giữa để lại cho cảnh chiến đấu. Không xem ảnh này là ảnh HUD bình thường hay chứng minh mọi item luôn hiện.
- [SUY LUẬN] Đặc điểm thẩm mỹ: cột lặp cùng trục, icon có silhouette rõ, số bám sát đối tượng, mảng nhấn nằm ở đúng ô có trạng thái thay vì bọc mọi thứ trong các panel lớn. Các thẻ đồng đội là những đơn vị nhỏ lặp có nhịp nhất quán, chứ không phải bốn icon nhỏ trôi trên một dải rộng rỗng. Phân cấp bằng nhịp lặp và trạng thái, không chỉ bằng tăng kích cỡ chữ.
- [SUY LUẬN] Ảnh `Healing yourself`: https://interfaceingame.com/screenshots/left-4-dead-2-healing-yourself/ . Icon dấu cộng, chữ `HEALING YOURSELF` và thanh tiến trình ngắn cùng một cụm gần tâm; cảnh có cảnh báo vàng ở bên dưới và ảnh gần như xám vì bối cảnh cụ thể. Không suy thời lượng hay âm thanh từ ảnh này; không sao chép cảnh báo vàng vì dự án quy định bốn màu.
- [SUY LUẬN] Áp dụng dự kiến: hotbar bốn slot cùng kích thước, mỗi item có silhouette và phím/số lượng tách khỏi icon, khoảng cách đồng đều; panel tổng bám nội dung. Ưu tiên cách đọc nhóm và trạng thái hơn là sao chép bố cục dọc: game này có viewmodel súng khác, cần kiểm đè hình thật trước khi chốt ngang/dọc.

## 4. Valorant — trọng số chữ và đường cơ sở

- [SUY LUẬN] Ảnh `Headshot`: https://interfaceingame.com/screenshots/valorant-headshot/ . Đây là spectator: có `SWITCH PLAYER` và `COMBAT REPORT`. Ở dải sát đáy, `100` lớn ở phía trái của cụm giữa, các icon kỹ năng đi theo cùng đường cơ sở, `14` lớn ở phía phải cạnh `39` nhỏ; súng nằm phía phải trên. Tương quan kích cỡ giúp nhìn thấy đạn hiện tại trước đạn dự trữ mà không cần một tiêu đề `AMMO` thật lớn.
- [SUY LUẬN] Phân tích: các vùng máu/kỹ năng/đạn phân bố theo chiều ngang tại đáy, các nét nối mảnh và khoảng trống cho cảm giác một hệ thống thay vì các ô độc lập cùng sức nặng. Nhưng tông thể thao, đường viền kỹ thuật và biểu tượng kỹ năng không tự phù hợp bối cảnh sinh tồn công nghiệp; spectator report không phải yếu tố HUD thường.
- [SUY LUẬN] Ảnh `Ability`: https://interfaceingame.com/screenshots/valorant-ability/ . Khi kỹ năng mở, bản đồ màu cam lớn ở giữa, còn `100` và các icon khả năng vẫn sát đáy; đó là overlay theo tác vụ, không phải lý do cho phép prompt tương tác thường chiếm giữa. Không bê màu sắc, góc cạnh hay widget kỹ năng của Valorant vào game.
- [SUY LUẬN] Áp dụng dự kiến: trong panel đạn chỉ để số hiện tại là điểm nhìn mạnh, số dự trữ và tên súng là thứ bậc phụ; canh baseline và khoảng cách nhất quán với hotbar. Cần thử cả số 1 chữ số và 3 chữ số để cụm không xô lệch.

## 5. Phê bình cụ thể bản v3 và hướng thiết kế lại

- [SUY LUẬN] `E:/Unity/Project/FPS/Reports/mockups/04-v3-normal.png`: tracker chiếm một mảng hình chữ nhật lớn; `FACTORY`, objective, nhánh và `[T]` gần như cùng trọng lượng sáng. Dấu chấm, số và nhãn nhánh không cùng một cấu trúc căn rõ; khoảng trống thừa trong panel tạo cảm giác bảng rỗng thay vì bảng công nghiệp có chủ đích.
- [SUY LUẬN] Cùng ảnh: cụm hotbar nằm trên viewmodel, dòng `GREYBOX PLACEHOLDER - ITEM ICONS` chen ngay phía trên và thu hút mắt; icon nhỏ, phím/số bị dàn quá rộng. Ammo lại là hộp riêng thấp hơn, thiếu cùng đường cơ sở. Cảnh nền nhìn lên trời và prompt/marker cùng nhắc `Shipping Manifest` mà không có vật mục tiêu nhìn thấy, làm phối cảnh thiếu thuyết phục. Nền `reticle-bright.png` là ảnh tham khảo có sẵn, không phải Game view mới.
- [SUY LUẬN] Chữ trắng có bóng đen dày làm từng glyph có viền thô; Teko Bold ở số phụ dày gần bằng số chính. Khung panel hiện tại tối và vuông, nhưng độ tối chung không tự tạo ra nhịp chữ, mép căn hay chất liệu; giảm opacity không chữa được lỗi cấu trúc.
- [SUY LUẬN] Đề xuất bố cục kế tiếp, chưa duyệt: tracker là một panel gọn, một dòng objective rõ nhất, hai hàng nhánh theo hai cột tên–tiến độ và `[T]` riêng nhưng không giành trọng số; ammo và hotbar là hai panel cùng trục và cùng biên, không bắt buộc đè lên súng. Health/infection là một panel hẹp với một thanh và hai nhãn đủ phân biệt. Prompt/marker không nền panel lớn; bóng chữ nhẹ có kiểm trên cảnh sáng/tối; marker gắn đúng vật. Nhãn `GREYBOX PLACEHOLDER` đặt ở phần chú thích ngoài ảnh gameplay thay vì nổi trên item, vẫn phải ghi rõ trong tài liệu.
- [SUY LUẬN] Dùng bốn token theme, ba cỡ 36/26/20 tham chiếu, một font chữ và một font số hiện có, cùng một kiểu 9-slice; không khẳng định các giá trị đó đủ đẹp/đủ đọc cho mọi nền. Nền panel điều chỉnh opacity độc lập với chữ/icon; 0% có thể làm mất tương phản và chưa phải giá trị an toàn được kiểm. Cam chỉ việc cần làm ngay, đỏ chỉ nguy hiểm. Không hiện thoại trong khung thường, không xóa subtitle.
- [SUY LUẬN] Khi vẽ lại, cần cảnh có thể thấy mục tiêu thật và súng thật trong cùng khung; thử thêm cảnh sáng/tối. Bốn panel bám content, không đặt bốn khung viền trắng dày; không tự xem các kích thước ước lượng ở trên là thông số cố định cho Unity. Không tự tạo khung/icon mới bằng code hoặc gọi imagegen; thiếu asset phải ghi `GREYBOX PLACEHOLDER`.

## Đề xuất

- [SUY LUẬN] Chỉ sau khi thống nhất hướng phân tích, phác một bản mới với asset đã kiểm và chụp ảnh 1920×1080 trên cảnh có vật mục tiêu; xét đủ trạng thái khóa, giữ, thả, hoàn tất, lỗi, marker trong/ngoài màn. Đó là đề xuất mockup, không phải permission sửa game.
- [SUY LUẬN] Tiếp tục tìm nguồn gameplay Genshin đủ rõ về khoảng cách và marker ngoài màn; xem thêm ảnh L4D2 đang chiến đấu bình thường để kiểm độ ổn định của cột item. Không khẳng định đã hoàn thành hai phần này.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] `Không có panel lớn` chỉ áp dụng prompt và marker; bốn cụm còn lại vẫn có panel. Ảnh Requiem interact có dải tối nhẹ, không được mô tả thành không có nền tuyệt đối.
- [SUY LUẬN] Bản v3 là bản bị chê, không phải bản được duyệt; ảnh tham khảo tĩnh hỗ trợ phân tích thẩm mỹ nhưng không chứng minh hành vi, độ đọc mọi nền hay thành công khi chơi.

## Chưa kiểm được

- [SUY LUẬN] Chưa thấy ảnh quest Genshin đủ rõ cho khoảng cách và offscreen; chưa xác nhận mod/cài đặt/phiên bản nguồn, thời lượng/animation/âm thanh của các game tham khảo. Chưa tạo mockup mới, chưa có phê duyệt thiết kế mới.
- [SUY LUẬN] Không mở Unity hoặc Play Mode, không import/gọi imagegen; chưa có Game view mới 1080p/4K và chưa kiểm tương phản, text dài, vùng né súng trong chuyển động. Unity Editor không chạy tại lúc kiểm; `scene.isDirty` cuối chưa kiểm được, không tuyên bố Save Gate đạt.


---

# Kế hoạch chi tiết tạo mockup A mới — 30/10/2026

- [SUY LUẬN] Đây chỉ là kế hoạch; chưa tạo mockup mới trong lượt này. Phong cách A đã được chọn, các ảnh A cũ và v3 đã bị chê, không được dùng làm mẫu được duyệt. Duyệt kế hoạch không tự động đồng nghĩa duyệt mockup, flow hay triển khai.
- [SUY LUẬN] Đã đọc `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md`, `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/references/ui-planning-workflow.md`, `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/templates/mockup-layout-plan.yaml`: kế hoạch-only không bắt đầu dựng, tách yêu cầu đã chốt với giả định, chuẩn bị cây owner và ledger/rect khi có ảnh. Đã đọc `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`: đề xuất safe area, neo theo vùng, kiểm chữ dài và tỷ lệ khác. `create-game-assets/SKILL.md` KHÔNG TÌM THẤY tại hai root skill đã kiểm, không tuyên bố đã đọc hay áp dụng. Dùng quy tắc kiểm asset trong `AGENTS.md` và `E:/Unity/Project/FPS/Reports/02-ui-assets.md`.

## A. Mục tiêu và nguyên tắc

- [SUY LUẬN] Mượn từ Requiem: các cụm nhỏ có khoảng thở, prompt gần vật không có bảng lớn; từ Genshin: tracker phân cấp bằng căn lề, không phụ thuộc viền; từ L4D2: item đều nhịp, số đi cạnh item; từ Valorant: đạn hiện tại có thứ bậc rõ hơn dự trữ. Các ảnh/giới hạn xem mục nghiên cứu ở trên, không sao chép tài sản hoặc khẳng định tương đương game gốc.
- [SUY LUẬN] Giữ **panel cho tracker, máu/nhiễm, hotbar và đạn**; không tạo panel lớn cho prompt và waypoint. Dùng một sprite panel 9-slice chung, mép tinh tế, không viền trắng dày; bề mặt tối có opacity điều chỉnh độc lập với chữ/icon. `[T]` và phím theo dõi là keycap nhỏ riêng, không phải một panel lớn bao prompt.
- [SUY LUẬN] Bốn màu là trắng `#F2F2F2` chữ chính, xám `#25282A` phụ/nền, cam `#F2A436` chỉ hành động ngay, đỏ `#D84B4B` chỉ nguy hiểm; opacity là alpha, không phải token màu mới. Font chữ Liberation Sans và font số Teko Bold; ba cấp đề xuất ở 1080p 36/26/20 px. Dùng chữ tiếng Anh trong ảnh, tiếng Việt trong báo cáo. Những thông số này là điểm bắt đầu cần thử, không phải giá trị đã duyệt hoặc chứng minh tương phản.
- [SUY LUẬN] Không làm subtitle xuất hiện thường trực chỉ để trang trí; không xóa chức năng. Khi cần kiểm trạng thái thoại, trình bày phương án chữ phù hợp và kiểm không chạm panel đạn; yêu cầu cũ có nền mờ và viền chữ, còn phản hồi gần đây coi màu chữ là đủ, cách xử lý cuối cần xác nhận thay vì tự xóa yêu cầu cũ.

## B. Nền và minh bạch bằng chứng

1. [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/AssetReplacement/Gameplay/tracker-marker-1920x1080-100.png] Ảnh cũ 1920×1080 cho thấy súng góc phải, nhà xưởng phía trước và HUD cũ đã dính vào ảnh. Nó chỉ là tài liệu tham khảo vị trí viewmodel và độ sáng, **không** đặt HUD mới đè rồi trình bày như nền sạch hoặc ảnh sau triển khai.
2. [SUY LUẬN] `E:/Unity/Project/FPS/Documents/LevelDesignAudit/2026-10-22/screenshots/audit-manifest.png` đã xem: ảnh cảnh tường/nhà xưởng không thấy súng hoặc vật mục tiêu rõ; `E:/Unity/Project/FPS/Documents/LevelDesignAudit/2026-10-22/screenshots/audit-generator.png` cũng không có súng hay vật mục tiêu rõ. Dùng chúng thử nhịp HUD/nền nếu cần, không giả vờ là tình huống đang ngắm vật và không chứng minh né súng.
3. [SUY LUẬN] Trước khi tạo ảnh chính, tìm **trong ảnh sẵn có** một khung có thân mục tiêu nhìn thấy, góc súng và ít HUD dính; ghi đường dẫn, thời điểm nếu biết và những gì bị che. Nếu KHÔNG TÌM THẤY, dùng cảnh sạch gần nhất cho bố cục tổng và một ảnh cũ riêng để đối chiếu vùng súng; đánh dấu vị trí mục tiêu chưa được kiểm thay vì bịa vật hoặc chỉnh sửa cảnh để giấu HUD cũ. Không vào Unity/Play Mode để tạo nền trong vai này.

## C. Bản đồ bố cục 1920×1080 — thông số khởi điểm

- [SUY LUẬN] Toạ độ tính từ góc trên trái, chỉ để đặt bản phác; kích thước có thể đổi sau kiểm trực quan, không phải rect Unity hay cam kết đủ cho mọi nội dung. Chừa lề mục tiêu khoảng 64 px ở bốn mép và giữ vùng trung tâm cho ngắm/cảnh. Safe area thực tế và vị trí tránh súng cần kiểm theo từng ảnh.

| Vùng | Khung khởi điểm 1080p | Nội dung và nhịp dự kiến |
|---|---|---|
| Tracker panel | [SUY LUẬN] x=64, y=64, rộng tối đa 500, cao dự kiến 180–210 | Nhãn `FACTORY` cỡ nhỏ xám; objective một dòng cỡ lớn; hai nhánh có tên và tiến độ thẳng cột, chỉ nhánh đang theo dõi có `▶`; `[T]` là keycap riêng. Nền panel bám khối, không kéo rộng khi ít chữ. |
| Vitals panel | [SUY LUẬN] x=64, đáy cách 64, rộng khoảng 400, cao 78–100 | Chỉ một thanh máu; vùng nhiễm phủ lên thanh, có nhãn/số phân biệt; không diễn giải nhiễm là lượng máu mất. |
| Hotbar panel | [SUY LUẬN] sát đáy bên phải, rộng khoảng 360–420, cao 72–100 | Bốn ô đều nhịp, icon là điểm chính; phím và số lượng ở khu riêng, không chồng icon. Thử vị trí liền kề cụm đạn, tránh che phần quan trọng viewmodel nếu ảnh nền cho phép. |
| Ammo panel | [SUY LUẬN] gần hotbar cùng hàng dưới hoặc kề mép phải, rộng khoảng 160–210, cao 72–100 | Tên súng cỡ nhỏ; `15` cỡ lớn, `/ 45` cỡ nhỏ, cùng baseline. Không có `FIRE/AIM/CLASSIC`. Hai panel có mép và khoảng cách đồng nhất nhưng vẫn là hai panel. |
| Prompt | [SUY LUẬN] khoảng dưới tâm, gắn tương đối với vật đang ngắm | `[F] Shipping Manifest` thành một dòng, chữ và key cách nhau ít, không nền panel lớn; không hiển thị nếu chưa có tình huống tương tác trong ảnh. Khóa đổi sang icon khóa, lý do và keycap theo dõi. |
| Marker | [SUY LUẬN] gắn vật, không gắn cứng một tọa độ | Icon + tên + khoảng cách không panel lớn; khi ngoài màn dùng mũi tên ở mép an toàn. Tìm vị trí thử nằm ngoài silhouette súng trong từng ảnh; nếu không đủ nền để kiểm thì ghi chưa kiểm. |
| Hold ring | [SUY LUẬN] tâm tại (960,540), đường kính dự kiến 80–100 | Vòng, phím và trạng thái chỉ hiện khi giữ; test đầy, đang lùi khi thả và hoàn tất. Không vẽ thời gian hoặc âm như đã xác nhận. |
| Subtitle | [SUY LUẬN] dưới giữa khi có thoại, vùng có chiều rộng giới hạn và tách panel đạn | Không hiển thị trong ảnh tổng thường; minh họa riêng nếu xác định được xử lý nền/viền, không chèn thoại tự nghĩ vào ảnh bình thường. |

## D. Cây layer-to-layout và kế hoạch asset

`HUD / SafeArea` → `Tracker / Panel + Objective + BranchRow[2] + SwitchKey`; `WorldMarker / OnScreen | OffScreen`; `Center / Reticle + HoldRing + HoldKey`; `Interaction / Open | Locked | Complete | Error`; `Vitals / Panel + HealthTrack + HealthFill + InfectionOverlay + Labels`; `Hotbar / Panel + Slot[4](Key, Icon, Count)`; `Ammo / Panel + WeaponName + Current + Reserve`; `Subtitle / ContextOnly`. [SUY LUẬN]

- [SUY LUẬN] Panel ứng viên: `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png`; 9-slice trong Unity/importer chưa xác minh. Xem lại sprite thật trước dựng; nếu không thể đạt viền nhẹ mà không sửa asset, ghi **GREYBOX PLACEHOLDER** và nêu giới hạn, không tự tạo art UI thay thế.
- [SUY LUẬN] Font: `E:/Unity/Project/FPS/Assets/TextMesh Pro/Fonts/LiberationSans.ttf` và `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Fonts/Teko-Bold.ttf`; OFL theo nguồn trong bảng asset cũ. Thử Teko cho số lớn trước; nếu số phụ quá dày thì giảm mức nhấn theo alpha/kiểu sử dụng, không thêm font thứ ba.
- [SUY LUẬN] Icon ứng viên: `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Locked.png`, `Complete.png`, `Arrow.png`, `FPSSample/GoalMarkerCentre.png`; thanh `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Bars/HealthTrack.png`, `HealthFill.png`; vòng `E:/Unity/Project/FPS/Assets/FPS/Features/Survival/Content/Textures/ProgressRing.png`. File marker/vòng chỉ là ứng viên; giấy phép và khả năng đọc trên nền sáng/tối cần xác minh như báo cáo cũ.
- [SUY LUẬN] Item icons `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Frag.png`, `Fire.png`, `Medkit.png`, `Antidote.png` có nguồn/màu chưa xác minh; chỉ thử hình minh họa và ghi **GREYBOX PLACEHOLDER** ở chú thích ngoài ảnh, không để dòng placeholder che hotbar. Decal/biển cho từng vật và tín hiệu nhận diện proxy trước khi ngắm: **KHÔNG TÌM THẤY** trong phạm vi kiểm; phải ghi **GREYBOX PLACEHOLDER**, không bịa asset.
- [SUY LUẬN] Khi ảnh mockup mới hình thành, lập riêng ledger theo `mockup-layout-plan/v2`: candidate, source bounds, confidence, `review_decision: hold`, asset plan và các rect sau review; file `E:/Unity/Project/FPS/Reports/mockups/04-hud-layout-plan.yaml` cũ không mô tả thiết kế mới. Không tuyên bố đã validate hoặc sẵn sàng tạo object Unity.

## E. Trạng thái bắt buộc và hợp đồng logic

- [SUY LUẬN] **Mở được:** key thật ví dụ `[F]` và tên vật. **Khóa:** icon khóa + `Requires: [reason from logic]`, không `[F]`, key theo dõi độc lập `[?]` khi chưa biết binding. **Giữ:** vòng và phím; ảnh 25/75/100% chỉ là ví dụ. **Thả sớm:** thêm hai frame progress giảm để minh họa hủy, không tự gán thời gian. **Hoàn tất:** `COMPLETED` + icon/hiệu ứng minh họa; âm thật KHÔNG TÌM THẤY, ghi chưa kiểm; marker chuyển sau khi logic công bố mục tiêu mới. **Lỗi:** `Mismatch: [field from logic]`, không suy đoán đáp án.
- [SUY LUẬN] Bot 5 cần cung cấp objective, hai nhánh/tên/tiến độ/nhánh active và key đổi; trạng thái vật, tên, lý do khóa và key theo dõi; vị trí, tên, khoảng cách, hướng offscreen của mục tiêu; trạng thái và progress giữ/hủy; completion, trường lỗi, target mới; health/max, infection, ammo/current/reserve, weapon, key/count item. Đây là hợp đồng đề xuất, không xác nhận code đã hỗ trợ; hint từ `CampaignContentCatalog` chỉ chỉ nguồn, không chứa đáp án.

## F. Trình tự tạo và kiểm mockup sau bước kế hoạch

1. [SUY LUẬN] Kiểm ảnh nền và asset sẵn có; chọn một ảnh chính đủ ngữ cảnh hoặc công khai nền không đủ. Không import, không sửa asset, không dùng imagegen.
2. [SUY LUẬN] Tạo một bản phác HTML/SVG ngoài `Assets/` tại `E:/Unity/Project/FPS/Reports/mockups/`; chỉ dùng sprite/font sẵn có và placeholder được đánh dấu. Tạo tracker → vitals → hotbar/ammo → prompt/marker → hold, không dùng lại HTML v3 như một thiết kế hoàn chỉnh. Cùng một theme, không tự vẽ khung/icon mới bằng primitives/flat colors.
3. [SUY LUẬN] Chụp 1920×1080 ảnh tổng, nền sáng và tối; ảnh riêng cho khóa, offscreen, đang giữ/thả, hoàn tất/lỗi, 30% và 70% opacity. Slider thử panel là công cụ review, không khẳng định setting game tồn tại; chỉ làm mờ mặt panel, không làm mờ chữ/icon. Ảnh mô phỏng các mức không chứng minh mọi giá trị an toàn.
4. [SUY LUẬN] Kiểm trực quan mỗi ảnh: `[T]` và hai nhánh rõ; key/count không đè icon; một thanh máu/nhiễm; đạn hiện tại lớn hơn dự trữ; prompt không có panel lớn; khóa không `[F]`; marker không che súng trên ảnh có súng; các nhãn không chạm nhau. Thử chuỗi dài và giá trị 1–3 chữ số; nếu không đạt, báo lỗi và sửa mockup, không tự tuyên bố đẹp hay được duyệt.
5. [SUY LUẬN] Kiểm thêm khung 3840×2160 và một tỷ lệ khác bằng mô phỏng; phân biệt ảnh mockup scale từ 1080p với Game view native 4K. Nền không có súng/target không thể chứng minh tránh súng/neo mục tiêu; thiếu ảnh thì ghi chưa kiểm. Báo vị trí, giá trị, ảnh, giả định và asset thiếu.
6. [SUY LUẬN] Đưa một visual target chính và các ảnh trạng thái phụ cho người dùng duyệt; dừng ở cổng duyệt mockup. Không tạo runtime object, không thay scene/prefab/code, không tuyên bố playtest.

## Đề xuất

- [SUY LUẬN] Người phụ trách Unity sau cổng duyệt cần xác minh scene sạch, thực hiện Save Gate theo quy tắc dự án và chỉ kiểm Game view/playtest khi được phép; cần ảnh trước/sau ở 1080p và 4K thật, độ đọc nền sáng/tối và marker né viewmodel động.
- [SUY LUẬN] Tiếp tục bổ sung bằng chứng Genshin marker có số mét/offscreen và giấy phép icon/vòng; những chi tiết chưa có bằng chứng không được biến thành quyết định mô phỏng game gốc.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Panel vẫn có ở bốn cụm HUD, chỉ prompt và waypoint không có panel lớn; slider mờ mặt panel không đồng nghĩa làm mờ toàn HUD. Một thanh máu phủ nhiễm không đồng nghĩa hai giá trị là một đại lượng.
- [SUY LUẬN] Ảnh nền cũ có HUD không phải ảnh sau, mockup HTML không phải Unity, plan không phải mockup hay chấp thuận flow. Vị trí đề xuất là giả định để kiểm, không tuyên bố đã phù hợp mọi viewmodel.

## Chưa kiểm được

- [SUY LUẬN] Chưa tìm được nền sạch đồng thời có mục tiêu rõ và viewmodel; chưa kiểm độ tương phản, text dài, 4K thực, chuyển động, các trạng thái/âm trong Play Mode, binding theo dõi, license một số icon/vòng hay 9-slice importer. Chưa xác minh chi tiết marker Genshin offscreen.
- [SUY LUẬN] Không mở Unity Editor/Play Mode, không lưu, không import hay gọi imagegen; Editor không chạy khi kiểm nên `scene.isDirty` lúc kết thúc chưa xác minh. Chưa có mockup mới hoặc phê duyệt mockup.


---

# Mockup A v4 — bản phác chờ duyệt

- [SUY LUẬN] Người dùng yêu cầu tiến hành từng bước sau kế hoạch. Đã tạo `E:/Unity/Project/FPS/Reports/mockups/04-hud-a-v4.html` và các ảnh `04-v4-*.png`; chúng là HTML/ảnh phác ngoài Assets, không phải asset import hoặc Game view. A v4 chưa được người dùng duyệt. V3 và các phương án cũ chỉ lưu lịch sử.
- [SUY LUẬN] Đã đọc `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md`: dùng cây parent/owner và ledger để giữ candidate ở trạng thái hold, không biến thành Unity object; `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`: giữ lề, kiểm mockup ở 1080p, 2560×1080 và bản scale 4K; `C:/Users/hieud/.agents/skills/no-ai-design-slop/SKILL.md` và `ARTICLE.md`: đặt thông tin liên quan gần nhau, loại bớt trang trí, xem trực tiếp ảnh và sửa khi key/count chạm icon; `C:/Users/hieud/.agents/skills/ui-ux-pro-max/SKILL.md`: tra hierarchy/contrast và overflow, thử chữ dài. Search gợi ý kiểm tương phản và xử lý overflow; không coi kết quả tra hay ảnh phác là chứng nhận độ tương phản. `create-game-assets/SKILL.md` KHÔNG TÌM THẤY trong các root đã kiểm, không nói đã đọc hoặc áp dụng; kiểm asset theo `AGENTS.md`.
- [SUY LUẬN] Nền ảnh chính lấy từ `E:/Unity/Project/FPS/Documents/LevelDesignAudit/2026-10-22/screenshots/audit-arrival-wide.png`; nền sáng dùng `audit-manifest.png`, tối dùng `audit-generator.png` cùng thư mục. Những ảnh này không thấy viewmodel súng hay vật mục tiêu; vị trí `Shipping Manifest 21 m` là ví dụ đặt tĩnh, **không gắn vào vật thật**, không chứng minh marker tránh súng. Không trình bày ảnh này như Play Mode hiện tại.

## Phong cách và thành phần

- [SUY LUẬN] Bốn token thiết kế: trắng `#F2F2F2` chính, xám `#25282A` phụ, cam `#F2A436` cho hành động giữ/`[F]`, đỏ `#D84B4B` dành nguy hiểm và không xuất hiện trong ảnh thường. Ba cấp 20/26/36 px tại 1080p, font chữ `E:/Unity/Project/FPS/Assets/TextMesh Pro/Fonts/LiberationSans.ttf`, số `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Fonts/Teko-Bold.ttf`. Ảnh cảnh và texture sprite có màu pixel riêng; chưa đo chúng theo quy tắc bốn token.
- [SUY LUẬN] Tracker, vitals, hotbar và ammo dùng cùng `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png` làm 9-slice CSS với mép 3 px, không có viền trắng dày. CSS không chứng minh border importer Unity. Nền bốn panel điều khiển chung opacity, chữ không mờ theo; các keycap `T`/`?` dùng sprite riêng, không điều khiển bởi slider. Mặc định mockup 72% chỉ để xem, không phải ngưỡng an toàn hay setting game.

| Thành phần | Trạng thái/phần tử minh họa | Cỡ, màu, asset và giới hạn |
|---|---|---|
| Tracker | [SUY LUẬN] `Read the shipping manifest`, `▶ Power 0 / 3`, `Logistics 0 / 2`, keycap `T` riêng | [SUY LUẬN] Objective 36, nhánh 26, nhãn/key 20; trắng/xám; panel nêu trên; chưa chứng minh hiển thị chữ dài. |
| Prompt mở | [SUY LUẬN] Ảnh `04-v4-open.png` có `[F] Shipping Manifest`; ảnh thường ẩn prompt vì không có vật nhìn thấy | [SUY LUẬN] 26, cam cho F, trắng cho tên; không có panel lớn; tách ảnh để không giả vờ đang ngắm vật. |
| Prompt khóa | [SUY LUẬN] `04-v4-locked.png` có icon khóa, `Requires: [condition from logic]`, keycap `?`; không có `[F]` | [SUY LUẬN] Tên 26, lý do/key 20, trắng/xám; icon `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Locked.png`; `?` là binding chưa biết. |
| Marker | [SUY LUẬN] Tên + `21 m`; ảnh khóa minh họa mũi tên cạnh trái | [SUY LUẬN] Tên 26, khoảng cách 20, trắng; `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/GoalMarkerCentre.png` và `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Arrow.png`; vị trí tĩnh, chưa kiểm projection hoặc né súng. |
| Máu/nhiễm | [SUY LUẬN] `HEALTH 80 / 100`, `INFECTION 20%`, một thanh phủ vùng nhiễm màu xám | [SUY LUẬN] Nhãn 20, số 26; trắng/xám; `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Bars/HealthTrack.png`, `HealthFill.png`; đây là minh họa hai đại lượng, chưa xác minh quy tắc phủ theo logic. |
| Hotbar | [SUY LUẬN] Bốn item, phím trên icon và số lượng dưới icon | [SUY LUẬN] Phím/số 20 trắng; `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Frag.png`, `Fire.png`, `Medkit.png`, `Antidote.png` khử màu; **GREYBOX PLACEHOLDER**, license chưa xác minh. |
| Đạn | [SUY LUẬN] `WARDEN`, `15 / 45`; không có `FIRE/AIM/CLASSIC` | [SUY LUẬN] Số hiện tại 36, tên và dự trữ 20, trắng/xám; panel chung; số và tên chỉ là ví dụ. |
| Giữ/hủy | [SUY LUẬN] `hold25/75/100`, `release65/30`: vòng, `F`, phần trăm; thả sớm đổi nhãn `CANCELLED` | [SUY LUẬN] Phím 26, nhãn 20, cam khi giữ, trắng khi hủy; `E:/Unity/Project/FPS/Assets/FPS/Features/Survival/Content/Textures/ProgressRing.png` làm mask; license, thời gian và animation chưa kiểm. |
| Hoàn tất/lỗi | [SUY LUẬN] `04-v4-complete.png`: `COMPLETED`, icon và mục tiêu ví dụ đổi; `04-v4-error.png`: `Mismatch: [field from logic]` | [SUY LUẬN] Chữ 26 trắng; `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Complete.png`; âm và hiệu ứng runtime KHÔNG TÌM THẤY, nên chưa đủ chứng minh phản hồi hoàn tất. |
| Subtitle / nhãn thế giới | [SUY LUẬN] Không vẽ subtitle thường trực, không xóa chức năng; decal/biển hoặc nhãn khi ngắm chưa được minh họa trên vật thật | [SUY LUẬN] Asset gắn vật phù hợp **KHÔNG TÌM THẤY** trong phạm vi kiểm: **GREYBOX PLACEHOLDER**; xử lý nền thoại còn cần thống nhất theo phản hồi. |

## Cây, kiểm tra và hợp đồng

- [SUY LUẬN] Cây và candidate ledger theo template tại `E:/Unity/Project/FPS/Reports/mockups/04-hud-v4-layout-plan.yaml`; các candidate đều `hold`, stack chưa xác định, chưa chạy validator. Cấu trúc: `HUD/SafeArea → Tracker; Vitals; Loadout/Hotbar + Ammo; Marker; Center; Interaction; Subtitle`. Chưa có rect leaf/asset plan đủ triển khai và không tạo Unity object.
- [SUY LUẬN] Playwright/Edge headless chụp các trạng thái `normal/open/locked/hold25/hold75/hold100/release65/release30/complete/error`: mỗi ảnh DOM có bốn panel, không ảnh asset bị lỗi tải; kiểm bounding box không thấy phím hoặc số lượng chồng icon. Sau kiểm trực quan đã sửa một lỗi phím/số sát icon và sửa slider đè nhãn ghi chú. Những kiểm tra này chỉ áp dụng HTML, không phải Unity runtime.
- [SUY LUẬN] `04-v4-opacity30.png` và `04-v4-opacity70.png`: computed opacity panel lần lượt 0.3/0.7, text 1; 30% trên ảnh sáng làm tracker nhạt và chưa đo được độ tương phản, không khuyến nghị mức này như giá trị an toàn. `04-v4-ultrawide.png` là 2560×1080 HTML; `04-v4-scaled-4k.png` là phóng mockup lên 3840×2160, **không phải Game view 4K native**.
- [SUY LUẬN] Thử objective dài `Investigate the extremely long logistics shipping manifest at the remote warehouse`: nội dung cần 1315 px trong ô rộng 486 px và bị ellipsis. Đây là **lỗi còn mở**: một dòng không hiển thị đủ nội dung; cần quyết định cách đọc đầy đủ mà không phá yêu cầu một dòng, không coi test chữ dài đã đạt.
- [SUY LUẬN] Hợp đồng Bot 5 vẫn theo kế hoạch trước: objective, hai nhánh/tiến độ/active, binding; trạng thái mở/khóa và lý do, target body/tên/khoảng cách/hướng; progress giữ/hủy; hoàn tất/lỗi/field và target kế; health/infection/ammo/weapon/slot. Target mới phải được công bố trước khi marker đổi; hint lấy từ `CampaignContentCatalog`, không chứa đáp án. Đây là đề xuất, chưa chứng minh nguồn dữ liệu thật đã có.

## Đề xuất

- [SUY LUẬN] Người dùng xem ảnh chính và ảnh mở/khóa, sau đó xác định liệu nhịp tracker và cụm hotbar/đạn là hướng chấp nhận được; chưa xem đây là phê duyệt. Khi có ảnh gameplay sạch với súng và vật mục tiêu, dùng nó để thử vị trí prompt/marker thay vì tấm nền hiện tại.
- [SUY LUẬN] Sau duyệt mockup, bên phụ trách Unity kiểm Save Gate, Game view 1080p/4K, tương phản và behavior thật, license, sound/visual completion; Bot 4 không sửa game.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Chỉ prompt/marker không có panel lớn; tracker/vitals/hotbar/ammo vẫn có bốn panel. Slider chỉ đổi mặt panel, không đổi chữ/icon; keycap có sprite riêng.
- [SUY LUẬN] Ảnh thường ẩn prompt do không có vật, ảnh `open` chỉ minh họa trạng thái; marker là ví dụ tĩnh không chứng minh vật hay khoảng cách thực. HTML, ảnh scale và ledger hold không phải Game view, playtest, flow hay mockup được duyệt.

## Chưa kiểm được

- [SUY LUẬN] Vật đích và súng không có trong nền sạch, nên chưa kiểm nhãn gắn vật, marker né viewmodel hoặc chuyển động. Chưa đo tương phản, chưa có cách đọc đủ objective dài trong một dòng, chưa có subtitle mẫu và decal phù hợp, chưa xác minh license icon/vòng hoặc 9-slice Unity.
- [SUY LUẬN] Không mở Unity Editor/Play Mode, không import, không sửa code/scene/prefab/asset, không gọi imagegen. Không có Game view 1080p/4K trước-sau hay playtest mới; Unity không chạy nên `scene.isDirty` cuối chưa xác minh và Save Gate chưa đạt.


---

# Mockup A v5 — bố cục sát góc, chờ phản hồi

- [SUY LUẬN] Lời nhắn trực tiếp xác định ảnh `C:/Users/hieud/AppData/Local/Temp/codex-clipboard-f3ddfeb4-a01b-4371-8fa2-71ffc5a7c9c1.png` chỉ là sơ đồ vị trí: HUD thực tế vẫn có panel vuông/gọn và cụm nằm gần góc. Văn bản đính kèm `C:/Users/hieud/.codex/attachments/670846d7-0376-46e0-98b0-635c3c660945/Pasted text.txt` là tài liệu đề xuất, không phải bằng chứng runtime hay thay thế chỉ dẫn trực tiếp. V5 là bản thăm dò mới, không phải mockup được duyệt.
- [SUY LUẬN] Đã đọc `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md`: dựa trên ảnh để đề xuất cây nhóm và kiểm tài nguyên trước khi sáng tác; không tạo object Unity. Đã đọc `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`: dành mép an toàn thử nghiệm, nhóm cụm theo góc; 40 px chỉ là giả định ở 1080p, không phải chứng nhận safe area. Đã đọc `C:/Users/hieud/.agents/skills/no-ai-design-slop/SKILL.md`: xem ảnh render thật và sửa chồng lấn key/số lượng với icon, giữ nhóm liên quan gần nhau. `create-game-assets/SKILL.md` **KHÔNG TÌM THẤY** ở hai thư mục skill đã kiểm; không tuyên bố đã đọc hoặc áp dụng skill đó. Kiểm và tái dùng tài nguyên theo `AGENTS.md`.
- [SUY LUẬN] `E:/Unity/Project/FPS/Reports/mockups/04-v5-exploration.png` là ảnh 1920×1080 chụp HTML; HTML gốc `E:/Unity/Project/FPS/Reports/mockups/04-hud-a-v5.html`. Không phải Game view, asset để import hay phê duyệt. Nền lấy từ `E:/Unity/Project/FPS/Documents/LevelDesignAudit/2026-10-22/screenshots/audit-arrival-wide.png`, được crop/scale bằng CSS `cover`; **không giữ nguyên khung gốc**. Ảnh nền không thấy vật đích hoặc súng, marker `Shipping Manifest 21 m` chỉ là vị trí và dữ liệu giả định, không neo vào đối tượng thật.

## Các quyết định nhìn thấy

- [SUY LUẬN] Tracker cách trái/trên 40 px, rộng 494 px: `FACTORY`, một dòng objective, hai dòng `Power 0 / 3` và `Logistics 0 / 2`, mũi tên chỉ Power, keycap `T SWITCH`. Nền panel 45% là giá trị thử, không tuyên bố phù hợp mọi cảnh.
- [SUY LUẬN] Vitals sát góc dưới trái; health và infection chung một thanh với đoạn nhiễm phủ xám. Hai panel hotbar và đạn sát góc dưới phải, 4 icon, phím và số lượng ở hàng riêng; `15` lớn hơn `/ 45`. Marker không panel, dùng `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/GoalMarker.png` là mũi tên tạm, **không phải hình thoi**; chưa tìm được icon hình thoi phù hợp trong phạm vi đã kiểm.
- [SUY LUẬN] Cả bốn panel dùng `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png`, CSS slice 2 px; không xác nhận sprite đã cấu hình 9-slice trong Unity. Các icon `Frag.png`, `Fire.png`, `Medkit.png`, `Antidote.png` thuộc `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/`: minh họa **GREYBOX PLACEHOLDER**, giấy phép chưa xác minh. Font chữ `E:/Unity/Project/FPS/Assets/TextMesh Pro/Fonts/LiberationSans.ttf`, font số `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/Fonts/Teko-Bold.ttf`; ba cấp 18/24/36 px; màu theme trắng/xám/cam/đỏ, ảnh nền và sprite có màu riêng.
- [SUY LUẬN] HTML có `?controls=1&opacity=20` để thử nền panel, ảnh `E:/Unity/Project/FPS/Reports/mockups/04-v5-opacity20.png`: DOM báo opacity panel 0.20, chữ objective 1.00; đây chỉ là điều khiển review, chưa phải setting game và chưa chứng minh 20% đủ tương phản. Không đặt subtitle, prompt F hay watermark trong ảnh exploration; không có F tại khoảng cách giả định 21 m.
- [SUY LUẬN] `E:/Unity/Project/FPS/Reports/mockups/04-v5-expanded.png` chỉ đổi khoảng cách giữa hai dòng để xem bố cục; **không phải** trạng thái mở chi tiết hoặc hành vi phím T. Tài liệu đính kèm đề xuất T mở chi tiết và ẩn nhánh khi thường; yêu cầu vai và quy tắc dự án yêu cầu hai nhánh luôn hiện và T đổi nhánh. Chưa tự đổi hành vi hoặc ẩn hai nhánh; cần người dùng quyết định xung đột trước khi làm B đúng nghĩa. Tài liệu đề xuất hai thanh, nhưng chuẩn dự án yêu cầu một thanh phủ nhiễm nên giữ một thanh.

## Cây bố cục và kiểm tra

- [SUY LUẬN] Cây trung lập: `HUD / SafeArea → Tracker / Objective + Branch[2] + SwitchKey; Vitals / HealthTrack + HealthFill + InfectionOverlay; Loadout / Hotbar / Slot[4] + Ammo; Marker; Reticle`. Các vị trí trong HTML là tọa độ bố cục 1920×1080 thử nghiệm, không phải hợp đồng anchor Unity hoặc ledger sẵn sàng triển khai.
- [SUY LUẬN] Playwright/Edge headless kiểm HTML: không có ảnh bị lỗi; 4 slot không chồng bounding box key/icon hoặc count/icon; `[F]` không hiện trong ảnh thường. Thử objective dài 876 px vào ô 458 px: vẫn bị ellipsis, nên chưa đáp ứng đọc đủ chữ dài. Chưa kiểm tỉ lệ khác hoặc 4K cho v5.
- [SUY LUẬN] Hợp đồng đề xuất cho Bot 5 giữ nguyên: objective, hai nhánh/tiến độ/active và binding; tên, vị trí/khoảng cách target; trạng thái tương tác và lý do khóa; tiến trình giữ/hủy, completion/lỗi/target mới; health/infection, vũ khí/đạn và số lượng từng slot. Không xác nhận các nguồn này đã có trong code.

## Đề xuất

- [SUY LUẬN] Xin ảnh gameplay sạch 16:9 có viewmodel và mục tiêu thực, để kiểm marker không che súng/vật và so nền sáng/tối; không thay thế hoặc chỉnh sửa cảnh gốc.
- [SUY LUẬN] Làm trạng thái mở chi tiết sau khi thống nhất vai trò T; sau đó bổ sung ảnh prompt khóa/giữ/hoàn tất, offscreen và xử lý objective dài trước khi xin duyệt toàn bộ mockup.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Panel vẫn hiện ở tracker/vitals/hotbar/ammo; chỉ waypoint và prompt không có panel lớn. Mức opacity review chỉ tác động mặt panel, không làm mờ chữ.
- [SUY LUẬN] Hai nhánh luôn hiện và T dùng để đổi nhánh; ảnh `expanded` không chứng minh T mở overlay. Một thanh có vùng nhiễm không phải hai thanh. Ảnh HTML không phải ảnh gameplay hay bằng chứng playtest.

## Chưa kiểm được

- [SUY LUẬN] Chưa có ảnh nền sạch đúng 16:9 kèm súng/vật; chưa kiểm neo marker, né viewmodel, độ tương phản định lượng, 4K, aspect ratio khác, font dài, 9-slice Unity, giấy phép icon, âm thanh và các trạng thái runtime. Không gọi imagegen, không import, không sửa code/scene/prefab/asset, không mở Editor/Play Mode; Unity không chạy khi kiểm nên `scene.isDirty == false` cuối lượt **chưa xác minh**. Bản v5 chưa được duyệt.


---

# Mockup A v6 — đủ ô vũ khí, chưa duyệt

- [SUY LUẬN] V5 bỏ cả icon súng đang dùng lẫn súng còn lại: đây là lỗi của bản phác dù nghiên cứu trước có nhắc silhouette trong ảnh tham khảo. Xin lỗi; không nên kết luận là đã áp dụng đầy đủ nghiên cứu khi bản render thiếu nhóm này.
- [CODE Assets/FPS/Features/Weapons/Runtime/WeaponManager.cs:31] Danh sách `weapons` mô tả slot 0 là primary và slot 1 là sidearm; dòng 40 khai báo giá trị mặc định `maxWeaponSlots = 2`, không chứng minh mọi cấu hình runtime đều đúng hai slot.
- [CODE Assets/FPS/Features/UI/Runtime/HUDManager.cs:465] `UpdateWeaponUI` lấy current và unused; dòng 561–565 gán icon cho cả hai, dòng 616–622 lấy icon từ `weapon.Data.weaponIcon`. V6 minh họa hai ô: primary `VANDAL`, sidearm `CLASSIC`, ô primary sáng hơn, số đạn chỉ gắn súng đang dùng. Tên, cặp súng và số đạn trong hình là ví dụ, không chứng minh loadout hiện tại.
- [CODE Assets/FPS/Features/UI/Runtime/HUDManager.cs:589] Binding hiển thị súng lấy action `Weapon1`/`Weapon2`, có fallback `1`/`2`; [CODE Assets/FPS/Features/Survival/Runtime/SurvivalHotbar.cs:147] grenade đang chọn có `Grenade` fallback `G`, medkit `4`, antidote `5`, cycle grenade `3` ở dòng 151. V5 gán sai `1–4` cho cả bốn vật phẩm; v6 minh họa các fallback này, không xác nhận binding người chơi thực tế.
- [SUY LUẬN] Đã xem lại ảnh Left 4 Dead 2 trạng thái bị hạ tại https://interfaceingame.com/screenshots/left-4-dead-2-damage/ : sát mép phải có hai dòng silhouette súng, dòng dưới màu xanh và các item bên dưới. Đây là một khung hình bị hạ, không chứng minh mọi trạng thái hay số slot của game đó. Rút ra nguyên tắc phân biệt súng đang chọn và liệt kê súng cùng cụm, không sao chép nguyên giao diện.
- [SUY LUẬN] Đã đọc `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md`, dùng nhóm cạnh góc và tài nguyên sẵn có; `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`, tách phím khỏi icon và giữ inset thử nghiệm; `C:/Users/hieud/.agents/skills/no-ai-design-slop/SKILL.md`, xem ảnh render và giữ vùng súng cạnh đạn thay vì thêm trang trí. `create-game-assets/SKILL.md` **KHÔNG TÌM THẤY** trong hai thư mục skill đã kiểm; không tuyên bố đã đọc hoặc áp dụng.
- [SUY LUẬN] Sprite minh họa từ `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Icons/Weapon_Vandal.png` và `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Icons/Weapon_Classic.png`; chưa xác minh chúng được gán vào `WeaponData` tương ứng trong runtime hay giấy phép. **GREYBOX PLACEHOLDER** cho đến khi kiểm xong. Icon Warden phù hợp **KHÔNG TÌM THẤY** trong thư mục icon đã kiểm, vì vậy không giả nhận icon khác là Warden.
- [SUY LUẬN] `E:/Unity/Project/FPS/Reports/mockups/04-hud-a-v6.html` và `E:/Unity/Project/FPS/Reports/mockups/04-v6-exploration.png` là mockup HTML/ảnh 1920×1080, không phải Game view hay asset import. Panel súng x=1643, y=812, rộng 237, cao 92; hai panel item/đạn dưới nó sát phải 40 px. Trong Playwright hai ảnh súng tải được, ô đang dùng opacity 1, ô còn lại 0.58; chưa kiểm độ dễ nhận biết cho mọi icon/nền.
- [SUY LUẬN] Bổ sung hợp đồng Bot 5: tập slot vũ khí đang sở hữu, thứ tự slot, icon/tên theo `WeaponData`, slot active, binding hiển thị đã remap và trạng thái slot trống; đạn hiện tại/dự trữ của súng active. Đây là yêu cầu dữ liệu cho thiết kế, không xác nhận khả năng runtime đầy đủ.

## Đề xuất

- [SUY LUẬN] Sau phản hồi bố cục, kiểm hình thật của từng icon súng, license, trường hợp thiếu icon/slot trống và mức tương phản trên nền cảnh thật; không tự tạo hoặc import icon mới.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Hai silhouette là ví dụ cho hai slot được mô tả trong code, không khẳng định người chơi hiện có Vandal/Classic. `1`/`2` và `G`/`3`/`4`/`5` là fallback trong code, không phải binding xác nhận từ phiên chơi. Không đặt súng vào bốn slot consumable.

## Chưa kiểm được

- [SUY LUẬN] Chưa xem cấu hình thực hoặc số slot runtime, chưa xác nhận mapping và license sprite, chưa kiểm vũ khí đổi, nhãn dài, 4K hoặc marker trên viewmodel. Không mở Unity/Play Mode, không sửa asset/code/scene, không import hoặc gọi imagegen; `scene.isDirty` cuối chưa xác minh. V6 vẫn chờ duyệt.


---

# Mockup A v7 — gộp súng và đạn

- [SUY LUẬN] V6 đã tự tách panel súng khỏi đạn, trái ý người dùng; v7 bỏ panel đạn riêng, chuyển `15 / 45` vào cùng dòng và cùng panel với icon súng đang dùng, tên `VANDAL` bên dưới icon; dòng dưới là icon và tên `CLASSIC` mờ hơn. Tên, loadout và số lượng đạn chỉ minh họa, không xác nhận trạng thái game.
- [SUY LUẬN] Ảnh `E:/Unity/Project/FPS/Reports/mockups/04-v7-exploration.png`, HTML `E:/Unity/Project/FPS/Reports/mockups/04-hud-a-v7.html`. Cụm item và súng ở cùng mép dưới, súng cách mép phải 40 px; không có panel đạn riêng. Vị trí là mẫu 1920×1080 chưa được duyệt.
- [SUY LUẬN] Theo `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md` đã đọc ở lượt trước, nhóm các phần có quan hệ cùng owner; theo `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`, giữ cụm ở góc có lề thử nghiệm; theo `C:/Users/hieud/.agents/skills/no-ai-design-slop/SKILL.md`, xem render, chuyển tên xuống dưới icon để tránh dính vào số đạn. `create-game-assets/SKILL.md` **KHÔNG TÌM THẤY** trong hai thư mục skill đã kiểm, không tuyên bố đã đọc hoặc áp dụng.
- [SUY LUẬN] Playwright kiểm HTML: hai dòng súng, đạn nằm trong dòng active, không có `.ammo.panel` riêng, ảnh tải đủ; kiểm bounding box tên súng và đạn không chồng nhau. Đây không phải xác minh runtime hoặc chứng nhận tương phản.

## Đề xuất

- [SUY LUẬN] Chỉ sau khi người dùng đồng ý bố cục, kiểm trạng thái đổi súng, slot trống, text dài, nền sáng/tối và màn hình thật.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Súng và đạn cùng một panel; hotbar vật phẩm vẫn là panel kế bên. Hai súng và số đạn chỉ là ví dụ, không suy ra loadout thực tế.

## Chưa kiểm được

- [SUY LUẬN] Chưa kiểm icon và binding runtime, tương phản, viewmodel, 4K hay scene. Không mở Editor/Play Mode, không sửa code/scene/prefab/asset, không import hoặc gọi imagegen; `scene.isDirty` cuối chưa xác minh. V7 chưa được duyệt.


---

# Phân tích ảnh tham chiếu người dùng — cụm HUD và phân tầng

- [SUY LUẬN] Đầu vào duy nhất là ảnh `C:/Users/hieud/AppData/Local/Temp/codex-clipboard-18515192-1082-4c6f-9f9c-5264ba1ebe4a.png`, 1920×1080. Đây là ảnh tham chiếu người dùng gửi, **không xác nhận là Game view hay playtest**, không suy ra mọi trạng thái hoạt động. Các số đo sau ước lượng theo pixel ảnh, không phải RectTransform.
- [SUY LUẬN] Đã đọc `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md`: tách lớp hình ảnh thành cây nhóm và phân biệt cấu trúc từ hình thức; `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`: xem quan hệ với mép, safe area và cảnh báo chưa kiểm các tỷ lệ; `C:/Users/hieud/.agents/skills/no-ai-design-slop/SKILL.md`: đánh giá phân cấp, độ gần của các phần liên quan, phân biệt chi tiết hữu ích và trang trí. `create-game-assets/SKILL.md` **KHÔNG TÌM THẤY** ở `C:/Users/hieud/.agents/skills/` và `C:/Users/hieud/.codex/skills/`; không nói đã đọc hay áp dụng.

## Quan sát từng vùng

- [SUY LUẬN] **Trên trái, tracker:** một panel tối bắt đầu sát mép trên/trái, vùng thấy được khoảng x=0–420, y=0–193. Thứ tự từ trên xuống: `FACTORY`, `Read the shipping manifest` đậm hơn, hai dòng `→ Power 0 / 3` và `Logistics 0 / 2`, rồi `T SWITCH`. Tên nhánh và tiến độ nằm hai đầu cùng dòng; chỉ Power có mũi tên. `T` và `SWITCH` trong ảnh không nằm trong hai ô nền riêng. Viền dưới/phải màu xám sáng dạng kim loại nhiều lớp.
- [SUY LUẬN] **Dưới trái, vitals:** panel tối sát góc, vùng thấy được khoảng x=0–340, y=942–1080; phần dưới chạm mép ảnh. Hai nhãn `HEALTH` và `INFECTION` cùng một vùng, `80 / 100` lớn/đậm hơn `20%`; bên dưới là một thanh ngang, phần lớn sáng và phần cuối xám. Ảnh tĩnh không giải thích chính xác phần xám tính theo health hay infection.
- [SUY LUẬN] **Dưới phải, loadout:** một panel chung, vùng thấy được khoảng x=1394–1920, y=862–1080; cạnh phải và dưới chạm khung ảnh. Hàng trên có bốn ô item nhỏ, lần lượt icon và `x2`, `x1`, `x3`, `x1`; `G`, `[3] CYCLE`, `4`, `5` ở hàng trên. Key không đặt lên icon; số lượng nằm bên phải từng icon trong cùng ô, không phủ lên hình icon trong khung ảnh. Một đường ngang mảnh ngăn hàng item với hàng súng.
- [SUY LUẬN] **Trong chính panel loadout:** dòng đầu theo chiều trái → phải là `1`, `VANDAL`, silhouette súng dài, `15 / 45` sát phải; `15` đậm hơn `/ 45`. Dòng thứ hai là `2`, `CLASSIC` và silhouette súng ngắn, tối hơn dòng trên. Đạn nằm **cùng dòng súng active**, không phải một panel riêng; hai dòng súng và hàng item cùng nằm trong một panel lớn hơn. Không thấy số đạn dòng thứ hai, nhưng một ảnh tĩnh không chứng minh quy tắc hiện đạn khi đổi súng.
- [SUY LUẬN] **Waypoint:** icon tam giác nhỏ trắng với `Shipping Manifest 21 m` nằm vùng x≈1125–1340, y≈410–430, không có panel chữ nhật lớn; có viền/bóng tối giúp chữ tách khỏi nền. Không thấy mục tiêu interactable cụ thể gắn với icon; không thể xác minh độ chính xác hay khoảng cách thực, chỉ nhìn được vị trí cố định trong ảnh.
- [SUY LUẬN] **Chữ ví dụ:** góc trên phải có `THÔNG BÁO MẪU` và `Nội dung giả lập để kiểm tra bố cục`; giữa đáy có hai dòng `PHỤ ĐỀ MẪU — dòng thứ nhất` / `PHỤ ĐỀ MẪU — dòng thứ hai`, chữ sáng có viền tối, không thấy panel nền riêng. Đó là nội dung trong ảnh, **không phải chỉ dẫn đổi ngôn ngữ HUD** và không chứng minh có hội thoại hoặc thông báo trong runtime.

## Hệ thống bố cục nhìn thấy

- [SUY LUẬN] Cây thị giác: `HUD → Tracker[Header, Objective, BranchRow×2, Switch]; Vitals[HealthLabel, InfectionLabel, OneBar]; LoadoutPanel[ItemRow/ItemSlot×4, Divider, WeaponRow×2(active: Key+Name+Icon+Ammo; inactive: Key+Name+Icon)]; Waypoint[Icon, Name, Distance]; SampleNotice; SampleSubtitle`. Đây là phân tích bố cục ảnh, chưa phải cây Unity hay approval.
- [SUY LUẬN] **Nhóm và thứ bậc:** panel chung tạo một vùng đọc dưới phải; bốn ô vật phẩm và hai hàng súng có thứ tự rõ. Bản v7 đã gộp đạn với súng, nhưng **vẫn tách vật phẩm và súng thành hai panel ngang**, trong khi ảnh này xếp item trên súng trong một panel. V7 cũng đặt icon trước tên súng, còn ảnh đặt tên trước icon. Những khác biệt này là quan sát, không tự xem ảnh là chỉ thị phải sao chép từng pixel.
- [SUY LUẬN] **Điểm cần giữ:** đạn cạnh icon của súng active, hiển thị cả hai súng, item có slot đủ chỗ cho icon và count, cụm sát góc; marker không panel lớn. Cách này đáp ứng đúng phản hồi trực tiếp đã có của người dùng về súng/đạn và vị trí.
- [SUY LUẬN] **Điểm không tự sao chép:** ảnh có khung trang trí kim loại xám khá dày và nhiều cạnh; người dùng đã phản đối viền trắng dày, nên cần tách quyết định về chất liệu/độ dày viền khỏi việc chấp nhận bố cục. Ảnh chứa subtitle và thông báo tiếng Việt giả lập; không tự bổ sung chúng vào bản HUD tiếng Anh. Panel bám mép/có phần ngoài ảnh không chứng minh an toàn trên màn có overscan, nên không tự đặt nội dung quan trọng sát mép bằng 0.
- [SUY LUẬN] **So với v7:** ảnh mẫu dành một panel cho toàn bộ item+súng, tách hàng bằng đường mảnh; v7 dùng panel item 293 px cạnh panel súng 292 px và nội dung súng ở hai cột. Một panel chung có thể giảm số cạnh cạnh tranh, nhưng kích thước, độ trong suốt, độ rõ trên nền sáng/tối và nguy cơ che viewmodel phải kiểm trong bản phác tiếp theo, không thể kết luận chỉ từ một ảnh.

## Đề xuất

- [SUY LUẬN] Nếu được yêu cầu dựng lại, thử một panel loadout duy nhất sát góc phải: item bên trên, hai dòng súng bên dưới, đạn trong dòng súng active; điều chỉnh nền độc lập với chữ, không vẽ prompt/waypoint thành panel. Dùng tài nguyên có sẵn và đánh dấu placeholder; không sửa Unity hay tự thêm viền dày.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Nhận diện bố cục của ảnh không đồng nghĩa chấp nhận viền dày hay chữ tiếng Việt trong ảnh. `15 / 45` cùng dòng súng khác với panel đạn riêng; panel chung chứa cả item và súng khác với việc bỏ panel.
- [SUY LUẬN] Mọi số đo đều là ước lượng theo ảnh; ảnh tham chiếu không được gọi là playtest, thông tin active hay tương tác theo thời gian chưa được xác minh.

## Chưa kiểm được

- [SUY LUẬN] Không biết ảnh xuất từ game hay mockup, asset/9-slice và opacity gốc, ý nghĩa chính xác phần xám của thanh, thời lượng subtitle/notification, binding thực, trạng thái đổi súng, kích thước safe area, tương phản, 4K hoặc khi súng viewmodel xuất hiện. Không mở Unity/Play Mode, không sửa code/scene/prefab/asset, không import hoặc gọi imagegen; `scene.isDirty` cuối **chưa xác minh**. Không tạo mockup mới hay coi ảnh này là phê duyệt.


---

# Giao diện người dùng đã chọn

- [SUY LUẬN] Người dùng xác nhận ảnh `C:/Users/hieud/AppData/Local/Temp/codex-clipboard-18515192-1082-4c6f-9f9c-5264ba1ebe4a.png` là giao diện được chọn. Ảnh này là mốc tham chiếu được chọn cho thiết kế HUD; v7 và các bản trước là lịch sử, không thay thế ảnh đã chọn. Đây là sự chọn giao diện, không phải bằng chứng flow đã duyệt, cho phép sửa game, hay xác nhận Unity runtime đã giống ảnh.
- [SUY LUẬN] Bố cục theo ảnh: tracker trên trái; health/infection dưới trái; một panel dưới phải chứa item bên trên và hai dòng súng bên dưới; đạn ở cùng dòng súng active; waypoint không panel lớn. Giữ các yêu cầu trước về nội dung HUD tiếng Anh, opacity nền điều chỉnh độc lập với chữ và không có panel lớn cho prompt F/waypoint. Nội dung thông báo/phụ đề tiếng Việt trong ảnh là ví dụ, không tự chuyển ngôn ngữ hoặc tạo hội thoại.
- [SUY LUẬN] Ảnh có các cạnh khung kim loại sáng nhiều lớp; đã ghi nhận đây là hình thức của ảnh được chọn. Người dùng trước đó không thích viền trắng dày; cần đối chiếu trực quan độ dày khi tiếp tục, không tự kết luận rằng ý kiến trước đã bị hủy hoặc tự thay panel bằng giao diện khác.
- [SUY LUẬN] Đã đọc `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md`: giữ ảnh đã chọn làm nguồn bố cục và không triển khai Unity trước cổng duyệt; `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`: ghi lại các nhóm theo góc, để kiểm safe area và độ phân giải về sau. `create-game-assets/SKILL.md` **KHÔNG TÌM THẤY** trong hai thư mục skill đã kiểm, không nói đã đọc hay áp dụng.

## Đề xuất

- [SUY LUẬN] Bước tiếp theo nếu được yêu cầu: đối chiếu ảnh đã chọn với các asset và trạng thái thực, rồi lập handoff thiết kế; không tự sửa code, scene hoặc asset.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Lựa chọn ảnh này thay thế các bản phác v7 trở về trước về hướng giao diện; việc chọn ảnh không chứng minh các giá trị, scene hay flow trong ảnh là runtime. Một panel chung item+súng vẫn giữ đạn cùng dòng súng, không phải panel đạn tách rời.

## Chưa kiểm được

- [SUY LUẬN] Chưa kiểm opacity, viền thực tế, safe area, hình trong 4K, tương phản và runtime; ảnh nằm trong thư mục tạm và có thể không còn sau này. Không mở Unity/Play Mode, không import hoặc sửa code/scene/prefab/asset; `scene.isDirty` cuối chưa xác minh.


---

# Ngoài HUD: giao diện cốt truyện và asset — kiểm kê sơ bộ

- [SUY LUẬN] Người dùng đã chọn HUD và muốn quan tâm thêm giao diện hiện trong cốt truyện, nội dung text, máy tính, file và asset; không đồng nghĩa đã duyệt thiết kế các màn đó, thay đổi flow hoặc cho phép sửa game.
- [SUY LUẬN] Đã đọc `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md`: phân nhóm giao diện theo nội dung và owner, phân biệt code với hình runtime; `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`: ghi nhu cầu safe area, scroll và focus cần kiểm; `C:/Users/hieud/.agents/skills/no-ai-design-slop/SKILL.md`: xem quan hệ bằng chứng–lựa chọn trước khi đề xuất lớp trang trí. `create-game-assets/SKILL.md` **KHÔNG TÌM THẤY** trong hai thư mục skill đã kiểm, không nói đã đọc hoặc áp dụng; dò asset theo AGENTS.md.

## Những màn có bằng chứng code

- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:161] `JournalAndDocument` dùng một panel, vùng document có `ScrollRect` và vùng controls; không suy ra diện mạo runtime từ code. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:232] Màn tương tác đọc title/instructions, có thể thêm `EVIDENCE REFERENCE` hoặc `SOURCE REQUIRED`, rồi tạo lựa chọn và nút giữ xác nhận theo trường hợp. Đây là ứng viên để nghiên cứu giao diện đọc/đối chiếu; không sửa nội dung câu đố.
- [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:176] Màn file có danh sách với nhãn `NEW`, `EVIDENCE`, `OPTIONAL`; [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:190] chọn file đưa `preview`, `icon`, title và body/summary vào chi tiết. Không khẳng định file thực có preview/icon được gán khi chưa inspect catalog.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:287] `TEAM JOURNAL` trình bày evidence và objective; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:223] guide cửa dùng title, hướng dẫn truy cập và objective; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:283] `FIELD SUPPLY` có nút chọn ammo/medkit. Mỗi màn cần kiểm thực tế và đánh giá liệu nên khác chất liệu hiển thị, không tự bổ sung flow.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:479] Code có các trang `PREPARATION`, `MISSION FAILED` và hoàn tất; [CODE Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs:106] có thông tin optional files trong status. Chưa kiểm scene, vị trí hiển thị hoặc nội dung đầy đủ của các màn.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignContentCatalog.cs:33] Mỗi file có title, summary, body, audio, icon, preview; [CODE Assets/FPS/Features/Missions/Runtime/CampaignContentCatalog.cs:81] objective có instructions, hint, hai tập choices và file liên quan. Đây là cấu trúc dữ liệu, không chứng minh đủ nội dung hay asset đã gán.

## Asset ứng viên, chưa duyệt

- [SUY LUẬN] Sprite panel sẵn có `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png`, control `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Controls/Control.png`, icon file `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/File.png`, icon notebook `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Icons/Mission/Notebook.png`; đây chỉ là file tồn tại, chưa xác nhận sprite 9-slice, license, chất lượng hoặc gán runtime.
- [SUY LUẬN] Ứng viên vật thể thế giới: `E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/PC_Monitor.prefab`, `E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/Keyboard.prefab`, `E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/Paper.prefab`; chưa xem prefab/render hoặc xác nhận đang dùng làm mục tiêu tương tác. Không suy từ tên file rằng có giao diện màn hình máy tính hoạt động.
- [SUY LUẬN] Với từng file/tài liệu, ảnh giấy, tem, con dấu, trang máy tính hoặc file preview phù hợp **CHƯA KIỂM**, không gọi là có/thiếu toàn kho; tạm ghi **GREYBOX PLACEHOLDER** khi chưa xác minh được asset thực. Không tự vẽ asset, import hoặc dùng imagegen.

## Đề xuất

- [SUY LUẬN] Sau khi được yêu cầu tiếp tục: kiểm các loại màn và asset bằng chứng theo chapter, phân biệt đọc giấy / màn máy tính / so sánh dữ kiện / journal và lựa chọn; sau đó đưa concept có tham chiếu cụ thể và hỏi lựa chọn, trước bất kỳ mockup hoặc thay đổi game. Áp dụng theme chung của HUD đã chọn nhưng không giả định mọi màn phải dùng cùng bố cục HUD.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] HUD đã chọn không đồng nghĩa màn cốt truyện đã được duyệt; filename prefab/sprite và class dữ liệu không chứng minh scene hiển thị chúng hoặc runtime hoạt động. Các đoạn code mô tả khả năng, không phải ảnh playtest.

## Chưa kiểm được

- [SUY LUẬN] Chưa kiểm Unity/Play Mode, catalog asset instance, từng màn/chapter, trạng thái khóa–giữ–hoàn tất, hình ảnh và license tài liệu/máy tính, keyboard focus, scroll thực tế hoặc scene isDirty. Không sửa code/scene/prefab/asset, không import, không dùng imagegen; chưa có mockup mới.


---

# Nghiên cứu mở rộng — ngôn ngữ HUD đã chọn và giao diện cốt truyện

## 1. Giới hạn và cách đọc bằng chứng

- [SUY LUẬN] Ảnh người dùng chọn được giữ tại `E:/Unity/Project/FPS/Reports/mockups/04-hud-selected.png`. Lời người dùng chọn toàn giao diện trong ảnh; các phản hồi trước bổ sung yêu cầu không đẩy các cụm về giữa, không tách đạn khỏi súng, không dùng panel lớn cho F/waypoint, có opacity nền độc lập và chữ trong game dùng tiếng Anh. Không tự coi ảnh HUD là duyệt những màn cốt truyện khác.
- [SUY LUẬN] Đã đọc `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md`: phân tích layer thành nhóm và khảo sát asset trước phác; `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`: tách cấu trúc, safe area, nội dung dài và trạng thái; `C:/Users/hieud/.agents/skills/no-ai-design-slop/SKILL.md`: so khoảng cách, phân cấp và loại chi tiết không có chức năng. `create-game-assets/SKILL.md` **KHÔNG TÌM THẤY** trong `C:/Users/hieud/.agents/skills/` và `C:/Users/hieud/.codex/skills/`; không nói đã đọc hay áp dụng. Codebase Memory MCP và Unity-Skills không có resource/tool khả dụng ở phiên này; kiểm source hẹp và snapshot audit, không gọi Editor.
- [SUY LUẬN] Dữ liệu scene bên dưới là snapshot audit 29/10/2026 tại `E:/Unity/Project/FPS/Reports/QuestFlowEvidence/interactables.json`, `interactable-properties.json`, `CampaignContentCatalog.json`; không xác nhận scene hiện tại chưa thay đổi. Danh mục là danh sách trong snapshot, **không** chứng minh đã tìm mọi UI, vật trang trí hay asset trong toàn dự án. Không công bố nội dung đáp án.

## 2. Phân tích sở thích từ ảnh chọn và lời sửa

- [SUY LUẬN] **Bố cục cạnh:** tracker gắn góc trên trái; health góc dưới trái; item+súng+đạn góc dưới phải. Trung tâm chủ yếu để cảnh, tâm ngắm và waypoint. Điều có bằng chứng là người dùng chọn ảnh và dặn sát góc; không suy ra mọi màn đọc tài liệu bắt buộc nằm ở góc — màn đọc cần đủ chỗ đọc khi chủ động mở.
- [SUY LUẬN] **Nhóm có nghĩa:** ảnh đặt bốn item cùng hai dòng súng trong một panel; đường ngang phân hàng, súng active cùng hàng với đạn. Đây là quan hệ thông tin người dùng đã trực tiếp sửa nhiều lần; khi áp sang UI cốt truyện nên đặt dữ kiện cạnh lựa chọn liên quan, không chia chúng thành các panel rời theo sở thích người thiết kế.
- [SUY LUẬN] **Phân cấp chữ:** objective đậm hơn tên nhánh, HP và đạn hiện tại nổi bật hơn số phụ; thông tin phụ nhỏ và xám hơn. Áp dụng như nguyên tắc thiết kế đề xuất: tiêu đề/tác vụ nổi bật, dữ kiện và nhãn phụ theo sau; không tự thêm câu chuyện hay đáp án.
- [SUY LUẬN] **Chất liệu:** ảnh có panel xám-đen, viền kim loại sáng nhiều lớp và ô item vuông có texture. Người dùng chọn toàn ảnh nhưng từng phản đối viền trắng dày; do vậy giữ ảnh làm chuẩn nhìn, chưa đoán độ dày/opacity chính xác của các màn mới hoặc xóa toàn bộ viền. Panel cốt truyện có thể lớn hơn HUD khi thực sự đang đọc/chọn; prompt F và waypoint vẫn không dùng panel lớn.
- [SUY LUẬN] **Giữ lại và chưa suy ra:** English, opacity nền/chữ độc lập, khung gọn và item/súng rõ là yêu cầu đã nói. Không suy người dùng thích subtitle tiếng Việt, thông báo giả lập trong ảnh, font chính xác, hay độ mờ của ảnh; không tự đặt nội dung UI quan trọng sát mép 0 px trên mọi màn.

## 3. Danh mục snapshot theo ba map

- [SCENE CampaignWorld/Factory/Map_v2/ColdLedger_Extension/Gameplay/MissionController/Objectives] Snapshot có 7 objective: `OBJ_UtilitiesBreakerWest`, `OBJ_UtilitiesBreakerEast`, `OBJ_UtilitiesGenerator`, `OBJ_LogisticsManifest`, `OBJ_LogisticsSecurityOverride`, `OBJ_ColdStorageSample`, `OBJ_ExtractionRadio`. Sáu file là `FILE_FactoryProcedure`, `FILE_FactoryManifest`, `FILE_FactoryB2Transfer`, `FILE_OptionalFactory01`, `FILE_OptionalFactory02`, `FILE_OptionalFactory03`; ba file đầu nằm dưới objective tương ứng trong snapshot.
- [SCENE CampaignWorld/Asylum/CampaignObjectives] Snapshot có 7 objective: `AsylumAccess`, `AsylumFuse`, `AsylumInstall`, `AsylumPower`, `AsylumPatient`, `AsylumTransfer`, `AsylumLift`. Bảy file là `FILE_AsylumDeskLog`, `FILE_AsylumPatientRecord`, `FILE_AsylumMortuaryTransfer`, `FILE_OptionalAsylum01`, `FILE_OptionalAsylum02`, `FILE_OptionalAsylum03`, `FILE_OptionalAsylum04`; ba file đầu nằm dưới objective tương ứng.
- [SCENE CampaignWorld/Laboratory/CampaignObjectives] Snapshot có 6 objective: `LabTrace`, `LabIndex`, `LabPower`, `LabArchive`, `LabCase`, `LabTransmit`. Tám file là `FILE_LabIndex`, `FILE_LabPowerRecord`, `FILE_LabOriginalArchive`, `FILE_OptionalLab01`, `FILE_OptionalLab02`, `FILE_OptionalLab03`, `FILE_OptionalLab04`, `FILE_OptionalLab05`; ba file đầu nằm dưới objective tương ứng.
- [SCENE Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset / files] Snapshot catalog có 21 file, 9 đánh dấu required và 12 optional; từng trường `preview` trả `null` trong snapshot, không suy không có ảnh phù hợp ở nơi khác. Snapshot cho biết số lượng và reference thời điểm audit, không bảo đảm ngày hiện tại.

## 4. Nhóm màn để thiết kế lại

| Nhóm | Bằng chứng và mục đích | Hướng thiết kế sơ bộ — chưa duyệt |
|---|---|---|
| Files / đọc tài liệu | [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:176] List, trạng thái, icon; [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:190] title và body/summary, preview fallback. | [SUY LUẬN] Shell nhất quán; danh sách và vùng đọc liên hệ rõ, title nổi bật; ảnh giấy/chữ chỉ dùng asset thực hoặc `GREYBOX PLACEHOLDER`, hỗ trợ văn bản dài và scroll. |
| Điểm đọc trong map | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:204] `documentOnly` yêu cầu discover, không tự chứng minh từng vật mở ngay panel. | [SUY LUẬN] Phân biệt nhận file và đọc file, giữ điều kiện logic thực; không giả nhận có ảnh giấy ở mọi vật. |
| Máy / đối chiếu | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:232] Title/instructions, evidence reference khi có, choices/confirm theo objective. | [SUY LUẬN] Cùng một không gian tác vụ, đặt nguồn đã thu thập cạnh trường so sánh; hierarchy như HUD, không làm bốn bảng độc lập. Không tiết lộ đáp án; từng nội dung/flow theo catalog và phải duyệt. |
| Bảng điện / công tắc | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:245] `LabPower` tạo bốn toggle; các breaker khác là objective riêng trong snapshot. | [SUY LUẬN] Chỉ hiển thị hành động cần thiết, các state đọc được mà không phụ thuộc màu; không bịa cấu hình hoặc thêm tương tác mới. |
| Journal / Key Items / Missions | [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:254] Ba tab `KEY ITEMS`, `FILES`, `MISSIONS`; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:287] `TEAM JOURNAL`. | [SUY LUẬN] Giữ tab rõ và panel có cùng ngôn ngữ vật liệu, khác với HUD thường trực; lọc thông tin theo trạng thái thực, không để lộ đáp án. |
| Cửa / tiếp tế / kết quả | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:223] Guide; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:283] supply; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:479] preparation/failure/completion. | [SUY LUẬN] Tập hợp làm nhóm màn tác vụ và kết quả, không tự thay thứ tự, lời, quy tắc hay logic. |
| Nhãn trên vật | [CODE Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:96] Có đường tạo `CampaignWorldLabel`; trạng thái từng scene chưa kiểm lại. | [SUY LUẬN] Đề xuất biển/decal gắn vật hoặc nhãn màn hình khi ngắm; asset cụ thể chưa xác minh, `GREYBOX PLACEHOLDER`, không tự thêm object. |

## 5. Tài nguyên và thiếu sót đã quan sát

- [CODE Assets/FPS/Features/UI/Runtime/HudSkin.cs:16] `HudSkin.Font` tải `Teko-Regular`/`Teko-Bold`; [CODE Assets/FPS/Features/UI/Runtime/HudSkin.cs:34] panel lấy sprite từ `Resources/HudSkin` và chọn sliced theo border sprite. Chưa kiểm importer hoặc khẳng định font này là font của ảnh được chọn.
- [SUY LUẬN] Đã mở trực tiếp `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png`: nền tối có viền tối mảnh; không khẳng định là khung nhiều lớp trong ảnh đã chọn. Giấy phép cho bộ Curated theo ghi chú `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Licenses/SunGraphica-Creepy-UI-FREE.txt` có yêu cầu credit; cần đối chiếu nguồn cụ thể từng sprite trước dùng.
- [SUY LUẬN] Đã mở `E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Icons/Mission/Notebook.png`: hình **laptop**, không phải biểu tượng tờ giấy. [SCENE Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset / files] Snapshot gán sprite tên `Notebook` cho nhiều file dạng thư/ghi chép; chưa xác nhận source path tương ứng hay hình runtime. Vì vậy không tự coi icon tên Notebook là ảnh giấy hoặc hứa có đủ preview.
- [SUY LUẬN] Tìm theo tên trong `E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/` thấy `PC_Monitor.prefab`, `Keyboard.prefab`, `Paper.prefab`; chưa xem hình prefab hoặc chứng minh chúng được gắn vào 41 điểm. File texture `E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/AsylumFacility/Textures/Paper_Albedo.png` có tồn tại, chưa xác minh chất lượng/giấy phép. Tài liệu, decal, giao diện màn máy và preview chưa kiểm đủ: **GREYBOX PLACEHOLDER** khi dựng phác.

## 6. Trình tự làm lại — chưa triển khai

1. [SUY LUẬN] Xác minh scene sạch và snapshot hiện tại bằng kênh Unity read-only khi được phép; cập nhật bảng 41 điểm, prop thực, nhãn, file và màn đang mở. Nếu scene dirty, báo và dừng; không save trong vai read-only.
2. [SUY LUẬN] Thu ảnh trước của các màn Files, tài liệu, bảng so sánh, điện, guide, journal và kết quả; kiểm từng điều kiện runtime theo flow đã duyệt, 1080p/4K. Không gọi ảnh cũ là playtest mới.
3. [SUY LUẬN] Kiểm trực quan sprite/texture/prefab và license trước khi phát minh; ghi có/thiếu, không import và không gọi imagegen khi chưa được duyệt.
4. [SUY LUẬN] Đề xuất một visual target đại diện cho màn đọc/đối chiếu và cho người dùng duyệt; duy trì style đã chọn, nhóm dữ kiện–lựa chọn, English, opacity nền độc lập; không mặc định biến HUD thành layout trung tâm.
5. [SUY LUẬN] Sau khi chốt visual target và flow: phác các biến thể tài liệu, máy, điện, journal và trạng thái khóa/giữ/hoàn tất/lỗi; kiểm chữ dài, asset thiếu, focus/scroll, đủ thông tin, không lộ đáp án. Mockup HTML/ảnh nằm trong Reports, không phải asset import.
6. [SUY LUẬN] Chờ phê duyệt mockup từng phạm vi và bàn giao hợp đồng dữ liệu cho Bot 5; không tự sửa code, scene, prefab hoặc assets. Việc đo quãng đường NavMesh và xác thực host/client thuộc kiểm flow sau, không suy ra từ phác UI.

## Đề xuất

- [SUY LUẬN] Ưu tiên kiểm một cặp đọc file + màn đối chiếu ở Factory làm mẫu, vì cùng kiểm được hierarchy, nội dung dài, nguồn và lựa chọn trước khi nhân sang Asylum/Lab; đây là thứ tự đề xuất, không thay đổi cốt truyện.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Việc dùng một panel cho nhóm thông tin liên quan không có nghĩa mọi màn cốt truyện chỉ được có một panel; F/waypoint vẫn không panel lớn. Ảnh được chọn là mốc HUD, không phê duyệt mockup cốt truyện.
- [SUY LUẬN] 41 điểm là số của snapshot audit, không kết luận trạng thái scene hiện tại. Preview null trong snapshot không đồng nghĩa mọi asset ảnh trong dự án thiếu. Notebook dạng laptop không được giới thiệu là hình tài liệu giấy.

## Chưa kiểm được

- [SUY LUẬN] Chưa kiểm scene hiện tại/dirty, Play Mode, 1080p/4K, toàn bộ prop và hình UI từng chapter, vật liệu/giấy phép từng asset, hiệu ứng/âm thanh, binding hoặc nội dung dài thực. Không sửa scene/prefab/code/asset, không import hoặc dùng imagegen; không có mockup mới. Cần duyệt flow và visual target trước khi làm lại.


---

# Ảnh giao diện có sẵn và yêu cầu chụp mới

- [SUY LUẬN] Người dùng yêu cầu chụp đầy đủ giao diện trong game để phân tích và làm mockup. Kiểm lần này không thấy process `Unity`, `127.0.0.1:8090/health` từ chối kết nối; không thể kiểm `scene.isDirty` hay chụp mới. Không tự mở Unity vì chưa thể kiểm Save Gate và thao tác khởi động có thể import/compile; không lưu hay sửa asset.
- [SUY LUẬN] Đã đọc `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md` (tách ảnh trước, mockup và xác minh thật); `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md` (kiểm nhiều kích thước, scroll/focus); `C:/Users/hieud/.agents/skills/unity-skills/SKILL.md`, `C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md` và `C:/Users/hieud/.agents/skills/unity-skills/skills/scene/SKILL.md` (kiểm trạng thái Editor/scene qua bridge trước thao tác). `create-game-assets/SKILL.md` **KHÔNG TÌM THẤY** tại hai thư mục skill đã kiểm ở lượt trước; không nói đã đọc hay áp dụng.
- [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/AssetReplacement/Gameplay/asset-files-1920x1080-100.png] Ảnh fixture cũ: `SQUAD INVENTORY`, ba tab, danh sách file bên trái, chi tiết bên phải; icon laptop lặp, dòng title dài lặp lại và vùng đọc bên phải cuộn. Ảnh không chứng minh trạng thái campaign hiện tại. Theo `E:/Unity/Project/FPS/Reports/UIUX/AssetReplacement/BanGiao.md`, đây thuộc fixture/kiểm layout, có stress data.
- [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/AssetReplacement/Gameplay/asset-document-1920x1080-100.png] Ảnh fixture cũ: panel chữ lớn ở giữa, nút `CLOSE JOURNAL` và `RESUME CHECKPOINT` bên dưới, subtitle dưới cùng; tên file ảnh có chữ document nhưng nội dung quan sát là journal đang cuộn. Không gọi đó là ảnh từng trang file trong map.
- [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/AssetReplacement/Gameplay/missions-1920x1080-100.png] Ảnh fixture cũ: danh sách nhiệm vụ bên trái, detail bên phải, trạng thái `AVAILABLE`, `LOCKED`, `TRACKING`; icon trắng lớn so với chữ. Không suy ra mọi bước hoặc thứ tự hiện tại từ hình.
- [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/AssetReplacement/Gameplay/debrief-completed-1920x1080-100.png] Ảnh fixture cũ: bảng `MISSION COMPLETE` ở giữa, các dòng thống kê và discovery, subtitle ở đáy. Không xem là bằng chứng kết thúc campaign tự nhiên.
- [SUY LUẬN] Cần chụp mới từng file, các màn so sánh, điện, cửa, pickup, trạng thái khóa/giữ/hủy/hoàn tất/lỗi, journal, ba tab, preparation, failed/completed và các prop trong ba map; một ảnh cho mỗi nhóm không đủ để xác minh tất cả 41 điểm. Chưa có danh sách ảnh đầy đủ hay mockup mới.

## Đề xuất

- [SUY LUẬN] Người dùng mở đúng project trong Unity với scene đã lưu sạch, bật Unity-Skills; sau đó kiểm `isDirty`, thỏa Save Gate trước/sau Play Mode theo đúng quyền được cấp, chụp vào Reports, không lưu scene tạm. Nếu SaveAssets có thể ghi asset chưa lưu thì cần làm rõ trước thay đổi trạng thái Editor.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Các ảnh fixture cũ có thể phân tích bố cục nhưng không thay thế ảnh mới từng màn/Play Mode. Không đổi tên ảnh journal thành tài liệu tương tác trong map; không gán các kết quả test cũ vào phiên hiện tại.

## Chưa kiểm được

- [SUY LUẬN] Chưa chụp ảnh mới, chưa kiểm editor/scene, runtime từng map, 1080p/4K hiện tại hoặc tạo mockup tiếp theo; `scene.isDirty == false` chưa xác minh. Không vào Unity/Play Mode, không import, không sửa code/scene/prefab/asset và không gọi imagegen.


---

# Đính chính: màn thiết bị campaign

- [SUY LUẬN] Trả lời trước đưa ba ảnh Files/Missions/Journal nhưng chưa đưa màn thiết bị, không phải toàn bộ màn campaign; thiếu sót này chưa thể coi là hoàn thành yêu cầu chụp.
- [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/AssetReplacement/Gameplay/campaign-hold.png] Ảnh fixture cũ 28/10/2026 hiển thị panel `ISOLATE PRODUCTION`, `SOURCE REQUIRED`, vòng tiến trình và `HOLD F TO CONFIRM`; nó là ví dụ màn campaign khi giữ, không phải bằng chứng đã chụp toàn bộ thiết bị.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:204] Campaign mở panel theo interactable; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:232] render title, chỉ dẫn, evidence và choices; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:245] `LabPower` có bốn toggle; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:223] cửa có guide; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:281] supply có panel chọn. Có màn thiết bị và campaign ngoài ba ảnh trước, nhưng chưa có ảnh mới cho từng cái.
- [SUY LUẬN] Cần chụp Factory: manifest, panel Shipping, Isolate/Backup/Generator, case/radio; Asylum: Access, Fuse/Install/Power, Patient/Transfer/Lift; Laboratory: Trace/Index/Power/Archive/Case/Transmit; cộng tài liệu, file, guide, supply, journal, khóa/giữ/hủy/hoàn tất/lỗi. Tên mục tiêu là danh mục snapshot, không chứng minh mọi mục hiện mở UI riêng.
- [SUY LUẬN] Đã đọc `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md`: phân biệt ảnh fixture với runtime, nhóm màn theo nhiệm vụ; `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`: ghi đủ trạng thái và kiểm hiển thị thực. `create-game-assets/SKILL.md` **KHÔNG TÌM THẤY** trong hai thư mục skill đã kiểm trước; không nói đã đọc hay áp dụng.

## Đề xuất

- [SUY LUẬN] Mở Unity project đúng và Unity-Skills; kiểm scene sạch, Save Gate và quyền ghi asset trước Play Mode; chụp từng thiết bị và trạng thái vào Reports, không chỉnh scene/code/asset.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Có màn thiết bị theo code và một ảnh fixture không nghĩa là đã chụp hết campaign; ba ảnh trước là minh họa tồn tại, không phải danh mục đầy đủ.

## Chưa kiểm được

- [SUY LUẬN] Lần kiểm lại Unity/bridge vẫn offline; chưa vào Play Mode, chưa chụp mới, chưa xem hết từng device và chưa xác minh `scene.isDirty`. Không import hay sửa code/scene/asset; chưa có mockup mới.


---

# Kiểm Unity trước khi chụp campaign

- [SCENE Assets/FPS/Scenes/MainMenu.unity / isDirty] Unity-Skills `scene_get_info` báo scene đang mở `MainMenu`, đường dẫn `Assets/FPS/Scenes/MainMenu.unity`, `isDirty=false`; `scene_get_loaded` báo chỉ một scene và `editor_get_state` báo `isPlaying=false`, `isCompiling=false` tại lúc kiểm. Chưa mở GameScene, chưa vào Play Mode, chưa chụp màn thiết bị.
- [SUY LUẬN] Đã đọc `C:/Users/hieud/.agents/skills/unity-skills/SKILL.md` (health/meta trước truy vấn), `C:/Users/hieud/.agents/skills/unity-skills/skills/scene/SKILL.md` (kiểm scene đang mở và dirty), `C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md` (kiểm play/compile trước thao tác). `create-game-assets/SKILL.md` **KHÔNG TÌM THẤY** ở hai thư mục đã kiểm trước, không nói đã đọc hay áp dụng.
- [SUY LUẬN] Save Gate yêu cầu lưu authored scene có đường dẫn và gọi `AssetDatabase.SaveAssets()` trước Play Mode hoặc đổi scene; lệnh có thể ghi asset đang chờ lưu. Chỉ thị vai cấm sửa scene/asset nên chưa tự thực hiện, đang xin xác nhận rõ ràng trước khi thực hiện gate.

## Đề xuất

- [SUY LUẬN] Sau khi được cho phép ghi theo Save Gate, xác minh lại scene và bảo vệ dữ liệu; sau đó tìm đường vào campaign và chụp trạng thái thiết bị tại `Reports/`, không tạo/cải sửa UI.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] `isDirty=false` là kết quả kiểm MainMenu tại thời điểm truy vấn, không phải trạng thái GameScene hay chứng minh asset không có thay đổi chưa lưu.

## Chưa kiểm được

- [SUY LUẬN] Chưa kiểm asset pending, chưa vào GameScene/Play Mode, chưa chụp màn thiết bị; không import hay sửa code/scene/prefab/asset. Cần xác nhận việc lưu trước khi tiếp tục.


---

# Cập nhật ngày 30/10/2026: thử mở campaign

- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Trước thao tác, Unity-Skills báo MainMenu có đường dẫn, isDirty=false, không Play và không biên dịch. Sau khi người dùng đồng ý Save Gate, đã gọi scene_save và menu Tools/AI Director/Save Authored Assets trước Play và sau khi dừng Play; menu này gọi AssetDatabase.SaveAssets() [CODE Assets/Editor/DirectorVerification/DirectorLiveProbe.cs:79]. Cuối lượt Unity-Skills báo MainMenu isDirty=false, isPlaying=false.
- [SCENE Assets/FPS/Scenes/MainMenu.unity] Đã vào Play Mode trong MainMenu, chưa vào GameScene hoặc tương tác thiết bị campaign. Unity-Skills báo isPlaying=true; sau đó đã dừng. Không dùng scene_screenshot vì skill ghi vào Assets/Screenshots; chưa chụp ảnh mới.
- [SUY LUẬN] Cửa sổ trò chơi khác hiện toàn màn hình; khi chọn cửa sổ Unity, công cụ quan sát hiển thị nội dung trò chơi đó, và lệnh đưa Unity lên trước thất bại: failed to activate captured window. Không dùng ảnh đó làm bằng chứng campaign.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignCheckpoint.cs:76] Checkpoint lưu trong Application.persistentDataPath, tên cold-ledger-campaign-v1.json; Save sử dụng File.Replace và tạo bản .previous. [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:747] Checkpoint đầu không ghi đè tệp đã có nhưng các checkpoint sau có thể ghi. Có tệp chính và .previous tại C:/Users/hieud/AppData/LocalLow/HieuDHM/FPS/; kích thước và thời gian sửa không đổi sau lần thử. Chưa xác minh checksum.
- [SUY LUẬN] Đã đọc C:/Users/hieud/.agents/skills/unity-skills/SKILL.md (kiểm health và dry-run), C:/Users/hieud/.agents/skills/unity-skills/skills/scene/SKILL.md (kiểm scene/dirty và giới hạn đường dẫn ảnh), C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md (kiểm trạng thái và Play/Stop), C:/Users/hieud/.codex/plugins/cache/openai-bundled/computer-use/26.1023.51421/skills/computer-use/SKILL.md cùng docs/guidance.md và docs/confirmations.md (quan sát cửa sổ, không tiếp tục khi kích hoạt thất bại). Các skill thiết kế đã ghi ở các mục trước; create-game-assets/SKILL.md vẫn KHÔNG TÌM THẤY.

## Đề xuất

- [SUY LUẬN] Thu nhỏ trò chơi toàn màn hình, đưa Unity lên trước; sau đó thử lại, chỉ chụp ảnh ngoài Assets. Trước khi tiến tới checkpoint về sau cần thống nhất cách bảo vệ save hiện có.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Mục trước nói chưa vào Play và chưa lưu là trạng thái tại thời điểm viết mục đó; cập nhật này ghi nhận đã lưu theo quyền người dùng cấp và đã vào rồi thoát Play. Chưa chụp màn thiết bị vẫn đúng.

## Chưa kiểm được

- [SUY LUẬN] Chưa quan sát màn thiết bị Factory/Asylum/Laboratory trong gameplay; chưa có ảnh mới 1080p hoặc 4K, chưa playtest các trạng thái, chưa làm mockup các màn này và chưa kiểm checksum checkpoint. Không tuyên bố đã chụp đủ hoặc phê duyệt thiết kế.

## Đính chính về Edit Mode

- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Ngày 30/10/2026 Unity-Skills báo MainMenu là scene duy nhất đang mở, isDirty=false, isPlaying=false. Có thể truy vấn scene, object và component bằng Unity-Skills mà không cần Computer Use; việc cửa sổ khác che Unity không chứng minh các cách chụp khác đều bất khả thi.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:47] CampaignHUD gọi Build trong Awake; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:204] Open yêu cầu item và Campaign hợp lệ rồi mới gọi RenderDocument; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:232] nội dung và lựa chọn được dựng theo item/trạng thái. Vì vậy không thể coi cấu trúc authored trong Edit Mode là ảnh thực tế của tất cả trạng thái runtime.
- [CODE Assets/FPS/Features/UI/Editor/UiVisualVerification.cs:20] Có công cụ render bản sao UI trong Edit Mode; [CODE Assets/FPS/Features/UI/Editor/HudReviewTools.cs:15] lệnh capture baseline nêu rõ không gồm campaign runtime canvases. Không gọi các lệnh này, không khẳng định chúng chụp được màn thiết bị.

## Đề xuất

- [SUY LUẬN] Ưu tiên khảo sát Editor qua Unity-Skills và tìm cách capture phù hợp giới hạn Reports/ trước khi yêu cầu thao tác cửa sổ; chỉ dùng Play Mode khi cần chứng minh trạng thái runtime.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Khó kích hoạt cửa sổ Unity không đồng nghĩa bắt buộc dùng Computer Use hay không thể kiểm tra Edit Mode; đây là đính chính của kết luận trước.

## Chưa kiểm được

- [SUY LUẬN] Chưa tìm được phương án chụp trực tiếp các màn thiết bị runtime vào Reports/ không ghi Assets/; chưa chụp các màn thiết bị mới.


# Khảo sát Edit Mode ngày 30/10/2026

- [SCENE Assets/FPS/Scenes/GameScene.unity/CampaignWorld] Đã mở GameScene trong Edit Mode sau Save Gate, gọi scene_find_objects với componentType CampaignInteractable và limit 200: trả 41 object, gồm 7 object objective Factory, 7 Asylum, 6 Laboratory và 21 object tên FILE_. Đây là kiểm kê object, không phải khẳng định có 41 giao diện khác nhau.
- [SCENE CampaignWorld/Asylum/CampaignObjectives/AsylumAccess] Ví dụ điểm cần khảo sát màn: AsylumAccess; [SCENE CampaignWorld/Laboratory/CampaignObjectives/LabPower] LabPower; [SCENE CampaignWorld/Factory/Map_v2/ColdLedger_Extension/Gameplay/MissionController/Objectives/OBJ_LogisticsManifest] OBJ_LogisticsManifest. Chưa chụp ảnh hay mở các màn đó.
- [CODE Assets/FPS/Features/UI/Editor/HudReviewTools.cs:149] Capture Gameplay gọi ScreenCapture.CaptureScreenshot tại Documents/UIUX/HUD-Evidence và ghi thêm tệp bên đó; không chạy vì quy định đặt ảnh trong Reports/. [CODE Assets/FPS/Features/UI/Editor/HudReviewTools.cs:15] Capture Baseline không gồm campaign runtime canvases.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Đã lưu theo Save Gate trước/sau mở GameScene và trước/sau khôi phục MainMenu. Cuối lượt MainMenu isDirty=false, isPlaying=false. Không sửa nội dung scene, prefab, code hay import asset.
- [SUY LUẬN] Đã đọc C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md (khảo sát layout trước khi thiết kế), C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md (ghi nhận trạng thái và độ phân giải thực tế), C:/Users/hieud/.agents/skills/unity-skills/skills/camera/SKILL.md (phân biệt ảnh camera và Game View).

## Đề xuất

- [SUY LUẬN] Tiếp tục tìm capture runtime xuất thẳng Reports/ và phương án tránh thay thế checkpoint; sau đó chụp từng nhóm thiết bị, phân tích theo HUD đã chọn và chỉ dựng mockup khi có đủ bằng chứng.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] 41 là số interactable được truy vấn trong scene, không phải số màn hình hoặc số ảnh đã chụp. Không coi ảnh fixture cũ là ảnh chơi mới.

## Chưa kiểm được

- [SUY LUẬN] Chưa chụp màn thiết bị, chưa kiểm các trạng thái runtime hoặc 1080p/4K; chưa xác nhận có cách capture thẳng Reports/ mà không cần thay đổi project.


# Thử chụp campaign qua Unity-Skills

- [PLAYTEST E:/Unity/Project/FPS/Reports/CampaignScreens-2026-10-30/01-exploring.png] Đã dùng host cục bộ phục vụ review, không dùng Computer Use; ảnh Game View thực tế 2560×1080 cho thấy tracker “Read the shipping manifest” và marker “271m ROUTE”. Đây là gameplay ở điểm xuất phát, không phải màn thiết bị.
- [PLAYTEST E:/Unity/Project/FPS/Reports/CampaignScreens-2026-10-30/03-route.png] Đã điều khiển qua fps_playtest_input; marker còn “207m ROUTE”. [PLAYTEST E:/Unity/Project/FPS/Reports/CampaignScreens-2026-10-30/05-route.png] Marker còn “192m ROUTE” nhưng trước camera là tường; không mở được manifest trong lần thử này. Các ảnh 02–06 ghi hành trình chứ không phải các giao diện thiết bị.
- [CODE Assets/FPS/Features/UI/Editor/HudReviewTools.cs:149] Với sự đồng ý của người dùng, đã dùng Capture Gameplay ghi tạm vào Documents/UIUX/HUD-Evidence, kiểm tra không trùng file rồi chuyển PNG và TXT vào Reports/CampaignScreens-2026-10-30/. Không dùng scene_screenshot vì công cụ đó ghi Assets/Screenshots/. [CODE Assets/FPS/Features/UI/Editor/HudReviewTools.cs:74] Đã chọn Game View 2560×1080 bằng lệnh review; kích thước được xác nhận qua Camera.pixelWidth/pixelHeight, nhưng chưa biết hoặc khôi phục được lựa chọn Game View ban đầu.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Đã ngừng virtual input, thoát Play, lưu theo Save Gate sau khi về MainMenu; kiểm tra cuối: isDirty=false, isPlaying=false. Save Gate được gọi trước chuỗi Play/local host và sau khi dừng; không lưu runtime scene. [SUY LUẬN] Hash SHA-256 của checkpoint chính và bản .previous trước/sau giống nhau.
- [SUY LUẬN] Đã đọc Documents/AgentPlaytest/README.md: thao tác qua input bình thường, chờ hoàn tất và quan sát ảnh mới; không dịch chuyển transform hoặc gọi hàm giải quest. Các skill đã đọc và quy tắc áp dụng được ghi tại các mục trước.

## Đề xuất

- [SUY LUẬN] Tiếp tục tìm đường tới manifest bằng quan sát frame, chụp các màn thiết bị và file trước khi làm mockup; giữ checkpoint nguyên vẹn. Đánh giá ảnh 1080p và 4K sau khi tiếp cận được giao diện cần chụp.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Đã có ảnh Game View mới nhưng chưa có ảnh màn thiết bị; không gọi các ảnh đường đi là hoàn thành việc chụp campaign. Nhận định trước rằng chưa tìm được cách capture cần cập nhật: đã có cách ghi tạm ngoài Reports theo ngoại lệ người dùng duyệt.

## Chưa kiểm được

- [SUY LUẬN] Chưa mở màn manifest, chưa chụp màn thiết bị của ba chapter, chưa có mockup mới hoặc ảnh runtime 1920×1080/3840×2160; chưa khôi phục lựa chọn độ phân giải Game View trước lần thử.


# Kiểm tra trực tiếp object manifest

- [SCENE CampaignWorld/Factory/Map_v2/ColdLedger_Extension/Gameplay/MissionController/Objectives/OBJ_LogisticsManifest/interactionBody] Unity-Skills trả body mang tên Wooden_box_v1_LD1square (21); nó có CampaignInteractionProxy. Object objective có BoxCollider isTrigger=true. [CODE Assets/FPS/Features/Interaction/Runtime/InteractionManager.cs:126] Việc hướng camera vào body phụ thuộc raycast; chỉ đứng gần object chưa chứng minh mở được màn.
- [PLAYTEST E:/Unity/Project/FPS/Reports/CampaignScreens-2026-10-30/07-manifest-location.png] Đã điều khiển qua host review và đặt trực tiếp transform của player clone trong Play Mode gần manifest, không lưu runtime scene; ảnh hiện LOGISTICS / MANIFEST, chưa mở panel. Lần thử chỉnh góc nhìn chưa mở được màn, nên ảnh này không phải màn thiết bị.
- [PLAYTEST E:/Unity/Project/FPS/Reports/UIUX/AssetReplacement/Gameplay/campaign-hold.png] Ảnh fixture cũ hiện màn ISOLATE PRODUCTION, SOURCE REQUIRED và HOLD F TO CONFIRM. Đây không phải ảnh mới hoặc playtest campaign tự nhiên.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Đã ngừng input, thoát Play và thực hiện Save Gate; MainMenu isDirty=false. Hash hai tệp checkpoint trước/sau giống nhau. [SUY LUẬN] Đã đọc C:/Users/hieud/.agents/skills/unity-skills/skills/gameobject/SKILL.md để truy vấn vị trí và điều chỉnh transform clone chỉ trong Play Mode, và C:/Users/hieud/.agents/skills/unity-skills/skills/component/SKILL.md để đọc component.

## Đề xuất

- [SUY LUẬN] Tiếp tục tìm cơ chế preview runtime có sẵn, không đi bộ, và phân loại ảnh fixture riêng với ảnh chơi thật.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Đặt clone gần object khác với sửa authored scene và khác playtest đi đến mục tiêu; không tuyên bố panel đã được mở hoặc cảnh đã chụp đầy đủ.

## Chưa kiểm được

- [SUY LUẬN] Chưa chụp mới panel manifest hoặc màn thiết bị Asylum/Laboratory; ảnh fixture cũ chỉ chứng minh trạng thái ISOLATE PRODUCTION lúc chụp.


# Ảnh màn manifest trực tiếp

- [PLAYTEST E:/Unity/Project/FPS/Reports/CampaignScreens-2026-10-30/13-manifest.png] Đã mở màn READ DELIVERY MANIFEST bằng tương tác F trong Play Mode; bố cục gồm panel xám đen lớn giữa màn, viền kim loại, nút CLOSE [I] ở trên phải, tiêu đề và EVIDENCE REFERENCE ở trên trái, thông báo NEW FILE ADDED gần giữa. Ảnh 2560×1080 chụp qua host review, player clone được đặt gần mục tiêu bằng Unity-Skills, không phải playtest đường đi tự nhiên.
- [SCENE CampaignWorld/Factory/Map_v2/ColdLedger_Extension/Gameplay/MissionController/Objectives/OBJ_UtilitiesBreakerWest/CampaignInteractable] Đã định vị FactoryIsolate, body Electric_box_v2; chưa chụp được panel trong lượt này vì chưa đưa được tâm nhìn về đúng body sau dịch chuyển.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Đã dừng input, thoát Play, lưu theo Save Gate; MainMenu isDirty=false. Hash checkpoint chính và .previous không đổi so với trước lượt.

## Đề xuất

- [SUY LUẬN] Chụp tiếp FactoryShipping, FactoryIsolate, FactoryBackup, FactoryGenerator và các màn Asylum/Laboratory; không thay thế ảnh thiếu bằng ảnh đường đi.

## Tự kiểm mâu thuẫn

- [SUY LUẬN] Mục trước nói chưa chụp manifest là đúng tại thời điểm trước; lượt này đã chụp được. Ảnh manifest không chứng minh đã chụp các màn khác hoặc gameplay tự nhiên.

## Chưa kiểm được

- [SUY LUẬN] Các panel thiết bị khác, ảnh 1080p/4K và mockup chưa hoàn thành; không xác nhận đầy đủ campaign.

# Đánh giá bảy giao diện campaign — 31/10/2026

## Phạm vi và skill
- [PLAYTEST Reports/CampaignScreens-2026-10-30/13-manifest.png] Manifest mở bằng F sau khi đặt clone gần vật; các ảnh còn lại là fixture/review cũ, không chứng minh tiến trình chơi tự nhiên.
- [SUY LUẬN] Đối chiếu Reports/mockups/04-hud-selected.png: nhóm thông tin ở góc, chữ sáng, súng và đạn cùng cụm; đây là tham chiếu thẩm mỹ, không phải phê duyệt màn campaign. UI mới dùng tiếng Anh.
- [SUY LUẬN] Đã đọc C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md: đối chiếu cấu trúc, phần lặp và tỷ lệ, không xem ảnh tĩnh là xác minh runtime.
- [SUY LUẬN] Đã đọc C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md: so ảnh nhiều tỷ lệ, không suy ra focus, gamepad hoặc safe area.
- [SUY LUẬN] Đã đọc C:/Users/hieud/.agents/skills/audit-ai-design-slop/SKILL.md: ghi vị trí, tác hại và ưu tiên; giảm chi tiết lặp trước khi thêm trang trí.
- [SUY LUẬN] Đã đọc C:/Users/hieud/.agents/skills/unity-skills/SKILL.md và C:/Users/hieud/.agents/skills/unity-skills/skills/perception/SKILL.md để tìm truy vấn read-only; Unity-Skills cổng 8090–8100 không phản hồi nên không khẳng định đã dùng Unity MCP thành công.
- [SUY LUẬN] create-game-assets/SKILL.md KHÔNG TÌM THẤY tại C:/Users/hieud/.agents/skills/create-game-assets/SKILL.md và C:/Users/hieud/.codex/skills/create-game-assets/SKILL.md; không nhận đã đọc hoặc áp dụng. Reports/02-ui-assets.md là audit tạm.

## Đánh giá
1. **P1 Files:** [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/asset-files-1920x1080-100.png] Tên Contractor Export Email và AL-04 Delivery Manifest lặp qua dấu /; khối chi tiết bắt đầu giữa câu rồi lặp đoạn phía dưới. [SUY LUẬN] Cần kiểm dữ liệu fixture trước khi quy lỗi code; đề xuất hiện tên một lần và đoạn mở đầu đầy đủ.
2. **P1 Khả năng đọc:** [PLAYTEST Reports/CampaignScreens-2026-10-30/13-manifest.png] Evidence trải gần hết chiều ngang panel; [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/asset-files-1024x768-100.png] chữ thân hẹp, tên xuống 2–3 dòng. [SUY LUẬN] Giới hạn chiều dài dòng và phân cấp chữ; chưa đo cỡ chữ hoặc contrast.
3. **P1 Isolate:** [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/campaign-hold.png] SOURCE REQUIRED trên trái, HOLD F TO CONFIRM ở đáy, vòng giữ nhỏ ở giữa. [SUY LUẬN] Gom điều kiện và hành động rõ hơn; ảnh không chứng minh có thể xác nhận khi thiếu nguồn.
4. **P1 Cuộn Files:** [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/asset-files-1024x768-100.png] Hàng đầu chỉ còn NEW, tên khuất ở mép trên. [SUY LUẬN] Có thể do đang cuộn giữa hàng; chưa kết luận lỗi clipping tự động.
5. **P2 Missions:** [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/missions-1920x1080-100.png] Vỏ ngoài, tab, từng hàng và hai cột đều có viền/texture mạnh; sao và khóa lớn lặp, TRACKING màu cam. [SUY LUẬN] Giảm trang trí/icon lặp, giữ chữ trạng thái; chuẩn dự án dành cam cho việc cần làm ngay.
6. **P2 Journal:** [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/asset-document-1920x1080-100.png] Không thấy tiêu đề Journal; CLOSE [I], CLOSE JOURNAL và RESUME CHECKPOINT ở hai khu vực; panel gần chạm subtitle. [SUY LUẬN] Làm rõ phân cấp và ý nghĩa thao tác; chưa chứng minh hai nút đóng trùng chức năng, không tự bỏ subtitle.
7. **P2 Kết quả:** [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/debrief-completed-1920x1080-100.png] MISSION COMPLETE và thống kê thành cột chữ nhỏ giữa panel rộng; [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/debrief.png] MISSION FAILED có RETRY CHECKPOINT ở đáy. [SUY LUẬN] Phân cấp kết quả, thống kê và hành động; chưa kiểm retry hoặc hành động ngoài ảnh.

## Đề xuất
- [SUY LUẬN] Mockup Files/tài liệu trước, tiếp theo Isolate rồi Missions/Journal và kết quả; giữ tông công nghiệp tiết chế. Panel tối vẫn dùng được cho màn đọc/thiết bị: người dùng chỉ bác bỏ panel ở prompt F và waypoint.
- [SUY LUẬN] Đưa tùy chọn opacity vào đề xuất; chưa kiểm thiết lập hiện tại hay khoảng phù hợp. Chưa sửa code, scene hoặc asset.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Xem ảnh nhiều độ phân giải không đồng nghĩa đã kiểm runtime; giữ panel tài liệu không mâu thuẫn yêu cầu bỏ panel prompt và waypoint.

## Chưa kiểm được
- [SUY LUẬN] Unity-Skills không kết nối nên KHÔNG TÌM THẤY isDirty hiện tại; lượt này không vào Editor/Play Mode, không lưu hay import.
- [SUY LUẬN] Chưa có ảnh màn thiết bị Factory còn lại, Asylum, Laboratory; chưa kiểm opacity, focus, cuộn, hành vi khóa, âm thanh hoặc thời gian; chưa tạo mockup mới.

# Mockup bảy màn campaign — 31/10/2026

## Phạm vi và cây bố cục
- [SUY LUẬN] Reports/mockups/04-campaign-review.html là bản phác HTML/CSS để duyệt hình, không phải asset import; không sửa HUD được chọn, scene, prefab, code hoặc asset. Nội dung tài liệu chỉ trích đoạn từ ảnh cũ, không đề nghị thay đổi nội dung cốt truyện.
- [SUY LUẬN] Cây chung: nền ảnh Game cũ → lớp làm tối → panel dùng ảnh 9-slice → header và nút đóng → vùng nội dung → chân panel. Manifest: nhãn evidence trái / văn bản phải. Files và Missions: tab → danh sách trái / chi tiết phải. Journal: đầu mục → đoạn văn → hành động. Isolate: nội dung trái / khóa hoặc giữ phải. Complete/Failed: kết quả → số liệu → discovery → hành động.
- [SUY LUẬN] Bảng màu theo chuẩn: trắng chính, xám phụ/nền, cam cho hành động tức thời và đỏ cho thất bại; ba cấp thiết kế 18/24/36px tại 1920×1080. Font chính Assets/TextMesh Pro/Fonts/LiberationSans.ttf, font số Assets/FPS/Features/UI/Content/Resources/HudSkin/Fonts/Teko-Bold.ttf. License LiberationSans: Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt; license Teko KHÔNG TÌM THẤY trong thư mục font đã kiểm.
- [SUY LUẬN] Dùng Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png làm khung 9-slice; icon từ Assets/FPS/Features/UI/Content/Resources/HudSkin/ và nền Documents/LevelDesignAudit/2026-10-22/screenshots/audit-arrival-wide.png. Các đường kẻ, ô phím và nền phụ do CSS tạo là GREYBOX PLACEHOLDER cho bản phác, không phải asset import.
- [SUY LUẬN] Đã đọc C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md: dựng cây và so bố cục trước thực thi Unity; game-ui-ux/SKILL.md: xem thêm 1024×768; audit-ai-design-slop/SKILL.md: bỏ chữ và viền lặp; no-ai-design-slop/SKILL.md: không thêm trang trí vô cớ; ui-ux-pro-max/SKILL.md: kiểm chữ dễ đọc, cấu trúc nhất quán. Các đường dẫn skill này đều thuộc C:/Users/hieud/.agents/skills/.
- [SUY LUẬN] create-game-assets/SKILL.md KHÔNG TÌM THẤY tại hai thư mục skill đã kiểm; không nhận đã áp dụng. Không gọi imagegen.

## Ảnh và thử nghiệm
- [SUY LUẬN] Bảy loại màn tại Reports/mockups/04-campaign-{manifest,isolate,files,journal,missions,complete,failed}.png; isolate-hold.png là ảnh bổ sung trạng thái giữ; tất cả là render HTML 1920×1080, không phải ảnh Play Mode.
- [SUY LUẬN] Edge headless: tám ảnh đều tải icon và font Lib; không báo pageerror. Bộ chọn chuyển màn và thanh opacity thử 70% trong HTML hoạt động. Các nút trong bản phác là trình bày, không thực thi tương tác game.
- [SUY LUẬN] Đã xem Reports/mockups/04-campaign-missions-1024x768.png: toàn panel nằm trong khung nhưng bị thu nhỏ toàn bộ, chữ nhỏ; không xem đó là đạt chuẩn responsive hoặc thay thế kiểm 1080p/4K trong Unity.
- [SUY LUẬN] Isolate khóa có biểu tượng khóa và Requires, không có F; ảnh giữ chỉ minh họa trạng thái giữ sau khi đủ điều kiện. Vòng tiến trình là ảnh tĩnh, chưa thể hiện tiến độ hoặc hủy thực. Thanh opacity chỉ tác động panel, dải 65–100% là giá trị thử, chưa phải cài đặt được duyệt.

## Đề xuất
- [SUY LUẬN] Sau khi người dùng duyệt diện mạo từng màn, xác minh màn thiết bị còn thiếu và trạng thái, sau đó mới làm mockup bổ sung; không triển khai Unity trước phê duyệt.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Không xóa panel tài liệu: yêu cầu bỏ panel chỉ áp dụng prompt F và waypoint. Mẫu khóa không có F; trạng thái giữ là hình riêng, không khẳng định đã kiểm flow.

## Chưa kiểm được
- [SUY LUẬN] Chưa có ảnh các màn Factory khác, Asylum, Laboratory; chưa kiểm tính đủ nội dung, điều khiển game, quyền truy cập, tỷ lệ chữ ở 4K, tương phản nền sáng/tối hay hiệu ứng/âm thanh. Trạng thái isDirty Unity hiện tại KHÔNG TÌM THẤY vì chưa kết nối lại; lượt này không vào Editor/Play Mode.

# Mẫu màn hình thiết bị — 31/10/2026

## Phạm vi
- [SUY LUẬN] Theo phản hồi người dùng, chỉ thay mẫu 1 Manifest và 2 Isolate bằng hình dung màn máy tính/industrial control, tách khỏi khung HUD; năm màn còn lại không sửa.
- [SUY LUẬN] Reports/mockups/04-device-screens-concept.html và ảnh 04-device-manifest.png, 04-device-locked.png, 04-device-hold.png là render HTML, không phải Game View, không phải asset import. Vỏ máy, màn hình, đường kẻ và nền phụ do CSS dựng là GREYBOX PLACEHOLDER; không đại diện asset 3D đã tìm thấy.
- [SCENE CampaignWorld/Factory/Map_v2/ColdLedger_Extension/Gameplay/MissionController/Objectives/OBJ_LogisticsManifest] Kết quả khảo sát phiên trước xác định body là Wooden_box_v1_LD1square (21); [SCENE CampaignWorld/Factory/Map_v2/ColdLedger_Extension/Gameplay/MissionController/Objectives/OBJ_UtilitiesBreakerWest] body là Electric_box_v2. [SUY LUẬN] Chưa xác minh hai body này có màn hình; mẫu thiết bị không chứng minh trong scene đã có terminal. Nếu phải thêm/thay vật tương tác hoặc đổi flow, cần phê duyệt riêng.
- [SUY LUẬN] Tái sử dụng icon Assets/FPS/Features/UI/Content/Resources/HudSkin/Locked.png, vòng Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/GoalProgress.png và font Assets/TextMesh Pro/Fonts/LiberationSans.ttf; nền lấy từ Reports/CampaignScreens-2026-10-30/07-manifest-location.png. Không tạo asset mới, không gọi imagegen.
- [SUY LUẬN] Theo các skill đã đọc và ghi ở mục trước: unity-mcp-ui-layout phân tách layer thiết bị và nội dung; game-ui-ux giữ chữ lớn và phân biệt trạng thái; audit-ai-design-slop giảm khung HUD lặp. Không khẳng định create-game-assets đã đọc.

## Thiết kế và kiểm tra
- [SUY LUẬN] Manifest: thanh tiêu đề của hệ thống nhà máy, menu hồ sơ trái và tài liệu đọc bên phải; văn bản là trích đoạn, chưa được phê duyệt thay nội dung.
- [SUY LUẬN] Isolate: màn điều khiển riêng, khung hướng dẫn và trạng thái; khóa có icon khóa, Requires và không có F; ảnh giữ hiển thị vòng tĩnh và F. Phần READY, LOCKED và IN PROGRESS là trạng thái minh họa, chưa chứng minh logic game.
- [SUY LUẬN] Trình duyệt Edge chụp ba ảnh 1920×1080, font và ảnh không báo lỗi; screenshot không chứng minh tương tác game, tiến trình giữ, âm thanh hoặc chuyển trạng thái.

## Đề xuất
- [SUY LUẬN] Sau khi chọn hướng màn hình, khảo sát màn và vật tương tác thực tế trước khi quyết định có cần 3D prop; không tự ý đổi object/flow.

## Tự kiểm mâuẫn
- [SUY LUẬN] Mẫu máy tính là hình dung thiết kế theo yêu cầu, không tuyên bố vật hiện tại đã có màn hình; vỏ CSS là greybox chứ không phải asset đã duyệt.

## Chưa kiểm được
- [SUY LUẬN] Chưa xem lại scene hiện tại/isDirty, chưa kiểm 4K, kiểm opacity của phương án mới, điều khiển, nguồn/đáp án, vòng giữ động và các thiết bị khác; chưa có phê duyệt hình hoặc flow. Lượt này không vào Editor/Play Mode, không sửa scene/prefab/code/asset.

# Xác nhận mẫu và kiểm kê màn campaign — 31/10/2026

## Mẫu đã chọn
- [SUY LUẬN] Người dùng xác nhận “Ok đấy” cho hướng Manifest và Isolate hiển thị như màn hình thiết bị ở lượt trước. Giữ ba bản gốc Reports/mockups/04-device-manifest.png, Reports/mockups/04-device-locked.png, Reports/mockups/04-device-hold.png và bản xem Reports/mockups/04-device-screens-concept.html. Mẫu này duyệt hướng thị giác, không tự suy diễn thành duyệt thay vật tương tác, flow hoặc triển khai Unity.
- [SUY LUẬN] HUD đã được chọn riêng tại Reports/mockups/04-hud-selected.png, không thay đổi. Năm màn Files, Journal, Missions, Complete, Failed có mockup trước đó nhưng không có bằng chứng người dùng đã duyệt.
- [SUY LUẬN] Đã đọc C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md: phân biệt thiết kế được duyệt với cấu trúc và runtime; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md: liệt kê riêng các trạng thái màn và khả năng điều hướng; C:/Users/hieud/.agents/skills/unity-skills/skills/perception/SKILL.md: ưu tiên truy vấn scene read-only. Unity-Skills 8090–8100 không kết nối; MCP resources/templates không có Codebase Memory hoặc Unity, nên không xác minh scene hiện tại.
- [SUY LUẬN] create-game-assets/SKILL.md KHÔNG TÌM THẤY tại hai đường dẫn đã kiểm ở phần trước; không khẳng định đã đọc hoặc áp dụng.

## Các màn và trạng thái trong phạm vi campaign
1. **Tài liệu/Manifest tại mục tiêu.** [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:204] Mở objective; FactoryManifest có đường render riêng [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:244]. Có ảnh cũ Reports/CampaignScreens-2026-10-30/13-manifest.png và mockup hướng màn máy tính đã chọn; chưa xác nhận vật hiện tại có màn hình.
2. **Tài liệu được nhặt/Files.** [CODE Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:70] documentOnly yêu cầu discover; [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:109] OpenFile mở tab Files, [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:86] việc tự mở có điều kiện. Không suy ra từng file có một màn khác nhau.
3. **Màn nhiệm vụ/thiết bị thông thường.** [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:232] Tiêu đề, hướng dẫn, evidence nếu có hoặc SOURCE REQUIRED, nút giữ và trạng thái đã ghi. Có mockup Isolate khóa/giữ; chưa có ảnh màn cụ thể của toàn bộ objective.
4. **Màn chọn đối chiếu một hoặc hai nhóm.** [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:253] Hai dãy choices và secondaryChoices; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:267] FactoryShipping đặt nhãn theo hai nhóm. Chưa có mockup riêng hoặc ảnh runtime xác nhận cho từng cấu hình.
5. **Màn phân bổ điện LabPower.** [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:245] Bốn lựa chọn bật/tắt và thông báo khi không hợp lệ. Chưa có ảnh/mẫu riêng; không tự đưa đáp án vào bảng theo dõi.
6. **Hướng dẫn cửa/lối vào.** [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:223] OpenGate hiển thị tên cửa, chỉ dẫn và CLOSE GUIDE. Chưa có ảnh/mẫu riêng.
7. **Chọn vật tư.** [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:281] FIELD SUPPLY, lựa chọn đạn hoặc medkit. Chưa có ảnh/mẫu riêng; không thay logic hay nội dung.
8. **Team inventory / Key Items.** [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:14] Là một trong ba tab; [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:158] hiển thị key item từ catalog. Chưa có mockup nội dung tab riêng.
9. **Team inventory / Files.** [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:174] Có danh sách, file chưa đọc và chi tiết; ảnh Reports/UIUX/AssetReplacement/Gameplay/asset-files-1920x1080-100.png và mockup Reports/mockups/04-campaign-files.png; mockup chưa được duyệt.
10. **Team inventory / Missions.** [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:198] Danh sách objective và các trạng thái COMPLETE, TRACKING, AVAILABLE, LOCKED [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:211]. Có mockup Reports/mockups/04-campaign-missions.png, chưa được duyệt.
11. **Team Journal.** [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:287] Journal, đóng, resume khi cho phép và ready/cancel trong Preparing; code [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:368] cho thấy luồng mở phụ thuộc TeamInventoryUI. Có mẫu Reports/mockups/04-campaign-journal.png, chưa được duyệt.
12. **Preparation / Ready.** [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:467] Mã có nhánh PREPARATION với READY/NOT READY tại dòng 479; cần kiểm điều kiện thực tế vì CampaignStatusUI có thể xử lý trạng thái. Chưa có mockup panel riêng.
13. **Mission Complete / Mission Failed.** [CODE Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs:85] Debrief sau phase; retry chỉ hiện cho server khi thất bại [CODE Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs:90]. Có mẫu Reports/mockups/04-campaign-complete.png và Reports/mockups/04-campaign-failed.png, chưa được duyệt.
14. **Fallback failed/completed.** [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:470] Khi CampaignStatusUI hiện diện, nhánh failed/completed ở CampaignHUD được bỏ qua; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:480] có nhánh dự phòng riêng. Chưa chứng minh nhánh dự phòng xuất hiện trong scene hiện tại.
15. **Thông báo kết quả thao tác.** [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:316] ShowResult xử lý accepted/sai/khác; ShowMessage có thời gian 4 giây [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:326]. Chưa chụp đủ các trạng thái và chưa xác nhận âm thanh.
16. **Lời thoại và chuyển cảnh.** [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:329] Có hàng đợi dialogue; [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:54] định nghĩa dialogue theo chapter/sự kiện. Không tự ý redesign subtitle theo yêu cầu trước; cinematic chưa được chụp đầy đủ.

## Đối chiếu từng mục tiêu và tài liệu
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:9] Có 20 ID objective khác None: Factory 7 (FactoryIsolate, FactoryBackup, FactoryGenerator, FactoryManifest, FactoryShipping, FactoryCase, FactoryRoute); Asylum 7 (AsylumAccess, AsylumFuse, AsylumInstall, AsylumPower, AsylumPatient, AsylumTransfer, AsylumLift); Laboratory 6 (LabTrace, LabIndex, LabPower, LabArchive, LabCase, LabTransmit). Đây là ID trong code, không phải bằng chứng 20 layout hay 20 ảnh.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:19] Có 21 ID file khác None: FactoryProcedure, FactoryManifest, FactoryB2Transfer, AsylumDeskLog, AsylumPatientRecord, AsylumMortuaryTransfer, LabIndex, LabPowerRecord, LabOriginalArchive, OptionalFactory01–03, OptionalAsylum01–04, OptionalLab01–05. Đây là danh sách enum, chưa xác minh catalog/scene hiện tại chứa và mở hết từng mục.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:45] Có bốn ID key item khác None: FactoryEvidenceCase, ServiceFuse, B2AccessCard, LabEvidenceCase; chưa chụp mỗi trạng thái.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignContentCatalog.cs:84] Mỗi objective có choices, secondaryChoices và file; chưa đọc được catalog instance hiện tại qua Unity nên không khẳng định ID nào dùng nhóm lựa chọn nào ngoài các nhánh code xác định.

## Đề xuất
- [SUY LUẬN] Ưu tiên thu ảnh và mockup còn thiếu: FactoryShipping hai nhóm, LabPower, cửa, vật tư, Key Items, Preparation; sau đó kiểm các objective/file từng chapter. Không triển khai Unity, không sửa vật/flow trước khi duyệt riêng.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Bảy màn đã vẽ là bảy mẫu từ bộ ảnh cũ, không phải toàn bộ giao diện campaign; danh sách trên bổ sung các loại xác định bằng code, không xem enum là số layout. Xác nhận hướng hai màn thiết bị không đồng nghĩa duyệt năm mockup còn lại hoặc vật thể trong scene.

## Chưa kiểm được
- [SUY LUẬN] Unity-Skills không kết nối, KHÔNG TÌM THẤY isDirty hiện tại, catalog instance và trạng thái runtime; không vào Editor/Play Mode. Chưa chụp toàn bộ các biến thể Factory, Asylum, Laboratory, chưa kiểm layout của từng file, opacity, flow, gamepad, âm thanh và 4K. Không thể tuyên bố kiểm kê mọi màn Unity một cách đầy đủ khi scene và runtime chưa truy vấn được.

# Yêu cầu triển khai chín mẫu — 31/10/2026

- [SUY LUẬN] Người dùng đã xem HUD và tám ảnh campaign rồi yêu cầu triển khai cả chín. Ba ảnh thiết bị gồm Manifest, Isolate khóa và Isolate giữ là ba trạng thái hình, không phải ba màn độc lập.
- [SCENE MainMenu.isDirty] Unity-Skills kết nối tại cổng 8090; scene đang mở MainMenu có isDirty=false, không ở Play Mode. Không lưu, không mở GameScene, không sửa code/scene/asset và không import trong lượt này.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:137] CampaignHUD tạo UGUI khi chạy; [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:239] TeamInventoryUI cũng tạo UGUI khi chạy. Các tệp này, HUDManager.cs và CampaignStatusUI.cs đã có thay đổi Git từ trước; cần bảo toàn khi triển khai.
- [SUY LUẬN] Unity AssetDatabase tìm thấy model Assets/FPS/Features/World/Content/AsylumFacility/Models/PC_Monitor.fbx và sprite Panel; chưa kiểm hình model hoặc chứng minh dùng được cho hai vật tương tác Factory. Vỏ màn trong Reports/mockups/04-device-screens-concept.html là CSS greybox, không phải asset được phép import. Việc duyệt mẫu không tự cho phép đổi object hoặc flow.
- [SUY LUẬN] Đã đọc C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md và references/execution-contract.md, references/agent-runbook.md: kiểm Unity state, asset, bố cục và ảnh sau triển khai; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md: neo bố cục và kiểm khả năng đọc; C:/Users/hieud/.agents/skills/unity-skills/SKILL.md và skills/perception/SKILL.md: truy vấn read-only qua Unity-Skills trước khi sửa.

## Đề xuất
- [SUY LUẬN] Cần xác định cách thể hiện khung thiết bị mà không tự ý đổi vật tương tác: duyệt riêng việc dùng model sẵn có nếu sau khi kiểm thấy phù hợp, hoặc duyệt giải pháp khác. Những màn khác không phụ thuộc quyết định thay object này.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Mục trước ghi Unity không kết nối là trạng thái lần trước; lần này đã kết nối. Scene MainMenu sạch không chứng minh GameScene sạch trong Editor hay dự án không có thay đổi Git.

## Chưa kiểm được
- [SUY LUẬN] Chưa triển khai giao diện nào, chưa kiểm thực tế GameScene hoặc ảnh trước/sau, playtest, 1080p/4K và asset thiết bị phù hợp. Không coi việc duyệt mockup là nghiệm thu implementation.

# Yêu cầu asset màn hình — 01/11/2026
- [SUY LUẬN] Người dùng muốn màn hình máy tính là asset hoàn chỉnh, không phải vỏ CSS hoặc vật dựng tạm; chưa đồng ý import, imagegen hoặc thay object tương tác cụ thể.
- [SCENE AssetDatabase/Assets/FPS/Features/World/Content/AsylumFacility/Models/PC_Monitor.fbx] Unity-Skills báo model PC_Monitor có một mesh, 389 đỉnh, 298 tam giác và một material; có prefab Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/PC_Monitor.prefab. Chưa xem preview, chưa kiểm mặt hiển thị, chất lượng, bản quyền hoặc độ hợp cảnh Factory; không khẳng định asset này đạt yêu cầu.
- [SUY LUẬN] Đã đọc C:/Users/hieud/.agents/skills/unity-skills/SKILL.md và unity-mcp-ui-layout/SKILL.md ở lượt trước: ưu tiên kiểm tra asset đang có, phân biệt dữ liệu model với bằng chứng hình ảnh. Tài liệu unity-skills/skills/model/SKILL.md KHÔNG TÌM THẤY; không khẳng định đã áp dụng skill đó.
- [SCENE MainMenu.isDirty] MainMenu đang mở và isDirty=false; không sửa scene/code/asset, không import và không vào Play Mode trong lượt này.

## Đề xuất
- [SUY LUẬN] Xem trực quan, kiểm khả năng đặt nội dung lên màn hình và nguồn/license của model hiện có trước khi đề xuất; nếu không đạt thì đề xuất asset khác để người dùng duyệt, không tự import hay thay object.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Có file FBX và prefab không chứng minh asset phù hợp hoặc đã có quyền thay object. Chấp thuận thiết kế hình ảnh không đồng nghĩa chấp thuận vỏ greybox làm asset cuối.

## Chưa kiểm được
- [SUY LUẬN] Chưa xem preview hoặc kiểm license model, chưa chọn asset cuối, chưa triển khai chín mẫu, chưa có ảnh Game View hoặc playtest.

# Khảo sát asset thiết bị — 01/11/2026
- [SUY LUẬN] Người dùng cho phép tìm và tải asset từ Internet để xem; không đồng nghĩa cho phép import vào Unity hoặc thay object tương tác. Không dùng imagegen.
- [SUY LUẬN] Industrial Terminal, Vaportrash, CC BY 4.0, 12.638 tam giác; tác giả mô tả màn hình có texture và emission riêng. Ảnh trang nguồn cho thấy tủ đứng với đèn xanh, phong cách retro-futuristic; có nguy cơ quá sci-fi so với Factory. Nguồn: https://sketchfab.com/3d-models/industrial-terminal-846bb4773f74406bb8a24fcf2f17cf22 . Chưa tải, chưa xác minh mesh hoặc cách đặt UI thực tế.
- [SUY LUẬN] Industrial Control Panel Box, uliana, CC BY 4.0, 42.2k tam giác theo trang tác giả; ảnh cho thấy tủ kim loại xước, màn nhỏ, nút cơ và dây. Hợp hướng công nghiệp hơn nhưng màn nhỏ và số tam giác cần kiểm theo cảnh; chưa kiểm mesh màn tách riêng. Nguồn: https://sketchfab.com/3d-models/industrial-control-panel-box-ccea58b4f827480787aa81d7490f93e2 . Chưa tải.
- [SUY LUẬN] Abandoned Computer Terminal, olegolegperepelica, CC BY 4.0, 10.4k tam giác theo trang tác giả; ảnh cho thấy máy CRT cũ và bàn phím. Mặt kính gợi cảm giác nhìn vào màn hình nhưng tỉ lệ 4:3 khác mockup rộng, bàn phím có layout Ukraine theo tác giả. Nguồn: https://sketchfab.com/3d-models/abandoned-computer-terminal-game-prop-1c56df74822a45b682899b0bc7dc2355 . Chưa tải.
- [SUY LUẬN] Trang Sketchfab yêu cầu đăng nhập khi bấm Download; API download trả HTTP 401. Không vượt cơ chế đăng nhập và không khẳng định đã tải model. CC BY cần ghi công tác giả, nguồn và giấy phép khi sử dụng.
- [SUY LUẬN] Đã đọc skill C:/Users/hieud/.codex/plugins/cache/openai-bundled/computer-use/26.1027.30824/skills/computer-use/SKILL.md: quan sát trang và kiểm hành vi nút tải; unity-mcp-ui-layout và game-ui-ux đã đọc trước đó: so sánh asset với thiết kế và công dụng màn hình. Không khẳng định create-game-assets đã đọc vì KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Ưu tiên kiểm Industrial Control Panel Box cho thiết bị điều khiển và tìm màn hình đủ rộng cho Manifest; không tự thay object hoặc triển khai flow. Tiếp tục tìm nguồn tải hợp lệ không yêu cầu đăng nhập.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Trang mô tả asset miễn phí không đồng nghĩa đã tải được; ảnh hiển thị trên web không chứng minh model đã import hoặc chạy trong Unity.

## Chưa kiểm được
- [SUY LUẬN] Chưa tải được các model Sketchfab, chưa kiểm UV/material/giấy phép của file tải thực tế và chưa triển khai chín giao diện. Chưa tạo ảnh Game View sau sửa hoặc playtest; Unity chưa bị sửa trong lượt này.

# Đính chính phạm vi — 01/11/2026
- [SUY LUẬN] Người dùng xác nhận yêu cầu là giao diện UI khi tương tác, không phải tìm hay thay object máy tính. Những đề xuất model 3D trước đó không liên quan và không phải điều kiện để làm UI.
- [SCENE GameScene/Canvas] HUD dùng Canvas trong scene; [SCENE GameScene/CampaignSystems] CampaignHUD, TeamInventoryUI và CampaignStatusUI thuộc CampaignSystems. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:137] CampaignHUD tạo Canvas screen-space khi chạy. Tập trung giao diện, giữ nguyên object và flow.
- [SCENE GameScene.isDirty] Đã dùng Unity-Skills: chạy menu Tools/AI Director/Save Authored Assets trước và sau khi mở GameScene; menu gọi AssetDatabase.SaveAssets(). GameScene báo isDirty=false. Không vào Play Mode, không sửa code/scene/asset và không import model.
- [SUY LUẬN] Skill đã đọc ở lượt trước: unity-mcp-ui-layout/SKILL.md dùng để phân biệt UI runtime với object; game-ui-ux/SKILL.md dùng định hướng neo và phân tầng; unity-skills/SKILL.md cùng skills/perception/SKILL.md dùng truy vấn cấu trúc scene. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Áp dụng các mockup đã chọn trực tiếp lên UI hiện có, không thay object hay nội dung nhiệm vụ; kiểm 1080p, 4K và các trạng thái sau khi triển khai.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Việc đã mở GameScene khác với kết luận cũ rằng chưa mở; việc chạy SaveAssets cũng khác với lượt trước không lưu. Không xem việc quan sát cấu trúc là triển khai.

## Chưa kiểm được
- [SUY LUẬN] Chưa triển khai chín mẫu, chưa chụp ảnh Game View trước/sau trong lượt này và chưa playtest; các thay đổi sẵn có trong tệp UI phải được bảo toàn.

# Triển khai UI — cập nhật 01/11/2026
- [SUY LUẬN] Phạm vi là UI 2D đã duyệt; không tìm, thay hoặc sửa object 3D. Không dùng imagegen, không tải hay import asset mới.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:223] Tracker nguồn định dạng hai nhánh POWER và LOGISTICS, chỉ đánh dấu ▶ nhánh đang theo dõi; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:324] waypoint nguồn ghép tên và khoảng cách, chỉ ghi TANK khi vị trí nhận được thực sự là Tank; fallback dùng tên mục tiêu hoặc PREPARATION AREA.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:20] Test ba trường hợp nhánh: job d4878716 chạy trước khi Unity nhận nguồn mới nên timeout và TestResults.xml ghi total=0; sau tự biên dịch, job 3b3d67b7 chạy 1 test, đạt 1, lỗi 0.
- [SUY LUẬN] Lúc đầu assembly cũ hơn mã nguồn; sau khi sửa mã Unity tự reload, FPS.dll và FPS.UI.Tests.Editor.dll được cập nhật lúc 00:31 ngày 01/11/2026. Console sau reload báo 0 lỗi. Không gọi Refresh thủ công, không thêm asset ngoài; không khẳng định Unity không tự import lại script.
- [CODE Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs:101] Debrief nguồn tách tiêu đề, bốn chỉ số và DISCOVERY; giữ Summary và hành vi retry hiện có. Chưa chụp hoặc xác nhận hình thực tế, khung vẫn dùng Panel cũ.
- [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:248] Inventory vẫn dùng InventoryBoard; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:161] document vẫn dùng JournalAndDocument. Chưa triển khai đầy đủ toàn bộ mockup đã duyệt.
- [SCENE GameScene.isDirty] Unity-Skills báo GameScene đang mở, isDirty=false sau lần chạy test; đã dùng menu Save Authored Assets trước và sau Test Runner; không vào Play Mode.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md và references/execution-contract.md, references/agent-runbook.md — phân biệt UGUI runtime, kiểm ảnh và không gọi hoàn tất khi thiếu bằng chứng; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — xét anchor và khả năng đọc; C:/Users/hieud/.agents/skills/unity-skills/SKILL.md cùng skills/editor/SKILL.md, skills/test/SKILL.md, skills/debug/SKILL.md — kiểm scene, save gate và báo kết quả test thực tế; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — giới hạn thay đổi và nêu phần chưa kiểm. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Tiếp tục áp dụng các mockup còn lại bằng asset UI có sẵn; khung Resources/HudSkin/Panel.png hiện có viền sáng dày, không coi là đáp ứng mẫu đã duyệt.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Phần trước ghi chưa sửa code chỉ đúng tại thời điểm trước đó; cập nhật này đã sửa nguồn navigation. Mẫu được duyệt không đồng nghĩa với giao diện đã được triển khai hoặc kiểm trực quan.

## Chưa kiểm được
- [SUY LUẬN] Chưa xác nhận glyph ▶ hiển thị trong font; chưa test trực quan hoặc playtest debrief; chưa chụp 1080p/4K trước và sau, chưa triển khai đầy đủ chín giao diện, chưa xác nhận opacity. Đây là tình trạng ở thời điểm ghi mục này, xem cập nhật sau.

# Tiếp tục triển khai và kiểm HUD — 01/11/2026

- [PLAYTEST Reports/UIUX/MockupImplementation/After/Exploring-1920x1080-20261101-025715-129.png] Trước sửa waypoint, chữ mục tiêu bị xuống dòng, phần mũi tên và chữ nằm chồng lên vùng súng bên phải. Ảnh này đã có các thay đổi tracker và máu của lượt trước, không phải baseline trước toàn bộ công việc.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:117] Tăng độ rộng marker 228 lên 360 đơn vị UI; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:131] bật co chữ và không tự xuống dòng; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:313] mở rộng vùng tránh phía dưới bên phải từ 422 lên 650 đơn vị UI.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/Exploring-1920x1080-20261101-031724-357.png] Trong góc nhìn thử, waypoint nằm phía trên súng và hiện một dòng “READ DELIVERY MANIFEST 271 m”; nhánh LOGISTICS được đánh dấu ▶. Đây là một góc nhìn, không chứng minh mọi hướng camera hoặc tên dài đều đúng.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/Exploring-3840x2160-20261101-031733-407.png] Ảnh Game View 4K sau sửa cùng góc nhìn cũng cho thấy waypoint nằm trên súng và tên cùng khoảng cách không xuống dòng.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/inventory-1920x1080-20261101-025904-653.png] Missions vẫn có bảng rỉ sét, viền dày và chữ Teko cô đặc; không giống mẫu Reports/mockups/04-campaign-missions.png. Ảnh là trạng thái thực tế, không phải kết quả triển khai xong.
- [CODE Assets/FPS/Features/UI/Runtime/HUDManager.cs:269] Thay đổi từ lượt trước đặt phần nhiễm trên thanh máu; ảnh sau chỉ cho thấy INFECTION 0%, không chứng minh phần tô đè khi nhiễm dương. [CODE Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs:139] Debrief mới tách bốn chỉ số, nhưng chưa có ảnh kiểm trạng thái thành công/thất bại.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:228] EditMode ProjectionHandlesBehindCameraAndAvoidsOccupiedCorner, job fc262ef7: 1/1 đạt; đây kiểm phép chiếu và tránh vùng HUD tổng quát, không kiểm toàn bộ cấu hình marker mới. Test FactoryTrackerMarksOnlyTheSelectedBranch job 3b3d67b7 và InfectionOverlayUsesHealthBarWithoutHidingSepsisWarning job ae12d2dd của lượt trước đều 1/1 đạt. PlayMode ArtworkScrollAndEventBindingsSurviveStress job bbececfc lỗi Test Runner trước khi chạy, không tính là đạt.
- [SCENE MainMenu.isDirty] Đã dùng Unity-Skills vào Play Mode, mở local host và GameScene để chụp, thoát Play Mode, chạy EditMode test và Save Authored Assets; kết thúc MainMenu isDirty=false, Console 0 lỗi. Hai file checkpoint có hash không đổi so với bản sao trước lượt thử.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md và references/execution-contract.md, references/agent-runbook.md — dùng UGUI, đối chiếu ảnh runtime và không coi ảnh chụp là duyệt hình; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — kiểm vị trí, khả năng đọc và hai độ phân giải; C:/Users/hieud/.agents/skills/unity-skills/SKILL.md và skills/editor/SKILL.md, skills/test/SKILL.md, skills/debug/SKILL.md — kiểm trạng thái Editor, chạy test và đọc console; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — sửa phạm vi hẹp và nêu kiểm chứng còn thiếu. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Tiếp tục dựng tám giao diện campaign và hoàn thiện HUD theo mockup; không thay object 3D. Cần xử lý asset khung UI trước khi có thể làm giống mẫu mà không vẽ panel bằng primitive hoặc import asset mới.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Ảnh 1080p/4K mới là ảnh sau một phần thay đổi, không phải ảnh trước/sau của toàn bộ chín giao diện. ▶ đã nhìn thấy ở ảnh runtime này, nhưng không chứng minh glyph trong mọi font hoặc độ phân giải. 1 test EditMode đạt không thay thế playtest nhiệm vụ.

## Chưa kiểm được
- [SUY LUẬN] Chưa hoàn thành chín giao diện; chưa có ảnh sau cho thiết bị, Files, Journal, Complete và Failed; Missions còn khác mockup. Chưa có before 1080p/4K của toàn bộ thay đổi, chưa kiểm opacity, nhiễm >0, tên waypoint rất dài, mọi góc camera, các trạng thái khóa/giữ/hoàn tất, chơi host/client hoặc playtest hoàn chỉnh. Người dùng chưa duyệt hình implementation.

# Triển khai Files và Missions — 01/11/2026

- [SCENE GameScene/CampaignSystems/TeamInventoryUI.campaignPanel] Đã gắn sprite Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png vào component TeamInventoryUI trong GameScene; không thay object thiết bị hoặc import asset. Sprite này là khung đen viền mảnh có sẵn.
- [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:252] Shell của inventory dùng khung trên; bố cục có tiêu đề, ba tab, danh sách cuộn bên trái, nội dung cuộn bên phải và footer. [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:361] Các đường chia dùng sprite HudSkin/Fill có sẵn; tab hiện tại có gạch dưới. [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:403] Chữ inventory dùng LiberationSans SDF từ Assets/TextMesh Pro/Resources/Fonts & Materials; không thay font HUD chung.
- [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:157] Mục được focus ban đầu theo mục đang hiển thị; [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:376] hàng chọn có nền xám và trạng thái focus, còn hàng không chọn trong suốt. Các callback chọn mission, file và UiStateStack được giữ lại.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/inventory-1920x1080-20261101-051417-361.png] Game View 1080p sau sửa: Missions có khung đen, danh sách trái, chi tiết phải; mục READ DELIVERY MANIFEST được chọn. So với mẫu, tiêu đề mục còn chữ in hoa, hàng chọn còn vân, thiếu một số đường kẻ/mô tả và nút close chưa giống; không tuyên bố y hệt.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/inventory-1920x1080-20261101-051424-398.png] Tab Files được mở nhưng trạng thái game thử chưa thu thập file, hiện “NO FILES RECORDED”; không chứng minh trạng thái có file khớp mẫu. [PLAYTEST Reports/UIUX/MockupImplementation/After/inventory-3840x2160-20261101-051431-450.png] Có ảnh Files rỗng 4K; chưa chụp Missions 4K sau lần chỉnh cuối.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:20] Test InventoryRowKeepsSelectionAndKeyboardFocusVisible job aa7efaf7 chạy sau biên dịch, 1/1 đạt; chỉ kiểm màu trạng thái và sprite, không chứng minh thao tác chuột/gamepad hay đọc file. Sau Play Mode, [SCENE MainMenu.isDirty] MainMenu isDirty=false; Unity console 0 lỗi; hai checkpoint có hash không đổi so với bản sao trước khi test.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md và references/execution-contract.md, references/agent-runbook.md — sửa UGUI theo khung và bố cục mockup, kiểm ảnh và báo chỗ lệch; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — giữ anchor, vùng an toàn, scroll và focus; C:/Users/hieud/.agents/skills/unity-skills/SKILL.md cùng skills/asset/SKILL.md, skills/scene/SKILL.md, skills/component/SKILL.md — truy tìm font/sprite, gắn sprite hiện có và kiểm scene; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — giữ phạm vi UI và thêm test. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Tiếp tục kiểm Files có dữ liệu và hoàn thiện Journal, debrief, màn thiết bị cùng HUD theo các mockup; không thay object 3D.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Mục trước nói chưa có ảnh Files và Missions chưa sửa đúng tại thời điểm trước; mục này có ảnh Files rỗng và Missions sau sửa. Những ảnh đó không chứng minh toàn bộ trạng thái hoặc chín giao diện đã hoàn thành.

## Chưa kiểm được
- [SUY LUẬN] Files có tài liệu, lựa chọn dài, cuộn và gamepad chưa playtest; ảnh sau cuối chưa có Missions 4K hoặc tỷ lệ màn khác. Journal, Complete, Failed, các trạng thái thiết bị và HUD chưa hoàn chỉnh theo mẫu. Chưa có ảnh trước/sau 1080p và 4K đầy đủ cho từng màn; không có phê duyệt hình triển khai của người dùng.

# Debrief: ảnh kiểm fixture — 01/11/2026

- [CODE Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs:109] Complete và Failed dùng cùng khung, hiển thị bốn chỉ số và ba dòng Discovery lấy từ CampaignRunSummary; Failed có nút Retry theo điều kiện server và phase. [CODE Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs:157] Khung dùng lại sprite campaignPanel hiện có; font nội dung dùng LiberationSans SDF, số dùng Teko Bold.
- [CODE Assets/FPS/Features/UI/Editor/HudReviewTools.cs:133] Ảnh sau là fixture chỉ chạy trong Play Mode: đặt dữ liệu giả, không chuyển phase hoặc kiểm nhiệm vụ kết thúc thật. Tên ảnh chứa Exploring vì phase thật vẫn Exploring; không gọi đây là playtest flow kết thúc.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/Exploring-1920x1080-20261101-080458-002.png] Fixture Complete 1080p. [PLAYTEST Reports/UIUX/MockupImplementation/After/Exploring-1920x1080-20261101-080503-059.png] Fixture Failed 1080p: chữ [ENTER] thay glyph bị ô vuông; nền nút không còn mảng xám sáng ở trạng thái fixture.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/Exploring-3840x2160-20261101-080509-117.png] Fixture Failed 4K. [PLAYTEST Reports/UIUX/MockupImplementation/After/Exploring-3840x2160-20261101-080514-148.png] Fixture Complete 4K. [PLAYTEST Reports/UIUX/MockupImplementation/After/Exploring-1024x768-20261101-080520-189.png] Fixture Complete 4:3: bốn chỉ số và ba dòng vẫn trong khung.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/Exploring-1920x1080-20261101-080503-059.png] So với Reports/mockups/04-campaign-failed.png, ba dòng Discovery vẫn sát hơn, nhãn ENTER không phải ô phím màu cam như mẫu, và nền chưa tối/nhòe như mẫu; tracker, súng và HUD còn hiện trong fixture vì fixture không mở UiStateStack. Chưa suy ra hiện tượng này xảy ra hoặc không xảy ra khi kết thúc thật.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:20] EditMode DebriefShowsSummaryValuesAndResultState, job 116f0055: 1/1 đạt; kiểm text, giá trị và màu trạng thái, không kiểm nút Retry hoặc nhập phím. [CODE Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs:94] Gọi RefreshFocus sau khi đặt trạng thái nút; chưa kiểm focus và Enter thực tế.
- [SCENE MainMenu.isDirty] Đã dùng Unity-Skills vào Play Mode, chạy local host, chụp Game View fixture, thoát Play Mode và gọi Save Authored Assets; MainMenu cuối cùng isDirty=false. Hai tệp checkpoint có hash khớp bản sao trước kiểm thử.
- [SUY LUẬN] Console sau khi thoát Play Mode ghi 1 IOException sharing violation tại Library/UnitySkills/batch_state.json, từ BatchPersistence.cs:83; chưa xác định nguyên nhân hay tác động, không khẳng định console sạch. Console trước Play Mode ghi 0 lỗi.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md cùng references/execution-contract.md và references/agent-runbook.md — đối chiếu ảnh mockup với UGUI ở nhiều tỷ lệ, phân biệt ảnh fixture với phê duyệt; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — chú ý vùng an toàn và focus; C:/Users/hieud/.agents/skills/unity-skills/SKILL.md cùng skills/editor/SKILL.md và skills/test/SKILL.md — kiểm Editor, save gate và ghi kết quả test thật; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — giữ sửa đổi trong UI và nêu kiểm chứng còn thiếu. create-game-assets/SKILL.md KHÔNG TÌM THẤY, không khẳng định đã áp dụng.

## Đề xuất
- [SUY LUẬN] Tiếp tục hoàn thiện debrief so với mẫu và kiểm flow thật; tiếp tục HUD, thiết bị, Files có dữ liệu và Journal, không thay object 3D.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Mục trước nói chưa có ảnh debrief là tình trạng trước lần này; hiện chỉ có ảnh fixture, không có nghĩa đã playtest Complete/Failed thực tế hoặc đã hoàn tất chín giao diện.

## Chưa kiểm được
- [SUY LUẬN] Chưa kiểm Retry bằng chuột, Enter, gamepad hoặc host/client; chưa xác nhận HUD ẩn khi kết thúc thực. Chưa có ảnh before 1080p và 4K đầy đủ cho từng màn, chưa hoàn thành chín giao diện hoặc xác nhận opacity. Chưa xử lý hay xác định tác động lỗi UnitySkills trong Console.

# Điều chỉnh danh sách Missions — 01/11/2026
- [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:339] Thêm đường phân cách từng hàng và vạch báo hàng được chọn; [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:394] nền highlight không dùng sprite vân cũ. [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:104] Nhãn Close góc trên và footer lấy phím Journal/Pause từ binding hiện tại.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/inventory-1920x1080-20261101-090418-323.png] Ảnh Game View 1080p: hàng READ DELIVERY MANIFEST không còn vân, có các đường ngăn, góc trên hiện CLOSE [I], footer hiện CLOSE I / Esc. So với Reports/mockups/04-campaign-missions.png, nền hàng đang chọn vẫn sáng hơn, tiêu đề mục tiêu vẫn chữ in hoa, nhãn góc trên chưa có ô phím riêng và chi tiết chưa đúng hoàn toàn.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/inventory-3840x2160-20261101-090424-359.png] Ảnh Missions 4K sau sửa; [PLAYTEST Reports/UIUX/MockupImplementation/After/inventory-1024x768-20261101-090430-405.png] ảnh 4:3 sau sửa. Không dùng ảnh Files rỗng ở lần chụp trước làm bằng chứng Files có nội dung.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:49] EditMode InventoryRowKeepsSelectionAndKeyboardFocusVisible job ddedcbbf: 1/1 đạt; kiểm trạng thái màu, sprite và vạch chọn, không thay thế thao tác bàn phím hoặc chuột.
- [SCENE MainMenu.isDirty] Unity-Skills đã vào Play Mode, tạo local host, chụp Game View, thoát Play Mode và lưu theo Save Gate; MainMenu isDirty=false, Console 0 lỗi. Hai checkpoint có hash khớp bản sao trước kiểm tra.
- [SCENE AssetDatabase] Tra tìm sprite theo tên terminal, monitor và screen không có kết quả; các texture monitor tìm được là texture cho model 3D, không chứng minh có asset khung UI màn hình phù hợp. Không thay object, import asset, hay gọi imagegen; chưa dựng màn thiết bị theo mockup.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md cùng references/execution-contract.md và references/agent-runbook.md — so mockup với UGUI và ảnh ở nhiều tỷ lệ; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — kiểm trạng thái chọn và phím đóng; C:/Users/hieud/.agents/skills/unity-skills/SKILL.md cùng skills/asset/SKILL.md, skills/editor/SKILL.md và skills/test/SKILL.md — tìm asset, kiểm Editor và chạy test; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — giữ thay đổi UI có kiểm thử. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Tiếp tục làm màn thiết bị, Journal, HUD và các trạng thái Files; cần asset khung UI thiết bị phù hợp trước khi tuyên bố giống mẫu, không dùng model 3D thay thế giao diện.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Console 0 lỗi là kết quả cuối lượt này, không phủ nhận lỗi UnitySkills được ghi tại lượt trước. Nền highlight đã hết vân nhưng vẫn chưa đúng độ sáng mockup; hai nhận xét không mâu thuẫn.

## Chưa kiểm được
- [SUY LUẬN] Chưa playtest chọn dòng bằng chuột, phím hoặc gamepad, chưa xác minh Files có dữ liệu, các màn thiết bị và Journal chưa triển khai đúng mẫu; chưa đủ ảnh trước/sau 1080p và 4K cho cả chín giao diện. Chưa xác nhận opacity và chưa được duyệt hình triển khai.

# Bố cục thiết bị tạm — 01/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:187] Thêm vùng thiết bị cho FactoryManifest, FactoryIsolate, FactoryBackup và FactoryGenerator: tên thiết bị phía trên, nội dung dữ liệu hiện có bên trái và trạng thái bên phải; khi đóng khôi phục layout tài liệu cũ. Khung dùng lại sprite Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png: **GREYBOX PLACEHOLDER**, không phải asset màn hình thiết bị đạt mẫu.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:318] Khi CampaignRules trả Prerequisite, nút đổi thành LOCKED, không ghi F, có biểu tượng khóa và lý do từ CampaignRules.LockReason; điều kiện giữ phím phía client không gửi yêu cầu khi cùng trạng thái khóa. Không thay CampaignRules hoặc nội dung nhiệm vụ.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1920x1080-20261101-095610-907.png] Ảnh fixture Backup 1080p hiển thị biểu tượng khóa, "Requires: complete Power / Isolate" và "LOCKED", không thấy gợi ý F. Ảnh được mở trực tiếp bằng menu thử, không chứng minh người chơi đã đi tới và tương tác vật thể. [PLAYTEST Reports/UIUX/MockupImplementation/After/document-3840x2160-20261101-095616-954.png] Ảnh Backup 4K; [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1024x768-20261101-095622-994.png] ảnh Backup 4:3.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1920x1080-20261101-093926-348.png] Fixture Isolate trước chỉnh cuối hiển thị "SOURCE REQUIRED" và "READY" cùng lúc. [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:163] CanUse hiện không chặn FactoryIsolate theo file nguồn; nhãn mới "ACTION READY" mô tả điều kiện hành động, không xác nhận đã đọc nguồn. Đây vẫn là sự không rõ ràng cần kiểm flow riêng, không tự thay luật để giả khớp mẫu.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1920x1080-20261101-095610-907.png] So với Reports/mockups/04-device-locked.png, placeholder thiếu khung màn hình công nghiệp, các khối thông tin và bố cục/nút đẹp như mẫu; nền thế giới chưa tối theo mẫu, Close và nút dùng sprite/font cũ. Không tuyên bố giao diện hoàn thành hay giống mẫu.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:20] EditMode DeviceLayoutRestoresDocumentLayoutWhenClosed job a2437254: 1/1 đạt, chỉ kiểm bật/tắt surface và khôi phục anchors, không kiểm giữ phím hay server. [SCENE MainMenu.isDirty] Unity-Skills vào Play Mode với local host, mở hai fixture, chụp Game View, thoát và lưu theo Save Gate; MainMenu isDirty=false, Console 0 lỗi, hash hai checkpoint khớp bản sao.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md cùng references/execution-contract.md, references/agent-runbook.md — phân biệt placeholder, so ảnh với mẫu và giữ layout cũ khi đóng; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — nội dung khóa có lý do, kiểm thêm tỷ lệ; C:/Users/hieud/.agents/skills/unity-skills/SKILL.md cùng skills/editor/SKILL.md và skills/test/SKILL.md — vận hành Editor và kiểm test thật; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — không thay logic nhiệm vụ. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Thay GREYBOX PLACEHOLDER bằng asset UI màn hình được cấp phép phù hợp mockup, hoàn thiện hierarchy, nút và trạng thái giữ; không thay object 3D hoặc nội dung cốt truyện.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Lần trước ghi chưa dựng màn thiết bị; lần này chỉ dựng bố cục tạm, không đồng nghĩa có asset chuẩn hoặc đã hoàn thành. Ảnh fixture khác playtest tương tác thật.

## Chưa kiểm được
- [SUY LUẬN] Chưa kiểm giữ F, thả sớm, hoàn tất, thao tác chuột/gamepad và client/server; chưa chụp Manifest và Generator, chưa có ảnh trước/sau đầy đủ, chưa giải quyết khác biệt điều kiện nguồn của Isolate. Các giao diện khác và opacity chưa hoàn tất; không có phê duyệt hình triển khai.

# Kiểm tra trạng thái thiết bị — 01/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:292] Dữ liệu Manifest chỉ hiển thị nội dung file khi CampaignState đã ghi nhận file; surface được cập nhật khi file, objective, chapter hoặc phase đổi và không dựng lại trong lúc giữ phím. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:338] Nhãn trạng thái theo kết quả CampaignRules.CanUse; không sửa CampaignRules.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1920x1080-20261101-103849-719.png] Trước sửa, Isolate hiển thị cùng lúc SOURCE REQUIRED và ACTION READY. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:297] Chuyển thông báo thành SOURCE NOT RECORDED và hướng dẫn đọc nguồn trước khi so sánh; không chặn hay mở thêm hành động. [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1920x1080-20261101-104646-873.png] Ảnh sau hiển thị SOURCE NOT RECORDED ở trái, ACTION READY và HOLD F TO CONFIRM ở phải.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/document-3840x2160-20261101-104652-914.png] Ảnh 4K sau sửa. [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1024x768-20261101-104658-949.png] Ảnh 4:3 sau sửa. Đây là fixture mở thiết bị từ xa bằng menu thử trong Play Mode/local host, không phải playtest đi đến thiết bị hoặc giữ phím.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:20] DeviceStatusUsesCampaignEligibility: job 8628e66b đạt 1/1. [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:39] ManifestDeviceShowsFileOnlyAfterItIsRecorded: job 3c4c3a39 đạt 1/1 sau sửa nhãn; kiểm nội dung trước/sau ghi nhận bằng fixture EditMode, không chứng minh hành vi mạng.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Đã chạy Unity-Skills vào và thoát Play Mode, save gate trước/sau, chạy EditMode test; cuối cùng MainMenu isDirty=false, isPlaying=false và Console báo 0 lỗi. [SUY LUẬN] Hash hai checkpoint khớp bản sao trước thử.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md, references/execution-contract.md và references/agent-runbook.md — đối chiếu mockup và ảnh fixture, phân biệt trạng thái chưa hoàn thành; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — kiểm tra hiển thị ở thêm tỷ lệ; C:/Users/hieud/.agents/skills/unity-skills/SKILL.md, skills/editor/SKILL.md và skills/test/SKILL.md — kiểm Editor và kết quả test thực; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — chỉ sửa nhãn trong UI, không thay luật nhiệm vụ. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Tiếp tục dựng chín giao diện theo mẫu với asset UI hiện có hoặc GREYBOX PLACEHOLDER được ghi rõ; không thay vật thể 3D hay luật nhiệm vụ.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Nhãn mới chỉ giải thích trạng thái file, không biến file thành điều kiện bắt buộc của Isolate. Ảnh fixture xác nhận chữ xuất hiện, không xác nhận flow thực tế hoặc chất lượng giống mockup.

## Chưa kiểm được
- [SUY LUẬN] Chưa hoàn thành chín giao diện; màn thiết bị còn thiếu khung và bố cục như mẫu. Chưa playtest giữ/thả F, hoàn tất, host/client, opacity, Manifest và Generator; chưa đủ ảnh trước/sau 1080p và 4K cho từng giao diện, chưa được người dùng duyệt bản triển khai.

# Bố cục hai vùng thiết bị — 01/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:197] DeviceSurface gồm tên và thông tin truy cập phía trên, vùng INSTRUCTIONS / REFERENCE ở trái, CONTROL / STATUS ở phải, và footer; hai vùng dùng lại sprite panel của campaign, không import hay thêm object 3D. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:172] Backdrop dùng CampaignBackdrop của theme và chỉ bật cùng layout thiết bị.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:543] Code cho vòng giữ trên thiết bị và chữ IN PROGRESS; CancelHold ẩn vòng và trả trạng thái theo CampaignRules. Chưa có bằng chứng playtest giữ/thả hoặc hoàn thành nên không kết luận tương tác đã đạt.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1920x1080-20261101-112533-770.png] Fixture Backup 1080p: SOURCE NOT RECORDED, LOCKED, biểu tượng khóa và Requires: complete Power / Isolate; không có gợi ý F. [PLAYTEST Reports/UIUX/MockupImplementation/After/document-3840x2160-20261101-112539-805.png] Ảnh 4K; [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1024x768-20261101-112545-842.png] ảnh 4:3 có hai vùng và nội dung được ngắt dòng.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1920x1080-20261101-113044-114.png] Fixture Isolate 1080p: SOURCE NOT RECORDED, ACTION READY và HOLD F TO CONFIRM. [PLAYTEST Reports/UIUX/MockupImplementation/After/document-3840x2160-20261101-113050-146.png] Ảnh 4K. Các fixture mở thiết bị từ xa trong Play Mode, không chứng minh đã đi tới vật hoặc tương tác thực.
- [SUY LUẬN] Chưa giống Reports/mockups/04-device-locked.png và 04-device-hold.png: thiếu bezel công nghiệp, header/footer có đường chia, nền các ô xám sáng, nút cam khi giữ và ô phím riêng; nút Close vẫn ở góc trên. Sprite panel hiện tại là GREYBOX PLACEHOLDER, không phải asset chuẩn của màn hình thiết bị. Chưa chuẩn hóa ba cấp chữ của các trường mới vào theme.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:86] DeviceLayoutRestoresDocumentLayoutWhenClosed job d3af5edf đạt 1/1: kiểm sự hiện diện vùng, sprite và bật/tắt backdrop. [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:39] ManifestDeviceShowsFileOnlyAfterItIsRecorded job 5f1059f8 đạt 1/1: kiểm ẩn hai vùng điều khiển cho Manifest và nội dung trước/sau ghi nhận.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Đã dùng Unity-Skills vào Play Mode/local host, chụp fixture, thoát Play Mode và chạy save gate; cuối cùng MainMenu isDirty=false, isPlaying=false, Console 0 lỗi. [SUY LUẬN] Hash hai checkpoint khớp bản sao trước kiểm tra.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md, references/execution-contract.md, references/agent-runbook.md — tái dùng sprite và đối chiếu ảnh nhiều tỷ lệ; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — đặt vùng theo anchors, kiểm ảnh 4:3; C:/Users/hieud/.agents/skills/unity-skills/SKILL.md và skills/asset/SKILL.md — tìm sprite qua AssetDatabase và không import asset. C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md và skills/test/SKILL.md đã đọc ở lượt trước để vận hành Editor và test; create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Tiếp tục cải thiện asset UI, header, footer, nút và hierarchy Manifest theo ảnh được duyệt; không đổi object 3D hay điều kiện nhiệm vụ.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Đã thêm hai vùng vào layout thiết bị, nhưng vẫn thiếu khung và chi tiết như mockup; có ảnh fixture không đồng nghĩa hoàn thành hoặc playtest thành công.

## Chưa kiểm được
- [SUY LUẬN] Chưa playtest giữ/thả F, hoàn tất, thao tác chuột/gamepad, client/server; chưa chụp Manifest và Generator; chưa đủ ảnh trước/sau 1080p và 4K cho chín giao diện. Chưa hoàn thành toàn bộ giao diện hoặc được người dùng duyệt bản triển khai.

# Trang Manifest tạm — 01/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:208] Thêm sidebar RECORDS / Delivery records và vùng tài liệu cho FactoryManifest; các panel điều khiển nguồn/action được ẩn. Tiêu đề và nội dung file lấy từ CampaignContentCatalog; nội dung file chỉ xuất hiện sau khi state ghi nhận. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:213] Mặt trang màu sáng là **GREYBOX PLACEHOLDER** bằng Image, không phải asset giấy chuẩn hoặc asset được import.
- [SCENE AssetDatabase/asset_find] Không tìm thấy sprite theo paper, page, document, journal; Paper_Albedo có chữ in sẵn nên không dùng làm trang dữ liệu. Chưa tìm thấy sprite bezel phù hợp qua các truy vấn monitor, screen, terminal, frame.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1920x1080-20261101-121730-190.png] Ảnh fixture trước khi ghi nhận: bên trái ghi RECORDS / Delivery records; trang hiển thị AL-04 Delivery Manifest, hướng dẫn và SOURCE NOT RECORDED. [PLAYTEST Reports/UIUX/MockupImplementation/After/document-3840x2160-20261101-121736-224.png] Ảnh 4K; [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1024x768-20261101-121742-265.png] ảnh 4:3 có tiêu đề xuống dòng.
- [CODE Assets/FPS/Features/UI/Editor/HudReviewTools.cs:134] Fixture mở bằng reflection trong Play Mode và không gọi RequestDiscoverFile; không phải bằng chứng người chơi đọc file hoặc ảnh có nội dung file đã ghi nhận. [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:39] Test ManifestDeviceShowsFileOnlyAfterItIsRecorded job 69f2f2ce đạt 1/1, kiểm bật sidebar/trang và nội dung trước/sau thay state trong EditMode.
- [SUY LUẬN] So với Reports/mockups/04-device-manifest.png, còn thiếu bezel công nghiệp, màu nền và các đường chia, hiệu ứng trang giấy, ô chọn sáng, footer bên phải và vị trí Close; chữ trên ảnh fixture không phải nội dung file thật đã được ghi. Chưa chuẩn hóa ba cấp chữ vào theme.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Đã chạy save gate, vào Play Mode/local host, chụp fixture rồi thoát; MainMenu isDirty=false, isPlaying=false. [SUY LUẬN] Hash hai checkpoint khớp bản sao. Console còn 1 InvalidOperationException do lần gọi fixture trước khi local host sẵn sàng; sau đó local host đã khởi động và ảnh fixture đã được chụp. Không khẳng định Console sạch.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md và references/execution-contract.md, references/agent-runbook.md — dựng cấu trúc từ mockup và kiểm ảnh nhiều tỷ lệ; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — anchors, vùng cuộn và thử 4:3; C:/Users/hieud/.agents/skills/unity-skills/skills/asset/SKILL.md — tìm asset bằng AssetDatabase; C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md và skills/test/SKILL.md — vận hành Editor, save gate và lấy kết quả test thật. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Hoàn thiện trang Manifest và màn thiết bị với asset UI phù hợp, sau đó kiểm file thực và flow; không thay object 3D.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Lần trước chưa chụp Manifest; lần này chỉ có ảnh fixture chưa ghi nhận, không đồng nghĩa có ảnh file thực hoặc màn đã giống mẫu.

## Chưa kiểm được
- [SUY LUẬN] Chưa playtest đọc Manifest thực, giữ/thả F, hoàn tất, chuột/gamepad và client/server; chưa chụp Generator, chưa đủ ảnh trước/sau 1080p và 4K cho chín màn, chưa hoàn thành hoặc được duyệt bản triển khai.

# Kiểm Journal và chữ dài — 01/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:267] Journal có vùng nội dung cuộn, nhãn CURRENT RECORD, NEXT và các nút điều khiển; dữ liệu lấy từ trạng thái campaign. Đường phím Journal thông thường mở TeamInventoryUI nếu instance tồn tại; OpenJournal là đường dự phòng, không thể coi ảnh fixture là bằng chứng phím mở đúng màn này. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:563]
- [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1920x1080-20261101-131646-266.png] Ảnh fixture Journal 1080p: nút CLOSE [I] ở trên và hai nút nền vân lớn ở dưới, chưa giống mockup có nút chữ/ô phím gọn, đường phân cách và nền thế giới tối. [PLAYTEST Reports/UIUX/MockupImplementation/After/document-3840x2160-20261101-131650-352.png] Ảnh 4K; [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1024x768-20261101-131654-472.png] ảnh 4:3. Các ảnh mở bằng menu fixture trong local host, không chứng minh thao tác mở bằng phím trong gameplay.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:122] EditMode job 159a260b đạt 1/1, kiểm chuyển surface và ScrollRect, không kiểm thị giác.
- [CODE Assets/Tests/PlayMode/HudGameplayAcceptanceTests.cs:104] PlayMode job 832f4885 thất bại vì tiêu đề DetailTitle bị cắt khi nhân đôi tên file. [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:289] Đã mở rộng vùng và bật tự co chữ trong khoảng 23–36; đây là ngoại lệ chưa chuẩn hóa ba cấp chữ theo theme. Job fa9c20bb đã vượt kiểm tra đó ở 1080p, kiểm được cuộn Journal đến cuối và Back đóng document, nhưng thất bại tại 720p vì Eyebrow không đạt ngưỡng kích thước chữ 16 px. Không tuyên bố test PlayMode đạt. [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/ArtworkScrollAndEventBindingsSurviveStress.txt]
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Sau test Unity ở Edit Mode, MainMenu isDirty=false; Console trả 0 lỗi. [SUY LUẬN] Hai checkpoint chính và .previous có SHA-256 khớp bản sao checkpoint-backup.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md và references/execution-contract.md, references/agent-runbook.md — kiểm ảnh ở các tỷ lệ, tách fixture khỏi bằng chứng gameplay; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — kiểm chữ dài và ScrollRect; C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md và skills/test/SKILL.md — Save Gate, Play Mode và lấy kết quả test; C:/Users/hieud/.agents/skills/unity-skills/skills/debug/SKILL.md — kiểm trạng thái compile và lỗi; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — sửa tập trung vùng tiêu đề. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Sửa khả năng đọc 720p, kiểm đường mở Journal thực tế và tiếp tục hoàn thiện chín màn so với mockup, không thay object 3D hoặc flow.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Cuộn và Back đã qua kiểm fixture 1080p nhưng toàn bộ PlayMode stress vẫn thất bại tại 720p; ảnh Journal chưa chứng minh đường mở phím thông thường hoặc thẩm mỹ đã được duyệt.

## Chưa kiểm được
- [SUY LUẬN] Chưa hoàn thành chín giao diện, chưa đủ ảnh trước/sau 1080p và 4K, chưa kiểm hold/release, host-client, opacity và đường mở Journal thông thường. Ảnh Journal khác mẫu; chưa được người dùng duyệt bản triển khai.

# Điều chỉnh Journal theo mockup — 01/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:267] Thu hẹp panel Journal, dùng lại nền tối CampaignBackdrop, thêm ba đường chia bằng sprite Fill hiện có, thay nút nền vân bằng nút chữ sử dụng TMP LiberationSans và giữ nút Close/Resume. Không đổi nội dung hay hành vi nhiệm vụ.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1920x1080-20261101-143732-921.png] Fixture 1080p hiển thị Journal, CURRENT RECORD, NEXT, CLOSE JOURNAL và RESUME CHECKPOINT; ảnh [PLAYTEST Reports/UIUX/MockupImplementation/After/document-3840x2160-20261101-143736-980.png] 4K và [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1024x768-20261101-143741-032.png] 4:3. Mở bằng menu Preview Journal trên local host, không phải bằng phím của người chơi.
- [SUY LUẬN] So với Reports/mockups/04-campaign-journal.png, panel và đường chia gần hơn nhưng vẫn thiếu ô phím riêng ở góc trên và footer, cỡ chữ/chiều dọc khác và nội dung mẫu không trùng trạng thái campaign hiện tại. Không kết luận giống hệt hoặc được duyệt.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:122] Job 9baf3498 đạt 1/1: kiểm nền bật/tắt, nút Close trong suốt và TMP label. Không kiểm click hoặc phím. [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Sau compile, test và Play Mode đã thực hiện Save Gate; Unity ở Edit Mode, MainMenu isDirty=false, Console 0 lỗi. [SUY LUẬN] SHA-256 hai checkpoint khớp bản sao checkpoint-backup.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md cùng references/execution-contract.md và references/agent-runbook.md — đối chiếu mockup, ảnh 1080p/4K/4:3 và ghi giới hạn fixture; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — duy trì các nút và nhãn hành động; C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md và skills/test/SKILL.md — dùng Editor và chạy test theo Save Gate; C:/Users/hieud/.agents/skills/unity-skills/skills/debug/SKILL.md — kiểm compile và lỗi; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — giới hạn thay đổi tại Journal. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Tiếp tục đưa Journal vào đường mở thực tế mà không mất quyền truy cập Inventory, hoàn thiện ô phím, kiểm thao tác và các giao diện còn lại theo mẫu.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Ảnh chụp chứng minh fixture vẽ được cấu trúc mới, không chứng minh người chơi mở được Journal bằng phím hoặc phê duyệt thẩm mỹ; test EditMode không thay thế playtest.

## Chưa kiểm được
- [SUY LUẬN] Chưa hoàn thành chín màn; chưa kiểm click, phím, host/client, opacity, hold/release và ảnh trước/sau đầy đủ. Lỗi chữ 720p trong PlayMode stress trước đó chưa được sửa và chưa chạy lại test sau thay đổi Journal.

# Bố cục màn thiết bị — 01/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:207] DeviceSurface được đặt vào trong khung, thêm đường chia header/footer bằng sprite Fill; dùng lại Assets/FPS/Features/UI/Content/Resources/HudSkin/Button.png làm khung và Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/Surfaces/Panel.png cho nội dung. Khung Button là **GREYBOX PLACEHOLDER** vì không phải asset bezel được thiết kế cho màn hình, không phải asset chuẩn theo mockup.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:194] Nút CLOSE được đưa ra gần góc dưới phải và dùng TMP; nút xác nhận dùng TMP, biểu tượng khóa nằm trong nút, và khi đóng thiết bị biểu tượng được ẩn để không rò sang màn khác. Không thay object 3D, CampaignRules hoặc nội dung nhiệm vụ.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1920x1080-20261101-164521-643.png] Fixture Backup 1080p hiện Requires: complete Power / Isolate, LOCKED và biểu tượng khóa; không hiện phím F. [PLAYTEST Reports/UIUX/MockupImplementation/After/document-3840x2160-20261101-164527-712.png] 4K; [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1024x768-20261101-164533-769.png] 4:3. Ảnh mở bằng menu fixture trong local host, không chứng minh người chơi đã tiếp cận hay sử dụng thiết bị.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1920x1080-20261101-162653-284.png] Fixture Isolate sau thay đổi bố cục trước khi dịch icon cuối cùng hiện SOURCE NOT RECORDED và ACTION READY; đó là kết quả của CampaignRules hiện tại, chưa chứng minh flow này đúng. [SUY LUẬN] So với Reports/mockups/04-device-locked.png, màn vẫn thiếu bezel công nghiệp đúng mẫu, nền xám sáng và các khối trạng thái/ô phím; ảnh chưa phải phê duyệt.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:92] Test DeviceLayoutRestoresDocumentLayoutWhenClosed job 94997b52 đạt 1/1 sau thay đổi cuối. Chạy cả HudRegressionTests trước khi dịch icon: job 992b17d7 đạt 14/15; ProjectionHandlesBehindCameraAndAvoidsOccupiedCorner thất bại vì kỳ vọng (640,360), thực tế (512,360) trong trạng thái Game View 1024x768. Không khẳng định nguyên nhân ngoài dữ liệu đó, không sửa code waypoint.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Sau test và Play Mode đã Save Gate: MainMenu isDirty=false, isPlaying=false, Console 0 lỗi. [SUY LUẬN] SHA-256 hai checkpoint khớp bản sao checkpoint-backup.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md cùng references/execution-contract.md, references/agent-runbook.md — đối chiếu mockup, tái dùng sprite và kiểm 1080p/4K/4:3; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — bố trí lý do khóa và nút; C:/Users/hieud/.agents/skills/unity-skills/skills/asset/SKILL.md — kiểm asset hiện có, không import; C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md, skills/test/SKILL.md và skills/debug/SKILL.md — Save Gate, compile và kiểm test; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — giới hạn thay đổi UI. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Tiếp tục hoàn thiện bezel bằng asset UI thích hợp, các trạng thái hold và các màn còn lại, sau đó kiểm flow thực tế và đầy đủ ảnh trước/sau.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Test layout 1/1 không mâu thuẫn với kết quả toàn bộ nhóm 14/15: đây là hai phạm vi chạy khác nhau. Ảnh fixture không chứng minh gameplay hay rằng thiết bị đã giống mẫu.

## Chưa kiểm được
- [SUY LUẬN] Tại thời điểm phần này được viết, chưa kiểm giữ/thả F và hoàn tất; kết quả fixture bổ sung được ghi ở phần sau. Chưa có đủ ảnh trước/sau cho tất cả màn, chưa có asset bezel chuẩn, chưa được duyệt bản triển khai; lỗi chữ 720p và test waypoint ở trên còn phải đánh giá riêng.

# Vòng giữ thiết bị và debrief — 02/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:248] Tái sử dụng sprite Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/GoalProgress.png cho vòng nền; cung trắng biểu thị tiến trình, vòng tối vẫn nhìn thấy khi mới giữ; hủy hoặc đóng sẽ ẩn vòng. Trạng thái giữ hiển thị COMMAND IN PROGRESS, IN PROGRESS và RELEASE TO CANCEL. Không thay CampaignRules hoặc object 3D.
- [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/campaign-hold.png] Ảnh Game View 1080p chụp bằng fixture local host, phím F tổng hợp và gọi tick authority: nút màu cam nâu có vòng, [F] HOLD TO CONFIRM, trạng thái và hướng dẫn nhả. Đây không phải playtest người thật đi tới thiết bị; còn khác mockup Reports/mockups/04-device-hold.png về bezel, nền thẻ, phân cấp và ô phím.
- [CODE Assets/Tests/PlayMode/HudGameplayAcceptanceTests.cs:204] Trong job df29876c và chạy lại sau hoàn tác 83c16fdb, fixture kiểm được bắt đầu giữ, hiện vòng nền, thả sớm hủy phía authority và giữ lại hoàn tất; toàn bài 0/1 vì Eyebrow không đạt ngưỡng 16 px tại 1280x720. Kiểm client dùng giả lập presentation/sender, không phải hai máy thực. [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/CampaignHoldAndDebriefRespectCancellationAndAuthority.txt] Log này được ghi đè bởi lần chạy 7547cd2c ở phần sau.
- [CODE Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs:170] Mở rộng chiều cao vùng thống kê từ .08 lên .10 chiều cao panel. Trong job df29876c kiểm tra Statistic0 / 00:36 đã qua ở 1920x1080 với scale 100% và 125%, trước khi dừng ở lỗi 720p. Đây không chứng minh mọi chuỗi hoặc mọi độ phân giải đã hết cắt.
- [SUY LUẬN] Đã thử tăng chữ debrief tối thiểu lên 24; job 6d3ac244 thất bại vì TEAM DOWNS bị cắt tại 1920x1080 scale 125%. Đã hoàn tác riêng thử nghiệm tăng cỡ chữ đó, giữ thay đổi chiều cao thống kê. Chưa có giải pháp cân bằng kích cỡ 720p và không cắt ở 125%.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Sau test và compile cuối đã chạy Save Gate; MainMenu isDirty=false, Unity không ở Play Mode, Console 0 lỗi. Không import asset, không xác nhận bản triển khai được phê duyệt.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md và references/execution-contract.md — so bố cục với mockup, phân biệt fixture và playtest, giữ kết quả kiểm thử thất bại; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — kiểm kích thước chữ và trạng thái tương tác; C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md, test/SKILL.md, debug/SKILL.md — kiểm Editor, chạy test bất đồng bộ và kiểm compile; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — tái dùng sprite sẵn có và giới hạn sửa đổi. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Thiết kế lại vùng thống kê debrief để chữ đủ lớn tại 720p mà không cắt ở 125%, rồi chạy lại bài kiểm thử; tiếp tục hoàn thiện các màn theo mockup đã duyệt, không thay flow.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Các kiểm hold riêng trong fixture đạt không đồng nghĩa toàn bài test đạt; cả ba job đều 0/1. Việc hoàn tác thử nghiệm tăng chữ không hoàn tác vòng nền và vùng thống kê đã mở rộng. Ảnh fixture không chứng minh thiết kế đã giống mẫu.

## Chưa kiểm được
- [SUY LUẬN] Chưa hoàn thành chín giao diện, chưa đủ ảnh trước/sau 1080p và 4K, chưa kiểm client thực, chuột/gamepad, opacity hoặc hành trình gameplay tự nhiên. Đã chạy lại bài test mục tiêu sau hoàn tác nhưng chưa chạy toàn bộ suite; lỗi 720p, khác biệt thẩm mỹ và asset bezel vẫn tồn tại. Chưa được người dùng duyệt kết quả triển khai.

# Căn bố cục thiết bị — 02/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:209] Căn lại vùng màn hình, header, hai thẻ hướng dẫn/điều khiển và footer theo tỷ lệ mockup 1920x1080; dịch nút khóa/vòng giữ tương ứng. Không sửa CampaignRules, nội dung hay object 3D. [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/campaign-hold.png] Ảnh fixture giữ F 1080p sau căn bố cục cho thấy nút cam nâu và vòng giữ; ảnh được chụp trước chỉnh sửa riêng Manifest ở dưới.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1920x1080-20261101-173418-637.png] Fixture Backup 1080p ghi Requires: complete Power / Isolate, LOCKED và biểu tượng khóa, không có [F]. [PLAYTEST Reports/UIUX/MockupImplementation/After/document-3840x2160-20261101-173427-679.png] Ảnh 4K; [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1024x768-20261101-173436-724.png] ảnh 4:3. Đây là preview local host được mở từ menu, không phải người chơi tự đi tới thiết bị.
- [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1024x768-20261101-173322-725.png] Trước chỉnh sửa riêng Manifest, tiêu đề hai dòng chạm đầu trang. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:222] Sau đó tăng chiều cao vùng tiêu đề và hạ trang; [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1024x768-20261101-173852-398.png] ảnh 4:3 cho thấy khoảng trống giữa tiêu đề và trang. [PLAYTEST Reports/UIUX/MockupImplementation/After/document-1920x1080-20261101-173834-320.png] 1080p; [PLAYTEST Reports/UIUX/MockupImplementation/After/document-3840x2160-20261101-173843-361.png] 4K. Preview Manifest không ghi file, nên SOURCE NOT RECORDED không phải nội dung tài liệu đã thu thập.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:39] Job 80278806 đạt 1/1 kiểm bố cục Manifest và ẩn nội dung trước ghi nhận; job f6b63704 đạt 1/1 kiểm bật/tắt bố cục thiết bị trước chỉnh riêng Manifest. [CODE Assets/Tests/PlayMode/HudGameplayAcceptanceTests.cs:204] Job 7547cd2c đạt các bước giữ/hủy trong fixture nhưng toàn bài 0/1 vì Eyebrow dưới 16 px tại 720p; sau chỉnh Manifest chưa chạy lại bài PlayMode này. [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/CampaignHoldAndDebriefRespectCancellationAndAuthority.txt]
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Đã dùng Unity 6000.5.6f1, UGUI runtime dưới CampaignCanvas/JournalAndDocument; trước chỉnh không có object nào được chọn, cửa sổ Game View và Console 0 lỗi. Sau compile, test, hai lần Play Mode/local host và Save Gate, MainMenu isDirty=false, isPlaying=false, Console 0 lỗi. SHA-256 hai checkpoint khớp bản sao trước kiểm thử.
- [SUY LUẬN] Vẫn khác mockup: bezel Button.png là GREYBOX PLACEHOLDER bị kéo dọc; hai thẻ và nền còn gần đen thay vì xám, thiếu khối COMMAND/LOCKED, breadcrumb và footer thiết bị có nhãn; Manifest thiếu sidebar đủ mục và nền giấy chuẩn. Lần tìm AssetDatabase đã thấy WindowFrame và WindowFrameBackground nhưng ảnh sprite màu xanh công nghệ, không phù hợp mẫu; không import hoặc tạo asset. Ảnh chụp không phải phê duyệt thị giác.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md cùng references/execution-contract.md và references/agent-runbook.md — căn anchors theo mockup, so 1080p/4K/4:3 và phân biệt fixture; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — kiểm chữ bị chạm và nội dung trạng thái; C:/Users/hieud/.agents/skills/unity-skills/skills/asset/SKILL.md — tìm sprite trước khi dùng lại; C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md, test/SKILL.md, debug/SKILL.md — Save Gate, Play Mode, test bất đồng bộ, kiểm compile; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — thay đổi tập trung và bổ sung kiểm tra. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Tìm asset bezel và bề mặt UI phù hợp mẫu, xử lý cỡ chữ 720p, hoàn thiện các vùng còn thiếu và chạy lại kiểm thử toàn diện mà không thay flow.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Hai test EditMode 1/1 kiểm các phạm vi riêng, không phủ nhận PlayMode 0/1. Ảnh Backup được chụp trước thay đổi chỉ áp dụng cho Manifest; ảnh Manifest sau chỉnh không phải ảnh file đã thu thập hay chứng cứ hoàn thành giao diện.

## Chưa kiểm được
- [SUY LUẬN] Tại thời điểm này chưa hoàn thành chín giao diện; thiếu ảnh trước/sau và playtest thực tế cho toàn bộ màn, chưa kiểm client thực, gamepad, opacity và Generator. Vấn đề cỡ chữ debrief 720p được kiểm lại ở cập nhật tiếp theo; thiếu asset bezel, thiếu một số chi tiết so với mockup và chưa có phê duyệt bản triển khai.

# Debrief và Retry — 02/11/2026
- [CODE Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs:47] Cỡ chữ debrief được tăng khi cần để đạt tối thiểu 16 pixel theo canvas; vùng nội dung mở rộng tới tối thiểu 600 pixel tại bề ngang hẹp, giữ tỷ lệ gốc khi đủ rộng.
- [CODE Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs:200] Nút Retry giữ nguyên lời gọi RequestRetry, vùng nhấn và điều hướng Selectable; bỏ nền SlotSelected sáng, dùng chữ RETRY CHECKPOINT một dòng, chữ chuyển màu khi được chọn hoặc rê chuột. Đã bỏ nhãn [ENTER] vì chưa xác minh binding Submit thực tế; mockup có keycap riêng và hiện chưa tái hiện được chi tiết đó.
- [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/debrief-failed-1920x1080-100.png] Ảnh fixture Failed 1080p; [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/debrief-failed-3840x2160-100.png] 4K; [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/debrief-failed-1024x768-100.png] 4:3 cho thấy Retry không có khối nền sáng và nằm trên một dòng. [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/debrief-completed-1920x1080-100.png] Complete 1080p; [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/debrief-completed-3840x2160-100.png] Complete 4K.
- [CODE Assets/Tests/PlayMode/HudGameplayAcceptanceTests.cs:204] Job 2de34488 đạt 1/1: fixture local host kiểm giữ/thả/hoàn tất thiết bị và chữ Failed/Complete tại 1080p, 720p, 4:3, 4K, 1080p scale 125%; Complete thêm ultrawide. Kiểm client và sender là giả lập, không phải client thật; bài kiểm Retry kiểm dòng chữ và targetGraphic, không bấm Retry thật. [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/CampaignHoldAndDebriefRespectCancellationAndAuthority.txt] Log được ghi đè bởi job này.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Unity 6000.5.6f1: sau compile, test và Save Gate, MainMenu isDirty=false, Edit Mode, Console 0 lỗi. Không thay object 3D hoặc import asset mới trong lần sửa này.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md và references/execution-contract.md, references/agent-runbook.md — sửa UGUI theo mockup, kiểm tỷ lệ khác và nêu sai khác; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — kiểm độ đọc chữ và focus; C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md, test/SKILL.md, debug/SKILL.md — Save Gate, test bất đồng bộ, kiểm compile; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — giới hạn sửa đổi và kiểm thử vùng đã sửa. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Xác minh binding Submit thực tế và bổ sung keycap đúng thiết bị, sau đó tiếp tục đối chiếu chín giao diện với mockup và kiểm playtest thực.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Các lỗi 720p trong mục cũ phản ánh những lần chạy trước; job 2de34488 đã vượt kiểm chữ ở 720p với dữ liệu fixture, không chứng minh mọi nội dung hoặc mọi độ phân giải. Ảnh fixture không phải phê duyệt thẩm mỹ.

## Chưa kiểm được
- [SUY LUẬN] Chưa hoàn thành chín giao diện, chưa có đủ ảnh trước/sau và playtest tự nhiên 1080p/4K, chưa kiểm thao tác Retry bằng chuột/bàn phím/gamepad, client thật, opacity, Generator và độ giống mẫu cho toàn bộ UI; keycap Retry và asset bezel còn thiếu.

# Khối trạng thái thiết bị — 02/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:225] Màn thiết bị có thêm khối trạng thái nằm dưới nội dung hướng dẫn: LOCKED kèm lý do từ CampaignRules, COMMAND / Hold to confirm khi sẵn sàng và RELEASE EARLY TO CANCEL khi đang giữ. Khối dùng lại sprite panel hiện có làm **GREYBOX PLACEHOLDER**, không thay luật nhiệm vụ hoặc object 3D.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:454] Khi nguồn chưa được ghi, vùng nội dung chỉ hiện hướng dẫn thao tác, trạng thái NOT RECORDED nằm trong cột Source và khối trạng thái nhắc READ LINKED DOCUMENT FOR DETAILS; nội dung nguồn đã ghi vẫn được đưa vào vùng có ScrollRect. Nhãn SCROLL FOR MORE chỉ hiện khi nội dung dài hơn vùng nhìn. Manifest không dùng khối trạng thái này.
- [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/device-locked-1920x1080.png] Fixture Backup 1080p cho thấy LOCKED, Requires: complete Power / Isolate, biểu tượng khóa và không có [F]; [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/device-locked-3840x2160.png] ảnh 4K; [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/device-locked-1024x768.png] ảnh 4:3. Các ảnh là mở trực tiếp bằng fixture, không chứng minh người chơi đã đi tới thiết bị.
- [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/campaign-hold.png] Fixture Isolate giữ F 1080p cho thấy COMMAND / Hold to confirm / RELEASE EARLY TO CANCEL, vòng tiến trình và nút HOLD TO CONFIRM. [CODE Assets/Tests/PlayMode/HudGameplayAcceptanceTests.cs:204] Job 60f0f7f4 đạt 1/1: kiểm khóa ở 1080p, 4K, 4:3, kiểm chữ lý do khóa không tràn, giữ/thả/hoàn tất bằng phím F giả lập và kiểm debrief; sender/client là giả lập. Log tại [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/CampaignHoldAndDebriefRespectCancellationAndAuthority.txt].
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Đã dùng Unity 6000.5.6f1, UGUI runtime và Play Mode test; sau compile/test và Save Gate, MainMenu isDirty=false, Edit Mode, Console 0 lỗi. Không import asset.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md và references/execution-contract.md, references/agent-runbook.md — so cấu trúc với mockup và kiểm 1080p/4:3; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — phân biệt khóa, sẵn sàng và đang giữ; C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md, test/SKILL.md, debug/SKILL.md — Save Gate, kiểm test và compile; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — giới hạn thay đổi và bổ sung kiểm thử. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Tiếp tục thay các bề mặt placeholder bằng asset UI phù hợp và xử lý breadcrumb, footer, màu nền thiết bị theo mẫu đã duyệt; không thay object 3D hoặc flow.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Việc có khối LOCKED/COMMAND khắc phục một mục còn thiếu trong báo cáo cũ, không có nghĩa toàn màn giống mockup. Isolate vẫn cho thao tác khi nguồn chưa ghi theo luật hiện tại; không tự biến nguồn thành điều kiện khóa.

## Chưa kiểm được
- [SUY LUẬN] Tại thời điểm này chưa kiểm nội dung nguồn đã ghi dài và khả năng cuộn bằng chuột/gamepad sau khi thu hẹp vùng văn bản; xem kết quả bổ sung ở phần sau. Chưa kiểm toàn bộ objective, opacity, client thực hoặc playtest tự nhiên. Các panel vẫn tối, bezel bị kéo dọc và nhiều chi tiết khác mẫu; chín giao diện chưa hoàn thành.

# Vùng đọc nguồn thiết bị — 02/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:454] Khi file liên kết đã được ghi và có nội dung, mở rộng vùng hướng dẫn theo chiều dọc và chuyển khối trạng thái xuống dưới, không che vùng đọc; khi nguồn chưa được ghi hoặc là Manifest thì giữ bố cục tương ứng. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:413] Khi mở thiết bị, dừng quán tính cuộn và đưa nội dung về đầu.
- [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/device-source-recorded-1920x1080.png] Ảnh fixture nguồn Backup đã ghi ở 1080p; [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/device-source-recorded-1024x768.png] 4:3; [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/device-source-recorded-3840x2160.png] 4K. Nội dung dài được cắt tại mép vùng đọc và có SCROLL FOR MORE; ảnh không chứng minh chất lượng thị giác đã được duyệt.
- [CODE Assets/Tests/PlayMode/HudGameplayAcceptanceTests.cs:224] Job 1b289b13 đạt 1/1: kiểm nguồn đã ghi, vùng đọc không đè khối trạng thái, sự kiện scroll giả lập, tới dòng cuối của nội dung lặp tổng hợp và trở về đầu khi mở lại tại 1080p, 4K và 4:3; đồng thời kiểm giữ/hủy và debrief. [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/device-source-scroll-synthetic-1920x1080.png] Ảnh nội dung lặp tổng hợp tại cuối vùng cuộn; [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/CampaignHoldAndDebriefRespectCancellationAndAuthority.txt] log lần chạy đạt.
- [SUY LUẬN] Job aa6afcdc trước đó 0/1 do assertion và InvalidOperationException trong bộ đệm NativeInputRuntime của Unity Input System; lần chạy lại 1b289b13 đạt. Chưa đủ dữ liệu để kết luận nguyên nhân gốc lỗi bộ đệm. Log text bị lần chạy mới ghi đè; chi tiết lỗi nằm trong kết quả job aa6afcdc.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Sau kiểm thử và Save Gate, MainMenu isDirty=false, Edit Mode, Console 0 lỗi. Không import asset hoặc thay object 3D.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md và references/execution-contract.md, references/agent-runbook.md — kiểm chủ sở hữu ScrollRect, nội dung dài và tỉ lệ màn hình; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — duy trì trạng thái khóa trong khi đọc; C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md, test/SKILL.md, debug/SKILL.md — Save Gate và kiểm test/Console; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — sửa điểm reset cuộn và thêm kiểm thử tập trung. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Tiếp tục hoàn thiện thiết bị và các màn còn lại theo mockup, kiểm nguồn đã ghi bằng input thật và kiểm điều hướng gamepad.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Kết quả sự kiện cuộn giả lập và nội dung lặp tổng hợp không chứng minh thao tác chuột vật lý, gamepad, hay playtest tự nhiên. Job thất bại do Input System được ghi riêng, không bị thay bằng kết quả chạy lại.

## Chưa kiểm được
- [SUY LUẬN] Chưa hoàn thành chín giao diện, chưa kiểm chuột vật lý/gamepad, opacity, toàn bộ objective hoặc client thực. Panel và bezel vẫn khác mockup; ảnh fixture không phải phê duyệt thẩm mỹ.

# Cập nhật thiết bị — 02/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:247] Bổ sung breadcrumb theo màn: POWER SYSTEM / PRODUCTION, BACKUP hoặc GENERATOR; Manifest dùng RECORDS / LOGISTICS / MANIFEST. Không sửa logic nhiệm vụ.
- [CODE Assets/FPS/Features/UI/Runtime/TacticalUiTheme.cs:20] Nền sau giao diện thiết bị dùng alpha 0,90; Journal tiếp tục dùng nền alpha 0,52. Đây không phải tùy chỉnh opacity cho người chơi.
- [PLAYTEST Reports/UIUX/MockupImplementation/Before/20261102-device/device-locked-1920x1080.png] và [PLAYTEST Reports/UIUX/MockupImplementation/Before/20261102-device/device-locked-3840x2160.png] là ảnh trước; [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/device-locked-1920x1080.png] và [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/device-locked-3840x2160.png] là ảnh sau. Fixture mở Backup trực tiếp; không phải người chơi đi đến thiết bị trong game.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:39] Job EditMode baeb8188 đạt 15/15 sau biên dịch; [CODE Assets/Tests/PlayMode/HudGameplayAcceptanceTests.cs:204] job PlayMode 2fae8891 đạt 1/1, kiểm thử host fixture và đầu vào tổng hợp. Job 1ebeab3d chạy trước khi DLL được cập nhật nên không dùng làm bằng chứng cho thay đổi.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Sau test và Save Gate: MainMenu isDirty=false, không ở Play Mode; Console 0 lỗi.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md cùng references/execution-contract.md và references/agent-runbook.md: đối chiếu bố cục và tách fixture khỏi playtest; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md: dùng anchor và kiểm tra tỷ lệ khác; C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md, test/SKILL.md, debug/SKILL.md: Save Gate, kiểm tra trạng thái và chạy test bất đồng bộ; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md: thay đổi giới hạn và kiểm thử. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Tiếp tục thay bezel bị kéo dọc bằng asset UI phù hợp đã được phép sử dụng, và hoàn thiện các màn còn lại theo mockup; không thay object 3D.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Ảnh thiết bị sau sửa vẫn dùng bezel và panel tối làm GREYBOX PLACEHOLDER, khác mockup; breadcrumb và nền tối hơn không chứng minh chín giao diện đã hoàn tất hoặc được duyệt thị giác.

## Chưa kiểm được
- [SUY LUẬN] Chưa hoàn tất chín giao diện; chưa có điều chỉnh opacity cho người chơi, chưa đủ ảnh trước/sau và playtest tự nhiên ở 1080p và 4K cho tất cả màn. Chưa kiểm client thật, chuột/gamepad hoặc mọi objective; chưa kiểm ảnh Manifest sau thay đổi breadcrumb. Các đoạn báo cáo cũ có dấu hiệu lỗi mã hóa tiếng Việt, cần phục hồi từ nguồn đáng tin cậy nếu có.

# Opacity HUD và phụ đề — 02/11/2026
- [CODE Assets/FPS/Features/UI/Runtime/SettingsUI.cs:123] Trong Graphics thêm slider HUD panel opacity, phạm vi 0–100%, mặc định 100%; giá trị được lưu PlayerPrefs, RESET DEFAULTS trả 100%. Slider dùng lại hàng sensitivity hiện có thay vì import asset.
- [CODE Assets/FPS/Features/UI/Runtime/HudSkin.cs:15] [CODE Assets/FPS/Features/UI/Runtime/HUDManager.cs:275] [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:83] [CODE Assets/FPS/Features/Survival/Runtime/SurvivalHotbar.cs:36] Giá trị điều chỉnh alpha ảnh nền tracker, HealthReadabilityPlate, WeaponFrame và bốn Shade của hotbar; không chỉnh alpha chữ, icon hoặc cảnh báo. Không áp dụng tùy chọn này cho panel thiết bị, Inventory hay debrief.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:190] Tắt ảnh nền sau phụ đề, giữ chữ và thêm Outline màu đen; không thay audio hay lời thoại.
- [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/settings-hud-opacity-1920x1080.png] [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/settings-hud-opacity-3840x2160.png] Ảnh Game View khi fixture mở Settings / Graphics và đặt slider 35%. [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/hud-opacity-35-1920x1080.png] [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/hud-opacity-35-3840x2160.png] HUD ở 35%; ảnh 100% cùng thư mục có tên hud-opacity-100-1920x1080.png và hud-opacity-100-3840x2160.png. Đây là local-host fixture, không phải người chơi tự thao tác.
- [PLAYTEST Reports/UIUX/MockupImplementation/Before/20261102-subtitle/hud-1920x1080.png] [PLAYTEST Reports/UIUX/MockupImplementation/Before/20261102-subtitle/hud-3840x2160.png] Ảnh trước khi bỏ nền phụ đề, đã bật thay đổi opacity. Ảnh Settings trước nằm trong cùng thư mục.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:20] EditMode job e0dd3398: 16/16; kiểm lưu giá trị, mở lại không nhân slider và chặn giá trị ngoài 0–1. Job a32e5c5b không khởi động được Test Runner trong 90 giây, 0 test; job ed38b5d2 chạy lại nhóm SettingsDifficultyRegressionTests đạt 9/9.
- [CODE Assets/Tests/PlayMode/HudGameplayAcceptanceTests.cs:104] PlayMode job decf2fe8: 1/1; fixture gọi nút Graphics và thay slider bằng code, kiểm alpha nền, alpha chữ tracker, máu và cảnh báo đặc biệt, ghi ảnh 1080p/4K. Không kiểm chứng kéo chuột vật lý hoặc gamepad.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Sau test và Save Gate, MainMenu isDirty=false; Edit Mode, Console 0 lỗi. Không import asset, sửa object 3D, gameplay, audio hoặc save game.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md, references/execution-contract.md và references/agent-runbook.md — dùng UI sẵn có, chụp trước/sau và phân biệt fixture; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — giữ chữ và cảnh báo độc lập với nền; C:/Users/hieud/.agents/skills/unity-skills/skills/asset/SKILL.md — tìm asset sẵn có trước; C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md, test/SKILL.md, debug/SKILL.md — Save Gate, compile, kiểm thử; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — tái sử dụng slider, thay đổi trong phạm vi HUD. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Hoàn thiện bezel và bề mặt thiết bị bằng asset UI có nguồn phù hợp, sau đó đối chiếu từng màn còn lại với mockup được duyệt.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Khả năng đổi opacity chỉ áp dụng nền HUD được liệt kê, không có nghĩa toàn bộ panel trong game đổi opacity; cảnh báo và chữ không đổi alpha, nhưng bài test chưa chứng minh độ tương phản trong mọi nền hoặc mọi trạng thái.

## Chưa kiểm được
- [SUY LUẬN] Chín giao diện chưa giống hệt mockup và chưa hoàn tất; panel/bezel thiết bị vẫn là GREYBOX PLACEHOLDER. Chưa có đủ ảnh trước/sau và playtest tự nhiên 1080p/4K cho cả chín màn, chưa kiểm chuột/gamepad thật, client thật hoặc toàn bộ objective. Journal vẫn chưa chứng minh đường truy cập thông thường; các đoạn báo cáo cũ có lỗi mã hóa tiếng Việt.

# Tracker Factory — 02/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:114] Tách số tiến độ hai nhánh sang cột căn phải; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:175] giữ phím đổi nhánh, ẩn cột ở ADS hoặc ngoài Factory/Exploring; [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:222] các số vẫn lấy từ trạng thái nhiệm vụ, không đổi flow.
- [PLAYTEST Reports/UIUX/MockupImplementation/Before/20261102-tracker/hud-1920x1080.png] [PLAYTEST Reports/UIUX/MockupImplementation/Before/20261102-tracker/hud-3840x2160.png] Ảnh trước; [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/hud-opacity-100-1920x1080.png] [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/hud-opacity-100-3840x2160.png] ảnh sau trong local-host fixture, không phải playtest tự nhiên.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:277] EditMode job 0187b388: 1/1, kiểm chỉ nhánh được chọn có dấu ▶. [CODE Assets/Tests/PlayMode/HudGameplayAcceptanceTests.cs:104] PlayMode job 886c58ee: 1/1, kiểm cột căn phải, số 0/3 và 0/2 của fixture, phím đổi nhánh, opacity và chụp 1080p/4K.
- [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/NavigationUsesLiveSceneAndInvalidatesConsistently.txt] Job 79f62ee4: 0/1 do fixture tìm InfectionIcon khi object đang ẩn. [CODE Assets/Tests/PlayMode/HudGameplayAcceptanceTests.cs:1273] Sửa tìm cả object ẩn; job c79b6a00 chạy được kiểm tra bố cục 1024×768, 1280×720, 1920×1080, 2560×1080 và 3440×1440 ở 100%/125%, sau đó 0/1 tại kiểm tra nhãn khoảng cách đường đi một phần; không sửa logic điều hướng ngoài phạm vi này. [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/tracker-marker-1024x768-100.png] [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/tracker-marker-3440x1440-125.png] là hai ảnh fixture từ lần chạy này.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Sau test và Save Gate: MainMenu isDirty=false, Edit Mode, Console 0 lỗi.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md, references/execution-contract.md và references/agent-runbook.md — đối chiếu mockup, giữ chủ sở hữu UGUI và ghi riêng fixture; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — giữ anchor góc và kiểm tỷ lệ khác; C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md, test/SKILL.md, debug/SKILL.md — Save Gate, chạy test bất đồng bộ và kiểm Console; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — giới hạn sửa vào tracker và test liên quan. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Tiếp tục thay khung và kiểu chữ cho đúng mockup đã duyệt, xác minh icon Warden và hoàn thiện các màn còn lại; không thay object 3D.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Kiểm tra tracker tại các tỷ lệ đã chạy trước assertion thất bại không biến toàn bộ job c79b6a00 thành đạt; ảnh fixture không chứng minh đã hoàn thành chín giao diện hay được duyệt thị giác.

## Chưa kiểm được
- [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/hud-opacity-100-1920x1080.png] HUD vẫn khác mockup: tracker lớn và dùng font cô đọng, khung khác mẫu và Warden không có icon trong ảnh. [SUY LUẬN] Chưa hoàn thiện chín giao diện, chưa kiểm playtest tự nhiên, client thực hoặc gamepad; kiểm tra điều hướng một phần thất bại như trên.

# Kiểu chữ tracker — 03/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:105] Tracker dùng LiberationSans SDF có sẵn cho tên khu vực, mục tiêu và hai nhánh; kích thước panel còn 468×240 đơn vị canvas thay vì 520×264. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:119] Dấu ▶ dùng lại Text và font HUD cũ để tránh ô vuông thiếu glyph; hình ảnh thực tế hiển thị mũi tên sát trước nhánh được chọn.
- [PLAYTEST Reports/UIUX/MockupImplementation/Before/20261102-tracker-font/hud-1920x1080.png] [PLAYTEST Reports/UIUX/MockupImplementation/Before/20261102-tracker-font/hud-3840x2160.png] Ảnh trước đổi font. [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/hud-opacity-100-1920x1080.png] [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/hud-opacity-100-3840x2160.png] Ảnh sau trong local-host fixture. [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/tracker-marker-1920x1080-100.png] Ảnh fixture mục tiêu dài xuống hai dòng; không kết luận đã khớp mockup hay được duyệt thị giác.
- [CODE Assets/Tests/Editor/UI/HudRegressionTests.cs:277] EditMode job 79b266b1: 1/1. [CODE Assets/Tests/PlayMode/HudGameplayAcceptanceTests.cs:104] PlayMode job 6a8ba388: 1/1, kiểm opacity, nội dung mũi tên và không gian dọc, ghi ảnh 1080p/4K. Job bab383e9 không khởi động trong 90 giây, 0 test; sau khi phục hồi MainMenu và biên dịch lại, các job bef1bcce và 6a8ba388 đạt 1/1.
- [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/NavigationUsesLiveSceneAndInvalidatesConsistently.txt] Job fcb46aec đã chạy kiểm bố cục qua 1024×768, 1280×720, 1920×1080, 2560×1080 và 3440×1440 ở 100% và 125%, nhưng toàn job 0/1 tại assertion nhãn khoảng cách đường đi một phần. [CODE Assets/Tests/PlayMode/HudGameplayAcceptanceTests.cs:806] Job 75716ecc 0/1 tại kiểm cỡ chữ Eyebrow, trước khi tới các assertion tracker Preparing/Encounter; không tính hai job này là đạt.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Sau test và Save Gate: MainMenu isDirty=false, Edit Mode, Console 0 lỗi. Không import asset hoặc sửa object 3D.
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md — đối chiếu ảnh mẫu và xác minh bố cục; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — giữ neo góc và kiểm nhiều tỷ lệ; C:/Users/hieud/.agents/skills/unity-skills/skills/asset/SKILL.md — tìm sprite có sẵn; editor/SKILL.md, test/SKILL.md, debug/SKILL.md, console/SKILL.md, scene/SKILL.md, script/SKILL.md và scriptableobject/SKILL.md trong C:/Users/hieud/.agents/skills/unity-skills/skills/ — kiểm trạng thái, compile, test, khôi phục MainMenu mà không lưu scene Test Runner; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — thay đổi tập trung và kiểm thử. create-game-assets/SKILL.md KHÔNG TÌM THẤY.

## Đề xuất
- [SUY LUẬN] Tìm asset khung UI và icon Warden đúng mẫu trước khi hoàn thiện các màn còn lại; không dùng hình tủ hoặc thay object 3D.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Mục trước ghi tracker dùng font cô đọng là tình trạng trước cập nhật này; ảnh mới dùng LiberationSans, nhưng khung, kích thước và bố cục toàn HUD vẫn chưa giống hệt mẫu. Fixture đạt không thay thế playtest tự nhiên.

## Chưa kiểm được
- [SUY LUẬN] Chưa hoàn thành chín giao diện; chưa xác minh tracker Preparing/Encounter sau đổi font, playtest tự nhiên, client thực, gamepad và toàn bộ objective. Hai kiểm thử có assertion thất bại như ghi trên; chưa xác minh nguyên nhân gốc.

# Ba dòng điều khiển thiết bị - 03/11/2026
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:262] Cột điều khiển hiển thị tên hệ thống, Source và Action thành ba hàng, có hai đường phân cách dùng sprite sẵn có; hàng và nút được kiểm tra không chồng lấn trong fixture.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:480] Isolate dùng tiêu đề PRODUCTION BREAKER / INSTRUCTIONS và CONTROL / ISOLATE; footer khi khóa ghi CHECK REQUIREMENTS thay vì khẳng định sai nguồn tài liệu là điều kiện khóa.
- [PLAYTEST Reports/UIUX/MockupImplementation/Before/20261103-device-rows/device-locked-1920x1080.png] [PLAYTEST Reports/UIUX/MockupImplementation/Before/20261103-device-rows/device-locked-3840x2160.png] Ảnh trước; [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/device-locked-1920x1080.png] [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/device-locked-3840x2160.png] ảnh sau; [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/device-locked-1024x768.png] ảnh sau 4:3. Các ảnh là Backup mở trực tiếp bằng fixture, không phải hành trình chơi tự nhiên.
- [CODE Assets/Tests/PlayMode/HudGameplayAcceptanceTests.cs:284] Unity Test Runner job 5efe5140 hoàn thành 1/1; fixture kiểm ba hàng tại 1080p, 4K và 4:3, cuộn nguồn, Isolate giữ/hủy và debrief; sender/client và input trong fixture được mô phỏng.
- [SCENE Assets/FPS/Scenes/MainMenu.unity/isDirty] Sau test đã chạy Tools/AI Director/Save Authored Assets; MainMenu isDirty=false, Edit Mode, không compile, Console 0 lỗi. [CODE Assets/Editor/DirectorVerification/DirectorLiveProbe.cs:79] Lệnh bỏ qua scene Untitled, scene test và preview, gọi AssetDatabase.SaveAssets().
- [SUY LUẬN] Skill đã đọc: C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md cùng references/execution-contract.md và references/agent-runbook.md — đối chiếu mockup, kiểm thêm tỷ lệ và phân biệt fixture với playtest; C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md — kiểm bố cục theo tỷ lệ màn hình và trạng thái khóa; C:/Users/hieud/.agents/skills/unity-skills/SKILL.md cùng skills/editor/SKILL.md, skills/test/SKILL.md, skills/debug/SKILL.md — kiểm trạng thái Editor, kết quả test và Console; C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md — giới hạn thay đổi và kiểm thử liên quan. create-game-assets/SKILL.md: KHÔNG TÌM THẤY, không tuyên bố đã áp dụng.

## Đề xuất
- [SUY LUẬN] Cần sprite UI bezel phù hợp và icon đúng của Warden để tiếp tục đối chiếu với mẫu; không thay bằng hình tủ hay icon súng khác.

## Tự kiểm mâu thuẫn
- [PLAYTEST Reports/UIUX/AssetReplacement/Gameplay/device-locked-1920x1080.png] Ba hàng đã hiển thị nhưng bezel vẫn bị kéo dọc, bề mặt panel tối hơn và không giống mẫu; kiểm thử đạt không có nghĩa là giao diện đã giống mẫu hoặc được người dùng duyệt.

## Chưa kiểm được
- [SUY LUẬN] Chưa hoàn thành hoặc chứng minh đủ chín giao diện giống mockup; chưa có đủ ảnh trước/sau và playtest tự nhiên 1080p/4K cho toàn bộ màn, chưa kiểm client thực, gamepad hay mọi objective. Các job fcb46aec và 75716ecc đã thất bại như ghi ở phần trước và chưa được khẳng định đã sửa. Bezel và panel thiết bị còn là GREYBOX PLACEHOLDER; Warden chưa có icon.















