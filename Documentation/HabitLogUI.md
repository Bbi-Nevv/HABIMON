# Báo cáo thói quen – Parent

`Assets/_Scenes/Parent.unity → Canvas → HabitLogPanel` đã được dựng theo khung 1080×1920, dùng ảnh khu vườn và Habi có sẵn trong project.

- Thẻ tổng kết: nhiệm vụ đã xong, tỷ lệ cả tuần, số bé.
- Bộ chuyển tuần có nút lùi, tiến và trở về tuần hiện tại.
- Biểu đồ bảy ngày: mỗi bé một cột/ngày, màu được giữ theo bé; có cuộn ngang khi nhiều bé.
- Chạm ngày để xem từng bé, từng nhiệm vụ, trạng thái chờ duyệt và giờ hoàn thành theo lịch Việt Nam (UTC+7). Danh sách chi tiết cuộn độc lập.
- DOTween chạy bằng thời gian thực: thẻ xuất hiện lần lượt, Habi chuyển động nhẹ, nút thu/phóng khi chạm, cột và thanh tỷ lệ chuyển động, chuyển tuần và chi tiết đổi nhẹ.
- `HabitLogPanelMotion` căn khung thiết kế vào safe area. Báo cáo dùng trực tiếp Taskbar cũ của Parent; không tạo hoặc ẩn thanh điều hướng khác.
- Controller nằm dưới panel, chỉ giữ Firebase listener khi báo cáo mở. Thêm/xóa nhiệm vụ hoặc cập nhật nhật ký sẽ làm mới dữ liệu tự động. Báo cáo không đổi bé đang được chọn ở các tab khác.

Các con số dùng cùng `HabitAnalytics` với phần còn lại của app. Ngày trước `effectiveFrom` hoặc từ ngày `archivedAt` trở đi không tạo nhiệm vụ; lịch lặp rỗng không được hiểu là hằng ngày. Thói quen cũ thiếu mốc lịch sử được đánh dấu chưa đủ dữ liệu. Log raw trong tuần được giữ để phát hiện bản ghi trùng mâu thuẫn. Chỉ log hoàn thành được duyệt mới được tính là hoàn thành khi log yêu cầu duyệt; quy tắc duyệt tại thời điểm ghi log được ưu tiên.

`ReportRounded.png` là sprite trắng bo góc dùng chung, có border để slice. Không chỉnh sửa ảnh nền, mascot hay sprite sheet hiện có.

UI hiện dùng báo cáo tuần, nhiều bé. Các bộ lọc ngày/tháng/năm cũ không còn trên màn hình này. `Tools/RebuildHabitLogPanel.cjs` chỉ nên chạy sau khi scene đang chỉnh đã được lưu; công cụ thay nội dung HabitLogPanel và giữ Taskbar cùng callback điều hướng.

Ảnh `output/design/habit-log-garden-1080x1920.png` được render bằng Unity với **dữ liệu minh họa trong project tạm**; dữ liệu minh họa không được đưa vào scene hoặc Firebase thật.
