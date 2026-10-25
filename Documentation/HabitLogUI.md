# Báo cáo thói quen – Parent

`Assets/_Scenes/Parent.unity → Canvas → HabitLogPanel` đã được dựng theo khung 1080×1920, dùng ảnh khu vườn và Habi có sẵn trong project.

- Thẻ tổng kết: nhiệm vụ đã xong, tỷ lệ cả tuần, số bé.
- Bộ chuyển tuần có nút lùi, tiến và trở về tuần hiện tại.
- Biểu đồ bảy ngày: mỗi bé một cột/ngày, màu được giữ theo bé; có cuộn ngang khi nhiều bé.
- Chạm ngày để xem từng nhiệm vụ, trạng thái chờ duyệt và giờ hoàn thành thực tế. Danh sách chi tiết cuộn độc lập.
- DOTween chạy bằng thời gian thực: thẻ xuất hiện lần lượt, Habi chuyển động nhẹ, nút thu/phóng khi chạm, cột và thanh tỷ lệ chuyển động, chuyển tuần và chi tiết đổi nhẹ.
- `HabitLogPanelMotion` căn khung thiết kế vào safe area. Báo cáo dùng trực tiếp Taskbar cũ của Parent; không tạo hoặc ẩn thanh điều hướng khác.
- Controller nằm dưới panel, chỉ giữ Firebase listener khi báo cáo mở. Thêm/xóa nhiệm vụ hoặc cập nhật nhật ký sẽ làm mới dữ liệu tự động.

Các con số được tính từ nhiệm vụ **đang hoạt động và đúng lịch lặp hiện tại**. Xóa hoặc tắt nhiệm vụ sẽ loại nhiệm vụ đó khỏi biểu đồ, kể cả khi xem tuần cũ. `completedAt` hiển thị theo giờ thiết bị; hoàn thành đang chờ duyệt vẫn được tính là đã làm.

`ReportRounded.png` là sprite trắng bo góc dùng chung, có border để slice. Không chỉnh sửa ảnh nền, mascot hay sprite sheet hiện có.

Có thể dựng lại hierarchy bằng `node Tools/RebuildHabitLogPanel.cjs` từ thư mục project. Công cụ chỉ thay nội dung HabitLogPanel, gắn controller vào panel và giữ nguyên Taskbar cùng các callback điều hướng gốc. Lưu scene đang chỉnh trước khi chạy công cụ.

Ảnh `output/design/habit-log-garden-1080x1920.png` được render bằng Unity với **dữ liệu minh họa trong project tạm**; dữ liệu minh họa không được đưa vào scene hoặc Firebase thật.
