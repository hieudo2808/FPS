# Kiểm định runtime: AI Director và Dynamic Difficulty

**Ngày chạy: 13/10/2026. Unity 6000.5.6f1, GameScene, host nội bộ, một người chơi, độ khó Medium.**

## Kết luận có thể bảo vệ bằng bằng chứng

**Nhánh adaptive có tác động thật khi được bật: nó đổi pha, giữ DDA đến ranh giới Relax, thay nhịp spawn và áp dụng HP/sát thương lên quái mới. Tuy nhiên, GameScene hiện lưu cấu hình adaptive tắt. Có một lỗi gate special trong policy mặc định và các giới hạn tích hợp cần xử lý trước khi bảo vệ như một tính năng hoàn chỉnh.**

Đã chạy domain logic thật trong Unity: **8 kiểm tra đạt, 0 thất bại**, kèm **1 bất thường về special gate**. Trong lượt runtime chính, ghi **977 snapshot** và **27 sự kiện common zombie spawn thành công**. Log quan sát đủ Calm, BuildUp, Combat, Peak, Relax. Tám kiểm tra gồm bảy nhóm kiểm tra hành vi và một kiểm tra đọc cấu hình đang mở; con số này không phải số ca kiểm thử end-to-end toàn game.

| DDA tại lúc spawn | Số quái được ghi | MaxHealth đọc từ component | AttackDamage đọc từ component | NavMeshAgent.speed |
|---:|---:|---:|---:|---:|
| 0,90 | 5 | 90 | 9 | 5 |
| 1,00 | 4 | 100 | 10 | 5 |
| 1,10 | 18 | 110 | 11 | 5 |

Các số này lấy tại callback `ZombieFactory.OnZombieSpawned`, sau khi factory áp dụng stats. Chúng không phải giá trị được tính rồi giả định rằng game đã nhận.

![Stats đọc từ quái spawn thật](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/Captures/runtime-stats.png)

**Chưa có bằng chứng rằng trải nghiệm người chơi tốt hơn.** Actor trong lượt đo không thực hiện một trận chiến người thật: HP được quan sát luôn là 100, không có dữ liệu bắn/hạ quái tự nhiên đủ ngưỡng. Dữ liệu hiệu suất dùng để kích hoạt DDA là fixture có kiểm soát, được ghi rõ trong log can thiệp.

## Đã đo như thế nào

### Quan sát cấu hình và lượt baseline

Đọc scene và component qua Unity Skills, không đọc YAML scene/prefab. Cấu hình lưu của `AIManager/AIDirector`:

- `enableAdaptiveDirector = false`.
- `adaptiveObserveOnly = true`.
- Hai policy asset đều `null`, nên domain dùng mặc định trong code khi được khởi tạo.
- Base interval 2 giây; base cap 30; minimum interval 0,5 giây.

Baseline chạy bằng công cụ host nội bộ đã có của project. Đường legacy chuyển BUILD/PEAK/RELAX, DDA adaptive giữ 1. Đã ghi ba common spawn thành công với HP 100, damage 10, speed 5. Trong insertion, campaign chặn spawn; điều này là điều kiện gameplay, không phải bằng chứng Director bị hỏng.

[Cấu hình đọc từ Unity](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/unity-director-snapshot.json) · [Log baseline](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/Captures/natural-20261013-071906-863-594927ae.jsonl)

### Lượt runtime có kiểm soát

Giữ nguyên code gameplay. Thêm công cụ **Editor-only** để đọc trạng thái, xuất log và thực hiện các can thiệp chủ động trong một phiên Play Mode dùng cho kiểm tra:

