# GAME DESIGN DOCUMENT

## [Tên tạm: "OUTBREAK PROTOCOL"] — Co-op Survival FPS

### Đồ án tốt nghiệp — SoICT, Đại học Bách khoa Hà Nội

_(Tên game là placeholder, nhóm có thể đổi tùy ý — giữ nguyên format tài liệu)_

---

## 0. THÔNG TIN CHUNG

| Mục           | Chi tiết                                                                                                                  |
| ------------- | ------------------------------------------------------------------------------------------------------------------------- |
| Thể loại      | Co-op Survival FPS (First-Person Shooter)                                                                                 |
| Cảm hứng      | Left 4 Dead 2 (AI Director, gunplay, finale) + Resident Evil (atmosphere, khan hiếm tài nguyên, kể chuyện qua môi trường) |
| Số người chơi | 4 người (co-op), hỗ trợ chơi ít hơn 4                                                                                     |
| Nền tảng      | PC                                                                                                                        |
| Góc nhìn      | First-person                                                                                                              |
| Team          | 2 thành viên                                                                                                              |

---

## 1. TẦM NHÌN THIẾT KẾ (Design Pillars)

Mọi quyết định thiết kế trong tài liệu này nên được kiểm tra lại bằng 4 tiêu chí sau — nếu một tính năng không phục vụ ít nhất 1 trong 4 điều này, cân nhắc cắt bỏ:

1. **Căng thẳng đến từ sự khan hiếm, không phải số lượng địch.** Đạn dược, vật phẩm hồi máu có giới hạn thật sự — người chơi phải ra quyết định, không phải chỉ bắn liên tục.
2. **Hợp tác là bắt buộc, không phải tùy chọn.** Cơ chế downed/revive, chia sẻ tài nguyên, và vai trò khác biệt khiến 4 người chơi solo-style sẽ thua.
3. **Kể chuyện qua môi trường trước, thoại sau.** Tài liệu, hiện trường, âm thanh môi trường truyền tải cốt truyện; cutscene chỉ dùng ở các điểm nhấn quan trọng (đầu/cuối mission, twist).
4. **Mỗi lần chơi lại khác nhau.** AI Director đảm bảo không có 2 lần chơi giống hệt nhau trên cùng 1 map.

---

## 2. CỐT TRUYỆN & THẾ GIỚI QUAN

### 2.1 Bối cảnh (Premise)

Một loại virus sinh học thử nghiệm — tạm gọi **"Chủng T-9"** _(đặt tên khác tùy nhóm)_ — bị rò rỉ từ một tổ hợp nghiên cứu - sản xuất tư nhân. Trong vòng 72 giờ, khu vực bị phong tỏa hoàn toàn. Một đội đặc nhiệm tư nhân (không phải quân đội chính quy — tạo lý do hợp lý cho việc chỉ có 4 người, trang bị hạng nhẹ, không có không quân yểm trợ) được thuê để xâm nhập, thu thập dữ liệu/vật phẩm quan trọng, và rút lui trước khi khu vực bị "dọn dẹp" (không rõ bằng cách nào — có thể là không kích, gợi mở phần sau).

### 2.2 Tổ chức & phe phái

- **Bên thuê đội (Client):** một tập đoàn ẩn danh muốn thu hồi dữ liệu/mẫu vật trước khi bị tiêu hủy — không rõ động cơ thật sự (cứu người hay che giấu tội).
- **Đội đặc nhiệm (Player squad):** lính đánh thuê/nhân viên hợp đồng, không phải anh hùng chính nghĩa — điều này cho phép cốt truyện xám (morally grey), phù hợp kể chuyện qua tài liệu môi trường thay vì thoại giải thích dài dòng.

### 2.3 Nhân vật — đề xuất hệ thống 4 vai trò (Role System)

Đây là gợi ý **nên cân nhắc thêm** vì tận dụng tốt cơ chế 4-player co-op đã có, đồng thời không tốn nhiều công nghệ mới (chỉ là balance số liệu + animation set khác nhau cho vũ khí chính):

