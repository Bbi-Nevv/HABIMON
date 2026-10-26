# Thành tựu tưới cây

`Achievements` cộng dồn số giọt đã dùng trong các lần tưới thành công. `Watering.StartWatering()` cần đúng 2 giọt; thiếu nước hoặc bấm lại trong cùng animation không trừ thêm và không tăng tiến độ.

Scene Child hiện có 4 Item sở hữu ban đầu: Cup, Gậy phép, Nơ và Huân Chương. Ba mốc đang cấu hình là:

| Tổng giọt đã dùng | Phần thưởng |
| ---: | --- |
| 4 | Kính tròn |
| 6 | Kính ngôi sao |
| 10 | Kính râm |

`WateringCape` vẫn là asset có thể mở khóa, nhưng chưa được gán cho mốc nào trong scene hiện tại. Muốn dùng áo choàng, hãy gán nó vào một milestone rõ ràng; không thay ID milestone hiện có nếu cần giữ receipt đã nhận.

## Thiết lập và khôi phục

Trong **Achievements > Milestones**, thêm mốc với **Required Water** và **Reward Item**. Mốc dùng tổng giọt cộng dồn, không reset sau khi nhận. Mỗi mốc có ID riêng; giữ ID đó khi đổi tên hoặc sắp xếp lại để bảo toàn trạng thái.

Khi đạt mốc, Item được thêm vào `Inventory.Items` và lưu receipt trước hiệu ứng bay. `Achievements.Awake()` đăng ký Item thưởng với Inventory, nên quyền sở hữu được khôi phục kể cả khi panel túi đang đóng. **Items** là đồ đang sở hữu; **Unlockable Items** là catalogue Item có thể mở khóa và không tự cấp đồ.

Scene Child đã bật `Achievements`, `Inventory` và `SaveEquipment` trên Canvas. Dữ liệu nước, milestone, ownership và trang bị lưu trong PlayerPrefs, dùng chung trên thiết bị và độc lập với Firebase.

Nếu nâng cấp từ bản main cũ có 3 kính miễn phí, `SaveEquipment` chỉ tạo ownership receipt cho kính có ID đang được lưu trong đúng slot trang bị. Các phần thưởng chưa đạt mốc không được mở khóa bởi migration.

## Thanh tiến độ và hiệu ứng

`AchievementProgressView` tạo thẻ tiến độ lúc chạy. Thanh hiển thị mốc chưa nhận gần nhất và chuyển sang trạng thái đã mở khóa khi hoàn tất toàn bộ milestone. Gán **Inventory Target** tới biểu tượng túi trên taskbar; **Reward Origin** mặc định lấy từ thanh tiến độ; **Animation Root** dùng root Canvas.

DOTween chạy marker, hiệu ứng nhận thưởng và các giọt nước theo thời gian thực. Phần thưởng cùng tiến độ được lưu trước hiệu ứng, nên đóng app giữa animation không làm mất dữ liệu. Khi đạt nhiều mốc trong một lần cập nhật, phần thưởng được xử lý lần lượt.

## PlayerPrefs

| Key | Nội dung |
| --- | --- |
| `Habimon.Achievements.TotalWaterUsed` | Tổng giọt đã tiêu để tưới |
| `Habimon.Achievements.Completed.<id>` | Receipt milestone đã nhận |
| `Habimon.Inventory.Unlocked.<ItemId>` | Receipt ownership của Item mở khóa |
| `Habimon.Attendance.WaterDrops` | Số giọt hiện có |

Các khóa trang trí và trang bị thuộc thiết bị hiện tại, không theo từng hồ sơ bé. Không dùng chúng để ghi đè vàng hoặc kinh nghiệm lưu trong Firebase.

## Kiểm tra trong Play Mode

1. Điểm danh để nhận nước. Tưới thành công 2 giọt mỗi lần; xác nhận tiến độ tăng đúng 2.
2. Sau 2 lần tưới, nhận Kính tròn ở mốc 4; sau 3 lần, nhận thêm Kính ngôi sao ở mốc 6; sau 5 lần, nhận Kính râm ở mốc 10. Mỗi Item chỉ được thêm một lần.
3. Thử tưới khi có 0 hoặc 1 giọt, và bấm lại khi animation đang chạy; không được trừ thêm hay tăng tiến độ.
4. Mở túi, trang bị phần thưởng vào slot kính, đóng app rồi mở lại; ownership và trang bị phải được khôi phục.
5. Kiểm tra kéo Item vào đúng/sai slot, hiệu ứng bay và bố cục ở các tỷ lệ màn hình hỗ trợ.