1. Host nội bộ tải GameScene và spawn prefab Brimstone thật qua công cụ review có sẵn.
2. Bỏ qua cinematic insertion bằng phase handler của campaign; đánh dấu can thiệp.
3. Chuyển actor tới **recovery marker có sẵn của map**, chọn vị trí mà `DirectorSpawnService.IsAnchorValid` xác nhận có anchor hợp lệ. Giữ các guard về đất, khoảng cách, vật cản, capsule và NavMesh.
4. Bật adaptive ở runtime, tắt observe-only. Dùng state machine thật để đặt điều kiện đầu Combat; đánh dấu việc ép pha.
5. Ghi cả spawn tự chạy qua Update và các lần chủ động hết timer. Log riêng ghi từng lần ép timer, nên có thể phân biệt hai loại.
6. Bơm snapshot hiệu suất vào collector, gọi handler adaptive thật để đi qua Peak → Relax. Handler thật chạy evaluator, đồng bộ multiplier và reset encounter.
7. Cho Update tiếp tục chạy để quan sát chu kỳ, spawn thành công, stats và population. Lặp với dữ liệu kém để kiểm tra chiều giảm.

Việc bơm trực tiếp vào collector **bỏ qua thu thập sự kiện bắn/kill và kiểm tra player registry**. Nó xác nhận đoạn collector → evaluator → Director → factory, chưa xác nhận toàn bộ chuỗi từ hành động người chơi qua telemetry server. Host review cũng không thay thế một lượt đi qua lobby/multiplayer sản phẩm.

![Ảnh Game View thật: adaptive hoạt động, DDA 1,10, 22 quái sống](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/Captures/director-runtime-dda-110.png)

Ảnh cho thấy quái được tạo trong scene dù không nằm trước camera. Việc spawn được kiểm tra bằng event/component và số lượng sống, không suy ra từ hình ảnh nhìn thấy trên màn hình.

## Các kết quả runtime quan trọng

### 1. DDA chỉ thay đổi tại Relax và điều chỉnh hai chiều

Fixture tốt: 20 shots, 20 hits/headshot hits, 10 kills, không damage/down. Collector cho score 0,875; target mặc định là 1,1125. Bước tối đa 0,10 nên handler thực tế đưa **1,00 → 1,10** tại Relax.

Fixture kém: 80 shots, 8 kills, 800 damage, 3 down, không headshot. Score là 0,025; target Medium là 0,8575. Hai lần chạy handler tại Relax đưa **1,10 → 1,00 → 0,90**. Bộ đếm encounter sau mỗi lần được reset về 0.

Trong Combat, multiplier được giữ nguyên giữa các boundary. Có các chu kỳ tự chuyển pha tiếp theo; toàn bộ timeline cũng chứa những ranh giới bị ép theo log can thiệp, nên không trình bày toàn bộ biểu đồ như một playtest tự nhiên.

![Timeline từ các snapshot runtime](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/Captures/runtime-timeline.png)

### 2. Relax ngừng spawn mới, quái cũ vẫn tồn tại

Đã thử hết timer khi đang Relax:

- Lần DDA 1,10: population **4 → 4**, không có event common spawn mới từ lần thử.
- Lần DDA 0,90: population **22 → 22**, không có event common spawn mới từ lần thử.

Relax tạo khoảng nghỉ bằng việc chặn bổ sung địch; nó không làm biến mất địch đang sống, không hồi máu cho người chơi và không đảm bảo ngay lập tức hết áp lực.

### 3. Nhịp spawn thực tế thay đổi đúng hướng

Chỉ lấy những cặp common spawn liên tiếp trong Combat, cùng multiplier và cách nhau dưới 5 giây, để loại các khoảng không có anchor hợp lệ hoặc các lần đổi điều kiện dài:

| DDA | Interval theo công thức, một người ở Medium | Trung vị interval của các cặp event đo được | Số cặp |
|---:|---:|---:|---:|
| 0,90 | 2,222 giây | 2,228 giây | 4 |
| 1,00 | 2,000 giây | 2,016 giây | 3 |
| 1,10 | 1,818 giây | 1,826 giây | 17 |

Sai khác nhỏ phù hợp với việc timer được xử lý theo frame. Đây là kiểm chứng nhịp spawn trong các đoạn có spawn thành công; không phải phép đo throughput toàn trận hoặc A/B về trải nghiệm. Các nhóm có số mẫu và vị trí actor khác nhau.

### 4. Population còn phụ thuộc môi trường

