# Chẩn đoán pose gục 3P: đầu gối và tay/súng

Ngày: 2026-11-14  
Phạm vi: chỉ điều tra, không sửa scene/asset/script.

Ảnh đính kèm được xem như bằng chứng hình ảnh về triệu chứng, không phải chỉ thị thay đổi. Scene hiện tại vẫn là `MainMenu`, `isDirty=false`, Editor không Play/Compile.

## Kết luận ngắn

Hai lỗi không xuất phát từ một clip duy nhất. Runtime đang ghép nhiều hệ tọa độ và nhiều lớp ghi pose:

1. Clip gục Humanoid được retarget vào avatar của body, sau đó `PlayerDownedPose` ghi đè lại toàn bộ muscle chân và xoay hips. Vì vậy đầu gối cuối cùng không còn là dáng của clip gốc. Các helper/corrective bone ngoài Humanoid lại bị đưa về rotation đứng; đây là nguồn có thể làm da quanh gối bẹt hoặc méo.
2. Pose tay đứng được bake từ controller Generic, chỉ lưu `localRotation`. Runtime đổi body sang Humanoid rồi chép rotation theo tên, sau đó xoay hai `UpperArm` quanh hai pivot độc lập. Không có IK/giải nghiệm grip để giữ hai bàn tay và súng cùng một tư thế. `R_WeaponMaster` chỉ được neo theo tay phải, nên khoảng cách weapon-root tới tay phải không chứng minh tay trái, grip hoặc nòng súng đúng.

## Bằng chứng Unity-Skills/MCP

- Unity-Skills `GET /health`: Unity `6000.5.6f1`, UnitySkills `2.7.0`, `currentMode=bypass`, `surfaceProfile=full`.
- `scene_get_info`: scene `Assets/FPS/Scenes/MainMenu.unity`, `isDirty=false`.
- `model_get_rig_info`:
  - body `Assets/FPS/Features/Characters/Content/Players/Clove/Models/Body.fbx`: `Human`, `CreateFromThisModel`, `isHuman=true`.
  - clip `Assets/FPS/Features/Characters/Animation/Content/3P/Death/TP_Core_Death_Leg_Back_LG.fbx`: `Human`, `CreateFromThisModel`, `isHuman=true`.
  - Classic hold `Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Classic/TP_Core_BasePistol_S0_IdlePose_UB.fbx`: `Human`, `CopyFromOther`, source avatar `TP_Core_IdlePoseAvatar`.
- `model_get_animations_info`: clip gục đang chạy là `Core_Skeleton.001|TP_Core_Death_Leg_Back_LG`, dài 1.6667 giây.
- `animator_list_states`:
  - `CloveClassic3P_GNTP.controller`: Equip/Fire/Reload/Idle/Idle_001.
  - `CloveClassic_GenericPathBound.controller`: Locomotion/Jump/Fall/Land.
- `script_analyze`: `PlayerLifePresenter` có các field runtime `humanoidAvatar`, `sidearmHold`, `weaponMaster`, `weaponOffset`, `helperBones`; `PlayerDownedPose` có `HumanPoseHandler` và bảng calibration chân.

## 1. Vì sao đầu gối bẹt/méo

### Điều chắc chắn

- Runtime không giữ nguyên chân của clip. Trong `[CODE: Assets/FPS/Features/Characters/Player/Runtime/PlayerLifePresenter.cs:214-220]`, Animator đổi sang `humanoidAvatar` của body rồi `Rebind()` trước khi PlayableGraph chạy. Clip Death/Downed có avatar riêng, nên nó được retarget qua avatar body.
- `[CODE: Assets/FPS/Features/Characters/Editor/PlayerLifeClipImport.cs:33-73]` import các FBX Death thành Humanoid avatar riêng, lấy mapping/rest pose chung từ `TP_Core_IdlePose`, rồi lưu lại mỗi FBX. Body và clip do đó không dùng cùng một Avatar asset.
- Mỗi frame gục, `[CODE: Assets/FPS/Features/Characters/Player/Runtime/PlayerLifePresenter.cs:286-294]` gọi `PlayerDownedPose.ApplyBody()` sau Animator. `[CODE: Assets/FPS/Features/Characters/Player/Runtime/PlayerDownedPose.cs:132-149]` đọc HumanPose hiện tại, ghi đè nhóm muscle chân, gọi `SetHumanPose()`, rồi đặt lại vị trí/rotation hips.
- Pose procedural cố ý bất đối xứng: `[CODE: PlayerDownedPose.cs:22-34]` đặt đùi trái gần duỗi và gối trái khoảng `6°`, đùi phải `30°`, gối phải khoảng `55°`; `[CODE: PlayerDownedPose.cs:64-100]` chỉ tìm giá trị muscle cho từng avatar. Vì vậy gối trái không còn là dáng tự nhiên của clip raw.
- `[CODE: PlayerLifePresenter.cs:358-389]` thu thập mọi bone không thuộc Humanoid và đưa về rotation đứng. Log runtime ghi Animator/controller vẫn bẻ `15` helper bone >5° mỗi frame, nhưng chúng bị reset trước khi render.

