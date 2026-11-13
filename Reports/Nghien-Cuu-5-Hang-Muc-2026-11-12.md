# Nghiên cứu 5 hạng mục: Nhu yếu phẩm, Âm thanh, Hit reaction, Death/Gục, Ánh sáng

Ngày: 2026-11-12. Trạng thái: **chỉ nghiên cứu, chưa sửa code/scene/asset nào**. Chờ duyệt flow trước khi thực thi (AGENTS.md → Approval Sequence).

Nhãn bằng chứng: `[CODE file:line]`, `[SCENE]` (truy vấn Unity Editor qua Unity-Skills), `[INFERENCE]` (suy luận, chưa kiểm chứng bằng playtest).

## 0. Cách làm và giới hạn

- Skill đã đọc: `unity-skills` (quy tắc dryRun/đọc schema trước khi gọi), `audio-design` (bus thay vì volume từng clip; nhạc adaptive theo layer; ducking bus riêng), `game-feel` (feedback xếp tầng theo mức quan trọng; không khoá input), `fps-shooter` (hit feedback bắt buộc; server authority).
- Skill `level-design` mà AGENTS.md yêu cầu cho quest/level flow **không có trong phiên này**, nên chưa đọc. Chỉ đọc file SKILL.md chính, chưa đọc các file `references/`.
- Codebase Memory MCP: index cả repo thất bại, đã index riêng `Assets/FPS` (9334 node). Phần lớn tra cứu thực tế làm bằng grep có phạm vi + đọc file.
- Editor: đã chuyển từ MainMenu (sạch) sang GameScene để đo ánh sáng. GameScene `isDirty == false` trước và sau. Không lưu gì.
- Chưa playtest, chưa chụp Game view. Một ảnh Scene View thử nghiệm bị sai khung hình nên đã xoá.
- Render pipeline là **Built-in, Forward, Linear** (không phải URP) `[SCENE graphics_get_overview]`.

---

## 1. Lỗi ánh sáng (ưu tiên 1 vì là bug)

### Hiện trạng `[SCENE]`

316 đèn trong GameScene, phân bố rất lệch:

| Khu | Đèn chiếu sáng thật | Light Probe Group | Reflection Probe |
|---|---|---|---|
| Laboratory | 124 (point, mixed, range 8–9, intensity 0.8–2.2) | 1 | 6 |
| Asylum | 98 (point, mixed) | 27 | 40 |
| **Factory** | **21 point + 1 directional** | **0** | **0** |

(72 đèn còn lại là đèn glow nhỏ range 0.7 của bàn nhu yếu phẩm, không chiếu sáng môi trường.)

Factory rộng khoảng 130 × 200 m, có khoảng 40 khối `Hangar_*`. Trong 21 point light của Factory:
- 17 đèn có `range = 2`, `intensity = 3`, treo ở độ cao 4.3–5.8 m. Bán kính 2 m ở độ cao đó **không chạm tới sàn**, chỉ là đốm sáng quanh bóng đèn.
- 4 đèn có `range = 5`, dồn ở một góc (x≈110).
- Tất cả tập trung thành 5–6 cụm; phần lớn hangar không có đèn nào.

Ánh sáng còn lại của Factory: 1 directional realtime (intensity 1.5) + ambient Skybox (intensity 1) + lightmap baked (5 lightmap 2048). Fog tắt.

### Nguyên nhân gốc

1. Trong hangar, directional bị mái/tường chắn, nên chỉ còn GI baked từ cửa hắt vào. Càng sâu càng về 0. Không có đèn nội thất bù.
2. Factory không có Light Probe nào, nên vật thể động (người chơi 3P, zombie, vũ khí, pickup) trong hangar lấy ambient probe toàn cục hoặc tối đen, không khớp với nền. `[INFERENCE]`
3. Không có đèn pin hay nguồn sáng nào gắn theo người chơi (grep `flashlight|torch` không có kết quả) `[CODE]`.
4. Quality level trong Editor đang là "Low" (shadows Disable). Cần kiểm tra build dùng level nào vì ảnh hưởng độ tương phản.

### Đề xuất sửa

