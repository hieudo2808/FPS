# FPS Zombie Survival — Kiến trúc Project, AI Director và Ý tưởng Special Infected

> Tài liệu tổng hợp toàn bộ hướng trao đổi về project FPS Zombie Survival, kiến trúc codebase, AI Director, hệ thống zombie, lựa chọn Special Infected và concept **Infector / Parasite**.
>
> Mục tiêu: dùng như một **design + technical reference** để tiếp tục phát triển đồ án mà không mất bối cảnh.

---

# 1. Tổng quan đồ án

Project đang đi theo hướng:

- FPS zombie survival.
- Multiplayer.
- Server-authoritative.
- Zombie horde.
- Adaptive AI / AI Director.
- Dynamic Difficulty Adjustment.
- Player Profiling.
- Team Analysis.
- Special Infected.
- NavMesh.
- Object Pooling.
- Attack Slot.
- Mission / extraction flow.
- Combat telemetry.

Điểm mạnh của đồ án không nên nằm ở việc có thật nhiều loại zombie. Điểm nên tập trung để tạo giá trị kỹ thuật là:

> **Game quan sát trạng thái và hành vi của người chơi → AI Director điều chỉnh encounter → zombie/special infected thay đổi áp lực → game thu telemetry mới → Director tiếp tục thích nghi.**

Đây là một **closed feedback loop**.

---

# 2. Mental model của toàn project

Có thể hiểu project qua sơ đồ:

```text
                    NetworkGameManager
                          │
              authoritative multiplayer
                          │
        ┌─────────────────┼─────────────────┐
        │                 │                 │
     PLAYER            MISSION             AI
        │                 │                 │
     combat            objectives        Director
        │                                   │
        │                               spawning
        │                                   │
        └──────────────────────────────► zombies
                                            │
                                         EnemyAI
                                            │
                                         combat
                                            │
                                            ▼
                                          PLAYER
                                            │
                                       telemetry
                                            │
                                            └────► Director
```

Nói ngắn gọn:

- `NetworkGameManager`: nền networking / session.
- Player: movement, health, weapon, interaction.
- Mission: objective và match progression.
- AI Director: điều phối nhịp độ encounter.
- EnemyAI: logic từng zombie.
- PlayerProfiler / TeamAnalyzer: đọc trạng thái player/team.
- Adaptive Difficulty: thay đổi độ khó theo performance.
- SpawnController: quyết định số lượng / tần suất spawn.
- AttackSlotManager: điều phối zombie quanh player.
- Telemetry: feedback trở lại Director.

---

# 3. Kiến trúc codebase

Code chính được chia theo feature:

```text
Assets/
└── FPS/
    ├── Core/
    │   └── Runtime/
    ├── Editor/
    ├── Features/
    │   ├── AI/
    │   ├── Audio/
    │   ├── Characters/
    │   ├── Input/
    │   ├── Interaction/
    │   ├── Missions/
    │   ├── Networking/
    │   ├── UI/
    │   ├── Weapons/
    │   └── World/
    ├── Scenes/
    └── Tests/
```

Đây là feature-based organization. Tuy nhiên phần lớn code vẫn nằm trong cùng một assembly `FPS.asmdef`, vì vậy modularity hiện chủ yếu là tổ chức source/phân trách nhiệm, chưa phải compile-time isolation hoàn toàn.

---

# 4. Networking là xương sống

Project được thiết kế theo hướng:

```text
CLIENT
   │
   │ input / request
   ▼
SERVER
   │
   ├── validate
   ├── simulate
   ├── modify authoritative state
   └── replicate
          │
          ▼
       CLIENTS
      presentation
```

Các state quan trọng như player health, enemy health, weapon state, ammo, reload, match state và mission state đều nên đi theo tư duy server-authoritative.

Điều này đặc biệt quan trọng khi thêm Special Infected. **Special ability không nên để client tự quyết định hit/status.** Server phải là nơi:

1. xác nhận ability;
2. xác nhận target;
3. apply damage / infection / state;
4. replicate kết quả cho client.

---

# 5. Combat flow

Một phát bắn có thể hình dung:

```text
Input
 │
 ▼
WeaponFireHandler
 │
 │ fire request
 ▼
SERVER VALIDATION
 │
 ├── fire rate
 ├── ammo
 ├── weapon state
 └── request validity
 │
 ▼
Lag Compensation
 │
 ▼
Raycast / Hit Validation
 │
 ▼
EnemyHealth
 │
 ├── damage
 ├── death
 └── telemetry
```

Dữ liệu combat có thể quay lại hệ Adaptive Difficulty, ví dụ:

- shots fired;
- shots hit;
- headshots;
- kills;
- damage taken;
- downs;
- ammo efficiency.

---

# 6. Mission và AI

Mission không nên bị xem là một subsystem tách rời hoàn toàn khỏi AI Director. Director có thể dựa vào mission state để thay đổi spawn.

Ví dụ:

```text
Normal encounter
    ↓
Spawn thường

Extraction
    ↓
Crescendo

Finale
    ↓
Special rules / special spawn anchors
```

Điều này cho phép encounter phù hợp với nhịp độ mission.

---

# 7. Hai tầng AI

AI trong project nên được hiểu thành hai tầng.

## 7.1 Macro AI

```text
"Trận đấu nên căng đến đâu?"
```

Do AI Director xử lý. Nó quan tâm tới:

- pacing;
- intensity;
- dynamic difficulty;
- spawn rate;
- special infected;
- team health;
- team separation;
- mission state.

## 7.2 Micro AI

```text
"Con zombie này nên làm gì?"
```

Do EnemyAI / Special Infected AI xử lý. Nó quan tâm tới:

- target selection;
- chase;
- pathfinding;
- attack;
- retreat;
- ability;
- attack slots.

---