| Role         | Vai trò gameplay                            | Trang bị đặc trưng                  | Passive                                                               |
| ------------ | ------------------------------------------- | ----------------------------------- | --------------------------------------------------------------------- |
| Assault      | Damage chính diện                           | Rifle + nhiều đạn dự trữ            | +10% tốc độ reload                                                    |
| Recon/Sniper | Xử lý từ xa, phát hiện special infected sớm | Sniper + pistol                     | Phát hiện special infected qua tường ở khoảng cách xa (icon cảnh báo) |
| Medic        | Hỗ trợ sinh tồn                             | Bộ dụng cụ y tế mang được nhiều hơn | Hồi máu đồng đội hiệu quả hơn 20%                                     |
| Heavy        | Tank, chống đỡ special infected             | Shotgun/Machine gun                 | HP tối đa cao hơn, giảm knockback khi bị tấn công                     |

_(Nếu 2 người làm không kịp balance 4 role riêng biệt, có thể rút gọn còn 2 role cho 4 slot — vẫn giữ được sự khác biệt mà giảm khối lượng công việc.)_

### 2.4 Environmental Storytelling — chi tiết loại manh mối

Thay vì liệt kê nội dung cụ thể (sẽ viết ở giai đoạn viết kịch bản chi tiết), đây là **các loại vật thể mang thông tin** nên có trong mỗi map:

- **Tài liệu giấy/màn hình:** email nội bộ, nhật ký nhân viên, báo cáo thí nghiệm — đặt gần bàn làm việc, tủ hồ sơ
- **Hiện trường xác chết có bố cục kể chuyện:** ví dụ 1 xác nằm chắn cửa thoát hiểm với dấu cào trên cửa → gợi ý nạn nhân cố chạy nhưng không kịp
- **Camera an ninh còn hoạt động:** người chơi có thể xem lại đoạn ghi hình ngắn (không bắt buộc, phần thưởng cho người tò mò khám phá)
- **Bảng thông báo/loa phát thanh nội bộ:** thông báo sơ tán, cảnh báo — tạo không khí nhưng không chặn tiến trình chính
- **Vật phẩm cá nhân:** ảnh gia đình, thư chưa gửi — tăng chiều sâu cảm xúc mà không cần thoại

**Nguyên tắc quan trọng:** không bao giờ để game dừng lại để buộc người chơi đọc — mọi tài liệu là optional, chỉ có 1-2 điểm quan trọng ảnh hưởng trực tiếp đến objective (ví dụ: code khóa cửa nằm trong 1 tài liệu).

---

## 3. GAMEPLAY LOOP

### 3.1 Sơ đồ tổng quát

```
Briefing (chọn loadout)
   → Insertion (vào map)
      → Explore & Survive (tìm item + chiến đấu)
         → Objective (lấy vật phẩm mục tiêu)
            → Extraction (finale — giữ chân tới khi được cứu)
               → Debrief (kết quả mission)
```

### 3.2 Chi tiết từng giai đoạn

**Briefing:**

- Người chơi chọn role/loadout trước khi vào map (nếu áp dụng hệ thống role ở mục 2.3)
- Hiển thị mission goal ngắn gọn (dạng văn bản, không cần cutscene riêng cho bước này)

**Insertion:**

- Cutscene ngắn (10-20 giây, in-engine camera) — đội đặc nhiệm tiếp cận facility
- Đây là điểm đặt tone cho map (ánh sáng, âm thanh môi trường bắt đầu ngay từ đây)

**Explore & Survive:**

- Giai đoạn chính, AI Director hoạt động liên tục điều chỉnh nhịp độ
- Người chơi tìm: đạn dược, vật phẩm hồi máu, tài liệu lore, và tiến dần tới khu vực chứa objective
- Có các "choke point" (điểm hẹp bắt buộc đi qua) để Director dễ kiểm soát pacing — kỹ thuật quan trọng từ L4D2 gốc

**Objective:**

- Lấy vật phẩm mục tiêu thường kèm 1 sự kiện kích hoạt (ví dụ: báo động, khóa cửa tự động, spawn horde nhỏ) — tạo cảm giác "điểm không quay đầu"

