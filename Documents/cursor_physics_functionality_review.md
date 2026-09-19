# Physics functionality review
_Exported on 8/5/2026 at 14:57:24 GMT+7 from Cursor (3.14.7)_

---

**User**

Kiểm tra chức năng liên quan đến vật lý như chạy, nhảy, movement, shooting feeling, bắn súng, recoil có đúng không, feeling đã tốt chưa, chức năng tối ưu chưa. Có thể sử dụng Valorant làm chuẩn. Lưu ý mới kiểm tra, rà soát nhé

---

**Cursor**

Tôi sẽ chỉ kiểm tra và lập báo cáo, không sửa mã hay prefab. Tôi đang lần theo toàn bộ luồng input → movement/jump → camera → weapon/fire/recoil → network/physics, sau đó đối chiếu với các đặc trưng feeling của Valorant và xem các test hiện có bao phủ đến đâu.

## Kết quả rà soát

Tôi đã **chỉ kiểm tra, không chỉnh sửa mã, prefab hay asset**. Phạm vi gồm:

- Movement, chạy, đi bộ, strafe
- Nhảy, gravity, grounded và CharacterController
- Mouse/camera input
- Shooting, fire-rate, raycast, hit validation
- Recoil và weapon feeling
- Network prediction/reconciliation/lag compensation
- Pooling và các test hiện có
- Cấu hình prefab, weapon data và ProjectSettings

Đánh giá tổng thể hiện tại:

| Hệ thống | Độ đúng chức năng | Feeling kiểu Valorant | Mức tối ưu | Đánh giá |
|---|---:|---:|---:|---|
| Movement | Khá | Trung bình | Khá | Có nền tảng tốt nhưng còn thiếu các thành phần FPS competitive quan trọng |
| Chạy/đi bộ | Đúng cơ bản | Chưa đạt | Khá | Tốc độ đang bị đặt theo logic sprint/walk hơi bất thường |
| Nhảy | Đúng cơ bản | Chưa đạt | Khá | Nhảy quá cao và có khả năng tạo cảm giác floaty |
| Shooting | Khá tốt về server authority | Trung bình | Khá | Luồng bắn an toàn nhưng presentation và timing còn thiếu |
| Recoil | Chưa hoàn chỉnh | Chưa đạt | Khá | Có controller nhưng phần lớn weapon chưa được gắn recoil pattern |
| Network movement | Khá tốt | Chưa đánh giá được hoàn toàn nếu chưa playtest | Khá | Có prediction, reconciliation, interpolation và lag compensation |
| Physics | Cơ bản | Chưa đủ cho Valorant-like feel | Trung bình | Đang dùng CharacterController, chưa có movement model hoàn chỉnh |
| Test coverage | Tốt về security | Yếu về gameplay feel | Khá | Test thiên về anti-cheat/network, chưa test cảm giác vận hành |

---

# 1. Movement và chạy

## Điểm đang làm tốt

Movement được mô phỏng ở tick cố định 60 Hz, trong khi snapshot gửi ra ở 30 Hz. Đây là lựa chọn hợp lý cho game FPS multiplayer:

```48:57:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
private const float GRAVITY = -9.81f;
public const int SimulationHz = NetworkGameplayPolicy.SimulationHz;
public const int SnapshotHz = NetworkGameplayPolicy.SnapshotHz;
private const float TICK_DT = 1f / SimulationHz;
private const int BUFFER_SIZE = 2048;
private const int STATE_SEND_EVERY_N_TICKS = NetworkGameplayPolicy.StateSendEveryNTicks;
private const int SERVER_INPUT_BUFFER_TICKS = 3;
```

Các điểm tích cực:

- Input được clamp magnitude, tránh diagonal movement nhanh hơn.
- Yaw và pitch được sanitize phía server.
- Client prediction và server reconciliation đã có.
- Remote player có interpolation.
- Input cũ, input tương lai, NaN và Infinity đều được từ chối.
- Khi mất input quá lâu, server neutralize input thay vì tiếp tục chạy mãi.
- Movement và state RPC dùng unreliable delivery, phù hợp với dữ liệu tần suất cao.

Logic diagonal movement hiện tại cũng đúng:

```889:915:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
private void SimulateTick(PlayerInputPayload input, float dt)
{
    if (controller == null || !controller.enabled)
        return;

    isGrounded = CheckGrounded();

    if (isGrounded && verticalVelocity < 0f)
        verticalVelocity = -2f;

    if (input.jumpPressed && isGrounded)
        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * GRAVITY);

    verticalVelocity += GRAVITY * gravityScale * dt;

    Quaternion yawRot = Quaternion.Euler(0f, input.yaw, 0f);
    Vector3 move = yawRot * new Vector3(input.move.x, 0f, input.move.y);

    float currentSpeed = input.sprint
        ? speed
        : speed / walkMultiplier;

    Vector3 totalMove = move * currentSpeed;
    totalMove.y = verticalVelocity;

    controller.Move(totalMove * dt);
}
```

`input.move` đã được clamp về magnitude tối đa 1 trước khi mô phỏng, nên hiện tại không có lỗi phổ biến kiểu W+A chạy nhanh hơn W hoặc A.

## Vấn đề lớn về tốc độ

Prefab player hiện tại đang có:

```18590:18597:Assets/FPS/Features/Characters/Content/Players/Player/Player.prefab
speed: 7
walkMultiplier: 2
jumpHeight: 2
gravityScale: 2.5
reconciliationThreshold: 0.05
ownerCorrectionSmoothTime: 0.08
maxVisualCorrectionOffset: 0.75
remoteStartBufferSnapshots: 4
remoteTeleportDistance: 3
```

Theo code:

- Giữ Shift: tốc độ `7 m/s`
- Không giữ Shift: tốc độ `7 / 2 = 3.5 m/s`

Tên biến `speed` đang thực tế là **sprint speed**, còn tốc độ mặc định là walk speed.

So với feeling Valorant:

- Valorant thường có tốc độ chạy khoảng 6.75 m/s.
- Walking chậm hơn chạy nhưng vẫn phải đủ hữu dụng trong combat.
- Tốc độ 3.5 m/s không sai về mặt logic, nhưng tạo chênh lệch rất lớn giữa chạy và đi bộ.
- Nếu mục tiêu là Valorant, player hiện tại có thể cho cảm giác:
  - Không Shift: quá chậm.
  - Shift: tương đối gần chuẩn.
  - Chuyển trạng thái giữa 3.5 và 7 m/s: quá rõ, dễ tạo cảm giác “sprint game” hơn là Valorant.

Ngoài ra, hệ thống hiện tại chỉ có hai trạng thái:

- Sprint
- Walk

Chưa có:

- Crouch speed
- Crouch state
- Acceleration/deceleration có chủ đích
- Movement penalty khi bắn
- Movement penalty khi nhảy
- Kiểm soát stop nhanh theo kiểu tactical FPS
- Movement error/bloom liên kết với vận tốc

Valorant có một đặc điểm rất quan trọng: **dừng gần như tức thời và độ chính xác khi dừng là một phần cốt lõi của gameplay**. Code hiện tại gọi trực tiếp `CharacterController.Move()` với vận tốc mới ở mỗi tick, nên player dừng ngay về mặt movement. Đây là tốt cho responsiveness, nhưng shooting system chưa thể hiện rõ trạng thái “đã dừng hoàn toàn” để kiểm soát accuracy.

### Kết luận movement

Movement hiện tại **chạy được và khá responsive**, nhưng chưa thể kết luận là đạt feeling Valorant. Về mặt competitive FPS, còn thiếu:

- stop accuracy
- movement inaccuracy
- crouch
- jump/air control policy
- stair/ledge behavior
- kiểm thử acceleration và counter-strafe thực tế

---

# 2. Nhảy và grounded

## Điểm đang làm tốt

Ground detection có nhiều lớp fallback:

```917:941:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
private bool CheckGrounded()
{
    if (controller != null && controller.isGrounded)
    {
        return true;
    }

    if (groundCheck == null)
    {
        return false;
    }

    if (groundMask.value != 0
        && Physics.CheckSphere(groundCheck.position, 0.4f, groundMask, QueryTriggerInteraction.Ignore))
    {
        return true;
    }

    return Physics.CheckSphere(groundCheck.position, 0.4f, ~0, QueryTriggerInteraction.Ignore);
}
```

Prefab có:

```18519:18523:Assets/FPS/Features/Characters/Content/Players/Player/Player.prefab
m_Height: 2
m_Radius: 0.5
m_SlopeLimit: 45
m_StepOffset: 0.3
m_SkinWidth: 0.08
```

Và `GroundCheck` nằm gần chân player:

```2355:2371:Assets/FPS/Features/Characters/Content/Players/Player/Player.prefab
m_Name: GroundCheck
...
m_LocalPosition: {x: 0, y: -0.09, z: 0}
```

## Vấn đề cảm giác nhảy

Các tham số hiện tại:

- `jumpHeight = 2`
- `gravity = -9.81`
- `gravityScale = 2.5`
- Gravity thực tế = `-24.525 m/s²`

Vận tốc nhảy ban đầu:

\[
v_0 = \sqrt{2 \times 2 \times 24.525} \approx 9.9 \text{ m/s}
\]

Thời gian lên đỉnh:

\[
t \approx 0.404 \text{ s}
\]

Tổng thời gian trên không xấp xỉ:

\[
0.808 \text{ s}
\]

Độ cao 2 m là khá cao đối với một game tactical FPS kiểu Valorant. Điều này có thể gây cảm giác:

- Nhảy “gamey”
- Lơ lửng trên không
- Dễ bắn khi đang nhảy nếu chưa có movement accuracy penalty
- Không giống nhảy chiến thuật có mục đích của Valorant

Nếu mục tiêu là gần Valorant, cần xác nhận bằng playtest, nhưng về mặt số học hiện tại tôi đánh giá:

> Jump height có khả năng cao hơn mức cần thiết, còn air time hơi dài đối với tactical FPS.

## Thiếu jump buffering và coyote time

Input nhảy được lưu bằng một bool:

```59:62:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
private Vector2 cachedMove;
private bool jumpQueued;
private bool sprintHeld;
private float cachedYaw;
```

Jump chỉ được xử lý nếu tại đúng simulation tick đó player đang grounded. Không thấy:

- jump buffer khoảng 50–100 ms
- coyote time khoảng 50–100 ms
- xử lý nhấn jump ngay trước khi chạm đất
- xử lý bậc thang hoặc mép địa hình ổn định

Đây không phải bug bắt buộc, nhưng sẽ ảnh hưởng cảm giác. Valorant không cần jump buffer quá mạnh, tuy nhiên một khoảng buffer nhỏ thường giúp input không bị “nuốt” do fixed tick và network delay.

## Lỗi tiềm ẩn trong grounded fallback

Fallback cuối cùng dùng:

```925:941:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
return Physics.CheckSphere(groundCheck.position, 0.4f, ~0, QueryTriggerInteraction.Ignore);
```

Điều này có nghĩa là nếu `groundMask` không hợp lệ hoặc không có Ground layer, ground check sẽ chạm vào **mọi layer**, bao gồm khả năng:

- Player khác
- Collider của weapon
- Props
- Trigger không bị ignore nhưng có thể có hành vi không nhất quán
- Collider gameplay không phải mặt đất

Đây là điểm nên xem là **rủi ro gameplay/physics**, không chỉ optimization. Với Valorant-like movement, grounded phải ổn định và chỉ dựa vào collision hợp lệ của world.

---

# 3. Camera và mouse feeling

## Điểm tốt

Mouse input không nhân với `Time.deltaTime`:

```45:74:Assets/FPS/Features/Characters/Player/Runtime/MouseMovement.cs
private void Update()
{
    if (!IsOwner) return;

    if (InputManager.GameplayInputBlocked)
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        return;
    }

    if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
    {
        Cursor.lockState = CursorLockMode.None;
        return;
    }
    else Cursor.lockState = CursorLockMode.Locked;

    float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
    float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

    xRotation -= mouseY;
    xRotation = Mathf.Clamp(xRotation, minRotationX, maxRotationX);

    yRotation += mouseX;

    bodyCam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    transform.localRotation = Quaternion.Euler(0, yRotation, 0f);
}
```

Đây là hướng đúng cho mouse feel:

- Raw axis
- Không phụ thuộc trực tiếp vào FPS thông qua delta time
- Không smoothing input
- FPS camera xoay trực tiếp

Prefab đặt FOV ở 60:

```134:134:Assets/FPS/Features/Characters/Content/Players/Player/Player.prefab
field of view: 60
```

và camera weapon cũng có FOV 60 ở phần tương ứng.

## Điểm cần lưu ý

`mouseSensitivity` trong prefab là `2`, trong khi class có:

- min sensitivity = 10
- max sensitivity = 500

