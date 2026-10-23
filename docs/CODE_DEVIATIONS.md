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
