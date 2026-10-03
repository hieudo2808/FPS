# Bổ sung duyệt asset — máy tính, bom, đạn và vật phẩm dùng trực tiếp

Ngày: 27/09/2026. Kế thừa [báo cáo prop](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/Approval.md).

## Quyết định đã nhận

- Người dùng đồng ý phần còn lại của báo cáo trước; yêu cầu **L2 cũng phải thay bằng asset máy tính thật**. Không giữ phương án máy tính `ControlDesk` cũ.
- Người dùng xác nhận: **hộp đạn chỉ cấp cho đúng súng; không nhận được thì không mất hộp**. Đây là thay đổi cơ chế pickup được chấp thuận, chưa được implement trong đợt lập bảng ảnh này.
- Các model mới bên dưới là **ứng viên chờ duyệt visual**. Chưa tải model/import, chưa thay prefab/scene, chưa đổi C# gameplay.
- Chấp thuận báo cáo trước không tự giải quyết K1 cầu chì đang technical hold, cũng không có nghĩa đặt đồng thời cả A2-A lẫn A2-B.

## 1. Những gì gameplay thực sự có

Đã query Unity AssetDatabase và đọc các file C# được thu hẹp. Codebase Memory không có tool được expose trong phiên này, nên không giả lập kết quả index; đối chiếu nguồn tại các đường dẫn cụ thể.

| Phần | Bằng chứng hiện tại | Hệ quả cho asset |
|---|---|---|
| Bom | `ThrowableKind { Frag, Incendiary }` trong `SurvivalInventory.cs`; hai pickup tương ứng tồn tại | Cần 2 model khác hình dáng. Không thêm smoke, flashbang, pipe bomb hoặc Molotov chỉ để lấp bảng. |
| Primary | `PrimaryWeaponId { Vandal, Operator, Odin, Bucky }` trong `WeaponManager.cs` | Bốn hộp đạn primary, cộng Classic cho pistol. |
| Ammo | `PickupType.Ammo` và `PickupItem.GiveAmmo` gọi `AddReserveAmmoServer(ammoAmount)`; chưa có trường ammo category trong `WeaponData` | Hiện chưa phải hệ pickup phân loại theo từng súng. Thay ảnh/model riêng không tự thay cơ chế. |
| Medical | `ConsumableKind { Medkit, Antidote }`; có MedkitPickup và AntidotePickup | Cần visual cho đồ dùng trực tiếp, không chỉ thùng tiếp tế đặt trong map. |
| Nguồn grenade cũ | `Documents/Survival-Items-Implementation.md` ghi mesh `HE_Grenade_3PV` và texture từ folder người dùng; thumbnail hiện có khớp hình grenade đó | Không gọi mọi grenade là tự dựng. G1 là đề xuất đổi sang phong cách realistic, không phải khẳng định grenade hiện tại làm từ cube. |

AssetDatabase còn có WeaponData mang tên Warden, AKM và Pistol; sự tồn tại của file không chứng minh chúng là lựa chọn đang dùng trong campaign. Không tự thêm ba loại đạn chỉ vì nhìn thấy tên asset. Mapping đợt này bao phủ Classic + bốn primary có trong contract hiện tại.

## 2. L2-R, bom và đồ y tế

![Máy tính, hai grenade và medical — ảnh tác giả, trừ ô LOCAL](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/04-Computer-Grenades-Medical.png)

