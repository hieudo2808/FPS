# Sửa ngõ cụt Tank campaign — 11/11/2026

Thực hiện theo yêu cầu trực tiếp: "tìm cách khắc phục hoàn toàn vấn đề trên, đừng để lỗi ingame". Phạm vi: logic đặt và chờ Tank của campaign phía server, chữ HUD của Tank, test. Không đổi AI của Tank, chỉ số, audio, save schema, catalog, scene hay prefab.

## Nguyên nhân đã xác nhận bằng scene thật

Trước đây chỉ là suy luận từ code. Bài PlayMode mới chạy trong GameScene với host thật, đặt người chơi tại ba nơi Tank thường được kích hoạt, và ghi lại:

```
FactoryGenerator: strict=False relaxed=True distance=15.0
FactoryShipping: strict=False relaxed=True distance=30.0
FactoryCase: strict=False relaxed=True distance=30.0
```

[PLAYTEST Reports/TankSpawnFix-2026-11-11/FactoryTankIsStagedAfterStrictPlacementTimesOut.txt]

Tức là với luật cũ, **không có anchor nào hợp lệ tại cả ba điểm**. Lỗi này không hiếm: trong fixture này nó xảy ra mọi lần. Luật cũ đòi anchor cách mọi người chơi tối thiểu 28 m, ngoài tầm nhìn, có đường NavMesh hoàn chỉnh, và trống bốn phía 1,5 m [CODE Assets/FPS/Features/AI/Runtime/DirectorSpawnService.cs:13] [CODE Assets/FPS/Features/AI/Runtime/DirectorSpawnService.cs:270]. Sau 20 giây không đạt, Factory vào trạng thái thất bại vĩnh viễn.

Giới hạn của bằng chứng: người chơi được dịch chuyển tới từng điểm, một người chơi, director đang ở pha Relax. Chưa đo với người chơi đi bộ tự nhiên hay nhiều người.

## Cách sửa: thang nới lỏng, không có trạng thái thất bại

Server không bao giờ dừng thử. Thời gian chờ không thành công được chia bậc theo `tankSpawnTimeoutSeconds` (mặc định 20 giây):

| Bậc | Thời gian | Hành vi |
|---|---|---|
| 0 | 0 đến 10 s | Luật cũ đầy đủ: ưu tiên chỗ khuất, xa, tôn trọng director và special khác |
| 1 | 10 đến 20 s | Như bậc 0, duyệt anchor theo thứ tự ngược (đã có sẵn, nay áp cho mọi chapter) |
| 2 | 20 đến 60 s | Nới lỏng: bỏ qua pha bảo vệ của director, special khác còn sống, khoảng cách 28 m và yêu cầu trống bốn phía. Chỉ giữ điều kiện vật lý: đủ chỗ cho thân Tank, có đường đi tới một người chơi còn sống, cách mọi người chơi ít nhất 10 m. Thử anchor trước, rồi các vòng 30, 22, 15, 10 m quanh từng người chơi, ưu tiên điểm không ai nhìn thấy |
| 3 | sau 60 s | Vẫn không thể tạo Tank (ví dụ thiếu prefab, pool hỏng): bỏ encounter, mở khóa Case, ghi cảnh báo vào log. Người chơi không thấy thông báo lỗi nào |

Đồng hồ vẫn dừng khi không còn người chơi điều khiển được hoặc đang trong thời gian đọc tài liệu, như trước.

[CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:121] `Tier`. [CODE Assets/FPS/Features/Missions/Runtime/CampaignTypes.cs:363] `SkipUnstageableTank`. [CODE Assets/FPS/Features/Missions/Runtime/CampaignMissionController.Tank.cs:43] vòng lặp. [CODE Assets/FPS/Features/AI/Runtime/DirectorSpawnService.cs:307] tìm vị trí nới lỏng. [CODE Assets/FPS/Features/Characters/Enemies/Runtime/SpecialInfectedRegistry.cs:287] spawn nới lỏng.

