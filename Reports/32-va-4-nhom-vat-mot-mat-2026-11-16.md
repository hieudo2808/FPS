# 32 — Vá 4 nhóm vật còn một mặt (2026-11-16)

Nối tiếp báo cáo 30, mục 3. Người dùng duyệt: "Vá 4 vật một mặt".

Ảnh trước/sau: `Reports/MapFixOneSided-2026-11-16/` (xoá sau khi review xong).

## 1. Đã sửa

| Nhóm | Vật | Cách sửa | Số renderer |
|---|---|---|---|
| Hộp điện | `ColdStorageObjective/SampleVaultControl` (115.3, 1.6, 58) | Dùng lại mesh `Electric_box_v2_TwoSided` đã tạo ở báo cáo 25 | 1 |
| Bàn lab | Mọi `LAB_C3(Clone)` trong `CampaignWorld` | Bản lật đúng chiều cho 10 mesh bị lộn mặt (xem mục 2) | 250 (25 bàn × 10 mesh) |
| Tường chắn | `SouthFreightRetainingEdge/NorthWall_07`, `NorthWall_08`, `Map_v2/Static/UNIConcrete_wall_v1_S_left` | Mesh hai mặt | 3 |
| Mảnh rào cắt | `NarrativePerimeter/North_54` và `North_55` (mesh `CampaignDoorCut_3 1`, `CampaignDoorCut_5 1`), cạnh cổng sang Asylum | Mesh hai mặt | 2 |

Tổng 256 `MeshFilter` đổi mesh. 15 mesh mới (~326 KB) trong `Assets/FPS/Features/World/Generated/TwoSided/`:
- `UNIConcrete_wall_v1_W_tile_1_TwoSided`, `UNIConcrete_wall_v1_S_left_TwoSided`, `CampaignDoorCut_3_1_TwoSided`, `CampaignDoorCut_5_1_TwoSided`
- `LAB_C3_Plane.001_Flipped`, `LAB_C3_Plane.002_Flipped`, `LAB_C3_Cabinet1_Door_Flipped`, và `.001`, `.003`, `.004`, `.006`, `.007`, `.009`, `.010` cùng dạng.

`[SCENE]` Đã nạp lại scene: các renderer vẫn giữ mesh mới, `isDirty=false`.

## 2. Bàn lab: nguyên nhân khác với báo cáo 30

Báo cáo 30 ghi bàn lab "một mặt". Kiểm kỹ thì 10/18 mesh của `Assets/ThirdParty/ApprovedProps/LAB_C3/Models/LAB_C3.fbx` (mặt bàn `Plane.001`, khung dưới `Plane.002`, 8 cánh tủ `Cabinet1_Door*`) là **mesh kín nhưng lộn trong ra ngoài**: thể tích có dấu âm, pháp tuyến cùng chiều với thứ tự đỉnh, tức cả mesh quay mặt vào trong. Nhìn từ ngoài, game vẽ mặt trong phía xa thay cho mặt gần — mặt bàn và cánh tủ trông mỏng, mất cạnh và nhìn xuyên được. 8 mesh còn lại (thân tủ, khung cửa) có thể tích dương, không lỗi. `[SCENE]`

- **Khác so với phương án:** phương án ghi "mesh hai mặt". Với mesh lộn mặt, cách đúng là lật lại (đảo thứ tự đỉnh, đảo pháp tuyến), giữ nguyên số đỉnh, nên tôi làm bản `_Flipped` thay vì nhân đôi mặt.
- Đợt sửa ở báo cáo 29 bỏ sót nhóm này vì phép kiểm "mesh hở cạnh" không bắt được mesh kín bị lộn mặt.
- Số bàn thực tế là **25** (16 bàn trong `LabFitout_02` + 9 bàn tiếp tế `ApprovedTable_*`, 3 mỗi chương), không phải 21 như báo cáo 30 ghi.

## 3. Kiểm chứng

