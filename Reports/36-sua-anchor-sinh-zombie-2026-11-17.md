# 36 — Sửa anchor sinh zombie (2026-11-17)

Nối tiếp báo cáo 30, đề xuất 3. Người dùng chọn "Anchor zombie".

## 1. Cách đo

Kiểm từng `DirectorSpawnAnchor` theo phần điều kiện không phụ thuộc vị trí người chơi của `DirectorSpawnService.IsAnchorValid`. `[CODE Assets/FPS/Features/AI/Runtime/DirectorSpawnService.cs:117]`

- Anchor nằm trong danh sách `anchors` của chương (`CampaignMissionController.AllowsAnchor`).
- Zone có cho sinh (Playable) và chứa vị trí sinh.
- Có mặt đất trong 3 m bên dưới.
- Thân zombie trống: capsule r 0,5 m, cao 2 m, đặt cao hơn chân 0,08 m (`CapsuleClear`).
- Có NavMesh trong 2 m, lệch cao không quá 0,75 m.
- Có đường đi trọn vẹn tới khu chính của chương (đo trên NavMesh mới của báo cáo 34–35).

Khoảng cách tới người chơi và tầm nhìn của người chơi thay đổi theo lúc chơi, nên không đo ở đây.

## 2. Hiện trạng trước khi sửa

| Chương | Anchor trong danh sách | Hợp lệ | Hỏng |
|---|---|---|---|
| Factory | 58 | 42 | **16** |
| Asylum | 18 | 18 | 0 (đã ổn sau khi nối link cửa ở báo cáo 35) |
| Laboratory | 22 | 22 | 0 |

**Nguyên nhân 16 anchor Factory hỏng:**
- **11 anchor lún dưới mặt sân 0,13 m** (y = 0,08 trong khi mặt sân/đường ở y ≈ 0,21). Thân zombie chạm vào sân nên Director bỏ qua anchor đó. Báo cáo 30 không bắt được nhóm này vì công cụ khi đó đặt capsule cao hơn.
- **5 anchor nằm trong vật đặc:**
  - `Z04_Logistics/Anchor_03…05` nằm trong bệ móng `Plinth_24_Hangar_v2_1 (3)`;
  - `Z06_Maintenance/Anchor_03` chạm `MaintenanceMachineHall`;
  - `C02_ExtractionLink/Anchor_06` chạm `FlightRadioBuilding`.

## 3. Đã sửa

Mỗi anchor được dời tới chỗ hợp lệ gần nhất trong cùng zone, cách mọi anchor khác trong danh sách ít nhất 2 m. Thử bán kính 6 m trước, chỉ mở rộng tới 16 m cho anchor không tìm được chỗ. Không đổi loại anchor, zone, trọng số hay danh sách chương.

| Anchor | Dời | Ghi chú |
|---|---|---|
| `Z04_Logistics/Anchor_01`, `_02`; `Z06_Maintenance/Anchor_04`, `_05`, `_06`; `Z07_Extraction/Anchor_03`, `_04`; `C01_InsertionLink/Anchor_01`, `_03`, `_04`; `C02_ExtractionLink/Anchor_05` | Nâng 0,13 m | Giữ nguyên vị trí ngang |
| `Z06_Maintenance/Anchor_03` | 0,25 m | Lùi khỏi tường nhà máy |
| `C02_ExtractionLink/Anchor_06` | 0,75 m | Ra khỏi chân nhà radio |
| `Z04_Logistics/Anchor_05` | 1,50 m | Ra đầu bắc bệ móng |
| `Z04_Logistics/Anchor_03` | 1,75 m | Ra đầu nam bệ móng |
| `Z04_Logistics/Anchor_04` | 7,75 m | Bệ móng rộng, không có chỗ gần hơn. Giờ nằm cạnh `Anchor_05` (cách 2,2 m) ở đầu bắc bệ |

## 4. Kiểm chứng (sau khi lưu và nạp lại scene)

| Chương | Hợp lệ trước | Hợp lệ sau |
|---|---|---|
| Factory | 42/58 | **58/58** |
| Asylum | 18/18 | 18/18 |
| Laboratory | 22/22 | 22/22 |

`[SCENE]` Scene sạch sau khi nạp lại. Kết quả dời thật trùng khớp từng dòng với kế hoạch đã chạy thử.

## 5. Anchor không dùng (không sửa)

26 anchor dưới `DirectorSpawnService/Anchors` **không nằm trong danh sách của chương nào**, nên ở chế độ chiến dịch Director không bao giờ dùng tới. Danh sách này có vẻ được lọc có chủ ý: nhiều anchor trong đó nằm trong vật đặc.
- `Z02_FactoryHub/Anchor_06…09`, `Z05_ColdStorage/Anchor_03…06`, `_10`
- `Z04_Logistics/Anchor_06…12`, `Z06_Maintenance/Anchor_07`, `_08`
- `Z03_Utilities/Anchor_05`, `_10`, `C02_ExtractionLink/Anchor_01`, `_02`, `Z07_Extraction/Anchor_12…14`, `_16`

Báo cáo 30 đếm 11 anchor "lọt trong vật đặc" và 5 anchor "chật chỗ". Đính chính: phần lớn số đó là các anchor không dùng này. Các anchor đang dùng mà hỏng thật là 16 cái ở mục 2. Có thể xoá 26 anchor không dùng, hoặc đưa lại vào danh sách sau khi sửa — chờ quyết định.

## Tuân thủ và giới hạn

- Chỉ dời transform của anchor; không đổi code, zone, vật tương tác, NavMesh.
- Save Gate: scene sạch trước khi dời; script tự lưu scene và gọi `AssetDatabase.SaveAssets()`; nạp lại để kiểm, `isDirty=false`.
- Script tạm `Assets/Editor/TempAnchors.cs` đã xoá; compile sạch.
- Edit mode, chưa Play. Cần Play để xem zombie thực sự sinh ra ở các anchor này (khuất tầm nhìn, đúng khoảng cách).
- Skill đã đọc: `unity-navmesh`, `unity-skills`. Chưa có reviewer độc lập.

## Còn lại

1. 26 anchor không dùng (mục 5) — xoá hay sửa rồi đưa vào danh sách.
2. Nâng 6 nhóm mặt phẳng chồng khít 1–2 cm (báo cáo 30).
3. Bake lại ánh sáng, Play thử host/client, commit.
