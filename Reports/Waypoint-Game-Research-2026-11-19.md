# Nghiên cứu waypoint nhiệm vụ trong game — 19/11/2026

Câu hỏi: ở đoạn đầu của FPS, làm sao để người chơi biết **đang theo điểm nào**, **phải đi qua đâu** và **quay sang hướng nào** khi điểm ra ngoài khung? Đây là ba thông tin khác nhau. Phạm vi khảo sát là chỉ dẫn nhiệm vụ trong hành lang, phòng và cầu thang; không lấy ping đồng đội hay dẫn đường lái xe làm mẫu chính.

| Game / bằng chứng | Cách dẫn đường | Điều áp dụng được cho FPS |
| --- | --- | --- |
| **Rainbow Six 3: Raven Shield** (PC, nhiệm vụ chơi đơn) — [hướng dẫn Ubisoft, mục Waypoints and Planning, tr. in 22](https://store.steampowered.com/manual/19830#page=13) | Mốc hiện tại là vòng tròn kèm số mét khi trong tầm nhìn. Khi mốc ra ngoài tầm nhìn, mũi tên ở mép cho biết phải quay về phía nào để thấy lại cùng điểm đó. | Ví dụ sát nhất với hiện tượng tròn/nhọn của FPS. Hai hình biểu thị cùng một mốc trong kế hoạch nhiệm vụ. Tài liệu không cho biết game chống đổi qua lại ở mép ra sao. |
| **Halo 3** — [đặc tả HUD do David Candland và Jaime Griesemer soạn, mục 12, tr. 22](https://static1.squarespace.com/static/5aa416faf8370ab26ce4c7dc/t/5ab3400d562fa79e4d5ada7f/1521696785017/Halo3_HUD_spec.pdf#page=22) | Đặc tả ghi giữ mũi tên có kích thước thay đổi từ Halo 2. Với multiplayer, tài liệu chỉ định hình thoi khi mục tiêu bị che và biểu tượng mục tiêu khi nhìn thấy. Đây là đặc tả của đội thiết kế, không phải phép đo bản phát hành. | Trạng thái bị tường che và trạng thái ra khỏi khung là hai chuyện khác nhau. Dùng thành phần chỉ hướng ở mép để trả lời “quay về đâu”. Tài liệu không xác nhận ngưỡng chuyển hay vùng đệm. |
| **Warframe** — [patch PC 19.11.5, 28/02/2017](https://www.warframe.com/en/patch-notes/pc/19-11-6) và [Developer Workshop có video minh họa](https://forums.warframe.com/topic/765560-waypoint-markers-coming-changes/) | Digital Extremes giảm số góc nhỏ lấy từ NavMesh, đặt marker ở giữa cửa và làm chuyển động giữa các mốc dần hơn. Bài viết cũng nêu việc có điểm thể hiện sai phía và việc tính tuyến khi người chơi rời NavMesh. | Trên đường qua nhiều phòng, điểm người chơi cần nhìn là **cửa đi tiếp**, không phải góc nhỏ của lưới. Làm mượt có ích sau khi chọn đúng điểm; cần kiểm tra trước việc chuyển mốc tại cửa và cầu thang. |
| **Tom Clancy’s The Division** (bản 2016) — [Anders Johansson, Lead UI Designer, Game UX Summit 2016](https://www.youtube.com/watch?v=ZDahvtIcLnk) và [ghi chép của Celia Hodent](https://celiahodent.com/game-ux-summit-2016-ux/) | Với chỉ dẫn trong nhà hoặc trong nhiệm vụ, đội UI mô tả một icon 2D phát nhịp, di chuyển về phía mục tiêu và để lại vệt để người chơi đọc hướng. | Nếu đánh dấu cửa vẫn không đủ ở khu dễ lạc, thử một đoạn dấu dẫn ngắn tại chỗ rẽ. Mẫu này không buộc FPS phải vẽ vệt suốt tuyến. |
| **Gears 5: Escape** — [The Coalition, tài liệu Accessibility, mục Navigation Ping](https://www.gearsofwar.com/en-us/games/gears-5/accessibility/) | `Next Doorways` dùng tín hiệu âm thanh trong không gian để chỉ lối ra khỏi phòng hiện tại. `Full Path` dùng tín hiệu dày hơn để dẫn quanh tường và vật cản. Hai mức có thể luôn bật hoặc gọi bằng Tac-com. | Bắt đầu với mức chỉ cửa. Dẫn từng đoạn là mức hỗ trợ riêng khi bố cục khó; ví dụ này nói về độ chi tiết của chỉ dẫn, không đề xuất thay hệ thống âm thanh của FPS. |
| **DOOM Eternal** — [Bethesda Support: không tiến được tới mục tiêu](https://help.bethesda.net/app/answers/detail/a_id/49889/~/what-do-i-do-if-i-cant-progress-in-an-objective-in-doom-eternal) | Bethesda lưu ý marker có thể trông gần dù thực tế ở sàn cao hơn hoặc thấp hơn, và hướng người chơi kiểm tra bản đồ để xác định đúng vị trí. | Khoảng cách ngắn không chứng minh người chơi đã tới đúng tầng. Nếu đích ở tầng khác, chỉ cầu thang/lối lên tầng và giữ gợi ý cao/thấp khi cần. |

Video đối chiếu thêm: [Halo Infinite – Campaign Overview, 03:42](https://www.youtube.com/watch?v=tgn8VOv9dc0&t=222s) có marker hình thoi đỏ trên một công trình trong khung hình. Đây là footage giới thiệu năm 2021; khung này không xác nhận cách game đổi marker khi điểm ra khỏi màn hình.

## Đề xuất cho đoạn đầu

[CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:329] Hiện tại hai điều kiện bật icon độc lập: mũi tên dùng cho điểm ngoài vùng HUD, góc rẽ hoặc mất đường; khi mất đường và đích vẫn trong vùng, hai sprite có thể cùng bật. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:281] Việc qua góc chỉ xét khoảng cách theo mặt phẳng ngang. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:322] Né HUD có thể dời vị trí hiển thị nhưng không đổi điểm thế giới mà mũi tên lấy hướng.

[INFERENCE] Mẫu phù hợp nhất là **chỉ báo hướng ở mép kiểu Raven Shield + đánh dấu cửa tiếp theo kiểu Warframe**. Mẫu hành vi để xem xét:

| Tình huống | Người chơi thấy gì |
| --- | --- |
| Điểm kế tiếp còn trong khung | Một marker với hình cố định, đặt tại cửa/lối rẽ có ý nghĩa hoặc tại đích cuối. Mỗi lần chỉ có một điểm đang chỉ. |
| Điểm đó ra ngoài khung | Cùng nhận diện waypoint ở mép, kèm mũi tên nhỏ chỉ hướng quay. Chừa dải đệm giữa ngưỡng bật và tắt mũi tên để dao động nhẹ quanh mép không đổi trạng thái liên tục. Tách việc né HUD khỏi quyết định có cần mũi tên. |
| Vừa đi qua cửa hoặc tới chiếu nghỉ đúng tầng | Điểm dẫn mới chuyển sang lối kế tiếp. Việc lia camera không làm đổi cửa đang được dẫn tới. |
| Không tìm được tuyến hoàn chỉnh | Không vẽ hai icon lên nhau. Nếu giữ hướng về đích và nhãn `TO TARGET`, đó là hướng và khoảng cách thẳng; không trình bày nó như lối đi qua tường. |

Thứ tự thử: (1) ổn định nhận diện và chuyển trạng thái ở mép; (2) kiểm tra cửa và cầu thang đầu game, chỉ chỉnh mốc ở nơi người mới hay đi sai. Một vệt dẫn theo yêu cầu chỉ đáng thử nếu đánh dấu cửa chưa đủ. Đây là đề xuất để thử trong project, không phải khẳng định các game trên dùng cùng một thuật toán.

Kiểm tra với người chưa biết map: đứng yên lia qua mép nhiều lần; từ một phòng có nhiều cửa, yêu cầu họ chỉ cửa sẽ chọn; đi tới chỗ hai tầng chồng nhau rồi qua cầu thang. Ghi lại số lần icon đổi khi đứng yên, cửa sai đầu tiên và đoạn mất hướng. Khi có bản sửa UI, kiểm tra Game view 1080p/4K và thử đoạn đó trên host/client.

Skill đã áp dụng: `game-ui-ux` — xét vùng an toàn, khả năng đọc HUD và kiểm tra ở nhiều độ phân giải. Đây là khảo sát tài liệu nhà phát triển và ghi chép buổi trình bày; chưa thử trực tiếp từng game hay đo chuyển trạng thái của bản phát hành. Chưa thay đổi code, scene hoặc luồng nhiệm vụ.
