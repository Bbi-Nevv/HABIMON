# HABIMON — Kế hoạch hành động Firebase, dữ liệu và client

> **For agentic workers:** Khi được yêu cầu triển khai, dùng `superpowers:executing-plans` để thực hiện từng task và kiểm chứng trước khi chuyển tiếp. Các checkbox dưới đây là công việc dự kiến, không phải bằng chứng đã hoàn tất. Việc ghi kế hoạch này không cấp quyền deploy, đổi billing hoặc sửa/xóa production data.

**Goal:** Đóng có bằng chứng F25, F24, F02/E10, F03, F10, F14/F15/E08; sửa độc lập E03/E11 và khôi phục Firebase native runtime thuộc F31.

**Architecture:** Giữ Unity/Firebase Auth/Firestore hiện có. Client sở hữu UX và scope; quyền phụ huynh, xác thực PIN, privileged writes và migration thuộc phía tin cậy. Tách policy/rollout, migration dữ liệu và client fixes thành các deliverable riêng, không viết lại app hoặc tự thêm Chatbot UI.

**Tech Stack:** Unity `6000.3.11f1`, Firebase Unity SDK `13.15.0`, Firestore Standard `(default)`, Firebase CLI `15.34.0` và plugin Firebase. Backend codebase đã chọn là repo `HABIMON-BE` (FastAPI + Neon PostgreSQL); module PIN đang ở nhánh local `codex/simple-chatbot-implementation`. Read-only Azure CLI inventory ngày 24/10 chỉ thấy một Web App course-demo, không có Function App/Container App; production host/database/secret store chưa được chọn.

**Spec:** Phạm vi người dùng thống nhất trong cuộc trao đổi ngày 22/10/2026; [thiết kế R01–R17 và điều kiện đóng](CODEBASE_REMEDIATION_PLAN_2026-10-18.md), [audit](CODEBASE_AUDIT_FINDINGS_2026-10-18.md), [Firestore setup](FIRESTORE_SETUP_2026-10-18.md). Tài liệu này chi tiết hóa thứ tự hành động và bổ sung inventory production mới; không thay ID findings.

## 1. Quy ước và ranh giới

### Global Constraints

- Project thực: **`habimon-808e2`**, project number `350279351530`; database **`(default)`**, Standard, **`asia-southeast1`**. Emulator cũng dùng đúng project ID, nhưng mọi test write chỉ tới `127.0.0.1`.
- Baseline source: branch **`Nam`**, HEAD **`486765987db5726873185235fb1a6c398f6b98c8`**, có nhiều thay đổi tracked/untracked. Commit SHA một mình không đại diện toàn bộ worktree; ghi thêm manifest/hash các file được test.
- Tài liệu/kế hoạch tự nó không cấp quyền deploy API, enable service, đổi IAM/billing hoặc ghi dữ liệu production. Hành động cụ thể chỉ theo yêu cầu user đã nêu trong phiên; mọi migration/deploy còn lại phải dừng ở artifact reviewable trước khi thay đổi remote.
- Không log/export vào Git tên/email trẻ/phụ huynh, PIN/hash/salt, token, credential hoặc record dump. Báo cáo chung chỉ chứa aggregate counts; manifest vận hành chứa paths phải được lưu riêng, giới hạn truy cập.
- Mọi bước production phải có phạm vi record, dry-run, điều kiện cạnh tranh, readback và đường dừng cụ thể. Không gọi admin write thành công là bằng chứng mobile authorization đúng.
- Giữ nguyên hash daily ID: `daily_` + SHA-256 UTF-8 của `habitId + "\n" + dateKey`, hex thường. Không đổi delimiter/normalization/casing làm sinh namespace khác.
- Không dùng lịch hiện tại, document createTime, age hiện tại hoặc approved=true để bịa lịch sử, ngày bắt đầu, ngày khai tuổi hoặc thưởng chưa từng cấp.
- Không tự hồi sinh dữ liệu đã xóa, reset balances, tạo sample habit/reward hàng loạt hoặc đổi PIN về một mã chung khác.
- Android/device/hosted CI/Chatbot host chưa được kiểm chứng. F27/E13 vẫn là defer lịch sử, không được kế hoạch này tự mở rộng thành công việc UI/chat deployment.

### Review Focus

1. **Write commit nhưng response/refresh mất:** retry không tạo record thứ hai hoặc ghi scope mới; test ở E11, F10 và provisioning.
2. **Đổi A→B→A hoặc logout→login cùng UID:** equality của UID/child chưa đủ, generation và ownership operation phải được kiểm tra; test ở E11 và API elevation.
3. **Production thay đổi sau inventory:** updateTime mismatch, target mới xuất hiện hoặc child tái tạo phải dừng/reclassify; test ở migration, F03/F10.
4. **Rules allow chồng lấp và quyền Admin:** broad wildcard không được vô hiệu hóa policy chặt; Admin SDK không thay business auth tests; test ở F25.
5. **Thiếu provenance/receipt:** không suy zero/default/approved thành bằng chứng có thể reseed/cộng thưởng; test ở F10/provisioning và phân loại unknown.

### Trạng thái hành động

| Nhãn | Ý nghĩa |
|---|---|
| Có thể chuẩn bị local | Sửa source/tests, tạo dry-run/fixture, chưa chạm production |
| Cần quyết định vận hành | Có thể thao tác kỹ thuật nhưng thiếu policy, compatibility hoặc phạm vi dữ liệu được phép sửa |
| Bị chặn môi trường | Thiếu binary/toolchain/API/hosting/credential cụ thể; ghi rõ blocker, không giả lập thành pass |
| Đã đóng finding | Đạt điều kiện đóng tương ứng, gồm deployed/data/runtime evidence nếu finding yêu cầu |

## 2. Bằng chứng đầu vào — kiểm chứng ngày 22/10/2026

### 2.1. Inventory có phạm vi

| Đối tượng | Kết quả |
|---|---|
| Parent | 33 document tồn tại; không thấy missing parent ancestor trong traversal |
| Child | 51 document tồn tại + 3 missing child ancestors; tổng 54 mục được duyệt |
| Domain documents | 81 habits + 64 logs + 34 pets + 32 rewards = 211 |
| Credentials cũ | 29 parent có passwordHash/passwordSalt; 15 cặp có giá trị, 14 cặp rỗng; 4 parent không có hai field |
| PIN | 29 giá trị khớp SHA-256 của PIN mặc định 0000; 1 SHA-256 khác; 2 giá trị không đúng định dạng SHA-256; 1 thiếu/null |
| Provisioning | 33/33 parent thiếu marker; 19/51 child thiếu habimon/status, thuộc 16 parent |
| Child thiếu pet | 19 child này có gold=0, totalExp=0 trong projection; không chứng minh chưa từng có pet |
| Optional data | 7 existing child không habits, 21 không rewards; chưa kết luận đây là lỗi seed |
| Orphan subtree | 3 missing child profiles còn 20 descendants: 8 habits, 8 logs, 2 pets, 2 rewards |
| Habit provenance | 81/81 thiếu effectiveFrom, createdAt nghiệp vụ và typed schedule; timeTarget có 26 giờ, 54 nhãn buổi, 1 unclassified theo phân loại tĩnh |
| Daily logs | 64/64 ID legacy; 1 nhóm trùng gồm 2 records xung đột; 1 log thiếu dateKey; 9 logs dưới child còn tồn tại tham chiếu habit không thấy trong inventory |
| History consistency | 2 completed logs lệch repeatDays hiện tại; chưa chứng minh sai lịch tại thời điểm ghi |
| Reward provenance | 58 completed, 8 approved; không log nào có rewardGranted field hoặc đủ gold/EXP snapshots |
| Legacy timestamp | 5 incomplete logs còn completedAt; 51 existing child thiếu ageAsOf |

Đã duyệt pagination root users/children, collection IDs dưới từng parent/child và dưới 211 domain documents; không lỗi/pagination bỏ dở, không tìm thấy collection sâu hơn tại lần đọc. Counts habits/logs/pets/rewards **gồm cả dữ liệu dưới missing children**; các nhóm có thể giao nhau, không cộng chúng thành số entity độc lập. Reads không dùng shared readTime nên **không phải atomic snapshot**. Đây là baseline để chuẩn bị, không phải manifest được phép chạy migration sau nhiều giờ/ngày.

Nguồn phiên làm việc: Firebase plugin field-masked reads, `showMissing=true`, IAM/Auth/service metadata bằng CLI credentials. Artifacts aggregate tại `/tmp/HABIMON-firebase-audit-20261022/`; chỉ lưu tạm, không có values của verifier/PIN/PII. Paths record cụ thể chưa được lưu thành manifest bảo vệ để chạy migration: phải tạo lại ở Task A0.

### 2.2. Quyền, cấu hình và dịch vụ

