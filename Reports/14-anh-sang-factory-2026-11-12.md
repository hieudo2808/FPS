# Sửa ánh sáng hangar Factory – 2026-11-12/13

Phê duyệt của người dùng, theo thứ tự:
1. "Duyệt, làm ánh sáng" sau báo cáo `Nghien-Cuu-5-Hang-Muc-2026-11-12.md`.
2. Bác hướng đèn trần: "đừng thêm đèn kiểu đèn điện rồi lửa củi… mà phải là ánh sáng từ trời (Vì trời sáng)".
3. "Cho phép bake", sau đó "Cấp quyền cho bạn" cho các lần bake tiếp theo.
4. Chấp nhận Asylum/Laboratory sáng hơn sau bake: "Còn sáng thế là ok rồi" (lightmap cũ làm từ Unity 2022, dự án nay ở Unity 6.5).

Trạng thái: đã làm xong và đã playtest trong game; **chờ người dùng xem ảnh để nghiệm thu**. Chưa commit.

## Kết quả

Sáu hangar đi vào được của Factory giờ nhìn rõ bên trong, chỉ bằng ánh sáng trời đi qua các ô cửa đang mở.

Tỉ lệ điểm ảnh gần đen (độ sáng < 0.04) và độ sáng trung bình, quality Low, 1920×1080:

| Góc | Hangar | Trước (Edit Mode) | Sau (Edit Mode) | Sau (trong game, camera người chơi) |
|---|---|---|---|---|
| 00 | Hangar_v2_6 tây | 98% / 0.006 | 39% / 0.063 | 29% / 0.098 |
| 01 | Hangar_v2_6 tây | 100% / 0.003 | 84% / 0.023 | 64% / 0.063 |
| 02 | Hangar_v2_7 (1) | 90% / 0.026 | 2% / 0.177 | 3% / 0.190 |
| 03 | Hangar_v2_7 (1) | 99% / 0.009 | 10% / 0.168 | 3% / 0.186 |
| 04 | Hangar_v2_6 đông | 93% / 0.019 | 5% / 0.179 | 5% / 0.194 |
| 05 | Hangar_v2_6 đông | 100% / 0.002 | 43% / 0.105 | 38% / 0.112 |
| 06 | Hangar_v2_7 | 92% / 0.023 | 7% / 0.160 | 5% / 0.179 |
| 07 | Hangar_v2_7 | 75% / 0.034 | 6% / 0.156 | 5% / 0.179 |
| 08 | Hangar_v2_outbuilding1 | 9% / 0.154 | 1% / 0.318 | 2% / 0.312 |
| 09 | Hangar_v2_outbuilding1 | 25% / 0.246 | 0.6% / 0.358 | 2% / 0.350 |

Bản 4K của 4 góc đầu cho số liệu trùng bản 1080p. `[PLAYTEST screenshot]` Ảnh trong `Reports/FactoryLighting-2026-11-12/`: `before-*.png`, `after-*.png` (Edit Mode), `playtest-*.png` (trong game, có HUD và súng).

Hai góc còn tối hơn phần còn lại, cả hai đều là mặt quay lưng lại cửa: góc 01 (đứng sát mặt sau một container trong hangar tây) và góc 05 (ngăn quây bằng container trong hangar đông). Tôi dừng chỉnh ở đây để người dùng đánh giá bằng mắt.

Ambient mà vật thể động (nhân vật, zombie, súng) nhận được `[SCENE LightProbes.GetInterpolatedProbe]`:

| Vị trí | Trước | Sau |
|---|---|---|
| Ngoài trời Factory (điểm spawn) | 0.045 | 0.261 |
| Giữa 5 hangar lớn | 0.001–0.006 | 0.12–0.21 |
| Hangar_v2_outbuilding1 | 0.030 | 0.78 |

## Nguyên nhân gốc

Bốn nguyên nhân chồng lên nhau:

1. **Bake chỉ cho ánh sáng dội 2 lần.** Nắng muốn tới sàn hangar phải đi sân → tường → trần/sàn, tức ít nhất 3 lần dội `[SCENE CampaignLighting.lighting maxBounces=2]`.
2. **Cửa nhỏ so với thể tích.** Mỗi hangar 230–270 m² sàn mà chỉ có 21–54 m² cửa mở, tường nâu sẫm, sân ngay ngoài cửa thường nằm trong bóng râm. Bake đúng vật lý vẫn cho ra bên trong gần đen.
3. **Factory không có Light Probe.** Vật thể động ở Factory nội suy từ probe trong nhà của Asylum/Laboratory, nên ngay cả ngoài trời cũng chỉ nhận ambient 0.045 trong khi bầu trời là (0.16, 0.28, 0.46).
4. **Shader `Mobile/Diffuse`** trên 38 material của bộ asset công nghiệp bỏ qua mọi point light realtime trên vật thể có lightmap. Điều này giải thích vì sao 21 đèn cũ của Factory không chiếu sáng gì, nhưng không phải nguyên nhân của việc thiếu ánh sáng trời.

## Thay đổi đã làm

Tất cả dưới `CampaignWorld/Factory/Map_v2/Light` `[SCENE]`:

1. **`DaylightPortals`: 14 cổng ánh sáng trời** tại đúng 14 ô cửa/cổng đang mở của 6 hangar. Mỗi cổng là một Light kiểu Rectangle, chế độ Baked, màu bầu trời (0.72, 0.84, 1.0), lấp đúng kích thước ô cửa và hướng vào trong. Không có mesh, không có collider, không chạy lúc runtime: kết quả chỉ nằm trong lightmap và light probe.
   - Cường độ theo từng hangar = 6 × (diện tích sàn / diện tích cửa / 5), kẹp trong [0.4, 3]. Thực tế: 2.4 (nhà phụ) đến 15.5 (hangar tây, một cổng cho 272 m²).
   - Độ dội gián tiếp ×3, để mặt khuất sáng lên mà khu gần cửa không chói thêm.
   - Đây là cân chỉnh cho dễ nhìn, không phải mô phỏng vật lý chính xác.
2. **`SkyLightProbes`: 3176 light probe** phủ toàn bộ sàn đi được của Factory (lưới 8 m), dày hơn trong hangar chơi được (lưới 4 m), hai độ cao mỗi điểm.
3. **Directional Light**: độ dội gián tiếp 1 → 3. Chỉ tác động nơi nắng chiếu tới.
4. **`CampaignLighting.lighting`**: số lần dội tối đa 2 → 4. Cài đặt này dùng chung cho cả scene.
5. **Bake lại lightmap GameScene** (6 lần, mỗi lần khoảng 10 phút, Progressive CPU). Ghi đè `Assets/FPS/Scenes/GameScene/` (lightmap, LightingData, reflection probe).
6. **38 material** `Mobile/Diffuse` → `Legacy Shaders/Diffuse`, `_Color` về trắng. Đây là phần còn lại của hướng đèn trần đã bị bác; lệnh hoàn tác bằng git bị hệ thống quyền chặn nên vẫn giữ. Đo ngoại cảnh trước/sau khi đổi shader: không đổi. Danh sách: `materials-switched.csv`.

Code:
- `[CODE Assets/FPS/Features/Missions/Editor/FactoryInteriorLighting.cs]` công cụ Editor, menu `Tools/FPS/Factory Lighting/`: dò hangar chơi được, dò ô cửa mở, đặt probe, đặt cổng ánh sáng, cấu hình độ dội, bake, chẩn đoán, chụp trước/sau.
- `[CODE Assets/Tests/Editor/FactoryDaylightTests.cs]` 6 Editor test.
- `[CODE Assets/Tests/PlayMode/FactoryDaylightPlaytest.cs]` playtest trong game.

Không thêm đèn điện hay lửa. 31 đèn trần của hướng cũ đã gỡ `[SCENE còn 21 point light cũ + Directional]`. Không đụng weapon, audio, AI, save, interactable, quest.

## Phạm vi: vì sao 6 hangar

Khảo sát tìm thấy 17 hangar có sàn NavMesh dưới mái. 11 cái kín hoàn toàn: không ô sàn nào nối tới điểm spawn. 6 cái còn lại có ít nhất một ô sàn nối tới tuyến đi và được chiếu sáng: `Hangar_v2_6` (2), `Hangar_v2_7` (2), `Hangar_v2_7 (1)`, `Hangar_v2_outbuilding1`.

## Tác dụng phụ ngoài hangar

Đo trên 11 góc đối chứng cố định (`control-before-*.png`, `control-after-*.png`), độ sáng trung bình trước → sau:

| Khu | Trước → sau |
|---|---|
| Ngoài trời Factory (4 góc) | 0.307→0.327, 0.279→0.299, 0.284→0.300, 0.317→0.327 |
| Asylum (3 góc) | 0.238→0.267, 0.185→0.206, 0.098→0.257 |
| Laboratory (4 góc) | 0.320→0.371, 0.356→0.407, 0.362→0.364, 0.404→0.443 |

