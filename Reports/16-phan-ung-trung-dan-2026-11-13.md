# Phản ứng trúng đạn theo bộ phận – 2026-11-13

Phê duyệt của người dùng:
1. Yêu cầu gốc: "Không có phản ứng khi trúng đạn theo từng bộ phận của quái".
2. "Sang hit reaction".
3. Chọn qua câu hỏi: phạm vi **"Hình + gameplay nhẹ"**; máu **"Dùng BloodSpray có sẵn luôn, có thể thì lôi nó rồi đưa vào thư mục chính của code tôi"**.

Trạng thái: đã cài đặt; test Editor, host (offline/authority) và host + client đều qua. Chưa commit.

Skill đã đọc: `game-feel` (phản hồi trúng đòn phải đọc được, giật nhanh rồi hồi dần, không khoá cứng nhân vật), `fps-shooter` (vùng trúng/hệ số sát thương giữ nguyên, server quyết định), `unity-skills` (thao tác Editor qua REST). Quy tắc AGENTS.md áp dụng: server quyết định, client chỉ trình diễn; test cả host và host + client; ghi nguồn asset.

## Hiện trạng trước khi sửa

- Quái đã có hitbox theo vùng `[SCENE prefab]`: zombie thường có Head, Chest, Body (capsule gốc), 2 Leg; Infector có 4 Arm (2 tay, 2 gốc xúc tu); Tank có 2 Arm, 2 Leg; Screamer chỉ Head, Chest.
- Server biết chính xác vùng trúng, điểm trúng, hướng bắn: `EnemyHealth.OnDamageApplied` phát `DamageInfo` đầy đủ `[CODE EnemyHealth.cs:166]`. Chỉ `SI_Tank` dùng nó (stagger riêng).
- Không có gì trình diễn: không clip giật nào trong các Animator (chỉ Locomotion/Attack/Death), `Weapon.SpawnSurfaceImpact` bỏ qua hitbox nên không có máu trên người `[CODE Weapon.cs:803]`.
- Rig lẫn lộn: Mixamo (Copzombie, Zombiegirl), UE (ZombieMale), Rigify (ZombieRig), Bip01 (Tank), Character1 (Infector). Hitbox nằm ở object con ngay dưới xương `[SCENE Tools/FPS/Hit Reaction/Report Enemy Rigs]`.

## Thiết kế

### Server quyết định (gameplay)

`[CODE EnemyHitReactionRules.cs]`, `[CODE EnemyAI.HitReaction.cs]`

- Gom mọi phát trúng trong một loạt (mọi viên của một phát shotgun, hoặc đạn liên thanh trong 0,1 s) thành **một** phản ứng, xét theo tổng sát thương, vùng của viên mạnh nhất.
- Chỉ đạn và nổ gây phản ứng. Cận chiến, lửa cháy (Environment) không.
- "Nặng" = loạt ≥ 25% máu tối đa, hoặc headshot ≥ 15%.

| Tình huống | Kết quả |
|---|---|
| Trúng nhẹ thân/đầu/tay | Giật (chỉ hình ảnh) |
| Trúng chân, bất kỳ | **Khựng** 0,4 s: đứng lại, mất đường đi, rồi đuổi tiếp |
| Nặng, không đang vồ | **Khựng** 0,4 s |
| Nặng, đang vung đòn vồ chưa trúng | **Ngắt đòn**: đòn không gây sát thương, giật lùi 0,5 s, về Locomotion |
| Nhẹ khi đang vung | Giật, đòn vẫn trúng (đòn đã ra không bị chân nhẹ cắt ngang) |
| Trong 1,5 s sau một lần Khựng/Ngắt | Chỉ giật. Bắn liên thanh không thể khoá cứng quái |
| Quái có não riêng (Tank, Screamer, Infector) hoặc đang dùng kỹ năng đặc biệt | Chỉ giật. Tank giữ stagger riêng |

### Đồng bộ

`[CODE EnemyReplicatedState.cs]` thêm 8 byte vào trạng thái sẵn có của quái: số thứ tự lần trúng, loại phản ứng, chỉ số hitbox, điểm trúng (cm, theo hệ toạ độ hitbox), hướng đạn (256 bước), lực. Không thêm RPC, không thêm NetworkBehaviour. Client trình diễn khi số thứ tự đổi; khi vừa vào trận hoặc quái lấy lại từ pool thì không phát lại phát trúng cũ.

### Trình diễn (mọi máy, kể cả host)

`[CODE EnemyHitReactionPresenter.cs]`

- Sau khi Animator tạo dáng, bẻ xương mang hitbox (và 2 xương cha, mỗi cấp 45%) ra xa hướng đạn, lên đỉnh trong 0,05 s rồi hồi dần 0,28–0,5 s. Góc 8°–26° theo lực, đầu ×1,25, tay ×1,1, chân/ngực ×0,9.
- Trúng hitbox Body (capsule ở gốc, không có xương) thì bẻ xương ngực.
- Ngắt đòn: bẻ thêm cả thân trên.
- Không cần clip mới, chạy cho mọi rig.
- Máu: một nhịp BloodSpray tại điểm trúng, phun ra khỏi vết thương, ngả về phía người bắn. Tối đa 24 tia trên toàn màn hình, mỗi tia tự huỷ sau 2,5 s.

### Asset máu

Theo yêu cầu, chuyển `BloodSprayEffect` cùng material và texture nó dùng từ `Assets/ThirdParty/UnityTechnologies/EffectExamples/Blood/` sang `Assets/FPS/Features/Characters/Content/Effects/Blood/` bằng `AssetDatabase.MoveAsset` (giữ GUID). Nguồn và giấy phép ghi ở `SOURCE.md` cùng thư mục. Đính chính: trong câu hỏi tôi nói gói này dùng "Unity Companion License"; thực tế gói không kèm file giấy phép, readme chỉ ghi các asset mẫu "can be used in your games" `[INFERENCE]`.

