Audit đồ họa và độ khó — 05/10/2026

Trạng thái sau khắc phục — 05/10/2026

- Đã sửa cả 3 finding bên dưới. Phòng chờ chờ manager spawn rồi đồng bộ giá trị độ khó hiện tại, không phát lại sự kiện chọn; khi mở lại UI cũng đồng bộ lại.
- EnemyAI.SetStats cập nhật tốc độ nền đã scale cho zombie đang được theo dõi và xóa trạng thái boost cũ. Đăng ký lại không lấy tốc độ đang boost làm tốc độ nền; giữ nguyên luồng spawn/pool.
- Reset Defaults khôi phục preset đồ họa mặc định của nền tảng được ghi nhận trước khi scene khởi động và trước khi nạp lựa chọn đã lưu. Engine, SettingsManager, PlayerPrefs và dropdown cùng trở về giá trị đó; không hard-code một index cho mọi nền tảng.
- Regression được giữ trong `Assets/Tests/Editor/UI/SettingsDifficultyRegressionTests.cs`: job cuối `a92ae9c2`, 9/9 đạt. Kiểm tra gồm chờ spawn, đồng bộ lúc đăng ký/mở lại UI, boost/restore ở 4 độ khó, thay stats khi đang boost, tái sử dụng pool, và reset 3 preset mặc định giả lập.
- Compile hoàn tất; Console cuối có 0 error và 0 warning. Editor trở lại GameScene active, không có thay đổi scene chưa lưu; preset Low ban đầu và các preference đã thay trong test được phục hồi. Không sửa scene/prefab hay xóa thay đổi có sẵn của người dùng.
- Chưa chạy một trận multiplayer hoàn chỉnh. Các probe trong `.codex/settings-audit` bên dưới ghi nhận trạng thái trước khi sửa; dùng regression mới để kiểm tra bản đã khắc phục.

Nội dung audit ban đầu (trước khi sửa):

Phạm vi: SettingsUI/SettingsManager, lựa chọn độ khó trong phòng chờ, chuyển lựa chọn vào GameScene, scaling zombie, cơ chế đuổi kịp và những thông số campaign liên quan. Đây là audit; chưa sửa mã gameplay/UI.

Kết luận: preset đồ họa và các mức độ khó có tác dụng thật. Có hai lỗi đã tái hiện bằng kiểm tra chạy trong Unity và một thiếu sót được xác nhận từ toàn bộ hàm xử lý Reset Defaults. Không có bằng chứng rằng mọi preset đều render như nhau hoặc mọi trận đều chạy một độ khó.

| Ưu tiên | Vấn đề | Tác động |
|---|---|---|
| P1 | Phòng chờ hiển thị Easy nhưng giữ Medium | Người chơi có thể bắt đầu trận với độ khó khác lựa chọn đang hiển thị. |
| P2 | Cơ chế đuổi kịp giữ tốc độ trước khi scale độ khó | Zombie ở Hard/Pandemonium có thể bị khôi phục về tốc độ nền sau khi đuổi kịp. |
| P2 | Reset Defaults bỏ qua đồ họa | Quality đang chọn và giá trị lưu vẫn giữ nguyên dù UI thông báo đã khôi phục mặc định. |

1. Phòng chờ hiển thị Easy nhưng giữ Medium

- LobbyScene có DifficultyDropdown.value = 0, tương ứng Easy. WaitingRoomManager khởi tạo LobbyDifficulty bằng Medium.
- WaitingRoomUI.TrySubscribe chỉ đăng ký OnValueChanged, không đưa LobbyDifficulty.Value hiện tại vào dropdown. Start và RefreshPlayerList cũng không làm việc này.
- Do TMP_Dropdown không phát thay đổi khi chọn lại chính giá trị hiện tại, chọn Easy khi nó đang hiển thị Easy không gửi lựa chọn sang server. WaitingRoomManager.StartMatch lấy LobbyDifficulty.Value, nên vẫn truyền Medium.
- Người vào muộn cũng có thể bỏ lỡ thay đổi đã xảy ra trước lúc UI đăng ký.
- Hướng sửa nhỏ: đồng bộ giá trị hiện tại bằng SetValueWithoutNotify ngay khi đăng ký sự kiện; luôn hiển thị giá trị server đã xác nhận.
- Vị trí: Assets/FPS/Features/UI/Runtime/WaitingRoomUI.cs:134-146; Assets/FPS/Features/Networking/Runtime/WaitingRoomManager.cs:53,308.
- Bằng chứng chạy: `Authored dropdown=Easy; lobby state=Medium`.

2. Cơ chế đuổi kịp ghi nhớ tốc độ trước khi scale độ khó

- ZombiePoolManager.GetZombie gọi NetworkObject.Spawn trước khi trả đối tượng cho ZombieFactory. Nhánh không dùng pool cũng Spawn trước ApplyStats.
- EnemyAI.OnNetworkSpawn đăng ký zombie vào RubberBandingSystem. RegisterZombie chụp agent.speed lúc này vào originalSpeed.
- Sau đó ZombieFactory.ApplyStats gọi SetStats để áp dụng hệ số độ khó và đăng ký lại. RegisterZombie bỏ qua đối tượng đã có trong dictionary, nên originalSpeed không cập nhật.
- UpdateCatchUpSpeed dùng originalSpeed để tăng tốc rồi khôi phục tốc độ. Vì thế tốc độ phục hồi không còn là tốc độ đã scale. Hard/Pandemonium bật cơ chế này nên chịu ảnh hưởng; learning modifier tăng tốc cũng có cùng vấn đề.
- Hướng sửa nhỏ: thống nhất thời điểm chụp tốc độ sau SetStats, hoặc cập nhật tốc độ nền của registration đã tồn tại khi factory vừa áp dụng stats; tránh nhân lặp vào tốc độ đang được boost.
- Vị trí: Assets/FPS/Features/AI/Runtime/RubberBandingSystem.cs:63-72,196-205; Assets/FPS/Features/Characters/Enemies/Runtime/EnemyAI.cs:180,1200-1205; Assets/FPS/Features/Characters/Enemies/Runtime/ZombieFactory.cs:66,83,256-262; Assets/FPS/Features/Characters/Enemies/Runtime/ZombiePoolManager.cs:111.
- Bằng chứng chạy chuỗi đăng ký → SetStats → đăng ký lại: `Scaled agent speed=7.5; catch-up cached original=5`.
- Đây là tái hiện phương thức và đối chiếu thứ tự gọi; chưa quay trận mạng để đo số lần lỗi xảy ra trong gameplay.