Phần lớn thay đổi ở Asylum/Laboratory xuất hiện ngay từ lần bake đầu, khi chưa đổi cài đặt nào, tức lightmap cũ đã lỗi thời so với scene. Góc Asylum tầng hầm B2 đổi nhiều nhất: tường trước đây tối và loang bóng, giờ sáng đều hơn. Người dùng đã chấp nhận.

## Test (log thật)

```
FPS.Tests.FactoryDaylightTests        job 87ca4ae2  Test run completed: 6/6 passed.
FPS.Tests.FactoryDaylightPlaytest     job 927e5f80  Test run completed: 1/1 passed.   (PlayMode)
FPS.Tests.CampaignPlacementSafetyTests job 8b250d8f Test run completed: 2/2 passed.
FPS.Tests.CampaignFileSceneTests      job fa721abb  Test run completed: 1/1 passed.
FPS.Tests.ApprovedPropSceneTests      job d7ea8293  Test run completed: 9/10 passed, 1 failed.
  ReplacementDevices_HaveReachableBindingsAndNoActivePedestals
  System.InvalidOperationException : Prop lacks a clear reachable reader position: LabPower
```

Sáu Editor test kiểm tra: không có đèn trần và mọi thứ thêm vào đều chỉ dùng khi bake; mỗi ô cửa mở có đúng một cổng lấp kín nó và hướng vào trong; mọi hangar chơi được đều có ô cửa; probe phủ sàn hangar; cài đặt bake cho đủ số lần dội; ambient ngoài trời > 0.15 và giữa mỗi hangar > 0.02.

Playtest: khởi động host thật qua `Tools/FPS/UI/HUD Review/Start Local Host`, chờ campaign vào pha Exploring ở chapter Factory, đặt nhân vật của người chơi tại 10 góc nhìn, đo khung hình Game view. Điều kiện qua: mỗi hangar có ít nhất một góc dưới 50% gần đen và không góc nào trên 90%. Trước khi sửa, mọi góc của các hangar lớn đều 75–100%.

Lỗi `LabPower`: test kiểm tra vị trí đứng đọc của một mục tiêu ở Laboratory bằng physics/NavMesh `[CODE ApprovedPropImplementation.cs:484-503]`. Thay đổi của tôi không thêm collider nào và không đụng Laboratory. `[INFERENCE]` Lỗi có từ trước; **chưa xác nhận được bằng thực nghiệm** vì không chạy được test này trên scene gốc.

## Save gate

Đã chạy `Tools/FPS/UI/Save Authored Scenes` (có `AssetDatabase.SaveAssets()`) sau playtest. GameScene `isDirty == false`. Console không có lỗi biên dịch.

## Skill và quy tắc áp dụng

- `unity-skills`: truy vấn Editor trước, đọc schema trước khi gọi, kiểm tra lại trong Editor sau khi ghi.
- AGENTS.md "No Primitives": không dựng hình bằng code; cổng ánh sáng không có mesh.
- AGENTS.md "Tests & Evidence": có Editor test và playtest kèm log thật, ảnh 1080p và 4K.
- `level-design` không có trong phiên nên không đọc.

## Chưa làm / chưa kiểm chứng

- Chưa thử với client thứ hai. Ánh sáng là dữ liệu tĩnh của scene nên host và client dùng chung, nhưng điều này là suy luận, chưa chạy.
- Chưa đo FPS. Thay đổi không thêm đèn runtime nào; chi phí thêm là 3176 light probe và việc 21 đèn cũ của Factory giờ có thể vẽ theo pixel ở quality Medium trở lên do đổi shader.
- Chỉ chụp ở quality Low. Chưa xem ở các mức khác.
- Bake báo một lỗi console: `'Bake Ambient Probe' job failed with error code: 2`. Probe ngoài trời vẫn cho giá trị hợp lý (0.26) nên tôi chưa điều tra thêm.
- Reflection Probe cho Factory chưa thêm.
- 11 hangar kín không được chiếu sáng; nếu sau này mở cửa cho chúng thì chạy lại menu `1c Apply Daylight Portals` rồi bake.
- Thư mục evidence nặng 142 MB và còn ảnh của hướng đèn trần cũ (`outdoor-*`, `survey.csv`). Xoá sau khi người dùng duyệt xong.
