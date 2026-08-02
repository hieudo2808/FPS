# TASK BREAKDOWN & IMPLEMENTATION PLAN

### Tài liệu bổ sung cho GDD_Outbreak_Protocol.md — Đồ án SoICT

Tài liệu này trả lời 3 câu hỏi: **làm gì, làm như thế nào, làm theo thứ tự nào.** Không gắn mốc thời gian — nhóm tự phân bổ theo tốc độ thực tế. Mỗi task có mã ID (VD: `B2`) để tiện trao đổi trong nhóm ("làm task B2 trước nhé").

---

## MỤC LỤC TASK

| Phase | Nội dung                               | Trạng thái tham chiếu                   |
| ----- | -------------------------------------- | --------------------------------------- |
| A     | Củng cố Core Systems đã có             | Ưu tiên cao nhất                        |
| B     | Player Systems                         | Nền tảng cho mọi phase sau              |
| C     | Zombie & Enemy AI                      | Phụ thuộc A, B                          |
| D     | Vũ khí                                 | Phụ thuộc B                             |
| E     | Map / Level Design                     | Phụ thuộc B, C, D (chạy song song được) |
| F     | Narrative & Environmental Storytelling | Phụ thuộc E                             |
| G     | UI/UX & HUD                            | Phụ thuộc B, C, D                       |
| H     | Audio                                  | Chạy song song, chốt cuối cùng          |
| I     | QA & Polish                            | Sau khi các phase trên có bản chạy được |
| J     | Báo cáo & Bảo vệ                       | Song song xuyên suốt, hoàn thiện cuối   |

**Nguyên tắc thứ tự chung:** làm theo hướng **rủi ro cao → rủi ro thấp**, và **hệ thống lõi → nội dung lặp lại**. Lý do: nếu core (multiplayer, AI Director) phát sinh vấn đề, cần phát hiện sớm khi còn nhiều thời gian sửa — không để dồn xuống cuối.

---

## PHASE A — CỦNG CỐ CORE SYSTEMS (đã triển khai, cần hardening)

### A1. Test & vá lỗi Multiplayer 4-player

- **Mục tiêu:** đảm bảo hệ thống đã chạy ổn với 2 người cũng ổn định khi lên 4 người thật
- **Cách làm:**
  1. Tổ chức 1 buổi test với đủ 4 máy/4 người thật (không dùng bot giả lập)
  2. Ghi lại mọi hiện tượng lag, desync animation, sai state
  3. Test riêng: 1 người disconnect giữa chừng, 1 người mạng yếu (dùng tool giả lập network throttle)
  4. Kiểm tra race condition khi 2 người cùng nhặt 1 item cùng lúc
- **Phụ thuộc:** không — làm ngay đầu tiên
- **Điều kiện hoàn thành:** chơi trọn 1 map thử nghiệm với 4 người, không crash, không desync nghiêm trọng

### A2. Tinh chỉnh AI Director cho 4 người + Tách tầng Dynamic Difficulty

- **Mục tiêu:** Director hiện có (dạng StressValue gộp 1 tầng) cần refactor thành kiến trúc 2 tầng độc lập — AI Director (micro pacing) + Dynamic Difficulty (macro, output multiplier) — theo GDD mục 5 (đã cập nhật)
- **Cách làm:**
  1. Refactor Director hiện tại: đổi output từ số tuyệt đối ("Peak = 40 zombie") thành **base value** theo pha (Calm/Build-up/Combat/Peak/Relax) — xem GDD mục 5.1
  2. Viết module `DynamicDifficulty` mới, độc lập: tính `PerformanceScore` từ headshot ratio, damage taken, downed count, ammo efficiency (xem GDD mục 5.2) — **chỉ cập nhật tại thời điểm Director chuyển sang Relax**, không tính liên tục
  3. Viết `SpawnController` nối 2 tầng: `finalSpawnCount = directorBaseValue × difficultyMultiplier` (xem GDD mục 5.3)
  4. Áp dụng bảng phân chia trách nhiệm ở GDD mục 5.3 (Director gate điều kiện trigger Special Infected, Dynamic Difficulty roll xác suất thực sự)
  5. Cập nhật input: dùng HP người yếu nhất thay vì chỉ trung bình cả team, thêm per-player tracking (GDD mục 5.4)
  6. Test case: 1 người skill cao + 3 người yếu — xác nhận Dynamic Difficulty dùng người yếu nhất làm giới hạn, không bị người giỏi kéo multiplier lên cao
  7. Bật data logging riêng biệt cho 2 tầng (GDD mục 5.5) ngay từ bước này — càng sớm càng có nhiều dữ liệu cho báo cáo
