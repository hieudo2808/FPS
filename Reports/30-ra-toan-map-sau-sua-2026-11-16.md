# 30 — Rà toàn map sau các đợt sửa (2026-11-16)

Yêu cầu: "Rà lại toàn bộ map giúp tôi xem, từng chi tiết một" / "Tiếp tục rà map". Phạm vi: `Assets/FPS/Scenes/GameScene.unity` ở trạng thái hiện tại (đã gồm các sửa ở báo cáo 25, 27, 28, 29). Lượt này **chỉ đọc** — scene không đổi (`isDirty=false` suốt quá trình).

Ảnh: `Reports/MapAudit3-2026-11-16/` (9 ảnh; xoá sau khi review xong).

> **Cập nhật 2026-11-16:** khe rào phía nam và 4 điểm hồi sinh (`Recovery_Extraction`, `Recovery_Cold`, `Recovery_21`, `Recovery_22`) đã sửa — xem báo cáo 31. Đính chính mục 2: code game kiểm từng điểm hồi sinh (`CampaignPlacement.TryFloor` + `Clear`) và bỏ qua điểm không hợp lệ, nên các điểm hỏng là **điểm chết** (người chơi được đưa về điểm khác), không đặt người chơi vào trong vật. `PlayerStart_1` ở Lab không được component nào dùng.
>
> **Cập nhật 2026-11-16 (2):** mục 3 (4 nhóm vật một mặt) đã sửa — xem báo cáo 32. Đính chính mục 3: bàn `LAB_C3` không phải mesh một mặt mà là 10 mesh bị lộn trong ra ngoài; số bàn là 25, không phải 21.
>
> **Cập nhật 2026-11-16 (3):** vật phẩm Asylum (mục 2) đã sửa — xem báo cáo 33. Đính chính: có 3 trạm × 12 = 36 vật phẩm, không phải 24; lỗi gốc là cả 3 bàn đặt xuyên tường/tủ/kệ.
>
> **Cập nhật 2026-11-17:** NavMesh (mục 4) đã bake lại và bỏ NavMesh trên mái — xem báo cáo 34. Đính chính mục 4: "sân trước Asylum (x 105–140, z 205–225)" phần lớn là bên trong toà nhà; sân thật nằm ở z ≈ 196–211 và không có NavMesh vì terrain được gắn cờ loại khỏi bake một cách cố ý. Các phòng Asylum tách đảo vì cửa hẹp hơn bề rộng agent, bake lại không sửa được.
>
> **Cập nhật 2026-11-17 (2):** anchor sinh zombie (mục 2) đã sửa — xem báo cáo 36. Đính chính: phần lớn anchor "lọt trong vật đặc/chật chỗ" ở mục 2 không nằm trong danh sách chương nào (không được dùng). Anchor đang dùng mà hỏng thật là 16 cái ở Factory, chủ yếu do lún 0,13 m dưới mặt sân.
>
> **Cập nhật 2026-11-17 (8):** đã sửa các mặt chồng khít có từ trước còn lại — xem báo cáo 42. Cặp trùng thật mà người chơi nhìn thấy: 343 → 3 (2 cặp thuộc cửa/cổng gameplay, cố ý giữ nguyên).
>
> **Cập nhật 2026-11-17 (7):** đã xoá 26 anchor không thuộc chương nào — xem báo cáo 41. Còn 98 anchor, đều hợp lệ.
>
> **Cập nhật 2026-11-17 (6):** hall `Hangar_v2_1 (2)` (có điểm hồi sinh) đã có 6 đèn `NeonLamp` có sẵn và đã bake lại — xem báo cáo 40.
>
> **Cập nhật 2026-11-17 (5):** đã rà lại toàn map bằng cùng bộ công cụ — xem báo cáo 39. Mọi mục của báo cáo này đã xong. Trong lúc rà có thêm hai lỗi mới và đã sửa: NavMesh trên nóc trần Lab (do bake ở báo cáo 34) và chỗ nối rào trùng mặt phẳng. Còn lại chỉ là các việc chờ quyết định.
>
> **Cập nhật 2026-11-17 (4):** đã bake lại ánh sáng cho mọi thay đổi hình học từ báo cáo 25 — xem báo cáo 38. Trong lúc đó đã sửa hai lỗi ánh sáng của mesh hai mặt: viền tối ở mép viên sàn Lab, và rò sáng vào nhà kín. Bên trong nhà kín giờ tối đúng thực tế; riêng `Hangar_v2_1 (2)` (có điểm hồi sinh) quá tối, chờ duyệt thêm đèn.
>
> **Cập nhật 2026-11-17 (3):** mục 5 (mặt chồng khít) — xem báo cáo 37. Đo lại trên tam giác thật: các nhóm Asylum/sân bay/vạch sơn cách nhau 5–10 mm hoặc không chồng thật, không nhấp nháy ở tầm chơi; lỗi thật là 281 chỗ nối rào lưới trùng mặt phẳng 0 mm (đã sửa).

