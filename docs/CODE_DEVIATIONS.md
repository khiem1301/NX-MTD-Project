# NX-MTD – Khác biệt giữa code và tài liệu

> Ghi theo Coding Rules mục 8. Chỉ thêm mục mới vào cuối file. Trạng thái: `Mở` · `Đã sửa tài liệu` · `Bỏ`.
> Tài liệu gốc: Report-3 SRS (16/10), NX-MTD_AI_Context.md (19/10), NX-MTD_Database.sql (65 bảng), NX-MTD_Tong_hop_Use_Case.md (20/10).
> Repo này gộp cả backend và frontend (`nxmtd-web`) trong một git repo nên dùng chung một file ở gốc repo thay vì mỗi repo một file như mục 8.1 giả định.

## Bảng tổng hợp

| Mã | Ngày | Người | UC / bảng | Tóm tắt | Trạng thái |
|---|---|---|---|---|---|
| DEV-MI-09-1 | 21/10/2026 | Minh (AI) | UC31, `Payroll.ImportedFileUrl`, `PayslipItem` | Tự chọn cấu trúc cột file Excel nhập bảng lương (SRS chưa chốt mẫu); không lưu lại file đã nhập | Mở |
| DEV-MI-09-2 | 21/10/2026 | Minh (AI) | UC32, `Payroll.Status`, `AccountingPeriod.Status` | "Mở lại" bảng lương đã duyệt về Nháp thì mở khóa luôn tháng kế toán | Mở |
| DEV-MI-09-3 | 21/10/2026 | Minh (AI) | UC31/UC32, GBR-15, bảng `AuditLog` | Thao tác bảng lương (nhập/gửi duyệt/duyệt/từ chối/mở lại) chưa ghi `AuditLog` | Bỏ |
| DEV-MI-04-1 | 21/10/2026 | Minh (AI) | UC30/UC62, `TeamMealConfirmation.RemindedAt` | JOB-02 không đẩy thông báo thật, chỉ ghi `RemindedAt` | Bỏ |
| DEV-MI-03-1 | 21/10/2026 | Minh (AI) | UC30, `TeamMealConfirmation` | Màn tổ trưởng là web nhập số tổng, không phải checklist từng người trên mobile | Mở |
| DEV-MI-09-4 | 21/10/2026 | Minh (AI) | UC31, `IExcelPayrollParser`, `PayslipItem` | Nhận được file bảng lương thật của xưởng (~40 cột, nhiều sheet) — khác hẳn mẫu 6 cột đã chọn ở DEV-MI-09-1; chờ người dùng chốt cách gộp cột trước khi sửa parser | Bỏ |
| DEV-MI-16-1 | 21/10/2026 | Minh (AI) | UC19 (KPI tổ may), `KpiTarget` | Không chặn lập/sửa KPI theo tồn kho BTP (bỏ rule "block nếu thiếu BTP" dù một số nguồn SRS có nhắc) | Mở |
| DEV-MI-16-2 | 21/10/2026 | Minh (AI) | `SystemSetting`, `KPI_AUTO_CLOSE_TIME` | Khóa giờ chạy KpiAutoCloseJob chưa được seed trong `NX-MTD_Database.sql` (bảng `SystemSetting` do Khiêm sở hữu); job chạy bằng giá trị mặc định 00:05 trong code | Mở |
| DEV-MI-16-3 | 21/10/2026 | Minh (AI) | UC19 (KPI tổ may), GBR-15, bảng `AuditLog` | `KpiService` (Create/Update/Cancel/CloseExpiredAsync) chưa ghi `AuditLog` — cùng một khoảng trống đã ghi ở DEV-MI-09-3/DEV-MI-04-1 (chưa có `IAuditLogService` dùng chung), không lặp lại toàn bộ lý do ở đây | Bỏ |
| DEV-MI-12-1 | 21/10/2026 | Minh (AI) | UC12, bảng `MonthlyCost` | "Có lịch sử" khi import lại cùng tháng không được schema hỗ trợ; import lại = xóa hẳn dòng cũ của tháng rồi ghi lại, chỉ còn `ImportedAt` làm dấu vết yếu | Mở |
| DEV-MI-12-2 | 21/10/2026 | Minh (AI) | UC12/UC30, `MonthlyCost` (MEALS), `DailyMeal.TotalAmount` | Đối soát MEALS với Σ DailyMeal.TotalAmount chỉ là cảnh báo mềm (so khác tuyệt đối, không có ngưỡng %), không chặn import | Mở |
| DEV-MI-12-3 | 21/10/2026 | Minh (AI) | UC12, `IExcelMonthlyCostParser`, `ClosedXmlMonthlyCostParser`/`Writer` | Tự chọn cấu trúc cột file Excel chi phí tháng + file mẫu (SRS chỉ nói "tải file mẫu", chưa chốt cột); file đã nhập không được lưu lại (chưa có `IAttachmentService`) | Mở |
| DEV-MI-12-4 | 21/10/2026 | Minh (AI) | UC12, GBR-15, bảng `AuditLog` | `MonthlyCostService.ImportAsync` chưa ghi `AuditLog` — cùng khoảng trống đã ghi ở DEV-MI-09-3/DEV-MI-04-1/DEV-MI-16-3, không lặp lại lý do ở đây | Bỏ |
| DEV-MI-18-1 | 21/10/2026 | Minh (AI) | MI-18 "KPI theo ngày" (UC19), task sheet | Mô tả task MI-18 bị bỏ trống trong bảng phân công; tự diễn giải thành "lộ dữ liệu `vw_KpiDailyResult` theo từng `KpiTargetId`" dựa trên tên task + phụ thuộc MI-16, đã xác nhận với người yêu cầu trước khi code | Mở |
| DEV-MI-18-2 | 21/10/2026 | Minh (AI) | `KpiTarget.Status`/`.Result`, `KpiTargetDto`, Coding Rules mục 2.2 | `KpiTarget.Status`/`.Result` là cột `string` thường, API từng trả nguyên văn UPPER_SNAKE thay vì PascalCase như Coding Rules mục 2.2 yêu cầu; **đã sửa cùng ngày** (xem chi tiết) nên không còn là khác biệt | Bỏ |
| DEV-MI-13-1 | 21/10/2026 | Minh (AI) | UC11, `BreakEvenResult.AllocationBasis`, `OutputRecord`, `DailyTimesheet` | Chốt "chưa chốt" ở AI Context mục 12 (AllocationBasis) bằng cách cài đặt cả hai công thức, Admin chọn mỗi lần chạy; công thức LABOUR_HOURS tự đặt ra cách quy đổi giờ công theo mã hàng vì schema không có cột "giờ công theo mã hàng" | Mở |
| DEV-MI-13-2 | 21/10/2026 | Minh (AI) | UC27/UC11, GBR-15, bảng `AuditLog` | `FinanceService` (xuất Excel doanh thu, chạy tính hòa vốn) chưa ghi `AuditLog` — cùng khoảng trống đã ghi ở DEV-MI-09-3/DEV-MI-04-1/DEV-MI-16-3/DEV-MI-12-4 | Bỏ |
| DEV-MI-19-1 | 21/10/2026 | Minh (AI) | UC12 "Tổng hợp chi phí", `CostSummaryService` | Lương chỉ cộng vào tổng khi `Payroll.Status = APPROVED`; thiếu bảng lương đã duyệt không chặn cả màn hình (khác GBR-17/UC11) mà chỉ hiện `payrollTotal = null` | Mở |
| DEV-MI-19-2 | 21/10/2026 | Minh (AI) | UC36 "Báo cáo kế toán", bảng `Vehicle`/`VehicleTrip` | Báo cáo sổ xe gộp theo xe (không theo từng chuyến); km/xăng chỉ tính chuyến `COMPLETED`, nhưng đếm cảnh báo `KmGapWarning`/`FuelOverNormWarning` trên mọi chuyến trong kỳ kể cả `STARTED` | Mở |
| DEV-MI-19-3 | 21/10/2026 | Minh (AI) | bảng `VehicleTrip` (Thảo sở hữu), `Domain/Constants/VehicleCodes.cs` | Thêm hằng số `VehicleTripStatus` (STARTED/COMPLETED) vì chưa ai khai trước MI-19 — chỉ đọc, không đổi schema/entity của Thảo | Mở |
| DEV-MI-19-4 | 21/10/2026 | Minh (AI) | UC12/UC36, GBR-15, bảng `AuditLog` | `CostSummaryService`/`AccountingReportService` không ghi `AuditLog` — cả hai chỉ đọc (không ghi dữ liệu nghiệp vụ) nên không phát sinh khoảng trống mới, ghi chú cùng chuỗi DEV-MI-09-3/DEV-MI-04-1/DEV-MI-16-3/DEV-MI-12-4/DEV-MI-13-2 để dễ tra cứu | Mở |
| DEV-MI-09-5 | 21/10/2026 | Minh (AI) | UC31/UC32, `Payroll.ImportedFileUrl`, bảng `Attachment` | File Excel Kế toán nhập được lưu nguyên bản vào thư mục cục bộ (không dùng `IAttachmentService`/bảng `Attachment`); giám đốc tải qua endpoint có phân quyền | Mở |
| DEV-MI-09-6 | 21/10/2026 | Minh (AI) | UC31/UC06, `PayslipItem`, `Employee.AttendanceCode` | Import theo file "Bảng thanh toán lương" thật (một sheet mỗi tổ): tra theo mã chấm công, danh mục cột đặt trong code, bậc thợ lưu thành dòng `INFO` dạng chữ, số tổng không lưu, nhân 1000 theo "ĐVT" | Mở |
| DEV-MI-07-1 | 21/10/2026 | Minh (AI) | UC14/UC23/UC34, `Supplier`, `PurchaseOrder`, `Attachment` | Chốt đặt hàng (đơn giá > 0, NCC ACTIVE, khóa số lượng), hủy = REJECTED, ảnh chứng từ không bắt buộc, trả lại → Quản đốc sửa đơn giá → gửi lại, thông báo theo vai trò | Mở |
| DEV-KI-00-1 | 21/10/2026 | Minh (AI) làm hộ Kiều | UC25/UC26/UC52/UC55/UC56/UC57/UC22 | Minh làm BE các task KI-01/03/04/06/08/10/11; chưa làm FE của Kiều, KI-07, KI-09 | Mở |
| DEV-KI-04-1 | 21/10/2026 | Minh (AI) làm hộ Kiều | UC55, `vw_WorkerOutputCap` | Trần sản lượng cấp tổ (BTP đã nhận) khi công nhân chưa có phiếu giao BTP (TH-19 chưa làm) | Mở |
| DEV-KI-06-1 | 21/10/2026 | Minh (AI) làm hộ Kiều | UC57, `QcBatch` | "Hàng trên chuyền" = BTP nhận − QC đạt − Scrap; hết ca lấy từ SystemSetting | Mở |
| DEV-KI-08-1 | 21/10/2026 | Minh (AI) làm hộ Kiều | UC56, `Request` | Phiếu Scrap chưa tự sinh đơn cắt bù (KI-09 chưa làm) | Mở |
| DEV-KI-11-1 | 21/10/2026 | Minh (AI) làm hộ Kiều | UC22, MSG20 | Cửa sổ sửa áp theo từng thao tác; Quản đốc sửa sau hạn kèm lý do + AuditLog, không có màn riêng | Mở |
| DEV-MI-10-1 | 21/10/2026 | Minh (AI) | UC59 | Chi tiết ngày đọc OutputRecord/DefectRecord trực tiếp (view không có mức công đoạn) | Mở |
| DEV-MI-17-1 | 21/10/2026 | Minh (AI) | UC19/UC21, `KpiTarget` | % đạt tính trên cả kỳ; Admin chỉ xem KPI | Mở |
| DEV-TH-12-1 | 21/10/2026 | Thái | `PurchaseOrder`, `PurchaseOrderLine`, `MaterialReceipt`, `MaterialReceiptLine` | Đơn mua NVL không bắt buộc gắn đơn hàng (mua dùng chung kim, chỉ...) | Mở |
| DEV-TH-12-2 | 21/10/2026 | Thái | `Material` | Quản đốc nhập tên NVL mới khi tạo đơn mua, NVL tự vào danh mục | Mở |
| DEV-KH-07-1 | 22/10/2026 | Khiêm | `Attachment.FileUrl` | Lưu khóa lưu trữ, file ở ổ đĩa (dev), tải qua API có kiểm quyền | Mở |
| DEV-KH-08-1 | 23/10/2026 | Khiêm | UC10, `Product` | Ảnh mẫu mã hàng nhập đường dẫn, chưa upload | Mở |
| DEV-KH-10-1 | 24/10/2026 | Khiêm | UC18, `Employee` | Chuyển tổ chỉ cho ngày hiệu lực ≤ hôm nay | Mở |
| DEV-KH-10-2 | 24/10/2026 | Khiêm | UC16, UC18 | Bổ nhiệm tổ trưởng / tổ phó hạ người cũ về Công nhân; Quản đốc đổi vai trò được qua bổ nhiệm | Mở |
| DEV-KH-06-1 | 22/10/2026 | Khiêm | UC07, `Request` | Tổ trưởng / tổ phó tổ may cũng tạo được đơn xin NVL | Mở |
| DEV-KH-06-2 | 22/10/2026 | Khiêm | UC07, UC08, R1 | Tổ phó luôn có quyền như tổ trưởng (chưa xét tổ trưởng vắng) | Mở |
| DEV-MI-09-7 | 21/10/2026 | Minh (AI) | UC31/UC12/UC29, `ClosedXmlPayrollParser`, `XlsxWorkbookLoader` | File Excel thật có logo ảnh hỏng và công thức tham chiếu file khác: gói được làm sạch (bỏ hình, bỏ công thức, giữ giá trị lưu sẵn) trước khi đọc; sheet có cột SLĐ nhưng không có Thực lĩnh bị bỏ qua thay vì làm lỗi cả file | Mở |
| DEV-KI-03-2 | 22/10/2026 | Minh (AI) | UC52/UC56, `OperationAssignmentsController` | Thêm endpoint `GET team-leader/operation-assignments/operations?orderId=` trả công đoạn may đang dùng của các mã hàng trong đơn (không kèm đơn giá) để tổ trưởng chọn khi phân công và ghi hàng hỏng; sheet KI-03 / KI-08 không nêu cách lấy danh sách này | Mở |
| DEV-KI-01-2 | 22/10/2026 | Minh (AI) | UC25, `Operation`, `ClosedXmlOperationParser` | Công đoạn của mã hàng nhập từ file Excel quy trình công nghệ (sheet "BƯỚC CÔNG VIỆC"), thay cho thêm tay từng công đoạn: mã tự sinh từ STT (OP + STT), bỏ dòng "Dự phòng", không đọc cột TGQĐ (cột TIỀN: xem DEV-KI-01-4), không có cột công đoạn cuối nên chọn khi nhập | Mở |
| DEV-KI-01-3 | 22/10/2026 | Minh (AI) | UC25/UC26, `OperationDto.NextRate`, màn "Công đoạn và đơn giá" | Gộp màn "Công đoạn may" và "Đơn giá công đoạn" của Kế toán làm một: đơn giá đặt ngay trên từng công đoạn, `OperationDto` thêm `NextRate` (đơn giá mới nhất chưa thành hiện hành); màn riêng chỉ còn lại cho Admin duyệt | Mở |
| DEV-KI-01-4 | 22/10/2026 | Minh (AI) | UC25/UC26, `PieceRate`, `OperationService.ImportAsync`, `PieceRateService.ApproveManyAsync` | Nhập công đoạn từ Excel đọc luôn cột TIỀN thành đơn giá công đoạn (Nháp, hoặc Chờ duyệt nếu gửi duyệt luôn) theo ngày hiệu lực Kế toán chọn; thêm Admin duyệt nhiều đơn giá một lần | Mở |
| DEV-MI-20-1 | 22/10/2026 | Minh (AI) | MI-05, MI-12, MI-13, MI-09, `XlsxWorkbookLoader`, `FinanceService` | Sửa lỗi phát hiện khi chạy thử toàn luồng trên DB riêng: thiếu màn FE chấm công hộ; gán mã chấm công phải gõ tay mã nhân viên; file mẫu chi phí tháng có dòng ví dụ bị nhập nhầm; tính hòa vốn hai lần trong cùng một giây báo 409; file không phải Excel báo lỗi tiếng Anh | Đã sửa |
| DEV-KI-09-1 | 22/10/2026 | Minh (AI) | UC56, UC07 (cắt bù), `ReplacementCuttingRequestHandler`, `DefectDto` | Làm đơn cắt bù (KI-09): bộ kiểm đơn REPLACEMENT_CUTTING gắn phiếu hàng hỏng Scrap của tổ mình, mỗi chi tiết một dòng, tối đa một đơn mỗi phiếu; phiếu hàng hỏng hiện đơn cắt bù; Quản đốc hoàn thành tay vì chưa có lệnh cắt bù tự hoàn thành | Mở |
| DEV-KI-14-1 | 22/10/2026 | Minh (AI) | UC55, UC56, UC57, màn xưởng | Làm gọn màn xưởng cho điện thoại (KI-14): lưới sản lượng dạng thẻ, ẩn cột phụ trên màn nhỏ, hộp thoại co theo màn hình; thêm dòng ngoài phân công vào lưới sản lượng (GBR-20) | Mở |
| DEV-MI-20-2 | 22/10/2026 | Minh (AI) | `SQL/CreateAndSeed.sql`, `ValidationFilter` | Sửa script (Khiêm, 21/10) thiếu thủ tục `usp_Attendance_ApplyAdjustment` nên chấm công hộ báo 500 trên DB dựng mới; `ValidationFilter` vỡ (500) khi rule mức đối tượng không có tên thuộc tính | Đã sửa |
| DEV-MI-20-3 | 22/10/2026 | Minh (AI) | `SQL/`, Coding Rules 3.12, AI Context | Gộp mọi script SQL thành MỘT file `SQL/NX-MTD_Database.sql` (schema + TH-12 + tài khoản mẫu + thủ tục chấm công hộ + dữ liệu demo mục 19); xóa `CreateAndSeed.sql`, `SeedAuth.sql`, thư mục `Migrations/`, `DemoData_KH-06-10_purchase-orders.sql` | Đã sửa |
| DEV-ALL-1 | 23/10/2026 | Claude (AI) | `MaterialReceiptService`, `ShortDeliveryService`, `MaterialSupplyUpdater` | Nhập NVL từ NCC theo đơn mua; nhận thiếu tự sinh đơn SHORT_DELIVERY, Quản đốc chọn giao bù / không bù; phiếu bổ sung; cập nhật GoodsStatus đơn mua và MaterialStatus đơn hàng | Đã làm |
| DEV-ALL-2 | 23/10/2026 | Claude (AI) | `FgHandoverService`, `DispatchNoteService` | Bàn giao thành phẩm QA/QC → Thủ kho; phiếu xuất hàng Quản đốc → Thủ kho → Kế toán, đơn hàng tự đổi trạng thái giao | Đã làm |
| DEV-ALL-3 | 23/10/2026 | Claude (AI) | `WorkOrderService.CreateReplacementAsync`, `CutHandoverService`, `MaterialReturnService`, `WorkerPieceMovementService` | Lệnh cắt bù từ đơn cắt bù đã duyệt, tổ cắt trả vải thừa, giao / thu hồi BTP với công nhân | Đã làm |
| DEV-ALL-4 | 23/10/2026 | Claude (AI) | `StocktakeService`, `VehicleService` | Kiểm kê kho và sổ xe + nhiên liệu | Đã làm |
| DEV-ALL-5 | 23/10/2026 | Claude (AI) | Thông báo, bậc thợ, trạng thái đơn, dashboard, AuditLog, import nhân viên, JOB-04 / JOB-05 | Chuông thông báo + thông báo nhanh, bậc thợ, chuyển đơn sang sản xuất, dashboard tiến độ đơn, nhật ký thay đổi, import nhân viên Excel, hai job nền | Đã làm |
| DEV-ALL-6 | 23/10/2026 | Claude (AI) | `WarehouseDocumentExportService`, `ClosedXmlWarehouseDocumentWriter` | In / xuất Excel từng phiếu nhập, phiếu cấp, phiếu xuất hàng (TH-16) | Đã làm |
| DEV-ALL-7 | 23/10/2026 | Claude (AI) | `EmployeeService.ManagerUpdateAsync`, `GradeService.SetGradeAsync`, UC15, UC16 | Quản đốc đổi tổ và vai trò nhân viên; Admin / Quản đốc đặt bậc thợ trực tiếp không qua đề xuất | Đã làm |
| DEV-ALL-8 | 24/10/2026 | Claude (AI) | Bảng mới `KpiDailyTarget`, `vw_KpiDailyResult`, `KpiService`, UC19 | Chỉ tiêu KPI lập theo tổng cả kỳ; từng ngày đặt riêng (không chia đều) | Đã làm |
| DEV-ALL-9 | 24/10/2026 | Claude (AI) | `KpiCalendarService`, `KpiCalendarDrawer`, `KpiMonthGrid` | Lịch KPI theo ngày (dạng tờ lịch): đặt KPI từng ngày, Tổng bán thành phẩm, số còn lại để đặt KPI | Đã làm |
| DEV-ALL-10 | 24/10/2026 | Claude (AI) | `OrderMaterialService`, `IncomingMaterialService`, `IncomingMaterialsPage`, `PendingMaterialOrdersTable` | Quản đốc thấy đơn chờ khai báo NVL và bấm thông báo mở đúng đơn; Thủ kho có màn NVL chờ nhận và được báo khi khách cấp / đơn mua đã đặt | Đã làm |
| DEV-ALL-11 | 25/10/2026 | Claude (AI) | `SQL/NX-MTD_Database.sql` (bản 24/10 của Morgan), toàn bộ entity / dịch vụ / giao diện | Chuyển code sang database mới: định mức theo mã hàng, dòng đơn trỏ biến thể, thiếu hàng tự tính, bỏ KPI theo ngày và dữ liệu demo 19.3–19.6 | Đã làm |
| DEV-ALL-16 | 29/10/2026 | Claude (AI) | UC10, UC15, UC19, UC40, UC45–UC47, UC51 | Chuyển code sang database 29/10: định mức không còn lượng vải / hao hụt / thứ tự, màu – size chỉ đọc từ đơn hàng (kèm kích thước), Kế toán cùng Admin sửa định mức, lệnh cắt thường không kiểm vải, lệnh cắt bù ước tính theo `vw_RecutFabricNeed`; hoàn lại bậc thợ A–D, xuất hàng 1 bước, bỏ loại đơn tăng ca; các mục "Doing" của sheet | Đã làm |
| DEV-ALL-17 | 31/10/2026 | Claude (AI) | UC50, `GradeLevelCode`, `GradeLevel`, `Employee`, `GradeEvaluation` | Khôi phục bậc thợ động theo yêu cầu của Minh: Admin thêm / sửa / ngừng dùng / xóa bậc (E, F…), bỏ CHECK cứng A–D; sửa / xóa mức tiền bậc chưa hiệu lực | Mở |
| DEV-ALL-18 | 31/10/2026 | Claude (AI) | Cấu trúc `Application/`, `Infrastructure/Data` | Chia `Services`, `Interfaces`, `DTOs`, `Validators` thành thư mục con theo module (Shared, Auth, Hr, Orders, Requests, Warehouse, Cutting, Purchasing, Production, Attendance, Finance); cấu hình EF tách thành `IEntityTypeConfiguration<T>` từng entity | Mở |
| DEV-ALL-20 | 31/10/2026 | Claude (AI) | UC39, `PurchaseOrder` | Màn "NVL chờ nhận" của Thủ kho hiện tình trạng đơn mua (còn nhận tiếp được) và danh sách đơn mua đã đóng do giao thiếu, không đổi DB | Mở |
| DEV-ALL-21 | 31/10/2026 | Claude (AI) | UC37, UC39 | Phiếu nhập: đính ảnh chứng từ trước khi lưu (không thêm ảnh ở màn chi tiết); không lập phiếu cho NVL khách cấp đã bị Quản đốc chốt không bù; điền sẵn nhiều NVL khi lập phiếu | Mở |
| DEV-ALL-22 | 31/10/2026 | Claude (AI) | UC24 | Tồn kho NVL: thêm lựa chọn "không thuộc đơn hàng nào" vào ô lọc đơn hàng; đổi nhãn bộ lọc đơn hàng cho rõ là đơn đặt của khách | Mở |
| DEV-ALL-23 | 31/10/2026 | Claude (AI) | UC19, UC21, `KpiTarget`, bảng mới `KpiDailyTarget`, `vw_KpiDailyResult` | Mỗi đơn + mã hàng + tổ một chỉ tiêu; chỉ tiêu chỉ so với số lượng đơn; khôi phục KPI theo ngày (lịch tháng), BTP khả dụng chỉ kiểm khi đặt KPI ngày | Mở |
| DEV-ALL-24 | 01/11/2026 | Claude (AI) | UC19, `KpiTarget`, `SystemSetting`, `EndOfOrderService` | Một đơn + mã hàng + tổ có nhiều chỉ tiêu theo các kỳ không trùng ngày (thay quy tắc một chỉ tiêu ở DEV-ALL-23); KPI ngày khóa sau 08:00 của chính ngày đó | Mở |
| DEV-ALL-25 | 01/11/2026 | Claude (AI) | UC19, `SystemSetting` | Kiểm ngày của kỳ KPI; cảnh báo kỳ kết thúc sau hoặc sát ngày giao hàng; form lập KPI hiện ngày giao | Mở |
| DEV-ALL-26 | 01/11/2026 | Claude (AI) | UC09 (phần NVL) | Màn "NVL của đơn" mở đầu bằng danh sách đơn chờ NVL có phân trang, lọc; bỏ ô chọn đơn | Mở |
| DEV-ALL-27 | 01/11/2026 | Claude (AI) | UC21, JOB-04 | Cảnh báo nguy cơ trễ quét cả đơn Đang giao và Giao một phần; đơn đã đóng được gỡ cờ | Mở |
| DEV-ALL-28 | 01/11/2026 | Claude (AI) | UC10, bảng mới `ProductImage` | Mã hàng có một ảnh chính và tối đa 10 ảnh phụ, job dọn ảnh mồ côi; giới hạn nhập mã hàng và định mức (kích thước, số chi tiết, số dòng mỗi lần lưu) | Mở |
| DEV-ALL-29 | 01/11/2026 | Claude (AI) | `Attachment`, `IFileStorage` | Ảnh chứng từ đã gỡ hoặc bị thay chỉ giữ 15 ngày rồi job dọn tệp xóa file và dòng; thêm `Attachment.RemovedAt` | Mở
| DEV-ALL-30 | 01/11/2026 | Claude (AI) | UC09, UC14 | Siết kiểm tra khách hàng và nhà cung cấp: email chỉ ASCII và có tên miền, độ dài tính sau khi cắt khoảng trắng, lỗi trùng tên hiện ngay dưới ô tên; danh sách khách hàng phân trang phía server | Mở
| DEV-ALL-31 | 01/11/2026 | Claude (AI) | UC09 (phần NVL), UC10 | NVL của đơn: giới hạn số lượng, số lần giao; chặn sửa, xóa khi đơn đã đóng. Bỏ kích thước ở màu – size | Mở
| DEV-ALL-32 | 01/11/2026 | Claude (AI) | UC10, UC19, `ProductVariant`, `WorkOrderLine` | Bỏ dài, rộng ở màu – size; lệnh cắt thường tính vải theo kích thước từng chi tiết và chỉ cảnh báo; lệnh cắt thường được cắt lẻ thêm thành phần | Mở
| DEV-ALL-33 | 01/11/2026 | Claude (AI) | UC48, UC49 | Kiểm kê kho: giới hạn số đếm, ghi chú, phạm vi, bộ lọc danh sách; form lập phiếu phân trang để kiểm kê được hơn 100 hàng hóa | Mở
| DEV-ALL-34 | 01/11/2026 | Claude (AI) | Ma trận vai trò, UC19, UC21, UC22 | Admin có toàn bộ chức năng của Quản đốc | Mở |
| DEV-ALL-35 | 01/11/2026 | Claude (AI) | UC19 (tổ cắt) | Lệnh cắt: giới hạn ghi chú, lý do cắt thiếu, số dòng; kiểm tra thêm cho lệnh cắt bù, báo cắt xong, bộ lọc danh sách; sửa lệnh cũ giữ nguyên hạn đã qua | Mở
| DEV-ALL-36 | 01/11/2026 | Claude (AI) | UC22 (KI-11), UC55 | Sửa sản lượng sau hạn: giới hạn số lượng, lý do, số dòng; chặn chọn ngày tương lai | Mở
| DEV-ALL-37 | 01/11/2026 | Claude (AI) | UC56 (KI-08), UC22 | Hàng hỏng của các tổ: tách bộ lọc, bảng gọn; công nhân phản đối thì Quản đốc / Admin chấp nhận phiếu hoặc hủy phiếu; giới hạn nhập | Mở
| DEV-ALL-38 | 01/11/2026 | Claude (AI) | UC57, UC22 | Sửa số hàng đạt QC: giới hạn nhập, chặn giảm dưới số đã bàn giao kho thành phẩm | Mở
| DEV-ALL-39 | 01/11/2026 | Claude (AI) | UC (KPI tổ may) | Chỉ tiêu KPI tổ may: giới hạn kỳ và số lượng, chặn sửa chỉ tiêu dưới số đã làm | Mở
| DEV-ALL-40 | 01/11/2026 | Claude (AI) | UC09, UC23 | Đơn mua NVL: tạo là đặt hàng luôn; NVL lỗi thiếu tồn tạo đơn từ Mua NVL thay vì đơn mua | Mở
| DEV-ALL-41 | 01/11/2026 | Claude (AI) | UC45–UC47 | Phiếu xuất hàng: giới hạn nhập, chặn ngày sai, chặn xuất đơn đã đóng, gửi phiên bản phiếu khi thao tác; sửa báo lỗi tải ảnh quá lớn | Mở
| DEV-ALL-42 | 01/11/2026 | Claude (AI) | UC10, UC19, `ProductMarker`, `ProductMarkerFabric` | Lệnh cắt cả bộ tính số mét vải theo bìa của mã hàng, thay cho kích thước từng chi tiết | Mở
| DEV-ALL-43 | 01/11/2026 | Claude (AI) | UC51, `CuttingLay` | Bàn cắt lưu chiều dài bàn cắt; tính vải đã cắt và vải còn lại khi nhập bàn | Mở
| DEV-ALL-44 | 02/11/2026 | Claude (AI) | UC08, UC39 | Thủ kho xem được mọi đơn thiếu NVL Quản đốc đã xử lý, không chỉ đơn đang chờ hàng bù | Mở
| DEV-ALL-45 | 02/11/2026 | Claude (AI) | UC08, UC39, `OrderMaterial` | Chốt không bù NVL khách cấp: số khách cấp giảm bằng số đã nhận, phần thiếu thành số phải mua | Mở
| DEV-ALL-46 | 02/11/2026 | Claude (AI) | UC24, `Material` | Vải cố định đơn vị mét; khổ vải nhập và báo bằng mét | Mở
| DEV-ALL-47 | 02/11/2026 | Claude (AI) | UC23, UC37 | Nút tạo đơn mua NVL chuyển từ màn NVL của đơn sang màn Phiếu nhập NVL | Mở
| DEV-ALL-48 | 02/11/2026 | Claude (AI) | UC52, GBR-20, UC42 | Tổ may phải phân công đủ công đoạn trước khi nhận BTP; màn phân công chọn công nhân ngay trên từng công đoạn | Mở
| DEV-ALL-49 | 02/11/2026 | Claude (AI) | UC52, UC42 | Quản đốc, Admin chia dòng đơn (mã hàng × màu × size) cho tổ may; tổ chỉ phân công mã có dòng được chia, chỉ xin và nhận BTP của dòng được chia | Mở |
| DEV-ALL-50 | 02/11/2026 | Claude (AI) | UC25, UC26 | Admin dùng được màn công đoạn trừ phần đơn giá; thêm quản lý nhóm công đoạn và xếp nhóm nhiều công đoạn | Mở |
| DEV-ALL-51 | 02/11/2026 | Claude (AI) | UC17, UC18 | Công nhân chưa bắt buộc thuộc tổ; màn Tổ thêm nhân viên vào tổ, loại khỏi tổ, thôi giữ chức tổ trưởng / tổ phó | Mở |
| DEV-ALL-52 | 02/11/2026 | Claude (AI) | UC19, UC43 | Lệnh cắt tự hoàn thành khi kho nhận đủ BTP theo kế hoạch; bỏ nút "Cắt xong" của tổ cắt; Quản đốc, Admin đóng lệnh cắt thiếu | Mở |
| DEV-ALL-53 | 02/11/2026 | Claude (AI) | UC51, UC42 | Form bàn cắt bỏ khối vải còn lại; phiếu trả vải thừa tự điền số vải còn lại sau cắt | Mở |
| DEV-ALL-54 | 02/11/2026 | Claude (AI) | UC05, UC20 | Hộp thư lọc Từ quản lý / Hệ thống, trả thêm `IsManual`; thông báo phân việc đổi nguồn từ WAREHOUSE sang PRODUCTION | Mở |
| DEV-ALL-55 | 02/11/2026 | Claude (AI) | UC52 | Phân công công đoạn thêm chế độ Theo công nhân: bảng công nhân, khung chọn công đoạn có nút Lưu | Mở |
| DEV-ALL-56 | 02/11/2026 | Claude (AI) | UC19 (KPI), UC21 | Tổ trưởng, tổ phó may xem KPI của tổ ở trang chủ; thêm API chỉ đọc `GET team-leader/kpi-targets` | Mở |
| DEV-ALL-57 | 02/11/2026 | Claude (AI) | UC55, GBR-20 | Tổ trưởng nhập sản lượng theo công nhân: bảng công nhân, khung nhập theo nhóm công đoạn, nhập nhóm tự điền từng công đoạn; lưới cũ chỉ còn cho Quản đốc | Mở |
| DEV-ALL-58 | 02/11/2026 | Claude (AI) | Dữ liệu demo, `Customer`, `Operation` | Dữ liệu demo 19.7, 19.8: thêm khách hàng ngoài công ty mẹ, đổi công đoạn cuối DEMO-QU02, đổi trạng thái có sẵn của tháng 2026-05 | Mở |
| DEV-ALL-59 | 02/11/2026 | Claude (AI) | UC09 (phần NVL) | NVL của đơn khai báo ở tab trong chi tiết đơn, sau khi đơn đã tạo; bỏ màn và API đơn chờ NVL; Quản đốc xem được đơn hàng (không có đơn giá, không sửa); thêm API `GET manager/customer-orders` | Mở |
| DEV-ALL-60 | 03/11/2026 | Claude (AI) | UC09 (phần NVL) | Thêm NVL của đơn theo hai cách: chọn mã hàng trong đơn rồi nhập số cho các NVL định mức, hoặc chọn NVL ngoài; thêm API `GET .../order-materials/product-materials`, `POST .../order-materials/batch` | Mở |
| DEV-ALL-61 | 03/11/2026 | Claude (AI) | UC10, UC38 | Ô chọn NVL ở tab Định mức NVL của Định mức mã hàng có nút Thêm NVL mới; thêm API `POST accountant/materials` | Mở |
| DEV-ALL-62 | 03/11/2026 | Claude (AI) | UC19 (KPI), UC21, bảng mới `KpiTargetLine` | Chỉ tiêu KPI tổ may nhập theo từng màu × size tổ được chia; các tổ cùng mã hàng chia nhau số đặt của từng màu × size; bảng KPI xem chỉ tiêu và QC đạt theo màu × size | Mở |
| DEV-ALL-63 | 03/11/2026 | Claude (AI) | UC53, UC54, UC60, `WorkAllocation` | Giao sản lượng công đoạn theo ngày, phiếu của tổ trưởng CONFIRMED ngay, thu hồi cuối ngày (phần thu hồi cuối ngày đã thay bằng DEV-ALL-64) | Mở |
| DEV-ALL-64 | 03/11/2026 | Claude (AI) | UC53, UC55, UC60, `WorkAllocation` | Không thu hồi cuối ngày: phần chưa làm hết chuyển sang hôm sau cho chính công nhân; "Giao như lần trước"; nhập sản lượng vượt số được giao thì chuyển phần vượt từ người cùng công đoạn / phần tổ chưa giao | Mở |
| DEV-ALL-65 | 03/11/2026 | Claude (AI) | UC38 | Quản đốc và Admin quản lý danh mục NVL như Thủ kho (xem, thêm, sửa, ngừng dùng / dùng lại); thêm API `GET`, `GET {id}`, `PUT {id}`, `PATCH {id}/status` ở `admin/materials`, `manager/materials` | Mở |
| DEV-ALL-66 | 03/11/2026 | Claude (AI) | UC45, UC47, GBR-24 | Kế toán xác nhận giao không nhập số khách nhận (nhận = xuất, phiếu luôn DELIVERED); xuất kho báo thêm Quản đốc (Admin nhận bản sao theo DEV-ALL-67) | Mở |
| DEV-ALL-67 | 03/11/2026 | Claude (AI) | Thông báo (`Notification`), mọi UC có thông báo | Thông báo gửi Quản đốc thì Admin nhận bản sao; việc do Quản đốc làm thì các Quản đốc khác nhận bản sao; người thao tác không nhận thông báo việc của chính mình | Mở |
| DEV-ALL-68 | 03/11/2026 | Claude (AI) | UC53, UC55 | Giao sản lượng theo ngày và Sản lượng cuối ca: bắt buộc chọn đơn hàng → mã hàng → màu - size trước khi giao / nhập (bỏ chế độ xem mọi dòng đơn ở màn giao việc); chỉ chọn được và BE chỉ nhận đơn / mã hàng / màu - size đã chia cho tổ | Mở |
| DEV-ALL-69 | 03/11/2026 | Claude (AI) | UC22, UC55 | Màn Sửa sản lượng sau hạn của Quản đốc dùng cùng bố cục với Sản lượng cuối ca của tổ trưởng (bảng công nhân → khung nhập theo công nhân), lý do sửa nhập trong khung của từng công nhân | Mở |
| DEV-ALL-70 | 03/11/2026 | Claude (AI) | UC47, GBR-23, GBR-24, `DispatchNote`, `DispatchNoteLine`, `vw_OrderLineProgress` | Phiếu xuất hàng bỏ trạng thái PARTIALLY_DELIVERED và cột ReceivedQty; số đã giao của đơn tính theo số xuất của phiếu đã giao | Mở |
| DEV-ALL-71 | 05/11/2026 | Claude (AI) | UC10, `ProductImage`, `ProductVariant`, `ProductMarkerFabric`, bảng mới `ProductColor`, `ProductColorImage` | Màu áo thành bảng `ProductColor` (biến thể, vải bìa, ảnh theo màu trỏ `ProductColorId`, bỏ cột `Color`); ảnh phụ giảm từ 10 còn 5; mỗi màu có 1-3 ảnh | Mở |
| DEV-ALL-72 | 05/11/2026 | Claude (AI) | `ChangePasswordDto`, mọi ô "Lý do", R2 (AI_Context) | Mật khẩu mới tối đa 50 ký tự; mọi ô "Lý do" tối đa 500 ký tự (hằng số `TextLimits` ở BE, `constants/limits.ts` ở FE); lý do của đơn từ giảm từ 1.000 xuống 500 | Mở |
| DEV-ALL-73 | 05/11/2026 | Claude (AI) | UC53, UC55, UC60 (giao việc, phân công) | Công đoạn phải có đơn giá được duyệt (hiệu lực đến ngày giao) mới giao / phân công được cho công nhân | Mở |
| DEV-ALL-74 | 05/11/2026 | Claude (AI) | UC15, UC16 (nhân viên), UC18 (tổ) | Họ tên nhân viên chỉ gồm chữ cái và khoảng trắng; các ô mã chặn ký tự không cho phép ngay khi gõ | Mở |
| DEV-ALL-75 | 05/11/2026 | Claude (AI) | UC09 (chi tiết đơn hàng) | Chi tiết đơn hàng hiện số đã giao và số còn phải giao theo màu, và tổng đã giao / tổng đặt | Mở |
| DEV-ALL-76 | 05/11/2026 | Claude (AI) | UC06, UC38 (khách hàng, nhà cung cấp) | Ngừng hợp tác với khách hàng / nhà cung cấp không bắt buộc ghi lý do | Mở |
| DEV-ALL-77 | 06/11/2026 | Claude (AI) | UC40 (cấp vải / BTP), UC53, UC55, UC56, UC57, đơn Xin NVL / Xin BTP | Ghi hàng hỏng, sản lượng, QC chỉ trong thời gian gia công của đơn (giao theo ngày: xem DEV-ALL-78); validate dòng và số lượng đơn Xin NVL / Xin BTP (vải lẻ 2 chữ số, NVL khác vải và BTP số nguyên); phiếu cấp vải / BTP có đề xuất theo mã hàng + màu (+ size) | Mở |
| DEV-ALL-78 | 06/11/2026 | Claude (AI) | UC53, UC60 (giao sản lượng theo ngày) | Chỉ giao và sửa trong ngày hôm nay; sang ngày hôm sau chỉ xem được | Mở |
| DEV-ALL-79 | 06/11/2026 | Claude (AI) | UC56 (phiếu lỗi), UC55, UC59, `DefectRecord`, 3 trigger, 3 view | Thêm loại phiếu OUTPUT_ERR "Nhập sai sản lượng": trừ sản lượng và tiền của công nhân, tính lại trần sản lượng và tiến độ; đổi tên màn Hàng hỏng thành Phiếu lỗi / điều chỉnh | Mở |
| DEV-ALL-80 | 06/11/2026 | Claude (AI) | UC10 (mã hàng), `ProductColorImage` | Bỏ ảnh theo màu của mã hàng (bỏ bảng `ProductColorImage`, API `colorImages`, mục "Ảnh theo màu" ở form); giữ bảng `ProductColor` và 5 ảnh phụ. Thay phần ảnh theo màu của DEV-ALL-71 | Mở |
| DEV-ALL-81 | 07/11/2026 | Claude (AI) | UC09, UC49, UC47, UC19, `CustomerOrder.Status`, `DispatchNote`, `WorkOrder`, `KpiTarget` | Chặn dừng đơn khi hàng đã xuất hết (`ORDER_FULLY_DISPATCHED`); hủy / dừng đơn tự hủy phiếu xuất PENDING, lệnh cắt OPEN chưa làm gì và KPI ACTIVE | Mở |
| DEV-ALL-82 | 07/11/2026 | Claude (AI) | UC41–UC46 (phiếu cấp, phiếu nhập, kiểm kê, trả vải thừa), UC09, `PurchaseOrder`, `OrderMaterial` | Quy tắc ngày chứng từ kho (không tương lai, không trước ngày nhận đơn, không quá 7 ngày trước); thêm `ExpectedDeliveryDate` cho đơn mua và NVL khách cấp; siết kiểm trả vải thừa (một phiếu chờ mỗi dòng, không vượt số thừa theo số đã cắt) | Mở |
| DEV-ALL-83 | 07/11/2026 | Claude (AI) | UC35 (xe, chuyến xe), đơn xin của Cơ điện (`SPARE_PARTS`, `MACHINE_SUPPLIES`, `NEW_MACHINE`) | Siết kiểm biển số, số xăng, tiền xăng, số km, định mức xăng; đơn xin linh kiện / vật tư / máy chỉ nhận số nguyên có trần (9.999 / 99) và bắt buộc có dòng | Mở |
| DEV-ALL-84 | 07/11/2026 | Claude (AI) | UC30 (suất ăn) | Số suất ăn tổ trưởng xác nhận không được vượt số người đang làm trong tổ | Mở |
| DEV-ALL-86 | 09/11/2026 | Claude (AI) | UC25 (công đoạn), UC10, `OperationComponent` | Công đoạn gắn với chi tiết vải × vải: `OperationComponent` thêm `MaterialId`, chỉ chi tiết PANEL và vải có trong định mức của chi tiết; phụ liệu không còn gắn công đoạn | Đã làm |
| DEV-ALL-85 | 09/11/2026 | Claude (AI) | UC10, UC49–UC51 (lệnh cắt, bàn cắt), `ProductMarker`, `ProductMarkerRatio`, `ProductMarkerComponent`, `ProductComponentMaterial` | Bìa cắt theo đơn: ước mét vải = số lớp × chiều dài bìa; bỏ kích thước chi tiết, khổ vải, khổ bìa (khổ cố định 1,5 m) và 2 view; bàn cắt không gắn bìa. Đã sửa DB, backend, frontend (tab "Bìa cắt" ở chi tiết đơn, tab "Nhóm bìa" ở định mức). Thay cách tính của DEV-ALL-42 | Mở |
| DEV-ALL-15 | 28/10/2026 | Claude (AI) | UC10, UC15–UC17, UC19, UC26, UC27, UC41–UC43, UC52–UC56 | Sửa theo sheet ghi chú: UI theo vai trò và loại tổ, tìm kiếm tức thì, KPI theo BTP khả dụng và chỉ chốt cuối đơn, tăng ca, phiếu lỗi lệch kiểm cuối đơn, bậc thợ động, Quản đốc quản lý tài khoản (trừ Admin, Kế toán); đối chiếu swimlane: xuất hàng có Quản đốc duyệt, ảnh phiếu ký / biên bản giao bắt buộc, lệnh cắt hoàn thành sau trả vải thừa + so vải, NVL lỗi tự tạo phiếu xin / đơn mua, nhận thành phẩm báo KPI, "không bù" NCC giữ PARTIAL; thông báo hệ thống cho luồng cắt, kho, KPI | Đã làm |
| DEV-ALL-14 | 27/10/2026 | Claude (AI) | UC24, UC38, UC40–UC44, UC47, UC52–UC55, UC60 | Chuyển code sang database bản 26/10: màu NVL nằm trong `Material.Color`, giao việc không còn BTP giao kèm, bỏ `vw_TeamPieceHolding`; thêm validate và dữ liệu demo 19.3 | Đã làm |
| DEV-ALL-13 | 26/10/2026 | Claude (AI) | UC24, UC40–UC44, UC47, UC52–UC55, UC60 | Chuyển code sang database bản 25/10: ghi kho qua usp_Stock_Post, cắt bù theo chi tiết × vải, giao việc theo công đoạn (WorkAllocation), công đoạn to / chi tiết | Đã làm |
| DEV-ALL-12 | 25/10/2026 | Claude (AI) | UC37, UC45–UC47, swimlane III.1–III.5 | Đối chiếu màn hình luồng chính với swimlane; còn lệch: Thủ kho không nhập thiếu / lỗi, chưa có bước Quản đốc duyệt phiếu xuất (database chưa có trạng thái) | Mở |

---

### DEV-MI-04-1 · JOB-02 chưa đẩy thông báo thật, chỉ ghi mốc đã nhắc

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-04 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC30, UC62 (JOB-02 — nhắc tổ chưa xác nhận suất ăn lúc 08:15), bảng `TeamMealConfirmation` (cột `RemindedAt`)
- **Tài liệu gốc ghi:** `NX-MTD_AI_Context.md` mục 4.6: "JOB-02 08:15 nhắc tổ chưa xác nhận". `NX-MTD_AI_Context.md` mục 0.5 quy định luồng khác cần dữ liệu của luồng khác phải gọi qua interface dùng chung (`INotificationService`...).
- **Code đang làm:** `be/Application/Services/MealService.cs` (`RunReminderJobAsync`, gọi bởi `be/Infrastructure/Jobs/MealReminderJob.cs` lúc 08:15) không tạo thông báo trong app/push cho tổ trưởng. Với mỗi tổ chưa xác nhận, job chỉ tạo/cập nhật dòng `TeamMealConfirmation` (`Source = 'ATTENDANCE'`, `MealCount`/`AttendanceCount` = số đã quẹt) và ghi `RemindedAt = now`. Tổ trưởng chỉ biết "đã được nhắc" khi tự mở màn hình suất ăn và thấy số gợi ý; không có banner/notification/push chủ động.
- **Lý do:** Bảng `Notification` (và `Announcement`) thuộc sở hữu Khiêm (`NX-MTD_AI_Context.md` mục 6.2, mục 8). Tại thời điểm code, `be/Application/Interfaces/` chưa có `INotificationService` hay interface dùng chung tương đương để gọi (đã grep xác nhận). Theo luật 0.5/0.6 của AI Context, không được ghi thẳng vào bảng của người khác và không tự ý tạo bảng/luồng mới ngoài phạm vi task. Vì vậy chọn phương án tối thiểu: chỉ lưu vết "đã nhắc" trên bảng mình sở hữu (`TeamMealConfirmation.RemindedAt`), đủ để JOB-03 và các job sau biết tổ nào đã được nhắc, và để không chặn tiến độ MI-04.
- **Ảnh hưởng người khác:** Khiêm (chủ bảng `Notification`) — khi `INotificationService` (hoặc tương đương) có sẵn, cần sửa `MealReminderJob`/`MealService.RunReminderJobAsync` để gọi thêm bước tạo `Announcement`/`Notification` cho tổ trưởng/tổ phó của tổ chưa xác nhận. Đã không tag PR vì chưa mở PR.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3: mô tả UC62/JOB-02 nêu rõ "nhắc" hiện tại = tự xem trên màn hình, chưa phải push/in-app notification
  - [ ] `NX-MTD_AI_Context.md` mục 4.6: ghi chú tạm thời cho tới khi nối với `INotificationService`
- **Trạng thái:** Bỏ — đã xử lý ngày 21/10/2026 sau khi Khiêm có sẵn dịch vụ dùng chung (KH-07, `IAuditLogService` / `INotificationService`), xem test tương ứng trong `tests/Application.Tests`.

---

### DEV-MI-03-1 · Màn tổ trưởng xác nhận suất ăn là web nhập số tổng, không phải checklist từng người trên mobile

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-03 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC30 (FR 5.1), bảng `TeamMealConfirmation`
- **Tài liệu gốc ghi:** Mockup báo cáo (mục III.5, màn 5.1 "Team meal") mô tả màn hình **mobile app** cho tổ trưởng: danh sách từng công nhân trong tổ (mặc định tick sẵn người đã quẹt vân tay), tổ trưởng tick/bỏ tick từng người, xem tổng chạy, bấm "Xác nhận".
- **Code đang làm:** `fe/src/features/meal/pages/TeamMealPage.tsx` + `TeamMealConfirmForm.tsx` là màn **web** (đúng stack dự án theo Coding Rules: chỉ có `nxmtd-web`, không có app mobile riêng — xem Phụ lục A/mục 11.4 `NX-MTD_AI_Context.md`), hiển thị **một số tổng gợi ý** (số người đã quẹt vân tay hôm đó) và một ô nhập **số suất ăn của cả tổ** (`ConfirmTeamMealDto.MealCount`), không có checklist theo từng công nhân.
- **Lý do:** Schema `TeamMealConfirmation` (đã có sẵn, không được đổi theo yêu cầu task) chỉ lưu `MealCount`/`AttendanceCount` là **số tổng của cả tổ** theo ngày, không có bảng con lưu trạng thái theo từng nhân viên. Thêm bảng/cột mới để lưu chi tiết từng người là đổi schema, ngoài phạm vi MI-03/MI-04 (task yêu cầu không đổi schema). Đồng thời dự án chỉ build một web app React (không có app di động), nên không thể dựng đúng màn hình mobile trong mockup.
- **Ảnh hưởng người khác:** Không ảnh hưởng luồng của thành viên khác (bảng `TeamMealConfirmation` do Minh sở hữu).
- **Cần sửa trong tài liệu:**
  - [ ] Report-3 / mockup: cập nhật màn 5.1 "Team meal" thành web, nhập một số tổng thay vì checklist từng người
  - [ ] `NX-MTD_Database.sql` / `_Giai_thich.md`: ghi rõ `TeamMealConfirmation.MealCount` là số tổng, không phải theo từng nhân viên (nếu sau này cần theo từng người thì phải thêm bảng mới, ví dụ `TeamMealConfirmationLine`)
- **Trạng thái:** Mở

---

### DEV-MI-09-1 · Tự chọn mẫu file Excel nhập bảng lương; không lưu lại file đã nhập

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-09 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC31, bảng `Payroll` (cột `ImportedFileUrl`), `Payslip`/`PayslipItem`, MSG24, MSG27
- **Tài liệu gốc ghi:** `NX-MTD_AI_Context.md` mục 12 "còn chưa chốt" ghi rõ: "mẫu Excel bảng lương (nên `PayslipItem` để dạng dòng)" — chưa chốt cấu trúc cột cụ thể. Report-3 UC31 chỉ mô tả bước "Kế toán xuất dữ liệu → tính trong Excel → import lại", không nêu tên cột.
- **Code đang làm:** `be/Application/Interfaces/IExcelPayrollParser.cs` + `be/Infrastructure/Services/ClosedXmlPayrollParser.cs` tự chọn cấu trúc file nhập: mỗi dòng là một khoản của một nhân viên (cột mã nhân viên, số công, thực lĩnh, loại khoản `ADDITION`/`DEDUCTION`/`INSURANCE`/`INFO`, tên khoản, số tiền), khớp `PayslipItemGroup` đã có sẵn trong DB. File Excel đã nhập **không được lưu lại**: `Payroll.ImportedFileUrl` luôn `null` (`PayrollService.ImportAsync`) vì MI-09 chưa có `IAttachmentService`/kho file cho loại file này. `IPayrollRepository.GetDaysWithoutOutputAsync` (cảnh báo "ngày có giờ công nhưng chưa nhập sản lượng") tính xấp xỉ theo ngày, không theo ca, vì `DailyTimesheet` không lưu cột ca.
- **Lý do:** SRS đánh dấu rõ là "chưa chốt"; chọn cấu trúc cột đơn giản nhất ánh xạ thẳng vào `PayslipItemGroup`/`PayslipItem` sẵn có, để Kế toán tính xong lương trên Excel ngoài hệ thống (GBR-12) rồi dán kết quả vào đúng cột, không cần theo một mẫu kế toán thật nào (chưa tồn tại tại thời điểm code).
- **Ảnh hưởng người khác:** Không ảnh hưởng trực tiếp luồng người khác hiện tại; nếu sau này có mẫu Excel chính thức từ phòng kế toán thật thì phải sửa `IExcelPayrollParser`/`ClosedXmlPayrollParser`/`ClosedXmlPayrollWriter` cho khớp cột, và cần `IAttachmentService` (Khiêm) nếu muốn lưu lại file gốc.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3: UC31 bổ sung mô tả cột của file Excel nhập mẫu
  - [ ] `NX-MTD_AI_Context.md` mục 12: đánh dấu mẫu Excel bảng lương đã có bản tạm (không còn "chưa chốt" nhưng chưa phải bản chính thức)
  - [ ] `NX-MTD_Database.sql` / `_Giai_thich.md`: ghi chú `Payroll.ImportedFileUrl` hiện luôn `NULL` cho tới khi có lưu file
- **Trạng thái:** Mở

---

### DEV-MI-09-2 · "Mở lại" bảng lương đã duyệt cũng mở khóa luôn tháng kế toán

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-09 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC32, bảng `Payroll.Status`, `AccountingPeriod.Status`, GBR-10
- **Tài liệu gốc ghi:** Report-3 UC32 chỉ ghi "Admin mở lại bảng lương đã duyệt về Nháp (bắt buộc lý do)", không nói rõ việc mở lại có mở khóa `AccountingPeriod` hay không. `NX-MTD_AI_Context.md` R7/GBR-10 chỉ ghi "tháng đã khóa: không sửa/import/tính lại", không nói chiều ngược lại (mở khóa khi nào).
- **Code đang làm:** `PayrollService.ApproveAsync` khóa `AccountingPeriod` cùng lúc duyệt bảng lương (duyệt = chốt tháng). Theo đó, `PayrollService.ReopenAsync` suy luận ngược: mở lại bảng lương về Nháp thì mở khóa luôn tháng đó (`AccountingPeriod.Status = OPEN`, `LockedAt = null`), giữ quan hệ 1-1 giữa `Payroll.Status = APPROVED` và `AccountingPeriod.Status = LOCKED`.
- **Lý do:** Nếu không mở khóa tháng thì thao tác "mở lại về Nháp" vô nghĩa trên thực tế — Kế toán vẫn không nhập lại được Excel vì `ImportAsync` chặn tháng khóa (`ErrorCodes.PayrollPeriodLocked`, MSG21). Chọn phương án nhất quán nhất với cách `ApproveAsync` đã làm.
- **Ảnh hưởng người khác:** `AccountingPeriod` do Minh sở hữu, nhưng mở khóa tháng cũng mở lại khả năng sửa dữ liệu các module khác bị chặn bởi tháng khóa (`OutputRecord`, `DefectRecord`, `QcBatch`, `AttendanceAdjustment`, `MonthlyCost` — trigger khóa tháng dùng chung `fn_IsPeriodLocked`). Kiều/Thái nên biết nếu có luồng phụ thuộc trạng thái khóa tháng khi Admin mở lại bảng lương.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3: UC32 bổ sung bước "mở khóa tháng kế toán" khi Admin mở lại bảng lương
  - [ ] `NX-MTD_AI_Context.md` mục 4.7: ghi rõ quan hệ 1-1 giữa `Payroll.Status = Approved` và khóa `AccountingPeriod`
- **Trạng thái:** Mở

---

### DEV-MI-09-3 · Thao tác bảng lương chưa ghi `AuditLog`

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-09 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC31/UC32, GBR-15 (quy tắc R6), bảng `AuditLog`
- **Tài liệu gốc ghi:** `NX-MTD_AI_Context.md` mục 5, quy tắc R6: "Mọi thay đổi chứng từ kho, sản lượng, chấm công, đơn giá, bảng lương, tài khoản → `AuditLog` (người, lúc, giá trị cũ/mới JSON, lý do) trong cùng transaction (GBR-15)."
- **Code đang làm:** `be/Application/Services/PayrollService.cs` (import, gửi duyệt, duyệt, từ chối, mở lại) không ghi bất kỳ dòng `AuditLog` nào cho các thay đổi trạng thái `Payroll`/`AccountingPeriod`.
- **Lý do:** Tại thời điểm code, toàn bộ `be/Application` (không riêng MI-09) chưa có `IAuditLogService` hay interface dùng chung tương đương, và cũng chưa module nào khác trong repo gọi ghi `AuditLog` (đã grep xác nhận). Bảng `AuditLog` do Khiêm sở hữu (`NX-MTD_AI_Context.md` mục 6.2); theo luật 0.5/0.6, không tự ghi thẳng vào bảng người khác hay tự dựng luồng audit riêng ngoài phạm vi MI-09. Chọn bỏ qua audit logging cho bảng lương cho tới khi interface dùng chung sẵn sàng.
- **Ảnh hưởng người khác:** Khiêm (chủ bảng `AuditLog`) — khi `IAuditLogService` có sẵn, cần bổ sung lời gọi ghi audit trong `PayrollService.SubmitAsync`/`ApproveAsync`/`RejectAsync`/`ReopenAsync`/`ImportAsync` (cùng transaction với `SaveChangesAsync`).
- **Cần sửa trong tài liệu:**
  - [ ] Report-3: lưu ý luồng duyệt bảng lương hiện chưa có audit trail đầy đủ theo GBR-15
  - [ ] `NX-MTD_AI_Context.md` mục 5 (R6): ghi chú tạm thời cho bảng lương cho tới khi nối `IAuditLogService`
- **Trạng thái:** Bỏ — đã xử lý ngày 21/10/2026 sau khi Khiêm có sẵn dịch vụ dùng chung (KH-07, `IAuditLogService` / `INotificationService`), xem test tương ứng trong `tests/Application.Tests`.

---

### DEV-MI-09-4 · Mẫu Excel bảng lương thật của xưởng khác hẳn mẫu đã chọn ở DEV-MI-09-1

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-09 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC31, `be/Application/Interfaces/IExcelPayrollParser.cs`, `be/Infrastructure/Services/ClosedXmlPayrollParser.cs`/`ClosedXmlPayrollWriter.cs`, `PayslipItem` (`ItemGroup`/`ItemName`)
- **Tài liệu gốc ghi:** Chưa có tài liệu gốc ghi cột cụ thể (xem DEV-MI-09-1) — mục này ghi lại dữ liệu thật vừa nhận được để không bị thất lạc trước khi chốt.
- **Phát hiện:** Người dùng (Minh) gửi ảnh chụp file thật đang dùng ở xưởng: "BẢNG THANH TOÁN LƯƠNG THÁNG 7 NĂM 2026" (Công ty TNHH NX May Tiến Dũng, đơn vị Tổ may 3). File thật có ~40 cột chia nhiều nhóm (ngày công/giờ công, lương sản phẩm theo nhiều mã hàng, lương tăng giờ, lương thời gian, phụ cấp/phúc lợi/hỗ trợ, tiền thưởng, khoản trừ BHXH 10,5%, thực lĩnh) trải trên nhiều vùng cột và kèm một sheet phụ "Bảng phụ" chỉ dùng để Excel tự tính bù lương tối thiểu vùng, hoàn toàn khác cấu trúc 6-cột-một-dòng-một-khoản đã chọn ở DEV-MI-09-1. Cột mã nhân viên trong file thật là "SLĐ" (VD `25119`, `25121`), đã xác nhận với người dùng: đây là **mã chấm công** (`Employee.AttendanceCode`), không phải `EmployeeCode` dạng chữ.
- **Quyết định tạm thời:** Người dùng yêu cầu **tạm dừng, chưa sửa code/parser** — sẽ chốt với team (cách gộp ~40 cột vào các dòng `PayslipItem` loại `ADDITION`/`DEDUCTION`/`INSURANCE`, cột nào bỏ qua vì chỉ phục vụ tính toán nội bộ trong Excel) rồi quay lại. Hai việc đã chốt sẵn để làm khi quay lại: (1) import phải tra nhân viên theo `AttendanceCode` thay vì `EmployeeCode`; (2) giữ nguyên quy tắc "hệ thống không tính lương" — chỉ lưu số đã tính sẵn trong file, không tính lại.
- **Ảnh hưởng người khác:** Không ảnh hưởng luồng người khác; chỉ ảnh hưởng tới chính `IExcelPayrollParser`/`ClosedXmlPayrollParser`/`ClosedXmlPayrollWriter` (Minh sở hữu) khi quay lại sửa.
- **Cần sửa trong tài liệu:**
  - [ ] `NX-MTD_AI_Context.md` mục 12: cập nhật "mẫu Excel bảng lương" bằng cấu trúc thật thay vì bản tạm ở DEV-MI-09-1
  - [ ] Report-3 UC31: mô tả đúng các nhóm cột thật thay vì mẫu giả định
- **Trạng thái:** Bỏ — đã chốt với Minh (21/10/2026) và làm trong code theo file thật; cách gộp cột và các quyết định kèm theo ghi ở DEV-MI-09-5, DEV-MI-09-6

---

### DEV-MI-16-1 · KPI tổ may không chặn theo tồn kho BTP

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-16 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC19 (KPI tổ may — theo bảng phân công, khác "UC19" đã dùng cho lệnh cắt ở `WorkOrderService`), bảng `KpiTarget`
- **Tài liệu gốc ghi:** Một số nguồn SRS gốc cho UC19 (KPI tổ may) có nhắc quy tắc "chặn lập/sửa chỉ tiêu nếu tồn kho BTP không đủ".
- **Code đang làm:** `be/Application/Services/KpiService.cs` (`CreateAsync`/`UpdateAsync`) không kiểm tra tồn BTP (`vw_TeamPieceHolding`/`IStockService`) trước khi lưu chỉ tiêu KPI.
- **Lý do:** Đã trao đổi trực tiếp với người dùng (Minh) trước khi code: chốt bỏ hẳn rule chặn theo tồn kho cho MI-16, không triển khai. Quyết định của người dùng, không phải AI tự suy diễn.
- **Ảnh hưởng người khác:** Không ảnh hưởng bảng/luồng người khác (chỉ đọc `vw_KpiDailyResult`, không đọc/ghi tồn kho của Thái).
- **Cần sửa trong tài liệu:**
  - [ ] Report-3 UC19 (KPI tổ may): bỏ mô tả "chặn nếu tồn BTP không đủ"
  - [ ] `NX-MTD_AI_Context.md` mục 4.8: ghi rõ KPI tổ may không có cổng kiểm tra tồn kho
- **Trạng thái:** Mở

---

### DEV-MI-16-2 · Khóa SystemSetting cho KpiAutoCloseJob chưa được seed

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-16 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** `SystemSetting`, khóa `KPI_AUTO_CLOSE_TIME`, `be/Infrastructure/Jobs/KpiAutoCloseJob.cs`
- **Tài liệu gốc ghi:** `NX-MTD_Database.sql` chỉ seed sẵn các khóa `SystemSetting` đã có từ trước (`MEAL_REMINDER_TIME`, `MEAL_CUTOFF_TIME`...); không có khóa nào cho job tự đóng KPI vì job này mới phát sinh ở MI-16.
- **Code đang làm:** Thêm hằng `SettingKeys.KpiAutoCloseTime = "KPI_AUTO_CLOSE_TIME"` (`be/Domain/Constants/AttendanceCodes.cs`) và dùng trong `KpiAutoCloseJob` qua `ISystemSettingReader.GetTimeAsync(key, new TimeOnly(0,5), ct)`. Vì không được tự thêm migrate/seed vào bảng `SystemSetting` (bảng do Khiêm sở hữu, task yêu cầu không đổi schema), khóa này **chưa có dòng seed** trong `NX-MTD_Database.sql`; job chạy hoàn toàn bằng giá trị fallback mặc định `00:05` cho tới khi có dòng seed thật.
- **Lý do:** `ISystemSettingReader` đã có sẵn cơ chế fallback khi thiếu khóa trong DB (đọc không ra thì dùng `DefaultScheduleTime` truyền vào), nên job vẫn chạy đúng ngay cả khi chưa seed; thêm seed là việc của Khiêm (chủ bảng `SystemSetting`).
- **Ảnh hưởng người khác:** Khiêm (chủ bảng `SystemSetting`) — cần thêm dòng seed `KPI_AUTO_CLOSE_TIME` (gợi ý giá trị `00:05`) vào `NX-MTD_Database.sql`/`CreateAndSeed.sql` khi tiện, để Quản đốc chỉnh được giờ chạy job mà không cần sửa code.
- **Cần sửa trong tài liệu:**
  - [ ] `NX-MTD_AI_Context.md` mục 6.4: thêm `KPI_AUTO_CLOSE_TIME` 00:05 vào danh sách khóa `SystemSetting` quan trọng
  - [ ] `NX-MTD_Database.sql`: thêm dòng seed khi Khiêm cập nhật bảng `SystemSetting`
- **Trạng thái:** Mở

---

### DEV-MI-16-3 · `KpiService` chưa ghi `AuditLog` (cùng khoảng trống đã ghi ở DEV-MI-09-3/DEV-MI-04-1)

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-16 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC19 (KPI tổ may), GBR-15, bảng `AuditLog`
- **Ghi chú:** Lý do và hướng xử lý giống hệt DEV-MI-09-3 (và DEV-MI-04-1) — chưa có `IAuditLogService`/interface dùng chung để ghi vào bảng `AuditLog` (Khiêm sở hữu) tại thời điểm code. `KpiService.CreateAsync`/`UpdateAsync`/`CancelAsync`/`CloseExpiredAsync` cũng không ghi `AuditLog`. Không lặp lại toàn bộ lý do/tài liệu cần sửa ở đây — xem chi tiết tại mục DEV-MI-09-3 ở trên.
- **Ảnh hưởng người khác:** Khiêm (chủ bảng `AuditLog`) — khi `IAuditLogService` có sẵn, bổ sung ghi audit cho cả `PayrollService` (đã ghi ở DEV-MI-09-3) lẫn `KpiService` (mục này) trong cùng một đợt.
- **Trạng thái:** Bỏ — đã xử lý ngày 21/10/2026 sau khi Khiêm có sẵn dịch vụ dùng chung (KH-07, `IAuditLogService` / `INotificationService`), xem test tương ứng trong `tests/Application.Tests`.

---

### DEV-MI-12-1 · Import lại chi phí tháng không giữ được "lịch sử" như SRS mô tả

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-12 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC12, bảng `MonthlyCost`
- **Tài liệu gốc ghi:** Report SRS (mục chi phí tháng): "Import lại cùng tháng thì thay bản cũ, **có lịch sử**; không import vào tháng đã khóa." `NX-MTD_AI_Context.md` mục 4.7 chỉ ghi "import lại thay số tháng đó", không nhắc lịch sử.
- **Code đang làm:** `be/Application/Services/MonthlyCostService.cs` (`ImportAsync`) khi nhập lại một tháng đã có dữ liệu: gọi `IMonthlyCostRepository.RemoveItemsByPeriodIdAsync` xóa **toàn bộ** dòng `MonthlyCost` cũ của `PeriodId` đó rồi `AddItems` ghi lại các dòng hợp lệ mới, trong cùng một `SaveChangesAsync`. Không có bảng/cột nào lưu phiên bản trước đó; mỗi dòng chỉ còn `ImportedAt` (giờ nhập lần gần nhất) làm dấu vết yếu, không phải lịch sử đầy đủ (không biết ai đã nhập, không xem lại được số liệu phiên bản trước).
- **Lý do:** Schema `MonthlyCost` (đã có sẵn, không được đổi theo yêu cầu task — không migration) không có cột versioning/supersede (không giống `OperationAssignment.ValidTo` hay `GradeLevel` lưu lịch sử theo dòng). "Thay bản cũ" là cách diễn giải đơn giản nhất khớp schema thật; thêm bảng lịch sử mới là đổi schema, ngoài phạm vi MI-12. Cùng kiểu khoảng trống đã ghi ở `DEV-MI-09-2` (Payroll reopen) cho mảng kế toán.
- **Ảnh hưởng người khác:** Không ảnh hưởng bảng/luồng người khác (`MonthlyCost` do Minh sở hữu).
- **Cần sửa trong tài liệu:**
  - [ ] Report SRS: bỏ chữ "có lịch sử" ở mô tả UC12, hoặc ghi rõ "lịch sử" ở đây chỉ là `ImportedAt` của lần nhập gần nhất
  - [ ] `NX-MTD_AI_Context.md` mục 4.7: ghi rõ import lại = xóa và ghi lại, không giữ phiên bản cũ
- **Trạng thái:** Mở

---

### DEV-MI-12-2 · Đối soát MEALS chỉ là cảnh báo mềm, không có ngưỡng chênh lệch

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-12 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC12, UC30, bảng `MonthlyCost` (CostCategoryCode = MEALS), `DailyMeal.TotalAmount`
- **Tài liệu gốc ghi:** `NX-MTD_Database.sql` (comment trên bảng `MonthlyCost`): "Hóa đơn bếp cuối tháng (UC30) là một dòng CostCategoryCode = 'MEALS'; đối soát với Σ DailyMeal.TotalAmount." Không nói rõ cách tính "chênh lệch đáng kể" hay có chặn hay không.
- **Code đang làm:** `MonthlyCostService.BuildMealsReconciliationAsync` so sánh `Amount` của dòng MEALS (nếu có trong file/đã lưu) với `Σ DailyMeal.TotalAmount` trong khoảng ngày của tháng (`IMonthlyCostRepository.GetMealsTotalAmountAsync`), trả về `MealsReconciliationDto.HasDiscrepancy = true` khi **hai số khác nhau bất kỳ mức nào** (so sánh tuyệt đối `!=`, không có ngưỡng %). Đây chỉ là thông tin tham khảo trả về trong preview/import/GetByPeriodMonth — **không chặn** import khi lệch (đúng tinh thần "đối soát", không phải "khớp bắt buộc").
- **Lý do:** SRS/AI Context không định nghĩa ngưỡng "chênh lệch đáng kể"; không có khóa `SystemSetting` nào cho việc này (khác `FUEL_NORM_TOLERANCE_PERCENT` 15% đã có cho sổ xe). Thêm khóa `SystemSetting` mới là đổi dữ liệu seed của bảng người khác (Khiêm sở hữu `SystemSetting`), ngoài phạm vi MI-12 nếu không bàn trước. Chọn so sánh tuyệt đối đơn giản nhất, an toàn hơn (không bỏ sót chênh lệch nhỏ) cho tới khi có ngưỡng chính thức.
- **Ảnh hưởng người khác:** Không ảnh hưởng bảng người khác (chỉ đọc `DailyMeal`, bảng Minh sở hữu).
- **Cần sửa trong tài liệu:**
  - [ ] `NX-MTD_AI_Context.md` mục 4.7/12: thêm mục "chưa chốt" về ngưỡng chênh lệch MEALS cần cảnh báo (hiện dùng so sánh tuyệt đối)
- **Trạng thái:** Mở

---

### DEV-MI-12-3 · Tự chọn mẫu file Excel chi phí tháng; file đã nhập không được lưu lại

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-12 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC12, `be/Application/Interfaces/IExcelMonthlyCostParser.cs`, `be/Infrastructure/Services/ClosedXmlMonthlyCostParser.cs`/`ClosedXmlMonthlyCostWriter.cs`, MSG24, MSG27
- **Tài liệu gốc ghi:** Report SRS chỉ ghi "Tải file mẫu, điền chi phí tháng... xem trước dòng hợp lệ/lỗi/trùng rồi import", không nêu tên cột cụ thể của file mẫu (cùng tình huống đã ghi ở `DEV-MI-09-1` cho bảng lương).
- **Code đang làm:** Tự chọn 6 cột theo thứ tự `Loại chi phí (CostCategoryCode) | Kiểu (FIXED/VARIABLE) | Mã hàng (ProductCode, chỉ bắt buộc khi VARIABLE) | Số tiền (Amount) | Số lượng (Quantity, tùy chọn) | Ghi chú (Note, tùy chọn)`, ánh xạ thẳng vào các cột đã có của `MonthlyCost`. `GET /api/v1/accountant/monthly-costs/template` dựng sẵn file mẫu có 2 dòng ví dụ (một khoản FIXED, một khoản VARIABLE) để Kế toán biết cách điền (MSG27). File chi phí đã nhập **không được lưu lại** — không có trường tương đương `Payroll.ImportedFileUrl` trên `MonthlyCost` và cũng chưa có `IAttachmentService` (đã grep xác nhận `be/Application/Interfaces/` chưa có), giống khoảng trống đã ghi ở `DEV-MI-09-1`.
- **Lý do:** SRS chưa chốt mẫu cột; chọn cấu trúc đơn giản nhất khớp 1-1 với các cột của bảng `MonthlyCost` (không cần gộp nhiều dòng thành 1 bản ghi như Payroll/PayslipItem), để không phải đoán mẫu kế toán thật chưa tồn tại tại thời điểm code.
- **Ảnh hưởng người khác:** Không ảnh hưởng luồng người khác hiện tại; nếu sau này có mẫu Excel chi phí tháng chính thức từ phòng kế toán thì phải sửa `IExcelMonthlyCostParser`/`ClosedXmlMonthlyCostParser`/`ClosedXmlMonthlyCostWriter` cho khớp cột, và cần `IAttachmentService` (Khiêm) nếu muốn lưu lại file gốc (`Attachment.OwnerType = 'ACCOUNTING_PERIOD'` theo comment DB).
- **Cần sửa trong tài liệu:**
  - [ ] Report-3: UC12 bổ sung mô tả cột của file Excel mẫu chi phí tháng
  - [ ] `NX-MTD_AI_Context.md` mục 12: thêm "mẫu Excel chi phí tháng" vào danh sách chưa chốt cùng "mẫu Excel bảng lương"
- **Trạng thái:** Mở

---

### DEV-MI-12-4 · `MonthlyCostService` chưa ghi `AuditLog` (cùng khoảng trống đã ghi ở DEV-MI-09-3/DEV-MI-04-1/DEV-MI-16-3)

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-12 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC12, GBR-15, bảng `AuditLog`
- **Ghi chú:** Lý do và hướng xử lý giống hệt DEV-MI-09-3 (và DEV-MI-04-1, DEV-MI-16-3) — chưa có `IAuditLogService`/interface dùng chung để ghi vào bảng `AuditLog` (Khiêm sở hữu) tại thời điểm code. `MonthlyCostService.ImportAsync` (xóa + ghi lại chi phí tháng) cũng không ghi `AuditLog`. Không lặp lại toàn bộ lý do/tài liệu cần sửa ở đây — xem chi tiết tại mục DEV-MI-09-3 ở trên.
- **Ảnh hưởng người khác:** Khiêm (chủ bảng `AuditLog`) — khi `IAuditLogService` có sẵn, bổ sung ghi audit cho `MonthlyCostService` cùng đợt với `PayrollService`/`KpiService`.
- **Trạng thái:** Bỏ — đã xử lý ngày 21/10/2026 sau khi Khiêm có sẵn dịch vụ dùng chung (KH-07, `IAuditLogService` / `INotificationService`), xem test tương ứng trong `tests/Application.Tests`.

---

### DEV-MI-18-1 · Mô tả task MI-18 bị bỏ trống — tự diễn giải thành "KPI theo ngày"

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-18 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** MI-18 "KPI theo ngày" (UC19 — KPI tổ may, cùng UC đã dùng ở MI-16), `be/Application/Interfaces/IKpiService.cs`, `KpiService.cs`, `KpiTargetsController.cs`, `fe/src/features/kpi/`
- **Tài liệu gốc ghi:** `NX-MTD_Phan_cong_cong_viec.xlsx` có dòng task MI-18 tên "KPI theo ngày", phụ thuộc duy nhất là MI-16, nhưng **ô mô tả bị để trống** — không có đặc tả UC/FR nào khác nhắc riêng đến "KPI theo ngày" (chỉ có UC19 KPI tổ may chung, đã làm ở MI-16).
- **Code đang làm:** Dựa vào tên task + view sẵn có `vw_KpiDailyResult` (đã scaffold ở MI-16, dùng trong `KpiService.CloseExpiredAsync`), diễn giải MI-18 là: thêm `GET /api/v1/manager/kpi-targets/{id}/daily-results` chiếu thẳng (thin pass-through) các dòng của view theo một `KpiTargetId`, cho Quản đốc xem tiến độ từng ngày thay vì chỉ xem tổng kết ở `GetByIdAsync`. Không thêm nghiệp vụ gộp/tính toán nào khác ngoài việc này.
- **Lý do:** Đã trao đổi với người yêu cầu (Minh) trước khi code: xác nhận đây là cách diễn giải hợp lý nhất, bám sát schema, rủi ro thấp nhất cho một task mô tả trống, thay vì đoán thêm tính năng không có trong task sheet.
- **Ảnh hưởng người khác:** Không ảnh hưởng luồng người khác (chỉ đọc view đã có, không đổi schema, không đụng bảng của ai khác).
- **Cần sửa trong tài liệu:**
  - [ ] `NX-MTD_Phan_cong_cong_viec.xlsx`: điền mô tả task MI-18 theo diễn giải đã chốt ở trên
- **Trạng thái:** Mở

---

### DEV-MI-18-2 · `KpiTarget.Status`/`.Result` trả UPPER_SNAKE thay vì PascalCase

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-18 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** `KpiTarget.Status`/`.Result`, `Application/DTOs/KpiDtos.cs` (`KpiTargetDto`), `NX-MTD_Coding_Rules.md` mục 2.2, `fe/src/features/kpi/types.ts`
- **Tài liệu gốc ghi:** Coding Rules mục 2.2: "Enum (trạng thái, role) trả về dạng chuỗi PascalCase (`"Pending"`, `"Approved"`) bằng `JsonStringEnumConverter`". Một số luồng khác đã làm trước MI-18 (`Payroll.Status`, `DailyMeal.Status`...) đúng theo quy tắc này (FE khai báo `'PendingApproval'`, `'Open'`...).
- **Code đang làm:** `be/Domain/Entities/KpiTarget.cs` khai báo `Status`/`Result` là `string` thường (đã có sẵn từ MI-16, không đổi ở MI-18); `KpiStatuses`/`KpiResults` (`be/Domain/Constants/KpiConstants.cs`) là hằng chuỗi UPPER_SNAKE (`"ACTIVE"`, `"NOT_ACHIEVED"`...) gán thẳng, không qua C# enum + `JsonStringEnumConverter`. Do đó API hiện trả nguyên văn `"ACTIVE"`/`"CLOSED"`/`"CANCELLED"`/`"ACHIEVED"`/`"NOT_ACHIEVED"`, không phải `"Active"`/`"NotAchieved"`. Khi viết FE cho MI-18 (`fe/src/features/kpi/types.ts`), đã khai báo `KpiTargetStatus`/`KpiTargetResult` khớp đúng giá trị UPPER_SNAKE thật BE trả, không theo PascalCase như các feature khác, để không bị lệch runtime.
- **Lý do:** Phát hiện khi đọc lại `KpiTarget.cs`/`KpiTargetDto` lúc viết FE cho MI-18; đây là lựa chọn đã có sẵn từ MI-16 (ngoài phạm vi sửa của MI-18 — đổi sang enum + converter sẽ đụng lại `KpiService`/`KpiServiceTests` đã merge/đang dùng, rủi ro ngoài phạm vi task đọc dữ liệu này). Ghi lại để FE/BE không bị lệch giả định khi ai đó động vào field này sau.
- **Ảnh hưởng người khác:** Không ảnh hưởng bảng người khác; nếu sau này chuẩn hóa `KpiTarget.Status`/`.Result` thành C# enum + `JsonStringEnumConverter` cho đúng Coding Rules, cần sửa đồng thời `KpiService`, `KpiServiceTests`, và `fe/src/features/kpi/types.ts`/`constants.ts` (map key).
- **Cần sửa trong tài liệu:**
  - [x] Không cần sửa tài liệu — đã sửa code cho khớp Coding Rules mục 2.2 thay vì ghi ngoại lệ
- **Đã sửa (21/10/2026, cùng ngày):** `KpiService.GetPagedAsync`/`GetByIdAsync` giờ gọi `CodeText.ToPascal` trên `Status`/`Result` trước khi trả DTO (mirror `PayrollService.ToSummary`/`MealService`), và lọc `query.Status` qua `ToRawStatus` (mirror `PayrollService.ToRawStatus`) để FE gửi PascalCase vẫn khớp cột UPPER_SNAKE trong DB. Đã sửa `KpiServiceTests.CreateAsync_Valid_CreatesActiveTargetAndReturnsDto` (assert cũ so sánh với hằng UPPER_SNAKE) và `fe/src/features/kpi/types.ts`/`constants.ts` sang đúng PascalCase (`'Active'`, `'NotAchieved'`...). `KpiTarget.Status`/`.Result` vẫn là `string` thường ở tầng Domain/DB (không đổi sang C# enum — không cần thiết, các service khác trong repo cũng chỉ dùng hằng chuỗi + `CodeText.ToPascal` ở biên DTO, không dùng `JsonStringEnumConverter`). `dotnet test`: 176/176 pass.
- **Trạng thái:** Bỏ

---

### DEV-MI-13-1 · Công thức LABOUR_HOURS (giờ công quy đổi theo mã hàng) là engineering judgment call cho khoảng trống "chưa chốt"

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-13 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC11 (Hòa vốn + giá trị thặng dư), bảng `BreakEvenResult.AllocationBasis`, đọc chéo `OutputRecord`/`OrderLine` (Kiều/Thái, chỉ đọc) và `DailyTimesheet` (Minh)
- **Tài liệu gốc ghi:** `NX-MTD_AI_Context.md` mục 12 "Còn chưa chốt": "phân bổ chi phí cố định khi tính hòa vốn (`AllocationBasis` LABOUR_HOURS / OUTPUT)" — liệt kê là một điểm chưa quyết định, không nói cách tính LABOUR_HOURS. Schema `BreakEvenResult.AllocationBasis` đã có sẵn CHECK `IN ('LABOUR_HOURS','OUTPUT')` từ trước nhưng không có bảng nào lưu "giờ công theo mã hàng" trực tiếp.
- **Code đang làm:** `be/Application/Services/FinanceService.cs` (`CalculateAsync`/`ComputeLaborHoursByProductAsync`) cài đặt **cả hai** công thức, Admin chọn `AllocationBasis` mỗi lần bấm "Tính toán" (không có giá trị mặc định cố định rồi bỏ công thức kia). Với LABOUR_HOURS: với mỗi cặp (công nhân, ngày) có `OutputRecord` trong kỳ, lấy `DailyTimesheet.WorkedHours` của công nhân đó đúng ngày đó, rồi chia giờ công này cho các `ProductId` công nhân đã ghi sản lượng hôm đó theo tỉ lệ `Quantity` (vd: công nhân làm 8 giờ, ghi 60 sản phẩm mã A và 40 sản phẩm mã B trong ngày → gán 4,8 giờ cho A và 3,2 giờ cho B). Cộng dồn giờ quy đổi theo mã hàng trên toàn kỳ, tỉ lệ phân bổ = giờ quy đổi mã hàng / tổng giờ quy đổi của mọi mã hàng trong phạm vi tính (đã hoàn thiện trong tháng). Nếu một mã hàng không quy đổi được giờ công hợp lệ nào trong kỳ (không có `OutputRecord`, hoặc công nhân không có `DailyTimesheet` khớp đúng ngày) thì **fallback sang tỉ lệ OUTPUT riêng cho mã hàng đó** (chỉ mã hàng đó, các mã khác vẫn theo LABOUR_HOURS) — ghi chú trực tiếp trong code, không log một dòng `CODE_DEVIATIONS` mới cho từng lần fallback.
- **Lý do:** Đây là một khoảng trống đã được chính dự án đánh dấu "chưa chốt" (không phải AI tự ý đoán khác tài liệu đã chốt). Vì không có cột/bảng "giờ công theo mã hàng" sẵn có và không được tự thêm bảng mới (luật 0.2 AI Context), phải suy ra giờ công từ hai nguồn đã có sẵn (`OutputRecord` của Kiều, `DailyTimesheet` của chính Minh) theo tỉ lệ sản lượng trong ngày — đây là cách suy diễn đơn giản, hợp lý nhất nhưng là lựa chọn của AI, cần đội ngũ xác nhận lại sau. Không chuẩn hoá tổng tỉ lệ phân bổ về đúng 100% khi có mã hàng fallback (tổng có thể lệch 100% một chút) — chấp nhận theo đúng mô tả yêu cầu gốc của task, không tự ý thêm bước chuẩn hoá ngoài phạm vi.
- **Ảnh hưởng người khác:** Kiều (chủ `OutputRecord`) — chỉ đọc, không ghi; Thái (chủ `OrderLine`, dùng để suy ra `ProductId` từ `OutputRecord.OrderLineId`) — chỉ đọc. Không đổi dữ liệu của hai người này.
- **Cần sửa trong tài liệu:**
  - [ ] `NX-MTD_AI_Context.md` mục 12: xoá "AllocationBasis LABOUR_HOURS/OUTPUT" khỏi danh sách chưa chốt, thay bằng mô tả công thức quy đổi giờ công ở trên (hoặc công thức chính thức khác nếu đội ngũ chốt lại)
  - [ ] Report-3 SRS UC11: bổ sung mô tả cách tính LABOUR_HOURS nếu Report có nhắc tới UC11 chi tiết
- **Trạng thái:** Mở

---

### DEV-MI-13-2 · `FinanceService` chưa ghi `AuditLog` (cùng khoảng trống đã ghi ở DEV-MI-09-3/DEV-MI-04-1/DEV-MI-16-3/DEV-MI-12-4)

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-13 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC27 (Doanh thu), UC11 (Hòa vốn), GBR-15, bảng `AuditLog`
- **Ghi chú:** Lý do và hướng xử lý giống hệt DEV-MI-09-3 (và DEV-MI-04-1, DEV-MI-16-3, DEV-MI-12-4) — chưa có `IAuditLogService`/interface dùng chung để ghi vào bảng `AuditLog` (Khiêm sở hữu) tại thời điểm code. `FinanceService.CalculateAsync` (ghi thêm bộ `BreakEvenResult` mới mỗi lần tính — dữ liệu "mật" theo GBR-02, càng nên có audit trail khi có `IAuditLogService`) và `ExportRevenueAsync`/`ExportBreakEvenAsync` (xuất file có thể chứa dữ liệu mật) cũng không ghi `AuditLog`. Không lặp lại toàn bộ lý do/tài liệu cần sửa ở đây — xem chi tiết tại mục DEV-MI-09-3 ở trên.
- **Ảnh hưởng người khác:** Khiêm (chủ bảng `AuditLog`) — khi `IAuditLogService` có sẵn, bổ sung ghi audit cho `FinanceService` cùng đợt với `PayrollService`/`KpiService`/`MonthlyCostService`.
- **Ghi chú xử lý:** `CalculateAsync` đã ghi `AuditLog` (chỉ số mã hàng và cách tính, không ghi doanh thu / đơn giá CMT — D26). `ExportRevenueAsync` / `ExportBreakEvenAsync` vẫn không ghi log vì chỉ đọc dữ liệu và `CK_AuditLog_Action` không có hành động xuất file.
- **Trạng thái:** Bỏ — đã xử lý ngày 21/10/2026 sau khi Khiêm có sẵn dịch vụ dùng chung (KH-07, `IAuditLogService` / `INotificationService`), xem test tương ứng trong `tests/Application.Tests`.

---

### DEV-MI-19-1 · UC12 "Tổng hợp chi phí": thiếu bảng lương đã duyệt không chặn cả màn hình

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-19 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC12 (Tổng hợp chi phí, CHỈ Admin, GBR-02), `CostSummaryService.GetAsync`
- **Tài liệu gốc ghi:** Report-3 SRS (mục cho màn báo cáo, đã lấy nguyên văn trong brief task MI-19): "the Admin views monthly costs ... based on the Accountant's import and the approved payroll". GBR-17 (UC11 hòa vốn) chặn hẳn khi một trong hai điều kiện (bảng lương duyệt / chi phí đã nhập) chưa đủ, trả MSG25.
- **Code đang làm:** `CostSummaryService.GetAsync` vẫn trả thành công (200) khi chưa có bảng lương `APPROVED` cho tháng — chỉ đặt `payrollApproved = false`, `payrollTotal = null` và `grandTotal` không cộng phần lương; FE hiện ghi chú "Chưa có bảng lương đã duyệt cho tháng này" ngay trên ô Lương thay vì chặn cả màn hình. Màn hình vẫn tiếp tục hiện chi phí vật tư (MonthlyCost) đã nhập.
- **Lý do:** UC12 là một màn **xem báo cáo tổng hợp**, không phải một phép tính ràng buộc chặt như hòa vốn (UC11, GBR-17) — chặn cả màn hình vì thiếu một trong hai nguồn sẽ khiến Admin không xem được chi phí vật tư đã có sẵn chỉ vì Kế toán chưa duyệt lương xong, không hợp lý cho một màn hình "xem", trong khi hòa vốn là một phép tính cần cả hai số đúng để ra kết quả đúng.
- **Ảnh hưởng người khác:** Không — `CostSummaryService` là module mới, không có luồng khác phụ thuộc.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3 SRS: mục UC12, ghi rõ "chưa có bảng lương đã duyệt" chỉ ẩn/để trống phần lương, không chặn xem chi phí vật tư
- **Trạng thái:** Mở

---

### DEV-MI-19-2 · UC36 "Báo cáo kế toán": sổ xe gộp theo xe, phạm vi đếm cảnh báo khác phạm vi tính km/xăng

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-19 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC36 (Báo cáo kế toán, CHỈ Kế toán), bảng `Vehicle`/`VehicleTrip` (Thảo sở hữu, chỉ đọc), `AccountingReportService.GetVehicleReportAsync`
- **Tài liệu gốc ghi:** Task brief MI-19 nêu rõ đây là một lựa chọn mở: "per-vehicle (or per-trip, your call on granularity)"; schema `VehicleTrip` không quy định báo cáo gộp theo chiều nào. `NX-MTD_AI_Context.md` mục 4.8 chỉ nói "cảnh báo km không liền chuyến trước hoặc xăng vượt định mức 15% (MSG30)", không nói báo cáo tổng hợp gộp ra sao.
- **Code đang làm:** `AccountingReportService.GetVehicleReportAsync` gộp theo xe (`VehicleId`), không liệt kê từng chuyến. Trong một xe: `TotalKm`/`TotalFuelLiters`/`TotalFuelCost` chỉ cộng các chuyến `Status = COMPLETED` trong kỳ; còn `KmGapWarningCount`/`FuelOverNormWarningCount` đếm trên **mọi** chuyến có `TripDate` trong kỳ bất kể `Status` (kể cả `STARTED`, vì `KmGapWarning` có thể được đánh dấu ngay khi ghi chuyến, trước khi hoàn thành).
- **Lý do:** "Period summary per vehicle is more 'report'-like" (gợi ý trong task brief) — phù hợp với UC36 là báo cáo tổng hợp tháng, không phải danh sách chuyến (đã có thể xem chi tiết ở màn sổ xe khác, ngoài phạm vi MI-19). Km/xăng cần `OdometerEnd`/đã khóa `FuelLiters` nên chỉ đáng tin với chuyến đã `COMPLETED`; cảnh báo thì cần hiện ngay cả khi chuyến chưa xong để Kế toán không bỏ sót.
- **Ảnh hưởng người khác:** Thảo (chủ bảng `Vehicle`/`VehicleTrip`, vào từ Phase 3) — khi Thảo xây màn sổ xe chi tiết theo chuyến (UC35), có thể cần đối chiếu lại quy ước gộp này nếu muốn hai màn hình nhất quán.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3 SRS / `NX-MTD_Tong_hop_Use_Case.md`: UC36, ghi rõ báo cáo sổ xe là tổng hợp theo xe, không phải theo từng chuyến
- **Trạng thái:** Mở

---

### DEV-MI-19-3 · Thêm hằng số `VehicleTripStatus` cho bảng `VehicleTrip` (Thảo sở hữu)

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-19 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** Bảng `VehicleTrip.Status` (Thảo sở hữu, phân công Phase 3), `Domain/Constants/VehicleCodes.cs`
- **Tài liệu gốc ghi:** `NX-MTD_AI_Context.md` mục 0.5: "Không đụng code của luồng người khác... cần dữ liệu luồng khác thì gọi qua interface dùng chung". Mục 6.2: `Vehicle`/`VehicleTrip` thuộc Thảo.
- **Code đang làm:** Thêm file `Domain/Constants/VehicleCodes.cs` khai hai hằng số `VehicleTripStatus.Started`/`.Completed` ("STARTED"/"COMPLETED", đúng giá trị `CHECK` của schema) để `AccountingReportService` so sánh `VehicleTrip.Status` khi gộp báo cáo — không có entity/interface nào khác khai hằng số này trước MI-19 (đã grep xác nhận).
- **Lý do:** `Domain/Constants` không phải code nghiệp vụ của Thảo (không có `IVehicleService`/repository nào bị đụng tới) — chỉ là hằng số mã trạng thái đọc từ CHECK constraint có sẵn trong schema, cần thiết để tránh hard-code chuỗi "COMPLETED" theo đúng Coding Rules mục 1 (cấm magic string). Đọc `VehicleTrip`/`Vehicle` hoàn toàn read-only qua `AccountingReportRepository` riêng, không chạm bảng ghi/transaction nào của Thảo.
- **Ảnh hưởng người khác:** Thảo — khi xây UC35 (kiểm kê, sổ xe), nên dùng lại `VehicleCodes.cs` thay vì khai hằng số trùng; nếu Thảo cần thêm giá trị khác hoặc đổi quy ước, có thể sửa trực tiếp file này (không phải bảng/entity).
- **Trạng thái:** Mở

---

### DEV-MI-19-4 · `CostSummaryService`/`AccountingReportService` không ghi `AuditLog` (chỉ đọc, không phát sinh khoảng trống mới)

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-19 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC12/UC36, GBR-15, bảng `AuditLog`
- **Ghi chú:** Hai service của MI-19 chỉ đọc dữ liệu (xem báo cáo + xuất Excel), không ghi/sửa bản ghi nghiệp vụ nào, nên về nguyên tắc không bắt buộc `AuditLog` theo GBR-15 (chỉ áp dụng cho "thay đổi" dữ liệu). Ghi chú ở đây để nối vào cùng chuỗi DEV-MI-09-3/DEV-MI-04-1/DEV-MI-16-3/DEV-MI-12-4/DEV-MI-13-2 cho dễ tra cứu khi `IAuditLogService` dùng chung sẵn sàng — nếu về sau có yêu cầu audit cả hành vi "xem dữ liệu mật" (UC12), sẽ cần bổ sung ở đây.
- **Ảnh hưởng người khác:** Khiêm (chủ bảng `AuditLog`).
- **Trạng thái:** Mở

---

### DEV-MI-09-5 · File Excel bảng lương gốc lưu cục bộ, không qua `IAttachmentService`

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-09 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC31, UC32, `Payroll.ImportedFileUrl`, bảng `Attachment` (Khiêm), `IPayrollFileStore`/`LocalPayrollFileStore`
- **Tài liệu gốc ghi:** Chưa có tài liệu ghi việc Kế toán nhập lại file thì file gốc được giữ và giám đốc xem được. Report-3 UC31/UC32 chỉ mô tả import và duyệt; `DEV-MI-09-1` từng ghi `ImportedFileUrl` luôn `NULL`.
- **Code đang làm:** `PayrollService.ImportAsync` lưu nguyên byte file Kế toán tải lên qua `IPayrollFileStore` (`LocalPayrollFileStore`: thư mục `PayrollFiles:RootPath`, mặc định `App_Data/payroll-files/<yyyy-MM>/<guid>.xlsx`), ghi đường dẫn tương đối vào `Payroll.ImportedFileUrl`. Nhập lại thì file mới thay file cũ (file cũ bị xóa sau khi ghi DB thành công; ghi DB lỗi thì xóa file mới). API không bao giờ trả đường dẫn đó: `PayrollSummaryDto`/`PayrollDetailDto` chỉ có `HasImportedFile`; tải qua `GET /api/v1/{accountant|admin}/payrolls/{id}/file`. Mỗi tháng một file cho cả xưởng (nhiều sheet = nhiều tổ), nên một `ImportedFileUrl` mỗi `Payroll` là đủ.
- **Lý do:** Giám đốc cần xem đúng file Kế toán đã lập; `IAttachmentService` (Khiêm, KH-07) chưa có và bảng `Attachment` do Khiêm sở hữu (luật 0.5/0.6). File chứa lương toàn xưởng nên không đặt ở thư mục tĩnh, chỉ tải qua endpoint phân quyền.
- **Ảnh hưởng người khác:** Khiêm: `IAttachmentService` / `IFileStorage` (KH-07) đã có nhưng `IAttachmentService` chỉ nhận ảnh JPG/PNG/WEBP và `IFileStorage` chưa có thao tác xóa file, nên bảng lương chưa chuyển sang dùng. Khi `IFileStorage` có `DeleteAsync` chỉ cần đổi `LocalPayrollFileStore` (`IPayrollFileStore` giữ nguyên). Khi triển khai nhiều máy chủ/container cần trỏ `PayrollFiles:RootPath` tới ổ đĩa chia sẻ và sao lưu thư mục này cùng DB. Thư mục `App_Data` nên nằm trong `.gitignore`.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3: UC31 bổ sung "file Excel gốc được lưu lại"; UC32 bổ sung "Admin tải file gốc để xem trước khi duyệt"
  - [ ] `NX-MTD_Database.sql` / `_Giai_thich.md`: `Payroll.ImportedFileUrl` là đường dẫn tương đối trong kho file cục bộ, không phải URL
- **Trạng thái:** Mở

---

### DEV-MI-09-6 · Import theo file "Bảng thanh toán lương" thật của xưởng

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-09 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC31, UC06, `IExcelPayrollParser`/`ClosedXmlPayrollParser`, `PayslipLayout`, `PayslipItem`, `Employee.AttendanceCode`
- **Tài liệu gốc ghi:** Chưa có tài liệu ghi cấu trúc file (xem `DEV-MI-09-1`, `DEV-MI-09-4`); `NX-MTD_AI_Context.md` mục 12 vẫn ghi mẫu Excel bảng lương "chưa chốt". Thay thế mẫu 6 cột của `DEV-MI-09-1`.
- **Code đang làm:** Theo file mẫu tháng 7/2026 (Tổ may 1, Cắt) và đã chốt với Minh:
  - **Cấu trúc:** mỗi tổ một sheet; sheet nào có ô "SLĐ" ở 25 dòng đầu là sheet lương, sheet khác bỏ qua. Tiêu đề 2 dòng; cột nhận theo TÊN (mỗi tổ bộ cột khác nhau), chỉ đọc các cột bên trái "Thực lĩnh"/"Còn lĩnh" (vùng "Bảng phụ" bên phải bị bỏ). Cột mã hàng (giữa "Thưởng PL" và "Tổng các mã") nhận động, mỗi mã một dòng `ADDITION`. Dừng ở dòng "Tổng cộng" nên bảng nhỏ bên dưới (thời vụ, VD "Huỳnh Triệu Vi") không bị đọc — người thời vụ không tính vào hệ thống.
  - **Mã nhân viên:** cột SLĐ là mã chấm công, tra theo `Employee.AttendanceCode` (không phải `EmployeeCode`).
  - **Đơn vị:** dòng "ĐVT: 1000 Đ" → cột tiền nhân 1000 sang đồng; số ngày/giờ công không nhân.
  - **Số tổng không lưu:** "Tổng các mã", "Tổng lương sản phẩm", "Tổng cộng" và "Trả BHXH 21.5%" (phần công ty đóng, hiện chỉ tính thực lĩnh) bị bỏ qua để không cộng hai lần. Thực lĩnh = tổng khoản cộng − BHXH 10,5% − thuế, lấy đúng số trong file (hệ thống không tính lương, GBR-12).
  - **Danh mục cột đặt trong code** (`be/Application/Common/PayslipLayout.cs`): tên cột → `ItemGroup` (cộng/trừ/BHXH/INFO) + nhóm hiển thị theo mẫu phiếu giấy. `PayslipItem` chỉ có 4 giá trị `ItemGroup` và không có cột nhóm hiển thị (schema không đổi). Cột lạ ngoài vùng mã hàng bị bỏ qua và chỉ lộ ra qua cảnh báo đối chiếu tổng.
  - **Bậc thợ (cột Phân loại):** `Payslip`/`PayslipItem` không có chỗ lưu chữ, nên lưu thành dòng `INFO` có `ItemName = "Phân loại: A"`, `Amount = 0`; BE tách lại thành tên + `TextValue` khi trả cho FE. Lấy từ file (không lấy `Employee.CurrentGradeCode`) vì bảng lương đã duyệt là chứng từ; khác bậc hiện tại thì cảnh báo.
  - **Cảnh báo không chặn:** tổng các khoản lệch Thực lĩnh quá 1.000 đ (Excel làm tròn từng ô) hoặc khác bậc thợ hệ thống — dòng vẫn `Valid`, ghi chú hiển thị ở bảng xem trước.
  - **Xem trước theo nhân viên:** mỗi dòng xem trước là một nhân viên (không còn mỗi dòng một khoản như `DEV-MI-09-1`); "Trùng" = cùng SLĐ xuất hiện lần hai trong file. Ô công thức lỗi (#REF!...) làm riêng dòng đó lỗi.
  - **Phạm vi nhập lại:** vẫn thay toàn bộ phiếu lương của tháng (`RemovePayslipsAsync`) vì một file/tháng cho cả xưởng; không có import cộng dồn theo tổ.
- **Lý do:** Mẫu 6 cột của `DEV-MI-09-1` không dùng được với file thật (~40 cột, nhiều sheet, bộ cột khác nhau theo tổ). Ép Kế toán đổi sang mẫu giả định là thêm việc thủ công và dễ sai.
- **Ảnh hưởng người khác:** Kiều/Thái: không. Phiếu lương công nhân (UC06) đổi cách hiển thị: nhóm theo mẫu phiếu giấy thay vì 4 nhóm cộng/trừ/BH/thông tin (FE `PayslipItemsView`; DTO `PayslipItemDto` thêm `Section`/`Unit`/`TextValue`). Khi xưởng thêm cột mới vào file (ngoài vùng mã hàng) phải bổ sung vào `PayslipLayout`. File mẫu đã kiểm tra chỉ là bản Google Sheets toàn ô `#ERROR!/#REF!`; parser đã test bằng workbook dựng tay, **chưa chạy thử trên file .xlsx thật có số** — cần Kế toán đưa một file xuất từ Excel để kiểm lần cuối.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3 UC31: mô tả đúng file "Bảng thanh toán lương" (một sheet mỗi tổ, cột SLĐ, ĐVT nghìn đồng, các nhóm cột thật)
  - [ ] `NX-MTD_AI_Context.md` mục 12: bỏ "mẫu Excel bảng lương chưa chốt", ghi cấu trúc thật và quy tắc bỏ qua số tổng
  - [ ] Report-3 UC06: phiếu lương công nhân hiển thị theo nhóm của mẫu phiếu giấy
- **Trạng thái:** Mở

---

### DEV-TH-12-1 · Đơn mua NVL không bắt buộc gắn đơn hàng

- **Ngày / Người / Task / PR:** 21/10/2026 · Thái · TH-12 · (chưa mở PR)
- **Ảnh hưởng tới:** bảng `PurchaseOrder` (`OrderId`), `PurchaseOrderLine` (`OrderMaterialId`, thêm `MaterialId`, `MaterialColor`), `MaterialReceipt` (`OrderId`), `MaterialReceiptLine` (`OrderMaterialId`, thêm `MaterialId`, `MaterialColor`); unique `UQ_POL`, `UQ_MRL`; AI Context mục 5 (luồng NVL), phân công Minh (đơn mua)
- **Tài liệu gốc ghi:** `PurchaseOrder.OrderId` bắt buộc, mỗi dòng đơn mua trỏ vào `OrderMaterial` của một đơn hàng; thiếu NVL thì xin khách trước, khách không cấp mới mua và Quản đốc tạo `PurchaseOrder` cho đơn đó.
- **Code đang làm:** `OrderId` và `OrderMaterialId` cho phép NULL. Đơn mua không có `OrderId` là đơn mua NVL dùng chung nhiều đơn (kim, chỉ...); dòng của đơn này xác định NVL bằng `MaterialId` + `MaterialColor`. Có CHECK `CK_POL_Target` / `CK_MRL_Target` (một dòng chỉ theo một trong hai cách) và unique dạng filtered (`UQ_POL`, `UQ_POL_Stock`, `UQ_MRL`, `UQ_MRL_Stock`). Đơn mua có `OrderId` vẫn như tài liệu (dòng phải khớp NVL đã khai báo của đơn). Kho NVL tính theo (NVL, màu) nên không phải sửa `IStockService`. API: `GET/POST /api/v1/manager/purchase-orders`, đơn tạo xong ở `PENDING_FINALISATION` / `UNPAID`. FE: màn "NVL của đơn", khi chưa chọn đơn thì nút Tạo mở form đơn mua dùng chung (chỉ Quản đốc).
- **Lý do:** Quản đốc cần mua kim, chỉ dùng cho nhiều đơn mà không thể chờ Admin tạo mã đơn hàng cho từng lần mua (yêu cầu của Thái ngày 21/10/2026).
- **Ảnh hưởng người khác:** Minh (sở hữu `PurchaseOrder`, NCC, thanh toán): service đơn mua đã có phần tạo/xem, Minh nối tiếp bước chọn NCC, chốt đặt hàng và thanh toán; `OrderId` của đơn có thể NULL nên mọi màn/báo cáo theo đơn phải xử lý đơn không có đơn hàng. Thái (TH-12): phiếu nhập từ NCC cho đơn mua dùng chung chưa làm (hiện chỉ nhận nguồn khách cấp, vẫn bắt buộc `OrderId`).
- **Cần sửa trong tài liệu:**
  - [ ] `NX-MTD_Database.sql` / `_Giai_thich.md`: các cột trên, CHECK và unique mới
  - [ ] `NX-MTD_AI_Context.md` mục 5 (luồng NVL): thêm trường hợp đơn mua dùng chung; mục 6.2 nếu đổi người sở hữu
  - [ ] Report-3: UC đơn mua / thiếu NVL (luồng thay thế mua dùng chung), mockup màn "NVL của đơn"
  - [ ] `NX-MTD_Tong_hop_Use_Case.md`: UC liên quan đơn mua
- **File migrate:** đã gộp vào `SQL/NX-MTD_Database.sql` (xem DEV-MI-20-3); file `SQL/Migrations/20261021_01_TH-12_stock-purchase-order.sql` đã xóa
- **Trạng thái:** Mở

### DEV-TH-12-2 · Quản đốc nhập tên NVL mới khi tạo đơn mua

- **Ngày / Người / Task / PR:** 21/10/2026 · Thái · TH-12 · (chưa mở PR)
- **Ảnh hưởng tới:** bảng `Material`; AI Context mục 2 (vai trò), màn Danh mục NVL
- **Tài liệu gốc ghi:** Danh mục NVL do Thủ kho quản lý (menu "Danh mục NVL" chỉ role Thủ kho); đơn mua chỉ chọn NVL đã có.
- **Code đang làm:** Trong form đơn mua dùng chung, Quản đốc chọn NVL có sẵn hoặc gõ tên NVL chưa có kèm đơn vị. Khi lưu đơn, hệ thống dùng lại NVL phụ liệu trùng tên (không phân biệt hoa thường), nếu chưa có thì tạo `Material` mới: `MaterialType = TRIM`, `Category = OTHER`, mã tự sinh `PL-xxxx`. NVL được tạo trước khi đơn mua lưu; nếu lưu đơn lỗi thì NVL mới vẫn còn trong danh mục.
- **Lý do:** Kim, chỉ... mua lần đầu thường chưa có trong danh mục; bắt Quản đốc chờ Thủ kho thêm danh mục rồi mới tạo được đơn làm chậm việc mua.
- **Ảnh hưởng người khác:** Thủ kho thấy NVL mới trong Danh mục NVL, có thể sửa nhóm/đơn vị.
- **Cần sửa trong tài liệu:**
  - [ ] `NX-MTD_AI_Context.md` mục 2: Quản đốc được thêm NVL phụ liệu (nhóm OTHER) khi tạo đơn mua
  - [ ] Report-3: UC quản lý danh mục NVL / UC đơn mua
- **Trạng thái:** Mở

---

### DEV-KH-07-1 · Ảnh lưu khóa lưu trữ, tải qua API

- **Ngày / Người / Task / PR:** 22/10/2026 · Khiêm (Thái làm hộ) · KH-07 · (chưa mở PR)
- **Ảnh hưởng tới:** bảng `Attachment` (cột `FileUrl`)
- **Tài liệu gốc ghi:** `NX-MTD_Database.sql`: `FileUrl -- đường dẫn S3`.
- **Code đang làm:** `FileUrl` lưu khóa lưu trữ dạng `REQUEST/2026/10/<guid>.jpg`; môi trường dev lưu trên ổ đĩa server (`App_Data/uploads`); ảnh chỉ tải qua `GET /api/v1/common/attachments/{id}/content` có kiểm quyền theo loại chứng từ. Chỉ nhận JPG / PNG / WEBP ≤ 5 MB. Gỡ ảnh = `IsCurrent = 0`.
- **Lý do:** Ảnh chứng từ (thanh toán, phiếu ký) không được có link công khai; S3 làm ở KH-25 qua cùng `IFileStorage`.
- **Ảnh hưởng người khác:** Minh (MI-07), Thái (TH-14), Kiều – dùng `IAttachmentService`, đăng ký `IAttachmentOwnerAccess` cho loại chứng từ của mình.
- **Cần sửa trong tài liệu:**
  - [ ] `NX-MTD_Database.sql` / `_Giai_thich.md`: comment cột `FileUrl`
- **Trạng thái:** Mở

---

### DEV-KH-06-1 · Tổ may cũng tạo được đơn xin NVL

- **Ngày / Người / Task / PR:** 22/10/2026 · Khiêm (Thái làm hộ) · KH-06 · (chưa mở PR)
- **Ảnh hưởng tới:** UC07, bảng `Request` (`RequestTypeCode = MATERIAL_REQUISITION`)
- **Tài liệu gốc ghi:** Report-3 UC07 và `NX-MTD_Tong_hop_Use_Case.md` UC07: phiếu xin NVL do tổ trưởng **tổ cắt** lập.
- **Code đang làm:** Tổ trưởng / tổ phó tổ **cắt và tổ may** đều tạo được `MATERIAL_REQUISITION` (tổ may xin phụ liệu: chỉ, khóa, nhãn…), Thủ kho xử lý.
- **Lý do:** `NX-MTD_AI_Context.md` 4.8 (19/10) đã chốt NVL gồm cả phụ liệu và tổ may cần xin phụ liệu; AI Context thắng Report-3.
- **Ảnh hưởng người khác:** Thái (TH-05: phiếu cấp phải nhận đơn của tổ may).
- **Cần sửa trong tài liệu:**
  - [ ] Report-3: UC07 (tác nhân của loại đơn xin NVL), Permission Matrix
  - [ ] `NX-MTD_Tong_hop_Use_Case.md` UC07
- **Trạng thái:** Mở

### DEV-KH-06-2 · Tổ phó luôn có quyền như tổ trưởng

- **Ngày / Người / Task / PR:** 22/10/2026 · Khiêm (Thái làm hộ) · KH-06 · (chưa mở PR)
- **Ảnh hưởng tới:** UC07, UC08, R1, view `vw_TeamActingLeader`
- **Tài liệu gốc ghi:** `NX-MTD_AI_Context.md` mục vai trò: tổ phó có quyền tổ trưởng **khi tổ trưởng vắng** (`vw_TeamActingLeader`).
- **Code đang làm:** Tổ phó luôn có quyền như tổ trưởng trong tổ mình (tạo / xem / sửa / hủy đơn của tổ), không đọc `vw_TeamActingLeader`.
- **Lý do:** Giữ thống nhất với runbook đăng nhập (D2); giới hạn theo giờ vào cần dữ liệu chấm công thật (MI-06). Sẽ làm ở KH-24.
- **Ảnh hưởng người khác:** Thái, Minh, Kiều (mọi màn tổ trưởng) – cùng cách kiểm `Roles.IsTeamRole`.
- **Cần sửa trong tài liệu:** không (tạm thời); đổi trạng thái `Bỏ` khi KH-24 xong.
- **Trạng thái:** Mở

---

### DEV-KH-08-1 · Ảnh mẫu mã hàng nhập đường dẫn

- **Ngày / Người / Task / PR:** 23/10/2026 · Khiêm (Thái làm hộ) · KH-08 · (chưa mở PR)
- **Ảnh hưởng tới:** UC10, bảng `Product` (`SampleImageUrl`), `Attachment` (`OwnerType`)
- **Tài liệu gốc ghi:** UC10: thêm / sửa mã hàng gồm "ảnh mẫu".
- **Code đang làm:** Ô "Ảnh mẫu" là đường dẫn ảnh (http/https) người dùng dán vào; chưa có upload.
- **Lý do:** `CK_Attachment_OwnerType` không có `PRODUCT`; không đổi schema trong Phase 1.
- **Ảnh hưởng người khác:** không.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3: UC10 / màn 6 (ghi rõ ảnh mẫu là đường dẫn) – hoặc thêm `PRODUCT` vào `OwnerType` ở Phase sau rồi đổi trạng thái `Bỏ`
- **Trạng thái:** Mở

---

### DEV-KH-10-1 · Chuyển tổ chỉ cho ngày hiệu lực ≤ hôm nay

- **Ngày / Người / Task / PR:** 24/10/2026 · Khiêm (Thái làm hộ) · KH-10 · (chưa mở PR)
- **Ảnh hưởng tới:** UC18, bảng `Employee` (`TeamId`), `AuditLog`
- **Tài liệu gốc ghi:** UC18: "chuyển CN giữa các tổ có ngày hiệu lực".
- **Code đang làm:** Ngày hiệu lực chọn được nhưng không được sau hôm nay; `Employee.TeamId` đổi ngay khi lưu; ngày hiệu lực ghi trong `AuditLog.Reason`. Không hẹn chuyển tổ trong tương lai.
- **Lý do:** Schema chỉ có tổ hiện tại (`Employee.TeamId`), không có bảng lịch sử / hẹn lịch chuyển tổ; không đổi schema trong Phase 1.
- **Ảnh hưởng người khác:** Minh (chấm công, lương theo tổ), Kiều (phân công công đoạn) – tổ đổi ngay lúc lưu.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3 + `NX-MTD_Tong_hop_Use_Case.md` UC18: ghi rõ ngày hiệu lực ≤ hôm nay
- **Trạng thái:** Mở

### DEV-KH-10-2 · Bổ nhiệm tổ trưởng / tổ phó

- **Ngày / Người / Task / PR:** 24/10/2026 · Khiêm (Thái làm hộ) · KH-10 · (chưa mở PR)
- **Ảnh hưởng tới:** UC16, UC18, bảng `Employee` (`RoleId`), trigger `trg_Employee_TeamLeadership`
- **Tài liệu gốc ghi:** UC18: Quản đốc / Admin "bổ nhiệm tổ trưởng"; UC16: Quản đốc "không đổi vai trò".
- **Code đang làm:** Màn Tổ có "Bổ nhiệm tổ trưởng / tổ phó" cho Admin và Quản đốc. Người được bổ nhiệm phải là Công nhân / Tổ trưởng / Tổ phó đang ở tổ đó. Người đang giữ chức (nếu có) tự chuyển về vai trò Công nhân, giữ kiểu lương và bậc. Đây là cách duy nhất Quản đốc đổi vai trò; form nhân viên của Quản đốc không có ô vai trò. Mọi người bị đổi vai trò phải đăng nhập lại.
- **Lý do:** Gộp UC16 + UC18 cho nhất quán; trigger cho phép tối đa 1 tổ trưởng + 1 tổ phó nên phải hạ người cũ trước. **Chờ Morgan xác nhận** trước khi merge.
- **Ảnh hưởng người khác:** Minh (lương của người bị hạ vẫn theo `PayType` cũ), Kiều (màn tổ trưởng của người mới sau khi đăng nhập lại).
- **Cần sửa trong tài liệu:**
  - [ ] Report-3 UC16 (ngoại lệ bổ nhiệm), UC18 (luồng bổ nhiệm, có tổ phó)
  - [ ] `NX-MTD_AI_Context.md` mục vai trò
- **Trạng thái:** Mở

---

### DEV-MI-07-1 · Nhà cung cấp, chốt đặt hàng, thanh toán đơn mua: các quyết định ngoài tài liệu

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-07 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC14, UC23, UC34, GBR-18, bảng `Supplier`, `PurchaseOrder` (`GoodsStatus`, `PaymentStatus`, các cột thanh toán), `PurchaseOrderLine`, `Attachment` (`OwnerType = PURCHASE_ORDER`, `Purpose = PAYMENT_PROOF`)
- **Tài liệu gốc ghi:** `NX-MTD_AI_Context.md` mục 4.1 (4): "Quản đốc tạo `PurchaseOrder`, chọn NCC ACTIVE, đặt hàng; Kế toán xác nhận thanh toán ngay trên PO (`PaymentDate`, `PaidAmount`, `PaymentMethod`, ảnh chứng từ trong `Attachment`), không sửa NCC/số lượng (GBR-18)." Schema có `PaymentStatus = RETURNED` + `PaymentReturnReason` ("Kế toán trả lại Quản đốc") nhưng tài liệu không mô tả Quản đốc làm gì sau khi bị trả lại. Bảng phân công MI-07: "Kế toán xác nhận thanh toán + ảnh chứng từ".
- **Code đang làm:** (`be/Application/Services/Purchasing/`, API `manager/purchase-order-tracking`, `accountant/purchase-payments`, `admin/suppliers`, `manager/suppliers`; FE `features/purchase`)
  - **Đặt hàng (Quản đốc):** `PENDING_FINALISATION → ORDERED`, phải gửi đủ mọi dòng của đơn (không thêm / bớt), số lượng > 0, đơn giá > 0, ngày đặt ≤ hôm nay, ngày giao ≥ ngày đặt, nhà cung cấp phải `ACTIVE`. Đơn mua do Thái tạo có đơn giá mặc định 0 nên đơn giá chốt ở bước này. Sau khi đặt hàng, số lượng và nhà cung cấp bị khóa (GBR-18).
  - **Hủy đơn mua:** chỉ khi chưa đặt hàng, dùng `GoodsStatus = REJECTED` + `RejectReason` (bắt buộc lý do) vì schema không có trạng thái "hủy" riêng.
  - **Ảnh chứng từ thanh toán không bắt buộc** (Minh chốt ngày 21/10/2026, vd. trả tiền mặt): Kế toán xác nhận được khi không có ảnh; ảnh tối đa 5, chỉ Kế toán thêm / gỡ khi đơn đang chờ thanh toán, Admin / Quản đốc / Kế toán xem được.
  - **Trả lại → sửa đơn giá → gửi lại:** Kế toán trả lại (`UNPAID → RETURNED`, bắt buộc lý do); Quản đốc chỉ sửa được **đơn giá và ghi chú** (số lượng, nhà cung cấp vẫn khóa) rồi gửi lại (`RETURNED → UNPAID`). Đây là bước tự thêm để luồng không bế tắc sau khi bị trả lại.
  - **Thanh toán:** chỉ xác nhận khi đơn đã đặt hàng (`ORDERED / PARTIALLY_RECEIVED / RECEIVED / CLOSED_SHORT`) và đang `UNPAID`; một lần, không hoàn tác `PAID`. Ngày thanh toán ≤ hôm nay; số tiền nhập tay, có thể khác giá trị đơn (FE chỉ cảnh báo), vì schema chỉ lưu một lần thanh toán.
  - **Thông báo theo vai trò (`INotificationService`):** đặt hàng / gửi lại → Kế toán; thanh toán / trả lại → Quản đốc. Gửi cho mọi người cùng vai trò vì `PurchaseOrder` không lưu người tạo.
  - **AuditLog** (`IAuditLogService`) cho đặt hàng, hủy, sửa đơn giá, thanh toán, trả lại và mọi thay đổi nhà cung cấp, cùng transaction với dữ liệu (GBR-15).
  - Admin chỉ quản lý danh mục nhà cung cấp (thêm / sửa / ngừng hợp tác), không thao tác trên đơn mua.
- **Lý do:** Tài liệu dừng ở mức "Quản đốc đặt hàng, Kế toán thanh toán"; các điều kiện trên là phần tối thiểu để các ràng buộc DB (`CK_PO_Ordered`, `CK_PO_Paid`, `CK_PO_PayReturn`, `CK_PO_Reject`) và GBR-18 luôn đúng và luồng không bế tắc.
- **Ảnh hưởng người khác:** Thái (TH-12/TH-13): phiếu nhập từ NCC chỉ nên nhận đơn mua đã đặt hàng (`GoodsStatus` trong nhóm `Placed` ở `PurchaseGoodsStatuses`); các trạng thái `PARTIALLY_RECEIVED / RECEIVED / CLOSED_SHORT` vẫn do luồng nhập kho của Thái cập nhật, MI-07 chỉ đọc. Khiêm: thêm `IAttachmentOwnerAccess` cho `PURCHASE_ORDER`, thêm khối mới vào `Messages` / `ErrorCodes`, không sửa code có sẵn.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3 UC23 / UC34: bổ sung bước Kế toán trả lại, Quản đốc sửa đơn giá và gửi lại; ghi rõ ảnh chứng từ không bắt buộc
  - [ ] `NX-MTD_AI_Context.md` mục 4.1 (4): thêm điều kiện đặt hàng (đơn giá > 0, NCC `ACTIVE`) và luồng RETURNED
  - [ ] `NX-MTD_Database_Giai_thich.md`: `PurchaseOrder.PaymentStatus = RETURNED` → Quản đốc chỉ sửa đơn giá; `Attachment` `PAYMENT_PROOF` không bắt buộc
- **Trạng thái:** Mở

---

### DEV-KI-00-1 · Minh làm hộ phần BE của Kiều (KI-01, 03, 04, 06, 08, 10, 11)

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) làm hộ Kiều · KI-01, KI-03, KI-04, KI-06, KI-08, KI-10, KI-11 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC25, UC26, UC52, UC55, UC56, UC57, UC22; bảng `Operation`, `PieceRate`, `OperationAssignment`, `OutputRecord`, `DefectRecord`, `QcBatch`; `be/Application/Services/Production/`, `IOutputQueryService`
- **Tài liệu gốc ghi:** Bảng phân công: các task trên do Kiều làm (Phase 1–2); MI-10 và MI-17 của Minh phụ thuộc KI-10.
- **Code đang làm:** Kiều chưa đẩy nhánh nào lên GitHub, Minh (có Claude) làm phần **BE** của các task cấp dữ liệu cho lương tạm tính, KPI, tiến độ: công đoạn + đơn giá có duyệt, phân công, sản lượng cuối ca, QC, hàng hỏng, `IOutputQueryService`, hạn sửa (KI-11). Đăng ký ở `ProductionModule` (một dòng trong `Program.cs`). **Chưa làm:** toàn bộ FE của Kiều (KI-02, KI-05, FE của KI-06/KI-08, KI-14), KI-07 (nối trần với BTP thật), KI-09 (đơn cắt bù), test của Kiều (KI-18/19).
- **Lý do:** Chặn tiến độ MI-10 / MI-17 và các luồng UC21, UC44 của người khác nếu không có dữ liệu sản lượng; được chủ dự án yêu cầu coi các task liên quan là của Minh.
- **Ảnh hưởng người khác:** Kiều: khi bắt đầu code phải đọc phần đã có, không viết lại; phần FE còn nguyên của Kiều. Thái (TH-11): lấy số QC đạt qua `IOutputQueryService.GetQcFinishedByOrderLineAsync`. Khiêm (KH-16): tiến độ đơn lấy qua `GetOrderLineProgressAsync`.
- **Cần sửa trong tài liệu:**
  - [ ] Bảng phân công: ghi rõ Minh đã làm hộ BE các task KI-01/03/04/06/08/10/11
- **Trạng thái:** Mở

### DEV-KI-04-1 · Trần sản lượng cấp tổ khi công nhân chưa có phiếu giao BTP

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) làm hộ Kiều · KI-04 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC55, MSG16, `vw_WorkerOutputCap`, `vw_TeamPieceHolding`, `WorkerPieceMovement` (TH-19, Thái)
- **Tài liệu gốc ghi:** `NX-MTD_AI_Context.md` mục 4.3 (5): "Trần theo `vw_WorkerOutputCap` (MSG16)". Sheet KI-04: "trần ≤ BTP đã nhận (tạm dùng tồn seed)".
- **Code đang làm:** `OutputService` áp **hai** trần: (1) cấp tổ — Σ sản lượng của một công đoạn trên một dòng đơn ≤ BTP tổ đã nhận (`vw_TeamPieceHolding.ReceivedFromWarehouse`); (2) cấp công nhân — chỉ khi công nhân đã có phiếu giao BTP (`vw_WorkerOutputCap` có dòng): ≤ số BTP công nhân đang giữ. Tổ chưa nhận BTP nào thì chặn mọi sản lượng của dòng đơn đó.
- **Lý do:** Luồng giao / thu hồi BTP với công nhân (`WorkerPieceMovement`, TH-19) là Phase 3 nên `vw_WorkerOutputCap` đang rỗng; chỉ dùng trần công nhân thì không có trần nào, còn bỏ qua trần thì sản lượng vượt BTP thật.
- **Ảnh hưởng người khác:** Thái (TH-19): khi có phiếu giao BTP, trần theo công nhân tự có hiệu lực, không phải sửa thêm ở đây. Thái (TH-08): tổ chỉ nhập được sản lượng sau khi Thủ kho cấp BTP và tổ xác nhận.
- **Cần sửa trong tài liệu:**
  - [ ] `NX-MTD_AI_Context.md` mục 4.3 (5): ghi thêm trần cấp tổ khi công nhân chưa có phiếu giao
- **Trạng thái:** Mở (đổi `Bỏ` khi TH-19 xong và muốn chỉ dùng trần công nhân)

### DEV-KI-06-1 · "Hàng trên chuyền" của QC và hạn sửa theo ca

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) làm hộ Kiều · KI-06 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC57, MSG19, `QcBatch.EditDeadline`, `SystemSetting` (`LUNCH_START_TIME`, `WORK_END_TIME`)
- **Tài liệu gốc ghi:** AI Context 4.3 (7): "trần ≤ hàng trên chuyền (MSG19)"; "`QcBatch` đến hết ca". Không định nghĩa "hàng trên chuyền" và "hết ca".
- **Code đang làm:** Hàng trên chuyền (theo tổ + dòng đơn) = BTP tổ đã nhận − Σ số QC đạt − Σ Scrap chưa hủy (Rework không trừ vì sẽ được sửa rồi QC lại). Hết ca: sáng = đầu nghỉ trưa (`LUNCH_START_TIME`, mặc định 11:30), chiều = hết giờ làm (`WORK_END_TIME`, mặc định 16:30), tăng ca = hết ngày làm việc đó.
- **Lý do:** Cần một định nghĩa nhất quán với `vw_OrderLineProgress`; giờ ca đặt trong `SystemSetting` như quy tắc 0.8 (không hard-code).
- **Ảnh hưởng người khác:** Thái (TH-11): tổng bàn giao thành phẩm ≤ số QC đạt, lấy qua `IOutputQueryService`.
- **Cần sửa trong tài liệu:**
  - [ ] `NX-MTD_AI_Context.md` mục 4.3 (7): định nghĩa "hàng trên chuyền" và giờ hết ca
- **Trạng thái:** Mở

### DEV-KI-08-1 · Phiếu Scrap chưa sinh đơn cắt bù

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) làm hộ Kiều · KI-08, KI-09 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC56, UC07 (cắt bù), bảng `Request` (`REPLACEMENT_CUTTING`), `DefectRecord.RequestId`
- **Tài liệu gốc ghi:** AI Context 4.3 (6): Scrap "sinh yêu cầu cắt bù `REPLACEMENT_CUTTING` → Quản đốc → lệnh cắt bù".
- **Code đang làm:** `DefectService` ghi phiếu Rework / Scrap, khóa `ScrapUnitRate`, báo công nhân, cho hủy / phản hồi; **không** tạo `Request` cắt bù (KI-09 chưa làm theo thỏa thuận). Tổ trưởng muốn cắt bù hiện tạo đơn tay ở màn Đơn từ.
- **Lý do:** KI-09 đụng khung đơn từ (Khiêm) và lệnh cắt bù (Thái, TH-10); làm sau để thống nhất `IRequestTypeHandler` cho `REPLACEMENT_CUTTING` với họ.
- **Ảnh hưởng người khác:** Thái (TH-10: lệnh cắt bù có `SourceRequestId`), Khiêm (`RequestTypePolicies`).
- **Cần sửa trong tài liệu:** không (tạm thời); đổi `Bỏ` khi KI-09 xong.
- **Trạng thái:** Mở

### DEV-KI-11-1 · Cửa sổ sửa (UC22) làm theo từng thao tác, không có màn riêng

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) làm hộ Kiều · KI-11 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC22, GBR-09, MSG20, `OutputRecord`, `DefectRecord`, `QcBatch`, `AuditLog`
- **Tài liệu gốc ghi:** AI Context 4.3 (9): sửa đến hết ngày làm việc kế tiếp (tổ trưởng), hết ca (QA/QC); sau hạn chỉ Quản đốc sửa, bắt buộc lý do, ghi `AuditLog` (UC22).
- **Code đang làm:** Cùng một quy tắc (`ProductionGuard.CheckEditWindow`) áp trong từng service: trong hạn người nhập sửa; quá hạn → tổ trưởng / QA-QC nhận lỗi 403 `PRODUCTION_EDIT_DEADLINE_PASSED` (MSG20); Quản đốc sửa được kèm lý do bắt buộc (400 nếu thiếu), mọi thay đổi ghi `AuditLog` (cả lý do). Quản đốc dùng các endpoint `manager/output-records`, `manager/qc-batches`, `manager/defect-records` (sửa sản lượng, sửa / xóa đợt QC, hủy phiếu lỗi). Hạn của sản lượng / phiếu lỗi tính bằng cách port `fn_NextWorkingDayEnd` sang C# (bỏ Chủ nhật).
- **Lý do:** Quy tắc chung nên đặt một chỗ; không làm màn "sửa quá hạn" riêng vì các màn nhập của Kiều (FE) chưa có.
- **Ảnh hưởng người khác:** Kiều (FE): màn nhập cần hiển thị `canEdit` / `editDeadline` server trả về; màn của Quản đốc cần ô lý do khi `canEdit` do quá hạn.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3 UC22: ghi cách Quản đốc sửa sau hạn theo từng loại chứng từ
- **Trạng thái:** Mở

### DEV-MI-10-1 · Chi tiết lương tạm tính theo ngày đọc trực tiếp OutputRecord / DefectRecord

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-10 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC59, `IOutputQueryService`, `OutputRecord`, `DefectRecord`
- **Tài liệu gốc ghi:** Sheet MI-10: "SL × đơn giá khóa − scrap, chỉ tổ may". `IOutputQueryService` (KI-10) là nguồn dùng chung cho UC59.
- **Code đang làm:** Tổng theo ngày lấy từ `IOutputQueryService.GetWorkerDailyPayAsync` (`vw_WorkerDailyEstimatedPay`). Màn mở rộng một ngày (từng công đoạn / ca / từng dòng Scrap) vẫn đọc thẳng `OutputRecord` / `DefectRecord` của chính công nhân trong `WorkerPayService.GetMyDayDetailAsync` vì view không có mức chi tiết đó. Người không phải công nhân khoán sản phẩm nhận danh sách rỗng (view đã lọc `PIECE_RATE`).
- **Lý do:** Không đổi view / schema; chi tiết theo công đoạn là phần riêng của màn này.
- **Ảnh hưởng người khác:** Kiều: nếu đổi cột `OutputRecord` / `DefectRecord` thì cập nhật hàm này.
- **Cần sửa trong tài liệu:** không.
- **Trạng thái:** Mở

### DEV-MI-17-1 · Màn KPI: % đạt tính trên cả kỳ; Admin xem thêm

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-17 · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC19 (KPI tổ may), UC21, `KpiTarget`, `vw_KpiDailyResult`, phân quyền `admin/kpi-targets`
- **Tài liệu gốc ghi:** Sheet MI-17: "Chỉ tiêu vs thực tế theo tổ (đọc IOutputQueryService)", vai trò "Quản đốc, Admin". Không nói "thực tế" tính trên khoảng nào.
- **Code đang làm:** Thực tế = Σ hàng hoàn thiện của cả kỳ (`vw_KpiDailyResult`, đọc qua `IOutputQueryService.GetKpiFinishedTotalsAsync`); % đạt = thực tế / chỉ tiêu cả kỳ × 100 (không so với số ngày đã trôi qua; theo từng ngày xem ở bảng MI-18). Admin chỉ **xem** (`admin/kpi-targets`), không lập / sửa / hủy.
- **Lý do:** Cùng cách `KpiService.CloseExpiredAsync` đã dùng để suy ra Đạt / Không đạt; tránh hai định nghĩa "đạt" khác nhau.
- **Ảnh hưởng người khác:** Khiêm (KH-16 dashboard UC21): dùng chung `IOutputQueryService`.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3 UC19 / UC21: ghi định nghĩa thực tế và % đạt của màn KPI, và Admin chỉ xem
- **Trạng thái:** Mở

### DEV-MI-09-7 · Đọc file Excel thật: bỏ hình và công thức trước khi nạp

- **Ngày / Người / Task / PR:** 21/10/2026 · Minh (AI) · MI-09 (cũng áp cho MI-12, MI-01) · (chưa có PR, đang ở working tree)
- **Ảnh hưởng tới:** UC31 (nhập bảng lương), UC12 (nhập chi phí tháng), UC29 (nhập chấm công dự phòng), `ClosedXmlPayrollParser`, `ClosedXmlMonthlyCostParser`, `ClosedXmlAttendanceParser`
- **Tài liệu gốc ghi:** Không nói gì về chất lượng file Excel đầu vào.
- **Code đang làm:** Thử trên 40 file lương thật (`LUONG 2026`): phần lớn file `LƯƠNG THÁNG N.xlsx` làm ClosedXML 0.102.3 ném lỗi ngay khi mở (logo trỏ ảnh `Target="NULL"` → `ArgumentOutOfRangeException`) và khi đọc ô (công thức tham chiếu file khác → `NotImplementedException`). `XlsxWorkbookLoader` sửa gói trong bộ nhớ trước khi nạp: bỏ hình / ghi chú của sheet, bỏ công thức và giữ giá trị Excel đã lưu sẵn. Ô công thức chưa từng được Excel tính sẽ đọc ra trống. Ngoài ra sheet có cột "SLĐ" nhưng không có "Thực lĩnh" (vd bảng công "Công AB") bị bỏ qua, chỉ báo "sai mẫu" khi không còn sheet lương nào.
- **Lý do:** Hệ thống không tính lương (GBR-12), chỉ đọc số đã tính; giữ nguyên file gốc cho giám đốc ở `IPayrollFileStore`.
- **Ảnh hưởng người khác:** Khiêm / Thái: nếu thêm importer Excel mới nên dùng `XlsxWorkbookLoader.Open`.
- **Cần sửa trong tài liệu:** không.
- **Trạng thái:** Mở

### DEV-KI-03-2 · Endpoint danh sách công đoạn theo đơn cho tổ trưởng

- **Ngày / Người / Task / PR:** 22/10/2026 · Minh (AI) làm hộ Kiều · KI-03, KI-08 (FE) · (chưa có PR)
- **Ảnh hưởng tới:** UC52 (phân công công đoạn), UC56 (ghi hàng hỏng), `OperationAssignmentsController`, `IOperationAssignmentService`
- **Tài liệu gốc ghi:** Không nêu API nào cho tổ trưởng lấy danh sách công đoạn của đơn. API công đoạn hiện có (`accountant/operations`) chỉ mở cho Kế toán và kèm đơn giá hiện hành.
- **Code đang làm:** `ListOperationsAsync(orderId)`: công đoạn đang dùng, thuộc tổ may, của các mã hàng có trong đơn, sắp theo mã hàng + thứ tự; không trả đơn giá để tổ trưởng không thấy giá.
- **Lý do:** Màn phân công và màn ghi hàng hỏng cần ô chọn công đoạn theo đơn.
- **Ảnh hưởng người khác:** Kiều: cùng endpoint dùng cho các màn FE của KI-03 / KI-08.
- **Cần sửa trong tài liệu:** không.
- **Trạng thái:** Mở

### DEV-KI-01-2 · Nhập công đoạn từ file Excel quy trình công nghệ

- **Ngày / Người / Task / PR:** 22/10/2026 · Minh (AI) làm hộ Kiều · KI-01 · (chưa có PR)
- **Ảnh hưởng tới:** UC25 (công đoạn của mã hàng), `Operation`, `OperationsController`, màn "Công đoạn may"
- **Tài liệu gốc ghi:** Kế toán lập công đoạn của mã hàng; không nói cách nhập.
- **Code đang làm:** Theo yêu cầu của Minh (22/10/2026) màn công đoạn nhập từ file Excel quy trình công nghệ của phòng kỹ thuật (thử với "LUNA tổ HT áo dạ ngắn 1 lớp nẹp cúc.xlsx", 85 công đoạn, tổng TGCT 4.007 giây khớp tổng trong file). Đọc sheet đang hiển thị có cột STT, BƯỚC CÔNG VIỆC, BẬC, TGCT, THIẾT BỊ. Mã công đoạn = "OP" + STT, nên nhập lại file là cập nhật đúng công đoạn cũ, không xóa công đoạn nào. Bỏ qua tiêu đề nhóm, chỗ trống đã đánh số và dòng "Dự phòng" (file không tính vào tổng TGCT). `StandardTimeSec` = TGCT; `SkillLevel` = BẬC; cột THIẾT BỊ chỉ hiện ở bản xem trước vì `Operation` không có cột này. File không đánh dấu công đoạn cuối nên chọn ở bước nhập (không bắt buộc). Cột TIỀN được đọc thành đơn giá công đoạn, xem DEV-KI-01-4; đơn giá vẫn phải qua Admin duyệt (UC26).
- **Lý do:** Quy trình công nghệ có hàng chục công đoạn mỗi mã hàng, nhập tay từng dòng không thực tế.
- **Ảnh hưởng người khác:** Kiều: màn KI-01 FE dùng cách nhập này; nút thêm công đoạn bằng tay đã bỏ khỏi màn (API `POST accountant/operations` vẫn còn).
- **Cần sửa trong tài liệu:**
  - [ ] Report-3 UC25: mô tả nhập công đoạn từ Excel (cột đọc, quy tắc mã công đoạn)
- **Trạng thái:** Mở

### DEV-KI-01-3 · Gộp màn công đoạn và đơn giá công đoạn của Kế toán

- **Ngày / Người / Task / PR:** 22/10/2026 · Minh (AI) làm hộ Kiều · KI-01 · (chưa có PR)
- **Ảnh hưởng tới:** UC25, UC26, `OperationDto`, `OperationService.ListAsync`, FE `features/production`
- **Tài liệu gốc ghi:** Hai chức năng riêng: lập công đoạn (UC25) và lập đơn giá có duyệt (UC26).
- **Code đang làm:** Theo yêu cầu của Minh (22/10/2026) Kế toán dùng một màn "Công đoạn và đơn giá": nhập công đoạn từ Excel, đặt đơn giá ngay trên từng công đoạn (sửa bản Nháp / bị từ chối, hoặc lập bản mới theo ngày hiệu lực), gửi duyệt, mở rộng dòng để xem lịch sử đơn giá. `OperationDto` thêm `NextRate` = đơn giá mới nhất (theo ngày hiệu lực) nếu chưa là đơn giá hiện hành (Nháp, Chờ duyệt, Bị từ chối, hoặc đã duyệt nhưng chưa tới ngày hiệu lực). `ListAsync` lấy đơn giá của cả mã hàng bằng một truy vấn thay vì mỗi công đoạn một truy vấn. Quy trình duyệt không đổi: Admin vẫn duyệt / từ chối ở màn riêng "Duyệt đơn giá công đoạn"; API `accountant/piece-rates` giữ nguyên.
- **Lý do:** Công đoạn và đơn giá luôn đi cùng nhau; tách hai màn buộc Kế toán chọn lại mã hàng và công đoạn.
- **Ảnh hưởng người khác:** Kiều: màn FE KI-01 là màn gộp này; `OperationService` không còn phụ thuộc `IPieceRateService`.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3 UC25 / UC26: mô tả một màn chung cho công đoạn và đơn giá
- **Trạng thái:** Mở

### DEV-KI-01-4 · Nhập đơn giá công đoạn từ cột TIỀN và duyệt nhiều đơn giá

- **Ngày / Người / Task / PR:** 22/10/2026 · Minh (AI) làm hộ Kiều · KI-01 · (chưa có PR)
- **Ảnh hưởng tới:** UC25, UC26, `PieceRate`, `OperationService`, `PieceRateService`, `PieceRatesController` (Admin)
- **Tài liệu gốc ghi:** Kế toán lập đơn giá từng công đoạn (UC25) rồi Admin duyệt từng đơn giá (UC26).
- **Code đang làm:** Theo yêu cầu của Minh (22/10/2026), khi nhập công đoạn từ Excel thì đọc luôn cột TIỀN (đồng/sản phẩm, làm tròn 2 số) thành đơn giá của công đoạn đó, hiệu lực từ ngày Kế toán chọn khi nhập (mặc định hôm nay). Mỗi công đoạn: chưa có đơn giá ngày đó → thêm bản mới; đã có bản Nháp / bị từ chối cùng ngày → cập nhật; đã chờ duyệt / đã duyệt cùng ngày với giá khác → bỏ qua; giá bằng đơn giá hiện hành hoặc bản cùng ngày → không đổi; công đoạn đã ngừng dùng → bỏ qua. Đơn giá tạo ra ở trạng thái Nháp, hoặc Chờ duyệt (Admin nhận một thông báo chung) nếu chọn "Gửi Admin duyệt luôn". File LUNA: 85 đơn giá, tổng 17.887,48 đồng (file ghi 18.000 vì cộng cả dòng "Dự phòng" bị bỏ qua). Admin có thêm `POST admin/piece-rates/approve-many` để duyệt nhiều đơn giá chờ duyệt trong một giao dịch, ghi nhật ký từng đơn giá, gửi Kế toán một thông báo; dòng không ở trạng thái chờ duyệt bị bỏ qua. Từ chối vẫn từng đơn giá, bắt buộc lý do.
- **Lý do:** Mỗi mã hàng có hàng chục công đoạn, nhập và duyệt từng đơn giá không thực tế.
- **Ảnh hưởng người khác:** Kiều: màn FE KI-01 dùng cách nhập và duyệt này.
- **Cần sửa trong tài liệu:**
  - [ ] Report-3 UC25 / UC26: nhập đơn giá hàng loạt từ Excel và Admin duyệt nhiều đơn giá cùng lúc
- **Trạng thái:** Mở

### DEV-MI-20-1 · Lỗi phát hiện khi chạy thử toàn luồng (đã sửa)

- **Ngày / Người / Task / PR:** 22/10/2026 · Minh (AI) · MI-05, MI-12, MI-13, MI-09 · (chưa có PR)
- **Ảnh hưởng tới:** màn Chấm công (FE), `ClosedXmlMonthlyCostWriter`, `FinanceService.CalculateAsync`, `XlsxWorkbookLoader`
- **Cách phát hiện:** dựng DB riêng từ `CreateAndSeed.sql` + migration dữ liệu chung, chạy backend thứ hai và gọi ~110 endpoint GET cùng các luồng ghi qua HTTP thật (nhập công đoạn + đơn giá bằng file, duyệt nhiều, sản lượng, QC, hàng hỏng, mua hàng, KPI, suất ăn, chi phí, lương, hòa vốn).
- **Lỗi và cách sửa:**
  - **MI-05** Thiếu FE "Chấm công hộ": API `accountant/attendance/adjustments` có nhưng không có màn. Thêm tab "Chấm công hộ" (danh sách, form, danh sách đơn xin chấm bù đã duyệt kèm nút chấm hộ điền sẵn).
  - **MI-02** `MapCodeForm` bắt gõ tay `EmployeeId` (TODO chờ API nhân sự của Khiêm, API đã có). Đổi thành chọn tổ rồi nhân viên (`EmployeePicker`).
  - **MI-12** File mẫu chi phí tháng có dòng ví dụ `MEALS` số tiền 0 hợp lệ, quên xóa là nhập thành khoản chi 0 đồng. Ví dụ chuyển sang sheet `HuongDan`; sheet nhập chỉ còn tiêu đề.
  - **MI-13** Tính hòa vốn lần hai trong cùng một giây với lần trước (hoặc bấm đúp) bị 409 vì khóa UNIQUE `(PeriodId, CalculatedAt, ProductId)` chỉ chính xác đến giây. `CalculatedAt` luôn lấy mốc lớn hơn lần tính gần nhất.
  - **MI-09 / MI-12 / MI-08** File không phải .xlsx hiện thông báo kỹ thuật tiếng Anh ("End of Central Directory record could not be found"). `XlsxWorkbookLoader` đổi thành thông báo tiếng Việt.
- **Cần sửa trong tài liệu:** không.
- **Trạng thái:** Đã sửa

### DEV-KI-09-1 · Đơn cắt bù từ phiếu hàng hỏng Scrap

- **Ngày / Người / Task / PR:** 22/10/2026 · Minh (AI) làm hộ Kiều · KI-09 · (chưa có PR)
- **Ảnh hưởng tới:** UC56, UC07 (loại đơn cắt bù), `IRequestTypeHandler`, `DefectDto`, `DefectRecordsPage`
- **Tài liệu gốc ghi:** Sheet KI-09 "Loại đơn cắt bù". Khung đơn từ (Khiêm) đã có chính sách `REPLACEMENT_CUTTING` (tổ trưởng / tổ phó tổ may tạo, Quản đốc duyệt) nhưng chưa có bộ kiểm riêng và chưa có form.
- **Code đang làm:** `ReplacementCuttingRequestHandler` kiểm: bắt buộc `DefectRecordId`; phiếu phải là Scrap chưa hủy; tổ trưởng / tổ phó chỉ tạo cho phiếu của tổ mình; `OrderId` của đơn khớp đơn hàng của phiếu; mỗi dòng có tên chi tiết (không trùng nhau), đúng dòng đơn của phiếu, số lượng nguyên từ 1 đến số hàng đã hủy; mỗi phiếu tối đa một đơn (khớp `UX_Request_Defect`, kể cả đơn đã bị từ chối / hủy). FE: nút "Đơn cắt bù" trên phiếu Scrap trong màn Hàng hỏng; phiếu hiện mã và trạng thái đơn. `AllowManualComplete = true` vì chưa có lệnh cắt bù của tổ cắt tự hoàn thành đơn (WorkOrder có `SourceRequestId` là việc của Thái); sau khi có thì đổi thành `false`.
- **Lý do:** Khung đơn từ để mỗi luồng tự thêm bộ kiểm cho loại đơn của mình.
- **Ảnh hưởng người khác:** Thái: lệnh cắt bù nên gắn `SourceRequestId` và hoàn thành đơn qua `IRequestService`. Khiêm: `CreateRequestBody` (FE) thêm `orderId`, `defectRecordId`.
- **Cần sửa trong tài liệu:** không.
- **Cập nhật 23/10/2026:** lệnh cắt bù đã có (TH-10, xem DEV-ALL-3); đơn cắt bù tự hoàn thành khi BTP bù vào kho đủ số, nên đơn này vẫn cho hoàn thành tay chỉ để xử lý ngoại lệ.
- **Trạng thái:** Mở

### DEV-KI-14-1 · Màn xưởng trên điện thoại và nhập ngoài phân công

- **Ngày / Người / Task / PR:** 22/10/2026 · Minh (AI) làm hộ Kiều · KI-14, KI-05 · (chưa có PR)
- **Ảnh hưởng tới:** UC55, UC56, UC57, `OutputEntryPage`, các hộp thoại của feature `production`
- **Code đang làm:** Trên màn nhỏ (dưới breakpoint `md`): lưới sản lượng đổi thành thẻ (mỗi công nhân một thẻ, ô nhập số lớn); ẩn cột phụ (đơn giá, hạn sửa, đơn hàng, người kiểm...) ở các bảng sản lượng, hàng hỏng, QC, phân công; hộp thoại có bề rộng `min(Npx, 100vw - 16px)`. Lưới sản lượng thêm "Thêm dòng ngoài phân công" cho tổ trưởng (BE vốn đã hỗ trợ `IsOutsideAssignment`, GBR-20).
- **Chưa làm:** chưa thử trên thiết bị thật / trình duyệt; chưa có bài kiểm tự động cho bố cục.
- **Cần sửa trong tài liệu:** không.
- **Trạng thái:** Mở

### DEV-MI-20-2 · Hai lỗi nền tảng phát hiện khi chạy thử

- **Ngày / Người / Task / PR:** 22/10/2026 · Minh (AI) · MI-05 · (chưa có PR)
- **Ảnh hưởng tới:** `SQL/NX-MTD_Database.sql` (trước đó là `CreateAndSeed.sql` của Khiêm, 21/10), `API/Filters/ValidationFilter.cs` (Khiêm)
- **Lỗi và cách sửa:**
  - Script `CreateAndSeed.sql` 21/10 thiếu `usp_Attendance_ApplyAdjustment` (bản `NX-MTD_Database.sql` cũ có): chấm công hộ báo 500 trên DB dựng từ script này. Thủ tục (nguyên văn) nay nằm trong `NX-MTD_Database.sql` duy nhất (DEV-MI-20-3). Đã đối chiếu mọi đối tượng giữa hai script: chỉ thiếu đúng thủ tục này.
  - `ValidationFilter` đọc `PropertyName[0]` nên ném `IndexOutOfRangeException` (500) khi rule mức đối tượng `RuleFor(x => x).Must(...)` báo lỗi (vd. chấm hộ không có giờ vào / ra). Nay trả 400 kèm thông báo của rule.
- **Cần sửa trong tài liệu:** không.
- **Trạng thái:** Đã sửa

### DEV-MI-20-3 · Một file database duy nhất

- **Ngày / Người / Task / PR:** 22/10/2026 · Minh (AI) · MI-20 · (chưa có PR)
- **Ảnh hưởng tới:** toàn bộ thư mục `SQL/`; Coding Rules 3.12 và AI Context vẫn nhắc `CreateAndSeed.sql` và `SQL/Migrations/`
- **Thay đổi:** nhóm đã bỏ `CreateAndSeed.sql` và chỉ dùng `NX-MTD_Database.sql`, nhưng bản 16/10 đã cũ nên người mới phải chạy 4 file đúng thứ tự. Nay `NX-MTD_Database.sql` (bản 22/10) là file duy nhất, chạy một lần: `sqlcmd -S localhost -E -C -I -f 65001 -b -i SQL/NX-MTD_Database.sql`. Gồm schema, TH-12 (đơn mua dùng chung), tài khoản mẫu (mục 17b), thủ tục `usp_Attendance_ApplyAdjustment` và mục 19 dữ liệu demo (188 công đoạn + 110 đơn giá, đơn hàng, sản lượng, QC, hàng hỏng, KPI, suất ăn, bảng lương demo, và tháng 08/2026 có sẵn doanh thu + chi phí để xem hòa vốn ngay; không có họ tên thật, lương thật hay hash mật khẩu). Muốn DB sạch thì xóa mục 19. Mục 19 chạy lại không nhân đôi dữ liệu.
- **File đã xóa:** `CreateAndSeed.sql`, `SeedAuth.sql`, `Migrations/20261021_01_TH-12_stock-purchase-order.sql`, `Migrations/20261022_01_MI_shared-data.sql`, `DemoData_KH-06-10_purchase-orders.sql` (cần mã hàng `TEST-MH01` không script nào tạo nên không chạy được trên DB sạch; đơn mua mẫu đã có trong mục 19).
- **Kiểm chứng:** dựng DB mới chỉ từ file này: 65 bảng, 4 thủ tục `usp_Attendance_*` (QUOTED_IDENTIFIER = 1), 188 công đoạn, 110 đơn giá, 14 nhân viên / 14 tài khoản (đủ 9 role); backend chạy được, đăng nhập các role và chấm công hộ trả 200.
- **Cần sửa trong tài liệu:**
  - [ ] Coding Rules 3.12 và AI Context: thay `CreateAndSeed.sql` + `SQL/Migrations/` bằng một file `NX-MTD_Database.sql`
- **Trạng thái:** Đã sửa

### DEV-ALL-1 · TH-12, TH-13

- **Ngày / Người / Task / PR:** 23/10/2026 · Claude (AI) · TH-12, TH-13 · (chưa có PR)
- **Ảnh hưởng tới:** `MaterialReceiptService`, `ShortDeliveryService`, `MaterialSupplyUpdater`
- **Code đang làm:**
  - Phiếu nguồn SUPPLIER lập theo đơn mua đã đặt (ORDERED / PARTIALLY_RECEIVED), số nhập ≤ số còn phải nhận; dòng phiếu nhập hai nhóm (NVL của đơn / dòng đơn mua).
  - Thủ kho xem đơn thiếu NVL chỉ khi Quản đốc đã chọn "giao bù" (controller riêng). Đơn SHORT_DELIVERY không cho hoàn thành tay (`AllowManualComplete = false`): chỉ đóng khi nhận đủ hàng bù hoặc Quản đốc chốt không bù.
  - `NotReplenishedQty` chỉ tăng tối đa bằng phần còn lại trên dòng đơn mua (đặt − đã nhận − đã chốt) để không vi phạm `CK_POL_Qty`. Phiếu khách cấp không có đơn mua nên "không bù" chỉ ghi nhận, không đổi số liệu.
  - MaterialStatus = COMPLETE khi mọi NVL của đơn có (đã nhận + đã chốt không bù) ≥ cần; AWAITING khi chưa nhận gì; còn lại PARTIAL.
- **Cần sửa trong tài liệu:** AI Context mục 4.1 bước 5: ghi rõ phiếu bổ sung phải ≤ số Quản đốc chọn giao bù và số đã nhận bù được cộng dồn theo từng dòng.
- **Trạng thái:** Mở

### DEV-ALL-2 · TH-11, TH-14

- **Ngày / Người / Task / PR:** 23/10/2026 · Claude (AI) · TH-11, TH-14 · (chưa có PR)
- **Ảnh hưởng tới:** `FgHandoverService`, `DispatchNoteService`
- **Code đang làm:**
  - QA/QC chỉ thấy và rút phiếu của mình; số còn giao được = Σ QC đạt − số đã giao (phiếu chờ + đã nhận). QA/QC không được xem tỉ lệ lỗi nên màn này không hiện lỗi.
  - Phiếu xuất giữ chỗ thành phẩm bằng `EnsureAvailableAsync` ngay khi lập (vw_StockBalance tính phiếu PENDING là đã giữ). Đơn → IN_DELIVERY khi Thủ kho xuất, → DELIVERED / PARTIALLY_DELIVERED theo tổng khách đã nhận so với số đặt (GBR-23). Không có trạng thái giao thất bại (GBR-24).
  - Ảnh phiếu xuất (Thủ kho) và biên bản giao (Kế toán) đều gắn `DISPATCH_NOTE` nhưng khác mục đích (SIGNED_ISSUE_SLIP / SIGNED_DELIVERY_NOTE).
- **Cần sửa trong tài liệu:** không.
- **Trạng thái:** Mở

### DEV-ALL-3 · TH-10, TH-19

- **Ngày / Người / Task / PR:** 23/10/2026 · Claude (AI) · TH-10, TH-19 · (chưa có PR)
- **Ảnh hưởng tới:** `WorkOrderService.CreateReplacementAsync`, `CutHandoverService`, `MaterialReturnService`, `WorkerPieceMovementService`
- **Code đang làm:**
  - Lệnh cắt bù không tính vào trần kế hoạch của dòng đơn (đã có sẵn trong `CheckPlanCapAsync`); vải cần tính theo chi tiết (`PanelName`) của đơn cắt bù. Đơn cắt bù tự hoàn thành khi BTP của lệnh đã vào kho đủ số kế hoạch.
  - Trả vải thừa: chỉ tổ cắt, theo phiếu cấp đã nhận, số trả ≤ đã nhận − đã trả (phiếu chờ tính số tổ trả, phiếu đã nhận tính số kho đếm).
  - Giao / thu hồi BTP: giao ≤ BTP tổ đang giữ trừ số đang chờ công nhân nhận; thu hồi ≤ công nhân đang giữ trừ số đang chờ trả; kiểm lại lúc xác nhận. Xác nhận hộ (ON_BEHALF) bắt buộc lý do. Số xác nhận làm đổi `vw_WorkerOutputCap` nên trần sản lượng (MSG16) bám BTP thật.
- **Cần sửa trong tài liệu:** không.
- **Trạng thái:** Mở

### DEV-ALL-4 · TA-02, TA-03

- **Ngày / Người / Task / PR:** 23/10/2026 · Claude (AI) · TA-02, TA-03 · (chưa có PR)
- **Ảnh hưởng tới:** `StocktakeService`, `VehicleService`
- **Code đang làm:**
  - Kiểm kê: số sổ chụp lúc lập phiếu (và chụp lại khi sửa phiếu bị trả); duyệt ghi điều chỉnh qua `IStockService` (chênh lệch = số đếm − số sổ), nên tồn thực lúc duyệt không âm. Số đếm để trống = khớp sổ.
  - Sổ xe: biển số chuẩn hóa chữ hoa, không xóa cứng xe. Cảnh báo lệch km so với km cuối chuyến HOÀN THÀNH gần nhất; xăng vượt định mức × (1 + `FUEL_NORM_TOLERANCE_PERCENT`). Cảnh báo chỉ báo, vẫn lưu (MSG30). Mỗi xe tối đa một chuyến chưa kết thúc.
- **Cần sửa trong tài liệu:** không.
- **Trạng thái:** Mở

### DEV-ALL-5 · KH-12, KH-14, KH-15, KH-16, KH-22, KH-23

- **Ngày / Người / Task / PR:** 23/10/2026 · Claude (AI) · KH-12, KH-14, KH-15, KH-16, KH-22, KH-23 · (chưa có PR)
- **Ảnh hưởng tới:** Thông báo, bậc thợ, trạng thái đơn, dashboard, AuditLog, import nhân viên, JOB-04 / JOB-05
- **Code đang làm:**
  - Thông báo: chưa push FCM (PushStatus luôn SKIPPED); chuông tự nạp lại mỗi phút thay cho push thời gian thực. Người gửi không nhận thông báo của chính mình.
  - Bậc thợ: số lỗi gần đây tính trong 30 ngày (Rework + Scrap chưa hủy). Mỗi công nhân chỉ một đề xuất chờ quyết định; đề xuất đã quyết định không đổi được (GBR-22).
  - JOB-04: chưa hoàn thiện cái nào trong N ngày thì chỉ gắn cờ khi đã quá hạn giao (tránh gắn cờ mọi đơn mới). Tính theo ngày lịch, không bỏ Chủ nhật. JOB-05 01:00: khóa giờ chạy `NOTIFICATION_CLEANUP_TIME` chưa seed nên dùng mặc định trong code.
  - Import nhân viên: cột cố định (Mã NV, Họ tên, Giới tính, Ngày sinh, SĐT, Vai trò, Mã tổ, Hình thức lương, Ngày vào làm, Mã chấm công), tối đa 500 dòng; mật khẩu tạm chỉ trả một lần ngay khi nhập.
- **Cần sửa trong tài liệu:** Report-3 UC17 (định dạng file import nhân viên), UC21 (công thức nguy cơ trễ), UC05 (không push FCM).
- **Trạng thái:** Mở

### DEV-ALL-6 · TH-16 In / xuất Excel chứng từ kho

- **Ngày / Người / Task / PR:** 23/10/2026 · Claude (AI) · TH-16 · (chưa có PR)
- **Ảnh hưởng tới:** UC13 (phiếu nhập), UC40 (phiếu cấp), UC43 (phiếu xuất hàng); `GET .../{id}/excel` ở các controller Quản đốc, Thủ kho, Tổ trưởng, Kế toán
- **Code đang làm:**
  - Xuất theo từng phiếu (một trang A4 dọc: tiêu đề, thông tin chung, bảng dòng hàng, hàng chữ ký), không xuất cả danh sách. Danh sách vẫn xem trên màn hình.
  - Dữ liệu lấy qua dịch vụ chi tiết hiện có nên giữ nguyên quyền xem: tổ trưởng chỉ in được phiếu cấp của tổ mình (khác tổ → 403 / 404 như xem chi tiết).
  - Quản đốc in được phiếu nhập; Thủ kho in được cả ba loại; Kế toán in được phiếu xuất hàng.
- **Cần sửa trong tài liệu:** Report-3 UC13: ghi rõ "in" là tải file Excel để tự in, chưa có bản PDF.
- **Trạng thái:** Mở

### DEV-ALL-7 · Quản đốc sửa tổ / vai trò nhân viên và đặt bậc thợ trực tiếp

- **Ngày / Người / Task / PR:** 23/10/2026 · Claude (AI) theo yêu cầu của Minh · UC15, UC16 · (chưa có PR)
- **Ảnh hưởng tới:** `PUT manager/employees/{id}` (`ManagerUpdateEmployeeDto` thêm `Role`, `TeamId`); `POST admin|manager/grade-evaluations/set`, `GET .../workers`; form nhân viên và màn Bậc thợ ở FE
- **Tài liệu gốc ghi:** UC16: Quản đốc chỉ sửa hồ sơ cá nhân và mã chấm công, không đổi vai trò, tổ, kiểu lương. UC15 / UC50: bậc thợ chỉ đổi qua đề xuất của tổ trưởng rồi Admin / Quản đốc duyệt.
- **Code đang làm:**
  - Quản đốc sửa thêm tổ và vai trò: không gán được Admin / Quản đốc / Kế toán (403), vẫn không xem hay sửa được nhân viên có các vai trò đó (GBR-01), không đổi kiểu lương. Đổi tổ ghi nhật ký "Chuyển tổ"; đổi tổ hoặc vai trò làm token cũ hết hiệu lực. Các kiểm tra tổ trưởng / tổ phó (đúng loại tổ, không trùng người) dùng chung với Admin.
  - Đặt bậc trực tiếp (Admin và Quản đốc): tạo ngay một dòng lịch sử `APPLIED` (người đặt = người đăng nhập, lý do bắt buộc, hiệu lực mặc định hôm nay), cập nhật `CurrentGradeCode`, ghi AuditLog, báo công nhân. Đề xuất đang chờ của công nhân đó bị đóng thành `NOT_APPLIED` với ghi chú, báo người đề xuất. Mỗi người đặt tối đa một lần mỗi công nhân mỗi ngày (ràng buộc `UQ_GradeEval`); sai thì phải đợi sang ngày hoặc nhờ Admin.
  - Quản đốc vẫn không sửa mức tiền bậc A–D (chỉ Admin).
- **Cần sửa trong tài liệu:** Report-3 UC16 (quyền sửa của Quản đốc), UC15 / UC50 (đặt bậc trực tiếp).
- **Trạng thái:** Mở

### DEV-ALL-8 · Chỉ tiêu KPI theo từng ngày

- **Ngày / Người / Task / PR:** 24/10/2026 · Claude (AI) theo yêu cầu của Minh · UC19, FR 3.14 · (chưa có PR)
- **Ảnh hưởng tới:** schema (bảng mới `KpiDailyTarget`, viết lại `vw_KpiDailyResult`), `SaveKpiTargetDto` (vẫn nhập tổng cả kỳ; `WorkingDays` do BE tự tính), form lập KPI ở FE
- **Tài liệu gốc ghi:** Chỉ tiêu ngày = chỉ tiêu cả kỳ ÷ số ngày làm việc (chia đều).
- **Lý do:** Mấy ngày đầu vào đơn tổ may làm chậm hơn (công nhân chưa thạo), chia đều làm KPI ngày đầu luôn "không đạt".
- **Code đang làm:**
  - Lập / sửa KPI chỉ nhập đơn, mã hàng, tổ, kỳ và **tổng cả kỳ**. Chỉ tiêu từng ngày đặt sau, trên lịch KPI (DEV-ALL-9). `WorkingDays` = số ngày đã đặt KPI, chưa đặt thì = số ngày trong kỳ trừ Chủ nhật.
  - Đổi kỳ thì bỏ các ngày nằm ngoài kỳ mới; tổng cả kỳ mới không được nhỏ hơn tổng KPI các ngày còn lại (409).
  - `vw_KpiDailyResult`: có dòng cho mọi ngày có chỉ tiêu (chưa có hàng = 0) và ngày QC ngoài kế hoạch (chỉ tiêu, % đạt = NULL). KPI chưa đặt ngày giữ cách chia đều cũ. Cột "chỉ tiêu ngày" ở danh sách / chi tiết là trung bình.
  - DB đang chạy cần chạy thêm: CREATE TABLE `KpiDailyTarget` + CREATE OR ALTER VIEW `vw_KpiDailyResult` (lấy đúng khối trong `SQL/NX-MTD_Database.sql`).
- **Cần sửa trong tài liệu:** Report-3 UC19 (form lập KPI), FR 3.14 (công thức chỉ tiêu ngày), ERD (bảng mới).
- **Trạng thái:** Mở

### DEV-ALL-9 · Lịch KPI theo ngày (Tổng BTP và số còn lại để đặt KPI)

- **Ngày / Người / Task / PR:** 24/10/2026 · Claude (AI) theo yêu cầu của Minh · UC19, UC21, FR 3.14 · (chưa có PR)
- **Ảnh hưởng tới:** `GET manager|admin/kpi-targets/{id}/calendar`, `PUT manager/kpi-targets/{id}/daily-plan`; nút "Xem theo ngày" ở màn KPI mở lịch; không đổi schema
- **Tài liệu gốc ghi:** không có (yêu cầu bổ sung). Màn "So sánh BTP – KPI" làm trước đó (`/kpi/btp-compare`, Thủ kho và tổ trưởng xem được) đã bỏ, gộp vào lịch này.
- **Code đang làm:**
  - Lịch tháng kiểu tờ lịch (Thứ hai → Chủ nhật, Thứ bảy xanh, Chủ nhật cam). Quản đốc gõ KPI vào từng ô ngày, hoặc "Chia tăng dần" (3 ngày đầu 50% → 70% → 90% mức ổn định), "Chia đều", "Xóa các ngày chưa tới". Ngày đã qua không sửa được. Admin và KPI đã đóng chỉ xem.
  - **Tổng bán thành phẩm** = BTP đã cắt vào kho (giao dịch kho BTP `CUT_HANDOVER`) − hàng QA/QC đã nhập ra chuyền, tính theo (đơn, mã hàng) cho mọi tổ. **Số lượng còn lại để đặt KPI** = Tổng BTP − KPI đã đặt chưa ra chuyền (các ngày sau hôm nay + phần còn thiếu của hôm nay, của mọi KPI đang chạy cùng đơn và mã hàng). Giảm khi QA/QC nhập hàng ra chuyền.
  - Lưu bị chặn (409) khi Σ KPI các ngày > tổng cả kỳ, hoặc KPI chưa ra chuyền vượt số BTP còn lại (báo "tối đa N"). Đang giảm thì vẫn lưu được dù BTP đã thiếu từ trước.
  - Mỗi ô: KPI, thực tế của tổ (QA/QC), BTP còn sau ngày (ngày tương lai là dự báo theo KPI đã đặt), cờ "Sắp thiếu BTP" và "Chậm KPI".
  - Hạn chế: BTP là kho chung theo (đơn, mã hàng), không tách theo tổ may; Thủ kho và tổ trưởng không còn màn xem riêng.
  - Dữ liệu demo mục 19.6 (BTP): DEMO-DH001 cắt đủ 5 dòng trước ngày tổ may nhận (PC-DEMO-001) nên kho BTP về 0; DEMO-DH002 cắt 800 chưa cấp tổ may; DEMO-DH000 (đã hoàn thành) cắt rồi cấp hết cho tổ may. Thẻ kho chỉ thêm mới (trigger append-only), nên DB demo đang chạy chỉ được bổ sung, không sửa các đợt cắt cũ.
- **Cần sửa trong tài liệu:** thêm lịch KPI vào Report-3 (UC19 / UC21) và ma trận quyền.
- **Trạng thái:** Mở

### DEV-ALL-10 · Đơn chờ khai báo NVL (Quản đốc) và màn NVL chờ nhận (Thủ kho)

- **Ngày / Người / Task / PR:** 24/10/2026 · Claude (AI) theo yêu cầu của Minh · UC09 (phần NVL), UC37, UC38 · (chưa có PR)
- **Ảnh hưởng tới:** `GET admin|manager/order-materials/pending-orders`, `GET warehouse-keeper/incoming-materials`; màn "NVL của đơn" và màn mới "NVL chờ nhận"; thông báo; không đổi schema
- **Tài liệu gốc ghi:** Admin tạo đơn → Quản đốc nhận thông báo "Có đơn hàng mới" rồi tự vào NVL của đơn chọn đơn. Thủ kho lập phiếu nhập bằng cách tự chọn đơn / đơn mua; không có danh sách việc chờ.
- **Lý do:** Quản đốc không thấy đơn mới ở đâu để khai báo NVL; Thủ kho không biết khách sẽ cấp hoặc đơn mua đã đặt.
- **Code đang làm:**
  - Màn "NVL của đơn" có bảng **Đơn hàng chờ khai báo / mua NVL** (đơn NEW / IN_PRODUCTION có NVL chưa về đủ, đơn chưa khai báo NVL lên đầu). Bấm Mở đơn thì chọn đơn; đơn đang xem nằm trên đường dẫn `?orderId=`.
  - Thông báo "Có đơn hàng mới" (gửi Quản đốc khi Admin tạo đơn) giờ có liên kết tới `/warehouse/order-materials?orderId=…`.
  - Màn **NVL chờ nhận** (chỉ Thủ kho): (1) NVL khách cấp = số khách cấp đã khai báo − số đã nhận (phiếu nhập đã xác nhận), kèm ngày hẹn, quá hẹn tô đỏ; (2) dòng đơn mua đã đặt hàng (ORDERED / PARTIALLY_RECEIVED) = đặt − đã nhận − đã chốt không bù. Nút Lập phiếu nhập mở form phiếu nhập điền sẵn nguồn, đơn / đơn mua, dòng NVL và số còn phải nhận.
  - Thông báo cho Thủ kho: khi Quản đốc khai báo NVL khách cấp (hoặc đổi số khách cấp / ngày hẹn); khi Quản đốc đặt hàng đơn mua với nhà cung cấp.
  - Lưu ý: đơn mua chỉ vào danh sách chờ nhận sau khi Quản đốc bấm Đặt hàng (trạng thái ORDERED); đơn mua mới tạo (chờ chốt) chưa hiện. Phiếu nhập đang chờ xác nhận chưa trừ vào số còn phải nhận.
- **Cần sửa trong tài liệu:** Report-3 UC09 / UC37 (thêm màn NVL chờ nhận và danh sách đơn chờ khai báo), ma trận quyền (Thủ kho: NVL chờ nhận).
- **Trạng thái:** Mở

### DEV-ALL-11 · Chuyển code sang database mới của Morgan (24/10/2026)

- **Ngày / Người / Task / PR:** 25/10/2026 · Claude (AI) theo yêu cầu của Minh · toàn hệ thống · (chưa có PR)
- **Ảnh hưởng tới:** `SQL/NX-MTD_Database.sql` (thay nguyên file), `be/Domain/Entities/*` (scaffold lại), hầu hết dịch vụ / DTO / validator, giao diện kho – cắt – đơn hàng – mã hàng, test.
- **Tài liệu gốc ghi:** script cũ: định mức nhập theo từng đơn (`OrderMaterialNorm`), dòng đơn lưu màu / size, phiếu nhập có số thiếu / lỗi, NVL khách cấp có ngày hẹn, `KpiDailyTarget` cho lịch KPI theo ngày.
- **Code đang làm (khác tài liệu cũ):**
  - **Mã hàng có biến thể, thành phần và định mức** (`ProductVariant`, `ProductComponent`, `ProductComponentMaterial`). Admin khai ở màn Mã hàng → nút "Định mức" (`GET|PUT admin/products/{id}/spec`). Dòng đơn trỏ `ProductVariantId`; Admin lưu đơn với màu × size chưa khai báo thì hệ thống **tự tạo biến thể** (ACTIVE), định mức khai bổ sung sau. Vải cần của lệnh cắt tính từ `ProductComponentMaterial` (chi tiết PANEL, NVL loại vải); NVL của đơn so "cần theo định mức" với số Quản đốc khai.
  - **Số lần giao dự kiến** thay ngày hẹn: `OrderMaterial.ExpectedDeliveryCount` (bắt buộc ≥ 1 khi khách cấp), `PurchaseOrder.ExpectedDeliveryCount` (nhập khi đặt hàng). Màn NVL chờ nhận hiện "đã nhận / dự kiến".
  - **Thiếu hàng tự tính, Thủ kho không nhập số thiếu / số lỗi** (cột đã bỏ). Khi phiếu nhập được xác nhận mà số phiếu đã xác nhận ≥ số lần giao dự kiến (mặc định 1) và dòng còn thiếu (đặt hoặc khách cấp − đã nhận − đã chốt không bù > 0) thì hệ thống sinh đơn SHORT_DELIVERY cho Quản đốc, số thiếu = phần còn thiếu. Dòng đã có đơn thiếu đang chờ không sinh thêm. Phiếu bổ sung = phiếu có `ShortageRequestId`.
  - **Cắt bù theo chi tiết**: `WorkOrderLine/RequestLine/CuttingLayLine/CutHandoverLine/MaterialIssueLine/StocktakeLine/StockTransaction.ProductComponentId`. Tổ may chọn chi tiết PANEL của mã hàng (không gõ tên); kho BTP tách tồn "cả bộ" và "chi tiết lẻ"; tồn tổ may đang giữ chỉ tính bộ đủ. Vải cắt bù = số lượng × định mức ÷ số chi tiết trên một sản phẩm.
  - **Đăng nhập bằng `Employee.EmployeeCode`** (`Account.Username` đã bỏ). **Thông báo**: `RefTable/RefId` + `ReadAt` (`IsRead`, `Source`, `MaterialType`, `IsDelayRisk` là cột tính, code không ghi). **Đơn giá** luôn qua `PieceRate` (`OutputRecord`, `DefectRecord.PieceRateId`). **Bảng lương**: file Excel ở `Attachment` (PAYROLL / IMPORT_FILE), tổng = Σ NetPay, lý do mở lại đọc từ `AuditLog` (UNLOCK). **Suất ăn** tổng qua `vw_DailyMealTotal`. **Xe**: `DriverName` chỉ có khi tài xế không phải nhân viên. **Tổ cắt** của phiếu bàn giao / trả vải lấy từ lệnh cắt / phiếu cấp.
  - **Gỡ KPI theo ngày** (script mới không có bảng `KpiDailyTarget`) và **tạm bỏ dữ liệu demo 19.3–19.6** (BTP, KPI, xe, suất ăn do Claude thêm); script mới chỉ có 19.1–19.2. Code lịch KPI (`KpiCalendarService`, `KpiCalendarDrawer`, `KpiMonthGrid`, endpoint `calendar` / `daily-plan`) còn trong git: commit `2e047a2` và các commit trước đó. Khôi phục = thêm lại bảng `KpiDailyTarget` + `vw_KpiDailyResult` vào script (xin Morgan) rồi lấy lại các file đó. Nút "Xem theo ngày" quay về bảng kết quả từng ngày của `vw_KpiDailyResult`.
  - Dữ liệu demo mới nhất phải dựng lại từ script; không có lệnh nâng cấp database cũ.
- **Cần sửa trong tài liệu:** Report-3 (UC09, UC10 thêm định mức / biến thể, UC37 bỏ số thiếu / lỗi và thêm số lần giao dự kiến, UC19, UC24 chi tiết lẻ, lương, suất ăn); ERD.
- **Trạng thái:** Đã làm

### DEV-ALL-16 · Chuyển code sang database 29/10 và các mục "Doing" của sheet (29/10/2026)

- **Ngày / Người / Task / PR:** 29/10/2026 · Claude (AI) theo yêu cầu của Minh · nhiều luồng · cùng nhánh `feature/ALL-missing-features`
- **Đổi schema:** script chính `SQL/NX-MTD_Database.sql` khớp bản 29/10 của nhóm (đã áp patch `SQL/patches/2026-10-29_db_29_10.sql` lên `NXMTD_demo5`; chạy lại không lỗi). Bản này **không có** 3 patch 28/10 của nhánh này nên đã xóa `2026-10-28_grade_level_code.sql`, `end_of_order.sql`, `dispatch_approval.sql`.
  - `ProductComponent` bỏ `SortOrder`; `ProductComponentMaterial` bỏ `QtyPerGarment`, `WastagePercent`; `ProductVariant`, `Material` thêm `LengthCm`, `WidthCm`; bỏ `vw_OrderMaterialNorm`, `vw_WorkOrderFabricNeed`; thêm `vw_RecutFabricNeed`.
  - Hoàn lại về bản gốc: bậc thợ `CHAR(1)` A–D (bỏ `GradeLevelCode`), `CK_DN_Status` và giữ chỗ `vw_StockBalance` chỉ `PENDING`, `CK_DR_Reason` bỏ `END_OF_ORDER_MISMATCH`, `CK_Request_Type` bỏ `OVERTIME`, bỏ khóa `KPI_OT_AUTO_THRESHOLD_PCT`.
- **Chức năng bị gỡ / thu nhỏ so với DEV-ALL-15 (code còn trong git, khôi phục được khi DB có lại các cột / bảng này):**
  - Bậc thợ động (bậc E, F… do Admin thêm): về 4 bậc cố định; vẫn sửa tiền bậc theo ngày hiệu lực.
  - Đề xuất tăng ca khi KPI không đạt: "Kết thúc đơn" chỉ gửi thông báo cho Quản đốc và tổ trưởng của tổ kèm % thiếu và gợi ý (thiếu không quá ngưỡng thì nên tăng ca, thiếu nhiều thì tổ trưởng quyết định); phiếu hàng hỏng lệch ghi lý do `OTHER`, ghi chú "Lệch kiểm cuối đơn".
  - Xuất hàng 2 bước duyệt: về `PENDING → IN_DELIVERY`; vẫn bắt buộc ảnh phiếu xuất có Quản đốc ký (Thủ kho chụp ở trạng thái chờ xuất) và ảnh biên bản giao có khách ký.
  - Lượng vải theo định mức: lệnh cắt thường **không** kiểm đủ vải khi lập, không còn "xem trước vải", tab "Vải theo đơn", so sánh thực dùng với định mức, cột "Cần theo định mức" ở NVL của đơn. Chi tiết lệnh cắt chỉ còn đã cấp / khả dụng / đã nhận / đã trả / thực dùng; Thủ kho tự nhập số cấp. Lệnh cắt bù ước tính số mét từ dài × rộng chi tiết và khổ vải (`RecutFabricEstimator`, cùng công thức `vw_RecutFabricNeed`) so với kho khả dụng + vải tổ cắt đang giữ; thiếu kích thước thì bỏ qua kiểm.
- **Code đang làm theo sheet (tab "Doing"):**
  - Thành phần và định mức NVL do **Kế toán và Admin** nhập (`/accountant/products`: danh sách + định mức, không sửa mã hàng, không đơn giá); tab Màu – size chỉ đọc (màu – size do đơn hàng tạo), chỉ nhập kích thước thành phẩm; bỏ cột Thứ tự, Định mức / SP, % hao hụt.
  - Tồn kho: nhãn "Đang giữ chỗ" → "Đã lên phiếu". Hộp "Đơn cần xử lý": trạng thái có "Tất cả" (`status=All`). Đơn hàng: một mã hàng chỉ khai một khối / một đơn giá (FE loại mã đã chọn, validator chặn). NVL có thêm dài / rộng (khổ vải).
  - "NVL của đơn": chỉ hiện bảng chi tiết khi đã chọn đơn. Dòng "Nhật ký thay đổi" của sheet để trống nên không làm.
- **Cần sửa trong tài liệu:** `docs/NX-MTD_Database.dbml`, `NX-MTD_Database_Mo_ta.md` (Morgan cập nhật theo script 29/10), Report-3 (định mức không lượng vải, lệnh cắt không kiểm vải).
- **Trạng thái:** Đã làm

### DEV-ALL-15 · Sửa theo sheet ghi chú của Minh (28/10/2026)

> **Cập nhật 29/10 (DEV-ALL-16):** bậc thợ động, đơn tăng ca, xuất hàng 2 bước duyệt và so vải thực dùng với định mức bên dưới đã bị gỡ / thu nhỏ khi theo database 29/10.

- **Ngày / Người / Task / PR:** 28/10/2026 · Claude (AI) theo yêu cầu của Minh · nhiều luồng · PR #21
- **Đổi schema (có patch riêng, đã áp lên `NXMTD_demo5`; script chính `SQL/NX-MTD_Database.sql` đã cập nhật):**
  - `SQL/patches/2026-10-28_grade_level_code.sql`: bảng `GradeLevelCode` (danh mục bậc thợ), mã bậc `VARCHAR(5)` ở `GradeLevel` / `Employee` / `GradeEvaluation`, bỏ 3 CHECK cứng A–D, thay bằng khóa ngoại.
  - `SQL/patches/2026-10-28_end_of_order.sql`: lý do `END_OF_ORDER_MISMATCH` ở `CK_DR_Reason`, loại đơn `OVERTIME` ở `CK_Request_Type`, khóa `KPI_OT_AUTO_THRESHOLD_PCT` (mặc định 10) ở `SystemSetting`.
  - **Cần Morgan cập nhật** `docs/NX-MTD_Database.dbml` và `NX-MTD_Database_Mo_ta.md` (còn là bản 25/10).
- **Code đang làm (khác tài liệu / bản trước):**
  - **Menu và route theo loại tổ**: Tổ trưởng / Tổ phó / Công nhân của tổ cắt chỉ thấy Lệnh cắt, Bàn cắt, Bàn giao BTP; tổ may chỉ thấy giao việc, sản lượng, hàng hỏng, BTP của tổ. BE trả 403 (`CuttingTeamOnlyAttribute`) nếu tổ may gọi API của tổ cắt. Admin không có "Ngày công của tôi".
  - **Lệnh cắt theo đúng swimlane (BTP vào kho → trả vải thừa → so sánh → Quản đốc xem kết quả)**: OPEN → CUTTING (bàn cắt đầu tiên) → **COMPLETED khi tổ cắt bấm "Cắt xong"** (`WorkOrderService.FinishCuttingAsync`): không còn phiếu bàn giao / phiếu trả vải chờ kho, mọi BTP đã cắt được kho xác nhận, cắt thiếu kế hoạch thì nêu lý do. Lúc đó hệ thống so vải thực dùng (nhận − trả đã xác nhận) với định mức cho số đã cắt, báo Quản đốc; chi tiết lệnh hiện cột Đã trả / Thực dùng / Định mức theo đã cắt / Chênh lệch. Kho nhận BTP đủ KHÔNG còn tự hoàn thành lệnh. Bỏ `CompleteAsync` của Quản đốc và nút "Đóng lệnh". Kiểm tra vải khi lập lệnh trừ phần vải các lệnh cắt khác đã giữ chỗ, chặn dòng đơn chưa có định mức vải thân, xem trước vải ngay trong form, tab "Vải theo đơn".
  - **KPI**: chỉ tiêu tối đa = BTP còn trong kho + BTP tổ đã nhận chưa làm xong − phần còn phải làm của KPI khác (form hiện số này). **Chỉ đánh giá cuối đơn**: bỏ tự đóng theo ngày hết kỳ và xem theo ngày; Quản đốc bấm "Kết thúc đơn" → so sản lượng báo ở công đoạn cuối với QC đạt + hàng hủy, phần lệch ghi `DefectRecord` (SCRAP, lý do "Lệch kiểm cuối đơn") theo tỉ lệ sản lượng từng công nhân, chốt Đạt / Không đạt và đóng KPI. KPI không đạt: thiếu ≤ ngưỡng (10%) tự duyệt đơn tăng ca; thiếu hơn do tổ trưởng của tổ duyệt / từ chối (loại đơn `OVERTIME`, hiện ở "Đơn của tôi" của tổ). Đơn tăng ca chỉ là quyết định; giờ OT tính lương vẫn từ máy chấm công.
  - **Bậc thợ động**: Admin thêm / sửa / ngừng dùng / xóa (bậc chưa ai dùng) trong "Danh mục bậc"; sửa / xóa mức tiền bậc chưa có hiệu lực; mọi ô chọn bậc lấy từ API. Đặt bậc trực tiếp: chọn tổ → công nhân, hiện bậc hiện tại.
  - **Tài khoản**: Quản đốc quản lý mọi người trừ Admin và Kế toán (thêm, sửa kể cả kiểu lương, nhập Excel, nghỉ việc, khóa / mở khóa / đặt lại mật khẩu).
  - **Khác**: Khách hàng và Nhà cung cấp bắt buộc SĐT, email, địa chỉ (chỉ ở FE + validator, cột DB vẫn NULL); duyệt đơn giá công đoạn có tab "Tất cả", Admin không thấy Nháp; nút "Thêm công đoạn"; trang chủ có thông báo mới và (Admin) doanh thu; tìm kiếm tức thì ở các danh sách; bộ lọc mặc định "Tất cả"; "Tay áo" giữ một chi tiết × 2 (màn cắt bù ghi chú gồm tay trái + tay phải).
  - **Thông báo hệ thống (chỉ báo người phải xử lý tiếp; chuông tự nạp 30 giây và bật toast khi có mới):** lệnh cắt mới → tổ trưởng cắt + Thủ kho (cấp vải), sửa / hủy → tổ cắt; phiếu cấp vải / BTP chờ nhận → tổ nhận, bị từ chối → Thủ kho lập phiếu; bàn giao BTP chờ → Thủ kho, được xác nhận / từ chối → tổ cắt, lệnh cắt đủ kế hoạch → Quản đốc; phiếu nhập chờ xác nhận → Quản đốc, bị trả lại → Thủ kho; phiếu trả vải xác nhận / từ chối → tổ cắt; KPI giao / hủy → tổ trưởng tổ may. Thêm `INotificationService.NotifyTeamLeadersAsync`. Các luồng đã có sẵn (đơn mua, kiểm kê, xuất hàng, thành phẩm, giao việc, hàng hỏng, đơn từ) giữ nguyên.
  - **Xuất hàng có bước duyệt** (đổi DB: `CK_DN_Status` thêm PENDING_APPROVAL, APPROVED; `vw_StockBalance` giữ chỗ cả hai; patch `SQL/patches/2026-10-28_dispatch_approval.sql`): Quản đốc lập (PENDING) → Thủ kho lập phiếu xuất chốt số xuất thực tế ≤ số đặt (PENDING_APPROVAL) → Quản đốc duyệt (APPROVED) hoặc trả lại có lý do (về PENDING) → Thủ kho in, chụp ảnh phiếu có Quản đốc ký, xuất (IN_DELIVERY). Thủ kho vẫn từ chối được ở PENDING; Quản đốc hủy được đến khi hàng rời kho. **Ảnh phiếu xuất đã ký bắt buộc khi xuất và ảnh biên bản giao có khách ký bắt buộc khi Kế toán xác nhận khách nhận.** Số xuất sau khi Thủ kho giảm là trần mới nếu bị trả lại (không lưu riêng số Quản đốc đặt ban đầu).
  - **NVL lỗi** (`DefectiveMaterialRequestHandler`): Quản đốc duyệt → kiểm tồn từng NVL; đủ thì tự tạo đơn Xin NVL cho tổ cắt (Thủ kho cấp bù), thiếu thì tự tạo đơn mua chờ chốt cho phần thiếu (gắn `SourceRequestId`); kết quả ghi ở `ResultNote` và báo Quản đốc / Thủ kho.
  - **Nhận thành phẩm → KPI**: Thủ kho xác nhận phiếu bàn giao thành phẩm thì so số thành phẩm kho đã nhận của đơn + mã hàng với KPI đang hoạt động và báo Quản đốc đạt / chưa đạt (chỉ báo, đóng KPI vẫn qua "Kết thúc đơn").
  - **MaterialStatus**: chốt "không bù" NVL mua từ NCC không còn làm đơn hàng COMPLETE (giữ PARTIAL, đơn mua vẫn CLOSED_SHORT); NVL khách cấp giữ như cũ.
  - **Giờ chốt suất ăn cấu hình được**: Admin đổi giờ nhắc (mặc định 08:15) và giờ chốt (mặc định 08:30) ở "Giờ chốt suất ăn" (`SystemSetting` MEAL_REMINDER_TIME / MEAL_CUTOFF_TIME, giờ chốt trong khoảng 05:00–11:00, nhắc sớm hơn chốt, ghi nhật ký, có hiệu lực ngay vì xóa cache đọc). Job JOB-02 / JOB-03 và kiểm tra khóa của `MealService` đã đọc khóa này sẵn; thông báo và chữ trên giao diện không còn ghi cứng 08:30. Trigger DB chỉ dựa trạng thái ngày (OPEN) nên không phụ thuộc giờ; chỉ câu thông báo lỗi trong trigger còn ghi "08:30".
  - **Khách hàng có trạng thái** (đổi DB: `Customer.Status` ACTIVE / DISCONTINUED như Supplier; patch `SQL/patches/2026-10-29_customer_status.sql`): Admin "Ngừng hợp tác" / "Hợp tác lại" (`CustomerService.DiscontinueAsync/ReactivateAsync`, ghi nhật ký), lọc theo trạng thái; tạo đơn hoặc đổi sang khách ngừng hợp tác bị chặn (`CUSTOMER_NOT_ACTIVE`), đơn cũ giữ khách cũ vẫn sửa được.
  - **Nhập liệu**: ô Màu (tạo đơn, tab Màu – size của định mức) gợi ý màu đã khai ở các mã hàng (`GET common/order-lookups/product-colors`, gõ không dấu "đ" ra "Đen", vẫn nhập được màu mới); NCC bắt buộc "Loại NVL cung cấp" (chỉ ở ứng dụng, cột DB vẫn NULL được); mã hàng gọi là "Ngừng sản xuất" / "Sản xuất lại"; tab Định mức NVL có tìm kiếm, lọc theo thành phần / NVL / màu / size và phân trang.
  - **Doanh thu** vẫn theo SL hoàn thiện đã nhập kho thành phẩm × đơn giá CMT (không theo SL đã giao).
- **Cần sửa trong tài liệu:** Report-3 (KPI cuối đơn, đơn tăng ca, danh mục bậc, phạm vi tài khoản Quản đốc), `.dbml` / mô tả DB.
- **Trạng thái:** Đã làm

### DEV-ALL-14 · Chuyển code sang database bản 26/10 (27/10/2026)

- **Ngày / Người / Task / PR:** 27/10/2026 · Claude (AI) theo yêu cầu của Minh · kho, mua hàng, cắt, may · PR #20
- **Ảnh hưởng tới:** `SQL/NX-MTD_Database.sql` (bản 26/10 + mục demo 19.3 do Claude viết thêm), `StockKey` / `StockRepository` (bỏ `@MaterialColor` của `usp_Stock_Post`), `MaterialService`, `OrderMaterialService`, `PurchaseOrderService`, `MaterialReceiptService`, `MaterialIssueService`, `StocktakeService`, `CuttingLayService`, `FabricPlanner`, `ProductSpecService`, `WorkAllocationService`, `OutputService`, `QcBatchService`, `InventoryService` và các màn NVL / định mức / đơn mua / phiếu / bàn cắt / kiểm kê / giao việc / BTP của tổ.
- **Tài liệu gốc ghi:** bản 25/10: mỗi dòng NVL có `MaterialColor` riêng (định mức, NVL của đơn, đơn mua, phiếu nhập / cấp, tồn kho, bàn cắt); `WorkAllocation` có `PieceQty` / `ConfirmedPieceQty`; view `vw_TeamPieceHolding`.
- **Code đang làm (khác bản cũ):**
  - **Màu NVL là một phần của NVL** (`Material.Color`, `''` = không phân màu): cùng loại vải khác màu là hai NVL (`UQ_Material_NameType` = tên + loại + màu). Chọn NVL là đã chọn màu nên các form bỏ ô màu; dòng đơn mua tồn kho dùng chung chỉ nhập màu khi thêm NVL mới. Danh mục NVL thêm cột / ô Màu; đổi màu NVL đã dùng (định mức, NVL của đơn, chứng từ, tồn kho) bị chặn. Thẻ kho, tồn, tính vải cần và vải tổ cắt giữ khớp theo `MaterialId`.
  - **Bàn cắt chỉ chọn vải** (bỏ `FabricColor`): vải phải là vải chi tiết PANEL trong định mức của các dòng đơn trên bàn; dòng cắt bù phải dùng đúng vải của bàn (kiểm trước, báo rõ, trigger 50049 là chốt chặn).
  - **Giao việc không còn đếm BTP giao kèm** (DB: mỗi công đoạn nhận riêng từng thành phần nên không đếm được BTP theo bộ đủ): chỉ còn số lần làm. Trần giao = số bộ BTP tổ đã nhận (phiếu cấp BTP đã nhận, dòng bộ đủ) qua `TeamPieceQueries.ReceivedSets`, đúng công thức trigger 50050. `Operation.UsesSemiFinished` chỉ để tham khảo (hiện thẻ "Dùng BTP").
  - **Trang "BTP của tổ" đổi nghĩa**: `vw_TeamPieceHolding` đã bỏ nên trang hiện số bộ đã nhận từ kho, QC đạt, hàng hủy và còn trên chuyền (không còn "tổ đang giữ" trừ số giao công nhân). Trần số lượng QC / báo sản lượng đọc số bộ đã nhận từ phiếu cấp BTP.
  - **Validate thêm trước khi tới trigger:** NVL trùng theo màu (nêu rõ NVL và màu); NVL của đơn phải có số cần hoặc số khách cấp > 0; dòng phiếu cấp NVL không lẫn dòng BTP và ngược lại; chi tiết cấp BTP / kiểm kê BTP phải là dòng định mức vải của đúng màu × size (50041); giao việc chỉ công đoạn may đang dùng, tổ phải đã nhận BTP, phiếu giao không mang lý do thu hồi; xác nhận giao việc từ 1 đến số trên phiếu, lệch phải có lý do.
  - **Dữ liệu demo 19.3** (chạy lại không nhân đôi): bù thẻ kho cho dữ liệu 19.1 qua `usp_Stock_Post` (phiếu nhập, cấp BTP, bàn giao thành phẩm…), tồn đầu kỳ, đơn **DEMO-DH003** với mỗi bước xác nhận có sẵn một phiếu đang chờ (xem `docs/DEMO_SCRIPT.md`).
- **Cần sửa trong tài liệu:** Report-3 (UC24 trang BTP của tổ, UC53–UC55 / UC60 không còn BTP giao kèm, UC38 màu NVL); `docs/NX-MTD_Database.dbml` và `NX-MTD_Database_Mo_ta.md` còn là bản 25/10 (Minh chưa gửi bản 26/10) — mô tả `vw_TeamPieceHolding` và các cột màu đã lỗi thời.
- **Trạng thái:** Đã làm

### DEV-ALL-13 · Chuyển code sang database bản 25/10 (26/10/2026)

- **Ngày / Người / Task / PR:** 26/10/2026 · Claude (AI) theo yêu cầu của Minh · kho, cắt, may, công đoạn · PR #19
- **Ảnh hưởng tới:** `SQL/NX-MTD_Database.sql` (thay bằng bản 25/10; thêm `docs/NX-MTD_Database.dbml`, `docs/NX-MTD_Database_Mo_ta.md`), `StockService`, `StockRepository`, `StocktakeService`, `WorkAllocationService` (thay `WorkerPieceMovementService`), `OutputService`, `OperationService`, `ProductSpecService`, các dịch vụ cắt / cấp BTP, giao diện Giao việc, Công đoạn, Sản lượng, Kiểm kê.
- **Tài liệu gốc ghi:** bản 24/10: thẻ kho tự ghi từng dòng, tồn tính lại từ thẻ kho; BTP giao / thu hồi cho công nhân theo dòng đơn (`WorkerPieceMovement`); trần sản lượng theo dòng đơn.
- **Code đang làm (khác bản cũ):**
  - **Ghi kho chỉ qua `dbo.usp_Stock_Post`** (trigger chặn ghi thẳng, 50047): `StockService.PostAsync` vẫn kiểm trước cả lô bằng `StockBalance` để báo đủ mọi mặt hàng thiếu, rồi gọi thủ tục cho từng dòng chứng từ trong transaction của caller; thủ tục khóa dòng tồn, chặn tồn âm (50048) và điền `BalanceAfter`. `sp_getapplock` còn dùng cho bước giữ chỗ (`EnsureAvailableAsync`). **Kiểm kê** lưu `StockBalanceId`; mặt hàng chưa có dòng tồn thì tạo dòng tồn 0.
  - **Cắt bù theo dòng định mức (chi tiết × NVL × màu của đúng màu × size)**: `ProductComponentId` → `ProductComponentMaterialId` ở dòng đơn từ, lệnh cắt, bàn cắt, bàn giao BTP, phiếu cấp BTP và `StockBalance`. Tổ may chọn "Tay áo – Vải gió Đen"; bàn cắt bù phải cắt đúng vải của dòng định mức (50049); kho BTP tách tồn theo dòng đó. Mã hàng: lưu định mức theo kiểu **cập nhật / thêm / xóa** (giữ `ProductComponentMaterialId`), không xóa rồi tạo lại; dòng định mức đã được dùng không xóa được, thành phần đã dùng thì ngừng dùng.
  - **Giao việc thay giao / thu hồi BTP** (`WorkAllocation`): mỗi lần giao là số lần làm một công đoạn × màu × size cho một công nhân, kèm BTP nếu công đoạn dùng BTP (`Operation.UsesSemiFinished`). Tổng số lần giao của công đoạn ≤ số bộ BTP tổ đã nhận (50050); BTP giao kèm ≤ BTP tổ giữ (50053); BTP của tổ chỉ giảm theo BTP giao kèm, không giảm theo số lần. **Trần sản lượng theo từng công nhân × công đoạn** (`vw_WorkerOutputCap`): chưa được giao thì không báo được, DB chặn cứng (50052); `OutputService` bỏ trần cấp tổ cũ. Màn Giao việc hiện bảng chia việc (`vw_OperationAllocationProgress`) kèm số đã làm; tìm được theo tên công đoạn to.
  - **Công đoạn to** (`OperationGroup`), **công đoạn ↔ chi tiết** (`OperationComponent`), **cờ giao kèm BTP** (đã bỏ ở DEV-ALL-50): sửa ở màn Công đoạn và đơn giá; nhập Excel không đổi các trường này. `OperationAssignment.CopiedFromAssignmentId` đã bỏ (sao chép phân công vẫn chạy, chỉ không lưu nguồn).
  - Đã thay một phần ở DEV-ALL-14 (bản 26/10 bỏ BTP giao kèm và `vw_TeamPieceHolding`).
- **Cần sửa trong tài liệu:** Report-3 (UC24, UC40–UC44, UC52–UC55, UC60: giao việc theo công đoạn, trần sản lượng theo công đoạn, kho BTP theo chi tiết × vải); ERD.
- **Trạng thái:** Đã làm

### DEV-ALL-12 · Đối chiếu màn hình luồng chính với swimlane (25/10/2026)

- **Ngày / Người / Task / PR:** 25/10/2026 · Claude (AI) theo yêu cầu của Minh · UC37, UC39, UC43, UC44, UC45–UC47, UC51–UC57 · (chưa có PR)
- **Ảnh hưởng tới:** nhập kho, cắt, may, xuất hàng; chi tiết xem `docs/SO_SANH_SWIMLANE.md`.
- **Tài liệu gốc ghi:** swimlane và UC37 (bản v25) cho Thủ kho nhập số đạt, thiếu, lỗi; luồng xuất hàng "Thủ kho lập → Quản đốc xác nhận → Thủ kho xuất" (UC45–UC46) và "Quản đốc lập → Thủ kho chuẩn bị → Quản đốc duyệt → Thủ kho xuất" (III.4).
- **Code đang làm:**
  - Đã sửa theo swimlane: tổ may xác nhận nhận BTP thì đơn tự sang IN_PRODUCTION (nút của Admin vẫn dùng); tổ cắt rút phiếu bàn giao BTP khi Thủ kho chưa nhận; Quản đốc chỉ xác nhận phiếu nhập khi có ảnh chứng từ; ghi hàng hủy (Scrap) thì mở luôn form đơn cắt bù; tổ cắt có form đơn "NVL lỗi"; đặt KPI bị chặn khi tổng KPI vượt BTP đã vào kho; Quản đốc đóng lệnh cắt (kèm lý do khi BTP vào kho chưa đủ kế hoạch).
  - **Còn lệch (cần quyết định):** (1) Thủ kho không nhập thiếu / lỗi — quyết định của Minh, theo script mới; swimlane và UC37 v25 cần Morgan cập nhật. (2) **Chưa có bước Quản đốc duyệt phiếu xuất**: `DispatchNote.Status` chỉ có PENDING / REJECTED / CANCELLED / IN_DELIVERY / DELIVERED / PARTIALLY_DELIVERED và bảng không có cột người duyệt, nên không làm được nếu không sửa script của Morgan. Đề xuất cho Morgan: thêm trạng thái `APPROVED` vào `CK_DN_Status` và hai cột `ApprovedBy INT NULL`, `ApprovedAt DATETIME2(0) NULL` (kèm ràng buộc `Status IN ('APPROVED','IN_DELIVERY',...) → ApprovedBy NOT NULL`); khi có, code đổi: Quản đốc lập (PENDING) → Thủ kho gửi duyệt → Quản đốc duyệt / trả lại có lý do → Thủ kho in và xuất. (3) In phiếu xuất là Excel (swimlane ghi PDF), ảnh phiếu ký chưa bắt buộc, Kế toán chưa có ô ghi chú khi xác nhận giao.
- **Cần sửa trong tài liệu:** swimlane III.1 (Thủ kho không nhập thiếu / lỗi), III.4 (bước duyệt phiếu xuất, nếu Morgan thêm trạng thái).
- **Trạng thái:** Mở

### DEV-ALL-17 · Khôi phục bậc thợ động (31/10/2026)

- **Ngày / Người / Task / PR:** 31/10/2026 · Claude (AI) theo yêu cầu của Minh · UC50 · cùng nhánh `feature/ALL-missing-features`
- **Tài liệu gốc ghi:** Report-3 / AI_Context: bậc thợ A–D cố định. DEV-ALL-16 đã hoàn lại về `CHAR(1)` A–D khi theo database 29/10.
- **Code đang làm:** khôi phục phần của DEV-ALL-15 (commit `e8e66e0`): bảng `GradeLevelCode` (danh mục bậc), mã bậc `VARCHAR(5)` ở `GradeLevel` / `Employee` / `GradeEvaluation`, khóa ngoại thay cho CHECK A–D; Admin thêm / sửa tên và thứ tự / ngừng dùng / xóa bậc chưa dùng; sửa / xóa mức tiền bậc chưa hiệu lực. FE: tab "Danh mục bậc" ở màn Bậc thợ, các ô chọn bậc lấy từ danh mục.
- **Đổi schema:** `SQL/NX-MTD_Database.sql` sửa lại theo bản có `GradeLevelCode` (khác bản 29/10 của nhóm ở 3 chỗ này); DB đã dựng cần chạy `SQL/patches/2026-10-28_grade_level_code.sql` (chạy lại không lỗi). Cần nhóm DB xác nhận trước khi hợp nhất với bản 29/10.
- **Cần sửa trong tài liệu:** Report-3 UC50, AI_Context mục 56 (bậc A–D), NX-MTD_Database.dbml / Mo_ta (`GradeLevelCode`, kiểu cột mã bậc).
- **Trạng thái:** Mở

### DEV-ALL-18 · Tổ chức lại thư mục backend theo module (31/10/2026)

- **Ngày / Người / Task / PR:** 31/10/2026 · Claude (AI) theo yêu cầu của Minh · tổ chức code · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Tài liệu gốc ghi:** Coding Rules 3.1: `Application/Services`, `Interfaces`, `DTOs`, `Validators` là thư mục phẳng; 3.7: cấu hình EF bằng `IEntityTypeConfiguration<T>`.
- **Code đang làm:**
  - Mỗi thư mục trên chia thư mục con theo module: Shared, Auth, Hr, Orders, Requests, Warehouse, Cutting, Purchasing, Production, Attendance, Finance. `IUnitOfWork`, `IGenericRepository` giữ ở gốc `Interfaces/`. **Namespace giữ theo tầng** (VD `NX_MTD.Application.Services`, không có đoạn module), nên không phải sửa `using` ở nơi dùng; các service từng nằm trong `Services/Hr|Orders|Production|Purchasing|Requests` đã đổi từ namespace lồng về namespace phẳng.
  - `NxMtdDbContext.OnModelCreating` (2651 dòng) tách thành 88 file `Infrastructure/Data/Configurations/<Entity>Configuration.cs`, nạp bằng `ApplyConfigurationsFromAssembly`; mô hình EF so sánh trước / sau giống hệt. Lưu ý: nếu scaffold lại bằng `--force` thì các file này sẽ bị ghi đè, cần chuyển cấu hình mới vào đây.
  - Tách các method quá 40 dòng (mới làm `FinanceService.CalculateAsync`, `OutputService.SaveAsync`; còn 37 method).
- **Cần sửa trong tài liệu:** Coding Rules 3.1 (ghi cấu trúc thư mục con theo module).
- **Trạng thái:** Mở

### DEV-ALL-19 · AuditLog có thêm cột Summary (câu mô tả tiếng Việt) (01/11/2026)

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) theo yêu cầu của Minh · nhật ký thay đổi · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Tài liệu gốc ghi:** schema `AuditLog` chỉ có `OldValues` / `NewValues` (JSON) và `Reason`; người đọc phải đọc JSON.
- **Code đang làm:**
  - Thêm cột `AuditLog.Summary NVARCHAR(500) NULL` (patch `SQL/patches/2026-11-01_all_changes.sql`, script chính đã cập nhật, đã áp lên `NXMTD`).
  - `AuditLogService.Add` tự dựng câu mô tả bằng `AuditText.Summary` (hành động + đối tượng + mã bản ghi + các field đổi, dịch tiếng Việt, kèm lý do), nên mọi chỗ gọi `_audit.Add` đều có câu mô tả mà không phải sửa. `AuditEntry` có thêm tham số `Summary` để chỗ gọi tự viết câu riêng khi cần (đã dùng ở bậc thợ và tài khoản).
  - Dòng log cũ để `Summary = NULL`; giao diện tự ghép "{hành động} {đối tượng} #{Id}". Danh sách nhãn đối tượng / trường / giá trị nằm ở `AuditText` (BE) và `fe/src/utils/auditLabels.ts` (FE): thêm bảng / trường mới thì cập nhật cả hai.
- **Cần sửa trong tài liệu:** Report-3 / script DB (mô tả bảng `AuditLog`).
- **Trạng thái:** Mở

### DEV-ALL-20 · Thủ kho xem tình trạng đơn mua NVL: còn nhận tiếp hay đã đóng do giao thiếu (31/10/2026)

- **Ngày / Người / Task / PR:** 31/10/2026 · Claude (AI) theo yêu cầu của Minh · UC39 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** UC39 (nhận NVL), màn "NVL chờ nhận", `PurchaseOrder.GoodsStatus`
- **Tài liệu gốc ghi:** màn chỉ liệt kê dòng đơn mua còn phải nhận; đơn đã đóng thiếu (CLOSED_SHORT) biến mất khỏi màn.
- **Code đang làm:** `GET .../incoming-materials` trả thêm `goodsStatus` cho từng dòng chờ nhận (ORDERED / PARTIALLY_RECEIVED, hiện "Còn nhận tiếp được") và mảng `closedShort` (dòng đơn mua CLOSED_SHORT kèm đã đặt, đã nhận, thiếu đã chốt không bù). Không đổi schema, không thêm trạng thái mới.
- **Cần sửa trong tài liệu:** Report-3 UC39 (màn NVL chờ nhận).
- **Trạng thái:** Mở

### DEV-ALL-21 · Phiếu nhập: ảnh trước khi lưu, chặn NVL đã chốt không bù, điền sẵn nhiều NVL (31/10/2026)

- **Ngày / Người / Task / PR:** 31/10/2026 · Claude (AI) theo yêu cầu của Minh · UC37, UC39 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** UC37 (lập phiếu nhập), UC39 (xử lý thiếu NVL), `MaterialReceipt`, `Request` (SHORT_DELIVERY)
- **Tài liệu gốc ghi:** Thủ kho thêm ảnh chứng từ ở màn chi tiết phiếu sau khi lưu; phiếu khách cấp không có bước chặn sau khi Quản đốc chốt "không bù".
- **Code đang làm:**
  - Form lập phiếu nhập có ô chọn ảnh chứng từ (tối đa 5), ảnh tải lên ngay sau khi lưu phiếu mới; màn chi tiết phiếu chỉ xem ảnh, không thêm / gỡ. Khi sửa phiếu (chờ xác nhận / bị trả) ảnh thêm / gỡ ngay trong form sửa.
  - NVL khách cấp mà Quản đốc đã chốt "không bù" ở đơn thiếu NVL: BE từ chối lập phiếu (`RECEIPT_SHORTAGE_CLOSED`, 409) và màn NVL chờ nhận không liệt kê dòng đó. Đơn mua nhà cung cấp đã chặn sẵn qua `NotReplenishedQty`.
  - Chọn đơn mua / đơn hàng thì form điền sẵn mọi dòng NVL còn phải nhận; thêm dòng bằng ô chọn các NVL còn lại.
- **Cần sửa trong tài liệu:** Report-3 UC37 (luồng ảnh chứng từ), UC39 (hậu quả của chốt không bù với phiếu khách cấp).
- **Trạng thái:** Mở

### DEV-ALL-22 · Tồn kho NVL: lọc NVL không thuộc đơn hàng nào (31/10/2026)

- **Ngày / Người / Task / PR:** 31/10/2026 · Claude (AI) theo yêu cầu của Minh · UC24 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Tài liệu gốc ghi:** màn tồn kho chỉ lọc theo đơn hàng.
- **Code đang làm:** `GET .../inventories?warehouseType=MATERIAL` nhận thêm `orderScope=NO_ORDER` (NVL không thuộc đơn nào, như hàng mua dự trữ dùng chung). FE gộp vào ô lọc đơn hàng ở tab Kho NVL: chọn một đơn hoặc "Không thuộc đơn hàng nào". Không đổi schema.
- **Cần sửa trong tài liệu:** Report-3 UC24 (bộ lọc màn tồn kho).
- **Trạng thái:** Mở

### DEV-ALL-23 · KPI tổ may: không trùng đơn + mã hàng + tổ, KPI theo ngày trên lịch tháng

- **Ngày / Người / Task / PR:** 31/10/2026 · Claude (AI) · UC19, UC21, FR 3.14 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** schema (patch `SQL/patches/2026-11-01_all_changes.sql`, script chính đã cập nhật): `UQ_Kpi`, bảng `KpiDailyTarget`, `vw_KpiDailyResult`. API: `GET manager/kpi-targets/order-capacity` thay `available-btp`; thêm `GET manager|admin/kpi-targets/{id}/calendar`, `PUT manager/kpi-targets/{id}/daily-plan`; `SaveKpiTargetDto` bỏ `WorkingDays`.
- **Tài liệu gốc ghi:** `UQ_Kpi` gồm cả `StartDate`, nên một tổ lập được nhiều chỉ tiêu cho cùng đơn + mã hàng. Swimlane III.3.1: đặt KPI thì kiểm BTP khả dụng. Chỉ tiêu ngày = tổng ÷ `WorkingDays` nhập tay. DEV-ALL-11 đã gỡ KPI theo ngày.
- **Lý do:** yêu cầu của người dùng: chỉ tiêu cả kỳ chỉ so với số lượng đơn đặt, BTP chỉ kiểm khi đặt KPI theo ngày; một đơn + mã hàng có thể nhiều tổ làm, một tổ làm nhiều đơn + mã hàng.
- **Code đang làm:**
  - **Không trùng:** `UQ_Kpi` thành filtered index (`OrderId, ProductId, SewingTeamId`, `Status <> 'CANCELLED'`). Chỉ tiêu đã hủy thì lập lại được; chỉ tiêu đã đóng vẫn tính là trùng. Patch chuyển các chỉ tiêu trùng sẵn có sang `CANCELLED` (giữ chỉ tiêu ACTIVE, không có thì giữ chỉ tiêu mới nhất). Dữ liệu demo: chỉ tiêu tháng 9 của DEMO-DH001 đổi từ `CLOSED` sang `CANCELLED`, nên demo không còn dòng KPI đã đóng.
  - **Chỉ tiêu cả kỳ:** tổng chỉ tiêu các tổ của một đơn + mã hàng không vượt Σ `OrderLine.OrderedQty` (409 `KPI_EXCEEDS_ORDER_QTY`). Chỉ tiêu đang chạy tính cả chỉ tiêu, chỉ tiêu đã đóng tính số thực tế làm được, đã hủy không tính. Không kiểm BTP ở bước này.
  - **KPI theo ngày:** nút "KPI theo ngày" ở bảng KPI mở modal lịch tháng (`KpiCalendarModal`, dùng `Calendar` của Ant Design). Quản đốc gõ KPI từng ô; ngày đã qua không sửa được (BE chặn); Admin và chỉ tiêu đã đóng / hủy chỉ xem. Mỗi ô hiện KPI và thực tế QA/QC. Tổng các ngày không vượt chỉ tiêu cả kỳ. `WorkingDays` = số ngày đã đặt, chưa đặt thì = số ngày trong kỳ trừ Chủ nhật. Đổi kỳ thì bỏ các ngày ngoài kỳ mới; chỉ tiêu cả kỳ không được nhỏ hơn tổng KPI các ngày còn lại.
  - **BTP khả dụng khi đặt KPI ngày** = BTP bộ đủ trong kho + BTP tổ đang giữ − phần các tổ khác phải lấy từ kho (KPI đã đặt chưa hoàn thiện của tổ đó trừ BTP tổ đó đang giữ, chỉ tính KPI đang chạy). Lưu bị chặn (409 `KPI_EXCEEDS_STOCK`) khi KPI đã đặt chưa hoàn thiện vượt số này; đang giảm thì vẫn lưu được. Kiểm theo BTP hiện có, chưa tính BTP sẽ cắt thêm, nên đơn chưa cắt đủ thì chỉ đặt được phần đã có BTP.
  - **Thông báo sau khi kho nhận thành phẩm** (khác DEV-ALL-15): phiếu bàn giao không ghi tổ, nên trước đây tổng kho nhận của đơn + mã hàng bị so với chỉ tiêu của từng tổ, báo "đã đạt" sai khi nhiều tổ cùng làm. Nay mỗi đơn + mã hàng một thông báo: kho nhận so với tổng chỉ tiêu các tổ đang chạy; từng tổ đạt hay chưa tính theo QA/QC trong kỳ của tổ, chỉ tổ đạt mới được gợi ý Kết thúc đơn.
  - Khác DEV-ALL-9 (bản cũ đã gỡ): không có "Chia tăng dần" / "Chia đều", không dự báo BTP từng ngày; BTP tính theo kho + tổ giữ của DB mới thay vì giao dịch cắt − QC.
- **Ảnh hưởng người khác:** Morgan (schema), DB đang chạy phải chạy patch trước khi chạy backend mới.
- **Cần sửa trong tài liệu:** Report-3 UC19, UC21, FR 3.14 (ràng buộc, kiểm số lượng đơn, KPI theo ngày, BTP); `NX-MTD_AI_Context.md` phần KPI tổ may; `NX-MTD_Database.dbml`, `NX-MTD_Database_Mo_ta.md`, ERD.
- **Trạng thái:** Mở

### DEV-ALL-24 · KPI tổ may: nhiều chỉ tiêu không trùng kỳ, khóa KPI ngày sau 08:00

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC19, FR 3.14 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** schema (patch `SQL/patches/2026-11-01_all_changes.sql`, script chính đã cập nhật): `UQ_Kpi` thêm lại `StartDate`, khóa `KPI_EDIT_CUTOFF_TIME`. API lịch KPI thêm `firstEditableDate`, `editCutoff`. Mã lỗi `KPI_PERIOD_OVERLAP` thay `KPI_DUPLICATE_TARGET`.
- **Tài liệu gốc ghi:** DEV-ALL-23: mỗi đơn + mã hàng + tổ chỉ một chỉ tiêu; ngày đã qua không sửa được.
- **Lý do:** yêu cầu của người dùng.
- **Code đang làm:**
  - **Nhiều chỉ tiêu:** cùng đơn + mã hàng + tổ được lập nhiều chỉ tiêu, nhưng các kỳ không được có chung ngày nào, kể cả ngày kết thúc kỳ này trùng ngày bắt đầu kỳ kia (409 `KPI_PERIOD_OVERLAP`). Tính cả chỉ tiêu đã đóng, không tính chỉ tiêu đã hủy. Trùng kỳ kiểm ở `KpiService`; `UQ_Kpi` (lọc `Status <> 'CANCELLED'`) chỉ chặn trùng ngày bắt đầu. Bỏ quy tắc "một chỉ tiêu" của DEV-ALL-23.
  - **Số lượng đơn:** tổng các chỉ tiêu không vượt số lượng đơn đặt, tính cả các chỉ tiêu khác của cùng tổ.
  - **BTP khả dụng:** chỉ tiêu kỳ khác của cùng tổ trừ thẳng phần còn phải làm vì dùng chung BTP tổ đang giữ; tổ khác vẫn chỉ trừ phần phải lấy từ kho.
  - **Kết thúc đơn:** thực tế = QC cả đơn của tổ − QC nằm trong kỳ của các chỉ tiêu khác cùng đơn + mã hàng + tổ, để không tính lặp. Một chỉ tiêu thì không đổi.
  - **Patch:** các patch chưa đẩy lên remote gộp vào `SQL/patches/2026-11-01_all_changes.sql` (audit log, lý do ngừng hợp tác, bỏ `Material.LengthCm`, KPI). File gộp bỏ bước chuyển chỉ tiêu trùng sang `CANCELLED` của DEV-ALL-23, vì nhiều kỳ giờ hợp lệ và `UQ_Kpi` cũ đã gồm `StartDate` nên không có dòng trùng.
  - **Khóa KPI ngày:** KPI của một ngày không sửa được từ 08:00 của chính ngày đó (khóa `KPI_EDIT_CUTOFF_TIME`, giờ Việt Nam, thiếu khóa thì mặc định 08:00). BE chặn cả lưu lịch lẫn đổi kỳ làm mất ngày đã khóa. Chỉ tiêu cả kỳ vẫn sửa được.
- **Ảnh hưởng người khác:** Morgan (schema), DB đang chạy phải chạy patch.
- **Cần sửa trong tài liệu:** Report-3 UC19 (ràng buộc kỳ, giờ khóa); `NX-MTD_AI_Context.md` mục 6.4 (thêm khóa `KPI_EDIT_CUTOFF_TIME`); `NX-MTD_Database.dbml`, `NX-MTD_Database_Mo_ta.md`.
- **Trạng thái:** Mở

### DEV-ALL-25 · KPI tổ may: kiểm ngày của kỳ và cảnh báo theo ngày giao hàng

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC19 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** khóa mới `KPI_DUE_WARNING_DAYS` (patch `SQL/patches/2026-11-01_all_changes.sql`, script chính đã cập nhật); `GET manager/kpi-targets/order-capacity` thêm `dueDate`, `dueWarningDays`; mã lỗi `KPI_PERIOD_INVALID`; bố cục form lập KPI.
- **Tài liệu gốc ghi:** chỉ yêu cầu ngày kết thúc không trước ngày bắt đầu; không nhắc tới ngày giao hàng khi lập KPI.
- **Lý do:** yêu cầu của người dùng, để Quản đốc kiểm soát kỳ KPI theo ngày giao.
- **Code đang làm:**
  - **Chặn (409 `KPI_PERIOD_INVALID`):** ngày bắt đầu trước ngày nhận đơn hoặc ở quá khứ; ngày kết thúc ở quá khứ. Khi sửa chỉ kiểm mốc ngày bị đổi, để chỉ tiêu đang chạy vẫn sửa được số lượng.
  - **Chỉ cảnh báo, vẫn lưu được:** kỳ kết thúc sau ngày giao, hoặc cách ngày giao không quá `KPI_DUE_WARNING_DAYS` ngày (mặc định 3). Cảnh báo tính ở form, BE không chặn.
  - **Trạng thái đơn:** đơn đã dừng, hủy, đã giao hoặc hoàn tất thì không lập, không sửa chỉ tiêu và không đặt KPI theo ngày (lịch chỉ xem, trước đây đặt KPI ngày chưa kiểm). Đơn đang giao hoặc giao một phần vẫn đặt được, form cảnh báo. Hủy chỉ tiêu và Kết thúc đơn vẫn làm được để đóng KPI của đơn đã dừng.
  - **Lịch KPI theo ngày:** API lịch thêm `dueDate`. Modal hiện ngày giao hàng, đánh dấu ô ngày giao, tô nền các ô sau ngày giao và cảnh báo khi có KPI đặt sau ngày giao; vẫn lưu được.
  - **Form:** bố cục hai cột; sau khi chọn đơn và mã hàng hiện ngày giao hàng, số ngày còn lại, số lượng đơn, phần đã giao và số tối đa. Danh sách đơn hiện kèm ngày giao. Lập mới không chọn được ngày đã qua.
- **Cần sửa trong tài liệu:** Report-3 UC19; `NX-MTD_AI_Context.md` mục 6.4 (thêm khóa `KPI_DUE_WARNING_DAYS`).
- **Trạng thái:** Mở

### DEV-ALL-26 · Màn NVL của đơn: danh sách đơn chờ là màn chính

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC09 (phần NVL) · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `GET admin|manager/order-materials/pending-orders` nhận `page, pageSize, keyword, materialState` và trả dạng phân trang; không đổi schema.
- **Tài liệu gốc ghi:** DEV-ALL-10: màn NVL của đơn có ô chọn đơn, phía dưới là bảng đơn chờ không phân trang.
- **Lý do:** yêu cầu của người dùng.
- **Code đang làm:** Màn chính là bảng "Đơn hàng chờ khai báo / mua NVL" (phân trang server, tìm mã đơn / khách hàng, lọc tình trạng NVL); đơn chưa khai báo lên đầu. Bấm Mở đơn mới sang NVL của đơn, có nút quay lại; `?orderId=` trong thông báo vẫn mở thẳng. Bỏ ô chọn đơn; đơn đã đủ NVL mở qua lựa chọn lọc "Đã đủ NVL". Chỉ gồm đơn Mới và Đang sản xuất, nên đơn đang giao chỉ mở được qua đường dẫn. Quản đốc tạo đơn mua dùng chung bằng nút "Tạo đơn mua dùng chung" ở danh sách.
- **Bảng NVL của một đơn:** thêm `GET admin|manager/order-materials/paged` (`orderId, page, pageSize, keyword, supply, receiptState`). Tìm mã, tên, màu NVL; lọc nguồn cấp (khách cấp, phải mua) và tình trạng nhập kho (đã nhận đủ khi số nhận ≥ số cần). `GET order-materials` giữ nguyên cho các form cần đủ dòng.
- **Cần sửa trong tài liệu:** Report-3 UC09 (màn NVL của đơn).
- **Trạng thái:** Mở

### DEV-ALL-27 · JOB-04: cảnh báo nguy cơ trễ cho đơn đã bắt đầu giao

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC21, JOB-04 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `OrderProgressService.RunDelayCheckAsync`, cờ `IsDelayRisk` / `DelayRiskSince`; không đổi schema.
- **Tài liệu gốc ghi:** JOB-04 đặt cờ theo số lượng còn lại; code chỉ quét đơn `IN_PRODUCTION`.
- **Lý do:** yêu cầu của người dùng. Đơn giao từng đợt rời `IN_PRODUCTION` khi xuất kho đợt đầu dù phần còn lại vẫn đang sản xuất, nên bị bỏ sót cảnh báo.
- **Code đang làm:**
  - Quét `IN_PRODUCTION`, `IN_DELIVERY`, `PARTIALLY_DELIVERED` (`OrderStatuses.DelayTracked`). Công thức giữ nguyên, số còn lại vẫn tính theo số hoàn thiện (QC đạt) so với số đặt, không theo số đã giao.
  - Đơn ngoài nhóm này mà còn cờ cũ (đã giao, dừng, hủy...) được gỡ cờ ở lần chạy kế tiếp, không gửi thông báo. Trước đây cờ cũ không bao giờ được gỡ.
- **Cần sửa trong tài liệu:** Report-3 UC21, mô tả JOB-04.
- **Trạng thái:** Mở

### DEV-ALL-28 · Mã hàng: ảnh chính + 10 ảnh phụ, giới hạn nhập mã hàng và định mức

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC10 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `Product`, bảng mới `ProductImage`, `GET admin|accountant/products`, `POST / PUT admin/products`, `PUT .../{id}/spec`
- **Tài liệu gốc ghi:** mã hàng có một ảnh mẫu (`Product.SampleImageUrl`); định mức không ghi giới hạn kích thước, số chi tiết hay số dòng mỗi lần lưu.
- **Lý do:** yêu cầu của người dùng (mã hàng có 1 ảnh chính và 10 ảnh phụ, dọn ảnh mồ côi); các giới hạn còn lại chặn lỗi hệ thống khi nhập số quá cột database và yêu cầu quá lớn.
- **Code đang làm:**
  - **Ảnh:** ảnh chính vẫn là `Product.SampleImageUrl` (giữ dữ liệu cũ, nhãn đổi thành "Ảnh chính"). Ảnh phụ lưu ở bảng `ProductImage` (`SortOrder` 1-10, mỗi vị trí một ảnh, database tự chặn ảnh thứ 11). `SaveProductDto.ExtraImages`: null = giữ nguyên, danh sách rỗng = xóa hết; chỉ nhận khóa ảnh tải từ máy, không lặp, tối đa 10. Danh sách mã hàng và chi tiết trả thêm `extraImages`. Ảnh bị gỡ hoặc bị thay không xóa ngay mà để job dọn ảnh mồ côi xử lý (xem bên dưới). Bảng `Attachment` không dùng vì ảnh mã hàng cần thứ tự và lưu nguyên danh sách trong một lần, còn `Attachment` chỉ ẩn ảnh gỡ (`IsCurrent = 0`) và cần `OwnerType` mới cùng quyền riêng.
  - **Dọn ảnh mồ côi:** job hằng ngày lúc 02:00 (`PRODUCT_IMAGE_CLEANUP_TIME`, chưa seed nên mặc định 02:00) xóa file trong thư mục `PRODUCT` mà không còn `Product.SampleImageUrl` hay `ProductImage.ImageKey` nào trỏ tới. Chỉ xóa file cũ hơn 24 giờ (ảnh được tải lên ngay khi chọn, trước khi bấm Lưu) và chỉ file đúng dạng khóa ảnh mã hàng. Ảnh còn nằm trong lịch sử nhật ký không được giữ lại. `IFileStorage` thêm `ListAsync` và `DeleteAsync`.
  - **Mã hàng:** mã tối đa 30 ký tự tính sau khi cắt khoảng trắng đầu cuối, chỉ chữ không dấu, số và `. _ -`.
  - **Định mức:** kích thước chi tiết từ 0,01 đến 99.999,99 cm, tối đa 2 số lẻ (cột DECIMAL(7,2); trước đây vượt thì lỗi hệ thống 500); số chi tiết trên một sản phẩm từ 1 đến 99; tối đa 200 thành phần, 200 màu – size và 5.000 dòng định mức (tính sau khi nở theo màu – size) mỗi lần lưu. Dòng định mức trỏ vào thành phần bằng khóa dòng trên màn hình nên đổi tên thành phần thì dòng định mức đi theo; xóa thành phần thì xóa luôn các dòng định mức dùng nó.
  - **Sửa lỗi:** form sửa mã hàng nạp mô tả từ chi tiết (trước đây lưu lại là xóa mất mô tả).
- **Database:** `SQL/NX-MTD_Database.sql` thêm `ProductImage`; patch `SQL/patches/2026-11-01_all_changes.sql` cho database đã dựng (chạy lại không lỗi).
- **Cần sửa trong tài liệu:** Report-3 UC10 (ảnh mã hàng, giới hạn định mức); ERD (bảng `ProductImage`).
- **Trạng thái:** Mở

### DEV-ALL-29 · Dọn tệp chứng từ: ảnh đã gỡ giữ 15 ngày rồi xóa

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · `Attachment` · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** bảng `Attachment` (thêm `RemovedAt`), `IFileStorage`, `AttachmentService.RemoveAsync`
- **Tài liệu gốc ghi:** gỡ ảnh chỉ đặt `IsCurrent = 0`, không xóa file, không xóa dòng (ảnh cũ giữ lại mãi).
- **Lý do:** yêu cầu của người dùng: ảnh chứng từ bị thay đổi mà thành mồ côi thì sau 15 ngày sẽ xóa.
- **Code đang làm:**
  - Gỡ ảnh ghi thêm `Attachment.RemovedAt`. Job hằng ngày lúc 02:30 (`ATTACHMENT_CLEANUP_TIME`, chưa seed nên mặc định 02:30) xóa file rồi xóa dòng của ảnh có `IsCurrent = 0` và `RemovedAt` quá 15 ngày. File không xóa được thì giữ dòng để thử lại lần sau.
  - Cùng job: file trong kho không còn dòng `Attachment` nào trỏ tới (vd. tải lên xong nhưng chưa ghi được dòng) và cũ hơn 15 ngày cũng bị xóa. Ảnh đã gỡ còn trong hạn giữ vẫn được bảo vệ vì dòng của nó còn.
  - Chỉ áp dụng loại chứng từ có tệp ở `IFileStorage` (`AttachmentOwnerTypes.InFileStorage`) và file đúng dạng khóa ảnh. Tệp bảng lương và file chấm công nhập có kho riêng nên không bị đụng. Ảnh đang dùng (`IsCurrent = 1`) không bao giờ bị xóa.
  - Ảnh đã gỡ trước khi có cột `RemovedAt` được gán mốc là lúc chạy patch (giữ thêm 15 ngày); dòng gỡ mà không có `RemovedAt` không bị xóa.
- **Database:** `SQL/NX-MTD_Database.sql` thêm cột; patch `SQL/patches/2026-11-01_all_changes.sql` cho database đã dựng (chạy lại không lỗi).
- **Cần sửa trong tài liệu:** Report-3 phần đính kèm; ERD (cột `RemovedAt`).
- **Trạng thái:** Mở

### DEV-ALL-30 · Khách hàng, nhà cung cấp: siết kiểm tra giá trị nhập

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC09 (khách hàng), UC14 (nhà cung cấp) · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `SaveCustomerDto`, `SaveSupplierDto`, lý do ngừng hợp tác, `GET admin/customers?keyword`, `PhoneRule`; không đổi schema.
- **Tài liệu gốc ghi:** email phải hợp lệ, số điện thoại 10 số; không nêu giới hạn độ dài hay ký tự được phép.
- **Lý do:** yêu cầu của người dùng (kiểm tra validate và giá trị biên). Trước đây email "nguyễn@b.vn" được nhận rồi lưu thành "nguy?n@b.vn" (cột `Email` là VARCHAR); "a@b" và "a b@c.vn" cũng lọt; từ khóa tìm khách hàng dài 5.000 ký tự gây lỗi hệ thống; tên đúng 200 ký tự kèm khoảng trắng đầu cuối bị từ chối dù bản lưu chỉ 200 ký tự.
- **Code đang làm:**
  - **Email:** dạng `ten@mien.com`, chỉ ký tự ASCII, tên miền có ít nhất một dấu chấm, không có khoảng trắng (`EmailRule` ở BE, `utils/email.ts` ở FE). Email có chữ có dấu bị từ chối thay vì lưu sai.
  - **Số điện thoại:** `PhoneRule` dùng `[0-9]` và `z` thay vì `d` và `$` (`d` của .NET khớp cả chữ số Ả Rập – Ấn Độ, `$` cho phép xuống dòng ở cuối).
  - **Độ dài:** tên 200, email 100, địa chỉ 500, loại NVL 200, ghi chú 500, lý do ngừng hợp tác 500, từ khóa tìm khách hàng 100; tính sau khi cắt khoảng trắng đầu cuối, thông báo bằng tiếng Việt (`MaxTrimmedLength`). Số trang và số dòng mỗi trang cũng báo bằng tiếng Việt.
  - **Giao diện:** quy tắc nhập dùng Zod (`utils/partnerRules.ts`) khớp BE; lỗi trùng tên và lỗi từng trường hiện ngay dưới ô (form nhà cung cấp trước đây chỉ có thông báo chung).
  - **Phân trang:** `GET admin/customers` phân trang phía server (`page`, `pageSize`, `keyword`, `status`) và trả `PagedResult`, đổi so với trước là trả cả danh sách. Các ô chọn khách hàng (form đơn hàng, bộ lọc đơn hàng) dùng `GET admin/customers/options`: mọi khách hàng ở dạng rút gọn (mã, tên, trạng thái), không phân trang.
- **Giữ nguyên:** hai khách hàng khác nhau chỉ ở dấu ("Công ty A" và "Cong ty A") vẫn được phép vì collation `Vietnamese_100_CI_AS` phân biệt dấu.
- **Cần sửa trong tài liệu:** Report-3 UC09, UC14 (quy tắc nhập).
- **Trạng thái:** Mở

### DEV-ALL-31 · NVL của đơn: giới hạn nhập, chặn sửa khi đơn đã đóng

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC09 (phần NVL), UC10 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `POST / PUT / DELETE admin|manager/order-materials`; không đổi schema.
- **Tài liệu gốc ghi:** NVL của đơn chỉ yêu cầu số không âm và có số cần hoặc số khách cấp; thêm NVL cho đơn đã đóng bị chặn, sửa và xóa thì không nêu.
- **Lý do:** yêu cầu của người dùng (kiểm tra validate và giá trị biên NVL của đơn).
- **Code đang làm:**
  - **Số cần, số khách cấp:** không âm, tối đa 100.000.000, tối đa 3 số lẻ (cột `DECIMAL(18,3)`). Trước đây từ 10^15 trở lên gây lỗi hệ thống, quá 3 số lẻ bị làm tròn âm thầm (1,2345 thành 1,235) và 0,0004 lưu thành 0.
  - **Số lần giao dự kiến:** số nguyên từ 1 đến 99 (trước đây backend nhận tới 32767 trong khi giao diện chặn ở 99). Ghi chú tối đa 500 ký tự tính sau khi cắt khoảng trắng. Thông báo lỗi bằng tiếng Việt, mỗi ô một thông báo.
  - **Đơn đã đóng:** sửa và xóa NVL của đơn bị chặn như thêm mới (409 `WAREHOUSE_ORDER_CLOSED`); trước đây sửa và xóa vẫn được dù đơn đã hủy hoặc đã giao.
  - **Giao diện:** form từng dòng và bảng sửa cả đơn dùng chung bộ kiểm tra (`utils/orderMaterialRules.ts`); ô số bỏ `min / max / precision` để báo lỗi rõ thay vì tự đổi giá trị.
- **Chưa làm:** giảm số cần hoặc số khách cấp xuống thấp hơn số đã nhận vẫn được lưu; chưa chốt có chặn hay không.
- **Cần sửa trong tài liệu:** Report-3 UC09.
- **Trạng thái:** Mở

### DEV-ALL-32 · Lệnh cắt thường: kiểm vải theo từng chi tiết, cắt lẻ thêm thành phần

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC10, UC19 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** bỏ `ProductVariant.LengthCm, WidthCm, CK_PV_Size`; `GET .../products/{id}/spec` và `PUT` bỏ hai trường này ở màu – size; `POST / PUT manager/work-orders` nhận `productComponentMaterialId` theo dòng; `GET .../work-orders/{id}` thêm `fabricChecks`; endpoint mới `POST manager/work-orders/fabric-check`.
- **Tài liệu gốc ghi:** DEV-ALL-16: lệnh cắt thường không kiểm đủ vải khi lập, màu – size có kích thước thành phẩm; dòng lệnh chọn chi tiết chỉ dùng cho lệnh cắt bù.
- **Lý do:** yêu cầu của người dùng.
- **Code đang làm:**
  - **Tính vải:** cùng công thức cắt bù (`PieceFabricEstimator`, `vw_RecutFabricNeed`): mỗi chi tiết là hình chữ nhật dài × rộng trong định mức; số miếng một hàng = làm tròn xuống (khổ vải ÷ rộng); số hàng = làm tròn lên (số miếng ÷ số miếng một hàng); số mét = số hàng × dài. Cả bộ: số miếng = số bộ × `PiecesPerGarment`; cắt lẻ: số miếng do người lập nhập. Tính riêng từng NVL vải, từng màu – size, từng thành phần vải đang dùng. Không tính hao phí, không lồng chi tiết nên thường dư.
  - **Cảnh báo:** so với kho khả dụng + vải tổ cắt đang giữ của đơn. Hiện ở form lập lệnh và chi tiết lệnh đang mở, không chặn lưu, không gửi thông báo: không đủ vải (nêu số mét thiếu), chi tiết rộng hơn khổ vải (nêu chi tiết), chưa tính được (thiếu kích thước chi tiết, thiếu khổ vải, NVL không tính theo mét, màu – size chưa có định mức vải). Lệnh cắt bù giữ cách kiểm cũ.
  - **Cắt lẻ thêm thành phần:** lệnh cắt thường có thêm dòng màu – size × chi tiết vải × số chi tiết, cùng một dòng đơn vừa cắt cả bộ vừa cắt lẻ được. Dòng cắt lẻ không tính vào trần số đặt; tối đa 1.000.000 chi tiết mỗi dòng.
- **Chưa xử lý:** kho tính tổng mét theo NVL, không theo từng cuộn, nên nhiều đầu vải ngắn cộng lại vẫn được coi là đủ.
- **Database:** `SQL/NX-MTD_Database.sql`; phần 7 của patch `SQL/patches/2026-11-01_all_changes.sql` cho database đã dựng (chạy lại không lỗi).
- **Cần sửa trong tài liệu:** Report-3 UC10, UC19; ERD (`ProductVariant`).
- **Trạng thái:** Mở

### DEV-ALL-33 · Kiểm kê kho: giới hạn nhập, form lập phiếu phân trang

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC48, UC49 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `SaveStocktakeDtoValidator`, `StocktakeQueryValidator` (mới), `ReasonDtoValidator`, `StocktakeStatuses.All`, form lập phiếu kiểm kê ở FE; không đổi schema.
- **Tài liệu gốc ghi:** số đếm không âm, để trống = khớp sổ; không nêu giới hạn trên hay số lẻ.
- **Lý do:** yêu cầu của người dùng (kiểm tra validate và giá trị biên). Số đếm 1e15 làm database báo tràn số (lỗi hệ thống 500), 0,0001 bị làm tròn thành 0 âm thầm, kho BTP / thành phẩm nhận được 5,5 cái. Form chỉ nạp 100 dòng tồn đầu tiên nên kho nhiều hơn 100 hàng hóa không kiểm kê được phần còn lại.
- **Code đang làm:**
  - **Số đếm:** từ 0 đến 100.000.000 (khớp số lượng NVL của đơn); kho NVL tối đa 3 số lẻ, kho BTP / thành phẩm phải là số nguyên. Ô nhập ở FE chặn cùng giới hạn.
  - **Chữ:** phạm vi và ghi chú dòng tối đa 300 ký tự, lý do trả lại tối đa 500, tính sau khi cắt khoảng trắng, thông báo tiếng Việt. Mã NVL, dòng đơn, chi tiết phải lớn hơn 0.
  - **Danh sách phiếu:** trạng thái và loại kho lọc phải hợp lệ; số trang, số dòng mỗi trang, từ khóa dùng luật chung.
  - **Form lập phiếu:** danh sách tồn phân trang phía server, có ô tìm kiếm; số đã nhập giữ nguyên khi chuyển trang hoặc tìm kiếm. Không nhập gì thì báo ngay thay vì chờ máy chủ; tối đa 1.000 dòng mỗi phiếu. Hàng hóa tồn sổ 0 nhưng còn hàng giữ cho đơn cũng hiện để đếm.
- **Chưa xử lý:** hàng hóa chưa từng có tồn (số sổ 0, chưa có giao dịch) chưa chọn được ở form dù máy chủ nhận.
- **Màn duyệt:** phiếu chờ duyệt có thêm cột "Tồn hiện tại" và cảnh báo khi tồn đã đổi so với số sổ lúc lập. Duyệt vẫn điều chỉnh theo (số đếm − số sổ lúc lập) để không mất các lần nhập xuất xảy ra trong lúc chờ duyệt; tồn không đủ để trừ thì báo lỗi và phiếu giữ nguyên.
- **Cần sửa trong tài liệu:** Report-3 UC48, UC49 (quy tắc nhập).
- **Trạng thái:** Mở

### DEV-ALL-34 · Admin có toàn bộ chức năng của Quản đốc

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · ma trận vai trò, UC19, UC21, UC22 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `ManagerBaseController`, `ProductionGuard.IsManager`, `PurchaseOrderFlowService`, `RequestAccess`, menu, route và nút ở FE; không đổi schema.
- **Tài liệu gốc ghi:** `NX-MTD_AI_Context.md` mục 3 tách việc của `ADMIN` và `MANAGER`; MI-17 / UC21 ghi Admin chỉ xem KPI.
- **Lý do:** yêu cầu của người dùng.
- **Code đang làm:**
  - **API:** mọi API `api/v1/manager/...` mở cho cả Admin. Chức năng chỉ có ở API Quản đốc thì Admin gọi chung API đó (`useManagerRolePath` ở FE).
  - **Chức năng Admin có thêm:** lệnh cắt, phiếu nhập NVL, thiếu NVL khi nhập, đơn mua, phiếu xuất hàng, lập và sửa KPI, sản lượng, hàng hỏng, đợt QC (gồm sửa quá hạn UC22), xử lý các loại đơn của Quản đốc (bổ sung NVL, NVL lỗi, cắt bù, thiếu hàng khi nhập).
  - **KPI:** bỏ màn "KPI tổ may (xem)" ở `/admin/kpi-targets` và `Admin/KpiTargetsController`; Admin dùng chung màn `/kpi/targets`.
- **Giữ nguyên:**
  - Quản đốc không có thêm quyền của Admin. Admin vẫn dùng màn và API nhân viên, nhà cung cấp riêng, rộng hơn của Quản đốc.
  - Admin vẫn không có trang "Ngày công của tôi", vì Giám đốc không chấm công theo ngày.
  - Thông báo gửi cho Quản đốc không gửi thêm cho Admin, trừ các thông báo vốn đã gửi cả hai. Admin xem việc cần làm ở các màn tương ứng.
- **Cần sửa trong tài liệu:** `NX-MTD_AI_Context.md` mục 3, Report-3 ma trận phân quyền, UC21.
- **Trạng thái:** Mở

### DEV-ALL-35 · Lệnh cắt: giá trị biên

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC19 (tổ cắt) · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `SaveWorkOrderDtoValidator`, `CheckWorkOrderFabricDtoValidator`, `CreateReplacementWorkOrderDtoValidator`, `FinishCuttingDtoValidator`, `WorkOrderQueryValidator` (mới), `WorkOrderService.FinishCuttingAsync`, form lập lệnh cắt ở FE; không đổi schema.
- **Tài liệu gốc ghi:** không nêu giới hạn độ dài ghi chú hay lý do cắt thiếu.
- **Lý do:** yêu cầu của người dùng (kiểm tra validate và giá trị biên). Báo cắt xong nối "Cắt thiếu: <lý do>" vào ghi chú của lệnh (cột 1.000 ký tự) mà lý do không có giới hạn: ghi chú 900 ký tự cộng lý do 200 ký tự làm database báo cắt chuỗi (lỗi hệ thống 500) đúng lúc tổ cắt báo xong. Lệnh cắt bù, báo cắt xong và bộ lọc danh sách không có validator nào.
- **Code đang làm:**
  - **Ghi chú tối đa 600, lý do cắt thiếu tối đa 300:** cộng lại luôn vừa cột. Ghi chú cũ dài hơn 600 vẫn đọc được; nếu cộng lý do vào mà vượt 1.000 thì báo lỗi rõ thay vì lỗi hệ thống.
  - **Số dòng:** tối đa 1.000 dòng mỗi lệnh (200 màu – size cộng các dòng cắt lẻ); trước đây lưu lệnh không giới hạn, còn kiểm vải giới hạn 200 nên lệnh có nhiều dòng cắt lẻ không kiểm vải được.
  - **Thêm validator:** lệnh cắt bù (đơn cắt bù, tổ, hạn, ghi chú), báo cắt xong (lý do), bộ lọc danh sách (trạng thái, đơn hàng, số trang, từ khóa). Hạn cắt để trống báo bằng tiếng Việt.
  - **Form:** báo ngay khi chưa nhập dòng nào hoặc khai trùng chi tiết cắt lẻ; ghi chú có đếm ký tự. Sửa lệnh cũ mà giữ nguyên hạn đã qua thì lưu được, khớp với BE (trước đây form chặn dù BE cho qua).
  - **Giờ hoàn thành:** dùng đồng hồ hệ thống (IClock) thay vì giờ máy.
- **Cần sửa trong tài liệu:** Report-3 UC19 (quy tắc nhập).
- **Trạng thái:** Mở

### DEV-ALL-36 · Sửa sản lượng sau hạn: giá trị biên

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC22 (KI-11), UC55 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `SaveOutputDtoValidator`, `OutputLimits` (mới), `OutputService.ValidateRows`, lưới sản lượng ở FE; không đổi schema.
- **Tài liệu gốc ghi:** số lượng là số nguyên không âm; sau hạn chỉ Quản đốc sửa và bắt buộc lý do (MSG20).
- **Lý do:** yêu cầu của người dùng (kiểm tra validate và giá trị biên). Số lượng không có giới hạn trên: khi so với trần, phép cộng số đã nhập với số lượng mới tràn số int nên một dòng 2.147.483.647 có thể lọt qua kiểm tra trần và chỉ bị trigger của database chặn bằng lỗi hệ thống. Lý do dài quá 500 ký tự (kể cả khoảng trắng đầu cuối) và số dòng mỗi lần lưu không bị chặn bằng thông báo rõ ràng.
- **Code đang làm:**
  - **Số lượng:** từ 0 đến 1.000.000 mỗi dòng (0 = xóa dòng đã nhập); ô nhập ở FE chặn cùng giới hạn. Vẫn phải nằm trong trần số lần được giao (MSG16).
  - **Lý do:** tối đa 500 ký tự tính sau khi cắt khoảng trắng, thông báo tiếng Việt; vẫn bắt buộc khi sửa sau hạn (kiểm ở service).
  - **Số dòng:** tối đa 2.000 dòng mỗi lần lưu. Ngày làm việc để trống, mã dòng đơn, tổ, công đoạn, công nhân không hợp lệ đều báo bằng tiếng Việt.
  - **Ngày:** ô chọn ngày ở FE không cho chọn ngày chưa tới (BE vẫn chặn như cũ).
- **Chưa xử lý:** đợt QC (`UpdateQcBatchDtoValidator`, `CreateQcBatchDtoValidator`) và phiếu hàng hỏng (`CreateDefectDtoValidator`) cùng nhóm UC22 vẫn chưa có giới hạn trên cho số lượng và còn thông báo lý do bằng tiếng Anh.
- **Cần sửa trong tài liệu:** Report-3 UC22, UC55 (quy tắc nhập).
- **Trạng thái:** Mở

### DEV-ALL-37 · Hàng hỏng của các tổ: chấp nhận hoặc hủy phiếu sau phản đối, tách bộ lọc, giá trị biên

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC56 (KI-08), UC22 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** ràng buộc `CK_DR_Status` của `DefectRecord` (thêm giá trị `CONFIRMED`, không thêm cột), `DefectService`, `Manager/DefectRecordsController`, `DefectQuery`, `DefectDto` (thêm `UpdatedAt`), các validator hàng hỏng, màn Hàng hỏng của các tổ và Hàng hỏng của tôi ở FE.
- **Tài liệu gốc ghi:** phiếu hàng hỏng chỉ có ghi, hủy và công nhân phản hồi; không nêu ai quyết định khi công nhân phản đối.
- **Lý do:** yêu cầu của người dùng. Công nhân phản đối nhưng Quản đốc chỉ có cách hủy phiếu, không có cách giữ phiếu; bộ lọc và bảng khó dùng (15 cột, cuộn ngang); một số giá trị nhập chưa có giới hạn.
- **Code đang làm:**
  - **Quyết định sau phản đối (Quản đốc, Admin):** chỉ có hai kết quả và kết quả nằm ở cột `Status`: chấp nhận phiếu → `CONFIRMED` (phiếu được giữ, công nhân không phản đối lại được, không hủy được nữa), hoặc hủy phiếu → `CANCELLED` (chức năng hủy có sẵn, hủy sau hạn vẫn bắt buộc lý do). Chấp nhận chỉ áp dụng cho phiếu đã bị công nhân phản đối; ghi chú không bắt buộc. Công nhân và tổ trưởng, tổ phó được báo khi phiếu được chấp nhận.
  - **Giờ và người thao tác:** giờ chấp nhận hoặc hủy chỉ nằm ở `UpdatedAt` (cả hai thao tác đều cập nhật cột này, màn hình hiện ở dòng mở rộng); người thao tác và ghi chú nằm ở nhật ký thay đổi (AuditLog). Không có cột người / giờ / ghi chú riêng.
  - **Database:** không thêm cột; `CK_DR_Status` thêm `CONFIRMED`. Patch `SQL/patches/2026-11-01_all_changes.sql` (có dọn 7 cột của bản đầu đã chạy trên máy dev, nên chạy lại không lỗi); `SQL/NX-MTD_Database.sql` đã cập nhật. Chấp nhận phiếu thuộc tháng đã khóa thì báo lỗi rõ (database chặn sửa phiếu của tháng đã khóa).
  - **Bộ lọc tách riêng:** tìm kiếm, khoảng ngày (tối đa 366 ngày), tổ (chỉ Quản đốc), loại hỏng, tình trạng phiếu (gồm Đã chấp nhận), phản đối (không phản đối, chờ quyết định, đã quyết định); lọc phía máy chủ. Tổ trưởng không mở rộng được sang tổ khác bằng bộ lọc tổ.
  - **Bảng gọn:** từ 15 cột còn 7 (phiếu, đơn / mã hàng, công đoạn / công nhân, hỏng, lý do, tình trạng, thao tác); thông tin phụ ở dòng mở rộng.
  - **Giá trị biên:** số lượng 1 đến 1.000.000; ghi chú, lý do hủy, phản hồi, ghi chú khi chấp nhận tối đa 500 ký tự sau khi cắt khoảng trắng; lý do Khác bắt buộc ghi chú; ca, loại, lý do, ngày làm việc kiểm bằng thông báo tiếng Việt; thiếu phiên bản dữ liệu báo rõ.
  - **Hạn phản đối 48 giờ:** công nhân chỉ phản đối được trong 48 giờ kể từ lúc phiếu được ghi (`RecordedAt`; đúng 48 giờ vẫn được), quá hạn thì backend từ chối với thông báo tiếng Việt và màn Hàng hỏng của tôi ẩn nút phản đối. Phiếu quá 48 giờ không bị phản đối thì job nền `DefectAutoConfirmJob` (15 phút quét một lần, `DefectAutoConfirmService`) tự chuyển `RECORDED` sang `CONFIRMED`, giờ chuyển nằm ở `UpdatedAt`; phiếu đã bị phản đối vẫn chờ Quản đốc quyết định. Hệ quả: phiếu đã tự chốt không hủy được nữa (hủy chỉ áp dụng cho phiếu `RECORDED`).
  - **Bỏ trạng thái "Đã khóa" ở ứng dụng:** ứng dụng chưa có chỗ nào chuyển phiếu sang `LOCKED` (khi duyệt bảng lương chỉ kỳ kế toán bị khóa, phiếu giữ nguyên trạng thái, việc chặn sửa do trigger `trg_DefectRecord_PeriodLock` lo). Vì vậy `LOCKED` bị bỏ khỏi `DefectStatuses.All` (lọc `status=Locked` trả 400), ô lọc, nhãn và kiểu `DefectStatus` ở giao diện. Database giữ nguyên `LOCKED` trong `CK_DR_Status` và trigger; hằng `DefectStatuses.Locked` còn lại cho test điểm hạng.
  - **Dữ liệu demo:** `SQL/NX-MTD_Database.sql` mục 11 có đủ các tình huống (đã hủy, tự chốt, chờ quyết định, đã chấp nhận sau phản đối, phiếu mới còn trong hạn phản đối).
- **Giữ nguyên:** việc ghi phiếu vẫn chỉ do tổ trưởng, tổ phó tổ may; hủy phiếu vẫn theo hạn sửa (Quản đốc sau hạn phải nhập lý do).
- **Chưa xử lý:** không còn (giới hạn của đợt QC xử lý ở DEV-ALL-38).
- **Cần sửa trong tài liệu:** Report-3 UC56, UC22; ERD (`DefectRecord.Status`).
- **Trạng thái:** Mở

### DEV-ALL-38 · Sửa số hàng đạt QC: giới hạn nhập, chặn giảm dưới số đã bàn giao
- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC57, UC22 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `QcBatchService` (sửa, xóa), `CreateQcBatchDtoValidator`, `UpdateQcBatchDtoValidator`, `DeleteQcBatchDtoValidator`, mã lỗi `QC_BELOW_HANDED_OVER`, hai form thêm và sửa đợt QC ở FE.
- **Tài liệu gốc ghi:** số đạt không vượt hàng đang có trên chuyền (MSG19); hạn sửa hết ca, sau hạn chỉ Quản đốc sửa kèm lý do (MSG20); không nêu giới hạn trên của số nhập và không nêu quan hệ với số đã bàn giao kho thành phẩm.
- **Lý do:** yêu cầu của người dùng (kiểm tra validate, giá trị biên). Số QC đạt là nguồn của "còn bàn giao được" (QC đạt − đã bàn giao), nên sửa giảm hoặc xóa đợt có thể làm số này âm; số nhập cũng chưa có trần.
- **Code đang làm:**
  - **Chặn giảm dưới số đã bàn giao:** sau khi sửa hoặc xóa, tổng QC đạt của dòng đơn (mọi tổ) không được thấp hơn tổng số đã bàn giao cho kho thành phẩm (phiếu chờ nhận và đã nhận; phiếu bị từ chối hoặc đã hủy không tính). Đúng bằng thì được. Thông báo nêu số QC còn lại và số đã bàn giao.
  - **Giá trị biên:** số hàng đạt 1 đến 1.000.000 (thêm và sửa); lý do tối đa 500 ký tự sau khi cắt khoảng trắng; thiếu phiên bản dữ liệu báo rõ; thông báo "không âm" sai cho giá trị 0 đổi thành "lớn hơn 0".
- **Giữ nguyên:** sửa hoặc xóa đợt QC của đơn đã đóng vẫn được phép (chủ ý: Quản đốc cần chỉnh số liệu sau khi đóng đơn; có kỳ lương khóa, lý do bắt buộc sau hạn và nhật ký kiểm soát). Thêm mới cho đơn đã đóng vẫn bị chặn.
- **Chưa xử lý:** không.
- **Cần sửa trong tài liệu:** Report-3 UC57, UC22.
- **Trạng thái:** Mở

### DEV-ALL-39 · Chỉ tiêu KPI tổ may: giới hạn kỳ và số lượng, chặn sửa dưới số đã làm
- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · KPI tổ may · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `KpiService.UpdateAsync`, `SaveKpiTargetDtoValidator`, `SaveKpiDailyPlanDtoValidator`, `KpiTargetQueryValidator`, `KpiLimits`, form lập chỉ tiêu và lịch KPI ở FE (`KpiTargetFormModal`, `KpiCalendarBody`, `useKpiDailyPlan`, `periodRule`).
- **Tài liệu gốc ghi:** chỉ tiêu KPI theo kỳ, số lượng lớn hơn 0; không nêu độ dài tối đa của kỳ, trần số lượng, hay quan hệ giữa chỉ tiêu sửa lại và số tổ đã làm.
- **Lý do:** yêu cầu của người dùng (kiểm tra validate, giá trị biên). Kỳ không giới hạn làm vòng lặp theo ngày chạy rất lâu và tràn cột `WorkingDays` (SMALLINT); số KPI từng ngày không có trần làm phép cộng tổng tràn int (lỗi 500); sửa chỉ tiêu thấp hơn số đã làm cho % đạt vô lý (vd. 500%).
- **Code đang làm:**
  - **Kỳ:** tối đa 366 ngày (gồm hai đầu), FE báo lỗi tại ô chọn kỳ.
  - **Số lượng:** chỉ tiêu cả kỳ và KPI từng ngày từ 1 đến 1.000.000 (bằng trần số lượng đơn); một lần lưu KPI ngày tối đa 366 dòng; dòng rỗng báo lỗi thay vì ném lỗi; ô nhập FE có trần tương ứng.
  - **Bộ lọc danh sách:** trạng thái chỉ nhận Active, Closed, Cancelled; từ khóa tối đa 100 ký tự sau khi cắt khoảng trắng.
  - **Chặn sửa dưới số đã làm:** khi sửa, chỉ tiêu cả kỳ không được nhỏ hơn tổng hàng hoàn thiện của tổ trong kỳ (đúng bằng thì được), thông báo nêu số đã làm. Lập mới không áp dụng.
- **Giữ nguyên:** các kiểm tra nghiệp vụ có sẵn (không trùng ngày giữa các kỳ, tổng không vượt số lượng đơn, kỳ không ở quá khứ, đơn đã đóng, khóa KPI ngày đã qua).
- **Chưa xử lý:** ô nhập FE chưa tự đặt mức tối thiểu bằng số đã làm (chỉ báo lỗi từ BE khi lưu).
- **Cần sửa trong tài liệu:** Report-3 phần KPI tổ may.
- **Trạng thái:** Mở

### DEV-ALL-40 · Đơn mua NVL: tạo là đặt hàng luôn, NVL lỗi thiếu tồn tạo đơn từ Mua NVL
- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC09, UC23 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `PurchaseOrderService.CreateAsync`, `CreatePurchaseOrderDto` và validator, `PlaceOrderModal`, `PurchaseOrderFormModal`, `DefectiveMaterialRequestHandler`, `RequestTypes` / `RequestTypePolicies` (loại `MATERIAL_PURCHASE`), `RequestRepository.GetMaxCodeAsync`, ràng buộc `CK_Request_Type`.
- **Tài liệu gốc ghi:** Quản đốc tạo đơn mua ở trạng thái chờ chốt rồi mở chi tiết để đặt hàng; duyệt đơn NVL lỗi thiếu tồn thì hệ thống tự tạo đơn mua chờ chốt.
- **Lý do:** yêu cầu của người dùng. Quản đốc phải nhập nhà cung cấp, số lượng hai lần (lúc tạo và lúc đặt hàng); đơn mua tự tạo thiếu nhà cung cấp và đơn giá nên không có giá trị, Quản đốc muốn tự tạo đơn mua.
- **Code đang làm:**
  - **Tạo là đặt hàng:** form tạo thêm ngày đặt (không sau hôm nay) và số lần giao dự kiến (1 đến 99), đơn giá lớn hơn 0; đơn vào thẳng trạng thái đã đặt, ghi nhật ký và báo Kế toán, Thủ kho như bước Đặt hàng cũ. Form Đặt hàng (cho đơn chờ chốt có sẵn) điền sẵn nhà cung cấp của đơn.
  - **NVL lỗi thiếu tồn:** không tự tạo đơn mua. Phần đủ tồn vẫn tạo đơn Xin NVL cho Thủ kho; phần thiếu tạo đơn từ loại `MATERIAL_PURCHASE` ("Mua NVL", hệ thống tạo, Quản đốc xử lý, không cần duyệt) kèm NVL và số thiếu để Quản đốc tự tạo đơn mua.
  - **Database:** `CK_Request_Type` thêm `MATERIAL_PURCHASE` (patch `SQL/patches/2026-11-01_all_changes.sql` phần 9, script chính và dbml đã cập nhật).
  - **Sửa lỗi kèm theo:** một lần duyệt tạo hai đơn từ hệ thống thì trùng mã đơn (409); hàm sinh mã nay tính cả đơn vừa thêm chưa lưu.
- **Giữ nguyên:** đơn chờ chốt đã có trong database vẫn đặt hàng và hủy được ở form cũ.
- **Chưa xử lý:** đơn tạo tay không còn trạng thái chờ chốt nên không có bước hủy trước khi đặt; đơn Mua NVL chưa liên kết với đơn mua Quản đốc tạo ra nên phải bấm Hoàn thành thủ công.
- **Cần sửa trong tài liệu:** Report-3 UC09, UC23; ERD (`Request.RequestTypeCode`).
- **Trạng thái:** Mở

### DEV-ALL-41 · Phiếu xuất hàng: giá trị biên, phiên bản phiếu; báo lỗi tải ảnh quá lớn

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC45–UC47 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `DispatchNoteService`, `DispatchNoteDtoValidators`, ba `DispatchNotesController`, `DispatchNoteDto` (thêm `RowVer`), màn phiếu xuất ở FE; `ApiServiceExtensions`, `ExceptionHandlingMiddleware`; `imageFile`, `useRequestForm`, `useReceiptForm` ở FE.
- **Tài liệu gốc ghi:** không nêu giới hạn ngày giao, ngày khách nhận, số lượng; không nêu xuất kho khi đơn đã đóng.
- **Lý do:** yêu cầu của người dùng (kiểm tra validate, giá trị biên; kiểm tra các chỗ tải ảnh).
- **Code đang làm:**
  - **Ngày:** ngày giao từ hôm nay đến 366 ngày sau; ngày khách nhận không sau hôm nay.
  - **Trạng thái đơn:** Thủ kho không xuất được phiếu của đơn đã hủy, dừng hoặc hoàn tất sau khi phiếu được lập.
  - **Giá trị biên:** số xuất và số khách nhận tối đa 1.000.000 mỗi dòng, tối đa 2.000 dòng; địa chỉ 500, ghi chú 1.000 ký tự sau khi cắt khoảng trắng; có validator cho bộ lọc danh sách.
  - **Phiên bản phiếu:** hủy, xuất kho, từ chối, xác nhận khách nhận gửi kèm `rowVer` của phiếu đang xem; lệch thì 409 `COMMON_CONCURRENCY_CONFLICT`. Hai API hủy và xuất kho trước đây không có body nay bắt buộc body `{ rowVer }`.
  - **Tải ảnh:** tệp vượt giới hạn dung lượng trước đây trả 500, nay trả 413 với câu báo dung lượng ảnh; ảnh mã hàng ở FE chỉ nhận JPG, PNG, WebP ngay lúc chọn; cảnh báo ảnh chưa tải được kèm lý do từ máy chủ.
- **Giữ nguyên:** số xuất không vượt số còn phải giao và thành phẩm khả dụng; ảnh phiếu có ký là bắt buộc trước khi xuất.
- **Chưa xử lý:** ảnh HEIC từ iPhone bị từ chối, chưa tự chuyển đổi định dạng.
- **Cần sửa trong tài liệu:** Report-3 UC45, UC46, UC47.
- **Trạng thái:** Mở

### DEV-ALL-42 · Lệnh cắt cả bộ tính số mét vải theo bìa của mã hàng

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC10, UC19 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** bảng mới `ProductMarker`, `ProductMarkerFabric` (patch `SQL/patches/2026-11-02_all_changes.sql`, `SQL/NX-MTD_Database.sql` đã cập nhật); `GET`, `PUT .../products/{id}/spec` thêm `markers`, `markerFabrics` (`PUT` bắt buộc gửi hai danh sách); `WorkOrderFabricChecker`, `ProductSpecService`; tab "Bìa" ở màn định mức.
- **Tài liệu gốc ghi:** DEV-ALL-32: lệnh cắt thường tính vải theo kích thước từng chi tiết trong `ProductComponentMaterial`.
- **Lý do:** yêu cầu của người dùng. Xưởng có sẵn bìa theo mã hàng, nhập kích thước từng chi tiết cho từng màu × size tốn công và kém chính xác hơn bìa.
- **Code đang làm:**
  - **Lưu dữ liệu:** `ProductMarker`: mỗi dòng là một bìa (`MarkerNo`) ở một size, có dài, rộng; một bìa tính cho một sản phẩm. `ProductMarkerFabric`: bìa nào, màu áo nào, dùng vải nào; vải không đổi theo size. Bìa không gắn với chi tiết, nên một chi tiết dùng nhiều vải vẫn khai được; mỗi vải dùng trên sản phẩm cần một bìa.
  - **Tính số mét cả bộ:** với mỗi dòng lệnh, tra các bìa có vải cho màu áo đó; gộp số sản phẩm theo (vải, bìa, size); số bìa ngang khổ = khổ vải ÷ rộng bìa, làm tròn xuống; số hàng = số sản phẩm ÷ số bìa ngang khổ, làm tròn lên; số mét = số hàng × dài bìa. Cộng theo vải rồi so với kho khả dụng + vải tổ cắt đang giữ, nêu đúng vải thiếu.
  - **Chưa tính được:** bìa chưa có kích thước của size, vải thiếu khổ hoặc không tính theo mét, hoặc vải có trong định mức chi tiết cắt của màu đó mà chưa nằm trên bìa nào (báo để không bị bỏ sót vải thiếu).
  - **Quyền:** Kế toán và Admin sửa được bìa, cùng quyền với định mức; vai trò khác bị từ chối (403).
  - **Kiểm tra khi lưu:** không trùng; size và màu phải có trong màu – size của mã hàng; mỗi số bìa có đủ kích thước và vải; vải phải là NVL loại vải đang dùng và có trong định mức chi tiết cắt của màu đó. Vì `MarkerNo` không có FK nên backend kiểm việc khớp số bìa giữa hai bảng.
- **Giữ nguyên:** vẫn chỉ cảnh báo, không chặn lập lệnh, không lưu kết quả kiểm vải. Cắt lẻ và lệnh cắt bù vẫn tính theo kích thước chi tiết. `ProductComponentMaterial` không đổi.
- **Chưa xử lý:** mã hàng cũ chưa có bìa báo "chưa tính được" cho đến khi nhập bìa. Số mét là ước tính theo hình chữ nhật của bìa, có thể dư so với sơ đồ giác thực tế. Kho tính tổng mét, không theo từng cuộn.
- **Cần sửa trong tài liệu:** Report-3 UC10, UC19; ERD (thêm `ProductMarker`, `ProductMarkerFabric`); `NX-MTD_AI_Context.md` mục 4.2.
- **Trạng thái:** Mở

### DEV-ALL-43 · Bàn cắt: chiều dài bàn cắt, vải đã cắt và vải còn lại

- **Ngày / Người / Task / PR:** 01/11/2026 · Claude (AI) · UC51 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** cột mới `CuttingLay.LayLengthM` (patch `SQL/patches/2026-11-02_all_changes.sql`, `SQL/NX-MTD_Database.sql` đã cập nhật); `POST`, `PUT` bàn cắt bắt buộc thêm `layLengthM`; `CuttingLayDto` thêm `layLengthM`, `fabricUsedM`; `FabricRequirementDto` thêm `heldQty`, `cutQty`, `remainingQty`, `unmeasuredLayCount`; form, chi tiết và danh sách bàn cắt ở FE.
- **Tài liệu gốc ghi:** bàn cắt chỉ có số lớp và tỉ lệ size, không lưu số mét vải. Vải thực dùng chỉ suy ra khi kết thúc lệnh (đã nhận − đã trả).
- **Lý do:** yêu cầu của người dùng: biết vải đã cắt và vải còn lại ngay khi nhập sản lượng bàn cắt.
- **Code đang làm:**
  - **Vải đã cắt của bàn** = số lớp × chiều dài bàn cắt; tính, không lưu. Vải đã cắt và vải còn lại chỉ dựa vào chiều dài bàn cắt do tổ cắt nhập, không dùng bìa của mã hàng (bìa ở DEV-ALL-42 chỉ để ước tính khi lập lệnh).
  - **Vải còn lại** = vải tổ đang giữ của đơn (đã nhận − đã trả về kho) − tổng vải đã cắt của tổ trên đơn, tính riêng từng vải. Dùng cùng cách tính "tổ đang giữ" với kiểm vải lệnh cắt, nên nhiều lệnh của một đơn dùng chung số dư.
  - **Form nhập bàn:** hiện đang giữ, đã cắt, còn lại, bàn này dùng, sau bàn này còn; tính lại ngay khi đổi vải, số lớp, chiều dài. Khi sửa bàn cũ không tính trùng phần bàn đó.
  - **Giá trị biên:** chiều dài bàn cắt bắt buộc, lớn hơn 0, tối đa 1.000 m, tối đa 2 số lẻ.
- **Giữ nguyên:** vượt vải còn lại chỉ cảnh báo, không chặn lưu, vì số liệu phiếu nhận vải có thể chưa cập nhật kịp. Số cắt vẫn theo số lớp × tỉ lệ size.
- **Chưa xử lý:** bàn nhập trước khi có cột để trống chiều dài, không cộng vào số đã cắt (màn hình báo số bàn thiếu); sửa bàn đó thì phải nhập. Vải không tính theo mét không hiện số dư.
- **Cần sửa trong tài liệu:** Report-3 UC51; ERD (`CuttingLay`).
- **Trạng thái:** Mở

### DEV-ALL-44 · Thủ kho xem đơn thiếu NVL đã xử lý

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC08, UC39 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `WarehouseKeeper/ShortDeliveriesController`, `ShortDeliveryQuery`, `ShortDeliveryService`.
- **Tài liệu gốc ghi:** Thủ kho chỉ xem đơn thiếu NVL khi Quản đốc đã chọn giao bù và còn chờ nhận bù.
- **Lý do:** yêu cầu của người dùng. Đơn Quản đốc chốt không bù hoặc đã nhận đủ biến mất khỏi màn của Thủ kho.
- **Code đang làm:** Thủ kho thấy mọi đơn Quản đốc đã xử lý (giao bù hoặc chốt không bù), kể cả đơn đã hoàn thành; đơn chưa xử lý vẫn không hiện. Lọc "Đang chờ nhận hàng bù" vẫn dùng được. Thủ kho chỉ lập phiếu bổ sung ở đơn đang chờ hàng bù.
- **Cần sửa trong tài liệu:** Report-3 UC39.
- **Trạng thái:** Mở

### DEV-ALL-45 · Chốt không bù NVL khách cấp: phần thiếu chuyển thành số phải mua

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC08, UC39 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `ShortDeliveryService`; `OrderMaterial.CustomerQty`, `ExpectedDeliveryCount`.
- **Tài liệu gốc ghi:** AI Context 4.1.4: thiếu NVL thì xin khách trước, khách không cấp được mới mua. DEV-ALL-21: chốt không bù phiếu khách cấp chỉ chặn lập thêm phiếu, không đổi số khách cấp, nên số phải mua không tăng.
- **Lý do:** yêu cầu của người dùng. Trước đây khách hứa 100, giao 70, chốt không bù thì số phải mua vẫn bằng 0, không mua được 30 còn thiếu.
- **Code đang làm:** khi Quản đốc chốt không bù đơn thiếu của phiếu khách cấp, `CustomerQty` giảm xuống bằng số đã nhận thật (phiếu đã xác nhận), `PurchaseQty` là cột tính nên tự tăng phần thiếu. Khách chưa giao gì thì `ExpectedDeliveryCount` về NULL theo `CK_OM_Delivery`. Ghi AuditLog giá trị cũ, mới, lý do trong cùng giao dịch. Đơn mua NCC không đổi.
- **Giữ nguyên:** hệ thống không tự tạo đơn mua hay đơn từ; Quản đốc tự tạo đơn mua khi cần. Chốt không bù vẫn chặn lập thêm phiếu khách cấp cho NVL đó.
- **Chưa xử lý:** đơn thiếu đã chốt không bù trước thay đổi này không được sửa lại số khách cấp; Quản đốc chỉnh tay ở NVL của đơn nếu cần. Phiếu khách cấp còn chờ xác nhận lúc chốt không được tính vào số đã nhận.
- **Cần sửa trong tài liệu:** Report-3 UC39; `NX-MTD_AI_Context.md` mục 4.1.
- **Trạng thái:** Mở

### DEV-ALL-46 · Vải cố định đơn vị mét, khổ vải tính bằng mét

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC24 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `MaterialService`, validator NVL, `WorkOrderFabricChecker`, form NVL ở FE. Không đổi schema.
- **Tài liệu gốc ghi:** `Material.Unit` là chữ tự do; khổ vải nhập bằng cm.
- **Lý do:** yêu cầu của người dùng. Vải khác mét không tính được vải cần, khổ vải quen dùng đơn vị mét.
- **Code đang làm:** NVL loại vải luôn lưu `Unit = m`, backend bỏ qua đơn vị gửi lên, form khóa ô đơn vị. Khổ vải trên form nhập bằng mét (tối đa 2 số lẻ), đổi sang cm khi gửi; API và cột `WidthCm` vẫn là cm vì bìa và chi tiết tính bằng cm. Cảnh báo "rộng hơn khổ vải" báo khổ bằng mét.
- **Giữ nguyên:** phụ liệu nhập đơn vị tự do. Bìa, chi tiết vẫn nhập cm. Chiều dài bàn cắt vẫn là mét.
- **Chưa xử lý:** database không có ràng buộc đơn vị của vải; vải cũ khác mét (nếu có) đổi sang mét khi sửa và lưu lại NVL, chưa đổi số lượng tồn tương ứng.
- **Cần sửa trong tài liệu:** Report-3 UC24; ERD (`Material.Unit`).
- **Trạng thái:** Mở

### DEV-ALL-47 · Nút tạo đơn mua NVL chuyển sang màn Phiếu nhập NVL

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC23, UC37 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `OrderMaterialPage`, `MaterialReceiptListPage` ở FE. Không đổi API, schema.
- **Tài liệu gốc ghi:** Quản đốc tạo đơn mua NVL dùng chung ở màn NVL của đơn (TH-12).
- **Lý do:** yêu cầu của người dùng.
- **Code đang làm:** màn NVL của đơn bỏ nút; màn Phiếu nhập NVL có nút "Tạo đơn mua" cho Admin và Quản đốc. Thủ kho không thấy nút, vẫn chỉ lập phiếu nhập. Nút ở màn Đơn mua NVL giữ nguyên.
- **Cần sửa trong tài liệu:** Report-3 UC23 (màn thao tác).
- **Trạng thái:** Mở

### DEV-ALL-48 · Phải phân công đủ công đoạn trước khi nhận BTP

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC52, UC42, GBR-20 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `MaterialIssueService.ConfirmAsync`, mã lỗi `ASSIGNMENT_INCOMPLETE`, màn Phân công công đoạn ở FE. Không đổi schema, API phân công giữ nguyên.
- **Tài liệu gốc ghi:** GBR-20: phân công chỉ là gợi ý nạp lưới sản lượng, không bắt buộc.
- **Lý do:** yêu cầu của người dùng. Phân công là việc làm trước khi nhận BTP, làm cơ sở cho cả đợt làm đơn: công nhân nào làm công đoạn nào.
- **Code đang làm:**
  - **Chặn khi nhận BTP:** tổ may xác nhận nhận phiếu cấp BTP thì mọi công đoạn may đang dùng của các mã hàng trên phiếu phải có ít nhất một công nhân đang làm của tổ; thiếu thì báo 409, nêu số công đoạn thiếu và tối đa 5 mã. Phân công của đơn khác, tổ khác, đã bỏ, hoặc công nhân đã nghỉ, chuyển tổ không tính. Phiếu cấp vải của tổ cắt không kiểm.
  - **Validate của phân công:** chỉ công đoạn tổ may, chỉ công nhân (không phải tổ trưởng, tổ phó) đang làm của tổ; đơn đã đóng thì không bỏ phân công được; mỗi lần giao 1 đến 500 công đoạn, không trùng.
  - **Mã hàng chưa có công đoạn:** mã hàng trên phiếu không có công đoạn may đang dùng thì cũng bị chặn, nêu mã hàng, vì chưa phân công được. Màn phân công báo trước: mã được chia mà chưa có công đoạn vẫn hiện trong ô chọn mã với nhãn "chưa khai công đoạn", kèm cảnh báo nêu mã hàng; mặc định mở mã đã có công đoạn.
  - **Dữ liệu demo:** đơn DEMO-DH003 phân công đủ công đoạn trong `SQL/NX-MTD_Database.sql`; database đã dựng chạy `SQL/patches/2026-11-02_all_changes.sql`.
  - **Màn phân công:** mỗi công đoạn một dòng, chọn nhiều công nhân ngay trên dòng; chọn thêm là phân công, bỏ chọn là đóng phân công (giữ lịch sử). Có thanh tiến độ; tìm theo mã, tên; lọc theo nhóm, công nhân (kèm số công đoạn đang làm), tình trạng. Bảng phân trang; tích chọn nhiều dòng (giữ khi chuyển trang) rồi giao một lần cho một công nhân, thay hộp phân công nhiều công đoạn. Vẫn có chép từ đơn khác. Bỏ hộp "Đổi người" vì chọn và bỏ chọn đã thay thế.
- **Giữ nguyên:** sau khi đã nhận BTP, công nhân làm thay vẫn nhập sản lượng được và gắn nhãn ngoài phân công (phần còn lại của GBR-20). Phiếu đã nhận trước thay đổi này không bị ảnh hưởng. API đổi người ở BE vẫn còn.
- **Chưa xử lý:** phiếu cấp BTP vẫn lập được khi tổ chưa phân công, chỉ chặn lúc tổ nhận. Bỏ phân công sau khi đã nhận BTP không bị chặn, chỉ ảnh hưởng lần nhận sau.
- **Cần sửa trong tài liệu:** Report-3 UC52, UC42, GBR-20.
- **Trạng thái:** Mở

### DEV-ALL-49 · Quản đốc, Admin chia dòng đơn (mã hàng × màu × size) cho tổ may

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC52, UC42 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** bảng mới `OrderLineTeam`, `OrderLineTeamService`, `OperationAssignmentService`, `MaterialIssueService`, API `manager/order-line-teams`, `LookupService`, `OrderLookupService`, `RequestService`, `KpiService`; màn mới "Chia đơn cho tổ may", màn Phân công công đoạn. Patch `SQL/patches/2026-11-02_all_changes.sql`.
- **Tài liệu gốc ghi:** tổ may phân công theo đơn, không có bước chia đơn hoặc mã hàng cho tổ.
- **Lý do:** yêu cầu của người dùng. Một tổ có thể chỉ làm một mã hàng, một màu hoặc một size trong đơn. Tổ cắt chỉ có một tổ nên không chia.
- **Code đang làm:**
  - **Chia:** màn "Chia đơn cho tổ may" cho Admin, Quản đốc liệt kê từng dòng đơn (mã hàng × màu × size) của đơn đang mở, chọn một hoặc nhiều tổ may ngay trên dòng; tích nhiều dòng rồi chia cho thêm tổ một lần (tối đa 500 dòng, tổ đang được chia giữ nguyên). Quan hệ nhiều-nhiều: một dòng có nhiều tổ, một tổ nhận nhiều dòng. Tổ trưởng tổ mới được chia nhận thông báo. Thay đổi ghi nhật ký.
  - **Không bỏ chia được** (409) tổ có phiếu cấp BTP chưa bị từ chối của đúng dòng đó, hoặc dòng đó là dòng cuối của mã hàng mà tổ đang có phân công hiệu lực, chỉ tiêu KPI chưa hủy.
  - **Màn phân công và KPI vẫn theo mã hàng,** vì công đoạn khai theo mã, không theo màu, size: tổ được làm mã hàng khi có ít nhất một dòng của mã đó. Đơn có từ 2 mã được chia thì chọn mã, bảng, bộ lọc và tiến độ theo mã đang chọn. API phân công và chép từ đơn khác cũng chặn mã chưa được chia.
  - **Cấp BTP:** Thủ kho lập phiếu cấp BTP có dòng chưa chia cho tổ nhận thì bị chặn, nêu mã hàng, màu, size.
  - **Ô chọn đơn và dòng đơn:** API tra cứu (`lookups`, `order-lookups`) chỉ trả đơn có dòng được chia và đúng các dòng đó khi người dùng thuộc tổ may, nên áp dụng cho mọi màn của tổ. Quản đốc, Admin, QA/QC, Thủ kho và tổ cắt không bị giới hạn.
  - **Đơn từ:** tổ may tạo, sửa đơn nêu dòng đơn chưa được chia, hoặc nêu đơn mà tổ không có dòng nào, thì bị chặn.
  - **Chỉ tiêu KPI:** ô chọn tổ chỉ liệt kê tổ có ít nhất một dòng của mã hàng; API cũng chặn (409).
  - **Không chặn thêm ở:** giao việc, sản lượng, hàng hỏng, QC, vì các bước này đã giới hạn theo BTP tổ nhận hoặc sản lượng của tổ, mà BTP đã phải được chia.
  - **Dòng bị bỏ khỏi đơn** (chỉ khi đơn còn NEW): việc chia của dòng đó bị xóa theo. Dòng mới thêm vào đơn đã chia thì chưa thuộc tổ nào, phải chia thêm.
  - **Dữ liệu đang có:** patch chia sẵn mọi màu × size của mã hàng cho tổ đang có phân công hiệu lực, phiếu cấp BTP, chỉ tiêu KPI chưa hủy hoặc đơn Xin BTP đang chờ. Đơn khác phải chia trước.
- **Cần sửa trong tài liệu:** Report-3 UC52, UC42; thêm chức năng chia dòng đơn cho tổ may.
- **Trạng thái:** Mở

### DEV-ALL-50 · Admin dùng màn công đoạn, thêm quản lý nhóm công đoạn

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC25, UC26 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `OperationGroupService`, `OperationService`, API `admin/operations`, `admin/operation-groups`, `accountant/operation-groups`, màn Công đoạn và đơn giá ở FE. Không đổi schema.
- **Tài liệu gốc ghi:** UC25: Kế toán lập công đoạn. Nhóm công đoạn chỉ tạo được bằng cách gõ tên khi sửa từng công đoạn, không có chức năng quản lý.
- **Lý do:** yêu cầu của người dùng: chỉnh màn công đoạn và nhóm công đoạn của Admin và Kế toán.
- **Code đang làm:**
  - **Admin:** dùng được menu, thêm, sửa, ngừng dùng, dùng lại, nhập Excel công đoạn và quản lý nhóm. Admin chỉ xem đơn giá, không đặt hay gửi duyệt; nhập Excel của Admin không đọc đơn giá (API bỏ qua ngày hiệu lực). Lý do: Admin là người duyệt đơn giá (UC26), không tự đặt rồi tự duyệt.
  - **Nhóm công đoạn:** thêm, đổi tên, xóa trong một hộp thoại riêng theo mã hàng; tên không trùng trong mã hàng (409). Xóa nhóm đưa công đoạn trong nhóm về chưa có nhóm, vì nhóm chỉ để nhóm và tìm kiếm.
  - **Xếp nhóm hàng loạt:** tích nhiều công đoạn (giữ khi chuyển trang, tối đa 500) rồi xếp vào nhóm hoặc bỏ nhóm. Công đoạn và nhóm phải cùng mã hàng.
  - **Màn hình:** bảng phân trang, lọc theo nhóm kể cả "Chưa có nhóm", đổi mã hàng thì xóa bộ lọc. Gọi thống nhất là "Nhóm công đoạn" thay cho "Công đoạn to".
  - **Validate:** API chặn bậc ngoài 1 đến 9, thời gian chuẩn quá `DECIMAL(9,2)`, tên nhóm quá 200 ký tự, thiếu mã hàng khi lấy danh sách, mã công đoạn có dấu hoặc khoảng trắng, công đoạn cuối của tổ cắt. Đơn giá phải lớn hơn 0 (tài liệu và DB cũ cho phép 0), tối đa 100.000.000 đồng, tối đa 2 chữ số thập phân, bắt buộc ngày hiệu lực. File Excel cũng kiểm giới hạn TGCT và TIỀN theo từng dòng. Patch `SQL/patches/2026-11-02_all_changes.sql` thêm ràng buộc tương ứng ở DB.
  - **Ảnh hưởng tới các bảng khác:** không ngừng dùng được công đoạn còn việc đang chờ xác nhận hoặc chưa làm xong trong đơn đang mở (`WorkAllocation`, `vw_WorkerOutputCap`), vì màn giao việc ẩn công đoạn đã ngừng nên không thu hồi được và sản lượng cũng bị chặn. Công đoạn đã ngừng không sửa, gửi duyệt hay duyệt đơn giá được (`PieceRate`). Mã hàng đã ngừng không thêm, nhập công đoạn được. Excel không đánh dấu công đoạn cuối lên công đoạn đã ngừng.
  - **Gửi duyệt hàng loạt:** Kế toán tích nhiều công đoạn có đơn giá Nháp để gửi duyệt một lần, Admin nhận một thông báo chung.
  - **Bỏ ô tích "Giao việc kèm BTP":** cờ `Operation.UsesSemiFinished` từ 26/10 chỉ để tham khảo, chỉ tạo thẻ chữ "Kèm BTP" ở màn Công đoạn và "Dùng BTP" ở màn Chia việc, không quy tắc nào dùng. Đã bỏ cột, bỏ cột trong `vw_OperationAllocationProgress`, bỏ ở form, API và hai màn hình. Patch `SQL/patches/2026-11-02_all_changes.sql` (dữ liệu cột này bị xóa, trường này cũng bị xóa khỏi JSON của các dòng `AuditLog` của `Operation`, nên các dòng nhật ký cũ mất thông tin về cờ đó); `SQL/NX-MTD_Database.sql` đã sửa theo. API cũ gửi `usesSemiFinished` vẫn được, trường bị bỏ qua.
  - **Công đoạn cuối:** tick "Là công đoạn cuối" ở một công đoạn thì công đoạn cuối đang dùng trước đó tự bỏ tick (có log), nên mã hàng không bao giờ có hai công đoạn cuối đang dùng. Hệ thống lưu bỏ cờ cũ trước rồi mới ghi công đoạn mới, vì chỉ mục `UX_Operation_FinalStep` chặn hai dòng cùng là công đoạn cuối trong một lô lệnh. Ngừng công đoạn cuối hoặc bỏ tick vẫn được, hộp xác nhận cảnh báo mã hàng sẽ không còn công đoạn cuối. Nhập Excel vẫn báo 409 khi mã hàng đã có công đoạn cuối.
  - **Lọc theo trạng thái:** màn Công đoạn và màn Tổ bỏ ô "Hiện cả đã ngừng", dùng bộ lọc Trạng thái như các màn danh sách khác. Mặc định là Tất cả nên hiện cả dòng đã ngừng. Dòng tóm tắt của màn Công đoạn vẫn chỉ tính công đoạn đang dùng. Thêm bộ lọc Trạng thái cho màn Bàn cắt (`CuttingLayQuery.Status`, sai giá trị trả 400), Hàng hỏng của tôi (`DefectQuery.Status` ở `ListMineAsync`) và tab Lịch sử đồng bộ (`sync-sessions?status=`, sai giá trị trả 400); ba màn này lọc ở backend vì danh sách có giới hạn số dòng.
- **Chưa xử lý:** nhập Excel chưa đọc nhóm từ dòng tiêu đề nhóm của file. Gộp hai nhóm làm bằng cách lọc nhóm, tích hết rồi xếp sang nhóm kia.
- **Cần sửa trong tài liệu:** Report-3 UC25; thêm quyền Admin và chức năng quản lý nhóm công đoạn.
- **Trạng thái:** Mở

### DEV-ALL-51 · Nhân viên chưa bắt buộc thuộc tổ, quản lý thành viên trong màn Tổ

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC17, UC18 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `EmployeeService`, `TeamService`, validator tạo / sửa nhân viên, import nhân viên Excel, API `admin/teams` và `manager/teams`, màn Tổ ở FE. Không đổi schema (`Employee.TeamId` đã cho phép NULL).
- **Tài liệu gốc ghi:** UC17: nhân viên Công nhân, Tổ trưởng, Tổ phó bắt buộc chọn tổ khi tạo. UC18: tổ chỉ có chuyển tổ và bổ nhiệm.
- **Lý do:** yêu cầu của người dùng: tạo nhân viên trước, xếp vào tổ sau; quản lý thành viên ngay trong màn Tổ.
- **Code đang làm:**
  - **Tạo, sửa, import nhân viên:** chỉ Tổ trưởng và Tổ phó bắt buộc có tổ (trigger `trg_Employee_TeamLeadership` yêu cầu, và tổ phải là tổ cắt hoặc tổ may). Công nhân và các vai trò khác để trống tổ được, kể cả dòng import Excel không có mã tổ.
  - **Thêm nhân viên vào tổ** (`POST teams/{id}/add-members`): chỉ thêm người đang làm việc và chưa thuộc tổ nào; người đang ở tổ khác báo 409 `TEAM_MEMBER_HAS_TEAM`, phải dùng chuyển tổ. Tổ phải đang hoạt động. Danh sách người chọn lấy từ `GET teams/candidates` (không gồm Admin, Quản đốc, Kế toán). Có ghi `AuditLog` từng người, người được thêm phải đăng nhập lại.
  - **Loại khỏi tổ** (`POST teams/{id}/remove-members`): `TeamId` về NULL, người đó chờ được thêm vào tổ khác. Tổ trưởng, tổ phó báo 409 `TEAM_REMOVE_LEADER`, phải thôi giữ chức trước (giống chuyển tổ).
  - **Điều chuyển sang tổ khác:** giữ nguyên chức năng chuyển tổ có ngày hiệu lực (DEV-KH-10-1), chọn nhiều người trong tổ.
  - **Thôi giữ chức** (`POST teams/{id}/dismiss`): tổ trưởng hoặc tổ phó về Công nhân, vẫn ở tổ, giữ kiểu lương. Trước đây chỉ rời chức được bằng cách bổ nhiệm người khác.
  - **Bổ nhiệm:** màn Tổ hiện tổ trưởng, tổ phó đang giữ chức (hoặc "Chưa có"), tổ trưởng và tổ phó xếp đầu danh sách thành viên, tổ trưởng và tổ phó không tích chọn được để chuyển hay loại. Bổ nhiệm giữ nguyên quy tắc DEV-KH-10-2.
  - **Ảnh hưởng của công nhân chưa có tổ:** không có tổ nên không xuất hiện ở điểm danh, suất ăn, KPI, giao việc của tổ nào cho đến khi được thêm vào tổ.
- **Cần sửa trong tài liệu:** Report-3 UC17 (tổ không bắt buộc với công nhân), UC18 (thêm, loại thành viên, thôi giữ chức).
- **Trạng thái:** Mở

### DEV-ALL-52 · Lệnh cắt tự hoàn thành khi kho nhận đủ BTP

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC19, UC43 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `CutHandoverService.ConfirmAsync`, `WorkOrderCompletion` (mới), `WorkOrderService.CloseShortAsync` (thay `FinishCuttingAsync`), API `manager/work-orders/{id}/close-short` (mới, Admin dùng chung), bỏ API `team-leader/work-orders/{id}/finish-cutting`, màn chi tiết lệnh cắt. Không đổi schema.
- **Tài liệu gốc ghi / code cũ làm:** theo swimlane, BTP vào kho → tổ cắt trả vải thừa → tổ cắt bấm "Cắt xong" thì lệnh mới Hoàn thành và Quản đốc nhận số vải thực dùng (mục 943 của DEV-ALL-15, DEV-ALL-35).
- **Lý do:** yêu cầu của người dùng: tổ cắt không tự báo cắt xong nữa; Thủ kho nhận đủ số bàn giao theo lệnh thì hệ thống tự cập nhật lệnh cắt Hoàn thành.
- **Code đang làm:**
  - **Tự hoàn thành:** ngay khi Thủ kho xác nhận một phiếu bàn giao BTP, nếu mọi dòng của lệnh (màu × size, kể cả dòng chi tiết cắt lẻ) đã nhận đủ số kế hoạch tính theo số kho xác nhận (cộng mọi phiếu đã xác nhận) thì lệnh chuyển `COMPLETED`, ghi `CompletedAt`. Quản đốc nhận thông báo kèm vải đã nhận, đã trả, thực dùng tại thời điểm đó; tổ cắt nhận thông báo "lệnh cắt đã hoàn thành" cùng thông báo phiếu đã vào kho. Nhận dư kế hoạch vẫn tính là đủ.
  - **Bỏ nút "Cắt xong"** của tổ trưởng, tổ phó cắt. Trả vải thừa không còn là điều kiện để lệnh hoàn thành; tổ cắt vẫn lập phiếu trả vải sau khi lệnh hoàn thành được.
  - **Cắt thiếu:** Quản đốc hoặc Admin bấm "Đóng lệnh cắt thiếu" ở lệnh đang cắt, lý do bắt buộc (tối đa 300 ký tự, nối vào ghi chú lệnh). Chặn khi còn phiếu bàn giao hoặc phiếu trả vải chờ kho, hoặc còn BTP đã cắt chưa vào kho. Tổ cắt nhận thông báo lệnh đã đóng kèm số đã nhận / kế hoạch và lý do.
- **Cần sửa trong tài liệu:** Report-3 UC19, UC43 và swimlane tổ cắt: bỏ bước "báo cắt xong", thêm bước Quản đốc đóng lệnh cắt thiếu.
- **Trạng thái:** Mở

### DEV-ALL-53 · Vải còn lại chuyển sang gợi ý ở phiếu trả vải thừa

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC51, UC42 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** form bàn cắt (`LayFormModal`, bỏ `LayFabricBalance` và `utils/layFabric`), `MaterialReturnService.GetReturnableAsync` (thêm `ReturnableLineDto.SuggestedQty`), màn Lập phiếu trả vải. Không đổi schema.
- **Code cũ làm:** form bàn cắt hiện khối "Tổ đang giữ … m, đã cắt … m, còn lại … m" và số dư sau bàn này.
- **Lý do:** yêu cầu của người dùng: form bàn cắt gọn lại; số mét vải còn lại để hệ thống gợi ý khi tổ trưởng cắt trả vải về kho.
- **Code đang làm:**
  - **Form bàn cắt:** các ô Ngày cắt, Ca, Vải, Số lớp, Chiều dài bàn cắt nằm một hàng; bỏ khối vải còn lại; nhãn "Vải (tùy chọn)" sửa thành "Vải" vì ô này bắt buộc.
  - **Phiếu trả vải:** mỗi dòng phiếu cấp có số gợi ý = đã nhận − đã trả / đang trả − đã cắt (Σ số lớp × chiều dài các bàn của tổ trên cùng đơn, cùng vải), chia vào các dòng phiếu cấp cùng đơn, cùng vải (phiếu mới trước), không quá số còn trả được. Chọn phiếu cấp thì ô "Trả lần này" tự điền số gợi ý, tổ trưởng sửa lại theo cuộn vải thực tế. Có bàn cắt chưa nhập chiều dài thì không gợi ý và báo nhập theo cuộn thực tế.
- **Cần sửa trong tài liệu:** Report-3 UC42 (số trả gợi ý), UC51 (form bàn cắt).
- **Trạng thái:** Mở

### DEV-ALL-54 · Hộp thư tách thông báo do cấp trên gửi và do hệ thống tạo

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC05, UC20 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `NotificationQuery.Group`, `NotificationDto.IsManual`, `NotificationGroups`, màn hộp thư (`InboxTab`), `WorkAllocationService`; không đổi schema.
- **Tài liệu gốc ghi:** AI Context 4.8: thông báo gồm `Announcement` → `Notification`, không nêu việc lọc theo nguồn.
- **Code đang làm:** hộp thư lọc Tất cả / Từ quản lý (`SourceType = ANNOUNCEMENT`) / Hệ thống (các nguồn còn lại), mỗi dòng có nhãn nguồn. Hai thông báo phân việc của `WorkAllocationService` đổi nguồn từ WAREHOUSE sang PRODUCTION; thông báo đã tạo trước đó giữ nguồn cũ.
- **Lý do:** yêu cầu của người dùng.
- **Cần sửa trong tài liệu:** Report-3 UC05 (bộ lọc hộp thư).
- **Trạng thái:** Mở

### DEV-ALL-55 · Phân công công đoạn theo công nhân

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC52 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `OperationAssignmentsPage`, `AssignmentByWorker`, `WorkerOperationsModal`, `useSaveWorkerOperations`, `OperationOptionDto.StandardTimeSec`, bộ lọc tình trạng đổi thành dropdown; không đổi schema.
- **Tài liệu gốc ghi:** UC52: chọn công nhân theo từng công đoạn.
- **Code đang làm:** giữ màn cũ và thêm nút chuyển sang Theo công nhân: bảng công nhân kèm số công đoạn và các công đoạn đang làm, khung chọn công đoạn có lọc nhóm, tình trạng và nút Lưu. Lưu gồm một lần thêm theo lô rồi đóng từng phân công bị bỏ tích, chưa có API đóng hàng loạt nên không gói trong một transaction: lỗi giữa chừng thì phần trước vẫn đã lưu.
- **Lý do:** yêu cầu của người dùng, tổ đông người khó tìm công nhân trong ô chọn.
- **Cần sửa trong tài liệu:** Report-3 UC52 (thêm cách phân công theo công nhân).
- **Trạng thái:** Mở

### DEV-ALL-56 · KPI của tổ ở trang chủ tổ trưởng may

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC19 (KPI), UC21 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `TeamLeader/KpiTargetsController` (`GET /api/v1/team-leader/kpi-targets`), `TeamKpiPanel`, `HomePage`, `kpiColumns`; không đổi schema.
- **Tài liệu gốc ghi:** AI Context 4.8: KPI tổ may do Quản đốc lập, không nêu màn xem cho tổ trưởng.
- **Code đang làm:** tổ trưởng, tổ phó tổ may thấy khối KPI của tổ ở trang chủ, mặc định chỉ tiêu đang theo dõi, chỉ đọc. Tổ lấy từ token, bỏ qua `sewingTeamId` gửi lên (R1).
- **Lý do:** yêu cầu của người dùng.
- **Cần sửa trong tài liệu:** Report-3 UC19 (KPI), UC21 (trang chủ theo vai trò).
- **Trạng thái:** Mở

### DEV-ALL-57 · Nhập sản lượng cuối ca theo công nhân và theo nhóm công đoạn

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC55, GBR-20 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `OutputEntryBoard`, `OutputByWorker`, `WorkerOutputModal`, `workerOutputColumns`, `utils/outputWorkerBoard`, `useOutputEntry`, bỏ `OutputExtraRowBar`; `OutputGridRowDto.GroupName`. Không đổi schema, không đổi quy tắc lưu ở BE. DEV-KI-14-1: tổ trưởng không còn dạng thẻ trên điện thoại.
- **Tài liệu gốc ghi:** AI Context 4.3.5: lưới nạp sẵn từ phân công theo công nhân × công đoạn, công đoạn ngoài phân công thêm được cho riêng ca đó (GBR-20).
- **Code đang làm:**
  - Tổ trưởng, tổ phó thấy bảng công nhân của tổ theo ngày, ca, dòng đơn; mở khung của từng người để nhập và bấm Lưu. Quản đốc vẫn dùng lưới cũ (UC22).
  - Công đoạn chia theo nhóm. Nhập ở dòng nhóm điền cùng số cho các công đoạn lưu được của nhóm (còn hạn sửa, có đơn giá duyệt, còn số lần được giao); sửa lại từng công đoạn vẫn được.
  - Khung nhập chỉ hiện các công đoạn lấy từ màn Giao việc (đã giao và công nhân đã xác nhận), kèm dòng đã nhập từ trước. Bỏ thao tác thêm công đoạn ngoài phân công (GBR-20) ở màn tổ trưởng: theo quyết định của người dùng, muốn báo công đoạn nào thì giao ở màn Giao việc trước; trần `vw_WorkerOutputCap` (MSG16, trigger 50052) giữ nguyên. Công đoạn đã giao nhưng ngoài phân công vẫn hiện, có nhãn Ngoài phân công.
- **Lý do:** yêu cầu của người dùng.
- **Cần sửa trong tài liệu:** Report-3 UC55 (màn nhập, nhập theo nhóm), GBR-20.
- **Trạng thái:** Mở

### DEV-ALL-58 · Dữ liệu demo đủ chức năng (mục 19.7, 19.8)

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · dữ liệu demo · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `SQL/NX-MTD_Database.sql` mục 19.7, `SQL/patches/2026-11-02_demo_full_test_data.sql`, `SQL/patches/2026-11-02_demo_output_group_test_data.sql`; không đổi schema.
- **Tài liệu gốc ghi:** AI Context mục 1: khách hàng duy nhất là công ty mẹ (seed 1 dòng `Customer`). Coding Rules 3.12: migrate đặt ở `SQL/Migrations/`.
- **Code đang làm:** thêm Công ty B và Công ty C (ngừng) để thử danh sách và lọc. Công đoạn cuối DEMO-QU02 chuyển sang công đoạn đang dùng có thứ tự lớn nhất vì công đoạn cuối cũ đã ngừng dùng. Tháng 2026-05 chuyển LOCKED. Patch đặt ở `SQL/patches/` theo cách repo đang làm, không có `SQL/Migrations/`.
- **Lý do:** yêu cầu tạo dữ liệu test đủ chức năng của người dùng.
- **Cần sửa trong tài liệu:** không có, chỉ là dữ liệu demo; cần xóa Công ty B, C nếu vẫn giữ quy tắc một khách hàng khi demo.
- **Trạng thái:** Mở

### DEV-ALL-59 · NVL của đơn nằm trong chi tiết đơn; Quản đốc xem đơn hàng

- **Ngày / Người / Task / PR:** 02/11/2026 · Claude (AI) · UC09 (phần NVL) · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `Manager/CustomerOrdersController` (`GET /api/v1/manager/customer-orders`, `GET .../{id}`), `CustomerOrderService` (`ListOverviewAsync`, `GetOverviewAsync`, link thông báo đơn mới), `OrderOverviewPage`, `OrderOverviewListPage`, `OrderDetailTabs`, `OrderMaterialsTab`, `OrderMaterialLinkRedirect`; bỏ `OrderMaterialPage`, `PendingMaterialOrdersTable`, `GET admin|manager/order-materials/pending-orders`; không đổi schema. Thay phần đơn chờ khai báo của DEV-ALL-10 và DEV-ALL-26.
- **Tài liệu gốc ghi:** AI Context mục 3 và 4.1: đơn hàng do Admin quản lý, vai trò Quản đốc không có màn đơn hàng. DEV-ALL-10, DEV-ALL-26: NVL của đơn khai báo ở màn riêng `/warehouse/order-materials`, thông báo "Có đơn hàng mới" mở màn đó.
- **Code đang làm:**
  - Tạo đơn không nhập NVL. Tạo xong chuyển sang chi tiết đơn, tab "NVL của đơn"; thêm, sửa, sửa cả đơn, xóa NVL đều ở tab này. Đơn đã đóng thì tab chỉ đọc.
  - Admin và Quản đốc đều khai báo NVL ở tab này như trước đây ở màn riêng.
  - Bỏ menu và màn "NVL của đơn" cùng danh sách đơn chờ khai báo / mua NVL, theo quyết định của người dùng. Quản đốc, Admin tìm đơn cần NVL ở màn Đơn hàng (cột NVL). Thông báo đơn mới trỏ `/manager/orders/{id}?tab=materials`; đường dẫn cũ `/warehouse/order-materials` trong thông báo đã gửi chỉ còn chuyển hướng tới đơn hoặc danh sách đơn.
  - Quản đốc có menu "Đơn hàng" và màn chi tiết, chỉ xem: không có nút tạo, sửa, hủy, dừng, chuyển sản xuất; API tạo, sửa, hủy, dừng vẫn chỉ Admin gọi được. Lọc theo trạng thái và tìm kiếm; không có ô lọc khách hàng vì API danh sách khách chỉ dành cho Admin.
  - Giữ GBR-02: API của Quản đốc không trả đơn giá CMT, thành tiền; không có tab Lịch sử vì lịch sử đơn chứa đơn giá.
- **Lý do:** yêu cầu của người dùng.
- **Cần sửa trong tài liệu:** Report-3 UC09 (luồng, màn NVL của đơn, quyền Quản đốc); AI Context mục 3 (vai trò Quản đốc).
- **Trạng thái:** Mở

### DEV-ALL-60 · Thêm NVL của đơn theo mã hàng hoặc NVL ngoài

- **Ngày / Người / Task / PR:** 03/11/2026 · Claude (AI) · UC09 (phần NVL) · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `OrderMaterialService` (`GetProductMaterialsAsync`, `CreateBatchAsync`), `GET /api/v1/{admin|manager}/order-materials/product-materials`, `POST /api/v1/{admin|manager}/order-materials/batch`, `OrderMaterialsTab`, `OrderMaterialByProductModal`; không đổi schema. Bổ sung DEV-ALL-59.
- **Tài liệu gốc ghi:** UC09: khai báo từng NVL của đơn, chọn trong toàn bộ danh mục NVL.
- **Code đang làm:**
  - Tab NVL của đơn có hai nút. "Thêm theo mã hàng": chọn mã hàng trong đơn (đơn một mã thì tự chọn), bảng liệt kê NVL định mức của những màu × size đơn đặt, nhập số cần, khách cấp, số lần giao, ghi chú. "Thêm NVL ngoài": form từng dòng như trước, ô chọn NVL không có NVL đã có trong đơn.
  - Số lượng vẫn nhập tay, không tính từ định mức vì định mức không còn lượng dùng. NVL dùng chung cho nhiều mã hàng vẫn chỉ một dòng trong đơn (`UQ_OM`), không ghi NVL thuộc mã nào.
  - Bảng không hiện NVL đơn đã khai báo, NVL hoặc thành phần đã ngừng dùng. NVL không dùng thì xóa khỏi bảng; mọi dòng còn lại phải có số. Lưu cả bảng một lần: một dòng lỗi thì không lưu dòng nào.
- **Lý do:** yêu cầu của người dùng.
- **Cần sửa trong tài liệu:** Report-3 UC09 (luồng thêm NVL của đơn).
- **Trạng thái:** Mở

### DEV-ALL-61 · Thêm NVL mới ngay ở màn định mức mã hàng

- **Ngày / Người / Task / PR:** 03/11/2026 · Claude (AI) · UC10, UC38 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `POST /api/v1/accountant/materials`, `SpecNormsTab`, `specNormColumns`, `useSaveMaterial`; không đổi schema.
- **Tài liệu gốc ghi:** UC38: Thủ kho quản lý danh mục NVL. Admin, Quản đốc được thêm nhanh khi khai báo NVL của đơn. Coding Rules 4.3: feature không import chéo feature khác.
- **Code đang làm:**
  - Ô chọn NVL trên từng dòng định mức có nút Thêm NVL mới như màn Thêm NVL ngoài; tạo xong tự chọn NVL đó cho dòng đang sửa.
  - Kế toán được thêm NVL mới vì là người nhập định mức. Sửa, ngừng dùng NVL vẫn chỉ Thủ kho.
  - Feature đơn hàng nạp lười `MaterialFormModal` qua `index.ts` của feature kho, như `purchase` đang dùng `PurchaseOrderFormModal`.
- **Lý do:** yêu cầu của người dùng.
- **Cần sửa trong tài liệu:** Report-3 UC10, UC38 (quyền thêm NVL của Kế toán); AI Context mục 3.
- **Trạng thái:** Mở

### DEV-ALL-62 · Chỉ tiêu KPI tổ may chia theo màu × size

- **Ngày / Người / Task / PR:** 03/11/2026 · Claude (AI) · UC19 (KPI tổ may), UC21 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** schema: bảng mới `KpiTargetLine` (`KpiTargetId`, `OrderLineId`, `TargetQty`; `UQ_KpiTargetLine`), patch `SQL/patches/2026-11-03_all_changes.sql`, script chính đã cập nhật. API: `SaveKpiTargetDto` bỏ `TargetFinishedQty`, thêm `Lines` (`orderLineId`, `targetQty`); `KpiTargetDto` thêm `Lines` (màu, size, chỉ tiêu, QC đạt); thêm `GET manager/kpi-targets/line-capacity`. `KpiService`, `IOutputQueryService.GetKpiLineFinishedAsync`, `KpiTargetFormModal`, `KpiLineTargetsTable`, cột Màu × size ở bảng KPI và KPI của tổ ở trang chủ. Bổ sung DEV-ALL-23, DEV-ALL-49.
- **Tài liệu gốc ghi:** UC19 / FR 3.14: chỉ tiêu KPI theo đơn + mã hàng + tổ + kỳ, một số `TargetFinishedQty`. DEV-ALL-23: tổng chỉ tiêu các tổ không vượt Σ số đặt của mã hàng. DEV-ALL-49: KPI vẫn theo mã hàng, chưa chia màu, size.
- **Lý do:** yêu cầu của người dùng. Khi nhiều tổ cùng làm một mã hàng, chia KPI chỉ có số lượng nên không biết tổ nào làm màu, size nào.
- **Code đang làm:**
  - **Lập / sửa:** chọn tổ xong, form liệt kê các màu × size của mã hàng đã chia cho tổ (`OrderLineTeam`): số đặt, đã giao cho tổ khác, tối đa; Quản đốc, Admin nhập chỉ tiêu từng dòng, để trống là không giao. Chỉ tiêu cả kỳ = tổng các dòng (BE tính, vẫn lưu ở `KpiTarget.TargetFinishedQty` nên lịch KPI theo ngày, kết thúc đơn, thông báo nhận thành phẩm không đổi).
  - **Trần theo từng dòng:** chỉ tiêu dòng ≤ số đặt − phần đã giao cho tổ khác ở dòng đó (chỉ tiêu đang chạy tính số giao, đã đóng tính QC đạt của dòng trong kỳ, đã hủy không tính), 409 `KPI_EXCEEDS_ORDER_QTY` nêu màu, size. Vẫn kiểm cả mã hàng như DEV-ALL-23 vì chỉ tiêu cũ có thể chỉ có số tổng.
  - **Dòng không hợp lệ:** dòng không thuộc mã hàng hoặc chưa chia cho tổ thì 409 `KPI_INVALID_REFERENCE`. Validator: ít nhất một dòng, tối đa 500, không trùng dòng, mỗi dòng 1 đến 1.000.000, tổng tối đa 1.000.000.
  - **Sửa:** dòng mới thêm, dòng bỏ đi xóa, dòng còn sửa số. Một dòng không được hạ (hoặc bỏ) dưới số QC đạt của tổ ở dòng đó trong kỳ (409 `KPI_PLAN_INVALID`). Nhật ký ghi cả danh sách dòng trước và sau.
  - **Xem:** bảng KPI (Quản đốc, Admin) và KPI của tổ ở trang chủ tổ trưởng có cột Màu × size: bấm xem QC đạt / chỉ tiêu từng màu × size. QC đạt theo dòng đếm như `vw_KpiDailyResult`: QC của tổ, đúng đơn + mã hàng, trong kỳ.
  - **Chỉ tiêu cũ:** patch chia số tổng của chỉ tiêu chưa hủy cho các màu × size đã chia cho tổ, theo tỉ lệ số còn trống của từng dòng; không đủ chỗ thì để nguyên, bảng hiện "Chưa chia", sửa thì form nhắc nhập lại theo màu × size.
  - **Không đổi:** KPI theo ngày vẫn theo tổng của chỉ tiêu, không chia màu × size.
- **Ảnh hưởng người khác:** Minh (KPI tổ may), Morgan (schema). DB đang chạy phải chạy patch trước khi chạy backend mới.
- **Cần sửa trong tài liệu:** Report-3 UC19, UC21, FR 3.14 (form lập KPI theo màu × size, trần theo dòng); `NX-MTD_AI_Context.md` mục 4.8 (KPI tổ may) và 6.2 (bảng mới); `NX-MTD_Database.dbml`, `NX-MTD_Database_Mo_ta.md`, ERD.
- **Trạng thái:** Mở

### DEV-ALL-63 · Giao sản lượng công đoạn theo ngày cho công nhân

- **Ngày / Người / Task / PR:** 03/11/2026 · Claude (AI) · UC53, UC54, UC60 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** schema: `WorkAllocation` thêm `WorkDate`, `RetrievalNote`; `CK_WA_Mode` thêm `LEADER`, `SYSTEM`; `CK_WA_Behalf` chỉ bắt lý do với `ON_BEHALF`; `CK_WA_Retr` thêm `END_OF_DAY`, `ADJUSTED`; thêm `IX_WA_Day`; patch `SQL/patches/2026-11-03_all_changes.sql`, script chính đã cập nhật. API (`team-leader/work-allocations`): `GET daily`, `PUT daily`, `POST daily/retrieve-remaining`; `WorkAllocationDto` thêm `workDate`, `retrievalNote`. BE: `DailyWorkAllocationService`, `WorkAllocationAutoRetrieveJob`. FE: màn "Giao sản lượng theo ngày" (`DailyAllocationBoard`, `DailyWorkerModal`, `RetrieveRemainingModal`) thay bảng chia việc theo công đoạn và khung Giao / Thu hồi của từng công đoạn.
- **Tài liệu gốc ghi:** UC53 / UC54 / UC60: tổ trưởng chia việc theo công đoạn (số lần làm), công nhân xác nhận hoặc tổ trưởng xác nhận hộ; không có khái niệm ngày. Trần sản lượng cộng dồn theo công nhân × dòng đơn × công đoạn.
- **Code đang làm:**
  - **Giao theo ngày:** mỗi ngày tổ trưởng chọn ngày, đơn, dòng đơn; bảng công nhân của tổ (đã giao, đã làm, chưa làm hết trong ngày). Khung của từng công nhân lấy sẵn các công đoạn đã phân công (`OperationAssignment`) trước khi may, nhập số lần giao của ngày, nhập ở dòng nhóm thì điền cả nhóm như màn Sản lượng cuối ca. Thêm được công đoạn ngoài phân công (gắn nhãn).
  - **Phiếu của tổ trưởng ghi là CONFIRMED ngay** (`ConfirmMode = LEADER`, `ConfirmedQty = Quantity`), công nhân không phải xác nhận. Đường xác nhận / từ chối của công nhân và xác nhận hộ giữ nguyên cho phiếu PENDING cũ. Chỉ giao cho hôm nay hoặc ngày đã qua.
  - **Lưu là "số của ngày":** BE so với số giao ròng của ngày, giao thêm phần chênh (tối đa số còn giao được của công đoạn, 409 `ALLOCATION_EXCEEDS_REQUIRED`); hạ số thì ghi phiếu thu hồi `ADJUSTED`, bắt buộc lý do (`RetrievalNote`), không hạ dưới sản lượng đã báo của ngày (409 `PIECE_RETRIEVAL_EXCEEDS_HOLDING`).
  - **Thu hồi cuối ngày:** tổ trưởng bấm Thu hồi cuối ngày (chọn công nhân hoặc cả tổ, bắt buộc lý do): thu phần được giao nhưng chưa làm hết của ngày (`END_OF_DAY`). Nếu không thu, `WorkAllocationAutoRetrieveJob` (15 phút một lần) tự thu các ngày đã qua hạn sửa sản lượng (ngày D thu khi sang D+2), `ConfirmMode = SYSTEM`, ghi lý do tự động.
  - **Trần sản lượng không đổi:** `vw_WorkerOutputCap`, `trg_OutputRecord_Check`, `trg_WorkAllocation_Check` vẫn cộng dồn giao − thu hồi. Vì phần chưa làm hết của mỗi ngày bị thu hồi nên trần cộng dồn bằng phần giao còn lại; trong lúc ngày D chưa được thu, báo sản lượng của ngày D+1 vẫn tính trên phần dư đó. Phiếu cũ: `WorkDate` = ngày tạo.
  - **Màn đổi tên:** "Giao việc cho công nhân" thành "Giao sản lượng công đoạn theo ngày" (menu: Giao sản lượng theo ngày). Tab Phiếu giao / thu hồi giữ, thêm cột ngày làm việc và lý do thu hồi. Màn Phân công công đoạn (`OperationAssignment`) không đổi.
- **Lý do:** yêu cầu của người dùng.
- **Ảnh hưởng người khác:** Thái (TH-19 giao việc), Minh (sản lượng, UC55), Morgan (schema). DB đang chạy phải chạy patch trước khi chạy backend mới. Chưa có test tích hợp trên SQL Server cho trigger với phiếu LEADER / SYSTEM.
- **Cần sửa trong tài liệu:** Report-3 UC53, UC54, UC60 (giao theo ngày, thu hồi cuối ngày, không còn xác nhận của công nhân với phiếu của tổ trưởng); FR 3.5, 3.6, 3.8; `NX-MTD_AI_Context.md` mục 4.3; `NX-MTD_Database_Mo_ta.md`, DBML, ERD (WorkAllocation).
- **Trạng thái:** Mở

### DEV-ALL-64 · Giao việc chuyển sang hôm sau, chuyển phần vượt khi nhập sản lượng cuối ca

- **Ngày / Người / Task / PR:** 03/11/2026 · Claude (AI) · UC53, UC55, UC60 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** không đổi schema (dùng `WorkAllocation.WorkDate`, `RetrievalReason = ADJUSTED`, `ConfirmMode = LEADER` đã có từ DEV-ALL-63; trigger 50050 / 50052 giữ nguyên). API: bỏ `POST team-leader/work-allocations/daily/retrieve-remaining`; `GET daily` thêm `carriedQty`, `previousQty` ở mỗi dòng; `DailyAllocationResultDto` bỏ `retrieved`; `GET .../output-records/grid` thêm `transferableQty` ở mỗi dòng, `POST .../output-records` thêm `transferExcess` ở thân lưu. BE: bỏ `WorkAllocationAutoRetrieveJob`, `RetrieveRemainingAsync`, `AutoRetrieveAsync`; thêm `OutputCapTransfer`. FE: bỏ nút / modal Thu hồi cuối ngày; bảng và khung giao việc thêm cột Tồn từ hôm trước, nút Giao như lần trước; khung nhập sản lượng của công nhân có cảnh báo chuyển phần vượt và hỏi xác nhận.
- **Tài liệu gốc ghi:** DEV-ALL-63: cuối ngày phần chưa làm hết được thu hồi (tổ trưởng bấm, hoặc hệ thống tự thu sau hạn sửa sản lượng). UC55: sản lượng vượt số lần được giao thì chặn (MSG16), tổ trưởng phải sang màn Giao việc giao thêm.
- **Code đang làm:**
  - **Không thu hồi cuối ngày:** phần chưa làm hết của các ngày trước là "Tồn từ hôm trước" của chính công nhân đó. Trần sản lượng của ngày = tồn từ hôm trước + giao trong ngày (trần cộng dồn `vw_WorkerOutputCap` vốn đã như vậy). Hạ số giao của ngày: tồn + số giao mới không nhỏ hơn sản lượng đã báo trong ngày. Phiếu `END_OF_DAY` / `SYSTEM` cũ vẫn hiện trong lịch sử.
  - **Giao như lần trước:** khung giao việc điền lại số giao ròng của lần giao gần nhất trước ngày đang xem cho từng công đoạn, đưa về khoảng nhập được.
  - **Trách nhiệm theo công đoạn, không theo BTP vật lý:** một BTP qua nhiều công đoạn do nhiều người làm thì mỗi người chịu trách nhiệm số lần của công đoạn mình; hàng chuyền tay giữa các công nhân trong ngày không phải ghi.
  - **Chuyển phần vượt cuối ca:** hai người chia nhau một công đoạn mà làm lệch so với lúc giao, khi nhập sản lượng người làm vượt, màn hình báo phần vượt chuyển được (phần dư của người cùng tổ, cùng công đoạn × dòng đơn, cộng phần tổ chưa giao). Tổ trưởng xác nhận thì trong một giao dịch: thu hồi `ADJUSTED` từ người dư nhiều trước (ghi lý do vào `RetrievalNote`), thiếu thì lấy phần tổ chưa giao, giao phần vượt cho người báo vượt, rồi mới ghi sản lượng. Lưu thu hồi, giao, sản lượng theo thứ tự (gọi `SaveChanges` mỗi bước) để trigger 50050 / 50052 thấy đúng trần. Người đang báo thêm trong cùng lần lưu không bị lấy phần dư. Không đủ nguồn thì chặn như cũ (409 `OUTPUT_EXCEEDS_CAP`).
- **Lý do:** yêu cầu của người dùng: một BTP có nhiều công đoạn chia cho nhiều người, hàng chuyền liên tục nên tổ trưởng không thể thu hồi / giao lại trong ngày; tổ trưởng chỉ nhập sản lượng cuối ca.
- **Ảnh hưởng người khác:** Thái (TH-19 giao việc), Minh (sản lượng, UC55). Không cần chạy patch DB.
- **Cần sửa trong tài liệu:** Report-3 UC53, UC55 (MSG16: cho chuyển phần vượt), UC60; FR 3.5, 3.6, 3.8; `NX-MTD_AI_Context.md` mục 4.3; `NX-MTD_Database_Mo_ta.md` mục 4.7 (`WorkAllocation`: không còn thu hồi cuối ngày, ví dụ chuyển phần vượt).
- **Trạng thái:** Mở

### DEV-ALL-65 · Quản đốc, Admin quản lý danh mục NVL

- **Ngày / Người / Task / PR:** 03/11/2026 · Claude (AI) · UC38 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** API: `admin/materials` và `manager/materials` thêm `GET` (danh sách phân trang), `GET {id}`, `PUT {id}`, `PATCH {id}/status` (trước chỉ có `POST` thêm nhanh). FE: menu và route "Danh mục NVL" (`ROUTES.materials`) mở cho Admin, Quản đốc; trang dùng chung `MaterialListPage`, gọi API theo role đang đăng nhập. Không đổi schema.
- **Tài liệu gốc ghi:** UC38: Thủ kho quản lý danh mục NVL. Quản đốc / Admin chỉ thêm nhanh NVL mới khi lập NVL của đơn (UC09), sửa / ngừng dùng vẫn do Thủ kho.
- **Code đang làm:** Quản đốc và Admin xem, tìm, lọc, thêm, sửa, ngừng dùng / dùng lại NVL như Thủ kho, cùng quy tắc kiểm tra của `IMaterialService` (trùng mã, màu NVL...).
- **Lý do:** yêu cầu của người dùng.
- **Ảnh hưởng người khác:** Thái (UC38, danh mục NVL).
- **Cần sửa trong tài liệu:** Report-3 UC38 (thêm actor Quản đốc, Admin); `NX-MTD_AI_Context.md` mục 3 (quyền theo role).
- **Trạng thái:** Mở

### DEV-ALL-66 · Xác nhận giao không nhập số khách nhận; xuất kho báo Quản đốc

- **Ngày / Người / Task / PR:** 03/11/2026 · Claude (AI) · UC45, UC47 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `DispatchNoteService` (`ConfirmDeliveryAsync`, `ExportAsync`), `ConfirmDeliveryDto` (bỏ `Lines`, bỏ `DeliveryLineInputDto`), `ConfirmDeliveryDtoValidator`, `WarehouseMessages.DispatchReceivedExceedsIssued` (bỏ). API `POST accountant/dispatch-notes/{id}/confirm-delivery` chỉ còn thân `{ customerReceivedDate, rowVer }`. FE: màn chi tiết phiếu xuất bỏ ô nhập số khách nhận, cột Khách nhận chỉ hiển thị. Không đổi schema.
- **Tài liệu gốc ghi:** UC47 / AI_Context mục 4.4: Kế toán xác nhận khách đã nhận → `DELIVERED` / `PARTIALLY_DELIVERED`; GBR-24: giao hàng luôn thành công. Code trước đây cho Kế toán nhập số khách nhận từng dòng (≤ số xuất). Xuất kho chỉ báo Kế toán.
- **Code đang làm:**
  - **Khách nhận = số xuất:** Kế toán chỉ chọn ngày khách nhận và tải ảnh biên bản giao; hệ thống ghi `ReceivedQty = IssueQty` cho mọi dòng, phiếu luôn `DELIVERED`. Phiếu không còn vào `PARTIALLY_DELIVERED` (giá trị vẫn giữ trong `CK_DN_Status` cho dữ liệu cũ). Trạng thái đơn hàng vẫn theo GBR-23 (`PARTIALLY_DELIVERED` khi tổng đã giao < số đặt).
  - **Thông báo khi xuất kho:** ngoài Kế toán, báo thêm Quản đốc "Hàng đã xuất kho"; Admin nhận bản sao thông báo của Quản đốc (DEV-ALL-67).
  - **Hủy phiếu:** giữ như cũ, Quản đốc chỉ hủy được phiếu `PENDING` (hàng chưa xuất kho).
- **Lý do:** yêu cầu của người dùng. Nhập số khách nhận nhỏ hơn số xuất làm phần chênh lệch đã trừ kho nhưng không quay lại kho, không lập phiếu giao bù được và đơn không lên được `DELIVERED`.
- **Ảnh hưởng người khác:** Thái (xuất hàng UC45–47).
- **Cần sửa trong tài liệu:** Report-3 UC47 (bỏ bước nhập số khách nhận, bỏ `PARTIALLY_DELIVERED` của phiếu), UC46 (thông báo Quản đốc, Admin); `NX-MTD_AI_Context.md` mục 4.4.
- **Trạng thái:** Mở

### DEV-ALL-67 · Admin nhận thông báo của Quản đốc; Quản đốc khác nhận thông báo việc Quản đốc làm

- **Ngày / Người / Task / PR:** 03/11/2026 · Claude (AI) · thông báo (KH-12) · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `NotificationService` (Infrastructure, thêm `ICurrentUser`). Không đổi `INotificationService`, không đổi chỗ gọi trong các service, không đổi schema. Thông báo chung (`Announcement`, `NotificationCenterService`) không đổi.
- **Tài liệu gốc ghi:** mỗi UC nêu người nhận thông báo riêng (VD UC37 phiếu nhập chờ xác nhận báo Quản đốc, UC46 Thủ kho từ chối phiếu xuất báo Quản đốc); Admin chỉ nhận một số thông báo (nguy cơ trễ hạn JOB-04, duyệt đơn giá, đề xuất bậc...).
- **Code đang làm:** áp chung cho mọi thông báo gửi qua `INotificationService`:
  - **Admin nhận bản sao** mọi thông báo có Quản đốc trong danh sách người nhận (thông báo Quản đốc nhận được coi là việc quan trọng cấp quản lý): phiếu nhập chờ xác nhận, đơn từ chờ Quản đốc xử lý, phiếu xuất bị từ chối, xuất kho, lệnh cắt hoàn thành, nhận thành phẩm báo KPI, kiểm kê chờ duyệt, thiếu NVL, đơn mua...
  - **Quản đốc khác nhận bản sao** mọi thông báo phát sinh từ thao tác của một Quản đốc (VD Quản đốc lập phiếu xuất → Thủ kho và các Quản đốc khác cùng nhận). Bản sao này không kéo thêm Admin.
  - **Người thao tác không nhận** thông báo về việc chính mình làm (trước đây Quản đốc / Admin thao tác có thể tự nhận).
  - Cùng một thông báo chỉ ghi một lần cho mỗi người trong một request (gọi Quản đốc rồi gọi Admin với cùng nội dung không bị trùng).
  - Job nền và agent chấm công không có người thao tác: chỉ áp luật Admin nhận bản sao.
- **Lý do:** yêu cầu của người dùng: hoạt động có thông báo quan trọng thì báo thêm Admin; thông báo do Quản đốc làm thì báo cho các Quản đốc khác.
- **Ảnh hưởng người khác:** tất cả luồng có thông báo (Khiêm, Kiều, Thái, Minh, Thảo); Admin và Quản đốc sẽ nhận nhiều thông báo hơn.
- **Cần sửa trong tài liệu:** Report-3 các UC có bước thông báo (người nhận); `NX-MTD_AI_Context.md` mục 4.8 (Thông báo).
- **Trạng thái:** Mở

### DEV-ALL-68 · Chọn đơn hàng, mã hàng, màu - size trước khi giao việc và nhập sản lượng

- **Ngày / Người / Task / PR:** 03/11/2026 · Claude (AI) · UC53, UC55 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** FE: component dùng chung `OrderLinePicker` (`components/common`), `OrderSelect` (thêm `sewingTeamId`, `disabled`), `useOrderOptions` / `useOrderLineLookup` (thêm `sewingTeamId`), `DailyAllocationBoard` (Giao sản lượng theo ngày), `OutputFilterBar` / `useOutputEntry` (Sản lượng cuối ca). BE: `DailyWorkAllocationService` (`GetBoardAsync`, `ValidateAsync`), `OutputService` (`GetGridAsync`, `SaveAsync`), `LookupService.GetOpenOrdersAsync`, `OrderLookupService.LinesAsync`. API: `GET common/lookups/orders` và `GET common/order-lookups/orders/{id}/lines` nhận thêm `sewingTeamId` (tùy chọn). Không đổi schema.
- **Tài liệu gốc ghi:** DEV-ALL-63: màn giao việc chọn ngày, đơn, dòng đơn (dòng đơn không bắt buộc, để trống là xem mọi dòng đơn). Sản lượng cuối ca chọn dòng đơn trong một ô "mã hàng - màu - size".
- **Code đang làm:**
  - Cả hai màn chọn theo thứ tự **Đơn hàng → Mã hàng → Màu - Size** (ô màu - size chỉ hiện các màu - size của mã hàng đã chọn, kèm số đặt nếu có). Đơn chỉ có một mã hàng thì mã đó được chọn sẵn.
  - **Giao sản lượng theo ngày:** chưa chọn đủ thì bảng công nhân trống và báo "Chọn mã hàng và màu - size để giao việc"; bỏ lựa chọn "Tất cả dòng đơn". Khung giao việc của công nhân chỉ có công đoạn của dòng đơn đã chọn.
  - **Sản lượng cuối ca:** vẫn bắt buộc dòng đơn như trước, tách ô chọn thành mã hàng và màu - size. Quản đốc phải chọn tổ may trước; đổi tổ thì bỏ đơn, dòng đơn đã chọn.
  - **Chỉ phần đã chia cho tổ (`OrderLineTeam`, DEV-ALL-49):** ô đơn hàng, mã hàng, màu - size chỉ hiện đơn / dòng đơn đã chia cho tổ (tổ trưởng: tổ của mình; Quản đốc: tổ đang chọn qua `sewingTeamId`, tổ may truyền tổ khác vẫn chỉ thấy tổ mình). BE chặn thêm: bảng giao việc bỏ dòng đơn chưa chia cho tổ; lưu giao việc, tải lưới và lưu sản lượng với dòng đơn chưa chia trả 409 `ASSIGNMENT_PRODUCT_NOT_ALLOTTED` (MSG "Màu, size này chưa được chia cho tổ của bạn..."). Trước đây màn giao việc hiện mọi dòng đơn cùng mã hàng và màn sản lượng không kiểm tra việc chia.
- **Lý do:** yêu cầu của người dùng: phải chọn đơn hàng, mã hàng, màu, size rồi mới giao để công nhân biết đang may cái gì; chỉ được chọn phần đã giao cho tổ.
- **Ảnh hưởng người khác:** Thái (TH-19 giao việc), Minh (UC55 sản lượng).
- **Cần sửa trong tài liệu:** Report-3 UC53, UC55 (mockup bộ lọc); `NX-MTD_AI_Context.md` mục 4.3.
- **Trạng thái:** Mở

### DEV-ALL-69 · Màn Sửa sản lượng sau hạn dùng bố cục của Sản lượng cuối ca

- **Ngày / Người / Task / PR:** 03/11/2026 · Claude (AI) · UC22, UC55 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** FE: `OutputEntryBoard`, `OutputByWorker` (dùng được cho tổ may Quản đốc chọn), `WorkerOutputModal` (thêm `requireReason`), `useOutputEntry` (bỏ số đang sửa trên lưới, lý do chung, `handleSave`); thêm `OutputReasonField`, `WorkerOutputAlerts`; xóa `outputColumns.tsx`, `OutputMobileList.tsx` (lưới công nhân × công đoạn và dạng thẻ trên điện thoại của Quản đốc), `outputKeyOf`. Không đổi API, không đổi schema.
- **Tài liệu gốc ghi:** UC22 / KI-11: Quản đốc sửa sản lượng sau hạn trên lưới công nhân × công đoạn, ghi một lý do cho cả lần lưu.
- **Code đang làm:**
  - Quản đốc chọn ngày, ca, **tổ may**, đơn hàng, mã hàng, màu - size (DEV-ALL-68) → bảng công nhân của tổ đó (số công đoạn đã nhập trong ca, tổng sản lượng, cảnh báo chưa chấm công; tìm và lọc chưa nhập / đã nhập) → bấm công nhân mở khung nhập theo nhóm công đoạn như tổ trưởng (điền theo nhóm, tìm công đoạn, cảnh báo vượt số giao, chuyển phần vượt).
  - **Lý do sửa** nằm trong khung nhập của từng công nhân, bắt buộc với Quản đốc (chưa ghi thì nút Lưu bị khóa và báo "Quản đốc sửa sản lượng phải ghi lý do."); mỗi lần lưu là sản lượng của một công nhân. Quản đốc vẫn sửa được dòng đã quá hạn (cờ `canEdit` do BE trả), tổ trưởng thì không. Khung của tổ trưởng không có ô lý do.
  - Điện thoại: dùng chung bảng công nhân của tổ trưởng thay cho dạng thẻ riêng của Quản đốc.
- **Lý do:** yêu cầu của người dùng: màn của Quản đốc gần giống màn Sản lượng cuối ca của tổ trưởng.
- **Ảnh hưởng người khác:** Minh (UC55 sản lượng), Kiều (UC22 sửa dữ liệu quá hạn).
- **Cần sửa trong tài liệu:** Report-3 UC22 (mockup, lý do theo từng lần lưu của công nhân), UC55.
- **Trạng thái:** Mở

### DEV-ALL-70 · Phiếu xuất hàng bỏ trạng thái giao một phần và số khách nhận

- **Ngày / Người / Task / PR:** 03/11/2026 · Claude (AI) · UC45–UC47 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** schema: `CK_DN_Status`, `CK_DN_Delivered`, `CK_DN_Dispatched` bỏ `PARTIALLY_DELIVERED`; bỏ cột `DispatchNoteLine.ReceivedQty` và `CK_DNL_Received`; `vw_OrderLineProgress.DeliveredQty` = tổng `IssueQty` của phiếu `DELIVERED`. Patch `SQL/patches/2026-11-03_dispatch_delivered_only.sql` (chuyển phiếu `PARTIALLY_DELIVERED` cũ thành `DELIVERED`, đơn `PARTIALLY_DELIVERED` đã đủ số thành `DELIVERED`); `SQL/NX-MTD_Database.sql` (bảng, view, dữ liệu demo XH-DEMO-010) và `docs/NX-MTD_Database.dbml` đã cập nhật. BE: `DispatchStatuses` (bỏ `PartiallyDelivered`), `DispatchNoteLine` (bỏ `ReceivedQty`), `DispatchNoteListItemDto` (bỏ `TotalReceivedQty`), `DispatchNoteLineDto` (bỏ `ReceivedQty`), `DispatchNoteService`, `DispatchNoteAttachmentAccess`, Excel phiếu xuất (bỏ cột "SL khách nhận"). FE: bỏ trạng thái "Giao một phần" (bộ lọc, nhãn), bỏ cột Khách nhận ở danh sách và chi tiết phiếu; "Đã giao đủ" đổi thành "Đã giao".
- **Tài liệu gốc ghi:** AI_Context 4.4 / UC47: Kế toán xác nhận khách đã nhận → `DELIVERED` / `PARTIALLY_DELIVERED`; DB có `ReceivedQty` (khách nhận thực tế ≤ số xuất). DEV-ALL-66 đã bỏ ô nhập số khách nhận (ghi bằng số xuất) nhưng còn giữ cột và trạng thái.
- **Code đang làm:** phiếu xuất chỉ còn PENDING / REJECTED / CANCELLED / IN_DELIVERY / DELIVERED. Kế toán xác nhận khách nhận (ngày + ảnh biên bản) thì phiếu `DELIVERED`, khách nhận đúng số xuất. Đơn hàng vẫn có `PARTIALLY_DELIVERED` theo GBR-23: tổng số xuất của các phiếu đã giao < số đặt.
- **Lý do:** yêu cầu của người dùng.
- **Ảnh hưởng người khác:** Thái (xuất hàng UC45–47), Khiêm (tiến độ đơn UC21 đọc `vw_OrderLineProgress`), Morgan (schema). DB đang chạy phải chạy patch trước khi chạy backend mới (entity không còn `ReceivedQty`). Patch dữ liệu demo cũ `SQL/patches/2026-11-02_demo_full_test_data.sql` còn ghi `ReceivedQty` / `PARTIALLY_DELIVERED`: chỉ chạy được trước patch này.
- **Cần sửa trong tài liệu:** Report-3 UC47 (bỏ giao một phần, bỏ số khách nhận), sơ đồ trạng thái phiếu xuất; `NX-MTD_AI_Context.md` mục 4.4 và bảng trạng thái (`DispatchNote.Status`); `NX-MTD_Database_Mo_ta.md`, ERD (`DispatchNoteLine`).
- **Trạng thái:** Mở

### DEV-ALL-71 · Màu áo thành bảng `ProductColor`; mã hàng tối đa 5 ảnh phụ, mỗi màu 1-3 ảnh

- **Ngày / Người / Task / PR:** 05/11/2026 · Claude (AI) · UC10 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:**
  - Schema: bảng mới `ProductColor` (`ProductId`, `Color` NVARCHAR(50), UNIQUE (`ProductId`, `Color`), UNIQUE (`ProductColorId`, `ProductId`)). `ProductVariant`, `ProductMarkerFabric` **bỏ cột `Color`**, thêm `ProductColorId` với FK ghép (`ProductColorId`, `ProductId`) → `ProductColor`. `UQ_PV` thành (`ProductColorId`, `SizeCode`), `UQ_PMF` thành (`ProductId`, `MarkerNo`, `ProductColorId`). Bảng mới `ProductColorImage` (`ProductColorId`, `ImageKey`, `SortOrder` 1-3, UNIQUE (`ProductColorId`, `SortOrder`)). `CK_ProductImage_Sort` đổi từ 1-10 thành 1-5. `vw_OrderLineProgress.Color` đọc từ `ProductColor`.
  - Patch `SQL/patches/2026-11-05_product_color_images.sql`: đưa các tên màu đang có vào `ProductColor` (bỏ khoảng trắng đầu cuối, màu chỉ khác hoa thường gộp làm một), chuyển biến thể và vải của bìa sang `ProductColorId` rồi bỏ cột `Color`, gỡ ảnh phụ thứ 6 trở đi. `SQL/NX-MTD_Database.sql` (bảng, view, dữ liệu demo), `docs/NX-MTD_Database.dbml`, `docs/NX-MTD_Database_Mo_ta.md` đã cập nhật.
  - BE: entity `ProductColor`, `ProductColorImage`; `ProductVariant.Color`, `ProductMarkerFabric.Color` đổi thành điều hướng `ProductColor`. Mọi chỗ đọc màu của dòng đơn (cắt, sản xuất, KPI, kho, xuất hàng, đơn từ…) đọc `ProductVariant.ProductColor.Color`. DTO, API vẫn trả `color` dạng chuỗi như cũ nên FE không đổi. Lưu đơn hàng gặp màu mới thì tạo `ProductColor` trước, rồi mới tạo biến thể; lưu vải của bìa tra `ProductColorId` theo tên màu.
  - API mã hàng: danh sách và chi tiết trả thêm `colorImages` (`[{ color, images }]`), chi tiết trả thêm `colors` (các màu của mã hàng); `POST / PUT admin/products` nhận `colorImages` (null = giữ nguyên, rỗng = xóa hết). Job dọn ảnh mồ côi giữ cả ảnh trong `ProductColorImage`.
- **Tài liệu gốc ghi:** DB: màu áo là cột chữ `ProductVariant.Color`, và `ProductMarkerFabric.Color` chép lại tên màu đó. DEV-ALL-28: mã hàng có 1 ảnh chính và tối đa 10 ảnh phụ, không có ảnh theo màu.
- **Code đang làm:**
  - **Màu áo chỉ lưu tên ở `ProductColor`.** Biến thể, vải của bìa và ảnh theo màu đều trỏ `ProductColorId`. Database chặn hai màu trùng tên trong một mã hàng (collation không phân biệt hoa thường), và chặn biến thể hay vải bìa trỏ sang màu của mã hàng khác (FK ghép).
  - Ảnh phụ tối đa **5** (BE `ProductImageLimits.MaxExtra`, FE `MAX_EXTRA_IMAGES`, CHECK database). Mã hàng đang có hơn 5 ảnh phụ thì patch gỡ ảnh thứ 6 trở đi, file ảnh để job dọn ảnh mồ côi xóa.
  - **Ảnh theo màu:** form mã hàng có mục "Ảnh theo màu", mỗi dòng một màu với **1 đến 3 ảnh**. Ô màu gợi ý các màu của mã hàng và màu ở mã hàng khác. Tên trùng màu đã có (không phân biệt hoa thường, bỏ khoảng trắng) thì dùng lại màu đó; chưa có thì tạo `ProductColor` mới, vì mã hàng mới chưa có đơn hàng nào tạo màu. Màu bị bỏ khỏi danh sách mà không có biến thể hay vải bìa (tức chỉ được tạo để khai ảnh) thì xóa luôn. Không trùng màu, không dùng một ảnh cho hai màu, tối đa 30 màu mỗi lần lưu, tên màu tối đa 50 ký tự. Lưu là thay nguyên danh sách ảnh theo màu trong cùng transaction với ảnh phụ; gửi đúng như cũ (kể cả đổi thứ tự màu) thì không ghi lại.
  - Danh sách mã hàng: nhãn "+N" tính cả ảnh theo màu; khung "Xem tất cả ảnh" có thêm phần ảnh theo từng màu. Nhật ký ghi thêm "Số ảnh theo màu".
- **Lý do:** yêu cầu của người dùng (mỗi màu 1-3 ảnh, ảnh phụ còn 5). Lúc đầu ảnh theo màu chép tên màu như `ProductMarkerFabric`; người dùng chỉ ra tên màu sẽ lệch giữa các bảng vì không có khóa ngoại, nên tách màu thành bảng riêng.
- **Ảnh hưởng người khác:** Khiêm (UC10 mã hàng, đơn hàng), Thái (lệnh cắt, bìa cắt, xuất hàng), Minh (giao việc, sản lượng, KPI), Morgan (schema). DB đang chạy phải chạy patch trước khi chạy backend mới (entity không còn `ProductVariant.Color`). Câu SQL tự viết đọc `ProductVariant.Color` phải join `ProductColor`. Các patch dữ liệu demo cũ (`SQL/patches/2026-11-02_demo_*.sql`) còn ghi `ProductVariant.Color`, chỉ chạy được trước patch này.
- **Cần sửa trong tài liệu:** Report-3 UC10 (ảnh phụ tối đa 5, ảnh theo màu, mockup form mã hàng); ERD và `NX-MTD_Database_Giai_thich.md` (bảng `ProductColor`, `ProductColorImage`; `ProductVariant`, `ProductMarkerFabric` bỏ `Color`); `NX-MTD_AI_Context.md` mục 8 dòng "giới hạn 5 ảnh".
- **Trạng thái:** Mở

### DEV-ALL-72 · Mật khẩu mới tối đa 50 ký tự; mọi ô "Lý do" tối đa 500 ký tự

- **Ngày / Người / Task / PR:** 05/11/2026 · Claude (AI) · UC03 (đổi mật khẩu), mọi UC có ô "Lý do" · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** BE: `ChangePasswordDtoValidator` (mật khẩu mới), các validator có ô lý do (đơn hàng, mua hàng, kho, cắt, sản xuất, giao việc, nhân sự, bậc thợ, chấm công, lương, đơn từ), cấu hình EF các cột lý do, tham số `@Reason` của `usp_Attendance_ApplyAdjustment` trong `AttendanceRepository`. FE: `ChangePasswordForm`, các ô nhập lý do. Không đổi schema (các cột lý do vốn đã NVARCHAR(500), `Account.LockReason` vẫn 300).
- **Tài liệu gốc ghi:** `NX-MTD_AI_Context.md` R2: không có luật mật khẩu (chỉ hai lần nhập khớp). Report-3 không ghi độ dài tối đa của ô lý do.
- **Code đang làm:**
  - **Mật khẩu mới** tối đa **50** ký tự (trước đây 100 ở BE); không có độ dài tối thiểu. Đăng nhập vẫn không giới hạn trên để tài khoản cũ còn vào được.
  - **Lý do** tối đa **500** ký tự ở mọi nơi, lấy từ `TextLimits.Reason` (BE) và `REASON_MAX_LENGTH` (FE) thay cho số 500 rải rác. Lý do của đơn từ (`RequestDtoValidators`) giảm từ 1.000 xuống 500. Ngoại lệ giữ nguyên: lý do khóa tài khoản 300 (cột `Account.LockReason`), lý do cắt thiếu 300 (nối vào ghi chú lệnh cắt, DEV trước đó).
  - Tham số `@Reason` của `usp_Attendance_ApplyAdjustment` ở `AttendanceRepository` lấy theo `TextLimits.Reason` (trước đây số 500 viết cứng).
- **Lý do:** yêu cầu của người dùng (kiểm tra giá trị biên của Search, Lý do, Mật khẩu). Mật khẩu được băm BCrypt (work factor 11) nên độ dài tối đa 50 không ảnh hưởng cột `PasswordHash`.
- **Ảnh hưởng người khác:** Khiêm (đơn từ UC, đơn hàng), Thái (kho, cắt), Minh (sản xuất, giao việc, chấm công, lương). Đơn từ cũ có lý do dài hơn 500 vẫn đọc được nhưng không sửa lại được nếu không rút ngắn.
- **Cần sửa trong tài liệu:** Report-3 UC03 (thêm "mật khẩu tối đa 50 ký tự"), các UC có ô lý do (giới hạn 500 ký tự); `NX-MTD_AI_Context.md` mục R2.
- **Trạng thái:** Mở

### DEV-ALL-73 · Công đoạn phải có đơn giá được duyệt mới giao được cho công nhân

- **Ngày / Người / Task / PR:** 05/11/2026 · Claude (AI) · UC53, UC55, UC60 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** BE: `OperationAssignmentService` (phân công một / nhiều công đoạn, sao chép phân công), `WorkAllocationService` (giao việc), `DailyWorkAllocationService` (giao theo ngày), lớp mới `AllocationRateCheck`, thông báo `Messages.AllocationPieceRateMissing`. Không đổi schema, không đổi API (lỗi trả 409, mã `PIECE_RATE_NOT_APPROVED` đã có).
- **Tài liệu gốc ghi:** Report-3 MSG18: chưa có đơn giá duyệt thì không nhập được sản lượng. Giao việc / phân công không đòi đơn giá.
- **Code đang làm:** công đoạn chưa có đơn giá **Approved** hiệu lực đến ngày giao (Nháp, Chờ duyệt, Từ chối, hoặc đơn giá bắt đầu sau ngày giao không tính) thì không phân công, không giao (phiếu giao, giao theo ngày với số lượng > 0) được; thông báo nêu mã công đoạn thiếu đơn giá. Giảm số giao và thu hồi không bị chặn. Sao chép phân công từ đơn khác bỏ qua công đoạn chưa có đơn giá. Ngày giao tính theo hôm nay (phân công, phiếu giao) hoặc ngày làm việc của phiếu (giao theo ngày).
- **Lý do:** yêu cầu của người dùng. Tránh giao việc rồi mới biết không nhập được sản lượng (MSG18).
- **Ảnh hưởng người khác:** Minh (đơn giá công đoạn, giao việc, sản lượng), Thái (tổ may). Công đoạn mới tạo phải được Admin duyệt đơn giá trước khi tổ trưởng giao việc.
- **Cần sửa trong tài liệu:** Report-3 UC53 / UC55 / UC60 (điều kiện trước: công đoạn đã có đơn giá duyệt), MSG18.
- **Trạng thái:** Mở

### DEV-ALL-74 · Họ tên nhân viên chỉ có chữ cái; ô mã chặn ký tự không cho phép

- **Ngày / Người / Task / PR:** 05/11/2026 · Claude (AI) · UC15, UC16, UC18 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** BE: `PersonNameRule`, `CreateEmployeeDtoValidator`, `AdminUpdateEmployeeDtoValidator`, `ManagerUpdateEmployeeDtoValidator` (kể cả nhập Excel vì dùng chung validator). FE: `utils/textInput.ts`; ô Họ tên, Mã nhân viên (`EmployeeFormModal`), Mã tổ (`TeamFormModal`), Mã công đoạn (`OperationFormModal`), Mã bậc thợ (`GradeCodeFormModal`). Dữ liệu mẫu `SQL/NX-MTD_Database.sql` và patch `SQL/patches/2026-11-05_employee_name_letters_only.sql` (đổi 5 tên mẫu có số hoặc `/` sang chữ).
- **Tài liệu gốc ghi:** Report-3 không ghi ký tự được phép trong họ tên.
- **Code đang làm:** họ tên gồm chữ cái (kể cả tiếng Việt có dấu) và một dấu cách giữa các từ, tối đa 100 ký tự; không số, không dấu gạch, chấm, nháy, `/`. BE trả 400 kèm thông báo; FE bỏ ký tự không hợp lệ ngay khi gõ hoặc dán và vẫn báo lỗi nếu còn sai. Ô mã chỉ nhận chữ, số và `. _ -` (mã bậc thợ chỉ chữ và số, tối đa 5), khớp quy tắc BE đã có. Ô số điện thoại và ô số lượng đã có bộ lọc từ trước. Nhân viên cũ có tên chứa số hay ký tự đặc biệt vẫn xem được nhưng phải sửa tên khi lưu lại.
- **Lý do:** yêu cầu của người dùng.
- **Ảnh hưởng người khác:** Khiêm (nhân viên, nhập Excel): file Excel có họ tên chứa số hoặc ký tự đặc biệt sẽ báo lỗi ở dòng đó. Morgan (dữ liệu mẫu).
- **Cần sửa trong tài liệu:** Report-3 UC15 / UC16 / UC18 (quy tắc họ tên), mẫu Excel nhập nhân viên.
- **Trạng thái:** Mở

### DEV-ALL-75 · Chi tiết đơn hàng hiện số đã giao

- **Ngày / Người / Task / PR:** 05/11/2026 · Claude (AI) · UC09 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** BE: `OrderLineDto.DeliveredQty` (chi tiết Admin), `OrderOverviewLineDto` mới thay cho `OrderLineLookupDto` trong `OrderOverviewDto` (chi tiết Quản đốc), `OrderRepository.GetDetailAsync` đọc `vw_OrderLineProgress.DeliveredQty`. FE: `OrderReadOnlyGrid`, `OrderSummary`, `orderGrid.ts`. Không đổi schema.
- **Tài liệu gốc ghi:** Report-3 UC09 chi tiết đơn không có số đã giao (tiến độ giao nằm ở Trang chủ).
- **Code đang làm:** lưới số lượng của đơn có thêm cột Đã giao và Còn phải giao theo từng màu; phần thông tin chung có "Đã giao / Tổng SL". Số đã giao = tổng số xuất của phiếu xuất ở trạng thái `DELIVERED` (DEV-ALL-70). Admin và Quản đốc đều thấy (không phải dữ liệu mật).
- **Lý do:** yêu cầu của người dùng.
- **Ảnh hưởng người khác:** Khiêm (UC09, UC21), Thái (xuất hàng).
- **Cần sửa trong tài liệu:** Report-3 UC09 (mockup chi tiết đơn).
- **Trạng thái:** Mở

### DEV-ALL-76 · Ngừng hợp tác với khách hàng / nhà cung cấp không bắt buộc ghi lý do

- **Ngày / Người / Task / PR:** 05/11/2026 · Claude (AI) · UC06, UC38 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** BE: `DiscontinueCustomerDto`, `DiscontinueSupplierDto` (`Reason` nhận null), validator bỏ bắt buộc, `CustomerService`, `SupplierService`. FE: hộp nhập lý do ở `CustomersPage`, `SuppliersPage` dùng `NoteModal` không bắt buộc. Không đổi schema (cột `DiscontinueReason` vốn cho NULL).
- **Tài liệu gốc ghi:** `NX-MTD_Database.sql` ghi lý do ngừng hợp tác bắt buộc; Report-3 MSG09.
- **Code đang làm:** lý do để trống (hoặc toàn khoảng trắng) vẫn ngừng được, `DiscontinueReason` lưu NULL và nhật ký không có lý do; nhập thì tối đa 500 ký tự (DEV-ALL-72).
- **Lý do:** yêu cầu của người dùng.
- **Ảnh hưởng người khác:** Khiêm (khách hàng), Thái (nhà cung cấp).
- **Cần sửa trong tài liệu:** Report-3 UC khách hàng / nhà cung cấp (bỏ MSG09 ở bước ngừng hợp tác), `NX-MTD_Database.sql` chú thích cột `DiscontinueReason`.
- **Trạng thái:** Mở

### DEV-ALL-77 · Ngày làm việc trong thời gian gia công; validate đơn Xin NVL / BTP; đề xuất khi cấp vải / BTP

- **Ngày / Người / Task / PR:** 06/11/2026 · Claude (AI) · UC40, UC53, UC55, UC56, UC57 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:**
  - BE: `ProductionGuard.BeforeOrderStart`, `DefectService`, `OutputService`, `QcBatchService`, `DailyWorkAllocationService` (kiểm ngày); `RequisitionRequestHandlers` (đơn Xin NVL / Xin BTP, đăng ký ở `WarehouseModule`, mã lỗi `REQUEST_LINES_INVALID`); `WorkOrderService.GetFabricNeedsAsync` và API mới `GET warehouse-keeper/work-orders/{id}/fabric-needs?color=&sizeCode=`. Không đổi schema.
  - FE: bộ chọn ngày ở form hàng hỏng, giao việc theo ngày, sản lượng cuối ca (`useWorkDateRange`); form đơn xin (`RequestFormModal`); thanh đề xuất ở `IssueCreateModal` (`FabricSuggestionBar`, `PieceSuggestionBar`).
- **Tài liệu gốc ghi:** Report-3 chỉ cấm ngày chưa tới (MSG ngày tương lai); đơn Xin NVL / BTP dùng form chung, chỉ kiểm số lượng > 0.
- **Code đang làm:**
  - **Thời gian gia công:** ngày làm việc của phiếu hàng hỏng, sản lượng cuối ca, đợt QC và giao việc theo ngày phải từ ngày nhận đơn trở đi và không quá hôm nay; sai thì báo "Ngày … trước ngày nhận đơn …". Ô chọn ngày ở FE chặn cùng khoảng này sau khi chọn đơn.
  - **Đơn Xin NVL / Xin BTP:** phải có 1 đến 50 dòng, không trùng; ngày cần không trước hôm nay khi tạo mới. **Xin NVL:** mỗi dòng phải chọn NVL trong danh mục đang dùng (không còn gõ tên tự do); **vải** nhập số lẻ tối đa 2 chữ số sau dấu phẩy, **NVL khác vải** (chỉ, cúc, phụ liệu…) chỉ nhập số nguyên, tối đa 9.999.999 mỗi dòng; đổi sang NVL không phải vải thì số đã nhập tự làm tròn. **Xin BTP:** số nguyên từ 1 đến 1.000.000. Loại đơn khác (linh kiện, máy…) giữ cách nhập cũ (lẻ tối đa 3 chữ số). Đơn Xin NVL cũ chỉ có tên hàng (chưa gắn NVL) phải chọn lại NVL khi sửa.
  - **Đề xuất khi lập phiếu cấp:** cấp BTP: chọn mã hàng + màu (+ size) rồi "Thêm dòng đề xuất" thêm các dòng đơn khớp, số cấp = nhỏ hơn của khả dụng trong kho BTP và số đặt, bỏ dòng hết hàng hoặc đã có. Cấp vải: chọn màu (+ size) của lệnh cắt rồi "Đề xuất số cấp" điền số mét cần (tính theo bìa / định mức, cùng bộ tính cảnh báo vải của lệnh cắt), không vượt khả dụng trong kho; Thủ kho vẫn sửa được trước khi lưu.
- **Lý do:** yêu cầu của người dùng. Tổ trưởng may chọn được ngày nhiều năm trước khi ghi hàng hỏng và giao sản lượng; số lượng đơn xin không bị chặn đúng kiểu.
- **Ảnh hưởng người khác:** Minh (sản xuất, QC, giao việc), Thái (kho, cắt, đơn xin).
- **Cần sửa trong tài liệu:** Report-3 UC53 / UC55 / UC56 / UC57 (khoảng ngày hợp lệ), UC40 (đề xuất số cấp), đơn Xin NVL / Xin BTP (quy tắc dòng và số lượng).
- **Trạng thái:** Mở

### DEV-ALL-78 · Giao sản lượng công đoạn theo ngày: chỉ sửa trong ngày, sang ngày sau chỉ xem

- **Ngày / Người / Task / PR:** 06/11/2026 · Claude (AI) · UC53, UC60 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** BE: `DailyWorkAllocationService.SaveAsync`, `WarehouseMessages.DailyDateMustBeToday` (thay `DailyDateInFuture`). FE: `DailyAllocationBoard`, `DailyWorkerModal`, `dailyModalColumns`. Không đổi schema, không đổi API.
- **Tài liệu gốc ghi:** DEV-ALL-63: "Chỉ giao cho hôm nay hoặc ngày đã qua" (cho giao bù ngày cũ).
- **Code đang làm:** `PUT team-leader/work-allocations/daily` chỉ nhận `workDate` = hôm nay (ngày đã qua hoặc chưa tới trả 409 `WAREHOUSE_INVALID_REFERENCE`, thông báo "Chỉ giao và sửa việc trong ngày hôm nay..."). `GET daily` vẫn xem được mọi ngày. Trên màn Giao sản lượng theo ngày, chọn ngày đã qua thì có thông báo "chỉ xem", các ô nhập số giao và nút Lưu bị khóa. Bộ chọn ngày vẫn chỉ cho từ ngày nhận đơn đến hôm nay (DEV-ALL-77).
- **Lý do:** yêu cầu của người dùng: tổ trưởng không được giao hay sửa sản lượng cho quá khứ; sang ngày hôm sau chỉ còn xem.
- **Ảnh hưởng người khác:** Thái (TH-19 giao việc), Minh (UC55 sản lượng cuối ca: phần chuyển sang hôm sau vẫn tự tính, không phụ thuộc việc sửa ngày cũ).
- **Cần sửa trong tài liệu:** Report-3 UC53 / UC60 (điều kiện: chỉ giao trong ngày); DEV-ALL-63 dòng "ngày đã qua" nay thay bằng mục này.
- **Trạng thái:** Mở

### DEV-ALL-79 · Phiếu lỗi thêm loại "Nhập sai sản lượng"; màn Hàng hỏng đổi tên thành Phiếu lỗi / điều chỉnh

- **Ngày / Người / Task / PR:** 06/11/2026 · Claude (AI) · UC56, UC55, UC59 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:**
  - Schema: `CK_DR_Type` thêm `'OUTPUT_ERR'` (cột `DefectType` là VARCHAR(10) nên mã loại là `OUTPUT_ERR`); `trg_WorkAllocation_Check`, `trg_OutputRecord_Check`, `vw_WorkerOutputCap`, `vw_OperationAllocationProgress`, `vw_WorkerDailyEstimatedPay` tính sản lượng hiệu lực = Σ `OutputRecord` − Σ phiếu `OUTPUT_ERR` chưa hủy. Patch `SQL/patches/2026-11-06_defect_output_error.sql` (đã chạy thử trên DB tạm: dựng mới và nâng cấp từ bản cũ, chạy lại không lỗi); `SQL/NX-MTD_Database.sql` đã cập nhật.
  - BE: `DefectTypes.OutputError` / `DefectTypes.All`, `DefectService` (ghi phiếu, hủy phiếu), validator `CreateDefectDto` / `DefectQuery`, `Messages`, `AuditText`. API không đổi, `defectType` nhận thêm `OutputErr`.
  - FE: `DefectFormModal`, `defectText.ts`, `types.ts`, `auditLabels.ts`; tên màn ở menu, tiêu đề trang, thông báo.
- **Tài liệu gốc ghi:** UC56: phiếu lỗi có hai loại Rework (không trừ sản lượng) và Scrap (trừ tiền theo đơn giá khóa); màn tên "Hàng hỏng".
- **Code đang làm:**
  - **Loại mới "Nhập sai sản lượng"** (`OUTPUT_ERR`): tổ trưởng may chọn công nhân, dòng đơn, công đoạn, **ngày đã nhập sai** và số lượng cần trừ. Phiếu **không sửa bản ghi sản lượng cũ** mà trừ bên ngoài (phiếu điều chỉnh âm): sản lượng của công nhân = đã nhập − Σ phiếu. Hệ quả: trần sản lượng còn lại của công nhân tăng lên (nhập lại số đúng được), số "đã làm" của công đoạn giảm nên số còn phải làm / BTP còn trên chuyền tính lại, tiền sản phẩm trong lương tạm tính giảm theo đơn giá duyệt khóa lúc ghi (như Scrap) và tiền Chủ nhật tính trên phần còn lại.
  - **Ràng buộc:** bắt buộc ghi chú lý do nhập sai (mã lý do tự đặt là Khác); phải có đơn giá duyệt (MSG18); số trừ không vượt sản lượng công nhân đã nhập đúng công đoạn trong **ngày đó** trừ các phiếu nhập sai đã ghi (không tính phiếu đã hủy); hủy phiếu chỉ được khi công nhân còn chỗ dưới trần số lần được giao (hủy trả sản lượng lại). Công nhân vẫn được thông báo và phản hồi như các loại khác; hạn sửa, khóa tháng giữ nguyên.
  - **Không đổi:** tỉ lệ lỗi, `ReworkQty` / `ScrapQty`, tồn BTP trong kho, cắt bù và các số liệu hàng hỏng thật không tính loại này (không phải hàng lỗi vật lý, hàng không rời xưởng). NVL: hệ thống không có số NVL nào suy ra từ sản lượng công đoạn nên không có số NVL để cộng lại.
  - **Tên màn hình:** "Hàng hỏng" thành "Phiếu lỗi / điều chỉnh" (Quản đốc: "Phiếu lỗi / điều chỉnh của các tổ"; công nhân: "Phiếu lỗi / điều chỉnh của tôi"). Đường dẫn `/defect-records` giữ nguyên.
- **Lý do:** yêu cầu của người dùng: tổ trưởng nhập sai sản lượng công đoạn cần một đường chính thức để trừ sản lượng của công nhân (sau hạn sửa, hoặc khi không muốn sửa bản ghi cũ) và tính lại số liệu BTP.
- **Ảnh hưởng người khác:** Minh (UC55 sản lượng, UC56 phiếu lỗi), Kiều (lương tạm tính UC59, view `vw_WorkerDailyEstimatedPay`), Morgan (schema). DB đang chạy phải chạy patch trước khi chạy backend mới, nếu không ghi phiếu loại mới sẽ vướng `CK_DR_Type`.
- **Cần sửa trong tài liệu:** Report-3 UC56 (loại phiếu thứ ba, tên màn hình), UC55 / UC59 (sản lượng hiệu lực), `NX-MTD_AI_Context.md` mục `DefectRecord.DefectType`; `NX-MTD_Database_Mo_ta.md` bảng `DefectRecord`, các view nêu trên.
- **Trạng thái:** Mở

### DEV-ALL-80 · Bỏ ảnh theo màu của mã hàng

- **Ngày / Người / Task / PR:** 06/11/2026 · Claude (AI) · UC10 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:**
  - Schema: bỏ bảng `ProductColorImage` (patch `SQL/patches/2026-11-06_drop_product_color_image.sql`, đã chạy thử trên DB tạm: dựng mới và nâng cấp từ bản cũ, chạy lại không lỗi); `SQL/NX-MTD_Database.sql`, `docs/NX-MTD_Database.dbml`, `docs/NX-MTD_Database_Mo_ta.md` đã cập nhật. Bảng `ProductColor` (nơi duy nhất lưu tên màu) và `ProductImage` (tối đa 5 ảnh phụ) giữ nguyên.
  - BE: bỏ entity / cấu hình `ProductColorImage`, `ProductColorImagesDto`, trường `colorImages` ở `ProductListItemDto`, `ProductDetailDto`, `SaveProductDto`; `ProductService`, validator, `ProductImageCleanupService` (không còn tính ảnh theo màu là ảnh đang dùng); bỏ các thông báo liên quan. `ProductDetailDto.colors` (các màu đã có biến thể) vẫn trả như cũ.
  - FE: bỏ `ColorImagesField`, `ColorImagesRow`, mục "Ảnh theo màu" ở form mã hàng, ở khung xem tất cả ảnh, số "+N" ở cột ảnh danh sách mã hàng chỉ còn đếm ảnh phụ.
- **Tài liệu gốc ghi:** DEV-ALL-71: mỗi màu áo có 1-3 ảnh (`ProductColorImage`); mã hàng có 1 ảnh chính và tối đa 5 ảnh phụ.
- **Code đang làm:** mã hàng chỉ còn 1 ảnh chính và tối đa 5 ảnh phụ, không có ảnh theo màu. Phần "ảnh theo màu" của DEV-ALL-71 không còn hiệu lực (phần tách màu thành `ProductColor` vẫn giữ). Dữ liệu ảnh theo màu đã nhập bị xóa cùng bảng; file ảnh trên ổ lưu trữ do job dọn ảnh mồ côi xóa sau 24 giờ.
- **Lý do:** yêu cầu của người dùng.
- **Ảnh hưởng người khác:** Khiêm (UC10 mã hàng), Morgan (schema). DB đang chạy phải chạy patch trước hoặc sau khi chạy backend mới đều được (backend mới không còn đọc bảng này); chạy patch thì mất dữ liệu ảnh theo màu.
- **Cần sửa trong tài liệu:** Report-3 UC10 (bỏ ảnh theo màu, mockup form mã hàng); ERD và `NX-MTD_Database_Giai_thich.md` (bỏ `ProductColorImage`); `NX-MTD_AI_Context.md` mục 8.
- **Trạng thái:** Mở

### DEV-ALL-81 · Dừng đơn chặn khi hàng đã xuất hết; hủy / dừng đơn tự hủy phiếu xuất, lệnh cắt, KPI đang chờ

- **Ngày / Người / Task / PR:** 07/11/2026 · Claude (AI) · UC09, UC49 (phiếu xuất hàng) · nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** `CustomerOrder.Status`, `DispatchNote.Status`, mã lỗi `ORDER_FULLY_DISPATCHED`. Không đổi schema.
- **Tài liệu gốc ghi:** hủy / dừng đơn không nói gì về phiếu xuất hàng của đơn.
- **Code đang làm:**
  - **Dừng đơn** (đơn `IN_PRODUCTION` / `IN_DELIVERY` / `PARTIALLY_DELIVERED`, kể cả đã giao một phần) bị chặn khi tổng số của các phiếu `IN_DELIVERY` + `DELIVERED` ≥ tổng số đặt: trả `ORDER_FULLY_DISPATCHED` (409). Lúc đó không còn phần nào để dừng; khách nhận xong thì đơn tự sang `DELIVERED` (GBR-23). Phiếu `PENDING`, `REJECTED`, `CANCELLED` không tính.
  - **Hủy hoặc dừng đơn tự hủy mọi phiếu xuất `PENDING` của đơn** (cùng lần lưu), ghi lịch sử từng phiếu và báo Thủ kho. Phiếu `IN_DELIVERY` / `DELIVERED` giữ nguyên. Vì dừng chỉ được khi còn phần chưa xuất, đơn `STOPPED` có phiếu `IN_DELIVERY` đang giao vẫn xác nhận giao được nhưng giữ `STOPPED` (không thể thành `DELIVERED` vì số xuất chưa đủ).
  - **Hủy hoặc dừng đơn cũng xử lý lệnh cắt và KPI:** lệnh cắt chưa làm gì (`OPEN`, chưa có bàn cắt, chưa có phiếu cấp vải trừ phiếu bị từ chối) thì **hủy**; lệnh đã có việc (có bàn cắt / phiếu cấp, hoặc đang `CUTTING`) thì **đóng** (`COMPLETED`, ghi lý do vào ghi chú) nếu đủ điều kiện như Quản đốc đóng tay (hết phiếu bàn giao / trả vải chờ kho, BTP đã cắt đã vào kho hết; điều kiện dùng chung `WorkOrderFinishCheck`), chưa đủ thì để nguyên và báo Quản đốc đóng sau. Báo tổ trưởng / tổ phó tổ cắt. Chỉ tiêu KPI `ACTIVE` của đơn thì hủy, báo tổ may. Ghi lịch sử từng bản ghi (`OrderCloseCascade`).
  - **Giữ nguyên:** lệnh cắt chưa đủ điều kiện đóng, KPI đã đóng kỳ, phiếu cấp NVL và phiếu bàn giao thành phẩm / bán thành phẩm chờ xác nhận (hàng đã rời tay người giao), đơn từ đang chờ duyệt, đơn mua NVL, giao việc theo ngày (đã bị chặn lập mới khi đơn đóng).
- **Lý do:** yêu cầu của người dùng: phiếu `PENDING` bị treo khi đơn đóng; đơn đã xuất hết hàng thì không nên dừng.
- **Ảnh hưởng người khác:** Thủ kho (phiếu xuất), tổ trưởng / tổ phó tổ cắt (lệnh cắt), tổ trưởng / tổ phó tổ may (KPI) nhận thông báo khi bản ghi bị hủy tự động.
- **Cần sửa trong tài liệu:** Report-3 UC09 (điều kiện dừng đơn), UC49 (phiếu xuất tự hủy), UC47 (lệnh cắt tự hủy / đóng), UC19 (KPI tự hủy); `NX-MTD_AI_Context.md` mục trạng thái đơn hàng.
- **Trạng thái:** Mở

### DEV-ALL-82 · Ngày chứng từ kho, ngày dự kiến giao hàng, kiểm trả vải thừa

- **Ngày / Người / Task / PR:** 07/11/2026 · Claude (AI) · UC41–UC46, UC09 · nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:**
  - Schema: thêm `OrderMaterial.ExpectedDeliveryDate`, `PurchaseOrder.ExpectedDeliveryDate` (DATE, NULL) + `CK_OM_DeliveryDate`, `CK_PO_DeliveryDate`. Patch `SQL/patches/2026-11-07_expected_delivery_date.sql` (chạy lại không lỗi); `SQL/NX-MTD_Database.sql`, `docs/NX-MTD_Database.dbml`, `docs/NX-MTD_Database_Mo_ta.md` đã cập nhật. Chưa chạy thử patch trên DB thật.
  - BE: `DocumentDateRules` (dùng chung), `MaterialIssueService`, `MaterialReceiptService`, `StocktakeService`, `OrderMaterialService`, `PurchaseOrderService`, `PurchaseOrderFlowService`, `PurchaseDeliveryDate`, `ShortDeliveryService` (xóa ngày khi khách cấp về 0), `MaterialReturnService`, validators, DTO, mã lỗi `RETURN_PENDING_EXISTS`, `RETURN_EXCEEDS_LEFTOVER`.
  - FE: form tạo đơn mua, form đặt hàng, form NVL của đơn (từng dòng và sửa cả đơn), cột ngày ở danh sách NVL của đơn và màn NVL chờ nhận, chi tiết đơn mua.
- **Tài liệu gốc ghi:** không có quy tắc cho ngày phiếu cấp / phiếu nhập (kiểm kê chỉ cấm ngày tương lai); đơn mua và NVL khách cấp chỉ có số lần giao dự kiến (23/10 đã bỏ ngày dự kiến); trả vải thừa chỉ kiểm số trả ≤ số còn lại của dòng phiếu cấp.
- **Code đang làm:**
  - **Ngày phiếu cấp, ngày phiếu nhập (kể cả phiếu bổ sung), ngày kiểm kê:** không sau hôm nay, không trước hôm nay quá 7 ngày (`DocumentDateRules.MaxBackDays`), và (phiếu cấp, phiếu nhập có đơn) không trước ngày nhận đơn. Phiếu nhập theo đơn mua còn không trước ngày đặt đơn mua. Lỗi trả 400 (validation).
  - **Ngày dự kiến giao:** bắt buộc khi tạo đơn mua, khi đặt hàng đơn chờ chốt và khi NVL của đơn có khách cấp (`CustomerQty > 0`); khách không cấp thì phải để trống. Không trước hôm nay, không trước ngày đặt (đơn mua), không sau hạn giao của đơn hàng (nếu đơn mua / NVL gắn đơn). Ngày giữ nguyên khi sửa thì không kiểm lại (sửa ghi chú đơn cũ vẫn được). Số lần giao dự kiến giữ nguyên. Dữ liệu cũ để trống ngày.
  - **Trả vải thừa:** mỗi dòng phiếu cấp chỉ có một phiếu trả đang chờ Thủ kho xác nhận (`RETURN_PENDING_EXISTS`); tổng số trả theo (đơn, vải) không vượt số vải còn thừa theo số đã cắt, cộng các phiếu cấp cùng đơn, cùng vải (`RETURN_EXCEEDS_LEFTOVER`), không chặn khi còn bàn cắt chưa nhập chiều dài. Giữ nguyên: số trả ≤ còn lại của dòng, phiếu cấp phải `RECEIVED`, số xác nhận ≤ số tổ trả, lệch phải có lý do.
- **Lý do:** yêu cầu của người dùng.
- **Ảnh hưởng người khác:** Thủ kho (lập phiếu cấp / nhập / kiểm kê, trả vải), Quản đốc (đặt hàng, khai NVL khách cấp), tổ cắt (trả vải), Morgan (schema). DB đang chạy phải chạy patch trước khi chạy backend mới, nếu không đọc / ghi đơn mua và NVL của đơn sẽ lỗi cột.
- **Cần sửa trong tài liệu:** Report-3 UC41–UC46 (quy tắc ngày, trả vải thừa), UC09 và UC23 (ngày dự kiến giao); ERD và `NX-MTD_Database_Giai_thich.md` (hai cột và hai CHECK mới); `NX-MTD_AI_Context.md` mục `PurchaseOrder`, `OrderMaterial`.
- **Trạng thái:** Mở

### DEV-ALL-83 · Kiểm biển số, xăng, tiền xăng; đơn xin của Cơ điện chỉ số nguyên có trần

- **Ngày / Người / Task / PR:** 07/11/2026 · Claude (AI) · UC35, đơn xin của Cơ điện · nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** không đổi schema. BE: `VehiclePlate`, `VehicleLimits`, `VehicleDtoValidators`, `MachineRequestHandlers` (đăng ký ở `RequestModule`), `Messages`, `WarehouseMessages`. FE: `features/vehicle-log` (`VehicleFormModal`, `TripCompleteModal`, `TripModals`, `constants`), `features/requests` (`lineQuantity`, `constants`).
- **Tài liệu gốc ghi:** biển số chỉ cần duy nhất; xăng / tiền xăng chỉ kiểm ≥ 0; đơn xin linh kiện / máy / vật tư nhận số lẻ đến 3 chữ số, trần chung 9.999.999, không bắt buộc có dòng.
- **Code đang làm:**
  - **Biển số:** đúng mẫu ô tô (mã tỉnh 2 số, 1-2 chữ cái, gạch ngang, 4-5 số, biển mới có dấu chấm trước 2 số cuối), ví dụ `51C-123.45`, `30F-1234`; tự viết hoa, vẫn duy nhất. Biển đã lưu không đúng mẫu vẫn dùng được, chỉ bị kiểm khi sửa xe.
  - **Số xăng:** lớn hơn 0, tối đa 1.000 lít, tối đa 2 số lẻ. **Tiền xăng:** số nguyên đồng lớn hơn 0, tối đa 100.000.000 ₫. Phải nhập cả hai hoặc để trống cả hai (0 không còn hợp lệ). **Số km** (đầu, cuối) tối đa 9.999.999; **định mức xăng** tối đa 2 số lẻ.
  - **Đơn xin của Cơ điện:** linh kiện và vật tư máy là số nguyên 1-9.999 mỗi dòng, máy mới số nguyên 1-99 mỗi dòng; bắt buộc có ít nhất một dòng, tối đa 50 dòng, không trùng tên hàng, ngày cần không ở quá khứ (dùng chung kiểm của đơn Xin NVL / BTP).
- **Lý do:** yêu cầu của người dùng.
- **Ảnh hưởng người khác:** Kế toán (nhập xe, chuyến xe), Cơ điện (đơn xin), tester (dữ liệu mẫu hiện có vẫn hợp lệ).
- **Cần sửa trong tài liệu:** Report-3 UC35 (quy tắc biển số, xăng, tiền xăng) và mục đơn xin của Cơ điện (số nguyên, trần).
- **Trạng thái:** Mở

### DEV-ALL-84 · Số suất ăn của tổ không vượt số người trong tổ

- **Ngày / Người / Task / PR:** 07/11/2026 · Claude (AI) · UC30 · nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:** không đổi schema. BE: `MealService.ConfirmTeamMealAsync`, `IMealRepository` / `MealRepository` (`GetTeamMemberCountAsync`), `ErrorCodes.MealCountExceedsMembers`, `Messages`.
- **Tài liệu gốc ghi:** tổ trưởng xác nhận số suất, chỉ kiểm ≥ 0.
- **Code đang làm:** số suất xác nhận phải ≤ số người đang làm (`Status = ACTIVE`) của tổ, vượt thì trả 409 `MEAL_COUNT_EXCEEDS_MEMBERS`. Suất do job tự lấy từ chấm công thì không cần kiểm (chấm công không vượt số người).
- **Lý do:** yêu cầu của người dùng, tránh đặt dư suất.
- **Ảnh hưởng người khác:** Tổ trưởng (xác nhận suất ăn), Kế toán (tổng suất).
- **Cần sửa trong tài liệu:** Report-3 UC30 (quy tắc kiểm số suất).
- **Trạng thái:** Mở

### DEV-ALL-85 · Bìa cắt theo đơn: ước mét vải = số lớp × chiều dài bìa

- **Ngày / Người / Task / PR:** 09/11/2026 · Claude (AI) · UC10, UC49–UC51 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:**
  - Schema: `ProductMarker` thành bìa của đơn (thêm `OrderId`, `MarkerCode`, `LengthM`, `Status`; bỏ `SizeCode`, `LengthCm`, `WidthCm`; `UQ_PM` thành (OrderId, ProductId, MarkerNo, MarkerCode)); bảng mới `ProductMarkerRatio` (tỉ lệ size của bìa), `ProductMarkerComponent` (chi tiết trên nhóm bìa); `ProductComponentMaterial` bỏ `LengthCm`, `WidthCm`, `CK_PCM_Size`; `Material` bỏ `WidthCm` (khổ vải), `CK_Material_Size`: **khổ vải cố định 1,5 m**, cách ước theo bìa không cần khổ nên backend không còn hằng số khổ; bỏ `vw_RecutFabricNeed`, `vw_ProductLastSizeRatio`; trigger mới `trg_ProductMarker_Check`, `trg_ProductMarkerRatio_Check` (mã lỗi 50055). `ProductMarkerFabric` giữ nguyên, `MarkerNo` nay là nhóm bìa.
  - Patch `SQL/patches/2026-11-09_marker_by_order.sql`; `SQL/NX-MTD_Database.sql`, `docs/NX-MTD_Database.dbml`, `docs/NX-MTD_Database_Mo_ta.md` đã cập nhật. Đã kiểm trên 2 DB tạm (dựng mới; dựng từ script cũ rồi chạy patch 2 lần): cấu trúc và dữ liệu bìa của 5 bảng giống nhau, 10 ca kiểm ràng buộc / trigger đúng; DB tạm đã xóa.
  - Backend: entity `ProductMarker` (OrderId, MarkerNo, MarkerCode, LengthM, Status), `ProductMarkerRatio`, `ProductMarkerComponent`; bỏ `Material.WidthCm`, kích thước chi tiết, 2 view, `PieceFabricEstimator`. Định mức mã hàng (`ProductSpecService`) nhận `MarkerComponents` (nhóm × chi tiết) thay cho kích thước bìa và kiểm: không trùng, chi tiết là PANEL, nhóm có cả chi tiết và vải, mọi PANEL thuộc ít nhất một nhóm, vải có trong định mức chi tiết cắt của màu, không bỏ nhóm đang có bìa của đơn. API mới `GET api/v1/manager/order-markers/{orderId}`, `PUT .../{orderId}/products/{productId}` (`OrderMarkerService`, Admin dùng chung API manager): gửi đủ danh sách bìa của một mã hàng, đơn đã đóng không sửa, mã bìa không trùng trong nhóm, nhóm phải đã khai vải, size phải có trong đơn; mã lỗi `ORDER_MARKER_INVALID`. Ước vải lệnh cắt (`WorkOrderFabricChecker` + `MarkerFabricEstimator`) và kiểm vải cắt bù (`WorkOrderService.CheckRecutFabricAsync`) theo bìa của đơn; bìa dư quá `CUT_SURPLUS_PERCENT` thì cảnh báo lệch tỉ lệ (trạng thái `Enough` kèm ghi chú).
  - Frontend: định mức mã hàng tab "Nhóm bìa" (chọn chi tiết cắt và vải theo màu cho từng nhóm; số nhóm giữ cố định khi xóa nhóm khác vì bìa của đơn trỏ theo số nhóm); bỏ cột dài / rộng chi tiết, ô khổ vải ở NVL và cột khổ ở NVL của đơn; chi tiết đơn (Admin, Quản đốc) có tab "Bìa cắt": mỗi mã hàng một bảng bìa (nhóm, mã, dài m, số sản phẩm theo từng size của đơn, trạng thái); cảnh báo vải lệnh cắt có nhóm "Bìa lệch tỉ lệ lệnh cắt".
  - Test: backend 2115 test qua (thêm `MarkerFabricEstimatorTests`, `OrderMarkerServiceTests`, viết lại `WorkOrderFabricCheckTests`), frontend 593 test qua (thêm `orderMarker.test.ts`, viết lại `markerSpec.test.ts`), typecheck, lint, build qua.
  - Chưa làm: xóa dòng đơn / bỏ mã hàng khỏi đơn khi đã có bìa không bị chặn; tỉ lệ của size đã bỏ nằm lại trên bìa nhưng không ảnh hưởng ước vải (size không cần thì không tính) và bị bỏ ở lần lưu bìa sau.
- **Tài liệu gốc ghi:** DEV-ALL-42: bìa thuộc mã hàng, mỗi dòng là một bìa ở một size (dài × rộng cm); mét vải tính bằng xếp hình chữ nhật lên khổ vải; cắt bù tính theo kích thước chi tiết.
- **Code đang làm:**
  - Bìa do phía trên giác theo tỉ lệ size của từng đơn: mỗi bìa thuộc đơn × mã hàng × nhóm bìa, có chiều dài (m) và tỉ lệ size (`PiecesPerPly`). Nhóm bìa, vải theo màu áo (`ProductMarkerFabric`) và chi tiết trên nhóm bìa (`ProductMarkerComponent`) khai một lần ở mã hàng.
  - Cách ước: cắt cả bộ: số lớp = max theo size của ⌈cần ÷ số sản phẩm size đó trên 1 lớp⌉, mét = số lớp × chiều dài, chọn bìa ít mét nhất; cắt lẻ / cắt bù: tối thiểu một chiều dài bìa. Chỉ để cảnh báo đủ / thiếu, không lưu.
  - **Bàn cắt không gắn bìa** (khác bản thiết kế ban đầu): một lần nhập có thể dùng nhiều bìa; vải đã cắt = Σ `PlyCount × LayLengthM`, sản lượng = các dòng `CuttingLayLine` như hiện tại. Không có `CuttingLay.ProductMarkerId`, không có mã lỗi 50054.
  - DB chặn (50055): mã hàng của bìa phải có trong đơn; nhóm bìa phải đã khai vải; size trong tỉ lệ phải có trong đơn. Backend kiểm thêm: mọi chi tiết PANEL thuộc ít nhất một nhóm, bìa có ít nhất một dòng tỉ lệ (xem trên).
- **Dữ liệu:** chạy patch **xóa toàn bộ bìa cũ** (theo size, không đổi sang được) và kích thước chi tiết; dữ liệu demo có 18 bìa ở các đơn DEMO-% (DEMO-DH001 có thêm bìa 1B để thử chọn bìa).
- **Lý do:** yêu cầu của người dùng: xưởng cắt theo bìa phía trên giác theo tỉ lệ đơn, nên tính mét theo hình chữ nhật không đối chiếu được với vải thực dùng. Người dùng chốt không gắn bìa vào bàn cắt vì một lần nhập dùng nhiều bìa, chỉ cần số mét và sản lượng của lần nhập.
- **Ảnh hưởng người khác:** Thái (lệnh cắt, bàn cắt, cấp vải), Khiêm (định mức mã hàng UC10, nhập bìa theo đơn), Morgan (schema). **Chạy patch cùng lúc với bản code này** (code mới đọc cột / bảng mới; code cũ đọc cột đã bỏ).
- **Cần sửa trong tài liệu:** Report-3 UC10 (bìa, bỏ kích thước chi tiết), UC49–UC51 (ước vải theo bìa của đơn); ERD và `NX-MTD_Database_Giai_thich.md`; DEV-ALL-42 chuyển sang thay thế bởi mục này.
- **Trạng thái:** Mở

### DEV-ALL-86 · Công đoạn gắn với chi tiết vải × vải

- **Ngày / Người / Task / PR:** 09/11/2026 · Claude (AI) · UC25, UC10 · cùng nhánh `feature/ALL-ui-sync-grade-levels`
- **Ảnh hưởng tới:**
  - Schema: `OperationComponent` thêm `MaterialId` (FK `FK_OC_Material` → `Material`), PK thành (OperationId, ProductComponentId, MaterialId), thêm `IX_OC_Material`. Patch `SQL/patches/2026-11-09_operation_component_fabric.sql`; `SQL/NX-MTD_Database.sql` (bảng, dữ liệu demo), `docs/NX-MTD_Database.dbml`, `docs/NX-MTD_Database_Mo_ta.md` đã cập nhật. Đã kiểm trên 2 DB tạm (dựng từ script đã commit rồi chạy patch 2 lần; dựng mới từ script mới): cấu trúc, FK, index và 50 dòng demo giống hệt nhau; DB tạm đã xóa.
  - Backend: `SaveOperationDto` / `OperationDto` đổi `ComponentIds` thành `ComponentFabrics` (`ProductComponentId`, `MaterialId`); `OperationService` chỉ nhận cặp là chi tiết PANEL của mã hàng × NVL loại vải có trong định mức của chi tiết đó (sai → `OPERATION_INVALID`), tối đa 500 cặp; import Excel vẫn không đổi liên kết. `ProductSpecService` lưu định mức thì gỡ liên kết có cặp chi tiết × vải không còn trong định mức chi tiết cắt. `ProductNormDto` thêm `MaterialType`.
  - Frontend: form công đoạn, ô "Chi tiết vải công đoạn làm trên" chọn theo dòng "chi tiết – vải" lấy từ định mức mã hàng (chỉ chi tiết cắt đang dùng, NVL loại vải).
  - Test: backend 2119 test qua (thêm test gắn chi tiết × vải và validator), frontend 596 test qua (thêm `componentFabric.test.ts`).
- **Tài liệu gốc ghi:** `OperationComponent` (25/10): công đoạn làm trên chi tiết nào (thành phần bất kỳ, cả phụ liệu), không phân biệt vải.
- **Code đang làm:** mỗi dòng là công đoạn × chi tiết vải × vải; chi tiết có vải chính và vải lót thì chọn riêng từng vải. Vải theo màu (`Material.Color`) nên một chi tiết có thể có nhiều dòng (mỗi màu vải một dòng). Không gắn theo từng màu – size, nên đổi màu – size của định mức không làm mất liên kết.
- **Dữ liệu:** patch nở mỗi liên kết cũ ra mọi vải của chi tiết trong định mức; liên kết với phụ liệu (vd Nhãn) hoặc chi tiết chưa có vải bị bỏ (DB demo: 30 liên kết thành 50 dòng, bỏ 5).
- **Lý do:** yêu cầu của người dùng: công đoạn liên kết với chi tiết vải theo từng vải, khớp cách kho BTP tách chi tiết theo vải (`ProductComponentMaterialId`).
- **Ảnh hưởng người khác:** Kiều (công đoạn, đơn giá UC25), Khiêm (định mức mã hàng UC10), Morgan (schema). Chạy patch cùng lúc với bản code này.
- **Cần sửa trong tài liệu:** Report-3 UC25 (công đoạn gắn chi tiết vải × vải), ERD.
- **Trạng thái:** Đã làm
