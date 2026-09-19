# Combat and survival icon selection — 14/09/2026

Nguồn chính: `E:/ProjectSettings/Assets/Resources/inventory/icons` (asset local người dùng). Bộ `weapons/*.png` có nền trong suốt và cùng line-art xanh, nên phù hợp làm icon HUD nhỏ. Tôi đã đưa 5 silhouette đang có trong enum `PrimaryWeaponId` vào Curated:

| Game weapon | Curated icon | Local source |
|---|---|---|
| Vandal | `Icons/Weapon_Vandal.png` | `weapons/Ak48.png` |
| Classic | `Icons/Weapon_Classic.png` | `weapons/PistolVr.png` |
| Operator | `Icons/Weapon_Operator.png` | `weapons/RifleGun.png` |
| Odin | `Icons/Weapon_Odin.png` | `weapons/HeavyMachineGun.png` |
| Bucky | `Icons/Weapon_Bucky.png` | `weapons/ShotgunX.png` |

`WeaponData.weaponIcon` đã được gán cho cả 5 asset. `HUDManager` đã đọc icon từ weapon đang active và weapon dự phòng, vì vậy không cần thêm icon tĩnh hay mapping thứ hai.

| Item | Curated asset | Ý định dùng |
|---|---|---|
| Grenade | `Icons/Items/FragGrenade.png` | inventory/HUD grenade |
| Heal kit | `Icons/Items/Medkit.png` | pickup/inventory medkit |
| Infection suppression candidate | `Icons/Items/AntidoteVial.png` | icon lọ thuốc cho UI giảm infection |

Hai ảnh medical 512×512 là inventory card có nền xám, nên hiện chỉ được tổ chức như asset item; chưa gắn trực tiếp vào một `PickupType` mới. `PickupItem` hiện chỉ hỗ trợ Ammo/Health/Weapon, còn infection treatment hiện là hành động tương tác. Khi gameplay thêm pickup antidote, có thể bind `AntidoteVial` mà không phải đổi lại nguồn icon.

Hash và provenance đầy đủ ở `curated-combat-items.json`. Không tải thêm internet vì folder người dùng đã có một bộ icon cùng phong cách và đủ silhouette cho yêu cầu.