**Extraction (Finale):**

- Đây là đoạn quan trọng nhất về mặt thiết kế — nên đầu tư kỹ nhất
- Cấu trúc: người chơi tới điểm rút → gọi phương tiện cứu hộ → countdown chờ (60-90 giây) → Director spawn horde liên tục tăng dần + có thể có Tanker xuất hiện → phương tiện tới → hoàn thành
- Đây là nơi Dynamic Difficulty thể hiện rõ nhất: nếu team đang yếu (ít đạn, HP thấp), Director có thể rút ngắn thời gian chờ hoặc giảm cường độ horde cuối, và ngược lại

**Debrief:**

- Màn hình kết quả: thời gian hoàn thành, số zombie hạ, số item thu thập, trạng thái sống sót của từng người

---

## 4. PLAYER CHARACTER & CONTROLLER

### 4.1 Movement (thông số đề xuất — cần tinh chỉnh qua playtest)

| Thông số             | Giá trị đề xuất                              |
| -------------------- | -------------------------------------------- |
| Tốc độ đi bộ         | 4 m/s                                        |
| Tốc độ chạy (sprint) | 6.5 m/s                                      |
| Stamina sprint       | 5 giây liên tục, hồi sau 3 giây không sprint |
| Tốc độ crouch        | 2 m/s                                        |
| Camera FOV mặc định  | 90° (nên cho phép chỉnh trong settings)      |
| ADS (aim down sight) | Giảm FOV ~15%, giảm spread súng ~40%         |

### 4.2 Health & Damage — nên theo hướng RE nhiều hơn L4D2

Khuyến nghị: **không tự động hồi máu** (khác L4D2 pill hồi tạm thời) — điều này phù hợp hơn với tinh thần RE và làm tăng giá trị của item y tế giới hạn.

- HP tối đa: 100
- Không regen theo thời gian
- Vật phẩm hồi máu: giới hạn số lượng mang theo (2-3 cái/người), hồi ~50 HP/cái
- **Trạng thái Downed (ngã gục):** khi HP = 0, không chết ngay mà vào trạng thái bò/không tự đứng dậy được, đồng đội phải tới hồi sinh (revive) trong X giây — nếu không ai tới trong thời gian giới hạn hoặc bị tấn công thêm khi đang downed → chết hẳn (permadeath trong mission đó)
- Nếu tất cả 4 người chết → mission thất bại

### 4.3 Inventory — giới hạn kiểu RE

- Slot vũ khí: 2 (súng chính + súng phụ), không mang cả 6-8 khẩu cùng lúc — buộc người chơi chọn loadout trước mission
- Slot vật phẩm: giới hạn (ví dụ 4 ô) cho thuốc, đạn dự trữ, vật phẩm nhiệm vụ
- Đạn dược: không infinite, phải nhặt trong map — đây là driver chính của tension theo phong cách RE

---

## 5. TWO-LAYER ADAPTIVE DIFFICULTY FRAMEWORK

_(AI Director + Dynamic Difficulty — đây là điểm mới quan trọng nhất của đồ án, nên trình bày kỹ nhất trong báo cáo bảo vệ.)_

### 5.0 Tổng quan kiến trúc

Thay vì gộp chung "AI Director" và "Dynamic Difficulty" thành 1 hệ thống, đồ án tách thành **2 tầng độc lập, mỗi tầng giải quyết 1 bài toán khác nhau, kết nối qua 1 giá trị trung gian duy nhất**:

```
Player Metrics (macro, đo theo encounter)
        │
        ▼
 DYNAMIC DIFFICULTY  ──── lấy cảm hứng Resident Evil
        │
        │  output: DifficultyMultiplier (VD: 0.6 → 1.5)
        ▼
   AI DIRECTOR   ──── lấy cảm hứng Left 4 Dead 2
        │  (vẫn tự chạy chu kỳ Calm → Build-up → Combat → Peak → Relax,
        │   không đổi vì multiplier — chỉ đổi độ "nặng/nhẹ" của Peak)
        │
        │  output: Director State + Base Spawn Value
        ▼
  SPAWN CONTROLLER
        │  finalSpawnCount = directorBaseValue × difficultyMultiplier
        ▼
   Zombie / Special Infected / Loot
```

