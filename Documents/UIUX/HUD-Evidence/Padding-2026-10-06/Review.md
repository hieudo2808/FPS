# HUD: khoảng đệm chữ và chỉ dẫn mét

Đã sửa khoảng đệm trong khung máu, Infection/Sepsis, vũ khí, nhiệm vụ, squad và hotbar. Chữ chỉ dẫn mét là con trực tiếp của marker, dùng outline để đọc rõ và không còn DistancePlate.

HUD chính có khoảng đệm chữ tối thiểu 16 đơn vị canvas; squad 16 ngang/12 dọc; hotbar 12 quanh chữ. Marker giữ toàn bộ vùng chữ cách safe area ít nhất 28 đơn vị canvas.

Kiểm tra GameScene ở 1280×720, 1920×1080, 3440×1440 với UI 100% và 125%: không cắt chữ, không chồng các nhóm HUD. Có kiểm tra Sepsis, đạn dự trữ 3 chữ số, squad downed, ADS, scope và các trạng thái campaign.

- GameplayStatesAndLayoutRemainReadable: PASS, job b4d2c08d, 1606 assertions.
- NavigationUsesLiveSceneAndInvalidatesConsistently: PASS trong job 97558e24, 60 assertions; xoay camera 360°, kiểm tra toàn bộ chữ trong safe area và không còn panel.
- Hai scene đã lưu sạch; Editor dừng, Console không có lỗi.
- MainMenu giữ nguyên SHA256 6F7123A0845A59AFD92156FBDFE8F02BD052994AB26474FAE04F1718FD258534.

Lượt đầu phát hiện ô đạn dự trữ quá hẹp, đã sửa. Bài test ADS cần input giả lập không phụ thuộc focus của Game View: cấu hình tạm được clone và khôi phục trong teardown, không ghi thay đổi binding hoặc cấu hình input vào project.

Các ảnh là trạng thái kiểm tra tạm trong Play Mode. `sepsis.png`, `stress-1280x720-125.png` và `ads.png` thể hiện các trường hợp quan trọng. Thư mục Before giữ bản sao trước khi sửa, bao gồm GameScene đã có bản sửa hố Factory.
