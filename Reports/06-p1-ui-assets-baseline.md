# P1 — Bot 6: baseline HUD và asset nhận diện Shipping

**Cập nhật cuối phiên:** P1 còn dở dang. Bot 6 nhận kiểm cleanup từ Bot 5; REST không kết nối được, UI không có cửa sổ Unity, log có shutdown. Save Gate cuối/restore GameScene/`isDirty=false` chưa xác minh được; hash 72 entry đầu–cuối giữ nguyên. Chi tiết và phần P1 chưa chạy ở [baseline flow](E:/Unity/Project/FPS/Reports/06-p1-flow-baseline.md). Phân công Bot 5 dưới đây mô tả giai đoạn thu baseline; không phải xác nhận cleanup đã hoàn tất.

Ngày 29/10/2026. **P1 đã bắt đầu theo lệnh người dùng.** Bot 5 là người duy nhất điều khiển Editor/Play/lifecycle/Save Gate; Bot 6 chỉ query asset/scene, đọc source đã khoanh và xem ảnh do Bot 5 chụp. Không code, rebind Shipping, sửa volume, gắn đèn/viền hoặc dựng mockup. Các điều kiện hiện hành nằm trong [P0 v3 — điều kiện duyệt](E:/Unity/Project/FPS/Reports/04-flow-proposal-v3-conditions.md).

## 1. Kết quả đã làm thật và giới hạn

| Kiểm | Kết quả thực tế | Giới hạn |
|---|---|---|
| AssetDatabase outline/đèn | **ĐÃ KIỂM**: tìm thấy ItemOutline, material/shader SurvivalOutline, bộ QuickOutline và bốn prefab đèn. [SCENE/ASSET asset-discovery.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot6/asset-discovery.json). | Có asset không có nghĩa đã bind hoặc render đúng trên thùng Shipping. |
| Ba thùng S1 | **ĐÃ KIỂM** bằng entityId + vị trí: thùng 20/22 không có proxy/viền/đèn trên root; thùng Manifest 21 có CampaignInteractionProxy, cả ba childCount=0 tại snapshot Edit Mode. [SCENE crate-fixture-components.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot6/crate-fixture-components.json). | Root không có component không loại trừ caller/reference ở object khác. Chưa thay binding cũ. |
| Call site viền hiện có | **ĐÃ KIỂM CÓ PHẠM VI**: InteractionManager raycast rồi ShowOutline/HideOutline; không phải cơ chế tự hiện khi đến gần trước khi ngắm. | Codebase Memory trả `projects=[]`; không tạo index. Chỉ đọc các source được AssetDatabase/call site khoanh, không chứng nhận toàn project không có cơ chế khác. |
| HUD live 1080p/4K | **ĐÃ CHỤP bởi Bot 5, ĐÃ XEM bởi Bot 6**: local host, phase Exploring, tracked FactoryManifest. | Hai frame khác thời điểm; không có after vì chưa sửa. Không phải visual approval, test người mới hoặc kiểm HUD toàn trạng thái. |
| Viền Shipping trước ngắm / đúng target từng client | **NOT RUN** trên binding S1 mới. | S1 chưa được triển khai; không gắn thử component hoặc đổi scene để làm baseline qua. |
| Đèn trạng thái Isolate/Backup/Generator | **Asset đã kiểm; hành vi mới NOT RUN**. | Chưa gắn/đổi state đèn; có rủi ro ánh sáng bake và renderer chung (§4). |
| Lift bốn người + Tank / safe anchors / cửa 5+3 | Kết quả do Bot 5 chủ trì; Bot 6 **không ký đạt** từ diện tích hay ảnh solo. | Bốn capsule hoặc bốn lượt solo không thay bốn actor kết nối đồng thời. |
| Năm cue / license | **Chưa nghe, chưa chốt license**. Không phát clip phụ trong lúc Bot 5 điều khiển phiên. | Thiếu âm máy phát tạm đã được người dùng chấp nhận; không cản baseline. Không import/tạo âm. |

## 2. Định danh S1: tránh bind nhầm thùng trùng tên/path

**[SCENE]** Query tên ba thùng trả **9 object**, gồm nhiều object trùng cả tên lẫn hierarchy path trong `CampaignWorld/Factory/Map_v2/Static`. Ba object liên quan đề xuất S1 tại snapshot trước Play là:

