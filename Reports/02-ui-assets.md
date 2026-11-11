# Audit UI và asset — tạm thời, chưa hoàn tất

## Phạm vi và trạng thái
- [SCENE Assets/FPS/Scenes/GameScene.unity/isDirty] Unity-Skills báo `GameScene` đang mở và `isDirty == false`; chỉ truy vấn, không lưu hay đổi scene.
- [SUY LUẬN] Trước Play Mode, Save Gate của dự án còn yêu cầu `AssetDatabase.SaveAssets()`. Thao tác này có thể ghi asset đang chờ lưu và mâu thuẫn yêu cầu không sửa asset; chưa vào Play Mode, chưa lưu.
- [SUY LUẬN] Unity-Skills `scene_screenshot` chụp Game view cùng UI nhưng ghi vào `Assets/Screenshots/`, không phù hợp nơi lưu ảnh. `mcp__cua_repl` có thể chụp cửa sổ Unity; chưa xác minh độ phân giải.
- [SUY LUẬN] Git có nhiều thay đổi có sẵn, gồm `GameScene.unity`, `CampaignHUD.cs`, `HUDManager.cs`; ảnh cung cấp không chứng minh runtime hiện tại.

## Skill đã đọc
- [SUY LUẬN] `C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md`: đối chiếu anchor, CanvasScaler, safe area và polling.
- [SUY LUẬN] `C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md`: phân biệt ảnh tham chiếu và xác minh thực tế, kiểm tra tài nguyên sẵn có.
- [SUY LUẬN] `C:/Users/hieud/.agents/skills/game-feel/SKILL.md`: giới hạn nhận xét phản hồi thị giác; chưa xác minh hiệu ứng runtime.
- [SUY LUẬN] `C:/Users/hieud/.agents/skills/unity-skills/SKILL.md`: truy vấn scene chỉ đọc trước thao tác Editor.
- [SUY LUẬN] `C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md`: giữ phạm vi, không sửa code hay asset.
- [SUY LUẬN] `create-game-assets/SKILL.md`: KHÔNG TÌM THẤY trong hai thư mục skill đã kiểm tra; không khẳng định đã đọc hay áp dụng. Codebase Memory MCP không công bố resource/template trong phiên.

## Bước 0 — ảnh người dùng gửi
- [SUY LUẬN] Ảnh `C:/Users/hieud/AppData/Local/Temp/codex-clipboard-d1ecc882-e61b-4256-9832-4c15bdf7c309.png`: trên trái bảng tối viền xám, `FACTORY` cam, `Read the shipping manifest`, `POWER 0/3 · LOGISTICS 0/2 [T]`, `switch branch` trắng.
- [SUY LUẬN] Giữa ảnh là chữ cam phản chiếu `POWER / ISOLATE`, tâm ngắm trắng trên cửa kim loại; container xanh trái, thùng xanh và súng bên phải.
- [SUY LUẬN] Dưới trái là `INFECTION 0%`, `100 / 100` và thanh sáng trong hai panel viền kim loại. Dưới giữa đọc được `Read the Delivery Manifest at the labeled logistics crate; its fields are`; thời lượng chưa biết.
- [SUY LUẬN] Bên phải có tam giác vàng và `144m ROUTE`; dưới phải có bốn ô đồ vật, `WARDEN`, `15 / 45`, `FIRE`, `AIM`, `2`, `CLASSIC`. Không thấy prompt khóa hay vòng giữ F trong ảnh tĩnh.

