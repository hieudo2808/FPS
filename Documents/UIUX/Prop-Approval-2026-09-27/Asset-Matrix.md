# Asset Matrix — gameplay prop replacement gate

Latest status, 2026-10-02: all 71 LabEquipment replacements are saved in GameScene with explicit user approval. Final-batch targeted EditMode tests passed **12/12** (10 prop, 1 Files, 1 morgue route). Scene is clean; runtime/full visual acceptance remains pending. Section 11 supersedes the historical planning snapshots below.

Cập nhật: 30/09/2026. **Đã tải và import 17/17 source archive vào `Assets/ThirdParty/ApprovedProps/<code>/`; gate scene placement/visual/technical vẫn chưa hoàn tất.**

### Latest execution update — 30/09/2026

- User approved C2/C3 as the main bench/fume-cupboard family after the Unity preview. C1/C4 remain candidates, not approved substitutes.
- Actual Unity renders and the remap audit are under `Validation-2026-09-30/`. Older sections below describing packages as not imported are historical, superseded by this update.
- Material remaps pass for 16 packages. The full C3 import still has two pipe meshes using a default material; these are excluded from the bench derivative.
- AM2 and AM5 count differences exactly match their 352 and 8 zero-area triangles. C2 differs by its 128 zero-area triangles. C4 has 54,288 evaluated triangles in both Blender source and exported FBX; the earlier 32,484 count omitted evaluated modifiers.
- C3 export omitted 20 Icosphere objects (1,600 triangles); this is not a validated full-source conversion. Selected bench/cabinet parts are present; do not claim all C3 geometry was faithfully imported.
- M1 green-mark and M2 fictional T-9 label derivatives replace the original protected/real-world markings, without changing the source mesh.
- Downloads cleanup completed on 2026-10-03: 25 validated asset archives, including the duplicate `m67-grenade.zip`, were moved to the Windows Recycle Bin. Unrelated Downloads files were left untouched. Receipt: `Download-Cleanup-2026-10-03.json`.

Xem [Download-Receipt-2026-09-30.md](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Download-Receipt-2026-09-30.md) cho file thực tế, định dạng, entry model và SHA-256. 12 mẫu chính, K1 và 4 ứng viên Lab đã có nguồn; không cần tải lại. Giữ riêng approval và archive confirmation.

Xem [Import-Verification-2026-09-30.md](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Import-Verification-2026-09-30.md) cho đường dẫn Unity, mesh/triangle audit và blocker sau import. Import asset không đồng nghĩa đã thay object trong `GameScene`.

Tài liệu này bổ sung trạng thái mới sau [audit scene](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Scene-Audit-2026-09-29.md), [phê duyệt visual trước](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Approval.md) và [phụ lục combat](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Combat-Items-Addendum.md). Không thay đổi các lựa chọn người dùng đã duyệt; không dùng các ảnh proposal làm bằng chứng đã import.

## 1. Quy ước và bằng chứng

- **Approved visual:** người dùng đã chọn hình dáng. Không đồng nghĩa có original archive, import thành công hoặc đạt test.
- **Blocked:** thiếu file gốc, chưa đạt kiểm tra kỹ thuật hoặc chưa đủ bằng chứng license.
- **Pending:** ứng viên mới, chưa được người dùng chọn. Không được tự thay vào scene.
- **Rejected:** phương án giữ ControlDesk tự dựng cho L2 đã bị bác bỏ; không dùng lại làm phương án hoàn thành.

Metadata của 17 model được lấy từ nguồn Sketchfab ngày 29/09/2026, khoảng 10:13 UTC, lưu trong [Asset-Gate-Sources.json](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Asset-Gate-Sources.json). Số tam giác bên dưới là metadata của nguồn, **không phải số tris đã đo trong Unity**. URL model là trang để tải chính thức; không phải URL ZIP trực tiếp.

Lịch sử 29/09: trước khi người dùng đăng nhập, Download 3D Model yêu cầu login và endpoint không xác thực trả 401. **Blocker tải file này đã được gỡ:** 17 archive thực tế trong Downloads đã được kiểm tra, gồm ZIP lồng của G2/K2/LAB-C1. Không bypass login và không lấy geometry từ viewer.

