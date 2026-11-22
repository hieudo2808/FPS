# HUD Factory và kế hoạch rà toàn bộ giao diện · 22/11/2026

**Quyết định cần lấy vòng này:** HUD trên cảnh dùng chữ trực tiếp hay cụm có nền; người chơi có cần chủ động gọi trợ giúp tìm hướng khi mất dấu hay không. Đây là bản thảo để thảo luận, chưa phải flow hoặc mockup đã duyệt. [Mở minh họa HTML](./56-hud-factory.html) · [Đánh giá và ảnh hiện trạng](./55-danh-gia-hud-va-giao-dien-2026-11-21.md) · [Luồng hai đích Factory đã duyệt](./54-waypoint-tai-dich-factory-2026-11-20.md).

## Điều gì làm HUD trông dày?

[PLAYTEST screenshot – ảnh Factory lưu ngày 20/11, đối chiếu trong HTML] Khi Warden còn `15 / 45`, bốn ô đồ dùng và hai hàng vũ khí nằm chung một tấm nền. [CODE HUDManager.cs:333] là khung 526 × 218, bằng 27,4% chiều **ngang** tại 1920 × 1080. [CODE CampaignHUD.FactoryDestinations.cs:61] tracker nhiệm vụ rộng 421 đơn vị; ảnh có sáu dòng. [CODE SquadHUD.cs:145] đồng đội khỏe vẫn mang nhãn `ALIVE`. Thanh sức khỏe/nhiễm đã gộp: không thiết kế lại thành hai thanh.

[INFERENCE] Điều đáng thử trước là **đổi thứ tự mắt bắt gặp thông tin**: đưa số đạn trong băng lên trước đạn dự trữ và bốn món có thể chọn; để tên đồng đội yên khi khỏe và nói rõ ai gục khi có việc cần làm. Có thể giữ tâm ngắm, cỡ phụ đề và cả hai nhánh. Một HUD ít chi tiết mà khó đọc trên cửa sáng vẫn là phương án tệ. Cảm giác khó chịu là phản hồi thật của người dùng; nguyên nhân này là giả thuyết, chưa có đo lường người chơi.

## Mẫu tham khảo để nhìn đúng chi tiết

