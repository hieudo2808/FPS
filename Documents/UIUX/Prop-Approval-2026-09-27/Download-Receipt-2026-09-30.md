# Download receipt — 30/09/2026

## Kết quả

**Đủ 17/17 original archive trong Asset Matrix: 12 mẫu chính đã chọn, 1 fuse technical hold, 4 ứng viên Lab chưa duyệt.**

- Thư mục thực: `C:/Users/hieud/Downloads/`.
- Tổng: **467,655,072 bytes (446.0 MiB)**, không tính bản M67 trùng.
- G1 đã có từ trước; lượt này tải 16 archive còn lại bằng Sketchfab Download 3D Model → Original format trong phiên đăng nhập của người dùng.
- Kiểm tra ZIP bằng .NET: đọc được tất cả entry streams, kiểm tra đường dẫn; G2/K2/LAB-C1 đã kiểm tra cả ZIP lồng. Có model nguồn trong đủ 17 gói.
- Kiểm tra này không thay thế import test, visual QA, kiểm tra dependency/material hoặc kiểm tra CRC độc lập. Không chạy script hay executable từ archive.
- Các archive không có README/license riêng được nhận diện; license/credit lấy từ trang nguồn và giữ trong [Provenance-Manifest.csv](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Provenance-Manifest.csv).
- Không import Unity, không sửa code/prefab/scene, không save GameScene trong lượt tải này. Scene không được query lại; trạng thái dirty cũ không được ghi thành kiểm tra mới.

## File đã tải

| Mã | Archive | MiB | Model gốc đã xác nhận | Bước còn lại |
|---|---|---:|---|---|
| L2-R | [computer-desk.zip](<C:/Users/hieud/Downloads/computer-desk.zip>) | 48.48 | FBX | Chờ audit import/material/scale |
| G1 | [m67-grenade (1).zip](<C:/Users/hieud/Downloads/m67-grenade (1).zip>) | 126.30 | FBX | Chờ audit import/material/scale |
| G2 | [incendiary-grenade.zip](<C:/Users/hieud/Downloads/incendiary-grenade.zip>) | 3.67 | DAE (ZIP lồng) | Chờ chuyển đổi và audit; không có FBX trong lựa chọn gốc |
| M1-S1 | [medical-kit-bag.zip](<C:/Users/hieud/Downloads/medical-kit-bag.zip>) | 18.47 | GLB | Chờ chuyển đổi và audit; không có FBX trong lựa chọn gốc |
| M2 | [az-vaccine-vial.zip](<C:/Users/hieud/Downloads/az-vaccine-vial.zip>) | 0.21 | FBX | Chờ audit import/material/scale |
| AM1 | [handgun-ammo-box-remake.zip](<C:/Users/hieud/Downloads/handgun-ammo-box-remake.zip>) | 46.74 | BLEND | Chờ chuyển đổi và audit; không có FBX trong lựa chọn gốc |
| AM2 | [assault-rifle-ammo-box-remake.zip](<C:/Users/hieud/Downloads/assault-rifle-ammo-box-remake.zip>) | 10.98 | FBX | Chờ audit import/material/scale |
| AM3 | [shotgun-ammo-box.zip](<C:/Users/hieud/Downloads/shotgun-ammo-box.zip>) | 49.72 | FBX | Chờ audit import/material/scale |
| AM4 | [sniper-ammo-box-game-ready.zip](<C:/Users/hieud/Downloads/sniper-ammo-box-game-ready.zip>) | 30.58 | FBX | Chờ audit import/material/scale |
| AM5 | [ammunition-box.zip](<C:/Users/hieud/Downloads/ammunition-box.zip>) | 3.91 | FBX | Chờ audit import/material/scale |
| K2 | [keycard.zip](<C:/Users/hieud/Downloads/keycard.zip>) | 2.40 | FBX (ZIP lồng) | Chờ audit import/material/scale |
| R1 | [military-radio.zip](<C:/Users/hieud/Downloads/military-radio.zip>) | 48.68 | FBX | Chờ audit import/material/scale |
| K1 | [fuse-3d-model.zip](<C:/Users/hieud/Downloads/fuse-3d-model.zip>) | 8.99 | BLEND | Technical hold: 126,296 tris theo nguồn; chưa optimize |
| LAB-C1 | [lab-shelf-utility-cart-virus-laboratory.zip](<C:/Users/hieud/Downloads/lab-shelf-utility-cart-virus-laboratory.zip>) | 21.22 | FBX (ZIP lồng) | Chưa duyệt visual/family |
| LAB-C2 | [fume-cupboards.zip](<C:/Users/hieud/Downloads/fume-cupboards.zip>) | 3.19 | GLB | Chưa duyệt visual/family |
| LAB-C3 | [lab.zip](<C:/Users/hieud/Downloads/lab.zip>) | 20.48 | GLB | Chưa duyệt visual/family |
| LAB-C4 | [lab-bench.zip](<C:/Users/hieud/Downloads/lab-bench.zip>) | 1.97 | BLEND | Chưa duyệt visual/family |

