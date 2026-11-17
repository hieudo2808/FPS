# 39 — Rà lại toàn map sau các đợt sửa 31–38 (2026-11-17)

Sau khi xong các mục của báo cáo 30 (báo cáo 31–38), tôi chạy lại đúng bộ công cụ rà toàn map của báo cáo 30 để xem còn lỗi hoặc có lỗi mới không, và thêm hai phép kiểm mới (mặt chồng khít đo trên tam giác thật cho toàn map; đảo NavMesh). Lỗi tìm thấy trong phạm vi các việc đã duyệt thì sửa luôn; phần còn lại ghi thành đề xuất.

## 1. Kết quả so với báo cáo 30

| Hạng mục | Báo cáo 30 | Bây giờ |
|---|---|---|
| Script / mesh / material thiếu | 0 | 0 (16.522 object) |
| Rơi khỏi map | khe 10 m ở rào nam | **0**. Còn 23 "lỗ nền" của lưới thô, trùng hệt danh sách đã xác minh là không rơi thật |
| Rơi xuống rồi kẹt | 0 (51 cụm thô, đã xác minh) | 0 (cùng 51 cụm) |
| Tường vô hình | 2 backstop sát rào | Như cũ |
| Điểm gameplay hỏng | 54 điểm | **0 điểm tệ đi, 57 điểm tốt lên** (còn lại xem mục 4) |
| Vật lộ mặt sau (quét GPU từ 15.063 chỗ đứng) | 191 vật | 133 vật. Các dấu mới là viền 2 mm của lớp trong mesh hai mặt; render thật chỉ khác 1 điểm ảnh ở mép (báo cáo 38) |
| Va chạm chồng nhau ≥ 0,25 m | 444 cặp | 456 cặp. 12 cặp mới đều ở rào khe nam chồng lên backstop / đoạn bên cạnh; vật tĩnh, vô hại |
| NavMesh | — | **Lỗi mới, đã sửa** (mục 2) |
| Mặt chồng khít (đo mới trên tam giác thật) | — | **Một phần đã sửa** (mục 3) |
| Ánh sáng | lightmap cũ | Đã bake lại (báo cáo 38) |

`[SCENE]` Lưới đi lại 0,5 m, dò capsule người chơi, quét GPU, kiểm 275 điểm gameplay: cùng công cụ và tham số như báo cáo 30.

## 2. NavMesh trên nóc trần Lab (lỗi mới, đã sửa)

**Phát hiện.** Ở báo cáo 29, viên trần Lab (`Floor_01`, quay xuống) được làm hai mặt. Mặt sau quay lên trên, nên khi bake lại NavMesh ở báo cáo 34, Unity coi nóc trần là sàn đi được: 9 đảo, tổng 3.989 m² ở y −14,2 (sàn Lab ở −18). `[SCENE]`

**Hậu quả.** `NavMesh.SamplePosition` ưu tiên polygon nằm ngay phía trên điểm hỏi. Với bán kính 5 m, như `ZombieFactory` (4 m) và `SpecialEscapePlanner` (4 m) đang dùng, 41/78 điểm gameplay Lab bị bắt dính lên nóc trần. Tức là zombie có thể bị đặt lên trần. `[CODE Assets/FPS/Features/Characters/Enemies/Runtime/ZombieFactory.cs:220]` `[SCENE]`
- HUD dẫn đường dùng bán kính 2 m nên vẫn hoạt động. `[CODE Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:349]`

**Sửa.**
- Thêm `CampaignSystems/LaboratoryNavigation/CeilingExclusions`: 9 `NavMeshModifierVolume` vùng Not Walkable, mỗi vùng phủ một đảo, y từ −14,9 tới −13,0.
- Bake lại riêng surface Lab, theo cách của báo cáo 34 (gom trong vùng dữ liệu cũ) → `Data/LaboratoryNavigation_2026-11-17.asset`. File 2026-11-16 giữ lại để quay lại khi cần.

**Kiểm (sau khi lưu và nạp lại scene):**

| Đo | Trước | Sau |
|---|---|---|
| NavMesh trên nóc trần Lab | 3.989 m² | **0** |
| NavMesh sàn Lab | 3.387 m² | 3.387 m² |
| Điểm Lab bị bắt lên trần (bán kính 5 m) | 41/78 | **0/78** |
| Điểm Lab HUD dẫn đường được | 78/78 | 78/78 |

Ở Factory, đảo NavMesh trong vỏ nhà ở trên cao giảm so với trước báo cáo 34 (37 → 20 đảo), nên không có hồi quy.

## 3. Mặt chồng khít — đo lại trên tam giác thật cho toàn map

Báo cáo 30 chỉ dò được tấm mỏng dưới 3 cm, nên bỏ sót vật dày như bệ rào, tường chắn, bó vỉa. Lần này với mọi cặp vật có hộp bao chạm nhau, tôi tìm các tam giác cùng hướng, cách nhau ≤ 2 mm và chồng lên nhau. Sau đó lọc theo việc người chơi có nhìn thấy không (đường nhìn thẳng từ chỗ đứng được, trong 40 m). `[SCENE]`

