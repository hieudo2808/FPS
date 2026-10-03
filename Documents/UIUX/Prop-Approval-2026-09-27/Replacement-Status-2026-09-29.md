# Prop replacement — continuation status, 29/09/2026

> Cập nhật tải file 30/09/2026: đã xác nhận đủ 17/17 source archive, xem [Download receipt](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Download-Receipt-2026-09-30.md). Các nhận định thiếu file dưới đây là snapshot lịch sử trước download; manifest và Asset Matrix đã cập nhật. Chưa import/thay/save scene trong lượt tải này.

> Cập nhật import sau đó: 17/17 model đã được tổ chức dưới `Assets/ThirdParty/ApprovedProps/<code>/` và verified bằng Gerty/Unity. Xem [Import verification](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Import-Verification-2026-09-30.md). `GameScene` vẫn không dirty; scene placement/replacement chưa chạy.

**Kết luận: chưa thay xong.** Công việc mới của phần asset gate là metadata, contact sheets, provenance và bảng đích cho 108 stable supply. Không coi việc gọi lại helper cũ là tiến độ thay các model còn thiếu.

## 1. Deliverables thực có

| File | Nội dung / trạng thái |
|---|---|
| [Asset-Matrix.md](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Asset-Matrix.md) | Mapping object → asset, download pages, approval/blocker, import path dự kiến, coverage đủ 13 loại Lab. |
| [Provenance-Manifest.csv](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Provenance-Manifest.csv) | 21 dòng: 17 model web, 2 model case đã import, microscope và folder được cấp. Tách license nguồn tải thực, quyền được xác nhận và phần chưa rõ. |
| [Asset-Gate-Sources.json](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Asset-Gate-Sources.json) | 17 source metadata records, source URLs/author/license/triangles/timestamp. Không có original archive nào trong 17 record được ghi là confirmed. |
| [Supply-Replacement-Targets.csv](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Supply-Replacement-Targets.csv) | 108 ID và GlobalObjectId đọc từ scene hiện tại; dữ liệu hiện tại và planned mapping tách riêng. |
| [Build-AssetGateBoards.ps1](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Build-AssetGateBoards.ps1) | Helper dựng contact sheet từ preview tác giả; không tạo model hoặc sửa gameplay. |
| Contact sheets | Combat, Equipment, Lab Candidates: mỗi nhóm 1920×1080 và 1280×720. Sáu ảnh decode được, đúng kích thước; các sheet 720p đã xem kiểm tra bố cục. |

Ảnh nguồn không phải isolated render của model đã import, không phải ảnh after. Chưa có after của prop còn thiếu model. Ảnh before trong [Scene-Audit](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Scene-Audit-2026-09-29.md) giữ nguyên để đối chiếu, không ghi đè.

## 2. Kiểm tra trực tiếp mới nhất

Đọc bằng Unity Skills/Gerty, không đọc raw YAML:

- Scene: `Assets/FPS/Scenes/GameScene.unity`, **8 root, dirty, Edit Mode**, không compiling tại thời điểm query.
- **108 CampaignSupply, 108 supplyId duy nhất**, 108 GlobalObjectId của component duy nhất trong bảng đích.
- **27 ammo caches đều ammoWeapon = null**. Source ammo station 0/1 là 30, station 2 là 45. Party gate 1/2/4 đang có.
- **72 survival supplies đều minimumPartySize = 1**. Planned mapping đổi 36 hàng bổ sung sang 3; chưa apply.
- **9 medicineOnly caches** được tách riêng, không gộp với 18 survival medkit.
- `Laboratory_1_0.chooseReward = true` đã kiểm tra trực tiếp; giữ nguyên khi đổi visual/gán Vandal.
- Console snapshot mới khoảng **10:21 UTC: 0 error, 0 warning**. Không clear Console trong bước đọc này. Kết quả này không thay thế compile/test mới và không xóa lịch sử lỗi của các thao tác trước.

Baseline 14.960 GameObject / 7.480 renderer và phép đo 72 item ngoài bàn vẫn lấy từ audit trước; không tuyên bố đã quét lại/đo lại toàn scene trong phần documentation này.

### Kiểm tra dữ liệu báo cáo đã chạy

PASS:

- 17 mã source không trùng, 21 mã manifest không trùng.
- URL, license URL và số source triangles từng record khớp JSON; không có model chưa tải nào bị ghi thành imported.
- 108 supplyId và component GlobalObjectId duy nhất.
- Cả 27 mapping weapon/amount/gate khớp bảng đã khóa cho ba chapter.
- Phân bố planned cache: Classic 9, Vandal 6, Bucky 6, Operator 3, Odin 3.
- 72 survival: 36 gate 1 và 36 gate 3; đúng loại G1/G2/M1-S1/M2.
- Giữ medicineOnly, alternative reward và gate của 9 MedicalCase.
- Cả 6 PNG đúng kích thước yêu cầu.

Đây là **kiểm tra báo cáo/planned data**, không phải EditMode/PlayMode test của game. Chưa có ID nào được gán `REPLACED_AND_VALIDATED` trong đợt mới này.

Có thể dựng lại và tự kiểm tra kích thước các sheet từ preview đã có bằng:

```powershell
& 'E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Build-AssetGateBoards.ps1'
```

## 3. Trạng thái từng phần plan