Thay đổi hành vi cần biết:

- Ở bậc 2, Tank có thể xuất hiện trong tầm nhìn nếu không còn chỗ khuất nào, và có thể xuất hiện khi một special khác còn sống.
- Bậc 3 ở Factory coi như Tank đã bị hạ để mở Case. Trước đây Northbridge và Laboratory đã bỏ qua Tank sau 20 giây; nay chúng cũng đi qua bậc 2 trước, nên Tank ở hai chapter đó sẽ xuất hiện thường hơn trước.
- Save cũ còn giữ trạng thái thất bại sẽ tự quay lại chờ khi nạp.

## Đã xóa

- `RequestRetryTank`, `CanRetryTank`, RPC retry, `CanRetryFactoryTank`, `RetryFactoryTank`, `TankSpawnFailed`: không còn đường nào dẫn tới.
- RPC "Tank unavailable. Safety override: continue to the lift."
- Toàn bộ chữ kỹ thuật trên tracker ("director protection is active", "spawn request rejected", "attempt budget", "Host: Retry Tank"). Người chơi chỉ còn thấy "Tank incoming" với "Stay alert. A Tank is closing in.", rồi "Eliminate the Tank". Câu chữ mới này chưa được duyệt.
- Đếm ngược budget không còn được replicate mỗi giây.

Giá trị enum `SpawnFailed` và các trường `tankWaitReason`, `tankFallback`, `tankSpawnRemaining` được giữ để không đổi schema checkpoint.

## Kiểm tra

| Kiểm tra | Job | Kết quả |
|---|---|---|
| Compile | debug_get_errors | 0 lỗi |
| CampaignTankStagingTests, PlayMode, GameScene thật | a7c8f176 | 3/3 pass, 192 s |
| CampaignLifecycleTests, PlayMode | b4d5734d | 5/5 pass, 60 s |
| CampaignRulesTests, EditMode | 1a8155f8 | 63/63 pass, 28 s |
| HudRegressionTests, EditMode | 783f90f1 | 19/19 pass, 37 s |

Log thật của ba bài mới:

```
after one timeout: stage=Active reason=None
relaxed tier with no prefab: stage=Pending reason=SpawnRejected
after three timeouts: stage=Resolved
```

Ba bài mới trong `Assets/Tests/PlayMode/CampaignTankStagingTests.cs`:

1. Sau một timeout, Tank thật được spawn, cách người chơi ít nhất 9 m, Case vẫn khóa cho tới khi hạ.
2. Gỡ prefab Tank khỏi registry (tiêm lỗi): bậc 2 vẫn chờ, sau ba timeout thì encounter được bỏ và Case mở.
3. Trạng thái thất bại từ save cũ tự quay về chờ.

Các lượt chạy trước khi đạt, ghi đúng thứ tự:

- Lượt đầu của CampaignTankStagingTests (job 19e1e7c3) 2/3: bài số 2 fail do fixture trừ thời gian ra số âm khi server mới chạy chưa tới 60 giây. Sửa fixture, không sửa runtime.
- CampaignLifecycleTests (job 0d404bdf) 4/5: `TankDespawnDoesNotCountAsKill_DeathUnlocksFactory` fail với `TargetParameterCountException` tại một dòng gắn hàm một tham số vào delegate không tham số. Dòng này không do thay đổi hôm nay gây ra; đã sửa đúng dòng đó trong test.

Trong `CampaignRulesTests`, bài `FactoryTankTimeoutRetryAndConfirmedDeathKeepTheTwoBranches` được viết lại phần retry thành kiểm bậc thang và `SkipUnstageableTank`.

Save Gate trước và sau mỗi lượt compile và test. [SCENE Assets/FPS/Scenes/MainMenu.unity] cuối phiên `isDirty=false`, `isPlaying=false`, `isCompiling=false`, Console 0 lỗi.

