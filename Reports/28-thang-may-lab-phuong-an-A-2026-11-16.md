# 28 — Dựng lại hai cabin thang máy Lab theo phương án A (2026-11-16)

Người dùng nhận xét cabin Lab "không có lót đáy, màu không hợp lý, không nhìn ra thang máy", sau đó duyệt phương án A ("Duyệt A, làm đi"): thang hàng dựng bằng bộ module của Lab.

Ảnh trước/sau: `Reports/MapFixLift-2026-11-16/` (7 cặp + 1 ảnh biển; xoá sau khi review xong).

## Đã làm (giống nhau cho `LabArrivalCabin` và `LabExtractionCabin`)

| Phần | Thay đổi |
|---|---|
| Cửa | `CabinDoor` dời ra mặt tường phòng (x=147.19 / 153.19). Thêm 2 `Door_Arch_01` và cụm `CampaignLeaf` (2 `Door_Left_01` + `PhysicalGate`), nhân bản từ `Maintenance/DoorDoubleLocked`. |
| Buồng | Nhóm mới `LabStyleCar` 6 × 6 m: 4 `Floor3x3`, 4 `Ceiling3x3`, 6 `Wall_Simple_01`, 6 `CeilingFascia`, 1 đèn trần — đều nhân bản từ `A_RECEPTION`. |
| Biển | ~~`DoorPlaque_LIFT` cạnh cửa~~ — **đã xoá** theo yêu cầu người dùng (xem "Cập nhật" bên dưới). |
| Khối cũ | Tắt (không xoá): `CabinFloor`, `BackWall`, `LeftWall`, `RightWall`, `CabinRoof`, `CeilingFixture`, `DoorLeaf`, `LeftJamb`, `RightJamb`, `ArrivalConnector`, `ExtractionConnector`, 2 dải tường 0,5 m và 2 nẹp trần ở mỗi ô cửa. |
| Xoá | 6 tấm `ConnectorWall*` / `ConnectorCeiling` tôi thêm ở báo cáo 25. |

`[SCENE]` log: mỗi cabin 21 mảnh buồng, `saved=True dirtyAfter=False`.

### Gameplay

- `CampaignDoor.leaf` → `CampaignLeaf`; `closedLocalPosition (0,0,0)`; `openOffset (0,4,0)` (lấy từ `DoorDoubleLocked`). `rule` giữ nguyên: `LabArrival` và `Manual`. `[SCENE]` đọc lại sau khi nạp scene.
- `Safety`: 5,5 × 3 × 1 m; `NavigationBlocker`: 5,5 × 3 × 0,5 m.
- **Không phải dời vùng `Boarding` và 4 điểm `CampaignArrivals`**: buồng mới rộng hơn và vẫn bao trọn chúng (khác với phương án đã trình, vốn dự kiến dời ~0,4 m).

### Sửa phát sinh: ô sàn bị khoét

Ô sàn tôi lấy làm mẫu và các ô sàn phòng ngay trước cửa dùng mesh `CampaignDoorCut_*` — sàn đã bị khoét lỗ từ trước cho tấm sàn tối cũ đè lên. Tắt tấm cũ làm lộ lỗ. Đã gán lại mesh `Floor_01` nguyên vẹn cho 12 ô: 8 ô trong hai buồng, 2 ô trước cửa `A_RECEPTION`, 2 ô trước cửa `F_TRANSFER BAY`. `[SCENE]`

## Cập nhật: bỏ biển (người dùng yêu cầu)

Người dùng xem `L_arr_sign_after.png` và yêu cầu bỏ: người chơi không quan tâm, biển trông lơ lửng, và không được tự làm biển — mọi thứ phải dùng asset thật; nếu thật cần đề tên thì đặt trên cửa hoặc trên tường phía trên/cạnh cửa.

- Đã xoá `DoorPlaque_LIFT` ở cả hai cabin và lưu scene; `GameScene.unity` không còn chuỗi `DoorPlaque_LIFT`. `[SCENE]`
- Ảnh `L_arr_sign_after.png` và tấm biển trong các ảnh `*_after.png` khác là trạng thái trước khi xoá.
- Trong Lab còn 10 biển cùng kiểu (khối + chữ) có từ trước: `DoorPlaque_A…F`, `DoorPlaque_C-03_ISOLATED`, 3 `DoorPlaque_SUPPLIES`. **Đã xoá cả 10** sau khi người dùng trả lời "Xoá luôn"; scene đã lưu, không còn object `DoorPlaque*`, `WallMountedPlate`, `PrintedLabel`, `RoomCodeBand` nào. `[SCENE]` Xoá hẳn, không phải tắt; khôi phục bằng `git` nếu cần.

## Kiểm chứng

| Phép dò (capsule r=0,5 m) | Kết quả |
|---|---|
| Cửa đóng: phòng → trong buồng (cả hai cabin) | no (8/8 điểm) |
| Cửa ẩn tạm như đang mở: phòng → 4 điểm trong buồng | REACHED (8/8), 8,3–12,8 m |

`[SCENE]`. Ảnh `[PLAYTEST screenshot]` (camera Editor, chưa Play): `L_arr_front`, `L_arr_oblique`, `L_arr_inside_E`, `L_arr_inside_W`, `L_ext_front`, `L_ext_oblique`, `L_ext_inside_E`, `L_arr_sign`.

## Chưa kiểm / còn tồn

- **Chưa Play, chưa thử host/client.** Chưa xác nhận: cửa trượt lên đúng lúc, chuyển chương đặt người chơi đúng trong buồng, và cửa `Manual` của cabin đi còn bấm tương tác được với cánh mới. `[INFERENCE]` tương tác vẫn hoạt động vì collider cánh nằm dưới `CabinDoor`.
- **Chưa có lightmap** cho các mảnh mới và 4 ô sàn trước cửa: có vệt tối trên sàn trước cửa (`L_arr_front_after.png`, `L_ext_oblique_after.png`). Bake lại sẽ hết.
- **NavMesh chưa bake lại**: buồng rộng hơn buồng cũ nên rìa buồng chưa có NavMesh.
- Buồng ở đầu Asylum vẫn là buồng nhỏ `Elevator`; hai đầu khác nhau như đã nêu khi duyệt.
- Cửa trượt lên nguyên khối, không tách hai cánh.
- `GameScene` báo `isDirty=true` sau khi chụp ảnh hoặc sau khi Unity nạp lại domain, dù không ai sửa; tôi nạp lại scene để bỏ trạng thái đó. Trạng thái trên đĩa đã kiểm lại là đúng.

## Tuân thủ

- Quy tắc "No Primitives": buồng và cửa mới chỉ dùng module có sẵn của bộ Lab; các khối `Cube` cũ đã tắt.
- Save Gate: lưu sau mỗi bước sửa; `isDirty=false` khi kết thúc; Editor đang mở `GameScene`.
- Script tạm (`TempLiftFix.cs`, `TempFineReach.cs`, `TempMapAuditShots.cs`) đã xoá; compile sạch.
- Bản sao scene trước khi sửa: `C:\Users\hieud\AppData\Local\Temp\claude\8eb716aa96ff7eab\e3762ba4-2842-495e-8a8e-4cb9b06d362a\scratchpad\audit\GameScene.before_lift.unity` (thư mục tạm, có thể bị dọn).
- Skill đã đọc: `unity-skills`. Chưa có reviewer độc lập.