## A. HUD — xác minh một phần
| Thành phần | Bằng chứng | Còn thiếu |
|---|---|---|
| Tracker | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:98] `MissionTracker` góc trên trái, `HudSkin/Panel`, font 24, viền hard-code dòng 103; hai nhánh và phím đổi dòng 169–171. | Runtime hiện tại. |
| Subtitle | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:152] Canvas phụ, `HudSkin/Panel`, font 26, shadow đen; cập nhật `Update` dòng 354; thời lượng tối thiểu 6 giây dòng 462. | Thời lượng trong ảnh. |
| Marker | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:116] Sprite `FPSSample/GoalMarkerCentre`/`GoalMarker`; safe area dòng 274; biểu tượng hướng khi `edge` dòng 315. Chuỗi dòng 317–319 chỉ có khoảng cách và `ROUTE`/`TO TARGET`, không thêm tên mục tiêu. | Hình ngoài màn hình và 1080p/4K. |
| Tâm ngắm | [CODE Assets/FPS/Features/UI/Runtime/HUDManager.cs:826] Có logic ADS, màu hard-code dòng 869. | Sprite/anchor tâm ngắm thường. |
| Prompt | [CODE Assets/FPS/Features/Interaction/Runtime/InteractionManager.cs:54] `Update` xử lý mục tiêu; [CODE Assets/FPS/Features/UI/Runtime/HUDManager.cs:635] xử lý text và icon khóa. | Lý do khóa thực tế. |
| Máu | [CODE Assets/FPS/Features/UI/Runtime/HUDManager.cs:677] Cập nhật màu chữ/thanh. | Sprite, anchor, tín hiệu. |
| Infection | [SUY LUẬN] Ảnh có panel riêng; chuẩn dự án yêu cầu gộp thanh. | Script và thiết kế thể hiện hai đại lượng. |
| Đạn/vũ khí | [CODE Assets/FPS/Features/UI/Runtime/HUDManager.cs:580] `FIRE`/`AIM` theo binding dòng 592–611. | Anchor, điều kiện ẩn và tính dư thừa theo giai đoạn. |
| Hotbar | [SUY LUẬN] Ảnh có bốn ô. | Script, asset, cập nhật. |
| Journal/đối chiếu | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:161] `JournalAndDocument` dùng `HudSkin/Panel`; journal dòng 287, lựa chọn dòng 270. | Trạng thái runtime. |
| Giữ F | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:147] Vòng `FPSSample/GoalProgress`, `Radial360`, 64×64; dòng 398–400 ghi `HOLD {phím Interact} TO CONFIRM {phần trăm}` và bật vòng; dòng 309–310 xóa khi hủy. | Chưa playtest giữ/thả. |

- [CODE Assets/FPS/Features/UI/Runtime/HudLayout.cs:20] Canvas campaign dùng `ScaleWithScreenSize`, `TacticalUiTheme.ReferenceResolution`, match 1; root có `UiSafeArea` dòng 29–36. Chưa xác nhận toàn HUD dùng chung root.
- [CODE Assets/FPS/Features/UI/Runtime/HudSkin.cs:13] `HudSkin` và [CODE Assets/FPS/Features/UI/Runtime/TacticalUiTheme.cs:9] `TacticalUiTheme` gom một phần màu/font; vẫn có giá trị hard-code, không phải một theme asset duy nhất.

## B. Chữ lật
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:96] `EnsurePresentation` tìm `TextMeshPro` con hoặc tạo `CampaignWorldLabel` text 3D tại `TargetPosition + Vector3.up * 1.25f`.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:141] `FactoryIsolate` có `POWER / ISOLATE`; dòng 133–137 cập nhật vị trí và xoay theo camera. Chưa chứng minh đó là decal trên bề mặt.
- [SUY LUẬN] Chưa kiểm tra transform, mặt hiển thị hoặc shader runtime nên chưa kết luận nguyên nhân lật hay số lượng nhãn.