**Nguyên tắc phân tầng — trả lời 2 câu hỏi khác nhau:**

- **Dynamic Difficulty (RE):** _"Đội này đang chơi giỏi hay kém — nên chơi ở độ khó nào?"_
- **AI Director (L4D2):** _"Ở độ khó đó, trận đấu nên diễn ra theo nhịp độ nào ngay lúc này?"_

Hai câu hỏi này **độc lập về thời gian**: Dynamic Difficulty cập nhật chậm (theo từng encounter), AI Director cập nhật nhanh (theo từng giây/phase) — đây là lý do phải tách 2 tầng thay vì gộp vào 1 công thức, tránh multiplier bị "giật" giữa lúc đang combat.

### 5.1 Tầng 1 — AI Director (Micro Pacing Layer)

**Vai trò:** điều khiển nhịp độ ngắn hạn, hoạt động theo state machine, không quan tâm người chơi giỏi/dở.

**State machine 5 pha:**

```
Calm → Build-up → Combat → Peak → Relax → (quay lại Calm)
```

| Pha      | Mô tả                                                   | Spawn base behavior                                                                         |
| -------- | ------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| Calm     | Vừa qua Relax, môi trường yên tĩnh                      | Gần như không spawn, chỉ ambient                                                            |
| Build-up | Bắt đầu có dấu hiệu nguy hiểm (âm thanh, 1-2 zombie lẻ) | Spawn thấp, tăng dần                                                                        |
| Combat   | Giao tranh chính                                        | Spawn theo base value của map/encounter                                                     |
| Peak     | Cường độ cao nhất trong chu kỳ                          | Spawn base value × 1.5-2, có thể mở khóa trigger special infected                           |
| Relax    | Bắt buộc sau Peak, không thể bỏ qua                     | Spawn gần 0, đây là **thời điểm duy nhất** Dynamic Difficulty được phép cập nhật multiplier |

**Input của Director (giữ nguyên từ bản cũ — vẫn cần thiết ở tầng này):**

| Tham số                 | Cách đo                                             | Dùng để làm gì                                            |
| ----------------------- | --------------------------------------------------- | --------------------------------------------------------- |
| HP người yếu nhất       | Real-time, không chỉ lấy trung bình                 | Quyết định chuyển sớm sang Relax nếu có người nguy kịch   |
| Vị trí/khoảng cách team | Team tản quá xa → spawn không tập trung vào 1 người | Chọn vị trí spawn                                         |
| Thời gian đứng yên      | Phát hiện team bị kẹt/câu giờ                       | Điều chỉnh Build-up kéo dài hay rút ngắn                  |
| Downed gần đây          | Có ai vừa downed trong ~15-20s                      | Ép chuyển Relax sớm (grace period), bất kể đang ở pha nào |

**Output — Director điều khiển gì (giữ nguyên tinh thần bản cũ):**

- Base spawn rate & vị trí spawn theo pha hiện tại
- Loại zombie ưu tiên spawn (không phụ thuộc độ khó, phụ thuộc pha)
- Điều kiện **được phép** trigger special infected (cooldown đã hết + đang ở Peak) — _chỉ mở khóa điều kiện_, không tự quyết có trigger hay không (xem 5.3)

### 5.2 Tầng 2 — Dynamic Difficulty (Macro Difficulty Layer)

**Vai trò:** đo phong độ người chơi trong khoảng thời gian dài hơn, trả về **1 con số nhân duy nhất**, không tự spawn bất cứ thứ gì.

**Nguyên tắc cập nhật quan trọng:** chỉ tính lại `DifficultyMultiplier` khi Director chuyển sang pha **Relax** (giữa 2 đợt combat) — không tính lại giữa lúc đang Combat/Peak. Nếu tính liên tục, multiplier sẽ nhảy giữa chừng 1 encounter, phá vỡ cảm giác "adaptive" thành "khó đoán vô lý".

**Metrics theo dõi (macro, tính theo rolling window dài ~1 encounter):**

