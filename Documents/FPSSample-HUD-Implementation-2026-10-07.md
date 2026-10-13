# FPSSample HUD — hoàn tất kiểm chứng 08/10/2026

Đã tích hợp 9 ảnh FPSSample vào HUD đang dùng. Giữ bố cục, màu chủ đạo, đạn hữu hạn và các luồng gameplay. `UI_Hud_Inifity` chỉ là asset dự trữ; không xuất hiện trong HUD. Âm thanh không thuộc thay đổi này.

## Asset và authoring

- Bộ 10 PNG cùng LICENSE.md, SOURCE.md và source-assets.json nằm tại `Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample`. Folder được chuyển bằng AssetDatabase.MoveAsset; cả 10 GUID và nội dung PNG được giữ nguyên. Manifest dùng đường dẫn đích mới. Evidence: `.codex/fpssample-hud-review/content-verification.json`, `move-verification.json`, `original-guids.json`.
- Scene và UI runtime dùng chung asset qua `HudSkin.Get("FPSSample/…")`. `Tools/FPS/UI/Apply Gameplay HUD Only` cập nhật GameScene, chạy lại không sinh object trùng.
- HitMarker là Image 48×48 giữa màn hình, tách Crosshair, không có TMP/số damage. Body dùng màu chữ HUD, headshot dùng WarningColor; tồn tại 0,18 giây unscaled. CombatFeedback có Canvas riêng để render trên scope.
- LowHealthBlood dùng DamageVignette, stretch toàn Canvas dưới chữ/reticle. Alpha mục tiêu tăng tuyến tính dưới 45% HP, tối đa 0,35; chuyển mềm bằng SmoothDamp với smoothTime 0,15 giây.
- GoalMarkerCentre/GoalMarker thay ảnh nhiệm vụ cũ; giữ route, khoảng cách, mục tiêu ngoài màn hình và tránh HUD. Mũi tên dùng hướng sprite mới, không giữ offset của Arrow cũ.
- Medkit/Antidote dùng GoalProgress Radial360, từ trên, chiều kim đồng hồ, theo SurvivalInventory.UseProgress. Bỏ TreatmentProgressFill; số lượng/phím nằm trên vòng tiến độ.
- WeaponPanel cao 176; Bullets 24×24 trên số đạn dự trữ; FIRE/AIM dùng icon 24×24 và chữ 16. Hotbar nâng lên 216; vùng tránh bên phải tăng 32. Image mới không nhận raycast.
- Kiểm tra ảnh scope phát hiện artwork che số đạn. Đã đặt ScopeOverlay trước các sibling HUD; native Operator scope và marker headshot nay giữ panel đạn/prompt rõ ràng.

## Sát thương, multiplayer và vòng đời

- DamageInfo có sourcePosition tùy chọn ở cuối constructor, tách hitPoint; EnemyHealth giữ nguồn khi sao chép.
- PlayerHealth triển khai IAttributedDamageable, giữ API float/infection. Các overload đi chung kiểm tra server, damage hợp lệ, protection và trạng thái sống. Sepsis không có nguồn và không tạo hướng.
- Hai đường súng, EnemyAI, Infector, Tank swing/slam, lửa và lựu đạn truyền nguồn server. DirectionalDamageClientRpc chỉ gửi owner nạn nhân; HUD chỉ đăng ký player local.
- Súng xác nhận bằng HP EnemyHealth giảm hoặc DamageRevision PlayerHealth đổi. Không marker cho target chết, damage bị chặn, bắn trượt hay yêu cầu bị từ chối; shotgun giữ xác nhận tổng hợp sau pellet loop.
- Bốn Image nhận đòn dựng sẵn, không tạo object mỗi hit. Gộp trong 30°, dùng ô trống hoặc thay ô cũ nhất; 1 giây tồn tại, fade 0,3 giây cuối; bán kính 128, ảnh 192×48. Hướng XZ cập nhật theo yaw camera; nguồn trùng/không hợp lệ bị bỏ qua.
- Feedback dùng unscaled time, ẩn qua menu/inventory/document/cinematic và vẫn hết hạn. Downed/dead/spectating/respawn/despawn đổi hoặc xóa feedback. Đổi PlayerObject gỡ owner cũ, bind owner mới, đồng bộ HP ngay; vignette chỉ hiện khi local player sống.
- GetPromptBindingPath và GetPromptDisplayName chọn cùng effective binding theo thiết bị. Chuột trái/phải dùng ảnh tương ứng; nút khác dùng tên binding, trống dùng `--`; cập nhật bằng PromptBindingsChanged.

## Kết quả test

| Suite | Kết quả đã hoàn tất | Evidence/job |
| --- | --- | --- |
| HudRegressionTests | 7/7 pass sau chỉnh sửa cuối; authoring hai lần, không trùng, asset/reference, không infinity/legacy damage TMP/progress, save gate bỏ qua unsaved | `cc4df99e`, `.codex/fpssample-hud-review/editmode-hud-result.json` |
| HudGameplayAcceptanceTests | 4/4 pass sau chỉnh sửa cuối | `d0cdad3b`, `.codex/fpssample-hud-review/playmode-gameplay-result.json` |
| HudInputPlayModeTests | 2/2 pass; derived gamepad, binding trống, chuyển thiết bị và overlay input | `38d36432`, `.codex/fpssample-hud-review/playmode-input-result.json` |
| SurvivalNetworkTests | Lượt toàn suite 15/16 pass, gồm hai ca mới owner RPC/từng nguồn và hai đường gun damage. Ca Clove duy nhất fail đã chạy lại nguyên assertion và pass 1/1 | `4c568f2f` + `5358f232`, `playmode-network-result.json` + `network-clove-rerun.json` |

