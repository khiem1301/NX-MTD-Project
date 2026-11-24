# UT_SUMMARY_AND_CONSTRAINTS

> Tài liệu tổng hợp **chỉ từ đọc tĩnh** (không chạy lại `dotnet test` / `npm test`).
> Nguồn chính: `Report-5.1_Unit Test.xlsx` (data_only sau recalc), `tests/Application.Tests`, `be/Domain`, `be/NX-MTD.csproj`, `fe/package.json`, `docs/NX-MTD_Database_Mo_ta.md`.
> Ngày đối chiếu Excel: Cover v3.2 / Test Statistics Notes v3.2 Round 2 after fix.

## 1. THỐNG KÊ CHÍNH XÁC KẾT QUẢ UNIT TEST (II.3 Test Reports)

### 1.1 Tổng hợp Round 1 / Round 2 (toàn workbook 20 sheet TC)

| Metric | Round 1 | Round 2 |
|---|---:|---:|
| **Total Test Cases** | **974** | **974** |
| **Passed** | **833** | **974** |
| **Failed** | **141** | **0** |
| **Pending / Untested** | **0** | **0** |
| **Pass rate** | **85.52%** | **100.00%** |

Ghi chú:
- Cột Round trên mỗi sheet TC: Round 1 = lần chạy đầu (giữ lịch sử fail); Round 2 = retest sau fix → **974 Passed / 0 Failed**.
- Test Statistics (active) trỏ Round 2: Sub total **974 / 0 / 974**, coverage & successful coverage **100%**.
- Bộ HighComplexity UTCID01–511: Round 1 từng có Group B fail; Round 2 đã Passed toàn bộ trên sheet tương ứng.
- Bộ rebaseline UTCID533–974: Round 1 từng fail assert-lock; Round 2 Passed sau fix production.

### 1.2 Chi tiết theo sheet

| Sheet | Total | R1 Passed | R1 Failed | R2 Passed | R2 Failed |
|---|---:|---:|---:|---:|---:|
| OperationService | 70 | 54 | 16 | 70 | 0 |
| MaterialReceiptService | 64 | 56 | 8 | 64 | 0 |
| PayrollService | 69 | 62 | 7 | 69 | 0 |
| OutputService | 48 | 42 | 6 | 48 | 0 |
| MaterialIssueService | 40 | 33 | 7 | 40 | 0 |
| ProductSpecService | 41 | 34 | 7 | 41 | 0 |
| WorkOrderService | 59 | 49 | 10 | 59 | 0 |
| DefectService | 42 | 36 | 6 | 42 | 0 |
| CustomerOrderService | 54 | 44 | 10 | 54 | 0 |
| DailyWorkAllocationService | 59 | 52 | 7 | 59 | 0 |
| VehicleService | 41 | 39 | 2 | 41 | 0 |
| FinanceService | 72 | 64 | 8 | 72 | 0 |
| OrderPaymentService | 90 | 86 | 4 | 90 | 0 |
| SpecImportBuilder | 37 | 33 | 4 | 37 | 0 |
| SpecImportService | 33 | 27 | 6 | 33 | 0 |
| EmployeeService | 40 | 30 | 10 | 40 | 0 |
| TeamService | 60 | 47 | 13 | 60 | 0 |
| AttendanceService | 12 | 11 | 1 | 12 | 0 |
| MachineRepairRequestHandler | 14 | 13 | 1 | 14 | 0 |
| HomeDashboardService | 29 | 21 | 8 | 29 | 0 |
| **Sub total** | **974** | **833** | **141** | **974** | **0** |

### 1.3 Defect IDs ghi nhận ở Round 1 (cột Round 1 = Failed)

Mỗi UTCID Failed ở Round 1 được coi là một Defect ID (trùng mã Test Case). Round 2 đã Passed → defect đã đóng trên báo cáo.

