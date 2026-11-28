# Report-5.1_Unit Test

_Nguồn: `Report-5.1_Unit Test.xlsx` — trích xuất tự động sang Markdown._

## Mục lục

1. [Cover](#cover)
2. [Test Cases](#test-cases)
3. [Test Statistics](#test-statistics)
4. [OperationService](#operationservice)
5. [MaterialReceiptService](#materialreceiptservice)
6. [PayrollService](#payrollservice)
7. [OutputService](#outputservice)
8. [MaterialIssueService](#materialissueservice)
9. [ProductSpecService](#productspecservice)
10. [WorkOrderService](#workorderservice)
11. [DefectService](#defectservice)
12. [CustomerOrderService](#customerorderservice)
13. [DailyWorkAllocationService](#dailyworkallocationservice)
14. [VehicleService](#vehicleservice)
15. [FinanceService](#financeservice)
16. [OrderPaymentService](#orderpaymentservice)
17. [SpecImportBuilder](#specimportbuilder)
18. [SpecImportService](#specimportservice)
19. [EmployeeService](#employeeservice)
20. [TeamService](#teamservice)
21. [AttendanceService](#attendanceservice)
22. [MachineRepairRequestHandler](#machinerepairrequesthandler)
23. [HomeDashboardService](#homedashboardservice)

## Cover

- ****: UNIT TEST DOCUMENT

- **Project Name**: NX-MTD (Nội thất May – Quản lý sản xuất) | Creator | khiemnxhe194682
- **Project Code**: SEP490_NX-MTD | Issue Date | 2026-11-22 00:00:00
- **Document Code**: SEP490_NX-MTD_XXX_vx.x | 1.0


**Record of change**
- **Effective Date**: Version | Change Item | *A,D,M | Change description | Reference
- **2026-11-22 00:00:00**: 2.0 | Unit Test Round 1 — 10 high-complexity services (after QA noise filter) | A | Round 1b complete cho 10 service HighComplexity (UTCID01–501 baseline). Group B bugs giữ FAIL; Round 2/3 Pending. | TestResults/*_Round1b
- **2026-11-22 00:00:00**: 2.1 | Unit Test Round 1 — DailyWorkAllocation DEV-ALL-157 | M | Cập nhật expected UTCID479–481 + thêm UTCID502–511 (42 TC). Group A (0); Group B (6) kept. | DailyWorkAllocationServiceHighComplexity_Round1b
- **2026-11-22 00:00:00**: 2.2 | Unit Test Phụ lục A — VehicleService (DEV-ALL-158/159) | A | Thêm suite thường UTCID512–532 (21 TC): GetLiveStatus + StartTrip/AppLock. Không thuộc shortlist HighComplexity Round 1 (vẫn 511). | VehicleService_UTCID512_532
- **2026-11-23 00:00:00**: 3.0 | Unit Test Round 2 | M | Re-test 10 high-complexity services sau fix Round 1 Failed: 511 Passed / 0 Failed / 511 TC (UTCID01–511). Op 70, MR 64, Payroll 69, Output 48, MaterialIssue 40, ProductSpec 23, WorkOrder 59, Defect 42, CustomerOrder 54, DailyWorkAllocation 42. | TestResults/*HighComplexity_Round2
- **2026-11-24 00:00:00**: 3.1 | Unit Test Re-baseline fa1e8b8 → 85d50fd | A, M | M: Round3 ProductSpec UTCID292/294/296/301/313. A: UTCID533–974 (442 TC) + 9 sheet mới. Cũ→Round3; mới→Round1. Fail giữ Assert-locking. | TestResults/Rebaseline_v3.1_Round
- **2026-11-24 00:00:00**: 3.2 | Unit Test Re-baseline Round 2 (after fix) | M | Retest sau fix 61 fail v3.1: case mới UTCID533–974 → Round 2 Passed; ProductSpec 5 case sửa → Round 3 Passed. 447/447 UTCID pass. | TestResults/Rebaseline_v3.1_Round2


---

## Test Cases

- ****: TEST CASE LIST

- ****: Project Name | NX-MTD (Nội thất May – Quản lý sản xuất)
- ****: Project Code | SEP490_NX-MTD
- ****: Test Environment Setup Description | Unit test môi trường:<br>1. .NET 9 / xUnit / NSubstitute / FluentAssertions<br>2. Project tests/Application.Tests<br>3. Không cần SQL Server (mock IUnitOfWork + AsTestAsyncQueryable)<br>4. Clock cố định: 2026-11-22 10:00


- ****: No | Module Name | Sheet Name | Description | Pre-Condition
- ****: 1 | OperationService | OperationService | Unit test 70 TC (UTCID01–UTCID70) cho Application Service OperationService — List/Create/Update/Discontinue/Reactivate/PreviewImport/Import | Seed Product/Operation/PieceRate theo từng case; mock Excel parser, Audit, Notifier, UoW
- ****: 2 | MaterialReceiptService | MaterialReceiptService | Unit test 64 TC (UTCID71–UTCID134) — GetPaged/GetById/Create/Update/CreateSupplementary/Confirm/Return | Mock Stock, CodeGenerator, RequestService, DeliveryCountGuard, ShortageTracker
- ****: 3 | PayrollService | PayrollService | Unit test 69 TC (UTCID135–UTCID203) — Export/PreviewImport/Import/Submit/GetPayrolls/Approve/Reject/Reopen/GetMyPayslips | Mock PayrollRepository, ExcelParser/Writer, FileStore, Audit, UoW, Clock
- ****: 4 | OutputService | OutputService | Unit test 48 TC (UTCID204–UTCID251) — GetGrid/GetDayGrid/Save (cap, edit window, transfer, concurrency) | Mock UoW, Rates, Locks, Clock, ProductionGuard
- ****: 5 | MaterialIssueService | MaterialIssueService | Unit test 40 TC (UTCID252–UTCID291) — GetPaged/GetById/Create/Confirm/Reject | Mock Stock, Codes, RequestService, Notifier, UoW
- ****: 6 | ProductSpecService | ProductSpecService | 41 TC (UTCID292–314, UTCID533–550): Get/Save/Check/Write + color-only, Pieces[1,99] | Mock UoW repos, Audit
- ****: 7 | WorkOrderService | WorkOrderService | Unit test 59 TC (UTCID315–UTCID373) — GetPaged/GetById/Create/Update/Cancel/CloseShort/Fabric/Replacement | Mock UoW, Stock, Codes, Notifier, FabricPlanner/Checker
- ****: 8 | DefectService | DefectService | Unit test 42 TC (UTCID374–UTCID415) — ListForTeam/ListMine/Create/Update/Cancel | Mock UoW, PieceRate, Notifier, Audit, Clock
- ****: 9 | CustomerOrderService | CustomerOrderService | Unit test 54 TC (UTCID416–UTCID469) — List/Get/Create/Update/Cancel/Stop/Start | Mock Order/Product repos, UoW, Notifier, Audit, Clock
- ****: 10 | DailyWorkAllocationService | DailyWorkAllocationService | 59 TC (UTCID470–511, UTCID551–567): GetBoard/Save + DEV-ALL-157 + SettleOnTeamLeave | Mock UoW, AppLock, PieceRate, Notifier, Clock
- ****: 11 | VehicleService | VehicleService | 41 TC (UTCID512–532, UTCID568–587): GetLiveStatus/StartTrip + CompleteTrip (FuelCost) | Mock UoW, IAppLock, Audit, Clock, Settings
- ****: 12 | FinanceService | FinanceService | 72 TC (UTCID588–659) | Mock Finance repos/views, UoW
- ****: 13 | OrderPaymentService | OrderPaymentService | 90 TC (UTCID660–749) | Mock Order/Payment, Clock, UoW
- ****: 14 | SpecImportBuilder | SpecImportBuilder | 37 TC (UTCID750–786) | Workbook fixtures
- ****: 15 | SpecImportService | SpecImportService | 33 TC (UTCID787–819) | Mock Builder deps, UoW
- ****: 16 | EmployeeService | EmployeeService | 40 TC (UTCID820–859) | Mock HR repos, Settle, UoW
- ****: 17 | TeamService | TeamService | 60 TC (UTCID860–919) | Mock Team/Employee, Settle, UoW
- ****: 18 | AttendanceService | AttendanceService | 12 TC (UTCID920–931) | Mock Attendance sync, Notifier
- ****: 19 | MachineRepairRequestHandler | MachineRepairRequestHandler | 14 TC (UTCID932–945) | Mock Machine/Request
- ****: 20 | HomeDashboardService | HomeDashboardService | 29 TC (UTCID946–974) | Mock role dashboard deps


---

## Test Statistics

**UNIT TEST STATISTICS**

- **Project Name**: NX-MTD (Nội thất May – Quản lý sản xuất) | Creator
- **Project Code**: SEP490_NX-MTD | Reviewer/Approver
- **Document Code**: SEP490_NX-MTD_Test Report_vx.x | Issue Date | 2026-11-22 00:00:00
- **Notes**: v3.2 Round 2 after fix 2026-11-24 \| Stats trỏ Round 2 (B6) cho mọi sheet. ProductSpec Round3 = 5 case sửa (Passed). Case mới 533–974 Round2 Passed. Log: TestResults/Rebaseline_v3.1_Round2



- **No**: Function code | Passed | Failed | Untested | N | A | B | Total Test Cases
- **1**: OperationService | 70 | 0 | 0 | 13 | 24 | 33 | 70
- **2**: MaterialReceiptService | 64 | 0 | 0 | 12 | 28 | 24 | 64
- **3**: PayrollService | 69 | 0 | 0 | 15 | 28 | 26 | 69
- **4**: OutputService | 48 | 0 | 0 | 7 | 19 | 22 | 48
- **5**: MaterialIssueService | 40 | 0 | 0 | 8 | 18 | 14 | 40
- **6**: ProductSpecService | 41 | 0 | 0 | 7 | 22 | 12 | 41
- **7**: WorkOrderService | 59 | 0 | 0 | 12 | 25 | 22 | 59
- **8**: DefectService | 42 | 0 | 0 | 9 | 20 | 13 | 42
- **9**: CustomerOrderService | 54 | 0 | 0 | 12 | 25 | 17 | 54
- **10**: DailyWorkAllocationService | 59 | 0 | 0 | 10 | 25 | 24 | 59
- **11**: VehicleService | 41 | 0 | 0 | 7 | 18 | 16 | 41
- **12**: FinanceService | 72 | 0 | 0 | 13 | 27 | 32 | 72
- **13**: OrderPaymentService | 90 | 0 | 0 | 18 | 27 | 45 | 90
- **14**: SpecImportBuilder | 37 | 0 | 0 | 4 | 14 | 19 | 37
- **15**: SpecImportService | 33 | 0 | 0 | 7 | 15 | 11 | 33
- **16**: EmployeeService | 40 | 0 | 0 | 8 | 20 | 12 | 40
- **17**: TeamService | 60 | 0 | 0 | 14 | 29 | 17 | 60
- **18**: AttendanceService | 12 | 0 | 0 | 2 | 3 | 7 | 12
- **19**: MachineRepairRequestHandler | 14 | 0 | 0 | 4 | 4 | 6 | 14
- **20**: HomeDashboardService | 29 | 0 | 0 | 9 | 8 | 12 | 29
- ****: Sub total | 974 | 0 | 0 | 191 | 399 | 384 | 974

- ****: Test coverage | 100 | %
- ****: Test successful coverage | 100 | %
- ****: Normal case | 19.609856262833677 | %
- ****: Abnormal case | 40.965092402464066 | %
- ****: Boundary case | 39.42505133470226 | %


---

## OperationService

### Tóm tắt sheet

- **Code Module**: OperationService | Passed
- **Number of TCs**: 70 | Pending
- **Testing Round**: Passed | Failed | Pending | N/A
- **Round 1**: 54 | 16 | 0
- **Round 2**: 70 | 0 | 0
- **Round 3**: 0 | 0 | 70
- **Testing Type**: Normal | Abnormal | Boundary | N/A
- **Count**: 13 | 24 | 33

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **70**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### ListAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID01 | Seed Product P1 (Id=1) gồm 3 công đoạn: OP01, OP02 Active; OP03 Discontinued. PieceRate OP01: APPROVED hiệu lực 2026-11-01. | productId=1, includeDiscontinued=false | `Result.Success`, 2 phần tử (OP01, OP02); OP01.CurrentRate = rate 2026-11-01 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID02 | Như trên | productId=1, includeDiscontinued=true | Success, 3 phần tử, có OP03 với Status=Discontinued | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID03 | Không cần seed | productId=0 | `Result.Failure` (Validation), repo `Operation` không được query | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID04 | Không cần seed | productId=-1 | Failure (Validation) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID05 | Product Id=1 tồn tại, 0 công đoạn | productId=1, includeDiscontinued=false | Success, danh sách rỗng (Count=0) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID06 | OP01 có rate APPROVED EffectiveDate = 2026-11-22 (= Today), không có rate tương lai | productId=1 | OP01.CurrentRate = rate đó; UpcomingRate = null (ngày = Today tính là hiện hành) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID07 | OP01: rate APPROVED tương lai 2026-12-01 + rate PENDING 2026-12-15 | productId=1 | NextRate = PENDING (ưu tiên PENDING); UpcomingRate = APPROVED 2026-12-01. Ghi nhận 2 giá trị khác nhau (điểm nghi ngờ #4) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID08 | Không có product Id=999 | productId=999 | Failure `NotFound` (nhất quán với CreateAsync). ⚠ Code hiện tại trả Success với list rỗng (#8) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |

#### CreateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID09 | Seed Product Id=1; mock `IAuditLogService`; UoW transaction spy | dto {ProductId=1, OperationCode="OP10", Name="May cổ", StationSequence=10, StandardTimeSec=45, IsFinalStep=false} | `Result<int>.Success`, Data > 0; BeginTransaction 1 lần, SaveChanges 2 lần, Commit 1 lần; Audit được log 1 lần | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID10 | Product 1 đã có OP20 IsFinalStep=true, Active | dto {OperationCode="OP30", StationSequence=30, IsFinalStep=true} | Success; OP30.IsFinalStep=true; OP20.IsFinalStep=false (MoveFinalStep) | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID11 | Seed OperationGroup "Lắp ráp", OperationComponent "Thân trước", "Tay" | dto có GroupName="Lắp ráp", Components=["Thân trước","Tay"] | Success; công đoạn gắn đúng GroupId và 2 link component | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID12 | Product 1 tồn tại | OperationCode="  OP11  " | Success; mã lưu DB = "OP11" (đã Trim) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID13 | Product 1 tồn tại; cấu hình MaxLength OperationCode = 50 | OperationCode = chuỗi 50 ký tự | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID14 | Như trên | OperationCode = chuỗi 51 ký tự | Failure (Validation), không gọi SaveChanges | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID15 | Product 1 tồn tại | OperationCode="" (rỗng) | Failure (Validation) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID16 | Product 1 tồn tại | StandardTimeSec=0 | Failure (Validation). ⚠ Service không check, phụ thuộc validator (#5) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID17 | Product 1 tồn tại | StandardTimeSec=-1 | Failure (Validation). ⚠ (#5) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID18 | Product 1 tồn tại | Components=[] (0 phần tử), GroupName=null | Success, không tạo link component nào | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID19 | — | dto = null | `ArgumentNullException` hoặc Failure (Validation), không mở transaction | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID20 | Product 1 tồn tại | OperationCode=null | Failure (Validation). ⚠ Code hiện tại ném `NullReferenceException` tại `.Trim()` (#6) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | System.NullReferenceException : Object reference not set to an instance of an object. |  |
| UTCID21 | Không có Product 999 | dto.ProductId=999 | Failure `NotFound`; repo Add không được gọi | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID22 | Product 1 đã có OP10 | OperationCode="OP10" | Failure (DuplicateCode); không SaveChanges | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID23 | Product 1 đã có công đoạn STT=10 | StationSequence=10, code mới | Failure (DuplicateSequence) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID24 | Mock SaveChanges lần 2 ném `DbUpdateException` | dto hợp lệ, IsFinalStep=true | Rollback được gọi; không Commit; exception propagate (hoặc Failure). Final step cũ giữ nguyên | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected oldFinal.IsFinalStep to be True, but found False. |  |

#### UpdateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID25 | Seed OP10 (Id=5, ProductId=1, Active) | operationId=5, dto {Name="May cổ mới", StandardTimeSec=50} | Success; Name, StandardTimeSec cập nhật; Audit log | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID26 | OP10 Active; OP20 đang IsFinalStep | operationId=5, IsFinalStep=true | Success; OP10 final, OP20 bỏ final | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID27 | OP10 Id=5 | dto.OperationCode="OP10" (trùng chính nó) | Success (không tính là trùng mã với chính mình) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID28 | OP10 Id=5 ProductId=1 | dto.ProductId=2 | Success; ProductId vẫn = 1 (ép theo entity cũ) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID29 | OP10 Discontinued; OP20 final Active | IsFinalStep=true | Không MoveFinalStep (chỉ chạy khi Active); OP20 vẫn final | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected discontinued.IsFinalStep to be False, but found True. |  |
| UTCID30 | Không có Id 999 | operationId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID31 | — | operationId=0 | Failure `NotFound`/Validation | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID32 | OP10 (Id=5), OP20 (Id=6) cùng product | operationId=5, OperationCode="OP20" | Failure (DuplicateCode) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID33 | OP10 Id=5 | OperationCode=null | Failure (Validation). ⚠ NRE tại `.Trim()` (#6) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | System.NullReferenceException : Object reference not set to an instance of an object. |  |
| UTCID34 | Mock SaveChanges ném `DbUpdateConcurrencyException` | operationId=5, dto hợp lệ | Exception propagate / Failure (Concurrency); transaction rollback | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### DiscontinueAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID35 | OP10 Active, không có WorkAllocation PENDING, mọi RemainingQty = 0 | operationId=5 | Success; Status = Discontinued; Audit log | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID36 | OP10 đã Discontinued | operationId=5 | Success (idempotent); SaveChanges không gọi | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID37 | OP10 có 1 WorkAllocation PENDING | operationId=5 | Failure `HasOpenWork` | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID38 | VwWorkerOutputCap RemainingQty=1, đơn chưa đóng | operationId=5 | Failure `HasOpenWork` | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID39 | RemainingQty=5 nhưng đơn đã đóng (Completed/Cancelled) | operationId=5 | Success (đơn đóng không tính open work) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID40 | Không có Id 999 | operationId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID41 | OP20 là công đoạn cuối Active duy nhất của Product 1 | operationId=OP20 | Failure (FinalStepRequired). ⚠ Code hiện tại Success → mã hàng mất final step (#1) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |

#### ReactivateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID42 | OP03 Discontinued, IsFinalStep=false | operationId=OP03 | Success; Status = Active | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID43 | OP03 Discontinued, IsFinalStep=true; product không còn final Active | operationId=OP03 | Success; OP03 Active và là final duy nhất | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID44 | OP10 đang Active | operationId=OP10 | Success no-op (idempotent), SaveChanges không gọi | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID45 | OP03 Discontinued, IsFinalStep=true; OP20 final Active đã tồn tại | operationId=OP03 | Failure (FinalStepConflict) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID46 | Không có Id 999 | operationId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### PreviewImportAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID47 | Product 1 tồn tại; mock `IExcelOperationParser` trả 1 sheet "CongDoan", 3 dòng hợp lệ (STT 10/20/30, dòng 30 final) | productId=1, stream hợp lệ, sheetName="CongDoan" | Success Preview 3 dòng, Action Create/Update đúng; **không** SaveChanges | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID48 | Parser trả 2 sheet | sheetName=null | Dùng sheet đầu tiên | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID49 | Parser trả sheet chỉ có header, 0 dòng | productId=1 | Success, 0 dòng (hoặc Failure Empty theo spec); không exception | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID50 | Parser trả 2 dòng cùng STT=10 | productId=1 | 2 dòng Action=Error, lỗi trùng STT | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.Data.Rows to contain only items matching (r.Action == "Error"), but |  |
| UTCID51 | Parser trả 2 dòng IsFinal=true | productId=1 | Các dòng final đánh dấu Error "nhiều công đoạn cuối" | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID52 | Dòng có component "Cầu vai" không tồn tại | productId=1 | Dòng hợp lệ + warning component skip | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID53 | Dòng STT=32767 | productId=1 | Dòng hợp lệ, StationSequence=32767 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID54 | Dòng STT=32768 | productId=1 | Dòng Error (STT vượt giới hạn). ⚠ Cast `(short)` tràn số âm (#9) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.Data!.Rows.Single().Action to be "Error" with a length of 5, but "New" has a length of 3, differs near "New" (index 0). |  |
| UTCID55 | Parser ném exception (file không phải xlsx) | stream rác | Failure (ExcelInvalid); exception được catch | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID56 | Parser trả sheet "CongDoan" | sheetName="XYZ" | Failure (SheetNotFound) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID57 | Parser trả list sheet rỗng | sheetName=null | Failure (ExcelInvalid). ⚠ Code hiện tại ném `ArgumentOutOfRangeException` tại `sheets[0]` (#7) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | System.ArgumentOutOfRangeException : Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'index') |  |
| UTCID58 | Không có Product 999 | productId=999, stream hợp lệ | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID59 | — | stream = null | `ArgumentNullException` hoặc Failure (ExcelInvalid) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### ImportAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID60 | Product 1 rỗng; parser trả 3 dòng hợp lệ | replace=false, submitRates=false | Success ImportResultDto {Created=3}; Commit 1 lần; không tạo PieceRate | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID61 | Như trên; mock `INotificationService` | submitRates=true, effectiveDate=2026-12-01 | 3 PieceRate PENDING EffectiveDate=2026-12-01; Notifier gửi Admin đúng 1 lần | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID62 | Product 1 có OP99 chưa dùng (không có Output/Defect/Assignment), file không có OP99 | replace=true | Success; OP99 bị xóa; Deleted=1 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID63 | OP99 đã có OutputRecord, file không có OP99 | replace=true | OP99 không bị xóa; kết quả báo blocked/used | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID64 | Parser trả 3 dòng, cả 3 Error | replace=true | Failure (NoValidRows) hoặc Success kèm cảnh báo rõ "0 thay đổi". ⚠ Hiện tại Success im lặng, dễ hiểu nhầm đã replace (#2) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID65 | 3 dòng hợp lệ | submitRates=true, effectiveDate = 2026-11-22 (= Today) | Success, rate EffectiveDate = Today | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID66 | 3 dòng hợp lệ | submitRates=true, effectiveDate=null | Rate dùng ngày mặc định (Today) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected _rates to contain only items matching (r.EffectiveDate == OperationServiceHighComplexityTests.SpecToday), but the collection is empty. |  |
| UTCID67 | OP10 có component cũ "Tay"; dòng import chỉ có component không tồn tại | replace=false | Component cũ "Tay" giữ nguyên + warning. Assert warning có trong kết quả (#3) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID68 | Parser trả 2 dòng IsFinal=true | replace=false | Failure (FinalStep); không SaveChanges | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.ErrorCode to be the same string, but they differ at index 10: |  |
| UTCID69 | Parser ném exception | stream rác | Failure (ExcelInvalid); BeginTransaction không gọi | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID70 | SaveChanges ném `DbUpdateException` | submitRates=true | Rollback; Notifier **không** được gọi | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |


---

## MaterialReceiptService

### Tóm tắt sheet

- **Code Module**: MaterialReceiptService | Passed
- **Number of TCs**: 64 | Pending
- **Testing Round**: Passed | Failed | Pending | N/A
- **Round 1**: 56 | 8 | 0
- **Round 2**: 64 | 0 | 0
- **Round 3**: 0 | 0 | 64
- **Testing Type**: Normal | Abnormal | Boundary | N/A
- **Count**: 12 | 28 | 24

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **64**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### GetPagedAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID71 | Seed 5 phiếu: 3 Pending, 2 Confirmed | query {Status=Pending, Page=1, PageSize=10} | Success; Items=3 Pending; TotalCount=3 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID72 | Phiếu thuộc đơn "DH2611-001" | query {Keyword="DH2611"} | Trả về các phiếu có code/đơn/PO chứa keyword | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID73 | 5 phiếu | Page=2, PageSize=1 | Items=1 (phiếu thứ 2 theo sort); TotalCount=5 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID74 | 5 phiếu | Page=10, PageSize=10 (vượt trang) | Items rỗng; TotalCount=5 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID75 | 5 phiếu | Keyword="   " | Không áp filter, trả 5 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID76 | DB rỗng | query mặc định | Success; Items rỗng; Total=0 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID77 | 5 phiếu | OrderId=999 (không tồn tại) | Success; Items rỗng (không lỗi) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID78 | 5 phiếu | Page=0, PageSize=0 | Chuẩn hóa về Page=1/PageSize mặc định, hoặc Failure Validation; không exception chia 0 | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### GetByIdAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID79 | Phiếu Id=10 Confirmed, 2 dòng, là lần giao thứ 2 | receiptId=10 | Success; Lines=2; DeliveryNo=2 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID80 | Phiếu 10 có Request SHORT_DELIVERY, ReplenishQty=50 | receiptId=10 | ShortDeliveryRequestId ≠ null; ToReplenishQty=50 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID81 | Phiếu 10 và 11 Confirmed cùng ConfirmedAt | receiptId=11 | DeliveryNo phiếu 11 = DeliveryNo phiếu 10 + 1 (tie-break theo Id) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID82 | Phiếu 12 Pending, 0 dòng | receiptId=12 | Success; Lines rỗng; không exception | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID83 | Không có 999 | receiptId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID84 | — | receiptId=0 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID85 | Dòng phiếu có OrderMaterial = null (data lệch) | receiptId=13 | Failure hoặc MaterialId=null an toàn. ⚠ NRE tại `l.MaterialId!.Value` (#4) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### CreateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID86 | Đơn 1 có NVL M1 cần 200, đã nhận 0; mock `IDocumentCodeGenerator` → "PN2611-001"; mock Notifier | dto {Source=CUSTOMER, OrderId=1, ReceiptDate=2026-11-22, Lines=[M1, AcceptedQty=100]} | Success DTO Status=Pending, Code="PN2611-001"; Notifier gọi 1 lần | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID87 | PO 5 (Supplier S1) dòng M2 = 300 | dto {Source=SUPPLIER, POId=5, Lines=[M2,150]} | Success; SupplierId=S1 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID88 | M1 outstanding=100 | AcceptedQty=100 | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID89 | Đơn 1 hợp lệ | Lines=[] (0 dòng) | Failure (Validation) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.Kind to be ResultKind.Validation {value: 1}, but found ResultKind.NotFound {value: 4}. |  |
| UTCID90 | DeliveryCountGuard: max 3 lần, đã có 2 | dto hợp lệ | Success (lần thứ 3) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID91 | Đã đủ 3 lần giao | dto hợp lệ | Failure `DeliveryCountReached` | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID92 | — | Source="OTHER" | Failure (Validation) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID93 | Không có đơn 999 | Source=CUSTOMER, OrderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID94 | Đã có phiếu Pending cho M1 của đơn 1 | Lines=[M1, 50] | Failure (DuplicatePending) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID95 | Shortage request của M1 đã đóng | dto M1 | Failure (ShortageClosed) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID96 | Mock transaction trả `created.IsSuccess=false` | dto hợp lệ | Trả nguyên Failure. ⚠ Code hiện tại vẫn gọi `GetByIdAsync(created.Data)` (#7) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### UpdateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID97 | Phiếu 10 Pending, 1 dòng M1=100 | receiptId=10, Lines=[M1=80, M3=20] | Success; DTO có 2 dòng mới | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID98 | Phiếu 10 Returned | receiptId=10, dto hợp lệ | Success (Returned được sửa) | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID99 | M1 outstanding = 100 (không tính chính phiếu 10 đang giữ 100) | receiptId=10, AcceptedQty=100 | Success. ⚠ Nếu không truyền `exceptReceiptId` sẽ đếm trùng → Failure sai (#9) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID100 | Phiếu 10 Pending | Lines=[] | Failure (Validation) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID101 | Phiếu 10 Confirmed | receiptId=10 | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID102 | Phiếu 14 là phiếu bổ sung (supplementary) Pending | receiptId=14 | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID103 | Không có 999 | receiptId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID104 | Dòng cũ có ảnh/attachment hoặc OriginalReceiptLine tham chiếu | receiptId=10, Lines mới | Tham chiếu được giữ hoặc chặn update. ⚠ Xóa hết rồi thêm lại → orphan (#5) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### CreateSupplementaryAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID105 | Shortage request 20, line RL1 ReplenishQty=50, đã bổ sung 0 | dto {ShortageRequestId=20, ReceiptDate=2026-11-22, Lines=[RL1, 30]} | Success; phiếu bổ sung Pending | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID106 | remaining = 50 | AcceptedQty=50 | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID107 | remaining = 50 | AcceptedQty=51 | Failure `ExceedsReplenish` | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID108 | Đã có phiếu bổ sung Pending 20 (includePending) → remaining=30 | AcceptedQty=31 | Failure `ExceedsReplenish` | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID109 | Request tạo ngày 2026-11-20 | ReceiptDate=2026-11-20 (cùng ngày) | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID110 | Request tạo ngày 2026-11-20 | ReceiptDate=2026-11-19 | Failure (ReceiptDate invalid) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.Kind to be ResultKind.Validation {value: 1}, but found ResultKind.NotFound {value: 4}. |  |
| UTCID111 | Không có request 999 | ShortageRequestId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID112 | Request 20 đã Resolved/Closed | ShortageRequestId=20 | Failure `NotResolvable` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID113 | DeliveryCountGuard trả đã đủ lần | dto hợp lệ | Failure `DeliveryCountReached` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID114 | RL99 không thuộc request 20 | Lines=[RL99, 10] | Failure (Validation) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### ConfirmAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID115 | Phiếu 10 Pending, M1 AcceptedQty=100 ≤ outstanding 200, có ảnh; mock `IStockService.PostAsync` → Success | receiptId=10, confirmedByEmployeeId=7 | Success; Stock nhận +100 (1 lần); MaterialSupplyUpdater.Recalculate gọi; Status=Confirmed, ConfirmedAt=Now, ConfirmedBy=7 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID116 | Lần giao cuối (3/3), M1 cần 200, đã nhận 150, phiếu 50 → thiếu 0; một trường hợp khác nhận 30 → thiếu 20 | receiptId=10 (nhận 30) | Success + message `SHORT_DELIVERY`; ShortageTracker tạo request thiếu 20 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID117 | outstanding=100 | AcceptedQty=100 | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID118 | outstanding=100, nguồn SUPPLIER | AcceptedQty=101 | Failure `ExceedsPurchase`; Stock không gọi | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID119 | outstanding=100, nguồn CUSTOMER | AcceptedQty=101 | Failure `ExceedsCustomer` | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID120 | Mọi dòng AcceptedQty=0 | receiptId=10 | Theo policy: Failure (EmptyReceipt) hoặc không tính lần giao. ⚠ Hiện tại Success, Stock không gọi nhưng vẫn đếm lần giao / có thể kích SHORT_DELIVERY (#3) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID121 | NotReplenishedQty lớn → outstanding = -20 | AcceptedQty=10 | Failure Exceeds với message không hiển thị số âm. ⚠ Message "còn -20" (#6) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Did not expect result.Message "Số nhập vượt số còn phải nhận của dòng đơn mua: còn -20, nhập 10." to contain "còn -". |  |
| UTCID122 | Không có 999 | receiptId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID123 | Phiếu 10 đã Confirmed | receiptId=10 | Failure `InvalidStatus`; Stock không gọi | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID124 | Phiếu 10 Returned | receiptId=10 | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID125 | Phiếu 10 Pending, chưa có ảnh | receiptId=10 | Failure `PhotoRequired` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID126 | Phiếu 10 Pending hợp lệ | confirmedByEmployeeId=0 | Failure (Validation). ⚠ Service không check, lỗi FK muộn (#1) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID127 | Mock `IStockService` trả Failure | receiptId=10 | Failure (stock); Rollback; Status vẫn Pending | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID128 | 2 phiếu Pending P10 (80) và P11 (80) cùng M1, outstanding=100; P10 đã Confirm trước | Confirm P11 | Failure `ExceedsPurchase/Customer` (others Confirmed=80, 80+80 > 100) (#2) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### ReturnAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID129 | Phiếu 10 Pending; mock Notifier | receiptId=10, Reason="Sai số lượng" | Success; Status=Returned; Notifier gửi Thủ kho | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID130 | Phiếu 10 Pending; MaxLength Reason=500 | Reason = 500 ký tự | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID131 | Phiếu 10 Pending | Reason="" | Failure (Validation) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID132 | Phiếu 10 Pending | Reason=null | Failure (Validation). ⚠ NRE tại `dto.Reason.Trim()` (#8) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | System.NullReferenceException : Object reference not set to an instance of an object. |  |
| UTCID133 | Phiếu 10 Confirmed | Reason="x" | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID134 | Không có 999 | receiptId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |


---

## PayrollService

### Tóm tắt sheet

- **Code Module**: PayrollService | Passed
- **Number of TCs**: 69 | Pending
- **Testing Round**: Passed | Failed | Pending | N/A
- **Round 1**: 62 | 7 | 0
- **Round 2**: 69 | 0 | 0
- **Round 3**: 0 | 0 | 69
- **Testing Type**: Normal | Abnormal | Boundary | N/A
- **Count**: 15 | 28 | 26

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **69**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### ExportAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID135 | Period 2026-10 tồn tại; mock `IPayrollExcelWriter` trả byte[] | periodMonth="2026-10" | Success ExcelFileDto: FileName có "2026-10", Content.Length > 0 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID136 | Chưa có period 2026-12 | periodMonth="2026-12" | Success; period 2026-12 được tạo (GetOrCreatePeriod) | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID137 | — | periodMonth="2026-01" | Success (tháng biên dưới) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID138 | — | periodMonth="2026-13" | Failure `PeriodMonthInvalid` | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID139 | — | periodMonth="2026-00" | Failure `PeriodMonthInvalid` | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID140 | — | periodMonth="" | Failure `PeriodMonthInvalid` | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID141 | — | periodMonth=null | Failure `PeriodMonthInvalid` (không exception) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | System.ArgumentNullException : Value cannot be null. (Parameter 'input') |  |
| UTCID142 | — | periodMonth="2026/10" | Failure `PeriodMonthInvalid` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### PreviewImportAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID143 | Mock `IExcelPayrollParser` trả 3 dòng hợp lệ (NV001–003, NetPay = Σ items) | stream hợp lệ | Success; 3 dòng Valid | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID144 | Parser trả 0 dòng | stream chỉ có header | Failure (Empty) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID145 | Dòng WorkDays=0 | stream | Dòng Valid | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID146 | Dòng WorkDays=31 | stream | Dòng Valid | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID147 | Dòng WorkDays=32 | stream | Dòng Error | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID148 | Dòng WorkDays=-1 | stream | Dòng Error | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID149 | Dòng WorkDays=null | stream | Dòng Error (bắt buộc). ⚠ Hiện tại Valid (#2) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.Data!.Rows.Single().RowStatus to be "Error", but "Valid" differs near "Val" (index 0). |  |
| UTCID150 | NetPay lệch Σ items đúng 1.000đ | stream | Valid, không warning | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID151 | NetPay lệch Σ items 1.001đ | stream | Valid + warning lệch NetPay (#1) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID152 | NV001 xuất hiện 2 dòng | stream | Dòng sau đánh dấu Duplicate | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID153 | Parser ném exception | stream rác | Failure (ExcelInvalid), không ném ra ngoài | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID154 | Dòng NetPay=null | stream | Dòng Error | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID155 | Dòng items rỗng | stream | Dòng Error | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID156 | Dòng mã NV "NV999" không tồn tại | stream | Dòng Error (EmployeeNotFound) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### ImportAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID157 | Payroll 2026-10 Draft, period mở, có file cũ "old.xlsx"; mock `IPayrollFileStore` | stream 3 dòng hợp lệ, periodMonth="2026-10" | Success ImportResult {Imported=3}; FileStore.Save gọi 1 lần; Delete("old.xlsx") gọi; payslips cũ thay bằng mới | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID158 | Payroll 2026-10 Rejected | stream hợp lệ | Success (Rejected được import lại) | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID159 | Stream 2 dòng Valid + 1 Error | periodMonth="2026-10" | Success; Imported=2, ErrorCount=1. Response phải có error count (#3) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID160 | Stream toàn dòng Error | periodMonth="2026-10" | Failure (EmptyValid); FileStore.Save **không** gọi | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID161 | AccountingPeriod 2026-10 Locked | stream hợp lệ | Failure `PeriodLocked`; FileStore.Save không gọi | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID162 | Payroll 2026-10 PendingApproval | stream hợp lệ | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID163 | Payroll 2026-10 Approved | stream hợp lệ | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID164 | — | periodMonth="10-2026" | Failure `PeriodMonthInvalid` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID165 | Mock persist (SaveChanges) ném exception sau khi FileStore.Save "new.xlsx" | stream hợp lệ | Failure; FileStore.Delete("new.xlsx") được gọi; file cũ **không** bị xóa (#7) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID166 | Parser ném exception | stream rác | Failure (ExcelInvalid) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### GetByPeriodMonthAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID167 | Payroll 2026-10 có 3 payslip | periodMonth="2026-10" | Success Summary: EmployeeCount=3, TotalNetPay đúng | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID168 | — | periodMonth="2026-13" | Failure `PeriodMonthInvalid` | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID169 | Chưa có payroll 2026-09 | periodMonth="2026-09" | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### SubmitAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID170 | Payroll Id=1 Draft, 5 payslip | payrollId=1 | Success; Status=PendingApproval; Audit log | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID171 | Payroll Id=2 Draft, 0 payslip | payrollId=2 | Failure (Empty) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID172 | Payroll Id=3 Approved | payrollId=3 | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID173 | Không có 999 | payrollId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### GetImportedFileAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID174 | Payroll 1 có ImportedFilePath; FileStore trả stream | payrollId=1 | Success File (Content, ContentType xlsx) | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID175 | Payroll 4 chưa import (FilePath=null) | payrollId=4 | Failure `FileNotFound` | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID176 | FilePath có nhưng FileStore trả null (file bị xóa trên disk) | payrollId=1 | Failure `FileNotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID177 | Không có 999 | payrollId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### GetPayrollsAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID178 | 3 payroll: Draft, PendingApproval, Approved | query {Status="PendingApproval", Page=1} | Success; 1 item (map sang raw PENDING_APPROVAL) | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID179 | 3 payroll | Status=null | Trả cả 3 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID180 | 3 payroll | Page=5, PageSize=10 | Items rỗng, Total=3 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID181 | 3 payroll | Status="Foo" | Failure (Validation 400). ⚠ Hiện tại trả list rỗng (#5) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |

#### GetDetailAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID182 | Payroll 1 có 5 payslip | payrollId=1 | Success Detail: 5 payslip, đúng Status | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID183 | Payroll 2 Draft, 0 payslip | payrollId=2 | Success; Payslips rỗng, Total=0 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID184 | — | payrollId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### ApproveAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID185 | Payroll 1 PendingApproval; AccountingPeriod 2026-10 mở | payrollId=1 | Success; Status=Approved; AccountingPeriod.IsLocked=true; Audit log | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID186 | Payroll PendingApproval nhưng 0 payslip (data bất thường) | payrollId=5 | Failure (Empty). ⚠ Hiện tại vẫn Approve (#8) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID187 | Payroll 2 Draft | payrollId=2 | Failure `InvalidStatus`; period không bị khóa | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID188 | — | payrollId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### RejectAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID189 | Payroll 1 PendingApproval | payrollId=1, Reason="Sai phụ cấp" | Success; Status=Rejected; RejectReason lưu | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID190 | Payroll 1 PendingApproval | Reason="   " | Failure (Validation). ⚠ Service không check (#9) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID191 | Payroll 2 Draft | Reason="x" | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID192 | — | payrollId=999, Reason="x" | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### ReopenAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID193 | Payroll 1 Approved; period 2026-10 Locked | payrollId=1, Reason="Điều chỉnh thưởng" | Success; Status=Draft; period IsLocked=false (side-effect cả tháng, #4) | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID194 | Payroll 1 Approved | Reason=null | Failure (Validation; SRS bắt buộc lý do). ⚠ (#9) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID195 | Payroll 2 Draft | Reason="x" | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID196 | — | payrollId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### GetMyPayslipsAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID197 | NV Id=10 có payslip Approved tháng 08, 09, 10 | employeeId=10 | Success; 3 phần tử, sắp xếp tháng giảm dần | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID198 | NV 11 chưa có payslip | employeeId=11 | Success; list rỗng | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID199 | — | employeeId=0 | Failure (Validation) hoặc list rỗng; không exception | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |

#### GetMyPayslipAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID200 | NV 10 có payslip 2026-10 Approved | employeeId=10, periodMonth="2026-10" | Success Detail: items, NetPay | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID201 | — | employeeId=10, periodMonth="2026-13" | Failure `PeriodMonthInvalid` | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID202 | Payslip 2026-10 chỉ có của NV 11 | employeeId=10, periodMonth="2026-10" | Failure `NotFound` (không lộ lương người khác) (#6) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID203 | Payroll 2026-11 còn Draft | employeeId=10, periodMonth="2026-11" | Failure `NotFound` (NV không xem được lương chưa duyệt; xác nhận lại với spec) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |


---

## OutputService

### Tóm tắt sheet

- **Code Module**: OutputService | Passed
- **Number of TCs**: 48 | Pending
- **Testing Round**: Passed | Failed | Pending | N/A
- **Round 1**: 42 | 6 | 0
- **Round 2**: 48 | 0 | 0
- **Round 3**: 0 | 0 | 48
- **Testing Type**: Normal | Abnormal | Boundary | N/A
- **Count**: 7 | 19 | 22

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **48**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### GetGridAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID204 | ICurrentUser = TeamLeader tổ 3; OrderLine 100 đã chia tổ 3 (Allotted); Assignment: W1×OP10, W2×OP10; OutputRecord W1×OP10 = 20; Cap W1×OP10 = 50; mock PieceRateService trả rate 1.500đ; Now trước EditDeadline | workDate=2026-11-22, shiftCode="CA1", orderLineId=100, sewingTeamId=null | Success grid 2 dòng; W1: Reported=20, Cap=50; CanEdit=true | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID205 | ICurrentUser = Manager | workDate=2026-11-22, shiftCode="CA1", orderLineId=100, sewingTeamId=3 | Success grid của tổ 3 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID206 | W3×OP20 không có assignment/record nhưng Cap=10 | như UTCID trên | Có dòng W3×OP20 (union keys, cap > 0) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID207 | W4×OP20 chỉ có Cap=0 | như trên | Không có dòng W4×OP20 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID208 | TeamLeader; IClock.Now = EditDeadline (đúng mốc) | workDate=2026-11-22 | CanEdit=true (`<=`) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID209 | TeamLeader; IClock.Now = EditDeadline + 1 giây | workDate=2026-11-22 | CanEdit=false | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID210 | Manager; Now sau EditDeadline | sewingTeamId=3 | CanEdit=true | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID211 | — | shiftCode="XYZ" | Failure (InvalidShift) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID212 | Không có OrderLine 999 | orderLineId=999 | Failure (OrderLine NotFound) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID213 | OrderLine 101 chưa chia cho tổ 3 | orderLineId=101 | Failure (NotAllotted) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID214 | ICurrentUser = Manager | sewingTeamId=null | Failure (Team required) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID215 | OutputRecord có OperationId=777 không còn trong dictionary operations | orderLineId=100 | Bỏ qua dòng hoặc Failure rõ ràng. ⚠ `KeyNotFoundException` tại `operations[k.OperationId]` (#7) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Did not expect System.Collections.Generic.KeyNotFoundException, but found System.Collections.Generic.KeyNotFoundException: The given key '777' was not present in the dictionary. |  |

#### GetDayGridAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID216 | W1×OP10: CA1=10, CA2=15 | workDate=2026-11-22, orderLineId=100 | Success; W1 Quantity=25; CanEdit=false; Shift=AllDay | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID217 | Có assignment nhưng 0 record | như trên | Dòng Quantity=0 | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected row.Quantity to be 0, but found <null>. |  |
| UTCID218 | CA1 rate 1.500, CA2 rate 1.700 (khác rate) | như trên | UnitRate hiển thị cần xác định rõ (rate đại diện / bình quân). ⚠ Hiện tại `FirstOrDefault` (#5) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected row.UnitRate to be 1620M, but found 1500M (difference of -120). |  |
| UTCID219 | OrderLine 101 không Allotted cho tổ 3 | orderLineId=101 | Failure (NotAllotted) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### SaveAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID220 | TeamLeader tổ 3; OP10 Active cùng product; W1, W2 thuộc tổ 3; Cap 50; rate APPROVED; mock IAppLock acquire OK | dto {WorkDate=2026-11-22, Shift="CA1", OrderLineId=100, Rows=[(OP10,W1,10),(OP10,W2,12)]} | Success {Inserted=2, Updated=0, Deleted=0}; AppLock acquire/release; Commit; Audit log | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID221 | Record W1×OP10 CA1 = 10, RowVersion=v1 | Rows=[(OP10,W1,15,RowVer=v1)] | Success {Updated=1}; Quantity=15 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID222 | Record W1×OP10 = 10 | Rows=[(OP10,W1,0,v1)] | Success {Deleted=1}; record bị xóa | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID223 | Cap W1×OP10=50; W5 cùng tổ còn trần dư 10; mock OutputCapTransfer.ApplyAsync Success | Rows=[(OP10,W1,55)], TransferExcess=true | Success + warning chuyển trần; ApplyAsync gọi với excess=5 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID224 | Config MaxQty=1000 | Quantity=1000 | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID225 | MaxQty=1000 | Quantity=1001 | Failure (Validation) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID226 | Chưa có record W1×OP10 | Quantity=0 | Success {Inserted=0} (skip) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID227 | — | Quantity=-1 | Failure (Validation) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID228 | Cap=50; ca khác đã báo 30 (others) | Quantity=20 (30+20 = cap) | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID229 | Cap=50; others=30 | Quantity=21 | Failure (CapExceeded) (cộng dồn mọi ca, #9) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID230 | — | Rows=[] (0 phần tử) | Success 0/0/0 hoặc Failure Validation; không mở transaction | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID231 | — | WorkDate=2026-11-23 (Today+1) | Failure (FutureDate) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID232 | Đơn ReceivedDate=2026-11-10 | WorkDate=2026-11-10 | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID233 | Đơn ReceivedDate=2026-11-10 | WorkDate=2026-11-09 | Failure (BeforeOrderStart) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID234 | TeamLeader; Now = EditDeadline | Rows sửa record cũ | Success (`<=`) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID235 | TeamLeader; Now = EditDeadline + 1 giây | Rows sửa record cũ | Failure (EditWindowClosed) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID236 | Manager; quá hạn | Rows sửa, Reason="Bổ sung sót" | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID237 | Manager; quá hạn | Rows sửa, Reason=null | Failure (ReasonRequired) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID238 | Đã có record W1×OP10 và W2×OP10 | Rows chỉ gửi W1 (bỏ W2) | Record W2 **vẫn tồn tại** (omit ≠ delete); Deleted=0 (#1) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID239 | — | Rows=[(OP10,W1,5),(OP10,W1,7)] (trùng key) | Failure (DuplicateRow) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID240 | ProductionGuard: kỳ 2026-11 Locked | dto hợp lệ | Failure `PeriodLocked`; không SaveChanges | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID241 | Đơn đã đóng (Completed/Stopped) | dto hợp lệ | Failure (OrderClosed) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID242 | PieceRate OP10 chưa APPROVED | Row mới (insert) | Failure `PieceRateNotApproved` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID243 | PieceRate OP10 chưa APPROVED; record cũ đã tồn tại | Row update qty 10→12 | Success (update không re-check rate, GBR-11) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID244 | OP30 Discontinued | Rows=[(OP30,W1,5)] | Failure (OperationInactive) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID245 | OP40 thuộc product khác | Rows=[(OP40,W1,5)] | Failure (OperationNotInProduct) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID246 | W9 thuộc tổ 5 | Rows=[(OP10,W9,5)] | Failure (WorkerNotInTeam) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID247 | Record RowVersion=v2 | Row gửi RowVer=v1 | Failure (Concurrency) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException : Exception of type 'Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException' was thrown. |  |
| UTCID248 | Không có trần dư; mock OutputCapTransfer.ApplyAsync Failure | Quantity vượt cap, TransferExcess=true | Failure; Rollback; không record nào được ghi (#3) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID249 | Mock IAppLock acquire timeout | dto hợp lệ | Failure (Busy/LockTimeout); không SaveChanges | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | System.InvalidOperationException : Lock timeout |  |
| UTCID250 | Mock SaveChanges ném SqlException số 50052 (trigger cap) | dto hợp lệ | Failure (CapExceeded) đã map; Rollback (#2) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Microsoft.EntityFrameworkCore.DbUpdateException : Sản lượng vượt số lần được giao cho công nhân ở công đoạn này. |  |
| UTCID251 | — | dto=null | `ArgumentNullException` hoặc Failure (Validation) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |


---

## MaterialIssueService

### Tóm tắt sheet

- **Code Module**: MaterialIssueService | Passed
- **Number of TCs**: 40 | Pending
- **Testing Round**: Passed | Failed | Pending | N/A
- **Round 1**: 33 | 7 | 0
- **Round 2**: 40 | 0 | 0
- **Round 3**: 0 | 0 | 40
- **Testing Type**: Normal | Abnormal | Boundary | N/A
- **Count**: 8 | 18 | 14

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **40**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### GetPagedAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID252 | 4 phiếu cấp: tổ 3 (2 phiếu), tổ 5 (2 phiếu) | query {Page=1}, receivingTeamId=null (Thủ kho) | Success; 4 items | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID253 | Như trên | receivingTeamId=3, Status=Pending | Chỉ phiếu tổ 3 có Status Pending | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID254 | DB rỗng | query mặc định | Success; Items rỗng | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID255 | 4 phiếu | Page=3, PageSize=2 | Items rỗng; Total=4 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID256 | Phiếu cấp không theo đơn (Order=null) hoặc IssuedBy bị xóa | query mặc định | Success; OrderCode/IssuedByName = null. ⚠ Lỗi navigation thiếu null-safe (#8) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### GetByIdAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID257 | Phiếu 50 thuộc tổ 3 | issueId=50, receivingTeamId=3 | Success DTO đủ lines | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID258 | Phiếu 50 tổ 3 | issueId=50, receivingTeamId=null | Success (không check team) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID259 | Phiếu 50 tổ 3 | issueId=50, receivingTeamId=5 | Failure `TeamMismatch` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID260 | — | issueId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### CreateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID261 | Đơn xin 30 Approved: NVL thô M1 = 100; tồn kho M1 = 500; mock CodeGenerator → "PX2611-001" | dto {RequestId=30, Lines=[M1, IssuedQty=100]}, issuedBy=7 | Success DTO Status=PendingReceipt; tồn kho chưa trừ | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID262 | BTP OrderLine 100, công đoạn đã phân công cho tổ 3 | dto cấp BTP cho tổ 3, Lines có OrderLineId=100 | Success | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID263 | Tồn M1 = 100 | IssuedQty=100 | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID264 | Tồn M1 = 100 | IssuedQty=101 | Failure (InsufficientStock) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID265 | — | Lines=[] | Failure (Validation) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID266 | Đơn xin M1 = 100; tồn đủ | IssuedQty=120 | Success + cảnh báo vượt số xin (DEV-ALL-108, policy chỉ cảnh báo) (#9) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID267 | — | IssuedQty=0 | Failure (Validation) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID268 | — | IssuedQty=-5 | Failure (Validation) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID269 | Đơn xin 31 Pending (chưa duyệt) | RequestId=31 | Failure (InvalidStatus) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID270 | — | RequestId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID271 | BTP OrderLine 100 chưa phân công công đoạn cho tổ 3 | cấp BTP | Failure (OperationsNotAssigned) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.ErrorCode to be "ASSIGNMENT_INCOMPLETE", but found <null>. |  |
| UTCID272 | Phiếu BTP có lines không có OrderLineId | cấp BTP | Failure (Validation). ⚠ `lineIds` rỗng → pass check phân công (#5) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | System.InvalidOperationException : Nullable object must have a value. |  |

#### ConfirmAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID273 | Phiếu 50 Pending tổ 3, line M1 Issued=100; tồn 500; mock IStockService OK | issueId=50, dto Lines=[M1, Confirmed=100], teamId=3, employeeId=12 | Success; Status=Received; tồn −100; Request.MarkCompleted gọi | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID274 | Phiếu BTP; đơn Status=NEW, MaterialStatus=Ready | Confirmed = Issued | Success; CustomerOrder.StartProduction gọi → IN_PRODUCTION | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID275 | Issued=100 | Confirmed=99, DiscrepancyReason="Thiếu 1 cuộn" | Success; tồn −99 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID276 | Issued=100 | Confirmed=99, Reason=null | Failure (ReasonRequired) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID277 | Issued=100 | Confirmed=101 | Failure (ExceedsIssued) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID278 | Phiếu BTP | Mọi dòng Confirmed=0 (+ reason) | Failure (nhận 0 không hợp lệ) hoặc không StartProduction. ⚠ Hiện tại Received và StartProduction (#1) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID279 | Phiếu BTP; đơn NEW, MaterialStatus=Awaiting | Confirmed=Issued | Success; StartProduction **không** gọi, đơn vẫn NEW. Cần thông báo/log rõ (#4) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID280 | — | Confirmed=-1 | Failure (Validation). ⚠ Service không check (#2) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID281 | Phiếu 50 đã Received | issueId=50 | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID282 | Phiếu 50 thuộc tổ 3 | teamId=5 | Failure `TeamMismatch` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID283 | Phiếu 50 có 2 lines | dto chỉ gửi 1 line | Failure (MissingLines) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID284 | Tồn M1 đã bị phiếu khác lấy còn 50; mock Stock Failure | Confirmed=100 | Failure (InsufficientStock); Rollback; phiếu vẫn Pending (#3) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID285 | — | issueId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### RejectAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID286 | Phiếu 50 Pending tổ 3; mock Notifier | issueId=50, Reason="Sai mã NVL", teamId=3 | Success; Status=Rejected; Request vẫn Approved; Notifier được **await** | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID287 | Phiếu 50 Pending | Reason="" | Failure (Validation) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID288 | Phiếu 50 Pending | Reason=null | Failure (Validation). ⚠ NRE tại `.Trim()` (#7) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | System.NullReferenceException : Object reference not set to an instance of an object. |  |
| UTCID289 | Phiếu 50 tổ 3 | teamId=5 | Failure `TeamMismatch` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID290 | Phiếu 50 Received | Reason="x" | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID291 | Mock Notifier ném exception | Reason="x" | Lỗi được log/xử lý, không mất im lặng. ⚠ `NotifyEmployees` không await (#7) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | System.InvalidOperationException : notify failed |  |


---

## ProductSpecService

### Tóm tắt sheet

- **Code Module**: ProductSpecService | Passed
- **Number of TCs**: 41 | Pending
- **Testing Round**: Passed | Failed | Pending | N/A
- **Round 1**: 34 | 7 | 0
- **Round 2**: 41 | 0 | 0
- **Round 3**: 5 | 0 | 36
- **Testing Type**: Normal | Abnormal | Boundary | N/A
- **Count**: 7 | 22 | 12

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **41**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### GetAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID292 | Product 1: 3 Component (2 Active, 1 Discontinued), 2 Variant (Đỏ-M, Xanh-L), 4 Norm Active, 1 MarkerGroup có MarkerComponents + MarkerFabrics | productId=1 | `Result.Success`; Components=3 (đủ mọi status), Variants=2, Norms=4, MarkerGroups có Components/Fabrics | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |  |
| UTCID293 | Product 2 chưa có spec | productId=2 | Success; các list rỗng | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID294 | Product 1 có 1 Norm Discontinued; Component "Túi" Discontinued | productId=1 | Norm Discontinued **không** có trong DTO; Component "Túi" **có** trong DTO với Status=Discontinued | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |  |
| UTCID295 | — | productId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### SaveAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID296 | Product 1 có spec; mock Audit; UoW spy | dto hợp lệ (component, norm Đỏ/Xanh, marker group 1 PANEL 1 vải/màu) | Gọi CheckAsync → BeginTransaction → Apply → Commit → GetAsync; trả Spec mới; Audit log 1 lần | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |  |
| UTCID297 | Component Id=11 "Thân trước" | dto component {Id=11, Name="Thân trước trái"} | Success; component 11 đổi tên (không tạo mới) | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID298 | Component "Thân Trước" (không gửi Id) | dto component {Id=null, Name="thân trước"} | Khớp component cũ (case-insensitive), không tạo bản ghi mới (#2) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID299 | Component 12 đã dùng trong OperationComponent | dto bỏ component 12 | Component 12 Status=Discontinued (soft), không xóa cứng | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected comp.Status to be the same string, but they differ at index 0: |  |
| UTCID300 | — | PiecesPerGarment=1 | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID301 | Product 1 | Component PiecesPerGarment=0 | Failure (Validation: Pieces ∈ [1,99]); BeginTransaction không gọi | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Expected result.IsSuccess to be False, but found True. |  |
| UTCID302 | Component chưa dùng ở đâu | Components=[] (0 phần tử) | Success; component cũ bị xóa (hoặc Failure nếu spec bắt buộc ≥1) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID303 | Variant đơn: Đỏ, Xanh | dto có variant "Vàng" (mới) | Failure (VariantNotInOrder); không ghi | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | NSubstitute.Exceptions.ReceivedCallsException : Expected to receive no calls matching: |  |
| UTCID304 | Mock CheckAsync trả Failure (trùng tên component) | dto lỗi | Failure; BeginTransaction **không** gọi | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID305 | Mock GetAsync sau ghi trả Failure | dto hợp lệ | Trả Failure. ⚠ Hiện tại không kiểm `spec.IsSuccess`, có thể Success với Data null (#6) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID306 | — | productId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### CheckAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID307 | Product 1 | dto hợp lệ | Success; SaveChanges **không** gọi | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID308 | — | 2 component tên "Tay" và "tay " | Failure (DuplicateComponent) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID309 | — | productId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID310 | — | dto=null | `ArgumentNullException` hoặc Failure (Validation) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### WriteAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID311 | Đã gọi CheckAsync Success; đã BeginTransaction bên ngoài | productId=1, dto hợp lệ | `Result<int>` Success; dữ liệu ghi đúng sau khi Commit bên ngoài | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID312 | Không có norm nào | dto norm rỗng | Success; không ghi norm | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID313 | **Không** gọi CheckAsync; dto có marker group PANEL với 2 vải cho cùng màu Đỏ, và norm tham chiếu màu "Tím" không có trong ProductColor | productId=1 | Failure (Validation) an toàn. ⚠ `Single()` ném `InvalidOperationException` / `KeyNotFoundException` màu | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |  |
| UTCID314 | Operation link tham chiếu component tên cũ không còn trong `componentByName` | productId=1 | Bỏ qua/Failure an toàn. ⚠ `KeyNotFoundException` trong RemoveStaleOperationLinksAsync (#7) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected a <System.Exception> to be thrown, but no exception was thrown. |  |

#### CheckAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID533 | Product 1: Variants Đỏ-M, Xanh-L; ProductColor có thêm "Vàng" (color-only, chưa có size) | dto norm cho màu "Vàng" | Success (colors = Variants ∪ ProductColor) | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID535 | Product 1 | Component PiecesPerGarment=99 | Success (biên trên) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID536 | Product 1 | Component PiecesPerGarment=100 | Failure (Validation: Pieces ∈ [1,99]) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID537 | Product 1 | Component PiecesPerGarment=-1 | Failure (Validation) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID538 | Product 1 | 2 Variant trùng (Đỏ-M, Đỏ-M) | Failure (DuplicateVariant) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID539 | Product 1 không có màu "Tím" ở Variants lẫn ProductColor | norm màu "Tím" | Failure (ColorUnknown) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID540 | Product 1 | norm tham chiếu component "Cầu vai" không có trong dto | Failure (Norm reference invalid) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID541 | Vật tư M7 Status=Inactive | norm dùng M7 | Failure (MaterialInactive) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID542 | Product 1 | component Panel, màu Đỏ có 2 vải khác nhau | Failure (Panel chỉ được 1 vải/màu) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID543 | Product 1 | marker group Layout không hợp lệ (không có component PANEL) | Failure (MarkerGroup Layout) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID544 | Product 1 | marker group không phủ đủ component PANEL (Coverage thiếu) | Failure (MarkerGroup Coverage) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID545 | Product 1 | marker group vải không khớp vải norm (GroupFabric) | Failure (MarkerGroup Fabric) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID546 | Group G1 đã khóa (LockedGroup, có sơ đồ cắt) | dto sửa G1 | Failure (LockedGroup) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID547 | Group G2 đã dùng (InUse) | dto bỏ G2 | Failure (MarkerGroup InUse) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID548 | ProductColor có 2 màu cùng tên "Đỏ" (data lệch) | dto norm màu "Đỏ" | Báo lỗi trùng hoặc chọn đúng Id; không gán nhầm im lặng (rủi ro `First()`) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |

#### SaveAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID534 | Như trên | dto norm + marker group cho màu "Vàng" | Success; norm/marker "Vàng" được ghi; không tạo Variant mới | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |

#### WriteAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID549 | Gọi Write ngoài TX; mock SaveChanges ném exception sau bước ClearMarkerGroups | productId=1, dto hợp lệ | Exception propagate. Ghi nhận: marker group cũ đã bị xóa (partial commit) khi gọi ngoài TX | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |

#### GetAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID550 | Norm có MaterialId trỏ vật tư đã xóa (orphan navigation) | productId=1 | Bỏ qua/trả MaterialName=null an toàn. ⚠ `NullReferenceException` | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |


---

## WorkOrderService

### Tóm tắt sheet

- **Code Module**: WorkOrderService | Passed
- **Number of TCs**: 59 | Pending
- **Testing Round**: Passed | Failed | Pending | N/A
- **Round 1**: 49 | 10 | 0
- **Round 2**: 59 | 0 | 0
- **Round 3**: 0 | 0 | 59
- **Testing Type**: Normal | Abnormal | Boundary | N/A
- **Count**: 12 | 25 | 22

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **59**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### GetPagedAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID315 | 4 lệnh cắt: tổ cắt 1 (3 lệnh), tổ cắt 2 (1 lệnh) | query {Page=1}, teamId=1 | Success; 3 items, có cut/handed qty | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID316 | DB rỗng | query mặc định | Success; Items rỗng | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID317 | 2 lệnh Status raw "IN_PROGRESS" | Status="InProgress" (Pascal) | Trả 2 lệnh (chuẩn hóa ToCode như đơn hàng). ⚠ So raw string → rỗng (#8) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.Data!.Items to contain 2 item(s), but found 0: {empty}. |  |
| UTCID318 | — | Status="Foo" | Failure (Validation) hoặc list rỗng theo quy ước chung; không exception | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### GetByIdAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID319 | Lệnh 60 Open, lệnh thường, tổ cắt 1; mock WorkOrderFabricChecker trả 2 check | workOrderId=60, teamId=1 | Success; FabricChecks có 2 phần tử | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID320 | Lệnh 61 Open, là lệnh bù (replacement) | workOrderId=61 | FabricChecks rỗng (chỉ tính lệnh thường) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID321 | Lệnh 62 đang cắt (InProgress), kho thiếu vải | workOrderId=62 | Theo design không có FabricChecks. Ghi nhận rủi ro mất cảnh báo (#5) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID322 | Lệnh 60 tổ cắt 1 | teamId=2 | Failure `TeamMismatch` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID323 | — | workOrderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### CheckFabricAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID324 | Đơn 1: OrderLine 100 cần vải F1 120m; tồn F1 200m | dto {OrderId=1, Team=1, Lines=[100, Qty=300]} | Success; F1 đủ (Shortage=0) | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID325 | Tồn F1 = đúng nhu cầu 120m | như trên | F1 đủ, Shortage=0 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID326 | Tồn F1 = 119,99m | như trên | F1 thiếu 0,01m | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID327 | — | Lines=[] | Success; danh sách rỗng | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID328 | OrderLine 555 thuộc đơn khác | Lines=[100, 555] | Failure (Validation: line không thuộc đơn). ⚠ Hiện tại bỏ im lặng, trả list ngắn hơn (#1) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID329 | — | OrderId=999 | Failure `NotFound` | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |

#### GetFabricRequirementsAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID330 | Lệnh 60 cần F1, F2 | workOrderId=60 | Success; 2 dòng nhu cầu vải đúng định mức | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID331 | Lệnh 67: product chưa có định mức vải | workOrderId=67 | Success; list rỗng (không exception chia/null) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID332 | — | workOrderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### GetFabricNeedsAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID333 | Lệnh 60: F1 NeedMeters=50 | workOrderId=60 | Success; có F1 = 50 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID334 | F2 NeedMeters=0 (đã đủ) | workOrderId=60 | F2 không có trong kết quả (chỉ NeedMeters > 0) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.Data!.Where(n => n.MaterialId == FabricId) to be empty, but found at least one item |  |
| UTCID335 | — | workOrderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### GetMaterialAllowanceAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID336 | Lệnh 60 có định mức NVL | workOrderId=60 | Success; allowance đúng PlannedQty × định mức | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID337 | Lệnh 68 PlannedQty=1, định mức 0,25m | workOrderId=68 | Allowance = 0,25m (không làm tròn về 0) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID338 | — | workOrderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### CreateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID339 | Đơn 1 IN_PRODUCTION, ReceivedDate=2026-11-10; tổ 1 là tổ cắt; đủ vải; mock CodeGenerator → "LC2611-001"; mock Notifier | dto {OrderId=1, CuttingTeamId=1, DueDate=2026-11-25, Lines=[100, PlannedQty=300]} | Success DTO Status=Open, Code="LC2611-001"; Notifier gọi | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID340 | Như trên | DueDate=2026-11-22 (= Today) | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID341 | Như trên | DueDate=2026-11-21 (< Today) | Failure (DueDate invalid) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID342 | Đơn ReceivedDate=2026-11-22 (= Today) | DueDate=2026-11-22 (= ReceivedDate) | Failure (`DueDate <= ReceivedDate`). Xác nhận spec biên (#3) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID343 | Đơn ReceivedDate=2026-11-22 | DueDate=2026-11-23 | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID344 | — | PlannedQty=0 | Failure (Validation). ⚠ Service không check (#4) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID345 | — | Lines=[] | Failure (Validation) (#4) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID346 | — | PlannedQty=-10 | Failure (Validation) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID347 | Tồn F1 thiếu so với nhu cầu | dto hợp lệ (lệnh thường) | Failure (FabricShortage); không tạo lệnh | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID348 | Đơn 1 Stopped/Completed | dto hợp lệ | Failure (OrderClosed) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID349 | Tổ 3 là tổ may | CuttingTeamId=3 | Failure (NotCuttingTeam) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID350 | OrderLine 555 không thuộc đơn 1 | Lines=[555] | Failure (Validation) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID351 | Mock tạo lệnh trả `created.IsSuccess=false` | dto hợp lệ | Trả Failure. ⚠ Vẫn gọi `GetByIdAsync(created.Data)` (#7) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### GetOpenReplacementRequestsAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID352 | Request bù 40 Approved chưa có WO; request 41 Approved đã có WO; request 42 Pending | — | Success; chỉ trả request 40 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID353 | Không có request nào thỏa | — | Success; list rỗng | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID354 | Request 43 Approved nhưng OrderLine gốc bị xóa (FK lệch) | — | Success; request 43 bị bỏ qua hoặc trả field null; không exception | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | System.NullReferenceException : Object reference not set to an instance of an object. |  |

#### CreateReplacementAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID355 | Request bù 40 Approved; đủ vải | dto {RequestId=40, CuttingTeamId=1, DueDate=2026-11-24} | Success; lệnh bù Open, IsReplacement=true | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID356 | Request 40; kho thiếu vải | dto hợp lệ | Success + warning thiếu vải (lệnh bù không chặn, theo design) (#2) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID357 | Request 42 Pending | RequestId=42 | Failure (InvalidStatus) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID358 | Request 41 đã có WO | RequestId=41 | Failure (AlreadyHasWorkOrder) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID359 | — | RequestId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### UpdateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID360 | Lệnh 60 Open, cut qty=0 | workOrderId=60, DueDate=2026-11-28, PlannedQty=320 | Success; dữ liệu cập nhật | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID361 | Lệnh 60 Open | DueDate=2026-11-22 (= Today) | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID362 | Lệnh 63 đã cắt 50 | workOrderId=63 | Failure `InvalidStatus` (đã cắt) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID363 | — | workOrderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### CancelAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID364 | Lệnh 60 Open, chưa cắt, chưa cấp NVL | workOrderId=60 | Success; Status=Cancelled | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID365 | Lệnh 64 Open, cut qty=0 nhưng đã có phiếu cấp vải Received | workOrderId=64 | Failure, hoặc Success kèm hoàn kho rõ ràng; không orphan stock (#6) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID366 | Lệnh 63 đã cắt 50 | workOrderId=63 | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID367 | Lệnh 65 đã Cancelled | workOrderId=65 | Failure `InvalidStatus` (hoặc idempotent theo spec) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID368 | — | workOrderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### CloseShortAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID369 | Lệnh 63 InProgress, Planned=300, Cut=250 | workOrderId=63, Reason="Hết vải" | Success; Status=ClosedShort; BTP 250 giữ nguyên | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID370 | Lệnh 60 Cut=0 | workOrderId=60 | Failure (phải dùng Cancel) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID371 | Lệnh 66 Cut=Planned=300 | workOrderId=66 | Failure (đã đủ, không phải đóng thiếu) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID372 | Lệnh 65 Cancelled | workOrderId=65 | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID373 | — | workOrderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |


---

## DefectService

### Tóm tắt sheet

- **Code Module**: DefectService | Passed
- **Number of TCs**: 42 | Pending
- **Testing Round**: Passed | Failed | Pending | N/A
- **Round 1**: 36 | 6 | 0
- **Round 2**: 42 | 0 | 0
- **Round 3**: 0 | 0 | 42
- **Testing Type**: Normal | Abnormal | Boundary | N/A
- **Count**: 9 | 20 | 13

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **42**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### ListForTeamAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID374 | ICurrentUser = TeamLeader tổ 3; 5 phiếu tổ 3, 4 phiếu tổ 5 | query {Page=1} | Success; 5 phiếu tổ 3 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID375 | ICurrentUser = Manager | query {Page=1} | Success; 9 phiếu (mọi tổ) | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID376 | Phiếu tổ 3 ngày 2026-11-20 và 2026-11-21 | FromDate=ToDate=2026-11-20 | Chỉ phiếu ngày 2026-11-20 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID377 | — | Keyword="" | Không filter keyword | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID378 | TeamLeader tổ 3 | query {TeamId=5} | Chỉ dữ liệu tổ 3 hoặc Failure (Forbidden); **không** trả phiếu tổ 5 | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID379 | — | FromDate=2026-11-22, ToDate=2026-11-01 (From > To) | Failure (Validation) hoặc list rỗng | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### ListMineAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID380 | Công nhân W1 có 3 phiếu Active | workerId=W1 | Success; 3 phiếu | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID381 | W1 có 1 phiếu Cancelled | workerId=W1 | Phiếu Cancelled bị ẩn | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID382 | W1 có MaxPageSize + 1 phiếu | workerId=W1 | Có paging/Total để xem hết. ⚠ Hiện tại `Take(MaxPageSize)` cắt cụt, không báo total (#4) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.Data!.Count to be 101 because expected full list or paging total when worker has more than MaxPageSize defects, but found 100 (difference of -1). |  |
| UTCID383 | — | workerId=0 | Failure (Validation). ⚠ Không validate (#4) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |

#### CreateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID384 | TeamLeader tổ 3; OrderLine 100 Allotted tổ 3; OP10 Active; W1 thuộc tổ 3; kỳ mở; mock Notifier | dto {Type=Rework, OrderLineId=100, OperationId=OP10, WorkerId=W1, Quantity=5, Shift="CA1", Date=2026-11-22} | Success; phiếu tạo; Notifier được **await** | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID385 | Như trên; PieceRate OP10 APPROVED | Type=Scrap, Quantity=3 | Success; PieceRate bị khóa cho phiếu | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID386 | Như trên | Quantity=1 | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID387 | — | Quantity=0 | Failure (Validation) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID388 | Hàng trên chuyền = 50 | Type=Scrap, Quantity=1.000.000 | Failure (vượt MaxQty / vượt hàng trên chuyền). ⚠ Không có MaxQty (#1) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID389 | W1×OP10 đã báo sản lượng cả ngày = 20 (CA1 10 + CA2 10) | Type=OutputError, Quantity=20 | Success (= sản lượng đã báo) (#5) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID390 | Như trên | Type=OutputError, Quantity=21 | Failure (ExceedsReported) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID391 | — | Quantity=-1 | Failure (Validation) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID392 | ICurrentUser = Manager (không có tổ) | dto hợp lệ | Failure (chỉ TeamLeader được ghi) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID393 | ICurrentUser = Admin | dto hợp lệ | Failure (Forbidden), xác nhận role map (#2) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID394 | ProductionGuard: kỳ Locked | dto hợp lệ | Failure `PeriodLocked` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID395 | PieceRate OP10 chưa APPROVED | Type=Scrap | Failure `PieceRateNotApproved` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID396 | OrderLine 101 chưa chia cho tổ 3 | OrderLineId=101 | Failure (NotAllotted). ⚠ Hiện tại không check (#7) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID397 | ICurrentUser teamId=999 (team không tồn tại) | dto hợp lệ | Failure (Team NotFound). ⚠ `FirstAsync` ném `InvalidOperationException` (#8) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.Kind to be one of {ResultKind.NotFound {value: 4}, ResultKind.Validation {value: 1}, ResultKind.Forbidden {value: 3}}, but found ResultKind.Conflict {value: 5}. |  |
| UTCID398 | Mock Notifier ném exception | dto hợp lệ | Lỗi được xử lý/log; không mất im lặng trong transaction. ⚠ Không await (#6) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Did not expect System.InvalidOperationException because notifier errors must be logged/handled, not lost silently, but found System.InvalidOperationException: notify down |  |
| UTCID399 | OP99 không tồn tại | OperationId=OP99 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### UpdateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID400 | Phiếu 70 Rework qty=5, tạo bởi TeamLeader tổ 3, trong cửa sổ sửa | defectId=70, Quantity=4 | Success; qty=4; Audit log | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID401 | Phiếu 70 Rework; PieceRate APPROVED | Type=Scrap | Success; rate được khóa; CheckIncreaseWithinOnLine được gọi (#3) | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID402 | Phiếu 71 Scrap đang khóa rate | Type=Rework | Success; rate được release | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID403 | Now = hạn cuối cửa sổ sửa | defectId=70, Quantity=4 | Success (biên `<=`) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID404 | Now = hạn cuối + 1 giây, TeamLeader | defectId=70 | Failure (NotMutable) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID405 | — | Quantity=0 | Failure (Validation) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID406 | ICurrentUser = Manager | defectId=70, Reason=null | Failure (ReasonRequired) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID407 | Phiếu 70 Rework; rate OP10 chưa APPROVED | Type=Scrap | Failure `PieceRateNotApproved` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID408 | Phiếu 72 Cancelled | defectId=72 | Failure (NotMutable/InvalidStatus) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID409 | — | defectId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### CancelAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID410 | Phiếu 70 trong cửa sổ | defectId=70, Reason="Ghi nhầm" | Success; Status=Cancelled; rate (nếu Scrap) release | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID411 | Phiếu 70 | Reason="" | Failure (ReasonRequired) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID412 | Phiếu 70 | Reason=null | Failure (ReasonRequired), không NRE | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID413 | Phiếu 70 ngoài cửa sổ, TeamLeader | Reason="x" | Failure (NotMutable) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID414 | Phiếu 72 đã Cancelled | Reason="x" | Failure (InvalidStatus) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID415 | — | defectId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |


---

## CustomerOrderService

### Tóm tắt sheet

- **Code Module**: CustomerOrderService | Passed
- **Number of TCs**: 54 | Pending
- **Testing Round**: Passed | Failed | Pending | N/A
- **Round 1**: 44 | 10 | 0
- **Round 2**: 54 | 0 | 0
- **Round 3**: 0 | 0 | 54
- **Testing Type**: Normal | Abnormal | Boundary | N/A
- **Count**: 12 | 25 | 17

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **54**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### ListAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID416 | 5 đơn: 2 NEW, 2 IN_PRODUCTION, 1 CANCELLED | query {Status="InProduction", Page=1} | Success; 2 đơn | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID417 | 5 đơn | Status=null, Page=1, PageSize=2 | Items=2, Total=5 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID418 | — | Status="Foo" | Failure (Validation 400) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### ListOverviewAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID419 | 5 đơn có tiến độ | query {Page=1} | Success; overview gồm % tiến độ từng đơn | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID420 | DB rỗng | query mặc định | Success; rỗng | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID421 | — | Status="Foo" | Failure (Validation 400) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### SuggestCodeAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID422 | Today=2026-11-22; đã có DH2611-001…DH2611-007 | — | "DH2611-008" | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID423 | Chưa có đơn tháng 11/2026 (chỉ có DH2610-xxx) | — | "DH2611-001" | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID424 | Đã có DH2611-999 | — | "DH2611-1000" (hoặc theo quy tắc độ dài; không tràn/lỗi format) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID425 | Có mã "DH2611-ABC" (suffix không phải số) và DH2611-003 | — | "DH2611-004" (bỏ qua suffix không phải số) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID426 | 2 request gọi đồng thời, cùng DB state DH2611-007 | 2 lần gọi | Cả 2 trả "DH2611-008"; CreateAsync thứ 2 phải Failure `CodeDuplicate`, không trùng dữ liệu (#1) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### GetAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID427 | Đơn 1 có 3 dòng + 2 bản ghi audit | orderId=1 | Success; Lines=3; History=2 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID428 | Đơn 7 NEW, 0 dòng, chưa có audit | orderId=7 | Success; Lines rỗng; History rỗng | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID429 | — | orderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### GetOverviewAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID430 | Đơn 1 | orderId=1 | Success overview (cut/sewn/dispatched) | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID431 | Đơn 7 chưa cắt/may/xuất | orderId=7 | Success; tiến độ 0% (không chia cho 0 khi ordered=0) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID432 | — | orderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### CreateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID433 | Product P1 có sẵn; mock Notifier, Audit | dto {OrderCode="DH2611-008", CustomerId=1, ReceivedDate=2026-11-22, DueDate=2026-12-20, Lines=[P1, Đỏ, 500]} | Success; OrderId > 0; Status=NEW; Audit log | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID434 | — | dto có StagedProducts=[{TempId=-1, Code="AO-MOI"}], Lines=[ProductId=-1, 200] | Success; product AO-MOI được tạo; line bind đúng ProductId thật | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID435 | — | Lines có 1 dòng | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID436 | — | Lines=[] | Failure (Validation) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID437 | — | DueDate = ReceivedDate = 2026-11-22 | Theo spec (Success hoặc Failure); ghi rõ kết quả | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID438 | — | OrderedQty=0 trên 1 dòng | Failure (Validation) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID439 | — | DueDate=2026-11-21 < ReceivedDate=2026-11-22 | Failure (Validation). ⚠ Service không check tường minh (#6) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID440 | Đã có DH2611-008 | OrderCode="DH2611-008" | Failure `CodeDuplicate` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID441 | Product "AO-01" đã có trong DB | StagedProducts Code="AO-01" | Failure (ProductCodeDuplicate); không tạo đơn | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID442 | — | 2 StagedProducts cùng Code="AO-MOI" | Failure (DuplicateInPayload) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID443 | — | StagedProducts TempId=-2 không dòng nào dùng | Failure hoặc warning rõ. ⚠ Hiện tại bỏ qua im lặng (#3) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID444 | — | dto=null | `ArgumentNullException` hoặc Failure (Validation) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### UpdateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID445 | Đơn 1 NEW, RowVer=v1 | orderId=1, dto đổi DueDate, thêm dòng, RowVer=v1 | Success; full edit được áp dụng | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID446 | Đơn 2 IN_PRODUCTION (LimitedEdit) | sửa ghi chú/DueDate | Success; chỉ trường được phép thay đổi | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID447 | Đơn 2 IN_PRODUCTION, dòng 200 đã cắt 300 | OrderedQty=300 (= đã cắt) | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID448 | Như trên | OrderedQty=299 (< đã cắt) | Failure (BelowProduced) (#2) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False because ordered qty must not go below already produced/cut quantity, but found True. |  |
| UTCID449 | Đơn 2 IN_PRODUCTION, dòng 200 đã sản xuất | xóa dòng 200 | Failure (LineInUse) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID450 | Đơn 1 RowVer=v2 | RowVer=v1 | Failure (Concurrency) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.Kind to be ResultKind.Conflict {value: 5}, but found ResultKind.Ok {value: 0}. |  |
| UTCID451 | Đơn 3 CANCELLED | dto hợp lệ | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID452 | — | orderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### CancelAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID453 | Đơn 1 NEW, RowVer=v1; mock OrderCloseCascade | orderId=1, Reason="Khách hủy", RowVer=v1 | Success; Status=CANCELLED; cascade gọi 1 lần | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID454 | Đơn 1 NEW | Reason="" | Failure (ReasonRequired) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID455 | Đơn 2 IN_PRODUCTION | Reason="x" | Failure `InvalidStatus` (message gợi ý dùng Stop) (#4) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID456 | Đơn 1 RowVer=v2 | RowVer=v1 | Failure (Concurrency) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.Kind to be ResultKind.Conflict {value: 5}, but found ResultKind.Ok {value: 0}. |  |
| UTCID457 | — | orderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID458 | CancellationToken đã cancel | orderId=1 | `OperationCanceledException`. ⚠ `GetByIdAsync` không truyền ct (#7) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected a <System.OperationCanceledException> to be thrown, but no exception was thrown. |  |

#### StopAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID459 | Đơn 2 IN_PRODUCTION, đã xuất 40% | orderId=2, Reason="Khách dừng" | Success; Status=STOPPED; cascade gọi | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID460 | Đơn 4 PARTIALLY_DELIVERED, chưa xuất đủ | Reason="x" | Success (xác nhận định nghĩa "đủ xuất") (#5) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID461 | Đơn 5 IN_PRODUCTION, 0 dòng (ordered=0) | Reason="x" | Failure, hoặc hành vi được định nghĩa rõ; không Stop bất thường (#5) | Boundary | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID462 | Đơn 6 đã xuất đủ 100% (FullyDispatched) | Reason="x" | Failure (FullyDispatched) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID463 | Đơn 3 CANCELLED | Reason="x" | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID464 | — | orderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### StartProductionAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID465 | Đơn 1 NEW, MaterialStatus=Ready | orderId=1 | Success; Status=IN_PRODUCTION | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID466 | Đơn 1 NEW, MaterialStatus=Partial (khác Awaiting) | orderId=1 | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID467 | Đơn 1 NEW, MaterialStatus=Awaiting | orderId=1 | Failure (MaterialAwaiting) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID468 | Đơn 2 IN_PRODUCTION | orderId=2 | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID469 | — | orderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |


---

## DailyWorkAllocationService

### Tóm tắt sheet

- **Code Module**: DailyWorkAllocationService | Passed
- **Number of TCs**: 59 | Pending
- **Testing Round**: Passed | Failed | Pending | N/A
- **Round 1**: 52 | 7 | 0
- **Round 2**: 59 | 0 | 0
- **Round 3**: 0 | 0 | 59
- **Testing Type**: Normal | Abnormal | Boundary | N/A
- **Count**: 10 | 25 | 24

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **59**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### GetBoardAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID470 | Tổ may 3; đơn 1; OrderLine 100 Allotted tổ 3; Assignment W1×OP10; Carry=20, DayNet=30, DayPending=0, DayPendingRetrieval=0, DayOutput=25, Held=100, Output=60; rate APPROVED | teamId=3, workDate=2026-11-22, orderId=1, orderLineId=100 | Success; DayGiven=30; Remaining = min(20+30−25, 100−60) − 0 = 25; có Unallocated, Rate | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID471 | Đơn 1 có OrderLine 100, 101 Allotted, 102 không Allotted | orderLineId=null | Board gồm dòng 100 và 101; không có 102 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID472 | Carry+DayNet−DayOutput = -5; DayPendingRetrieval=0 | như trên | Remaining = 0 (floor 0) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID473 | Đơn có OrderLine nhưng 0 assignment/position | orderLineId=100 | Success; rows rỗng | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID474 | — | workDate=2026-11-21 (quá khứ) | Success board (chỉ xem); FE phải biết không Save được (#1) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID475 | Handover CONFIRMED có ConfirmedQty=null (data lỗi) | như trên | Remaining tính an toàn, không âm/sai lệch. ⚠ `?? 0` làm Held sai (#2) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID476 | — | teamId=999 | Failure (Team NotFound). ⚠ Hiện tại không validate (#1) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID477 | — | orderId=999 | Failure `NotFound`. ⚠ Không validate (#1) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False because unknown order should be validated as NotFound per business spec, but found True. |  |
| UTCID478 | Row có OperationId=777 không có trong dictionary `sequence` | như trên | Đẩy xuống cuối hoặc bỏ qua. ⚠ `KeyNotFoundException` khi sort (#7) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### SaveAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID479 | Tổ may 3; W1 thuộc tổ; line 100 Allotted, đơn mở; Unallocated OP10 = 50; rate APPROVED; mock IAppLock OK, Notifier | dto {WorkDate=2026-11-22, Items=[(W1,100,OP10,Qty=30)]} (đang 0 → 30), teamId=3 | Success {Added=1, Adjusted=0}; tạo Handover PENDING 30, ConfirmMode=WORKER; message `DailySavedPendingConfirm`; AppLock theo line; Notifier được gọi | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID480 | W1×OP10 DayGiven=30, toàn PENDING (DayPending=30) | Qty=20, Reason="Đổi việc" | Success {Adjusted=1}; PENDING giảm còn 20 (hạ pending trước, REJECTED/giảm qty); **không** tạo Retrieval; message `PieceMovementCreated` (chỉ hạ pending → không awaiting) | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID481 | W1×OP10 DayGiven=30 (10 PENDING + 20 CONFIRMED), Remaining=15, DayPendingRetrieval=0 | Qty=15, Reason="Điều chuyển" | Success {Adjusted=1}; hủy/giảm 10 PENDING + tạo **Retrieval ADJUSTED Qty=5**, Status=**PENDING**, ConfirmMode=**WORKER** (DEV-ALL-157, không còn Confirmed ngay); message `DailySavedPendingConfirm`; Notifier báo W1 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID482 | Unallocated OP10 = 50, đang giao 0 | Qty=50 (delta = unallocated) | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID483 | Unallocated = 50 | Qty=51 | Failure (OverUnallocated) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID484 | W1×OP10 hiện 30 | Qty=30 (delta=0) | Success {Added=0, Adjusted=0}; không ghi DB; không Notifier | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID485 | — | WorkDate=2026-11-21 (Today−1) | Failure (WorkDateMustBeToday) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID486 | — | WorkDate=2026-11-23 (Today+1) | Failure (WorkDateMustBeToday) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID487 | — | Items=[] | Success 0/0 hoặc Failure Validation; không lấy lock | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID488 | DayGiven=10 toàn PENDING, Remaining=0 | Qty=0, Reason="x" | Success; chỉ hủy PENDING, không tạo Retrieval (#4) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID489 | — | Qty=-1 | Failure (Validation). ⚠ Cần xác nhận ValidateAsync có chặn (#6) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.Kind to be ResultKind.Validation {value: 1}, but found ResultKind.Conflict {value: 5}. |  |
| UTCID490 | — | Items=[(W1,100,OP10,10),(W1,100,OP10,20)] (trùng key) | Failure (DuplicateItem). ⚠ Nguy cơ delta kép (#6) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | Expected result.IsSuccess to be False, but found True. |  |
| UTCID491 | W1×OP10 hiện 30 | Qty=20, Reason=null | Failure (ReasonRequired) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID492 | DayGiven=30 (0 PENDING, 30 CONFIRMED), Remaining=5 | Qty=20 (cần hạ 10 từ confirmed > Remaining 5), Reason="x" | Failure (fromConfirmed > Remaining) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID493 | W9 thuộc tổ 5 | Items=[(W9,100,OP10,10)] | Failure (WorkerNotInTeam) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID494 | OrderLine 102 không Allotted cho tổ 3 | Items=[(W1,102,OP10,10)] | Failure (NotAllotted) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID495 | Đơn của line 100 đã Stopped | dto hợp lệ | Failure (OrderClosed) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID496 | Tổ 1 là tổ cắt | teamId=1 | Failure (NotSewingTeam) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID497 | — | teamId=999 | Failure (Team NotFound) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID498 | PieceRate OP10 chưa APPROVED | Qty=10 | Failure `PieceRateNotApproved` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID499 | PieceRate OP10 chưa APPROVED, W1 hiện 10 | Qty=0, Reason="x" | Success (Qty = 0 không cần rate) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID500 | Mock IAppLock timeout | dto hợp lệ | Failure (Busy); không SaveChanges | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | System.TimeoutException : busy |  |
| UTCID501 | Mock Notifier ném exception | dto hợp lệ (giao thêm hoặc hạ confirmed) | Lỗi được log/báo; dữ liệu phân việc vẫn commit. ⚠ `NotifyWorkers` không await (#5) | Abnormal | Failed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  | System.InvalidOperationException : notify |  |

#### GetBoardAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID502 | W1×OP10: DayNet=20, DayPending=10, DayPendingRetrieval=5 (có Retrieval PENDING), DayOutput=0, Carry=0, Held=20, Output=0 | teamId=3, workDate=Today, orderLineId=100 | Success; **DayGiven = 20+10−5 = 25**; PendingQty hiển thị/đi kèm phản ánh đúng phần chờ | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID503 | W1×OP10: Carry=0, DayNet=30, DayOutput=10, Held=30, Output=10, DayPendingRetrieval=15 | như trên | Remaining = max(0, min(0+30−10, 30−10) − 15) = max(0, 20−15) = **5** (trừ DayPendingRetrieval) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### SaveAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID504 | W1×OP10: 0 PENDING, 30 CONFIRMED, Remaining≥20, DayPendingRetrieval=0; mock Notifier | Qty=10, Reason="Giảm ca" (hạ 20 từ confirmed) | Success {Adjusted=1}; **1 Retrieval** MovementType=Retrieval/ADJUSTED, Qty=20, Status=**PENDING**, ConfirmMode=**WORKER**, ConfirmedQty=null, ConfirmedAt=null; RetrievalReason=Adjusted; message `DailySavedPendingConfirm`; Notifier gọi W1 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID505 | Seed: DayNet=30, DayPending=0, DayPendingRetrieval=10 → DayGiven=20; Remaining=8 | Qty=15, Reason="Hạ tiếp" (hạ thêm 5 từ confirmed) | Success; thêm Retrieval PENDING Qty=5; sau Save tổng DayPendingRetrieval=15; Remaining mới = 8−5=3 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID506 | DayNet=30, DayPending=0, DayPendingRetrieval=10, Remaining=0 (đã trừ hết) | Qty=15 (&lt; DayGiven 20 nhưng fromConfirmed=5 &gt; Remaining 0), Reason="x" | Failure `PieceRetrievalExceedsHolding` (không tạo Retrieval mới) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID507 | DayNet=30, DayPending=0, DayPendingRetrieval=0, DayOutput=25, Carry=0 → không được hạ dưới sản lượng đã báo | Qty=4 (hạ 26 → Carry+DayNet−fromConfirmed &lt; DayOutput), Reason="x" | Failure `DailyBelowOutput` / `PieceRetrievalExceedsHolding` | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID508 | DayPending=5, DayNet=20 (Confirmed), Remaining đủ; cut=5 (= DayPending) | Qty = DayGiven−5, Reason="x" | Success; chỉ WithdrawPending; **0** Retrieval mới; message `PieceMovementCreated` (không awaiting vì `-delta == DayPending`, không `>`) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID509 | DayPending=5, DayNet=20; cut=8 | Qty = DayGiven−8, Reason="x" | Success; Withdraw 5 PENDING + Retrieval PENDING Qty=3; message `DailySavedPendingConfirm`; Notifier | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID510 | Đã có Retrieval PENDING Qty=10 cho W1×OP10 | GetBoard / Save không đổi Qty | DayGiven đã trừ 10; không double-count khi LoadPositions (pendingRetrieval group theo key) | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID511 | W1 vừa hạ confirmed → Retrieval PENDING; Unallocated tăng lại `+cut` | Sau Save, gọi Save giao thêm Qty tăng trong hạn Unallocated mới | Success; Unallocated đã được cộng lại khi hạ (free tăng) nên giao thêm trong hạn mới được | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-23 00:00:00 | Khiêm | Pending |  |  |  |  |

#### SettleOnTeamLeaveAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID551 | Tổ may 3. W1 trên line 100 có: Handover H1 PENDING, Retrieval R1 PENDING, Position Remaining=10, OperationAssignment OA1 ValidTo=null; mock IAppLock OK | oldTeamId=3, workerIds=[W1] | Success; lock key "work-allocation:3:100"; H1 → Rejected kèm reason; R1 → Confirmed (System) kèm note; thêm Retrieval ADJUSTED qty=10 Status=**CONFIRMED** (System), khác luồng SaveAsync tạo PENDING/WORKER ở UTCID504; OA1.ValidTo=2026-11-24; SaveChanges 2 lần | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID552 | W1×OP10 line 100: Retrieval R1 PENDING qty=5 (DayPendingRetrieval=5); Remaining hiển thị = 15 (đã trừ 5 theo DEV-ALL-157) | oldTeamId=3, [W1] | R1 → Confirmed (System) qty=5; Retrieval ADJUSTED CONFIRMED qty=15. Tổng thu hồi = 20 = toàn bộ hàng đang giữ (không trừ kép phần R1, không thu thừa) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID553 | W1 có allocation trên line 100 và 101 | oldTeamId=3, [W1] | Lock lần lượt "…:3:100" và "…:3:101"; xử lý pending + Remaining cho cả 2 line | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID554 | — | oldTeamId=3, workerIds=[] | Success ngay; không query, không lock, không SaveChanges | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID555 | Như case Normal | workerIds=[W1,W1] | Xử lý W1 đúng 1 lần (Distinct); chỉ 1 Retrieval ADJUSTED | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID556 | W1 không có HO/RT PENDING; Remaining=5 | [W1] | SaveChanges đầu (cho pending) **không** gọi; thêm Retrieval 5; SaveChanges cuối gọi 1 lần | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID557 | Position Remaining=0 | [W1] | Không thêm Retrieval ADJUSTED | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID558 | Position Remaining=1 | [W1] | Thêm Retrieval ADJUSTED CONFIRMED qty=1 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID559 | OA2 đã có ValidTo=2026-11-20 | [W1] | OA2 giữ nguyên ValidTo=2026-11-20 (chỉ đóng assignment ValidTo=null) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID560 | W2 không có WorkAllocation nào; OA3 ValidTo=null | [W2] | Không lock; OA3.ValidTo=Today; Success | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID561 | — | oldTeamId=999 (không tồn tại), [W1] | Success no-op; không ghi DB. Ghi nhận rủi ro no-op im lặng | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID562 | Tổ 1 là tổ cắt | oldTeamId=1, [W1] | Success no-op; không lock, không SaveChanges | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID563 | Tổ văn phòng Office1 | oldTeamId=Office1, [E4] | Success no-op | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID564 | Lock line 100 OK, lock line 101 timeout; gọi trong TX của caller | oldTeamId=3, [W1] | Failure `TeamOrderBusy`; caller Rollback; không thay đổi nào được commit | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID565 | Gọi **ngoài** TX; SaveChanges lần 2 ném exception | [W1] | Exception propagate. Ghi nhận partial commit: H1/R1 đã đổi trạng thái ở lần Save 1 | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID566 | H1 PENDING thuộc người nhận W5 | [W1] | H1 Rejected; người liên quan nên được thông báo. ⚠ Không gọi notifier | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID567 | — | workerIds=null | `ArgumentNullException` hoặc coi như rỗng (Success) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |


---

## VehicleService

### Tóm tắt sheet

- **Code Module**: VehicleService | Passed
- **Number of TCs**: 41 | Pending
- **Testing Round**: Passed | Failed | Pending | N/A
- **Round 1**: 39 | 2 | 0
- **Round 2**: 41 | 0 | 0
- **Round 3**: 0 | 0 | 41
- **Testing Type**: Normal | Abnormal | Boundary | N/A
- **Count**: 7 | 18 | 16

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **41**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### GetLiveStatusAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID512 | Xe 1 Active có 1 Trip STARTED (DriverName="Tài xế", OdometerStart=1000); Xe 2 Active không chuyến mở | query `{}` (Running=null) | Success; 2 dòng: (1, IsRunning=true, OpenTripCount=1), (2, IsRunning=false, OpenTripCount=0); xe đang chạy xếp trước | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID513 | Như UTCID512 | Running=true | Success; đúng 1 xe Id=1; CurrentTripId / DriverName / OdometerStart khớp chuyến STARTED | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID514 | Như UTCID512 | Running=false | Success; đúng 1 xe Id=2 | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID515 | Xe 1 Plate `51C-123.45`, Type `Xe tải` | Keyword=`51C` | Success; chỉ xe 1 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID516 | Keyword trim khoảng trắng | Keyword=`  51C  ` | Success; giống UTCID515 (đã Trim) | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID517 | Xe 1 có **2** Trip STARTED (data lệch) | Running=null | Success; IsRunning=true, **OpenTripCount=2**; CurrentTripId = chuyến mới nhất (OrderBy TripDate/Id desc). FE hiện cảnh báo | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID518 | 0 xe trong DB | query `{}` | Success; Items rỗng, TotalItems=0 | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID519 | Xe có Trip COMPLETED + STARTED | Running=true | Chỉ tính STARTED; LastOdometer lấy Max(OdometerEnd ?? OdometerStart) trên mọi trip | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID520 | — | Keyword=null, Running=null | Success; không exception, tương đương không lọc keyword | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |

#### StartTripAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID521 | Xe 1 Active, không Trip STARTED; mock `IAppLock` + UoW transaction spy | StartTripDto {VehicleId=1, TripDate=Today, OdometerStart=1000, DriverName="A"} | Success; Trip Status=STARTED; **AcquireAsync(`vehicle-trip:1`)** được gọi 1 lần; chạy trong `InTransactionAsync`; Audit Insert | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID522 | Xe 1 Active; chuyến COMPLETED gần nhất OdometerEnd=1000 | OdometerStart=1000 | Success; KmGapWarning=false | Normal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID523 | Chuyến COMPLETED gần nhất OdometerEnd=1000 | OdometerStart=1100 | Success; KmGapWarning=true; message chứa cảnh báo lệch km | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID524 | Xe 1 Active, không trip | TripDate=Today (biên hôm nay) | Success | Boundary | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID525 | Xe 1 đã có Trip STARTED | dto hợp lệ | Failure `VehicleTripInvalid` / `TripAlreadyOpen`; **vẫn gọi AcquireAsync** trước khi fail; không Add trip mới | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID526 | Mock `IAppLock.AcquireAsync` ném timeout / trả fail | dto hợp lệ | Failure (Busy/lock); không SaveChanges / không tạo Trip | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID527 | Xe Status ≠ Active | dto.VehicleId=1 | Failure `VehicleInactive` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID528 | Không có xe 999 | VehicleId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID529 | — | TripDate=Today+1 | Failure `TripDateFuture` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID530 | DispatchNoteId=55 không tồn tại | dto có DispatchNoteId=55 | Failure `NotFound` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID531 | Đã có trip cùng ngày + cùng OdometerStart | dto trùng | Failure `TripDuplicate` | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |

#### PrepareTripAsync / StartTrip

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID532 | Hai lần StartTrip tuần tự (mock lock no-op): lần 1 Success tạo STARTED | Lần 2 cùng VehicleId | Lần 2 Failure `TripAlreadyOpen` (check code). Race thật + `UQ_VT_OneOpen` → **Integration Test**, không thay bằng unit | Abnormal | Passed | 2026-11-22 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |

#### CompleteTripAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Test date | Tester | Round 3 | Test date | Tester | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID568 | Trip T1 Status=Started, OdometerStart=10.000; định mức 12L/100km, tolerance 10% (`ISystemSettingReader`) | tripId=T1, dto {OdometerEnd=10.150, FuelLiters=15, FuelCost=null} | Success; Status=Completed; End=10.150, Liters=15; FuelOverNormWarning=false (ngưỡng 19,8L); Audit + SaveChanges | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID569 | T1 Started; có attachment FuelReceipt IsCurrent=true | dto {OdometerEnd=10.150, FuelLiters=15, FuelCost=500.000} | Success; FuelCost=500.000 lưu | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID570 | T1 Start=10.000 | OdometerEnd=10.000 (= Start) | Failure Validation, field `odometerEnd` (`<=`) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID571 | T1 Start=10.000 | OdometerEnd=10.001 | Success | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID572 | 150km × 12L/100km = 18L, tolerance 10% → ngưỡng 19,8L | FuelLiters=19,8 | Success; FuelOverNormWarning=false | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID573 | Như trên | FuelLiters=19,81 | Success (vẫn lưu); FuelOverNormWarning=true | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID574 | T1 Started, không attachment | FuelLiters=null, FuelCost=null | Success; không yêu cầu ảnh | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID575 | T1 Started, không attachment | FuelLiters=20, FuelCost=null | Success (chỉ bắt ảnh khi FuelCost ≠ null, rủi ro #1). Xác nhận spec | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID576 | Có FuelReceipt IsCurrent | FuelCost=1 | Success (biên dưới validator) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID577 | Có FuelReceipt IsCurrent | FuelCost=100.000.000 | Success (biên trên validator) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID578 | Có FuelReceipt IsCurrent | FuelCost=100.000.001 | Failure Validation (validator ngoài service) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID579 | Có FuelReceipt IsCurrent | FuelCost=0 hoặc -1 | Failure Validation (FuelCost phải nguyên dương) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID580 | T1 không có attachment FuelReceipt | FuelCost=500.000 | Failure Validation, field `fuelCost`, code `FuelReceiptRequired`; trip vẫn Started | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID581 | T1 có FuelReceipt nhưng IsCurrent=false (đã thay) | FuelCost=500.000 | Failure `FuelReceiptRequired` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID582 | T1 Start=10.000 | OdometerEnd=9.990 | Failure Validation `odometerEnd` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID583 | — | tripId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID584 | T2 đã Completed | tripId=T2 | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID585 | T3 chưa Start (Planned) | tripId=T3 | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID586 | Có FuelReceipt IsCurrent | FuelCost=500.000 | Bản ghi Audit "after" chứa FuelCost. ⚠ Audit after không ghi FuelCost (rủi ro #5) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |
| UTCID587 | 2 request Complete đồng thời cho T1 | tripId=T1 × 2 | Request thứ 2 Failure (`InvalidStatus`/Concurrency). ⚠ Complete không lock xe (rủi ro #6) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm | Pending |  |  |  |  |


---

## FinanceService

### Tóm tắt sheet

- **Code Module**: FinanceService
- **Number of TCs**: 72
- **Testing Round**: Passed | Failed | Pending
- **Round 1**: 64 | 8 | 0
- **Round 2**: 72 | 0 | 0
- **Round 3**: 0 | 0 | 0
- **Testing Type**: Normal | Abnormal | Boundary
- **Count**: 13 | 27 | 32

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **72**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### GetRevenueAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID588 | View doanh thu tháng 2026-10: (O1,P1), (O1,P2), (O2,P3); mock GetOrderCodesAsync, GetProductsAsync trả đủ mã | query {PeriodMonth="2026-10", Page=1, PageSize=10} | Success `PagedResult`: Total=3, Items có OrderCode/ProductCode đúng | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID589 | Như trên | query {PeriodMonth="2026-10", OrderId=O1} | Total=2 (chỉ đơn O1) | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID590 | Như trên | query {PeriodMonth="2026-10", ProductId=P2} | Total=1 | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID591 | Dữ liệu tháng 2026-09 (2 dòng) và 2026-10 (3 dòng) | PeriodMonth=null | Trả cả 5 dòng (không lọc tháng) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID592 | Như trên | PeriodMonth="" | Trả 5 dòng (coi như không lọc), không báo invalid | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID593 | — | PeriodMonth="2026-01" | Success (tháng biên dưới hợp lệ) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID594 | — | PeriodMonth="2026-12" | Success (tháng biên trên hợp lệ) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID595 | Không có dòng nào tháng 2026-11 | PeriodMonth="2026-11" | Success; Items rỗng; Total=0; Message = `NoData` | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID596 | 3 dòng | Page=2, PageSize=2 | Items=1; Total=3 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID597 | 3 dòng | Page=0, PageSize=0 | Normalize về Page=1, PageSize mặc định; không exception | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID598 | 3 dòng; MaxPageSize=100 | PageSize=10000 | PageSize bị chặn về 100 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID599 | Dòng có FinishedQty=null, Revenue=null | PeriodMonth="2026-10" | DTO FinishedQty=0, Revenue=0 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID600 | — | PeriodMonth="2026-13" | Failure `RevenuePeriodMonthInvalid` (Validation); GetRevenueRowsAsync không gọi | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID601 | — | PeriodMonth="2026/10" | Failure `RevenuePeriodMonthInvalid` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID602 | Dòng có OrderId=O9 không có trong GetOrderCodesAsync | PeriodMonth="2026-10" | Success; OrderCode="" (không NotFound, không exception) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID603 | — | query=null | `ArgumentNullException` hoặc Failure (Validation). ⚠ Không null-check (rủi ro #5) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |

#### GetRevenueFilterOptionsAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID604 | Doanh thu: O1 (P1,P2), O2 (P3) | — | Success; Orders=[O1,O2] sort theo mã; Products=[P1,P2,P3] sort theo mã | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID605 | View doanh thu rỗng | — | Success; Orders rỗng, Products rỗng | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID606 | O1 có doanh thu ở 3 tháng khác nhau | — | O1 chỉ xuất hiện 1 lần (GroupBy) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID607 | Cùng OrderId=O1 nhưng 2 dòng có code khác nhau (data lệch) | — | Success, lấy code đầu tiên; không exception | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID608 | Product P3 đã soft-delete nhưng còn doanh thu | — | P3 vẫn hiện trong Products (xác nhận spec: có nên ẩn hay không) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID609 | Mock repo ném exception | — | Exception propagate (không nuốt) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |

#### ExportRevenueAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID610 | 3 dòng tháng 2026-10; mock `IFinanceExcelWriter.WriteRevenueSheet` trả byte[] | query {PeriodMonth="2026-10"} | Success ExcelFileDto: FileName="DoanhThu.xlsx", Content không rỗng; Message=`ExcelExported`; writer nhận 3 DTO | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID611 | Không có dữ liệu tháng 2026-11 | PeriodMonth="2026-11" | Success; writer được gọi với list rỗng | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID612 | 5 dòng nhiều tháng | PeriodMonth=null | Xuất toàn bộ 5 dòng (không phân trang) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID613 | — | PeriodMonth="2026-00" | Failure `RevenuePeriodMonthInvalid`; writer không gọi | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID614 | Mock writer ném exception | PeriodMonth="2026-10" | Exception propagate (không wrap thành Result) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |

#### CalculateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID615 | Period 2026-10 sẵn sàng (payroll Approved, có monthly cost). Doanh thu: P1 qty=1.000 / 20.000.000đ; P2 qty=500 / 15.000.000đ. Định phí 9.000.000đ. Biến phí P1=8.000.000đ, P2=6.000.000đ. Giờ công P1=600h, P2=400h | dto {PeriodMonth="2026-10"} | Success; Items=4 (2 product × OUTPUT + LABOUR_HOURS). **P1·OUTPUT**: share 2/3, Allocated=6.000.000, Cmt=20.000, VarPerUnit=8.000, BreakEvenQty=500, Percent=200, Surplus=6.000.000, IsAtRisk=false. **P1·LABOUR**: share 0,6, Allocated=5.400.000. Repo Add 4 entity, Audit, SaveChanges 1 lần | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID616 | Như trên + P3 qty=500 / 5.000.000đ, biến phí 4.000.000đ, Allocated OUTPUT=3.000.000 | PeriodMonth="2026-10" | P3: Surplus = −2.000.000, IsAtRisk=true ở cả 2 basis; AtRiskWarnings chỉ chứa P3 **1 lần** (Distinct) | Normal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID617 | Product có Cmt = VarPerUnit (Revenue 10.000.000, qty 1.000, biến phí 10.000.000) | PeriodMonth="2026-10" | BreakEvenQty=null, PercentReached=null (điều kiện `Cmt <= VarPerUnit`) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID618 | Cmt < VarPerUnit (biến phí 12.000.000, qty 1.000, revenue 10.000.000) | PeriodMonth="2026-10" | BreakEvenQty=null, PercentReached=null; IsAtRisk=true | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID619 | Tổng định phí = 0 | PeriodMonth="2026-10" | AllocatedFixed=0, BreakEvenQty=0, PercentReached=100 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID620 | Có dòng doanh thu P4 với FinishedQty=0, Revenue=2.000.000 | PeriodMonth="2026-10" | P4: Cmt=0, VarPerUnit=0, BreakEvenQty=null; Surplus = Revenue − Var − Allocated (rủi ro #4) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID621 | Tổng qty mọi product = 0 | PeriodMonth="2026-10" | outputShare=0 → AllocatedFixed=0 cho basis OUTPUT; không lỗi chia 0 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID622 | P2 không có giờ công (LabourShares thiếu P2) | PeriodMonth="2026-10" | Share LABOUR của P2 fallback = outputShare; sau normalize, Σ share LABOUR = 100%. Ghi nhận P1 bị pha loãng (rủi ro #3) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID623 | 3 product chia đều định phí 10.000.000 | PeriodMonth="2026-10" | Allocated mỗi product = 3.333.333,33; Σ = 9.999.999,99 (lệch 0,01 do làm tròn, rủi ro #2). Xác nhận spec có cần dồn phần dư hay không | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID624 | Số liệu cho Surplus = 0 đúng | PeriodMonth="2026-10" | IsAtRisk=false (điều kiện `Surplus < 0`) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID625 | Đã có kết quả CalculatedAt = 2026-11-24 10:00:00 | PeriodMonth="2026-10" (Now cùng giây) | Kết quả mới có CalculatedAt = 10:00:01 (NextCalculatedAt); không vi phạm UNIQUE | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID626 | Có biến phí 2.000.000 không gắn ProductId | PeriodMonth="2026-10" | Biến phí chung phải được phân bổ hoặc có cảnh báo. ⚠ Hiện bị bỏ qua → thặng dư ảo (rủi ro #1) | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID627 | — | PeriodMonth="2026-13" | Failure `BreakEvenPeriodMonthInvalid`; repo không gọi | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID628 | — | dto.PeriodMonth=null | Failure `BreakEvenPeriodMonthInvalid` | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID629 | Không có AccountingPeriod 2026-10 | PeriodMonth="2026-10" | Failure `BreakEvenNotReady` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID630 | Period có, payroll chưa Approved | PeriodMonth="2026-10" | Failure `BreakEvenNotReady` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID631 | Period có, payroll Approved, `HasMonthlyCostAsync`=false | PeriodMonth="2026-10" | Failure `BreakEvenNotReady` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID632 | Period sẵn sàng, 0 dòng doanh thu | PeriodMonth="2026-10" | Failure `BreakEvenNoOutput`; không Add/SaveChanges | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID633 | Period 2026-10 Status=Locked, đủ điều kiện | PeriodMonth="2026-10" | Success (Locked không chặn Calculate, theo DEV-ALL-172). Xác nhận spec | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID634 | Mock SaveChanges ném `DbUpdateException` (UNIQUE, race cùng giây) | PeriodMonth="2026-10" | Failure Conflict (409) hoặc exception propagate; không có kết quả nửa vời (rủi ro #7) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |

#### GetBreakEvenInputsAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID635 | Period 2026-10: payroll Approved, 3 khoản chi phí, doanh thu 2 product (qty 1.500 / 35.000.000đ), giờ công 1.000h, Status=Open | periodMonth="2026-10" | Success {ProductCount=2, TotalQty=1.500, TotalRevenue=35.000.000, PayrollApproved=true, CostCount=3, LabourHours=1.000, CanCalculate=true, PeriodLocked=false} | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID636 | Như trên, Status=Locked | periodMonth="2026-10" | PeriodLocked=true; CanCalculate=true (Locked không chặn, rủi ro #2) | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID637 | Không có period 2026-09; view có 1 product doanh thu | periodMonth="2026-09" | **Success** (không NotFound); PayrollApproved=false, CostCount=0, CanCalculate=false; ProductCount/Revenue vẫn lấy từ view (rủi ro #3) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID638 | Period có, payroll Approved, 0 khoản chi phí | periodMonth="2026-10" | CanCalculate=false | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID639 | Period có, đủ chi phí, ProductCount=0 | periodMonth="2026-10" | CanCalculate=false | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID640 | Đủ điều kiện, LabourHours=0 | periodMonth="2026-10" | CanCalculate=true (giờ công 0 không chặn, đúng design) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID641 | Payroll Draft | periodMonth="2026-10" | PayrollApproved=false; CanCalculate=false | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID642 | — | periodMonth="2026-13" | Failure `BreakEvenPeriodMonthInvalid` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID643 | — | periodMonth=null | Failure `BreakEvenPeriodMonthInvalid` | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID644 | costs.Count=2 nhưng `HasMonthlyCostAsync`=false (không có khoản chi phí tháng) | periodMonth="2026-10", sau đó gọi CalculateAsync cùng tháng | Hai hàm cho kết quả nhất quán. ⚠ Inputs báo CanCalculate=true trong khi Calculate trả `BreakEvenNotReady` (rủi ro #1) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |

#### GetBreakEvenAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID645 | Period 2026-10 có 2 lần tính T1 < T2, mỗi lần 4 dòng (2 product × 2 basis) | periodMonth="2026-10", productId=null, history=false | Success; 4 dòng của T2; SharePct trong mỗi nhóm (T2, Basis) cộng = 100% | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID646 | Như trên | history=true | 8 dòng (cả T1, T2) | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID647 | Như trên | productId=P1, history=false | 2 dòng (P1·OUTPUT, P1·LABOUR_HOURS) của T2 | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID648 | P1 chỉ có ở T1, không có ở T2 | productId=P1, history=false | Trả dòng P1 của T1 (Max tính trên tập đã lọc). Xác nhận spec: "mới nhất của P1" hay "lần tính gần nhất của kỳ" | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID649 | Mọi AllocatedFixed = 0 | periodMonth="2026-10" | SharePct=0; không lỗi chia 0 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID650 | 4 dòng chèn theo thứ tự ngẫu nhiên | periodMonth="2026-10" | Thứ tự ổn định (theo ProductCode, Basis). ⚠ Không có sort ổn định | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID651 | — | periodMonth="2026-13" | Failure `BreakEvenPeriodMonthInvalid` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID652 | Không có period 2026-08 | periodMonth="2026-08" | Failure `BreakEvenNotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID653 | Period 2026-11 có nhưng chưa tính | periodMonth="2026-11" | Failure `BreakEvenNotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID654 | Kết quả có `r.Product` = null (orphan) | periodMonth="2026-10" | Bỏ qua/Failure an toàn. ⚠ `NullReferenceException` trong ToDto | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |

#### ExportBreakEvenAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 | Defect IDs | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID655 | Như case Get Normal; mock `WriteBreakEvenSheet` | periodMonth="2026-10", history=false | Success; FileName="HoaVon_2026-10.xlsx"; writer nhận 4 dòng | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID656 | 2 lần tính | history=true | Writer nhận 8 dòng; tên file vẫn "HoaVon_2026-10.xlsx" | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID657 | Period 2026-11 chưa tính | periodMonth="2026-11" | Failure `BreakEvenNotFound` (propagate từ Get); writer không gọi | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID658 | — | periodMonth="abc" | Failure `BreakEvenPeriodMonthInvalid` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |
| UTCID659 | Mock writer ném exception | periodMonth="2026-10" | Exception propagate | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 | Khiêm |  |


---

## OrderPaymentService

### Tóm tắt sheet

- **Code Module**: OrderPaymentService
- **Number of TCs**: 90
- **Testing Round**: Passed | Failed | Pending
- **Round 1**: 86 | 4 | 0
- **Round 2**: 90 | 0 | 0
- **Round 3**: 0 | 0 | 0
- **Testing Type**: Normal | Abnormal | Boundary
- **Count**: 18 | 27 | 45

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **90**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### IsEditable

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID660 | Now = 2026-11-24 10:00 | CreatedAt = 2026-11-24 08:00 (2 giờ trước) | true | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID661 | Now = 2026-11-24 10:00 | CreatedAt = 2026-11-23 10:00:00 (đúng 24 giờ) | true (`<=`) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID662 | Now = 2026-11-24 10:00 | CreatedAt = 2026-11-23 09:59:59 (24 giờ 1 giây) | false | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID663 | Now = 2026-11-24 10:00 | CreatedAt = 2026-11-24 11:00 (tương lai, lệch đồng hồ) | true (now − CreatedAt âm ≤ 24h). Ghi nhận | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### InRange

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID664 | — | paid=50, final=100 | true | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID665 | — | paid=0, final=100 | true | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID666 | — | paid=100, final=100 | true | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID667 | — | paid=101, final=100 | false | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID668 | — | paid=-1, final=100 | false | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID669 | — | paid=-100, final=-100 | true (khoảng [−100, 0]) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID670 | — | paid=-101, final=-100 | false | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID671 | — | paid=0, final=0 | true | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID672 | — | paid=10, final=-100 | false | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID673 | — | paid=1, final=0 | false | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### Remaining

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID674 | — | final=100.000.000, paid=30.000.000 | 70.000.000 | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID675 | — | final=0, paid=0 | 0 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID676 | — | final=null, paid=0 | null (chưa chốt) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### StateOf

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID677 | — | final=100, paid=30 | Partial | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID678 | — | final=100, paid=0 | Unpaid | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID679 | — | final=100, paid=100 | Settled | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID680 | — | final=0, paid=0 | Settled (nhánh paid==final được xét trước paid==0) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID681 | — | final=null, paid=0 | NotSet | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### ListAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID682 | O1 Final=null; O2 Final=100tr Paid=100tr; O3 Final=100tr Paid=0; O4 Final=100tr Paid=40tr; O5 Final=0 Paid=0. Mỗi đơn có OrderLines | query {State=null, Page=1} | Success; 5 item; mỗi item có Amount = Σ OrderedQty × CmtUnitPrice, Remaining, State đúng theo StateOf | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID683 | Như trên | State="NotSet" | Chỉ O1 | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID684 | Như trên | State="Settled" | O2 và O5 (Final=0 được tính là Settled) | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID685 | Như trên | State="Unpaid" | Chỉ O3 (O5 bị loại vì Final=0) | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID686 | Như trên | State="Partial" | Chỉ O4 | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID687 | O2 mã "DH2611-002", khách "May Việt" | Keyword="May Việt" | Trả O2 | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID688 | O6: dòng 1 = 1.000 × 12.000đ, dòng 2 = 500 × 20.000đ | State=null | O6.Amount = 22.000.000 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID689 | O7 không có OrderLine | State=null | O7.Amount = 0 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID690 | DB rỗng | query mặc định | Success; Items rỗng | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID691 | 5 đơn | Page=3, PageSize=2 | Items=1; Total=5 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID692 | 5 đơn | Keyword="" | Không lọc keyword | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID693 | 5 đơn | State="settled" (sai casing) | Nhận không phân biệt hoa thường, hoặc Failure Validation. ⚠ Hiện trả list rỗng | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID694 | 5 đơn | State="Foo" | Success list rỗng (đúng design hiện tại). Đề xuất trả Validation (xác nhận spec) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID695 | Có mã đơn chứa ký tự "%" | Keyword="%" | Tìm theo ký tự literal, không trả toàn bộ | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### GetAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID696 | O4: 2 dòng đơn, Customer "May Việt", 2 payment (20tr lúc Now−2h, 20tr lúc Now−30h), audit table "OrderPayment" có 3 bản ghi | orderId=O4 | Success; Paid=40tr, Remaining=60tr; Payment 1 CanEdit=true, Payment 2 CanEdit=false; History=3; có RowVer; OrderAmount = Σ dòng đơn | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID697 | Payment CreatedAt = Now − 24h đúng | orderId=O4 | CanEdit=true | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID698 | Payment CreatedAt = Now − 24h − 1s | orderId=O4 | CanEdit=false | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID699 | O3 chưa có payment | orderId=O3 | Paid=0; Payments rỗng; State=Unpaid | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID700 | O1 Final=null | orderId=O1 | Remaining=null; State=NotSet | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID701 | — | orderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID702 | O8 có CustomerId lệch, Customer=null | orderId=O8 | CustomerName=null an toàn. ⚠ `NullReferenceException` | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID703 | O6 có dòng đơn | gọi ListAsync và GetAsync cho O6 | Amount ở List và OrderAmount ở Get bằng nhau (rủi ro: 2 biểu thức khác nhau) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### SetFinalAmountAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID704 | O1 Final=null, Paid=0, RowVer=v1; UoW spy | orderId=O1, dto {Amount=100.000.000, Reason="Chốt theo biên bản", RowVer=v1} | Success; FinalAmount=100tr, Reason lưu; RowVer touched; Audit + SaveChanges trong TX; Commit | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID705 | O4 Final=100tr, Paid=40tr | Amount=50.000.000, Reason="Điều chỉnh" | Success; State=Partial, Remaining=10tr | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID706 | O4 Paid=40tr | Amount=40.000.000 | Success; State=Settled | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID707 | O4 Paid=40tr | Amount=39.999.999 | Failure `FinalAmountBelowPaid`; không SaveChanges | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID708 | O1 Paid=0 | Amount=0 | Success; State=Settled. Sau đó mọi AddPayment khác 0 đều Failure (out of range) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID709 | O1 Paid=0 | Amount=-5.000.000 | Success (cho phép âm) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID710 | O4 Paid=40tr | Amount=-5.000.000 | Failure `FinalAmountBelowPaid`. Ghi nhận: message "dưới số đã trả" không đúng ngữ cảnh final âm | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID711 | O1 | Reason="" | Failure (Validation: Reason bắt buộc) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID712 | O1 | Reason="   " | Failure (Validation: Reason blank) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID713 | O1; MaxLength Reason=500 | Reason = 500 ký tự | Success | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID714 | O1 có Σ CMT = 100tr | Amount=200.000.000 | Success (không ràng buộc với Σ CMT). Xác nhận spec | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID715 | — | orderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID716 | O1 | Reason=null | Failure (Validation) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID717 | O1 RowVer hiện tại = v2 | RowVer=v1 | Failure Conflict (409, `DbUpdateConcurrencyException`); Rollback | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID718 | — | dto=null | `ArgumentNullException` hoặc Failure (Validation) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### AddPaymentAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID719 | O3 Final=100tr, Paid=0, RowVer=v1 | dto {PaymentDate=2026-11-24, Amount=30.000.000, Method="BANK_TRANSFER", RowVer=v1} | Success; payment mới CreatedAt=Now; Paid=30tr; Audit action Confirm | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID720 | O3 | Method="CASH", Amount=10.000.000 | Success | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID721 | O4 Final=100tr, Paid=40tr | Amount=60.000.000 (Paid+Amount = Final) | Success; State=Settled | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID722 | O4 Paid=40tr | Amount=60.000.001 | Failure (Validation: vượt Final) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID723 | O3 | Amount=0 | Failure (Validation: Amount ≠ 0) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID724 | O4 Paid=40tr | Amount=-10.000.000 (hoàn tiền, Paid còn 30tr) | Success | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID725 | O4 Paid=40tr | Amount=-40.000.001 (Paid âm) | Failure (Validation: out of range) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID726 | O9 Final=-5tr, Paid=0 | Amount=-5.000.000 | Success; State=Settled | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID727 | O9 Final=-5tr | Amount=1.000.000 | Failure (Validation: out of range) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID728 | O3 | PaymentDate=2026-11-24 (= Today) | Success | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID729 | O3 | PaymentDate=2026-11-25 (Today+1) | Failure (Validation: ngày tương lai) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID730 | O3 | Method="MOMO" | Failure (Validation: Method ∉ {BANK_TRANSFER, CASH}) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID731 | O3 | Method=null | Failure (Validation) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID732 | O1 Final=null | Amount=10.000.000 | Failure `NeedFinalAmount` (Kind=Conflict, khác Validation) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID733 | — | orderId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID734 | O3 đã Cancelled, Final=100tr | Amount=10.000.000 | Success (không khóa theo status đơn). Xác nhận spec | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID735 | O3 RowVer=v2 | RowVer=v1 | Failure Conflict (409) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### UpdatePaymentAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID736 | O4 Final=100tr, payment P1=20tr (CreatedAt=Now−2h), P2=20tr | orderId=O4, paymentId=P1, dto {Amount=25.000.000, Method="CASH", PaymentDate=2026-11-24, Reason="Sửa nhập sai", RowVer=v1} | Success; P1.Amount=25tr, EditReason lưu; RowVer của Order touched; Audit | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID737 | P1 CreatedAt = Now − 24h đúng | dto hợp lệ | Success | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID738 | P1 CreatedAt = Now − 24h − 1s | dto hợp lệ | Failure `EditExpired` | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID739 | Final=100tr, others (P2)=20tr | Amount=80.000.000 (others+new = Final) | Success | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID740 | Như trên | Amount=80.000.001 | Failure (Validation: out of range) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID741 | O4 | Amount=0 | Failure (Validation). Ghi nhận: không có cách xóa payment để về Paid=0 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID742 | O4 | Reason="" | Failure (Validation: Reason bắt buộc) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID743 | O4 | PaymentDate=2026-11-25 | Failure (Validation: ngày tương lai) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID744 | — | orderId=999, paymentId=P1 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID745 | O4 | paymentId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID746 | Payment P5 thuộc O3 | orderId=O4, paymentId=P5 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID747 | O10 Final=null nhưng có payment cũ (data lệch) | orderId=O10 | Failure `NotFound` (theo code; khác AddPayment trả Conflict). Ghi nhận không nhất quán | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID748 | O4 RowVer=v2 | RowVer=v1 | Failure Conflict (409) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID749 | O4 | Method="ATM" | Failure (Validation) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |


---

## SpecImportBuilder

### Tóm tắt sheet

- **Code Module**: SpecImportBuilder
- **Number of TCs**: 37
- **Testing Round**: Passed | Failed | Pending
- **Round 1**: 33 | 4 | 0
- **Round 2**: 37 | 0 | 0
- **Round 3**: 0 | 0 | 0
- **Testing Type**: Normal | Abnormal | Boundary
- **Count**: 4 | 14 | 19

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **37**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### Build

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID750 | spec rỗng; materials có M1, M2 | workbook: 2 component, 2 norm, 1 group PANEL; replace=false | Mọi dòng Action=New; ErrorCount=0; SpecChanged=true | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID751 | spec hiện có trùng khớp toàn bộ workbook | replace=false | Mọi dòng Action=Unchanged; SpecChanged=false | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID752 | spec có component "Thân trước" Pieces=2 | workbook "Thân trước" Pieces=1 | Dòng Action=Update | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID753 | spec có component "Túi" chưa dùng; workbook không có "Túi" | replace=true, usage rỗng | "Túi" nằm trong danh sách Removed | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID754 | — | component Name = 100 ký tự | Hợp lệ | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID755 | — | component Name = 101 ký tự | Action=Error (Name > 100) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID756 | — | Pieces=1 và Pieces=99 | Hợp lệ | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID757 | — | Pieces=0 | Action=Error (Pieces ∉ [1,99]) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID758 | — | Pieces=100 | Action=Error | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID759 | — | norm Note = 200 ký tự | Hợp lệ | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID760 | — | norm Note = 201 ký tự | Action=Error (Note > 200) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID761 | MarkerNo max = 99 | group MarkerNo=99 | Hợp lệ | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID762 | MarkerNo max = 99 | group MarkerNo=100 | Action=Error | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID763 | — | workbook không có dòng dữ liệu ở mọi sheet | Không có dòng; ErrorCount=0; SpecChanged=false | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID764 | Màu "Vàng" color-only: key có Size=`"?"` | workbook có norm "Vàng" | Sau MergeSpec, entry Size=`"?"` bị strip; spec kết quả không có Variant size `"?"` | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID765 | Đơn có size thật tên `"?"` | workbook norm size `"?"` | Size thật vẫn được giữ. ⚠ Bị strip như color-only | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID766 | spec có cặp Đỏ\ | Thân trước dùng M1 + M2 | workbook cặp Đỏ\ | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID767 | Component "Túi" đã dùng (usage chứa "Túi") | replace=true, workbook không có "Túi" | "Túi" **không** bị Removed | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID768 | Cặp norm Đỏ\ | Thân trước\ | M1 đã dùng | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID769 | Group G1 đã dùng | replace=true, workbook không có G1 | G1 được giữ lại | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID770 | Order marker OM1 đã dùng | replace=true, workbook không có OM1 | OM1 cần được giữ như component/norm. ⚠ Order markers không có cơ chế usage-keep → bị xóa | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID771 | spec không đổi | workbook chỉ đổi OrderMarkers | SpecChanged (hoặc cờ riêng) phản ánh có thay đổi. ⚠ SpecChanged không gồm OrderMarkers | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID772 | orderProduct=null (mã chưa có đơn) | workbook có OrderMarkers | Không kiểm size đơn; dòng OrderMarker bị bỏ qua hoặc báo không có đơn, không exception | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID773 | — | component Pieces="abc" | Action=Error (ParseError) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID774 | — | 2 dòng component "Tay" | Dòng trùng Action=Error; DuplicateCount=1 và ErrorCount phản ánh đúng | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID775 | — | norm màu "Tím" không có ở spec/ProductColor | Action=Error (màu lạ) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID776 | — | norm component "Cầu vai" không có | Action=Error (component lạ) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID777 | — | norm có mã vật tư không có trong materials | Action=Error (material null) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID778 | — | 2 norm trùng Color\ | Comp\ | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID779 | — | group không có component PANEL | Action=Error | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID780 | — | group có mọi màu unknown | Action=Error | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID781 | — | group có màu Đỏ (biết) và "Tím" (lạ) | Không Error; dòng có note "skip màu Tím". Ghi nhận rủi ro bỏ qua im lặng | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID782 | productCode="AO01" | OrderMarker ProductCode="AO02" | Dòng bị skip | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID783 | — | OrderMarker Width=null | Action=Error | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID784 | Đơn có size S, M | OrderMarker size XL | Action=Error (không có size trong đơn) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID785 | — | OrderMarker tham chiếu group "G9" không có | Action=Error (nhóm unknown) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID786 | — | workbook=null | `ArgumentNullException` | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |


---

## SpecImportService

### Tóm tắt sheet

- **Code Module**: SpecImportService
- **Number of TCs**: 33
- **Testing Round**: Passed | Failed | Pending
- **Round 1**: 27 | 6 | 0
- **Round 2**: 33 | 0 | 0
- **Round 3**: 0 | 0 | 0
- **Testing Type**: Normal | Abnormal | Boundary
- **Count**: 7 | 15 | 11

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **33**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### PreviewAsync(Stream)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID787 | Product 1 có đủ màu/vật tư; mock Excel parser trả workbook hợp lệ | productId=1, stream hợp lệ, replace=false | Success Preview; CanImport=true; Plan chạy với `createColors:false`; SaveChanges không gọi | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID791 | workbook có màu "Vàng" chưa có ở product | productId=1 | colorsToCreate=1; dòng "Vàng" đánh dấu sẽ tạo; CrossErrors bị bỏ qua → CanImport=true. Xác nhận spec (rủi ro preview lạc quan) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID792 | workbook có sheet nhưng 0 dòng | productId=1 | Success; Preview rỗng; không exception | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID793 | Sheet tên "components" (thường) thay vì "Components" | productId=1 | Nhận diện sheet không phân biệt hoa thường. ⚠ Parts case-sensitive → bỏ sheet | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID794 | colorsToCreate=0; norm và marker lệch nhau (CrossErrors) | productId=1 | Preview có CrossErrors; CanImport=false | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID795 | Parser ném exception | stream rác | Failure (Excel parse); exception được catch | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID796 | — | productId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID797 | — | stream=null | `ArgumentNullException` hoặc Failure | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### PreviewAsync(Workbook)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID788 | Như trên | productId=1, workbook object | Kết quả giống overload Stream | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID789 | Workbook có sheet nhưng 0 dòng | productId=1, workbook rỗng | Success; Preview rỗng | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID790 | — | productId=1, workbook=null | `ArgumentNullException` hoặc Failure | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### ImportAsync(Stream)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID798 | Product 1 đủ màu/vật tư; file có OrderMarkers; mock `IProductSpecService.WriteAsync`, `IOrderMarkerService.Check/Write` Success | productId=1, stream hợp lệ | Success; trong 1 TX: specs.Write gọi 1 lần, markers.Check rồi Write; Commit | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID799 | File có màu "Vàng" chưa có | productId=1 | EnsureColors tạo ProductColor "Vàng" (không tạo Variant size); norm "Vàng" ghi với size `"?"` đã strip; Commit | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID803 | File không có sheet OrderMarkers | productId=1 | Success; `IOrderMarkerService` không được gọi | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID804 | File có màu mới "Vàng" và 1 dòng component Error → CanImport=false | productId=1 | Failure; Rollback; ProductColor "Vàng" (tạo bởi EnsureColors) **không** còn trong DB | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID805 | Norm dùng mã vật tư chưa có | productId=1 | Failure (dòng Error material unknown; khác CheckAsync báo NewMaterial) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID806 | Mock markers.Check trả Failure | productId=1 | Failure; Rollback; spec không được ghi | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID807 | Mock specs.WriteAsync ném exception | productId=1 | Rollback; exception/Failure propagate | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID808 | Parser ném exception | stream rác | Failure (parse); BeginTransaction không gọi hoặc Rollback | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID809 | — | productId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### ImportAsync(Workbook)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID800 | Như case Normal đầu | productId=1, workbook object | Kết quả giống overload Stream | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID801 | Workbook 0 dòng dữ liệu | productId=1, workbook rỗng | Success không thay đổi gì, hoặc Failure (không có dữ liệu) theo spec; không ghi DB nửa vời | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID802 | — | productId=1, workbook=null | `ArgumentNullException` hoặc Failure; BeginTransaction không gọi | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### CheckAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID810 | Product 1 đủ màu/vật tư | productId=1, workbook hợp lệ | Success; không dòng Pending/NewMaterial; CanImport=true | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID811 | Mã hàng mới chưa tạo | productId=null, workbook hợp lệ | Success; màu được AssumeColors, dòng liên quan Action=Pending | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID812 | Product 1 thiếu màu "Vàng" | workbook norm "Vàng" | Dòng "Vàng" Action=Pending | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID813 | Vật tư "M99" chưa có | workbook norm M99 | AssumeMaterials gán Id âm; dòng Action=NewMaterial | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID814 | Marker tham chiếu màu "Vàng" giả định | workbook có OrderMarker "Vàng" | Dòng marker Action=Pending | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID815 | Có dòng Pending/NewMaterial | productId=1 | CanImport phản ánh đúng sau MarkAssumed. ⚠ MarkAssumed không cập nhật CanImport | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID816 | Mã hàng chưa có đơn | productId=null, workbook có OrderMarkers | HasOrderMarkers chỉ true khi có đơn tương ứng. ⚠ Luôn set true | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID817 | Còn màu/NVL giả định; norm và marker lệch nhau | productId=1 | CrossErrors **không** được tính (chỉ tính khi đủ màu + NVL thật) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID818 | — | productId=999 | Failure `NotFound`. ⚠ `productId ?? 0` coi như mã mới | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID819 | — | workbook=null | `ArgumentNullException` hoặc Failure | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |


---

## EmployeeService

### Tóm tắt sheet

- **Code Module**: EmployeeService
- **Number of TCs**: 40
- **Testing Round**: Passed | Failed | Pending
- **Round 1**: 30 | 10 | 0
- **Round 2**: 40 | 0 | 0
- **Round 3**: 0 | 0 | 0
- **Testing Type**: Normal | Abnormal | Boundary
- **Count**: 8 | 20 | 12

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **40**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### ListAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID820 | 10 NV: 6 Worker, 2 Accountant, 2 Manager; 1 Worker Resigned | query {Role="Worker", Status="Active"} | Success; 5 Worker Active | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID821 | Như trên | query {Role=null, Status=null} | Trả toàn bộ NV mà người gọi CanSee | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID822 | Không có NV role Electrician | Role="Electrician" | Success; list rỗng | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID823 | — | Role="Foo" | Failure (Validation) hoặc list rỗng; không exception | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### GetAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID824 | NV E1 có 3 bản ghi audit; người gọi Admin | employeeId=E1 | Success DTO + History=3 | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID825 | NV E2 mới tạo, chưa có audit | employeeId=E2 | Success; History rỗng | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID826 | — | employeeId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID827 | Người gọi Manager; E9 là Admin (ngoài phạm vi xem) | employeeId=E9 | Failure `NotFound` (!CanSee, không lộ sự tồn tại) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### CreateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID828 | Role Accountant là office creatable; UoW spy | dto {FullName="Nguyễn Văn A", Role="Accountant", TeamId=Office1, Email hợp lệ} | Success; tạo Employee + Account trong 1 TX; trả temp password **1 lần**; SettleOnTeamLeaveAsync không gọi | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID829 | MaxLength FullName=100 | FullName = 100 ký tự | Success | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID830 | — | FullName = 101 ký tự | Failure (Validation) | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID831 | — | FullName="" | Failure (Validation) | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID832 | Worker không phải office creatable | Role="Worker" | Failure (Validation: role không tạo được ở đây) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID833 | Team S3 là tổ may | Role="Accountant", TeamId=S3 | Failure (CheckPlacement) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID834 | Email a@x.vn đã tồn tại | Email="a@x.vn" | Failure (Duplicate) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID835 | Mock tạo Account ném exception | dto hợp lệ | Rollback; không có Employee mồ côi | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID836 | — | dto=null | `ArgumentNullException` hoặc Failure | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### ImportFromRjAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID837 | Dữ liệu RJ: 2 Worker hợp lệ, tổ may S3 | danh sách 2 NV RJ | Success; tạo 2 Worker gắn S3 kèm Account; không settle | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID838 | — | danh sách rỗng | Success 0 bản ghi (hoặc Validation); không mở TX | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID839 | Mã RJ "RJ001" đã import | danh sách chứa RJ001 | Failure (Duplicate) cho dòng đó | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID840 | Team chỉ định là Office | Worker → Office1 | Failure (CheckPlacement) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### AdminUpdateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID841 | Worker E1 Active ở tổ may A; tổ may B; mock `IDailyWorkAllocationService.SettleOnTeamLeaveAsync` Success (callback ghi lại E1.TeamId tại thời điểm gọi) | employeeId=E1, dto {TeamId=B, …} | Success; Settle(A, [E1]) gọi **1 lần, trước khi gán TeamId** (callback thấy TeamId=A); cùng TX; TeamId=B; Audit; Commit | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID842 | E1 ở tổ A | dto {TeamId=A, đổi Phone} | Success; Settle **không** gọi | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID843 | E2 TeamId=null | dto {TeamId=B} | Success; Settle không gọi (oldTeamId null) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID844 | E1 ở tổ A | dto {TeamId=null} | Success; Settle(A,[E1]) gọi; TeamId=null | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID845 | E3 Accountant ở tổ văn phòng Office1 → Office2 | dto {TeamId=Office2} | Settle(Office1,[E3]) được gọi (Employee không phân biệt loại tổ; Settle tự no-op); Success | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID846 | E1 ở tổ A; Settle trả Failure `TeamOrderBusy` | dto {TeamId=B} | Failure `TeamOrderBusy`; Rollback; TeamId vẫn = A; không Audit | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID847 | E5 Resigned | dto bất kỳ | Failure (chỉ sửa NV Active) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID848 | — | employeeId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID849 | — | Worker → TeamId=Office1 | Failure (CheckPlacement) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID850 | E4 là Tổ trưởng tổ A | dto {TeamId=B} | Failure (không chuyển tổ trưởng, nhất quán với TransferIn). ⚠ Hiện cho phép (rủi ro #4) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### ManagerUpdateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID851 | Manager phụ trách xưởng có tổ A, B; E1 ở A | dto {TeamId=B} | Success; Settle(A,[E1]) trước gán TeamId; cùng TX | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID852 | E1 ở A | dto không đổi TeamId | Success; Settle không gọi | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID853 | E8 thuộc xưởng ngoài phạm vi Manager | employeeId=E8 | Failure (scope: Forbidden/NotFound) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID854 | Settle trả `TeamOrderBusy` | dto {TeamId=B} | Failure `TeamOrderBusy`; Rollback; TeamId không đổi | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID855 | E5 Resigned | dto bất kỳ | Failure (chỉ sửa NV Active) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### ResignAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID856 | E6 Accountant Active, không có tổ | employeeId=E6 | Success; Status=Resigned; RotateStamp (phiên đăng nhập cũ vô hiệu) | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID857 | E1 Worker Active ở tổ may A, còn handover PENDING | employeeId=E1 | Settle(A,[E1]) được gọi và TeamId=null. ⚠ Hiện không settle, vẫn giữ TeamId (rủi ro #3, gap nghiêm trọng) | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID858 | E5 đã Resigned | employeeId=E5 | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID859 | — | employeeId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |


---

## TeamService

### Tóm tắt sheet

- **Code Module**: TeamService
- **Number of TCs**: 60
- **Testing Round**: Passed | Failed | Pending
- **Round 1**: 47 | 13 | 0
- **Round 2**: 60 | 0 | 0
- **Round 3**: 0 | 0 | 0
- **Testing Type**: Normal | Abnormal | Boundary
- **Count**: 14 | 29 | 17

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **60**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### ListAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID860 | 4 tổ: 2 Sewing, 1 Cutting, 1 Office (1 tổ Discontinued) | query {Type="Sewing"} | Success; 2 tổ may | Normal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID861 | DB rỗng | query mặc định | Success; list rỗng | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID862 | — | query {Type="Foo"} | Failure (Validation) hoặc list rỗng; không exception | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### GetAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID863 | Tổ A có 5 thành viên, 1 tổ trưởng | teamId=A | Success DTO + danh sách thành viên | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID864 | Tổ C chưa có thành viên | teamId=C | Success; Members rỗng; Leader=null | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID865 | — | teamId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### CreateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID866 | — | dto {Name="Tổ may 5", Type="Sewing"} | Success; tổ Active; Settle không gọi | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID867 | MaxLength Name=100 | Name = 100 ký tự | Success; Name 101 ký tự → Failure (Validation) | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID868 | Đã có "Tổ may 5" | Name="Tổ may 5" | Failure (Duplicate) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID869 | — | Type="Foo" | Failure (Validation) | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### UpdateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID870 | Tổ A Sewing | dto {Name="Tổ may A1"} | Success; Settle không gọi | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID871 | Tổ C có 0 thành viên Active | dto {Type="Cutting"} | Success (đổi loại khi 0 thành viên) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID872 | Tổ A có 5 thành viên Active | dto {Type="Office"} | Failure (còn thành viên Active) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID873 | — | teamId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### DiscontinueAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID874 | Tổ C 0 thành viên Active | teamId=C | Success; Status=Discontinued | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID875 | Tổ D có 1 thành viên Resigned, 0 Active | teamId=D | Success (chỉ tính Active) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID876 | Tổ A còn 5 thành viên Active | teamId=A | Failure (còn thành viên) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID877 | Tổ C đã Discontinued | teamId=C | Failure `InvalidStatus` (hoặc idempotent theo spec) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID878 | — | teamId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### ReactivateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID879 | Tổ C Discontinued | teamId=C | Success; Status=Active | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID880 | Tổ C Discontinued, 0 thành viên | teamId=C | Success; Members vẫn rỗng; Settle không gọi | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID881 | Tổ A Active | teamId=A | Failure `InvalidStatus` (hoặc idempotent theo spec) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID882 | — | teamId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### TransferInAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID883 | Tổ đích B (Sewing). E1, E2 thuộc tổ may A; E3 thuộc tổ may C; mock Settle Success | targetTeamId=B, dto {EmployeeIds=[E1,E2,E3], EffectiveDate=2026-11-24} | Success; trong 1 TX: Settle(A,[E1,E2]) 1 lần, Settle(C,[E3]) 1 lần; TeamId=B cho cả 3; stamp + Audit + LogTransfer; Message có suffix "đã quyết toán việc tổ may" | Normal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID884 | E4 thuộc tổ văn phòng Office1 | dto {EmployeeIds=[E4], EffectiveDate=Today} | Success; Settle(Office1,[E4]) gọi (no-op); Message **không** có suffix | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID885 | E1 ở tổ A | EffectiveDate=2026-11-24 (= Today) | Success | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID886 | E1 ở tổ A | EffectiveDate=2026-11-25 (Today+1) | Failure (Validation: ngày tương lai); không Settle | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID887 | E1 ở tổ A | EffectiveDate=2026-11-01 (quá khứ) | Success; ngày chỉ ghi vào audit. Xác nhận spec | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID888 | E5 TeamId=null | dto {EmployeeIds=[E5]} | Success; không Settle; TeamId=B | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID889 | — | EmployeeIds=[] | Failure (Validation) | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID890 | Tổ đích Office1 | Worker E1 → Office1 | Success (rủi ro: không chặn loại tổ đích). Xác nhận spec | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID891 | E6 là Tổ trưởng tổ A | EmployeeIds=[E1,E6] | Failure (không chuyển Leader/Deputy); không ai bị đổi tổ | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID892 | E7 là Tổ phó tổ C | EmployeeIds=[E7] | Failure (Deputy) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID893 | — | EmployeeIds=[E1,E1] | Failure (Validation: trùng) hoặc tự loại trùng. ⚠ Hiện trả NotFound | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID894 | — | EmployeeIds=[999] | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID895 | — | targetTeamId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID896 | Tổ đích C Discontinued | targetTeamId=C | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID897 | Settle(A) Success, Settle(C) trả `TeamOrderBusy` | EmployeeIds=[E1,E3] | Failure `TeamOrderBusy`; Rollback toàn bộ; E1, E3 giữ TeamId cũ | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### RemoveMembersAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID898 | Tổ may A có E1, E2 (Worker); mock Settle Success | teamId=A, EmployeeIds=[E1,E2] | Success; trong TX: Settle(A,[E1,E2]) rồi TeamId=null; Message có suffix tổ may | Normal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID899 | Tổ văn phòng Office1 có E4 | teamId=Office1, [E4] | Success; TeamId=null; Message không suffix | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID900 | — | EmployeeIds=[] | Failure (Validation) | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID901 | E6 là Tổ trưởng tổ A | [E1,E6] | Failure (không xóa Leader); không ai bị xóa | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID902 | E3 thuộc tổ C | teamId=A, [E3] | Failure (NotFound/không thuộc tổ) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID903 | Settle trả `TeamOrderBusy` | teamId=A, [E1] | Failure `TeamOrderBusy`; Rollback; TeamId=A giữ nguyên | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID904 | — | teamId=999 | Failure `NotFound` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### AddMembersAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID905 | E5, E8 TeamId=null, Active | teamId=A, [E5,E8] | Success; TeamId=A; Settle không gọi | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID906 | — | EmployeeIds=[] | Failure (Validation) | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID907 | E1 đang ở tổ A | teamId=B, [E1] | Failure (chỉ nhận NV chưa có tổ; phải dùng TransferIn) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID908 | Tổ C Discontinued | teamId=C, [E5] | Failure `InvalidStatus` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### AppointAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID909 | Tổ A: E6 đang là Leader; E1 là Worker trong tổ | teamId=A, employeeId=E1, Role=Leader | Success; trong TX: E6 hạ về Worker, E1 lên Leader | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID910 | Tổ B chưa có Leader | employeeId=E2 (thành viên B), Role=Leader | Success; chỉ nâng E2 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID911 | E3 không thuộc tổ A | teamId=A, employeeId=E3 | Failure (không phải thành viên) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID912 | — | teamId=999 | Failure `NotFound` | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### DismissAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID913 | E6 là Leader tổ A | teamId=A, employeeId=E6 | Success; E6 về Worker, **vẫn ở tổ A**; Settle không gọi | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID914 | E7 là Tổ phó tổ C | teamId=C, employeeId=E7 | Success; E7 về Worker, vẫn ở tổ C | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID915 | E1 là Worker | employeeId=E1 | Failure (không giữ chức vụ) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID916 | — | employeeId=999 | Failure `NotFound` | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### CandidatesAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID917 | E5, E8 Active, TeamId=null; E9 Resigned, TeamId=null | teamId=A | Success; [E5, E8] (không có E9) | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID918 | Không có NV tự do | teamId=A | Success; list rỗng | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID919 | — | teamId=999 | Failure `NotFound` | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |


---

## AttendanceService

### Tóm tắt sheet

- **Code Module**: AttendanceService
- **Number of TCs**: 12
- **Testing Round**: Passed | Failed | Pending
- **Round 1**: 11 | 1 | 0
- **Round 2**: 12 | 0 | 0
- **Round 3**: 0 | 0 | 0
- **Testing Type**: Normal | Abnormal | Boundary
- **Count**: 2 | 3 | 7

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **12**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### AlertSyncOverdueAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID920 | Threshold=30 phút; LastSucceededAt=Now−45 phút; chưa có notification AttendanceSyncOverdue | ct | NotifyRole(Admin) 1 lần; SaveChanges 1 lần; LogWarning "Attendance sync overdue" | Normal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID921 | LastSucceededAt=Now−10 phút | ct | Return sớm; Notifier và SaveChanges không gọi | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID922 | LastSucceededAt=Now−30 phút đúng | ct | Không overdue (`>`); không notify | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID923 | LastSucceededAt=Now−31 phút | ct | Overdue; notify Admin | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID924 | Setting threshold không cấu hình; LastSucceededAt=Now−31 phút | ct | Dùng mặc định 30 → notify | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID925 | Chưa từng sync (lastAt=null); chưa có notification | gọi lần 1 | Overdue; notify Admin 1 lần | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID926 | lastAt=null; đã có notification từ lần 1 (since=MinValue) | gọi lần 2 | Không notify lại (rủi ro #1: im đến khi có sync thành công) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID927 | LastSucceededAt=Now−45 phút; đã có notification CreatedAt=Now−5 phút (> since) | ct | Không notify (đã báo đợt này) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID928 | Notification cũ CreatedAt=Now−3 giờ, trước LastSucceededAt=Now−45 phút | ct | Notify mới (đợt overdue mới) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID929 | Overdue; mock Notifier ném exception | ct | Exception propagate; SaveChanges không gọi (hàm trả Task, không Result) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID930 | Overdue; Notifier OK; SaveChanges ném exception | ct | Exception propagate. Ghi nhận Notify và SaveChanges tách nhau (rủi ro #5) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID931 | Overdue | ct | Chỉ role Admin nhận; Manager **không** nhận (rủi ro #4). Xác nhận spec | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |


---

## MachineRepairRequestHandler

### Tóm tắt sheet

- **Code Module**: MachineRepairRequestHandler
- **Number of TCs**: 14
- **Testing Round**: Passed | Failed | Pending
- **Round 1**: 13 | 1 | 0
- **Round 2**: 14 | 0 | 0
- **Round 3**: 0 | 0 | 0
- **Testing Type**: Normal | Abnormal | Boundary
- **Count**: 4 | 4 | 6

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **14**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### ValidateAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID932 | Today=2026-11-24 | dto {RequestId=null, Lines=[], TargetDate=2026-11-26} | Success | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID933 | — | create, Lines=[], TargetDate=2026-11-24 (= Today) | Success | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID934 | — | create, Lines=[], TargetDate=2026-11-23 (Today−1) | Failure `RequisitionTargetDatePast` | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID935 | — | update {RequestId=5, Lines=[], TargetDate=2026-11-23} | Success (update bỏ qua check ngày quá khứ). Xác nhận spec | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID936 | — | create, Lines=[], TargetDate=null | Success (không check) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID937 | — | create, Lines=[1 dòng], TargetDate=Today+1 | Failure message `MachineRepairNoLines`, ErrorCode `RequestLinesInvalid` | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID938 | — | dto=null | `ArgumentNullException` hoặc Failure | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### OnStatusChangedAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID939 | Request MACHINE_REPAIR | status mới = Completed | Success; không side-effect | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID940 | — | status mới = Rejected / Cancelled | Success | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID941 | — | status mới = null hoặc giá trị lạ "FOO" | Success (handler không xử lý theo status); không exception | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### MachineRepairRequestHandler (Policy)

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID942 | Người tạo: Tổ trưởng tổ cắt | tạo đơn sửa máy | Được phép; người xử lý = Electrician; handler có RequestType="MACHINE_REPAIR", AllowManualComplete=true | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID943 | Người tạo: Tổ trưởng tổ may | tạo đơn sửa máy | Được phép; người xử lý = Electrician | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID944 | Người tạo: NV đang giữ quyền tổ trưởng tạm (acting leader) tổ may | tạo đơn sửa máy | Được phép như tổ trưởng (xác nhận spec) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID945 | Người tạo: Worker | tạo đơn sửa máy | Không được phép theo policy (Forbidden) | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |


---

## HomeDashboardService

### Tóm tắt sheet

- **Code Module**: HomeDashboardService
- **Number of TCs**: 29
- **Testing Round**: Passed | Failed | Pending
- **Round 1**: 21 | 8 | 0
- **Round 2**: 29 | 0 | 0
- **Round 3**: 0 | 0 | 0
- **Testing Type**: Normal | Abnormal | Boundary
- **Count**: 9 | 8 | 12

### Test cases

_Số dòng TC (không tính tiêu đề nhóm): **29**_

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

#### GetAdminAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID946 | 3 PieceRate PENDING, 1 Payroll PendingApproval; doanh thu/chi phí tháng 11 và tháng 10; 4 đơn đang chạy | ct | Success DTO: PieceRatePending=3, PayrollPending=1, Revenue/Cost this & last, RunningOrders=4 | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID947 | Chưa nhập chi phí tháng 11 | ct | CostThisMonth=null (không phải 0) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID948 | Đơn status lưu "IN_PRODUCTION"; RunningStatuses dạng Pascal | ct | Đơn vẫn được đếm. ⚠ Lệch casing → đếm 0 | Boundary | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID949 | Mock service doanh thu ném exception | ct | Không exception; Revenue=0 (fallback); nên có log lỗi | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### GetManagerAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID950 | 2 phiếu nhận chờ; inbox 3 việc; 1 quyền tổ trưởng tạm; 5 đơn mở | ct | Success DTO đủ 4 khối số liệu | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID951 | Inbox có 7 việc | ct | Inbox trả 5 (giới hạn) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID952 | Mock inbox service ném exception | ct | Inbox = [] ; phần khác vẫn có dữ liệu | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### GetAccountantAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID953 | 4 NV thiếu chấm công; 2 công đoạn thiếu đơn giá; 3 PO chưa trả; 2 đơn chưa chốt; chi phí/lương tháng | ct | Success DTO đúng số liệu | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID954 | Không có PO chưa trả | ct | UnpaidPO=0 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID955 | Mock timesheet service ném exception | ct | MissingPunch=0 (fallback); không exception | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### GetWarehouseAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID956 | 3 lô đến; 2 lệnh cắt chưa cấp NVL; 4 đơn xin; phiếu cắt/TP/trả chờ | ct | Success DTO đủ khối | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID957 | Không có lô đến, lệnh, đơn xin, phiếu chờ | ct | Mọi số đếm = 0; list rỗng | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID958 | Mock incoming ném exception | ct | Incoming = (0, []) ; không exception | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### GetTeamLeaderAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID959 | Tổ trưởng tổ cắt | ct | CuttingPanel có dữ liệu; SewingPanel=null | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID960 | Tổ trưởng tổ may | ct | SewingPanel có dữ liệu; CuttingPanel=null | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID961 | Người dùng TeamId=null | ct | Cả 2 panel = null; không exception | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID962 | Tổ loại Office | ct | Cả 2 panel = null | Abnormal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### GetQaqcAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID963 | Đã cấp 500, hoàn thành 300, hủy 20; 100 TP chưa nhập kho | ct | OnLineQty=180; FinishedNotReceived=100 | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID964 | issued − finished − scrap = −10 (data lệch) | ct | OnLineQty=0 (Max(0, …)) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID965 | Mock service số cấp phát ném exception | ct | OnLineQty=0 (fallback); không exception | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### GetElectricianAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID966 | 3 ticket open do user hiện tại tạo; 2 ticket do người khác tạo | ct | Trả 3 ticket của user hiện tại | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID967 | 25 ticket open của user | ct | Trả 20 (giới hạn) | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID968 | Không có ticket | ct | List rỗng | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID969 | Ticket của user đã Closed; mock repo ticket ném exception ở lần gọi khác | ct | Ticket Closed không được trả; khi repo lỗi → list rỗng (fallback), không exception | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |

#### GetWorkerAsync

| Test Case ID | Pre-conditions | Input parameters | Expected Results | Type | Round 1 | Test date | Tester | Round 2 | Round 3 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UTCID970 | Worker khoán có sản lượng hôm nay, payslip tháng 10, 2 allocation PENDING, 1 defect 3 ngày trước, suất ăn hôm nay | ct | Success DTO: EstPay > 0, Payslip, PendingAlloc=2, Defects7d=1, Meal | Normal | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID971 | Worker không hưởng lương khoán | ct | EstPay=null | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID972 | Không có sản lượng hôm nay | ct | Today=0 | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID973 | Defect cách đúng 7 ngày và 8 ngày | ct | Đếm defect 7 ngày, loại defect 8 ngày | Boundary | Passed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |
| UTCID974 | Mock payslip service ném exception | ct | Payslip=null (fallback); không exception | Abnormal | Failed | 2026-11-24 00:00:00 | Khiêm | Passed | 2026-11-24 00:00:00 |


---