## Chưa kiểm được

- Chưa chơi tay từ đầu chapter tới Tank; test đặt state trực tiếp và tự gọi nhịp cập nhật.
- Chưa kiểm host cùng client hai tiến trình. Logic chỉ chạy trên server nên rủi ro thấp, nhưng chưa có bằng chứng. [INFERENCE]
- Chưa kiểm Northbridge và Laboratory trong scene; chúng dùng chung đường code.
- Chưa kiểm trường hợp nhiều người chơi tản xa nhau.
- Bậc 0 và 1 hiện không tìm được anchor tại ba điểm đã đo, nên trên thực tế Tank sẽ luôn tới sau khoảng 20 giây ở bậc 2. Muốn Tank tới sớm và khuất hơn thì cần thêm hoặc dời anchor trong scene Factory; đó là việc dựng level, chưa làm.

## Đề xuất

- Rà anchor trong scene Factory để bậc 0 có chỗ hợp lệ gần cold store, hoặc giảm thời gian chờ trước bậc 2 xuống dưới 20 giây.
- Duyệt câu chữ tracker của Tank.

## Skills đã đọc và quy tắc áp dụng

- `unity-skills`: test bất đồng bộ tuần tự theo jobId, Save Gate, readback scene.
- `level-design`: kiểm soft-lock trên critical path; gate phải luôn có đường thỏa.
- `fps-shooter` không được mở trong phiên này; thay đổi không chạm hành vi chiến đấu của Tank, chỉ chạm việc đặt vị trí và điều kiện chờ. Nếu cần chỉnh encounter thì phải đọc skill đó trước.
- AGENTS.md: server quyết định trạng thái Tank; client chỉ hiển thị; flow được người dùng yêu cầu trực tiếp.

## Bổ sung: rà anchor Factory và gốc rễ thật

Yêu cầu tiếp theo: "Rà anchor Factory". Đo bằng một bài PlayMode tạm trong GameScene thật (đã gỡ khỏi source sau khi đo); số liệu thô ở `Reports/TankSpawnFix-2026-11-11/AnchorAudit-before-route-fix.txt` và `AnchorAudit-after-route-fix.txt`.

### Anchor không phải thủ phạm

Factory có 58 anchor, đều thuộc chapter, khoảng cách tối thiểu 22 m, tối đa 110 m. [PLAYTEST Reports/TankSpawnFix-2026-11-11/AnchorAudit-before-route-fix.txt]

| Trạng thái tĩnh của anchor | Số lượng |
|---|---:|
| Đủ chỗ cho Tank | 39 |
| Không đủ khoảng trống bốn phía 1,5 m | 15 |
| Lệch khỏi NavMesh quá 0,5 m | 4 |

Trong 39 anchor đủ chỗ có hai anchor dựng riêng cho Tank: `Tank_ColdStorage_1` và `Tank_ColdStorage_2`, cách điểm Evidence Case 38 m và ngoài tầm nhìn. Vậy mà trước khi sửa, cả hai vẫn bị loại.

### Gốc rễ: bước kiểm đường đi không thể thỏa

Trước khi sửa, lý do loại áp đảo tại mọi điểm là "thân Tank không lọt đường tới người chơi": 17 anchor ở Generator, 13 ở Case, 11 ở Isolate. Đo dọc từng tuyến:

- Tank thật: NavMeshAgent bán kính 0,8 m, cao 2,7 m; collider thân bán kính 0,88 m.
- NavMesh của scene bake cho agent bán kính 0,5 m, cao 2 m.
- Bước kiểm quét một capsule bán kính 0,9 m dọc đường NavMesh. Đường đó hợp lệ khi cách tường 0,5 m, nên capsule 0,9 m chạm tường ở mọi góc và cổng.
- Mọi điểm chạm đo được là chạm ngang ở tầm thấp (tường, plinth, hàng rào); không có điểm nào chỉ vướng trần.

