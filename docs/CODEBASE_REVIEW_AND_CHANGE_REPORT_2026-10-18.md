# HABIMON — Báo cáo rà soát codebase và thay đổi

Ngày lập: **18/10/2026**; cập nhật kiểm chứng: **24/10/2026**  
Nhánh: **`Nam`**  
HEAD tại thời điểm rà soát: **`486765987db5726873185235fb1a6c398f6b98c8`**

Báo cáo này tổng hợp trạng thái code hiện tại, các thay đổi trong worktree và mức xử lý của 60 findings trong [audit findings](CODEBASE_AUDIT_FINDINGS_2026-10-18.md) và [remediation plan](CODEBASE_REMEDIATION_PLAN_2026-10-18.md). Ma trận per-finding trong remediation plan là nguồn chi tiết cho điều kiện đóng; tài liệu này giải thích kết quả theo nhóm và chỉ ra giới hạn bằng chứng.

**Bổ sung sau kiểm kê Firebase ngày 22/10:** xem [FIREBASE_REMEDIATION_ACTION_PLAN_2026-10-22.md](FIREBASE_REMEDIATION_ACTION_PLAN_2026-10-22.md) cho baseline dữ liệu thật, phạm vi có quyền thực hiện, dry-run/production gates và từng task. Cập nhật kiểm chứng 24/10 trong §5.1 phản ánh local code/test mới và read-only Rules readback; không có repair/migration/deployment production mới trong lượt đó.

## 1. Kết luận rà soát

Hai báo cáo trước chứa **60 findings**: F01–F32, N01–N14 và E01–E14. Theo ma trận trạng thái hiện tại:

| Trạng thái | Số lượng | Findings |
|---|---:|---|
| Một phần | 21 | F01, F02, F03, F09, F10, F11, F12, F14, F15, F24, F25, F26, F30, F31, N06, N12, E05, E08, E10, E11, E12 |
| Đã xử lý local | 37 | F04, F05, F06, F07, F08, F13, F16, F17, F18, F19, F20, F21, F22, F23, F28, F29, F32, N01, N02, N03, N04, N05, N07, N08, N09, N10, N11, N13, N14, E01, E02, E03, E04, E06, E07, E09, E14 |
| Mở | 0 | — |
| Defer theo quyết định người dùng | 2 | F27, E13 |

“Đã xử lý local” nghĩa là đã thay đổi source, scene hoặc tài liệu và có kiểm tra phù hợp trong checkout; không đồng nghĩa đã chứng minh API backend đã deploy, migration dữ liệu, client authorization hoặc hành vi trên thiết bị thật. Vì vậy **37 findings local không phải kết luận 37 findings đã được đóng end-to-end**. F01/F02/F03/F09/F10/F11/F24/F25/F26 và E10 vẫn còn gate bảo mật/server/runtime hoặc dữ liệu lịch sử.

Rà soát được thực hiện trên diff hiện có của nhánh `Nam`, các tệp mới trong worktree, ma trận 60 findings và các source/scene liên quan. Tại lần kiểm chứng 22/10, diff tracked gồm **34 file, +3.370/−1.716 dòng** (kể cả thay đổi font asset đã có trước lượt này); có thêm các module/test/asset mới và năm tài liệu docs đang untracked. Đây là checkout cục bộ; không có thay đổi nào được stage, commit hoặc push.

## 2. Những thay đổi đã thực hiện

### 2.1. Firebase, đăng nhập, provisioning và session

Các file chính: [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), [Login.cs](../Assets/_Backend/Login.cs), [SignUp.cs](../Assets/_Backend/SignUp.cs), [Logout.cs](../Assets/_Backend/Logout.cs), [ForgetPassword.cs](../Assets/_Backend/ForgetPassword.cs), [Pin.cs](../Assets/_Backend/Pin.cs), [ChildService.cs](../Assets/_Backend/Service/ChildService.cs), [Childrent.cs](../Assets/_Backend/Childrent.cs), [Mapping.cs](../Assets/_Mapping/Mapping.cs).

