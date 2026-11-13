# 13. Giao diện Missions, tracker và panel device (mockup A đến E)

Ngày: 2026-11-12. Nối tiếp báo cáo 12.

## Kết luận

- Cả năm mục A, B, C, D, E của mockup `Reports/mockups/12-missions-tracker-device-mockup.html` đã được code sau khi bạn duyệt ("E: toast + giữ phím", "A + B: Missions, tracker", "C + D: panel device").
- Bài chụp ảnh mọi màn hình ở 1920x1080 và 3840x2160 đạt: 180 kiểm tra ok, 0 FAIL. Hai bộ EditMode đạt 63/63 và 19/19.
- Bộ acceptance HUD lúc đầu đạt 9/14; sau các lượt điều tra và sửa bổ sung (xem các mục "bổ sung" trong phần Test) đạt 14/14 trong một lượt chạy (job 97c665f1). Chưa có lượt chạy baseline trên HEAD để đối chiếu.
- Chưa có playtest tay, và chưa kiểm giao diện mới trên host cùng client thật. Theo AGENTS.md, phần UI vì thế chưa đủ tiêu chí "done".
- Quyết định NavMesh cỡ Tank: bạn chọn "Giữ nguyên", không làm gì thêm.

## Thay đổi theo từng mục mockup

| Mục | Trước | Sau | Bằng chứng |
|---|---|---|---|
| E. Objective bị khoá | Mở panel device với biểu tượng khoá và dòng "Requires" | Không mở panel; hiện toast `Requires: <điều kiện thật>` | [CODE `CampaignHUD.cs` `Open()` / `IsLockedForPanel`], [PLAYTEST `locked-toast-factory-backup-1920x1080.png`, `device-locked-toast-3840x2160.png`] |
| E. Preparation | Modal Preparation | Bỏ modal. Khi chơi squad và đứng trong khu chuẩn bị, hiện lời nhắc `HOLD <phím> TO READY UP`, giữ 1 giây thì gửi `RequestReady(true)` lên server | [CODE `CampaignHUD.cs` `UpdateReadyPrompt`], [PLAYTEST `preparation-tracker-1920x1080.png`] |
| A. Missions | Danh sách phẳng | Hàng nhóm không bấm được, có số đếm xong/tổng. Factory: EXTRACTION, POWER, LOGISTICS. Chapter khác: một nhóm mang tên mục tiêu chapter | [CODE `TeamInventoryUI.cs` `PopulateMissions` / `AddGroup`], [PLAYTEST `missions-factory-*.png`, `missions-northbridge-*.png`, `missions-laboratory-*.png`] |
| B. Tracker | Tên chapter, bước hiện tại, dòng "Then:", dòng SWITCH riêng | `FACTORY — Recover the evidence case` ở dòng đầu, bước hiện tại, hai nhánh kèm số đếm; `[T] SWITCH` nằm ngay trên nhánh đang theo; bỏ dòng "Then:". Cao 172 (trước là 194 cộng 66 khi có "Then:") | [CODE `CampaignHUD.Navigation.cs`], [PLAYTEST `tracker-factory-*.png`, `tracker-northbridge-*.png`, `tracker-laboratory-*.png`] |
| C + D. Panel device | Hai kiểu: panel cũ cho màn chọn đáp án, khung device cho màn điều khiển | Mọi objective dùng khung device. Màn hồ sơ: cột trái tên tài liệu, trang giấy nội dung nguồn, hàng lựa chọn theo nhóm có tiêu đề. Màn điều khiển: chỉ còn hàng Action, bỏ hàng System và Source | [CODE `CampaignHUD.cs` `RenderDocument` / `ChoiceBand` / `StyleDeviceChoice`, `CampaignHUD.SelectedLayout.cs` `LayoutSelectedDevice`], [PLAYTEST `device-factory-shipping-*.png`, `device-northbridge-patient-*.png`, `device-laboratory-archive-*.png`, `device-laboratory-power-*.png`, `device-factory-isolate-*.png`] |

Sai lựa chọn ở màn shipping: tiêu đề nhóm sai đổi thành `<tên nhóm>   —   CHECK THIS GROUP` màu Danger (`device-factory-shipping-mismatch-*.png`).

Ảnh:
- Trước: `Reports/UIUX/ThreeTabInventory-2026-11-10/After/` (có `selected-locked-*`, `selected-missions-*`, `selected-manifest-*`, `selected-hud-*`) và `Reports/UIUX/DeletionPass-2026-11-11/After/`.
- Sau: `Reports/UIUX/MissionsTrackerDevice-2026-11-11/After/`, mỗi màn có bản 1920x1080 và 3840x2160; một số có thêm 1024x768.

