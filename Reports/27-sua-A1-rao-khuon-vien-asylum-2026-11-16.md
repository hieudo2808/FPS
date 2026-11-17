# 27 — Sửa A1: rào khuôn viên Asylum (2026-11-16)

Nối tiếp báo cáo 26. Phương án A1 được người dùng duyệt ("Duyệt, làm đi").

Ảnh trước/sau: `Reports/MapFixA1-2026-11-16/` (5 cặp; xoá sau khi review xong).

## Đã làm

Thêm 5 đoạn rào vào `CampaignWorld/FactoryAsylumConnection/AuthoredCompoundFences`, nhân bản từ đoạn `Concrete_fence_v2_S` có sẵn ở hàng rào bắc (kèm lưới và collider):

| Object | Vị trí (tâm) | Dài | Mục đích |
|---|---|---|---|
| `CourtyardMouthWest_0`, `_1` | (113.34, 1.13, 211), (118.86, 1.13, 211) | 5,67 m mỗi đoạn | Bịt miệng sân trước phía tây |
| `CourtyardMouthEast_0`, `_1` | (131.14, 1.13, 211), (136.66, 1.13, 211) | 5,67 m mỗi đoạn | Bịt miệng sân trước phía đông |
| `NorthEastCornerClosure` | (188.27, 1.13, 268) | 2,93 m (co theo chiều dài) | Bịt góc đông-bắc khuôn viên |

`[SCENE]` log: `saved=True dirtyAfter=False`.

## Kiểm chứng (capsule r=0,5 m, lưới 0,1 m, có quét rơi thật)

| Phép dò | Trước | Sau |
|---|---|---|
| Sân trước → sau nhà (137.2, 0, 252) | REACHED, 130,8 m | no |
| Sân trước → sau nhà (160, 0, 256.6) | REACHED, 108,3 m | no |
| Sân trước → ngoài miệng sân (150, 0, 206) | REACHED, 56,7 m | no |
| Sân sau → ngoài khuôn viên (192.5, 0.3, 271) | REACHED, 46 m | no |
| Hành lang Factory → sân trước → sảnh (125.5, 0, 232) | REACHED | REACHED (không đổi) |

`[SCENE]` Ảnh: `f_top`, `f_eastmouth`, `f_westmouth`, `f_necorner`, `f_from_corridor` `[PLAYTEST screenshot]` (camera Editor, chưa Play).

## Phần không làm so với phương án

- **Việc 2 (bịt mép hở lầu 1) bị huỷ.** Khi đo trong Editor, lầu 1 kín: có tường bắc ở z=247.6 và tường đông ở x=141.25. "Lỗ rơi" là kết quả giả của phép dò cũ (xem đính chính trong báo cáo 26). Dò lại bằng phép đã sửa: `from_1F = no`.

## Điểm cần người dùng biết

- **Sân bên và sân sau Asylum giờ không vào được.** Hai khu này không có NavMesh và không có vật thể gameplay.
- Đoạn `NorthEastCornerClosure` co còn 52% chiều dài nên hoạ tiết hơi bị nén, và 5 đoạn mới chưa có lightmap nên tối hơn rào cũ một chút (`f_necorner_after.png`).
- 5 object mới là bản sao thường, không phải prefab instance.
- `git diff` của `GameScene.unity` hiện là 2.300 dòng thêm / 320 dòng bớt so với commit gần nhất (gồm cả các lần sửa ở báo cáo 25).
- Mặt sau Asylum dang dở, lỗ terrain và mép bản sàn vẫn còn; chỉ là người chơi không tới được nữa.

## Tuân thủ và giới hạn

- Save Gate: scene sạch trước khi sửa, đã `SaveScene` + `SaveAssets`, `isDirty=false` sau khi dọn script.
- Script tạm (`TempFenceFix.cs`, `TempFineReach.cs`, `TempMapAuditShots.cs`, `TempA1Probe.cs`) đã xoá; compile sạch.
- Edit mode, cửa ở trạng thái Editor, chưa Play, chưa host/client. `CharacterController` thật có skin width nên có thể khác ở các khe sát ngưỡng.
- Chỉ dò từ sân trước, hành lang và lầu 1; chưa quét lại toàn map sau khi thêm rào.
- Skill đã đọc: `unity-skills`. Không có skill `level-design`. Chưa có reviewer độc lập.
