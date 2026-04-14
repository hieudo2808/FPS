# Cẩm nang Dựng giao diện Main Menu (Ultra Detailed Guide)

Tài liệu này là cẩm nang cầm tay chỉ việc, hướng dẫn bạn từng cú Click chuột trong Unity Editor để xây dựng một Main Menu UI chuẩn eSports có Popup phân mốc hoàn hảo, dùng chung với Hệ thống `Unified Multiplayer` và `LobbyUI.cs`.

---

## BƯỚC 1: Cấu hình chuẩn cho CANVAS
1. Trong cửa sổ **Hierarchy**, Click chuột phải thả vào khoảng trống ➞ `UI` ➞ `Canvas`.
2. Đổi tên nó thành `LobbyCanvas`.
3. Nhìn sang **Inspector** của `LobbyCanvas`:
   - Tìm component `Canvas Scaler`.
   - Đổi `UI Scale Mode` thành **Scale With Screen Size**.
   - Đặt `Reference Resolution` là `X: 1920`, `Y: 1080`. (Giúp UI không bị méo trên các màn hình khác nhau).
4. Thêm component `LobbyUI.cs` vào chính cái `LobbyCanvas` này (Drag & drop script thả vào Inspector).

---

## BƯỚC 2: Cấu tạo cây gia phả UI (Hierarchy Tree)
Bây giờ, tạo lần lượt các bệ khung UI bằng cách Click đúp chuột phải vào `LobbyCanvas` ➞ `UI`. Hãy làm sao để Hierarchy của bạn trông y chang thế này:

```text
LobbyCanvas
 ├── Background (UI > Image)
 ├── MainPanel (UI > Empty Object / Hoặc Image trong suốt)
 │    ├── TitleText (UI > Text - TextMeshPro)
 │    ├── PlayBtn (UI > Button - TextMeshPro)
 │    ├── SettingsBtn (UI > Button - TextMeshPro)
 │    └── QuitBtn (UI > Button - TextMeshPro)
 │
 ├── PlayPopup (UI > Image) - SẼ LÀM MỜ PANEL NÀY
 │    └── Container (UI > Image) - KHUNG CỬA SỔ
 │         ├── Title (UI > Text - TextMeshPro)
 │         ├── CloseBtn (UI > Button - TextMeshPro)
 │         ├── HostMatchBtn (UI > Button - TextMeshPro)
 │         ├── JoinCodeInput (UI > Input Field - TextMeshPro)
 │         └── JoinMatchBtn (UI > Button - TextMeshPro)
 │
 ├── SettingsPopup (UI > Image) - SẼ LÀM MỜ PANEL NÀY
 │    └── Container (UI > Image)
 │         ├── Title (UI > Text - TextMeshPro)
 │         ├── PlayerNameInput (UI > Input Field - TextMeshPro)
 │         ├── SaveBtn (UI > Button - TextMeshPro)
 │         └── CloseBtn (UI > Button - TextMeshPro)
 │
 └── StatusText (UI > Text - TextMeshPro)
```

---

## BƯỚC 3: Mẹo Căn chỉnh (Anchoring) Giao diện

### Phân rã `MainPanel`
Nhóm UI đứng hiển thị đầu tiên cho người chơi.
1. Chọn `MainPanel` 👉 Chạm vào icon Ô vuông Anchors ở góc trên bên trái ô Rect Transform 👉 Giữ `Alt` (Windows) / `Option` (Mac) và chọn ô Dàn Đều Toàn Bộ màn hình (Stretch-Stretch dưới cùng góc bên phải).
2. Tương tự với Background. Đổi màu Color của Background tối lại hoặc thả ảnh Map cảnh nền xịn xò vào.
3. Kéo thả 3 nút Play, Settings, Quit nằm về cạnh Trái hoặc lùi tít sang Phải cho phong cách giống *Valorant*. Cầm từng nút, set phông chữ To lên, tô chữ in nghiêng (Italic).

