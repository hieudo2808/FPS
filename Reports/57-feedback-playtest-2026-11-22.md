# Feedback Playtest — Outbreak Protocol (2026-11-22)

- **Ngày ghi nhận:** 2026-11-22
- **Commit tham chiếu:** `37f54d20` (`main`)
- **Mục đích:** Ghi nhận phản hồi trực tiếp từ trải nghiệm chơi thử để làm cơ sở lập kế hoạch sửa lỗi và cải thiện trải nghiệm (UI/UX, Game Feel, Combat, AI & Spawn).

---

## Bảng tổng hợp nhanh

| # | Nhóm | Tóm tắt vấn đề |
|---|---|---|
| 1 | UI/UX | UI sơ sài, cứng; font chữ còn nhiều lỗi; một số nút bấm không tương tác được |
| 2 | Flow / Loading | Thiếu màn hình loading khi bắt đầu vào trận (hiện bị đơ trong lúc load) |
| 3 | Game Feel / Player | Di chuyển của nhân vật bị cứng, thiếu độ rung nhẹ (head bob) khi đi bộ/chạy |
| 4 | Combat / Survival | Ném lựu đạn bị lỗi (ném không trúng zombie, không aim ném được); hiệu ứng particle nên là vụ nổ thay vì lửa |
| 5 | AI / Navigation | Một số zombie bị lạc phương hướng, bị kẹt hoặc chạy ra xa thay vì tấn công khi player lại gần |
| 6 | UI/UX | UI trang bị và các nút bấm chưa rõ ràng, khó nhìn |
| 7 | Game Feel / Feedback | Thiếu animation, dấu hiệu UI và độ rung camera khi bị tấn công → bị tụt máu nhưng không nhận biết được |
| 8 | AI / Spawn & Awareness | Spawn enemy sau lưng hơi khó ngay từ đầu trận; thiếu dấu hiệu cảnh báo địch ở phía sau *(Optional: thêm mini-map kiểu GPS chấm đỏ báo địch)* |
| 9 | UI / World Guidance | Thiếu icon hiển thị vị trí nhặt trang bị trên map |
| 10 | AI / Detection | Enemy giai đoạn đầu phát hiện player từ quá xa; nên yêu cầu phát hiện trong tầm/tầm nhìn mới tấn công |

---

## Chi tiết 10 mục Feedback

### 1. Giao diện (UI), Font chữ và Nút bấm
- UI hiện tại đang khá sơ sài và có cảm giác cứng.
- Chữ viết / font chữ vẫn còn nhiều lỗi hiển thị.
- Một số nút bấm (button) không bấm được.

### 2. Màn hình Loading khi vào trận
- Cần bổ sung **màn hình loading** lúc bắt đầu trận vào game để tránh cảm giác bị đơ màn hình trong quá trình tải scene/tài nguyên.

### 3. Cảm giác di chuyển của Player (Head Bob / Camera Feel)
- Player di chuyển hiện đang bị cứng.
- Cần bổ sung độ rung nhẹ tự nhiên cho camera/tay cầm vũ khí theo nhịp chuyển động khi **đi bộ** và **chạy**.

### 4. Cơ chế ném lựu đạn & Hiệu ứng nổ
- Phần ném lựu đạn đang bị lỗi: có những lúc ném không trúng zombie và không hỗ trợ ngắm (aim) quỹ đạo ném.
- Hiệu ứng particle khi kích nổ nên sử dụng kiểu **vụ nổ (explosion)** thay vì hiệu ứng lửa như hiện tại.

### 5. Lỗi điều hướng / hành vi của Zombie
- Một số zombie bị lạc phương hướng, không tấn công player.
- Xuất hiện tình trạng zombie bị kẹt hoặc tự chạy ra xa khi player lại gần.

### 6. UI Trang bị và Chỉ dẫn nút bấm
- UI hiển thị trang bị và các nút bấm điều khiển tương ứng chưa được làm rõ, hơi khó nhìn trong lúc chơi.

### 7. Phản hồi khi Player bị tấn công (Hit / Damage Feedback)
- Thiếu animation, dấu hiệu UI (vignette/hit indicator/flash) và độ rung camera khi bị zombie tấn công.
- Dẫn đến việc người chơi bị tụt máu mà không nhận biết được mình đang bị đánh.

### 8. Spawn Enemy sau lưng & Cảnh báo hướng địch
- Việc spawn enemy sau lưng ngay từ giai đoạn đầu trận đang hơi khó và gây bất ngờ thiếu công bằng.
- Thiếu dấu hiệu nhận biết (âm thanh/UI chỉ hướng) để biết có enemy ở phía sau mà quay lại xử lý.
- **Đề xuất mở rộng (Optional):** Làm một bản đồ nhỏ kiểu **GPS / radar** ở góc màn hình, đánh dấu màu đỏ cho quân địch để dễ nhận biết enemy có đang spawn và ở hướng nào.

### 9. Icon hiển thị vị trí nhặt trang bị
- Bổ sung icon hiển thị vị trí các điểm nhặt trang bị trên map/HUD để người chơi dễ định hướng và tìm đồ.