| Metric                     | Cách đo                                  | Ảnh hưởng                                 |
| -------------------------- | ---------------------------------------- | ----------------------------------------- |
| Headshot ratio             | % số phát trúng đầu / tổng số phát trúng | Cao → team đang giỏi                      |
| Damage taken trung bình    | Tổng damage nhận / số zombie đã hạ       | Thấp → team đang giỏi                     |
| Số lần downed/encounter    | Đếm trong encounter vừa qua              | Cao → team đang yếu                       |
| Ammo efficiency            | Số zombie hạ / số đạn tiêu thụ           | Cao → team đang giỏi                      |
| Tốc độ hoàn thành mục tiêu | So với baseline thời gian dự kiến        | Nhanh bất thường → có thể tăng nhẹ độ khó |

**Công thức đề xuất:**

```
PerformanceScore = v1*headshot_ratio
                  + v2*(1 - damage_taken_norm)
                  + v3*(1 - downed_count_norm)
                  + v4*ammo_efficiency_norm

DifficultyMultiplier = clamp(0.6 + PerformanceScore * 0.9, 0.6, 1.5)
```

- `PerformanceScore` chuẩn hóa về khoảng 0 → 1 (v1+v2+v3+v4 = 1)
- `DifficultyMultiplier` luôn nằm trong **0.6 → 1.5** — không để về gần 0 (finale mất ý nghĩa) hoặc quá cao (không thể thắng nổi)
- Nên đổi multiplier **từ từ** giữa các encounter (VD: tối đa ±0.15/lần cập nhật) thay vì nhảy đột ngột, để người chơi không cảm nhận được "máy đang chỉnh độ khó" một cách lộ liễu

**Output — Dynamic Difficulty điều khiển gì:**

- `DifficultyMultiplier` nhân vào base spawn count của Director
- HP zombie (không phải Director quản)
- Tỷ lệ rơi loot/đạn (nghịch với multiplier — khó hơn thì loot nhích lên nhẹ để vẫn công bằng)
- Xác suất trigger special infected **khi điều kiện đã mở** (xem 5.3)

### 5.3 Phân chia trách nhiệm rõ ràng giữa 2 tầng

Đây là bảng quan trọng nhất để tránh 2 hệ thống "giẫm chân" nhau khi code — nên đưa nguyên bảng này vào báo cáo:

| Tham số                                          | AI Director | Dynamic Difficulty   |
| ------------------------------------------------ | ----------- | -------------------- |
| Khi nào chuyển Peak/Relax                        | ✅          | ❌                   |
| Vị trí spawn                                     | ✅          | ❌                   |
| Loại zombie ưu tiên theo pha                     | ✅          | ❌                   |
| Số lượng zombie (base value)                     | ✅          | —                    |
| Số lượng zombie (giá trị cuối)                   | —           | ✅ (nhân multiplier) |
| HP / damage của zombie                           | ❌          | ✅                   |
| Tỷ lệ rơi loot/đạn                               | ❌          | ✅                   |
| **Điều kiện được phép** trigger Special Infected | ✅ (gate)   | ❌                   |
| **Xác suất thực sự** trigger khi đã đủ điều kiện | ❌          | ✅ (roll %)          |

**Luồng gọi hàm cho trường hợp Special Infected (ví dụ cụ thể để code):**

1. Director check: đang ở Peak? Cooldown đã hết? → nếu KHÔNG, dừng, không trigger
2. Nếu CÓ → hỏi Dynamic Difficulty roll xác suất (VD: team đang chơi tốt → 70%, đang chơi tệ → 30%)
3. Roll thành công → Spawn Controller thực sự spawn Special Infected

### 5.4 Cá nhân hóa (Per-player tracking)

Áp dụng ở cả 2 tầng, không chỉ tính chỉ số trung bình cả team:

- **Ở tầng Director:** nếu 1 người có HP thấp cụ thể → spawn né hướng người đó, không chỉ nhìn HP trung bình
- **Ở tầng Dynamic Difficulty:** nên cân nhắc dùng metric của **người yếu nhất** làm giới hạn dưới khi tính multiplier (tránh 1 người chơi rất giỏi kéo multiplier lên cao, làm khó 3 người còn lại)