### Phân rã Popups (`PlayPopup` & `SettingsPopup`)
Hai cửa sổ này đè cúp lên màn trước mặt của người chơi.
1. **Backdrop làm mờ:** 
   - Chọn đối tượng `PlayPopup`. Set Anchor tràn lưới `Stretch-Stretch` bao phủ toàn bộ vùng Game. 
   - Vào mục Color của Component Image, thiết lập: Red: 0, Green: 0, Blue: 0. Chỉnh thanh Alpha (A) mờ khoảng `180`. Tương tự làm y hệt vậy với Panel `SettingsPopup`.
2. **Khung thoại (Container):** 
   - Chọn `Container` nằm bên trong Popup. Set Anchor là Center-Middle (nằm lọt thỏm ngay giữa rốn của màn hình).
   - Đặt `Width = 600`, `Height = 400` (Khung hình chữ nhật bo góc). Tô màu Color Xám/Vàng tùy hỉ họa. 
   - Dàn xếp HostBtn, InputField, JoinBtn chui trọn vẹn vào đây.
3. **Nút Tắt (CloseBtn "X"):**
   - Đổi dòng Text nhỏ xíu ở trong cái Nút thành chữ `X`.
   - Vứt nó lên góc lề phải trên cùng của khung Container.

👉 Đỉnh điểm quan trọng: TẮT ĐI THEO QUY ƯỚC: Sau khi nặn hình xong hòm hòm, nhớ click vào `PlayPopup` và `SettingsPopup` rồi **TẮT TICK "Active" bỏ che khung mắt thần** (Con mắt hoặc Dấu Tick sát chữ tên của Game Object ngoài cùng Inspector). Game chạy thì code sẽ tự động Gọi Bật (Active).

---

## BƯỚC 4: Rót vào Mã Nguồn (Wire The Code)

Giờ là khâu quan trọng nhất: Khai báo với Code những thứ đồ chơi bạn vừa vẽ.
Bấm vào `LobbyCanvas` mẹ. Di chuyển chuột tới chóp Inspector nơi kẹp Script `LobbyUI.cs` đang ngự trị. Dùng thao tác Kéo / Thả lần lượt. Thiết lập như thông tin dưới, Cấm Kéo Trái Đi:

### 4.1 Panels
- **Main Panel**: Lôi `MainPanel` vào chui mọt đây.
- **Play Popup**: Lôi `PlayPopup` panel bị mờ vào chui mọt đây.
- **Settings Popup**: Lôi `SettingsPopup` panel chứa nút đổi tên vào đây.

### 4.2 Main Menu Buttons
- **Open Play Btn**: Thả Nút bự chảng `PlayBtn` ban nãy vào.
- **Open Settings Btn**: Thả Nút `SettingsBtn` vào.
- **Quit Btn**: Thả Nút `QuitBtn` hẩm hiu góc gách vào.

### 4.3 Play Popup Elements
- **Host Button**: Thả `HostMatchBtn`.
- **Join Button**: Thả `JoinMatchBtn`.
- **Close Play Popup Btn**: Thả Nút dấu `X` trong bảng PlayPopup vào rãnh.
- **Join Code Input**: Thả nguyên cụm InputField gốc (Chứa component `TMP_InputField`), chớ không kéo cục ruột Text vào! 
- **Status Text**: Kéo cái `StatusText` nằm lạc loài dưới đáy Hierarchy lên chui rỗng đây.

### 4.4 Settings Popup Elements
- **Close Settings Popup Btn**: Thả Dấu `X` của bảng SettingsPopup.
- **Save Name Btn**: Thả nút `SaveBtn`.
- **Player Name Input**: Thả thẻ gốc bọc vòng Input thay đổi tên nằm ở mảng Settings.

---

## 🎉 HOÀN THIỆN XONG! TẬN HƯỞNG

Chạy `Play` kiểm tra luồng Logic:
- Vô game đứng, UI đè 3 cái Nút. Bấm `Settings`, Màn hình mờ đen mọc lên cái bảng Đổi Tên. Nhập tên bạn thích, Gõ Save, Ấn Dấu `X` trả về Main.
- Ấn Play, bảng Host/Join bật lên. Bấm `Host Match` chờ vài giây, Dòng trạng thái nháy màu báo *"SERVER ESTABLISHED. JOIN CODE: XXXX"*! Đại công cáo thành!
