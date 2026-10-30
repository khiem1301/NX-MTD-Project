# NX-MTD – Context dự án cho AI (đọc trước khi code)

> Bản 19/10/2026. Tổng hợp từ Report-1 (14/10), Report-2 (v1.1), Report-3 SRS (16/10), các quyết định Morgan đã chốt 14–18/10, `NX-MTD_Database.sql` (65 bảng), `NX-MTD_Coding_Rules.md` (19/10), `NX-MTD_Phan_cong_cong_viec.xlsx` (19/10).
> Dùng làm `CLAUDE.md` hoặc nạp đầu phiên chat với AI. Khi nguồn mâu thuẫn, file này đã ghi rõ cách xử lý ở mục 11.

---

## 0. Luật cho AI (bắt buộc)

1. **Chỉ làm đúng task được giao.** Không tự thêm tính năng, màn hình, bảng, cột, endpoint, thư viện ngoài task. Thấy thiếu thì ghi `TODO(<tên>, <mã task>)` và báo lại, không tự làm.
2. **Schema là `NX-MTD_Database.sql`.** EF Core scaffold từ script này (database-first). Không tự đặt tên bảng/cột khác, không thêm bảng lookup, bảng lịch sử, bảng mirror Ronald Jack Pro, cột `CreatedBy/UpdatedBy/ApprovedBy`. Đổi schema = trong cùng PR phải có **file migrate mới** `SQL/Migrations/<yyyyMMdd>_<NN>_<mã task>_<mo-ta>.sql` (idempotent, không sửa file migrate đã merge), cập nhật `SQL/CreateAndSeed.sql` cho khớp, sửa entity, và nêu tên file migrate trong mô tả PR (Coding Rules mục 3.12). Để người khác chạy migrate thay vì tạo lại DB.
3. **Không làm việc ngoài phạm vi** (mục 9). Đặc biệt: hệ thống **không tính lương chính thức**, không khóa tài khoản khi sai mật khẩu, không có QA/QC phân loại lỗi.
4. **Quy tắc nghiệp vụ ở mục 5 thắng chữ trong Report-3** khi khác nhau.
5. **Không đụng code của luồng người khác** (mục 8). Cần dữ liệu luồng khác thì gọi qua interface dùng chung (`IStockService`, `IOutputQueryService`, `IAttachmentService`, `IAuditLogService`, khung `Request`).
6. Theo đúng Coding Rules (mục 7): controller mỏng, logic ở Service, `Result` + `errorCode`, transaction cho nghiệp vụ nhiều bước, AuditLog cho mọi thay đổi dữ liệu nhạy cảm.
7. Chuỗi hiển thị dùng đúng mã MSG ở mục 6.4, tiếng Việt. Tên code tiếng Anh.
8. Không đoán. Thông tin chưa chốt (mục 12) thì dùng giá trị trong `SystemSetting` và để cấu hình được, không hard-code.
9. **Ghi lại mọi khác biệt với tài liệu.** Đọc `docs/CODE_DEVIATIONS.md` của repo trước khi code (code có thể đã khác file này). Khi code làm khác Report-3 SRS, file này, `NX-MTD_Database.sql` hoặc tổng hợp Use Case (đổi luồng, role, điều kiện, MSG, bảng/cột/trạng thái, UC bỏ hoặc mới), phải **tự thêm mục vào `docs/CODE_DEVIATIONS.md` trong cùng lần sửa** theo mẫu Coding Rules mục 8 (mã `DEV-<mã task>-<n>`, tài liệu gốc ghi gì, code làm gì, lý do, mục Report cần sửa, trạng thái `Mở`) và báo cho người dùng. Không tự ý làm khác tài liệu khi chưa được người code đồng ý.

---

## 1. Bài toán

- **Khách hàng:** xưởng gia công may mặc (CMT) NX-MTD, khoảng 100 công nhân, 1 tổ cắt + nhiều tổ may + QA/QC + kho + văn phòng. **Khách hàng duy nhất là công ty mẹ** (seed sẵn 1 dòng `Customer`; `Customer.Status` ACTIVE / DISCONTINUED, khách ngừng hợp tác không tạo được đơn mới).
- **Hiện trạng:** giấy, Zalo, chấm tay; tổ trưởng mất 15–25 phút mỗi sáng đếm suất ăn trước 08:30; phiếu KCS đến kế toán chậm; lệch kho; trả lương sai do đếm sản lượng sai; giám đốc không thấy lỗi, chi phí, tiến độ.
- **Ý tưởng cốt lõi:** nối 4 nguồn dữ liệu gốc đang rời rạc: **giờ công** (máy vân tay Ronald Jack Pro), **suất ăn**, **sản lượng công đoạn**, **tồn kho**. Tiến độ, chi phí, lương tạm tính suy ra từ dữ liệu thật.
- **Hai luồng sản lượng tách biệt (quan trọng nhất):**
  - *Sản lượng công đoạn* (`OutputRecord`): tổ trưởng may nhập cuối ca theo công nhân × công đoạn × dòng đơn. Dùng cho lương tạm tính, **không** dùng cho tiến độ.
  - *Sản lượng hoàn thành* (`QcBatch`): QA/QC đếm hàng đạt ở cuối chuyền theo đợt. Dùng cho bàn giao thành phẩm, tiến độ, doanh thu.
- **Trần sản lượng theo chuỗi (GBR-08):** cắt ≤ số đặt + % dư; sản lượng công đoạn ≤ BTP công nhân đã nhận; hoàn thành ≤ hàng trên chuyền.

## 2. Nhóm, lịch, công nghệ

- Nhóm SEP490-G40: Khiêm (leader/PM, merge PR), Kiều, Thái, Minh, Thảo (vào từ Phase 3). GVHD Lê Thị Tú Kiên.
- **Phase code:** P1 20–25/10 luồng chính (tag `v0.1`); P2 26/10–1/11 demo bảo vệ lần 1 (tag `v0.2-demo` trên `main`); P3 2–22/11 test, sửa, bổ sung; Report 5 Test nộp trước 20/11; bảo vệ 18/12.
- **Stack:** ASP.NET Core Web API + EF Core + SQL Server (collation `Vietnamese_100_CI_AS`), deploy AWS · React + TypeScript (Vite) + Ant Design + TanStack Query + Axios + Zustand + Zod · test xUnit/Moq/FluentAssertions (BE), Jest + RTL (FE). Job nền: Hangfire hoặc HostedService.
- **2 repo:** backend (4 lớp `API / Application / Domain / Infrastructure`) và frontend `nxmtd-web`. Chỉ một web app React, responsive; màn hình xưởng (tổ trưởng, QA/QC, công nhân, thủ kho) phải dùng được trên điện thoại, nút to, tối đa 3 thao tác.
- **Agent chấm công:** console .NET chạy trên máy Windows cài Ronald Jack Pro, đọc `RJData.mdb` (Access) bằng OleDb, gửi lô lượt quẹt lên API ingest.

## 3. Vai trò (9 role, bảng `Role`)

