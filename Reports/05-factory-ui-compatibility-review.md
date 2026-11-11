# Đối chiếu kế hoạch Factory với UI đã triển khai

Ngày 03/11/2026, Asia/Saigon. Phạm vi: rà soát chỉ đọc; chỉ tạo báo cáo này, không sửa kế hoạch gốc, code, scene hoặc asset. [INFERENCE]

## Kết luận

**Nên cập nhật điểm xuất phát và phần bàn giao của kế hoạch; chưa có căn cứ yêu cầu làm lại chín giao diện. Các sửa logic Factory vẫn cần được xét riêng.** Kế hoạch đã cấm sửa Build, RectTransform, theme/font/icon/animation và SelectedLayout, nên không xung đột toàn bộ với UI mới. [CODE Reports/05-factory-flow.md:233] [INFERENCE]

Đã đọc chat được dẫn `01a1a036-3fce-7c03-ba52-ca4155c91fab`, tên “Lập kế hoạch luồng Factory”. Chat kết thúc ở Giai đoạn A; phản hồi đổi tên thành Northbridge không phải bằng chứng tám bước B đã được thực hiện. Lượt rà soát này không biến lời báo UI đã xong thành quyền triển khai flow. [INFERENCE]

## Bằng chứng hiện hành

- Báo cáo mới `Reports/UIUX/SelectedImplementation-2026-11-03/bao-cao.md` ghi cả chín màn đã triển khai và kiểm kỹ thuật; Journal là tab trong Túi đồ; phần UI không thay flow nhiệm vụ. Báo cáo này mới hơn các nhận định chưa hoàn thành trong `Reports/04-hud-design.md`. [CODE Reports/UIUX/SelectedImplementation-2026-11-03/bao-cao.md:3] [CODE Reports/UIUX/SelectedImplementation-2026-11-03/bao-cao.md:9] [CODE Reports/UIUX/SelectedImplementation-2026-11-03/bao-cao.md:11]
- Đã đọc trực tiếp bốn JSON kết quả lưu sẵn: composite `67a787f4`, nine-screens `321d37a2`, Editor `4c88e07e`, navigation `d5b27be5`: từng job `completed`, `totalTests=1`, `passedTests=1`, `failedTests=0`. Đây là bốn lần chạy riêng đã có, không phải test mới của lượt này hoặc toàn bộ suite. Không dùng lỗi navigation cũ để tuyên bố bản UI hiện tại còn lỗi đó. [CODE Reports/UIUX/SelectedImplementation-2026-11-03/composite-result-67a787f4.json:2] [CODE Reports/UIUX/SelectedImplementation-2026-11-03/nine-screens-result-321d37a2.json:2] [CODE Reports/UIUX/SelectedImplementation-2026-11-03/editor-result-4c88e07e.json:2] [CODE Reports/UIUX/SelectedImplementation-2026-11-03/navigation-result-d5b27be5.json:2]
- Đã xem ba ảnh mockup gốc HUD/Locked/Manifest, hai ảnh fixture Gameplay và ba ảnh ghép cuối `mockup-final-1.jpg`, `mockup-final-2.jpg`, `mockup-final-3.jpg`. Ảnh ghép thể hiện khung và vùng nội dung chín màn; dữ liệu gameplay và Journal tab khác ảnh minh họa. Việc xem ảnh không xác minh mọi thao tác hoặc mọi chữ ở 4K. [PLAYTEST Reports/UIUX/SelectedImplementation-2026-11-03/mockup-final-1.jpg] [PLAYTEST Reports/UIUX/SelectedImplementation-2026-11-03/mockup-final-2.jpg] [PLAYTEST Reports/UIUX/SelectedImplementation-2026-11-03/mockup-final-3.jpg]
- Đầu lượt, Unity-Skills 2.7.0, Unity 6000.5.6f1 báo một scene trống, `name=""`, `path=""`, `rootCount=0`, `isDirty=false`; Edit Mode, không compiling. Cuối lượt truy vấn lại: GameScene đang active, path `Assets/FPS/Scenes/GameScene.unity`, `rootCount=14`, `isDirty=false`, `isPlaying=true`, `isCompiling=false`. Trạng thái Editor đã đổi giữa hai lần đọc; lượt rà soát này không gửi lệnh load scene, Play, Test Runner hoặc save. Không suy ai đã đổi trạng thái. Kiểm metadata scene không thay thế playtest flow. [SCENE Assets/FPS/Scenes/GameScene.unity / isDirty=false / isPlaying=true]
- Đã hỏi local Codebase Memory bằng executable và cấu hình của dự án: `list_projects` trả `projects=[]`, `total=0`. Không có index để kiểm coverage; sau đó chỉ đọc source đã khoanh vùng từ kế hoạch và báo cáo. Git có nhiều thay đổi sẵn, kể cả HUD và Tank; không quy các thay đổi đó cho lượt này. [INFERENCE]

