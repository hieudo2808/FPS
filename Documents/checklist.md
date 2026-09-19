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
   Điều chỉnh công thức stress level để tính đúng cho 4 người thay vì 2 (tránh 1 người "gánh" làm sai lệch chỉ số cả team) TRUE — runtime A2 profile 4 peer đã ghi score/multiplier tại Relax
   Test case: 1 người skill cao, 3 người yếu — Director có cân bằng đúng không TRUE — controlled runtime profile: score 0.2403, multiplier 0.9221
   Thêm cooldown/giới hạn spawn để tránh spawn dồn dập gây khó chịu (spam feeling) TRUE — special chỉ mở ở Peak, qua registry cooldown/budget/type cap/max-alive
   Log lại dữ liệu Director (spawn rate, stress value theo thời gian) để làm biểu đồ minh chứng cho báo cáo/bảo vệ — hội đồng rất thích thấy số liệu cụ thể TRUE — JSONL phase/multiplier/spawn events trong A2 runtime report
   Balance riêng cho từng map (facility vs phòng nghiên cứu có mật độ zombie khác nhau) FALSE
   Test "worst case": cả 4 người đứng yên 1 chỗ xem Director phản ứng thế nào (không được bug loop) FALSE
3. Zombie & Special Infected
   Hoàn thiện thêm 2 loại zombie thường (biến thể tốc độ/HP từ 2 model gốc, tái dùng animation) FALSE
   Chọn 1 special infected (Screamer hoặc Tanker) làm trước, làm kỹ: TRUE — đã triển khai và test cả Screamer, Tanker, Infector; special regression pass 71/71 EditMode và 22/22 PlayMode
   Thiết kế behavior tree riêng (không dùng chung AI với zombie thường) FALSE — theo kiến trúc đã duyệt, dùng FSM/custom server brain nhỏ thay vì dựng behavior framework mới
   Animation riêng (attack, alert, death) TRUE — controller/state riêng; GameScene PlayMode gate đo đủ ba vòng cho từng special, LocomotionRate 0.75–1.35 và sai số distance ≤10%
   Sound cue đặc trưng để player nhận biết từ xa TRUE — enemy-local 3D logarithmic audio + pitch riêng; clip hiện là placeholder từ asset sẵn có, cần thay SFX production
   Test đồng bộ multiplayer riêng cho con này (do AI phức tạp hơn dễ desync hơn) FALSE
   Nếu còn thời gian mới làm special infected thứ 2 TRUE — cả ba special đã được đăng ký Playable và NetworkPrefab
   Kiểm tra pathfinding (NavMesh) hoạt động ổn trong GameScene, không bị kẹt góc TRUE — cả ba special chạy ba complete route đa góc trên NavMesh thật, có progress watchdog 2 s và endpoint timeout
   Scale enemy theo snapshot team size 1–4 TRUE — profile chung; 17 EditMode case kiểm tra bảng HP/damage/status/CC/cooldown, không double-scale, late join/disconnect và pool respawn capture lại
   Screamer né và hét ngoài LOS toàn team TRUE — endpoint còn thấy bị reject; multi-observer visibility test pass; crescendo request và HUD warning đã nối
   Infector durable + retreat khuất toàn team TRUE — HP 500/750/975/1200; implant lock 1.625 s; khuất liên tục 1.5 s, timeout 8 s
   Tanker threat targeting hài hòa TRUE — 50/30/20, ledger 8 s/half-life 4 s, commitment 4 s, strict switch >20%
   Stationary action không NavMesh drift TRUE — GameScene PlayMode đo prefab thật cho Screamer scream/generic attack, Tanker swing/slam/stagger và Infector implant; tất cả ≤0.05 m
4. Vũ khí
   Xây dựng weapon base class/system chung (stats: damage, fire rate, recoil, ammo capacity) để thêm súng mới nhanh TRUE — hiện dùng `WeaponData + WeaponServerState + WeaponManager`; fire rate do Animator baker sinh
   Hoàn thiện 4 khẩu core trước: Assault Rifle, Pistol, Shotgun, Sniper TRUE — Vandal, Classic, Bucky, Operator đã có gameplay và presentation timing
   Thêm 2 khẩu còn lại (Machine gun, Handgun phụ) nếu 4 khẩu core đã mượt FALSE — Odin đã hoàn thiện; không thêm handgun thứ hai vì phạm vi đã khóa ở 5 súng
   Recoil pattern + spread riêng cho từng loại (không dùng chung 1 công thức) TRUE — Vandal/Classic/Operator/Odin có pattern riêng; Bucky chủ đích không recoil và dùng cone 8 pellet
   Hiệu ứng bắn trúng theo vùng (headshot vs bodyshot) — ăn điểm về feel bắn súng TRUE — damage dùng multiplier của từng `HitboxSegment`; VFX hit riêng vẫn thuộc phase sau
   Đồng bộ multiplayer: reload animation, ammo count hiển thị đúng cho tất cả client FALSE — authoritative state/presentation đã triển khai, chưa pass gate host + client thực
   Sound & VFX riêng biệt cho từng khẩu (không dùng 1 sound bắn chung) FALSE — để phase sau
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
   Indicator hướng special infected khi nó phát ra tiếng động FALSE — Screamer replicated directional warning đã có; Tanker/Infector chưa có indicator tương đương
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