# 8. AI Director

Pipeline tổng thể:

```text
                    GAME STATE
                        │
          ┌─────────────┼──────────────┐
          │             │              │
          ▼             ▼              ▼
  PlayerProfiler   TeamAnalyzer   ServerTelemetry
          │             │              │
          │             │              ▼
          │             │      AdaptiveMetrics
          └─────────────┼──────────────┘
                        ▼
                    AIDirector
                        │
         ┌──────────────┴──────────────┐
         ▼                             ▼
 DirectorStateMachine        DynamicDifficulty
         │                             │
         └──────────────┬──────────────┘
                        ▼
                 SpawnController
                        │
            ┌───────────┴───────────┐
            ▼                       ▼
    Spawn Frequency              Spawn Cap
    Special Chance               etc.
            │
            ▼
       Spawn Placement
            │
      ┌─────┴─────────────┐
      ▼                   ▼
DirectorSpawnService   InfluenceMap
      │                   │
      └─────────┬─────────┘
                ▼
          ZombieFactory
                │
          ZombiePoolManager
                │
                ▼
             EnemyAI
```

---

# 9. Director phases

Hệ adaptive mới nên dùng 5 phase:

```text
Calm
  ↓
BuildUp
  ↓
Combat
  ↓
Peak
  ↓
Relax
  └──────► Calm
```

## Calm

- gần như không gây áp lực;
- player có thời gian di chuyển / loot;
- chuẩn bị cho encounter tiếp theo.

## BuildUp

- bắt đầu tăng spawn;
- báo hiệu encounter đang tới;
- tension tăng dần.

## Combat

- encounter chính;
- common infected xuất hiện đều;
- special bắt đầu phù hợp.

## Peak

- mức áp lực cao nhất;
- có thể cho Tank / special mạnh;
- spawn multiplier cao.

## Relax

- dừng hoặc giảm spawn;
- team hồi phục;
- Director đánh giá performance;
- Dynamic Difficulty có thể cập nhật tại đây.

---

# 10. Spawn multiplier theo phase

Có thể dùng logic kiểu:

```text
Calm      ≈ 0.05
BuildUp   ≈ 0.50
Combat    ≈ 1.00
Peak      ≈ 1.50
Relax     = 0
```

Spawn rate cuối cùng có thể là:

```text
Director pacing
       ×
Static difficulty
       ×
Adaptive difficulty
       ×
Player count scaling
```

Từ đó tính spawn interval, max alive, special chance, max concurrent attackers.

---

# 11. Static Difficulty và Dynamic Difficulty

Hai hệ này nên tách biệt.

## Static difficulty

Ví dụ:

```text
Easy
Medium
Hard
Pandemonium
```

Static tier có thể ảnh hưởng:

- Zombie HP.
- Damage.
- Speed.
- Spawn interval.
- Max alive.
- Special chance.
- Max concurrent attackers.
- Rubber banding.

## Dynamic Difficulty

Adaptive Difficulty là lớp bổ sung. Mục tiêu: không thay đổi difficulty quá phản ứng từng giây.

```text
Encounter
   │
   │ collect telemetry
   ▼
Relax
   │
   ▼
Evaluate performance
   │
   ▼
small difficulty adjustment
   │
   ▼
Next encounter
```

Đây là cách tốt hơn việc player vừa headshot vài con thì game lập tức tăng mạnh zombie.

---

# 12. PlayerProfiler và TeamAnalyzer

## Adaptive Metrics

Trả lời:

> Team đang chơi tốt tới đâu?

Có thể sử dụng:

- accuracy;
- headshot rate;
- damage taken;
- downs;
- kill rate;
- ammo efficiency.

## PlayerProfiler

Trả lời:

> Player hiện đang ở trạng thái nào?

Ví dụ:

- low health;
- low ammo;
- reloading;
- isolated;
- camping;
- movement;
- look direction;
- team distance.

## TeamAnalyzer

Có thể xác định:

- grouped;
- split;
- solo;
- carry;
- frontline;
- lone wolf.

Nhờ đó Director có thể tạo situational adaptation:

```text
player camp
→ spawn anti-camping special

team grouped
→ spawn area denial special

player isolated
→ spawn hunter/infector type
```

---

# 13. Spawn placement

Có thể tồn tại hai hướng placement.

## DirectorSpawnService

Authored spawn anchors, phù hợp cho:

- special spawn;
- mission-specific spawn;
- crescendo;
- extraction;
- finale.

Có thể lọc theo:

- anchor type;
- distance;
- visibility;
- mission state;
- NavMesh validity.

## InfluenceMap

Dynamic spawn selection với constraint:

- không quá gần player;
- không quá xa;
- tránh spawn ngay trong FOV;
- cần NavMesh hợp lệ;
- chọn vùng tạo pressure nhưng công bằng.

```text
                     PLAYER
                        ↑
                   camera/FOV
                ╱       │       ╲
             BAD       BAD      BAD
             SPAWN     SPAWN    SPAWN


           ✓                         ✓
        candidate                 candidate

                 phía sau / khuất
                      ✓
                  good spawn
```

---

# 14. EnemyAI cơ bản

Common zombie có thể dùng FSM:

```text
              ┌─────────────┐
              │    IDLE     │
              └──────┬──────┘
                     │ detect target
                     ▼
              ┌─────────────┐
        ┌────►│    CHASE    │◄─────────┐
        │     └──────┬──────┘          │
        │            │ in range        │
        │            ▼                 │
        │     ┌─────────────┐           │
        └─────│   ATTACK    │───────────┘
              └─────────────┘

                     │ hp <= 0
                     ▼

              ┌─────────────┐
              │    DEAD     │
              └─────────────┘
```

AI server-side:

```text
SERVER
 EnemyAI
    │
    ├── think
    ├── pathfind
    ├── attack
    └── authoritative state
             │
             ▼
Replicated presentation state
             │
             ▼
CLIENT
 animation / sound / visual
```

---

# 15. Target selection

Không nên chỉ dùng nearest player. Có thể dùng utility scoring:

```text
Closer player                    +
Low health                       +
Isolated                         +
Reloading                        +
Low ammo                         +
Carry role                       +
Lone wolf                        +
Current target bias              +

Too many enemies already target  -
```

Điều này giúp zombie chọn target hợp lý hơn và hạn chế cả horde focus một người.

---

# 16. AttackSlotManager

Mục tiêu:

> Không để toàn bộ horde cùng đứng đúng một vị trí và cùng đánh player.

Thay vì:

```text
       Z Z Z
        ZZZ
         Z
         P
```

phân slot quanh player:

```text
                   Z
                   │
             Z     │     Z
               \   │   /
                \  │  /
          Z ----- PLAYER ----- Z
                /  │  \
               /   │   \
             Z     │     Z
                   │
                   Z
```

Zombie có thể được chia vai trò:

- Attacker.
- Flanker.
- Pressure.
- Reserve.

Difficulty có thể thay số concurrent attackers:

```text
Easy          2
Medium        3
Hard          4
Pandemonium   6
```

Nhờ vậy horde vẫn trông đông nhưng player không bị 20 zombie cùng damage một lúc.

---

# 17. Behavior Tree

Không nên xem Behavior Tree là core AI bắt buộc. Hướng hợp lý:

```text
Common zombie
    → custom FSM / utility

Special infected
    → dedicated SI_* runtime behavior

Behavior Tree
    → legacy / optional authoring
```

Không cần ép toàn bộ enemy quay lại Behavior Tree chỉ vì project có dependency cũ.

---

# 18. Feedback loop hoàn chỉnh

```text
 ┌──────────────────────────────────────────────────────┐
 │                      PLAYERS                         │
 │                                                      │
 │ health / ammo / movement / formation / shooting     │
 └───────────────┬───────────────────────────┬──────────┘
                 │                           │
                 ▼                           ▼
        PlayerProfiler              ServerTelemetry
        TeamAnalyzer                       │
                 │                          ▼
                 │                 AdaptiveMetrics
                 └──────────────┬───────────┘
                                ▼
                         ┌────────────┐
                         │ AI DIRECTOR│
                         └─────┬──────┘
                               │
                     phase + difficulty
                               │
                               ▼
                       SpawnController
                               │
               ┌───────────────┴────────────────┐
               ▼                                ▼
        spawn quantity                    special gate
        spawn interval                    max alive
               │
               └───────────────┬────────────────┘
                               ▼
                       Spawn positioning
                               │
                      ZombieFactory/Pool
                               │
                               ▼
                         ┌──────────┐
                         │ EnemyAI  │
                         └────┬─────┘
                              │
            ┌─────────────────┼──────────────────┐
            ▼                 ▼                  ▼
      choose target       Attack Slots         NavMesh
            │                 │                  │
            └─────────────────┼──────────────────┘
                              ▼
                           attack
                              │
                              ▼
                      Player takes damage
                              │
                              │
               feedback ──────┘
```

Đây là phần quan trọng nhất khi thuyết trình đồ án.

---

# 19. Hướng Special Infected

Không nên làm quá nhiều special cùng lúc. Scope hợp lý:

```text
1. Screamer   → đã có, cần polish
2. Tank       → special heavy
3. Infector   → special mới, tạo điểm khác biệt
4. Stalker    → optional
5. Spitter    → optional
6. Charger    → optional
```

Nếu thời gian hạn chế:

> **Screamer + Tank + Infector** là bộ đủ mạnh để trình bày đồ án.

---

# 20. Mapping các model đã xem

## Model #1

Đặc điểm:

- thân gầy;
- tay dài;
- dị dạng;
- silhouette nổi bật.

Phù hợp:

- Screamer;
- caster-like special;
- fragile special.

Nhưng project hiện đã có Screamer.

## Model #5

Đặc điểm:

- thân rất to;
- tay cực lớn;
- silhouette đọc ngay được là brute.

Phù hợp:

- Tank;
- Brute;
- heavy infected.

Đây là lựa chọn tốt nhất cho Tank.

## Model #8

Đặc điểm:

- nhiều appendage;
- hình thể dài;
- rất khác human zombie;
- có cảm giác parasite / mutant.

Phù hợp:

- Infector;
- Parasite;
- Stalker;
- ambusher.

**Đây là model được ưu tiên cho Infector.**

---

# 21. Screamer

Screamer đã có.

Role:

> **Horde controller / priority target**

Gameplay:

```text
Detect player
      ↓
Position
      ↓
Scream windup
      ↓
Player có cơ hội interrupt
      ↓
Nếu scream thành công
      ↓
Spawn / reinforce horde
```

Điểm mạnh:

- tạo priority target;
- tương tác trực tiếp với Director;
- dễ nghe / dễ nhận biết;
- dễ showcase.

Screamer đại diện cho **threat bên ngoài player**.

---

# 22. Tank

Tank không nên chỉ là:

```text
HP × 10
Damage × 5
```

Nên có ít nhất ba đặc điểm.

## Heavy Swing

```text
windup
  ↓
heavy hit
  ↓
damage + knockback
```

## Slam

```text
Tank
 ↓
AoE Slam
 ↓
nearby players knockback
```

## Stagger

Ví dụ:

```text
nhiều damage trong thời gian ngắn
↓
Tank stagger 1–1.5s
```

Tạo teamwork: **dồn damage để stagger Tank**.

---

# 23. Special Infected mới: INFECTOR / PARASITE

Đây là special phù hợp nhất với yêu cầu:

> **Có khả năng gây trạng thái lây nhiễm bên trong player.**

