# Bằng chứng từ tài liệu: AI Director và Dynamic Difficulty

Ngày đối chiếu: 09/10/2026. Phạm vi: tài liệu Markdown trong `Documents`, không sửa mã gameplay, scene, prefab hoặc cấu hình Unity. Đây là bản đọc tài liệu; kết luận về triển khai hiện tại phải đối chiếu source/runtime riêng.

## 1. Luận điểm có thể bảo vệ

> Đồ án triển khai một cơ chế điều khiển thích nghi hai tầng cho FPS co-op: Director điều phối nhịp giao tranh ngắn hạn; Dynamic Difficulty đánh giá kết quả của encounter để điều chỉnh cường độ những encounter tiếp theo trong giới hạn. Giá trị nằm ở cách tách trách nhiệm, hạn chế dao động giữa combat, xử lý chênh lệch năng lực trong đội và khả năng kiểm chứng bằng dữ liệu.

Câu này nêu **đóng góp kỹ thuật của đồ án**, không tự nhận phát minh AI Director, DDA hoặc chứng minh tính mới học thuật trên toàn lĩnh vực. Cảm hứng từ Left 4 Dead/Resident Evil đã được GDD ghi nhận; đó chưa phải tài liệu tham khảo nghiên cứu hay bằng chứng thuật toán hai trò chơi được sao chép chính xác.

Nên trình bày theo chuỗi: vấn đề cụ thể → yêu cầu thiết kế → cơ chế → minh họa một encounter → bằng chứng hiện có → giới hạn → cách đánh giá tiếp. Tránh mở đầu bằng danh sách lớp hoặc khẩu hiệu “AI thông minh”.

## 2. Bản đồ luận điểm và nguồn

Các đường dẫn dưới đây tương đối với `E:/Unity/Project/FPS/`; số sau dấu `:` là dòng.

| Luận điểm trong tài liệu | Nguồn | Cách dùng khi bảo vệ |
|---|---|---|
| Căng thẳng từ khan hiếm; hợp tác; kể chuyện môi trường; khác biệt khi chơi lại | `Documents/basic_gdd.md:28`–31 | Nêu mục tiêu thiết kế. Không biến “không có hai lần giống hệt” thành bảo đảm toán học. |
| DDA trả lời đội đang có phong độ thế nào; Director trả lời nhịp trận ngay lúc này | `Documents/basic_gdd.md:179`–184 | Là phân biệt cốt lõi, dễ hiểu với hội đồng. |
| Director chạy Calm → Build-up → Combat → Peak → Relax | `Documents/basic_gdd.md:186`–202 | Vẽ timeline 5 pha. Nhấn mạnh Relax có vai trò hồi phục và ranh giới cập nhật. |
| Director xét HP thấp nhất, khoảng cách đội, đứng yên, downed gần đây | `Documents/basic_gdd.md:204`–217 | Ý định thiết kế về pacing và công bằng; phải xác nhận từng input trong code. |
| DDA chỉ tính lại multiplier khi vào Relax | `Documents/basic_gdd.md:221`–223 | Một invariant rất thích hợp để test; không chỉ nói “hệ thống thích nghi”. |
| Macro metrics: headshot, damage/kills, downs, kills/ammo, tốc độ mục tiêu | `Documents/basic_gdd.md:225`–233 | Phân biệt metric đề xuất với metric thật đang được thu thập. Tốc độ mục tiêu nằm trong bảng nhưng không nằm trong công thức ngay sau. |
| Công thức đề xuất `S = Σ vᵢxᵢ`, `M = clamp(0.6 + 0.9S, 0.6, 1.5)` | `Documents/basic_gdd.md:235`–248 | Ghi rõ “công thức đề xuất trong GDD” cho tới khi đối chiếu source. `Σvᵢ = 1`; giới hạn thay đổi ±0.15 là khuyến nghị trong tài liệu. |
| Director cấp gate của special, DDA cấp xác suất sau gate | `Documents/basic_gdd.md:257`–277 | Minh họa separation of concerns; cooldown/cap vẫn phải thỏa. Ví dụ 70%/30% chỉ là ví dụ. |
| Không dùng trung bình đội đơn thuần; cân nhắc người yếu nhất | `Documents/basic_gdd.md:279`–284 | Giải thích vấn đề một người giỏi che khuất ba người yếu. Từ “nên cân nhắc” là đề xuất, chưa đủ để khẳng định triển khai. |
| Timeline phase, step chart multiplier, spawn thực tế để kiểm chứng | `Documents/basic_gdd.md:286`–294 | Đây là yêu cầu minh chứng của chính GDD, không phải dữ liệu đã có. |
| Static difficulty và adaptive difficulty là hai khái niệm | `Documents/FPS_Project_Architecture_AI_Special_Infected_Ideas.md:379`–426 | Easy/Medium/Hard/Pandemonium là nền người chơi chọn; thích nghi là điều chỉnh trong phạm vi nền đó. |
| Ý tưởng tổng spawn có pacing × static tier × adaptive × player-count | `Documents/FPS_Project_Architecture_AI_Special_Infected_Ideas.md:351`–375 | Sơ đồ giải thích; không dùng làm công thức số lượng spawn thực tế nếu code xử lý interval/cap. |
| Mission quản mục tiêu/sự kiện; Director quản encounter; không đổi HP/damage địch đang đánh theo việc nhặt thuốc | `Documents/CAMPAIGN_STORY_GAMEPLAY_PUZZLES.md:673`–679 | Nêu ranh giới nhiệm vụ và điều phối áp lực. Các thời lượng nghỉ 25s, downed 15–20s được đánh dấu cấu hình thử. |
| Spawn fairness: kiểm tra mọi người sống/kết nối, LOS, complete path, không đủ anchor thì hoãn/giảm | `Documents/CAMPAIGN_STORY_GAMEPLAY_PUZZLES.md:681`–689 | Tiêu chí thiết kế cụ thể, không đồng nhất “khó hơn” với spawn vô lý. |

