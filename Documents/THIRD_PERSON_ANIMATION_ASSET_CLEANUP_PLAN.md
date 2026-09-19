# Kế hoạch thực thi dọn dẹp asset animation 3P

Ngày lập: 2026-09-02  
Trạng thái: **Đã thực thi cleanup; 3P editor/test tooling đã retire**  
Phạm vi hệ thống: animation nhân vật và súng 3P cho `Clove`, `Sage`, `Gekko`, `Brimstone`; năm loại súng `Vandal`, `Classic`, `Operator`, `Odin`, `Bucky`.

Tài liệu nền: [THIRD_PERSON_WEAPON_ANIMATION_TECHNICAL_REPORT.md](THIRD_PERSON_WEAPON_ANIMATION_TECHNICAL_REPORT.md).

## 1. Mục tiêu

Dọn các prefab thử nghiệm, FBX trung gian, clip `.anim`, Animator Controller, Animator Override Controller, Avatar và Avatar Mask không còn tham gia runtime hoặc quy trình authoring hợp lệ. Kết quả phải:

- giảm asset thừa và tránh dùng nhầm pipeline animation cũ;
- không làm mất khả năng chạy đủ năm vũ khí 3P;
- không phá presentation Humanoid/legacy hiện vẫn được `Sage`, `Gekko`, `Brimstone` dùng;
- không để editor tool hoặc test âm thầm tạo lại asset vừa xóa;
- giữ nguyên GUID của mọi asset production;
- có manifest, bằng chứng dependency, số dung lượng và cách rollback cho từng đợt xóa.

Đây không phải kế hoạch tối ưu texture, material, audio, animation compression hay chuyển toàn bộ player sang Generic. Không sửa nội dung keyframe hoặc cân bằng gameplay trong đợt cleanup.

## 2. Nguyên tắc an toàn và định nghĩa “không còn sử dụng”

Một asset chỉ được đánh dấu **Confirmed Removable** khi đồng thời thỏa tất cả điều kiện sau:

1. Không có serialized reference từ scene, prefab, controller, override controller, ScriptableObject, Addressables group hoặc asset production khác.
2. Không được nạp qua `Resources`, Addressables, `AssetDatabase.LoadAssetAtPath`, GUID, tên/path ghép động, reflection hoặc cấu hình build.
3. Không được source code, editor tool, menu, test hay validation gọi bằng path, GUID hoặc tên.
4. Không phải source input để tái tạo controller, `.anim`, mask, Avatar hoặc prefab production.
5. Không được bất kỳ player nào trong bốn player prefab hoặc năm weapon prefab sử dụng.
6. Không phải asset cần giữ để debug/rollback theo quyết định có chủ đích. Nếu chỉ cần làm lịch sử, Git phải là nơi lưu lịch sử, không giữ bản sao mồ côi trong `Assets`.
7. Đã qua giai đoạn quarantine, compile, asset validation, EditMode, PlayMode và kiểm tra runtime thủ công.

`AssetDatabase.GetDependencies()` không đủ để chứng minh asset vô dụng: nó không thấy source path nằm trong hằng số C#, workflow editor hoặc asset nguồn dùng để generate output. Phải hoàn thành cả dependency audit ở mục 6.

## 3. Hiện trạng kiến trúc phải bảo toàn

### 3.1. Clove — production Generic Path-Bound

Clove hiện có năm profile `GenericPathBound`:

- `Avatar = None`;
- Body clip bind trực tiếp vào đúng hierarchy của Clove;
- súng được mount dưới `R_WeaponMaster`;
- runtime left-hand IK bị tắt;
- Body dùng generated path-bound `.anim`/controller;
- gun Animator dùng clip GNTP đã normalize root Transform;
- Body và gun action đồng bộ bằng cùng trigger/deadline gameplay.

Các nhóm sau được **protect mặc định**, chưa được đưa vào danh sách xóa:

