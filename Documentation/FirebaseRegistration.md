# Đăng ký và quyền Firestore

`SignUp.OnSignUpClicked()` nhận `success` từ `FirebaseManager.RegisterParentAsync()`.
Hàm chỉ trả `true` sau khi tạo Firebase Auth user và ghi đủ các tài liệu:

1. `users/{uid}`
2. `users/{uid}/children/{childId}`
3. `users/{uid}/children/{childId}/habimon/status`
4. `users/{uid}/children/{childId}/habits/habit_001`
5. `users/{uid}/children/{childId}/rewards/reward_01`

`Missing or insufficient permissions` ở các bước ghi là lỗi Firestore.
Log `[RegisterParent]` ghi bước, project, UID và document bị lỗi.
Không ghi mật khẩu hoặc token vào log.

## Áp dụng rule

File `firestore.rules` khớp với rules được cung cấp từ Firebase Console: chỉ chủ UID
được tạo/đọc/cập nhật hồ sơ; các subcollection chỉ được truy cập khi tài liệu phụ huynh
đã tồn tại. Xóa tài liệu phụ huynh không được cấp quyền. Đây là ranh giới quyền theo tài khoản phụ huynh; rule không
phân biệt chế độ trẻ/phụ huynh trong ứng dụng vì cả hai dùng cùng Firebase Auth user.

Rule tạo hồ sơ cấm sự xuất hiện của `passwordHash` và `passwordSalt`, kể cả giá trị
rỗng hoặc `null`. Code đăng ký cũ gửi hai trường này nên bị từ chối ngay tại
`users/{uid}`. `ParentModel` và cả hai luồng email/Google hiện chỉ ghi `email`,
`pincode`, `createdAt`; mật khẩu do Firebase Auth quản lý. Không cần nới lỏng rule.
Các verifier cũ có thể được giữ nguyên hoặc xóa, nhưng không được thêm hay thay đổi.

1. Mở Firebase Console, chọn project trùng với `Project` trong log. Cấu hình desktop
   trong repo hiện dùng `habimon-808e2`.
2. Chọn **Firestore Database → Rules** (không phải Realtime Database).
3. Đối chiếu rule đang chạy với `firestore.rules`; ghép điều kiện quyền này vào rules
   hiện có nếu project còn các chính sách hoặc collection khác cần giữ.
   Rule chỉ cho `users/{uid}` không tự cấp quyền cho subcollection.
   Rule dựa trên `resource.data` của tài liệu chưa tồn tại cũng có thể chặn bước tạo mới.
4. Kiểm tra trong Rules Playground: UID của chủ tài liệu được tạo/đọc dữ liệu;
   UID khác và request chưa xác thực phải bị từ chối. Sau đó chọn **Publish**.

File trong repo không tự thay đổi rules trên Firebase. Chưa xác minh hoặc triển khai
rules của project thật từ môi trường này.

## Tài khoản đã tạo nhưng dữ liệu chưa hoàn tất

Firebase Auth và Firestore không dùng chung transaction. Auth user có thể đã tồn tại
trong **Authentication → Users** dù đăng ký báo lỗi khi ghi Firestore; đăng ký lại cùng
email sẽ gặp lỗi email đã tồn tại. Sau khi cập nhật code, đăng nhập bằng email/mật khẩu
đã dùng: `LoginParentAsync` xác thực qua Firebase Auth rồi gọi `EnsureParentDataAsync`
để tạo hồ sơ phụ huynh nếu thiếu và tạo dữ liệu mặc định nếu chưa có trẻ. Hồ sơ đã có
không bị ghi đè. Trường hợp đã có trẻ nhưng thiếu pet/habit/reward vẫn cần kiểm tra
riêng; helper chưa khôi phục các tài liệu con của trẻ đã có.

Tài liệu Firebase:
- https://firebase.google.com/docs/auth/unity/password-auth
- https://firebase.google.com/docs/firestore/security/rules-conditions
- https://firebase.google.com/docs/firestore/security/rules-structure