## Những chỗ nên sửa trong kế hoạch

| Mục | Điều chỉnh đề xuất | Cơ sở |
|---|---|---|
| Điểm xuất phát | Ghi chín UI đã triển khai là bản nền hiện hành; liên kết báo cáo SelectedImplementation, SelectedUiTheme, SelectedUiTypography và CampaignHUD.SelectedLayout. Không giao Bot 6 dựng lại các màn hoặc sửa lại lỗi đã có kết quả đạt. | [CODE Reports/UIUX/SelectedImplementation-2026-11-03/bao-cao.md:3] [CODE Reports/05-factory-flow.md:233] |
| B1–B4 và hợp đồng mục 5 | Chuyển cách diễn đạt “Bot 6 vẽ/hiển thị” thành nối dữ liệu vào widget sẵn có. Giữ panel, font, màu, vòng giữ và Journal trong Túi đồ; chỉ bổ sung trạng thái chưa có. | [CODE Reports/UIUX/SelectedImplementation-2026-11-03/bao-cao.md:9] [CODE Reports/05-factory-flow.md:135] [INFERENCE] |
| B4 và B7: phần UI bổ sung | Tách màn đối chiếu Shipping với dữ kiện/hai nhóm/lỗi từng nhóm và Pending/SpawnFailed/Retry Tank thành phần bổ sung riêng. Chín mẫu hiện có không tự mô tả đầy đủ các trạng thái này. Nếu cần thêm bố cục/control, trình mockup bổ sung trước khi code theo AGENTS.md; không mở lại toàn bộ thiết kế đã duyệt. | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:453] [CODE Reports/05-factory-flow.md:100] [CODE Reports/05-factory-flow.md:167] [INFERENCE] |
| B5: tên và chữ minh họa | UI hiện dùng Logistics, kế hoạch đề xuất Shipping. Khuyến nghị giữ Logistics làm tên hiển thị của nhánh theo UI đã chọn, giữ Shipping cho bước Verify Shipping Release; dùng cùng tên nhánh trong mọi surface. Giữ yêu cầu đổi An Lac thành Northbridge đã có trong kế hoạch. Không thêm khóa “đọc nguồn trước Isolate” chỉ để khớp ảnh minh họa. | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:259] [CODE Reports/05-factory-flow.md:108] [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:161] [INFERENCE] |
| Test/nghiệm thu | Dùng bốn bài UI đã đạt làm regression cho phần tích hợp; thêm Editor tests logic theo từng B và kiểm host/client cho flow. Không coi test chín màn là đã kiểm xong Factory gameplay. | [CODE Reports/UIUX/SelectedImplementation-2026-11-03/bao-cao.md:45] [CODE Reports/05-factory-flow.md:172] [INFERENCE] |

Các điểm trên là đề xuất rà soát, chưa sửa văn bản kế hoạch gốc hoặc gửi thông điệp sang chat khác. [INFERENCE]

## Logic còn thiếu dù giao diện đã xong

1. **Tank timeout vẫn tính là đã hạ:** khi spawn trả null và hết timeout, server đặt Resolved và `factoryTankDefeated=true`; Case kiểm đúng cờ này. B7 vẫn cần sửa nguồn xác nhận, cùng đường retry có giữ tiến độ. Đây là lỗi logic, không được khắc phục bằng hoàn thiện skin UI. [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.Tank.cs:60] [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:166]
2. **Shipping chưa có lỗi authoritative theo từng nhóm:** ShowResult suy nhóm sai từ lựa chọn local và chỉ trả một trong hai nhóm. Open chưa có guard riêng chặn dựng panel Shipping khi thiếu Manifest; server vẫn có prerequisite. B4 vẫn có việc cần làm. Hai index mặc định -1 và nhãn hai nhóm đã tồn tại, cần giữ chúng. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:441] [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:457] [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:599] [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:704] [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:165]
3. **Hint vẫn hai mức và có câu viết cứng trong HUD:** PresentHint dùng mốc 45/90 giây, mức đầu là chuỗi cố định. B6 ba mức từ catalog chưa được UI mới thay thế. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:840]
4. **T chưa luôn đổi nhánh:** khi ngắm vật khóa, T gọi TrackLockedObjective thay vì CycleTrackedBranch. UI có chữ T SWITCH không chứng minh hành vi B2 đã có. [CODE Assets/FPS/Features/Interaction/Runtime/InteractionManager.cs:63]
5. **Hoàn tất và tên vẫn cần nối dữ liệu:** thông báo hiện ghi “Recorded for the squad” không có tên bước; tracker Asylum vẫn có AN LAC và nhánh Logistics. B3/B5 không phải việc đã được chín màn giải quyết hết. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:702] [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:155] [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:259]

