# Báo cáo kỹ thuật toàn bộ vấn đề animation vũ khí 3P

Ngày tổng hợp: 2026-09-02  
Phạm vi: Body 3P, vũ khí 3P, clip TP/GNTP, Avatar, hierarchy, Animator layer, transition, runtime/network và công cụ preview của năm súng `Vandal`, `Classic`, `Operator`, `Odin`, `Bucky`.

## 1. Mục đích và mức độ chắc chắn

Tài liệu này là postmortem kỹ thuật của toàn bộ đợt sửa animation 3P. Mục tiêu là giải thích không chỉ “đã sửa gì” mà cả vì sao những cách thử ban đầu thất bại, vì sao clip chạy đúng trong Animation Preview nhưng méo ở runtime, và cấu trúc nào phải được giữ khi thêm súng hoặc nhân vật mới.

Quy ước về bằng chứng:

- **Sự thật đã xác nhận**: có thể đối chiếu trực tiếp trong code, prefab, controller, test hoặc log hiện tại.
- **Suy luận hợp lý**: giải thích phù hợp với triệu chứng và nguyên lý Mecanim, nhưng trạng thái asset ở đúng thời điểm rất sớm của đợt sửa không còn được lưu nguyên vẹn để tái dựng bit-for-bit.
- **Không kết luận quá mức**: không coi Humanoid là “hỏng”, không coi mọi chuyển động khuỷu tay lạ là lỗi rig, và không nói Generic có thể tự retarget hai skeleton bất kỳ.

## 2. Kết luận ngắn

Lỗi không xuất phát từ một nguyên nhân duy nhất. Có ba nhóm lỗi độc lập từng tạo ra các biểu hiện rất giống nhau:

1. **Sai mô hình binding/ownership ngay từ asset import**: người dùng chỉ lấy nguyên TP clip gốc đưa vào controller, không tự tạo hay gán Avatar mới. Tuy vậy, các FBX TP gốc đã được Unity import sẵn là Humanoid và `Copy From Other Avatar` của `TP_Core_IdlePose`. Khi phát trên Clove, Unity dùng Human/Muscle motion được tạo từ TP Core rig thay vì direct Transform motion của đúng hierarchy Clove. Clip còn cần cả hai tay và nhánh `MasterWeapon`, trong khi các helper bone đó không thuộc `HumanBodyBones`.
2. **Sai không gian Transform của clip súng**: GNTP có root scale thường là `1`, trong khi một số Animator root của prefab súng được author ở scale `100`. Khi clip ghi root Transform lên prefab, súng hoặc băng đạn bị co, biến mất hay bay xa.
3. **Sai phối hợp runtime**: Body và súng là hai Animator khác nhau; clip có thời lượng khác nhau, layer additive còn hoạt động khi reload, transition tự thoát độc lập và state cũ có thể rò sang vũ khí mới. Một clip đơn lẻ vẫn preview đúng nhưng tổ hợp runtime làm cổ, tay và súng trông như bị biến dạng.

Giải pháp production hiện tại gồm:

- Clove dùng profile **Generic Path-Bound**, `Avatar=None`, curve Transform trực tiếp trên đúng hierarchy và mount súng dưới `R_WeaponMaster`.
- Clip nguồn được sao chép và chuyển đổi; không sửa FBX/model/rest pose nguồn.
- Clip GNTP của súng được chuẩn hóa root position/rotation/scale theo prefab và quy đổi translation của child bone.
- Reload/Equip của Body và súng dùng cùng completion trigger và cùng deadline gameplay.
- Fire được khớp với `WeaponData.FireInterval`; Fire additive bị hủy khi Reload/Equip bắt đầu.
- Runtime đổi cả controller lẫn Avatar profile rồi gọi `Animator.Rebind()` để xóa binding stream cũ.
- Trạng thái action được reset khi đổi slot và khi thay primary weapon trong cùng slot.

## 3. Thuật ngữ và hierarchy liên quan

### 3.1. Hai nhóm animation khác nhau

- **TP/Core/Body clip**: điều khiển thân người, hai tay, một phần locomotion và các helper bone dành cho vũ khí.
- **GNTP gun clip**: điều khiển skeleton riêng bên trong prefab súng, ví dụ bolt, slide, magazine, shell hoặc feed mechanism.
- **1P**: tay và vũ khí nhìn từ camera người chơi, dùng `FPAnim` và hierarchy 1P riêng.
- **3P**: full-body nhân vật và prefab súng mà người chơi khác nhìn thấy.

Body và gun clip không thể thay thế cho nhau. Body clip đặt tay và nhánh giữ súng; GNTP clip làm chuyển động cơ khí bên trong súng.

### 3.2. Nhánh quan trọng của Clove

Hierarchy production mà Generic clip nhắm tới:

```text
Body                                  <- Animator root hiện tại
└─ CS_Smonk_S0_Skelmesh.ao
   └─ Skeleton
      └─ Root
         └─ Splitter
            ├─ Spine1
            │  └─ ... hai vai, khuỷu, bàn tay, ngón tay ...
            └─ MasterWeaponAim
               └─ MasterWeapon
                  └─ R_WeaponMaster   <- parent production của súng Clove
```

Các tên như `MasterWeaponAim`, `MasterWeapon`, `R_WeaponMaster`, `WeaponPoint` và `R_WeaponPoint` là helper/weapon bone. Chúng không đồng nghĩa với `R_Hand`, và không phải bone người chuẩn trong Humanoid Avatar.

`Animator` có thể đặt ở `CS_Smonk_S0_Skelmesh.ao` **nếu** toàn bộ binding path được viết lại tương đối với root đó. Tuy nhiên production hiện tại đặt Animator ở `Body`, nên clip sinh ra có prefix `CS_Smonk_S0_Skelmesh.ao/...`. Chỉ kéo Animator xuống model root mà không rebake path sẽ làm toàn bộ binding lệch một cấp.

## 4. Bảng triệu chứng, nguyên nhân và cách sửa

