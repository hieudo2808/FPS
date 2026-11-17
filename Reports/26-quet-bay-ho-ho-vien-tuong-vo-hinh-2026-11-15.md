# 26 — Quét thêm: chỗ kẹt, lối thoát khỏi map, hở viền, vật cắt nhau, tường vô hình (2026-11-15)

Phạm vi: `Assets/FPS/Scenes/GameScene.unity`, sau khi đã sửa theo báo cáo 25. Lần này chỉ đọc — scene không đổi (`isDirty=false` trước và sau, `git diff` của scene giữ nguyên so với sau báo cáo 25).

Ảnh: `Reports/MapAudit2-2026-11-15/` (11 ảnh; xoá sau khi review xong).

## Kết luận nhanh

1. **Asylum hở ở góc đông-bắc:** người chơi đi bộ từ sảnh ra ngoài nhà được, từ đó ra bãi đất trống và tới mép terrain.
2. **Bốn chỗ trong Asylum rơi xuống rồi không ra được**, và giếng thang ở tầng hầm hở xuống nóc cabin Lab.
3. **Rãnh bê tông giữa Factory** có hai cầu thang nhưng một cái bị hàng rào chắn ngang, cái còn lại quá hẹp so với người chơi.
4. Khoảng rỗng dưới bục nâng Factory và 140 khe quanh bồn/nhà: **người chơi không lọt** (khe < 0,9 m), không phải lỗi.
5. Tường vô hình: chỉ có `NorthBackstop`. Hở viền: một khe sàn ở hai cabin Lab và các vệt 1–3 pixel giữa module tường Lab.

Mọi kết quả "tới được / kẹt" tính với cửa ở trạng thái Editor và chưa chạy Play.

## Đính chính mục B (2026-11-16): B1–B5 là kết quả giả

Phép dò ban đầu coi một ô lưới "không đứng được" ở tầng trên là lỗ thủng, nên cho capsule rơi xuyên qua khe nối giữa các tấm sàn và qua chân tường. Sau khi thêm bước quét capsule đi xuống thật trong cột đích:

| Mục | Kết quả dò lại |
|---|---|
| B1 trệt → `MachineRoom` | `a_hole_to_machine = no` — không rơi được |
| B2 trệt → `HallUnderground` | `b_ground_to_b1hall = no` |
| B3, B4 lầu 1 → `VisitRoom`, `GuardRoom` | `c_1f_to_visit = no` |
| B5 hầm → nóc cabin Lab | `d_liftshaft = no` |
| Rơi từ lầu 1 ra sau nhà (139.2, 3.81, 245.5) | `from_1F = no` |

**Rút lại B1–B5 và ý "rơi từ lầu 1".** B6 (rãnh bê tông Factory) không đổi: capsule 0,5 m không vào cũng không ra được, tức là hai cầu thang không dùng được chứ không phải bẫy. Mục C, D, E, F và phần "đi vòng quanh nhà" + "góc rào đông-bắc" của A không bị ảnh hưởng (đã dò lại, vẫn đúng). `[SCENE]`

## Đính chính mục A1 (sau khi dò lại bằng capsule 0,5 m, có in đường đi)

Mô tả A1 bên dưới sai một phần. Điểm (137.2, 0, 252) nằm **ngoài** nhà, trên dải sàn sau sảnh; sảnh chính kín (`h_N.png`). Đường ra thật là:

- **Đi vòng quanh nhà từ sân trước.** Hai miệng sân trước ở z≈210–211 (x 110.4–121.7 và x 128.3–139.6) không có rào, nên người chơi đi vòng cánh đông ra sân bên và sân sau. Sân bên/sau không có NavMesh và không có vật thể gameplay. `[SCENE]` `from_spawn_yard … goal(160,0.01,256.6)=REACHED`
- **Rơi từ lầu 1.** Hành lang lầu 1 ở (139.2, 3.81, 245.5) hở mép, rơi xuống dải sàn sau nhà. `[SCENE]` `from_1F`; `[PLAYTEST screenshot]` `u_ext.png`
- **Góc đông-bắc hàng rào khuôn viên hở ~3 m** (rào bắc dừng ở x=186.8, rào đông ở x=190): đây là chỗ ra bãi terrain ngoài (A3). `[SCENE]`

## A. Lối thoát khỏi map và chỗ map bị cắt

