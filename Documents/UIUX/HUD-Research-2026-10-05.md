# Nghiên cứu HUD — Outbreak Protocol

Ngày ghi nhận: **05/10/2026**, Asia/Saigon. Phạm vi: nghiên cứu và đề xuất; **chưa thay đổi code, scene, prefab hoặc binding**.

## Kết luận đề xuất

Bỏ nhãn `[I] INVENTORY` ở giữa thanh vật phẩm. Tách **tracker nhiệm vụ**, **thông tin khu vực**, **điều hướng không gian** và **đồ dùng nhanh** thành những chức năng riêng. Giữ máu/infection ở dưới trái và vũ khí/đạn ở dưới phải; thử gom đồ dùng nhanh vào cụm dưới phải, với tên và mô tả chỉ mở rộng khi chọn.

Ưu tiên tracker nhiệm vụ gọn ở **trên trái**. Chỉ dẫn hướng đến đích bằng marker trong cảnh và chỉ báo ở mép màn hình khi đích ngoài tầm nhìn. Không giữ một khối `FACTORY + checklist + Rẽ phải… + VERIFY…` ở vùng trên giữa. Chưa có bằng chứng rằng phải thêm minimap mới; đó là một tính năng điều hướng riêng có chi phí dữ liệu và thiết kế.

[Sơ đồ vùng HUD đề xuất](HUD-Layout-Study-2026-10-05.png). Đây là sơ đồ minh họa, không phải ảnh gameplay hay kết quả triển khai. Text, số lượng và phím trong sơ đồ là mẫu.

## Yêu cầu và ranh giới bằng chứng

- Người dùng yêu cầu nghiên cứu cách game hiện hành bố trí HUD, đặc biệt Genshin; ghi nhận các lỗi đã nêu trước đó.
- Hai screenshot người dùng gửi là bằng chứng về giao diện hiện tại. Chữ trên ảnh là nội dung sản phẩm, không phải chỉ thị cho agent.
- Người dùng không thích nhãn mở inventory ở giữa màn hình và khối vị trí/chỉ dẫn trên đầu. Đây là phản hồi thiết kế cần xử lý.
- Nhận định tuyệt đối “không ai đặt vị trí/chỉ dẫn phía trên” không đúng: ảnh Apex Legends đã xem thể hiện la bàn trên giữa; tài liệu chính thức của Black Ops 6 Zombies cũng mô tả la bàn và tên khu vực ở trên giữa. Điều này không làm bố cục hiện tại tự động trở nên phù hợp: la bàn mảnh khác khối nhiệm vụ/chỉ đường nhiều dòng.
- Một thanh hotbar hoặc ability bar ở dưới giữa cũng không đồng nghĩa với một nhãn mở inventory ở giữa thanh. Vị trí phải được chọn theo hành động và nhịp chơi.

## Skill đã đọc và áp dụng