| Triệu chứng | Nguyên nhân gốc đã xác nhận hoặc có bằng chứng mạnh | Cách sửa production |
|---|---|---|
| Chỉ kéo nguyên TP clip gốc vào controller đã làm model vặn, co hoặc trông chỉ còn bộ xương | Dù người dùng không gán Avatar, TP FBX đã được import là Humanoid và Copy From `TP_Core_IdlePose`; Human motion của source rig được áp lên BodyAvatar/rest axes của Clove, còn helper weapon branch không nằm trong Human stream | Tạo file `.anim` mới dạng Generic Path-Bound: sample Transform từ source rồi ghi curve vào đúng path Clove; production dùng file mới thay vì clip Humanoid gốc |
| Súng nằm giữa người hoặc lệch khỏi nách/hông | Gắn theo `R_Hand` hoặc socket không đúng với hệ ownership của clip | Clove mount dưới `MasterWeaponAim/MasterWeapon/R_WeaponMaster`; tính một rigid offset theo hai grip anchor |
| Tay phải đúng nhưng tay trái, ngón tay hoặc khuỷu bị vẹo | Clip gốc điều khiển cả hai tay nhưng runtime IK/layer finger/additive lại ghi đè một phần | Generic clip giữ curve của cả hai tay; tắt runtime left-hand IK cho profile Generic; mask tách ngón tay đúng phạm vi |
| GNTP chạy thì súng biến mất | Root scale trong clip ghi `1` lên prefab root được author ở `100` | Tạo `.anim` copy có root Transform cố định bằng Transform prefab |
| Băng đạn chạy rất xa sau khi sửa scale | Chỉ sửa root scale mà không đổi translation của child bone | Nhân child local-position curve với `sourceRootScale / authoredRootScale`, thường là `0.01` |
| Preview từng clip đúng, vào game reload thì cổ/tay méo | Runtime chồng locomotion, upper-body, movement additive, Fire additive, finger layer và gun Animator; Fire chưa bị hủy | Reload/Equip hủy Fire trigger và đưa layer `Fire Additive` về `Zero` trước khi phát action |
| Reload xong bị kẹt pose hoặc bật về pose lạ | Body/gun tự thoát theo normalized time khác nhau hoặc completion edge không đồng nhất | Cùng `ReloadComplete`, cùng playback deadline, blend 0,05 giây |
| Equip súng mới nhưng state cũ còn sót | Thay primary object trong cùng slot không phát `OnWeaponChanged` | `OnPrimaryWeaponChanged` gọi `ResetThirdPersonActionState()` và refresh presentation |
| Odin chỉ giật ở viên đầu | Policy continuous-fire 1P bị dùng nhầm cho presentation 3P | 3P phát Fire cho từng phát bắn server chấp nhận; state Fire 3P có timed exit |
| Operator ADS không đưa súng lên mắt | `AimN` chưa được hiểu/bake như neutral forward ADS trên hierarchy Clove | Bake correction vào clip sinh ra để scope line qua mắt phải; không sửa rest pose |
| Operator có `FireZoomed` khác `Fire` gây nhánh không nhất quán | Hai state cùng biểu diễn một action nhưng rẽ theo Aiming | Xóa state/asset `FireZoomed` production; hip-fire và ADS cùng dùng `Fire` |
| Preview/editor để lại băng đạn ở xa | Animation Preview có thể giữ sampled pose nếu thao tác tiếp khi `AnimationMode` còn mở | Dừng `AnimationMode` trước authoring; mọi phép đo dùng instance tạm và destroy trong `finally` |

## 5. Giai đoạn đầu: vì sao không chỉnh gì, chỉ dùng nguyên animation vẫn làm méo model

### 5.1. Điều thực sự đã xảy ra

Trình tự ban đầu cần được ghi chính xác như sau:

1. Người dùng lấy nguyên TP animation FBX đưa vào Animator/controller của Body 3P.
2. Không có bước tự tạo Avatar mới, không chỉnh bone mapping và không sửa Transform model.
3. Ngay khi clip được đánh giá trên Body, model đã vặn/xoắn hoặc co sập.
4. Cách sửa đầu tiên là tạo một **file `.anim` mới** từ clip gốc.
5. Controller production sau đó dùng generated `.anim` mới, không dùng trực tiếp Humanoid clip trong FBX gốc.

Điểm dễ gây hiểu nhầm là “không gán Avatar” trong Inspector không có nghĩa animation asset đang là raw/Generic animation. Importer của các TP clip gốc đã chứa cấu hình Humanoid từ trước.

Ví dụ đã kiểm tra trực tiếp với `TP_Core_AK_S0_Reload_UB.fbx.meta`:

```text
animationType: 3     # Humanoid
avatarSetup: 2       # Copy From Other Avatar
lastHumanDescriptionAvatarSource:
  TP_Core_IdlePose.fbx
```

Trong khi model Body của Clove cũng được import Humanoid nhưng tạo Avatar từ model Clove:

```text
Body.fbx
animationType: 3     # Humanoid
avatarSetup: 1       # Create From This Model
```

Do đó, dù người dùng không thao tác với Avatar, Unity vẫn thực hiện chuỗi ngầm:

```text
TP Core skeleton/rest pose
    -> TP_Core_IdlePose Avatar
    -> Human/Muscle animation
    -> Clove BodyAvatar
    -> Clove skeleton/rest axes
```

Lỗi xuất hiện ngay ở bước này, trước các lỗi runtime transition, GNTP scale hay IK phát sinh sau đó.

### 5.2. Humanoid retarget cái gì

Humanoid không retarget bằng tên/path của mọi Transform. Nó chuyển motion thành Human/Muscle stream dựa trên các bone người chuẩn như hông, cột sống, đầu, vai, cánh tay, bàn tay và chân.

Điều này rất hữu ích nếu mục tiêu là dùng chung walk/run/human action giữa hai skeleton người khác nhau. Nhưng weapon animation trong dự án không chỉ có “người”:

- `MasterWeaponAim`
- `MasterWeapon`
- `R_WeaponMaster`
- các weapon point/socket
- các bone phụ trợ cho magazine hoặc contact

Các nhánh này không thuộc `HumanBodyBones`. Avatar có thể hợp lệ đối với người nhưng vẫn không thể đại diện đầy đủ cho ownership của súng.