- IAM probe có `datastore.entities.get/list/create/update/delete`, `firebaserules.rulesets.create/releases.update`, Auth config get/update, backup schedule create/list, database update, service enable và Functions/Run create/list.
- Probe **không trả** `datastore.databases.export` hoặc `resourcemanager.projects.setIamPolicy`. Có API create permission chưa chứng minh đủ service-account actAs, secret access, billing hoặc quyền deploy trọn luồng.
- Baseline inventory đầu ngày thấy Rules public; sau đó đã deploy containment owner-scope và policy không cho tái tạo `passwordHash/passwordSalt`. Readback active Rules hiện không còn broad public allow; policy vẫn không phân biệt Parent/Child hoặc enforce domain operations. Chi tiết post-action ở §2.4.
- Không thấy backup/backup schedule; PITR tắt; database delete protection tắt; version retention 3600s. Retention 1 giờ không thay backup được kiểm chứng.
- Firebase environment báo billing chưa bật. Cloud Functions v1/v2 và Cloud Run list trả `403 SERVICE_DISABLED`. Không suy backend tại project/dịch vụ khác không tồn tại.
- Android/iOS apps ACTIVE, app/project/package/bundle IDs khớp config local; Google provider bật; Android có certificate SHA-1/SHA-256. Chưa chứng minh certificate khớp signing key của APK thực tế.

### 2.3. Hành động production/local đã chạy sau inventory — 22/10/2026

- **F25 containment:** Rules được deploy lên `habimon-808e2`, sau đó đọc lại active source/release. Release hiện hành là `3e9f7ec6-4218-45a7-8f1b-154f609327b1`; trước đó đã deploy `ebe182b6-66d0-42b0-bba4-5acd731036a3`. Policy kiểm tra UID owner, giới hạn nested subtree, chặn tái tạo `passwordHash/passwordSalt`, và không còn `allow read, write: if true`. Rules Emulator tests hiện 9/9 pass. **F25 vẫn partial**: owner UID không phân biệt Parent/Child cùng UID, schema/domain writes chưa được bảo vệ đầy đủ, client compatibility/end-to-end chưa chứng minh.
- **F24 legacy field retirement:** đã xóa đúng `passwordHash` và `passwordSalt` trên 29 parent đã phân loại (15 nonempty pairs, 14 empty pairs), dùng update mask chỉ chứa hai fields và precondition updateTime cho từng document. Readback toàn scope: 33 parent, 0 `passwordHash`, 0 `passwordSalt`. Không đổi `pincode`, Auth password hoặc sibling data. **F24 vẫn partial** cho đến khi writer ngoài checkout/client cũ và retention ngoài Firestore được xử lý/đánh giá.
- **B3 local backend:** codebase hiện có là `/home/dodaonam/Projects/HABIMON-BE`, branch `codex/simple-chatbot-implementation`. Có Firebase ID-token verifier + Admin SDK revoked/disabled-user check, password/Google reauth challenge, PIN scrypt verifier, Neon rate-limit/elevation state và migration `0003_parent_security`. Pytest 45/45 pass; Admin Auth status calls được mock, không chứng minh ADC/IAM/live Auth. Migration chưa chạy trên Neon và API chưa deploy/tích hợp Unity.
- **Azure check 24/10:** `az webapp list` chỉ thấy `habimon-chat-demo-261017-0ed006` ở Malaysia West; không thấy Function App hoặc Container App. App Settings names hiện có là `DATABASE_URL`, `OPENAI_API_KEY`, `OPENAI_MODEL`, `SCM_DO_BUILD_DURING_DEPLOYMENT`; không có Firebase/ADC/PIN settings. Đây là app course-demo, không dùng làm production target nếu chưa được owner chọn rõ.

Các release IDs, số đếm và readbacks ở trên phân biệt trạng thái sau hành động với inventory baseline §2.1–2.2. Không lưu field values, PIN, token hoặc record paths ở tài liệu này.

### 2.4. Baseline local tests

| Check 22/10 | Actual | Giới hạn |
|---|---|---|
| C# build | Pass, 0 warning/error | Không phải APK/native initialization |
| EditMode | 55/55 pass | Chưa có regression mới E03/E11 |
| PlayMode | 9 pass, 1 explicit skip | Không đếm skipped case là pass |
| Rules Emulator | 6/6 pass | Chỉ prototype owner scoping |
| Auth/Firestore integration riêng | Failed 1/1 trước nghiệp vụ | Native `.so` là LFS pointer, invalid ELF header |

Không thay số lượng expected bằng cách bỏ test. Khi thêm test mới, báo số lượng thực được discovery; không giữ cứng 55/9/6 làm tiêu chí duy nhất. Artifact local cũ là bằng chứng lịch sử, không chứng minh source sau sửa vẫn pass.

## 3. Thứ tự thực hiện và dependency

| Task | Findings | Có thể bắt đầu | Phụ thuộc trước production |
|---|---|---|---|
| A0 — Freeze evidence, re-inventory | Tất cả migration | Ngay, read-only | Quyết định phạm vi/retention của từng đợt |
| A1 — Native LFS/runtime | F31 hỗ trợ F30 | Ngay nếu lấy được LFS objects | Không deploy Firebase; platform build proof là gate riêng |
| A2 — SSE frame reset | E03 | Độc lập ngay | Không phụ thuộc DB/server |
| A3 — Pending operation scope | E11 | Độc lập ngay | A1 cho integration Firebase; fake-based regression có thể chạy trước |
| B1 — F25 policy và containment | F25, nền F01/F02/E10 | Containment đã deploy/readback; final policy tiếp tục local | Client compatibility, policy vận hành, API/domain authorization |
| B2 — Credential field retirement | F24 | 29 parent legacy fields đã xóa/readback | Chặn mọi writer ngoài checkout; Auth/client compatibility và remote retention |
| B3 — Parent/PIN authority | F02/E10, nền F01 | Code/tests local trong `HABIMON-BE` | Chọn production host/secret store, cấu hình, migration và client integration |
| C1 — Orphan classification/cleanup | F03 | Inventory/dry-run ngay | B1/B3 cho workflow app; xác nhận restore/erase/retention |
| C2 — Daily-log reconciliation | F10 | Classifier/tests ngay | Identity/conflict/reward policy, compatibility writer cũ, race protection |
| C3 — Provisioning classification/repair | F14/F15/E08 | Classifier/tests ngay | Seed policy, identity/Auth checks, chặn client race/reseed |

**Ưu tiên rủi ro không phải thứ tự commit tuyến tính:** public Rules/default PIN/verifiers cần được quyết định sớm; không chờ toàn bộ C1–C3 mới xử lý truy cập public. A1/A2/A3 và thiết kế/tests B1–B3 có thể tiến hành độc lập về mặt kỹ thuật. Tài liệu này không yêu cầu chạy nhiều agent hoặc chỉnh các file chung đồng thời.

## 4. A0 — Evidence, migration contract và gate vận hành

**File hiện có để đọc:** `.firebaserc`, `firebase.json`, `firestore.rules`, các test suites, `Assets/_Mapping/Mapping.cs`, `Assets/_Backend/FirebaseManager.cs`.

**File đề xuất mới khi triển khai:** `tools/firebase-remediation/README.md`, `inventory.mjs`, `classify.mjs`, `apply.mjs`, `verify.mjs`; tests tại `tests/firebase-remediation/`. Runtime Node cho tooling, dùng Firebase Admin/Firestore SDK hoặc authenticated REST; phải pin dependency trong manifest/lockfile sau khi chọn thư viện, không coi tools này đã tồn tại.

### Hợp đồng tooling đề xuất

- `inventory({projectId, databaseId, readTime?, scope}) -> InventoryResult`: read-only, field projections, pagination đầy đủ và counters completeness.
- `classify(inventory, policyVersion) -> MigrationPlan`: pure, không network write, tách proposed/noop/conflict/blocked.
- `apply(plan, {expectedProjectId, runId, allowedActions}) -> ApplyReport`: chỉ tác động action đã có trong plan, có precondition; không tự mở rộng selector.
- `verify(plan, applyReport) -> VerificationReport`: đọc lại outcomes và invariants của siblings/ngoài scope; không chỉ kiểm tra exit code.
- Production mặc định dry-run; `--apply` phải yêu cầu project/database/run/manifest digest khớp và không được nhận query selector rộng thay cho manifest đã rà soát. Đây là thiết kế CLI mới, chưa có lệnh thực thi trong repo.

Mỗi action trong manifest bảo vệ phải có: task/finding ID, policyVersion, source path, expected updateTime/existence, action type, allowed fields, expected postcondition, conflict reason và outcome. Scope có record paths không đưa vào Git/chat/log công khai. Không đưa password/PIN values vào manifest; với payload không nhạy cảm chỉ giữ dữ liệu tối thiểu để kiểm chứng hoặc digest. Digest không biến đường dẫn cá nhân thành dữ liệu có thể công khai.

