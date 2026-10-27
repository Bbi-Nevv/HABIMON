# HABIMON — Đề xuất khắc phục toàn bộ findings

Lập ngày **18/10/2026**, cập nhật trạng thái **22/10/2026** (Asia/Ho_Chi_Minh). Baseline `main @ 486765987db5726873185235fb1a6c398f6b98c8`. Bằng chứng và phân loại từng ID: [Audit findings](CODEBASE_AUDIT_FINDINGS_2026-10-18.md).

**Kế hoạch hành động chi tiết và inventory Firebase bổ sung ngày 22/10:** [FIREBASE_REMEDIATION_ACTION_PLAN_2026-10-22.md](FIREBASE_REMEDIATION_ACTION_PLAN_2026-10-22.md). Đây là nguồn thứ tự thực hiện cho F25/F24/F02/E10/F03/F10/F14/F15/E08, E03/E11 và bước nền F31; §9.1 ghi các bước đã deploy, đã sửa local và còn thiếu.

## 1. Mục tiêu, phạm vi và những quyết định được đề xuất

Các §1–§8 là **thiết kế/điều kiện nghiệm thu**, không phải danh sách tính năng đã hoàn tất. **§9 là trạng thái worktree được đối chiếu ngày 22/10/2026**; xem phần này trước khi kết luận đã làm được gì. Bao phủ F01–F32, N01–N14, E01–E14. “Khắc phục hoàn toàn” nghĩa là xử lý nguyên nhân, dữ liệu legacy, UI/server consistency và chứng minh acceptance criteria; không chỉ đổi vài dòng C# rồi coi production đã an toàn.

Giữ Unity 6000.3.11f1 và Firebase hiện có; refactor theo luồng, không viết lại app. Giữ module chatbot độc lập với model Firebase; host UI/config/auth là một công việc tích hợp riêng khi roadmap yêu cầu. Không sửa vendor SDK bằng tay để che lỗi ứng dụng. Không dùng dữ liệu giả/seed làm trạng thái live.

Các lựa chọn dưới đây là **đề xuất của audit**, chưa phải quyết định sản phẩm đã phê duyệt:

- Timezone nghiệp vụ mặc định `Asia/Ho_Chi_Minh`; không lấy timezone điện thoại làm nguồn chân lý cho ngày ghi. Nếu cần đa timezone, timezone của child phải có lịch sử hiệu lực.
- ISO weekday 1=Monday…7=Sunday; dateKey Gregorian `yyyy-MM-dd`, numeric formatting dùng invariant culture. Empty repeatDays phải được xác định tường minh: đề xuất không có lịch cho model mới; migration đánh dấu dữ liệu cũ từng mang nghĩa hằng ngày.
- Parent authorization phải kiểm chứng được ở phía tin cậy; PIN chỉ là UX gate nếu vẫn tự so hash trên client. Privileged operations được đưa qua API server có phiên nâng quyền ngắn hạn.
- Habit archive/schedule revisions giữ lịch sử. Hard-delete habit không phải thao tác quản lý hằng ngày; xóa thật dành cho quy trình erasure của child/account.
- Completion chưa duyệt và approved completion là hai số liệu khác nhau. Habit không cần duyệt có thể được server auto-approve; habit cần duyệt không được tự cấp thưởng.
- Sau khi reward được cấp, trẻ không tự uncomplete để nhận lại; parent correction phải có reversal ledger được audit. Nếu sản phẩm chọn policy khác, vẫn giữ idempotency ledger và test tương đương.
- Không tự thêm birth date đầy đủ: đề xuất age + ageAsOf hoặc birthYear nếu đủ cho mục tiêu.
- Giới hạn/deadline chatbot nêu trong R16 là giá trị khởi đầu đề xuất, phải được đối chiếu backend contract và mobile measurements trước khi release.

## 2. Bất biến phải giữ xuyên subsystem

1. Mọi read/render/write gắn với snapshot `(parentId, childId, sessionGeneration)`; response cũ không được đổi UI/session mới.
2. Child đã chọn hợp lệ không bị fallback ghi đè. Selection persisted phải scoped theo parent; chỉ fallback khi chưa có selection hợp lệ.
3. Mọi thao tác destructive/parent-only được server từ chối nếu thiếu quyền; ẩn nút là lớp UX bổ sung.
4. Daily record có khóa duy nhất theo child + habit + businessDate; retry không tạo record hay reward thứ hai.
5. Analytics quá khứ dùng lịch có hiệu lực tại ngày đó và logs/ledger có provenance; thay habit hiện tại không viết lại quá khứ.
6. Write committed, write pending, validation failure, transport failure và refresh failure là trạng thái khác nhau.
7. UI lifecycle và network cancellation có ownership rõ; sau await chỉ render nếu object/scope/version còn hợp lệ.
8. Secrets/verifiers không vào chat context/log/audit artifacts. Firebase document IDs có thể dùng nội bộ để join nhưng không vào model context; child scope API gửi riêng.

## 3. Thứ tự triển khai và dependency

| Giai đoạn | Nhóm | Deliverable độc lập có thể nghiệm thu |
|---|---|---|
| A | R01, R02 | Authorization server/rules và xóa có kiểm soát; chặn destructive ở Child Mode |
| B | R03, R04, R05, R11 | Session/lifecycle rõ, popup dùng được lần đầu, chart không exception |
| C | R07, R08, R06 | Schedule/day-log invariant và historical analytics; migration kiểm soát |
| D | R10, R09 | Provisioning có thể phục hồi; approval/reward nếu thuộc roadmap |
| E | R12, R13, R14 | Platform integration, emulator/tests/CI, repository policy |
| F | R15, R16, R17 | Parser/transport chatbot ổn định và host integration nếu cần |

R13 bắt đầu sớm để cung cấp test harness cho các giai đoạn, không chờ cuối mới viết test. R15 có thể làm sớm độc lập. R06 phụ thuộc schema schedule R07 và daily log R08. R02 phụ thuộc R01. R17 phụ thuộc session R03 và chatbot R15/R16. Không triển khai reward R09 trước uniqueness/authorization R01/R08.

Audit ban đầu ghi `ResolveCurrentChildIdAsync` **HIGH** với callers trong GoogleLogin/Mission/DisplayMission/HabitLogUI. Re-index 19/10 trả `UNKNOWN/target not found` cho method và `LOW` cho `FirebaseManager` class nhưng chỉ liệt kê tài liệu/test; text search hiện tại vẫn thấy calls trong `FirebaseManager`, `Mission`, `DisplayMission` và `HabitLogUI`. Vì graph và source mâu thuẫn, blast radius của method phải được xem là **chưa xác định**, không suy từ LOW/empty rằng không có caller. UnityEvents/bindings luôn cần đối chiếu riêng. Trước mọi commit chạy `detect_changes(scope: all)`; partial/truncated/UNKNOWN phải được xử lý bằng kiểm tra bổ sung.

## 4. Giải pháp theo nhóm

<a id="R01"></a>

### R01 — Authorization phụ huynh và Rules kiểm chứng được

**Bao phủ:** F01, F02, F24, F25, E10. **File client liên quan:** `FirebaseManager.cs`, `Pin.cs`, `Childrent.cs`, `SceneManage.cs`, các mode buttons trong Menu/Child/Parent. Repo mobile có Rules source và Emulator tests cho owner UID isolation. Server-side parent authorization/elevation vẫn phải nằm trong backend do mobile/DB owner phê duyệt vì cùng Firebase UID không biểu diễn Parent/Child role.

1. Xuất/đối chiếu deployed Rules qua operator được cấp quyền; lưu bản secret-free vào Git và viết tests. Không dùng Rules suy đoán để ghi đè production.
2. Child mode vẫn có thể dùng identity owner cho read được phép, nhưng parent-only create/update/delete, approval, reward/gold/EXP và PIN changes không được direct-write bằng cùng identity đó. Server endpoints xác thực Firebase ID token, suy ra parentId từ token, kiểm tra child ownership; không tin parentId/client role/plan do client khai.
3. Parent unlock dùng fresh reauthentication hoặc PIN server verification. Server lưu verifier riêng, có salt và KDF phù hợp, thêm secret pepper trong secret store nếu chọn PIN; không đưa verifier vào user document client đọc. Rate limit theo account/device/IP với backoff/lock expiry và audit; hash mạnh không biến PIN 4 số thành mật khẩu mạnh.
4. Sau unlock, server trả opaque elevation token ngắn hạn, đề xuất 5 phút, bound parent/device/session và scopes. Token chỉ ở bộ nhớ host; logout, hết hạn, app background theo policy hoặc session change phải revoke/clear. Server kiểm tra token ở từng privileged operation. Không gắn permanent parent claim vào cùng identity trẻ đang giữ, không lưu elevation trong collection mà child có thể tự tạo.
5. Onboarding buộc thiết lập PIN riêng nếu dùng PIN, không seed 0000; recovery dùng reauthentication đáng tin, không đặt lại mặc định âm thầm.
6. Childrent không tạo edit/delete trong Child Mode; privileged confirmation hiển thị rõ child name/id context. Điều hướng vào Parent scene vẫn phải có gate ở controller, nhưng scene gate không thay cho server check.
7. Rules deny direct client updates tới credential verifiers, approval, ledger, gold/EXP. Allow read/write khác phải kiểm tra owner, schema/type/allowed fields, child document tồn tại và child không pending deletion. Admin SDK bypass Rules nên API authorization phải có tests riêng.

**Nghiệm thu:** emulator/API tests cho unauthenticated, user A→B, cùng owner nhưng thiếu elevation, token expired/revoked, childId giả, client role/plan giả và field injection đều bị từ chối. Parent hợp lệ thành công. Đóng/mở/reload UI không reset server rate limit; callback PIN không gửi request trùng. Kiểm tra Menu load Parent trực tiếp không cấp quyền ngoài gate. Local Emulator Suite phải dùng project ID `habimon-808e2` để khớp config repo và phải chứng minh mọi Firestore I/O đi tới emulator; không dùng project demo hoặc ghi test data lên production.

