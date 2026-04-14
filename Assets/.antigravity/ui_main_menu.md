# Giao diện Main Menu (UI/UX)

## 1. Triết lý Thiết kế
Giao diện của game đi theo phong cách Tối giản Hiện đại (Modern Minimalist) như các tựa game eSports (Valorant, CS2). 
Tuyệt đối KHÔNG hiển thị các thuật ngữ kỹ thuật rườm rà như Cổng (Port), IPv4, IPv6 ra ngoài cho người chơi cuối (End-user) nhìn thấy.

## 2. Cấu trúc Cửa sổ (Window Management)
Toàn bộ hoạt động sảnh chờ được quản lý bởi `LobbyUI.cs` dưới dạng 3 nhóm Popup (tắt/bật `SetActive`):

- **Main Panel**: Nhóm nút điều hướng dọc ở cạnh trái (Play, Settings, Quit).
- **Play Popup**: Nằm đè lên Main Menu khi chạm vào nút Play. 
  - Bao gồm nút `HOST MATCH` để khởi tạo phòng.
  - Bao gồm ô Input gõ `JOIN CODE` (ví dụ: H8X2V9) và Nút `JOIN MATCH` để dò tìm phòng từ máy người khác.
- **Settings Popup**: Dành cho việc chỉnh UI/Cấu hình. Đặc biệt có ô Input để đặt tên người chơi (Lưu qua `PlayerPrefs.SetString("PlayerName")`).

## 3. Hoạt động của Code
Khi người chơi thao tác, UI tương tác trực tiếp lên `NetworkGameManager.cs` (đang có vai trò là cầu nối với Core Multiplayer):
- Bấm **Host Match** -> UI chuyển sang trạng thái Disable. -> Mạng gọi Unity Cloud sinh Code -> Nhận thành công, in ID phòng đó qua UI Text.
- Bấm **Join Match** -> UI bắt điều kiện ô ID không được rỗng -> Mạng chuyển Code đó yêu cầu đám mây cấp quyền -> Load vào Map chơi.
