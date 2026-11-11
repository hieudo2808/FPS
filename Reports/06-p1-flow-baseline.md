# P1 — baseline flow: phiên đầu, còn dở dang

Ngày 29/10/2026. Bot 5 đã chạy local host trong Play Mode theo quyền P1 được cấp; Bot 6 tổng hợp bằng chứng và nhận phần kiểm cleanup khi phiên Bot 5 bị ngắt. **P1 chưa hoàn tất.** Đã có ảnh baseline Factory, chưa có lượt hoàn thành tuyến/interaction Accepted, cohort người mới hoặc bốn client. Không sửa code, binding Shipping, scene/volume hay asset; không chạy Test Runner. Điều kiện dùng để đánh giá nằm trong [P0 v3 — điều kiện duyệt](E:/Unity/Project/FPS/Reports/04-flow-proposal-v3-conditions.md).

## 1. Những việc đã chạy thật

| Kiểm | Kết quả thực tế | Bằng chứng và giới hạn |
|---|---|---|
| Preflight | GameScene đang active, `isDirty=false`; Editor không Play, không compile. | [preflight.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/preflight.json). Đây là trạng thái đầu phiên, không thay kiểm cuối phiên. |
| Save Gate trước load và Play | Menu `Tools/FPS/UI/Save Authored Scenes` trả success trước load MainMenu và trước Play. | [save-before-load.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/save-before-load.json), [save-before-play.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/save-before-play.json). Không save Untitled/test scene. |
| Vào Play/local host | Job Play hoàn tất, sau đó menu Start Local Host trả success; Game-view đến Exploring, tracked FactoryManifest. | [entered-play-recovered.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/entered-play-recovered.json), [local-host-editor.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/local-host-editor.json), [metadata](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/facing-route.txt). Lần gọi host sớm bị mất kết nối trong quá trình chuyển Play; không coi lần lỗi đó là host chạy thành công. |
| Baseline HUD live | Đã chụp 1920×1080 và 3840×2160; Bot 6 đã xem. | [1080p](E:/Unity/Project/FPS/Reports/P1Evidence/Bot6/baseline-factory-1920x1080.png), [4K](E:/Unity/Project/FPS/Reports/P1Evidence/Bot6/baseline-factory-3840x2160.png), [báo cáo asset/HUD](E:/Unity/Project/FPS/Reports/06-p1-ui-assets-baseline.md). Hai frame khác thời điểm; không có after/visual approval. |
| Input bình thường | Hai lệnh quay nhìn và một lệnh W trong 10 s đã được gửi; command 3 kết thúc `completed` lúc 13:33:56. | [session.jsonl](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/session.jsonl), [players-afterwalk.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/players-afterwalk.json). Không có cặp vị trí đầu/cuối và hướng đủ chuẩn để hiệu chuẩn tốc độ hoặc kết luận movement hỏng. |
| Runtime state cuối còn query được | 13:34:37: `isPlaying=true`, `isCompiling=false`; sau đó query thấy một PlayerHealth ở (−121.597328, 0.07999992, −133.899323). | [afterwalk-state.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/afterwalk-state.json), [players-afterwalk.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/players-afterwalk.json). Chỉ có một actor trong snapshot này. |

**[PLAYTEST screenshot]** Tracker nêu `Read the shipping manifest`; marker thấy được ghi `271m ROUTE`, thiếu tên Manifest. Đây là quan sát nhãn, chưa chứng minh tọa độ marker sai. Marker giữ cùng số sau input không đủ kết luận người chơi bị chặn: hướng đi W có thể chưa trùng hướng tuyến. Chưa tương tác tới Manifest; không dùng các lần thử input này làm dữ liệu người mới.

**[PLAYTEST/LOG]** Snapshot lỗi đầu phiên có một lỗi UnitySkills `Thread was being aborted` tại lúc chuyển Play ([runtime-errors-insertion.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/runtime-errors-insertion.json)). Tail Editor.log sau đó còn có lỗi capture ghi file và cảnh báo SettingsManager sinh object khi đóng scene. Các ảnh có file và metadata được giữ vẫn là bằng chứng thật; không vì menu capture trả success mà chứng nhận mọi lần capture thành công. Chưa xác định các lỗi này do lượt P1 nào ngoài chronology đã lưu; không gán chúng thành lỗi gameplay mới hoặc kết luận Console sạch.

## 2. Chưa chạy — không chấm đạt/không đạt thay bằng suy luận

