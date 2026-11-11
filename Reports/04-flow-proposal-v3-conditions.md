# P0 v3 — Điều kiện duyệt và lệnh bắt đầu P1

Ngày 29/10/2026. **Người dùng duyệt P0 v3 có điều kiện, chọn S1 Shipping, cho phép Bot 5/6 chạy P1 baseline bằng Play Mode với Save Gate. Chưa cho phép code hoặc triển khai binding/volume/feedback mới.** Văn bản này cập nhật các phần tương ứng của v3; không mở lại những quyết định đã duyệt.

## 1. Shipping S1 đã chọn, nhận diện là điều kiện phải đáp ứng

Giữ transform prop; Shipping sẽ bind vào thùng **(20)** tại **(92.11679, 2.498578, −86.07217)**. Chưa thực hiện rebind trong baseline. Điểm bấm thay đổi khoảng 16,55 m đường thẳng so với binding cũ; không gọi khoảng cách này là NavMesh distance. S2 không còn là lựa chọn đang chờ.

Thùng Shipping phải được nhận ra **khi lại gần, trước khi ngắm**, bằng đèn/viền dùng asset sẵn; không chỉ đổi tên hoặc hiện tag sau khi ngắm. Ba thùng (20)/(21)/(22) giống nhau cần phân biệt được trong tình huống thực. P1 kiểm asset/component và ảnh baseline; P2 trình phương án nhận diện cùng cách làm HUD người dùng chọn, chưa triển khai trong P1. Scene có thể có tên/path trùng; dùng **entityId đang hợp lệ + vị trí + collider/reference đã kiểm**, không bind theo tên đơn độc. Báo cáo asset P1 phải kèm bằng chứng xác nhận đúng thùng.

## 2. Auto hint khi đứng yên/đi vòng; H luôn gọi được trong gameplay

Giữ mốc **45/90 s kể từ tiến triển liên quan gần nhất** trên nhánh đang theo. Các mốc chỉ tạo hint chờ; **không tự hiện khi khoảng cách còn lại tới đích đang giảm**. Không reset tiến độ chỉ do đi đúng đường, nhưng phải hoãn hiển thị trong lúc đó. Hint tự động chỉ hiện khi người chơi đứng yên hoặc đi vòng và các điều kiện hoãn cũ (hold/panel/menu/cảnh báo/combat) đã hết. Không xếp hai hint để phát dồn sau khi ngừng đi.

**[ĐỀ XUẤT để P1/P2 rà độ ổn định, chưa có code]** Lấy mẫu tối đa 1 Hz và xem xu hướng trong cửa sổ quan sát 10 s; **ưu tiên hoãn ngay khi đo được khoảng cách còn lại đang giảm, kể cả giảm chậm**. Chỉ xét hiện khi đã xác nhận đứng yên hoặc di chuyển nhưng không tiến gần đích/đi lặp lại tuyến; dữ liệu nhiễu, chưa đủ cửa sổ hoặc chưa phân loại rõ thì tiếp tục hoãn. P1 ghi nhiễu vị trí/khoảng cách để đề xuất sai số đo, không tự đặt một mức “đi chưa đủ nhanh” rồi coi người chơi đi đúng là đi vòng. Dùng route Complete tới đích hiện tại; nếu chỉ có hướng thẳng thì ghi rõ fallback và hoãn khi khoảng cách thẳng giảm, không tuyên bố điều đó chứng minh đã đi đúng tuyến. Đổi đích/revision/route làm mất liên tục thì làm mới cửa sổ đo để tránh nhảy hint do đường vừa đổi.

Nếu cả mốc 45 và 90 s đã qua trong lúc đang đi, chỉ giữ một hint phù hợp mới nhất. Giữ giới hạn hai lần tự nhắc cho một đoạn chưa tiến triển, nội dung vẫn chỉ nêu nguồn/hành động/địa điểm, không nâng thành đáp án. Hết đoạn đi đúng rồi đứng lại phải đủ cửa sổ quan sát trước khi hiện, không bật ngay đúng frame ngừng đi.

**H/Hint vẫn gọi ngay khi đang đi đúng tuyến, ở xa, bị khóa hoặc đang combat; không phụ thuộc CanUse, LOS, khoảng cách hay bộ hoãn auto.** Giữ routing phím theo ngữ cảnh gameplay và cooldown chống spam 2 s đã nêu ở v3; không coi chúng là gate của objective. H không làm hiện choices thiếu nguồn hoặc tắt cảnh báo/ring. Hint lấy từ CampaignContentCatalog theo objective/phase, không viết đáp án trong HUD.

## 3. Reposition Tank — HP của Tank, thông báo cả đội, cả ba chương

N=30 s theo thời gian gameplay server. **“Không nhận sát thương” nghĩa là HP của chính Tank không giảm do damage server xác nhận trong cửa sổ đó. Không phải HP người chơi.** Damage thật lên HP Tank reset đồng hồ này; đạn trượt hoặc chỉ sát thương giáp không làm HP Tank giảm thì không reset. Tiêu chí không có route giao chiến hợp lệ và không có LOS vẫn là cửa sổ riêng như v3; Unknown/Partial đơn lẻ không chứng minh unreachable.

