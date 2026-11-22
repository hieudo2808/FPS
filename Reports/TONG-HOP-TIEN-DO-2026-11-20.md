# Trạng thái các hạng mục FPS — 20/11/2026

Đối chiếu báo cáo, artifact còn lưu và một số source đã khoanh vùng. Đây là kiểm kê tiến độ, **không phải regression hoặc playtest toàn dự án mới**. Giữ nguyên bộ FINAL Director/DDA đã chốt; không sửa manifest của nó.

“Đã nghiệm thu” dưới đây chỉ phạm vi đã được người dùng chấp nhận trong báo cáo lịch sử. “Đã triển khai” không tự đồng nghĩa hết bug hoặc đã qua toàn bộ cổng nghiệm thu hiện tại.

| Hạng mục người dùng nêu | Trạng thái hiện tại | Đã có | Còn mở |
|---|---|---|---|
| Game còn giật lag | **Chưa giải quyết dứt điểm** | Benchmark Director bốn process và tối ưu allocation riêng sampling | Game vẫn giật theo phản hồi người dùng; frame time chưa cải thiện, có spike Director8,196ms; chưa khoanh toàn bộ nguyên nhân CPU/GPU/GC/loading |
| Tối ưu HUD | **Đã cải thiện nhiều; chưa đóng toàn bộ** | Missions/tracker/device, tránh chồng nhóm, ADS giảm tracker; acceptance lịch sử14/14; waypoint hai đích Factory mới đã kiểm host/client riêng | Chưa có nghiệm thu toàn HUD hiện tại sau mọi thay đổi; báo cáo13 còn lỗi CYCLE/G hotbar; chưa kiểm hôm nay lỗi ấy còn hay đã sửa; chưa có benchmark chi phí HUD riêng |
| Anim healing/medkit/injection | **Có triển khai; chưa đủ nghiệm thu** | Code owner self-use gọi heal/injection; có viewmodel và fallback trình diễn | Chưa xác nhận asset binding/runtime mọi prefab, heal đồng đội và góc nhìn3P/co-op; có code không chứng minh animation đang chạy đẹp |
| Câu từ nhiệm vụ | **Đã sửa và đưa vào game trong phạm vi đã làm** | Tên gọi/callsign, radio28mã và166nhịp, dữ liệu EN/VI; chỉ dẫn bỏ cách nói log kỹ thuật; phụ đề ưu tiên1dòng; test một số chuỗi host/client | Chưa chơi hết chiến dịch để duyệt mọi câu theo ngữ cảnh; không gọi tất cả câu mọi màn đã chốt |
| Tối ưu hiệu năng tổng thể | **Mới làm một phần** | Đo Director/pressure, sửa foreach→for loại allocation riêng; đầy đủ báo cáo benchmark | Chưa có tối ưu/tái đo toàn game trên workload chơi thật; không có bằng chứng tăngFPS; HUD/animation/render/lighting/network đều có thể góp tải nhưng chưa quy nguyên nhân |
| Điều chỉnh phân bổ nhu yếu phẩm | **Đã triển khai và test tính năng** | Hộp đạn nạp súng đang cầm; dự trữ mở theo đội khó; tránh hiện/ẩn trong đường nhìn; báo cáo13/11 có host/client | Ngưỡng cân bằng còn ban đầu; chưa playtest cân bằng3–4người và các chapter đầy đủ; có câu AmmoNotTaken cũ được ghi nhận. Một số raw test path lịch sử hiện không còn để kiểm lại |
| Tool kiểm thử AI Director/DDA | **Đã có công cụ và hồ sơ FINAL; chưa nghiệm thu toàn SPEC** | Harness Player thật, benchmark10/30/70địch, CSV/JSONL/log, calibration/latency, biểu đồ;18cửa sổ/120trial; snapshot/hash bảo toàn | Cần đủ telemetry boundary/lifecycle/DDAítkill/special/observe-only regression; tool không tự thay bằng chứng từng kịch bản |
| Âm nhạc/SFX | **Đã làm phần lớn; còn polish** | Mixer/bus/pool, nhạc theo phase/Tank, súng rút/bắn/nạp, hurt/down/death4nhân vật; audio20/11 có9/9testgốc | Mix nghe thật, ducking, âm va bề mặt/bước chân theo vật liệu và một số music co-op; Downed dùng lại HurtHeavy, chưa có take riêng |
| Recoil | **Có hệ thống/tuning; chưa đủ nghiệm thu cuối** | Pattern/stability/context/recovery và tách camera cosmetic; source hiện có | Chưa tìm thấy báo cáo nghiệm thu recoil riêng trong phạm vi rà; cần spray/playtest thực tế và xác nhận tuning/network trước chốt cảm giác |
| React từng bộ phận khi bắn | **Đã xong trong phạm vi duyệt; có nghiệm thu lịch sử** | Bone reaction, máu theo vùng và tác động gameplay nhẹ; báo cáo test Editor/host/client và người dùng chấp nhận14/11; raw log còn | Chưa chạy lại regression hôm nay; prediction máu owner chưa làm, phản hồi theo vòng mạng |
| Death/Downed người chơi | **Đã xong luồng chính; có nghiệm thu lịch sử** | Gục dùng Classic, revive trả súng, death theo vùng/hướng; sửa pose3P; test host/client, raw log còn | Death trên không/va xuyên tường, pitch3P khi gục và playtest bắn chuột thật lúc gục còn được báo cáo là giới hạn |
| Material nhìn xuyên một mặt | **Đã sửa các nhóm lỗi xác định; chưa chốt sạch toàn game** | Vỏ nhà/props/rào/tấm một mặt; các đợt sửa khe hở/chồng mặt/culledshader; có ảnh map lưu | Chưa Play host/client sau các đợt map mới nhất, chưa phủ mọi góc nhìn/asset; không gộp mọi transparency vào một nguyên nhân |
| Main menu background/effect | **Đã triển khai theo báo cáo** | Nền hành lang render từ project, pan/sương chuyển động, focus pointer/keyboard; báo cáo ghi kiểm1080p/4K | Raw test/capture menu được dẫn trong báo cáo hiện không còn; cần xác nhận lại bản hiện tại nếu ký nghiệm thu mới |
| Light không phù hợp/chỗ map quá tối | **Đã sửa nhiều vùng; vẫn để mở** | Chỉnh các góc Factory, bake lại, tăng sáng hall bảo trì bằng6đèn có sẵn | Đợt17/11 chỉ camera Editor, chưa playtest mạng; cần kiểm lại vị trí người dùng còn không thấy đường, không thể gọi toànmap đã ổn |