## Lệch so với mockup

1. Tiêu đề nhóm đầu của Factory là `EXTRACTION`, không phải `EXTRACTION — RECOVER THE EVIDENCE CASE`. Chuỗi dài không vừa cột ở cỡ chữ Small của theme, và font HUD không có ký tự "…" để cắt bớt.
2. Ở 1024x768 hoặc khi tỉ lệ canvas dưới 1, dòng đầu tracker chỉ hiện mục tiêu chapter, bỏ tên chapter, để không tràn.
3. Màn supply cache (`OpenSupply`) vẫn dùng panel cũ. Màn này không có trong mockup nên tôi không đụng.

## Chuỗi mới cần bạn duyệt lời

Các chuỗi sau không có trong mockup, tôi phải đặt để giao diện hoàn chỉnh:

- Tiêu đề lựa chọn: `WHAT DO THE RECORDS SHOW?`, `WHICH ARCHIVE ENTRY MATCHES?`, `EMERGENCY LOADS — KEEP SAFETY ONLINE, TOTAL 6 OR LESS`.
- Phụ đề thiết bị: `PATIENT RECORDS`, `POWER ALLOCATION`, `ARCHIVE RECORDS`, `ON-SITE CONTROL`; nhãn bezel `LOCAL CONTROL` cho màn điều khiển ngoài Factory.
- Màn hồ sơ: `READ SOURCE`, `SOURCE`, `Record the source document`, `SOURCE NOT RECORDED…`.
- Cổng party: `Ready up in the preparation area`.

## Test

| Bài | Chế độ | Job | Kết quả thật |
|---|---|---|---|
| `FPS.Tests.CampaignDeviceReviewCapture` | PlayMode | 14f211c6 | `Test run completed: 1/1 passed.` (93 giây); log 180 dòng ok, 0 FAIL |
| `FPS.Tests.CampaignRulesTests` | EditMode | e8d1c5b9 | `Test run completed: 63/63 passed.` |
| `FPS.Tests.HudRegressionTests` | EditMode | 9118bd3c | `Test run completed: 19/19 passed.` |
| `FPS.Tests.HudGameplayAcceptanceTests` | PlayMode | 5111c14a | `Test run completed: 9/14 passed, 5 failed.` (730 giây) |

Năm bài trượt trong bộ acceptance:

| Bài | Câu kiểm trượt | Nhận định |
|---|---|---|
| `CampaignHoldAndDebriefRespectCancellationAndAuthority` | `Simulated client debrief exposes RETURN TO MENU` (dòng 735) | Bài đặt phase Failed rồi đòi nhãn `RETURN TO MENU`, nhưng [CODE `CampaignStatusUI.cs:150`] trả `RETRY CHECKPOINT` mỗi khi failed, bất kể host hay client. File này không nằm trong diff. Các phần panel device mới ở phía trên của bài đều qua |
| `SelectedUiInputScrollOpacityAndNativeHoldRemainValid` | Cùng câu trên (gọi lại bài trên) | Như trên |
| `AcceptedShotgunHitsConfirmAndRejectedShotsStaySilent` | `Shotgun body confirmation uses HUD text color` | Hit marker, không thuộc file nào trong diff [INFERENCE: có từ trước] |
| `FpsSampleFeedbackTracksCombatAndPlayerLifecycle` | `Body hit uses a plain image in HUD text color` | Như trên [INFERENCE] |
| `GameplayStatesAndLayoutRemainReadable` | `Secondary detail expires after two seconds` | Chi tiết hotbar, không thuộc file nào trong diff [INFERENCE] |

Runtime trong diff chỉ gồm `CampaignHUD.cs`, `CampaignHUD.Navigation.cs`, `CampaignHUD.SelectedLayout.cs`, `CampaignTypes.cs`, `TeamInventoryUI.cs`. Báo cáo 10 ghi bộ acceptance chưa được chạy lại sau lượt xoá, nên không có số baseline để khẳng định chắc năm bài này trượt từ trước.

### Điều tra năm bài trượt (bổ sung)

Cả năm bài trượt từ commit `42ae9781` (HEAD), không do lượt này. Căn cứ: `git diff HEAD` trên `HUDManager.cs`, `SurvivalHotbar.cs`, `CampaignStatusUI.cs`, `TacticalUiTheme.cs` rỗng, và bốn câu kiểm trượt có nguyên văn trong bản test ở HEAD (dòng 734, 825, 881, 1047). Chưa chạy lại bộ test trên bản HEAD sạch; kết luận dựa trên đọc code.

