# Phân bổ nhu yếu phẩm theo Director – 2026-11-13

Phê duyệt của người dùng:
1. Yêu cầu gốc: điều chỉnh phân bổ nhu yếu phẩm, ưu tiên chỗ cần thiết, dùng AI Director / Dynamic Difficulty để mở đồ khi đội khó, "cấm không spawn giữa mặt người ta".
2. "Nghiệm thu, sang tiếp" sau hạng mục ánh sáng.
3. Chọn qua câu hỏi: hộp đạn **"Nạp súng đang cầm"**; đồ dự trữ **"Theo Director khi đội khó"**.

Trạng thái: đã cài đặt, test host và host + client đã qua. Chưa commit.

## Hiện trạng trước khi sửa

Đọc trực tiếp từ `GameScene.unity` (108 `CampaignSupply`, mã đều khác nhau). Ba chapter giống hệt nhau, mỗi chapter 3 bàn × 12 món:

| Mỗi bàn | Số lượng | Điều kiện mở cũ |
|---|---|---|
| Hộp đạn, mỗi hộp cho một súng cố định | 3 | đội ≥1, ≥2, ≥4 |
| Medkit riêng | 1 | đội ≥1 |
| Frag, incendiary, medkit, antidote | 4 | đội ≥1 |
| Frag, incendiary, medkit, antidote (bộ thứ hai) | 4 | đội ≥3 |

Vấn đề:
- Chơi một mình chỉ có đạn Classic (bàn 0), Vandal (bàn 1), Bucky (bàn 2). Operator và Odin chỉ mở khi đội ≥2. **Warden không có hộp nào.**
- Hộp của súng không ai mang thì vô dụng: server trả `AmmoUnavailable` `[CODE WeaponFireHandler.cs AddReserveAmmoServer(WeaponData,int)]`.
- Không dùng tín hiệu nào của Director.
- `CampaignSupply.Update` bật/tắt renderer tức thì khi `supplyPartySize` đổi ở checkpoint chẵn, kể cả khi người chơi đang nhìn.

Đính chính: trong lúc khảo sát tôi từng báo "12 hộp medkit dùng chung một mã". Điều đó sai, do công cụ truy vấn nhầm các đối tượng anh em cùng tên `FiniteSupply`. Các hộp đó là hộp đạn và mã của chúng khác nhau.

## Thay đổi

Không sửa dữ liệu scene, không thêm vật thể, không thêm prop. 108 món giữ nguyên vị trí, mã, mô hình.

### 1. Hộp đạn nạp cho súng đang cầm

`[CODE CampaignTypes.cs CampaignRules.SupplyAmmo]`, `[CODE WeaponFireHandler.cs AddHeldWeaponAmmoServer]`, `[CODE CampaignSupply.cs TryGiveAmmoServer]`

Mỗi hộp cho nửa dự trữ của súng đang cầm, tối thiểu một băng, không vượt sức chứa:

| Súng | Băng / dự trữ tối đa | Một hộp |
|---|---|---|
| Vandal | 25 / 75 | 38 |
| Warden | 15 / 45 | 23 |
| Odin | 100 / 200 | 100 |
| Bucky | 5 / 40 | 20 |
| Classic | 12 / 24 | 12 |
| Operator | 5 / 10 | 5 |

Giữ luật có sẵn "chỉ nhận cả gói": nếu không đủ chỗ cho cả gói thì hộp ở lại trên bàn. Trường `ammoWeapon` và `ammo` của từng hộp vẫn còn trong scene nhưng giờ chỉ quyết định mô hình hộp; số lượng theo súng đang cầm. Ba mức đội ≥1 / ≥2 / ≥4 cho hộp đạn giữ nguyên.

Chữ hiển thị: `[F] HELD WEAPON AMMO`, và màn chọn của hộp có lựa chọn ghi "Ammo for the weapon you are holding".

### 2. Đồ dự trữ mở theo Director

`[CODE CampaignMissionController.Supply.cs]`, `[CODE CampaignTypes.cs SquadNeedsReserve, SupplyWanted]`

Bộ thứ hai ở mỗi bàn (4 món, trước đây cần đội ≥3) giờ là "dự trữ". Server mở khi đội đang khó, tính trên những người còn đứng được:

- máu trung bình dưới 50%, hoặc
- có người đang gục, hoặc vừa có người gục trong 30 giây, hoặc
- đạn dự trữ trung bình (tổng trên các súng đang mang) dưới 25%.

Mỗi lần mở một bàn: bàn gần tâm đội nhất mà không ai nhìn thấy. Hai lần mở cách nhau tối thiểu 45 giây. Bàn đã mở thì không đóng lại. Trạng thái mở không lưu vào checkpoint (không đụng save); sau khi tải lại, server tính lại theo tình trạng đội. Món đã nhặt vẫn theo danh sách `claimedSupplies` như cũ.

Hệ quả cần biết: đội 3–4 người đang khoẻ giờ **không** còn tự động có bộ thứ hai.

### 3. Không hiện/biến mất trong tầm nhìn