## Bằng chứng để mở lại từng nhóm

| Nhóm | Báo cáo/nguồn |
|---|---|
| Director và hiệu năng | [FINAL](FINAL/AI-Director-DDA/BAO-CAO-NGHIEM-THU.md), đặc biệt ma trậnNT/SPEC và spike/cadence |
| HUD | [Báo cáo13](13-ui-missions-tracker-device-2026-11-12.md), [waypoint20/11](54-waypoint-tai-dich-factory-2026-11-20.md) |
| Nhiệm vụ/radio | [43](43-sua-loi-thoai-ten-goi-2026-11-18.md), [49](49-trien-khai-radio-ban-48.md), [51](51-phu-de-radio-uu-tien-mot-dong.md) |
| Phân bổ đồ | [15](15-nhu-yeu-pham-director-2026-11-13.md); báo cáoSupplies18/11 chỉ dọn hìnhảnh tắt, không phải retune phân bổ |
| Audio | [19](19-am-thanh-2026-11-14.md), [23](23-am-thanh-sung-moi-2026-11-15.md), [CharacterAudio20/11](CharacterAudio-2026-11-20/implementation.md) |
| Hit reaction | [16](16-phan-ung-trung-dan-2026-11-13.md); `HitReaction-2026-11-13/RemoteClientPresentsTheServersHitOnTheSameBoneWithBlood.txt` còn lưu |
| Death/Downed | [17](17-nguoi-choi-guc-chet-2026-11-14.md), [sửa3P](19-fix-guc-3p-2026-11-14.md); `PlayerLife-2026-11-14/DownedSidearmReviveAndDirectionalDeathAcrossPeers.txt` còn lưu |
| Map/material | [29](29-vo-nha-hai-mat-toan-factory-2026-11-16.md), [42](42-sua-mat-chong-khit-con-lai-2026-11-17.md) |
| Menu | [MainMenu](MainMenu-2026-11-12.md) |
| Ánh sáng | [14](14-anh-sang-factory-2026-11-12.md), [tăng sáng01/05](FactoryLighting-Tang-Sang-01-05-2026-11-13.md), [bake](38-bake-anh-sang-2026-11-17.md), [hall](40-den-hall-bao-tri-2026-11-17.md) |
| Recoil/medical | [CODE] `RecoilController.cs`, `WeaponStability.cs`, `SurvivalPresentation.cs`, `SelfInjectionAnimation.cs`; reviewer đã tra graph/coverage rồi đọc source khoanh vùng. Chưa lấy source presence thay runtimeacceptance |

## Cách đọc trạng thái và ưu tiên

Không có căn cứ để đánh dấu tất cả14mục là hoàn tất. Hai mục có nghiệm thu lịch sử rõ là hit reaction theo bộ phận và luồng Death/Downed người chơi. Supplies, nội dung radio/nhiệm vụ, nềnmenu và các lớp audio đã có phần triển khai cụ thể. ToolDirector đã có góiFINAL giữ lại được nhưng phạm vi nghiệm thu là benchmark, chưa phải mọi SPECgameplay.

[INFERENCE] Ưu tiên xử lý tiếp nên là giật lag trong workload chơi thật, sau đó chỗ map tối/cản đọc đường và các trình diễn medical/recoil chưa có gatecuối. Lý do là chúng còn ảnh hưởng trực tiếp việc chơi và cảm giác điều khiển; đây là đề xuất ưu tiên, không tự mở scope sửa ở lượt kiểm kê này.

## Phạm vi kiểm tra lần này

- Đọc báo cáo và một số raw artifact; hai reviewer độc lập khảo sát visual và combat/audio theo phần việc không chồng nhau.
- Không chạy mới game/test/build, không thay gameplay, không sửa góiFINAL đã sealed.
- Scene ban đầuGameScene sạch. Thiếu artifact lịch sử được ghi là thiếu khả năng kiểm lại, không kết luận test chưa từng chạy.
- Áp dụng `code-review-and-quality` đã đọc: phân biệt implementation, test, giới hạn và tuyên bố nghiệm thu. ChecklistTRUE/FALSE cũ không được dùng làm nguồn duy nhất.

Các lỗi người dùng vẫn gặp được giữ mở dù báo cáo cũ ghi “đã sửa”. Cần tái hiện trên bản hiện tại trước khi chốt hết lỗi.