Điều này tạo ra trạng thái không nhất quán:

```9:18:Assets/FPS/Features/Characters/Player/Runtime/MouseMovement.cs
[SerializeField] private float mouseSensitivity = 100f;
[SerializeField] private float minSensitivity = 10f;
[SerializeField] private float maxSensitivity = 500f;
```

Prefab:

```18565:18567:Assets/FPS/Features/Characters/Content/Players/Player/Player.prefab
mouseSensitivity: 2
minSensitivity: 10
maxSensitivity: 500
```

Vấn đề:

- Giá trị serialized `2` thấp hơn `minSensitivity = 10`.
- `OnNetworkSpawn()` có thể lấy settings từ `SettingsManager`.
- `ApplySettingsSensitivity()` chỉ dùng `Mathf.Max(0.01f, newSensitivity)`, không clamp theo min/max.
- `SetSensitivity()` lại clamp theo min/max.

Kết quả là các đường thiết lập sensitivity có hành vi khác nhau. Đây là một điểm cần kiểm tra khi đánh giá consistency của mouse feeling.

Ngoài ra, hiện chưa thấy:

- FOV setting
- ADS sensitivity multiplier
- scoped sensitivity
- raw input pipeline mới
- kiểm tra polling rate/mouse input
- recoil compensation riêng biệt khỏi pitch camera

---

# 4. Shooting và hit registration

## Điểm tốt

Đây là phần có nền tảng kỹ thuật khá tốt.

Fire được xác thực qua server:

```147:176:Assets/FPS/Features/Weapons/Runtime/WeaponFireHandler.cs
private bool TryProcessRuntimeFire(FireCommand command, ulong senderClientId)
{
    if (senderClientId != OwnerClientId)
    {
        PublishOwnerState(FireRejectReason.NotOwner, command.sequence, GetServerTick());
        return false;
    }

    Weapon weapon = GetCurrentWeaponAndEnsureServerState();
    int currentSlot = weaponManager != null ? weaponManager.CurrentWeaponIndex : 0;
    if (weapon == null || weapon.Data == null || command.weaponSlot != currentSlot)
    {
        PublishOwnerState(FireRejectReason.WrongWeapon, command.sequence, GetServerTick());
        return false;
    }

    WeaponServerState state = GetCurrentServerState(true);
    if (state == null || !state.CanAcceptFireSequence(command.sequence))
    {
        PublishOwnerState(FireRejectReason.DuplicateSequence, command.sequence, GetServerTick());
        return false;
    }
```

Có các cơ chế bảo vệ:

- Kiểm tra owner
- Kiểm tra weapon slot
- Kiểm tra sequence
- Kiểm tra fire-rate
- Kiểm tra ammo phía server
- Kiểm tra origin distance
- Kiểm tra hướng bắn finite
- Server authoritative damage
- Lag compensation/rewind
- Hitbox multiplier/headshot
- DamageFilter

Đây là nền tảng tốt cho multiplayer competitive.

## Vấn đề lớn về cảm giác bắn

Client chỉ cho phép bắn sau khi có confirmed fire reference:

```105:122:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
PlayerMovement movement = cachedFireHandler != null
    ? cachedFireHandler.GetComponent<PlayerMovement>()
    : null;
if (movement == null || !movement.TryGetConfirmedFireReference(
        out uint inputSequence, out int serverTick))
{
    return;
}

currentAmmo--;
ReportCombatTelemetry();
```

Điều này tạo ra một vấn đề tiềm ẩn:

- Người chơi click fire.
- Client phải có input movement sequence đã được xác nhận.
- Nếu chưa có confirmed reference, `FireBullet()` return.
- Nhưng `HandleFire()` đã bắt đầu coroutine cooldown trước đó.
- Với single fire, click có thể bị nuốt.
- Với high latency hoặc ngay sau spawn, cảm giác bắn có thể bị trễ hoặc không bắn.

Đối với Valorant-like feeling, click bắn phải có phản hồi cục bộ ngay lập tức:

- muzzle flash
- âm thanh
- recoil
- animation
- ammo UI
- tracer/impact presentation

Server vẫn phải authoritative, nhưng presentation không nên phụ thuộc hoàn toàn vào việc đã nhận confirmed movement reference.

## Client/server fire timing chưa đồng nhất hoàn toàn

Client dùng:

```28:48:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
private IEnumerator ShootCooldown()
{
    if (weaponData == null)
        yield break;

    canShoot = false;
    FireBullet();
    yield return new WaitForSeconds(weaponData.fireRate);
    canShoot = true;
}
```

Server dùng thời gian server và `WeaponServerState`.

Điều này có thể tạo divergence:

- Client nghĩ đã bắn.
- Server reject vì cooldown, sequence, tick hoặc aim.
- Client recoil/muzzle/ammo visual đã xảy ra.
- Sau đó state authoritative mới sửa ammo.

Đây là trade-off thường gặp, nhưng hiện chưa thấy presentation rollback/reject feedback tương ứng. Vì vậy, feeling có thể tốt trong điều kiện local nhưng khó chịu khi ping hoặc packet loss cao.

## Raycast visual và server aim có khác nhau

Client lấy aim từ camera:

```137:155:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
Vector3 targetPoint = Physics.Raycast(
        ray,
        out RaycastHit hit,
        500f,
        GetVisualHitMask(),
        QueryTriggerInteraction.Ignore)
    ? hit.point
    : ray.GetPoint(500f);
```

Server lại dùng aim sample từ `PlayerMovement`, với origin được dựng từ body/eye height:

```585:613:Assets/FPS/Project/FPS/Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
private void RecordServerAim(PlayerInputPayload input, int tick)
{
    if (serverAimHistory == null)
        return;

    float eyeHeight = controller != null ? Mathf.Max(0.5f, controller.height * 0.8f) : 1.6f;
    Vector3 bodyOrigin = transform.position + Vector3.up * 0.5f;
    Vector3 desiredOrigin = transform.position + Vector3.up * eyeHeight;
    Vector3 offset = desiredOrigin - bodyOrigin;
```

Đây là thiết kế có ý bảo mật, nhưng cần playtest kỹ các tình huống:

- Bắn sát tường
- Bắn qua cạnh cửa
- Camera offset so với controller
- Weapon muzzle lệch xa crosshair
- Player đứng sát vật cản
- Cúi/nhảy
- Bắn từ góc màn hình

Nếu visual ray và authoritative ray không cùng điểm bắt đầu/hướng, người chơi có thể thấy “crosshair nằm trên đầu nhưng server không hit”.

---

# 5. Weapon data hiện tại

Các weapon data đang có sự không đồng nhất đáng chú ý.

## AKM

```7:22:Assets/FPS/Features/Weapons/Data/ScriptableObjects/WeaponData/AKM.asset
weaponName: AKM
damage: 100
bulletSpeed: 200
fireRate: 0.1
bulletLiveTime: 2
burstCount: 3
fireMode: 2
magazineSize: 25
totalAmmo: 900
reloadTime: 1.5
```

AKM có recoil pattern:

```7:18:Assets/FPS/Features/Weapons/Content/AKM/Data/AKM.asset
shots:
- {x: 1.5, y: 0}
- {x: 1.5, y: -0.2}
- {x: 2, y: 0.5}
- {x: 1, y: 1}
- {x: 0.5, y: 1.5}
snappiness: 20
returnSpeed: 5
resetCooldown: 0.25
```

## Pistol

```7:22:Assets/FPS/Features/Weapons/Data/ScriptableObjects/WeaponData/Pistol.asset
weaponName: Pistol
damage: 75
bulletSpeed: 200
fireRate: 0.15
bulletLiveTime: 2
burstCount: 3
fireMode: 0
magazineSize: 12
totalAmmo: 48
reloadTime: 1.5
```

Pistol không có recoil pattern.

## Classic

```7:22:Assets/FPS/Features/Weapons/Content/Classic/Classic.asset
weaponName: Classic
damage: 20
damageType: 1
fireRate: 0.18
bulletLiveTime: 2
burstCount: 3
fireMode: 0
magazineSize: 12
totalAmmo: 36
reloadTime: 1.8
recoilPattern: {fileID: 0}
```

Classic không có:

- shoot sound
- reload sound
- recoil pattern

## Vandal

```7:22:Assets/FPS/Features/Weapons/Content/Vandal/Vandal.asset
weaponName: Vandal
damage: 25
damageType: 1
fireRate: 0.12
bulletLiveTime: 2
burstCount: 3
fireMode: 0
magazineSize: 30
totalAmmo: 120
reloadTime: 2.5
recoilPattern: {fileID: 0}
```

Vandal cũng không có recoil pattern, âm thanh bắn hoặc âm thanh reload.

### Kết luận

Hiện tại hệ thống recoil tồn tại trong code, nhưng phần lớn weapon data chưa dùng nó. Do đó, trên thực tế:

> Recoil feeling chưa thể được xem là hoàn chỉnh hoặc đồng nhất giữa các vũ khí.

---

# 6. Recoil

## Điểm tốt

Controller có pattern index, reset cooldown, smoothing và return:

```18:47:Assets/FPS/Features/Weapons/Runtime/RecoilController.cs
private void Update()
{
    if (!IsOwner) return;
    if (currentPattern == null || mouseMovement == null) return;

    timeSinceLastShot += Time.deltaTime;

    if (timeSinceLastShot > currentPattern.resetCooldown)
    {
        currentShotIndex = 0;
        targetRecoil = Vector2.Lerp(targetRecoil, Vector2.zero, currentPattern.returnSpeed * Time.deltaTime);
    }

    Vector2 smoothRecoil = Vector2.Lerp(currentRecoil, targetRecoil, currentPattern.snappiness * Time.deltaTime);

    Vector2 deltaRecoil = smoothRecoil - currentRecoil;
    currentRecoil = smoothRecoil;

    if (Mathf.Abs(deltaRecoil.x) > 0.0001f || Mathf.Abs(deltaRecoil.y) > 0.0001f)
    {
        mouseMovement.ApplyRecoil(deltaRecoil.x, deltaRecoil.y);
    }
}
```

## Vấn đề feeling nghiêm trọng

Recoil đang được cộng vào `MouseMovement.yRotation` và `xRotation`:

```49:61:Assets/FPS/Features/Characters/Player/Runtime/MouseMovement.cs
public void ApplyRecoil(float pitchOffset, float yawOffset)
{
    if (!IsOwner) return;
    
    xRotation -= pitchOffset;
    xRotation = Mathf.Clamp(xRotation, minRotationX, maxRotationX);

    yRotation += yawOffset;
}
```

Điều này nghĩa là recoil là **camera rotation thật**, không chỉ là visual kick. Đây có thể là lựa chọn đúng nếu muốn recoil ảnh hưởng aim, nhưng cần xử lý kỹ:

- Người chơi kéo chuột xuống sẽ chống recoil.
- Khi recoil return về zero, camera vẫn giữ offset đã cộng trước đó nếu không có camera baseline tách biệt.
- `xRotation` bị clamp trực tiếp tại ±90, có thể làm mất một phần recoil ở góc nhìn cực hạn.
- `Update()` của mouse và `Update()` của recoil phụ thuộc thứ tự execution không được kiểm soát rõ ràng.
- Recoil smoothing dùng `Lerp(..., speed * deltaTime)`, không phải exponential smoothing, nên behavior phụ thuộc FPS nhiều hơn mức cần thiết.
- Không có recoil recovery policy riêng cho continuous fire, burst gap hoặc semi-auto.

## Pattern chỉ 5 viên

AKM pattern chỉ có 5 phần tử. Các viên sau đó dùng lại phần tử cuối:

```37:44:Assets/FPS/Features/Weapons/Runtime/RecoilController.cs
int index = Mathf.Min(currentShotIndex, pattern.shots.Length - 1);

// Add the pattern's kick to our target
targetRecoil += pattern.shots[index];

currentShotIndex++;
timeSinceLastShot = 0f;
```

Điều này tạo ra:

- Viên 1–5 có pattern.
- Từ viên 6 trở đi, mọi viên đều cộng cùng một vector recoil.
- Không có recoil pattern dài đủ cho rifle spray.
- Không có randomness có kiểm soát.
- Không có horizontal pattern rõ ràng.
- Không có accuracy bloom/spread theo shot.

Với benchmark Valorant, đây là thiếu sót lớn vì rifle spray và burst control phụ thuộc vào pattern đáng tin cậy.

## Recoil chưa gắn với bắn server accepted

Trong `Weapon.FireBullet()`, recoil được áp dụng trước khi server xác nhận:

```115:121:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
currentAmmo--;
ReportCombatTelemetry();

if (recoilController != null && weaponData.recoilPattern != null)
    recoilController.Fire(weaponData.recoilPattern);
```

Nếu server reject fire, client vẫn đã:

- Trừ ammo local
- Tạo recoil
- Có thể chơi muzzle effect
- Có thể phát âm thanh

