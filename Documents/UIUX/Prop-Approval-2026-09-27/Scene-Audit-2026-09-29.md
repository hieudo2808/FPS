# GameScene — kiểm kê đồ cũ tự dựng và lỗi đặt vật phẩm

Ngày kiểm tra: **29/09/2026**. Đã lưu nguyên trạng `Assets/FPS/Scenes/GameScene.unity` theo yêu cầu; sau kiểm kê scene vẫn sạch, ở Edit Mode. **Không thay model, di chuyển, tắt, xóa object hoặc sửa gameplay trong lượt này.**

## 1. Phạm vi và kết luận

Đã duyệt đủ **8 scene root, 14.960 GameObject, 7.480 renderer**, bao gồm object inactive. Có **1.008 nhóm đường dẫn nguồn mesh** trong lượt quét. UI Canvas, manager, trigger và điểm spawn không có mesh prop được phân biệt với đồ vật nhìn thấy.

Kết luận: việc thay prop **chưa hoàn tất**. Không chỉ còn ba console: vật phẩm y tế, bàn tiếp tế và nhiều thiết bị Lab vẫn là geometry tự dựng. Có lỗi đặt vật phẩm xuyên suốt cả ba chapter.

- **Xác nhận trực tiếp:** mesh primitive Unity, trạng thái renderer/GameObject, vị trí, nguồn prefab/mesh và component trong Editor.
- **Xác nhận từ hồ sơ project:** 13 loại `LabEquipment` được tạo cho project, không phải model bên ngoài; riêng kính hiển vi có nguồn bên ngoài rõ ràng.
- **Không suy diễn:** file `.fbx` không chứng minh đó là asset tải ngoài; file `.asset` cũng không tự chứng minh là model AI dựng. Mesh được chỉnh/cắt từ map có sẵn được tách riêng bên dưới.

Số lượng trong báo cáo là **instance trong scene đã lưu**, không phải số loại item, số lượng trong túi hoặc số prefab toàn project. “Còn bật” nghĩa là `activeInHierarchy && Renderer.enabled`; không bảo đảm đang nằm trong camera hay đang khả dụng ở mọi phase runtime.

### Dữ liệu đối chiếu đầy đủ

- [Toàn bộ 7.480 renderer: path, mesh, prefab, material, trạng thái, tọa độ, GlobalObjectId](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Scene-Audit-2026-09-29.csv).
- [Toàn bộ 108 CampaignSupply: stable ID, loại, party size, mặt đỡ và độ lệch khỏi bàn](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Supply-Support-Audit-2026-09-29.csv).

Một số hierarchy path trùng nhau vì object cùng tên; dùng `globalObjectId` hoặc `supplyId` trong CSV để phân biệt, không chọn/xóa theo tên đơn thuần.

## 2. Đồ gameplay tự dựng vẫn còn bật

| Nhóm | Số lượng | Hiện trạng / bằng chứng |
|---|---:|---|
| Console `AsylumFuse` | 1 bộ, 2 khối | `EquipmentPedestal` + `ControlFace`, cả hai còn bật. Chưa có model cầu chì thay thế ở điểm này. |
| Console `LabTrace` | 1 bộ, 2 khối | Bệ và mặt điều khiển ghép cube còn đứng trước workstation khu B. |
| Console `LabTransmit` | 1 bộ, 2 khối | Bệ và mặt điều khiển ghép cube còn ở khu truyền dữ liệu. |
| Medkit dùng trực tiếp | **18** | Mỗi túi là 8 renderer primitive: `CanvasPouch`, `Flap`, 2 `Strap`, `Patch`, `CrossV`, `CrossH`, `Handle`. 6 túi/chapter. Chưa thay M1. |
| Antidote dùng trực tiếp | **18** | Mỗi lọ là 4 renderer primitive: `Suppressant`, `Seal`, `MedicalLabel`, `Identifier`. 6 lọ/chapter. Chưa thay M2. |
| Incendiary grenade | **18** | Model lai: mesh grenade cũ nhập từ thư mục người dùng **cộng** primitive `ThermiteCore`. Không phải toàn bộ model mới realistic đã duyệt; chưa thay G2. |
| `MedicalCase` tiếp tế | **9** | Hộp y tế cũ, mỗi bộ gồm `MedicalCase`, `CaseTrim`, `MedicalMark`; 3 bộ/chapter. Khác với 18 túi medkit tiêu hao ở trên. Chưa thay S1. |
| `SupplyBench_*` | **9** | Bàn tiếp tế tự ghép `Worktop`, `LowerShelf`, 4 chân và 4 bánh: tổng 90 renderer primitive. Không phải `LabBench.fbx`. 3 bàn/chapter. |
| Chi tiết `SupplyWorktop` của case cũ | **27** | Vẫn bật bên dưới `FiniteSupply/CaseVisual` mặc dù thân/viền case cũ đã tắt. Đây là khối nhỏ khoảng **12 × 0,4 × 6 cm**, không phải một chiếc bàn; mesh 24 vertex/12 triangle. Cần kiểm tra/xử lý phần dư cùng model case mới. |