| Hạng mục P1 | Trạng thái | Dữ liệu còn cần khi tiếp tục |
|---|---|---|
| D1(c), arrival → tương tác Manifest đầu | **NOT RUN / chưa chấm được** | T0 xác định, tuyến hợp lệ đã đo `L_ref`, tốc độ đi chuẩn `v_walk`, Accepted đầu tiên, các pause kỹ thuật/combat có timestamp và số lần mất hướng. Khoảng raw 368,46 m trong P0 không tự thành `L_ref` đi bộ đã đo. |
| Cohort người mới | **n=0** | QA đã biết map không tính người mới. Có thể kết luận sơ bộ n=1–4 và ghi giới hạn; n≥5 mới dùng tỷ lệ ≥80%. |
| Hint 45/90, H khi đi/khóa | **NOT RUN cho luật mới** | Chưa triển khai luật hoãn khi khoảng cách giảm; sau khi được phép triển khai, đo đứng yên/đi vòng/đúng tuyến, H mọi ngữ cảnh gameplay, không phát dồn hint. |
| Shipping S1 + nhận diện trước ngắm | **Asset đã kiểm, hành vi mới NOT RUN** | Binding S1 chưa đổi. Phương án/rủi ro asset và tách đèn trạng thái khỏi viền mục tiêu ở [báo cáo Bot 6](E:/Unity/Project/FPS/Reports/06-p1-ui-assets-baseline.md). |
| Lift preparation 4×3,5 m | **NOT RUN** | Bốn actor kết nối cùng lúc, vị trí/collision/lối thoát, với được Lift, READY/countdown; thêm tình huống Tank Active. Một host hoặc bốn capsule không thay bốn người. Thiếu chỗ thì trình đề xuất nới volume; không tự dời/nới. |
| Tank Pending fallback / Active recovery | **NOT RUN** | Pending timeout và thông báo fallback; recovery khi HP Tank không giảm 30 s hoặc không có đường giao chiến hợp lệ 30 s, thông báo cả đội, Factory/An Lac/Lab. Recovery mới chưa code, không báo đã kiểm nút hoạt động. |
| Cửa 5 s + 3 s, safe anchors thật | **NOT RUN** | Bị kẹt cửa/cản đường và kiểm vị trí an toàn thật với host/client; không ký an toàn từ tọa độ thiết kế. |
| Timeout gom đồng đội, READY/đang chờ ai | **NOT RUN** | Host/client, người ở xa, Downed/Dead, giao diện chờ đúng người và server authority. |
| Checkpoint/save exception/Downed bản sao | **NOT RUN** | Không nạp hoặc sửa save người dùng để tạo test giả. Đợi quyền code rồi kiểm Editor test và host/client theo P0. |
| Năm cue / lịch sử thoại phiên | **NOT RUN** | Nghe từng clip, xác minh nguồn/license, kiểm cue với chữ/hình và thoại bị bỏ lỡ. Thiếu âm máy phát được chấp nhận tạm thời. |
| Editor tests | **NOT RUN** | Không có thay đổi flow/code để giao kèm test; không mở Test Runner hoặc tạo scene tạm. Khi code được duyệt phải chạy test thật và lưu kết quả. |

Khi tiếp tục D1(c), giữ nguyên số thô và chấm theo điều kiện mới:

`T_first = T_first_total − T_technical`; `T_walk = L_ref / v_walk`; `T_excess_raw = T_first − T_walk`; **`T_excess = T_excess_raw − T_combat`**.

Chỉ trừ combat có bằng chứng timestamp, không chồng pause kỹ thuật, không tự trừ toàn bộ Tank Active; giữ số âm để kiểm mô hình nếu combat có chồng thời gian di chuyển. Ngưỡng đạt cùng lượt: `T_excess ≤45 s` và `≤1 lần mất hướng`; n≥5 yêu cầu ≥80% lượt đạt cả hai. Cờ đỏ: `T_excess ≥90 s`, cần trợ giúp hoặc ≥3 lần mất hướng. Chưa có Accepted/route/speed hợp lệ thì chưa chấm, không ghi fail chỉ vì phiên dở dang. Nếu vượt ngưỡng sẽ xin người dùng chuyển D1(c) sang (b), không tự đổi vị trí.

## 3. Cleanup và Save Gate cuối chưa xác minh được