Có đoạn đã hết timer nhưng không spawn thêm dù chưa đạt cap. Đổi actor sang recovery marker khác có anchor hợp lệ giúp factory spawn tiếp. Vì vậy cần phân biệt:

```text
Director muốn spawn
→ không ở Relax và chưa đạt cap
→ có anchor hợp lệ theo campaign/địa hình/visibility/NavMesh
→ factory tạo quái thành công
→ event xác nhận stats và population
```

Trong lượt chính, khoảng cách ngắn nhất từ vị trí spawn thành công tới actor được ghi trong khoảng **47,02–98,82 m**. Đây là khoảng cách thẳng đo ở sự kiện spawn, không phải độ dài đường đi hoặc thời gian quái tiếp cận. Ngưỡng map đang dùng là 22–110 m; không dùng mặc định trong source để mô tả scene này.

Sau nhóm DDA 0,90, population đạt 27. Đây đúng bằng cap được tính cho Medium: `round(30 × 0,90)`. Bộ đếm Director và số `EnemyHealth` sống quan sát được khớp nhau trong lượt chính. Monitor không đo toàn bộ chi phí CPU, NavMesh hay render.

### 5. Tác động lên người chơi và môi trường cần hiểu đúng

| Đối tượng | Đã xác nhận | Giới hạn kết luận |
|---|---|---|
| Quái mới | HP/damage tăng hoặc giảm theo DDA; speed không nhận DDA trong đường common này | Chưa đo số viên đạn cần hạ, thời gian hạ quái hoặc hit thật lên người chơi |
| Người chơi | Thay đổi mật độ địch tiềm năng, khoảng nghỉ và stats đối thủ | HP actor luôn 100 trong log; chưa chứng minh stress, fairness, enjoyment hoặc survival rate |
| Không gian map | Spawn dùng anchor sẵn và các guard của môi trường; vị trí actor ảnh hưởng khả năng spawn | Director này không sinh map mới hoặc tự sửa tường/cửa/ánh sáng/địa hình |
| Quái đang sống | Relax không xóa quái; nhóm quái được tạo ở từng mức nhận stats tại spawn | Đường adaptive được kiểm tra áp dụng stats cho quái mới, không có bước rescale toàn bộ quái cũ |
| Special/hazard | Có gate và đường gọi special trong code | Gate mặc định có lỗi; không dùng hiệu ứng special/hazard làm bằng chứng DDA đã điều khiển đúng |
| Loot/tiếp tế | Có mô tả dự kiến trong GDD | Chưa có bằng chứng runtime DDA đổi loot; không đưa thành kết quả đã triển khai |

Không diễn giải `M=1,10` là “trải nghiệm khó hơn đúng 10%”: HP, damage, interval và population thay đổi đồng thời, còn vị trí, loại quái, vũ khí, số người và nhiệm vụ đều ảnh hưởng kết quả.

## Những điểm chưa đúng hoặc cần sửa trước khi bảo vệ

### A. Scene mặc định chưa bật tính năng được mô tả

Đây là phát hiện từ cấu hình Unity thật. Nếu trình bày GameScene hiện tại mà không kiểm tra hai cờ, bạn đang demo Director legacy. Bật adaptive ở các lượt kiểm tra chỉ là thay đổi runtime; cấu hình authored được giữ như ban đầu sau khi thoát Play.

### B. Special gate mặc định không có cửa sổ Peak hợp lệ

Kiểm tra domain chạy **1.000 bước × 0,25 giây**, input đội khỏe, không idle/separation: **0 bước mở gate**, **0 bước mở gate trong Peak**.

Gate yêu cầu thời gian pha ≥ 15 giây, nhưng Peak mặc định cũng kết thúc ở 15 giây; state machine chuyển sang Relax trước khi trả decision. BuildUp mặc định kết thúc ở 15 giây nên cũng không có cửa sổ. Khi BuildUp kéo dài, domain có thể mở gate ở đó, nhưng `TrySpawnSpecial` runtime yêu cầu legacy PEAK.