Server giữ một `NetworkVariable<ulong>`, mỗi bit là một món của chapter hiện tại (thứ tự theo mã). `CampaignSupply.IsAvailable` trên mọi máy chỉ đọc bit đó. Server đổi một bit chỉ khi không người chơi nào còn sống hoặc đang gục, trong vòng 60 m, có đường thẳng từ tầm mắt tới món đồ. Quy tắc này không xét hướng nhìn, nên quay lưng lại vẫn tính là "đang thấy". Ngoại lệ duy nhất: món vừa bị nhặt biến mất ngay.

Quy tắc áp dụng cho cả việc mở theo quy mô đội ở checkpoint lẫn việc Director mở dự trữ.

## Test (log thật)

```
FPS.Tests.CampaignSupplyRulesTests      b7f195ce  Test run completed: 19/19 passed.          (Editor)
FPS.Tests.CampaignSupplyDirectorTests   7e4c9fd2  Test run completed: 2/2 passed.            (PlayMode, host)
FPS.Tests.CampaignRulesTests            e8b886c4  Test run completed: 63/63 passed.
FPS.Tests.AmmoPickupRulesTests          d23dcc62  Test run completed: 1/1 passed.
FPS.Tests.SurvivalRulesTests            d8d83f6c  Test run completed: 6/6 passed.
FPS.Tests.FactoryDaylightTests          80d08754  Test run completed: 6/6 passed.
FPS.Tests.ApprovedPropSceneTests        50c3614c  Test run completed: 9/10 passed, 1 failed.  (LabPower, như trước)
```

Host, `[PLAYTEST Reports/SupplyDirector-2026-11-13/DirectorOpensReserveForAStrugglingSquad_ButNeverInSight.txt]`:
```
party=1 released=000
ok    healthy squad: base stock shown, every reserve hidden
station 0: 275 m from the operator at (-125, 0, -137), seen=False
station 1: 232 m from the operator at (-125, 0, -137), seen=False
station 2: 336 m from the operator at (-125, 0, -137), seen=False
hurt at spawn -> released=010 expected station 1
ok    nearest unseen station opened, others still shut
watching station 0 -> released=110 visibleThere=0 otherSeen=False
ok    watched station left alone; unseen station opened instead
back at spawn -> released=111
ok    station 0 opened once nobody could see it
```

Host, `AmmoCrateRefillsTheHeldWeapon_AndATakenCrateLeavesTheTable.txt`:
```
held=Warden reserve=45/45 crateArt=Classic pack=23
ok    full reserve: crate refused and still on the table
after take: claimed=True reserve=23 visible=False
ok    held weapon refilled; taken crate removed
```

Host + client (Editor làm host, bản build làm client), `[PLAYTEST Reports/HostClient-2026-11-11/RemoteClientSeesTheServersSupplyTableAndTakesAmmoThroughARequest.txt]`, job `1f1ab7d4`:
```
client connected as id 2 on port 57742
GameScene loaded: completed=[0,2] timedOut=[]
party=2 visible=21 released=0
released=2 reserveVisible=4 clientMatches=True
  -> client: supply Factory_0_0 0          (từ xa: server từ chối)
  -> client: supply Factory_0_0 0          (đứng cạnh hộp: được nhận)
client held=Warden crateArt=Classic serverReserve=23 clientReserve=23 pack=23
```
Bài này kiểm tra: bàn đồ client thấy trùng với server cho đội 2 người; Director mở một bàn dự trữ và client thấy đúng 4 món đó; yêu cầu nhặt từ xa bị từ chối; yêu cầu hợp lệ thì server cấp 23 viên Warden và HUD của client hiện 23.

Để chạy bài này tôi đã: mở rộng `CampaignPeerProbe` (báo danh sách món client đang hiển thị, đạn dự trữ, thêm lệnh `supply`), thêm một test vào `CampaignHostClientTests`, và build lại `Builds/CampaignPeer/` (253 giây, 0 lỗi).

## Lỗi kết nối ở phiên host + client đầu tiên: đã tìm ra và sửa trong bộ thử

Triệu chứng: phiên host + client đầu tiên của mỗi lượt chạy test lỗi ở bước khởi tạo (`KeyNotFoundException: key '1'`), phiên thứ hai qua. Lặp lại 6/6 lần.

Nguyên nhân: khi Editor làm host và tải GameScene, luồng chính của nó đứng hình hàng chục giây và không xử lý mạng. Hai giới hạn thời gian phía client bị vượt:

- `ClientConnectionBufferTimeout = 10` giây chờ host duyệt kết nối. Bài test gọi tải GameScene ngay khi client vừa nối ở tầng transport (`ConnectedClientsList.Count == 2`), trước khi client được duyệt và đồng bộ.
- `DisconnectTimeoutMS = 30000` của transport: host im lặng quá 30 giây thì client coi host đã chết.

Bằng chứng (log thật, đồng hồ phía host):

