# Đối chiếu asset FPSSample với FPS — 07/10/2026

**Cập nhật HUD:** đã khôi phục vào thư mục review ngoài Assets và xem trực tiếp 26 ảnh HUD/UI-HUD, xác minh SHA-256; sau khi người dùng chọn lấy, đã import 10 Sprite được đề xuất vào [FPSSample](E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample), giữ license nguồn. [Báo cáo HUD chi tiết](E:/Unity/Project/FPS/Documents/FPSSample-HUD-Reuse-2026-10-07.md) ghi danh sách và xác nhận import. Các nhận xét về HUD chưa xem binary trong bản khảo sát ban đầu bên dưới đã được cập nhật bởi báo cáo này; 9 Sprite đã tích hợp vào HUD; icon vô cực giữ dự trữ. Xem FPSSample-HUD-Implementation-2026-10-07.md để biết kết quả kiểm tra.

Nguồn: `E:/Unity/Project/FPSSample-master`. Đích: working tree hiện tại của `E:/Unity/Project/FPS`, Unity 6000.5.6f1, Built-in Render Pipeline.

**Kết luận: chưa có lý do để nhập nguyên bộ model, UI, animation hay gameplay của FPSSample. FPS đã có phần lớn các nhóm này. Khoảng trống rõ nhất là các tham chiếu âm thanh súng và hit marker chưa được gắn; bank footsteps/landing của FPSSample là ứng viên bổ sung cụ thể.**

## Đã kiểm tra gì

- AssetDatabase hiện tại qua Unity Skills: 363 model, 269 AnimationClip asset trong `Assets` (270 nếu tính một asset của package Timeline), 44 AnimatorController, 494 prefab, 25 AudioClip. Các số này là kết quả tìm asset, không phải số animation sub-asset nhúng trong mọi FBX.
- Riêng `World/Content`: 200 model và 374 prefab. Scene hiện tại đã có Factory, Asylum, Laboratory và các kết nối giữa chúng.
- Đọc source hiện tại của AudioManager, WeaponData, đường phát âm thanh trong Weapon, HUDManager, HudSkin, ButtonAudioFeedback, PlayerVisibilityController và các đường âm thanh trong Survival/Player/Missions.
- Đọc trực tiếp trong Editor các field của sáu WeaponData, SurvivalCatalog, HUDManager và một ButtonAudioFeedback đang gắn trên SettingsPanel.
- Đối chiếu đuôi file và dung lượng của 3.957 đường dẫn asset/font hiện tại với các Git LFS pointer nguồn; tính SHA-256 của 16 ứng viên cùng đuôi và dung lượng. Không có file trùng byte trong phạm vi này. Kiểm tra này không loại trừ asset đã được chuyển định dạng, chỉnh sửa hoặc retarget.
- Codebase Memory MCP đã được gọi trước khi đọc C#, nhưng không khởi động được do lỗi DACL của daemon endpoint trong AppData. Không sửa quyền hệ thống. Snapshot Unity semantic ngày 20/09 chỉ dùng để định vị; kết luận về tham chiếu dựa trên Editor hiện tại và source đang có, kể cả file modified/untracked.

## So sánh và quyết định