### 5.3. Vì sao model có thể trông như “chỉ còn bone”

**Sự thật đã xác nhận**:

- TP clip gốc là `humanMotion=True`.
- Nó dùng Avatar của `TP_Core_IdlePose`, không phải Avatar tạo từ `Body.fbx` của Clove.
- Các clip có Human curves cho cơ thể/ngón tay nhưng weapon helper branch không được đại diện đầy đủ trong Human stream.
- Generated production clip hiện tại là một asset `.anim` khác, `humanMotion=False`, dùng direct Transform binding.

Hai rig có thể cùng tên bone và cùng được Unity đánh dấu Humanoid nhưng vẫn khác rest rotation, bone axis, hierarchy phụ hoặc cách chuẩn hóa T-pose. Human/Muscle values được giải ngược qua một Avatar khác có thể làm vai, cẳng tay, cổ và ngón tay nhận rotation không tương đương với source pose.

Skinned mesh không thật sự bị xóa. Bone deform của mesh bị giải ra các rotation/position không phù hợp với rest axes của Clove, khiến bề mặt co, vặn hoặc chồng vào nhau; trong Scene view người dùng chủ yếu còn nhìn rõ skeleton/gizmo nên cảm giác như “chỉ còn bone”.

Không còn đủ snapshot của controller ở đúng lần thử đầu để khẳng định một bone duy nhất gây toàn bộ collapse. Vì vậy kết luận an toàn là lỗi thuộc **Humanoid source-avatar → Clove-avatar conversion kết hợp với thiếu ownership của helper branch**, không phải do mesh bị hỏng và cũng không phải do người dùng đã tự gán nhầm Avatar.

Không nên quy toàn bộ hiện tượng này cho một “Avatar bị lỗi”. Cùng triệu chứng co/mất model về sau còn được tạo bởi root scale của GNTP. Hai lỗi phải được kiểm tra riêng.

### 5.4. Vì sao tạo một Humanoid Avatar mới vẫn không giải quyết trọn vẹn

Avatar mới có thể sửa mapping vai, khuỷu hoặc bàn tay. Nó không làm `MasterWeapon` trở thành human bone và không tự tạo lại path mà clip cần. Nếu clip phải di chuyển cả hai tay **và** weapon-master branch thì retarget chỉ human bones vẫn thiếu dữ liệu quan trọng.

Vì vậy kết luận đúng là:

> Humanoid phù hợp cho canonical human motion, nhưng không đủ làm biểu diễn duy nhất cho bộ clip 3P có helper weapon branch thiết yếu.

### 5.5. File `.anim` mới đã sửa lỗi đầu tiên như thế nào

Fix không chỉnh lại keyframe bằng tay và không đổi Transform model. Tool thực hiện:

1. Sao chép TP FBX nguồn sang vùng production.
2. Import bản sao thành `Generic` + `NoAvatar`.
3. Sample animation trên chính source skeleton, nơi motion ban đầu được author đúng.
4. Lấy local position/rotation thật của từng Transform ở từng frame.
5. Chỉ giữ các path tồn tại và có ý nghĩa trên hierarchy Clove, gồm cả hai tay và `MasterWeaponAim/MasterWeapon`.
6. Ghi lại thành generated `.anim` với binding tương đối đúng với Animator root `Body`:

```text
CS_Smonk_S0_Skelmesh.ao/Skeleton/...
```

7. Controller được thay motion để dùng generated `.anim` này.
8. Runtime profile đặt `Avatar=None`, nên Unity không chuyển qua Human/Muscle stream lần nữa.

Nói ngắn gọn:

```text
Trước: TP Humanoid motion -> source Avatar -> Clove Avatar -> pose bị xoắn
Sau:   sampled source Transform -> Clove path-bound .anim -> đúng Transform đích
```

Đó là lý do **một file animation mới** đã sửa được lỗi vặn model ban đầu, ngay cả trước khi xử lý các vấn đề mount súng, GNTP scale và transition runtime.

## 6. Vì sao 1P dùng `FPAnim` lại chạy tốt

1P không phải bằng chứng rằng Avatar 3P “đáng lẽ cũng tự chạy”. Hai hệ có contract khác nhau.

Ở 1P:

- `FPAnim` điều khiển đúng skeleton tay 1P được author cho nó.
- Binding path của clip và hierarchy đích thống nhất trực tiếp.
- `MasterWeapon` có thể hoạt động dù không xuất hiện trong Humanoid Avatar, vì clip không cần chuyển nó thành `HumanBodyBones`; clip tìm đúng Transform theo path.
- Camera, arms và weapon presentation đều được thiết kế cùng một rig contract.

Nguyên tắc học được từ 1P không phải “bỏ Avatar là xong”, mà là:

> Animation phải được phát trên đúng root và đúng hierarchy mà binding của nó được author cho.

Generic Path-Bound của Clove áp dụng đúng nguyên tắc này cho 3P.

## 7. Vấn đề parent súng: `R_Hand`, `R_WeaponPoint` hay `R_WeaponMaster`

### 7.1. Tại sao gắn thẳng vào tay phải từng gây sai

Nếu clip chỉ animate cánh tay và súng là prop tĩnh, gắn súng vào `R_Hand` là hợp lý. Nhưng các clip đang dùng có thể animate đồng thời:

- tay phải;
- tay trái;
- ngón tay;
- `MasterWeaponAim/MasterWeapon`;
- chuyển động reload/equip của chính nhánh giữ súng.

Khi đặt toàn bộ súng dưới tay phải, phần motion của `MasterWeapon` bị bỏ qua. Súng chỉ theo bàn tay, trong khi tay còn lại vẫn chạy theo pose được author quanh một weapon-master khác. Kết quả là báng súng vào giữa ngực, tay trái không chạm foregrip hoặc reload bị kéo lệch.

### 7.2. `R_WeaponPoint` không tự động là câu trả lời đúng

Tên socket không quyết định ownership. Cần kiểm tra:

1. Clip có curve nhắm vào socket/ancestor nào?
2. Animator root làm cho path đó resolve được không?
3. Súng được author với trục local và scale nào?
4. Body clip hay gun Animator là bên sở hữu chuyển động nào?