`MedicalCase_MedicalCase.asset` có 96 vertex/48 triangle; `EquipmentCase_EquipmentCase.asset` cũ có 48 vertex/24 triangle. Chúng là các mesh hộp đơn giản nội bộ, không được tính là model realistic mới đã duyệt.

### Ba console còn hoạt động — vị trí chính xác

| Objective | Hierarchy root | Tọa độ `ControlFace` (world) |
|---|---|---|
| AsylumFuse | `CampaignWorld/Asylum/CampaignObjectives/AsylumFuse` | `(152.700, 1.300, 244.205)` |
| LabTrace | `CampaignWorld/Laboratory/CampaignObjectives/LabTrace` | `(179.190, -16.750, 245.805)` |
| LabTransmit | `CampaignWorld/Laboratory/CampaignObjectives/LabTransmit` | `(168.190, -16.750, 304.805)` |

Ảnh hiện trạng, chưa sửa:

![LabTrace: bệ cũ và dãy ControlDesk tự dựng](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Audit-LabTrace-Unchanged-2026-09-29.png)

![AsylumFuse: hai khối vẫn thay cho vật phẩm nhiệm vụ](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Audit-AsylumFuse-Reader-Unchanged-2026-09-29.png)

![LabTransmit: console cũ còn hoạt động](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Audit-LabTransmit-Reader-Unchanged-2026-09-29.png)

## 3. Toàn bộ bộ thiết bị Lab tự dựng: 13 loại, 71 instance

Nguồn xác nhận: [THIRD_PARTY_NOTICES.md — Map-owned equipment](E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/ExperimentFacility/THIRD_PARTY_NOTICES.md:16) ghi các model khác kính hiển vi trong `Models/LabEquipment` được tạo cho project, bằng script Blender `Documents/ExperimentFacility/Review-02/build_lab_equipment.py`.

**Giới hạn bằng chứng:** script được nhắc tới hiện không tồn tại tại đường dẫn đó trong workspace. Vì vậy xác nhận nguồn “project-authored” dựa trên hồ sơ hiện hữu, không giả vờ đã đọc hoặc chạy lại script. Không xác định tác giả cụ thể chỉ từ file FBX.

Tất cả 71 instance bên dưới hiện còn bật:

| Model | Hiểu là | Số instance | Khu vực trong `Props/LabFitout_02` |
|---|---|---:|---|
| `ControlDesk` | Bàn máy tính, màn hình và bàn phím | **9** | A: 1, B: 6, D: 1, F: 1 |
| `LabBench` | Bàn thí nghiệm | **7** | C, E |
| `ReagentCabinet` | Tủ hóa chất/vật tư | **9** | A, C, F, SupplyStore |
| `ColdStorage` | Tủ lạnh bảo quản | **5** | C, E |
| `ServerRack` | Tủ máy chủ | **7** | B, D, FinalDetails |
| `Centrifuge` | Máy ly tâm | **3** | C, E |
| `FumeHood` | Tủ hút | **2** | C, E |
| `LabCart` | Xe đẩy phòng lab | **2** | C, E |
| `LabSink` | Bồn rửa phòng lab | **2** | A, C |
| `LabStool` | Ghế phòng lab | **1** | C |
| `SpecimenChamber` | Buồng mẫu | **1** | E |
| `TransferTrolley` | Xe chuyển hàng | **3** | F, SupplyStore |
| `TransitCase` | Thùng vận chuyển | **20** | F, FinalDetails |
| **Tổng** | **13 loại** | **71** | |

Mesh nằm trong `Assets/FPS/Features/World/Content/ExperimentFacility/Models/LabEquipment/`. Full path, tọa độ và prefab của từng instance có trong CSV.

