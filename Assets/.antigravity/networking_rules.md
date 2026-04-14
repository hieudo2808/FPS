# Quy tắc Mạng & Chắp vá Lỗi Cũ (Networking Rules & Errata)

Tài liệu này ghi chú lại các "Gotchas" (Nguồn gây lỗi) và bộ xương sống Networking đã thống nhất để sau này đụng vào Mạng không bị bỡ ngỡ.

## 1. Unity 6.3 - Unified Multiplayer Services
Chúng ta **KHÔNG** sử dụng `Relay` rời (Deprecated) hay SDK cũ. Tất cả mọi thứ phải khai qua `Unity.Services.Multiplayer` (Sessions).

### Nguồn Lỗi Chết Người Đã Khắc Phục (Do Not Re-introduce)
Vào thời điểm cập nhật, API `SessionOptions` từ Unity **CẤM/KHÔNG CÓ** các hàm fluent (Hàm nối đuôi dấu chấm) như `.WithMaxPlayers(4)` hay `.WithRelayNetwork()`. 
Cố tình gõ những đoạn code cũ trên mạng hay Tutorial cũ vào sẽ làm nổ trình biên dịch. 
👉 Cách làm đúng: Đắp thuộc tính thẳng vào Constructor (Khởi tạo Object).
```csharp
var options = new SessionOptions { MaxPlayers = 4 };
var session = await MultiplayerService.Instance.CreateSessionAsync(options);
```

### Gotcha Thứ Hai: Không Tự Động StartHost/StartClient
Trong Unity 6.3, `CreateSessionAsync` **CHỈ** lập thẻ trên Internet và móc dây vào Relay chứ **TUYỆT ĐỐI KHÔNG** khởi động hệ thống Mạng Gameplay (Netcode). Nó sẽ không tự gọi lệnh chạy Game.
👉 **Bắt Buộc:** Ngay sau câu lệnh CreateSession hoặc JoinSession, bạn phải chèn tay 2 dòng này vĩnh viễn:
```csharp
NetworkManager.Singleton.StartHost(); // Nếu là Host
// -- hoặc --
NetworkManager.Singleton.StartClient(); // Nếu là Client
```

## 2. Xác Báo & Tick Simulation (CSP)
Movement System của chúng ta là Server-Authoritative chạy qua kiến trúc Client-Side Prediction (CSP) tĩnh:
- Game hoạt động bất biến trên tốc độ `Fixed Tick = 60Hz` (Mọi thứ về mô phỏng di chuyển và vật lý không dùng `Time.deltaTime` ở vòng `Update`).
- Góc camera vật lý luôn được quản lý chuyên biệt bởi chuột (`MouseMovement.cs`), còn hướng thân thể được quay bởi Code nội suy bên ngoài để chống giật (Jitter).