| RoleCode | Tên | Việc chính |
|---|---|---|
| `ADMIN` | Giám đốc | Đơn hàng, mã hàng, nhân sự + tài khoản, bậc thợ, NCC; duyệt đơn giá, duyệt bảng lương; xem tài chính mật (hòa vốn, chi phí) |
| `MANAGER` | Quản đốc | Tổ, hồ sơ NV (không thêm/xóa NV, không đổi role, không thấy NV Admin/Manager/Accountant), KPI, thông báo nhanh, tiến độ; xác nhận phiếu nhập NVL, xử lý thiếu NVL; **tạo đơn mua**; lập phiếu xuất hàng; duyệt kiểm kê; sửa dữ liệu sản xuất quá hạn (UC22) |
| `ACCOUNTANT` | Kế toán | Công đoạn + đơn giá, chấm công (map mã, chấm bù), suất ăn khách, xuất dữ liệu/nhập bảng lương Excel, chi phí tháng, xác nhận thanh toán đơn mua, xác nhận đã giao khách, sổ xe |
| `WAREHOUSE_KEEPER` | Thủ kho | Lập phiếu nhập NVL, cấp NVL/BTP cho tổ, nhận trả vải, nhận BTP, nhận thành phẩm, xuất hàng, kiểm kê. **Không** tạo đơn mua, **không** xem lương ngày |
| `TEAM_LEADER` | Tổ trưởng | Cắt hay may do `Team.TeamType` quyết định. Cắt: xin NVL, bàn cắt, bàn giao BTP, trả vải. May: xin BTP, giao/thu hồi BTP với CN, phân công công đoạn, sản lượng cuối ca, hàng hỏng, cắt bù, đề xuất bậc. Cả hai: xác nhận suất ăn, xin chấm công bù |
| `TEAM_DEPUTY` | Tổ phó | Cùng quyền `TEAM_LEADER` trong tổ mình khi tổ trưởng vắng (`vw_TeamActingLeader`: sau 08:00 tổ trưởng chưa có giờ vào). `RoleId` không đổi |
| `QAQC` | QA/QC | Nhập số đạt theo đợt (`QcBatch`), bàn giao thành phẩm cho kho. **Không** phân loại lỗi, **không** xem số lỗi/tỉ lệ lỗi |
| `ELECTRICIAN` | Cơ điện | Gửi đơn linh kiện / máy mới / vật tư máy |
| `WORKER` | Công nhân | Chỉ xem dữ liệu của mình: ngày công, bậc, phiếu lương; CN may thêm sản lượng + lương tạm tính; xác nhận số BTP được giao/thu hồi |

- Mỗi tổ cắt/may đúng 1 tổ trưởng + tối đa 1 tổ phó (trigger `trg_Employee_TeamLeadership`); không có cột leader trên `Team`.
- **Kiểu lương (`Employee.PayType`):** CN tổ may `PIECE_RATE`; thợ cắt và QA/QC `DAILY` (theo ngày công); còn lại `MONTHLY`.
- **Bậc thợ A–D** chỉ áp cho CN may, tổ trưởng, tổ phó.
- **3 tầng dữ liệu:** Công khai / Vận hành / Mật. Mật: đơn giá CMT, hòa vốn, giá trị thặng dư, tổng hợp chi phí (chỉ Admin); doanh thu (Admin + Kế toán) (GBR-02).

## 4. Luồng nghiệp vụ chính

### 4.1 Đơn hàng và NVL
1. Admin tạo `Product` (mã hàng) → `CustomerOrder` + `OrderLine` (mã hàng × màu × size, `OrderedQty`, `CmtUnitPrice`). Size là chữ tự do.
2. NVL của đơn: mỗi (đơn, NVL, màu) = 1 dòng `OrderMaterial`: `RequiredQty` **nhập tay** theo số khách báo (không tính từ định mức), `CustomerQty`, `CustomerExpectedDate`; `PurchaseQty` và `Source` (CUSTOMER/SUPPLIER/BOTH) là cột tính. Định mức theo mã hàng ở `ProductComponentMaterial` (thành phần × NVL × màu – size, kèm dài / rộng chi tiết; **không còn lượng vải / hao hụt** từ DB 29/10), do Kế toán và Admin nhập; màu – size lấy từ đơn hàng.
3. `Material.MaterialType` = `FABRIC` (đi qua tổ cắt) / `TRIM` (cấp thẳng tổ may); `Category` chi tiết (MAIN_FABRIC, LINING, INTERLINING, RIB / THREAD, ZIPPER, BUTTON, SNAP, ELASTIC, TAPE, LABEL, HANG_TAG, PADDING, PACKAGING, OTHER).
4. Thiếu NVL: xin khách trước, khách không cấp được mới mua. **Quản đốc tạo `PurchaseOrder`**, chọn NCC ACTIVE, đặt hàng; **Kế toán xác nhận thanh toán** ngay trên PO (`PaymentDate`, `PaidAmount`, `PaymentMethod`, ảnh chứng từ trong `Attachment`), không sửa NCC/số lượng (GBR-18). Số đã nhận/còn phải nhận xem `vw_PurchaseOrderLineStatus`.
5. Nhập NVL: Thủ kho lập `MaterialReceipt` (`NEW`, nguồn CUSTOMER hoặc SUPPLIER có PO), mỗi dòng `AcceptedQty / ShortQty / DefectiveQty` → **Quản đốc xác nhận** → kho NVL tăng theo `AcceptedQty` (GBR-19). Có thiếu/lỗi → hệ thống tạo `Request` `SHORT_DELIVERY` (`CreatedBy` NULL) + MSG31 → Quản đốc chọn `REPLENISH` (bên giao bù bằng phiếu `SUPPLEMENTARY` trỏ phiếu gốc) hoặc `NO_REPLENISH` (`PurchaseOrderLine.NotReplenishedQty`, PO `CLOSED_SHORT`).
6. `CustomerOrder.MaterialStatus` (AWAITING / PARTIAL / COMPLETE) tách khỏi `Status`. Đơn chỉ chuyển `NEW → IN_PRODUCTION` khi `MaterialStatus <> 'AWAITING'` (kiểm tra ở backend).

### 4.2 Cắt
1. Lệnh cắt `WorkOrder(+Line)` theo dòng đơn; DB 29/10 không còn lượng vải theo định mức nên lệnh cắt thường không kiểm vải khi lập; lệnh cắt bù (`SourceRequestId` có giá trị) ước tính số mét từ dài × rộng chi tiết và khổ vải (`vw_RecutFabricNeed`), thiếu thì không lưu và báo thiếu loại nào.
2. Tổ trưởng cắt xin NVL (`Request` `MATERIAL_REQUISITION`) → Thủ kho lập `MaterialIssue` `WarehouseType = MATERIAL` (**chỉ giữ chỗ**, `PENDING_RECEIPT`) → tổ trưởng đếm, xác nhận (`RECEIVED`, **lúc này mới trừ kho**; lệch phải có lý do, báo Quản đốc – GBR-06) hoặc từ chối (nhả giữ chỗ).
3. Bàn cắt `CuttingLay(+Line)`: SL = số lớp × tỉ lệ size; `ActualQty` khác `CalculatedQty` phải có lý do. Tổng cắt vượt số đặt trong mức `CUT_SURPLUS_PERCENT` → cảnh báo MSG15; vượt mức → chặn MSG29.
4. `CutHandover(+Line)`: tổ cắt bàn giao BTP vào kho `SEMI_FINISHED` theo dòng đơn; Thủ kho xác nhận (lệch có lý do) hoặc từ chối.
5. `MaterialReturn(+Line)`: **chỉ tổ cắt trả vải thừa**, theo phiếu cấp gốc; Thủ kho xác nhận/từ chối. Tổ may không trả phụ liệu.

