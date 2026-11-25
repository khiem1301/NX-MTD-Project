# Report 5 – Software Test Documentation (NX-MTD, nhóm G40)

> Nội dung điền vào `Report-5.0_Test Documentation.docx`. Số liệu UT lấy từ `Report-5.1_Unit Test.xlsx` (v3.2, Round 2 after fix). Cột IT/ST/AT để "-" cho tới khi có kết quả.

## Change Log

| **Date** | **A\*, M, D** | **In charge** | **Change Description** |
|---|---|---|---|
| 24/11/2026 | A | [Tên thành viên] | Bổ sung Mục I (Overall Plan), II.1 (Test Data), II.2 (Test Cases), II.3 (Test Reports – cột UT Round 1 & Round 2) |

\*A - Added, M - Modified, D - Deleted

# I. Overall Plan

## 1. Scope of Testing

#### 1.1 Đối tượng kiểm thử (Target-of-Test)

NX-MTD là nền tảng web quản lý xưởng may gia công. Backend dùng ASP.NET Core 9 và EF Core với SQL Server; frontend dùng React 18, TypeScript và Ant Design. Giai đoạn Unit Test hiện tại tập trung vào **tầng Application Service**, nơi chứa toàn bộ luật nghiệp vụ.

**Tính năng/chức năng ĐƯỢC kiểm thử ở mức UT (Report-5.1: 20 sheet TC, 974 test case):**

| **Nhóm nghiệp vụ** | **Service / Component** |
|---|---|
| Sản xuất: công đoạn, mã hàng, lệnh cắt | `OperationService`, `ProductSpecService`, `WorkOrderService`, `SpecImportBuilder`, `SpecImportService` |
| Kho: nhập kho, xuất kho | `MaterialReceiptService`, `MaterialIssueService` |
| May: phân việc, sản lượng, lỗi | `DailyWorkAllocationService`, `OutputService`, `DefectService` |
| Đơn hàng và thanh toán | `CustomerOrderService`, `OrderPaymentService` |
| Nhân sự, tổ, chấm công, lương | `EmployeeService`, `TeamService`, `AttendanceService.AlertSyncOverdueAsync`, `PayrollService` |
| Tài chính và hoà vốn | `FinanceService` |
| Vận chuyển | `VehicleService` (GetLiveStatus / StartTrip / CompleteTrip) |
| Yêu cầu sửa máy | `MachineRepairRequestHandler` / `NoLineRequestHandler` |
| Trang chủ | `HomeDashboardService` |

**Yêu cầu phi chức năng được kiểm thử gián tiếp ở mức UT:**

- An toàn ngoại lệ: không để `NullReferenceException`, `KeyNotFoundException`, `ArgumentOutOfRangeException` lọt ra đường gọi phía API.
- Toàn vẹn giao dịch: Rollback/Commit đúng; không gọi `SaveChanges` khi validation thất bại.
- Chống ghi đồng thời: khoá `IAppLock`, xung đột Concurrency.
- Khả năng chịu lỗi: HomeDashboard fallback từng khối dữ liệu, không làm đổ cả DTO.
- Tính tất định: dùng `IClock` cố định.

**KHÔNG kiểm thử ở mức UT** (để lại cho IT/ST/AT):

| **Tầng** | **Lý do loại trừ** |
|---|---|
| API Controllers | Binding, filter, `[Authorize]`, HTTP status: thuộc Integration/API Test |
| Middleware / JWT pipeline | Auth scheme, CORS, exception middleware |
| EF Core DbContext ↔ SQL Server | Trigger, CHECK constraint, stored procedure (`usp_Stock_Post`), transaction isolation thật |
| Frontend (`fe/`) | Có bộ Jest riêng, không tính vào Report-5.1 |
| RJ Sync Agent / máy chấm công | Tiến trình ngoài và phần cứng; UT chỉ mock snapshot/repository |
| Test class khác trong `tests/Application.Tests` (Auth, Meal, KPI, Request, …) | Có tồn tại nhưng không nằm trong 974 TC của Report-5.1 |
| Hiệu năng, tải, UI/UX | Thuộc System Test |

#### 1.2 Các cấp độ kiểm thử áp dụng

| **Level** | **In-charge** | **Đầu vào / Thời điểm** | **Trọng tâm** | **Tiêu chí chấp nhận** |
|---|---|---|---|---|
| **Unit Test** | Dev và Tester nhóm G40 (người thiết kế TC tách biệt với người viết service) | SRS, mô tả Database, chữ ký interface/DTO; sau khi mỗi service hoàn thành chức năng | Luật nghiệp vụ, nhánh logic, validation, chuyển trạng thái, xử lý ngoại lệ | 100% TC được thực thi; Round cuối Pass rate = 100%; 0 defect Critical/Major còn mở; CI gate `Category!=HighComplexity` xanh |
| **Integration Test** | Tester G40 | API chạy ở localhost, DB có TestDataSeeder; sau khi UT Round 2 đạt | Controller ↔ Service ↔ EF Core ↔ SQL Server, JWT/phân quyền, IDOR, ràng buộc DB, background job | ≥ 95% TC Passed; 0 Critical còn mở; mọi lỗ hổng phân quyền đã fix |
| **System Test** | Tester G40 | Bản build FE + BE hoàn chỉnh; sau IT | Luồng end-to-end (đơn hàng → nhập kho → cắt → may → xuất kho → lương), UI, hiệu năng cơ bản | Toàn bộ luồng chính Passed; 0 Critical/Major còn mở |
| **Acceptance Test** | Đại diện xưởng (khách hàng), GVHD; nhóm hỗ trợ | Bản release candidate | Nghiệp vụ thực tế theo kịch bản người dùng | Khách hàng ký xác nhận |

#### 1.3 Ràng buộc và giả định (Constraints & Assumptions)

1. **Cô lập phụ thuộc:** UT dùng NSubstitute thay thế toàn bộ phụ thuộc; không kết nối SQL Server thật, không gọi HTTP, không chạm máy chấm công RJ.

