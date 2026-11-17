# 24 — Trace map: kiến trúc và vật thể nhìn xuyên một phía (2026-11-15)

Phạm vi: `Assets/FPS/Scenes/GameScene.unity` (Factory, FactoryAsylumConnection, Asylum, Laboratory). Chỉ đọc — không sửa scene, prefab, material nào.

Ảnh bằng chứng: `Reports/MapTrace-2026-11-15/` (17 ảnh; xoá sau khi review xong theo quy định).

## Kết luận nhanh

1. **Nguyên nhân "nhìn xuyên từ một phía" là hình học một mặt, không phải material trong suốt.** Toàn bộ shader trong scene là Built-in (`Standard`, `Legacy Shaders/Diffuse`, `Cutout/Diffuse`) và đều cull mặt sau. Mesh nào chỉ dựng một mặt thì nhìn từ phía sau sẽ biến mất.
2. Có **4 lỗi lớn** người chơi nhìn thấy được: hangar tàng hình khi đứng bên trong, hai cabin thang máy Lab hở ra skybox, hộp điện mục tiêu biến mất từ phía sau, góc bo tường Lab lộ dải mỏng.
3. Về kiến trúc: Factory và Lab nhìn chung hợp lý; **Asylum nhìn từ ngoài còn ở mức blockout** và mép nền Factory lơ lửng trên skybox.
4. NavMesh có **54.600 m² (35%) không tới được**, và đi xuyên qua ít nhất một hangar đặc.

## A. Vật thể nhìn xuyên một phía

Cách đo: render từ 12.431 điểm trên NavMesh tới được (cao mắt 1,6 m, 6 hướng), đếm pixel mà mặt sau của một renderer nằm gần camera nhất. 493/6.962 renderer bị gắn cờ; 486 vị trí nhìn đã kiểm tra là người chơi đứng vừa và có đường đi không bị collider chặn.

### Mức nặng

| # | Vật thể | Vị trí | Hiện tượng | Bằng chứng |
|---|---|---|---|---|
| A1 | `Factory/Map_v2/Static/Hangar_v2_1 (2)` | (-24, 2.1, -70) | Đứng trong hangar (sàn y=2,1, có 35 điểm đứng được, cách một `MapRecoveryPoint` 3,5 m) thì tường, mái đều biến mất, chỉ còn khung cửa lơ lửng. Nhìn từ ngoài thì bình thường. | `[PLAYTEST screenshot]` `hv2_in_W.png`, `hv2_out.png` (ảnh Editor, chưa chạy Play) |
| A2 | `Laboratory/LabArrivalCabin` và `LabExtractionCabin` | (145.7, -18, 242.9) và (151.7, -18, 302.9) | Quanh cabin thang máy có khe hở, nhìn thấy biển/trời của skybox dù đang ở độ sâu -18 m. Trong cabin nhìn lên thấy đáy tầng hầm Asylum và trời. | `[PLAYTEST screenshot]` `lab_arr_W.png`, `lab_ext_W.png`, `lab_cabin_S.png`, `lab_cabin_up.png` |
| A3 | `Objectives/OBJ_ExtractionRadio/Visual` (mesh `Electric_box_v2`) | (112, 1, 105) | Hộp điện là mesh treo tường (hở lưng) nhưng đặt đứng giữa sân. Từ phía bắc hộp biến mất hoàn toàn. Cùng lỗi: `FlightFuelSystem/FuelControl` (155, 1, 82) và `D_POWER SYSTEMS/WallCabinet/Visuals` trong Lab. | `[PLAYTEST screenshot]` `ebox_back.png` so với `ebox_front.png` |
| A4 | `Wall_HalfArc_90_02` (24 cái) và `Door_Arch_01` (23 cái) trong `Laboratory/Geometry` | khắp Lab | Miếng bo góc và vòm cửa chỉ có một mặt; ở góc lồi lộ ra dải cong mỏng và khe tối. | `[PLAYTEST screenshot]` `bf_07.png` |

### Mức nhẹ

