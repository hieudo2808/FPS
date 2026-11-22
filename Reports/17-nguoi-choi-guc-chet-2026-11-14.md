# Người chơi: Gục và Chết – 2026-11-14

Phê duyệt và chỉ đạo của người dùng:
1. Yêu cầu gốc: "Death/Downed anim và cơ chế chưa hoàn thiện".
2. "Death/Downed ở đây là nói người chơi ấy, có các anim Death rồi, bạn xem xem mà áp, với làm anim khi nằm xuống (Down)".
3. Chọn qua câu hỏi: góc nhìn khi gục là **"Gục nhưng vẫn bắn được Classic khẩu lục ấy"**.
4. Death là **"dạng Valorant, chết luôn ấy"**, dùng bộ clip người dùng thêm ở `Assets/FPS/Features/Characters/Animation/Content/3P/Death`.
5. Gục: **"trong anim Death có thể capture được transform lúc nằm nửa là được … cho nằm ngửa rồi tay vẫn bắn hướng khác thôi, như l4d2 ấy"**. Bản pose tự dựng trước đó bị từ chối ("gập méo hết cả chân") nên đã xoá.

Trạng thái: **người dùng nghiệm thu 2026-11-14** ("Nghiệm thu, sang tiếp"). Chưa commit.

Skill đã đọc: `unity-animation` (Humanoid, PlayableGraph), `fps-shooter` (server quyết định bắn), `game-feel`, `unity-skills`. Quy tắc AGENTS.md áp dụng: server quyết định, client chỉ trình diễn; test cả host và host + client; ghi nguồn asset.

## Luồng trạng thái (không đổi luật, chỉ thêm trình diễn và khẩu lục khi gục)

```
Alive ──(hết máu, còn người đứng)──► Downed ──(được cứu)──► Alive
  │                                    └─(hết 30 s / bị đánh tiếp)──► Dead ──► Spectating
  └──(hết máu, không còn ai đứng)───────────────────────────────────► Dead ──► Spectating
```
`[CODE PlayerHealth.cs ApplyDamage]`, `[CODE CampaignMissionController.cs:65 ActivePlayerCount]`

| Chuyển | Góc thứ ba (người khác nhìn) | Góc thứ nhất (chính mình) |
|---|---|---|
| Alive → Downed | Clip `Leg_Back_LG` (khuỵu, ngã ngửa gần chỗ đứng), rồi dáng gục dựng trong không gian muscle: nằm ngửa phẳng, chân trái duỗi, gối phải co, cột sống gập để nhổm vai, hai tay chĩa súng theo hướng nhìn (cung 100°), thân nằm yên khi người chơi xoay | Mắt ở chỗ đầu đang nằm (cao 0,45 m, lùi 0,55 m), đầu nghiêng 10°, hơi ngước, thở; quay tối đa ±60° quanh hướng lúc ngã, ngước −55° / cúi 20°; Classic tự rút ra |
| Downed → Alive | Blend về Animator thường trong 0,45 s | Camera nâng lên, trả lại súng đang cầm trước khi gục |
| Downed → Dead | Hạ vai, buông tay, nằm hẳn | Spectator như cũ |
| Alive → Dead | Clip Death theo **vùng trúng × phía bị đánh × mức** (Valorant), giữ khung cuối | Spectator như cũ |

## Bộ clip Death (của người dùng)