**Phân bố định dạng:** 10 gói có FBX, 1 DAE, 3 GLB, 3 BLEND. Không đổi đuôi file để giả thành FBX.

## Model bên trong và checksum

### L2-R — computer-desk.zip

- SHA-256: `1F7F62B1DB3061032FEEE02BED37775D230E34E66916CD089642554FAA5C6F60`.
- Model: `source/ComputerDesk.fbx`.
- Texture rời ở ZIP ngoài: 64. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### G1 — m67-grenade (1).zip

- SHA-256: `12B0E085DD98FA0C4E9C51657FA605FD466CA6602228CCBBF3065D4AD5C101D1`.
- Model: `source/granada_sketchfab.fbx`.
- Texture rời ở ZIP ngoài: 25. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### G2 — incendiary-grenade.zip

- SHA-256: `573CC3BE732D4B71D53B3B751BF9A3E8D7FB3F676EEF74D2DCBD14427ACFF85D`.
- Model: `source/model.zip!/model/model.dae`.
- Texture rời ở ZIP ngoài: 5; có thêm texture bên trong ZIP lồng. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### M1-S1 — medical-kit-bag.zip

- SHA-256: `E50263FB4270815A1A065FF98E03D8A031070B9C00B8C1D480D6A75D1460B986`.
- Model: `source/model.glb`.
- Texture rời ở ZIP ngoài: 4. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### M2 — az-vaccine-vial.zip

- SHA-256: `442260973F43ABE5FDA1FE2D82D3880D8134DF6BA833FEA3643BF58879812D5E`.
- Model: `source/AZvaccine.fbx`.
- Texture rời ở ZIP ngoài: 1. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### AM1 — handgun-ammo-box-remake.zip

- SHA-256: `0C58E0705F58C2006A448FAE2BE8B0D01E9D4B42DCCDC9DD36E9FF3D01B2BE69`.
- Model: `source/Handgun Ammo Box REMAKE.blend`.
- Texture rời ở ZIP ngoài: 11. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### AM2 — assault-rifle-ammo-box-remake.zip

- SHA-256: `1CFC731A8C68406F17A342E5F9785DF42E6DDC1538B9503883C7B19AA45BAE9E`.
- Model: `source/Assault Rifle Ammo Box REMAKE.fbx`.
- Texture rời ở ZIP ngoài: 8. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### AM3 — shotgun-ammo-box.zip

- SHA-256: `C91BADF2778DDD6DB4BC675478A2D68FEB6E377BE78702778017B75DF4CBD39C`.
- Model: `source/Shotgun Ammo Box.fbx`.
- Texture rời ở ZIP ngoài: 8. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### AM4 — sniper-ammo-box-game-ready.zip

- SHA-256: `2353B1563DAE9907D6AE8CDEE4833EA977E6E477FB6CE07626A463CE1A05BD8F`.
- Model: `source/Sniper Ammo Box.fbx`.
- Texture rời ở ZIP ngoài: 11. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### AM5 — ammunition-box.zip

- SHA-256: `1774098743EE768ADE38D1DBE8C28683B1161B83E4F0F3C9A4F83C663ADEAE21`.
- Model: `source/005_AmmunitionBox_FinalRenderTest.fbx`.
- Texture rời ở ZIP ngoài: 4. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### K2 — keycard.zip

