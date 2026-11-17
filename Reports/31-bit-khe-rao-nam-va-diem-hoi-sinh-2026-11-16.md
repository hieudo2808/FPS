# 31 — Bịt khe rào phía nam và dời điểm hồi sinh (2026-11-16)

Nối tiếp báo cáo 30. Phương án được người dùng duyệt ("Duyệt, làm đi").

Ảnh trước/sau: `Reports/MapFixGapRecovery-2026-11-16/` (xoá sau khi review xong). Ảnh "trước" của `Recovery_Cold`, `Recovery_21`, `Recovery_22` bị mất do công cụ chụp đọc nhầm danh sách cũ — chỉ có ảnh sau.

## 1. Khe rào phía nam (x −51,9 … −42,1, z = −184)

Đã thêm vào `.../StructuralBoundaries/NarrativePerimeter/`:

| Object | Nhân bản từ | Phủ đoạn x |
|---|---|---|
| `SouthGap_HalfWest` | `Static/Concrete_fence_v2_S_half` | −52,28 … −49,45 |
| `SouthGap_Fence` | `NarrativePerimeter/SouthWest_22` (đoạn rào kề bên) | −49,84 … −44,16 |
| `SouthGap_HalfEast` | `Static/Concrete_fence_v2_S_half` | −44,56 … −41,73 |

- `SouthWestBackstop` (collider vô hình) nới từ x −174…−50 thành −174…−45, không còn khe giữa hai backstop.
- **Khác so với phương án:** phương án dùng `Concrete_fence_v2_Gate` ở giữa. Khi chụp cận thì asset này chỉ là khung cổng trống (cổng mẫu ở chỗ khác được chặn bằng 2 khối barie), nên chỗ đó sẽ thành "tường vô hình" nhìn ra khoảng không. Tôi đã xoá khung cổng vừa thêm và thay bằng một đoạn rào đầy đủ giống hai bên. `[PLAYTEST screenshot]` `g04_gate_close_after.png`
- **Ánh sáng:** vật mới không có lightmap, và mép nam nằm ngoài vùng light probe (probe ở đó cho giá trị đen), nên chân rào ban đầu đen kịt. Đã đặt 3 đoạn rào này nhận ánh sáng môi trường của scene (`receiveGI = LightProbes`, `lightProbeUsage = Off`). Giờ chân rào xám, hơi xanh và tối hơn rào cũ một chút; bake lại ánh sáng sẽ khớp hẳn.

**Kiểm chứng** (capsule người chơi, lưới 0,1 m):

| Phép dò | Trước | Sau |
|---|---|---|
| Từ đường (−47, 0.1, −178) tới mép map (−47, 0, −184.7) | REACHED | no |
| Số vị trí bước ra là rơi khỏi map | 40 | 0 |

`[SCENE]`. Ảnh `g01_southgap` và `g02_edge` (trước/sau), `g03_gate_side_after`, `g04_gate_close_after`. `[PLAYTEST screenshot]` (camera Editor, chưa Play).

## 2. Điểm hồi sinh

Kiểm bằng đúng điều kiện của `CampaignPlacement.TryFloor` + `Clear` (NavMesh trong 0,65 m, sàn thuộc đúng chương, thân người r 0,5 m không vướng):

| Điểm | Trước | Sau | Dời |
|---|---|---|---|
| `Recovery_Extraction` | (112, 0.15, 105) — INVALID (trong hộp radio) | (112.5, 0.08, 105.87) — OK | 1,0 m |
| `Recovery_Cold` | (119, 0.76, 58) — INVALID (lơ lửng 0,69 m) | (119.5, 0.08, 58.35) — OK | 0,5 m, hạ xuống sàn |
| `Recovery_21` | (104, 0.08, 217.29) — INVALID | (105.33, 0.08, 217.68) — OK | 0,75 m |
| `Recovery_22` | (148, 0.08, 218.29) — INVALID | (148.25, 0.08, 219.33) — OK | 0,5 m |

Giữ nguyên hướng xoay. Nạp lại scene xác nhận vị trí đã lưu. `[SCENE]`

Lưu ý: `Recovery_21/22` nằm trong phòng ăn và phòng sinh hoạt Asylum, mà NavMesh hai phòng này là đảo riêng — hai điểm này chỉ dùng được cho người chơi đang ở trong chính phòng đó. Nối NavMesh (bake lại) thuộc phạm vi AI, chưa làm.

Không sửa: `Laboratory/.../A_Markers/PlayerStart_1` (không component nào tham chiếu), 11 anchor zombie (phạm vi AI).

## Tuân thủ và giới hạn

- Save Gate: scene sạch trước mỗi bước, đã lưu sau mỗi bước, `isDirty=false` sau khi nạp lại.
- Script tạm (`TempGapRecoveryFix.cs`, `TempLightInfo.cs`, `TempFineReach.cs`, `TempMapAuditShots.cs`, `TempRecoveryProbe.cs`) đã xoá; compile sạch.
- Để lấy danh sách điểm hồi sinh của từng chương, tôi đọc thẳng file `GameScene.unity` vì công cụ Editor không trả được mảng tham chiếu (khác với quy tắc "không đọc YAML khi Editor trả lời được").
- Edit mode, chưa Play, chưa host/client. Chưa bake lại ánh sáng hay NavMesh.
- Skill đã đọc: `unity-skills`. Chưa có reviewer độc lập.