Tên có thể dùng:

- Infector.
- Parasite.
- Carrier.
- Seeder.
- Injector.
- Brood.
- Leech.

Tên dễ hiểu nhất: **Infector**.

---

# 24. Fantasy của Infector

Infector không phải tank và không nên quá nhiều HP.

Mục tiêu:

```text
Find target
   ↓
Approach / Stalk
   ↓
Implant attack
   ↓
Player infected
   ↓
Retreat
```

Điểm quan trọng:

> Sau khi ability thành công, **mối đe dọa chính chuyển từ con quái sang trạng thái bên trong player**.

Đây là thứ khiến Infector khác Screamer và Tank.

---

# 25. Model cho Infector

Ưu tiên: **Model #8**.

Lý do:

- hình thể parasite-like;
- appendage phù hợp animation cấy nhiễm;
- silhouette khác common zombie;
- không trông giống bruiser;
- dễ nhận ra trong horde.

Có thể thiết kế một appendage làm:

- stinger;
- parasite injector;
- spine;
- tendril.

---

# 26. Core ability: Implant

Ability nên là melee, không nên làm projectile ở version đầu.

```text
           INFECTOR
              │
         windup ~0.6s
              │
              ▼
      ─── tentacle stab ───► PLAYER
                                  │
                                  ▼
                             + Infection
```

Nếu muốn animation mạnh hơn:

```text
stab
 ↓
hold target ~0.8s
 ↓
implant parasite
 ↓
push/release
 ↓
Infector retreat
```

Không nên giữ player 4–5 giây vì mất quyền điều khiển, multiplayer khó đồng bộ và dễ gây frustration.

---

# 27. Vì sao không nên dùng projectile cho Infector trước

Projectile tạo thêm workload:

```text
network projectile
+
collision
+
pooling
+
latency
+
hit validation
+
VFX
+
lifetime
+
replication
```

Trong khi melee implant:

- dễ validate server-side;
- animation dễ đọc;
- gameplay rõ;
- ít bug networking hơn.

---

# 28. Infection System

Tạo riêng:

```text
Player
├── PlayerHealth
├── PlayerMovement
├── PlayerProfiler
└── PlayerInfectionController
```

Không nên nhét infection trực tiếp vào `PlayerHealth`.

Lý do:

- health là damage/life subsystem;
- infection là status gameplay subsystem;
- sau này có thể thêm nhiều type status;
- dễ unit test;
- dễ replicate riêng.

---

# 29. Infection state

Có thể dùng:

```text
Infection 0 → 100
```

với các stage:

```text
0–30     Incubation
30–70    Symptomatic
70–99    Critical
100      Sepsis / Active Parasite
```

---

# 30. Stage 1 — Incubation

Mức:

```text
0–30
```

Hiệu ứng nhẹ, không nên damage.

Dấu hiệu:

- heartbeat;
- cough nhẹ;
- screen pulse;
- HUD infection icon;
- vignette nhẹ.

Mục đích: player và teammate biết chuyện gì đang xảy ra.

---

# 31. Stage 2 — Symptomatic

Mức:

```text
30–70
```

Gameplay penalty nhỏ.

Ví dụ:

```text
stamina recovery    -15%
reload speed        -10%
weapon sway         +10%
```

Nếu game không có stamina:

```text
movement speed      -5%
accuracy recovery   chậm hơn
```

Không nên debuff quá mạnh.

---

# 32. Stage 3 — Critical

Mức:

```text
70–100
```

Bắt đầu tạo pressure từ bên trong.

Ý tưởng tốt nhất:

> **Cough / infection pulse tạo noise và thu hút zombie.**

```text
infected player
      │
      ▼
    COUGH
      │
      ├── noise event
      │
      └── zombie biết vị trí
```

Điều này kết nối trực tiếp với AI system:

```text
Infection
   ↓
PlayerProfiler / Threat
   ↓
Enemy utility
   ↓
Zombie pressure
```

---

# 33. Infection không nên chỉ là Poison

Tránh thiết kế:

```text
infected
↓
-5 HP/sec
```

Đó chỉ là DOT đổi tên.

Infection nên thay đổi:

- decision making;
- team spacing;
- enemy aggro;
- resource usage;
- encounter pressure.

---

# 34. Contagion — lây sang teammate

Nếu muốn infection thật sự "lây nhiễm nội bộ player/team", có thể cho Stage Critical truyền infection.

Ví dụ:

```text
Player A = Critical

        radius 2.5m
   ┌─────────────────┐
   │                 │

Player B           Player C
```

Rule:

```text
distance < 2.5m
+
continuous exposure > 5 sec
      ↓
+15 infection
```

Không nên chỉ chạm 0.1 giây là bị nhiễm, vì sẽ gây frustration.

---

# 35. Gameplay tension từ contagion

Bình thường co-op khuyến khích team đứng gần nhau. Infection lại khuyến khích team giãn nhẹ. Trong khi zombie horde ép team tụ lại để cover nhau.

Ta tạo conflict:

```text
Common Horde
→ ép team giữ formation

Infector / Infection
→ ép team không đứng quá sát
```

Đây là emergent gameplay tốt.

---

# 36. Cách chữa Infection

Player phải có counterplay. Có thể dùng:

- Antidote.
- Injector.
- Medical Kit.
- Serum.

Hướng tốt nhất là teammate treatment:

```text
Hold E – Treat Infection
████████░░ 3 sec
```

Sau đó:

```text
infection -= 60
```

hoặc cure hoàn toàn.

Điều này tạo co-op interaction.

---

# 37. Self-treatment vs teammate treatment

Không nên instant cure.

Có thể dùng:

```text
Self-treatment      = 7 sec
Teammate treatment  = 3 sec
```

