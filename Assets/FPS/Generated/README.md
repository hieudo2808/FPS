# Assets/FPS/Generated

Thư mục này chứa toàn bộ các tài nguyên tự động (procedural / automated assets) được tạo ra bởi các công cụ Script & Editor trong dự án.

## Cấu trúc thư mục

- `Animations/HandAnimations`:
  - `AllSources/`: Chứa các tệp Animation Clip (`.anim`) tự động sinh từ quy trình Retargeting tay nhân vật (xem `HandAnimationRetargeter.cs`).
  - `FP_Classic/`: Chứa các Animator Override Controller (`.overrideController`) và clip tương ứng cho từng nhân vật (Brimstone, Gekko, Sage, v.v.).

## Lưu ý

- Các tệp trong thư mục này được tạo tự động thông qua menu Unity Editor: **FPS > Animation > Build Hand Retargeted Animations**.
- Không chỉnh sửa trực tiếp các tệp `.anim` tự sinh trừ khi cần thiết; khi chạy lại quy trình build retarget, các tệp này có thể được cập nhật tự động.
