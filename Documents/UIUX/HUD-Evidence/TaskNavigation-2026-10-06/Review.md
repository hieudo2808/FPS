# Thanh nhiệm vụ và chỉ dẫn — 2026-10-06

Đã sửa MissionTracker trong `CampaignHUD.Navigation.cs`: padding ngang 32, dọc 24 đơn vị Canvas; nền Image và viền Outline mảnh 2 đơn vị. Bản đầy đủ 468×196, bản thu gọn khi ngắm 468×80. Kiểm tra chữ không bị cắt và không chồng các nhóm HUD khác.

Chỉ dẫn bỏ dịch chuyển 160 đơn vị khi người chơi ngắm vào mục tiêu. Hướng mũi tên được tính từ mục tiêu trước khi dịch nhãn tránh HUD. Phép chiếu dùng clip X/Y để giữ hướng khi mục tiêu ở ngang hoặc sau camera. Đường dẫn bỏ điểm bắt đầu đã sample, theo điểm rẽ trung gian ổn định bất kể vật che camera, và giữ tiến độ qua điểm rẽ trong mỗi đường dẫn được cache.

## Kiểm chứng trong Unity 6000.5.6f1

| Kiểm thử | Kết quả | Job |
| --- | --- | --- |
| `HudRegressionTests.ProjectionHandlesBehindCameraAndAvoidsOccupiedCorner` (EditMode) | 1/1 passed | `b00d07eb` |
| `HudGameplayAcceptanceTests.NavigationUsesLiveSceneAndInvalidatesConsistently` (PlayMode) | 1/1 passed | `1730693d` |
| `HudGameplayAcceptanceTests.GameplayStatesAndLayoutRemainReadable` (PlayMode) | 1/1 passed | `8e93c789` |

- Quét 1.296 tư thế camera: yaw mỗi 5°, pitch −65°/0°/65°, tại 1280×720, 1920×1080, 3440×1440 và scale HUD 100%/125%.
- Xác nhận nhãn mét không có panel, giữ trọn trong safe area; mục tiêu ở tâm ngắm không nhảy sang bên; mũi tên giữ hướng thật sau khi tránh HUD.
- Xác nhận bỏ sampled start, vật che không làm đổi điểm rẽ, đi qua điểm rẽ không bị quay lại giữa các lần tính đường.
- Kiểm tra đường NavMesh và cổng trong GameScene; đường không hoàn chỉnh, đích khác tầng, camera/đích mất, đổi chapter và rollback checkpoint vẫn được xử lý.
- Kiểm tra padding, chiều cao chữ, khoảng cách với squad/hotbar và chế độ ngắm; kiểm tra các trạng thái chuẩn bị, countdown, downed, spectating, cinematic và debrief.
- Biên dịch hoàn tất; Console 0 lỗi; Editor đã dừng; GameScene/MainMenu đều sạch. MainMenu SHA256 giữ nguyên: `6F7123A0845A59AFD92156FBDFE8F02BD052994AB26474FAE04F1718FD258534`.

Chi tiết kết quả ở [Validation.json](Validation.json) và hai receipt `.txt` trong thư mục này. Lượt navigation đầu tiên dừng vì test tìm mũi tên đang ẩn; fixture đã được sửa và lượt cuối qua toàn bộ. Input chuột kiểm thử dùng `InputState.Change` để không phụ thuộc việc Editor nhận sự kiện chuột đang queue.

## Ảnh đã xem

- `task-preview.png`: crop từ screenshot thật của bản đầy đủ và ADS, không dựng lại giao diện.
- `stress-1280x720-125.png`: tracker đầy đủ với squad và các thông báo gameplay.
- `ads.png`: tracker thu gọn khi ngắm.
- `tracker-marker-3440x1440-125.png`: tracker ở ultrawide.
- `navigation-next-turn.png`: chỉ dẫn theo điểm rẽ; khối xám là vật che tạm của test.

Phạm vi: quét góc quay dùng mục tiêu/đường dẫn tạm để giữ điều kiện xác định trong GameScene; các kiểm tra NavMesh/cổng dùng scene thật. Đây không phải xác nhận mọi đường đi trong cả ba chapter. Các screenshot `tracker-marker-*` cố ý bật fallback “Route unavailable” trong fixture phép chiếu.