| Defect ID | Module/Service | Nguyên nhân gốc rễ (từ Expected / ⚠ trên sheet) | Severity |
|---|---|---|---|
| UTCID08 | OperationService | Code hiện tại trả Success với list rỗng (#8) | Major |
| UTCID14 | OperationService | Failure (Validation), không gọi SaveChanges | Major |
| UTCID15 | OperationService | Failure (Validation) | Major |
| UTCID16 | OperationService | Service không check, phụ thuộc validator (#5) | Major |
| UTCID17 | OperationService | (#5) | Major |
| UTCID20 | OperationService | Code hiện tại ném `NullReferenceException` tại `.Trim()` (#6) | Critical |
| UTCID23 | OperationService | Failure (DuplicateSequence) | Major |
| UTCID24 | OperationService | Rollback được gọi; không Commit; exception propagate (hoặc Failure). Final step cũ giữ nguyên | Critical |
| UTCID29 | OperationService | Không MoveFinalStep (chỉ chạy khi Active); OP20 vẫn final | Minor |
| UTCID33 | OperationService | NRE tại `.Trim()` (#6) | Critical |
| UTCID41 | OperationService | Code hiện tại Success → mã hàng mất final step (#1) | Major |
| UTCID50 | OperationService | 2 dòng Action=Error, lỗi trùng STT | Minor |
| UTCID54 | OperationService | Cast `(short)` tràn số âm (#9) | Major |
| UTCID57 | OperationService | Code hiện tại ném `ArgumentOutOfRangeException` tại `sheets[0]` (#7) | Major |
| UTCID66 | OperationService | Rate dùng ngày mặc định (Today) | Minor |
| UTCID68 | OperationService | Failure (FinalStep); không SaveChanges | Minor |
| UTCID89 | MaterialReceiptService | Failure (Validation) | Major |
| UTCID100 | MaterialReceiptService | Failure (Validation) | Major |
| UTCID110 | MaterialReceiptService | Failure (ReceiptDate invalid) | Minor |
| UTCID120 | MaterialReceiptService | Hiện tại Success, Stock không gọi nhưng vẫn đếm lần giao / có thể kích SHORT_DELIVERY (#3) | Major |
| UTCID121 | MaterialReceiptService | Message "còn -20" (#6) | Major |
| UTCID126 | MaterialReceiptService | Service không check, lỗi FK muộn (#1) | Major |
| UTCID131 | MaterialReceiptService | Failure (Validation) | Major |
| UTCID132 | MaterialReceiptService | NRE tại `dto.Reason.Trim()` (#8) | Critical |
| UTCID141 | PayrollService | Failure `PeriodMonthInvalid` (không exception) | Minor |
| UTCID149 | PayrollService | Hiện tại Valid (#2) | Major |
| UTCID181 | PayrollService | Hiện tại trả list rỗng (#5) | Major |
| UTCID186 | PayrollService | Hiện tại vẫn Approve (#8) | Major |
| UTCID190 | PayrollService | Service không check (#9) | Major |
| UTCID194 | PayrollService | (#9) | Major |
| UTCID199 | PayrollService | Failure (Validation) hoặc list rỗng; không exception | Major |
| UTCID215 | OutputService | `KeyNotFoundException` tại `operations[k.OperationId]` (#7) | Major |
| UTCID217 | OutputService | Dòng Quantity=0 | Minor |
| UTCID218 | OutputService | Hiện tại `FirstOrDefault` (#5) | Major |
| UTCID247 | OutputService | Failure (Concurrency) | Critical |
| UTCID249 | OutputService | Failure (Busy/LockTimeout); không SaveChanges | Minor |
| UTCID250 | OutputService | Failure (CapExceeded) đã map; Rollback (#2) | Major |
| UTCID265 | MaterialIssueService | Failure (Validation) | Major |
| UTCID271 | MaterialIssueService | Failure (OperationsNotAssigned) | Minor |
| UTCID272 | MaterialIssueService | `lineIds` rỗng → pass check phân công (#5) | Major |
| UTCID278 | MaterialIssueService | Hiện tại Received và StartProduction (#1) | Major |
| UTCID287 | MaterialIssueService | Failure (Validation) | Major |
| UTCID288 | MaterialIssueService | NRE tại `.Trim()` (#7) | Critical |
| UTCID291 | MaterialIssueService | `NotifyEmployees` không await (#7) | Major |
| UTCID299 | ProductSpecService | Component 12 Status=Discontinued (soft), không xóa cứng | Minor |
| UTCID301 | ProductSpecService | Failure (Validation: Pieces ∈ [1,99]); BeginTransaction không gọi | Major |
| UTCID303 | ProductSpecService | Failure (VariantNotInOrder); không ghi | Minor |
| UTCID305 | ProductSpecService | Hiện tại không kiểm `spec.IsSuccess`, có thể Success với Data null (#6) | Major |
| UTCID314 | ProductSpecService | `KeyNotFoundException` trong RemoveStaleOperationLinksAsync (#7) | Major |
| UTCID548 | ProductSpecService | Báo lỗi trùng hoặc chọn đúng Id; không gán nhầm im lặng (rủi ro `First()`) | Minor |
| UTCID550 | ProductSpecService | `NullReferenceException` | Critical |
| UTCID317 | WorkOrderService | So raw string → rỗng (#8) | Major |
| UTCID328 | WorkOrderService | Hiện tại bỏ im lặng, trả list ngắn hơn (#1) | Major |
| UTCID329 | WorkOrderService | Failure `NotFound` | Major |
| UTCID334 | WorkOrderService | F2 không có trong kết quả (chỉ NeedMeters > 0) | Minor |
| UTCID344 | WorkOrderService | Service không check (#4) | Major |
| UTCID345 | WorkOrderService | Failure (Validation) (#4) | Major |
| UTCID346 | WorkOrderService | Failure (Validation) | Major |
| UTCID347 | WorkOrderService | Failure (FabricShortage); không tạo lệnh | Minor |
| UTCID354 | WorkOrderService | Success; request 43 bị bỏ qua hoặc trả field null; không exception | Minor |
| UTCID371 | WorkOrderService | Failure (đã đủ, không phải đóng thiếu) | Minor |
| UTCID382 | DefectService | Hiện tại `Take(MaxPageSize)` cắt cụt, không báo total (#4) | Major |
| UTCID383 | DefectService | Không validate (#4) | Major |
| UTCID388 | DefectService | Không có MaxQty (#1) | Major |
| UTCID396 | DefectService | Hiện tại không check (#7) | Major |
| UTCID397 | DefectService | `FirstAsync` ném `InvalidOperationException` (#8) | Major |
| UTCID398 | DefectService | Không await (#6) | Major |
| UTCID436 | CustomerOrderService | Failure (Validation) | Major |
| UTCID438 | CustomerOrderService | Failure (Validation) | Major |
| UTCID439 | CustomerOrderService | Service không check tường minh (#6) | Major |
| UTCID443 | CustomerOrderService | Hiện tại bỏ qua im lặng (#3) | Major |
| UTCID448 | CustomerOrderService | Failure (BelowProduced) (#2) | Minor |
| UTCID450 | CustomerOrderService | Failure (Concurrency) | Critical |
| UTCID454 | CustomerOrderService | Failure (ReasonRequired) | Critical |
| UTCID456 | CustomerOrderService | Failure (Concurrency) | Critical |
| UTCID458 | CustomerOrderService | `GetByIdAsync` không truyền ct (#7) | Major |
| UTCID461 | CustomerOrderService | Failure, hoặc hành vi được định nghĩa rõ; không Stop bất thường (#5) | Minor |
| UTCID476 | DailyWorkAllocationService | Hiện tại không validate (#1) | Major |
| UTCID477 | DailyWorkAllocationService | Không validate (#1) | Major |
| UTCID489 | DailyWorkAllocationService | Cần xác nhận ValidateAsync có chặn (#6) | Major |
| UTCID490 | DailyWorkAllocationService | Nguy cơ delta kép (#6) | Major |
| UTCID500 | DailyWorkAllocationService | Failure (Busy); không SaveChanges | Minor |
| UTCID501 | DailyWorkAllocationService | `NotifyWorkers` không await (#5) | Major |
| UTCID567 | DailyWorkAllocationService | `ArgumentNullException` hoặc coi như rỗng (Success) | Minor |
| UTCID586 | VehicleService | Audit after không ghi FuelCost (rủi ro #5) | Major |
| UTCID587 | VehicleService | Complete không lock xe (rủi ro #6) | Critical |
| UTCID603 | FinanceService | Không null-check (rủi ro #5) | Major |
| UTCID616 | FinanceService | P3: Surplus = −2.000.000, IsAtRisk=true ở cả 2 basis; AtRiskWarnings chỉ chứa P3 **1 lần** (Distinct) | Minor |
| UTCID626 | FinanceService | Hiện bị bỏ qua → thặng dư ảo (rủi ro #1) | Major |
| UTCID628 | FinanceService | Failure `BreakEvenPeriodMonthInvalid` | Minor |
| UTCID643 | FinanceService | Failure `BreakEvenPeriodMonthInvalid` | Minor |
| UTCID644 | FinanceService | Inputs báo CanCalculate=true trong khi Calculate trả `BreakEvenNotReady` (rủi ro #1) | Major |
| UTCID650 | FinanceService | Không có sort ổn định | Major |
| UTCID654 | FinanceService | `NullReferenceException` trong ToDto | Critical |
| UTCID693 | OrderPaymentService | Hiện trả list rỗng | Major |
| UTCID702 | OrderPaymentService | `NullReferenceException` | Critical |
| UTCID718 | OrderPaymentService | `ArgumentNullException` hoặc Failure (Validation) | Major |
| UTCID731 | OrderPaymentService | Failure (Validation) | Major |
| UTCID761 | SpecImportBuilder | Hợp lệ | Minor |
| UTCID765 | SpecImportBuilder | Bị strip như color-only | Major |
| UTCID770 | SpecImportBuilder | Order markers không có cơ chế usage-keep → bị xóa | Major |
| UTCID786 | SpecImportBuilder | `ArgumentNullException` | Minor |
| UTCID793 | SpecImportService | Parts case-sensitive → bỏ sheet | Major |
| UTCID790 | SpecImportService | `ArgumentNullException` hoặc Failure | Minor |
| UTCID802 | SpecImportService | `ArgumentNullException` hoặc Failure; BeginTransaction không gọi | Minor |
| UTCID815 | SpecImportService | MarkAssumed không cập nhật CanImport | Major |
| UTCID816 | SpecImportService | Luôn set true | Major |
| UTCID819 | SpecImportService | `ArgumentNullException` hoặc Failure | Minor |
| UTCID830 | EmployeeService | Failure (Validation) | Major |
| UTCID831 | EmployeeService | Failure (Validation) | Major |
| UTCID833 | EmployeeService | Failure (CheckPlacement) | Minor |
| UTCID834 | EmployeeService | Failure (Duplicate) | Major |
| UTCID836 | EmployeeService | `ArgumentNullException` hoặc Failure | Minor |
| UTCID840 | EmployeeService | Failure (CheckPlacement) | Minor |
| UTCID849 | EmployeeService | Failure (CheckPlacement) | Minor |
| UTCID850 | EmployeeService | Hiện cho phép (rủi ro #4) | Major |
| UTCID853 | EmployeeService | Failure (scope: Forbidden/NotFound) | Major |
| UTCID857 | EmployeeService | Hiện không settle, vẫn giữ TeamId (rủi ro #3, gap nghiêm trọng) | Major |
| UTCID860 | TeamService | Success; 2 tổ may | Minor |
| UTCID862 | TeamService | Failure (Validation) hoặc list rỗng; không exception | Major |
| UTCID867 | TeamService | Success; Name 101 ký tự → Failure (Validation) | Major |
| UTCID869 | TeamService | Failure (Validation) | Major |
| UTCID883 | TeamService | Success; trong 1 TX: Settle(A,[E1,E2]) 1 lần, Settle(C,[E3]) 1 lần; TeamId=B cho cả 3; stamp + Audit + LogTransfer; Message có suffix "đã quyết toán việc tổ may" | Major |
| UTCID889 | TeamService | Failure (Validation) | Major |
| UTCID893 | TeamService | Hiện trả NotFound | Major |
| UTCID898 | TeamService | Success; trong TX: Settle(A,[E1,E2]) rồi TeamId=null; Message có suffix tổ may | Major |
| UTCID900 | TeamService | Failure (Validation) | Major |
| UTCID906 | TeamService | Failure (Validation) | Major |
| UTCID912 | TeamService | Failure `NotFound` | Major |
| UTCID916 | TeamService | Failure `NotFound` | Major |
| UTCID919 | TeamService | Failure `NotFound` | Major |
| UTCID920 | AttendanceService | NotifyRole(Admin) 1 lần; SaveChanges 1 lần; LogWarning "Attendance sync overdue" | Minor |
| UTCID938 | MachineRepairRequestHandler | `ArgumentNullException` hoặc Failure | Minor |
| UTCID948 | HomeDashboardService | Lệch casing → đếm 0 | Major |
| UTCID949 | HomeDashboardService | Không exception; Revenue=0 (fallback); nên có log lỗi | Minor |
| UTCID952 | HomeDashboardService | Inbox = [] ; phần khác vẫn có dữ liệu | Minor |
| UTCID955 | HomeDashboardService | MissingPunch=0 (fallback); không exception | Minor |
| UTCID958 | HomeDashboardService | Incoming = (0, []) ; không exception | Minor |
| UTCID965 | HomeDashboardService | OnLineQty=0 (fallback); không exception | Minor |
| UTCID969 | HomeDashboardService | Ticket Closed không được trả; khi repo lỗi → list rỗng (fallback), không exception | Minor |
| UTCID974 | HomeDashboardService | Payslip=null (fallback); không exception | Minor |

**Tổng Defect Round 1:** 141 (khớp tổng Failed Round 1 = 141).

## 2. RÀNG BUỘC KỸ THUẬT & GIẢ ĐỊNH KIỂM THỬ (I.1 Scope & Constraints)

### 2.1 Thành phần bị cô lập / giả lập (Mock / Stub)

Tầng Unit Test (`tests/Application.Tests`) dùng **NSubstitute** thay thế phụ thuộc; **không** kết nối SQL Server thật, **không** gọi HTTP/FE, **không** đụng máy chấm công RJ thật.

| Nhóm | Interface / thành phần mock điển hình | Mục đích |
|---|---|---|
| Persistence | `IUnitOfWork`, `I*Repository`, `IGenericRepository<T>` | Cô lập DB / EF Core |
| Thời gian | `IClock` (`Today`/`Now` cố định, thường `2026-11-24`) | Deterministic boundary theo ngày |
| Người dùng | `ICurrentUser` (Role, EmployeeId, AccountId) | Phân quyền / scope |
| Thông báo | `INotificationService` | Không gửi mail/push thật |
| Audit | `IAuditLogService` | Ghi spy thay vì ghi DB |
| Khóa phân tán | `IAppLock` | Giả lập timeout / success |
| Cấu hình | `ISystemSettingReader` | Định mức nhiên liệu, ngưỡng sync… |
| Excel I/O | Parser/Writer stub; workbook in-memory | Không đọc file disk thật (trừ fixture ClosedXML khi cần) |
| Bảo mật | `IPasswordHasher`, `ITemporaryPasswordGenerator` | Không BCrypt thật / không sinh entropy ngoài test |
| Dịch vụ chéo | `IDailyWorkAllocationService.SettleOnTeamLeaveAsync`, `IOrderProgressService`, `ICostSummaryService`, … | Cô lập side-effect giữa service |
| File store | `IFileStore` / attachment stubs | Không ghi blob thật |

### 2.2 Phạm vi đã bao phủ bởi Unit Test

**Trong phạm vi Report-5.1 (20 sheet TC / 974 case):** Application Services & pure builders:

- Production/Warehouse/Orders HC: `OperationService`, `MaterialReceiptService`, `PayrollService`, `OutputService`, `MaterialIssueService`, `ProductSpecService`, `WorkOrderService`, `DefectService`, `CustomerOrderService`, `DailyWorkAllocationService`
- Vehicle: `VehicleService` (GetLiveStatus / StartTrip / CompleteTrip)
- Rebaseline: `FinanceService`, `OrderPaymentService`, `SpecImportBuilder`, `SpecImportService`, `EmployeeService`, `TeamService`, `AttendanceService.AlertSyncOverdueAsync`, `MachineRepairRequestHandler`/`NoLineRequestHandler`, `HomeDashboardService`

Ngoài Report-5.1, project còn nhiều test class khác dưới `tests/Application.Tests` (Auth, Meal, KPI, Request, …) — **không** nằm trong 974 TC của Report-5.1.

### 2.3 Ngoài phạm vi Unit Test

| Tầng | Lý do loại trừ khỏi UT Report-5.1 |
|---|---|
| API Controllers | Binding, filter, `[Authorize]`, HTTP status — thuộc integration/API test |
| Middleware / JWT pipeline | Auth scheme, CORS, exception middleware |
| EF Core DbContext ↔ SQL Server | Trigger, CHECK, transaction isolation thật |
| Frontend (React/Ant Design) | `fe/` — Jest riêng, không vào Report-5.1 |
| RJ Sync Agent / phần cứng máy chấm công | Process ngoài; UT chỉ mock snapshot/repo |
| CI job HighComplexity | `Category=HighComplexity` informational (`continue-on-error`); gate merge là `Category!=HighComplexity` |

## 3. BỘ LUẬT VALIDATION & RÀNG BUỘC NGHIỆP VỤ ĐÃ KIỂM CHỨNG (I.2.1 Testing Types)

Tổng hợp từ các case **Abnormal / Boundary** trong Report-5.1 (không liệt kê từng UTCID):

### 3.1 Null / rỗng / thiếu tham số
- DTO / query / workbook / `workerIds` / draft null → `ArgumentNullException` hoặc `Result` Validation/Failure (không để `NullReferenceException` thoát ra API-facing path).
- Chuỗi bắt buộc rỗng (`FullName`, `TeamCode`, tên component, …) → Validation.
- Danh sách `EmployeeIds` rỗng khi Transfer/Add/Remove → Validation.

### 3.2 Độ dài chuỗi
- `FullName` / `TeamCode` / tên component: max **100**; 101 → Validation.
- Spec import Note: max **200**.
- Marker component name: max **100**; Color max **50**.

### 3.3 Số học / biên số
- `PiecesPerGarment` ∈ **[1, 99]** (0 / 100 / âm → Validation).
- `MarkerNo` ∈ **[1, 20]** (product decision tránh phình; 21+ → Error/Validation). `MaxMarkerRows = MaxMarkerNo × MaxVariants`.
- OdometerEnd phải **>** OdometerStart; FuelCost nguyên dương trong biên validator; FuelLiters so ngưỡng định mức → cảnh báo over-norm.
- `FuelCost` có giá trị → bắt buộc FuelReceipt `IsCurrent`.

### 3.4 Trùng lặp
- Email/username account trùng khi Create employee → Duplicate.
- Team code trùng → Duplicate.
- Variant / ProductColor trùng tên; duplicate EmployeeIds trong transfer → Validation (hoặc dedupe theo spec).
- Norm / component / marker group trùng trong SpecImport → Action=Error.

### 3.5 Placement / phân quyền / scope
- Accountant không gắn tổ may; Worker không gắn tổ Office (`CheckPlacement`).
- Không chuyển tổ trưởng/phó sang tổ khác (`TEAM_TRANSFER_LEADER`).
- Manager không sửa NV ngoài phạm vi tổ/xưởng → NotFound/Forbidden.
- Role office-creatable vs floor-from-RJ khi Create/Import.

### 3.6 Chặn chuyển trạng thái sai
- Trip: chỉ Complete khi `STARTED`; Planned/Completed → InvalidStatus; Complete có lock xe chống double-complete.
- Resign chỉ khi Active; đã Resigned → InvalidStatus.
- Discontinue/Reactivate Team theo Active/Discontinued.
- Order/WorkOrder/Receipt/Issue/Payroll: các luồng HC kiểm InvalidStatus / period locked / edit window.
- DailyWorkAllocation Settle: TeamOrderBusy khi lock timeout; workerIds null an toàn.
- ProductSpec Write: unknown color / panel 2 vải / duplicate color → Validation (không crash KeyNotFound).

### 3.7 Nhất quán tính toán / fallback
- Finance: period month null → `BreakEvenPeriodMonthInvalid`; sort ổn định; Distinct at-risk; phân bổ biến phí chung; null Product an toàn.
- HomeDashboard: exception khối phụ → fallback 0/`[]`/null, không làm đổ cả DTO.
- OrderPayment: filter State không phân biệt hoa thường; Customer null → CustomerName null.

## 4. CẤU TRÚC DỮ LIỆU MỒI & PHÂN QUYỀN (II.1 Test Data)

### 4.1 Roles (token / `Roles.cs`)

| Role (Pascal) | RoleCode (DB) |
|---|---|
| Admin | ADMIN |
| Manager | MANAGER |
| Accountant | ACCOUNTANT |
| WarehouseKeeper | WAREHOUSE_KEEPER |
| TeamLeader | TEAM_LEADER |
| TeamDeputy | TEAM_DEPUTY |
| QaQc | QAQC |
| Electrician | ELECTRICIAN |
| Worker | WORKER |

- Office creatable: Admin, Manager, Accountant, WarehouseKeeper, QaQc, Electrician.
- Floor from RJ: Worker, TeamLeader, TeamDeputy.
- Hidden from Manager: Admin, Accountant.

### 4.2 Account / Employee / Record status

`RecordStatus` (`be/Domain/Constants/RecordStatus.cs`):
- `ACTIVE`, `LOCKED`, `RESIGNED`, `DISCONTINUED`, `OPEN` (AccountingPeriod chưa khóa).
- `Account.Status` / `Employee.Status` dùng các mã Active/Resigned/Locked tương ứng (Account 1–1 Employee).
- PayTypes: `PIECE_RATE`, `DAILY`, `MONTHLY`.
- TeamTypes: `OFFICE`, `CUTTING`, `SEWING`.

### 4.3 Enum / status constants theo Entity (rút gọn — nguồn `Domain/Constants`)

| Nhóm | Giá trị |
|---|---|
| OrderStatuses | NEW, IN_PRODUCTION, IN_DELIVERY, PARTIALLY_DELIVERED, CANCELLED, STOPPED, DELIVERED, COMPLETED |
| ReceiptStatuses | PENDING, CONFIRMED, RETURNED |
| IssueStatuses | PENDING_RECEIPT, RECEIVED, REJECTED |
| WorkOrderStatuses | OPEN, CUTTING, COMPLETED, CANCELLED |
| HandoverStatuses | PENDING, CONFIRMED, REJECTED, CANCELLED |
| TripStatuses / VehicleTripStatus | STARTED, COMPLETED |
| StocktakeStatuses | PENDING_APPROVAL, APPROVED, RETURNED |
| OperationStatuses | ACTIVE, DISCONTINUED |
| PieceRateStatuses | DRAFT, PENDING_APPROVAL, APPROVED, REJECTED |
| DefectStatuses | RECORDED, CONFIRMED, CANCELLED (+ LOCKED DB-only) |
| DefectTypes | REWORK, SCRAP, OUTPUT_ERR |
| OutputStatuses | SAVED, LOCKED |
| PayrollStatus | DRAFT, PENDING_APPROVAL, APPROVED, REJECTED |
| RequestStatuses | PENDING_APPROVAL, PENDING_PROCESSING, APPROVED, REJECTED, COMPLETED, NOT_COMPLETED, CANCELLED |
| PurchaseOrderStatuses | PENDING_FINALISATION, UNPAID, … |
| AllocationBasis (BreakEven) | OUTPUT, LABOUR_HOURS |
| ComponentTypes | PANEL, TRIM |
| MaterialStatuses | ACTIVE, DISCONTINUED |
| WorkAllocation MovementType | HANDOVER, RETRIEVAL |
| ConfirmModes | WORKER, ON_BEHALF, LEADER, SYSTEM |
| RetrievalReasons | OPERATION_CHANGED, WORKER_ABSENT, STYLE_FINISHED, END_OF_DAY, ADJUSTED |

### 4.4 Cây phụ thuộc khóa ngoại lõi (cha → con)

Rút từ `docs/NX-MTD_Database_Mo_ta.md` + navigation Entity:

```
Role → Employee → Account
Team → Employee
Customer → CustomerOrder → OrderLine → (OrderLineTeam, WorkOrderLine, OutputRecord, DefectRecord, …)
Product → ProductColor → ProductVariant
Product → ProductComponent → ProductComponentMaterial → Material
Product → ProductMarkerComponent / ProductMarkerFabric (MarkerNo)
CustomerOrder × Product → ProductMarker (+ ProductMarkerRatio)
CustomerOrder → OrderPayment / OrderMaterial
WorkOrder → WorkOrderLine → CuttingLay / MaterialIssue…
MaterialReceipt → MaterialReceiptLine → Stock (via usp_Stock_Post)
MaterialIssue → MaterialIssueLine
Request → RequestLine
Payroll → Payslip → PayslipItem
Vehicle → VehicleTrip
AccountingPeriod → (khóa ghi Production / Attendance / MonthlyCost / Payroll theo tháng)
```

### 4.5 Dữ liệu mồi trong UT (điển hình)

- Clock cố định: `Today = 2026-11-24`, `Now = 2026-11-24 08:00/10:00 +07`.
- Seed in-memory: Product/Variant/Norm, Team Sewing/Office, Employee Active/Resigned, Trip Started, Order Running, … theo từng UTCID.
- Không dùng connection string production trong UT.

## 5. THÔNG SỐ MÔI TRƯỜNG & CÔNG CỤ (I.2.3 & I.3.1)

| Hạng mục | Tên | Version (từ file cấu hình) |
|---|---|---|
| Ngôn ngữ / Runtime (Backend + UT) | .NET / C# | **net9.0** (`be/NX-MTD.csproj`, `tests/Application.Tests/Application.Tests.csproj`) |
| Framework Backend | ASP.NET Core (Sdk Web) + EF Core SqlServer | **9.0.x** (`Microsoft.EntityFrameworkCore.SqlServer` **9.0.10**; JwtBearer **9.***) |
| Database Engine | Microsoft SQL Server (qua EF Core) | Driver/package **9.0.10**; DB script NXMTD (SQL Server) theo `docs/NX-MTD_Database_Mo_ta.md` |
| Framework Frontend | React + Vite + TypeScript + Ant Design | React **18.3.1**, Vite **8.3.4**, TypeScript **5.9**, antd **5.29.3**; Node engines **>=24** (`fe/package.json`) |
| Unit Test Framework (BE) | xUnit | **2.9.2** (+ `Microsoft.NET.Test.Sdk` **17.12.0**, runner VS **2.8.2**) |
| Mocking | NSubstitute | **6.2.0** |
| Assertion | FluentAssertions | **7.2.0** |
| Coverage collector | coverlet.collector | **6.0.2** |
| FE Unit Test | Jest + Testing Library | Jest **29.7.0**, `@testing-library/react` **16.3.3** (ngoài Report-5.1) |
| Validation (prod) | FluentValidation | **12.2.0** |
| Excel (prod/test support) | ClosedXML | **0.102.3** |

### 5.1 CI liên quan

- Workflow: `.github/workflows/ci.yml`
- Gate: `dotnet test --filter Category!=HighComplexity`
- Informational: `Category=HighComplexity` (`continue-on-error: true`)

---

*Generated statically from Excel dump + project manifests. Do not treat as a fresh test execution log.*