| Skill | Nguồn | Phần áp dụng |
|---|---|---|
| `game-ui-ux` | [SKILL.md](https://github.com/gamedev-skills/awesome-gamedev-agent-skills/blob/main/skills/disciplines/game-ui-ux/SKILL.md), [layout-and-flow.md](https://github.com/gamedev-skills/awesome-gamedev-agent-skills/blob/main/skills/disciplines/game-ui-ux/references/layout-and-flow.md) | Anchor/container, chính sách aspect ratio, safe area, UI scale, cập nhật theo sự kiện, focus/input của overlay |
| `game-ui-design` | [SKILL.md](https://github.com/omer-metin/skills-for-antigravity/blob/main/skills/game-ui-design/SKILL.md), cùng `patterns.md`, `sharp_edges.md`, `validations.md` | Hiển thị theo ngữ cảnh, phân tầng thông tin, không che gameplay, readability khi chuyển động, prompt theo action/binding, marker trong thế giới |
| `unity-ui` | `.agents/skills/unity-skills/skills/ui/SKILL.md` | RectTransform, Canvas, bố cục theo nhóm và kiểm chứng qua Editor khi triển khai |
| `find-skills` | Skill cục bộ | Tìm skill game UI chuyên biệt, kiểm tra nguồn trước khi áp dụng |

Hai skill game UI được đọc từ upstream, **không cài thêm vào cấu hình agent**. Kết quả kiểm tra tại thời điểm nghiên cứu: nguồn `gamedev-skills/awesome-gamedev-agent-skills` có 1.304 GitHub stars, kết quả Skills CLI khoảng 5,6K installs cho `game-ui-ux`; nguồn `omer-metin/skills-for-antigravity` có 160 stars, khoảng 3,4K installs cho `game-ui-design`. Đây là tín hiệu nguồn tham khảo, không chứng minh chất lượng từng hướng dẫn.

Nội dung đã đọc được xác định bằng Git blob: `game-ui-ux` = `c84646755c3d8f2b96fd955d1d2706e911b45596`; `game-ui-design` = `89600260be95c413ee91c5cfe52d872737d33652`.

Một số quy tắc trong skill là khái quát quá mạnh hoặc thiên về web. Không áp dụng máy móc các regex CSS, kích thước touch target, giả định minimap luôn ở một góc, hoặc tự ẩn máu/đạn trong game có nguy hiểm liên tục. Ví dụ vị trí trong skill phải được đối chiếu với game thực tế và bối cảnh dự án. Skill kỹ thuật tham chiếu Unity 6.3; Editor đã xác nhận là 6000.5.6f1, nên API cụ thể vẫn cần kiểm tra khi triển khai.

## Đối chiếu game

| Game và bằng chứng | Quan sát đã xác minh | Bài học cho FPS này |
|---|---|---|
| **Genshin Impact**, [catalog console/PC](https://www.gameuidatabase.com/gameData.php?id=470), [ảnh HUD đã xem](https://www.gameuidatabase.com/uploads/Genshin-Impact01032021-021301-90744.jpg) | Minimap trên trái; mục tiêu nằm dưới minimap; có khoảng cách và thông tin đích thấp hơn (`Lower 269m`). HP dưới giữa; thao tác dưới phải; menu/icon túi đồ trên phải. Banner nhận nhiệm vụ có thể xuất hiện trên giữa. | Học cách phân tách mục tiêu, điều hướng và thao tác. Banner tạm thời khác tracker thường trực. Không sao chép bố trí HP hay nút điều khiển của RPG góc nhìn thứ ba sang FPS. |
| **Cyberpunk 2077**, [catalog](https://www.gameuidatabase.com/gameData.php?id=439), [ảnh nhiệm vụ/đường đi đã xem](https://www.gameuidatabase.com/uploads/Cyberpunk-207712292020-013438-75882.jpg) | Minimap trên phải, tracker bên phải dưới minimap; có tuyến trên minimap và marker đích trong cảnh với khoảng cách. Đạn dưới phải, các thao tác/đồ dùng dưới trái. | Nhiệm vụ không phải luôn ở trái. Tracker trả lời “làm gì”; marker/minimap trả lời “đi đâu”. Marker không cần mang cả câu nhiệm vụ. |
| **Black Ops 6 Zombies**, [tài liệu chính thức](https://www.callofduty.com/blog/2024/08/call-of-duty-black-ops-6-zombies-deep-dive-terminus-map-intel) | Mô tả Default HUD: minimap và objectives trên trái; **compass và tên khu vực trên giữa**; vitals dưới trái; vũ khí, ammo, tactical/lethal dưới phải; perks dưới giữa. Có preset Legacy và các lựa chọn readability. | Phản ví dụ rõ cho nhận định không game nào dùng phía trên. Cụm combat dưới phải là tiền lệ gần với FPS sinh tồn này. Cho phép giảm HUD theo nhu cầu thay vì coi một bố cục là phổ quát. |
| **Apex Legends**, [ảnh HUD đã xem](https://www.gameuidatabase.com/uploads/Apex-Legends07152020-072204-49086.jpg) | La bàn ngang với heading trên giữa; minimap trên trái; squad/vitals dưới trái; abilities dưới giữa và vũ khí/đạn dưới phải. | Bằng chứng trực quan rõ rằng điều hướng phía trên là một lựa chọn hợp lệ. Ability bar giữa dưới không phải nhãn mở inventory; mỗi vùng phục vụ nhiệm vụ riêng. |
| **Dead Island 2**, [ảnh gameplay đã xem](https://www.gameuidatabase.com/uploads/Dead-Island-208122025-011640-79215.jpg) | Tracker mục tiêu trên phải; HP trên trái trong ảnh; thao tác ở các góc dưới. Trung tâm chủ yếu dành cho kẻ địch/weapon/reticle. | Cùng góc nhìn thứ nhất và chủ đề zombie vẫn có quy ước khác. Học sự rõ ràng và khoảng trống gameplay; không quyết định vị trí bằng số lượng game dùng một góc. |

Các ảnh Genshin/Cyberpunk/Apex là tư liệu lưu trữ 2021/2020/2020; ảnh Dead Island 2 nằm trong đợt tư liệu có tên file 2025; tài liệu BO6 xuất bản 2024. Không coi chúng là xác minh giao diện patch mới nhất tháng 10/2026. Chúng đủ để kiểm tra các mẫu bố trí cụ thể và bác bỏ nhận định tuyệt đối. Game UI Database không được dùng làm nguồn asset sản phẩm hoặc AI asset generation; nghiên cứu này chỉ đối chiếu bố cục, không sao chép hình ảnh vào game.

**Khác biệt nguồn BO6:** ảnh quảng bá `BO6-ZOMBIES-888-009.jpg` đã được xem và thể hiện compass quanh minimap trên trái, trong khi phần mô tả cùng bài nói trên giữa. Đây là tư liệu trước phát hành và có khả năng dùng cấu hình khác; chưa xác minh nguyên nhân. Vì vậy, vị trí trên giữa của BO6 trong bảng là nội dung tài liệu, không phải kết luận rút ra từ ảnh đó hoặc xác nhận Default HUD của bản hiện tại. Ảnh Apex là bằng chứng trực quan riêng cho la bàn trên giữa.

Nguồn nguyên tắc từ Unity: [How to immerse your players through effective UI and game design](https://unity.com/blog/games/how-to-immerse-your-players-through-effective-ui-and-game-design). Bài viết nhấn mạnh HUD phải phù hợp thể loại, dễ đọc và hỗ trợ thông tin người chơi cần; giao diện diegetic không luôn thích hợp với nhu cầu đọc nhanh.

## Cấu trúc điều hướng đề xuất

1. **Tracker cố định trên trái:** một mục tiêu đang theo dõi và bước hành động hiện tại; khu vực `FACTORY` là thông tin phụ. Checklist đầy đủ mở trong nhật ký nhiệm vụ. Không lặp toàn bộ tên nhiệm vụ ở hai nơi.
2. **Marker không gian:** chỉ một mục tiêu đang theo dõi, gắn vào vị trí đích. Hiển thị icon + khoảng cách; thêm ký hiệu cao/thấp khi chênh tầng có ý nghĩa. Khi đích ngoài màn hình, dùng chỉ báo mép màn hình thay marker đang thấy.
3. **Hướng đi và lộ trình:** khoảng cách thẳng đến mục tiêu không đồng nghĩa chiều dài đường đi. Marker bám đích cũng không đảm bảo đường đi khả dụng qua tường/cửa/tầng. Nếu hiển thị “rẽ phải”, câu đó phải có dữ liệu tuyến/waypoint và cập nhật theo vị trí, hướng camera và hành lang thực tế. Chưa xác minh hệ thống hiện tại có dữ liệu này.
4. **Đến gần đích:** nhường ưu tiên cho prompt tương tác với cửa/công tắc/tài liệu. Ngưỡng hoàn thành lấy từ logic nhiệm vụ; không chỉ từ khoảng cách hình học, để tránh hoàn thành qua tường hoặc sai tầng.
5. **Minimap:** chỉ bổ sung nếu playtest cho thấy marker + môi trường chưa đủ cho định hướng. Minimap cần dữ liệu các tầng, đường đi và mật độ icon; không phải sửa layout đơn thuần.

Không mặc định thêm đường phát sáng kiểu RPG. Cảnh nhà máy/campaign sinh tồn cần giữ khả năng tự quan sát môi trường; có thể cung cấp hỗ trợ điều hướng khi người chơi yêu cầu.

## Bố cục và trạng thái

| Vùng | Mặc định | Khi thay đổi ngữ cảnh |
|---|---|---|
| Trên trái | Tracker 1 mục tiêu, khu vực phụ | Nhiệm vụ mới/đổi bước hiện chi tiết ngắn rồi thu gọn. Khi bắn/ADS, giữ một dòng mục tiêu nếu cần; không che cảnh bằng checklist. |
| Trên giữa | Để thoáng trong phương án ưu tiên | La bàn mảnh là phương án thay thế hợp lệ nếu gameplay cần heading/ping đội. Không thêm mặc định. Banner cập nhật là tạm thời. |
| Trong cảnh/mép màn hình | Marker của mục tiêu đang theo dõi | Đổi thành chỉ báo mép khi offscreen; tránh vùng sát reticle và giới hạn icon trùng nhau. |
| Dưới trái | HP và infection thành một nhóm | Cảnh báo tăng mức nhấn bằng icon/text cùng màu; không ẩn cảnh báo khẩn cấp. |
| Dưới phải | Weapon/ammo và đồ dùng nhanh gọn | Số đạn băng lớn, dự trữ nhỏ hơn; icon + count + binding ngắn. Tên vật phẩm/đổi loại chỉ hiện khi chọn hoặc mở quick select. |
| Dưới giữa | Dành chỗ cho phụ đề và prompt tương tác | Quick select có thể mở tạm thời ở đây nếu controller/gameplay cần; không giữ nhãn inventory thường trực. |
| Inventory/quest log | Màn hình/overlay mở theo yêu cầu | Binding/glyph lấy từ action đang gán. Multiplayer không mặc định pause/slow motion khi mở. |

Một biểu tượng túi đồ nhỏ hoặc hint mở inventory có thể nằm ở góc và chỉ xuất hiện lúc onboarding/người chơi yêu cầu. Không cần giữ chữ `INVENTORY` liên tục nếu đã có binding trong Controls/menu.

## Backlog đã ghi nhận — bằng chứng trước sửa

| ID | Ưu tiên | Vấn đề hoặc yêu cầu | Bằng chứng/trạng thái | Tiêu chí kiểm chứng sau sửa |
|---|---|---|---|---|
| HUD-01 | P1 | Bỏ `[I] INVENTORY` giữa hotbar; chấm dứt chồng text lên icon/count | Chồng lấn nhìn thấy ở cả hai ảnh; thay vị trí theo phản hồi người dùng | Không nhãn giao nhau ở khám phá, chọn đồ, combat và inventory |
| HUD-02 | P1 | Tách nhiệm vụ/khu vực/điều hướng; bỏ khối chỉ dẫn nhiều dòng phía trên giữa | Thấy ở ảnh 2; ưu tiên thiết kế đã ghi nhận | 1 mục tiêu đang theo dõi; tên/action/marker không lặp và có thứ bậc rõ |
| HUD-03 | P1 | Cải thiện khả năng đọc tracker trên trời sáng/tường nhiều chi tiết | Rủi ro nhìn thấy từ hai nền; chưa đo contrast runtime | Đọc được trên nền sáng/tối và khi camera chuyển động; không tăng plate quá lớn |
| HUD-04 | P2 | Chữ infection, tên vũ khí, thông tin phụ quá nhỏ trong ảnh cung cấp | Quan sát ảnh; chưa biết fontSize thật hoặc scaling | Đọc ở độ phân giải đích và UI scale lớn; không suy fontSize gốc từ ảnh thu nhỏ |
| HUD-05 | P2 | Làm rõ `G` và `[3] SWITCH GRENADE` | Cùng xuất hiện; **chưa chứng minh binding sai** | Mỗi prompt có hành động rõ; rebind/device switch cập nhật glyph đúng |
| HUD-06 | P2 | Giảm khối hotbar thường trực, chuẩn hóa icon/count/selected state | Đánh giá bố cục; chưa có playtest | Nhận ra vật phẩm đang chọn; trạng thái unavailable không chỉ dùng màu; không ép đọc 4 tên mỗi lần |
| HUD-07 | P2 | Gom infection với HP, giữ thứ bậc ammo chính/dự trữ | Đề xuất tổ chức thông tin | Người chơi đọc trạng thái cơ thể và tài nguyên chiến đấu nhanh, không mất thông tin nguy hiểm |
| HUD-08 | Cần xác minh | Tâm ngắm nhỏ, có nguy cơ chìm vào tường sọc | Chưa xác nhận lỗi khi chơi | Kiểm tra nền tối/sáng, chuyển động/ADS; chỉnh outline/size nếu có bằng chứng |
| HUD-09 | Cần xác minh | Anchor, CanvasScaler, bounds và vùng phụ đề/interaction/weapon | Chưa inspect live cấu hình HUD trong lượt này | Không overlap ở 1280×720, 1920×1080, ultrawide; không che phụ đề, prompt hoặc reticle |
| HUD-10 | Cần xác minh | Tính đúng của khoảng cách/chỉ dẫn rẽ và đích khác tầng | Text điều hướng có trong ảnh; thuật toán chưa inspect | Kiểm tra camera quay, offscreen/behind-camera, cửa khóa, tuyến đổi, đích tầng khác và mục tiêu hoàn thành |

## Khi chuyển sang triển khai

- Query Unity-Skills/Gerty trước để xác định Canvas và UI thực tế; query Codebase Memory trước khi tra C# và change impact.
- Áp dụng save gate và kiểm chứng sau mutation theo AGENTS.md; không ghi đè scene/prefab đang dirty.
- Giữ kiến trúc hiện có trừ khi có bằng chứng cần đổi; các tài liệu UI tháng 9 chỉ là ngữ cảnh lịch sử, không xác minh trạng thái code/Editor hiện tại.
- Dùng anchor/container và chính sách scaling đã kiểm tra; không sửa lỗi bằng một bộ offset chỉ đúng ở screenshot hiện tại.
- Sau code change, compile và inspect Console; kiểm chứng có mục tiêu vào overlap, binding, navigation và các state HUD đã thay đổi.
- Các ghi nhận trong phần nghiên cứu là baseline trước sửa; kết quả gameplay sau triển khai nằm trong [báo cáo bàn giao](HUD-Implementation-2026-10-05.md). Controller vật lý chưa được playtest.


## Theo dõi triển khai HUD-01–HUD-10

Bốn mốc đã đạt trong phạm vi fixture được mô tả tại [báo cáo bàn giao](HUD-Implementation-2026-10-05.md). Báo cáo phân biệt kiểm chứng trình bày trong local host với multiplayer nhiều client và controller vật lý.

- [x] Mốc 1: baseline và bố cục. Lưu ảnh gameplay người dùng trước sửa; áp dụng riêng GameScene với Undo/save gate và đọc lại RectTransform. Fixture gameplay đạt cả ba độ phân giải × 100%/125%, không overlap giữa nhóm HUD, không overflow/truncation; reticle trong 0,5 px.
- [x] Mốc 2: tab Nhiệm vụ và điều hướng. Tracker/marker/Missions dùng lựa chọn cục bộ chung. Fixture live GameScene đạt camera 360°, FactoryInterlock khóa/mở, partial/invalid route, sampling khác tầng, cao/thấp, missing camera/target, shared completion, chuyển chương và checkpoint rollback.
- [x] Mốc 3: binding, readability và trạng thái. Test rebind/reset/derived gamepad đạt; text hiển thị ≥16 px tại 720p, subtitle strip không chồng Inventory. Fixture đạt exploration, combat, ADS/scope, đổi grenade, dùng/hủy thuốc, hết đồ, tương tác, squad downed/revive, downed/spectating, pause/settings, cinematic, ready/countdown, debrief và retry checkpoint.
- [x] Mốc 4: test, bằng chứng và bàn giao. 28 test độc lập có XML PASS; compile runtime cuối 0 lỗi/54 warning, Console không có error. Lưu ảnh gameplay trước/sau, sáu cấu hình gameplay/Missions và các trạng thái chuyên biệt; [báo cáo](HUD-Implementation-2026-10-05.md) ghi trạng thái từng HUD-01–HUD-10, receipts và giới hạn fixture.

Không coi preview authoring là ảnh gameplay. Không coi các lần filter test khớp 0 test là PASS.