- **Phụ thuộc:** A1 (cần multiplayer 4 người ổn định để test)
- **Điều kiện hoàn thành:** có ít nhất 1 biểu đồ log Stress Value theo thời gian từ 1 buổi playtest 4 người

---

## PHASE B — PLAYER SYSTEMS

### B1. Health & Damage system

- **Mục tiêu:** implement theo GDD mục 4.2 — không regen, dùng item hồi máu giới hạn
- **Cách làm:** state machine đơn giản: `Healthy → Injured (dưới 30%) → Downed (0 HP) → Dead`; injured có thể thêm hiệu ứng nhẹ (rung màn hình, giảm tốc độ) để tăng cảm giác nguy hiểm
- **Phụ thuộc:** không
- **Điều kiện hoàn thành:** player nhận damage đúng, dùng item hồi máu đúng số lượng giới hạn

### B2. Downed / Revive system

- **Mục tiêu:** cơ chế bắt buộc hợp tác theo GDD mục 4.2
- **Cách làm:**
  1. Khi HP = 0 → chuyển animation/state "downed", player vẫn có thể bò/bắn pistol yếu (tùy chọn) nhưng không tự đứng
  2. Đồng đội tương tác (giữ phím X giây) để revive, có progress bar
  3. Nếu bị tấn công thêm khi đang downed hoặc hết thời gian giới hạn → chết hẳn
  4. **Đồng bộ multiplayer:** đây là điểm dễ bug nhất — test kỹ trạng thái downed hiển thị đúng trên tất cả client
- **Phụ thuộc:** B1, A1
- **Điều kiện hoàn thành:** test 4 người, revive hoạt động đúng và đồng bộ

### B3. Inventory & Loadout system

- **Mục tiêu:** giới hạn slot theo GDD mục 4.3
- **Cách làm:** UI đơn giản dạng slot cố định (2 vũ khí + N item), không cần drag-drop phức tạp — ưu tiên function over form ở giai đoạn đầu
- **Phụ thuộc:** không
- **Điều kiện hoàn thành:** chọn loadout trước mission, mang đúng giới hạn item trong game

### B4. _(Optional)_ Role/Class system

- **Mục tiêu:** nếu quyết định giữ hệ thống 4 role (GDD mục 2.3)
- **Cách làm:** mỗi role là 1 config khác nhau của cùng 1 player controller (passive stat modifier) — không cần character riêng biệt hoàn toàn, tiết kiệm công sức animation
- **Phụ thuộc:** B1, B3
- **Điều kiện hoàn thành:** 4 role có sự khác biệt rõ ràng khi chơi thử, không role nào rõ ràng yếu/mạnh hơn hẳn (balance sơ bộ)
- **Ghi chú:** nếu thời gian gấp, đây là task đầu tiên nên cắt — game vẫn chạy tốt nếu 4 người dùng chung 1 loại nhân vật

---

## PHASE C — ZOMBIE & ENEMY AI

### C1. Zombie variant thứ 3 (Runner)

- **Mục tiêu:** thêm biến thể tốc độ cao, HP thấp (GDD mục 6.1)
- **Cách làm:** tái sử dụng rig/animation của Walker đã có, chỉnh lại tốc độ NavMeshAgent, giảm HP, đổi animation di chuyển nếu có sẵn asset
- **Phụ thuộc:** không (độc lập với các zombie khác)
- **Điều kiện hoàn thành:** spawn được, hành vi lao thẳng, chết đúng số hit

### C2. Zombie variant thứ 4 (Armor/Heavy)

- **Mục tiêu:** biến thể giáp, HP cao, khắc chế bằng headshot (GDD mục 6.1)
- **Cách làm:** thêm hitbox riêng cho vùng giáp giảm damage, đầu vẫn full damage — khuyến khích chiến thuật bắn đầu
- **Phụ thuộc:** cần hệ thống headshot multiplier đã có ở weapon system (D1)
- **Điều kiện hoàn thành:** test damage đúng theo vùng trúng đạn

### C3. Special Infected #1 — chọn 1 trong Screamer/Tanker làm trước

