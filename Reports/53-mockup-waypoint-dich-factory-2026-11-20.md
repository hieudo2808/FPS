# Mockup waypoint tại đích — đầu Factory

Flow đã đồng ý: ngay khi hai nhánh đi vào trạng thái có thể làm, HUD liệt kê cả Power lẫn Logistics. Mỗi nhánh đánh dấu điểm tương tác của bước hiện hành; người chơi chọn thứ tự và tự tìm đường. Bản dưới là mockup chờ duyệt, chưa sửa HUD trong game.

```text
┌─ KHUNG NHÌN ─────────────────────────────────────────────────────────────────┐
│ ┌───────────────────────────────────┐                                        │
│ │ FACTORY                           │                                        │
│ │ Recover the evidence case         │                                        │
│ │                                   │                                        │
│ │ Power                       0/3   │                                        │
│ │ Isolate production power          │                                        │
│ │                                   │                                        │
│ │ Logistics                   0/2   │                         ●              │
│ │ Read delivery manifest            │                      LOGISTICS          │
│ └───────────────────────────────────┘                                        │
│                                        +                                     │
│                    ●                                                         │
│                  POWER                                                       │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

`+` là tâm ngắm hiện có. Hai `●` đại diện sprite `GoalMarkerCentre` gắn vào tủ điện và bàn manifest. Vị trí trái/phải trong hình chỉ minh họa góc camera; tên bước là chữ minh họa bố cục, khi triển khai lấy từ catalog. `HIGHER` / `LOWER` nằm ngay dưới tên nhánh khi đích có chênh cao rõ ràng.

| Tình huống | Hiển thị đề xuất |
|---|---|
| Cả hai đích nằm trong khung | Hai chấm trắng, mỗi chấm có tên nhánh trùng với tracker. Quay camera chỉ đổi vị trí chiếu của chấm. |
| Hai mốc gần như trùng nhau trên màn hình | Giữ tâm chấm ở tọa độ chiếu của từng đích. Tách hai nhãn sang hai bên với thứ tự cố định khi chúng đè nhau; nếu tâm trùng khít, hai chấm có thể nhìn thành một nhưng cả hai tên vẫn đọc được. |
| Đích khuất sau tường, hoặc khác cao độ | Vẫn thấy chấm nếu vị trí chiếu nằm trong khung; thêm `HIGHER` hoặc `LOWER` khi đủ chênh cao. Đây là vị trí đích, không phải lối đi qua tường. |
| Đích ngoài khung, sau lưng hoặc lọt dưới một panel HUD | Ẩn mốc tương ứng; tracker vẫn hiện tên và việc cần làm. Không có mũi tên ở mép, khoảng cách theo tuyến hoặc dòng `ROUTE UNAVAILABLE` cho hai nhánh này. |
| Xong một bước | Chỉ nhánh vừa xong cập nhật việc và mốc khi server xác nhận. Nhánh hoàn tất hiện `COMPLETE` và bỏ mốc. Bước bị khóa không có mốc. |

[CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:286] `FactoryNext` dùng điều kiện nhiệm vụ hiện có để lấy bước khả dụng của từng nhánh. [CODE Assets/FPS/Features/Missions/Runtime/CampaignInteractable.cs:60] `TargetPosition` là vị trí dùng cho mốc. [CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:95] Tái dùng khung tracker, font/theme và sprite `Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/GoalMarkerCentre.png`; bỏ gợi ý `[T] SWITCH` cùng dấu chọn nhánh trong tracker này. Giới hạn thay đổi ở giai đoạn Power/Logistics; khi hai nhánh xong, hướng dẫn pha tiếp theo trở lại hành vi hiện có.

[INFERENCE] Mốc đích sẽ hết đổi sang một góc rẽ khác khi lia camera. Mốc qua tường vẫn không giải thích cửa hay cầu thang nào dẫn tới đích; cần thử một lượt từ cửa Decon để xem người mới có tìm được cả hai nơi bằng lối đi sẵn có không.

Skill đã đọc và áp dụng: `unity-mcp-ui-layout` (phác bố cục trước khi tạo UI, tái dùng tài nguyên), `game-ui-ux` (neo tracker theo vùng an toàn; giữ chữ của hai dòng độc lập), `level-design` (giữ nguyên điều kiện mở bước và thử tìm đường từ đầu màn), `unity-skills` (kiểm tra scene là thao tác chỉ đọc). [SCENE] `GameScene`, `isDirty=false`. Chưa chạy playtest cho mockup; sau khi duyệt, cần kiểm host/client và ảnh Game View trước/sau ở 1080p và 4K theo `AGENTS.md`.
