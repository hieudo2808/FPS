# Rà soát chi tiết UI — 14/09/2026

## Kết luận

Bản hiện tại đã vượt qua mức prototype về bố cục và đồng bộ hình ảnh, nhưng chưa nên xem là bản chốt phát hành. Điểm yếu còn lại nằm ở **độ đọc khi thu nhỏ, trạng thái focus bằng bàn phím/gamepad và một vài hành vi Settings**, không nằm ở việc thiếu thêm texture.

Rà soát này kết hợp ảnh Canvas ở 1920×1080, 1280×720, 1024×768 và 2560×1080; ảnh runtime Settings; probe HUD HP 25/100; source/runtime hiện tại; và hai nguyên tắc vừa đối chiếu:

- GamesIndustry/Double Eleven khuyên dùng design pattern theo thể loại, wireframe grayscale trước màu, hierarchy rõ, animation nhẹ và không để UI cạnh tranh với thông tin chính ([bài viết](https://www.gamesindustry.biz/best-practices-for-designing-an-effective-video-game-ui), các đoạn “design patterns”, “wireframing”, “hierarchy”, “animation”).
- Xbox Accessibility Guidelines 101/102 khuyến nghị text PC/VR tối thiểu 18 px ở 1080p, hỗ trợ scale tới 200%; text thông thường tương phản tối thiểu 4.5:1, text lớn 3:1, high contrast 7:1, và không truyền đạt trạng thái chỉ bằng màu ([XAG 101](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/101), [XAG 102](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/102)). Các ngưỡng này là hướng dẫn accessibility, không phải chứng nhận tự động cho game.

## Phát hiện theo mức độ

### P0 — cần làm trước khi gọi UI hoàn thiện

**Không có P0 về chồng lấn trong các trạng thái đã render.** 13 ảnh layout đều báo text/control trong vùng; HUD nguy hiểm cũng giữ được khoảng cách icon/chữ. Đây là phạm vi các trạng thái mẫu đã tạo, không bao phủ mọi trận online, subtitle, chat hay hiệu ứng runtime.

### P1 — nên sửa trong đợt UI tiếp theo

1. **HUD phụ quá nhỏ ở 1024×768 và có nguy cơ không đọc được trên màn hình xa.** Ảnh đo được ink height xấp xỉ 7 px cho `INFECTION 0%`, 6 px cho `EMPTY` và phím slot. Source TMP tương ứng là 18/16 px ở reference 1920×1080; khi Canvas thu nhỏ, chúng giảm mạnh. Số liệu: [ui-review-measurements.json](ui-review-measurements.json), bảng đầy đủ: [ui-review-scene-metrics.tsv](ui-review-scene-metrics.tsv). Tăng scale tối thiểu của nhóm HUD phụ hoặc thêm tuỳ chọn HUD scale; không chỉ tăng font riêng lẻ vì sẽ làm lệch anchor.
2. **Không có lựa chọn text/HUD scale hoặc high-contrast mode.** XAG 101 yêu cầu mục tiêu scale tới 200%; XAG 102 khuyến nghị nền chữ/opacity, màu cấu hình và high contrast. Hiện có palette cố định và safe area, chưa có các tuỳ chọn này. Đây là thiếu tính năng accessibility, không phải lỗi mỹ thuật. Ưu tiên tạo token scale chung cho menu/HUD/notification rồi kiểm tra lại roster dài và 1024×768.
3. **Focus keyboard/gamepad bị xoá sau nhiều chuyển trạng thái.** `SettingsUI`, `LobbyUI` và `InGameMenuUI` gọi `EventSystem.current.SetSelectedGameObject(null)` khi mở/đóng hoặc kết thúc rebind; probe Edit Mode cũng thấy `firstSelectedGameObject=<null>`. Chuột vẫn hoạt động, nhưng người dùng tay cầm có thể mở màn hình mà không có control được chọn. Mỗi panel nên gán control đầu tiên hợp lệ sau `SetActive`, tab nên chọn lại tab tương ứng, và pause menu nên khôi phục Resume.
4. **Reset Defaults có phạm vi không rõ.** Nút nằm ở footer mọi tab nhưng reset âm thanh, sensitivity và keybind; không reset quality dropdown. Người chơi ở Graphics có thể tưởng toàn bộ Settings đã reset. Đổi nhãn thành `RESET ALL SETTINGS`, hoặc giới hạn theo tab và thêm trạng thái xác nhận nhẹ; nếu giữ reset toàn cục thì phải reset cả graphics và cập nhật dropdown.
5. **Giá trị preview slider không phản ánh chắc chắn trạng thái runtime.** Ảnh authoring Controls thể hiện slider 100% trong khi runtime Settings probe thể hiện sensitivity 0.1; code runtime đọc SettingsManager/PlayerPrefs, còn renderer authoring dựng text mẫu. Đây là sai khác giữa công cụ preview và game, không phải bằng chứng gameplay đang sai. Preview nên khởi tạo từ giá trị fixture rõ ràng hoặc ghi “sample state”; không dùng ảnh preview để QA giá trị người chơi.

### P2 — chất lượng và tính nhất quán

6. **Main menu ở ultrawide 2560×1080 để khoảng trống lớn bên phải.** Đây là hệ quả hợp lý của giữ cảnh thành phố làm hero image, nhưng cột menu bên trái hơi nhỏ so với diện tích toàn màn hình. Có thể tăng nhẹ scale cột/title ở aspect > 2.0 hoặc đặt một vùng visual anchor mờ bên phải; không kéo panel vào giữa vì sẽ phá bố cục 16:9.
7. **Nhiều control dùng cùng một plate.** Texture đã đồng bộ nhưng tab, input, dropdown, button và row plate vẫn gần cùng trọng lượng thị giác. Giảm border/opacity cho input không hoạt động, giữ accent cho hành động chính và selection; mục tiêu là hierarchy bằng độ sáng/kích thước trước khi thêm ornament.
8. **Disabled state còn phụ thuộc nhiều vào màu.** `TacticalUiTheme` đã dùng muted, nhưng một button bị disable chủ yếu tối đi. Bổ sung icon/label trạng thái hoặc pattern/alpha rõ hơn để không yêu cầu phân biệt màu; đặc biệt Start/Deploy ở lobby khi chưa đủ người.
9. **Tên và thông tin phụ dùng full caps dày đặc.** Heading caps phù hợp phong cách, nhưng helper text, trạng thái và tên người chơi nên sentence case/regular weight khi có thể. XAG 101 miễn trừ nhãn một-hai từ, nhưng các câu dài nên ưu tiên đọc tự nhiên.
10. **Sanitize tên người chơi xoá khoảng trắng trong lúc gõ.** `LobbyUI` gọi `SanitizePlayerName` ở `onValueChanged`; mô phỏng nhập “Minh Anh” cho kết quả “MinhAnh”. Đây là lỗi UX dữ liệu có thể tái hiện, không phải lỗi layout. Chỉ sanitize/trim khi end-edit hoặc cho phép khoảng trắng nội bộ, sau đó giới hạn 24 ký tự.
11. **Tín hiệu “online continues” trong pause menu đúng về hệ thống nhưng cần nổi bật như cảnh báo.** Dòng này hiện là helper text muted. Vì game nhiều người không thực sự dừng, dùng màu/biểu tượng cảnh báo nhẹ để người chơi không nhầm Resume là pause server; không cần thêm phase label.
12. **Copy room code chỉ phản hồi bằng status text.** Có “Join code copied!” trong source; nên giữ focus/feedback trực quan trên nút trong khoảng ngắn, vì người dùng có thể không nhìn vùng status ở độ phân giải thấp.

## Điều đã kiểm chứng và điều chưa thể khẳng định

- Đã xem trực tiếp main, play, Settings audio/graphics/controls, lobby, HUD thường, HUD HP thấp và pause; ảnh chi tiết tổng hợp ở [UI-review-details.jpg](UI-review-details.jpg).
- HUD logic thật xác nhận HP 25%, fill 0.25, blood edge bật; HP đầy tắt blood edge; infection icon tách khỏi text. Probe không bao phủ toàn bộ `SepsisWarning`.
- Code có binding cho Move/Jump/Sprint và interactive rebind dùng Input System; Settings hiện chỉ trình bày bảy binding chiến đấu. Nếu game cần remap movement, phải thêm chúng vào UX trước khi quảng bá phần Controls là đầy đủ.
- `PlayerRosterEntryView` tách player name/detail/readiness và giới hạn tên 24 ký tự; ảnh tên dài không đè readiness. Cần thêm kiểm thử ký tự Unicode, CJK và font fallback nếu game hỗ trợ các ngôn ngữ đó.
- Chưa có playtest người thật, gamepad vật lý, thiết bị DPI khác, subtitle/chat, Relay nhiều client hay một match chiến đấu hoàn chỉnh. Không suy rộng PASS của ảnh Canvas thành bảo đảm mọi trạng thái runtime.

## Thứ tự đề xuất

1. Sửa focus restoration và Reset Defaults (P1 chức năng).
2. Thêm token UI/HUD scale + high-contrast/text background opacity (P1 accessibility).
3. Sửa sanitize tên và feedback copy code (P2 UX).
4. Rerender 1024×768, ultrawide, text scale 150/200%; sau đó playtest gamepad và một lobby thật.
5. Chỉ sau các bước trên mới cân nhắc tăng ornament hoặc thêm asset mới.

Lần rà soát này không tự ý đổi layout theo cảm tính; scene đang mở vẫn là MainMenu và đã được lưu sạch.
