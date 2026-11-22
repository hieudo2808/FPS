# Outbreak Protocol · Mẫu phụ đề radio ưu tiên một dòng

**20/11/2026 · Mẫu chữ chờ duyệt.** Bản 48 ngắt tay nhiều nhịp ở khoảng 40 ký tự, tính cả nhãn người nói. Vì dấu xuống dòng đã nằm trong lời thoại nạp vào catalog, có thêm chỗ ngang cũng không làm hai dòng nhập lại. [CODE `Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset:1014`] [CODE `Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:903`]

**Mẫu D05 tiếng Anh, bản hai đặc vụ**

Đang hiển thị:

```text
Vega: Generator's up.
Cold storage has power.
```

Đề xuất:

```text
Vega: Generator's up. Cold storage has power.
```

**Cùng quy tắc cho tiếng Việt và các mã khác:**

```text
D05   Vega: Máy phát chạy rồi. Kho lạnh có điện trở lại.
D09   Vega: Sổ bàn bảo vệ ghi 02:40, đúng không?
D21   Bộ chỉ huy: Vanguard, trạm ngoài báo đã nhận đủ bản sao E-02.
```

Dòng D21 là ví dụ cho khung đủ rộng. Nếu khung thực tế hẹp, giữ nhãn ở dòng đầu và ngắt theo cụm nghĩa, ví dụ:

```text
Bộ chỉ huy: Vanguard, trạm ngoài báo
đã nhận đủ bản sao E-02.
```

- Ưu tiên một dòng nếu vừa khung và dễ đọc trong thời gian đang hiện. Hai dòng dành cho nhịp thực sự cần ngắt; không thu nhỏ chữ để cố ép thành một dòng. Một lượt thoại vẫn có thể hiện qua vài nhịp nối tiếp.
- Giữ lời, người nói và thời lượng từng nhịp. Cần thử câu ngắn và câu dài ở 1080p, 4K và trong cửa sổ hẹp, cả lúc đang chơi lẫn lúc mở bảng nhiệm vụ, để kiểm tra tràn chữ hoặc đè HUD.
- Tài liệu đối chiếu: *Netflix Timed Text Style Guide – General Requirements*, mục “Line Treatment”, ưu tiên một dòng trong giới hạn. *Xbox Accessibility Guideline 104 – Subtitles and captions* khuyên tránh dòng dài, nhất là khi thoại nhanh. Với game này, mốc 40 là dấu hiệu cần kiểm tra khả năng đọc, không phải điểm bắt buộc xuống hàng.
- Skill đã đọc: [unity-mcp-ui-layout](C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md), mục *Text Layout Rules*: chọn quy tắc xuống dòng và xem bề rộng khung trước cỡ chữ; [game-ui-ux](C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md), *Core workflow* bước 3 và 7: giữ trong vùng an toàn và kiểm tra ở nhiều kích thước màn hình.

Chỉ có mẫu chữ; chưa đổi dữ liệu hay giao diện Unity. [AGENTS.md](../AGENTS.md) yêu cầu duyệt mẫu trước khi sửa mã UI.