**Đính chính báo cáo trước:** “dùng bàn/thiết bị có sẵn” chỉ có nghĩa chúng đã nằm trong project, không có nghĩa là asset bên ngoài. Các bàn Lab dùng cho index/archive vẫn thuộc nhóm project-authored này. `ControlDesk` và `TransitCase` không còn được ghi chung là “chưa tìm thấy provenance”: hồ sơ trên đã giải đáp nguồn nội bộ của chúng.

**Ngoại lệ không gộp nhầm:** 4 `OmaxMicroscope` là model của **3Donimus**, nguồn Poly Pizza, CC BY 3.0, có hồ sơ chuyển đổi GLB → FBX. Không tính chúng vào 71 thiết bị tự dựng.

## 4. Lỗi vật phẩm đứng trên không — không chỉ ở Lab

Đã đo cả **72 survival supply**, gồm 18 frag, 18 incendiary, 18 medkit và 18 antidote. Đây là nhóm riêng, không phải 27 case đạn hoặc 9 medical case.

| Chapter | Survival supply | Tâm mesh nằm ngoài mặt bàn tiếp tế gần nhất | Khoảng từ đáy mesh tới collider ngoài item bên dưới |
|---|---:|---:|---:|
| Factory | 24 | **24/24** | 0,150–0,740 m |
| Asylum | 24 | **24/24** | 0,315–0,789 m |
| Laboratory | 24 | **24/24** | 0,710–0,740 m |
| **Tổng** | **72** | **72/72** | |

Phương pháp: hợp bounds các renderer đang bật của item, bỏ qua collider thuộc bất kỳ `CampaignSupply` nào khi raycast xuống; so tâm XZ của mesh với bounds `Worktop` của bàn gần nhất. Đây là kiểm tra support ở tâm và footprint bàn, không phải kiểm định đầy đủ mọi góc/hitbox/đường đi của cả map.

Ví dụ cụm `Laboratory_0`:

- Mặt bàn tiếp tế: Z **245.625–246.145**, Y mặt trên **−17.300**.
- Hàng đồ tiêu hao: Z khoảng **245.085**, tức nằm trước mép bàn tối thiểu **0,54 m**, không nằm trên bàn.
- Đáy grenade/medkit/lọ ở Y khoảng **−17.290 đến −17.260**; sàn Y **−18.000**.
- Hàng thứ hai còn tràn khỏi chiều dài bàn. Chỉnh riêng trục Y không giải quyết được bố cục.

![Hiện trạng: vật phẩm cũ treo phía trước và ngoài bàn](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Audit-Lab-Supplies-Unchanged-2026-09-29.png)

**Mâu thuẫn dữ liệu cần ghi rõ:** tài liệu survival cũ nói có hàng dự phòng cho 3–4 người. Tuy nhiên trong scene hiện tại, **cả 72 survival supply đều có `minimumPartySize = 1`**. Không thể giải thích ảnh này đơn thuần là “Editor hiển thị cả hàng dự phòng”. Chưa sửa balance hoặc availability trong lượt audit.

27 case ammo và 9 medical case có ray support ở tâm với gap xấp xỉ 0 trong phép đo này; không gộp chúng vào 72 vật phẩm lơ lửng. Điều đó cũng không chứng minh toàn bộ footprint/collider của từng case đã đạt visual QA.

## 5. Geometry tự ghép khác vẫn hiện trong map

Phần này được liệt kê vì yêu cầu rà **toàn scene**, nhưng không tự coi mọi mặt sàn, dấu sơn hoặc lan can là “item phải thay”. Một số cấu kiện có vai trò collision, NavMesh, che spawn hoặc nối đường; xóa chúng chỉ vì là primitive có thể làm hỏng campaign.

### Primitive trực tiếp, ngoài item/console/bàn tiếp tế

| Khu | Nhóm | Số renderer còn bật |
|---|---|---:|
| Factory | Móng/nền `*_Foundation` ghép khối | 44 |
| Factory | Cột và thanh lan can `Post_*`, `Rail_*` | 36 |
| Factory | `CentralStair_*` và `SouthAccessStep_*` | 12 |
| Factory | Vạch đáp `CenterBar`, `CrossBar`, `H_*` | 7 |
| Asylum | `TankArena_ReceivingPartition`, `TankArena_PartitionTiles` | 2 |
| Lab geometry | `CeilingFascia` | 324 |
| Lab geometry | `PhysicalGate` | 2 |
| Lab lighting | 121 bộ `Housing` + `Diffuser` | 242 |
| Lab props | Vách/kính/khung, cấu kiện pit và guard, plate/biển phòng, `SupportedService`, đèn chỉ thị | 112 |
| Lab transit | Hai cabin thang và hai connector | 20 |