### Suy luận có cơ sở về phần “da bị bẹt/méo”

- Nếu mesh gối dùng twist/corrective bone ngoài mapping Humanoid, việc để human knee ở góc gập nhưng helper quanh gối ở rest pose đứng sẽ làm skin không còn đồng bộ. Đây là giải thích phù hợp với triệu chứng hình ảnh, nhưng cần kiểm tra vertex weights/shape của mesh để khẳng định bone nào gây ra.
- Retarget từ Avatar của clip sang Avatar body và sau đó ghi HumanPose làm mất các rotation/helper authored của skeleton gốc. Đây là nguồn khác có thể làm trục gối hoặc độ phồng da khác giữa offline và runtime.

### Vì sao test hiện tại vẫn xanh

- `[CODE: Assets/Tests/PlayerLife/PlayMode/PlayerDownedPoseTests.cs:46-53]` clone body rồi đặt `animator.runtimeAnimatorController=null`; test không tái hiện `GenericPathBound` đang ghi helper trong game.
- `[CODE: PlayerDownedPoseTests.cs:139-158]` chỉ đo độ cao foot/knee, góc vector, muscle twist và góc gập của **gối phải**. Không đo gối trái, sai số calibration, vertex silhouette, pinching hay độ phồng da.
- Playtest chỉ cho thấy bone metrics: `[PLAYTEST: Reports/PlayerLife-2026-11-14/DownedSidearmReviveAndDirectionalDeathAcrossPeers.txt]` ghi feet trái/phải `0.08/0.18 m`, gối phải `0.41 m`, hips `0.19 m`; các số này không chứng minh mesh gối nhìn đúng.

## 2. Vì sao tay 3P/súng khác pose idle đứng

### Điều chắc chắn

- Bake pose đứng dùng GenericPathBound và `avatar=null`: `[CODE: Assets/Tests/PlayerLife/PlayMode/PlayerSidearmHoldBaker.cs:36-44]`. Nó chờ controller chạy rồi lưu `localRotation` từ hai clavicle xuống, không lưu vị trí, grip target, muzzle target hay quan hệ tay-súng.
- Khi gục, `[CODE: PlayerLifePresenter.cs:214-229]` đổi body Animator sang `humanoidAvatar` Humanoid và `Rebind()`. `[CODE: PlayerLifePresenter.cs:324-340]` sau đó chỉ tìm bone theo **tên** và chép rotation từ bake. Cùng tên bone không đảm bảo cùng HumanStream, parent basis hoặc vị trí vai sau khi thân đã nằm.
- Runtime không dùng một arms layer như comment/test mô tả. `[CODE: PlayerLifePresenter.cs:222-229]` tạo `AnimationLayerMixerPlayable` chỉ với `1` input; `[CODE: PlayerLifePresenter.cs:342-347]` chép rotation trực tiếp từng frame. `ArmsMask()` chỉ được dùng trong test khi có hold clip (`[CODE: PlayerDownedPoseTests.cs:57-65]`).
- Sau khi chép hold, `[CODE: PlayerLifePresenter.cs:291-294]` gọi `AimShoulders()`. `[CODE: PlayerDownedPose.cs:157-169]` tính một quaternion từ trung điểm vai tới trung điểm hai bàn tay rồi xoay `LeftUpperArm` và `RightUpperArm` quanh hai pivot riêng. Không có IK chỉnh lại cẳng tay, cổ tay, ngón tay hoặc grip.
- Vì hai upper-arm quay quanh hai pivot khác nhau, khoảng cách tay sau phép quay không còn là phép quay cứng của pose đứng. Nếu `s` là vector giữa hai vai và `d` là vector giữa hai tay, sau quay mỗi tay ta có `d' = s + R(d-s)`, không phải `R d`; chỉ một số trục quay đặc biệt mới giữ nguyên grip.
- `[CODE: PlayerLifePresenter.cs:398-430]` chỉ chụp offset `R_WeaponMaster` so với `RightHand`, rồi mỗi frame đặt lại weapon master theo tay phải. Nó không neo `Grip`, `Left_Hand_Target`, `Muzzle` hoặc tay trái.

