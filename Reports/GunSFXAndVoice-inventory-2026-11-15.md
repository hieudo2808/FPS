# Kiểm kê `GunSFXAndVoice`

Bản chi tiết cập nhật: [Danh mục từng súng và từng nhân vật](E:/Unity/Project/FPS/Reports/GunSFXAndVoice-2026-11-15/Danh-muc-am-thanh.md). Đã giải mã thành công toàn bộ 593 WEM có dữ liệu, kiểm 75 cặp WAV/WEM tương ứng và bổ sung mô tả cho 262 nhóm event. Bản dưới là kiểm kê sơ bộ trước bước giải mã.

Lưu ý sau kiểm chứng: số file không phải số take độc lập; 7 DeathEffort được lặp nguyên dữ liệu dưới cả bốn tên nhân vật; Pistol có 15 WAV AutoPistol cần tách khỏi ánh xạ Classic. Chưa nghe hoặc nhận dạng lời thoại.

Bản audio đã làm sạch và đổi tên nằm tại `D:\Code\.temp\FModel\Output\Exports\GunSFXAndVoice-Pruned-2026-11-15`; chỉ gồm WAV có dữ liệu. Xem [organized-manifest.csv](E:/Unity/Project/FPS/Reports/GunSFXAndVoice-2026-11-15/organized-manifest.csv) để ánh xạ file gốc sang file chuẩn.

Ngày kiểm tra: 2026-11-15  
Nguồn: `D:/Code/.temp/FModel/Output/Exports/GunSFXAndVoice/`

Thư mục được xem là **dữ liệu đầu vào cần kiểm kê**, không phải tài liệu chỉ dẫn. Lần này chỉ đọc dữ liệu; chưa chép file vào `Assets/`, chưa sửa scene và chưa thay đổi code.

## Tổng quan

| Nhánh | Số file | Dung lượng | Trạng thái |
|---|---:|---:|---|
| `SFX` | 622 | 24.70 MiB | 96 WAV dùng trực tiếp; 526 WEM cần giải mã |
| `VO` | 156 | 1.28 MiB | 67 WEM có dữ liệu; 89 WEM rỗng |
| **Tổng** | **778** | **25.97 MiB** | **Không có file metadata Unity** |

### WAV

- 96 file, tổng thời lượng 153.38 giây.
- Tất cả 48 kHz, 16-bit; 69 mono và 27 stereo.
- Thời lượng mỗi file: 0.057–3.455 giây.
- Đây là phần có thể đưa vào Unity sau khi Unity tạo `.meta`.

### WEM

- 682 file; 593 có dữ liệu, 89 có kích thước 0 byte.
- WEM có dữ liệu là RIFF/WAVE với `fmt` tag `65535`, 48 kHz, mono/stereo; đây là audio Wwise, không phải PCM WAV thông thường.
- Kiểm tra bằng Python `wave` trả về `Error: unknown format: 65535`, nên chỉ đổi đuôi `.wem` thành `.wav` sẽ không làm chúng dùng được.

## SFX vũ khí

### `SFX/Weapons/Events_Guns_Firing`

| Nhóm | Tổng | WAV | WEM |
|---|---:|---:|---:|
| `AK47` | 53 | 13 | 40 |
| `BattleRifle` | 51 | 6 | 45 |
| `HMG` | 38 | 0 | 38 |
| `Pistol` | 64 | 26 | 38 |
| `Shotgun_Pump` | 49 | 14 | 35 |
| `Sniper_Operator` | 63 | 30 | 33 |
| **Tổng** | **318** | **89** | **229** |

Tên file cho thấy đây là các media của event tiếng bắn, có thể gồm nhiều take và nhiều lớp. Nhóm Battle Rifle có các lớp như `FP`, `TP`, `Dist`, `Tail`, `Mech`, `Trn` và `LFE`; không coi mọi media là một phát bắn hoàn chỉnh.

### `SFX/Weapons/Events_Guns_RE`