## Tóm tắt

| Mức | Vấn đề | Số chỗ |
|---|---|---|
| Nặng | Khe hở 10 m ở hàng rào phía nam: đi được ra mép map và rơi khỏi map | 1 |
| Nặng | Điểm xuất hiện / hồi sinh nằm trong vật thể | 3 điểm người chơi, 11 điểm sinh zombie |
| Trung bình | Vật phẩm Asylum đặt lọt trong tường / tủ | 4 cụm (24 vật phẩm) |
| Trung bình | Vật còn một mặt nhìn thấy từ chỗ đứng được | 4 nhóm |
| Trung bình | Sân trước Asylum và hai buồng thang Lab không có NavMesh | ~200 m² + 2 buồng |
| Nhẹ | Mặt phẳng chồng khít có thể nhấp nháy (z-fight) | 6 nhóm |
| Không có | Chỗ rơi xuống rồi kẹt, tường vô hình, shader lỗi, script/mesh/material thiếu, vật nhìn thấy mà đi xuyên | 0 |

## 1. Rơi khỏi map — khe hở hàng rào phía nam (Nặng)

- Hàng rào `NarrativePerimeter` phía nam (z = −184) dừng ở x = −51,9 (`SouthWest_22`) và tiếp tục ở x = −42,1 (`SouthEast_01`). Đường `Road_-5_-18` / `Road_-4_-18` chạy thẳng vào khe này rồi kết thúc ở mép bản sàn.
- Hai tường vô hình `SouthWestBackstop` (x −174…−50) và `SouthEastBackstop` (x −45…205) cũng để hở x −50…−45.
- Dò bằng capsule người chơi: từ đường (−47, 0.1, −178) đi tới mép (−47, 0, −185) được, bước tiếp thì rơi vào khoảng không (40 vị trí rơi). Volume `FactoryFallRecovery` ở y −6 sẽ bắt người rơi, nhưng người chơi vẫn thấy mình rơi khỏi map. `[SCENE]`
- `[PLAYTEST screenshot]` `w01_southgap.png` (khe hở giữa hai đoạn rào, đường dẫn thẳng ra), `v13_edge_s.png` (đứng ở mép nhìn ra: không rào, thấy nền skybox).

## 2. Điểm gameplay đặt sai (Nặng / Trung bình)

Kiểm 308 điểm (anchor sinh zombie, tiếp tế, hồi sinh, xuất phát, vật tương tác): mặt đất bên dưới, có lọt trong collider không, có chỗ đứng cho thân người không, khoảng cách tới NavMesh. `[SCENE]`

**Điểm người chơi:**
- `Laboratory/GameplayMarkers/A_Markers/PlayerStart_1` (153.19, −18, 245.9) nằm trong tủ bàn lab `LAB_C3(Clone)/Cabinet1.001`. `w02_labstart.png`
- `Recovery/RecoveryPoints/Recovery_Extraction` (112, 0.15, 105) nằm trong hộp radio `OBJ_ExtractionRadio/Visual`. `v16_recov_ext.png`
- `Recovery_Cold` (119, 0.76, 58): không đủ chỗ đứng (sát `SampleVaultControl`).
- Asylum: `Recovery_21` (104, 0.08, 217.3) không đủ chỗ đứng, cách NavMesh 3,8 m; `Recovery_22` (148, 0.08, 218.3) lọt trong ghế `DayRoom/ChairSchool (17)`.

**Anchor sinh zombie lọt trong vật đặc (11):**
- Trong bồn dầu: `Z03_Utilities/Anchor_10` (`Oil_tank_v1 (1)`), `Z04_Logistics/Anchor_07` (`Oil_tank_v1 (12)`).
- Trong nhà kín: `Z05_ColdStorage/Anchor_03…06` (`Hangar_v5_4 (1)`), `Anchor_10` (`Hangar_v6_1 (7)`), `Z06_Maintenance/Anchor_07` (`QuarantineHall`), `C02_ExtractionLink/Anchor_06` (`FlightRadioBuilding`).
- Trong bệ móng: `Z04_Logistics/Anchor_03…05` (`Plinth_24_Hangar_v2_1 (3)`).
- Thêm 5 anchor không đủ chỗ đứng: `Z02_FactoryHub/Anchor_06…09`, `Z04_Logistics/Anchor_12`.
- Hậu quả tuỳ code sinh zombie (nếu kéo về NavMesh gần nhất thì có thể ra mái nhà hoặc phía bên kia tường) — chưa kiểm khi Play. `[INFERENCE]`

