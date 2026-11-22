# 23 · Âm thanh súng mới từ E:\Audio, khớp theo animation

Ngày: 2026-11-15. Yêu cầu: "Tôi để âm thanh tại E:\Audio. Nghiên cứu rồi đưa vào súng phù hợp cho tôi, xong đưa ra video tôi xem khớp không".

Video kiểm tra: [SungMoi_6khau_v2.mp4](Audio-2026-11-15/SungMoi_6khau_v2.mp4), bản đã sửa lệch hình–tiếng (xem mục "Sửa lệch hình–tiếng trong video"). Video dài 65,7 giây, gồm 6 khẩu, mỗi khẩu có ba đoạn: rút súng, bắn, nạp đạn. Chữ ở giữa đáy màn hình là tên tiếng thao tác, hiện đúng lúc tiếng đó bắt đầu phát.

## Kết quả

- **Người dùng duyệt (2026-11-15):** "Khớp rồi" với video v2 — hình và tiếng của 6 khẩu đã khớp.

- **Đã xong:** 6 khẩu dùng bộ tiếng mới, gồm:
  - tiếng bắn,
  - tiếng rút súng,
  - tiếng kéo khoá nòng hoặc bơm sau phát bắn (Operator, Bucky),
  - toàn bộ chuỗi nạp đạn.
- **Đã kiểm tra:**
  - Test Editor đều qua.
  - Test host + client đều qua.
  - Đã đối chiếu hình và tiếng cho một số mốc.
- **Chưa kiểm tra:** chưa ai nghe để duyệt độ to nhỏ và chất tiếng. Phần này chờ người dùng xem video.

## Nghiên cứu nguồn E:\Audio [INFERENCE từ tên file, đo bằng Blender]

- **Khối lượng:** 174 file WAV (48 kHz, 16-bit, 11,9 MB). Thư mục chia theo `<Súng>/Fire` và `<Súng>/Handling`.
- **Nguồn gốc:** tên file theo kiểu âm thanh gốc Valorant (`Vandal_MagIn_Start_FP_001`, `Warden_Fire_TP_Distant_Front_001`, …).
- **Phạm vi dùng:** chỉ dùng trong dự án giáo dục, không phát hành, theo AGENTS.md. Không được phát hành công khai.
- **Các lớp của tiếng bắn**, tách theo đo phổ và đường bao âm lượng:

| Súng | Lớp chính | Lớp phụ |
|---|---|---|
| Classic | `Classic_Fire_Normal_001` (0,97 s) | `_002`: lớp trầm, trọng tâm phổ 42 Hz |
| Vandal | `Vandal_Fire_002` (2,86 s) | `_001`: lớp cơ khí, đỉnh ở 89 ms |
| Odin | `Odin_Fire_002` (1,48 s) | `_001`: lớp cơ khí, khoảng 7,5 kHz |
| Operator | `Operator_Fire_001` (2,99 s, stereo) | `_002`: lớp trầm, 54 Hz |
| Bucky | `BuckyFire` (2,14 s) | không có |
| Warden | đủ 4 lớp góc nhìn thứ nhất: transient, tail, LFE, mechanical | 6 bản góc nhìn thứ ba ở gần, 6 bản ở xa |

## Cách gắn vào súng

### Tiếng bắn

- **Trộn lớp:** lớp chính 0 dB, lớp phụ −3 hoặc −6 dB. Cắt đuôi dưới −60 dB, chuẩn hoá đỉnh −1 dBFS.
- **Warden:** 4 tổ hợp lớp, cho mỗi phát một take khác nhau.
- **Bản nghe từ xa:**
  - Warden dùng chính bản `TP_Distant` có sẵn.
  - Các khẩu khác dùng bản gần đã lọc thấp 1,4 kHz và thêm vọng ngắn.
- **Người chơi khác bắn Warden:** nghe bản `TP_Close` qua trường mới `SoundEvent.remoteClips` [CODE Assets/FPS/Features/Audio/Runtime/SoundEvent.cs:18], [CODE Assets/FPS/Features/Audio/Runtime/GameAudioHost.cs:85].
- **Âm lượng sự kiện:** cân theo RMS 150 ms đầu, mức đo sau khi nhân âm lượng:

| Súng | Âm lượng | RMS đo được |
|---|---|---|
| Vandal, Odin, Warden | 0,75 | khoảng −10,8 dB |
| Operator | 1,0 | −10,1 dB |
| Bucky | 0,8 | −9,3 dB |
| Classic | 0,85 | −14,4 dB (nhỏ hơn súng trường khoảng 3,7 dB) |

- **Tiếng đuôi dài khi bắn liên thanh:** một voice bị cướp chỗ giờ tắt dần trong 60 ms thay vì cắt ngang, để tránh tiếng tách [CODE Assets/FPS/Features/Audio/Runtime/GameAudioHost.cs:136].

### Tiếng thao tác đặt theo mốc thời gian của animation

- **Dữ liệu mới:** `WeaponSoundCue` (thời điểm, clip, âm lượng), gồm 3 danh sách `equipCues`, `fireCues`, `reloadCues` [CODE Assets/FPS/Features/Weapons/Runtime/WeaponData.cs:11], [CODE Assets/FPS/Features/Weapons/Runtime/WeaponData.cs:226].
- **Cách phát** [CODE Assets/FPS/Features/Weapons/Runtime/WeaponAudio.cs:61], [CODE Assets/FPS/Features/Weapons/Runtime/WeaponAudio.cs:101]:
  - Mốc nạp đạn bám dòng thời gian nạp đạn do server phát lại, giãn theo hệ số tốc độ nạp.
  - Mốc rút súng bắt đầu từ thời điểm equip [CODE Assets/FPS/Features/Weapons/Runtime/Weapon.cs:1231].
- **Bucky** nạp từng viên, vẫn dùng 3 sự kiện:
  - `Weapon_Bucky_ReloadStart`;
  - `Weapon_Bucky_ShellIn`: 2 take luân phiên, đã cắt phần đầu để tiếng cạch rơi vào 5 ms;
  - `Weapon_Bucky_ReloadEnd`.
- **Bảng mốc:** [WeaponFoley.json](../Assets/FPS/Features/Audio/Editor/WeaponFoley.json), 89 mốc. Menu `Tools/FPS/Audio/Build All` đọc bảng này [CODE Assets/FPS/Features/Audio/Editor/GameAudioSetup.cs:362]. Muốn chỉnh mốc nào thì sửa `t` trong file JSON rồi chạy lại menu.
- **Cách tính mốc:**
  - Đọc chuyển động xương tay và xương súng trong clip `FP_*` và `GN_*`, tìm lúc tay kéo khoá nòng, đẩy băng, gạt bơm.
  - Trừ đi độ trễ từ đầu file tới đỉnh tiếng của từng file.
  - Đối chiếu bằng tấm ghép khung hình [PLAYTEST]. Kết quả đối chiếu:

| Súng | Mốc | Lệch so với hình |
|---|---|---|
| Vandal | tháo băng (0,63 s) | dưới 0,04 s |
| Vandal | lắp băng (1,33–1,40 s) | dưới 0,04 s |
| Classic | lắp băng, kéo/thả trượt (1,31 / 1,49 s) | khớp |
| Warden | hất băng (0,70 s), lắp băng (1,53 s), khoá nòng (1,97 / 2,12 s) | khớp |
| Vandal | kéo khoá nòng, bản quay lần 1 | trễ 0,1–0,2 s, đã sửa sang 1,64 / 1,88 s |
| Warden | "Magazine_Out", bản quay lần 1 | đặt ở 0,12 s là sai, đã dời sang 0,69 s |

- **Đã gỡ:** 7 sự kiện CC0 dùng chung, không còn ai tham chiếu: `Weapon_Reload_*`, `Weapon_Shotgun_*`, `Weapon_Bolt_Cycle`.

## Bằng chứng chạy

