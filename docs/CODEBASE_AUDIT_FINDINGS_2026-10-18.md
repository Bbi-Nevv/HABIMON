# HABIMON — Kiểm chứng findings và audit codebase

Audit gốc: **18/10/2026**; cập nhật trạng thái: **24/10/2026**, múi giờ Asia/Ho_Chi_Minh. Tài liệu đi cùng: [Đề xuất khắc phục](CODEBASE_REMEDIATION_PLAN_2026-10-18.md).

**Kế hoạch triển khai chi tiết + inventory Firebase:** [FIREBASE_REMEDIATION_ACTION_PLAN_2026-10-22.md](FIREBASE_REMEDIATION_ACTION_PLAN_2026-10-22.md). Read-only kiểm kê production đã thực hiện; 24/10 bổ sung local code/test và read-only Rules recheck, không có migration/deployment production hoặc ghi/xóa dữ liệu mới.

## 0. Trạng thái hiện tại — đọc trước các mô tả baseline

Checkout hiện tại: `Nam @ 486765987db5726873185235fb1a6c398f6b98c8`, có thay đổi tracked/untracked chưa commit. Inventory ngày 22/10: **43 file C# do dự án quản lý** (34 implementation/model, 9 test files), ngoài 3 C# vendor Firebase. Phạm vi gồm source nghiệp vụ, ba build scenes, test/config/Rules/workflow; không khẳng định audit toàn bộ native binary, art assets hoặc backend ngoài repo.

Ma trận hiện tại: **37 đã xử lý local, 21 một phần, 0 mở, 2 defer (F27/E13)**. F25 vẫn partial sau containment Rules; active source readback 24/10 là owner-scoped nhưng chưa final Parent/Child/domain authorization. F02 có BE Admin SDK revoked/disabled-user check local (pytest 45/45), chưa có ADC/IAM, deploy hoặc Unity integration. E03 pass trong EditMode suite. E11 có scope/payload guard và Emulator retry/conflict evidence nhưng lifecycle/account cases còn mở. F31 native LFS/runtime local đã phục hồi, còn clean-clone/target build. Nguồn chi tiết: [remediation plan](CODEBASE_REMEDIATION_PLAN_2026-10-18.md); bản tổng hợp: [review report](CODEBASE_REVIEW_AND_CHANGE_REPORT_2026-10-18.md).

- **F25:** containment owner-scoped và verifier-retirement đã deploy/readback trên `habimon-808e2`, không còn broad public allow. Chưa enforce Parent-vs-Child cùng UID hoặc toàn bộ domain/business authorization.
- **F30/F31:** baseline 22/10 có Auth/Firestore integration fail do native LFS. Cập nhật 24/10: C# build, EditMode 63/63, PlayMode 9/9, Rules Emulator 9/9 và Auth/Firestore Emulator integration 1/1 pass sau khi hydrate đủ 8 LFS objects. Chưa có clean clone, Android/iOS target build, device hoặc hosted CI evidence.
- **F12:** probe source xác nhận disable một habit có effectiveFrom hợp lệ vẫn làm ngày cũ 1/1→0/0 unknown; chưa có activation/schedule revisions, không chỉ thiếu legacy migration.
- **E03:** `DispatchEvent` clear eventName cả khi frame không có data; Chatbot parser suite pass trong Unity EditMode 63/63 tổng suite. Đã xử lý local.
- **E11:** `PendingHabitCreate`/`PendingChildCreate` giữ scope/payload; create transactions so sánh cùng operation ID và từ chối payload conflict. Emulator xác nhận selected-child change bị chặn và explicit resume chỉ cùng target. Chưa kiểm thử Auth account A→B, response-loss thật, cancel-in-flight, popup destroy hoặc process restart; vẫn một phần.

**Cách đọc lịch sử:** mô tả nguyên nhân và line references ở §1–§4 chủ yếu thuộc baseline 18/10, các đoạn “Cập nhật local 18–19/10” là lịch sử sửa/test. Chúng không ghi đè ma trận 22/10. Các nhận định “không có readiness/transaction/tests/Rules” của baseline không còn đúng với worktree. §5/§7 dưới đây đã được cập nhật cho hiện trạng; §8 là hướng dẫn tái hiện baseline, không phải expected output của parser đã sửa.

## 1. Baseline 18/10 và phạm vi audit gốc

- Checkout: `/home/dodaonam/Projects/HABIMON`, nhánh `main`, HEAD `486765987db5726873185235fb1a6c398f6b98c8`.
- Remote `origin` của checkout: `https://github.com/Bbi-Nevv/HABIMON.git`; tên repository trong hai báo cáo là `dodaonam/HABIMON`. Việc đối chiếu dùng commit và nội dung local, không suy hai remote name là cùng trạng thái live.
- Hai báo cáo người dùng cung cấp cùng đánh giá `Nhan @ c849dbf25f2969895383af47068af538f0ee1d9c`. Giữ nguyên ID F01–F32 và N01–N14 để truy vết; E01–E14 là bổ sung của lượt audit này.
- Git diff xác nhận các controller nghiệp vụ cũ, model, UI, scene và prefab viện dẫn không thay đổi giữa commit báo cáo và HEAD. HEAD bổ sung module chatbot, bridge Firebase và tests.
- Đã đọc toàn bộ **32 file C# do dự án quản lý**, gồm 28 file implementation/model và 4 file tests, tổng 5.006 dòng theo `splitlines()`. Có **10 phương thức `[Test]`**. Danh sách file ở cuối tài liệu.
- Đã kiểm tra ba scene build `Menu`, `Parent`, `Child`; bốn prefab nghiệp vụ; hierarchy, UnityEvents, GUID/fileID liên quan; manifest/lock packages, asmdef, Player Settings, Build Settings, Gradle templates, dependency resolver, Firebase client configuration, Git ignore và tài liệu chatbot hiện có.
- Vendor Firebase/EDM được khảo sát về cấu trúc, phiên bản, nguồn dependency và các source API liên quan. **Không reverse-engineer DLL/native libraries, không audit từng pixel/art asset, không khẳng định đã kiểm chứng mọi shader hay nội bộ SDK.** Recovery/template scenes không nằm trong ba scene build đang bật.

### Trạng thái bằng chứng

| Nhãn | Ý nghĩa |
|---|---|
| STATIC | Có bằng chứng trực tiếp từ code/serialized configuration; chưa chạy toàn bộ luồng Unity |
| PROBE | Có thêm thực thi cô lập; phạm vi probe được ghi rõ |
| CONDITIONAL | Điều kiện/rủi ro có thật trong code, ảnh hưởng phụ thuộc runtime/cấu hình/đặc tả |
| GAP | Thiếu triển khai hoặc thiếu bằng chứng vận hành; không mặc nhiên là bug hồi quy |
| MAINTENANCE | Chính sách bảo trì/lưu trữ hoặc mô hình sản phẩm cần quyết định |

P1: sai chức năng/dữ liệu hoặc rủi ro đáng kể cần xử lý sớm. P2: độ tin cậy, tích hợp, hiệu năng hoặc giới hạn có điều kiện. P3: bảo trì/mô hình. **Không tự gán P0 cho mọi rủi ro bảo mật:** phạm vi production chưa được biết; thao tác destructive trái quyền được chứng minh ở server phải được nâng ưu tiên ngay. Ưu tiên không thay thế độ chắc chắn của bằng chứng.

### Giới hạn thực thi của lượt audit gốc 18/10

Không chạy Play Mode end-to-end, Unity EditMode suite đầy đủ, Android/iOS build, thiết bị thật hoặc Firebase Emulator trong lượt này. Không truy cập Firebase Console/project thật, deployed Rules, remote cleanup jobs hoặc backend chatbot. Các bài test hiện có được đọc, không được đánh dấu pass bằng lịch sử.

Probe C# dùng source `ChatbotSseParser.cs`/`ChatbotContracts.cs` hiện tại trên .NET 10 và `UnityEngine.CoreModule.dll` của Editor **6000.3.11f1** cho kiểm tra UnityEvent. Đây không phải Android/IL2CPP hay Play Mode. Kết quả:

```text
SSE bytes=4096 OK
SSE bytes=4097 ArgumentException
SSE bytes=8192 ArgumentException
empty data multiline actual=x
Unity listener callbacks=3
exit=0
```

Probe multiline đưa vào `data:\ndata: x\n\n`; dữ liệu đúng theo SSE là `\nx`, nhưng parser trả `x`. Listener probe đăng ký cùng kiểu lambda capture `this` ba lần, invoke một lần.

GitNexus `HABIMON` được refresh tới HEAD: 435 covered files, 1.025 nodes, 1.924 edges, 30 communities, 54 reported flows. Analyzer cảnh báo còn flow không được trace do giới hạn; **flow/caller vắng mặt không được dùng làm bằng chứng code không chạy**. UnityEvents và bindings được đối chiếu riêng. Impact của `FirebaseManager.ResolveCurrentChildIdAsync`: **HIGH**, 6 direct callers, 18 affected symbols trong tối đa ba lớp; các luồng Google login, Mission và DisplayMission liên quan. Đây là cảnh báo cho triển khai sau này, không phải code đã được sửa.

**Re-index worktree 19/10/2026:** `Nam` tại HEAD `4867659`, toàn bộ 476 covered files được index; 1.692 nodes, 16.533 edges, 51 clusters, 98 flows reported. `detect-changes --scope all` trên diff hiện tại ghi 34 files, 65 changed symbols, 96 affected process paths, risk **CRITICAL aggregate**. Analyzer cảnh báo độ bao phủ flow còn thiếu (14/214 entry-point candidates không được xếp hạng, 171 entry points không được trace, 24 deduplicated flows bị loại theo cap, 67 callees bị bỏ, 7 walks dừng vì budget); do đó đây không phải risk riêng cho từng finding hoặc chứng minh code path không chạy.

## 2. Findings từ báo cáo đầu — nguyên nhân baseline và lịch sử cập nhật

<a id="F01"></a>

### F01 — Child Mode được gắn thao tác xóa hồ sơ

