# Tank hai chapter còn lại, chơi hết campaign, host cùng client — 11/11/2026

Thực hiện theo yêu cầu trực tiếp "Tiến hành toàn bộ" cho năm việc còn mở sau các báo cáo 09, 10, 11. Báo cáo này phủ ba việc không cần quyết định của người dùng (Tank ở Northbridge và Laboratory, chơi hết campaign, host cùng client). Hai việc còn lại (NavMesh cỡ Tank, UI/UX) đang chờ quyết định; mockup ở `Reports/mockups/12-missions-tracker-device-mockup.html`.

## Kết luận

- Không tìm thấy điểm kẹt nào trong cả ba chapter. Một người chơi hoàn thành campaign từ đầu tới màn kết bằng đúng các request mà client gửi được; mọi chặng giữa các objective đều đi bộ được.
- Tank ở Northbridge và Laboratory được stage trong scene thật. Không cần sửa runtime.
- Host cùng client hai tiến trình chạy được: bản build Development nối vào Editor, đồng bộ đúng, server từ chối đúng những gì client không được làm.
- **Không sửa file runtime gameplay nào.** Thay đổi gồm test, một fixture chỉ có trong Development build, và một menu build.

## 1. Tank ở Northbridge và Laboratory

Hai bài PlayMode mới trong `Assets/Tests/PlayMode/CampaignTankStagingTests.cs`, chạy trong GameScene thật với host thật, đo tại mọi nơi Tank có thể được kích hoạt rồi chạy đúng nhịp encounter của server.

| Chapter | Điểm đo | Luật đầy đủ | Nới lỏng |
|---|---|---|---|
| Northbridge | Lift | Có, 17,9 m | Có |
| Northbridge | Transfer | Có, 11,5 m | Có |
| Northbridge | Vùng chuẩn bị | Có, 16,7 m | Có |
| Northbridge | Vùng lên thang | Có, 19,8 m | Có |
| Laboratory | Case | Có, 50,7 m | Có |
| Laboratory | Transmit | Có, 69,7 m | Có |
| Laboratory | Vùng chuẩn bị | Có, 69,5 m | Có |
| Laboratory | Vùng lên thang | **Không** | Có, 72,1 m |

[PLAYTEST Reports/TankSpawnFix-2026-11-11/NorthbridgeTankIsStagedWhereTheTransferCompletes.txt] [PLAYTEST Reports/TankSpawnFix-2026-11-11/LaboratoryTankIsStagedDuringTheTransmission.txt]

Northbridge có 18 anchor, Laboratory 22. Ở cả hai chapter Tank chỉ xuất hiện sau khoảng 20 giây, vì trong 20 giây đầu director đang ở pha bảo vệ (`reason=Protection`) nên luật đầy đủ chưa được thử; sau đó bậc nới lỏng đặt Tank. Đây là hành vi đã thiết kế ở báo cáo 11, không phải lỗi.

Tại vùng lên thang của Laboratory, không anchor nào đủ chỗ cho thân Tank (`withRoom=0`); Tank vẫn tới qua bậc nới lỏng. Trong thực tế Tank được kích hoạt khi bắt đầu truyền tin, lúc người chơi còn ở gần Transmit, nên trường hợp này chỉ xảy ra nếu Tank bị despawn giữa chừng. [INFERENCE]

Lượt chạy trước khi đạt, ghi đúng thứ tự:

- Job e574c853, Northbridge fail: bài test giữ tham chiếu `CampaignState` cũ; trên host mỗi lần publish thay object state. Sửa test.
- Job 65dd2dea, Laboratory fail tại vùng lên thang: fixture tắt `Update` của controller nên cửa không theo state, buồng thang bị bịt. Thêm bước đồng bộ cửa vào test; đo lại thì nới lỏng tìm được chỗ.
- Job 2e236916 bị mất do Editor khởi động lại trong lúc tạm dừng; đã dọn ba scene tạm `InitTestScene*` mà Test Runner để lại trong `Assets/`.

## 2. Chơi hết campaign tự động

Bài mới `Assets/Tests/PlayMode/CampaignPlaythroughTests.cs`: GameScene thật, host thật, controller chạy bình thường. Một operator làm lần lượt mọi objective của ba chapter chỉ bằng `RequestHold` và `RequestDiscoverFile`, đứng ở chỗ mà server chấp nhận tầm với, chờ cổng chuẩn bị, sống qua encounter, vào vùng lên thang, chuyển chapter, tới màn Completed. Mỗi lần chờ có hạn; hết hạn thì fail kèm state.

Kết quả lượt cuối: `COMPLETED: files=9 factoryCase=True labCase=True downs=0`, `legs a player could not walk: 0`, `errors logged during run: 0`. [PLAYTEST Reports/CampaignPlaythrough-2026-11-11/SoloOperatorFinishesAllThreeChapters.txt]

Giới hạn cần biết, để không đọc kết quả rộng hơn thực tế:

- Operator được **đặt** tới từng điểm, không tự đi. Để bù, mỗi chặng được kiểm bằng NavMesh của game; chặng nào NavMesh không trả lời được thì bài test bake collider hiện tại cho một agent mảnh, lấy tuyến, rồi đẩy `CharacterController` thật của nhân vật đi dọc tuyến đó. Sáu chặng ở Northbridge đi theo cách này và đều tới nơi (49, 31, 52, 85, 81 và 5 m).
- Thời lượng encounter được rút ngắn trên một bản sao settings (Factory và Northbridge 12 giây, Laboratory 58 giây để vẫn qua mốc truyền tin 55 giây). Luật, thời gian giữ, cổng và Tank không đổi.
- Tank Factory bị hạ qua `EnemyHealth.TakeDamage`, không qua bắn súng. Người chơi được bật bảo vệ nên không kiểm độ khó.
- Một người chơi. Không kiểm cảm giác chơi, nhịp, hay khả năng lạc đường.

### Phát hiện

| # | Mức | Phát hiện | Bằng chứng | Đề xuất |
|---|---|---|---|---|
| 1 | P2 | Ở Northbridge người chơi đứng chờ ở cổng chuẩn bị khoảng 43 đến 48 giây với lý do "A Tank is closing in" trước khi Tank xuất hiện: khoảng 22 giây đồng hồ Tank tạm dừng do thời gian đọc tài liệu, rồi 20 giây pha bảo vệ của director | [PLAYTEST SoloOperatorFinishesAllThreeChapters.txt] dòng `[tank]` của Asylum/Preparing; `preparation -> Encounter after 47s` | Không tính thời gian đọc khi phase đã là Preparing, hoặc rút bậc chờ ở Northbridge. Chạm logic Tank, cần flow duyệt |
| 2 | P2 | NavMesh của game không nối các phòng tầng trên của Northbridge với nhau: sáu chặng trả `PathPartial` dù người chơi đi bộ được | Cùng file, các dòng `no game-NavMesh route` | Hệ quả: marker rơi về khoảng cách đường thẳng (báo cáo 09 đã nêu) và zombie không theo được giữa các phòng đó. Việc dựng level, chưa làm |
| 3 | P3 | Vùng chuẩn bị của Northbridge có đáy hộp chỉ thấp hơn vị trí đứng của nhân vật 4,3 cm (đáy y=-3,80, nhân vật nghỉ ở y=-3,757). Hiện vẫn tính là trong vùng | [PLAYTEST] lượt chẩn đoán: `simulated operator rests at (146.243, -3.757, 240.456) inPreparation=True` | Hạ đáy hộp xuống dưới sàn như vùng của Factory. Sửa scene, chưa làm |
| 4 | P3 | LabTrace và LabIndex đều có interactable trong scene và hoàn thành được; trả lời câu hỏi còn mở của báo cáo 09 | Cùng file: `LabTrace: completed`, `LabIndex: completed` | Không |
| 5 | Kỹ thuật | Truy vấn NavMesh không kèm filter nhìn thấy dữ liệu của **mọi** loại agent. Khi bài test nạp thêm một NavMesh cho agent riêng, server lập tức không đặt được Tank ở Northbridge và bỏ qua encounter | Lượt chạy job 44d8a3b8 và f3b34c1a: `Tank could not be staged in Asylum (NoSafePosition)`; gỡ NavMesh phụ thì Tank stage sau 20,9 giây (job 85cfad80) | Ảnh hưởng trực tiếp tới việc bake NavMesh cỡ Tank, xem mục "Chờ quyết định" |

Phát hiện 5 lúc đầu trông như lỗi game (Tank bị bỏ qua, chờ 87 giây). Nó là do bài test của tôi gây ra, đã sửa trong test bằng cách gỡ NavMesh phụ ngay sau mỗi lần kiểm. Runtime của game hiện chỉ có một NavMesh nên không bị.

## 3. Host và client hai tiến trình

- Fixture `Assets/FPS/Features/Missions/Runtime/CampaignPeerProbe.cs`, chỉ biên dịch trong Development build và Editor, chỉ chạy khi có tham số `-campaignPeer`. Nó nối vào host loopback như một client thường, ghi ra file những gì client đó quan sát, và gửi request qua đúng các hàm public mà HUD dùng.
- Menu `Tools/FPS/Campaign/Build Peer Client` build ra `Builds/CampaignPeer/FPS.exe` (thư mục `Builds/` đã nằm trong `.gitignore`). Build đạt: 225 giây, 0 lỗi, 19 cảnh báo, khoảng 2 GB.
- Bài `Assets/Tests/PlayMode/CampaignHostClientTests.cs`: Editor làm host, chạy bản build làm client, hai operator cùng vào insertion.

| Kiểm tra | Kết quả |
|---|---|
| Client nạp GameScene, có HUD campaign, thấy đúng chapter, phase, revision, roster 2 người | Đạt: `revision=3/3 roster=2 hud=True isServer=False` |
| Client gọi restore checkpoint | Server từ chối, phase vẫn Exploring |
| Client ghi file khi không ở gần nguồn | Server từ chối, `files=0` |
| Client giữ objective còn khóa (Generator) | Server từ chối |
| Client giữ objective đã mở nhưng ngoài tầm (Isolate) | Server từ chối, `completed=0` |
| Server dời operator của client, client xác nhận, hai bên khớp vị trí | Đạt, lệch 0 m |
| Client giữ Isolate trong tầm | Server hoàn thành, client nhận lại `completed=2`, `revision=5/5` |
| Host đọc manifest | Client nhận `files=4` |
| Client thoát | Host vẫn Exploring, roster giữ slot với `connected=False`, tiến độ của client được giữ |

