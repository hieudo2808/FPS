# Danh sách asset lấy từ FPSSample — 07/10/2026

Nguồn: `E:/Unity/Project/FPSSample-master/`.

Danh sách đầy đủ trong phạm vi đề xuất sau khi đối chiếu FPS: **40 WAV, khoảng 5,45 MiB dữ liệu gốc**. Trong đó **20 clip ưu tiên**, **20 clip cân nhắc sau khi nghe thử và xác định cách tích hợp**. Chưa import asset vào FPS. Ngày 07/10/2026 đã tải và khôi phục 11 GirlFootstepsV2 ngay trong folder nguồn để sửa lỗi mở file báo no audio.

**11 WAV footsteps đã chứa dữ liệu thật**, đúng SHA-256/dung lượng của Git LFS pointer gốc, đọc được dưới dạng PCM WAV và có mẫu âm thanh khác zero. **29 WAV còn lại vẫn chỉ là Git LFS pointer**. Đã kiểm tra sự tồn tại của cả 40 `.meta` đi kèm; chưa nghe để đánh giá âm sắc/chất lượng.

## 1. Ưu tiên: footsteps người chơi — 11 WAV

Folder nguồn: `Assets/Audio/Clips/Characters/`.

Nơi đặt đề xuất trong FPS: `Assets/FPS/Features/Audio/Content/SFX/Movement/Footsteps/`.

Lý do: chưa thấy bank footsteps người chơi riêng trong 25 AudioClip đã kiểm tra. Cần điểm phát theo bước di chuyển; nhập file không tự thêm tính năng.

- `GirlFootstepsV2-01.wav`
- `GirlFootstepsV2-02.wav`
- `GirlFootstepsV2-03.wav`
- `GirlFootstepsV2-04.wav`
- `GirlFootstepsV2-05.wav`
- `GirlFootstepsV2-06.wav`
- `GirlFootstepsV2-07.wav`
- `GirlFootstepsV2-08.wav`
- `GirlFootstepsV2-09.wav`
- `GirlFootstepsV2-10.wav`
- `GirlFootstepsV2-11.wav`

Dung lượng dữ liệu gốc: khoảng **0,94 MiB**. Cấu hình gốc đã được xác minh: Terraformer_1P_FootStep_Left, Terraformer_1P_FootStep_Right và Terraformer_3P_FootStep đều tham chiếu cùng 11 WAV này. SoundSystem.cs chọn clip bằng Random.Range; CharacterEvents.cs phát theo sự kiện FootDown và luân phiên chân trái/phải. Không có phân loại bề mặt cho từng số 01–11 trong các cấu hình này.

1P: phát 2D ở -18 dB, pan trái -0,2 đến 0 hoặc pan phải 0 đến +0,2. 3P: phát 3D ở -10 dB, minDistance 1,5 và maxDistance 30; cả ba cấu hình đổi pitch ngẫu nhiên từ -1 đến +1 semitone. Đây là khác biệt cách phát theo góc nhìn, không phải các bank vật liệu khác nhau. Chưa xác định chúng phù hợp bê tông/kim loại/đất bằng việc nghe.

## 2. Ưu tiên: landing người chơi — 9 WAV

Folder nguồn: `Assets/Audio/Clips/Characters/`.

Nơi đặt đề xuất: `Assets/FPS/Features/Audio/Content/SFX/Movement/Landing/`.

Lý do: bổ sung bank chạm đất; cần nối với chuyển trạng thái airborne → grounded. Phân loại nhỏ/lớn dựa trên tên nguồn, cần nghe và cân mức âm trước khi sử dụng.

- `GirlLandSmall01.wav`
- `GirlLandSmall02.wav`
- `GirlLandSmall03.wav`
- `GirlLandSmall04.wav`
- `GirlLandSmall05.wav`
- `GirlLandSmall06.wav`
- `GirlLandLarge01.wav`
- `GirlLandLarge02.wav`
- `GirlLandLarge03.wav`

Dung lượng dữ liệu gốc: khoảng **2,59 MiB**. Tổng hai nhóm ưu tiên: **20 WAV / 3,53 MiB**.

## 3. Cân nhắc: tiếng quần áo khi di chuyển — 10 WAV

Folder nguồn: `Assets/Audio/Clips/Characters/`.

Nơi đặt đề xuất: `Assets/FPS/Features/Audio/Content/SFX/Movement/Clothing/`.

Lý do: thêm lớp âm thanh chuyển động cho người chơi, sau khi footsteps/landing đã hoạt động. Hiện chưa có điểm phát riêng được xác nhận trong source Player/Input/Audio đã kiểm tra.