**Vật phẩm Asylum lọt trong tường / tủ (24 vật):**
- Lầu 2 (117.95–119, 4.59, 232.2–232.4): 4 `FiniteSupply` + 4 `SurvivalSupply_*` lọt trong `ExamRoom_facing`, `Surgery`, `MedRack`. `w04_asy2f_supply.png` (tủ trống, vật phẩm nằm sau vách).
- Tầng hầm (149.95–151.7, −3.06, 234.2–234.4): 4 `FiniteSupply` + 4 `SurvivalSupply_*` lọt trong `SeclusionRoomB_facing`. `v14_supplyb1.png`
- Phòng kho tầng trệt (151.95–153.7, 0.78, 246.8–247.0): 8 vật phẩm không có mặt đất ngay dưới và không có đường NavMesh từ điểm xuất phát.

## 3. Vật còn một mặt (Trung bình)

Quét GPU từ 15.062 vị trí người chơi đứng được (lưới 2,5 m, cao mắt 1,6 m, 6 hướng): 294 vật còn lộ mặt sau. Sau khi bỏ decal và mép đường (vài chục pixel), còn:

| Vật | Vị trí | Ghi chú |
|---|---|---|
| `ExistingMapBindings/ColdStorageObjective/SampleVaultControl` (`Electric_box_v2`) | (115.3, 1.6, 58) | Hộp điện thứ 4 chưa sửa ở báo cáo 25; nhìn từ sau chỉ thấy một tấm mỏng. `v01_vaultbox.png` |
| Bàn lab `LAB_C3(Clone)` (mặt bàn `Plane.001/002`, cánh tủ `Cabinet1_Door*`) | 19 bàn ở Lab, 1 ở Factory (110.8, 0.7, 102), 1 ở Asylum lầu 2 | Nhìn từ dưới/sau thấy xuyên mặt bàn và cánh tủ. `v02_labbench.png` |
| Tường chắn `SouthFreightRetainingEdge/NorthWall_07/08`, `UNIConcrete_wall_v1_S_left` | (−12, 1.3, −62.6), (65, 1.4, 16.4) | Đã nêu ở báo cáo 24, chưa sửa vì nằm ngoài các nhóm đã duyệt. `v03_retain.png` |
| Mảnh rào cắt `CampaignDoorCut_3 1`, `CampaignDoorCut_5 1` (material `Concrete_fence_v2`) | (120.7 / 129.3, 1.3, 182) cạnh cổng sang Asylum | Mesh nằm ở `Campaign/Meshes` nên lọt khỏi đợt 2. |

## 4. NavMesh (Trung bình, phạm vi AI)

- Sân trước Asylum (x 105–140, z 205–225) gần như không có NavMesh: zombie đuổi theo từ Factory không đi qua được sân để vào toà nhà. `[SCENE]`
- Hai buồng thang Lab mới (báo cáo 28) chưa có NavMesh vì chưa bake lại.
- Nhiều vật tương tác/vật phẩm Asylum không có đường NavMesh từ điểm xuất phát nào (ví dụ `AsylumAccess`, `AsylumFuse`, `AsylumTransfer`): có thể NavMesh trong Asylum bị chia đảo. Edit mode không mô phỏng việc cửa khoét NavMesh lúc chạy, nên cần kiểm khi Play. `[INFERENCE]`
- Mái nhà vẫn có NavMesh như báo cáo 24 (chưa bake lại).

## 5. Mặt phẳng chồng khít (Nhẹ, cần nhìn khi Play)

Hai bề mặt cách nhau < 8 mm và chồng lên nhau ≥ 0,25 m² — dễ nhấp nháy khi nhìn xa:
- Sàn lầu 2 Asylum: `HallUpstair_floor` (y 3.813) chồng với sàn `ExamRoom`, `Surgery`, `Office`, `Kitchen`, `Classroom`, `Bathroom` (y 3.806–3.818).
- Trần tầng trệt Asylum: `HallDownstair_roof` (3.594) với trần `KitchenA`, `OfficeA`, `VisitRoom`, `Gatehouse`, `GuardRoom`, `Stock` (3.606).
- Thảm `OfficeA/Carpet` trùng cao độ sàn `OfficeA_floor` (0.032).
- Vạch sơn bãi đáp `LZ_Clearance`, `Helipad_Clearance` (`CenterBar`, `CrossBar`, `H_Left/Right`) trùng cao độ mặt sân (0–0.005).
- Nền `FacilityGround_Visual` (−0.005) nằm dưới các tấm sân bay `CargoApron_*`, `FlightApron_*` (0.005), lệch 1 cm.