- [ ] Xác nhận lại account/project/database bằng Firebase environment và IAM permission probe, không in token.
- [ ] Ghi HEAD, dirty/untracked inventory và hashes file implementation/test liên quan; giữ font/scene/config edits của người dùng.
- [ ] Re-inventory có pagination; dùng common readTime nếu API hỗ trợ và còn trong retention. Nếu không, ghi rõ interval, rồi revalidate từng record trước apply.
- [ ] So các count với §2; drift là lý do regenerate plan, không ghi đè theo số cũ.
- [ ] Tạo fixture Emulator tổng hợp theo cấu trúc/problem classes, không copy credentials/PII production vào fixtures.
- [ ] Chạy dry-run hai lần trên cùng fixture: cùng classification/action IDs, không có writes; assert unknown/conflicts không bị auto-apply.
- [ ] Test apply: updateTime mismatch, source missing/recreated, destination tồn tại, interruption/restart, permission denied và scope drift; mỗi case phải có outcome riêng.
- [ ] Chốt recovery/retention cho từng task trước production. Chưa có managed export permission/backup là gate cụ thể, không viết “đã backup”. Có thể chọn secure export được cấp quyền cho noncredential records; với credential retirement chọn irreversible deletion có reauth recovery, không lưu lại verifier để rollback.

**Dừng đợt:** project sai; pagination/error chưa xử lý; manifest/policy digest lệch; record thay đổi; có unknown schema; target vượt scope; thiếu quyền/recovery đã yêu cầu. `conflict` không tự đổi thành success qua retry vô hạn. Resume chỉ chạy action chưa hoàn tất và revalidate preconditions; kết quả đã đạt phải thành noop, không replay side effects.

## 5. A1 — Khôi phục native Firebase / F31

**Hiện trạng ban đầu 22/10:** 8 `.so`/`.bundle` là Git LFS pointers; App `.so` dài 133 byte, expected object size 80.228.616 byte. `.gitattributes` đã có LFS patterns. **Cập nhật 24/10:** cài Git LFS 3.4.1 vào user-local `~/.local/bin`, tải và hydrate đủ 8 objects được plugin metadata tham chiếu; `git lfs fsck --objects` pass. Bốn `.so` là ELF x86-64, bốn `.bundle` là Mach-O universal. Các files vẫn clean theo LFS filter.

**Chạm tới:** `.gitattributes`, các files hiện có dưới `Assets/Firebase/Plugins/x86_64/`; không sửa C#/vendor manifests để che lỗi. Đây là dependency restoration, không phải đổi version SDK.

- [x] Lưu status, xác nhận LFS remote và hydrate đủ 8 pointers được project tham chiếu; không checkout/reset worktree.
- [x] Đối chiếu LFS objects qua `git lfs fsck --objects`; Linux `.so` là ELF x86-64, `.bundle` là Mach-O universal.
- [x] `dotnet build Assembly-CSharp.csproj -v:minimal`: thành công, 0 warning/0 error.
- [x] Unity EditMode 63/63, Parent popup/chart PlayMode 9/9, Firestore Rules Emulator 9/9, Auth/Firestore Emulator integration 1/1 pass.
- [ ] Tái chạy từ clean checkout có các sửa đã được đưa vào checkout theo quy trình được phép, kiểm tra resolver/target build khi toolchain sẵn sàng. Bản copy giữ Library/GeneratedLocalRepo không đạt clean-clone criterion.

**Commands hiện có, chỉ chạy khi triển khai được yêu cầu:**

```bash
# Chỉ đọc; không in credential/config values.
git lfs version
git lfs ls-files
file Assets/Firebase/Plugins/x86_64/FirebaseCppApp-13_15_0.so

dotnet build Assembly-CSharp.csproj --no-restore -v:minimal
# UNITY_EDITOR phải trỏ executable Unity 6000.3.11f1 trong môi trường chạy.
npm run test:firestore-rules
npm run test:firebase-provisioning
```

`npm run test:firebase-provisioning` dùng `UNITY_EDITOR` và script kiểm tra Auth/Firestore emulator hosts. Không chạy script Unity business test trực tiếp với production config/hosts trống. Chạy các Unity suites tuần tự trên cùng project; không `-quit` trước Test Runner. Restore đúng bytes các file người dùng nếu importer sửa trong workspace, không dùng blanket git restore.

**Đóng blocker runtime local:** native load không còn invalid ELF header và explicit emulator integration vào assertions nghiệp vụ pass. **F31 vẫn partial:** chưa có clean-clone/resolver regeneration hay Android/iOS target build/device evidence; các test trên chạy trong worktree hiện tại, không phải checkout sạch.

## 6. A2 — SSE event type reset / E03

**Modify:** `Assets/_Backend/Chatbot/ChatbotSseParser.cs`, method `DispatchEvent`.

**Test:** `Assets/_Backend/Chatbot/Tests/EditMode/ChatbotSseParserTests.cs`.

**Interface giữ nguyên:** `Feed(byte[] bytes, int count)`, `Complete()`, `EventReceived` và caps/strict UTF-8/strict EOF đã có; không sửa API client hoặc backend contract để hợp thức hóa lỗi parser.

- [x] Regression `EventOnlyFrameDoesNotLeakEventTypeToNextDataFrame`: pre-fix fail, sau fix pass; LF/CRLF/CR được feed từng byte, assert chỉ có một event `message` với payload giữ nguyên.
- [x] Coverage có frame CR/CRLF và split separator, event-only frame, empty data, strict EOF và named frame sau đó; suite hiện hành được chạy.
- [x] Sửa tối thiểu trong `DispatchEvent`: frame không data clear `eventName` trước return; không đổi decoder/API.
- [x] Target regression pass 3/3 và Chatbot EditMode suite pass 29/29 trong isolated Unity test copy ngày 22/10.

**Đóng E03 local:** regression không phụ thuộc cách chia bytes, parser suite pass, incomplete EOF không dispatch. Không cần Firebase/cloud/device để đóng parser finding. Kết quả là từ isolated test copy; chưa coi đây là Android/Firebase integration proof.

## 7. A3 — Pending create/retry giữ scope / E11

**Modify:** `Assets/_UI/AddHabitPopup.cs`, `Assets/_UI/AddChild.cs`, `Assets/_Backend/Mission.cs`, `Assets/_Backend/FirebaseManager.cs`; kiểm tra `ChildService.AddChildAsync` và `AddHabitAsync` operation semantics.

**Test hiện có cần mở rộng:** `Assets/_Backend/Core/Tests/PlayMode/PopupAndChartPlayModeTests.cs`, `FirebaseProvisioningEmulatorTests.cs`. Pure scope tests có thể thêm vào Core EditMode; không dùng fake test thay cho runtime write assertion.

**Interface triển khai local 24/10:** `HABIMON.Core.PendingHabitCreate` và `PendingChildCreate` giữ operationId, `FirebaseWriteScope` (parentId, childId, sessionGeneration) và payload get-only. Không có generic command framework.

- [x] Habit submit truyền **captured scope** tới `Mission.SubmitHabitFromPopup`; retry không resolve current child để đổi target.
- [x] Child create truyền **parent/generation** tới manager vì Firestore path nằm dưới parent, không dưới child đang chọn; child ID là operationId. Khi parent khác thì chặn. Explicit retry cùng parent có thể cập nhật generation dù child selection đổi, nhưng giữ nguyên operationId/payload.
- [x] Service boundary xác nhận captured scope trước write; UI xác nhận lifecycle sau await. Scope đổi không rollback write đã commit.
- [x] Retry dùng cùng operation ID/payload và transaction create-if-absent. Document đã tồn tại với payload khớp là idempotent success; ID trùng với payload khác bị từ chối, không ghi đè bằng `MergeAll`.
- Parent khác luôn bị chặn; habit target còn yêu cầu đúng child. Explicit retry tạo generation scope mới trong target hợp lệ nhưng không đổi payload/operationId; không có retry tự động sang target khác.

- [ ] Test write A commit + refresh fail → chọn B → retry: không có document mới ở B, A chỉ có một record.
- [ ] Test logout A → login B; logout A → login lại A; A→B→A: operation cũ không được tái hợp lệ chỉ vì ID trùng.
- [ ] Test AddChild giữ owner; đổi account giữa pending/retry không tạo child ở account mới.
- [ ] Test response lost, double click, cancel in flight, popup destroy, conflicting payload cùng ID và write success/refresh failure. Assert server count/scope và UI outcome riêng.
- [x] Sửa source trong `AddHabitPopup.cs`, `AddChild.cs`, `Mission.cs`, `FirebaseManager.cs`, `ChildService.cs` và Core DTO; không sửa scene/prefab; giữ single-flight/finally behavior.
- [x] Chạy Core EditMode scope tests, popup PlayMode suite và Auth/Firestore Emulator retry/conflict assertions; không chỉ reflection kiểm tra field operationId.