Áp dụng **Factory, An Lac/Asylum và Laboratory khi Tank Active**, có người/mục tiêu hợp lệ cùng chương; giới hạn một lần cho mỗi lượt Active, giữ HP/kill count, không sinh Tank thứ hai. Factory dùng phục hồi Tank đang chặn Case; An Lac và Lab là công cụ host khi cần, **không thêm điều kiện phải kill Tank để rời chương**. Pending giữ timeout 20 s; không áp đồng hồ HP cho Pending.

Khi server xác nhận đủ điều kiện và nút host chuyển sang khả dụng, **báo cả đội ngay**, kể cả người chưa mở menu: `Host recovery available — Tank HP unchanged for 30s` hoặc `Host recovery available — No valid engagement route for 30s`. Hiện lý do bằng chữ + badge; có thể dùng cue mở khóa trong năm cue đã duyệt, không thêm loại thứ sáu. Không chỉ báo riêng host. Mỗi lần đổi eligibility chỉ phát một lần, không lặp theo refresh/menu mở lại.

Khi host bấm và server kiểm lại còn hợp lệ: cả đội thấy `Host is repositioning the Tank` trước di chuyển; sau xác nhận vị trí mới mới báo `Tank repositioned`. Nếu điều kiện đã hết hoặc không có anchor an toàn, báo rõ thất bại/Retry, không giả reposition thành công, không đổi defeated. Cảnh báo nguy hiểm Tank vẫn giữ. P1 đo baseline; tool recovery mới chưa được triển khai.

## 4. Lift preparationArea — bắt buộc kiểm với bốn người

**[SCENE từ v3]** Volume AsylumPreparation center (146.3, −2.3, 238.785), size (4, 3, 3.5); mặt bằng khoảng **4 × 3,5 m**. Target Lift sát mép volume. Diện tích danh nghĩa không chứng minh bốn người đứng/READY được cùng lúc khi có Tank.

P1 phải thử **bốn actor người chơi kết nối đồng thời**, cùng vào volume, đứng không chồng collider, nhìn/với được Lift, READY/dwell được, có đường thoát; ghi thêm tình huống Tank Active và địch cản. Ghi vai host/client, vị trí bốn người, collision, mất READY/countdown, tác động Tank và ảnh/video. Bốn capsule hoặc bốn lượt solo không thay thử bốn người.

Nếu không đủ chỗ hoặc không thể tập hợp an toàn, P1 gửi evidence + **đề xuất mở rộng volume quanh vị trí Lift hiện tại** kèm kích thước/tọa độ đề xuất để người dùng duyệt. Không tự đổi volume hoặc dời Lift. Nếu chưa có bốn client/người tham gia, ghi **NOT RUN — thiếu phiên kết nối**, không ký đạt; vẫn làm các phép đo độc lập khác.

## 5. Hai kênh nhận diện riêng

| Kênh | Ý nghĩa và cách phân biệt | Thời điểm/nguồn trạng thái |
|---|---|---|
| Đèn trạng thái thiết bị | **Xong/chưa xong**: fixture nhỏ cố định tại thiết bị, ổn định, có trạng thái như ISOLATED/BACKUP READY/RUNNING trên screen tag khi ngắm. Không chạy quanh thân vật và không dùng nhấp nháy để gọi mục tiêu. | Theo completion server; còn đúng khi bỏ tracking, teammate hoàn tất, Retry hoặc late join. Không đổi đèn chỉ vì đang theo nhánh đó. |
| Viền/chỉ đích mục tiêu | **Đây là vật cần bấm**: viền theo hình thân tương tác hoặc fixture chỉ đích riêng, xuất hiện khi mục tiêu đang được theo và người chơi đến gần trước khi ngắm. Screen tag tên vật + action/lock reason khi ngắm. | Theo target đang theo của từng người, độc lập completion; đổi nhánh thì chuyển viền đúng thân vật. Mục tiêu khóa vẫn nhận diện được, nhưng prompt không [F] cho hành động khóa. |

Đèn cố định và viền thân khác **vị trí/hình dạng/hành vi**, không chỉ khác màu. Viền có thể cùng tồn tại với đèn đã hoàn tất khi cần xem lại; không hiểu “có viền = đã xong”. Chỉ cam cho hành động ngay, trạng thái trắng/xám theo theme; mục tiêu khóa không dùng cam như có thể bấm. Asset chưa đáp ứng phải báo thiếu, không vẽ shader/icon/primitive mới. Bot 6 xác nhận phương tiện sẵn trong P1, P2 trình cách phối hợp hai kênh; đây chưa phải mockup hoặc visual approval.

## 6. T_excess — chấm sau trừ combat, giữ số thô

