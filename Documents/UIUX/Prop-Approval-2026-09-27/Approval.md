# Outbreak Protocol — Bảng duyệt thiết bị và vật phẩm

Ngày kiểm tra: 27/09/2026. **Cập nhật: người dùng đồng ý phần còn lại của báo cáo, yêu cầu thay L2 bằng asset máy tính thật và bổ sung bom/hộp đạn.**

Phương án **giữ L2 cũ bị loại**. Các asset bổ sung L2-R, G1/G2, AM1–AM5 và M1/M2 nằm trong [phụ lục duyệt bổ sung](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Combat-Items-Addendum.md). Các ghi chú “chưa duyệt” bên dưới mô tả đợt đề xuất ban đầu, không phủ nhận phản hồi mới của người dùng. K1 vẫn là technical hold; chấp thuận tổng thể không làm model nặng trở thành đạt chuẩn. A2 có hai phương án, không triển khai cả hai.

Chưa thay hoặc xóa prop, chưa import model ngoài, chưa chỉnh gameplay và chưa save `GameScene`. Ảnh nhóm 01 là ảnh chụp Unity hiện tại; ảnh nhóm 02 là preview gốc của tác giả, không phải hình đã render trong game. Chỉ tải preview và metadata để làm bảng duyệt. Không có ảnh AI hoặc hình “sau khi sửa” giả lập.

## 1. Hướng đề xuất

- Tương tác trực tiếp với máy tính, máy phát, tủ điện và đồ nội thất có sẵn. Không dựng một bộ `EquipmentPedestal` + `ControlFace` cho mỗi objective.
- Đồ mang theo mới cần model riêng: cầu chì, thẻ B2, hai evidence case. Vật phẩm tiếp tế dùng túi/hộp dễ phân biệt với đồ nhiệm vụ.
- Giữ objective ID, thứ tự puzzle, điều kiện server và checkpoint. Chỉ thay cách biểu diễn và điểm tương tác trong đúng khu vực.
- `AsylumInstall` và `AsylumPower` dùng **cùng một tủ điện**, lần lượt lắp cầu chì và khôi phục điện; không gộp hai objective thành một.
- Mọi tọa độ đặt mới, collider và góc tương tác trong tài liệu này là đề xuất chờ thử nghiệm sau duyệt, không phải trạng thái đã triển khai.

## 2. Nhóm tái sử dụng trong project

![Ảnh chụp Unity — sáu đề xuất tái sử dụng](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/01-Existing-Scene-Assets.png)

| Mã duyệt | Asset / vị trí | Dùng cho | Đề xuất cụ thể |
|---|---|---|---|
| **A1** | Máy tính CRT và bàn phím trong GuardRoom | `AsylumAccess` | Ngắm màn hình để mở desk log. Bỏ hai cube thừa sau khi chuyển interaction; giữ nguyên bàn và máy tính. |
| **A2-A** | `Electric_box_v2` đang dùng ở Factory | `AsylumInstall`, `AsylumPower` | Tái dùng prefab tủ điện này ở sát tường khu máy basement. Ảnh là mẫu đang ở Factory, **không phải** đã được đặt sang Asylum. Ít asset mới nhất. |
| **A3** | `ExamRoom/TableOffice` | `AsylumPatient` | Đặt hồ sơ P046 bằng `Paper.prefab` lên bàn khám có sẵn. Không thêm console hoặc body bệnh nhân. |
| **A4** | `Basement/Morgue/TableWhite` | `AsylumTransfer` | Giấy chuyển viện và thẻ B2 trên bàn trắng. Record/puzzle vẫn xác nhận mâu thuẫn thời gian; thẻ chỉ được cấp đúng khi objective commit. |
| **L1** | Máy phát xanh + tủ điện trong `D_POWER SYSTEMS` | `LabPower` | Dùng trực tiếp cụm máy người dùng chỉ trong ảnh. Tương tác ở phần điều khiển; không thêm một máy giả phía trước. |
| **L2 — BỎ PHƯƠNG ÁN CŨ** | Workstation `ControlDesk` hiện có | `LabTrace`, `LabTransmit` | Người dùng yêu cầu thay bằng asset máy tính thật. Xem **L2-R** trong phụ lục. Ảnh nhóm 01 là hiện trạng để đối chiếu, không còn là phương án giữ nguyên máy tính. |
| **L3** | Tái dùng model PC từ A1, đặt trên `LabBench` có sẵn ở E | `LabArchive` | Chỉ bổ sung máy tính từ prefab có sẵn vào khu E Containment, giữ archive gần case và đúng tuyến hiện tại. Không chuyển archive về B chỉ vì B có máy tính. Chưa chụp được bố trí sau thay vì chưa triển khai. |
| **F1** | `Paper.prefab` hiện có | `LabIndex` ở C; hồ sơ giấy/optional Files | Index là giấy trên bàn C Research. Các hồ sơ giấy khác tiếp tục dùng paper asset; nội dung dài đọc trong panel riêng. Không biến 21 File thành 21 máy. |

