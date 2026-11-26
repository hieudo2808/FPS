# Bước 1 — Bản chuẩn hóa 100% Tiếng Anh: 20 Objectives, 21 Files & 28 Radio

- **Trạng thái:** Chờ duyệt (Bước 1 — Tài liệu nội dung & cấu trúc; **chưa chỉnh sửa** code, scene, prefab hay `CampaignContentCatalog.asset`)
- **Ngày lập:** 2026-11-26
- **Nguồn đối chiếu:**
  - Canon cốt truyện & ID ổn định: [Outbreak-Protocol-Master-Canon.md](file:///d:/du%20an/FPSGAME-HIEU/FPS/Documents/Outbreak-Protocol-Master-Canon.md)
  - Kịch bản Radio bản 48: [48-radio-kich-ban-cho-duyet.md](file:///d:/du%20an/FPSGAME-HIEU/FPS/Reports/48-radio-kich-ban-cho-duyet.md)
  - Feedback playtest & đề xuất UX: [57-feedback-playtest-2026-11-22.md](file:///d:/du%20an/FPSGAME-HIEU/FPS/Reports/57-feedback-playtest-2026-11-22.md)
  - Luật & cấu trúc dữ liệu hiện hành: [CampaignTypes.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs), [CampaignContentCatalog.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignContentCatalog.cs), [CampaignDialogue.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignDialogue.cs)
- **Nguyên tắc bảo toàn hệ thống:**
  1. Giữ nguyên 100% ID ổn định: `CampaignObjectiveId` (`1–20`), `CampaignFileId` (`1–21`), `CampaignKeyItemId` (`1–4`), `CampaignDialogueId` (`D01–D28`).
  2. Giữ nguyên đáp án logic trong `CampaignRules.TryComplete`: `FactoryShipping` = `(1, 1)`, `AsylumPatient` = `1`, `AsylumTransfer` = `0`, `LabPower` = mask `13`, `LabArchive` = `1`.
  3. Không thêm yếu tố ngoài canon (không có thuốc chữa, không có thanh tẩy toàn bộ, không có NPC đi cùng hay kết thúc giải cứu toàn bộ bệnh nhân).
  4. Toàn bộ chữ hiển thị cho người chơi trong game dùng **100% Tiếng Anh** thống nhất một bộ thuật ngữ.

---

## Phần 1 — Bảng từ điển chuẩn 1 Lore (Single Source of Truth — English Only)

### 1.1. Danh xưng, thực thể & thuật ngữ bất biến

| Nhóm | Thuật ngữ chuẩn trong Game (EN) | Ghi chú nhất quán |
|---|---|---|
| Tổ chức điều phối | **BRT** (Biohazard Response Team) | Đơn vị cử đội đi thu hồi chứng cứ |
| Đội người chơi | **Vanguard** (`Mason`, `Doc`, `Vega`, `Raven`) | `Brimstone/Sage/Gekko/Clove` chỉ dùng ở prefab/enum nội bộ |
| Chỉ huy qua radio | **Command** | Không đặt tên riêng hoặc quân hàm |
| Thế lực cản trở | **Client** (hoặc *contractor* trong văn bản cũ) | Chỉ xuất hiện trực tiếp qua sóng ở `D22–D23` |
| Mầm bệnh & vật chứng | **T-9** · **T9-17 Evidence Case** · **AL-04 / K6** | Vật chứng trung tâm nối 3 chương |
| 3 chương & địa điểm | **Factory** → **Northbridge Care Institute** (**Northbridge**) → **Laboratory B2** (**B2**) | Giữ đúng 3 tên địa điểm trên Tracker & Radio |
| Nhóm & bệnh nhân | **Group C12** · **Patient P046** · **E-02 Original Archive** | Không đổi mã số |
| 3 nhân chứng đã mất | **Dr. Adrian Cole**, **Marcus Hale**, **Maya Reed** | Chỉ tồn tại qua hồ sơ/ghi âm, không spawn NPC |
| Vũ khí | `Classic`, `Vandal`, `Operator`, `Odin`, `Bucky`, `Warden` | Giữ nguyên tên vũ khí của dự án |

### 1.2. Cấu trúc HUD 3 tầng (`Why → What → Where`)

Mỗi màn chơi hiển thị cùng một cấu trúc giúp người mới nhìn 1 giây là hiểu **vì sao phải làm**, **bước kế tiếp là gì**, và **đi hướng nào**:

| Chương | Tầng 1 — `Location / Goal (Why)` | Tầng 2 — `Current Step (What)` | Tầng 3 — `Waypoint (Where)` |
|---|---|---|---|
| **Chapter 1** | `FACTORY — RECOVER THE T9-17 CASE` | Hai nhánh song song:<br>• `POWER (0/3)`: `Isolate production` → `Engage cold-store backup` → `Start cold-store generator`<br>• `LOGISTICS (0/2)`: `Read delivery manifest` → `Verify shipping release`<br>Sau đó: `Eliminate the Tank` → `Secure T9-17 case` → `Open service route` | Khớp 100% tên động từ bước đang track + khoảng cách (ví dụ: `Isolate production · 18m`). Bỏ nhãn mơ hồ `SOURCE DOCUMENT`. |
| **Chapter 2** | `NORTHBRIDGE — TRACE P046 & REACH B2` | `Read security desk log` → `Recover service fuse` → `Install service fuse` → `Restore lower power` → `Verify P046 intake` → `Flag mortuary contradiction` → `Call B2 freight lift` | Khớp tên bước + khoảng cách; nhắc rõ `Evade the Tank — Reach the B2 lift` khi phase chuyển `Preparing`/`Encounter`. |
| **Chapter 3** | `LABORATORY B2 — TRANSMIT COMPLETE E-02` | `Trace B2 transfer` → `Read B2 archive index` → `Allocate emergency power` → `Verify original E-02 archive` → `Secure E-02 case` → `Transmit full E-02 archive` | Khớp tên bước + khoảng cách; khi truyền tin nhắc rõ `Stay mobile — Tank incoming`. |

### 1.3. Quy ước UX hỗ trợ người mới (áp dụng ở Bước 2 & Bước 3 sau khi duyệt)

- **Nhịp gợi ý rút ngắn (`CampaignRules.HintTier`):** `Tier 1 = 25s` · `Tier 2 = 45s` · `Tier 3 = 75s` (thay cho `45 / 90 / 135s`).
- **Màu sắc gợi ý thống nhất:** Dùng token `SelectedUiTheme.Current.Accent` (`#E9A64C` — theo quy chuẩn tô màu ở **Mục 1.4**) để tách biệt rõ với chữ thông thường.
- **Tách Nhật ký khỏi Túi đồ & Tóm tắt tại chỗ:** Đổi nhãn sang **`MISSION JOURNAL & EVIDENCE`** (gán phím riêng `[J]`), giữ **`KEY DATA`** nổi bật ở đầu trang (tô màu từ khóa theo **Mục 1.4**) và gắn sẵn tab tham chiếu hồ sơ nguồn ngay trong panel đối chiếu để người mới không phải mở đi mở lại nhiều lần.
- **Phím quét mục tiêu / tiếp tế (`[V]`):** Khi bấm sẽ bật lại vòng sáng/chấm tròn đánh dấu các điểm nhặt trang bị & hồ sơ trong tầm gần.

### 1.4. Quy chuẩn tô màu thuật ngữ, manh mối giải đố & hướng dẫn nhiệm vụ (Highlight Standard)

#### A. Bảng màu chuẩn theo `SelectedUiTheme` (Không hardcode màu ngoài Theme)

Toàn bộ phần tô màu bám chặt 4 token màu đã chuẩn hóa của dự án tại [SelectedUiTheme.cs:12-15](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/UI/Runtime/SelectedUiTheme.cs#L12-L15):

| Token (`SelectedUiTheme`) | Mã màu mặc định | Vai trò hiển thị trên HUD · Radio · Nhật ký |
|---|---|---|
| `Theme.Text` | `#F3F3F3` *(Trắng)* | Văn bản chính (mô tả bước, câu thoại, nội dung hồ sơ). |
| `Theme.Accent` | `#E9A64C` *(Cam hổ phách)* | **1 màu nhấn thống nhất** cho thuật ngữ cốt lõi, mã/manh mối giải đố, tên thiết bị/khu vực đích, phím bấm (`[F]`, `[J]`, `[V]`) và động từ chỉ hành động chính. |
| `Theme.Danger` | `#D84B4B` *(Đỏ)* | Chỉ dùng cho cảnh báo nguy hiểm, trạng thái khóa, hoặc yếu tố cần loại trừ (`Tank`, `Screamer`, `Infector`, `down!`, `DECEASED`, `03:05`, `TEST RIG OFF`, `LOCKED`). |
| `Theme.Muted` | `#AEB2B2` *(Xám)* | Nhãn phụ, tên tác giả, thời gian phụ, dữ liệu nhiễu/chú thích nền. |

> **Lưu ý linh hoạt theo Precedence:** Nếu ở bước hoàn thiện UI bạn muốn đổi màu nhấn manh mối từ Cam hổ phách sang Vàng hoặc Xanh dương sáng (theo đề xuất tại [57-feedback-playtest-2026-11-22.md](file:///d:/du%20an/FPSGAME-HIEU/FPS/Reports/57-feedback-playtest-2026-11-22.md#L97)), chỉ cần đổi giá trị token trong `SelectedUiTheme` mà **không phải sửa lại text trong `CampaignContentCatalog`**.

#### B. Từ điển cụm từ được tô màu (Keyword Color Map)

| Phạm vi | Nhóm `Theme.Accent` (Manh mối · Đích đến · Hành động) | Nhóm `Theme.Danger` (Nguy hiểm · Loại trừ · Khóa) |
|---|---|---|
| **Chung (HUD & Radio)** | `BRT`, `Vanguard`, `T9-17`, `AL-04`, `K6`, `Northbridge`, `Group C12`, `P046`, `Level B2`, `E-02`, `[F]`, `[J]`, `[V]`, `preparation zone`, `boarding zone`, `freight lift` | `T-9`, `Tank`, `Screamer`, `Infector`, `I'm down!`, `LOCKED`, `ignore that order`, `Do not` |
| **Ch.1 — Factory** | `PRODUCTION`, `west breaker`, `BACKUP COLD STORE`, `east breaker`, `physical lever`, `Delivery Manifest`, `NORTHBRIDGE / 02:40`, `AL-04 / K6`, `service exit`, `refrigerated` | `green alarm lamp`, `shutters closed`, `Kill any Tank` |
| **Ch.2 — Northbridge** | `security desk`, `02:40`, `Group C12`, `maintenance storage`, `Service Fuse`, `upper records room`, `P046 / C12 / 02:40`, `ALIVE`, `03:50`, `B2 Access Card`, `Evade the Tank` | `DECEASED`, `03:05`, `contradiction`, `empty drawer` |
| **Ch.3 — Lab B2** | `B2 Archive Index`, `E-02`, `6 units`, `SAFETY (2)`, `DATA (2)`, `FREIGHT LIFT (2)`, `E-02 / T9-17 / C12 / P046`, `full copy`, `patient identities`, `Dr. Adrian Cole`, `halt order` | `TEST RIG (4) OFF`, `Overload`, `sample-only`, `scrubbed`, `refuse receipt` |

#### C. Quy tắc áp dụng đồng bộ trên 3 bề mặt (HUD · Radio · Nhật ký)

1. **Giới hạn mật độ:** Tối đa **2–3 cụm tô màu / dòng hoặc cue thoại**, tuyệt đối không tô cả câu dài để tránh loãng điểm nhìn khi đang bắn zombie.
2. **Trên HUD (`CampaignHUD` — Tracker, Hints, Prompt `[F]`):**
   - Tầng 1 (`Goal`) giữ màu trắng đậm `Theme.Text`; Tầng 2 (`Current Step`) giữ động từ hành động rõ, tô `Theme.Accent` cho tên mục tiêu/mã cần chú ý và `Theme.Danger` cho chữ `Tank` / `LOCKED`.
   - Cả 3 cấp gợi ý (`Hint 1/2/3` tại `25s / 45s / 75s`) tự động tô `Theme.Accent` cho từ khóa chỉ đường/đáp án kiểm tra.
3. **Trên Lời thoại Radio (`D01`–`D28` & `soloCues`):**
   - Tiền tố người nói (`Command:`, `Mason:`, `Raven:`, `Vega:`, `Doc:`, `Client:`) và các cụm trong bảng từ điển được tô màu bằng `<color=#...>` tại runtime (`supportRichText = true`).
   - **Quy tắc đếm 40 ký tự:** Tag rich-text `<color=#...>` **không tính vào độ dài hiển thị `≤ 40 ký tự/dòng`**.
4. **Trong Nhật ký & Panel tóm tắt (`FILE_01`–`FILE_21`):**
   - Toàn bộ các cụm **`**...**`** trong phần **`KEY DATA` (`summary`)** và **`BODY` (`body`)** ở **Phần 3** là danh sách chính xác các cụm sẽ được tô màu tự động (`Theme.Accent` cho mã/quy trình/đích đến; `Theme.Danger` cho `DECEASED`, `03:05`, `TEST RIG OFF`).

#### D. Ví dụ hiển thị thực tế trên 3 bề mặt

- **HUD Hint (`25s / 45s`):**
  - `"Check the <color=#E9A64C>west breaker</color> panel and read <color=#E9A64C>Factory Utilities Procedure</color>."`
  - `"<color=#D84B4B>Tank</color> incoming — <color=#E9A64C>Evade the Tank</color> and board the <color=#E9A64C>B2 lift</color>."`
- **Lời thoại Radio (`D11` & `D16`):**
  - `Doc: Command, mortuary logged` / `<color=#E9A64C>P046</color> <color=#D84B4B>dead at 03:05</color>.`
  - `Doc: Same wristband, same <color=#E9A64C>P046</color>.` / `Transferred <color=#E9A64C>alive at 03:50</color>.`
  - `Command: Don't waste ammo fighting it.` / `<color=#E9A64C>Evade the Tank</color>, board the lift.`
- **Nhật ký `KEY DATA` (`FILE_02` & `FILE_08`):**
  - `Shipment / Seal: <color=#E9A64C>AL-04 / K6</color> (Lot <color=#E9A64C>T9-17</color>)`
  - `Destination / Time: <color=#E9A64C>NORTHBRIDGE / 02:40</color> (<color=#E9A64C>Group C12</color>)`
  - `Required ON: <color=#E9A64C>SAFETY (2) + DATA (2) + FREIGHT LIFT (2) = 6</color> | OFF: <color=#D84B4B>TEST RIG (4)</color>`

> **Ghi chú kỹ thuật (cho Bước 2–3):** Dùng 1 helper dùng chung (ví dụ `CampaignTextHighlight.Format(text, SelectedUiTheme.Current)`) đọc màu động từ `SelectedUiTheme` (tương tự pattern `ColorUtility.ToHtmlStringRGB` đang dùng ở [CampaignHUD.Navigation.cs:155, 211](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs#L155-L211)), đảm bảo 1 nơi định nghĩa màu cho cả HUD, Radio và Mission Journal.

---

## Phần 2 — Chuẩn hóa 20 Objectives (`CampaignObjectiveId` 1–20) & 4 Key Items

> Quy ước: `Title` dùng chung cho Tracker, `[F]` Prompt và Waypoint. Mọi `Title` đều bắt đầu bằng động từ hành động. Đáp án trong `Choices` giữ nguyên index khớp `CampaignRules.TryComplete`.

### 2.1. Chapter 1 — Factory (Objectives 1–7)

#### 1. `FactoryIsolate` *(Branch: Power 1/3)*
- **Title (HUD / [F] / Waypoint):** `Isolate production power`
- **Instructions:** `Shut off the PRODUCTION branch at the west breaker. Leave the cold-store circuit connected so the T9-17 case stays refrigerated.`
- **Result:** `Production power isolated.`
- **Linked File:** `FILE_01` (`FactoryProcedure`)
- **Choices:** *(Hold interaction)*
- **Hint 1 (25s):** `Check the west breaker panel and read Factory Utilities Procedure.`
- **Hint 2 (45s):** `Isolate the PRODUCTION branch first; do not cut power to Cold Storage.`
- **Hint 3 (75s):** `Hold [F] at the West Production Breaker marked by the POWER waypoint.`

#### 2. `FactoryBackup` *(Branch: Power 2/3)*
- **Title (HUD / [F] / Waypoint):** `Engage cold-store backup`
- **Instructions:** `Switch the east panel to BACKUP COLD STORE feed so the refrigeration generator can start.`
- **Result:** `Cold-store backup engaged.`
- **Linked File:** `FILE_01` (`FactoryProcedure`)
- **Choices:** *(Hold interaction)*
- **Hint 1 (25s):** `Production is isolated. Move to the east backup feed panel.`
- **Hint 2 (45s):** `Follow the POWER waypoint to the east breaker and switch on the backup feed.`
- **Hint 3 (75s):** `Hold [F] at the East Cold-Store Backup Panel to ready the generator.`

#### 3. `FactoryGenerator` *(Branch: Power 3/3)*
- **Title (HUD / [F] / Waypoint):** `Start cold-store generator`
- **Instructions:** `Pull the physical generator control lever to restore cold-store power. Ignore the green alarm lamp—it lights before the contactor closes.`
- **Result:** `Cold-store power restored.`
- **Linked File:** `FILE_01` (`FactoryProcedure`)
- **Choices:** *(Hold interaction)*
- **Hint 1 (25s):** `Backup feed is ready. Find the cold-store generator control.`
- **Hint 2 (45s):** `Do not rely on the green lamp. Use the physical generator lever.`
- **Hint 3 (75s):** `Hold [F] at the Cold-Store Generator lever following the POWER waypoint.`

#### 4. `FactoryManifest` *(Branch: Logistics 1/2)*
- **Title (HUD / [F] / Waypoint):** `Read delivery manifest`
- **Instructions:** `Collect the original Delivery Manifest to log the shipment number, security seal, destination, and intake time.`
- **Result:** `Delivery manifest recorded in Journal.`
- **Linked File:** `FILE_02` (`FactoryManifest`)
- **Choices:** *(File pickup / Read)*
- **Hint 1 (25s):** `Follow the LOGISTICS waypoint to find the Delivery Manifest.`
- **Hint 2 (45s):** `Look for the paper manifest near the dispatch desk in the factory.`
- **Hint 3 (75s):** `Press [F] on the Delivery Manifest sheet to save AL-04 / K6 details.`

#### 5. `FactoryShipping` *(Branch: Logistics 2/2)*
- **Title (HUD / [F] / Waypoint):** `Verify shipping release`
- **Instructions:** `Match both shipment fields against your recorded Delivery Manifest (FILE_02). Wrong selections do not lock the terminal.`
- **Result:** `Shipping release verified: AL-04 / K6 to Northbridge at 02:40.`
- **Linked File:** `FILE_02` (`FactoryManifest`)
- **Choices (Group 1 — Shipment / Seal):**
  - `[0]` `AL-03 / K9`
  - `[1]` `AL-04 / K6` ✅ *(Correct)*
  - `[2]` `AL-07 / K2`
- **SecondaryChoices (Group 2 — Destination / Receiving Time):**
  - `[0]` `NORTH DEPOT / 06:10`
  - `[1]` `NORTHBRIDGE / 02:40` ✅ *(Correct)*
  - `[2]` `NORTHBRIDGE / 04:20`
- **Hint 1 (25s):** `Compare the terminal fields with the Delivery Manifest saved in your Journal.`
- **Hint 2 (45s):** `Check both groups separately: Shipment/Seal and Destination/Time.`
- **Hint 3 (75s):** `Match Shipment AL-04 / Seal K6 and Destination Northbridge / 02:40.`

#### 6. `FactoryCase` *(Prerequisite: Power + Logistics + Defeat Factory Tank)*
- **Title (HUD / [F] / Waypoint):** `Secure T9-17 case`
- **Instructions:** `With cold-store power active, logistics verified, and the Tank eliminated, retrieve the sealed T9-17 Evidence Case and its transfer addendum.`
- **Result:** `T9-17 Evidence Case secured. Seal intact.`
- **Linked File:** `FILE_03` (`FactoryB2Transfer` — auto-discovered on pickup)
- **Choices:** *(Hold interaction)*
- **Hint 1 (25s):** `Complete Power and Logistics first, then eliminate the Tank guarding cold storage.`
- **Hint 2 (45s):** `The Tank must be killed before the cold-store case unlocks.`
- **Hint 3 (75s):** `Approach the refrigerated vault and hold [F] to secure the T9-17 Evidence Case.`

#### 7. `FactoryRoute` *(Encounter Trigger)*
- **Title (HUD / [F] / Waypoint):** `Open service route`
- **Instructions:** `Activate the service-route interlock cabinet. Once unlocked, gather the squad in the preparation zone and hold until the gate opens.`
- **Result:** `Service interlock cycling. Hold the control room.`
- **Linked File:** `None`
- **Choices:** *(Hold interaction inside preparation area)*
- **Hint 1 (25s):** `With T9-17 secured, follow the waypoint to the service-route cabinet.`
- **Hint 2 (45s):** `Hold [F] at the service-route panel while standing inside the preparation zone.`
- **Hint 3 (75s):** `Have the squad gather at the service exit, ready up, and defend until the interlock clears.`

---

### 2.2. Chapter 2 — Northbridge Care Institute (Objectives 8–14)

#### 8. `AsylumAccess`
- **Title (HUD / [F] / Waypoint):** `Read security desk log`
- **Instructions:** `Inspect the security desk log to uncover how shipment AL-04 entered the institute after public admissions closed.`
- **Result:** `Security desk log recorded.`
- **Linked File:** `FILE_04` (`AsylumDeskLog`)
- **Choices:** *(Hold interaction)*
- **Hint 1 (25s):** `Search the ground-floor reception security desk for the intake log.`
- **Hint 2 (45s):** `Follow the waypoint to the front desk and inspect the security log.`
- **Hint 3 (75s):** `Hold [F] at the Security Desk Log to reveal the service intake and spare fuse location.`

#### 9. `AsylumFuse`
- **Title (HUD / [F] / Waypoint):** `Recover service fuse`
- **Instructions:** `Collect the replacement fuse from ground-floor maintenance storage. The fuse is shared across the squad.`
- **Result:** `Service fuse acquired.`
- **Linked File:** `None`
- **Choices:** *(Hold interaction)*
- **Hint 1 (25s):** `The Security Desk Log points to ground-floor maintenance storage.`
- **Hint 2 (45s):** `Check the ground-floor maintenance room marked by the waypoint.`
- **Hint 3 (75s):** `Hold [F] on the spare Service Fuse inside ground-floor maintenance storage.`

#### 10. `AsylumInstall`
- **Title (HUD / [F] / Waypoint):** `Install service fuse`
- **Instructions:** `Fit the replacement fuse into the lower service breaker box so power can be restored to the upper records room.`
- **Result:** `Service fuse installed.`
- **Linked File:** `None`
- **Choices:** *(Hold interaction)*
- **Hint 1 (25s):** `Bring the spare fuse to the lower service electrical panel.`
- **Hint 2 (45s):** `Follow the waypoint to the lower service panel near the maintenance corridor.`
- **Hint 3 (75s):** `Hold [F] at the fuse box to install the Service Fuse.`

#### 11. `AsylumPower`
- **Title (HUD / [F] / Waypoint):** `Restore lower power`
- **Instructions:** `Throw the lower service breaker to bring the upper records room terminal online.`
- **Result:** `Upper records room powered.`
- **Linked File:** `None`
- **Choices:** *(Hold interaction)*
- **Hint 1 (25s):** `The fuse is fitted. Flip the lower service power switch.`
- **Hint 2 (45s):** `Use the power lever right next to the newly installed fuse box.`
- **Hint 3 (75s):** `Hold [F] on the Lower Service Breaker to restore power upstairs.`

#### 12. `AsylumPatient`
- **Title (HUD / [F] / Waypoint):** `Verify P046 intake`
- **Instructions:** `Read the Intake Record (FILE_05) and select the patient entry that matches Group C12 and receiving time 02:40 from the desk log.`
- **Result:** `Patient P046 confirmed under Group C12 (02:40).`
- **Linked File:** `FILE_05` (`AsylumPatientRecord`)
- **Choices:**
  - `[0]` `P041 / C08 / 01:20`
  - `[1]` `P046 / C12 / 02:40` ✅ *(Correct)*
  - `[2]` `P064 / C12 / 04:20`
- **Hint 1 (25s):** `Go upstairs to the powered records room and read the Intake Record first.`
- **Hint 2 (45s):** `Cross-check the Intake Record against Group C12 and time 02:40 in FILE_04.`
- **Hint 3 (75s):** `Select the entry matching Patient P046, Group C12, receiving time 02:40.`

#### 13. `AsylumTransfer`
- **Title (HUD / [F] / Waypoint):** `Flag mortuary contradiction`
- **Instructions:** `Inspect the Mortuary Transfer Discrepancy (FILE_06) and log the contradiction between the death entry and the B2 freight transfer.`
- **Result:** `Contradiction logged. B2 Access Card acquired.`
- **Linked File:** `FILE_06` (`AsylumMortuaryTransfer`)
- **Choices:**
  - `[0]` `CONTRADICTION: DEATH 03:05 / LIVE TRANSFER 03:50` ✅ *(Correct)*
  - `[1]` `CONSISTENT: DEATH FOLLOWED BY BODY COLLECTION`
  - `[2]` `UNRELATED PATIENTS WITH SIMILAR REFERENCES`
- **Hint 1 (25s):** `Follow the waypoint to the mortuary desk and read the discrepancy report.`
- **Hint 2 (45s):** `Compare the mortuary time (03:05) with the living B2 transfer time (03:50) for P046.`
- **Hint 3 (75s):** `Confirm the contradiction (Death 03:05 vs Live Transfer 03:50) to claim the B2 Access Card.`

#### 14. `AsylumLift` *(Encounter Trigger)*
- **Title (HUD / [F] / Waypoint):** `Call B2 freight lift`
- **Instructions:** `Scan the B2 Access Card at the freight lift console. Stay in the lift landing zone until the cabin arrives—you do not need to kill the Tank.`
- **Result:** `B2 freight lift inbound. Evade the Tank and prepare to board.`
- **Linked File:** `None`
- **Choices:** *(Hold interaction inside preparation area)*
- **Hint 1 (25s):** `Take the B2 Access Card to the freight lift console.`
- **Hint 2 (45s):** `Hold [F] at the B2 Lift Panel, then gather in the preparation area.`
- **Hint 3 (75s):** `Keep moving to evade the Tank until the lift doors open, then board immediately.`

---

### 2.3. Chapter 3 — Laboratory B2 (Objectives 15–20)

#### 15. `LabTrace`
- **Title (HUD / [F] / Waypoint):** `Trace B2 transfer`
- **Instructions:** `Check the B2 receiving terminal to link Northbridge shipment AL-04 and patient P046 to the underground lab directory.`
- **Result:** `B2 transfer traced to archive directory.`
- **Linked File:** `None`
- **Choices:** *(Hold interaction)*
- **Hint 1 (25s):** `Check the B2 receiving terminal near the freight lift landing.`
- **Hint 2 (45s):** `Follow the waypoint to the B2 intake console to trace where P046 was logged.`
- **Hint 3 (75s):** `Hold [F] at the B2 Transfer Console to unlock the archive trail.`

#### 16. `LabIndex`
- **Title (HUD / [F] / Waypoint):** `Read B2 archive index`
- **Instructions:** `Inspect the restricted B2 Archive Index to identify which original archive connects T9-17, Group C12, and Patient P046.`
- **Result:** `B2 Archive Index recorded.`
- **Linked File:** `FILE_07` (`LabIndex`)
- **Choices:** *(Hold interaction)*
- **Hint 1 (25s):** `Locate the B2 Archive Index before opening the main archive terminal.`
- **Hint 2 (45s):** `Follow the waypoint to the operations desk and inspect the archive directory.`
- **Hint 3 (75s):** `Hold [F] on the B2 Archive Index to record the original archive ID (E-02).`

#### 17. `LabPower`
- **Title (HUD / [F] / Waypoint):** `Allocate emergency power`
- **Instructions:** `Read Maya's Emergency Power note (FILE_08). Total capacity is 6 units: keep SAFETY (2) online, turn TEST RIG (4) OFF, and enable DATA (2) + FREIGHT LIFT (2).`
- **Result:** `Emergency power balanced: SAFETY, DATA, and FREIGHT LIFT online.`
- **Linked File:** `FILE_08` (`LabPowerRecord`)
- **Choices (Bitmask Selection):**
  - `SAFETY` (`+2`, locked ON = bit 1)
  - `TEST RIG` (`+4`, must be OFF = bit 2)
  - `DATA` (`+2`, must be ON = bit 4)
  - `FREIGHT LIFT` (`+2`, must be ON = bit 8)
  - ✅ **Correct Bitmask:** `13` (`SAFETY + DATA + FREIGHT LIFT = 6 units`)
- **Hint 1 (25s):** `Read Emergency Power Allocation (FILE_08) at the power panel.`
- **Hint 2 (45s):** `Total load cannot exceed 6 units. TEST RIG (4) leaves no room for DATA and LIFT.`
- **Hint 3 (75s):** `Turn TEST RIG off, then switch DATA and FREIGHT LIFT on alongside SAFETY (6/6).`

#### 18. `LabArchive`
- **Title (HUD / [F] / Waypoint):** `Verify original E-02 archive`
- **Instructions:** `Search the powered archive terminal and select the unredacted original archive matching T9-17, Group C12, and Patient P046 (FILE_07).`
- **Result:** `Original E-02 archive verified with full patient table and Cole's halt order.`
- **Linked File:** `FILE_09` (`LabOriginalArchive` — auto-discovered on completion)
- **Choices:**
  - `[0]` `E-01 / T9-03 / C08`
  - `[1]` `E-02 / T9-17 / C12 / P046` ✅ *(Correct)*
  - `[2]` `E-03 / EQUIPMENT ONLY`
- **Hint 1 (25s):** `With DATA powered, open the Archive Terminal and check FILE_07 in your Journal.`
- **Hint 2 (45s):** `Reject sample-only or earlier logs. Match T9-17, Group C12, and P046.`
- **Hint 3 (75s):** `Select E-02 / T9-17 / C12 / P046 to recover the unredacted archive.`

#### 19. `LabCase`
- **Title (HUD / [F] / Waypoint):** `Secure E-02 case`
- **Instructions:** `Collect the physical E-02 Laboratory Evidence Case while keeping the T9-17 Factory Case secured.`
- **Result:** `E-02 Evidence Case secured. Both cases accounted for.`
- **Linked File:** `None`
- **Choices:** *(Hold interaction)*
- **Hint 1 (25s):** `Now that E-02 is verified, collect the physical E-02 Evidence Case.`
- **Hint 2 (45s):** `Follow the waypoint to the vault beside the archive terminal.`
- **Hint 3 (75s):** `Hold [F] to secure the E-02 Evidence Case before transmitting.`

#### 20. `LabTransmit` *(Encounter Trigger)*
- **Title (HUD / [F] / Waypoint):** `Transmit full E-02 archive`
- **Instructions:** `Start the off-site transmission to BRT and ready the squad in the preparation zone. Keep moving once the upload starts—you do not need to kill the Tank.`
- **Result:** `Transmission active. Hold out until BRT confirms receipt.`
- **Linked File:** `None`
- **Choices:** *(Hold interaction inside preparation area)*
- **Hint 1 (25s):** `Take both cases to the transmission terminal and start the BRT upload.`
- **Hint 2 (45s):** `Hold [F] at the transmission console, then stay inside the preparation zone.`
- **Hint 3 (75s):** `Do not stop to fight the Tank once upload begins—survive until BRT confirms receipt, then board the lift.`

---

### 2.4. Chuẩn hóa 4 Key Items (`CampaignKeyItemId` 1–4)

| ID | Enum | Title | Description (EN) | Acquired After | Consumed After | Persistence |
|---|---|---|---|---|---|---|
| 1 | `FactoryEvidenceCase` | `T9-17 Factory Evidence Case` | `Tamper-sealed cold-storage sample case from Factory shipment AL-04 (Seal K6). Retained through extraction.` | `FactoryCase` | `None` | `persistAfterUse = true` (Secured through ending) |
| 2 | `ServiceFuse` | `Replacement Service Fuse` | `Heavy-duty fuse recovered from Northbridge maintenance storage to restore lower service power.` | `AsylumFuse` | `AsylumInstall` | Removed after installation |
| 3 | `B2AccessCard` | `B2 Freight Lift Keycard` | `Restricted security keycard retained with the P046 discrepancy file to summon the underground B2 lift.` | `AsylumTransfer` | `AsylumLift` | Removed after lift authorization |
| 4 | `LabEvidenceCase` | `E-02 Laboratory Evidence Case` | `Physical backup drive and signed ledger from Laboratory B2 containing the unredacted E-02 archive.` | `LabCase` | `None` | `persistAfterUse = true` (Secured through ending) |

---

## Phần 3 — Chuẩn hóa 21 Files (`FILE_01`–`FILE_21`) kèm hộp `KEY DATA`

> Quy ước hiển thị:
> - **KEY DATA (`summary`):** 2–3 dòng siêu gọn đặt ở đầu trang Nhật ký và trong panel tóm tắt nhanh trên HUD. Các mã/số liệu cần đối chiếu được **in đậm** (khi dựng UI Bước 2–3 sẽ tự động tô màu xanh/vàng theo rule theme).
> - **BODY (`body`):** Toàn văn giữ nguyên giọng văn canon (quy trình kỹ thuật, biên bản, thư tay, email, băng ghi âm), súc tích, không bịa chi tiết ngoài canon.

### 3.1. Nhóm A — 9 Hồ sơ nhiệm vụ bắt buộc (`FILE_01`–`FILE_09`)

#### `FILE_01` · `FactoryProcedure` · FACTORY · REQUIRED
- **Title:** `Factory Utilities Procedure`
- **Linked Objective:** `FactoryIsolate` / `FactoryBackup` / `FactoryGenerator`
- **KEY DATA (`summary`):**
  - `1. West Breaker:` **ISOLATE PRODUCTION**
  - `2. East Breaker:` **BACKUP COLD STORE ON** (keep case refrigerated)
  - `3. Generator:` **CHECK PHYSICAL LEVER** (green alarm lamp lights early)
- **BODY (`body`):**
  > Night operator — Maya Reed. Isolate the **PRODUCTION** branch at the west breaker before touching the backup feed. Then engage **BACKUP COLD STORE** at the east breaker and start the generator. Do not isolate the cold store itself: its monitor and sealed cargo must remain refrigerated. The green generator lamp shares an alarm return and can light before the contactor closes; check the physical lever. Logistics must reconcile the shipment and seal independently. Keep the **T9-17** case sealed until both checks are complete. Report any instruction to bypass this sequence in the shift ledger.

#### `FILE_02` · `FactoryManifest` · FACTORY · REQUIRED
- **Title:** `Delivery Manifest (AL-04)`
- **Linked Objective:** `FactoryManifest` / `FactoryShipping`
- **KEY DATA (`summary`):**
  - `Shipment / Seal:` **AL-04 / K6** (Lot **T9-17**)
  - `Destination / Time:` **NORTHBRIDGE / 02:40** (Service Entrance, **Group C12**)
  - `Note:` Returned unopened to Factory cold storage after quarantine shutters closed
- **BODY (`body`):**
  > Dispatch copy. Shipment **AL-04**; security seal **K6**; lot **T9-17**. Destination: **Northbridge Care Institute**, service receiving entrance. Scheduled intake: **02:40**, group **C12**. Security release: Marcus Hale. The public cargo description is temperature-controlled medical stock. Do not replace this page with the contractor's abbreviated receipt: that copy omits the receiving group and service entrance. Added in blue ink: if receiving refuses the sealed case, retain it in factory cold storage and leave the delivery record attached. Neither the seal number nor the destination may be changed by telephone.

#### `FILE_03` · `FactoryB2Transfer` · FACTORY · REQUIRED *(Auto-discovered on `FactoryCase`)*
- **Title:** `B2 Transfer Addendum`
- **Linked Objective:** `FactoryCase` → leads to Chapter 2
- **KEY DATA (`summary`):**
  - `Authority:` **Dr. Adrian Cole**, **Level B2** beneath **Northbridge**
  - `Missing Records:` Patient-linked records stayed at **Northbridge** under **Group C12**
  - `Warning:` Contractor asked for sample-only return; retain full addendum
- **BODY (`body`):**
  > Route amendment to **AL-04**, seal **K6**. Receiving authority: **Dr. Adrian Cole**, technical level **B2** beneath Northbridge. The sample case was returned unopened to factory cold storage after the internal quarantine shutters closed. Its movement record remains valid and must travel with it. Patient-linked records stayed at the institute under group **C12**; they are not included in this case. B2 is absent from the public visitor directory. Any recovery team must retain this addendum with the original seal receipt. The contractor requested a sample-only return. Security has not authorized removal of the supporting records.

#### `FILE_04` · `AsylumDeskLog` · ASYLUM · REQUIRED
- **Title:** `Northbridge Security Desk Log`
- **Linked Objective:** `AsylumAccess` / `AsylumFuse` / `AsylumPatient`
- **KEY DATA (`summary`):**
  - `Intake:` **AL-04 / K6** received at **02:40** for **Group C12** (Service Entrance)
  - `Next Target:` Upper Records Room — requires lower service power
  - `Spare Fuse:` **Ground-floor Maintenance Storage**
- **BODY (`body`):**
  > **02:40** — Received **AL-04** under seal **K6** for group **C12**. Public admissions are suspended; technical intake continues through the service entrance. Record the intake against the receiving time (**02:40**), not the earlier appointment time. The upper records room holds the intake sheet. Access requires lower service power; the replacement fuse is in ground-floor maintenance storage. No patient has been discharged through this desk tonight. Staff asking for the public lift must be redirected to security. Marcus Hale instructed us to keep both the public log and technical supplement. The contractor asked for only the public log.

#### `FILE_05` · `AsylumPatientRecord` · ASYLUM · REQUIRED
- **Title:** `P046 Intake Record`
- **Linked Objective:** `AsylumPatient`
- **KEY DATA (`summary`):**
  - `Match Target:` **P046 / C12 / 02:40** (Lot **T9-17**, Shipment **AL-04**)
  - `Public vs Internal:` Public says *Recovery Transfer*; Internal says **B2 Technical Observation**
  - `Ignore:` P041 / C08 / 01:20
- **BODY (`body`):**
  > Patient reference **P046**. Intake group **C12**. Receiving time **02:40**. Linked consignment **AL-04**, lot **T9-17**. Observation status: fever and disorientation following the T-9 exposure protocol. **Dr. Adrian Cole** has requested immediate suspension of further exposure; approval remains unsigned. The public status reads *RECOVERY TRANSFER*. The internal destination reads **B2 TECHNICAL OBSERVATION**. Do not substitute P041, whose C08 intake was logged at 01:20. Family contact requests are held by administration. This sheet must accompany any movement order so the receiving team can reconcile the patient reference with the original identity register.

#### `FILE_06` · `AsylumMortuaryTransfer` · ASYLUM · REQUIRED
- **Title:** `Mortuary Transfer Discrepancy`
- **Linked Objective:** `AsylumTransfer`
- **KEY DATA (`summary`):**
  - `Mortuary Entry:` **P046 / C12** logged **DECEASED at 03:05** (drawer empty)
  - `Freight Entry:` Same wristband logged **ALIVE TRANSFER to B2 at 03:50**
  - `Attachment:` **B2 Lift Access Card** retained with this report
- **BODY (`body`):**
  > **P046 / C12**. Mortuary entry: **deceased, 03:05**. Freight movement entry: **living subject for B2 observation, 03:50**. Both entries use the same patient reference and receiving wristband; this is not a second patient. The assigned mortuary drawer is empty. Maya Reed's lift note confirms the later trip carried an occupied observation trolley. The **B2 access card** is retained with this discrepancy report for an independent check. Do not resolve the conflict by deleting either entry. Whoever examines the records must see that a live transfer was logged after the reported death. — Security review copy.

#### `FILE_07` · `LabIndex` · LABORATORY · REQUIRED
- **Title:** `B2 Archive Index`
- **Linked Objective:** `LabIndex` / `LabArchive`
- **KEY DATA (`summary`):**
  - `Target Archive:` **E-02** (links **T9-17 / C12 / P046**)
  - `Ignore:` **E-01** (earlier lot) & **E-03** (equipment only)
  - `Requirement:` Power **DATA** circuit before opening archive terminal
- **BODY (`body`):**
  > Restricted index, revision before contractor export. Trace lot **T9-17** to intake group **C12** and patient **P046**. The corresponding original archive is **E-02**. E-01 belongs to an earlier shipment and cannot establish this chain. E-03 is an equipment record without patient identities. The contractor export of E-02 excludes the identity table and **Adrian Cole's** stop-work request. Preserve the original directory, not just its export. Power the **DATA** path before opening the archive terminal. Security has placed the original under hold so an outside investigator can compare what was recorded with what the client intended to receive.

#### `FILE_08` · `LabPowerRecord` · LABORATORY · REQUIRED
- **Title:** `Emergency Power Allocation`
- **Linked Objective:** `LabPower`
- **KEY DATA (`summary`):**
  - `Max Capacity:` **6 Units** (`SAFETY 2` · `TEST RIG 4` · `DATA 2` · `FREIGHT LIFT 2`)
  - `Required ON:` **SAFETY (2) + DATA (2) + FREIGHT LIFT (2) = 6**
  - `Required OFF:` **TEST RIG (4)**
- **BODY (`body`):**
  > Maya Reed — B2 maintenance. Available emergency capacity: **six units**. **SAFETY** uses two, **TEST RIG** uses four, **DATA** uses two, and **FREIGHT LIFT** uses two. **SAFETY** must remain online throughout any switch. For records recovery, turn **TEST RIG OFF**, then enable **DATA** and **FREIGHT LIFT**. The resulting load is **six**; no reserve remains for experiments. The test indicator is not a safety interlock. An overload refuses the selection without damaging the archive. If interrupted, return to the panel and check its actual switches. Keep the escape lift powered until the last occupied trip is clear.

#### `FILE_09` · `LabOriginalArchive` · LABORATORY · REQUIRED *(Auto-discovered on `LabArchive`)*
- **Title:** `E-02 Original Incident Archive`
- **Linked Objective:** `LabArchive` → `LabCase` / `LabTransmit`
- **KEY DATA (`summary`):**
  - `Verified Chain:` **E-02 / T9-17 / C12 / P046** (unredacted names + Cole's halt order)
  - `Confirmed Dead:` **Dr. Adrian Cole**, **Marcus Hale**, **Maya Reed**
  - `Order:` Transmit **COMPLETE** archive to BRT; do not strip patient identities
- **BODY (`body`):**
  > Original **E-02**: lot **T9-17**, group **C12**, patient **P046**. The identity table, movement history, and stop-work request are retained without redaction. **Adrian Cole** suspended the trial before containment failed. The later sample-only export was prepared under the client's instruction, not medical authority. Final incident ledger confirms the deaths of **Dr. Adrian Cole**, security chief **Marcus Hale**, and technician **Maya Reed** during the breach; their signed records remain attached. Patient outcomes beyond the documented transfers are unresolved. Copy this complete archive to independent custody. Removing the identity fields would erase the link between the material and the people exposed.

---

### 3.2. Nhóm B — 12 Tài liệu hiện trường tùy chọn (`FILE_10`–`FILE_21`)

#### `FILE_10` · `OptionalFactory01` · FACTORY · OPTIONAL
- **Title:** `Unsent Factory Letter`
- **KEY DATA (`summary`):**
  - `Author:` Night-shift factory worker writing home to Ellie & Ben
  - `Detail:` After-hours sealed deliveries handled separately by security & an outside caller
- **BODY (`body`):**
  > Ellie, I traded shifts again. Please do not wait at the bus stop tonight. They keep adding a sealed delivery after the ordinary trucks leave, and somebody has to stay with the temperature recorder until security signs. I used to know every driver by name. Now they speak to a caller before they speak to us. There is soup in the blue container, not the one with the cracked lid. Tell Ben I have not forgotten his Saturday match. I will come home when they let the last truck through.

#### `FILE_11` · `OptionalFactory02` · FACTORY · OPTIONAL
- **Title:** `Night Shift Handover`
- **KEY DATA (`summary`):**
  - `Author:` Factory shift crew handover note
  - `Detail:` **Maya Reed** left the proper electrical procedure beside the generator
- **BODY (`body`):**
  > To the morning crew: the kettle trips the socket behind the time clock. Use the canteen socket until maintenance replaces it. Do not stack empty pallets against the service door, even if dispatch says the corridor is unused. We had to move all of them during yesterday's alarm. **Maya** left a proper electrical procedure beside the generator; read it instead of copying the last operator's switch positions. The spare locker keys are with security. If nobody relieves you, write your departure time honestly. We are not signing another complete shift that nobody actually worked.

#### `FILE_12` · `OptionalFactory03` · FACTORY · OPTIONAL
- **Title:** `Cold Store Return Memo`
- **KEY DATA (`summary`):**
  - `Author:` **Marcus Hale** (Security Desk)
  - `Detail:` An outside caller asked to destroy the temperature strip on returned **T9-17**; Hale refused
- **BODY (`body`):**
  > Security desk — **Marcus Hale**. The returned **T9-17** case remains sealed. Photograph the seal before storage and attach the failed delivery receipt; do not classify it as unused stock. A caller asked whether destroying the temperature strip would simplify the return. I declined and recorded the call reference. Refrigeration is a custody requirement, not proof that the contents are safe. No factory employee is authorized to inspect the sample. If the client sends a replacement manifest, keep both versions. A cleaner document is not necessarily a more truthful one.

#### `FILE_13` · `OptionalAsylum01` · ASYLUM · OPTIONAL
- **Title:** `Ward Notice: Visiting Hours`
- **KEY DATA (`summary`):**
  - `Source:` Northbridge Ward Administration
  - `Detail:` Visiting suspended; *Technical Observation* transfers caused reception record errors
- **BODY (`body`):**
  > Visiting hours are suspended until further notice. Please leave letters with reception; staff will record the recipient and delivery time. Do not tell families that a patient has been discharged unless a named clinician has signed the discharge sheet. Administrative transfer is not discharge. The new *technical observation* stamps have caused repeated mistakes at this desk. If an outside caller asks for a room that no longer appears on the ward list, retain the request for review. Families should not have to provide the same name every time someone moves a folder.

#### `FILE_14` · `OptionalAsylum02` · ASYLUM · OPTIONAL
- **Title:** `A Letter Never Delivered`
- **KEY DATA (`summary`):**
  - `Author:` Anna (family member of a transferred patient)
  - `Detail:` Reception reassigned the patient's room and withheld personal belongings
- **BODY (`body`):**
  > Dear Daniel, the receptionist said your room changed and that visiting would resume next week. I left the photograph anyway. Your brother insists the dog recognizes you in it, which sounds impossible, but I am choosing to believe him. They would not let me leave your own blanket because everything has to be issued by the ward. I have written our number again on the back. You do not need to explain why you missed the call. Just ask someone to tell us which room you are in now. Love, Anna.

#### `FILE_15` · `OptionalAsylum03` · ASYLUM · OPTIONAL
- **Title:** `B2 Lift Maintenance Card`
- **KEY DATA (`summary`):**
  - `Author:` **Maya Reed** (Maintenance)
  - `Detail:` B2 lift stays on the emergency circuit; keycard held by security
- **BODY (`body`):**
  > Service entry — **Maya Reed**. The freight lift answered a lower-floor call after the public closure notice went up. That is not a fault: **B2** remains connected to the emergency circuit. I replaced the landing lamp, not the card reader. Stop requesting a master bypass; the card is held by security for a reason. On descent, wait for the receiving door to finish opening before unloading. The cabin cannot safely carry a trolley across a half-open threshold. Leave this card at the panel so the next shift knows what was actually repaired.

#### `FILE_16` · `OptionalAsylum04` · ASYLUM · OPTIONAL
- **Title:** `Night Nurse Audio Transcript`
- **KEY DATA (`summary`):**
  - `Source:` Recorded ward handover
  - `Detail:` **P046** was breathing inside the B2 lift even though the upstairs screen said *Deceased*
- **BODY (`body`):**
  > Recorded ward handover, transcript complete. I checked the trolley wristband twice. **P046** was breathing when the doors closed. The screen upstairs had already changed to *deceased*. I asked whether we were taking the patient to emergency care, and security told me the receiving team would explain. Nobody did. I am leaving this recording with the paper handover because the electronic entry no longer accepts corrections from our ward. If you hear this on the next shift, preserve both times. Please do not sign my name under a report I did not write.

#### `FILE_17` · `OptionalLab01` · LABORATORY · OPTIONAL
- **Title:** `Operator Shutdown Checklist`
- **KEY DATA (`summary`):**
  - `Source:` B2 Operations Desk (annotated by **Dr. Adrian Cole**)
  - `Detail:` Client export tool hides unresolved patient records automatically
- **BODY (`body`):**
  > Before leaving the operations desk, record the active run, preserve its index, and place any unsigned stop-work request with the original archive. Do not mark the trial complete merely because the sample has moved. Completion requires an authorized review of the incident record. **Dr. Cole** has returned this checklist twice with the same annotation: *unresolved patient outcomes are not empty fields.* The client export tool offers a clean summary automatically. That summary is a delivery convenience only. It must never replace the underlying record or become the only copy available to the next operator.

#### `FILE_18` · `OptionalLab02` · LABORATORY · OPTIONAL
- **Title:** `Contractor Export Email`
- **KEY DATA (`summary`):**
  - `Thread:` Contract Liaison ↔ **Dr. Adrian Cole**
  - `Detail:` Client explicitly demanded stripping patient names and internal objections
- **BODY (`body`):**
  > From: Contract Liaison. Subject: delivery scope. The receiving client expects material identifiers, storage condition, and a confirmation of sample custody. Patient names, ward correspondence, and internal objections fall outside the requested export. Please use the abbreviated profile already installed on the archive terminal.
  > Reply from **Adrian Cole**: those fields establish what happened here. Omitting them changes the meaning of the result. I will not certify a sample-only record as the original.
  > Forwarded response: certification can be resolved after collection. Keep this discussion off the public incident channel until our client has reviewed the package.

#### `FILE_19` · `OptionalLab03` · LABORATORY · OPTIONAL
- **Title:** `Calibration Bench Note`
- **KEY DATA (`summary`):**
  - `Author:` B2 Instrument Technician
  - `Detail:` Faulty indicator display & altered reporting template hide trial interruptions
- **BODY (`body`):**
  > Bench note — do not discard with consumables. The replacement display arrives with its brightness set to maximum, which makes an unlit warning look active through the scratched cover. Check the diagnostic page before logging a fault. This is a display problem, not evidence that containment is operating. I have tagged the affected unit and attached yesterday's reading so nobody has to reconstruct the comparison from memory. If the archive later shows a perfect sequence with no interruptions, compare it with the paper sheet. The instruments did not become more reliable when the reporting template changed.

#### `FILE_20` · `OptionalLab04` · LABORATORY · OPTIONAL
- **Title:** `Emergency Drill Review`
- **KEY DATA (`summary`):**
  - `Author:` **Marcus Hale** (Security Copy)
  - `Detail:` Confirms why **BRT** requires off-site receipt confirmation (`D21`) instead of trusting a local progress bar
- **BODY (`body`):**
  > Drill review, security copy. Staff reached the freight landing, but two teams waited for contradictory clearance messages while the lift stood open. A local alarm did not reach the outside coordinator. Corrective action: confirm receipt beyond the facility before describing a transmission as delivered. A progress bar is not an acknowledgement. **Marcus Hale** requested an independent receiving contact and a printed boarding roster. The client declined the additional reporting step as unnecessary delay. This review remains open. Do not close it simply because the next drill has been removed from the calendar.

#### `FILE_21` · `OptionalLab05` · LABORATORY · OPTIONAL
- **Title:** `Maya's Final Shift Recording`
- **KEY DATA (`summary`):**
  - `Author:` **Maya Reed** (Recovered personal recorder)
  - `Detail:` Quarantine shutters locked B2 while Cole fought to halt the trial and Hale protected the full directory
- **BODY (`body`):**
  > Personal recorder, final intact segment. This is **Maya Reed**. I have left the power note at the panel and the original maintenance sheets with the archive. **Cole** is still trying to stop the trial. **Hale** has ordered security to preserve the records, even if the client calls again. I can hear the shutters closing behind operations. If somebody reaches this desk later, do not trust the small export marked complete. The full directory is still here. Take the names with the numbers. We worked beside these people; they were never just a shipment. End of recovered segment.

---

### 3.3. Chuẩn hóa UI Text cho `Mission Journal & Evidence` (3 Tab, Nhóm File, Trạng thái Key Items)

Tách hoàn toàn màn hình Nhật ký khỏi Túi đồ chiến đấu (`Squad Inventory` cũ), gán phím mở riêng `[J]` (vẫn có thể bấm nút từ HUD), toàn bộ text hiển thị chuẩn hóa **100% Tiếng Anh**:

#### A. Tiêu đề chính & 3 Tab điều hướng

| Thành phần UI | Chuỗi chuẩn (EN) | Mục đích & nội dung hiển thị |
|---|---|---|
| **Screen Title** | `MISSION JOURNAL & EVIDENCE` | Thay cho `Squad Inventory` |
| **Open / Close Key Hint** | `JOURNAL [J]` · `CLOSE [J / ESC]` | Tách biệt rõ khỏi hotbar trang bị sinh tồn |
| **Tab 1** | `MISSIONS` | Tiến độ chương hiện tại (`Chapter Goal` + bước đang thực hiện) và `EVIDENCE CHAIN` (tóm tắt các mắt xích bằng chứng đã khớp) |
| **Tab 2** | `FILES` | Tách rõ 2 nhóm tài liệu: `MISSION EVIDENCE (9)` và `FIELD RECORDS (12)` + `CAMPAIGN RECORD` (lịch sử truyền tin radio) |
| **Tab 3** | `KEY ITEMS` | 4 vật chứng & khóa dùng chung toàn đội kèm badge trạng thái |

#### B. Chi tiết hiển thị & Trạng thái từng Tab

1. **Tab `MISSIONS`:**
   - **Header mục tiêu chương:** Dùng đúng `Goal (Why)` ở Phần 1.2 (ví dụ: `FACTORY — RECOVER THE T9-17 CASE`).
   - **Danh sách bước (`OBJECTIVES`):** Trạng thái từng bước (`DONE` / `ACTIVE` / `LOCKED: Requires ...`), đồng bộ 100% với Tracker góc trái.
   - **Khối tóm tắt chuỗi bằng chứng (`EVIDENCE CHAIN`):** Tự động cập nhật theo các dữ kiện đã xác minh (ví dụ: `AL-04 / K6 → Northbridge (02:40, C12) → P046 (03:05 Dead / 03:50 Live to B2) → E-02 Full Archive`).
   - **Empty state:** `NO MISSIONS AVAILABLE` / `Mission objectives appear when the squad enters a chapter.`

2. **Tab `FILES`:**
   - **Nhóm 1 — `MISSION EVIDENCE ({found}/9)`:**
     - Gắn icon mục tiêu / khóa (`Objective / Lock icon`), dùng badge **`NEW`** (chưa đọc) hoặc **`EVIDENCE`** (đã đọc).
     - Đầu trang đọc luôn hiển thị hộp nổi bật **`KEY DATA`** (in đậm/tô xanh dương các mã đối chiếu từ `summary`), phía dưới ngăn cách bằng tiêu đề **`FULL RECORD`** chứa toàn văn (`body`).
   - **Nhóm 2 — `FIELD RECORDS ({found}/12)`:**
     - Dùng cho 12 tài liệu tùy chọn (`FILE_10`–`FILE_21`), dùng badge **`NEW`** hoặc **`OPTIONAL`**, giữ cấu trúc **`KEY DATA`** + **`FULL RECORD`** để đọc lướt nhanh.
   - **Mục cuối danh sách — `CAMPAIGN RECORD` (`RECORDED`):** Lưu các dòng truyền tin radio đã phát để tra cứu lại nếu cần.
   - **Footer notice (khi mở panel giải đố đối chiếu):** Tab tham chiếu nhanh hiển thị sẵn **`KEY DATA`** của file nguồn liên quan ngay cạnh lựa chọn đáp án.

3. **Tab `KEY ITEMS`:**
   - Hiển thị 4 vật phẩm dùng chung toàn đội theo thứ tự cố định:
     - `T9-17 Factory Evidence Case` — trạng thái: **`SECURED`** (giữ tới hết chiến dịch)
     - `Replacement Service Fuse` — trạng thái: **`OWNED`** → **`USED`** (sau khi lắp tại bảng điện)
     - `B2 Freight Lift Keycard` — trạng thái: **`OWNED`** → **`USED`** (sau khi quẹt thẻ gọi thang B2)
     - `E-02 Laboratory Evidence Case` — trạng thái: **`SECURED`** (giữ tới hết chiến dịch)
   - **Empty state:** `NO KEY ITEMS ACQUIRED` / `Shared squad evidence cases and access items appear here.`

#### C. Quy ước UX đọc nhanh & Pause (áp dụng ở Bước 3)

- **Chế độ Solo (1 người chơi):** Tự động **Pause (`Time.timeScale = 0`)** khi mở màn hình `MISSION JOURNAL & EVIDENCE` toàn màn hình hoặc popup đọc tài liệu mới nhặt để người chơi yên tâm đọc không bị zombie đánh lén.
- **Chế độ Co-op (2–4 người chơi) hoặc vừa đánh vừa xem:** Không thể đóng băng server time nên sử dụng **Panel tóm tắt `KEY DATA` thu gọn** gắn ở góc/trên HUD (bật/tắt nhanh hoặc tự hiển thị khi tới gần bảng giải đố) kèm đánh dấu màu xanh dương/vàng cho các mã cần nhập.

---

## Phần 4 — Chuẩn hóa 28 đoạn Radio (`D01`–`D28`) + Bản một đặc vụ (`soloCues`)

> **Quy tắc trình bày & phụ đề:**
> - Toàn bộ lời thoại dùng **100% Tiếng Anh**, mỗi cue tối đa 2 dòng (`<br>` là ngắt dòng), **mỗi dòng ≤ 40 ký tự** tính cả tiền tố người nói ở dòng đầu (ví dụ `"Command: "` = 9 ký tự, `"Client: "` = 8 ký tự, `"Mason: "`/`"Raven: "` = 7 ký tự).
> - Đã chỉnh lại các đoạn từng vượt 40 ký tự ở bản 48 (`D06`, `D07`, `D14` và `soloCues` của `D08`, `D12`, `D25`), đồng thời lồng rõ **hướng dẫn bước tiếp theo** và **luật Tank từng chương** cho người chơi mới.

### 4.1. Bảng chỉ mục nhanh `D01`–`D28`

| ID | Enum | Priority | Once / Cooldown | Trigger | First / Second |
|---|---|---|---|---|---|
| `D01` | `InsertionBriefing` | P2 | Once | Insertion start | `Mason` / `Command` |
| `D02` | `FactoryChapter` | P2 | Once | Factory `Exploring` | `Vega` / `Command` |
| `D03` | `AsylumChapter` | P2 | Once | Asylum `Exploring` | `Command` / `Raven` |
| `D04` | `LaboratoryChapter` | P2 | Once | Lab `Exploring` | `Vega` / `Command` |
| `D05` | `FactoryGenerator` | P2 | Once | `FactoryGenerator` done | `Vega` / `Raven` |
| `D06` | `FactoryShipping` | P2 | Once | `FactoryShipping` done | `Raven` / `Command` |
| `D07` | `FactoryCase` | P2 | Once | `FactoryCase` done | `Mason` / `Command` |
| `D08` | `AsylumAccess` | P2 | Once | `AsylumAccess` done | `Raven` / `Vega` |
| `D09` | `AsylumPower` | P2 | Once | `AsylumPower` done | `Vega` / `Raven` |
| `D10` | `AsylumPatient` | P2 | Once | `AsylumPatient` done | `Doc` / `Raven` |
| `D11` | `AsylumTransfer` | P2 | Once | `AsylumTransfer` done | `Doc` / `Command` |
| `D12` | `LabPower` | P2 | Once | `LabPower` done | `Vega` / `Raven` |
| `D13` | `LabArchive` | P2 | Once | `LabArchive` done | `Raven` / `Doc` |
| `D14` | `LabCase` | P2 | Once | `LabCase` done | `Mason` / `Command` |
| `D15` | `FactoryEncounter` | P2 | Once | Factory `Encounter` | `Mason` / `Vega` |
| `D16` | `AsylumEncounter` | P2 | Once | Asylum `Encounter` | `Mason` / `Command` |
| `D17` | `LabEncounter` | P2 | Once | Lab `Encounter` | `Vega` / `Command` |
| `D18` | `Boarding` | P2 | 30s | `AwaitingParty` | `Command` / `Mason` |
| `D19` | `AsylumTransition` | P2 | Once | Asylum `Transitioning` | `Mason` / `Doc` |
| `D20` | `Completed` | P2 | Once | `Completed` | `Mason` / `Command` |
| `D21` | `EvidenceTransmitted` | P2 | Once | Lab `evidenceTransmitted` | `Command` / `Raven` |
| `D22` | `ClientSuppressionOrder` | P2 | Once | Lab `LabTransmit` start | `Client` / `Raven` |
| `D23` | `CommandRejectsOrder` | P2 | Once | Immediately after `D22` | `Command` / `Raven` |
| `D24` | `OperatorDowned` | P1 | 15s | Teammate downed | `First (subject)` / `Second` |
| `D25` | `TankWarning` | P1 | 20s | Tank spawned | `Vega` / `Mason` |
| `D26` | `ScreamerWarning` | P1 | 20s | Screamer spawned | `Doc` / `Raven` |
| `D27` | `InfectorWarning` | P1 | 20s | Infector spawned | `Raven` / `Vega` |
| `D28` | `Revived` | P1 | 15s | Teammate revived | `First (subject)` / `Command` |

---

### 4.2. Chương 1 — Factory (`D01`, `D02`, `D05`, `D06`, `D07`, `D15`)

#### `D01` · `InsertionBriefing` · P2 · *(Insertion — Xác lập nhiệm vụ BRT & T-9)*
1. **First (`Mason`):** `Command, Vanguard is inside.`<br>`Quarantine shutters are sealed.`
2. **Command:** `Copy. T-9 came from lot T9-17.`<br>`Pull that case from cold storage.`
3. **Command:** `Keep the security seal intact.`
4. **First (`Mason`):** `What about the manifest?`<br>`Bring it or read the numbers?`
5. **Command:** `Bring it with the case.`<br>`We need where T9-17 went next.`
6. **First (`Mason`):** `Roger. Both items, seal intact.`<br>`Vanguard out.`

#### `D02` · `FactoryChapter` · P2 · *(Exploring — Định hướng 2 nhánh Power & Logistics)*
1. **First (`Vega`):** `Cold storage is dead.`<br>`Grab the case or fix power first?`
2. **Command:** `Fix power first.`<br>`Keep T9-17 refrigerated.`
3. **Command:** `Check the manifest in parallel.`<br>`Match its seal at the terminal.`
4. **First (`Vega`):** `Copy. Reading the breaker manual.`

#### `D05` · `FactoryGenerator` · P2 · *(Sau khi bật máy phát)*
1. **First (`Vega`):** `Generator's up.`<br>`Cold storage has power.`
2. **Second (`Raven`):** `That green lamp was lit early.`<br>`It wasn't powered then?`
3. **First (`Vega`):** `Shares the alarm return.`<br>`Lit before I threw the switch.`
4. **Second (`Raven`):** `Got it. Make sure Logistics`<br>`checks out so we can grab T9-17.`

#### `D06` · `FactoryShipping` · P2 · *(Sau khi đối chiếu vận đơn)*
1. **First (`Raven`):** `Command, manifest matches.`<br>`Destination is Northbridge.`
2. **Command:** `Did anyone sign for it there?`
3. **First (`Raven`):** `No. Just intake time 02:40.`<br>`Only dispatch signed off.`
4. **Command:** `Copy. Keep that manifest.`<br>`Kill any Tank blocking the vault.`

#### `D07` · `FactoryCase` · P2 · *(Sau khi hạ Tank & thu hồi T9-17)*
1. **First (`Mason`):** `Command, we've got T9-17.`<br>`Tank is down, seal intact.`
2. **Command:** `Copy. Why was it returned?`
3. **First (`Mason`):** `Addendum says shutters closed.`<br>`Patient files stayed at Northbridge.`
4. **Command:** `Keep the addendum attached.`<br>`Open the service exit now.`

#### `D15` · `FactoryEncounter` · P2 · *(Cửa kỹ thuật đang nhả khóa)*
1. **First (`Mason`):** `I hit the service release.`<br>`Door's not budging.`
2. **Second (`Vega`):** `Interlock is still cycling.`<br>`Hold the control room.`
3. **First (`Mason`):** `Copy. Protecting the case.`<br>`Call me when it clears.`

---

### 4.3. Chương 2 — Northbridge Care Institute (`D03`, `D08`, `D09`, `D10`, `D11`, `D16`, `D19`)

#### `D03` · `AsylumChapter` · P2 · *(Exploring — Vào viện Northbridge)*
1. **Command:** `Vanguard, AL-04 arrived here.`<br>`Find who it was delivered for.`
2. **First (`Raven`):** `Manifest only lists Group C12.`<br>`Where do we find a name?`
3. **Command:** `Start at the security desk.`<br>`Match 02:40 to their intake log.`
4. **First (`Raven`):** `Copy. Heading to security.`

#### `D08` · `AsylumAccess` · P2 · *(Sau khi đọc sổ bàn bảo vệ)*
1. **First (`Raven`):** `Public admissions stopped.`<br>`Yet a 02:40 intake slipped in.`
2. **Second (`Vega`):** `They reopened the main gate?`
3. **First (`Raven`):** `No. Service entrance, Group C12.`<br>`Intake sheets are upstairs.`
4. **Second (`Vega`):** `Records needs lower power.`<br>`Log says spare fuse is nearby.`
5. **First (`Raven`):** `Grab it from maintenance.`<br>`I'll keep the desk log open.`

#### `D09` · `AsylumPower` · P2 · *(Sau khi bật điện tầng dưới)*
1. **First (`Vega`):** `Records room has power.`<br>`Heading upstairs for Group C12.`
2. **Second (`Raven`):** `Watch the intake time.`<br>`Could be multiple sheets.`
3. **First (`Vega`):** `Zero two forty, Group C12.`
4. **Second (`Raven`):** `Right. Match both fields.`

#### `D10` · `AsylumPatient` · P2 · *(Sau khi xác nhận hồ sơ P046)*
1. **First (`Doc`):** `It's P046. Public sheet`<br>`reads "recovery transfer."`
2. **Second (`Raven`):** `Transferred where?`
3. **First (`Doc`):** `Internal note says Level B2.`<br>`Technical observation.`
4. **Second (`Raven`):** `Did they move the patient?`
5. **First (`Doc`):** `Checking the mortuary log next.`

#### `D11` · `AsylumTransfer` · P2 · *(Sau khi chỉ ra mâu thuẫn 03:05 vs 03:50)*
1. **First (`Doc`):** `Command, mortuary logged`<br>`P046 dead at 03:05.`
2. **Command:** `Then who went down to B2?`
3. **First (`Doc`):** `Same wristband, same P046.`<br>`Transferred alive at 03:50.`
4. **Command:** `Keep both records and the card.`<br>`Call the B2 freight lift now.`

#### `D16` · `AsylumEncounter` · P2 · *(Đang gọi thang B2 — dạy luật né Tank)*
1. **First (`Mason`):** `Command, B2 lift is called.`<br>`Squad's holding at the doors.`
2. **Command:** `Copy. Priority is reaching B2.`
3. **First (`Mason`):** `What if a Tank blocks us here?`
4. **Command:** `Don't waste ammo fighting it.`<br>`Evade the Tank, board the lift.`
5. **First (`Mason`):** `Roger. Waiting for the doors.`

#### `D19` · `AsylumTransition` · P2 · *(Đang xuống thang B2)*
1. **First (`Mason`):** `Doc, what do we check`<br>`first once we reach B2?`
2. **Second (`Doc`):** `The B2 transfer console.`<br>`See where they routed P046.`
3. **First (`Mason`):** `Fair. Wait till doors clear.`

---

### 4.4. Chương 3 — Laboratory B2 (`D04`, `D12`, `D13`, `D14`, `D22`, `D23`, `D17`, `D21`, `D20`)

#### `D04` · `LaboratoryChapter` · P2 · *(Exploring — Xuống tầng B2 ẩn)*
1. **First (`Vega`):** `Command, Vanguard is on B2.`<br>`Any blueprint on your map?`
2. **Command:** `Negative. B2 is unlisted.`<br>`Check the receiving terminal.`
3. **First (`Vega`):** `Copy. Tracing the B2 transfer`<br>`to find their archive index.`

#### `D12` · `LabPower` · P2 · *(Sau khi phân bổ nguồn 6 đơn vị)*
1. **First (`Vega`):** `Data path is powered.`<br>`Raven, is the terminal up?`
2. **Second (`Raven`):** `It's up. Thought we'd have`<br>`to cut lift power for this.`
3. **First (`Vega`):** `Lift is still live.`<br>`I killed the test rig instead.`
4. **Second (`Raven`):** `Good. Pulling up P046 now.`

#### `D13` · `LabArchive` · P2 · *(Sau khi xác minh bản gốc E-02)*
1. **First (`Raven`):** `Found original E-02.`<br>`Names and P046 match up.`
2. **Second (`Doc`):** `Anything from Dr. Cole?`
3. **First (`Raven`):** `His halt order is still here.`<br>`Client export scrubbed both out.`
4. **Second (`Doc`):** `Grab the physical E-02 case.`<br>`Don't read names on open comms.`
5. **First (`Raven`):** `Copy. Securing E-02 case now.`

#### `D14` · `LabCase` · P2 · *(Sau khi thu hồi E-02 case)*
1. **First (`Mason`):** `Command, we have both cases.`<br>`Request clearance to extract.`
2. **Command:** `Negative. Transmit full E-02`<br>`off-site before you leave.`
3. **First (`Mason`):** `Who's receiving the upload?`
4. **Command:** `BRT's off-site station.`<br>`Start the upload at the console.`

#### `D22` · `ClientSuppressionOrder` · P2 · *(Bên thuê xen vào khi bấm truyền)*
1. **Client:** `Vanguard, the export is`<br>`for sample data only.`
2. **Client:** `Strip out patient identities`<br>`and all staff objections.`
3. **First (`Raven`):** `Wipe the names and how do`<br>`you trace who was exposed?`
4. **Client:** `We track by lot number only.`<br>`Leave original E-02 on site.`

#### `D23` · `CommandRejectsOrder` · P2 · *(Bộ chỉ huy bác bỏ ngay lập tức)*
1. **Command:** `Vanguard, ignore that order.`<br>`Send the full E-02 copy to BRT.`
2. **Client:** `We refuse receipt if names`<br>`remain in the export.`
3. **Command:** `BRT will receive it directly.`<br>`I'm authorizing the upload.`
4. **First (`Raven`):** `Confirmed. Names and Cole's`<br>`objections stay in. Sending now.`

#### `D17` · `LabEncounter` · P2 · *(Đang truyền tin — dạy luật né Tank)*
1. **First (`Vega`):** `Upload's running.`<br>`Do I need to guard the console?`
2. **Command:** `No, it transmits on its own.`<br>`Stay mobile. Evade any Tank.`
3. **First (`Vega`):** `Copy. Ping us when BRT confirms.`

#### `D21` · `EvidenceTransmitted` · P2 · *(Trạm ngoài xác nhận sau 55s)*
1. **Command:** `Vanguard, BRT station has`<br>`the full E-02 archive.`
2. **First (`Raven`):** `Copy. What about both cases?`
3. **Command:** `Bring both cases to the lift`<br>`and hold for extraction.`
4. **First (`Raven`):** `Roger. Moving to the lift now.`

#### `D20` · `Completed` · P2 · *(Hoàn tất chiến dịch)*
1. **First (`Mason`):** `Command, Vanguard is clear.`<br>`Both cases accounted for.`
2. **Command:** `Roger. BRT holds the archive.`<br>`Mark P046 status unknown.`
3. **Command:** `Quarantine stays in effect.`<br>`Investigators take it from here.`
4. **First (`Mason`):** `Copy. Briefing on debrief.`
5. **Command:** `Command out.`

---

### 4.5. Cảnh dùng chung (`D18`, `D24`, `D25`, `D26`, `D27`, `D28`)

#### `D18` · `Boarding` · P2 · Cooldown 30s · *(AwaitingParty)*
1. **Command:** `Vanguard, exit's open.`<br>`Get the squad to the boarding zone.`
2. **First (`Mason`):** `Moving now. Calling once clear.`
3. **Command:** `Copy. Channel clear.`

#### `D24` · `OperatorDowned` · P1 · Cooldown 15s · *(Đồng đội gục)*
1. **First (Downed):** `Vanguard, I'm down!`<br>`Need cover and a revive!`
2. **Second (Helper):** `Copy! Moving to you now.`

#### `D28` · `Revived` · P1 · Cooldown 15s · *(Đã được cứu dậy)*
1. **First (Revived):** `Command, back on my feet.`
2. **Command:** `Copy. Can you keep moving?`
3. **First (Revived):** `Yeah. Regrouping with squad.`

#### `D25` · `TankWarning` · P1 · Cooldown 20s · *(Cảnh báo Tank)*
1. **First (`Vega`):** `Tank! Mason, give me room`<br>`to back out of this lane!`
2. **Second (`Mason`):** `Copy! Vanguard, clear space!`

#### `D26` · `ScreamerWarning` · P1 · Cooldown 20s · *(Cảnh báo Screamer)*
1. **First (`Doc`):** `Screamer! Hold the comms.`<br>`Let me locate its cry.`
2. **Second (`Raven`):** `Copy. Staying off air.`

#### `D27` · `InfectorWarning` · P1 · Cooldown 20s · *(Cảnh báo Infector)*
1. **First (`Raven`):** `Infector! Backing off!`<br>`Vega, keep your spacing!`
2. **Second (`Vega`):** `Copy. Giving you room.`

---

### 4.6. Bản một đặc vụ (`soloCues`)

> Dùng tự động khi `second == CampaignRadioVoice.None` (`CampaignDialogueDefinition.CuesFor`). Các mã `D01, D02, D03, D04, D06, D07, D11, D14, D16, D17, D18, D20, D21, D22, D23` đã giao tiếp trực tiếp với `Command` hoặc `Client` nên dùng chung `cues`. Với `D09`, `D10`, `D13` ở chế độ 1 người, `Command` đảm nhận lượt `Second`.

- **`D05` (`FactoryGenerator` · soloCues):**
  1. **First:** `Command, cold store has power.`
  2. **Command:** `Did the generator engage,`<br>`or just the green lamp?`
  3. **First:** `Lever engaged. Lamp lit early.`

- **`D15` (`FactoryEncounter` · soloCues):**
  1. **First:** `I hit the service release.`<br>`Door's not moving yet.`
  2. **Command:** `Interlock is still cycling.`<br>`Hold inside the control room.`
  3. **First:** `Copy. Holding with the case.`

- **`D08` (`AsylumAccess` · soloCues):**
  1. **First:** `Main admissions closed.`<br>`Service intake still logged C12.`
  2. **Command:** `Does it say where the intake`<br>`sheets are stored?`
  3. **First:** `Upstairs. Needs power.`<br>`Spare fuse is in maintenance.`
  4. **Command:** `Grab the fuse on this floor.`<br>`Keep the desk log recorded.`

- **`D19` (`AsylumTransition` · soloCues):**
  1. **First:** `Heading down to Level B2.`<br>`Checking transfer console first.`
  2. **Command:** `Good. Wait till lift doors`<br>`clear before stepping out.`
  3. **First:** `Copy. Calling in any P046 lead.`

- **`D12` (`LabPower` · soloCues):**
  1. **First:** `Data circuit is powered.`<br>`Opening the archive terminal.`
  2. **Command:** `Does the freight lift still`<br>`have emergency power?`
  3. **First:** `Both stay live. Test rig off.`

- **`D24` (`OperatorDowned` · soloCues):**
  1. **First:** `Command, I'm down!`<br>`Do you read me?`
  2. **Command:** `Loud and clear.`<br>`Stay on the line with me.`

- **`D28` (`Revived` · soloCues):**
  1. **First:** `Command, back on my feet.`
  2. **Command:** `Copy. Can you move on your own?`
  3. **First:** `Yeah. Pushing forward now.`

- **`D25` (`TankWarning` · soloCues):**
  1. **Command:** `Tank alert!`<br>`Keep an exit route open.`
  2. **First:** `Copy! Keeping my distance.`

- **`D26` (`ScreamerWarning` · soloCues):**
  1. **First:** `Screamer! Command, hold air`<br>`so I can track its sound.`
  2. **Command:** `Staying off the air.`<br>`Call if you spot it.`

- **`D27` (`InfectorWarning` · soloCues):**
  1. **Command:** `Infector alert!`<br>`Keep your distance.`
  2. **First:** `Copy. Backing off.`

---

## Phần 5 — Danh mục chuỗi Code/UI cần đồng bộ ở Bước 2–3 (Chưa chỉnh sửa ở Bước 1)

Khi bạn duyệt xong bản Bước 1 này, Bước 2 sẽ nạp nội dung trên vào `CampaignContentCatalog.asset` và đồng bộ các chuỗi còn lẫn Tiếng Việt / thiếu nhất quán trong mã nguồn:

| Vị trí hiện tại | Chuỗi cũ | Chuẩn mới (EN — Bước 2/3) |
|---|---|---|
| [CampaignInteractable.cs:68-71](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs#L68-L71) | `🔒 Cần: ...` và `[T] theo dõi` | `LOCKED: Requires {reason}` và `[T] Track objective` |
| [CampaignInteractionProxy.cs:19](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignInteractionProxy.cs#L19) | `"🔒 Cần: objective"` | `"LOCKED: Objective unavailable"` |
| [CampaignInteractable.cs:130](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs#L130) | `"SOURCE DOCUMENT"` | Hiển thị `Title` thật của File (`Delivery Manifest (AL-04)` / `Factory Utilities Procedure`...) |
| [CampaignCheckpoint.cs:110,119,126,155,166,170](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignCheckpoint.cs#L110-L170) | `"Checkpoint trống."`, `"Chưa có checkpoint đã lưu."`, v.v. | `"Checkpoint file is empty."`, `"No saved checkpoint found."`, `"Unsupported checkpoint version."`, `"Incompatible or incomplete checkpoint."`, `"Invalid player snapshot."`, `"Invalid supply ID."` |
| [CampaignTypes.cs:246](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs#L246) | `seconds >= 135 ? 3 : seconds >= 90 ? 2 : seconds >= 45 ? 1 : 0` | `seconds >= 75 ? 3 : seconds >= 45 ? 2 : seconds >= 25 ? 1 : 0` |
| [TeamInventoryUI.cs:193](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs#L193) | `title.text = "Squad Inventory";` | Bước 3: Tách `MISSION JOURNAL & EVIDENCE [J]` theo chuẩn **Phần 3.3** (3 tab `MISSIONS`, `FILES` tách 2 nhóm, `KEY ITEMS`) + panel tóm tắt `KEY DATA` & auto-pause ở Solo |
| `CampaignHUD` · `TeamInventoryUI` | Văn bản thuần (chỉ tô màu 2 chỗ `Goal` và `ROUTE UNAVAILABLE`) | Bước 2–3: Áp dụng `CampaignTextHighlight.Format(..., SelectedUiTheme.Current)` theo **Phần 1.4** lên HUD Tracker/Hints, Radio subtitle (`supportRichText = true`) và `KEY DATA`/`BODY` trong Nhật ký |

---

## Phần 6 — Bằng chứng thực thi & Giới hạn (Compliance)

- **Skill đã đọc trước khi soạn:**
  - `level-design`: cấu trúc critical path, gating rõ ràng, "teach then test" (dạy cơ chế Tank & giải đố qua radio + gợi ý 3 cấp).
  - `dialogue-systems`: tách dữ liệu thoại khỏi logic runner bằng ID ổn định (`CampaignDialogueId`), hỗ trợ `cues` và `soloCues`.
- **Nguồn xác minh:**
  - `[CODE CampaignTypes.cs:7-27, 125-191, 246-547]` — danh sách 20 `CampaignObjectiveId`, 21 `CampaignFileId`, 4 `CampaignKeyItemId`, 28 `CampaignDialogueId`, quy tắc `TryComplete` và `HintTier`.
  - `[CODE CampaignContentCatalog.cs:33-160]` — schema `CampaignFileDefinition`, `CampaignKeyItemDefinition`, `CampaignDialogueDefinition`, `CampaignObjectiveDefinition`.
  - `[CODE SelectedUiTheme.cs:12-15]` & `[CODE CampaignHUD.Navigation.cs:155, 211]` — 4 token màu chuẩn (`Text`, `Muted`, `Accent`, `Danger`) và cách dựng `<color=#...>` động từ `ColorUtility.ToHtmlStringRGB`.
  - `[CODE CampaignInteractable.cs:68-71, 130]`, `[CODE CampaignInteractionProxy.cs:19]`, `[CODE CampaignCheckpoint.cs:110-170]`, `[CODE TeamInventoryUI.cs:193]` — các chuỗi còn lẫn Tiếng Việt / nhãn chung.
  - `[INFERENCE]` — toàn bộ văn bản gợi ý Bước 1 là bản chuẩn hóa nội dung chờ duyệt; chưa nạp vào `CampaignContentCatalog.asset`, chưa chỉnh sửa `Assets/`, chưa chạy test trong Unity Editor.