| Xem gì | Có gì trong nguồn | Chuyển sang Factory thế nào (đề xuất) |
|---|---|---|
| Resident Evil Requiem: [cảnh ngắm](https://interfaceingame.com/screenshots/resident-evil-requiem-hud/) → [túi đồ](https://interfaceingame.com/screenshots/resident-evil-requiem-inventory/) | Trong ảnh hành lang, cụm góc phải dưới hiển thị súng và đạn, phần lớn khung là cảnh. Mở túi mới thấy lưới, mô tả và các thuộc tính. Hai ảnh cụ thể, không suy ra toàn game tự ẩn HUD. | Khi đi, thấy đạn hiện tại và món đang cầm; lúc đổi món mới mở các lựa chọn và phím. Với người mới, kiểm tra xem họ tìm được thao tác đổi món. Không đưa khung kim loại vào mọi góc chỉ để giữ chất survival. |
| Genshin Impact: [GIF Navigate của HoYoverse](https://webstatic.hoyoverse.com/upload/op-public/2023/06/19/3384bdf76aedd5e964b6710029edb0c3_1055117744910620332.gif) · [ghi chú bản 3.8](https://genshin.hoyoverse.com/en/news/detail/112174) | Khi mục tiêu đủ xa, chọn Navigate sẽ mở bản đồ để định hướng. GIF của nhóm phát triển minh họa thao tác, ghi chú bản phát hành xác nhận. Đây là **theo nhiệm vụ**, khác điểm dịch chuyển. | Tách “tôi đang làm việc gì?” khỏi “đích ở đâu?”. Factory vẫn có hai đích, tự chọn thứ tự. Gợi ý theo yêu cầu khi mất hướng chỉ là phương án hỏi, không mặc định mang bản đồ, mét, mũi tên hay đường sáng vào game. |
| Left 4 Dead 2: [khi một người đang được cứu](https://interfaceingame.com/screenshots/left-4-dead-2-being-revived/) | Hàng chân dung và máu ở mép dưới vẫn hiện; thông báo được cứu và thanh tiến độ hiện riêng gần tâm. Đây là góc nhìn **của người được cứu**. | Giữ chỗ quen thuộc để tìm đồng đội; phân biệt “Mason gục” với “đang cứu” và “chính tôi gục”. Màu kèm tên/trạng thái. Không mặc định đưa mọi cảnh báo tới tâm khi người chơi còn đang bắn. |
| VALORANT: [màn hình mua đồ](https://interfaceingame.com/screenshots/valorant-buy/) · [hướng dẫn của Riot](https://playvalorant.com/en-us/news/announcements/beginners-guide/) | Shop dày, nhưng vũ khí được xếp theo nhóm và thông tin của món đã chọn nằm cùng một chỗ. Việc mua gắn với giai đoạn chuẩn bị. | Phòng chờ, túi đồ và hồ sơ có thể chứa nhiều chi tiết khi người chơi chủ động mở. Mỗi màn vẫn cần một việc chính dễ tìm, ví dụ mời đồng đội hay sẵn sàng. |

## Hai bản thử cùng cảnh

A – **Chữ trên cảnh**: cảnh Factory làm nền, chữ có bóng và lớp tối cục bộ ở vị trí cần thiết. Đạn trong băng lớn hơn đạn dự trữ. B – **Cụm thiết bị**: cùng chữ, số và hai đích, nhưng dùng tấm nền lấy từ `HudSkin/Selected/Loadout` của dự án. B ổn định tương phản hơn; A để lộ nhiều cảnh hơn. Có thể phối A trên HUD với B cho terminal và màn xem đồ. Hai bản đều giữ ba nhóm ở các góc, tránh làm người chơi phải học vị trí mới khi đổi trạng thái.

Bộ chọn tình huống đổi đồng thời cả hai bản: 15 viên, Mason khỏe → 3 viên, Mason gục còn 18 giây. Giá trị và vị trí waypoint trong cảnh minh họa là dữ liệu mẫu; đây không phải chứng cứ tọa độ hay playtest. Click **Đối chiếu** để mở ảnh gốc client đúng trạng thái quan sát trong lần rà trước. Ảnh nền sân Factory và các sprite Warden, Frag, GoalMarkerCentre, Loadout cùng font số Teko lấy từ dự án; không sinh ảnh mới.

## Phạm vi rà soát từ khi mở game đến khi rời đội

| Chặng | Có căn cứ gì và cần kiểm tra gì | Bài thử tự nhiên trước khi quyết định sửa |
|---|---|---|
| Vào game, menu chính | [SCENE theo báo cáo MainMenu ngày 12/11] Menu chính dùng hành lang và ba lựa chọn. Kiểm tra màn khởi động/chờ, focus đầu, Settings và đường quay lại. | Bỏ chuột xuống, đi từ menu đến tạo phòng rồi quay lại bằng bàn phím hoặc tay cầm. |
| Tạo/join, lỗi, chờ và ready | [CODE LobbyUI.cs:252; PlayerRosterEntryView.cs:23] Tên được trim và ghi ngược khi đang nhập; mỗi ghế trống lặp hướng dẫn chia sẻ mã. | Gõ tên có dấu cách, nhập mã sai rồi sửa; một host + một client tự mời/ready, phân biệt vai trò và lỗi với trạng thái đang chờ. |
| Vào nhiệm vụ, chuyển pha, chỉ hướng | [PLAYTEST screenshot] Factory hiện mục tiêu chương và hai cặp nhánh/bước. [SCENE theo báo cáo 54] Ở pha này, đích ngoài màn hình ẩn; khi đủ độ cao có HIGHER/LOWER; không có mũi tên mép hay mét. | Người chơi mới nói được bước tiếp của từng nhánh; quay lưng rồi tự tìm lại; thử hai mốc chồng nhau, khác tầng, 4:3 và có phụ đề. |
| HUD cá nhân, đội, đổi đồ | [CODE HUDManager.cs:333; SquadHUD.cs:145] Cụm đồ có trọng lượng lớn; đầy máu/khỏe vẫn có chữ. | Sau một giây ở mỗi mẫu, hỏi còn mấy viên và ai cần cứu. Khi đổi món, người mới có tìm được phím hay không? |
| Tương tác, terminal, tài liệu/bằng chứng, thao tác giữ | Các bề mặt này cần thêm ảnh đúng trạng thái trước khi kết luận thẩm mỹ; kiểm tra prompt khi tới/đi khỏi thiết bị, chờ, khóa, hủy và xong. | Tới thiết bị, nhận ra có thể tương tác, làm một lần, lùi ra giữa chừng rồi quay lại; không gặp hai yêu cầu bấm trái ngược. |
| Kho đội, nhật ký, radio và phụ đề | [CODE TeamInventoryUI.cs:193,252] Mục `Campaign Record` nằm trong kho; [báo cáo phụ đề](./51-phu-de-radio-uu-tien-mot-dong.md) quy định ưu tiên một dòng. | Tìm lại một thông điệp radio đã nghe; mở tài liệu rồi Back; phụ đề được đọc hết khi hai mốc và lời nhắc cùng hiện. |
| Gục, cứu, chết, quan sát | [CODE CampaignStatusUI.cs:90; SpectatorController.cs:32] Có trạng thái cho chính người chơi gục/chờ cứu và đổi người khi xem. Đánh giá riêng hai đầu cứu/được cứu. | Host và client đổi vai: ai gục, còn bao lâu, đã bắt đầu cứu chưa, chết rồi xem ai. Không giả định game có nút hồi sinh riêng. |
| Tạm dừng, cài đặt, gán phím | [CODE InGameMenuUI.cs:51,111; SettingsUI.cs:415] Rời trận gọi disconnect trực tiếp; reset gồm cả binding. Hai việc này quan trọng hơn trang trí lại nút. | Người làm host hiểu hậu quả trước khi rời; client hiểu mình chỉ rời đội. Reset phải cho biết sẽ mất phím đã gán; Back không thoát khỏi trận nhầm. |
| Extraction, thắng/thua, retry, mất kết nối | [CODE CampaignStatusUI.cs:136,160] Host có thể thử lại khi thất bại; client chờ hoặc về menu. Kiểm tra cả chuyển cảnh và thông báo mất kết nối. | Kết thúc đủ hai kết quả, host/client hiểu ai được retry; client gặp host rời biết tại sao và làm gì tiếp theo. |

## Trình tự và tiêu chí quyết định

1. Vòng này chốt hướng thị giác cho HUD và mức dẫn đường mong muốn; chưa duyệt chữ, từng khoảng cách hoặc thay flow. Hai hướng trên có cùng dữ liệu để tách sở thích thị giác khỏi lợi ích của bớt thông tin.
2. Sau lựa chọn, lập bảng trạng thái: luôn hiện / chỉ hiện khi đổi hoặc có nguy / mở theo yêu cầu; vẽ màn đối chứng ngoài HUD (phòng chờ và một thiết bị). Duyệt riêng luồng có thay đổi hành vi, nhất là rời trận, reset và trợ giúp định hướng.
3. Dùng hành trình trong bảng để lập đủ bộ ảnh theo màn và trạng thái. Cho người biết FPS nhưng chưa chơi nhiệm vụ thử bằng cùng câu hỏi, đổi thứ tự A/B giữa người thử. Ghi câu trả lời sai, thao tác nhầm và thời gian tìm; không lấy nhận xét “đẹp hơn” thay cho khả năng đọc cảnh báo.
4. Chỉ khi flow và mockup được duyệt mới sửa Unity. Kiểm tra host/client, trước/sau Game View 1080p & 4K, thêm 4:3; cảnh sáng/tối, radio, nhiều dòng, tên dài, bàn phím/tay cầm. Kết quả kiểm tra trình duyệt của HTML không thay cho mốc này.

**Kết quả vòng hiện tại:** kiểm tra hai kiểu ở hai tình huống trong Edge, chiều rộng trình duyệt 1024, 375 và 320 px: số đạn và cảnh báo đổi đúng, các cụm nhiệm vụ/mốc/trang bị/máu không chồng nhau, ảnh gốc mở được bằng bàn phím. [SCENE] Lần kiểm tra đầu ghi nhận GameScene đang mở sạch; không vào Play Mode, không chỉnh code, scene hay prefab. Hình game khác là tư liệu quan sát tại ảnh hoặc GIF được dẫn; chẩn đoán tính dễ chịu là suy luận thiết kế. Toàn hành trình đã có phạm vi và kịch bản rà, chưa có bằng chứng hình ảnh cho mọi màn/trạng thái.

**Skill và quy tắc áp dụng:** `grilling`: hỏi theo vòng, đưa khuyến nghị, không hỏi lại điều đã chốt; `unity-mcp-ui-layout`: dùng asset đang có, kiểm tra độ ổn định theo vùng và tỉ lệ; `game-ui-ux`: xét cả luồng Back/focus và ghi độ phân giải thực kiểm; `visualize`: hai biến thể cùng cảnh, điều khiển trạng thái bằng bàn phím; `unity-skills`: kiểm tra trạng thái scene qua cầu nối chỉ đọc.