- `SouthFreightRetainingEdge/NorthWall_07`, `NorthWall_08`, `SouthWall_02` (`UNIConcrete_wall_v1_W_tile_1`): tường chắn một mặt, người chơi lọt được vào phía sau quanh (-11, 0, -62). `[SCENE]`
- `Concrete_fence_v1_wall_set_v2` (5 cái), `Concrete_fence_v2_S` (27 cái, gồm `DirectorSightlineScreens/HubEastScreen_01` và `NarrativePerimeter`): mặt sau lộ ở đầu hồi và khi đứng trên bục cao. `[SCENE]`
- `Hangar_v3_outbuilding_2` (-20, 4, -79): lộ mặt sau khi đứng trong `Hangar_v2_1 (2)`. `[SCENE]`
- Bàn lab `LAB_C3(Clone)`: `Plane.001`, `Plane.002`, cánh tủ `Cabinet1_Door*` chỉ có một mặt, thấy được khi đi vòng ra sau bàn. `[SCENE]`
- `Asylum/Decals_All/Decals_2`, `Decals_3` (44 cái): decal phẳng, lộ lưng ở hành lang tầng hầm và sảnh. Ít gây chú ý vì nền trong suốt. `[SCENE]`
- `Geometry/*/CeilingFascia` cạnh hai cabin Lab: lộ mặt sau khi nhìn lên. `[SCENE]`

### Không phải lỗi

- Hàng rào lưới `Lattice_v1` (398 renderer, Cutout): mesh đã dựng hai mặt. `[SCENE]`
- Các mesh `*_ExteriorFaces` của Asylum: phần lớn đã dựng hai mặt; chỉ `HallDownstair_ExteriorFaces` còn lộ khi nhìn lên từ cầu thang hầm. `[SCENE]`
- Kính (`Source_Glass`, `Lab_Glass`, `glass`…): Standard Transparent, cull mặt sau, nhưng không bị gắn cờ từ điểm đứng được.

## B. Kiến trúc và tính hợp lý

### Factory (380 × 370 m, nền y=0, các bục 1 m và 2 m)

- **Mép nền lơ lửng.** Nền là một tấm phẳng, ngoài hàng rào không có đất; nhìn qua lưới ở mép đông/nam/tây thấy trời và biển phía dưới. Chỉ phía bắc có terrain `GroundedCompound`. `[PLAYTEST screenshot]` `perim_NE.png`, `fac_bird_SW.png`
- **Đường nội bộ cụt.** Nhiều đoạn đường kết thúc vào hông nhà hoặc giữa sân (đoạn chéo quanh (-60, -70), nhánh chữ L quanh (-40, -130), vòng chữ U quanh hai hangar trắng giữa map chỉ nối ra một nhánh). Phần lớn hangar không có đường xe dẫn tới cửa cuốn. `[PLAYTEST screenshot]` `fac_top_all.png` `[INFERENCE]`
- **NavMesh xuyên nhà đặc.** `Hangar_v6_1 (3)` (100, 6, -103) có `BoxCollider` đặc phủ kín, nhưng 63 điểm NavMesh nối với đảo chính nằm bên trong. Zombie có thể tìm đường xuyên nhà rồi kẹt. `[SCENE]` `[INFERENCE]` (chưa chạy Play)
- **NavMesh thừa.** 451 đảo, 54.625 m² không tới được: mái hangar (y≈11–12,6) và ruột các hangar kín. Chỉ 10 đảo, 101.309 m² tới được. `[SCENE]`
- **Cầu thang.** Năm cụm bậc đạt chuẩn (cao 16–17,5 cm, mặt 30–49 cm). Riêng `03_Vertical_Access/SouthAccessStep_*` cao 25 cm, mặt 80 cm — quá cao cho bậc đi bộ. `[SCENE]`
- **Dốc.** `Road_set_v1_s_road_up_2m_LD` dốc 11,3°, vượt mức dốc xe tải thông thường (~8°); các dốc khác 3–7° ổn. `[SCENE]`
- **Tường vô hình.** Sáu `ColliderBackstops` cao 6 m quanh chu vi trong khi hàng rào nhìn thấy cao ~2,6 m. `[SCENE]`
- `Fan_v1_1_bb`/`_frame` (7 cặp) không có collider. `[SCENE]`

