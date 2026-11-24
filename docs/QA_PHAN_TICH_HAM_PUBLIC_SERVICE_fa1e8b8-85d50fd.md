# QA — Phân tích hàm public Service (`fa1e8b8` → `85d50fd`)

> **Vai trò:** Senior QA Engineer  
> **Đầu vào phạm vi:** [`PHAN_LOAI_ANH_HUONG_SERVICE_fa1e8b8-85d50fd.md`](./PHAN_LOAI_ANH_HUONG_SERVICE_fa1e8b8-85d50fd.md)  
> **Mục đích:** Phân tích từng hàm public để chuẩn bị viết / viết lại unit test. **Chưa gồm code test.**  
> **Với mỗi hàm:** (1) Chữ ký · (2) Luồng & nhánh · (3) Dependency · (4) Rủi ro / bug tiềm ẩn (3–5+)

---

## Thứ tự ưu tiên (P0 / P1 / P2)

| Mức | Ý nghĩa | Service |
|---|---|---|
| **P0** | Nhóm A — đổi hành vi cũ → viết lại / mở rộng test | `FinanceService` → `OrderPaymentService` → Spec (`SpecImport` + `ProductSpec` + `SpecImportBuilder`) → `EmployeeService` + `TeamService` (+ settle) |
| **P1** | Nhóm B nghiệp vụ nặng | `SettleOnTeamLeaveAsync` + `VehicleService` (FuelCost + ảnh hóa đơn) |
| **P2** | Nhóm B mới / smoke | `AlertSyncOverdueAsync`, `MachineRepair` / `NoLine` handlers, `HomeDashboard*` theo vai trò |

---

# P0-1. `FinanceService`

**File:** `be/Application/Services/Finance/FinanceService.cs`  
**UC:** Doanh thu (UC27) + Hòa vốn (UC11, DEV-ALL-171)  
**DI:** `IFinanceRepository`, `IFinanceExcelWriter`, `IAuditLogService`, `IClock`, `ILogger<FinanceService>`

**Đổi chính:** Hòa vốn luôn tính/lưu **hai** `AllocationBasis` (`OUTPUT` + `LABOUR_HOURS`); thêm `GetBreakEvenInputsAsync`.

---

### `GetRevenueAsync`

| | |
|---|---|
| **Input** | `RevenueQuery` (`PeriodMonth?`, `OrderId?`, `ProductId?`, Page/PageSize), `ct` |
| **Output** | `Result<PagedResult<RevenueSummaryDto>>` |
| **Failure** | `RevenuePeriodMonthInvalid` (Validation) nếu tháng có giá trị nhưng không khớp `yyyy-MM` |

**Luồng:** Validate tháng (nếu có) → `GetRevenueRowsAsync` → Normalize page → Skip/Take in-memory → `ToRevenueDtosAsync` → Success (`NoData` nếu total=0).

**Dependency:** `IFinanceRepository.GetRevenueRowsAsync`, `GetOrderCodesAsync`, `GetProductsAsync`.

**Rủi ro:**
1. Phân trang sau khi load full view → chậm/OOM.
2. `PeriodMonth` null/empty → trả mọi tháng.
3. Order/Product thiếu → code `""`, không NotFound.
4. `FinishedQty`/`Revenue` null → ép 0.
5. Không null-check `query`.

---

### `GetRevenueFilterOptionsAsync`

| | |
|---|---|
| **Input** | `ct` |
| **Output** | `Result<RevenueFilterOptionsDto>` (Orders, Products) |

**Luồng:** Load toàn bộ revenue → GroupBy OrderId/ProductId → First code → OrderBy → Success (kể cả rỗng).

**Rủi ro:** Load full chỉ để filter; `First()` nếu code lệch; không lọc theo tháng; soft-delete vẫn hiện.

---

### `ExportRevenueAsync`

| | |
|---|---|
| **Input** | `RevenueQuery`, `ct` |
| **Output** | `Result<ExcelFileDto>` (`DoanhThu.xlsx`) |
| **Failure** | `RevenuePeriodMonthInvalid` |

**Luồng:** Validate → full rows → DTO → `WriteRevenueSheet` → Success (`ExcelExported`). List rỗng vẫn Success.