## Skill đã đọc và quy tắc áp dụng

| Skill | Quy tắc đã dùng trong rà soát |
|---|---|
| [level-design](C:/Users/hieud/.codex/skills/level-design/SKILL.md) | Phân biệt prerequisite/critical path với cách trình bày; không dùng mockup để tự đổi gate. |
| [unity-mcp-ui-layout](C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md) | Đối chiếu bản triển khai mới với nguồn đã duyệt; tách sửa cục bộ và thiết kế mới; không lấy ảnh chụp thay cho kiểm hành vi. |
| [game-ui-ux](C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md) | Đối chiếu dữ liệu trạng thái và điều hướng/input với UI; giữ đường Journal/tab hiện hành. |
| [fps-shooter](C:/Users/hieud/.agents/skills/fps-shooter/SKILL.md) | Áp dụng phần authority server cho kết quả encounter Tank; không tune súng hoặc combat. |
| [unity-skills](C:/Users/hieud/.agents/skills/unity-skills/SKILL.md) | Đọc health/meta, dùng truy vấn Editor chỉ đọc, không đọc scene YAML. Recommend bị timeout; tham số hai truy vấn được xác định từ module docs. |
| [unity-scene](C:/Users/hieud/.agents/skills/unity-skills/skills/scene/SKILL.md) | scene_get_loaded xác minh path/isDirty; không lưu scene trống. |
| [unity-editor](C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md) | editor_get_state xác minh Edit Mode/compile; không gọi Play/refresh/test. |
| [ponytail](C:/Users/hieud/.codex/plugins/cache/ponytail/ponytail/5.3.3/skills/ponytail/SKILL.md) | Rà đúng phần tương thích plan/UI, tái sử dụng kết quả và widget; không thêm implementation hoặc dependency. |

## Tự kiểm mâu thuẫn

- “Chín giao diện hoàn tất trong phạm vi UI” không có nghĩa logic B1–B8 đã thực hiện. Báo cáo UI mới tự ghi không thay flow; source vẫn có các khoảng trống cụ thể phía trên. [CODE Reports/UIUX/SelectedImplementation-2026-11-03/bao-cao.md:11] [INFERENCE]
- Báo cáo UI cũ ghi thiếu và test lỗi; báo cáo SelectedImplementation có bằng chứng đạt mới hơn. Không yêu cầu làm lại các phần chỉ dựa trên báo cáo cũ. [CODE Reports/UIUX/SelectedImplementation-2026-11-03/bao-cao.md:65] [INFERENCE]
- Có dữ kiện và đáp án trong tài liệu Manifest đã đọc là cần thiết cho đối chiếu. Không gọi đó là rò đáp án để rồi xóa dữ kiện; B5 cần phân biệt tài liệu nguồn với tracker/hint/lời chỉ đáp án. [CODE Reports/05-factory-flow.md:264] [INFERENCE]
- Giữ UI đã duyệt không buộc chép trạng thái Isolate khóa trong ảnh: CanUse hiện không có điều kiện file nguồn cho Isolate; đổi điều kiện đó là thay flow riêng. [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:161] [INFERENCE]

## Chưa kiểm được

- Không chạy test mới, không playtest Factory/host-client hai máy, không nghe âm, không đo NavMesh hoặc kiểm object/flow của GameScene hiện tại. Chỉ đọc kết quả đã lưu, xem ảnh có sẵn và kiểm metadata scene. GameScene kết thúc lượt kiểm với `isDirty=false`; Editor đang Play Mode do trạng thái đã thay đổi giữa các truy vấn, không phải lệnh của lượt này. [SCENE Assets/FPS/Scenes/GameScene.unity / isDirty=false / isPlaying=true] [INFERENCE]
- Không xác nhận toàn bộ UI giống từng pixel hoặc mọi chuỗi ở 4K; review này không yêu cầu thay mỹ thuật của chín màn đã được người dùng chấp nhận. Rủi ro còn rõ trong source là timeout Tank làm mở Case sai điều kiện. [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.Tank.cs:63] [INFERENCE]