| Bài | Nguyên nhân | Loại |
|---|---|---|
| `AcceptedShotgunHits…`, `FpsSampleFeedback…` | Test so màu hit marker với `TacticalUiTheme.Text` (235, 232, 218) [CODE `TacticalUiTheme.cs:13`]. HUD lấy màu từ `SelectedUiTheme.Current.Text` (243, 243, 243) [CODE `HUDManager.cs:10`, `:85`, `:921`; `SelectedUiTheme.cs:12`] | Test lỗi thời: HUD đã chuyển sang theme mới, test còn so với theme cũ |
| `GameplayStatesAndLayout…` | Test đòi dòng `SelectionDetail` tắt sau 2 giây. Bản cũ có điều kiện `Time.unscaledTime < detailUntil`; bản HEAD bỏ điều kiện đó, dòng này thành gợi ý `[3] CYCLE` hiện thường trực [CODE `SurvivalHotbar.cs:137`, `:160`] | Test lỗi thời |
| `CampaignHoldAndDebrief…`, `SelectedUiInputScroll…` | Test đòi client ở debrief thất bại thấy nút `RETURN TO MENU`. Code: khi failed, nhãn luôn là `RETRY CHECKPOINT` và nút bị ẩn với client; `debriefEnd` cũng bị ẩn [CODE `CampaignStatusUI.cs:150-152`] | Test và code mâu thuẫn về hành vi; cần quyết định |

Điểm cần quyết định ở hai bài debrief: theo code hiện tại, client không có nút nào trên màn debrief thất bại và phải chờ host bấm retry. Hoặc đó là chủ ý (server quyết định retry), khi đó sửa test; hoặc client cần lối `RETURN TO MENU`, khi đó sửa `CampaignStatusUI`. Tôi chưa kiểm client có thoát được bằng menu tạm dừng trong lúc này hay không.

### Nút RETURN TO MENU cho client ở debrief thất bại (bổ sung, theo quyết định của người dùng)

Người dùng xác nhận client cần nút này. Sửa trong `Assets/FPS/Features/UI/Runtime/CampaignStatusUI.cs`:

- Khi thua, client thấy nút `RETURN TO MENU`; bấm thì client tự ngắt kết nối (`NetworkGameManager.Disconnect()`, cùng lệnh đang dùng khi thắng). Host vẫn thấy `RETRY CHECKPOINT` và chỉ host gửi được `RequestRetry()`.
- Dòng chân trang của client khi thua: `Wait for the host to retry, or return to menu.` (chuỗi mới, cần duyệt lời).
- Server không đổi: bài test vẫn kiểm server từ chối retry từ người gửi không phải host.

| Bài | Job | Kết quả thật |
|---|---|---|
| `CampaignHoldAndDebriefRespectCancellationAndAuthority` | 7518113e | `0/1 passed, 1 failed`: dừng ở dòng 723 "Fresh hold completes…", bước giữ phím theo thời gian thực, trước chỗ sửa |
| Cùng bài, chạy lại không đổi code | f711d6af | `Test run completed: 1/1 passed.` |
| `SelectedUiInputScrollOpacityAndNativeHoldRemainValid` | c9c7f7fd | `Test run completed: 1/1 passed.` |

Bước giữ phím ở dòng 723 trượt một lần rồi qua khi chạy lại, nên bài này không ổn định theo thời gian [INFERENCE: phụ thuộc đồng hồ thực và input giả lập]. Ảnh: [PLAYTEST `debrief-client-fixture.png`]. Trong ảnh, biểu tượng phím nằm cách nhãn xa hơn bình thường, vì vị trí của nó chỉ được tính lại khi đổi tỉ lệ màn hình chứ không tính lại khi nhãn đổi; chưa sửa. Nút mới chưa được bấm thử trên client thật, và bộ acceptance đầy đủ chưa chạy lại sau thay đổi này; ba bài test lỗi thời còn lại chưa sửa.

### Sửa ba bài test lỗi thời và biểu tượng phím (bổ sung, theo yêu cầu "Sửa luôn")