### 5.5 Data Logging — quan trọng cho báo cáo bảo vệ

Ghi log riêng biệt cho từng tầng để chứng minh chúng hoạt động đúng vai trò của mình:

- **Director:** biểu đồ chuyển pha theo thời gian (Calm/Build-up/Combat/Peak/Relax) — dạng timeline
- **Dynamic Difficulty:** biểu đồ `DifficultyMultiplier` theo từng encounter (nên là dạng step chart, vì chỉ đổi tại các điểm Relax, không đổi liên tục)
- **Kết hợp:** biểu đồ overlay số zombie spawn thực tế = base value (từ Director) × multiplier (từ Dynamic Difficulty) tại từng thời điểm — đây là biểu đồ mạnh nhất để chứng minh kiến trúc 2 tầng hoạt động đúng như thiết kế

Đây là phần **có giá trị học thuật cao nhất** của đồ án. Khi trình bày, nên dùng đúng câu framing: _"Đồ án đề xuất một hệ thống điều khiển độ khó hai tầng (Two-layer Adaptive Difficulty Framework), kết hợp Dynamic Difficulty ở mức chiến lược (lấy cảm hứng Resident Evil) và AI Director ở mức chiến thuật (lấy cảm hứng Left 4 Dead 2)"_ — không chỉ mô tả bằng lời mà cần có số liệu/biểu đồ thực tế đi kèm.

---

## 6. ZOMBIE & SPECIAL INFECTED DESIGN

### 6.1 Common Zombie (4-6 loại — dùng chung rig, khác animation/behavior)

| Loại                           | HP  | Tốc độ                     | Damage/hit                 | Hành vi đặc trưng                                                        |
| ------------------------------ | --- | -------------------------- | -------------------------- | ------------------------------------------------------------------------ |
| Walker (cơ bản — đã có)        | 100 | Chậm                       | 10                         | Di chuyển thẳng về phía tiếng động/ánh sáng                              |
| Runner                         | 60  | Nhanh (x1.8 Walker)        | 8                          | Lao thẳng, dễ hạ nhưng nguy hiểm nếu số đông                             |
| Armor/Heavy                    | 250 | Chậm                       | 20                         | Có giáp ở phần thân — khuyến khích bắn đầu, giáp giảm dmg từ súng nhỏ    |
| Crawler                        | 40  | Trung bình, di chuyển thấp | 6 (nhưng tấn công bất ngờ) | Ẩn dưới bàn/xác chết, chỉ lộ diện khi player lại gần — jump-scare design |
| _(nếu làm thêm)_ Spitter-style | 80  | Chậm                       | Ranged (AoE nhẹ)           | Tấn công tầm xa, buộc player phải di chuyển thay vì đứng bắn 1 chỗ       |

### 6.2 Special Infected — chi tiết

**Screamer** (vai trò: báo động/gọi bầy — tương đương Boomer/Witch nhưng theo hướng khác)

- HP: thấp (150) — nhưng nguy hiểm nếu để sống lâu
- Hành vi: lẩn tránh player, khi phát hiện team sẽ phát ra tiếng hét lớn → tăng vọt Stress Value tạm thời và trigger spawn horde ngay lập tức tại vị trí nó đứng
- Chiến thuật buộc player: phải hạ nhanh trước khi nó hét, hoặc chấp nhận horde tới
- Sound design quan trọng: tiếng hét phải nhận diện được từ xa để player có thời gian phản ứng

**[A1 ĐÃ THỰC HIỆN — NETWORK REPLICATION]** Screamer có replicated locomotion/action state, action sequence/start tick và server-authoritative ability trigger; client chỉ chạy animation/audio/VFX presentation. Multi-peer animation gate vẫn chờ kiểm thử thực tế.

**Tanker** (vai trò: tank, buộc team phải tập trung hỏa lực — tương đương Tank L4D2)