`R_WeaponPoint` có thể đúng cho một rig khác hoặc legacy Humanoid presentation. Với Clove Generic production, nhánh đúng là:

```text
MasterWeaponAim/MasterWeapon/R_WeaponMaster
```

### 7.3. Cách mount production

Editor tool đặt weapon object dưới `R_WeaponMaster`, sau đó tính **một** rigid offset sao cho:

- tay phải gần `Trigger`;
- tay trái gần `Left_Hand_Target`;
- trục giữa hai grip của súng khớp trục giữa hai bàn tay ở Hold pose.

Đây là phép author/calibration một lần trong Editor. Offset được serialize vào prefab. Runtime không liên tục kéo súng về tay và không thay đổi Transform gốc của model/FBX.

### 7.4. Trạng thái các player prefab

- **Clove/Sage/Gekko/Brimstone**: mỗi prefab có đủ năm profile Generic Path-Bound, weapon parent là `R_WeaponMaster`, Avatar của profile Generic là `null` và không dùng runtime left-hand IK.
- Bốn model 3P dùng chung tên root binding `CS_Smonk_S0_Skelmesh.ao`. Đây là tên GameObject root phục vụ lookup path, không phải tên bone hay thay đổi source FBX; nhờ vậy các prefab tái sử dụng cùng controller và `.anim` của Clove mà không nhân bản dữ liệu animation.
- Các path production bắt buộc cho thân, hai tay và `MasterWeaponAim/MasterWeapon/R_WeaponMaster` tồn tại trên cả bốn model. Brimstone thiếu một helper vai tùy chọn; Sage thiếu bảy IK/helper path đã retire. Unity bỏ qua các binding không có đích này và production không phụ thuộc vào chúng.

## 8. Quá trình thử IK và lý do production Generic không dùng runtime IK

IK tay trái từng là cách hợp lý để giữ bàn tay hỗ trợ trên foregrip hoặc băng đạn trong khi Body clip và gun clip chưa cùng contract. Công cụ cũ có proxy target, `TwoBoneIKConstraint`, weight curve và các target theo từng súng.

Nhưng IK trở thành nguồn ghi đè thứ ba khi clip gốc đã điều khiển cả hai tay:

```text
Body clip -> pose hai tay
Gun clip  -> magazine/bolt/shell
IK        -> kéo lại tay trái sau Animator
```

Nếu target, weight hoặc thời điểm contact không khớp từng frame, tay trái bị khóa vào súng trong lúc clip muốn rời ra lấy băng, hoặc khuỷu bị ép sang nghiệm gập không tự nhiên. Với Odin, equip/reload gốc đặc biệt phụ thuộc chuyển động phối hợp của cả hai tay nên ép toàn bộ vào một right-hand rig hoặc IK support-hand càng dễ sai.

Production Generic hiện tại đặt:

- `UseLeftHandIK = false`
- `AnimationDrivenLeftHandIK = false`

Hai tay và weapon-master branch do direct Transform curves sở hữu. IK vẫn tồn tại như đường legacy cho profile khác, không phải thành phần bắt buộc của Clove Generic.

## 9. Cách Generic Path-Bound được xây dựng

Nguồn triển khai trước đây là `GenericPathBoundWeaponSetup.cs` và `OperatorGenericThirdPersonSetup.cs`. Hai editor tool này đã được retire sau khi output production Generic Path-Bound hoàn tất; output production hiện hành không phụ thuộc vào việc các tool còn tồn tại.

### 9.1. Không sửa asset nguồn

Mỗi source FBX được sao chép sang vùng production riêng. Bản sao được import với:

- `ModelImporterAnimationType.Generic`
- `ModelImporterAvatarSetup.NoAvatar`
- `optimizeGameObjects = false`

Tắt optimize là cần thiết vì direct Transform curve phải tìm thấy helper bone thật trong hierarchy.

FBX gốc, model Clove, Avatar gốc và rest pose không bị sửa.

### 9.2. Chỉ giữ path tồn tại ở cả nguồn và đích

Tool lấy các Transform binding trong source clip rồi chỉ giữ path thỏa đồng thời:

- thuộc vùng motion cần thiết (`FullSkeleton` hoặc `UpperBody`);
- tồn tại trong source hierarchy;
- tồn tại dưới model root Clove.

Vùng Upper Body gồm cả:

- `Skeleton/Root/Splitter/Spine1/...`
- `Skeleton/Root/Splitter/MasterWeaponAim/...`

Đây là khác biệt cốt lõi so với Humanoid-only: nhánh weapon được giữ như Transform bình thường.

### 9.3. Sample và rebake curve

Tool sample source clip theo frame. Với mỗi Transform tương thích, nó ghi:

- local position;
- local rotation quaternion;
- quaternion continuity để tránh đổi dấu gây interpolation dài.

Destination binding được thêm prefix model root vì Animator hiện nằm ở `Body`:

```text
CS_Smonk_S0_Skelmesh.ao/Skeleton/...
```

Translation được đổi theo tỉ lệ lossy scale giữa source và target model root. Generated body clip bị cấm animate Transform scale để không làm sập hierarchy nhân vật.

Generic không “tự retarget” skeleton tùy ý. Nó hoạt động vì source và destination có các path tương thích đã được chọn và rebake có chủ đích.

### 9.4. Xử lý additive reference pose

Một số source clip có external additive reference pose. Sau khi path được rebake từ `Skeleton/...` thành `CS_Smonk.../Skeleton/...`, reference clip cũ vẫn nhắm path cũ. Nếu giữ lại, Additive layer sẽ cộng một full pose không cùng binding và có thể xoắn cổ/tay.

Tool xóa external additive reference không tương thích. Unity khi đó đánh giá generated clip tương đối với frame đầu của chính clip, tức cùng hierarchy đích.

### 9.5. Avatar Mask theo Transform path

Mask Generic không bật Humanoid body part. Nó liệt kê Transform path thật:

- layer `Upper Body Gun Pose` dùng upper-body mask;
- `Upper Body Movement Additive` dùng mask không gồm finger;
- `Fire Additive` cũng không gồm finger;
- layer finger riêng có thể quản lý ngón nếu cần;
- root các prefab presentation có Animator riêng bị loại khỏi Body mask để Body Animator không ghi đè skeleton súng.