Lab props gồm: 2 `LabPartition`, 2 `ObservationPane`; các `GlazingPost/Head/Sill/Dado/UpperRail`, `PartitionEnd/Clerestory/Soffit`; `PitBase`, 2 `PitWall`, 2 `PitEnd`, 4 `SafetyRail`; 47 `SupportedService`, 10 `Footplate`; 10 `WallMountedPlate`, 10 `RoomCodeBand`; `Mount` và `AmberLens`. Có thêm 10 `PrintedLabel` là mesh chữ TMP, **không phải prop mô hình hóa**.

Hai cabin nằm tại `CampaignWorld/Laboratory/LabArrivalCabin` và `LabExtractionCabin`, gồm sàn, tường, mái, cánh/khung cửa, đèn. Các connector là `ArrivalConnector`, `ExtractionConnector`.

Toàn scene có **1.164 renderer dùng mesh primitive built-in**, **1.131 còn bật**. Đây là số renderer, không phải 1.164 vật phẩm độc lập; bao gồm các nhóm item, kiến trúc, đèn và cấu kiện ở trên.

### Mesh nội bộ hoặc đã chỉnh từ asset — không gộp với model tự dựng mới

| Nhóm nguồn mesh | Tổng renderer / còn bật | Phân loại thận trọng |
|---|---:|---|
| `AsylumFacility/Meshes/ExteriorFaces` | 429 / 425 | Bản mesh xử lý bề mặt của nhiều asset Asylum. Không kết luận 429 đồ nội thất đều tự dựng chỉ từ đuôi `.asset`. |
| `AsylumFacility/Meshes/Architecture` | 18 / 18 | Các joint/cap và cabin đã chỉnh; cần giữ riêng với prop gameplay. |
| `World/Content/Meshes/Concrete` | 210 / 139 | Geometry concrete nội bộ; chưa truy nguyên từng mesh để kết luận tác giả. |
| `Roads/FactoryYard/Meshes/Ground` | 90 / 90 | Paving, apron, kerb, ramp, nền sân. |
| `Buildings/Industrial/Meshes/Stairs` | 62 / 62 | Các bậc, stringer và landing của hai cụm cầu thang. |
| `Roads/Road_sets/FactoryRoads/Meshes` | 11 / 11 | Mesh đường Factory. |
| `Buildings/Industrial/ServiceShed/Meshes` | 8 / 6 | Mesh service shed. |
| `Oil_tanks/Meshes/Pads` | 5 / 5 | Đế bồn. |
| `Campaign/MorgueArena` | 5 / 5 | Mesh morgue/hall được chỉnh cho arena; không phải item cũ. |
| `Roads/FactoryYard/Meshes` (ngoài Ground) | 4 / 4 | Mesh phụ trợ sân. |
| `Fences/Meshes` | 3 / 3 | Mesh hàng rào đã xử lý. |

`Campaign/Meshes` có 127 renderer: 108 thuộc các chi tiết case cũ/medical case/chi tiết dư đã nêu; 19 thuộc `CampaignDoorCut_*` (17 còn bật) cho nền, fence, floor, wall, fascia. Đây là nguồn geometry đã sửa/cắt, không tự động là 19 prop mới phải thay.

## 6. Đồ cũ còn lưu nhưng không còn hiện

| Nhóm | Trạng thái xác nhận |
|---|---|
| 9 bộ pedestal/control face đã thay | **18 GameObject inactive**: AsylumAccess, AsylumInstall, AsylumPower, AsylumPatient, AsylumTransfer, LabIndex, LabPower, LabArchive, LabCase. |
| 27 thân EquipmentCase và 27 CaseTrim cũ | **54 renderer thuộc object inactive**; thân mới là RPaciorek. 27 `SupplyWorktop` nhỏ vẫn bật, đã tách thành tồn đọng ở mục 2. |
| Trực thăng placeholder | 6 khối inactive: Body, TailBoom, MainRotor, TailRotor, Skid_Left, Skid_Right. Không tính là trực thăng đang hiển thị. |
| 4 `ContainmentBarrier` | GameObject vẫn active nhưng **renderer tắt**; không đồng nghĩa collider/logic đã tắt. |
| 6 `*Backstop` ở Factory | GameObject active nhưng **renderer tắt**; là boundary, không phải đồ nhìn thấy. NorthBackstop dùng mesh đã cắt, năm cái còn lại dùng primitive. |
| `AuthoredCounterSample` cũ ở Factory | Inactive. Không gộp với evidence case mới; asset cũ là visual được tái sử dụng, không tự động gọi là primitive AI tạo. |