- HP: rất cao (1500-2000, cần cả team focus fire)
- Damage: cao, có thể gây knockback mạnh hoặc downed ngay 1-2 hit nếu không né
- Hành vi: lao thẳng, ưu tiên tấn công người gây damage nhiều nhất (aggro system đơn giản)
- Xuất hiện: chỉ nên trigger ở Extraction/Finale hoặc as "mini-boss" điểm nhấn giữa map, không spawn tùy tiện — nên có cooldown dài
- Animation cần riêng: attack (swing/slam), stagger (khi bị dồn damage tới ngưỡng), death

---

## 7. VŨ KHÍ

### 7.1 Base Weapon System (kiến trúc kỹ thuật đề xuất)

Xây dựng 1 `WeaponBase` class/scriptable object chứa các field chung, mỗi khẩu súng là 1 config khác nhau — giúp thêm súng mới nhanh mà không viết lại logic:

```
WeaponBase {
  damage, fireRate, magazineSize, reloadTime,
  recoilPattern, spreadAngle, effectiveRange,
  headshotMultiplier, ammoType
}
```

### 7.2 Danh sách vũ khí đề xuất (6 khẩu core — 2 khẩu mở rộng nếu còn thời gian)

| Loại                    | Tên đề xuất    | Damage                 | Fire rate  | Mag size | Vai trò                                       |
| ----------------------- | -------------- | ---------------------- | ---------- | -------- | --------------------------------------------- |
| Rifle                   | Assault Rifle  | Trung bình             | Cao        | 30       | All-around, súng chính mặc định               |
| Handgun                 | Pistol         | Thấp                   | Trung bình | 15       | Backup, đạn dễ tìm nhất                       |
| Shotgun                 | Combat Shotgun | Rất cao (tầm gần)      | Thấp       | 6-8      | Xử lý cận chiến, hiệu quả với Crawler         |
| Sniper                  | Marksman Rifle | Rất cao (headshot lớn) | Rất thấp   | 5        | Xử lý từ xa, khắc chế Armor zombie (headshot) |
| Machine Gun             | LMG            | Trung bình             | Rất cao    | 60-100   | Suppress horde lớn, độ giật cao               |
| _(mở rộng)_ Handgun phụ | Magnum         | Cao                    | Thấp       | 6        | Damage cao hơn pistol thường, dùng dự phòng   |

**Ammo economy:** đạn cho mỗi loại nên có độ khan hiếm khác nhau — pistol/rifle dễ tìm, sniper/shotgun hiếm hơn để giữ giá trị chiến thuật.

---

## 8. MAP DESIGN

### 8.1 Nguyên tắc chung cho cả 3 map

- Mỗi map có cấu trúc: **Insertion Point → Explore Zone → Objective Zone → Extraction Zone**
- Có ít nhất 1-2 "choke point" (hành lang hẹp) để AI Director dễ kiểm soát pacing combat
- Có ít nhất 1 không gian mở đủ lớn cho horde finale
- Rải item và tài liệu lore theo nguyên tắc "risk vs reward" (item tốt hơn ở xa đường chính, gần nguy hiểm hơn)

### 8.2 Map 1 — Công xưởng (Factory)

- **Theme:** dây chuyền sản xuất, máy móc lớn, không gian công nghiệp tối, nhiều tầng
- **Đặc trưng thiết kế:** hành lang giữa các máy móc tạo choke point tự nhiên; khu vực nhà kho mở làm nơi finale (nhiều hướng địch có thể tràn vào — phù hợp test AI Director spawn đa hướng)
- **Lore gợi ý:** đây là nơi sản xuất/thử nghiệm virus quy mô nhỏ trước khi chuyển tới lab chính

### 8.3 Map 2 — Phòng nghiên cứu (Research Lab)

- **Theme:** hành lang vô trùng, phòng thí nghiệm kính, hệ thống khóa keycard
- **Đặc trưng thiết kế:** cơ chế nhỏ dùng keycard/mã code (tìm trong tài liệu) để mở khu vực chứa objective — tạo mini-puzzle nhẹ, không cần phức tạp
- **Lore gợi ý:** đây là nơi chứa dữ liệu nghiên cứu chính — nhiều tài liệu/log máy tính kể chi tiết nguồn gốc virus