**Khuyến nghị A2:** A2-A nếu ưu tiên đồng nhất đồ hiện có. A2-B ở nhóm dưới nếu muốn nhìn rõ các đầu cầu chì; chỉ chọn **một**, không đặt cả hai. Chưa tìm thấy hồ sơ chứng minh nguồn gốc bên ngoài của `ControlDesk.fbx`; phương án giữ máy tính L2 đã được bỏ theo phản hồi người dùng. Chỉ giữ bàn nếu hình dáng và collider phù hợp.

### Asset path đã xác định

Các đường dẫn dưới đây là asset hiện có, không phải đường dẫn import dự kiến:

| Mã | Asset path |
|---|---|
| A1 / L3 | `Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/PC_Monitor.prefab` |
| A2-A | `Assets/FPS/Features/World/Content/Other_props/Electric_box/Electric_box_v2/Electric_box_v2.prefab` |
| L1 — generator | `Assets/FPS/Features/World/Content/Other_props/Generators/Generator_v1/Generator_v1.prefab` |
| L1 — cabinet | `Assets/FPS/Features/World/Content/Other_props/Electric_box/Electric_box_v3/Electric_box_v3.prefab` |
| L2 | `Assets/FPS/Features/World/Content/ExperimentFacility/Prefabs/LabEquipment/ControlDesk.prefab` |
| F1 | `Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/Paper.prefab` |

A3/A4 là đồ nội thất đang nằm trong scene, không cần tạo thêm bàn:

- `CampaignWorld/Asylum/Asylum/2ndFloor/ExamRoom/TableOffice`
- `CampaignWorld/Asylum/Asylum/Basement/Morgue/TableWhite`

Tái sử dụng asset nội bộ không đồng nghĩa đã xác minh lại license gốc. Giữ hồ sơ bản quyền/provenance hiện có; không kết luận đồ bên trong project đều do AI tự dựng hoặc đều là asset bên thứ ba.

## 3. Model ngoài đề xuất

![Preview gốc của tác giả — model ngoài](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/02-External-Asset-Proposals.png)

