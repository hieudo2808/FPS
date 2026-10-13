# Bằng chứng mã nguồn: AI Director và Dynamic Difficulty

Ngày kiểm tra: 09/10/2026. Chỉ đọc mã hiện tại; không chạy Unity Test Runner, không biên dịch, không đổi scene/prefab/script. Đây là phụ lục kiểm chứng, không phải kết quả thử nghiệm người chơi.

## Phạm vi và độ tin cậy

- Đã thử Codebase Memory trước bằng CLI đã cấu hình tại `.codex/codebase-memory-mcp/codebase-memory-mcp.exe` (v0.10.8). `list_projects` bị chặn bởi kiểm tra DACL của thư mục AppData, vì vậy không có kết quả graph/index hoặc xác nhận độ phủ index. Sau đó chỉ đọc vùng đã thu hẹp: `Assets/FPS/Features/AI/Runtime`, các nguồn trực tiếp được chúng tham chiếu và `Assets/Tests`.
- Git ghi nhận `Assets/FPS/Features/AI/Runtime/RubberBandingSystem.cs` đang có thay đổi trước phiên này; không sửa hoặc hoàn nguyên.
- Các số dưới đây là mặc định trong mã. Giá trị serialize trong scene hoặc policy asset có thể khác; phải đối chiếu Unity Inspector trước demo.
- Chưa có kết luận rằng adaptive đang bật trong scene dựa riêng vào mã nguồn. Cần kết hợp bằng chứng Editor của phiên kiểm tra.

## Ba lớp cần phân biệt trước hội đồng

1. **Độ khó tĩnh** do người chơi chọn: Easy, Medium, Hard, Pandemonium. `DifficultyManager.cs:7`, `:35`, `:108`; bảng mặc định trong `StaticDifficultyProfiles.cs:32`.
2. **AI Director** điều khiển nhịp độ encounter: trạng thái, thời điểm spawn, nghỉ và điều kiện bảo vệ đội. Có hai đường chạy hiện tồn tại: legacy ba trạng thái và adaptive năm trạng thái. `AIDirector.cs:177`.
3. **Dynamic Difficulty** ước lượng hiệu suất rồi cập nhật hệ số điều chỉnh tại ranh giới Relax. Đây là thuật toán quy tắc và phản hồi, không có bằng chứng mô hình học máy được huấn luyện trong các nguồn đã đọc. `AdaptiveDifficultyDomain.cs:452`.

Tên trường `learningRate` ở hệ legacy không tự biến hệ thống thành machine learning: đoạn hiện tại chỉ cộng hệ số khi điểm carry vượt ngưỡng. `AIDirector.cs:611`.

## Đường chạy legacy đang có trong mã

- Chu trình `BUILD → PEAK → RELAX → BUILD`; mặc định 45 giây / 15 giây / 20 giây. BUILD cũng lên PEAK khi intensity ≥ 80; RELAX kết thúc sớm khi số địch ≤ 2. `AIDirector.cs:14`, `:204`.
- Khi ít nhất hai người nhiễm nguy kịch, PEAK giảm còn 60% thời gian và interval spawn nhân 1,3. `AIDirector.cs:215`, `:316`.
- Intensity là biến heuristic: common spawn thành công +5; special +15; địch chết −3; mỗi giây giảm 2, giới hạn 0–100. Không nên gọi đây là phép đo trực tiếp cảm xúc/stress của người chơi. `AIDirector.cs:200`, `:345`, `:551`, `:587`.
- `UpdateLearningModifiers` chạy mỗi lần địch chết khi adaptive tắt hoặc observe-only. Điểm carry = `0,4 × headshotRatio + 0,3 × min(totalKills/100,1)`. Vượt 0,3 thì tăng HP/damage +0,01, speed +0,005; có nhánh tăng thêm cho camping/headshot. Chặn trên HP=2, speed=1,5, damage=1,5. Đây là tăng một chiều trong phương thức này, không phải DDA hai chiều năm trạng thái. `AIDirector.cs:25`, `:595`, `:611`.

