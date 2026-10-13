# Tối ưu hiệu năng FPS — 10/10/2026

Đã triển khai native occlusion trong GameScene và giữ cấu hình 5 m đã sửa lỗi visibility. Chi phí render giảm rõ trong các cửa sổ Player hợp lệ. **Chưa chứng nhận 60 FPS ổn định cho toàn game hoặc hoàn tất cổng phát hành trong kế hoạch.** Theo ưu tiên mới của người dùng, đã dừng các lượt benchmark lặp lại và không chạy tiếp bài 30 phút.

## Thay đổi giữ lại

- GameScene: bốn OcclusionArea bao phủ Factory, Asylum, Laboratory và FactoryAsylumConnection; bake Smallest Occluder 5 m, Smallest Hole 0,25 m, Backface Threshold 100%.
- Dữ liệu native: **750.704 byte**, **3.408 occluder**, 8.319 MeshRenderer có mesh. Đã đối chiếu GlobalObjectId: toàn bộ static flags ngoài OccluderStatic/OccludeeStatic giữ nguyên (`final-invariants.json`).
- Chỉ chọn kết cấu lớn, cố định, opaque. Loại geometry chuyển động, cửa, kính và các building shell bao quanh camera arrival/recovery đã gây lỗi culling.
- Không đổi collider, NavMesh, chapter activation, gameplay, enemy count, network protocol, save format hoặc render pipeline.
- `FPS.MedicalAnimation.Editor.asmdef`: giới hạn assembly prototype vào Editor, tham chiếu FPS và Unity.Netcode.Runtime để sửa lỗi chặn build release.

Harness và Editor menu tạm đã được xóa khỏi Assets qua Unity AssetDatabase. Source audit giữ tại `Tools/`; ảnh Editor chuyển sang `EditorImages/`.

## Số đo Player hợp lệ hiện có

Windows64 Mono, D3D11, Low, 1920×1080, VSync 0, FPS không giới hạn; seed 1701, warmup 30 giây, đo 60 giây. Camera/FOV và roster được khóa; AI, NavMesh, animation và replication tiếp tục hoạt động. Hai phía dùng cùng executable OcclusionSafe5, thay duy nhất camera occlusion.

| Scenario | Average trước → sau (ms) | p95 trước → sau (ms) | p99 sau (ms) | Frame >50 ms sau, trong harness |
|---|---:|---:|---:|---:|
| Factory, 3 zombie | 11,96 → 4,83 | 16,12 → 7,28 | 8,33 | 0 |
| Factory, 43 zombie | 14,46 → 5,01 | 18,56 → 6,77 | 8,08 | 0 |
| Laboratory, 27 zombie | 5,78 → 3,66 | 8,31 → 4,57 | 5,18 | 0 |
| Laboratory, 70 zombie | 9,19 → 5,46 | 13,15 → 6,98 | 8,33 | 0 |
| Asylum, 10 zombie | 7,18 → 3,84 | 9,41 → 4,75 | 5,29 | 0 |

**Mỗi phía hiện chỉ có một cửa sổ hợp lệ cho mỗi scenario.** Laboratory/Asylum dùng lượt bật occlusion trước đó, khác thời điểm đối chứng. Các con số là kết quả quan sát; chưa có đủ ba lượt độc lập để đánh giá dao động hoặc công bố phần trăm cải thiện đáng tin cậy. Các cửa sổ này đạt ngân sách average/p95/p99 của kế hoạch, nhưng không thay thế kiểm tra toàn gameplay.

Nguồn đối chứng: `GateVerified/NoOcclusion/Run1/`. Factory sau tối ưu: `GateVerified/Occlusion/Run1/factory3-r1.*` và `Run1Retry1/factory43-r1.*`. Laboratory/Asylum sau tối ưu: `GateFinal/Occlusion/Run1/`. `summary.json` lưu thống kê và đường dẫn từng cửa sổ bị loại.

Không so trực tiếp với 45,6 FPS của audit cũ: fixture hiện tại khóa camera, roster và spawn schedule, có warmup dài hơn và không tái hiện toàn bộ workload của lượt khảo sát cũ.

## Bằng chứng bottleneck và bộ nhớ

