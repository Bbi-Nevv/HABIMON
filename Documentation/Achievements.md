# Thành tựu tưới cây

`Achievements` theo dõi tổng số giọt đã dùng để bắt đầu tưới cây thành công. `StartWatering()` tiêu 2 giọt thì tiến độ tăng 2. Thiếu nước, bấm lại trong cùng animation hoặc tiêu nước ngoài thao tác tưới sẽ không tăng tiến độ.

Scene Child đã gắn controller **Achievements** trên Canvas, thanh **WateringAchievementsProgress** phía trên taskbar và mốc mẫu **20 giọt → Áo choàng chăm cây**. Áo choàng là Item mới thuộc loại `neck`, dùng sprite có sẵn của bộ phụ kiện; không nằm trong danh sách đồ sở hữu ban đầu và không thay đổi 7 Item hiện có.

## Thiết lập mốc

Trong **Achievements > Milestones**, thêm phần tử và gán **Required Water**, **Reward Item**. Mốc là tổng nước cộng dồn, ví dụ 20/50/100 giọt; không reset sau khi nhận thưởng. Mỗi mốc có ID riêng, tự tạo trong Editor; giữ nguyên ID nếu muốn giữ trạng thái đã nhận khi đổi tên/sắp xếp mốc.

Khi đạt mốc, phần thưởng tự nhận vào `Inventory.Items`, UI túi cập nhật nếu đang mở. Cùng một Item chỉ thêm một lần. Nếu đã sở hữu skin từ trước thì vẫn đánh dấu hoàn thành nhưng không sinh thêm bản sao hoặc hiệu ứng nhận skin mới. Nếu một lần tăng tiến độ vượt nhiều mốc, nhận tất cả mốc đạt được, hiệu ứng bay chạy lần lượt.

Controller đăng ký skin thưởng vào **Inventory > Unlockable Items**. Ở scene mới, nên điền sẵn danh sách này với toàn bộ Item thưởng để Inventory khôi phục đồ ngay cả khi panel/controller thành tựu chưa bật. Scene Child đã gán sẵn áo choàng trong danh sách này. **Items** là đồ sở hữu ban đầu, **Unlockable Items** là danh sách dữ liệu có thể mở khóa và không tự cấp đồ.

## Thanh tiến độ và hiệu ứng

`AchievementProgressView` tạo UI lúc chạy: thẻ trắng bo góc, tiêu đề “Hành trình của cây”, bộ đếm `12/20 giọt`, chấm xanh/xám, marker Habi chạy theo tiến độ và biểu tượng khóa/phần thưởng. Thanh hiển thị mốc chưa nhận gần nhất; khi hết mốc chuyển sang “Đã mở khóa”. Chỉnh vị trí/kích thước qua RectTransform của **WateringAchievementsProgress**, hình Habi/giọt nước và số chấm qua component.

Trên controller, **Inventory Target** là RectTransform của biểu tượng túi đồ trên taskbar, không phải panel túi đang đóng. **Reward Origin** mặc định là đầu mở khóa trên thanh tiến độ. **Animation Root** là lớp UI trên cùng hoặc root Canvas. DOTween cho skin phóng nhẹ rồi bay cong, thu nhỏ về túi. Thanh tiến độ cũng tween marker và nhún nhẹ khi nhận quà. Các hiệu ứng vẫn chạy khi `Time.timeScale = 0`.

Skin được thêm và lưu trước khi bay. Tắt màn hình hoặc đóng app giữa hiệu ứng không làm mất quà. Khi thiếu tham chiếu hiệu ứng, vẫn cấp phần thưởng bình thường. Controller nên luôn hoạt động trên Canvas; nếu tắt controller, tổng nước vẫn được lưu, các mốc sẽ được xét lại khi bật lên.

## PlayerPrefs

| Key | Nội dung |
| --- | --- |
| `Habimon.Achievements.TotalWaterUsed` | Tổng giọt đã dùng để tưới từ khi triển khai hệ thống |
| `Habimon.Achievements.Completed.<id>` | Mốc đã nhận thưởng |
| `Habimon.Inventory.Unlocked.<ItemId>` | Quyền sở hữu skin thưởng |

Tiến độ cũ trước khi thêm hệ thống chưa có bộ đếm nên bắt đầu từ 0. Lưu độc lập Firebase, dùng chung trên thiết bị như các chức năng trang trí hiện có. Việc khôi phục trang bị của `SaveEquipment` dùng được các skin đã mở khóa kể cả khi túi đang đóng.

## Kiểm tra Play Mode

1. Tưới 6 lần hợp lệ: hiện 12/20; chưa có áo choàng. Tưới tiếp 4 lần: nhận áo choàng đúng một lần, ảnh skin bay về túi.
2. Thử thiếu nước và bấm liên tiếp khi animation tưới đang chạy: tiến độ không tăng thêm.
3. Mở Inventory: thấy skin mới, kéo vào ô cổ được; kéo vào ô kính/tay bị từ chối.
4. Đổi trang bị sang skin thưởng, tắt/mở app khi Inventory đang đóng: tiến độ, quyền sở hữu và trang bị đều giữ nguyên.
5. Đóng màn hình giữa lúc skin bay hoặc vượt nhiều mốc: không mất phần thưởng, không sinh Item trùng.
6. Tắt controller, tưới đủ mốc rồi bật lại: phần thưởng được bù khi xét tiến độ. Thử sắp xếp lại danh sách mốc và Item sau khi nhận, không được nhận trùng.
7. Kiểm tra bố cục, vị trí thanh và đường bay trong Game view với các tỉ lệ màn hình đang hỗ trợ.