[Provenance-Manifest.csv](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Provenance-Manifest.csv) tách source/license, credit, đường dẫn thật và đường dẫn dự kiến. License theo khai báo nguồn không bảo đảm độc lập quyền với mọi nhãn hiệu/biểu tượng trong texture.

## 2. Contact sheets

Các sheet dùng preview gốc của tác giả, có ghi rõ **NOT Unity imports / NOT after-replacement screenshots**. Mỗi sheet có hai bản đã kiểm tra kích thước.

| Nhóm | 1920×1080 | 1280×720 |
|---|---|---|
| Grenades, medical, Classic/Vandal | [Mở ảnh](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Asset-Gate-Combat-1920x1080.png) | [Mở ảnh](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Asset-Gate-Combat-1280x720.png) |
| Bucky/Operator/Odin, computer, card, radio | [Mở ảnh](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Asset-Gate-Equipment-1920x1080.png) | [Mở ảnh](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Asset-Gate-Equipment-1280x720.png) |
| Lab candidates + fuse hold | [Mở ảnh](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Asset-Gate-Lab-Candidates-1920x1080.png) | [Mở ảnh](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Asset-Gate-Lab-Candidates-1280x720.png) |

Ảnh M1 còn chữ thập đỏ, M2 còn nhãn vaccine thật và L2-R còn bàn gỗ vì đây là preview nguyên bản. Đó **không phải** chấp thuận đưa nguyên trạng các phần này vào game.

## 3. Model đã được chọn — file gốc đã tải, chờ audit kỹ thuật

Tất cả hàng trong bảng này có metadata **CC BY 4.0**. Khi sử dụng: ghi tên model/tác giả, nguồn, license và thay đổi đã thực hiện. K1 chỉ có hướng hình dáng, vẫn technical hold.

