# Unit Test Case — Re-baseline theo `Report-5.1_Unit_Test.xlsx` (`fa1e8b8` → `85d50fd`)

> Đầu vào nghiệp vụ: `QA_PHAN_TICH_HAM_PUBLIC_SERVICE_fa1e8b8-85d50fd.md`
> Đối chiếu với file báo cáo hiện tại: `Report-5.1_Unit_Test.xlsx` (14 sheet, 532 TC, UTCID01–UTCID532). Chỉ đọc, **không sửa** file Excel.
> Bản này **thay thế** file `UNIT_TEST_CASES_REBASELINE_fa1e8b8-85d50fd.md` gửi trước đó, vì dải UTC502–942 trong file đó trùng với UTCID502–532 đã có trong báo cáo.

## Quy ước

- **Mã Test Case:** giữ định dạng của báo cáo là `UTCIDnnn`. Case mới đánh số liên tục từ **UTCID533 đến UTCID974**. Không dùng lại số cũ, không reset.
- **Thứ tự cấp số:** trước hết là các sheet đã có trong file (theo thứ tự sheet: ProductSpecService → DailyWorkAllocationService → VehicleService), sau đó đến các sheet mới (theo ưu tiên P0 → P2). Cách này giống cách báo cáo đã làm ở v2.1 / v2.2.
- **Case sửa giữ nguyên ID cũ.** Nên reset kết quả Round 3 của các case này về `Pending`.
- **Đồng hồ:** `IClock.Today = 2026-11-24`, `Now = 2026-11-24 10:00 (+07)`. Môi trường giống sheet *Test Cases*: .NET 9 / xUnit / NSubstitute / FluentAssertions.
- **⚠** = case dự kiến FAIL trên code hiện tại (theo mục "Rủi ro" của tài liệu phân tích). **(xác nhận spec)** = hành vi mong muốn chưa được chốt.
- Bảng có thêm cột *Function Name* theo yêu cầu. Khi dán vào sheet, cột này tương ứng với dòng tiêu đề group (ví dụ `GetAsync`) như các sheet hiện có.

---

## Phần 1 — Kết quả đối chiếu từng sheet

| # | Sheet trong file | ID hiện có | Ảnh hưởng | Hành động |
|---|---|---|---|---|
| 1 | Cover | — | Có | Thêm dòng *Record of change* (gợi ý ở Phần 5) |
| 2 | Test Cases | — | Có | Thêm 9 dòng module mới + cập nhật mô tả 3 module (Phần 5) |
| 3 | Test Statistics | — | Có | Cập nhật số liệu (Phần 5) |
| 4 | OperationService | UTCID01–70 | Không | Giữ nguyên |
| 5 | MaterialReceiptService | UTCID71–134 | Không | Giữ nguyên |
| 6 | PayrollService | UTCID135–203 | Không | Giữ nguyên |
| 7 | OutputService | UTCID204–251 | Không | Giữ nguyên |
| 8 | MaterialIssueService | UTCID252–291 | Không | Giữ nguyên |
| 9 | **ProductSpecService** | UTCID292–314 | **Có** (color-only, Pieces ∈ [1,99], Get trả Components mọi status) | **Sửa 5 case** (UTCID292, 294, 296, 301, 313) + **thêm 18 case** (UTCID533–550) |
| 10 | WorkOrderService | UTCID315–373 | Không | Giữ nguyên |
| 11 | DefectService | UTCID374–415 | Không | Giữ nguyên |
| 12 | CustomerOrderService | UTCID416–469 | Không (OrderAmount DEV-ALL-137 nằm ở OrderPaymentService) | Giữ nguyên |
| 13 | **DailyWorkAllocationService** | UTCID470–511 | **Có**: hàm mới `SettleOnTeamLeaveAsync`. GetBoard/Save không đổi | Giữ nguyên 42 case + **thêm 17 case** (UTCID551–567) |
| 14 | **VehicleService** | UTCID512–532 | **Có**: `CompleteTripAsync` (FuelCost + ảnh hóa đơn). GetLiveStatus/StartTrip không đổi | Giữ nguyên 21 case + **thêm 20 case** (UTCID568–587) |
| 15 | **FinanceService** *(sheet mới)* | — | Service mới trong phạm vi | Tạo sheet, **72 case** (UTCID588–659) |
| 16 | **OrderPaymentService** *(sheet mới)* | — | Service mới trong phạm vi | Tạo sheet, **90 case** (UTCID660–749) |
| 17 | **SpecImportBuilder** *(sheet mới)* | — | Service mới trong phạm vi | Tạo sheet, **37 case** (UTCID750–786) |
| 18 | **SpecImportService** *(sheet mới)* | — | Service mới trong phạm vi | Tạo sheet, **33 case** (UTCID787–819) |
| 19 | **EmployeeService** *(sheet mới)* | — | Service mới trong phạm vi | Tạo sheet, **40 case** (UTCID820–859) |
| 20 | **TeamService** *(sheet mới)* | — | Service mới trong phạm vi | Tạo sheet, **60 case** (UTCID860–919) |
| 21 | **AttendanceService** *(sheet mới)* | — | Service mới trong phạm vi | Tạo sheet, **12 case** (UTCID920–931) |
| 22 | **MachineRepairRequestHandler** *(sheet mới)* | — | Service mới trong phạm vi | Tạo sheet, **14 case** (UTCID932–945) |
| 23 | **HomeDashboardService** *(sheet mới)* | — | Service mới trong phạm vi | Tạo sheet, **29 case** (UTCID946–974) |

### Chi tiết ProductSpecService (UTCID292 – UTCID314)

| ID | Group | Kết quả Round 2 | Trạng thái | Lý do |
|---|---|---|---|---|
| UTCID292 | GetAsync | Passed | **Sửa** | DTO nay gồm Components mọi status, MarkerComponents/Fabrics |
| UTCID293 | GetAsync | Passed | Giữ nguyên | — |
| UTCID294 | GetAsync | Passed | **Sửa** | Hành vi đã chốt: Norm Discontinued ẩn, Component Discontinued vẫn trả kèm Status (bỏ "ẩn hoặc gắn cờ" và ⚠) |
| UTCID295 | GetAsync | Passed | Giữ nguyên | — |
| UTCID296 | SaveAsync | Passed | **Sửa** | Thêm thứ tự Check → TX(Apply) → Commit → Get lại; có marker group |
| UTCID297 – UTCID300 | SaveAsync | Passed | Giữ nguyên | UTCID300 (Pieces=1) là biên dưới của [1,99] |
| UTCID301 | SaveAsync | Passed (sau fix) | **Sửa** | Pieces=0 nay bị chặn ở CheckAsync theo rule [1,99]; bỏ ⚠ "service không check" |
| UTCID302 – UTCID312 | Save/Check/Write | Passed | Giữ nguyên | — |
| UTCID313 | WriteAsync | Passed | **Sửa** | Rủi ro mới khi bypass Check: `Single()` vải Panel / KeyNotFound màu |
| UTCID314 | WriteAsync | Passed | Giữ nguyên | — |

---

## Phần 2 — Case sửa (giữ ID cũ) · sheet `ProductSpecService`


| Test Case ID | Function Name | Type (Normal/Boundary/Abnormal) | Pre-conditions | Input parameters | Expected Results (Return / Exception / Log Message) |
|---|---|---|---|---|---|
| UTCID292 | ProductSpecService.GetAsync | Normal | Product 1: 3 Component (2 Active, 1 Discontinued), 2 Variant (Đỏ-M, Xanh-L), 4 Norm Active, 1 MarkerGroup có MarkerComponents + MarkerFabrics | productId=1 | `Result.Success`; Components=3 (đủ mọi status), Variants=2, Norms=4, MarkerGroups có Components/Fabrics |
| UTCID294 | ProductSpecService.GetAsync | Boundary | Product 1 có 1 Norm Discontinued; Component "Túi" Discontinued | productId=1 | Norm Discontinued **không** có trong DTO; Component "Túi" **có** trong DTO với Status=Discontinued |
| UTCID296 | ProductSpecService.SaveAsync | Normal | Product 1 có spec; mock Audit; UoW spy | dto hợp lệ (component, norm Đỏ/Xanh, marker group 1 PANEL 1 vải/màu) | Gọi CheckAsync → BeginTransaction → Apply → Commit → GetAsync; trả Spec mới; Audit log 1 lần |
| UTCID301 | ProductSpecService.SaveAsync | Boundary | Product 1 | Component PiecesPerGarment=0 | Failure (Validation: Pieces ∈ [1,99]); BeginTransaction không gọi |
| UTCID313 | ProductSpecService.WriteAsync | Abnormal | **Không** gọi CheckAsync; dto có marker group PANEL với 2 vải cho cùng màu Đỏ, và norm tham chiếu màu "Tím" không có trong ProductColor | productId=1 | Failure (Validation) an toàn. ⚠ `Single()` ném `InvalidOperationException` / `KeyNotFoundException` màu |

---

## Phần 3 — Case thêm vào sheet có sẵn

### Sheet `ProductSpecService` — thêm UTCID533–UTCID550 (18 case)

Thêm vào cuối sheet `ProductSpecService`, sau UTCID314. Group theo hàm (CheckAsync / SaveAsync / WriteAsync / GetAsync). Gộp với case đang có trong sheet thì mỗi hàm đủ N/B/A: SaveAsync có Abnormal ở UTCID303–306; WriteAsync có Normal/Boundary ở UTCID311–312.

| Test Case ID | Function Name | Type (Normal/Boundary/Abnormal) | Pre-conditions | Input parameters | Expected Results (Return / Exception / Log Message) |
|---|---|---|---|---|---|
| UTCID533 | ProductSpecService.CheckAsync | Normal | Product 1: Variants Đỏ-M, Xanh-L; ProductColor có thêm "Vàng" (color-only, chưa có size) | dto norm cho màu "Vàng" | Success (colors = Variants ∪ ProductColor) |
| UTCID534 | ProductSpecService.SaveAsync | Normal | Như trên | dto norm + marker group cho màu "Vàng" | Success; norm/marker "Vàng" được ghi; không tạo Variant mới |
| UTCID535 | ProductSpecService.CheckAsync | Boundary | Product 1 | Component PiecesPerGarment=99 | Success (biên trên) |
| UTCID536 | ProductSpecService.CheckAsync | Boundary | Product 1 | Component PiecesPerGarment=100 | Failure (Validation: Pieces ∈ [1,99]) |
| UTCID537 | ProductSpecService.CheckAsync | Boundary | Product 1 | Component PiecesPerGarment=-1 | Failure (Validation) |
| UTCID538 | ProductSpecService.CheckAsync | Abnormal | Product 1 | 2 Variant trùng (Đỏ-M, Đỏ-M) | Failure (DuplicateVariant) |
| UTCID539 | ProductSpecService.CheckAsync | Abnormal | Product 1 không có màu "Tím" ở Variants lẫn ProductColor | norm màu "Tím" | Failure (ColorUnknown) |
| UTCID540 | ProductSpecService.CheckAsync | Abnormal | Product 1 | norm tham chiếu component "Cầu vai" không có trong dto | Failure (Norm reference invalid) |
| UTCID541 | ProductSpecService.CheckAsync | Abnormal | Vật tư M7 Status=Inactive | norm dùng M7 | Failure (MaterialInactive) |
| UTCID542 | ProductSpecService.CheckAsync | Abnormal | Product 1 | component Panel, màu Đỏ có 2 vải khác nhau | Failure (Panel chỉ được 1 vải/màu) |
| UTCID543 | ProductSpecService.CheckAsync | Abnormal | Product 1 | marker group Layout không hợp lệ (không có component PANEL) | Failure (MarkerGroup Layout) |
| UTCID544 | ProductSpecService.CheckAsync | Abnormal | Product 1 | marker group không phủ đủ component PANEL (Coverage thiếu) | Failure (MarkerGroup Coverage) |
| UTCID545 | ProductSpecService.CheckAsync | Abnormal | Product 1 | marker group vải không khớp vải norm (GroupFabric) | Failure (MarkerGroup Fabric) |
| UTCID546 | ProductSpecService.CheckAsync | Abnormal | Group G1 đã khóa (LockedGroup, có sơ đồ cắt) | dto sửa G1 | Failure (LockedGroup) |
| UTCID547 | ProductSpecService.CheckAsync | Abnormal | Group G2 đã dùng (InUse) | dto bỏ G2 | Failure (MarkerGroup InUse) |
| UTCID548 | ProductSpecService.CheckAsync | Abnormal | ProductColor có 2 màu cùng tên "Đỏ" (data lệch) | dto norm màu "Đỏ" | Báo lỗi trùng hoặc chọn đúng Id; không gán nhầm im lặng (rủi ro `First()`) |
| UTCID549 | ProductSpecService.WriteAsync | Abnormal | Gọi Write ngoài TX; mock SaveChanges ném exception sau bước ClearMarkerGroups | productId=1, dto hợp lệ | Exception propagate. Ghi nhận: marker group cũ đã bị xóa (partial commit) khi gọi ngoài TX |
| UTCID550 | ProductSpecService.GetAsync | Abnormal | Norm có MaterialId trỏ vật tư đã xóa (orphan navigation) | productId=1 | Bỏ qua/trả MaterialName=null an toàn. ⚠ `NullReferenceException` |