Không ghi lượt network tổng hợp là một lần chạy 16/16. Các lượt acceptance bị ngắt hoặc fail trước khi sửa fixture không dùng làm bằng chứng đạt.

Acceptance cuối kiểm tra body/headshot, shotgun nhiều pellet, miss/filter/protection/wrong sender, ADS và scope thật; heal qua ngưỡng; bốn hướng/gộp/thay/hết hạn/xoay camera/nguồn sai/sepsis; lifecycle và owner rebind; rebind FIRE/AIM; cancel và hoàn thành cả Medkit/Antidote qua API thật; pause/settings/inventory/cinematic/checkpoint; navigation các bearing và route invalid.

Fixture shotgun từng fail vì frame Editor kéo dài 0,213 giây, vượt thời hạn marker 0,18 giây. Diagnostic chứng minh HP giảm và marker hiện ngay đúng màu/owner, rồi hết hạn đúng thời gian. Assertion nay kiểm tra xác nhận ngay và kiểm tra riêng expiry; không tăng thời hạn runtime. Fixture input dùng synthetic input Update và đường toggle ADS thật như suite ADS hiện có. Không thay cơ chế gameplay để làm test pass.

## Bố cục và ảnh gameplay

Đã chụp Game View thật ở 1280×720, 1920×1080, 3440×1440, UI scale 100% và 125%; kiểm tra kích thước PNG đúng yêu cầu, kiểm tra chữ/padding/reticle ≤0,5 px/vùng HUD không chồng và xem ảnh thực tế. Các ảnh stress có subtitles populated, prompt, số đạn và marker nhiệm vụ. Bộ Missions và navigation cũng đủ sáu cấu hình.

Ảnh nằm trong `Documents/UIUX/HUD-Evidence/FPSSample-2026-10-07/`:

- `layout-matrix.png`: sáu cấu hình gameplay; từng ảnh gốc là `stress-<resolution>-<scale>.png`.
- `shotgun-headshot-scope.png`: marker vàng trên scope sprite thật; súng Bucky là fixture để kiểm tra shotgun, artwork scope Operator được bật trực tiếp.
- `scope-presentation.png`: native Operator ADS, số đạn/prompt/hotbar hiển thị trên artwork.
- `low-health-vignette.png`, `four-damage-directions.png`, `treatment.png`, `treatment-completed.png`.
- Bốn file TXT tên test ghi từng assertion; không có FAIL trong lượt cuối.

## Save gate và trạng thái bàn giao

Không lưu Untitled, scene unsaved, preview hoặc Assets/InitTestScene; không mở Save Scene dialog. Quy tắc được lưu trong AGENTS.md và local memory. `Tools/FPS/UI/Save Authored Scenes` chỉ lưu authored scene có asset path rồi gọi AssetDatabase.SaveAssets; đã chạy trước/sau authoring, compile và các lượt test. Các công cụ UI đã bỏ SaveOpenScenes.

Unity 6000.5.6f1: compile thành công, 0 lỗi; compiler còn báo 54 warning API obsolete của dự án. Console Error hiện 0. Không ở Play Mode, không compiling/updating/domain reload pending. GameScene active tại Assets/FPS/Scenes/GameScene.unity báo isDirty=false sau save gate cuối. Evidence: `.codex/fpssample-hud-review/final-scene-state.json`, `final-compile-status.json`. Git diff whitespace check pass khi nhận CRLF đúng định dạng Windows.

## Chỉnh bố cục theo phản hồi người dùng — 08/10/2026

- Bỏ gợi ý idle `[5] SELF · ANTIDOTE` / `[4] SELF · MEDKIT` trong InteractionManager. Phím tự chữa vẫn nằm trong hotbar và hoạt động như trước; prompt mục tiêu/pickup và hỗ trợ đồng đội được giữ. MedicalAction chỉ hiện khi đang điều trị.
- Phụ đề gameplay chuyển từ bottom 292 xuống bottom 16 (trong safe area), cao 128. Prompt/tiến độ giữ nút dùng chung vùng bottom 160; thông báo dùng vùng bottom 236. Các vùng có khoảng cách riêng; prompt thường ẩn khi có tiến độ/lời nhắc hồi sinh. Cùng geometry được dùng trong runtime và routine authoring.
- Marker nhiệm vụ tránh cả vùng chữ dưới màn hình, kể cả khi mục tiêu còn trong viewport; vẫn giữ bearing mũi tên. Vùng tránh tính tới kích thước đầy đủ của nhãn khoảng cách.
- Regression 7/7 và gameplay 4/4 đã chạy lại hoàn tất sau chỉnh sửa. Acceptance thêm assertion không còn idle SELF, phụ đề sát đáy, tiến độ thay prompt thường và navigation distance không chồng phụ đề/thông báo. Ma trận sáu cấu hình đã chụp lại với phụ đề, prompt tương tác, thông báo và HUD cùng hiện; giữ fixture prompt để không bị scan môi trường ghi đè trước khi chụp.
- `layout-matrix.png` và các PNG `stress-*` là ảnh sau chỉnh sửa này. Save gate cuối xác nhận GameScene active, isDirty=false, compile không lỗi và Console Error=0.