## 3. Bằng chứng lịch sử và phần chưa chứng minh

`Documents/checklist.md:12`–15 ghi **đã có controlled runtime A2 profile 4 peer**, tình huống một người giỏi/ba người yếu cho `score = 0.2403`, `multiplier = 0.9221`; special có gate Peak/cooldown/budget/type cap/max alive; từng có JSONL phase/multiplier/spawn events.

Tuy nhiên:

- Đây là **kết quả được tài liệu thuật lại**, chưa phải log gốc được kiểm lại trong lượt này. Không dùng như một thí nghiệm mới ngày 09/10/2026.
- `Documents/ProjectCleanup/README.md:7` nói các báo cáo verification cũ đã bị xóa. Dòng 24–25 nói đã gỡ API `ForTests`/override verification và xóa `NetworkDiagnostics.cs`, `AdaptiveDirectorDiagnostics.cs`; game không còn ghi verification JSONL/callback. Dòng 18 nói đợt cleanup không chạy lại multiplayer/Play Mode và không phải kết quả end-to-end mới.
- `Documents/checklist.md:2`–5 vẫn ghi chưa test bốn người thật và Director khi bốn input đồng thời. “4 peer controlled profile” không thể đổi nhãn thành “4 người chơi thật đã xác nhận tốt”.
- `Documents/checklist.md:16`–17 vẫn ghi chưa balance từng map và chưa test cả bốn đứng một chỗ. Dòng 64 chưa playtest với người ngoài; dòng 68–69 chưa video demo/slide số liệu.
- `Documents/Settings-Difficulty-Audit-2026-10-05.md:5`–10 báo đã sửa ba lỗi settings/difficulty, 9/9 regression pass; chính tài liệu ghi chưa chạy trận multiplayer hoàn chỉnh. Kết quả này xác nhận lỗi cụ thể được sửa, không xác nhận hiệu quả trải nghiệm DDA.

Các trạng thái `TRUE/FALSE` trong checklist có thể lạc hậu. `git status` tại lúc đọc cho thấy `Documents/checklist.md` có thay đổi sẵn; không ghi đè file này. Nên giữ nhãn “tài liệu hiện có ghi nhận” và lấy source/runtime mới làm chuẩn triển khai.

## 4. Những điểm tài liệu cần làm rõ trước khi đưa lên slide