**Không tuyên bố P1 done hoặc scene cuối sạch.** Khi nhận cleanup, Bot 6 gọi `fps_playtest_stop` và `editor_get_state` nhưng REST từ chối kết nối ([cleanup-stop-input.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/cleanup-stop-input.json), [cleanup-before-stop-state.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/cleanup-before-stop-state.json)). Không có Unity-Skills listener khả dụng trong kiểm cổng; Computer Use `list_apps` và `list_windows` đều không có cửa sổ Unity để gửi Stop hoặc menu Save Gate. Không kill/restart/launch Unity, không gửi phím tới cửa sổ khác.

**[LOG]** Log dự án cuối ghi NetworkManager teardown, Input System `ShutdownInProgress → Shutdown`, `Cleanup mono`, `CodeReloadManager destroyed`, Package Manager shutdown; mtime 14:02:57 +07. [Trích log có số dòng](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/cleanup-shutdown-excerpt.txt), [biên bản cleanup](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/cleanup-verification.json). Bằng chứng này phù hợp phiên Editor đã shutdown, nhưng không chứng minh scene được save hoặc `isDirty=false`. Không xác định ai/tác nhân nào đã đóng phiên; không suy PID Unity còn tồn tại là chính phiên test còn ở Play.

Save Gate cuối, trạng thái stable Edit Mode và việc restore GameScene ban đầu đều **NOT VERIFIED**. Menu Save Authored Scenes hiện có từ chối chạy khi Play/đang chuyển Play và chỉ save authored scene có path, bỏ Untitled/test/preview trước `AssetDatabase.SaveAssets()` ([TacticalUiWorkshop.cs:628](E:/Unity/Project/FPS/Assets/FPS/Features/UI/Editor/TacticalUiWorkshop.cs:628)); không gọi save runtime để lách gate.

Hash sau cleanup so với đầu phiên: **72 entry giống nhau**, không có file đã tồn tại bị đổi trong phạm vi đo, gồm source/config đã ghi, GameScene/MainMenu, AGENTS và save chính/previous. Save `.tmp` vắng mặt cả trước và sau, không gọi là đã xóa. [Trước](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/hashes-before.json), [sau](E:/Unity/Project/FPS/Reports/P1Evidence/Bot5/hashes-after.json). Hash trên đĩa không chứng minh dirty state trong Editor, cũng không chứng minh mọi file toàn project bất biến.

Trước khi tiếp tục P1 khi Editor khả dụng trở lại: query scene/state trước; nếu scene dirty thì báo và dừng theo luật read-only, không tự save. Nếu sạch/stable Edit Mode, chạy Save Gate đúng authored scenes rồi restore GameScene theo lifecycle đã duyệt, Save Gate sau load, query `isDirty=false`. Mỗi chuỗi Play tiếp theo phải có Save Gate trước/sau và không save scene runtime/Untitled/test. Cần ghi state/hashes cuối thật trước khi ký hoàn tất.

## 4. Skill áp dụng và tự kiểm báo cáo

Bot 6 dùng các skill đã đọc ở nhánh này: [Unity-Skills](C:/Users/hieud/.agents/skills/unity-skills/SKILL.md) — query/schema trước, không bịa kết quả khi route mất; [unity-editor](C:/Users/hieud/.agents/skills/unity-skills/skills/editor/SKILL.md) và [unity-scene](C:/Users/hieud/.agents/skills/unity-skills/skills/scene/SKILL.md) — phân biệt lifecycle/scene authoring, chỉ save authored scene đúng gate; [computer-use](C:/Users/hieud/.codex/plugins/cache/openai-bundled/computer-use/26.1023.50603/skills/computer-use/SKILL.md) — đọc guidance/api/confirmations, chọn từ cửa sổ trả về, dừng khi không có Unity target. Không có input UI được gửi. Các skill asset/HUD được ghi riêng trong báo cáo Bot 6; không nhận thay các skill Bot 5 chưa được chứng minh bằng log nhánh này.

Tự kiểm: báo cáo không dùng ảnh baseline làm after/visual approval; không biến solo thành bốn người; không lấy raw distance thay route chuẩn; không lấy shutdown/hash thay `isDirty=false`; không chấm luật chưa triển khai là đã pass. P0 vẫn có điều kiện, P2 chờ người dùng chọn cách làm HUD, AGENTS diff chờ duyệt riêng, chưa có quyền code.

Chưa kiểm: completion/Accepted tuyến đầu, metric người mới, bốn người/Lift, Tank/cửa/safe anchors, cue/license và Save Gate cuối. Rủi ro: chưa đủ dữ liệu ký P1; cần Editor khả dụng và state thật để tiếp tục an toàn.
