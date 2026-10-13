# Bàn giao HUD FPS — 05/10/2026

Trạng thái: hoàn thành HUD-01–HUD-10 và nghiệm thu trong phạm vi fixture đã mô tả.

Triển khai theo [nghiên cứu](HUD-Research-2026-10-05.md) và [bố cục đã thống nhất](HUD-Layout-Study-2026-10-05.png), áp dụng `game-ui-ux`, `game-ui-design`, `unity-ui`. Giữ UGUI/TMP, input actions và kiến trúc campaign hiện có.

## Kết quả theo backlog

| ID | Trạng thái | Thay đổi | Bằng chứng |
|---|---|---|---|
| HUD-01 | Đạt | Bỏ nhãn inventory thường trực; chia prompt, progress, feedback và phụ đề thành các hàng riêng | Ảnh gameplay `stress-*`, Inventory `missions-*`; assertion nhóm HUD không overlap |
| HUD-02 | Đạt | Tracker trên trái chỉ một mục tiêu; checklist chương nằm trong tab Missions của Inventory | `missions-*`, test lựa chọn và điều hướng |
| HUD-03 | Đạt | Tracker dùng plate tối và chữ có outline; khoảng trống trên giữa giữ cho gameplay | `stress-*`, `ads.png`, palette contrast |
| HUD-04 | Đạt | Chữ cần đọc nhanh tối thiểu 16 px tại 720p; tăng vùng chứa và wrap tên nhiệm vụ | Assertion font hiển thị, overflow/truncation trong sáu cấu hình |
| HUD-05 | Đạt | Prompt lấy effective binding theo thiết bị; `G` ném, `3` đổi loại; reset và rebind phát sự kiện | HudRegressionTests, HudInputPlayModeTests; slot grenade chỉ hiện binding ném trên loại đang chọn |
| HUD-06 | Đạt | Bốn slot dưới phải, icon/count/binding; selected underline, progress ring; số `0` và gạch chéo khi hết đồ | `stress-*`, `treatment.png`; bind idempotent, subscription một lần |
| HUD-07 | Đạt | HP/infection/treatment dưới trái, squad phía trên; ammo chính lớn hơn dự trữ | `stress-*`, `squad-downed.png`, `treatment.png` |
| HUD-08 | Đạt | Giữ reticle hiện có sau kiểm chứng outline trên ba nền; giữ ADS/scope/spread | `reticle-contrast-review.png`, `scope-presentation.png`; sai lệch tâm được đo trong matrix |
| HUD-09 | Đạt | Reference 1920×1080, scaling theo chiều cao, safe area; ẩn HUD sau overlay/cinematic; phụ đề có layer và strip riêng khi mở overlay | Matrix ba độ phân giải × 100%/125%, cinematic, downed, spectating, pause/settings, debrief |
| HUD-10 | Đạt | Tracker/Missions/marker dùng cùng TrackedObjectiveId; lựa chọn cục bộ ổn định; route NavMesh, waypoint khi khuất, indicator mép, cue cao/thấp | Fixture live GameScene, FactoryInterlock, partial/invalid path, camera 360°, checkpoint/chapter; không hoàn thành nhiệm vụ bằng proximity |

Khoảng cách `ROUTE` chỉ hiện trên đường NavMesh hoàn chỉnh. `TO TARGET` biểu thị khoảng cách thẳng; tracker báo `Route unavailable` khi không có đường hoàn chỉnh. Cue `HIGHER`/`LOWER` dùng ngưỡng 2,5 m, không suy số tầng.

Hướng dẫn chuẩn bị được đóng khi campaign chuyển phase để countdown trở lại tracker. Khi ADS/bắn, tracker thu một dòng; countdown vẫn nằm trong dòng đang hiển thị. Root HUD trung tâm cùng reticle/blood edge bị ẩn trong cinematic; scope/reflex nằm ngoài root này và được kiểm soát riêng.

## Ảnh đối chiếu

- [Trước sửa — ảnh gameplay người dùng](HUD-Evidence/before-user-gameplay.png).
- [Gameplay 720p / 125%](HUD-Evidence/stress-1280x720-125.png), [1080p / 100%](HUD-Evidence/stress-1920x1080-100.png), [ultrawide / 125%](HUD-Evidence/stress-3440x1440-125.png).
- [Missions 720p / 125%](HUD-Evidence/missions-1280x720-125.png), [1080p](HUD-Evidence/missions-1920x1080-100.png), [ultrawide](HUD-Evidence/missions-3440x1440-125.png).
- [Squad downed](HUD-Evidence/squad-downed.png), [đồng đội downed sau lưng](HUD-Evidence/squad-downed-behind.png), [điều trị](HUD-Evidence/treatment.png), [ADS](HUD-Evidence/ads.png), [scope Operator](HUD-Evidence/scope-presentation.png).
- [Cinematic](HUD-Evidence/cinematic-presentation.png), [downed](HUD-Evidence/downed.png), [spectating](HUD-Evidence/spectating.png), [debrief](HUD-Evidence/debrief.png).
- [Ready](HUD-Evidence/preparation-tracker.png), [countdown](HUD-Evidence/countdown-tracker.png), [navigation sau lưng](HUD-Evidence/navigation-behind.png), [navigation không có route](HUD-Evidence/navigation-invalid.png).
- [Reticle trên ba nền](HUD-Evidence/reticle-contrast-review.png), [contrast palette](HUD-Evidence/contrast-palette.json).

Ảnh `stress-*` và `missions-*` là bằng chứng gameplay của fixture nghiệm thu cuối. Các ảnh `Exploring-*`, `inventory-*`, `Insertion-*`, `unbound-*` và `baseline-authored.png` là các capture trung gian; không dùng để đánh giá bản cuối. Ảnh contrast là crop phóng to nearest-neighbor, không đổi reticle trong game.

