# 34 — Bake lại NavMesh và bỏ NavMesh trên mái (2026-11-16/17)

Nối tiếp báo cáo 30, đề xuất 6. Người dùng chọn "Bake NavMesh".

Ảnh: `Reports/MapFixNavMesh-2026-11-17/` (xoá sau khi review xong). Lớp màu xanh là NavMesh, vẽ đè lên ảnh camera Editor.

## 1. Hiện trạng NavMesh trong scene

Scene dùng 3 `NavMeshSurface` có dữ liệu, cùng một loại agent (Humanoid, bán kính 0,5, cao 2, bước 0,75). `[SCENE]`

| Surface | Cách gom | Dữ liệu cũ |
|---|---|---|
| `CampaignSystems/FactoryNavigation` | Khối 380 × 24 × 392 m, collider, voxel 0,1 | `FactoryTraversal/FactoryNavigationFilled.asset` |
| `CampaignSystems/AsylumNavigation` | Khối 79 × 19 × 52 m, render mesh | `MorgueArena/AsylumNavigation 8.asset` |
| `CampaignSystems/LaboratoryNavigation` | **"All"** (gom cả scene), render mesh | `Data/LaboratoryNavigation.asset` (chỉ phủ khu Lab) |

Ngoài ra có 1.118 `NavMeshModifier`, 3 vùng đệm cho đấu trường Tank, 2 `NavMeshLink`, 13 vật cản ở cửa (`CampaignDoor`). 3 surface khác không có dữ liệu.

**Bẫy:** surface Lab đang để "All". Nếu bake theo thiết lập này, nó sẽ sinh NavMesh cho cả Factory và Asylum chồng lên 2 surface kia. Vì vậy tôi bake Lab trong đúng vùng của dữ liệu cũ (tâm (196.49, −17.73, 277.39), kích thước 113.9 × 10.95 × 89.5), rồi trả thiết lập về như cũ.

## 2. Đã làm

1. **Bake lại cả 3 surface**, giữ nguyên mọi thiết lập, ghi vào file mới:
   - `FactoryTraversal/FactoryNavigation_2026-11-16.asset`
   - `MorgueArena/AsylumNavigation_2026-11-16.asset`
   - `Data/LaboratoryNavigation_2026-11-16.asset`

   File cũ giữ nguyên (không còn được tham chiếu) để quay lại khi cần: chỉ việc gán lại vào `Nav Mesh Data` của surface tương ứng.
2. **Bỏ NavMesh trên mái Factory:** thêm `CampaignSystems/FactoryNavigation/RoofExclusions` gồm 147 `NavMeshModifierVolume` (vùng Not Walkable), mỗi cái phủ một đảo NavMesh thoả cả 3 điều kiện:
   - không có đường đi tới từ điểm xuất phát Factory;
   - cao trên 4,5 m;
   - không có điểm gameplay nào trong phạm vi 2 m (anchor, điểm hồi sinh, điểm xuất phát, vật nhiệm vụ, vật phẩm, cửa).

   4 đảo được giữ lại vì nằm gần anchor.

Theo dữ liệu dò đường người chơi của đợt rà toàn map, người chơi không lên được chỗ nào cao trên 4,5 m ở Factory, nên các vùng này chỉ là NavMesh thừa. `[INFERENCE]`: dữ liệu đó dùng lưới thô 0,5 m.

## 3. Kết quả (đo trên scene đã lưu và nạp lại)

| Chỉ số | Trước | Sau |
|---|---|---|
| NavMesh trên mái Factory (mặt thoáng trời, cao hơn mặt đất quanh đó trên 2,5 m) | 18.479 m² | 1.110 m² |
| Tổng diện tích NavMesh | 155.932 m² | 137.148 m² |
| Phủ mặt đất Factory đứng được | 97% | 98% |
| Phủ toàn khu Lab | 72% | 84% |
| Buồng thang Lab: đến / đi | 50% / 63% | 91% / 94% |
| Điểm gameplay có NavMesh | 274/288 | 274/288 |
| Điểm có đường đi trọn vẹn từ điểm xuất phát chương | 207 | 212 |