```
[GameAudioSetup] imports=1 ... events=26 ... weapons=6 foleyCues=89 enemies=8[...] players=4[...]
FPS.Tests.AudioContentTests  Test run completed: 5/5 passed.
FPS.Tests.AudioRulesTests    Test run completed: 5/5 passed.
FPS.Tests.GameAudioPlayTests Test run completed: 2/2 passed.
FPS.Tests.AudioHostClientTests (peer build mới, 17:38) Test run completed: 1/1 passed.
  reload Warden: host cues=10 (Warden_Reload_Movement_C_001 0->1), client own cues=10 (Warden_Reload_Movement_C_001 0->1)
FPS.Tests.AudioVisualCapture.RecordGunVideo Test run completed: 1/1 passed.
  classic: 3 shots, equip 1.00s, reload 2.12s | vandal: 10 shots | odin: 12 shots, reload 6.00s
  warden: 6 shots | operator: 2 shots, reload 4.50s | bucky: 2 shots, reload 4.03s
```

- **Log đầy đủ:** [RecordGunVideo.txt](Audio-2026-11-15/RecordGunVideo.txt) và [MusicGunsReloadZombiesAndVoicesAcrossPeers.txt](Audio-2026-11-15/MusicGunsReloadZombiesAndVoicesAcrossPeers.txt).
- **Host + client:**
  - Client (Sage cầm Warden) nghe đủ 10 mốc nạp đạn của chính mình.
  - Host nghe cùng 10 mốc đó ở dạng 3D, cùng tiếng rút súng và tiếng bắn góc nhìn thứ ba.
- **Test đã sửa theo thay đổi có chủ đích:**
  - Voice bị cướp chỗ giờ tắt dần thay vì dừng ngay.
  - Số sự kiện giảm từ 30 xuống 26 vì đã gỡ 7 sự kiện CC0 và thêm 3 sự kiện Bucky.

## Cách làm video

- **Công cụ ghi:** `AvRecorder.BeginVideo` ghi từng khung Game view thành JPEG qua `AsyncGPUReadback`. Âm thanh lấy thẳng từ đầu ra của AudioListener (`OnAudioFilterRead`).
- **Kịch bản quay:** test `RecordGunVideo` dùng Editor làm host và bản peer làm đồng đội. Trước khi quay có một lần nạp đạn khởi động, vì lần nạp đạn đầu tiên trong phiên bị khựng khoảng 0,27 s.
- **Dựng video:** Blender dựng MP4 (H.264 + AAC) bằng [make_video.py](Audio-2026-11-15/scripts/make_video.py). Âm lượng nâng đều ×3,08 cho mọi đoạn, vì bản thu có đỉnh 0,31; tương quan to nhỏ giữa các súng giữ nguyên.
- **Hạn chế:**
  - Editor chỉ đạt khoảng 23 khung/giây.
  - Độ lệch hình–tiếng trong video ước khoảng ±1 khung [INFERENCE].
  - Odin và Operator có vài khựng 0,08–0,13 s trong lúc quay.

## Sửa lệch hình–tiếng trong video (sau phản hồi người dùng)

Người dùng nhận xét: tiếng trong cả video nhanh hơn animation một chút.

**Kiểm tra** [PLAYTEST]:
- Ghi tiếng theo đồng hồ DSP cho thấy bản thu đủ: các khối âm thanh nối liền, không có khoảng trống hay chồng lấn.
- Đồng hồ DSP và đồng hồ thực chênh dưới 30 ms trên mỗi đoạn quay.

Ví dụ thống kê của một đoạn:

```
classic blocks=328 gapMs=0 overlapMs=0 dspSpan=6.997 realSpan=6.978
vandal blocks=378 gapMs=0 overlapMs=0 dspSpan=8.064 realSpan=8.071
```

**Nguyên nhân** nằm ở dấu thời gian của hình, nên trong video hình đi sau tiếng khoảng 40–60 ms [INFERENCE]:
- Mỗi khung bị gắn mốc lúc đọc lại ở cuối khung, trễ thêm khoảng một khung (khoảng 43 ms ở 23 khung/giây).
- Khi dựng video, khung lại bị làm tròn về sau, trễ trung bình khoảng 17 ms.