**Bằng chứng 24/10:** Unity EditMode 63/63 pass; Parent popup/chart PlayMode 9/9 pass; Auth/Firestore Emulator integration 1/1 pass. Emulator xác nhận matching retry, conflicting child/habit payload, selected-child switch, A→B→A không tự hồi sinh generation và explicit retry cùng target. **Còn thiếu:** A đổi Auth account sang B, write-commit/refresh-fail rồi đổi account, transport response thật bị mất, cancel khi write đang chạy, popup GameObject bị destroy và phục hồi sau process restart. E11 vẫn partial.

**Đóng E11:** retry/cancel/refresh có kết quả rõ, không duplicate, không overwrite payload đầu và không đổi owner/child qua mọi case trên. Không cần chờ parent authorization redesign để sửa client bug; E11 pass không đóng F01/F25.

## 8. B1 — F25: containment, final authorization và rollout

**Modify hiện có:** `firestore.rules`, `firestore.indexes.json` nếu query thực yêu cầu, `tests/firestore-rules/firestore.rules.test.mjs`, `.github/workflows/firestore-rules.yml`. Không thêm index vì đoán trước; chưa có composite index không tự là bug.

**Phạm vi:** có thể giảm public exposure trước khi toàn bộ server hoàn tất, nhưng containment chỉ là giảm rủi ro, không đóng F25/F01/F02.

### 8.1. Hai mốc policy riêng

| Mốc | Deliverable | Không được kết luận |
|---|---|---|
| Containment có kiểm soát | Chặn anonymous/cross-owner/out-of-scope; xác định các đường privileged cần tạm khóa hoặc bắt nâng cấp client; kiểm thử client compatibility | Owner check không phân biệt parent/child; không gọi đây là final authorization |
| Final policy | Explicit paths/field validators + trusted parent/business authority; deny direct privileged writes và private backend paths | Admin SDK bypass Rules nên còn phải kiểm thử API auth/ownership/elevation riêng |

Containment không phải copy nguyên prototype rồi deploy. Trước rollout lập bảng các request thật của login/signup, parent/child list, PIN, habit/log/reward/pet và deletion. Mỗi request phải có expected allow/deny cho client cũ/mới. Nếu chưa có cách vừa giữ privileged client flow vừa bảo đảm authority thì chọn **maintenance/read-only/minimum-client-version** có chủ đích cho phần đó; không giữ public Rules để tránh lỗi UI. Quyết định ảnh hưởng người dùng phải được ghi trước deployment.

### 8.2. Bảng quyền mục tiêu (thiết kế đề xuất)

| Path/data | Client sau chuyển đổi | Trusted backend |
|---|---|---|
| `users/{uid}` public profile tối thiểu | Owner read; create/update theo exact allowlist. Không credential/elevation/role tự khai | Provisioning/controlled profile migration theo UID xác thực |
| `children/{childId}` | Owner read; privileged create/update/delete qua API có elevation | Kiểm ownership, pending-delete, allowed fields và idempotency |
| `habits/{habitId}` | Owner read; parent management qua privileged API | Validate schedule/effective policy, archive/history |
| `habit_logs/{logId}` | Owner read; completion endpoint theo state machine, không tự set approval/reward | Kiểm scheduled/active/date/scope và cấp thưởng theo authority thiết kế |
| `habimon/status`, rewards/balances | Read theo owner; client không tự cộng gold/EXP/level/unlock | Transaction domain service; migrate không tự phát reward |
| `parent_security/{uid}`, elevation state, deletion jobs, migration control | Deny mọi mobile/web read/write | IAM-restricted service, explicit API authorization |

Các collection backend phía trên là **đề xuất mới**, không nằm trong schema deployed đã kiểm kê. Không dùng client-writeable role hoặc permanent parent claim trên cùng UID trẻ đang giữ. Không truyền elevation token qua model context/log/PlayerPrefs.

Firestore đọc cả document, không dùng Rules để che một vài field verifier trong document cho cùng request đọc. Cần chuyển verifier sang private storage hoặc loại bỏ trước khi cho client đọc profile đó. Policy không được cho credential mới quay lại qua `set/merge`, nested map hoặc unknown fields. Với legacy fields đang tồn tại, thiết kế transitional validation bằng diff/affectedKeys và task migration rõ, không hạ hasOnly để giữ mọi field cũ vô hạn.

**Overlapping matches:** bỏ broad recursive allow trước khi mong một match con deny có tác dụng. Trong Firestore, một matching allow=true là đủ cho phép; thêm deny ở match khác không ghi đè allow rộng. Kiểm tra riêng `users/{uid}` và mọi backend collection ngoài subtree.

### 8.3. Hành động và kiểm chứng

- [ ] Lưu deployed Rules source/release metadata không có secrets; ghi checksum; không đánh đồng file local với active release.
- [ ] Viết negative tests trước: anonymous, UID A→B, unknown root path, parent thiếu, child missing/pending delete, same UID thiếu elevation, injected fields/types/oversize arrays/strings, direct PIN/approval/reward/balance writes.
- [ ] Với final design, kiểm thử API expired/revoked elevation, forged parentId, child khác owner, plan/role giả và retries. Token-valid không đồng nghĩa parent-authorized.
- [ ] Test positive signup/provisioning/login/read/list/query và legitimate mutations qua đường mới; dùng fixtures legacy thực hình dạng gồm fields rỗng/markerless, không dùng credential values thật.
- [ ] Rà soát query compatibility: Rules không phải filter; list/query phải phù hợp policy. Validate limits dựa contract/schema thực, không đặt arbitrary caps gây chặn dữ liệu hợp lệ.
- [ ] Chạy `npm run test:firestore-rules`, Auth/Firestore Emulator integration sau A1 và API tests ở backend đã chọn. Existing 6 tests chưa đủ cho bảng quyền mới.
- [ ] Chuẩn bị manifest deployment: old/new policy digest, compatibility result, client version policy, probe accounts/test cases và monitoring/rollback target là phiên policy an toàn.
- [ ] Chỉ khi có yêu cầu thực thi production: deploy đúng project, đọc lại active Rules, chạy negative/positive access probes bằng Firebase **client** identities. Không dùng Admin read pass để xác nhận Rules deny/allow đúng; không thêm test data vào tài khoản người dùng.

**Rollback:** nếu client mới lỗi, dùng maintenance/read-only hoặc policy an toàn đã review; không quay lại `allow true`. Dừng privileged writes nếu authority service chưa sẵn sàng. Rules rollback không khôi phục dữ liệu đã xóa.

**Đóng F25:** active policy digest đúng, không public/cross-owner, schema/domain/API authorization có tests, client flows đã chạy, không còn broad allow bypass hoặc bằng chứng chỉ từ Admin. Containment chỉ đạt một phần; F01/F02/E10 có DoD riêng ở B3.

## 9. B2 — F24: loại bỏ password verifier legacy

**Source consumer kiểm tra:** `Assets/_Backend/FirebaseManager.cs`, `Assets/_Mapping/Mapping.cs`, Auth UI; kiểm tra cả client versions/deployments ngoài worktree khi có nguồn. Source mới đã không dùng custom passwordHash/passwordSalt; chưa chứng minh mọi client đang chạy đều mới.

**Tooling đề xuất:** task-specific handler `retirePasswordVerifiers` trong `tools/firebase-remediation/` theo contract A0; fixture/tests ở `tests/firebase-remediation/`. Không cần xây server PIN để viết dry-run này.

### Phạm vi và thuật toán

1. Read-only re-inventory **presence** hai fields và updateTime. Không in hoặc copy giá trị verifier vào logs/manifest/artifacts.
2. Phân loại: cả hai nonempty, cả hai empty, absent (noop), mixed/type lạ (conflict cần xem). Inventory 22/10 có 15 nonempty + 14 empty pairs, không dùng counts này làm selector.
3. Trước apply, chặn việc ghi verifier trở lại bằng policy/schema mới và client compatibility policy. Nếu client cũ còn ghi được thì cleanup không ổn định; nếu chưa chặn được, ghi rõ task bị chặn, không tuyên bố đóng F24.
4. Mỗi parent action chỉ delete field `passwordHash` và `passwordSalt` bằng partial update có expected updateTime; không SetAsync toàn ParentModel, không xóa user document, không đổi email/pincode/provisioningState/createdAt/children hoặc Firebase Auth password.
5. Không có field → noop. UpdateTime mismatch → conflict; re-read/reclassify, không tự bỏ precondition để retry. Không xóa parent được tạo lại sau inventory chỉ vì cùng path.
6. Readback absence trên cả scope; so allowed-field delta và test đăng nhập Firebase Auth vẫn hoạt động. Monitor attempted reintroduction qua policy tests/client telemetry phù hợp.