<a id="R02"></a>

### R02 — Xóa trẻ có quyền, idempotency và recovery

**Bao phủ:** F01, F03. **Sửa client:** `ChildService.cs`, `FirebaseManager.cs`, `Childrent.cs`; server đề xuất child deletion service/job.

Luồng: confirm parent→server kiểm tra elevation/ownership→transaction đánh dấu `deletionState=pending`, tạo job với operationId cố định→client bỏ chọn/ẩn child→worker xóa các descendants theo batches→kiểm tra hết dữ liệu thuộc scope→xóa document child cuối cùng→job completed. Worker phải duyệt descendants thật, không chỉ bốn collection hiện biết; domain additions phải được đưa vào registry deletion. Jobs/audit đặt ngoài subtree bị xóa, không giữ tên/PII không cần thiết.

Pending deletion chặn new writes ở Rules và API; response pending không được báo đã erasure hoàn toàn. Worker retry tiếp từ checkpoint, không tạo job thứ hai; lỗi có trạng thái failed/retryable, UI có thể đọc tiến độ. Backup/retention, storage và chatbot session deletion chỉ áp dụng khi có liên kết scope được chứng minh; không suy ra backend đã có API erase Firebase child.

Legacy orphan audit phải tìm cả parent document không tồn tại, ghi inventory secret-free và owner mapping, có retention decision/backup trước cleanup. Không thể phát hiện toàn bộ orphan chỉ bằng query danh sách child đang tồn tại. Không cho client chạy recursive delete với quyền rộng.

**Nghiệm thu:** emulator fixture child có nested collections; thiếu quyền bị deny; lỗi ở batch giữa chừng rồi retry vẫn xóa đủ và không xóa sibling; write trong pending deletion bị deny; job completed chỉ khi subtree sạch. Parent UI confirm đúng trẻ; Child Mode không có action. Remote erasure/backup policy cần operator evidence trước đóng F03 production.

<a id="R03"></a>

### R03 — Session context duy nhất và chọn trẻ ổn định

**Bao phủ:** F05, F13, F17, F21, N09, N10, N07, E07, E12. **Sửa:** `FirebaseManager.cs`, `Childrent.cs`, `Mission.cs`, `DisplayMission.cs`, `HabitLogUI.cs`; **mới đề xuất:** `Assets/_Backend/SessionContext.cs`, test session transitions.

SessionContext chứa parentId, selectedChildId, generation và Changed event. Login/logout/auth-state changes và chọn/xóa trẻ tăng generation. Logout luôn clear selection/caches/elevation ngay cả khi auth đã null. Mọi response mang snapshot lấy trước await; không cập nhật session nếu generation/parent thay đổi.

Đổi resolver thành `EnsureSelectedChildAsync`: ưu tiên selection hợp lệ còn thuộc parent, kế đó remembered child của parent đã verify tồn tại, cuối cùng default có quy tắc tường minh. Chỉ gán kết quả fallback nếu context chưa đổi trong thời gian query. Remembered IDs lưu theo UID; không tái sử dụng selection của user A cho B. Xóa child chọn hiện tại chọn lại theo cùng policy hoặc về empty state.

Childrent chỉ publish selection, không tự GetComponent<Mission> trên object Child Scene. Mission và DisplayMission subscription riêng reload đúng controller; popup scope bám child được mở, không đổi theo global field giữa write. HabitLogUI subscription đổi trẻ cũng reset data/loading để không hiển thị A dưới label B.

Requests từng screen có local version; fetch vào local lists, kiểm tra generation/version/lifecycle, rồi commit UI atomically. Cancellation có thể chỉ bỏ kết quả vì Firebase SDK không phải API nào cũng hỗ trợ cancel transport; không tuyên bố server write được rollback do bỏ kết quả.

**Nghiệm thu:** delayed responses A→B→A, logout A trong read rồi login B, child bị xóa từ thiết bị khác, remembered ID stale, response resolve về sau selection mới; tất cả giữ đúng snapshot. Chỉ request mới nhất spawn/render; N10 không duplicate items. Test handlers unsubscribe khi destroy/disable đúng ownership. Changes ảnh hưởng HIGH nên chạy cả auth/child/mission/dashboard regression.

<a id="R04"></a>

### R04 — Popup lifecycle và kết quả submit rõ ràng

**Bao phủ:** F04, F20, N01, N02, E11. **Sửa:** `AddChild.cs`, `UpdateChild.cs`, `AddHabitPopup.cs`, `Mission.cs`, `Parent.unity`.

Giữ trạng thái inactive serialized của popup. Bỏ logic đóng chính popup/ancestor khỏi Start/Awake của script nằm trong popup. Init dropdown/listeners ở Awake một lần nếu có thể cần gọi Open trước Start; Open chuẩn bị data, captured scope, pending/status rồi mới bật panel. UpdateChild không gọi lại Awake thủ công. Không đổi hierarchy hàng loạt nếu bỏ self-close và bind một lần đủ sửa.

`SubmitHabitFromPopupAsync` trả result có `writeCommitted`, entityId, errorCode, message; service không return thành công giả khi readiness/context thiếu. Lock submit bằng isSubmitting trước await và try/catch/finally. AddChild dùng cùng pattern thay vì chỉ disable button; giữ UpdateChild isSaving/version đang có.

Đề xuất khóa cancel trong create đã gửi write, cho phép đóng sau kết quả; nếu sản phẩm yêu cầu cancel UI, hiển thị operation vẫn có thể hoàn tất, tracking operationId để không retry tạo mới vô ý. Write-success/refresh-failure trả saved và banner reload retry, không ép người dùng gửi lại create. Pending operation phải giữ bất biến parentId/childId/sessionGeneration và payload từ lần gửi đầu; đổi scope phải chặn retry hoặc yêu cầu operation mới rõ ràng, không tái sử dụng ID trong subtree khác (E11 vẫn thiếu). Clear inputs/close chỉ sau write commit hoặc cancel rõ ràng; reload danh sách không quyết định write có thành công hay không.

**Nghiệm thu:** mở lần đầu/lần sau mỗi popup, child A/B, invalid input, permission-denied, cancel trong pending, double click, write success+refresh fail. Không popup tự đóng, không listener trùng, không sửa nhầm child; saved entity chỉ một lần; pending/failure không mất inputs.

<a id="R05"></a>

### R05 — Chart có prefab đúng, cleanup đúng và biểu diễn đúng

**Bao phủ:** F06, N03, N14, E01. **Sửa:** `HabitLogUI.cs`, `Parent.unity`; **mới đề xuất:** `Assets/_Prefab/HabitChartBar.prefab`, các references TMP label/value trong prefab.

Bar prefab asset độc lập có RectTransform+Image trên bar object, label/value là child riêng có TextMeshProUGUI. Không gắn Image và TMP cùng GameObject Graphic. Gán asset GUID trong Inspector, không dùng template là descendant chartRoot. Track một generated-elements root hoặc list owned objects; clear chỉ owned output, không destroy static title/template.

Nếu vẫn cần fallback runtime, luôn SetParent(chartRoot,false), layer UI, component concrete; dùng cùng cleanup ownership. Instantiate/lookup validation thất bại trả lỗi chart rõ ràng, không tiếp tục dereference null Image. Với prefab mandatory, có thể bỏ fallback sau migration binding và validation, tránh hai renderer khác nhau.

Ratio=0 thì bar height=0; tạo baseline/marker riêng nếu cần click target. Ratio từ completed/total phải hợp lệ trong [0,1]; total=0 có nhãn không có lịch, không mặc nhiên 0% thất bại. Month dùng contentWidth>=days*minSpacing và horizontal scrolling hoặc thưa nhãn; đề xuất minSpacing=64 UI units, labels không vượt cell. Đây là UI design lựa chọn cần test màn hình nhỏ và localization.

**Nghiệm thu:** render ba mode liên tục ít nhất 20 lần, zero/all-complete/no-task, 28/29/30/31 ngày, viewport 320/600/1080 UI units; không exception, không root bars bị orphan, template asset còn nguyên, labels đọc được. Play Mode assert generated object count không tăng qua các lần redraw. Analytics tests độc lập với rendering.

<a id="R06"></a>

### R06 — Analytics ngày/tháng/năm từ lịch sử có provenance

**Bao phủ:** F07, F12, F13, N06, E07. **Sửa:** `HabitLogUI.cs`, `Mapping.cs`, retrieval service; **mới đề xuất:** `Assets/_Backend/Service/HabitAnalytics.cs` và tests thuần C#.

Tách tính toán khỏi MonoBehaviour, input immutable snapshot với scope, schedule revisions và daily records. Denominator của ngày dựa lịch có hiệu lực ngày đó; numerator dùng completion/approval state được quy định. Month tổng hợp mọi date trong `[monthStart,nextMonthStart)`. Year mỗi điểm month tổng hợp cả tháng, tổng summary bằng tổng points. Year leap tính đủ 366 ngày, không sample ngày 1.

Archive/current isActive không làm biến mất scheduled occurrence quá khứ. Nếu child/history chưa có dữ liệu được chứng minh, hiển thị coverage unknown/partial, không invent missing history. Future periods nếu cho navigation phải phân biệt planned và observed; không trộn future planned denominator vào tỉ lệ đã thực hiện mà không label.

Refresh fetch local habits/revisions/logs; sau await kiểm tra generation/version, swap snapshot một lần. Không clear/AddRange shared lists từng await. Requests mới do R03 child subscription phải được bảo vệ từ đầu.

**Nghiệm thu:** completion ngày 18 vẫn tính trong Year; tháng 28/29/30/31, leap/non-leap, habit created giữa tháng, lịch đổi giữa tuần, archive/delete management không đổi tháng cũ, pending approval tách số liệu, duplicates legacy flagged, dữ liệu unknown không thành 0. Đối chiếu daily sums=monthly/yearly sums. Concurrent refresh không trộn A/B.

