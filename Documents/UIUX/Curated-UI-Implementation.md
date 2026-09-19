# UI đã lọc và tích hợp — 14/09/2026

Đã áp dụng hướng **kinh dị sinh tồn, thô và u ám** vào MainMenu, Settings, lobby, menu trong trận và HUD. Menu chính giữ tên **OUTBREAK PROTOCOL** và điều hướng; không thêm slogan hay nhãn phase trận đấu.

[Ảnh tổng quan](UI-Curated-Overview.jpg) · [Asset được chọn](curated-art-preview.jpg) · [Danh sách nguồn và SHA-256](curated-assets.json)

Rà soát chi tiết sau tích hợp: [UI-Detailed-Review-2026-09-14.md](UI-Detailed-Review-2026-09-14.md) · [ảnh chi tiết](UI-review-details.jpg).

## Tổ chức asset

Chọn **17 PNG, tổng 407.503 byte trên đĩa** vào `Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/`. Bộ icon chiến đấu/vật phẩm bổ sung được ghi riêng trong `Combat-Icon-Selection.md`; đây là dung lượng file nguồn, không phải bộ nhớ texture khi chạy game.

| Thư mục | Số sprite | Nguồn và chức năng |
|---|---:|---|
| Surfaces | 2 | SunGraphica: panel có texture và lớp xước nhẹ |
| Controls | 2 | SunGraphica: nền control và trạng thái chọn menu |
| Bars | 3 | Lotus Garden: frame, fill và track thanh máu |
| Icons | 9 | SunGraphica: check, back, settings, audio, controls, lock; ProjectSettingsPack: HP, biohazard, grenade |
| Effects | 1 | ProjectSettingsPack: viền máu khi HP thấp |

Các sprite HP/biohazard/lựu đạn/viền máu tận dụng nhóm asset người dùng tại `E:/ProjectSettings/Assets`: `DataIcons_HP`, `MissionIcons_biohazard`, `FragGrenade`, `BloodBorderTB` đã được đối chiếu byte với bản Unity tương ứng (bảng [local-assets-verified.json](local-assets-verified.json)). Tôi dùng bản đã nhập trong `Assets/ThirdParty` để Unity giữ GUID/import ổn định, rồi cắt đúng rect atlas thành PNG Curated; không copy lại file ngoài project để tránh tạo asset trùng. Sprite được loại padding và chỉnh màu để cùng bảng màu xám than, trắng ngà, đỏ trầm. Panel kín nền giúp cảnh phía sau không cạnh tranh với chữ; thanh máu có fill riêng để hiển thị đúng phần trăm.

Các asset khác trong folder cũng đã được xem: `TextFrameLeft/Right` và `WhiteFrameThin` phù hợp làm khung phụ; `MenuButtonSelected1` và `enemyHealthBarBackground` thiên sci-fi/xanh nên không ghép vào bộ horror hiện tại. Chúng không bị xóa.

ZIP, PSD, biến thể màu/kích cỡ và các phần chưa chọn được giữ tại [FreeAssets](FreeAssets/README.md), ngoài Assets. Gamanbit thiên fantasy nên chỉ giữ tham khảo. Không xóa các sprite cũ còn có thể được scene/prefab khác tham chiếu.