- Hai câu kiểm màu hit marker giờ so với `SelectedUiTheme.Current.Text`.
- Câu kiểm "expires after two seconds" đổi thành kiểm gợi ý `[3] CYCLE` hiện thường trực.
- `CampaignStatusUI.cs`: vị trí biểu tượng phím được tính lại mỗi khi nhãn nút đổi [PLAYTEST `debrief-client-fixture.png`].

Qua được ba câu đó thì lộ ra các lỗi nằm phía sau, trước đây bị che:

| Bài | Lỗi lộ ra | Xử lý |
|---|---|---|
| `AcceptedShotgunHits…`, `FpsSampleFeedback…` | `NullReferenceException` trong helper `GetStatic` của test: nó tìm `WarningColor` như một field, nhưng trên `HUDManager` đó là property [CODE `HUDManager.cs:88`] | Sửa helper cho đọc được cả property. Hai bài đạt 1/1 (job e9013af3, c6b21e60) |
| `GameplayStatesAndLayoutRemainReadable` | `Text clears its decorative frame: HealthPanel / SepsisWarning / text=(x:24, y:16, width:302, height:30); panel=(… width:340, height:138)` | Chưa sửa, xem dưới |

**Lỗi layout Sepsis (thật, chưa sửa).** Khi infection đạt mức Sepsis, dòng `SEPSIS - TREAT NOW` được đưa vào khung máu nhưng vẫn giữ rect cũ của khung infection. Trong khung máu 340x138 hiện tại, rect đó cách mép phải 14 px (yêu cầu 16) và nằm ở y 92 đến 122 tính từ trên, tức đè lên dòng trạng thái infection (y 70 đến 100) và thanh máu (y 100 đến 106) [CODE `HUDManager.cs` `ConfigureSelectedPresentation`; INFERENCE từ toạ độ, chưa có ảnh vì bài test dừng trước bước chụp]. Khung không còn chỗ trống cho một dòng thứ ba. Đây là thay đổi bố cục HUD nên cần bạn duyệt hướng. Đề xuất: khi Sepsis, đặt dòng cảnh báo vào đúng vị trí dòng trạng thái infection và ẩn dòng trạng thái (dòng này lúc đó chỉ ghi `SEPSIS 100%`).

Ghi nhận thêm: `WarningColor` trên `HUDManager` đang trả cùng màu với `TextColor` [CODE `HUDManager.cs:85`, `:88`], nên hit marker headshot và body cùng màu; câu kiểm "Headshot changes marker color" đạt mà không phân biệt được gì. Không sửa (thuộc phản hồi chiến đấu, ngoài phạm vi).

| Bài | Job | Kết quả thật |
|---|---|---|
| `FPS.Tests.HudGameplayAcceptanceTests` (toàn bộ, trước khi sửa helper) | 84a7ee7b | `Test run completed: 11/14 passed, 3 failed.` |
| `AcceptedShotgunHitsConfirmAndRejectedShotsStaySilent` (sau khi sửa helper) | e9013af3 | `Test run completed: 1/1 passed.` |
| `FpsSampleFeedbackTracksCombatAndPlayerLifecycle` (sau khi sửa helper) | c6b21e60 | `Test run completed: 1/1 passed.` |
| `GameplayStatesAndLayoutRemainReadable` | f2759b26 | `0/1 passed, 1 failed` (lỗi Sepsis ở trên) |

Tính gộp: 13/14 bài đạt, còn 1 bài trượt do lỗi Sepsis. Chưa có một lượt chạy toàn bộ duy nhất cho con số này.

### Sửa layout Sepsis (bổ sung, người dùng duyệt hướng đề xuất)

`Assets/FPS/Features/UI/Runtime/HUDManager.cs`: khung máu chỉ có một dòng trạng thái. Khi Sepsis, dòng `SEPSIS - TREAT NOW` (cỡ Small, màu Danger của theme) được đặt vào đúng ô của dòng trạng thái infection (24, 70, 284x30 trong khung 340x138), và dòng trạng thái bị tắt cho tới khi rời mức Sepsis [CODE `ConfigureSelectedPresentation`, `UpdateInfection`], [PLAYTEST `sepsis.png` ở 1920x1080; chưa có ảnh 4K cho trạng thái này].

Lượt chạy toàn bộ sau cùng: `FPS.Tests.HudGameplayAcceptanceTests`, job b35eae8f, `Test run completed: 13/14 passed, 1 failed.` (824 giây).