<a id="R07"></a>

### R07 — Schedule, effective dates và icon catalog

**Bao phủ:** F08, F09, F12, N05, N06, F26. **Sửa:** `Mapping.cs`, `Mission.cs`, `DisplayMission.cs`, `HabitLogUI.cs`, `AddHabitPopup.cs`, `FirebaseManager.cs`, chatbot bridge, `Parent.unity`.

Schema đề xuất habit có scheduleKind (`day_period`, `local_time`), scheduleValue, ISO repeatDays, timezone, archivedAt và schedule revision reference. Revisions lưu immutable `effectiveFrom` inclusive/`effectiveTo` exclusive, activation/lịch/reward/approval rule tại thời điểm đó. Không rewrite revision đã dùng; sửa hiện tại mở revision mới từ businessDate được server cho phép.

Một pure scheduler dùng chung để list UI, validate write server và analytics. Quy tắc clock→period phải được chốt; đề xuất morning 05:00–11:59, afternoon 12:00–17:59, evening còn lại, chỉ để lọc UI; giữ giờ exact riêng. Các khoảng này chưa phải yêu cầu đã thống nhất. Không nhận giá trị lạ bằng substring như authoritative schedule.

Migration `20:00`→local_time, nhãn Việt→day_period; invalid non-empty giữ unknown/unclassified để repair, không tự ép hằng ngày. effectiveFrom legacy chỉ backfill khi có bằng chứng từ timestamps/logs/import history; thời điểm log sớm nhất là lower bound, không chứng minh ngày tạo. Ghi provenance/coverageStart và unknown periods.

Icon dùng catalog stable key→Sprite, ví dụ book→asset đã verify; không dùng Sprite.name làm database ID. Map legacy icon_book/icon_default/asset-name tới keys mới; unresolved dùng fallback Sprite reference thực có. Resolve Multiple Sprite bằng Editor AssetDatabase và chọn subasset đúng; kiểm tra cả AddHabitPopup/MissionUI/DisplayMission. Không rename assets hàng loạt để ép khớp.

**Nghiệm thu:** bảy weekday, Sunday legacy 0/7, malformed repeatDays, empty policy, boundaries clock→period, giờ 20:00 trong tab Tối, revise schedule không đổi quá khứ. Icon keys default/custom/unknown resolve đúng; homework subasset được Editor load và render. Không tự nâng unknown lịch legacy thành dữ liệu chắc chắn.

<a id="R08"></a>

### R08 — Daily completion duy nhất và server-authoritative

**Bao phủ:** F09, F10, F16, F26, N08. **Sửa:** `HabitLog.cs`, `FirebaseManager.cs`, `DisplayMission.cs`, `Mapping.cs`; server daily completion service/tests.

Đề xuất record key `habitId + "_" + yyyy-MM-dd` trong scope child, chỉ khi habitId được service sinh và không mơ hồ; nếu nhập ID ngoài schema, encode key deterministic. Firestore transaction/server operation đọc habit revision và record, kiểm tra owner/child-active/schedule/date policy, cập nhật record theo expectedVersion. Set state desired=true/false, không lật trạng thái bằng toggle trên server, để retry idempotent.

Server xác định businessDate từ clock/timezone canonical. Client gửi date dự kiến để phát hiện stale/midnight, không tự được sửa ngày ngoài policy. Timestamp server cho createdAt/updatedAt/completedAt. False state: completedAt=null, updatedAt ghi thời điểm sửa; nếu cần audit giữ transition event. Approval/rewarded records không được child tự sửa trái state machine R09.

ID deterministic chống duplicate document nhưng **không tự chống stale false overwrite true**; cần expectedVersion/serialize per item và server conflict result. RequestId idempotent lưu để response lost+retry không ghi event/reward lần nữa.

Đọc màn hình: query một lần theo businessDate hoặc direct-get deterministic records, tạo dictionary habitId→state. History query range dateKey và pagination; không đọc toàn collection cho từng item. Cache scoped session+child+date và invalidated khi write/delete/logout/midnight. Index requirements phải được ghi trong config backend và kiểm thử thực query.

**Nghiệm thu:** 20 concurrent same-day requests không duplicate; true→false có completedAt semantics đúng; stale expectedVersion rejected; response lost+retry idempotent; unscheduled/inactive/foreign child writes deny; change device clock không cho rewrite lịch sử. Read instrumentation chứng minh không còn N full-collection calls; billed reads vẫn cần đo riêng.

<a id="R09"></a>

### R09 — Approval, rewards và pet progression theo roadmap

**Bao phủ:** F11 và khoảng trống gamification. Người dùng đã xác nhận approval/reward nằm trong scope. Checkout hiện có approval UI, Firestore transactions và pure pet progression calculator; phần cần thêm để đóng finding là server approval/reward authority, ledger và authorization tests. Không coi client transaction là security boundary.

State machine: scheduled→submitted→approved/rejected; no-approval habit server có thể approved trực tiếp. Dashboard tách submitted/approved và reward status. Child không ghi isApproved/ledger/balances trực tiếp. Parent approval cần elevation và expectedVersion.

Transaction authoritative tạo reward ledger với unique key dailyRecordId+grant kind và cộng balances/EXP một lần; duplicate approval/retry trả kết quả đã grant. Transaction retry không gửi side effects ngoài DB trong callback. Pet level/stage từ pure rule table có version, không tăng tùy UI callback. Reward purchase/spend có ledger, kiểm tra balance không âm và stock/unlock policy.

Uncomplete sau granted mặc định child bị deny; parent reversal tạo entry bù, không xóa audit grant. Legacy logs approved/complete nhưng không có ledger **không được tự cộng retroactive** nếu chưa có reconciliation policy, vì không biết đã thưởng qua công cụ khác hay chưa.

**Nghiệm thu:** pending không cấp thưởng, parent deny thiếu quyền, duplicate approval đa thiết bị chỉ một ledger/grant, retry/injected transaction conflict không double reward, reject/uncomplete/reversal đúng policy, spend không âm, pet progression leap boundaries đúng. Roadmap defer phải được ghi là GAP được chấp nhận, không đánh dấu feature đã hoàn thiện.

<a id="R10"></a>

### R10 — Provisioning, readiness và authentication recovery

**Bao phủ:** F14, F15, F22, F23, F24, N11, E08, E09, E11. **Sửa:** `FirebaseManager.cs`, `ChildService.cs`, `SignUp.cs`, `Login.cs`, `GoogleLogin.cs`; server provisioning đề xuất.

Firebase init state enum Uninitialized/Initializing/Ready/Failed, Task readiness dùng chung và structured error. Retry single-flight có policy; không polling sleep tùy ý. UI subscribe readiness hoặc await Task, có loading/retry; scene thiếu manager hiển thị lỗi rõ, không tự gọi production từ test.

Sau Auth thành công, chạy provisioning idempotent cho UID. Firestore bootstrap parent/child/pet và optional examples được batched/transaction phù hợp; seed child ID được reserved ổn định bởi server/provisioning record, không mỗi retry sinh GUID mới. `provisioningVersion/state` cho phép login/email/Google đều repair các phần **thiếu**, không reset pet progression hay overwrite edits đã tồn tại. Chỉ có child count>0 không chứng minh provisioning đủ. AddChild dùng cùng service khởi tạo child; sample habit/reward optional theo policy chung.

Auth và Firestore không atomic chung: ưu tiên giữ Auth user với provisioning=pending/retryable, cho resume sau login; UI phân biệt authenticated-profile-pending với invalid credentials. Không tự xóa user đã có dữ liệu do lỗi mạng. SignUp single-flight try/finally; duplicate requests server dùng operationId. Error codes tách credentials/network/dependency/profile/permission, thông báo credentials chung tránh user enumeration; UI dùng result thay hardcode.

Bỏ ghi passwordHash/passwordSalt mới; migration xóa verifiers cũ bằng server job có audit count/backup policy. HashPin cũng chuyển sang server verifier R01, không để model removal bị hiểu là đã xóa data. Không đổi mật khẩu người dùng hoặc migrate khỏi Firebase Auth.

**Nghiệm thu:** inject failure sau từng write Auth/parent/child/pet/habit/reward; retry/login tạo một child và repair đủ, không reset entity người dùng sửa. Google đã có child thiếu pet vẫn repair. Ready delayed/fail/retry thông báo đúng; concurrent signup một logical operation; password verifiers không còn trong fresh write và legacy migration được kiểm kê riêng.

<a id="R11"></a>

### R11 — Async UI, mutation scope và listener ownership

**Bao phủ:** F17, F18, F19, F20, N04, N07, N10, N11, E09, E10, E11. **Sửa:** controllers UI hiện có, không thay hàng loạt async void khi chúng là event handlers.

Mỗi handler gọi Task method có catch/result và try/finally; isSubmitting/isVerifying được set trước await. Error UI phân loại failure; finally chỉ cập nhật object còn sống và request còn thuộc scope hợp lệ. OnDestroy/scene change vô hiệu generation/cancel; after-await check Unity object bằng overload `this != null`/component != null, không chỉ CLR null semantics.

Mutation bám parent/child/habitId lúc item bind; không refresh global selection rồi ghi item cũ vào child mới. Disable pending toggle, remember previousState, on success commit local/cache; on failure rollback nếu version còn đúng. Nếu write committed nhưng refresh fail, thông báo saved+retry refresh. Serialized per-item writes/expectedVersion R08 giải quyết rapid toggle, không dùng inversion `!isOn` nếu state đã thay đổi tiếp.

Logout dùng stable method group; chọn bind một lần Awake/Start hoặc OnEnable/OnDisable đối xứng. RemoveListener đúng callback do component sở hữu, không RemoveAllListeners trên button dùng chung. PIN lock attempt count có UI single-flight nhưng server vẫn là authority R01.