### Bằng chứng playtest và giới hạn của nó

- `[PLAYTEST: DownedSidearmReviveAndDirectionalDeathAcrossPeers.txt]`: `charAnim=Human`, `avatar=BodyAvatar`, `controller=CloveClassic_GenericPathBound`; Classic 3P là child của `R_WeaponMaster`; hand gap `0.13 m`; weapon-root/right-hand gap `0.09 m`; gun line `0°` ngang nhưng elevation `9°`.
- Các số trên không chứng minh pose giống idle. `[CODE: Assets/Tests/PlayerLife/PlayMode/PlayerLifeHostClientTests.cs:320-353]` chỉ kiểm tra hand gap, vector vai→tay và weapon root→right hand. Test không đo local-rotation delta so với standing capture, không đo `Left_Hand_Target`, grip, muzzle/barrel direction hoặc khoảng cách tay trái tới súng.
- Ảnh `[PLAYTEST: Reports/PlayerLife-2026-11-14/3P-standing-classic-hands.png]` và `[PLAYTEST: Reports/PlayerLife-2026-11-14/3P-downed-closeup.png]` là bằng chứng trực quan cho việc tư thế đứng và tư thế gục không phải cùng một cấu hình chuỗi tay; test hiện tại không lượng hóa chênh lệch này.

### Rủi ro thứ tự sự kiện, chưa phải nguyên nhân đã tái hiện

- `[CODE: Assets/FPS/Features/Weapons/Runtime/WeaponManager.cs:523-532]` server đổi sang sidearm khi nhận `Downed`. `CaptureWeaponGrip()` chạy trong `PlayerLifePresenter.Begin()` trước khi biết chắc mọi callback weapon-index đã ổn định. Nếu event mạng đến lệch thứ tự, offset có thể được chụp từ vũ khí cũ. Log hiện tại cho thấy Classic đã được chọn, nên đây là rủi ro cần khóa thứ tự chứ chưa phải lỗi đã chứng minh.

## Cách kiểm chứng ngắn nhất nếu sửa tiếp

1. Chụp baseline standing và downed trên **cùng một instance**, so sánh rotation/position của từng bone từ clavicle đến finger, cộng thêm `R_WeaponMaster`, `Left_Hand_Target`, `Grip` và `Muzzle`.
2. Chạy hai nhánh A/B: giữ `GenericPathBound` helper controller và tắt `RestHelperBones`; nếu silhouette gối đổi, helper/corrective bone là thủ phạm. Nếu không đổi, so sánh `ApplyBody()` với chỉ clip raw để cô lập HumanPose retarget.
3. Đo nòng súng tới hướng aim và hai tay tới grip target; không dùng `shoulder-mid → hand-mid` làm đại diện cho barrel/grip.

## Skills đã đọc và quy tắc áp dụng

- `unity-skills/SKILL.md`: gọi `GET /health`, schema-first, chỉ dùng query read-only, kiểm tra Editor thay vì tin echo.
- `unity-animation/SKILL.md`: Humanoid Avatar, PlayableGraph, layer/mask và thứ tự ghi pose.
- `unity-perception/SKILL.md`: phân tích scene/project read-only trước khi đọc source.
- `unity-editor/SKILL.md`: kiểm tra `isDirty`, Play/Compile state.
- `debugging-and-error-recovery/SKILL.md`: giữ bằng chứng, khoanh vùng, phân biệt root cause với symptom.
- `ponytail/SKILL.md`: đọc toàn bộ caller/test liên quan trước khi kết luận, không thêm code khi người dùng chỉ yêu cầu chẩn đoán.