## Adaptive Director: cơ chế đã được viết

`AdaptiveDifficultyDomain.cs` chứa domain logic tách khỏi MonoBehaviour; đây là điểm tốt để giải thích tính kiểm thử được và tính giải thích được.

| Trạng thái | Thời gian mặc định / điều kiện | Hệ số tốc độ spawn |
|---|---|---:|
| Calm | 3 giây | 0,05 |
| BuildUp | 15 giây, có thể kéo dài theo idle và separation | 0,5 |
| Combat | 60 giây hoặc intensity ≥ 80 | 1 |
| Peak | 15 giây | 1,5 |
| Relax | 20 giây | 0 — chặn spawn mới |

Nguồn: `AdaptiveDifficultyDomain.cs:90`, `:172`, `:241`.

- Nếu HP thấp nhất ≤ 20%, hoặc thời gian từ lần down gần nhất nằm trong 0–20 giây, chuyển từ BuildUp/Combat/Peak sang Relax. Calm/Relax được miễn nhánh ép này. Đây là **điều kiện của domain**; hạn chế dữ liệu runtime được nêu phía dưới. `AdaptiveDifficultyDomain.cs:180`, `:273`.
- BuildUp thực tế: `15 × [1 + min(idleSeconds/20,1) × 0,25 + separation01 × 0,25]`; phạm vi mặc định 15–22,5 giây. Idle và tách đội **kéo dài tăng áp lực**, không làm spawn tăng tức thì trong nhánh này. `AdaptiveDifficultyDomain.cs:280`.
- Input được lấy từ `PlayerProfiler`: HP yếu nhất trên người còn sống, khoảng cách tới đồng đội gần nhất chia 30m rồi clamp, thời gian camping lớn nhất. `AIDirector.cs:464`. Profiler cập nhật vị trí mỗi 0,5 giây, trạng thái mỗi 0,1 giây và team metrics mỗi 0,2 giây theo mặc định. `PlayerProfiler.cs:48`, `:53`.

## DDA: dữ liệu và công thức chính xác

Collector cộng dồn telemetry theo stable player ID trong encounter. Snapshot được NetworkGameManager nhận và chuyển vào collector ở server. `AdaptiveDifficultyMetricsCollector.cs:27`; `NetworkGameManager.cs:117`, `:138`, `:144`.

Với từng người:

- `H = headshotHits / shotsHit`; fallback `headshotKills / kills` nếu không có hit. Đây không phải tỷ lệ headshot trên mọi viên đạn đã bắn. `AdaptiveDifficultyMetricsCollector.cs:53`.
- `D = clamp01(damageTaken / max(100, kills × 100))`. Đây là damage chuẩn hóa theo kill, không phải phần trăm HP đã mất. `:58`.
- `K = clamp01(downedCount / 3)`. `:59`.
- `A = clamp01(kills / shotsFired)`. Đây là hiệu suất kill/shot, không phải accuracy. `:60`.
- `S = 0,20H + 0,25(1−D) + 0,30(1−K) + 0,25A`. Các trọng số mặc định tổng bằng 1; phương thức còn chia cho tổng trọng số để hỗ trợ policy khác. `AdaptiveDifficultyDomain.cs:407`, `:522`.

Chỉ người có **ít nhất 20 shots và ít nhất 8 kills** được đưa vào danh sách điểm. Nếu không có người đủ bằng chứng, giữ nguyên multiplier và `HasEvidence=false`. `AdaptiveDifficultyDomain.cs:346`, `:475`.

Với danh sách đủ điều kiện:

```text
S_team = min(0,60 × median(S) + 0,40 × min(S), min(S) + 0,25)
target = clamp(1 + 0,30 × (S_team − 0,50), giới hạn tier và toàn cục)
newMultiplier = MoveTowards(oldMultiplier, target, 0,10)
```

Nguồn: `AdaptiveDifficultyDomain.cs:486`.

| Tier | Giới hạn multiplier trong evaluator |
|---|---:|
| Easy | 0,60–1,00 |
| Medium | 0,85–1,15 |
| Hard | 0,90–1,25 |
| Pandemonium | 1,00–1,50 |