Quét GPU lại đúng 85 góc nhìn tệ nhất mà đợt rà toàn map (báo cáo 30) ghi cho các vật này (mắt 1,6 m, 6 hướng). Đếm số điểm ảnh đang vẽ mặt sau của từng nhóm:

| Nhóm | Trước: lớn nhất / tổng | Sau | Renderer còn lộ mặt sau (≥ 8 px) |
|---|---|---|---|
| Hộp điện | 10.664 / 10.666 | 0 / 0 | 1 → 0 |
| Bàn lab | 3.689 / 120.138 | 0 / 0 | 200 → 0 |
| Tường chắn | 2.729 / 4.447 | 0 / 0 | 3 → 0 |
| Mảnh rào cắt | 505 / 728 | 0 / 0 | 2 → 0 |

`[SCENE]` Ảnh camera Editor (chưa Play) `[PLAYTEST screenshot]`:
- `t01_vaultbox`: trước chỉ thấy một tấm mỏng; sau thấy cả thân hộp.
- `t02_bench_B`, `t04_cabinet_doors`: mặt bàn và cánh tủ có lại cạnh và độ dày.
- `t05_retain`, `t06_wall_S_left_a`: trước nhìn xuyên tường chắn thấy lan can/vật phía sau; sau là mặt tường.
- `t07_fencecut`: hai đầu rào cạnh cổng sang Asylum (hai mảng đỏ trong ảnh so sánh) trước bị khuyết, giờ liền.

## 4. Còn lưu ý

- **Hộp điện `SampleVaultControl`:** asset thiết kế để gắn tường nên mặt sau để hở. Giờ nhìn từ sau sẽ thấy lòng hộp, và nhãn "M0090" ở mặt trong cửa bị ngược chữ (giống 3 hộp đã sửa ở báo cáo 25). Muốn trông thật hơn thì phải dời hộp sát tường, mà đây là vật tương tác của nhiệm vụ Cold Storage nên tôi chưa động — chờ bạn duyệt.
- **Prefab/FBX gốc chưa sửa:** đây là override trên từng bàn trong scene. Nếu đặt thêm `LAB_C3` mới từ prefab thì lỗi lộn mặt sẽ lặp lại. Có thể sửa ở prefab gốc nếu bạn muốn.
- **Ánh sáng:** tường chắn, rào và hộp điện có lightmap; mặt sau mới dùng chung UV lightmap với mặt trước (giống các đợt trước), nên ánh sáng mặt sau không đúng hẳn — trong ảnh `t05` mặt sau tường chắn tối hơn mặt tường bên cạnh. Bake lại sẽ khớp. Bàn lab không dùng lightmap.

## Tuân thủ và giới hạn

- Save Gate: scene sạch trước khi sửa, script sửa tự lưu scene và gọi `AssetDatabase.SaveAssets()`; scene bị đánh dấu bẩn sau khi render kiểm tra (không có sửa đổi thật) nên đã nạp lại để bỏ, `isDirty=false`.
- Script tạm `Assets/Editor/TempFix32.cs` và shader `Assets/Editor/TempAuditID.shader` đã xoá; compile sạch.
- Chỉ đổi mesh hiển thị; không đụng collider, vật tương tác, nhiệm vụ, AI, âm thanh, vũ khí.
- Edit mode, chưa Play, chưa host/client. Chưa bake lại ánh sáng.
- Skill đã đọc: `unity-skills`. Chưa có reviewer độc lập.

## Còn lại từ báo cáo 30 (chưa làm, chờ duyệt)

1. Đặt lại 24 vật phẩm Asylum lọt tường/tủ.
2. Bake lại NavMesh (sân trước Asylum, buồng thang Lab, bỏ NavMesh trên mái).
3. Dời 11 anchor sinh zombie lọt trong vật đặc + 5 anchor chật chỗ.
4. Nâng 6 nhóm mặt phẳng chồng khít 1–2 cm.
5. Bake lại ánh sáng, Play thử host/client, commit.
