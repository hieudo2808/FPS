# Factory — rà mức hiểu nhiệm vụ, hướng dẫn và narrative

Ngày 03/11/2026, Asia/Saigon. **Chỉ rà tài liệu/source và sửa kế hoạch; chưa sửa gameplay, UI, scene hoặc catalog.** Kế hoạch đã cập nhật tại [05-factory-flow.md](E:/Unity/Project/FPS/Reports/05-factory-flow.md), mục 3.2–3.3, B3/B5/B6, E07/E12/E18/E19 và H01–H08. [SUY LUẬN]

## Kết luận

**Khung flow có cơ sở, nhưng chưa đủ bằng chứng để kết luận người mới sẽ hiểu.** Hai nhánh tự chọn thứ tự, dữ kiện trước lựa chọn, lý do khóa, feedback và quyền server là nền hợp lý. Phần thiếu là chuyển các điều kiện kỹ thuật thành câu trả lời tại mỗi beat: làm gì, tìm đúng vật nào, vì sao cần làm và thấy gì khi xong. Đi theo waypoint hoặc vượt qua Editor tests chưa chứng minh hiểu nhiệm vụ. [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:153] [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:213] [SUY LUẬN]

Không cần làm lại chín màn đã được chấp nhận. Các cải thiện trước hết là nội dung/trạng thái nối vào tracker, prompt, panel, Missions, Files và Journal hiện có. Việc bổ sung bố cục/control thực sự thiếu vẫn theo phạm vi mockup riêng trong plan. [CODE Reports/UIUX/SelectedImplementation-2026-11-03/bao-cao.md:3] [SUY LUẬN]

## Phạm vi và nguồn kiểm

- Đã đọc chat **“Lập kế hoạch luồng Factory”** (`01a1a036-3fce-7c03-ba52-ca4155c91fab`, local) trước khi dùng: chat kết thúc ở kế hoạch, chưa thực hiện B1–B8. Nội dung chat chỉ là đầu vào; các nhận định dưới được đối chiếu source/canon. [SUY LUẬN]
- Đọc AGENTS.md, plan hiện hành, báo cáo tương thích UI và nguồn SelectedImplementation; narrative đối chiếu [Master Canon](E:/Unity/Project/FPS/Documents/Outbreak-Protocol-Master-Canon.md). Không lấy báo cáo UI lịch sử làm bằng chứng giao diện hiện vẫn thiếu. [SUY LUẬN]
- Đã truy vấn Codebase Memory trước source: danh sách project indexed rỗng, nên fallback sang các file đã được plan chỉ ra; không grep đệ quy toàn repo. Source là working tree có thay đổi từ các phiên trước, không khẳng định giống HEAD hoặc đã chạy trong GameScene. [SUY LUẬN]
- Unity-Skills chỉ query metadata. Đầu lượt có scene Test Runner sạch đang Play; không lưu/dừng/đụng hoạt động đó. Lần kiểm kế tiếp MainMenu active, `isDirty=false`; không load GameScene để tránh thay đổi trạng thái Editor. Trạng thái cuối lượt ghi ở phần xác minh. [SCENE Assets/FPS/Scenes/MainMenu.unity / isDirty] [SUY LUẬN]

## Các điểm cần sửa trong plan