### 8.4 Map 3 — đề xuất: Trạm kiểm dịch / Khu nhà ở nhân viên (Quarantine Checkpoint)

_(Gợi ý thay thế nếu cần ý tưởng cụ thể hơn "công xưởng, phòng nghiên cứu,...")_

- **Theme:** khu vực nhân viên từng cố gắng sơ tán — nhà ở tạm, trạm kiểm soát, hàng rào phong tỏa
- **Đặc trưng thiết kế:** không gian ngoài trời (khác 2 map trong nhà) — tạo cảm giác khác biệt, nhiều điểm bắn tỉa cho Sniper role
- **Lore gợi ý:** đây là nơi kể câu chuyện con người nhất — dấu vết của cuộc sơ tán thất bại, phù hợp environmental storytelling nặng nhất trong 3 map

---

## 9. MULTIPLAYER SYSTEMS

- **Co-op bắt buộc:** cơ chế downed/revive là điểm mấu chốt buộc hợp tác
- **Chia sẻ tài nguyên:** vật phẩm nhặt được là chung cho ai lấy trước — cân nhắc thêm hệ thống "share" đơn giản (drop item cho đồng đội)
  **[A1 ĐÃ THỰC HIỆN]** Pickup dùng server transaction nguyên tử, có race winner/loser, duplicate-result cache và rate limit; cơ chế drop/share vẫn là phần mở rộng.
- **Vai trò khác biệt (nếu áp dụng mục 2.3):** tạo lý do gameplay để 4 người thực sự cần nhau, không chỉ 4 người làm cùng 1 việc
- **Lưu ý kỹ thuật networking (đã lưu ý ở checklist trước):** ưu tiên test đồng bộ animation zombie/special infected và lag compensation cho hit detection — đây là 2 điểm dễ lộ bug nhất khi lên 4 người thật
  **[A1 ĐÃ THỰC HIỆN — LAG COMPENSATION]** Server dùng một server tick thống nhất, rewind tối đa 250 ms, dedupe fire sequence và đã pass test EditMode/PlayMode. Đồng bộ animation đã có compact replicated state trong code, nhưng chưa đánh dấu hoàn tất cho đến khi có test multi-peer thực tế.

---

## 10. UI/UX & HUD

- HUD tối giản kiểu RE: HP, ammo hiện tại, số lượng vật phẩm y tế
- Indicator hướng khi Screamer hét hoặc Tanker xuất hiện (âm thanh + icon hướng)
- Màn hình downed: hiển thị rõ ai đang tới cứu, thời gian còn lại trước khi chết hẳn
- Menu loadout trước mission (chọn 2 vũ khí + role nếu áp dụng)

---

## 11. AUDIO DIRECTION

- **Ambient theo khu vực:** mỗi map có lớp âm thanh nền riêng tạo cảm giác ngột ngạt kiểu RE
- **Dynamic music tied to Director:** nhạc nền thay đổi cường độ theo Stress Value — khi Director chuyển sang "Peak" nhạc tăng nhịp, khi "Relax" nhạc dịu lại. Đây là điểm cộng lớn vì thể hiện rõ AI Director ảnh hưởng tới trải nghiệm tổng thể, không chỉ số lượng zombie
- **Sound cue đặc trưng** cho từng special infected để player nhận diện threat từ xa mà không cần nhìn thấy

---

## 12. GHI CHÚ MỞ RỘNG (Stretch Goals — chỉ làm nếu còn dư thời gian)

- Cơ chế lây nhiễm dần khi bị cắn (thay vì chết ngay) — tăng chiều sâu quyết định nhưng tốn thêm state machine cho player
- Special infected thứ 3 (Spitter-style tấn công tầm xa)
- Map thứ 4 hoặc chế độ Versus (1 người điều khiển special infected) — chỉ nên nghĩ tới nếu core loop đã hoàn thiện và ổn định sớm

---

_Tài liệu này là bản nháp cấu trúc — các con số (HP, damage, tốc độ...) đều là điểm khởi đầu, cần tinh chỉnh qua playtest thực tế trước khi chốt final balance._