1. **Điểm và multiplier lịch sử không khớp phép thay trực tiếp.** `0.6 + 0.9 × 0.2403 = 0.81627`, khác `0.9221`. Có thể do smoothing, bước giới hạn, cách tổng hợp đội hoặc phiên bản thuật toán khác. Không kết luận bug; cũng không ghép hai số với công thức GDD rồi bảo đó là phép tính trực tiếp.
2. **“Một giá trị trung gian duy nhất” là mô tả kiến trúc GDD.** Tài liệu cùng lúc nêu HP/damage, loot và special probability là output; cần chỉ ra chúng được suy ra từ multiplier hay có modifier riêng trong source.
3. **Loot có câu tự mâu thuẫn.** `basic_gdd.md:254` ghi “nghịch với multiplier” nhưng cũng ghi “khó hơn thì loot nhích lên nhẹ”. Chưa thể mô tả một hàm đơn điệu từ câu đó. Không dùng loot như tính năng đã hoàn tất chỉ dựa vào GDD.
4. **AI Director không đồng nghĩa machine learning.** Nếu source là FSM, metric và heuristic, hãy gọi đúng. “Adaptive” nghĩa là có phản hồi theo dữ liệu runtime; không mặc nhiên có training, neural network hay reinforcement learning.
5. **Bảo đảm công bằng và trải nghiệm tốt cần hai loại bằng chứng.** Unit/integration test có thể chứng minh giới hạn/cập nhật đúng pha. Chúng không tự chứng minh người chơi thấy công bằng, vui hoặc muốn chơi lại.
6. **Giá trị học thuật không đến từ tên gọi.** “Two-layer Adaptive Difficulty Framework” là cách đặt vấn đề trong đồ án. Tuyên bố hơn các hệ khác cần baseline và số liệu; tuyên bố mới toàn cầu cần khảo cứu tài liệu liên quan.

## 5. Lời dẫn khoảng 90 giây

“Vấn đề em giải quyết là cùng một cấu hình độ khó cố định có thể quá nhẹ với một đội nhưng quá nặng với đội khác. Ngay trong một đội, phong độ và tài nguyên cũng thay đổi sau từng giao tranh. Tuy nhiên nếu hệ thống phản ứng ngay sau mỗi phát bắn thì độ khó có thể dao động và người chơi cảm thấy bị phạt vì chơi tốt.

Em tách bài toán thành hai tầng. AI Director điều khiển nhịp độ ngắn hạn qua năm pha: bình ổn, tăng dần, giao tranh, cao trào và hồi phục. Dynamic Difficulty quan sát kết quả encounter và điều chỉnh mức áp lực cho encounter tiếp theo. Điểm quan trọng là hai tầng hoạt động trên hai thang thời gian khác nhau: Director điều phối trong trận; multiplier thích nghi chỉ được chốt ở ranh giới Relax.

Đối với co-op, một giá trị trung bình có thể che khuất người yếu. Vì vậy em quan tâm tới cách tổng hợp dữ liệu đội và các giới hạn bảo vệ, chứ không chỉ tăng số zombie khi điểm kỹ năng tăng. Em sẽ trình bày trước tiên cơ chế đang có trong code, sau đó minh họa giới hạn và ranh giới cập nhật. Các kết luận về mức độ hấp dẫn hoặc công bằng với người thật cần thí nghiệm đối chứng; em không coi sơ đồ hoặc một lượt demo là bằng chứng đủ cho kết luận đó.”

Chỉ giữ câu “đang có trong code” cho các chi tiết đã được lượt kiểm tra source xác nhận. Không đọc nguyên lời dẫn nếu slide vẫn chứa tính năng đề xuất.

## 6. Những câu hội đồng có thể hỏi