Ví dụ: tuyến từ `Tank_ColdStorage_1` tới Case dài 39 m, bị chặn 6/85 mẫu, tất cả bởi vách `Hangar_v6_1 (2)` ngay tại cửa.

Tank vẫn di chuyển được trên các tuyến này trong game, vì agent đi theo NavMesh; nó chỉ lấn vào tường khoảng 0,4 m ở góc. Bước kiểm vì thế khắt khe hơn chính hành vi của Tank.

### Sửa

- Khi quét đường cho Tank, dùng bán kính mà NavMesh bảo đảm (bán kính agent của NavMesh trừ 0,05 m), giữ nguyên chiều cao 2,8 m để vẫn kiểm trần. Vị trí spawn vẫn phải đủ chỗ cho toàn thân. Chỉ áp cho Tank; special khác không đổi. [CODE Assets/FPS/Features/AI/Runtime/DirectorSpawnService.cs:256]
- Anchor loại Special được thử trước anchor thường, để Tank ưu tiên điểm phục kích đã dựng. [CODE Assets/FPS/Features/AI/Runtime/DirectorSpawnService.cs:354]

### Kết quả sau sửa

| Vị trí người chơi | Anchor hợp lệ theo luật đầy đủ, trước | Sau |
|---|---:|---:|
| Evidence Case | 0 | 8, gồm cả hai anchor cold storage |
| Shipping | 0 | 3 |
| Manifest | 0 | 2 |
| Generator | 0 | 0 |
| Isolate | 0 | 0 |

Ở khu Power (Generator, Isolate) vẫn chưa có anchor hợp lệ: các tuyến tới đó đi sát lan can và cột (`Rail_*`, `Post_*`) tới mức capsule hẹp cũng chạm. Tại đó Tank sẽ tới qua bậc nới lỏng sau 20 giây, hoặc sớm hơn nếu người chơi đi về phía cold storage.

Chạy lại sau sửa:

| Kiểm tra | Job | Kết quả |
|---|---|---|
| CampaignTankStagingTests, PlayMode | 6f0836c1 | 3/3 pass, 191 s |
| CampaignLifecycleTests, PlayMode | c4bd86e5 | 5/5 pass, 38 s |
| CampaignRulesTests, EditMode | b44bfba3 | 63/63 pass, 18 s |

```
FactoryGenerator: strict=False relaxed=True distance=39.6
FactoryShipping: strict=True relaxed=True distance=61.7
FactoryCase: strict=True relaxed=True distance=81.4
after one timeout: stage=Active reason=None
```

[SCENE Assets/FPS/Scenes/MainMenu.unity] cuối phiên `isDirty=false`, Console 0 lỗi. Không sửa scene hay prefab.

### Còn mở

- Tank lấn tường ở góc là hệ quả của việc dùng chung NavMesh bán kính 0,5 m. Sửa triệt để là bake thêm một NavMesh cho agent cỡ Tank; đó là việc dựng level trong GameScene, chưa làm.
- 4 anchor lệch NavMesh và 15 anchor thiếu khoảng trống không ảnh hưởng zombie thường nhưng vô dụng với Tank; có thể dời nếu muốn thêm lựa chọn.
- Khu Power chưa có điểm phục kích hợp lệ; thêm một anchor Special gần đó sẽ cho Tank tới ngay thay vì sau 20 giây.
- HudRegressionTests và các bài PlayMode khác chưa chạy lại sau thay đổi cuối này. Chưa đo Northbridge và Laboratory.

## Bổ sung 2: yêu cầu "Thêm anchor Power" — không thêm, vì tìm ra lỗi thứ hai