| # | Vị trí | Hiện tượng | Bằng chứng |
|---|---|---|---|
| A1 | Asylum, sảnh sau, quanh (137–140, 0, 250–256) | Đứng ở cuối sảnh nhìn về bắc/đông không thấy tường hay trần, chỉ thấy bãi đất và Lab lộ thiên phía xa. Capsule người chơi đi từ (137.2, 0, 252) ra ngoài tới (160, 0, 256.6) không bị chặn. | `[PLAYTEST screenshot]` `t_e_in.png`; `[SCENE]` dò lưới 0,1 m |
| A2 | Mặt ngoài Asylum góc đông-bắc | Nhìn từ ngoài: khối tầng trên lơ lửng, bên dưới hở, thấy cả tầng hầm qua lỗ terrain. | `[PLAYTEST screenshot]` `t_e_out.png` |
| A3 | Terrain `GroundedCompound` (x -15…255, z 180…330) | Từ A1 đi dọc chân rào tới (54, 0.3, 269.8) rồi ra toàn bộ terrain; cả bốn mép terrain không có gì chặn, bước ra là rơi (1.116 ô mép). | `[SCENE]` lưới 0,5 m; `[INFERENCE]` đoạn vòng qua đầu rào chưa dò bằng capsule 0,5 m |
| A4 | Quanh chân Asylum: x≈93 (z 210–243), z≈255.8 (x 100–150), x≈156.8 (z 210–228) | Bản sàn y=0 quanh nhà kết thúc đột ngột, bên dưới không có collider trong 60 m. | `[SCENE]` |

## B. Chỗ rơi xuống rồi kẹt (capsule r=0,5 m, cao 2 m, nhảy 0,8 m)

| # | Từ đâu | Rơi xuống | Lối ra | Bằng chứng |
|---|---|---|---|---|
| B1 | Tầng trệt (135.2, 0, 241.2) | `Basement/MachineRoom` (y -4.9, 25 m²) | Không | `[SCENE]` `a_hole_to_machine=REACHED`, `a_machine_out=no`. Sàn tầng trệt nhìn vẫn liền (`t_a_hole.png`) — có thể sàn thiếu collider. |
| B2 | Tầng trệt (144.8, 0, 227.5) | `Basement/HallUnderground` chân cầu thang (y -3.84, 27 m²) | Không, kể cả lên bằng cầu thang | `[SCENE]` `b_b1hall_out=no` |
| B3 | Lầu 1 (132.2, 3.8, 234.2) | `1stFloor/VisitRoom` (20 m²) | Không | `[SCENE]` `c_visit_out=no` |
| B4 | Lầu 1 (130.8, 3.8, 234.2) | `1stFloor/GuardRoom` (12 m²) | Không | `[SCENE]` `c_guard_out=no` |
| B5 | Hầm Asylum (142.2, -3.8, 240.2) | Nóc `LabArrivalCabin` (y -14.4), rơi 10,6 m | Chưa dò đường xuống sàn Lab | `[SCENE]` `d_liftshaft=REACHED`; `t_d_shaft.png` |
| B6 | Rãnh bê tông Factory (38.9, 0.74, 7.3), 4 × 9,5 m, sâu 1,5 m | — | Không, theo cả hai chiều | `[SCENE]` `pitA_out=no`; `[PLAYTEST screenshot]` `pitA_above.png`, `t_pit_stairs.png` |

Ghi chú:
- B3, B4 và có thể B1 phụ thuộc cửa phòng: nếu cửa mở được lúc chơi thì không kẹt. `[INFERENCE]`
- B6: cầu thang phía bắc bị hàng rào `DirectorSightlineScreens/HubEastScreen_01…03` (z=12) chắn ngang; cầu thang phía tây lòng chỉ ~1,1 m trong khi `CharacterController` rộng 1,0 m. Với capsule đúng 0,5 m thì cũng không vào được rãnh từ sân hay từ bục; lưới thô 0,45 m thì vào được nhưng không ra. Cần thử Play để chốt.
- B2: cầu thang gỗ xuống hầm có đèn tường; chưa rõ thứ gì chặn capsule. Cần thử Play.
- Hố `E_CONTAINMENT/RoomPitGuarded` trong Lab: rào chắn đủ, người chơi không rơi vào được. `[SCENE]`

## C. Không phải lỗi với người chơi

- Dưới bục nâng y=2,1 của Factory (x -41…110, z -123…14) là khoảng rỗng cao 2,1 m xuống `WorldBase_Continuous`; mặt bục chỉ có một mặt nên đứng dưới nhìn lên thấy trời (`c0_under.png`). 143 khe hở dẫn xuống đó (quanh bồn dầu, chân hangar, mép ô bục), nhưng chỉ 3 khe đủ rộng cho capsule 0,45 m, và 3 khe đó chính là B6, cầu thang hầm Asylum và một ô 0,5 m². `[SCENE]` `trap_center.png`
- Vật nhỏ hơn người chơi (đồ rơi, zombie bán kính nhỏ) vẫn có thể lọt. `[INFERENCE]`

## D. Hở viền

