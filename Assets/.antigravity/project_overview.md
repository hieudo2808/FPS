# Tổng quan Dự án (Project Overview)

## 🎮 Thể loại Game
*   **Thể loại:** Co-op PvE First Person Shooter (FPS).
*   **Hành vi Mạng:** Drop-in / Drop-out (Vào ra tự do giữa trận).
*   **Số lượng người chơi:** Tối đa 4 người (1 Host + 3 Clients).

## 🌍 Core Systems (Hệ thống cốt lõi)
1. **Networking Core:** 
   - Sử dụng **Netcode for GameObjects (NGO)** làm xương sống đồng bộ.
   - Quản lý phiên chơi bằng **Unity.Services.Multiplayer (Sessions)** (Chuẩn Unity 6.3).
   - Truyền thông mạng qua **Unity Relay**, bỏ qua cấu trúc IP/Port cũ kỹ, thay bằng "Join Code" (Mã kết nối 6 chữ số).
2. **Dynamic GameDirector:**
   - Script tự động lắng nghe số lượng Client kết nối để tịnh tiến độ khó.
   - 1-2 người: Độ khó thường.
   - 3-4 người: x2 Máu quái vật, cho phép spawn Elites.
3. **Procedural Recoil (Giật súng eSports):**
   - Đạn giật theo *Pattern* (Mảng Vector2) cấu hình trong ScriptableObject (`RecoilPattern`).
   - Sử dụng Delta Recoil để tác động song song vào `MouseMovement`, cho phép người chơi thực hiện thao tác **Gìm Tâm (Control Recoil)** như Valorant hay CS2.
   - Hệ thống tự động hồi tâm (Return to Center) khi ngưng bắn.
