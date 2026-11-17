# 37 — Mặt phẳng chồng khít (2026-11-17)

Nối tiếp báo cáo 30, mục 5 / đề xuất 7. Người dùng chọn "Mặt chồng khít".

Ảnh: `Reports/MapFixCoplanar-2026-11-17/` (xoá sau khi review xong).

## 1. Đo lại cho chính xác

Báo cáo 30 chỉ so hộp bao. Lần này đo trên **tam giác mesh thật**:
- tìm vùng hai mặt cùng hướng chồng lên nhau;
- đo khoảng cách giữa hai mặt;
- kiểm phía trước mặt đó có khoảng trống để người chơi nhìn thấy không.

Áp cho 68 cặp lớn nhất, sau đó quét riêng toàn bộ 393 tấm rào lưới. `[SCENE]`

| Nhóm (báo cáo 30) | Kết quả đo thật |
|---|---|
| Trần tầng trệt Asylum và phần lớn sàn lầu 2 | **Không chồng thật** (17 cặp): mặt ngoài nằm kẹp trong khe sàn, không ai nhìn thấy |
| Sàn Kitchen lầu 2 / sàn hành lang; sàn nhà vệ sinh / `EastStoreyCap` | Cách 5,1 mm / 6,8 mm, trong nhà |
| Thảm `OfficeA` / sàn | Thảm cao hơn sàn 5,5 mm |
| Sân bay `CargoApron_*`, `FlightApron_*` / nền `FacilityGround_Visual` | Sân cao hơn nền 10 mm |
| Vạch sơn bãi đáp / mặt sân | Vạch cao hơn 7,5–9 mm |
| **Rào lưới `Concrete_fence_v2_lattice_S`** | **281 chỗ nối trùng mặt phẳng 0 mm** (chồng 0,04–2,17 m) |

## 2. Có nhấp nháy thật không

Camera người chơi: near 0,18, far 1000, FOV 60 `[CODE SagePlayer.prefab: near clip plane 0.18]`.

- **Bãi đáp LZ và Helipad:** chụp từ 30, 80, 150 m, rồi chụp lại sau khi nâng vạch sơn (và sân) thêm 2 cm. Chỉ 0,0–0,15% điểm ảnh đổi, đều ở mép vạch quá mảnh (vạch rộng 0,32 m nhìn rất nghiêng chỉ còn chưa tới 1 điểm ảnh). Không có nhiễu chập mặt. Ảnh `lz_pad_30m_current`. `[PLAYTEST screenshot]`
- **Sàn, thảm trong Asylum:** cách 5–7 mm, tầm nhìn trong nhà dưới 30 m. Với near 0,18, độ phân giải độ sâu ở 30 m cỡ 0,3 mm, nên không chập. `[INFERENCE]` tính theo bộ đệm sâu 24 bit.
- **Rào lưới:** lỗi thật. Chụp chỗ nối khi bật cả hai tấm, chỉ tấm A, rồi chỉ tấm B: ảnh hai tấm là **lưới dày gấp đôi, lệch hoa văn** trong dải chồng, và các sợi trùng nhau có thể nhấp nháy. Ảnh `joint_fuelcourt_closeup_before`, `joint_hubscreen_closeup_before`. `[PLAYTEST screenshot]`

=> Chỉ sửa rào lưới. Các nhóm còn lại không đổi gì: nâng thêm chỉ làm lộ mép mà không đem lại lợi ích nào.

## 3. Đã sửa

- Mesh lưới là một tấm phẳng hai mặt (8 đỉnh), UV chạy tuyến tính theo chiều dài.
- Ở mỗi chỗ nối, tôi cắt bớt đúng phần chồng ở **một trong hai tấm** (chia tấm theo kiểu tô hai màu, mỗi dãy rào xen kẽ tấm cắt và tấm giữ nguyên). Đầu tấm bị cắt lùi vào thêm 2 mm.
- UV được dịch theo, nên phần lưới còn lại giữ nguyên mật độ và vị trí hoa văn.
- Kết quả: 148 tấm được cắt, dùng 24 mesh cắt sẵn, đặt ở `Assets/FPS/Features/World/Generated/FenceTrim/` (`Concrete_fence_v2_lattice_S_trim<đầu dưới>_<đầu trên>`, đơn vị mm; tổng 240 KB).
- Phạm vi: rào biên map `NarrativePerimeter` (259 chỗ nối), `FlightFuelCourt` (14), `DirectorSightlineScreens` (5), và 3 chỗ ở rào khuôn viên Asylum tôi thêm ở báo cáo 27.
- Chỉ đổi mesh hiển thị. Collider, cọc, bệ bê tông giữ nguyên.

## 4. Kiểm chứng (sau khi lưu và nạp lại scene)

| Đo | Trước | Sau |
|---|---|---|
| Chỗ nối rào lưới trùng mặt phẳng và chồng lên nhau | 281 | **0** |
| Tấm lưới | 393 | 393 (không thêm, không xoá) |

`[SCENE]`

`[PLAYTEST screenshot]` (camera Editor, chưa Play):
- `joint_fuelcourt_closeup_*`, `joint_hubscreen_closeup_*`: trước là lưới đôi lệch hoa văn, sau còn một lớp.
- `joint_perimeter_*`: chỗ nối rào biên giữa hai cọc.

## Tuân thủ và giới hạn

- Save Gate: scene sạch trước khi sửa; script tự lưu scene và gọi `AssetDatabase.SaveAssets()`; nạp lại để kiểm, `isDirty=false`.
- Các phép thử bật/tắt renderer và nâng tạm chỉ làm trong bộ nhớ rồi trả lại như cũ; scene không bẩn.
- Script tạm `Assets/Editor/TempZFight.cs` đã xoá; compile sạch.
- Lightmap: tấm lưới bị cắt vẫn dùng UV2 cũ ở hai đầu, nên bóng sáng của lưới có thể co nhẹ cho tới khi bake lại. Lưới là vật liệu cutout mỏng, khó thấy.
- Edit mode, chưa Play. Skill đã đọc: `unity-skills`. Chưa có reviewer độc lập.

## Còn lại

1. 26 anchor không dùng (báo cáo 36) — xoá hay sửa rồi đưa vào danh sách.
2. Bake lại ánh sáng (cho mọi thay đổi hình học từ báo cáo 25 tới nay).
3. Play thử host/client và commit.