| Điểm | Bằng chứng và ảnh hưởng | Đã bổ sung vào kế hoạch |
|---|---|---|
| Hướng dẫn đầu chưa đủ cụ thể | `Inspect the electrical isolation panel` nói vật để inspect; `Configure the cold-store backup` chưa nói trạng thái cần tạo. Đây là copy source, chưa phải kết luận rằng người chơi thực tế bị lạc. | Mỗi beat có động từ, tên vật và kết quả; một việc đang tracked, vẫn thấy cả hai nhánh. Bảng mục 3.2 không hứa vị trí chưa kiểm scene. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:215] [SUY LUẬN] |
| Không nên đợi hint mới hiểu việc đầu | `PresentHint` chỉ chạy từ 45 giây; 45–90 giây dùng câu chung “Cross-check the linked source before comparing fields.” Câu này không dạy thao tác điện. | B6 có chỉ dẫn cơ bản ngay; 45/90/135 chỉ hỗ trợ khi mắc kẹt. Hint Power nói nguồn/trình tự; Shipping nói nguồn/nhóm trường; không đẩy chỉ dẫn puzzle vào fallback HUD. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:840] [SUY LUẬN] |
| Lời thoại có thể kéo về việc đã xong | D05 trong canon yêu cầu verify Logistics sau Generator; controller phát dialogue theo objective, không chọn theo nhánh còn thiếu tại call site này. Chưa đọc catalog live nên đây là rủi ro nội dung có bằng chứng, chưa phải bug đã tái hiện. | D05 đề xuất trung tính “Cold-store power is restored. Keep the evidence case sealed.”; Next dựa state mới, thử cả hai thứ tự. Không thêm dialogue schema/runner. [CODE Documents/Outbreak-Protocol-Master-Canon.md:179] [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:642] [SUY LUẬN] |
| Luật AFK và lời gọi tập hợp cần cùng nghĩa | D18 và canon boarding yêu cầu mọi người sống, trong khi B8 đề xuất tiếp tục với tập đủ điều kiện sau timeout. Nếu giữ cả hai sẽ hứa sai. | D18 trung tính, tracker giải thích người còn thiếu/countdown/override; patch đối chiếu cả quy tắc canon. Dialogue dùng chung phải kiểm các chương khác; không tự lan luật AFK mới. [CODE Documents/Outbreak-Protocol-Master-Canon.md:192] [CODE Documents/Outbreak-Protocol-Master-Canon.md:208] [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:202] [SUY LUẬN] |
| Chống lộ đáp án quá rộng sẽ làm người chơi mù thông tin | Power không có nhánh chấm answer trong TryComplete; Shipping có. Manifest là dữ kiện thật cần hiện cạnh lựa chọn, không phải thứ phải xóa để giữ bí mật. | B5 phân loại theo vai trò và điều kiện xuất hiện. Giữ động từ/thứ tự Power, tài liệu đã ghi, choices và tên đã biết; chỉ bỏ guidance gợi sẵn giá trị đáp án đối chiếu. [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:204] [CODE Documents/Outbreak-Protocol-Master-Canon.md:53] [SUY LUẬN] |
| Waypoint đúng chưa đủ nhận diện vật | Source tạo nhãn TextMeshPro nổi; nhãn Route là `EXIT / RADIO`, khác instruction `service-route control cabinet`. Đèn nhấn mục tiêu chỉ bật khi near/current. Chưa xác minh GameScene hiện dùng các đường này. | H04 kiểm nhìn ra body trước khi ngắm, ba hộp phân biệt được, tên khớp giữa prompt/marker/nguồn. Dùng signage/asset hiện có; phần art/scene vẫn là đề xuất, không thêm nhãn nổi hoặc tự tạo art. [CODE Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:111] [CODE Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:131] [CODE Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:149] [CODE Assets/FPS/Features/Missions/Runtime/CampaignDialogue.cs:95] [SUY LUẬN] |

## Narrative và nhịp trải nghiệm

Động cơ xuyên Factory là **thu hồi bằng chứng, bảo toàn case và hồ sơ**, rồi lần theo tuyến vận chuyển. Utilities Procedure giải thích giữ mạch lạnh và cảnh báo đèn Generator không đáng tin; Manifest chỉ rõ hồ sơ gốc có thông tin bản rút gọn bỏ đi; addendum đưa tới B2 và hồ sơ còn ở viện. Các chi tiết này có quan hệ với hành động, có thể giúp người chơi hiểu lý do thay vì chỉ hoàn thành checklist. Hiệu quả thực tế vẫn cần H06. [CODE Documents/Outbreak-Protocol-Master-Canon.md:53] [CODE Documents/Outbreak-Protocol-Master-Canon.md:57] [CODE Documents/Outbreak-Protocol-Master-Canon.md:61] [SUY LUẬN]