## Kiểm thử và giới hạn bằng chứng

| Nhóm kiểm thử | Kết quả đã lưu | Receipt |
|---|---|---|
| SurvivalUiRegressionTests | 6/6 | [XML](HUD-Evidence/SurvivalUiRegressionTests.xml) |
| HudRegressionTests — selection/projection/rebind/reset | 4/4 | [XML](HUD-Evidence/HudRegressionTests.xml) |
| HudInputPlayModeTests — overlay/focus/device switch | 2/2 | [XML](HUD-Evidence/HudInputPlayModeTests.xml) |
| HudGameplayAcceptanceTests.GameplayStatesAndLayoutRemainReadable | 1/1 | [XML](HUD-Evidence/HudGameplayStatesTests.xml), [assertions](HUD-Evidence/GameplayStatesAndLayoutRemainReadable.txt) |
| HudGameplayAcceptanceTests.NavigationUsesLiveSceneAndInvalidatesConsistently | 1/1 | [XML](HUD-Evidence/HudNavigationAcceptanceTests.xml), [assertions](HUD-Evidence/NavigationUsesLiveSceneAndInvalidatesConsistently.txt) |
| CampaignRulesTests | 11/11 | [XML](HUD-Evidence/CampaignRulesTests.xml) |
| CampaignLifecycleTests | 2/2 | [XML](HUD-Evidence/CampaignLifecycleTests.xml) |
| CampaignMorgueNavigationTests | 1/1 | [XML](HUD-Evidence/CampaignMorgueNavigationTests.xml) |

28 test độc lập có receipt PASS ([tổng hợp](HUD-Evidence/test-summary-final.json)). Hai method acceptance được chạy riêng; không cộng các lần chạy lặp lại. Các lần thất bại khi sửa fixture và filter khớp 0 test không được tính là PASS. Kiểm tra cửa dùng thời hạn chờ carving NavMesh 5 giây, thay cho sleep cố định 1,5 giây; assertion vẫn yêu cầu đường crossing thật sự mở. [Kích thước PNG cuối](HUD-Evidence/capture-dimensions-final.json) xác nhận đủ 12 ảnh gameplay/Missions ở đúng ba độ phân giải và hai mức scale.

Compile runtime cuối thành công: **0 lỗi, 54 warning** ([diagnostics](HUD-Evidence/compile-runtime-final.json)). [Snapshot recompile incremental](HUD-Evidence/compile-final.json) không phải clean rebuild toàn bộ project. [Các dòng warning trong log phiên làm việc](HUD-Evidence/compile-warnings-existing.txt) được giữ để đối chiếu, có thể chứa diagnostic của nhiều lần compile. Không tuyên bố toàn dự án có 0 warning. [Readback Unity và Console](HUD-Evidence/unity-readback-final.json) xác nhận scaler/anchor và Console không có error.

Fixture `HudGameplayAcceptanceTests` khởi động local host và tải GameScene thật. Nó dùng input device ảo của Unity Input System, prefab player, native PlayerHealth/SurvivalInventory, scope Operator và NavMesh của scene. Các trạng thái campaign được đóng băng/inject để kiểm tra trình bày; checkpoint retry gọi API thật với checkpoint trong bộ nhớ. Không thay đổi asset, luật campaign hoặc file checkpoint trong bước retry.

Ba đồng đội là actor từ prefab thật với ownership ID tạm; roster/row binding được fixture gán rõ. Đây là kiểm chứng trình bày squad, không phải nghiệm thu bốn máy/client mạng. Device-switch gamepad được kiểm tra bằng layout gamepad kế thừa ảo; chưa có phiên chơi bằng controller vật lý. Kiểm chứng camera chuyển động/projection chạy trong Game View; chưa thay thế được playtest mức độ dễ đọc và điều hướng với người chơi mới.

Text chính/plate trên nền trắng đạt 14,7:1; muted/plate đạt 7,94:1. Status mục tiêu đang tracking dùng text sáng, đạt 6,82:1 trên nền selected thay cho mức 3,69:1 của muted cũ. Đo palette bổ sung kiểm chứng hình ảnh thực tế; không coi contrast của palette là phép đo mọi pixel của ảnh.

## Phạm vi bảo toàn

- Authoring chỉ áp dụng HUD GameScene qua `Tools/FPS/UI/Apply Gameplay HUD Only`, có save gate và Undo; không chạy `ApplyAll()`.
- Không thêm minimap, compass, route glow, dependency, RPC, save schema hoặc setting HUD scale. 125% chỉ là fixture nghiệm thu.
- Bảo toàn thay đổi Warden, Weapon.cs, package, test shutdown/weapon và Documents/checklist.md đã có trong workspace. Không commit/push các thay đổi khác của người dùng.
- Canvas/phần tử authoring đã được đọc lại bằng Unity-Skills sau áp dụng. Không sửa scene/prefab bằng raw YAML.

Để chạy lại: Unity Test Runner với từng class trong bảng kết quả; `HudGameplayAcceptanceTests` cần Game View được hiển thị. Công cụ `Tools/FPS/UI/HUD Review` cung cấp local host, resolution, scale fixture và capture. Test/network review dùng cổng loopback khả dụng, không dùng cổng cố định đang bị một Unity process khác chiếm.

## Bổ sung phần hình ảnh

Lượt sửa bằng asset cho HUD và Inventory theo góp ý mới được ghi tại [HUD-Asset-Skin-2026-10-05.md](HUD-Asset-Skin-2026-10-05.md). Ảnh/receipt của lượt này nằm trong `HUD-Evidence/AssetSkin`; các ảnh cũ là bằng chứng cho lượt bố cục/hành vi trước đó.