- **E1 (khuyến nghị)**: Đèn nội thất cho từng hangar đi vào được. Dùng lại prefab đèn có sẵn trong `Assets/` (ví dụ `NeonLamp`, `CeilingFixture` của Asylum/Lab) làm nguồn sáng có vật lý, đặt theo lưới trần, range đủ chạm sàn (8–10 m), mode Mixed/Baked. Thêm Light Probe Group theo lối đi và 1 Reflection Probe mỗi hangar lớn. Bake lại lightmap.
- **E2**: Nâng sàn ambient tối thiểu cho khu Factory để góc khuất vẫn đọc được hình khối (không sáng phẳng).
- **E3 (đề xuất thêm, không tự làm)**: Đèn pin gắn vũ khí cho người chơi. Đây là tính năng mới, đụng weapon/input/network nên chỉ liệt kê.
- Nghiệm thu: chụp Game view 1080p và 4K trước/sau tại cùng các điểm đứng trong ít nhất 5 hangar + playtest đi sâu vào góc.

Rủi ro: bake lightmap toàn scene lâu và ghi đè lightmap của Asylum/Lab. `FactoryPitFillLighting` đang mượn lightmap của đường kế bên `[CODE FactoryPitFillLighting.cs:17]`, sau khi bake lại cần kiểm tra lại.

---

## 2. Phân bổ nhu yếu phẩm

### Hiện trạng

- 108 `CampaignSupply` đặt tĩnh trên 9 bàn (`SupplyBench_*`), 12 món mỗi bàn, 3 bàn mỗi chapter `[CODE ApprovedPropImplementation.Downloaded.cs:340-351]`.
- Không có spawn động. Thứ duy nhất thay đổi theo người chơi là `minimumPartySize` (1/2/3/4): món nào vượt quy mô đội thì ẩn `[CODE CampaignSupply.cs:23]`.
- Server quyết định việc nhận: kiểm tra chapter, tầm với, sức chứa, đã nhận chưa `[CODE CampaignMissionController.cs:734-753]`. Phần này đúng yêu cầu authority.
- **Nguồn gây "hiện ra giữa mặt"** `[INFERENCE]`: `CampaignSupply.Update` bật/tắt renderer ngay lập tức khi `supplyPartySize` đổi `[CODE CampaignSupply.cs:56-64]`, mà giá trị này được tính lại ở mỗi checkpoint chẵn `[CODE CampaignMissionController.cs:764]`. Người đứng cạnh bàn lúc đó sẽ thấy món đồ hiện/biến mất tức thì. Chưa tái hiện bằng playtest.
- AI Director đã có sẵn tín hiệu dùng được: `CurrentPhase` (BUILD/PEAK/RELAX), `Intensity`, `ActorPressure` theo từng người `[CODE AIDirector.cs:100-110, AIDirector.AdaptiveBridge.cs:33]`. `DirectorSpawnService` đã có kiểm tra khoảng cách, frustum và line-of-sight cho quái `[CODE DirectorSpawnService.cs:117-211]`, tái sử dụng được cho đồ.
- Nhu yếu phẩm không dùng gì từ Director hiện tại.

### Đề xuất flow (cần duyệt vì thêm interactable)

- **A1 Tầng cố định (bắt buộc)**: giữ các bàn hiện có ở chỗ cần thiết (đầu chapter, trước encounter/Tank). Rà lại số lượng từng loại theo chapter, có bảng số trước/sau để bạn duyệt.
- **A2 Tầng động theo Director**: thêm các "điểm tiếp tế" đặt tay trong scene (anchor trên kệ/thùng/bàn có sẵn, có vật chứa vật lý). Server chọn điểm nào được kích hoạt, dựa trên: máu/đạn trung bình của đội, số lần gục gần đây, `ActorPressure`. Đội đang khó thì mở thêm điểm ở phía trước tuyến đi; đội đang dư thì không mở.
- **A3 Quy tắc cấm hiện trước mặt** (áp cho cả A1 và A2): chỉ đổi trạng thái hiển thị khi **mọi** người chơi còn sống đều thoả: ngoài frustum, không có line-of-sight, và cách tối thiểu N mét. Nếu không thoả thì hoãn, thử lại sau. Quyết định ở server, client chỉ hiển thị.
- **A4**: Sửa luôn nguồn pop-in hiện tại ở `CampaignSupply.Update` bằng cùng quy tắc A3.
- Test: Editor test cho hàm chọn điểm (thuần logic) + PlayMode host/client xác nhận không có kích hoạt nào trong tầm nhìn.

