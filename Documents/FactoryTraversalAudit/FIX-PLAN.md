# Điều chỉnh yêu cầu: lấp kín L30

Kế hoạch hiện tại: lấp toàn bộ hố ngang mặt đường bằng nền có collider đặc; gỡ dốc/landing và rào tạm, khôi phục rào gốc; bake lại riêng Factory, xác minh đường đi bằng controller ở tốc độ đi bộ/chạy và kiểm tra hai cửa interlock; lưu scene, ảnh và kết quả vào [FillEvidence](FillEvidence/README.md). Kế hoạch dốc bên dưới được giữ làm lịch sử.

# Kế hoạch sửa Factory — 06/10/2026

## Mục tiêu và phạm vi

Sửa các vấn đề đã có bằng chứng trong báo cáo: lối rơi một chiều vào L30, việc chấp nhận nền cô lập làm vị trí an toàn, và NavMesh thiếu kết nối tại những lối controller thực sự đi được. Hoàn thành bằng scene đã lưu, code đã biên dịch, kiểm tra di chuyển/cứu hộ và bằng chứng trước–sau. Không coi 77 vùng hình học là 77 hố phải lấp.

Bằng chứng đầu vào: 822.127 mẫu, 24.744 phép thử; L30 gần `(15,0,-98)`, giới hạn X 10.5..29.75/Z -103..-95.5, đáy Y=0, đường Y≈2.092. Cú nhảy đo được 0.749 m, bước 0.3 m. L01/L02/L03/L15/L61 đã có tuyến thoát. 36 điểm đường nối phụ thuộc cổng nhiệm vụ; không kết luận chúng là lỗi hình học. Vị trí ảnh gốc chưa xác định chắc chắn.

## 1. Chốt baseline và luồng dùng thực tế

- Kiểm tra Editor, scene dirty, Console và Git trước mỗi đợt sửa; lưu bản sao GameScene cùng metadata tham chiếu NavMesh trước khi ghi. Giữ toàn bộ thay đổi HUD, vũ khí và công việc khác đang có.
- Baseline hiện tại: GameScene sạch, SHA256 `63F712AD5EEDC32D3FB88C30D410F58EC58A06A1E1BE848C0AFF63342094E9AA`; MainMenu đang active, GameScene loaded additive.
- Dùng UnitySkills để đọc object/collider/material/surface. Codebase Memory đã thử lại index_status nhưng vẫn bị chặn bởi DACL endpoint; không thay quyền thư mục. Dùng các feature đã khoanh vùng để kiểm tra callers và tests.
- Factory có hai NavMeshSurface: CampaignSystems/FactoryNavigation có dữ liệu FactoryNavigation, thu RenderMeshes; surface cũ dưới Map_v2 thu PhysicsColliders nhưng chưa thấy dữ liệu hợp lệ. Xác minh dữ liệu được runtime dùng trước khi bake; tránh đưa Asylum/Laboratory vào dữ liệu Factory.

## 2. Làm lối thoát L30 bằng hình học thật

- Đặt một dốc rộng khoảng 3 m ở hành lang phía tây L30, nối đáy lên mặt đường phía bắc. Phương án ban đầu gần X=13.5, từ Z≈-101.5 tới -94.5; cao độ và chiều dài cuối cùng lấy từ raycast/collider thực tế.
- Kiểm tra khoảng trống capsule suốt tuyến, độ dốc dưới 45°, mép chân/đỉnh không có bậc cao quá 0.3 m, và tránh bệ Hangar/đường lân cận. Điều chỉnh vị trí nếu khoảng trống thực đo không đủ.
- Dùng vật liệu và mẫu authoring sẵn có. Tạo mesh dốc/collider riêng nếu không có phần đường hiện hữu phù hợp; không sửa mesh dùng chung. Hình ảnh phải thể hiện đúng nơi đi được.
- Giữ jumpHeight/gravityScale hiện tại: chiều cao nhảy là lựa chọn gameplay toàn cục, không cần đổi để sửa một lối thoát thiếu.
- Chạy controller lên/xuống dốc, từ các phần đáy hố đến đường và Logistics, với chạy lẫn tốc độ đi thường. Kiểm tra tiếp đất cuối tuyến và khoảng trống trần.

## 3. Cập nhật NavMesh Factory và các lối thoát đang thiếu

- Bake riêng surface Factory bằng nguồn/cỡ vùng phù hợp hình học và collider thật; lưu NavMeshData mới rồi gán cho scene, giữ dữ liệu cũ để có thể phục hồi.
- Xác minh NavMesh giữ đúng tầng của L30, L15, đường/bệ, recovery và mục tiêu. Thêm điều chỉnh bake cục bộ nếu cần; chỉ dùng link khi có tuyến di chuyển vật lý tương ứng.
- Kiểm tra cả 51 lượt tiếp đất L15 và năm điểm mép tường/đường từng PathPartial nhưng controller về được. Mục tiêu là đường dẫn đi được và không chiếu lên tầng sai; điểm trên nóc tường có thể cần chỉ đường xuống thay vì được coi là sàn chơi thông thường.
- Giữ cổng nhiệm vụ và quy tắc tiến chương. Kiểm tra cổng đóng vẫn chặn, cổng mở cho đi qua; không sửa cổng chỉ để mọi truy vấn Edit Mode trả PathComplete.

