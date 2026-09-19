# Cleanup — cập nhật 13/09/2026

Theo yêu cầu xóa hẳn, không giữ bản sao lưu:

- Đã xóa hai ZIP sao lưu và toàn bộ 10 file artifact/script của lượt cleanup trước: 370,58 MB.
- Đã xóa 43 file C# (109 file kể cả meta/asmdef): toàn bộ World/Editor builders, authoring, verification; Tests/EditMode và PlayMode; Networking/Runtime/Simulation gồm A1 harness, recorder/probe, Campaign smoke và capture.
- Đã xóa các runner `.ps1`, analyzer/helper `.py`, báo cáo verification cũ, manifest lớn, bộ cài tải thừa Codebase Memory và script setup `.local`: 336,90 MB.
- Theo xác nhận riêng của người dùng, đã xóa ba `.blend` nguồn, `finalize_door.py` và `Refresh-CodebaseMemory.ps1`: 30,99 MB.
- Đã xóa manifest tạm của lượt này. Tổng giải phóng thêm khoảng **739,32 MB**. Không có ZIP/backup thay thế.

## Kiểm tra

- Unity compile `ready`; MainMenu ở Edit Mode, `isDirty == false`.
- 1.730 asset trong Assets/FPS được đối chiếu SHA-256, không có thay đổi. Prefab Gekko, súng, animation, map và dữ liệu bake được giữ nguyên.
- Kiểm tra hiện tại: 440 prefab, không phát hiện Missing Script.
- `.git/index` giữ nguyên; không stage/reset/checkout/commit.
- Đồ thị Unity đã cập nhật sau runtime cleanup; không còn hai logger bị xóa. Codebase Memory đã refresh thành công: 15.383 nodes, 56.573 edges; còn 19 file parse-partial, nên đồ thị vẫn chỉ là chỉ mục best-effort.
- Lượt runtime cleanup xác minh compile, Console, reflection, prefab và hash. Không chạy lại phiên multiplayer hoặc Play Mode; đây không phải kết quả kiểm thử end-to-end mới.

## Runtime cleanup đã hoàn tất

- Đã gỡ toàn bộ phím xem/chọn súng F5–F10 và input giả lập trong runtime.
- Lượt này sửa 13 file C# và xóa 2 logger, không tạo script/helper/backup mới.
- Đã gỡ các API `ForTests`, override verification của AI/mission/save và nhánh ngắt mạng cưỡng bức dành cho harness.
- Đã xóa `NetworkDiagnostics.cs` và `AdaptiveDirectorDiagnostics.cs` cùng file `.meta`; game không còn ghi JSONL verification hay phát callback thu bằng chứng.
- Luồng gameplay thật vẫn giữ: input người chơi, kiểm tra quyền server, reconnect bình thường, xác thực khoảng cách/line-of-sight pickup và save campaign trong `Application.persistentDataPath`.
- `NetworkMatchStateManager.testMode` thực tế phục vụ trạng thái khi chưa spawn; đã đổi tên thành `useLocalState` và giữ hành vi offline.
- Unity compile thành công; reflection kiểm tra assembly cho `hookCount=0`; 440 prefab không có Missing Script; MainMenu Edit Mode `isDirty == false`.
- Đối chiếu lại 1.730 asset gameplay: SHA-256 không đổi; `.git/index` không đổi.

Hạ tầng đang dùng (MCP/Unity-Skills, exporter đồ thị, packages, Unity import cache và script của package/skill) được giữ lại. Script `.ps1`/`.py` do agent tạo trong Documents, ArtSource và thư mục artifact của task đã được xóa.

## Tổ chức lại world assets — 13/09/2026

- Di chuyển 1.996 asset bằng AssetDatabase; nội dung của toàn bộ file giữ nguyên. Xóa các thư mục nguồn sau khi xác nhận chúng rỗng.
- Nội dung `Abandoned_Asylum` đã vào `World/Content/AsylumFacility`; `Barking_Dog/ExperimentRoom` đã vào `ExperimentFacility`, chia theo Materials/Models/Prefabs/Textures/Data/Scenes.
- Scene authoring và thư mục bake đi cùng nằm trong `Scenes`; scene mẫu import ở `Scenes/Source`. Prefab ExperimentRoom gốc ở `Prefabs/Source`, giữ riêng các bản trùng tên nhưng GUID khác với prefab đã chỉnh cho game.
- `FactoryRepair`, `FactoryGroundReview`, `FactoryMasterPlan` đã phân về Buildings, Roads, Fences, Oil_tanks, Materials và Meshes/Concrete theo công dụng.
- `Campaign/ArtRepair` đã phân về AsylumFacility/Meshes và Campaign/Terrain. Quy ước chi tiết nằm trong `Documents/AI_Knowledge/PROJECT_MAP.md`.
- Kiểm tra Unity: không thiếu asset, 1.995 GUID có sẵn được giữ, dependency của 10 scene không đổi, 440 prefab không có Missing Script. Nội dung cả 450 file scene/prefab và `.git/index` giữ nguyên.
- Sửa một lỗi import có sẵn: `.meta` rỗng của `Wall_4_Albedo.png`. Texture giữ nguyên dữ liệu ảnh và được Unity tạo GUID hợp lệ; đây là thay đổi metadata duy nhất trong các asset đã chuyển.