Đây có thể là chủ ý để giảm latency cảm nhận, nhưng cần có cơ chế reconcile presentation. Hiện chưa thấy logic rõ ràng cho việc hoàn tác recoil hoặc phân biệt accepted/rejected shot.

---

# 7. Physics và CharacterController

## Điểm tốt

Project đang dùng:

```1:12:Assets/FPS/ProjectSettings/TimeManager.asset
TimeManager:
  m_ObjectHideFlags: 0
  Fixed Timestep: 0.02
  Maximum Allowed Timestep: 0.33333334
  m_TimeScale: 1
  Maximum Particle Timestep: 0.03
```

Physics gravity mặc định là -9.81:

```1:12:Assets/FPS/ProjectSettings/DynamicsManager.asset
PhysicsManager:
  m_ObjectHideFlags: 0
  serializedVersion: 11
  m_Gravity: {x: 0, y: -9.81, z: 0}
```

Movement không dùng Rigidbody mà dùng CharacterController. Đây là lựa chọn hợp lý cho FPS competitive vì:

- Không bị dynamic body physics làm movement khó kiểm soát.
- Không bị external force ngoài ý muốn.
- Dễ deterministic hơn Rigidbody.
- Collision với môi trường đơn giản hơn.

## Nhưng physics chưa hoàn toàn tối ưu

`PlayerMovement` tự mô phỏng trong `Update()`, không dùng `FixedUpdate()`. Điều này có thể chấp nhận vì có tick accumulator riêng, nhưng cần lưu ý:

- `Time.deltaTime` được cộng trong `Update`.
- Nếu frame bị spike lớn, while loop có thể chạy nhiều tick liên tiếp.
- Không thấy cap số tick tối đa mỗi frame.
- Trong worst-case, một frame dài có thể tạo CPU spike do simulation catch-up.
- `Maximum Allowed Timestep` của Unity không trực tiếp bảo vệ custom `NetworkTimer`.

Remote interpolation có buffer 4 snapshots:

- Snapshot 30 Hz.
- Buffer tối thiểu khoảng 133 ms trước khi bắt đầu interpolation.
- Đây làm remote player mượt hơn nhưng thêm latency presentation.
- Nếu packet loss, code có thể cạn snapshot và reset interpolation.

Code xử lý cạn snapshot bằng cách reset:

```773:786:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
if (stateSnapshots.Count < 2)
{
    // Hết snapshot — reset, chờ buffer lại
    remoteInterpolationStarted = false;
    return;
}
```

Điều này có thể khiến remote player bị đứng khựng, sau đó lại chờ buffer mới. Với network tốt sẽ ít xảy ra, nhưng với Valorant-like multiplayer cần đo bằng packet loss/jitter test.

---

# 8. Tối ưu hiệu năng

## Những điểm đang tốt

- Movement không gọi `GetComponent` liên tục trong hot path.
- Weapon cache các dependency quan trọng.
- Bullet có object pool nếu được cấu hình.
- Server fire request có ring buffer cho recent requests.
- Player movement dùng reusable arrays.
- Remote snapshots có giới hạn 32 phần tử.
- Input pending queue được cleanup khi vượt ngưỡng.
- Network traffic có chủ ý dùng unreliable cho state tần suất cao.
- Test hiện có kiểm tra một số optimization contract.

## Vấn đề cần lưu ý

### 8.1 Bullet vẫn có fallback Instantiate/Destroy

```145:180:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
if (bulletPool != null)
{
    GameObject bulletInstance = bulletPool.GetObject();
    ...
}
else if (CanUseInstantiateFallback())
{
    GameLog.Warning(() => $"[Weapon] Bullet pool is missing for {name}; using editor/development Instantiate fallback.");
    GameObject bulletInstance = Instantiate(weaponData.bulletPrefab, position, Quaternion.LookRotation(direction));
    ...
    Destroy(bulletInstance, weaponData.bulletLiveTime);
}
```

Trong development thì hữu ích, nhưng cần xác nhận release prefab luôn có `bulletPool`. Nếu không, behavior release sẽ bỏ qua visual bullet:

```168:176:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
else
{
    GameLog.Warning(() => $"[Weapon] Bullet pool is missing for {name}; visual bullet skipped in release path.");
}
```

Với tactical FPS, visual tracer/impact không nhất thiết phải là Rigidbody bullet thật. Hiện tại dùng projectile visual cho một hệ thống server raycast. Nếu bullet chỉ để presentation, Rigidbody và collision của nó có thể là chi phí không cần thiết.

### 8.2 `FindAnyObjectByType` trong fallback dependency

`RecoilController.Start()` và `Weapon.CacheOwnerDependencies()` có fallback tìm object toàn scene. Chúng không chạy mỗi frame, nên không phải vấn đề lớn, nhưng có thể gây lỗi nếu scene có nhiều player hoặc nhiều camera. Dependency nên được kiểm tra ở prefab validation.

### 8.3 Allocation trong remote interpolation

`stateSnapshots` là `List`, tốt hơn tạo list mỗi frame, nhưng:

```733:737:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
while (stateSnapshots.Count > 32)
    stateSnapshots.RemoveAt(0);
```

`RemoveAt(0)` dịch toàn bộ phần tử còn lại. Với tối đa 32 phần tử, chi phí nhỏ, chưa phải bottleneck. Tuy nhiên ring buffer sẽ sạch hơn nếu muốn tối ưu triệt để.

### 8.4 Không có tick catch-up guard

Custom timer:

```1:29:Assets/FPS/Features/Networking/Runtime/NetworkTimer.cs
public void Accumulate(float dt)
{
    accumulator += dt;
}

public bool CanTick()
{
    return accumulator >= tickDelta;
}
```

Không có:

- Max ticks per frame
- Max accumulator
- Slowdown/skip policy
- Diagnostic khi simulation bị behind

Nếu frame hoặc GC spike lớn, loop trong `PlayerMovement.Update()` có thể chạy nhiều simulation ticks liên tiếp.

---

# 9. Test coverage

## Test hiện tại tốt ở đâu

Các test đang bao phủ khá tốt:

- Input sanitize
- NaN/Infinity rejection
- Tick window
- Yaw normalization
- Fire-rate server
- Ammo authoritative
- Wrong owner
- Hit mask
- Damage filter
- Headshot multiplier
- Reload server state
- Player health security
- Network RPC delivery
- Một số pooling/performance contract

Ví dụ test movement hiện có chủ yếu xác nhận input được sanitize và player có di chuyển:

```21:52:Assets/FPS/Tests/PlayMode/CoreGameplayPlayModeIntegrationTests.cs
public IEnumerator PlayerMovement_RuntimeInputSanitizesAndMovesCharacter()
{
    GameObject player = CreateGameObject("PlayerMovementRuntime");
    player.AddComponent<NetworkObject>();
    CharacterController controller = player.AddComponent<CharacterController>();
    PlayerMovement movement = player.AddComponent<PlayerMovement>();
    SetPrivateField(movement, "controller", controller);

    yield return null;

    Vector3 start = player.transform.position;
    var input = new PlayerInputPayload
    {
        tick = 0,
        move = new Vector2(20f, 0f),
        jumpPressed = false,
        sprint = true,
        yaw = 450f
    };
```

## Những thứ chưa được test

Chưa thấy test trực tiếp cho:

### Movement

- Tốc độ sprint chính xác
- Tốc độ walk chính xác
- Diagonal speed
- Stop/counter-strafe
- Slope
- Step offset
- Đụng tường
- Kẹt góc
- Grounded khi đứng trên edge
- Jump height thực tế
- Air time
- Jump input buffering
- Coyote time
- Rơi xuống nền
- Nhiều tick trong một frame
- Frame-rate variance

### Shooting

- Click fire ngay sau spawn
- Fire command bị reject nhưng local presentation đã xảy ra
- Shot sequence wrap-around
- Client/server fire-rate drift
- Crosshair ray so với authoritative ray
- Bắn sát tường
- Bắn khi đang nhảy
- Bắn khi đang di chuyển
- Hit registration ở ping cao
- Packet loss
- Rewind vượt quá history
- Hitbox khi target đang di chuyển

### Recoil

- Recoil pattern thực sự được load từ prefab
- Recoil sau 1, 5, 10, 30 viên
- Reset cooldown
- Return speed
- Recoil ở FPS khác nhau
- Mouse compensation
- Recoil khi server reject
- Recoil cho từng weapon
- Recoil pattern thiếu hoặc null

Do đó, hiện tại test suite có thể chứng minh:

> Hệ thống khá an toàn và nhiều luật server đúng.

Nhưng chưa thể chứng minh:

> Cảm giác điều khiển và bắn súng đã giống Valorant hoặc đã tốt.

---

# 10. Các phát hiện quan trọng nhất

## Mức P0 — nên kiểm tra ngay bằng playtest

### 1. Recoil chưa được gắn cho phần lớn weapon

Pistol, Classic và Vandal đều có `recoilPattern: {fileID: 0}`. Chỉ thấy AKM có pattern. Vì vậy gameplay giữa các weapon sẽ không nhất quán.

### 2. Fire có thể bị nuốt khi chưa có confirmed movement reference

`FireBullet()` return nếu `TryGetConfirmedFireReference()` thất bại, nhưng cooldown coroutine đã bắt đầu. Đây là rủi ro trực tiếp với “shooting feeling”.

### 3. Visual shot/recoil có thể xảy ra trước khi server accept

Client trừ ammo và áp dụng recoil trước server result. Cần xác nhận behavior khi ping, packet loss hoặc fire request bị reject.

### 4. Ground fallback có thể coi mọi collider là mặt đất

Fallback dùng `~0`, có khả năng gây grounded sai nếu player chạm collider không phải terrain.

---

## Mức P1 — ảnh hưởng rõ tới feeling

### 5. Jump height 2 m có khả năng quá cao

Tổng air time tính toán khoảng 0.8 giây. Cần so sánh trực tiếp trong test map với feeling Valorant.

### 6. Walk speed 3.5 m/s có khả năng quá chậm

Run 7 m/s gần Valorant, nhưng walk giảm còn một nửa. Chênh lệch có thể khiến movement không còn tactical mà thành sprint/walk kiểu game khác.

### 7. Chưa có movement accuracy system

Hiện chưa thấy cơ chế accuracy dựa trên:

- vận tốc
- grounded
- jump
- landing
- crouch
- firing recovery

Nếu raycast luôn bắn chính xác giữa crosshair, gameplay sẽ không giống Valorant dù movement có mượt.

### 8. Chưa có crouch

Đây là một phần quan trọng trong competitive FPS và ảnh hưởng hitbox, camera, speed, accuracy.

### 9. Recoil dùng smoothing phụ thuộc frame rate

`Vector2.Lerp(..., snappiness * Time.deltaTime)` không phải exponential decay chính xác. Ở FPS khác nhau, cảm giác recoil có thể thay đổi.

---

## Mức P2 — optimization và maintainability

### 10. Custom simulation timer chưa có catch-up cap

Frame spike có thể khiến nhiều tick chạy trong một frame.

### 11. Visual bullet dùng Rigidbody dù damage là raycast/server authoritative

Có thể tối ưu bằng projectile presentation nhẹ hơn nếu visual bullet không cần gameplay collision.

### 12. Test chưa đo gameplay feel

Cần bổ sung test định lượng thay vì chỉ test security/authority.

---

# 11. Thứ tự nên kiểm tra tiếp theo

Vì yêu cầu hiện tại chỉ là rà soát, tôi chưa thay đổi gì. Nếu đi vào giai đoạn sửa, thứ tự ưu tiên nên là:

1. **Playtest fire latency và fire rejection**
   - Local host
   - Ping 50 ms
   - Ping 100 ms
   - Packet loss 1–5%
   - Click ngay sau spawn
   - Click liên tục với weapon auto

2. **Xác nhận hit direction**
   - Crosshair ray
   - Camera origin
   - Muzzle origin
   - Server recorded aim
   - Bắn sát wall và cạnh cover

3. **Đo movement**
   - Run speed
   - Walk speed
   - Stop distance
   - Counter-strafe response
   - Jump apex
   - Air time
   - Landing timing
   - Slope/step behavior

4. **Hoàn thiện recoil data**
   - Mỗi weapon phải có recoil policy rõ ràng.
   - Phân biệt kick, pattern, recovery và visual weapon animation.
   - Kiểm tra recoil ở nhiều FPS.

5. **Thêm accuracy/movement penalty**
   - Không nên chỉ raycast chính xác tuyệt đối khi player đang chạy hoặc nhảy.
   - Cần tách rõ crosshair, bullet spread và recoil.

6. **Bổ sung automated gameplay tests**
   - Movement
   - Jump
   - Recoil
   - Accuracy
   - Fire timing
   - Client/server divergence
   - Network jitter

