# Phân tích QA — 10 Service độ phức tạp CAO

> Vai trò: Senior QA Engineer  
> Phạm vi: shortlist Service tầng Application (bỏ mức THẤP)  
> Mục tiêu: map Input/Output, nhánh điều kiện, dependency, và điểm nghi ngờ bug/thiếu validation  
> Ngày: 2026-11-22

**Lưu ý:** Hầu hết API trả `Result` / `Result<T>` (không throw nghiệp vụ). Exception thật sự chủ yếu từ EF concurrency, Excel parse (đã catch một phần), hoặc I/O file.

---

## Mục lục

1. [OperationService](#1-operationservice)
2. [MaterialReceiptService](#2-materialreceiptservice)
3. [PayrollService](#3-payrollservice)
4. [OutputService](#4-outputservice)
5. [MaterialIssueService](#5-materialissueservice)
6. [ProductSpecService](#6-productspecservice)
7. [WorkOrderService](#7-workorderservice)
8. [DefectService](#8-defectservice)
9. [CustomerOrderService](#9-customerorderservice)
10. [DailyWorkAllocationService](#10-dailyworkallocationservice)

---

## 1. OperationService

**File:** `@be/Application/Services/Production/OperationService.cs` (~799 LOC)  
**Dependencies chung:** `IUnitOfWork`, `IAuditLogService`, `IClock`, `IExcelOperationParser`, `INotificationService`  
**Entity/Repo chính:** `Operation`, `PieceRate`, `OperationGroup`, `OperationComponent`, `Product`, `WorkAllocation`, `OperationAssignment`, `OutputRecord`, `DefectRecord`, `VwWorkerOutputCap`

### Public methods

#### `ListAsync(int productId, bool includeDiscontinued, CancellationToken ct)`
| | |
|---|---|
| **Input** | `productId`, `includeDiscontinued` |
| **Output** | `Result<IReadOnlyList<OperationDto>>` — Failure nếu `productId <= 0` |
| **Nhánh** | `productId <= 0`; lọc `Active` nếu không include; `UpcomingRate` ternary null; `CurrentAndNextRate` ưu tiên PENDING |
| **Deps** | Repo `Operation`, `PieceRate`; `_clock.Today` |

#### `CreateAsync(SaveOperationDto dto, CancellationToken ct)`
| | |
|---|---|
| **Input** | DTO công đoạn (mã, tên, STT, skill, thời gian, nhóm, chi tiết, IsFinalStep…) |
| **Output** | `Result<int>` (OperationId) |
| **Nhánh** | Check product; CheckAsync (trùng mã/STT, final step); ApplyGroupAndComponents; `if IsFinalStep` → MoveFinalStep; transaction 2 lần SaveChanges |
| **Deps** | UoW transaction, Audit, Product/Operation/Group/Component repos |

#### `UpdateAsync(int operationId, SaveOperationDto dto, CancellationToken ct)`
| | |
|---|---|
| **Input** | Id + DTO (ProductId bị ép theo entity cũ) |
| **Output** | `Result` — NotFound / validation / Success |
| **Nhánh** | null entity; CheckAsync; IsFinalStep && Active → MoveFinalStep; related fail |
| **Deps** | Giống Create |

#### `DiscontinueAsync(int operationId, CancellationToken ct)`
| | |
|---|---|
| **Input** | `operationId` |
| **Output** | `Result` — NotFound / HasOpenWork / Success (idempotent nếu đã Discontinued) |
| **Nhánh** | Status đã ngừng; HasOpenWork (PENDING allocation hoặc RemainingQty > 0 trên đơn chưa đóng) |
| **Deps** | WorkAllocation, VwWorkerOutputCap, OrderLine |

#### `ReactivateAsync(int operationId, CancellationToken ct)`
| | |
|---|---|
| **Input** | `operationId` |
| **Output** | `Result` |
| **Nhánh** | NotFound; đã Active; final step conflict khi reactive |
| **Deps** | Operation, FinalStepExists |

#### `PreviewImportAsync` / `ImportAsync`
| | |
|---|---|
| **Input** | `productId`, Excel `Stream`, `sheetName?`, `effectiveDate?`, `submitRates`, `replace` |
| **Output** | Preview DTO / ImportResultDto; Failure Excel invalid / sheet not found / final step |
| **Nhánh** | try/catch parse; chọn sheet; PlanRows (trùng STT, nhiều final, component skip); replace → xóa unused / block used; ApplyImportedRates |
| **Deps** | Excel parser, PieceRate, OperationComponent, Notifier (Admin khi submit rates) |

### Điểm nghi ngờ (≥5)

1. **`DiscontinueAsync` không chặn IsFinalStep** — ngừng công đoạn cuối có thể để mã hàng không còn final step đang Active.
2. **Import replace: file 0 dòng hợp lệ thì không xóa gì** (`rows.Any(Action != Error)`), nhưng file chỉ có dòng lỗi vẫn “thành công” với 0 thay đổi — dễ hiểu nhầm là đã replace.
3. **`ResolveComponents` trả `ids=null` khi mọi tên skip** → giữ nguyên chi tiết cũ (cảnh báo thôi) — silent keep, dễ lệch ý người import.
4. **`ListAsync` UpcomingRate vs NextRate** có thể trùng/lệch với PENDING (Next ưu tiên PENDING, Upcoming chỉ APPROVED tương lai) — FE có thể hiển thị 2 “giá sắp tới” khác nhau.
5. **`CreateAsync`/`UpdateAsync` Trim code nhưng không check `StandardTimeSec <= 0`** ở service (phụ thuộc validator) — nếu bypass validator thì thời gian 0/âm lọt xuống.
6. **`dto.OperationCode.Trim()` (~L99/L124)** — `OperationCode` null → `NullReferenceException` (không bọc Result).
7. **`sheets[0]` (~L505)** khi parse trả list rỗng → `ArgumentOutOfRangeException`.
8. **`ListAsync` không kiểm product tồn tại** — khác Create (NotFound); trả list rỗng im lặng.
9. **`(short)row.Stt` (~L701)** — STT > 32767 tràn `StationSequence`.

---

## 2. MaterialReceiptService

**File:** `@be/Application/Services/Warehouse/MaterialReceiptService.cs` (~657 LOC)  
**Dependencies:** `IUnitOfWork`, `IStockService`, `IDocumentCodeGenerator`, `IRequestService`, `MaterialSupplyUpdater`, `IClock`, `INotificationService`, `DeliveryCountGuard`, `ShortageTracker`, `ILogger`

### Public methods

#### `GetPagedAsync(MaterialReceiptQuery query, CancellationToken ct)`
| | |
|---|---|
| **Input** | Status, OrderId, POId, Source, Keyword, paging |
| **Output** | `Result<PagedResult<…>>` (luôn Success) |
| **Nhánh** | Nhiều `Where` optional; keyword Contains trên code/đơn/PO |
| **Deps** | MaterialReceipt query |

#### `GetByIdAsync(int receiptId, CancellationToken ct)`
| | |
|---|---|
| **Input** | Id |
| **Output** | NotFound / Success (+ ShortDeliveryRequestId, ToReplenishQty, DeliveryNo) |
| **Nhánh** | null → NotFound; WithDeliveryProgress / WithReplenishQty; Status Confirmed dùng ConfirmedAt so sánh `<` và tie-break Id |
| **Deps** | Request, MaterialReceiptLine, POLine, OrderMaterial |

#### `CreateAsync` / `UpdateAsync`
| | |
|---|---|
| **Input** | `SaveMaterialReceiptDto` (Source CUSTOMER/SUPPLIER, lines AcceptedQty…) |
| **Output** | DTO sau GetById; InvalidStatus nếu Update phiếu bổ sung hoặc không Pending/Returned |
| **Nhánh** | ResolveCustomer/Supplier; duplicate pending; delivery count; closed shortage; Update xóa hết lines rồi Apply lại |
| **Deps** | codes, notifier, DeliveryCountGuard, supply |

#### `CreateSupplementaryAsync(SaveSupplementaryReceiptDto dto, CancellationToken ct)`
| | |
|---|---|
| **Input** | ShortageRequestId, ngày, lines RequestLineId + AcceptedQty |
| **Output** | NotFound / NotResolvable / ExceedsReplenish / DeliveryCountReached |
| **Nhánh** | CheckReplenishable; CheckReceiptDate; remaining = ReplenishQty − done(includePending); `AcceptedQty > remaining` |
| **Deps** | Request+lines, ShortageTracker, DeliveryCountGuard |

#### `ConfirmAsync(int receiptId, int confirmedByEmployeeId, CancellationToken ct)`
| | |
|---|---|
| **Input** | Id + employee xác nhận |
| **Output** | NotFound / InvalidStatus / ExceedsPurchase/Customer / DeliveryCount / PhotoRequired / Stock fail / Success (+ msg SHORT_DELIVERY) |
| **Nhánh** | Status != Pending; CheckConfirmable; chỉ post stock `AcceptedQty > 0`; Recalculate supply; HandleShortage |
| **Deps** | StockService, MaterialSupplyUpdater, ShortageTracker, RequestService |

#### `ReturnAsync(int receiptId, ReasonDto dto, CancellationToken ct)`
| | |
|---|---|
| **Input** | Id + Reason |
| **Output** | NotFound / InvalidStatus / Success |
| **Nhánh** | Chỉ Pending → Returned; notify Thủ kho |
| **Deps** | Notifier |

### Điểm nghi ngờ (≥5)

1. **`ConfirmAsync` không validate `confirmedByEmployeeId > 0` / tồn tại** — FK có thể fail muộn hoặc ghi Id rác.
2. **Race 2 phiếu Pending cùng NVL:** Create chặn duplicate Pending, nhưng Confirm không khóa app-lock — hai Confirm gần nhau vẫn có thể vượt `outstanding` nếu timing lệch trước khi status đổi (DB trigger có thể cứu, service thì `FindOverLimitAsync` đọc “others đã Confirmed”).
3. **`AcceptedQty = 0` vẫn Confirm được** (bỏ qua stock) — phiếu “rỗng” vẫn đếm lần giao / có thể kích SHORT_DELIVERY.
4. **`GetById` projection dùng `l.MaterialId!.Value`** khi OrderMaterial null — nếu data lệch schema → NullReference lúc materialize.
5. **Update Pending: xóa toàn bộ lines rồi thêm lại** — mất MaterialReceiptLineId; attachment/ảnh gắn line (nếu có) hoặc OriginalReceiptLine tham chiếu có thể orphan.
6. **Biên `>` vs `>=` ở over-limit:** `AcceptedQty > outstanding` cho phép nhận đúng bằng outstanding; OK. Nhưng `outstanding` có thể âm nếu `NotReplenishedQty` lớn → vẫn so sánh, message “còn -x”.
7. **`CreateAsync` / `CreateSupplementaryAsync` (~L213/L259)** — không kiểm `created.IsSuccess` sau transaction trước `GetByIdAsync(created.Data)`.
8. **`ReturnAsync` (~L412)** — `dto.Reason.Trim()` khi Reason null → NRE (validator có thể chặn, service không defense).
9. **`UpdateAsync` Resolve vượt trần** có thể không truyền `exceptReceiptId` đúng path → đếm trùng chính phiếu đang sửa.

---

## 3. PayrollService

**File:** `@be/Application/Services/Finance/PayrollService.cs` (~626 LOC)  
**Dependencies:** `IPayrollRepository`, `IExcelPayrollParser`, `IPayrollExcelWriter`, `IPayrollFileStore`, `IAuditLogService`, `IUnitOfWork`, `IClock`, `ILogger`

### Public methods (nhóm)

| Method | Input | Output chính | Nhánh trọng yếu |
|---|---|---|---|
| `ExportAsync(periodMonth)` | `yyyy-MM` | ExcelFileDto / PeriodMonthInvalid | Regex tháng; GetOrCreatePeriod |
| `PreviewImportAsync(stream)` | Excel stream | Preview rows Valid/Error/Duplicate | try/catch parse; empty; ValidateRows |
| `ImportAsync(stream, periodMonth)` | stream + tháng | ImportResult / locked / invalid status / empty valid | Period locked; chỉ Draft/Rejected; ghi file; xóa file cũ; catch → delete orphan |
| `GetByPeriodMonthAsync` | tháng | Summary / NotFound | |
| `SubmitAsync(payrollId)` | Id | InvalidStatus / Empty / Success | Chỉ Draft; Payslips.Count==0 |
| `GetImportedFileAsync` | Id | File / FileNotFound | |
| `GetPayrollsAsync(query)` | Status Pascal, page | Paged | ToRawStatus: giá trị lạ giữ nguyên → list rỗng |
| `GetDetailAsync` | Id | Detail / NotFound | |
| `ApproveAsync` | Id | Approve + **Lock period** | Chỉ PendingApproval |
| `RejectAsync` | Id + Reason | Reject | |
| `ReopenAsync` | Id + Reason | Draft + **Unlock period** | Chỉ Approved |
| `GetMyPayslipsAsync` / `GetMyPayslipAsync` | employeeId, tháng | List/Detail / NotFound | |

### Điểm nghi ngờ (≥5)

1. **`NetPay` lấy từ file, không = Σ items** — chỉ cảnh báo nếu lệch > 1000đ (`NetPayTolerance`). Import vẫn lưu NetPay file → lương “sai công thức” vẫn duyệt được.
2. **`WorkDays is < 0 or > 31`** — cho phép `WorkDays == null` pass CheckRow (chỉ fail khi NetPay null / items rỗng). Công null vẫn vào group.
3. **Import bỏ qua dòng Error nhưng vẫn Success** nếu còn ≥1 group hợp lệ — Kế toán có thể không để ý `error` count.
4. **`ApproveAsync` khóa cả `AccountingPeriod`** — mọi chấm công/chi phí tháng đó bị khóa theo; Reopen một payroll mở khóa cả tháng (side-effect lớn, đã ghi DEV-MI-09-2).
5. **`ToRawStatus` giá trị lạ không 400** — lọc ra rỗng, che lỗi FE gửi sai status.
6. **Không check `employeeId` ownership ở controller layer trong service** `GetMyPayslip*` — nếu controller truyền nhầm Id người khác vẫn đọc được (phụ thuộc controller).
7. **`ImportAsync` (~L114)** — `SaveAsync` file **trước** transaction DB; Persist fail đã xóa file mới nhưng vẫn có cửa sổ orphan / race import đồng thời.
8. **`ApproveAsync` không re-check payslip rỗng / có file** — chỉ Status PendingApproval (dữ liệu bất thường vẫn duyệt được).
9. **`RejectAsync` / `ReopenAsync`** — không validate Reason rỗng/null ở service (Reopen SRS bắt buộc lý do).

---

## 4. OutputService

**File:** `@be/Application/Services/Production/OutputService.cs` (~534 LOC)  
**Dependencies:** `IUnitOfWork`, `ProductionGuard`, `IPieceRateService`, `IAuditLogService`, `IAppLock`, `IClock`, `OutputCapTransfer`

### Public methods

#### `GetGridAsync(workDate, shiftCode, orderLineId, sewingTeamId?, ct)`
| | |
|---|---|
| **Input** | Ngày, ca, dòng đơn, tổ (optional cho Manager) |
| **Output** | Failure ca/team/line/not allotted; Success grid + Cap/Reported/Transferable |
| **Nhánh** | ShiftCodes.All; ResolveSewingTeam; IsAllotted; union keys assignment∪records∪caps>0; CanEdit = trong hạn hoặc Manager |
| **Deps** | OperationAssignment, OutputRecord, VwWorkerOutputCap, PieceRateService, OutputCapTransfer |

#### `GetDayGridAsync(workDate, orderLineId, sewingTeamId?, ct)`
| | |
|---|---|
| **Input** | Không có shift — cộng cả ngày |
| **Output** | CanEdit luôn false; ShiftCodes.AllDay |
| **Nhánh** | GroupBy sum Quantity; UnitRate FirstOrDefault trong group (có thể lệch nếu nhiều ca khác rate — hiếm) |

#### `SaveAsync(SaveOutputDto dto, ct)`
| | |
|---|---|
| **Input** | WorkDate, Shift, OrderLineId, SewingTeamId?, Rows[(Op,Worker,Qty,RowVer?)], Reason?, TransferExcess |
| **Output** | Validation / PeriodLocked / Order closed / Cap exceed / PieceRateNotApproved / Success counts + warnings |
| **Nhánh** | Qty&lt;0, Qty&gt;Max, duplicate key, future date; Classify insert/update/delete; edit window; Active op + đúng product + worker team; CheckCaps `others+qty <= cap`; transfer nếu flag; transaction + app lock |
| **Deps** | ProductionGuard, PieceRateService, OutputCapTransfer, AppLock, Audit, DailyTimesheet (MSG17) |

### Điểm nghi ngờ (≥5)

1. **`Quantity == 0` trên dòng mới → skip** (không insert); trên dòng cũ → delete. Client gửi thiếu dòng ≠ xóa — chỉ xóa khi gửi qty=0. Dễ “sót xóa” nếu FE omit row.
2. **Cap check N+1 query** từng (worker, op) — race: giữa CheckCaps và Persist, Confirm allocation khác có thể đổi Cap; dựa trigger 50052.
3. **`TransferExcess=true` thiếu nguồn** → fail ở `OutputCapTransfer.ApplyAsync` sau khi đã classify — UX lỗi muộn.
4. **Update không re-check PieceRate** (đúng GBR-11) nhưng cũng **không chặn Quantity tăng vượt khi PeriodLocked đã check ngày** — OK. Tuy nhiên **xóa/sửa sau hạn chỉ cần Reason nếu Manager** — TeamLeader sau hạn bị chặn; cần test biên `Now == EditDeadline` (`<=` cho phép đúng deadline).
5. **`GetDayGridAsync` `UnitRate = FirstOrDefault`** khi nhiều ca — rate hiển thị có thể không đại diện cả ngày.
6. **Không validate `workDate` quá khứ xa / trước ReceivedDate ở GetGrid** (chỉ Save check BeforeOrderStart) — grid vẫn mở được ngày vô nghĩa.
7. **`operations[k.OperationId]` (~L199)** — OperationId mồ côi trong keys → `KeyNotFoundException`.
8. **`LoadRatesAsync` `missing != 0` (~L349)** — nếu `OperationId == 0` (data hỏng) bỏ qua thiếu đơn giá.
9. **`CheckCapsAsync` cộng `others` mọi ca** trong khi Save thường một ca — dễ hiểu nhầm trần theo ca vs ngày (đúng design cộng dồn nhưng cần case test rõ).

---

## 5. MaterialIssueService

**File:** `@be/Application/Services/Warehouse/MaterialIssueService.cs` (~485 LOC)  
**Dependencies:** `IUnitOfWork`, `IStockService`, `FabricPlanner`, `IDocumentCodeGenerator`, `IClock`, `ILogger`, `IRequestService`, `IAuditLogService`, `INotificationService`

### Public methods

| Method | Input → Output | Nhánh chính |
|---|---|---|
| `GetPagedAsync` | query + receivingTeamId? → Paged | Lọc team/status/warehouse/order/WO/keyword |
| `GetByIdAsync` | issueId + team? → DTO / TeamMismatch | |
| `CreateAsync` | Create DTO + issuedBy → DTO | LoadRequest → Fill → ResolveTarget → CheckLines → EnsureAvailable → PendingReceipt |
| `ConfirmAsync` | issueId, ConfirmIssueDto, teamId, employeeId → Result | Pending + team; đủ lines; ConfirmedQty ≤ Issued; lệch cần reason; stock −qty; BTP → StartProduction; request MarkCompleted |
| `RejectAsync` | issueId, Reason, teamId → Result | Pending → Rejected; request vẫn Approved |

### Điểm nghi ngờ (≥5)

1. **`ConfirmAsync` cho phép `ConfirmedQty = 0` mọi dòng** → phiếu Received không trừ kho — “nhận 0” vẫn StartProduction nếu BTP.
2. **`ConfirmedQty < 0` không check ở service** (phụ thuộc validator) — nếu bypass → StockMovement âm kép hoặc lỗi DB.
3. **Stock chỉ trừ lúc Confirm, Create chỉ EnsureAvailable** — giữa Create và Confirm tồn có thể bị phiếu khác lấy; Confirm fail stock nhưng phiếu vẫn Pending.
4. **`StartProductionAsync` chỉ khi Status==NEW và MaterialStatus != Awaiting** — nhận BTP khi vẫn Awaiting thì **không** chuyển IN_PRODUCTION (im lặng).
5. **CheckOperationsAssigned chỉ SemiFinished** — cấp NVL thô không kiểm phân công (đúng design) nhưng cấp BTP với `lineIds` rỗng (phiếu không có OrderLineId?) → `products` rỗng → pass assignment check.
6. **Reject không hoàn tồn** vì chưa trừ — OK; nhưng không notify đủ role nếu IssuedBy inactive.
7. **`RejectAsync` (~L218)** — `NotifyEmployees` **không await**; `dto.Reason.Trim()` null → NRE.
8. **`GetPagedAsync` (~L48)** — `i.Order.OrderCode` / `IssuedByNavigation.FullName` thiếu null-safe → lỗi query nếu FK lệch.
9. **Có đơn xin: không chặn IssuedQty > số xin** (DEV-ALL-108, chỉ UI cảnh báo) — policy cần test chấp nhận.

---

## 6. ProductSpecService

**File:** `@be/Application/Services/Orders/ProductSpecService.cs` (~458 LOC)  
**Dependencies:** `IUnitOfWork`, `IAuditLogService`

### Public methods

| Method | Input → Output | Nhánh chính |
|---|---|---|
| `GetAsync(productId)` | → Spec DTO / NotFound | Chỉ norms Active |
| `SaveAsync(productId, dto)` | → Spec sau ghi | CheckAsync → transaction ApplySpec → GetAsync |
| `CheckAsync(productId, dto)` | → Result (không ghi) | Validate đầy đủ |
| `WriteAsync(productId, dto)` | → Result&lt;int&gt; | ApplySpec **không** tự Check / không tự transaction |

### Điểm nghi ngờ (≥5)

1. **`WriteAsync` tin người gọi đã Check + bọc transaction** — gọi nhầm từ chỗ khác → ghi thiếu validate / không atomic; có thể `KeyNotFoundException` / `.Single()` trong WriteNorms/WriteMarkerGroups.
2. **Match component theo tên (case-insensitive)** khi không có Id — đổi tên + thêm mới dễ khớp nhầm entity cũ.
3. **Variant không cho tạo mới** (từ đơn) — Save với màu mới → fail; nhưng norms theo màu không có trong variants gửi lên có thể bị xử lý lệch trong WriteNorms.
4. **Gỡ component “đã dùng” → soft discontinue thay vì xóa** — GetAsync vẫn có thể không show discontinued norms (chỉ Active) trong khi component Status discontinued vẫn trả về → UI lệch.
5. **Không validate PiecesPerGarment ≤ 0** ở tầng service (validator?) — biên 0 làm ước vải/cắt sai.
6. **`SaveAsync` (~L71)** — không kiểm `spec.IsSuccess` sau `GetAsync` → có thể Success với `Data` null / NRE `spec.Data!`.
7. **`RemoveStaleOperationLinksAsync`** — `componentByName[name]` không kiểm tên lệch → exception.

---

## 7. WorkOrderService

**File:** `@be/Application/Services/Cutting/WorkOrderService.cs` (~469 LOC)  
**Dependencies:** `IUnitOfWork`, `IStockService`, `FabricPlanner`, `IDocumentCodeGenerator`, `INotificationService`, `ILogger`, `IClock`, `WorkOrderFabricChecker`, `WorkOrderFabricNeeds`

### Public methods

| Method | Input → Output | Nhánh chính |
|---|---|---|
| `GetPagedAsync` / `GetByIdAsync` | filter team? | TeamMismatch; cut/handed qty; fabric checks chỉ Open thường |
| `CheckFabricAsync` | OrderId, Team, Lines | Bỏ qua OrderLineId không thuộc đơn (silent filter) |
| `GetFabricRequirementsAsync` / `GetFabricNeedsAsync` / `GetMaterialAllowanceAsync` | WO id (+ filter) | Needs chỉ `NeedMeters > 0` |
| `CreateAsync` | SaveWorkOrderDto | Validate (đơn mở, team cắt, due date, product/lines, **chặn thiếu vải** lệnh thường) |
| `GetOpenReplacementRequestsAsync` | — | Approved + chưa có WO |
| `CreateReplacementAsync` | RequestId + team + due | ValidateReplacement; **vải chỉ cảnh báo không chặn** |
| `UpdateAsync` / `CancelAsync` / `CloseShortAsync` | | Chỉ khi chưa cắt / điều kiện đóng thiếu |

### Điểm nghi ngờ (≥5)

1. **`CheckFabricAsync` bỏ line không map được** thay vì lỗi — FE gửi sai OrderLineId vẫn Success với list ngắn hơn.
2. **Lệnh thường chặn thiếu vải; lệnh bù không chặn** — cắt bù có thể Open dù kho thiếu (design) → bug vận hành nếu quên cấp.
3. **DueDate: `DueDate < Today` và `DueDate <= ReceivedDate`** — DueDate == Today OK; DueDate == ReceivedDate **fail** (`<=`) — biên ngày nhận đơn cần test.
4. **Create không kiểm PlannedQty = 0 / âm / lines rỗng** nếu validator lỏng — lệnh “0 sản phẩm”.
5. **GetById FabricChecks chỉ khi Open && không replacement** — WO đang cắt không còn cảnh báo thiếu vải dù thực tế thiếu.
6. **Cancel/CloseShort** (phần sau file): cần test còn BTP đã cắt / đã cấp — dễ orphan stock nếu cascade thiếu.
7. **`CreateAsync` / `CreateReplacementAsync` (~L181/L211)** — không kiểm `created.IsSuccess` trước `GetByIdAsync(created.Data)`.
8. **`GetPagedAsync` Status** so sánh raw string, không `ToCode` như đơn hàng — filter status Pascal/SNAKE dễ ra rỗng.

---

## 8. DefectService

**File:** `@be/Application/Services/Production/DefectService.cs` (~467 LOC)  
**Dependencies:** `IUnitOfWork`, `ProductionGuard`, `IPieceRateService`, `INotificationService`, `IAuditLogService`, `ICurrentUser`, `IClock`

### Public methods

| Method | Input → Output | Nhánh chính |
|---|---|---|
| `ListForTeamAsync` | DefectQuery | Manager xem mọi tổ; leader chỉ tổ mình; filter status/type/date/keyword |
| `ListMineAsync` | workerId + query | Ẩn Cancelled; Take MaxPageSize (không full paging) |
| `CreateAsync` | CreateDefectDto | Team leader only; ValidateInput; PeriodLocked; refs; Scrap cần PieceRate; OutputError check qty |
| `UpdateAsync` | Id + UpdateDefectDto | CheckMutable (cửa sổ giờ / ràng buộc); Manager bắt buộc Reason; đổi type/qty/rate |
| `CancelAsync` | Id + Reason | CheckMutable; luôn cần reason |

### Điểm nghi ngờ (≥5)

1. **`quantity <= 0` chặn; không có MaxQty** ở ValidateInput (Output có MaxQty) — scrap số cực lớn có thể âm “hàng trên chuyền”.
2. **Create: Manager không ghi được** (`ResolveSewingTeam(null)`) — đúng comment; nhưng Admin cũng fail → phụ thuộc role map.
3. **Rework không khóa PieceRate; Scrap khóa** — Update đổi Rework→Scrap cần rate; ngược lại release rate — dễ sót `CheckIncreaseWithinOnLine` / cap.
4. **ListMineAsync cắt `Take(MaxPageSize)`** — công nhân nhiều phiếu cũ không xem đủ, không báo total; `workerId <= 0` không validate.
5. **OutputError so với sản lượng đã báo** — cộng **cả ngày mọi ca** dù phiếu có ShiftCode; biên `>=` vs `>` cần test.
6. **`CreateAsync` (~L127)** — `_notifier.NotifyEmployees` **không await** trong transaction → mất thông báo im lặng.
7. **Create không kiểm `OrderLineTeam` allotted** (khác OutputService) — có thể ghi phiếu cho dòng chưa chia tổ.
8. **`BuildDefectAsync` `FirstAsync` Team** — teamId không tồn tại → exception thay vì Result.

---

## 9. CustomerOrderService

**File:** `@be/Application/Services/Orders/CustomerOrderService.cs` (~458 LOC)  
**Dependencies:** `ICustomerOrderRepository`, `IProductRepository`, `IGenericRepository<OrderLine>`, `IUnitOfWork`, `IClock`, `IAuditLogService`, `INotificationService` (+ OrderCloseCascade khi Cancel/Stop)

### Public methods

| Method | Input → Output | Nhánh chính |
|---|---|---|
| `ListAsync` / `ListOverviewAsync` | OrderQuery | Status lạ → 400 validation |
| `SuggestCodeAsync` | — | Parse suffix số sau `DHyyMM-` |
| `GetAsync` / `GetOverviewAsync` | orderId | NotFound; Get + Audit history |
| `CreateAsync` | CreateOrderDto | Trùng mã; staged products; bind temp Id; variants |
| `UpdateAsync` | Id + UpdateOrderDto + RowVer | NEW full edit; LimitedEdit; staged trong transaction |
| `CancelAsync` | Id + Reason + RowVer | Chỉ NEW → Cancelled + cascade |
| `StopAsync` | Id + Reason | Stoppable; chặn nếu FullyDispatched |
| `StartProductionAsync` | Id | NEW + MaterialStatus không Awaiting |

### Điểm nghi ngờ (≥5)

1. **`SuggestCodeAsync` race** — hai request cùng lúc → trùng mã (Create mới fail CodeDuplicate, Suggest thì không atomic).
2. **Update LimitedEdit: ApplyLines** có thể cấm xóa/sửa qty dòng đã sản xuất — cần test biên giảm OrderedQty &lt; đã cắt/giao.
3. **Staged ProductCode trùng trong DB** → fail cả đơn; staged trùng trong payload cũng fail — OK. Nhưng **TempId không có trong lines** bị bỏ qua im lặng.
4. **Cancel chỉ NEW** — đơn IN_PRODUCTION phải Stop; nếu FE gọi Cancel → InvalidStatus (OK) nhưng message chung.
5. **Stop + FullyDispatched** — định nghĩa “đủ xuất” vs “đủ giao khách” cần test PARTIALLY_DELIVERED; đơn 0 dòng (`ordered > 0` mới so) có thể Stop bất thường.
6. **DueDate &lt; ReceivedDate** — nếu chỉ validator FE/Fluent, service Apply không thấy check tường minh ở đoạn Create đã đọc.
7. **`Cancel`/`Stop`/`StartProduction`** — `GetByIdAsync` **không truyền `ct`** → hủy request không dừng đọc DB.

---

## 10. DailyWorkAllocationService

**File:** `@be/Application/Services/Production/DailyWorkAllocationService.cs` (~436 LOC)  
**Dependencies:** `IUnitOfWork`, `INotificationService`, `IAppLock`, `IClock`, `IPieceRateService`

### Public methods

#### `GetBoardAsync(teamId, workDate, orderId, orderLineId?, ct)`
| | |
|---|---|
| **Input** | Tổ, ngày, đơn, dòng optional |
| **Output** | Board rows (Carry, DayGiven, Remaining, Unallocated, Rate…) — **không check team tồn tại** ở Get |
| **Nhánh** | Chỉ dòng Allotted; union assignment + positions; Remaining = min(Carry+DayNet−DayOutput, Held−Output) ≥ 0 |
| **Deps** | OrderLineTeam, VwOperationAllocationProgress, Operation, Assignment, WorkAllocation, OutputRecord, PieceRate |

#### `SaveAsync(dto, teamId, ct)`
| | |
|---|---|
| **Input** | WorkDate **phải = Today**, Items[(Worker, Line, Op, Qty)], Reason? |
| **Output** | Failure date/team/workers/lines/rate/over unallocated; Success (added, adjusted) |
| **Nhánh** | Validate team sewing; workers thuộc tổ; lines allotted & đơn chưa đóng; PieceRate nếu Qty&gt;0; app-lock theo line; delta&gt;0 GiveMore (≤ free unallocated); delta&lt;0 Lower (pending trước, rồi ADJUSTED); Reason bắt buộc khi hạ |
| **Deps** | AppLock, WorkAllocation, views, Notifier |

### Điểm nghi ngờ (≥5)

1. **Chỉ lưu được “hôm nay”** — không backfill/sửa ngày trước (design) nhưng GetBoard vẫn mở ngày quá khứ → user tưởng Save được; GetBoard **không validate** teamId/orderId tồn tại.
2. **`Remaining` dùng `ConfirmedQty ?? 0` ở Held nhưng DayPending tách** — nếu ConfirmedQty null trên Handover CONFIRMED (data lỗi) → Remaining sai.
3. **GiveMore so với Unallocated của view** — race + lock theo line; vẫn phụ thuộc view freshness trong transaction.
4. **Hạ số: `fromConfirmed > Remaining` fail** — biên Remaining=0, DayGiven&gt;0 (pending) cần hạ pending trước; test `Quantity = DayPending only`.
5. **`NotifyWorkers` không await** — thất bại notification không báo caller.
6. **Không chặn Qty âm / trùng key** (worker×line×op) trong một payload — ValidateAsync cần xác nhận; trùng key → delta kép.
7. **`sequence[r.OperationId]` (~L113)** — operation lệch dictionary → `KeyNotFoundException` khi sort board.

---

## Ma trận ưu tiên test (gợi ý giai đoạn Unit / Integration 5.x)

| Ưu tiên | Service | Kịch bản biên nên viết trước |
|:---:|---|---|
| P0 | OutputService.SaveAsync | Vượt cap; TransferExcess; qty=0 xóa; đúng EditDeadline; period locked |
| P0 | MaterialReceiptService.ConfirmAsync | Đủ lần giao → SHORT_DELIVERY; thiếu ảnh; vượt outstanding; AcceptedQty=0 |
| P0 | DailyWorkAllocationService.SaveAsync | Hạ số có/không reason; vượt Unallocated; không phải Today |
| P0 | PayrollService.Import/Approve/Reopen | Lệch NetPay; tháng khóa; Approve khóa period |
| P1 | WorkOrderService.Create / Replacement | Thiếu vải chặn vs không chặn; DueDate == ReceivedDate |
| P1 | DefectService.Create/Update | Scrap không rate; OutputError = đúng sản lượng; cửa sổ giờ |
| P1 | MaterialIssueService.Confirm | Confirm 0; BTP chưa phân công; StartProduction khi Awaiting |
| P1 | CustomerOrderService.Stop/Cancel | FullyDispatched; cascade; race SuggestCode |
| P2 | OperationService.Import replace | File lỗi hết dòng; discontinue final step |
| P2 | ProductSpecService.Write vs Save | Gọi Write không Check; PiecesPerGarment=0 |

---

## Kết luận nhanh

Các service shortlist **đã có nhiều guard nghiệp vụ** (Result + validator + DB trigger), nhưng điểm yếu chung hay gặp:

1. **Tin FluentValidation / DB** — service thiếu defense-in-depth cho âm/0/null Id / `Reason.Trim()`.  
2. **Điều kiện biên `<=` / `<`** (deadline, DueDate, cap, outstanding).  
3. **Race thiếu app-lock** (receipt confirm, suggest order code).  
4. **Success một phần** (import Excel bỏ dòng lỗi; Confirm qty=0).  
5. **Side-effect trạng thái im lặng** (StartProduction không chạy; replace import 0 dòng).  
6. **Không kiểm `Result.IsSuccess` sau transaction** rồi gọi `GetByIdAsync(created.Data)` (Receipt / WorkOrder).  
7. **Notify fire-and-forget** (`NotifyEmployees` không await — Issue Reject, Defect Create, Daily allocation).  
8. **KeyNotFound / NRE runtime** trên dictionary navigation / Trim null (Output grid, Daily board, OperationCode).

File này là đầu vào cho thiết kế test case (chưa gồm code test).

---

## Phụ lục — nguồn đối chiếu

Đã đối chiếu / bổ sung phát hiện chi tiết từ các vòng review song song:

- [QA Ops/Output/Defect](7e85e4ea-a9c3-45e9-8556-ed19a27e603a)
- [QA Warehouse/Payroll](da60f7fd-b645-4f6e-850b-e51be63bc500)
- [QA Orders/Cutting/Daily](065d29cf-bfca-465b-9fae-a82dc558fd37)
