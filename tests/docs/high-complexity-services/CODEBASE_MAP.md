# CODEBASE MAP — NX-MTD

> Bản đồ file logic nghiệp vụ (Service / Handler / Helper / Validator / Parser / Job / Solver).
> Phạm vi: `be/Application/Services`, `be/Application/Validators`, `be/Application/Common` (quy tắc), và hạ tầng có logic (`Infrastructure/Excel|Jobs|Ai|Optimization|Security|Notifications|Audit|Common`).
> Đã bỏ qua: Controllers mỏng, DTO, Interface, Entity, Mapping, Migrations, static assets, `node_modules`/`bin`/`obj`, Options thuần.

**Tổng số file:** 207  
**Phân bố độ phức tạp:** Cao = 60 · Trung bình = 89 · Thấp = 58

## Quy tắc đánh giá độ phức tạp

| Mức | Tiêu chí |
|---|---|
| **Cao** | Nhiều nhánh if/else/switch, vòng lặp + công thức, chuyển trạng thái, giao dịch nhiều bảng |
| **Trung bình** | Validation đầu vào, lọc/tìm kiếm nhiều điều kiện, parse/export có cấu trúc |
| **Thấp** | CRUD thuần / helper nhỏ / rule đơn giản, ít biến đổi nghiệp vụ |

## Mục lục module

