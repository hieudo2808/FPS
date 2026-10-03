# Import verification — 30/09/2026

## Kết quả

- Đã giải nén có kiểm tra path/sha vào thư mục tạm rồi chuẩn hóa thành **17 gói** dưới `Assets/ThirdParty/ApprovedProps/<code>/`.
- Mỗi gói có `Models/`, `Textures/` (nếu có), `Materials/`, `ImportData.json` và `SOURCE.md`.
- Unity nhận diện thành công **17/17 model**: 16 FBX và 1 DAE (`G2`).
- `GameScene` không bị chỉnh: `sceneDirty = false`, vẫn Edit Mode; không có object nào được thay trong scene ở bước này.
- Console sau import: **0 error**; còn 3 warning lịch sử về tên texture Operator trước khi FBX được sửa/reimport. Không có warning mới sau lần reimport cuối.

## Model/mesh đã xác nhận

| Code | Unity asset | Mesh | Tris kiểm tra | Trạng thái |
|---|---|---:|---:|---|
| AM1 | `Assets/ThirdParty/ApprovedProps/AM1/Models/AM1.fbx` | 3 | 1,330 | Imported; 5 texture nguồn thiếu được ghi trong `ImportData.json` |
| AM2 | `.../AM2/Models/AM2.fbx` | 2 | 6,372 | Imported |
| AM3 | `.../AM3/Models/AM3.fbx` | 2 | 2,732 | Imported; roughness kim loại thiếu trong nguồn |
| AM4 | `.../AM4/Models/AM4.fbx` | 3 | 6,000 | Imported/reimported sau khi sửa tên texture có dấu chấm |
| AM5 | `.../AM5/Models/AM5.fbx` | 11 | 7,484 | Imported; packed mask Unity Standard |
| G1 | `.../G1/Models/G1.fbx` | 5 | 4,747 | Imported; normal pipe thiếu trong nguồn |
| G2 | `.../G2/Models/G2.dae` | 1 | 2,796 | Imported từ DAE gốc, không giả đổi sang FBX |
| K1 | `.../K1/Models/K1.fbx` | 2 | 126,296 | Imported nhưng **technical hold** |
| K2 | `.../K2/Models/K2.fbx` | 1 | 76 | Imported |
| L2-R | `.../L2_R/Models/L2_R.fbx` | 7 | 772 | Chỉ computer/monitor/keyboard/mouse/speaker; không đưa bàn/ghế vào asset chuẩn bị |
| LAB-C1 | `.../LAB_C1/Models/LAB_C1.fbx` | 2 | 5,264 | Candidate only |
| LAB-C2 | `.../LAB_C2/Models/LAB_C2.fbx` | 13 | 3,441 | Candidate only; CC BY-NC |
| LAB-C3 | `.../LAB_C3/Models/LAB_C3.fbx` | 282 | 198,412 | Candidate only; CC BY-NC, chưa phải family đã duyệt |
| LAB-C4 | `.../LAB_C4/Models/LAB_C4.fbx` | 35 | 54,288 | Candidate only |
| M1-S1 | `.../M1_S1/Models/M1_S1.fbx` | 1 | 9,983 | Imported từ GLB, chưa thay texture red-cross |
| M2 | `.../M2/Models/M2.fbx` | 1 | 1,330 | Imported; nhãn nguồn vẫn cần đổi thành T-9 trước khi dùng |
| R1 | `.../R1/Models/R1.fbx` | 2 | 28,807 | Imported |

## Không làm trong bước này

- Không đặt asset vào `GameScene`, không thay pickup/console/objective, không tắt legacy object.
- Không tạo collider gameplay, proxy, prefab hay network object.
- Không ghi asset thành `REPLACED_AND_VALIDATED`; tất cả vẫn là `IMPORTED_REVIEW_PENDING`, `IMPORTED_TECHNICAL_HOLD` hoặc `IMPORTED_CANDIDATE_REVIEW_PENDING`.
- Không tự bổ sung các texture bị thiếu từ nguồn; các lỗi này giữ trong `ImportData.json` để xử lý có chủ đích.

## Dọn dữ liệu

- Đã kiểm tra và giữ ZIP gốc trong `C:/Users/hieud/Downloads/` làm provenance/rollback.
- Sau khi ghi báo cáo này, chỉ thư mục giải nén tạm `E:/Unity/Project/FPS/Temp/ApprovedPropImport-20260930/` được xóa.