| Phase | Đã có | Chưa xong |
|---|---|---|
| 1 — asset gate | Source/license metadata, 6 contact sheets, matrix, manifest | Original models cho 12 lựa chọn đã duyệt; K1 chưa đạt kỹ thuật; Lab chưa đủ/duyệt family; isolated/in-scene renders sau import chưa có. |
| 2 — technical audit | Audit nền và live supply identity/reward check | Chưa đo after bounds, support toàn footprint, LOS và NavMesh của replacement mới. |
| 3 — consoles | Baseline xác định 3 owner phải giữ | AsylumFuse/LabTrace/LabTransmit chưa được thay model còn thiếu. |
| 4 — consumables | Mapping 18 mỗi loại và 9 MedicalCase | Chưa thay world/held/third-person/projectile/HUD bằng mẫu mới. |
| 5 — supply/ammo | 108 planned rows, amount clamp, party gate và reward preservation | 9 bàn, 72 placement, 27 ammo models/mapping và 27 SupplyWorktop legacy chưa xử lý. |
| 6 — Lab | Matrix đủ 13 loại / 71 instance và 4 ứng viên | Chưa có approved family đủ coverage; chưa thay 71 instance. |
| 7–10 — rollback, QA, report/save | Giữ baseline/evidence, ghi blocker | Chưa có full scene/runtime/network/navmesh/visual acceptance; chưa tới final save gate. |

Không thay foundations/rails/lighting/cabin/Tank geometry/collision architecture. Không tạo thêm manager, framework interaction hoặc primitive prop.

## 4. Blockers cụ thể

| Nhóm | Trạng thái hiện tại | Cần gì để tiếp tục |
|---|---|---|
| L2-R, G1/G2, M1/M2, AM1–AM5, K2, R1 | **BLOCKED_MISSING_ASSET** | File gốc qua tải chính thức, kèm materials/textures/license. Trang download nằm trong matrix. |
| K1 | **BLOCKED_TECHNICAL**, đồng thời thiếu model gốc | Kiểm tra model 126.296 tris và optimization hợp lệ hoặc lựa chọn khác được duyệt. Không tạo fuse giả. |
| LabEquipment + SupplyBench | **Pending approval / thiếu asset đã chốt** | Chốt family và từng loại chưa có; bốn preview hiện tại không phải bộ đủ 13 loại. |
| Supply layout/legacy cleanup | Chưa được apply | Đi cùng model/bàn thật được duyệt và validation; không dùng cube đỡ đồ. |
| Scene-dependent tests | **Chưa chạy lại thành công** | Xử lý trạng thái scene dirty theo chỉ thị người dùng; không tự save/reload để vượt guard. |

Folder `E:/ProjectSettings/Assets/` có Unity mesh assets/prefab/texture; chưa xác nhận matched original của model đã duyệt. Không kết luận folder không có 3D assets. Không yêu cầu mật khẩu/token Sketchfab.

## 5. Sai lệch save gate đã xảy ra — không che giấu

Ở phần đầu continuation, agent đã:

1. Gọi lại `ApprovedPropImplementation.ApplyLocalProps()`, verify rồi SaveScene.
2. Gọi lại `ApplyRemainingLocalProps()`, verify rồi SaveScene.
3. Gọi `ApplyEvidenceCases()`; batch này chưa save.

Hai lần SaveScene đầu **không có xác nhận save gate mới**, sai với plan. Các helper trên chủ yếu đặt lại những asset đã có trước đó, không giải quyết missing original models; không được báo là đã thay xong ba console hoặc ammo.

Một lần thử undo có guard đã **dừng trước khi undo** vì Undo context đổi: group 4 / `Place approved evidence cases` không còn là context hiện tại. Query mới nhất là group 7 / `Place approved cabinet, index and archive terminal`. Không có cơ sở an toàn để tự Undo hàng loạt hay reload scene từ đĩa; cũng không kết luận ai tạo mọi thay đổi chỉ từ tên Undo group.

Scene vẫn dirty. Từ khi phát hiện sai lệch, phần tiếp nối chỉ đọc Editor và tạo tài liệu/ảnh báo cáo, **không save, reload, xóa dirty flag hoặc chạy lại helper authoring**. Git worktree có nhiều thay đổi từ trước; không reset/revert chúng.

## 6. Giới hạn test / điều kiện bàn giao tiếp

- Lần yêu cầu `ApprovedPropSceneTests` đã bị từ chối do scene chưa lưu; **không có test mới nào pass từ lần yêu cầu đó**.
- Các kết quả pass trong `Implementation-2026-09-28.md` thuộc snapshot/lượt trước, không đem chứng minh snapshot dirty hiện tại.
- Không có thay đổi C# runtime mới trong phần asset-gate documentation, không tuyên bố đã compile/test campaign sau replacement.
- Chưa chạy host/client, 4 peer, packet loss, checkpoint rollback hoặc visual QA của model mới.
- Bounds/collider hai evidence case đã được đọc lại trong continuation, đúng khoảng 0,62 m cạnh dài nhất; kiểm tra hẹp đó không thay full scene tests. Transform scale 100 tự nó không chứng minh sai khi mesh unit/world bounds đúng.
- Giữ scene dirty hiện tại để không mất công việc. Final save vẫn cần approval của người dùng **sau** khi các điều kiện validation được đáp ứng; không yêu cầu save ngay để làm báo cáo trông hoàn tất.

Bước tiếp theo cần original model files và quyết định family Lab. Những phần nào có đủ điều kiện sẽ tiếp tục riêng; phần thiếu vẫn ghi blocker, không dùng substitute chưa được duyệt.