**Rủi ro:** Không giới hạn size; tên file cố định; writer exception không wrap; filter không phản ánh trong tên file.

---

### `CalculateAsync` ⭐

| | |
|---|---|
| **Input** | `BreakEvenCalculateDto(PeriodMonth)`, `ct` |
| **Output** | `Result<BreakEvenCalculationResultDto>` (Items = productCount × 2 basis) |
| **Failure** | `BreakEvenPeriodMonthInvalid`; `BreakEvenNotReady`; `BreakEvenNoOutput` |

**Luồng:**
```
Validate month
FindReadyPeriod (period tồn tại + payroll approved + has monthly cost) → null = NotReady
GetRevenueRows → Count==0 = NoOutput
BuildEntities (mỗi Product × OUTPUT + LABOUR_HOURS):
  Group revenue theo ProductId
  Fixed = sum Fixed; Variable group theo ProductId (bỏ biến phí không ProductId)
  outputShare = qty/total (total=0 → 0)
  labourShare = LabourShares (fallback outputShare; normalize tổng 100%)
  NextCalculatedAt (tránh UNIQUE trùng giây)
  ComputeBreakEven từng (product, basis)
SaveResults (Add + Audit + SaveChanges)
Map DTO + Distinct AtRiskWarnings → Success
```

**Công thức `ComputeBreakEven`:**
- `CmtUnitPrice = Qty>0 ? Round(Revenue/Qty,2) : 0`
- `AllocatedFixed = Round(totalFixed * share, 2)`
- `VarPerUnit = Qty>0 ? Var/Qty : 0`
- `BreakEvenQty = (Cmt ≤ VarPerUnit) ? null : Round(Allocated/(Cmt−Var), 2)`
- `PercentReached`: null / 100 nếu BE=0 / else Qty/BE×100
- `Surplus = Round(Revenue − Var − Allocated, 2)`; `IsAtRisk = Surplus < 0`

**Dependency:** Full `IFinanceRepository` hòa vốn + labour (`GetOutputForLabour`, `GetWorkedHours`) + `IAuditLogService`, `IClock`.

**Rủi ro:**
1. Biến phí không gắn `ProductId` bị bỏ → thặng dư ảo.
2. Làm tròn share từng dòng → Σ Allocated ≠ totalFixed.
3. `LabourShares` fallback + renormalize pha loãng mã có giờ công thật.
4. `FinishedQty=0` vẫn có revenue row → edge AtRisk.
5. `Cmt == VarPerUnit` → BreakEvenQty=null (`<=`).
6. Period Locked không chặn Calculate (DEV-ALL-172).
7. Race cùng giây → UNIQUE 409.
8. `IsAtRisk` chỉ theo Surplus&lt;0, không theo PercentReached&lt;100.

---

### `GetBreakEvenInputsAsync` ⭐ (mới)

| | |
|---|---|
| **Input** | `string periodMonth`, `ct` |
| **Output** | `Result<BreakEvenInputsDto>` |
| **Failure** | `BreakEvenPeriodMonthInvalid`. Period null → **Success** flags false (không NotFound) |

**Luồng:** Validate → FindPeriod + RevenueRows → đếm product/qty/revenue → period null → Success zeros → else PayrollApproved, costs, LabourHours, `CanCalculate = payroll && costs.Count>0 && productCount>0`, `PeriodLocked = Status==Locked`.

**Rủi ro:**
1. **Lệch cổng với Calculate:** Inputs dùng `costs.Count>0`; Calculate dùng `HasMonthlyCostAsync`.
2. Locked không chặn `CanCalculate`.
3. Period null vẫn trả productCount/revenue từ view.
4. LabourHours=0 không chặn (đúng design).
5. `VariableProductCount` không đối chiếu với product có doanh thu.

---

### `GetBreakEvenAsync`

| | |
|---|---|
| **Input** | `periodMonth`, `productId?`, `history`, `ct` |
| **Output** | `Result<BreakEvenListResultDto>` |
| **Failure** | Invalid month; `BreakEvenNotFound` (không kỳ hoặc không kết quả) |

**Luồng:** FindPeriod → GetResults → history=false giữ Max(CalculatedAt) → SharePct theo Σ AllocatedFixed cùng (CalculatedAt, Basis) → ToDto (dùng `r.Product`).