Controller được yêu cầu có ít nhất năm layer production. Layer finger của Generic được đặt weight `0` khi direct clip đã sở hữu ngón tay.

### 9.6. Runtime profile switching

[`PlayerVisibilityController.cs`](../Assets/FPS/Features/Characters/Player/Runtime/PlayerVisibilityController.cs) lưu theo từng súng:

- weapon object;
- body controller;
- `ThirdPersonCharacterRigMode`;
- Avatar cần khôi phục nếu quay lại Authored Avatar;
- policy left-hand IK.

Khi chọn profile Generic:

1. gán controller tương ứng;
2. đặt `Animator.avatar = null`;
3. gọi `Animator.Rebind()`;
4. gọi `Animator.Update(0f)`.

`Rebind()` là bắt buộc khi đổi mode. Nếu không, Mecanim có thể giữ HumanStream/binding cache của vũ khí trước, tạo pose sai dù controller mới đúng.

## 10. GNTP: vì sao animation súng từng biến mất hoặc làm băng đạn bay xa

Nguồn triển khai trước đây là `ThirdPersonGunClipNormalizer.cs`. Editor tool đã được retire sau khi các clip normalized production đã hoàn tất; các clip output hiện hành được giữ nguyên.

### 10.1. Lỗi root scale

Một số GNTP source clip có root scale hằng bằng `1`, trong khi gun Animator root trong prefab được author ở scale `100`. Animation curve ở path rỗng là curve của chính Animator root. Khi state bắt đầu, curve thắng Transform prefab:

```text
prefab root scale = 100
clip root scale   = 1
=> súng co 100 lần hoặc đổi vị trí tương đối, trông như biến mất
```

Đây không phải lỗi Avatar, vì gun Animator có hierarchy riêng.

### 10.2. Vì sao chỉ ghi lại root scale vẫn chưa đủ

Nếu root được giữ ở `100` nhưng child translation vẫn dùng số liệu dành cho root `1`, khoảng đi của magazine/shell cũng bị nhân lên theo world scale. Băng đạn vì thế có thể chạy rất xa.

Tool tính theo từng trục:

```text
childTranslationScale = sourceRootScale / authoredPrefabRootScale
```

Trường hợp thường gặp:

```text
1 / 100 = 0.01
```

Mọi curve `m_LocalPosition.x/y/z` của child hợp lệ được nhân cả value lẫn tangent với tỉ lệ này.

### 10.3. Những gì normalizer thực hiện

- Tạo `.anim` copy theo weapon/controller; không sửa imported GNTP FBX.
- Chỉ giữ binding có path tồn tại dưới gun Animator root.
- Drop binding không tương thích và log số lượng.
- Ghi root local position/rotation/scale hằng bằng đúng Transform authored của prefab.
- Xác minh root ở các mốc 0%, 25%, 50%, 75%, 100%.
- Xác minh child translation sau conversion khớp source nhân tỉ lệ.
- Từ chối non-uniform conversion vì rotated descendant không thể quy đổi an toàn bằng một scalar local-space đơn giản.

Nhờ vậy súng giữ đúng kích thước và chuyển động magazine/bolt vẫn giữ đúng khoảng cách world-space.

## 11. Vì sao clip preview đúng nhưng runtime méo

Animation Window thường chỉ sample một clip lên một target. Runtime thực tế đánh giá cả graph:

```text
Locomotion (lower/full body)
  + Upper Body Gun Pose (override)
  + Upper Body Movement Additive
  + Fire Additive
  + Finger Pose
  + Body transition blending
  + Gun Animator độc lập
  + gameplay/network action deadline
```

Vì thế “mỗi clip riêng đều ổn” chỉ chứng minh dữ liệu clip không tự hỏng. Nó không chứng minh:

- Avatar Mask không overlap sai;
- additive reference đúng;
- layer weight đúng;
- Body và gun cùng normalized time;
- state cũ đã thoát;
- transition không blend hai pose không tương thích;
- runtime đã rebind sau khi đổi Avatar/controller.

### 11.1. Fire additive chồng lên Reload/Equip

Fire thường nằm ở layer additive. Nếu người chơi bắn rồi reload ngay, trigger/state Fire có thể còn active trong lúc Reload ở upper-body layer. Recoil tiếp tục cộng lên cổ/vai/khuỷu của full reload pose, làm model trông như clip reload bị hỏng.

[`WeaponManager.cs`](../Assets/FPS/Features/Weapons/Runtime/WeaponManager.cs) hiện làm hai việc trước Reload/Equip:

- reset trigger `Fire`;
- đưa mọi layer có tên `Fire Additive` về state `Zero`.

Đây là sửa runtime composition, không phải sửa keyframe của reload.

### 11.2. Mask và finger layer

Nếu movement additive được phép ghi finger hoặc weapon prefab root, nó có thể ghi đè grip. Nếu finger layer giữ weight `1` trong khi Generic body clip đã animate ngón, ngón bị ép về pose khác. Production mask loại finger khỏi movement/fire additive và Generic profile đặt finger layer mặc định weight `0`.

### 11.3. Khuỷu tay lùi sâu ở frame 36–39

Một chuyển động đầu khuỷu tay lùi ra sau không tự động là lỗi rig. Nếu nó xuất hiện giống nhau khi preview đúng generated reload clip và không làm bone length/scale bất thường, đó là pose/keyframe của animation gốc hoặc kết quả retarget có chủ đích.

Nó chỉ được coi là lỗi hệ thống khi runtime khác preview, arm chain bị kéo giãn, hand mất grip, hoặc pose chỉ xuất hiện do layer/transition chồng. Các probe sau sửa đã đo bone length gần `1x` và runtime gần direct sample; vì vậy không nên “chữa” khuỷu bằng đổi rest pose.

## 12. Transition: hiểu đúng Exit Time

### 12.1. Exit Time không nằm “bên trong clip”

Clip có thời lượng và keyframe. `Has Exit Time` là thuộc tính của `AnimatorStateTransition`, không phải marker tự động nằm trong animation.