Đây là lỗi logic về khả năng mở gate trong cấu hình mặc định, không phải kết luận từ “chưa nhìn thấy special nên chắc chắn không có”. Pha/trigger campaign khác vẫn có thể spawn special qua đường riêng; điều đó không sửa được gate adaptive này.

Nguồn: [DirectorStateMachine/GetDecision](/E:/Unity/Project/FPS/Assets/FPS/Features/AI/Runtime/AdaptiveDifficultyDomain.cs) · [AIDirector/TrySpawnSpecial](/E:/Unity/Project/FPS/Assets/FPS/Features/AI/Runtime/AIDirector.cs) · [Kết quả kiểm tra](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/Captures/isolated-checks-20261013-071818-909.json)

### C. Observe-only chưa phải đối chứng hoàn toàn độc lập

Đối chiếu source: `UpdateAdaptiveDirector` vẫn map pha adaptive sang pha legacy khi state đổi, rồi nhánh observe-only tiếp tục gọi pacing/spawning legacy. Vì vậy có khả năng kết quả quan sát đã tác động tới nhịp trận.

Lượt runtime chính dùng `observeOnly=false`; chưa chạy A/B cô lập để lượng hóa mức ảnh hưởng của observe-only. Không dùng nó làm baseline “không có tác động” trước khi kiểm chứng/cách ly phần ghi pha.

### D. Grace sau down/revive chưa có dữ liệu timestamp đầy đủ

Domain kiểm tra grace 20 giây đúng ở boundary. Nhưng runtime `BuildDirectorInput` truyền 0 khi actor đang Downed, và 999 ở các tình trạng khác; chưa thấy timestamp lần down gần nhất trong đường này. Vì thế bảo vệ lúc đang down và bảo vệ thêm 20 giây sau revive là hai tuyên bố khác nhau. Phần sau chưa được nối đúng bằng input này.

Đây là finding source; chưa chạy tình huống down/revive người chơi trong lượt hiện tại.

### E. DDA bảo vệ người yếu nhất trong tập người đủ mẫu

Chỉ người có ít nhất **20 shots và 8 kills** được tính vào score đội. Người quá yếu hoặc mới vào trận có thể chưa đủ kill để được đưa vào tập. Guard HP của Director là đường riêng; không gọi công thức score này là bảo vệ mọi đồng đội trong mọi trường hợp.

### F. Tài liệu thiết kế cần đồng bộ với code

GDD đề xuất `0,6 + 0,9 × score`, bước ±0,15. Code hiện dùng `1 + 0,30 × (score − 0,5)`, bước ±0,10, có tier envelope và giới hạn theo người yếu nhất. Với mặc định và khởi tạo 1, target raw chỉ 0,85–1,15; dải clamp 0,6–1,5 không có nghĩa multiplier thực tế tự quét hết dải đó.

Không gọi `learningRate` trong legacy là mô hình machine learning đã huấn luyện. Các thuật toán đang kiểm tra là FSM, heuristic và điều khiển phản hồi có giới hạn.

## Công cụ trực quan đã có trong Unity

Mở **Tools → AI Director → Runtime Monitor**. Cửa sổ hiển thị mode thật, pha legacy/adaptive, multiplier, proposal, số quái trong scene, bộ đếm Director, HP người chơi và mẫu encounter có đủ ngưỡng hay không.

Trong Play Mode:

- **Show Runtime HUD**: đưa các giá trị đọc được lên Game View; HUD không chỉnh gameplay và không bắt raycast UI.
- **Start Natural Capture / Stop Capture**: ghi snapshot và event spawn của một lượt bạn chơi bình thường, không dùng các lệnh Controlled trong lượt này.
- **Start Controlled Capture**: dùng cho các thử nghiệm bơm input/ép pha; header ghi rõ loại bằng chứng.
- **Export Current Snapshot**: lưu trạng thái hiện tại.
- **Run Isolated Checks**: chạy lại các kiểm tra domain thật, không mở Test Runner.

