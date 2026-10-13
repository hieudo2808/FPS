# Kiểm chứng Warden 3P — 08/10/2026

Bộ 3P hiện tại đã triển khai và chạy được **Hold, Aim, Equip, Reload** trên Brimstone, Clove, Gekko và Sage. Kết luận này dựa trên prefab thật, Animator của Unity và bài kiểm tra chạy lại; không chỉ dựa trên tên clip hay việc đã gán controller.

## Bằng chứng theo nhân vật

| Nhân vật | Controller người | Đường dẫn Transform khớp trong mỗi clip Warden | Xương xoay thân trên được đối chiếu với pose |
|---|---|---:|---:|
| Brimstone | BrimstoneWarden_GenericPathBound | 94/94 | 72 |
| Clove | CloveWarden_GenericPathBound | 95/95 | 73 |
| Gekko | CloveWarden_GenericPathBound | 95/95 | 73 |
| Sage | CloveWarden_GenericPathBound | 88/95 | 71 |

Gekko và Sage dùng đường dẫn skeleton tương thích với Clove, kể cả `L_Hand`, `R_Hand` và `R_WeaponMaster`. Súng trên cả bốn prefab được gắn dưới `MasterWeaponAim/MasterWeapon/R_WeaponMaster` và dùng `Warden3P_Gun.controller`.

Sage thiếu 7 đường dẫn helper: `IK_RootTarget`, hai `IK_FootTarget`, hai `Hand_IKpv_IUE` và hai `Foot_IKpv_IUE`. Đây cũng là những helper không tồn tại trong profile Vandal của Sage. Các xương tay, ngón tay và xương gắn súng được kiểm tra vẫn khớp; không thêm helper hoặc sửa rig để làm sạch số liệu.

## Clip và hành vi đã nối

| Động tác | Clip người đang chạy | Clip súng đang chạy |
|---|---|---|
| Cầm súng | Brimstone/Clove_Warden_Hold | Warden3P_Gun_Idle |
| ADS | Brimstone/Clove_Warden_Aim | Warden3P_Gun_Idle; hướng súng đi theo xương người |
| Rút súng | Brimstone/Clove_Warden_Equip | Warden3P_Gun_Equip |
| Nạp đạn | Brimstone/Clove_Warden_Reload | Warden3P_Gun_Reload |

Các clip người chạy ở layer `Upper Body Gun Pose`, với mask riêng cho skeleton Brimstone/Clove. Layer `Locomotion` vẫn điều khiển chân. Khi đặt `Speed=4`, cả bốn nhân vật giữ state `Warden Hold` ở thân trên; xương gối phải thay đổi khoảng 40,64–41,16° trong lượt lấy mẫu. Ảnh `*-Run.png` ghi lại pose này.

Equip có độ dài clip người 2,0417 giây và clip súng 4,0833 giây. Reload có độ dài 3,75 giây ở cả hai. Hai Animator nhận chung tiến độ action bằng `EquipNormalizedTime`/`ReloadNormalizedTime`; bài kiểm tra lấy mẫu theo tiến độ clip tương ứng, không giả định hai clip Equip có cùng độ dài.

## Bài kiểm tra có thể chạy lại

Chạy class `FPS.Tests.WardenThirdPersonTests` trong Unity Test Runner, chế độ EditMode. Kết quả cuối cùng: **4 passed, 0 failed**, job `6a97a89d`, hoàn tất lúc `2026-10-07 17:20:35Z` tức `00:20:35` ngày 08/10 ở Việt Nam.

Bài kiểm tra dùng luồng chọn súng thật của `WeaponManager` và `PlayerVisibilityController`, sau đó kiểm tra:

- Controller đúng theo nhân vật, chế độ `GenericPathBound`, avatar null và root motion tắt.
- Cả hai tay, ngón tay, xương gắn súng và các Transform được mask cho phép khớp pose lấy mẫu trực tiếp bằng `AnimationClip.SampleAnimation` của Unity. Ngưỡng sai lệch xoay là 0,2°, vị trí là 0,0001 đơn vị local.
- Equip và Reload ở các mốc 10%, 25%, 50%, 75%, 90%: state người/súng đúng, tham số tiến độ bằng nhau và pose các bộ phận súng khớp clip.
- Complete trả người về Hold, súng về Idle; magazine chính chuyển động lúc Reload và về vị trí lắp sau Complete.
- Aim vào/ra đúng; đổi về Vandal ẩn Warden và trả controller Vandal.
- Không có renderer hay curve `Magazine_Extra`, không gắn component gameplay `Weapon` lên súng 3P.

Các lần đối chiếu ban đầu bằng cách tự nội suy từng thành phần quaternion đã cho sai số ở DustCover. Kết quả cuối dùng bộ lấy mẫu native của Unity, thay vì nới ngưỡng kiểm tra để bỏ qua sai số đó.

## Ảnh và số liệu

- [Ảnh động Equip + Reload của cả bốn nhân vật](Warden-3P-Synced.gif): mỗi action có 21 mốc từ 0% đến 100%; body và súng cùng nhận tiến độ trước khi chụp.
- [Ma trận 16 pose: 4 nhân vật × Hold/Aim/Equip/Reload](Warden-3P-Matrix.png).
- Các ảnh gốc `Brimstone-*.png`, `Clove-*.png`, `Gekko-*.png`, `Sage-*.png` được render từ prefab thật trong preview scene của Unity, không lưu scene kiểm tra.
- [Kết quả Test Runner XML](EditMode-TestResults.xml).
- [Audit native: controller, binding, mask, pose, chạy và import Generic](Native-Audit.txt).

**Ảnh ban đầu đã sai ở quy trình capture:** mesh skinned của body chưa cập nhật theo các pose kế tiếp trong cùng lượt Editor, trong khi súng đã đổi pose. Vì vậy ảnh hiển thị tay ở pose cũ và súng ở pose mới. Ảnh hiện tại lấy mesh bằng `SkinnedMeshRenderer.BakeMesh(mesh, true)` sau khi cập nhật cả hai Animator ở từng mốc, có tính scale của model. Nhờ đó hình ảnh hiển thị đúng pose xương đang được kiểm tra. File `Warden-3P-Matrix-Before.png` giữ ảnh cũ để đối chiếu lỗi capture.

Audit thân trên ghi nhận sai lệch xoay 0° tại các mốc đã lấy mẫu so với clip authored. Phép đo bổ sung giữa pivot súng và tay phải có biến thiên so với Hold tới khoảng 3,39 cm và 1,02° khi thực hiện động tác. Đây là biến thiên của quan hệ pivot/khớp tay, không phải phép đo tiếp xúc từng ngón tay với mesh; không dùng nó để tuyên bố hình học khớp tuyệt đối ở mọi frame.

Các dòng `SOURCE MATCH` trong audit là phép so scalar curve thô theo tên bone với các FBX nguồn dự kiến. Chúng **không phải chứng minh clip giống hệt nguồn**: bind pose, cơ sở tọa độ bone và dấu quaternion có thể khác khi giữ rig từng nhân vật. Kết luận pose ở trên dựa vào clip đã authored chạy trên prefab thật.

## Giới hạn của kết luận

Tất cả 14 FBX TP/GNTP trong thư mục Warden vẫn được import **Generic**. Lượt kiểm chứng này không sửa rig, clip, controller hay prefab gameplay; chỉ tăng độ phủ bài test và lưu bằng chứng.

Không có clip/state Fire 3P riêng trong bộ hiện tại. Các clip crouch, LB và biến thể montage không được nối thành các state Warden riêng; controller hiện tại cũng không có tham số crouch. Vì vậy, đã chứng minh bốn động tác được chọn hoạt động trên bốn nhân vật; không tuyên bố mọi FBX được sử dụng, có anim Fire riêng, hay mọi frame đều không xuyên mesh.

Đây là kiểm tra Mecanim trên prefab trong EditMode, có lấy mẫu locomotion; không phải một lượt kiểm tra multiplayer host/client từ đầu đến cuối.
