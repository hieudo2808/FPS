# Outbreak Protocol · Triển khai radio bản 48

**19/11/2026.** Kịch bản đã duyệt tại [bản 48](48-radio-kich-ban-cho-duyet.md) được đưa vào `CampaignContentCatalog`: 28 mã D01–D28, 10 bản một đặc vụ, 166 nhịp phụ đề với đủ hai bản tiếng Việt và tiếng Anh. Bản chạy hiện dùng tiếng Anh theo giao diện hiện có; phụ đề tiếng Việt đã có dữ liệu để dùng khi dự án có chọn ngôn ngữ.

- **D05** xác nhận máy phát đang chạy và kho lạnh có điện trở lại. Bản một đặc vụ cũng phân biệt có điện với việc đèn báo sáng.
- **D09** đối chiếu giờ tiếp nhận **02:40** với **nhóm C12**. Nếu không đủ hai vai hiện trường thì Bộ chỉ huy đáp thay.
- **D21** nhận xác nhận từ trạm ngoài rằng đã nhận đủ **bản sao E-02**. Cả đội đem **hai kiện** ra thang hàng và chờ ở đó.

## Cách radio phát trong game

- [CODE CampaignContentCatalog.cs:79] Mỗi mã giữ các nhịp theo thứ tự trong bản duyệt. D07 và D13 mỗi cảnh tách thêm một nhịp tại ranh giới câu; so lại sau khi chuẩn hóa ngắt dòng cho thấy nguyên văn và dấu câu khớp bản duyệt. Dùng bí danh trong nhãn lẫn trong câu gọi người khác.
- [CODE CampaignMissionController.cs:179] Server chọn người đang có mặt và đủ điều kiện. Ưu tiên vai theo kịch bản, rồi dùng bí danh đang có; nếu không đủ hai bí danh phân biệt thì dùng bản một đặc vụ hoặc Bộ chỉ huy đáp. D24 kiểm đúng người gục và người có thể tới hỗ trợ trong tầm hồi sinh; D28 dùng đúng người được cứu bằng hành động hồi sinh. [CODE CampaignMissionController.cs:487] D19 bắt đầu khi cabin khóa cửa.
- [CODE CampaignDialogue.cs:165] HUD phát tuần tự, từ 1,6 giây mỗi nhịp và tăng theo số ký tự. Cảnh báo chen lên, phụ đề đang đọc tiếp tục sau cảnh báo, và các lời đáp nguy hiểm quá 8 giây bị bỏ; D28 loại phần đáp D24 đang chờ của cùng người. D22 và D23 xếp chung một cuộc gọi. D20 kết thúc trước màn tổng kết [CODE CampaignStatusUI.cs:115].
- [CODE NetworkSessionContracts.cs:11] Host và client dùng giao thức 15 để đồng bộ người nói. Hai máy chơi cùng nhau cần cùng bản build. Định dạng checkpoint giữ nguyên.

Unity xuất lại catalog sau khi nhập: 28 bản ghi radio chứa đúng dữ liệu nạp; toàn bộ trường cũ của radio, hồ sơ, vật phẩm và nhiệm vụ so sánh bằng nhau với bản xuất trước khi sửa. Vì vậy nhật ký vẫn dùng phần tóm tắt hiện có; không đổi tài liệu nhặt được hoặc tiến độ nhiệm vụ. Bản này dùng phụ đề, catalog hiện chưa có clip lồng tiếng cho radio.

## Kiểm tra

- **EditMode:** `Test run completed: 4/4 passed.` Kiểm tất cả mã và biến thể, cả hai ngôn ngữ và mọi cặp bí danh có thể hiển thị; tối đa hai dòng và 40 ký tự mỗi dòng gồm nhãn. Kiểm đổi vai, xếp ưu tiên, bỏ câu đáp đã cũ và thứ tự D22 → D23 → D21.
- **Windows Development Player:** `Succeeded`, `errors=0`, `warnings=24`, `seconds=125`. Client chạy thành tiến trình riêng.
- **PlayMode co-op:** `Test run completed: 1/1 passed.` (job `d609be81`). Host chạy trong Editor, client chạy bằng bản Windows ở tiến trình riêng. Hai máy khớp nhãn người nói và phụ đề từng nhịp của D01, D02, D24, D28, D05 và D09 bản một đặc vụ, cùng chuỗi D22 → D23 → D21. D01/D02 phát từ luồng nhiệm vụ trong `GameScene`; D24/D28 dùng tình huống Doc gục và Mason giữ tương tác hồi sinh. D05, D09 và chuỗi D22 → D23 → D21 được kích từ điểm phát radio phía server trong bài test.
- **[PLAYTEST screenshot] 1080p và 4K:** Game view kiểm 368 trường hợp phụ đề tiếng Việt và tiếng Anh ở mỗi độ phân giải. Tất cả tối đa hai dòng và không bị cắt theo chiều dọc. Hai cặp ảnh trước/sau được đối chiếu; ở D05, người kiểm độc lập xác nhận phụ đề tiếng Anh không đè các phần khác của HUD. Ảnh tạm đã được xóa sau khi đối chiếu; giữ log test.
- **[SCENE]** Sau khi lưu scene và project, `scene_get_info` trả `GameScene`, `isDirty=false`. SHA-256 trước và sau test khớp cho `GameScene.unity` (`032c97d5…6db0d87`), checkpoint hiện tại (`97370558…e6b688a`) và checkpoint dự phòng (`57b13f37…057f8d`). `git diff --check` không báo lỗi khoảng trắng trong phần thay đổi liên quan, với `cr-at-eol` cho tệp dùng CRLF.

Trích từ `Logs/Editor.log`:

```text
[Radio48] GameScene loaded on clients [0,1]
[Radio48] D05 host = client: Mason: Command, cold storage has power.
[Radio48] D09 host = client: Command: Right. That's the intake time. The sheet has to match C12 too.
[Radio48] D21 host = client: Command: Vanguard, the station confirms it has the full E-02 copy.
[Radio48] D21 host = client: Mason: Roger. We'll bring both cases to the lift and wait there.
[Radio48] 1920x1080: 368 localized captions checked on the actual HUD, at most two lines, no vertical clipping
[Radio48] 3840x2160: 368 localized captions checked on the actual HUD, at most two lines, no vertical clipping
```

Chưa chơi xuyên suốt chiến dịch; các mã được kích trực tiếp qua bài test chưa được thử qua toàn bộ chuỗi thao tác của người chơi.

## Skill đã áp dụng

- [dialogue-systems](C:/Users/hieud/.codex/skills/dialogue-systems/SKILL.md): mục *Core workflow*, bước 1 “Keep what exists”; giữ mã D và catalog sẵn có, dùng chuỗi lượt thoại và để trạng thái nhiệm vụ ở server.
- [unity-skills](C:/Users/hieud/.agents/skills/unity-skills/SKILL.md), mô-đun [scriptableobject](C:/Users/hieud/.agents/skills/unity-skills/skills/scriptableobject/SKILL.md) và [test](C:/Users/hieud/.agents/skills/unity-skills/skills/test/SKILL.md): chạy thử tham số nhập JSON trước, nhập qua Editor, xuất lại để đối chiếu và lấy kết quả `test_run_by_name` qua `test_get_result`.
