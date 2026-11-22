# Sửa waypoint đầu Factory — 20/11/2026

Khi quay camera, chấm vẫn đại diện cho cùng một điểm dẫn đường. Nếu điểm ra khỏi khung, HUD ghim chấm trong vùng hiển thị và thêm mũi tên quay. Mũi tên chỉ tắt khi điểm lọt lại vào khung thêm 24 px ở chuẩn 1080p. Nếu không có tuyến đi hoàn chỉnh, HUD giữ tên nhiệm vụ và báo `ROUTE UNAVAILABLE`; không hiện icon và số mét theo đường thẳng.

- [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:325] Sau đoạn mở màn, HUD đợi cửa Decon mở và NavMesh ổn định rồi mới hiện tuyến đầu tiên. Đổi nhánh trong thời gian chờ không bỏ qua bước này.
- [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:356] Tuyến được kiểm tra lại mỗi 0,5 giây từ vị trí chân; giữ mốc nếu góc đầu hợp lệ vẫn khớp. Chỉ qua góc khi trong 1 m ngang, chênh cao không quá 0,6 m và đoạn tiếp thông trên NavMesh.
- [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.cs:96] Chiếu mốc sau khi camera người chơi cập nhật. [CODE E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HudMarkerProjection.cs:8] Điểm sau lưng theo phương ngang chỉ hướng quay trái/phải. [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:425] Ở mép dưới, cụm waypoint có thể nhích lên trên HUD để giữ số mét mà không lệch sang bên.
- [CODE E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:296] Khi Tank là đích, chỉ dẫn chờ tham chiếu Tank hợp lệ. Trạng thái Tank và tiến độ nhiệm vụ vẫn do server quyết định.

[SCENE] `Gate_DeconExit` dùng luật `AfterInsertion`, mở 5 m với tốc độ 4 m/s; `CampaignDoorSafety` dùng `NavMeshObstacle` có bật carve. [INFERENCE] Log chẩn đoán `87f35292` tái hiện tuyến `442 m → 323 m` và chấm dời `262,77 px`, dù chân và góc camera giữ nguyên. Lượt kiểm sau xác nhận nếu chờ obstacle tắt thì tuyến vòng không xuất hiện; lỗi này khác với việc sprite đổi hình khi lia quanh mép.

[PLAYTEST screenshot] So sánh trực tiếp ảnh Game View cùng góc nhìn trước/sau ở 1920×1080 và 3840×2160: mũi tên trong khung cũ được thay bằng chấm; số mét Power `323 m` và Logistics `267 m` vẫn đọc được. Ảnh sau sửa ở mép có chấm và mũi tên; ảnh mép dưới giữ số mét và ở cùng phía với điểm chiếu. Ảnh mất tuyến giữ `READ DELIVERY MANIFEST` và `ROUTE UNAVAILABLE`. Mockup tái dùng `GoalMarkerCentre` và `GoalMarker` đã được Noether duyệt trước khi sửa; rà soát độc lập chỉ ra ba ca biên sau đó đã bổ sung vào test.

Log Unity Test Runner:

```text
ac9bf243  Test run completed: 1/1 passed.  (ảnh Game View trước sửa)
b13f0fb8  Test run completed: 6/6 passed.  (EditMode)
828c4162  Test run completed: 1/1 passed.  (PlayMode host)
```

```text
Gate_DeconExit: held the marker for 113 frames with the blocker active; branch switch did not start a stale route; open=True; 323 m
Gate_DeconExit: walked (-11.00, 2.10, -73.63) -> (-6.04, 2.17, -73.63); route anchor (-1.90, 2.10, -75.40) -> (-1.90, 2.10, -75.40).
EastProcessAccess: walked (115.00, 0.05, -38.00) -> (110.04, 2.17, -38.00); route anchor (109.70, 2.10, -39.10) -> (108.90, 2.10, -39.70).
EastProcessAccess: corner retained with a simulated foot position 1 m below; advanced at the same XZ on the landing.
Unresolved Tank reference: action preserved, no boarding waypoint or distance.
```

Bài host thử mất và khôi phục tuyến bằng cách tạm dời điểm tương tác của manifest trong Play Mode. Ca sai tầng và tham chiếu Tank chưa phân giải là mô phỏng có assert; ca đi qua cửa và lên cầu thang dùng `CharacterController` thật trong Factory.

Skill đã áp dụng: `unity-skills` (Unity Test Runner, truy vấn scene và Save Gate), `unity-mcp-ui-layout` (tái dùng hai sprite và duyệt mockup trước khi sửa), `game-ui-ux` (vùng hiển thị an toàn, so sánh tại 1080p và 4K), `level-design` (kiểm lối đi thật qua cửa và cầu thang), `computer-use:computer-use` (tìm cửa sổ client trong lượt thử thủ công; kết quả cuối dùng kiểm tra trong Development Player).

[PLAYTEST screenshot] Development client kết nối riêng với Editor host (`server=False`, hai người chơi). Hai nhánh đều giữ nguyên mốc khi xoay 180° và quay lại; ảnh client 1920×1080 xác nhận chấm trong khung và chấm + mũi tên + số mét ở mép. Đổi nhánh ở client không thay đổi tiến độ nhiệm vụ trên server. Lượt kiểm cửa sổ thủ công hết hạn; phép kiểm sau chạy trên chính HUD của client và đã đạt.

```text
Development Player build: Succeeded; errors=0; warnings=22
6e482fb7  Test run completed: 1/1 passed.  (client)
Remote client 1, server=False, peers=2, feet=(-124.00, 0.08, -137.00): PASS FactoryIsolate 322 m: dot -> dot + turn arrow -> dot; anchor=(-76.80, 0.05, -128.00); FactoryManifest 266 m: dot -> dot + turn arrow -> dot; anchor=(-76.80, 0.05, -128.00)
```

[SCENE] Unity Editor trở lại Edit Mode với `GameScene`, `isDirty=false`. SHA-256 của scene khớp mốc trước lượt xác minh cuối (`3F27141F…4A0D33D`). Checkpoint chính `97370558…E6B688A` và bản dự phòng `57B13F37…057F8D` khớp trước/sau. `git diff --check` đạt cho các file đã sửa.

Phạm vi playtest là đầu Factory và hai đoạn đi thật đã nêu; chưa chơi hết campaign hoặc thử Tank sống trên client.
