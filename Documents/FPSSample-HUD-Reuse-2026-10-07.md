# HUD có thể lấy từ FPSSample — 07/10/2026

**Có thể tái sử dụng ảnh HUD độc lập trong FPS hiện tại. Ưu tiên HitMarker; DamageIndicator là ứng viên thêm tính năng báo hướng nhận đòn. GoalProgress và các icon chuột/đạn dùng được nếu chọn đổi cách trình bày. Khung HP, tâm ngắm và chỉ dẫn nhiệm vụ hiện đã có phần tương đương, nên lấy chúng là lựa chọn giao diện.**

Đã xem thực tế **26 ảnh** trong hai thư mục nguồn, không kết luận chỉ từ tên file. Đã tải bản đầy đủ vào thư mục review ngoài Assets, xác minh dung lượng và SHA-256 theo từng Git LFS pointer. Tổng dữ liệu gốc 12.39 MiB.

**Đã import 10 ảnh được chọn** vào [FPSSample](E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample): HitMarker, DamageIndicator, DamageVignette, GoalMarker, GoalMarkerCentre, GoalProgress, UI_HUD_LMB, UI_HUD_RMB, UI_Hud_Bullets, UI_Hud_Inifity. Tất cả là PNG, Sprite/Single, Bilinear, Clamp, không mipmap, không CPU Read/Write, RGBA32 không nén, giữ kích thước gốc và alpha. Bốn TIF đã được chuyển sang PNG và so sánh toàn bộ pixel RGBA. Unity đã sinh GUID/meta mới; không sao chép meta hoặc prefab nguồn. [Nguồn và license](E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/SOURCE.md), [manifest hash](E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/source-assets.json). Đã nối 9 ảnh vào HUD gameplay; UI_Hud_Inifity giữ dự trữ. Kết quả kiểm tra triển khai được ghi trong FPSSample-HUD-Implementation-2026-10-07.md.

## Khảo sát FPS trước triển khai (giữ làm đối chiếu)

Kết quả dựa trên source working tree hiện tại và Unity Editor đang mở GameScene; các file HUD và scene có thay đổi sẵn trong working tree.

| Phần | Thực trạng đã xác nhận | Ý nghĩa khi lấy asset |
|---|---|---|
| Tâm ngắm | Có Crosshair trong scene, các Image tạo đường và bóng; kho hiện có 27 PNG crosshair. | Hai ảnh crosshair nguồn là kiểu dáng thay thế. |
| Hit feedback | [WeaponFireHandler.cs](E:/Unity/Project/FPS/Assets/FPS/Features/Weapons/Runtime/WeaponFireHandler.cs:975) gọi [ShowHitConfirmed](E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HUDManager.cs:679). Field `hitMarkerText` trong GameScene hiện null, method trả về ngay. | HitMarker.png hữu ích cho hiển thị bằng ảnh; cần thêm Image và nối vào flow hiện có. PNG không gán trực tiếp vào field TMP. |
| HP | `healthFill` đã gán HealthBar; Image dùng HealthFill.png, Filled/Horizontal. | Không thiếu thanh HP. Khung/thanh nguồn chỉ đổi hình dáng. |
| Ít HP | `healthDangerBackground` đã gán LowHealthBlood, dùng BloodEdge.png. RectTransform hiện là dải dưới màn hình cao 115, không phủ toàn màn hình. [HUDManager.cs](E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HUDManager.cs:565) bật khi HP ≤45%, tăng màu ở ≤25%. | DamageVignette có thể thêm hiệu ứng quanh toàn màn hình; cần bố cục fullscreen. Thay ảnh vào dải hiện tại sẽ làm sai hình. |
| Tiến độ điều trị | `treatmentProgressFill` đã gán Image; [UpdateTreatmentUI](E:/Unity/Project/FPS/Assets/FPS/Features/UI/Runtime/HUDManager.cs:592) cập nhật fillAmount từ tiến độ sử dụng vật phẩm/điều trị. | Có thể dùng GoalProgress cho vòng tiến độ nếu muốn; cần đổi Image sang Filled/Radial360 và bố cục phù hợp. |
| Chỉ dẫn nhiệm vụ | [CampaignHUD.Navigation.cs](E:/Unity/Project/FPS/Assets/FPS/Features/Missions/Runtime/CampaignHUD.Navigation.cs:73) đã tạo Destination bằng Mission.png, RouteCorner bằng Arrow.png; có chiếu vị trí, hướng và khoảng cách. | GoalMarker/GoalMarkerCentre đổi ảnh cho hệ thống này; không cần mang hệ thống goal của FPSSample sang. |
| Hướng nhận đòn | [PlayerHealth.TakeDamage(float)](E:/Unity/Project/FPS/Assets/FPS/Features/Characters/Player/Runtime/PlayerHealth.cs:238) trong đường đã đọc không truyền vị trí/hướng người tấn công. | DamageIndicator cần bổ sung dữ liệu hướng và đường cập nhật HUD; import ảnh riêng chưa tạo tính năng. |

