# Chẩn đoán chân còn hơi méo — 2026-11-15

## Kết luận

Ảnh crop cho thấy góc ngoài gối/ống quần vẫn bị gãy mặt nhẹ dù bone pose đúng. Đây là biến dạng Linear Blend Skinning của mesh khi đùi và cẳng chân xoay mạnh; corrective `*_Knee_Ndl` đã được giữ đúng nên không còn là nguyên nhân chính.

## Bằng chứng

- [PLAYTEST] Host/client sau fix ghi `knee corrective L_Knee: neutral orientation error=0.000 deg` và `R_Knee: 0.000 deg`; vị trí chân vẫn đúng mặt đất và gối phải cao hơn bàn chân.
- [CODE] `PlayerDownedPose` calibrate góc gối phải mục tiêu `55°`; runtime đo được khoảng `56°`. Vì vậy khớp xương không bị lật hoặc sai target rõ ràng.
- [UNITY MCP model_get_mesh_info] Brimstone, Clove, Gekko, Sage đều `blendShapeCount=0`; mesh body không có pose-space knee corrective shape. Các mesh cũng `isReadable=false`, nên không có dữ liệu weight trực tiếp từ query này.
- [UNITY MCP model_get_rig_info] Cả bốn body FBX là Humanoid, `CreateFromThisModel`, `sourceAvatar=null`. Runtime presenter lại bind clip bằng avatar Humanoid serialized của presentation. Sự khác biệt avatar retarget là nguồn sai số nhỏ thứ hai, nhưng không giải thích việc corrective đã đo đúng mà silhouette vẫn gãy.

## Vì sao ảnh vẫn thấy méo

Các đỉnh vùng gối đang được blend giữa thigh/calf theo weight hiện có. Khi hai bone tạo góc khoảng 56°, thể tích ngoài gối bị kéo thành mặt phẳng/góc cứng; material tối và đèn bên càng làm cạnh đó rõ hơn. Mesh không có blendshape hoặc dual-quaternion corrective để bù thể tích này.

## Hướng xử lý

- Giảm knee bend khoảng 10–20° là cách nhanh, ít rủi ro và giữ nguyên asset.
- Muốn gối tròn đúng ở góc hiện tại cần art fix: thêm pose-space corrective blendshape hoặc corrective bone/skin weights riêng cho gối.
- Đổi avatar retarget theo từng body có thể giảm sai số nhỏ, nhưng là phạm vi rộng hơn và cần kiểm tra lại cả death/downed clip.

Tôi chưa đổi góc gối trong lượt chẩn đoán này; thay đổi đó sẽ đổi silhouette cả bốn nhân vật. Rủi ro còn lại chủ yếu là skin weight/retarget của từng mesh, không phải logic grip.