| Vai trò | Tên | EntityId trong snapshot | Vị trí world | Component root |
|---|---|---|---|---|
| Shipping S1 đã được chọn | Wooden_box_v1_LD1square (20) | 568105589213745024 | (92.11679, 2.498578, −86.07217) | Transform, MeshFilter, MeshRenderer, BoxCollider |
| Manifest hiện có | Wooden_box_v1_LD1square (21) | 568105584918900330 | (100.303993, 2.498577, −77.46367) | Các component trên + CampaignInteractionProxy |
| Thùng giống gần đó | Wooden_box_v1_LD1square (22) | 568105589213735686 | (77.501, 2.498578, −71.67561) | Transform, MeshFilter, MeshRenderer, BoxCollider |

EntityId là định danh query của snapshot, **phải tìm/kiểm lại sau reload**; không ghi các số trên thành khóa runtime/save. Khi được phép triển khai S1, đối chiếu vị trí, renderer/collider và serialized reference trước khi bind; không dùng tên/path đơn độc. Ba thùng giống hình là dữ kiện người dùng đã nêu; kiểm root hiện tại cũng chưa thấy một child fixture/nhãn phân biệt trên ba thùng này. Chưa đứng tại chúng trong Game-view để chấm nhận diện.

Nguồn thùng sẵn: [Wooden_box_v1_LD1square.prefab](E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/Boxes/Wooden_box_v1/Wooden_box_v1_LD1square.prefab). Khoảng cách Manifest–thùng 20 khoảng 11,88 m và đổi điểm bấm Shipping khoảng 16,55 m là **đường thẳng từ v3**, không phải route NavMesh mới đã đo.

## 3. Asset tìm được và phương án trình P2

| Asset/phương tiện sẵn | Bằng chứng / phần tái dùng được | Điều chưa được chứng minh |
|---|---|---|
| [ItemOutline.cs](E:/Unity/Project/FPS/Assets/FPS/Features/Interaction/Runtime/ItemOutline.cs:66) | Có ShowOutline/HideOutline và slot material. [InteractionManager.cs:127](E:/Unity/Project/FPS/Assets/FPS/Features/Interaction/Runtime/InteractionManager.cs:127) lấy raycast; [:157](E:/Unity/Project/FPS/Assets/FPS/Features/Interaction/Runtime/InteractionManager.cs:157) tìm ItemOutline rồi bật. | Chưa có trên ba root thùng; caller hiện tại chỉ chọn sau ngắm, chưa đáp ứng pre-aim. Cần kiểm render trên mesh thùng sau khi được phép triển khai. |
| [Outline0.mat](E:/Unity/Project/FPS/Assets/FPS/Features/Survival/Content/Materials/Outline0.mat), [Outline1.mat](E:/Unity/Project/FPS/Assets/FPS/Features/Survival/Content/Materials/Outline1.mat), [Outline2.mat](E:/Unity/Project/FPS/Assets/FPS/Features/Survival/Content/Materials/Outline2.mat), [Outline3.mat](E:/Unity/Project/FPS/Assets/FPS/Features/Survival/Content/Materials/Outline3.mat) + [SurvivalOutline.shader](E:/Unity/Project/FPS/Assets/FPS/Features/Survival/Content/Materials/SurvivalOutline.shader:1) | AssetDatabase xác nhận bốn material và shader `FPS/Survival/InteractionOutline`. Shader có viền theo normal, `Cull Front`, `ZTest LEqual`, độ rộng world. | Chưa kiểm material nào hợp theme, submesh/thickness/độ rõ 1080p–4K trên thùng. Shader chưa có alpha blending; không hứa pulse alpha của ItemOutline sẽ thành nhịp sáng nhìn thấy. |
| [Outline.cs](E:/Unity/Project/FPS/Assets/ThirdParty/QuickOutline/Scripts/Outline.cs:19), [OutlineFill.mat](E:/Unity/Project/FPS/Assets/ThirdParty/QuickOutline/Resources/Materials/OutlineFill.mat), [OutlineMask.mat](E:/Unity/Project/FPS/Assets/ThirdParty/QuickOutline/Resources/Materials/OutlineMask.mat) | Bộ QuickOutline sẵn có mode OutlineVisible và material/shader. Đây là asset dự phòng đã tồn tại, không phải yêu cầu thêm plugin. | Chưa render probe trên Shipping; nguồn license chưa được xác nhận độc lập. Query component tên `Outline` có thể khớp UGUI Outline nên không dùng nó làm chứng nhận QuickOutline đã hoạt động trong scene. |
| [LampWall.prefab](E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/LampWall.prefab), [NeonLamp.prefab](E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/AsylumFacility/Prefabs/NeonLamp.prefab) | AssetDatabase xác nhận hai prefab; scene có fixture cùng tên với child Point light. [fixture-hierarchy.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot6/fixture-hierarchy.json). | Các instance scene đã unpack/không được prefab_find_instances nhận là liên kết còn sống (query trả 0); không suy ra prefab vắng. Fit/scale/anchor trên thiết bị chưa chốt. |
| [LightWhite.mat](E:/Unity/Project/FPS/Assets/FPS/Features/World/Content/ExperimentFacility/Materials/LightWhite.mat) | AssetDatabase xác nhận material trắng có sẵn. | Chưa kiểm emission/runtime switching; không coi tên material là bằng chứng đèn trạng thái đã hoạt động. |
| Icon chỉ đích sẵn | [GoalMarker.png](E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/GoalMarker.png), [GoalMarkerCentre.png](E:/Unity/Project/FPS/Assets/FPS/Features/UI/Content/Resources/HudSkin/FPSSample/GoalMarkerCentre.png). | Chỉ là ứng viên marker màn hình, không thay yêu cầu nhận diện thân thùng trước ngắm. Chưa chọn bố cục HUD. |