Codebase Memory đã được thử trước khi đọc C# nhưng không chạy được vì lỗi DACL của daemon endpoint; không sửa quyền hệ thống. Đối chiếu C# dùng các file đã khoanh vùng và tham chiếu live trong Editor. Những kết luận về thiếu wiring chỉ áp dụng cho scene/flow đã kiểm tra.

## 11 ảnh trong Sprites/HUD

Bản đầy đủ để xem nằm tại [thư mục review HUD](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/HUD).

| File | Kích thước | Dùng được vào đâu / đề xuất |
|---|---|---|
| [HitMarker.png](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/HUD/HitMarker/HitMarker.png) | 256×256 | Bốn vạch chéo báo bắn trúng quanh tâm ngắm. **Ưu tiên lấy**; nối Image vào hit-confirm hiện có. |
| [DamageIndicator.png](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/HUD/DamageIndicator/DamageIndicator.png) | 1024×256 | Cung/mũi hướng nhận đòn. **Đáng lấy nếu làm directional damage**; cần dữ liệu hướng, xoay theo camera và timer. |
| [DamageVignette.png](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/HUD/DamageVignette/DamageVignette.png) | 1600×1200 | Viền mờ trắng, giữa trong suốt; có thể tint đỏ. Tùy chọn bổ sung overlay fullscreen; ảnh 4:3 cần xem lại trên màn hình 16:9/ultrawide. |
| [GoalMarker.png](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/HUD/GoalMarker/GoalMarker.png) | 256×256 | Mũi chỉ hướng mục tiêu khi ngoài vùng giữa màn hình. Lựa chọn thay Arrow hiện có. |
| [GoalMarkerCentre.png](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/HUD/GoalMarker/GoalMarkerCentre.png) | 256×256 | Đĩa tròn đánh dấu mục tiêu trong vùng giữa màn hình. Lựa chọn thay Mission hiện có. |
| [GoalProgress.png](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/HUD/GoalMarker/GoalProgress.png) | 512×512 | Vòng tiến độ; có thể dùng với tiến độ điều trị/sử dụng vật phẩm đã có. Không tự tạo dữ liệu tiến độ nhiệm vụ. |
| [Crosshair.png](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/HUD/Crosshair/Crosshair.png) | 256×256 | Tâm ngắm bốn vạch cố định. Có thể lấy làm mẫu, không cần cho HUD hiện tại. |
| [Crosshair_Element_A.png](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/HUD/Crosshair/Crosshair_Element_A.png) | 32×32 | Một vạch mềm để ghép tâm ngắm động; hiện đã có các Image vạch tương đương. |
| [DeathMarker.png](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/HUD/DeathMarker/DeathMarker.png) | 512×512 | Mặt hoạt hình mắt chữ X. Source bật khi hit gây chết: **kill confirmation**, không phải marker xác đồng đội. Không ưu tiên theo phong cách HUD hiện tại. |
| [GoalMarker_Attack.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/HUD/GoalMarker/GoalMarker_Attack.tif) | 128×128 | Hai kiếm chéo. Dùng nếu muốn phân biệt nhiệm vụ tấn công; phong cách nổi khối khác các icon phẳng. |
| [GoalMarker_Defend.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/HUD/GoalMarker/GoalMarker_Defend.tif) | 128×128 | Khiên. Dùng nếu muốn phân biệt nhiệm vụ phòng thủ; tùy chọn giao diện. |

![Ảnh HUD gốc và ba sprite FPS hiện tại](E:/Unity/Project/FPS/.codex/fpssample-hud-review/hud-contact-sheet.png)

## 15 ảnh trong Sprites/UI/HUD

Bản đầy đủ nằm tại [thư mục review UI/HUD](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD).