| **Nhóm** | **Thành phần mock** | **Mục đích** |
|---|---|---|
| Persistence | `IUnitOfWork`, `I*Repository`, `IGenericRepository<T>` | Cô lập DB / EF Core |
| Thời gian | `IClock` (Today = 2026-11-24) | Biên ngày tất định |
| Người dùng | `ICurrentUser` (Role, EmployeeId, AccountId) | Phân quyền, phạm vi dữ liệu |
| Thông báo | `INotificationService` | Không gửi push/mail thật |
| Audit | `IAuditLogService` | Ghi vào spy thay vì DB |
| Khoá | `IAppLock` | Giả lập timeout / thành công |
| Cấu hình | `ISystemSettingReader` | Định mức nhiên liệu, ngưỡng sync |
| Excel I/O | Parser/Writer stub, workbook in-memory | Không đọc file đĩa (trừ fixture ClosedXML) |
| Bảo mật | `IPasswordHasher`, `ITemporaryPasswordGenerator` | Không chạy BCrypt thật |
| Dịch vụ chéo | `IDailyWorkAllocationService`, `IOrderProgressService`, `ICostSummaryService`, … | Cô lập side-effect giữa service |
| File store | `IFileStore` | Không ghi blob thật |

2. **Giả định:** hành vi thật của DB (trigger, CHECK, FK, isolation level) chưa được xác nhận ở UT, nên phải kiểm lại ở IT.

3. Bộ **HighComplexity** (`Category=HighComplexity`) chỉ mang tính thông tin trên CI (`continue-on-error: true`); gate merge là `Category!=HighComplexity`.

4. Chỉ số "coverage 100%" trong sheet Test Statistics là **tỉ lệ TC được thực thi/thành công**, không phải code coverage do coverlet đo.

5. Không dùng connection string production ở bất kỳ cấp độ test nào.

## 2. Test Strategy

Chiến lược kiểm thử của NX-MTD kết hợp bốn kỹ thuật:

1. **Context-Isolated Adversarial Testing (Kiểm thử đối kháng tách biệt ngữ cảnh):** người/agent thiết kế test case chỉ nhận đặc tả (SRS, mô tả DB, chữ ký interface, DTO, hằng số Domain), không được xem chi tiết hiện thực service. Họ đóng vai "đối thủ", chủ động tìm đầu vào làm hỏng code (null, rỗng, tràn số, trùng lặp, sai trạng thái, đua ghi) thay vì xác nhận code chạy đúng như cách nó đang được viết. Việc tách ngữ cảnh loại bỏ thiên kiến "test theo code".