1. [Auth](#auth) (2 file)
2. [Attendance](#attendance) (11 file)
3. [Cutting (Cắt)](#cutting-cắt) (15 file)
4. [Orders (Đơn hàng / Mã hàng)](#orders-đơn-hàng-mã-hàng) (26 file)
5. [Warehouse (Kho)](#warehouse-kho) (32 file)
6. [Purchasing (Mua hàng)](#purchasing-mua-hàng) (9 file)
7. [Production (Sản xuất / May)](#production-sản-xuất-may) (26 file)
8. [Hr (Nhân sự)](#hr-nhân-sự) (16 file)
9. [Finance (Tài chính)](#finance-tài-chính) (18 file)
10. [Requests (Đơn xin)](#requests-đơn-xin) (9 file)
11. [Vehicle (Xe / Chuyến)](#vehicle-xe-chuyến) (2 file)
12. [Shared (Dùng chung)](#shared-dùng-chung) (8 file)
13. [Shared (Excel / Hạ tầng)](#shared-excel-hạ-tầng) (12 file)
14. [Common (Quy tắc dùng chung)](#common-quy-tắc-dùng-chung) (21 file)

---

## Auth

| STT | Module | Đường dẫn file | Chức năng chính (tóm tắt 1 dòng) | Mức độ phức tạp logic |
| ---: | --- | --- | --- | --- |
| 1 | Auth | `@be/Application/Services/Auth/AuthService.cs` | Đăng nhập, refresh token, phát hành phiên và lấy thông tin me. | Trung bình |
| 2 | Auth | `@be/Application/Validators/Auth/AuthDtoValidators.cs` | Validate DTO đăng nhập/refresh. | Thấp |

---

## Attendance

| STT | Module | Đường dẫn file | Chức năng chính (tóm tắt 1 dòng) | Mức độ phức tạp logic |
| ---: | --- | --- | --- | --- |
| 3 | Attendance | `@be/Application/Services/Attendance/AttendanceService.cs` | Đồng bộ chấm công RJ + map mã + xem bảng công theo vai trò. | Cao |
| 4 | Attendance | `@be/Application/Services/Attendance/MealRegistrationSeeder.cs` | Seed suất ăn từ lượt quẹt cho người chưa có dòng đăng ký. | Thấp |
| 5 | Attendance | `@be/Application/Services/Attendance/MealService.cs` | Quản lý tick suất ăn, bỏ tick, chốt WillEat theo job. | Cao |
| 6 | Attendance | `@be/Application/Services/Attendance/MealSettingsService.cs` | Cấu hình khung giờ chốt suất ăn trưa. | Trung bình |
| 7 | Attendance | `@be/Application/Validators/Attendance/AttendanceDtoValidators.cs` | Validate định dạng đầu vào luồng chấm công. | Trung bình |
| 8 | Attendance | `@be/Application/Validators/Attendance/MealDtoValidators.cs` | Validate DTO suất ăn (định dạng). | Trung bình |
| 9 | Attendance | `@be/Application/Validators/Attendance/MealSettingsValidator.cs` | Validate cấu hình giờ chốt suất ăn. | Thấp |
| 10 | Attendance | `@be/Infrastructure/Excel/ClosedXmlAttendanceParser.cs` | Parse Excel chấm công dự phòng. | Cao |
| 11 | Attendance | `@be/Infrastructure/Jobs/AttendanceSyncMonitorJob.cs` | Job giám sát health đồng bộ chấm công RJ. | Thấp |
| 12 | Attendance | `@be/Infrastructure/Jobs/MealAutoCloseJob.cs` | JOB-03 08:30 chốt suất ăn theo WillEat. | Thấp |
| 13 | Attendance | `@be/Infrastructure/Jobs/MealReminderJob.cs` | JOB-02 08:15 nhắc tổ chưa xác nhận suất ăn. | Thấp |

---

## Cutting (Cắt)

| STT | Module | Đường dẫn file | Chức năng chính (tóm tắt 1 dòng) | Mức độ phức tạp logic |
| ---: | --- | --- | --- | --- |
| 14 | Cutting (Cắt) | `@be/Application/Services/Cutting/BranchAndBoundMarkerSolver.cs` | Giải tối ưu ghép bìa bằng duyệt nhánh có cận dưới. | Cao |
| 15 | Cutting (Cắt) | `@be/Application/Services/Cutting/CutHandoverService.cs` | Bàn giao BTP tổ cắt → thủ kho xác nhận/từ chối. | Cao |
| 16 | Cutting (Cắt) | `@be/Application/Services/Cutting/CuttingLayCalculator.cs` | Kiểm tổng SL sau cắt ≤ kế hoạch + % dư. | Thấp |
| 17 | Cutting (Cắt) | `@be/Application/Services/Cutting/CuttingLayService.cs` | Nhập bàn cắt (màu-size, mét vải) và chặn vượt kế hoạch/vải. | Cao |
| 18 | Cutting (Cắt) | `@be/Application/Services/Cutting/FabricPlanner.cs` | Tổng hợp vải cần/đang giữ/đã cấp/đã trả theo lệnh cắt. | Trung bình |
| 19 | Cutting (Cắt) | `@be/Application/Services/Cutting/MarkerFabricEstimator.cs` | Dựng bài toán ước mét vải theo bìa để solver giải. | Thấp |
| 20 | Cutting (Cắt) | `@be/Application/Services/Cutting/MarkerPlanning.cs` | Mô hình dữ liệu bìa/lớp/Across phục vụ ước vải. | Trung bình |
| 21 | Cutting (Cắt) | `@be/Application/Services/Cutting/WorkOrderCompletion.cs` | Tự hoàn thành lệnh cắt khi kho nhận đủ BTP. | Trung bình |
| 22 | Cutting (Cắt) | `@be/Application/Services/Cutting/WorkOrderFabricChecker.cs` | Kiểm đủ vải và ước mét khi lập lệnh cắt (cả bộ/cắt lẻ). | Cao |
| 23 | Cutting (Cắt) | `@be/Application/Services/Cutting/WorkOrderFabricNeeds.cs` | Tính mét vải cần và phần còn xin được theo trần lệnh. | Trung bình |
| 24 | Cutting (Cắt) | `@be/Application/Services/Cutting/WorkOrderFinishCheck.cs` | Điều kiện đóng lệnh cắt (đủ/thiếu) dùng chung. | Trung bình |
| 25 | Cutting (Cắt) | `@be/Application/Services/Cutting/WorkOrderService.cs` | Lập/sửa/đóng lệnh cắt; chặn cắt bù khi thiếu vải. | Cao |
| 26 | Cutting (Cắt) | `@be/Application/Validators/Cutting/CuttingDtoValidators.cs` | Validate DTO bàn cắt / bàn giao cắt. | Trung bình |
| 27 | Cutting (Cắt) | `@be/Application/Validators/Cutting/WorkOrderDtoValidators.cs` | Validate DTO lệnh cắt (ghi chú, lý do cắt thiếu...). | Trung bình |
| 28 | Cutting (Cắt) | `@be/Infrastructure/Optimization/OrToolsMarkerPlanSolver.cs` | Solver CP-SAT (OrTools) tối ưu ghép bìa/mét vải. | Cao |

---

## Orders (Đơn hàng / Mã hàng)

| STT | Module | Đường dẫn file | Chức năng chính (tóm tắt 1 dòng) | Mức độ phức tạp logic |
| ---: | --- | --- | --- | --- |
| 29 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/CustomerOrderService.cs` | Tạo/sửa/đóng đơn hàng khách kèm dòng hàng và cascade trạng thái. | Cao |
| 30 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/CustomerService.cs` | CRUD khách hàng (không xóa, chỉ ngừng hợp tác). | Trung bình |
| 31 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/ExcelBundleService.cs` | Kiểm tra file Excel nhiều sheet trên form mã hàng (không ghi). | Cao |
| 32 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/ImportTemplateService.cs` | Sinh file mẫu Excel theo chức năng (dữ liệu thật hoặc ví dụ). | Cao |
| 33 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/NormUsage.cs` | Kiểm định mức NVL đã được chứng từ nào dùng chưa. | Thấp |
| 34 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/OrderCloseCascade.cs` | Hủy việc chờ khi đơn hủy/dừng (phiếu xuất, lệnh cắt, KPI). | Trung bình |
| 35 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/OrderImportService.cs` | Xem trước dòng hàng đơn từ Excel ma trận màu×size. | Trung bình |
| 36 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/OrderLookupService.cs` | Lookup đơn/dòng cho kho-cắt-may (không lộ đơn giá). | Trung bình |
| 37 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/OrderMarkerService.cs` | Quản lý bìa cắt của đơn (tỉ lệ size, nhóm bìa). | Cao |
| 38 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/OrderProgressService.cs` | Dashboard tiến độ đơn + cờ nguy cơ trễ. | Trung bình |
| 39 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/ProductImageCleanupService.cs` | Dọn ảnh mẫu mã hàng mồ côi sau grace period. | Thấp |
| 40 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/ProductImportService.cs` | Validate dòng mã hàng từ Excel (không ghi DB). | Thấp |
| 41 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/ProductService.cs` | CRUD mã hàng + quản lý ảnh mẫu. | Cao |
| 42 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/ProductSpecService.cs` | Lưu thành phần, biến thể, định mức NVL và nhóm bìa của mã hàng. | Cao |
| 43 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/SpecImportBuilder.cs` | Dựng bản gộp định mức từ Excel để kiểm/ghi. | Cao |
| 44 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/SpecImportParts.cs` | Định nghĩa các bảng định mức có thể nhập riêng. | Thấp |
| 45 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/SpecImportService.Check.cs` | Kiểm bảng định mức Excel cho popup (kể cả mã hàng chưa tạo). | Cao |
| 46 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Services/Orders/SpecImportService.cs` | Import Excel định mức + bìa đơn trong một giao dịch. | Cao |
| 47 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Validators/Orders/OrderDtoValidators.cs` | Validate đơn hàng, khách hàng, mã hàng. | Trung bình |
| 48 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Validators/Orders/OrderMarkerDtoValidators.cs` | Validate bìa cắt của đơn. | Trung bình |
| 49 | Orders (Đơn hàng / Mã hàng) | `@be/Application/Validators/Orders/ProductSpecDtoValidators.cs` | Validate thành phần/định mức/nhóm bìa mã hàng. | Trung bình |
| 50 | Orders (Đơn hàng / Mã hàng) | `@be/Infrastructure/Excel/ClosedXmlImportTemplateWriter.cs` | Ghi file mẫu nhập Excel (ClosedXML). | Trung bình |
| 51 | Orders (Đơn hàng / Mã hàng) | `@be/Infrastructure/Excel/ClosedXmlOrderLinesParser.cs` | Parse Excel dòng hàng đơn (ma trận size). | Cao |
| 52 | Orders (Đơn hàng / Mã hàng) | `@be/Infrastructure/Excel/ClosedXmlProductParser.cs` | Parse Excel danh sách mã hàng. | Trung bình |
| 53 | Orders (Đơn hàng / Mã hàng) | `@be/Infrastructure/Excel/ClosedXmlSpecWorkbookParser.cs` | Parse workbook định mức (thành phần/NVL/nhóm/bìa). | Cao |
| 54 | Orders (Đơn hàng / Mã hàng) | `@be/Infrastructure/Jobs/OrderMonitorJobs.cs` | JOB-04/05: gắn cờ trễ đơn và dọn thông báo quá hạn. | Thấp |

---

## Warehouse (Kho)

| STT | Module | Đường dẫn file | Chức năng chính (tóm tắt 1 dòng) | Mức độ phức tạp logic |
| ---: | --- | --- | --- | --- |
| 55 | Warehouse (Kho) | `@be/Application/Services/Warehouse/CustomerDeliveryPlanService.cs` | Lịch khách giao NVL theo đơn và số lần giao. | Trung bình |
| 56 | Warehouse (Kho) | `@be/Application/Services/Warehouse/DeliveryCountGuard.cs` | Chặn vượt số lần giao dự kiến; đẩy thiếu sang đơn thiếu NVL. | Trung bình |
| 57 | Warehouse (Kho) | `@be/Application/Services/Warehouse/DeliveryScheduleStore.cs` | Lưu/đọc lịch ngày giao theo loại chủ (PO/đơn hàng). | Trung bình |
| 58 | Warehouse (Kho) | `@be/Application/Services/Warehouse/DispatchNoteService.cs` | Phiếu xuất/giao hàng: lập→xuất→khách nhận; cập nhật đơn DELIVERED. | Cao |
| 59 | Warehouse (Kho) | `@be/Application/Services/Warehouse/FgHandoverService.cs` | Bàn giao thành phẩm QC → kho FINISHED. | Cao |
| 60 | Warehouse (Kho) | `@be/Application/Services/Warehouse/IncomingMaterialService.cs` | Danh sách NVL chờ nhận từ khách cấp / đơn mua. | Trung bình |
| 61 | Warehouse (Kho) | `@be/Application/Services/Warehouse/InventoryService.cs` | Xem tồn 3 kho, thẻ kho, BTP tổ và NXK. | Thấp |
| 62 | Warehouse (Kho) | `@be/Application/Services/Warehouse/MaterialIssueService.cs` | Phiếu cấp NVL/BTP cho tổ; kiểm phân công đủ trước nhận BTP. | Cao |
| 63 | Warehouse (Kho) | `@be/Application/Services/Warehouse/MaterialReceiptAttachmentAccess.cs` | Quyền ảnh chứng từ phiếu nhập NVL. | Thấp |
| 64 | Warehouse (Kho) | `@be/Application/Services/Warehouse/MaterialReceiptService.cs` | Phiếu nhập NVL: lập/xác nhận/trả lại + sinh SHORT_DELIVERY. | Cao |
| 65 | Warehouse (Kho) | `@be/Application/Services/Warehouse/MaterialReturnCapacity.cs` | Tính trần số trả NVL/BTP theo tổ cắt/may. | Trung bình |
| 66 | Warehouse (Kho) | `@be/Application/Services/Warehouse/MaterialReturnService.cs` | Phiếu trả NVL/BTP/phế phẩm về kho. | Cao |
| 67 | Warehouse (Kho) | `@be/Application/Services/Warehouse/MaterialService.cs` | CRUD danh mục NVL (vải luôn tính mét). | Trung bình |
| 68 | Warehouse (Kho) | `@be/Application/Services/Warehouse/MaterialSupplyUpdater.cs` | Cập nhật GoodsStatus/MaterialStatus sau xác nhận nhập. | Trung bình |
| 69 | Warehouse (Kho) | `@be/Application/Services/Warehouse/OrderMaterialService.cs` | Quản lý NVL khách cấp / phải mua của đơn. | Cao |
| 70 | Warehouse (Kho) | `@be/Application/Services/Warehouse/OrderMaterialService.Import.cs` | Import Excel NVL của đơn (gộp cập nhật/thêm). | Cao |
| 71 | Warehouse (Kho) | `@be/Application/Services/Warehouse/ShortageTracker.cs` | Theo dõi NVL còn thiếu theo nguồn PO/khách cấp. | Cao |
| 72 | Warehouse (Kho) | `@be/Application/Services/Warehouse/ShortDeliveryService.cs` | Xử lý đơn thiếu hàng: bù hàng hoặc chốt không bù. | Cao |
| 73 | Warehouse (Kho) | `@be/Application/Services/Warehouse/StockService.cs` | Ghi kho qua usp_Stock_Post; pre-check thiếu hàng cả lô. | Trung bình |
| 74 | Warehouse (Kho) | `@be/Application/Services/Warehouse/StocktakeService.cs` | Phiếu kiểm kê: lập → duyệt điều chỉnh tồn / trả lại. | Cao |
| 75 | Warehouse (Kho) | `@be/Application/Services/Warehouse/WarehouseDocumentExportService.cs` | Xuất Excel phiếu nhập/cấp/xuất kho. | Trung bình |
| 76 | Warehouse (Kho) | `@be/Application/Validators/Warehouse/DispatchNoteDtoValidators.cs` | Validate phiếu xuất/giao hàng. | Trung bình |
| 77 | Warehouse (Kho) | `@be/Application/Validators/Warehouse/FgHandoverDtoValidators.cs` | Validate bàn giao thành phẩm. | Trung bình |
| 78 | Warehouse (Kho) | `@be/Application/Validators/Warehouse/MaterialDtoValidators.cs` | Validate CRUD danh mục NVL. | Trung bình |
| 79 | Warehouse (Kho) | `@be/Application/Validators/Warehouse/MaterialIssueDtoValidators.cs` | Validate phiếu cấp NVL/BTP. | Trung bình |
| 80 | Warehouse (Kho) | `@be/Application/Validators/Warehouse/MaterialReceiptDtoValidators.cs` | Validate phiếu nhập NVL và dòng hàng. | Trung bình |
| 81 | Warehouse (Kho) | `@be/Application/Validators/Warehouse/MaterialReturnDtoValidators.cs` | Validate phiếu trả NVL/BTP. | Trung bình |
| 82 | Warehouse (Kho) | `@be/Application/Validators/Warehouse/OrderMaterialDtoValidators.cs` | Validate NVL của đơn (số cần, khách cấp). | Trung bình |
| 83 | Warehouse (Kho) | `@be/Application/Validators/Warehouse/StockQueryValidators.cs` | Validate query tồn kho / thẻ kho / NXK. | Trung bình |
| 84 | Warehouse (Kho) | `@be/Application/Validators/Warehouse/StocktakeDtoValidators.cs` | Validate phiếu kiểm kê. | Trung bình |
| 85 | Warehouse (Kho) | `@be/Infrastructure/Excel/ClosedXmlOrderMaterialParser.cs` | Parse Excel NVL của đơn. | Cao |
| 86 | Warehouse (Kho) | `@be/Infrastructure/Excel/ClosedXmlWarehouseDocumentWriter.cs` | Dựng trang Excel chứng từ kho in được. | Trung bình |

---

## Purchasing (Mua hàng)

| STT | Module | Đường dẫn file | Chức năng chính (tóm tắt 1 dòng) | Mức độ phức tạp logic |
| ---: | --- | --- | --- | --- |
| 87 | Purchasing (Mua hàng) | `@be/Application/Services/Purchasing/PurchaseAmounts.cs` | Tính mốc trả đủ và trạng thái thanh toán đơn mua. | Thấp |
| 88 | Purchasing (Mua hàng) | `@be/Application/Services/Purchasing/PurchaseDeliveryDate.cs` | Validate ngày dự kiến giao của đơn mua. | Thấp |
| 89 | Purchasing (Mua hàng) | `@be/Application/Services/Purchasing/PurchaseOrderAttachmentAccess.cs` | Quyền xem/thêm/gỡ ảnh chứng từ thanh toán đơn mua. | Thấp |
| 90 | Purchasing (Mua hàng) | `@be/Application/Services/Purchasing/PurchaseOrderFlowService.cs` | Luồng chốt NCC/đặt hàng/hủy và xác nhận thanh toán. | Cao |
| 91 | Purchasing (Mua hàng) | `@be/Application/Services/Purchasing/PurchaseOrderService.cs` | Tạo/xem/sửa đơn mua NVL (kèm lịch giao). | Cao |
| 92 | Purchasing (Mua hàng) | `@be/Application/Services/Purchasing/PurchasePaymentService.cs` | Sửa lần thanh toán đơn mua trong 24 giờ (có lý do). | Trung bình |
| 93 | Purchasing (Mua hàng) | `@be/Application/Services/Purchasing/SupplierService.cs` | CRUD nhà cung cấp (không xóa, chỉ ngừng hợp tác). | Trung bình |
| 94 | Purchasing (Mua hàng) | `@be/Application/Validators/Purchasing/PurchaseFlowDtoValidators.cs` | Validate luồng đặt hàng / thanh toán / NCC. | Trung bình |
| 95 | Purchasing (Mua hàng) | `@be/Application/Validators/Purchasing/PurchaseOrderDtoValidators.cs` | Validate tạo/sửa đơn mua và dòng hàng. | Trung bình |

---

## Production (Sản xuất / May)

| STT | Module | Đường dẫn file | Chức năng chính (tóm tắt 1 dòng) | Mức độ phức tạp logic |
| ---: | --- | --- | --- | --- |
| 96 | Production (Sản xuất / May) | `@be/Application/Services/Production/AllocationRateCheck.cs` | Chặn giao việc nếu công đoạn chưa có đơn giá duyệt. | Thấp |
| 97 | Production (Sản xuất / May) | `@be/Application/Services/Production/DailyWorkAllocationService.cs` | Giao sản lượng theo ngày, xác nhận/thu hồi và chuyển tồn ngày. | Cao |
| 98 | Production (Sản xuất / May) | `@be/Application/Services/Production/DefectiveMaterialRequestHandler.cs` | Xử lý duyệt đơn NVL lỗi: cấp bù hoặc tạo luồng mua thiếu. | Trung bình |
| 99 | Production (Sản xuất / May) | `@be/Application/Services/Production/DefectService.cs` | Ghi/sửa/hủy phiếu lỗi REWORK/SCRAP và phản hồi công nhân. | Cao |
| 100 | Production (Sản xuất / May) | `@be/Application/Services/Production/EndOfOrderService.cs` | Kết thúc đơn tổ may: đối soát lệch sản lượng + chốt KPI. | Cao |
| 101 | Production (Sản xuất / May) | `@be/Application/Services/Production/KpiCalendarService.cs` | Lịch KPI ngày và kiểm BTP khả dụng khi đặt chỉ tiêu. | Cao |
| 102 | Production (Sản xuất / May) | `@be/Application/Services/Production/KpiService.cs` | Lập/sửa/hủy chỉ tiêu KPI tổ may theo kỳ. | Cao |
| 103 | Production (Sản xuất / May) | `@be/Application/Services/Production/OperationAssignmentService.cs` | Phân công công nhân × công đoạn (đóng ValidTo, mở dòng mới). | Cao |
| 104 | Production (Sản xuất / May) | `@be/Application/Services/Production/OperationGroupService.cs` | CRUD nhóm công đoạn (chỉ để nhóm/tìm kiếm). | Trung bình |
| 105 | Production (Sản xuất / May) | `@be/Application/Services/Production/OperationService.cs` | CRUD công đoạn mã hàng + import Excel quy trình. | Cao |
| 106 | Production (Sản xuất / May) | `@be/Application/Services/Production/OrderLineTeamService.cs` | Chia dòng đơn cho tổ may và kiểm cổng tiến độ nhận đơn mới. | Cao |
| 107 | Production (Sản xuất / May) | `@be/Application/Services/Production/OutputCapTransfer.cs` | Chuyển trần sản lượng giữa công nhân khi báo vượt giao. | Trung bình |
| 108 | Production (Sản xuất / May) | `@be/Application/Services/Production/OutputQueryService.cs` | Đọc view sản lượng/Scrap/QC cho lương-KPI-tiến độ. | Trung bình |
| 109 | Production (Sản xuất / May) | `@be/Application/Services/Production/OutputService.cs` | Nhập/sửa sản lượng cuối ca theo trần và hạn sửa. | Cao |
| 110 | Production (Sản xuất / May) | `@be/Application/Services/Production/PieceRateService.cs` | Lập/gửi duyệt/duyệt đơn giá công đoạn (không sửa bản đã duyệt). | Cao |
| 111 | Production (Sản xuất / May) | `@be/Application/Services/Production/ProductionGuard.cs` | Quy tắc dùng chung: tổ, hết ca, khóa tháng, sửa sau hạn. | Trung bình |
| 112 | Production (Sản xuất / May) | `@be/Application/Services/Production/QcBatchService.cs` | Nhập số QC đạt theo đợt; trần theo hàng trên chuyền. | Cao |
| 113 | Production (Sản xuất / May) | `@be/Application/Services/Production/ReplacementCuttingRequestHandler.cs` | Validate đơn cắt bù từ phiếu Scrap khi Quản đốc duyệt. | Trung bình |
| 114 | Production (Sản xuất / May) | `@be/Application/Services/Production/WorkAllocationAutoConfirmService.cs` | Tự xác nhận phiếu giao việc quá hạn chờ công nhân. | Trung bình |
| 115 | Production (Sản xuất / May) | `@be/Application/Services/Production/WorkAllocationService.Batch.cs` | Công nhân xác nhận hàng loạt phiếu giao việc. | Cao |
| 116 | Production (Sản xuất / May) | `@be/Application/Services/Production/WorkAllocationService.cs` | Giao việc công đoạn×màu×size; xác nhận/thu hồi theo trần BTP. | Cao |
| 117 | Production (Sản xuất / May) | `@be/Application/Validators/Production/KpiDtoValidators.cs` | Validate chỉ tiêu KPI và lịch ngày. | Trung bình |
| 118 | Production (Sản xuất / May) | `@be/Application/Validators/Production/ProductionDtoValidators.cs` | Validate công đoạn, đơn giá, sản lượng, QC, lỗi. | Trung bình |
| 119 | Production (Sản xuất / May) | `@be/Application/Validators/Production/WorkAllocationDtoValidators.cs` | Validate giao việc / xác nhận giao việc. | Trung bình |
| 120 | Production (Sản xuất / May) | `@be/Infrastructure/Excel/ClosedXmlOperationParser.cs` | Parse Excel quy trình công nghệ (công đoạn). | Cao |
| 121 | Production (Sản xuất / May) | `@be/Infrastructure/Jobs/WorkAllocationAutoConfirmJob.cs` | Job 5 phút tự xác nhận phiếu giao việc quá hạn. | Thấp |

---

## Hr (Nhân sự)

| STT | Module | Đường dẫn file | Chức năng chính (tóm tắt 1 dòng) | Mức độ phức tạp logic |
| ---: | --- | --- | --- | --- |
| 122 | Hr (Nhân sự) | `@be/Application/Services/Hr/AccountAdminService.cs` | Khóa/mở khóa/đặt lại mật khẩu tài khoản theo phạm vi vai trò. | Trung bình |
| 123 | Hr (Nhân sự) | `@be/Application/Services/Hr/EmployeeImportService.cs` | Xem trước và import Excel nhân viên (hợp lệ/lỗi/trùng). | Cao |
| 124 | Hr (Nhân sự) | `@be/Application/Services/Hr/EmployeeScope.cs` | Phạm vi nhân viên Admin vs Quản đốc được quản lý. | Thấp |
| 125 | Hr (Nhân sự) | `@be/Application/Services/Hr/EmployeeService.cs` | CRUD nhân viên + tạo tài khoản, import từ RJ. | Cao |
| 126 | Hr (Nhân sự) | `@be/Application/Services/Hr/GradeDefectCounter.cs` | Đếm phiếu lỗi gần đây phục vụ đề xuất bậc. | Thấp |
| 127 | Hr (Nhân sự) | `@be/Application/Services/Hr/GradeMetricsReader.cs` | Tổng hợp công/sản lượng/lỗi để xét bậc trong kỳ. | Trung bình |
| 128 | Hr (Nhân sự) | `@be/Application/Services/Hr/GradeService.cs` | Danh mục bậc, đề xuất và quyết định áp dụng bậc thợ. | Cao |
| 129 | Hr (Nhân sự) | `@be/Application/Services/Hr/GradeSuggestionService.cs` | Gợi ý bậc bằng AI (ẩn danh) và xác nhận thành đề xuất. | Cao |
| 130 | Hr (Nhân sự) | `@be/Application/Services/Hr/HrLookupService.cs` | Tra cứu tổ/thành viên cho các luồng khác. | Thấp |
| 131 | Hr (Nhân sự) | `@be/Application/Services/Hr/ProfileService.cs` | Đổi mật khẩu và cập nhật hồ sơ cá nhân. | Trung bình |
| 132 | Hr (Nhân sự) | `@be/Application/Services/Hr/TeamService.cs` | Quản lý tổ, chuyển tổ, bổ nhiệm tổ trưởng/phó. | Cao |
| 133 | Hr (Nhân sự) | `@be/Application/Validators/Hr/GradeDtoValidators.cs` | Validate đề xuất/quyết định bậc và tham số AI. | Trung bình |
| 134 | Hr (Nhân sự) | `@be/Application/Validators/Hr/HrDtoValidators.cs` | Validate nhân viên/tổ/tài khoản HR. | Trung bình |
| 135 | Hr (Nhân sự) | `@be/Infrastructure/Ai/GeminiGradeAdvisor.cs` | Gọi Gemini generateContent trả JSON gợi ý bậc thợ. | Trung bình |
| 136 | Hr (Nhân sự) | `@be/Infrastructure/Ai/GeminiGradePrompt.cs` | Dựng prompt ẩn danh gửi Gemini xét bậc. | Trung bình |
| 137 | Hr (Nhân sự) | `@be/Infrastructure/Excel/ClosedXmlEmployeeParser.cs` | Parse Excel import nhân viên. | Trung bình |

---

## Finance (Tài chính)

| STT | Module | Đường dẫn file | Chức năng chính (tóm tắt 1 dòng) | Mức độ phức tạp logic |
| ---: | --- | --- | --- | --- |
| 138 | Finance (Tài chính) | `@be/Application/Services/Finance/AccountingReportService.cs` | Báo cáo kế toán tháng: công-tăng ca, suất ăn, sổ xe. | Trung bình |
| 139 | Finance (Tài chính) | `@be/Application/Services/Finance/CostSummaryService.cs` | Tổng hợp chi phí tháng + lương đã duyệt. | Trung bình |
| 140 | Finance (Tài chính) | `@be/Application/Services/Finance/FinanceService.cs` | Doanh thu và tính hòa vốn/thặng dư theo kỳ. | Cao |
| 141 | Finance (Tài chính) | `@be/Application/Services/Finance/MonthlyCostService.cs` | Import/quản lý chi phí tháng (FIXED/VARIABLE) và đối soát bếp. | Cao |
| 142 | Finance (Tài chính) | `@be/Application/Services/Finance/OrderPaymentService.cs` | Chốt giá trị đơn và ghi nhận thanh toán nhiều lần. | Cao |
| 143 | Finance (Tài chính) | `@be/Application/Services/Finance/PayrollService.cs` | Import bảng lương, duyệt/mở lại và khóa tháng kế toán. | Cao |
| 144 | Finance (Tài chính) | `@be/Application/Services/Finance/WorkerPayService.cs` | Công nhân xem sản lượng + lương tạm tính của mình. | Trung bình |
| 145 | Finance (Tài chính) | `@be/Application/Validators/Finance/FinanceDtoValidators.cs` | Validate đầu vào doanh thu / hòa vốn. | Trung bình |
| 146 | Finance (Tài chính) | `@be/Application/Validators/Finance/OrderPaymentDtoValidators.cs` | Validate chốt giá trị và lần thanh toán đơn. | Trung bình |
| 147 | Finance (Tài chính) | `@be/Application/Validators/Finance/PayrollDtoValidators.cs` | Validate import/duyệt/từ chối bảng lương. | Trung bình |
| 148 | Finance (Tài chính) | `@be/Application/Validators/Finance/WorkerPayDtoValidators.cs` | Validate khoảng ngày xem lương tạm tính. | Thấp |
| 149 | Finance (Tài chính) | `@be/Infrastructure/Excel/ClosedXmlAccountingReportWriter.cs` | Xuất Excel báo cáo kế toán 3 sheet. | Trung bình |
| 150 | Finance (Tài chính) | `@be/Infrastructure/Excel/ClosedXmlCostSummaryWriter.cs` | Xuất Excel tổng hợp chi phí. | Trung bình |
| 151 | Finance (Tài chính) | `@be/Infrastructure/Excel/ClosedXmlFinanceWriter.cs` | Xuất Excel doanh thu / hòa vốn. | Trung bình |
| 152 | Finance (Tài chính) | `@be/Infrastructure/Excel/ClosedXmlMonthlyCostParser.cs` | Parse Excel chi phí tháng. | Trung bình |
| 153 | Finance (Tài chính) | `@be/Infrastructure/Excel/ClosedXmlMonthlyCostWriter.cs` | Xuất mẫu Excel chi phí tháng + hướng dẫn. | Thấp |
| 154 | Finance (Tài chính) | `@be/Infrastructure/Excel/ClosedXmlPayrollParser.cs` | Parse bảng thanh toán lương thật của xưởng. | Cao |
| 155 | Finance (Tài chính) | `@be/Infrastructure/Excel/ClosedXmlPayrollWriter.cs` | Xuất Excel dữ liệu tính lương cho Kế toán. | Thấp |

---

## Requests (Đơn xin)

| STT | Module | Đường dẫn file | Chức năng chính (tóm tắt 1 dòng) | Mức độ phức tạp logic |
| ---: | --- | --- | --- | --- |
| 156 | Requests (Đơn xin) | `@be/Application/Services/Requests/MachineRequestHandlers.cs` | Handler đơn xin cơ điện + giới hạn số lượng đang mở. | Thấp |
| 157 | Requests (Đơn xin) | `@be/Application/Services/Requests/OtherRequestHandler.cs` | Handler đơn Khác: chỉ nội dung, không dòng chi tiết. | Thấp |
| 158 | Requests (Đơn xin) | `@be/Application/Services/Requests/RequestAccess.cs` | Phạm vi tạo/xem/xử lý đơn theo vai trò và loại. | Trung bình |
| 159 | Requests (Đơn xin) | `@be/Application/Services/Requests/RequestAttachmentAccess.cs` | Quyền đính kèm đơn xin khi còn chờ. | Thấp |
| 160 | Requests (Đơn xin) | `@be/Application/Services/Requests/RequestQueryService.cs` | Truy vấn hộp thư đơn xin theo bộ lọc trạng thái/loại. | Trung bình |
| 161 | Requests (Đơn xin) | `@be/Application/Services/Requests/RequestService.cs` | Tạo/duyệt/từ chối/hoàn thành đơn xin đa loại. | Cao |
| 162 | Requests (Đơn xin) | `@be/Application/Services/Requests/RequestTypeHandlers.cs` | Registry handler theo loại đơn + DefaultHandler. | Thấp |
| 163 | Requests (Đơn xin) | `@be/Application/Services/Requests/RequisitionRequestHandlers.cs` | Handler Xin NVL/Xin BTP: validate dòng và giới hạn. | Cao |
| 164 | Requests (Đơn xin) | `@be/Application/Validators/Requests/RequestDtoValidators.cs` | Validate tạo/duyệt đơn xin và dòng chi tiết. | Trung bình |

---

## Vehicle (Xe / Chuyến)

| STT | Module | Đường dẫn file | Chức năng chính (tóm tắt 1 dòng) | Mức độ phức tạp logic |
| ---: | --- | --- | --- | --- |
| 165 | Vehicle (Xe / Chuyến) | `@be/Application/Services/Vehicle/VehicleService.cs` | Quản lý xe và sổ chuyến; cảnh báo lệch km/xăng vượt định mức. | Cao |
| 166 | Vehicle (Xe / Chuyến) | `@be/Application/Validators/Vehicle/VehicleDtoValidators.cs` | Validate xe và chuyến xe (km, xăng). | Trung bình |

---

## Shared (Dùng chung)

| STT | Module | Đường dẫn file | Chức năng chính (tóm tắt 1 dòng) | Mức độ phức tạp logic |
| ---: | --- | --- | --- | --- |
| 167 | Shared (Dùng chung) | `@be/Application/Services/Shared/AttachmentCleanupService.cs` | Job dọn tệp chứng từ đã gỡ hoặc mồ côi sau retention. | Trung bình |
| 168 | Shared (Dùng chung) | `@be/Application/Services/Shared/AttachmentService.cs` | Upload/list/open/remove chứng từ theo owner. | Trung bình |
| 169 | Shared (Dùng chung) | `@be/Application/Services/Shared/AuditLogQueryService.cs` | Truy vấn nhật ký AuditLog theo bộ lọc. | Thấp |
| 170 | Shared (Dùng chung) | `@be/Application/Services/Shared/LookupService.cs` | Lookup dùng chung: đơn mở, dòng đơn, panel, tổ, NVL. | Trung bình |
| 171 | Shared (Dùng chung) | `@be/Application/Services/Shared/NotificationCenterService.cs` | Hộp thư thông báo: đọc/đánh dấu đã đọc của chính mình. | Trung bình |
| 172 | Shared (Dùng chung) | `@be/Application/Services/Shared/PartnerContactChecker.cs` | Chặn SĐT/email trùng chéo khách hàng và NCC. | Trung bình |
| 173 | Shared (Dùng chung) | `@be/Application/Validators/Shared/AnnouncementDtoValidators.cs` | Validate gửi thông báo hệ thống. | Thấp |
| 174 | Shared (Dùng chung) | `@be/Application/Validators/Shared/PageQueryValidatorBase.cs` | Base validate phân trang/sắp xếp query. | Thấp |

---

## Shared (Excel / Hạ tầng)

| STT | Module | Đường dẫn file | Chức năng chính (tóm tắt 1 dòng) | Mức độ phức tạp logic |
| ---: | --- | --- | --- | --- |
| 175 | Shared (Excel / Hạ tầng) | `@be/Infrastructure/Audit/AuditLogService.cs` | Ghi và đọc lịch sử AuditLog. | Thấp |
| 176 | Shared (Excel / Hạ tầng) | `@be/Infrastructure/Common/DocumentCodeGenerator.cs` | Sinh mã chứng từ tuần tự. | Thấp |
| 177 | Shared (Excel / Hạ tầng) | `@be/Infrastructure/Common/SqlAppLock.cs` | Bọc sp_getapplock cho khóa nghiệp vụ. | Thấp |
| 178 | Shared (Excel / Hạ tầng) | `@be/Infrastructure/Common/VnClock.cs` | Đồng hồ giờ VN (+7) khớp fn_VnNow. | Thấp |
| 179 | Shared (Excel / Hạ tầng) | `@be/Infrastructure/Excel/ExcelCells.cs` | Helper đọc ô Excel an toàn cho importer. | Thấp |
| 180 | Shared (Excel / Hạ tầng) | `@be/Infrastructure/Excel/XlsxWorkbookLoader.cs` | Làm sạch/mở .xlsx hỏng hình/công thức trước khi parse. | Trung bình |
| 181 | Shared (Excel / Hạ tầng) | `@be/Infrastructure/Jobs/DailyScheduledJob.cs` | Khung BackgroundService chạy đúng 1 lần/ngày theo giờ setting. | Trung bình |
| 182 | Shared (Excel / Hạ tầng) | `@be/Infrastructure/Notifications/NotificationService.cs` | Phát thông báo (Admin/QD nhận bản sao theo quy tắc). | Trung bình |
| 183 | Shared (Excel / Hạ tầng) | `@be/Infrastructure/Security/BCryptPasswordHasher.cs` | Hash/verify mật khẩu bằng BCrypt. | Thấp |
| 184 | Shared (Excel / Hạ tầng) | `@be/Infrastructure/Security/TemporaryPasswordGenerator.cs` | Sinh mật khẩu tạm 6 số an toàn. | Thấp |
| 185 | Shared (Excel / Hạ tầng) | `@be/Infrastructure/Security/TokenService.cs` | Tạo/đọc JWT access + refresh token. | Trung bình |
| 186 | Shared (Excel / Hạ tầng) | `@be/Infrastructure/Security/UsernameGenerator.cs` | Sinh username từ họ tên Việt. | Thấp |

---

## Common (Quy tắc dùng chung)

| STT | Module | Đường dẫn file | Chức năng chính (tóm tắt 1 dòng) | Mức độ phức tạp logic |
| ---: | --- | --- | --- | --- |
| 187 | Common (Quy tắc dùng chung) | `@be/Application/Common/AuditText.cs` | Dựng câu mô tả tiếng Việt cho dòng AuditLog lúc ghi. | Trung bình |
| 188 | Common (Quy tắc dùng chung) | `@be/Application/Common/ComponentLabels.cs` | Ghép nhãn hiển thị dòng định mức (chi tiết × vải). | Thấp |
| 189 | Common (Quy tắc dùng chung) | `@be/Application/Common/DocumentDateRules.cs` | Quy tắc kiểm ngày chứng từ kho và ngày dự kiến giao hàng. | Trung bình |
| 190 | Common (Quy tắc dùng chung) | `@be/Application/Common/EmailRule.cs` | Quy tắc validate email ASCII dạng ten@mien.com. | Thấp |
| 191 | Common (Quy tắc dùng chung) | `@be/Application/Common/Fail.cs` | Factory tạo Failure nghiệp vụ dùng chung cho service. | Thấp |
| 192 | Common (Quy tắc dùng chung) | `@be/Application/Common/FileSignatures.cs` | Nhận diện ảnh bằng magic bytes, không tin Content-Type client. | Thấp |
| 193 | Common (Quy tắc dùng chung) | `@be/Application/Common/PagedResult.cs` | Kiểu kết quả phân trang khớp contract FE. | Thấp |
| 194 | Common (Quy tắc dùng chung) | `@be/Application/Common/PayslipLayout.cs` | Ánh xạ cột/cách hiển thị phiếu lương Excel. | Trung bình |
| 195 | Common (Quy tắc dùng chung) | `@be/Application/Common/PersonNameRule.cs` | Quy tắc họ tên chỉ chữ cái (kể cả tiếng Việt) và khoảng trắng. | Thấp |
| 196 | Common (Quy tắc dùng chung) | `@be/Application/Common/PhoneRule.cs` | Quy tắc SĐT 10 số / +84 dùng chung. | Thấp |
| 197 | Common (Quy tắc dùng chung) | `@be/Application/Common/RequestTypePolicies.cs` | Bảng chính sách ai tạo / ai xử lý theo loại đơn xin. | Trung bình |
| 198 | Common (Quy tắc dùng chung) | `@be/Application/Common/Result.cs` | Kiểu Result thành công/thất bại cho lớp Application. | Thấp |
| 199 | Common (Quy tắc dùng chung) | `@be/Application/Common/SampleImageKey.cs` | Chuẩn khóa lưu trữ ảnh mẫu mã hàng. | Thấp |
| 200 | Common (Quy tắc dùng chung) | `@be/Application/Common/SewingTeamScope.cs` | Giới hạn phạm vi đơn/mã hàng theo tổ may của người dùng. | Thấp |
| 201 | Common (Quy tắc dùng chung) | `@be/Application/Common/SizeOrder.cs` | Sắp xếp mã size theo thứ tự chuẩn hệ thống. | Thấp |
| 202 | Common (Quy tắc dùng chung) | `@be/Application/Common/TeamPieceQueries.cs` | Truy vấn số bộ BTP tổ may đã nhận (trần giao việc). | Thấp |
| 203 | Common (Quy tắc dùng chung) | `@be/Application/Common/TextLengthRule.cs` | Đo độ dài sau Trim để khớp MaximumLength khi lưu. | Thấp |
| 204 | Common (Quy tắc dùng chung) | `@be/Application/Common/UnitOfWorkExtensions.cs` | Bọc nghiệp vụ nhiều bước trong một transaction. | Thấp |
| 205 | Common (Quy tắc dùng chung) | `@be/Application/Common/VehiclePlate.cs` | Validate định dạng biển số xe xưởng. | Thấp |
| 206 | Common (Quy tắc dùng chung) | `@be/Application/Common/WarehouseFailures.cs` | Các Failure hay dùng của luồng kho/cắt. | Thấp |
| 207 | Common (Quy tắc dùng chung) | `@be/Application/Common/WidthRule.cs` | Quy tắc khổ vải/bìa (mét, tối đa 3.5m, 2 số lẻ). | Thấp |