- Firebase initialization có một luồng khởi tạo dùng chung, trạng thái lỗi, sự kiện readiness và khả năng retry. Các màn hình chờ readiness thay vì đọc Firebase quá sớm rồi giữ UI ở trạng thái chưa tải.
- Session giữ parent/child hiện tại cùng generation và phát sự kiện khi đổi tài khoản hoặc trẻ. Child selection ưu tiên trẻ hiện đang hợp lệ, sau đó lựa chọn đã nhớ theo owner, cuối cùng là fallback ổn định. Các response cũ bị loại nếu session hoặc scope đã đổi.
- Parent mới được tạo với `provisioningState: "pending"`; retry chỉ hoàn tất seed child mặc định/pet/habit/reward trong trạng thái này rồi chuyển sang `ready`. Parent cũ không có marker không bị auto-seed, và profile `ready` không tạo lại seed đã xóa. Add Child nhận operation ID ổn định và tạo hồ sơ bằng transaction.
- Bản sao password hash/salt tùy biến cạnh Firebase Auth đã được bỏ khỏi đường ghi/đọc mới. PIN mặc định `0000` không còn được seed; người dùng có thể thiết lập PIN ban đầu. Lỗi credentials dùng thông báo không tiết lộ tài khoản có tồn tại hay không, tách khỏi lỗi mạng, rate limit và lỗi provisioning.
- Logout xóa child context và tăng generation để request cũ không cập nhật phiên mới. Các handler đăng nhập, đăng ký, reset mật khẩu và PIN khôi phục trạng thái nút sau khi request kết thúc.
- Child Mode không tạo action sửa/xóa hồ sơ. Ở Parent Mode, thao tác xóa cần click xác nhận riêng và hiển thị trạng thái xác nhận.

**Findings liên quan:** đã xử lý local F05, F13, F17–F19, F21–F23, N04, N09–N11, E09. F01, F02, F14, F15, F24, F25 và E08 mới xử lý được một phần.

**Giới hạn runtime hiện tại:** native Firebase LFS objects đã được hydrate và Auth/Firestore Emulator integration pass trong worktree ngày 24/10; clean-clone/resolver/Android/iOS target build và thiết bị thật chưa kiểm chứng (xem §5.1). Các bullet trên mô tả implementation trong source.

**Giới hạn bảo mật:** Unity hiện vẫn có PIN hash SHA-256/check ở client; backend mới có private verifier, trusted rate limiter/elevation và Admin SDK revoked/disabled-user check trên nhánh local, nhưng API chưa deploy/tích hợp. Parent/Child dùng cùng Firebase Auth identity nên client không tự chứng minh được role phụ huynh. Auth và Firestore không có transaction chung. Inventory 22/10 xác nhận verifier cũ, pet thiếu và orphan subtree; chưa migrate/repair dữ liệu remote.

### 2.2. Habit, lịch, completion, analytics và phần thưởng

Các file chính: [HabitSchedule.cs](../Assets/_Backend/Core/HabitSchedule.cs), [BusinessCalendar.cs](../Assets/_Backend/Core/BusinessCalendar.cs), [HabitRewardProgression.cs](../Assets/_Backend/Core/HabitRewardProgression.cs), [HabitLog.cs](../Assets/_Backend/HabitLog.cs), [Mission.cs](../Assets/_Backend/Mission.cs), [DisplayMission.cs](../Assets/_Backend/DisplayMission.cs), [HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs), [HabitRewardService.cs](../Assets/_Backend/Service/HabitRewardService.cs), [HabitApprovalUI.cs](../Assets/_UI/HabitApprovalUI.cs), [Mapping.cs](../Assets/_Mapping/Mapping.cs).

- Lịch cũ được chuẩn hóa thành dạng có kiểu `day_period`, `local_time` hoặc `unclassified`; quy tắc weekday dùng ISO, xử lý Sunday legacy `0`, lịch rỗng và biên giờ thống nhất. 20:00 được phân loại là buổi tối.
- Habit mới lưu `effectiveFrom`, `createdAt`, loại/giá trị lịch; archive giữ lịch trước ngày archive. Chưa có activation/schedule revisions: tắt isActive vẫn làm ngày cũ chuyển sang unknown (F12), kể cả habit mới. Analytics gom completion theo ngày, dùng approval requirement được lưu lúc completion, không đếm record duplicate xung đột như một completion hợp lệ, và báo coverage unknown cho dữ liệu thiếu provenance.
- Daily log mới có document ID xác định từ habit/date và được upsert vào cùng key. `DailyHabitLogIdentity` giữ nguyên hash format; create/upsert reject mismatch, legacy update chỉ sửa existing document khi habit/date khớp. Đọc Mission lấy log theo ngày; analytics đọc theo range thay vì tải toàn bộ lịch sử lặp lại cho từng habit.
- Bật/tắt completion có rollback UI nếu write lỗi, chặn cập nhật vào item/scope đã thay đổi và xóa `completedAt` khi bỏ completion.
- Parent scene có queue duyệt habit. Khi habit không yêu cầu approval, transaction client ghi completion và cập nhật reward/pet progression; khi cần approval, completion chờ duyệt, và transaction duyệt cập nhật log, gold, EXP, pet level cùng trạng thái reward. `HabitRewardProgression` dùng O(1) phép chia để xử lý EXP qua nhiều level; hai test mới bảo vệ reward lớn và overflow.
- Thói quen bị xóa khỏi UI được archive thay vì hard-delete, nhằm giữ lịch sử cho analytics.