Giữ tiết lộ theo bằng chứng đã ghi, không khóa thêm nguồn để ép thứ tự kể chuyện. Tài liệu required hiện được phép đọc từ nguồn vật lý hoặc console đã đăng ký. Nếu addendum hợp lệ được tìm sớm, hiểu biết theo file đó; không cấm chỉ vì nhịp dự kiến đặt ở Case. “Ghi file cho đội” cũng không chứng minh từng người đã đọc, nên người vào muộn cần facts và cách đọc lại, không cần phát lại toàn bộ radio. [CODE Documents/Outbreak-Protocol-Master-Canon.md:49] [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:552] [SUY LUẬN]

Tank là cổng combat bắt buộc theo canon sau hai nhánh; chưa có bằng chứng “tiếng máy phát gây Tank” hoặc Tank chắn Case bằng vật lý. Plan chỉ cần công bố Pending/Active và mục tiêu sống thật, không bịa quan hệ nhân quả để hợp thức hóa gate. Giữ lạnh/niêm phong bảo toàn custody, không chứng minh nội dung an toàn. Không thêm lời hứa chữa bệnh, cứu mọi bệnh nhân hoặc NPC còn sống từ tên người ký. [CODE Documents/Outbreak-Protocol-Master-Canon.md:206] [CODE Documents/Outbreak-Protocol-Master-Canon.md:97] [CODE Documents/Outbreak-Protocol-Master-Canon.md:11] [CODE Documents/Outbreak-Protocol-Master-Canon.md:20] [SUY LUẬN]

Nhịp dự kiến: làm quen thao tác/đọc nguồn → vận dụng ở nhánh kia → áp lực Tank → thu case và manh mối → chuẩn bị/chờ tuyến rời đi. Cả hai nhánh là bắt buộc, quyền chọn thứ tự không biến nhánh kia thành optional. Controller có thể yêu cầu crescendo sau Generator/Shipping/Case; chưa đo xem nó ngắt đọc/lời thoại thế nào. H08 thu thời gian và chỗ mất thông tin trước khi đề nghị sửa; lượt này không tune AI, audio, tài nguyên hoặc thời gian combat. [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:635] [SUY LUẬN]

## Phần đã sửa và cách kiểm tiếp

1. Mục 3.2–3.3 bổ sung hợp đồng người chơi hiểu và copy D05/D18 đề xuất; giữ hai nhánh, authority, prerequisite và migration B2/B7. [SUY LUẬN]
2. B3/B5/B6 và phần bàn giao đã gắn nội dung vào hệ thống có sẵn; E07/E12/E18/E19 mở rộng kiểm cả Next, nội dung hiện ngay, thứ tự nhánh và dialogue. Giữ đủ 29 test ID E01–E29; chưa chạy chúng. [SUY LUẬN]
3. H01–H08 tách kiểm mức hiểu khỏi test code: 5 giây, hai thứ tự nhánh, đối chiếu thực sự, nhận diện prop, hold/feedback, narrative, chờ/khôi phục và nhịp đọc. Pilot ba người mới là bước phát hiện lỗi, không phải bằng chứng thống kê cho mọi người chơi. Không coaching; báo từng thất bại. [SUY LUẬN]

## Skills đã đọc và quy tắc thực sự dùng

