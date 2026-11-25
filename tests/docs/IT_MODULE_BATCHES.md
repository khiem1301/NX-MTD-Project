# IT_MODULE_BATCHES — Kế hoạch Integration Test cuốn chiếu

> Nguồn cấu trúc: `tests/docs/UT_SUMMARY_AND_CONSTRAINTS.md` (§4.4 cây FK), `tests/docs/high-complexity-services/CODEBASE_MAP.md` (mục lục module), danh sách tên file `be/API/Controllers`, `be/Application/DTOs`, `be/Domain/Entities`.
> Nguyên tắc: Batch sớm = bảng gốc / ít FK; Batch muộn = chứng từ nhiều FK; **Import/Export + Báo cáo/Home** để **Batch cuối**.
> Mỗi Batch ~2–4 nhóm Controller nghiệp vụ liên quan (liệt kê đủ biến thể theo role để copy-paste `@`).

---

## Batch 1 — Auth & HR nền (ưu tiên 1)

**Quan hệ bảng DB:** `Role` → `Employee` → `Account` (1–1); `Team` → `Employee`; master hạng/bậc `GradeLevel` / `GradeLevelCode`. Ít FK nghiệp vụ — nền cho mọi batch sau.

**Danh sách file chỉ định cho Claude (Copy-paste sẵn):**

```
@be/API/Controllers/Common/AuthController.cs @be/API/Controllers/Common/ProfileController.cs @be/API/Controllers/Common/LookupsController.cs @be/API/Controllers/Common/HrLookupsController.cs @be/API/Controllers/Admin/EmployeesController.cs @be/API/Controllers/Manager/EmployeesController.cs @be/API/Controllers/Admin/TeamsController.cs @be/API/Controllers/Manager/TeamsController.cs @be/API/Controllers/Admin/GradeLevelsController.cs @be/API/Controllers/Admin/GradeCodesController.cs @be/API/Controllers/Common/GradeCodeOptionsController.cs @be/Application/DTOs/Auth/AuthDtos.cs @be/Application/DTOs/Hr/HrDtos.cs @be/Application/DTOs/Hr/GradeDtos.cs @be/Application/DTOs/Shared/LookupDtos.cs @be/Domain/Entities/Role.cs @be/Domain/Entities/Account.cs @be/Domain/Entities/Employee.cs @be/Domain/Entities/Team.cs @be/Domain/Entities/GradeLevel.cs @be/Domain/Entities/GradeLevelCode.cs
```

---

## Batch 2 — Master Catalog (ưu tiên 2)

**Quan hệ bảng DB:** `Customer`, `Supplier`, `Material`, `Vehicle` gần như độc lập; `Product` (+ `ProductImage`) là gốc mã hàng. Chưa phụ thuộc đơn hàng / chứng từ kho.

**Danh sách file chỉ định cho Claude (Copy-paste sẵn):**

```
@be/API/Controllers/Admin/CustomersController.cs @be/API/Controllers/Admin/SuppliersController.cs @be/API/Controllers/Manager/SuppliersController.cs @be/API/Controllers/Admin/MaterialsController.cs @be/API/Controllers/Manager/MaterialsController.cs @be/API/Controllers/Accountant/MaterialsController.cs @be/API/Controllers/WarehouseKeeper/MaterialsController.cs @be/API/Controllers/Admin/ProductsController.cs @be/API/Controllers/Accountant/ProductsController.cs @be/API/Controllers/Admin/VehiclesController.cs @be/API/Controllers/WarehouseKeeper/VehiclesController.cs @be/API/Controllers/Accountant/VehiclesController.cs @be/Application/DTOs/Orders/OrderDtos.cs @be/Application/DTOs/Warehouse/MaterialDtos.cs @be/Application/DTOs/Vehicle/VehicleDtos.cs @be/Application/DTOs/Purchasing/PurchaseOrderDtos.cs @be/Domain/Entities/Customer.cs @be/Domain/Entities/Supplier.cs @be/Domain/Entities/Material.cs @be/Domain/Entities/Product.cs @be/Domain/Entities/ProductImage.cs @be/Domain/Entities/Vehicle.cs
```

---

## Batch 3 — Định mức mã hàng & Công đoạn (ưu tiên 3)