| Nhóm | FPS hiện có / thực trạng | FPSSample có gì thêm | Đề xuất |
|---|---|---|---|
| Âm thanh súng | Có `pistol_shot.mp3`, `pistol_reload.mp3`, `128297__xenonn__layered-gunshot-7.wav`. Classic, Vandal, Odin, Bucky, Operator, Warden đều có `shootSound = null`, `reloadSound = null`, được xác nhận trong Editor. `Weapon.cs` đã phát các field này khi có clip. | 32 WAV của robot/terraformer, bắn/reload/grenade. | Gắn và kiểm tra clip đang có trước. Sau đó chỉ lấy biến thể nghe phù hợp; chưa thể khẳng định âm thanh sci-fi tốt hơn vì nguồn chưa có WAV thật để nghe. |
| Footsteps / landing người chơi | Trong 25 AudioClip có `dantewalk`/`danterun` của enemy Tanker, nhưng chưa thấy bank footsteps người chơi. Không tìm thấy trigger footsteps/landing audio trong source Player/Input/Audio đã kiểm tra. | 11 `GirlFootstepsV2-*`, 9 `GirlLandSmall/Large*`, 10 `GirlMovementClothes*`. | Đây là ứng viên bổ sung rõ nhất: 20 clip footsteps + landing, khoảng 3,53 MiB dữ liệu gốc. Cần thêm điểm phát theo bước/chạm đất và nghe thử; nhập WAV riêng không tự tạo tính năng. Clothes là bổ sung sau. |
| Âm thanh UI/menu | Có 9 clip UI/menu, `ButtonAudioFeedback` đã gắn trên các nút. Nút ResetDefaults đang tham chiếu `buttonselect` cho hover và `button` cho click. | 4 UI-select WAV, 4 Lethal/NonLethal HUD WAV. | Không lấy UI-select để lấp một khoảng trống không tồn tại. Hit-confirm audio chỉ là tùy chọn sau khi hoàn thiện hit marker. |
| Lựu đạn, cháy, pickup, điều trị | SurvivalCatalog đã gắn FragExplosion, Throw, Treatment, FireLoop và cả bốn pickup clip; completionClip cũng có dữ liệu. | Grenade/impact/barrel explosion WAV và các VFX. | Giữ phần đang có. Chỉ thay clip/effect khi có nhu cầu chất lượng cụ thể sau khi nghe/xem. |
| Crosshair / HUD | Crosshair đang tồn tại trong GameScene, với các Image tạo đường và bóng. Kho hiện tại có 27 ảnh crosshair, 18 ảnh Resources/HudSkin, icon vũ khí, FPS Icons Pack và UI kit. LowHealthBlood đã được gắn. | Crosshair, damage indicator/vignette, hit marker, goal marker PNG/TIF. | Không nhập cả bộ HUD. `crosshairRoot` chưa gắn trực tiếp nhưng code có tìm object Crosshair khi cần; đó không phải thiếu asset. |
| Hit marker | WeaponFireHandler gọi `HUDManager.ShowHitConfirmed`; HUDManager đã có code hiển thị TMP và timer. Nhưng `hitMarkerText` đang `null` trong GameScene, nên method trả về ngay. | `Sprites/HUD/HitMarker/HitMarker.png`. | Hoàn thiện tham chiếu TMP hiện có trước. PNG nguồn không cắm trực tiếp vào field TextMeshProUGUI; dùng ảnh cần sửa phần hiển thị. Đây là thiếu wiring, không phải thiếu bộ UI. |
| Môi trường / props | 200 model và 374 prefab trong World/Content: nhà máy, asylum, lab, cửa, thùng, container, hàng rào, quạt, máy phát, tủ điện, đường, phương tiện. Scene đã dùng ba khu vực này. | 398 FBX môi trường sci-fi, tileset và props/vehicles. | Không ưu tiên nhập hàng loạt. Chưa xác định props chức năng nào bị thiếu mà bộ này giải quyết; chỉ lấy model cụ thể khi có vị trí và yêu cầu art rõ ràng. |
| Nhân vật / vũ khí | Có bốn player prefab Brimstone, Clove, Gekko, Sage và sáu bộ súng Classic, Vandal, Odin, Bucky, Operator, Warden. | Robot, Terraformer và các súng sci-fi. | Không đề xuất thay hoặc thêm chỉ vì nguồn có. Cần nhu cầu nhân vật/súng mới và quyết định phong cách trước. |
| Animation | Có FPAnim, bộ 3P shared, controller/clip riêng cho các súng, hỗ trợ GenericPathBound và timing reload/equip đã gắn với WeaponData. | 262 FBX animation; tài liệu nguồn mô tả Humanoid 3P, Generic 1P và graph Playables riêng. | Không nhập cả pack/controller. Chỉ xem xét clip cụ thể khi xác nhận thiếu động tác; Generic 1P khác skeleton, còn 3P cần kiểm tra tương thích rig/timing. |
| Muzzle flash / impact | Cả sáu WeaponData đã tham chiếu MuzzleFlashEffect và BulletImpactStoneEffect trong Editor; có EffectExamples và effect survival riêng. | VFX Graph, effect mesh/texture, material HDRP. | Không có khoảng trống đã xác nhận. VFX Graph/HDRP không phải thay thế trực tiếp cho pipeline hiện tại. |
| Ambient / nhạc | Có nhạc menu. Trong GameScene ở Edit Mode không thấy AudioSource; source AudioManager/Survival/Campaign tạo một số source lúc runtime, nên không suy ra gameplay hoàn toàn không có audio. | Ba file trong Ambient đều có tên nhạc: `InGameMusic01`, `UnitySampleGameDroneMusic-action`, `UnitySampleGame-TitleScreenMusic`. | Chưa có căn cứ đề xuất chúng như bank âm môi trường nhà máy. Cần nghe và xác định nhu cầu nhạc trước. |