- Khe sàn giữa `ArrivalConnector` và sàn `A_RECEPTION` (quanh x≈148, z 240–245): nhìn xuống thấy khoảng trống; tương tự ở `LabExtractionCabin`. `[PLAYTEST screenshot]` `t_labfloor.png`
- Khoảng 30 điểm đứng trong Lab thấy 1–6 pixel "không có gì" giữa các module tường (ví dụ quanh (206, -18, 243), (188, -18, 260), (244, -18, 256), (154.5, -18, 290–297)). Đo ở 96 px/90°, nên là khe rất mảnh. `[SCENE]`
- 7 điểm ở hầm Asylum thấy khe tương tự, lớn nhất quanh (152.6, -3.8, 241.2). `[SCENE]`
- Phép đo này không vẽ được terrain, nên không dùng để kết luận cho ngoài trời.

## E. Vật thể cắt xuyên nhau (độ lún ≥ 0,25 m)

| Vật | Cắt vào | Vị trí | Lún |
|---|---|---|---|
| `Concrete_fence_v2_S` (`HubEastScreen_01`) | cầu thang `UNIConcrete_wall_v1_S_right` | (37, 0.5, 12) | 2,3 m |
| `Concrete_fence_v2_lattice_S` (6 đoạn) | `Hangar_v4` | (134, 1.8, 77.3) | 1,95 m |
| `Concrete_fence_v2_S` + lưới | `Hangar_v6` | (67.5, 2.6–3.9, -59) | 0,76 m |
| `Concrete_fence_v2_S` (8 đoạn) | `FlightWorkshop` | (202, 0.5, 103) | 0,82 m |
| `Concrete_fence_v2_S` + lưới (4) | `Plinth_36_Hangar_v2` | (90.5, 0.5, -84) | 1,1 m |
| `SouthAccessStep` (5 bậc) | `Plinth_3_Hangar_v6` | (57.2, 0.6, -66) | 1,4 m |
| `ServiceShed_ColdLoadingDock_Fo…` | `PavingBase_-1` | (-49, 0.07, 139) | 3,3 m |
| `Bags_on_pallet`, `Generator_v…` | `Hangar_v2_6_inside` | (-17.5, 2.9, -34), (-7.4, 2.9, -30.6), (-2.2, 2.5, -41.5) | 1,8–1,9 m, cần xem bằng mắt |

Ống `Pipes_set_v1_*` đâm vào hangar và bồn (31 cặp) tôi coi là chủ ý. Tổng 491 cặp; bảng trên là các cặp đáng xem. `[SCENE]`

## F. Tường vô hình

- Chỉ `ColliderBackstops/NorthBackstop` chặn người chơi mà trong 0,4 m không có gì nhìn thấy: 416 lần chạm, đều ở phía terrain (z≈184.5, y 3–6), tức là chỉ gặp nếu đã ra ngoài theo A3. `[SCENE]`
- Các backstop khác có hàng rào ngay phía trước. Không tìm thấy tường vô hình nào khác trong vùng đi lại hai chiều.

## G. Phương pháp và giới hạn

- Lưới đi lại 0,5 m toàn map (732.566 ô đứng được), đồ thị có hướng: lên ≤ 0,9 m, rơi tự do nhưng không xuyên sàn; vùng "kẹt" = tới được từ điểm spawn/recovery nhưng không quay lại được.
- Xác minh lại từng ứng viên bằng lưới 0,1 m với capsule đúng của `SagePlayer.prefab` (`m_Radius 0.5`, `m_Height 2`, `m_StepOffset 0.3`; nhảy 0,8 m = `jumpHeight 2 / gravityScale 2.5`). `[CODE PlayerMovement.cs:1022]`
- Quét khoảng trống: 12.431 điểm, 6 hướng, đếm pixel không có hình học.
- Giới hạn:
  - Edit mode, cửa ở trạng thái Editor, chưa Play, chưa host/client.
  - `CharacterController` thật có skin width và trượt mép, nên các ca sát ngưỡng (B2, B6) có thể khác.
  - Không xét nhảy lên vật rồi nhảy tiếp ngoài quy tắc ≤ 0,9 m mỗi bước.
- Tôi lại tạo script Editor tạm (`TempWalkAudit.cs`, `TempVoidAudit.cs`, `TempSlotProbe.cs`, `TempMapAuditShots.cs`, `TempAuditID.shader`); đã xoá hết, compile sạch, Editor trả về `MainMenu`.
- Skill đã đọc: `unity-skills` (như báo cáo 24). Không có skill `level-design`. Chưa có reviewer độc lập.

## H. Đề xuất (chưa làm, chờ duyệt)

1. A1/A2: dựng lại tường, trần và collider góc đông-bắc Asylum; đây là gốc của A3, A4 và phần F.
2. B1–B4: thêm collider sàn/lan can ở các mép rơi, hoặc đảm bảo phòng có lối ra.
3. B5: bịt giếng thang hầm Asylum.
4. B6: dời `HubEastScreen_01…03` khỏi cầu thang và nới cầu thang tây, hoặc lấp rãnh.
5. D: nối kín sàn ở hai cabin Lab.
6. E: dời các đoạn rào đang cắt qua hangar và cầu thang.