**Cách sửa:**
- `AvRecorder` gắn mốc khung bằng thời điểm đầu khung, chạy trước mọi `Update` khác.
- Âm thanh được đặt theo đúng thời điểm DSP của từng khối (`AudioTap`).
- `make_video.py` chọn khung gần nhất thay vì khung trước đó.

**Kết quả:**
- Đã quay lại và dựng thành `SungMoi_6khau_v2.mp4`. Đối chiếu khung Vandal: chữ "MagIn End" xuất hiện đúng lúc súng giật lên khi băng vào khớp.
- Mốc tiếng trong game không đổi. Khi chơi thật, độ trễ đầu ra của card âm thanh còn làm tiếng đến muộn hơn hình, chứ không sớm hơn [INFERENCE].

## Chưa chắc chắn

- Mốc rút súng của Odin: chưa có dữ liệu rõ lúc kéo khoá nòng, hiện chỉ đặt theo vị trí tay.
- Thứ tự hai tiếng lắp băng `Magazine_In_A` và `Magazine_In_B` của Classic.
- Khoảng 0,05 s sớm hay muộn của tiếng nhét đạn Bucky: tiếng cạch hiện rơi sau lúc đạn vào ống khoảng 0,04 s.
- Bản nghe từ xa của 5 khẩu (trừ Warden) là bản lọc giả lập, không phải bản thu thật.

## Skill đã đọc và quy tắc áp dụng

- `audio-design` (SKILL.md, đọc trong lượt này sau khi đã gắn xong), đã đối chiếu lại các quy tắc:
  - "Route every source": mọi tiếng thao tác đi qua bus Weapons [CODE Assets/FPS/Features/Weapons/Runtime/WeaponAudio.cs:139].
  - "Leave headroom": tiếng bắn đỉnh −1 dBFS; bản thu thật không vượt 0,31.
  - "Vary repeated SFX": pitch 0,97–1,03 và jitter 0,8 dB; Warden 4 take; Bucky 2 take nhét đạn.
  - "Verify on real output": video lấy âm từ đầu ra thật của game.
- Không đọc `game-feel` trong lượt này.

## Tệp thay đổi trong lượt này (chưa commit)

- **Runtime:**
  - `WeaponData.cs`: thêm `WeaponSoundCue` và 3 danh sách mốc; bỏ `cycleSound` và `cycleDelay`.
  - `WeaponAudio.cs`.
  - `Weapon.cs`: thêm một dòng hook khi rút súng.
  - `SoundEvent.cs`: thêm `remoteClips`.
  - `GameAudioHost.cs`: thêm tắt dần khi cướp voice.
  - `GameAudio.cs`: clip lẻ được đếm theo tên clip.
- **Editor:**
  - `GameAudioSetup.cs`: tiếng súng giờ dựng theo bảng.
  - `WeaponFoley.json` (mới).
  - Công cụ tạm `ReloadMotionDump.cs` đã xoá sau khi dùng.
- **Nội dung:**
  - `Audio/Content/Weapons/<Súng>/Fire` và `Handling`: 26 file bắn và 76 file thao tác.
  - 6 asset súng.
  - Sự kiện `Weapon_<Súng>_Fire` và 3 sự kiện Bucky.
  - `Weapons/SOURCE.md`.
- **Test:**
  - `AudioContentTests`, `GameAudioPlayTests`, `AudioHostClientTests`.
  - `AudioVisualCapture`: thêm `RecordGunVideo`.
  - `AvRecorder`: thêm chế độ video và `AudioTap`.
- **Bản build:** `Builds/CampaignPeer` đã build lại.
- **Không đụng tới:** scene, flow nhiệm vụ, UI.

## Đề xuất (chưa làm)

- ~~Xoá các file CC0 cũ không còn dùng~~: người dùng duyệt "Xoá đi"; đã chuyển 74 file WAV và `.meta` vào Thùng rác Windows (không có tham chiếu nào trước khi xoá; `AudioContentTests` 5/5 qua sau khi xoá). Giữ `Shared/dry_fire_*` vì vẫn đang dùng.
- Thêm tiếng khi xem súng (Inspect): nguồn có sẵn `*_Inspect_*`, nhưng game chưa có hook cho thao tác này.
