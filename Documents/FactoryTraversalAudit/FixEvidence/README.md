# Factory traversal fix — 06/10/2026

> Bản dốc dưới đây đã được thay bằng nền lấp kín theo yêu cầu mới. Xem [FillEvidence](../FillEvidence/README.md) cho scene, ảnh và kiểm chứng hiện tại. Các số liệu bên dưới thuộc bản dốc trước đó.

Đã sửa hố L30 và tiêu chuẩn lưu/chọn vị trí an toàn theo [FIX-PLAN](E:/Unity/Project/FPS/Documents/FactoryTraversalAudit/FIX-PLAN.md). Bằng chứng audit trước sửa vẫn nằm ở thư mục cha.

## Thay đổi đã lưu

- L30 tại `(15,0,-98)` có dốc bê tông rộng 3 m, dài 6 m, lên cao 2,092 m (độ dốc khoảng 19,22°), thêm landing dài 1 m. Mở khoảng 3,5 m trên hàng rào trang trí phía bắc bằng hai đoạn có cả mesh và collider; giữ hàng rào gốc dưới dạng inactive. Không thay thông số nhảy/controller.
- Bake riêng Factory và đoạn service road đến Z=207 từ 2.511 nguồn collider. Loại các chương chồng tầng và lá cổng di chuyển khỏi nguồn bake; giữ modifier của Unity và obstacle động. Agent radius 0,5 m, height 2 m, slope 45°, climb 0,3 m, voxel 0,1 m. Dữ liệu mới: `Assets/FPS/Features/World/Content/Campaign/FactoryTraversal/FactoryNavigation.asset`; dữ liệu cũ không bị ghi đè.
- L15 có link xuống một chiều từ `(106,0.56,-24.18)` tới `(110.5,0,-26)`, theo tuyến controller đã kiểm chứng. Chênh cao 0,56 m cho phép bước xuống, nhưng vượt step 0,3 m khi đi ngược.
- `CampaignPlacement.TrySafeFloor` dùng chung cho việc lưu pose, kiểm tra lại pose cũ, fallback arrival và reconnect: đúng floor/chương, đủ chỗ đứng và có đường **từ floor về** recovery đang hoạt động. Nền có NavMesh nhưng cô lập bị từ chối. Server authority, cooldown và cơ chế owner xác nhận relocation được giữ.

## Kết quả đối chiếu 3.306 điểm tiếp đất

| Nhóm | Trước sửa | Sau sửa |
|---|---:|---:|
| Có lối trở về đã kiểm chứng bằng controller | 3.195 | 3.268 |
| Kẹt tại L30 | 73 | 0 |
| Phụ thuộc cổng interlock đang đóng | 36 | 36 |
| Mép ngoài WorldBase, đi ra ngoài sẽ rơi | 2 | 2 |

- 3.266/3.266 đường NavMesh mới đi hết bằng `PlayerMovement.SimulateTick` ở 60 Hz, không nhảy. Hai mép hẹp còn lại không có NavMesh phù hợp bán kính 0,5 m, nhưng 2/2 tuyến vật lý cũ vẫn đi hết. Chúng không được chấp nhận làm safe pose.
- 256/256 lượt tập trung ở L30, L15 và năm điểm mép đường đã qua cả chạy 7 m/s và đi thường 3,5 m/s. Một điểm L30 nay nằm ngay trên đỉnh dốc cũng nằm trong bộ 3.266 tuyến toàn map.
- 12/12 lượt riêng lên/xuống dốc tại ba vị trí ngang, hai tốc độ, đã qua; 99/99 mẫu floor/slope/clearance qua. Các thử đi thẳng qua vật cản phía đông hố được giữ trong `escape-controller-probes.json`; đường NavMesh vòng qua vật cản đã qua trong bộ kiểm chứng cuối.
- Một lần đối chiếu 3.306 điểm (floor, clearance, safe-floor, path và xuất báo cáo) mất 2,444 s trong Editor, khoảng 0,74 ms/điểm. Đây là số đo gồm nhiều truy vấn và I/O, không phải profiler runtime. Safe pose vẫn lấy mẫu mỗi 0,25 s, thoát sớm ở recovery đầu tiên nối được; chưa thêm cache.
- Cả bốn arrival Factory và bốn arrival Asylum có safe-floor hợp lệ. Bốn arrival Laboratory có floor/clearance nhưng đang tách khỏi recovery khi cổng đóng trong Edit Mode; guard từ chối đúng trạng thái này. Có sáu recovery Laboratory nối được. Xem `chapter-arrivals.csv`; kiểm tra này không chứng minh toàn bộ chuỗi chuyển chương Laboratory.