**Rủi ro:** totalFixed khi đọc = Σ allocated (không phải fixed gốc); filter productId + history=false trên subset; Product null → NRE; kỳ có nhưng chưa tính → cùng NotFound; không sort ổn định.

---

### `ExportBreakEvenAsync`

| | |
|---|---|
| **Input** | giống Get |
| **Output** | `ExcelFileDto` (`HoaVon_{periodMonth}.xlsx`) |

**Luồng:** Gọi Get → fail propagate → WriteBreakEvenSheet → Success.

**Rủi ro:** Phụ thuộc bug Get; history=true file lớn; tên file không phản ánh productId/history; writer exception không bắt.

---

### Điểm nóng QA — Finance

| # | Khu vực |
|---|---|
| 1 | Calculate × 2 AllocationBasis — assert `productCount × 2` |
| 2 | Inputs vs FindReadyPeriod (`costs.Count` vs `HasMonthlyCost`) |
| 3 | ComputeBreakEven biên Cmt≤Var, BE=0, Qty=0 |
| 4 | LabourShares fallback + normalize |
| 5 | Biến phí không ProductId bị drop |

---

# P0-2. `OrderPaymentService`

**File:** `be/Application/Services/Finance/OrderPaymentService.cs`  
**DEV-ALL-137**  
**DI:** `IUnitOfWork`, `IAuditLogService`, `IClock`  
**Đổi chính:** List/Get `OrderAmount` = Σ `OrderLines.OrderedQty × CmtUnitPrice` (+ Include).

---

### `ListAsync`

| | |
|---|---|
| **Input** | `OrderPaymentQuery` (Keyword, State?, Page) |
| **Output** | `Result<PagedResult<OrderPaymentListItemDto>>` |

**Luồng filter State:**
- `NotSet` → FinalAmount null
- `Settled` → Final != null && Sum(Paid) == Final
- `Unpaid` → Final != null && Final != 0 && Paid == 0
- `Partial` → Final != null && Paid != 0 && Paid != Final  
State lạ → list rỗng.

**Amount** = Σ OrderedQty × CmtUnitPrice. Map Remaining + StateOf.

**Rủi ro:** State sai casing → rỗng; FinalAmount=0 → Settled (không Unpaid); Amount phụ thuộc OrderLines; Contains theo collation; Sum Payments lặp trong SQL.

---

### `GetAsync`

| | |
|---|---|
| **Input** | `orderId`, `ct` |
| **Output** | `OrderPaymentDto` (+ Payments, History, RowVer) |
| **Failure** | NotFound |

**Luồng:** Load Include Customer/Payments/OrderLines → Paid → CanEdit = IsEditable (≤24h) → Audit history → OrderAmount từ dòng đơn.

**Rủi ro:** Customer null → NRE; List vs Get biểu thức Amount hơi khác; History table `"OrderPayment"`; CanEdit phụ thuộc IClock.

---

### `SetFinalAmountAsync`

| | |
|---|---|
| **Input** | `orderId`, `SetFinalAmountDto(Amount, Reason, RowVer)` |
| **Failure** | NotFound; Reason blank; `!InRange(Paid, Amount)` → FinalAmountBelowPaid |

**Luồng:** Touch RowVer → gán FinalAmount/Reason → TX Audit + Save. Cho phép Amount âm/0. Không so với OrderAmount CMT.

**Rủi ro:** Không ràng buộc với Σ CMT; message BelowPaid dùng cả khi final âm; Final=0 + Paid=0 → Settled, sau đó không add payment được; concurrent 409 qua RowVer.

---

### `AddPaymentAsync`

| | |
|---|---|
| **Input** | `AddOrderPaymentDto(PaymentDate, Amount, Method, Note?, RowVer)` |
| **Failure** | NotFound; chưa chốt Final; method/amount=0/date future/out of range |

**Luồng:** Validate method ∈ {BANK_TRANSFER,CASH}; Amount≠0; Date≤Today; `InRange(Paid+Amount, Final)` → Add payment CreatedAt=now → Audit Confirm.

**Rủi ro:** Biên Paid+Amount == Final OK; Final âm chỉ nhận hướng tổng về final; NeedFinalAmount Kind=Conflict (khác Validation); không khóa theo status đơn.

---