- Toàn map: 1.151 cặp; 347 cặp người chơi nhìn thấy được.

**Đã sửa (cùng phạm vi "Mặt chồng khít" đã duyệt ở báo cáo 37):**
- **Rào khe nam** (do tôi thêm ở báo cáo 31): bệ các đoạn chồng nhau 2,5–2,8 m², trùng mặt phẳng 0 mm. Khi đi dọc rào, ở góc xiên thấy lẫn hai tấm (3–11% điểm ảnh). Đã dời `SouthGap_HalfWest` / `_Fence` / `_HalfEast` lần lượt 4 / 8 / 2 mm vào trong map và xuống dưới.
- **Rào cổng Asylum** `North_54`, `North_55` (có từ trước): sửa cùng cách (2–4 mm).
- **Chỗ nối các đoạn rào gốc** (`NarrativePerimeter`, `FlightFuelCourt`, rào `Static`…): 326 chỗ nối, mỗi chỗ trùng 1,2–5,9 m². Tôi chia các đoạn thành hai nhóm xen kẽ (như cách cắt lưới ở báo cáo 37) và dời 166 bệ rào thuộc một nhóm 4 mm theo phương ngang mặt rào và 4 mm xuống. Bỏ qua 2 bệ trùng tên, và các cổng thuộc `Gameplay/GatesAndShortcuts`, `Gate_DeconExit` (vật gameplay).
- Kết quả: diện tích trùng mặt phẳng giữa các bệ rào từ **585 m² còn 17 m²**. Phần còn lại là mặt ngang nhỏ 0,03–0,13 m² ở đỉnh chỗ nối (nhiều cái là mặt dưới gờ), bệ rào cắm vào `PavingBase`, và các cổng gameplay.
- Chỉ dời vài mm; không đổi mesh, collider, lightmap. Mặt cách nhau ≥ 2 mm thì không nhấp nháy ở tầm chơi (báo cáo 37).

**Chưa sửa (có từ trước, mức nhẹ):**
- Mặt đứng bó vỉa `Road_set_v1_s_tile_1` trùng với cạnh `PavingBase_*` dọc các con đường Factory: khoảng 200 cặp. Chụp góc nhìn thật (FOV 60, mắt 1,6 m), bó vỉa chỉ có một đường đổi sắc mảnh. `[PLAYTEST screenshot]`
- Mặt ngang `PavingBase` / `Cube` / bậc nền nhà: tấm thắng đổi theo vị trí camera nhưng không lẫn điểm ảnh.
- Thanh bậc thang `WorkshopEntrySteps`, `EastProcessAccess`, `NorthProcessAccess`: mỗi chỗ < 0,1 m².
- Asylum: khung cửa sổ phòng `Surgery` trùng mặt tường ngoài, giấy trên bàn (`Paper`), cánh tủ `MedRack`, `ProjectorCloth`, bồn cầu.
- Tường chắn `SouthFreightRetainingEdge`: các tấm 10 m đặt cách nhau 5 m, chồng một nửa lên nhau, nhưng ở phía người chơi không nhìn thấy.

## 4. Điểm gameplay còn đánh dấu (không phải lỗi mới)

- 22 anchor trong nhóm 26 anchor không dùng của báo cáo 36. Cả 22 đều thuộc danh sách đó.
- 8 vật tương tác có điểm tương tác nằm trong chính mô hình của nó (hộp điện, máy phát, radio, màn hình PC). Có từ trước; người chơi tương tác bằng tia nhắm vào collider.
- `Laboratory/.../PlayerStart_1` vẫn nằm trong tủ, nhưng không component nào dùng nó (báo cáo 30).

## Tuân thủ và giới hạn

- Save Gate: scene sạch trước mỗi thay đổi; script tự lưu scene và gọi `AssetDatabase.SaveAssets()`; nạp lại để kiểm, `isDirty=false`; kết thúc bằng `File/Save Project`.
- Script tạm `TempFullAudit.cs`, `TempFineReach.cs`, `TempAuditID.shader`, `TempLabNav.cs`, `TempBake.cs` đã xoá; compile sạch (0 lỗi).
- Không đổi code, AI, vật tương tác, nhiệm vụ. NavMesh chỉ thêm vùng Not Walkable trên trần Lab và bake lại surface Lab (cùng phạm vi đã duyệt ở báo cáo 34).
- Edit mode, chưa Play, chưa host/client.
- Skill đã đọc: `unity-skills`, `unity-navmesh`. Chưa có reviewer độc lập.

## Còn lại (chờ bạn quyết)

1. ~~`Hangar_v2_1 (2)` quá tối, bên trong có điểm hồi sinh~~ — đã thêm 6 đèn `NeonLamp` có sẵn và bake lại, xem báo cáo 40.
2. ~~26 anchor không dùng~~ — đã xoá, xem báo cáo 41.
3. ~~Mặt chồng khít có từ trước~~ — đã sửa, xem báo cáo 42 (343 → 3 cặp trùng thật nhìn thấy được).
4. Sân trước Asylum không có NavMesh: thiết kế có chủ ý (báo cáo 34).
5. Play thử host/client và commit.
