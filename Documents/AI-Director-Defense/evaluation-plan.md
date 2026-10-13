# Kiểm chứng AI Director và Dynamic Difficulty trong runtime

Ngày chuẩn bị: 09/10/2026. Đây là kế hoạch và quy tắc diễn giải bằng chứng, **không phải kết quả đã chạy**. Bằng chứng runtime mới phải có log/snapshot/video đi kèm. Các công tắc/chế độ nêu dưới đây chỉ được sử dụng sau khi kiểm tra source; không mặc định game đã có sẵn.

## Câu hỏi cần trả lời

Hệ thống có quan sát dữ liệu, ra quyết định đúng thời điểm, rồi thực sự làm thay đổi gameplay theo quyết định đó hay không? Cần theo được cả chuỗi **input → score → multiplier → quyết định Director → hành động thành công → ảnh hưởng nhìn thấy**. Giá trị trên Inspector thay đổi chỉ xác nhận một phần của chuỗi.

| Mức khẳng định | Bằng chứng tối thiểu | Chưa được suy ra |
|---|---|---|
| 1. Cơ chế hoạt động đúng trong tình huống đã chạy | Source hiện hành + snapshot trước/sau + hành động thực tế + điều kiện đối chứng | Đúng trong mọi map, mọi số người, hoặc mọi nhánh chưa chạy |
| 2. Ổn định và đáp ứng hiệu năng trong điều kiện thử | Chạy đủ dài qua nhiều encounter; log lỗi; profiler/frame time; cấu hình máy và số người; số lần lặp | Ổn định trên máy yếu hoặc 4 người nếu chỉ chạy Editor/host đơn |
| 3. Cải thiện trải nghiệm người chơi | So sánh có đối chứng với người thật; thứ tự cân bằng; thước đo cảm nhận và kết quả; báo cáo số mẫu | Một lượt demo, một biểu đồ multiplier, hoặc FPS tốt không chứng minh vui/công bằng hơn |

## Bộ probe runtime ưu tiên

Trước và sau mỗi chuỗi thao tác: lưu scene tác giả đã có asset path và `AssetDatabase.SaveAssets()`; bỏ qua Untitled, preview và scene Test Runner tạm. Kết thúc phải kiểm tra scene đang làm việc `isDirty == false`. Lưu lại giá trị ban đầu của mọi trường bị đổi và phục hồi sau thử.

| Probe | Cách làm và dữ liệu cần giữ | Điều kiện kết luận |
|---|---|---|
| P1. Pha Director | Theo dõi `Calm / BuildUp / Combat / Peak / Relax`, thời điểm chuyển, input liên quan, và số địch thực tế qua một chu kỳ. Nếu dùng ép pha/debug, ghi rõ "kích thích có kiểm soát". | Chuyển pha tự nhiên xác nhận vòng điều khiển trong điều kiện đã chạy. Ép pha chỉ xác nhận handler/output của pha đó; không xác nhận điều kiện chuyển pha tự nhiên. |
| P2. Multiplier chỉ chốt tại ranh giới quy định | Ghi multiplier đầu encounter; đưa vào hai profile dữ liệu khác nhau; lấy snapshot giữa combat và sau khi vào Relax. Tính giá trị dự kiến bằng **công thức source hiện hành**, kể cả smoothing, clamp và giới hạn bước. | Trong combat không đổi vì DDA; sau Relax đổi đúng công thức khi đủ điều kiện. Phải phân biệt thay đổi static tier hoặc override với thay đổi do DDA. |
| P3. Không phản ứng với dữ liệu thiếu | Chạy encounter không có mẫu hợp lệ và profile ít dữ liệu; kiểm tra mẫu số 0, NaN, fallback và multiplier giữ nguyên/reset theo code. | Không crash/NaN và hành vi khớp rule. "Không đổi" có thể là gate bảo vệ hợp lệ, chưa phải DDA hỏng. |
| P4. Quyết định spawn có thành hành động | Giữ static tier, team size, map và gate hợp lệ; so sánh profile nhẹ/nặng. Ghi interval/cap được yêu cầu, số request, số spawn thành công, alive count, spawn bị chặn và lý do. | Chỉ gọi là ảnh hưởng thực tế khi địch được tạo hợp lệ hoặc nhịp spawn thay đổi. Target/cap tăng mà NavMesh/LOS/cooldown chặn hết thì chưa có tác động gameplay trong probe đó. |
| P5. Chỉ số địch mới và địch đang sống | Đọc max HP/damage của một địch đã spawn; thay điều kiện theo đường chính thức; đọc lại địch đó và một địch spawn mới. Ghi prefab, static tier, multiplier lúc spawn. | Nếu thiết kế áp dụng lúc spawn: chỉ địch mới nhận cấu hình mới; địch cũ giữ kỳ vọng chiến đấu. Cần xác nhận đúng code thực tế trước khi dùng điều kiện này làm yêu cầu. |
| P6. Observe-only có thật sự không can thiệp | Bật chế độ quan sát nếu có; giữ logging và input; theo dõi score/pha, đồng thời kiểm tra các đường spawn/stat/loot đang tiêu thụ output. Đối chiếu một cửa sổ thời gian tương đương với active. | Dữ liệu vẫn được tính nhưng actuator bị vô hiệu/được baseline tiếp quản. Một cờ mang tên observe-only không đủ nếu consumer khác vẫn đọc multiplier. |
| P7. Gate và cap | Thử điều kiện bị cấm: ngoài pha cho special, đạt max alive, cooldown chưa hết hoặc không có điểm spawn hợp lệ. Ghi request bị từ chối. | Không có spawn vi phạm gate/cap. Ít nhất một lần spawn thành công trong điều kiện hợp lệ để tránh kết luận sai từ hệ thống không spawn gì. |
| P8. Tính đúng trong co-op | Nếu có đủ peer: dùng profile một người mạnh/những người còn lại yếu, và đối chiếu từng input cùng phép tổng hợp đội. Kiểm tra server quyết định và client nhìn thấy cùng kết quả. | Host đơn không được đổi nhãn thành xác nhận co-op nhiều người. Input tổng hợp giả lập xác nhận toán/logic, không xác nhận mạng hay người thật. |

