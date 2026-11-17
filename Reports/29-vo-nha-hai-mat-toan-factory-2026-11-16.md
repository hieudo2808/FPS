# 29 — Sửa xuyên thấu khi đứng trong nhà: toàn bộ vỏ nhà Factory (2026-11-16)

Người dùng gửi ảnh Scene view đặt camera trong nhà nhìn ra, vẫn thấy xuyên tường và mái: "để cam trong nhà nhìn ra vẫn xuyên thấu mà đã được sửa đâu?". Báo cáo 25 chỉ sửa đúng một hangar (`Hangar_v2_1 (2)`) vì khi đó tôi chỉ xét những chỗ người chơi đi tới được; các hangar khác có collider đặc nên tôi đã bỏ qua. Lần này áp dụng cùng kỹ thuật cho mọi vỏ nhà.

Ảnh trước/sau: `Reports/MapFixBuildings-2026-11-16/` (12 kiểu nhà × 2 hướng, cộng 4 ảnh trong hai kiểu nhà kho có giàn; xoá sau khi review xong).

## Góc trong ảnh của người dùng

Là bên trong `CampaignWorld/Factory/Map_v2/Static/Hangar_v7` (nhà mái vòm ở (44.5, 7.4, -10)), nhìn dọc ra cổng vòm và hai ống khói. `[PLAYTEST screenshot]` `in_Hangar_v7_a_before.png` (thấy trời, khung vòm, ống khói) → `in_Hangar_v7_a_after.png` (mái vòm và tường kín).

## Đã làm

- Quét mọi `MeshRenderer` đang bật dưới `CampaignWorld/Factory` có mesh lấy từ `Buildings/Industrial/Hangars/` hoặc `Buildings/Industrial/ServiceShed/`; chỉ xử lý mesh hở (có cạnh biên — tức là chỉ dựng một mặt).
- Sinh 29 mesh `*_TwoSided` mới trong `Assets/FPS/Features/World/Generated/TwoSided/` (dùng lại `Hangar_v2_1_TwoSided` có sẵn) và gán cho **123 renderer**. `[SCENE]` log: `assigned=123 alreadyTwoSided=3 closedSkipped=1 saved=True dirtyAfter=False`.

| Mesh | Số renderer |
|---|---|
| `Hangar_v6_1` | 13 |
| `Hangar_v3_outbuilding_2` | 15 |
| `Hangar_v5_5` | 10 |
| `Hangar_v1_half` | 7 |
| `Hangar_v6_4`, `Hangar_v5_4`, `ServiceShed_BackWall` | 6 mỗi loại |
| `Hangar_v7`, `Hangar_v5_2` | 5 mỗi loại |
| `Hangar_v2_1` (4 cái còn lại), `Hangar_v4`, `Hangar_v3_basic`, `Hangar_v3_outbuilding_1` | 4 mỗi loại |
| `Hangar_v2_7` (+ `_inside`, `_supports`), `Hangar_v2_basic`, `Hangar_v2_outbuilding` | 3 mỗi loại |
| `Hangar_v2_6` (+ `_inside`, `_supports`, `_door1`, `_gate1`), `Hangar_v2_7_gate1`, `_gate4` | 2 mỗi loại |
| `Hangar_v2_2`, `Hangar_v2_outbuilding1`, `Hangar_v2_outbuilding3`, `Hangar_v2_7_gate2`, `_gate3` | 1 mỗi loại |

Không đổi material, collider, NavMesh, script.

## Kiểm chứng

- Ảnh trước/sau từ giữa 12 kiểu nhà, mỗi kiểu 2 hướng: trước khi sửa `Hangar_v6_1`, `Hangar_v7`… thấy trời và cảnh bên ngoài; sau khi sửa đều thấy tường và mái. `[PLAYTEST screenshot]` (camera Editor, chưa Play).
- Nạp lại scene: `Hangar_v7` dùng `Hangar_v7_TwoSided`; `isDirty=false`. `[SCENE]`

## Điểm cần người dùng biết

