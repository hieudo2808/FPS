# 38 — Bake lại ánh sáng (2026-11-17)

Nối tiếp báo cáo 30 và 37. Người dùng chọn "Bake ánh sáng".

Ảnh: `Reports/MapFixLighting-2026-11-17/` (18 ảnh; xoá sau khi review xong). Các ảnh chụp bằng camera Editor (near 0,18, như camera người chơi), chưa Play.

## 1. Vì sao phải bake lại

Lightmap cũ được bake trước các đợt sửa từ báo cáo 25 trở đi. Từ đó tới nay đã có nhiều thay đổi hình học: 2.270 vật được chuyển sang mesh hai mặt (vỏ nhà, rào, tấm Lab), rào khe nam, rào khuôn viên Asylum, buồng thang Lab, rào lưới bị cắt. Các vật này hoặc chưa có lightmap, hoặc đang dùng lightmap của vị trí/hình dạng cũ. `[SCENE]`

Thiết lập bake giữ nguyên: `CampaignLighting.lighting`, Progressive CPU, 4 điểm ảnh/m, tối đa 2048, Shadowmask, nén HighQuality, 32/64/32 mẫu, 332 đèn, 46 reflection probe, 29 nhóm light probe. Lần bake thứ 3 tôi có thử tăng số mẫu rồi trả lại đúng giá trị cũ; file thiết lập giống hệt bản gốc. `[SCENE]`

## 2. Chuẩn bị

- Sao lưu lightmap, `LightingData`, reflection probe và scene trước khi bake, chép ra ngoài project (thư mục tạm của phiên làm việc).
- 3 đoạn rào khe nam (báo cáo 31) được đưa về chế độ nhận lightmap như rào cũ.
- 79 mesh hai mặt trước đây dùng chung UV lightmap cho mặt trước và mặt sau. Bake nguyên như vậy thì ánh sáng trong và ngoài nhà ghi đè lên nhau, nên tôi tạo UV lightmap riêng cho từng mặt.

## 3. Sáu lần bake và các lỗi tìm ra

| Lần | Thay đổi | Thời gian | Kết quả |
|---|---|---|---|
| 1 | UV lightmap riêng cho 79 mesh hai mặt | 14 phút | Sàn/trần Lab lộ ô sáng/ô tối giữa các viên; ánh sáng rò vào nhà kín |
| 2 | Tăng gấp đôi độ phân giải lightmap cho 1.400 tấm Lab (`Floor_01`, `Wall_Simple_01`, `Door_*`, `Wall_HalfArc`) | 14 phút | Giảm một phần |
| 3 | Thử tăng số mẫu gấp 4 | 40 phút | Gần như không đổi, nên **đã trả lại số mẫu cũ** |
| 4 | Tạo lại UV lightmap, khoảng cách giữa các mảnh ≥ 3 điểm ảnh | 13 phút | Hết viền tối ở mép viên |
| 5 | Tách lớp trong của mesh hai mặt khỏi lớp ngoài 2 mm, mỗi mặt lùi theo pháp tuyến riêng của nó | 12 phút | Hết rò sáng, nhưng hở khe ở cạnh lồi |
| 6 | Tách 2 mm theo pháp tuyến trung bình tại mỗi đỉnh, lớp trong vẫn kín | 12 phút | **Bản chốt** |

**Lỗi 1 — viền tối ở mép viên.** Khi tạo UV lightmap cho mesh hai mặt, Unity chỉ chừa khoảng cách 0,004 UV giữa mảnh mặt trước và mảnh mặt sau. Với một viên sàn, khoảng đó chỉ bằng 0,2 điểm ảnh lightmap, nên bóng tối của mặt sau lem sang một mép mặt trước. `[SCENE]`
- Đo dọc đường cắt ngang mép hai viên sàn buồng thang: bake 1 có một điểm ảnh tụt từ 0,88 xuống 0,74 ngay tại mép; bản chốt liền mạch.
- Sửa: tạo lại UV với khoảng cách tính theo kích thước nhỏ nhất của mesh đó trong lightmap.

**Lỗi 2 — rò sáng qua tường.** Lớp trong và lớp ngoài của mesh hai mặt trùng khít vị trí. Khi bake, tia kiểm tra bóng đổ xuất phát từ lớp trong lọt qua tường, nên trong nhà kín vẫn có vệt nắng trực tiếp (ảnh `closed_hall_bake1_leak`).
- Sửa: lùi lớp trong vào 2 mm. Bake 5 lùi từng mặt theo pháp tuyến riêng của nó, nên các cạnh lồi bị hở khe nhìn xuyên ra ngoài: chấm sáng dọc mép mái, ảnh `closed_hall_shell1_cracks`. Bake 6 lùi theo pháp tuyến trung bình tại mỗi đỉnh, lớp trong vẫn kín.
- So ảnh render thật giữa lớp trùng khít và lớp đã lùi: chỉ khác đường viền rộng 1 điểm ảnh ở mép vật (dịch 2 mm), không có lỗ. `[PLAYTEST screenshot]`

## 4. Kết quả cuối (bake 6)

