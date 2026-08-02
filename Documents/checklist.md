1. Multiplayer & Networking
   Test full 4 người thật (không phải bot giả lập) — ưu tiên số 1 FALSE
   Test kịch bản 1 người disconnect giữa chừng → reconnect có vào lại được không FALSE
   Test packet loss / độ trễ cao (dùng tool giả lập mạng yếu) xem desync có xảy ra không FALSE
   Test đồng bộ trạng thái AI Director khi có 4 input đồng thời (4 người cùng bắn/cùng bị thương) FALSE
   Test host migration hoặc xử lý khi host thoát (nếu dùng peer-to-peer/host-based) FALSE
   Đồng bộ animation zombie (đặc biệt special infected) trên tất cả client — dễ bị lệch animation nhất FALSE
   Đồng bộ item pickup (2 người cùng nhặt 1 item cùng lúc → race condition) TRUE — server transaction race/idempotency/rate-limit tests pass
   Xử lý lag compensation cho hitbox súng (bắn trúng trên máy mình nhưng miss trên server) TRUE — EditMode A1 tests and PlayMode smoke test pass
   Giới hạn bandwidth: đo thử lượng data gửi/nhận khi 4 người + nhiều zombie cùng lúc FALSE
2. AI Director & Dynamic Difficulty
   Điều chỉnh công thức stress level để tính đúng cho 4 người thay vì 2 (tránh 1 người "gánh" làm sai lệch chỉ số cả team) FALSE
   Test case: 1 người skill cao, 3 người yếu — Director có cân bằng đúng không FALSE
   Thêm cooldown/giới hạn spawn để tránh spawn dồn dập gây khó chịu (spam feeling) FALSE
   Log lại dữ liệu Director (spawn rate, stress value theo thời gian) để làm biểu đồ minh chứng cho báo cáo/bảo vệ — hội đồng rất thích thấy số liệu cụ thể FALSE
   Balance riêng cho từng map (facility vs phòng nghiên cứu có mật độ zombie khác nhau) FALSE
   Test "worst case": cả 4 người đứng yên 1 chỗ xem Director phản ứng thế nào (không được bug loop) FALSE
3. Zombie & Special Infected
   Hoàn thiện thêm 2 loại zombie thường (biến thể tốc độ/HP từ 2 model gốc, tái dùng animation) FALSE
   Chọn 1 special infected (Screamer hoặc Tanker) làm trước, làm kỹ: FALSE
   Thiết kế behavior tree riêng (không dùng chung AI với zombie thường) FALSE
   Animation riêng (attack, alert, death) FALSE
   Sound cue đặc trưng để player nhận biết từ xa FALSE
   Test đồng bộ multiplayer riêng cho con này (do AI phức tạp hơn dễ desync hơn) FALSE
   Nếu còn thời gian mới làm special infected thứ 2 FALSE
   Kiểm tra pathfinding (NavMesh) hoạt động ổn trên cả 3 map, không bị kẹt góc FALSE
4. Vũ khí
   Xây dựng weapon base class/system chung (stats: damage, fire rate, recoil, ammo capacity) để thêm súng mới nhanh FALSE
   Hoàn thiện 4 khẩu core trước: Assault Rifle, Pistol, Shotgun, Sniper FALSE
   Thêm 2 khẩu còn lại (Machine gun, Handgun phụ) nếu 4 khẩu core đã mượt FALSE
   Recoil pattern + spread riêng cho từng loại (không dùng chung 1 công thức) FALSE
   Hiệu ứng bắn trúng theo vùng (headshot vs bodyshot) — ăn điểm về feel bắn súng FALSE
   Đồng bộ multiplayer: reload animation, ammo count hiển thị đúng cho tất cả client FALSE
   Sound & VFX riêng biệt cho từng khẩu (không dùng 1 sound bắn chung) FALSE
5. Map / Level Design
   Map 1 (facility): hoàn thiện blockout → detail → lighting → optimization FALSE
   Map 2 (phòng nghiên cứu): tối thiểu chơi được trọn vẹn, không cần chi tiết bằng map 1 FALSE
   Map 3: nếu thời gian không đủ, có thể cắt hoặc làm dạng map nhỏ/tuyến tính đơn giản FALSE
   Thiết kế đường đi cho AI Director (chọn điểm spawn hợp lý, tránh chỗ bí bức player) FALSE
   Đặt item cần lấy (theo mission "lấy vật phẩm") ở vị trí có ý nghĩa với environmental storytelling FALSE
   Test performance (FPS) trên máy cấu hình trung bình, không chỉ máy dev FALSE
6. Environmental Storytelling & Cutscene
   Viết outline cốt truyện ngắn gọn: bối cảnh virus, tổ chức cử đội, lý do vào facility FALSE
   Đặt note/tài liệu/audio log rải rác trong map thay vì kể chuyện qua cutscene dài FALSE
   Cutscene intro (mở đầu mission) — dùng in-engine camera, không cần animation phức tạp FALSE
   Cutscene outro (khi extract thành công/thất bại) — có thể làm 2 bản ngắn FALSE
   Đảm bảo cutscene không bug khi có 4 người (ai trigger, ai bị skip, có bị treo máy người khác không) FALSE
7. UI/UX & HUD
   HUD hiển thị: HP, ammo, stamina, trạng thái đồng đội (đặc biệt quan trọng với cơ chế lây nhiễm nội bộ) FALSE
   Màn hình lobby/matchmaking cho 4 người FALSE
   Menu chọn vũ khí trước mission (nếu có) FALSE
   Indicator hướng special infected khi nó phát ra tiếng động FALSE
   Death/spectate screen khi 1 người chết nhưng team vẫn tiếp tục FALSE
8. Audio
   Ambient sound riêng theo từng khu vực map (tạo cảm giác căng thẳng kiểu RE) FALSE
   Sound cue cảnh báo khi Director tăng độ khó (jump scare setup) FALSE
   Voice line đơn giản của nhân vật (callout khi thấy zombie, hết đạn...) FALSE
9. Polish & QA cuối
   Playtest với người ngoài team (không phải bạn — để phát hiện bug/balance mà team quen mắt không thấy) FALSE
   Fix bug list ưu tiên theo mức độ nghiêm trọng (crash > desync > gameplay > cosmetic) FALSE
   Kiểm tra build cuối chạy được trên máy khác, không phụ thuộc máy dev FALSE
10. Báo cáo & Bảo vệ đồ án
    Quay sẵn video demo đầy đủ (đề phòng live-demo lỗi mạng trước hội đồng) FALSE
    Chuẩn bị slide/số liệu minh chứng AI Director (biểu đồ stress level, spawn rate theo thời gian — điểm nhấn kỹ thuật) FALSE
    Viết rõ trong báo cáo phần kiến trúc networking (đây là phần khó, cần show ra để ăn điểm) FALSE
    Chuẩn bị trả lời câu hỏi "vì sao chọn Photon/Netcode", "xử lý desync thế nào" — hội đồng SoICT hay hỏi sâu phần kỹ thuật FALSE