## Tests và giới hạn

- EditMode `CampaignPlacementSafetyTests`: 2/2 qua (job `59308466`), gồm nền cô lập, reconnect, sai chương, recovery tắt/thiếu và fallback pose cũ.
- PlayMode `CampaignLifecycleTests`: 3/3 qua (job `32789675`), gồm kiểm tra recovery trên server: insertion bị chặn, pose cô lập bị bỏ, arrival hợp lệ được dùng, relocation chờ owner xác nhận, input bị khóa và cooldown được đặt. Hai test lifecycle cũ vẫn qua.
- PlayMode `FactoryBakeLeavesBothInterlocksUnderDynamicGateControl`: 1/1 qua (job `810b20e4`). Dùng NavMeshData đã lưu và footprint/offset của hai interlock Z=188/198 trong scene test cô lập: collider và carving chặn khi đóng, authority mở lá cổng thì collider thông và PathComplete trở lại. Không bake lá cổng thành bức tường cố định.
- Chưa chạy một phiên campaign có hai máy/client độc lập; chưa chứng minh owner nhận RPC và gửi ACK qua mạng thật, hoặc toàn bộ tiến trình ba chương. Tests server cô lập không thay cho kiểm chứng đó.
- Vị trí chính xác trong ảnh báo lỗi ban đầu chưa được xác nhận; sửa này dựa trên hố đã tái hiện trong audit, không khẳng định mọi vùng thấp đều là lỗi.

## Bằng chứng và khả năng khôi phục

- [Ảnh sau sửa](E:/Unity/Project/FPS/Documents/FactoryTraversalAudit/FixEvidence/factory-l30-fixed.png), `escape-geometry.csv`, `landing-placement.csv`, `landing-paths.json`, `nav-controller-results.json`, `focused-controller-probes.json`, `ledge-return-results.json`, `summary.json`.
- `Baseline/` giữ GameScene, MainMenu và hai file runtime trước thao tác; các `.cs.diff` chỉ thể hiện phần sửa runtime của công việc này. MainMenu đã được khôi phục chính xác về SHA256 `6F7123A0845A59AFD92156FBDFE8F02BD052994AB26474FAE04F1718FD258534` sau khi thao tác save thử làm Unity ghi lại dữ liệu không liên quan.
- GameScene sau sửa: SHA256 `BFE8C108590E99632A070BAED910506BBAD68E11E54067A4719ACFAEB44EBB13`. Scene và assets đã lưu, hai scene đều sạch sau Test Runner, Console có 0 lỗi, không còn controller probe; các thay đổi HUD/vũ khí/công việc khác có sẵn không bị reset. Không tạo commit hoặc PR.
- Khi bake lại, dùng menu **Tools/FPS/Factory Traversal Repair/Bake Factory Only** để giữ bước lọc chapter/lá cổng và thông số controller. Nút bake Volume mặc định của Surface không thực hiện toàn bộ bộ lọc này. Authoring dùng API nội bộ của package Navigation hiện cài (`134dd6a3b97b`) qua reflection, tương tự mẫu authoring có sẵn; cần kiểm tra lại khi nâng package.

![Lối thoát L30 sau sửa](E:/Unity/Project/FPS/Documents/FactoryTraversalAudit/FixEvidence/factory-l30-fixed.png)