**[ĐỀ XUẤT để P2 duyệt, chưa triển khai]** Ưu tiên **viền theo đúng silhouette của thùng 20**, tái dùng ItemOutline + material/shader sẵn sau kiểm render. Viền hiện khi Shipping là mục tiêu đang theo của người chơi và họ lại gần, **không đợi raycast/ngắm**; ngắm mới thêm tag màn hình `Shipping — Verify cargo record` + prompt/lock reason. Không tạo text 3D, icon, shader hoặc prop bằng code. Cần nối điều kiện tracking/proximity vào phương tiện có sẵn sau hai gate; việc tìm thấy asset chưa làm xong phần nối này.

Viền thuộc presentation **từng client/người theo dõi**: hai người đang theo hai nhánh không làm viền của nhau đổi mục tiêu. Server vẫn quyết định prerequisite/completion. Không network một màu/boolean viền chung làm cả đội bị đổi target; không sửa state nhiệm vụ để bật viền. Viền của mục tiêu khóa dùng trắng/xám và tag khóa, **không cam như có thể bấm**. Cam chỉ hành động khả dụng ngay theo theme. Khoảng “lại gần”, độ dày và asset material cuối cùng chờ P2 chọn cách HUD rồi kiểm trong Game-view; chưa tự chốt một ngưỡng khoảng cách mới.

Trong ba thùng tương tự, trước Manifest: chỉ thân Manifest 21 mang chỉ đích khi được theo; sau đọc: Shipping 20 mang chỉ đích nếu nhánh Logistics vẫn được theo; thùng 22 không nhận chỉ đích nhiệm vụ. Nếu muốn xem lại Manifest khi Shipping đang theo, nguồn đã ghi vẫn xem qua sổ, không làm hai thân cùng giả là bước kế. Phải kiểm đúng reference và cả góc nhìn không ngắm ở P5/P6 trước ký điều kiện S1.

## 4. Hai kênh riêng và rủi ro tái dùng

| Kênh | Phương tiện / vị trí | Quy tắc phân biệt |
|---|---|---|
| Trạng thái xong/chưa | Fixture đèn **nhỏ, cố định** tại Isolate/Backup/Generator, tái dùng mesh đèn sẵn; sáng ổn định hoặc tắt theo state server. Screen tag khi ngắm ghi ISOLATED/BACKUP READY/RUNNING. | Không bật vì đổi tracking. Isolate hoàn tất: báo mạch sáng→tắt; Backup/Generator hoàn tất: tắt→trắng ổn định theo v3. Không yêu cầu người chơi nhớ màu để hiểu kết quả; chữ ghi nhận/cue/marker vẫn đi cùng Accepted. |
| Đây là thân cần bấm | **Viền quanh silhouette** thân đang theo; độc lập fixture đèn. Khi gần đã thấy viền trước ngắm; khi ngắm mới thêm tên/action/khóa. | Đổi tracking chuyển viền; completion chuyển target sau state. Có viền không nghĩa là xong. Mục tiêu khóa vẫn được nhận diện, không [F] cho hành động khóa. |

Hai kênh khác **vị trí/hình dạng/hành vi**, không chỉ khác màu. Viền trên thân thùng Shipping không dùng đèn completion làm chỉ đích và không tắt/bật ánh sáng toàn map để báo nhiệm vụ.

