# 42 — Sửa nốt các mặt chồng khít có từ trước (2026-11-17)

Nối tiếp báo cáo 39, mục 3 (phần chưa sửa). Người dùng chọn "Sửa luôn".

Ảnh: `Reports/MapFixCoplanar2-2026-11-17/` (xoá sau khi review xong). Camera Editor, chưa Play.

## 1. Cách đo

Cùng công cụ của báo cáo 39: với mọi cặp vật có hộp bao chạm nhau, tìm các tam giác cùng hướng, nằm sát nhau và chồng lên nhau. Sau đó lọc theo đường nhìn từ 15.063 chỗ người chơi đứng được, trong 40 m. `[SCENE]`

Lần này tôi tách theo khoảng cách giữa hai mặt:
- **Trùng thật (< 0,45 mm):** mới là lỗi nhấp nháy.
- **Lệch 0,5–2 mm:** phần lớn là lệch có chủ ý của người làm map. Ví dụ móng nhà thấp hơn tấm nền lát `PavingBase` 1 mm, bệ hangar thấp hơn 2 mm, tấm `ConcreteUV` thấp hơn 5 mm. Các cặp này không nhấp nháy ở tầm chơi.

| Nhìn thấy được, trước khi sửa | Số cặp | Diện tích |
|---|---|---|
| Trùng thật | 343 | 714 m² |
| Lệch có chủ ý 0,5–2 mm | 70 | 2.173 m² |

## 2. Đã sửa

**Tấm nền lát `PavingBase_*` (59 mesh, `Roads/FactoryYard/Meshes/Ground/`):**
- Mặt đứng của chúng trùng 0 mm với mặt đứng bó vỉa, dốc, bậc nền dọc các con đường Factory (khoảng 190 cặp).
- Sửa: lùi mọi cạnh của tấm nền vào 3 mm. Mỗi đỉnh dời theo pháp tuyến trung bình nên tấm vẫn kín, không hở khe.
- Mặt trên giữ nguyên, vì móng, bệ và tấm bê tông xung quanh đã được đặt thấp hơn nó 1–5 mm. Lần thử đầu tôi hạ cả mặt trên thì nó trùng với chính các lớp đó, nên đã trả mesh về bản gốc trong git và làm lại.

**Các nhóm còn lại:**
- Dời vật nhỏ hơn trong mỗi cặp 2–3,5 mm về phía nó nằm, để nó thắng.
- Chạy nhiều lượt với khoảng dời khác nhau (3 / 2 / 2,5 / 1,5 / 3,5 / 4,5 mm) để không trùng lại với vật đã dời.
- Các nhóm: viền trần Lab (`CeilingFascia`), vạch sơn bãi đáp (`LZ_Clearance`, `Helipad_Clearance`), chân vách hố Lab, vách ngăn nhà xác, ngưỡng cửa xưởng (`ConcreteThreshold`), container đỡ sàn cao, bó vỉa góc sân (`ServiceCourt*Kerb`).
- Asylum: khung cửa sổ phòng `Surgery`, giấy trên bàn, cánh tủ `MedRack`, kệ, hộp, nệm, bồn cầu, `ProjectorCloth`.

**Thanh bậc thang** (`EastProcessAccess`, `NorthProcessAccess`, `WorkshopEntrySteps`):
- Đây là chuỗi thanh giống nhau nối tiếp nhau. Dời từng cặp thì thanh bị đẩy cùng chiều nhiều lần, có thanh trôi tới 17 mm.
- Đã đưa cả chuỗi về mặt phẳng gốc (thanh đầu chuỗi không bị dời), rồi đặt xen kẽ 0 và 3 mm.

**Chỉnh lại sau khi kiểm:**
- Sàn `Asylum/Basement/MachineRoom_floor` từng bị dời 7,5 mm: đã trả về chỗ cũ. Thay vào đó nâng 2 mm hộp và nệm đặt trên nó.
- Cánh tủ `MedRackDoor_R` ở `Surgery` bị dời 5–6,5 mm: giảm còn 2 mm.

**Không đụng:** vật thuộc cửa và cổng gameplay (cánh `CampaignLeaf` của buồng thang Lab, cổng `Gate_DeconExit`).

Tổng cộng khoảng 230 vật được dời. Sau khi chỉnh, mỗi vật lệch khỏi vị trí cũ tối đa khoảng 6 mm, phần lớn 2–3 mm. Chỉ dời vị trí, không đổi mesh (trừ `PavingBase`), không đổi collider hay lightmap.

## 3. Kiểm chứng (sau khi lưu và nạp lại scene)

| Đo | Trước | Sau |
|---|---|---|
| Cặp trùng mặt phẳng toàn map (≤ 2 mm) | 1.003 | 612 |
| — trong đó trùng thật, người chơi nhìn thấy | **343** (714 m²) | **3** (0,4 m²) |
| Đi lại (lưới 0,5 m): chỗ rơi ra ngoài / bẫy rơi / tường vô hình | 23 / 51 / 2 (đã xác minh vô hại ở báo cáo 30) | không đổi |
| Điểm gameplay hợp lệ | — | tất cả (chỉ còn `PlayerStart_1` ở Lab, vốn không được dùng) |

`[SCENE]`
- 3 cặp còn lại:
  - 2 cặp thuộc cửa/cổng gameplay, cố ý giữ nguyên;
  - 1 khe nối bó vỉa cong 0,02 m² phát sinh khi dời bó vỉa.
- 63 cặp còn lại người chơi nhìn thấy đều thuộc loại lệch có chủ ý 0,5–2 mm.

`[PLAYTEST screenshot]`:
- `curb_before` / `curb_after`: mặt bó vỉa trước có một đoạn đổi sắc (hai mặt tranh nhau); sau đồng đều.
- `stairs_after`: cầu thang `EastProcessAccess` bình thường sau khi đưa các thanh về.

## Tuân thủ và giới hạn

- Người dùng duyệt ("Sửa luôn"). Không đổi code, vật tương tác, nhiệm vụ; không dời vật gameplay.
- Save Gate:
  - scene sạch trước mỗi lượt;
  - script tự lưu scene và gọi `AssetDatabase.SaveAssets()`;
  - nạp lại scene trước và sau mỗi lượt; một lần áp dụng bị bỏ qua vì scene báo dirty (lỗi đã biết), tôi nạp lại rồi làm lại;
  - kết thúc bằng `File/Save Project`, `isDirty=false`.
- 59 mesh `PavingBase_*` thay đổi (đã có trong git, hoàn tác bằng `git checkout` được).
- Script tạm `TempCoplanarFix.cs`, `TempFullAudit.cs`, `TempFineReach.cs`, `TempBake.cs` đã xoá; compile sạch.
- NavMesh và lightmap không bake lại; các thay đổi chỉ vài mm nên không ảnh hưởng.
- Edit mode, chưa Play. Skill đã đọc: `unity-skills`. Chưa có reviewer độc lập.

## Còn lại

1. Play thử host/client và commit.