Như vậy teammate hỗ trợ hiệu quả hơn nhưng player solo vẫn có counterplay.

---

# 38. Infection 100 không nên instant kill

Không nên:

```text
infection = 100
→ dead
```

Thay vào đó:

```text
100 infection
     ↓
SEPSIS / ACTIVE PARASITE
     ↓
periodic HP damage
+
noise pulse
+
movement penalty
```

Ví dụ:

```text
5 HP mỗi 5 giây
```

Player vẫn có cơ hội cứu.

---

# 39. Infector target selection

Không nên nearest target thuần túy.

Có thể dùng utility:

```text
score =
    isolated          * 2.0
  + lowHealth         * 0.8
  + reloading         * 0.6
  + camping           * 0.4
  - alreadyInfected   * 3.0
  - nearbyTeam        * 0.8
```

Ý nghĩa:

- ưu tiên player dễ bị ambush;
- tránh spam cùng một người;
- tận dụng PlayerProfiler.

---

# 40. Infector AI FSM

Giữ state machine nhỏ:

```text
             ┌──────────┐
             │  SEARCH  │
             └────┬─────┘
                  │ target
                  ▼
             ┌──────────┐
             │  STALK   │
             └────┬─────┘
                  │ opening
                  ▼
             ┌──────────┐
             │ APPROACH │
             └────┬─────┘
                  │ range
                  ▼
             ┌──────────┐
             │ IMPLANT  │
             └────┬─────┘
                  │ success
                  ▼
             ┌──────────┐
             │ RETREAT  │
             └────┬─────┘
                  │ cooldown
                  └──────► SEARCH
```

Không nên cho nó quá nhiều skill. Một fantasy rõ tốt hơn.

---

# 41. Retreat là mechanic quan trọng

Sau khi implant thành công:

```text
Infector
   ↓
retreat
```

Lý do:

- ability đã tạo pressure;
- nếu nó tiếp tục đứng melee sẽ giống common zombie;
- player có cơ hội bắn trả;
- tạo hành vi đặc trưng.

Có thể retreat 4–6 giây.

---

# 42. Prototype stats cho Infector

Giá trị thử nghiệm ban đầu:

```text
HP                  180–250
Move speed          1.15 × common
Melee damage        10–15
Implant infection   +30
Implant cooldown    12–15 sec
Retreat duration    4–6 sec
Spawn cap           1
```

Đây chỉ là starting point, balance phải dựa vào playtest.

---

# 43. Spawn cap

Bản prototype nên:

```text
Max Infector alive = 1
```

Không nên cho 2–3 Infector xuất hiện sớm.

---

# 44. Director spawn condition cho Infector

Ví dụ:

```text
No current Infector
AND
Team infection low
AND
Not Relax
AND
Special cooldown ready
AND
Encounter pressure allows
```

Không nên spawn nếu:

- cả team đang infection cao;
- nhiều người down;
- Tank đang gây áp lực quá mạnh;
- Director đang Relax.

---

# 45. Infection ảnh hưởng AI Director

Director nên biết:

```text
TeamInfectionLevel
```

Ví dụ:

```text
0 infected
→ normal

1 infected
→ normal pressure

2 infected
→ reduce common spawn slightly

3 infected
→ prefer Relax earlier
```

Lý do: Infection bản thân đã là một nguồn pressure. Nếu Director không tính đến nó, game có thể tạo death spiral.

---

# 46. Death Spiral cần tránh

Tình huống xấu:

```text
player low HP
+
critical infection
+
Tank alive
+
Screamer horde
+
Director Peak
+
Infector mới spawn
```

Đây có thể trở thành trạng thái không còn counterplay.

Adaptive Director phải nhìn cả:

- health;
- downs;
- infection;
- ammo;
- alive players;
- active specials.

---

# 47. Special Budget

Nên dùng budget thay vì random hoàn toàn.

Ví dụ:

```text
Screamer = 1 point
Infector = 2 points
Tank     = 3 points
```

Director budget:

```text
Combat = 2
Peak   = 4
```

Combination:

```text
Combat:
Infector(2)             ✓
Screamer(1)             ✓

Peak:
Tank(3) + Screamer(1)   ✓
Infector(2) + Screamer  ✓
Tank(3) + Infector(2)   ✗ nếu budget 4
```

Cách này dễ tune hơn random probability độc lập.

---

# 48. Special role matrix

| Special | Role | Pressure type | Counter |
|---|---|---|---|
| Screamer | Horde controller | External / quantity | Kill before scream |
| Tank | Heavy pressure | HP / space / knockback | Team focus / dodge |
| Infector | Internal status | Infection / spacing | Treat / kill / avoid implant |
| Stalker | Ambush | Isolation / camping | Awareness / stay together |
| Spitter | Area denial | Positioning | Spread / reposition |
| Charger | Displacement | Formation break | Dodge / focus |

---

# 49. Screamer + Infector synergy

Hai con không trùng role.

```text
SCREAMER
Threat bên ngoài
      ↓
thêm horde

INFECTOR
Threat bên trong
      ↓
status / contagion
```

Screamer khiến team: **"Giết nó trước khi hét."**

Infector khiến team: **"Ai bị nhiễm? Giữ khoảng cách. Có antidote không?"**

Đây là cặp special có gameplay identity rõ.

---

# 50. Tank + Infector synergy

Không nên spawn cùng lúc quá thường xuyên.

Tank ép team:

```text
focus fire
+
kite
+
reposition
```

Infector ép:

```text
watch flank
+
avoid implant
+
manage infection
```

Kết hợp chỉ nên dùng ở late Peak, higher difficulty, boss encounter hoặc scripted moment.

---

# 51. Screamer + Tank + Infector là bộ special tốt cho đồ án

Nếu chỉ chọn ba con:

## Screamer

Showcase:

- Director.
- Horde spawning.
- Interruptible ability.

## Tank

Showcase:

- heavy enemy AI;
- target priority;
- stagger;
- spatial pressure.

## Infector

Showcase:

- PlayerProfiler.
- Status subsystem.
- Adaptive Difficulty.
- team state;
- anti-death-spiral logic;
- emergent co-op.

Bộ này đủ đa dạng mà vẫn có scope hợp lý.

---

# 52. Model / art direction

Special cần silhouette đọc nhanh. Player phải nhận ra trong khoảng 1 giây.

## Common infected

Nên gần human silhouette.

## Screamer

Gầy / dị dạng / có phần cơ thể liên quan scream.

## Tank

Vai và tay lớn.

## Infector

Appendage / tendril / stinger.

Điều này giúp người chơi phân biệt mà không cần UI marker.

---

# 53. Animation tối thiểu cho Infector

Không cần animation pack quá lớn.

Minimum viable:

```text
Idle
Walk
Run
Alert
Implant Windup
Implant Hit
Implant Miss
Retreat
Hit React
Death
```

Optional:

```text
Crawl
Taunt
Spawn
Roar
Climb
```

Không cần làm optional trước khi gameplay core ổn.

---

# 54. Sound design cho Infector

Audio cue rất quan trọng.

## Ambient

- wet clicking;
- parasite chirp;
- breathing;
- tendril movement.

## Windup

Một sound đặc trưng trước Implant để player nghe được Infector sắp lao vào.

## Successful Implant

Cần sound rất rõ:

- stab;
- parasite injection;
- heartbeat transition.

## Infection stages

Stage càng cao:

- heartbeat rõ hơn;
- cough nhiều hơn;
- parasite pulse mạnh hơn.

---

# 55. VFX / UI

Nên nhẹ.

## On implant

- short screen pulse;
- small blood/parasite effect;
- infection icon.

## HUD

```text
INFECTION
██████░░░░ 60%
```

Có thể thêm stage icon:

```text
Incubating
Symptomatic
Critical
```

Không nên che màn hình quá nhiều.

---

# 56. Network architecture cho Infection

Đề xuất:

```text
SI_Infector
    │
    │ successful implant
    ▼
SERVER
    │
PlayerInfectionController.AddInfection()
    │
    ├── calculate stage
    ├── apply gameplay state
    └── replicate
             │
             ▼
CLIENT
    ├── HUD
    ├── VFX
    ├── audio
    └── presentation
```

Server là nguồn sự thật.

---

# 57. Không replicate infection mỗi frame

Không cần gửi:

```text
42.01
42.02
42.03
...
```

Có thể server tick 1–4 lần/giây và client interpolate UI. Điều này giảm network spam.

---

# 58. Suggested classes

Có thể tổ chức:

```text
Features/
└── Characters/
    ├── Player/
    │   ├── PlayerHealth.cs
    │   ├── PlayerMovement.cs
    │   └── PlayerInfectionController.cs
    │
    └── Enemies/
        └── Runtime/
            └── SpecialInfected/
                ├── SpecialInfectedBase.cs
                ├── SI_Screamer.cs
                ├── SI_Tank.cs
                └── SI_Infector.cs
```

Optional:

```text
Features/
└── AI/
    └── Runtime/
        ├── InfectionThreatEvaluator.cs
        └── SpecialSpawnBudget.cs
```

---

# 59. Suggested Infection API

Conceptual:

```csharp
public enum InfectionStage
{
    None,
    Incubation,
    Symptomatic,
    Critical,
    Sepsis
}
```

Controller có thể expose:

```csharp
AddInfection(float amount)
ReduceInfection(float amount)
Cure()
GetStage()
IsContagious()
```

Không nhất thiết exact implementation như trên, nhưng separation này tốt.

---

# 60. Suggested Infector states

Conceptual enum:

```csharp
public enum InfectorState
{
    Search,
    Stalk,
    Approach,
    Implant,
    Retreat,
    Cooldown,
    Dead
}
```

Có thể tái dùng base EnemyAI cho navigation, target references và animation replication rồi chỉ override special behavior.

---

# 61. Suggested target score

Prototype:

```text
TargetScore =
    DistanceScore
  + IsolationScore       × 2.0
  + LowHealthScore       × 0.8
  + ReloadingScore       × 0.6
  + CampingScore         × 0.4
  - AlreadyInfected      × 3.0
  - NearbyTeammates      × 0.8
```

Sau playtest mới tune.

---

# 62. Suggested infection progression

Ví dụ đơn giản:

```text
Implant hit = +30 infection

Passive progression:
+2 / sec while parasite active

Treatment:
-60

Natural decay:
0 hoặc rất thấp
```

Một alternative tốt hơn là không auto progression quá nhanh. Có thể để infection tăng chủ yếu do implant, contagion và repeated exposure. Như vậy player dễ hiểu nguyên nhân hơn.

---

# 63. Option khác: Parasite Stack

Thay vì bar 0–100:

```text
Parasite Stack 0–3
```

Ví dụ:

```text
1 stack → incubation
2 stack → symptomatic
3 stack → critical
```

Ưu:

- dễ network;
- dễ UI;
- dễ hiểu;
- dễ balance.

Nhược:

- ít granularity hơn.

Đây là option đáng cân nhắc nếu scope đồ án cần nhỏ.

---

# 64. Nên dùng Infection 0–100 hay Stack?

Nếu muốn showcase kỹ thuật: **0–100**.

Nếu muốn triển khai nhanh và ổn định hơn: **3-stack**.

Cho đồ án có nhiều subsystem khác, 3-stack thực tế có thể an toàn hơn.

---

# 65. MVP cho Infector

Version 1 chỉ cần:

```text
1. Spawn
2. Choose target
3. Approach
4. Implant
5. Add Infection
6. Retreat
7. Infection HUD
8. Treatment
```

Chưa cần:

- contagion;
- advanced VFX;
- coughing aggro;
- complex stage debuffs.

---

# 66. Version 2

Sau khi MVP chạy ổn:

```text
9. Infection stages
10. Cough noise
11. Zombie aggro modifier
12. Director TeamInfectionLevel
13. Special budget
```

---

# 67. Version 3

Optional polish:

```text
14. Contagion
15. Advanced audio
16. Stronger VFX
17. Special animation transitions
18. Director combo rules
19. Difficulty-specific infection behavior
```

---

# 68. Implementation priority

```text
PRIORITY 1
──────────
Infector basic AI
Implant
Server-authoritative infection

PRIORITY 2
──────────
HUD
Treatment
Retreat behavior

PRIORITY 3
──────────
Infection stages
Cough / noise
Director awareness

PRIORITY 4
──────────
Contagion
Advanced VFX / sound
```

---

# 69. Những thứ không nên scope creep

Không nên làm sớm:

- wall climbing;
- full IK tentacle;
- infection procedural body mutation;
- projectile parasite;
- complex pin system;
- physics ragdoll grab;
- possession;
- player PvP conversion;
- infection hallucination AI;
- multiple parasite types.

Đây đều là nice-to-have.

---

# 70. Giá trị đồ án của Infector

Infector kết nối nhiều subsystem:

```text
Special AI
      ↓
Player status
      ↓
Profiler
      ↓
Team state
      ↓
Director
      ↓
Dynamic Difficulty
      ↓
Spawn pressure
```

Nó chứng minh AI của project không chỉ là zombie đuổi theo player, mà là **AI phản ứng với trạng thái gameplay và thay đổi hành vi encounter theo context**.

---

# 71. Cách trình bày trước hội đồng

## Demo 1 — bình thường

```text
Team healthy
↓
Combat
↓
common horde
```

## Demo 2 — Screamer

```text
Screamer enters
↓
player không interrupt
↓
horde reinforcement
```

Giải thích: special này ảnh hưởng encounter-level pressure.

## Demo 3 — Infector

```text
Infector targets isolated player
↓
implant
↓
infection state starts
↓
HUD changes
↓
cough attracts zombie
```

Giải thích: special này gây internal persistent state.

## Demo 4 — Director adaptation

```text
Multiple infected / low health
↓
Director detects team pressure
↓
reduces spawn / enters Relax earlier
```

Đây là phần mạnh khi defense.

---

# 72. Một câu mô tả ngắn về AI của project

> **Hệ thống AI không chỉ điều khiển từng zombie mà còn theo dõi trạng thái người chơi và đội hình, từ đó AI Director điều chỉnh nhịp độ encounter, độ khó, số lượng zombie và loại Special Infected để duy trì áp lực phù hợp mà vẫn tránh tạo trạng thái không thể cứu vãn.**

---

# 73. Một câu mô tả ngắn về Infector

> **Infector là Special Infected chuyên cấy trạng thái nhiễm vào người chơi. Thay vì gây sát thương lớn ngay lập tức, nó tạo áp lực kéo dài thông qua debuff, noise, khả năng lây truyền và yêu cầu đồng đội hỗ trợ điều trị.**

---

# 74. Pillars của Special Infected

Mỗi special nên trả lời 4 câu hỏi:

## 1. Silhouette là gì?

Player nhận ra nó từ xa không?

## 2. Threat là gì?

Nó làm team sợ điều gì?

## 3. Counterplay là gì?

Player có cách phản ứng rõ không?

## 4. Director dùng nó khi nào?

Nó giải quyết loại hành vi player nào?

Ví dụ Screamer:

```text
Silhouette:
gầy / dị dạng

Threat:
horde reinforcement

Counter:
interrupt / focus fire

Director use:
tăng encounter pressure
```

Ví dụ Infector:

```text
Silhouette:
parasite appendages

Threat:
persistent infection

Counter:
avoid implant / treatment

Director use:
isolation / resource pressure
```

---

# 75. Quy tắc quan trọng khi thiết kế Special

Không làm special theo kiểu:

```text
common zombie
+
more HP
+
more damage
```

Special cần thay đổi decision của player:

```text
Screamer
→ đổi target priority

Tank
→ đổi positioning

Infector
→ đổi team spacing + resource management

Spitter
→ đổi area control

Stalker
→ đổi awareness / formation
```

---

# 76. Scope chốt đề xuất

```text
COMMON
──────
Walker
Runner
Heavy / cosmetic variations

SPECIAL
───────
Screamer
Tank
Infector
```

Nếu có thời gian: Stalker.

Sau đồ án: Spitter, Charger.

---

# 77. Thứ tự phát triển đề xuất từ hiện tại

## Step 1 — Polish Screamer

Checklist:

- scream animation;
- sound cue;
- interrupt timing;
- server spawn result;
- replicated action;
- HUD/feedback nếu cần.

## Step 2 — Làm `PlayerInfectionController`

Chưa cần Infector.

Test bằng debug button:

```text
Press key
→ AddInfection(30)
```

Đảm bảo:

- server authority;
- NetworkVariable;
- UI;
- stage;
- cure.

## Step 3 — Làm `SI_Infector`

Chỉ:

- select target;
- chase;
- implant;
- retreat.

## Step 4

Nối Infection → PlayerProfiler.

## Step 5

Nối Infection → AI Director.

## Step 6

Thêm cough/noise.

## Step 7

Thêm contagion nếu còn thời gian.

## Step 8

Làm Tank.

---

# 78. Vì sao nên làm InfectionController trước AI Infector

Nếu làm AI trước:

```text
Infector attack works
↓
nhưng status system chưa có
↓
khó test ability
```

Tốt hơn:

```text
Infection system standalone
↓
test được
↓
Infector chỉ gọi API
```

Dependency sẽ sạch hơn.

---

# 79. Suggested event flow

```text
Infector hits player
       │
       ▼
OnImplantSuccess
       │
       ▼
PlayerInfectionController.AddInfection()
       │
       ├── OnInfectionChanged
       ├── OnStageChanged
       └── OnBecameCritical
              │
              ├── UI
              ├── Audio
              ├── PlayerProfiler
              └── AI Director
```

Event-driven flow giúp giảm coupling.

---

# 80. Suggested separation of responsibility

## `SI_Infector`

Chịu trách nhiệm:

- target;
- navigation;
- ability timing;
- implant request.

Không chịu trách nhiệm:

- infection UI;
- infection progression;
- cure;
- Director difficulty.

## `PlayerInfectionController`

Chịu trách nhiệm:

- amount;
- stage;
- progression;
- cure;
- contagion.

## `PlayerProfiler`

Chỉ đọc infection state để đưa ra context.

## `AIDirector`

Chỉ đọc team-level infection metrics.

---

# 81. Potential future extension

Sau đồ án có thể thêm:

## Mutation at 100

Nếu infection full quá lâu:

```text
player temporarily attracts horde
```

Không nhất thiết biến thành zombie.

## Antidote station

Map có medical station tạo objective phụ.

## Infection resource choice

Dùng antidote cho người đang Critical hay giữ lại cho finale? Điều này tạo resource tension.

---

# 82. Những nguyên tắc balancing

## Infector

- dễ giết hơn Tank;
- ability nguy hiểm hơn melee;
- clear audio cue;
- implant cần windup;
- ability không spam;
- max alive thấp.

## Infection

- tiến triển đủ chậm để phản ứng;
- không instant-kill;
- treatment rõ;
- không làm player mất control quá lâu;
- critical pressure phải đáng sợ nhưng cứu được.

---

# 83. Kill priority

Ideal player priority:

```text
Screamer screaming
      ↓
priority #1

Infector approaching vulnerable target
      ↓
priority #2

Tank
      ↓
long-term threat
```

Nếu mọi special đều priority #1 thì combat trở nên hỗn loạn.

---

# 84. AI Director awareness

Director nên có một snapshot kiểu:

```text
TeamSnapshot
{
    alivePlayers
    weakestHealth
    averageAmmo
    separation
    downs
    activeSpecialBudget
    infectedCount
    criticalInfectedCount
    encounterIntensity
}
```

Từ đó ra quyết định.

---

# 85. Possible Director rules

Ví dụ:

```text
if criticalInfectedCount >= 2
    reduce special budget

if weakestHealth < 0.25
    prefer Relax

if teamGrouped && noInfector
    allow Infector

if teamSeparated
    Infector target utility rises

if teamUnderperforming
    lower dynamic multiplier
```

Rule-based vẫn đủ mạnh và dễ giải thích trong đồ án. Không cần machine learning.

---

# 86. Vì sao rule-based vẫn là Adaptive AI hợp lệ

AI adaptive có thể là:

```text
Telemetry
↓
Rules / scoring
↓
Difficulty / pacing decision
↓
Gameplay response
```

Ưu điểm:

- deterministic;
- debug được;
- test được;
- trình bày được;
- dễ balance.

---

# 87. Metric nên log khi playtest Infector

```text
Infector spawn count
Implant attempts
Implant hit rate
Average infection per player
Time to cure
Critical infection count
Deaths while infected
Team spread distance
Zombie kills caused by cough aggro
Infector kill time
```

Nhờ đó tune được thay vì balance theo cảm giác.

---

# 88. Dấu hiệu Infector quá mạnh

Nếu:

- hơn ~70% implant thành công;
- player gần như luôn lên Critical;
- antidote luôn bắt buộc;
- team chết ngay sau một implant;
- player cảm thấy không tránh được;

thì cần nerf.

Có thể:

- windup dài hơn;
- move speed thấp hơn;
- infection +20 thay +30;
- cooldown dài hơn;
- retreat lâu hơn.

---

# 89. Dấu hiệu Infector quá yếu

Nếu:

- player không quan tâm;
- implant gần như không trúng;
- infection không bao giờ vượt Stage 1;
- treatment không cần dùng;
- player luôn focus Tank/Screamer trước;

thì buff.

Có thể:

- tăng approach speed;
- tăng target intelligence;
- tăng infection amount;
- tăng cough pressure;
- giảm attack windup nhẹ.

---

# 90. Final recommendation

Từ toàn bộ hướng đã trao đổi, cấu hình phù hợp nhất hiện tại là:

```text
Core project identity
────────────────────
Server-authoritative FPS
Adaptive AI Director
Player profiling
Dynamic difficulty
Horde coordination

Special roster
──────────────
Screamer
Tank
Infector

Infector identity
─────────────────
Model #8
Fast / fragile
Implant melee
Persistent infection
Retreat after success
Team treatment
Optional contagion
Director-aware pressure
```

---

# 91. Priority cuối cùng

```text
1. Hoàn thiện core systems
2. Polish Screamer
3. Build InfectionController
4. Build Infector
5. Integrate Director
6. Build Tank
7. Playtest
8. Chỉ sau đó mới thêm special khác
```

Mục tiêu không phải:

> "Có nhiều quái nhất."

Mà là:

> **"Mỗi special chứng minh một phần khác nhau của hệ thống AI và tạo một loại decision khác nhau cho player."**

---

# 92. Tóm tắt một dòng

> **Screamer điều khiển horde, Tank điều khiển không gian, Infector điều khiển trạng thái nội bộ của người chơi; AI Director dùng dữ liệu từ player/team để quyết định khi nào nên đưa từng loại áp lực vào trận đấu.**