### `UpdatePaymentAsync`

| | |
|---|---|
| **Input** | `orderId`, `paymentId`, `UpdateOrderPaymentDto` (+ Reason) |
| **Failure** | NotFound (đơn/payment/**Final null**); EditExpired; Validation |

**Luồng:** IsEditable ≤24h → Validate + Reason + InRange(others+new) → Touch → cập nhật + EditReason.

**Rủi ro:** Đúng 24h vẫn sửa (`<=`); Final null → NotFound (không Validation); RowVer trên Order không trên Payment; **không xóa** — không về Paid=0 nếu chỉ còn 1 lần (Amount≠0).

---

### Helpers static

| Helper | Quy tắc |
|---|---|
| `IsEditable` | `now - CreatedAt <= 24h` |
| `InRange(paid, final)` | `paid ∈ [Min(0,final), Max(0,final)]` |
| `Remaining` | `final - paid` (null nếu chưa chốt) |
| `StateOf` | null→NotSet; paid==final→Settled; paid==0→Unpaid; else Partial |

---

# P0-3. Cụm Spec (`ProductSpecService` + `SpecImportBuilder` + `SpecImportService`)

**Đổi chính:** Màu đã có ở mã hàng nhưng **chưa có size** (`ColorOnlySize = "?"`); Import **tạo màu thiếu**; validate/save nhận color-only.

---

## A. `ProductSpecService`

**File:** `be/Application/Services/Orders/ProductSpecService.cs`  
**DI:** `IUnitOfWork`, `IAuditLogService`  
**Public:** `GetAsync`, `SaveAsync`, `CheckAsync`, `WriteAsync`

### `GetAsync`
Product exists? → load Components (mọi status), Variants, Norms **Active**, MarkerComponents/Fabrics → DTO.  
**Rủi ro:** Norms ẩn Discontinued; Components/Variants hiện cả ngừng dùng; NRE navigation orphan.

### `CheckAsync` / `ValidateAsync`
Pieces ∈[1,99]; trùng component/variant; variant phải đã có từ đơn (không tạo mới); **colors = Variants ∪ ProductColor** (color-only OK); norms reference; material Active; Panel 1 vải/màu; ValidateMarkerGroups (Layout/Coverage/Fabric/LockedGroup/InUse).

**Rủi ro:** Color-only hợp lệ nhưng variant list có thể thiếu màu; ColorIds First() nếu trùng tên; Validate message-only.

### `SaveAsync` / `WriteAsync` / `ApplySpec`
Save = Check + TX(Apply) + Get lại. Write = Apply không mở TX.  
Apply: match Id/tên; colorByName + ColorIds referenced; ClearMarkerGroups → RemoveDropped → UpsertComponents → WriteNorms → RemoveStaleOpLinks → WriteMarkerGroups (`Single()` vải) → Audit.

**Rủi ro:** `Single()` nếu bypass Check; KeyNotFound màu; Clear giữa chừng ngoài TX mất nhóm; Resign-style partial SaveChanges trong Apply.

---

## B. `SpecImportBuilder`

**File:** `be/Application/Services/Orders/SpecImportBuilder.cs` — thuần tính, không I/O.

**Constructor:** `(spec, materials, orderProduct?, productCode, workbook, replace, usage?)`  
**Actions:** New | Update | Unchanged | Error | Pending* | NewMaterial*  
**Hằng:** `ColorOnlySize = "?"`

**Luồng:** Index keys file → BuildComponents/Norms/Groups → **MergeSpec (strip Size=`?`)** → OrderMarkers/MergeMarkers → ComputeRemoved (replace).

| Bảng | Lỗi chính |
|---|---|
| Components | ParseError, Name>100, Pieces∉[1,99], trùng |
| Norms | màu lạ, component lạ, material null, Note>200, trùng Color\|Comp\|Mat |
| Groups | MarkerNo max, không PANEL, **mọi** màu unknown; màu lạ một phần → note skip |
| OrderMarkers | ProductCode khác → skip; Width null; không size đơn; nhóm unknown |

**Replace:** bỏ dòng cũ không có trong file **trừ** usage (component/norm pair/group đã dùng). Order markers **không** có usage-keep tương đương.

**Rủi ro:** Merge thay cả bộ NVL theo cặp Color\|Comp; group subset màu im lặng; SpecChanged không gồm OrderMarkers; DuplicateCount vs ErrorCount; size `"?"` thật cũng bị strip.

---

## C. `SpecImportService`

**Files:** `SpecImportService.cs` + `SpecImportService.Check.cs` + `SpecImportParts.cs`  
**DI:** UoW, `IProductSpecService`, `IOrderMarkerService`, Excel parser, validators, `IClock`

### `PreviewAsync` (Stream | Workbook)
PlanAsync `createColors:false` → WithColorOnly + AssumeColors → Builder → CrossErrors chỉ khi `colorsToCreate==0` → Preview.  
**Rủi ro:** CanImport=true khi còn màu chưa tạo (bỏ CrossErrors); parts case-sensitive; parse catch rộng.

### `ImportAsync` (Stream | Workbook)
TX: Plan `createColors:true` (EnsureColors) → !CanImport fail → Write (specs.Write + markers Check/Write nếu cần).  
**Rủi ro:** EnsureColors SaveChanges giữa Plan; Material unknown = Error (khác Check NewMaterial); color tạo không có variant size thật.

### `CheckAsync` (popup)
productId? null OK; AssumeColors + AssumeMaterials (Id âm); MarkAssumed Pending/NewMaterial; CrossErrors chỉ khi đủ màu+NVL thật.  
**Rủi ro:** MarkAssumed **không** cập nhật CanImport; productId??0; HasOrderMarkers set kể cả chưa có đơn.

### Ma trận Spec

| # | Kịch bản |
|---|---|
| 1 | Color-only ProductColor — Save norm/marker OK |
| 2 | Import tạo màu + strip `?` khi ghi |
| 3 | Preview CanImport khi colorsToCreate>0 |
| 4 | Check Pending/NewMaterial/Pending marker |
| 5 | Replace giữ Used; group skip màu unknown một phần |
| 6 | Panel 1 vải / GroupFabric / LockedGroup |

---

# P0-4. `EmployeeService` + `TeamService` (+ Settle)

**Đổi chính:** Trước khi gán `TeamId` mới → `SettleOnTeamLeaveAsync` trong cùng transaction.

---

## `EmployeeService`

**DI thêm:** `IDailyWorkAllocationService`

### `ListAsync` / `GetAsync`
Filter Role/Status; Get + History; !CanSee → NotFound.

### `CreateAsync` / `ImportFromRjAsync`
Office creatable / RJ Worker; CheckPlacement; CreateWithAccount TX (temp password 1 lần). Không settle.

### `AdminUpdateAsync` / `ManagerUpdateAsync` ⭐
```
Check Active + placement + scope
teamChanged = oldTeamId != dto.TeamId
ApplyTeamChangeAndSaveAsync (TX):
  if teamChanged && oldTeamId is int from:
      SettleOnTeamLeaveAsync(from, [id])  // TRƯỚC gán TeamId
  TeamId = new; Audit; Save; MapAtt?
```

**Rủi ro:**
1. Chỉ settle khi oldTeamId != null (null→team không settle — đúng).
2. Không phân biệt may/văn phòng ở Employee (Settle no-op).
3. **`ResignAsync` không settle, không clear TeamId** — gap nghiêm trọng.
4. Update có thể đổi TeamId của tổ trưởng (TransferIn thì cấm).
5. Reason audit dùng Today, không có EffectiveDate.

### `ResignAsync`
Resigned + RotateStamp; **không settle**. Có thể vẫn giữ TeamId.

---

## `TeamService`

**DI thêm:** `IDailyWorkAllocationService`

### CRUD tổ
List/Get/Create/Update/Discontinue/Reactivate — **không** settle. Đổi type / discontinue cần 0 Active members.

### `TransferInAsync` ⭐
EffectiveDate > Today → fail; chặn Leader/Deputy trong list; TX: GroupBy oldTeam → Settle từng nhóm → gán TeamId target + stamp + audit + LogTransfer. Message + suffix nếu tổ cũ Sewing.

**Rủi ro:** EffectiveDate quá khứ chỉ ghi audit; duplicate EmployeeIds → NotFound; target Office vẫn OK; settledSewing theo TeamType nguồn.

### `RemoveMembersAsync` ⭐
Chặn leader; TX: Settle(teamId, ids) → TeamId=null. Suffix nếu Sewing.

### `AddMembersAsync` / `AppointAsync` / `DismissAsync` / `CandidatesAsync`
Add chỉ nhận TeamId null (không settle). Appoint hạ holder rồi nâng trong TX. Dismiss về Worker **ở lại tổ** (không settle).

---

## Ma trận HR + Settle

| # | Case |
|---|---|
| 1 | Employee đổi tổ may A→B trong 1 TX |
| 2 | RemoveMembers: reject HO pending, confirm RT, retrieval Remaining, đóng assignment |
| 3 | TransferIn nhiều nguồn; chặn Leader; date tương lai |
| 4 | Tổ văn phòng → Settle no-op |
| 5 | Lock timeout → TeamId không đổi |
| 6 | Resign còn TeamId may — không settle |
| 7 | Employee Update vs Transfer quy tắc leader |

---

# P1-1. `SettleOnTeamLeaveAsync`

**File:** `be/Application/Services/Production/DailyWorkAllocationService.cs`  
**Input:** `oldTeamId`, `workerIds`, `ct` → `Result`  
**Failure:** `TeamOrderBusy` (lock timeout)

**Luồng:**
```
workerIds rỗng → Success
Team null / !Sewing → Success (no-op)
Distinct workers → lineIds từ WorkAllocation
Lock work-allocation:{team}:{line}
PENDING Handover → Rejected + reason
PENDING Retrieval → Confirmed (System) + note
Save nếu có pending
Positions Remaining > 0 → Add Retrieval ADJUSTED CONFIRMED
OperationAssignment ValidTo=null → ValidTo=Today
Save → Success
```

**Dependency:** Team, WorkAllocation, OperationAssignment, `IAppLock`, `IClock`, UoW. **Không** notifier.

**Rủi ro:** No-op im lặng; Remaining sai → sót/thừa; lock tuần tự một line fail cả batch; reject HO không notify; ValidTo=Today ambiguous trong ngày; gọi ngoài TX → partial commit.

---

# P1-2. `VehicleService` — `CompleteTripAsync` (fuel)

**File:** `be/Application/Services/Vehicle/VehicleService.cs`  
**DI:** UoW, `ISystemSettingReader`, Audit, Clock, `IAppLock`

### Các hàm khác (tóm tắt)
GetVehicles / LiveStatus / Create / Update / ChangeStatus / GetTrips / GetTrip / StartTrip(+Prepare lock) / GetAvailable — hành vi cũ; KmGapWarning vẫn lưu khi start.

### `CompleteTripAsync` ⭐

| | |
|---|---|
| **Input** | `tripId`, `CompleteVehicleTripDto(OdometerEnd, FuelLiters?, FuelCost?)` |
| **Output** | `Result<VehicleTripDto>` |
| **Failure** | NotFound; InvalidStatus; Validation odometerEnd / fuelCost |

**Luồng:**
```
Status == Started?
OdometerEnd <= Start → Validation (field odometerEnd)
FuelCost != null && !HasFuelReceipt (Attachment FuelReceipt IsCurrent)
  → Validation fuelCost = FuelReceiptRequired
Gán End/Liters/Cost; Status=Completed
FuelOverNormWarning = Liters > norm*(1+tolerance/100)  // cảnh báo, vẫn lưu
Audit + Save
```

**Liên quan:** `VehicleTripAttachmentAccess` — Accountant write, Admin/Accountant read, max 5 ảnh.

**Rủi ro:**
1. Chỉ bắt ảnh khi `FuelCost != null` (có Liters không bắt).
2. Validator ngoài: FuelCost nguyên dương ≤100M.
3. Chỉ `IsCurrent` attachment.
4. `OdometerEnd <= Start` (bằng cũng fail).
5. Over-norm không chặn; Audit after **không** ghi FuelCost.
6. Complete không lock xe (khác Start).

---

# P2-1. `AttendanceService.AlertSyncOverdueAsync`

**File:** `be/Application/Services/Attendance/AttendanceService.cs`  
**Gọi từ:** `AttendanceSyncMonitorJob`  
**Output:** `Task` (không Result)

**Luồng:**
```
GetHealth: overdue = lastAt==null OR minutes > threshold (default 30)
!overdue → return
since = LastSucceededAt ?? MinValue
Notification Ref=AttendanceSyncOverdue AND CreatedAt > since → return (đã báo đợt này)
NotifyRole Admin + SaveChanges + LogWarning
```

**Dependency:** Attendance repo, Settings, Notification repo, `INotificationService`, UoW, Logger.

**Rủi ro:**
1. Chưa từng sync: báo 1 lần rồi im đến khi có sync thành công.
2. `minutes == threshold` → **không** overdue (`>`).
3. Không Result — job khó biết fail.
4. Chỉ Admin (không Manager).
5. Notify + SaveChanges tách.

---

# P2-2. `NoLineRequestHandler` + `MachineRepairRequestHandler`

### `NoLineRequestHandler` (abstract)
- `AllowManualComplete = true`
- `ValidateAsync`: Lines.Count>0 → fail `_noLinesMessage`; **create** (`RequestId==null`) + TargetDate &lt; Today → fail `RequisitionTargetDatePast`; else Success
- `OnStatusChangedAsync` → luôn Success

**Rủi ro:** TargetDate==Today OK; **update** bỏ qua check ngày quá khứ; TargetDate null không check; ErrorCode chung `RequestLinesInvalid`.

### `MachineRepairRequestHandler`
- `RequestType = MACHINE_REPAIR`; message `MachineRepairNoLines`
- Policy: Leaders Cutting/Sewing → Electrician
- Không logic riêng ngoài base

---

# P2-3. `HomeDashboardService` (smoke theo vai trò)

**Files:** `HomeDashboardService.cs` + partial theo role  
**Chỉ đọc;** lỗi service con thường **nuốt → rỗng/0**.

| Hàm | Snapshot | Smoke |
|---|---|---|
| `GetAdminAsync` | PieceRate/Payroll pending; revenue/cost this vs last; running orders | RunningStatuses Pascal; cost null khi chưa nhập |
| `GetManagerAsync` | Receipts pending; inbox 5; acting leaders; open orders | Inbox fail → empty |
| `GetAccountantAsync` | Missing punch; piece-rate gaps; unpaid PO; orders chưa chốt; cost/payroll tháng | Timesheet fail → 0 |
| `GetWarehouseAsync` | Incoming; WO chưa cấp; requests; cut/fg/return pending | Incoming fail → (0,[]) |
| `GetTeamLeaderAsync` | Cutting / Sewing theo TeamType; TeamId null → panels null | Switch type |
| `GetQaqcAsync` | On-line qty; finished chưa NK; FG pending | Max(0, issued−finished−scrap) |
| `GetElectricianAsync` | Max 20 ticket CreatedBy=me, status open | |
| `GetWorkerAsync` | Est pay; payslip; pending alloc; defects 7 ngày; meal | Không khoán → est null; không có hôm nay → Today=0 |

**Rủi ro chung:** Nuốt lỗi che backend; lệch casing Status; không auth trong service (controller phải chặn).

---

# Checklist viết test (sau Clean Re-baseline)

## P0
1. Finance — Calculate ×2 basis; Inputs vs NotReady; biên công thức; LabourShares
2. OrderPayment — OrderAmount từ OrderLines; InRange dương/âm/0; State Final=0; Edit 24h
3. Spec — color-only; Import tạo màu; Preview CanImport; Check Pending/NewMaterial; Replace Used
4. HR — Update/Transfer/Remove + Settle trong TX; Resign gap; Leader rules

## P1
5. Settle — pending HO/RT; Remaining; ValidTo; lock timeout; no-op non-sewing
6. Vehicle Complete — FuelCost + không ảnh fail; có IsCurrent OK; FuelCost null không bắt ảnh

## P2
7. Alert — overdue 1 lần / im lần 2 / sync rồi overdue lại; biên threshold
8. NoLine/MachineRepair — có lines fail; TargetDate quá khứ lúc create
9. Home — mỗi Get*Async Success shape + 1 nhánh fail dependency → fallback

---

*Tài liệu này tổng hợp phân tích QA từ phiên làm việc theo `PHAN_LOAI_ANH_HUONG_SERVICE_fa1e8b8-85d50fd.md`. Cập nhật khi code service đổi hành vi.*
