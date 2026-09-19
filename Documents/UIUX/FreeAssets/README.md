# Free UI/HUD assets — survival horror FPS

**Cập nhật 14/09/2026:** đã lọc và tích hợp 17 sprite vào menu, Settings, lobby và HUD. Xem [kết quả triển khai](../Curated-UI-Implementation.md). Thư mục này giữ bộ nguồn; runtime dùng bản chọn tại `Assets/FPS/Features/UI/Content/Sprites/Survival/Curated/`.

Tôi đã tải và kiểm tra trực tiếp các bản miễn phí từ itch.io. ZIP được kiểm tra lỗi (`testzip == None`), đường dẫn giải nén được kiểm tra chống path traversal, và không chạy file thực thi hay script nào trong archive.

## Bộ nên dùng trước

**Lotus Garden — Post Apocalypse Survival UI Asset Pack** là lựa chọn nền cho HUD/gameplay của FPS này: có thanh máu tách riêng frame/fill/background, 2 hotbar, inventory, popup 9-slice, dialogue kim loại gỉ, nút và checkbox. Hình minh họa dùng vàng cảnh báo và kim loại sáng; nên giữ một màu nhấn đỏ máu/xám than khi đưa vào game để tránh cảm giác sci-fi nhiều màu.

**SunGraphica — Horror Game UI Creator Kit (FREE)** hợp cho menu, bảng inventory và icon u ám: texture gỗ/kim loại sẫm, 18 PNG UI và 78 mẫu icon (nhiều kích cỡ/màu), kèm 4 PSD. Đây là bản FREE; archive 418 MB của tác giả là bản trả phí và không được tải trong lượt này.

Gamanbit chỉ có 4 ảnh HUD mẫu, có chất vẽ tay fantasy; giữ làm nguồn tham khảo phụ, không dùng làm bộ chính.

## File đã tải

| Bộ | ZIP | Giải nén | Nội dung thực tế | SHA-256 |
|---|---|---|---|---|
| Lotus Garden | `E:\Unity\Project\FPS\Documents\UIUX\FreeAssets\Lotus-Post-Apocalypse-UI.zip` (14.97 MB) | `E:\Unity\Project\FPS\Documents\UIUX\FreeAssets\Lotus-Post-Apocalypse-UI` | 117 PNG + 89 PSD | `4135224bcddd5af49b6e4ed56c71d981ae9cde00a24d40882fa196889fdc2f0d` |
| SunGraphica FREE | `E:\Unity\Project\FPS\Documents\UIUX\FreeAssets\SunGraphica-Creepy-UI-FREE.zip` (71.95 MB) | `E:\Unity\Project\FPS\Documents\UIUX\FreeAssets\SunGraphica-Creepy-UI-FREE` | 417 PNG + 4 PSD | `b215f0c2549856f6efa1d3959e7c5b7934aac5b2ffc113725645a986947857be` |
| Gamanbit | `E:\Unity\Project\FPS\Documents\UIUX\FreeAssets\Gamanbit-Dark-Survival-HUD.zip` (1.72 MB) | `E:\Unity\Project\FPS\Documents\UIUX\FreeAssets\Gamanbit-Dark-Survival-HUD` | 4 PNG + license/readme | `777d95d5db80ef0ac2240a72b889c9de41716224c49caa2408f6c8f821ea2ec9` |

Ảnh xem nhanh:
- `E:\Unity\Project\FPS\Documents\UIUX\FreeAssets\Lotus-preview.jpg`
- `E:\Unity\Project\FPS\Documents\UIUX\FreeAssets\SunGraphica-preview.jpg`
- `E:\Unity\Project\FPS\Documents\UIUX\FreeAssets\SunGraphica-icons-preview.jpg`

## Giấy phép và credit

- **Lotus Garden:** cho phép dùng cá nhân/thương mại và chỉnh sửa; cấm bán hoặc phân phối lại asset. Trang không yêu cầu credit rõ ràng; nên ghi `Post Apocalypse Survival UI Asset Pack — lotus_garden` trong credits của game.
- **SunGraphica:** cho phép dùng cá nhân/thương mại và chỉnh sửa; cấm bán hoặc phân phối lại; yêu cầu credit `SunGraphica`.
- **Gamanbit:** file `Licence.txt` yêu cầu credit `Dark Survival HUD Elements | gamanbit.com`; cho phép dùng cá nhân/thương mại và chỉnh sửa; cấm phân phối lại PNG độc lập.

## Lưu ý tích hợp Unity

Các archive cung cấp hình ảnh/PSD, không có prefab, animation, binding dữ liệu hay code HUD. Bộ nguồn được giữ trong `Documents/UIUX/FreeAssets`; phần đã chọn được cắt/chỉnh màu và nhập vào `Survival/Curated/`, cấu hình Sprite, bilinear, không mipmap, 9-slice cho panel/control. Đã áp dụng vào ba scene và hai prefab UI, giữ binding/controller đang dùng. Danh sách đầy đủ nguồn và biến đổi: [curated-assets.json](../curated-assets.json).

Nguồn:
- https://lotus-garden.itch.io/post-apocalypse-survival-ui-asset-pack
- https://sungraphica.itch.io/creepy-game-ui
- https://gamanbit.itch.io/dark-survival-hud-elements-free-asset-pack