---

## 3. Âm thanh

### Hiện trạng

- Toàn dự án có **25 file audio**: 1 nhạc menu (`DE2_main_menu_new.wav`, 23 MB), 3 tiếng súng/reload, 5 tiếng Tank (thư mục `source`, chưa rõ đã gán chưa), 8 tiếng survival (nổ, lửa, pickup, ném, chữa), 8 tiếng UI.
- Mixer `Sound.mixer` chỉ có 2 group (Master, SFX) và 1 tham số expose (`sfx`). Không có group Music/Ambience/UI/Voice.
- `AudioManager` `[CODE AudioManager.cs]`:
  - Nhạc chỉ phát ở MainMenu/LobbyScene, vào game thì dừng hẳn (dòng 98-116). Không có nhạc gameplay, không có ambience.
  - Master dùng `AudioListener.volume`, Music chỉnh thẳng `musicSource.volume`, không đi qua mixer (dòng 160-183). Trái quy tắc "route mọi nguồn qua bus".
  - `PlaySFXSound` là một `AudioSource` 2D duy nhất dùng `PlayOneShot` (dòng 118-127).
- Súng: mọi vũ khí phát tiếng qua `PlaySFXSound` 2D `[CODE Weapon.cs:644,1001,1037]`. Hệ quả: tiếng súng đồng đội không có vị trí/khoảng cách; mỗi vũ khí chỉ có 1 clip bắn + 1 clip reload, không biến thiên pitch.
- Quái: có sẵn slot `attackSound`, `deathSound`, và các slot của Tank/Infector/Screamer `[CODE EnemyAI.cs:62-63, SI_Tank.cs:48-50, SI_Infector.cs:42-45, SI_Screamer.cs:30]`, phát 3D qua `SpecialSpatialAudio`. Hạ tầng có, **chưa kiểm tra slot nào đã gán clip**; zombie thường không có file audio nào trong dự án.
- Người chơi: không có bước chân, nhảy/tiếp đất, trúng đòn, gục, hồi sinh. Chỉ có slot ho/tim đập/implant của hệ nhiễm `[CODE PlayerInfectionController.cs:44-46]`.
- Không có code nào đọc `AIDirector.CurrentPhase` để điều khiển nhạc.

### Đề xuất

- **B1 Kiến trúc mixer**: `Master ← {Music, SFX ← {Weapons, Enemies, Player, World}, Ambience, UI, Voice}`, expose volume từng group, slider map theo dB. Thêm bus `MusicDuck` để duck nhạc khi có thoại/tiếng Tank gầm mà không đè lên slider của người chơi. Chuyển Master/Music về mixer.
- **B2 Dịch vụ SFX**: pool `AudioSource` 3D, mỗi "sự kiện âm thanh" là một ScriptableObject gồm mảng clip + khoảng pitch/volume ngẫu nhiên + group + min/max distance. Súng của người khác phát 3D, của mình phát 2D.
- **B3 Nhạc adaptive theo Director**: 3 trạng thái gameplay khớp `GamePhase`: RELAX (ambience + pad nhẹ), BUILD (thêm layer căng), PEAK (layer combat). Thêm stinger cho Tank xuất hiện, đồng đội gục, hoàn thành mục tiêu. Menu và Lobby có nhạc riêng. Crossfade theo layer. Client chỉ đọc phase đã được replicate.
- **B4 Danh sách asset cần có** (ước lượng tối thiểu ~90–120 clip): 6 súng × (bắn 3–4 biến thể, reload, hết đạn, rút súng); zombie × (idle gầm gừ, phát hiện, tấn công, trúng đạn, chết) nhiều biến thể; bước chân theo 2–3 loại bề mặt; va chạm đạn theo bề mặt; người chơi trúng đòn/gục/hồi sinh; ambience 3 chapter; 5–7 track/stem nhạc.