- mọi thư mục `.../S0/3P/Anims/Clove_GenericPathBound/` đang được năm profile dùng;
- `Assets/FPS/Features/Characters/Animation/Content/3P/Clove/GenericPathBoundShared/`;
- `Assets/FPS/Features/Characters/Animation/Content/3P/Clove/NormalizedGunClips/`;
- các controller `Clove*3P_GNTP.controller` đang được prefab/profile production tham chiếu;
- output trước đây của `ThirdPersonWeaponAnimationSyncSetup` đang được production dùng; tool đã retire sau khi output hoàn tất;
- `ClovePlayer.prefab` và năm prefab súng 3P production;
- FBX nguồn/copy nằm trong các thư mục generated nếu builder hiện hành cần chúng để tái sinh output.

Không được xóa FBX gốc chỉ vì đã có `.anim` generated. `.anim` là output, còn FBX có thể là nguồn tái tạo. Muốn xóa nguồn phải chứng minh output được commit, builder không còn cần nguồn và dự án đã chấp nhận mất khả năng regenerate.

### 3.2. Sage, Gekko và Brimstone — Humanoid/legacy

Ba player này chưa được chứng minh là đã chuyển sang cùng contract Generic của Clove. Chúng vẫn có khả năng dùng:

- Avatar Humanoid;
- controller/override legacy;
- weapon parent dưới `R_Hand`;
- IK hoặc presentation profile riêng.

Vì vậy không được suy luận rằng “Clove không dùng nữa” đồng nghĩa “toàn dự án không dùng nữa”. Mọi controller, override, Avatar, mask và FBX TP gốc phải được kiểm tra trên cả ba prefab này trước khi phân loại.

## 4. Manifest phân loại ban đầu

Đây là manifest điều tra, chưa phải lệnh xóa. Trạng thái cuối cùng phải được cập nhật bằng GUID, reverse dependency và kết quả test.

### 4.1. Nhóm A — có độ tin cậy cao, đưa vào quarantine sau khi audit

| Candidate | Lý do | Điều kiện trước khi cách ly |
|---|---|---|
| `Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer_Operator1PStyle_Experiment.prefab` | Prefab thử nghiệm 1P-style, không phải `ClovePlayer.prefab` production | Không có scene/test/tool còn tham chiếu; retire tool tạo prefab tương ứng |
| `Assets/FPS/Features/Characters/Content/Players/Clove/ClovePlayer_OperatorHybrid_Experiment.prefab` | Prefab thử nghiệm hybrid | Không có scene/test/tool còn tham chiếu; retire tool tạo prefab tương ứng |
| `Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Operator/S0/3P/Anims/Experiment_1PStyle/` | Toàn bộ output thử nghiệm 1P-style/hybrid | Production controller không phụ thuộc; gỡ hai experiment builders hoặc chuyển chúng ra khỏi production assembly |
| `Assets/FPS/Features/Weapons/Content/ThirdPerson/Guns/Vandal/S0/3P/Anims/New Animator Controller.controller` | Tên mặc định, không thuộc naming/production manifest | Không có GUID/string reference và không nằm trong controller chain đang dùng |

Các item nhóm A vẫn phải quarantine, không xóa trực tiếp.

### 4.2. Nhóm B — chỉ xóa sau khi retire code/tool/test cũ

Các asset dưới đây trước đây bị `ThirdPersonLeftHandIKSetup.cs` và/hoặc `WeaponAnimatorFlowTests.cs` nhắc tới. Hai code path này đã được retire/xóa theo quyết định của người dùng; các asset tương ứng đã được xử lý trong cleanup.

**IK clip cũ của Clove:**

- `CloveBuckyEquipLeftHandIK.anim`
- `CloveBuckyFireLeftHandIK.anim`
- `CloveBuckyIdleLeftHandIK.anim`
- `CloveBuckyReloadLeftHandIK.anim`
- `CloveClassicReloadLeftHandIK.anim`
- `CloveOdinEquipLeftHandIK.anim`
- `CloveOdinReloadLeftHandIK.anim`
- `CloveOperatorReloadLeftHandIK.anim`
- `CloveVandalReloadLeftHandIK.anim`