**Hiện trạng 22/10 — Một phần:** Ẩn edit/delete trong Child và xác nhận delete ở Parent; thiếu server elevation. [Assets/_Backend/Childrent.cs](../Assets/_Backend/Childrent.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC; hậu quả server CONDITIONAL.** Nguồn: [Childrent.cs](../Assets/_Backend/Childrent.cs), dòng 102–110, 139–173; [Child.unity](../Assets/_Scenes/Child.unity), component 42285985; [Child_in_SceneChild.prefab](../Assets/_Prefab/Child_in_SceneChild.prefab).

`SpawnChildItem` tạo DeleteButton nếu chưa có, chỉ kiểm tra childId; không kiểm tra mode. Child Scene thực sự dùng controller và prefab này. Callback gọi `FirebaseManager.DeleteChildAsync`, không yêu cầu PIN hoặc confirmation. Mở danh sách trẻ trong Child Mode có thể cung cấp thao tác destructive đối với từng trẻ trong danh sách của tài khoản. Xóa thành công phụ thuộc Rules; không kết luận quyền truy cập chéo tài khoản. Sửa UI thôi chưa tạo authorization boundary. Khắc phục: [R01](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R01), [R02](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R02).

<a id="F02"></a>

### F02 — PIN mặc định và ranh giới phụ huynh yếu

**Hiện trạng 22/10 — Một phần:** Source không seed 0000 mới nhưng inventory production 22/10 còn 29 PIN hash khớp 0000, 2 giá trị khác định dạng dự kiến. Verifier vẫn ở client; reset phải qua reauth/private verifier/limiter/elevation, không đổi sang PIN chung khác.

**Cập nhật 24/10 — Một phần:** `HABIMON-BE` local có private scrypt verifier, fresh reauth, atomic limiter/elevation và Firebase Admin SDK check `check_revoked=True` để từ chối revoked/disabled users; Auth status-check failure trả `503` fail-closed. Backend suite 45/45 pass với local disposable PostgreSQL và mock Auth. Chưa cấu hình ADC/IAM, deploy/migrate, tích hợp Unity hoặc xử lý legacy PIN; dữ liệu tồn tại trong production không được suy ra đã migrate.

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC/CONDITIONAL.** Nguồn: [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), dòng 154, 235–249, 290–318, 386; [Pin.cs](../Assets/_Backend/Pin.cs), dòng 46–100; [Child.unity](../Assets/_Scenes/Child.unity), UnityEvent dòng 1215.

Email signup và Google provisioning đặt `HashPin("0000")`; SHA-256 không salt. Verify đọc hash về client rồi so sánh tại client; scene switch không thay đổi Firebase identity. Không có buộc thiết lập PIN trước khi cho dùng chức năng phụ huynh. Bộ đếm UI không phải server rate limit. **Đính chính:** nút hiện tại mở popup bằng `GameObject.SetActive(true)`, không gọi `OpenVerifyPopup`, nên đóng/mở không chứng minh reset attempts; nút bị khóa có thể giữ nguyên tới scene reload. Hash 4 chữ số dễ vét cạn nếu đọc được. Không khẳng định hash đã bị lộ. Khắc phục: [R01](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R01), [R03](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R03).

<a id="F03"></a>

### F03 — Xóa document trẻ không xử lý subcollections

**Hiện trạng 22/10 — Một phần:** Inventory 22/10 xác nhận 3 missing child profiles còn 20 descendants (8 habits, 8 logs, 2 pets, 2 rewards), chưa cleanup/restore. Client chỉ xóa 4 collection known; vẫn thiếu authoritative recursive job, pending-write denial, recovery và retention evidence.

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC; dữ liệu production chưa được kiểm kê.** Nguồn: [ChildService.cs](../Assets/_Backend/Service/ChildService.cs), dòng 100–122; [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), các collection habits/habit_logs/habimon/rewards.

`DeleteChildAsync` chỉ gọi `childRef.DeleteAsync()`. Theo [Firebase delete-data](https://firebase.google.com/docs/firestore/manage-data/delete-data), xóa cha không tự xóa subcollections. Nếu không có cleanup ngoài repo, dữ liệu con còn tồn tại dù profile không còn trong danh sách. Đây là vấn đề retention/tính hoàn chỉnh của thao tác xóa, không chứng minh mọi người đọc được dữ liệu mồ côi. Không thấy worker xóa đệ quy trong checkout; chưa biết remote jobs. Khắc phục: [R02](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R02).

**Cập nhật local 19/10/2026:** `ChildService.DeleteChildAsync` hiện xóa theo batch bốn collection được code biết (`habits`, `habit_logs`, `habimon`, `rewards`) trước child document. Auth/Firestore Emulator integration tạo habit/log/reward/pet, xóa child và xác nhận các dữ liệu này mất trong khi hai child còn lại được giữ. Đây không xóa subtree mới/chưa biết, không inventory orphan đã phát sinh từ phiên bản cũ, và không thay worker/server có quyền kiểm soát; F03 vẫn một phần.

<a id="F04"></a>

### F04 — Popup thêm habit tự tắt lần đầu

**Hiện trạng 22/10 — Đã xử lý local:** Start không self-close; scene tests mở lần đầu. [Assets/_UI/AddHabitPopup.cs](../Assets/_UI/AddHabitPopup.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC, cần Play Mode nghiệm thu.** Nguồn: [AddHabitPopup.cs](../Assets/_UI/AddHabitPopup.cs), dòng 42–66; [Parent.unity](../Assets/_Scenes/Parent.unity), component 929335567 dòng 9864, nút mở dòng 21243.

Script gắn trên `Canvas/Mission/Popup_addHabit`, popup inactive, `popupPanel` trỏ chính GameObject 929335565. Button mở dùng SetActive(true). Khi được kích hoạt lần đầu, Start lại SetActive(false). Start không chạy lại mỗi lần mở nên triệu chứng có thể chỉ xảy ra lần đầu. Đây là functional blocker, **không phải bằng chứng mất dữ liệu/bảo mật P0**. Khắc phục: [R04](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R04).

<a id="F05"></a>

### F05 — Context chọn trẻ bị ghi đè và reload sai controller

**Hiện trạng 22/10 — Đã xử lý local:** Resolver ưu tiên selected/remembered/fallback; Childrent gọi đúng Mission hoặc DisplayMission. [Assets/_Backend/FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC.** Nguồn: [DisplayMission.cs](../Assets/_Backend/DisplayMission.cs), dòng 53–70; [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), dòng 41–49; [ChildService.cs](../Assets/_Backend/Service/ChildService.cs), dòng 17–28; [Childrent.cs](../Assets/_Backend/Childrent.cs), dòng 116–128; [Child.unity](../Assets/_Scenes/Child.unity), object 1313833946.

LoadHabitsAsync luôn resolve trẻ đầu tiên rồi ghi đè currentChildId, kể cả đã chọn trẻ khác. Callback chọn trẻ tìm `GetComponent<Mission>()`; object `Misstion` ở Child Scene gắn DisplayMission, nên không gọi reload controller đang dùng. Chọn B lần đầu có thể tải A; lần chọn tiếp theo cũng không refresh đúng. habitLoadVersion không giải quyết sai scope đầu vào. Khắc phục: [R03](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R03).

<a id="F06"></a>

### F06 — Tạo component abstract TMP_Text trong chart

**Hiện trạng 22/10 — Đã xử lý local:** Dùng TextMeshProUGUI cụ thể. [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC.** Nguồn: [HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs), dòng 376, 391; source package `Library/PackageCache/com.unity.ugui@1dee9a4a8364/Runtime/TMP/TMP_Text.cs:111`.

Hai GameObject label/value được tạo bằng `typeof(TMP_Text)`. Package hiện cài khai báo `public abstract class TMP_Text`; phải dùng component cụ thể, thường TextMeshProUGUI cho Canvas. Rendering không thể dựa vào việc Unity tạo abstract component hợp lệ. Exception log/thứ tự lỗi thực tế chưa được quan sát, vì N03 cũng có thể làm lỗi sớm hơn. Khắc phục: [R05](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R05).

<a id="F07"></a>

### F07 — Chế độ Year chỉ tính ngày 1 của mỗi tháng

**Hiện trạng 22/10 — Đã xử lý local:** Year dùng range toàn tháng; test completion 18/10 được tính. [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC.** Nguồn: [HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs), dòng 178–221.

Vòng lặp 12 tháng gọi CreatePointForDate(new DateTime(year, month, 1)); hàm đó chỉ đếm một dateKey. Ví dụ habit hằng ngày, chỉ hoàn thành 18/10/2026: điểm tháng 10 hiện là 0/1, dù tháng có một completion. Tổng năm không tổng hợp toàn bộ ngày. Phép tính sai độc lập với lỗi chart; đổi kiểu component không sửa analytics. Khắc phục: [R06](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R06).

<a id="F08"></a>

### F08 — timeTarget vừa là giờ vừa là nhãn buổi

**Hiện trạng 22/10 — Đã xử lý local:** Typed schedule và clock→period; test các biên 05/12/18h. [Assets/_Backend/Core/HabitSchedule.cs](../Assets/_Backend/Core/HabitSchedule.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC.** Nguồn: [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), dòng 197–199, 429–431; [DisplayMission.cs](../Assets/_Backend/DisplayMission.cs), dòng 134–157; [AddHabitPopup.cs](../Assets/_UI/AddHabitPopup.cs), dòng 44–52.

Habit mẫu lưu 20:00; popup lưu Buổi Sáng/Chiều/Tối. MatchesPeriod tìm từ khóa buổi nên 20:00 không khớp Tối, chỉ xuất hiện tab Tất cả. Chatbot adapter đã phân biệt day_period/local_time nhưng không sửa model nghiệp vụ hay filter UI. Không có quy tắc giờ→buổi thống nhất. Khắc phục: [R07](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R07).

<a id="F09"></a>

### F09 — Hiển thị/ghi completion bỏ qua lịch repeatDays

**Hiện trạng 22/10 — Một phần:** Client dùng scheduler; inventory có 2 completed logs lệch repeatDays hiện tại, chưa chứng minh sai lịch tại lúc ghi do thiếu revisions. Rules active chưa enforce schedule/business policy.

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC.** Nguồn: [DisplayMission.cs](../Assets/_Backend/DisplayMission.cs), dòng 113–124; [HabitLog.cs](../Assets/_Backend/HabitLog.cs), dòng 60–107; [HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs), dòng 252–268.

Danh sách trẻ chỉ lọc isActive và period; write không đọc habit để kiểm tra active/lịch. Habit T2–T6 có thể hiển thị/đánh dấu cuối tuần, trong khi analytics loại khỏi ngày đó. Client và thống kê dùng hai chính sách khác nhau. Server có từ chối write hay không chưa biết. Khắc phục: [R07](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R07), [R08](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R08).

**Cập nhật local 19/10/2026:** `DisplayMission.ShowByPeriod` hiện lọc habit bằng cùng `HabitLog.IsScheduledForDate`, còn transaction `HabitRewardService.SetTodayCompletionAsync` xác minh active/repeatDays trước khi ghi. Auth/Firestore Emulator thử completion cho habit chỉ schedule vào weekday khác và xác nhận request bị từ chối, không tạo daily log. Đây chỉ chứng minh client path; deployed Rules/API chưa kiểm tra lịch nên F09 còn một phần.

<a id="F10"></a>

### F10 — Daily log không có định danh duy nhất/atomicity

**Hiện trạng 22/10 — Một phần:** Deterministic ID và transaction có trong source; production 64 log đều legacy, có 1 duplicate group gồm 2 records xung đột, 1 thiếu dateKey, 9 dangling habit refs. Chưa migration/conflict resolution hoặc server uniqueness/stale-version enforcement.

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC/CONDITIONAL concurrency.** Nguồn: [HabitLog.cs](../Assets/_Backend/HabitLog.cs), dòng 74–106; [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), dòng 554–569.

Hai request đồng thời đọc collection, cùng không thấy log hôm nay, cùng AddAsync tạo ID khác nhau. Sau đó FirstOrDefault có thể chọn một bản ghi trong số trùng; true/false không có quy tắc conflict. Analytics HashSet có chống đếm trùng habit nhưng không sửa trạng thái write/storage. Chưa khẳng định production đã có duplicates. Khắc phục: [R08](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R08).

**Cập nhật local 18/10/2026:** đường `HabitLog.ToggleHabitForTodayAsync` dùng `HabitRewardService.SetTodayCompletionAsync`, transaction đọc/ghi ID xác định từ `(habitId,dateKey)`. `DailyHabitLogIdentity` giữ nguyên format SHA-256 hiện có; tests xác nhận hash tương thích. `AddHabitLogAsync`/`UpsertDailyHabitLogAsync` nay reject ID không khớp habit/date; `UpdateHabitLogAsync` chỉ transaction-update document đã tồn tại khi habit/date trùng, nên không tạo doc mới bằng arbitrary ID. Auth/Firestore Emulator integration chạy hai completion cùng habit/ngày đồng thời và xác nhận chỉ tồn tại một log document. Legacy IDs vẫn có thể được cập nhật nếu identity match. Text search không thấy caller runtime cho hai wrapper trong C# hiện tại. F10 vẫn partial vì duplicate legacy và client tampering/server uniqueness chưa được resolve.

<a id="F11"></a>

### F11 — Approval và reward chưa có vòng lặp hoàn chỉnh

**Hiện trạng 22/10 — Một phần:** Client queue/transaction/progression có trong source. Production có 58 completed, 8 approved logs nhưng không có rewardGranted field hoặc đủ reward snapshots; chưa ledger/server authority/reconciliation. Không tự cộng lại thưởng từ thiếu receipt.

**Bằng chứng baseline/lịch sử:**

**GAP · P1 nếu đã cam kết tính năng.** Nguồn: [HabitLog.cs](../Assets/_Backend/HabitLog.cs), dòng 89–103; [HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs), dòng 239; [Mapping.cs](../Assets/_Mapping/Mapping.cs), dòng 14–17, 43–44, 71.

Completion luôn đặt isApproved=false, analytics không xét requireApproval/isApproved. Có rewardGold/rewardExp/gold/totalExp nhưng không thấy xử lý duyệt, ledger trao thưởng hay cập nhật pet progression end-to-end. Model/seed data không chứng minh chức năng đã triển khai. Nếu roadmap chưa yêu cầu, quản lý như backlog; nếu quảng bá completion phải duyệt, code chưa đáp ứng. Khắc phục: [R09](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R09).

**Cập nhật local 18/10/2026:** Parent UI có queue duyệt; completion và approval cập nhật log, gold, EXP và pet trong Firestore transaction; log mới snapshot reward tại completion và dùng deterministic daily ID. `HabitRewardProgression.Apply` nay tính level bằng phép chia O(1), thay vì vòng lặp theo số level. EditMode test kiểm tra reward 1 tỷ EXP trả đúng level mà không lặp tỷ lần và level overflow bị từ chối. Auth/Firestore Emulator integration test tạo HabitLog có timestamp/bool/int optional, chạy hai request approval đồng thời rồi retry lần nữa, và xác nhận child chỉ nhận 11 gold và 17 EXP tổng cộng; pass 1/1. F11 vẫn một phần: transaction/auth chạy trong client, Rules/server authority và ledger độc lập chưa có; chưa có ledger/reconciliation legacy có thẩm quyền.

<a id="F12"></a>

### F12 — Lịch sử phụ thuộc trạng thái habit hiện tại

**Hiện trạng 22/10 — Một phần:** Probe disable habit mới vẫn làm ngày cũ 1/1→0/0 unknown; chưa activation/schedule revisions. Inventory 81 habits đều thiếu effectiveFrom/createdAt nghiệp vụ, không thể tự bịa lịch sử từ document createTime.

**Bằng chứng baseline/lịch sử:**


**P1 · STATIC.** Nguồn: [HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs), dòng 224–250; [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), dòng 529–550.

Mẫu số và tử số được tính từ habits hiện tồn tại/isActive. Tắt hoặc hard-delete habit rồi refresh làm dữ liệu quá khứ thay đổi; lịch repeat hiện tại cũng bị áp vào ngày cũ. Logs chưa mất không có nghĩa historical analytics còn đúng. Không có schedule revision hoặc occurrence snapshot trong model. Khắc phục: [R06](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R06), [R07](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R07).

<a id="F13"></a>

### F13 — Dashboard không refresh khi đổi trẻ

**Hiện trạng 22/10 — Đã xử lý local:** Subscribe ChildChanged/SessionChanged, commit snapshot sau scope/version guard. [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC.** Nguồn: [HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs), dòng 48–51, 115–143; [Childrent.cs](../Assets/_Backend/Childrent.cs), dòng 116–128.

Habits/logs được cache; caller RefreshAsync tìm thấy trong graph/source là Start. Child selection chỉ set currentChildId và xử lý Mission. Không có subscription vào child change hay binding UnityEvent RefreshAsync được xác nhận trong scene. Sau lần tải đầu, dashboard có thể giữ dữ liệu A khi đã chọn B. Khắc phục: [R03](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R03), [R06](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R06).

<a id="F14"></a>

### F14 — Thêm trẻ không dùng cùng quy trình provisioning

**Hiện trạng 22/10 — Một phần:** Operation ID tạo child+pet có trong source. Inventory 19/51 child thiếu pet (16 parent), 7 không habits, 21 không rewards; chưa repair. Pet/optional seed policy và provenance phải chốt trước backfill.

**Bằng chứng baseline/lịch sử:**

**P1 về nhất quán; GAP về yêu cầu seed.** Nguồn: [ChildService.cs](../Assets/_Backend/Service/ChildService.cs), dòng 125–166; [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), dòng 159–216, 397–445.

AddChild chỉ tạo ChildModel; signup/Google tạo pet, habit và reward mẫu. Nếu pet bắt buộc, trẻ mới thiếu phần đó; habit/reward mẫu có cần bắt buộc hay không là quyết định sản phẩm. Không kết luận mọi trẻ mới đều không hoạt động. Tuổi 0/Unnull của seed còn là placeholder, không phải hồ sơ trẻ thật. Khắc phục: [R10](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R10).

**Cập nhật local 18/10/2026:** AddChild dùng operationId ổn định làm child document ID và transaction để tạo child/pet khi thiếu. Auth/Firestore Emulator integration gọi lại thao tác với cùng ID nhưng khác payload, xác nhận vẫn chỉ có một child và dữ liệu gốc không bị overwrite; `ageAsOf` round-trip được. F14 vẫn một phần vì chưa kiểm kê child cũ hoặc quyết định yêu cầu sample habit/reward.

<a id="F15"></a>

### F15 — Đăng ký Auth/Firestore có thể hoàn tất một phần

**Hiện trạng 22/10 — Một phần:** pending→ready/recovery có trong source; production 33/33 parent đều markerless. Đã kiểm kê Firestore nhưng chưa inventory đầy đủ Auth owner states, chưa repair/backfill hoặc failure-injection mọi write; không được set ready/pending hàng loạt.

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC/CONDITIONAL failure.** Nguồn: [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), dòng 123–230.

Auth user được tạo trước nhiều lần ghi Firestore tuần tự; lỗi sau đó trả failure nhưng không undo user hay ghi trạng thái provisioning để retry. Đăng ký lại cùng email không phải recovery đáng tin cậy vì user đã tồn tại. Auth và Firestore không thể được gộp thành một Firestore transaction. Khắc phục: [R10](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R10).

**Cập nhật local 18–19/10/2026:** khi Auth user đã tạo nhưng provisioning lỗi, `RegisterParentAsync` trả thông báo yêu cầu đăng nhập lại; nếu current Auth UID vẫn là UID vừa đăng ký, client sign out và xóa parent/child session state. `LoginParentAsync` cũng sign out cùng UID nếu xác thực thành công nhưng Firestore provisioning thất bại, để session không bị xem là hợp lệ khi profile chưa đồng bộ. Parent mới có field Firestore `provisioningState: "pending"`; retry seed default child/pet/habit/reward còn thiếu rồi mới đặt `ready`. Emulator integration trên Auth/Firestore thay Rules cục bộ xác minh signup/login thất bại vẫn sign out và marker ở `pending`; một failure injection thứ hai cho phép giao dịch seed default commit nhưng từ chối ghi pet thiếu cho child đã tồn tại, xác nhận marker tiếp tục `pending`, rồi retry thành công đặt `ready`. Test còn xác minh profile `ready` và profile mô phỏng legacy không có marker đều không tự tạo lại default habit/reward đã xóa. Firestore Unity không deserialize `Nullable<Timestamp>` trong POCO; model dùng backing `object` giữ nguyên Firestore field name với accessor nullable, được xác minh cho timestamp/bool/int trên HabitLog. Chưa có server-side state/authorization, migration/inventory legacy hoặc failure-injection ở mọi write, nên F15 vẫn một phần.

<a id="F16"></a>

### F16 — Đọc toàn bộ logs cho từng habit

**Hiện trạng 22/10 — Đã xử lý local:** Tải logs theo ngày một lần cho list, dashboard theo range; chưa đo billed reads. [Assets/_Backend/DisplayMission.cs](../Assets/_Backend/DisplayMission.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC query pattern.** Nguồn: [DisplayMission.cs](../Assets/_Backend/DisplayMission.cs), dòng 218, 234; [HabitLog.cs](../Assets/_Backend/HabitLog.cs), dòng 22–36; [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), dòng 648–675.

N habit tạo N lần GetHabitLogsAsync; mỗi lần query toàn collection lịch sử M records rồi lọc local. Toggle write cũng đọc collection. Số lời gọi/tăng trưởng dữ liệu rõ ràng; latency, cache hit và billed reads chưa đo, nên không coi N×M là hóa đơn đã xác nhận. Khắc phục: [R08](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R08).

<a id="F17"></a>

### F17 — Mission render response cũ khi đổi trẻ nhanh

**Hiện trạng 22/10 — Đã xử lý local:** Request version/session guard trước render; chưa chứng minh mọi A/B race ngoài test. [Assets/_Backend/Mission.cs](../Assets/_Backend/Mission.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC/CONDITIONAL timing.** Nguồn: [Mission.cs](../Assets/_Backend/Mission.cs), dòng 48–103.

Không có version/cancel kiểm tra sau await; response A về sau B có thể clear rồi render A. CurrentParentId/currentChildId còn là mutable fields. AddDataToUi khởi động context refresh và load riêng trong khi load cũng refresh. DisplayMission có version cho một đoạn query, nhưng không áp dụng cho Mission hoặc session logout. Khắc phục: [R03](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R03), [R11](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R11).

<a id="F18"></a>

### F18 — Toggle bị dùng lại sau await dù UI đã destroy

**Hiện trạng 22/10 — Đã xử lý local:** Kiểm tra this/toggle/scope sau await. [Assets/_Backend/DisplayMission.cs](../Assets/_Backend/DisplayMission.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC/CONDITIONAL lifecycle.** Nguồn: [DisplayMission.cs](../Assets/_Backend/DisplayMission.cs), dòng 229–267.

Chỉ kiểm tra toggle trước query, rồi truy cập sau await; đổi tab/reload xóa item đang chờ. Callback ghi cũng dùng toggle sau await trên nhánh rollback. Có thể MissingReferenceException; cần Play Mode với delay để chứng minh log cụ thể. Version của list không bảo vệ item task đã spawn. Khắc phục: [R11](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R11).

<a id="F19"></a>

### F19 — Error handling không thống nhất giữa các async handler

**Hiện trạng 22/10 — Đã xử lý local:** Các handler chính có single-flight/catch/finally; chưa inject failure mọi UI handler. [Assets/_Backend/Login.cs](../Assets/_Backend/Login.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC/CONDITIONAL failure.** Nguồn: [Login.cs](../Assets/_Backend/Login.cs), dòng 41–64; [Pin.cs](../Assets/_Backend/Pin.cs), dòng 84, 127–143; [AddChild.cs](../Assets/_UI/AddChild.cs), dòng 113–145; [ForgetPassword.cs](../Assets/_Backend/ForgetPassword.cs), dòng 35–55.

Pin đổi mã disable nút trước await nhưng không finally; Login resolve lần hai ở ngoài manager catch. ForgetPassword không có finally quanh handler. **AddChild đã có try/finally**, nên không được nói nó hoàn toàn không bảo vệ nút; nó thiếu catch/UI error cho exception phát sinh khi refresh danh sách. GoogleLogin và UpdateChild có nhiều bảo vệ hơn. Không phải async void nào cũng sai. Khắc phục: [R11](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R11).

<a id="F20"></a>

### F20 — Popup habit không phân biệt lưu thành công với return sớm

**Hiện trạng 22/10 — Đã xử lý local:** Submit trả result, chặn double click, thông báo refresh fail; retry đổi scope vẫn còn ở E11. [Assets/_Backend/Mission.cs](../Assets/_Backend/Mission.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC.** Nguồn: [AddHabitPopup.cs](../Assets/_UI/AddHabitPopup.cs), dòng 94–118; [Mission.cs](../Assets/_Backend/Mission.cs), dòng 108–142; [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), dòng 487–501.

SubmitHabitFromPopup trả Task không có result; thiếu context hoặc readiness có thể return bình thường, sau đó popup đóng. Không khóa submit, có thể ghi lặp. **Exception từ write sẽ vượt await và không chạy ClosePopup**; lỗi write thành công nhưng reload thất bại cũng chưa được phân biệt. Khắc phục: [R04](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R04), [R11](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R11).

<a id="F21"></a>

### F21 — Logout giữ lại context trẻ và request cũ

**Hiện trạng 22/10 — Đã xử lý local:** Logout clear context và tăng generation. [Assets/_Backend/FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC; data exposure chưa chứng minh.** Nguồn: [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), dòng 23–48, 475–482.

SignOut không clear currentChildId hoặc tăng generation để vô hiệu request. Tác vụ bắt parent/child trước logout có thể hoàn tất sau đó. Đăng nhập kế tiếp thường resolve lại trẻ, nhưng khoảng chuyển tiếp vẫn có stale state. Nhiều method kiểm tra auth; finding này **không tự chứng minh rò dữ liệu chéo user** hay server cho phép query đó. Khắc phục: [R03](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R03).

<a id="F22"></a>

### F22 — Firebase initialization chỉ có trạng thái thành công

**Hiện trạng 22/10 — Đã xử lý local:** Readiness Task dùng chung, error/event và retry sau failure. [Assets/_Backend/FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC.** Nguồn: [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), dòng 107–118.

CheckAndFixDependenciesAsync không được catch tại handler; status khác Available không được công bố là failed. UI chỉ thấy IsInitialized=false, không biết đang chờ hay đã thất bại, không có retry có quản lý. Tác động startup cụ thể cần runtime SDK failure injection. Khắc phục: [R10](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R10), [R11](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R11).

<a id="F23"></a>

### F23 — Lỗi đăng nhập bị báo sai nguyên nhân

**Hiện trạng 22/10 — Đã xử lý local:** Phân biệt lỗi credentials/network/rate-limit; credentials không enumeration. [Assets/_Backend/FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC.** Nguồn: [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), dòng 323–337; [Login.cs](../Assets/_Backend/Login.cs), dòng 58–63.

Manager catch tất cả Auth/Firestore resolve lỗi bằng thông báo tài khoản không tồn tại; UI bỏ qua message trả về rồi lại dùng cùng thông báo. Sai credentials, mất mạng và profile load failure không phân biệt. Nếu Auth thành công nhưng resolve lỗi, phiên Auth có thể đã tồn tại trong khi UI báo login failure. Thông báo an toàn nên tránh enumeration, nhưng vẫn phải phân biệt transport/profile failure. Khắc phục: [R10](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R10), [R11](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R11).

<a id="F24"></a>

### F24 — Tự lưu bản sao hash mật khẩu bên cạnh Firebase Auth

**Hiện trạng sau thao tác 22/10 — Một phần:** đã xóa `passwordHash/passwordSalt` ở 29 parent (15 nonempty pairs, 14 empty pairs) với updateTime precondition + field mask; toàn scope 33 parent được đọc lại với cả hai field bằng 0. Deployed Rules cũng chặn tái tạo hai fields. Chưa chứng minh mọi writer/client cũ hoặc archive/retention ngoài Firestore; chưa end-to-end Auth/client compatibility.

**Bằng chứng baseline/lịch sử:**

**P1 về giảm bề mặt dữ liệu nhạy cảm · STATIC/CONDITIONAL exposure.** Nguồn: [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), dòng 145–157, 251–288; [Mapping.cs](../Assets/_Mapping/Mapping.cs), dòng 27–28.

Client ghi SHA-256(password+salt) và salt vào user document; VerifyPassword không thấy caller nghiệp vụ. Đây không phải plain-text password, nhưng là bản sao verifier nhanh không cần thiết cho Firebase Auth login. Chưa biết ai đọc được document. Xóa field trong model không tự xóa dữ liệu đã lưu. Khắc phục: [R01](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R01), [R10](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R10).

<a id="F25"></a>

### F25 — Không có Rules và bằng chứng authorization server trong repo

**Hiện trạng sau rollout 22/10 — Một phần:** containment owner-scoped đã deploy và active Rules readback xác nhận không còn broad public allow; release hiện hành `3e9f7ec6-4218-45a7-8f1b-154f609327b1`. Rules Emulator 9/9 pass. Policy vẫn không phân biệt Parent/Child cùng UID hoặc enforce toàn bộ schema/domain operations; phải xem API elevation và client compatibility cùng nhau. [firestore.rules](../firestore.rules)

**Bằng chứng baseline/lịch sử:**

**GAP · P1 trước release.** Nguồn: inventory `git ls-files`, không có `firestore.rules`, `firebase.json` hay Rules tests; [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), các direct writes.

Không thể kiểm chứng isolation giữa user, quyền parent-only, schema, daily uniqueness, gold/EXP/approval write policy. Parent và Child dùng cùng Auth identity nên kiểm tra UID đơn thuần không phân biệt người cầm thiết bị. Không suy ra Rules đang mở hoặc backend không tồn tại. Cần export cấu hình có thẩm quyền và tests trước khi đánh dấu security đạt. Khắc phục: [R01](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R01), [R13](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R13).

**Cập nhật kiểm chứng 18/10/2026:** repo hiện có Firebase CLI config, Rules source, indexes manifest, package manifest và Emulator tests; project `habimon-808e2` đã được chọn. Deployed Rules vừa được re-read read-only: vẫn `allow read, write: if true` cho toàn bộ documents. Local `firestore.rules` nay là prototype owner-scoped policy, không giống policy đang active. Rules Emulator tests pass 6/6 cho anonymous denial, UID owner access and self-create, cross-owner denial, paths ngoài `users` và orphan child denial. Prototype không phân biệt Parent/Child cùng UID, không validate domain fields/operations và chưa deploy; F25 tiếp tục mở cho đến khi mobile/DB owner phê duyệt policy đầy đủ, tests bao phủ app authorization và deployed Rules được xác minh.

**Tái kiểm tra read-only 19/10/2026:** Firebase CLI và Firebase MCP cùng xác nhận active project `habimon-808e2`; Firestore `(default)` là Standard edition, location `asia-southeast1`. Firebase MCP trả lại Rules đang áp dụng vẫn là `allow read, write: if true` cho mọi document. Không có thao tác deploy hoặc ghi dữ liệu production trong lần kiểm tra này.

**Post-rollout verification 22/10/2026:** sau baseline 19/10, Rules containment được deploy lên project `habimon-808e2`; active readback khớp source đã duyệt, release `3e9f7ec6-4218-45a7-8f1b-154f609327b1`. Owner UID/subtree và legacy-verifier retirement được enforce, broad public allow đã bỏ. Đây mới là containment: Parent/Child dùng cùng UID, business-field allowlists và parent-only server authority còn thiếu, nên F25 đổi từ `Mở` sang `Một phần`, chưa close.

<a id="F26"></a>

### F26 — Ngày nghiệp vụ và completion timestamp dựa vào client clock

**Hiện trạng 22/10 — Một phần:** UTC+7 ở client, completion/reward timestamps có server writes; dateKey vẫn client-supplied. Inventory có 1 log thiếu dateKey; không chứng minh clock tampering hoặc trusted server business-date policy.

**Bằng chứng baseline/lịch sử:**

**P1 nếu cần chống gian lận; P2 cho consistency · STATIC.** Nguồn: [HabitLog.cs](../Assets/_Backend/HabitLog.cs), dòng 9–19, 74–103.

DateTime.Today sinh dateKey, Timestamp.GetCurrentTimestamp sinh timestamps. Thay clock/timezone có thể đổi ngày ghi theo client; không có timezone nghiệp vụ cố định. CanEditToday chỉ so ngày client, và lookup đã chọn log hôm nay nên không phải server history lock. Chưa kiểm chứng backend chặn ngày ngoài khoảng. Khắc phục: [R07](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R07), [R08](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R08).

<a id="F27"></a>

### F27 — Google federated-provider API không hỗ trợ desktop

**Hiện trạng 22/10 — Defer theo quyết định người dùng:** Defer kế thừa docs; desktop unsupported, chưa chứng minh mobile provider login. [Assets/_Backend/FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC + tài liệu API.** Nguồn: [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs), dòng 347–352; [FirebaseAuth reference](https://firebase.google.com/docs/reference/unity/class/firebase/auth/firebase-auth#signinwithproviderasync).

SignInWithProviderAsync hỗ trợ iOS/tvOS/Android; platform khác trả unimplemented. Không thể coi lỗi OAuth bằng API này trên Ubuntu Editor là bằng chứng cấu hình Android hỏng. GoogleLogin đã có single-flight và finally, không cần thay vô cớ các bảo vệ đó. Login mobile thật chưa được kiểm chứng. Khắc phục: [R12](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R12).

<a id="F28"></a>

### F28 — Android TTS không đặt/kiểm tra tiếng Việt

**Hiện trạng 22/10 — Đã xử lý local:** Code setLanguage vi-VN và text fallback; chưa Android runtime proof. [Assets/_Backend/Features/TextToSpeech.cs](../Assets/_Backend/Features/TextToSpeech.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC; phát âm CONDITIONAL.** Nguồn: [TextToSpeech.cs](../Assets/_Backend/Features/TextToSpeech.cs), dòng 28–39, 70–78.

TTS được tạo và speak gọi sau status Success nhưng không set Locale vi-VN hay kiểm tra language support. Engine có thể dùng tiếng Việt sẵn, nên không khẳng định mọi thiết bị đọc sai. Nhánh chỉ Android player là có chủ đích; iOS/Editor không có engine nói trong code này. Khắc phục: [R12](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R12).

<a id="F29"></a>

### F29 — Coroutine lời thoại cũ có thể ẩn câu mới

**Hiện trạng 22/10 — Đã xử lý local:** Speak mới stop coroutine cũ; OnDisable clear display/queue. [Assets/_Backend/Features/TextToSpeech.cs](../Assets/_Backend/Features/TextToSpeech.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC/CONDITIONAL timing.** Nguồn: [TextToSpeech.cs](../Assets/_Backend/Features/TextToSpeech.cs), dòng 110, 133, 156–165.

Mỗi Speak khởi động coroutine mới trên cùng displayBoxchat; sau delay, coroutine cũ ẩn box/xóa text dù lời mới chưa hết thời gian hiển thị. Không giữ coroutine handle hoặc generation. Queue audio không giải quyết lifecycle text. Khắc phục: [R12](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R12).

<a id="F30"></a>

### F30 — Coverage nghiệp vụ và CI còn thiếu; không còn đúng nếu nói không có tests

**Hiện trạng 22/10 — Một phần:** Baseline khi đó: EditMode 55/55, PlayMode 9 pass + 1 skip, Rules Emulator 9/9; Auth/Firestore integration thất bại trước assertions nghiệp vụ do native LFS.

**Cập nhật 24/10 — Một phần:** Local Unity EditMode 63/63, Parent popup/chart PlayMode 9/9, Rules Emulator 9/9 và Auth/Firestore provisioning integration 1/1 pass sau khi hydrate native LFS. Chưa có hosted Unity CI, full business authorization hoặc device evidence.

**Bằng chứng baseline/lịch sử:**


**GAP · P1 release gate.** Nguồn: [Chatbot Tests/EditMode](../Assets/_Backend/Chatbot/Tests/EditMode), [Packages/manifest.json](../Packages/manifest.json); inventory Git không có `.github/workflows`.

HEAD có 10 tests trong bốn file chatbot; commit báo cáo chưa có module này. Tests hiện chủ yếu routes/JSON/adapter/parser nhỏ, không có daily-log transaction, authorization, child switch, popup, analytics hoặc real HTTP streaming. Unity EditMode 55/55, PlayMode 9/9 và Auth/Firestore provisioning integration 1/1 là evidence lịch sử; Rules Emulator ban đầu 6/6, cập nhật sau containment 9/9. Các suite này vẫn chưa bao phủ đầy đủ business authorization, toàn bộ Firestore transactions, Android/iOS release/device behavior hoặc chatbot HTTP service end-to-end. Khắc phục: [R13](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R13).

<a id="F31"></a>

### F31 — Chính sách vendor/generated assets chưa được quy định rõ

**Hiện trạng 22/10 — Một phần:** Có policy provenance nhưng 8 native Firebase .so/.bundle còn là LFS pointers; Auth/Firestore integration fail `invalid ELF header`.

**Cập nhật 24/10 — Một phần:** User-local Git LFS 3.4.1 đã hydrate đủ 8 objects; `git lfs fsck --objects` pass; `.so` là ELF x86-64 và `.bundle` là Mach-O universal. `dotnet build Assembly-CSharp.csproj -v:minimal` pass, Unity EditMode 63/63, popup/chart PlayMode 9/9, Rules Emulator 9/9 và Auth/Firestore Emulator integration 1/1 pass. Chưa có clean-clone/resolver regeneration, Android/iOS target build hoặc device evidence.

**Bằng chứng baseline/lịch sử:**


**P3 · MAINTENANCE.** Nguồn: [Assets/_Recovery/0.unity](../Assets/_Recovery/0.unity), [AndroidResolverDependencies.xml](../ProjectSettings/AndroidResolverDependencies.xml), Firebase/GeneratedLocalRepo inventory.

Có recovery scene không nằm trong build và binary SDK/native assets lớn; resolver còn tham chiếu GeneratedLocalRepo. Vendor SDK hoặc Firebase client API key không tự là bug/secret leak. Không xóa thư viện hay cấu hình vì chỉ thấy binary hoặc JSON. Cần chính sách nguồn authoritative và clean-clone build trước cleanup. Khắc phục: [R14](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R14).

**Cập nhật kiểm chứng 18/10/2026:** R14 xác định `Packages/manifest.json` + lockfile là nguồn Unity registry package, Firebase Editor version manifests/dependency XML là nguồn phiên bản Firebase Unity SDK, EDM4U manifest + `AndroidResolverDependencies.xml` là resolver provenance, `Assets/Firebase/m2repository` là SDK Maven source, và `Assets/GeneratedLocalRepo` là resolver output cần giữ cho tới khi có build proof. Trong bản sao cô lập không có `Library`, Unity import và EditMode pass 55/55; bản sao này vẫn có GeneratedLocalRepo hiện tại nên không chứng minh tái tạo artifacts. Khi bỏ GeneratedLocalRepo và gọi EDM4U resolve, resolver dừng với `Build target directory for AndroidPlayer does not exist`, do Android Build Support đã được defer. Không có binary cleanup. F31 vẫn partial vì clean Git checkout và Android resolver/build chưa được chứng minh.

<a id="F32"></a>

### F32 — age không biểu diễn được tuổi thay đổi theo thời gian

**Hiện trạng 22/10 — Đã xử lý local:** Source lưu/hiển thị ageAsOf cho new/updated profile và không bịa legacy date; production 51/51 existing child còn thiếu ageAsOf. Chưa backfill và không dùng document createTime để suy ngày khai tuổi.

**Bằng chứng baseline/lịch sử:**

**P3 · MAINTENANCE/product model.** Nguồn: [Mapping.cs](../Assets/_Mapping/Mapping.cs), dòng 42; [ChildService.cs](../Assets/_Backend/Service/ChildService.cs), ghi age.

Chỉ lưu số tuổi, không có ngày xác định tuổi hoặc birth data; không tự tăng khi dùng lâu dài. Không phải crash/runtime bug. Lưu DOB đầy đủ tăng dữ liệu cá nhân, không nên tự thêm nếu năm sinh hoặc ageAsOf đáp ứng mục tiêu. Khắc phục: [R14](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R14).

## 3. Findings bổ sung từ báo cáo thứ hai — nguyên nhân baseline

<a id="N01"></a>

### N01 — Popup thêm trẻ tự đóng lần đầu

**Hiện trạng 22/10 — Đã xử lý local:** Start không tự tắt popup; PlayMode có first-open test. [Assets/_UI/AddChild.cs](../Assets/_UI/AddChild.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC.** [AddChild.cs](../Assets/_UI/AddChild.cs):17–47; [Parent.unity](../Assets/_Scenes/Parent.unity):2654, component 172714561, popupPanel 1048830174.

Script nằm tại `Canvas/Panel(inactive)/AddChild(activeSelf=true)`. Start tắt Panel ancestor. Nút mở scene kích hoạt Panel; lần activeInHierarchy đầu khiến Start chạy rồi tắt. Không dùng activeSelf của child để suy ra script đã Start khi scene load. Khắc phục: [R04](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R04).

<a id="N02"></a>

### N02 — Popup cập nhật trẻ tự đóng trong Awake lần đầu

**Hiện trạng 22/10 — Đã xử lý local:** Awake không self-close; OpenPopup(child) giữ ID trẻ. [Assets/_UI/UpdateChild.cs](../Assets/_UI/UpdateChild.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC.** [UpdateChild.cs](../Assets/_UI/UpdateChild.cs):21–50, 107–139; [Parent.unity](../Assets/_Scenes/Parent.unity):6110, component 571445108, popupPanel 1286425807.

Script nằm dưới `Canvas/Panel (1)(inactive)` và Awake tắt ancestor. OpenPopup(child) SetActive(true) lần đầu có thể kích hoạt Awake ngay trong quá trình mở; sau đó mới gán editingChildId và inputs. Không có lần SetActive(true) thứ hai trong method để khôi phục popup. Chưa quan sát hình ảnh Play Mode; lifecycle phù hợp tài liệu Unity. Khắc phục: [R04](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R04).

<a id="N03"></a>

### N03 — Bar template là TMP object nằm trong vùng bị Destroy

**Hiện trạng 22/10 — Đã xử lý local:** Không dùng template con bị destroy; runtime tạo bar và text riêng. [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC.** [Parent.unity](../Assets/_Scenes/Parent.unity):3529, 12932–12950, 14429; [HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs):340–368.

chartRoot=RectTransform 265215166; chartBarPrefab=GameObject 1222274430 `Text (TMP)`, component TextMeshProUGUI, là con chartRoot. Clear loop destroy cả template rồi instantiate mẫu text thay bar. Base Graphic trong package có DisallowMultipleComponent, nên AddComponent<Image> có thể bị từ chối; **không khẳng định cả TMP và Image cùng tồn tại hợp lệ**. Destroy có hiệu lực cuối frame nên exact lỗi lần đầu tùy timing. Sau template bị destroy còn E01. Khắc phục: [R05](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R05).

<a id="N04"></a>

### N04 — Logout tích lũy runtime listeners

**Hiện trạng 22/10 — Đã xử lý local:** OnEnable/OnDisable add/remove đúng method group. [Assets/_Backend/Logout.cs](../Assets/_Backend/Logout.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC + PROBE.** [Logout.cs](../Assets/_Backend/Logout.cs):8–26; [Parent.unity](../Assets/_Scenes/Parent.unity):3857, path `Canvas/Settings(inactive)/Viewport/Content/Logout`.

Mỗi Settings enable đăng ký lambda mới; OnDisable không tháo. Probe CoreModule đúng phiên bản cho 3 registrations→3 callbacks. Đây chứng minh invoke pattern, chưa chạy logout/scene load thật. OnDestroy RemoveAllListeners quá rộng đối với runtime listeners do code khác quản lý; nó **không xóa persistent Inspector listeners**. Khắc phục: [R11](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R11).

<a id="N05"></a>

### N05 — Icon key và Sprite references không thống nhất

**Hiện trạng 22/10 — Đã xử lý local:** Scene test resolve homework subasset; fallback/book references; chưa catalog ID độc lập asset names hoàn chỉnh. [Assets/_Backend/Core/Tests/EditMode/SceneAssetIntegrityTests.cs](../Assets/_Backend/Core/Tests/EditMode/SceneAssetIntegrityTests.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC; import resolution CONDITIONAL.** [MissionUI.cs](../Assets/_UI/MissionUI.cs):21–49, 84–95; [Parent.unity](../Assets/_Scenes/Parent.unity):9887, 16198; [homework meta](../Assets/_Art/Image/Icon/homework%201.png.meta).

Default `icon_book`/fallback `icon_default` không có mapping trong danh sách assets gán; lookup dựa Sprite.name. Homework asset Multiple Sprite: popup dùng fileID -3401245754914414353, MissionUI dùng 21300000 cùng GUID 8a340e82fd915ea449d84491cd50dca6. Meta khai báo sprite homework 1_0 với internalID âm đó. Sai khác đã xác nhận; sprite mất hoặc rendered fragment cần AssetDatabase/Editor kiểm tra. Khắc phục: [R07](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R07).

<a id="N06"></a>

### N06 — Habit mới bị áp vào ngày trước khi tồn tại

**Hiện trạng 22/10 — Một phần:** Habit mới có effectiveFrom trong source; inventory 81 legacy habits đều thiếu effectiveFrom/createdAt nghiệp vụ. Đã inventory, chưa migration; provenance không có vẫn phải unknown, không lấy document createTime làm ngày bắt đầu chắc chắn.

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC.** [HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs):224–268; [Mapping.cs](../Assets/_Mapping/Mapping.cs):6–18.

Không có start/end hiệu lực trong HabitModel và predicate không kiểm tra thời điểm tạo. Thêm habit hôm nay rồi xem tháng trước có thể tăng mẫu số dù ngày đó habit chưa tồn tại. Khác F12 ở nguyên nhân ngày bắt đầu, nhưng phải sửa cùng lịch sử schedule. Không được bịa effectiveFrom chính xác cho legacy data không có provenance. Khắc phục: [R06](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R06), [R07](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R07).

<a id="N07"></a>

### N07 — Active toggle không rollback khi ghi thất bại

**Hiện trạng 22/10 — Đã xử lý local:** Toggle pending, await result, rollback lỗi; callback mutation lấy current controller scope, cần thêm delayed integration. [Assets/_UI/MissionUI.cs](../Assets/_UI/MissionUI.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC/CONDITIONAL write failure.** [MissionUI.cs](../Assets/_UI/MissionUI.cs):103–112; [Mission.cs](../Assets/_Backend/Mission.cs):149–164.

UI/model đổi isActive trước, callback Action không có result; async write fire-and-forget không catch/rollback. Khi permission-denied hoặc mạng lỗi, UI có thể báo trạng thái khác server. Việc parentId/childId được refresh tại write còn có thể đổi scope so với item đã render. Khắc phục: [R11](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R11), [R03](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R03).

<a id="N08"></a>

### N08 — completedAt được ghi khi bỏ completion

**Hiện trạng 22/10 — Đã xử lý local:** Uncomplete path clear completedAt; đã granted/approved thì chặn child uncomplete. [Assets/_Backend/Service/HabitRewardService.cs](../Assets/_Backend/Service/HabitRewardService.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC.** [HabitLog.cs](../Assets/_Backend/HabitLog.cs):89–103.

Cả cập nhật isCompleted=false và tạo log false mới đều có completedAt=now. Field không còn nghĩa thời điểm hoàn thành. Nếu dùng cho reward/audit, không thể đọc field này như chứng cứ completion. Cần tách updatedAt và event history hoặc null completedAt trên state false theo đặc tả. Khắc phục: [R08](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R08).

<a id="N09"></a>

### N09 — Default child được chọn theo query order

**Hiện trạng 22/10 — Đã xử lý local:** Selected→remembered theo UID→ordinal fallback. [Assets/_Backend/Core/HabitSchedule.cs](../Assets/_Backend/Core/HabitSchedule.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC.** [ChildService.cs](../Assets/_Backend/Service/ChildService.cs):17–28; [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs):41–48.

FirstOrDefault không phải trẻ gần nhất/được chọn trước đó. Query order không cần ngẫu nhiên để gây bug: business rule default vẫn vắng mặt. Không có user-scoped remembered selection và validation child tồn tại. Đây là nguyên nhân hỗ trợ F05, không cần đếm như lỗi độc lập khi lập effort. Khắc phục: [R03](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R03).

<a id="N10"></a>

### N10 — Reload danh sách trẻ không chống request chồng lấn

**Hiện trạng 22/10 — Đã xử lý local:** Load version/lifecycle/parent guard, render sau khi query hoàn tất. [Assets/_Backend/Childrent.cs](../Assets/_Backend/Childrent.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC/CONDITIONAL timing.** [Childrent.cs](../Assets/_Backend/Childrent.cs):29–57.

Cả hai request clear UI trước await; sau đó cả hai có thể spawn, tạo duplicates hoặc result cũ. Không có request version và kiểm tra this/contentTransform sau await. Add/update/delete đều có thể gọi reload. Destroy UI là deferred nên thời điểm còn ảnh hưởng cụ thể. Khắc phục: [R03](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R03), [R11](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R11).

<a id="N11"></a>

### N11 — Submit đăng ký không có single-flight

**Hiện trạng 22/10 — Đã xử lý local:** Single-flight + finally; provisioning stable seed IDs. [Assets/_Backend/SignUp.cs](../Assets/_Backend/SignUp.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC.** [SignUp.cs](../Assets/_Backend/SignUp.cs):34–97.

Không disable signUpButton hoặc isSubmitting trước RegisterParentAsync. Nhiều request có thể chạy đồng thời; Firebase có thể chặn cùng email nhưng UI response/active Auth identity và provisioning vẫn khó dự đoán. Không khẳng định Auth tạo duplicate user cùng email. Khắc phục: [R10](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R10), [R11](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R11).

<a id="N12"></a>

### N12 — Firebase mobile/desktop config có nhiều bản khác nhau

**Hiện trạng 22/10 — Một phần:** Read-only 22/10: Android/iOS ACTIVE, config app/project/package/bundle khớp, Google provider enabled, Android có SHA-1/SHA-256. Chưa so signing certificate của APK thực tế, clean target build/resolver hoặc device login.

**Bằng chứng baseline/lịch sử:**

**P2 · CONDITIONAL integration; khác biệt STATIC.** `Assets/Data/google-services.json` (đã xóa khỏi worktree; mô tả baseline), [Android JSON](../Assets/Plugins/Android/google-services.json), [desktop JSON](../Assets/StreamingAssets/google-services-desktop.json), [Apple plist](../Assets/Data/GoogleService-Info.plist).

JSON cùng project habimon-808e2, Android package com.GSAM.HABIMON. OAuth entries tương ứng 0/2/2. Không in API keys/client credentials vào audit. Chưa xác định nguồn resolver đang chọn hoặc build mobile sử dụng bản nào; desktop config có thể được SDK sinh từ mobile. Đây chưa phải build bug đã tái hiện. Khắc phục: [R12](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R12).

**Cập nhật kiểm chứng 18/10/2026:** Firebase CLI read-only liệt kê đúng một Android app và một iOS app ACTIVE trong `habimon-808e2`. Android app ID/package khớp `google-services.json`; iOS app ID/bundle khớp `GoogleService-Info.plist` và `ProjectSettings/ProjectSettings.asset` (`com.GSAM.HABIMON`). N12 được giảm một phần về config provenance; clean Android/iOS build, resolver output trên clean clone và provider login thật vẫn chưa được kiểm tra. Client keys không được ghi vào báo cáo.

<a id="N13"></a>

### N13 — FirebaseTester có write thử nghiệm trong Start

**Hiện trạng 22/10 — Đã xử lý local:** File và meta đã xóa trong worktree; không còn script auto-write. `Assets/_Backend/FirebaseTester.cs`

**Bằng chứng baseline/lịch sử:**

**P2 · CONDITIONAL dormant risk.** `Assets/_Backend/FirebaseTester.cs` (đã xóa khỏi worktree; mô tả baseline):7–23, 31–47.

IDs parent_001/child_001 hardcode, chờ 2 giây rồi AddHabitAsync nếu initialized. Quét GUID trong tất cả scene/prefab/asset hiện có không tìm thấy binding. Không có chứng cứ nó đang tự ghi ở build hiện tại; nếu được gắn sau này có thể ghi lên project đang configured. Khắc phục: [R13](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R13).

<a id="N14"></a>

### N14 — Chart labels và zero bar chưa có quy tắc hiển thị đúng

**Hiện trạng 22/10 — Đã xử lý local:** 0% bar height 0, no-task em dash, content rộng; chưa visual QA mọi viewport. [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC geometry; visual overlap CONDITIONAL.** [HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs):353–404.

31 cột trên fallback width 600 cho spacing 19,35; label/value widths 50/60 chồng rectangle. Glyph có chồng hay không cần kiểm tra viewport/font. Mathf.Max(10f, ratio*height) khiến 0% vẫn có bar 10. Đây là thiết kế biểu diễn dữ liệu cần quyết định, ngoài blockers F06/N03/E01. Khắc phục: [R05](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R05).

## 4. Findings mở rộng ở HEAD baseline — không gồm sửa worktree

<a id="E01"></a>

### E01 — Chart fallback tạo bar ngoài chartRoot

**Hiện trạng 22/10 — Đã xử lý local:** Bar/text SetParent chartRoot và generated objects được cleanup. [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC.** [HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs):361–374.

Nhánh Instantiate có parent, nhánh new GameObject không SetParent(chartRoot). Sau template null/destroyed, bar là root object không dưới Canvas chart; labels/value vẫn parent chartRoot. Cleanup chỉ destroy chartRoot children nên root bars còn không được cleanup bằng vòng lặp này. Sửa TMP không đủ. Khắc phục: [R05](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R05).

<a id="E02"></a>

### E02 — SSE decoder không xử lý chunk vượt char buffer

**Hiện trạng 22/10 — Đã xử lý local:** Decoder.Convert incremental loop, tests chunk lớn và split UTF-8. [Assets/_Backend/Chatbot/ChatbotSseParser.cs](../Assets/_Backend/Chatbot/ChatbotSseParser.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC + PROBE.** [ChatbotSseParser.cs](../Assets/_Backend/Chatbot/ChatbotSseParser.cs):11, 23; [ChatbotApiClient.cs](../Assets/_Backend/Chatbot/ChatbotApiClient.cs):271–294.

Feed giải mã toàn chunk vào char[4096] bằng GetChars. DownloadHandler buffer byte[8192] và Drain không chia nhỏ. Source probe nhận 4097 hoặc 8192 ASCII bytes ném ArgumentException; nhiều frame nhỏ được network gộp cũng đủ gây lỗi, không cần message riêng cực lớn. Exception chưa được chuyển thành onError trong SendMessage. Không chứng minh mọi response network có chunk này. Khắc phục: [R15](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R15).

<a id="E03"></a>

### E03 — SSE multiline rỗng mất dữ liệu; EOF policy khác chuẩn

**Hiện trạng sau fix 22/10 — Đã xử lý local:** `DispatchEvent` clear eventName cho frame không data. Regression feed LF/CRLF/CR từng byte và Chatbot EditMode 29/29 pass trong isolated Unity test copy. [Assets/_Backend/Chatbot/ChatbotSseParser.cs](../Assets/_Backend/Chatbot/ChatbotSseParser.cs)

**Bằng chứng baseline/lịch sử:**


**P2 · PROBE cho mất newline; CONDITIONAL compatibility cho EOF.** [ChatbotSseParser.cs](../Assets/_Backend/Chatbot/ChatbotSseParser.cs):28–40, 101–120; [ChatbotSseParserTests.cs](../Assets/_Backend/Chatbot/Tests/EditMode/ChatbotSseParserTests.cs):32–45.

Append newline chỉ khi data.Length>0 nên `data:` rỗng đầu tiên bị mất trong multiline. Complete còn dispatch event cuối dù không có blank separator, khác [WHATWG SSE](https://html.spec.whatwg.org/multipage/server-sent-events.html). Tests hiện cố ý chấp nhận EOF flush; phải chốt compatibility contract, không đổi hành vi đó ngầm. SendMessage chỉ xem tên done/error là terminal, không validate payload trước khi coi stream hoàn tất. Khắc phục: [R15](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R15).

<a id="E04"></a>

### E04 — Exception của consumer callback bị coi là response lỗi

**Hiện trạng 22/10 — Đã xử lý local:** Consumer callback ngoài catch parse; transport regression có thật. [Assets/_Backend/Chatbot/ChatbotApiClient.cs](../Assets/_Backend/Chatbot/ChatbotApiClient.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC/CONDITIONAL callback failure.** [ChatbotApiClient.cs](../Assets/_Backend/Chatbot/ChatbotApiClient.cs):245–258, 202–206.

SendJson đặt FromJson và onSuccess trong cùng catch(Exception). Nếu response hợp lệ nhưng callback UI throw, onError nhận invalid_response, che nguyên nhân lỗi consumer. Callback onError trong catch còn có thể throw tiếp. SSE onEvent throw đi thẳng khỏi coroutine, không phải một network error được quản lý. Khắc phục: [R16](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R16).

<a id="E05"></a>

### E05 — Chat requests thiếu deadline/cancellation và giới hạn stream buffers

**Hiện trạng 22/10 — Một phần:** Snapshot sở hữu cancellation, scope change/Dispose abort; chưa consuming view lifecycle. [Assets/_Backend/ChatbotIntegration/FirebaseChatbotContextAdapter.cs](../Assets/_Backend/ChatbotIntegration/FirebaseChatbotContextAdapter.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC; thời gian treo/bộ nhớ CONDITIONAL.** [ChatbotApiClient.cs](../Assets/_Backend/Chatbot/ChatbotApiClient.cs):128–221, 277–294; [ChatbotSseParser.cs](../Assets/_Backend/Chatbot/ChatbotSseParser.cs):9–10, 43–76.

Không set timeout, không có API cancel handle/token; while stream chờ operation.isDone vô hạn theo policy client hiện có. Byte queue, buffered line và event data không có cap. Coroutine bị host dừng hoặc server giữ stream/line dài cần cleanup/deadline rõ ràng. Không khẳng định leak/DoS đã xảy ra. Khắc phục: [R16](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R16).

**Cập nhật local 18/10/2026:** Chatbot client nay có timeout, cancellation handle và stream limits. `FirebaseChatbotContextSnapshot` gắn cancellation với parent/child/session scope; đổi scope hủy active request, còn `Dispose()` hủy request và tháo listeners. E05 vẫn partial vì repo không có consuming host để kiểm chứng rằng `Dispose()` luôn được gọi khi view/host kết thúc.

<a id="E06"></a>

### E06 — API client chấp nhận HTTP khi gửi bearer token/context

**Hiện trạng 22/10 — Đã xử lý local:** Client yêu cầu HTTPS, HTTP chỉ loopback với cờ development. [Assets/_Backend/Chatbot/ChatbotApiClient.cs](../Assets/_Backend/Chatbot/ChatbotApiClient.cs)

**Bằng chứng baseline/lịch sử:**

**P1 nếu dùng ngoài môi trường local · CONDITIONAL.** [ChatbotApiClient.cs](../Assets/_Backend/Chatbot/ChatbotApiClient.cs):94–118, 224–228.

Constructor/routes chấp nhận http và https; Authorization gửi owner token. Nếu host cấp URL HTTP ngoài local, token và dữ liệu trẻ không được TLS bảo vệ. Không có URL deployment hardcode trong module và chưa biết host config; do đó đây là thiếu enforce production transport, không phải chứng cứ production đang dùng HTTP. Khắc phục: [R16](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R16).

<a id="E07"></a>

### E07 — Hai refresh thống kê có thể trộn habits/logs khác scope

**Hiện trạng 22/10 — Đã xử lý local:** Tasks tải local data rồi swap sau scope/version guard; không AddRange xen kẽ await. [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs)

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC/CONDITIONAL overlapping refresh.** [HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs):115–143.

Refresh clear shared lists rồi AddRange sau hai awaits, không dựng local snapshot/version. Hai refresh A/B xen kẽ có thể add cả hai tập habits/logs hoặc trộn scope; không chỉ last-response-wins. Hiện chỉ Start gọi trực tiếp, nên overlap cần caller mới/manual hoặc sửa F13; **phải sửa E07 đồng thời khi thêm auto-refresh**. Khắc phục: [R03](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R03), [R06](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R06).

<a id="E08"></a>

### E08 — Google retry bỏ qua child được provisioning dở dang

**Hiện trạng 22/10 — Một phần:** Marker pending/ready/retry đã có trong source; inventory 33 parent markerless và 19 child thiếu pet. Đã có phạm vi classifier/repair; chưa migrate. Không blind pending/reseed hoặc hồi sinh dữ liệu chủ động xóa.

**Bằng chứng baseline/lịch sử:**

**P1 · STATIC/CONDITIONAL partial failure.** [FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs):392–445.

EnsureGoogleParentDataAsync return ngay khi children.Count>0. Nếu lần đầu child.SetAsync thành công nhưng pet/habit/reward write fail, lần login tiếp theo thấy child và không repair phần thiếu. Hai provisioning chạy đồng thời cũng có thể cùng thấy zero rồi tạo hai GUID. Single-flight GoogleLogin chỉ bảo vệ instance UI, không phải đa thiết bị. Khắc phục: [R10](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R10).

**Cập nhật local 19/10/2026 — xử lý một phần:** parent mới bắt đầu với `provisioningState: "pending"`; đăng nhập retry tạo phần còn thiếu bằng transaction, kể cả khi child khác đã tồn tại, rồi chỉ chuyển sang `ready` sau khi các bước provisioning hoàn tất. Auth/Firestore Emulator inject một lỗi trước seed và lỗi sau khi default seed commit nhưng pet của child có sẵn không ghi được; cả hai giữ `pending`, retry sau đó hoàn tất. Test xóa default habit/reward rồi đăng nhập lại khi `ready`, sau đó gỡ marker để mô phỏng legacy profile và xác nhận cả hai trạng thái đều không auto-seed lại dữ liệu đã xóa. Việc inventory/classify/backfill legacy cần mobile/DB owner; trường hợp markerless partial provisioning trước khi nâng phiên bản sẽ cần migration có chủ đích.

<a id="E09"></a>

### E09 — UI tải một lần trước readiness rồi không tự phục hồi

**Hiện trạng 22/10 — Đã xử lý local:** UI await readiness; failure có message/retry ở manager; không chứng minh mọi startup native failure. [Assets/_Backend/FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · STATIC/CONDITIONAL startup order.** [Childrent.cs](../Assets/_Backend/Childrent.cs):18–35; [DisplayMission.cs](../Assets/_Backend/DisplayMission.cs):25–28, 55–59; [HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs):48–51, 117–121.

Start gọi load; IsInitialized=false thì return, không await readiness hay subscribe init thành công. Menu auth gate có thể tránh trong đường đi bình thường; mở scene trực tiếp hoặc startup/recovery khác vẫn có thể bỏ tải vĩnh viễn tới reload thủ công. Không coi thứ tự Start giữa GameObjects là bảo đảm readiness. Khắc phục: [R10](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R10), [R11](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R11).

<a id="E10"></a>

### E10 — PIN verification cho phép nhiều request trước khi attempts tăng

**Hiện trạng sau bước BE local 22/10 — Một phần:** PIN UI single-flight/reopen in-flight đã sửa; HABIMON-BE có private scrypt verifier, account limiter và elevation 5 phút cùng proof-based reset. API/migration chưa deploy hoặc tích hợp Unity; production Firestore vẫn còn PIN field/hash, nên client chưa chuyển quyền verify.

**Bằng chứng baseline/lịch sử:**

**P2 client reliability; security liên quan F02 · STATIC.** [Pin.cs](../Assets/_Backend/Pin.cs):68–100.

OnVerifyPinClicked không isVerifying/disable trước await; failedAttempts chỉ tăng sau response. Nhiều lần nhấn có thể gửi quá ba request khi bộ đếm chưa đổi; success/failure còn race với scene load. UI MaxAttempts không giới hạn server guessing và reload scene tạo instance mới. Khắc phục: [R01](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R01), [R11](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R11).

**Cập nhật local 19/10/2026:** handler hiện single-flight bằng `isVerifying` trước await, disable nút trong lúc verify và khôi phục qua `finally`. EditMode test mới phát hiện `OpenVerifyPopup()` vẫn reset counter/bật nút nếu popup được mở lại trong khi request cũ chạy; code giờ giữ count và nút disabled tới khi request kết thúc. `ChildPinEntryButtonRunsPinPopupResetBeforeShowing` fail trước sửa (`/tmp/HABIMON-pin-inflight-red.xml`), pass sau sửa (`/tmp/HABIMON-pin-inflight-green.xml`); full EditMode pass 55/55 (`/tmp/HABIMON-e10-editmode.xml`). Server-side guessing vẫn không được rate limit hoặc verifier riêng bảo vệ; E10/F02 vẫn một phần.

<a id="E11"></a>

### E11 — Cancel/create và reload failure không phân biệt write đã commit

**Hiện trạng 22/10 — Một phần:** Giữ operation ID nhưng thiếu parent/child/generation gắn với operation; retry sau đổi trẻ có thể ghi scope mới.

**Cập nhật 24/10 — Một phần:** `PendingHabitCreate`/`PendingChildCreate` giữ scope và payload get-only. FirebaseManager kiểm tra parent/child/session generation cho habit và parent/session generation cho child create; Firestore transactions tạo nếu chưa có, xác nhận retry payload khớp và từ chối payload khác thay vì ghi đè. Core EditMode 63/63, popup/chart PlayMode 9/9 và Auth/Firestore Emulator integration 1/1 pass; integration kiểm tra child switch, A→B→A generation, matching/conflicting retries và explicit resume tại cùng target. Chưa kiểm thử đổi Firebase Auth account A→B, transport response loss thật, cancel trong lúc write, popup destroy hoặc process-restart recovery; E11 chưa đóng. [Assets/_UI/AddHabitPopup.cs](../Assets/_UI/AddHabitPopup.cs)

**Bằng chứng baseline/lịch sử:**


**P2 · STATIC/CONDITIONAL timing/failure.** [AddChild.cs](../Assets/_UI/AddChild.cs):72–87, 113–139; [AddHabitPopup.cs](../Assets/_UI/AddHabitPopup.cs):83–117; [Mission.cs](../Assets/_Backend/Mission.cs):140–141.

AddChild chỉ disable nút submit, cancel vẫn có thể đóng popup khi write đang chạy; request không bị rollback bởi ClosePopup. Sau write thành công, reload lỗi làm handler không hoàn tất như người dùng kỳ vọng; retry create dùng GUID/AddAsync mới có thể tạo bản ghi nữa. Habit có cùng sự nhập nhằng write-success/reload-failure. Không áp điều này cho UpdateChild, vốn có isSaving và khóa cancel. Khắc phục: [R04](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R04), [R10](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R10), [R11](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R11).

<a id="E12"></a>

### E12 — Chatbot bridge cần snapshot được host bảo đảm cùng child/session

**Hiện trạng 22/10 — Một phần:** Loader scoped + explicit date range; không có production caller hoặc runtime A/B integration proof. [Assets/_Backend/ChatbotIntegration/FirebaseChatbotContextAdapter.cs](../Assets/_Backend/ChatbotIntegration/FirebaseChatbotContextAdapter.cs)

**Bằng chứng baseline/lịch sử:**

**P2 · CONDITIONAL integration contract.** [FirebaseChatbotContextAdapter.cs](../Assets/_Backend/ChatbotIntegration/FirebaseChatbotContextAdapter.cs):10–77; [ChatbotContextAdapter.cs](../Assets/_Backend/Chatbot/ChatbotContextAdapter.cs):58–74, 152–175.

Bridge nhận selectedChild và các collections độc lập, không có scope envelope để kiểm tra cùng parent/child/version. Log chỉ join bằng habitId; pet được nhận như live value nếu caller truyền vào. Nếu host trộn caches khi đổi trẻ, context có thể gán dữ liệu B cho tên A. Chưa có host caller nên không kết luận runtime đã trộn; đây là invariant bắt buộc khi tích hợp, tránh sửa adapter thành tự chọn trẻ đầu tiên. Khắc phục: [R17](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R17).

**Cập nhật local 18/10/2026:** `FirebaseChatbotContextAdapter.LoadCurrentSnapshotAsync` tải child/habits/rewards/pet cùng bounded log date range theo cùng parentId và childId, gắn `ChatbotSessionScope` gồm parent, child và `SessionGeneration`, rồi trả `null` nếu scope đổi trong lúc đọc. Kiểm thử scope thuần là 5/5 pass. E12 giảm xuống một phần; vẫn thiếu caller trong consuming app và kiểm thử Firebase/runtime cho A→B/account switch.

<a id="E13"></a>

### E13 — Chatbot là module, chưa phải chức năng UI đã tích hợp

**Hiện trạng 22/10 — Defer theo quyết định người dùng:** Module-only theo docs; tìm graph+source không thấy host tạo client/call snapshot loader. [Assets/_Backend/Chatbot/README.md](../Assets/_Backend/Chatbot/README.md)

**Bằng chứng baseline/lịch sử:**

**GAP theo mục tiêu release; không phải lỗi của scope module.** [Chatbot README](../Assets/_Backend/Chatbot/README.md), [ChatbotApiClient.cs](../Assets/_Backend/Chatbot/ChatbotApiClient.cs), [FirebaseChatbotContextAdapter.cs](../Assets/_Backend/ChatbotIntegration/FirebaseChatbotContextAdapter.cs).

Graph và text search không thấy production host tạo ChatbotApiClient/call bridge; module không thêm scene/button/token persistence/backend URL. README và plan trước ghi rõ UI/auth/config thuộc consuming app. Không đánh giá thiếu UI là hồi quy hoặc tự ý mở rộng module; nếu release cần chat, integration/deployed API phải có acceptance evidence riêng. Backend scope hashing/owner authorization trong README là contract dự kiến, chưa được audit độc lập ở repo này. Khắc phục: [R17](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R17).

<a id="E14"></a>

### E14 — docs bị ignore, tài liệu local không tự được version-control

**Hiện trạng 22/10 — Đã xử lý local:** Whitelist 5 docs tồn tại; git ls-files docs rỗng, chưa team/clone persistence. [.gitignore](../.gitignore)

**Bằng chứng baseline/lịch sử:**

**P3 · MAINTENANCE.** [.gitignore](../.gitignore):109; `git check-ignore -v docs/audit-findings.md` trả rule `docs/`.

Hai tài liệu này được ghi đúng nơi người dùng yêu cầu nhưng sẽ không hiện như untracked trong git status thông thường. Đây là chính sách hiện có, không phải file chưa được tạo. Không tự đổi ignore hay stage/commit trong lượt audit. Nếu muốn dùng tài liệu xuyên clone/team, cần một thay đổi policy hoặc force-add có chủ đích. Khắc phục: [R14](CODEBASE_REMEDIATION_PLAN_2026-10-18.md#R14).

**Cập nhật local 18/10/2026:** `.gitignore` hiện allowlist đúng bốn tài liệu audit/setup được yêu cầu, nên chúng hiện trong worktree status và có thể được version-control nếu sau này được commit. Không mở cả thư mục `docs/`; không stage/commit trong lần cập nhật này. Clean clone/team visibility chưa được chứng minh bằng commit/checkout mới.

## 5. Bằng chứng hiện tại và giới hạn

Baseline readback đầu ngày 22/10 thấy deployed Rules public; sau đó Rules containment owner-scope và legacy-verifier block đã được deploy/readback. Source cũng có readiness/session guards, client transaction, approval UI, deterministic log IDs, Rules source/tests và nhiều sửa UI. Không còn đúng nếu gọi toàn bộ các mục này là chưa triển khai. Tuy nhiên:

- Baseline public policy đã đọc được không phải evidence đã thử khai thác/cross-user data; lượt inventory bổ sung 22/10 đã đọc projections và aggregate orphan/duplicates/verifiers, không thử khai thác anonymous/cross-user data. Sau inventory, 29 parent legacy password verifier fields đã được xóa bằng field mask/precondition; PIN fields chưa đổi.
- C# compile và 55 EditMode/9 PlayMode pass không chứng minh Firebase native SDK hoạt động: integration hiện fail do LFS pointer.
- HABIMON-BE có local PIN reauth/verifier/limiter/elevation code, nhưng chưa deploy hoặc tích hợp Unity; thiếu parent/domain authorization runtime, authoritative reward ledger/completion/date, recursive deletion worker và nhiều migration evidence.
- Chưa có APK/AAB, Android/iOS device OAuth/TTS, hosted CI, clean resolver/clone hoặc chatbot host→actual API→history proof.
- Hành vi E11 đổi scope là STATIC; F12 probe vẫn tái hiện thiếu revisions. E03 parser bug đã được sửa local và test pass. Không biến các nhận định có điều kiện thành sự cố production đã quan sát.
- Báo cáo ban đầu chỉ audit/read-only; các hành động sau inventory ngày 22/10 được ghi ở §0 và action plan. Không có thay đổi nào được stage/commit/push.

## 6. Tài liệu API đối chiếu

1. [Firestore xóa document/subcollections](https://firebase.google.com/docs/firestore/manage-data/delete-data) — F03.
2. [Firestore transactions và batched writes](https://firebase.google.com/docs/firestore/manage-data/transactions) — thiết kế atomic write; không bao gồm Auth transaction.
3. [FirebaseAuth.SignInWithProviderAsync](https://firebase.google.com/docs/reference/unity/class/firebase/auth/firebase-auth#signinwithproviderasync) — F27.
4. [Unity 6000.3 Awake](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Awake.html), [Start](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/MonoBehaviour.Start.html) — F04/N01/N02.
5. [Decoder.GetChars](https://learn.microsoft.com/en-us/dotnet/api/system.text.decoder.getchars), [Decoder.Convert](https://learn.microsoft.com/en-us/dotnet/api/system.text.decoder.convert) — E02.
6. [WHATWG SSE parsing](https://html.spec.whatwg.org/multipage/server-sent-events.html#parsing-an-event-stream) — E03; EOF compatibility hiện là quyết định khác chuẩn, có test ghi nhận.
7. [UnityWebRequest.timeout](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Networking.UnityWebRequest-timeout.html) — E05; timeout mặc định 0 không áp dụng timeout.

## 7. Bản đồ codebase và dữ liệu hiện tại — 22/10

| Lớp | Đã có trong source/worktree | Giới hạn |
|---|---|---|
| Menu/auth/PIN | Readiness, provisioning retry, single-flight handlers, mode scene switch | Parent button gọi SceneManage trực tiếp; PIN client không tạo server authorization |
| FirebaseManager/ChildService | Session generation/events, selected/remembered child, pending/ready seed, child+pet transaction, known-subcollection delete | Native loading đang bị LFS chặn; thiếu recursive worker/server authority/migration |
| Parent/Child UI | First-open popup fixes, correct Mission/DisplayMission reload, dashboard scope guard, approval queue | Pending create/retry chưa giữ scope đầu tiên (E11) |
| Habit/analytics | Typed schedule, UTC+7, daily hash ID, client completion/reward transaction, range analytics, effectiveFrom/archive | F12 thiếu revisions; server date/ledger/authorization chưa có |
| Chatbot module/bridge | HTTP/SSE deadlines/cancel/limits/HTTPS, privacy JSON, scoped snapshot loader; E03 reset fix và parser suite pass | Chưa consuming host/UI/runtime integration; E05/E12 caller proof còn thiếu |
| Vendor/build/test | Firebase 13.15.0, Unity 6000.3.11f1, LFS patterns, test assemblies và Rules workflow source | 8 native pointers chưa hydrate; native integration fail, chưa platform/device/hosted CI proof |

```text
users/{parentId}                         email, pincode (client verifier), provisioningState
  children/{childId}                     ChildModel; ageAsOf nếu có provenance
    habimon/status                      pet state
    habits/{habitId}                     typed schedule, effectiveFrom, archivedAt
    habit_logs/daily_{SHA256(habitId+"\n"+dateKey)}
                                        completion/approval/reward snapshots và receipt
    habit_logs/{legacyLogId}             legacy docs có thể còn tồn tại, chưa inventory/migrate
    rewards/{rewardId}                   reward definitions
```

Schema/path trên là source client hiện tại, không phải remote inventory. Không còn đường ghi passwordHash/passwordSalt riêng; dữ liệu cũ chưa được chứng minh đã xóa. Receipt trong daily log không thay authoritative reward ledger độc lập. Prototype Rules local không validate các business invariants trên.

Package lock xác nhận UGUI 2.0.0, Test Framework 1.6.0, Input System 1.19.0 và URP 17.3.0. Android Player Settings dùng package `com.GSAM.HABIMON`, min SDK 25, target SDK automatic, IL2CPP, ARM64; resolver package versions tương thích cấu hình đã ghi. Không phát hiện mâu thuẫn đủ bằng chứng để tự kết luận Gradle/platform compile lỗi. Import metadata có platform entries cho native plugins; sự có mặt entries không thay cho native loading test.

## 8. Tái kiểm tra hiện trạng và baseline

### 8.1. Probes đã tái hiện ngày 22/10

Tạo console project tạm ngoài repo, compile trực tiếp `HabitSchedule.cs`, `ChatbotSseParser.cs` và `ChatbotContracts.cs` từ worktree (không copy lại implementation khác). Dùng `System`, `System.Text`, `HABIMON.Core` và `HABIMON.Chatbot`; các statements sau không gọi Firebase/network:

```csharp
var habit = new HabitOccurrence {
    HabitId = "h", EffectiveFrom = new DateTime(2026, 10, 1),
    IsActive = true, RepeatDays = new[] { 1, 2, 3, 4, 5, 6, 7 }
};
var logs = new[] { new HabitCompletion {
    HabitId = "h", Date = new DateTime(2026, 10, 18),
    IsCompleted = true, RequiresApprovalAtCompletion = false
} };
var start = new DateTime(2026, 10, 18);
var end = start.AddDays(1);
var before = HabitAnalytics.Calculate(start, end, new[] { habit }, logs);
habit.IsActive = false;
var after = HabitAnalytics.Calculate(start, end, new[] { habit }, logs);
Console.WriteLine($"before={before.Completed}/{before.Total}, unknown={before.HasUnknownCoverage}");
Console.WriteLine($"after={after.Completed}/{after.Total}, unknown={after.HasUnknownCoverage}");
// Observed: before=1/1 unknown=False; after=0/0 unknown=True.

var parser = new ChatbotSseParser();
parser.EventReceived += e => Console.WriteLine($"event={e.eventName}, data={e.data}");
byte[] bytes = Encoding.UTF8.GetBytes("event: done\n\ndata: payload\n\n");
parser.Feed(bytes, bytes.Length);
parser.Complete();
// Observed: event=done, data=payload. Expected: event=message, data=payload.
```

Đây là source probes .NET 10 cho pure logic, không phải UI+Firebase integration hoặc Android behavior. E11 chỉ được đối chiếu STATIC qua AddHabitPopup→Mission.SubmitHabitFromPopup→FirebaseManager.AddHabitAsync; chưa có runtime test đổi scope chứng minh end-to-end.

### 8.2. Cách tái kiểm tra baseline 18/10 — bằng chứng lịch sử

Các lệnh sau chỉ đọc trạng thái; so sánh SHA trước khi dùng line references:

```bash
git rev-parse HEAD
git branch --show-current
git status --short
node .gitnexus/run.cjs status
git diff c849dbf HEAD -- Assets/_UI Assets/_Scenes Assets/_Prefab Assets/_Mapping
git check-ignore -v docs/CODEBASE_AUDIT_FINDINGS_2026-10-18.md
```

Để tái hiện lỗi decoder/multiline của baseline, tạo project console tạm ngoài repo, dùng `git show 4867659:Assets/_Backend/Chatbot/ChatbotSseParser.cs` và contracts cùng commit, không dùng parser worktree đã sửa. Compile source baseline (không viết implementation giả). Logic test tối thiểu:

```csharp
foreach (int n in new[] { 4096, 4097, 8192 })
{
    var parser = new HABIMON.Chatbot.ChatbotSseParser();
    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(new string('a', n));
    try { parser.Feed(bytes, bytes.Length); Console.WriteLine($"{n}: OK"); }
    catch (Exception ex) { Console.WriteLine($"{n}: {ex.GetType().Name}"); }
}
var multiline = new HABIMON.Chatbot.ChatbotSseParser();
multiline.EventReceived += e => Console.WriteLine(e.data.Replace("\n", "<LF>"));
byte[] input = System.Text.Encoding.UTF8.GetBytes("data:\ndata: x\n\n");
multiline.Feed(input, input.Length); // baseline 18/10 trả x; parser worktree đã sửa empty multiline thành <LF>x
```

UnityEvent probe cần reference `Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll` của **6000.3.11f1**. Trong một class instance có counter/event, gọi cùng method đăng ký `evt.AddListener(() => counter++)` ba lần rồi `evt.Invoke()`: counter=3. Không cần Firebase network hoặc load scene cho kiểm tra managed callback này. Source/dll đã được dùng trong probe baseline 18/10; console runtime là .NET 10, không phải Unity Mono/IL2CPP.

Scene bindings có thể tái kiểm tra bằng parser Unity YAML: resolve component.m_GameObject→RectTransform.m_Father→ancestor GameObject.m_IsActive, rồi resolve serialized popupPanel/Mission/chartRoot/chartBarPrefab. Không dùng chỉ `m_IsActive` của script child để suy activeInHierarchy. Đối với N05 phải dùng Unity AssetDatabase/Play Mode trước khi kết luận sprite resolution thực tế.

## 9. Inventory source baseline 18/10 — line counts lịch sử

Inventory dưới đây giữ số dòng tại lượt audit gốc, không phải số dòng worktree 22/10; không bao gồm vendor DLL hoặc source package cache. Các line references ở trên gắn với SHA ở đầu tài liệu, sẽ phải refresh nếu code thay đổi.

| File | Dòng |
|---|---:|
| [Assets/_Backend/Chatbot/ChatbotApiClient.cs](../Assets/_Backend/Chatbot/ChatbotApiClient.cs) | 303 |
| [Assets/_Backend/Chatbot/ChatbotContextAdapter.cs](../Assets/_Backend/Chatbot/ChatbotContextAdapter.cs) | 201 |
| [Assets/_Backend/Chatbot/ChatbotContextJson.cs](../Assets/_Backend/Chatbot/ChatbotContextJson.cs) | 251 |
| [Assets/_Backend/Chatbot/ChatbotContracts.cs](../Assets/_Backend/Chatbot/ChatbotContracts.cs) | 119 |
| [Assets/_Backend/Chatbot/ChatbotSseParser.cs](../Assets/_Backend/Chatbot/ChatbotSseParser.cs) | 123 |
| [Assets/_Backend/Chatbot/Tests/EditMode/ChatbotApiContractTests.cs](../Assets/_Backend/Chatbot/Tests/EditMode/ChatbotApiContractTests.cs) | 76 |
| [Assets/_Backend/Chatbot/Tests/EditMode/ChatbotContextAdapterTests.cs](../Assets/_Backend/Chatbot/Tests/EditMode/ChatbotContextAdapterTests.cs) | 139 |
| [Assets/_Backend/Chatbot/Tests/EditMode/ChatbotContextJsonTests.cs](../Assets/_Backend/Chatbot/Tests/EditMode/ChatbotContextJsonTests.cs) | 64 |
| [Assets/_Backend/Chatbot/Tests/EditMode/ChatbotSseParserTests.cs](../Assets/_Backend/Chatbot/Tests/EditMode/ChatbotSseParserTests.cs) | 47 |
| [Assets/_Backend/ChatbotIntegration/FirebaseChatbotContextAdapter.cs](../Assets/_Backend/ChatbotIntegration/FirebaseChatbotContextAdapter.cs) | 88 |
| [Assets/_Backend/Childrent.cs](../Assets/_Backend/Childrent.cs) | 211 |
| [Assets/_Backend/DisplayMission.cs](../Assets/_Backend/DisplayMission.cs) | 284 |
| [Assets/_Backend/Features/TextToSpeech.cs](../Assets/_Backend/Features/TextToSpeech.cs) | 167 |
| [Assets/_Backend/FirebaseManager.cs](../Assets/_Backend/FirebaseManager.cs) | 703 |
| `Assets/_Backend/FirebaseTester.cs` (đã xóa) | 61 |
| [Assets/_Backend/ForgetPassword.cs](../Assets/_Backend/ForgetPassword.cs) | 79 |
| [Assets/_Backend/GoogleLogin.cs](../Assets/_Backend/GoogleLogin.cs) | 71 |
| [Assets/_Backend/HabitLog.cs](../Assets/_Backend/HabitLog.cs) | 110 |
| [Assets/_Backend/Login.cs](../Assets/_Backend/Login.cs) | 66 |
| [Assets/_Backend/Logout.cs](../Assets/_Backend/Logout.cs) | 28 |
| [Assets/_Backend/Mission.cs](../Assets/_Backend/Mission.cs) | 194 |
| [Assets/_Backend/Pin.cs](../Assets/_Backend/Pin.cs) | 166 |
| [Assets/_Backend/SceneManage.cs](../Assets/_Backend/SceneManage.cs) | 10 |
| [Assets/_Backend/Service/ChildService.cs](../Assets/_Backend/Service/ChildService.cs) | 167 |
| [Assets/_Backend/SignUp.cs](../Assets/_Backend/SignUp.cs) | 124 |
| [Assets/_Mapping/HabimonModel.cs](../Assets/_Mapping/HabimonModel.cs) | 15 |
| [Assets/_Mapping/Mapping.cs](../Assets/_Mapping/Mapping.cs) | 73 |
| [Assets/_UI/AddChild.cs](../Assets/_UI/AddChild.cs) | 155 |
| [Assets/_UI/AddHabitPopup.cs](../Assets/_UI/AddHabitPopup.cs) | 119 |
| [Assets/_UI/HabitLogUI.cs](../Assets/_UI/HabitLogUI.cs) | 421 |
| [Assets/_UI/MissionUI.cs](../Assets/_UI/MissionUI.cs) | 126 |
| [Assets/_UI/UpdateChild.cs](../Assets/_UI/UpdateChild.cs) | 245 |
