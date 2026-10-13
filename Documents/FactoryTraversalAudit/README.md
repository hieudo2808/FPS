# Kiểm tra vùng thấp và đường thoát của Factory

**Đã tái hiện một vùng kẹt ở phía nam Logistics: L30, gần `(15, 0, -98)`.** Mặt đường ở khoảng Y=2.092, đáy ở Y=0, nhưng controller chỉ bước lên 0.3 m và nhảy thực tế khoảng 0.749 m. Vùng đã đo không có lối dốc hoặc bậc nối trở lên.

L01, L02, L03, L15 và L61 có đường đi bộ tới Logistics đã kiểm chứng từ vị trí thử. L01 phải vòng khoảng **366 m** theo NavMesh. L15 đi qua `(106, 0.56, -24.5)` đến `(110.5, 0, -26)`, rồi lên lối dốc phía đông nam; thử lại toàn tuyến xác nhận **không cần nhảy**, sửa kết luận sơ bộ từ phép thử chỉ dùng nhảy.

Chưa sửa map hoặc gameplay. Chưa xác định chắc chắn tọa độ ảnh người dùng gửi; không đồng nhất ảnh với L30 dựa trên hình dáng hoặc khoảng cách HUD.

![Mặt bằng và vùng kẹt](E:/Unity/Project/FPS/Documents/FactoryTraversalAudit/factory-traversal-map.png)

## L30 — bằng chứng vùng kẹt

| Thuộc tính | Kết quả |
|---|---|
| Tọa độ tham chiếu | `(15, 0, -98)` |
| Giới hạn quét tinh | X 10.5..29.75, Z -103..-95.5 |
| Nền | WorldBase_Continuous, Y=0 |
| Mặt đường lân cận | khoảng Y=2.092 |
| Quét tinh | bước 0.125 m; 28.675 mẫu toàn ô, 4.692 mẫu nối với đáy hố |
| Kết nối lên tầng trên | không tìm thấy trong quét tinh |
| Thử thoát | 36 hướng và 6 ứng viên nhảy xa đều thất bại |
| NavMesh về Logistics | PathPartial |
| Thử vào | 34 từ mép đường, 39 từ tầng cao tái hiện tiếp đất |

Bốn phép thử mép ban đầu cũng tái hiện đi xuống và không trở lại được. Một lối rơi được thử lại với 30 tick chờ sau tiếp đất vẫn thành công. Đây là bằng chứng bằng CharacterController và đúng PlayerMovement.SimulateTick trong Editor; chưa phải phiên chơi mạng đầy đủ.

Bệ Plinth_13_Hangar_v2_outbuilding1, Plinth_14_Hangar_v2_1 và đường nâng cao bao quanh nền thấp. Có nền đứng được không đồng nghĩa với có lối thoát. LowYardFoundations đang tắt, nhưng chưa có bằng chứng bật toàn bộ nhóm là cách sửa phù hợp.

## Cơ chế cứu hộ và chiều cao nhảy

FactoryFallRecovery ở Y=-7.5..-4.5, nên người đang đứng trên đáy Y≈0 không vào trigger cứu khi rơi khỏi map.

Kiểm tra trực tiếp CampaignPlacement tại L30, với Y=0.08 và 0.12: **TryFloor và Clear đều thành công, nhưng không có CompletePath tới cả tám điểm cứu hộ Factory**. WorldRecoveryService.Update chỉ dùng TryFloor/Clear để lưu vị trí an toàn. **Suy luận có cơ sở:** khi điều kiện phiên chơi cho phép lấy mẫu, đáy hố có thể được lưu; nhánh fallback khi cứu có thể dùng lại vị trí này. Chưa chạy callback cứu trong phiên mạng, nên không khẳng định mọi lần cứu đều trả lại hố.