| File | Kích thước | Dùng được vào đâu / đề xuất |
|---|---|---|
| [UI_HUD_LMB.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/UI_HUD_LMB.tif) | 512×512 | Icon chuột trái; dùng trong hướng dẫn bắn/tương tác. Đồ họa chung, dễ tái sử dụng. |
| [UI_HUD_RMB.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/UI_HUD_RMB.tif) | 512×512 | Icon chuột phải; hướng dẫn ngắm. Nếu hỗ trợ đổi phím, icon phải khớp binding thực tế. |
| [UI_Hud_Bullets.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/UI_Hud_Bullets.tif) | 256×256 | Ba viên đạn, dùng cạnh số đạn. FPS đã có current/reserve ammo được gán; đây là bổ sung hình. |
| [UI_Hud_Inifity.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/UI_Hud_Inifity.tif) | 512×256 | Dấu vô cực. Chỉ hiển thị khi gameplay thực sự có tài nguyên/đạn vô hạn; tên nguồn viết `Inifity`. |
| [UI_Hud_DisabledOverlay.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/UI_Hud_DisabledOverlay.tif) | 512×512 | Vòng tròn chữ X cho slot/action không khả dụng. FPS đã có Unavailable.png/Locked.png trong HudSkin, nên là mẫu thay thế. |
| [UI_HUD_HealthIcon.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/UI_HUD_HealthIcon.tif) | 512×512 | Dấu cộng đỏ báo HP. Dùng được; tránh lẫn với Medkit trong bộ survival nếu chọn cùng vị trí. |
| [UI_HUD_Player_Health_Bar.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/UI_HUD_Player_Health_Bar.tif) | 1024×256 | Thanh trắng bo tròn, có nhiều khoảng trong suốt phía trên. Thay HealthFill được sau khi căn/crop và chỉnh bố cục. |
| [UI_HUD_Player_Health_Frame.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/UI_HUD_Player_Health_Frame.tif) | 1024×256 | Khung đen bo tròn đi cùng Bar; cần cân độ tương phản trên nền tối. Không phải hệ thống HP mới. |
| [UI_HUD_NamePlateIcon.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/UI_HUD_NamePlateIcon.tif) | 32×32 | Chấm tròn mềm cho nameplate. Dùng được nhưng rất cơ bản. |
| [UI_Hud_Rocket.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/UI_Hud_Rocket.tif) | 512×512 | Silhouette tên lửa. Giữ làm mẫu nếu có loại đạn/vũ khí tương ứng; chưa có nhu cầu đã xác nhận. |
| [DefaultHUDSprite.png](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/DefaultHUDSprite.png) | 128×128 | Viền hình thoi xanh; có thể dùng làm khung/marker, không lấp khoảng trống cụ thể. |
| [IconRobotWeaponA.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/IconRobotWeaponA.tif) | 882×524 | Silhouette súng robot nguồn. Không dùng làm icon cho sáu súng FPS hiện tại vì sai hình vũ khí. |
| [IconTerraformerWeaponA.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/IconTerraformerWeaponA.tif) | 719×329 | Silhouette súng Terraformer nguồn. Chỉ đi cùng vũ khí tương ứng. |
| [UI_HUD_RobotA.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/UI_HUD_RobotA.tif) | 291×225 | Chân dung Robot nguồn; không đại diện đúng bốn nhân vật FPS hiện tại. |
| [UI_HUD_TerraformerA.tif](E:/Unity/Project/FPS/.codex/fpssample-hud-review/source/Assets/Sprites/UI/HUD/UI_HUD_TerraformerA.tif) | 363×447 | Chân dung Terraformer nguồn; chỉ lấy nếu dùng nhân vật này. |

![Ảnh UI/HUD gốc và HealthFill FPS hiện tại](E:/Unity/Project/FPS/.codex/fpssample-hud-review/ui-hud-contact-sheet.png)

## Sprite, code và prefab khác nhau

- Các PNG/TIF đã xem có alpha, dùng được như ảnh Sprite trong uGUI của Built-in; không kéo theo HDRP/VFX Graph. TIF có thể xuất PNG khi thực sự import để dễ quản lý; khi đó cần kiểm tra alpha và crop.
- Không đề xuất chép nguyên [CharacterHUD.prefab](E:/Unity/Project/FPSSample-master/Assets/Resources/Prefabs/CharacterHUD.prefab) hoặc các script HUD nguồn. [HUDGoal](E:/Unity/Project/FPSSample-master/Assets/Scripts/Game/Modules/Character/UI/HUDGoal.cs:19) đọc LocalPlayer/playerState; [CharacterUISystems](E:/Unity/Project/FPSSample-master/Assets/Scripts/Game/Modules/Character/UI/CharacterUISystems.cs:5) phụ thuộc BaseComponentSystem, EntityManager và dữ liệu gameplay nguồn. Cách ít sửa nhất là giữ HUDManager/CampaignHUD hiện tại rồi thay/thêm ảnh đã chọn.
- [HUDCrosshair.ShowHitMarker](E:/Unity/Project/FPSSample-master/Assets/Scripts/Game/Modules/Character/UI/HUDCrosshair.cs:70) xác nhận mục đích DeathMarker là lethal-hit confirmation. Không suy ra mục đích từ tên file.
- Có kèm DamageVignetteAnim.anim trong thư mục nguồn UI/HUD; chưa đánh giá binding/timing của clip này và không đề xuất mang sang thay cho code HP hiện tại.
- Giữ [LICENSE.md](E:/Unity/Project/FPS/.codex/fpssample-hud-review/LICENSE.md) của Unity Companion License khi sử dụng phần lấy từ nguồn. Hai manifest lưu đầy đủ nguồn, đường dẫn bản review và SHA-256: [HUD](E:/Unity/Project/FPS/.codex/fpssample-hud-review/manifest.json), [UI/HUD](E:/Unity/Project/FPS/.codex/fpssample-hud-review/ui-hud-manifest.json). File nguồn tại FPSSample-master vẫn là các LFS pointer; ảnh đầy đủ đang ở thư mục review.
- Kiểm tra import trong Unity: cả 10 texture có Sprite/Single, alpha từ ảnh nguồn, đúng kích thước và cấu hình UI; hash PNG khớp manifest, license giữ nguyên. [Kết quả import](E:/Unity/Project/FPS/.codex/fpssample-hud-review/import-verification.json). Đã save scene và Save Project trước/sau import, không vào Play Mode. Kiểm tra cuối GameScene `isDirty == false`: [kết quả live](E:/Unity/Project/FPS/.codex/fpssample-hud-review/final-scene-state.json).