- [ ] Regression trên Emulator: nonempty/empty/mixed/absent fields; unrelated fields và children giữ nguyên; updateTime conflict; resume sau timeout; replay thành noop.
- [ ] Test Auth login không phụ thuộc verifier Firestore mới; custom verifier không xuất hiện trong new signup/Google/provisioning writes.
- [ ] Dry-run tạo đúng field-deletion actions, 0 document deletions, 0 Auth password changes và không lộ values.
- [ ] Quyết định **irreversible credential deletion** trước apply. Không backup verifier vào Git/storage dễ đọc chỉ để rollback; recovery bằng Firebase Auth reauthentication/password reset chính thức, không restore hash cũ.
- [ ] Apply theo manifest bảo vệ, kiểm tra lại từng record và readback/re-inventory sau hoàn tất.

**Đóng F24:** không còn verifier fields trong scope hiện hành, không writer được phép tái tạo, Auth behavior pass, audit counts và conflicts được xử lý. Chỉ 15 cặp nonempty không phải lý do bỏ qua 14 cặp rỗng nếu mục tiêu là retire schema. Không thể bảo đảm một bản backup lịch sử bên ngoài đã bị xóa nếu chưa kiểm kê retention của nơi đó; ghi phạm vi bằng chứng rõ.

**Post-action 22/10:** đã xóa đúng hai field trên 29 parent bằng updateTime precondition + updateMask; readback 33 parent cho kết quả `passwordHash=0`, `passwordSalt=0`. Deployed Rules hiện chặn thêm/tái tạo hai fields. F24 còn partial cho đến khi compatibility với mọi writer/client cũ, Auth login và retention/archive ngoài Firestore được kiểm chứng.

## 10. B3 — F02/E10: parent authority, PIN reset và limiter

**Không phải một script đổi PIN.** Inventory ban đầu thấy 29 default hashes và 2 giá trị khác format; không dùng dữ liệu đó làm proof hoặc import vào verifier mới. Hiện 32 parent vẫn có `pincode` field; đổi hash algorithm trong BE không tự thu hồi client-side verifier.

### 10.1. Backend đã chọn cho code; production runtime vẫn chưa chọn

- **Codebase:** `HABIMON-BE`, FastAPI, SQLAlchemy async và Neon PostgreSQL. Đây là backend hiện có của chatbot; Firebase parent-security API dùng Firebase ID token riêng và không thay `require_owner_token` của chatbot.
- **Database:** migration mới `alembic/versions/0003_parent_security.py`. Đã chạy `alembic upgrade head` thành công trên PostgreSQL 17 disposable local; chưa apply lên Neon.
- **Host:** Azure CLI hiện chỉ thấy `habimon-chat-demo-261017-0ed006` (Malaysia West), app course-demo. Không có App Settings `FIREBASE_PROJECT_ID`, `FIREBASE_WEB_API_KEY`, `FIREBASE_GOOGLE_CLIENT_IDS`, `PIN_PEPPER`; không dùng app này làm production target theo suy đoán.
- **Firebase Admin identity:** local code dùng `firebase_admin.auth.verify_id_token(check_revoked=True)` cho revoked/disabled-user status. Runtime cần ADC/workload identity với `firebaseauth.users.get`; role `roles/firebaseauth.viewer` chứa permission đọc Auth users. Khi ADC không dùng hosted identity, `GOOGLE_APPLICATION_CREDENTIALS` phải trỏ tới file credential được secret-mount (không commit). Đây không cấp Firestore writes; domain proxy về sau cần IAM/Rules bypass design riêng.
- **Secret store:** trước runtime thật cần cấu hình `FIREBASE_PROJECT_ID=habimon-808e2`, ADC, Firebase Web API key, allowlist Google client IDs và `PIN_PEPPER` ngẫu nhiên tối thiểu 32 byte. Pepper phải được giữ trong secret store; đổi pepper sẽ khiến verifier cũ không xác minh được nếu chưa có kế hoạch rotation.
- **KDF:** local code dùng scrypt maintained (`N=2^14`, `r=8`, `p=1`, 32-byte output) với salt riêng và HMAC pepper. Chưa benchmark trên runtime Azure/production; đánh giá performance trước release.

Code/test local không đồng nghĩa PIN server đã phục vụ mobile. Chưa có production host, secret values, migration trên Neon, Firebase/Google live test, mobile caller hoặc API bảo vệ các thao tác Firestore hiện tại.

### 10.2. Contract API hiện implement local, chưa deployed

Base path đang được đăng ký trong FastAPI là `/api/v1/parent-security`; routes có trong source nhưng chưa có client production caller.

| Route | Auth/input | Output/action |
|---|---|---|
| `GET /pin/status` | Verified Firebase ID token | Enrollment state và credentialVersion của caller |
| `POST /reauth-challenges` | Token; operation=enroll/reset, method=password/google, session ID | Challenge + nonce, TTL 120s; chưa cấp elevation |
| `POST /reauth-challenges/{id}/verify` | Challenge, session ID, password/email hoặc Google ID token | One-time reauth proof sau khi server verify Firebase UID |
| `POST /pin/enroll` | Token, proof bound `enroll`, session ID, idempotency key, 4-digit PIN | Tạo verifier private và tăng credentialVersion |
| `POST /pin/reset` | Token, proof bound `reset`, session ID, idempotency key, new PIN | Đổi verifier, consume proof, revoke prior elevation |
| `POST /pin/verify` | Token, session ID, PIN | Opaque elevation token TTL 300s hoặc lỗi/rate-limit |
| `POST /elevations/revoke` | Token, session ID, elevation token | Revoke token cùng UID/session |

Full API path là `/api/v1/parent-security/...`. Firebase UID lấy từ token đã verify; routes không nhận owner/parent UID từ body. Chatbot owner-token auth chưa bị thay đổi. Firebase ID token hợp lệ không tự authorize enroll/reset; endpoint phải verify lại password credentials hoặc Google ID token mới.

**Proof boundary đề xuất bắt buộc:** server tạo challenge ngẫu nhiên, bind UID + operation (enroll/reset) + session + credentialVersion + expiry; TTL khởi đầu 120 giây. Server chỉ mint opaque one-time reauth proof sau khi tự xác minh parent secret/provider challenge mới. Proof chỉ giữ trong RAM client, private server state; không chấp nhận cờ `reauthenticated=true`, callback UI hoặc cached/refreshed Firebase token làm proof. Enroll/reset transaction atomically consume proof và rotate verifier/version; replay cho operation khác bị deny, retry cùng idempotency key chỉ trả result đã hoàn tất.

- Email/password local code gọi Identity Toolkit và so `localId` với UID; response ID/refresh token không được lưu/trả. Helper tests dùng HTTP mock; chưa gọi project thật.
- Google local code yêu cầu nonce từ challenge, allowlisted audience, issuer, `auth_time`/`iat` mới trong 5 phút và Identity Toolkit UID mapping với `autoCreate=false`. Mobile chưa chứng minh có thể phát hành credential đủ claims; Google-only production reset vẫn bị chặn tới khi test Unity/platform thật.
- Provider tokens/passwords chỉ được giữ trong request memory cho thao tác reauth; không dùng passwordHash Firestore.

Code hiện đặt challenge/proof TTL 120 giây, Google freshness tối đa 5 phút và elevation TTL 5 phút. Đây là implementation values cần review/benchmark trước release.

Elevation token chỉ trả một lần, raw token không lưu DB; record lưu hash, UID/session/credentialVersion/scope/expiry/revocation. `require_parent_elevation` đã có trong backend nhưng chưa được endpoint nghiệp vụ nào dùng. Unity phải giữ token trong memory, clear khi logout/account switch/background theo policy, không đưa vào model context/PlayerPrefs.

PostgreSQL upsert limiter atomic theo UID/action: challenge create 10/15 phút; reauth verify 5/15 phút theo method; PIN verify 5/15 phút. Valid attempts cũng tính. API hiện chưa có IP/device/edge-ingress rate limit; cấu hình ingress khi chọn production host. Failure response không trả verifier/PIN/token.

**Local verification 24/10:** toàn bộ HABIMON-BE pytest pass 45/45 với PostgreSQL 17 disposable container; tests parent-security kiểm tra enroll/reset, invalid/default PIN, proof replay, retry idempotent/conflict, UID/session binding, persisted PIN attempts, elevation revoke, token refresh giữa challenge/proof, revoked/disabled rejection và fail-closed status lookup. Alembic `upgrade head` đạt `0003_parent_security` trên database local. Firebase/Identity Toolkit/Admin Auth live calls không được thực hiện; status tests mock Admin API. Chi tiết API nằm ở [HABIMON-BE/docs/parent-security-api.md](../../HABIMON-BE/docs/parent-security-api.md).

### 10.3. Legacy reset và client changes

