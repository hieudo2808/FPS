# 35 — Nối các phòng Asylum bằng NavMeshLink (2026-11-17)

Nối tiếp báo cáo 34, mục 4.1. Người dùng duyệt phương án 21 link (có cả tầng hầm): "Duyệt, làm đi".

## 1. Nguyên nhân

Ô cửa phòng Asylum rộng 1,04 m (cửa đôi 1,88 m). Agent bán kính 0,5 m làm NavMesh co lại ở ô cửa nên không xuyên qua được; mỗi phòng thành một đảo riêng. Người chơi vẫn đi qua được. `[SCENE]`

Hậu quả trước khi sửa:
- Zombie không đuổi người chơi vào phòng.
- Các anchor trong phòng là điểm chết, vì Director chỉ sinh zombie ở chỗ có đường đi trọn vẹn tới người chơi. `[CODE Assets/FPS/Features/AI/Runtime/DirectorSpawnService.cs:213]`

## 2. Đã làm

Thêm `CampaignSystems/AsylumNavigation/DoorLinks` với 21 `NavMeshLink`, mỗi cửa một link:
- Agent type Humanoid, hai chiều, rộng 0, vùng Walkable, `autoUpdate` tắt.
- Dài 2,2 m, mỗi đầu cách tâm ô cửa 1,1 m và đặt khít lên NavMesh hai bên.

| Tầng | Link |
|---|---|
| Tầng trệt (9) | `DoorLink_1stFloor_` DayRoom, Gatehouse, GuardRoom, Stock, OfficeA, DiningRoom, KitchenA, ToiletsManA, ToiletsWomanA |
| Lầu 2 (9) | `DoorLink_2ndFloor_` Classroom, Bathroom, Bedroom, ExamRoom, Kitchen, Office, Surgery, ToiletsMan, ToiletsWoman |
| Tầng hầm (3) | `DoorLink_Basement_` HallUnderground, SeclusionRoomA, SeclusionRoomB |

Trước khi tạo link, mỗi cửa đều được kiểm 3 điều:
- capsule người chơi (r 0,45) quét qua ô cửa không vướng gì;
- hai bên cửa đều có NavMesh;
- hai bên chưa nối với nhau sẵn.

Không nối 2 cửa:
- `KitchenA/Door_V3 (3)` sang KitchenStock: cánh cửa đóng, người chơi cũng không qua được.
- `MachineRoom/Door_V3 (2)`: trong phòng không có NavMesh.

Không bake lại; không đổi agent, AI, cửa hay vật tương tác.

## 3. Kiểm chứng (sau khi lưu và nạp lại scene)

| Đo | Trước | Sau |
|---|---|---|
| Điểm gameplay tầng trệt + lầu 2 có đường đi trọn vẹn từ sảnh `(124.35, 0.09, 230.49)` | 13/43 | 43/43 |
| Điểm gameplay tầng hầm có đường đi trọn vẹn từ chỗ ra thang máy `(144.8, −3.82, 240.49)` | 8/21 | 21/21 |
| Cửa có đường đi ngắn (dưới 6 m) giữa hai bên | 0/21 | 21/21 |

Điểm gameplay gồm anchor, điểm hồi sinh, điểm xuất phát, vật nhiệm vụ và vật phẩm tiếp tế của chương Asylum. `[SCENE]`

Sẽ hoạt động lại:
- 9 anchor sinh zombie: `Finale_Upper_1/2`, `Common_Dining_1/2`, `Common_DayRoom_1/2`, `Common_Upper_1/2`, `Common_Basement_1`.
- 2 điểm hồi sinh `Recovery_21`, `Recovery_22`.
- Zombie tới được chỗ làm `AsylumAccess` và cầu chì phòng kho (`AsylumFuse`), cùng 3 trạm tiếp tế.

Mọi prefab zombie để `autoTraverseOffMeshLink = 1`, và không có code nào can thiệp vào link, nên zombie sẽ tự đi qua link. `[CODE prefab m_AutoTraverseOffMeshLink]` `[INFERENCE]`: chưa thấy tận mắt khi Play.

## 4. Lưu ý gameplay

- Asylum sẽ đông và khó hơn: zombie sinh trong các phòng và đuổi người chơi vào phòng.
- Tank (bán kính 0,8 m) có thể theo người chơi vào phòng cách ly A/B qua cửa 1,04 m và sẽ lẹm vào khung cửa. Đây là lựa chọn đã duyệt, để phòng cách ly B (có trạm tiếp tế) không thành chỗ trốn bất tử.
- Zombie đi qua link theo đường thẳng, có thể trông hơi cứng ở ô cửa; cần xem khi Play.

## Tuân thủ và giới hạn

- Phương án được duyệt trước khi làm. Bản dựng thử để lập phương án chỉ nằm trong bộ nhớ, không lưu.
- Save Gate: scene sạch trước khi tạo link; script tạo link tự lưu scene và gọi `AssetDatabase.SaveAssets()`; nạp lại để đo, `isDirty=false`.
- Script tạm `Assets/Editor/TempNavLinks.cs` đã xoá; compile sạch.
- Edit mode, chưa Play, chưa host/client.
- Skill đã đọc: `unity-navmesh`, `unity-skills`. Chưa có reviewer độc lập.

## Còn lại

1. Dời 11 anchor sinh zombie lọt trong vật đặc + 5 anchor chật chỗ (báo cáo 30).
2. Nâng 6 nhóm mặt phẳng chồng khít 1–2 cm.
3. Sân trước Asylum không có NavMesh (terrain bị loại khỏi bake có chủ ý) — quyết định thiết kế.
4. Bake lại ánh sáng, Play thử host/client, commit.