### 4.3 May và QC
1. Tổ trưởng may xin BTP (`Request` `SEMI_FINISHED_REQUISITION`, gắn `KpiTargetId`) → Thủ kho cấp `MaterialIssue` `SEMI_FINISHED` (dòng là `OrderLineId`) và phụ liệu (`MATERIAL`) → tổ xác nhận như 4.2.2.
2. Giao/thu hồi BTP với công nhân: `WorkerPieceMovement` (`HANDOVER` / `RETRIEVAL` với lý do OPERATION_CHANGED, WORKER_ABSENT, STYLE_FINISHED). CN tự xác nhận (UC60) hoặc tổ trưởng xác nhận hộ `ON_BEHALF` (bắt buộc lý do). BTP tổ đang giữ: `vw_TeamPieceHolding`.
3. Công đoạn + đơn giá: Kế toán định nghĩa `Operation` (đúng 1 công đoạn may cuối / mã hàng) và `PieceRate` (DRAFT → PENDING_APPROVAL → APPROVED / REJECTED có lý do); Admin duyệt. **Chưa duyệt thì không nhập sản lượng** (MSG18, GBR-11).
4. Phân công `OperationAssignment` (UC52): đổi phân công = đóng dòng cũ (`ValidTo`), giữ lịch sử. Có thể chép phân công từ mã hàng cũ.
5. Sản lượng cuối ca `OutputRecord`: lưới nạp sẵn từ phân công; công đoạn ngoài phân công thêm được cho riêng ca đó (GBR-20). `UnitRate` **khóa lúc nhập**. Unique (ngày, ca, tổ, dòng đơn, công đoạn, CN). Trần theo `vw_WorkerOutputCap` (MSG16). CN chưa có chấm công → cảnh báo MSG17. Ca: MORNING / AFTERNOON / OVERTIME.
6. Hàng hỏng `DefectRecord` (tổ trưởng may ghi theo QA/QC báo): `REWORK` (trả lại CN sửa, **không trừ** sản lượng, chỉ tính số lỗi khi xét bậc) hoặc `SCRAP` (trừ khỏi hàng trên chuyền và sản lượng được trả của CN gây lỗi, lưu `ScrapUnitRate`, sinh yêu cầu cắt bù `REPLACEMENT_CUTTING` → Quản đốc → lệnh cắt bù). Bắt buộc công đoạn, CN gây lỗi, lý do (`OTHER` phải ghi chú), ảnh tùy chọn; báo cho CN gây lỗi.
7. `QcBatch`: QA/QC nhập **chỉ `FinishedQty`** theo đợt trong ca, trần ≤ hàng trên chuyền (MSG19).
8. `FgHandover(+Line)`: QA/QC gom số đạt bàn giao vào kho `FINISHED` theo dòng đơn (được giao một phần, tổng ≤ số QC đạt); Thủ kho xác nhận/từ chối.
9. **Cửa sổ sửa (GBR-09):** sản lượng + phiếu lỗi đến hết ngày làm việc kế tiếp (`EditDeadline`, `fn_NextWorkingDayEnd`, bỏ qua Chủ nhật); `QcBatch` đến hết ca. Sau hạn chỉ Quản đốc sửa, bắt buộc lý do, ghi `AuditLog` (UC22, MSG20).

### 4.4 Xuất hàng
- `DispatchNote(+Line)` kiêm lệnh giao: Quản đốc lập (hàng thành phẩm được giữ chỗ) → Thủ kho xác nhận xuất (`IN_DELIVERY`, trừ kho FINISHED) hoặc từ chối có lý do → Kế toán xác nhận khách đã nhận (`CustomerReceivedDate`, ảnh biên bản có khách ký) → `DELIVERED` / `PARTIALLY_DELIVERED`. Có 2 ảnh khác nhau: phiếu xuất có Quản đốc ký (thủ kho chụp) và biên bản giao (kế toán chụp).
- Giao hàng luôn thành công, không có trạng thái giao thất bại/trả hàng (GBR-24). Đơn hàng `DELIVERED` khi tổng đã giao ≥ số đặt (GBR-23).

### 4.5 Kho
- 3 kho: `MATERIAL` (theo NVL + màu), `SEMI_FINISHED`, `FINISHED` (theo `OrderLineId`).
- `StockTransaction` là thẻ kho **append-only** (trigger chặn sửa/xóa), `BalanceAfter >= 0`, unique theo dòng chứng từ nguồn. **Không có bảng số dư**: tồn = `vw_StockBalance` (tồn thực, giữ chỗ, khả dụng).
- Kho chỉ đổi khi chứng từ được xác nhận; không cấp/xuất vượt `AvailableQty` (GBR-07, MSG13). Mọi ghi kho đi qua `IStockService` (Thái) trong cùng transaction với việc xác nhận chứng từ.
- Kiểm kê `Stocktake(+Line)`: Thủ kho lập, Quản đốc/Admin duyệt (điều chỉnh kho) hoặc trả lại có lý do.

### 4.6 Chấm công (Ronald Jack Pro) và suất ăn
- Chỉ lấy 3 trường từ `CheckInOut` của RJ: `ID` → `AttendanceLog.SourceRecordId`, `MaChamCong` → `AttendanceCode`, `GioCham` → `PunchTime`. Không lấy vân tay, mật khẩu, ảnh, tên máy. Không tạo bảng mirror nhân viên/máy/phòng ban của RJ.
- JOB-01 mỗi 10 phút: đọc `WHERE ID > MAX(LastSourceRecordId)` (mốc theo ID, không theo giờ) → `usp_Attendance_IngestBatch` (TVP `AttendancePunchTableType`). Trùng (mã, giờ) hoặc trùng ID bỏ qua (GBR-16). Lượt thuộc tháng khóa bỏ qua (`SkippedLockedMonth`). Mã chưa map vẫn lưu, `EmployeeId` NULL; map bằng `usp_Attendance_MapCode` thì gán lượt cũ + tính lại công. Quá 30 phút không đồng bộ được → MSG23, báo Kế toán. Dự phòng: import Excel RJ (UC29, `Source = 'EXCEL_IMPORT'`, xem trước MSG24).
- **Cách tính `DailyTimesheet` (`usp_Attendance_RebuildTimesheet`):** ca "Hành Chính" 07:30–16:30, nghỉ trưa 11:30–12:30, 1 công = 8 giờ (đều trong `SystemSetting`). Lượt cách lượt trước < 10 phút bỏ qua. Giờ vào = lượt đầu; giờ ra = **lượt kế tiếp sau giờ vào** (quẹt ra nhiều lần chỉ tính lần đầu, đánh dấu `HasExtraPunch`). Công = giờ thực làm trong ca / 8, tối đa 1. Tăng ca = giờ ra − 16:30. **Chủ nhật x2** (`SUNDAY_PAY_MULTIPLIER`, cột `IsSunday`).
- Chấm bù `AttendanceAdjustment`: chỉ Kế toán, bắt buộc lý do (GBR-25); theo đơn `ATTENDANCE_CORRECTION` của tổ trưởng hoặc ngoại lệ.
- Suất ăn: tổ trưởng xác nhận số suất tổ (`TeamMealConfirmation`, gợi ý từ chấm công), Kế toán nhập suất khách (`DailyMeal.GuestCount`). **JOB-02 08:15** nhắc tổ chưa xác nhận; **JOB-03 08:30** đồng bộ chấm công lần cuối (20s, không retry) → tổ chưa xác nhận lấy số người đã quẹt, văn phòng lấy số người đã quẹt, cộng suất khách → khóa (trigger chặn sửa, MSG22). Kế toán báo bếp ngoài hệ thống; hóa đơn bếp cuối tháng là một dòng `MonthlyCost` (MEALS).