- [ ] Với tất cả nhóm default/nondefault/malformed/missing: không copy SHA-256 legacy vào private KDF store rồi coi đã migrate. New enrollment/reset dựa reauthentication; không gửi mã mới qua log/chat.
- [ ] Đặt trạng thái cần enrollment/reset ở private policy; profile readable chỉ giữ thông tin không nhạy cảm tối thiểu nếu UI cần. Existing pincode được retire sau khi recovery/client rollout sẵn sàng; không xóa trước rồi bỏ người dùng không có đường vào Parent.
- [ ] Cập nhật `FirebaseManager.VerifyParentPinAsync/ChangeParentPinAsync`, `Pin.cs`, `SceneManage.cs` và Menu/Parent/Child bindings liên quan; gate điều hướng lẫn API, không chỉ child→parent popup.
- [ ] Tài khoản shared UID không chứng minh người đang cầm máy là parent. Fresh reauth phải yêu cầu credential/provider challenge thực sự của parent; không tự lấy một cached token cũ để bypass reset.
- [ ] Nếu Google provider cần interactive reauth: kiểm mobile target thật; không dùng Ubuntu provider failure hoặc conditional compile làm proof.
- [ ] Trước khi xóa pincode legacy, B1 chặn tái ghi/đọc sensitive fields theo rollout đã chọn. Firestore không field-redact document reads; cần profile/private-data separation rõ.

### 10.4. Tests/DoD

- [ ] Valid Firebase token nhưng không elevation: parent-only mutations deny; A→B child deny; forged/expired/revoked token deny.
- [ ] 20 concurrent verify requests không vượt limiter; popup close/open, scene reload, app restart hoặc device khác không reset account limiter.
- [ ] Dùng lại shared-session token vừa được parent đăng nhập, không hoàn tất challenge mới → enroll/reset deny. Cached/refreshed token, proof của UID/operation khác, expired/replayed proof đều deny; Google provider response không bind nonce/challenge cũng deny.
- [ ] Fresh reauth challenge sai/mất mạng/cancel không enroll/reset; default PIN 0000 không tự nâng quyền; credential rotation vô hiệu token cũ.
- [ ] Enroll/reset response lost + retry idempotent; replay với payload khác conflict. Server không trả verifier hoặc ghi PIN vào logs.
- [ ] Parent hợp lệ vào đúng flow; child không sửa private docs/approval/balances qua direct SDK hoặc gọi endpoint thiếu quyền.

**Đóng F02/E10:** private verifier, trusted limiter/elevation và recovery chạy end-to-end trên target được phép, legacy reset đã xử lý theo policy, client không còn authoritative PIN comparison. **F01** chỉ đóng thêm khi mọi thao tác parent-only của app được gate đúng; không đóng F01 chỉ vì endpoint PIN pass.

## 11. C1 — F03: phân loại orphan và deletion workflow chống tái phát

**Hiện có:** `Assets/_Backend/Service/ChildService.cs` chỉ batch-delete 4 collection được biết rồi child; `FirebaseManager.DeleteChildAsync`, `Childrent.cs` gọi client path. Không được dùng admin script cleanup một lần để tuyên bố đã thay thế workflow này.

**Đề xuất backend:** deletion service + resumable job trong backend đã chọn ở B3; operational job/tombstone nằm ngoài subtree bị xóa và client không được tự sửa. Tooling C1 dùng contract A0; không đưa thông tin cá nhân vào job payload nếu scope IDs đủ.

### 11.1. Cleanup 3 missing child scopes hiện tại

- [ ] Re-inventory `showMissing=true` và list collection IDs mọi cấp; dữ liệu mới/unknown schema làm plan drift. 20 descendants là snapshot tham chiếu, không hardcode whitelist 20 paths rồi bỏ qua phần mới.
- [ ] Phân loại từng missing child scope: `restore_with_authority`, `erase_confirmed`, `retain_pending_review`. Missing document hoặc balance 0 không đủ chứng minh erasure consent.
- [ ] Chỉ scope erase_confirmed mới tạo delete actions. Các scope còn lại giữ nguyên; nếu không có quyết định thì C1 cleanup dừng ở dry-run, không purge để làm sạch số liệu.
- [ ] Đảm bảo recovery/retention được chấp nhận cho noncredential records trước delete. Không có managed export/backup thì ghi đúng blocker; không tự tạo bản dump PII local rồi gọi đó là backup an toàn.
- [ ] Trước apply, xác nhận child vẫn missing và parent/owner vẫn đúng, descendants updateTime không drift; nếu child đã được tái tạo, dừng scope đó để reclassify.

### 11.2. Workflow mới đề xuất

`POST /children/{childId}/deletion-jobs` nhận verified Firebase ID token + valid parent elevation + idempotency key. UID owner suy từ token; API xác nhận child ownership và allowed scope, không tin parentId body. Response `accepted/pending` + operationId, **không báo erasure complete**. `GET /deletion-jobs/{operationId}` chỉ owner có quyền tương ứng đọc status không chứa PII.

1. Transaction tạo/reuse job ngoài subtree và đánh dấu child `deletionState=pending`; missing-child cleanup dùng server-owned tombstone riêng, không tạo child giả để xóa nó lần nữa.
2. Rules/API chặn writes mới vào scope đang xóa, bao gồm child recreation. Admin writers cũng phải kiểm tombstone/job vì bypass Rules. Khóa này phải còn hiệu lực cho tới khi verify và policy quyết định retire tombstone.
3. Worker duyệt **collection IDs thực**, enumerate documents gồm missing ancestors, đi sâu từng nhánh, checkpoint và delete theo batches hữu hạn. Giữ 400 docs/batch như client cũ có thể là lựa chọn khởi đầu, không giả đây là giới hạn API duy nhất; đo payload/deadline và transaction constraints của runtime.
4. Với record thay đổi ngoài expected lifecycle, dừng/retry theo policy, không silently xóa state mới. Side effects không nằm trong retryable Firestore transaction callback.
5. Verify không còn descendants trong scope khi writes vẫn bị khóa; xóa profile cuối nếu còn, cập nhật job completed. Đọc trực tiếp từng reference/collection cần thiết, không chỉ list children hiện có.
6. Status failed/retryable có checkpoint; retry cùng operation không tạo job khác hoặc xóa sibling. Giữ audit không nhạy cảm theo retention, không lưu bản copy PII vô thời hạn trong job.

- [ ] Emulator/API tests: nested collection chưa có trong 4 collection cũ; missing intermediate documents; failure giữa batch + resume; sibling preservation; duplicate request; no elevation/cross-owner denial.
- [ ] Test write/recreate trong pending bị deny cả mobile và API, worker completion không chạy trước subtree-empty verification; concurrent child recreation làm cleanup manifest conflict.
- [ ] Update client delete để gọi API, hiển thị pending/completed/failure; không giữ fallback direct recursive delete khi API lỗi.
- [ ] Kiểm scope liên quan storage/chat/history bên ngoài Firestore **chỉ khi có bằng chứng liên kết**. Nếu có, bổ sung retention/erasure task; không suy Firebase child delete đã xóa chatbot session ngoài project.

**Đóng F03:** workflow có quyền và recovery đã chạy, cleanup/retain/restore outcome của inventory cũ được ghi đầy đủ, không còn unresolved orphan trong scope được yêu cầu xóa; remote retention và unknown descendants đã kiểm. Một lần xóa 20 documents không đáp ứng phần chống tái phát.

## 12. C2 — F10: daily-log reconciliation và uniqueness

**Modify hiện có:** `HabitLog.cs`, `FirebaseManager.AddHabitLogAsync/UpdateHabitLogAsync/UpsertDailyHabitLogAsync`, `HabitRewardService.cs`, `DisplayMission.cs`, `HabitLogUI.cs` và tests tương ứng. `DailyHabitLogIdentity.cs` là nguồn hash format phải giữ nguyên.

**Tooling đề xuất:** `classifyDailyLogs`/`migrateDailyLogs` trong tooling A0. Client transaction và deterministic ID đã có; phần mới phải xử lý legacy, stale writes và tampering, không chỉ đổi tên document.

### 12.1. Classifier có kết quả hữu hạn

| Nhóm | Hành động mặc định |
|---|---|
| Đủ child/habit/date, duy nhất, schema hợp lệ, target chưa tồn tại | Candidate migration sau khi reward/identity policy được xác nhận |
| Nhiều record cùng key, mọi state/provenance quan trọng giống nhau | Candidate dedup nhưng vẫn phải kiểm receipt/reward history, không chỉ isCompleted |
| State/approval/reward khác nhau | Conflict, giữ nguyên để reconciliation có thẩm quyền |
| Thiếu/invalid dateKey, thiếu habitId, habit không còn tồn tại | Blocked/unknown; không tự lấy createdAt hoặc tên habit thay identity |
| Target deterministic đã có | So identity/state/receipt: đã migrate thì noop; khác thì conflict, không overwrite |
| Scope child đã mất/pending delete | Chuyển C1/policy liên quan; không migrate vào một child mới cùng ID |

