# Outbreak Protocol · Phụ đề radio ưu tiên một dòng

**20/11/2026 · Đã triển khai mẫu người dùng duyệt.** Xem [mẫu duyệt 50](E:/Unity/Project/FPS/Reports/50-radio-mau-phu-de-mot-dong.md). Phụ đề giữ một dòng khi đủ chỗ; HUD tự xuống dòng theo bề rộng thực tế. D05 ở 1080p và 4K hiện: `Vega: Generator's up. Cold storage has power.`

[CODE CampaignContentCatalog.asset:980](E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset:980) Thay 265 dấu xuống dòng cứng bằng dấu cách trong `cues` và `soloCues`. Phạm vi gồm 28 mã radio, trong đó 10 mã có bản một đặc vụ: tổng cộng 166 nhịp thoại và 332 chuỗi tiếng Anh, tiếng Việt. So sánh toàn bộ JSON Unity xuất trước và sau khi nhập xác nhận rằng chỉ các dấu xuống dòng này thay đổi. Lời thoại, dấu câu, người nói, thứ tự và số ký tự của từng nhịp giữ nguyên. Cách tính thời lượng và mã HUD giữ nguyên.

[SCENE] PlayMode chạy với host nội bộ trên `GameScene`. Khi kết thúc, cảnh không còn thay đổi chưa lưu (`isDirty = false`), console có 0 lỗi. SHA-256 của `GameScene.unity` và hai tệp checkpoint khớp giá trị trước khi chạy.

[PLAYTEST screenshot] Đã đối chiếu ảnh Game View trước/sau ở 1920×1080 và 3840×2160, có rà soát độc lập. D05 chuyển từ hai dòng còn một, hiện đủ chữ giữa đáy màn hình và không chạm bảng máu hay vũ khí. Ở 1440×1080 (4:3), D05 vẫn hiện đủ nhưng tự ngắt hai dòng, còn riêng từ `power.` ở dòng cuối. Khi mở Campaign Record, D21 hiện dưới mép bảng, không đè nội dung hay nút đóng trên các ảnh đã xem.

Unity Test Runner · PlayMode `e88eb8a2`: **1/1 đạt**. Bài kiểm thử phát D05 trước/sau và D21 qua radio trong lúc chơi; đồng thời đo số dòng của 1.140 chuỗi sau khi gắn tên đặc vụ, tính cả hai ngôn ngữ và các tổ hợp đặc vụ. Mọi chuỗi đo khi mở Campaign Record đều vừa một dòng. Trích [qa.log](C:/Users/hieud/AppData/Local/Temp/HieuDHM/FPS/campaign-radio-singleline-904fac70/qa.log):

```text
Local host started; synchronizing GameScene
1920x1080 gameplay: 1140 captions, 1 line=1123, 2=17, 3+=0; widest: Raven: Phòng trên phải có điện mới vào được. Sổ có ghi cầu chì để đâu không?
1920x1080 record: 1140 captions, 1 line=1140, 2=0, 3+=0; widest: Raven: Phòng trên phải có điện mới vào được. Sổ có ghi cầu chì để đâu không?
3840x2160 gameplay: 1140 captions, 1 line=1116, 2=24, 3+=0; widest: Raven: Phòng trên phải có điện mới vào được. Sổ có ghi cầu chì để đâu không?
3840x2160 record: 1140 captions, 1 line=1140, 2=0, 3+=0; widest: Raven: Phòng trên phải có điện mới vào được. Sổ có ghi cầu chì để đâu không?
1440x1080 gameplay: 1140 captions, 1 line=363, 2=777, 3+=0; widest: Raven: Phòng trên phải có điện mới vào được. Sổ có ghi cầu chì để đâu không?
1440x1080 record: 1140 captions, 1 line=1140, 2=0, 3+=0; widest: Raven: Phòng trên phải có điện mới vào được. Sổ có ghi cầu chì để đâu không?
PASS: live radio, full caption matrix and Game View captures
```

Các skill và quy tắc đã áp dụng:

- [unity-mcp-ui-layout · Text Layout Rules](C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/references/text-layout-rules.md): kiểm tra quy tắc xuống dòng và bề rộng khung chữ; giữ cỡ chữ, kiểm lại chuỗi dài và bản dịch.
- [game-ui-ux · Core workflow](C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md): bước 3 và 7, kiểm tra khoảng cách với mép màn hình và HUD ở nhiều độ phân giải, tỷ lệ khung hình.
- UnitySkills [scriptableobject](C:/Users/hieud/.agents/skills/unity-skills/skills/scriptableobject/SKILL.md): dùng `scriptableobject_export_json` và `scriptableobject_import_json`, xuất lại để đối chiếu dữ liệu đã lưu.
- UnitySkills [scene](C:/Users/hieud/.agents/skills/unity-skills/skills/scene/SKILL.md): xác nhận cảnh đang mở và áp dụng Save Gate của [AGENTS.md](E:/Unity/Project/FPS/AGENTS.md) trước/sau thao tác Editor và test.
- UnitySkills [test](C:/Users/hieud/.agents/skills/unity-skills/skills/test/SKILL.md): lấy kết quả từ `test_get_result(jobId)` sau khi job PlayMode kết thúc.

Chưa chạy bản Windows độc lập, client co-op riêng hoặc toàn chiến dịch trong lần sửa dữ liệu này. Game View chạy lời tiếng Anh; tiếng Việt được đo độ vừa khung bằng cùng bộ hiển thị. Ảnh tạm đã được dọn sau rà soát độc lập; giữ lại log, các bản xuất catalog và mã kiểm thử trong thư mục QA.
