# 40 — Thêm đèn cho hall `Hangar_v2_1 (2)` (2026-11-17)

Nối tiếp đề xuất 1 của báo cáo 38/39. Người dùng chọn "Thêm đèn có sẵn".

Ảnh: `Reports/MapFixLighting-2026-11-17/maintenance_hall_*` (xoá sau khi review xong). Camera Editor, chưa Play.

## 1. Vấn đề

Sau khi bake lại ánh sáng (báo cáo 38), bên trong `Factory/Map_v2/Static/Hangar_v2_1 (2)` gần như đen (độ sáng trung bình ảnh 3,7/255). Nhà này kín và không có đèn. Bên trong có 302 m² sàn đi được và điểm hồi sinh `Recovery_Maintenance` tại (−35; 2,25; −75). `[SCENE]` `[PLAYTEST screenshot]` `maintenance_hall_1_before`

## 2. Chọn đèn

- Bộ asset Factory không có mô hình đèn nào. Đèn có sẵn trong project chỉ thuộc bộ Asylum (`NeonLamp`, `SingleLight`, `Lamp`, `LampWall`) và Lab (`Light_01`, kiểu khoa học viễn tưởng). `[SCENE]`
- Chọn **đèn huỳnh quang `NeonLamp`**, hợp với nhà xưởng. Asylum đang dùng 60 bộ loại này: mesh `AsylumFacility/Models/NeonLamp.fbx` phóng 1,218 lần, gắn sát trần, kèm một đèn điểm bake con (cường độ 1, tầm 5, bóng mềm).
- Tôi nhân bản đúng bộ `Asylum/1stFloor/HallDownstair/NeonLamp (23)` (mesh, vật liệu phát sáng, cờ static, đèn con) nên giống hệt đèn ở Asylum. Không tự dựng hình khối.

## 3. Đã làm

- Thêm `CampaignWorld/Factory/Map_v2/Light/MaintenanceHallLamps` gồm 6 bộ `NeonLamp_MaintenanceHall_01…06`, xếp 2 hàng × 3:
  - x = −32 / −24 / −16;
  - z = −72,6 / −66,6, tức hai bên nóc mái.
- Mỗi bộ gắn sát mặt dưới mái tôn tại chỗ tia dò chạm mái của chính hall (cao 10,56 m, sàn 2,09 m), nghiêng theo độ dốc mái 25°, cách mái khoảng 9 mm để không trùng mặt phẳng.
- Đèn con: giữ kiểu Point / Baked / bóng mềm, chỉ tăng cường độ 1 → 5 và tầm 5 → 12 m. Lý do: đèn treo cao 8,5 m so với 3,5 m ở Asylum; với cường độ 5 thì độ rọi ngay dưới đèn tương đương một đèn Asylum.
- Bộ đèn không có collider (giống bản gốc), nên không ảnh hưởng đi lại hay NavMesh.
- Bake lại ánh sáng (13 phút, thiết lập không đổi): 251 đèn điểm bake (trước 245), vẫn 3 lightmap 2048, không vật nào thiếu lightmap.

## 4. Kiểm chứng

| Ảnh | Trước | Sau |
|---|---|---|
| Trong hall, nhìn dọc nhà (`maintenance_hall_2_lamps`) | 3,7 | **84,5** |
| Từ điểm hồi sinh nhìn vào hall (`maintenance_hall_3_recovery_view`) | — | 75,3 |
| `Hangar_v2_7` (3 góc), `Hangar_v2_basic`, `Hangar_v7` | 38–95 | không đổi (lệch ≤ 0,1) |

Độ sáng trung bình ảnh, thang 0–255. `[PLAYTEST screenshot]`
- Mặt ngoài hall (`maintenance_hall_4_outside`): bình thường, cửa cuốn đóng, không thấy ánh sáng lọt ra.
- Scene lưu và nạp lại sạch, `isDirty=false`. `[SCENE]`

## Tuân thủ và giới hạn

- Dùng asset có sẵn, không tự dựng hình khối hay biển báo. Không đổi vật tương tác, nhiệm vụ, điểm hồi sinh.
- Save Gate: scene sạch trước khi đặt; script tự lưu scene và gọi `AssetDatabase.SaveAssets()`; nạp lại để kiểm; kết thúc bằng `File/Save Project`, `isDirty=false`.
- Script tạm `TempHallLight.cs`, `TempBake.cs` đã xoá; compile sạch.
- Edit mode, chưa Play. Đèn là đèn bake, nên vẫn sáng khi Play mà không tốn hiệu năng thời gian thực.
- Skill đã đọc: `unity-skills`. Chưa có reviewer độc lập.

## Còn lại (chờ bạn quyết)

1. ~~26 anchor không dùng~~ — đã xoá (báo cáo 41).
2. Mặt chồng khít nhẹ có từ trước (báo cáo 39, mục 3).
3. Play thử host/client và commit.
