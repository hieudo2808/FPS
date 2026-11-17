# 25 — Sửa 4 lỗi nhìn xuyên mức nặng (2026-11-15)

Nối tiếp báo cáo 24. Phương án đã được người dùng duyệt ("Duyệt, làm đi") trước khi thực hiện.

Ảnh trước/sau: `Reports/MapFix-2026-11-15/` (11 cặp `*_before.png` / `*_after.png`; xoá sau khi review xong).

## Đã làm

| # | Lỗi | Thay đổi | Kết quả |
|---|---|---|---|
| A1 | `Hangar_v2_1 (2)` tàng hình khi đứng trong | Gán mesh `Hangar_v2_1_TwoSided` cho 1 renderer | Đứng trong thấy đủ tường, mái, cửa cuốn. `[PLAYTEST screenshot]` `hv2_in_W`, `hv2_in_N` |
| A2 | Hai cabin Lab hở ra skybox | Thêm `ConnectorWallLeft`, `ConnectorWallRight`, `ConnectorCeiling` vào `LabArrivalCabin` và `LabExtractionCabin` (nhân bản `BackWall`, material `ServiceWall`) | Không còn thấy trời/biển từ phòng, từ đoạn nối và khi nhìn lên. `[PLAYTEST screenshot]` `lab_arr_W`, `lab_ext_W`, `lab_cabin_S`, `lab_cabin_up` |
| A3 | Hộp điện biến mất từ phía sau | Gán `Electric_box_v2_TwoSided` cho `OBJ_ExtractionRadio/Visual`, `FlightFuelSystem/FuelControl`, `D_POWER SYSTEMS/WallCabinet/Visuals` | Thấy được từ phía sau. `[PLAYTEST screenshot]` `ebox_back`, `fuel_back`, `labcab_back` |
| A4 | Góc bo và vòm cửa Lab một mặt | Gán `Wall_HalfArc_90_02_TwoSided` (24) và `Door_Arch_01_TwoSided` (28) trong `Laboratory/Geometry` | Hết dải mỏng; mặt sau góc bo hiện thành tấm cong. `[PLAYTEST screenshot]` `bf_07` |

Tổng: 56 renderer đổi mesh, 6 object mới, 4 asset mesh mới. `[SCENE]`

## File thay đổi

- `Assets/FPS/Scenes/GameScene.unity`
- `Assets/FPS/Features/World/Generated/TwoSided/` — 4 mesh `.asset` và `.meta`

Không đổi material, shader, collider, script, NavMesh, lightmap.

## Điểm cần người dùng xem bằng mắt

- **A3:** mặt sau hộp điện hiện lại hoạ tiết mặt trước bị lật gương (chữ "M0090" ngược). Hộp trong Lab nhìn từ sau là vỏ rỗng.
- **A4:** nhiều miếng góc bo đang quay lưng vào phòng, nên giờ người chơi thấy một tấm cong tối đứng ở góc thay vì khe hở. Hết nhìn xuyên nhưng chưa chắc đẹp; xem `bf_07_after.png` và `lab_cabin_S_after.png`.
- **A1:** bên trong hangar là hoạ tiết tôn mặt ngoài, chưa phải nội thất thật.
- Mặt sau dùng chung lightmap với mặt trước; chưa bake lại.

## Kiểm chứng và giới hạn

- Ảnh chụp bằng camera Editor ở Edit mode, cùng vị trí và góc trước/sau. Chưa chạy Play, chưa thử host/client.
- Chưa chạy lại phép quét mặt sau toàn map; chỉ kiểm lại 11 góc đã lỗi.
- 6 khối tường cabin có `BoxCollider` (thừa hưởng từ `BackWall`); NavMesh không bake lại. `[INFERENCE]` không ảnh hưởng đường đi vì tường nằm ngoài mép sàn đoạn nối.
- Save Gate: scene sạch trước khi sửa, đã `SaveScene` + `SaveAssets` sau khi sửa, `isDirty=false`; Editor trả về `MainMenu`.
- Script tạm `TempTwoSidedFix.cs`, `TempMapAuditShots.cs` đã xoá; compile sạch.
- Skill đã đọc: `unity-skills` (như báo cáo 24). Chưa có reviewer độc lập.

## Bổ sung: xoay lại 24 góc bo Lab (người dùng yêu cầu "Xoay lại góc bo")

- **Nguyên nhân:** cả 24 `Wall_HalfArc_90_02` đặt lệch 90° so với cách prefab được thiết kế. Tấm cong chạy từ đúng góc phòng chéo ra giữa phòng thay vì nối hai bức tường. `[SCENE]` (đo từ vị trí collider trong prefab và toạ độ đỉnh mesh)
- **Sửa:** với mỗi góc bo, xoay root thêm +90° quanh Y và dời 1,5 m dọc tường, giữ nguyên ô 1,5 × 1,5 m ở góc phòng. 24/24 đã dời, không bỏ sót. `[SCENE]`
- **Kết quả:** góc phòng thành góc bo tròn liền với hai tường, dải màu và nẹp trần khớp nhau. `[PLAYTEST screenshot]` `arcA_room`, `arcA_top`, `arcB_room`, `arc_wide` (trước/sau), `bf_07_after2`, `lab_cabin_S_after2`
- **Còn lại:**
  - Mesh này vốn là góc lồi (mặt trước quay về phía tường), nên phòng đang thấy mặt sau do mesh hai mặt cung cấp; góc bo hơi tối hơn tường vì dùng lightmap của mặt trước. Bake lại sẽ hết.
  - Collider của góc bo dời theo; NavMesh chưa bake lại nên còn phủ khoảng hẹp sau tấm cong. `[INFERENCE]` ảnh hưởng nhỏ.
- Script tạm `TempArcProbe.cs`, `TempMapAuditShots.cs` đã xoá; compile sạch.
- **Save Gate:** scene lưu ngay sau khi dời (`saved=True`, `isDirty=false`). Sau khi xoá script tạm và Unity nạp lại domain, `GameScene` báo `isDirty=true` một lần; tôi không rõ nguyên nhân và đã chuyển scene mà không lưu trạng thái đó. Mở lại `GameScene` để kiểm: góc bo ở vị trí mới, tường cabin còn nguyên, `isDirty=false`. Editor đã trả về `MainMenu`.

## Chưa làm

Các lỗi mức nhẹ, NavMesh xuyên `Hangar_v6_1 (3)`, mép nền Factory, mặt ngoài Asylum (xem báo cáo 24).