**Gun target clip cũ:**

- `CloveClassicGunReloadLeftHandTarget.anim`
- `CloveOdinGunReloadLeftHandTarget.anim`
- `CloveOperatorGunReloadLeftHandTarget.anim`
- `CloveVandalGunReloadLeftHandTarget.anim`

**Override/controller/avatar cũ cần audit cùng tool:**

- `CloveBuckyThirdPerson.overrideController`
- `CloveClassic3P_Gun.overrideController`
- `CloveClassicThirdPerson.overrideController`
- `CloveOdinThirdPerson.overrideController`
- `CloveOperator3P_Gun.overrideController`
- `CloveOperatorThirdPerson.overrideController`
- `CloveThirdPerson.overrideController`
- `CloveVandal3P_Gun.overrideController`
- `CloveTPCoreAvatar.asset`
- `CloveOdin3P_Body.controller`
- `CloveOdin3P_Gun.controller`
- `CloveOdin3P_ThirdPersonGun.controller`

Quyết định bắt buộc cho nhóm B:

- Nếu workflow IK/Humanoid cũ không còn được hỗ trợ: xóa menu/build path, constants và helper tạo asset; đổi test thành xác nhận production Generic; sau đó mới quarantine asset.
- Nếu workflow này vẫn cần cho Sage/Gekko/Brimstone hoặc làm nguồn chuyển đổi: giữ asset, đổi tên/phân thư mục thành `LegacyShared` hoặc `AuthoringSources`, ghi rõ owner và không gọi nó là unused.

Không được chỉ xóa output: builder cũ có thể tái tạo chúng ở lần menu/validation tiếp theo.

### 4.3. Nhóm C — cần kiểm tra tay và bằng chứng dependency

- Các controller `*3P_Body.controller`, `*3P_Gun.controller`, `*GNTP.controller` không nằm trong manifest production rõ ràng.
- Static-pose `.anim` trùng chức năng trong `NormalizedGunClips` hoặc các thư mục cũ.
- Avatar Mask cũ trùng upper-body/no-fingers mask hiện hành.
- Bản FBX copy có prefix `Source_`, `*_Source_Generic.fbx` hoặc bản normalize trung gian.
- `.anim` có hậu tố `LeftHandIK`, `LeftHandTarget`, `Hybrid`, `Clove` nhưng không nằm trong controller production.
- Animator Controller/Override Controller có tên mặc định, hậu tố test, preview, backup, old, copy hoặc experiment.
- Prefab player/weapon preview được tạo trong các đợt sửa nhưng không nằm trong scene/build/test có chủ đích.
- Avatar con được import bên trong FBX. Không xóa riêng sub-asset; nếu cần chỉnh phải xử lý ModelImporter và kiểm tra mọi clip dùng chung.

Mỗi item nhóm C phải được mở trong Inspector/Animator để xác nhận base controller, override table, motion, layer mask, Avatar và serialized owner. Không phân loại theo tên file đơn thuần.

### 4.4. Nhóm D — bắt buộc giữ trong đợt này

- Bốn player prefab production: `Clove`, `Sage`, `Gekko`, `Brimstone`.
- Năm weapon prefab và mọi model/material/hitbox cần để chúng chạy.
- Năm controller/profile Generic Path-Bound của Clove.
- Shared locomotion/additive path-bound clips và mask đang được controller dùng.
- Normalized GNTP clips và active GNTP gun controllers.
- Original TP/GNTP FBX còn được player khác dùng hoặc còn là input của builder production.
- Không giữ editor tool, runner, validator hoặc test chỉ phục vụ 3P animation sau khi output đã hoàn tất; việc nghiệm thu 3P dựa trên kiểm tra hình ảnh thủ công theo xác nhận của người dùng.
- File `.meta` của mọi asset được giữ; không tách asset khỏi `.meta`.