Bài còn trượt vẫn là `GameplayStatesAndLayoutRemainReadable`, nhưng ở câu kiểm kế tiếp: `HUD groups remain separate: SquadPanel / InteractionPrompt`, tại 1024x768 tỉ lệ 100% (độ phân giải đầu tiên của vòng lặp), khi có ba dòng đồng đội cùng lời nhắc tương tác. Chưa điều tra, chưa sửa: nằm ngoài phần đã duyệt, và 1024x768 thấp hơn hai mốc 1080p và 4K của tiêu chí UI. Các độ phân giải sau đó trong bài này chưa được kiểm vì bài dừng ở đây.

Ghi nhận thêm từ `sepsis.png`: gợi ý `[3] CYCLE` đè lên nhãn phím `G` của ô lựu đạn trong hotbar. `SurvivalHotbar.cs` không nằm trong diff; chưa sửa.

### Điều tra: Squad chồng lời nhắc tương tác ở 1024x768 (bổ sung)

Số đo thật từ bài test (job chạy lại sau khi thêm toạ độ vào thông báo lỗi): `SquadPanel (x:24.89, y:291.56, width:302.22, height:270.22) vs InteractionPrompt (x:323.56, y:295.89, width:211.56, height:56.89) at 1024x768`. Hai khối chồng nhau khoảng 3,6 px theo chiều ngang.

Nguyên nhân, gồm hai phần cộng lại:

1. Lệch 4 đơn vị canvas giữa hai quy ước. Khung Squad đặt cách mép trái 28, rộng `VitalsWidth` = 340, nên mép phải ở 368 [CODE `SquadHUD.cs:170`]. Dải giữa màn hình (lời nhắc, phụ đề, toast) bắt đầu ở `VitalsWidth + 24` = 364 [CODE `HudLayout.cs:51`]. Hai khối luôn lấn nhau 4 đơn vị theo chiều ngang; chỉ thành lỗi khi chúng ở cùng độ cao.
2. Ở 1024x768 canvas chỉ rộng 1152 đơn vị, dải giữa còn 238 đơn vị. Phụ đề dài bị xuống nhiều dòng, khung phụ đề cao lên và đẩy lời nhắc lên khoảng 333 đơn vị [CODE `CampaignHUD.cs:172`], vượt đáy khung Squad ở 328. Ở màn rộng, phụ đề chỉ một hai dòng, lời nhắc nằm ở 160 đến 224 nên không chạm Squad.

Lỗi có từ HEAD: `HudLayout.cs`, `SquadHUD.cs`, `CampaignDialogue.cs` và các dòng liên quan trong `CampaignHUD.cs` không nằm trong diff.

Chưa sửa. Đề xuất: đổi mép trái của dải giữa thành `VitalsWidth + 28 + 24` (một dòng trong `HudLayout.CentralBand`). Ở màn rộng dải giữa vẫn rộng 820 và canh giữa nên không đổi gì; ở màn hẹp dải co thêm 28 đơn vị (238 còn 210 tại 1024x768), phụ đề sẽ xuống thêm dòng. Sau lỗi này bài test còn các độ phân giải khác chưa chạy tới, nên có thể còn lỗi phía sau.

Thông báo lỗi của câu kiểm "HUD groups remain separate" trong test giờ in kèm toạ độ hai khối và độ phân giải.

### Sửa lỗi Squad chồng lời nhắc và các lỗi lộ ra sau đó (bổ sung, người dùng yêu cầu sửa)

| Lỗi | Nguyên nhân | Sửa |
|---|---|---|
| Squad chồng lời nhắc ở 1024x768 | Dải giữa bắt đầu ở 364, Squad kết thúc ở 368 | [CODE `HudLayout.cs` `CentralBand`]: mép trái dải giữa thành `28 + VitalsWidth + 24` |
| `Text clears its decorative frame: SquadMember_1 / Portrait` ở tỉ lệ 125% (thiếu 0,07 px lề trái) | Neo của hàng Squad tính theo bề rộng 350 trong khi hàng rộng 340, lề thật là 15,54 thay vì 16 | [CODE `SquadHUD.cs:182-188`]: neo tính theo `HudLayout.VitalsWidth` |
| `ADS reduces tracker: reduced=True blocked=False height=172` | Tracker chỉ được vẽ lại khi đổi objective, đổi phase hoặc có toast; lúc khám phá, bắt đầu ngắm không gọi vẽ lại. Có từ HEAD (diff không chạm cơ chế này) | [CODE `CampaignHUD.cs` `Update`]: vẽ lại tracker khi `HudLayout.SecondaryReduced` đổi giá trị |
| `Ready count is in the tracker` | Do thay đổi mục E của lượt này: chơi một mình không còn dòng READY, test vẫn đòi chữ `READY` | Sửa test: kiểm dòng cổng party có `in zone` |