- **Mục tiêu:** hoàn thiện 1 special infected đầy đủ trước khi làm con thứ 2
- **Cách làm (nếu chọn Tanker — độ ưu tiên gợi ý vì tác động finale rõ hơn):**
  1. Behavior tree riêng: aggro theo damage dealer cao nhất, không dùng chung AI với zombie thường
  2. Animation riêng: idle/chase, attack (swing), stagger khi bị dồn damage, death
  3. HP cao (1500-2000), cần hiển thị health bar riêng cho player nhận biết tiến độ
  4. Test đồng bộ multiplayer riêng — AI phức tạp hơn dễ desync hơn zombie thường
  5. Cooldown spawn hợp lý (không xuất hiện quá thường xuyên)
- **Phụ thuộc:** A1, A2 (cần multiplayer + Director ổn định để tích hợp trigger)
- **Điều kiện hoàn thành:** trigger được trong 1 buổi test 4 người, không desync, cảm giác "đe dọa" rõ ràng qua playtest feedback

### C4. Special Infected #2 (nếu còn thời gian)

- **Mục tiêu:** con còn lại (Screamer nếu đã làm Tanker trước, hoặc ngược lại)
- **Cách làm:** tương tự C3, đặc thù riêng: Screamer cần cơ chế lẩn tránh (flee behavior) + trigger horde khi phát hiện
- **Phụ thuộc:** C3 hoàn thành trước (để không làm dở dang cả 2)
- **Điều kiện hoàn thành:** tương tự C3
- **Ghi chú:** đây là task đầu tiên nên cắt nếu thời gian không đủ — 1 special infected làm kỹ vẫn tốt hơn 2 con làm dở

---

## PHASE D — VŨ KHÍ

### D1. Weapon Base Architecture

- **Mục tiêu:** hệ thống chung theo GDD mục 7.1 để thêm súng mới nhanh
- **Cách làm:** tạo `WeaponBase` (ScriptableObject hoặc class) chứa damage, fireRate, magSize, reloadTime, recoilPattern, spreadAngle, headshotMultiplier — mỗi khẩu súng chỉ là 1 config khác nhau, không viết logic riêng từng khẩu
- **Phụ thuộc:** không — nên làm sớm vì mọi vũ khí sau đều phụ thuộc vào đây
- **Điều kiện hoàn thành:** 1 khẩu súng mẫu chạy đầy đủ (bắn, reload, damage, recoil) qua hệ thống base này

### D2-D5. 4 khẩu core: Rifle, Handgun, Shotgun, Sniper

- **Mục tiêu:** hoàn thiện 4 khẩu chính trước (GDD mục 7.2)
- **Cách làm:** dùng config từ D1, mỗi khẩu chỉnh số liệu + model/animation riêng + sound/VFX riêng
- **Phụ thuộc:** D1
- **Điều kiện hoàn thành:** 4 khẩu chơi được, cảm giác bắn khác biệt rõ ràng giữa các loại (quan trọng hơn số liệu chính xác)

### D6-D7. Machine Gun + khẩu mở rộng (nếu còn thời gian)

- **Mục tiêu:** hoàn thiện set 6 khẩu
- **Phụ thuộc:** D1, sau khi D2-D5 đã ổn
- **Điều kiện hoàn thành:** tương tự D2-D5
- **Ghi chú:** đây là task dễ cắt nhất nếu thiếu thời gian — 4 khẩu core đã đủ thể hiện được đa dạng loại vũ khí

### D8. Ammo economy trong map

- **Mục tiêu:** đặt tỷ lệ rơi/nhặt đạn hợp lý theo độ khan hiếm từng loại (GDD mục 7.2)
- **Phụ thuộc:** D2-D5, E1 (cần có map để đặt item)
- **Điều kiện hoàn thành:** playtest xác nhận không bao giờ dư đạn quá nhiều hoặc thiếu đạn quá sớm

---

## PHASE E — MAP / LEVEL DESIGN

### E1. Map 1 (Công xưởng) — Blockout

- **Mục tiêu:** dựng layout cơ bản (hình khối, không detail) để test gameplay flow sớm
- **Cách làm:** dùng khối đơn giản (cube/prefab tạm) dựng đúng cấu trúc Insertion → Explore → Objective → Extraction (GDD mục 3.2, 8.1)
- **Phụ thuộc:** B1-B3 (cần player controller cơ bản để test đi lại trong blockout)
- **Điều kiện hoàn thành:** đi hết map từ đầu tới cuối, xác nhận flow hợp lý, choke point ở đúng vị trí