| Câu hỏi | Hướng trả lời có căn cứ |
|---|---|
| Khác gì chọn Easy/Hard? | Static tier xác định nền và phạm vi trải nghiệm; DDA phản hồi dữ liệu encounter trong nền đó. Ví dụ cùng Medium, hai đội có kết quả khác nhau sẽ nhận mức thích nghi khác, nếu các gate và giới hạn cho phép. |
| Vì sao phải hai tầng? | Nhịp trận và năng lực đội là hai tín hiệu có thời gian phản ứng khác nhau. Tách ra giúp tránh thay mức dài hạn giữa combat, kiểm thử từng trách nhiệm và chỉnh pacing mà không sửa công thức score. |
| Đây có thực sự là AI không? | Là game AI điều khiển dựa trên trạng thái/heuristic và phản hồi dữ liệu. Không tuyên bố có ML nếu không có mô hình được train. |
| Điểm mới ở đâu khi L4D đã có Director? | Đóng góp trong phạm vi đồ án là thiết kế/tích hợp/kiểm chứng cơ chế hai tầng cho FPS co-op cụ thể. Không nhận đã phát minh Director hay vượt L4D. Tính mới học thuật rộng cần related work. |
| Tại sao chọn các trọng số và 0.6–1.5? | Đây là tham số thiết kế có giới hạn. Nếu chưa calibration có hệ thống, nói rõ; kiểm thử kỹ thuật chứng minh bound hoạt động, playtest/sensitivity analysis mới đánh giá mức hợp lý. |
| Người chơi giỏi có bị phạt không? | DDA không nên xóa lợi ích của kỹ năng. Cần giới hạn tốc độ và biên điều chỉnh, giữ cảm giác tiến bộ; phải đo nhận thức người chơi. Không hứa cơ chế không bao giờ bị cảm nhận là phạt nếu chưa nghiên cứu. |
| Một người cố tình chơi kém để giảm độ khó thì sao? | Đây là failure mode hợp lệ của heuristic. Chốt theo encounter và bước nhỏ giảm phản ứng tức thời nhưng không chứng minh chống khai thác hoàn toàn. Cần test adversarial profile; nhận giới hạn prototype. |
| Đội một giỏi/ba yếu được xử lý thế nào? | Trình bày phép tổng hợp thật từ code. Tài liệu ghi profile lịch sử 0.2403/0.9221, nhưng chưa thay thế kiểm chứng mới hoặc playtest người thật. |
| Nếu tất cả đứng yên thì sao? | Mô tả hành vi thật nếu code có cơ chế. Checklist hiện ghi test này chưa hoàn tất; không dùng ý tưởng anti-camping như kết quả đã thử. |
| Không có chỗ spawn hợp lệ thì sao? | Thiết kế ưu tiên hoãn/giảm và kiểm tra đường đi/LOS; không được phá điều kiện công bằng chỉ để đạt target count. Cần demo/test nhánh thất bại placement từ code. |
| Làm sao biết tốt hơn hệ cố định? | So sánh fixed baseline, Director-only và Director+DDA với cùng map/tier/team size; kiểm soát seed/order khi khả thi; đo cả outcome và cảm nhận. Không chứng minh chỉ bằng biểu đồ multiplier thay đổi. |
| Vì sao không đổi HP địch ngay trong combat? | Làm biến đổi kỳ vọng “còn bao nhiêu đạn để hạ mục tiêu”. Chốt tham số cho spawn mới/encounter kế tiếp giúp tính nhất quán; nêu đúng cơ chế source đang dùng. |
| Multiplayer ai quyết định? | Phần trả lời phải theo source về server authority, dữ liệu trusted, replication; tài liệu kiến trúc định hướng server xử lý AI, client presentation tại dòng 568–585. |
| Kết quả hiện tại đã chứng minh điều gì? | Tách ba mức: (1) kiểm tra source, (2) invariant trong test/runtime, (3) hiệu quả trải nghiệm người thật. Chỉ công bố mức đã có dữ liệu tương ứng. |

## 7. Thí nghiệm tối thiểu đề xuất, chưa thực hiện

Không cần mở rộng game để bắt đầu luận chứng. Chọn một đoạn map ngắn với cùng static tier, số người và mục tiêu. So sánh A: fixed spawn; B: Director giữ adaptive multiplier = 1; C: Director + DDA. Nếu sản phẩm chưa có công tắc baseline, mô tả đây là kế hoạch thí nghiệm, không tạo ấn tượng các chế độ đã có.

Thu hai nhóm dữ liệu:

- **Tính đúng của cơ chế:** thời điểm/pha, score, multiplier trước/sau, lý do cập nhật, requested vs successful spawn, alive count/cap, HP tối thiểu, downed, số người. Kiểm tra multiplier chỉ đổi khi vào Relax, nằm trong bound, delta đúng giới hạn, special gate/cooldown/cap và không tạo địch ở vị trí bất hợp lệ.
- **Hiệu quả trải nghiệm:** số lần down/wipe, thời gian hoàn thành, tài nguyên còn lại, độ phân tán kết quả giữa các đội và đánh giá áp lực/công bằng sau lượt chơi. Tăng tỷ lệ sống sót không tự nghĩa là game tốt hơn; cũng có thể quá dễ.

Dùng biểu đồ thời gian căn cùng trục: pha Director, multiplier dạng bậc thang, spawn thực tế/alive count và các sự kiện down. Gắn nhãn rõ “log trận thực” hoặc “mô phỏng minh họa”. Số liệu mô phỏng không được dùng làm bằng chứng gameplay.

Với người thật, đảo thứ tự A/B/C giữa các nhóm để giảm hiệu ứng quen map; báo cỡ mẫu, điều kiện và độ biến thiên. Một vài lượt chỉ có thể là pilot, không đủ để tuyên bố hiệu quả tổng quát hay ý nghĩa thống kê.

Chưa kiểm tra source, log gốc A2 hoặc runtime Unity trong nhánh công việc đọc tài liệu này. Rủi ro lớn nhất khi dùng tài liệu cũ là trình bày ý tưởng/kết quả lịch sử như chức năng và bằng chứng hiện hành.