### Asylum (73 × 47 m; hầm -4, trệt 0, lầu +4, mái +7)

- **Mặt ngoài còn như blockout.** Tường trắng trơn, mái phẳng không lan can; cánh đông không có cửa sổ ở mặt nam trong khi cánh tây có; mặt đông/bắc có vệt đen lightmap và khe hở chân tường. `[PLAYTEST screenshot]` `asy_ext_S.png`, `asy_eastgap.png`
- **Khu đất trống.** Terrain phẳng một màu, không đường, cây hay công trình phụ; chỉ có lối rào dẫn từ Factory. `[PLAYTEST screenshot]` `to_asylum.png`
- Nội thất sảnh, cầu thang hầm, chiều cao tầng 4 m: hợp lý. `[PLAYTEST screenshot]` `asy_plan_G.png`

### Laboratory (112 × 88 m, sàn -18, trần -14,25)

- Bố cục 6 phòng A–F nối hành lang có vòng lặp, trần 3,75 m: hợp lý. `[PLAYTEST screenshot]` `lab_plan.png`
- Sàn và trần là mặt phẳng dày 0, không có khối kết cấu phía trên; trần không có collider. Chỉ lộ ra qua lỗi A2. `[SCENE]`
- Đảo NavMesh 389 m² quanh (242, -18, 307) sau `DoorDoubleLocked` không tới được khi cửa đóng (có thể chủ ý). `[SCENE]`

### Không phát hiện

- Không có renderer trùng lặp chồng khít (z-fight). `[SCENE]`
- Chỉ 3 vật không chạm gì: `ColdStorageObjective/SampleVaultControl` (115, 1.6, 58) và `CeilingFixture` trong hai cabin Lab. `[SCENE]`

## C. Đề xuất (chưa làm, chờ duyệt)

1. A1: thêm vỏ trong cho `Hangar_v2_1 (2)` hoặc dùng bản hangar có `Hangars_interior_v1`.
2. A2: bịt giếng thang quanh hai cabin Lab (tường + trần).
3. A3: thay `Electric_box_v2` đứng tự do bằng bản có lưng, hoặc áp sát tường.
4. A4: thay góc bo/vòm cửa một mặt ở các góc lồi.
5. Bake lại NavMesh để loại mái, ruột hangar kín và `Hangar_v6_1 (3)`.
6. Thêm đất/địa hình ngoài mép Factory; hoàn thiện mặt ngoài Asylum.

## D. Phương pháp, giới hạn, tuân thủ

- Skill đã đọc: `unity-skills` (`SKILL.md`, module `scene`, `perception`). Quy tắc áp dụng: First Contact Checklist (`/health` → `surfaceProfile=full`, mode `bypass`), đọc module doc trước khi gọi skill. **Không có skill `level-design`** trong máy nên phần kiến trúc dựa trên số đo và ảnh, không theo skill đó.
- Scene sạch trước và sau (`isDirty=false`): MainMenu lúc bắt đầu, GameScene trong lúc đo, MainMenu khi kết thúc. Không gọi save vì không có thay đổi.
- **Sai lệch so với "chỉ đọc":** tôi đã tạo 4 script Editor và 1 shader tạm trong `Assets/Editor/` (`TempMapAudit*.cs`, `TempAuditID.shader`) để dump dữ liệu và render; đã xoá hết, compile sạch, `git status Assets/Editor` không còn gì.
- Giới hạn:
  - Đo ở Edit mode, chưa Play, chưa thử host/client.
  - Chỉ một độ cao mắt 1,6 m, không xét nhảy/ngồi; ngưỡng ≥12 pixel ở 192×192 mỗi mặt.
  - "Tới được" tính theo NavMesh với cửa ở trạng thái Editor.
  - Nhận định về đường cụt, mặt ngoài Asylum là đánh giá bằng mắt từ ảnh.
- Chưa có reviewer độc lập.