### E2. Map 1 — Detail pass + NavMesh cho AI

- **Mục tiêu:** hoàn thiện visual + đảm bảo pathfinding zombie hoạt động đúng
- **Cách làm:** thay asset thật, bake NavMesh, test spawn point cho Director
- **Phụ thuộc:** E1, C1-C2 (cần có zombie để test spawn trong map thật)
- **Điều kiện hoàn thành:** chơi trọn map 1 với đầy đủ hệ thống (zombie, AI Director, vũ khí) không lỗi pathfinding

### E3-E4. Map 2 (Phòng nghiên cứu) — Blockout + Detail

- **Mục tiêu:** tương tự E1-E2, thêm cơ chế keycard/mã code nhỏ (GDD mục 8.3)
- **Phụ thuộc:** E2 hoàn thành trước (ưu tiên xong 1 map chỉn chu trước khi dàn trải)
- **Điều kiện hoàn thành:** tương tự E2 + cơ chế khóa cửa hoạt động đúng

### E5. Map 3 — chỉ làm nếu Map 1, 2 đã ổn định

- **Mục tiêu:** map thứ 3 theo GDD mục 8.4, có thể đơn giản/nhỏ hơn 2 map đầu
- **Phụ thuộc:** E4
- **Điều kiện hoàn thành:** tối thiểu chơi được trọn vẹn, không cần chi tiết bằng map 1-2
- **Ghi chú:** đây là task đầu tiên nên cắt/rút gọn nếu thời gian gấp — 2 map hoàn thiện tốt hơn 3 map dở dang

---

## PHASE F — NARRATIVE & ENVIRONMENTAL STORYTELLING

### F1. Viết outline cốt truyện đầy đủ

- **Mục tiêu:** chốt nội dung cụ thể (tên virus, tổ chức, nhân vật) dựa trên khung GDD mục 2
- **Cách làm:** viết dạng văn bản outline (1-2 trang), không cần kịch bản chi tiết từng câu thoại
- **Phụ thuộc:** không — có thể làm song song với các phase kỹ thuật khác
- **Điều kiện hoàn thành:** có outline được cả nhóm thống nhất, dùng làm cơ sở viết tài liệu môi trường

### F2. Viết nội dung tài liệu môi trường (email, nhật ký, log...)

- **Mục tiêu:** nội dung cụ thể cho các loại clue trong GDD mục 2.4
- **Phụ thuộc:** F1
- **Điều kiện hoàn thành:** đủ số lượng tài liệu cho cả 2-3 map (ước lượng 5-8 mảnh/map)

### F3. Đặt clue vào map

- **Mục tiêu:** tích hợp nội dung F2 vào world (đặt vị trí, tạo interaction đọc được)
- **Phụ thuộc:** F2, E2 (cần map detail để đặt đúng chỗ hợp lý)
- **Điều kiện hoàn thành:** đọc được toàn bộ tài liệu trong game, vị trí hợp lý với bối cảnh

### F4-F5. Cutscene Intro + Outro

- **Mục tiêu:** cutscene in-engine ngắn cho đầu/cuối mission (GDD mục 3.2)
- **Cách làm:** dùng camera animation đơn giản trong engine, không cần dựng cinematic phức tạp
- **Phụ thuộc:** E2 (cần map để quay cutscene trong đó)
- **Điều kiện hoàn thành:** cutscene chạy đúng, không bug khi có 4 người (ai trigger, có bị treo người khác không)

---

## PHASE G — UI/UX & HUD

### G1. HUD chính (HP, ammo, item)

- **Phụ thuộc:** B1, B3, D2-D5
- **Điều kiện hoàn thành:** hiển thị đúng real-time, đồng bộ đúng cho từng client

### G2. Lobby/Matchmaking UI

- **Phụ thuộc:** A1
- **Điều kiện hoàn thành:** 4 người vào cùng 1 phòng, bắt đầu mission đồng thời

### G3. Downed screen + indicator special infected

- **Phụ thuộc:** B2, C3
- **Điều kiện hoàn thành:** hiển thị rõ ràng, test với người chơi thật xem có dễ hiểu không

---

## PHASE H — AUDIO

### H1. Sound effect cơ bản (súng, zombie, footstep)