| Mã | Asset / nguồn | Mapping | Quyết định đề xuất |
|---|---|---|---|
| **L2-R** | [Computer Desk — CR!STALLL](https://sketchfab.com/3d-models/computer-desk-ff0674f66e11462e8b3417dee2b58ac9) | Máy tính LabTrace tại B, LabTransmit tại F | Lấy **monitor, keyboard, mouse** từ bộ asset PBR, đặt trên bàn lab hiện có. Không bê cả bàn gỗ/ghế/đèn trong ảnh vào phòng lab; không thêm pedestal. Tác giả liệt kê các model này riêng trong archive. 12.548 tris là **cả bộ**, chưa biết phần máy tính riêng bao nhiêu. |
| **G1** | [M67 GRENADE — Tiago Lopes](https://sketchfab.com/3d-models/m67-grenade-d202644dfaf441a0a145befbd7add45a) | FragGrenadePickup; model trên tay; projectile; thumbnail HUD | 4.747 tris, tác giả công bố FBX + PBR 4K và các phần tách rời. Silhouette tròn, thân kim loại xanh. Không thay sát thương, timing hoặc rule nổ khi thay asset. |
| **G2** | [Incendiary Grenade — R.Linden](https://sketchfab.com/3d-models/incendiary-grenade-74a5e9a18c1947118d1895dca900afb5) | IncendiaryGrenadePickup; trên tay; projectile; thumbnail HUD | 2.796 tris. Dạng canister dài có ký hiệu ngọn lửa và nhãn INCENDIARY, khác cả hình dáng lẫn chữ so với frag. Đây là thiết kế hư cấu mang chất liệu realistic, không khẳng định đúng một mẫu grenade quân sự thật. |
| **M1** | [Medical Kit Bag — yronthal](https://sketchfab.com/3d-models/medical-kit-bag-abc2f95bce4c4e69bfc0d8742d37e78b) | MedkitPickup, item dùng/trên tay, thumbnail; cùng họ S1 | Tái dùng mẫu túi đã đề xuất, không tạo thêm model medkit. Phân biệt **thùng/túi cấp vật phẩm S1** và **đồ tiêu hao M1** ở component/gameplay; không gộp hai loại pickup. Đổi biểu tượng/nhãn sang MEDICAL trung tính như báo cáo trước. |
| **M2** | [AZ Vaccine vial — VRC-IW](https://sketchfab.com/3d-models/az-vaccine-vial-a1bb302ae09645e78cbda7bd53fd9dff) | AntidotePickup, vial dùng trực tiếp, thumbnail | 1.330 tris. **Chỉ duyệt hình lọ/nắp và vật liệu**, bắt buộc thay toàn bộ chữ vaccine/COVID/brand bằng nhãn treatment T-9 hư cấu. Không trình bày thuốc thật là thuốc trị tác nhân hư cấu. Nếu không muốn asset cần đổi nhãn, mục này chưa chốt và sẽ tìm mẫu khác. |

Ảnh vẫn nguyên bản tác giả, **chưa sửa texture**. Duyệt G1/G2 bao gồm giữ cùng một model nhận diện ở world pickup, viewmodel và projectile; không để trên bàn một quả nhưng ném ra quả khác. Cần kiểm tra pivot/scale/tay/animation và collider sau import, không giả định “game-ready” là đã khớp rig hiện tại.

L3 của báo cáo trước vẫn là dùng prefab CRT có sẵn cho archive ở E. Không tự thay tất cả máy tính Asylum bằng LCD chỉ vì thay L2. Có thể tái dùng L2-R ở E nếu người dùng muốn đồng nhất riêng khu Lab, nhưng chưa gộp thay đổi đó vào duyệt L2.

## 3. Hộp đạn theo từng súng

![Năm hộp đạn đề xuất — ảnh tác giả](E:/Unity/Project/FPS/Documents/UIUX/Prop-Approval-2026-09-27/05-Ammo-Per-Weapon.png)

| Mã | Súng / nhãn gameplay | Asset / tác giả | Số tris công bố | Hình dáng và mapping |
|---|---|---|---:|---|
| **AM1** | Classic — PISTOL AMMO | [Handgun Ammo Box REMAKE — jsandwich96](https://sketchfab.com/3d-models/handgun-ammo-box-remake-74871b58382a418ab310e6d22b1e263f) | 1.330 | Hộp carton nhỏ với khay đạn ngắn. Chỉ cấp reserve Classic. |
| **AM2** | Vandal — RIFLE AMMO | [Assault Rifle Ammo Box REMAKE — jsandwich96](https://sketchfab.com/3d-models/assault-rifle-ammo-box-remake-0e880a3c2cfd4ea9a4891a81f621733b) | 6.724 | Hộp đạn rifle; khác hộp pistol về dáng/viên đạn và nhãn. Chỉ cấp Vandal. |
| **AM3** | Bucky — SHOTGUN SHELLS | [Shotgun Ammo Box — jsandwich96](https://sketchfab.com/3d-models/shotgun-ammo-box-964f780f87d34d54890f9de94792fd01) | 2.732 | Hộp dựng mở thấy shotgun shells. Chỉ cấp Bucky. |
| **AM4** | Operator — SNIPER AMMO | [Sniper Ammo Box — jsandwich96](https://sketchfab.com/3d-models/sniper-ammo-box-game-ready-5338de8c47ea4f68aa40dd51602a2b53) | 6.000 | Khay/hộp sniper, khác nhãn và bố cục hộp rifle. Chỉ cấp Operator. |
| **AM5** | Odin — MACHINE-GUN AMMO | [Ammunition Box — Darren McNerney 3D](https://sketchfab.com/3d-models/ammunition-box-17898b0b68114516b704d0aabc9d2142) | 7.492 cho bộ | Chọn một thùng kim loại, không đặt nguyên cả display nhiều hộp trong preview. Chỉ cấp Odin. Không hứa có dây đạn animate vì chưa kiểm tra mesh. |

**Giới hạn quan trọng:** chữ 9mm, 7mm, 12 gauge… trên preview là texture gốc của asset. Project hiện không khai báo cỡ đạn vật lý cho từng súng. Đề xuất dùng tên súng/nhóm đạn ở HUD và nhãn, không suy diễn Classic/Vandal/Operator dùng đúng cỡ đạn trên bao bì. Nếu cần đồng nhất canon, thay chữ caliber/brand và số viên in sẵn để không mâu thuẫn lượng pickup; giữ mesh/texture chất liệu của tác giả, không tự dựng hộp mới.

AM1–AM4 cùng tác giả giúp đồng nhất phong cách. Tác giả ghi shotgun/sniper lấy cảm hứng từ Resident Evil và tự làm model; AM5 được tác giả mô tả là phiên bản tự làm lấy cảm hứng Wolfenstein. Đây là khai báo provenance của uploader, không phải kết luận độc lập về mọi quyền bên thứ ba. Không quảng bá các model này là asset chính thức từ các game đó.

## 4. Contract pickup đạn đã được người dùng chốt

Quy tắc bắt buộc: **đúng súng, không nhận thì không mất hộp**. Đợt này chỉ ghi contract, chưa code:

1. Mỗi pickup có loại đạn/súng đích rõ ràng. Server quyết định lượng đạn nhận, không để client tự grant.
2. Không đổi AM3 thành đạn Vandal chỉ vì người chơi đang cầm Vandal; không ghi cùng một loại `Ammo` chung dưới năm model khác nhau.
3. Nếu người chơi không có súng phù hợp hoặc reserve đã đầy: nhận 0, **không claim/consume**, hiện lý do ngắn.
4. Đề xuất UX: xét súng đang sở hữu ở các slot, không bắt phải đổi sang súng đó mới nhặt. Đây là chi tiết triển khai đề xuất, chưa giả định hệ server hiện tại đã hỗ trợ grant vào slot không active.
5. Chỉ mark claimed sau khi server cấp thành công lượng dương; xử lý hai người nhặt cùng lúc, disconnect và đổi súng cùng lúc theo transaction hiện có.
6. Partial fill là điểm cần giữ rõ khi triển khai: khuyến nghị nhận tới giới hạn và giữ số còn lại trên hộp; không tự tuyên bố hành vi này đã được user chốt hoặc hiện có.
7. Checkpoint/retry phải giữ đúng type, số lượng và trạng thái claim; không hồi sinh hộp đã commit ngoài rule restore checkpoint.

Không thêm ammo grid, weight, loadout, drop/share ammo, caliber simulation hay generic inventory framework. Không âm thầm thay balance số đạn mỗi hộp trong lần thay model.

## 5. Nguồn có sẵn và tránh làm lại sai chỗ

- Folder `E:/ProjectSettings/Assets/Resources/inventory/icons` có `AmmoPistol.png`, `AmmoRifle.png`, `AmmoShotgun.png`, `AmmoMachineGun.png`, `AmmoHeavyGun.png`, `BonusItemAmmobox*.png`. Đây là **icon 2D**, không thay được yêu cầu model hộp đạn trên bàn/map. Có thể reuse hoặc render lại từ model được duyệt cho HUD.
- Grenade cũ đã có mesh từ folder người dùng. Preview LOCAL trong bảng 04 là thumbnail hiện có, không phải screenshot model mới hay bằng chứng nó do AI dựng.
- Theo hồ sơ triển khai survival hiện tại, medkit pouch/antidote vial là geometry đơn giản tự dựng; vì vậy bổ sung M1/M2 là cần thiết để không bỏ sót.
- Không biến mọi EquipmentCase S2 thành hộp đạn. S2 là supply prop trong báo cáo trước; AM1–AM5 mới là visual cho pickup Ammo theo súng. Trước khi thay từng instance phải đọc type/component thực tế.

## 6. License và kiểm tra còn lại

Tất cả **ứng viên chính** L2-R, G1, G2, M1, M2, AM1–AM5 được API nguồn ghi là downloadable, CC BY 4.0 tại thời điểm kiểm tra. Credit tên model/tác giả/URL/[license](https://creativecommons.org/licenses/by/4.0/) và ghi rõ chỉnh sửa texture. Metadata có timestamp lưu trong `Previews/*.json`.

Không nhầm với ảnh thử trong `Additional-Candidates-Review.png`: đó là scratch review, có ứng viên không được chọn (máy voxel, PC thiếu texture, grenade sci-fi phát sáng, injector không hợp phong cách). Board **04/05** và bảng trên mới là danh sách khuyến nghị. Grenade set Riskyerick dùng Free Standard, không bị gắn nhãn CC BY và không nằm trong đề xuất chính này.

Sau khi duyệt visual: tải model gốc; kiểm tra dependency, mesh/material, render pipeline thực tế, texture/normal, pivot/scale, animation và collider. Không assume URP: hồ sơ survival trước đây ghi Built-in; cần đọc setting hiện tại khi tích hợp. Không đổi package/render pipeline để dùng một prop.

Các kiểm tra mục tiêu khi implement ammo: đúng/sai súng, reserve đầy, lượng nhận dương, đồng thời hai client, slot không active nếu được hỗ trợ, disconnect trước commit, restore checkpoint. Đối với grenade: visual đồng nhất ở pickup/trên tay/projectile; không làm đổi trigger nổ, hitbox hoặc throw timing. Sau đó chụp ảnh thực trong Unity để duyệt kết quả tích hợp.

## Trạng thái cuối lượt lập bảng

Chỉ cập nhật tài liệu và ảnh/metadata đề xuất. Không sửa C# runtime, prefab, scene hay inventory state. Không chạy test gameplay để tạo cảm giác các asset đã tích hợp. Script dựng board có kiểm tra license và kiểm tra PNG đọc được ở kích thước dự kiến.