Giữ T0/Accepted, L_ref PathComplete tới chỗ đứng bấm và v_walk hiệu chuẩn như v3. Ghi đồng thời:

`T_first_total = thời gian thô từ T0 tới Accepted`  
`T_first = T_first_total − T_technical`  
`T_walk = L_ref / v_walk`  
`T_excess_raw = T_first − T_walk`  
**`T_excess = T_excess_raw − T_combat` — dùng số này để chấm đạt/không đạt.**

T_technical chỉ chứa pause kỹ thuật đã đánh dấu. T_combat là tổng hợp **các khoảng combat có timestamp/bằng chứng**, đã loại phần trùng pause kỹ thuật; không đếm đôi các khoảng giao nhau và không trừ toàn bộ thời gian Tank Active nếu người chơi chỉ đang đi/tìm nguồn. Ghi start/end khi thực sự giao chiến/né đe dọa tới lúc trở lại đi/đọc bình thường. Nếu không tách được combat đáng tin cậy thì giữ số thô và ghi **chưa chấm được**, không tự ước lượng phần trừ để đạt.

Ngưỡng v3 **≤45 s + ≤1 lần mất hướng cùng lượt**, tỷ lệ đồng thời ≥80% khi n≥5, cờ đỏ ≥90 s/cần trợ giúp/≥3 lần mất hướng nay áp dụng cho **T_excess sau trừ combat**. Khi chưa có Accepted, chỉ dùng đồng hồ tạm `(elapsed − technical − combat − T_walk)` để nhận diện cờ đỏ, không giả đã đo được T_first. n=1–4 được kết luận sơ bộ có giới hạn; QA đã biết map không tính thành người mới.

Giữ cả T_first_total, T_technical, T_walk, T_combat, T_excess_raw và T_excess cạnh nhau, cùng video/timestamp. Nếu phần thừa âm, giữ số âm và kiểm mô hình: di chuyển trong combat có thể chồng với T_walk nên cần kiểm trước khi kết luận đạt; không ép về 0 hoặc trừ cùng một khoảng hai lần. Không dùng đường đi lạc làm L_ref. Thiếu route/speed hợp lệ thì chưa chấm phần thừa. Phân loại mất hướng vẫn tách combat/đọc có chủ đích.

## 7. Trạng thái công việc sau duyệt

- **P1 bắt đầu:** Bot 5 sở hữu thao tác Editor/Play/lifecycle/save; Bot 6 kiểm asset/HUD và phối hợp lấy ảnh, tránh hai bot điều khiển cùng Editor. Save Gate theo AGENTS trước/sau; skip Untitled/temp Test Runner. Không sửa code/scene/asset nhằm làm test qua, không sửa save người dùng. Ghi run thật và NOT RUN rõ.
- **Âm máy phát thiếu tạm đã được chấp nhận.** Không chặn baseline, không tự import/tạo clip; năm cue vẫn phải nghe và kiểm giấy phép trước chốt.
- **P2 chờ người dùng chọn cách làm HUD; không yêu cầu ảnh mẫu nữa.** Chưa dựng mockup hoặc tự chọn phong cách. Phương án nhận diện Shipping/hai kênh là nội dung phải trình ở P1/P2.
- **AGENTS.md và diff vẫn không sửa/áp dụng.** Người dùng duyệt diff riêng. P3/P4/P5 chưa bắt đầu; chưa có quyền code.

**Kết quả đợt bắt đầu P1:** đã vào local-host Exploring và lấy ảnh baseline 1080p/4K; đã kiểm asset/viền/fixture và đưa phương án nhận diện. P1 **dở dang**: chưa có số đo movement/Accepted đủ chấm T_excess, người mới n=0, chưa thử Lift bốn người/Tank hoặc safe anchors/cửa. Bot 5 bị ngắt lượt do giới hạn sử dụng; Bot 6 tiếp quản cleanup thì REST không còn nghe và không có cửa sổ Unity, log có shutdown. Chưa xác minh được Save Gate cuối/scene isDirty=false hoặc khôi phục GameScene; không ghi đã hoàn tất. [Báo cáo asset/HUD](E:/Unity/Project/FPS/Reports/06-p1-ui-assets-baseline.md) có kết quả đã quan sát; [flow/cleanup](E:/Unity/Project/FPS/Reports/06-p1-flow-baseline.md) ghi ca đã chạy và NOT RUN. Chưa có hành vi mới nào được triển khai.

Các skill tiếp tục áp dụng: [level-design](C:/Users/hieud/.codex/skills/level-design/SKILL.md) — kích thước/nhịp dựa đo thực và không thay mô hình bằng bằng chứng người mới; [game-ui-ux](C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md) — tách ý nghĩa trạng thái, không dựa màu, kiểm độ phân giải; [Unity-Skills](C:/Users/hieud/.agents/skills/unity-skills/SKILL.md) — schema/query trước, Save Gate/lifecycle do Bot 5 điều phối. Đây là cập nhật spec, không chứng nhận các hành vi mới đã chạy.
