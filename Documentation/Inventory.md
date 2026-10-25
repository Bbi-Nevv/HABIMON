# Túi phụ kiện và trang bị monster

Các script nằm trong `Assets/_UI/invetory` (giữ tên thư mục hiện có).

## Dữ liệu Item

Trong Project, chọn **Create > HABIMON > Inventory > Item** để tạo một asset cho mỗi phụ kiện. Điền **Item Name**, **Item Icon** và **Item Type**:

| Item Type | Vị trí trên monster |
| --- | --- |
| `eyeglasses` | Kính |
| `neck` | Cổ |
| `hand` | Tay |

**Equipment Sprite** là ảnh mặc trên monster, nếu khác với icon trong túi. Để trống sẽ dùng **Item Icon**. Dữ liệu ScriptableObject không bị thay đổi khi trang bị.

## Chuẩn bị UI

1. Tạo một panel túi đồ dưới Canvas, thêm component **Inventory**. Tạo Content với `GridLayoutGroup` (hoặc Content của ScrollRect) để chứa các item.
2. Tạo prefab ô item: root có RectTransform và component **InventoryItemView**, bên trong có Image icon. Gán **Icon**, tùy chọn gán TMP **Item Name Text** và một GameObject **Equipped Marker** để hiện dấu đang trang bị. Giữ kích thước icon khác 0. Có thể dùng một template đang tắt ngay trong Content thay cho prefab asset.
3. Tạo ba ô trang bị, mỗi ô có Image nền trên root và component **EquipmentSlot**. Chọn **Item Type** riêng cho từng ô. Gán một Image con vào **Item Icon**, tách biệt Image nền; tùy chọn gán TMP tên item. Các ô có thể đặt cạnh monster hoặc trong panel túi.
4. Trên **Inventory**, gán danh sách **Items**, **Item Prefab**, **Content Root**, **Monster Accessories** và cả ba **Equipment Slots**. **Drag Root** có thể để trống để dùng root Canvas, hoặc gán một lớp UI trên cùng của Canvas để ảnh kéo không bị che.
5. Trên **Accessory** của monster, gán ba Image phụ kiện đã căn vị trí: **Eyeglasses Image**, **Neck Image**, **Hand Image**. Scene Child hiện đã có ba Image này. Tùy chọn gán **Starting Equipment** bằng các asset Item để mặc sẵn lúc bắt đầu.
6. Canvas phải có GraphicRaycaster; scene phải có EventSystem cùng UI Input Module phù hợp. Các CanvasGroup cha của ô thả cần cho phép raycast. Nút Inventory đang có trong scene Child có thể dùng OnClick để bật panel túi; nút đóng gọi `SetActive(false)` trên panel.

Danh sách sprite cũ **Accessory Images** vẫn được giữ để không mất dữ liệu scene, nhưng không tự gán sprite đầu tiên vào mọi vị trí nữa. Muốn mặc sẵn phụ kiện, dùng Item có loại rõ ràng trong **Starting Equipment**.

## Hành vi

- Inventory sinh các ô từ danh sách Item khi mở; gọi `Rebuild()` nếu thay đổi danh sách lúc chạy.
- Kéo bằng chuột trái hoặc cảm ứng. Ảnh kéo nằm trên UI, không chặn raycast và không làm thay đổi bố cục túi.
- Ô đúng loại đổi nền xanh; ô sai loại đổi nền đỏ. Chỉ Item thuộc đúng Inventory và có ItemType khớp với ô mới được nhận.
- Thả hợp lệ sẽ thay phụ kiện cũ cùng loại trên monster. Không ảnh hưởng các vị trí còn lại. Thiếu Image đích hoặc sprite trang bị cũng bị từ chối, giữ nguyên đồ cũ.
- Thả sai ô hoặc ra ngoài không thay đổi trang bị. Item luôn giữ trong túi để có thể đổi qua lại, không bị tiêu hao hay nhân bản dữ liệu.
- Có thể gắn nút tháo đồ vào `EquipmentSlot.Unequip()`.
- Đóng túi trong lúc kéo sẽ dọn ảnh kéo. Mở lại túi phản ánh đồ monster đang mặc.
- Trang bị và tháo đồ được lưu PlayerPrefs ngay khi thay đổi; mở lại app sẽ khôi phục.

## Lưu trang bị bằng SaveEquipment

Scene Child đã gắn **SaveEquipment** trên Canvas đang hoạt động, tham chiếu Inventory cùng hai Accessory của monster chính và bản xem trước. Đổi đồ trong túi sẽ cập nhật cả hai bản và lưu ngay; khi panel túi còn tắt, monster chính vẫn khôi phục được trang bị.

Để dùng ở scene khác, đặt **SaveEquipment** trên GameObject hoạt động ngay lúc vào scene và giữ bật trong lúc đổi đồ. Gán **Inventory** làm danh sách dữ liệu Item; **Accessory Displays** là các bản hiển thị của cùng một monster. **Additional Items** bổ sung những Item không có trong Inventory nhưng cần khôi phục. Tránh để hai SaveEquipment cùng điều khiển một Accessory.

Mỗi Item có ID được lấy từ GUID của asset trong Editor, không phụ thuộc tên hoặc thứ tự trong túi. Các asset Item hiện có đã được điền ID. Khi tạo Item mới, Unity tự cập nhật ID qua OnValidate. Khi đổi chỗ asset, giữ kèm file `.meta` để giữ ID.

PlayerPrefs dùng ba key mặc định: `Habimon.Equipment.0` (kính), `Habimon.Equipment.1` (cổ), `Habimon.Equipment.2` (tay). Giá trị là ID của Item; chuỗi rỗng nghĩa là đã tháo đồ. Nếu chưa có key thì giữ **Starting Equipment**. Nếu ID không còn trong danh sách dữ liệu hoặc sai ItemType, bỏ qua giá trị đó và ghi cảnh báo, không trang bị sai loại.

**Player Prefs Prefix** cho phép tách dữ liệu của các monster/profile độc lập. Mặc định trang bị dùng chung trên thiết bị, không gọi Firebase. Khi load, sự kiện cập nhật UI vẫn chạy nhưng không ghi đè dữ liệu đang khôi phục.

## Kiểm tra trong Play Mode

1. Dùng hai Item kính khác nhau, một Item cổ và một Item tay; xác nhận túi sinh đủ icon và tên.
2. Thả kính vào ô kính: icon ô, marker trong túi và hình monster cùng cập nhật.
3. Thả kính vào ô cổ/tay: không đổi trang bị; item vẫn ở túi.
4. Đổi kính thứ hai: chỉ kính thay đổi, marker chuyển đúng item, phụ kiện cổ/tay giữ nguyên.
5. Tháo phụ kiện: ô trống, Image trên monster ẩn và marker tắt.
6. Thả ngoài UI, đóng túi giữa lúc kéo, mở lại nhiều lần: không còn ảnh kéo và không sinh thừa item.
7. Kiểm tra trên cảm ứng, CanvasScaler và màn hình khác tỉ lệ; thử đổi đồ trong khi monster hoạt động.
8. Trang bị đủ ba vị trí, tắt và mở lại app khi túi đang đóng: monster chính phải hiển thị đúng; mở túi thì icon ô và marker khớp với đồ đã lưu.
9. Tháo một vị trí rồi mở lại app: vị trí đó vẫn trống, kể cả khi có Starting Equipment. Thử đổi tên và thứ tự Item trong Inventory để xác nhận vẫn load đúng dữ liệu.