## C. Asset thật/giả
- [SCENE CampaignWorld/Factory/Map_v2/ColdLedger_Extension/Gameplay/MissionController/Objectives] Có `OBJ_UtilitiesBreakerWest`, `OBJ_UtilitiesBreakerEast`, `OBJ_UtilitiesGenerator`, `OBJ_LogisticsManifest`, `OBJ_LogisticsSecurityOverride`, `OBJ_ExtractionRadio`. Các root có `BoxCollider` và `CampaignInteractable`, không có renderer trên chính root; điều này không chứng minh không có hình proxy bên ngoài root.
- [SCENE CampaignWorld/Factory/Map_v2/ColdLedger_Extension/Gameplay/MissionController/Objectives/OBJ_ColdStorageSample/EvidenceCase_Factory/MeshFilter.m_Mesh] Mesh tên `EvidenceCase_hard_case_2`; renderer có `Mat Varnish_Plastic Black` và `Shiny Varnish_Plastic Gray Dark`. Chưa xác định đường dẫn asset và texture, chưa phân loại.
- [SCENE CampaignWorld/Laboratory/CampaignObjectives/LabCase/EvidenceCase_Lab/MeshFilter.m_Mesh] Cùng mesh và hai material với case Factory; chưa xác định đường dẫn, texture hay phân loại.
- [SUY LUẬN] Chưa truy xuất mesh/material từng vật, prefab và vật thay thế; chưa đủ bằng chứng phân loại THẬT/GIẢ hoặc kết luận không có asset phù hợp. Chưa xác minh ba hộp v1/v2/v3, pickup, outline, Tank/zombie/đồng đội.

## D. Kho UI
- [SCENE AssetDatabase/t:Sprite] Trả `totalFound=1124`, chỉ xem 25 đầu; chưa liệt kê đầy đủ hoặc xác định sprite chưa dùng. Ví dụ `Assets/FPS/Features/Survival/Content/Textures/ProgressRing.png` và `Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/GoalMarker.png`.
- [SCENE AssetDatabase/t:TMP_FontAsset] Có `Assets/ThirdParty/UIKit/Fonts/Xolonium-Bold SDF.asset`, `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` và các font trong `Assets/FPS/Features/UI/Content/Resources/HudSkin/Fonts/`; chưa xác định font chưa dùng.
- [SUY LUẬN] Chưa xem đủ khung/atlas để xác nhận nhóm kim loại xước.

## E. Ảnh
- [SUY LUẬN] KHÔNG CÓ ảnh Play Mode mới ở `Reports/shots/`. Chưa chụp các trạng thái yêu cầu ở 1080p/4K; chưa teleport, tắt zombie hay chạy local host.

## F. Ưu tiên tạm thời
1. [SUY LUẬN] `POWER / ISOLATE` phản chiếu trong ảnh; nguyên nhân chưa xác minh.
2. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:317] Chuỗi marker thiếu tên mục tiêu.
3. [CODE Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:111] Có text 3D trên mục tiêu; cần kiểm chứng cách gắn từng nhãn.
4. [SUY LUẬN] Ảnh tách máu và infection thành hai panel, khác chuẩn dự án; chưa có thiết kế được duyệt.
5. [CODE Assets/FPS/Features/UI/Runtime/HudSkin.cs:13] Màu/cỡ không chỉ nằm trong một theme asset; chưa thống kê hết.
- [SUY LUẬN] Chưa đủ bằng chứng xếp đủ 10 điểm.

## Đề xuất
- [SUY LUẬN] Làm rõ quyền gọi `AssetDatabase.SaveAssets()` trước/sau Play Mode: lệnh có thể ghi những asset đang chờ lưu; không lưu scene theo chỉ dẫn hiện tại.

## Tự kiểm mâu thuẫn
- [SUY LUẬN] Code có vòng giữ không đồng nghĩa đã xác nhận vòng hiện đúng trong Play Mode.
- [SUY LUẬN] Scene `isDirty == false` không chứng minh asset không chờ lưu hoặc Git sạch.
- [SUY LUẬN] Ảnh người dùng là tham chiếu, không gắn nhãn `[PLAYTEST]` cho phiên này.

## Chưa kiểm được
- [SUY LUẬN] Play Mode/local host, scene sau play, ảnh yêu cầu, âm thanh, timing thực tế, hierarchy nhãn runtime, mesh/material/texture từng vật, kho UI đầy đủ và top 10.
- [SUY LUẬN] Thiếu skill `create-game-assets`; chưa có Codebase Memory MCP trong danh sách resource/template.