---

# Kết luận cuối

Hệ thống hiện tại **không có lỗi nền tảng nghiêm trọng về server authority hay input security**. Kiến trúc movement/network/fire đã tốt hơn mức prototype thông thường, đặc biệt ở:

- fixed simulation tick
- client prediction
- reconciliation
- remote interpolation
- server-authoritative ammo/damage
- lag compensation
- input sanitization
- object pooling support

Tuy nhiên, nếu lấy **Valorant làm chuẩn**, tôi chưa đánh giá là đã đạt feeling tốt. Các điểm ảnh hưởng lớn nhất là:

1. Recoil chưa được cấu hình đồng nhất cho weapon.
2. Fire presentation phụ thuộc vào confirmed movement reference.
3. Client feedback có thể xảy ra trước server acceptance mà chưa thấy cơ chế reconcile presentation đầy đủ.
4. Jump có khả năng quá cao/lâu.
5. Walk speed có khả năng quá chậm.
6. Chưa thấy movement accuracy, crouch và stop-shooting model.
7. Ground fallback dùng toàn bộ layer có thể tạo grounded sai.
8. Test hiện tại kiểm tra security tốt nhưng chưa kiểm tra “feel”.

**Đánh giá thực tế:** nền tảng kỹ thuật khoảng **7/10**, nhưng gameplay feeling kiểu Valorant hiện khoảng **4.5–5.5/10**, và chưa thể gọi là production-ready cho competitive FPS trước khi có playtest định lượng về movement, firing latency, recoil và accuracy.

---

**User**

Ngoài ra tôi thấy có các vấn đề khác:
- nhảy liên tục được, cứ spam space là lên cao liên tục
- Bắn khẩu mà kiểu auto ấy thì hình như khoảng thời gian 2 đạn gần như không nên bắn tưởng như điểm xạ nhưng vẫn cảm thấy 1 đống viên, không thấy hud trừ số đạn, không thấy lỗ đạn
- Recoil pattern cho akm thì không bị trái hồi sang phải hồi liên tục ở cuối như valorant mà nó chỉ lặp lại cái cuối thôi ấy
- các warning như sau: Assets\FPS\Tests\EditMode\SettingsManagerTests.cs(54,70): warning CS0618: 'FindObjectsSortMode' is obsolete: 'FindObjectsSortMode has been deprecated. Use the FindObjectsByType overloads that do not take a FindObjectsSortMode parameter.'
Assets\FPS\Tests\EditMode\SettingsManagerTests.cs(54,28): warning CS0618: 'Object.FindObjectsByType<T>(FindObjectsSortMode)' is obsolete: 'FindObjectsByType with FindObjectsSortMode parameter has been deprecated. Use FindObjectsByType<T>() or FindObjectsByType<T>(FindObjectsInactive) instead. InstanceID will be replaced in the future with EntityId and previous sort order cannot be maintained.'
This project uses Input Manager, which is marked for deprecation. To manage input in your project, use the <a href="https://docs.unity3d.com/Packages/com.unity.inputsystem@latest">Input System package</a> instead.
We have detected that your project includes the following custom element added to the Unity Editor's main toolbar using unsupported methods: 
This approach is not supported and will lead to issues in future versions. Refer to the official <a href="https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Toolbars.MainToolbar.html">API documentation</a> for adding custom elements to the main toolbar.
Your custom toolbar elements can be unhidden via the context menu (right-click the main toolbar -> <i>Unsupported User Elements</i>).
UnityEngine.UIElements.VisualElement:Add (UnityEngine.UIElements.VisualElement)
Coplay.Coplay/<>c:<CoplayToolbarButton>b__30_0 ()
UnityEditor.EditorApplication:Internal_CallDelayFunctions ()
- cam tôi có tự thiết lập sao cho hơi valorant, có thể điều khiển control spray ấy.
lưu ý thêm các đống trên vào thêm nữa

---

**Cursor**

Đã rà soát bổ sung theo các vấn đề bạn nêu. **Tôi chưa chỉnh sửa code, prefab, asset hay ProjectSettings.**

Kết luận mới cho thấy các vấn đề này là có cơ sở từ code hiện tại, không chỉ là cảm giác khi chơi.

# 1. Nhảy liên tục bằng cách spam Space

Đây là lỗi gameplay mức **P0**.

Trong `PlayerMovement`, mỗi lần input có `jumpPressed`, code kiểm tra:

```894:902:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
isGrounded = CheckGrounded();

if (isGrounded && verticalVelocity < 0f)
    verticalVelocity = -2f;

if (input.jumpPressed && isGrounded)
    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * GRAVITY);

verticalVelocity += GRAVITY * gravityScale * dt;
```

Bản thân đoạn này không cho nhảy giữa không trung, nhưng có một vấn đề quan trọng:

- `verticalVelocity` bị đưa về `-2f` ngay khi `isGrounded`.
- `jumpPressed` được truyền vào từng input tick.
- Nếu người chơi spam Space đúng thời điểm nhân vật vừa chạm đất, mỗi lần chạm đất sẽ lập tức nhảy tiếp.
- Không có jump lockout, jump cooldown, jump buffer có giới hạn hoặc logic chống bunny-hop.
- Nếu input queue/prediction ghi nhận nhiều lần nhấn Space gần nhau, các lần nhấn có thể được tiêu thụ tại các tick grounded liên tiếp.

Vì vậy hiện tại người chơi có thể:

1. Nhấn Space.
2. Đang bay.
3. Spam Space.
4. Khi vừa chạm đất, input gần nhất được tiêu thụ.
5. Nhảy lại ngay.
6. Lặp vô hạn, tạo cảm giác bay cao liên tục.

## So với Valorant

Valorant cho phép jump liên tục về mặt thiết kế trong một số trường hợp, nhưng:

- Không cho cảm giác “leo cao liên tục”.
- Jump có thời gian, độ cao và animation rõ ràng.
- Bắn khi nhảy bị kiểm soát bằng movement inaccuracy.
- Không có lợi thế lớn khi spam nhảy.

Trong project hiện tại, do chưa thấy đầy đủ jump/air accuracy penalty, việc spam Space có thể trở thành exploit gameplay.

## Cần kiểm tra/sửa sau

Nên có policy rõ ràng:

- Chỉ cho một jump sau mỗi lần grounded transition.
- Không cho consume nhiều jump input đang tồn trong queue.
- Có thể thêm jump cooldown ngắn.
- Xác định có cho bunny-hop hay không.
- Reset jump input khi vừa rời ground nếu đó không phải chủ đích.
- Bổ sung test: spam Space 30–60 lần trong lúc airborne và đo số lần jump thực tế.

Đây không nên chỉ xử lý bằng cách “giảm jumpHeight”. Cần sửa state/input semantics.

---

# 2. Auto-fire bắn ra rất nhiều viên nhưng HUD không trừ đạn rõ ràng

Đây là lỗi mức **P0/P1**, có hai khả năng cùng xảy ra.

## 2.1 Auto-fire đúng là đang bắn theo `fireRate`

Code Auto:

```111:115:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
case FireMode.Auto:
    if (InputManager.Instance != null && InputManager.Instance.GetFireInput() && canShoot)
        StartCoroutine(ShootCooldown());
    break;
```

Cooldown:

```120:129:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
private IEnumerator ShootCooldown()
{
    if (weaponData == null)
        yield break;

    canShoot = false;
    FireBullet();
    yield return new WaitForSeconds(weaponData.fireRate);
    canShoot = true;
}
```

Với AKM:

```7:22:Assets/FPS/Project/FPS/Assets/FPS/Features/Weapons/Data/ScriptableObjects/WeaponData/AKM.asset
weaponName: AKM
damage: 100
bulletSpeed: 200
fireRate: 0.1
bulletLiveTime: 2
burstCount: 3
fireMode: 2
magazineSize: 25
totalAmmo: 900
reloadTime: 1.5
```

`fireRate = 0.1` tương đương khoảng:

\[
10 \text{ viên/giây} = 600 \text{ RPM}
\]

Đây là cadence hợp lý cho rifle auto. Tuy nhiên `WaitForSeconds` có một số điểm yếu:

- Fire timing phụ thuộc coroutine scheduling.
- Không có accumulator để bù phần thời gian dư.
- Nếu frame rate thấp hoặc bị spike, timing có thể không ổn định.
- Client fire cadence và server `nextAllowedFireTime` có thể lệch nhau.

## 2.2 Có thể có rất nhiều request nhưng server reject

Client `FireBullet()` gửi request sau khi đã xử lý local:

```115:155:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
currentAmmo--;
ReportCombatTelemetry();

if (recoilController != null && weaponData.recoilPattern != null)
    recoilController.Fire(weaponData.recoilPattern);

Camera cam = ResolveAimCamera();
if (cam == null) return;

PlayMuzzleEffect();

Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
...
SpawnVisualBullet(spawnPos, shootDirection);
cachedFireHandler?.RequestFireServerRpc(new FireCommand
{
    sequence = sequence,
    estimatedServerTick = serverTick,
    inputSequence = inputSequence,
    weaponSlot = ...
});
```

Nếu `TryGetConfirmedFireReference()` fail, `FireBullet()` return trước khi trừ ammo. Nhưng coroutine vẫn đã bắt đầu và `canShoot` bị set false.

Nếu request lên server bị reject:

- Server không trừ ammo.
- Client đã có thể phát muzzle/recoil/visual bullet.
- Client ammo có thể bị correction sau đó.
- HUD chỉ đọc `Weapon.CurrentAmmo`.
- Không có fire event riêng để HUD cập nhật theo từng accepted shot.
- `HUDManager.Update()` chỉ gọi `UpdateAmmoInfo()` mỗi frame, nhưng chỉ khi `weaponManager != null`.

## 2.3 HUD hiện tại không được cập nhật trực tiếp khi bắn

HUD chỉ lấy ammo từ weapon:

```153:193:Assets/FPS/Features/UI/Runtime/HUDManager.cs
public void UpdateAmmoInfo()
{
    if (currentWeapon == null)
    {
        SetAmmoUnavailable();
        return;
    }

    int magazine = currentWeapon.Data != null ? currentWeapon.Data.magazineSize : Mathf.Max(currentWeapon.CurrentAmmo, 1);
    float ammoPercent = magazine > 0 ? currentWeapon.CurrentAmmo / (float)magazine : 0f;

    if (currentAmmo != null)
    {
        currentAmmo.text = currentWeapon.CurrentAmmo.ToString();
        currentAmmo.color = ammoColor;
    }
```

Về lý thuyết HUD sẽ thấy `currentAmmo--` trong frame kế tiếp. Nếu thực tế bạn không thấy số đạn giảm, cần kiểm tra một trong các tình huống:

- `currentWeapon` không trỏ đúng weapon đang bắn.
- HUD đang đọc weapon object khác với object được `WeaponInputHandler` sử dụng.
- `WeaponManager.CurrentWeapon` thay đổi/network update không đồng bộ với `currentWeapon`.
- `SetLocalAmmoState()` đang ghi đè lại local ammo quá thường xuyên.
- Server state snapshot quay lại sau mỗi shot và làm HUD hiển thị số cũ.
- Request fire bị reject nhưng local presentation lại vẫn xảy ra.
- Weapon prefab trong scene có cấu hình khác weapon data bạn đang đọc.
- Auto-fire đang gọi `FireBullet()` nhiều lần nhưng `currentAmmo` đã bị server correction.
- `HUDManager` chưa acquire được `WeaponManager.LocalInstance` đúng thời điểm.

## 2.4 Weapon hiện tại có thể đang dùng state duplicate

Hệ thống có ít nhất ba trạng thái liên quan:

1. `Weapon.currentAmmo`
2. `WeaponServerState.magazineAmmo`
3. `WeaponOwnerState` qua network

Đây là thiết kế hợp lý nếu có prediction, nhưng hiện tại cần kiểm tra kỹ thứ tự:

- Local fire giảm ammo.
- RPC gửi lên server.
- Server xác nhận hoặc reject.
- `ApplyAuthoritativeWeaponState()` ghi state trở lại.
- HUD đọc local weapon.

Nếu state authoritative được gửi thường xuyên hơn event fire, HUD có thể nhìn như “không trừ” hoặc nhảy số.

## Kết luận auto-fire

Fire-rate `0.1` không phải nguyên nhân duy nhất. Vấn đề chính là:

> Hệ thống chưa có một shot lifecycle rõ ràng giữa local predicted shot, server accepted shot, server rejected shot và HUD presentation.

Đây là lý do có thể nhìn thấy muzzle/đạn/recoil nhưng không thấy ammo giảm ổn định.

---

# 3. Không thấy lỗ đạn

Đây là lỗi đã xác nhận khá rõ từ asset/prefab.

## Visual bullet và damage là hai hệ thống độc lập