**Quan hệ bảng DB:** `Product` → `ProductColor` → `ProductVariant`; `Product` → `ProductComponent` → `ProductComponentMaterial` → `Material`; `Product` → `Operation` / `OperationGroup` / `PieceRate`; `ProductMarker*` gắn Product (và sau này Order).

**Danh sách file chỉ định cho Claude (Copy-paste sẵn):**

```
@be/API/Controllers/Admin/ProductsController.cs @be/API/Controllers/Accountant/ProductsController.cs @be/API/Controllers/Admin/OperationsController.cs @be/API/Controllers/Accountant/OperationsController.cs @be/API/Controllers/Admin/OperationGroupsController.cs @be/API/Controllers/Accountant/OperationGroupsController.cs @be/API/Controllers/Admin/PieceRatesController.cs @be/API/Controllers/Accountant/PieceRatesController.cs @be/Application/DTOs/Orders/ProductSpecDtos.cs @be/Application/DTOs/Production/ProductionDtos.cs @be/Domain/Entities/ProductColor.cs @be/Domain/Entities/ProductVariant.cs @be/Domain/Entities/ProductComponent.cs @be/Domain/Entities/ProductComponentMaterial.cs @be/Domain/Entities/ProductMarker.cs @be/Domain/Entities/ProductMarkerComponent.cs @be/Domain/Entities/ProductMarkerFabric.cs @be/Domain/Entities/ProductMarkerRatio.cs @be/Domain/Entities/Operation.cs @be/Domain/Entities/OperationGroup.cs @be/Domain/Entities/OperationComponent.cs @be/Domain/Entities/PieceRate.cs
```

---

## Batch 4 — Đơn hàng khách & phân tổ / NVL đơn (ưu tiên 4)

**Quan hệ bảng DB:** `Customer` → `CustomerOrder` → `OrderLine` → `OrderLineTeam`; `CustomerOrder` × `Product` → `OrderMaterial`, `ProductMarker` (bìa đơn); phụ thuộc Batch 1–3.

**Danh sách file chỉ định cho Claude (Copy-paste sẵn):**

```
@be/API/Controllers/Admin/CustomerOrdersController.cs @be/API/Controllers/Manager/CustomerOrdersController.cs @be/API/Controllers/Manager/OrderLineTeamsController.cs @be/API/Controllers/Manager/OrderMarkersController.cs @be/API/Controllers/Admin/OrderMaterialsController.cs @be/API/Controllers/Manager/OrderMaterialsController.cs @be/API/Controllers/WarehouseKeeper/OrderMaterialsController.cs @be/API/Controllers/Common/OrderLookupsController.cs @be/Application/DTOs/Orders/OrderDtos.cs @be/Application/DTOs/Orders/OrderMarkerDtos.cs @be/Application/DTOs/Warehouse/OrderMaterialDtos.cs @be/Application/DTOs/Shared/LookupDtos.cs @be/Domain/Entities/CustomerOrder.cs @be/Domain/Entities/OrderLine.cs @be/Domain/Entities/OrderLineTeam.cs @be/Domain/Entities/OrderMaterial.cs @be/Domain/Entities/DeliveryScheduleDate.cs
```

---

## Batch 5 — Mua hàng (ưu tiên 5)

**Quan hệ bảng DB:** `Supplier` → `PurchaseOrder` → `PurchaseOrderLine` → `PurchasePayment`; lịch giao `DeliveryScheduleDate`; liên kết `OrderMaterial` / thiếu hàng. Phụ thuộc Catalog + (tuỳ case) đơn hàng.

**Danh sách file chỉ định cho Claude (Copy-paste sẵn):**

```
@be/API/Controllers/Manager/PurchaseOrdersController.cs @be/API/Controllers/WarehouseKeeper/PurchaseOrdersController.cs @be/API/Controllers/Manager/PurchaseOrderTrackingController.cs @be/API/Controllers/Accountant/PurchasePaymentsController.cs @be/API/Controllers/Manager/SuppliersController.cs @be/API/Controllers/Admin/SuppliersController.cs @be/Application/DTOs/Purchasing/PurchaseOrderDtos.cs @be/Application/DTOs/Purchasing/PurchaseFlowDtos.cs @be/Domain/Entities/PurchaseOrder.cs @be/Domain/Entities/PurchaseOrderLine.cs @be/Domain/Entities/PurchasePayment.cs @be/Domain/Entities/DeliveryScheduleDate.cs @be/Domain/Entities/Supplier.cs
```

