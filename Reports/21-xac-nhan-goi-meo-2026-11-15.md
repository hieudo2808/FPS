# Xác nhận nguyên nhân gối hơi méo — 2026-11-15

## Kết luận

Góc xương không phải nguồn méo chính. Mesh body đang dùng Linear Blend Skinning và không có corrective blendshape cho tư thế gập gối; vì vậy vùng da ngoài gối bị kéo bẹt/gãy khi thigh và calf tạo góc khoảng 56°. Ánh sáng specular làm cạnh này lộ rõ hơn.

## Kiểm chứng

- `[UNITY MCP model_get_mesh_info]` Brimstone, Clove, Gekko và Sage đều có `blendShapeCount=0`, `isReadable=false`.
- `[CODE: Assets/FPS/Features/Characters/Player/Runtime/PlayerDownedPose.cs:24]` pose procedural đặt đùi phải nâng và dùng bảng Humanoid muscle.
- `[PLAYTEST] DownedBodyIsTidyOnEveryCharacter`: 1/1 passed; cả bốn nhân vật giữ `twist upper/lower = 0`, chân chạm đất, góc gối đo khoảng `56°`.
- `[PLAYTEST] DownedSidearmReviveAndDirectionalDeathAcrossPeers`: 1/1 passed; muzzle error `0.000°`, corrective gối trái/phải `0.000°`.
- Ảnh runtime cuối cùng: [3P-downed-closeup.png](PlayerLife-2026-11-14/fix/3P-downed-closeup.png) và [3P-downed-side.png](PlayerLife-2026-11-14/fix/3P-downed-side.png).
- Ảnh đối chiếu 4 prefab, góc nhìn ngang: `knee-proof-BrimstonePlayer.png`, `knee-proof-ClovePlayer.png`, `knee-proof-GekkoPlayer.png`, `knee-proof-SagePlayer.png` trong cùng thư mục evidence.
- Thử đổi mục tiêu `RightKneeBend` từ `55°` xuống `42°` chỉ đổi giá trị `Right Lower Leg Stretch` từ `1.00` xuống `0.38`; góc gối runtime vẫn `56°`. Thay đổi thử đã được hoàn tác vì muscle này kéo dài cẳng chân, không điều khiển góc gập gối.

## Cách sửa đúng

Muốn hết hẳn cần sửa asset: thêm pose-space corrective blendshape cho gối, hoặc chỉnh skin weights/corrective knee bone trong file FBX. Hạ góc bằng muscle hiện tại không phải đường sửa đúng và có thể làm sai chiều dài chân.

## Kỹ năng đã dùng

- `unity-skills/SKILL.md`: kiểm tra server trước, dùng REST skill và xác minh lại trong Editor.
- `unity-perception`: đọc trạng thái scene và thông tin mesh/rig trước khi kết luận.
- `unity-importer`: đọc `model_get_mesh_info` và `model_get_rig_info`.
- `unity-test`: chạy PlayMode test bất đồng bộ và kiểm tra kết quả.
- `unity-scene` / `unity-editor`: lưu scene trước và sau lượt test; kết thúc với `isDirty=false`.