Các giới hạn đã kiểm:

- **[SCENE] Ánh sáng bake:** mẫu LampWall scene có `m_BakingOutput.isBaked=True`; mẫu Neon kiểm được False. [fixture-light-serialized.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot6/fixture-light-serialized.json). Không mặc định bật/tắt Light.enabled sẽ loại bỏ chiếu sáng đã bake. Sau duyệt, fixture trạng thái cục bộ phải có trạng thái emission/đèn động được kiểm; không thay cấu hình đèn map dùng chung. Chưa thử on/off trong baseline.
- **[CODE] Renderer/property block:** [ItemOutline.cs:91](E:/Unity/Project/FPS/Assets/FPS/Features/Interaction/Runtime/ItemOutline.cs:91) khôi phục materials và `SetPropertyBlock(null)` khi ẩn. Nếu cùng renderer còn dùng property block cho trạng thái, ẩn viền có thể xóa trạng thái hình đó. Đề xuất fixture trạng thái có renderer riêng; kiểm không đổi material asset dùng chung. Chưa sửa helper hoặc thêm abstraction.
- **[CODE] Không tái dùng nguyên hành vi pickup cho tủ/thùng:** [SurvivalPickupPresentation.cs:55](E:/Unity/Project/FPS/Assets/FPS/Features/Survival/Runtime/SurvivalPickupPresentation.cs:55) có bob/rotate idle và [:25](E:/Unity/Project/FPS/Assets/FPS/Features/Survival/Runtime/SurvivalPickupPresentation.cs:25) tạo shell. Không gắn nguyên component vào thùng để làm cả thùng quay/nảy. Ưu tiên phương tiện ItemOutline hiện có và kiểm mesh; chưa viết thêm code tạo shell/primitive.
- **[INFERENCE] Pipeline/render:** có shader chưa chứng minh shader hợp renderer hiện dùng và mesh thùng. Nếu probe sau duyệt không đạt, dừng chọn phương tiện/asset và trình lại; không tự viết shader mới hoặc coi click/tag sau ngắm là đã thay được viền.

## 5. HUD baseline live đã xem

Bot 5 chụp trong local-host Play Mode, phase **Exploring**, `Tracked objective: FactoryManifest`, không phải preview tĩnh. Ảnh và metadata:

- [Game-view 1920×1080](E:/Unity/Project/FPS/Reports/P1Evidence/Bot6/baseline-factory-1920x1080.png) và [metadata 1080p](E:/Unity/Project/FPS/Reports/P1Evidence/Bot6/baseline-factory-1920x1080.txt).
- [Game-view 3840×2160](E:/Unity/Project/FPS/Reports/P1Evidence/Bot6/baseline-factory-3840x2160.png) và [metadata 4K](E:/Unity/Project/FPS/Reports/P1Evidence/Bot6/baseline-factory-3840x2160.txt).

**[PLAYTEST screenshot — quan sát thật, không chốt thiết kế]** Tracker ghi `Read the shipping manifest`; marker đang nhìn thấy ghi `271m ROUTE`, chưa nêu tên Manifest. Người chơi thấy việc cần làm nhưng tên đích trên marker chưa đáp ứng hợp đồng “tên vật + khoảng cách”. Không suy từ nhãn ROUTE rằng tọa độ marker chắc chắn trỏ nhầm object; cần đối chiếu navigation target theo snapshot Bot 5. Hai nhánh Power/Logistics và phím T có trong tracker; không kiểm được bằng một ảnh rằng đổi nhánh đã hoạt động.

**[PLAYTEST screenshot + metadata]** Bố cục các panel ở hai độ phân giải giữ tỷ lệ tương ứng: MissionTracker 520×264 tại 1080p và 1040×528 tại 4K; không thấy tràn trong các nhãn ngắn được chụp. Metadata TMP được chụp báo overflow=False. Máu và nhiễm đang là hai panel tách riêng, đúng baseline trước đợt gộp HUD. Kết quả này không chứng minh panel đối chiếu/khóa/hold/hủy, nhãn dài, safe area khác tỷ lệ hay client khác đã đạt.

**[PLAYTEST screenshot — khác thời điểm]** Subtitle mở đầu có trong ảnh 1080p, không có trong frame 4K chụp sau. Không gọi đây là lỗi 4K làm mất subtitle. Chưa thử replay thoại hoặc cue cảnh báo chèn thoại từ ảnh này. Không kết luận tỷ lệ bỏ cuộc/định hướng người mới từ người QA đã biết map.