**Findings liên quan:** đã xử lý local F06–F08, F16, N03, N05, N07, N08, N14, E01, E07. F09, F10, F11, F12, F26 và N06 mới xử lý một phần. F03 cũng được cải thiện ở client nhưng chưa có cleanup worker hoặc orphan inventory.

**Giới hạn:** deterministic ID ngăn tạo nhiều document mới cùng key qua đường code này, nhưng không tự giải quyết stale overwrite, conflict giữa true/false hoặc duplicate legacy. Reward transaction chạy từ mobile client; lịch sử 18–19/10 ghi Emulator xác nhận hai approval đồng thời không cấp reward lặp (lượt 22/10 không tái hiện vì native LFS), nhưng client transaction vẫn không phải security boundary và Rules/server authority còn thiếu. Business date dùng UTC+7 ở client; client vẫn cung cấp date key. Legacy habit không có ngày bắt đầu chính xác được giữ là unknown/partial, không được bịa ngày.

### 2.3. Popup, danh sách trẻ, toggle và chart

Các file chính: [AddChild.cs](../Assets/_UI/AddChild.cs), [AddHabitPopup.cs](../Assets/_UI/AddHabitPopup.cs), [UpdateChild.cs](../Assets/_UI/UpdateChild.cs), [MissionUI.cs](../Assets/_UI/MissionUI.cs), [HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs), [Child.unity](../Assets/_Scenes/Child.unity), [Parent.unity](../Assets/_Scenes/Parent.unity).

- Popup thêm trẻ, thêm habit và cập nhật trẻ không còn tự đóng trong `Start`/`Awake` ở lần mở đầu.
- Add Child và Add Habit phân biệt write đã commit với lỗi refresh danh sách. Nếu đã lưu nhưng reload thất bại, form giữ operation ID, khóa sửa dữ liệu đã gửi và cho retry bằng cùng ID trong cùng scope. Pending operation chưa giữ parent/child/generation ban đầu; đổi scope rồi retry có thể ghi sang subtree mới (E11).
- Load trẻ/habits có kết quả success/failure và version/scope guard. Toggle chờ write, rollback khi thất bại và không cập nhật UI sau khi đổi trẻ/session.
- Chart tạo đúng `TextMeshProUGUI`, đặt bars/labels dưới chart root, cleanup khi redraw, không dùng object trong vùng bị destroy làm template; zero bar và trạng thái không có task có biểu diễn riêng, vùng chart mở rộng theo nội dung.
- Nút PIN trong Child scene hiện gọi `Pin.OpenVerifyPopup()` thay vì chỉ `GameObject.SetActive(true)`. Nếu popup mở lại khi verify cũ còn chạy, attempts không reset và nút vẫn disabled tới khi request xong. Target EditMode regression đã fail trước sửa và pass sau sửa. Đây là sửa UI, không phải bảo vệ rate limit server.

**Findings liên quan:** F04, F18–F20, N01–N03, N07, N10, N14 đã xử lý local; E11 một phần do retry chưa giữ scope; F02 có thêm fix scene binding nhưng vẫn partial về security.

### 2.4. Chatbot module

Các file chính: [ChatbotApiClient.cs](../Assets/_Backend/Chatbot/ChatbotApiClient.cs), [ChatbotSseParser.cs](../Assets/_Backend/Chatbot/ChatbotSseParser.cs), [ChatbotContextAdapter.cs](../Assets/_Backend/Chatbot/ChatbotContextAdapter.cs), [FirebaseChatbotContextAdapter.cs](../Assets/_Backend/ChatbotIntegration/FirebaseChatbotContextAdapter.cs), [README.md](../Assets/_Backend/Chatbot/README.md).