Không cần chạy toàn bộ probe trong buổi trình bày. Ưu tiên một chuỗi ngắn: P1 → P2 → P4 → P5, sau đó một trường hợp chặn P7. Nếu runtime không đủ điều kiện spawn, báo nguyên nhân và giới hạn; không dùng thay đổi Inspector làm bằng chứng thay thế.

## Cách trình bày hiệu ứng để hội đồng nhìn thấy

- Dùng cùng mốc thời gian cho bốn dòng: pha Director, multiplier, spawn thành công/alive count, và HP/down của người chơi. Ghi rõ tick lấy mẫu và đơn vị.
- Chụp trước/sau cùng góc nhìn hoặc quay liên tục, kèm timestamp. Đặt con số dễ đọc cạnh hình: ví dụ interval và số địch thực tế trong cửa sổ quan sát; chỉ điền giá trị sau khi đo.
- Với HP/damage, cho thấy hai entity ID và thời điểm spawn. Không so hai prefab/tier khác nhau rồi quy mọi khác biệt cho DDA.
- Nhãn bằng chứng phải là một trong ba loại: **gameplay tự nhiên**, **runtime kích thích có kiểm soát**, **sơ đồ minh họa**. Hai loại sau không được đổi nhãn thành trải nghiệm người chơi thật.
- Giữ cả trường hợp không tác động vì gate và trường hợp tác động thành công. Điều này giải thích hệ thống có giới hạn, đồng thời giúp bắt được pipeline bị ngắt.

## Đối chứng tối thiểu và giới hạn diễn giải

Phân biệt các điều kiện; không coi tất cả là một nút "tắt AI":

| Điều kiện | Pacing/hành động thực tế | Vai trò |
|---|---|---|
| A. Baseline kịch bản cố định | Một lịch spawn được định trước và giữ nguyên giữa các lượt; hệ thích nghi không điều khiển actuator | Mốc so sánh sản phẩm khi không thích nghi |
| B. Baseline + Director observe-only | Cùng baseline A; Director thu thập/tính/ghi nhận nhưng output không được thi hành | Kiểm tra đường quan sát và chi phí tính; không phải bằng chứng lợi ích gameplay |
| C. Director active, DDA giữ mức chuẩn | Director điều phối pacing; multiplier thích nghi được giữ cố định theo cấu hình đã xác minh | Tách tác động pacing khỏi tác động DDA |
| D. Director active + DDA active | Cả pacing và điều chỉnh theo kết quả encounter đều được thi hành | Hệ thống hoàn chỉnh |

Đối chiếu A/B giúp phát hiện observe-only còn tác động ngoài ý muốn và đo overhead. Đối chiếu C/D giúp quy khác biệt cho DDA. Đối chiếu A/D đánh giá toàn bộ hệ; tự nó không tách được lợi ích của từng thành phần. Nếu game chưa hỗ trợ điều kiện nào, ghi "chưa thực hiện", không dựng số liệu.