| Nguồn | Quy tắc áp dụng |
|---|---|
| [level-design](C:/Users/hieud/.codex/skills/level-design/SKILL.md) | Giữ critical path/gate rõ; dạy thao tác trước khi thử dưới áp lực; không tự thay geometry. |
| [pacing-and-flow](C:/Users/hieud/.codex/skills/level-design/references/pacing-and-flow.md) | Phân biệt reachability với readability; đo nhịp và quan sát người chơi trước khi chỉnh áp lực. |
| [onboarding-tutorial-design](C:/Users/hieud/.codex/skills/onboarding-tutorial-design/SKILL.md) | Tách kiến thức cần ngay/cần sau, học qua hành động, cho đọc lại; kiểm hiểu thay vì chỉ làm theo. |
| [game-ui-ux](C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md) | Trạng thái/input/feedback phải đọc được, Next theo state mới; giữ input stack và điều hướng đã có. |
| [unity-mcp-ui-layout](C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md) | Chỉ dùng phần ranh giới sửa cục bộ/tái sử dụng UI cho kế hoạch bàn giao; không triển khai layout hay gọi quy trình build mockup. |
| [ponytail](C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md) | Sửa plan và assertions liên quan, dùng hệ thống/catalog sẵn; không thêm tutorial framework, dependency hoặc dialogue runner. |
| [unity-skills](C:/Users/hieud/.agents/skills/unity-skills/SKILL.md), [scene](C:/Users/hieud/.agents/skills/unity-skills/skills/scene/SKILL.md) | Query metadata trước; không save scene Test Runner, không thay Editor state để làm review. |

Không dùng một skill narrative riêng; phần narrative là đối chiếu canon. Không nhận đã áp dụng fps-shooter/save-systems/audio-design/game-feel trong lượt rà này; giữ nguyên các hợp đồng đã có, không mở rộng thực hiện. [SUY LUẬN]

## Xác minh tài liệu

Kiểm hai tài liệu: đủ tám mục B1–B8, 29 test ID E duy nhất và tám H01–H08; 59 liên kết cục bộ đều tồn tại; 99 dẫn chứng CODE trỏ tới file/dòng trong phạm vi; không có whitespace cuối dòng; cả hai có mục Tự kiểm mâu thuẫn và Chưa kiểm được. Đây là kiểm cấu trúc/đường dẫn, không tự chứng minh mọi nhận định source là hành vi runtime. Không chạy test gameplay mới. [SUY LUẬN]

Cuối lượt Unity-Skills trả một scene MainMenu loaded/active, `isDirty=false`, `isPlaying=false`, `isCompiling=false`. Không gọi save, Play, Test Runner, refresh, compile hoặc load scene. Hai file do lượt này sửa là plan và báo cáo review này; các diff source/canon có sẵn trong working tree không được nhận là công việc của lượt rà. [SCENE Assets/FPS/Scenes/MainMenu.unity / isDirty] [SUY LUẬN]

## Tự kiểm mâu thuẫn

- Giữ quyền chọn nhánh và một Next hiện hành; chỉ dẫn không buộc Power trước hoặc mở thêm objective tutorial. [SUY LUẬN]
- “Không lộ đáp án” không có nghĩa bỏ tài liệu, choices, tên đã biết hoặc động từ Power. B2 là tiết lộ theo nguồn, không thêm gate thu file. [SUY LUẬN]
- D05 trung tính bảo toàn ý nghĩa Journal đã ghi; D18 trung tính đi cùng reason/override thật, không thay điều kiện hồi sinh. [SUY LUẬN]
- Không dùng MainMenu sạch, code source hay bốn job UI lịch sử để nhận level đã dễ hiểu; copy mới là đề xuất, chưa được duyệt runtime. Chín màn đã chấp nhận vẫn là nền. [SUY LUẬN]

## Chưa kiểm được

- Chưa xem catalog asset/serialized overrides hiện hành, GameScene/body/landmarks/NavMesh, nhận diện trước khi ngắm, khoảng cách đi bộ, hoặc ảnh 1080p/4K mới. [SUY LUẬN]
- Không chạy Editor/PlayMode tests, host/client, Tank/AFK/checkpoint hay pilot người mới; E01–E29 và H01–H08 đều chưa có kết quả mới. Chưa thể kết luận người chơi hiểu chỉ từ bản sửa plan. [SUY LUẬN]
- Không triển khai nội dung D05/D18/hint hoặc sửa source/canon/asset. Việc cập nhật plan được yêu cầu không phải phê duyệt code/gameplay của flow mới. [SUY LUẬN]