Phép đo dùng hộp bao, nên một phần có thể không chồng thật; ảnh tĩnh không thể hiện nhấp nháy.

## 6. Không phát hiện vấn đề

- **Chỗ rơi xuống rồi kẹt:** 0. Lưới thô 0,5 m gắn cờ 49 cụm (13.868 m²); dò lại từng cụm bằng capsule người chơi thì không cụm nào vào được hoặc đều có lối ra (rãnh bê tông Factory đi ra phía tây được).
- **Rơi khỏi map ở chỗ khác:** 25 "lỗ nền" do lưới thô báo đều không có cú rơi thật.
- **Tường vô hình:** chỉ `SouthEastBackstop`/`SouthWestBackstop`, nằm sát trước hàng rào 0,23 m — không đáng kể.
- **Vật nhìn thấy mà đi xuyên được:** chỉ quạt và lỗ thông gió trên cao (không với tới).
- Không có script thiếu, mesh thiếu, material null, shader không hỗ trợ. `[SCENE]`
- Va chạm cắt nhau: 501 cặp (491 trước đây); 16 cặp mới đều do các vật tôi thêm và đều nhỏ (rào miệng sân lún 0,33 m vào lề đường; tường buồng thang trùng góc 0,25 m với tường phòng như mọi khớp module).

## 7. Đính chính về công cụ

Trong lượt này tôi phát hiện công cụ dò capsule dùng ở báo cáo 26–28 có hai lỗi: (1) coi mọi ô sát bậc thềm/lề đường là không đứng được (không tính bước 0,3 m của `CharacterController`), (2) loại mọi cú rơi cao hơn 0,25 m. Đã sửa cả hai, thêm đối chứng dương (đi qua lề đường, rơi 0,55 m, rơi ra mép map đều nhận đúng) rồi chạy lại các kết luận cũ:
- Báo cáo 26, mục B (rút lại 5 chỗ "rơi rồi kẹt" trong Asylum): **vẫn đúng** — không rơi vào được.
- Báo cáo 27 (rào A1): **vẫn đúng** — không ra được sân sau hay ngoài khuôn viên, từ sân trước lẫn rơi từ lầu 1.
- Báo cáo 28 (buồng thang Lab): kết luận cửa đóng chặn / cửa mở vào được không phụ thuộc lỗi này.

## Phương pháp và giới hạn

- Dump 16.343 object; lưới đi lại 0,5 m toàn map (cửa đóng và cửa mở); dò lại ứng viên bằng lưới 0,1 m với capsule đúng `SagePlayer` (r 0,5, cao 2, bước 0,3, nhảy 0,8).
- Quét GPU 15.062 điểm × 6 hướng: đếm mặt sau theo từng vật và pixel "không có gì" (khoảng trống). Terrain không vẽ được trong phép quét này, nên kết quả khoảng trống ở sân Asylum không dùng.
- Edit mode, chưa Play, chưa host/client.
- Tôi tạo các script Editor tạm (`TempFullAudit.cs`, `TempFineReach.cs`, `TempMapAuditShots.cs`, `TempAuditID.shader`) và đã xoá hết; compile sạch, scene không đổi.
- Skill đã đọc: `unity-skills`. Không có skill `level-design`. Chưa có reviewer độc lập.

## Đề xuất (chưa làm, chờ duyệt)

1. Bịt khe rào phía nam bằng 2 đoạn `Concrete_fence_v2_S` có sẵn (cùng cách báo cáo 27) và nối hai backstop.
2. Dời `PlayerStart_1` (Lab), `Recovery_Extraction`, `Recovery_Cold`, `Recovery_21`, `Recovery_22` ra chỗ trống gần nhất.
3. Dời 11 anchor sinh zombie lọt trong vật đặc và 5 anchor chật chỗ ra mặt đất trống (phạm vi AI — cần bạn duyệt).
4. Đặt lại 24 vật phẩm Asylum lên bàn/kệ có thật.
5. Mesh hai mặt cho `SampleVaultControl`, bàn `LAB_C3`, tường chắn, hai mảnh rào cắt.
6. Bake lại NavMesh (sân trước Asylum, buồng thang Lab, bỏ NavMesh trên mái).
7. Nâng các bề mặt chồng khít lên 1–2 cm.