Inventory hiện có một nhóm trùng 2 records xung đột, một log thiếu dateKey, 9 dangling habit refs. Các nhóm có thể giao nhau; đừng tính thành số conflicts độc lập trước classifier. 2 log lệch lịch hiện tại không được tự xóa vì không có revision chứng minh lịch lúc ghi. 5 incomplete logs còn completedAt là legacy cleanup riêng trong cùng policy, không thay đổi timestamp một cách lặng lẽ khi migrate ID.

### 12.2. Apply không tạo thưởng hoặc trạng thái giả

- [ ] Chốt precedence cho completed/approved/reward transitions từ nguồn có authority. Không default OR(true), max timestamp hoặc “approved thắng” cho conflict chưa có chronology.
- [ ] Chốt preservation/retention cho source records. Audit copy nếu cần nằm trong server-private storage ngoài business queries, không để cả legacy và canonical được analytics tính hai lần; erasure C1 phải biết mọi archive chứa scope data.
- [ ] Freeze/migrate writers theo client compatibility policy; client cũ ghi auto IDs phải bị chặn hoặc chuyển maintenance. Không chạy migration trong khi vẫn cho các writers cũ tạo namespace mới vô hạn.
- [ ] Transaction đọc source(s), canonical target, scope/tombstone/version trước writes; kiểm expected updateTime và identity. Ghi canonical state + migration marker, xử lý source theo retention trong transaction/operation đã thiết kế, không tạo khoảng source mất trước target tồn tại.
- [ ] Không gọi completion/approval/reward-grant service khi migrate record. Không cộng lại gold/EXP, không tự đặt rewardGranted=false cho record legacy không biết trạng thái trao thưởng; giữ unknown/provenance ngoài suy diễn boolean mặc định của POCO.
- [ ] Reconcile mismatched target dưới dạng conflict. Retry sau commit response lost phải nhận migration marker/noop, không xóa/copy một lần nữa.

### 12.3. Chống tái phát và kiểm thử

- [ ] Thêm state version/expectedVersion hoặc cơ chế serialize+conflict được authority kiểm chứng; stale false không được ghi đè true/approved đã đổi sau lần đọc.
- [ ] Siết direct wrappers không được tạo/chỉnh approval/reward qua arbitrary state. Hash-based ID hợp lệ ở client không tự chứng minh server uniqueness; Rules không được giả có SHA-256 primitive. Chọn trusted completion API hoặc identity scheme/version khác có compatibility migration riêng; không đổi hash hiện tại ngầm.
- [ ] Test ≥20 concurrent same-day operations; desired true/false, stale version, mixed clients, response lost + retry, target conflict, duplicate approval/reward handling.
- [ ] Test migration replay không đổi balances/EXP, không double-count analytics, legacy missing approval/reward provenance vẫn unknown; unresolved records giữ nguyên và được báo.
- [ ] Readback canonical uniqueness, outside-scope invariants, log totals phân loại trước/sau và conflicts; kiểm không xuất hiện auto-ID mới trong cửa sổ vận hành đã chọn.

**Đóng F10:** canonical record duy nhất cho identity hợp lệ; không lost update/stale overwrite; legacy conflicts đã được quyết định/giải quyết trong scope hoặc finding vẫn partial; server enforcement và repeatable migration evidence tồn tại. Quarantine chưa quyết định không được tính là resolved. Không tự đóng F11 ledger, F09 schedule hoặc F26 clock authority bằng C2.

## 13. C3 — F14/F15/E08: provisioning classification và repair

**Modify:** `FirebaseManager.EnsureParentDataAsync/EnsureDefaultChildDataAsync/EnsurePetExistsAsync`, register/login/Google flows, `ChildService.AddChildAsync`, models nếu thêm version/state. Test: `FirebaseProvisioningEmulatorTests.cs` và pure classifier tests.

### 13.1. Không blind reseed

33 parent đều markerless; không parent nào được chứng minh pending chỉ từ field thiếu. Mọi parent có child document hiện có, nhưng 16 parent có ít nhất một child thiếu pet. 19 child thiếu pet có gold/EXP=0 là tín hiệu phân loại, không bằng chứng có thể reset progression hoặc tạo pet lịch sử giả.

**Đề xuất policy bảo thủ để chuẩn bị:** pet là invariant cần cho progression; sample habit/reward là optional, không auto-backfill; thiếu provenance/history → `needs_review`. Đây là đề xuất, **chưa phải quyết định sản phẩm được phê duyệt**. Gate trước apply phải chốt policy và ghi version; nếu sample seeds bắt buộc, cần task riêng để không hồi sinh dữ liệu chủ động xóa.

| Classification | Điều kiện chứng minh | Proposed outcome |
|---|---|---|
| Complete legacy | Auth owner hợp lệ, child thuộc owner, required docs đúng schema, không pending-delete | Backfill readiness/version sau validation, không sửa business values |
| Repairable missing pet | Owner/child hợp lệ, thiếu đúng pet status, không evidence deliberate deletion/progression cần khôi phục, policy cho phép initialize | Tạo pet còn thiếu bằng transaction create-if-absent; ghi repair provenance và thời điểm thực, không bịa pet cũ |
| Conflicting/incomplete | Auth user thiếu/disabled chưa quyết định, orphan child, pet đã xuất hiện lại, balances/history mâu thuẫn hoặc schema lạ | Không sửa; needs_review với reason riêng |
| Optional seeds absent | Thiếu habit/reward, required invariant khác đạt | Không tự tạo; không coi failure nếu policy optional được chốt |

Auth account inventory chưa được kiểm chứng trong lượt 22/10; cần kiểm tra bằng công cụ Auth phù hợp, chỉ báo aggregate state, không export password hashes hoặc providers/tokens không cần thiết. Parent document không tự chứng minh Auth user hiện còn hợp lệ.

### 13.2. New profile và legacy marker tách biệt

- New parent: giữ pending→idempotent required provisioning→ready. Failure sau mỗi bước không làm session giả thành ready; resume không nhân đôi child/pet hoặc overwrite edits.
- Legacy parent: không gán pending hàng loạt rồi gọi `EnsureParentDataAsync`, vì code pending hiện có thể tạo default child/habit/reward. Dùng migration classifier/repair path riêng, ghi version/ready sau validation; unknown không auto-seed.
- Ready chỉ ghi sau required invariant của đúng scope đạt và auth/session chưa đổi. Một child complete không chứng minh mọi child complete; child count>0 không phải readiness check.
- Retry AddChild cùng operationId giữ payload đầu tiên; request payload khác conflict thay vì silently báo “thành công”. Existing child/pet bảo toàn mọi progression/user edits.
- Khi parent marker ready nhưng child khác thiếu pet do deliberate deletion/policy, không tự coi mỗi lần login là quyền tạo lại pet. Chốt recovery trigger và record disposition rõ.

### 13.3. Hành động/tests

- [ ] Classifier tests: complete markerless, missing pet with/without progression evidence, optional seeds absent, orphan, pending-delete, invalid owner/Auth state, pet xuất hiện sau inventory.
- [ ] Failure injection sau Auth create, parent create, child create, pet write, seed writes, ready marker; mỗi retry phải repair phần thiếu mà không overwrite phần đã dùng.
- [ ] Concurrent signup/login/Google/AddChild: một logical operation, stable IDs và no duplicate. Không dùng số child cố định làm invariant nếu người dùng đã tạo thêm trẻ hợp lệ.
- [ ] Ready/markerless không hồi sinh habit/reward bị xóa. Missing pet reset được policy từ chối khi có unresolved progression/deletion evidence.
- [ ] Apply theo manifest: revalidate auth/owner/scope/updateTime, create-if-absent pet, ghi provenance/version, chỉ set ready sau kiểm tra required docs. Changed record → conflict.
- [ ] Post-read: required-state completeness, optional-data preservation, unchanged balances và sibling docs, failed actions có reason/resume riêng; login thật phù hợp môi trường chứ không chỉ set marker thành công.

**Đóng F14:** invariant tạo trẻ và optional seed policy nhất quán, legacy scope đã phân loại/repair theo quyết định, no overwrite/duplicate. **Đóng F15:** recovery qua từng failure boundary và Auth↔Firestore consistency có bằng chứng, không yêu cầu transaction bất khả thi giữa hai hệ. **Đóng E08:** markerless partial data được xử lý có chủ đích, login retry không bỏ sót hoặc auto-resurrect; chưa có quyết định cho unresolved records thì giữ partial.

## 14. Runbook chung trước và sau từng đợt production

### 14.1. Preflight có thể tái chạy

```bash
# Chỉ đọc trạng thái; giữ nguyên dirty worktree.
git status --short --branch
git rev-parse HEAD
node .gitnexus/run.cjs status
```