## 4. Sửa tiêu chuẩn vị trí an toàn và fallback cứu hộ

- Tận dụng CampaignPlacement.TryFloor, Clear và CompletePath. Dùng chung điều kiện: floor thuộc đúng chương, thân người có chỗ đứng, và có đường từ floor về một recovery hợp lệ/đang hoạt động trong chương.
- Áp dụng khi WorldRecoveryService lưu safe pose, khi dùng lại pose trong TryCampaignRecovery, và khi chọn vị trí reconnect. Pose cũ hoặc mất kết nối sau khi cổng đóng phải được kiểm tra lại trước khi dùng.
- Nếu pose không còn hợp lệ, giữ vị trí an toàn trước đó hoặc dùng arrival hợp lệ của chương; không fallback vào nền cô lập. Giữ quyền quyết định ở server, cooldown, kiểm tra chết/transfer và thông điệp đồng bộ hiện tại.
- Kiểm tra chi phí NavMesh trong vòng lấy mẫu 0.25 s: thoát sớm khi tìm được recovery, không tìm đường trên mỗi frame; chỉ thêm tối ưu nếu phép đo cho thấy cần.
- Thêm kiểm tra hồi quy nhỏ theo mẫu tests có sẵn: nền có NavMesh/clear nhưng cô lập bị từ chối; nền có lối về được chấp nhận; pose cũ cô lập không được dùng lại; sai chương, recovery tắt hoặc không có recovery không được coi là an toàn. Dùng dữ liệu NavMesh tạm và dọn sạch sau test.

## 5. Kiểm chứng và bàn giao

- Biên dịch và đọc Console sau sửa code; chạy các tests liên quan placement/recovery, các phép thử L30/L15 và smoke test chuyển chương/cứu hộ ở Play Mode. Không chạy toàn bộ suite không liên quan.
- Thử từ nhiều điểm L30 và những mép rơi đã tái hiện: xuống được rồi trở lên được, không cần nhảy, không lọt collider. Kiểm tra đường NavMesh tới recovery và Logistics.
- Chạy lại 3.306 điểm tiếp đất đã thu được hoặc bộ đối chiếu tương đương sau bake để phát hiện mất tuyến cũ. Không cần quét lại toàn bộ 822.127 mẫu nếu thay đổi chỉ cục bộ và đối chiếu không phát hiện vấn đề mới.
- Kiểm tra runtime server/owner cho trigger cứu hộ và fallback, cùng bảo toàn mục tiêu/cổng chuyển chương. Nếu môi trường không cho chạy phiên mạng đủ, ghi rõ phần chưa chứng minh và không tuyên bố đã kiểm chứng mạng.
- Chụp lối thoát và xuất kết quả trước–sau vào Documents/FactoryTraversalAudit/FixEvidence. Giữ dữ liệu audit trước sửa để đối chiếu; không ghi đè kết quả lịch sử bằng scene mới.
- Chỉ lưu GameScene sau kiểm tra hình học và tham chiếu; kiểm tra lại dữ liệu NavMesh, object sửa, scene dirty, Console, Git diff và không còn object thử.

## Tiêu chí hoàn thành

1. L30 có lối ra vật lý đã lưu; các điểm thử đi xuống rồi ra lại ở tốc độ thường/chạy.
2. NavMesh L30 nối đúng tầng tới recovery/Logistics; L15 và các điểm thiếu NavMesh được giải quyết theo tuyến vật lý.
3. Nền cô lập không được lưu hoặc dùng lại làm safe pose; reconnect dùng cùng tiêu chuẩn có đường về.
4. Các tuyến thoát cũ giữ hoạt động, cổng tiến chương giữ đúng hành vi.
5. Code biên dịch, tests liên quan qua, Console không có lỗi mới; có bằng chứng kết quả và giới hạn runtime rõ ràng.
6. GameScene/NavMesh đã lưu đúng tham chiếu, không làm mất thay đổi có sẵn của công việc khác.

Rủi ro chính: bake thu cả các map chồng tầng; corner NavMesh khác độ cao capsule; cổng động làm thay đổi kết nối; vật liệu/mesh collider không khớp; kiểm tra kết nối thêm chi phí CPU. Mỗi rủi ro có kiểm tra cụ thể ở các bước trên. Đây là sửa cục bộ trong kiến trúc hiện tại; không thêm dependency hoặc hệ thống leo trèo mới.
