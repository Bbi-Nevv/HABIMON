# Tích hợp origin/main vào Nam — 25/10/2026

## Phạm vi và nguồn

- Nguồn remote: `origin/main` tại `7c1d7b5e8715b92ab5f87ea362949dde3fba19a5`.
- Nền: `Nam` tại `a54157b028f4f40002b27ce0e92b78603719d989`.
- Thực hiện ban đầu trong worktree riêng `integration/main-into-Nam`; không dùng local main.
- Review ba chiều xác định 172 đường dẫn khác nhau, 7 file/24 vùng conflict; đã kiểm tra cả 3 file auto-merge và tất cả đường dẫn.

## Kết quả tích hợp

Giữ init/session/provisioning, nullable models, UTC+7, pending-create scope/idempotency, archive/history, analytics và chatbot của Nam. Nhận assets, prefabs, animation, layer tree, affiliate và UI/realtime của main.

- Parent dùng một `ParentConfirm`, theo dõi nhiều trẻ bằng listener có parent/session generation guards. Component `HabitApprovalUI` cũ được gỡ khỏi scene; source giữ lại cho compatibility.
- Approval mới gọi `HabitRewardService.ApprovePendingLogForChildAsync` với child/log cụ thể và expected completion timestamp; không đổi trẻ đang chọn để duyệt. Request cũ sau cancel/recomplete bị từ chối.
- Completion tiếp tục giới hạn selected child và dùng cùng canonical daily ID, reward transaction Nam. Child, pet và log được cập nhật cùng transaction; không early-return sau khi đã queue một phần writes.
- Các log legacy/noncanonical đã tồn tại làm thao tác completion/approval trả về thất bại để đối soát; không tự xóa, chuyển ID hoặc cộng bù reward. Analytics vẫn đọc raw logs để phát hiện dữ liệu mâu thuẫn.
- DisplayMission giữ realtime, reload/focus/ngày mới, dừng listeners theo lifecycle, kiểm tra scope sau await, giữ schedule/fallback icons Nam. Khi save cũ kết thúc sau reload, UI mới được reconcile thay vì kẹt saving.
- Scope/ngày được kiểm tra trước mutation hủy hoàn thành để không ghi ngày đã khóa sau khi chờ đọc.
- Mô tả habit tùy chọn thống nhất ở popup, preparation và pending payload. MissionUI không crash khi thiếu optional add-button binding.
- Parent scene giữ đầy đủ button/name/fallback/book fields và sửa sprite subasset homework trong approval list. Font dùng asset đầy đủ main với dynamic population; không ghép atlas/glyph rời rạc.
- Affiliate validate/open cùng URL đã normalize. Cấu hình Firebase Data cũ tiếp tục bị xóa và được ignore rõ ràng.

## Kiểm chứng

Baseline Nam chạy lại: EditMode 63/63. Regression tests đã tái hiện các lỗi trước khi sửa: optional description, approval component, null MissionUI button, explicit-child approval API và pending-save settlement.

| Kiểm tra bản tích hợp | Kết quả |
|---|---|
| Unity EditMode | 69/69 pass |
| Unity PlayMode thông thường | 10/10 pass; 1 test emulator Explicit được chạy riêng |
| Auth + Firestore integration emulator | 1/1 pass, gồm provisioning/retry, multi-child approval, stale request, concurrent approvals/completions, exact balance checks và legacy duplicate guards |
| Firestore Rules emulator | 9/9 pass |
| npm audit moderate | 0 vulnerabilities |
| Linux development player | Build Succeeded, 0 errors, 1 warning; 313453128 bytes |
| C# conditional UNITY_ANDROID | Pass, 0 errors; 88 CS0649 serialized-field warnings và 1 CS0169 unused-field warning |
| GUID và Unity YAML | Không duplicate GUID/anchor; không thiếu local fileID trong các scene/prefab tracked |
| Đối chiếu 172 đường dẫn ban đầu | Đủ 172, không phát hiện thay đổi ngoài quyết định tích hợp |
| Independent code review | Ba Important findings đã sửa và được reviewer kiểm tra lại; không còn Critical/Important finding được báo |

PlayMode có chạy animation tưới cây thực: StartParticle, StopParticle và StopWatering. EditMode kiểm tra missing scripts trong Menu/Parent/Child và các prefab mới, approval icon subasset và approval flow duy nhất.

Phân tích graph đầy đủ (partial=false, truncated=false) ghi nhận 81 file, 127 symbols và 21 flows qua completion, approval và pending-create; nhãn risk Critical phản ánh phạm vi này, không phải chứng nhận không có lỗi. Kết quả graph phải được kết hợp với source review và runtime tests.

## Giới hạn còn lại

- Các kiểm tra trên là local/Emulator; chưa có hosted CI cho commit tích hợp nếu chưa push.
- Không triển khai Rules/backend, không migrate/repair hoặc ghi dữ liệu production.
- Parent/Child vẫn dùng cùng Firebase Auth identity; owner rules/client transactions/PIN UI chưa tạo ra server-authoritative parent-only authorization.
- Enumeration legacy nằm ở client; không ngăn tuyệt đối một client cũ tạo ID legacy mới đồng thời ngoài transaction đã kiểm tra. Cần backend/rules và migration policy để khép vấn đề production.
- Legacy balances, approved-without-reward, orphan descendants và historical schedule revisions cần đối soát riêng; không coi merge là đã giải quyết chúng.
- Editor cài LinuxStandaloneSupport, chưa có module Android/iOS. Build target/device cần kiểm chứng riêng; conditional compilation không thay APK/iOS/device proof.
- Build/runtime tích hợp không có nghĩa mọi finding trong audit 60 mục đã đóng. Không tuyên bố hoàn thiện production 100% từ kết quả merge.

## Ghi chú build

Cảnh báo Linux: “Invalid texture used for cursor” (yêu cầu RGBA32/readable/alphaIsTransparency/no mip chain). Cùng defaultCursor GUID đã có trên cả Nam và origin/main, ProjectSettings cùng texture importer không phải thay đổi của lần tích hợp này; không tự sửa thiết kế cursor ngoài phạm vi. Đây không phải build không có warning.

Conditional Android compilation dùng các runtime source/references của Unity, có UNITY_ANDROID và bỏ UNITY_EDITOR/UNITY_STANDALONE. Nó kiểm tra nhánh Google/mobile/TTS biên dịch nhưng không kiểm tra Gradle, manifest, signing, native Android ABI hoặc thiết bị.

Unity tự tạo thay đổi batching/analytics, performance resources và xóa dynamic font cache trong quá trình test/build. Những artifact kiểm chứng được dọn riêng; ProjectSettings được giữ như nền, font source được phục hồi nguyên blob main sau khi Editor thoát. Không đưa script build tạm hay thông tin môi trường vào commit.
