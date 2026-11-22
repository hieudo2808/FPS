# Sửa biến dạng gối 3P — 2026-11-15

## Kết quả

Đã sửa cả bốn player prefab bằng hai lớp nhỏ:

- `[CODE Assets/FPS/Features/Characters/Player/Runtime/PlayerLifePresenter.cs:422]`: corrective/helper `_Ndl` dùng half-angle theo knee procedural; `DownedKneeVolume` được bật theo `raise * weight`, và reset về 0 ở graph invalid, revive, disable/destroy.
- `[ASSET Assets/FPS/Features/Characters/Content/Players/*/Models/DownedKneeMesh.asset]`: mesh clone giữ topology, UV, submesh, bone weights và bindposes của FBX gốc; thêm một blendshape cục bộ quanh gối.
- `[BLENDER Reports/BlenderWork/PlayerKneeCorrectives.blend]`: Blender MCP tạo shape DQS−LBS theo đúng skin matrices đã export từ Unity. Delta tối đa: Brimstone 7.23 mm, Clove 11.12 mm, Gekko 8.58 mm, Sage 2.87 mm.

Shape không tác động pose đứng vì weight mặc định là 0. Khi player gục, shape lên 100; khi revive/pool reuse, shape về 0.

## Kiểm chứng

- `[TEST] PlayerKneeCorrectiveTests`: 4/4 pass. Kiểm tra vertices, normals, tangents, UV, weights, bindposes, indices/submeshes và reset lifecycle.
- `[PLAYTEST] DownedSidearmReviveAndDirectionalDeathAcrossPeers`: 1/1 pass. Log ghi `knee corrective L_Knee ... 0.000 deg`, `R_Knee ... 0.000 deg`, barrel `0.000 deg`, revive `eyeDrop=0.00`.
- `[PLAYTEST screenshot] Reports/PlayerLife-2026-11-14/fix/3P-downed-side.png`
- `[PLAYTEST screenshot] Reports/PlayerLife-2026-11-14/fix/3P-downed-closeup.png`
- `[BLENDER/EDITOR screenshot] Reports/BlenderWork/KneeBeforeAfterContact.png` — mỗi hàng là before/after cùng camera cho Brimstone, Clove, Gekko, Sage. Ảnh cận editor đã xóa sau review độc lập; contact sheet được giữ lại.

## Skills và quy tắc áp dụng

- `unity-skills`: “After a write, verify in the Editor” — kiểm mesh/prefab bằng actual presenter, ảnh và tests.
- `unity-animation`: “Verify ... during Play mode” — host/client runtime xác nhận pose, grip và revive.
- `unity-asset`: “BATCH-FIRST” — cài bốn mesh/prefab trong một menu batch qua AssetDatabase/PrefabUtility.
- `unity-editor`: “editor_execute_menu requires exact menu path” — dùng menu đã định nghĩa cho export/install/capture, kiểm compile state.
- `debugging-and-error-recovery`: “PRESERVE evidence ... DIAGNOSE ... FIX the root cause” — lưu thất bại assert cũ, đối chiếu half-angle và cập nhật assertion; xác minh lỗi cache ở ảnh A/B.
- `code-review-and-quality`: “After any bug fix (review both the fix and the regression test)” — review độc lập phát hiện và xác nhận sửa normal/tangent cùng ảnh cận cảnh.
- `ponytail`: “Lazy code without its check is unfinished” — thêm một regression test parameterized đủ bốn prefab; reuse presenter hiện có, không thêm runtime component.

Áp dụng Save Gate: save scene authored, `File/Save Project`/`AssetDatabase.SaveAssets()` trước và sau thao tác; scene cuối là `MainMenu`, `isDirty=false`.


## Giới hạn và cách chỉnh tiếp

- Các FBX gốc được giữ nguyên. Runtime prefab tham chiếu `DownedKneeMesh.asset`; source shape editable nằm trong `.blend`. Cách này tránh thay rig/import transforms khi round-trip FBX qua Blender.
- Trong Blender, mỗi `* Corrective Source` có Basis + DownedKneeVolume; `* Downed Preview` lưu pose để xem. Text block `Export Corrective Shapes.py` xuất delta mới; Unity menu `Tools/FPS/Player Life/Install Blender Knee Shapes` cập nhật asset.
- Shape là residual offline của DQS−LBS tại pose gục hiện tại, không phải DQS runtime tổng quát. Phải bake lại nếu đổi leg pose hoặc chiến lược knee helper.
- Sage giảm rõ highlight nhọn; ba body khác cải thiện nhẹ. Mesh gốc vẫn thấy cạnh khi zoom sát. Chưa kiểm toàn bộ các animation khác; shape đứng mặc định là 0 và source channels được test giữ nguyên.
- Host/client dùng peer build có sẵn; asset mới được chụp và kiểm tra trên Editor host. Chưa rebuild peer để kiểm visual asset mới ở phía executable client.

## Log thực thi trích xuất

```text
job 8bd0f1e2: status=completed; totalTests=4; passedTests=4; failedTests=0
resultSummary: Test run completed: 4/4 passed.
job 59c6eaa0: status=completed; totalTests=1; passedTests=1; failedTests=0
resultSummary: Test run completed: 1/1 passed.
[157.3s] host view of client body: head 0.52 m, hips 0.19 m above the capsule base
[157.3s] knee corrective L_Knee: half-angle orientation error=0.000 deg
[157.3s] knee corrective R_Knee: half-angle orientation error=0.000 deg
[157.4s] actual barrel vs aim=0.000 deg
[161.6s] revived: host overriding=False slot=0 client eyeDrop=0.00
```

Bản JSON đầy đủ: `Reports/BlenderWork/test-editmode-final.json`, `test-hostclient.json`.