- SSE parser xử lý UTF-8 incremental qua chunk, CR/LF/CRLF, BOM và `data:` rỗng; strict EOF không dispatch frame thiếu delimiter. `DispatchEvent` clear eventName cả khi frame không data; regression LF/CRLF/CR từng byte và Chatbot EditMode 29/29 pass trong isolated Unity test copy (E03 local).
- API client yêu cầu HTTPS; HTTP chỉ được cho loopback nếu bật tùy chọn development. Có handle cancel/abort, deadline JSON, idle/total deadline stream, giới hạn byte queue và validation cho meta/delta/done/error terminal events.
- Callback lỗi consumer được tách khỏi lỗi parse/transport; transport tests dùng local HTTP fixture cho status/chunk/cancel.
- Context adapter chuẩn hóa schedule và loại Firebase document IDs khỏi JSON model context; README mô tả trách nhiệm URL, token và child scope thuộc consuming host.

**Findings liên quan:** E02–E04 và E06 đã xử lý local. E03 regression cho frame không data đã pass cùng parser suite 29/29 trong isolated Unity test copy. E11 thuộc create/retry UI và vẫn một phần. E05 có thể truyền cancellation theo snapshot; snapshot tự hủy request khi parent/child đổi và hủy tiếp khi host dispose nó. E05 vẫn partial vì chưa có consuming caller chứng minh host gọi `Dispose` khi view đóng. E12 có loader snapshot scoped theo parent/child/session generation và bounded history range; vẫn partial vì chưa có consuming caller hoặc kiểm thử runtime đổi tài khoản/trẻ. E13 được defer: giữ module-only, chưa tạo UI hoặc kết nối API đã deploy theo quyết định người dùng.

### 2.5. TTS, config và kiểm thử

- [TextToSpeech.cs](../Assets/_Backend/Features/TextToSpeech.cs) chọn `vi-VN`, kiểm tra hỗ trợ/engine và giữ text fallback; câu nói mới dừng coroutine cũ, lifecycle cleanup khi disable/destroy. F28/F29 được xử lý local, chưa chạy trên Android engine/thiết bị thật.
- `Assets/Data/google-services.json` và `.meta` đã xóa sau khi so sánh thấy đây là bản thừa, cùng project/package nhưng thiếu OAuth client entries; test kiểm tra config Android/desktop còn lại cùng app. Việc này chưa chứng minh Android/iOS build hoặc OAuth login thật.
- `FirebaseTester.cs` và `.meta` đã xóa vì script ghi thử nghiệm tự chạy không còn scene/prefab reference.
- Thêm `HABIMON.Core` thuần cho lịch, analytics, business date, child selection và pet progression; thêm EditMode/PlayMode test assemblies. Chatbot API/SSE tests được mở rộng.
- `.gitignore` mở đúng năm tài liệu audit/setup/action plan hiện hành trong `docs/`, không mở toàn bộ thư mục.

## 3. Các findings còn một phần, mở hoặc defer

