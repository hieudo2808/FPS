<div align="center">

# Outbreak Protocol

[![Unity Version](https://img.shields.io/badge/Unity-2022.3.39f1-blue.svg)](https://unity.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)]()
[![Status](https://img.shields.io/badge/Status-In%20Development-yellow.svg)]()
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)

_Một tựa game sinh tồn góc nhìn thứ nhất (FPS) với Hệ thống AI Thích ứng (Adaptive AI)._

[Giới thiệu](#-giới-thiệu) •
[Tính năng nổi bật](#-tính-năng-nổi-bật) •
[Cơ chế kỹ thuật](#%EF%B8%8F-cơ-chế-kỹ-thuật) •
[Điều khiển](#-điều-khiển) •
[Cài đặt & Build](#-cài-đặt--build) •
[Roadmap](#-roadmap)

</div>

---

## Giới thiệu

Đây là một tựa game **First-Person Shooter (FPS) Zombie Survival** được phát triển trên **Unity**. Lấy cảm hứng từ những tựa game bắn súng sinh tồn nổi tiếng, trò chơi mang đến một hệ thống **AI Director** năng động, tự điều chỉnh nhịp độ và độ khó của game dựa trên kỹ năng của người chơi. Hãy thu thập vũ khí, tận dụng địa hình và cố gắng sống sót qua những đợt tấn công liên tiếp của thây ma trong bối cảnh một khu công nghiệp đầy căng thẳng!

## Tính năng nổi bật

- **AI Director (Hệ thống nhịp độ động)**: Trò chơi tự động luân chuyển giữa 3 giai đoạn: `BUILD` (Tích lũy), `PEAK` (Cao trào), và `RELAX` (Nghỉ ngơi) để tạo ra một vòng lặp nhịp độ hấp dẫn, không bao giờ nhàm chán.
- **AI Thích ứng (Adaptive AI)**: Các chỉ số của kẻ địch (sát thương, tốc độ) sẽ mở rộng dựa trên lượng máu và số lượng zombie bạn tiêu diệt được.
- **Hành vi Kẻ địch Thông minh**:
  - _Hệ thống Vị trí Tấn công (Attack Slot System)_: Đảm bảo zombie không bị chồng chéo, tự động xếp vị trí khi tấn công người chơi một cách tự nhiên.
  - _Rubber-banding_: Đảm bảo kẻ địch bị bỏ lại quá xa sẽ tự động tăng tốc hoặc dịch chuyển để bắt kịp người chơi.
- **Tối ưu Hàng đàn (Horde Optimization)**: Hỗ trợ 50+ zombie cùng lúc mà vẫn đảm bảo 60 FPS thông qua cơ chế Object Pooling.
- **Special Infected**: Boss zombie sở hữu các kỹ năng đặc biệt như Screamer (gọi bầy) giúp đa dạng hóa thử thách chiến đấu.

## Cơ chế kỹ thuật

Dự án áp dụng **Component-Based Architecture** kết hợp với các Design Pattern phổ biến nhằm đảm bảo hiệu năng và dễ dàng mở rộng tính năng:

- **Object Pooling (`ZombiePoolManager`)**: Khởi tạo trước kẻ địch, loại bỏ hiện tượng giật lag (GC spike) do quá trình Instantiate/Destroy sinh ra.
- **State Machine (FSM)**: Xử lý mượt mà các trạng thái của Zombie (Idle → Chase → Attack → Dead).
- **Behavior Tree (UniBT)**: AI ra quyết định phức tạp cho các Special Infected.
- **Decoupled Systems**: Giao tiếp giữa các hệ thống Gameplay, Audio và UI thông qua cơ chế Event-driven (Observer Pattern).

## Điều khiển

| Hành động           | Phím            |
| :------------------ | :-------------- |
| **Di chuyển**       | `W` `A` `S` `D` |
| **Xoay Camera**     | `Chuột`         |
| **Bắn**             | `Chuột Trái`    |
| **Ngắm (ADS)**      | `Chuột Phải`    |
| **Nạp đạn**         | `R`             |
| **Chạy nhanh**      | `Shift`         |
| **Nhảy**            | `Space`         |
| **Tạm dừng / Menu** | `Tab` / `Esc`   |

## Cài đặt & Build

### Yêu cầu hệ thống

- **Unity Engine**: `2022.3.39f1` (LTS) hoặc mới hơn.
- **Hệ điều hành**: Windows 10/11

### Hướng dẫn chạy (Play Mode)

1. Clone repository về máy:
   ```bash
   git clone <repository-url>
   ```
2. Mở dự án thông qua **Unity Hub**.
3. Mở scene chính tại: `Assets/Scenes/SampleScene.unity` hoặc `Assets/Map/Map_v2.unity`
4. Bấm nút **Play** ở giữa màn hình Editor để trải nghiệm.

### Hướng dẫn Build (Windows)

1. Trong Unity Editor, chọn `File > Build Settings`.
2. Đảm bảo platform đang chọn là **PC, Mac & Linux Standalone** (Target Platform: Windows).
3. Bấm **Build**, chọn thư mục lưu trữ rỗng và đợi tiến trình hoàn thành.

## Roadmap (Lộ trình phát triển)

- [x] Chuyển động người chơi & Cơ chế bắn súng
- [x] AI Zombie cơ bản (Chase, Attack)
- [x] AI Director điều khiển nhịp độ
- [x] Object Pooling & Attack Slot System
- [ ] Tích hợp hoàn chỉnh Special Infected (Screamer, Tank, Spitter,...)
- [ ] Cải thiện Giao diện Menu chính & Game Over
- [ ] Chức năng Save / Load
- [ ] Trau chuốt mảng Âm thanh (Audio Polish)
- [ ] Bổ sung đa dạng vũ khí

## Giấy phép & Tác giả

Dự án này được cấp phép theo giấy phép [MIT License](https://opensource.org/licenses/MIT).

- **Engine:** Unity
- **Assets tham khảo:** Survivalist FPS Pack, Industrial Building Pack, UniBT.