- Có Exit Time: transition được phép bắt đầu khi normalized time đạt mốc cấu hình.
- Không Exit Time, không condition: transition hợp lệ ngay lập tức và state có thể vừa vào đã thoát.
- Không Exit Time, có trigger/bool condition: state chờ condition.

Vì vậy tắt Exit Time cho `Reload -> Idle` mà không thêm điều kiện sẽ không làm clip “tự chạy hết rồi về Idle”; nó làm transition có thể xảy ra ngay.

### 12.2. Vì sao Exit Time đơn thuần không đồng bộ được Body và gun

Body và gun là hai Animator clock. Cùng `exitTime = 1` chỉ có nghĩa mỗi state chờ hết **clip của chính nó**. Trước sửa, các cặp dynamic có chênh lệch lớn, ví dụ audit ghi nhận:

| Weapon/action | Body | Gun trước đồng bộ |
|---|---:|---:|
| Classic Reload | 2,1167 s | 1,0000 s |
| Operator Equip | 2,3333 s | 0,8667 s |
| Operator Reload | 4,5000 s | 2,3500 s |
| Bucky Reload | 2,7083 s | 1,2708 s |
| Bucky Fire | 0,8000 s | 0,4000 s |

Cả hai đều “chạy hết clip”, nhưng súng đã về Idle trong khi tay còn ở giữa action. Khi blend ngược vào Hold/Idle, grip và magazine không còn cùng phase.

### 12.3. Policy cuối cho Reload và Equip

`ThirdPersonWeaponAnimationSyncSetup.cs` trước đây cấu hình cả Body và gun; editor tool đã được retire sau khi controller production hoàn tất:

- `ReloadComplete` trigger;
- `EquipComplete` trigger;
- `ReloadPlaybackSpeed` float;
- `EquipPlaybackSpeed` float;
- transition action → Hold/Idle không dùng Exit Time;
- transition chỉ có một completion condition;
- fixed blend duration `0,05 s`;
- không interruption giữa chừng.

Base state speed được tính:

```text
state.speed = clip.length / WeaponData action duration
```

Khi runtime có deadline khác, playback float được tính:

```text
playbackSpeed = authoredDuration / remainingAuthoritativeDuration
```

Nhờ vậy Body và gun cùng đạt phase cuối khi gameplay hoàn tất, kể cả reload bị chậm bởi infection modifier.

### 12.4. Policy cuối cho Fire

Fire là one-shot ngắn và được phép dùng timed exit:

- effective duration bằng `WeaponData.FireInterval`;
- transition `Fire -> Idle/Zero` dùng `exitTime = 1`;
- Fire additive blend `0,04 s` khi phù hợp;
- Any State entry cho phép self restart để mỗi phát bắn có thể khởi động lại recoil.

Do đó “tắt hết Exit Time cho mọi anim” không phải giải pháp đúng. Reload/Equip do gameplay completion quyết định; Fire do thời lượng phát bắn quyết định.

## 13. Reload, Equip và Fire ở runtime/network

### 13.1. Reload

Offline và server-authoritative path đều phát `Reload` khi bắt đầu và `ReloadComplete` khi gameplay thực sự kết thúc. Deadline được dùng để đặt playback speed trước khi action chạy.

Reload theo magazine có commit point riêng. Reload `PerShell` như Bucky tính:

```text
opening + roundsToLoad × perShellInterval + closing
```

Toàn bộ duration thực tế, kể cả infection multiplier, được đưa vào 3P presentation. Đây là lý do không thể hard-code một clip duration cố định rồi tin vào Exit Time.

### 13.2. Equip

Equip dùng authoritative `equipCompleteTime`. `WeaponManager.Update()` phát `EquipComplete` khi presentation time đạt deadline. Remote client nhận edge replicated và cũng chạy Body/gun qua cùng profile.

Khi thay primary weapon nhưng vẫn ở slot `0`, networked slot index không đổi nên callback đổi slot không chạy. Code hiện reset action state ngay trong `OnPrimaryWeaponChanged`, bật object mới, refresh profile rồi mới áp deadline equip.

### 13.3. Fire và Odin

Mỗi phát bắn hợp lệ được server phát effect/presentation cho remote. Presentation 3P phải restart Fire theo từng accepted shot.

Odin có policy continuous loop đặc biệt ở **1P** để feed/ejection không bị reset sai. Policy đó từng rò sang 3P và khiến 3P chỉ phản ứng ở viên đầu hoặc giữ Fire quá lâu. Production 3P hiện dùng timed Fire per accepted shot giống contract hiển thị của các súng khác; 1P vẫn có continuous-fire logic riêng trong [`Weapon.cs`](../Assets/FPS/Features/Weapons/Runtime/Weapon.cs).

## 14. Các xử lý riêng theo súng

### 14.1. Vandal

- Là profile đầu tiên dùng để xác thực Generic path-bound và đồng bộ Body/GNTP.
- Reload có clip GNTP động; các probe 25%, 50%, 75% cho thấy normalized time Body/gun khớp sau đồng bộ.
- Không dùng bản reload IK trong production Generic.

### 14.2. Classic

- Generic conversion giữ hai tay và helper branch thay vì ép toàn bộ súng vào tay phải.
- Reload gun trước đây ngắn hơn Body rõ rệt; nay cùng completion/deadline.
- Finger ownership được tách khỏi movement/fire additive.

### 14.3. Operator

- Có builder riêng do ADS, scope line và bộ clip bolt-sniper.
- `AimN` được hiểu là neutral/forward ADS; production bake sight axis về hướng trước và đặt scope theo eye relief của mắt phải.
- Arm correction được bake vào generated clip, không chạy IK mỗi frame.
- `FireZoomed` bị xóa; `Fire` dùng chung cho hip-fire và ADS.
- Reload/Equip/Fire của Body và gun từng chênh phase lớn nên Operator biểu hiện lỗi transition rõ nhất.

### 14.4. Bucky

- Gun có Equip/Reload/Fire động, không thể coi như static pose.
- Reload gameplay biến thiên theo số shell; presentation phải theo duration thực tế.
- Fire Body từng dài gấp đôi gun; hiện cả hai khớp `FireInterval`.