| Mã | Trang nguồn / tác giả | Source triangles | Trạng thái |
|---|---|---:|---|
| L2-R | [Computer Desk](https://sketchfab.com/3d-models/computer-desk-ff0674f66e11462e8b3417dee2b58ac9) — CR!STALLL | 12,548 | Approved visual / Source confirmed — chờ audit import |
| G1 | [M67 GRENADE](https://sketchfab.com/3d-models/m67-grenade-d202644dfaf441a0a145befbd7add45a) — Tiago Lopes | 4,747 | Approved visual / Source confirmed — chờ audit import |
| G2 | [Incendiary Grenade](https://sketchfab.com/3d-models/incendiary-grenade-74a5e9a18c1947118d1895dca900afb5) — R.Linden | 2,796 | Approved visual / Source confirmed — chờ audit import |
| M1-S1 | [Medical Kit Bag](https://sketchfab.com/3d-models/medical-kit-bag-abc2f95bce4c4e69bfc0d8742d37e78b) — yronthal | 9,983 | Approved visual / Source confirmed — chờ audit import |
| M2 | [AZ Vaccine vial](https://sketchfab.com/3d-models/az-vaccine-vial-a1bb302ae09645e78cbda7bd53fd9dff) — VRC-IW | 1,330 | Approved visual / Source confirmed — chờ audit import |
| AM1 | [Handgun Ammo Box REMAKE](https://sketchfab.com/3d-models/handgun-ammo-box-remake-74871b58382a418ab310e6d22b1e263f) — jsandwich96 | 1,330 | Approved visual / Source confirmed — chờ audit import |
| AM2 | [Assault Rifle Ammo Box REMAKE](https://sketchfab.com/3d-models/assault-rifle-ammo-box-remake-0e880a3c2cfd4ea9a4891a81f621733b) — jsandwich96 | 6,724 | Approved visual / Source confirmed — chờ audit import |
| AM3 | [Shotgun Ammo Box](https://sketchfab.com/3d-models/shotgun-ammo-box-964f780f87d34d54890f9de94792fd01) — jsandwich96 | 2,732 | Approved visual / Source confirmed — chờ audit import |
| AM4 | [Sniper Ammo Box (Game-ready)](https://sketchfab.com/3d-models/sniper-ammo-box-game-ready-5338de8c47ea4f68aa40dd51602a2b53) — jsandwich96 | 6,000 | Approved visual / Source confirmed — chờ audit import |
| AM5 | [Ammunition Box](https://sketchfab.com/3d-models/ammunition-box-17898b0b68114516b704d0aabc9d2142) — Darren McNerney 3D | 7,492 | Approved visual / Source confirmed — chờ audit import |
| K2 | [Keycard](https://sketchfab.com/3d-models/keycard-a0033bf027364417a7777d98d03980e1) — nuFF3 | 38 | Approved visual / Source confirmed — chờ audit import |
| R1 | [Military Radio](https://sketchfab.com/3d-models/military-radio-f5c8f0420f054b32a2e977fbcd248d84) — curichenkow | 28,807 | Approved visual / Source confirmed — chờ audit import |
| K1 | [Fuse 3d model](https://sketchfab.com/3d-models/fuse-3d-model-d2ed4a3206c1497ba9634adff36d24a0) — AliA Animations | 126,296 | Blocked — technical hold; đã có nguồn BLEND |

### Asset nào cho object nào

| Đích | Asset | Số lượng / điều kiện |
|---|---|---|
| AsylumFuse | K1 và thiết bị điện thật phù hợp | 1 objective; chưa có fuse đạt kỹ thuật. Không tạo fuse giả hoặc pedestal. |
| LabTrace | L2-R, chỉ phần máy tính | 1 objective; không import bàn gỗ/ghế. Phải có mặt đỡ thật. |
| LabTransmit | R1 | 1 objective; giữ authority, target/nextTarget, LOS. |
| Frag pickup | G1 | 18 world pickups; kiểm tra thêm held, third-person, projectile, HUD. |
| Incendiary pickup | G2 | 18 world pickups; bỏ legacy ThermiteCore subtree sau validation. |
| Medkit pickup | M1-S1 | 18; đổi dấu chữ thập đỏ sang nhãn MEDICAL trung tính. |
| Antidote pickup | M2 | 18; đổi nhãn vaccine thật sang T-9 hư cấu. |
| MedicalCase | M1-S1 | 9; không gộp số lượng này vào 18 medkit tiêu hao. |
| Classic ammo | AM1 | 9 cache; whole-pack amount 24. |
| Vandal ammo | AM2 | 6 cache; amount 30 theo mapping này. |
| Bucky ammo | AM3 | 6 cache; station 2 amount 40, station 1 amount 30. |
| Operator ammo | AM4 | 3 cache; amount 10. |
| Odin ammo | AM5 | 3 cache; amount 30. |
| AsylumTransfer card | K2 | Mẫu đã duyệt trước; commit/consume theo objective, không grant từ visual. |
| Factory extraction radio | R1 | Mẫu đã duyệt trước; hiện dùng Electric_box_v2 làm radio, chưa đổi. |
| 9 SupplyBench | Chưa chốt | Phải cùng family với Lab; không dùng bàn gỗ L2-R hoặc cube thay thế. |
| 27 SupplyWorktop nhỏ | Không cần model mới | Legacy dư trong CaseVisual; cần tắt subtree và validate sau asset gate. |

Không dùng một asset đã duyệt cho **vai trò khác chưa được duyệt**: ví dụ EquipmentCase bạc đang có không tự được coi là mẫu đã chốt cho 20 TransitCase.

### Import và kiểm tra kỹ thuật

Đích dự kiến cho model mới: `Assets/ThirdParty/ApprovedProps/<Code>/`; dấu gạch nối trong mã được đổi thành underscore theo manifest. **Chưa tạo/import các đường dẫn dự kiến này.** Tái dùng root ThirdParty hiện có, không thêm root song song.

Với từng original archive, cần ghi dữ liệu thực:

1. File/archive nguồn, dependencies và license đi kèm; chỉ import phần được dùng, không script lạ của pack.
2. Mesh/submesh, vertex/triangle count, material slots, texture names/resolution/channels.
3. Material tương thích Built-in renderer; normal maps đúng loại, albedo/sRGB và mask đúng dữ liệu. Không giả định PBR pack sẽ tự đúng shader.
4. Source unit, bounds thế giới, pivot, uniform scale, normals và chiều mặt. Không dùng x100 như quy tắc chung.
5. Collider/interaction body, layer, LOS/range, mặt đỡ toàn footprint và clearance.
6. Ảnh isolated từ model thật và ảnh trong scene. Archive đã tải nhưng **import/isolated render/scene placement vẫn NOT RUN** trong lượt tải này; không điền số đo giả.

Nguồn thực có 10 gói FBX, 1 DAE (G2), 3 GLB (M1-S1/C2/C3), 3 BLEND (AM1/K1/C4). Những mẫu không có FBX cần chuyển đổi có kiểm tra, không đổi đuôi file hoặc giả định tải nhầm.

CC BY cho phép adaptation nhưng không tự cấp quyền dùng nhãn thương hiệu hoặc biểu tượng được bảo hộ. Nhãn thay phải được kiểm tra trên texture thật; chưa khẳng định đã sửa.

## 4. Lab family — cần duyệt, chưa đủ 13 loại

**Không đề xuất trộn cả bốn bộ.** C2/C3 cùng tác giả là hướng nghiên cứu một family; C1 là ứng viên kệ/xe đẩy; C4 là phương án bàn khác. Preview chưa chứng minh các bộ cùng scale/palette hoặc đủ từng thiết bị.

| Mã | Model / tác giả | License | Source triangles | Phạm vi có thể xem xét |
|---|---|---|---:|---|
| LAB-C1 | [Lab Shelf & Utility Cart | Virus Laboratory](https://sketchfab.com/3d-models/lab-shelf-utility-cart-virus-laboratory-f0a63ba527db45be91b5520ae4470dbd) — Naked Singularity Studio | CC BY 4.0 | 5,264 | Kệ/xe đẩy; tác giả mô tả 2K PBR, real scale |
| LAB-C2 | [FUME CUPBOARDS](https://sketchfab.com/3d-models/fume-cupboards-c709d5d4ecea4f92bdfb51a0a18ec16f) — NightCandle | CC BY-NC 4.0 | 3,569 | Tủ hút; cùng tác giả với C3 |
| LAB-C3 | [Lab](https://sketchfab.com/3d-models/lab-7e0ccf8b9a2f4f08a7f69c26a4781ed1) — NightCandle | CC BY-NC 4.0 | 201,164 | Ứng viên family bàn/tủ Lab; số tris của toàn set, chưa biết mesh tách |
| LAB-C4 | [lab bench](https://sketchfab.com/3d-models/lab-bench-1ac2a62c52a848bbaf746146dc7253f8) — Konstantin_Keller | CC BY 4.0 | 54,480 | Phương án bàn thay thế; 54k tris toàn set, chưa duyệt palette |

C2/C3 là **noncommercial**: phù hợp với phạm vi giáo dục cá nhân không kinh doanh người dùng đã nêu, nhưng không được ghi thành license unrestricted. Nếu đổi mục đích sử dụng phải đánh giá lại. Cả bốn mẫu đã có original archive để kiểm tra; chưa import và chưa được duyệt thay scene.

| Loại | Instance | Ứng viên / phần còn thiếu | Gate hiện tại |
|---|---:|---|---|
| ControlDesk | 9 | Phần computer L2-R; tabletop Lab chưa chốt | Pending family; source confirmed |
| LabBench | 7 | C3 hoặc C4, không đồng thời mặc định | Pending; source confirmed, chưa xác nhận mesh tách |
| ReagentCabinet | 9 | C3 có thể có cabinet; chưa xác minh | Pending; chưa có mapping hợp lệ |
| ColdStorage | 5 | Chưa có mẫu phù hợp được xác minh | Blocked missing asset |
| ServerRack | 7 | Chưa có mẫu phù hợp được xác minh | Blocked missing asset |
| Centrifuge | 3 | Chưa có mẫu phù hợp được xác minh | Blocked missing asset |
| FumeHood | 2 | C2; đối chiếu C3 cùng tác giả | Pending |
| LabCart | 2 | C1 | Pending family |
| LabSink | 2 | Chưa xác nhận C3 có bồn rửa tách rời | Pending; chưa có mapping hợp lệ |
| LabStool | 1 | Chưa có mẫu phù hợp được xác minh | Blocked missing asset |
| SpecimenChamber | 1 | Chưa có mẫu phù hợp được xác minh | Blocked missing asset |
| TransferTrolley | 3 | C1 | Pending family |
| TransitCase | 20 | Chưa chốt model cùng family | Pending; không tự đổi sang S2 |
| **Tổng** | **71** | **13 loại; chưa có family đủ coverage** | **Chưa triển khai** |

Việc chưa có asset cho một hàng không cho phép tạo model primitive, không cản các nhóm độc lập đã có đủ asset và approval. Không đưa foundations, rails, lighting, cabin, arena hoặc NavMesh geometry vào danh sách thay này.

## 5. Asset thật đã có — không import lại

| Mã | File thật | Provenance | Tình trạng |
|---|---|---|---|
| K3 | `Assets/ThirdParty/ApprovedProps/RPaciorekHardCases/EvidenceCase.fbx` | Robert Ryszard Paciorek; GitHub BlenderAssets; MIT kèm LICENSE.txt | 2 case đã có. Đọc lại bounds/collider là kiểm tra hẹp, chưa thay thế test scene. |
| S2 | `Assets/ThirdParty/ApprovedProps/RPaciorekHardCases/EquipmentCase.fbx` | Cùng nguồn tải thực tế MIT | 27 case bạc đã có; không phải AM1–AM5; còn 27 SupplyWorktop dư. |
| MICROSCOPE | `Assets/FPS/Features/World/Content/ExperimentFacility/Models/LabEquipment/OmaxMicroscope.fbx` | 3Donimus / Poly Pizza / CC BY 3.0 theo THIRD_PARTY_NOTICES | 4 instance, loại khỏi nhóm tự dựng; chờ visual consistency QA. |

K3/S2 được lấy từ GitHub, không ghi nhầm license của bản listing Sketchfab thành license của nguồn thực đã tải. Giữ thông báo MIT trong folder. Các PC_Monitor, Keyboard, Paper và tủ điện tái dùng giữ hồ sơ cũ; chưa có cơ sở tuyên bố đã tái xác minh license gốc của toàn bộ pack.

`E:/ProjectSettings/Assets/` có Unity mesh `.asset`, prefab và texture; **không phải folder chỉ có icon**. Chưa xác định chúng trùng đúng model đã duyệt. Cần Editor/dependency/preview audit trước khi đề xuất mẫu thay khác; quyền dùng folder không tự biến mỗi asset thành lựa chọn visual đã được duyệt.

## 6. Mapping dữ liệu đã khóa, chưa áp dụng

[Supply-Replacement-Targets.csv](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Supply-Replacement-Targets.csv) có **108 ID đọc trực tiếp từ Editor** ngày 29/09/2026, kèm GlobalObjectId của component, path, reward, chooseReward, medicineOnly, ammo và party gate hiện tại. Các cột `planned*` chỉ là đích dự kiến. Thông tin support lấy từ baseline audit, không giả làm phép đo sau thay.

| Suffix station/row | Weapon | Source ammo | Effective ammo | Min party |
|---|---|---:|---:|---:|
| _0_0 | Classic | 30 | 24 | 1 |
| _1_0 | Vandal | 30 | 30 | 1 |
| _2_0 | Bucky | 45 | 40 | 1 |
| _0_1 | Operator | 30 | 10 | 2 |
| _1_1 | Odin | 30 | 30 | 2 |
| _2_1 | Classic | 45 | 24 | 2 |
| _0_2 | Vandal | 30 | 30 | 4 |
| _1_2 | Bucky | 30 | 30 | 4 |
| _2_2 | Classic | 45 | 24 | 4 |

Lặp đúng bảng cho Factory, Asylum và Laboratory; capacity lấy từ contract đã khóa, phải đối chiếu lại weapon asset khi gán dữ liệu.

- 72 survival supply: 36 ID hàng `_0-` giữ min party 1; 36 ID hàng `_3-` đổi thành min party 3 sau gate.
- 9 MedicalCase giữ medicineOnly/reward/gate; không bị nhầm với survival medkit.
- `Laboratory_1_0.chooseReward = true`: **giữ lựa chọn thuốc hiện có**, không tước bằng việc gán AM2.
- Whole-pack: sai weapon hoặc không đủ reserve cho cả pack thì reject và cache còn. Không tự đổi sang súng đang cầm.
- Hiện tại **27 ammoWeapon vẫn null**, cả 72 survival min party vẫn 1. Chưa áp dụng thay đổi gameplay data.

## 7. Trình tự tiếp tục và save gate

1. **Hoàn tất download:** đã có 17 original archive; xem receipt/manifest cho đường dẫn thật và checksum. Không cần tải lại.
2. Chốt visual family Lab và từng loại còn thiếu; asset nào thiếu thì tiếp tục ghi Blocked.
3. Chỉ các nhóm đã qua gate mới được import, kiểm tra isolated và thay theo helper hiện có.
4. Giữ gameplay root/IDs; chuyển proxy/body sang model thật; giữ legacy inactive và không để Awake bật lại.
5. Kiểm tra bounds/support/LOS, checkpoint, NavMesh/Tank route, targeted EditMode/PlayMode rồi multiplayer.
6. Chụp before/after thật; hoàn tất per-ID status. Không sử dụng ảnh tác giả làm after.
7. Chỉ save GameScene khi đủ điều kiện và **người dùng xác nhận save gate**.

Theo kiểm tra Editor cuối ngày 29/09, scene dirty; lượt tải 30/09 không query/chỉnh/save scene. Xem [báo cáo continuation](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Replacement-Status-2026-09-29.md) về các lệnh đã chạy nhầm trước đó và giới hạn test. Không Undo hàng loạt, reload bỏ thay đổi hoặc tự save để làm trạng thái trông sạch.

## 8. Current implementation state — 2026-10-01

Sections 1–7 retain the original planning snapshot. Their “not imported”, “not applied”, “pending C2/C3 approval” and K1 hold wording is historical, not current status. Use this section, the updated provenance manifest and the execution report for current decisions.

| Group | Current staged state | Evidence |
|---|---|---|
| 108 supplies + 9 tables | Sourced visuals, IDs/rewards/party gates/weapon mapping and 432 support corners pass in-place assertions | Validation-2026-09-30/Supplies-After.json; Execution-Status.md |
| AsylumFuse | K1 optimized derivative on existing SmallMetalicCase; legacy inactive | Validation-2026-09-30/Corrected-AsylumFuse.png |
| LabTrace | L2-R computer on C3 workbench, not the old homemade desk | Validation-2026-10-01/ControlDesk-05.png |
| LabTransmit | R1 on physically measured Laboratory_2 tabletop at y=-17.354 | Validation-2026-09-30/Corrected-LabTransmit.png |
| ControlDesk | 9/9 replaced with C3 + L2-R; files remain readable | Validation-2026-10-01/Furniture-After.csv |
| LabBench | 7/7 replaced, .92 m tabletop preserves supported devices | Validation-2026-10-01/Furniture-After.csv |
| FumeHood | 2/2 replaced with C2, uniform fit and reachable fronts | Validation-2026-10-01/Furniture-After.csv |
| LabCart + TransferTrolley | 5/5 replaced with approved C1 utility cart | Validation-2026-10-01/CartsCases-After.csv |
| TransitCase | 20/20 replaced with approved RPaciorek hard case | Validation-2026-10-01/CartsCases-After.csv |
| Remaining LabEquipment | 28 unchanged: 17 mapping-approved, 11 awaiting review; user requests one combined replacement batch | Execution-Status.md blocker table |
| OmaxMicroscope | All 4 repaired; 57,457-vertex source imports with failing UV2 generation disabled | EditMode nonempty-mesh regression passed; visual acceptance pending |

## 9. Current continuation — 2026-10-01

Sections 1-7 are historical planning snapshots. Sections 8-9 describe current scene state: **43/71 LabEquipment**, not 18/71:

| Group | Current state | Source / evidence |
|---|---|---|
| LabCart (2) + TransferTrolley (3) | C1 `pCube26`, local checks pass; user approved | Naked Singularity Studio, CC BY 4.0; `CartsCases-After.csv` |
| TransitCase (20) | RPaciorek `EquipmentCase.fbx`, local checks pass; user approved | GitHub BlenderAssets, MIT; `CartsCases-After.csv` |
| ReagentCabinet (9) | User approved; waiting for all-28 batch, not placed | `LAB_Cabinet`, Naked Singularity Studio, CC BY 4.0 |
| ServerRack (7) | User approved; waiting for all-28 batch, not placed | `LAB_Rack`, Spellkaze, CC BY 4.0 |
| LabStool (1) | User approved; waiting for all-28 batch, not placed | `LAB_Stool`, conndavis20, CC BY 4.0 |
| Centrifuge (3) | Open/closed configurations rendered separately; 18,847 tris / 164 renderers each; approval and static batching adaptation pending | `LAB_Centrifuge`, ProgressTH, CC BY 4.0 |
| ColdStorage (5) | KurtSteiner compact open fridge imported/rendered but on fit hold; existing tall dirty fridge alternative | `LAB_Fridge` CC BY 4.0; Asylum `Fridge.prefab` |
| LabSink (2) | Existing sourced sink remains pending family/fit decision | Asylum `Sink_V1.prefab` |
| SpecimenChamber (1) | Dominic Baker candidate rejected for visual/import issues; new 99,794-triangle cryopod imported/rendered, not placed | `LAB_Cryopod`, moneii2706, CC BY 4.0; technical/visual approval pending |

Mapping approval is distinct from final placement acceptance. On 2026-10-01 the user approved the 17 cabinet/rack/stool mappings but requested review of all 28 before replacing them together. The current 28-row ledger is `Validation-2026-10-01/Remaining-Lab-Blocked-Current.csv`; the old 53-row file is retained as historical audit evidence.

Stage is `STAGED_LOCAL_CHECKS_PASS`, **not** final `REPLACED_AND_VALIDATED`. Latest formal Unity Test Runner: **11/11 targeted EditMode tests pass** (9 prop tests, 1 Files test, 1 morgue route test). Earlier direct invocations and repeat-authoring checks are separate evidence. No new PlayMode/independent-peer result is claimed.

C2/C3 approval covers the selected workbench/fume-cupboard family; no automatic approval for unrelated equipment. CC BY-NC restrictions remain. K1 now uses a 5,682-triangle derivative (source 126,296; bounds error approx .018%).

Old `*-Eye-Validated`, `*-Close-Validated` and `*-After-Close` images contained incorrect placements; they are not acceptance proof. Current 18 furniture and 25 cart/case individual screenshots and contact sheets are under `Validation-2026-10-01/`. `FridgeFront-Candidate.png` / `FridgeBack-Candidate.png` used a wrong preview axis; use `Fridge-Native0.png` instead.

[Detailed execution report](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Validation-2026-09-30/Execution-Status.md) records remaining network/visual/save gates. No scene-save command was issued by this assistant.

## 10. Remaining 11: complete visual approval request - 2026-10-01

The user approved the 9 ReagentCabinet + 7 ServerRack + 1 LabStool mappings, but requested review of the other 11 before replacing all 28 together. No additional scene replacement or save was performed for this request.

All images below are actual imported/existing model previews, not final in-scene placement evidence.

| Code | Targets | Proposed source | Preview under Validation-2026-10-01 | Outstanding limitations |
|---|---|---|---|---|
| A | ColdStorage x5 | Existing AsylumFacility/Prefabs/Fridge.prefab | Fridge-Front.png | Tall closed domestic-style fridge; 0.78 x 1.54 x 0.63 m. Original pack author/license not re-established. Visual approval, provenance and placement checks pending. Compact open KurtSteiner fridge is not recommended as a drop-in replacement. |
| B | Centrifuge x3 | LAB_Centrifuge, ProgressTH, CC BY 4.0; open instance_0 | Centrifuge0-Candidate.png | DIY appearance, not a commercial laboratory machine. 18,847 triangles / 164 renderers per configuration; combine existing static source meshes by material before placement. |
| C | LabSink x2 | Existing AsylumFacility/Prefabs/Sink_V1.prefab | Existing-Sink.png | Worn steel utility sink; 1.17 x .89 x .82 m; visibly no faucet in this preview. Narrower than old generated counter. Preserve proportions. Original pack author/license not re-established. |
| D | SpecimenChamber x1 | LAB_Cryopod, moneii2706, CC BY 4.0 | Cryopod-Front.png | 99,794 triangles / 13 meshes; optimize source and verify materials/fit before placement. Preview 1.19 x 2.40 x 1.00 m. Sci-fi pod style; materials currently plain/dark. |

These four choices complete the remaining type list: 17 mapping-approved + 11 pending = 28 unchanged instances. Approval of appearance does not waive license, technical, placement, gameplay or save gates. Existing preview boards may still say PENDING; this dated text and the per-ID ledger supersede those historical labels for the approved 17 only.

## 11. Current final-batch status — 2026-10-02

This section supersedes the pre-batch status in sections 8–10. The user approved all remaining mappings and explicitly confirmed permission to use the existing Asylum pack. After explicit save confirmation, all 28 remaining instances are **saved** in GameScene: 9 ReagentCabinet, 7 ServerRack, 1 LabStool, 5 ColdStorage, 3 Centrifuge, 2 LabSink and 1 SpecimenChamber. Together with the earlier 43 replacements, all 71 LabEquipment instances have replacement visuals; this is not full runtime/visual acceptance.

- Centrifuge: ProgressTH open configuration, combined by source material from 164 to 5 renderers; 18,847 triangles retained.
- SpecimenChamber: moneii2706 cryopod derivative, 99,794 to 29,927 triangles; approximately 0.005% relative bounds error. Original source retained.
- ColdStorage and LabSink: existing Asylum Fridge and Sink_V1, user-confirmed project permission; original license metadata remains unverified.
- Direct validation rechecked on 2026-10-02: all 28 source models, 112 support probes, uniform scales, contained footprints, layers/materials and inactive legacy pass.
- Batch post-write check recorded unchanged root matrices/GlobalObjectIds, 108 supply states and objective/file serialized states. Current Editor is Edit Mode, not compiling, with 8 scene roots and no Console errors. Scene validation reports no errors/warnings; repeated names are informational, not proof of duplicate replacements.
- Final-batch formal EditMode tests after the approved save: **12/12 passed**, no failures/skips. ApprovedPropSceneTests 10/10 (`55f1a934`), CampaignFileSceneTests 1/1 (`71abad3a`), CampaignMorgueGeometryTests 1/1 (`331e8a64`). Readback confirms 71 props, 108 supplies, 8 roots and a clean scene. Saved SHA-256: `8983F11C070E305B746948623F1E7DF0F267AF909021140D23BFED5E778DD47A`. Runtime, full visual and independent-peer acceptance remain pending.

Per-ID status: `Validation-2026-10-01/Remaining-Lab-Blocked-Current.csv` now records `SAVED_EDITMODE_PASS_PENDING_RUNTIME`. Raw test evidence: `Validation-2026-10-01/FinalLab-Saved-EditMode-2026-10-02.json`. Bounds/support/screenshots: `Validation-2026-10-01/FinalLab-After.csv`. Contact sheets: `Final-Lab-1` through `Final-Lab-5`, in 1920x1080 and 1280x720. Some centrifuge/stool views are occluded and are not sufficient for complete visual acceptance.
