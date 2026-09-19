# Trực thăng với cửa cabin đóng/mở

## Sử dụng trong Unity

- Kéo `Prefabs/Mi24_DoorAnimated.prefab` vào scene; scale mặc định `(1,1,1)`.
- Trên Animator, đặt bool `DoorOpen = true` để mở; `false` để đóng.
- Ví dụ: `animator.SetBool("DoorOpen", true);`
- `DoorOpen`: 1,033 giây; `DoorClose`: 1,067 giây, 30 fps.
- Cửa mặc định đóng. Đổi lệnh giữa chuyển động sẽ đợi chuyển động hiện tại kết thúc
  rồi thực hiện lệnh mới, tránh nhảy vị trí cửa.
- Dùng bốn file `.anim` trong `Animations` cho Timeline/Animator tùy chỉnh. Chúng chỉ
  chứa đường dịch chuyển cửa; không ghi đè vị trí của trực thăng khi bay.
- FBX gốc có thêm các đường transform cố định do Blender bake. Ưu tiên các `.anim`
  đã lọc khi ghép với cinematic. Mũi model hướng về **-Z** trong Unity.

GameScene đã dùng bản cinematic `Mi24_Insertion` với rotor tách riêng và cảnh đu dây
28 giây. Prefab `Mi24_DoorAnimated` vẫn là bản độc lập để dùng Animator cửa.
Trực thăng cinematic không có collider gameplay và không phải phương tiện lái được.

## Intro đu dây trong GameScene — 2026-09-10

- Scene: `Assets/FPS/Scenes/GameScene.unity`.
- Model dành cho intro: `Models/Mi24_Insertion.fbx`,
  prefab `Prefabs/Mi24_Insertion.prefab`.
- Blender source: `ArtSource/Helicopters/Mi24/Mi24_Insertion.blend`.
  MainRotor và TailRotor tách từ mesh gốc, bảo toàn UV và material.
- `HelicopterInsertionRappel` trên Insertion_Helicopter lấy thời gian từ campaign
  server clock. Không dùng Timeline cũ cùng lúc để điều khiển chuyến bay.
- 0–5,5 giây: tiếp cận sân; 5,5–6,54: mở cửa; 7–8,1: thả hai dây.
- Server chốt danh sách 1–4 người sau khi mọi client đã kết nối có player sẵn sàng,
  nhận diện nhân vật từ prefab thực sự được spawn. Thứ tự theo client ID; tối đa
  hai người mỗi lượt. Chọn trùng nhân vật vẫn có hai bản hình ảnh riêng.
- Tâm đáp là dấu cộng gốc của sân: **X=-125, Z=-137, mặt sàn Y=0**.
  Solo dùng một dây ngay tâm; nhiều người dùng hai dây cân quanh tâm rồi tản ra
  đội hình cách nhau 2 m. Server đặt player thật vào đúng slot tương ứng trước
  khi bắt đầu đồng hồ intro và đợi xác nhận chuyển vị trí.
- Ngay khi từng người chạm đất, cast bật Vandal third-person và chuyển sang
  tư thế cầm súng có sẵn. Tay trống trong đoạn bám dây; sau khi xuống vẫn cầm
  súng khi bước ra điểm tập kết. Clip riêng InsertionArmedIdle/Walk được tạo
  từ locomotion và upper-body pose gốc, chỉnh đường dẫn bone theo từng nhân vật.
- 19,1–20,1: thu dây; 20–21,07: đóng cửa; 21,2–28: trực thăng bay đi.
- Thời lượng cấu hình tại CampaignSettings.insertionSeconds, mặc định 28 giây.
  Thay đổi giá trị sẽ co giãn toàn bộ phần trình diễn theo cùng tỷ lệ.
- Bốn Rappel_Double là template ẩn, có binding PlayerCharacterId rõ ràng.
  Chỉ tạo bản hình ảnh cho người có trong roster server; không có NetworkObject,
  collider hoặc logic player. Ngắt kết nối ẩn đúng người mà không đổi slot người
  còn lại. Người mới vào sau khi chốt đội spectate tới chuyển khu; reconnect đúng
  stable player ID dùng lại slot cũ. Player thật giữ nguyên object, HP và inventory;
  renderer khôi phục khi kết thúc. Đây chưa phải cơ chế đu dây điều khiển được.

Mở GameScene và dùng **Tools > FPS > Helicopter > Build insertion rappel presentation**
để dựng lại phần intro từ asset. Menu này thay thế phần hình ảnh trực thăng và
RappelPresentation của intro, do đó hãy lưu chỉnh sửa thủ công trước khi dùng.
**Align landing cross and roster** chỉ căn các marker theo dấu cộng gốc và cập
nhật binding nhân vật; không dựng lại kiến trúc hoặc model. Khi đổi template,
giữ thứ tự `rappellerCharacters` khớp `rappellers` trên component.
**Render and verify insertion** chụp các mốc trên những frame Editor riêng và
tự lưu scene sau khi hoàn tất; menu này dùng một đội mẫu bốn người để review.
Trong trận, roster luôn lấy từ server. Camera không có AudioListener riêng.