## Thứ tự thực tế

1. **Tận dụng asset đã có:** gắn các âm thanh súng phù hợp và hoàn thiện hit marker. Đây là các khoảng trống được xác nhận, chưa cần FPSSample để giải quyết.
2. **Ứng viên lấy từ FPSSample:** 11 footsteps + 9 landing của Terraformer; nghe thử rồi chọn. Có thể lấy thêm bank súng khi clip hiện tại thiếu biến thể phù hợp.
3. **Hoãn các nhóm còn lại:** model môi trường, nhân vật, bộ súng, HUD, controller, VFX/HDRP, font/texture hàng loạt. Chưa có yêu cầu cụ thể chứng minh lợi ích lớn hơn chi phí chuyển đổi.

## Tình trạng nguồn và giới hạn

- Tại thời điểm kiểm tra ban đầu, trong 12 nhóm nguồn có 2.139 Git LFS pointer, gồm toàn bộ 420 FBX trong Models, 262 FBX animation, 92 WAV, 39 ảnh Sprites và 21 TTF. Sau đó đã khôi phục 11 GirlFootstepsV2 từ repository gốc và xác nhận SHA-256, dung lượng, cấu trúc WAV cùng mẫu âm thanh khác zero; các nhóm khác chưa được khôi phục.
- Folder nguồn không có `.git`. Chạy `git lfs pull` ngay tại folder này không khôi phục được dữ liệu; cần checkout Git LFS đúng từ repository nguồn hoặc bản asset đầy đủ.
- Có thể chọn tải dữ liệu audio cần dùng thay vì tải cả kho texture khoảng 16,6 GiB. Chưa tải hoặc import gì trong lần kiểm tra này.
- LICENSE.md nguồn ghi Unity Companion License cho dự án phụ thuộc Unity; font có LICENSE.txt riêng (Apache 2.0). Giữ notice/license của phần được lấy.
- Chưa nghe/xem thử binary nguồn vì chúng đang thiếu. Không đánh giá chất lượng âm thanh, chất lượng rig hay mức cải thiện hình ảnh chỉ từ tên file.
- Không đổi code hoặc asset gameplay, không vào Play Mode, không mở scene khác. Scene GameScene đã được kiểm tra trong Editor và có `isDirty == false`.

## Các điểm đối chiếu chính

- [WeaponData.cs](E:/Unity/Project/FPS/Assets/FPS/Features/Weapons/Runtime/WeaponData.cs:200) — hai field clip của vũ khí.
- [Weapon.cs](E:/Unity/Project/FPS/Assets/FPS/Features/Weapons/Runtime/Weapon.cs:592) — phát shootSound; reloadSound ở dòng 965.
- [HUDManager.cs](E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HUDManager.cs:679) — hit marker trả về khi chưa gắn TMP.
- [WeaponFireHandler.cs](E:/Unity/Project/FPS/Assets/FPS/Features/Weapons/Runtime/WeaponFireHandler.cs:975) — hit-confirm đã đi đến HUD.
- [HudSkin.cs](E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HudSkin.cs:16) — dùng sprite Resources/HudSkin hiện tại.
- [PlayerVisibilityController.cs](E:/Unity/Project/FPS/Assets/FPS/Features/Characters/Player/Runtime/PlayerVisibilityController.cs:7) — các chế độ rig/presentation hiện có.
- Evidence Editor và hash được lưu trong `.codex/asset-reuse-*-evidence.json` và `.codex/asset-reuse-current-inventory.json`.