Điều khoản nguồn được giữ trong `Curated/Licenses`; Settings có credit **UI art: SunGraphica / lotus_garden**. Nguồn tải: [Lotus Garden](https://lotus-garden.itch.io/post-apocalypse-survival-ui-asset-pack), [SunGraphica FREE](https://sungraphica.itch.io/creepy-game-ui). Tất cả gói tải mới dùng ở đây là bản miễn phí.

## Phần đã triển khai

- Cả ba scene `MainMenu.unity`, `LobbyScene.unity`, `GameScene.unity` và hai prefab `SettingsPanel.prefab`, `PlayerEntry_Tactical.prefab` dùng asset đã chọn.
- Nền bảng, input, dropdown, slider, nút và focus menu dùng chung texture/palette. Settings có icon cho tab và Back; tăng chỗ cho chữ Back, giảm lớp nền lồng nhau.
- HUD có HP, biohazard, lựu đạn thật từ bộ asset; thanh máu Lotus tách frame/fill/track; số đạn dự trữ dùng `/ 120`; viền máu nối vào cảnh báo HP thấp hiện có. Đã xử lý khoảng cách icon nhiễm bệnh, bỏ nhãn FRAG bị cắt và separator đạn trùng.
- Giữ UGUI/TextMeshPro và controller dữ liệu đang dùng. `SurvivalUiArt` cấu hình import trong Editor; reference sprite được lưu trong scene/prefab, không tìm asset trong runtime.

Lệnh authoring: **Tools > FPS > UI > Apply Survival Horror Design**. Chỉ chạy khi muốn áp dụng lại toàn bộ layout đã định nghĩa; lệnh có thể ghi đè chỉnh sửa layout thủ công ở các màn hình này. Script chuẩn bị ảnh: [prepare_survival_assets.py](Tools/prepare_survival_assets.py); lưu scene/assets trước khi chạy lại vì script ghi lại 17 PNG dẫn xuất.

## Icon chiến đấu và vật phẩm sinh tồn

Các icon 5 vũ khí đang dùng cùng grenade, medkit và lọ giảm infection đã được bổ sung từ asset local trong `E:/ProjectSettings/Assets`. Chi tiết mapping, hash và preview: [Combat-Icon-Selection.md](Combat-Icon-Selection.md), [curated-combat-items.json](curated-combat-items.json), [preview](combat-icons-preview.png).

## Kiểm chứng

- `FPS.Tests.SurvivalUiRegressionTests`: **6/6 passed**, job `d418e3e9`; [kết quả](curated-test-results.json). Kiểm tra hủy rebind, modal, bounds/overlap hàng binding, tên roster dài, contrast và không hiện phase label.
- **13 tổ hợp màn hình/kích cỡ**: MainMenu main/play/audio/graphics/controls, lobby, HUD, pause tại 1920×1080; Controls 1280×720; Play/lobby/HUD 1024×768; MainMenu 2560×1080. Báo cáo đi kèm ảnh đều PASS cho text overflow và control bounds. Đã xem trực quan các màn hình chính; đây không phải phép kiểm tra mọi kiểu chồng lấn.
- Runtime MainMenu: chọn Settings bằng EventSystem/pointer, marker đạt alpha 1, mở Controls và Back khôi phục tương tác menu. [Ảnh Play Mode](runtime-curated-settings.png).
- Bản sao HUD gọi logic thật với HP 25/100 và infection 78%: fill 0,25, bật viền máu, icon biohazard tách khỏi chữ; HP 100 tắt viền máu. [Ảnh HUD cảnh báo](GameScene-hud-danger-1920x1080.png). Probe này không bao phủ toàn bộ nhãn SepsisWarning.
- Đã lưu sau kiểm thử và khôi phục MainMenu. Kiểm tra live cuối cùng xác nhận chỉ MainMenu đang mở, Edit Mode, Console có 0 lỗi và scene `isDirty == false`; đã gọi SaveOpenScenes và SaveAssets sau bước export.

Ảnh `MainMenu-*`, `LobbyScene-*`, `GameScene-*` là Canvas render trên bản sao trong PreviewScene; dữ liệu lobby/HUD là trạng thái mẫu, không phải ảnh một trận online. Chưa kiểm thử một trận chiến đấu đầy đủ, Relay nhiều client hoặc gamepad vật lý. Không suy rộng kết quả sang mọi trạng thái động.

Nghiên cứu UI/UX trước triển khai và các bài tham khảo nằm trong [UIUX-Review.md](UIUX-Review.md); so sánh bộ asset nằm trong [HUD-Asset-Shortlist.md](HUD-Asset-Shortlist.md).

Index C# đã refresh; tên project trả về là `E-Unity-Project-FPS`, truy vấn thấy class `SurvivalUiArt`. Index có 19 file parse-partial, nên vẫn chỉ dùng để điều hướng. Unity semantic graph đã được export bằng menu Editor và chứa reference đến bộ Curated; không chỉnh JSON bằng tay. Lệnh export vượt thời gian chờ của công cụ, nhưng file đầu ra mới đã được đọc kiểm tra và Editor đã phản hồi bình thường; save gate được chạy lại thành công sau đó.