`BloodStreamEffect` và phần còn lại của gói vẫn ở ThirdParty.

## File thay đổi

| File | Việc |
|---|---|
| `Assets/FPS/Features/Networking/Runtime/EnemyReplicatedState.cs` | `EnemyHitReactionKind`, 8 trường trúng đạn |
| `Assets/FPS/Features/Characters/Enemies/Runtime/EnemyAI.cs` | `partial`, đăng ký `OnDamageApplied`, gọi `ResolvePendingHit` trước não, ghi trường trúng vào trạng thái, reset khi về pool |
| `.../EnemyAI.HitReaction.cs` (mới) | Gom loạt, quyết định, khựng/ngắt, tìm hitbox, trình diễn |
| `.../EnemyHitReactionRules.cs` (mới) | Luật thuần, ngưỡng, lượng tử hoá |
| `.../EnemyHitReactionPresenter.cs` (mới) | Bẻ xương, máu, bộ đếm chẩn đoán |
| 8 prefab quái | Thêm `EnemyHitReactionPresenter` với blood prefab |
| `Assets/FPS/Features/Characters/Content/Effects/Blood/` | BloodSpray chuyển vào + `SOURCE.md` |
| `Assets/FPS/Features/Characters/Editor/EnemyHitReactionTools.cs` (mới) | Menu `Tools/FPS/Hit Reaction/…`: chuyển máu, gắn presenter, báo cáo rig |
| `Assets/FPS/Features/Missions/Runtime/CampaignPeerProbe.cs` | Báo số phản ứng/máu client đã trình diễn |
| `Assets/Tests/HitReaction/` (mới) | Test EditMode, PlayMode, host + client, chụp ảnh duyệt (commit "update" trước đó đã xoá cả `Assets/Tests`; giữ hay bỏ thư mục này là tuỳ bạn) |

## Kiểm chứng

Unity 6000.5.6f1, chạy qua Unity-Skills `test_run_by_name`. Scene `MainMenu` không dirty trước và sau, đã `File/Save Project`.

| Bộ test | Kết quả | Nội dung |
|---|---|---|
| `EnemyHitReactionRulesTests` (EditMode) | **15/15** (job d5302aa7) | Bảng luật ở trên, cooldown, quái não riêng, loại sát thương, lực, lượng tử hoá hướng/offset, đường cong giật, serialize `EnemyReplicatedState` |
| `EnemyHitReactionPlayTests` (PlayMode, prefab thật, máy này là server) | **4/4** (job 0106ae45) | ZombieRig + Copzombie: trúng chân → Khựng, khoá di chuyển 0,4 s rồi nhả, xương chân bẻ đỉnh **17,1°** rồi về < 0,5°, máu tại điểm trúng (< 0,3 m), một nhịp. 8 viên shotgun cùng frame = 1 phản ứng. Trong cooldown chỉ giật. Về pool thì reset. Đòn vồ bị ngắt: mục tiêu nhận **0** sát thương, sau khi hồi quái đánh lại trúng |
| `EnemyHitReactionHostClientTests` (Editor host + Development Player client, 2 tiến trình) | **1/1** (job 49e57835) | Xem log dưới |

Log host + client `[PLAYTEST Reports/HitReaction-2026-11-13/RemoteClientPresentsTheServersHitOnTheSameBoneWithBlood.txt]`:

```
[1.1s] connected 0
[5.7s] connected 1
[26.8s] GameScene loaded: completed=[0,1] timedOut=[]
[60.0s] zombie spawned at (-124.00, 0.05, -132.00) hp=100
[62.1s] server leg: kind=Stumble bone=shin.L hostBlood=1
[62.2s] client leg: kind=Stumble bone=shin.L blood=1
[63.9s] server chest: kind=Stumble bone=chest
[64.0s] client chest: kind=Stumble bone=chest blood=2
```

Client nhận và trình diễn khoảng 0,1 s sau server, cùng loại phản ứng, cùng xương, có máu.

Ảnh duyệt `[PLAYTEST Reports/HitReaction-2026-11-13/01..04-*.png]` (ZombieRig, clip đứng yên, chụp ở đỉnh giật): tư thế nghỉ; trúng chân (cẳng chân gập, chấm máu ở ống chân); trúng ngực nặng (thân trên ngửa ra sau, máu ở ngực); trúng đầu (đầu/cổ ngửa). Người dùng nghiệm thu 2026-11-14 ("Ok nghiệm thu, sang tiếp"); ảnh đã xoá theo AGENTS.md.

Sự cố trong lúc test (không phải lỗi code):
- Một lần chạy bị kẹt hàng đợi rồi chạy chồng lên lần sau → "Playmode tests were aborted". Chạy lại đơn lẻ thì qua.
- REST Unity-Skills tự khởi động lại sang cổng 8091 vì 8090 bị chiếm.
- Peer client được build lại (`Builds/CampaignPeer`, Succeeded, 0 lỗi) vì `EnemyReplicatedState` đổi cấu trúc: client cũ và mới **không** chơi chung được.

## Còn lại / đề xuất (chưa làm)

- Owner bắn thấy máu sau một vòng mạng (host thấy ngay). Nếu muốn máu tức thì phía người bắn thì cần dự đoán phía client trong `Weapon` — ngoài phạm vi.
- Death/Downed (mục 4) và âm thanh trúng thịt (mục 2) chưa làm.
