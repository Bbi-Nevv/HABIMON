# Điểm danh nhận giọt nước

Logic nằm trong `Assets/_Backend/Features/AttendanceCheck.cs`, class `AttendanceCheck`.

## Gắn UI trong Unity

1. Thêm component `AttendanceCheck` vào một GameObject trên màn hình có bộ đếm nước.
2. Tạo nút điểm danh và gán vào **Attendance Button**. Component tự đăng ký sự kiện click; không cần thêm OnClick trong Inspector.
3. Gán **Water Count Text** vào TMP hiển thị tổng nước (trong scene Child hiện là `Water/Count`).
4. Gán **Water Drop Icon** vào Image chứa sprite giọt nước hiện có. Sprite này được dùng để tạo các giọt bay; không cần prefab riêng.
5. Chỉnh **Daily Water Reward** để thay đổi phần thưởng, mặc định 10 giọt/ngày.

Các ô tùy chọn:

- **Status Text**: thông báo có thể điểm danh hoặc đã nhận hôm nay.
- **Countdown Text**: thời gian còn lại tới ngày mới, cập nhật mỗi giây.
- **Reward Origin**: vị trí xuất phát, mặc định là nút điểm danh.
- **Water Target**: vị trí đích, mặc định là icon nước, sau đó là bộ đếm.
- **Animation Root**: lớp UI chứa các giọt bay, mặc định là root Canvas của đích. Có thể gán một RectTransform trên cùng root Canvas và ở trên các panel khác để hiệu ứng luôn thấy được.
- **Max Animated Drops**, **Flight Duration**, **Drop Interval**, **Drop Size**: số giọt hiệu ứng tối đa, thời gian bay, khoảng cách thời gian giữa các giọt và kích thước ảnh.

Thiếu tham chiếu hiệu ứng thì phần thưởng vẫn được lưu và bộ đếm cập nhật ngay.

## Thời gian và lưu trữ

- Mỗi ngày theo lịch trên thiết bị được nhận một lần; ngày mới bắt đầu lúc 00:00, không cần chờ đủ 24 giờ từ lần nhận trước.
- UI cập nhật khi qua ngày mới, khi mở lại panel hoặc khi app trở lại foreground. Không tự nhận thưởng cho những ngày đã bỏ qua.
- Ngày nhận gần nhất không được nhận lại khi chỉnh đồng hồ lùi. Đây là hệ thống dùng đồng hồ và dữ liệu cục bộ, không xác thực thời gian qua server.
- Số dư và ngày nhận dùng hai key PlayerPrefs: `Habimon.Attendance.WaterDrops` và `Habimon.Attendance.LastCheckInDate`. `PlayerPrefs.Save()` chạy ngay khi nhận, trước hiệu ứng.
- Số dư dùng chung trên thiết bị giữa các scene/tài khoản và không gọi Firebase. Có thể đọc qua `attendanceCheck.WaterDrops` hoặc hằng `AttendanceCheck.WaterDropsKey`.
- Đóng panel giữa hiệu ứng sẽ dọn các tween và giọt tạm; mở lại hiển thị số dư đã lưu.

## DOTween

Đã thêm DOTween core v1.3.045 từ [bản phát hành chính thức Demigiant](https://dotween.demigiant.com/download.php) vào `Assets/Plugins/Demigiant/DOTween`, kèm tài liệu và thông tin bản quyền gốc. Hiệu ứng dùng các shortcut Transform của core nên không cần module UI hoặc Utility Panel. Xem [tài liệu DOTween](https://dotween.demigiant.com/documentation.php) và [giấy phép](https://dotween.demigiant.com/license.php).

## Kiểm tra trong Play Mode sau khi gán UI

1. Lần đầu: nhấn điểm danh, nước tăng đúng phần thưởng, giọt bay tới icon và nút chuyển sang không tương tác.
2. Nhấn nhiều lần hoặc gọi `CheckIn()` từ component khác trong cùng ngày: không cộng thêm.
3. Đóng panel giữa hiệu ứng, mở lại hoặc khởi động lại app: số dư và trạng thái nhận vẫn giữ nguyên.
4. Để app chạy qua 00:00 hoặc đưa app xuống nền rồi mở lại vào ngày tiếp theo: nút mở nhận thưởng mới.
5. Đặt `Time.timeScale = 0`: đồng hồ và hiệu ứng vẫn chạy.
6. Kiểm tra đường bay ở các tỉ lệ màn hình và chế độ Canvas đang sử dụng.