### 4.7 Lương, chi phí, tài chính
- **Hệ thống không tính lương chính thức (GBR-12).** Kế toán xuất dữ liệu (`vw_PayrollExportData`: ngày công, tăng ca, phần Chủ nhật, tiền sản phẩm, trừ Scrap, bậc + tiền bậc) → tính trong Excel → import `Payroll` / `Payslip` / `PayslipItem` → gửi duyệt → Admin duyệt (hoặc từ chối/mở lại có lý do) → công nhân xem phiếu lương → khóa tháng `AccountingPeriod` (`LOCKED`, GBR-10, MSG21). Thuế làm ở MISA, không trao đổi dữ liệu.
- **Lương tạm tính** chỉ cho CN may `PIECE_RATE` (`vw_WorkerDailyEstimatedPay`, UC59): Σ(SL × `UnitRate` khóa) − Σ(Scrap × `ScrapUnitRate`); Chủ nhật tiền sản phẩm x2, khoản trừ Scrap không nhân đôi. Luôn ghi rõ "tạm tính".
- Chi phí tháng `MonthlyCost`: Kế toán import Excel, 10 khoản (MEALS, FUEL, UTILITIES, PREMISES, MACHINE_REPAIR, MACHINE_SUPPLIES, STATIONERY, MATERIALS_BOUGHT, SCRAP, OTHER), FIXED/VARIABLE (VARIABLE phải có `ProductId`); import lại thay số tháng đó.
- Doanh thu (`vw_MonthlyRevenue`) = SL thành phẩm đã nhập kho trong tháng × `CmtUnitPrice`.
- Hòa vốn + giá trị thặng dư theo mã hàng (`BreakEvenResult`, lưu lịch sử mỗi lần tính): chỉ tính khi bảng lương tháng đã duyệt **và** chi phí tháng đã import (GBR-17, MSG25); cảnh báo mã hàng nguy cơ lỗ (MSG26). Chỉ Admin.

### 4.8 Khác
- **Đơn từ `Request` (10 loại, `RequestTypeCode`):**

| Loại | Người tạo | Người xử lý | Duyệt? |
|---|---|---|---|
| `SPARE_PARTS`, `NEW_MACHINE` | Cơ điện | Admin | có |
| `MACHINE_SUPPLIES` | Cơ điện | Kế toán | không |
| `ATTENDANCE_CORRECTION` | Tổ trưởng | Kế toán | có |
| `MATERIAL_SUPPLEMENT` | Tổ trưởng, Thủ kho | Quản đốc | có |
| `DEFECTIVE_MATERIAL` | Tổ trưởng cắt | Quản đốc | có |
| `REPLACEMENT_CUTTING` | Tổ trưởng may | Quản đốc | có |
| `SHORT_DELIVERY` | Hệ thống | Quản đốc | không |
| `MATERIAL_REQUISITION` | Tổ trưởng cắt/may | Thủ kho | không |
| `SEMI_FINISHED_REQUISITION` | Tổ trưởng may | Thủ kho | không |

  Trạng thái: PENDING_APPROVAL / PENDING_PROCESSING / APPROVED / REJECTED / COMPLETED / CANCELLED. Một ngày `TargetDate`, lý do trong `Content`, lý do từ chối trong `DecisionNote` (bắt buộc). Quyết định không đổi được (GBR-05). Ảnh ≤ 5 (`Attachment`). Lịch sử trạng thái ghi `AuditLog`.
- **KPI tổ may** (`KpiTarget`): đơn + mã hàng + tổ may + kỳ, `TargetFinishedQty`, `WorkingDays` (chỉ tiêu ngày = tổng / số ngày). **Không** có tỉ lệ lỗi tối đa, **không** thưởng KPI. Tổ cắt không có KPI (lệnh cắt là chỉ tiêu). Kết quả `vw_KpiDailyResult`.
- **Bậc thợ:** `GradeLevel` (A–D + tiền bậc tháng + ngày hiệu lực, đổi tiền = thêm dòng). Tổ trưởng đề xuất `GradeEvaluation` cho tổ mình; Admin/Quản đốc quyết (GBR-22). Dòng `APPLIED` là lịch sử bậc.
- **Thông báo:** `Announcement` (NORMAL/URGENT; toàn xưởng/tổ/vai trò) → `Notification` (giữ 90 ngày, JOB-05 01:00). Push: FCM (API-02), luôn lưu trong app.
- **Tiến độ đơn** (UC21, `vw_OrderLineProgress`): cắt, vào kho BTP, cấp tổ, hoàn thiện, nhập kho, đã giao, Rework/Scrap, tỉ lệ lỗi. **JOB-04 17:00** đặt cờ `IsDelayRisk` (SL còn lại ÷ trung bình hoàn thiện 7 ngày > số ngày còn lại), báo Admin + Quản đốc ngày đầu tiên bị cờ.
- **Sổ xe** `Vehicle` / `VehicleTrip`: cảnh báo km không liền chuyến trước hoặc xăng vượt định mức 15% (MSG30).

## 5. Quy tắc nghiệp vụ chung (đã chốt, thắng Report-3)