Nguồn: `AdaptiveDifficultyDomain.cs:545`. Global clamp mặc định 0,60–1,50, bước tối đa mỗi Relax 0,10.

**Không được nói multiplier mặc định tự quét toàn dải 0,60–1,50.** Với gain=0,30 và score 0–1, raw target chỉ là 0,85–1,15. Tier tiếp tục cắt dải này: Easy 0,85–1; Medium 0,85–1,15; Hard 0,90–1,15; Pandemonium 1–1,15, nếu dùng các mặc định và khởi tạo 1.

**Chỉ cập nhật khi vào Relax.** Runtime lấy sample, Evaluate, ghi multiplier và xóa bộ đếm encounter tại thời điểm đó. `AIDirector.cs:367`.

Ví dụ minh họa tính toán, không phải số liệu chơi thật: H=0,5; D=0,1; K=0; A=0,2 → S=0,675. Một người đủ điều kiện ở Medium cho target=1,0525. Từ multiplier=1, bước tiếp theo là 1,0525, vì nhỏ hơn giới hạn bước 0,10.

## Điều khiển đầu ra: thay đổi gì trong game

`SpawnController.cs:35` tổng hợp phase, độ khó tĩnh, số người và DDA:

```text
interval = max(minInterval,
  baseInterval × staticInterval / phaseRate / playerPressure / DDA)
maxAlive = max(1, round(baseMaxAlive × staticMaxAlive × DDA))
specialChance = clamp01(staticSpecialChance × DDA)
```

RELAX chặn spawn mới; không xóa toàn bộ địch đang sống. Khi đã đạt maxAlive thì ngừng spawn mới, không despawn để ép xuống cap. `SpawnController.cs:62`.

Adaptive spawn truyền DDA vào **HP và damage của common zombie**, truyền 1 vào speed. `AIDirector.cs:430`. Factory còn nhân thêm độ khó tĩnh rồi đường cong số người. `ZombieFactory.cs:70`, `:234`.

Vì vậy với common zombie:

```text
HP = baseHP × staticHP × DDA × playerCountHealthScale
damage = baseDamage × staticDamage × DDA × playerCountDamageScale
speed = baseSpeed × staticSpeed
```

Đường cong số người mặc định: HP common [1;1,35;1,70;2,05], damage [1;1,08;1,15;1,20], spawn pressure [1;1,20;1,40;1,60]. `EnemyScaling.cs:65`.

Đây là nhiều kênh cùng khuếch đại áp lực; `DDA=1,10` không đồng nghĩa trải nghiệm chỉ khó hơn đúng 10%. Chưa có bằng chứng thực nghiệm định lượng tổng tác động.

## Các giới hạn quan trọng cần nói thật

