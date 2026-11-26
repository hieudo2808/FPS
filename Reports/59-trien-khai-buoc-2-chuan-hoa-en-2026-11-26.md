# Báo cáo triển khai Bước 2 — Chuẩn hóa Catalog EN, Đồng bộ Runtime, Nhịp gợi ý 25/45/75s & Tô màu từ khóa

- **Ngày thực hiện:** 2026-11-26
- **Tài liệu đã duyệt:** [58-chuan-hoa-en-quest-files-radio-2026-11-26.md](file:///d:/du%20an/FPSGAME-HIEU/FPS/Reports/58-chuan-hoa-en-quest-files-radio-2026-11-26.md)
- **Phạm vi Bước 2:** Nạp toàn bộ nội dung chuẩn hóa 100% tiếng Anh vào `CampaignContentCatalog.asset`, đồng bộ chuỗi EN & nhịp gợi ý `25 / 45 / 75s` trong code runtime, triển khai bộ tô màu từ khóa `CampaignTextHighlight` bám `SelectedUiTheme`, và cập nhật/bổ sung Editor & PlayMode tests. *(Không thay đổi layout UI mới thuộc Bước 3 chưa duyệt mockup.)*

---

## 1. Các thay đổi đã thực hiện

### 1.1. Formatter tô màu động bám `SelectedUiTheme`
- Tạo [CampaignTextHighlight.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignTextHighlight.cs):
  - Đọc trực tiếp `SelectedUiTheme.Current.Accent` và `SelectedUiTheme.Current.Danger` (`ColorUtility.ToHtmlStringRGB`) — không hardcode màu ngoài theme; tự điều chỉnh tương phản khi `onPaper == true` (trên nền giấy sáng của panel thiết bị).
  - `Format(...)`: chuyển cụm `**...**` và từ khóa trong từ điển (Mục 1.4 của Report 58) thành `<color=#...>...</color>` với giới hạn tối đa `3` cụm/dòng, giữ idempotent (không bọc chồng tag).
  - `FormatFileRecord(...)`: tự động tách hộp **`KEY DATA`** (từ `file.summary`) ở đầu trang và **`FULL RECORD`** (từ `file.body`) ở bên dưới.
  - `StripRichText(...)`: gỡ tag rich-text để `CampaignHUD.RadioCaption` và test kiểm tra nội dung chính xác.

### 1.2. Nạp dữ liệu chuẩn hóa 100% EN vào Catalog
- Cập nhật [CampaignContentCatalog.asset](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset):
  - **21 Files (`id: 1–21`):** Đầy đủ hộp `KEY DATA` (`summary`) + `BODY` (`body`) kèm đánh dấu `**...**` để tô màu, bảo toàn toàn bộ 21 `icon` GUID và `autoDiscoverAfter`.
  - **4 Key Items (`id: 1–4`):** Đồng bộ `title` và `description` chuẩn EN, giữ nguyên 4 `icon` GUID và rule nhận/dùng.
  - **20 Objectives (`id: 1–20`):** Đồng bộ `title` (bắt đầu bằng động từ hành động), `instructions`, `result` và `hint` / `hint2` / `hint3` (`25s / 45s / 75s`), giữ nguyên đáp án `choices` / `secondaryChoices`.
  - **28 Radio (`id: 1–28`) + 10 `soloCues` (tổng 132 cues):** Cập nhật theo Report 58 (cùng `{2}` cho các lượt gọi tên đồng đội ở `D12`, `D19`, `D25`, `D27`), đồng bộ `vietnamese = english` (100% EN).

### 1.3. Đồng bộ chuỗi EN, Nhịp gợi ý & Gắn tô màu trong Runtime C#
- [CampaignTypes.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs#L246-L272):
  - `[CODE CampaignTypes.cs:246]` Đổi `HintTier` sang **`25s / 45s / 75s`** (`seconds >= 75 ? 3 : seconds >= 45 ? 2 : seconds >= 25 ? 1 : 0`).
  - `[CODE CampaignTypes.cs:269-272]` Chuẩn hóa `ChapterName` (`FACTORY`, `NORTHBRIDGE`, `LABORATORY B2`) và `ChapterGoal` (`Recover the T9-17 case`, `Trace P046 & reach B2`, `Transmit complete E-02`).
- [CampaignInteractable.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs#L68-L131) & [CampaignInteractionProxy.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignInteractionProxy.cs#L19):
  - `[CODE CampaignInteractable.cs:68-71]` Đổi `🔒 Cần: ...` / `[T] theo dõi` → `LOCKED: Requires ...` / `[T] Track objective`.
  - `[CODE CampaignInteractable.cs:127-131]` Thay nhãn chung `"SOURCE DOCUMENT"` bằng `Title` thật của tài liệu (`Field Document` nếu trống).
  - `[CODE CampaignInteractionProxy.cs:19]` Đổi `"🔒 Cần: objective"` → `"LOCKED: Objective unavailable"`.
- [CampaignCheckpoint.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignCheckpoint.cs#L110-L170):
  - `[CODE CampaignCheckpoint.cs:110-170]` Chuyển toàn bộ 6 thông báo lỗi tiếng Việt sang tiếng Anh.
- [CampaignHUD.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs#L68-L907), [CampaignHUD.Navigation.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs#L158-L186), [CampaignHUD.FactoryDestinations.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.FactoryDestinations.cs#L73), [TeamInventoryUI.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs#L258-L343):
  - `[CODE CampaignHUD.cs:134-136]` `RefreshHintTarget` dùng đúng mốc `{ 25f, 45f, 75f }`.
  - `[CODE CampaignHUD.cs:406, 531-535, 900, 907]` Bật `supportRichText = true`, gắn `CampaignTextHighlight.Format` / `FormatFileRecord` lên Tracker, Hints, Radio subtitle, trang tài liệu thiết bị và `TeamInventoryUI` (`KEY DATA` + `FULL RECORD`).
  - `[CODE CampaignHUD.cs:68]` `RadioCaption` dùng `StripRichText` để giữ kiểm chứng phụ đề sạch tag màu.

### 1.4. Cập nhật & Bổ sung Tests
- [CampaignRadioTests.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/Tests/CampaignRadio/Editor/CampaignRadioTests.cs): Cập nhật assertions khớp 28 scenes, 10 soloCues, tổng `132` cues, kiểm tra mọi tổ hợp cast `<= 40` ký tự/dòng và các chuỗi mới.
- [CampaignRadioHostClientTests.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/Tests/CampaignRadio/PlayMode/CampaignRadioHostClientTests.cs#L151): Dùng `CampaignTextHighlight.StripRichText(hostCaption.text)` khi đối chiếu phụ đề host/client.
- [CampaignStep2ContentTests.cs](file:///d:/du%20an/FPSGAME-HIEU/FPS/Assets/Tests/CampaignRadio/Editor/CampaignStep2ContentTests.cs): Bổ sung test kiểm tra `HintTier(25/45/75)`, `ChapterName`, `ChapterGoal`, `CampaignTextHighlight` (accent/danger/paper/idempotent/strip) và độ đầy đủ của `CampaignContentCatalog.asset` (`21 files` có `KEY DATA` riêng, `4 keyItems`, `20 objectives`, `28 dialogues`).

---

## 2. Kết quả kiểm chứng

| Hạng mục | Kết quả | Chi tiết |
|---|---|---|
| Biên dịch `FPS.dll` (Unity 6000.0.40f1 Roslyn `csc.dll`) | **PASS (Exit code 0)** | Gồm `CampaignTextHighlight.cs` và toàn bộ file sửa |
| Biên dịch `FPS.CampaignRadio.Tests.Editor.dll` | **PASS (Exit code 0)** | Gồm `CampaignRadioTests.cs` & `CampaignStep2ContentTests.cs` |
| Biên dịch `FPS.CampaignRadio.Tests.PlayMode.dll` | **PASS (Exit code 0)** | Gồm `CampaignRadioHostClientTests.cs` |
| Kiểm tra YAML `CampaignContentCatalog.asset` (`PyYAML`) | **PASS** | `files=21`, `keyItems=4`, `dialogue=28`, `totalCues=132`, `objectives=20`, `file_icons=21`, `key_icons=4` |
| Kiểm tra 40 ký tự phụ đề qua mọi tổ hợp `(first, second)` | **PASS (0 lỗi)** | Đã chỉnh `D02` (`Copy. Reading the breaker manual.`) để `Mason:`/`Raven:` đạt đúng `40` ký tự |
| Quét chuỗi tiếng Việt sót trong `Assets/FPS/Features/Missions/Runtime/` | **PASS (0 kết quả)** | Không còn `Cần:`, `theo dõi`, `Chưa có`, `không hợp lệ`, `trống` |

- **Giới hạn hiện tại `[INFERENCE]`:** Đã biên dịch thành công toàn bộ assembly bằng Roslyn của Unity `6000.0.40f1` và kiểm tra YAML + cast permutation bằng Python; chưa bấm chạy Test Runner trực tiếp trong tiến trình Unity Editor đang mở và chưa playtest lại Host/Client sau chỉnh sửa.

---

## 3. Lưu ý mở rộng & Bước tiếp theo
1. Trong **Unity Editor**, nhấn **`Ctrl + R`** để Unity import `CampaignTextHighlight.cs` / `CampaignStep2ContentTests.cs`, reload `CampaignContentCatalog.asset` và chạy `CampaignRadioTests` + `CampaignStep2ContentTests` trong **Window → General → Test Runner (EditMode)**.
2. Nếu bạn muốn triển khai tiếp **Bước 3** (tách riêng UI `MISSION JOURNAL & EVIDENCE [J]`, thêm auto-pause cho chế độ Solo, panel tóm tắt `KEY DATA` thu gọn khi đang chiến đấu, và phím ping `[V]` hiện lại chấm nhặt đồ), hãy báo để mình lập mockup/flow duyệt trước khi sửa UI layout theo đúng `AGENTS.md`.
