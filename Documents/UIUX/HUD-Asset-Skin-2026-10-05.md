# HUD và Inventory — asset skin, 05/10/2026

Yêu cầu: cải thiện phần hình ảnh của HUD và Inventory bằng asset, giữ hành vi HUD-01–HUD-10 đã triển khai.

## Asset được dùng

- **Post Apocalypse Survival UI — lotus_garden:** khung dialogue bằng kim loại gỉ, board Inventory, slot thường/chọn, nút thường/chọn, thanh fill và icon chìa khóa.
- **Creepy Game UI FREE — SunGraphica:** icon nhiệm vụ, tài liệu, hoàn thành, khóa, điều hướng và hết đồ.
- **Artwork có sẵn trong dự án:** frag, incendiary, medkit và antidote; cắt phần alpha trống để icon chiếm đúng vùng hiển thị.

18 PNG đã chọn, khoảng 0,97 MB trên đĩa. Không tạo texture hay hình UI mới bằng code; chỉ cắt alpha, trung hòa màu của một số icon và cấu hình 9-slice. Runtime dùng cùng sprite với authoring. Nền dim toàn màn hình và text vẫn là UGUI/TMP.

Nguồn từng sprite, crop/treatment, SHA-256 và border: [hud-skin-assets.json](hud-skin-assets.json). Bộ nguồn và điều khoản cho phép dùng/chỉnh sửa trong game: [FreeAssets/README.md](FreeAssets/README.md). Credit: `Post Apocalypse Survival UI Asset Pack — lotus_garden`, `Creepy Game UI FREE — SunGraphica`.

## Thay đổi

- Tracker, vitals, ammo và journal/document dùng khung asset; squad dùng nền button cùng bộ.
- Bốn slot đồ dùng nhanh dùng slot texture, viền chọn bằng sprite, icon lớn hơn, count có outline; giữ binding, hết đồ và progress.
- Inventory dùng board, khung list/detail, tab/button có texture và icon trạng thái. Có fallback icon cho item/file không có ảnh.
- HP phân cấp số hiện tại/tối đa; ammo trong băng nổi bật hơn.
- Marker đích và chỉ hướng dùng icon asset; giữ phép chiếu, NavMesh và quy tắc ưu tiên tương tác.
- Chỉ áp dụng authoring vào GameScene bằng `Apply Gameplay HUD Only`, với save gate và Undo.

## Kiểm chứng

- [x] Compile C#: 0 errors, 54 warnings (số warnings bằng baseline).
- [x] Đọc lại Image/RectTransform: sprite `Panel`, type `Sliced`, có border; anchor và kích thước HUD giữ đúng.
- [x] `SurvivalUiRegressionTests`: 7/7 PASS, XML tại `HUD-Evidence/AssetSkin/SurvivalUiRegressionTests.xml`.
- [x] Gameplay/Inventory ở 720p, 1080p, ultrawide và scale 125%; text không truncate/overflow và các cụm HUD không overlap trong fixture.
- [x] Tracking/navigation/input: 15 test riêng biệt PASS tổng cộng. Receipt và tổng hợp tại `HUD-Evidence/AssetSkin/verification-summary.json`.
- [x] Console lượt gameplay cuối: 0 errors. Compile cuối: 0 errors / 54 warnings. Editor trả về Edit Mode, MainMenu và GameScene đều clean; receipt tại `console-final.json`, `compile-final.json`, `editor-final.json`.

Ảnh trước: `HUD-Evidence/AssetSkin/before-user.png`, `before-missions.png`. Ảnh gameplay mới: `gameplay-after.png`, `inventory-after.png` (palette cuối); các ảnh `stress-*` và `missions-*` ghi ma trận resolution/scale trước lần giảm nhẹ tint nền nút. Tab Key Items và Files đã kiểm tra/chụp trạng thái rỗng tại `key-items-empty.png`, `files-empty.png`; chưa coi đây là nghiệm thu mọi tài liệu dài. Ảnh authored chỉ là preview UI, không thay bằng chứng gameplay.

Màu nền nút đã giảm nhẹ để kiểm tra texture nguồn cho contrast chữ phụ tối thiểu khoảng 4,60:1 trong phần ruột nút; panel khoảng 5,77:1 và nút chọn khoảng 4,86:1. Phép đo RGB nguồn/tint loại trừ khung, không phải chứng nhận mọi pixel sau render; hình gameplay cũng đã được xem trực tiếp.

Một lượt gameplay đầu thất bại vì coroutine realtime đọc hint trước frame HUD cập nhật expiry. Test đã thêm một frame đồng bộ rồi chạy lại PASS; không thay thời hạn hint hay logic gameplay. Các lượt Test Runner mất kết nối/không khởi chạy được không được tính là test đạt. Không nghiệm thu thêm multiplayer nhiều client hoặc controller vật lý trong lượt skin này.