| Lần chạy | Host đứng | Kết quả |
|---|---|---|
| Editor đã chạy nhiều giờ, sau 6 lần bake | 13.6 s → 59.9 s (46 s) | client ngắt `ClosedByRemote` |
| Sau khi khởi động lại Editor | 7.2 s → 29.4 s (22 s) | qua |
| Cố ý `Thread.Sleep(35 s)` sau khi gọi tải scene, bộ thử cũ | hơn 35 s | lỗi; client ghi `Timed out waiting for the server to approve the connection request` |
| Cùng độ trễ giả, bộ thử đã sửa | 7.0 s → 63.7 s (57 s) | client giữ kết nối, vào GameScene |

Trong bản build, client tải GameScene mất 3.3 giây (`[39.9s] Load GameScene` → `[43.2s] sceneLoaded GameScene` trong `probe-trace.log`). Việc đứng 22–46 giây là chi phí tải scene của Editor. `[INFERENCE]` Người chơi dùng bản build không gặp lỗi này; trong luồng thật host chỉ bắt đầu trận khi mọi người đã vào lobby. **Chưa chạy luồng Lobby thật để xác nhận.**

Tôi không định lượng được thay đổi ánh sáng (lightmap mới, 3176 probe) làm host tải chậm thêm bao nhiêu. Yếu tố lớn nhất quan sát được là Editor chạy lâu: khởi động lại thì từ 46 giây xuống 22 giây.

Bản sửa, chỉ trong code thử nghiệm, không đổi cài đặt mạng của game:
- `[CODE Assets/Tests/PlayMode/CampaignHostClientTests.cs]` chờ `OnClientConnectedCallback` của client (đã duyệt và đồng bộ) rồi mới tải GameScene; đặt `DisconnectTimeoutMS = 120000` cho phiên thử.
- `[CODE Assets/FPS/Features/Missions/Runtime/CampaignPeerProbe.cs]` đặt cùng giá trị phía client; ghi `probe-trace.log` (kết nối, ngắt kèm lý do và stack, sự kiện scene) thẳng ra đĩa vì host kill tiến trình không flush.
- Đã build lại `Builds/CampaignPeer/`.

Kết quả sau sửa, hai lượt liên tiếp:
```
FPS.Tests.CampaignHostClientTests  c163c38c  Test run completed: 2/2 passed.
FPS.Tests.CampaignHostClientTests  daadab9e  Test run completed: 2/2 passed.
```

Ghi chú:
- Trong lúc sửa, bài nhu yếu phẩm host + client từng lỗi một lần vì test đọc bàn đồ trước khi server chạy lượt đánh giá kế tiếp (0.5 giây một lần). Đã sửa test để chờ đúng điều kiện; đây là lỗi của test, không phải của logic.
- Một lần chạy với độ trễ giả 35 giây, bước "client giữ nút tại isolator" bị quá thời gian. Không tái hiện khi bỏ độ trễ giả.
- File bằng chứng `Reports/HostClient-2026-11-11/RemoteClientSeesStateActsThroughRequestsAndCannotOverrideTheServer.txt` đã bị ghi đè nhiều lần trong lúc điều tra; nội dung hiện tại là của lượt qua gần nhất, có thêm các dòng đồng hồ của host.
- Nếu muốn game chịu được host tải chậm trên máy yếu, có thể tăng `DisconnectTimeoutMS` trên `NetworkManager` thật. Tôi chưa làm vì nó làm chậm việc phát hiện người chơi rớt mạng; đây là quyết định của người dùng.

## Chưa làm / chưa kiểm chứng

- Chưa chơi tay một lượt để cảm nhận cân bằng. Các ngưỡng (50% máu, 25% đạn, 30 giây, 45 giây, nửa dự trữ) là giá trị khởi điểm.
- Chưa thử với đội 3–4 người thật.
- Thông báo `AMMO NOT TAKEN · Carry the matching weapon and make room for the whole pack` không còn đúng cho hộp ở bàn (không cần "matching weapon" nữa). Chưa sửa vì dòng này dùng chung với `PickupItem`.
- `ApprovedPropImplementation` (công cụ Editor) vẫn ghi `ammoWeapon`/`ammo` theo bảng cũ. Vô hại, nhưng số `ammo` trong scene giờ không phải số được cấp.
- Chỉ kiểm tra ở chapter Factory trong PlayMode; Asylum và Laboratory dùng cùng code và cùng cấu trúc dữ liệu (đã kiểm bằng Editor test trên cả ba chapter).
- "Không ai nhìn thấy" dùng đường thẳng vật lý, không xét hướng nhìn và không xét spectator đã chết.

## Save gate

`Tools/FPS/UI/Save Authored Scenes` đã chạy; Editor đang mở MainMenu, `isDirty == false`; console không có lỗi biên dịch.

## Skill và quy tắc

- AGENTS.md "Authority": server quyết định món nào hiển thị và ai được nhận; client chỉ hiển thị và gửi yêu cầu. Đã thử cả host và client.
- AGENTS.md "Scope": không đụng save; không thêm interactable hay nội dung mới.
- `fps-shooter`: "Trusting the client in multiplayer" → mọi kiểm tra tầm với và sức chứa ở server.
- `level-design` không có trong phiên nên không đọc.