---

## Batch 6 — Nhập kho NVL & thiếu giao (ưu tiên 6)

**Quan hệ bảng DB:** `MaterialReceipt` → `MaterialReceiptLine` → tồn qua `StockBalance` / `StockTransaction` (`usp_Stock_Post`); nguồn PO hoặc khách cấp (`OrderMaterial`); `ShortDelivery` / Incoming. Nhiều FK tới Material + Order/PO.

**Danh sách file chỉ định cho Claude (Copy-paste sẵn):**

```
@be/API/Controllers/Manager/MaterialReceiptsController.cs @be/API/Controllers/WarehouseKeeper/MaterialReceiptsController.cs @be/API/Controllers/WarehouseKeeper/IncomingMaterialsController.cs @be/API/Controllers/Manager/ShortDeliveriesController.cs @be/API/Controllers/WarehouseKeeper/ShortDeliveriesController.cs @be/API/Controllers/Common/AttachmentsController.cs @be/Application/DTOs/Warehouse/MaterialReceiptDtos.cs @be/Application/DTOs/Warehouse/IncomingMaterialDtos.cs @be/Application/DTOs/Shared/AttachmentDtos.cs @be/Domain/Entities/MaterialReceipt.cs @be/Domain/Entities/MaterialReceiptLine.cs @be/Domain/Entities/StockBalance.cs @be/Domain/Entities/StockTransaction.cs @be/Domain/Entities/Attachment.cs @be/Domain/Entities/OrderMaterial.cs
```

---

## Batch 7 — Lệnh cắt & bàn cắt / bàn giao BTP (ưu tiên 7)

**Quan hệ bảng DB:** `CustomerOrder`/`OrderLine` → `WorkOrder` → `WorkOrderLine` → `CuttingLay` (+ Fabric/Line) → `CutHandover` (+ Line). Phụ thuộc đơn + định mức/bìa + tồn vải.

**Danh sách file chỉ định cho Claude (Copy-paste sẵn):**

```
@be/API/Controllers/Manager/WorkOrdersController.cs @be/API/Controllers/TeamLeader/WorkOrdersController.cs @be/API/Controllers/WarehouseKeeper/WorkOrdersController.cs @be/API/Controllers/TeamLeader/CuttingLaysController.cs @be/API/Controllers/TeamLeader/CutHandoversController.cs @be/API/Controllers/WarehouseKeeper/CutHandoversController.cs @be/Application/DTOs/Cutting/WorkOrderDtos.cs @be/Application/DTOs/Cutting/CuttingDtos.cs @be/Domain/Entities/WorkOrder.cs @be/Domain/Entities/WorkOrderLine.cs @be/Domain/Entities/CuttingLay.cs @be/Domain/Entities/CuttingLayLine.cs @be/Domain/Entities/CuttingLayFabric.cs @be/Domain/Entities/CutHandover.cs @be/Domain/Entities/CutHandoverLine.cs
```

---

## Batch 8 — Cấp / trả NVL-BTP & tồn / kiểm kê (ưu tiên 8)

**Quan hệ bảng DB:** `MaterialIssue` → `MaterialIssueLine` (tổ / công đoạn / BTP); `MaterialReturn` → Line; `Stocktake` → Line → điều chỉnh `Stock*`. Phụ thuộc WorkOrder / phân công / tồn Batch 6–7.

**Danh sách file chỉ định cho Claude (Copy-paste sẵn):**

```
@be/API/Controllers/WarehouseKeeper/MaterialIssuesController.cs @be/API/Controllers/TeamLeader/MaterialIssuesController.cs @be/API/Controllers/WarehouseKeeper/MaterialReturnsController.cs @be/API/Controllers/TeamLeader/MaterialReturnsController.cs @be/API/Controllers/Admin/InventoriesController.cs @be/API/Controllers/Manager/InventoriesController.cs @be/API/Controllers/WarehouseKeeper/InventoriesController.cs @be/API/Controllers/Admin/StocktakesController.cs @be/API/Controllers/Manager/StocktakesController.cs @be/API/Controllers/WarehouseKeeper/StocktakesController.cs @be/Application/DTOs/Warehouse/MaterialIssueDtos.cs @be/Application/DTOs/Warehouse/MaterialReturnDtos.cs @be/Application/DTOs/Warehouse/StockDtos.cs @be/Application/DTOs/Warehouse/StocktakeDtos.cs @be/Domain/Entities/MaterialIssue.cs @be/Domain/Entities/MaterialIssueLine.cs @be/Domain/Entities/MaterialReturn.cs @be/Domain/Entities/MaterialReturnLine.cs @be/Domain/Entities/Stocktake.cs @be/Domain/Entities/StocktakeLine.cs @be/Domain/Entities/StockBalance.cs
```