| # | Quy tắc |
|---|---|
| R1 | Phạm vi dữ liệu theo vai trò, kiểm tra ở server mọi request: CN chỉ thấy của mình, tổ trưởng/tổ phó chỉ tổ mình, Quản đốc không xem/sửa NV Admin/Manager/Accountant (GBR-01). |
| R2 | Không tự đăng ký, không tự reset mật khẩu; Admin cấp tài khoản + mật khẩu tạm, reset. Không ép đổi mật khẩu, không có luật mật khẩu (chỉ 2 lần nhập khớp). Đổi mật khẩu → đổi `SecurityStamp`, đăng xuất phiên khác (GBR-04). Username = mã NV. |
| R3 | **Không khóa tài khoản khi sai mật khẩu**, không có job mở khóa. `Account.Status` chỉ ACTIVE/LOCKED do Admin. Báo lỗi đăng nhập không lộ sai user hay sai mật khẩu (GBR-03, MSG04). |
| R4 | Từ chối / trả lại luôn bắt buộc lý do (GBR-05, MSG09). Nhận lệch số luôn bắt buộc lý do và báo Quản đốc (GBR-06, MSG14). |
| R5 | Không xóa cứng master data đã dùng, chuyển `Status` (DISCONTINUED / RESIGNED / LOCKED) (GBR-14). Không xóa cứng dữ liệu kho, công, lương. |
| R6 | Mọi thay đổi chứng từ kho, sản lượng, chấm công, đơn giá, bảng lương, tài khoản → `AuditLog` (người, lúc, giá trị cũ/mới JSON, lý do) trong cùng transaction (GBR-15). Không dùng cột *By để thay AuditLog. |
| R7 | Tháng đã khóa: không sửa/import/tính lại (trigger chặn sản lượng, lỗi, QC, chấm bù, chi phí) (GBR-10). |
| R8 | Lương: CN may khoán theo sản phẩm; thợ cắt + QA/QC theo ngày công; vai trò khác lương tháng (cơ điện tạm để MONTHLY). Đơn giá đổi theo ngày hiệu lực, sản lượng đã lưu giữ đơn giá lúc nhập. |
| R9 | Rework ghi như Scrap (có CN gây lỗi) nhưng không trừ; Scrap trừ và sinh cắt bù (GBR-21). |
| R10 | Thủ kho không có màn xem lương ngày. QA/QC không xem lỗi/tỉ lệ lỗi. |
| R11 | Hủy bàn giao: BTP và thành phẩm đều có thể bị thủ kho từ chối (có lý do). |
| R12 | Lỗi đồng thời: bảng có `RowVer` (ROWVERSION) dùng optimistic concurrency → 409 `COMMON_CONCURRENCY_CONFLICT`. |

## 6. Database (`NX-MTD_Database.sql`, 65 bảng)

### 6.1 Quy ước thật của schema
- Database `NXMTD`, tên bảng **số ít PascalCase** (`CustomerOrder`, `OutputRecord`), khóa chính `<Bảng>Id` (`OrderId`, `EmployeeId`), FK cùng tên cột khóa.
- Trạng thái và danh mục cố định là **mã chữ UPPER_SNAKE có CHECK** (`'PENDING'`, `'IN_PRODUCTION'`), không có bảng lookup (trừ `Role`, `GradeLevel`, `SystemSetting`).
- SL sản phẩm `INT`; SL NVL `DECIMAL(18,3)`; tiền `DECIMAL(18,2)`.
- Thời điểm mặc định `dbo.fn_VnNow()` (giờ Việt Nam, `DATETIME2(0)`), có `CreatedAt`, `UpdatedAt`, nhiều bảng có `RowVer`. Không có `CreatedBy/UpdatedBy/IsDeleted`.
- Chỉ 26 FK tới `Employee`, khi dòng nói về chính người đó hoặc bước giao–nhận/xác nhận (`IssuedBy`, `ReceivedBy`, `ConfirmedBy`, `InspectorId`, `RecordedBy`, `DispatchedBy`, `Request.CreatedBy`, `ProposedBy`, `SenderId`, `AuditLog.ChangedBy`…).
- Tài khoản seed `ADMIN` có `PasswordHash = '__SET_BY_BACKEND_SEED__'`: backend phải ghi hash thật khi seed.
- Chạy script: `sqlcmd -S <server> -U sa -I -i NX-MTD_Database.sql` (cần `-I`).

### 6.2 Danh sách bảng theo người sở hữu

| Người | Bảng |
|---|---|
| Khiêm (18) | Role, Employee, Account, Team, GradeLevel, GradeEvaluation, Announcement, AnnouncementTarget, Notification, SystemSetting, Attachment, AuditLog, Customer, Product, CustomerOrder, OrderLine, Request, RequestLine |
| Kiều (6) | Operation, PieceRate, OperationAssignment, OutputRecord, DefectRecord, QcBatch |
| Thái (21) | Material, OrderMaterial, ProductComponentMaterial, MaterialReceipt(+Line), MaterialIssue(+Line), MaterialReturn(+Line), StockTransaction, WorkOrder(+Line), CuttingLay(+Line), CutHandover(+Line), WorkerPieceMovement, FgHandover(+Line), DispatchNote(+Line) |
| Minh (16) | AttendanceSyncSession, AttendanceLog, DailyTimesheet, AttendanceAdjustment, DailyMeal, TeamMealConfirmation, Supplier, PurchaseOrder, PurchaseOrderLine, AccountingPeriod, Payroll, Payslip, PayslipItem, MonthlyCost, BreakEvenResult, KpiTarget |
| Thảo (4) | Stocktake, StocktakeLine, Vehicle, VehicleTrip |

### 6.3 Trạng thái chính

| Bảng | Giá trị |
|---|---|
| `CustomerOrder.Status` | NEW, IN_PRODUCTION, IN_DELIVERY, PARTIALLY_DELIVERED, DELIVERED, COMPLETED, CANCELLED, STOPPED (phải có `StopReason`) |
| `CustomerOrder.MaterialStatus` | AWAITING, PARTIAL, COMPLETE |
| `PieceRate.Status` | DRAFT, PENDING_APPROVAL, APPROVED, REJECTED |
| `PurchaseOrder.GoodsStatus` / `PaymentStatus` | PENDING_FINALISATION, ORDERED, PARTIALLY_RECEIVED, RECEIVED, CLOSED_SHORT, CUSTOMER_SUPPLIED, REJECTED / UNPAID, PAID, RETURNED |
| `MaterialReceipt` | `ReceiptType` NEW/SUPPLEMENTARY · `Source` CUSTOMER/SUPPLIER · `Status` PENDING, CONFIRMED, RETURNED |
| `MaterialIssue.Status` | PENDING_RECEIPT (giữ chỗ), RECEIVED (trừ kho), REJECTED |
| `MaterialReturn`, `CutHandover`, `FgHandover` | PENDING, CONFIRMED, REJECTED, CANCELLED |
| `WorkOrder.Status` | OPEN, CUTTING, COMPLETED (tổ cắt bấm Cắt xong sau khi kho nhận BTP và trả vải thừa; Quản đốc không đóng tay), CANCELLED |
| `CuttingLay` / `OutputRecord` / `DefectRecord` | RECORDED, LOCKED / SAVED, LOCKED / RECORDED, CANCELLED, LOCKED |
| `WorkerPieceMovement` | `MovementType` HANDOVER/RETRIEVAL · `ConfirmMode` WORKER/ON_BEHALF · `Status` PENDING, CONFIRMED, REJECTED |
| `DispatchNote.Status` | PENDING, REJECTED, CANCELLED, IN_DELIVERY, DELIVERED, PARTIALLY_DELIVERED |
| `Stocktake.Status` | PENDING_APPROVAL, APPROVED, RETURNED |
| `DailyMeal.Status` | OPEN, AUTO_CLOSED, CLOSED, KITCHEN_NOTIFIED |
| `Payroll.Status` / `AccountingPeriod.Status` | DRAFT, PENDING_APPROVAL, APPROVED, REJECTED / OPEN, LOCKED |
| `GradeEvaluation.Status` | SUBMITTED, APPLIED, NOT_APPLIED |
| `KpiTarget` | `Status` ACTIVE, CLOSED, CANCELLED · `Result` ACHIEVED, NOT_ACHIEVED |
| `Employee` / `Account` / master data | ACTIVE, RESIGNED / ACTIVE, LOCKED / ACTIVE, DISCONTINUED |
| `DefectRecord.DefectReasonCode` | SKIPPED_STITCH, CROOKED_SEAM, WRONG_SIZE, TORN, STAINED, OTHER |