- **Phụ thuộc:** D2-D5, C1-C2
- **Điều kiện hoàn thành:** mỗi hành động có sound riêng, không dùng chung 1 âm thanh cho nhiều thứ khác nhau

### H2. Ambient sound theo map

- **Phụ thuộc:** E2, E4
- **Điều kiện hoàn thành:** mỗi map có không khí âm thanh riêng biệt

### H3. Dynamic music tied to AI Director

- **Mục tiêu:** nhạc nền thay đổi theo Stress Value (GDD mục 11) — điểm nhấn thể hiện rõ AI Director
- **Phụ thuộc:** A2 (cần Director logging hoạt động ổn định để hook nhạc vào)
- **Điều kiện hoàn thành:** nhạc tăng/giảm cường độ đúng theo Peak/Relax cycle, test được bằng tai nghe rõ ràng

---

## PHASE I — QA & POLISH

### I1. Playtest với người ngoài nhóm

- **Mục tiêu:** phát hiện bug/balance mà nhóm tự chơi không nhận ra
- **Phụ thuộc:** tất cả phase A-H có bản chạy được (không cần hoàn thiện 100%)
- **Điều kiện hoàn thành:** thu thập được feedback bằng văn bản/ghi âm từ ít nhất 4-6 người chơi thử

### I2. Fix bug theo độ ưu tiên

- **Thứ tự ưu tiên xử lý:** crash > desync multiplayer > gameplay-breaking > cosmetic
- **Phụ thuộc:** I1
- **Điều kiện hoàn thành:** không còn crash/desync nghiêm trọng trong bản build cuối

### I3. Build test trên máy khác

- **Mục tiêu:** đảm bảo game chạy được ngoài máy dev
- **Phụ thuộc:** I2
- **Điều kiện hoàn thành:** chạy ổn định trên ít nhất 1 máy không phải máy phát triển

---

## PHASE J — BÁO CÁO & CHUẨN BỊ BẢO VỆ

### J1. Quay video demo đầy đủ

- **Mục tiêu:** backup phòng khi live-demo lỗi mạng trước hội đồng
- **Phụ thuộc:** I3
- **Điều kiện hoàn thành:** có video quay đầy đủ 1 lượt chơi từ đầu tới cuối, chất lượng đủ rõ để trình chiếu

### J2. Chuẩn bị số liệu minh chứng AI Director

- **Mục tiêu:** biểu đồ Stress Value, spawn rate theo thời gian (từ log ở A2)
- **Phụ thuộc:** A2, nhiều buổi playtest để có đủ dữ liệu
- **Điều kiện hoàn thành:** có ít nhất 2-3 biểu đồ minh họa rõ ràng, sẵn sàng đưa vào slide

### J3. Viết phần kiến trúc kỹ thuật trong báo cáo

- **Mục tiêu:** trình bày rõ kiến trúc networking + AI Director — đây là phần hội đồng SoICT hay hỏi sâu
- **Phụ thuộc:** toàn bộ hệ thống đã ổn định
- **Điều kiện hoàn thành:** có sơ đồ kiến trúc (architecture diagram) rõ ràng trong báo cáo

### J4. Chuẩn bị Q&A

- **Mục tiêu:** dự trù câu hỏi hội đồng hay hỏi ("vì sao chọn Photon/Netcode", "xử lý desync thế nào", "AI Director khác gì so với random spawn thường")
- **Phụ thuộc:** J3
- **Điều kiện hoàn thành:** cả 2 thành viên đều trả lời được các câu hỏi kỹ thuật cốt lõi, không chỉ 1 người hiểu sâu

---

## GHI CHÚ SỬ DỤNG TÀI LIỆU

- Task ID dùng để trao đổi nhanh trong nhóm (VD: "hôm nay tao làm C3, mày làm D1")
- Cột **Phụ thuộc** là thứ tự bắt buộc — không nên bắt đầu 1 task khi task phụ thuộc chưa xong, trừ khi 2 người chia nhau làm song song 2 nhánh độc lập (VD: 1 người làm Phase C trong khi người kia làm Phase D — cả 2 đều phụ thuộc B nhưng độc lập với nhau)
- Các task đánh dấu **"nên cắt nếu thiếu thời gian"** đã được chọn lọc theo mức độ ảnh hưởng thấp nhất tới core value của đồ án — ưu tiên cắt theo đúng thứ tự liệt kê (D6-D7 trước, rồi C4, rồi E5)