- SHA-256: `1AF53259ECDD5E15BA2368F49B25FECC26568A7DB03C83319C47C0CDC862D372`.
- Model: `source/117a8266e3e74e7d9539f0fd2b5f306d.zip.zip!/Card_Model/Card_model.fbx`.
- Texture rời ở ZIP ngoài: 3; có thêm texture bên trong ZIP lồng. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### R1 — military-radio.zip

- SHA-256: `8436E90594DE9E18B538A6C0BE58F91C4A0DC844A054A03C9EF6959190063027`.
- Model: `source/radio.fbx`.
- Texture rời ở ZIP ngoài: 9. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### K1 — fuse-3d-model.zip

- SHA-256: `A716A6B16CB36716210DBBE48FF2DB7C3EF3342DD7FDF4F7CAF102B1AC8D5D76`.
- Model: `source/fuse.blend`.
- Texture rời ở ZIP ngoài: 0. Không có texture rời; material/procedural/packed data bên trong BLEND chưa kiểm tra.

### LAB-C1 — lab-shelf-utility-cart-virus-laboratory.zip

- SHA-256: `614999EAE6E43DEE6471DB632E7A25BD78CCAC53BFD7D85DCFA4088C5BD4F614`.
- Model: `source/SF_LafShelf_and_utilityCart_NakedSingularity.zip!/Lab shelf + utility cart.zip!/Utility_cart/SM_Utility_cart.fbx`; `source/SF_LafShelf_and_utilityCart_NakedSingularity.zip!/Lab shelf + utility cart.zip!/Lab_shelf/SM_lab_shelf_01.fbx`; `source/SF_LafShelf_and_utilityCart_NakedSingularity.zip!/SF_LabShelf_and_utilityCart_NakedSingularity.fbx`.
- Texture rời ở ZIP ngoài: 10; có thêm texture bên trong ZIP lồng. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### LAB-C2 — fume-cupboards.zip

- SHA-256: `E0717F1D8EFDBB97BAA85D2120631037A07C47A7186F3207A0B966B65783B802`.
- Model: `source/FUME CUPBOARDS.glb`.
- Texture rời ở ZIP ngoài: 6. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### LAB-C3 — lab.zip

- SHA-256: `61103E2DF79A06E26A42EC69D877FA62EED9BEC529338BA4E0A6745D14589764`.
- Model: `source/lab.glb`.
- Texture rời ở ZIP ngoài: 15. Chưa xác nhận material đã trỏ đúng hoặc đủ mọi dependency.

### LAB-C4 — lab-bench.zip

- SHA-256: `18F5E703DBB2426C109B01725D17051903A8ED268BFE284FDC94392D0AFE617E`.
- Model: `source/szene5_pastell&schatten.blend`.
- Texture rời ở ZIP ngoài: 0. Không có texture rời; material/procedural/packed data bên trong BLEND chưa kiểm tra.

## Giới hạn còn giữ nguyên

- M1 cần thay dấu chữ thập đỏ bằng nhãn MEDICAL trung tính; M2 cần nhãn T-9 hư cấu. Chưa sửa texture.
- L2-R chỉ dùng phần máy tính, không dùng bàn gỗ/ghế; tách mesh và mặt đỡ chưa kiểm tra.
- K1 có nguồn BLEND nhưng vẫn technical hold; không coi tải được là đủ điều kiện thay AsylumFuse.
- LAB-C1–C4 chỉ là ứng viên đã tải để audit. C2/C3 dùng CC BY-NC 4.0; chưa có family được duyệt phủ đủ 13 loại/71 instance.
- Nguyên bản `m67-grenade.zip` và `m67-grenade (1).zip` đều được giữ nguyên; không tải G1 thêm trong lượt này, không xóa file người dùng.
- SHA G1 đầy đủ kết thúc bằng **D1**; manifest này dùng hash đọc trực tiếp từ file (thay cho chuỗi bị cắt trong trả lời trước).
- Các final status như REPLACED_AND_VALIDATED chưa được gán cho bất kỳ prop mới nào. Archive confirmed không đồng nghĩa imported/replaced/validated.

