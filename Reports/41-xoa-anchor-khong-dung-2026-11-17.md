# 41 — Xoá 26 anchor sinh zombie không dùng (2026-11-17)

Nối tiếp báo cáo 36 (mục 5) và 39. Người dùng chọn "Xoá 26 anchor".

## 1. Xác nhận anchor không được dùng

- `DirectorSpawnService.RefreshAnchors`: khi có chương chiến dịch, Director chỉ nhận anchor trong danh sách `anchors` của chương đó. Chỉ khi không có chương nào mới gom toàn bộ anchor con. `[CODE Assets/FPS/Features/AI/Runtime/DirectorSpawnService.cs:49]`
- `IsAnchorValid` còn loại mọi anchor không có trong danh sách chương hiện tại, qua `CampaignMissionController.AllowsAnchor`. `[CODE Assets/FPS/Features/AI/Runtime/DirectorSpawnService.cs:119]` `[CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.cs:393]`
- GameScene luôn có `CampaignMissionController`, nên anchor ngoài danh sách chương không bao giờ được dùng.

Kiểm trong scene trước khi xoá: `[SCENE]`

| Đo | Kết quả |
|---|---|
| Anchor trong scene | 124 |
| Anchor nằm trong danh sách chương (Factory 58, Lab 22, Asylum 18) | 98 |
| Anchor không thuộc chương nào | **26**, đúng danh sách báo cáo 36 |
| Tham chiếu tới 26 anchor này từ bất kỳ component nào khác trong scene | **0** |
| Thành phần của mỗi anchor | chỉ `Transform` + `DirectorSpawnAnchor`, không có object con |

## 2. Đã xoá

26 object dưới `CampaignWorld/Factory/Map_v2/ColdLedger_Extension/Director/DirectorSpawnService/Anchors/`:
- `Z02_FactoryHub/Anchor_06…09`
- `Z03_Utilities/Anchor_05`, `_10`
- `Z04_Logistics/Anchor_06…12`
- `Z05_ColdStorage/Anchor_03…06`, `_10`
- `Z06_Maintenance/Anchor_07`, `_08`
- `Z07_Extraction/Anchor_12…14`, `_16`
- `C02_ExtractionLink/Anchor_01`, `_02`

Không đổi code, danh sách chương, zone, hay các anchor còn lại.

## 3. Kiểm chứng (sau khi lưu và nạp lại scene)

| Đo | Trước | Sau |
|---|---|---|
| Anchor trong scene | 124 | **98** |
| Anchor không thuộc chương nào | 26 | **0** |
| Danh sách chương (Factory / Lab / Asylum) | 58 / 22 / 18 | 58 / 22 / 18, không có phần tử rỗng |
| Anchor hợp lệ theo điều kiện của Director (công cụ báo cáo 36) | 58/58, 22/22, 18/18 | **58/58, 22/22, 18/18** |

`[SCENE]` Phép kiểm hợp lệ chạy trên NavMesh hiện tại, gồm cả NavMesh Lab đã bake lại ở báo cáo 39.

## Tuân thủ và giới hạn

- Người dùng duyệt việc xoá ("Xoá 26 anchor"). Phạm vi AI chỉ đụng tới dữ liệu scene; không đổi code AI.
- Save Gate: scene sạch trước khi xoá; script tự lưu scene và gọi `AssetDatabase.SaveAssets()`; nạp lại để kiểm; kết thúc bằng `File/Save Project`, `isDirty=false`.
- Script tạm `TempAnchorPrune.cs`, `TempAnchors.cs` đã xoá; compile sạch.
- Edit mode, chưa Play. Có thể hoàn tác bằng git (scene chưa commit) hoặc lấy lại từ bản trước của `GameScene.unity`.
- Skill đã đọc: `unity-skills`. Chưa có reviewer độc lập.

## Còn lại (chờ bạn quyết)

1. ~~Mặt chồng khít nhẹ có từ trước~~ — đã sửa (báo cáo 42).
2. Play thử host/client và commit.