**Điểm chặn**: dự án không có asset nào cho phần lớn danh sách B4. AGENTS.md cấm tự tạo asset và yêu cầu ghi nguồn + license. Cần bạn chọn nguồn (xem câu hỏi cuối). B1–B3 làm được trước với clip hiện có, slot còn thiếu để trống và liệt kê rõ.

---

## 4. Phản ứng từng bộ phận của kẻ địch khi trúng đạn

### Hiện trạng

- Hitbox theo vùng đã có: `HitboxZone { Body, Head, Chest, Arm, Leg }` với hệ số Head ×4, Arm ×0.75, Leg ×0.5 `[CODE HitboxSegment.cs:6-13, 84-93]`.
- `DamageInfo` mang đủ `hitZone`, `hitPoint`, `sourcePosition`, `damageType` `[CODE DamageInfo.cs:16-57]`, và `EnemyHealth.OnDamageApplied` phát sự kiện này trên server `[CODE EnemyHealth.cs:166]`.
- Nhưng chỉ có `SI_Tank` lắng nghe (để cộng dồn stagger). Zombie thường và các special khác **không phản ứng gì** khi trúng đạn `[CODE SI_Tank.cs:737]`.
- Animator zombie chỉ có 3 state: Locomotion, Attack, Death `[SCENE animator_list_states ZombieHumanoid/ZombieAnim]`. Không có state hay clip hit reaction nào trong dự án. Có sẵn clip chưa dùng: `zombie crawl`, `running crawl`, `zombie dying`, `zombie scream`.
- Phía người bắn đã có hit marker theo vùng `[CODE HUDManager.cs:930, WeaponFireHandler.cs:980-1007]`.
- Không có VFX máu/va chạm trên cơ thể quái gắn với vùng trúng (grep không ra) `[CODE]`.

### Đề xuất

- **C1 Server**: component `EnemyHitReaction` lắng nghe `OnDamageApplied`, phân loại theo vùng + lượng damage + hướng bắn, replicate một sự kiện gọn (vùng, hướng, mức) qua kênh action có sẵn (`EnemyReplicatedState`).
- **C2 Phản ứng theo vùng**:
  - Đầu: giật đầu ra sau, khựng ngắn. Headshot chết dùng hướng ngã riêng.
  - Ngực/thân: gập người theo hướng đạn; damage lớn (shotgun, sniper) gây khựng/lùi.
  - Tay: vai/tay giật, huỷ đòn đánh đang vung nếu vượt ngưỡng.
  - Chân: chậm lại tạm thời; cộng dồn đủ thì chuyển sang bò (dùng clip `zombie crawl` có sẵn).
- **C3 Trình diễn**: vì không có clip hit reaction, dùng flinch thủ tục (xoay cộng thêm trên xương spine/neck/shoulder trong `LateUpdate`, tắt dần). Không cần asset mới. Nếu bạn cấp clip hit reaction thì thêm layer additive sau.
- **C4 Giới hạn**: cooldown mỗi con và ngưỡng damage để súng liên thanh không khoá cứng quái. Tank giữ nguyên cơ chế stagger riêng.
- Phần gameplay (chậm, bò, huỷ đòn) thay đổi cân bằng nên cần bạn duyệt số liệu. VFX máu cần asset, hiện chưa có.

---

## 5. Animation và cơ chế Death / Gục

### Kẻ địch

- Khi chết: trigger `Die`, tắt NavMeshAgent, phát `deathSound`, sau `destroyDelay = 3s` trả về pool `[CODE EnemyAI.cs:622-650, EnemyHealth.cs:211-236]`.
- `HandleDeathVisual` chỉ tắt collider gốc, phần "animation die, ragdoll" là comment placeholder `[CODE EnemyHealth.cs:127-135]`.
- Chỉ có 1 clip chết cho mọi tình huống; không phân biệt hướng đạn, headshot, nổ. Xác biến mất tức thì sau 3 giây (pop-out). Không có ragdoll.

### Người chơi