### 6.4 View, thủ tục, trigger, hàm có sẵn (dùng lại, không viết lại bằng LINQ)
- View: `vw_StockBalance`, `vw_PurchaseOrderLineStatus`, `vw_MaterialStock`, `vw_TeamPieceHolding`, `vw_WorkerOutputCap`, `vw_WorkerDailyEstimatedPay`, `vw_OrderLineProgress`, `vw_KpiDailyResult`, `vw_MonthlyRevenue`, `vw_PayrollExportData`, `vw_TeamActingLeader`.
- Thủ tục: `usp_Attendance_IngestBatch`, `usp_Attendance_RebuildTimesheet`, `usp_Attendance_MapCode`.
- Hàm: `fn_VnNow`, `fn_IsPeriodLocked`, `fn_NextWorkingDayEnd`.
- Trigger: thẻ kho append-only, lượt quẹt không sửa/xóa, khóa tháng (OutputRecord, DefectRecord, QcBatch, AttendanceAdjustment, MonthlyCost), 1 tổ trưởng/1 tổ phó mỗi tổ, khóa suất ăn sau giờ chốt.
- `SystemSetting` (key quan trọng): `MEAL_REMINDER_TIME` 08:15, `MEAL_CUTOFF_TIME` 08:30, `ATTENDANCE_SYNC_MINUTES` 10, `ATTENDANCE_ALERT_MINUTES` 30, `DELAY_CHECK_TIME` 17:00, `DELAY_AVG_DAYS` 7, `NOTIFICATION_RETENTION_DAYS` 90, `CUT_SURPLUS_PERCENT` 3 (chưa chốt), `FUEL_NORM_TOLERANCE_PERCENT` 15, `REQUEST_MAX_PHOTOS` 5, `WORK_START_TIME`, `WORK_END_TIME`, `LUNCH_START_TIME`, `LUNCH_END_TIME`, `DUPLICATE_PUNCH_MINUTES` 10, `SUNDAY_PAY_MULTIPLIER` 2, `WORKDAY_STANDARD_HOURS` 8, `LEADER_ABSENT_CHECK_TIME` 08:00.

### 6.5 Database tự chặn gì, backend phải lo gì
- **DB chặn:** trùng khóa nghiệp vụ, mã cố định sai, thẻ kho âm/bị sửa, thiếu lý do khi từ chối/lệch số/sửa SL cắt, Scrap thiếu đơn giá, PO đã đặt thiếu NCC, phiếu nhập NCC thiếu PO, tháng khóa, sửa suất ăn sau giờ chốt.
- **Backend lo:** phân quyền theo vai trò + tổ; ai tạo/xử lý loại đơn nào; hạn sửa `EditDeadline`; trần sản lượng (`vw_WorkerOutputCap`); đủ hàng trước khi cấp/xuất (`AvailableQty`); đủ vải khi lưu lệnh cắt; ghi `StockTransaction` cùng transaction với xác nhận; chuyển trạng thái đơn hàng; ghi `AuditLog`; giới hạn 5 ảnh; đã nhận + không bù ≤ số đặt trên dòng PO; tổng bàn giao TP ≤ tổng QC đạt.

### 6.6 Thông báo hệ thống (MSG, Report-3 V.2)
MSG01 Vui lòng nhập {tên trường}. · MSG02 Không có dữ liệu phù hợp. · MSG03 Đã lưu. · MSG04 Tên đăng nhập hoặc mật khẩu không đúng. · MSG05 Mật khẩu nhập lại không khớp. · MSG06 Tài khoản đã bị khóa. Vui lòng liên hệ Admin. · MSG07 Mật khẩu hiện tại không đúng. · MSG08 Đã đổi mật khẩu. Các thiết bị khác đã được đăng xuất. · MSG09 Vui lòng nhập lý do. · MSG10 Đã gửi, đang chờ xác nhận. · MSG11 Đã xác nhận. · MSG12 Đã trả lại kèm lý do. · MSG13 Số lượng vượt tồn kho hiện có ({số tồn}). · MSG14 Vui lòng nhập lý do chênh lệch. · MSG15 Số cắt đang vượt số đặt {số lượng} sản phẩm. · MSG16 Sản lượng vượt số bán thành phẩm đã nhận ({số đã nhận}). · MSG17 {Tên công nhân} chưa có chấm công hôm nay. · MSG18 Mã hàng chưa được duyệt đơn giá công đoạn. · MSG19 Số hoàn thiện vượt hàng trên chuyền ({số lượng}). · MSG20 Đã hết hạn sửa. Vui lòng liên hệ Quản đốc. · MSG21 Tháng {tháng} đã khóa, không thể thay đổi dữ liệu. · MSG22 Đã chốt suất ăn lúc 08:30, không thể thay đổi. · MSG23 Chưa đồng bộ được chấm công hơn 30 phút. Có thể nhập file dự phòng. · MSG24 {a} dòng hợp lệ, {b} dòng lỗi, {c} dòng trùng. Chỉ các dòng hợp lệ được nhập. · MSG25 Chưa thể tính: bảng lương tháng chưa được duyệt hoặc chi phí tháng chưa được nhập. · MSG26 Mã hàng {mã hàng} có nguy cơ lỗ: mới đạt {x}% điểm hòa vốn. · MSG27 Đã xuất file Excel. · MSG28 Mất kết nối. Dữ liệu đang nhập vẫn được giữ, vui lòng thử lại. · MSG29 Số cắt vượt quá mức cho phép so với số đặt. · MSG30 Số km không liền với chuyến trước hoặc lượng xăng vượt định mức. · MSG31 Phiếu nhập có hàng thiếu hoặc lỗi. Hệ thống đã tạo yêu cầu xử lý thiếu NVL.

Map vào `constants/messages.ts` (FE) và `message` của `Result` (BE); mỗi lỗi nghiệp vụ có một `errorCode` UPPER_SNAKE riêng.

## 7. Coding Rules tóm tắt (đầy đủ: `NX-MTD_Coding_Rules.md`)

**API**
- URL `/api/v1/<role>/<resource>` kebab-case số nhiều; chung mọi role: `/api/v1/common/...`. Hành động nghiệp vụ: `POST .../{id}/approve`, `/confirm`, `/reject`.
- Phân trang `?page=1&pageSize=20&sortBy=&sortDir=&keyword=` (`pageSize` ≤ 100).
- JSON camelCase; enum trả **chuỗi PascalCase** (`"PendingReceipt"`); tiền là số; ngày ISO 8601.
- Response: `{ success, data, message }`; trang: `data = { items, page, pageSize, totalItems }`; lỗi: `{ success:false, errorCode, message, errors? }`.
- Status: 400 validation (FluentValidation, `errors` theo field) · 401 (FE refresh token 1 lần) · 403 · 404 · **409 mọi lỗi nghiệp vụ** + `errorCode` (không dùng 422) · 500 chỉ từ middleware toàn cục.
- `errorCode` khai báo một nơi: `Domain/Constants/ErrorCodes.cs` ↔ `src/constants/errorCodes.ts`, tiền tố module (`WAREHOUSE_INSUFFICIENT_STOCK`).