Damage server dùng raycast/authoritative fire. Visual bullet được spawn riêng:

```155:158:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
SpawnVisualBullet(spawnPos, shootDirection);
cachedFireHandler?.RequestFireServerRpc(new FireCommand
{
```

Visual bullet prefab `Bullet.prefab` có:

- Rigidbody
- MeshCollider
- `Bullet` MonoBehaviour
- `bulletHolePrefab`

```104:116:Assets/FPS/Features/Weapons/Content/Bullet/Prefabs/Bullet.prefab
m_EditorClassIdentifier: 
ShowTopMostFoldoutHeaderGroup: 1
bulletHolePrefab: {fileID: 4141317489459417463, guid: ba6ff69759a8e59468940736e0e069b4, type: 3}
```

Nhưng `Bullet` chỉ tạo lỗ đạn trong `OnCollisionEnter`:

```1:28:Assets/FPS/Features/Weapons/Runtime/Bullet.cs
public class Bullet : MonoBehaviour
{
    [SerializeField] private GameObject bulletHolePrefab;
    [SerializeField] private LayerMask bulletHoleLayer;

    private void OnCollisionEnter(Collision collision)
    {
        if (((1 << collision.gameObject.layer) & bulletHoleLayer) != 0)
            CreateBulletHole(collision);

        Destroy(gameObject);
    }
```

## `bulletHoleLayer` đang bị để bằng 0

Trong prefab:

```126:150:Assets/FPS/Features/Weapons/Content/Bullet/Prefabs/Bullet.prefab
m_IncludeLayers:
  serializedVersion: 2
  m_Bits: 0
...
m_ExcludeLayers:
  serializedVersion: 2
  m_Bits: 0
```

Không thấy serialized field `bulletHoleLayer` được thiết lập với layer nào. Giá trị mặc định của `LayerMask` là 0.

Điều kiện:

```csharp
((1 << collision.gameObject.layer) & bulletHoleLayer) != 0
```

với `bulletHoleLayer = 0` luôn trả về `false`.

Do đó:

> `CreateBulletHole()` gần như chắc chắn không bao giờ được gọi từ prefab hiện tại.

Đây là nguyên nhân trực tiếp khiến không thấy lỗ đạn.

## Có thêm vấn đề với pool

`Bullet.OnCollisionEnter()` luôn gọi:

```csharp
Destroy(gameObject);
```

Trong khi AKM có `BulletPool`:

```224:226:Assets/FPS/Features/Weapons/Content/AKM/Prefabs/AKM.prefab
m_EditorClassIdentifier: FPS::ObjectPooling
```

Nếu bullet được lấy từ pool mà bị `Destroy()` trực tiếp, pool có thể:

- Mất object khỏi pool.
- Không trả object đúng lifecycle.
- Không reset state.
- Tạo inconsistency giữa pooled và instantiated bullet.

Ngoài ra, `CreateBulletHole()` lại dùng `Instantiate`, không pooling. Khi auto-fire bắn liên tục, nếu lỗ đạn hoạt động sẽ có allocation liên tục.

## Quan trọng hơn: Bullet physics có thể không collision đúng

Projectile visual được spawn từ muzzle và Rigidbody có gravity:

```119:140:Assets/FPS/Features/Weapons/Content/Bullet/Prefabs/Bullet.prefab
m_UseGravity: 1
m_IsKinematic: 0
m_Interpolate: 0
m_CollisionDetection: 0
```

Vấn đề:

- `bulletSpeed = 200`, nhưng chưa thấy trong prefab đoạn này xác nhận velocity được thiết lập đúng.
- Collision detection đang là `Discrete`.
- Projectile nhanh, nhỏ, có thể xuyên collider giữa các physics frame.
- Damage vẫn có thể xảy ra do server raycast, nhưng visual projectile không va chạm và không tạo hole.
- Nếu visual bullet bay 2 giây mà raycast đã hit ngay, presentation không phản ánh hit thật.

## Đánh giá

Hệ thống hiện tại không nên dùng visual Rigidbody projectile để quyết định bullet hole. Cần một hit presentation event từ raycast:

- Raycast xác định `RaycastHit.point`, `normal`, collider.
- Client/server gửi kết quả hit presentation.
- Spawn decal tại hit point.
- Bullet hole được pool.
- Projectile chỉ là tracer visual, không phải hệ thống collision chính.

---

# 4. AKM recoil lặp viên cuối, không đổi trái/phải

Đây là lỗi đã xác nhận trực tiếp trong code.

Pattern AKM chỉ có 5 phần tử:

```7:18:Assets/FPS/Features/Weapons/Content/AKM/Data/AKM.asset
shots:
- {x: 1.5, y: 0}
- {x: 1.5, y: -0.2}
- {x: 2, y: 0.5}
- {x: 1, y: 1}
- {x: 0.5, y: 1.5}
```

Trong recoil controller:

```37:44:Assets/FPS/Features/Weapons/Runtime/RecoilController.cs
int index = Mathf.Min(currentShotIndex, pattern.shots.Length - 1);

// Add the current shot to the pattern array (cap at the last bullet)
targetRecoil += pattern.shots[index];

currentShotIndex++;
timeSinceLastShot = 0f;
```

`Mathf.Min(currentShotIndex, pattern.shots.Length - 1)` nghĩa là:

- Shot 0 → pattern 0
- Shot 1 → pattern 1
- Shot 2 → pattern 2
- Shot 3 → pattern 3
- Shot 4 → pattern 4
- Shot 5 trở đi → pattern 4
- Shot 6 trở đi → pattern 4
- Shot 20 trở đi → pattern 4

Do đó nhận xét của bạn là hoàn toàn chính xác:

> AKM không có chuỗi recoil hoàn chỉnh. Từ viên thứ 6 trở đi nó chỉ lặp vector `{x: 0.5, y: 1.5}`.

## Vấn đề thứ hai: pattern hiện chưa thể hiện rõ trái-phải liên tục

Các giá trị yaw hiện tại:

```text
0
-0.2
0.5
1
1.5
```

Đây gần như là xu hướng tăng dần về một phía, không phải pattern trái/phải rõ ràng.

Valorant-like rifle recoil cần phân biệt:

- Vertical kick
- Horizontal recoil pattern
- Direction change
- Pattern index theo từng viên
- Recovery
- Accuracy/spread
- Spray reset
- Spray transfer behavior

Hiện tại chỉ có 5 điểm, không đủ cho rifle magazine 25 hoặc 30 viên.

## Còn một vấn đề về cách áp dụng pattern

Pattern đang được cộng vào `targetRecoil`:

```45:50:Assets/FPS/Features/Weapons/Runtime/RecoilController.cs
public void Fire(RecoilPattern pattern)
{
    if (pattern == null || pattern.shots == null || pattern.shots.Length == 0) return;

    currentPattern = pattern;

    int index = Mathf.Min(currentShotIndex, pattern.shots.Length - 1);
    targetRecoil += pattern.shots[index];

    currentShotIndex++;
    timeSinceLastShot = 0f;
}
```

Nếu `targetRecoil` là cumulative recoil, pattern data nên đại diện cho từng **increment**. Nếu pattern data lại được tạo theo góc tuyệt đối, việc cộng dồn sẽ sai. Hiện asset đang có vẻ là increment, nhưng cần thống nhất rõ.

## Reset cooldown hiện tại

```18:35:Assets/FPS/Features/Weapons/Runtime/RecoilController.cs
if (timeSinceLastShot > currentPattern.resetCooldown)
{
    currentShotIndex = 0;
    targetRecoil = Vector2.Lerp(targetRecoil, Vector2.zero, currentPattern.returnSpeed * Time.deltaTime);
}
```

Pattern reset sau `0.25s`. AKM fire interval là `0.1s`, nên:

- Ngắt bắn khoảng 3 viên thì có thể reset.
- Tap/burst cách nhau trên 250 ms sẽ bắt đầu lại từ đầu.
- Điều này có thể đúng hoặc không tùy design, nhưng nên đo bằng test.

---

# 5. Camera đã được điều chỉnh theo Valorant và điều khiển spray

Tôi đã kiểm tra lại hệ thống camera. Hướng hiện tại là **đúng cho mouse FPS competitive**, nhưng chưa thể nói toàn bộ camera/recoil đã đạt chuẩn Valorant.

## Điểm tốt

Mouse dùng raw axis:

```130:143:Assets/FPS/Features/Characters/Player/Runtime/MouseMovement.cs
float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

xRotation -= mouseY;
xRotation = Mathf.Clamp(xRotation, minRotationX, maxRotationX);

yRotation += mouseX;

bodyCam.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
transform.localRotation = Quaternion.Euler(0, yRotation, 0f);
```

Không có smoothing hoặc `deltaTime` trên mouse movement. Đây là điều kiện tốt để người chơi control spray.

Recoil được áp dụng trực tiếp vào camera:

```146:157:Assets/FPS/Features/Characters/Player/Runtime/MouseMovement.cs
public void ApplyRecoil(float pitchOffset, float yawOffset)
{
    if (!IsOwner) return;
    
    xRotation -= pitchOffset;
    xRotation = Mathf.Clamp(xRotation, minRotationX, maxRotationX);

    yRotation += yawOffset;
}
```

Điều này cho phép người chơi kéo chuột ngược recoil, phù hợp với spray control.

## Nhưng camera hiện có một số vấn đề

### 5.1 Recoil và mouse input cùng ghi vào một state

Cả mouse input và recoil đều sửa:

- `xRotation`
- `yRotation`

Điều này làm recoil trở thành camera aim offset thật, nhưng không có baseline riêng. Hậu quả có thể là:

- Recoil recovery không khớp với vị trí camera mà người chơi đã kéo chuột.
- Người chơi kéo chuột để bù recoil nhưng code return recoil không phân biệt phần nào do user input.
- Pattern control khó đo và khó reset chính xác.
- Behavior phụ thuộc thứ tự `Update()` giữa `MouseMovement` và `RecoilController`.

### 5.2 Không có tách biệt giữa view kick và aim punch

Để control spray tốt, nên phân biệt:

- `UserLookRotation`: rotation do mouse.
- `RecoilAimRotation`: offset ảnh hưởng aim.
- `WeaponVisualKick`: offset chỉ làm súng/arms rung.
- `CameraImpulse`: rung camera nhẹ, không ảnh hưởng hướng đạn.

Hiện tại `ApplyRecoil` trộn trực tiếp recoil vào user rotation. Có thể chơi được, nhưng khó đạt cảm giác ổn định như Valorant.

### 5.3 Recoil pattern không tương ứng với camera/FOV calibration

Camera FOV là 60:

```134:134:Assets/FPS/Features/Characters/Content/Players/Player/Player.prefab
field of view: 60
```

FOV 60 không sai, nhưng recoil tính bằng độ cần được cảm nhận cùng:

- FOV
- sensitivity
- resolution
- aspect ratio
- ADS/zoom state
- mouse DPI

Hiện chưa thấy:

- ADS sensitivity multiplier
- scoped sensitivity
- recoil scale theo weapon state
- FOV change khi aim
- calibration test cho 360° turn distance

Vì vậy camera hiện đã có nền tảng để control spray, nhưng recoil asset quá ngắn và không đúng pattern nên người chơi chưa thể điều khiển spray theo kiểu Valorant.

---

# 6. Warning `FindObjectsSortMode` obsolete

Warning này có nguồn chính xác ở:

```51:59:Assets/FPS/Tests/EditMode/SettingsManagerTests.cs
private static void DestroyExistingSettingsManager()
{
    var managers = Object.FindObjectsByType<SettingsManager>(FindObjectsSortMode.None);
    foreach (var manager in managers)
    {
        Object.DestroyImmediate(manager.gameObject);
    }
}
```

Unity hiện tại cảnh báo overload truyền `FindObjectsSortMode` đã obsolete. Nên thay bằng overload không có sort mode, ví dụ:

```csharp
Object.FindObjectsByType<SettingsManager>();
```

Vì test không yêu cầu thứ tự, dùng overload không sort là phù hợp hơn và cũng tránh chi phí sort.

Đây là warning dễ sửa, mức **P2**, không ảnh hưởng runtime gameplay.

Lưu ý: trong `MouseMovement`, code đã dùng overload mới:

```85:96:Assets/FPS/Features/Characters/Player/Runtime/MouseMovement.cs
AudioListener[] listeners = FindObjectsByType<AudioListener>(
    FindObjectsInactive.Include);
```

Phần này không phải nguồn của warning được bạn đưa.

---

# 7. Warning Input Manager deprecated

Project hiện đang dùng legacy input ở nhiều nơi:

```110:143:Assets/FPS/Features/Characters/Player/Runtime/MouseMovement.cs
float mouseX = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
```

và:

```1:37:Assets/FPS/Features/Input/Runtime/InputManager.cs
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }
    private static bool menuInputBlocked;

    public static bool MatchInputBlocked { get; set; }
```