- Logic server đã đầy đủ: Alive → Downed (khi còn đồng đội) với `bleedoutSeconds = 30`, trúng đòn khi gục rút ngắn thời gian, hồi sinh bằng giữ nút, hết giờ thì Dead → Spectating `[CODE PlayerHealth.cs:260-318, CampaignMissionController.cs:693-696, CampaignSettings.cs:24-25]`.
- UI có chữ "DOWNED / BLEED OUT IN…" `[CODE CampaignStatusUI.cs:106-111]`.
- **Thiếu toàn bộ phần trình diễn**: Animator 3P chỉ có Locomotion/Jump/Fall/Land `[SCENE animator_list_states ThirdPersonCharacter]`, không có state gục/bò/chết/được cứu. `PlayerVisibilityController` và `PlayerMovement` không có nhánh hiển thị nào cho Downed ngoài việc chặn di chuyển `[CODE PlayerMovement.cs:208]`. Đồng đội nhìn người bị gục vẫn thấy đứng thẳng `[INFERENCE]`.
- Góc nhìn thứ nhất khi gục: không hạ camera, không hiệu ứng `[INFERENCE]`.
- Dự án không có clip gục/chết/hồi sinh nào cho nhân vật người chơi.

### Đề xuất

- **D1 Quái**: chọn kiểu chết theo `lastDamageInfo` (headshot, nổ, hướng trước/sau); dùng thêm `zombie dying` có sẵn làm biến thể. Xác chìm/mờ dần thay vì biến mất tức thì, và chỉ dọn khi không ai nhìn (cùng quy tắc A3). Ragdoll là lựa chọn thêm, cần dựng collider/joint cho từng rig.
- **D2 Người chơi 3P**: thêm state Downed (ngã → nằm/bò), Revive (được đỡ dậy), Dead. Điều khiển bằng `LifeState` đã replicate. **Cần clip mới** (không có trong dự án).
- **D3 Người chơi 1P**: hạ camera xuống tầm nằm, giới hạn góc nhìn, cất vũ khí; khi chết chuyển mượt sang spectator.
- **D4**: Thống nhất luật gục. Hiện tại chơi một mình là chết thẳng `[CODE PlayerHealth.cs:292-297]`; cần bạn xác nhận giữ hay đổi.
- Không đụng save/checkpoint. Checkpoint hiện chuyển Downed thành Dead khi lưu `[CODE CampaignCheckpoint.cs:45]`, giữ nguyên.

---

## 6. Thứ tự thực thi đề xuất

1. **Ánh sáng Factory (E1 + E2)**. Là bug, không cần asset ngoài, độc lập với phần còn lại.
2. **Nhu yếu phẩm (A3 + A4 trước, rồi A1, A2)**. Sửa pop-in trước, sau đó phân bổ.
3. **Hit reaction (C1–C4)**. Không cần asset mới nhờ flinch thủ tục.
4. **Death/Gục (D1, D3 trước; D2 khi có clip)**.
5. **Âm thanh (B1–B3 trước; B4 khi có asset)**. Khối lượng lớn nhất và phụ thuộc asset nhiều nhất.

Mỗi hạng mục sẽ có: Editor/PlayMode test kèm log thật, kiểm tra host + client, và báo cáo riêng trong `Reports/`.

## 7. Quyết định cần bạn đưa ra

1. Duyệt flow của từng hạng mục (1–5) và thứ tự ở mục 6.
2. **Nguồn asset âm thanh và animation**: bạn tự cấp, hay cho phép tôi tải từ nguồn CC0/miễn phí có ghi license (ví dụ Kenney, Freesound CC0, Mixamo cho clip gục/chết/hit reaction)?
3. Ánh sáng: chỉ đèn nội thất + probe (E1/E2), hay muốn thêm đèn pin (E3)?
4. Hit reaction: có cho phép thay đổi gameplay (bắn chân làm chậm/bò, bắn tay huỷ đòn) hay chỉ làm phần hình ảnh?
5. Luật gục khi chơi một mình: giữ "chết thẳng" hay cho gục có tự hồi?

## 8. Chưa kiểm chứng

- Chưa playtest bất kỳ hạng mục nào; các dòng `[INFERENCE]` cần xác nhận khi chạy thật.
- Chưa kiểm tra slot audio nào trên prefab quái đã được gán clip.
- Chưa kiểm tra quality level của bản build và cờ static/lightmap của từng hangar.
- Chưa kiểm tra các hangar nào thực sự đi vào được trong tuyến chơi.