---

## Batch 9 — May: phân công, giao việc, sản lượng, lỗi, QC (ưu tiên 9)

**Quan hệ bảng DB:** `OrderLineTeam` + `Operation` → `OperationAssignment` / `WorkAllocation`; `OutputRecord`, `DefectRecord`, `QcBatch` phụ thuộc tổ + công đoạn + trần BTP/giao việc. Nhiều FK sản xuất.

**Danh sách file chỉ định cho Claude (Copy-paste sẵn):**

```
@be/API/Controllers/TeamLeader/OperationAssignmentsController.cs @be/API/Controllers/TeamLeader/WorkAllocationsController.cs @be/API/Controllers/Worker/WorkAllocationsController.cs @be/API/Controllers/TeamLeader/PieceHoldingsController.cs @be/API/Controllers/TeamLeader/OutputRecordsController.cs @be/API/Controllers/Manager/OutputRecordsController.cs @be/API/Controllers/TeamLeader/DefectRecordsController.cs @be/API/Controllers/Manager/DefectRecordsController.cs @be/API/Controllers/Common/MyDefectsController.cs @be/API/Controllers/Manager/QcBatchesController.cs @be/API/Controllers/QaQc/QcBatchesController.cs @be/Application/DTOs/Production/ProductionDtos.cs @be/Application/DTOs/Production/WorkAllocationDtos.cs @be/Domain/Entities/OperationAssignment.cs @be/Domain/Entities/WorkAllocation.cs @be/Domain/Entities/OutputRecord.cs @be/Domain/Entities/DefectRecord.cs @be/Domain/Entities/QcBatch.cs
```

---

## Batch 10 — Thành phẩm, xuất giao khách & chuyến xe (ưu tiên 10)

**Quan hệ bảng DB:** `QcBatch` / tồn FINISHED → `FgHandover` → Line; `DispatchNote` → Line → cập nhật `CustomerOrder`; `Vehicle` → `VehicleTrip`. Phụ thuộc QC + đơn + xe.

**Danh sách file chỉ định cho Claude (Copy-paste sẵn):**

```
@be/API/Controllers/QaQc/FgHandoversController.cs @be/API/Controllers/WarehouseKeeper/FgHandoversController.cs @be/API/Controllers/WarehouseKeeper/DispatchNotesController.cs @be/API/Controllers/Manager/DispatchNotesController.cs @be/API/Controllers/Accountant/DispatchNotesController.cs @be/API/Controllers/Accountant/VehicleTripsController.cs @be/API/Controllers/Admin/VehiclesController.cs @be/API/Controllers/WarehouseKeeper/VehiclesController.cs @be/Application/DTOs/Warehouse/FgHandoverDtos.cs @be/Application/DTOs/Warehouse/DispatchNoteDtos.cs @be/Application/DTOs/Vehicle/VehicleDtos.cs @be/Domain/Entities/FgHandover.cs @be/Domain/Entities/FgHandoverLine.cs @be/Domain/Entities/DispatchNote.cs @be/Domain/Entities/DispatchNoteLine.cs @be/Domain/Entities/VehicleTrip.cs @be/Domain/Entities/Vehicle.cs
```

---

## Batch 11 — Chấm công, suất ăn & đơn xin (ưu tiên 11)

**Quan hệ bảng DB:** `Employee` → `AttendanceLog` / `DailyTimesheet` / `AttendanceAdjustment` / `AttendanceSyncSession`; `MealRegistration` / `DailyMeal` / `TeamMealConfirmation`; `Request` → `RequestLine` (+ loại MACHINE_REPAIR…). Độc lập tương đối với kho/cắt nhưng cần HR seed.