### Sheet `DailyWorkAllocationService` — thêm UTCID551–UTCID567 (17 case)

Thêm group mới **SettleOnTeamLeaveAsync** vào cuối sheet `DailyWorkAllocationService`, sau UTCID511. Các case Settle thu hồi phần còn giữ với Status=**CONFIRMED (System)**. Đây là quyết toán cưỡng bức khi rời tổ, khác luồng SaveAsync (PENDING/WORKER, UTCID504–509).

| Test Case ID | Function Name | Type (Normal/Boundary/Abnormal) | Pre-conditions | Input parameters | Expected Results (Return / Exception / Log Message) |
|---|---|---|---|---|---|
| UTCID551 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Normal | Tổ may 3. W1 trên line 100 có: Handover H1 PENDING, Retrieval R1 PENDING, Position Remaining=10, OperationAssignment OA1 ValidTo=null; mock IAppLock OK | oldTeamId=3, workerIds=[W1] | Success; lock key "work-allocation:3:100"; H1 → Rejected kèm reason; R1 → Confirmed (System) kèm note; thêm Retrieval ADJUSTED qty=10 Status=**CONFIRMED** (System), khác luồng SaveAsync tạo PENDING/WORKER ở UTCID504; OA1.ValidTo=2026-11-24; SaveChanges 2 lần |
| UTCID552 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Boundary | W1×OP10 line 100: Retrieval R1 PENDING qty=5 (DayPendingRetrieval=5); Remaining hiển thị = 15 (đã trừ 5 theo DEV-ALL-157) | oldTeamId=3, [W1] | R1 → Confirmed (System) qty=5; Retrieval ADJUSTED CONFIRMED qty=15. Tổng thu hồi = 20 = toàn bộ hàng đang giữ (không trừ kép phần R1, không thu thừa) |
| UTCID553 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Normal | W1 có allocation trên line 100 và 101 | oldTeamId=3, [W1] | Lock lần lượt "…:3:100" và "…:3:101"; xử lý pending + Remaining cho cả 2 line |
| UTCID554 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Boundary | — | oldTeamId=3, workerIds=[] | Success ngay; không query, không lock, không SaveChanges |
| UTCID555 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Boundary | Như case Normal | workerIds=[W1,W1] | Xử lý W1 đúng 1 lần (Distinct); chỉ 1 Retrieval ADJUSTED |
| UTCID556 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Boundary | W1 không có HO/RT PENDING; Remaining=5 | [W1] | SaveChanges đầu (cho pending) **không** gọi; thêm Retrieval 5; SaveChanges cuối gọi 1 lần |
| UTCID557 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Boundary | Position Remaining=0 | [W1] | Không thêm Retrieval ADJUSTED |
| UTCID558 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Boundary | Position Remaining=1 | [W1] | Thêm Retrieval ADJUSTED CONFIRMED qty=1 |
| UTCID559 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Boundary | OA2 đã có ValidTo=2026-11-20 | [W1] | OA2 giữ nguyên ValidTo=2026-11-20 (chỉ đóng assignment ValidTo=null) |
| UTCID560 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Boundary | W2 không có WorkAllocation nào; OA3 ValidTo=null | [W2] | Không lock; OA3.ValidTo=Today; Success |
| UTCID561 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Abnormal | — | oldTeamId=999 (không tồn tại), [W1] | Success no-op; không ghi DB. Ghi nhận rủi ro no-op im lặng |
| UTCID562 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Abnormal | Tổ 1 là tổ cắt | oldTeamId=1, [W1] | Success no-op; không lock, không SaveChanges |
| UTCID563 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Abnormal | Tổ văn phòng Office1 | oldTeamId=Office1, [E4] | Success no-op |
| UTCID564 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Abnormal | Lock line 100 OK, lock line 101 timeout; gọi trong TX của caller | oldTeamId=3, [W1] | Failure `TeamOrderBusy`; caller Rollback; không thay đổi nào được commit |
| UTCID565 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Abnormal | Gọi **ngoài** TX; SaveChanges lần 2 ném exception | [W1] | Exception propagate. Ghi nhận partial commit: H1/R1 đã đổi trạng thái ở lần Save 1 |
| UTCID566 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Abnormal | H1 PENDING thuộc người nhận W5 | [W1] | H1 Rejected; người liên quan nên được thông báo. ⚠ Không gọi notifier |
| UTCID567 | DailyWorkAllocationService.SettleOnTeamLeaveAsync | Abnormal | — | workerIds=null | `ArgumentNullException` hoặc coi như rỗng (Success) |

### Sheet `VehicleService` — thêm UTCID568–UTCID587 (20 case)

Thêm group mới **CompleteTripAsync** vào cuối sheet `VehicleService`, sau UTCID532. Các group GetLiveStatusAsync / StartTripAsync (UTCID512–532) giữ nguyên vì StartTrip không đổi hành vi (KmGapWarning vẫn lưu, khớp UTCID523).