**Nghiệm thu:** delay rồi destroy item/scene, permission-denied, timeout, repeated settings enable 10 lần→một callback, foreign runtime listener vẫn còn; toggle pending/success/failure đúng, chọn trẻ giữa write không đổi scope; UI không stuck disabled, lỗi không unobserved. GoogleLogin/UpdateChild đã có bảo vệ phải giữ và test, không refactor thành pattern kém hơn.

<a id="R12"></a>

### R12 — Firebase configuration và kiểm thử platform/TTS

**Bao phủ:** F27, F28, F29, N12. **Sửa:** `GoogleLogin.cs`/provider integration khi cần, `TextToSpeech.cs`, configuration source/documentation; Gradle/resolver chỉ sửa khi có lỗi được chứng minh.

Quy định một mobile config authoritative từ Firebase project/app đúng environment; dùng resolver sinh Android resources/desktop config, ghi provenance và quy trình regeneration. So project ID, package/bundle ID, app ID, OAuth client types, fingerprint theo operator config; không in keys vào logs/doc. Config khác chưa đủ lý do xóa một bản; clean-clone resolver/build chứng minh dependency paths.

Desktop Editor: disable/nêu rõ Google provider flow không hỗ trợ hoặc dùng email/emulator/test identity cho tests. Mobile OAuth phải thử trên thiết bị, kiểm tra provider enabled, fingerprints, app IDs theo docs SDK hiện tại. Không thay bằng desktop workaround rồi tuyên bố Android pass.

TTS init thành công xong set vi-VN, check supported/data-installed; fallback hiển thị text và thông báo phù hợp nếu unsupported. Giữ một display coroutine hoặc generation; Speak mới cancel coroutine cũ. OnDisable/Destroy dọn box, queue và engine đúng ownership; verify callback thread→main-thread nếu thao tác Unity objects được thêm. Locale/engine tests cần Android thật; Editor tests chỉ kiểm tra display lifecycle.

**Nghiệm thu:** Android release build/resolver từ checkout sạch; Google login success/cancel/offline/account switch; TTS tiếng Việt có/thiếu engine data; Speak liên tục câu mới không bị coroutine cũ ẩn. iOS/tvOS chỉ đánh dấu nếu mục tiêu release và thực test; không coi Apple assets là iOS readiness.

<a id="R13"></a>

### R13 — Tests, emulator và release evidence

**Bao phủ:** F25, F30, N13 và acceptance của toàn bộ nhóm. **Mới đề xuất:** `Assets/Tests/EditMode/HabitScheduleTests.cs`, `HabitAnalyticsTests.cs`, `SessionContextTests.cs`; `Assets/Tests/PlayMode/PopupLifecycleTests.cs`, `ChartRenderingTests.cs`, `AsyncUiLifecycleTests.cs`; Rules/API tests ở backend repo; CI workflow khi project đã chọn license/runner.

Pure logic tách assembly để tests không cần Firebase network. Fake repository có controllable completion order/failure cho A/B races. Play Mode dùng fakes, không load production Firebase initialization. Firebase Emulator Suite dùng project ID `habimon-808e2` và phải assert emulator host/config trước mọi test write; không dùng `demo-habimon-local` hoặc production database. FirebaseTester chuyển thành test-only Editor/test assembly hoặc bỏ scene-attachable auto write; chỉ fixture emulator được gọi tường minh.

Run toàn bộ chatbot tests hiện có và mở rộng R15/R16/R17; không thay assertions bằng implementation mirror. Transport tests dùng local HTTP server để kiểm tra actual status/body/chunks/cancel, không chỉ URL string assertions. CI: Unity compile+EditMode+PlayMode, Rules tests, target Android smoke build khi runner có module/license; artifacts XML/log secret-free và commit SHA. Không đưa license/credentials vào Git.

**Nghiệm thu:** clean clone compile/test/build có logs, zero failed theo suite áp dụng; có negative Rules tests và delayed race tests. Production release requires real target-device/API evidence; emulator pass không thay deployed Rules proof. Historical plan ghi 8 pass không được dùng làm kết quả của 10 tests HEAD.

<a id="R14"></a>

### R14 — Vendor, tuổi và tài liệu được lưu bền vững

**Bao phủ:** F31, F32, E14. **Thay đổi local:** `.gitignore` whitelist năm tài liệu audit/setup/action plan; ghi policy nguồn authoritative dưới đây; `ageAsOf` đã được thêm vào child model/UI. Không cleanup vendor trong patch sửa nghiệp vụ.

**Policy nguồn authoritative và xử lý:** Unity registry packages được pin bởi `Packages/manifest.json` + `Packages/packages-lock.json`; không sửa PackageCache. Firebase Unity SDK hiện nằm ở `Assets/Firebase`, với `Editor/*_version-*_manifest.txt` và `Editor/*Dependencies.xml` ghi phiên bản/import requirements; EDM4U có manifest phiên bản riêng và `ProjectSettings/AndroidResolverDependencies.xml` ghi kết quả resolve Android. `Assets/Firebase/m2repository` là kho Maven nguồn của SDK; `Assets/GeneratedLocalRepo` là local resolver output; không sửa tay output. Giữ artifacts đang được project tham chiếu trong Git cho đến khi clean-clone resolve/build target platform chứng minh có thể tái tạo chúng; hiện clean Android build chưa chạy vì Android Build Support đã được defer. `Assets/_Recovery/0.unity` không thuộc Build Settings; giữ lại cho đến khi owner quyết định archive/delete. `.gitattributes` hiện đã cấu hình LFS cho `*.so`/`*.bundle`; 8 native Firebase files còn là pointers và máy thiếu `git lfs` (22/10). Cần hydrate đúng objects rồi kiểm chứng native loading; không sửa tay hoặc xóa vendor binaries để che lỗi.

`Assets/Plugins/Android/google-services.json` và `Assets/Data/GoogleService-Info.plist` là Firebase app config của project hiện tại; không xem Firebase client config là server secret, cũng không thay bằng file của project khác. `.gitignore` whitelist đúng năm tài liệu audit/setup/action plan hiện hành; các docs local khác vẫn bị ignore.

Age policy implemented locally: new/updated child profiles store `ageAsOf`; no DOB is collected. Legacy data without `ageAsOf` is treated as unknown/as-reported, not dated using document creation time.

**Nghiệm thu:** clean clone cần có đúng năm docs được allowlist, links hoạt động và không có files local ngoài scope; clean-clone resolver/target build phải chứng minh generated dependencies có thể tái tạo; legacy age unknown được trình bày đúng mà không thu thêm PII. Docs/ignore policy đã sửa local. Bản sao cô lập không có Library pass Editor tests, nhưng EDM4U Android resolve không hoàn tất do AndroidPlayer target chưa cài; chưa có clean Git clone hoặc target build proof.

<a id="R15"></a>

### R15 — SSE parser chịu arbitrary chunking và có framing contract

**Bao phủ:** E02, E03. **Sửa:** `ChatbotSseParser.cs`, `ChatbotSseParserTests.cs`, phối hợp client terminal validation.

Decoder.Convert loop giải mã với char buffer hữu hạn, theo bytesUsed/charsUsed/completed; feed từng phần vào line parser, không giả định count<=4096. Giữ state UTF-8 qua chunks, policy invalid UTF-8 phải tường minh. Flush decoder EOF không dispatch frame thiếu delimiter nếu chọn strict standard.

Theo chuẩn, mỗi data field append value+LF kể cả empty; khi blank separator, nếu có data thì bỏ LF cuối và dispatch, sau đó reset state. Track có data field riêng với Length để phân biệt `data:` rỗng và event-only. Khi kết thúc frame không có data vẫn phải xóa event-type buffer; thêm regression `event: done\n\ndata: payload\n\n` phải phát `message`, không phải `done` (source 22/10 chưa đạt). Chuẩn xử lý CR/LF/CRLF, optional UTF-8 BOM đầu stream, comments; id/retry có thể ignore vì client chưa reconnect, nhưng phải ghi rõ không có resume guarantee.

EOF hiện có tests cho permissive flush. Đề xuất strict SSE và update backend/tests cùng lúc; nếu backend contract bắt buộc legacy EOF, đưa compatibility option explicit và không coi event-only/truncated done là success. Terminal done/error phải parse/validate theo schema backend đã version, không chỉ tên event; giữ error event khác transport error.

**Nghiệm thu:** 4096/4097/8192/64KiB chunks, nhiều frame coalesced, mỗi byte một chunk, multibyte tiếng Việt/emoji split, CRLF split, empty-first/middle/last data lines, comments/BOM, event-only, EOF giữa JSON/UTF-8, done thiếu payload/delimiter, invalid UTF-8, oversized line/event. Feed output độc lập cách network chia chunks; expected newline đúng; malformed/incomplete không báo completed.

<a id="R16"></a>

### R16 — HTTP/SSE client có deadlines, cancellation và error contract

**Bao phủ:** E04, E05, E06; hỗ trợ E02/E03. **Sửa:** `ChatbotApiClient.cs`, contracts, tests mới `ChatbotApiTransportTests.cs`.

Tách parse response try/catch khỏi invoke onSuccess. Validate DTO required fields trước success; callback exceptions được báo là consumer error/log có ownership hoặc propagate theo contract, không đổi thành invalid_response. OnError/OnEvent invocation policy nhất quán; network/protocol failures chỉ phát terminal outcome một lần. Không catch Exception indiscriminately rồi nuốt mọi lỗi.

Thêm request operation handle có Cancel→Abort, Dispose idempotent; host gọi khi đổi child/logout/destroy. Using/finally cleanup vẫn cần kiểm thử Unity coroutine stop semantics, không giả định StopCoroutine hoàn tất network write rollback. Với request JSON đề xuất total deadline 30 giây; stream idle deadline 45 giây và total 180 giây, reset idle khi nhận progress/heartbeat hợp lệ. UnityWebRequest.timeout là thêm bảo vệ, không thay idle timer. Các số này cần điều chỉnh theo backend SLA.