Các marker Flight_Approach / Flight_Hover / Flight_Departure nằm trong
`Cinematics/RappelPresentation`. Giữ marker ngoài root trực thăng để tránh
đường bay tự dịch theo máy bay. Rotor quay theo trục đã chuyển sang không gian
parent FBX; không giả định local Y của mesh nhập luôn hướng lên.

Ảnh `ArtSource/Helicopters/Mi24/Insertion_Live_0..3.png` chụp từ camera thực
trong Play Mode. Báo cáo tại `Documents/HelicopterInsertion/VERIFICATION.md`.

## Nguồn và nội dung

`Models/Mi24_DoorAnimated.fbx` is the Unity-ready export authored from the supplied
`Source/Mi24.glb`. The embedded mesh identifies itself as **SH-60B Seahawk Helicopter**;
the `Mi24` folder and filename are retained for project provenance and should not be
read as a verified Mi-24 identification.

The original sliding cabin-door geometry was separated from the body with its UVs and
materials intact. `CabinDoor_L_Glass` supplies the missing upper glazing and
`CabinDoor_L_LowerInset` closes the small lower through-hole without replacing the
original shell. The default pose is closed. The FBX is imported at file scale 1 with
Generic animation and contains `DoorClosed`, `DoorOpen`, `DoorOpened`, and `DoorClose`.

`Prefabs/Mi24_DoorAnimated.prefab` includes an Animator using
`Animations/Mi24_Doors.controller`; set the `DoorOpen` bool to play the open/hold or
close transition. The standalone `.anim` files are also available when a cinematic
needs direct clip control. The door translates aft 1.4 m after a short outward slide;
the body and prefab root remain stationary.

`ArtSource/Helicopters/Mi24/Mi24_Doors.blend` is the editable Blender source. The
repeatable Unity authoring/verification command is **Tools > FPS > Helicopter > Build
and verify door asset**. It rebuilds the material remaps, clips, prefab, and
`UnityVerification.json`; it does not edit campaign scenes or the source GLB.

## Xuất lại và kiểm tra

1. Mở `ArtSource/Helicopters/Mi24/Mi24_Doors.blend` từ thư mục gốc project.
2. Giữ hierarchy root → body/door → glass/inset; không đổi tên `CabinDoor_L`.
3. Timeline 1–110, 30 fps: đóng 1–8, mở 1–32, giữ mở 40–50, đóng lại 64–96.
4. Đặt frame 1 trước khi lưu/export. Chỉ export root, body, door và hai mesh con;
   loại camera/đèn review. FBX: -Z Forward, Y Up, FBX Units Scale, Bake Animation,
   không All Actions/NLA strips. `finalize_door.py` lưu các bước fit/export đã dùng;
   script này cần file `.blend` đã dựng, không tự xây lại từ GLB.
5. Lưu mọi scene và asset trong Unity, rồi chạy menu authoring ở trên. Menu tự save
   trước/sau, bảo toàn GUID của các clip/material/prefab đã tạo khi chạy lại.

Material Built-in dùng ba texture màu `_0`. Ba ảnh `_1` là dữ liệu packed theo
liên kết glTF gốc, không phải normal map: G = roughness, A × 0,5 = specular weight,
metallic factor gốc = 0. Các ảnh `UnitySpecGloss` là bản chuyển kênh riêng;
không sửa sáu ảnh nguồn.

Đã kiểm tra trực tiếp Unity: kích thước bao gồm rotor **18,585 × 7,119 × 24,160 m**;
4 mesh, 67.302 vertex nhập, 64.128 triangle; không thiếu material/script; thử
start/mid/end và chu trình bool mở–đóng đều đạt, root/body drift = 0.

Hành trình trượt aft 1,4 m, tách ngang 0,08 m, để tránh cụm phụ kiện phía sau.
Cửa mở còn che khoảng 0,12 m mép sau ô cửa, tương ứng vị trí đỗ cửa của model nguồn;
khoảng hở nhìn theo phương dọc thân khoảng 1,31 m. Đã kiểm tra không xuyên thân ở
các frame trượt 15/21/27/32/75. Khi đóng, vỏ cửa có phần chồng dưới mép khung;
không coi đây là kiểm định dung sai cơ khí hoặc collider. Ảnh review thể hiện fit thực tế.

Review images and fit data are in `ArtSource/Helicopters/Mi24/`:

- `Door_Closed_Final.png`, `Door_Open_Final.png` — Blender renders
- `Unity_DoorClosed.png`, `Unity_DoorOpened.png` — Unity preview renders
- `DoorFitVerification.json` — sampled Blender body/door overlap information
- `UnityVerification.json` — imported mesh, material, scale, clip, and Animator checks