So từng điểm trong 288 điểm gameplay (anchor sinh zombie, điểm hồi sinh, điểm xuất phát, vật nhiệm vụ, vùng chuẩn bị/lên thang): **không điểm nào xấu đi**. 5 điểm tốt lên:
- 2 anchor zombie `Z03_Utilities/Anchor_11`, `Anchor_12` nay nối được tới khu chính. Mỗi anchor được tính 2 lần (danh sách chương và danh sách Director), nên 2 anchor này chiếm 4 trong 5 điểm.
- Điểm cửa buồng thang Lab đi ra.

`[SCENE]`

Phần mái còn lại (1.110 m²) là mái thấp dưới 4,5 m, nóc container, và mái gần anchor mà tôi cố ý chừa lại.

`[PLAYTEST screenshot]` (camera Editor, chưa Play):
- `nav_roofs_west_*`, `nav_roofs_east_*`: trước, mái gần như mọi nhà đều có NavMesh; sau, mái sạch, mặt đất giữ nguyên.
- `nav_lab_arrival_cabin_*`: sàn Lab cạnh buồng thang trước bị lủng lỗ, sau phủ kín.

## 4. Chưa sửa được bằng bake (đề xuất)

> **Cập nhật 2026-11-17:** mục 4.1 (phòng Asylum tách đảo) đã sửa bằng 21 `NavMeshLink` — xem báo cáo 35.

1. **Các phòng Asylum là đảo riêng** (đường đi từ sảnh tới 15 điểm gameplay trong phòng đều dừng ngay trước cửa phòng). Cửa phòng chỉ hở khoảng 1,2 m, mà agent bán kính 0,5 m nên NavMesh không xuyên qua ô cửa. Bake lại không đổi được. Có 2 cách:
   - đặt `NavMeshLink` qua từng cửa phòng (dự án đã dùng cách này ở `OriginalLobbySeam`); khoảng 12 cửa liên quan tới các điểm gameplay;
   - hoặc giảm bán kính agent khi bake Asylum. Cách này đụng thiết lập AI.
2. **Sân trước Asylum (z ≈ 196–211):** terrain `FactoryAsylumConnection/GroundedCompound` có `NavMeshModifier` "Ignore From Build" được gắn cố ý, nên sân không có NavMesh, chỉ có dải đường ở giữa. Muốn zombie đuổi ra sân thì phải bỏ cờ này và bake lại. Đây là quyết định thiết kế, chưa đụng.
3. **Tầng hầm Asylum** không nối với tầng trệt (đi bằng thang máy), và các cửa `CampaignDoor` được bake ở trạng thái đóng (giống bản cũ). Đúng thiết kế, chỉ ghi nhận.
4. Thiết lập "All" của surface Lab nên đổi thành "Volume" đúng vùng Lab để lần bake sau không bị chồng.

## Tuân thủ và giới hạn

- Save Gate: scene sạch trước khi bake; đã lưu scene và gọi `AssetDatabase.SaveAssets()` sau khi bake; nạp lại scene để đo, `isDirty=false`.
- Giữa chừng Unity Editor đã tắt (thoát bình thường, sau khi scene đã lưu). Tôi mở lại project bằng `E:\Unity\6000.5.6f1\Editor\Unity.exe` để làm tiếp.
- Script tạm `Assets/Editor/TempNav.cs` đã xoá; compile sạch.
- Không đổi agent, AI, cửa, vật tương tác; chỉ thêm vùng Not Walkable và dữ liệu NavMesh mới.
- Edit mode, chưa Play, chưa host/client. Zombie thực tế đi thế nào cần thử khi Play.
- Skill đã đọc: `unity-navmesh`, `unity-skills`. Chưa có reviewer độc lập.

## Còn lại từ báo cáo 30

1. Nối phòng Asylum bằng `NavMeshLink` (mục 4.1 ở trên) — chờ duyệt.
2. Dời 11 anchor sinh zombie lọt trong vật đặc + 5 anchor chật chỗ.
3. Nâng 6 nhóm mặt phẳng chồng khít 1–2 cm.
4. Bake lại ánh sáng, Play thử host/client, commit.