| Đo | Trước | Sau |
|---|---|---|
| Lightmap | 5 (2 × 2048, 3 × 256) | 3 × 2048 |
| Vật nhận lightmap chưa được bake | — | **0** / 6.032 |
| Mesh hai mặt có UV lightmap chồng nhau (> 2%) | 79/79 (dùng chung) | **0**/79 |
| Đường nối ở buồng thang Lab (đo trên ảnh, mức 0–255) | 1,0–1,5 | **−1,1…0,7** (bake 1: 6,9–11,7) |
| Chênh sáng ở mép viên, toàn bộ 976 viên sàn/trần Lab (trung vị / p99) | — | 6,3% / 66% (bake 1: 10,9% / 93%) |
| Chấm sáng lọt khe trong nhà kín (ảnh `closed_hall`) | — | 17 (bake 5: 115) |
| Mặt đen kịt mà vẫn nhìn thấy trời (dấu hiệu bake sai) | — | 40/1.399 mặt trong |
| Dung lượng file ánh sáng (lightmap, `LightingData`, probe) | 141 MB | 174 MB |

`[SCENE]` Đo trên lightmap đã bake.
- Chỉ số "chênh sáng ở mép viên" không về 0 được: nó tính cả mép nằm dưới tường giữa hai phòng hoặc dưới bóng đồ đạc, nơi độ sáng chênh là đúng. Hai điểm cách nhau 0,24 m trong cùng một viên cũng đã chênh 3,8% (trung vị).
- Phép đo "mặt đen thấy trời": lấy mẫu ngẫu nhiên các mặt đen của mesh hai mặt, chụp từ mỗi mặt nhìn ra. 97% nằm trong không gian kín thật.

`[PLAYTEST screenshot]` (camera Editor):
- `lab_cabin_*`: sàn buồng thang. Bake 1 có đường nối; bản cuối liền.
- `lab_roomD_*`, `lab_roomE_*`: phòng Lab. Bake 1 lộ ô vuông trên sàn; bản cuối liền.
- `south_gap_*`: bệ rào khe nam trước tối hơn hai bên (38 so với 48–50); giờ đồng đều.
- `asylum_fence_*`: rào khuôn viên Asylum (báo cáo 27) giờ có lightmap riêng. Mặt quay ngược nắng tối hơn trước, đúng hướng nắng.
- `hangar_outside_*`: mặt ngoài nhà không đổi.

## 5. Thay đổi thấy được khi chơi

- **Bên trong nhà kín giờ tối**, đúng với việc hết rò sáng. Các nhà lớn `Hangar_v4/v5/v6` người chơi không vào được, chỉ nhìn thấy bên trong qua khe/cửa sổ.
- **Nhà mở cửa vẫn sáng tự nhiên.** Ví dụ `Hangar_v2_7`, nơi có vật nhiệm vụ `OBJ_LogisticsManifest`, `FILE_FactoryManifest`, `FILE_FactoryProcedure` (`open_hangar_v2_7_final`).
- **`Hangar_v2_1 (2)` rất tối** (độ sáng trung bình ảnh 3/255) trong khi bên trong có điểm hồi sinh `Recovery_Maintenance` và 302 m² sàn đi được. Nhà kín, không có đèn; trước đây sáng là nhờ lỗi rò sáng. Xem đề xuất 1.

## 6. File đã đổi

- `Assets/FPS/Scenes/GameScene/` — `LightingData.asset`, `Lightmap-0…2_*` (bản `Lightmap-3…4` cũ Unity tự xoá), `ReflectionProbe-*.exr`.
- `Assets/FPS/Scenes/GameScene.unity`:
  - 1.400 tấm Lab có `Scale In Lightmap` từ 1 thành 2;
  - 3 đoạn rào khe nam nhận lightmap.
- `Assets/FPS/Features/World/Generated/TwoSided/` — 79 mesh: UV lightmap mới, và lớp trong lùi 2 mm (kín). Thư mục này chưa có trong git; các bản sao từng bước nằm trong thư mục tạm của phiên làm việc.
- `CampaignLighting.lighting` — không đổi.

## Tuân thủ và giới hạn

- Save Gate:
  - scene sạch trước mỗi lần bake;
  - script tự lưu scene và gọi `AssetDatabase.SaveAssets()` sau mỗi thay đổi;
  - có một lần scene báo `isDirty=true` sau khi lưu (lỗi đã biết của scene này). Tôi nạp lại scene và kiểm thấy thay đổi đã được lưu;
  - kết thúc với `isDirty=false`.
- Script tạm `Assets/Editor/TempBake.cs` đã xoá; compile sạch.
- Không đổi đèn, vật tương tác, nhiệm vụ, AI.
- Edit mode, chưa Play. Lightmap trong bản build sẽ được nén (BC6H), dung lượng nhỏ hơn số trên đĩa.
- Skill đã đọc: `unity-skills`. Chưa có reviewer độc lập.

## Đề xuất (chưa làm, chờ duyệt)

1. ~~Thêm đèn trần cho `Hangar_v2_1 (2)`~~ — người dùng chọn "Thêm đèn có sẵn"; đã làm, xem báo cáo 40.