Đề xuất caps line=64KiB, event=256KiB, buffered queued bytes=1MiB; overflow trả protocol/resource-limit error và Abort, không silent drop bytes. Không auto-retry chat POST/create sau response lost khi backend chưa có idempotency key; GET có bounded retry. Nếu POST retry được hỗ trợ, operationId ổn định và server dedupe được contract-test.

Constructor production chỉ HTTPS; HTTP chỉ cho loopback/local test với explicit development policy, không chấp nhận wildcard arbitrary host. URL base không có userinfo/query/fragment để route concat không sai. Không log owner token/context; error codes/status safe. Secure token storage/lifecycle do host R17; frontend submitted plan không tự là entitlement.

**Nghiệm thu:** JSON hợp lệ+consumer throw không thành invalid_response; invalid/missing required DTO báo protocol failure; 401/403/429/5xx/offline/truncated stream/cancel/timeout có outcome đúng, request disposed; queue caps; HTTP nonlocal reject, local test allow theo policy; token không trong logs. Không ràng buộc error payload schema chưa đối chiếu backend.

<a id="R17"></a>

### R17 — Host integration chatbot đúng child, dữ liệu và privacy

**Bao phủ:** E12, E13. **Files đề xuất khi roadmap yêu cầu:** `Assets/_Backend/ChatbotIntegration/ChatbotSessionController.cs`, snapshot loader, secure credential provider interface và UI owned bởi developer UI; không sửa core module thành MonoBehaviour phụ thuộc Firebase.

Host lấy SessionContext snapshot R03, fetch child/habits/logs/rewards/pet cùng scope; only commit khi generation còn đúng. Context builder nhận envelope có parent/child/version/capturedAt/provenance, nhưng IDs dùng internal validation không serialize vào model context. Nếu pet không có fetch API/live provenance, truyền null; read failure không thành empty array/zero giả. Placeholder child hiện null semantics phải giữ.

Token installation high entropy do secure storage của host cung cấp; logout/account switch phải có policy namespace/isolation, tránh cùng installation token vô tình lộ session user A cho B. Nếu backend owner identity khác Firebase UID, phải contract rõ mapping/authorization; childId hash không tự chứng minh child ownership. Server xác thực entitlement/quota, không tin plan client. Chưa audit backend HABIMON-BE nên không giả định endpoints/schema/hashing/rules đã đúng.

Session ID scoped theo child và account; switch child Abort stream, clear/render history đúng scope, fetch lại sessions. Điều kiện stale-result, send duplicate và reconnect đều được version/operation quản lý. Context limits/history range dùng contract backend, tối thiểu hóa tên/tuổi/dữ liệu logs theo product privacy; parent consent/retention cần policy có thẩm quyền, không chỉ loại bỏ document IDs.

**Nghiệm thu:** browser/mobile→actual API→stored session/history end-to-end trên môi trường được cấp quyền; A/B/A và account switch không trộn context/history; owner/child scope mismatch deny; unknown pet/profile không fabricate; chunks tiếng Việt đúng; token tồn tại an toàn sau restart/revoked theo policy. Nếu scope chỉ module, ghi E13 accepted GAP, không tự thêm UI ngoài yêu cầu.

## 5. Schema migration và rollout không mất dữ liệu

### 5.1. Kiểm kê trước migration

Operator chạy read-only inventory trên môi trường được cấp quyền: child profiles/provisioning thiếu, orphan descendants, duplicate `(child,habit,dateKey)`, timestamps bất thường, legacy schedules/icons, hashes tồn tại, balances/approval không có ledger. Báo cáo chỉ counts và scoped identifiers cần thiết; không export credentials/PII vào repo. Backup/retention theo policy trước mọi destructive repair. Audit này chưa chạy inventory remote.

### 5.2. Migration từng nhóm

1. **Credentials:** stop writes verifiers mới trước; deploy private server verifier/R01 và onboarding/recovery; migrate PIN theo yêu cầu reset/reauth, không đổi về 0000; xóa password verifiers cũ có tracking.
2. **Provisioning:** thêm version/state, stable seed reservation; repair missing fields/docs theo policy, không overwrite live progression hoặc user edits. Không suy từ `children.Count` ra state ready.
3. **Schedules:** parse format cũ thành kind/value, chuẩn hóa weekday; preserve raw/provenance cho invalid; revision hiệu lực legacy không biết phải đánh dấu partial. Freeze semantics/new writes trước chuyển analytics.
4. **Daily duplicates:** group read-only; lưu originals/archive trước merge; chọn deterministic new ID. Không dùng OR(isCompleted) hoặc max timestamp mặc định vì true/false có thể là intentional reversal; conflicts thiếu chronology/manual authority đưa quarantine/review. isApproved và reward ledger reconcile riêng, không cấp thưởng lại chỉ vì migrate.
5. **Historical analytics:** giữ coverageStart/provenance; historical periods trước dữ liệu biết được hiển thị unknown/partial. Không backfill hằng ngày từ ngày tạo tài khoản nếu chưa có bằng chứng.
6. **Deletion:** inventory orphan scoped, enqueue jobs có quyền; đừng purge child subtree chỉ vì parent document missing mà chưa có owner/retention verification.

Mỗi job có schemaVersion, checkpoint, idempotency key, dry-run output, conflict count và resume. Dual-read v1/v2 có giới hạn thời gian; tránh dual-write tạo hai sources of truth. Feature flag/rollout cohorts nếu cần, nhưng không thêm hạ tầng flag chỉ để một migration nhỏ nếu maintenance mode và version gates đủ.

### 5.3. Deploy order và rollback

Deploy server đọc được v1/v2 và hỗ trợ mới→client v2→migration→Rules siết paths sau khi client compatibility được chứng minh→retire v1. Privileged security restriction có thể cần deny sớm và bắt upgrade thay vì để client cũ tiếp tục write; quyết định được ghi explicit.

Rollback code không undo erasure hoặc restore verifier đã xóa. Không nới Rules để rollback UI failure. Có compatibility/read-only mode; migration giữ originals trong phạm vi retention cho reconcile, không tự phục hồi dữ liệu đã được yêu cầu erasure. Rules/API backup và client minimum version phải được quản lý bởi operator.

## 6. Ma trận đóng finding

Mỗi ID chỉ đóng khi giải pháp nhóm tương ứng và test sau đây có bằng chứng. ID GAP/MAINTENANCE có thể được chấp nhận/defer có lý do; không đổi thành “fixed” nếu chức năng chưa triển khai.