- 72 FBX (36 clip + 36 bản `_Montage` giống hệt, không dùng). Import gốc là **Generic**, gốc cây xương tên `Core_Skeleton.001`, còn model người chơi là `CS_*_Skelmesh.ao`, nên bind theo đường dẫn khớp 0/122 `[PLAYTEST log]`.
- Đã đổi import sang **Humanoid**: avatar tạo từ chính file, dùng ánh xạ xương và tư thế gốc của `TP_Core_IdlePose`, kèm thêm tầng `Core_Skeleton.001`. Kết quả 72/72 avatar hợp lệ `[CODE PlayerLifeClipImport.cs ImportDeathSet]`. **Đây là thay đổi import trên asset bạn thêm vào.** Root bị khoá (bake into pose), để thân ngã trong pose còn vị trí mạng của người chơi đứng yên.
- Hệ hướng giống clip TP_Core đang chạy trong game. Tên clip là **phía bị trúng**: `Front` thì ngã ngửa, `Back` thì ngã sấp, `Left` thì ngã sang phải.
- `PlayerLifeAnimationSet.asset`: 24 clip chết, 1 headshot, clip gục `Leg_Back_LG`. Ánh xạ: `Body`/`Belly`/`Lumbar` là trúng thân, `Head` là trúng đầu, `Leg` là trúng chân. `SM` khi phát cuối < 30 sát thương, `LG` khi ≥ 30 hoặc do nổ. Có nhiều clip khớp thì chọn theo một số biến thể do server gửi, nên mọi máy ra cùng một clip.
- Đã quét cả 29 clip để tìm khung "nằm nửa ngửa". **Không clip nào có**: các clip ngã ngửa nằm phẳng rất nhanh (góc thân −7° đến −18°). Vì vậy dáng gục được dựng bằng code trên khung đó `[PLAYTEST]`.

### Lần 2 – sửa chân méo (người dùng: "chân cứ bị méo các kiểu, không chấp nhận nổi")

- Nguyên nhân đo được: ở khung cuối clip, muscle chân bị ép sát/vượt biên (Upper Leg Twist −0,97, Lower Leg Twist 1,08, In-Out 0,68, Spine Front-Back 1,90), nên đùi và cẳng chân bị vặn. Cách bẻ cột sống cũ (xoay xương theo trục thế giới) không kiểm soát được độ xoắn.
- Cách mới `[CODE PlayerDownedPose.cs]`: tự đặt hông nằm ngửa phẳng (từ tư thế đứng trung tính của avatar, không lấy từ clip), đặt chân/cột sống/cổ bằng **muscle Humanoid** (luôn trong giới hạn khớp, xoắn = 0), giữ nguyên hông sau `SetHumanPose` để chân không bị nhấc. Góc khớp (đùi thẳng, đùi phải gập 45°, gối phải 95°) được **tự hiệu chuẩn** ra muscle cho từng avatar, vì muscle 0 không phải "chân thẳng".
### Lần 3 – trong game méo hơn bản offline (người dùng: "chân ở ảnh 3 đang đẹp thì bạn lại làm méo đi … upper body cũng bị hỏng animation hold súng")

- Nguyên nhân: trong game, Animator vẫn chạy controller của súng (`CloveClassic_GenericPathBound`). Controller đó điều khiển các **xương phụ** không thuộc Humanoid (xoắn đùi, cẳng tay, xương phụ hông/vai) theo tư thế đứng cầm súng; clip gục chỉ ghi xương Humanoid. Bản render offline không có controller nên sạch.
- Sửa `[CODE PlayerLifePresenter.cs CollectHelperBones/RestHelperBones]`: trong lúc clip sống/chết chạy, mọi xương phụ của thân (trừ xương súng và mô hình súng) được đưa về góc nghỉ lấy từ avatar, trước khi dựng dáng. Đo trong game: 194 xương phụ, **13** bị controller bẻ lệch > 5° mỗi frame, nay về nghỉ `[PLAYTEST host + client]`.

### Lần 4 – tay chưa ôm súng, gối bẹp (người dùng: "Tay thì bạn xem lại lúc đứng, anim 3P thì tay trông thế nào là rõ mà")