`InputManager` vẫn dùng:

- `Input.GetKey`
- `Input.GetKeyDown`
- `Input.GetAxisRaw`

Đây là deprecation warning của Unity, không phải lỗi logic hiện tại.

## Đánh giá

Không nên migrate Input System giữa lúc đang debug movement/fire mà không có kế hoạch, vì migration có thể làm thay đổi:

- sensitivity
- mouse delta scale
- button edge timing
- key rebinding
- input buffering
- network input sequence
- auto-fire hold behavior

Nên xem là một hạng mục riêng:

1. Thêm Input System package.
2. Tạo Input Actions cho gameplay.
3. Giữ abstraction `InputManager`.
4. Chuyển từng action qua backend mới.
5. Test song song legacy/new input.
6. Cuối cùng tắt legacy input.

Mức độ:

- **P2** đối với gameplay hiện tại.
- Có thể thành **P1** nếu target Unity tương lai loại bỏ legacy Input Manager.

---

# 8. Warning Coplay custom toolbar

Warning này không xuất phát từ code gameplay trong `Assets/FPS`.

Nó đến từ package/editor integration:

```text
Coplay.Coplay/<>c:<CoplayToolbarButton>b__30_0 ()
UnityEditor.EditorApplication:Internal_CallDelayFunctions ()
```

Trong project có package:

```text
Packages/Coplay/Editor/
```

Warning nói package đang thêm UI trực tiếp vào Unity main toolbar bằng unsupported API.

## Đánh giá

- Không ảnh hưởng movement.
- Không ảnh hưởng shooting.
- Không ảnh hưởng runtime build nếu editor code được loại khỏi player.
- Có thể gây lỗi tương thích với Unity 6.5/6000.5 trở lên.
- Đây là vấn đề của Coplay package/editor extension.

Hướng xử lý:

- Update Coplay lên phiên bản hỗ trợ `UnityEditor.Toolbars.MainToolbar`.
- Tắt/hide custom Coplay toolbar element bằng context menu.
- Nếu không cần Coplay, disable/remove editor integration.
- Không nên tự sửa file package nếu package được quản lý bên ngoài project.

Mức độ: **P2 editor maintenance**, không phải gameplay bug.

---

# 9. Tổng hợp bug mới theo mức độ

## P0 — cần xử lý trước khi đánh giá gameplay

### Jump spam/bunny-hop không kiểm soát

- Spam Space có thể nhảy lại ngay mỗi lần chạm đất.
- Chưa có state transition hoặc anti-repeat policy rõ ràng.
- Cần test và sửa logic input/state.

### Bullet hole không xuất hiện

- `bulletHoleLayer` trong `Bullet.prefab` đang bằng 0.
- `Bullet.OnCollisionEnter()` gần như không bao giờ gọi `CreateBulletHole()`.
- Ngoài ra bullet physics đang tách khỏi authoritative raycast.

### Local fire/HUD/server state không có lifecycle thống nhất

- Có thể thấy nhiều viên, muzzle/recoil nhưng ammo không phản ánh rõ.
- Cần kiểm tra state overwrite và fire reject path.

## P1 — ảnh hưởng trực tiếp tới Valorant-like feeling

### Recoil AKM không hoàn chỉnh

- Chỉ có 5 phần tử.
- Từ shot 6 trở đi lặp shot cuối.
- Horizontal direction không có chuỗi trái-phải hợp lý.
- Không đủ cho 25 viên magazine.

### Auto-fire timing chưa ổn định

- Dùng coroutine `WaitForSeconds`.
- Có khả năng lệch cadence với server.
- Nên dùng fire timer/accumulator.
- Cần phân biệt local predicted shot và accepted shot.

### Camera/recoil chưa tách user look khỏi recoil

- Vẫn điều khiển spray được.
- Nhưng recovery và compensation khó ổn định.
- Khó đảm bảo feeling giống nhau ở các FPS/sensitivity khác nhau.

### Chưa có bullet hole event từ raycast

- Visual projectile có thể không va chạm.
- Bullet hole không phản ánh hit thật.
- Cần dùng hit result từ raycast làm nguồn presentation.

## P2 — warning/maintenance

### `SettingsManagerTests.cs` dùng API obsolete

Dòng 54 cần chuyển sang overload không sort.

### Legacy Input Manager

Không hỏng gameplay ngay, nhưng cần có roadmap migration.

### Coplay toolbar

Warning từ editor package, không phải logic FPS runtime.

---

# 10. Cập nhật đánh giá tổng thể

Sau khi bổ sung các phát hiện mới:

| Hệ thống | Đánh giá mới |
|---|---:|
| Movement architecture | 7/10 |
| Network authority/security | 8/10 |
| Ground/jump gameplay | 4/10 |
| Auto-fire reliability | 4.5/10 |
| HUD ammo feedback | 4/10 |
| Bullet impact presentation | 2.5/10 |
| AKM recoil pattern | 3/10 |
| Camera raw input | 7/10 |
| Spray-control foundation | 5/10 |
| Valorant-like combat feel | 4/10 |
| Runtime optimization | 6.5/10 |
| Warning cleanliness | 5/10 |

## Kết luận

Những quan sát của bạn là chính xác:

- **Spam Space** đang là lỗi gameplay cần ưu tiên.
- **Auto-fire** có khả năng đang tạo shot visual/local không đồng nhất với ammo/server/HUD.
- **Bullet hole** có nguyên nhân asset rõ ràng: `bulletHoleLayer` đang không được cấu hình.
- **AKM recoil** đúng là chỉ lặp phần tử cuối sau shot thứ 5.
- **Camera** đã có nền tảng phù hợp để kéo chuột control spray, nhưng recoil system hiện tại chưa đủ hoàn chỉnh để biến nền tảng đó thành feeling Valorant.
- Hai warning Unity API và warning Input Manager là vấn đề kỹ thuật cần xử lý riêng.
- Coplay warning là editor package warning, không phải lỗi gameplay.

Tôi vẫn giữ đúng phạm vi yêu cầu: **mới kiểm tra và rà soát, chưa sửa bất cứ thứ gì**.

---

**User**

Mục 1: không phải chạm đất mà nhảy trên không vẫn space để tăng thêm độ cao được.
mục 2: có thể scene, code đang bị lỗi thì sao, hay do chuyển giữa các scene chứ tôi có cảm giác như không có fireRate?
Mục 3: lỗ đạn thì bỏ qua tạm thời, tôi chưa xem xét kỹ

---

**Cursor**

Tôi đã kiểm tra thêm luồng bắn và gating match-state. Có hai nguồn rất đáng nghi khiến bạn có cảm giác “không có fireRate”:

1. `WeaponFireHandler` có **anti-spam budget** phía server, ngoài fireRate cơ bản  
2. `NetworkMatchStateManager` sẽ chặn input/bắn ở các state `Loading`, `Warmup`, `GameOver`

Cụ thể:

```260:274:Assets/FPS/Features/Weapons/Runtime/WeaponFireHandler.cs
float interval = weaponData != null ? Mathf.Max(0.001f, weaponData.fireRate) : 0.001f;
int burstAllowance = weaponData != null ? Mathf.Max(2, weaponData.burstCount) : 2;
int allowedPerSecond = Mathf.Clamp(Mathf.CeilToInt(1f / interval) + burstAllowance,
    4, recentFireRequests.Length);
if (fireRequestCount >= allowedPerSecond)
    return false;
```

Câu này nghĩa là server không chỉ dựa vào `fireRate`, mà còn giới hạn số request trong 1 giây. Nếu client/coroutine/phản hồi scene làm request dồn cục, bạn sẽ thấy cảm giác bắn “không đều” hoặc “như mất fireRate”.

Ngoài ra, bắn còn bị chặn bởi match state:

```67:90:Assets/FPS/Features/Networking/Runtime/NetworkMatchStateManager.cs
private void ApplyLocalInputBlock()
{
    InputManager.MatchInputBlocked =
        State == NetworkMatchState.Loading
        || State == NetworkMatchState.Warmup
        || State == NetworkMatchState.GameOver;
}
```

và:

```53:54:Assets/FPS/Features/Networking/Runtime/NetworkMatchStateManager.cs
public static bool IsGameplayActive => Instance == null || Instance.State == NetworkMatchState.Playing;
public static bool IsGameplayBlocked => !IsGameplayActive;
```

Nên nếu chuyển scene làm `NetworkMatchStateManager` hoặc `InputManager` bị reset/không đồng bộ, bắn có thể bị chặn một cách “im lặng”.

Mình đang tiếp tục rà soát singleton/scene dependency để xác định xem đây là bug state, bug scene, hay do timing/cooldown thật.

Đã xác nhận thêm: phần `Weapon` itself không có lỗi `fireRate` trực tiếp, nhưng cảm giác “không có fireRate” vẫn có thể xảy ra do **gating theo state** hoặc **request bị dồn/cắt** phía server.

`Weapon.HandleFire()` còn chặn hoàn toàn nếu match chưa ở trạng thái active:

```103:118:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
if (!NetworkMatchStateManager.IsGameplayActive) return;
```

Và `WeaponFireHandler.TryProcessRuntimeFire()` còn reject theo các điều kiện này trước cả khi trừ ammo server:

```130:174:Assets/FPS/Features/Weapons/Runtime/WeaponFireHandler.cs
if (weapon == null || weapon.Data == null || command.weaponSlot != currentSlot)
{
    PublishOwnerState(FireRejectReason.WrongWeapon, command.sequence, GetServerTick());
    return false;
}

if (!TryConsumeFireRequestBudget(weapon.Data, now))
{
    PublishOwnerState(FireRejectReason.CooldownOrAmmo, command.sequence, GetServerTick());
    return false;
}

if (!NetworkMatchStateManager.IsGameplayActive)
{
    PublishOwnerState(FireRejectReason.MatchBlocked, command.sequence, GetServerTick());
    return false;
}
```

Vì vậy, nếu cảm giác bắn “không có fireRate” xuất hiện sau chuyển scene, rất có thể là một trong các nguyên nhân sau:
- `NetworkMatchStateManager` chưa về `Playing`
- `InputManager` bị block bởi menu/warmup/loading
- `Weapon`/`WeaponFireHandler` bị enable-disable lại khi scene change làm `canShoot`, coroutine hoặc owner state reset
- `WeaponFireHandler` request budget đang reject do dồn request
- `Weapon` đang chờ `TryGetConfirmedFireReference()` nên click bắn bị nuốt
- scene mới có prefab instance khác, khiến weapon data/cached refs bị khác

Tôi sẽ tiếp tục rà thêm singleton/scene changes để trả lời dứt điểm hơn.

Đúng, nếu bạn muốn bỏ qua lỗ đạn thì mình sẽ không đào sâu phần đó nữa.

Về 2 điểm còn lại, mình đã xác nhận có vài nguyên nhân rất khả dĩ hơn là “fireRate bị mất”:

1. **`Weapon` đang chặn bắn theo match state**
- `HandleFire()` thoát ngay nếu `NetworkMatchStateManager.IsGameplayActive` chưa là `Playing`.
- Khi chuyển scene, nếu state manager chưa kịp về `Playing`, bắn sẽ bị chặn im lặng.

2. **`WeaponFireHandler` có thêm anti-spam budget**
- Không chỉ fireRate, server còn giới hạn số request trong 1 giây.
- Nếu request bị dồn sau scene switch, hoặc timing lệch giữa client/server, bạn sẽ thấy cảm giác bắn “không đều”.

3. **`FireBullet()` còn phụ thuộc vào confirmed fire reference**
- Nếu `TryGetConfirmedFireReference()` chưa có dữ liệu, shot bị nuốt.
- Đây là nguyên nhân rất dễ tạo cảm giác “không có fireRate”, đặc biệt ngay sau spawn hoặc sau scene change.

4. **Weapon/scene lifecycle có thể reset trạng thái**
- `OnEnable()` đặt lại `canShoot = weaponData != null`.
- `OnDisable()` dùng `StopAllCoroutines()`.
- Nếu scene chuyển làm weapon bị disable/enable lại, cooldown coroutine có thể bị cắt giữa chừng.
- Nếu player/object sống qua scene nhưng weapon object bị tái tạo, local ammo/canShoot/pending refs cũng có thể lệch.

Đoạn code liên quan:

```79:95:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
private void OnEnable()
{
    canShoot    = weaponData != null;
    isReloading = false;
    burstCoroutine = null;

    if (isOwner)
        CacheOwnerDependencies();
}

private void OnDisable()
{
    StopAllCoroutines();
    isReloading = false;
    canShoot    = true;
    ReportCombatTelemetry();
}
```

Và:

```111:178:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
private void HandleFire()
{
    if (weaponData == null) return;
    if (isReloading) return;
    if (!NetworkMatchStateManager.IsGameplayActive) return;
    ...
    switch (weaponData.fireMode)
    {
        case FireMode.Single:
            if (InputManager.Instance != null && InputManager.Instance.GetFireInputDown() && canShoot)
                StartCoroutine(ShootCooldown());
            break;

        case FireMode.Auto:
            if (InputManager.Instance != null && InputManager.Instance.GetFireInput() && canShoot)
                StartCoroutine(ShootCooldown());
            break;
```

Mình nói ngắn gọn theo đúng 3 mục bạn hỏi:

- **Mục 1**: Đúng, hiện tại bạn có thể spam Space để nhảy liên tục trên không nếu timing/input/state cho phép. Đây không chỉ là “chạm đất rồi nhảy lại”, mà là một lỗi/thiếu policy về jump state.
- **Mục 2**: Có khả năng rất cao là do **scene/state lifecycle + match gating + confirmed fire reference + server request budget**, chứ không phải chỉ do `fireRate` thuần túy. Cảm giác “không có fireRate” là hợp lý với code hiện tại.
- **Mục 3**: Mình sẽ bỏ qua theo yêu cầu.

Nếu bạn muốn, bước tiếp theo mình sẽ chỉ tập trung **khoanh vùng nguyên nhân mục 2 theo scene/singleton/lifecycle** để kết luận rõ hơn chỗ nào đang làm bắn bị nuốt hoặc bị chặn.

Đúng, hai điểm bạn nói là hợp lý.

## Mục 1
Đúng — hiện tại không chỉ là “chạm đất rồi nhảy lại”, mà **có thể nhấn Space trên không để tích thêm độ cao theo timing/state** nếu input queue và grounded transition ăn khớp.  
Nói cách khác, đây là **lỗi/thiếu policy về jump state**, không phải chỉ chỉnh `jumpHeight` là xong.

## Mục 2
Có khả năng rất cao là **không phải fireRate bị mất**, mà là bị **gating / lifecycle / scene state** làm bạn cảm giác như không có fireRate.

Mình đã rà lại và thấy các nguồn nghi ngờ mạnh nhất:

### 1) Bị chặn theo match state
`Weapon.HandleFire()` dừng ngay nếu game chưa ở `Playing`.

```111:118:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
private void HandleFire()
{
    if (weaponData == null) return;
    if (isReloading) return;
    if (!NetworkMatchStateManager.IsGameplayActive) return;
```

Nếu chuyển scene làm `NetworkMatchStateManager` chưa kịp sync về `Playing`, bắn sẽ bị chặn im lặng.

### 2) Có anti-spam budget ở server ngoài fireRate
Server không chỉ check fireRate, mà còn giới hạn số request trong 1 giây:

```260:274:Assets/FPS/Features/Weapons/Runtime/WeaponFireHandler.cs
float interval = weaponData != null ? Mathf.Max(0.001f, weaponData.fireRate) : 0.001f;
int burstAllowance = weaponData != null ? Mathf.Max(2, weaponData.burstCount) : 2;
int allowedPerSecond = Mathf.Clamp(Mathf.CeilToInt(1f / interval) + burstAllowance,
    4, recentFireRequests.Length);
if (fireRequestCount >= allowedPerSecond)
    return false;
```

Nếu request dồn sau scene switch hoặc timing lệch, cảm giác sẽ như “fireRate không hoạt động”.

### 3) Shot bị nuốt nếu chưa có confirmed fire reference
Trong `FireBullet()`, nếu không lấy được confirmed reference thì shot return luôn:

```181:198:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
private void FireBullet()
{
    if (weaponData == null) return;
    if (currentAmmo <= 0) return;

    PlayerMovement movement = cachedFireHandler != null
        ? cachedFireHandler.GetComponent<PlayerMovement>()
        : null;
    if (movement == null || !movement.TryGetConfirmedFireReference(
            out uint inputSequence, out int serverTick))
    {
        return;
    }
```

Đây là nguồn rất dễ tạo cảm giác “bấm mà không ra đạn”, nhất là:
- ngay sau spawn
- vừa đổi scene
- sau khi object/network state reset
- khi prediction chưa sync

### 4) Scene/object disable-enable có thể cắt cooldown
`Weapon` reset trạng thái khi enable/disable:

```79:95:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
private void OnEnable()
{
    canShoot    = weaponData != null;
    isReloading = false;
    burstCoroutine = null;

    if (isOwner)
        CacheOwnerDependencies();
}

private void OnDisable()
{
    StopAllCoroutines();
    isReloading = false;
    canShoot    = true;
    ReportCombatTelemetry();
}
```

Nếu scene chuyển làm weapon bị disable/enable lại, coroutine fireRate bị cắt giữa chừng, và shot timing sẽ rất dễ bị lệch.

## Kết luận cho mục 2
Mình nghi nhiều hơn rằng:
- **scene/state lifecycle**
- **match gating**
- **confirmed fire reference**
- **server request budget**

đang tạo cảm giác “không có fireRate”, hơn là `fireRate` value bị sai.

Nói ngắn:
> `fireRate` có tồn tại, nhưng luồng bắn hiện tại đang có nhiều chốt chặn đủ để làm cảm giác bắn bị nuốt hoặc bị trễ.

## Mục 3
Mình sẽ **bỏ qua phần lỗ đạn** theo yêu cầu của bạn.

Nếu bạn muốn, bước tiếp theo mình có thể chỉ tập trung **khoanh vùng riêng mục 2 theo scene/singleton/state** để xác định chỗ nào đang làm bắn bị nuốt sau chuyển scene.

---

**User**

Dựa vào các vấn đề bạn liệt kê từ đầu, hãy chạy test, chạy code hay làm gì đó kiểm thử để định vị chính xác vị trí lỗi của từng mục

---

**Cursor**

Tôi sẽ kiểm thử trực tiếp theo ba lớp: test tự động hiện có, kiểm tra tĩnh các đường chạy fire/jump, rồi chạy Unity test nếu môi trường hiện tại cho phép. Tôi sẽ không sửa code; chỉ dùng test/log để phân biệt lỗi đã tái hiện được với giả thuyết.

Đã kiểm thử/rà soát lại theo hướng định vị lỗi. **Không chỉnh sửa file nào.**

Tuy nhiên cần nói rõ: máy hiện tại không có Unity Editor/Unity Hub executable khả dụng trong PATH hoặc các đường dẫn cài đặt chuẩn, nên tôi **không thể thực sự khởi chạy Unity Test Runner/PlayMode** từ môi trường này. Vì vậy kết quả gồm:

- Kiểm tra static chính xác trên code/prefab/asset.
- Đối chiếu với test hiện có.
- Xác định test hiện có đang kiểm tra gì và chưa kiểm tra gì.
- Phân biệt lỗi đã chứng minh được với lỗi vẫn cần chạy runtime.

---

# Kết quả chính

## Mục 1 — Nhấn Space giữa không trung làm tăng độ cao

### Kết luận: chưa thể xác nhận từ code hiện tại rằng giữ Space giữa không trung trực tiếp cộng thêm độ cao

Sau khi kiểm tra kỹ, logic mô phỏng thực tế là:

```894:902:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
isGrounded = CheckGrounded();

if (isGrounded && verticalVelocity < 0f)
    verticalVelocity = -2f;

if (input.jumpPressed && isGrounded)
    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * GRAVITY);

verticalVelocity += GRAVITY * gravityScale * dt;
```

Điều kiện bắt buộc là:

```csharp
input.jumpPressed && isGrounded
```

Do đó, theo static code:

- Nhấn Space khi đang thật sự airborne không trực tiếp cộng velocity.
- `jumpPressed` không được xử lý trong nhánh airborne.
- Không có `AddForce`, `verticalVelocity += jumpForce` hoặc logic double jump rõ ràng.
- Không có biến `jumpCount`, `airJumpCount`, `canDoubleJump` hay tương tự.

Input jump cũng được tiêu thụ sau đúng một tick:

```285:297:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
var input = new PlayerInputPayload
{
    sequence = unchecked(++localCommandSequence),
    tick = tick,
    move = cachedMove,
    jumpPressed = jumpQueued,
    sprint = sprintHeld,
    yaw = cachedYaw,
    pitch = cachedPitch
};

jumpQueued = false;
return input;
```

Vì vậy, giả thuyết “giữ Space trên không cộng thêm độ cao trực tiếp” **không được chứng minh bởi code này**.

## Nhưng có một lỗi có thể tạo cảm giác tương tự

`CheckGrounded()` sử dụng fallback rất rộng:

```917:941:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
private bool CheckGrounded()
{
    if (controller != null && controller.isGrounded)
    {
        return true;
    }

    if (groundCheck == null)
    {
        return false;
    }

    if (groundMask.value != 0
        && Physics.CheckSphere(groundCheck.position, 0.4f, groundMask, QueryTriggerInteraction.Ignore))
    {
        return true;
    }

    return Physics.CheckSphere(groundCheck.position, 0.4f, ~0, QueryTriggerInteraction.Ignore);
}
```

Fallback `~0` kiểm tra toàn bộ layer. Nếu sphere dưới chân chạm vào:

- collider player khác
- collider phụ của map
- object gameplay
- collider không phải ground
- vật thể nhỏ dưới chân

thì player có thể bị coi là grounded trong lúc vẫn đang rơi hoặc đang ở trạng thái gần mặt đất. Khi đó nhấn Space sẽ được chấp nhận và tạo cảm giác:

> đang ở trên không nhưng Space làm nhảy cao thêm.

### Vị trí lỗi khả nghi nhất cho mục 1

```941:941:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
return Physics.CheckSphere(groundCheck.position, 0.4f, ~0, QueryTriggerInteraction.Ignore);
```

Ngoài ra prefab có ground check sphere radius `0.4`, trong khi CharacterController radius là `0.5`:

```18519:18523:Assets/FPS/Features/Characters/Content/Players/Player/Player.prefab
m_Height: 2
m_Radius: 0.5
m_SlopeLimit: 45
m_StepOffset: 0.3
m_SkinWidth: 0.08
```

và GroundCheck:

```2355:2371:Assets/FPS/Features/Characters/Content/Players/Player/Player.prefab
m_Name: GroundCheck
...
m_LocalPosition: {x: 0, y: -0.09, z: 0}
```

### Kết luận mục 1

- **Không tìm thấy double jump trực tiếp.**
- **Có lỗi grounded detection có thể xác nhận bằng runtime.**
- Vị trí nghi ngờ chính: `CheckGrounded()` fallback `~0`.
- Test hiện có chỉ test nhân vật di chuyển và input NaN, chưa test jump/grounded.

---

# Mục 2 — Cảm giác fireRate không tồn tại

## 2.1 FireRate trong asset có tồn tại

Ví dụ AKM:

```7:22:Assets/FPS/Features/Weapons/Data/ScriptableObjects/WeaponData/AKM.asset
weaponName: AKM
damage: 100
bulletSpeed: 200
fireRate: 0.1
bulletLiveTime: 2
burstCount: 3
fireMode: 2
magazineSize: 25
totalAmmo: 900
reloadTime: 1.5
```

`fireRate = 0.1` nghĩa là khoảng 10 phát/giây.

Pistol:

```7:22:Assets/FPS/Features/Weapons/Data/ScriptableObjects/WeaponData/Pistol.asset
weaponName: Pistol
damage: 75
fireRate: 0.15
magazineSize: 12
reloadTime: 1.5
```

Classic:

```7:22:Assets/FPS/Features/Weapons/Content/Classic/Classic.asset
weaponName: Classic
damage: 20
fireRate: 0.18
magazineSize: 12
reloadTime: 1.8
```

Vì vậy **không phải thiếu field `fireRate` trong weapon data**.

---

## 2.2 FireRate local đang được áp dụng bằng coroutine

```148:178:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
private IEnumerator ShootCooldown()
{
    if (weaponData == null)
        yield break;

    canShoot = false;
    FireBullet();
    yield return new WaitForSeconds(weaponData.fireRate);
    canShoot = true;
}
```

Với Auto:

```131:136:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
case FireMode.Auto:
    if (InputManager.Instance != null && InputManager.Instance.GetFireInput() && canShoot)
        StartCoroutine(ShootCooldown());
    break;
```

Static logic cho thấy fireRate có tác dụng ở client:

- Bắn một viên.
- `canShoot = false`.
- Chờ `weaponData.fireRate`.
- Cho phép bắn lại.

Vì vậy nếu test đúng weapon object, trong điều kiện scene ổn định, Auto không thể gọi `FireBullet()` ở mỗi frame.

---

## 2.3 Test hiện có xác nhận server fireRate

Test PlayMode hiện tại đã có case:

```37:54:Assets/FPS/Tests/PlayMode/CoreGameplayPlayModeIntegrationTests.cs
weaponData.fireRate = 10f;
weaponData.reloadTime = 0f;

fireHandler.InitializeServerWeaponStateForTests(3, 0);

Assert.IsTrue(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward));
Assert.IsFalse(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward),
    "Runtime weapon fire should reject shots before server fire-rate cooldown.");
Assert.AreEqual(2, fireHandler.ServerMagazineAmmo);
```

Test này chứng minh:

- Lần bắn đầu được chấp nhận.
- Lần thứ hai ngay lập tức bị server từ chối.
- Server có fire-rate cooldown.

Ngoài ra EditMode cũng có test tương tự:

```57:77:Assets/FPS/Tests/EditMode/WeaponSecurityTests.cs
data.fireRate = 10f;

fireHandler.InitializeServerWeaponStateForTests(3, 0);

Assert.IsTrue(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward));
Assert.IsFalse(fireHandler.ProcessFireServerForTests(Vector3.zero, Vector3.forward));

Assert.AreEqual(2, fireHandler.ServerMagazineAmmo,
    "Server must consume ammo only for the first shot when the second shot violates fire-rate.");
```

### Kết luận quan trọng

Test hiện có đã chứng minh:

> Server-side fireRate hoạt động trong unit/integration seam.

Nhưng test chưa chứng minh:

> Client Auto input, scene switching, local coroutine, HUD ammo và server acknowledgement hoạt động đúng cùng nhau.

---

## 2.4 Fire có thêm một lớp budget

```260:274:Assets/FPS/Features/Weapons/Runtime/WeaponFireHandler.cs
float interval = weaponData != null ? Mathf.Max(0.001f, weaponData.fireRate) : 0.001f;
int burstAllowance = weaponData != null ? Mathf.Max(2, weaponData.burstCount) : 2;
int allowedPerSecond = Mathf.Clamp(Mathf.CeilToInt(1f / interval) + burstAllowance,
    4, recentFireRequests.Length);
if (fireRequestCount >= allowedPerSecond)
    return false;
```

Với AKM:

- `fireRate = 0.1`
- `ceil(1 / 0.1) = 10`
- `burstCount = 3`
- `allowedPerSecond = 13`

Điều này không làm fire nhanh hơn. Nó chỉ cho phép tối đa 13 request trong rolling 1-second window.

Do đó anti-spam budget **không phải nguyên nhân trực tiếp khiến fireRate biến mất** trong điều kiện bình thường. Nó chỉ có thể gây reject nếu request bị dồn bất thường hoặc client gửi nhiều request sai.

---

## 2.5 Có nhiều đường return làm shot bị nuốt

Local `FireBullet()` có các điều kiện return:

```181:198:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
private void FireBullet()
{
    if (weaponData == null) return;
    if (currentAmmo <= 0) return;

    PlayerMovement movement = cachedFireHandler != null
        ? cachedFireHandler.GetComponent<PlayerMovement>()
        : null;
    if (movement == null || !movement.TryGetConfirmedFireReference(
            out uint inputSequence, out int serverTick))
    {
        return;
    }
```

Đây là vị trí quan trọng nhất đối với cảm giác “bắn không đúng fireRate”.

Nếu `TryGetConfirmedFireReference()` fail:

- `ShootCooldown()` vẫn đã set `canShoot = false`.
- `FireBullet()` không trừ ammo.
- Không spawn visual bullet.
- Không gửi `RequestFireServerRpc`.
- Sau `fireRate`, `canShoot` mới trở lại true.

Người chơi sẽ thấy:

- Bấm fire nhưng không có shot.
- Có khoảng trễ đúng bằng `fireRate`.
- Có thể tưởng rằng fireRate không hoạt động hoặc bắn bị random.

`TryGetConfirmedFireReference()` chỉ trả true khi:

```449:454:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
public bool TryGetConfirmedFireReference(out uint inputSequence, out int inputTick)
{
    inputSequence = confirmedFireInputSequence;
    inputTick = confirmedFireTick;
    return hasConfirmedFireReference;
}
```

Mà `hasConfirmedFireReference` được set trong `ClientOwnerTick()` chỉ ở nhánh host/server:

```324:331:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
if (IsServer)
{
    RecordServerAim(input, tick);
    confirmedFireInputSequence = input.sequence;
    confirmedFireTick = tick;
    hasConfirmedFireReference = true;
}
```

Ở client không phải host, confirmed reference được set ở đường server input processing:

```529:529:Assets/FPS/Features/Characters/Player/Runtime/PlayerMovement.cs
transform.rotation = Quaternion.Euler(0f, input.yaw, 0f);
SimulateTick(input, TICK_DT);
```

Cần đọc tiếp phần sau của `ServerTick()`/state RPC để xác định thời điểm client nhận confirmed reference. Nhưng static hiện tại đã xác nhận đây là dependency network, không phải local-only fire.

### Vị trí lỗi P0/P1 cho mục 2

```148:178:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
ShootCooldown()
```

kết hợp với:

```181:198:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
FireBullet()
```

Cụ thể: coroutine khóa `canShoot` trước khi biết shot có đủ fire reference hay không.

---

## 2.6 Scene switching có thể cắt coroutine

Đã kiểm tra lifecycle:

```79:95:Assets/FPS/Features/Weapons/Runtime/Weapon.cs
private void OnEnable()
{
    canShoot    = weaponData != null;
    isReloading = false;
    burstCoroutine = null;

    if (isOwner)
        CacheOwnerDependencies();
}

private void OnDisable()
{
    StopAllCoroutines();
    isReloading = false;
    canShoot = true;
    ReportCombatTelemetry();
}
```

Nếu weapon object bị disable khi chuyển scene:

- `StopAllCoroutines()` cắt `ShootCooldown()`.
- `canShoot` bị reset.
- `isReloading` bị reset.
- `cachedCamera`, `cachedFireHandler`, `cachedWeaponManager` có thể trỏ object cũ nếu player/weapon được tái tạo.

Tuy nhiên, **chưa thể khẳng định scene switching là nguyên nhân chính**, bởi chưa chạy được PlayMode scene test do thiếu Unity executable.

Test scene hiện có chỉ load tuần tự:

```17:33:Assets/FPS/Tests/PlayMode/GameScenePlayModeSmokeTests.cs
public IEnumerator BuildSettingsScenes_LoadInPlayModeWithoutRuntimeErrors()
{
    yield return LoadAndAssertScene("Assets/FPS/Scenes/MainMenu.unity", "MainMenu");
    yield return LoadAndAssertScene("Assets/FPS/Scenes/LobbyScene.unity", "LobbyScene");
    yield return LoadAndAssertScene("Assets/FPS/Scenes/GameScene.unity", "GameScene");
```

Test này không kiểm tra:

- fire trước/sau scene switch
- weapon enable/disable
- match state sau scene switch
- `InputManager.MatchInputBlocked`
- `NetworkMatchStateManager.Instance`
- fire reference sau spawn
- ammo HUD sau scene switch

### Kết luận scene

- Có đường code đủ để scene switch ảnh hưởng bắn.
- Nhưng hiện mới là **nguyên nhân có khả năng cao**, chưa phải lỗi đã runtime-reproduce.
- Cần test PlayMode thật với scene sequence và network spawn.

---

# Mục 3 — Bỏ qua lỗ đạn

Theo yêu cầu của bạn, tôi không tính phần bullet hole vào kết luận ưu tiên lần này.

---

# Recoil/AKM — đã định vị chính xác bằng static check

AKM asset chỉ có 5 recoil entries:

```7:18:Assets/FPS/Features/Weapons/Content/AKM/Data/AKM.asset
shots:
- {x: 1.5, y: 0}
- {x: 1.5, y: -0.2}
- {x: 2, y: 0.5}
- {x: 1, y: 1}
- {x: 0.5, y: 1.5}
```

Controller clamp index:

```37:44:Assets/FPS/Features/Weapons/Runtime/RecoilController.cs
int index = Mathf.Min(currentShotIndex, pattern.shots.Length - 1);
targetRecoil += pattern.shots[index];
currentShotIndex++;
timeSinceLastShot = 0f;
```

Kết luận này là **đã xác nhận chính xác**, không cần PlayMode để chứng minh:

- Shot 1–5 dùng các phần tử 1–5.
- Shot 6 trở đi luôn dùng phần tử thứ 5.
- Phần ngang từ shot 6 trở đi luôn cộng `+1.5`.
- Không có pattern trái/phải tiếp tục.

Ngoài ra phần lớn weapon data chưa có recoil pattern:

```7:22:Assets/FPS/Features/Weapons/Content/Classic/Classic.asset
recoilPattern: {fileID: 0}
```

```7:22:Assets/FPS/Features/Weapons/Content/Vandal/Vandal.asset
recoilPattern: {fileID: 0}
```

Đây là lỗi data/config đã xác nhận.

---

# Bảng kết quả kiểm thử

| Mục | Kết quả | Mức chắc chắn | Vị trí |
|---|---|---:|---|
| Space airborne trực tiếp cộng jump velocity | Chưa chứng minh; code hiện không có double jump rõ ràng | Cao về static, thấp về runtime | `SimulateTick()` |
| Grounded sai do collider/layer | Có khả năng rõ ràng | Cao | `CheckGrounded()` dòng 941 |
| Server fireRate | Đã có test xác nhận hoạt động | Cao | `WeaponServerState.TryConsumeFire()` |
| Client fireRate | Có coroutine áp dụng | Cao | `Weapon.ShootCooldown()` |
| Shot bị nuốt do thiếu confirmed ref | Có đường code xác nhận | Cao | `FireBullet()` |
| Scene switch cắt cooldown | Có thể xảy ra theo lifecycle | Trung bình | `OnDisable()` |
| Match state chặn input | Đã xác nhận trong code | Cao | `NetworkMatchStateManager` |
| Anti-spam budget làm mất fireRate | Không phải nguyên nhân bình thường | Trung bình-cao | `TryConsumeFireRequestBudget()` |
| AKM recoil lặp từ shot 6 | Đã chứng minh chính xác | Rất cao | `RecoilController` + `AKM.asset` |
| Lỗ đạn | Đã bỏ qua theo yêu cầu | — | — |

---

# Vì sao chưa thể nói “đã chạy test pass/fail hoàn chỉnh”

Các test hiện có đều nằm trong Unity Test Framework, nhưng môi trường hiện tại không có Unity Editor/Unity Hub executable để chạy:

- Không tìm thấy Unity process đang chạy.
- Không tìm thấy Unity executable ở đường dẫn version `6000.5.6f1`.
- Không tìm thấy `Unity.exe` trong PATH.
- Không thể gọi PlayMode Test Runner thật.

Do đó:

- Các test fireRate hiện có được đọc và đối chiếu.
- Không thể báo giả rằng Unity PlayMode đã chạy pass.
- Scene switching và input timing vẫn cần runtime test trên máy có Unity Editor.

# Kết luận ưu tiên

## Đã định vị chắc chắn

1. **AKM recoil sai từ shot thứ 6 trở đi**  
   Nguyên nhân: asset chỉ có 5 entry + controller clamp phần tử cuối.

2. **Server fireRate đang hoạt động**  
   Test hiện có đã kiểm tra server reject shot thứ hai trong cooldown.

3. **Client shot có thể bị nuốt trước khi gửi lên server**  
   Nguyên nhân: `ShootCooldown()` khóa `canShoot` trước, còn `FireBullet()` có thể return vì thiếu confirmed fire reference.

4. **Match state có thể chặn toàn bộ input fire**  
   Nguyên nhân: `InputManager.MatchInputBlocked` và `IsGameplayActive`.

## Chưa thể khẳng định runtime

1. Space airborne có thực sự tạo double jump trong scene thật hay không.
2. Scene switch có phải nguyên nhân khiến fireRate mất cảm giác hay không.
3. HUD có bị ghi đè bởi authoritative weapon state sau mỗi shot hay không.
4. Có scene/prefab nào đang tham chiếu nhầm `WeaponData` hay không.

## Vị trí cần instrument/test đầu tiên

Nếu được chạy Unity, nên log theo cùng một shot:

```text
[Weapon] fire input received
[Weapon] canShoot before shot
[Weapon] fireRate
[Weapon] confirmed reference available
[Weapon] local FireBullet accepted/rejected
[WeaponFireHandler] server request received
[WeaponFireHandler] reject reason
[Weapon] ammo before/after
[WeaponOwnerState] authoritative ammo
[NetworkMatchStateManager] current state
[Weapon] OnEnable/OnDisable
```

Với jump:

```text
tick
verticalVelocity before
isGrounded
grounded collider/layer
jumpPressed
verticalVelocity after
transform.position.y
```

Những log này sẽ phân biệt ngay:

- double jump thật
- grounded false-positive
- shot bị local return
- shot bị server reject
- scene lifecycle reset
- HUD bị authoritative state ghi đè.