[PLAYTEST Reports/HostClient-2026-11-11/RemoteClientSeesStateActsThroughRequestsAndCannotOverrideTheServer.txt]

Giới hạn: phiên dùng kết nối loopback trực tiếp, tắt connection approval, tự spawn player, giống công cụ HUD Review. Đường Lobby và Relay thật của game **không** được kiểm. Chỉ kiểm chapter Factory với hai người; chưa kiểm client qua chuyển chapter, cổng party nhiều người, Tank, hay kết nối lại.

## Kiểm tra

| Kiểm tra | Job | Kết quả |
|---|---|---|
| Compile | debug_get_errors | 0 lỗi |
| CampaignTankStagingTests, PlayMode, cả lớp | 3290378b | 5/5 pass, 265 s |
| CampaignPlaythroughTests, PlayMode | 288a4e07 | 1/1 pass, 331 s |
| CampaignHostClientTests, PlayMode, hai tiến trình | 93edb9c3 | 1/1 pass, 156 s |
| Build Peer Client | menu | Succeeded, 0 lỗi |

Log thật, nguyên văn `resultSummary`:

```
Test run completed: 5/5 passed.
Test run completed: 1/1 passed.
Test run completed: 1/1 passed.
```

Trích log thật:

```
first tick: stage=Pending reason=Protection
after one timeout: stage=Active reason=None
tank distance=16.9
COMPLETED: files=9 factoryCase=True labCase=True downs=0
legs a player could not walk: 0
errors logged during run: 0
client completed FactoryIsolate: host completed=2 client completed=2 revision=5/5
after client quit: phase=Exploring rosterConnected=False rosterSize=2
```

Bài playthrough phải chạy tám lượt mới đạt; bảy lượt fail đều do bài test (chỗ đặt nhân vật lấy độ cao NavMesh thay vì sàn, NavMesh phụ gây nhiễu, buồng thang không có NavMesh game), không lượt nào do lỗi game.

Chưa chạy lại trong phiên này: CampaignLifecycleTests, CampaignRulesTests, HudRegressionTests, HudGameplayAcceptanceTests. Runtime gameplay không đổi nên rủi ro thấp, nhưng chưa có bằng chứng. [INFERENCE]

File save thật của người chơi được sao lưu trước và khôi phục sau mỗi bài chạy qua checkpoint. Save Gate trước và sau mỗi lượt compile, test và build. [SCENE Assets/FPS/Scenes/MainMenu.unity] `isDirty=false`, `isPlaying=false`, Console 0 lỗi.

## Chờ quyết định

**NavMesh cỡ Tank.** Phát hiện 5 làm việc này lớn hơn dự tính ban đầu. Runtime có 25 truy vấn NavMesh không kèm filter trong 8 file (`DirectorSpawnService`, `CampaignHUD.Navigation`, `InfluenceMapManager`, `CampaignPlacement`, `ZombieFactory`, `AttackSlotManager`, `ZombiePoolManager`, `SpecialEscapePlanner`), không truy vấn nào có filter. Thêm một NavMesh cỡ Tank mà không sửa hết các truy vấn đó sẽ làm hỏng việc spawn, đúng như đã thấy trong bài test. Vì vậy "Tank đúng cỡ" gồm cả việc bake lẫn sửa 8 file AI rồi chạy lại toàn bộ test AI.

**UI/UX.** Mockup năm mục A đến E chờ duyệt. Chưa viết code UI nào.

**Cập nhật 2026-11-12.** NavMesh cỡ Tank: người dùng chọn "Giữ nguyên", không làm. Mockup A đến E đã được duyệt và code; xem báo cáo 13.

## Skills đã đọc và quy tắc áp dụng

- `unity-skills`: `/health` trước, lấy schema trước khi gọi, test bất đồng bộ tuần tự theo jobId, Save Gate, readback scene.
- `fps-shooter`: authority ở server, không tin client; áp cho bài host cùng client. Không chỉnh hành vi chiến đấu của Tank.
- `game-ui-ux`: HUD theo sự kiện, một stack màn hình, không thêm modal; áp cho mockup.
- `level-design` và `unity-mcp-ui-layout` chưa mở trong phiên này. Phần kiểm soft-lock dựa trên quy tắc đã ghi ở báo cáo 11; trước khi code UI phải đọc `unity-mcp-ui-layout`.
- AGENTS.md: server quyết định; kiểm cả host và client; không tự duyệt mockup; phần chạm flow chỉ nêu làm đề xuất; nhãn bằng chứng; báo cáo tiếng Việt trong `Reports/`.
- Codebase Memory MCP không có project nào được index (`list_projects` trả 0), nên đọc trực tiếp source đã khoanh vùng.