- Tay: bỏ cách tự bẻ tay (FromTo). Dùng đúng dáng cầm Classic lúc đứng của bộ 3P – clip Humanoid `TP_Core_BasePistol_S0_IdlePose_UB` (clip `Classic3P_Body.controller` đang dùng) – trộn bằng `AnimationLayerMixerPlayable` với mask chỉ hai tay và ngón tay. Sau đó xoay cả khối vai (`UpperChest`) cho nòng súng theo hướng ngắm, giữ hướng đầu `[CODE PlayerLifePresenter.cs ArmsMask, PlayerDownedPose.cs Apply]`.
- Gối: gối phải gập 95° làm da co rúm; giảm còn đùi 30°, gối 55°.
- Đo: offline 4 nhân vật – hai bàn tay cách 0,05–0,12 m, súng trùng hướng ngắm 0°, gối 56°; trong game – tay cách 0,19 m, súng lệch hướng nhìn 0° (ngẩng 7°), chân L 0,10 / R 0,19 m, gối 0,42 m `[PLAYTEST]`.

### Lần 5 – chép đúng dáng cầm súng 3P lúc đứng (người dùng: "Bạn capture lúc cầm lục ở third person trước đi", "xem lúc setup đứng thế nào mà tay chuẩn thì làm theo")

- Chụp dáng cầm Classic 3P lúc đứng của cả 4 nhân vật bằng đúng controller của họ (`BrimstoneClassic_GenericPathBound`, `CloveClassic_GenericPathBound`): cả 4 đều đúng – hai tay ôm súng, súng trong tay `[PLAYTEST Reports/PlayerLife-2026-11-14/standing-classic__*.png]`.
- Clip Humanoid `TP_Core_BasePistol` dùng ở lần 4 là bản retarget nên tay duỗi quá. Bỏ. Thay bằng `PlayerSidearmHoldPose.asset`: góc xoay cục bộ thật của 176–179 xương (từ xương đòn xuống tới đầu ngón, kể cả xương phụ ở tay) chụp từ dáng đứng đó, mỗi controller một bộ.
- Cách ngắm cũ (xoay cả `UpperChest`) làm gù vai, tay khuỳnh. Giờ chỉ xoay ở **khớp vai**, hai cánh tay cùng một góc, như người nằm hạ tay xuống ngắm; bàn tay và cách cầm giữ nguyên.
- Thân nhổm còn 24–25° (trước ~45–60°, trông như ngồi ghế).
- Xương phụ khi nằm giữ **góc mặc định của prefab** (ghi lúc Awake), không dùng góc nghỉ của avatar nữa.
- Đo trong game: đầu 0,51 m, tay cách 0,13 m, súng lệch hướng nhìn 0° (ngẩng 9°), chân L 0,08 / R 0,18 m, gối 0,41 m.
- Ghi chú lỗi khác, chưa sửa: khi server tự đổi súng cho người chơi khác (ví dụ lúc gục), dáng cầm 3P lúc đứng của họ trong game có lúc kẹt tay giơ che mặt, súng lơ lửng `[PLAYTEST 3P-standing-classic-front.png]`. Cần điều tra riêng.