## 6. License, skill và bàn giao

Asset trên là kho **đã có**, không thêm asset mới. Scoped search trong `Assets/ThirdParty/QuickOutline` và `Assets/ThirdParty/Imported/ProjectSettingsPack` chưa thấy tài liệu LICENSE/NOTICE/README phù hợp; [license-scope.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot6/license-scope.json). Header QuickOutline nêu Chris Nolet/copyright, **không tự là giấy phép**; chưa xác minh gói nguồn ngoài dự án. Không khẳng định asset không có quyền dùng. Năm cue ở P0 vẫn là ứng viên chưa nghe/chưa chốt license; thiếu âm máy phát được chấp nhận tạm, không báo đã có âm máy.

Codebase Memory đã query trước đọc symbol, trả không có project indexed; không index lại trong P1. Search source giới hạn Interaction/Runtime, Survival/Runtime và Missions cho tên outline/call site; không repo-wide grep. Không đọc raw .meta/.unity/.prefab YAML; serialized queries tránh getter `.mesh`/`.material`.

| Skill đã đọc / phần áp dụng | Quy tắc dùng tại P1 này |
|---|---|
| [Unity-Skills](C:/Users/hieud/.agents/skills/unity-skills/SKILL.md) | Health/schema/query trước; chỉ đọc, không tự Play/save hoặc authoring; Bot 5 điều phối Save Gate. |
| [unity-asset](C:/Users/hieud/.agents/skills/unity-skills/skills/asset/SKILL.md) | AssetDatabase tìm asset trước khi đề xuất; không import/generate. |
| [unity-gameobject](C:/Users/hieud/.agents/skills/unity-skills/skills/gameobject/SKILL.md) | Dùng entityId + vị trí, kiểm component/children; trùng tên/path không là identity đủ. |
| [unity-component — phần query](C:/Users/hieud/.agents/skills/unity-skills/skills/component/SKILL.md) | Ưu tiên serialized fields, không gọi setter/getter tạo instance; không đổi component. |
| [game-ui-ux](C:/Users/hieud/.agents/skills/game-ui-ux/SKILL.md) | Phân biệt ý nghĩa hình, không chỉ màu; kiểm ảnh ở hai độ phân giải và nêu trạng thái chưa quan sát. |
| [unity-mcp-ui-layout — phần asset strategy/verification](C:/Users/hieud/.agents/skills/unity-mcp-ui-layout/SKILL.md) | Có asset/capture chưa là visual approval; không dựng mockup khi cách làm HUD chưa chọn. |

Phạm vi file sẽ liên quan **khi được phép code**: CampaignInteractionProxy/Interactable và serialized binding Shipping; CampaignHUD.Navigation/ObjectiveSelection cho target theo người; ItemOutline/material được chọn, reference fixture đèn và controller state; test binding/idempotency/late join/client-local presentation. Đây là danh sách tác động để bàn giao, **không phải file đã sửa**. Caller graph đầy đủ/ownership/runtime render và test triển khai phải được kiểm lại trong P4/P5; không nâng coverage source hẹp thành chứng nhận toàn hệ.

**[INFERENCE — kiểm tài liệu]** [report-verification.json](E:/Unity/Project/FPS/Reports/P1Evidence/Bot6/report-verification.json) ghi kiểm liên kết/dòng source/cột bảng và hash sáu file source/AGENTS đã đọc; không có thay đổi hash. Không giữ helper script tạm. Bot 5 vẫn là chủ kiểm scene cuối/Save Gate; Bot 6 không gọi save và không kết luận Play đã dừng trong khi phiên baseline còn chạy.

**Bàn giao:** P1 đã có asset inventory, rủi ro chính, phương án nhận diện để P2 duyệt và ảnh baseline live. P2 chờ người dùng **chọn cách làm HUD**, không yêu cầu ảnh mẫu nữa. Shipping S1 được chọn có điều kiện nhưng chưa rebind; hai kênh nhận diện chưa triển khai. AGENTS.md/diff chưa sửa hoặc áp dụng.

Chưa kiểm: render viền trên Shipping, state đèn on/off, chủ quyền viền ở hai client, nghe/license cue, Lift bốn người và safe anchors. Rủi ro: asset tồn tại chưa đủ ký điều kiện nhận diện; ánh sáng bake và vật trùng tên có thể làm triển khai sai nếu không kiểm reference/render.