- **Bên trong nhà tối và dùng lại hoạ tiết mặt ngoài** (tôn, gạch) vì mặt sau dùng chung UV và lightmap với mặt ngoài; các hangar này vốn không có nội thất. Bake lại ánh sáng sẽ đỡ tối hơn nhưng vẫn là hoạ tiết mặt ngoài.
- Số tam giác của các vỏ nhà tăng gấp đôi (khoảng +16.000 tam giác cho cả Factory) — nhỏ so với toàn map.
- ~~**Còn lại các vật một mặt không phải nhà**~~ — đã xử lý, xem mục "Đợt 2" bên dưới. Danh sách ban đầu: container kín `Cargo_container_v1_LD2close` (83), bồn `Oil_tank_v1` (15), ống lớn `Industrial_pipe_v1` (12), giàn `Support_set_v1_*` (33), pallet bao `Bags_on_pallet_v1_2` (25), rào bê tông `Concrete_fence_v2_*` (~400), và các tấm tường/sàn/cửa của bộ Lab (đặt camera ra ngoài phòng Lab sẽ thấy xuyên). Đứng trong các vật này bằng camera Editor vẫn thấy xuyên.
- Đây là mở rộng kỹ thuật đã được duyệt ở báo cáo 25 sang toàn bộ vỏ nhà, làm theo phản hồi của người dùng.

## Đợt 2: các vật một mặt còn lại (người dùng: "Có, làm luôn")

Ảnh trước/sau: `Reports/MapFixProps-2026-11-16/` (16 cặp; xoá sau khi review xong).

- Phạm vi đúng các nhóm đã nêu: mesh từ `Containers/`, `Oil_tanks/`, `Other_props/Pipes/`, `Other_props/Support_set/`, `Other_props/Palets/`, `Fences/` ở mọi khu, và mesh `ExperimentFacility/Models/` + `Campaign/Meshes/` dưới `CampaignWorld/Laboratory`. Chỉ xử lý mesh hở.
- **Submesh trong suốt (kính) không nhân đôi** để tránh lỗi sắp xếp: `Door_Left_01` dùng bản `Door_Left_01_TwoSided_m10` (chỉ nhân đôi phần khung, giữ nguyên kính).
- Kết quả `[SCENE]` log: `assigned=2082 alreadyTwoSided=183 closedSkipped=432 transparentOnlySkipped=0 saved=True dirtyAfter=False`; 42 mesh mới.

| Nhóm | Renderer | Mesh chính |
|---|---|---|
| Tấm Lab | 1.344 | `Floor_01` (sàn và trần) 976, `Wall_Simple_01` 336, `Door_Left_01` 32 |
| Rào | 453 | `Concrete_fence_v2_S` 381, `UNIConcrete_fence_v1_s` 15, `Concrete_fence_v1_wall_set_v2` 15, `Concrete_fence_v2_Gate` 11, `Road_block_v1` 8… |
| Container | 84 | `Cargo_container_v1_LD2close` 83 |
| Ống | 78 | `Pipes_set_v1_*` 66, `Industrial_pipe_v1` 12 |
| Pallet | 58 | `Bags_on_pallet_v1_2` 52, `_1` 6 |
| Giàn | 37 | `Support_set_v1_bridge2_*` |
| Bồn | 28 | `Oil_tank_v1` 15, `Oil_tank_v2` 12 |

Kiểm chứng: ảnh trước/sau trong container, bồn, ống, pallet, giàn, hai mặt rào, và Lab nhìn từ trên trần / ngoài tường — sau khi sửa đều thấy bề mặt thay vì xuyên. `[PLAYTEST screenshot]` (camera Editor, chưa Play). Nạp lại scene: `Oil_tank_v1 (1)` dùng `Oil_tank_v1_TwoSided`, `isDirty=false`. `[SCENE]`

Lưu ý thêm:
- Bên trong container, bồn, pallet rất tối và chữ trên vỏ hiện bị lật gương — cùng lý do mặt sau dùng chung UV/lightmap.
- Thư mục `Generated/TwoSided` giờ có 75 mesh, khoảng 11 MB.
- Các mesh hở khác (đường/sàn sân Factory, đồ nội thất Asylum, decal, kính) vẫn một mặt; chỉ thấy được khi đặt camera dưới mặt đất hoặc bên trong đồ vật.

## Tuân thủ

- Save Gate: scene sạch trước khi sửa, đã `SaveScene` + `SaveAssets`, `isDirty=false` sau khi nạp lại.
- Script tạm `TempTwoSidedAll.cs`, `TempTwoSidedProps.cs`, `TempMapAuditShots.cs` đã xoá; compile sạch.
- Skill đã đọc: `unity-skills`. Chưa có reviewer độc lập.