**Backend**
- `API → Application → Domain`; `Infrastructure → Application/Domain`; Domain không reference EF/ASP.NET.
- Controller chia folder theo role, kế thừa base controller của role (`[Authorize(Roles = ...)]`), route slugify. Hai role cùng tài nguyên = 2 controller, 1 Service. Service/DTO/Repository **không** chia theo role.
- Controller mỏng, chỉ trả DTO (AutoMapper). Service: 1 method = 1 use case, > 40 dòng thì tách; dùng `IUnitOfWork`/Repository, không inject `DbContext`; nghiệp vụ nhiều bước trong **1 transaction**, `SaveChangesAsync()` 1 lần; lỗi nghiệp vụ `Result.Failure(ErrorCodes.X, "...")`, không trả null, không nuốt exception; Service nhận `userId`/`teamId` để kiểm quyền sở hữu; truyền `CancellationToken`.
- Validate định dạng ở Validator (400), nghiệp vụ ở Service (409).
- EF Core: Fluent API (`IEntityTypeConfiguration<T>`), `AsNoTracking()` khi chỉ đọc, chỉ `Select` cột cần, cấm N+1. Enum C# ↔ mã UPPER_SNAKE trong DB bằng value converter.
- JWT access + refresh, hash BCrypt/Identity. Log `ILogger<T>` (Serilog), không log mật khẩu/token/lương chi tiết.
- Đặt tên: PascalCase; `_camelCase` field; `I` interface; hậu tố `Async`; DTO `CreateXDto`/`XDto`; `XDtoValidator`; `XProfile`; `XRepository`; bool `Is/Has/Can`.
- Test bắt buộc cho tính công, lương tạm tính, tồn kho, trần sản lượng: xUnit + Moq/NSubstitute + FluentAssertions, tên `Method_Condition_Expected`.

**Frontend (`nxmtd-web`)**
- Node ≥ 24, npm, TS strict, không `any`, không `@ts-ignore` (được `@ts-expect-error` kèm lý do), hạn chế `as` và `!`.
- Feature-based theo nghiệp vụ (`features/<auth|users|orders|warehouse|cutting|sewing|qaqc|attendance|meal|payroll|...>/{api,components,hooks,pages,utils,constants.ts,types.ts,index.ts}`); import feature khác chỉ qua `index.ts`, alias `@/`.
- Luồng gọi API: component → hook (TanStack Query) → `features/x/api` → `services/apiClient.ts` (Axios, `env.apiBaseUrl` từ `VITE_API_BASE_URL`). Không fetch trong component. Không đưa dữ liệu API vào Zustand. Invalidate query sau mutation. Mọi màn có loading / data / empty / error.
- Form antd + Zod, disable submit khi đang gửi, modal xác nhận cho thao tác không hoàn tác. Chuỗi trong `constants/messages.ts`. Ẩn nút theo role bằng `usePermission()` / `<Can>` (chỉ là UX). Hiển thị giờ `Asia/Ho_Chi_Minh` dạng `dd/MM/yyyy HH:mm`, tiền `1.250.000 ₫`. Component ≤ 200 dòng.

**Git**
- Branch `main` (demo) / `develop` (tích hợp) / `feature/<task-id>-<ten-ngan>` / `bugfix/...` / `hotfix/...`; `<task-id>` là ID trong file phân công (KH-xx, KI-xx, TH-xx, MI-xx, TA-xx) hoặc UCxx.
- Conventional Commits `feat(warehouse): ...`. PR ≤ 400 dòng vào `develop`, BE: `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test`; FE: `typecheck`, `lint`, `test`, `build`. ≥ 1 approve, CI xanh, PM (Khiêm) merge.
- Không commit secret, `.env`, `appsettings.Development.json`.
- Đổi database → thêm file migrate trong `SQL/Migrations/` + cập nhật `CreateAndSeed.sql` + entity trong cùng PR, ghi tên file migrate trong mô tả PR (Coding Rules mục 3.12).
- Code khác tài liệu → thêm mục `docs/CODE_DEVIATIONS.md` trong cùng PR, ghi mã DEV trong mô tả PR (Coding Rules mục 8). Cuối Phase 3 mỗi người sửa Report theo các mục `Mở` của mình.

## 8. Phân công (ai làm gì, UC nào)

| Người | Luồng | Use case |
|---|---|---|
| **Khiêm** | Nền tảng (khung BE/FE, scaffold 65 entity, CI, seed), auth/RBAC, nhân sự + tài khoản + tổ, mã hàng + đơn hàng, khung đơn từ + `IAttachmentService` + `IAuditLogService`, thông báo, bậc thợ, dashboard tiến độ, deploy | UC01–05, 07–10 (khung), 15–18, 20, 21, 50; UC22 màn xem log |
| **Kiều** | Công đoạn + đơn giá, phân công, sản lượng cuối ca, hàng hỏng + loại đơn cắt bù, QC batch, cửa sổ sửa, `IOutputQueryService`; kiêm vẽ hình báo cáo | UC22, 25, 26, 52, 55, 56, 57 |
| **Thái** | NVL + NVL của đơn + định mức, `IStockService`, nhập NVL, xin/cấp NVL và BTP, trả vải, lệnh cắt (+ cắt bù), bàn cắt, bàn giao BTP, giao/thu hồi BTP với CN, bàn giao TP, xuất hàng, xử lý thiếu NVL + `MaterialStatus` | UC08 (thiếu NVL, cắt bù), 19 (lệnh cắt), 24, 37–47, 51, 53, 54, 60 |
| **Minh** | Agent RJ + đồng bộ, bảng công, chấm bù, import Excel chấm công, suất ăn + 3 job, NCC + đơn mua + thanh toán, bảng lương + khóa tháng, tra cứu cá nhân, chi phí, doanh thu, hòa vốn, KPI tổ may, báo cáo kế toán | UC06, 11, 12, 14, 19 (KPI), 23, 27–34, 36, 58, 59, 61, 62 |
| **Thảo** (P3) | Kiểm kê, sổ xe, test | UC35, 48, 49 |

UC13 (xuất Excel): mỗi người làm nút xuất cho màn của mình.

**Điểm bàn giao giữa các luồng:** khung + entity + seed (Khiêm, 20/10) → `IStockService` (Thái, 20/10) → đăng nhập + base controller (Khiêm, 21/10; trước đó tạm `[AllowAnonymous]`) → khung Request + Attachment (Khiêm, 22/10) → màn đơn hàng (Khiêm, 23/10) → nhân viên/tổ (Khiêm, 24/10) → BTP đã cấp cho tổ may (Thái, 25/10, Kiều dùng cho trần) → `IOutputQueryService`, loại đơn cắt bù (Kiều, 27/10) → đơn mua (Minh, 27/10, Thái dùng cho nhập NCC). Trước khi có phần phụ thuộc thì dùng dữ liệu seed.

