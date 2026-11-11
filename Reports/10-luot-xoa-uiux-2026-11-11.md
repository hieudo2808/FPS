# Lượt xóa UI/UX nhiệm vụ — 11/11/2026

Thực hiện theo yêu cầu trực tiếp "Làm lượt xóa" sau báo cáo `09-review-nhiem-vu-uiux-2026-11-11.md`. Phạm vi **hẹp hơn** danh sách 12 mục trong báo cáo 09: chỉ làm phần xóa không đụng flow đã duyệt hoặc test đang bảo vệ một quyết định có chủ đích. Không đổi logic nhiệm vụ, RPC, Tank, save, AI, audio, catalog asset.

## Đã xóa

| Mục | Thay đổi | Bằng chứng |
|---|---|---|
| Bảng tên fallback | `ObjectiveAction` chỉ còn đọc catalog; bỏ 20 chuỗi viết tay | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:220] |
| Tên bước lặp trên tracker | Pha Exploring không còn ghi lại tên bước ở dòng dưới; Factory vẫn hiện hai nhánh | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:167] |
| Popup hướng dẫn ở cửa | Xóa `OpenGate`, `ObjectiveText`, `AccessInstructions` và nút CLOSE GUIDE. Cửa giờ hiện toast "Requires: …", hoặc tên bước nếu đã mở khóa | [CODE Assets/FPS/Features/Missions/Runtime/CampaignDoor.cs:35] |
| Checklist trong panel Route và Lift | Bỏ đoạn `[DONE]/[ ]` nối vào cuối tài liệu | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:482] |
| Danh sách bước lặp trong chi tiết Missions | Giữ dòng đếm "POWER 1 OF 3 COMPLETE", bỏ các dòng lặp cột trái | [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:315] [PLAYTEST Reports/UIUX/DeletionPass-2026-11-11/After/three-tabs-missions-1920x1080-100.png] |
| Câu trạng thái viết lại trong Campaign Record | `JournalRecordText` chỉ còn Recorded Transmissions | [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:635] [PLAYTEST Reports/UIUX/DeletionPass-2026-11-11/After/three-tabs-record-1920x1080-100.png] |
| Footer Inventory | Bỏ "SELECT AN ENTRY TO VIEW DETAILS" và chỉ dẫn đóng thứ hai; đường kẻ footer chỉ hiện khi có nút Resume | [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:195] |
| Chuỗi không còn đường tới | "NO FILES RECORDED" | [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:200] |
| Chữ lộ kiến trúc mạng | Bỏ "Tracking is local; progress is shared by the squad." và "The server reports timeout…" | [CODE Assets/FPS/Features/UI/Runtime/TeamInventoryUI.cs:308] [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:876] |

Năm file runtime bị chạm: `CampaignHUD.cs`, `CampaignHUD.Navigation.cs`, `CampaignDialogue.cs`, `CampaignDoor.cs`, `TeamInventoryUI.cs`. Không sửa test nào; hằng `Evidence` trong `HudGameplayAcceptanceTests.cs` được đổi tạm để ảnh ghi vào thư mục mới rồi trả về nguyên giá trị.

## Cố ý không làm

| Mục trong báo cáo 09 | Lý do |
|---|---|
| Gộp hint ba bậc | Ba bậc 45/90/135 giây là yêu cầu đã chốt từ Bot 5, có test riêng và test đối chiếu catalog với nguồn. Đây là đổi flow, không phải xóa |
| Hàng "System -" và "Source" trên device | Test PlayMode assert có chủ đích từng hàng; bỏ hàng làm lệch layout device đã duyệt mockup |
| Nhánh Failed và Completed trong `RenderPhaseControls` | Chưa chứng minh được `CampaignStatusUI` luôn tồn tại trong mọi scene chạy campaign |
| Dòng "Then:" trên tracker | Đang là nơi duy nhất nói kết quả của bước; chỉ nên bỏ cùng lúc đưa mục tiêu chapter lên tracker |
| Eyebrow "CAMPAIGN" | Bỏ sẽ để lại khoảng trống trong header đã duyệt |
| Chữ kỹ thuật của Tank và cổng party, hint "the server must confirm" trong catalog | Là viết lại nội dung, đi cùng flow Tank; catalog phải sửa qua nguồn authoring |
| Nút NOT READY, đổi tên Campaign Record | Không thuộc lượt xóa thuần |

## Kiểm tra

| Kiểm tra | Job | Kết quả |
|---|---|---|
| Compile sau sửa | debug_get_errors | 0 lỗi |
| HudRegressionTests, EditMode | 848e40c7 | 19/19 pass, 67s |
| CampaignRulesTests, EditMode | c55f8a1f | 63/63 pass, 18s |
| HudAssetReplacementTests, EditMode | bb25e3e9 | 3/3 pass, 6s |
| ThreeTabInventoryPreservesRecordProgressAndFocus, PlayMode | 40fa5fb1 | 1/1 pass, 101s, 12 PNG |

Log thật, nguyên văn `resultSummary`:

```
Test run completed: 19/19 passed.
Test run completed: 63/63 passed.
Test run completed: 3/3 passed.
Test run completed: 1/1 passed.
```

JSON kết quả nằm trong `Reports/UIUX/DeletionPass-2026-11-11/`. Ảnh sau ở 1920×1080, 3840×2160, 1024×768, scale 100% và 125% nằm trong `After/`. Ảnh trước dùng bộ `Reports/UIUX/ThreeTabInventory-2026-11-10/After/` cùng tên file.

Đã xem ảnh Missions 1080p và 4K, Record 1080p: bố cục hai cột không vỡ, không tràn chữ, footer trống gọn. Ảnh cũng xác nhận phát hiện trong báo cáo 09: Case và Route nằm dưới nếp cuộn ("SCROLL FOR MORE").

Save Gate trước và sau mỗi lượt compile và test: `scene_save` cộng `File/Save Project`. [SCENE Assets/FPS/Scenes/MainMenu.unity] cuối phiên `isDirty=false`, `isPlaying=false`, `isCompiling=false`.

## Chưa kiểm được

- Toast ở cửa và dòng tracker ở Northbridge, Laboratory chưa có ảnh hay playtest; bài PlayMode đã chạy chỉ phủ Inventory ở Factory. Toàn bộ `HudGameplayAcceptanceTests` chưa chạy lại.
- Chưa chạy host cùng client hai tiến trình.
- Chưa có review độc lập; ảnh trong `After/` được giữ cho tới khi review xong.
- Khi cửa đã mở khóa, toast hiện tên bước lấy từ catalog (chữ in hoa); chưa được duyệt câu chữ. [INFERENCE]

## Skills đã đọc và quy tắc áp dụng

- `unity-skills`: lấy schema trước khi gọi; test bất đồng bộ, chạy tuần tự, đọc kết quả theo jobId; readback scene sau cùng.
- `audit-ai-design-slop`: xóa trước khi thay; giữ phần mang thông tin (dòng đếm tiến độ).
- `game-ui-ux`: không thêm màn vào stack; phản hồi cửa dùng kênh feedback sẵn có.
- AGENTS.md: ưu tiên chỉ dẫn trực tiếp của người dùng, nhưng dừng ở những mục có flow đã duyệt bảo vệ và liệt kê chúng làm đề xuất.
