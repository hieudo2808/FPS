# HUD/UI asset shortlist — kiểm tra thực tế 13/09/2026

**Cập nhật 14/09/2026:** đã dùng các bộ miễn phí Lotus Garden + SunGraphica và sprite từ ProjectSettingsPack, lọc 17 PNG rồi tích hợp vào UI. Xem [bản triển khai hiện tại](Curated-UI-Implementation.md). Các gói trả phí dưới đây chỉ là so sánh nghiên cứu, không mua.

Về `E:/ProjectSettings/Assets`: đã dùng nhóm HP/biohazard/lựu đạn/viền máu sau khi đối chiếu với bản đã nhập trong Unity. File dẫn xuất nằm trong `Survival/Curated`; không copy lại asset ngoài project vì các bản Unity tương ứng đã trùng byte và cần giữ GUID/import ổn định.

Có asset phù hợp. Kết luận trước đó quá hẹp: nhìn màu xanh của một atlas không đủ để loại các sprite riêng bên trong, và việc asset đã được import không có nghĩa nó đã được sử dụng tốt.

## Bộ UI/HUD trên internet

| Bộ | Đã kiểm chứng trực tiếp | Đánh giá cho FPS này | Giá tại lúc xem |
|---|---|---|---|
| [Dark — Complete Horror UI / Michsky](https://assetstore.unity.com/packages/2d/gui/dark-complete-horror-ui-200569) | Trang nhà bán, demo video có menu multiplayer, bảng grunge và rebind; native UGUI, demo scene, UI Manager, transition; v2.2.0 ngày 14/05/2026 | Ưu tiên cho main menu, Settings, lobby. Hợp yêu cầu thô/u ám nhất trong các bộ đã xem. Không mặc định đây là bộ HUD FPS gameplay đầy đủ | 39,99 USD chưa thuế |
| [INTERFACE — Apocalypse HUD / Synty](https://syntystore.com/products/interface-apocalypse-hud) | Trang nhà bán và ảnh component health/ammo: hơn 160 prefab có animation, hơn 650 icon, HP bar, damage FX, minimap, compass, weapon wheel, input glyph; dùng UGUI/2D, không chứa model 3D | Ưu tiên nếu muốn thay gameplay HUD bằng một bộ có component sẵn. Có các biến thể tối giản và kim loại/phế liệu. Cần chỉnh palette và chọn biến thể để hợp cảnh thực tế hơn phong cách POLYGON demo | Trang đang hiển thị 2.643.708 VND |
| [GUI Pro — Survival Clean / LAYERLAB](https://layerlab.itch.io/gui-pro-survival) | Trang tác giả và cover: 1700+ PNG, 300 pictogram, 90 item icon, sprite trắng/9-slice. Bản itch.io ghi không kèm code hoặc animation | Phong cách xanh sáng, nhiều màu, giống game mobile hơn horror mong muốn. Có giá trị về icon nhưng không phải lựa chọn đầu cho toàn bộ UI | 39,99 USD |
| [Grindhaus — Horror Icon Set / Beachboogeyman](https://beachboogeyman.itch.io/horror-icon-set-grind-haus) | Trang tác giả và preview: 32 icon, 16 splatter, kích thước 32/64 px, 148 file mỗi zip; CC-BY 4.0 | Miễn phí và đúng horror, nhưng pixel art, không nên ghép mặc định với UI hiện tại. Đây là icon pack, không phải full UI framework | Name your own price, có thể miễn phí |

Khuyến nghị: Dark UI là ứng viên mạnh cho menu. Apocalypse HUD là ứng viên mạnh cho gameplay. Không cần mua cả hai trước khi thử một bộ; ghép hai hệ component tùy tiện có thể lại gây thiếu đồng bộ. Các gói trả phí mới được nghiên cứu, chưa mua hoặc import.

Lưu ý kỹ thuật có bằng chứng: Dark UI ghi blur shader không hỗ trợ URP/HDRP và phần lớn quality options dựng sẵn không hoạt động với hai pipeline này. Nếu dùng gói này, giữ binding Settings hiện có và thay phần trình bày; không nối nguyên demo controller để điều khiển gameplay.

## Asset thực sự có trong E:/ProjectSettings/Assets

Đã đọc danh sách file, xem thêm 20 texture menu/frame và xuất 26 sprite riêng từ Unity để kiểm tra hình ảnh, thay vì chỉ nhìn atlas. File đối chiếu [local-assets-verified.json](local-assets-verified.json) xác nhận các sprite dưới đây trùng byte với bản trong `Assets/ThirdParty/UIKit/Sprites`.

| Asset | Nhìn thấy trong ảnh | Dùng được cho |
|---|---|---|
| `DataIcons_HP.asset` | Dấu cộng trong hình thoi, trắng | Icon HP nhỏ, rõ, không cần giữ palette sci-fi |
| `MissionIcons_biohazard.asset` | Biểu tượng biohazard trắng | Chỉ số infection và thông báo vùng nhiễm |
| `BloodBorderTB.asset` | Viền máu trong suốt, 916×133 | Mép màn hình khi low health/damage; không trải full màn hình hoặc hiển thị thường trực |
| `FragGrenade.asset` | Hình lựu đạn 57×32, có chi tiết vật thể | Thay placeholder lựu đạn hình chữ nhật; cần chuyển xanh sang trung tính nếu dùng |
| `TextFrameLeft.asset`, `TextFrameRight.asset` | Góc bracket mảnh | Nhóm HUD/key hint gọn, chỉ dùng một ngôn ngữ khung |
| `WhiteFrameThin.asset` | Khung vuông mảnh | Slot và keycap; cần cấu hình border nếu dùng 9-slice |
| `MenuButtonSelected1.asset` | Viền trắng kéo dài, đầu hơi vát | Focus của menu; cân nhắc hình dạng sci-fi trước khi áp dụng toàn bộ |
| `enemyHealthBarBackground.asset` | Khung health xanh, vát góc | Có cấu trúc sẵn nhưng hình dạng công nghệ rõ; không phải lựa chọn đầu cho horror |

[26 sprite đã xem](local-hud-sprites.jpg) · [20 texture bổ sung](local-asset-expanded.jpg)

Các hình này là asset có sẵn, không phải hình tôi tự vẽ để minh họa. Chúng chứng minh folder có phần dùng được; chưa đủ để khẳng định folder chứa một bộ HUD kinh dị hoàn chỉnh với prefab/animation/binding.

## Quyết định tích hợp

Một asset pack giải quyết phần mỹ thuật/component, còn vị trí HUD, độ ưu tiên thông tin và binding dữ liệu vẫn cần thiết kế. Với yêu cầu hiện tại, nên chọn một bộ chủ đạo trước rồi nối vào HUDManager/LobbyUI/SettingsUI hiện có. Không nên chỉ thêm vài icon rồi gọi đó là hoàn thành việc thay bộ HUD.

Ở thời điểm rà soát 13/09, chưa thay đổi scene/code UI hoặc mua gói nào. Bước lọc và tích hợp bằng asset miễn phí đã hoàn thành ngày 14/09; xem bản triển khai được liên kết đầu trang.

## Các bộ miễn phí đã tải về (13/09/2026)

| Bộ | Nội dung kiểm tra thực tế | Vai trò đề xuất |
|---|---|---|
| [Lotus Garden — Post Apocalypse Survival UI](https://lotus-garden.itch.io/post-apocalypse-survival-ui-asset-pack) | 117 PNG + 89 PSD: health bar tách frame/fill/background, hotbar, inventory, popup, dialogue, button/checkbox | Bộ nền cho gameplay HUD; chỉnh palette về đỏ máu/xám than |
| [SunGraphica — Horror Game UI Creator Kit FREE](https://sungraphica.itch.io/creepy-game-ui) | 417 PNG + 4 PSD trong bản FREE; thực tế gồm 18 PNG UI và 78 mẫu icon theo kích cỡ/màu. Bản 418 MB là trả phí, không tải | Menu, inventory, icon cảnh báo; dùng thống nhất với Lotus sau khi chọn một texture chủ đạo |
| [Gamanbit — Dark Survival HUD Elements](https://gamanbit.itch.io/dark-survival-hud-elements-free-asset-pack) | 4 PNG HUD, kèm Licence.txt; mẫu fantasy vẽ tay, không phải full FPS kit | Tham khảo/điểm nhấn phụ, không làm bộ chính |

Bản ZIP, manifest SHA-256, license/source và contact sheet nằm tại [FreeAssets/README.md](FreeAssets/README.md). Chỉ các PNG đã chọn được nhập vào `Assets/`; PSD và bộ nguồn vẫn ở Documents.