2. **Branch-Driven Testing (Kiểm thử bao phủ nhánh):** với mỗi phương thức public, liệt kê mọi nhánh quyết định (guard/if, switch trạng thái, catch, early-return, tập rỗng/một/nhiều phần tử) và gán ít nhất một UTCID cho mỗi nhánh. Nhánh rủi ro được đánh dấu ⚠ (#1, #2…) trên sheet.

3. **Assert-Locking (Khoá kỳ vọng):** cột Expected được chốt từ đặc tả **trước khi** chạy. Khi test fail, mặc định **sửa code ứng dụng, không sửa assertion**. Assertion chỉ được mở khoá khi nhóm xác nhận đặc tả sai, và phải ghi lý do trên sheet. Bộ rebaseline UTCID533–974 áp dụng đúng nguyên tắc này: Round 1 fail ở assert-lock, Round 2 Passed sau khi fix production.

4. **Phân loại ISTQB:** mỗi UTCID gắn một loại: **N (Normal)** luồng hợp lệ; **A (Abnormal)** đầu vào/trạng thái sai, phụ thuộc lỗi; **B (Boundary)** giá trị biên như 0/1/99/100, 20/21, 100/101 ký tự, ngày biên kỳ kế toán.

**Quy trình vận hành theo đợt:**

```
Thiết kế TC (tách ngữ cảnh) → Khoá Expected → Round 1 (dotnet test)
   → Lọc nhiễu (triage) → Fix bug trong code ứng dụng
   → Round 2 (hồi quy toàn bộ 974 TC) → Script Python xuất báo cáo Excel
```

- **Lọc nhiễu:** mỗi TC fail ở Round 1 được phân loại: (a) lỗi thật của code, ghi Defect với mã trùng UTCID; (b) lỗi setup/mock của test, sửa test và không tính defect; (c) đặc tả mơ hồ, chốt lại với nhóm rồi cập nhật Expected kèm lý do. Kết quả: toàn bộ **141 TC fail ở Round 1 đều xác nhận là defect thật** của code.
- **Round 2:** chạy lại toàn bộ bộ test (không chỉ các TC đã fail) để phát hiện hồi quy.
- **Tự động hoá báo cáo:** script Python đọc kết quả `dotnet test` (TRX/log), ghi Pass/Fail vào cột Round tương ứng trên từng sheet của `Report-5.1_Unit Test.xlsx` và tính lại sheet Test Statistics; lịch sử Round 1 được giữ nguyên để truy vết.

### 2.1 Testing Types

| **#** | **Testing Type** | **Objective** | **Technique** | **Completion Criteria** |
|---|---|---|---|---|
| 1 | **Functional Testing (Business Logic)** | Mỗi service thực hiện đúng nghiệp vụ: đơn hàng, nhập/xuất kho, lệnh cắt, phân việc, sản lượng, lương, hoà vốn, chuyến xe | Branch-driven; TC loại Normal; assert trên `Result`, dữ liệu trả về và số lần gọi repository/UoW (NSubstitute `Received()`) | 100% nhánh chính có TC; mọi TC Normal Passed |
| 2 | **Input Validation Testing** | Đầu vào sai bị chặn bằng `Result` Validation hoặc `ArgumentNullException`, không crash | Equivalence Partitioning + Boundary Value Analysis: null DTO/query/workbook/`workerIds`; chuỗi bắt buộc rỗng; `FullName`/`TeamCode`/tên component ≤ 100 ký tự; Note ≤ 200; Color ≤ 50; `PiecesPerGarment` ∈ [1, 99]; `MarkerNo` ∈ [1, 20]; OdometerEnd > OdometerStart; FuelCost có giá trị thì bắt buộc FuelReceipt `IsCurrent`; trùng email/username/team code/variant/norm | Mọi TC Abnormal/Boundary Passed; không TC nào trả exception ngoài dự kiến |
| 3 | **State Transition Testing** | Chặn chuyển trạng thái sai trên entity có vòng đời | State table theo `Domain/Constants`: Trip chỉ Complete khi `STARTED`; Resign chỉ khi `ACTIVE`; Team Discontinue/Reactivate; Order/WorkOrder/Receipt/Issue/Payroll kiểm `InvalidStatus`, kỳ đã khoá, cửa sổ chỉnh sửa | Mỗi cặp (trạng thái nguồn, hành động) hợp lệ/không hợp lệ đều có TC Passed |
| 4 | **Business Rule & Authorization-Scope Testing** | Luật phân quyền và phạm vi dữ liệu ở tầng service | Mock `ICurrentUser`: `CheckPlacement` (Accountant không gắn tổ may, Worker không gắn tổ Office); không chuyển tổ trưởng/phó (`TEAM_TRANSFER_LEADER`); Manager ngoài phạm vi → NotFound/Forbidden; Admin/Accountant ẩn với Manager | Mọi TC scope Passed; 0 trường hợp lộ dữ liệu ngoài phạm vi |
| 5 | **Exception Handling & Resilience Testing** | Không ném NRE/KeyNotFound/OutOfRange; khối phụ lỗi thì fallback | Mock repository ném exception; assert `Result.Failure` hoặc giá trị fallback (HomeDashboard: 0, `[]`, null) | 0 unhandled exception trong 974 TC |
| 6 | **Transaction & Concurrency Testing** | Tính nguyên tử và chống ghi đồng thời | Mock `IUnitOfWork.BeginTransaction/Commit/Rollback`, `IAppLock` timeout, `DbUpdateConcurrencyException`; assert không `SaveChanges` khi lỗi, có Rollback, lock xe khi CompleteTrip, TeamOrderBusy | Mọi TC Concurrency/Busy/Rollback Passed |
| 7 | **Calculation Testing** | Đúng số liệu lương, hoà vốn, phân bổ chi phí, tồn kho | Kiểm số học theo đặc tả: basis `OUTPUT`/`LABOUR_HOURS`, Distinct at-risk, sort ổn định, phân bổ biến phí chung, `BreakEvenPeriodMonthInvalid` | Sai số = 0 với dữ liệu mẫu cố định |
| 8 | **Data Import Testing (Excel)** | Import thông số mã hàng/công đoạn từ Excel đúng và an toàn | Workbook in-memory (ClosedXML): sheet rỗng, tên sheet không phân biệt hoa thường, dòng trùng, marker/color-only, MarkAssumed, CanImport | Mọi TC SpecImport Passed |
| 9 | **Background Job Testing** | Job cảnh báo đồng bộ chấm công quá hạn hoạt động đúng | UT: gọi trực tiếp `AlertSyncOverdueAsync` với `IClock` và ngưỡng mock. IT: chạy job thật trên DB seed | UT Passed (đã đạt); IT: tạo đúng 1 thông báo cho Admin |
| 10 | **Security Testing** (IT/ST) | JWT, `[Authorize]`, IDOR ngang/dọc, khoá tài khoản | Gọi API bằng token của từng persona ở bảng 1.1; đổi ID tài nguyên sang của người khác | 0 lỗ hổng IDOR/leo quyền |
| 11 | **UI Testing** (IT/ST/AT) | Giao diện React/Antd hiển thị và thao tác đúng | Jest + Testing Library (component); kiểm thử thủ công theo kịch bản | Không lỗi Critical/Major về UI |
| 12 | **Performance Testing** (ST) | Màn hình chính và API phản hồi chấp nhận được | Đo thời gian phản hồi API chính trên localhost với dữ liệu seed mở rộng | Ngưỡng do nhóm chốt ở ST (ví dụ p95 < 2s) |

### 2.2 Test Levels

| **Type of Tests** | **Unit Test** | **Integration Test** | **System Test** | **Acceptance Test** |
|---|---|---|---|---|
| Functional Testing | ✓ | ✓ | ✓ | ✓ |
| Input Validation Testing | ✓ | ✓ |   |   |
| State Transition Testing | ✓ | ✓ | ✓ |   |
| Business Rule & Authorization-Scope Testing | ✓ | ✓ |   |   |
| Exception Handling & Resilience Testing | ✓ | ✓ |   |   |
| Transaction & Concurrency Testing | ✓ | ✓ |   |   |
| Calculation Testing | ✓ | ✓ | ✓ | ✓ |
| Data Import Testing (Excel) | ✓ | ✓ | ✓ |   |
| Background Job Testing | ✓ | ✓ |   |   |
| Security Testing (JWT, IDOR) |   | ✓ | ✓ |   |
| UI Testing | (Jest, ngoài Report-5.1) | ✓ | ✓ | ✓ |
| Performance Testing |   |   | ✓ |   |

> Ghi chú: ở UT, Background Job và Authorization chỉ kiểm phần logic trong service (mock clock/user); pipeline thật (hosted job, JWT, `[Authorize]`) được kiểm ở IT.

### 2.3 Supporting Tools

| **Purpose** | **Tool** | **Vendor/In-house** | **Version** |
|---|---|---|---|
| Unit Test framework (BE) | xUnit | xUnit.net (OSS) | 2.9.2 |
| Test SDK | Microsoft.NET.Test.Sdk | Microsoft | 17.12.0 |
| Test runner (VS) | xunit.runner.visualstudio | xUnit.net (OSS) | 2.8.2 |
| Mocking | NSubstitute | NSubstitute (OSS) | 6.2.0 |
| Assertion | FluentAssertions | Xceed / OSS | 7.2.0 |
| Code coverage | coverlet.collector | coverlet (OSS) | 6.0.2 |
| Excel fixture / import | ClosedXML | ClosedXML (OSS) | 0.102.3 |
| FE unit test | Jest + @testing-library/react | Meta / OSS | 29.7.0 / 16.3.3 |
| CI | GitHub Actions (`.github/workflows/ci.yml`) | GitHub | – |
| Xuất báo cáo UT ra Excel | Script Python (đọc kết quả `dotnet test`, ghi Report-5.1, tính lại Statistics) | In-house (G40) | [điền] |
| Quản lý Test Case / Report | Microsoft Excel (Report-5.1, Report5_Test Report) | Microsoft | [điền] |
| API test (IT, dự kiến) | Postman / `Microsoft.AspNetCore.Mvc.Testing` | Postman / Microsoft | [chốt khi bắt đầu IT] |

## 3. Test Plan

### 3.1 Test Environment

| **Purpose** | **Tool** | **Provider** | **Version** |
|---|---|---|---|
| Hệ điều hành máy test | Windows 10/11 hoặc macOS (máy cá nhân thành viên) | Microsoft / Apple | [điền] |
| Runtime và SDK backend, chạy UT | .NET SDK (target `net9.0`) | Microsoft | 9.0.x |
| Web framework backend | ASP.NET Core (Sdk.Web), JwtBearer | Microsoft | 9.0.x |
| ORM | EF Core SqlServer | Microsoft | 9.0.10 |
| Validation | FluentValidation | OSS | 12.2.0 |
| Database (IT/ST) | Microsoft SQL Server, DB `NXMTD` trên localhost (LocalDB/Developer Edition) | Microsoft | [điền] |
| Frontend runtime | Node.js | OpenJS Foundation | ≥ 24 |
| Frontend build/dev server | Vite (localhost, cổng mặc định của Vite) | OSS | 8.3.4 |
| Frontend | React / TypeScript / Ant Design | Meta / Microsoft / Ant Group | 18.3.1 / 5.9 / 5.29.3 |
| Backend API | Kestrel, `https://localhost:{port}` theo `launchSettings.json` | Microsoft | 9.0.x |
| Trình duyệt (ST/AT) | Google Chrome, Microsoft Edge | Google / Microsoft | Bản mới nhất |
| IDE | Visual Studio 2022 / VS Code / Cursor | Microsoft / Anysphere | [điền] |
| Đồng hồ hệ thống trong UT | `IClock` mock (Today = 2026-11-24; Now = 08:00/10:00 +07) | In-house | – |

### 3.2 Test Milestones

> Ngày UT bám theo tiến độ đã có (Round 2 chốt 24/11/2026). Ngày IT/ST/AT là dự kiến, nhóm chỉnh theo lịch SEP490.

| **Milestone Task** | **Start Date** | **End Date** |
|---|---|---|
| Lập Test Plan (Report-5.0 Mục I) | [dd/mm/2026] | [dd/mm/2026] |
| Thiết kế UT case tách ngữ cảnh, khoá Expected (UTCID01–511 HighComplexity) | [dd/mm/2026] | [dd/mm/2026] |
| Thiết kế UT rebaseline (UTCID533–974) | [dd/mm/2026] | [dd/mm/2026] |
| UT Round 1: thực thi 974 TC (833 Passed / 141 Failed) | [dd/mm/2026] | [dd/mm/2026] |
| Lọc nhiễu và fix 141 defect trong code ứng dụng | [dd/mm/2026] | [dd/mm/2026] |
| UT Round 2: hồi quy 974 TC (974 Passed), xuất báo cáo bằng script Python | [dd/mm/2026] | 24/11/2026 |
| Dựng TestDataSeeder và thiết kế IT case | 25/11/2026 | [dd/mm/2026] |
| Integration Test Round 1 → fix → Round 2 | [dd/mm/2026] | [dd/mm/2026] |
| System Test (E2E, UI, Performance, Security) | [dd/mm/2026] | [dd/mm/2026] |
| Acceptance Test với khách hàng/GVHD | [dd/mm/2026] | [dd/mm/2026] |
| Hoàn thiện Report-5 (Test Reports đầy đủ IT/ST/AT) | [dd/mm/2026] | [dd/mm/2026] |

# II. Implementation & Schedule

## 1. Test Data

**Quy tắc áp dụng cho NX-MTD:**

- Test UT và IT phải tự chứa: UT seed in-memory theo từng UTCID; IT dùng `TestDataSeeder` chạy trong transaction hoặc DB riêng, rollback/reset sau mỗi test class.
- Không phụ thuộc dữ liệu do test trước để lại.
- **Không dùng ID tự tăng:** mọi tham chiếu dùng khoá nghiệp vụ (Username, TeamCode, OrderCode, ProductCode, …); Seeder tra ID thật lúc chạy bằng mã này.
- Ngày tháng tính tương đối theo `IClock.Today` (T, T−1, T+3…), không hard-code ngày tuyệt đối; kỳ kế toán neo theo tháng của T (CUR = tháng của T, PREV = tháng trước).
- Mật khẩu chỉ là giá trị test (`NxTest@2026`), được hash bằng `IPasswordHasher` khi seed; không dùng mật khẩu thật.

### 1.1 Minimum Test Account

| **Username** | **Role** | **Password** | **Account Status** | **Purpose** |
|---|---|---|---|---|
| admin_01 | Admin | NxTest@2026 | ACTIVE | Tính năng chỉ dành cho Admin: tạo đơn hàng, duyệt lương, quản lý tài khoản, audit log; nhận cảnh báo sync chấm công quá hạn |
| admin_02 | Admin | NxTest@2026 | ACTIVE | Admin thứ hai: test guard "không khoá/xoá Admin cuối cùng" và duyệt chéo |
| manager_01 | Manager | NxTest@2026 | ACTIVE | Quản đốc: tạo KPI tổ may, phân việc, quản lý NV trong phạm vi xưởng; kiểm Admin/Accountant bị ẩn với Manager |
| accountant_01 | Accountant | NxTest@2026 | ACTIVE | Lập bảng lương, kết thúc chuyến xe, thanh toán đơn, hoà vốn; kiểm `CheckPlacement` (không gắn tổ may) |
| warehouse_01 | WarehouseKeeper | NxTest@2026 | ACTIVE | Nhập kho, xuất kho cho tổ cắt, chọn xe/phương thức giao, kiểm kê |
| qaqc_01 | QaQc | NxTest@2026 | ACTIVE | Ghi nhận hàng ra chuyền và lỗi (REWORK/SCRAP/OUTPUT_ERR) |
| electrician_01 | Electrician | NxTest@2026 | ACTIVE | Xử lý yêu cầu sửa máy (`MachineRepairRequestHandler`) |
| leader_sew_a | TeamLeader | NxTest@2026 | ACTIVE | Tổ trưởng TEAM-SEW-A: phân việc, xác nhận sản lượng tổ mình; không được chuyển sang tổ khác (`TEAM_TRANSFER_LEADER`) |
| leader_sew_b | TeamLeader | NxTest@2026 | ACTIVE | Tổ trưởng TEAM-SEW-B: **IDOR ngang**, không được xem/sửa sản lượng, phân việc của TEAM-SEW-A |
| deputy_sew_a | TeamDeputy | NxTest@2026 | ACTIVE | Tổ phó TEAM-SEW-A: xác nhận thay (ON_BEHALF); chặn chuyển tổ tổ phó |
| worker_sew_a1 | Worker | NxTest@2026 | ACTIVE | Công nhân may (PIECE_RATE) tổ A, có phiếu lương và phiếu giao hàng PENDING; người dùng chính cho test IDOR |
| worker_sew_a2 | Worker | NxTest@2026 | ACTIVE | Công nhân may cùng tổ A: **IDOR ngang**, không được xem payslip/phiếu giao/yêu cầu của worker_sew_a1 và ngược lại |
| worker_sew_b1 | Worker | NxTest@2026 | ACTIVE | Công nhân tổ B: IDOR khác tổ; đối tượng chuyển tổ (Transfer) |
| worker_cut_01 | Worker | NxTest@2026 | ACTIVE | Công nhân tổ cắt (PayType DAILY, tính công theo ngày) |
| worker_locked | Worker | NxTest@2026 | LOCKED | Đăng nhập phải bị từ chối; token cũ không còn dùng được |
| worker_resigned | Worker | NxTest@2026 | RESIGNED | Không đăng nhập được; Resign lần hai trả `InvalidStatus`; không xuất hiện trong danh sách phân việc |

### 1.2 Minimum Domain Data

| **Entity** | **Count** | **Required State** | **Purpose** |
|---|---|---|---|
| Role | 9 | ADMIN, MANAGER, ACCOUNTANT, WAREHOUSE_KEEPER, TEAM_LEADER, TEAM_DEPUTY, QAQC, ELECTRICIAN, WORKER | Đủ danh mục Role cho bảng 1.1 |
| Team | 5 | TEAM-OFFICE: OFFICE/ACTIVE · TEAM-CUT-01: CUTTING/ACTIVE · TEAM-SEW-A: SEWING/ACTIVE (leader_sew_a, deputy_sew_a, worker_sew_a1, worker_sew_a2) · TEAM-SEW-B: SEWING/ACTIVE (leader_sew_b, worker_sew_b1) · TEAM-SEW-X: SEWING/DISCONTINUED (không có NV) | Đủ 3 TeamType và 2 trạng thái; test Transfer, Discontinue/Reactivate, IDOR khác tổ, `CheckPlacement` |
| Employee (PayType) | theo 1.1 | PIECE_RATE: worker_sew_*; DAILY: worker_cut_01; MONTHLY: nhân sự văn phòng | Đủ 3 PayType cho tính lương |
| Customer | 1 | CUST-PARENT: công ty mẹ, ACTIVE | Xưởng con chỉ nhận đơn từ công ty mẹ |
| Material | 3 | MAT-FAB-01: vải, ACTIVE, tồn đủ · MAT-FAB-02: vải, ACTIVE, tồn thấp hơn định mức · MAT-TRIM-OLD: phụ liệu, DISCONTINUED | Xuất/nhập đủ và thiếu hàng (chặn lưu); vật tư ngừng dùng |
| Product | 2 | PRD-001: ACTIVE, 2 màu × 2 size = 4 ProductVariant, component PANEL (PiecesPerGarment = 1 và 99) và TRIM · PRD-002: chưa có ProductMarker (thiếu thông số) | Thông số mã hàng, biên Pieces [1, 99], đơn có/không đủ thông số |
| Operation (công đoạn) | 4 | PRD-001: OP10, OP20 ACTIVE (OP20 là final step) · OP30: DISCONTINUED · OP40: ACTIVE, đơn giá hiệu lực từ T−30 | Final step, chuyển final, công đoạn ngừng, đơn giá theo ngày |
| ProductMarker / Ratio | 2 | ORD-IT-PROD × PRD-001: MarkerNo = 1 và MarkerNo = 20 (biên trên) | Biên `MarkerNo` [1, 20]; lệnh cắt theo sơ đồ |
| CustomerOrder | 8 | ORD-IT-NEW: NEW · ORD-IT-PROD: IN_PRODUCTION · ORD-IT-INDLV: IN_DELIVERY · ORD-IT-PART: PARTIALLY_DELIVERED · ORD-IT-DLV: DELIVERED · ORD-IT-DONE: COMPLETED · ORD-IT-STOP: STOPPED · ORD-IT-CANC: CANCELLED (đều thuộc CUST-PARENT, PRD-001) | Phủ đủ 8 OrderStatus; chặn chuyển trạng thái sai; giảm số lượng dưới mức đã sản xuất (BelowProduced) |
| OrderLine / OrderLineTeam | 4 | ORD-IT-PROD: 4 variant; 2 dòng giao TEAM-SEW-A, 2 dòng giao TEAM-SEW-B | Phân việc theo tổ, IDOR tổ, tiến độ đơn |
| OrderPayment | 2 | ORD-IT-DLV: đã thanh toán một phần · ORD-IT-DONE: đã thanh toán đủ | Lọc State không phân biệt hoa thường; công nợ đơn |
| WorkOrder (lệnh cắt) | 4 | WO-IT-OPEN: OPEN · WO-IT-CUT: CUTTING · WO-IT-DONE: COMPLETED · WO-IT-CANC: CANCELLED (đều thuộc ORD-IT-PROD) | Đủ 4 WorkOrderStatus; lệnh thường chỉ cảnh báo thiếu vải, lệnh cắt bù bị chặn |
| MaterialReceipt (nhập kho) | 3 | GRN-IT-01: PENDING · GRN-IT-02: CONFIRMED (đã post Stock) · GRN-IT-03: RETURNED (có Reason) | Đủ ReceiptStatus; nhập thiếu thì chặn lưu; `usp_Stock_Post` |
| MaterialIssue (xuất kho) | 3 | ISS-IT-01: PENDING_RECEIPT → TEAM-CUT-01 · ISS-IT-02: RECEIVED · ISS-IT-03: REJECTED (có Reason) | Đủ IssueStatus; tồn không đủ thì chặn lưu |
| Stock | 2 | MAT-FAB-01: dư · MAT-FAB-02: thiếu so với ISS-IT-01 | Kiểm tồn khi xuất |
| Stocktake | 3 | STK-IT-01: PENDING_APPROVAL · STK-IT-02: APPROVED · STK-IT-03: RETURNED | Đủ StocktakeStatus |
| Handover (WorkAllocation) | 4 | ORD-IT-PROD, TEAM-SEW-A → worker_sew_a1: PENDING (tạo lúc Now−61 phút) · CONFIRMED (ConfirmMode = WORKER) · REJECTED · CANCELLED | Đủ HandoverStatus; auto-confirm SYSTEM sau 60 phút; xác nhận thay ON_BEHALF |
| WorkAllocation Movement | 2 | HANDOVER và RETRIEVAL (RetrievalReason = END_OF_DAY) cho worker_sew_a2 | Đủ MovementType; settle khi rời tổ |
| OutputRecord | 2 | worker_sew_a1, OP10, ngày T: SAVED · ngày thuộc PREV: LOCKED | Sửa được bản SAVED; chặn bản LOCKED/kỳ đã khoá |
| DefectRecord | 4 | REWORK/RECORDED · SCRAP/CONFIRMED · OUTPUT_ERR/CANCELLED · 1 bản LOCKED (DB-only, thuộc PREV) | Đủ DefectType và DefectStatus |
| PieceRate | 4 | DRAFT · PENDING_APPROVAL · APPROVED · REJECTED (theo OP10/OP20) | Đủ PieceRateStatus; luồng duyệt đơn giá |
| AccountingPeriod | 2 | CUR: OPEN · PREV: LOCKED | Chặn ghi Production/Attendance/MonthlyCost/Payroll vào kỳ đã khoá |
| Payroll / Payslip | 4 | PREV: APPROVED (có Payslip của worker_sew_a1 và worker_sew_a2) · CUR: DRAFT · 1 bản PENDING_APPROVAL · 1 bản REJECTED | Đủ PayrollStatus; IDOR payslip giữa a1 và a2; chặn duyệt lại |
| MonthlyCost (Break-even) | 2 | CUR: đủ chi phí cho basis OUTPUT và LABOUR_HOURS · PREV: thiếu chi phí | Hoà vốn 2 basis; `BreakEvenNotReady` |
| Attendance / RJ Sync | 2 | worker_sew_a1: ngày T−1 thiếu 1 lần chấm (MissingPunch) · SyncState: lần sync cuối vượt ngưỡng `ISystemSettingReader` | Cảnh báo thiếu chấm công; job `AlertSyncOverdueAsync` |
| Request | 7 | Lần lượt PENDING_APPROVAL, PENDING_PROCESSING, APPROVED, REJECTED, COMPLETED, NOT_COMPLETED, CANCELLED (có 1 yêu cầu sửa máy giao electrician_01; worker_sew_a1 tạo ít nhất 1 yêu cầu) | Đủ RequestStatus; IDOR yêu cầu giữa các worker |
| PurchaseOrder | 2 | PO-IT-01: PENDING_FINALISATION · PO-IT-02: UNPAID | Phủ các trạng thái đã biết (bổ sung theo `PurchaseOrderStatuses` thực tế) |
| Vehicle | 2 | VEH-01: đang có chuyến · VEH-02: rảnh | Lock xe chống double-complete; StartTrip trên xe rảnh |
| VehicleTrip | 2 | TRIP-IT-01: STARTED (VEH-01, gắn ISS-IT-02, "công ty giao") · TRIP-IT-02: COMPLETED (có FuelCost và FuelReceipt `IsCurrent`) | Complete chỉ khi STARTED; OdometerEnd > Start; vượt định mức nhiên liệu |
| SystemSetting | 2 | Định mức nhiên liệu (lít/km) · Ngưỡng sync chấm công quá hạn | Cấu hình cho cảnh báo over-norm và sync overdue |

## 2. Test Cases

- Unit Test Cases: `Report-5.1_Unit Test.xlsx` — 20 sheet TC, 974 UTCID, phân loại N/A/B, cột Round 1/Round 2 và sheet Test Statistics.
- Other Test Cases (IT, ST, AT): `Report5_Test Report.xlsx` (lập ở giai đoạn IT).

## 3. Test Reports

Ở Round 1, 974 UT case chạy được 833 Passed và 141 Failed (pass rate 85,52%). Lọc nhiễu xác nhận cả 141 lần fail đều là lỗi thật trong code ứng dụng: **13 Critical, 86 Major, 42 Minor**. Lỗi tập trung vào null-safety (NRE tại `.Trim()`), thiếu validation, thiếu kiểm trạng thái/concurrency và thiếu `await`. Toàn bộ được sửa trong production code (không sửa assertion). Round 2 chạy hồi quy đủ 974 TC: 974 Passed, 0 Failed (100%).

Module nhiều lỗi nhất ở Round 1: OperationService (16), TeamService (13), WorkOrderService, CustomerOrderService và EmployeeService (mỗi module 10).

### 3.1 Summary Table

| **Round** | **Metrics** | **UT** | **IT** | **ST** | **AT** | **Total** | **Note** |
|---|---|---|---|---|---|---|---|
| **Round #1** | **Total** | 974 | - | - | - | 974 | 20 sheet TC trong Report-5.1 |
|   | **Passed** | 833 | - | - | - | 833 | Pass rate 85,52% |
|   | **Failed** | 141 | - | - | - | 141 | Toàn bộ xác nhận là defect thật sau lọc nhiễu |
| **Round #2** | **Total** | 974 | - | - | - | 974 | Hồi quy toàn bộ sau fix code ứng dụng |
|   | **Passed** | 974 | - | - | - | 974 | Pass rate 100%; 0 Pending/Untested |
|   | **Failed** | 0 | - | - | - | 0 | Đóng 141/141 defect |

### 3.2 Test Levels' Defects

| **Metric** | **Review** | **UT** | **IT** | **ST** | **AT** | **Total** | **Note** |
|---|---|---|---|---|---|---|---|
| **Total Defects** | - | 141 | - | - | - | 141 | R1: 13 Critical, 86 Major, 42 Minor |
| **Open Defects (Critical)** | - | 0 | - | - | - | 0 | 13/13 đã đóng ở Round 2 |
| **Open Defects (Major)** | - | 0 | - | - | - | 0 | 86/86 đã đóng ở Round 2 |
| **Open Defects (Minor)** | - | 0 | - | - | - | 0 | 42/42 đã đóng ở Round 2 |

### 3.3 Work Product's Defects

| **Product** | **Review** | **UT** | **IT** | **ST** | **AT** | **Total** | **Note** |
|---|---|---|---|---|---|---|---|
| **Software Package** | - | 141 | - | - | - | 141 | Lỗi trong tầng Application Service (BE); đã fix 100% |
| **Report 1 (Intro)** | - | 0 | - | - | - | 0 | UT không phát hiện lỗi tài liệu |
| **Report 2 (Plan)** | - | 0 | - | - | - | 0 |   |
| **Report 3 (SRS)** | - | 0 | - | - | - | 0 | Điểm mơ hồ đặc tả đã chốt ở bước lọc nhiễu, không tính defect |
| **Report 4 (SDD)** | - | 0 | - | - | - | 0 |   |

---

# PHỤ LỤC: Hai bảng Test Data (dùng để dựng TestDataSeeder cho Integration Test)

> Quy ước cho Seeder: tham chiếu bằng mã nghiệp vụ, không dùng ID cứng. `T` = `IClock.Today`; `CUR` = kỳ tháng của T, `PREV` = tháng trước. Mật khẩu test chung: `NxTest@2026` (hash khi seed).

### 1.1 Minimum Test Account

| **Username** | **Role** | **Password** | **Account Status** | **Purpose** |
|---|---|---|---|---|
| admin_01 | Admin | NxTest@2026 | ACTIVE | Tính năng chỉ dành cho Admin: tạo đơn hàng, duyệt lương, quản lý tài khoản, audit log; nhận cảnh báo sync chấm công quá hạn |
| admin_02 | Admin | NxTest@2026 | ACTIVE | Admin thứ hai: test guard "không khoá/xoá Admin cuối cùng" và duyệt chéo |
| manager_01 | Manager | NxTest@2026 | ACTIVE | Quản đốc: tạo KPI tổ may, phân việc, quản lý NV trong phạm vi xưởng; kiểm Admin/Accountant bị ẩn với Manager |
| accountant_01 | Accountant | NxTest@2026 | ACTIVE | Lập bảng lương, kết thúc chuyến xe, thanh toán đơn, hoà vốn; kiểm `CheckPlacement` (không gắn tổ may) |
| warehouse_01 | WarehouseKeeper | NxTest@2026 | ACTIVE | Nhập kho, xuất kho cho tổ cắt, chọn xe/phương thức giao, kiểm kê |
| qaqc_01 | QaQc | NxTest@2026 | ACTIVE | Ghi nhận hàng ra chuyền và lỗi (REWORK/SCRAP/OUTPUT_ERR) |
| electrician_01 | Electrician | NxTest@2026 | ACTIVE | Xử lý yêu cầu sửa máy (`MachineRepairRequestHandler`) |
| leader_sew_a | TeamLeader | NxTest@2026 | ACTIVE | Tổ trưởng TEAM-SEW-A: phân việc, xác nhận sản lượng tổ mình; không được chuyển sang tổ khác (`TEAM_TRANSFER_LEADER`) |
| leader_sew_b | TeamLeader | NxTest@2026 | ACTIVE | Tổ trưởng TEAM-SEW-B: **IDOR ngang**, không được xem/sửa sản lượng, phân việc của TEAM-SEW-A |
| deputy_sew_a | TeamDeputy | NxTest@2026 | ACTIVE | Tổ phó TEAM-SEW-A: xác nhận thay (ON_BEHALF); chặn chuyển tổ tổ phó |
| worker_sew_a1 | Worker | NxTest@2026 | ACTIVE | Công nhân may (PIECE_RATE) tổ A, có phiếu lương và phiếu giao hàng PENDING; người dùng chính cho test IDOR |
| worker_sew_a2 | Worker | NxTest@2026 | ACTIVE | Công nhân may cùng tổ A: **IDOR ngang**, không được xem payslip/phiếu giao/yêu cầu của worker_sew_a1 và ngược lại |
| worker_sew_b1 | Worker | NxTest@2026 | ACTIVE | Công nhân tổ B: IDOR khác tổ; đối tượng chuyển tổ (Transfer) |
| worker_cut_01 | Worker | NxTest@2026 | ACTIVE | Công nhân tổ cắt (PayType DAILY, tính công theo ngày) |
| worker_locked | Worker | NxTest@2026 | LOCKED | Đăng nhập phải bị từ chối; token cũ không còn dùng được |
| worker_resigned | Worker | NxTest@2026 | RESIGNED | Không đăng nhập được; Resign lần hai trả `InvalidStatus`; không xuất hiện trong danh sách phân việc |

### 1.2 Minimum Domain Data

| **Entity** | **Count** | **Required State** | **Purpose** |
|---|---|---|---|
| Role | 9 | ADMIN, MANAGER, ACCOUNTANT, WAREHOUSE_KEEPER, TEAM_LEADER, TEAM_DEPUTY, QAQC, ELECTRICIAN, WORKER | Đủ danh mục Role cho bảng 1.1 |
| Team | 5 | TEAM-OFFICE: OFFICE/ACTIVE · TEAM-CUT-01: CUTTING/ACTIVE · TEAM-SEW-A: SEWING/ACTIVE (leader_sew_a, deputy_sew_a, worker_sew_a1, worker_sew_a2) · TEAM-SEW-B: SEWING/ACTIVE (leader_sew_b, worker_sew_b1) · TEAM-SEW-X: SEWING/DISCONTINUED (không có NV) | Đủ 3 TeamType và 2 trạng thái; test Transfer, Discontinue/Reactivate, IDOR khác tổ, `CheckPlacement` |
| Employee (PayType) | theo 1.1 | PIECE_RATE: worker_sew_*; DAILY: worker_cut_01; MONTHLY: nhân sự văn phòng | Đủ 3 PayType cho tính lương |
| Customer | 1 | CUST-PARENT: công ty mẹ, ACTIVE | Xưởng con chỉ nhận đơn từ công ty mẹ |
| Material | 3 | MAT-FAB-01: vải, ACTIVE, tồn đủ · MAT-FAB-02: vải, ACTIVE, tồn thấp hơn định mức · MAT-TRIM-OLD: phụ liệu, DISCONTINUED | Xuất/nhập đủ và thiếu hàng (chặn lưu); vật tư ngừng dùng |
| Product | 2 | PRD-001: ACTIVE, 2 màu × 2 size = 4 ProductVariant, component PANEL (PiecesPerGarment = 1 và 99) và TRIM · PRD-002: chưa có ProductMarker (thiếu thông số) | Thông số mã hàng, biên Pieces [1, 99], đơn có/không đủ thông số |
| Operation (công đoạn) | 4 | PRD-001: OP10, OP20 ACTIVE (OP20 là final step) · OP30: DISCONTINUED · OP40: ACTIVE, đơn giá hiệu lực từ T−30 | Final step, chuyển final, công đoạn ngừng, đơn giá theo ngày |
| ProductMarker / Ratio | 2 | ORD-IT-PROD × PRD-001: MarkerNo = 1 và MarkerNo = 20 (biên trên) | Biên `MarkerNo` [1, 20]; lệnh cắt theo sơ đồ |
| CustomerOrder | 8 | ORD-IT-NEW: NEW · ORD-IT-PROD: IN_PRODUCTION · ORD-IT-INDLV: IN_DELIVERY · ORD-IT-PART: PARTIALLY_DELIVERED · ORD-IT-DLV: DELIVERED · ORD-IT-DONE: COMPLETED · ORD-IT-STOP: STOPPED · ORD-IT-CANC: CANCELLED (đều thuộc CUST-PARENT, PRD-001) | Phủ đủ 8 OrderStatus; chặn chuyển trạng thái sai; giảm số lượng dưới mức đã sản xuất (BelowProduced) |
| OrderLine / OrderLineTeam | 4 | ORD-IT-PROD: 4 variant; 2 dòng giao TEAM-SEW-A, 2 dòng giao TEAM-SEW-B | Phân việc theo tổ, IDOR tổ, tiến độ đơn |
| OrderPayment | 2 | ORD-IT-DLV: đã thanh toán một phần · ORD-IT-DONE: đã thanh toán đủ | Lọc State không phân biệt hoa thường; công nợ đơn |
| WorkOrder (lệnh cắt) | 4 | WO-IT-OPEN: OPEN · WO-IT-CUT: CUTTING · WO-IT-DONE: COMPLETED · WO-IT-CANC: CANCELLED (đều thuộc ORD-IT-PROD) | Đủ 4 WorkOrderStatus; lệnh thường chỉ cảnh báo thiếu vải, lệnh cắt bù bị chặn |
| MaterialReceipt (nhập kho) | 3 | GRN-IT-01: PENDING · GRN-IT-02: CONFIRMED (đã post Stock) · GRN-IT-03: RETURNED (có Reason) | Đủ ReceiptStatus; nhập thiếu thì chặn lưu; `usp_Stock_Post` |
| MaterialIssue (xuất kho) | 3 | ISS-IT-01: PENDING_RECEIPT → TEAM-CUT-01 · ISS-IT-02: RECEIVED · ISS-IT-03: REJECTED (có Reason) | Đủ IssueStatus; tồn không đủ thì chặn lưu |
| Stock | 2 | MAT-FAB-01: dư · MAT-FAB-02: thiếu so với ISS-IT-01 | Kiểm tồn khi xuất |
| Stocktake | 3 | STK-IT-01: PENDING_APPROVAL · STK-IT-02: APPROVED · STK-IT-03: RETURNED | Đủ StocktakeStatus |
| Handover (WorkAllocation) | 4 | ORD-IT-PROD, TEAM-SEW-A → worker_sew_a1: PENDING (tạo lúc Now−61 phút) · CONFIRMED (ConfirmMode = WORKER) · REJECTED · CANCELLED | Đủ HandoverStatus; auto-confirm SYSTEM sau 60 phút; xác nhận thay ON_BEHALF |
| WorkAllocation Movement | 2 | HANDOVER và RETRIEVAL (RetrievalReason = END_OF_DAY) cho worker_sew_a2 | Đủ MovementType; settle khi rời tổ |
| OutputRecord | 2 | worker_sew_a1, OP10, ngày T: SAVED · ngày thuộc PREV: LOCKED | Sửa được bản SAVED; chặn bản LOCKED/kỳ đã khoá |
| DefectRecord | 4 | REWORK/RECORDED · SCRAP/CONFIRMED · OUTPUT_ERR/CANCELLED · 1 bản LOCKED (DB-only, thuộc PREV) | Đủ DefectType và DefectStatus |
| PieceRate | 4 | DRAFT · PENDING_APPROVAL · APPROVED · REJECTED (theo OP10/OP20) | Đủ PieceRateStatus; luồng duyệt đơn giá |
| AccountingPeriod | 2 | CUR: OPEN · PREV: LOCKED | Chặn ghi Production/Attendance/MonthlyCost/Payroll vào kỳ đã khoá |
| Payroll / Payslip | 4 | PREV: APPROVED (có Payslip của worker_sew_a1 và worker_sew_a2) · CUR: DRAFT · 1 bản PENDING_APPROVAL · 1 bản REJECTED | Đủ PayrollStatus; IDOR payslip giữa a1 và a2; chặn duyệt lại |
| MonthlyCost (Break-even) | 2 | CUR: đủ chi phí cho basis OUTPUT và LABOUR_HOURS · PREV: thiếu chi phí | Hoà vốn 2 basis; `BreakEvenNotReady` |
| Attendance / RJ Sync | 2 | worker_sew_a1: ngày T−1 thiếu 1 lần chấm (MissingPunch) · SyncState: lần sync cuối vượt ngưỡng `ISystemSettingReader` | Cảnh báo thiếu chấm công; job `AlertSyncOverdueAsync` |
| Request | 7 | Lần lượt PENDING_APPROVAL, PENDING_PROCESSING, APPROVED, REJECTED, COMPLETED, NOT_COMPLETED, CANCELLED (có 1 yêu cầu sửa máy giao electrician_01; worker_sew_a1 tạo ít nhất 1 yêu cầu) | Đủ RequestStatus; IDOR yêu cầu giữa các worker |
| PurchaseOrder | 2 | PO-IT-01: PENDING_FINALISATION · PO-IT-02: UNPAID | Phủ các trạng thái đã biết (bổ sung theo `PurchaseOrderStatuses` thực tế) |
| Vehicle | 2 | VEH-01: đang có chuyến · VEH-02: rảnh | Lock xe chống double-complete; StartTrip trên xe rảnh |
| VehicleTrip | 2 | TRIP-IT-01: STARTED (VEH-01, gắn ISS-IT-02, "công ty giao") · TRIP-IT-02: COMPLETED (có FuelCost và FuelReceipt `IsCurrent`) | Complete chỉ khi STARTED; OdometerEnd > Start; vượt định mức nhiên liệu |
| SystemSetting | 2 | Định mức nhiên liệu (lít/km) · Ngưỡng sync chấm công quá hạn | Cấu hình cho cảnh báo over-norm và sync overdue |