## 5. Sửa code và test trước khi dọn asset

Theo quyết định sau cùng của người dùng, các tool/test editor-only trong phạm vi 3P đã được retire và xóa cùng asset không còn sử dụng. Không chạy lại test suite để làm tiêu chí giữ/xóa; test có thể sai so với kiểm tra hình ảnh thủ công.

### 5.1. Retire experiment builders

Rà soát và xóa hoặc vô hiệu hóa có chủ đích:

- `ThirdPersonGenericWeaponDriverExperiment.cs` — đã retire và xóa.
- `ThirdPersonHybridWeaponAnimationExperiment.cs` — đã retire và xóa.

Nếu không còn giá trị production, xóa cả menu item, constants, generated-output path và test liên quan. Không để script compile nhưng trỏ tới folder đã xóa.

### 5.2. Thu gọn `ThirdPersonLeftHandIKSetup` (đã retire/xóa)

Tách ba trách nhiệm hiện đang bị trộn:

1. builder production Generic Path-Bound;
2. validator production;
3. builder/repair legacy IK/Humanoid.

Giữ (1) và (2). Với (3), hoặc xóa hẳn nếu không còn owner, hoặc chuyển thành legacy tool có manifest riêng. Sau khi quyết định, gỡ mọi constant/path tạo các asset nhóm B không còn hỗ trợ.

Validation production phải chỉ đọc và fail rõ ràng; không được `CreateAsset`, `CopyAsset`, `AddComponent`, sửa prefab hoặc “Ensure” asset để che cấu hình thiếu.

### 5.3. Cập nhật test theo contract hiện hành

`WeaponAnimatorFlowTests` và test liên quan trước đây phải:

- xác nhận năm profile Clove dùng đúng Generic Path-Bound controller, `Avatar=None`, `R_WeaponMaster` và IK tắt;
- xác nhận gun controller/clip normalize đúng cho từng vũ khí;
- không yêu cầu override/IK controller cũ chỉ vì đường dẫn được hard-code;
- vẫn kiểm tra contract Humanoid/legacy riêng của Sage/Gekko/Brimstone;
- fail khi production prefab trỏ vào quarantine hoặc missing GUID;
- không gọi setup/builder để tự chữa asset trước khi assert.

## 6. Quy trình audit dependency

`ThirdPersonAnimationAssetAudit` đã được retire sau cleanup. Manifest trong `Documents/ThirdPersonAnimationAssetAudit/quarantine-manifest.json` được giữ làm bằng chứng lịch sử/rollback Git, không phải Unity asset hay runtime reference.

Đây là quy trình audit đã dùng trước cleanup. Editor tool `ThirdPersonAnimationAssetAudit` đã được retire/xóa; manifest lịch sử vẫn được giữ trong `Documents/ThirdPersonAnimationAssetAudit/quarantine-manifest.json`.

### Bước 1 — chụp baseline

- Save gate bắt buộc ở mục 10.
- Ghi commit/branch, Unity version, active scene và `isDirty`.
- Xuất toàn bộ asset thuộc scope: path, GUID, type, byte size, importer type, labels, Addressables/Resources status.
- Chụp serialized assignments trên bốn player prefab và năm weapon prefab.
- Xuất controller graph: layer, state, motion, transition, mask và nested BlendTree.
- Với override controller, xuất base controller và toàn bộ override pair.

### Bước 2 — forward dependency

Chạy `AssetDatabase.GetDependencies(path, true)` cho:

- bốn player prefab;
- năm weapon prefab;
- gameplay scene/build scenes;
- controller/profile production;
- Addressables groups và Resources assets nếu có.

Hợp kết quả thành `protected-guids.json` hoặc CSV để review được bằng Git.

### Bước 3 — reverse dependency