### 10. Tầm phát hiện của Enemy giai đoạn đầu
- Ở giai đoạn đầu trận, một số enemy nên chuyển sang cơ chế **phải phát hiện player** (trong tầm nhìn / khoảng cách phù hợp) mới bắt đầu tấn công, thay vì phát hiện và lao vào từ rất xa như hiện tại.

---

## Đề xuất hoàn thiện Hệ thống Nhiệm vụ, Nhật ký & Hướng dẫn người mới

### A. Một trục Lore & Bảng từ điển thống nhất (Single Source of Truth)
- Toàn bộ màn hình (**Menu, Loading, Tracker, Prompt `[F]`, Panel giải đố, Nhật ký, Radio**) cần dùng **cùng 1 bộ ngôn ngữ** (ưu tiên 100% Tiếng Việt hoặc 100% Tiếng Anh, không trộn nửa Anh nửa Việt) và **cùng 1 bộ danh xưng** xuyên suốt.

### B. Cấu trúc hiển thị Quest 3 tầng trên HUD (Why → What → Where)
- **Dòng 1 (Mục tiêu chương — Why):** Ngắn gọn 1 ý, gắn trực tiếp cốt truyện:
  - **Ch.1 Nhà máy:** `THU HỒI KIỆN T9-17`
  - **Ch.2 Viện Northbridge:** `TRUY DẤU BỆNH NHÂN P046 & XUỐNG TẦNG B2`
  - **Ch.3 Lab B2:** `TRUYỀN TOÀN BỘ HỒ SƠ GỐC E-02`
- **Dòng 2–3 (Bước cụ thể — What):** Luôn bắt đầu bằng động từ hành động và khớp 100% tên với Prompt `[F]`:
  - **Ch.1 (2 nhánh rõ ràng):**
    - `Nguồn điện (0/3)`: Ngắt cầu dao Sản xuất → Bật dự phòng Kho lạnh → Khởi động máy phát.
    - `Vận đơn (0/2)`: Đọc Vận đơn giao hàng → Đối chiếu phiếu xuất kho.
    - *(Sau khi xong 2 nhánh)*: Hạ Tank để tiếp cận Kho lạnh → Thu hồi kiện T9-17 → Mở cửa tuyến kỹ thuật.
- **Waypoint ngoài cảnh (Where):** Chấm chỉ đường luôn hiển thị cùng nhãn ngắn với dòng bước hiện tại (ví dụ: `Cầu dao Sản xuất · 18m`), không hiện tên chung chung như `Source Document`.

### C. Tách Nhật ký & Nhiệm vụ ra khỏi Túi đồ + Cải thiện UX đọc/tra cứu
- **Tách riêng khỏi Túi đồ:** Không gộp Nhật ký/Nhiệm vụ chung trong `Squad Inventory`; gán **1 phím riêng** để mở Nhật ký và hiển thị tracker gọn ở **góc trái màn hình** (kiểu quest tracker của Genshin Impact).
- **Trình bày Nhật ký dễ đọc lướt:** Viết ngắn gọn hơn và **bôi màu nổi bật (xanh dương)** cho các từ khóa, mã số, ký hiệu đặc biệt để dễ nhận diện ngay.
- **Pause hoặc Panel tóm tắt gọn trên HUD:**
  - Khi mở Nhật ký toàn màn hình hoặc mở UI lúc nhặt đồ thì nên **pause game lại** để người chơi yên tâm đọc (hoặc hỗ trợ cơ chế an toàn tương ứng).
  - Nếu muốn vừa chiến đấu vừa xem manh mối, làm thêm **1 panel nhỏ trên HUD** (ở góc hoặc phía trên màn hình) tóm tắt sẵn đoạn nhật ký kèm các ký hiệu/mã quan trọng.
- **Hướng dẫn trực quan, không bắt nhớ nhiều:** Hiển thị rõ ràng, trực tiếp dùng màu hoặc HUD nổi bật việc cần làm ngay trên 1 UI nhỏ ở góc/trên đầu màn hình để người chơi mới hiểu ngay cần làm gì, **không bắt người chơi phải ghi nhớ hay mở đi mở lại file nhiều lần**.

### D. Gợi ý nhanh hơn & Phím quét lại vị trí nhặt đồ
- **Rút ngắn nhịp gợi ý 3 cấp:** Giảm thời gian chờ gợi ý xuống **`25s / 45s / 75s`** (mức `45s / 90s / 135s` hiện tại quá dài).
- **Màu sắc gợi ý nổi bật:** Hiển thị các dòng gợi ý bằng 1 màu dễ nhận diện thống nhất (**màu vàng hoặc xanh dương**).
- **Phím ping/quét lại vật phẩm (ví dụ phím `V`):** Khi bấm sẽ hiện lại các **chấm tròn đánh dấu vị trí nhặt đồ / mục tiêu** trên map để người chơi dễ biết chỗ đi nhặt trang bị.


###Optional: Nên thêm 1 số particle hiệu ứng cho hồi hp
