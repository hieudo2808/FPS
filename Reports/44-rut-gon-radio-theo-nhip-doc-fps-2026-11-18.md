# Rút gọn radio theo nhịp đọc FPS — 18/11/2026

## Vấn đề và phạm vi sửa

Phản hồi của người dùng đúng: bản trước có 12 dấu `;` trong 11/28 câu, nhiều lượt chứa nhiều phát hiện và mệnh lệnh, kể cả trong encounter. Việc chữ vừa panel ở 1080p/4K không chứng minh người chơi đọc kịp khi còn phải quan sát và điều khiển. Lượt sửa trước đánh giá thiếu tải đọc trong FPS. [INFERENCE]

Lượt này chỉ sửa 28 cặp EN/VI trong catalog và đồng bộ canon. Giữ Mason/Doc/Vega/Raven, Command, Anonymous Client, toàn bộ ID/trigger/priority/cooldown/once/audio và flow. Không đổi layout, font, timing, AI, súng, audio hoặc save logic. [INFERENCE]

## Nguồn đã nghiên cứu

- [Microsoft XAG 104](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/104): “Avoid long lines of text (more than 40 characters), especially when the dialogue is fast.” Ưu tiên hai dòng và ngắt cụm có nghĩa.
- [Game Accessibility Guidelines — subtitle presentation](https://gameaccessibilityguidelines.com/if-any-subtitles-captions-are-used-present-them-in-a-clear-easy-to-read-way/): tối đa 40 ký tự mỗi dòng, hai dòng mỗi cue; ba dòng chỉ là ngoại lệ.
- [Game Accessibility Guidelines — reading speed](https://gameaccessibilityguidelines.com/ensure-that-subtitlescaptions-are-cut-down-to-and-presented-at-an-appropriate-words-per-minute-for-the-target-age-group/): rút gọn cách diễn đạt, không làm mất thông tin. Trang không đưa một WPM cố định cho FPS.

Reviewer độc lập đã đọc trực tiếp các trang gốc, kiểm bản copy và các mốc truyện. Đây là hướng dẫn game; không gán quy tắc phụ đề phim thành chuẩn game. Không nguồn nào cấm mọi dấu chấm phẩy: việc bỏ `;` áp dụng yêu cầu trực tiếp của người dùng và nhịp radio hiện tại. Chi tiết nguồn, phạm vi và approval ở [review.md](Narrative-2026-11-18/short-radio/review.md). [INFERENCE]

## Bản sửa

- Không còn `;` trong cả EN/VI.
- Câu EN còn 3–12 từ; tổng số từ giảm **414 → 209 (49,5%)**. Đây là mục tiêu biên tập cho bộ thoại này, không phải tốc độ đọc phổ quát. [INFERENCE; short-radio/metrics.json]
- Mỗi bản EN/VI có tối đa hai dòng được ngắt theo cụm nghĩa, mỗi dòng tối đa 40 ký tự **tính cả tên người nói**. Cần kiểm wrap thực tế vì số ký tự không thay thế số đo font/panel.
- Cảnh nguy hiểm giữ hành động trước mắt; chi tiết hướng dẫn lặp lại đã có trong mục tiêu và Files được bỏ khỏi radio. D11 vẫn nêu mâu thuẫn hai mốc thời gian; không khẳng định người bệnh hiện còn sống. D20/21 vẫn giữ hai kiện, bản sao đầy đủ ở ngoài cơ sở và phong tỏa tiếp tục.
- D22 chỉ loại danh tính và phản đối khỏi bản gửi; D23 bác lệnh, yêu cầu gửi tất cả. D18 dùng câu chung về lối ra vì trigger này dùng cho mọi chương, không mặc định luôn có thang máy. [INFERENCE]

Ví dụ mới (xuống dòng như cue):

```text
Command: Vanguard, get the sealed case
and its records.

Command: Lift's coming.
Leave the Tank behind.

Command: Ignore that.
Send it all. Names and objections too.
```

Runtime sẵn có dùng `max(6 giây, số ký tự đã format × 0,06, thời lượng audio)`. Bản trước không phải bị khóa ở một thời gian quá ngắn cố định; vấn đề là lượng thông tin và việc nhiều cue dài làm hàng đợi kéo dài. Không đổi công thức timing để che vấn đề copy. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:899]

## Bằng chứng

Approval cuối trước triển khai: SHA256 draft `6ECEB01117816A867FB8187DD9F91C9F0C70A8774682C1F2EA2FA4EFBBD1C7DD`. [INFERENCE; short-radio/review.md]

```text
PASS: all 56 EN/VI cues <=2 authored lines and <=40 characters per line including speaker
PASS: 56 text fields changed; 821 other serialized fields unchanged
PASS: all 28 canon radio entries match the short draft; ID metadata preserved
```

Snapshot và công cụ đối chiếu nằm trong [short-radio](Narrative-2026-11-18/short-radio/). Baseline là catalog sau lượt 43, trước lượt rút gọn này. Các FILE, đáp án, điều kiện nhiệm vụ và references giữ nguyên qua so sánh SerializedObject của UnitySkills. Bản nháp dài và log lượt 43 được giữ làm lịch sử, không được xem là thoại hiện hành. [INFERENCE]

Đã chạy solo host bằng campaign/prefab thật tới Factory/Exploring. Runtime đối chiếu đủ 28 ID/cặp EN–VI và speaker với draft đã duyệt. Trong mỗi độ phân giải **1920×1080 và 3840×2160**, QA gọi `EnqueueDialogue`/`UpdateSubtitles` hiện có rồi đọc deadline thật: cả 28 cue EN nhận 6 giây. Đây là đo deadline do HUD đặt, không phải đo tốc độ đọc của người chơi. [PLAYTEST; short-radio/after-playtest.txt]

Với `UnityEngine.UI.Text` thật của HUD, 112 bản ghi (28 câu × 2 ngôn ngữ × 2 độ phân giải) đều có 1–2 dòng, tối đa 40 ký tự/dòng và không cắt chiều cao. Bản VI được gán vào widget để kiểm font/wrap, không phải playtest chức năng đổi locale. [PLAYTEST; short-radio/layout-1920x1080.csv, layout-3840x2160.csv]

Đã chụp 12 Game View gốc: D01/D16/D22 trước/sau ở cả hai độ phân giải. Trước là copy lượt 43 replay trên renderer không đổi, giữ 30 giây để chụp; không được dùng làm bằng chứng thời lượng cũ. D16 phát lại tại Factory để kiểm phụ đề, không phải encounter Tank thật. Reviewer độc lập đã xem đủ 6 cặp crop native pixel, xác nhận AFTER ngắn hơn, hai dòng, không cắt hoặc chồng HUD trong vùng ảnh đã xem. Chi tiết phạm vi và giới hạn trong [visual-review.md](Narrative-2026-11-18/short-radio/visual-review.md). [PLAYTEST]

Sau review đã xóa 12 ảnh gốc và 6 ảnh ghép theo AGENTS, giữ kích thước/hash trong [screenshots-manifest.json](Narrative-2026-11-18/short-radio/screenshots-manifest.json). Script QA tạm và meta đã gỡ khỏi Assets. Trạng thái cuối: không Play, không compile, Console errors=0; GameScene sạch. Hash scene và hai checkpoint khớp baseline trước test. Lần kiểm đầu sau refresh gặp ConnectionRefused do domain reload; retry khi health trở lại đã PASS. [SCENE; short-radio/final-editor-check.json]

Log thực:

```text
PASS: 28 unique IDs, complete EN/VI, exact reviewed copy and speakers
PASS: actual HUD renderer, 28 EN + 28 VI, <=2 lines, <=40 characters/line, no vertical clipping; actual UpdateSubtitles duration 6 seconds for all 28 cues at 1920x1080
PASS: actual HUD renderer, 28 EN + 28 VI, <=2 lines, <=40 characters/line, no vertical clipping; actual UpdateSubtitles duration 6 seconds for all 28 cues at 3840x2160
STOP: original authored scene and checkpoint restored; dirty=False
PASS: Editor stopped, compiled, errors=0; GameScene clean; scene/checkpoint hashes restored; QA harness removed
```

Reviewer dữ liệu đã đối chiếu độc lập đủ 56 field thay đổi và 821 field còn lại, cả metadata SerializedProperty. Canon khớp 28 EN sau khi đổi `<br>` thành newline. `git diff --check` của phạm vi lượt này đạt. [INFERENCE; short-radio/review.md]

## Skills đã đọc và quy tắc áp dụng

| Skill | Quy tắc áp dụng |
|---|---|
| creative-writing-craft / scene-construction | Nhịp và giọng theo tình huống; câu cảnh báo không gánh thêm thuyết minh. |
| writing-principles | Economy và Flow: bỏ phần lặp, giữ thông tin mới; yêu cầu người dùng bỏ `;` cao hơn gợi ý dấu câu dành cho văn xuôi trong skill. |
| dialogue-systems | Giữ runner, stable IDs và trạng thái hiện có; không cài thêm hệ thống thoại. |
| unity-skills + scene/editor/scriptableobject | Kiểm scene sạch, Save Gate; SerializedObject setters, dryRun rồi đọc lại mọi field. |
| game-ui-ux + unity-mcp-ui-layout (verification) | Kiểm renderer thật ở 1080p/4K, phân biệt số đo, ảnh và khả năng đọc của người chơi. |
| ponytail | Chỉ rút copy và đồng bộ canon, giữ timing/layout có sẵn; QA tạm ngoài runtime. |

## Giới hạn

Chưa có thử nghiệm tốc độ đọc với người chơi đang giao tranh; số từ và phép đo UI không chứng minh mọi người đọc kịp. Không thay queue/preemption của radio hoặc thêm chế độ chờ người chơi đọc. Không playtest toàn chiến dịch hay host/client thật. Game hiện dùng EN; VI được đồng bộ trong dữ liệu, không thêm chức năng đổi ngôn ngữ.