| Mã duyệt | Model / tác giả | Dùng cho | Vì sao chọn / điều kiện |
|---|---|---|---|
| **K3** | [Hard Cases — RPaciorek](https://sketchfab.com/3d-models/hard-cases-assets-lib-ce859ba7507148588262207255974aa4) | Factory Evidence Case và Lab Evidence Case | Chọn hộp đen có tay xách/chốt khóa; khác nhãn T9-17 và E-02. Không dùng thùng gỗ hoặc biểu tượng lọ mẫu để giả làm case. |
| **S2** | Cùng bộ Hard Cases, chọn **hộp kim loại bạc** | 27 `EquipmentCase` tiếp tế hiện có | Hình dáng/màu/chữ khác evidence case; không chỉ phân biệt bằng màu. Chỉ thay visual, không đổi loại vật phẩm hay thêm loadout. |
| **S1** | [Medical Kit Bag — yronthal](https://sketchfab.com/3d-models/medical-kit-bag-abc2f95bce4c4e69bfc0d8742d37e78b) | 9 `MedicalCase` hiện có | Túi cứu thương có vải, dây đai, khóa; dễ nhận ra hơn hộp khối. Nếu duyệt, đổi biểu tượng chữ thập đỏ/nhãn sang chữ `MEDICAL` trung tính; license model không tự cấp quyền dùng mọi biểu tượng trên texture. Preview ở đây vẫn nguyên bản. |
| **K2** | [Keycard — nuFF3](https://sketchfab.com/3d-models/keycard-a0033bf027364417a7777d98d03980e1) | B2 Access Card nhận ở `AsylumTransfer`, dùng ở `AsylumLift` | Thẻ vật lý đơn giản, texture trầy xước, 38 tam giác. Sau duyệt có thể đổi nhãn thành B2, không tạo hình thẻ bằng cube. Không cần thêm scanner nếu panel thang hiện tại đủ rõ. |
| **R1** | [Military Radio — curichenkow](https://sketchfab.com/3d-models/military-radio-f5c8f0420f054b32a2e977fbcd248d84) | `OBJ_ExtractionRadio` tại Factory | Radio có handset, nút và màn hình. Thay visual `Electric_box_v2` đang được dùng cho radio; giữ nguyên logic gọi tuyến thoát. Tác giả mô tả thiết bị quân sự Nga: chỉ đề xuất tái dùng hình dáng thiết bị, không đổi BRT thành tổ chức Nga; rà texture/nhãn khi import. |
| **A2-B** | [Vintage Fuse Box — Alex Filip](https://sketchfab.com/3d-models/freebie-game-art-vintage-fuse-box-4109ae3e180d4ed8b3427fe6a97d58b6) | Phương án thay **A2-A** cho Install/Power | Tủ mở thấy các đầu cầu chì, dễ hiểu thao tác hơn tủ kín. Gắn sát tường, không thêm bàn/bệ. Chưa xác minh cầu chì có mesh tách rời, không hứa tách được để dùng làm pickup. |
| **K1 — CHƯA CHỐT** | [Fuse 3d model — AliA Animations](https://sketchfab.com/3d-models/fuse-3d-model-d2ed4a3206c1497ba9634adff36d24a0) | Service Fuse ở `AsylumFuse` | Hình dáng cầu chì sứ phù hợp để tham khảo nhưng **126.296 tam giác**, quá tốn cho một pickup nhỏ. Không khuyến nghị import nguyên trạng. Nếu thích hình này, chỉ duyệt hướng hình dáng; cần model nhẹ hơn hoặc bản tối ưu được kiểm tra trước. |

### License, provenance và trạng thái kỹ thuật

API Sketchfab tại thời điểm kiểm tra công bố tất cả model trong bảng trên là downloadable và **[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/)**. Phải credit tên tác giả, tên model, URL nguồn, license và ghi nhận chỉnh sửa. Đây là kiểm tra theo khai báo nguồn, không phải bảo đảm độc lập về toàn bộ quyền của uploader.

| Model | Số tam giác API công bố | Ghi chú import sau duyệt |
|---|---:|---|
| Hard Cases | 8.210 cho bộ được liệt kê | Chọn đúng mesh cần dùng, không đưa toàn bộ bộ case vào mỗi pickup. Chưa xác minh vật liệu thực tế trong render pipeline của project. |
| Medical Kit Bag | 9.983 | Kiểm tra texture, scale, collider đơn giản; dùng chung mesh/material giữa các instance. |
| Keycard | 38 | Kiểm tra hai mặt, pivot và độ rõ khi nằm trên bàn. |
| Military Radio | 28.807 | Chấp nhận làm ứng viên một set-piece, chưa đo hiệu năng hay xác minh số material. |
| Vintage Fuse Box | 3.950 | Tác giả ghi PBR 4K, cần giữ imported normals/tangents. Cân nhắc giảm texture cho kích thước hiển thị thực tế. |
| Fuse | 126.296 | **Technical hold**; không đánh dấu game-ready chỉ dựa vào tên/mô tả. |

Metadata có timestamp, nguồn preview và license nằm trong thư mục `Previews/`, cạnh ảnh tác giả. Chưa tải file model, chưa kiểm tra FBX/GLTF, UV, material tương thích project, collider hoặc pivot. Vì vậy **duyệt ở đây là duyệt lựa chọn visual**, không phải kết luận đã đạt kiểm tra tích hợp.

Đường dẫn import dự kiến sau duyệt: `Assets/FPS/Features/World/Content/Campaign/ThirdParty/<Author>/<Asset>/`. Chưa tạo thư mục runtime này. Không copy nguyên pack/script không liên quan.

## 4. Bao phủ đủ 12 bộ pedestal/control face

Mỗi hàng hiện có một cặp `EquipmentPedestal` / `ControlFace`, tổng cộng 24 cube. Chỉ loại bỏ visual/collider thừa sau khi chuyển và kiểm tra interaction; không xóa mù root chứa state hoặc clue.

| Objective hiện tại | Mã đề xuất | Sau duyệt người chơi tương tác với |
|---|---|---|
| AsylumAccess | A1 | Máy tính phòng bảo vệ |
| AsylumFuse | K1 — chờ | Cầu chì thật nằm trên mặt bàn/kệ, không phải console |
| AsylumInstall | A2-A **hoặc** A2-B | Tủ điện nhận cầu chì |
| AsylumPower | Cùng lựa chọn A2 | Chính tủ điện đó, bước cấp điện sau khi lắp |
| AsylumPatient | A3 + F1 | Hồ sơ bệnh nhân trên bàn khám |
| AsylumTransfer | A4 + F1 + K2 | Hồ sơ nhà xác; thẻ được cấp đúng sau xác nhận |
| LabTrace | L2 | Workstation B Operations |
| LabIndex | F1 | Archive index trên bàn khu C Research |
| LabPower | L1 | Máy phát/tủ điện D Power Systems |
| LabArchive | L3 | PC prefab có sẵn đặt lên bàn E Containment |
| LabCase | K3 | Evidence case E-02, không thêm pedestal |
| LabTransmit | L2 | Workstation F Transfer Bay |

### Không cần thay thêm một cách máy móc

- Factory Utilities breakers/generator/security override đã dùng model `Electric_box_v1/v2/v3` và `Generator_v1`: giữ lại, không thay bằng bộ mới chỉ cho đồng loạt.
- Factory manifest: giữ giấy trên mặt bàn/thùng có sẵn. Factory sample hiện dùng thùng gỗ: đổi sang K3.
- `AsylumLift/OriginalStyleLiftControl` là asset tủ điện, không phải một cặp cube tương tự. Giữ trước; chưa đề xuất bắt buộc thêm máy quét thẻ phát sáng.
- Hồ sơ: giữ paper asset đã có. Tài liệu số đọc trên PC; transcript vẫn vào Files, không cần đặt recorder mới cho mọi transcript.
- `TransitCase`/`ControlDesk` có FBX trong project nhưng chưa thấy provenance đủ kết luận ai tạo. Không dùng tên “asset có sẵn” để khẳng định chất lượng hay bản quyền đã xác minh.
- Hai partition khu morgue thuộc geometry/encounter Tank, không nằm trong đợt duyệt item này. Không tự xóa vì có thể ảnh hưởng đường đi, LOS và NavMesh.

## 5. Folder người dùng cung cấp

![Đối chiếu icon từ folder cung cấp](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/03-Supplied-Icons-Review.png)

Đã kiểm tra các PNG liên quan trong `E:/ProjectSettings/Assets/Resources/inventory/icons`. Đây là **icon 2D**, không tự chứng minh có model 3D tương ứng sử dụng được:

- **UI1 / BlueCard:** có thể dùng cho icon thẻ. Khuyến nghị cuối cùng là render thumbnail từ model K2 sau khi được duyệt để vật trên bàn và trong Inventory khớp nhau.
- **BatteryPart1:** tên nguồn là bộ phận pin; hình trụ có thể gợi nhiều loại vật. Không khẳng định đây là model cầu chì. Nó không khớp ứng viên cầu chì sứ K1; cần rà lại mapping cũ.
- **VirusSample:** minh họa ống mẫu, không phải vali bằng chứng.
- **Notebook:** hình laptop, không phải sổ giấy.

Folder cũng có tên asset liên quan card/tablet, nhưng chưa có preview 3D và kiểm tra dependency đủ tin cậy để đưa vào danh sách “sẵn sàng chọn”. Không kết luận cả folder chỉ có icon hoặc không có model phù hợp. Khi lựa chọn model được chốt, thumbnail có thể render từ chính model, không tự vẽ icon mới.

Các mapping icon cũ trong `Asset-Provenance.md` và `Gameplay-UI-Asset-Matrix.md` **chưa được sửa**. Tài liệu này chỉ nêu vấn đề và đề xuất; không âm thầm coi thay đổi đã được duyệt.

## 6. Cách duyệt và giới hạn triển khai

Có thể trả lời theo mã, ví dụ:

> A1, A3, A4, L1, L2, L3, F1: duyệt. A2 chọn B. K2/K3/R1: duyệt. S1 đổi mẫu. S2 giữ. K1 tìm tiếp.

Không trả lời một mục không được tính là đồng ý mục đó. K1 dù có trong ảnh vẫn chưa đủ điều kiện tích hợp. A2-A và A2-B là hai lựa chọn loại trừ nhau.

Sau duyệt: chỉ import/thay các mục đã chọn; giữ IDs/state; kiểm tra vị trí nhìn thấy được, raycast/LOS, collider không chặn lối, checkpoint và multiplayer discovery. Mỗi cụm thay cần ảnh trước/sau thật. Save gate và kiểm tra dirty scene vẫn bắt buộc. Không sửa layout morgue/Tank hoặc các module gameplay khác trong đợt thay prop này nếu chưa có chỉ thị riêng.

### Kiểm chứng của đợt lập đề xuất

- Dùng UnitySkills để xem hierarchy/component và chụp Scene View; không đọc/ghi scene YAML.
- Scene View camera đã được trả về góc ban đầu.
- Unity báo `GameScene.isDirty = false` sau thao tác xem/chụp.
- Không đổi C# runtime, nên không chạy compile/test gameplay giả để tuyên bố các model đã tích hợp.
- Các file mới chỉ phục vụ báo cáo/preview và ảnh chụp. Những thay đổi có sẵn trong working tree được giữ nguyên.