Với từng candidate:

- lấy GUID từ `.meta`;
- tìm GUID trong toàn bộ text-serialized `Assets`, `Packages`, `ProjectSettings`;
- duyệt dependencies của mọi asset trong scope để tìm owner ngược;
- tìm cả tên/path tương đối và tuyệt đối trong `.cs`, `.asmdef`, test, JSON, asset config và tài liệu build có tác dụng;
- kiểm tra `Resources.Load`, Addressables key/label, custom registry và string ghép động.

Reference chỉ xuất hiện trong quarantine manifest không tính là production dependency. Reference từ test/tool phải được giải quyết, không được bỏ qua.

### Bước 4 — kiểm tra Animator chuyên biệt

- Resolve mọi `AnimatorOverrideController` về base controller và clip cuối cùng.
- Duyệt `BlendTree` đệ quy; không chỉ đọc `AnimatorState.motion` cấp đầu.
- Kiểm tra Avatar Mask, StateMachineBehaviour và animation events.
- Đối chiếu parameter do runtime gửi với parameter có trong controller.
- Xác minh cả Body Animator và gun Animator cho từng profile.
- Với FBX, kiểm tra sub-clips, Avatar source và builder input trước khi đánh dấu xóa.

### Bước 5 — kết luận theo bằng chứng

Audit xuất bảng:

| GUID | Path | Type | Size | Runtime owners | Tool/test owners | Regeneration role | Classification | Reason |
|---|---|---:|---:|---|---|---|---|---|

Không cho phép `Confirmed Removable` nếu bất kỳ cột owner/regeneration nào chưa rỗng hoặc chưa có quyết định retire tương ứng.

## 7. Thực thi hai giai đoạn

Phần quarantine/xóa asset đã được thực hiện. Theo xác nhận của người dùng, sau khi kiểm tra bằng mắt thấy ổn thì không yêu cầu chạy lại test matrix; các test/editor-only 3P đã được xóa.

### Giai đoạn 1 — quarantine có thể rollback

1. Tạo branch `codex/3p-animation-asset-cleanup` từ trạng thái đã lưu; không trộn cleanup với sửa animation mới.
2. Hoàn tất code/test retirement ở mục 5 và compile sạch.
3. Tạo thư mục `Assets/_Quarantine/ThirdPersonAnimation/<YYYY-MM-DD>/` bằng `AssetDatabase`.
4. Di chuyển candidate bằng `AssetDatabase.MoveAsset` để giữ GUID; di chuyển cả folder theo asset operation, không thao tác rời `.meta`.
5. Ghi `quarantine-manifest.json` gồm GUID, old path, new path, size, classification, owner audit và lý do.
6. Không sửa reference để “làm cho test pass” sau khi move. GUID reference hợp lệ sẽ tiếp tục resolve; mục đích quarantine là phát hiện path-string dependency và nguồn bị thiếu.
7. Save, compile, validate và chạy matrix ở mục 9.
8. Giữ quarantine ít nhất một vòng review/runtime verification được người dùng chấp nhận.

Nếu dự án đóng gói toàn bộ `Assets`, đặt quarantine ngoài build scope hoặc dùng label/filter rõ ràng. Không để asset quarantine làm tăng build chỉ vì nó còn nằm trong project.

### Giai đoạn 2 — xóa vĩnh viễn

Chỉ thực hiện khi:

- quarantine suite pass;
- không phát sinh missing script/motion/GUID;
- tất cả năm súng đã được runtime verify;
- cả bốn player prefab được kiểm tra;
- manifest được người dùng duyệt.

Xóa bằng `AssetDatabase.DeleteAsset` theo manifest, không dùng wildcard và không xóa bằng File Explorer. Sau xóa: save, refresh, compile, chạy lại toàn bộ matrix và commit riêng. Git vẫn là cơ chế phục hồi.

## 8. Dung lượng và báo cáo trước/sau