- `GirlMovementClothes01.wav`
- `GirlMovementClothes02.wav`
- `GirlMovementClothes03.wav`
- `GirlMovementClothes04.wav`
- `GirlMovementClothes05.wav`
- `GirlMovementClothes06.wav`
- `GirlMovementClothes07.wav`
- `GirlMovementClothes08.wav`
- `GirlMovementClothes09.wav`
- `GirlMovementClothes10.wav`

Dung lượng dữ liệu gốc: khoảng **0,48 MiB**.

## 4. Nghe thử để chọn: âm thanh súng Terraformer — 6 WAV

Folder nguồn: `Assets/Audio/Clips/Weapons/`.

Nơi đặt đề xuất: `Assets/FPS/Features/Audio/Content/SFX/Weapons/Terraformer/`.

FPS đã có ba clip bắn/reload. Sáu WeaponData đang để trống cả shootSound/reloadSound, nên việc gắn clip phù hợp đang có là bước đầu tiên; đây là các biến thể bổ sung, chưa khẳng định tốt hơn hoặc hợp với từng súng.

- `GirlWeaponFire01.wav`
- `GirlWeaponFireAlt01.wav`
- `GirlWeaponFireAlt04.wav`
- `GirlWeaponReload01.wav`
- `GirlWeaponReloadPart1.wav`
- `GirlWeaponReloadPart2.wav`

Ba clip Fire là ứng viên cho `WeaponData.shootSound`. Reload01 là ứng viên cho `reloadSound`. Hai ReloadPart không cắm cùng lúc vào một field reloadSound: chỉ sử dụng khi cần phát theo từng giai đoạn reload hoặc tạo một clip tổng phù hợp.

Dung lượng dữ liệu gốc: khoảng **0,99 MiB**.

## 5. Cân nhắc: âm thanh xác nhận hit/kill — 4 WAV

Folder nguồn: `Assets/Audio/Clips/HUD/`.

Nơi đặt đề xuất: `Assets/FPS/Features/Audio/Content/SFX/HUD/HitConfirm/`.

- `NonLethal01.wav`
- `NonLethal02.wav`
- `Lethal01.wav`
- `Lethal02.wav`

NonLethal: ứng viên cho phản hồi trúng đạn đã được xác nhận. Lethal: chỉ phát khi có xác nhận tiêu diệt; headshot không tự đồng nghĩa với kill.

Hiện WeaponFireHandler đã gọi HUDManager.ShowHitConfirmed, nhưng `hitMarkerText` trong GameScene đang null. Hoàn thiện phản hồi hiện tại trước; bốn WAV này cần thêm đường phát âm thanh nếu chọn sử dụng.

Dung lượng dữ liệu gốc: khoảng **0,45 MiB**.

## File đi kèm và các nhóm không đưa vào danh sách lấy

- Kèm `LICENSE.md` ở root FPSSample khi tái sử dụng nội dung.
- Các `.wav.meta` đi cùng đều tồn tại; giữ chúng nếu muốn giữ cấu hình AudioImporter, kiểm tra GUID khi nhập. Không lấy nguyên hệ Sounddefs/Mixer/Scripts của FPSSample để phát các WAV này; FPS đã có AudioManager và các đường phát âm thanh riêng.
- Không đưa thêm model, nhân vật, súng, prefab môi trường, texture, font hoặc cả pack animation vào lần lấy này: chưa có thiếu hụt cụ thể được xác nhận sau đối chiếu.
- Không đưa Crosshair/HitMarker PNG vào danh sách: FPS có crosshair, còn hit marker hiện dùng TMP và thiếu tham chiếu. PNG nguồn không thay được field TMP trực tiếp.
- Không đưa UI-select, nhạc, grenade/explosion WAV hay VFX/HDRP vào danh sách: FPS đã có các nhóm tương ứng, chưa có đánh giá chất lượng nguồn để đề xuất thay thế.
- Không đưa Robot footsteps/gunfire và GirlJetPack vào danh sách: chưa có yêu cầu robot/jetpack hoặc phong cách âm thanh tương ứng trong FPS hiện tại.

Manifest máy đọc, gồm toàn bộ đường dẫn, SHA-256 và dung lượng dự kiến: [.codex/fpssample-selected-assets-manifest.json](E:/Unity/Project/FPS/.codex/fpssample-selected-assets-manifest.json).

Căn cứ đối chiếu: [FPSSample-Asset-Reuse-Audit-2026-10-07.md](E:/Unity/Project/FPS/Documents/FPSSample-Asset-Reuse-Audit-2026-10-07.md).