Cả bốn prefab có jumpHeight=2, gravityScale=2.5, radius=0.5, height=2, step=0.3, slope=45°, skin=0.08, tốc độ chạy=7 m/s. Impulse dùng sqrt(2×9.81×jumpHeight), còn gia tốc rơi dùng 9.81×gravityScale. Do đó chiều cao lý thuyết là **jumpHeight/gravityScale=0.8 m**, đo ở 60 Hz là khoảng **0.749 m**. Giá trị jumpHeight=2 không đồng nghĩa với nhảy cao 2 m trong công thức hiện tại; chưa xác nhận ý định thiết kế ban đầu.

## Độ bao phủ

- 3.213 collider Factory và Factory–Asylum; 2.519 collider rắn hoạt động.
- Lưới 881×1.222, bước 0.5 m, quét mọi tầng có mặt đứng và khoảng trống phù hợp: **822.127 mẫu**, đủ 1.222 hàng, không tràn bộ đệm.
- **77 vùng thấp hình học**, không phải 77 hố kẹt đã xác nhận.
- 10.476 phép thử mép sân thấp: 1.471 tiếp đất thành công, thuộc L01/L02/L03/L15/L30.
- 9.918 phép thử tầng cao và đường nối, gồm 400 ở đường nối: 1.835 tiếp đất thành công.
- NavMesh tại cả 3.306 điểm tiếp đất: 3.139 PathComplete, 165 PathPartial, 2 PathInvalid. Cả 3.139 đường hoàn chỉnh đã về tới Logistics bằng controller.
- Thử riêng 51 lối thoát L15 và 5 điểm trên mép tường/mặt đường không có NavMesh hoàn chỉnh: tất cả về được, không cần nhảy.
- Tổng cộng **24.744 lượt thử controller**. Trong 3.306 phép thử tiếp đất thành công: 3.195 có tuyến về đã kiểm chứng; 73 rơi vào cùng hố L30; 36 nằm trong/sau cổng nhiệm vụ đóng; 2 ở mép ngoài rồi rơi qua cao độ cứu hộ. Đây là số lượt thử, không phải số vị trí hoặc hố khác nhau.

18 đường về ban đầu dừng tại hai cụm góc NavMesh, gần `(-29.10, -122.28)` và `(20.10, -16.14)` trong X/Z. Kiểm tra cho thấy capsule đã tới đúng X/Z, nhưng cao độ khi đứng sát mép dốc lệch hơn ngưỡng 0.3 m của công cụ. Cho phép sai khác 0.55 m **chỉ tại waypoint NavMesh trung gian**, vẫn giữ kiểm tra cuối tuyến 0.3 m và 30 tick chờ tiếp đất: cả 18 tuyến đi qua được. Đây là lỗi tiêu chí đo đã được sửa, không phải hai hố kẹt mới. 66 thử lối vòng trước khi xác định nguyên nhân cũng xác nhận mỗi điểm xuất phát bị báo lỗi có một lối về.

L26 nằm ngoài backstop phía bắc: bước tiếp ra mép làm người chơi rơi dưới cao độ trigger; chưa thử callback cứu. Đây khác với đứng kẹt trên nền Y=0.

36 điểm đường nối có NavMesh chưa hoàn chỉnh nằm trong/sau các cổng FactoryInterlock và AsylumInterlock đang đóng; hai điểm khác trên mép WorldBase tại Z=185. Không thể kết luận lỗi hố chỉ từ phép thử cổng đóng; trạng thái mở cổng/chuyển chương cần xét riêng.

CampaignMissionController.ApplyDoorTargets mở entryDoor ở phase AwaitingParty và exitDoor khi chương Completed/đã đi qua; CampaignDoor.Update di chuyển cánh cổng theo trạng thái được đồng bộ và tắt navigationBlocker khi mở hoàn toàn. Đây là bằng chứng cổng thuộc luồng nhiệm vụ; phép đo Edit Mode chưa xác nhận thời điểm mở trong phiên chơi.

## Giới hạn