**Danh sách file chỉ định cho Claude (Copy-paste sẵn):**

```
@be/API/Controllers/Accountant/AttendanceController.cs @be/API/Controllers/TeamLeader/AttendanceController.cs @be/API/Controllers/Common/MyAttendanceController.cs @be/API/Controllers/Agent/AttendanceSyncController.cs @be/API/Controllers/Common/RjEmployeesController.cs @be/API/Controllers/Accountant/MealsController.cs @be/API/Controllers/TeamLeader/MealsController.cs @be/API/Controllers/Common/MyMealsController.cs @be/API/Controllers/Admin/MealSettingsController.cs @be/API/Controllers/Common/RequestsController.cs @be/API/Controllers/Common/RequestInboxController.cs @be/Application/DTOs/Attendance/AttendanceDtos.cs @be/Application/DTOs/Attendance/MealDtos.cs @be/Application/DTOs/Attendance/MealSettingsDtos.cs @be/Application/DTOs/Requests/RequestDtos.cs @be/Domain/Entities/AttendanceLog.cs @be/Domain/Entities/DailyTimesheet.cs @be/Domain/Entities/AttendanceAdjustment.cs @be/Domain/Entities/AttendanceSyncSession.cs @be/Domain/Entities/RjEmployeeSnapshot.cs @be/Domain/Entities/MealRegistration.cs @be/Domain/Entities/DailyMeal.cs @be/Domain/Entities/TeamMealConfirmation.cs @be/Domain/Entities/Request.cs @be/Domain/Entities/RequestLine.cs @be/Domain/Entities/SystemSetting.cs
```

---

## Batch 12 — Thanh toán đơn, lương & chi phí tháng (ưu tiên 12)

**Quan hệ bảng DB:** `CustomerOrder` → `OrderPayment`; `Payroll` → `Payslip` → `PayslipItem` (cần sản lượng/công); `AccountingPeriod` khóa tháng; `MonthlyCost`. Phụ thuộc đơn + attendance/output.

**Danh sách file chỉ định cho Claude (Copy-paste sẵn):**

```
@be/API/Controllers/Accountant/OrderPaymentsController.cs @be/API/Controllers/Accountant/PayrollsController.cs @be/API/Controllers/Admin/PayrollsController.cs @be/API/Controllers/Common/MyPayslipsController.cs @be/API/Controllers/Common/MyEstimatedPayController.cs @be/API/Controllers/Accountant/MonthlyCostsController.cs @be/Application/DTOs/Finance/OrderPaymentDtos.cs @be/Application/DTOs/Finance/PayrollDtos.cs @be/Application/DTOs/Finance/MonthlyCostDtos.cs @be/Application/DTOs/Finance/WorkerPayDtos.cs @be/Domain/Entities/OrderPayment.cs @be/Domain/Entities/Payroll.cs @be/Domain/Entities/Payslip.cs @be/Domain/Entities/PayslipItem.cs @be/Domain/Entities/MonthlyCost.cs @be/Domain/Entities/AccountingPeriod.cs
```

---

## Batch 13 — KPI, đánh giá bậc & thông báo (ưu tiên 13)

**Quan hệ bảng DB:** `KpiTarget` → `KpiTargetLine` / `KpiDailyTarget` (tổ may + đơn/BTP); `GradeEvaluation`; `Announcement` → `AnnouncementTarget`; `Notification`. Phụ thuộc tổ + tiến độ may.

**Danh sách file chỉ định cho Claude (Copy-paste sẵn):**

```
@be/API/Controllers/Manager/KpiTargetsController.cs @be/API/Controllers/TeamLeader/KpiTargetsController.cs @be/API/Controllers/Admin/GradeEvaluationsController.cs @be/API/Controllers/Manager/GradeEvaluationsController.cs @be/API/Controllers/TeamLeader/GradeEvaluationsController.cs @be/API/Controllers/TeamLeader/GradeSuggestionsController.cs @be/API/Controllers/Admin/AnnouncementsController.cs @be/API/Controllers/Manager/AnnouncementsController.cs @be/API/Controllers/Common/NotificationsController.cs @be/Application/DTOs/Production/KpiDtos.cs @be/Application/DTOs/Production/KpiCalendarDtos.cs @be/Application/DTOs/Hr/GradeDtos.cs @be/Application/DTOs/Hr/GradeSuggestionDtos.cs @be/Application/DTOs/Shared/NotificationDtos.cs @be/Domain/Entities/KpiTarget.cs @be/Domain/Entities/KpiTargetLine.cs @be/Domain/Entities/KpiDailyTarget.cs @be/Domain/Entities/GradeEvaluation.cs @be/Domain/Entities/Announcement.cs @be/Domain/Entities/AnnouncementTarget.cs @be/Domain/Entities/Notification.cs @be/Domain/Entities/AuditLog.cs
```