Firebase plugin: `firebase_get_environment`, `firestore_get_database`, `firebase_get_security_rules(type=firestore)`, `firestore_list_collections`, `firestore_list_documents` với field masks/showMissing/pagination. IAM/Auth/service metadata có thể dùng authenticated REST qua CLI khi plugin thiếu capability; token chỉ trong RAM, không shell output/env dump. `firebase-tools 15.34.0` không có `firestore:rules:get`, không ghi lệnh đó làm runbook.

Trước sửa function/class/method trong giai đoạn triển khai: GitNexus impact đúng symbol; HIGH/CRITICAL phải cảnh báo, UNKNOWN phải kiểm source/callers/bindings bổ sung. Trước commit được yêu cầu: detect_changes(scope=all), xử lý partial/truncated, xem diff chỉ chứa scope đã làm. Kế hoạch không tự cấp quyền commit.

### 14.2. Checklist cho từng wave

- [ ] Owner/runner xác nhận exact project/database, manifest/policy digest, source/test manifest và cửa sổ vận hành.
- [ ] Dry-run + expected counts/conflicts/noops + plan cho concurrent writers và client versions.
- [ ] Recovery/retention thực sự có thể dùng; không ghi backup pass khi thiếu quyền hoặc không có restore proof cần thiết.
- [ ] Chạy Emulator/API regression tương ứng; private data/identity fixtures không lên production.
- [ ] Thực thi chỉ action được yêu cầu, batch nhỏ có checkpoints; lỗi dừng/reclassify thay vì chuyển scope rộng.
- [ ] Readback data/Rules active, compare expected field deltas, Auth/legitimate client flow và outside-scope invariants.
- [ ] Báo attempted/applied/noop/conflict/failed/skipped; skipped/blocked không được tính pass.
- [ ] Cập nhật finding status theo DoD, lưu evidence không secrets; không đóng toàn nhóm vì một endpoint hoặc một script pass.

### 14.3. Rollback/recovery theo loại thay đổi

| Loại | Recovery được phép thiết kế | Không được làm |
|---|---|---|
| E03/E11 source | Revert riêng patch của task sau review, giữ local edits khác | Blanket reset/restore worktree |
| LFS restoration | Giữ đúng version/objects đã verify, xử lý missing objects/toolchain | Sửa vendor SDK code cho compile qua |
| Rules | Safe reviewed policy hoặc maintenance/read-only | Restore public allow true |
| Password verifier removal | Firebase Auth recovery/reauth; schema stays retired | Khôi phục hash/salt legacy để rollback |
| PIN reset | Fresh reauth/enrollment mới, revoke elevation cũ | Restore default PIN hoặc cũ readable verifier |
| Orphan deletion | Restore chỉ khi có backup/authority/retention cho phép và không trái erasure | Bịa profile/PII hoặc resurrect data đã xác nhận erase |
| Daily-log migration | Reconcile từ approved history/migration records theo retention, chống double reward | Cộng lại reward hoặc overwrite conflicted newer target |
| Provisioning repair | Corrective action có precondition; giữ user edits phát sinh sau repair | Delete pet/child mới chỉ vì rollback deployment |

## 15. Ma trận đóng và phạm vi chưa tự mở rộng

| Finding | Evidence tối thiểu để đóng | Không đủ |
|---|---|---|
| F25 | Final active policy + client negative/positive + API authorization tests, compatibility rollout | Containment prototype hoặc Admin CRUD pass |
| F24 | Fields retired, writer reintroduction blocked, Auth behavior/readback pass | Script xóa exit=0, còn legacy writers |
| F02/E10 | Private verifier, reauth reset, trusted limiter/elevation, legacy outcomes, target runtime proof | UI single-flight hoặc đổi SHA-256 thành KDF nhưng verifier vẫn client-readable |
| F03 | Authorized resumable recursive workflow + legacy scope dispositions + pending-write denial + remote retention evidence | Chỉ delete 20 docs hoặc bốn collection known |
| F10 | Canonical uniqueness, conflict policy, server anti-tampering/stale handling, migration/readback | Đổi ID của logs không conflict nhưng bỏ record lỗi unresolved |
| F14/F15/E08 | Policy/invariant rõ, legacy classification/repair, every-boundary recovery/concurrency, login proof | Set 33 marker=ready hoặc seed pet mọi lần login |
| E03 | Regression + parser suite pass, chunk-independent framing | Một example network response pass |
| E11 | Scope/payload bất biến, unknown commit/refresh retry idempotent, runtime A/B/account tests | Giữ operationId nhưng manager vẫn lấy current scope |
| F31 | Native load + integration + clean-clone/resolver/target proof theo scope platform | Chỉ cài git-lfs/tải một .so/C# compile |

**Dependency chưa biến thành công việc đã hoàn tất:** final F25 có business invariants gắn F09/F11/F26; containment đã deployed nhưng chưa tạo domain authority. Nếu các domain endpoints/authorities đó chưa implement/test, F25 vẫn partial dù public risk đã giảm. F12/N06 revisions và migration lịch sử, F11 reward ledger đầy đủ, N12 device/build, E05/E12 consuming host và F30 hosted CI vẫn là backlog/điều kiện riêng. Không tự mở Chatbot UI hoặc Android scope đã defer để làm đẹp số findings đóng.

## 16. Quyết định phải chốt trước bước phụ thuộc

| Quyết định | Default đề xuất để chuẩn bị | Gate bị chặn nếu chưa chốt |
|---|---|---|
| Containment ảnh hưởng client cũ | Deny public/cross-owner; privileged paths maintenance/read-only nếu chưa có trusted flow | Production B1; source/tests vẫn làm được |
| Retire password verifier không rollback | Xóa field, giữ Auth identity; recovery qua Firebase Auth | Apply B2 |
| Backend/ADC/secret store | Codebase được chọn là HABIMON-BE + Neon; runtime production chưa chọn, Azure app hiện hữu là course-demo; Firebase Admin cần ADC `firebaseauth.users.get` | Deploy B3/C1, revoked-user checks và domain authority integration |
| PIN freshness/TTL/limiter/KDF parameters | Local implementation: proof/challenge 120s, Google freshness 5m, elevation 5m, limits 5/15m, scrypt N=2^14; review/benchmark trên target trước release | Production B3 |
| 3 orphan scopes | retain_pending_review trừ khi có authority restore/erase | Delete/restore C1 |
| Daily conflict/malformed/dangling logs | Giữ nguyên và review; không auto-award/merge | Apply C2 cho records đó; F10 chưa đóng nếu unresolved |
| Pet/seed requirement | Pet required, sample habits/rewards optional, no blind reseed — proposal | Legacy apply C3 |
| Data backup/retention | Không dump credential; secure authorized backup cho destructive noncredential operations nếu policy yêu cầu | Wave tương ứng, không chặn pure local fixes |

Các gate này là thông tin thực thi còn thiếu, không phải yêu cầu người dùng trả lời ngay để hoàn thành tài liệu. Mỗi gate phải được biến thành quyết định ghi nhận và manifest/test cụ thể khi bắt đầu phần công việc liên quan.

## 17. Bằng chứng và tài liệu tham chiếu

- [Current findings/status](CODEBASE_AUDIT_FINDINGS_2026-10-18.md), [remediation DoD](CODEBASE_REMEDIATION_PLAN_2026-10-18.md), [review report](CODEBASE_REVIEW_AND_CHANGE_REPORT_2026-10-18.md), [Firestore setup](FIRESTORE_SETUP_2026-10-18.md).
- [Firestore IAM](https://firebase.google.com/docs/firestore/security/iam): phân biệt quyền admin và Rules của mobile.
- [Rules field controls](https://firebase.google.com/docs/firestore/security/rules-fields): diff/affectedKeys và giới hạn field access.
- [Overlapping allow rules](https://firebase.google.com/docs/rules/rules-behavior): một matching allow=true đủ cấp quyền.
- [Firebase ID-token verification](https://firebase.google.com/docs/auth/admin/verify-id-tokens), [session revocation](https://firebase.google.com/docs/auth/admin/manage-sessions): không suy token-valid thành fresh reauth hoặc parent role.
- [Transactions](https://firebase.google.com/docs/firestore/manage-data/transactions): reads trước writes, retry không replay side effects ngoài DB.
- [REST Precondition](https://firebase.google.com/docs/firestore/reference/rest/v1/Precondition): expected updateTime/existence để chống snapshot drift.
- [Firestore data model](https://firebase.google.com/docs/firestore/data-model): xóa document không tự xóa subcollections.
- [WHATWG SSE](https://html.spec.whatwg.org/multipage/server-sent-events.html#event-stream-interpretation): reset data/event-type buffers giữa frames.

**Trạng thái cuối tài liệu:** kế hoạch đã ghi; chưa hydrate LFS, sửa implementation, chạy migration, deploy Rules/API, bật dịch vụ/billing hoặc đóng thêm finding vì tài liệu này. Mọi checkbox triển khai còn unchecked. Inventory 22/10 là bằng chứng quá khứ có ngày, không cam kết remote state bất biến.