### 14.5. Odin

- Equip/Reload body gốc phối hợp cả hai tay; vì thế right-hand-only hoặc left-hand IK override đều có thể phá pose.
- Production Generic dùng clip gốc đã path-bind, không dùng bản reload IK.
- Fire 3P chạy theo mỗi viên đạn được chấp nhận; continuous policy chỉ giữ cho 1P nơi cần loop feed/ejection.

## 15. Tool F5 và preview đúng cách

[`WeaponInputHandler.cs`](../Assets/FPS/Features/Weapons/Runtime/WeaponInputHandler.cs) cung cấp debug view:

- `F5`: bật/tắt góc nhìn kiểm tra 3P;
- `F6`: Vandal;
- `F7`: Operator;
- `F8`: Odin;
- `F9`: Bucky;
- `F10`: Classic.

Tool này đổi camera/visibility và đi qua runtime weapon selection/presentation. Nó hữu ích hơn Animation Window cho lỗi transition vì chạy controller thật, nhưng vẫn phải kiểm tra đúng các chuỗi:

1. đứng yên → bắn → reload;
2. chạy → bắn → reload;
3. reload → đổi súng giữa chừng;
4. thay primary khác trong cùng slot;
5. aim Operator → fire → reload;
6. giữ bắn Odin nhiều viên;
7. Bucky reload một shell và nhiều shell.

Editor authoring tool luôn gọi `AnimationMode.StopAnimationMode()` trước khi đo/sửa. Mọi sample dùng object tạm với `HideAndDontSave`, sau đó `DestroyImmediate` trong `finally`; không sample trực tiếp rồi save prefab ở pose giữa clip.

## 16. Những gì tuyệt đối không bị thay đổi

Giải pháp cuối có các invariant:

- Không sửa source FBX.
- Không sửa rest pose/skeleton Transform của model Clove.
- Không sửa Avatar nguồn để nhét weapon helper vào Human mapping.
- Không thay Transform prefab nguồn chỉ để chống curve root sai.
- Không chạy calibration liên tục ở runtime.
- Không dùng `AddComponent` để vá reference animation khi chạy game.

Những thứ được tạo/chỉnh là:

- bản sao Generic của source;
- generated path-bound `.anim`;
- normalized GNTP `.anim`;
- controller/mask production;
- serialized mount offset trên weapon presentation trong player prefab;
- runtime parameter/deadline flow.

Operator builder còn so sánh property modification của skeleton trước/sau và fail nếu authoring làm đổi pose đã có.

## 17. Validation và test

### 17.1. EditMode

Các test dưới đây là bằng chứng lịch sử của quá trình authoring, không còn là acceptance gate; chúng đã được retire theo quyết định nghiệm thu bằng mắt của người dùng.

`WeaponAnimatorFlowTests.cs` trước đây bao phủ:

- flow trigger của controller 1P và 3P;
- đủ năm weapon presentation trên bốn prefab;
- controller/body/gun assignment;
- duration Reload/Equip/Fire;
- completion trigger và playback parameter;
- root Transform clip súng khớp prefab;
- grip tay phải/tay trái;
- locomotion và movement additive;
- Operator reload transition;
- gun controller chỉ dùng GNTP/static motion hợp lệ.

Lớp hiện tại mở rộng thành 24 case sau khi tính các `[TestCase]`. Kết quả nghiệm thu cuối của đợt sửa được ghi nhận là 24/24 pass. Các dòng fail cũ còn trong `Editor.log`/`Editor-prev.log` là các vòng audit trước khi timing và grip được sửa; không nên lấy chúng làm trạng thái cuối.

### 17.2. PlayMode

Các test dưới đây là bằng chứng lịch sử của quá trình authoring, không còn là acceptance gate; chúng đã được retire theo quyết định nghiệm thu bằng mắt của người dùng.

`VandalThirdPersonProductionPlayModeTests.cs` trước đây bao phủ runtime production prefab:

- profile switch;
- simultaneous Body/gun state entry;
- repeated action;
- walk + fire + reload/equip;
- remote reload/equip;
- action cancellation;
- grip không rách trong runtime;
- lower/upper body locomotion cycle.

Kết quả cuối được lưu tại `Logs/ClaudeTestRunSummary.txt`: 15 pass, 0 fail.

### 17.3. Validator authoring

Các validator cuối đã pass:

- `[ThirdPersonGunClips] Production clip validation passed.`
- `[3PAnimationSync] Validation passed for all authored player weapons.`
- Generic Path-Bound validation cho Vandal, Classic, Odin, Bucky.
- Operator Generic/ADS/hierarchy validation trong builder riêng.

Trạng thái Unity cuối được log trước khi viết tài liệu:

```text
scene=Assets/FPS/Scenes/MainMenu.unity
dirty=False
playing=False
compiling=False
```

Việc tạo tài liệu này không mở scene, không vào Play Mode và không mutate asset Unity.

## 18. Checklist thêm một vũ khí 3P mới

### 18.1. Kiểm tra source

- Xác định clip Body và clip GNTP riêng.
- Liệt kê Transform binding của clip Body.
- Xác định clip có dùng `MasterWeaponAim/MasterWeapon` và cả hai tay không.
- Kiểm tra clip root có position/rotation/scale curve không.
- Kiểm tra additive reference pose và path của reference.

### 18.2. Chọn rig mode

- Chọn Humanoid nếu chỉ cần canonical human bones và helper branch không tham gia.
- Chọn Generic Path-Bound nếu helper/weapon branch là một phần bắt buộc của pose.
- Không chọn mode chỉ vì tên model “Humanoid”; quyết định dựa trên binding contract.

### 18.3. Chọn Animator root và mount

- Mọi binding path phải tương đối đúng với Animator root.
- Parent weapon phải khớp ownership của clip, không mặc định là `R_Hand`.
- Có `Trigger` và `Left_Hand_Target` để đo grip.
- Mount offset được giải một lần trong Editor và serialize.

### 18.4. Chuẩn hóa gun clip

