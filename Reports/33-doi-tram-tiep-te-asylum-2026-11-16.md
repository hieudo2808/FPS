# 33 — Dời 3 trạm tiếp tế Asylum ra khỏi tường và kệ (2026-11-16)

Nối tiếp báo cáo 30, mục 2 (vật phẩm Asylum). Đây là vật tương tác nên tôi lập phương án trước; người dùng duyệt: "Duyệt, làm đi".

Ảnh: `Reports/MapFixAsySupply-2026-11-16/` (xoá sau khi review xong).

## 1. Hiện trạng trước khi sửa

Asylum có 3 trạm, mỗi trạm gồm một bàn lab `ApprovedTable_Asylum_N`, 4 `FiniteSupply` và 8 `SurvivalSupply_*`, tổng 36 vật phẩm (báo cáo 30 ghi 24 là thiếu). Bàn tạm cũ `SupplyBench_Asylum_N` vẫn nằm cùng chỗ nhưng đang ẩn.

| Trạm | Vấn đề | Nhặt được |
|---|---|---|
| Lầu 2 (`_1`) | Bàn xuyên qua tường giữa phòng khám và phòng mổ, cắm vào 4 tủ thuốc; chỉ lòi một đầu ra phòng mổ. 4 vật phẩm nằm trong tủ. | 5/12 |
| Tầng hầm (`_2`) | Nửa bàn nằm trong tường bắc phòng cách ly B; 6 vật phẩm nằm trong tường; mặt tủ quay vào tường. | 6/12 |
| Phòng kho tầng trệt (`_0`) | Bàn cắt ngang kệ sắt `BigShelf_Stock (5)`; chỉ còn 1 chỗ đứng. | 5/12 |

"Nhặt được" đo theo đúng điều kiện của game: `CampaignSupply.InReach` (mắt = chân + 1,55 m, tầm `interactionRange` 2,5 m, tia từ mắt tới vật không bị chắn) và tia ngắm của `InteractionManager` phải trúng collider của vật. `[CODE Assets/FPS/Features/Missions/Runtime/CampaignSupply.cs:50]` `[CODE Assets/FPS/Features/Interaction/Runtime/InteractionManager.cs:129]` `[SCENE]`

## 2. Đã sửa

Mỗi trạm được dời nguyên khối (bàn, 12 vật phẩm, bàn tạm cũ đang ẩn), giữ nguyên vị trí tương đối giữa các món. Không đổi `supplyId`, phần thưởng, chương hay component nào, nên logic server (cả trạm dự phòng của Director, tính theo `supplyId`) giữ nguyên.

| Trạm | Từ | Tới | Hướng |
|---|---|---|---|
| Lầu 2 | (118.83, 3.86, 232.29) | (115.40, 3.86, 229.05): phòng khám, sát tường nam dưới cửa sổ | Giữ 0°; mặt tủ quay ra phòng |
| Tầng hầm | (150.83, −3.79, 234.29) | (151.85, −3.79, 233.90): cùng tường, kéo ra 0,4 m, dịch 1 m xa cửa | 0° → 180° để mặt tủ quay ra phòng |
| Phòng kho | (152.83, 0.05, 246.89) | (155.65, 0.05, 247.80): góc đông bắc, sát tường dưới cửa sổ, cuối lối giữa kệ và tường | 0° → 180° để mặt tủ quay ra lối |

42 transform đổi (3 × 14). Scene đã lưu.

## 3. Kiểm chứng (sau khi lưu và nạp lại scene)

| Trạm | Chồng vật khác | Chỗ đứng trước bàn | Nhặt được | Ngắm trúng | Dò đường bằng capsule người chơi |
|---|---|---|---|---|---|
| Lầu 2 | 0 | 5 | 12/12 | 12/12 | Từ hành lang lầu 2 tới trước bàn: tới được |
| Tầng hầm | 0 | 5 | 12/12 | 12/12 | Từ hành lang tầng hầm qua cửa phòng: tới được |
| Phòng kho | 0 | 3 | 12/12 | 12/12 | Từ trong phòng kho tới trước bàn: tới được |

- Khi chọn chỗ (trước khi dời) đã kiểm: có sàn dưới cả 4 góc bàn; có NavMesh trong 0,65 m quanh các chỗ đứng (5/5, 5/5, 3/3); cách cửa trên 0,9 m; không sát vật nhiệm vụ nào trong cùng phòng. Hồ sơ bệnh nhân (`FILE_AsylumPatientRecord`) cách bàn lầu 2 khoảng 2 m. Hồ sơ chuyển xác cách bàn tầng hầm 1,5 m nhưng nằm sau tường.
- `[SCENE]` Tất cả số liệu trên lấy từ bản scene đã lưu. Sau khi nạp lại, `isDirty=false`.
- `[PLAYTEST screenshot]` (camera Editor, chưa Play):
  - Trước: `before_s1_2F_surgery`, `before_s1_2F_examroom`, `before_s2_B1_seclusion`, `before_s0_1F_stock`.
  - Sau: `after_s1_2F_surgery` (đầu bàn cũ không còn lòi ra giữa tủ thuốc), `after_s1_2F_examroom`, `after_s2_B1_seclusion`, `after_s0_1F_stock`, `after_s0_1F_stock_old` (kệ sắt cũ đã trống).

## 4. Lưu ý

- **Cửa phòng kho hẹp:** khe hở 1,19 m, người chơi rộng 1 m. Capsule bán kính 0,48 m quét thẳng qua được, nhưng bộ dò lưới 0,1 m của tôi không đi được từ hành lang vào phòng. Nên thử khi Play. NavMesh phòng kho cũng là một đảo tách riêng (nhóm NavMesh, chưa làm).
- **Lầu 2:** trạm chuyển hẳn sang phòng khám, không còn ở phòng mổ.
- **Phòng kho:** bàn nằm ở góc, khuất sau kệ khi nhìn từ lối ngang; thấy rõ khi đi vào lối sát tường đông.
- Báo cáo 30 ghi "8 vật phẩm phòng kho không có mặt đất ngay dưới". Đo lại thì vật phẩm đặt trên mặt bàn; lỗi thật là bàn cắt qua kệ và thiếu chỗ đứng.

## Tuân thủ và giới hạn

- Phương án được duyệt trước khi sửa vật tương tác (AGENTS.md: không sửa interactable khi chưa có phương án được duyệt).
- Ảnh xem trước trong phương án được dựng bằng cách dời tạm trong bộ nhớ rồi trả về như cũ, không lưu; scene đã nạp lại từ đĩa sau đó.
- Save Gate: scene sạch trước khi dời; script dời tự lưu scene và gọi `AssetDatabase.SaveAssets()`; đã nạp lại để xác nhận, `isDirty=false`.
- Script tạm `TempAsySupply.cs`, `TempFineReach.cs` đã xoá; compile sạch.
- Không đụng NavMesh, AI, âm thanh, vũ khí, save. Edit mode, chưa Play, chưa host/client.
- Skill đã đọc: `unity-skills`. Chưa có reviewer độc lập.

## Còn lại từ báo cáo 30 (chưa làm, chờ duyệt)

1. Bake lại NavMesh (sân trước Asylum, buồng thang Lab, đảo phòng kho/phòng Asylum, bỏ NavMesh trên mái).
2. Dời 11 anchor sinh zombie lọt trong vật đặc + 5 anchor chật chỗ.
3. Nâng 6 nhóm mặt phẳng chồng khít 1–2 cm.
4. Bake lại ánh sáng, Play thử host/client, commit.
