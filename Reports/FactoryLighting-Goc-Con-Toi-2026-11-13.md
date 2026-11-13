# Hai góc Factory còn tối để người dùng đánh giá

Ảnh gốc của lần playtest ngày 13/11/2026 lúc khoảng 00:58, quality Low, 1920×1080; không tăng sáng hoặc chỉnh màu. Đây là bằng chứng đã lưu, không phải ảnh chụp mới trong phiên này.

## Góc 01 — hangar phía tây, sát mặt sau container

[PLAYTEST screenshot] Mặt container chiếm phần lớn khung hình, chữ và gân vỏ còn khó thấy. Theo `FactoryLighting-2026-11-12/playtest-luminance.csv`, nearBlackFraction = 0.6432, meanLuminance = 0.0626.

![Góc 01 — hangar phía tây](FactoryLighting-2026-11-12/playtest-01-1920x1080.png)

## Góc 05 — hangar phía đông, ngăn quây container

[PLAYTEST screenshot] Vách bên phải đã nhìn rõ hơn; góc phía trái, thùng gỗ và phần sàn khuất vẫn rất tối. Số liệu đã lưu: nearBlackFraction = 0.3821, meanLuminance = 0.1118.

![Góc 05 — hangar phía đông](FactoryLighting-2026-11-12/playtest-05-1920x1080.png)

## Giới hạn đánh giá

[INFERENCE] Góc 01 nhìn rất sát một bề mặt khuất sáng; tỷ lệ điểm ảnh tối ở góc này không đại diện độ sáng của cả hangar. Hai ảnh giúp đánh giá chi tiết nhìn thấy ở các góc khuất, chưa đủ kết luận khả năng nhận diện mục tiêu khi giao chiến.

[SCENE] Truy vấn Unity-Skills `scene_get_loaded` trong phiên này trả về GameScene, đường dẫn `Assets/FPS/Scenes/GameScene.unity`, `isDirty == false`. Không thực hiện thao tác sửa scene, bake hoặc chạy test.

## Skill đã đọc và quy tắc áp dụng

- `unity-skills`: GET `/health`, GET `/skills/meta`, lấy schema trước khi gọi; dùng truy vấn Editor cho trạng thái scene.
- `unity-scene`: dùng `scene_get_loaded` để kiểm tra `isDirty`.
- `ponytail`: dùng lại ảnh và số liệu hiện có; không thêm công cụ hoặc mã nguồn cho việc xem ảnh.

Chưa chạy playtest mới và chưa kiểm tra quality khác Low. Không thay đổi ánh sáng trong phiên này.
