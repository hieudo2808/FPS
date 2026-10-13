# Factory — lấp kín hố L30, 06/10/2026

Thay phương án dốc trong [FixEvidence](../FixEvidence/README.md) theo yêu cầu lấp hố.

- `LogisticsSouthFill` lấp toàn bộ vùng X=10…30, Z=−103,6…−95, mặt nền Y=2,094 m; BoxCollider đặc xuống Y=−0,12 m. Dùng asphalt của đường hiện có, cao hơn 2 mm để tránh z-fighting.
- Đã gỡ `LogisticsSouthEscape`, landing và hai đoạn rào tạm; khôi phục rào gốc. Scene và mesh đã lưu.
- Bake riêng Factory và service road đến Z=207 từ 2.509 nguồn collider, giữ bộ lọc chapter/lá cửa và modifier. Surface dùng `FactoryNavigationFilled.asset`.
- 680/680 mẫu có nền đặc cùng độ cao. 623 mẫu đủ khoảng trống thân người; 57 mẫu vướng công trình hiện có, không phải lỗ nền.
- 73/73 điểm từng tiếp đất trong hố có PathComplete tới Logistics, điểm đầu NavMesh giữ nguyên X/Z và đúng tầng.
- 166/166 lượt controller thật `PlayerMovement.SimulateTick` ở 60 Hz đã tới đích, không nhảy: 73 tuyến × đi bộ/chạy, cộng 20 lượt qua năm đường nối với nền cũ theo hai chiều và hai tốc độ.

Nền mới dùng UV2 mặt trên và lightmap của tile đường liền kề để khớp ánh sáng bake hiện có. `FactoryPitFillLighting` khôi phục ánh sáng khi mở scene, chỉ áp dụng nếu nền chưa có lightmap riêng; đã kiểm tra bằng tải lại GameScene và chạy **Inspect Pit Fill**. Đây là ánh sáng tái sử dụng, chưa bake GI cho hình học mới; nên bake lại khi chỉnh lighting Factory. Collider và NavMesh dùng hình học mới đã lưu.

- PlayMode `FactoryBakeLeavesBothInterlocksUnderDynamicGateControl`: 1/1 qua, job `98f3dccd`. Dùng NavMeshData của bản lấp; hai cửa vẫn chặn khi đóng và nối đường khi mở.
- Code biên dịch, Console 0 lỗi, hai scene sạch sau lưu/tải lại, không còn controller probe. MainMenu giữ nguyên SHA256 `6F7123A0845A59AFD92156FBDFE8F02BD052994AB26474FAE04F1718FD258534`.
- GameScene cuối: SHA256 `534481537449B0E835F721891C90F29284BD2ED8BE947C850FB6B9B9E137F86F`. Giữ nguyên thay đổi khác trong workspace; chưa tạo commit/PR.

`GameScene-before-fill.unity` và `FactoryTraversalRepair-before-fill.cs` giữ bản trước thay đổi. Kết quả toàn map, runtime recovery và tests trước đó ở FixEvidence là bằng chứng của bản dốc; không được tính là kiểm chứng mới cho bản lấp. Vị trí ảnh báo lỗi ban đầu chưa được xác nhận; L30 là hố kẹt đã tái hiện trong audit.

![Hố L30 đã lấp](E:/Unity/Project/FPS/Documents/FactoryTraversalAudit/FillEvidence/factory-l30-filled.png)
