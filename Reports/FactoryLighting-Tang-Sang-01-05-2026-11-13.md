# Tăng sáng hai góc khuất Factory — 13/11/2026

Đã tăng ánh sáng trời dội tới góc 01 và 05, bake lại và kiểm chứng bằng camera người chơi. Scene đã lưu; chưa commit.

## Kết quả

[PLAYTEST screenshot] Cùng 10 vị trí camera, quality Low, 1920×1080. Điểm ảnh gần đen dùng ngưỡng luminance < 0.04.

| Góc | Vị trí | Gần đen trước → sau | Độ sáng trung bình trước → sau |
|---|---|---|---|
| 01 | Mặt sau container, hangar tây | 64.32% → 42.90% | 0.0626 → 0.0816 |
| 05 | Ngăn container, hangar đông | 38.18% → 27.60% | 0.1119 → 0.1339 |

Vách và chữ ở góc 01 rõ hơn; ở góc 05 thấy rõ hơn thùng gỗ, gân vách và nền khuất. Vẫn giữ bóng tối ở các mặt quay lưng cửa.

Ảnh trước: `FactoryLighting-2026-11-13/Before/playtest-01-*` và `playtest-05-*`. Ảnh cuối: `FactoryLighting-2026-11-13/playtest-01-*` và `playtest-05-*`. Cả hai có 1920×1080 và 3840×2160; đã đọc ảnh và kiểm tra kích thước PNG thật. CSV cùng góc trước/sau đã đối chiếu tại `playtest-comparison.json`.

## Thay đổi thực tế

[SCENE] Dưới `CampaignWorld/Factory/Map_v2/Light/DaylightPortals`:

- `Daylight_10_Hangar_v2_6`: bounceIntensity 3 → 8.
- `Daylight_02_Hangar_v2_6`: bounceIntensity 3 → 12.
- 12 cổng còn lại vẫn có bounceIntensity 3. Cường độ trực tiếp, màu, loại Rectangle Baked, vị trí cửa, sun và các thông số bake không đổi trong lần chỉnh này.

Không thêm đèn runtime, mesh, collider, material hay nội dung gameplay. Hai lần bake ghi lại lightmap, LightingData và reflection probe dùng chung của GameScene.

[CODE Assets/FPS/Features/Missions/Editor/FactoryInteriorLighting.cs:355] Giữ tinh chỉnh 8/12 khi chạy lại ApplyPortals. Hai hangar có cùng tên nên phân biệt bằng dấu world X; đã xác nhận pivot thật qua Diagnose: tây (-9.08, 1.63, -36.18), đông (106.08, 0.57, -7.46). Có comment `shortcut:` yêu cầu khảo sát lại nếu Factory di chuyển. Dữ liệu xác nhận ở `hangar-pivots.json`.

[CODE Assets/FPS/Features/Missions/Editor/FactoryInteriorLighting.cs:31] Output bằng chứng sang thư mục ngày 13. Hai JSON vị trí camera được sao chép nguyên vẹn từ ngày 12, đã kiểm SHA256 trùng.

[CODE Assets/Tests/PlayMode/FactoryDaylightPlaytest.cs:72] Bắt buộc đủ 10 góc; giữ tiêu chí mọi hangar có góc rõ; thêm ngưỡng riêng góc 01 <50% và góc 05 <30%, kiểm ảnh thực 1080p/4K. Đầu vào vẫn là fixture ngày 12; output ngày 13 bảo toàn ảnh Before.

## Kiểm chứng và log thật

```text
4960f389  FactoryDaylightPlaytest trước chỉnh scene: 0/1 passed, 1 failed.
  Shaded west container: 64 % near black; expected below 50%
  Shaded east container: 38 % near black; expected below 30%
b658f95b  FactoryDaylightPlaytest sau mức 8/8: 0/1 passed, 1 failed.
  Shaded east container: 32 % near black; expected below 30%
1825d193  FactoryDaylightTests sau mức 8/12: Test run completed: 6/6 passed.
c4e32010  FactoryDaylightPlaytest sau mức 8/12: Test run completed: 1/1 passed.
PASS: 14 portals; west8/east12/others3; direct intensities unchanged; west/east pivots confirmed; viewpoint fixtures identical.
PASS: before/after 1080p and 4K PNG dimensions; both target views brighter and below unchanged thresholds.
```