| ID | Nhóm | Điều kiện đóng cụ thể |
|---|---|---|
| [F01](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F01) | [R01](#R01)/[R02](#R02) | Child UI không edit/delete; server deny cùng owner thiếu elevation; delete hợp lệ chỉ đúng child |
| [F02](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F02) | [R01](#R01)/[R03](#R03) | Không seed 0000; verifier private; rate limit server không reset theo popup/scene; elevation expire |
| [F03](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F03) | [R02](#R02) | Nested subtree/job retry sạch; orphan legacy audit và remote retention có evidence |
| [F04](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F04) | [R04](#R04) | AddHabit popup mở được lần đầu và lần sau trong scene thực |
| [F05](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F05) | [R03](#R03) | Child B không bị resolver A ghi đè; DisplayMission reload đúng selected child |
| [F06](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F06) | [R05](#R05) | Không tạo abstract TMP; chart render không exception |
| [F07](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F07) | [R06](#R06) | Completion ngày 18 được tính Year; month/year sums đúng |
| [F08](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F08) | [R07](#R07) | 20:00 và nhãn buổi dùng typed schedule; evening filter đúng |
| [F09](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F09) | [R07](#R07)/[R08](#R08) | List/write/analytics cùng scheduler; unscheduled write deny server |
| [F10](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F10) | [R08](#R08) | Concurrent/retry một daily record, stale state conflict có policy |
| [F11](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F11) | [R09](#R09) | Approval+ledger end-to-end pass hoặc roadmap defer được ghi rõ |
| [F12](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F12) | [R06](#R06)/[R07](#R07) | Archive/revise hôm nay không đổi lịch sử cũ |
| [F13](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F13) | [R03](#R03)/[R06](#R06) | ChildChanged refresh dashboard, clear stale UI và không mix snapshots |
| [F14](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F14) | [R10](#R10) | AddChild/signup/Google cùng provisioning invariants, optional seeds rõ |
| [F15](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F15) | [R10](#R10) | Failure mỗi bước rồi resume không duplicate user/child hoặc bỏ hồ sơ dở |
| [F16](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F16) | [R08](#R08) | Instrumented load không N lần full logs collection; range/query bounded |
| [F17](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F17) | [R03](#R03)/[R11](#R11) | Delayed A/B responses chỉ render latest scope |
| [F18](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F18) | [R11](#R11) | Destroy item/scene trong await không MissingReference/unobserved error |
| [F19](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F19) | [R11](#R11) | Failure injection nút phục hồi, error UI đúng; bảo vệ existing không bị mất |
| [F20](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F20) | [R04](#R04)/[R11](#R11) | Return early báo failure, không close như saved; double-submit được chặn |
| [F21](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F21) | [R03](#R03) | Logout clears context/generation; old response không sửa phiên mới |
| [F22](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F22) | [R10](#R10)/[R11](#R11) | Init fail/available/retry có state/error và readiness subscribers |
| [F23](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F23) | [R10](#R10)/[R11](#R11) | Credential/network/profile lỗi phân biệt và UI dùng structured result |
| [F24](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F24) | [R01](#R01)/[R10](#R10) | Không ghi verifier mới; legacy password hashes được inventory/migrate xóa |
| [F25](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F25) | [R01](#R01)/[R13](#R13) | Versioned deployed Rules+negative tests+API auth proof, không chỉ source client |
| [F26](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F26) | [R07](#R07)/[R08](#R08) | BusinessDate/server timestamps; device clock manipulation không rewrite history |
| [F27](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F27) | [R12](#R12) | Desktop unsupported handled; Google mobile device proof đúng API/config |
| [F28](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F28) | [R12](#R12) | Locale support tested; unsupported language có fallback |
| [F29](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F29) | [R12](#R12) | Hai Speak gần nhau không bị coroutine cũ ẩn câu mới |
| [F30](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F30) | [R13](#R13) | Fresh suite/build/Rules/CI evidence; coverage nghiệp vụ có tests thật |
| [F31](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F31) | [R14](#R14) | Dependency inventory và clean-clone build trước cleanup; policy vendor rõ |
| [F32](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#F32) | [R14](#R14) | Age policy/privacy quyết định, legacy unknown không bịa birth data |
| [N01](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#N01) | [R04](#R04) | AddChild popup không self-disable lần đầu |
| [N02](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#N02) | [R04](#R04) | UpdateChild popup không self-disable Awake, đúng editing child |
| [N03](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#N03) | [R05](#R05) | Bar asset độc lập không nằm trong cleanup root; Graphic types đúng |
| [N04](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#N04) | [R11](#R11) | Settings enable nhiều lần chỉ một logout callback; foreign listener giữ nguyên |
| [N05](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#N05) | [R07](#R07) | Catalog fallback/default và Multiple Sprite load/render verified |
| [N06](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#N06) | [R06](#R06)/[R07](#R07) | Before-effectiveFrom không tăng denominator; unknown legacy được label |
| [N07](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#N07) | [R03](#R03)/[R11](#R11) | Write fail rollback đúng scope/version, pending rõ |
| [N08](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#N08) | [R08](#R08) | Uncomplete không giả completedAt; transition audit/updatedAt đúng |
| [N09](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#N09) | [R03](#R03) | Remembered child owner-scoped + existence check + fallback rule deterministic |
| [N10](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#N10) | [R03](#R03)/[R11](#R11) | Overlap children loads không duplicate/stale render |
| [N11](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#N11) | [R10](#R10)/[R11](#R11) | Signup single-flight và idempotent provisioning |
| [N12](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#N12) | [R12](#R12) | Authoritative config/provenance, resolver/build/login real evidence |
| [N13](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#N13) | [R13](#R13) | Auto write test script không vào production; emulator fixtures explicit |
| [N14](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#N14) | [R05](#R05) | Zero/no-task biểu diễn đúng; labels đọc được ở target viewports |
| [E01](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#E01) | [R05](#R05) | Fallback parented và cleanup không orphan bars |
| [E02](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#E02) | [R15](#R15) | 4097/8192/large coalesced chunks không overflow decoder |
| [E03](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#E03) | [R15](#R15) | Empty data multiline đúng; EOF/terminal framing có contract/test |
| [E04](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#E04) | [R16](#R16) | Consumer exception không đổi thành invalid_response; outcome policy rõ |
| [E05](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#E05) | [R16](#R16) | Timeout/cancel/overflow cleanup được transport tests chứng minh |
| [E06](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#E06) | [R16](#R16) | Production HTTP denied; token/context chỉ HTTPS ngoài local test |
| [E07](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#E07) | [R03](#R03)/[R06](#R06) | Refresh atomic snapshot, A/B habits/logs không trộn |
| [E08](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#E08) | [R10](#R10) | Google existing child incomplete được repair, không duplicate concurrent seeds |
| [E09](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#E09) | [R10](#R10)/[R11](#R11) | Early UI load resume khi Ready, Failed có retry rõ |
| [E10](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#E10) | [R01](#R01)/[R11](#R11) | PIN UI single-flight và server rate limit độc lập UI |
| [E11](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#E11) | [R04](#R04)/[R10](#R10)/[R11](#R11) | Pending cancel semantics rõ; committed-write/reload-failure không duplicate retry |
| [E12](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#E12) | [R17](#R17) | Host scope envelope/provenance và A/B integration tests |
| [E13](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#E13) | [R17](#R17) | End-to-end chat evidence hoặc accepted module-only scope GAP |
| [E14](CODEBASE_AUDIT_FINDINGS_2026-10-18.md#E14) | [R14](#R14) | `.gitignore` allowlist đúng năm docs; clean clone cần xác nhận sau khi chúng được version-control theo quy trình người dùng cho phép |

## 7. Release gates và bằng chứng phải lưu

- **Gate security/destructive:** exported/versioned Rules, API auth tests; verifier không client-readable và elevation token chỉ ở bộ nhớ host, không trong Firestore/log/context; deletion recovery và legacy inventory. Không đóng gate bằng ẩn nút hoặc PIN hash client.
- **Gate UI/session:** Play Mode target scene/prefab tests, screenshots/log cho first open, child/account switching với delayed requests, chart redraw lifecycle.
- **Gate data:** scheduler/analytics fixtures cố định, transaction/retry tests, migration dry-run/conflict report và rollback/retention policy.
- **Gate platform:** fresh Unity compile, Android build và device login/TTS theo target; iOS chỉ nếu release cần và có proof.
- **Gate chatbot:** parser/transport tests và actual host→API→history scope tests nếu chat thuộc release. Module-only tests không chứng minh deployment hay storage authorization.

Mỗi evidence ghi commit SHA, environment, command/test case, expected/actual, log/XML artifact location, ngày và người có thẩm quyền xác nhận remote state. Không dùng checkbox đã tick từ kế hoạch cũ thay run mới. Operator credentials/deployed Rules/product decisions còn thiếu là work cần hoàn tất, không phải lý do để bịa trạng thái.

## 8. Tham chiếu kỹ thuật

- [Firestore transactions/batched writes](https://firebase.google.com/docs/firestore/manage-data/transactions): atomicity trong Firestore; Auth provisioning vẫn cần recovery riêng.
- [Firestore delete-data](https://firebase.google.com/docs/firestore/manage-data/delete-data): xóa document không cascade.
- [Firestore Rules structure](https://firebase.google.com/docs/firestore/security/rules-structure): mỗi hierarchy cần rule phù hợp; Admin APIs cần authorization riêng.
- [FirebaseAuth API](https://firebase.google.com/docs/reference/unity/class/firebase/auth/firebase-auth#signinwithproviderasync): giới hạn platform cho provider flow.
- [Decoder.Convert](https://learn.microsoft.com/en-us/dotnet/api/system.text.decoder.convert): incremental decode với bytesUsed/charsUsed.
- [WHATWG SSE](https://html.spec.whatwg.org/multipage/server-sent-events.html#parsing-an-event-stream): blank delimiter, data lines và EOF semantics.
- [UnityWebRequest.timeout](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Networking.UnityWebRequest-timeout.html): total timeout không thay idle/protocol policy.

Tài liệu không cấp quyền deploy/migrate/delete remote. Khi triển khai thực tế cần baseline mới, impact mới và evidence theo gate. Trạng thái triển khai mã nguồn trên nhánh `Nam` được ghi ở §9; các finding chưa đủ bằng chứng vẫn được giữ mở.

## 9. Trạng thái worktree `Nam` — cập nhật 22/10/2026

### 9.1. Ma trận trạng thái 60 findings

`Đã xử lý local` xác nhận sửa nguyên nhân chính trong source/scene/tài liệu và kiểm tra phù hợp; không có nghĩa đã đóng toàn bộ acceptance criteria trong §6 hoặc đã release. `Một phần` còn thiếu implementation hoặc evidence; `Mở` chưa đủ điều kiện đóng; `Defer` giữ quyết định lịch sử trong scope trước. **Không dùng số findings local làm phần trăm hoàn thành production.**

| Trạng thái | Số lượng | Findings |
|---|---:|---|
| Một phần | 21 | F01, F02, F03, F09, F10, F11, F12, F14, F15, F24, F25, F26, F30, F31, N06, N12, E05, E08, E10, E11, E12 |
| Đã xử lý local | 37 | F04, F05, F06, F07, F08, F13, F16, F17, F18, F19, F20, F21, F22, F23, F28, F29, F32, N01, N02, N03, N04, N05, N07, N08, N09, N10, N11, N13, N14, E01, E02, E03, E04, E06, E07, E09, E14 |
| Mở | 0 | — |
| Defer theo quyết định người dùng | 2 | F27, E13 |

So với ma trận 18–19/10, E11 vẫn một phần nhưng source local đã giữ scope/payload và có Auth/Firestore Emulator retry/conflict evidence ngày 24/10; còn thiếu Auth account switch, write-response loss, cancel và popup-destroy cases. E03 đã pass trong EditMode suite hiện tại. F25 containment Rules đã deploy/readback; F24 fields đã retire nhưng vẫn thiếu client/writer compatibility closure. F12 vẫn một phần vì thiếu revisions cho cả dữ liệu mới. F31 đã khôi phục local native LFS objects và integration runtime, nhưng clean-clone/resolver/target-platform gates còn mở.

| ID | Trạng thái | Kết quả và phần còn lại |
|---|---|---|
| F01 | Một phần | Ẩn edit/delete trong Child và xác nhận delete ở Parent; thiếu server elevation. [Assets/_Backend/Childrent.cs](../Assets/_Backend/Childrent.cs) |
| F02 | Một phần | HABIMON-BE local có Firebase ID-token verification, Admin SDK revoked/disabled-user check (fail closed), fresh reauth challenge, private scrypt verifier, atomic account limiter và elevation. Local suite 45/45 pass; ADC/IAM/runtime, migration `0003`, Unity integration và legacy PIN migration còn mở. |
| F03 | Một phần | Inventory 22/10 xác nhận 3 missing child profiles còn 20 descendants (8 habits, 8 logs, 2 pets, 2 rewards), chưa cleanup/restore. Client chỉ xóa 4 collection known; vẫn thiếu authoritative recursive job, pending-write denial, recovery và retention evidence. |
| F04 | Đã xử lý local | Start không self-close; scene tests mở lần đầu. [Assets/_UI/AddHabitPopup.cs](../Assets/_UI/AddHabitPopup.cs) |
| F05 | Đã xử lý local | Resolver ưu tiên selected/remembered/fallback; Childrent gọi đúng Mission hoặc DisplayMission. [Assets/_Backend/FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs) |
| F06 | Đã xử lý local | Dùng TextMeshProUGUI cụ thể. [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs) |
| F07 | Đã xử lý local | Year dùng range toàn tháng; test completion 18/10 được tính. [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs) |
| F08 | Đã xử lý local | Typed schedule và clock→period; test các biên 05/12/18h. [Assets/_Backend/Core/HabitSchedule.cs](../Assets/_Backend/Core/HabitSchedule.cs) |
| F09 | Một phần | Client dùng scheduler; inventory có 2 completed logs lệch repeatDays hiện tại, chưa chứng minh sai lịch tại lúc ghi do thiếu revisions. Rules active chưa enforce schedule/business policy. |
| F10 | Một phần | Deterministic ID và transaction có trong source; production 64 log đều legacy, có 1 duplicate group gồm 2 records xung đột, 1 thiếu dateKey, 9 dangling habit refs. Chưa migration/conflict resolution hoặc server uniqueness/stale-version enforcement. |
| F11 | Một phần | Client queue/transaction/progression có trong source. Production có 58 completed, 8 approved logs nhưng không có rewardGranted field hoặc đủ reward snapshots; chưa ledger/server authority/reconciliation. Không tự cộng lại thưởng từ thiếu receipt. |
| F12 | Một phần | Probe disable habit mới vẫn làm ngày cũ 1/1→0/0 unknown; chưa activation/schedule revisions. Inventory 81 habits đều thiếu effectiveFrom/createdAt nghiệp vụ, không thể tự bịa lịch sử từ document createTime. |
| F13 | Đã xử lý local | Subscribe ChildChanged/SessionChanged, commit snapshot sau scope/version guard. [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs) |
| F14 | Một phần | Operation ID tạo child+pet có trong source. Inventory 19/51 child thiếu pet (16 parent), 7 không habits, 21 không rewards; chưa repair. Pet/optional seed policy và provenance phải chốt trước backfill. |
| F15 | Một phần | pending→ready/recovery có trong source; production 33/33 parent đều markerless. Đã kiểm kê Firestore nhưng chưa inventory đầy đủ Auth owner states, chưa repair/backfill hoặc failure-injection mọi write; không được set ready/pending hàng loạt. |
| F16 | Đã xử lý local | Tải logs theo ngày một lần cho list, dashboard theo range; chưa đo billed reads. [Assets/_Backend/DisplayMission.cs](../Assets/_Backend/DisplayMission.cs) |
| F17 | Đã xử lý local | Request version/session guard trước render; chưa chứng minh mọi A/B race ngoài test. [Assets/_Backend/Mission.cs](../Assets/_Backend/Mission.cs) |
| F18 | Đã xử lý local | Kiểm tra this/toggle/scope sau await. [Assets/_Backend/DisplayMission.cs](../Assets/_Backend/DisplayMission.cs) |
| F19 | Đã xử lý local | Các handler chính có single-flight/catch/finally; chưa inject failure mọi UI handler. [Assets/_Backend/Login.cs](../Assets/_Backend/Login.cs) |
| F20 | Đã xử lý local | Submit trả result, chặn double click và thông báo refresh fail; E11 hiện chặn retry sang scope khác, nhưng lifecycle/account recovery vẫn partial. [Assets/_Backend/Mission.cs](../Assets/_Backend/Mission.cs) |
| F21 | Đã xử lý local | Logout clear context và tăng generation. [Assets/_Backend/FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs) |
| F22 | Đã xử lý local | Readiness Task dùng chung, error/event và retry sau failure. [Assets/_Backend/FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs) |
| F23 | Đã xử lý local | Phân biệt lỗi credentials/network/rate-limit; credentials không enumeration. [Assets/_Backend/FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs) |
| F24 | Một phần | Đã xóa `passwordHash/passwordSalt` khỏi 29 parent và readback 33 parent cho kết quả cả hai fields bằng 0; deployed Rules chặn reintroduction. Client/writer ngoài checkout, Auth compatibility và retention ngoài Firestore chưa kiểm chứng. |
| F25 | Một phần | Owner-scoped containment và legacy-verifier block đã deploy/readback; Rules Emulator 9/9 pass. Chưa phân biệt Parent/Child cùng UID, chưa enforce hết schema/domain operations hoặc parent-only server API. [firestore.rules](../firestore.rules) |
| F26 | Một phần | UTC+7 ở client, completion/reward timestamps có server writes; dateKey vẫn client-supplied. Inventory có 1 log thiếu dateKey; không chứng minh clock tampering hoặc trusted server business-date policy. |
| F27 | Defer theo quyết định người dùng | Defer kế thừa docs; desktop unsupported, chưa chứng minh mobile provider login. [Assets/_Backend/FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs) |
| F28 | Đã xử lý local | Code setLanguage vi-VN và text fallback; chưa Android runtime proof. [Assets/_Backend/Features/TextToSpeech.cs](../Assets/_Backend/Features/TextToSpeech.cs) |
| F29 | Đã xử lý local | Speak mới stop coroutine cũ; OnDisable clear display/queue. [Assets/_Backend/Features/TextToSpeech.cs](../Assets/_Backend/Features/TextToSpeech.cs) |
| F30 | Một phần | Kiểm chứng local 24/10: EditMode 63/63, Parent popup/chart PlayMode 9/9, Firestore Rules Emulator 9/9, Auth/Firestore provisioning integration 1/1. Chưa có hosted Unity CI, full business authorization hoặc device evidence. |
| F31 | Một phần | 8 native `.so`/`.bundle` LFS objects đã hydrate; `git lfs fsck --objects` pass, `.so` ELF x86-64, `.bundle` Mach-O universal; C# build và emulator integration pass. Chưa có clean clone, resolver regeneration, Android/iOS target build hoặc device proof. |
| F32 | Đã xử lý local | Source lưu/hiển thị ageAsOf cho new/updated profile và không bịa legacy date; production 51/51 existing child còn thiếu ageAsOf. Chưa backfill và không dùng document createTime để suy ngày khai tuổi. |
| N01 | Đã xử lý local | Start không tự tắt popup; PlayMode có first-open test. [Assets/_UI/AddChild.cs](../Assets/_UI/AddChild.cs) |
| N02 | Đã xử lý local | Awake không self-close; OpenPopup(child) giữ ID trẻ. [Assets/_UI/UpdateChild.cs](../Assets/_UI/UpdateChild.cs) |
| N03 | Đã xử lý local | Không dùng template con bị destroy; runtime tạo bar và text riêng. [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs) |
| N04 | Đã xử lý local | OnEnable/OnDisable add/remove đúng method group. [Assets/_Backend/Logout.cs](../Assets/_Backend/Logout.cs) |
| N05 | Đã xử lý local | Scene test resolve homework subasset; fallback/book references; chưa catalog ID độc lập asset names hoàn chỉnh. [Assets/_Backend/Core/Tests/EditMode/SceneAssetIntegrityTests.cs](../Assets/_Backend/Core/Tests/EditMode/SceneAssetIntegrityTests.cs) |
| N06 | Một phần | Habit mới có effectiveFrom trong source; inventory 81 legacy habits đều thiếu effectiveFrom/createdAt nghiệp vụ. Đã inventory, chưa migration; provenance không có vẫn phải unknown, không lấy document createTime làm ngày bắt đầu chắc chắn. |
| N07 | Đã xử lý local | Toggle pending, await result, rollback lỗi; callback mutation lấy current controller scope, cần thêm delayed integration. [Assets/_UI/MissionUI.cs](../Assets/_UI/MissionUI.cs) |
| N08 | Đã xử lý local | Uncomplete path clear completedAt; đã granted/approved thì chặn child uncomplete. [Assets/_Backend/Service/HabitRewardService.cs](../Assets/_Backend/Service/HabitRewardService.cs) |
| N09 | Đã xử lý local | Selected→remembered theo UID→ordinal fallback. [Assets/_Backend/Core/HabitSchedule.cs](../Assets/_Backend/Core/HabitSchedule.cs) |
| N10 | Đã xử lý local | Load version/lifecycle/parent guard, render sau khi query hoàn tất. [Assets/_Backend/Childrent.cs](../Assets/_Backend/Childrent.cs) |
| N11 | Đã xử lý local | Single-flight + finally; provisioning stable seed IDs. [Assets/_Backend/SignUp.cs](../Assets/_Backend/SignUp.cs) |
| N12 | Một phần | Read-only 22/10: Android/iOS ACTIVE, config app/project/package/bundle khớp, Google provider enabled, Android có SHA-1/SHA-256. Chưa so signing certificate của APK thực tế, clean target build/resolver hoặc device login. |
| N13 | Đã xử lý local | File và meta đã xóa trong worktree; không còn script auto-write. `Assets/_Backend/FirebaseTester.cs` |
| N14 | Đã xử lý local | 0% bar height 0, no-task em dash, content rộng; chưa visual QA mọi viewport. [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs) |
| E01 | Đã xử lý local | Bar/text SetParent chartRoot và generated objects được cleanup. [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs) |
| E02 | Đã xử lý local | Decoder.Convert incremental loop, tests chunk lớn và split UTF-8. [Assets/_Backend/Chatbot/ChatbotSseParser.cs](../Assets/_Backend/Chatbot/ChatbotSseParser.cs) |
| E03 | Đã xử lý local | `DispatchEvent` xóa eventName sau frame không data; Chatbot parser regression trong EditMode suite hiện tại pass (63 tests tổng cộng, gồm 29 Chatbot tests). [Assets/_Backend/Chatbot/ChatbotSseParser.cs](../Assets/_Backend/Chatbot/ChatbotSseParser.cs) |
| E04 | Đã xử lý local | Consumer callback ngoài catch parse; transport regression có thật. [Assets/_Backend/Chatbot/ChatbotApiClient.cs](../Assets/_Backend/Chatbot/ChatbotApiClient.cs) |
| E05 | Một phần | Snapshot sở hữu cancellation, scope change/Dispose abort; chưa consuming view lifecycle. [Assets/_Backend/ChatbotIntegration/FirebaseChatbotContextAdapter.cs](../Assets/_Backend/ChatbotIntegration/FirebaseChatbotContextAdapter.cs) |
| E06 | Đã xử lý local | Client yêu cầu HTTPS, HTTP chỉ loopback với cờ development. [Assets/_Backend/Chatbot/ChatbotApiClient.cs](../Assets/_Backend/Chatbot/ChatbotApiClient.cs) |
| E07 | Đã xử lý local | Tasks tải local data rồi swap sau scope/version guard; không AddRange xen kẽ await. [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs) |
| E08 | Một phần | Marker pending/ready/retry đã có trong source; inventory 33 parent markerless và 19 child thiếu pet. Đã có phạm vi classifier/repair; chưa migrate. Không blind pending/reseed hoặc hồi sinh dữ liệu chủ động xóa. |
| E09 | Đã xử lý local | UI await readiness; failure có message/retry ở manager; không chứng minh mọi startup native failure. [Assets/_Backend/FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs) |
| E10 | Một phần | PIN UI single-flight/reopen in-flight đã sửa; BE local có private verifier, PIN rate limit 5/15 phút và short-lived elevation. Chưa deploy/tích hợp Unity hoặc kiểm thử device; legacy PIN vẫn còn ở Firestore. |
| E11 | Một phần | Pending DTO giữ payload immutable; habit scope là parent/child/generation, child create scope là parent/session generation. Transactions từ chối payload khác cùng ID, retry đúng target idempotent, đổi child chặn write, quay lại cùng target cần retry tường minh. EditMode 63/63, PlayMode 9/9, emulator integration 1/1 pass. Auth account switch, mất response thực, cancel/popup destroy và process-restart recovery còn thiếu. [Assets/_UI/AddHabitPopup.cs](../Assets/_UI/AddHabitPopup.cs) |
| E12 | Một phần | Loader scoped + explicit date range; không có production caller hoặc runtime A/B integration proof. [Assets/_Backend/ChatbotIntegration/FirebaseChatbotContextAdapter.cs](../Assets/_Backend/ChatbotIntegration/FirebaseChatbotContextAdapter.cs) |
| E13 | Defer theo quyết định người dùng | Module-only theo docs; tìm graph+source không thấy host tạo client/call snapshot loader. [Assets/_Backend/Chatbot/README.md](../Assets/_Backend/Chatbot/README.md) |
| E14 | Đã xử lý local | Whitelist 5 docs tồn tại; git ls-files docs rỗng, chưa team/clone persistence. [.gitignore](../.gitignore) |

### 9.2. Kiểm chứng baseline — 22/10/2026 (lịch sử)

Các kết quả dưới đây được chạy trên worktree `Nam` tại HEAD `486765987db5726873185235fb1a6c398f6b98c8`, gồm thay đổi tracked/untracked. Đây là baseline lịch sử, không phải trạng thái hiện tại; đối chiếu cập nhật 24/10 tại §9.3. Chỉ số local không phải kết luận release readiness.

| Kiểm tra | Kết quả | Giới hạn |
|---|---|---|
| `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` | PASS, 0 warning / 0 error | C# compile, không phải APK hoặc native SDK initialization |
| Unity 6000.3.11f1 EditMode | PASS 55/55 | Bao phủ các test hiện có, không bao phủ mọi race/framing case |
| Unity PlayMode | 9 pass, 0 failed, 1 skipped | Explicit Emulator test được skip và chạy riêng bên dưới |
| `npm run test:firestore-rules` | PASS 6/6 | Local owner-scope Rules prototype, không phải deployed/business authorization |
| `FirebaseProvisioningEmulatorTests.ExistingAuthUserIsProvisionedOnLogin` trên Auth/Firestore Emulator | FAILED 1/1 | Fail trước business assertions: native plugin là LFS pointer, `invalid ELF header` |
| Probe analytics dùng trực tiếp `HabitSchedule.cs` | Tái hiện F12 | Ngày cũ từ 1/1 sang 0/0 unknown khi chỉ đổi IsActive=false |
| Probe parser dùng trực tiếp `ChatbotSseParser.cs`/contracts | Tái hiện E03 | Frame không data giữ eventName sang frame sau |
| Firebase MCP readback Rules `habimon-808e2` | Baseline trước containment rollout; active source được đọc lại 24/10 là owner-scoped | Containment chưa phải final Parent/Child authorization; không thử truy cập dữ liệu người dùng |

Artifacts của lần chạy: `/tmp/HABIMON-audit-20261022/{editmode,playmode,provisioning}.xml`, logs cùng tên, `results.json`, `probe/Program.cs` và `probe/Probe.csproj`. `/tmp` là lưu trữ tạm, không phải artifact đã version-control. Kết quả và nguyên nhân được ghi trực tiếp trong tài liệu này để không phụ thuộc vào việc các file tạm còn tồn tại. Bản sao Unity dùng để chạy test giữ `Library` và generated dependencies từ workspace, đã dọn sau test; đây không phải clean-clone proof.

**Root cause native trong baseline 22/10:** `Assets/Firebase/Plugins/x86_64/FirebaseCppApp-13_15_0.so` dài 133 byte, nội dung bắt đầu `version https://git-lfs.github.com/spec/v1`, expected LFS object size 80.228.616 byte; Unity báo `DllNotFoundException` / `invalid ELF header`. Khi đó 8 pointers chưa được hydrate. Cập nhật object restoration và runtime integration ở §9.3.

**Bằng chứng lịch sử, không phải kết quả hiện tại:** các tài liệu 18–19/10 ghi EditMode 55/55, PlayMode 9/9, provisioning integration 1/1, Rules 6/6; conditional `UNITY_ANDROID` và các test assemblies compile pass; npm audit/actionlint pass; bản sao không Library pass Editor tests nhưng EDM4U resolve thiếu AndroidPlayer. Các XML `/tmp/HABIMON-e10-*.xml` cũ không còn tại lúc rà soát 22/10. Không tái chạy npm audit, actionlint, Android conditional build hoặc clean resolver trong lượt 22/10; không trình bày chúng như kết quả mới. Provisioning 1/1 cũ chưa tái hiện được ở checkout hiện tại.

### 9.3. Cập nhật kiểm chứng local — 24/10/2026

| Kiểm tra | Kết quả | Giới hạn |
|---|---|---|
| Git LFS | Cài user-local 3.4.1; 8/8 native Firebase `.so`/`.bundle` hydrate; `git lfs fsck --objects` pass; files giữ tracked-clean | Chưa thử clean clone; LFS install trong user environment không nằm trong Git |
| Native formats | Bốn `.so` là ELF x86-64; bốn `.bundle` là Mach-O universal | Chỉ Linux `.so` được load trong runtime test hiện tại |
| `dotnet build Assembly-CSharp.csproj -v:minimal` | PASS, 0 warning / 0 error | Không phải APK/iOS target build |
| Unity 6000.3.11f1 EditMode | PASS 63/63 | Local Editor; chưa có hosted CI |
| Parent popup/chart PlayMode | PASS 9/9 | Chưa phải device test |
| `npm run test:firestore-rules` | PASS 9/9 | Rules Emulator; không chứng minh final Parent/Child business authorization |
| `npm run test:firebase-provisioning` | PASS 1/1 trên Auth/Firestore Emulator `127.0.0.1` | Local emulator, không gọi production Auth/Firestore |
| Firebase Admin token status check | `HABIMON-BE` pytest PASS 45/45; mock kiểm tra `check_revoked=True`, revoked/disabled → 401, Auth status API unavailable → 503 | Chưa cấu hình ADC/IAM hoặc kết nối live Firebase từ backend |
| Firestore Security Rules source | Active source đọc lại là owner-scoped containment, không còn public wildcard | Cùng UID Parent/Child và business-field/domain authorization chưa được phân biệt; chưa final |

**E11:** `PendingHabitCreate`/`PendingChildCreate` giữ scope và payload get-only. FirebaseManager/ChildService kiểm tra scope; habit/child create dùng transaction so sánh payload, không overwrite theo operation ID. Emulator assertion xác nhận retry khớp, conflicting payload bị từ chối, selected-child switch bị chặn, A→B→A không tự revive generation, và explicit retry tại cùng target mới tạo scope phiên mới. Còn thiếu Auth account A→B, response loss thật, cancel khi write đang chạy, popup destroy và process-restart recovery; E11 vẫn Một phần.

Các kết quả trên chạy trên worktree hiện tại với Emulator cục bộ. Chúng không chứng minh deploy, clean checkout, Android/iOS build, device runtime, Neon migration, ADC/IAM hoặc deployed API→Unity integration. Không deploy, push hoặc ghi/xóa/migrate production data trong lượt này.

### 9.4. Gates còn mở và thứ tự tiếp theo

1. Chọn runtime production không phải demo, cấp ADC/workload identity Firebase Auth read permission cho BE, xác nhận Neon database, rồi review migration `0003` trước khi apply.
2. Tích hợp Unity PIN/reset với BE đã deploy; route parent-only business writes qua API có elevation trước khi siết direct Firestore Rules.
3. Chốt dry-run/repair policy cho F03/F10/F14/F15/E08. Unknown orphan, conflicted daily logs, legacy reward/provisioning state không được tự suy ra hoặc overwrite.
4. Chạy clean clone + EDM4U resolver regeneration, Android/iOS target build/device test và hosted Unity CI; F31/F30 vẫn Một phần.
5. Bổ sung E11 lifecycle/account tests, cùng các revision/ledger còn thiếu cho F12/F11/F09/F26. F27/E13 tiếp tục giữ defer lịch sử.