1. **Bật hệ nào phải kiểm tra trong Unity.** Mã mặc định `enableAdaptiveDirector=false`, `adaptiveObserveOnly=true`. Hai cờ quyết định demo đang đi đường nào. `AIDirector.cs:47`, `:183`.
2. **Observe-only chưa hoàn toàn cách ly hành vi theo mã hiện tại.** `UpdateAdaptiveDirector` vẫn map phase adaptive vào legacy khi phase đổi; sau đó vòng Update lại gọi legacy pacing/spawning. Vì vậy không nên khẳng định chế độ này chỉ ghi log mà tuyệt đối không ảnh hưởng nhịp độ. `AIDirector.cs:185`, `:360`.
3. **Grace period sau down chưa nối đầy đủ.** Domain hỗ trợ recentDownedSeconds≤20, nhưng BuildDirectorInput chỉ truyền 0 khi đang Downed; mọi lúc khác là 999. Chưa ghi lại timestamp lần down/revive ở đây. `AIDirector.cs:469`, `:486`.
4. **Adaptive special có khoảng trống tích hợp với mặc định.** Gate đòi BuildUp/Peak đã trôi ≥15 giây. Peak mặc định kết thúc ở 15 giây trước khi trả decision nên không mở gate ở Peak; BuildUp kéo dài có thể mở gate nhưng runtime `TrySpawnSpecial` lại chỉ chấp nhận legacy PEAK. Đây là suy luận trực tiếp từ mã, chưa kiểm chứng bằng một lần chạy có theo dõi spawn. `AdaptiveDifficultyDomain.cs:215`, `:263`; `AIDirector.cs:535`.
5. **“Bảo vệ người yếu nhất” cần nói rõ tập hợp.** Công thức performance chỉ tính người đủ 20 shots và 8 kills. Người rất yếu chưa đạt 8 kills có thể bị loại khỏi điểm đội; đường bảo vệ bằng HP vẫn riêng. `AdaptiveDifficultyDomain.cs:346`, `:475`.
6. **DDA common khác special.** Đã xác nhận multiplier truyền vào HP/damage common; đường `TrySpawnSpecial()` không truyền multiplier này. Không tuyên bố mọi thuộc tính của mọi enemy đều được DDA chỉnh. `AIDirector.cs:430`, `:543`.
7. **Telemetry cần phiên server có người chơi đã đăng ký.** Đường nhận sample hiện tại đòi NetworkManager listening/server, registry record và PlayerObject hợp lệ. Chỉ mở một scene offline không đảm bảo collector có dữ liệu. `NetworkGameManager.cs:138`.
8. **Không nên hứa “không thể bị khai thác”.** Ngưỡng bằng chứng, giới hạn bước và clamp hạn chế dao động; chúng không chứng minh chống việc người chơi cố tình thao túng chỉ số.
9. **Chưa có bằng chứng lợi ích người chơi.** Cấu trúc và công thức đã có, nhưng việc tăng enjoyment, giảm boredom, giảm wipe cần một phép so sánh có kiểm soát.

## Kiểm thử và bằng chứng còn thiếu

- Đã liệt kê C# trong `Assets/Tests`, tìm tên `DirectorStateMachine`, `DynamicDifficultyEvaluator`, `AdaptiveDifficultyMetricsCollector`, `SpawnController`, `AdaptiveDirector`, `DynamicDifficulty`, `TeamPerformanceScore`; không thấy kết quả trong phạm vi này. Không suy ra toàn project chưa từng có test/harness bên ngoài.
- Có `Assets/Tests/Editor/UI/SettingsDifficultyRegressionTests.cs`, và báo cáo `Documents/Settings-Difficulty-Audit-2026-10-05.md` ghi lần chạy trước 9/9 pass cho difficulty UI/rubberbanding. Đây không phải test bằng chứng cho evaluator adaptive, cũng không phải kết quả vừa chạy trong phiên này.
- Bộ tối thiểu nên chuẩn bị trước khi khẳng định demo adaptive: chuyển đủ 5 state; HP yếu ép Relax; chưa đủ sample giữ nguyên; score tốt/xấu cho đúng dấu; cap theo tier; bước ≤0,10; Relax không spawn; bật adaptive thực sự thay interval/HP common; kiểm tra special và grace sau revive.
- Kết quả A/B cần ghi rõ ai chơi, scene, độ khó tĩnh, số người, trạng thái bật adaptive, thứ tự lượt chơi, thời lượng. Thu time-in-state, multiplier, actual spawn interval, down/death, damage, completion time và đánh giá chủ quan; không trình bày số giả như kết quả thật.

## Cách phát biểu an toàn trước hội đồng

“Em tách bài toán thành điều khiển nhịp độ và hiệu chỉnh áp lực. Director quyết định khi tăng áp lực và khi cho đội nghỉ; DDA dùng telemetry để điều chỉnh một hệ số có giới hạn vào cuối encounter. Cách tiếp cận hiện tại là hệ quy tắc có thể giải thích và kiểm chứng. Mã đã có evaluator, state machine và đường tích hợp spawn; trạng thái bật thực tế và hiệu quả với người chơi phải được chứng minh qua demo cùng dữ liệu đánh giá, không chỉ qua sơ đồ.”

