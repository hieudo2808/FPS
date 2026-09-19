# UI/UX review — Outbreak Protocol

Cập nhật ngày 14/09/2026. Hướng đã chọn: **kinh dị sinh tồn, thô và u ám**.

**Bản hiện tại:** đã lọc và tích hợp 17 sprite từ Lotus Garden, SunGraphica FREE và ProjectSettingsPack vào menu, Settings, lobby và HUD. [Kết quả triển khai và kiểm thử mới nhất](Curated-UI-Implementation.md) · [Ảnh tổng quan](UI-Curated-Overview.jpg). Các phần dưới lưu lại nghiên cứu và bản thiết kế trước bước tích hợp asset; số liệu kiểm thử cũ có ngày/job riêng.

Rà soát vi mô sau tích hợp, gồm số đo chữ nhỏ, focus keyboard/gamepad và các hạng mục P1/P2: [UI-Detailed-Review-2026-09-14.md](UI-Detailed-Review-2026-09-14.md).

Làm rõ nguồn local: bốn nhóm `DataIcons_HP`, `MissionIcons_biohazard`, `FragGrenade`, `BloodBorderTB` trong `E:/ProjectSettings/Assets` đã được đối chiếu byte với bản trong `Assets/ThirdParty` và dùng để tạo sprite Curated. Không import/copy lại cả thư mục vì sẽ tạo bản trùng; các asset còn lại được giữ làm tham khảo.

**Bổ sung sau phản hồi về asset:** [HUD-Asset-Shortlist.md](HUD-Asset-Shortlist.md) có kết quả rà sâu hơn: Dark UI, Apocalypse HUD và các sprite HP/biohazard/viền máu trong folder. Đánh giá asset ở bản review ban đầu chưa đủ sâu; các phần dưới mô tả bản UI đã triển khai trước khi có shortlist này.

## Đánh giá và thay đổi

Theo đánh giá thiết kế của tôi, bản trước còn cảm giác prototype: quá nhiều khung chữ nhật đặc, độ nổi bật giữa các nhóm chữ gần nhau, và thông tin trạng thái trận đấu chiếm chỗ không cần thiết. Đây là nhận định thẩm mỹ dựa trên đối chiếu mẫu; chưa có nghiên cứu người chơi để kết luận mức độ hài lòng.

Đã triển khai trực tiếp trong Unity:

- Menu chính chỉ còn tên **OUTBREAK PROTOCOL**, PLAY ONLINE, SETTINGS và QUIT TO DESKTOP. Đã bỏ slogan, eyebrow, footer và operation card.
- Tên game có hai cấp chữ. Menu dùng chữ trên nền cảnh, marker và hiệu ứng focus 120 ms dùng thời gian unscaled, hỗ trợ pointer và selection từ EventSystem.
- HUD bỏ nhãn phase của director và các trạng thái Warmup/Playing. Nhãn trạng thái chỉ hiện GAME OVER khi trận kết thúc; thông tin bị hạ/hồi sinh vẫn được giữ.
- Health/ammo được gom về hai góc dưới, tăng khác biệt giữa số đạn chính và đạn dự trữ, thay khung đặc bằng lớp tối mờ ở cạnh, giảm chi tiết trang trí.
- Settings giảm nền lồng nhau, phân biệt tab đang chọn, giữ khoảng cách đều và vùng bấm rõ. Modal chặn tương tác nền; rebind không làm Esc đóng nhầm Settings.
- Lobby giữ vùng tên, nhân vật và readiness riêng, không để tên dài ghi đè trạng thái. Bảng và các nút dùng chung palette với menu/settings.
- Menu trong trận dùng tiêu đề MENU. Thông báo online tiếp tục chạy được giữ vì game nhiều người không thực sự tạm dừng.

## Nguồn online đã đọc và ảnh đã xem

Trình duyệt đã truy cập lại thành công. Phần này thay thế giới hạn nghiên cứu trong báo cáo cũ.