- Root curve phải khớp authored gun Animator root.
- Child translation phải được đổi theo root scale ratio.
- Drop hoặc fail binding không tồn tại; không im lặng giữ path rác.
- Không chấp nhận NaN, infinity, zero scale hoặc non-uniform conversion không an toàn.

### 18.5. Controller

- Reload và Equip của Body/gun dùng completion trigger chung.
- Effective duration khớp `WeaponData`.
- Fire khớp `FireInterval` và có restart policy rõ ràng.
- Reload/Equip hủy Fire additive.
- Mask không ghi đè finger/weapon root ngoài ownership.
- Không giữ state/parameter orphan như `FireZoomed`, `Fire_Alt` hoặc `Idle_001` nếu production không dùng.

### 18.6. Runtime/network

- Local và remote nhận cùng action edge.
- Late/authoritative deadline điều khiển playback speed.
- Đổi slot và thay object cùng slot đều reset action state.
- Reload interruption, death/down hoặc weapon replacement không để state completion cũ bắn vào controller mới.

### 18.7. Test bắt buộc

- Preview clip riêng để kiểm tra data.
- Runtime controller test để kiểm tra composition.
- Sample Body/gun ở 25%, 50%, 75%, không chỉ kiểm tra state entry.
- Test chạy + bắn + reload.
- Test root scale và magazine travel.
- Test grip distance hai tay.
- Test mọi weapon trên ít nhất một production prefab và authored-reference trên các prefab còn lại.

## 19. Những cách không nên lặp lại

1. Không đổi toàn bộ Body sang một Humanoid Avatar mới rồi giả định weapon helper tự được giữ.
2. Không gắn mọi súng dưới tay phải chỉ vì tay phải bóp cò.
3. Không sửa rest pose để bù cho một clip binding sai.
4. Không sửa trực tiếp imported FBX để chữa production hierarchy.
5. Không chỉ sửa root scale mà quên child translation.
6. Không kết luận runtime đúng chỉ vì Animation Preview đúng.
7. Không tắt Exit Time trên transition không có condition.
8. Không dùng Exit Time độc lập làm cơ chế đồng bộ hai Animator.
9. Không để Fire additive chạy cùng full Reload/Equip pose.
10. Không dùng policy continuous-fire 1P cho presentation 3P mà không xem lại semantics.
11. Không sample prefab đang mở rồi save khi `AnimationMode` còn active.
12. Không chuyển Animator root mà không rebake toàn bộ path.

## 20. Bản đồ file triển khai

| Trách nhiệm | File |
|---|---|
| Build Generic path-bound cho Vandal, Classic, Odin, Bucky | Editor tool đã retire sau khi output hoàn tất |
| Generic, ADS và unified Fire riêng cho Operator | Editor tool đã retire sau khi output hoàn tất |
| Chuẩn hóa root/child translation của GNTP | Editor tool đã retire sau khi clip output hoàn tất |
| Một nguồn author timing Reload/Equip/Fire | Editor tool đã retire sau khi controller output hoàn tất |
| Đường IK legacy và lịch sử support-hand | Đã retire và xóa trong cleanup 3P |
| Chọn profile, controller, Avatar và Rebind runtime | [`PlayerVisibilityController.cs`](../Assets/FPS/Features/Characters/Player/Runtime/PlayerVisibilityController.cs) |
| Route trigger, deadline, cancel additive, reset action | [`WeaponManager.cs`](../Assets/FPS/Features/Weapons/Runtime/WeaponManager.cs) |
| Reload offline, per-shell, infection timing, 1P Odin loop | [`Weapon.cs`](../Assets/FPS/Features/Weapons/Runtime/Weapon.cs) |
| Fire server-authoritative và remote effect edge | [`WeaponFireHandler.cs`](../Assets/FPS/Features/Weapons/Runtime/WeaponFireHandler.cs) |
| F5–F10 runtime inspection | [`WeaponInputHandler.cs`](../Assets/FPS/Features/Weapons/Runtime/WeaponInputHandler.cs) |
| EditMode contract tests | Đã retire theo quyết định nghiệm thu bằng mắt |
| Production PlayMode tests | Đã retire theo quyết định nghiệm thu bằng mắt |

## 21. Giới hạn còn lại

- Generic Path-Bound phụ thuộc vào path tương thích. Đổi tên/reparent bone sẽ yêu cầu regenerate clip và mask.
- Bốn player production cùng dùng contract Generic Path-Bound và tên model root `CS_Smonk_S0_Skelmesh.ao`. Khi thêm model mới, phải xác nhận đủ path thân, hai tay và `MasterWeaponAim/MasterWeapon/R_WeaponMaster`; không được suy luận tương thích chỉ từ tên root.
- Test định lượng ngăn collapse, lệch timing và grip rách, nhưng không thay thế review nghệ thuật từng frame. Một pose khuỷu hoặc báng súng có thể hợp kỹ thuật nhưng vẫn cần animator chỉnh thẩm mỹ.
- Baked Operator ADS phụ thuộc vào vị trí `R_Eyeball`, `ScopeTarget`, `Muzzle` và eye-relief threshold hiện tại. Thay model/scope phải bake lại.
- Bucky per-shell phụ thuộc gameplay duration; nếu thiết kế chuyển sang opening/loop/closing clip riêng, controller và network contract cũng phải được nâng cấp tương ứng.

## 22. Nguyên tắc kiến trúc cuối cùng

Toàn bộ đợt sửa có thể rút lại thành bốn quy tắc:

1. **Binding phải đúng hierarchy**: Avatar không thay thế được helper path mà animation thực sự cần.
2. **Ownership phải duy nhất**: xác định rõ Body clip, gun clip, IK và từng layer được phép ghi Transform nào.
3. **Không gian Transform phải nhất quán**: root scale thay đổi thì translation của child cũng phải đổi tương ứng.
4. **Gameplay là đồng hồ chung**: hai Animator không được tự đoán lúc Reload/Equip hoàn tất; chúng theo cùng authoritative deadline và completion edge.

Đây là lý do bản cuối vừa giữ được animation gốc, vừa không phải sửa Transform/rest pose nguồn, đồng thời tránh được các lỗi chỉ xuất hiện khi chạy game.