| Finding | Đã làm trong mobile checkout | Còn thiếu / lý do chưa đóng |
|---|---|---|
| E03 | Incremental UTF-8/empty multiline/strict EOF và reset eventName đã sửa; Chatbot EditMode 29/29 trong tổng suite 63 tests ngày 24/10. | Local parser finding đã xử lý; không cần Firebase/cloud gate cho finding này. |
| E11 | `PendingHabitCreate`/`PendingChildCreate` giữ scope/payload; habit gắn parent/child/generation, child create gắn parent/generation; transaction so sánh payload, explicit retry tại cùng target dùng lại operation ID. Unity EditMode 63/63, popup PlayMode 9/9, Emulator integration 1/1 pass. | Chưa cover Auth account switch, response loss thật, cancel-in-flight, popup destroy hoặc process restart; giữ partial. |
| F01 | Child Mode không hiện action sửa/xóa; Parent delete có bước xác nhận. | Server vẫn phải kiểm tra ownership/elevation; cùng Firebase identity không phân biệt người dùng parent/child. |
| F02 | BE local hiện có Firebase JWT + Admin revoked/disabled-user check (45 tests pass), fresh reauth challenge, private scrypt verifier, limiter và short-lived elevation; migration `0003` chưa lên Neon, Unity chưa gọi API, legacy PIN chưa migrate. | ADC/IAM, deployed runtime, Unity integration và migration còn mở; xem B3 action plan. |
| F03 | Inventory 22/10 xác nhận 3 missing child profiles còn 20 descendants (8 habits, 8 logs, 2 pets, 2 rewards), chưa cleanup/restore. Client chỉ xóa 4 collection known; vẫn thiếu authoritative recursive job, pending-write denial, recovery và retention evidence. | Chưa đạt điều kiện đóng; xem task tương ứng trong action plan. |
| F09 | Client dùng scheduler; inventory có 2 completed logs lệch repeatDays hiện tại, chưa chứng minh sai lịch tại lúc ghi do thiếu revisions. Rules active chưa enforce schedule/business policy. | Chưa đạt điều kiện đóng; xem task tương ứng trong action plan. |
| F10 | Deterministic ID và transaction có trong source; production 64 log đều legacy, có 1 duplicate group gồm 2 records xung đột, 1 thiếu dateKey, 9 dangling habit refs. Chưa migration/conflict resolution hoặc server uniqueness/stale-version enforcement. | Chưa đạt điều kiện đóng; xem task tương ứng trong action plan. |
| F11 | Client queue/transaction/progression có trong source. Production có 58 completed, 8 approved logs nhưng không có rewardGranted field hoặc đủ reward snapshots; chưa ledger/server authority/reconciliation. Không tự cộng lại thưởng từ thiếu receipt. | Chưa đạt điều kiện đóng; xem task tương ứng trong action plan. |
| F12 | Probe disable habit mới vẫn làm ngày cũ 1/1→0/0 unknown; chưa activation/schedule revisions. Inventory 81 habits đều thiếu effectiveFrom/createdAt nghiệp vụ, không thể tự bịa lịch sử từ document createTime. | Chưa đạt điều kiện đóng; xem task tương ứng trong action plan. |
| F14 | Operation ID tạo child+pet có trong source. Inventory 19/51 child thiếu pet (16 parent), 7 không habits, 21 không rewards; chưa repair. Pet/optional seed policy và provenance phải chốt trước backfill. | Chưa đạt điều kiện đóng; xem task tương ứng trong action plan. |
| F15 | pending→ready/recovery có trong source; production 33/33 parent đều markerless. Đã kiểm kê Firestore nhưng chưa inventory đầy đủ Auth owner states, chưa repair/backfill hoặc failure-injection mọi write; không được set ready/pending hàng loạt. | Chưa đạt điều kiện đóng; xem task tương ứng trong action plan. |
| F24 | Đã xóa hai fields khỏi 29 parent có mặt trong inventory; readback 33 parent không còn `passwordHash/passwordSalt`; deployed Rules chặn reintroduction. Còn writer/client ngoài checkout và retention/archive ngoài Firestore chưa kiểm chứng. | Chưa đạt điều kiện đóng; xem B2 action plan. |
| F26 | UTC+7 ở client, completion/reward timestamps có server writes; dateKey vẫn client-supplied. Inventory có 1 log thiếu dateKey; không chứng minh clock tampering hoặc trusted server business-date policy. | Chưa đạt điều kiện đóng; xem task tương ứng trong action plan. |
| F30 | 24/10 local: C# build pass, EditMode 63/63, popup PlayMode 9/9, Rules Emulator 9/9, Auth/Firestore provisioning Emulator 1/1. | Chưa full business authorization, hosted CI hoặc device evidence. |
| F31 | 8 native LFS objects hydrate, `git lfs fsck` pass; native formats valid; C# build và provisioning integration pass. | Chưa clean clone/resolver regeneration/Android/iOS target build hoặc device proof. |
| N06 | Habit mới có effectiveFrom trong source; inventory 81 legacy habits đều thiếu effectiveFrom/createdAt nghiệp vụ. Đã inventory, chưa migration; provenance không có vẫn phải unknown, không lấy document createTime làm ngày bắt đầu chắc chắn. | Chưa đạt điều kiện đóng; xem task tương ứng trong action plan. |
| N12 | Read-only 22/10: Android/iOS ACTIVE, config app/project/package/bundle khớp, Google provider enabled, Android có SHA-1/SHA-256. Chưa so signing certificate của APK thực tế, clean target build/resolver hoặc device login. | Chưa đạt điều kiện đóng; xem task tương ứng trong action plan. |
| E05 | `FirebaseChatbotContextSnapshot` sở hữu cancellation, tự hủy active request khi session đổi và hủy khi host dispose snapshot. | Chưa có consuming caller chứng minh dispose đúng lúc view/host kết thúc. |
| E08 | Marker pending/ready/retry đã có trong source; inventory 33 parent markerless và 19 child thiếu pet. Đã có phạm vi classifier/repair; chưa migrate. Không blind pending/reseed hoặc hồi sinh dữ liệu chủ động xóa. | Chưa đạt điều kiện đóng; xem task tương ứng trong action plan. |
| E10 | PIN UI single-flight/reopen in-flight đã sửa; BE local có rate limit 5/15 phút với attempt được ghi trước so PIN, scrypt verifier và elevation. API chưa deploy/tích hợp; không có Android/Unity end-to-end test. | Chưa đạt điều kiện đóng; xem B3 action plan. |
| F25 | Rules containment owner-scope và legacy-verifier retirement đã deploy/readback trên `habimon-808e2`; emulator 9/9 pass. | Chưa phân biệt Parent/Child cùng UID hoặc enforce hết domain operations; mobile compatibility và parent-authority API chưa chạy end-to-end. |
| E12 | `LoadCurrentSnapshotAsync` đọc dữ liệu theo cùng parent/child, nhận history range có giới hạn, và hủy kết quả nếu session generation đổi trong lúc tải. | Chưa có consuming caller hoặc runtime test A/B/account switch. |
| F27 | Desktop/Editor provider flow báo không hỗ trợ. | Android Build Support/APK/device login được defer theo yêu cầu “Skip for now”. |
| E13 | Module README giữ rõ UI/token/host là trách nhiệm consuming app. | Chat UI, host session flow và end-to-end API chưa triển khai theo lựa chọn module-only. |