- Factory 43: SetPass trung bình **851,7 → 168,1**; CPU busy từ PresentMon khoảng **14,24 → 4,85 ms**, GPU busy **6,12 → 2,02 ms**. Điều này hỗ trợ ưu tiên giảm visibility/render submission.
- PresentMon thu độc lập; trace coverage được kiểm tra so với số frame của harness. Không cộng CPU busy và GPU busy. Loại frame biên có Present vượt cửa sổ đo, để log/CSV xuất sau đo không bị tính vào gameplay.
- Một số counter Unity trong release không hỗ trợ được ghi `unavailable`. GPU busy từ PresentMon không phải Unity GPU Profiler marker.
- Texture current khoảng **2.828,7 MiB**, non-streaming **2.785,0 MiB**. Dedicated allocation theo Windows PDH khoảng **3,0–3,1 GiB** trong các lượt đã thu; đây không phải bằng chứng đầy đủ về physical residency.
- Process private bytes/commit khoảng **4,1–4,3 GiB**, working set khoảng **0,7–0,8 GiB**, ghi riêng. Máy còn khoảng 2,8–4 GiB RAM ở nhiều lượt; không cộng các thống kê bộ nhớ này với VRAM.

## Kiểm tra hình ảnh, rollback và regression

- `Safe5Visual/`: **124 cặp ảnh** tại 31 điểm arrival/recovery, bốn hướng nhìn mỗi điểm; 118 cặp giống hệt ở ROI cảnh. Các khác biệt lớn đã xem là marker HUD; geometry hangar/container bị mất ở bản thử trước đã hiện đúng trong Safe5.
- Bản bake 5 m mở rộng ban đầu và bản 2 m **bị loại** vì culling sai tại Factory recovery 9, dù giảm chi phí render. Bản giữ lại loại shell chứa camera recovery khỏi occluder.
- **16/16 regression passed**: projection HUD (1), rappel bốn actor (1), settings/difficulty (9), CampaignLifecycle — recovery/Tank/state restore/cửa NavMesh (4), navigation và khôi phục camera sau insertion (1). Kết quả và trạng thái scene sau từng run nằm trong `Regression/`.
- Save Gate thực hiện trước/sau tests, reload, cleanup và build; bỏ qua scene Untitled/Test Runner. `editor-before-clean-build.json` xác nhận GameScene `isDirty=false` trước build.

Không triển khai LOD, GPU instancing, texture streaming hoặc sửa AI/physics/networking vì chưa có marker cụ thể chứng minh một thay đổi tiếp theo cần thiết. Props polygon cao đã kiểm tra không nằm gần camera hotspot; texture đã có compression/mipmap và Read/Write tắt. Không tắt baked lights/probes.

## Build và giới hạn bàn giao

Build sạch đã **thành công**, job `e009fe11`: `E:/Unity/Project/FPS/Builds/PerformanceOptimization/Release/FPS.exe`, Windows64 Mono, non-Development. Đã kiểm tra FPS.dll không chứa `PerformanceAuditPlayer`, assembly MedicalAnimation.Editor không được ship, và Player khởi động D3D11/Mono, còn hoạt động sau 12 giây. Sau smoke startup, công cụ chủ động đóng Player; exit code 1 do TerminateProcess không phải lỗi crash được quan sát. Chưa đo pacing gameplay của clean release.

`build-clean-report.json`, `clean-release-verification.json` và `clean-release-smoke.log` lưu bằng chứng. Unity xác nhận **GameScene `isDirty=false`**, Edit Mode, không compile sau build (`editor-final.json`).

Chưa chạy đủ ba lượt A/B độc lập, Development Profiler/Frame Debugger đầy đủ, đường đi liên tục/cửa chuyển động/kính/lift/extraction, nhiệm vụ tự nhiên qua ba chapter, combat first-use trong clean release, multiplayer bốn máy, kiểm tra nhiệt/xung CPU và bài chạy dài 30 phút. Vì vậy bản này dùng để chơi thử và kiểm chứng tiếp, chưa là chứng nhận hiệu năng phát hành. Không thêm Addressables, package hoặc framework mới.

Có thể rollback riêng occlusion bằng source Editor tool trong `Tools/PerformanceOptimizationEditor.cs`: khôi phục chỉ hai occlusion flags từ `occlusion-original-flags.json`, xóa bốn area do audit tạo và Clear dữ liệu native. Không reset toàn scene vì có thay đổi của người dùng từ trước.