Yêu cầu: thêm một anchor Special gần khu Power. Trước khi đặt, đo 635 điểm lưới quanh khu Power (đủ chỗ cho Tank, trong zone cho phép spawn, cách ba bước Power ít nhất 24 m): **cả 635 đều bị loại vì đường vào**, không điểm nào bị loại vì tầm nhìn. Thêm anchor ở bất kỳ đâu cũng sẽ bị loại như vậy. [PLAYTEST Reports/TankSpawnFix-2026-11-11/PowerRoute-before-stairs-fix.txt]

### Lỗi thứ hai trong bước kiểm đường đi: cầu thang

Khu Power nằm trên bệ cao, phải lên bằng bậc thang. NavMesh chỉ đặt góc ở chân và đỉnh cầu thang; bước kiểm nối thẳng hai góc rồi quét capsule dọc đường thẳng đó, nên capsule cắt qua từng bậc. Log đo cho thấy các vật chặn là `Tread_0` tới `Tread_11`, tức chính các bậc thang, cộng một mép tường thấp mà đường thẳng nội suy cắt qua.

Sửa, chỉ áp cho Tank: mỗi điểm mẫu được đưa về đúng mặt NavMesh, và capsule bỏ qua phần thấp hơn chiều cao bước mà NavMesh cho phép (0,75 m). [CODE Assets/FPS/Features/AI/Runtime/DirectorSpawnService.cs:260]

Ngoài ra, trong mỗi nhóm (Special trước, thường sau) anchor gần người chơi nhất được thử trước, thay vì theo thứ tự danh sách. Trước đó Tank chọn một anchor cách 85 m trong khi anchor cold storage cách 51 m cũng hợp lệ. [CODE Assets/FPS/Features/AI/Runtime/DirectorSpawnService.cs:374]

### Kết quả

| Vị trí người chơi | Luật đầy đủ tìm được chỗ, ban đầu | Sau sửa bề rộng | Sau sửa cầu thang |
|---|---|---|---|
| Generator | Không | Không | Có, 16 anchor; chọn anchor cold storage cách 51 m |
| Shipping | Không | Có | Có, cách 62 m |
| Evidence Case | Không | Có | Có, anchor cold storage cách 38 m |

[PLAYTEST Reports/TankSpawnFix-2026-11-11/PowerRoute-after-stairs-fix.txt]

Vì Generator đã có 16 anchor hợp lệ, **không thêm anchor nào vào scene**. Scene và prefab không bị sửa. Tại vị trí Isolate và Backup vẫn chưa có anchor hợp lệ, nhưng Tank chỉ được kích hoạt sau khi xong Generator hoặc Shipping, nên hai vị trí đó không phải nơi Tank cần xuất hiện; chưa điều tra vật chặn ở đó.

Chạy lại sau thay đổi cuối:

| Kiểm tra | Job | Kết quả |
|---|---|---|
| CampaignTankStagingTests, PlayMode | 036ce69f | 3/3 pass, 184 s |
| CampaignLifecycleTests, PlayMode | 8a0936b2 | 5/5 pass, 38 s |
| CampaignRulesTests, EditMode | aae99a1f | 63/63 pass, 18 s |
| HudRegressionTests, EditMode | d8a6262c | 19/19 pass, 31 s |

```
FactoryGenerator: strict=True strictDistance=50.7 relaxed=True distance=57.5
FactoryShipping: strict=True strictDistance=61.7 relaxed=True distance=120.3
FactoryCase: strict=True strictDistance=38.0 relaxed=True distance=81.4
after one timeout: stage=Active reason=None
```

[SCENE Assets/FPS/Scenes/MainMenu.unity] cuối phiên `isDirty=false`, Console 0 lỗi. Hai bài đo tạm đã gỡ khỏi source.

Vẫn chưa kiểm: chơi tay, host cùng client, Northbridge và Laboratory, và các bài PlayMode HUD khác. Lỗi cầu thang và bề rộng có thể cũng ảnh hưởng special khác (Screamer, Infector) vì dùng chung hàm; chưa đo và chưa đổi cho chúng.
