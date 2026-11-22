# Dialogue và nhật ký — chờ người dùng duyệt

Đây là bản đọc duyệt nội dung hiện hành, xuất từ Unity ngày 18/11/2026. Chưa có thay đổi câu chữ hoặc flow nào được thực hiện trong lượt xuất này.

- [28 câu radio, VI + EN](45A-dialogue-cho-duyet.md): bản ngắn đã sửa, có ID và thời điểm phát, nhóm theo khu vực.
- [21 hồ sơ / nhật ký, VI + EN](45B-nhat-ky-ho-so-cho-duyet.md): nguyên văn đầy đủ, gồm 9 bắt buộc và 12 tùy chọn. Phần này chưa được viết lại trong hai lượt sửa radio trước.

## Campaign Record trong game

Mục Campaign Record hiện là bản ghi các radio đã mở theo tiến độ, không có một đoạn tường thuật riêng. Nội dung có thể xuất hiện gồm D01; D02–D04 theo chương; D05–D14 theo mục tiêu đã hoàn thành; D21 khi xác nhận nhận đủ bản sao. Các câu cảnh báo/encounter không được ghi toàn bộ vào mục này. Đây là mô tả nội dung có thể được ghi, không phải bản chụp tiến độ save hiện tại.

## Cách đánh dấu góp ý

Ghi ID và ý muốn sửa, ví dụ: `D16: ...`, `FILE_14: ...`. Có thể sửa trực tiếp hai tài liệu đọc duyệt; chúng chưa tự đồng bộ vào game. Từng câu và từng hồ sơ đều có cả VI và EN để duyệt nghĩa lẫn giọng.

Tên đặc vụ hiện thống nhất Mason / Doc / Vega / Raven; Bộ chỉ huy dùng Command. Tên người trong hồ sơ chứng cứ được giữ nguyên để bạn đọc và duyệt trong ngữ cảnh.

## Đối chiếu nguồn

Nguồn nội dung: `Assets/FPS/Features/World/Content/Campaign/Data/CampaignContentCatalog.asset`, đọc qua UnitySkills SerializedObject. Đã kiểm 28 ID radio và 21 ID hồ sơ, đủ cả hai ngôn ngữ, giữ nguyên nội dung và xuống dòng. Snapshot và manifest ở `Narrative-2026-11-18/user-review/`.

Skill đã đọc: unity-skills và unity-scriptableobject; áp dụng truy vấn asset qua Editor, Save Gate trước/sau và kiểm scene sạch. Campaign Record được đối chiếu với CampaignDialogue.Journal và TeamInventoryUI.SelectCampaignRecord trong source hiện hành.

Không chạy lại playtest hoặc sửa nội dung game trong lượt này. Bản VI là dữ liệu hiện có; game hiện vẫn dùng EN.