Baseline filesystem hiện biết:

| Candidate có độ tin cậy cao | Kích thước gần đúng |
|---|---:|
| Hai prefab Operator experiment | 3.68 MiB |
| `Experiment_1PStyle/` | 47.84 MiB |
| `New Animator Controller.controller` | < 0.01 MiB |
| Tổng nhóm A hiện biết | khoảng 51.52 MiB |

`CloveTPCoreAvatar.asset` khoảng 0.78 MiB nhưng thuộc nhóm B, không cộng vào mức chắc chắn. Các folder `Clove_GenericPathBound` chiếm dung lượng lớn nhưng là production/output/source hiện hành và **không phải mục tiêu xóa mặc định**.

Audit chính thức phải báo cáo:

- logical size của asset và `.meta` trước quarantine;
- dung lượng theo type: FBX, `.anim`, controller, override, mask, Avatar, prefab;
- dung lượng nhóm A/B/C/D;
- dung lượng sau xóa và mức giảm thực tế trong repository/build;
- lưu ý Git repository không giảm ngay nếu asset vẫn còn trong history; không rewrite history trong kế hoạch này.

## 9. Ma trận kiểm thử bắt buộc

### 9.1. Asset/EditMode

Không áp dụng làm gate acceptance sau quyết định retire test của người dùng; danh sách dưới đây chỉ còn là tiêu chí lịch sử của kế hoạch.

- Mọi protected GUID tồn tại và không nằm trong quarantine.
- Không prefab/controller/override có missing reference.
- Không duplicate/mồ côi production controller theo manifest.
- Mọi controller có đủ parameter runtime cần: locomotion, aim, fire, reload, equip và completion tương ứng.
- Override table không có null clip hoặc base controller bị mất.
- Generic Path-Bound clips có binding path resolve trên `ClovePlayer.prefab`.
- GNTP normalized clips không ghi root Transform sai contract.
- Builder/validator chạy idempotent; lần thứ hai không tạo/sửa asset ngoài dự kiến.

### 9.2. Runtime theo vũ khí

Với từng `Vandal`, `Classic`, `Operator`, `Odin`, `Bucky`:

- Equip từ slot khác rồi về Idle.
- Idle khi đứng yên; Walk/Run và dừng lại.
- AimN/ADS nếu vũ khí hỗ trợ.
- Fire khi đứng, di chuyển và aim; Odin kiểm tra nhiều phát liên tiếp.
- Reload thường; Bucky kiểm tra per-shell; Operator kiểm tra transition sau reload.
- Interrupt/cancel hợp lệ: đổi súng, fire/reload cạnh nhau, death/despawn nếu có.
- Body và gun Animator cùng chạy đúng action; magazine/bolt/slide không bay xa, co hoặc mất.
- Kết thúc action trở lại locomotion/idle, không kẹt pose, méo cổ/tay/ngón.

### 9.3. Theo player và network

- `Clove`: full matrix năm súng với Generic Path-Bound.
- `Sage`, `Gekko`, `Brimstone`: ít nhất Equip/Idle/Move/Fire/Reload cho từng presentation được author; mở rộng đủ năm súng nếu prefab cho phép tất cả.
- Host và remote client thấy cùng action/deadline; owner 1P không bị cleanup 3P tác động.
- F5 3P debug view chỉ quan sát, không thay đổi pose hoặc asset.
- Respawn, đổi slot và thay primary cùng slot không giữ state controller cũ.

### 9.4. Regression

- EditMode suite liên quan animation/weapon/prefab.
- PlayMode suite trên gameplay scene hợp lệ.
- Toàn bộ regression suite nếu cleanup chạm shared FBX/controller/tool.
- Build validation không có missing asset, Addressables key hoặc Resources path.

## 10. Save gate và trình tự Unity bắt buộc

Trước khi vào Play Mode, mở/chạy Test Runner, load/reload scene, compile/domain reload, restart Unity hoặc bắt đầu một chuỗi Unity Editor mutation:

```csharp
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
UnityEditor.AssetDatabase.SaveAssets();
```

Sau mỗi chuỗi trên và sau mỗi test run, gọi lại đúng save gate. Test Runner có thể mở lại `TestScene`, vì vậy phải khôi phục scene người dùng bằng `try/finally`, lưu lại và xác nhận active project scene đúng như ban đầu.

Quarantine tool phải lưu và khôi phục:

- danh sách scene đang mở, active scene và scene setup;
- selection;
- prefab stage nếu có thể khôi phục an toàn;
- Animation Preview/AnimationMode;
- trạng thái Play Mode ban đầu.

Không hoàn tất đợt thực thi nếu active scene chưa được kiểm tra trong Unity và `isDirty == false`.

## 11. Rollback và quản lý Git

- Chụp baseline commit trước cleanup; không dùng `git reset --hard` hoặc `git checkout --` lên thay đổi người dùng.
- Commit riêng theo lớp: `(1) retire tool/test`, `(2) quarantine`, `(3) permanent delete`.
- Rollback quarantine bằng manifest và `AssetDatabase.MoveAsset(newPath, oldPath)` để giữ GUID.
- Rollback permanent delete bằng restore đúng commit/path và cả `.meta`; không tái tạo thủ công `.meta` vì sẽ đổi GUID.
- Nếu test fail, dừng ở candidate đầu tiên gây lỗi, hoàn nguyên candidate đó và cập nhật classification/owner; không sửa production reference tùy tiện để hợp thức hóa việc xóa.
- Bảo toàn mọi thay đổi chưa commit của người dùng, đặc biệt `ClovePlayer.prefab`, scene/prefab và các tài liệu hiện có.

## 12. Tiêu chí nghiệm thu

Cleanup chỉ được coi là hoàn tất khi:

- có protected manifest và deletion manifest theo GUID, có lý do và owner audit cho từng asset;
- không còn experiment prefab/folder/controller đã được duyệt trong production asset tree;
- không còn code, menu, test hoặc validator tham chiếu/tái tạo asset đã xóa;
- không có missing script, missing motion, missing Avatar, null override hoặc duplicate controller ngoài manifest;
- năm vũ khí pass toàn bộ action 3P trên Clove, gồm cả Body và gun Animator;
- Sage/Gekko/Brimstone không bị phá contract Humanoid/legacy;
- multiplayer presentation, 1P và F5 debug view không regression;
- EditMode, PlayMode và regression suite đã chọn đều pass sau permanent delete;
- báo cáo dung lượng trước/sau được lưu;
- scene/setup/selection của người dùng được khôi phục;
- tất cả asset đã được save và active scene có `isDirty == false`.

## 13. Thứ tự thực thi đề xuất

1. Chụp baseline và sinh protected/candidate manifest.
2. Audit nhóm A; retire hai experiment builders.
3. Quyết định số phận workflow legacy trong `ThirdPersonLeftHandIKSetup` và cập nhật tests — đã hoàn tất bằng việc retire/xóa workflow 3P cũ.
4. Audit nhóm B/C trên cả bốn player và năm súng.
5. Quarantine nhóm A rồi chạy test matrix.
6. Quarantine từng cụm nhóm B đã được retire; không trộn nhiều cụm không liên quan trong một lần.
7. Review thủ công và giữ một vòng xác nhận.
8. Xóa vĩnh viễn theo manifest đã duyệt.
9. Chạy lại full validation, đo dung lượng, khôi phục scene và xác nhận clean state.

Kế hoạch này cố ý ưu tiên bằng chứng dependency và khả năng rollback hơn việc xóa tối đa. Asset lớn nhưng là nguồn tái tạo hoặc còn phục vụ một player khác phải được giữ hoặc phân loại rõ, không được xóa chỉ vì không xuất hiện trực tiếp trong controller của Clove.