Không xóa các object này trong lượt audit.

## 7. Đồ chưa thay nhưng không phải hoàn toàn tự dựng

| Nhóm | Số lượng / hiện trạng |
|---|---|
| Frag grenade | 18 pickup scene dùng mesh `HE_Grenade_3PV` từ thư mục người dùng, sao chép thành `FragGrenade.asset`. Là model cũ chưa đổi G1, **không phải quả grenade tự vẽ từ cube**. |
| Incendiary | 18 instance dùng cùng mesh grenade nguồn cộng `ThermiteCore` tự dựng; đã tính ở mục 2, không cộng lần hai. |
| Radio Factory | `OBJ_ExtractionRadio/Visual` vẫn dùng **Electric_box_v2.FBX**. Asset thật nhưng dùng sai biểu tượng thiết bị so với radio đã duyệt R1. |
| Omax microscope | 4 instance có nguồn 3Donimus/Poly Pizza, không thuộc model tự dựng nội bộ. |
| Case mới | 27 equipment case bạc và 2 evidence case đen dùng RPaciorek. Việc đã đổi thân case không đồng nghĩa mọi chi tiết cũ/đồ đặt cạnh nó đã xử lý xong. |
| Máy tính CRT, Paper, tủ điện/máy phát nguồn sẵn | Không đưa vào danh sách tự dựng chỉ vì chúng được agent đặt vào scene. |

Hai loại grenade dùng chung nguồn mesh nên CSV đếm **36 renderer `FragGrenade.asset`**, không có nghĩa có 36 frag pickup.

L2-R, G1/G2, M1/M2, AM1–AM5, K2 và R1 vẫn chưa được xác nhận tích hợp model đã duyệt. K1 còn technical hold. Không có model mới nào được tải/import trong lượt kiểm kê này.

## 8. Cách xử lý đề xuất — chưa thực hiện

1. Sửa layout của cả 72 survival supply dựa trên mặt đỡ thật, đủ footprint; không chỉ hạ Y và không thêm bệ cube để đỡ đồ.
2. Thay ba console còn lại bằng đúng prop nhiệm vụ đã chọn; giữ ID, LOS, range và checkpoint.
3. Thay medkit/antidote/incendiary và 9 medical case; rà cả world, tay cầm, projectile khi tới lượt tích hợp tương ứng.
4. Rà 27 chi tiết `SupplyWorktop` còn sót sau thay equipment case. Không thay cả thùng mới chỉ vì còn một chi tiết cũ.
5. Đưa **13 loại/71 instance LabEquipment** thành nhóm duyệt riêng rõ nguồn tự dựng; ưu tiên ControlDesk và các đồ nhìn gần. Việc chúng nằm trong project không phải lý do coi là đã đạt yêu cầu asset realistic.
6. Kiến trúc/lighting/cabin/arena là nhóm riêng: chỉ đổi sau khi xác định phạm vi, tránh làm mất collision, đường đi hoặc vùng che spawn.

## 9. Giới hạn và kiểm chứng cuối lượt

- Inventory mesh bao phủ toàn scene đã lưu; phân loại nguồn dựa trên Editor và hồ sơ tìm được, không tuyên bố biết tác giả của mọi file trong project.
- Ảnh là offscreen render thực của scene chưa sửa, không phải ảnh proposal hay ảnh sau-fix. Không thay ánh sáng/màu để che lỗi.
- Không chạy Play Mode, không sửa C#, không dùng kết quả test cũ để coi các tồn đọng này là đạt.
- Prefab chỉ được spawn khi chạy game (đồ trên tay, projectile, enemy...) không được cộng vào số instance của scene Edit Mode; chúng cần audit phụ thuộc riêng nếu yêu cầu mở rộng.
- Sau save và các truy vấn/chụp ảnh: `GameScene.isDirty = false`, Edit Mode. Không sửa/xóa prop nào.