Đo trên **physics GameScene đã lưu trong Edit Mode**, không phải scene clone. Tool từ chối scene chưa lưu và Play Mode. Controller tạm dùng HideAndDontSave, gọi tick thật, rồi bị hủy; không lưu scene.

Lưới 0.5 m có thể bỏ sót lối hẹp hoặc kẹt nhỏ hơn một ô; chỉ L30 quét tinh 0.125 m. Graph ban đầu lạc quan, chưa xét sweep qua tường/trần; không dùng graph này làm kết luận an toàn. Mái được đo hình học, nhưng chưa xác nhận mọi điểm bắt đầu trên mái tới được từ tuyến chơi bình thường. Phép thử mép tập trung sàn thấp và tầng đến khoảng 5.5 m.

Thử ở tốc độ chạy mặc định, không mô phỏng AI/đồng đội/vật thể động, giảm tốc, nội suy mạng, mở cổng nhiệm vụ hoặc callback cứu. Một tuyến thành công từ điểm đại diện không chứng minh mọi vị trí trong vùng lớn thoát được. Thử đường về từ điểm tiếp đất giảm khoảng trống này nhưng vẫn là lấy mẫu hữu hạn.

## Hướng sửa đề nghị

1. Thêm lối dốc/bậc có collider từ L30 lên mặt đường; cập nhật NavMesh và thử xuống–lên. Nếu sân không dành cho người chơi, chặn lối rơi bằng hình học rõ ràng.
2. Lưu vị trí an toàn cần kiểm tra kết nối tới điểm phục hồi phù hợp, tránh fallback vào nền cô lập.
3. Quyết định rõ chiều cao nhảy và trọng lực mong muốn trước khi sửa công thức toàn cục, vì ảnh hưởng mọi map.
4. Nối lại NavMesh ở lối thoát L15 và năm điểm quanh mép tường/mặt đường dù controller đi được. Các cổng đường nối có CampaignDoor, trạng thái IsClosed=true; cần kiểm tra chuyển chương để đánh giá khi mở.

## Dữ liệu và chạy lại

regions.csv chứa cả 77 vùng, tọa độ, nền và phân loại; landings.csv chứa phân loại cả 3.306 lượt tiếp đất; summary.json chứa tổng số. Bằng chứng: boundary-coverage.json, boundary-results.json, remaining-results.json, landing-paths.json, landing-return-results.json, final-validation-results.json, upper-last-exit-results.json, placement-checks.json và refinement-analysis.json. Tệp surfaces.csv/refinement-surfaces.csv giữ mẫu gốc.

Tool Assets/FPS/Features/Missions/Editor/FactoryTraversalAudit.cs, menu **Tools → FPS → Factory Traversal Audit**. probes.json là đầu vào hiện tại; sao chép tệp *-input.json để chạy lại. Run Controller Probes ghi controller-probes.json; lưu mỗi batch trước batch tiếp theo. Check Landing Paths and Recovery xuất kiểm tra NavMesh/cứu hộ. Tool không bake hoặc sửa asset map.

Chạy analyze.py --self-check với Python có NumPy/Pillow để kiểm tra hố cô lập, dốc, sàn chồng và giới hạn nhảy có hướng. Chạy report.py để tạo bảng/tổng số/bản đồ. Không chạy lại --low-regions rồi trộn kết quả cũ: thao tác tạo lại ID/đầu vào.

GameScene SHA256: 63F712AD5EEDC32D3FB88C30D410F58EC58A06A1E1BE848C0AFF63342094E9AA; Unity 6000.5.6f1. Codebase Memory đã được thử nhưng không khởi động do kiểm tra DACL endpoint; không thay quyền thư mục. Source được thu hẹp qua Unity và thư mục feature, không đọc YAML scene/prefab.

Kiểm tra cuối: script đã biên dịch, Console không có lỗi, MainMenu và GameScene đều không dirty, controller tạm đã bị hủy, hash GameScene giữ nguyên. final-verification.json ghi trạng thái Editor/Console. Các sửa đổi Git có sẵn của công việc khác được giữ lại.