Kết quả sau cùng, mỗi dòng là một lượt chạy thật:

| Bài | Job | Kết quả |
|---|---|---|
| `FPS.Tests.HudGameplayAcceptanceTests` (toàn bộ) | 97c665f1 | `Test run completed: 14/14 passed.` (814 giây) |
| `FPS.Tests.HudRegressionTests` | chạy ngay sau | `Test run completed: 19/19 passed.` |
| `FPS.Tests.CampaignRulesTests` | chạy ngay sau | `Test run completed: 63/63 passed.` |

Sau các sửa đổi này, mục "Kết luận" ở đầu báo cáo (9/14) không còn đúng: bộ acceptance đạt 14/14. `CampaignDeviceReviewCapture`, `CampaignPlaythroughTests`, `CampaignTankStagingTests` và `CampaignHostClientTests` chưa chạy lại sau các sửa đổi bổ sung. Lỗi gợi ý `[3] CYCLE` đè nhãn phím `G` trong hotbar vẫn chưa sửa.

Runtime đã đổi trong cả đợt: `CampaignHUD.cs`, `CampaignHUD.Navigation.cs`, `CampaignHUD.SelectedLayout.cs`, `CampaignTypes.cs`, `TeamInventoryUI.cs`, `CampaignStatusUI.cs`, `HUDManager.cs`, `HudLayout.cs`, `SquadHUD.cs`.

Test đã sửa cho khớp giao diện mới: `CampaignRulesTests.cs` (chuỗi `1/1 in zone`, đếm tiêu đề `CHECK THIS GROUP`), `HudRegressionTests.cs` (toast `Requires:`, không còn `DeviceLock`, `DeviceSource`, `DeviceSystemValue`; có hàng nhóm), `HudGameplayAcceptanceTests.cs` (các câu kiểm màn khoá đổi sang kiểm toast). Bài mới: `CampaignDeviceReviewCapture.cs`.

## Trạng thái Editor khi kết thúc

- [SCENE `Assets/FPS/Scenes/MainMenu.unity`] `isDirty=false`, không ở Play mode, Console 0 lỗi.
- Hằng `Evidence` trong `HudGameplayAcceptanceTests.cs` đã trả về `Reports/UIUX/Bot7Fixes-2026-11-10/After/`.
- Ba file font động bị test ghi lại atlas (`HudGlyphFallback.asset`, `LiberationSans SDF - Fallback.asset`, `Xolonium-Bold SDF.asset`) đã `git checkout` về bản đã commit.
- Không còn `Assets/InitTestScene*.unity`.
- Chưa commit gì.

## Giới hạn

- Không có playtest tay. Ảnh do bài PlayMode tự mở từng màn rồi chụp.
- Giao diện mới chưa được kiểm trên host cùng client hai tiến trình. Bản `Builds/CampaignPeer/FPS.exe` được build trước khi sửa UI, muốn chạy lại `CampaignHostClientTests` phải build lại.
- Lời nhắc giữ phím Ready mới được kiểm về cấu trúc và ảnh, chưa thử với hai người chơi thật.
- `CampaignPlaythroughTests` và `CampaignTankStagingTests` chưa chạy lại sau khi sửa UI. Thay đổi lượt này không chạm logic server, nhưng `PartyGateText` có đổi chuỗi.
- Ảnh review còn nằm trong `Reports/UIUX/MissionsTrackerDevice-2026-11-11/After/`; xoá sau khi review độc lập xong.

## Skills đã đọc và quy tắc áp dụng

- `unity-skills`: Save Gate trước và sau mỗi lượt test, test bất đồng bộ theo jobId, readback scene.
- `game-ui-ux`: HUD theo sự kiện, một stack màn hình, không thêm modal (bỏ modal Preparation, toast thay màn khoá).
- `unity-mcp-ui-layout`: lập layout theo rect từng phần tử, kiểm tràn chữ ở nhiều độ phân giải.
- `fps-shooter`: client chỉ gửi request (`RequestReady`), server quyết định.
- `level-design` không mở trong lượt này; không có thay đổi flow nhiệm vụ.
- AGENTS.md: mockup duyệt trước khi code UI; theme 4 màu, 3 cỡ chữ (bỏ auto-size, chỉ chọn Body hoặc Small); không tự duyệt; nhãn bằng chứng; báo cáo tiếng Việt trong `Reports/`.