Khi có thời gian playtest, ưu tiên C/D với cùng người hoặc **cùng đội** chơi cả hai: một nửa chơi C rồi D, nửa còn lại D rồi C; có lượt làm quen trước; dùng các đoạn map/seed tương đương và phân bổ cân bằng. Trong co-op, lượt của cùng đội là dữ liệu phụ thuộc; không coi mỗi người hoặc mỗi frame là một mẫu độc lập. Không xóa hiệu ứng học map chỉ bằng reset HP. Với ba điều kiện, phân bổ sáu thứ tự ACD/ADC/CAD/CDA/DAC/DCA nếu số đội cho phép; ghi rõ thiếu cân bằng nếu không đủ.

Chốt trước thước đo chính, chẳng hạn cảm nhận "thử thách phù hợp" sau mỗi lượt trên cùng thang điểm. Thước đo phụ: công bằng, căng thẳng, thích thú, số down/wipe, thời gian hoàn thành, tài nguyên cuối lượt, tần suất can thiệp, thời gian nằm ở clamp và frame time. Sống lâu hơn chưa chắc thích thú hơn. Score nội bộ được hệ thống tối ưu không tự đóng vai trò thước đo thành công độc lập.

Báo số người/đội/lượt thực tế, kinh nghiệm FPS, thiết bị, tier, version/commit, map, seed, điều kiện mạng và thiếu dữ liệu. Ghi trước quy tắc dừng/retry khi lỗi. Xem ít lượt là pilot; chưa có ước lượng hiệu ứng và phân tích cỡ mẫu thì không tuyên bố đủ statistical power hay kết luận tổng quát. Đây là bước tiếp theo; các probe runtime ở trên chỉ nhằm xác nhận cơ chế.

## Trả lời ngắn khi bị hỏi sâu

**"Đây có phải machine learning không?"** Không, nếu implementation là FSM, điểm có trọng số và luật điều chỉnh. Đây là game AI thích nghi theo dữ liệu runtime. Không có quá trình train thì không gọi là mô hình đã học.

**"Vì sao công thức/trọng số này đúng?"** Chúng là tham số thiết kế hiện hành. Có thể kiểm tra phép tính, giới hạn và thời điểm áp dụng bằng runtime; muốn nói tối ưu hoặc phù hợp với người chơi cần calibration và so sánh. Không dùng bài báo ngoài để hợp thức hóa trọng số riêng.

**"Demo chứng minh tốt hơn chưa?"** Demo chứng minh đường dữ liệu và hành động trong các điều kiện đã thử. Cải thiện công bằng/thích thú cần người thật và đối chứng. Các kết quả lịch sử không còn log gốc chỉ là ghi nhận tài liệu.

**"Có gì khác Left 4 Dead?"** Tham khảo ý tưởng điều phối nhịp, nhưng không khẳng định sao chép hay vượt thuật toán của Valve. Dự án tách pacing khỏi điều chỉnh độ khó dựa trên proxy kết quả encounter; phải chứng minh từng đường bằng implementation của dự án.

## Nguồn nền tảng đã đọc

1. **Michael Booth, _The AI Systems of Left 4 Dead_ (2009), Valve.** [PDF gốc](https://cdn.akamai.steamstatic.com/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf). PDF trả HTTP 200 và đã đọc nội dung các trang vật lý 78–82, 92. Trang 79–81 mô tả ước lượng intensity, Build Up/Sustain Peak/Peak Fade/Relax; trang 92 phân biệt rõ thuật toán điều chỉnh pacing với difficulty. Vì vậy không dùng nguồn này để nói Valve đã chứng minh công thức DDA của dự án. FSM 5 pha của dự án cũng không đồng nhất FSM trong bài nói.
2. **Robin Hunicke và Vernell Chapman, _AI for Dynamic Difficulty Adjustment in Games_.** [PDF tác giả tại Northwestern](https://www.cs.northwestern.edu/~hunicke/pubs/Hamlet.pdf). Đã đọc toàn văn PDF; trang 2 mô tả theo dõi dữ liệu và chính sách hành động; trang 3 nêu mục tiêu nhận biết lúc nào/cách nào nên điều chỉnh; trang 4–5 bàn về đánh đổi của thay đổi trực tiếp với các yếu tố đang giao tranh so với thay đổi ngoài tầm nhìn. Đây là bài về Hamlet, **không phải** bài riêng _The case for dynamic difficulty adjustment in games_ (2005); không gán nhầm tên/năm. Nguồn này hỗ trợ cách đặt vấn đề và các trade-off, không chứng minh heuristic của dự án hiệu quả.

Đã kiểm tra các nguồn trên ngày 09/10/2026. Kết quả probe hiện hành cần ghi riêng cùng artifact gốc để không trộn lý thuyết, kế hoạch và kết quả.