Log JSON tại `editor-tests-final.json`, `playmode-final.json`, `scope-check.txt`, `portals-final.json`. Ngưỡng bài test không hạ sau lần fail. Playtest chạy host thật và camera người chơi; không thay gameplay runtime để làm sáng ảnh.

[PLAYTEST screenshot] Đã kiểm ảnh gần cửa của hangar đông (góc 04): tường, trần và thùng còn chi tiết. Độ sáng trung bình góc này tăng 0.1944 → 0.2525. Góc 00 của hangar tây tăng 0.0984 → 0.1354.

Đối chứng cuối tại 11 góc cố định, lưu `control-comparison.json`: bốn góc Factory ngoài trời và bốn góc Laboratory khớp độ sáng trung bình trước tới bốn chữ số thập phân; ba góc Asylum lệch từ -0.12% tới +0.24%. Mức tối hơn 8–10% ở Laboratory từng xuất hiện trong ảnh sau bake thứ nhất, nhưng không còn ở bộ cuối; chưa xác định nguyên nhân cụ thể của chênh lệch tạm thời này.

[SCENE] `bake-console-final.json` và `console-final.json`: count=0 errors. Sau test đã trở về Edit Mode, gọi Save Authored Scenes có AssetDatabase.SaveAssets(); `scene-final.json` xác nhận GameScene isDirty=false.

## Skill và quy tắc áp dụng

- `unity-skills`: health/meta/schema trước thao tác, dryRun trước ghi, xác nhận lại bằng truy vấn Editor.
- `unity-scene`: dùng scene_get_loaded kiểm tra scene và isDirty, không đọc YAML.
- `unity-light`: Area lights cần bake; giữ nguồn trời tại ô cửa và dùng batch cho hai nguồn.
- `unity-component`: dùng tên property C# bounceIntensity và đọc lại giá trị đã lưu.
- `unity-batch`: batch transactional cho hai thay đổi; GET /jobs cho trạng thái test.
- `unity-editor`: chỉ gọi menu chính xác, Save Gate trước/sau thao tác hoặc test.
- `unity-test`: test bất đồng bộ, không chạy TestRunner song song.
- `ponytail`: dùng lại công cụ/test hiện có, thay đổi tối thiểu trong hai source đã có.
- `debugging-and-error-recovery`: giữ bằng chứng fail; xác nhận Unity dùng assembly cũ rồi Assets/Refresh, không hạ ngưỡng để qua test.
- `computer-use`: đã đọc và thử dùng ở giai đoạn đầu; ngừng khi nhận Escape. Thao tác này không cần thiết cho công việc. Toàn bộ chỉnh ánh sáng, bake và xác nhận cuối thực hiện bằng Unity-Skills.
- Subagent độc lập đọc `code-review-and-quality`, review source và fixture, không thấy lỗi chặn. Không giao subagent xem/xóa ảnh; ảnh cuối giữ lại để người dùng đánh giá.

Codebase Memory đã được gọi qua stdio theo .mcp.json nhưng daemon không khởi động được (`CBM daemon could not start within 30000 ms`). Chỉ đọc source đã thu hẹp và tìm caller trong các thư mục liên quan. Bản sao scene và dữ liệu bake trước chỉnh ở `.codex/backups/FactoryLighting-Corners-20261113-112823/`.

## Giới hạn

Số đo lấy trên khung hình gồm HUD và súng; góc 01 sát vách nên không đại diện cả hangar. 4K có ảnh và kiểm kích thước, không có bảng luminance riêng. Chỉ thử quality Low và host cục bộ; chưa đo FPS hoặc thử client thứ hai. Đây là cân chỉnh để dễ nhìn, chưa phải xác nhận mô phỏng ánh sáng vật lý.