**Kịch bản demo P2:** UC09 → 25 → 37 → 51 → 55 → 57 → 45 → 61 → 59 → 21 (1 đơn, 1 tổ cắt, 2 tổ may, ~20 công nhân).

## 9. Ngoài phạm vi (không code)

Tính lương chính thức (phụ cấp, bảo hiểm, khấu trừ), thuế/MISA (không trao đổi dữ liệu); kết nối bếp (báo bếp qua điện thoại/Zalo); là/gấp; nghỉ phép; hàng trả về từ công ty mẹ; GPS/IoT, giấy tờ xe; AI nhận diện lỗi; AI dự báo hòa vốn (API-03 chỉ là đề xuất); đa nhà máy; trả lương qua ngân hàng; chế độ offline; thay máy chấm công; khóa tài khoản tự động; QA/QC phân loại Pass/Rework/Scrap; KPI theo tháng cho tổ, thưởng KPI, tỉ lệ lỗi tối đa; giao hàng thất bại.

## 10. Nguồn sự thật, theo thứ tự ưu tiên

1. Quyết định Morgan đã chốt (14–18/10, đã gộp vào file này và vào DB).
2. `NX-MTD_Database.sql` + `NX-MTD_Database_Giai_thich.md` (schema, view, giả định).
3. Report-3 SRS bản 16/10 (UC, FR, GBR, MSG) – bỏ qua phần đã bị ghi đè ở mục 11.
4. `NX-MTD_Coding_Rules.md` (cách code). 5. Report-1, Report-2.

Riêng với **code đã có**: các mục trong `docs/CODE_DEVIATIONS.md` của repo là thay đổi đã thống nhất sau các tài liệu trên, thắng các nguồn 2–5 cho đến khi Report được sửa lại.

## 11. Mâu thuẫn giữa các nguồn và cách đang xử lý

| # | Mâu thuẫn | Đang làm theo |
|---|---|---|
| 1 | Report-1/2 nói hệ thống tính lương; Report-3 GBR-12 nói không | Không tính lương chính thức; chỉ lương tạm tính CN may |
| 2 | SRS NFR + JOB-04 khóa tài khoản 15 phút sau 5 lần sai | Không khóa, không có job mở khóa. JOB-04 là kiểm tra trễ đơn |
| 3 | SRS JOB-01: giờ ra = lượt quẹt cuối; SRS API-01: RJ là SQL DB | Giờ ra = lượt ra đầu tiên; RJ là file Access, đọc bằng agent trên Windows |
| 4 | SRS: Web React + **Mobile Flutter**; Coding Rules + kế hoạch: chỉ web React | Chỉ web React responsive (`nxmtd-web`), màn xưởng dùng tốt trên điện thoại |
| 5 | Coding Rules ghi **ASP.NET Core 8**; Report-2/SRS/DB ghi **.NET 9** | Theo phiên bản trong repo BE (`global.json`/csproj) do Khiêm dựng; nếu chưa có thì .NET 9 |
| 6 | Coding Rules mục 7 (từ điển: `ProductionOrder`, `GoodsReceipt`, `Inventory`, `CuttingRecord`, `Attendance`, `SewingOperation`, `WorkerOutput`, `LineOutput`, `QualityInspection`, `MealRegistration`) khác tên bảng DB | **Theo tên bảng DB**: `CustomerOrder`, `MaterialReceipt`, `StockTransaction`/`vw_StockBalance`, `CuttingLay`, `DailyTimesheet`, `Operation`, `OutputRecord`, `QcBatch`, `DailyMeal`/`TeamMealConfirmation`. API path = kebab số nhiều của entity (`customer-orders`, `material-receipts`, `output-records`, `qc-batches`) |
| 7 | Coding Rules: bảng số nhiều, PK `Id`, `BaseEntity` có `CreatedBy/UpdatedBy/IsDeleted`, soft delete query filter | **Theo DB**: bảng số ít, PK `<Bảng>Id`, chỉ `CreatedAt/UpdatedAt/RowVer`, ai làm gì ghi `AuditLog`, "xóa" = đổi `Status` |
| 8 | Coding Rules: lưu UTC; DB: `fn_VnNow()` lưu giờ VN | DB lưu giờ VN (DATETIME2 không offset) vì mốc 08:15/08:30/hết ngày làm việc là giờ VN; backend dùng một `IClock` trả giờ VN; API trả ISO 8601 kèm `+07:00` |
| 9 | Coding Rules: 6 role (`Admin, Accountant, Storekeeper, TeamLeader, QaQc, Worker`); DB: 9 role | 9 role theo DB: `Admin, Manager, Accountant, WarehouseKeeper, TeamLeader, TeamDeputy, QaQc, Electrician, Worker` (map từ `RoleCode`); folder controller tương ứng |
| 10 | SRS UC45/46: Thủ kho lập phiếu xuất, Quản đốc xác nhận | Theo DB + task TH-14: Quản đốc lập (giữ chỗ), Thủ kho xuất/từ chối, Kế toán xác nhận đã giao |
| 11 | SRS UC19/FR: KPI có tỉ lệ lỗi tối đa và thưởng | Đã bỏ (18/10) |
| 12 | SRS ghi PO do Thủ kho tạo ở vài chỗ | Quản đốc tạo PO |
| 13 | Report-2 lịch iteration (Package V1 23/10…) | Theo kế hoạch phase 19/10 ở mục 2 |

## 12. Còn chưa chốt (để cấu hình, không hard-code)

% cắt dư cho phép và cắt bù có tính vào không (`CUT_SURPLUS_PERCENT` = 3 tạm); phân bổ chi phí cố định khi tính hòa vốn (`AllocationBasis` LABOUR_HOURS / OUTPUT); khoản nào cố định/biến đổi; PO có cần Admin duyệt/hạn mức; Kế toán được sửa suất ăn sau 08:30 không; mẫu Excel bảng lương (nên `PayslipItem` để dạng dòng); in phiếu giao từ điện thoại hay máy tính; tần suất kiểm kê; ngày lễ (hiện chỉ bỏ qua Chủ nhật); lương cơ điện (tạm MONTHLY).

## 13. File tham khảo trong `SEP490/`

- `Claude outputs/NX-MTD_Database.sql`, `NX-MTD_Database_Giai_thich.md`, `NX-MTD_Database.dbml` – schema chi tiết.
- `Claude outputs/NX-MTD_Coding_Rules.md` – quy tắc code đầy đủ + file cấu hình FE.
- `Claude outputs/NX-MTD_Phan_cong_cong_viec.xlsx` – task, hạn, phụ thuộc, map UC01–UC62.
- `01.Báo cáo chính thức/Report-3_Software Requirement Specification.docx` – mô tả UC, FR mockup theo luồng (1 Nhập NVL, 2 Cắt, 3 May, 4 Xuất, 5 Suất ăn, 6 Hòa vốn, 7 Job, 8 API).
- `Ronald Jack Pro/Data/RJData.mdb` – dữ liệu chấm công thật để test agent (bảng `CheckInOut`, `NHANVIEN`). Không copy bảng mật khẩu/vân tay.