| Nguồn | Nội dung đã kiểm chứng | Quyết định áp dụng |
|---|---|---|
| [Unity — How to immerse your players through effective UI and game design](https://unity.com/blog/games/how-to-immerse-your-players-through-effective-ui-and-game-design), Christo Nobbs, 25/11/2022 | Đã đọc bài: UI cần phù hợp bối cảnh/thể loại, chỉ hiện thông tin hữu ích; ví dụ health diegetic của Dead Space | Giảm nội dung HUD và đồng bộ hình ảnh với cảnh kinh dị; không ép mọi thông tin thành UI diegetic khi hệ thống hiện có chưa hỗ trợ |
| [GamesIndustry.biz — Best practices for designing an effective video game UI](https://www.gamesindustry.biz/best-practices-for-designing-an-effective-video-game-ui), William Nelson / Edd Coates, 04/03/2021 | Đã đọc bài: tổ chức UX theo thể loại, hình thức theo chủ đề, ưu tiên hierarchy/contrast trước trang trí, chuyển động nhẹ không cản thao tác | Giảm khung cạnh tranh, chữ có cấp bậc, feedback nhanh và thống nhất |
| [Still Wakes the Deep (2024) — Game UI Database](https://www.gameuidatabase.com/gameData.php?id=2351) | Đã xem ảnh lớn title menu và audio settings: cảnh chiếm diện tích chính, tên game lớn, menu chữ, selection rõ; settings dùng hàng và slider gọn | Menu chính thoáng hơn; giữ phần tương tác đơn giản, để nền tạo không khí |
| [Dead Island 2 (2023) — Game UI Database](https://www.gameuidatabase.com/gameData.php?id=2142) | Đã xem ảnh gameplay lớn: HUD gọn ở rìa, health dễ nhận diện, phần giữa dành cho thế giới game | Dồn nhóm HUD về góc, giảm plate đặc, tăng khoảng trống giữa màn hình |
| [Dead Space (2008) — Game UI Database](https://www.gameuidatabase.com/gameData.php?id=581) | Đã xem catalog thumbnail giao diện holographic; đây là bản 2008 | Tham khảo nguyên tắc phù hợp bối cảnh, không coi đây là mẫu phát hành mới |

Ảnh tham chiếu đã xem trực tiếp: [Still Wakes the Deep — title](https://www.gameuidatabase.com/uploads/Still-Wakes-the-Deep02252026-122941-44779.jpg), [audio](https://www.gameuidatabase.com/uploads/Still-Wakes-the-Deep02252026-122956-14259.jpg), [Dead Island 2 — gameplay](https://www.gameuidatabase.com/uploads/Dead-Island-208122025-011640-79215.jpg).

Các ảnh game online chỉ dùng để nghiên cứu bố cục; không được nhập làm asset sản phẩm. Những nguyên tắc trên không đồng nghĩa mọi game hiện hành đều dùng một kiểu giao diện.

## Rà soát E:/ProjectSettings/

Đã kiểm tra các nhóm liên quan trong `E:/ProjectSettings/Assets`: Font, Sprite và Texture2D. Không nhập toàn bộ một project khác vào FPS.

| Asset/nhóm | Kết quả xem trực tiếp hoặc đối chiếu | Quyết định |
|---|---|---|
| `Sprite/WhiteCross.asset` | SHA256 trùng hoàn toàn với `Assets/ThirdParty/UIKit/Sprites/WhiteCross.asset`. Texture nguồn thực tế là hình vuông trắng 64×64, không phải dấu thập | Có thể dùng làm nền/fill đơn sắc; đã có bản trong project nên không nhập trùng. UI hiện dùng SolidFill có sẵn của đợt chỉnh này |
| `Sprite/AmmoIcon.asset` | SHA256 trùng hoàn toàn với `Assets/ThirdParty/UIKit/Sprites/AmmoIcon.asset` | Đã có bản trong project. HUD mới ưu tiên số đạn nên icon ammo phụ đang ẩn |
| Xolonium Bold | Font góc cạnh, thiên sci-fi | Giữ Teko cho tên game/heading và Liberation Sans cho nội dung |
| `SpriteAtlasTexture-_C16_HUD (Group 1)-1024x1024-fmt12.png`, atlas ingame | Biểu tượng xanh/cyan, khung công nghệ, ký hiệu radiation/health | Có hình dạng chức năng để tham khảo, không dùng nguyên atlas cho survival horror |
| `SpriteAtlasTexture-BloodFX-1024x1024-fmt12.png` | Vệt máu, mép máu đỏ; phù hợp cảnh báo bị thương hơn UI thường trực | Đã chọn BloodEdge và nối vào cảnh báo HP thấp ngày 14/09 |
| `VignetteMask.png` | Mask 128×128 có tâm sáng, cạnh tối | Có thể chuyển kênh sáng thành alpha để tạo vignette; hiện đã có NavigationShade/HudShade phù hợp nên không thêm lớp tối trùng chức năng |
| `BasicsAtlas.png`, `MegaAtlas.png` | Chủ yếu flare/glow/particle | Không phải bộ component menu hoàn chỉnh; không cần cho lần này |
| `DE2_menu_main_01Background.png` | Cảnh không gian/tàu, tông xanh | Không phù hợp bối cảnh thành phố zombie |
| `BUTTONS_Layer 31 copy 11.png`, `ButtonBackground.png` | Viền ô và gradient xanh | Chỉ tham khảo cấu trúc, không nhập trực tiếp |

[Contact sheet asset đã rà](local-asset-candidates.jpg).

Bản thiết kế trước dùng background `catastrophe.jpg`, Teko Bold/Regular và icon weapon hiện có, cùng NavigationShade, SurfaceWear, WornRule, SolidFill và HudShade. Ngày 14/09 đã bổ sung bộ `Survival/Curated/`: bề mặt/control có texture, thanh máu và các icon/viền máu từ asset nguồn; xem manifest trong bản triển khai hiện tại.

## Cách triển khai

Giữ **UGUI + TextMeshPro**, phù hợp controller, prefab và Input System đang có. Đổi framework không tự sửa lỗi anchor, tràn chữ hay thứ tự canvas, đồng thời tăng chi phí viết lại binding.

`TacticalUiTheme` tập trung màu; `TacticalUiWorkshop` authoring các scene/prefab; `SurvivalMenuItem` xử lý feedback menu. Controller gameplay và lobby vẫn là nguồn dữ liệu, không bị thay bằng dữ liệu mock.

| Token | Giá trị |
|---|---|
| Background / Surface / Control | `#0E0F10` / `#1A1B1C` / `#373634` |
| Text / Muted | `#EBE8DA` / `#AEACA3` |
| Accent / Action | `#DC7769` / `#87312B` |
| Canvas order | HUD 0, campaign 20, menu 100 |
| Reference resolution | 1920×1080, CanvasScaler Expand |

Thành phần trang trí không nhận raycast. Roster dùng vùng tên/readiness tách biệt; tên người chơi không được parse rich text. Safe area áp dụng MainPanel; chưa kiểm thử thiết bị có notch. Kích thước chữ/control giảm theo độ phân giải; chưa có slider phóng to toàn bộ UI.

## Kiểm chứng

- `FPS.Tests.SurvivalUiRegressionTests`: **6/6 passed**, job `b4a1fa3d`, [kết quả](test-results.json). Bao gồm hủy rebind hai lần, modal độc quyền, bounds/overlap các hàng binding, tên dài, contrast, và xóa nhãn phase khi thiếu match manager.
- Đã render lại 13 tổ hợp màn hình/kích thước: 8 màn hình ở 1920×1080, Controls 1280×720, Play/Lobby/HUD 1024×768, Main menu 2560×1080. Các kiểm tra text overflow/control bounds báo 0 lỗi; đã xem trực quan ảnh chính và contact sheet. Đây không phải bộ phát hiện mọi loại chồng lấn.
- Play Mode MainMenu: chọn Settings bằng EventSystem và gọi pointer enter; marker alpha đạt 1, label dịch 8 đơn vị; nút mở đúng Settings. [Ảnh runtime](runtime-refined-settings.png).
- Play Mode GameScene riêng: matchStateText rỗng/inactive, phaseText null, health hiện 100/100. Scene chạy riêng không có NetworkManager/nhân vật/camera gameplay, nên kiểm tra này chỉ xác nhận hành vi HUD khởi tạo.
- Console sau các bước runtime báo 0 lỗi. Scene MainMenu đã được khôi phục và lưu sau kiểm thử; kiểm tra live `isDirty == false`.

## Giới hạn của kết quả

- Chưa kiểm thử Relay nhiều client, một trận đầy đủ với weapon icon runtime, hay toàn bộ campaign/nhiệm vụ/phụ đề. Chưa thể kết luận mọi trạng thái động đều không đè nhau.
- Các ảnh `MainMenu-*`, `LobbyScene-*`, `GameScene-*` là bản sao Canvas render trong PreviewScene. Giá trị là trạng thái authoring/sample; lobby có tên dài mẫu, HUD chưa có icon weapon được cấp từ nhân vật. Không coi chúng là ảnh một trận chơi thực tế.
- Các ảnh `runtime-*` là ảnh Play Mode. GameView thực tế nhỏ hơn độ phân giải Canvas tham chiếu; ảnh HUD chạy riêng scene không có môi trường chơi đầy đủ.
- Chưa có playtest với người dùng hoặc điều khiển gamepad vật lý. Selection được kiểm bằng EventSystem; rebind đã có kiểm thử tự động và kiểm tra runtime ở lượt trước.

## Xem kết quả

- [Tổng quan hiện tại](UI-Curated-Overview.jpg)
- [Main menu](MainMenu-main-1920x1080.png)
- [Play / Join](MainMenu-play-1920x1080.png)
- [Controls](MainMenu-controls-1920x1080.png)
- [Lobby](LobbyScene-lobby-1920x1080.png)
- [HUD](GameScene-hud-1920x1080.png)
- [Menu trong trận](GameScene-pause-1920x1080.png)

Mã nguồn chính: `Assets/FPS/Features/UI/Editor/TacticalUiWorkshop.cs`, `Assets/FPS/Features/UI/Editor/UiVisualVerification.cs`, `Assets/FPS/Features/UI/Runtime/TacticalUiTheme.cs`, `Assets/FPS/Features/UI/Runtime/SurvivalMenuItem.cs`, `Assets/FPS/Features/UI/Runtime/HUDManager.cs`.
