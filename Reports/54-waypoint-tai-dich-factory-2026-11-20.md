# Waypoint tại đích cho Power và Logistics — 20/11/2026

Người dùng duyệt [mockup 53](/E:/Unity/Project/FPS/Reports/53-mockup-waypoint-dich-factory-2026-11-20.md) bằng “Triển khai đi”. HUD đầu Factory liệt kê tên, tiến độ và bước hiện hành của cả Power lẫn Logistics. Khi hai đích nằm trong khung, người chơi thấy hai chấm trắng tại điểm tương tác thật. Bỏ `[T] SWITCH`, số mét theo tuyến và mũi tên mép khung ở giai đoạn này. Hai nhánh xong thì HUD quay về hướng dẫn hiện có của pha kế tiếp.

- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.FactoryDestinations.cs:64] Tracker lấy bước khả dụng từ `CampaignRules.FactoryNext` và tiêu đề từ catalog. Bước bị khóa không có mốc. Nhánh hoàn tất hiện `COMPLETE` và bỏ mốc nếu nhánh kia còn việc. Server vẫn quyết định tiến độ.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.FactoryDestinations.cs:87] Chiếu độc lập hai `TargetPosition` bằng camera người chơi. Mốc ngoài khung, sau lưng hoặc dưới phần HUD đang hiển thị sẽ ẩn, kể cả khi vẫn thấy tên nhánh trong tracker. Tường trong màn không che mốc. Khi hai mốc sát nhau, nhãn tách sau khi né HUD, giữ nguyên tâm chấm và tránh che chấm của nhánh kia. Vùng trống của prompt không che mốc. Nhãn thêm `HIGHER` hoặc `LOWER` từ chênh cao 2,5 m.
- [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:268] Chỉ dùng giao diện hai đích khi Factory đang khám phá và còn nhánh chưa xong. Tái dùng Canvas, tracker, font/theme và sprite `FPSSample/GoalMarkerCentre`. [CODE Assets/FPS/Features/Interaction/Runtime/InteractionManager.cs:63] Bỏ đổi nhánh Factory bằng `T`; giữ luồng theo dõi khóa ở các chapter khác.

[SCENE] `GameScene` dùng uGUI. Tracker nằm trên trái trong vùng HUD an toàn; hai mốc là hai đối tượng độc lập trong `HudSafeArea`. Không tạo asset hình ảnh mới hay sửa scene authored cho tính năng này.

[PLAYTEST screenshot] Đã xem ảnh Game View trước/sau ở 1920×1080 và 3840×2160, thêm 1440×1080 sau sửa. Bản trước chỉ nhấn mạnh một nhánh với `[T] SWITCH`; ảnh 4K thấy Logistics theo tuyến `267 m`. Bản sau cho thấy cả hai đích cùng lúc ở góc nhìn đầu Factory. Ảnh client Development 1080p cũng thấy hai tên đích và hai dòng tracker cùng lúc. Ảnh tạm được dọn sau khi hoàn tất rà soát; log kiểm thử được giữ lại.

Log thực thi ngày 20/11/2026 (thời gian UTC từ Unity Test Runner):

```text
360d677c  Baseline, Editor: 1/1 passed.
199574bd  FPS.Tests.CampaignWaypointPlayModeTests.VerifyFactoryWaypointAfterFix
NUnit XML: Passed; duration=56.551325; end=2026-11-20 10:17:17Z
[WaypointQA] 1920x1080 real destinations: Power (102.87, 1.56, 14.57) -> (994.1, 676.2); Logistics (100.30, 2.50, -77.46) -> (1321.2, 684.9)
[WaypointQA] 3840x2160 real destinations: Power (102.87, 1.56, 14.57) -> (1988.2, 1352.3); Logistics (100.30, 2.50, -77.46) -> (2642.4, 1369.9)
[WaypointQA] 1440x1080 real destinations: Power (102.87, 1.56, 14.57) -> (707.6, 676.7); Logistics (100.30, 2.50, -77.46) -> (1028.8, 684.3)
[WaypointQA] Coincident destinations: both dots retain the same projection, with labels on opposite sides.
[WaypointQA] Lower screen: labels split after HUD avoidance; empty prompt band leaves the dot visible, active prompt hides it.
[WaypointQA] Staggered destinations: separated labels avoid the other branch's dot.
[WaypointQA] Server progress: Isolate -> Backup; Manifest -> Shipping; Power -> COMPLETE. Each update changed only its own destination.
[WaypointQA] Unresolved Tank reference: action preserved, no boarding waypoint or distance.

Build Peer Client (Development): Succeeded; errors=0; warnings=22; seconds=22.
026f0330  FPS.Tests.CampaignWaypointPlayModeTests.VerifyFactoryWaypointOnClient
NUnit XML: Passed; duration=71.020924; end=2026-11-20 10:19:15Z
[WaypointQA] Remote client 1, server=False, peers=2, feet=(-124.00, 0.08, -137.00): PASS Power and Logistics use real target projections, hide offscreen/behind and return unchanged; both visible together; replicated state unchanged.
```

Bài host dùng hai đích thật để kiểm tra hình chiếu khi lia camera. Các ca chồng nhãn, cao độ, prompt và cập nhật tiến độ dùng dữ liệu tạm trong Play Mode. Sau rà soát độc lập, đã thêm kiểm tra khi một đích sát prompt và khi nhãn lệch hàng có thể che chấm của nhánh kia. Bài client xác nhận hành vi qua tiến trình game riêng kết nối với host, dùng trạng thái nhận từ server.

[SCENE] Đã chạy `Save Authored Scenes` trước và sau mỗi bài Play Mode và lần build. Kết thúc ở Edit Mode, `GameScene.isDirty=false`. SHA-256 scene `3F27141F…4A0D33D`, checkpoint `97370558…E6B688A` và bản `.previous` `57B13F37…057F8D` khớp trước thử nghiệm. Không còn file checkpoint `.tmp`.

Skill đã đọc và áp dụng: `unity-mcp-ui-layout` (tái dùng tài nguyên uGUI và kiểm tra bố cục theo mockup đã duyệt); `game-ui-ux` (neo tracker trong vùng an toàn, cập nhật nội dung theo trạng thái); `level-design` (giữ điều kiện mở bước, kiểm tra góc nhìn đầu Factory); `unity-skills` và `unity-test` (lưu scene authored, chụp Game View và chạy Play Mode theo tên).

[INFERENCE] Hai mốc cho thấy vị trí đích nhưng không nói cửa hoặc cầu thang nào dẫn tới đó. Chưa quan sát người chơi mới tự tìm đường từ Decon đến cả hai mục tiêu.