Các finding “Đã xử lý local” vẫn có giới hạn platform/runtime tương ứng. Ví dụ F28/F29 chưa kiểm chứng TTS thật trên Android; test config không thay build thật; unit/PlayMode pass không thay Rules/emulator hoặc Firebase production evidence. Danh sách và tiêu chí đóng chính thức vẫn ở [ma trận remediation §9.1](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#91-ma-trận-trạng-thái-60-findings) và [§6](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#6-ma-trận-đóng-finding).

## 4. Inventory file thay đổi

### File tracked được sửa

- Repository/config: `.gitignore`.
- Firebase/auth/session: `Assets/_Backend/FirebaseManager.cs`, `Childrent.cs`, `ForgetPassword.cs`, `Login.cs`, `Logout.cs`, `Pin.cs`, `SignUp.cs`, `Assets/_Backend/Service/ChildService.cs`, `Assets/_Mapping/Mapping.cs`.
- Habit/analytics/UI: `Assets/_Backend/DisplayMission.cs`, `HabitLog.cs`, `Mission.cs`, `Assets/_UI/AddChild.cs`, `AddHabitPopup.cs`, `HabitLogUI.cs`, `MissionUI.cs`, `UpdateChild.cs`, `Assets/_Scenes/Child.unity`, `Parent.unity`.
- TTS: `Assets/_Backend/Features/TextToSpeech.cs`.
- Chatbot: `Assets/_Backend/Chatbot/ChatbotApiClient.cs`, `ChatbotContextAdapter.cs`, `ChatbotSseParser.cs`, `HABIMON.Chatbot.asmdef`, `README.md`, `Tests/EditMode/ChatbotApiContractTests.cs`, `Tests/EditMode/ChatbotSseParserTests.cs`, `Assets/_Backend/ChatbotIntegration/FirebaseChatbotContextAdapter.cs`.

### File bị xóa

- `Assets/Data/google-services.json` và `.meta`: bản config dư, không còn reference.
- `Assets/_Backend/FirebaseTester.cs` và `.meta`: script write thử nghiệm tự chạy, không còn scene/prefab reference.

### Code/test/Unity assets mới

- `Assets/_Backend/Chatbot/ChatbotSessionScope.cs` và `Tests/EditMode/ChatbotSessionScopeTests.cs`, cùng `.meta`.

- `Assets/_Backend/Core/`: `HABIMON.Core.asmdef`, `BusinessCalendar.cs`, `HabitSchedule.cs` (gồm `ChildSelectionPolicy` và logic analytics), `HabitRewardProgression.cs`, `DailyHabitLogIdentity.cs`.
- `Assets/_Backend/Core/Tests/EditMode/`: asmdef, `HabitScheduleContractTests.cs`, `SceneAssetIntegrityTests.cs`.
- `Assets/_Backend/Core/Tests/PlayMode/`: asmdef, `PopupAndChartPlayModeTests.cs`, `FirebaseProvisioningEmulatorTests.cs`.
- `Assets/_Backend/Service/HabitRewardService.cs` và `Assets/_UI/HabitApprovalUI.cs`.
- Firebase/emulator setup mới: `.firebaserc`, `firebase.json`, `firestore.rules`, `firestore.indexes.json`, `package.json`, `package-lock.json`; default project là `habimon-808e2`.
- `.github/workflows/firestore-rules.yml`: Rules CI workflow source cho push/pull request liên quan; actionlint pass, chưa có hosted run.
- `tests/firestore-rules/firestore.rules.test.mjs`: 6 local Emulator tests cho owner scoping, parent self-create, anonymous/cross-owner denial, out-of-scope paths và orphan documents.
- `docs/FIRESTORE_SETUP_2026-10-18.md`: tách rõ deployed public Rules với local prototype; ghi lệnh chạy Emulator tests an toàn.
- Unity `.meta` sidecars được thêm cho các source, asmdef, test và thư mục tương ứng.
- `docs/FIREBASE_REMEDIATION_ACTION_PLAN_2026-10-22.md`: kế hoạch hành động chi tiết mới, chưa thực thi.
- `docs/CODEBASE_AUDIT_FINDINGS_2026-10-18.md`, `docs/CODEBASE_REMEDIATION_PLAN_2026-10-18.md`, `docs/CODEBASE_REVIEW_AND_CHANGE_REPORT_2026-10-18.md` và `docs/FIRESTORE_SETUP_2026-10-18.md` cùng action plan là các tài liệu được mở whitelist riêng trong `.gitignore`.

## 5. Bằng chứng kiểm thử/build — baseline 22/10 (lịch sử)

Các kết quả dưới đây được chạy trên worktree `Nam` tại HEAD `486765987db5726873185235fb1a6c398f6b98c8`, gồm thay đổi tracked/untracked. Đây là baseline lịch sử; trạng thái mới nhất nằm ở §5.1. Chỉ số local không phải kết luận release readiness.

| Kiểm tra | Kết quả | Giới hạn |
|---|---|---|
| `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` | PASS, 0 warning / 0 error | C# compile, không phải APK hoặc native SDK initialization |
| Unity 6000.3.11f1 EditMode | PASS 55/55 | Bao phủ các test hiện có, không bao phủ mọi race/framing case |
| Unity PlayMode | 9 pass, 0 failed, 1 skipped | Explicit Emulator test được skip và chạy riêng bên dưới |
| `npm run test:firestore-rules` | PASS 6/6 | Local owner-scope Rules prototype, không phải deployed/business authorization |
| `FirebaseProvisioningEmulatorTests.ExistingAuthUserIsProvisionedOnLogin` trên Auth/Firestore Emulator | FAILED 1/1 | Fail trước business assertions: native plugin là LFS pointer, `invalid ELF header` |
| Probe analytics dùng trực tiếp `HabitSchedule.cs` | Tái hiện F12 | Ngày cũ từ 1/1 sang 0/0 unknown khi chỉ đổi IsActive=false |
| Probe parser dùng trực tiếp `ChatbotSseParser.cs`/contracts | Tái hiện E03 | Frame không data giữ eventName sang frame sau |
| Firebase MCP readback Rules `habimon-808e2` | Public `allow read, write: if true` | Chỉ đọc policy; không thử truy cập dữ liệu người dùng |

Artifacts của lần chạy: `/tmp/HABIMON-audit-20261022/{editmode,playmode,provisioning}.xml`, logs cùng tên, `results.json`, `probe/Program.cs` và `probe/Probe.csproj`. `/tmp` là lưu trữ tạm, không phải artifact đã version-control. Kết quả và nguyên nhân được ghi trực tiếp trong tài liệu này để không phụ thuộc vào việc các file tạm còn tồn tại. Bản sao Unity dùng để chạy test giữ `Library` và generated dependencies từ workspace, đã dọn sau test; đây không phải clean-clone proof.

**Root cause native:** `Assets/Firebase/Plugins/x86_64/FirebaseCppApp-13_15_0.so` hiện dài 133 byte, nội dung bắt đầu `version https://git-lfs.github.com/spec/v1`, expected LFS object size 80.228.616 byte; Unity báo `DllNotFoundException` / `invalid ELF header`. Quét Assets có 8 pointers: App/Auth/Firestore/Analytics, mỗi thư viện có `.so` và `.bundle`. `.gitattributes` đã dùng LFS cho hai extension; `git lfs` chưa được cài. File gốc và bản sao cùng là pointer. Không hydrate hoặc sửa vendor trong lượt rà soát.

**Bằng chứng lịch sử, không phải kết quả hiện tại:** các tài liệu 18–19/10 ghi EditMode 55/55, PlayMode 9/9, provisioning integration 1/1, Rules 6/6; conditional `UNITY_ANDROID` và các test assemblies compile pass; npm audit/actionlint pass; bản sao không Library pass Editor tests nhưng EDM4U resolve thiếu AndroidPlayer. Các XML `/tmp/HABIMON-e10-*.xml` cũ không còn tại lúc rà soát 22/10. Không tái chạy npm audit, actionlint, Android conditional build hoặc clean resolver trong lượt 22/10; không trình bày chúng như kết quả mới. Provisioning 1/1 cũ chưa tái hiện được ở checkout hiện tại.

## 5.1. Cập nhật kiểm thử/build — 24/10

| Kiểm tra | Kết quả | Giới hạn |
|---|---|---|
| Git LFS/native | 8/8 objects hydrate; `git lfs fsck --objects` pass; bốn Linux `.so` là ELF x86-64, bốn `.bundle` là Mach-O universal | Chưa chạy clean clone hoặc target platform build |
| `dotnet build Assembly-CSharp.csproj -v:minimal` | PASS, 0 warning / 0 error | Compile local assemblies, không phải APK/iOS/device proof |
| Unity 6000.3.11f1 EditMode | PASS 63/63 | Local Editor suite |
| Parent popup/chart PlayMode | PASS 9/9 | Local PlayMode, không phải thiết bị thật |
| `npm run test:firestore-rules` | PASS 9/9 | Rules Emulator, chưa là final business authorization |
| `npm run test:firebase-provisioning` | PASS 1/1 | Auth/Firestore Emulator tại `127.0.0.1`, không gọi production |
| HABIMON-BE | pytest PASS 45/45 với disposable PostgreSQL; migration `0003_parent_security` đạt head | Firebase Admin revoked/disabled checks được mock; chưa có ADC/IAM/live Firebase, Neon hoặc deploy |
| Firebase MCP read-only Rules readback | Authenticated owner-scoped containment; không có public wildcard | Parent/Child cùng UID và domain operations vẫn thiếu authorization |

`FIREBASE_REMEDIATION_ACTION_PLAN_2026-10-22.md` ghi thêm phạm vi E11 integration và các test còn thiếu. Tất cả evidence trên là local/Emulator hoặc read-only cloud readback; không chứng minh deployment hoặc client/API integration.

## 6. Kết luận và phần việc còn lại

**37 local / 21 một phần / 0 mở / 2 defer** theo ma trận 24/10. E11 đã có scope/payload guard và emulator evidence nhưng remains partial do account/lifecycle cases. E03 local pass. F25 containment đã deploy, còn final Parent/Child/domain authorization; F12 revisions, F30 hosted/device evidence và F31 clean-clone/target build còn thiếu. Local tests không thay điều kiện nghiệm thu end-to-end trong [remediation plan §6](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#6-ma-trận-đóng-finding).

1. Khôi phục native Git LFS objects trong môi trường phát triển, rồi chạy lại Auth/Firestore integration. Test UI/Core và C# build không thay bằng chứng Firebase native hoạt động.
2. Hoàn thiện policy server và Rules: parent authorization/elevation, private PIN verifier/rate limit, schema/business authorization, server date/completion/reward authority. Rules containment đã deploy/readback, nhưng chưa phải final policy.
3. Hoàn thiện E11 bằng scope bất biến được thread tới pending create/retry; E03 đã sửa local. Bổ sung activation/schedule revisions cho F12.
4. Inventory read-only 22/10 đã có; còn classifier/migration legacy verifiers, daily duplicates, orphan descendants, partial provisioning và historical schedules/balances; recursive deletion job, pending-delete/retry/retention và authoritative reward ledger còn thiếu.
5. Android/device OAuth/TTS, clean clone/resolver/target build, hosted Rules/Unity CI và chatbot consuming host→actual API→history vẫn chưa được chứng minh. F27/E13 giữ defer đã ghi trong lịch sử, không phải quyết định mới ngày 22/10.

Không stage/commit/push/deploy hoặc ghi/xóa/migrate production data trong lượt rà soát/cập nhật tài liệu. Những thay đổi nghiệp vụ đã mô tả vẫn ở worktree local; docs/Rules/workflow mới cũng chưa thuộc HEAD. Không suy backend bên ngoài repo đã hoặc chưa triển khai chỉ từ checkout này.