- Chiều dấu nâng vai đo được: +1 làm đầu chìm xuống −0,30 m, −1 nâng đầu lên 0,61 m `[PLAYTEST PlayerDownedPoseTests]`.
- Tham khảo L4D2: người gục nằm ngửa, chỉ dùng súng ngắn, súng ngắn bị giảm chính xác/tốc độ ([Prima eGuide](https://primagames.com/eguides/left-4-dead-2-eguide/essentials/movement-and-combat/health-and-life), [L4D Wiki – Magnum](https://left4dead.fandom.com/wiki/Magnum_Pistol)); camera khi gục không nhìn ra sau được ([AlliedMods](https://forums.alliedmods.net/showthread.php?p=2675598)). Phần giảm chính xác chưa làm (đề xuất bên dưới).

## Thay đổi

| File | Việc |
|---|---|
| `Player/Runtime/PlayerHealth.cs` | `IsDownedConscious`, `CanUseWeapon(WeaponData)`, biến mạng `networkLifeHit` (1 byte: vùng/phía/mức/biến thể) ghi trước khi đổi trạng thái; `TakeDamage(DamageInfo)` truyền vùng và loại sát thương |
| `Player/Runtime/PlayerLifeHit.cs` (mới) | Đóng gói phát trúng chí mạng, tính phía bị đánh theo hướng nhìn của nạn nhân |
| `Player/Runtime/PlayerLifeAnimationSet.cs` (mới) + `.asset` | Bộ clip và luật chọn |
| `Player/Runtime/PlayerLifePresenter.cs` (mới) | Trình diễn 3P bằng PlayableGraph đè lên Animator nhân vật; giữ súng 3P trong tay phải; hạ camera 1P |
| `Player/Runtime/PlayerDownedPose.cs` (mới) | Dáng gục trong không gian muscle (hông phẳng, chân hiệu chuẩn, nhổm vai) + hai tay chĩa súng, chạy sau Animator |
| `Player/Runtime/MouseMovement.cs` | `SetLookConstraint` (giới hạn quay/ngước khi gục), `SetLookOffset` (nghiêng đầu, thở) |
| `Player/Runtime/PlayerVisibilityController.cs` | `SuspendCharacterProfile`: không đổi controller/avatar theo súng trong lúc clip sống/chết đang chạy |
| `Player/Runtime/PlayerMovement.cs` | Khi gục vẫn gửi hướng nhìn (di chuyển = 0) để server có hướng ngắm |
| `Weapons/Runtime/WeaponData.cs`, `Classic.asset` | Cờ `usableWhileDowned`, chỉ Classic bật |
| `Weapons/Runtime/WeaponManager.cs` | Server: gục thì rút súng có cờ; được cứu thì trả súng cũ; khi gục không đổi súng được |
| `Weapons/Runtime/WeaponFireHandler.cs`, `Weapon.cs` | Quyền bắn/nạp dùng `CanUseWeapon(súng đang cầm)` |
| 4 prefab người chơi | Thêm `PlayerLifePresenter` (+20 dòng mỗi file) |
| `Characters/Editor/PlayerLifeClipImport.cs` (mới) | Menu `Tools/FPS/Player Life/…`: import Humanoid, dựng bộ clip, gắn presenter, báo cáo bind avatar |
| `Missions/Runtime/CampaignPeerProbe.cs` | Client báo trạng thái gục và những gì nó thấy của người kia |
| `Assets/Tests/PlayerLife/` (mới) | Test EditMode, PlayMode, host + client |

## Lỗi phát hiện khi chạy trong game thật và cách sửa

1. **3P vẫn đứng dù clip đang chạy.** Avatar `HumanAvatar` lấy từ `Human.fbx` của Sage và Gekko **không bind** vào body trong prefab của chính họ (`GetBoneTransform(Head)` = null). `TP_Core_IdlePoseAvatar` thì bind được nhưng tính theo **centimet**, làm thân bị phóng 100 lần (đầu cách chân 179,5). Đã kiểm từng prefab với từng avatar `[PLAYTEST PlayerLifeAvatarScaleTests]`. Kết quả gán: Brimstone dùng `HumanAvatar` của mình; Clove, Gekko, Sage dùng `BodyAvatar` của Clove (bind đúng, cao 1,78 m).
2. **Thân nằm lệch 91°** so với hướng nhìn: lúc đo dáng nằm, output còn weight 0. Đã sửa, giờ lệch 0°.
3. **Súng 3P lơ lửng**: súng treo ở xương `R_WeaponMaster` do clip Generic điều khiển. Giờ mỗi frame xương đó bám theo bàn tay phải (cách 0,09 m).
4. `Rebind()` chạy trực tiếp trên prefab làm bẩn hàng trăm giá trị transform. Đã khôi phục prefab và chỉ thử bind trên instance tạm.

## Kiểm chứng

Unity 6000.5.6f1, Unity-Skills `test_run_by_name`. Scene `MainMenu` không dirty trước và sau, đã `File/Save Project`.

| Bộ test | Kết quả |
|---|---|
| `PlayerLifeRulesTests` (EditMode) | **9/9**: đóng gói byte, phía bị đánh (kể cả khi nạn nhân quay hướng khác), mọi tổ hợp vùng × phía × mức ra đúng clip và giống nhau trên mọi máy, chỉ Classic dùng được khi gục |
| `PlayerLifeAvatarScaleTests` (PlayMode, 4 prefab thật) | **1/1**: avatar đã gán bind được, nằm xuống đúng cỡ người |
| `PlayerDownedPoseTests` (PlayMode, 4 prefab thật) | **1/1**: mọi nhân vật – bàn chân 0,11 m (chạm đất), gối phải 0,53 m, xoắn chân 0,00, chân lệch hướng nhìn 1°, tay lệch 7°, đầu 0,58–0,61 m |
| `PlayerLifeOutputOrderTests` (PlayMode) | **4/4**: clip đè được AnimatorController 3P, cả sau `Rebind()` |
| `PlayerLifeHostClientTests` (Editor host + Development Player client) | **1/1**, log dưới |
| Hồi quy: `EnemyHitReactionRulesTests` 15/15, `EnemyHitReactionPlayTests` 4/4, `EnemyHitReactionHostClientTests` 1/1 | Qua |

Log host + client `[PLAYTEST Reports/PlayerLife-2026-11-14/DownedSidearmReviveAndDirectionalDeathAcrossPeers.txt]`:

```
client holds slot 0 (Warden), sidearm slot 1
client own view: life=1 held=Classic sidearmUsable=True primaryUsable=False eyeDrop=1.04
host view of client: clip=…TP_Core_Death_Leg_Back_LG raise=0.96 arms=0.96
host view of client body: head 0.44 m, hips 0.19 m above the capsule base
diag: feet vs facing 0 deg, right forearm vs facing 9 deg, upper arm 6 deg, hand height 0.33
diag: 3P weapon Classic_3P hand gap 0.09 m
revived: host overriding=False slot=0 client eyeDrop=0.00
client view of host: life=1 clip=…TP_Core_Death_Leg_Back_LG raise=1.00
death: hit=Head/Back/LG/v2 clip=…TP_Core_Death_Head_Back_LG
host view of dead client: head 0.23 m
```

Log host + client lần cuối (sau khi sửa chân và 1P): `host view of client legs: feet L 0.09 R 0.10 m, right knee 0.52 m`, `head 0.57 m`, `feet vs facing 0 deg`, `right forearm vs facing 9 deg`, `hand gap 0.09 m`, client `eyeDrop=1.00`.

Ảnh duyệt trong GameScene `[PLAYTEST Reports/PlayerLife-2026-11-14/*.png]`: `3P-downed-front`, `3P-downed-side`, `3P-downed-closeup`, `1P-downed-host-view`, `3P-dead-head-back`. Ảnh duyệt đã xoá sau khi nghiệm thu (AGENTS.md).

## Chưa làm / đề xuất

- Blender MCP người dùng mở không kết nối được vào phiên này ("Connection closed" lúc khởi động); toàn bộ dáng được dựng trong Unity.
- Đề xuất theo L4D2: giảm độ chính xác / tốc độ bắn / tốc độ nạp của Classic khi gục (thay đổi cân bằng, chờ duyệt).

- Chưa bắn thật bằng chuột khi đang gục trong một phiên chơi tay. Test chỉ kiểm quyền bắn ở server và client, cùng việc rút Classic.
- Khi gục, người khác chỉ thấy tay chĩa theo hướng ngang, vì góc ngẩng/cúi (pitch) không được đồng bộ cho 3P.
- Chết do đang ở trên không chưa dùng bộ `InAir_*` và `Land_*`.
- Một khả năng [INFERENCE]: các presentation súng kiểu `AuthoredAvatar` của Sage/Gekko cũng dùng avatar không bind, nên dáng cầm súng 3P bình thường của họ có thể đang sai. Cần kiểm riêng, ngoài phạm vi lần này.
- Clip Death có thể đưa thân xa vị trí gốc tới 2–3 m nên có thể xuyên tường. Chưa xử lý.