3. Reset Defaults bỏ qua đồ họa

- SettingsUI.ResetDefaults reset audio, độ nhạy chuột và keybinding, sau đó hiển thị `Defaults restored.`
- Toàn bộ hàm không gọi SetGraphicsQuality, không cập nhật qualityDropdown và không xóa/khôi phục GraphicsQuality trong PlayerPrefs.
- Hướng sửa: định nghĩa preset đồ họa mặc định và áp dụng khi reset; nếu nút chỉ dành cho audio/input thì cần đổi phạm vi và thông báo cho đúng.
- Vị trí: Assets/FPS/Features/UI/Runtime/SettingsUI.cs:348-393.
- Xác nhận bằng đọc toàn bộ handler; chưa thao tác Reset Defaults trên tài khoản người dùng để tránh thay đổi các cài đặt khác.

Những điểm không tính là lỗi đã chứng minh

- Sáu preset QualitySettings có thông số khác nhau và SettingsManager thực sự gọi SetQualityLevel. Khác biệt mỹ thuật không nhất thiết lớn vì cùng model, vật liệu và bố cục cảnh.
- Cả bốn prefab Brimstone/Clove/Gekko/Sage và cả camera chính/camera vũ khí đều có HDR=True, MSAA=True, dynamic=False. Không xác nhận giả thuyết camera hiện tại bỏ mất HDR/MSAA khi đổi cảnh.
- Có một rủi ro tiềm ẩn: camera mới với HDR/MSAA bị tắt không được cấu hình lại nếu chọn lại cùng preset, do SetGraphicsQuality bỏ qua ApplyGraphicsQuality khi index không đổi. Probe xác nhận nhánh này, nhưng prefab người chơi hiện tại không có các cờ sai đó, nên không đưa vào ba finding chính.
- Máu/sát thương của quái đặc biệt đi qua EnemyScalingResolver và ScaleDamage/ScaleStatus. Các special có FSM riêng chủ động giữ cấu hình di chuyển; không kết luận mọi special phải nhận hệ số tốc độ zombie thường.
- Đạn tiếp tế, hồi phục, thời gian cứu người và thời gian nhiệm vụ đang dùng cấu hình campaign cố định hoặc điều kiện số người chơi. Đây là phần chưa thiết kế theo độ khó; chỉ tính là thiếu tính năng nếu yêu cầu sản phẩm muốn các giá trị ấy thay đổi. Giữ map/mỹ thuật giống nhau giữa độ khó không tự nó là bug.

Kiểm chứng

- Unity 6000.5.6f1, pipeline Built-in, GameScene và LobbyScene được kiểm tra qua Editor API; không đọc YAML scene/prefab.
- Codebase Memory được dùng để thu hẹp symbol/caller. Metadata của index ghi 13/09/2026 và có dấu hiệu thay đổi so với filesystem; mọi finding được đối chiếu với source hiện tại và trạng thái Unity, không dựa vào index cũ để kết luận thiếu code.
- Bộ probe cuối: job `2b017837`, 4/4 kiểm tra hoàn thành thành công. Các assert chủ động xác nhận hành vi lỗi và kiểm kê asset; kết quả này không có nghĩa sản phẩm không còn lỗi.
- Lần probe đầu có lỗi fixture vì Test Runner chạy scene riêng và cờ dynamic resolution không có hiệu lực trên cấu hình này. Đã sửa fixture và chạy lại; không tính chúng là lỗi sản phẩm.
- Probe được gỡ khỏi Assets sau khi chạy; bản có thể chạy lại giữ ở `.codex/settings-audit/SettingsDifficultyAuditProbeTests.cs`. Dữ liệu quan sát ở `.codex/settings-audit/evidence.txt`.
- Chạy lại bằng cách copy probe vào Assets/Tests/Editor/UI, mở Unity và chạy class FPS.Tests.SettingsDifficultyAuditProbeTests trong EditMode. Các probe tự phục hồi QualitySettings, camera flags và GraphicsQuality preference đã thay đổi trong test. Gỡ file probe cùng .meta khỏi Assets sau khi dùng.
- LobbyScene mở tạm đã được đóng; Editor trở lại GameScene. Không lưu thay đổi vào scene/prefab và không sửa mã sản phẩm.
- Kiểm tra cuối sau khi gỡ probe: Unity đã hoàn tất compile/update, Console có 0 error và 0 warning; chỉ GameScene đang mở, active và không có thay đổi chưa lưu.

Ưu tiên khắc phục: đồng bộ dropdown phòng chờ → cập nhật tốc độ nền sau khi scale zombie → hoàn thiện Reset Defaults. Không cần xây lại hệ thống setting hay difficulty để sửa ba điểm này.