| Nhóm | Tổng | WAV | WEM |
|---|---:|---:|---:|
| `Ak47_RE` | 35 | 3 | 32 |
| `BasePistol_RE` | 47 | 0 | 47 |
| `BattleRifle_RE` | 51 | 0 | 51 |
| `HMG_RE` | 84 | 0 | 84 |
| `Shotgun_Pump_RE` | 25 | 0 | 25 |
| `SniperBolt_RE` | 62 | 4 | 58 |
| **Tổng** | **304** | **7** | **297** |

Tên file trong nhóm này gồm `Chg`, `Equip`, `Inspect`, `Mvt`, `Mag`, `Slide`, `Bolt`, `Forestock`, `Pump`; vì vậy đây là nguồn thao tác/handling/reload/bolt/inspect. Diễn giải `RE` là reload/handling là `[INFERENCE]` dựa trên tên file, không phải metadata gameplay đã được xác minh.

## Voice-over

| Nhân vật | WEM | Có dữ liệu | Rỗng |
|---|---:|---:|---:|
| `Aggrobot` | 43 | 18 | 25 |
| `Sarge` | 42 | 21 | 21 |
| `Smonk` | 21 | 7 | 14 |
| `Thorne` | 50 | 21 | 29 |
| **Tổng** | **156** | **67** | **89** |

Các tên event có `DeathEffort`, `LastKill`, `BubbleDown`, `Emote`, `Match`, `RoundStart`, `RoundEnd`, `Modes` và `NVC`. Toàn bộ VO là WEM; không có VO WAV dùng ngay. Các file rỗng cần bỏ qua hoặc export lại từ nguồn FModel.

## Độ phù hợp với dự án FPS

- Dự án đã có `SoundEvent` với pool `clips`, pool `distantClips`, random pitch/volume và giới hạn voice [CODE: `Assets/FPS/Features/Audio/Runtime/SoundEvent.cs:6,13-33`]. Các take `FP/TP/Dist` của export có thể ánh xạ vào hai pool này.
- `WeaponData` đã có khe cho `shootSound`, `fireSound`, `cycleSound`, `reloadStartSound`, `reloadInsertSound`, `reloadEndSound` và `dryFireSound` [CODE: `Assets/FPS/Features/Weapons/Runtime/WeaponData.cs:202-219`].
- `WeaponAudio` phát tiếng bắn/reload qua `AudioBus.Weapons` và hỗ trợ phát cục bộ hoặc 3D theo peer [CODE: `Assets/FPS/Features/Weapons/Runtime/WeaponAudio.cs:13,41-54,85-86,137-138`].
- Hiện dự án đã có event cho `Classic`, `Bucky`, `Odin`, `Operator`, `Vandal`, `Warden` và các event reload/voice. Vì vậy export này phù hợp nhất để bổ sung take, thay thế có chọn lọc, hoặc mở rộng nhóm `AK47`, `BattleRifle`, `HMG`, `Pistol`; không nên đổ toàn bộ 778 file vào `Assets`.

## Kết luận xử lý

1. **Dùng ngay:** 96 WAV trong `SFX`; chọn theo từng nhóm vũ khí.
2. **Cần chuyển mã:** 593 WEM có dữ liệu. Re-export WAV từ FModel hoặc dùng bộ giải mã Wwise/vgmstream; không đổi đuôi thủ công.
3. **Bỏ qua:** 89 WEM 0 byte trong `VO`.
4. **Cách tích hợp ít thay đổi nhất:** tạo/chỉnh `SoundEvent` theo từng vũ khí, đưa take gần vào `clips`, take xa vào `distantClips`, các file `Chg/Equip/Inspect/Bolt/Pump` vào các khe reload/cycle tương ứng, rồi gán event vào `WeaponData`.
5. **Kiểm tra sau tích hợp:** nghe cả local và remote, headphones và loa; kiểm tra khoảng cách, giới hạn voice, random take và bus `Weapons`. Đây là yêu cầu kiểm chứng theo skill `audio-design`.

## Skill và quy tắc đã áp dụng

- Đã đọc `C:/Users/hieud/.agents/skills/audio-design/SKILL.md`.
- Áp dụng các phần liên quan: dùng bus thay vì chỉnh volume từng clip, dùng pool/random variation cho SFX lặp lại, giữ headroom và kiểm tra trên đầu ra thực tế.