Các lệnh **Controlled** chỉ dùng trong một Play Mode dùng để thử nghiệm: Activate Combat, Good/Poor Telemetry Then Relax, Resume Combat, Attempt One Common Spawn, Skip Campaign Intro, Move Player To Valid Map Position. Chúng thay trạng thái runtime thật. Muốn quay lại cấu hình authored, thoát Play Mode; không lưu các thay đổi thử nghiệm thành cấu hình sản phẩm.

Công cụ được cài trong [Assets/Editor/DirectorVerification](/E:/Unity/Project/FPS/Assets/Editor/DirectorVerification/DirectorRuntimeMonitor.cs); assembly chỉ dành cho Editor. Nguồn và script dựng lại biểu đồ nằm trong [Tools](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/Tools/render_evidence.py). Không thêm dependency.

Monitor poll mỗi 0,5 giây theo thời gian Editor; có thể bỏ lỡ chuyển pha ngắn. Spawn common được ghi bằng event nên không dựa riêng vào polling để đếm spawn. Chưa ghi event special, spawn request bị từ chối, damage impact hoặc toàn bộ profiler.

## Lộ trình chứng minh tiếp và cách nói trong 5–7 phút

Ưu tiên trước buổi bảo vệ: xác định mode sản phẩm muốn bật; sửa/cân chỉnh special gate; nối grace timestamp; kiểm tra observe-only; sau đó ghi một lượt qua lobby thật với bắn/kill/down để chứng minh toàn chuỗi telemetry, rồi mới đánh giá người chơi hoặc multiplayer.

Bạn có thể dùng chính ảnh và biểu đồ trong Markdown này để giải thích:

1. **Một phút:** Director quyết định nhịp; DDA hiệu chỉnh áp lực ở cuối encounter. Cho thấy mode thật trên HUD.
2. **Hai phút:** đi qua Relax, chỉ multiplier thay đổi ở boundary; đưa bảng quái spawn thật 90/100/110 HP và 9/10/11 damage.
3. **Một phút:** chỉ timeline population và interval đo được; giải thích Relax giữ quái cũ, môi trường có thể từ chối spawn.
4. **Một–hai phút:** nêu giới hạn bằng chứng và finding gate. Phân biệt kiểm chứng cơ chế đã đạt với lợi ích trải nghiệm còn cần playtest.

Câu kết luận phù hợp: **“Em đã xác nhận đường áp dụng trong runtime bằng event spawn và component thật. Điều chỉnh diễn ra tại ranh giới encounter, có giới hạn và có thể giải thích. Phiên kiểm tra có kiểm soát chưa chứng minh trải nghiệm tốt hơn; em cũng đã chỉ ra các khoảng trống cấu hình/tích hợp cần hoàn thiện.”**

## Hồ sơ bằng chứng và trạng thái cuối

- [Log runtime chính — 977 snapshot, 27 common spawn](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/Captures/controlled-20261013-073036-731-a89c0ef0.jsonl).
- [Log can thiệp, có UTC và thời gian game](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/Captures/interventions.txt).
- [Tổng hợp và các cặp interval](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/Captures/runtime-summary.json).
- [Kiểm tra thuật toán trong Unity](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/Captures/isolated-checks-20261013-071818-909.json).
- [Phụ lục source đã thu hẹp](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/code-evidence.md), [đối chiếu docs](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/docs-evidence.md), [kế hoạch đánh giá](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/evaluation-plan.md). Các phụ lục này được viết 09/10; kết quả chạy mới là các capture 13/10 ở trên.

Codebase Memory đã được thử ở bước khám phá nhưng gặp lỗi kiểm tra DACL, nên không có kết quả graph/index để xác nhận coverage. Việc đối chiếu source dùng các file và dependency đã được thu hẹp; trạng thái scene/prefab/runtime dùng Unity Skills.

Các phiên thử đã kết thúc, HUD runtime đã được gỡ. Đã đưa Editor về GameScene và gọi save gate cho authored scenes/assets. Cấu hình gameplay không được sửa để che các finding. Trạng thái cuối được lưu riêng trong [final-unity-state.json](/E:/Unity/Project/FPS/Documents/AI-Director-Defense/Captures/final-unity-state.json).