---

## Batch 14 — Import/Export & Báo cáo / Home (ưu tiên cuối)

**Quan hệ bảng DB:** Không tạo bảng gốc mới — đọc/ghi hàng loạt lên Product/Order/Spec/Employee hoặc đọc tổng hợp (`BreakEvenResult`, view `Vw*`, tiến độ đơn, dashboard Home). Chạy sau khi seed đủ Batch 1–13.

**Danh sách file chỉ định cho Claude (Copy-paste sẵn):**

```
@be/API/Controllers/Common/ImportTemplatesController.cs @be/API/Controllers/Admin/EmployeeImportController.cs @be/API/Controllers/Manager/EmployeeImportController.cs @be/API/Controllers/Admin/BreakEvenController.cs @be/API/Controllers/Admin/CostSummaryController.cs @be/API/Controllers/Admin/RevenueController.cs @be/API/Controllers/Accountant/RevenueController.cs @be/API/Controllers/Accountant/AccountingReportsController.cs @be/API/Controllers/Admin/OrderProgressController.cs @be/API/Controllers/Manager/OrderProgressController.cs @be/API/Controllers/Admin/HomeController.cs @be/API/Controllers/Manager/HomeController.cs @be/API/Controllers/Accountant/HomeController.cs @be/API/Controllers/WarehouseKeeper/HomeController.cs @be/API/Controllers/TeamLeader/HomeController.cs @be/API/Controllers/QaQc/HomeController.cs @be/API/Controllers/Electrician/HomeController.cs @be/API/Controllers/Worker/HomeController.cs @be/API/Controllers/Admin/AuditLogsController.cs @be/Application/DTOs/Orders/ImportTemplateDtos.cs @be/Application/DTOs/Orders/ProductImportDtos.cs @be/Application/DTOs/Orders/OrderImportDtos.cs @be/Application/DTOs/Orders/SpecImportDtos.cs @be/Application/DTOs/Orders/ExcelBundleDtos.cs @be/Application/DTOs/Hr/EmployeeImportDtos.cs @be/Application/DTOs/Warehouse/DocumentExportDtos.cs @be/Application/DTOs/Finance/FinanceDtos.cs @be/Application/DTOs/Finance/AccountingReportDtos.cs @be/Application/DTOs/Finance/CostSummaryDtos.cs @be/Application/DTOs/Orders/OrderProgressDtos.cs @be/Application/DTOs/Home/HomeDtos.cs @be/Application/DTOs/Shared/AuditLogDtos.cs @be/Domain/Entities/BreakEvenResult.cs @be/Domain/Entities/VwOrderLineProgress.cs @be/Domain/Entities/VwMonthlyRevenue.cs @be/Domain/Entities/VwKpiDailyResult.cs @be/Domain/Entities/VwWorkerDailyEstimatedPay.cs @be/Domain/Entities/VwPayrollExportDatum.cs @be/Domain/Entities/VwMaterialStock.cs @be/Domain/Entities/VwStockBalance.cs
```

---

## Ghi chú chạy cuốn chiếu

| Bước | Gợi ý |
|---|---|
| Seed | Dùng `tests/Integration.Tests/Seed/TestDataSeeder*.cs` — bổ sung dữ liệu tối thiểu theo Batch đang chạy |
| Auth | Batch 1 tạo token theo role trước; các Batch sau gắn `[Authorize]` / IDOR |
| Không nhảy cóc | Batch 7+ cần Order + Material + (thường) tồn đã Confirm từ Batch 6 |
| Import/Report | Chỉ mở Batch 14 khi CRUD lõi đã xanh |
| BaseController / Ping | Bỏ qua trong paste nghiệp vụ (`*BaseController`, `PingController`) trừ khi test pipeline |