| Test Case ID | Function Name | Type (Normal/Boundary/Abnormal) | Pre-conditions | Input parameters | Expected Results (Return / Exception / Log Message) |
|---|---|---|---|---|---|
| UTCID568 | VehicleService.CompleteTripAsync | Normal | Trip T1 Status=Started, OdometerStart=10.000; định mức 12L/100km, tolerance 10% (`ISystemSettingReader`) | tripId=T1, dto {OdometerEnd=10.150, FuelLiters=15, FuelCost=null} | Success; Status=Completed; End=10.150, Liters=15; FuelOverNormWarning=false (ngưỡng 19,8L); Audit + SaveChanges |
| UTCID569 | VehicleService.CompleteTripAsync | Normal | T1 Started; có attachment FuelReceipt IsCurrent=true | dto {OdometerEnd=10.150, FuelLiters=15, FuelCost=500.000} | Success; FuelCost=500.000 lưu |
| UTCID570 | VehicleService.CompleteTripAsync | Boundary | T1 Start=10.000 | OdometerEnd=10.000 (= Start) | Failure Validation, field `odometerEnd` (`<=`) |
| UTCID571 | VehicleService.CompleteTripAsync | Boundary | T1 Start=10.000 | OdometerEnd=10.001 | Success |
| UTCID572 | VehicleService.CompleteTripAsync | Boundary | 150km × 12L/100km = 18L, tolerance 10% → ngưỡng 19,8L | FuelLiters=19,8 | Success; FuelOverNormWarning=false |
| UTCID573 | VehicleService.CompleteTripAsync | Boundary | Như trên | FuelLiters=19,81 | Success (vẫn lưu); FuelOverNormWarning=true |
| UTCID574 | VehicleService.CompleteTripAsync | Boundary | T1 Started, không attachment | FuelLiters=null, FuelCost=null | Success; không yêu cầu ảnh |
| UTCID575 | VehicleService.CompleteTripAsync | Boundary | T1 Started, không attachment | FuelLiters=20, FuelCost=null | Success (chỉ bắt ảnh khi FuelCost ≠ null, rủi ro #1). Xác nhận spec |
| UTCID576 | VehicleService.CompleteTripAsync | Boundary | Có FuelReceipt IsCurrent | FuelCost=1 | Success (biên dưới validator) |
| UTCID577 | VehicleService.CompleteTripAsync | Boundary | Có FuelReceipt IsCurrent | FuelCost=100.000.000 | Success (biên trên validator) |
| UTCID578 | VehicleService.CompleteTripAsync | Boundary | Có FuelReceipt IsCurrent | FuelCost=100.000.001 | Failure Validation (validator ngoài service) |
| UTCID579 | VehicleService.CompleteTripAsync | Abnormal | Có FuelReceipt IsCurrent | FuelCost=0 hoặc -1 | Failure Validation (FuelCost phải nguyên dương) |
| UTCID580 | VehicleService.CompleteTripAsync | Abnormal | T1 không có attachment FuelReceipt | FuelCost=500.000 | Failure Validation, field `fuelCost`, code `FuelReceiptRequired`; trip vẫn Started |
| UTCID581 | VehicleService.CompleteTripAsync | Abnormal | T1 có FuelReceipt nhưng IsCurrent=false (đã thay) | FuelCost=500.000 | Failure `FuelReceiptRequired` |
| UTCID582 | VehicleService.CompleteTripAsync | Abnormal | T1 Start=10.000 | OdometerEnd=9.990 | Failure Validation `odometerEnd` |
| UTCID583 | VehicleService.CompleteTripAsync | Abnormal | — | tripId=999 | Failure `NotFound` |
| UTCID584 | VehicleService.CompleteTripAsync | Abnormal | T2 đã Completed | tripId=T2 | Failure `InvalidStatus` |
| UTCID585 | VehicleService.CompleteTripAsync | Abnormal | T3 chưa Start (Planned) | tripId=T3 | Failure `InvalidStatus` |
| UTCID586 | VehicleService.CompleteTripAsync | Abnormal | Có FuelReceipt IsCurrent | FuelCost=500.000 | Bản ghi Audit "after" chứa FuelCost. ⚠ Audit after không ghi FuelCost (rủi ro #5) |
| UTCID587 | VehicleService.CompleteTripAsync | Abnormal | 2 request Complete đồng thời cho T1 | tripId=T1 × 2 | Request thứ 2 Failure (`InvalidStatus`/Concurrency). ⚠ Complete không lock xe (rủi ro #6) |

---

## Phần 4 — Sheet mới

### Sheet `FinanceService` (P0-1) — UTCID588–UTCID659 (72 case)

| Test Case ID | Function Name | Type (Normal/Boundary/Abnormal) | Pre-conditions | Input parameters | Expected Results (Return / Exception / Log Message) |
|---|---|---|---|---|---|
| UTCID588 | FinanceService.GetRevenueAsync | Normal | View doanh thu tháng 2026-10: (O1,P1), (O1,P2), (O2,P3); mock GetOrderCodesAsync, GetProductsAsync trả đủ mã | query {PeriodMonth="2026-10", Page=1, PageSize=10} | Success `PagedResult`: Total=3, Items có OrderCode/ProductCode đúng |
| UTCID589 | FinanceService.GetRevenueAsync | Normal | Như trên | query {PeriodMonth="2026-10", OrderId=O1} | Total=2 (chỉ đơn O1) |
| UTCID590 | FinanceService.GetRevenueAsync | Normal | Như trên | query {PeriodMonth="2026-10", ProductId=P2} | Total=1 |
| UTCID591 | FinanceService.GetRevenueAsync | Boundary | Dữ liệu tháng 2026-09 (2 dòng) và 2026-10 (3 dòng) | PeriodMonth=null | Trả cả 5 dòng (không lọc tháng) |
| UTCID592 | FinanceService.GetRevenueAsync | Boundary | Như trên | PeriodMonth="" | Trả 5 dòng (coi như không lọc), không báo invalid |
| UTCID593 | FinanceService.GetRevenueAsync | Boundary | — | PeriodMonth="2026-01" | Success (tháng biên dưới hợp lệ) |
| UTCID594 | FinanceService.GetRevenueAsync | Boundary | — | PeriodMonth="2026-12" | Success (tháng biên trên hợp lệ) |
| UTCID595 | FinanceService.GetRevenueAsync | Boundary | Không có dòng nào tháng 2026-11 | PeriodMonth="2026-11" | Success; Items rỗng; Total=0; Message = `NoData` |
| UTCID596 | FinanceService.GetRevenueAsync | Boundary | 3 dòng | Page=2, PageSize=2 | Items=1; Total=3 |
| UTCID597 | FinanceService.GetRevenueAsync | Boundary | 3 dòng | Page=0, PageSize=0 | Normalize về Page=1, PageSize mặc định; không exception |
| UTCID598 | FinanceService.GetRevenueAsync | Boundary | 3 dòng; MaxPageSize=100 | PageSize=10000 | PageSize bị chặn về 100 |
| UTCID599 | FinanceService.GetRevenueAsync | Boundary | Dòng có FinishedQty=null, Revenue=null | PeriodMonth="2026-10" | DTO FinishedQty=0, Revenue=0 |
| UTCID600 | FinanceService.GetRevenueAsync | Abnormal | — | PeriodMonth="2026-13" | Failure `RevenuePeriodMonthInvalid` (Validation); GetRevenueRowsAsync không gọi |
| UTCID601 | FinanceService.GetRevenueAsync | Abnormal | — | PeriodMonth="2026/10" | Failure `RevenuePeriodMonthInvalid` |
| UTCID602 | FinanceService.GetRevenueAsync | Abnormal | Dòng có OrderId=O9 không có trong GetOrderCodesAsync | PeriodMonth="2026-10" | Success; OrderCode="" (không NotFound, không exception) |
| UTCID603 | FinanceService.GetRevenueAsync | Abnormal | — | query=null | `ArgumentNullException` hoặc Failure (Validation). ⚠ Không null-check (rủi ro #5) |
| UTCID604 | FinanceService.GetRevenueFilterOptionsAsync | Normal | Doanh thu: O1 (P1,P2), O2 (P3) | — | Success; Orders=[O1,O2] sort theo mã; Products=[P1,P2,P3] sort theo mã |
| UTCID605 | FinanceService.GetRevenueFilterOptionsAsync | Boundary | View doanh thu rỗng | — | Success; Orders rỗng, Products rỗng |
| UTCID606 | FinanceService.GetRevenueFilterOptionsAsync | Boundary | O1 có doanh thu ở 3 tháng khác nhau | — | O1 chỉ xuất hiện 1 lần (GroupBy) |
| UTCID607 | FinanceService.GetRevenueFilterOptionsAsync | Abnormal | Cùng OrderId=O1 nhưng 2 dòng có code khác nhau (data lệch) | — | Success, lấy code đầu tiên; không exception |
| UTCID608 | FinanceService.GetRevenueFilterOptionsAsync | Abnormal | Product P3 đã soft-delete nhưng còn doanh thu | — | P3 vẫn hiện trong Products (xác nhận spec: có nên ẩn hay không) |
| UTCID609 | FinanceService.GetRevenueFilterOptionsAsync | Abnormal | Mock repo ném exception | — | Exception propagate (không nuốt) |
| UTCID610 | FinanceService.ExportRevenueAsync | Normal | 3 dòng tháng 2026-10; mock `IFinanceExcelWriter.WriteRevenueSheet` trả byte[] | query {PeriodMonth="2026-10"} | Success ExcelFileDto: FileName="DoanhThu.xlsx", Content không rỗng; Message=`ExcelExported`; writer nhận 3 DTO |
| UTCID611 | FinanceService.ExportRevenueAsync | Boundary | Không có dữ liệu tháng 2026-11 | PeriodMonth="2026-11" | Success; writer được gọi với list rỗng |
| UTCID612 | FinanceService.ExportRevenueAsync | Boundary | 5 dòng nhiều tháng | PeriodMonth=null | Xuất toàn bộ 5 dòng (không phân trang) |
| UTCID613 | FinanceService.ExportRevenueAsync | Abnormal | — | PeriodMonth="2026-00" | Failure `RevenuePeriodMonthInvalid`; writer không gọi |
| UTCID614 | FinanceService.ExportRevenueAsync | Abnormal | Mock writer ném exception | PeriodMonth="2026-10" | Exception propagate (không wrap thành Result) |
| UTCID615 | FinanceService.CalculateAsync | Normal | Period 2026-10 sẵn sàng (payroll Approved, có monthly cost). Doanh thu: P1 qty=1.000 / 20.000.000đ; P2 qty=500 / 15.000.000đ. Định phí 9.000.000đ. Biến phí P1=8.000.000đ, P2=6.000.000đ. Giờ công P1=600h, P2=400h | dto {PeriodMonth="2026-10"} | Success; Items=4 (2 product × OUTPUT + LABOUR_HOURS). **P1·OUTPUT**: share 2/3, Allocated=6.000.000, Cmt=20.000, VarPerUnit=8.000, BreakEvenQty=500, Percent=200, Surplus=6.000.000, IsAtRisk=false. **P1·LABOUR**: share 0,6, Allocated=5.400.000. Repo Add 4 entity, Audit, SaveChanges 1 lần |
| UTCID616 | FinanceService.CalculateAsync | Normal | Như trên + P3 qty=500 / 5.000.000đ, biến phí 4.000.000đ, Allocated OUTPUT=3.000.000 | PeriodMonth="2026-10" | P3: Surplus = −2.000.000, IsAtRisk=true ở cả 2 basis; AtRiskWarnings chỉ chứa P3 **1 lần** (Distinct) |
| UTCID617 | FinanceService.CalculateAsync | Boundary | Product có Cmt = VarPerUnit (Revenue 10.000.000, qty 1.000, biến phí 10.000.000) | PeriodMonth="2026-10" | BreakEvenQty=null, PercentReached=null (điều kiện `Cmt <= VarPerUnit`) |
| UTCID618 | FinanceService.CalculateAsync | Boundary | Cmt < VarPerUnit (biến phí 12.000.000, qty 1.000, revenue 10.000.000) | PeriodMonth="2026-10" | BreakEvenQty=null, PercentReached=null; IsAtRisk=true |
| UTCID619 | FinanceService.CalculateAsync | Boundary | Tổng định phí = 0 | PeriodMonth="2026-10" | AllocatedFixed=0, BreakEvenQty=0, PercentReached=100 |
| UTCID620 | FinanceService.CalculateAsync | Boundary | Có dòng doanh thu P4 với FinishedQty=0, Revenue=2.000.000 | PeriodMonth="2026-10" | P4: Cmt=0, VarPerUnit=0, BreakEvenQty=null; Surplus = Revenue − Var − Allocated (rủi ro #4) |
| UTCID621 | FinanceService.CalculateAsync | Boundary | Tổng qty mọi product = 0 | PeriodMonth="2026-10" | outputShare=0 → AllocatedFixed=0 cho basis OUTPUT; không lỗi chia 0 |
| UTCID622 | FinanceService.CalculateAsync | Boundary | P2 không có giờ công (LabourShares thiếu P2) | PeriodMonth="2026-10" | Share LABOUR của P2 fallback = outputShare; sau normalize, Σ share LABOUR = 100%. Ghi nhận P1 bị pha loãng (rủi ro #3) |
| UTCID623 | FinanceService.CalculateAsync | Boundary | 3 product chia đều định phí 10.000.000 | PeriodMonth="2026-10" | Allocated mỗi product = 3.333.333,33; Σ = 9.999.999,99 (lệch 0,01 do làm tròn, rủi ro #2). Xác nhận spec có cần dồn phần dư hay không |
| UTCID624 | FinanceService.CalculateAsync | Boundary | Số liệu cho Surplus = 0 đúng | PeriodMonth="2026-10" | IsAtRisk=false (điều kiện `Surplus < 0`) |
| UTCID625 | FinanceService.CalculateAsync | Boundary | Đã có kết quả CalculatedAt = 2026-11-24 10:00:00 | PeriodMonth="2026-10" (Now cùng giây) | Kết quả mới có CalculatedAt = 10:00:01 (NextCalculatedAt); không vi phạm UNIQUE |
| UTCID626 | FinanceService.CalculateAsync | Boundary | Có biến phí 2.000.000 không gắn ProductId | PeriodMonth="2026-10" | Biến phí chung phải được phân bổ hoặc có cảnh báo. ⚠ Hiện bị bỏ qua → thặng dư ảo (rủi ro #1) |
| UTCID627 | FinanceService.CalculateAsync | Abnormal | — | PeriodMonth="2026-13" | Failure `BreakEvenPeriodMonthInvalid`; repo không gọi |
| UTCID628 | FinanceService.CalculateAsync | Abnormal | — | dto.PeriodMonth=null | Failure `BreakEvenPeriodMonthInvalid` |
| UTCID629 | FinanceService.CalculateAsync | Abnormal | Không có AccountingPeriod 2026-10 | PeriodMonth="2026-10" | Failure `BreakEvenNotReady` |
| UTCID630 | FinanceService.CalculateAsync | Abnormal | Period có, payroll chưa Approved | PeriodMonth="2026-10" | Failure `BreakEvenNotReady` |
| UTCID631 | FinanceService.CalculateAsync | Abnormal | Period có, payroll Approved, `HasMonthlyCostAsync`=false | PeriodMonth="2026-10" | Failure `BreakEvenNotReady` |
| UTCID632 | FinanceService.CalculateAsync | Abnormal | Period sẵn sàng, 0 dòng doanh thu | PeriodMonth="2026-10" | Failure `BreakEvenNoOutput`; không Add/SaveChanges |
| UTCID633 | FinanceService.CalculateAsync | Abnormal | Period 2026-10 Status=Locked, đủ điều kiện | PeriodMonth="2026-10" | Success (Locked không chặn Calculate, theo DEV-ALL-172). Xác nhận spec |
| UTCID634 | FinanceService.CalculateAsync | Abnormal | Mock SaveChanges ném `DbUpdateException` (UNIQUE, race cùng giây) | PeriodMonth="2026-10" | Failure Conflict (409) hoặc exception propagate; không có kết quả nửa vời (rủi ro #7) |
| UTCID635 | FinanceService.GetBreakEvenInputsAsync | Normal | Period 2026-10: payroll Approved, 3 khoản chi phí, doanh thu 2 product (qty 1.500 / 35.000.000đ), giờ công 1.000h, Status=Open | periodMonth="2026-10" | Success {ProductCount=2, TotalQty=1.500, TotalRevenue=35.000.000, PayrollApproved=true, CostCount=3, LabourHours=1.000, CanCalculate=true, PeriodLocked=false} |
| UTCID636 | FinanceService.GetBreakEvenInputsAsync | Normal | Như trên, Status=Locked | periodMonth="2026-10" | PeriodLocked=true; CanCalculate=true (Locked không chặn, rủi ro #2) |
| UTCID637 | FinanceService.GetBreakEvenInputsAsync | Boundary | Không có period 2026-09; view có 1 product doanh thu | periodMonth="2026-09" | **Success** (không NotFound); PayrollApproved=false, CostCount=0, CanCalculate=false; ProductCount/Revenue vẫn lấy từ view (rủi ro #3) |
| UTCID638 | FinanceService.GetBreakEvenInputsAsync | Boundary | Period có, payroll Approved, 0 khoản chi phí | periodMonth="2026-10" | CanCalculate=false |
| UTCID639 | FinanceService.GetBreakEvenInputsAsync | Boundary | Period có, đủ chi phí, ProductCount=0 | periodMonth="2026-10" | CanCalculate=false |
| UTCID640 | FinanceService.GetBreakEvenInputsAsync | Boundary | Đủ điều kiện, LabourHours=0 | periodMonth="2026-10" | CanCalculate=true (giờ công 0 không chặn, đúng design) |
| UTCID641 | FinanceService.GetBreakEvenInputsAsync | Boundary | Payroll Draft | periodMonth="2026-10" | PayrollApproved=false; CanCalculate=false |
| UTCID642 | FinanceService.GetBreakEvenInputsAsync | Abnormal | — | periodMonth="2026-13" | Failure `BreakEvenPeriodMonthInvalid` |
| UTCID643 | FinanceService.GetBreakEvenInputsAsync | Abnormal | — | periodMonth=null | Failure `BreakEvenPeriodMonthInvalid` |
| UTCID644 | FinanceService.GetBreakEvenInputsAsync | Abnormal | costs.Count=2 nhưng `HasMonthlyCostAsync`=false (không có khoản chi phí tháng) | periodMonth="2026-10", sau đó gọi CalculateAsync cùng tháng | Hai hàm cho kết quả nhất quán. ⚠ Inputs báo CanCalculate=true trong khi Calculate trả `BreakEvenNotReady` (rủi ro #1) |
| UTCID645 | FinanceService.GetBreakEvenAsync | Normal | Period 2026-10 có 2 lần tính T1 < T2, mỗi lần 4 dòng (2 product × 2 basis) | periodMonth="2026-10", productId=null, history=false | Success; 4 dòng của T2; SharePct trong mỗi nhóm (T2, Basis) cộng = 100% |
| UTCID646 | FinanceService.GetBreakEvenAsync | Normal | Như trên | history=true | 8 dòng (cả T1, T2) |
| UTCID647 | FinanceService.GetBreakEvenAsync | Normal | Như trên | productId=P1, history=false | 2 dòng (P1·OUTPUT, P1·LABOUR_HOURS) của T2 |
| UTCID648 | FinanceService.GetBreakEvenAsync | Boundary | P1 chỉ có ở T1, không có ở T2 | productId=P1, history=false | Trả dòng P1 của T1 (Max tính trên tập đã lọc). Xác nhận spec: "mới nhất của P1" hay "lần tính gần nhất của kỳ" |
| UTCID649 | FinanceService.GetBreakEvenAsync | Boundary | Mọi AllocatedFixed = 0 | periodMonth="2026-10" | SharePct=0; không lỗi chia 0 |
| UTCID650 | FinanceService.GetBreakEvenAsync | Boundary | 4 dòng chèn theo thứ tự ngẫu nhiên | periodMonth="2026-10" | Thứ tự ổn định (theo ProductCode, Basis). ⚠ Không có sort ổn định |
| UTCID651 | FinanceService.GetBreakEvenAsync | Abnormal | — | periodMonth="2026-13" | Failure `BreakEvenPeriodMonthInvalid` |
| UTCID652 | FinanceService.GetBreakEvenAsync | Abnormal | Không có period 2026-08 | periodMonth="2026-08" | Failure `BreakEvenNotFound` |
| UTCID653 | FinanceService.GetBreakEvenAsync | Abnormal | Period 2026-11 có nhưng chưa tính | periodMonth="2026-11" | Failure `BreakEvenNotFound` |
| UTCID654 | FinanceService.GetBreakEvenAsync | Abnormal | Kết quả có `r.Product` = null (orphan) | periodMonth="2026-10" | Bỏ qua/Failure an toàn. ⚠ `NullReferenceException` trong ToDto |
| UTCID655 | FinanceService.ExportBreakEvenAsync | Normal | Như case Get Normal; mock `WriteBreakEvenSheet` | periodMonth="2026-10", history=false | Success; FileName="HoaVon_2026-10.xlsx"; writer nhận 4 dòng |
| UTCID656 | FinanceService.ExportBreakEvenAsync | Boundary | 2 lần tính | history=true | Writer nhận 8 dòng; tên file vẫn "HoaVon_2026-10.xlsx" |
| UTCID657 | FinanceService.ExportBreakEvenAsync | Abnormal | Period 2026-11 chưa tính | periodMonth="2026-11" | Failure `BreakEvenNotFound` (propagate từ Get); writer không gọi |
| UTCID658 | FinanceService.ExportBreakEvenAsync | Abnormal | — | periodMonth="abc" | Failure `BreakEvenPeriodMonthInvalid` |
| UTCID659 | FinanceService.ExportBreakEvenAsync | Abnormal | Mock writer ném exception | periodMonth="2026-10" | Exception propagate |

### Sheet `OrderPaymentService` (P0-2) — UTCID660–UTCID749 (90 case)

| Test Case ID | Function Name | Type (Normal/Boundary/Abnormal) | Pre-conditions | Input parameters | Expected Results (Return / Exception / Log Message) |
|---|---|---|---|---|---|
| UTCID660 | OrderPaymentService.IsEditable | Normal | Now = 2026-11-24 10:00 | CreatedAt = 2026-11-24 08:00 (2 giờ trước) | true |
| UTCID661 | OrderPaymentService.IsEditable | Boundary | Now = 2026-11-24 10:00 | CreatedAt = 2026-11-23 10:00:00 (đúng 24 giờ) | true (`<=`) |
| UTCID662 | OrderPaymentService.IsEditable | Boundary | Now = 2026-11-24 10:00 | CreatedAt = 2026-11-23 09:59:59 (24 giờ 1 giây) | false |
| UTCID663 | OrderPaymentService.IsEditable | Abnormal | Now = 2026-11-24 10:00 | CreatedAt = 2026-11-24 11:00 (tương lai, lệch đồng hồ) | true (now − CreatedAt âm ≤ 24h). Ghi nhận |
| UTCID664 | OrderPaymentService.InRange | Normal | — | paid=50, final=100 | true |
| UTCID665 | OrderPaymentService.InRange | Boundary | — | paid=0, final=100 | true |
| UTCID666 | OrderPaymentService.InRange | Boundary | — | paid=100, final=100 | true |
| UTCID667 | OrderPaymentService.InRange | Boundary | — | paid=101, final=100 | false |
| UTCID668 | OrderPaymentService.InRange | Boundary | — | paid=-1, final=100 | false |
| UTCID669 | OrderPaymentService.InRange | Boundary | — | paid=-100, final=-100 | true (khoảng [−100, 0]) |
| UTCID670 | OrderPaymentService.InRange | Boundary | — | paid=-101, final=-100 | false |
| UTCID671 | OrderPaymentService.InRange | Boundary | — | paid=0, final=0 | true |
| UTCID672 | OrderPaymentService.InRange | Abnormal | — | paid=10, final=-100 | false |
| UTCID673 | OrderPaymentService.InRange | Abnormal | — | paid=1, final=0 | false |
| UTCID674 | OrderPaymentService.Remaining | Normal | — | final=100.000.000, paid=30.000.000 | 70.000.000 |
| UTCID675 | OrderPaymentService.Remaining | Boundary | — | final=0, paid=0 | 0 |
| UTCID676 | OrderPaymentService.Remaining | Abnormal | — | final=null, paid=0 | null (chưa chốt) |
| UTCID677 | OrderPaymentService.StateOf | Normal | — | final=100, paid=30 | Partial |
| UTCID678 | OrderPaymentService.StateOf | Normal | — | final=100, paid=0 | Unpaid |
| UTCID679 | OrderPaymentService.StateOf | Normal | — | final=100, paid=100 | Settled |
| UTCID680 | OrderPaymentService.StateOf | Boundary | — | final=0, paid=0 | Settled (nhánh paid==final được xét trước paid==0) |
| UTCID681 | OrderPaymentService.StateOf | Abnormal | — | final=null, paid=0 | NotSet |
| UTCID682 | OrderPaymentService.ListAsync | Normal | O1 Final=null; O2 Final=100tr Paid=100tr; O3 Final=100tr Paid=0; O4 Final=100tr Paid=40tr; O5 Final=0 Paid=0. Mỗi đơn có OrderLines | query {State=null, Page=1} | Success; 5 item; mỗi item có Amount = Σ OrderedQty × CmtUnitPrice, Remaining, State đúng theo StateOf |
| UTCID683 | OrderPaymentService.ListAsync | Normal | Như trên | State="NotSet" | Chỉ O1 |
| UTCID684 | OrderPaymentService.ListAsync | Normal | Như trên | State="Settled" | O2 và O5 (Final=0 được tính là Settled) |
| UTCID685 | OrderPaymentService.ListAsync | Normal | Như trên | State="Unpaid" | Chỉ O3 (O5 bị loại vì Final=0) |
| UTCID686 | OrderPaymentService.ListAsync | Normal | Như trên | State="Partial" | Chỉ O4 |
| UTCID687 | OrderPaymentService.ListAsync | Normal | O2 mã "DH2611-002", khách "May Việt" | Keyword="May Việt" | Trả O2 |
| UTCID688 | OrderPaymentService.ListAsync | Boundary | O6: dòng 1 = 1.000 × 12.000đ, dòng 2 = 500 × 20.000đ | State=null | O6.Amount = 22.000.000 |
| UTCID689 | OrderPaymentService.ListAsync | Boundary | O7 không có OrderLine | State=null | O7.Amount = 0 |
| UTCID690 | OrderPaymentService.ListAsync | Boundary | DB rỗng | query mặc định | Success; Items rỗng |
| UTCID691 | OrderPaymentService.ListAsync | Boundary | 5 đơn | Page=3, PageSize=2 | Items=1; Total=5 |
| UTCID692 | OrderPaymentService.ListAsync | Boundary | 5 đơn | Keyword="" | Không lọc keyword |
| UTCID693 | OrderPaymentService.ListAsync | Abnormal | 5 đơn | State="settled" (sai casing) | Nhận không phân biệt hoa thường, hoặc Failure Validation. ⚠ Hiện trả list rỗng |
| UTCID694 | OrderPaymentService.ListAsync | Abnormal | 5 đơn | State="Foo" | Success list rỗng (đúng design hiện tại). Đề xuất trả Validation (xác nhận spec) |
| UTCID695 | OrderPaymentService.ListAsync | Abnormal | Có mã đơn chứa ký tự "%" | Keyword="%" | Tìm theo ký tự literal, không trả toàn bộ |
| UTCID696 | OrderPaymentService.GetAsync | Normal | O4: 2 dòng đơn, Customer "May Việt", 2 payment (20tr lúc Now−2h, 20tr lúc Now−30h), audit table "OrderPayment" có 3 bản ghi | orderId=O4 | Success; Paid=40tr, Remaining=60tr; Payment 1 CanEdit=true, Payment 2 CanEdit=false; History=3; có RowVer; OrderAmount = Σ dòng đơn |
| UTCID697 | OrderPaymentService.GetAsync | Boundary | Payment CreatedAt = Now − 24h đúng | orderId=O4 | CanEdit=true |
| UTCID698 | OrderPaymentService.GetAsync | Boundary | Payment CreatedAt = Now − 24h − 1s | orderId=O4 | CanEdit=false |
| UTCID699 | OrderPaymentService.GetAsync | Boundary | O3 chưa có payment | orderId=O3 | Paid=0; Payments rỗng; State=Unpaid |
| UTCID700 | OrderPaymentService.GetAsync | Boundary | O1 Final=null | orderId=O1 | Remaining=null; State=NotSet |
| UTCID701 | OrderPaymentService.GetAsync | Abnormal | — | orderId=999 | Failure `NotFound` |
| UTCID702 | OrderPaymentService.GetAsync | Abnormal | O8 có CustomerId lệch, Customer=null | orderId=O8 | CustomerName=null an toàn. ⚠ `NullReferenceException` |
| UTCID703 | OrderPaymentService.GetAsync | Abnormal | O6 có dòng đơn | gọi ListAsync và GetAsync cho O6 | Amount ở List và OrderAmount ở Get bằng nhau (rủi ro: 2 biểu thức khác nhau) |
| UTCID704 | OrderPaymentService.SetFinalAmountAsync | Normal | O1 Final=null, Paid=0, RowVer=v1; UoW spy | orderId=O1, dto {Amount=100.000.000, Reason="Chốt theo biên bản", RowVer=v1} | Success; FinalAmount=100tr, Reason lưu; RowVer touched; Audit + SaveChanges trong TX; Commit |
| UTCID705 | OrderPaymentService.SetFinalAmountAsync | Normal | O4 Final=100tr, Paid=40tr | Amount=50.000.000, Reason="Điều chỉnh" | Success; State=Partial, Remaining=10tr |
| UTCID706 | OrderPaymentService.SetFinalAmountAsync | Boundary | O4 Paid=40tr | Amount=40.000.000 | Success; State=Settled |
| UTCID707 | OrderPaymentService.SetFinalAmountAsync | Boundary | O4 Paid=40tr | Amount=39.999.999 | Failure `FinalAmountBelowPaid`; không SaveChanges |
| UTCID708 | OrderPaymentService.SetFinalAmountAsync | Boundary | O1 Paid=0 | Amount=0 | Success; State=Settled. Sau đó mọi AddPayment khác 0 đều Failure (out of range) |
| UTCID709 | OrderPaymentService.SetFinalAmountAsync | Boundary | O1 Paid=0 | Amount=-5.000.000 | Success (cho phép âm) |
| UTCID710 | OrderPaymentService.SetFinalAmountAsync | Boundary | O4 Paid=40tr | Amount=-5.000.000 | Failure `FinalAmountBelowPaid`. Ghi nhận: message "dưới số đã trả" không đúng ngữ cảnh final âm |
| UTCID711 | OrderPaymentService.SetFinalAmountAsync | Boundary | O1 | Reason="" | Failure (Validation: Reason bắt buộc) |
| UTCID712 | OrderPaymentService.SetFinalAmountAsync | Boundary | O1 | Reason="   " | Failure (Validation: Reason blank) |
| UTCID713 | OrderPaymentService.SetFinalAmountAsync | Boundary | O1; MaxLength Reason=500 | Reason = 500 ký tự | Success |
| UTCID714 | OrderPaymentService.SetFinalAmountAsync | Boundary | O1 có Σ CMT = 100tr | Amount=200.000.000 | Success (không ràng buộc với Σ CMT). Xác nhận spec |
| UTCID715 | OrderPaymentService.SetFinalAmountAsync | Abnormal | — | orderId=999 | Failure `NotFound` |
| UTCID716 | OrderPaymentService.SetFinalAmountAsync | Abnormal | O1 | Reason=null | Failure (Validation) |
| UTCID717 | OrderPaymentService.SetFinalAmountAsync | Abnormal | O1 RowVer hiện tại = v2 | RowVer=v1 | Failure Conflict (409, `DbUpdateConcurrencyException`); Rollback |
| UTCID718 | OrderPaymentService.SetFinalAmountAsync | Abnormal | — | dto=null | `ArgumentNullException` hoặc Failure (Validation) |
| UTCID719 | OrderPaymentService.AddPaymentAsync | Normal | O3 Final=100tr, Paid=0, RowVer=v1 | dto {PaymentDate=2026-11-24, Amount=30.000.000, Method="BANK_TRANSFER", RowVer=v1} | Success; payment mới CreatedAt=Now; Paid=30tr; Audit action Confirm |
| UTCID720 | OrderPaymentService.AddPaymentAsync | Normal | O3 | Method="CASH", Amount=10.000.000 | Success |
| UTCID721 | OrderPaymentService.AddPaymentAsync | Boundary | O4 Final=100tr, Paid=40tr | Amount=60.000.000 (Paid+Amount = Final) | Success; State=Settled |
| UTCID722 | OrderPaymentService.AddPaymentAsync | Boundary | O4 Paid=40tr | Amount=60.000.001 | Failure (Validation: vượt Final) |
| UTCID723 | OrderPaymentService.AddPaymentAsync | Boundary | O3 | Amount=0 | Failure (Validation: Amount ≠ 0) |
| UTCID724 | OrderPaymentService.AddPaymentAsync | Boundary | O4 Paid=40tr | Amount=-10.000.000 (hoàn tiền, Paid còn 30tr) | Success |
| UTCID725 | OrderPaymentService.AddPaymentAsync | Boundary | O4 Paid=40tr | Amount=-40.000.001 (Paid âm) | Failure (Validation: out of range) |
| UTCID726 | OrderPaymentService.AddPaymentAsync | Boundary | O9 Final=-5tr, Paid=0 | Amount=-5.000.000 | Success; State=Settled |
| UTCID727 | OrderPaymentService.AddPaymentAsync | Boundary | O9 Final=-5tr | Amount=1.000.000 | Failure (Validation: out of range) |
| UTCID728 | OrderPaymentService.AddPaymentAsync | Boundary | O3 | PaymentDate=2026-11-24 (= Today) | Success |
| UTCID729 | OrderPaymentService.AddPaymentAsync | Boundary | O3 | PaymentDate=2026-11-25 (Today+1) | Failure (Validation: ngày tương lai) |
| UTCID730 | OrderPaymentService.AddPaymentAsync | Abnormal | O3 | Method="MOMO" | Failure (Validation: Method ∉ {BANK_TRANSFER, CASH}) |
| UTCID731 | OrderPaymentService.AddPaymentAsync | Abnormal | O3 | Method=null | Failure (Validation) |
| UTCID732 | OrderPaymentService.AddPaymentAsync | Abnormal | O1 Final=null | Amount=10.000.000 | Failure `NeedFinalAmount` (Kind=Conflict, khác Validation) |
| UTCID733 | OrderPaymentService.AddPaymentAsync | Abnormal | — | orderId=999 | Failure `NotFound` |
| UTCID734 | OrderPaymentService.AddPaymentAsync | Abnormal | O3 đã Cancelled, Final=100tr | Amount=10.000.000 | Success (không khóa theo status đơn). Xác nhận spec |
| UTCID735 | OrderPaymentService.AddPaymentAsync | Abnormal | O3 RowVer=v2 | RowVer=v1 | Failure Conflict (409) |
| UTCID736 | OrderPaymentService.UpdatePaymentAsync | Normal | O4 Final=100tr, payment P1=20tr (CreatedAt=Now−2h), P2=20tr | orderId=O4, paymentId=P1, dto {Amount=25.000.000, Method="CASH", PaymentDate=2026-11-24, Reason="Sửa nhập sai", RowVer=v1} | Success; P1.Amount=25tr, EditReason lưu; RowVer của Order touched; Audit |
| UTCID737 | OrderPaymentService.UpdatePaymentAsync | Boundary | P1 CreatedAt = Now − 24h đúng | dto hợp lệ | Success |
| UTCID738 | OrderPaymentService.UpdatePaymentAsync | Boundary | P1 CreatedAt = Now − 24h − 1s | dto hợp lệ | Failure `EditExpired` |
| UTCID739 | OrderPaymentService.UpdatePaymentAsync | Boundary | Final=100tr, others (P2)=20tr | Amount=80.000.000 (others+new = Final) | Success |
| UTCID740 | OrderPaymentService.UpdatePaymentAsync | Boundary | Như trên | Amount=80.000.001 | Failure (Validation: out of range) |
| UTCID741 | OrderPaymentService.UpdatePaymentAsync | Boundary | O4 | Amount=0 | Failure (Validation). Ghi nhận: không có cách xóa payment để về Paid=0 |
| UTCID742 | OrderPaymentService.UpdatePaymentAsync | Boundary | O4 | Reason="" | Failure (Validation: Reason bắt buộc) |
| UTCID743 | OrderPaymentService.UpdatePaymentAsync | Boundary | O4 | PaymentDate=2026-11-25 | Failure (Validation: ngày tương lai) |
| UTCID744 | OrderPaymentService.UpdatePaymentAsync | Abnormal | — | orderId=999, paymentId=P1 | Failure `NotFound` |
| UTCID745 | OrderPaymentService.UpdatePaymentAsync | Abnormal | O4 | paymentId=999 | Failure `NotFound` |
| UTCID746 | OrderPaymentService.UpdatePaymentAsync | Abnormal | Payment P5 thuộc O3 | orderId=O4, paymentId=P5 | Failure `NotFound` |
| UTCID747 | OrderPaymentService.UpdatePaymentAsync | Abnormal | O10 Final=null nhưng có payment cũ (data lệch) | orderId=O10 | Failure `NotFound` (theo code; khác AddPayment trả Conflict). Ghi nhận không nhất quán |
| UTCID748 | OrderPaymentService.UpdatePaymentAsync | Abnormal | O4 RowVer=v2 | RowVer=v1 | Failure Conflict (409) |
| UTCID749 | OrderPaymentService.UpdatePaymentAsync | Abnormal | O4 | Method="ATM" | Failure (Validation) |

### Sheet `SpecImportBuilder` (P0-3) — UTCID750–UTCID786 (37 case)

Kiểm thử qua constructor `(spec, materials, orderProduct?, productCode, workbook, replace, usage?)` và kết quả Build (thuần tính, không I/O).

| Test Case ID | Function Name | Type (Normal/Boundary/Abnormal) | Pre-conditions | Input parameters | Expected Results (Return / Exception / Log Message) |
|---|---|---|---|---|---|
| UTCID750 | SpecImportBuilder.Build | Normal | spec rỗng; materials có M1, M2 | workbook: 2 component, 2 norm, 1 group PANEL; replace=false | Mọi dòng Action=New; ErrorCount=0; SpecChanged=true |
| UTCID751 | SpecImportBuilder.Build | Normal | spec hiện có trùng khớp toàn bộ workbook | replace=false | Mọi dòng Action=Unchanged; SpecChanged=false |
| UTCID752 | SpecImportBuilder.Build | Normal | spec có component "Thân trước" Pieces=2 | workbook "Thân trước" Pieces=1 | Dòng Action=Update |
| UTCID753 | SpecImportBuilder.Build | Normal | spec có component "Túi" chưa dùng; workbook không có "Túi" | replace=true, usage rỗng | "Túi" nằm trong danh sách Removed |
| UTCID754 | SpecImportBuilder.Build | Boundary | — | component Name = 100 ký tự | Hợp lệ |
| UTCID755 | SpecImportBuilder.Build | Boundary | — | component Name = 101 ký tự | Action=Error (Name > 100) |
| UTCID756 | SpecImportBuilder.Build | Boundary | — | Pieces=1 và Pieces=99 | Hợp lệ |
| UTCID757 | SpecImportBuilder.Build | Boundary | — | Pieces=0 | Action=Error (Pieces ∉ [1,99]) |
| UTCID758 | SpecImportBuilder.Build | Boundary | — | Pieces=100 | Action=Error |
| UTCID759 | SpecImportBuilder.Build | Boundary | — | norm Note = 200 ký tự | Hợp lệ |
| UTCID760 | SpecImportBuilder.Build | Boundary | — | norm Note = 201 ký tự | Action=Error (Note > 200) |
| UTCID761 | SpecImportBuilder.Build | Boundary | MarkerNo max = 20 (tránh phình payload/UI) | group MarkerNo=20 | Hợp lệ |
| UTCID762 | SpecImportBuilder.Build | Boundary | MarkerNo max = 20 | group MarkerNo=21 | Action=Error |
| UTCID763 | SpecImportBuilder.Build | Boundary | — | workbook không có dòng dữ liệu ở mọi sheet | Không có dòng; ErrorCount=0; SpecChanged=false |
| UTCID764 | SpecImportBuilder.Build | Boundary | Màu "Vàng" color-only: key có Size=`"?"` | workbook có norm "Vàng" | Sau MergeSpec, entry Size=`"?"` bị strip; spec kết quả không có Variant size `"?"` |
| UTCID765 | SpecImportBuilder.Build | Boundary | Đơn có size thật tên `"?"` | workbook norm size `"?"` | Size thật vẫn được giữ. ⚠ Bị strip như color-only |
| UTCID766 | SpecImportBuilder.Build | Boundary | spec có cặp Đỏ\|Thân trước dùng M1 + M2 | workbook cặp Đỏ\|Thân trước chỉ có M1 | Kết quả cặp chỉ còn M1 (Merge thay cả bộ NVL theo cặp). Xác nhận spec |
| UTCID767 | SpecImportBuilder.Build | Boundary | Component "Túi" đã dùng (usage chứa "Túi") | replace=true, workbook không có "Túi" | "Túi" **không** bị Removed |
| UTCID768 | SpecImportBuilder.Build | Boundary | Cặp norm Đỏ\|Thân trước\|M1 đã dùng | replace=true, workbook không có cặp này | Cặp được giữ lại |
| UTCID769 | SpecImportBuilder.Build | Boundary | Group G1 đã dùng | replace=true, workbook không có G1 | G1 được giữ lại |
| UTCID770 | SpecImportBuilder.Build | Boundary | Order marker OM1 đã dùng | replace=true, workbook không có OM1 | OM1 cần được giữ như component/norm. ⚠ Order markers không có cơ chế usage-keep → bị xóa |
| UTCID771 | SpecImportBuilder.Build | Boundary | spec không đổi | workbook chỉ đổi OrderMarkers | SpecChanged (hoặc cờ riêng) phản ánh có thay đổi. ⚠ SpecChanged không gồm OrderMarkers |
| UTCID772 | SpecImportBuilder.Build | Boundary | orderProduct=null (mã chưa có đơn) | workbook có OrderMarkers | Không kiểm size đơn; dòng OrderMarker bị bỏ qua hoặc báo không có đơn, không exception |
| UTCID773 | SpecImportBuilder.Build | Abnormal | — | component Pieces="abc" | Action=Error (ParseError) |
| UTCID774 | SpecImportBuilder.Build | Abnormal | — | 2 dòng component "Tay" | Dòng trùng Action=Error; DuplicateCount=1 và ErrorCount phản ánh đúng |
| UTCID775 | SpecImportBuilder.Build | Abnormal | — | norm màu "Tím" không có ở spec/ProductColor | Action=Error (màu lạ) |
| UTCID776 | SpecImportBuilder.Build | Abnormal | — | norm component "Cầu vai" không có | Action=Error (component lạ) |
| UTCID777 | SpecImportBuilder.Build | Abnormal | — | norm có mã vật tư không có trong materials | Action=Error (material null) |
| UTCID778 | SpecImportBuilder.Build | Abnormal | — | 2 norm trùng Color\|Comp\|Mat | Action=Error (trùng) |
| UTCID779 | SpecImportBuilder.Build | Abnormal | — | group không có component PANEL | Action=Error |
| UTCID780 | SpecImportBuilder.Build | Abnormal | — | group có mọi màu unknown | Action=Error |
| UTCID781 | SpecImportBuilder.Build | Abnormal | — | group có màu Đỏ (biết) và "Tím" (lạ) | Không Error; dòng có note "skip màu Tím". Ghi nhận rủi ro bỏ qua im lặng |
| UTCID782 | SpecImportBuilder.Build | Abnormal | productCode="AO01" | OrderMarker ProductCode="AO02" | Dòng bị skip |
| UTCID783 | SpecImportBuilder.Build | Abnormal | — | OrderMarker Width=null | Action=Error |
| UTCID784 | SpecImportBuilder.Build | Abnormal | Đơn có size S, M | OrderMarker size XL | Action=Error (không có size trong đơn) |
| UTCID785 | SpecImportBuilder.Build | Abnormal | — | OrderMarker tham chiếu group "G9" không có | Action=Error (nhóm unknown) |
| UTCID786 | SpecImportBuilder.Build | Abnormal | — | workbook=null | `ArgumentNullException` |

### Sheet `SpecImportService` (P0-3) — UTCID787–UTCID819 (33 case)

| Test Case ID | Function Name | Type (Normal/Boundary/Abnormal) | Pre-conditions | Input parameters | Expected Results (Return / Exception / Log Message) |
|---|---|---|---|---|---|
| UTCID787 | SpecImportService.PreviewAsync(Stream) | Normal | Product 1 có đủ màu/vật tư; mock Excel parser trả workbook hợp lệ | productId=1, stream hợp lệ, replace=false | Success Preview; CanImport=true; Plan chạy với `createColors:false`; SaveChanges không gọi |
| UTCID788 | SpecImportService.PreviewAsync(Workbook) | Normal | Như trên | productId=1, workbook object | Kết quả giống overload Stream |
| UTCID789 | SpecImportService.PreviewAsync(Workbook) | Boundary | Workbook có sheet nhưng 0 dòng | productId=1, workbook rỗng | Success; Preview rỗng |
| UTCID790 | SpecImportService.PreviewAsync(Workbook) | Abnormal | — | productId=1, workbook=null | `ArgumentNullException` hoặc Failure |
| UTCID791 | SpecImportService.PreviewAsync(Stream) | Boundary | workbook có màu "Vàng" chưa có ở product | productId=1 | colorsToCreate=1; dòng "Vàng" đánh dấu sẽ tạo; CrossErrors bị bỏ qua → CanImport=true. Xác nhận spec (rủi ro preview lạc quan) |
| UTCID792 | SpecImportService.PreviewAsync(Stream) | Boundary | workbook có sheet nhưng 0 dòng | productId=1 | Success; Preview rỗng; không exception |
| UTCID793 | SpecImportService.PreviewAsync(Stream) | Boundary | Sheet tên "components" (thường) thay vì "Components" | productId=1 | Nhận diện sheet không phân biệt hoa thường. ⚠ Parts case-sensitive → bỏ sheet |
| UTCID794 | SpecImportService.PreviewAsync(Stream) | Abnormal | colorsToCreate=0; norm và marker lệch nhau (CrossErrors) | productId=1 | Preview có CrossErrors; CanImport=false |
| UTCID795 | SpecImportService.PreviewAsync(Stream) | Abnormal | Parser ném exception | stream rác | Failure (Excel parse); exception được catch |
| UTCID796 | SpecImportService.PreviewAsync(Stream) | Abnormal | — | productId=999 | Failure `NotFound` |
| UTCID797 | SpecImportService.PreviewAsync(Stream) | Abnormal | — | stream=null | `ArgumentNullException` hoặc Failure |
| UTCID798 | SpecImportService.ImportAsync(Stream) | Normal | Product 1 đủ màu/vật tư; file có OrderMarkers; mock `IProductSpecService.WriteAsync`, `IOrderMarkerService.Check/Write` Success | productId=1, stream hợp lệ | Success; trong 1 TX: specs.Write gọi 1 lần, markers.Check rồi Write; Commit |
| UTCID799 | SpecImportService.ImportAsync(Stream) | Normal | File có màu "Vàng" chưa có | productId=1 | EnsureColors tạo ProductColor "Vàng" (không tạo Variant size); norm "Vàng" ghi với size `"?"` đã strip; Commit |
| UTCID800 | SpecImportService.ImportAsync(Workbook) | Normal | Như case Normal đầu | productId=1, workbook object | Kết quả giống overload Stream |
| UTCID801 | SpecImportService.ImportAsync(Workbook) | Boundary | Workbook 0 dòng dữ liệu | productId=1, workbook rỗng | Success không thay đổi gì, hoặc Failure (không có dữ liệu) theo spec; không ghi DB nửa vời |
| UTCID802 | SpecImportService.ImportAsync(Workbook) | Abnormal | — | productId=1, workbook=null | `ArgumentNullException` hoặc Failure; BeginTransaction không gọi |
| UTCID803 | SpecImportService.ImportAsync(Stream) | Boundary | File không có sheet OrderMarkers | productId=1 | Success; `IOrderMarkerService` không được gọi |
| UTCID804 | SpecImportService.ImportAsync(Stream) | Abnormal | File có màu mới "Vàng" và 1 dòng component Error → CanImport=false | productId=1 | Failure; Rollback; ProductColor "Vàng" (tạo bởi EnsureColors) **không** còn trong DB |
| UTCID805 | SpecImportService.ImportAsync(Stream) | Abnormal | Norm dùng mã vật tư chưa có | productId=1 | Failure (dòng Error material unknown; khác CheckAsync báo NewMaterial) |
| UTCID806 | SpecImportService.ImportAsync(Stream) | Abnormal | Mock markers.Check trả Failure | productId=1 | Failure; Rollback; spec không được ghi |
| UTCID807 | SpecImportService.ImportAsync(Stream) | Abnormal | Mock specs.WriteAsync ném exception | productId=1 | Rollback; exception/Failure propagate |
| UTCID808 | SpecImportService.ImportAsync(Stream) | Abnormal | Parser ném exception | stream rác | Failure (parse); BeginTransaction không gọi hoặc Rollback |
| UTCID809 | SpecImportService.ImportAsync(Stream) | Abnormal | — | productId=999 | Failure `NotFound` |
| UTCID810 | SpecImportService.CheckAsync | Normal | Product 1 đủ màu/vật tư | productId=1, workbook hợp lệ | Success; không dòng Pending/NewMaterial; CanImport=true |
| UTCID811 | SpecImportService.CheckAsync | Normal | Mã hàng mới chưa tạo | productId=null, workbook hợp lệ | Success; màu được AssumeColors, dòng liên quan Action=Pending |
| UTCID812 | SpecImportService.CheckAsync | Boundary | Product 1 thiếu màu "Vàng" | workbook norm "Vàng" | Dòng "Vàng" Action=Pending |
| UTCID813 | SpecImportService.CheckAsync | Boundary | Vật tư "M99" chưa có | workbook norm M99 | AssumeMaterials gán Id âm; dòng Action=NewMaterial |
| UTCID814 | SpecImportService.CheckAsync | Boundary | Marker tham chiếu màu "Vàng" giả định | workbook có OrderMarker "Vàng" | Dòng marker Action=Pending |
| UTCID815 | SpecImportService.CheckAsync | Boundary | Có dòng Pending/NewMaterial | productId=1 | CanImport phản ánh đúng sau MarkAssumed. ⚠ MarkAssumed không cập nhật CanImport |
| UTCID816 | SpecImportService.CheckAsync | Boundary | Mã hàng chưa có đơn | productId=null, workbook có OrderMarkers | HasOrderMarkers chỉ true khi có đơn tương ứng. ⚠ Luôn set true |
| UTCID817 | SpecImportService.CheckAsync | Abnormal | Còn màu/NVL giả định; norm và marker lệch nhau | productId=1 | CrossErrors **không** được tính (chỉ tính khi đủ màu + NVL thật) |
| UTCID818 | SpecImportService.CheckAsync | Abnormal | — | productId=999 | Failure `NotFound`. ⚠ `productId ?? 0` coi như mã mới |
| UTCID819 | SpecImportService.CheckAsync | Abnormal | — | workbook=null | `ArgumentNullException` hoặc Failure |

### Sheet `EmployeeService` (P0-4) — UTCID820–UTCID859 (40 case)

| Test Case ID | Function Name | Type (Normal/Boundary/Abnormal) | Pre-conditions | Input parameters | Expected Results (Return / Exception / Log Message) |
|---|---|---|---|---|---|
| UTCID820 | EmployeeService.ListAsync | Normal | 10 NV: 6 Worker, 2 Accountant, 2 Manager; 1 Worker Resigned | query {Role="Worker", Status="Active"} | Success; 5 Worker Active |
| UTCID821 | EmployeeService.ListAsync | Boundary | Như trên | query {Role=null, Status=null} | Trả toàn bộ NV mà người gọi CanSee |
| UTCID822 | EmployeeService.ListAsync | Boundary | Không có NV role Electrician | Role="Electrician" | Success; list rỗng |
| UTCID823 | EmployeeService.ListAsync | Abnormal | — | Role="Foo" | Failure (Validation) hoặc list rỗng; không exception |
| UTCID824 | EmployeeService.GetAsync | Normal | NV E1 có 3 bản ghi audit; người gọi Admin | employeeId=E1 | Success DTO + History=3 |
| UTCID825 | EmployeeService.GetAsync | Boundary | NV E2 mới tạo, chưa có audit | employeeId=E2 | Success; History rỗng |
| UTCID826 | EmployeeService.GetAsync | Abnormal | — | employeeId=999 | Failure `NotFound` |
| UTCID827 | EmployeeService.GetAsync | Abnormal | Người gọi Manager; E9 là Admin (ngoài phạm vi xem) | employeeId=E9 | Failure `NotFound` (!CanSee, không lộ sự tồn tại) |
| UTCID828 | EmployeeService.CreateAsync | Normal | Role Accountant là office creatable; UoW spy | dto {FullName="Nguyễn Văn A", Role="Accountant", TeamId=Office1, Email hợp lệ} | Success; tạo Employee + Account trong 1 TX; trả temp password **1 lần**; SettleOnTeamLeaveAsync không gọi |
| UTCID829 | EmployeeService.CreateAsync | Boundary | MaxLength FullName=100 | FullName = 100 ký tự | Success |
| UTCID830 | EmployeeService.CreateAsync | Boundary | — | FullName = 101 ký tự | Failure (Validation) |
| UTCID831 | EmployeeService.CreateAsync | Boundary | — | FullName="" | Failure (Validation) |
| UTCID832 | EmployeeService.CreateAsync | Abnormal | Worker không phải office creatable | Role="Worker" | Failure (Validation: role không tạo được ở đây) |
| UTCID833 | EmployeeService.CreateAsync | Abnormal | Team S3 là tổ may | Role="Accountant", TeamId=S3 | Failure (CheckPlacement) |
| UTCID834 | EmployeeService.CreateAsync | Abnormal | Email a@x.vn đã tồn tại | Email="a@x.vn" | Failure (Duplicate) |
| UTCID835 | EmployeeService.CreateAsync | Abnormal | Mock tạo Account ném exception | dto hợp lệ | Rollback; không có Employee mồ côi |
| UTCID836 | EmployeeService.CreateAsync | Abnormal | — | dto=null | `ArgumentNullException` hoặc Failure |
| UTCID837 | EmployeeService.ImportFromRjAsync | Normal | Dữ liệu RJ: 2 Worker hợp lệ, tổ may S3 | danh sách 2 NV RJ | Success; tạo 2 Worker gắn S3 kèm Account; không settle |
| UTCID838 | EmployeeService.ImportFromRjAsync | Boundary | — | danh sách rỗng | Success 0 bản ghi (hoặc Validation); không mở TX |
| UTCID839 | EmployeeService.ImportFromRjAsync | Abnormal | Mã RJ "RJ001" đã import | danh sách chứa RJ001 | Failure (Duplicate) cho dòng đó |
| UTCID840 | EmployeeService.ImportFromRjAsync | Abnormal | Team chỉ định là Office | Worker → Office1 | Failure (CheckPlacement) |
| UTCID841 | EmployeeService.AdminUpdateAsync | Normal | Worker E1 Active ở tổ may A; tổ may B; mock `IDailyWorkAllocationService.SettleOnTeamLeaveAsync` Success (callback ghi lại E1.TeamId tại thời điểm gọi) | employeeId=E1, dto {TeamId=B, …} | Success; Settle(A, [E1]) gọi **1 lần, trước khi gán TeamId** (callback thấy TeamId=A); cùng TX; TeamId=B; Audit; Commit |
| UTCID842 | EmployeeService.AdminUpdateAsync | Normal | E1 ở tổ A | dto {TeamId=A, đổi Phone} | Success; Settle **không** gọi |
| UTCID843 | EmployeeService.AdminUpdateAsync | Boundary | E2 TeamId=null | dto {TeamId=B} | Success; Settle không gọi (oldTeamId null) |
| UTCID844 | EmployeeService.AdminUpdateAsync | Boundary | E1 ở tổ A | dto {TeamId=null} | Success; Settle(A,[E1]) gọi; TeamId=null |
| UTCID845 | EmployeeService.AdminUpdateAsync | Boundary | E3 Accountant ở tổ văn phòng Office1 → Office2 | dto {TeamId=Office2} | Settle(Office1,[E3]) được gọi (Employee không phân biệt loại tổ; Settle tự no-op); Success |
| UTCID846 | EmployeeService.AdminUpdateAsync | Abnormal | E1 ở tổ A; Settle trả Failure `TeamOrderBusy` | dto {TeamId=B} | Failure `TeamOrderBusy`; Rollback; TeamId vẫn = A; không Audit |
| UTCID847 | EmployeeService.AdminUpdateAsync | Abnormal | E5 Resigned | dto bất kỳ | Failure (chỉ sửa NV Active) |
| UTCID848 | EmployeeService.AdminUpdateAsync | Abnormal | — | employeeId=999 | Failure `NotFound` |
| UTCID849 | EmployeeService.AdminUpdateAsync | Abnormal | — | Worker → TeamId=Office1 | Failure (CheckPlacement) |
| UTCID850 | EmployeeService.AdminUpdateAsync | Abnormal | E4 là Tổ trưởng tổ A | dto {TeamId=B} | Failure (không chuyển tổ trưởng, nhất quán với TransferIn). ⚠ Hiện cho phép (rủi ro #4) |
| UTCID851 | EmployeeService.ManagerUpdateAsync | Normal | Manager phụ trách xưởng có tổ A, B; E1 ở A | dto {TeamId=B} | Success; Settle(A,[E1]) trước gán TeamId; cùng TX |
| UTCID852 | EmployeeService.ManagerUpdateAsync | Boundary | E1 ở A | dto không đổi TeamId | Success; Settle không gọi |
| UTCID853 | EmployeeService.ManagerUpdateAsync | Abnormal | E8 thuộc xưởng ngoài phạm vi Manager | employeeId=E8 | Failure (scope: Forbidden/NotFound) |
| UTCID854 | EmployeeService.ManagerUpdateAsync | Abnormal | Settle trả `TeamOrderBusy` | dto {TeamId=B} | Failure `TeamOrderBusy`; Rollback; TeamId không đổi |
| UTCID855 | EmployeeService.ManagerUpdateAsync | Abnormal | E5 Resigned | dto bất kỳ | Failure (chỉ sửa NV Active) |
| UTCID856 | EmployeeService.ResignAsync | Normal | E6 Accountant Active, không có tổ | employeeId=E6 | Success; Status=Resigned; RotateStamp (phiên đăng nhập cũ vô hiệu) |
| UTCID857 | EmployeeService.ResignAsync | Boundary | E1 Worker Active ở tổ may A, còn handover PENDING | employeeId=E1 | Settle(A,[E1]) được gọi và TeamId=null. ⚠ Hiện không settle, vẫn giữ TeamId (rủi ro #3, gap nghiêm trọng) |
| UTCID858 | EmployeeService.ResignAsync | Abnormal | E5 đã Resigned | employeeId=E5 | Failure `InvalidStatus` |
| UTCID859 | EmployeeService.ResignAsync | Abnormal | — | employeeId=999 | Failure `NotFound` |

### Sheet `TeamService` (P0-4) — UTCID860–UTCID919 (60 case)

| Test Case ID | Function Name | Type (Normal/Boundary/Abnormal) | Pre-conditions | Input parameters | Expected Results (Return / Exception / Log Message) |
|---|---|---|---|---|---|
| UTCID860 | TeamService.ListAsync | Normal | 4 tổ: 2 Sewing, 1 Cutting, 1 Office (1 tổ Discontinued) | query {Type="Sewing"} | Success; 2 tổ may |
| UTCID861 | TeamService.ListAsync | Boundary | DB rỗng | query mặc định | Success; list rỗng |
| UTCID862 | TeamService.ListAsync | Abnormal | — | query {Type="Foo"} | Failure (Validation) hoặc list rỗng; không exception |
| UTCID863 | TeamService.GetAsync | Normal | Tổ A có 5 thành viên, 1 tổ trưởng | teamId=A | Success DTO + danh sách thành viên |
| UTCID864 | TeamService.GetAsync | Boundary | Tổ C chưa có thành viên | teamId=C | Success; Members rỗng; Leader=null |
| UTCID865 | TeamService.GetAsync | Abnormal | — | teamId=999 | Failure `NotFound` |
| UTCID866 | TeamService.CreateAsync | Normal | — | dto {Name="Tổ may 5", Type="Sewing"} | Success; tổ Active; Settle không gọi |
| UTCID867 | TeamService.CreateAsync | Boundary | MaxLength Name=100 | Name = 100 ký tự | Success; Name 101 ký tự → Failure (Validation) |
| UTCID868 | TeamService.CreateAsync | Abnormal | Đã có "Tổ may 5" | Name="Tổ may 5" | Failure (Duplicate) |
| UTCID869 | TeamService.CreateAsync | Abnormal | — | Type="Foo" | Failure (Validation) |
| UTCID870 | TeamService.UpdateAsync | Normal | Tổ A Sewing | dto {Name="Tổ may A1"} | Success; Settle không gọi |
| UTCID871 | TeamService.UpdateAsync | Boundary | Tổ C có 0 thành viên Active | dto {Type="Cutting"} | Success (đổi loại khi 0 thành viên) |
| UTCID872 | TeamService.UpdateAsync | Abnormal | Tổ A có 5 thành viên Active | dto {Type="Office"} | Failure (còn thành viên Active) |
| UTCID873 | TeamService.UpdateAsync | Abnormal | — | teamId=999 | Failure `NotFound` |
| UTCID874 | TeamService.DiscontinueAsync | Normal | Tổ C 0 thành viên Active | teamId=C | Success; Status=Discontinued |
| UTCID875 | TeamService.DiscontinueAsync | Boundary | Tổ D có 1 thành viên Resigned, 0 Active | teamId=D | Success (chỉ tính Active) |
| UTCID876 | TeamService.DiscontinueAsync | Abnormal | Tổ A còn 5 thành viên Active | teamId=A | Failure (còn thành viên) |
| UTCID877 | TeamService.DiscontinueAsync | Abnormal | Tổ C đã Discontinued | teamId=C | Failure `InvalidStatus` (hoặc idempotent theo spec) |
| UTCID878 | TeamService.DiscontinueAsync | Abnormal | — | teamId=999 | Failure `NotFound` |
| UTCID879 | TeamService.ReactivateAsync | Normal | Tổ C Discontinued | teamId=C | Success; Status=Active |
| UTCID880 | TeamService.ReactivateAsync | Boundary | Tổ C Discontinued, 0 thành viên | teamId=C | Success; Members vẫn rỗng; Settle không gọi |
| UTCID881 | TeamService.ReactivateAsync | Abnormal | Tổ A Active | teamId=A | Failure `InvalidStatus` (hoặc idempotent theo spec) |
| UTCID882 | TeamService.ReactivateAsync | Abnormal | — | teamId=999 | Failure `NotFound` |
| UTCID883 | TeamService.TransferInAsync | Normal | Tổ đích B (Sewing). E1, E2 thuộc tổ may A; E3 thuộc tổ may C; mock Settle Success | targetTeamId=B, dto {EmployeeIds=[E1,E2,E3], EffectiveDate=2026-11-24} | Success; trong 1 TX: Settle(A,[E1,E2]) 1 lần, Settle(C,[E3]) 1 lần; TeamId=B cho cả 3; stamp + Audit + LogTransfer; Message có suffix "đã quyết toán việc tổ may" |
| UTCID884 | TeamService.TransferInAsync | Normal | E4 thuộc tổ văn phòng Office1 | dto {EmployeeIds=[E4], EffectiveDate=Today} | Success; Settle(Office1,[E4]) gọi (no-op); Message **không** có suffix |
| UTCID885 | TeamService.TransferInAsync | Boundary | E1 ở tổ A | EffectiveDate=2026-11-24 (= Today) | Success |
| UTCID886 | TeamService.TransferInAsync | Boundary | E1 ở tổ A | EffectiveDate=2026-11-25 (Today+1) | Failure (Validation: ngày tương lai); không Settle |
| UTCID887 | TeamService.TransferInAsync | Boundary | E1 ở tổ A | EffectiveDate=2026-11-01 (quá khứ) | Success; ngày chỉ ghi vào audit. Xác nhận spec |
| UTCID888 | TeamService.TransferInAsync | Boundary | E5 TeamId=null | dto {EmployeeIds=[E5]} | Success; không Settle; TeamId=B |
| UTCID889 | TeamService.TransferInAsync | Boundary | — | EmployeeIds=[] | Failure (Validation) |
| UTCID890 | TeamService.TransferInAsync | Boundary | Tổ đích Office1 | Worker E1 → Office1 | Success (rủi ro: không chặn loại tổ đích). Xác nhận spec |
| UTCID891 | TeamService.TransferInAsync | Abnormal | E6 là Tổ trưởng tổ A | EmployeeIds=[E1,E6] | Failure (không chuyển Leader/Deputy); không ai bị đổi tổ |
| UTCID892 | TeamService.TransferInAsync | Abnormal | E7 là Tổ phó tổ C | EmployeeIds=[E7] | Failure (Deputy) |
| UTCID893 | TeamService.TransferInAsync | Abnormal | — | EmployeeIds=[E1,E1] | Failure (Validation: trùng) hoặc tự loại trùng. ⚠ Hiện trả NotFound |
| UTCID894 | TeamService.TransferInAsync | Abnormal | — | EmployeeIds=[999] | Failure `NotFound` |
| UTCID895 | TeamService.TransferInAsync | Abnormal | — | targetTeamId=999 | Failure `NotFound` |
| UTCID896 | TeamService.TransferInAsync | Abnormal | Tổ đích C Discontinued | targetTeamId=C | Failure `InvalidStatus` |
| UTCID897 | TeamService.TransferInAsync | Abnormal | Settle(A) Success, Settle(C) trả `TeamOrderBusy` | EmployeeIds=[E1,E3] | Failure `TeamOrderBusy`; Rollback toàn bộ; E1, E3 giữ TeamId cũ |
| UTCID898 | TeamService.RemoveMembersAsync | Normal | Tổ may A có E1, E2 (Worker); mock Settle Success | teamId=A, EmployeeIds=[E1,E2] | Success; trong TX: Settle(A,[E1,E2]) rồi TeamId=null; Message có suffix tổ may |
| UTCID899 | TeamService.RemoveMembersAsync | Normal | Tổ văn phòng Office1 có E4 | teamId=Office1, [E4] | Success; TeamId=null; Message không suffix |
| UTCID900 | TeamService.RemoveMembersAsync | Boundary | — | EmployeeIds=[] | Failure (Validation) |
| UTCID901 | TeamService.RemoveMembersAsync | Abnormal | E6 là Tổ trưởng tổ A | [E1,E6] | Failure (không xóa Leader); không ai bị xóa |
| UTCID902 | TeamService.RemoveMembersAsync | Abnormal | E3 thuộc tổ C | teamId=A, [E3] | Failure (NotFound/không thuộc tổ) |
| UTCID903 | TeamService.RemoveMembersAsync | Abnormal | Settle trả `TeamOrderBusy` | teamId=A, [E1] | Failure `TeamOrderBusy`; Rollback; TeamId=A giữ nguyên |
| UTCID904 | TeamService.RemoveMembersAsync | Abnormal | — | teamId=999 | Failure `NotFound` |
| UTCID905 | TeamService.AddMembersAsync | Normal | E5, E8 TeamId=null, Active | teamId=A, [E5,E8] | Success; TeamId=A; Settle không gọi |
| UTCID906 | TeamService.AddMembersAsync | Boundary | — | EmployeeIds=[] | Failure (Validation) |
| UTCID907 | TeamService.AddMembersAsync | Abnormal | E1 đang ở tổ A | teamId=B, [E1] | Failure (chỉ nhận NV chưa có tổ; phải dùng TransferIn) |
| UTCID908 | TeamService.AddMembersAsync | Abnormal | Tổ C Discontinued | teamId=C, [E5] | Failure `InvalidStatus` |
| UTCID909 | TeamService.AppointAsync | Normal | Tổ A: E6 đang là Leader; E1 là Worker trong tổ | teamId=A, employeeId=E1, Role=Leader | Success; trong TX: E6 hạ về Worker, E1 lên Leader |
| UTCID910 | TeamService.AppointAsync | Boundary | Tổ B chưa có Leader | employeeId=E2 (thành viên B), Role=Leader | Success; chỉ nâng E2 |
| UTCID911 | TeamService.AppointAsync | Abnormal | E3 không thuộc tổ A | teamId=A, employeeId=E3 | Failure (không phải thành viên) |
| UTCID912 | TeamService.AppointAsync | Abnormal | — | teamId=999 | Failure `NotFound` |
| UTCID913 | TeamService.DismissAsync | Normal | E6 là Leader tổ A | teamId=A, employeeId=E6 | Success; E6 về Worker, **vẫn ở tổ A**; Settle không gọi |
| UTCID914 | TeamService.DismissAsync | Boundary | E7 là Tổ phó tổ C | teamId=C, employeeId=E7 | Success; E7 về Worker, vẫn ở tổ C |
| UTCID915 | TeamService.DismissAsync | Abnormal | E1 là Worker | employeeId=E1 | Failure (không giữ chức vụ) |
| UTCID916 | TeamService.DismissAsync | Abnormal | — | employeeId=999 | Failure `NotFound` |
| UTCID917 | TeamService.CandidatesAsync | Normal | E5, E8 Active, TeamId=null; E9 Resigned, TeamId=null | teamId=A | Success; [E5, E8] (không có E9) |
| UTCID918 | TeamService.CandidatesAsync | Boundary | Không có NV tự do | teamId=A | Success; list rỗng |
| UTCID919 | TeamService.CandidatesAsync | Abnormal | — | teamId=999 | Failure `NotFound` |

### Sheet `AttendanceService` (P2-1) — UTCID920–UTCID931 (12 case)

| Test Case ID | Function Name | Type (Normal/Boundary/Abnormal) | Pre-conditions | Input parameters | Expected Results (Return / Exception / Log Message) |
|---|---|---|---|---|---|
| UTCID920 | AttendanceService.AlertSyncOverdueAsync | Normal | Threshold=30 phút; LastSucceededAt=Now−45 phút; chưa có notification AttendanceSyncOverdue | ct | NotifyRole(Admin) 1 lần; SaveChanges 1 lần; LogWarning "Attendance sync overdue" |
| UTCID921 | AttendanceService.AlertSyncOverdueAsync | Normal | LastSucceededAt=Now−10 phút | ct | Return sớm; Notifier và SaveChanges không gọi |
| UTCID922 | AttendanceService.AlertSyncOverdueAsync | Boundary | LastSucceededAt=Now−30 phút đúng | ct | Không overdue (`>`); không notify |
| UTCID923 | AttendanceService.AlertSyncOverdueAsync | Boundary | LastSucceededAt=Now−31 phút | ct | Overdue; notify Admin |
| UTCID924 | AttendanceService.AlertSyncOverdueAsync | Boundary | Setting threshold không cấu hình; LastSucceededAt=Now−31 phút | ct | Dùng mặc định 30 → notify |
| UTCID925 | AttendanceService.AlertSyncOverdueAsync | Boundary | Chưa từng sync (lastAt=null); chưa có notification | gọi lần 1 | Overdue; notify Admin 1 lần |
| UTCID926 | AttendanceService.AlertSyncOverdueAsync | Boundary | lastAt=null; đã có notification từ lần 1 (since=MinValue) | gọi lần 2 | Không notify lại (rủi ro #1: im đến khi có sync thành công) |
| UTCID927 | AttendanceService.AlertSyncOverdueAsync | Boundary | LastSucceededAt=Now−45 phút; đã có notification CreatedAt=Now−5 phút (> since) | ct | Không notify (đã báo đợt này) |
| UTCID928 | AttendanceService.AlertSyncOverdueAsync | Boundary | Notification cũ CreatedAt=Now−3 giờ, trước LastSucceededAt=Now−45 phút | ct | Notify mới (đợt overdue mới) |
| UTCID929 | AttendanceService.AlertSyncOverdueAsync | Abnormal | Overdue; mock Notifier ném exception | ct | Exception propagate; SaveChanges không gọi (hàm trả Task, không Result) |
| UTCID930 | AttendanceService.AlertSyncOverdueAsync | Abnormal | Overdue; Notifier OK; SaveChanges ném exception | ct | Exception propagate. Ghi nhận Notify và SaveChanges tách nhau (rủi ro #5) |
| UTCID931 | AttendanceService.AlertSyncOverdueAsync | Abnormal | Overdue | ct | Chỉ role Admin nhận; Manager **không** nhận (rủi ro #4). Xác nhận spec |

### Sheet `MachineRepairRequestHandler` (P2-2) — UTCID932–UTCID945 (14 case)

`NoLineRequestHandler` là lớp abstract, nên kiểm thử qua lớp con `MachineRepairRequestHandler`. Có thể đặt tên sheet là `NoLine_MachineRepairHandler` nếu muốn thể hiện cả lớp cha.

| Test Case ID | Function Name | Type (Normal/Boundary/Abnormal) | Pre-conditions | Input parameters | Expected Results (Return / Exception / Log Message) |
|---|---|---|---|---|---|
| UTCID932 | MachineRepairRequestHandler.ValidateAsync | Normal | Today=2026-11-24 | dto {RequestId=null, Lines=[], TargetDate=2026-11-26} | Success |
| UTCID933 | MachineRepairRequestHandler.ValidateAsync | Boundary | — | create, Lines=[], TargetDate=2026-11-24 (= Today) | Success |
| UTCID934 | MachineRepairRequestHandler.ValidateAsync | Boundary | — | create, Lines=[], TargetDate=2026-11-23 (Today−1) | Failure `RequisitionTargetDatePast` |
| UTCID935 | MachineRepairRequestHandler.ValidateAsync | Boundary | — | update {RequestId=5, Lines=[], TargetDate=2026-11-23} | Success (update bỏ qua check ngày quá khứ). Xác nhận spec |
| UTCID936 | MachineRepairRequestHandler.ValidateAsync | Boundary | — | create, Lines=[], TargetDate=null | Success (không check) |
| UTCID937 | MachineRepairRequestHandler.ValidateAsync | Abnormal | — | create, Lines=[1 dòng], TargetDate=Today+1 | Failure message `MachineRepairNoLines`, ErrorCode `RequestLinesInvalid` |
| UTCID938 | MachineRepairRequestHandler.ValidateAsync | Abnormal | — | dto=null | `ArgumentNullException` hoặc Failure |
| UTCID939 | MachineRepairRequestHandler.OnStatusChangedAsync | Normal | Request MACHINE_REPAIR | status mới = Completed | Success; không side-effect |
| UTCID940 | MachineRepairRequestHandler.OnStatusChangedAsync | Boundary | — | status mới = Rejected / Cancelled | Success |
| UTCID941 | MachineRepairRequestHandler.OnStatusChangedAsync | Abnormal | — | status mới = null hoặc giá trị lạ "FOO" | Success (handler không xử lý theo status); không exception |
| UTCID942 | MachineRepairRequestHandler (Policy) | Normal | Người tạo: Tổ trưởng tổ cắt | tạo đơn sửa máy | Được phép; người xử lý = Electrician; handler có RequestType="MACHINE_REPAIR", AllowManualComplete=true |
| UTCID943 | MachineRepairRequestHandler (Policy) | Normal | Người tạo: Tổ trưởng tổ may | tạo đơn sửa máy | Được phép; người xử lý = Electrician |
| UTCID944 | MachineRepairRequestHandler (Policy) | Boundary | Người tạo: NV đang giữ quyền tổ trưởng tạm (acting leader) tổ may | tạo đơn sửa máy | Được phép như tổ trưởng (xác nhận spec) |
| UTCID945 | MachineRepairRequestHandler (Policy) | Abnormal | Người tạo: Worker | tạo đơn sửa máy | Không được phép theo policy (Forbidden) |

### Sheet `HomeDashboardService` (P2-3) — UTCID946–UTCID974 (29 case)

| Test Case ID | Function Name | Type (Normal/Boundary/Abnormal) | Pre-conditions | Input parameters | Expected Results (Return / Exception / Log Message) |
|---|---|---|---|---|---|
| UTCID946 | HomeDashboardService.GetAdminAsync | Normal | 3 PieceRate PENDING, 1 Payroll PendingApproval; doanh thu/chi phí tháng 11 và tháng 10; 4 đơn đang chạy | ct | Success DTO: PieceRatePending=3, PayrollPending=1, Revenue/Cost this & last, RunningOrders=4 |
| UTCID947 | HomeDashboardService.GetAdminAsync | Boundary | Chưa nhập chi phí tháng 11 | ct | CostThisMonth=null (không phải 0) |
| UTCID948 | HomeDashboardService.GetAdminAsync | Boundary | Đơn status lưu "IN_PRODUCTION"; RunningStatuses dạng Pascal | ct | Đơn vẫn được đếm. ⚠ Lệch casing → đếm 0 |
| UTCID949 | HomeDashboardService.GetAdminAsync | Abnormal | Mock service doanh thu ném exception | ct | Không exception; Revenue=0 (fallback); nên có log lỗi |
| UTCID950 | HomeDashboardService.GetManagerAsync | Normal | 2 phiếu nhận chờ; inbox 3 việc; 1 quyền tổ trưởng tạm; 5 đơn mở | ct | Success DTO đủ 4 khối số liệu |
| UTCID951 | HomeDashboardService.GetManagerAsync | Boundary | Inbox có 7 việc | ct | Inbox trả 5 (giới hạn) |
| UTCID952 | HomeDashboardService.GetManagerAsync | Abnormal | Mock inbox service ném exception | ct | Inbox = [] ; phần khác vẫn có dữ liệu |
| UTCID953 | HomeDashboardService.GetAccountantAsync | Normal | 4 NV thiếu chấm công; 2 công đoạn thiếu đơn giá; 3 PO chưa trả; 2 đơn chưa chốt; chi phí/lương tháng | ct | Success DTO đúng số liệu |
| UTCID954 | HomeDashboardService.GetAccountantAsync | Boundary | Không có PO chưa trả | ct | UnpaidPO=0 |
| UTCID955 | HomeDashboardService.GetAccountantAsync | Abnormal | Mock timesheet service ném exception | ct | MissingPunch=0 (fallback); không exception |
| UTCID956 | HomeDashboardService.GetWarehouseAsync | Normal | 3 lô đến; 2 lệnh cắt chưa cấp NVL; 4 đơn xin; phiếu cắt/TP/trả chờ | ct | Success DTO đủ khối |
| UTCID957 | HomeDashboardService.GetWarehouseAsync | Boundary | Không có lô đến, lệnh, đơn xin, phiếu chờ | ct | Mọi số đếm = 0; list rỗng |
| UTCID958 | HomeDashboardService.GetWarehouseAsync | Abnormal | Mock incoming ném exception | ct | Incoming = (0, []) ; không exception |
| UTCID959 | HomeDashboardService.GetTeamLeaderAsync | Normal | Tổ trưởng tổ cắt | ct | CuttingPanel có dữ liệu; SewingPanel=null |
| UTCID960 | HomeDashboardService.GetTeamLeaderAsync | Normal | Tổ trưởng tổ may | ct | SewingPanel có dữ liệu; CuttingPanel=null |
| UTCID961 | HomeDashboardService.GetTeamLeaderAsync | Boundary | Người dùng TeamId=null | ct | Cả 2 panel = null; không exception |
| UTCID962 | HomeDashboardService.GetTeamLeaderAsync | Abnormal | Tổ loại Office | ct | Cả 2 panel = null |
| UTCID963 | HomeDashboardService.GetQaqcAsync | Normal | Đã cấp 500, hoàn thành 300, hủy 20; 100 TP chưa nhập kho | ct | OnLineQty=180; FinishedNotReceived=100 |
| UTCID964 | HomeDashboardService.GetQaqcAsync | Boundary | issued − finished − scrap = −10 (data lệch) | ct | OnLineQty=0 (Max(0, …)) |
| UTCID965 | HomeDashboardService.GetQaqcAsync | Abnormal | Mock service số cấp phát ném exception | ct | OnLineQty=0 (fallback); không exception |
| UTCID966 | HomeDashboardService.GetElectricianAsync | Normal | 3 ticket open do user hiện tại tạo; 2 ticket do người khác tạo | ct | Trả 3 ticket của user hiện tại |
| UTCID967 | HomeDashboardService.GetElectricianAsync | Boundary | 25 ticket open của user | ct | Trả 20 (giới hạn) |
| UTCID968 | HomeDashboardService.GetElectricianAsync | Boundary | Không có ticket | ct | List rỗng |
| UTCID969 | HomeDashboardService.GetElectricianAsync | Abnormal | Ticket của user đã Closed; mock repo ticket ném exception ở lần gọi khác | ct | Ticket Closed không được trả; khi repo lỗi → list rỗng (fallback), không exception |
| UTCID970 | HomeDashboardService.GetWorkerAsync | Normal | Worker khoán có sản lượng hôm nay, payslip tháng 10, 2 allocation PENDING, 1 defect 3 ngày trước, suất ăn hôm nay | ct | Success DTO: EstPay > 0, Payslip, PendingAlloc=2, Defects7d=1, Meal |
| UTCID971 | HomeDashboardService.GetWorkerAsync | Boundary | Worker không hưởng lương khoán | ct | EstPay=null |
| UTCID972 | HomeDashboardService.GetWorkerAsync | Boundary | Không có sản lượng hôm nay | ct | Today=0 |
| UTCID973 | HomeDashboardService.GetWorkerAsync | Boundary | Defect cách đúng 7 ngày và 8 ngày | ct | Đếm defect 7 ngày, loại defect 8 ngày |
| UTCID974 | HomeDashboardService.GetWorkerAsync | Abnormal | Mock payslip service ném exception | ct | Payslip=null (fallback); không exception |

---

## Phần 5 — Số liệu gợi ý để cập nhật các sheet tổng hợp (tự cập nhật thủ công)

### Test Statistics — số TC theo Type sau re-baseline

| Function code | N | A | B | Total Test Cases | Ghi chú |
|---|---:|---:|---:|---:|---|
| OperationService | 13 | 24 | 33 | 70 | Không đổi |
| MaterialReceiptService | 12 | 28 | 24 | 64 | Không đổi |
| PayrollService | 15 | 28 | 26 | 69 | Không đổi |
| OutputService | 7 | 19 | 22 | 48 | Không đổi |
| MaterialIssueService | 8 | 18 | 14 | 40 | Không đổi |
| ProductSpecService | 7 | 22 | 12 | 41 | 23 + 18 mới; 5 case sửa không đổi Type |
| WorkOrderService | 12 | 25 | 22 | 59 | Không đổi |
| DefectService | 9 | 20 | 13 | 42 | Không đổi |
| CustomerOrderService | 12 | 25 | 17 | 54 | Không đổi |
| DailyWorkAllocationService | 10 | 25 | 24 | 59 | 42 + 17 mới (Settle) |
| VehicleService | 7 | 18 | 16 | 41 | 21 + 20 mới (CompleteTrip) |
| FinanceService | 13 | 27 | 32 | 72 | Sheet mới |
| OrderPaymentService | 18 | 27 | 45 | 90 | Sheet mới |
| SpecImportBuilder | 4 | 14 | 19 | 37 | Sheet mới |
| SpecImportService | 7 | 15 | 11 | 33 | Sheet mới |
| EmployeeService | 8 | 20 | 12 | 40 | Sheet mới |
| TeamService | 14 | 29 | 17 | 60 | Sheet mới |
| AttendanceService | 2 | 3 | 7 | 12 | Sheet mới |
| MachineRepairRequestHandler | 4 | 4 | 6 | 14 | Sheet mới |
| HomeDashboardService | 9 | 8 | 12 | 29 | Sheet mới |
| **Sub total** | **191** | **399** | **384** | **974** | 532 cũ + 442 mới |

Tỷ lệ: Normal 19.61% · Abnormal 40.97% · Boundary 39.43%.

Các case mới và 5 case sửa nên để cột Passed/Failed ở trạng thái **Untested/Pending** cho tới khi chạy.

### Cover — gợi ý dòng Record of change

| Effective Date | Version | Change Item | *A,D,M | Change description | Reference |
|---|---|---|---|---|---|
| 2026-11-24 | 3.1 | Unit Test Re-baseline `fa1e8b8 → 85d50fd` | A, M | M: sửa expected UTCID292, 294, 296, 301, 313 (ProductSpec color-only, Pieces [1,99]). A: UTCID533–UTCID974 (442 TC) gồm ProductSpec bổ sung, SettleOnTeamLeave, Vehicle CompleteTrip, và 9 sheet mới (Finance, OrderPayment, SpecImportBuilder, SpecImportService, Employee, Team, Attendance, MachineRepairRequestHandler, HomeDashboard) | QA_PHAN_TICH_HAM_PUBLIC_SERVICE_fa1e8b8-85d50fd.md |

### Test Cases — dòng module

| No | Module / Sheet | Mô tả gợi ý |
|---|---|---|
| 6 | ProductSpecService | 41 TC (UTCID292–314, UTCID533–550): Get/Save/Check/Write + color-only, Validate mở rộng |
| 10 | DailyWorkAllocationService | 59 TC (UTCID470–511, UTCID551–567): GetBoard/Save + DEV-ALL-157 + SettleOnTeamLeave |
| 11 | VehicleService | 41 TC (UTCID512–532, UTCID568–587): GetLiveStatus/StartTrip + CompleteTrip (FuelCost + ảnh hóa đơn) |
| 12 | FinanceService | 72 TC (UTCID588–UTCID659) |
| 13 | OrderPaymentService | 90 TC (UTCID660–UTCID749) |
| 14 | SpecImportBuilder | 37 TC (UTCID750–UTCID786) |
| 15 | SpecImportService | 33 TC (UTCID787–UTCID819) |
| 16 | EmployeeService | 40 TC (UTCID820–UTCID859) |
| 17 | TeamService | 60 TC (UTCID860–UTCID919) |
| 18 | AttendanceService | 12 TC (UTCID920–UTCID931) |
| 19 | MachineRepairRequestHandler | 14 TC (UTCID932–UTCID945) |
| 20 | HomeDashboardService | 29 TC (UTCID946–UTCID974) |
