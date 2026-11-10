/* =====================================================================================
   NX-MTD – Nền tảng số quản lý gia công may mặc
   Script tạo database SQL Server (T-SQL) – bản 23/10/2026
   ---------------------------------------------------------------------------------
   Nguồn: Report-3 SRS (bản 16/10), Report-1 (bản 14/10) và các quyết định đã chốt
   14–18/10. Xem file NX-MTD_Database_Giai_thich.md để biết bảng nào map với phần nào
   của SRS và các giả định khi SRS chưa thống nhất.

   Lịch sử thay đổi đã gộp vào script này (không cần chạy migration rời nữa):
   - 16/10/2026  Bản gốc (65 bảng).
   - 21/10/2026  TH-12 (DEV-TH-12-1, Thái) – đơn mua NVL dùng chung nhiều đơn (kim, chỉ...):
                 PurchaseOrder.OrderId, MaterialReceipt.OrderId cho phép NULL;
                 PurchaseOrderLine / MaterialReceiptLine: OrderMaterialId cho phép NULL, thêm
                 MaterialId, FK_POL_Material / FK_MRL_Material, CK_POL_Target /
                 CK_MRL_Target; UQ_POL / UQ_MRL đổi thành filtered unique index, thêm
                 UQ_POL_Stock / UQ_MRL_Stock.
   - 21/10/2026  SeedAuth – tổ may mẫu MAY1 + 10 nhân viên / tài khoản mẫu (mục 17b).
   - 22/10/2026  Thủ tục usp_Attendance_ApplyAdjustment (Kế toán chấm công hộ) có đủ trong script (bản 21/10 bị thiếu).
   - 22/10/2026  Gộp trường trùng lặp (xem NX-MTD_Toi_uu_truong_22-10.md): Material.MaterialType và
                 MaterialReceipt.Source thành cột tính; bỏ ReceiptType, OriginalReceiptId, ToReplenishQty,
                 OrderedAt, PaidAt, DeliveryConfirmedAt, UnitRate, CuttingTeamId (CutHandover, MaterialReturn),
                 SizeRatio, LastSizeRatio, DailyMeal.TeamTotal/TotalMeals/TotalAmount (sang vw_DailyMealTotal),
                 Payroll.ImportedFileUrl/ImportedAt/TotalAmount/ReopenReason, Notification.AnnouncementId;
                 PieceRate.ApprovalNote + RejectReason thành DecisionNote; DefectRecord.ScrapUnitRate thành PieceRateId;
                 VehicleTrip.DriverName chỉ ghi khi tài xế không phải nhân viên; thêm trg_PieceRate_Approved.
   - 22/10/2026  Mục 19: dữ liệu demo các luồng của Minh (mã hàng, đơn hàng, NVL, nhà cung cấp, đơn mua, 188 công đoạn
                 + 110 đơn giá, phân công, sản lượng, QC, hàng hỏng, KPI, chi phí tháng, bảng công, suất ăn, xe,
                 bảng lương demo). Không có họ tên thật, lương thật hay hash mật khẩu. Xóa mục 19 nếu muốn DB sạch.
   - 23/10/2026  Định mức theo mã hàng thay vì theo đơn: bỏ OrderMaterialNorm (và FK sang OrderMaterial); thêm
                 ProductVariant (màu × size), ProductComponent (thành phần PANEL / TRIM), ProductComponentMaterial
                 (định mức NVL + màu NVL + kích thước chi tiết theo từng biến thể × thành phần); OrderLine thêm
                 FK_OrderLine_Variant; WorkOrderLine.PanelName và RequestLine.PanelName thành ProductComponentId;
                 thêm vw_OrderMaterialNorm
                 (đối chiếu RequiredQty với định mức) và vw_WorkOrderFabricNeed (vải cần cho lệnh cắt).
   - 23/10/2026  Rà D3: bỏ FK_OrderLine_Product (đã có FK ghép sang ProductVariant); CustomerOrder.IsDelayRisk thành
                 cột tính từ DelayRiskSince; thêm trg_WorkOrderLine_Component và trg_RequestLine_Component (chi tiết
                 phải thuộc đúng mã hàng); demo bổ sung NVL cho áo Xanh của DEMO-DH001 (vải Xanh, vải Trắng, chỉ Xanh).
   - 23/10/2026  Rà D6 (Morgan chốt): kho BTP đếm cả chi tiết cắt bù. CutHandoverLine, MaterialIssueLine, StocktakeLine,
                 StockTransaction thêm ProductComponentId (NULL = bộ đủ); vw_StockBalance tách theo chi tiết;
                 vw_TeamPieceHolding và vw_OrderLineProgress chỉ đếm bộ đủ; thêm trg_CutHandoverLine_Component,
                 trg_MaterialIssueLine_Component.
   - 23/10/2026  Rà D1-D2, D4-D13: dòng phiếu phải thuộc đúng đơn / mã hàng / phiếu của đầu phiếu (trigger *_Check,
                 THROW 50042-50046); NVL dòng phiếu nhập khớp dòng đơn mua; công đoạn và đơn giá khớp mã hàng của dòng
                 đơn; bỏ PurchaseOrder.OriginalPurchaseOrderId, Account.Username (đăng nhập bằng Employee.EmployeeCode);
                 Notification.IsRead và DailyTimesheet.Source thành cột tính; CuttingLay.FabricMaterialId bắt buộc;
                 thêm CHECK cho RequestLine, StocktakeLine, Account, DispatchNote, Stocktake, Payroll, AccountingPeriod, PieceRate.
   - 23/10/2026  OrderLine trỏ thẳng ProductVariantId (FK ghép ProductVariantId + ProductId), bỏ OrderLine.Color, SizeCode;
                 màu, size của dòng đơn đọc từ ProductVariant.
   - 23/10/2026  Bỏ MaterialReceiptLine.ShortQty, DefectiveQty: số thiếu so với đơn mua xem vw_PurchaseOrderLineStatus;
                 số thiếu / lỗi báo trong RequestLine.Quantity của yêu cầu SHORT_DELIVERY.
   - 23/10/2026  OrderMaterial bỏ CustomerExpectedDate, thêm ExpectedDeliveryCount (số lần khách dự kiến giao NVL);
                 số lần đã nhận và số đã nhận xem vw_OrderMaterialDelivery (đếm phiếu nhập khách cấp đã xác nhận).
   - 23/10/2026  PurchaseOrder bỏ ExpectedDeliveryDate, thêm ExpectedDeliveryCount (số lần NCC dự kiến giao);
                 số lần đã nhận xem vw_PurchaseOrderDelivery (đếm phiếu nhập đã xác nhận của đơn mua).
   - 24/10/2026  CuttingLayLine thêm ProductComponentId (NULL = cả bộ; có = chi tiết cắt bù) để bàn cắt của lệnh cắt bù
                 ghi được chi tiết; vw_OrderLineProgress.CutQty và vw_ProductLastSizeRatio chỉ tính bộ đủ;
                 thêm vw_WorkOrderLineProgress (kế hoạch, đã cắt, đã vào kho, còn lại theo từng dòng lệnh cắt).
   - 25/10/2026  Thêm bảng tồn StockBalance (mỗi mặt hàng / kho một dòng, OnHandQty không âm). StockTransaction và
                 StocktakeLine bỏ các cột mặt hàng, thay bằng StockBalanceId. Ghi kho chỉ qua usp_Stock_Post (khóa dòng
                 tồn, chặn tồn âm, tự điền BalanceAfter); trigger chặn ghi thẻ kho trực tiếp (THROW 50047, 50048).
                 vw_StockBalance đọc tồn từ StockBalance, giữ chỗ vẫn tính từ phiếu đang chờ.
   - 25/10/2026  Thêm OperationGroup (công đoạn to của mã hàng, vd "May tay"); Operation thêm OperationGroupId
                 (NULL = chưa xếp nhóm) để tìm công đoạn to ra các công đoạn nhỏ. Phân công, đơn giá, sản lượng giữ nguyên.
   - 25/10/2026  Thêm OperationComponent (công đoạn làm trên chi tiết nào, nhiều–nhiều, cùng mã hàng).
                 WorkOrderLine và CuttingLayLine đổi ProductComponentId thành ProductComponentMaterialId (NULL = cả bộ;
                 có = chi tiết × NVL × màu × size đang cắt bù). Bàn cắt bù phải cắt đúng vải của bàn (THROW 50049).
   - 25/10/2026  RequestLine, CutHandoverLine, MaterialIssueLine, StockBalance cũng đổi ProductComponentId thành
                 ProductComponentMaterialId: kho BTP tách chi tiết theo vải (vd tay áo vải chính / vải lót).
                 WorkAllocation thay WorkerPieceMovement: giao số lần làm công đoạn × màu × size cho công nhân
                 (Quantity), PieceQty = BTP giao kèm; tổng số lần giao ≤ số bộ BTP tổ đã nhận. Operation thêm
                 UsesSemiFinished. Trần sản lượng theo công đoạn, báo vượt bị chặn (THROW 50050-50053).
   - 25/10/2026  OperationAssignment bỏ CopiedFromAssignmentId.
   - 26/10/2026  Màu NVL chuyển vào Material.Color (mỗi màu là một dòng Material, '' = không phân màu); bỏ MaterialColor ở
                 ProductComponentMaterial, OrderMaterial, RequestLine, PurchaseOrderLine, MaterialReceiptLine, MaterialIssueLine,
                 StockBalance, CuttingLay.FabricColor và tham số @MaterialColor của usp_Stock_Post. Các view bỏ cột màu
                 (vw_MaterialStock đọc Material.Color).
   - 26/10/2026  WorkAllocation bỏ PieceQty, ConfirmedPieceQty; bỏ vw_TeamPieceHolding và THROW 50051, 50053 (mỗi công đoạn nhận
                 riêng từng thành phần nên không đếm được số BTP giao kèm theo bộ đủ). Operation.UsesSemiFinished giữ lại, chỉ để tham khảo.
   - 28/10/2026  Customer thêm Status (ACTIVE / DISCONTINUED), như Supplier. ProductComponent bỏ SortOrder (chỉ để sắp xếp hiển thị).
   - 28/10/2026  ProductComponentMaterial bỏ WastagePercent, CK_PCM_Wastage (QtyPerGarment là lượng thực dùng cho 1 sản phẩm, đã gồm hao hụt);
                 vw_OrderMaterialNorm, vw_WorkOrderFabricNeed không còn nhân hệ số hao hụt. ProductVariant thêm LengthCm, WidthCm
                 (kích thước thành phẩm theo màu × size).
   - 28/10/2026  ProductComponentMaterial bỏ QtyPerGarment, CK_PCM_Qty; bỏ vw_OrderMaterialNorm và vw_WorkOrderFabricNeed
                 (cả hai tính từ QtyPerGarment). Định mức chỉ còn ghi thành phần dùng NVL nào (đã gồm màu) và kích thước chi tiết.
   - 28/10/2026  Material thêm LengthCm, WidthCm (chiều dài, chiều rộng / khổ của cuộn vải, cm).
   - 28/10/2026  Thêm vw_RecutFabricNeed: số mét vải ước tính cho dòng lệnh cắt bù (theo hình chữ nhật bao của chi tiết và khổ vải).
   - 31/10/2026  Thêm KpiDailyTarget, vw_KpiDailyResult đọc KPI theo ngày.
   - 01/11/2026  UQ_Kpi lọc chỉ tiêu đã hủy (trùng kỳ do KpiService kiểm); thêm KPI_EDIT_CUTOFF_TIME.
   - 01/11/2026  Attachment thêm RemovedAt (lúc gỡ ảnh, IsCurrent = 0): job dọn tệp xóa ảnh đã gỡ sau 15 ngày.
   - 01/11/2026  ProductVariant bỏ LengthCm, WidthCm, CK_PV_Size: số mét vải của lệnh cắt tính từ kích thước chi tiết trong ProductComponentMaterial.
   - 01/11/2026  Thêm ProductImage: ảnh phụ của mã hàng, tối đa 10 (ảnh chính vẫn là Product.SampleImageUrl).
   - 01/11/2026  CuttingLay thêm LayLengthM: chiều dài bàn cắt, để tính vải đã cắt của bàn (số lớp × chiều dài) và vải còn lại.
   - 01/11/2026  Thêm ProductMarker, ProductMarkerFabric: bìa cắt theo size và vải của bìa theo màu áo, để tính số mét vải của lệnh cắt cả bộ.
   - 02/11/2026  Thêm OrderLineTeam: Quản đốc chia từng dòng đơn (mã hàng × màu × size) cho tổ may, thay OrderProductTeam (chia theo mã hàng) đã bỏ.
   - 02/11/2026  Ràng buộc biên: CK_Operation_Skill (bậc 1 đến 9), CK_Operation_Sequence (thứ tự > 0); CK_PieceRate_Rate đổi từ >= 0 thành > 0.
   - 02/11/2026  Operation bỏ UsesSemiFinished (từ 26/10 chỉ để tham khảo, không còn quy tắc nào dùng); vw_OperationAllocationProgress bỏ cột này.
   - 02/11/2026  Rà dữ liệu demo mục 19: mã đơn mua PO-DEMO thành DM-DEMO (tiền tố hệ thống); DEMO-DH001 khớp mốc thời gian (lệnh cắt sau ngày
                 nhận đơn, giao việc 600 / 500, sản lượng từ 23/09, QC đạt không vượt sản lượng công đoạn cuối, bàn giao TP sau QC, KPI);
                 bảng công, suất ăn đến 31/10; đơn từ có ngày tạo; ngày giao phiếu xuất, thông báo mẫu theo hiện tại; giá mới đã duyệt
                 hiệu lực 16/11. Thêm mục 19.6: tổ may 2 và lịch sử tổ, công nhân chưa xếp tổ, KPI theo ngày, sản lượng DEMO-DH003.
   - 03/11/2026  Thêm KpiTargetLine: chỉ tiêu KPI chia theo dòng đơn (màu × size); chỉ tiêu demo được chia theo tỉ lệ số còn trống của dòng.
   - 03/11/2026  WorkAllocation thêm WorkDate, RetrievalNote; ConfirmMode thêm LEADER, SYSTEM; RetrievalReason thêm END_OF_DAY, ADJUSTED (giao việc theo ngày).
   - 02/11/2026  Thêm mục 19.7: dữ liệu demo đủ loại và trạng thái để test mọi chức năng (đơn DEMO-DH005…013, đủ 11 loại đơn từ, chấm công, lương, hòa vốn, thông báo); công đoạn cuối DEMO-QU02 chuyển sang công đoạn đang dùng.
   - 02/11/2026  Thêm mục 19.8: dữ liệu thử nhập sản lượng theo nhóm công đoạn (nhóm May túi của DEMO-AO01, CNMAY1, DEMO-DH003 dòng Đen L).
   - 03/11/2026  Phiếu xuất hàng bỏ trạng thái PARTIALLY_DELIVERED và cột DispatchNoteLine.ReceivedQty (khách nhận đúng số xuất);
                 vw_OrderLineProgress tính số đã giao theo số xuất của phiếu DELIVERED (DEV-ALL-70).
   - 09/11/2026  Bìa cắt theo đơn (DEV-ALL-85): ProductMarker thành bìa của đơn (OrderId, MarkerCode, LengthM, Status; bỏ SizeCode, LengthCm),
                 thêm ProductMarkerRatio (tỉ lệ size), ProductMarkerComponent (chi tiết trên nhóm bìa), trigger 50055; ProductComponentMaterial
                 bỏ LengthCm, WidthCm; bỏ vw_RecutFabricNeed, vw_ProductLastSizeRatio. Bàn cắt không gắn bìa.
                 Bỏ Material.WidthCm (khổ vải) và chiều rộng bìa: khổ vải cố định 1,5 m ở backend.
   - 09/11/2026  OperationComponent chỉ gắn chi tiết vải (PANEL) (DEV-ALL-86): vải của chi tiết tra qua ProductComponentMaterial; mỗi chi tiết cắt
                 chỉ một vải ở một màu áo (vải lót, mex, bo khai thành chi tiết riêng, backend kiểm), nên gắn chi tiết là biết vải.
   - 09/11/2026  ProductComponentMaterial theo màu áo, không theo size (DEV-ALL-87): ProductVariantId thay bằng ProductColorId (FK ghép ProductColor),
                 UQ_PCM thành (ProductColorId, ProductComponentId, MaterialId). Size của chứng từ (lệnh cắt, bàn cắt, kho BTP, phiếu cấp) lấy từ
                 OrderLine; trigger 50041 so màu của định mức với màu của dòng đơn.
   - 06/11/2026  Bỏ ảnh theo màu của mã hàng: bỏ bảng ProductColorImage (ProductColor vẫn giữ làm nơi lưu tên màu) (DEV-ALL-80).
   - 06/11/2026  Phiếu lỗi thêm loại OUTPUT_ERR (tổ trưởng nhập sai sản lượng công đoạn): trừ khỏi sản lượng và tiền của công nhân, trần sản lượng và tiến độ làm
                 được tính lại (trg_WorkAllocation_Check, trg_OutputRecord_Check, vw_WorkerOutputCap, vw_OperationAllocationProgress, vw_WorkerDailyEstimatedPay) (DEV-ALL-79).
   - 07/11/2026  Thêm OrderMaterial.ExpectedDeliveryDate (ngày khách dự kiến giao, chỉ khi khách cấp) và PurchaseOrder.ExpectedDeliveryDate (ngày NCC dự kiến giao, không trước ngày đặt); giữ ExpectedDeliveryCount (DEV-ALL-82).
   - 05/11/2026  ProductImage giảm còn tối đa 5 ảnh phụ (CK_ProductImage_Sort 1-5). Thêm ProductColor (màu áo của mã hàng,
                 nơi duy nhất lưu tên màu): ProductVariant, ProductMarkerFabric bỏ cột Color, trỏ ProductColorId (FK ghép
                 với ProductId); UQ_PV thành (ProductColorId, SizeCode). (ảnh theo màu đã bỏ ở DEV-ALL-80).
   Đây là MỘT file duy nhất: ai chưa có database chạy một lần là có đủ schema, tài khoản mẫu và dữ liệu demo.

   Chạy được trên SQL Server 2019 trở lên (đã chạy thử trên SQL Server 2019 CU32).
   Script tạo database mới tên NXMTD. Nếu database đã tồn tại, hãy xóa trước
   (DROP DATABASE NXMTD) hoặc đổi tên ở phần 0.
   Chạy bằng SSMS / Azure Data Studio, hoặc: sqlcmd -S localhost -E -C -I -f 65001 -b -i SQL/NX-MTD_Database.sql
   (cờ -I bật QUOTED_IDENTIFIER, cần cho filtered index; -f 65001 để đọc đúng tiếng Việt).
   Mật khẩu các tài khoản mẫu do backend gán khi khởi động lần đầu (Seed:DefaultPassword, mặc định 123456).

   Quy ước:
   - Tên bảng/cột tiếng Anh (PascalCase) để dùng thẳng với EF Core (.NET 9).
   - Trạng thái và danh mục cố định (ca, size, loại đơn từ, lý do lỗi, khoản chi phí…)
     lưu dạng mã chữ (VARCHAR) có CHECK, không tách bảng danh mục riêng.
   - Bám theo entity của Report-3 (65 bảng); lịch sử trạng thái ghi vào AuditLog.
   - Số lượng sản phẩm/bán thành phẩm: INT. Số lượng NVL: DECIMAL(18,3).
     Tiền: DECIMAL(18,2) (VND).
   - Mọi thời điểm lưu theo giờ Việt Nam qua hàm dbo.fn_VnNow() (server AWS chạy UTC
     vẫn đúng giờ cắt suất ăn 08:30).
   - Không xóa cứng dữ liệu master (GBR-14): dùng cột Status.
   ===================================================================================== */

/* =========================== 0. DATABASE =========================== */
USE master;
GO
IF DB_ID(N'NXMTD') IS NULL
    CREATE DATABASE NXMTD COLLATE Vietnamese_100_CI_AS;
GO
USE NXMTD;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* Giờ Việt Nam hiện tại (UTC+7), dùng làm DEFAULT cho mọi cột thời điểm */
CREATE FUNCTION dbo.fn_VnNow()
RETURNS DATETIME2(0)
AS
BEGIN
    RETURN CONVERT(DATETIME2(0), SYSDATETIMEOFFSET() AT TIME ZONE 'SE Asia Standard Time');
END;
GO

/* =====================================================================================
   1. DANH MỤC (LOOKUP)
   ===================================================================================== */

-- 9 vai trò (Report-1 FE-12, SRS I.4.1; 18/10: gộp Tổ trưởng cắt / may thành TEAM_LEADER, thêm Tổ phó).
-- Tổ cắt hay tổ may do Team.TeamType quyết định, quyền theo (vai trò, loại tổ) do backend ghép.
CREATE TABLE dbo.Role (
    RoleId          INT IDENTITY(1,1) CONSTRAINT PK_Role PRIMARY KEY,
    RoleCode        VARCHAR(30)   NOT NULL CONSTRAINT UQ_Role_Code UNIQUE,
    RoleName        NVARCHAR(100) NOT NULL,
    IsProduction    BIT           NOT NULL CONSTRAINT DF_Role_IsProduction DEFAULT 0 -- khối sản xuất (PRODUCTION_STAFF)
);

-- Tham số hệ thống (giờ chốt cơm, % cắt dư, chu kỳ đồng bộ…)
CREATE TABLE dbo.SystemSetting (
    SettingKey   VARCHAR(50)    NOT NULL CONSTRAINT PK_SystemSetting PRIMARY KEY,
    SettingValue NVARCHAR(200)  NOT NULL,
    Description  NVARCHAR(500)  NULL,
    UpdatedAt    DATETIME2(0)   NOT NULL CONSTRAINT DF_SystemSetting_UpdatedAt DEFAULT (dbo.fn_VnNow())
);

-- Danh mục bậc thợ (A, B, C, D... Admin thêm / sửa / ngừng dùng). Mã bậc là khóa; nhân viên, đánh giá và tiền bậc tham chiếu mã này.
CREATE TABLE dbo.GradeLevelCode (
    GradeCode  VARCHAR(5)    NOT NULL CONSTRAINT PK_GradeLevelCode PRIMARY KEY CONSTRAINT CK_GLC_Code CHECK (GradeCode NOT LIKE '%[^A-Z0-9]%'),
    GradeName  NVARCHAR(50)  NOT NULL,
    Status     VARCHAR(10)   NOT NULL CONSTRAINT DF_GLC_Status DEFAULT 'ACTIVE' CONSTRAINT CK_GLC_Status CHECK (Status IN ('ACTIVE','INACTIVE')),
    CreatedAt  DATETIME2(0)  NOT NULL CONSTRAINT DF_GLC_CreatedAt DEFAULT (dbo.fn_VnNow())
);

-- Tiền bậc theo tháng của từng bậc, có ngày hiệu lực; giữ phiên bản cũ (GRADE_LEVEL, UC15). Tên bậc chỉ lưu ở GradeLevelCode.
CREATE TABLE dbo.GradeLevel (
    GradeLevelId     INT IDENTITY(1,1) CONSTRAINT PK_GradeLevel PRIMARY KEY,
    GradeCode        VARCHAR(5)    NOT NULL CONSTRAINT FK_GradeLevel_Code REFERENCES dbo.GradeLevelCode(GradeCode),
    MonthlyAmount    DECIMAL(18,2) NOT NULL CONSTRAINT CK_GradeLevel_Amount CHECK (MonthlyAmount >= 0),
    EffectiveDate    DATE          NOT NULL,
    CreatedAt        DATETIME2(0)  NOT NULL CONSTRAINT DF_GradeLevel_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT UQ_GradeLevel UNIQUE (GradeCode, EffectiveDate)
);
GO

/* =====================================================================================
   2. NHÂN SỰ, TÀI KHOẢN, TỔ, BẬC THỢ, THÔNG BÁO  (ERD D1)
   ===================================================================================== */

CREATE TABLE dbo.Team (
    TeamId           INT IDENTITY(1,1) CONSTRAINT PK_Team PRIMARY KEY,
    TeamCode         VARCHAR(20)   NOT NULL CONSTRAINT UQ_Team_Code UNIQUE,
    TeamName         NVARCHAR(100) NOT NULL CONSTRAINT UQ_Team_Name UNIQUE,
    TeamType         VARCHAR(10)   NOT NULL,
    Status           VARCHAR(20)   NOT NULL CONSTRAINT DF_Team_Status DEFAULT 'ACTIVE',
    CreatedAt        DATETIME2(0)  NOT NULL CONSTRAINT DF_Team_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_Team_Type   CHECK (TeamType IN ('CUTTING','SEWING','OFFICE')),
    CONSTRAINT CK_Team_Status CHECK (Status IN ('ACTIVE','DISCONTINUED'))
);
-- Tổ trưởng và tổ phó của tổ: nhân viên ACTIVE có vai trò TEAM_LEADER / TEAM_DEPUTY và TeamId = tổ này.
-- Mỗi tổ cắt / may chỉ có tối đa 1 người mỗi vai trò, trigger trg_Employee_TeamLeadership chặn (phần 15).

CREATE TABLE dbo.Employee (
    EmployeeId       INT IDENTITY(1,1) CONSTRAINT PK_Employee PRIMARY KEY,
    EmployeeCode     VARCHAR(20)   NOT NULL CONSTRAINT UQ_Employee_Code UNIQUE,  -- = username
    FullName         NVARCHAR(100) NOT NULL,
    Gender           CHAR(1)       NULL CONSTRAINT CK_Employee_Gender CHECK (Gender IN ('M','F')),
    DateOfBirth      DATE          NULL,
    Phone            VARCHAR(15)   NULL,
    AvatarUrl        NVARCHAR(500) NULL,
    RoleId           INT           NOT NULL CONSTRAINT FK_Employee_Role REFERENCES dbo.Role(RoleId),
    TeamId           INT           NULL CONSTRAINT FK_Employee_Team REFERENCES dbo.Team(TeamId), -- tổ hiện tại (đổi tổ ghi AuditLog)
    PayType          VARCHAR(20)   NOT NULL,   -- PIECE_RATE: CN may; DAILY: thợ cắt, QA/QC; MONTHLY: còn lại
    CurrentGradeCode VARCHAR(5)    NULL CONSTRAINT FK_Employee_Grade REFERENCES dbo.GradeLevelCode(GradeCode), -- chỉ CN may, tổ phó & tổ trưởng
    AttendanceCode   INT           NULL,       -- mã chấm công trên Ronald Jack Pro (NHANVIEN.MaChamCong)
    StartDate        DATE          NOT NULL,
    ResignDate       DATE          NULL,
    Status           VARCHAR(20)   NOT NULL CONSTRAINT DF_Employee_Status DEFAULT 'ACTIVE',
    CreatedAt        DATETIME2(0)  NOT NULL CONSTRAINT DF_Employee_CreatedAt DEFAULT (dbo.fn_VnNow()),
    UpdatedAt        DATETIME2(0)  NULL,
    RowVer           ROWVERSION,
    CONSTRAINT CK_Employee_PayType CHECK (PayType IN ('PIECE_RATE','DAILY','MONTHLY')),
    CONSTRAINT CK_Employee_Status  CHECK (Status IN ('ACTIVE','RESIGNED')),
    CONSTRAINT CK_Employee_Resign  CHECK ((Status = 'RESIGNED' AND ResignDate IS NOT NULL) OR Status = 'ACTIVE')
);
-- Mỗi mã vân tay map đúng 1 nhân viên
CREATE UNIQUE INDEX UX_Employee_AttendanceCode ON dbo.Employee(AttendanceCode) WHERE AttendanceCode IS NOT NULL;
CREATE INDEX IX_Employee_Team ON dbo.Employee(TeamId) INCLUDE (FullName, RoleId, Status);


-- Tài khoản: đúng 1 tài khoản / nhân viên, không khóa tự động khi sai mật khẩu
CREATE TABLE dbo.Account (
    AccountId           INT IDENTITY(1,1) CONSTRAINT PK_Account PRIMARY KEY,
    EmployeeId          INT           NOT NULL CONSTRAINT UQ_Account_Employee UNIQUE
                                      CONSTRAINT FK_Account_Employee REFERENCES dbo.Employee(EmployeeId),
    PasswordHash        NVARCHAR(500) NOT NULL,          -- chỉ lưu hash (ASP.NET Identity / BCrypt)
    IsTemporaryPassword BIT           NOT NULL CONSTRAINT DF_Account_IsTemp DEFAULT 1,
    SecurityStamp       UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Account_Stamp DEFAULT NEWID(), -- đổi khi đổi/đặt lại mật khẩu → đăng xuất phiên khác
    Status              VARCHAR(20)   NOT NULL CONSTRAINT DF_Account_Status DEFAULT 'ACTIVE',
    LockedAt            DATETIME2(0)  NULL,
    LockReason          NVARCHAR(300) NULL,
    CreatedAt           DATETIME2(0)  NOT NULL CONSTRAINT DF_Account_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_Account_Status CHECK (Status IN ('ACTIVE','LOCKED')),
    CONSTRAINT CK_Account_Lock   CHECK (Status <> 'LOCKED' OR LockedAt IS NOT NULL)   -- tên đăng nhập = Employee.EmployeeCode
);

-- Đánh giá / đổi bậc thợ (GRADE_EVALUATION, UC16, UC17, UC50). Tổ trưởng đề xuất, Admin xét;
-- dòng APPLIED chính là lịch sử đổi bậc (bậc cũ, bậc mới, lý do, ngày hiệu lực); người xét ghi ở AuditLog (APPROVE / REJECT).
-- Admin đổi bậc trực tiếp = một dòng do Admin tạo, Status = APPLIED ngay.
CREATE TABLE dbo.GradeEvaluation (
    GradeEvaluationId INT IDENTITY(1,1) CONSTRAINT PK_GradeEvaluation PRIMARY KEY,
    WorkerId          INT           NOT NULL CONSTRAINT FK_GradeEval_Worker REFERENCES dbo.Employee(EmployeeId),
    ProposedBy        INT           NOT NULL CONSTRAINT FK_GradeEval_ProposedBy REFERENCES dbo.Employee(EmployeeId),
    TeamId            INT           NULL CONSTRAINT FK_GradeEval_Team REFERENCES dbo.Team(TeamId),
    CurrentGradeCode  VARCHAR(5)    NULL CONSTRAINT FK_GradeEval_Current REFERENCES dbo.GradeLevelCode(GradeCode),
    ProposedGradeCode VARCHAR(5)    NOT NULL CONSTRAINT FK_GradeEval_Proposed REFERENCES dbo.GradeLevelCode(GradeCode),
    Reason            NVARCHAR(500) NOT NULL,
    RecentDefectCount INT           NOT NULL CONSTRAINT DF_GradeEval_Defects DEFAULT 0,
    EvaluationDate    DATE          NOT NULL,
    Status            VARCHAR(20)   NOT NULL CONSTRAINT DF_GradeEval_Status DEFAULT 'SUBMITTED',
    EffectiveDate     DATE          NULL,     -- ngày bậc mới có hiệu lực (khi APPLIED)
    ConsideredAt      DATETIME2(0)  NULL,
    DecisionNote      NVARCHAR(500) NULL,
    CreatedAt         DATETIME2(0)  NOT NULL CONSTRAINT DF_GradeEval_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_GradeEval_Status CHECK (Status IN ('SUBMITTED','APPLIED','NOT_APPLIED')),
    CONSTRAINT CK_GradeEval_Applied CHECK (Status <> 'APPLIED' OR (EffectiveDate IS NOT NULL AND ConsideredAt IS NOT NULL)),
    CONSTRAINT UQ_GradeEval UNIQUE (WorkerId, ProposedBy, EvaluationDate)
);
CREATE INDEX IX_GradeEval_History ON dbo.GradeEvaluation(WorkerId, EffectiveDate DESC) WHERE Status = 'APPLIED';

-- Thông báo nhanh của Quản đốc/Admin (UC20)
CREATE TABLE dbo.Announcement (
    AnnouncementId         INT IDENTITY(1,1) CONSTRAINT PK_Announcement PRIMARY KEY,
    Content                NVARCHAR(2000) NOT NULL,
    Priority               VARCHAR(10)    NOT NULL CONSTRAINT DF_Announcement_Priority DEFAULT 'NORMAL',
    TargetScope            VARCHAR(10)    NOT NULL,  -- ALL: toàn xưởng; TEAM / ROLE: xem AnnouncementTarget
    SenderId               INT            NOT NULL CONSTRAINT FK_Announcement_Sender REFERENCES dbo.Employee(EmployeeId),
    SentAt                 DATETIME2(0)   NOT NULL CONSTRAINT DF_Announcement_SentAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_Announcement_Priority CHECK (Priority IN ('NORMAL','URGENT')),
    CONSTRAINT CK_Announcement_Scope    CHECK (TargetScope IN ('ALL','TEAM','ROLE'))
);

CREATE TABLE dbo.AnnouncementTarget (
    AnnouncementTargetId INT IDENTITY(1,1) CONSTRAINT PK_AnnouncementTarget PRIMARY KEY,
    AnnouncementId       INT NOT NULL CONSTRAINT FK_AnnTarget_Announcement REFERENCES dbo.Announcement(AnnouncementId),
    TeamId               INT NULL CONSTRAINT FK_AnnTarget_Team REFERENCES dbo.Team(TeamId),
    RoleId               INT NULL CONSTRAINT FK_AnnTarget_Role REFERENCES dbo.Role(RoleId),
    CONSTRAINT CK_AnnTarget_OneOf CHECK ((TeamId IS NOT NULL AND RoleId IS NULL) OR (TeamId IS NULL AND RoleId IS NOT NULL))
);

-- Hộp thư thông báo của từng người, giữ 90 ngày (JOB-05)
CREATE TABLE dbo.Notification (
    NotificationId   BIGINT IDENTITY(1,1) CONSTRAINT PK_Notification PRIMARY KEY,
    RecipientId      INT            NOT NULL CONSTRAINT FK_Notification_Recipient REFERENCES dbo.Employee(EmployeeId),
    SourceType       VARCHAR(20)    NOT NULL,
    RefTable         VARCHAR(50)    NULL,       -- chứng từ liên quan (vd. 'MaterialIssue', 'Announcement')
    RefId            BIGINT         NULL,
    Title            NVARCHAR(200)  NOT NULL,
    Message          NVARCHAR(1000) NOT NULL,
    LinkUrl          NVARCHAR(500)  NULL,       -- deep link tới màn hình liên quan
    ReadAt           DATETIME2(0)   NULL,       -- đọc rồi = có ReadAt
    IsRead           AS (CAST(IIF(ReadAt IS NULL, 0, 1) AS BIT)) PERSISTED,
    PushStatus       VARCHAR(10)    NOT NULL CONSTRAINT DF_Notification_Push DEFAULT 'PENDING',
    CreatedAt        DATETIME2(0)   NOT NULL CONSTRAINT DF_Notification_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_Notification_Source CHECK (SourceType IN ('ANNOUNCEMENT','REQUEST','WAREHOUSE','PRODUCTION','SYSTEM')),
    CONSTRAINT CK_Notification_Push   CHECK (PushStatus IN ('PENDING','SENT','FAILED','SKIPPED'))
);
CREATE INDEX IX_Notification_Inbox ON dbo.Notification(RecipientId, CreatedAt DESC) INCLUDE (IsRead, Title);
CREATE INDEX IX_Notification_CreatedAt ON dbo.Notification(CreatedAt);
GO

/* =====================================================================================
   3. KHÁCH HÀNG, MÃ HÀNG, ĐƠN HÀNG, CÔNG ĐOẠN, ĐƠN GIÁ, KPI  (ERD D2)
   ===================================================================================== */

-- Xưởng chỉ có 1 khách hàng (công ty mẹ), khai báo một lần
CREATE TABLE dbo.Customer (
    CustomerId      INT IDENTITY(1,1) CONSTRAINT PK_Customer PRIMARY KEY,
    CustomerName    NVARCHAR(200) NOT NULL CONSTRAINT UQ_Customer_Name UNIQUE,
    Phone           VARCHAR(15)   NULL,
    Email           VARCHAR(100)  NULL,
    DefaultAddress  NVARCHAR(500) NULL,
    Status          VARCHAR(20)   NOT NULL CONSTRAINT DF_Customer_Status DEFAULT 'ACTIVE',
    DiscontinueReason NVARCHAR(500) NULL,    -- lý do ngừng hợp tác (không bắt buộc, DEV-ALL-76)
    CONSTRAINT CK_Customer_Status CHECK (Status IN ('ACTIVE','DISCONTINUED'))
);

-- Nguyên vật liệu (tạo ngay khi nhập phiếu nhập – UC38). Người dùng chỉ chọn Category (loại chi tiết);
-- MaterialType (FABRIC: vải, đi qua tổ cắt; TRIM: phụ liệu, cấp thẳng cho tổ may) là cột tính từ Category (22/10)
CREATE TABLE dbo.Material (
    MaterialId    INT IDENTITY(1,1) CONSTRAINT PK_Material PRIMARY KEY,
    MaterialCode  VARCHAR(30)   NOT NULL CONSTRAINT UQ_Material_Code UNIQUE,
    MaterialName  NVARCHAR(200) NOT NULL,
    Color         NVARCHAR(50)  NOT NULL CONSTRAINT DF_Material_Color DEFAULT N'',   -- màu NVL; '' = không phân màu. Cùng loại vải khác màu là hai dòng Material
    MaterialType  AS (CAST(CASE WHEN Category IN ('MAIN_FABRIC','LINING','INTERLINING','RIB') THEN 'FABRIC' ELSE 'TRIM' END
                          AS VARCHAR(10))) PERSISTED,
    Category      VARCHAR(15)   NOT NULL,
    Unit          NVARCHAR(20)  NOT NULL,     -- m, kg, cuộn, cái, cuộn chỉ…
    Status        VARCHAR(20)   NOT NULL CONSTRAINT DF_Material_Status DEFAULT 'ACTIVE',
    CreatedAt     DATETIME2(0)  NOT NULL CONSTRAINT DF_Material_CreatedAt DEFAULT (dbo.fn_VnNow()),
    -- vải: vải chính, vải lót, mex, bo; phụ liệu: chỉ, khóa, cúc, cúc dập, chun, dây / viền, nhãn, thẻ treo, bông / đệm, bao bì, khác
    CONSTRAINT CK_Material_Category CHECK (Category IN ('MAIN_FABRIC','LINING','INTERLINING','RIB',
                                                        'THREAD','ZIPPER','BUTTON','SNAP','ELASTIC','TAPE',
                                                        'LABEL','HANG_TAG','PADDING','PACKAGING','OTHER')),
    CONSTRAINT CK_Material_Status CHECK (Status IN ('ACTIVE','DISCONTINUED')),
    CONSTRAINT UQ_Material_NameType UNIQUE (MaterialName, MaterialType, Color)
);

CREATE TABLE dbo.Supplier (
    SupplierId     INT IDENTITY(1,1) CONSTRAINT PK_Supplier PRIMARY KEY,
    SupplierName   NVARCHAR(200) NOT NULL CONSTRAINT UQ_Supplier_Name UNIQUE,
    Phone          VARCHAR(15)   NULL,
    Email          VARCHAR(100)  NULL,
    Address        NVARCHAR(500) NULL,
    MaterialTypes  NVARCHAR(200) NULL,       -- loại NVL cung cấp (mô tả)
    Note           NVARCHAR(500) NULL,
    Status         VARCHAR(20)   NOT NULL CONSTRAINT DF_Supplier_Status DEFAULT 'ACTIVE',
    DiscontinueReason NVARCHAR(500) NULL,    -- lý do ngừng hợp tác (không bắt buộc, DEV-ALL-76)
    CreatedAt      DATETIME2(0)  NOT NULL CONSTRAINT DF_Supplier_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_Supplier_Status CHECK (Status IN ('ACTIVE','DISCONTINUED'))
);

-- Mã hàng (style)
CREATE TABLE dbo.Product (
    ProductId       INT IDENTITY(1,1) CONSTRAINT PK_Product PRIMARY KEY,
    ProductCode     VARCHAR(30)    NOT NULL CONSTRAINT UQ_Product_Code UNIQUE,
    ProductName     NVARCHAR(200)  NOT NULL,
    SampleImageUrl  NVARCHAR(500)  NULL,
    Description     NVARCHAR(1000) NULL,
    Status          VARCHAR(20)    NOT NULL CONSTRAINT DF_Product_Status DEFAULT 'ACTIVE',
    CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_Product_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_Product_Status CHECK (Status IN ('ACTIVE','DISCONTINUED'))
);

-- Màu áo của mã hàng (05/11, DEV-ALL-71): nơi duy nhất lưu tên màu. Biến thể (màu × size), vải của bìa theo màu
-- và ảnh theo màu trỏ về đây bằng ProductColorId, nên tên màu không bị chép ra nhiều bảng rồi lệch nhau.
-- Màu được tạo khi đơn hàng có màu mới, hoặc khi khai ảnh cho màu ở form mã hàng.
CREATE TABLE dbo.ProductColor (
    ProductColorId  INT IDENTITY(1,1) CONSTRAINT PK_ProductColor PRIMARY KEY,
    ProductId       INT           NOT NULL CONSTRAINT FK_PCol_Product REFERENCES dbo.Product(ProductId),
    Color           NVARCHAR(50)  NOT NULL,   -- vd Xanh navy; không trùng trong một mã hàng (không phân biệt hoa thường)
    CreatedAt       DATETIME2(0)  NOT NULL CONSTRAINT DF_PCol_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT UQ_PCol UNIQUE (ProductId, Color),
    CONSTRAINT UQ_PCol_IdProduct UNIQUE (ProductColorId, ProductId)   -- cho FK ghép ở ProductVariant, ProductComponentMaterial và ProductMarkerFabric
);

-- Ảnh phụ của mã hàng (01/11, 05/11 giảm còn 5): ảnh chính là Product.SampleImageUrl; ở đây tối đa 5 ảnh phụ (SortOrder 1-5, mỗi vị trí một ảnh).
CREATE TABLE dbo.ProductImage (
    ProductImageId  INT IDENTITY(1,1) CONSTRAINT PK_ProductImage PRIMARY KEY,
    ProductId       INT           NOT NULL CONSTRAINT FK_ProductImage_Product REFERENCES dbo.Product(ProductId),
    ImageKey        NVARCHAR(500) NOT NULL,   -- khóa ảnh tải từ máy, vd PRODUCT/2026/11/<guid>.jpg
    SortOrder       TINYINT       NOT NULL,   -- thứ tự hiển thị 1-5
    CreatedAt       DATETIME2(0)  NOT NULL CONSTRAINT DF_ProductImage_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_ProductImage_Sort CHECK (SortOrder BETWEEN 1 AND 5),
    CONSTRAINT UQ_ProductImage_Sort UNIQUE (ProductId, SortOrder)
);

-- Biến thể của mã hàng (23/10): một màu × một size, vd Áo khoác – Xanh – S.
-- OrderLine trỏ về đây bằng ProductVariantId nên đơn chỉ dùng được biến thể đã khai báo định mức.
CREATE TABLE dbo.ProductVariant (
    ProductVariantId  INT IDENTITY(1,1) CONSTRAINT PK_ProductVariant PRIMARY KEY,
    ProductId         INT           NOT NULL CONSTRAINT FK_PV_Product REFERENCES dbo.Product(ProductId),
    ProductColorId    INT           NOT NULL,        -- màu sản phẩm (05/11: thay cột Color, DEV-ALL-71)
    SizeCode          VARCHAR(10)   NOT NULL,        -- XS, S, M, L, XL, XXL, 3XL, FREE…
    Status            VARCHAR(20)   NOT NULL CONSTRAINT DF_PV_Status DEFAULT 'ACTIVE',
    CONSTRAINT CK_PV_Status CHECK (Status IN ('ACTIVE','DISCONTINUED')),
    CONSTRAINT FK_PV_Color FOREIGN KEY (ProductColorId, ProductId) REFERENCES dbo.ProductColor(ProductColorId, ProductId),  -- màu của đúng mã hàng
    CONSTRAINT UQ_PV UNIQUE (ProductColorId, SizeCode),
    CONSTRAINT UQ_PV_IdProduct UNIQUE (ProductVariantId, ProductId)   -- cho FK ghép ở OrderLine
);

-- Thành phần của mã hàng (23/10): sản phẩm gồm những gì. Khai báo một lần cho mã hàng, dùng chung mọi màu / size.
-- PANEL: chi tiết cắt từ vải, đi qua tổ cắt (thân trước, thân sau, tay áo, cổ…);
-- TRIM: phụ liệu gắn vào sản phẩm, cấp thẳng cho tổ may (khóa, cúc, nhãn, chỉ may…)
CREATE TABLE dbo.ProductComponent (
    ProductComponentId  INT IDENTITY(1,1) CONSTRAINT PK_ProductComponent PRIMARY KEY,
    ProductId           INT           NOT NULL CONSTRAINT FK_PC_Product REFERENCES dbo.Product(ProductId),
    ComponentName       NVARCHAR(100) NOT NULL,
    ComponentType       VARCHAR(10)   NOT NULL,
    PiecesPerGarment    SMALLINT      NOT NULL CONSTRAINT DF_PC_Pieces DEFAULT 1,   -- số chi tiết trên 1 sản phẩm, vd tay áo = 2
    Status              VARCHAR(20)   NOT NULL CONSTRAINT DF_PC_Status DEFAULT 'ACTIVE',
    CONSTRAINT CK_PC_Type   CHECK (ComponentType IN ('PANEL','TRIM')),
    CONSTRAINT CK_PC_Pieces CHECK (PiecesPerGarment > 0),
    CONSTRAINT CK_PC_Status CHECK (Status IN ('ACTIVE','DISCONTINUED')),
    CONSTRAINT UQ_PC UNIQUE (ProductId, ComponentName),
    CONSTRAINT UQ_PC_IdProduct UNIQUE (ProductComponentId, ProductId)  -- cho FK ghép ở ProductComponentMaterial
);

-- Định mức NVL theo màu áo (23/10, thay OrderMaterialNorm; 09/11 theo màu, không theo size, DEV-ALL-87): với mỗi màu áo và mỗi thành phần,
-- dùng NVL nào (đã gồm màu, vì màu là cột của Material); 28/10 bỏ QtyPerGarment, 09/11 bỏ kích thước chi tiết (DEV-ALL-85).
-- Mọi size của một màu dùng chung định mức; size của chứng từ lấy từ dòng đơn (OrderLine). Một phụ liệu có thể dùng nhiều NVL;
-- chi tiết cắt (PANEL) chỉ một vải ở một màu (DEV-ALL-86, backend kiểm; vải lót, mex, bo khai thành chi tiết riêng).
-- ProductId lặp lại để FK ghép bảo đảm màu và thành phần cùng một mã hàng.
CREATE TABLE dbo.ProductComponentMaterial (
    ProductComponentMaterialId INT IDENTITY(1,1) CONSTRAINT PK_ProductComponentMaterial PRIMARY KEY,
    ProductId           INT            NOT NULL,
    ProductColorId      INT            NOT NULL,     -- màu áo (09/11 thay ProductVariantId, DEV-ALL-87)
    ProductComponentId  INT            NOT NULL,
    MaterialId          INT            NOT NULL CONSTRAINT FK_PCM_Material REFERENCES dbo.Material(MaterialId),
    SpecNote            NVARCHAR(200)  NULL,         -- ghi chú quy cách khác
    CONSTRAINT FK_PCM_Color     FOREIGN KEY (ProductColorId, ProductId)     REFERENCES dbo.ProductColor(ProductColorId, ProductId),
    CONSTRAINT FK_PCM_Component FOREIGN KEY (ProductComponentId, ProductId) REFERENCES dbo.ProductComponent(ProductComponentId, ProductId),
    CONSTRAINT UQ_PCM UNIQUE (ProductColorId, ProductComponentId, MaterialId)
);
CREATE INDEX IX_PCM_Material ON dbo.ProductComponentMaterial(MaterialId);

-- Nhóm bìa nào, với màu áo nào, trải trên vải nào (DEV-ALL-42, DEV-ALL-85). Khai một lần ở mã hàng. MarkerNo là nhóm bìa (thân, tay...),
-- khớp ProductMarker (bìa của đơn) và ProductMarkerComponent theo (ProductId, MarkerNo); không có bảng nhóm bìa riêng nên không có FK.
CREATE TABLE dbo.ProductMarkerFabric (
    ProductMarkerFabricId INT IDENTITY(1,1) CONSTRAINT PK_ProductMarkerFabric PRIMARY KEY,
    ProductId           INT            NOT NULL CONSTRAINT FK_PMF_Product REFERENCES dbo.Product(ProductId),
    MarkerNo            TINYINT        NOT NULL,
    ProductColorId      INT            NOT NULL,     -- màu áo (05/11: thay cột Color, DEV-ALL-71)
    MaterialId          INT            NOT NULL CONSTRAINT FK_PMF_Material REFERENCES dbo.Material(MaterialId),
    CONSTRAINT CK_PMF_No CHECK (MarkerNo > 0),
    CONSTRAINT FK_PMF_Color FOREIGN KEY (ProductColorId, ProductId) REFERENCES dbo.ProductColor(ProductColorId, ProductId),
    CONSTRAINT UQ_PMF UNIQUE (ProductId, MarkerNo, ProductColorId)
);
CREATE INDEX IX_PMF_Material ON dbo.ProductMarkerFabric(MaterialId);

-- Chi tiết nằm trên nhóm bìa nào (DEV-ALL-85), khai một lần ở mã hàng; dùng ước mét cho dòng cắt lẻ / cắt bù.
-- Một chi tiết có thể nằm trên nhiều nhóm (vd tay áo vải chính ở nhóm 2, vải lót ở nhóm 3).
CREATE TABLE dbo.ProductMarkerComponent (
    ProductId          INT     NOT NULL,
    MarkerNo           TINYINT NOT NULL,
    ProductComponentId INT     NOT NULL,
    CONSTRAINT PK_ProductMarkerComponent PRIMARY KEY (ProductId, MarkerNo, ProductComponentId),
    CONSTRAINT CK_PMC_No CHECK (MarkerNo > 0),
    CONSTRAINT FK_PMC_Component FOREIGN KEY (ProductComponentId, ProductId) REFERENCES dbo.ProductComponent(ProductComponentId, ProductId)
);
CREATE INDEX IX_PMC_Component ON dbo.ProductMarkerComponent(ProductComponentId);

-- Đơn hàng gia công (ORDER) – gốc của mọi dữ liệu sản xuất, kho, tiến độ, doanh thu
CREATE TABLE dbo.CustomerOrder (
    OrderId          INT IDENTITY(1,1) CONSTRAINT PK_CustomerOrder PRIMARY KEY,
    OrderCode        VARCHAR(30)    NOT NULL CONSTRAINT UQ_CustomerOrder_Code UNIQUE,
    CustomerId       INT            NOT NULL CONSTRAINT FK_CustomerOrder_Customer REFERENCES dbo.Customer(CustomerId),
    ReceivedDate     DATE           NOT NULL,
    DueDate          DATE           NOT NULL,
    MaterialStatus   VARCHAR(10)    NOT NULL CONSTRAINT DF_CustomerOrder_MatStatus DEFAULT 'AWAITING', -- tiến độ NVL cả đơn, tách khỏi Status; backend cập nhật khi xác nhận phiếu nhập
    Status           VARCHAR(25)    NOT NULL CONSTRAINT DF_CustomerOrder_Status DEFAULT 'NEW',
    DelayRiskSince   DATE           NULL,     -- JOB-04 ghi ngày bắt đầu có nguy cơ trễ, xóa khi hết nguy cơ
    IsDelayRisk      AS (CAST(IIF(DelayRiskSince IS NULL, 0, 1) AS BIT)), -- cờ, không phải trạng thái; tính từ DelayRiskSince
    StopReason       NVARCHAR(500)  NULL,
    Note             NVARCHAR(1000) NULL,
    CreatedAt        DATETIME2(0)   NOT NULL CONSTRAINT DF_CustomerOrder_CreatedAt DEFAULT (dbo.fn_VnNow()),
    UpdatedAt        DATETIME2(0)   NULL,
    RowVer           ROWVERSION,
    CONSTRAINT CK_CustomerOrder_MatStatus CHECK (MaterialStatus IN ('AWAITING','PARTIAL','COMPLETE')),
    CONSTRAINT CK_CustomerOrder_Status CHECK (Status IN ('NEW','IN_PRODUCTION',
                                                         'IN_DELIVERY','PARTIALLY_DELIVERED','DELIVERED','COMPLETED','CANCELLED','STOPPED')),
    CONSTRAINT CK_CustomerOrder_Dates  CHECK (DueDate >= ReceivedDate),
    CONSTRAINT CK_CustomerOrder_Stop   CHECK (Status <> 'STOPPED' OR StopReason IS NOT NULL)
);
CREATE INDEX IX_CustomerOrder_Status ON dbo.CustomerOrder(Status, DueDate);

-- Dòng đơn: một biến thể (mã hàng – màu – size) – đơn vị trung tâm cho cắt, sản lượng, QC, kho TP, giao hàng.
-- Màu, size đọc từ ProductVariant. ProductId giữ lại để lọc theo mã hàng; FK ghép bảo đảm khớp với biến thể.
CREATE TABLE dbo.OrderLine (
    OrderLineId      INT IDENTITY(1,1) CONSTRAINT PK_OrderLine PRIMARY KEY,
    OrderId          INT           NOT NULL CONSTRAINT FK_OrderLine_Order REFERENCES dbo.CustomerOrder(OrderId),
    ProductVariantId INT           NOT NULL,
    ProductId        INT           NOT NULL,      -- mã hàng của biến thể
    OrderedQty     INT           NOT NULL CONSTRAINT CK_OrderLine_Qty CHECK (OrderedQty > 0),
    CmtUnitPrice   DECIMAL(18,2) NOT NULL CONSTRAINT CK_OrderLine_Price CHECK (CmtUnitPrice >= 0), -- đơn giá gia công, tầng mật
    CONSTRAINT UQ_OrderLine UNIQUE (OrderId, ProductVariantId),
    CONSTRAINT FK_OrderLine_Variant FOREIGN KEY (ProductVariantId, ProductId)
        REFERENCES dbo.ProductVariant(ProductVariantId, ProductId)  -- biến thể đã khai báo, đúng mã hàng
);
CREATE INDEX IX_OrderLine_Product ON dbo.OrderLine(ProductId);

-- Bìa cắt của đơn (DEV-ALL-85): bìa do phía trên giác theo tỉ lệ size của từng đơn. Mỗi bìa thuộc một đơn × mã hàng × nhóm bìa
-- (MarkerNo, khớp ProductMarkerFabric / ProductMarkerComponent theo (ProductId, MarkerNo)), có mã trong nhóm (1A, 1B...) và
-- chiều dài cố định; tỉ lệ size ở ProductMarkerRatio. Chỉ dùng để ước số mét vải khi lập lệnh cắt: mét = số lớp × LengthM,
-- số lớp = max theo size của ⌈cần ÷ PiecesPerPly⌉. Bàn cắt không gắn bìa (một lần nhập có thể dùng nhiều bìa): số mét và
-- sản lượng thực tế lấy từ CuttingLay.PlyCount × LayLengthM và CuttingLayLine.
CREATE TABLE dbo.ProductMarker (
    ProductMarkerId INT IDENTITY(1,1) CONSTRAINT PK_ProductMarker PRIMARY KEY,
    OrderId         INT           NOT NULL CONSTRAINT FK_PM_Order REFERENCES dbo.CustomerOrder(OrderId),
    ProductId       INT           NOT NULL CONSTRAINT FK_PM_Product REFERENCES dbo.Product(ProductId),
    MarkerNo        TINYINT       NOT NULL,   -- nhóm bìa của mã hàng (thân, tay, lót...)
    MarkerCode      VARCHAR(20)   NOT NULL,   -- mã bìa trong nhóm, vd 1A, 1B
    LengthM         DECIMAL(7,2)  NOT NULL,   -- chiều dài bìa (mét)
    Status          VARCHAR(20)   NOT NULL CONSTRAINT DF_PM_Status DEFAULT 'ACTIVE',
    CONSTRAINT CK_PM_No     CHECK (MarkerNo > 0),
    CONSTRAINT CK_PM_Length CHECK (LengthM > 0),
    CONSTRAINT CK_PM_Status CHECK (Status IN ('ACTIVE','DISCONTINUED')),
    CONSTRAINT UQ_PM UNIQUE (OrderId, ProductId, MarkerNo, MarkerCode)
);
CREATE INDEX IX_PM_Product ON dbo.ProductMarker(ProductId);

-- Tỉ lệ size của bìa (DEV-ALL-85): số sản phẩm mỗi size trên một lớp vải. Size không có dòng = bìa không cắt size đó.
CREATE TABLE dbo.ProductMarkerRatio (
    ProductMarkerId INT          NOT NULL CONSTRAINT FK_PMR_Marker REFERENCES dbo.ProductMarker(ProductMarkerId),
    SizeCode        VARCHAR(10)  NOT NULL,
    PiecesPerPly    SMALLINT     NOT NULL CONSTRAINT CK_PMR_Pieces CHECK (PiecesPerPly > 0),
    CONSTRAINT PK_ProductMarkerRatio PRIMARY KEY (ProductMarkerId, SizeCode)
);

-- NVL của đơn (18/10): mỗi dòng = một NVL + màu mà đơn cần. Là bảng trung tâm của phần NVL:
-- phần khách cấp (màn 1.2), dòng đơn mua và dòng phiếu nhập đều trỏ về đây.
-- 23/10: định mức chuyển sang ProductComponentMaterial (theo mã hàng). Hai bên khớp theo MaterialId (đã gồm màu),
-- không có FK. RequiredQty nhập tay theo số khách báo.
-- Nguồn suy ra từ số lượng: CustomerQty = 0 là tự mua, CustomerQty >= RequiredQty là khách cấp, còn lại là cả hai.
CREATE TABLE dbo.OrderMaterial (
    OrderMaterialId      INT IDENTITY(1,1) CONSTRAINT PK_OrderMaterial PRIMARY KEY,
    OrderId              INT            NOT NULL CONSTRAINT FK_OM_Order REFERENCES dbo.CustomerOrder(OrderId),
    MaterialId           INT            NOT NULL CONSTRAINT FK_OM_Material REFERENCES dbo.Material(MaterialId),
    RequiredQty          DECIMAL(18,3)  NOT NULL,                                        -- tổng cần theo số liệu khách báo, nhập tay (không tính từ định mức)
    CustomerQty          DECIMAL(18,3)  NOT NULL CONSTRAINT DF_OM_Customer DEFAULT 0,   -- khách sẽ cấp (màn 1.2)
    ExpectedDeliveryCount SMALLINT      NULL,                                            -- số lần khách dự kiến giao NVL này; số lần đã nhận xem vw_OrderMaterialDelivery
    ExpectedDeliveryDate DATE           NULL,                                            -- ngày dự kiến khách giao (nhập khi khách có cấp; 07/11/2026)
    PurchaseQty          AS (CASE WHEN RequiredQty > CustomerQty THEN RequiredQty - CustomerQty ELSE 0 END), -- phần xưởng phải mua
    Source               AS (CONVERT(VARCHAR(10), CASE WHEN CustomerQty = 0 THEN 'SUPPLIER'
                                                       WHEN CustomerQty >= RequiredQty THEN 'CUSTOMER' ELSE 'BOTH' END)),
    Note                 NVARCHAR(500)  NULL,
    CONSTRAINT CK_OM_Qty  CHECK (RequiredQty >= 0 AND CustomerQty >= 0),
    CONSTRAINT CK_OM_Delivery CHECK ((CustomerQty = 0 AND ExpectedDeliveryCount IS NULL) OR (CustomerQty > 0 AND ExpectedDeliveryCount IS NOT NULL AND ExpectedDeliveryCount >= 1)), -- khách có cấp thì phải khai số lần giao
    CONSTRAINT CK_OM_DeliveryDate CHECK (ExpectedDeliveryDate IS NULL OR CustomerQty > 0),
    CONSTRAINT UQ_OM UNIQUE (OrderId, MaterialId)
);

-- Công đoạn to của mã hàng (25/10), vd "May tay" gồm các công đoạn nhỏ trong Operation.
-- Chỉ để nhóm và tìm kiếm; phân công, đơn giá, sản lượng vẫn ghi theo công đoạn nhỏ (Operation)
CREATE TABLE dbo.OperationGroup (
    OperationGroupId INT IDENTITY(1,1) CONSTRAINT PK_OperationGroup PRIMARY KEY,
    ProductId        INT           NOT NULL CONSTRAINT FK_OG_Product REFERENCES dbo.Product(ProductId),
    GroupName        NVARCHAR(200) NOT NULL,
    CONSTRAINT UQ_OG_Name UNIQUE (ProductId, GroupName),
    CONSTRAINT UQ_OG_Product UNIQUE (OperationGroupId, ProductId)   -- đích của FK ghép từ Operation
);
CREATE INDEX IX_OG_Name ON dbo.OperationGroup(GroupName);

-- Công đoạn của mã hàng (OPERATION)
CREATE TABLE dbo.Operation (
    OperationId      INT IDENTITY(1,1) CONSTRAINT PK_Operation PRIMARY KEY,
    ProductId        INT           NOT NULL CONSTRAINT FK_Operation_Product REFERENCES dbo.Product(ProductId),
    OperationGroupId INT           NULL,      -- công đoạn to chứa công đoạn này; NULL = chưa xếp nhóm (25/10)
    OperationCode    VARCHAR(30)   NOT NULL,
    OperationName    NVARCHAR(200) NOT NULL,
    StationSequence  SMALLINT      NOT NULL CONSTRAINT CK_Operation_Sequence CHECK (StationSequence > 0),
    SkillLevel       TINYINT       NULL CONSTRAINT CK_Operation_Skill CHECK (SkillLevel IS NULL OR SkillLevel BETWEEN 1 AND 9),
    StandardTimeSec  DECIMAL(9,2)  NULL CONSTRAINT CK_Operation_Time CHECK (StandardTimeSec IS NULL OR StandardTimeSec > 0),
    TeamType         VARCHAR(10)   NOT NULL CONSTRAINT DF_Operation_TeamType DEFAULT 'SEWING',
    IsFinalStep      BIT           NOT NULL CONSTRAINT DF_Operation_Final DEFAULT 0,
    Status           VARCHAR(20)   NOT NULL CONSTRAINT DF_Operation_Status DEFAULT 'ACTIVE',
    CONSTRAINT CK_Operation_TeamType CHECK (TeamType IN ('CUTTING','SEWING')),
    CONSTRAINT CK_Operation_Status   CHECK (Status IN ('ACTIVE','DISCONTINUED')),
    CONSTRAINT UQ_Operation UNIQUE (ProductId, OperationCode),
    CONSTRAINT UQ_Operation_IdProduct UNIQUE (OperationId, ProductId),   -- đích của FK ghép từ OperationComponent
    CONSTRAINT FK_Operation_Group FOREIGN KEY (OperationGroupId, ProductId) REFERENCES dbo.OperationGroup(OperationGroupId, ProductId) -- nhóm cùng mã hàng
);
CREATE INDEX IX_Operation_Group ON dbo.Operation(OperationGroupId) WHERE OperationGroupId IS NOT NULL;

-- Công đoạn làm trên chi tiết vải nào (25/10; 09/11 chỉ chi tiết PANEL, DEV-ALL-86), nhiều–nhiều: "Lộn tay" → Tay áo; "Tra tay" → Tay áo + Thân trước + Thân sau.
-- Vải của chi tiết tra qua ProductComponentMaterial: mỗi chi tiết cắt chỉ một vải ở một màu áo (vải lót, mex, bo khai thành
-- chi tiết riêng, vd "Lót tay áo", "Mex cổ"), backend kiểm. Công đoạn không gắn chi tiết (là, đi lấy hàng, kiểm hàng, gắn mác) thì không có dòng.
-- Công đoạn và chi tiết phải cùng mã hàng
CREATE TABLE dbo.OperationComponent (
    OperationId        INT NOT NULL,
    ProductComponentId INT NOT NULL,
    ProductId          INT NOT NULL,
    CONSTRAINT PK_OperationComponent PRIMARY KEY (OperationId, ProductComponentId),
    CONSTRAINT FK_OC_Operation FOREIGN KEY (OperationId, ProductId)        REFERENCES dbo.Operation(OperationId, ProductId),
    CONSTRAINT FK_OC_Component FOREIGN KEY (ProductComponentId, ProductId) REFERENCES dbo.ProductComponent(ProductComponentId, ProductId)
);
CREATE INDEX IX_OC_Component ON dbo.OperationComponent(ProductComponentId);
-- Đúng một công đoạn may cuối cho mỗi mã hàng
CREATE UNIQUE INDEX UX_Operation_FinalStep ON dbo.Operation(ProductId) WHERE IsFinalStep = 1 AND Status = 'ACTIVE';

-- Đơn giá công đoạn, có ngày hiệu lực và quy trình duyệt (UC25, UC26)
CREATE TABLE dbo.PieceRate (
    PieceRateId    INT IDENTITY(1,1) CONSTRAINT PK_PieceRate PRIMARY KEY,
    OperationId    INT           NOT NULL CONSTRAINT FK_PieceRate_Operation REFERENCES dbo.Operation(OperationId),
    Rate           DECIMAL(18,2) NOT NULL CONSTRAINT CK_PieceRate_Rate CHECK (Rate > 0),   -- cột TIỀN bằng 0 trong Excel là chưa có đơn giá
    EffectiveDate  DATE          NOT NULL,
    Status         VARCHAR(20)   NOT NULL CONSTRAINT DF_PieceRate_Status DEFAULT 'DRAFT',
    SubmittedAt    DATETIME2(0)  NULL,
    ApprovedAt     DATETIME2(0)  NULL,
    DecisionNote   NVARCHAR(500) NULL,   -- ghi chú khi duyệt; lý do khi từ chối (bắt buộc), giống Request
    CONSTRAINT CK_PieceRate_Status CHECK (Status IN ('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED')),
    CONSTRAINT CK_PieceRate_Reject CHECK (Status <> 'REJECTED' OR DecisionNote IS NOT NULL),
    CONSTRAINT CK_PieceRate_Approved CHECK (Status <> 'APPROVED' OR ApprovedAt IS NOT NULL),
    CONSTRAINT UQ_PieceRate UNIQUE (OperationId, EffectiveDate)
);

-- KPI tổ may theo đơn hàng + mã hàng + kỳ (UC19, FR 3.1). Tổ cắt không có KPI (mục tiêu là lệnh cắt)
CREATE TABLE dbo.KpiTarget (
    KpiTargetId      INT IDENTITY(1,1) CONSTRAINT PK_KpiTarget PRIMARY KEY,
    OrderId          INT           NOT NULL CONSTRAINT FK_Kpi_Order REFERENCES dbo.CustomerOrder(OrderId),
    ProductId        INT           NOT NULL CONSTRAINT FK_Kpi_Product REFERENCES dbo.Product(ProductId),
    SewingTeamId     INT           NOT NULL CONSTRAINT FK_Kpi_Team REFERENCES dbo.Team(TeamId),
    StartDate        DATE          NOT NULL,
    EndDate          DATE          NOT NULL,
    TargetFinishedQty INT          NOT NULL CONSTRAINT CK_Kpi_Target CHECK (TargetFinishedQty > 0),
    WorkingDays      SMALLINT      NOT NULL CONSTRAINT CK_Kpi_Days CHECK (WorkingDays > 0), -- chỉ tiêu ngày = Target / WorkingDays
    Status           VARCHAR(20)   NOT NULL CONSTRAINT DF_Kpi_Status DEFAULT 'ACTIVE',
    Result           VARCHAR(20)   NULL,
    CreatedAt        DATETIME2(0)  NOT NULL CONSTRAINT DF_Kpi_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_Kpi_Dates  CHECK (EndDate >= StartDate),
    CONSTRAINT CK_Kpi_Status CHECK (Status IN ('ACTIVE','CLOSED','CANCELLED')),
    CONSTRAINT CK_Kpi_Result CHECK (Result IS NULL OR Result IN ('ACHIEVED','NOT_ACHIEVED'))
);
GO
-- Một đơn + mã hàng + tổ có nhiều chỉ tiêu theo các kỳ không trùng ngày (KpiService kiểm); chỉ tiêu đã hủy không tính
CREATE UNIQUE INDEX UQ_Kpi ON dbo.KpiTarget(OrderId, ProductId, SewingTeamId, StartDate) WHERE Status <> 'CANCELLED';
GO

-- KPI theo từng ngày của một chỉ tiêu (lịch KPI); chưa đặt ngày nào thì chỉ tiêu ngày chia đều theo WorkingDays
CREATE TABLE dbo.KpiDailyTarget (
    KpiDailyTargetId INT IDENTITY(1,1) CONSTRAINT PK_KpiDailyTarget PRIMARY KEY,
    KpiTargetId      INT  NOT NULL CONSTRAINT FK_KpiDaily_Kpi REFERENCES dbo.KpiTarget(KpiTargetId),
    WorkDate         DATE NOT NULL,
    TargetQty        INT  NOT NULL CONSTRAINT CK_KpiDaily_Qty CHECK (TargetQty > 0),
    CONSTRAINT UQ_KpiDaily UNIQUE (KpiTargetId, WorkDate)
);
GO

-- Chỉ tiêu theo từng dòng đơn (màu × size) của một chỉ tiêu KPI (DEV-ALL-62); KpiTarget.TargetFinishedQty = Σ TargetQty.
-- Các tổ cùng mã hàng chia nhau số đặt của từng dòng; chỉ tiêu tạo trước 03/11 có thể chưa có dòng (chỉ có số tổng).
CREATE TABLE dbo.KpiTargetLine (
    KpiTargetLineId INT IDENTITY(1,1) CONSTRAINT PK_KpiTargetLine PRIMARY KEY,
    KpiTargetId     INT NOT NULL CONSTRAINT FK_KpiLine_Kpi REFERENCES dbo.KpiTarget(KpiTargetId),
    OrderLineId     INT NOT NULL CONSTRAINT FK_KpiLine_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    TargetQty       INT NOT NULL CONSTRAINT CK_KpiLine_Qty CHECK (TargetQty > 0),
    CONSTRAINT UQ_KpiTargetLine UNIQUE (KpiTargetId, OrderLineId)
);
CREATE INDEX IX_KpiLine_OrderLine ON dbo.KpiTargetLine(OrderLineId) INCLUDE (TargetQty);
GO

/* =====================================================================================
   4. ĐƠN TỪ (REQUEST)   (UC07, UC08, FR 2.2, FR 3.2)
   ---------------------------------------------------------------------------------
   Loại đơn (RequestTypeCode)    Người tạo                   Người xử lý   Cần duyệt
   SPARE_PARTS                   Cơ điện                     Admin         có
   NEW_MACHINE                   Cơ điện                     Admin         có
   MACHINE_SUPPLIES              Cơ điện                     Kế toán       không
   ATTENDANCE_CORRECTION         Tổ trưởng cắt / may         Kế toán       có
   MATERIAL_SUPPLEMENT           Tổ trưởng, Thủ kho          Quản đốc      có
   DEFECTIVE_MATERIAL            Tổ trưởng cắt               Quản đốc      có
   REPLACEMENT_CUTTING           Tổ trưởng may               Quản đốc      có
   SHORT_DELIVERY                Hệ thống (phiếu nhập thiếu) Quản đốc      không
   MATERIAL_REQUISITION          Tổ trưởng cắt / may         Thủ kho       không  (yêu cầu NVL)
   SEMI_FINISHED_REQUISITION     Tổ trưởng may               Thủ kho       không  (yêu cầu BTP)
   MATERIAL_PURCHASE             Hệ thống (NVL lỗi thiếu tồn) Quản đốc     không  (Quản đốc đọc rồi tự tạo đơn mua)
   Quy tắc ai tạo / ai xử lý nằm ở backend. Lịch sử đổi trạng thái ghi vào AuditLog.
   ===================================================================================== */

CREATE TABLE dbo.Request (
    RequestId            INT IDENTITY(1,1) CONSTRAINT PK_Request PRIMARY KEY,
    RequestCode          VARCHAR(30)    NOT NULL CONSTRAINT UQ_Request_Code UNIQUE,
    RequestTypeCode      VARCHAR(30)    NOT NULL,
    Status               VARCHAR(20)    NOT NULL,
    OrderId              INT            NULL CONSTRAINT FK_Request_Order REFERENCES dbo.CustomerOrder(OrderId),
    WorkOrderId          INT            NULL,   -- lệnh cắt liên quan (NVL lỗi, yêu cầu NVL của tổ cắt) – FK thêm sau
    KpiTargetId          INT            NULL CONSTRAINT FK_Request_Kpi REFERENCES dbo.KpiTarget(KpiTargetId), -- yêu cầu BTP theo KPI
    MaterialIssueId      INT            NULL,   -- phiếu cấp phát bị lỗi (FR 2.5) – FK thêm sau
    MaterialReceiptId    INT            NULL,   -- phiếu nhập (kể cả phiếu bổ sung) sinh ra yêu cầu thiếu NVL; mỗi phiếu tối đa 1 yêu cầu – FK thêm sau
    DefectRecordId       INT            NULL,   -- phiếu hàng hỏng Scrap sinh ra yêu cầu cắt bù – FK thêm sau
    TargetDate           DATE           NULL,   -- ngày cần NVL / BTP; ngày xin chấm công bù; ngày dự kiến bù NVL (SHORT_DELIVERY)
    Title                NVARCHAR(200)  NULL,
    Content              NVARCHAR(2000) NULL,   -- nội dung và lý do của người tạo
    CreatedBy            INT            NULL CONSTRAINT FK_Request_CreatedBy REFERENCES dbo.Employee(EmployeeId), -- NULL = hệ thống tạo
    CreatedByTeamId      INT            NULL CONSTRAINT FK_Request_Team REFERENCES dbo.Team(TeamId),
    DecidedAt            DATETIME2(0)   NULL,
    DecisionNote         NVARCHAR(1000) NULL,   -- ghi chú khi duyệt; lý do khi từ chối (bắt buộc)
    ResultNote           NVARCHAR(1000) NULL,
    CompletedAt          DATETIME2(0)   NULL,
    ShortageResolution   VARCHAR(20)    NULL,   -- chỉ SHORT_DELIVERY (FR 1.6)
    -- Lệnh cắt bù / đơn mua / phiếu cấp sinh ra từ đơn từ trỏ ngược về đây qua SourceRequestId / RequestId
    CreatedAt            DATETIME2(0)   NOT NULL CONSTRAINT DF_Request_CreatedAt DEFAULT (dbo.fn_VnNow()),
    UpdatedAt            DATETIME2(0)   NULL,
    RowVer               ROWVERSION,
    CONSTRAINT CK_Request_Type CHECK (RequestTypeCode IN ('SPARE_PARTS','NEW_MACHINE','MACHINE_SUPPLIES','ATTENDANCE_CORRECTION',
        'MATERIAL_SUPPLEMENT','DEFECTIVE_MATERIAL','REPLACEMENT_CUTTING','SHORT_DELIVERY','MATERIAL_REQUISITION','SEMI_FINISHED_REQUISITION','MATERIAL_PURCHASE')),
    CONSTRAINT CK_Request_Status CHECK (Status IN ('PENDING_APPROVAL','PENDING_PROCESSING','APPROVED','REJECTED','COMPLETED','CANCELLED')),
    CONSTRAINT CK_Request_Reject CHECK (Status <> 'REJECTED' OR DecisionNote IS NOT NULL),
    CONSTRAINT CK_Request_Shortage CHECK (ShortageResolution IS NULL OR ShortageResolution IN ('REPLENISH','NO_REPLENISH'))
);
CREATE INDEX IX_Request_Inbox ON dbo.Request(RequestTypeCode, Status) INCLUDE (CreatedAt);
CREATE INDEX IX_Request_CreatedBy ON dbo.Request(CreatedBy, Status);

-- Dòng chi tiết đơn từ: NVL (MaterialId + màu) hoặc BTP theo dòng đơn (OrderLineId), số lượng
CREATE TABLE dbo.RequestLine (
    RequestLineId     INT IDENTITY(1,1) CONSTRAINT PK_RequestLine PRIMARY KEY,
    RequestId         INT            NOT NULL CONSTRAINT FK_RequestLine_Request REFERENCES dbo.Request(RequestId),
    MaterialId        INT            NULL CONSTRAINT FK_RequestLine_Material REFERENCES dbo.Material(MaterialId),
    OrderLineId       INT            NULL CONSTRAINT FK_RequestLine_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    ProductComponentMaterialId INT   NULL CONSTRAINT FK_RequestLine_PCM REFERENCES dbo.ProductComponentMaterial(ProductComponentMaterialId), -- chi tiết × vải cần cắt bù (FR 3.10); 25/10 thay ProductComponentId
    ItemDescription   NVARCHAR(300)  NULL,   -- linh kiện / máy / vật tư máy (đơn cơ điện)
    Quantity          DECIMAL(18,3)  NOT NULL CONSTRAINT CK_RequestLine_Qty CHECK (Quantity > 0),
    ReplenishQty      DECIMAL(18,3)  NULL,   -- SHORT_DELIVERY: số NCC/khách sẽ bù (Quản đốc nhập)
    MaterialReceiptLineId INT        NULL,   -- SHORT_DELIVERY: dòng phiếu nhập bị thiếu / lỗi (Quantity = số thiếu / lỗi báo) – FK thêm sau
    CONSTRAINT CK_RequestLine_Kind     CHECK (MaterialId IS NULL OR OrderLineId IS NULL),   -- dòng NVL hoặc dòng BTP / cắt bù, không cả hai
    CONSTRAINT CK_RequestLine_Item     CHECK (MaterialId IS NOT NULL OR OrderLineId IS NOT NULL OR ProductComponentMaterialId IS NOT NULL OR ItemDescription IS NOT NULL)
);

GO

/* =====================================================================================
   5. NHẬP NVL: đơn mua, thanh toán, phiếu nhập (khách cấp hoặc NCC)   (FR III.1, ERD D5)
   ===================================================================================== */

-- Đơn mua NVL do Quản đốc tạo và đặt (UC23, FR 1.3, GBR-18)
-- TH-12 (21/10): OrderId NULL = đơn mua dùng chung nhiều đơn (kim, chỉ...), Quản đốc tạo không cần mã đơn hàng
CREATE TABLE dbo.PurchaseOrder (
    PurchaseOrderId       INT IDENTITY(1,1) CONSTRAINT PK_PurchaseOrder PRIMARY KEY,
    PoCode                VARCHAR(30)    NOT NULL CONSTRAINT UQ_PurchaseOrder_Code UNIQUE,
    OrderId               INT            NULL CONSTRAINT FK_PO_Order REFERENCES dbo.CustomerOrder(OrderId),     -- NULL = đơn mua dùng chung
    SupplierId            INT            NULL CONSTRAINT FK_PO_Supplier REFERENCES dbo.Supplier(SupplierId), -- bắt buộc khi đã đặt
    SourceRequestId       INT            NULL CONSTRAINT FK_PO_Request REFERENCES dbo.Request(RequestId),   -- yêu cầu NVL / thiếu NVL
    OrderDate             DATE           NULL,
    ExpectedDeliveryCount SMALLINT       NULL,  -- số lần NCC dự kiến giao; số lần đã nhận xem vw_PurchaseOrderDelivery
    ExpectedDeliveryDate  DATE           NULL,  -- ngày NCC dự kiến giao (07/11/2026)
    GoodsStatus           VARCHAR(25)    NOT NULL CONSTRAINT DF_PO_GoodsStatus DEFAULT 'PENDING_FINALISATION',
    PaymentStatus         VARCHAR(20)    NOT NULL CONSTRAINT DF_PO_PaymentStatus DEFAULT 'UNPAID',
    RejectReason          NVARCHAR(500)  NULL,
    PaymentReturnReason   NVARCHAR(500)  NULL,  -- Kế toán trả lại Quản đốc
    -- Thanh toán do Kế toán xác nhận (PAYMENT, UC34); ảnh chứng từ trong Attachment (OwnerType = PURCHASE_ORDER)
    PaymentDate           DATE           NULL,
    PaidAmount            DECIMAL(18,2)  NULL,
    PaymentMethod         VARCHAR(20)    NULL,
    PaymentNote           NVARCHAR(500)  NULL,
    Note                  NVARCHAR(1000) NULL,
    CreatedAt             DATETIME2(0)   NOT NULL CONSTRAINT DF_PO_CreatedAt DEFAULT (dbo.fn_VnNow()),
    RowVer                ROWVERSION,
    CONSTRAINT CK_PO_GoodsStatus CHECK (GoodsStatus IN ('PENDING_FINALISATION','ORDERED','PARTIALLY_RECEIVED','RECEIVED',
                                                         'CLOSED_SHORT','CUSTOMER_SUPPLIED','REJECTED')),
    CONSTRAINT CK_PO_PaymentStatus CHECK (PaymentStatus IN ('UNPAID','PAID','RETURNED')),
    CONSTRAINT CK_PO_PaymentMethod CHECK (PaymentMethod IS NULL OR PaymentMethod IN ('BANK_TRANSFER','CASH')),
    CONSTRAINT CK_PO_Paid   CHECK (PaymentStatus <> 'PAID' OR (PaymentDate IS NOT NULL AND PaidAmount > 0 AND PaymentMethod IS NOT NULL)),
    CONSTRAINT CK_PO_PayReturn CHECK (PaymentStatus <> 'RETURNED' OR PaymentReturnReason IS NOT NULL),
    CONSTRAINT CK_PO_Ordered CHECK (GoodsStatus IN ('PENDING_FINALISATION','CUSTOMER_SUPPLIED','REJECTED')
                                    OR (SupplierId IS NOT NULL AND OrderDate IS NOT NULL)),
    CONSTRAINT CK_PO_Reject CHECK (GoodsStatus <> 'REJECTED' OR RejectReason IS NOT NULL),
    CONSTRAINT CK_PO_DeliveryCount CHECK (ExpectedDeliveryCount IS NULL OR ExpectedDeliveryCount >= 1),
    CONSTRAINT CK_PO_DeliveryDate CHECK (ExpectedDeliveryDate IS NULL OR OrderDate IS NULL OR ExpectedDeliveryDate >= OrderDate)
);

-- Dòng đơn mua xác định NVL theo một trong hai cách (TH-12):
--   OrderMaterialId (NVL + màu của đơn), hoặc OrderMaterialId NULL + MaterialId (đơn mua dùng chung)
CREATE TABLE dbo.PurchaseOrderLine (
    PurchaseOrderLineId  INT IDENTITY(1,1) CONSTRAINT PK_PurchaseOrderLine PRIMARY KEY,
    PurchaseOrderId      INT           NOT NULL CONSTRAINT FK_POL_PO REFERENCES dbo.PurchaseOrder(PurchaseOrderId),
    OrderMaterialId      INT           NULL CONSTRAINT FK_POL_OrderMaterial REFERENCES dbo.OrderMaterial(OrderMaterialId), -- NVL + màu của đơn; NULL = dòng dùng chung
    OrderedQty           DECIMAL(18,3) NOT NULL CONSTRAINT CK_POL_Ordered CHECK (OrderedQty > 0),
    NotReplenishedQty    DECIMAL(18,3) NOT NULL CONSTRAINT DF_POL_NotRepl DEFAULT 0,       -- Quản đốc chốt không bù
    UnitPrice            DECIMAL(18,2) NOT NULL CONSTRAINT DF_POL_Price DEFAULT 0,
    Amount               AS (CONVERT(DECIMAL(18,2), OrderedQty * UnitPrice)),
    MaterialId           INT           NULL CONSTRAINT FK_POL_Material REFERENCES dbo.Material(MaterialId), -- chỉ khi OrderMaterialId NULL
    CONSTRAINT CK_POL_Qty CHECK (NotReplenishedQty >= 0 AND NotReplenishedQty <= OrderedQty), -- đã nhận + không bù <= đặt: backend kiểm tra, số đã nhận xem vw_PurchaseOrderLineStatus
    CONSTRAINT CK_POL_Target CHECK (
        (OrderMaterialId IS NOT NULL AND MaterialId IS NULL)
     OR (OrderMaterialId IS NULL AND MaterialId IS NOT NULL))
);
-- Unique dạng filtered index để nhiều dòng NULL vẫn hợp lệ
CREATE UNIQUE INDEX UQ_POL       ON dbo.PurchaseOrderLine(PurchaseOrderId, OrderMaterialId) WHERE OrderMaterialId IS NOT NULL;
CREATE UNIQUE INDEX UQ_POL_Stock ON dbo.PurchaseOrderLine(PurchaseOrderId, MaterialId) WHERE OrderMaterialId IS NULL;

-- Phiếu nhập NVL: Mới / Bổ sung (FR 1.4, UC37, UC39). Phiếu bổ sung = có ShortageRequestId; phiếu gốc của nó là
-- Request.MaterialReceiptId của yêu cầu đó. Source tính từ PurchaseOrderId (có đơn mua = NCC, không có = khách cấp)
CREATE TABLE dbo.MaterialReceipt (
    MaterialReceiptId   INT IDENTITY(1,1) CONSTRAINT PK_MaterialReceipt PRIMARY KEY,
    ReceiptCode         VARCHAR(30)    NOT NULL CONSTRAINT UQ_MaterialReceipt_Code UNIQUE,
    ShortageRequestId   INT            NULL CONSTRAINT FK_MR_ShortageRequest REFERENCES dbo.Request(RequestId), -- NULL = phiếu mới; có = phiếu bổ sung cho yêu cầu thiếu này
    OrderId             INT            NULL CONSTRAINT FK_MR_Order REFERENCES dbo.CustomerOrder(OrderId), -- NULL = phiếu nhập của đơn mua dùng chung (TH-12)
    Source              AS (CAST(IIF(PurchaseOrderId IS NULL, 'CUSTOMER', 'SUPPLIER') AS VARCHAR(10))) PERSISTED,
    PurchaseOrderId     INT            NULL CONSTRAINT FK_MR_PO REFERENCES dbo.PurchaseOrder(PurchaseOrderId),
    ReceiptDate         DATE           NOT NULL,
    Status              VARCHAR(20)    NOT NULL CONSTRAINT DF_MR_Status DEFAULT 'PENDING',
    ReturnReason        NVARCHAR(500)  NULL,
    Note                NVARCHAR(1000) NULL,
    SubmittedAt         DATETIME2(0)   NOT NULL CONSTRAINT DF_MR_SubmittedAt DEFAULT (dbo.fn_VnNow()),
    ConfirmedBy         INT            NULL CONSTRAINT FK_MR_ConfirmedBy REFERENCES dbo.Employee(EmployeeId),
    ConfirmedAt         DATETIME2(0)   NULL,
    RowVer              ROWVERSION,
    CONSTRAINT CK_MR_Status  CHECK (Status IN ('PENDING','CONFIRMED','RETURNED')),
    CONSTRAINT CK_MR_Order   CHECK (PurchaseOrderId IS NOT NULL OR OrderId IS NOT NULL), -- khách cấp thì phải biết đơn hàng
    CONSTRAINT CK_MR_Return  CHECK (Status <> 'RETURNED' OR ReturnReason IS NOT NULL)
);
CREATE INDEX IX_MR_Order ON dbo.MaterialReceipt(OrderId, Status);

-- Dòng phiếu nhập xác định NVL giống PurchaseOrderLine: OrderMaterialId, hoặc MaterialId (TH-12)
CREATE TABLE dbo.MaterialReceiptLine (
    MaterialReceiptLineId INT IDENTITY(1,1) CONSTRAINT PK_MaterialReceiptLine PRIMARY KEY,
    MaterialReceiptId     INT           NOT NULL CONSTRAINT FK_MRL_Receipt REFERENCES dbo.MaterialReceipt(MaterialReceiptId),
    OrderMaterialId       INT           NULL CONSTRAINT FK_MRL_OrderMaterial REFERENCES dbo.OrderMaterial(OrderMaterialId), -- NVL + màu của đơn; NULL = dòng dùng chung
    PurchaseOrderLineId   INT           NULL CONSTRAINT FK_MRL_POL REFERENCES dbo.PurchaseOrderLine(PurchaseOrderLineId),
    OriginalReceiptLineId INT           NULL CONSTRAINT FK_MRL_OrigLine REFERENCES dbo.MaterialReceiptLine(MaterialReceiptLineId),
    AcceptedQty           DECIMAL(18,3) NOT NULL CONSTRAINT DF_MRL_Accepted DEFAULT 0,  -- số đạt nhập kho; thiếu so với đơn mua xem vw_PurchaseOrderLineStatus
    MaterialId            INT           NULL CONSTRAINT FK_MRL_Material REFERENCES dbo.Material(MaterialId), -- chỉ khi OrderMaterialId NULL
    CONSTRAINT CK_MRL_Qty CHECK (AcceptedQty >= 0),   -- 0 = cả lô lỗi / không giao, vẫn có dòng để gắn yêu cầu thiếu
    CONSTRAINT CK_MRL_Target CHECK (
        (OrderMaterialId IS NOT NULL AND MaterialId IS NULL)
     OR (OrderMaterialId IS NULL AND MaterialId IS NOT NULL))
);
CREATE UNIQUE INDEX UQ_MRL       ON dbo.MaterialReceiptLine(MaterialReceiptId, OrderMaterialId) WHERE OrderMaterialId IS NOT NULL;
CREATE UNIQUE INDEX UQ_MRL_Stock ON dbo.MaterialReceiptLine(MaterialReceiptId, MaterialId) WHERE OrderMaterialId IS NULL;
GO

/* =====================================================================================
   6. LUỒNG CẮT & CẤP KHO: lệnh cắt, phiếu cấp (NVL cho tổ cắt/may, BTP cho tổ may),
      trả vải thừa, bàn cắt, bàn giao BTP vào kho   (FR III.2, FR 3.3–3.4, ERD D3)
   ===================================================================================== */

-- Lệnh cắt cho tổ cắt (FR 2.1). Lệnh cắt bù (cho tổ may) có SourceRequestId = yêu cầu cắt bù (REPLACEMENT_CUTTING); lệnh thường để trống
CREATE TABLE dbo.WorkOrder (
    WorkOrderId       INT IDENTITY(1,1) CONSTRAINT PK_WorkOrder PRIMARY KEY,
    WorkOrderCode     VARCHAR(30)    NOT NULL CONSTRAINT UQ_WorkOrder_Code UNIQUE,
    OrderId           INT            NOT NULL CONSTRAINT FK_WO_Order REFERENCES dbo.CustomerOrder(OrderId),
    ProductId         INT            NOT NULL CONSTRAINT FK_WO_Product REFERENCES dbo.Product(ProductId),
    CuttingTeamId     INT            NOT NULL CONSTRAINT FK_WO_Team REFERENCES dbo.Team(TeamId),
    SourceRequestId   INT            NULL CONSTRAINT FK_WO_Request REFERENCES dbo.Request(RequestId), -- trống = lệnh cắt thường
    DueDate           DATE           NOT NULL,
    Status            VARCHAR(20)    NOT NULL CONSTRAINT DF_WO_Status DEFAULT 'OPEN',
    Note              NVARCHAR(1000) NULL,
    CreatedAt         DATETIME2(0)   NOT NULL CONSTRAINT DF_WO_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CompletedAt       DATETIME2(0)   NULL,
    CONSTRAINT CK_WO_Status  CHECK (Status IN ('OPEN','CUTTING','COMPLETED','CANCELLED'))
);

-- Số cần cắt theo màu × size. Cắt bù từng chi tiết: ProductComponentMaterialId = dòng định mức (chi tiết PANEL × NVL của đúng màu áo;
-- size theo OrderLineId) cần cắt, PlannedQty = số chi tiết; lệnh cắt thường: NULL = cả bộ, PlannedQty = số sản phẩm; có chi tiết = cắt lẻ thêm thành phần, PlannedQty = số chi tiết (01/11)
CREATE TABLE dbo.WorkOrderLine (
    WorkOrderLineId    INT IDENTITY(1,1) CONSTRAINT PK_WorkOrderLine PRIMARY KEY,
    WorkOrderId        INT           NOT NULL CONSTRAINT FK_WOL_WO REFERENCES dbo.WorkOrder(WorkOrderId),
    OrderLineId        INT           NOT NULL CONSTRAINT FK_WOL_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    ProductComponentMaterialId INT   NULL CONSTRAINT FK_WOL_PCM REFERENCES dbo.ProductComponentMaterial(ProductComponentMaterialId),
    PlannedQty         INT           NOT NULL CONSTRAINT CK_WOL_Qty CHECK (PlannedQty > 0),
    CONSTRAINT UQ_WOL UNIQUE (WorkOrderId, OrderLineId, ProductComponentMaterialId)
);

-- Phiếu cấp kho (MATERIAL_ISSUE): NVL cho tổ cắt/may (FR 2.3) hoặc BTP cho tổ may (FR 3.3).
-- Xuất chỉ GIỮ CHỖ, trừ kho khi tổ trưởng xác nhận nhận (FR 2.4, 3.4)
CREATE TABLE dbo.MaterialIssue (
    MaterialIssueId       INT IDENTITY(1,1) CONSTRAINT PK_MaterialIssue PRIMARY KEY,
    IssueCode             VARCHAR(30)    NOT NULL CONSTRAINT UQ_MaterialIssue_Code UNIQUE,
    WarehouseType         VARCHAR(15)    NOT NULL CONSTRAINT DF_MI_WH DEFAULT 'MATERIAL',  -- MATERIAL: NVL; SEMI_FINISHED: BTP
    RequestId             INT            NULL CONSTRAINT FK_MI_Request REFERENCES dbo.Request(RequestId), -- yêu cầu NVL / BTP
    OrderId               INT            NOT NULL CONSTRAINT FK_MI_Order REFERENCES dbo.CustomerOrder(OrderId),
    WorkOrderId           INT            NULL CONSTRAINT FK_MI_WO REFERENCES dbo.WorkOrder(WorkOrderId),
    ReceivingTeamId       INT            NOT NULL CONSTRAINT FK_MI_Team REFERENCES dbo.Team(TeamId),
    IssueDate             DATE           NOT NULL,
    IssuedBy              INT            NOT NULL CONSTRAINT FK_MI_IssuedBy REFERENCES dbo.Employee(EmployeeId),
    Status                VARCHAR(20)    NOT NULL CONSTRAINT DF_MI_Status DEFAULT 'PENDING_RECEIPT',
    ReceivedBy            INT            NULL CONSTRAINT FK_MI_ReceivedBy REFERENCES dbo.Employee(EmployeeId),
    ReceivedAt            DATETIME2(0)   NULL,
    RejectReason          NVARCHAR(500)  NULL,
    CreatedAt             DATETIME2(0)   NOT NULL CONSTRAINT DF_MI_CreatedAt DEFAULT (dbo.fn_VnNow()),
    RowVer                ROWVERSION,
    CONSTRAINT CK_MI_WH     CHECK (WarehouseType IN ('MATERIAL','SEMI_FINISHED')),
    CONSTRAINT CK_MI_Status CHECK (Status IN ('PENDING_RECEIPT','RECEIVED','REJECTED')),
    CONSTRAINT CK_MI_Reject CHECK (Status <> 'REJECTED' OR RejectReason IS NOT NULL)
);
CREATE INDEX IX_MI_Team ON dbo.MaterialIssue(ReceivingTeamId, Status);

CREATE TABLE dbo.MaterialIssueLine (
    MaterialIssueLineId INT IDENTITY(1,1) CONSTRAINT PK_MaterialIssueLine PRIMARY KEY,
    MaterialIssueId     INT           NOT NULL CONSTRAINT FK_MIL_Issue REFERENCES dbo.MaterialIssue(MaterialIssueId),
    MaterialId          INT           NULL CONSTRAINT FK_MIL_Material REFERENCES dbo.Material(MaterialId),   -- phiếu NVL
    OrderLineId         INT           NULL CONSTRAINT FK_MIL_OrderLine REFERENCES dbo.OrderLine(OrderLineId), -- phiếu BTP
    ProductComponentMaterialId INT    NULL CONSTRAINT FK_MIL_PCM REFERENCES dbo.ProductComponentMaterial(ProductComponentMaterialId), -- BTP: NULL = bộ đủ; có = chi tiết × vải cắt bù (25/10)
    IssuedQty           DECIMAL(18,3) NOT NULL CONSTRAINT CK_MIL_Issued CHECK (IssuedQty > 0),
    ConfirmedQty        DECIMAL(18,3) NULL,
    DiscrepancyReason   NVARCHAR(500) NULL,
    CONSTRAINT CK_MIL_Confirmed CHECK (ConfirmedQty IS NULL OR (ConfirmedQty >= 0 AND ConfirmedQty <= IssuedQty)),
    CONSTRAINT CK_MIL_Reason    CHECK (ConfirmedQty IS NULL OR ConfirmedQty = IssuedQty OR DiscrepancyReason IS NOT NULL), -- GBR-06
    CONSTRAINT CK_MIL_Item      CHECK ((MaterialId IS NOT NULL AND OrderLineId IS NULL)
                                    OR (MaterialId IS NULL AND OrderLineId IS NOT NULL)),
    CONSTRAINT CK_MIL_Component CHECK (ProductComponentMaterialId IS NULL OR OrderLineId IS NOT NULL),
    CONSTRAINT UQ_MIL UNIQUE (MaterialIssueId, MaterialId, OrderLineId, ProductComponentMaterialId)
);

-- Phiếu trả vải thừa: chỉ tổ cắt lập, thủ kho xác nhận (FR 2.10, 2.11). Tổ trả = MaterialIssue.ReceivingTeamId
CREATE TABLE dbo.MaterialReturn (
    MaterialReturnId  INT IDENTITY(1,1) CONSTRAINT PK_MaterialReturn PRIMARY KEY,
    ReturnCode        VARCHAR(30)    NOT NULL CONSTRAINT UQ_MaterialReturn_Code UNIQUE,
    MaterialIssueId   INT            NOT NULL CONSTRAINT FK_MRet_Issue REFERENCES dbo.MaterialIssue(MaterialIssueId),
    Note              NVARCHAR(500)  NULL,
    Status            VARCHAR(20)    NOT NULL CONSTRAINT DF_MRet_Status DEFAULT 'PENDING',
    ConfirmedBy       INT            NULL CONSTRAINT FK_MRet_ConfirmedBy REFERENCES dbo.Employee(EmployeeId),
    ConfirmedAt       DATETIME2(0)   NULL,
    RejectReason      NVARCHAR(500)  NULL,
    CreatedAt         DATETIME2(0)   NOT NULL CONSTRAINT DF_MRet_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_MRet_Status CHECK (Status IN ('PENDING','CONFIRMED','REJECTED','CANCELLED')),
    CONSTRAINT CK_MRet_Reject CHECK (Status <> 'REJECTED' OR RejectReason IS NOT NULL)
);
CREATE TABLE dbo.MaterialReturnLine (
    MaterialReturnLineId INT IDENTITY(1,1) CONSTRAINT PK_MaterialReturnLine PRIMARY KEY,
    MaterialReturnId     INT           NOT NULL CONSTRAINT FK_MRetL_Return REFERENCES dbo.MaterialReturn(MaterialReturnId),
    MaterialIssueLineId  INT           NOT NULL CONSTRAINT FK_MRetL_IssueLine REFERENCES dbo.MaterialIssueLine(MaterialIssueLineId),
    ReturnedQty          DECIMAL(18,3) NOT NULL CONSTRAINT CK_MRetL_Qty CHECK (ReturnedQty > 0),
    ConfirmedQty         DECIMAL(18,3) NULL,
    DiscrepancyReason    NVARCHAR(500) NULL,
    CONSTRAINT CK_MRetL_Confirmed CHECK (ConfirmedQty IS NULL OR ConfirmedQty >= 0),
    CONSTRAINT CK_MRetL_Reason    CHECK (ConfirmedQty IS NULL OR ConfirmedQty = ReturnedQty OR DiscrepancyReason IS NOT NULL),
    CONSTRAINT UQ_MRetL UNIQUE (MaterialReturnId, MaterialIssueLineId)
);

-- Bàn cắt (CUTTING_LAY, FR 2.7): số lớp × tỉ lệ size. Tỉ lệ size nằm ở CuttingLayLine.RatioValue. Một lần nhập có thể dùng nhiều bìa
-- nên bàn cắt không gắn bìa; vải đã cắt = Σ PlyCount × LayLengthM (DEV-ALL-85)
CREATE TABLE dbo.CuttingLay (
    CuttingLayId    INT IDENTITY(1,1) CONSTRAINT PK_CuttingLay PRIMARY KEY,
    WorkOrderId     INT            NOT NULL CONSTRAINT FK_CL_WO REFERENCES dbo.WorkOrder(WorkOrderId),
    CuttingTeamId   INT            NOT NULL CONSTRAINT FK_CL_Team REFERENCES dbo.Team(TeamId),
    WorkDate        DATE           NOT NULL,
    ShiftCode       VARCHAR(10)    NOT NULL CONSTRAINT CK_CL_Shift CHECK (ShiftCode IN ('MORNING','AFTERNOON','OVERTIME')),
    LaySequence     SMALLINT       NOT NULL,
    FabricMaterialId INT           NOT NULL CONSTRAINT FK_CL_Material REFERENCES dbo.Material(MaterialId),
    PlyCount        SMALLINT       NOT NULL CONSTRAINT CK_CL_Ply CHECK (PlyCount > 0),
    LayLengthM      DECIMAL(7,2)   NULL CONSTRAINT CK_CL_Length CHECK (LayLengthM > 0),   -- chiều dài bàn cắt (mét); NULL = bàn nhập trước khi có cột này
    Note            NVARCHAR(500)  NULL,
    Status          VARCHAR(10)    NOT NULL CONSTRAINT DF_CL_Status DEFAULT 'RECORDED',
    CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_CL_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_CL_Status CHECK (Status IN ('RECORDED','LOCKED')),
    CONSTRAINT UQ_CL UNIQUE (CuttingTeamId, WorkDate, ShiftCode, LaySequence)
);
CREATE TABLE dbo.CuttingLayLine (
    CuttingLayLineId INT IDENTITY(1,1) CONSTRAINT PK_CuttingLayLine PRIMARY KEY,
    CuttingLayId     INT           NOT NULL CONSTRAINT FK_CLL_Lay REFERENCES dbo.CuttingLay(CuttingLayId),
    OrderLineId      INT           NOT NULL CONSTRAINT FK_CLL_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    ProductComponentMaterialId INT NULL CONSTRAINT FK_CLL_PCM REFERENCES dbo.ProductComponentMaterial(ProductComponentMaterialId), -- NULL = cả bộ; có = chi tiết × NVL cắt bù (25/10)
    RatioValue       SMALLINT      NOT NULL CONSTRAINT CK_CLL_Ratio CHECK (RatioValue > 0),
    CalculatedQty    INT           NOT NULL CONSTRAINT CK_CLL_Calc CHECK (CalculatedQty >= 0),   -- số lớp × tỉ lệ
    ActualQty        INT           NOT NULL CONSTRAINT CK_CLL_Actual CHECK (ActualQty >= 0),     -- có thể chỉnh, kèm lý do
    AdjustReason     NVARCHAR(500) NULL,
    CONSTRAINT CK_CLL_Reason CHECK (ActualQty = CalculatedQty OR AdjustReason IS NOT NULL),
    CONSTRAINT UQ_CLL UNIQUE (CuttingLayId, OrderLineId, ProductComponentMaterialId)
);

-- Bàn giao BTP từ tổ cắt vào kho BTP (FR 2.8, 2.9, UC43). Tổ cắt = WorkOrder.CuttingTeamId
CREATE TABLE dbo.CutHandover (
    CutHandoverId   INT IDENTITY(1,1) CONSTRAINT PK_CutHandover PRIMARY KEY,
    HandoverCode    VARCHAR(30)    NOT NULL CONSTRAINT UQ_CutHandover_Code UNIQUE,
    WorkOrderId     INT            NOT NULL CONSTRAINT FK_CH_WO REFERENCES dbo.WorkOrder(WorkOrderId),
    HandedAt        DATETIME2(0)   NOT NULL CONSTRAINT DF_CH_HandedAt DEFAULT (dbo.fn_VnNow()),
    Status          VARCHAR(20)    NOT NULL CONSTRAINT DF_CH_Status DEFAULT 'PENDING',
    ConfirmedBy     INT            NULL CONSTRAINT FK_CH_ConfirmedBy REFERENCES dbo.Employee(EmployeeId),
    ConfirmedAt     DATETIME2(0)   NULL,
    RejectReason    NVARCHAR(500)  NULL,
    CONSTRAINT CK_CH_Status CHECK (Status IN ('PENDING','CONFIRMED','REJECTED','CANCELLED')),
    CONSTRAINT CK_CH_Reject CHECK (Status <> 'REJECTED' OR RejectReason IS NOT NULL)
);
CREATE TABLE dbo.CutHandoverLine (
    CutHandoverLineId INT IDENTITY(1,1) CONSTRAINT PK_CutHandoverLine PRIMARY KEY,
    CutHandoverId     INT           NOT NULL CONSTRAINT FK_CHL_Handover REFERENCES dbo.CutHandover(CutHandoverId),
    OrderLineId       INT           NOT NULL CONSTRAINT FK_CHL_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    ProductComponentMaterialId INT  NULL CONSTRAINT FK_CHL_PCM REFERENCES dbo.ProductComponentMaterial(ProductComponentMaterialId), -- NULL = bộ đủ; có = chi tiết × vải cắt bù (25/10)
    HandedQty         INT           NOT NULL CONSTRAINT CK_CHL_Handed CHECK (HandedQty > 0),
    ConfirmedQty      INT           NULL,
    DiscrepancyReason NVARCHAR(500) NULL,
    CONSTRAINT CK_CHL_Confirmed CHECK (ConfirmedQty IS NULL OR ConfirmedQty >= 0),
    CONSTRAINT CK_CHL_Reason    CHECK (ConfirmedQty IS NULL OR ConfirmedQty = HandedQty OR DiscrepancyReason IS NOT NULL),
    CONSTRAINT UQ_CHL UNIQUE (CutHandoverId, OrderLineId, ProductComponentMaterialId)
);
GO

/* =====================================================================================
   7. LUỒNG MAY: phân công công đoạn, giao/thu hồi việc với công nhân, sản lượng,
      hàng hỏng, đợt kiểm QC, bàn giao thành phẩm   (FR III.3, ERD D3–D4)
      (yêu cầu BTP = Request loại SEMI_FINISHED_REQUISITION; cấp BTP = MaterialIssue kho SEMI_FINISHED)
   ===================================================================================== */

-- Phân công công đoạn cho công nhân (UC52); đổi phân công = đóng dòng cũ (ValidTo) + mở dòng mới
CREATE TABLE dbo.OperationAssignment (
    OperationAssignmentId INT IDENTITY(1,1) CONSTRAINT PK_OperationAssignment PRIMARY KEY,
    SewingTeamId    INT          NOT NULL CONSTRAINT FK_OA_Team REFERENCES dbo.Team(TeamId),
    OrderId         INT          NOT NULL CONSTRAINT FK_OA_Order REFERENCES dbo.CustomerOrder(OrderId),
    OperationId     INT          NOT NULL CONSTRAINT FK_OA_Operation REFERENCES dbo.Operation(OperationId),
    WorkerId        INT          NOT NULL CONSTRAINT FK_OA_Worker REFERENCES dbo.Employee(EmployeeId),
    ValidFrom       DATE         NOT NULL,
    ValidTo         DATE         NULL,
    AssignedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_OA_AssignedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_OA_Dates CHECK (ValidTo IS NULL OR ValidTo >= ValidFrom)
);
CREATE UNIQUE INDEX UX_OA_Current ON dbo.OperationAssignment(SewingTeamId, OrderId, OperationId, WorkerId) WHERE ValidTo IS NULL;

-- Quản đốc, Admin chia dòng đơn (mã hàng × màu × size) cho tổ may (DEV-ALL-49). Tổ cắt chỉ có một tổ nên không chia.
-- Đơn còn NEW được bỏ dòng nên xóa dòng thì xóa luôn việc chia của dòng đó.
CREATE TABLE dbo.OrderLineTeam (
    OrderLineTeamId INT IDENTITY(1,1) CONSTRAINT PK_OrderLineTeam PRIMARY KEY,
    OrderLineId  INT          NOT NULL CONSTRAINT FK_OLT_OrderLine REFERENCES dbo.OrderLine(OrderLineId) ON DELETE CASCADE,
    SewingTeamId INT          NOT NULL CONSTRAINT FK_OLT_Team REFERENCES dbo.Team(TeamId),
    AssignedAt   DATETIME2(0) NOT NULL CONSTRAINT DF_OLT_AssignedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT UQ_OrderLineTeam UNIQUE (OrderLineId, SewingTeamId)
);
CREATE INDEX IX_OLT_Team ON dbo.OrderLineTeam(SewingTeamId, OrderLineId);

-- Giao việc / thu hồi việc với công nhân (25/10 thay WorkerPieceMovement; FR 3.5, 3.6, 3.8; UC53, UC54, UC60).
-- Quantity = số lần làm công đoạn × màu × size được giao / thu hồi; tổ trưởng chia tùy ý, miễn tổng số lần đang giao của
-- công đoạn × màu × size không vượt số bộ BTP tổ đã nhận (MaterialIssueLine.ConfirmedQty, THROW 50050).
-- 26/10: bỏ PieceQty, ConfirmedPieceQty (mỗi công đoạn nhận riêng từng thành phần, không đếm được BTP giao kèm theo bộ đủ).
-- Trần sản lượng = Σ ConfirmedQty giao − thu hồi (vw_WorkerOutputCap)
CREATE TABLE dbo.WorkAllocation (
    WorkAllocationId  INT IDENTITY(1,1) CONSTRAINT PK_WorkAllocation PRIMARY KEY,
    WorkDate          DATE           NOT NULL CONSTRAINT DF_WA_WorkDate DEFAULT (CAST(dbo.fn_VnNow() AS DATE)),  -- ngày làm việc của phiếu (03/11)
    MovementType      VARCHAR(10)    NOT NULL,             -- HANDOVER: giao; RETRIEVAL: thu hồi
    SewingTeamId      INT            NOT NULL CONSTRAINT FK_WA_Team REFERENCES dbo.Team(TeamId),
    WorkerId          INT            NOT NULL CONSTRAINT FK_WA_Worker REFERENCES dbo.Employee(EmployeeId),
    OrderLineId       INT            NOT NULL CONSTRAINT FK_WA_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    OperationId       INT            NOT NULL CONSTRAINT FK_WA_Operation REFERENCES dbo.Operation(OperationId),
    Quantity          INT            NOT NULL CONSTRAINT CK_WA_Qty CHECK (Quantity > 0),
    ConfirmedQty      INT            NULL,                 -- số lần công nhân xác nhận
    DiscrepancyReason NVARCHAR(500)  NULL,
    RetrievalReason   VARCHAR(20)    NULL,
    RetrievalNote     NVARCHAR(500)  NULL,                 -- lý do thu hồi cuối ngày (chữ tự do)
    ConfirmMode       VARCHAR(10)    NOT NULL,             -- WORKER: CN tự xác nhận; ON_BEHALF: tổ trưởng xác nhận hộ
    OnBehalfReason    NVARCHAR(500)  NULL,
    Status            VARCHAR(10)    NOT NULL CONSTRAINT DF_WA_Status DEFAULT 'PENDING',
    RejectReason      NVARCHAR(500)  NULL,
    CreatedAt         DATETIME2(0)   NOT NULL CONSTRAINT DF_WA_CreatedAt DEFAULT (dbo.fn_VnNow()),
    ConfirmedAt       DATETIME2(0)   NULL,
    CONSTRAINT CK_WA_Type    CHECK (MovementType IN ('HANDOVER','RETRIEVAL')),
    CONSTRAINT CK_WA_Retr    CHECK (MovementType = 'HANDOVER' OR RetrievalReason IN ('OPERATION_CHANGED','WORKER_ABSENT','STYLE_FINISHED','END_OF_DAY','ADJUSTED')),
    CONSTRAINT CK_WA_Mode    CHECK (ConfirmMode IN ('WORKER','ON_BEHALF','LEADER','SYSTEM')),   -- LEADER: tổ trưởng giao trực tiếp; SYSTEM: tự thu hồi cuối ngày
    CONSTRAINT CK_WA_Behalf  CHECK (ConfirmMode <> 'ON_BEHALF' OR OnBehalfReason IS NOT NULL),
    CONSTRAINT CK_WA_Status  CHECK (Status IN ('PENDING','CONFIRMED','REJECTED')),
    CONSTRAINT CK_WA_Confirmed CHECK (ConfirmedQty IS NULL OR (ConfirmedQty >= 0 AND (ConfirmedQty = Quantity OR DiscrepancyReason IS NOT NULL))),
    CONSTRAINT CK_WA_ConfirmedStatus CHECK (Status <> 'CONFIRMED' OR ConfirmedQty IS NOT NULL),
    CONSTRAINT CK_WA_Reject  CHECK (Status <> 'REJECTED' OR RejectReason IS NOT NULL)
);
CREATE INDEX IX_WA_Line ON dbo.WorkAllocation(SewingTeamId, OrderLineId, OperationId, Status) INCLUDE (MovementType, Quantity, ConfirmedQty);
CREATE INDEX IX_WA_Worker ON dbo.WorkAllocation(WorkerId, OrderLineId, OperationId, Status);
CREATE INDEX IX_WA_Day ON dbo.WorkAllocation(SewingTeamId, WorkDate) INCLUDE (WorkerId, OrderLineId, OperationId, MovementType, ConfirmedQty, Status);

-- Sản lượng công đoạn cuối ca (OUTPUT_RECORD, UC55). Đơn giá khóa lúc nhập bằng PieceRateId (đơn giá đã duyệt không sửa được)
CREATE TABLE dbo.OutputRecord (
    OutputRecordId     BIGINT IDENTITY(1,1) CONSTRAINT PK_OutputRecord PRIMARY KEY,
    WorkDate           DATE           NOT NULL,
    ShiftCode          VARCHAR(10)    NOT NULL CONSTRAINT CK_OR_Shift CHECK (ShiftCode IN ('MORNING','AFTERNOON','OVERTIME')),
    SewingTeamId       INT            NOT NULL CONSTRAINT FK_OR_Team REFERENCES dbo.Team(TeamId),
    WorkerId           INT            NOT NULL CONSTRAINT FK_OR_Worker REFERENCES dbo.Employee(EmployeeId),
    OperationId        INT            NOT NULL CONSTRAINT FK_OR_Operation REFERENCES dbo.Operation(OperationId),
    OrderLineId        INT            NOT NULL CONSTRAINT FK_OR_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    Quantity           INT            NOT NULL CONSTRAINT CK_OR_Qty CHECK (Quantity > 0),     -- chỉ hàng may lần đầu, không cộng hàng sửa
    PieceRateId        INT            NOT NULL CONSTRAINT FK_OR_PieceRate REFERENCES dbo.PieceRate(PieceRateId),
    IsOutsideAssignment BIT           NOT NULL CONSTRAINT DF_OR_Outside DEFAULT 0,
    NoAttendanceWarning BIT           NOT NULL CONSTRAINT DF_OR_NoAtt DEFAULT 0,              -- MSG17
    EditDeadline       DATETIME2(0)   NOT NULL,     -- hết ngày làm việc kế tiếp; sau đó chỉ Quản đốc (UC22)
    Status             VARCHAR(10)    NOT NULL CONSTRAINT DF_OR_Status DEFAULT 'SAVED',
    EnteredAt          DATETIME2(0)   NOT NULL CONSTRAINT DF_OR_EnteredAt DEFAULT (dbo.fn_VnNow()),
    UpdatedAt          DATETIME2(0)   NULL,
    RowVer             ROWVERSION,
    CONSTRAINT CK_OR_Status CHECK (Status IN ('SAVED','LOCKED')),
    CONSTRAINT UQ_OutputRecord UNIQUE (WorkDate, ShiftCode, SewingTeamId, OrderLineId, OperationId, WorkerId)
);
CREATE INDEX IX_OR_Worker ON dbo.OutputRecord(WorkerId, WorkDate) INCLUDE (Quantity, PieceRateId, OrderLineId, OperationId);
CREATE INDEX IX_OR_OrderLine ON dbo.OutputRecord(OrderLineId, OperationId) INCLUDE (Quantity, WorkerId);

-- Hàng lỗi + công nhân gây lỗi (DEFECT_RECORD, UC56). Rework: không trừ sản lượng; Scrap: trừ
CREATE TABLE dbo.DefectRecord (
    DefectRecordId      INT IDENTITY(1,1) CONSTRAINT PK_DefectRecord PRIMARY KEY,
    DefectCode          VARCHAR(30)    NOT NULL CONSTRAINT UQ_DefectRecord_Code UNIQUE,  -- tổ + ngày + số thứ tự
    WorkDate            DATE           NOT NULL,
    ShiftCode           VARCHAR(10)    NULL CONSTRAINT CK_DR_Shift CHECK (ShiftCode IN ('MORNING','AFTERNOON','OVERTIME')),
    SewingTeamId        INT            NOT NULL CONSTRAINT FK_DR_Team REFERENCES dbo.Team(TeamId),
    OrderLineId         INT            NOT NULL CONSTRAINT FK_DR_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    OperationId         INT            NOT NULL CONSTRAINT FK_DR_Operation REFERENCES dbo.Operation(OperationId),
    ResponsibleWorkerId INT            NOT NULL CONSTRAINT FK_DR_Worker REFERENCES dbo.Employee(EmployeeId),
    DefectType          VARCHAR(10)    NOT NULL,
    Quantity            INT            NOT NULL CONSTRAINT CK_DR_Qty CHECK (Quantity > 0),
    DefectReasonCode    VARCHAR(20)    NOT NULL,
    ReasonNote          NVARCHAR(500)  NULL,    -- bắt buộc khi lý do = OTHER
    PieceRateId         INT            NULL CONSTRAINT FK_DR_PieceRate REFERENCES dbo.PieceRate(PieceRateId), -- đơn giá khóa để trừ lương tạm tính (chỉ Scrap)
    WorkerNotifiedAt    DATETIME2(0)   NULL,
    WorkerObjection     NVARCHAR(500)  NULL,    -- công nhân phản hồi nếu ghi sai
    ObjectedAt          DATETIME2(0)   NULL,
    EditDeadline        DATETIME2(0)   NOT NULL,
    Status              VARCHAR(10)    NOT NULL CONSTRAINT DF_DR_Status DEFAULT 'RECORDED',
    RecordedBy          INT            NOT NULL CONSTRAINT FK_DR_RecordedBy REFERENCES dbo.Employee(EmployeeId),
    RecordedAt          DATETIME2(0)   NOT NULL CONSTRAINT DF_DR_RecordedAt DEFAULT (dbo.fn_VnNow()),
    UpdatedAt           DATETIME2(0)   NULL,
    RowVer              ROWVERSION,
    CONSTRAINT CK_DR_Type   CHECK (DefectType IN ('REWORK','SCRAP','OUTPUT_ERR')),   -- OUTPUT_ERR: tổ trưởng nhập sai sản lượng công đoạn, trừ khỏi sản lượng của công nhân (DEV-ALL-79)
    CONSTRAINT CK_DR_Reason CHECK (DefectReasonCode IN ('SKIPPED_STITCH','CROOKED_SEAM','WRONG_SIZE','TORN','STAINED','OTHER')),
    CONSTRAINT CK_DR_Other  CHECK (DefectReasonCode <> 'OTHER' OR ReasonNote IS NOT NULL),
    CONSTRAINT CK_DR_Status CHECK (Status IN ('RECORDED','CONFIRMED','CANCELLED','LOCKED')),   -- CONFIRMED: Quản đốc chấp nhận phiếu sau khi công nhân phản đối
    CONSTRAINT CK_DR_Rate   CHECK (DefectType = 'REWORK' OR PieceRateId IS NOT NULL)
);
CREATE INDEX IX_DR_Worker ON dbo.DefectRecord(ResponsibleWorkerId, WorkDate) INCLUDE (DefectType, Quantity, PieceRateId, Status);
CREATE INDEX IX_DR_OrderLine ON dbo.DefectRecord(OrderLineId, DefectType) INCLUDE (Quantity, Status);

-- Đợt kiểm hoàn thiện của QA/QC: chỉ đếm hàng đạt, không phân loại lỗi (UC57, FR 3.11)
CREATE TABLE dbo.QcBatch (
    QcBatchId       INT IDENTITY(1,1) CONSTRAINT PK_QcBatch PRIMARY KEY,
    WorkDate        DATE           NOT NULL,
    ShiftCode       VARCHAR(10)    NOT NULL CONSTRAINT CK_QC_Shift CHECK (ShiftCode IN ('MORNING','AFTERNOON','OVERTIME')),
    BatchTime       TIME(0)        NOT NULL,
    SewingTeamId    INT            NOT NULL CONSTRAINT FK_QC_Team REFERENCES dbo.Team(TeamId),
    OrderLineId     INT            NOT NULL CONSTRAINT FK_QC_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    FinishedQty     INT            NOT NULL CONSTRAINT CK_QC_Qty CHECK (FinishedQty > 0),
    InspectorId     INT            NOT NULL CONSTRAINT FK_QC_Inspector REFERENCES dbo.Employee(EmployeeId),
    EditDeadline    DATETIME2(0)   NOT NULL,     -- hết ca
    CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_QC_CreatedAt DEFAULT (dbo.fn_VnNow()),
    UpdatedAt       DATETIME2(0)   NULL,
    RowVer          ROWVERSION,
    CONSTRAINT UQ_QcBatch UNIQUE (WorkDate, ShiftCode, BatchTime, SewingTeamId, OrderLineId)
);
CREATE INDEX IX_QC_OrderLine ON dbo.QcBatch(OrderLineId) INCLUDE (FinishedQty, WorkDate);

-- Phiếu bàn giao thành phẩm QA/QC → kho TP (FG_HANDOVER, UC44); số còn bàn giao được = Σ QC đạt − Σ đã bàn giao, theo dòng đơn
CREATE TABLE dbo.FgHandover (
    FgHandoverId    INT IDENTITY(1,1) CONSTRAINT PK_FgHandover PRIMARY KEY,
    HandoverCode    VARCHAR(30)    NOT NULL CONSTRAINT UQ_FgHandover_Code UNIQUE,
    OrderId         INT            NOT NULL CONSTRAINT FK_FGH_Order REFERENCES dbo.CustomerOrder(OrderId),
    ProductId       INT            NOT NULL CONSTRAINT FK_FGH_Product REFERENCES dbo.Product(ProductId),
    InspectorId     INT            NOT NULL CONSTRAINT FK_FGH_Inspector REFERENCES dbo.Employee(EmployeeId),
    HandedAt        DATETIME2(0)   NOT NULL CONSTRAINT DF_FGH_HandedAt DEFAULT (dbo.fn_VnNow()),
    Status          VARCHAR(20)    NOT NULL CONSTRAINT DF_FGH_Status DEFAULT 'PENDING',
    ConfirmedBy     INT            NULL CONSTRAINT FK_FGH_ConfirmedBy REFERENCES dbo.Employee(EmployeeId),
    ConfirmedAt     DATETIME2(0)   NULL,
    RejectReason    NVARCHAR(500)  NULL,
    CONSTRAINT CK_FGH_Status CHECK (Status IN ('PENDING','CONFIRMED','REJECTED','CANCELLED')),
    CONSTRAINT CK_FGH_Reject CHECK (Status <> 'REJECTED' OR RejectReason IS NOT NULL)
);
CREATE TABLE dbo.FgHandoverLine (
    FgHandoverLineId  INT IDENTITY(1,1) CONSTRAINT PK_FgHandoverLine PRIMARY KEY,
    FgHandoverId      INT           NOT NULL CONSTRAINT FK_FGHL_Handover REFERENCES dbo.FgHandover(FgHandoverId),
    OrderLineId       INT           NOT NULL CONSTRAINT FK_FGHL_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    HandedQty         INT           NOT NULL CONSTRAINT CK_FGHL_Handed CHECK (HandedQty > 0),
    ConfirmedQty      INT           NULL,
    DiscrepancyReason NVARCHAR(500) NULL,
    CONSTRAINT CK_FGHL_Confirmed CHECK (ConfirmedQty IS NULL OR ConfirmedQty >= 0),
    CONSTRAINT CK_FGHL_Reason    CHECK (ConfirmedQty IS NULL OR ConfirmedQty = HandedQty OR DiscrepancyReason IS NOT NULL),
    CONSTRAINT UQ_FGHL UNIQUE (FgHandoverId, OrderLineId)
);
GO

/* =====================================================================================
   8. XUẤT HÀNG: phiếu xuất kho (kiêm lệnh giao hàng), xác nhận giao   (FR III.4)
   ===================================================================================== */

-- Phiếu xuất kho thành phẩm (DISPATCH_NOTE, FR 4.1–4.8): Quản đốc lập (thay lệnh giao hàng),
-- hàng được giữ chỗ; Thủ kho xác nhận xuất thì trừ kho; Kế toán xác nhận khách đã nhận. Ảnh phiếu ký: Attachment
CREATE TABLE dbo.DispatchNote (
    DispatchNoteId      INT IDENTITY(1,1) CONSTRAINT PK_DispatchNote PRIMARY KEY,
    DispatchCode        VARCHAR(30)    NOT NULL CONSTRAINT UQ_DispatchNote_Code UNIQUE,
    OrderId             INT            NOT NULL CONSTRAINT FK_DN_Order REFERENCES dbo.CustomerOrder(OrderId),
    DeliveryDate        DATE           NOT NULL,
    DeliveryAddress     NVARCHAR(500)  NOT NULL,
    Note                NVARCHAR(1000) NULL,
    Status              VARCHAR(25)    NOT NULL CONSTRAINT DF_DN_Status DEFAULT 'PENDING',
    CreatedAt           DATETIME2(0)   NOT NULL CONSTRAINT DF_DN_CreatedAt DEFAULT (dbo.fn_VnNow()),
    RejectReason        NVARCHAR(500)  NULL,     -- Thủ kho từ chối (không đủ hàng…)
    DispatchedBy        INT            NULL CONSTRAINT FK_DN_DispatchedBy REFERENCES dbo.Employee(EmployeeId),  -- Thủ kho
    DispatchedAt        DATETIME2(0)   NULL,
    CustomerReceivedDate DATE          NULL,
    RowVer              ROWVERSION,
    CONSTRAINT CK_DN_Status CHECK (Status IN ('PENDING','REJECTED','CANCELLED','IN_DELIVERY','DELIVERED')),
    CONSTRAINT CK_DN_Reject CHECK (Status <> 'REJECTED' OR RejectReason IS NOT NULL),
    CONSTRAINT CK_DN_Delivered CHECK (Status <> 'DELIVERED' OR CustomerReceivedDate IS NOT NULL),
    CONSTRAINT CK_DN_Dispatched CHECK (Status NOT IN ('IN_DELIVERY','DELIVERED') OR (DispatchedBy IS NOT NULL AND DispatchedAt IS NOT NULL))
);
CREATE INDEX IX_DN_Order ON dbo.DispatchNote(OrderId, Status);
CREATE TABLE dbo.DispatchNoteLine (
    DispatchNoteLineId INT IDENTITY(1,1) CONSTRAINT PK_DispatchNoteLine PRIMARY KEY,
    DispatchNoteId     INT NOT NULL CONSTRAINT FK_DNL_DN REFERENCES dbo.DispatchNote(DispatchNoteId),
    OrderLineId        INT NOT NULL CONSTRAINT FK_DNL_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    IssueQty           INT NOT NULL CONSTRAINT CK_DNL_Qty CHECK (IssueQty > 0),   -- giao luôn thành công: khách nhận đúng số xuất (GBR-24)
    CONSTRAINT UQ_DNL UNIQUE (DispatchNoteId, OrderLineId)
);
GO

/* =====================================================================================
   9. KHO: tồn hiện tại (StockBalance), kiểm kê, thẻ kho (STOCK_TRANSACTION); tồn + giữ chỗ xem vw_StockBalance
      Ba kho: MATERIAL (NVL theo Material + màu), SEMI_FINISHED (BTP theo dòng đơn),
      FINISHED (thành phẩm theo dòng đơn)
   ===================================================================================== */

-- Tồn hiện tại (25/10): mỗi mặt hàng trong mỗi kho một dòng. Chỉ usp_Stock_Post được thay đổi OnHandQty,
-- nên OnHandQty luôn bằng Σ StockTransaction.QtyChange. Giữ chỗ không lưu ở đây (tính trong vw_StockBalance)
CREATE TABLE dbo.StockBalance (
    StockBalanceId     INT IDENTITY(1,1) CONSTRAINT PK_StockBalance PRIMARY KEY,
    WarehouseType      VARCHAR(15)   NOT NULL,
    MaterialId         INT           NULL CONSTRAINT FK_SB_Material REFERENCES dbo.Material(MaterialId),       -- kho NVL
    OrderLineId        INT           NULL CONSTRAINT FK_SB_OrderLine REFERENCES dbo.OrderLine(OrderLineId),    -- kho BTP / TP: màu × size
    ProductComponentMaterialId INT   NULL CONSTRAINT FK_SB_PCM REFERENCES dbo.ProductComponentMaterial(ProductComponentMaterialId), -- BTP: NULL = bộ đủ; có = chi tiết × vải cắt bù (25/10)
    OnHandQty          DECIMAL(18,3) NOT NULL CONSTRAINT DF_SB_OnHand DEFAULT 0,
    UpdatedAt          DATETIME2(0)  NOT NULL CONSTRAINT DF_SB_UpdatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_SB_WH        CHECK (WarehouseType IN ('MATERIAL','SEMI_FINISHED','FINISHED')),
    CONSTRAINT CK_SB_Item      CHECK ((WarehouseType = 'MATERIAL' AND MaterialId IS NOT NULL AND OrderLineId IS NULL)
                                   OR (WarehouseType <> 'MATERIAL' AND MaterialId IS NULL AND OrderLineId IS NOT NULL)),
    CONSTRAINT CK_SB_Component CHECK (ProductComponentMaterialId IS NULL OR WarehouseType = 'SEMI_FINISHED'),
    CONSTRAINT CK_SB_OnHand    CHECK (OnHandQty >= 0),   -- GBR-07
    CONSTRAINT UQ_SB_Item UNIQUE (WarehouseType, MaterialId, OrderLineId, ProductComponentMaterialId)
);

CREATE TABLE dbo.Stocktake (
    StocktakeId    INT IDENTITY(1,1) CONSTRAINT PK_Stocktake PRIMARY KEY,
    StocktakeCode  VARCHAR(30)    NOT NULL CONSTRAINT UQ_Stocktake_Code UNIQUE,
    WarehouseType  VARCHAR(15)    NOT NULL,
    Scope          NVARCHAR(300)  NULL,
    StocktakeDate  DATE           NOT NULL,
    Status         VARCHAR(20)    NOT NULL CONSTRAINT DF_Stocktake_Status DEFAULT 'PENDING_APPROVAL',
    ReturnReason   NVARCHAR(500)  NULL,
    CreatedAt      DATETIME2(0)   NOT NULL CONSTRAINT DF_Stocktake_CreatedAt DEFAULT (dbo.fn_VnNow()),
    ApprovedAt     DATETIME2(0)   NULL,
    CONSTRAINT CK_Stocktake_WH     CHECK (WarehouseType IN ('MATERIAL','SEMI_FINISHED','FINISHED')),
    CONSTRAINT CK_Stocktake_Status CHECK (Status IN ('PENDING_APPROVAL','APPROVED','RETURNED')),
    CONSTRAINT CK_Stocktake_Return CHECK (Status <> 'RETURNED' OR ReturnReason IS NOT NULL),
    CONSTRAINT CK_Stocktake_Approved CHECK (Status <> 'APPROVED' OR ApprovedAt IS NOT NULL)
);
CREATE TABLE dbo.StocktakeLine (
    StocktakeLineId INT IDENTITY(1,1) CONSTRAINT PK_StocktakeLine PRIMARY KEY,
    StocktakeId     INT           NOT NULL CONSTRAINT FK_STL_Stocktake REFERENCES dbo.Stocktake(StocktakeId),
    StockBalanceId  INT           NOT NULL CONSTRAINT FK_STL_Balance REFERENCES dbo.StockBalance(StockBalanceId), -- mặt hàng được đếm (25/10)
    BookQty         DECIMAL(18,3) NOT NULL,                   -- số sổ tại lúc kiểm kê (sau đó tồn vẫn đổi)
    CountedQty      DECIMAL(18,3) NULL,                       -- trống = khớp sổ
    DifferenceQty   AS (ISNULL(CountedQty, BookQty) - BookQty),
    Note            NVARCHAR(300) NULL,
    CONSTRAINT UQ_STL UNIQUE (StocktakeId, StockBalanceId)
);

-- Thẻ kho: mỗi dòng chứng từ được xác nhận = 1 giao dịch; chỉ thêm, không sửa/xóa; chỉ ghi qua usp_Stock_Post.
-- BalanceAfter = StockBalance.OnHandQty sau giao dịch (thủ tục điền), không được âm (GBR-07)
CREATE TABLE dbo.StockTransaction (
    StockTransactionId BIGINT IDENTITY(1,1) CONSTRAINT PK_StockTransaction PRIMARY KEY,
    StockBalanceId     INT           NOT NULL CONSTRAINT FK_ST_Balance REFERENCES dbo.StockBalance(StockBalanceId), -- mặt hàng + kho (25/10)
    SourceDocType      VARCHAR(30)   NOT NULL,
    SourceDocId        INT           NOT NULL,
    SourceLineId       INT           NULL,
    QtyChange          DECIMAL(18,3) NOT NULL CONSTRAINT CK_ST_Qty CHECK (QtyChange <> 0),
    BalanceAfter       DECIMAL(18,3) NOT NULL CONSTRAINT CK_ST_Balance CHECK (BalanceAfter >= 0),
    TransactionDate    DATE          NOT NULL,
    CreatedAt          DATETIME2(0)  NOT NULL CONSTRAINT DF_ST_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_ST_DocType CHECK (SourceDocType IN ('MATERIAL_RECEIPT','MATERIAL_ISSUE','MATERIAL_RETURN','CUT_HANDOVER',
                                                       'FG_HANDOVER','DISPATCH_NOTE','STOCKTAKE')),
    CONSTRAINT UQ_ST_SourceLine UNIQUE (SourceDocType, SourceDocId, SourceLineId, StockBalanceId)  -- một giao dịch / dòng chứng từ / mặt hàng
);
CREATE INDEX IX_ST_Balance ON dbo.StockTransaction(StockBalanceId, CreatedAt);
GO

/* =====================================================================================
   10. CHẤM CÔNG (lấy từ Ronald Jack Pro) & SUẤT ĂN   (UC28–30, UC61–62, JOB-01..03, API-01)
   ---------------------------------------------------------------------------------
   Ronald Jack Pro (RJ) là phần mềm riêng, lưu trong file Access RJData.mdb. Hệ thống chỉ lấy
   4 trường của bảng CheckInOut: ID, MaChamCong, GioCham (và ngày suy ra từ GioCham).
   Nhân viên, phòng ban, máy chấm công vẫn quản lý bên RJ; nối với nhân viên qua
   Employee.AttendanceCode = NHANVIEN.MaChamCong. Chi tiết: file giải thích, mục 8.
   ===================================================================================== */

-- Một lần đọc RJ (JOB-01, ~10 phút) hoặc một lần import file Excel dự phòng (UC29)
CREATE TABLE dbo.AttendanceSyncSession (
    SyncSessionId      INT IDENTITY(1,1) CONSTRAINT PK_AttendanceSyncSession PRIMARY KEY,
    Source             VARCHAR(15)    NOT NULL,
    StartedAt          DATETIME2(0)   NOT NULL CONSTRAINT DF_ASS_StartedAt DEFAULT (dbo.fn_VnNow()),
    FinishedAt         DATETIME2(0)   NULL,
    LastSourceRecordId INT            NULL,    -- CheckInOut.ID lớn nhất đã đọc = mốc cho lần sau
    RecordsReceived    INT            NOT NULL CONSTRAINT DF_ASS_Received DEFAULT 0,
    RecordsInserted    INT            NOT NULL CONSTRAINT DF_ASS_Inserted DEFAULT 0, -- còn lại là trùng hoặc thuộc tháng đã khóa
    Status             VARCHAR(10)    NOT NULL CONSTRAINT DF_ASS_Status DEFAULT 'RUNNING',
    ErrorMessage       NVARCHAR(1000) NULL,
    FileName           NVARCHAR(300)  NULL,    -- file Excel dự phòng
    CONSTRAINT CK_ASS_Source CHECK (Source IN ('RJ_SYNC','EXCEL_IMPORT')),
    CONSTRAINT CK_ASS_Status CHECK (Status IN ('RUNNING','SUCCEEDED','FAILED'))
);
CREATE INDEX IX_ASS_StartedAt ON dbo.AttendanceSyncSession(StartedAt DESC) INCLUDE (Status, Source, LastSourceRecordId);

-- Lượt quẹt vân tay. Không sửa giờ quẹt; trùng (mã chấm công, giờ quẹt) bị bỏ qua (GBR-16)
CREATE TABLE dbo.AttendanceLog (
    AttendanceLogId BIGINT IDENTITY(1,1) CONSTRAINT PK_AttendanceLog PRIMARY KEY,
    AttendanceCode  INT           NOT NULL,                      -- CheckInOut.MaChamCong
    PunchTime       DATETIME2(0)  NOT NULL,                      -- CheckInOut.GioCham
    WorkDate        AS (CAST(PunchTime AS DATE)) PERSISTED,      -- ngày công (xưởng không có ca đêm)
    SourceRecordId  INT           NULL,                          -- CheckInOut.ID (NULL nếu từ file Excel)
    EmployeeId      INT           NULL CONSTRAINT FK_AL_Employee REFERENCES dbo.Employee(EmployeeId), -- NULL = mã chưa map
    SyncSessionId   INT           NOT NULL CONSTRAINT FK_AL_Session REFERENCES dbo.AttendanceSyncSession(SyncSessionId),
    CONSTRAINT UQ_AttendanceLog UNIQUE (AttendanceCode, PunchTime)
);
CREATE INDEX IX_AL_Employee ON dbo.AttendanceLog(EmployeeId, WorkDate) INCLUDE (PunchTime);
CREATE INDEX IX_AL_Unmapped ON dbo.AttendanceLog(AttendanceCode) WHERE EmployeeId IS NULL;
CREATE UNIQUE INDEX UX_AL_SourceRecord ON dbo.AttendanceLog(SourceRecordId) WHERE SourceRecordId IS NOT NULL;

-- Chấm hộ của Kế toán (đủ công) cho trường hợp ngoại lệ (GBR-25)
CREATE TABLE dbo.AttendanceAdjustment (
    AttendanceAdjustmentId INT IDENTITY(1,1) CONSTRAINT PK_AttendanceAdjustment PRIMARY KEY,
    EmployeeId      INT           NOT NULL CONSTRAINT FK_AA_Employee REFERENCES dbo.Employee(EmployeeId),
    WorkDate        DATE          NOT NULL,
    AdjustmentType  VARCHAR(20)   NOT NULL,
    Reason          NVARCHAR(500) NOT NULL,
    RequestId       INT           NULL CONSTRAINT FK_AA_Request REFERENCES dbo.Request(RequestId),
    CheckIn         TIME(0)       NULL,
    CheckOut        TIME(0)       NULL,
    RecordedAt      DATETIME2(0)  NOT NULL CONSTRAINT DF_AA_RecordedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_AA_Type CHECK (AdjustmentType IN ('FORGOT_PUNCH','FINGERPRINT_FAIL','OFFSITE_WORK','APPROVED_REQUEST')),
    CONSTRAINT CK_AA_Request CHECK (AdjustmentType <> 'APPROVED_REQUEST' OR RequestId IS NOT NULL),
    CONSTRAINT UQ_AA UNIQUE (EmployeeId, WorkDate)
);

-- Bảng công ngày: giờ vào = lượt đầu, giờ ra = lần ra đầu tiên; tính lại bằng
-- dbo.usp_Attendance_RebuildTimesheet sau mỗi lần đồng bộ cho các ngày có lượt mới
CREATE TABLE dbo.DailyTimesheet (
    DailyTimesheetId INT IDENTITY(1,1) CONSTRAINT PK_DailyTimesheet PRIMARY KEY,
    EmployeeId       INT           NOT NULL CONSTRAINT FK_DT_Employee REFERENCES dbo.Employee(EmployeeId),
    WorkDate         DATE          NOT NULL,
    CheckIn          DATETIME2(0)  NULL,
    CheckOut         DATETIME2(0)  NULL,
    WorkedHours      DECIMAL(5,2)  NOT NULL CONSTRAINT DF_DT_Worked DEFAULT 0,  -- giờ trong ca, đã trừ nghỉ trưa
    WorkdayValue     DECIMAL(4,2)  NOT NULL CONSTRAINT DF_DT_Workday DEFAULT 0 CONSTRAINT CK_DT_Workday CHECK (WorkdayValue BETWEEN 0 AND 1.5),
    OvertimeHours    DECIMAL(5,2)  NOT NULL CONSTRAINT DF_DT_OT DEFAULT 0 CONSTRAINT CK_DT_OT CHECK (OvertimeHours >= 0),
    IsMissingPunch   BIT           NOT NULL CONSTRAINT DF_DT_Missing DEFAULT 0, -- chỉ 1 lượt quẹt: thiếu giờ vào hoặc ra
    HasExtraPunch    BIT           NOT NULL CONSTRAINT DF_DT_Extra DEFAULT 0,   -- quẹt quá 2 lần: chỉ tính lần ra đầu tiên
    IsSunday         AS (CAST(IIF(DATEDIFF(DAY, 0, WorkDate) % 7 = 6, 1, 0) AS BIT)) PERSISTED, -- Chủ nhật: lương x SUNDAY_PAY_MULTIPLIER
    AttendanceAdjustmentId INT     NULL CONSTRAINT FK_DT_Adjustment REFERENCES dbo.AttendanceAdjustment(AttendanceAdjustmentId),
    Source           AS (CAST(IIF(AttendanceAdjustmentId IS NULL, 'FINGERPRINT', 'ADJUSTMENT') AS VARCHAR(15))) PERSISTED, -- có chấm bù = ADJUSTMENT
    Note             NVARCHAR(300) NULL,
    UpdatedAt        DATETIME2(0)  NOT NULL CONSTRAINT DF_DT_UpdatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT UQ_DailyTimesheet UNIQUE (EmployeeId, WorkDate)
);
CREATE INDEX IX_DT_WorkDate ON dbo.DailyTimesheet(WorkDate) INCLUDE (EmployeeId, WorkdayValue, OvertimeHours);

-- Suất ăn ngày (DAILY_MEAL): mở → tự chốt 08:30 → Kế toán chốt → đã báo bếp.
-- Suất của tổ, tổng suất, thành tiền xem vw_DailyMealTotal (cộng từ TeamMealConfirmation)
CREATE TABLE dbo.DailyMeal (
    DailyMealId        INT IDENTITY(1,1) CONSTRAINT PK_DailyMeal PRIMARY KEY,
    MealDate           DATE           NOT NULL CONSTRAINT UQ_DailyMeal_Date UNIQUE,
    Status             VARCHAR(20)    NOT NULL CONSTRAINT DF_DM_Status DEFAULT 'OPEN',
    OfficeCount        INT            NOT NULL CONSTRAINT DF_DM_Office DEFAULT 0,
    GuestCount         INT            NOT NULL CONSTRAINT DF_DM_Guest DEFAULT 0,   -- suất khách (FR 5.2)
    GuestNote          NVARCHAR(300)  NULL,
    MealPrice          DECIMAL(18,2)  NULL,
    AttendanceIncomplete BIT          NOT NULL CONSTRAINT DF_DM_Incomplete DEFAULT 0, -- "Thiếu dữ liệu chấm công"
    AutoClosedAt       DATETIME2(0)   NULL,
    ClosedAt           DATETIME2(0)   NULL,
    KitchenNotifiedAt  DATETIME2(0)   NULL,
    CONSTRAINT CK_DM_Status CHECK (Status IN ('OPEN','AUTO_CLOSED','CLOSED','KITCHEN_NOTIFIED')),
    CONSTRAINT CK_DM_Counts CHECK (OfficeCount >= 0 AND GuestCount >= 0),
    CONSTRAINT CK_DM_Price  CHECK (MealPrice IS NULL OR MealPrice >= 0)
);

-- Số suất ăn của từng tổ (FR 5.1); tổ chưa xác nhận thì JOB-03 lấy theo số người đã quẹt vân tay
CREATE TABLE dbo.TeamMealConfirmation (
    TeamMealConfirmationId INT IDENTITY(1,1) CONSTRAINT PK_TeamMealConfirmation PRIMARY KEY,
    DailyMealId     INT           NOT NULL CONSTRAINT FK_TMC_DailyMeal REFERENCES dbo.DailyMeal(DailyMealId),
    TeamId          INT           NOT NULL CONSTRAINT FK_TMC_Team REFERENCES dbo.Team(TeamId),
    MealCount       INT           NOT NULL CONSTRAINT CK_TMC_Count CHECK (MealCount >= 0),
    AttendanceCount INT           NOT NULL CONSTRAINT DF_TMC_Att DEFAULT 0,
    Source          VARCHAR(15)   NOT NULL,      -- CONFIRMED: tổ trưởng xác nhận; ATTENDANCE: hệ thống lấy theo chấm công
    ConfirmedAt     DATETIME2(0)  NULL,
    RemindedAt      DATETIME2(0)  NULL,          -- JOB-02 nhắc lúc 08:15
    CONSTRAINT CK_TMC_Source CHECK (Source IN ('CONFIRMED','ATTENDANCE')),
    CONSTRAINT CK_TMC_Confirmed CHECK (Source = 'ATTENDANCE' OR ConfirmedAt IS NOT NULL),
    CONSTRAINT UQ_TMC UNIQUE (DailyMealId, TeamId)
);
GO

/* =====================================================================================
   11. KỲ KẾ TOÁN, BẢNG LƯƠNG, CHI PHÍ THÁNG, HÒA VỐN   (UC11, UC12, UC31–33, ERD D6)
   ===================================================================================== */

CREATE TABLE dbo.AccountingPeriod (
    PeriodId     INT IDENTITY(1,1) CONSTRAINT PK_AccountingPeriod PRIMARY KEY,
    PeriodMonth  CHAR(7)       NOT NULL CONSTRAINT UQ_AccountingPeriod UNIQUE,   -- 'YYYY-MM'
    StartDate    DATE          NOT NULL,
    EndDate      DATE          NOT NULL,
    Status       VARCHAR(10)   NOT NULL CONSTRAINT DF_AP_Status DEFAULT 'OPEN',
    LockedAt     DATETIME2(0)  NULL,     -- mở khóa lại: ghi AuditLog (Action = UNLOCK, Reason)
    CONSTRAINT CK_AP_Status CHECK (Status IN ('OPEN','LOCKED')),
    CONSTRAINT CK_AP_Locked CHECK (Status <> 'LOCKED' OR LockedAt IS NOT NULL),
    CONSTRAINT CK_AP_Month  CHECK (PeriodMonth LIKE '[12][0-9][0-9][0-9]-[01][0-9]'),
    CONSTRAINT CK_AP_Dates  CHECK (EndDate >= StartDate)
);

-- Bảng lương tháng: Kế toán tính bằng Excel rồi import; Admin duyệt (GBR-12). File import ở Attachment
-- (OwnerType = PAYROLL, Purpose = IMPORT_FILE, UploadedAt = lúc import); tổng tiền = Σ Payslip.NetPay;
-- mở lại bảng lương đã duyệt ghi AuditLog (Action = UNLOCK, Reason) giống mở khóa tháng
CREATE TABLE dbo.Payroll (
    PayrollId      INT IDENTITY(1,1) CONSTRAINT PK_Payroll PRIMARY KEY,
    PeriodId       INT            NOT NULL CONSTRAINT UQ_Payroll_Period UNIQUE
                                  CONSTRAINT FK_Payroll_Period REFERENCES dbo.AccountingPeriod(PeriodId),  -- một bảng lương / tháng
    Status         VARCHAR(20)    NOT NULL CONSTRAINT DF_Payroll_Status DEFAULT 'DRAFT',
    SubmittedAt    DATETIME2(0)   NULL,
    ApprovedAt     DATETIME2(0)   NULL,
    RejectReason   NVARCHAR(500)  NULL,
    RowVer         ROWVERSION,
    CONSTRAINT CK_Payroll_Status CHECK (Status IN ('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED')),
    CONSTRAINT CK_Payroll_Reject CHECK (Status <> 'REJECTED' OR RejectReason IS NOT NULL),
    CONSTRAINT CK_Payroll_Approved CHECK (Status <> 'APPROVED' OR ApprovedAt IS NOT NULL)
);

-- Phiếu lương từng nhân viên; chỉ xem được khi bảng lương APPROVED
CREATE TABLE dbo.Payslip (
    PayslipId        INT IDENTITY(1,1) CONSTRAINT PK_Payslip PRIMARY KEY,
    PayrollId        INT           NOT NULL CONSTRAINT FK_Payslip_Payroll REFERENCES dbo.Payroll(PayrollId),
    EmployeeId       INT           NOT NULL CONSTRAINT FK_Payslip_Employee REFERENCES dbo.Employee(EmployeeId),
    WorkDays         DECIMAL(5,2)  NULL,
    TotalAdditions   DECIMAL(18,2) NOT NULL CONSTRAINT DF_Payslip_Add DEFAULT 0,
    TotalDeductions  DECIMAL(18,2) NOT NULL CONSTRAINT DF_Payslip_Ded DEFAULT 0,
    Insurance        DECIMAL(18,2) NOT NULL CONSTRAINT DF_Payslip_Ins DEFAULT 0,
    NetPay           DECIMAL(18,2) NOT NULL,
    CONSTRAINT UQ_Payslip UNIQUE (PayrollId, EmployeeId)
);
-- Các khoản chi tiết trên phiếu lương (mẫu Excel chưa chốt nên để dạng dòng)
CREATE TABLE dbo.PayslipItem (
    PayslipItemId  INT IDENTITY(1,1) CONSTRAINT PK_PayslipItem PRIMARY KEY,
    PayslipId      INT           NOT NULL CONSTRAINT FK_PayslipItem_Payslip REFERENCES dbo.Payslip(PayslipId),
    ItemGroup      VARCHAR(15)   NOT NULL,
    ItemName       NVARCHAR(100) NOT NULL,
    Amount         DECIMAL(18,2) NOT NULL,
    SortOrder      SMALLINT      NOT NULL CONSTRAINT DF_PayslipItem_Sort DEFAULT 0,
    CONSTRAINT CK_PayslipItem_Group CHECK (ItemGroup IN ('ADDITION','DEDUCTION','INSURANCE','INFO'))
);

-- Chi phí tháng do Kế toán import (MONTHLY_COST, UC32). Import lại khi tháng chưa khóa = thay các dòng của tháng.
-- Hóa đơn bếp cuối tháng (UC30) là một dòng CostCategoryCode = 'MEALS'; đối soát với Σ vw_DailyMealTotal.TotalAmount.
-- File import lưu ở Attachment (OwnerType = 'ACCOUNTING_PERIOD').
CREATE TABLE dbo.MonthlyCost (
    MonthlyCostId       INT IDENTITY(1,1) CONSTRAINT PK_MonthlyCost PRIMARY KEY,
    PeriodId            INT           NOT NULL CONSTRAINT FK_MC_Period REFERENCES dbo.AccountingPeriod(PeriodId),
    CostCategoryCode    VARCHAR(30)   NOT NULL,
    CostType            VARCHAR(10)   NOT NULL,
    ProductId           INT           NULL CONSTRAINT FK_MC_Product REFERENCES dbo.Product(ProductId),
    Amount              DECIMAL(18,2) NOT NULL CONSTRAINT CK_MC_Amount CHECK (Amount >= 0),
    Quantity            INT           NULL,          -- vd. số suất trên hóa đơn bếp
    Note                NVARCHAR(300) NULL,
    ImportedAt          DATETIME2(0)  NOT NULL CONSTRAINT DF_MC_ImportedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_MC_Category CHECK (CostCategoryCode IN ('MEALS','FUEL','UTILITIES','PREMISES','MACHINE_REPAIR',
                                                          'MACHINE_SUPPLIES','STATIONERY','MATERIALS_BOUGHT','SCRAP','OTHER')),
    CONSTRAINT CK_MC_Type    CHECK (CostType IN ('FIXED','VARIABLE')),
    CONSTRAINT CK_MC_Product CHECK (CostType = 'FIXED' OR ProductId IS NOT NULL),
    CONSTRAINT UQ_MC UNIQUE (PeriodId, CostCategoryCode, ProductId)
);

-- Kết quả hòa vốn theo mã hàng (BREAK_EVEN_RESULT, UC33). Mỗi lần tính là một bộ dòng mới cùng CalculatedAt (GBR-17)
CREATE TABLE dbo.BreakEvenResult (
    BreakEvenResultId   INT IDENTITY(1,1) CONSTRAINT PK_BreakEvenResult PRIMARY KEY,
    PeriodId            INT           NOT NULL CONSTRAINT FK_BER_Period REFERENCES dbo.AccountingPeriod(PeriodId),
    ProductId           INT           NOT NULL CONSTRAINT FK_BER_Product REFERENCES dbo.Product(ProductId),
    AllocationBasis     VARCHAR(15)   NOT NULL,
    FinishedQty         INT           NOT NULL,
    CmtUnitPrice        DECIMAL(18,2) NOT NULL,
    Revenue             DECIMAL(18,2) NOT NULL,
    VariableCost        DECIMAL(18,2) NOT NULL,
    AllocatedFixedCost  DECIMAL(18,2) NOT NULL,
    BreakEvenQty        DECIMAL(18,2) NULL,     -- NULL khi đơn giá ≤ biến phí/sp
    PercentReached      DECIMAL(9,2)  NULL,
    SurplusValue        DECIMAL(18,2) NOT NULL, -- doanh thu − biến phí − định phí phân bổ
    IsAtRisk            BIT           NOT NULL, -- "Nguy cơ lỗ"
    CalculatedAt        DATETIME2(0)  NOT NULL CONSTRAINT DF_BER_At DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_BER_Basis CHECK (AllocationBasis IN ('LABOUR_HOURS','OUTPUT')),
    CONSTRAINT UQ_BER UNIQUE (PeriodId, CalculatedAt, ProductId)
);
GO

/* =====================================================================================
   12. XE & NHIÊN LIỆU   (UC35, FR 4.7)
   ===================================================================================== */

CREATE TABLE dbo.Vehicle (
    VehicleId         INT IDENTITY(1,1) CONSTRAINT PK_Vehicle PRIMARY KEY,
    PlateNumber       VARCHAR(20)   NOT NULL CONSTRAINT UQ_Vehicle_Plate UNIQUE,
    VehicleType       NVARCHAR(50)  NOT NULL,
    FuelNormPer100Km  DECIMAL(6,2)  NOT NULL CONSTRAINT CK_Vehicle_Norm CHECK (FuelNormPer100Km > 0),
    Status            VARCHAR(20)   NOT NULL CONSTRAINT DF_Vehicle_Status DEFAULT 'ACTIVE',
    CONSTRAINT CK_Vehicle_Status CHECK (Status IN ('ACTIVE','DISCONTINUED'))
);

CREATE TABLE dbo.VehicleTrip (
    VehicleTripId     INT IDENTITY(1,1) CONSTRAINT PK_VehicleTrip PRIMARY KEY,
    VehicleId         INT           NOT NULL CONSTRAINT FK_VT_Vehicle REFERENCES dbo.Vehicle(VehicleId),
    TripDate          DATE          NOT NULL,
    DriverName        NVARCHAR(100) NULL,      -- chỉ ghi khi tài xế không phải nhân viên
    DriverEmployeeId  INT           NULL CONSTRAINT FK_VT_Driver REFERENCES dbo.Employee(EmployeeId),
    Purpose           NVARCHAR(300) NULL,
    DispatchNoteId    INT           NULL CONSTRAINT FK_VT_Dispatch REFERENCES dbo.DispatchNote(DispatchNoteId),
    OdometerStart     INT           NOT NULL CONSTRAINT CK_VT_Start CHECK (OdometerStart >= 0),
    OdometerEnd       INT           NULL,
    FuelLiters        DECIMAL(8,2)  NULL CONSTRAINT CK_VT_Fuel CHECK (FuelLiters IS NULL OR FuelLiters >= 0),
    FuelCost          DECIMAL(18,2) NULL CONSTRAINT CK_VT_Cost CHECK (FuelCost IS NULL OR FuelCost >= 0),
    Status            VARCHAR(10)   NOT NULL CONSTRAINT DF_VT_Status DEFAULT 'STARTED',
    KmGapWarning      BIT           NOT NULL CONSTRAINT DF_VT_Gap DEFAULT 0,      -- MSG30
    FuelOverNormWarning BIT         NOT NULL CONSTRAINT DF_VT_Over DEFAULT 0,     -- vượt định mức > 15%
    RecordedAt        DATETIME2(0)  NOT NULL CONSTRAINT DF_VT_RecordedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_VT_End    CHECK (OdometerEnd IS NULL OR OdometerEnd > OdometerStart),
    CONSTRAINT CK_VT_Status CHECK (Status IN ('STARTED','COMPLETED')),
    CONSTRAINT CK_VT_Completed CHECK (Status = 'STARTED' OR OdometerEnd IS NOT NULL),
    CONSTRAINT CK_VT_Driver CHECK ((DriverEmployeeId IS NULL AND DriverName IS NOT NULL) OR (DriverEmployeeId IS NOT NULL AND DriverName IS NULL)),
    CONSTRAINT UQ_VT UNIQUE (VehicleId, TripDate, OdometerStart)
);
GO

/* =====================================================================================
   13. HỆ THỐNG: tệp đính kèm, nhật ký thay đổi (GBR-15) – gồm cả lịch sử trạng thái đơn từ, mở khóa tháng, đổi tổ
   ---------------------------------------------------------------------------------
   Quy tắc nối tới Employee: chỉ giữ khóa ngoại khi (1) bản ghi nói về chính nhân viên đó
   (công nhân, người gây lỗi, người được chấm công, người nhận thông báo…) hoặc (2) bước giao–nhận /
   xác nhận làm thay đổi kho hay trách nhiệm mà vai trò hoặc tổ không cho biết là ai (người cấp, người
   nhận, thủ kho xác nhận, QC kiểm, người lập đơn từ). Mọi "ai tạo / ai sửa / ai duyệt" còn lại
   backend ghi vào AuditLog (INSERT, UPDATE, APPROVE, REJECT, CONFIRM, LOCK, UNLOCK) trong cùng
   transaction, tra bằng IX_AuditLog_Record.
   ===================================================================================== */

-- Ảnh/tệp đính kèm dùng chung cho mọi chứng từ (đơn từ ≤ 5 ảnh, chứng từ thanh toán, ảnh phiếu…)
CREATE TABLE dbo.Attachment (
    AttachmentId  BIGINT IDENTITY(1,1) CONSTRAINT PK_Attachment PRIMARY KEY,
    OwnerType     VARCHAR(30)    NOT NULL,
    OwnerId       INT            NOT NULL,
    Purpose       VARCHAR(30)    NOT NULL CONSTRAINT DF_Attachment_Purpose DEFAULT 'PHOTO',
    FileUrl       NVARCHAR(500)  NOT NULL,     -- đường dẫn S3
    FileName      NVARCHAR(255)  NULL,
    ContentType   VARCHAR(100)   NULL,
    IsCurrent     BIT            NOT NULL CONSTRAINT DF_Attachment_IsCurrent DEFAULT 1, -- chụp lại ảnh phiếu ký: ảnh cũ giữ lại, IsCurrent = 0
    RemovedAt     DATETIME2(0)   NULL,         -- lúc gỡ ảnh (IsCurrent = 0); job dọn tệp xóa file và dòng này sau 15 ngày
    UploadedAt    DATETIME2(0)   NOT NULL CONSTRAINT DF_Attachment_UploadedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_Attachment_Owner CHECK (OwnerType IN ('REQUEST','PURCHASE_ORDER','MATERIAL_RECEIPT','MATERIAL_RETURN','DEFECT_RECORD',
                                                         'DISPATCH_NOTE','VEHICLE_TRIP','PAYROLL','ACCOUNTING_PERIOD','ATTENDANCE_IMPORT')),
    CONSTRAINT CK_Attachment_Purpose CHECK (Purpose IN ('PHOTO','DOCUMENT_PHOTO','PAYMENT_PROOF','SIGNED_ISSUE_SLIP',
                                                         'SIGNED_DELIVERY_NOTE','FUEL_RECEIPT','IMPORT_FILE'))
);
CREATE INDEX IX_Attachment_Owner ON dbo.Attachment(OwnerType, OwnerId);

-- Nhật ký thay đổi: ai, lúc nào, giá trị cũ/mới, lý do (GBR-15, UC22, NFR Audit log)
CREATE TABLE dbo.AuditLog (
    AuditLogId    BIGINT IDENTITY(1,1) CONSTRAINT PK_AuditLog PRIMARY KEY,
    TableName     VARCHAR(50)    NOT NULL,
    RecordId      BIGINT         NOT NULL,
    Action        VARCHAR(10)    NOT NULL,
    OldValues     NVARCHAR(MAX)  NULL,     -- JSON
    NewValues     NVARCHAR(MAX)  NULL,     -- JSON
    Reason        NVARCHAR(500)  NULL,     -- bắt buộc khi Quản đốc sửa dữ liệu quá hạn
    Summary       NVARCHAR(500)  NULL,     -- câu mô tả tiếng Việt do backend dựng lúc ghi (AuditText)
    ChangedBy     INT            NULL CONSTRAINT FK_AuditLog_ChangedBy REFERENCES dbo.Employee(EmployeeId),
    ChangedAt     DATETIME2(0)   NOT NULL CONSTRAINT DF_AuditLog_ChangedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_AuditLog_Action CHECK (Action IN ('INSERT','UPDATE','DELETE','APPROVE','REJECT','CONFIRM','LOCK','UNLOCK','LOGIN')),
    CONSTRAINT CK_AuditLog_Json CHECK ((OldValues IS NULL OR ISJSON(OldValues) = 1) AND (NewValues IS NULL OR ISJSON(NewValues) = 1))
);
CREATE INDEX IX_AuditLog_Record ON dbo.AuditLog(TableName, RecordId, ChangedAt);

GO

/* =====================================================================================
   14. KHÓA NGOẠI VÒNG (thêm sau vì hai bảng tham chiếu lẫn nhau)
   ===================================================================================== */
ALTER TABLE dbo.Request ADD
    CONSTRAINT FK_Request_WorkOrder       FOREIGN KEY (WorkOrderId)          REFERENCES dbo.WorkOrder(WorkOrderId),
    CONSTRAINT FK_Request_MaterialIssue   FOREIGN KEY (MaterialIssueId)      REFERENCES dbo.MaterialIssue(MaterialIssueId),
    CONSTRAINT FK_Request_MaterialReceipt FOREIGN KEY (MaterialReceiptId)    REFERENCES dbo.MaterialReceipt(MaterialReceiptId),
    CONSTRAINT FK_Request_DefectRecord    FOREIGN KEY (DefectRecordId)       REFERENCES dbo.DefectRecord(DefectRecordId);
ALTER TABLE dbo.RequestLine ADD
    CONSTRAINT FK_RequestLine_ReceiptLine FOREIGN KEY (MaterialReceiptLineId) REFERENCES dbo.MaterialReceiptLine(MaterialReceiptLineId);
-- Mỗi phiếu nhập chỉ sinh tối đa một yêu cầu thiếu NVL của chính nó (phiếu bổ sung vẫn thiếu thì sinh yêu cầu tiếp theo); mỗi phiếu Scrap tối đa một yêu cầu cắt bù
CREATE UNIQUE INDEX UX_Request_Receipt ON dbo.Request(MaterialReceiptId) WHERE MaterialReceiptId IS NOT NULL AND RequestTypeCode = 'SHORT_DELIVERY';
CREATE UNIQUE INDEX UX_Request_Defect  ON dbo.Request(DefectRecordId)    WHERE DefectRecordId IS NOT NULL;
GO

/* =====================================================================================
   15. HÀM & TRIGGER BẢO VỆ QUY TẮC CỐT LÕI
   ===================================================================================== */

-- Ngày thuộc kỳ đã khóa chưa? (GBR-10)
CREATE FUNCTION dbo.fn_IsPeriodLocked (@d DATE)
RETURNS BIT
AS
BEGIN
    RETURN CASE WHEN EXISTS (SELECT 1 FROM dbo.AccountingPeriod
                             WHERE Status = 'LOCKED' AND @d BETWEEN StartDate AND EndDate)
                THEN 1 ELSE 0 END;
END;
GO

-- Hết ngày làm việc kế tiếp sau @d (hạn sửa sản lượng & phiếu lỗi của tổ trưởng – GBR-09); bỏ qua Chủ nhật
CREATE FUNCTION dbo.fn_NextWorkingDayEnd (@d DATE)
RETURNS DATETIME2(0)
AS
BEGIN
    DECLARE @n DATE = DATEADD(DAY, 1, @d);
    IF DATEDIFF(DAY, 0, @n) % 7 = 6 SET @n = DATEADD(DAY, 1, @n);   -- 01/01/1900 là thứ Hai → 6 = Chủ nhật
    RETURN DATEADD(SECOND, -1, CAST(DATEADD(DAY, 1, @n) AS DATETIME2(0)));
END;
GO

-- Thẻ kho chỉ được thêm (append-only)
CREATE TRIGGER dbo.trg_StockTransaction_AppendOnly
ON dbo.StockTransaction
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 50001, N'Thẻ kho chỉ được thêm mới, không được sửa hoặc xóa.', 1;
END;
GO

-- Thẻ kho chỉ ghi qua usp_Stock_Post (25/10): ghi thẳng sẽ làm StockBalance lệch thẻ kho
CREATE TRIGGER dbo.trg_StockTransaction_PostOnly
ON dbo.StockTransaction
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    IF ISNULL(CAST(SESSION_CONTEXT(N'NXMTD_StockPost') AS INT), 0) <> 1
        THROW 50047, N'Thẻ kho chỉ được ghi qua dbo.usp_Stock_Post.', 1;
END;
GO

-- Dòng tồn: OnHandQty chỉ đổi qua usp_Stock_Post; chi tiết cắt bù phải là dòng định mức vải của đúng màu × size
CREATE TRIGGER dbo.trg_StockBalance_Check ON dbo.StockBalance AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF ISNULL(CAST(SESSION_CONTEXT(N'NXMTD_StockPost') AS INT), 0) <> 1
       AND EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.StockBalanceId = i.StockBalanceId
                   WHERE i.OnHandQty <> ISNULL(d.OnHandQty, 0))
        THROW 50047, N'Số tồn chỉ được thay đổi qua dbo.usp_Stock_Post.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.OrderLine ol                 ON ol.OrderLineId = i.OrderLineId
               JOIN dbo.ProductVariant olv           ON olv.ProductVariantId = ol.ProductVariantId
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductColorId <> olv.ProductColorId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết tồn kho phải là chi tiết vải trong định mức của đúng màu của dòng đơn.', 1;
END;
GO

/* Ghi kho (25/10): mọi chứng từ xác nhận đều gọi thủ tục này, mỗi dòng chứng từ một lần, trong transaction của backend.
   Truyền mặt hàng (@WarehouseType + NVL/màu hoặc dòng đơn/chi tiết), hoặc @StockBalanceId (kiểm kê).
   Thủ tục: tìm / tạo dòng StockBalance và khóa nó → chặn tồn âm (50048) → cập nhật OnHandQty →
   thêm StockTransaction với BalanceAfter = tồn mới. Trả về StockBalanceId, BalanceAfter. */
CREATE PROCEDURE dbo.usp_Stock_Post
    @WarehouseType      VARCHAR(15)   = NULL,
    @MaterialId         INT           = NULL,
    @OrderLineId        INT           = NULL,
    @ProductComponentMaterialId INT   = NULL,
    @StockBalanceId     INT           = NULL,
    @SourceDocType      VARCHAR(30),
    @SourceDocId        INT,
    @SourceLineId       INT           = NULL,
    @QtyChange          DECIMAL(18,3),
    @TransactionDate    DATE          = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @onHand DECIMAL(18,3), @after DECIMAL(18,3);
    BEGIN TRY
        BEGIN TRAN;
        IF @StockBalanceId IS NULL
        BEGIN
            SELECT @StockBalanceId = StockBalanceId
            FROM dbo.StockBalance WITH (UPDLOCK, HOLDLOCK)
            WHERE WarehouseType = @WarehouseType
              AND EXISTS (SELECT MaterialId, OrderLineId, ProductComponentMaterialId
                          INTERSECT SELECT @MaterialId, @OrderLineId, @ProductComponentMaterialId);
            IF @StockBalanceId IS NULL
            BEGIN
                INSERT dbo.StockBalance (WarehouseType, MaterialId, OrderLineId, ProductComponentMaterialId)
                VALUES (@WarehouseType, @MaterialId, @OrderLineId, @ProductComponentMaterialId);
                SET @StockBalanceId = SCOPE_IDENTITY();
            END
        END
        SELECT @onHand = OnHandQty FROM dbo.StockBalance WITH (UPDLOCK, HOLDLOCK) WHERE StockBalanceId = @StockBalanceId;
        IF @onHand IS NULL
            THROW 50048, N'Không tìm thấy dòng tồn kho.', 1;
        SET @after = @onHand + @QtyChange;
        IF @after < 0
            THROW 50048, N'Tồn kho không đủ để xuất.', 1;
        EXEC sys.sp_set_session_context @key = N'NXMTD_StockPost', @value = 1;
        UPDATE dbo.StockBalance SET OnHandQty = @after, UpdatedAt = dbo.fn_VnNow() WHERE StockBalanceId = @StockBalanceId;
        INSERT dbo.StockTransaction (StockBalanceId, SourceDocType, SourceDocId, SourceLineId, QtyChange, BalanceAfter, TransactionDate)
        VALUES (@StockBalanceId, @SourceDocType, @SourceDocId, @SourceLineId, @QtyChange, @after,
                ISNULL(@TransactionDate, CAST(dbo.fn_VnNow() AS DATE)));
        EXEC sys.sp_set_session_context @key = N'NXMTD_StockPost', @value = NULL;
        COMMIT;
        SELECT @StockBalanceId AS StockBalanceId, @after AS BalanceAfter;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 AND XACT_STATE() <> 0 ROLLBACK;
        EXEC sys.sp_set_session_context @key = N'NXMTD_StockPost', @value = NULL;
        THROW;
    END CATCH
END;
GO

-- Lượt chấm công không được sửa; không nhận lượt thuộc tháng đã khóa (JOB-01)
CREATE TRIGGER dbo.trg_AttendanceLog_Guard
ON dbo.AttendanceLog
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d JOIN inserted i ON i.AttendanceLogId = d.AttendanceLogId
               WHERE i.AttendanceCode <> d.AttendanceCode OR i.PunchTime <> d.PunchTime)
       OR (EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted))
        THROW 50002, N'Lượt chấm công không được sửa giờ quẹt hoặc xóa (chỉ được map nhân viên).', 1;
    -- Chỉ chặn lượt MỚI thuộc tháng đã khóa; map nhân viên / đánh dấu RJ đã xóa vẫn được phép
    IF NOT EXISTS (SELECT 1 FROM deleted)
       AND EXISTS (SELECT 1 FROM inserted i WHERE dbo.fn_IsPeriodLocked(i.WorkDate) = 1)
        THROW 50003, N'Không thể nhập chấm công vào tháng đã khóa.', 1;
END;
GO

-- Chặn ghi dữ liệu sản xuất / chấm hộ thuộc tháng đã khóa (GBR-10, MSG21)
CREATE TRIGGER dbo.trg_OutputRecord_PeriodLock ON dbo.OutputRecord AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted WHERE dbo.fn_IsPeriodLocked(WorkDate) = 1 AND Status <> 'LOCKED')
       OR EXISTS (SELECT 1 FROM deleted d WHERE dbo.fn_IsPeriodLocked(d.WorkDate) = 1
                  AND NOT EXISTS (SELECT 1 FROM inserted i WHERE i.OutputRecordId = d.OutputRecordId AND i.Status = 'LOCKED' AND d.Status = 'SAVED'))
        THROW 50010, N'Tháng đã khóa, không thể thay đổi sản lượng.', 1;
END;
GO
CREATE TRIGGER dbo.trg_DefectRecord_PeriodLock ON dbo.DefectRecord AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted WHERE dbo.fn_IsPeriodLocked(WorkDate) = 1 AND Status <> 'LOCKED')
       OR EXISTS (SELECT 1 FROM deleted d WHERE dbo.fn_IsPeriodLocked(d.WorkDate) = 1
                  AND NOT EXISTS (SELECT 1 FROM inserted i WHERE i.DefectRecordId = d.DefectRecordId AND i.Status = 'LOCKED' AND d.Status <> 'LOCKED'))
        THROW 50011, N'Tháng đã khóa, không thể thay đổi phiếu hàng lỗi.', 1;
END;
GO
CREATE TRIGGER dbo.trg_QcBatch_PeriodLock ON dbo.QcBatch AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted WHERE dbo.fn_IsPeriodLocked(WorkDate) = 1)
       OR EXISTS (SELECT 1 FROM deleted WHERE dbo.fn_IsPeriodLocked(WorkDate) = 1)
        THROW 50012, N'Tháng đã khóa, không thể thay đổi đợt kiểm.', 1;
END;
GO
CREATE TRIGGER dbo.trg_AttendanceAdjustment_PeriodLock ON dbo.AttendanceAdjustment AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted WHERE dbo.fn_IsPeriodLocked(WorkDate) = 1)
       OR EXISTS (SELECT 1 FROM deleted WHERE dbo.fn_IsPeriodLocked(WorkDate) = 1)
        THROW 50013, N'Tháng đã khóa, không thể chấm hộ.', 1;
END;
GO
CREATE TRIGGER dbo.trg_MonthlyCost_PeriodLock ON dbo.MonthlyCost AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.AccountingPeriod p ON p.PeriodId = i.PeriodId WHERE p.Status = 'LOCKED')
       OR EXISTS (SELECT 1 FROM deleted d JOIN dbo.AccountingPeriod p ON p.PeriodId = d.PeriodId WHERE p.Status = 'LOCKED')
        THROW 50014, N'Tháng đã khóa, không thể import lại chi phí.', 1;
END;
GO

-- Mỗi tổ cắt / may chỉ có tối đa 1 tổ trưởng và 1 tổ phó đang làm việc; hai vai trò này phải thuộc một tổ cắt hoặc may
CREATE TRIGGER dbo.trg_Employee_TeamLeadership ON dbo.Employee AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF NOT (UPDATE(RoleId) OR UPDATE(TeamId) OR UPDATE(Status)) RETURN;
    IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.Role r ON r.RoleId = i.RoleId
               LEFT JOIN dbo.Team t ON t.TeamId = i.TeamId
               WHERE r.RoleCode IN ('TEAM_LEADER','TEAM_DEPUTY') AND i.Status = 'ACTIVE'
                 AND (t.TeamId IS NULL OR t.TeamType NOT IN ('CUTTING','SEWING')))
        THROW 50030, N'Tổ trưởng / tổ phó phải thuộc một tổ cắt hoặc tổ may.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Employee e
               JOIN dbo.Role r ON r.RoleId = e.RoleId AND r.RoleCode IN ('TEAM_LEADER','TEAM_DEPUTY')
               WHERE e.Status = 'ACTIVE' AND e.TeamId IN (SELECT TeamId FROM inserted WHERE TeamId IS NOT NULL)
               GROUP BY e.TeamId, e.RoleId HAVING COUNT(*) > 1)
        THROW 50031, N'Mỗi tổ chỉ có một tổ trưởng và một tổ phó đang làm việc.', 1;
END;
GO

-- Suất ăn: không đổi số tổ / suất khách sau khi đã chốt 08:30 (GBR-13, MSG22)
CREATE TRIGGER dbo.trg_TeamMeal_Cutoff ON dbo.TeamMealConfirmation AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    -- JOB-03 tạo dòng ATTENDANCE cho tổ chưa xác nhận trước khi đổi trạng thái ngày, nên chỉ chặn khi ngày đã chốt
    IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.DailyMeal m ON m.DailyMealId = i.DailyMealId WHERE m.Status <> 'OPEN')
       OR EXISTS (SELECT 1 FROM deleted d JOIN dbo.DailyMeal m ON m.DailyMealId = d.DailyMealId WHERE m.Status <> 'OPEN')
        THROW 50020, N'Đã chốt suất ăn lúc 08:30, không thể thay đổi.', 1;
END;
GO
CREATE TRIGGER dbo.trg_DailyMeal_GuestCutoff ON dbo.DailyMeal AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.DailyMealId = i.DailyMealId
               WHERE d.Status <> 'OPEN' AND i.GuestCount <> d.GuestCount)
        THROW 50021, N'Đã chốt suất ăn lúc 08:30, không thể thay đổi suất khách.', 1;
END;
GO

-- Đơn giá đã duyệt là đơn giá khóa của sản lượng và hàng hỏng (OutputRecord / DefectRecord.PieceRateId):
-- không sửa số tiền, ngày hiệu lực, công đoạn, không xóa. Đổi giá = thêm dòng mới với ngày hiệu lực mới.
CREATE TRIGGER dbo.trg_PieceRate_Approved ON dbo.PieceRate AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted d LEFT JOIN inserted i ON i.PieceRateId = d.PieceRateId
               WHERE d.Status = 'APPROVED'
                 AND (i.PieceRateId IS NULL OR i.Rate <> d.Rate OR i.EffectiveDate <> d.EffectiveDate
                      OR i.OperationId <> d.OperationId OR i.Status <> 'APPROVED'))
        THROW 50040, N'Đơn giá đã duyệt không được sửa hoặc xóa; hãy tạo đơn giá mới với ngày hiệu lực mới.', 1;
END;
GO

/* ---------- Kiểm tra dòng phiếu khớp đầu phiếu (rà D3-D9, 23/10) ----------
   50041: chi tiết / dòng định mức không thuộc mã hàng (hoặc màu áo) của dòng đơn, hoặc không phải chi tiết vải
   50042: dòng phiếu trỏ sang đơn hàng / mã hàng / phiếu khác với đầu phiếu
   50043: NVL của dòng phiếu nhập khác NVL của dòng đơn mua
   50044: công đoạn / đơn giá không khớp mã hàng của dòng đơn
   50045: loại dòng không khớp loại kho của phiếu
   50046: dòng đơn từ cắt bù chi tiết mà không biết dòng đơn hoặc lệnh cắt
   50049: dòng bàn cắt bù chi tiết dùng NVL / màu khác vải của bàn cắt (25/10)
   50050: tổng số lần giao của công đoạn × màu × size vượt số bộ BTP tổ đã nhận, hoặc thu hồi quá số đang giao (25/10)
   50051: đã bỏ (26/10), không còn dùng
   50052: sản lượng vượt số lần được giao (trần sản lượng, 25/10)
   50053: đã bỏ (26/10), không còn dùng
   50055: bìa cắt không khớp đơn hàng: mã hàng không có trong đơn, nhóm bìa chưa khai vải, size tỉ lệ không có trong đơn (DEV-ALL-85)
   Chỉ kiểm tra khi thêm / sửa dòng; backend không đổi đơn hàng của đầu phiếu đã có dòng. */

/* Bìa cắt của đơn (DEV-ALL-85, 50055): mã hàng có trong đơn; nhóm bìa đã khai vải theo màu áo; tỉ lệ chỉ gồm size có trong đơn.
   Chỉ kiểm khi thêm / sửa; xóa dòng đơn sau khi có bìa thì backend tự chặn. */
CREATE TRIGGER dbo.trg_ProductMarker_Check ON dbo.ProductMarker AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderLine ol WHERE ol.OrderId = i.OrderId AND ol.ProductId = i.ProductId))
        THROW 50055, N'Bìa phải thuộc mã hàng có trong đơn hàng.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductMarkerFabric f WHERE f.ProductId = i.ProductId AND f.MarkerNo = i.MarkerNo))
        THROW 50055, N'Nhóm bìa chưa khai vải theo màu áo ở mã hàng (ProductMarkerFabric).', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.ProductMarkerRatio r ON r.ProductMarkerId = i.ProductMarkerId
               WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderLine ol JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
                                 WHERE ol.OrderId = i.OrderId AND ol.ProductId = i.ProductId AND pv.SizeCode = r.SizeCode))
        THROW 50055, N'Size trong tỉ lệ bìa phải là size có trong đơn hàng của mã hàng đó.', 1;
END;
GO

CREATE TRIGGER dbo.trg_ProductMarkerRatio_Check ON dbo.ProductMarkerRatio AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.ProductMarker pm ON pm.ProductMarkerId = i.ProductMarkerId
               WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderLine ol JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
                                 WHERE ol.OrderId = pm.OrderId AND ol.ProductId = pm.ProductId AND pv.SizeCode = i.SizeCode))
        THROW 50055, N'Size trong tỉ lệ bìa phải là size có trong đơn hàng của mã hàng đó.', 1;
END;
GO

CREATE TRIGGER dbo.trg_WorkOrderLine_Check ON dbo.WorkOrderLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.WorkOrder wo ON wo.WorkOrderId = i.WorkOrderId
               JOIN dbo.OrderLine ol ON ol.OrderLineId = i.OrderLineId
               WHERE ol.OrderId <> wo.OrderId OR ol.ProductId <> wo.ProductId)
        THROW 50042, N'Dòng lệnh cắt phải thuộc đúng đơn hàng và mã hàng của lệnh cắt.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.OrderLine ol                 ON ol.OrderLineId = i.OrderLineId
               JOIN dbo.ProductVariant olv           ON olv.ProductVariantId = ol.ProductVariantId
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductColorId <> olv.ProductColorId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết cắt phải là chi tiết vải (PANEL) trong định mức của đúng màu của dòng đơn.', 1;
END;
GO

CREATE TRIGGER dbo.trg_RequestLine_Check ON dbo.RequestLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.Request r    ON r.RequestId = i.RequestId
               JOIN dbo.OrderLine ol ON ol.OrderLineId = i.OrderLineId
               WHERE ol.OrderId <> r.OrderId)
        THROW 50042, N'Dòng đơn từ trỏ sang dòng của đơn hàng khác.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.Request r ON r.RequestId = i.RequestId
               WHERE i.ProductComponentMaterialId IS NOT NULL AND i.OrderLineId IS NULL AND r.WorkOrderId IS NULL)
        THROW 50046, N'Dòng cắt bù chi tiết phải ghi dòng đơn hoặc đơn từ phải gắn lệnh cắt.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               JOIN dbo.Request r                    ON r.RequestId = i.RequestId
               LEFT JOIN dbo.OrderLine ol            ON ol.OrderLineId = i.OrderLineId
               LEFT JOIN dbo.ProductVariant olv      ON olv.ProductVariantId = ol.ProductVariantId
               LEFT JOIN dbo.WorkOrder wo            ON wo.WorkOrderId = r.WorkOrderId
               WHERE pc.ComponentType <> 'PANEL'
                  OR (ol.OrderLineId IS NOT NULL AND pcm.ProductColorId <> olv.ProductColorId)
                  OR (ol.OrderLineId IS NULL AND pcm.ProductId <> ISNULL(wo.ProductId, pcm.ProductId)))
        THROW 50041, N'Chi tiết cắt bù phải là chi tiết vải trong định mức của đúng màu × size / mã hàng của lệnh cắt.', 1;
END;
GO

CREATE TRIGGER dbo.trg_PurchaseOrderLine_Check ON dbo.PurchaseOrderLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.PurchaseOrder po  ON po.PurchaseOrderId = i.PurchaseOrderId
               JOIN dbo.OrderMaterial om  ON om.OrderMaterialId = i.OrderMaterialId
               WHERE po.OrderId IS NULL OR om.OrderId <> po.OrderId)
        THROW 50042, N'Dòng đơn mua trỏ sang NVL của đơn hàng khác (đơn mua dùng chung thì ghi MaterialId + màu).', 1;
END;
GO

CREATE TRIGGER dbo.trg_MaterialReceiptLine_Check ON dbo.MaterialReceiptLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.MaterialReceipt mr     ON mr.MaterialReceiptId = i.MaterialReceiptId
               JOIN dbo.PurchaseOrderLine pol  ON pol.PurchaseOrderLineId = i.PurchaseOrderLineId
               WHERE mr.PurchaseOrderId IS NULL OR pol.PurchaseOrderId <> mr.PurchaseOrderId)
        THROW 50042, N'Dòng phiếu nhập trỏ sang dòng của đơn mua khác.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.MaterialReceipt mr ON mr.MaterialReceiptId = i.MaterialReceiptId
               JOIN dbo.OrderMaterial om   ON om.OrderMaterialId = i.OrderMaterialId
               WHERE mr.OrderId IS NULL OR om.OrderId <> mr.OrderId)
        THROW 50042, N'Dòng phiếu nhập trỏ sang NVL của đơn hàng khác.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.PurchaseOrderLine pol ON pol.PurchaseOrderLineId = i.PurchaseOrderLineId
               LEFT JOIN dbo.OrderMaterial om1 ON om1.OrderMaterialId = i.OrderMaterialId
               LEFT JOIN dbo.OrderMaterial om2 ON om2.OrderMaterialId = pol.OrderMaterialId
               WHERE COALESCE(om1.MaterialId, i.MaterialId) <> COALESCE(om2.MaterialId, pol.MaterialId))
        THROW 50043, N'NVL (đã gồm màu) của dòng phiếu nhập khác dòng đơn mua.', 1;
END;
GO

CREATE TRIGGER dbo.trg_MaterialIssue_Check ON dbo.MaterialIssue AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.WorkOrder wo ON wo.WorkOrderId = i.WorkOrderId WHERE wo.OrderId <> i.OrderId)
        THROW 50042, N'Phiếu cấp ghi đơn hàng khác với đơn hàng của lệnh cắt.', 1;
END;
GO

CREATE TRIGGER dbo.trg_MaterialIssueLine_Check ON dbo.MaterialIssueLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.MaterialIssue mi ON mi.MaterialIssueId = i.MaterialIssueId
               WHERE (mi.WarehouseType = 'MATERIAL' AND i.MaterialId IS NULL)
                  OR (mi.WarehouseType = 'SEMI_FINISHED' AND i.OrderLineId IS NULL))
        THROW 50045, N'Phiếu cấp NVL chỉ có dòng NVL, phiếu cấp BTP chỉ có dòng BTP.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.MaterialIssue mi ON mi.MaterialIssueId = i.MaterialIssueId
               JOIN dbo.OrderLine ol     ON ol.OrderLineId = i.OrderLineId
               WHERE ol.OrderId <> mi.OrderId)
        THROW 50042, N'Dòng phiếu cấp BTP thuộc đơn hàng khác với phiếu cấp.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.OrderLine ol                 ON ol.OrderLineId = i.OrderLineId
               JOIN dbo.ProductVariant olv           ON olv.ProductVariantId = ol.ProductVariantId
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductColorId <> olv.ProductColorId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết cấp BTP phải là chi tiết vải trong định mức của đúng màu của dòng đơn.', 1;
END;
GO

CREATE TRIGGER dbo.trg_MaterialReturnLine_Check ON dbo.MaterialReturnLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.MaterialReturn r      ON r.MaterialReturnId = i.MaterialReturnId
               JOIN dbo.MaterialIssueLine l   ON l.MaterialIssueLineId = i.MaterialIssueLineId
               WHERE l.MaterialIssueId <> r.MaterialIssueId)
        THROW 50042, N'Dòng phiếu trả phải là dòng của phiếu cấp mà phiếu trả gắn vào.', 1;
END;
GO

CREATE TRIGGER dbo.trg_CuttingLayLine_Check ON dbo.CuttingLayLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.CuttingLay cl ON cl.CuttingLayId = i.CuttingLayId
               JOIN dbo.WorkOrder wo  ON wo.WorkOrderId = cl.WorkOrderId
               JOIN dbo.OrderLine ol  ON ol.OrderLineId = i.OrderLineId
               WHERE ol.OrderId <> wo.OrderId OR ol.ProductId <> wo.ProductId)
        THROW 50042, N'Dòng bàn cắt phải thuộc đúng đơn hàng và mã hàng của lệnh cắt.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.OrderLine ol                 ON ol.OrderLineId = i.OrderLineId
               JOIN dbo.ProductVariant olv           ON olv.ProductVariantId = ol.ProductVariantId
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductColorId <> olv.ProductColorId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết cắt phải là chi tiết vải (PANEL) trong định mức của đúng màu của dòng đơn.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.CuttingLay cl                ON cl.CuttingLayId = i.CuttingLayId
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               WHERE pcm.MaterialId <> cl.FabricMaterialId)
        THROW 50049, N'Chi tiết cắt bù dùng vải (đã gồm màu) khác vải của bàn cắt.', 1;
END;
GO

CREATE TRIGGER dbo.trg_CutHandoverLine_Check ON dbo.CutHandoverLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.CutHandover h ON h.CutHandoverId = i.CutHandoverId
               JOIN dbo.WorkOrder wo  ON wo.WorkOrderId = h.WorkOrderId
               JOIN dbo.OrderLine ol  ON ol.OrderLineId = i.OrderLineId
               WHERE ol.OrderId <> wo.OrderId OR ol.ProductId <> wo.ProductId)
        THROW 50042, N'Dòng bàn giao BTP phải thuộc đúng đơn hàng và mã hàng của lệnh cắt.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.OrderLine ol                 ON ol.OrderLineId = i.OrderLineId
               JOIN dbo.ProductVariant olv           ON olv.ProductVariantId = ol.ProductVariantId
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductColorId <> olv.ProductColorId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết bàn giao phải là chi tiết vải trong định mức của đúng màu của dòng đơn.', 1;
END;
GO

CREATE TRIGGER dbo.trg_OperationAssignment_Check ON dbo.OperationAssignment AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.Operation op ON op.OperationId = i.OperationId
               WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderLine ol WHERE ol.OrderId = i.OrderId AND ol.ProductId = op.ProductId))
        THROW 50044, N'Công đoạn được phân công không thuộc mã hàng nào của đơn hàng.', 1;
END;
GO

/* Giao / thu hồi việc (25/10):
   - công đoạn thuộc mã hàng của dòng đơn (50044)
   - Σ số lần đang giao của tổ × dòng đơn × công đoạn (giao chưa bị từ chối − thu hồi đã xác nhận) ≤ số bộ BTP tổ đã nhận (50050);
     tổ trưởng chia cho từng công nhân tùy ý trong giới hạn này
   - mỗi công nhân không bị thu hồi quá số đang giữ (giao đã xác nhận − thu hồi chưa bị từ chối ≥ 0) (50050)
   - sau thu hồi, trần không nhỏ hơn sản lượng đã báo (50052) */
CREATE TRIGGER dbo.trg_WorkAllocation_Check ON dbo.WorkAllocation AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.OrderLine ol ON ol.OrderLineId = i.OrderLineId
               JOIN dbo.Operation op ON op.OperationId = i.OperationId
               WHERE op.ProductId <> ol.ProductId)
        THROW 50044, N'Công đoạn không thuộc mã hàng của dòng đơn.', 1;
    IF EXISTS (SELECT 1 FROM (SELECT DISTINCT SewingTeamId, OrderLineId, OperationId FROM inserted) k
               OUTER APPLY (SELECT SUM(CAST(l.ConfirmedQty AS INT)) Qty
                            FROM dbo.MaterialIssue mi JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = mi.MaterialIssueId
                            WHERE mi.WarehouseType = 'SEMI_FINISHED' AND mi.Status = 'RECEIVED' AND mi.ReceivingTeamId = k.SewingTeamId
                              AND l.OrderLineId = k.OrderLineId AND l.ProductComponentMaterialId IS NULL) r
               OUTER APPLY (SELECT SUM(CASE WHEN w.MovementType = 'HANDOVER' THEN ISNULL(w.ConfirmedQty, w.Quantity)
                                            WHEN w.Status = 'CONFIRMED' THEN -w.ConfirmedQty ELSE 0 END) Qty
                            FROM dbo.WorkAllocation w
                            WHERE w.SewingTeamId = k.SewingTeamId AND w.OrderLineId = k.OrderLineId
                              AND w.OperationId = k.OperationId AND w.Status <> 'REJECTED') a
               WHERE ISNULL(a.Qty, 0) > ISNULL(r.Qty, 0))
        THROW 50050, N'Tổng số lần giao của công đoạn vượt số bộ BTP tổ đã nhận của màu × size này.', 1;
    IF EXISTS (SELECT 1 FROM (SELECT DISTINCT SewingTeamId, OrderLineId, OperationId, WorkerId FROM inserted) k
               CROSS APPLY (SELECT SUM(CASE WHEN w.MovementType = 'HANDOVER' THEN IIF(w.Status = 'CONFIRMED', w.ConfirmedQty, 0)
                                            ELSE -ISNULL(w.ConfirmedQty, w.Quantity) END) Qty
                            FROM dbo.WorkAllocation w
                            WHERE w.SewingTeamId = k.SewingTeamId AND w.OrderLineId = k.OrderLineId AND w.OperationId = k.OperationId
                              AND w.WorkerId = k.WorkerId AND w.Status <> 'REJECTED') a
               WHERE a.Qty < 0)
        THROW 50050, N'Thu hồi nhiều hơn số lần công nhân đang được giao.', 1;
    IF EXISTS (SELECT 1 FROM (SELECT DISTINCT WorkerId, OrderLineId, OperationId FROM inserted) k
               OUTER APPLY (SELECT SUM(CASE WHEN w.MovementType = 'HANDOVER' THEN w.ConfirmedQty ELSE -w.ConfirmedQty END) Qty
                            FROM dbo.WorkAllocation w
                            WHERE w.Status = 'CONFIRMED' AND w.WorkerId = k.WorkerId
                              AND w.OrderLineId = k.OrderLineId AND w.OperationId = k.OperationId) c
               OUTER APPLY (SELECT SUM(o.Quantity) - ISNULL((SELECT SUM(d.Quantity) FROM dbo.DefectRecord d
                                                          WHERE d.DefectType = 'OUTPUT_ERR' AND d.Status <> 'CANCELLED' AND d.ResponsibleWorkerId = k.WorkerId
                                                            AND d.OrderLineId = k.OrderLineId AND d.OperationId = k.OperationId), 0) Qty
                            FROM dbo.OutputRecord o
                            WHERE o.WorkerId = k.WorkerId AND o.OrderLineId = k.OrderLineId AND o.OperationId = k.OperationId) o
               WHERE o.Qty > ISNULL(c.Qty, 0))
        THROW 50052, N'Thu hồi làm số lần còn lại nhỏ hơn sản lượng công nhân đã báo.', 1;
END;
GO

CREATE TRIGGER dbo.trg_OutputRecord_Check ON dbo.OutputRecord AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.OrderLine ol  ON ol.OrderLineId = i.OrderLineId
               JOIN dbo.Operation op  ON op.OperationId = i.OperationId
               JOIN dbo.PieceRate pr  ON pr.PieceRateId = i.PieceRateId
               WHERE op.ProductId <> ol.ProductId OR pr.OperationId <> i.OperationId)
        THROW 50044, N'Công đoạn / đơn giá của sản lượng không khớp mã hàng của dòng đơn.', 1;
    -- trần sản lượng (25/10, chặn cứng): Σ sản lượng ≤ số lần được giao của công nhân ở công đoạn × dòng đơn
    IF EXISTS (SELECT 1 FROM (SELECT DISTINCT WorkerId, OrderLineId, OperationId FROM inserted) k
               OUTER APPLY (SELECT SUM(CASE WHEN w.MovementType = 'HANDOVER' THEN w.ConfirmedQty ELSE -w.ConfirmedQty END) Qty
                            FROM dbo.WorkAllocation w
                            WHERE w.Status = 'CONFIRMED' AND w.WorkerId = k.WorkerId
                              AND w.OrderLineId = k.OrderLineId AND w.OperationId = k.OperationId) c
               OUTER APPLY (SELECT SUM(o.Quantity) - ISNULL((SELECT SUM(d.Quantity) FROM dbo.DefectRecord d
                                                          WHERE d.DefectType = 'OUTPUT_ERR' AND d.Status <> 'CANCELLED' AND d.ResponsibleWorkerId = k.WorkerId
                                                            AND d.OrderLineId = k.OrderLineId AND d.OperationId = k.OperationId), 0) Qty
                            FROM dbo.OutputRecord o
                            WHERE o.WorkerId = k.WorkerId AND o.OrderLineId = k.OrderLineId AND o.OperationId = k.OperationId) o
               WHERE o.Qty > ISNULL(c.Qty, 0))
        THROW 50052, N'Sản lượng vượt số lần được giao cho công nhân ở công đoạn này.', 1;
END;
GO

CREATE TRIGGER dbo.trg_DefectRecord_Check ON dbo.DefectRecord AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.OrderLine ol      ON ol.OrderLineId = i.OrderLineId
               JOIN dbo.Operation op      ON op.OperationId = i.OperationId
               LEFT JOIN dbo.PieceRate pr ON pr.PieceRateId = i.PieceRateId
               WHERE op.ProductId <> ol.ProductId OR pr.OperationId <> i.OperationId)
        THROW 50044, N'Công đoạn / đơn giá của phiếu lỗi không khớp mã hàng của dòng đơn.', 1;
END;
GO

CREATE TRIGGER dbo.trg_FgHandoverLine_Check ON dbo.FgHandoverLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.FgHandover h ON h.FgHandoverId = i.FgHandoverId
               JOIN dbo.OrderLine ol ON ol.OrderLineId = i.OrderLineId
               WHERE ol.OrderId <> h.OrderId OR ol.ProductId <> h.ProductId)
        THROW 50042, N'Dòng bàn giao thành phẩm phải thuộc đúng đơn hàng và mã hàng của phiếu.', 1;
END;
GO

CREATE TRIGGER dbo.trg_DispatchNoteLine_Check ON dbo.DispatchNoteLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.DispatchNote d ON d.DispatchNoteId = i.DispatchNoteId
               JOIN dbo.OrderLine ol   ON ol.OrderLineId = i.OrderLineId
               WHERE ol.OrderId <> d.OrderId)
        THROW 50042, N'Dòng phiếu xuất phải thuộc đơn hàng của phiếu xuất.', 1;
END;
GO

CREATE TRIGGER dbo.trg_StocktakeLine_Check ON dbo.StocktakeLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.Stocktake st    ON st.StocktakeId = i.StocktakeId
               JOIN dbo.StockBalance sb ON sb.StockBalanceId = i.StockBalanceId
               WHERE sb.WarehouseType <> st.WarehouseType)
        THROW 50045, N'Dòng kiểm kê không khớp loại kho của phiếu kiểm kê.', 1;
END;
GO

/* =====================================================================================
   15b. ĐỒNG BỘ CHẤM CÔNG TỪ RONALD JACK PRO (JOB-01, UC28, UC29, UC61)
   ---------------------------------------------------------------------------------
   JOB-01: lấy mốc = MAX(LastSourceRecordId) của các phiên SUCCEEDED, đọc RJ
     SELECT ID, MaChamCong, GioCham FROM CheckInOut WHERE ID > @mốc ORDER BY ID
   rồi gọi usp_Attendance_IngestBatch. File Excel dự phòng: backend đọc file (cột mã chấm công,
   ngày, giờ vào, giờ ra), xem trước trong bộ nhớ, rồi gọi cùng thủ tục với @Source = 'EXCEL_IMPORT'.
   Gọi lại cùng một lô bao nhiêu lần cũng không sinh bản ghi trùng (GBR-16).
   ===================================================================================== */

CREATE TYPE dbo.AttendancePunchTableType AS TABLE (
    SourceRecordId  INT           NULL,      -- CheckInOut.ID (NULL nếu từ file Excel)
    AttendanceCode  INT           NOT NULL,  -- MaChamCong
    PunchTime       DATETIME2(0)  NOT NULL   -- GioCham
);
GO

-- Tính lại bảng công ngày cho một khoảng ngày (bỏ qua tháng đã khóa và ngày đã chấm hộ).
-- Quy tắc xưởng chốt 16/10, các mốc giờ trong SystemSetting (theo ca "Hành Chính" của RJ):
--   * lượt cách lượt trước dưới DUPLICATE_PUNCH_MINUTES là máy đọc lặp, bỏ qua;
--   * giờ vào = lượt đầu tiên; giờ ra = lượt kế tiếp (quẹt ra nhiều lần chỉ tính lần ra đầu tiên);
--   * tính theo giờ thực làm: giờ làm = phần nằm trong [WORK_START, WORK_END] trừ nghỉ trưa,
--     giờ quẹt cắt về phút; công = giờ làm / WORKDAY_STANDARD_HOURS (làm tròn 2 số, tối đa 1);
--   * tăng ca = giờ ra − WORK_END;
--   * Chủ nhật tính như trên; hệ số lương SUNDAY_PAY_MULTIPLIER áp ở view lương.
CREATE PROCEDURE dbo.usp_Attendance_RebuildTimesheet
    @FromDate   DATE,
    @ToDate     DATE,
    @EmployeeId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ws  TIME(0)      = (SELECT CAST(SettingValue AS TIME(0)) FROM dbo.SystemSetting WHERE SettingKey = 'WORK_START_TIME'),
            @we  TIME(0)      = (SELECT CAST(SettingValue AS TIME(0)) FROM dbo.SystemSetting WHERE SettingKey = 'WORK_END_TIME'),
            @ls  TIME(0)      = (SELECT CAST(SettingValue AS TIME(0)) FROM dbo.SystemSetting WHERE SettingKey = 'LUNCH_START_TIME'),
            @le  TIME(0)      = (SELECT CAST(SettingValue AS TIME(0)) FROM dbo.SystemSetting WHERE SettingKey = 'LUNCH_END_TIME'),
            @std DECIMAL(5,2) = (SELECT CAST(SettingValue AS DECIMAL(5,2)) FROM dbo.SystemSetting WHERE SettingKey = 'WORKDAY_STANDARD_HOURS'),
            @dup INT          = (SELECT CAST(SettingValue AS INT) FROM dbo.SystemSetting WHERE SettingKey = 'DUPLICATE_PUNCH_MINUTES');

    ;WITH raw AS (
        SELECT l.EmployeeId, l.WorkDate, l.PunchTime,
               LAG(l.PunchTime) OVER (PARTITION BY l.EmployeeId, l.WorkDate ORDER BY l.PunchTime) AS PrevPunch
        FROM dbo.AttendanceLog l
        WHERE l.EmployeeId IS NOT NULL
          AND l.WorkDate BETWEEN @FromDate AND @ToDate
          AND (@EmployeeId IS NULL OR l.EmployeeId = @EmployeeId)
          AND dbo.fn_IsPeriodLocked(l.WorkDate) = 0
    ), kept AS (     -- bỏ lượt máy đọc lặp
        SELECT raw.*, ROW_NUMBER() OVER (PARTITION BY EmployeeId, WorkDate ORDER BY PunchTime) AS Seq,
               COUNT(*) OVER (PARTITION BY EmployeeId, WorkDate) AS KeptCount
        FROM raw
        WHERE PrevPunch IS NULL OR DATEDIFF(SECOND, PrevPunch, PunchTime) >= @dup * 60
    ), p AS (
        SELECT k1.EmployeeId, k1.WorkDate, k1.KeptCount,
               k1.PunchTime AS CheckIn,
               k2.PunchTime AS CheckOut                -- lần ra đầu tiên
        FROM kept k1
        LEFT JOIN kept k2 ON k2.EmployeeId = k1.EmployeeId AND k2.WorkDate = k1.WorkDate AND k2.Seq = 2
        WHERE k1.Seq = 1
    ), b AS (
        SELECT p.*,
               DATEADD(MINUTE, DATEDIFF(MINUTE, CAST(p.WorkDate AS DATETIME2(0)), p.CheckIn),  CAST(p.WorkDate AS DATETIME2(0))) AS InM,
               DATEADD(MINUTE, DATEDIFF(MINUTE, CAST(p.WorkDate AS DATETIME2(0)), p.CheckOut), CAST(p.WorkDate AS DATETIME2(0))) AS OutM,
               DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00' AS TIME(0)), @ws), CAST(p.WorkDate AS DATETIME2(0))) AS WS,
               DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00' AS TIME(0)), @we), CAST(p.WorkDate AS DATETIME2(0))) AS WE,
               DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00' AS TIME(0)), @ls), CAST(p.WorkDate AS DATETIME2(0))) AS LS,
               DATEADD(SECOND, DATEDIFF(SECOND, CAST('00:00' AS TIME(0)), @le), CAST(p.WorkDate AS DATETIME2(0))) AS LE
        FROM p
    ), c AS (
        SELECT b.*,
               CASE WHEN CheckOut IS NULL THEN 0 ELSE
                 ( IIF(DATEDIFF(MINUTE, IIF(InM > WS, InM, WS), IIF(OutM < WE, OutM, WE)) > 0,
                       DATEDIFF(MINUTE, IIF(InM > WS, InM, WS), IIF(OutM < WE, OutM, WE)), 0)
                 - IIF(DATEDIFF(MINUTE, IIF(InM > LS, InM, LS), IIF(OutM < LE, OutM, LE)) > 0,
                       DATEDIFF(MINUTE, IIF(InM > LS, InM, LS), IIF(OutM < LE, OutM, LE)), 0)
                 ) / 60.0 END AS Worked,
               CASE WHEN CheckOut IS NULL OR OutM <= WE THEN 0 ELSE DATEDIFF(MINUTE, WE, OutM) / 60.0 END AS OT
        FROM b
    )
    SELECT EmployeeId, WorkDate, CheckIn, CheckOut,
           CAST(Worked AS DECIMAL(5,2)) AS WorkedHours,
           CAST(IIF(Worked >= @std, 1, ROUND(Worked / @std, 2)) AS DECIMAL(4,2)) AS WorkdayValue,
           CAST(OT AS DECIMAL(5,2)) AS OvertimeHours,
           CAST(IIF(CheckOut IS NULL, 1, 0) AS BIT) AS IsMissingPunch,
           CAST(IIF(KeptCount > 2, 1, 0) AS BIT) AS HasExtraPunch
    INTO #ts FROM c;

    MERGE dbo.DailyTimesheet AS t
    USING #ts AS s ON t.EmployeeId = s.EmployeeId AND t.WorkDate = s.WorkDate
    WHEN MATCHED AND t.Source = 'FINGERPRINT' THEN UPDATE SET
        CheckIn = s.CheckIn, CheckOut = s.CheckOut, WorkedHours = s.WorkedHours, WorkdayValue = s.WorkdayValue,
        OvertimeHours = s.OvertimeHours, IsMissingPunch = s.IsMissingPunch, HasExtraPunch = s.HasExtraPunch,
        UpdatedAt = dbo.fn_VnNow()
    WHEN NOT MATCHED BY TARGET THEN INSERT
        (EmployeeId, WorkDate, CheckIn, CheckOut, WorkedHours, WorkdayValue, OvertimeHours, IsMissingPunch, HasExtraPunch)
        VALUES (s.EmployeeId, s.WorkDate, s.CheckIn, s.CheckOut, s.WorkedHours, s.WorkdayValue, s.OvertimeHours, s.IsMissingPunch, s.HasExtraPunch);
END;
GO

-- Nhận một lô lượt quẹt: bỏ trùng, bỏ lượt thuộc tháng đã khóa, map nhân viên theo mã chấm công,
-- ghi phiên đồng bộ và tính lại bảng công các ngày có lượt mới. Trả về 1 dòng tổng kết (MSG24).
CREATE PROCEDURE dbo.usp_Attendance_IngestBatch
    @Rows        dbo.AttendancePunchTableType READONLY,
    @Source      VARCHAR(15)   = 'RJ_SYNC',
    @FileName    NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    INSERT dbo.AttendanceSyncSession (Source, FileName) VALUES (@Source, @FileName);
    DECLARE @sid INT = SCOPE_IDENTITY();

    BEGIN TRY
        BEGIN TRAN;
        SELECT r.SourceRecordId, r.AttendanceCode, r.PunchTime, CAST(r.PunchTime AS DATE) AS WorkDate,
               ROW_NUMBER() OVER (PARTITION BY r.AttendanceCode, r.PunchTime ORDER BY r.SourceRecordId) AS rn
        INTO #in FROM @Rows r;
        DECLARE @received INT = @@ROWCOUNT;
        DECLARE @locked INT = (SELECT COUNT(*) FROM #in WHERE rn = 1 AND dbo.fn_IsPeriodLocked(WorkDate) = 1);

        DECLARE @ins TABLE (EmployeeId INT NULL, WorkDate DATE);
        INSERT dbo.AttendanceLog (AttendanceCode, PunchTime, SourceRecordId, EmployeeId, SyncSessionId)
        OUTPUT inserted.EmployeeId, inserted.WorkDate INTO @ins
        SELECT i.AttendanceCode, i.PunchTime, i.SourceRecordId, e.EmployeeId, @sid
        FROM #in i
        LEFT JOIN dbo.Employee e ON e.AttendanceCode = i.AttendanceCode
        WHERE i.rn = 1 AND i.AttendanceCode > 0 AND dbo.fn_IsPeriodLocked(i.WorkDate) = 0
          AND NOT EXISTS (SELECT 1 FROM dbo.AttendanceLog l WHERE l.AttendanceCode = i.AttendanceCode
                          AND (l.PunchTime = i.PunchTime
                               -- file Excel chỉ có giờ:phút nên trùng theo phút với lượt đã đồng bộ
                               OR (i.SourceRecordId IS NULL AND l.PunchTime >= i.PunchTime AND l.PunchTime < DATEADD(MINUTE, 1, i.PunchTime))))
          AND (i.SourceRecordId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.AttendanceLog l WHERE l.SourceRecordId = i.SourceRecordId));
        DECLARE @inserted INT = @@ROWCOUNT;

        DECLARE @from DATE = (SELECT MIN(WorkDate) FROM @ins WHERE EmployeeId IS NOT NULL),
                @to   DATE = (SELECT MAX(WorkDate) FROM @ins WHERE EmployeeId IS NOT NULL);
        IF @from IS NOT NULL EXEC dbo.usp_Attendance_RebuildTimesheet @from, @to;

        UPDATE dbo.AttendanceSyncSession
        SET FinishedAt = dbo.fn_VnNow(), Status = 'SUCCEEDED', RecordsReceived = @received, RecordsInserted = @inserted,
            LastSourceRecordId = (SELECT MAX(SourceRecordId) FROM #in)
        WHERE SyncSessionId = @sid;
        COMMIT;

        SELECT @sid AS SyncSessionId, @received AS Received, @inserted AS Inserted,
               @locked AS SkippedLockedMonth, @received - @inserted - @locked AS Duplicates,
               (SELECT COUNT(*) FROM @ins WHERE EmployeeId IS NULL) AS Unmapped;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        UPDATE dbo.AttendanceSyncSession SET FinishedAt = dbo.fn_VnNow(), Status = 'FAILED', ErrorMessage = LEFT(ERROR_MESSAGE(), 1000)
        WHERE SyncSessionId = @sid;
        THROW;
    END CATCH
END;
GO

-- Kế toán map một mã chấm công chưa có chủ cho nhân viên (UC28): gán lại các lượt cũ và tính lại công
CREATE PROCEDURE dbo.usp_Attendance_MapCode
    @AttendanceCode INT, @EmployeeId INT, @ChangedBy INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRAN;
        UPDATE dbo.Employee SET AttendanceCode = @AttendanceCode, UpdatedAt = dbo.fn_VnNow() WHERE EmployeeId = @EmployeeId;
        UPDATE dbo.AttendanceLog SET EmployeeId = @EmployeeId WHERE AttendanceCode = @AttendanceCode AND EmployeeId IS NULL;
        INSERT dbo.AuditLog (TableName, RecordId, Action, NewValues, ChangedBy)
        VALUES ('Employee', @EmployeeId, 'UPDATE', CONCAT('{"AttendanceCode":', @AttendanceCode, '}'), @ChangedBy);
        DECLARE @from DATE = (SELECT MIN(WorkDate) FROM dbo.AttendanceLog WHERE EmployeeId = @EmployeeId),
                @to   DATE = (SELECT MAX(WorkDate) FROM dbo.AttendanceLog WHERE EmployeeId = @EmployeeId);
        IF @from IS NOT NULL EXEC dbo.usp_Attendance_RebuildTimesheet @from, @to, @EmployeeId;
    COMMIT;
END;
GO

-- Kế toán chấm bù cho một nhân viên trong một ngày (UC29, GBR-25): ghi AttendanceAdjustment,
-- ghi AuditLog và ghi đè dòng bảng công của ngày đó bằng Source = 'ADJUSTMENT' (sau này
-- usp_Attendance_RebuildTimesheet chỉ ghi đè dòng Source = 'FINGERPRINT' nên không xóa chấm bù).
-- Giờ làm / công / tăng ca tính theo ca Hành Chính như usp_Attendance_RebuildTimesheet.
CREATE PROCEDURE dbo.usp_Attendance_ApplyAdjustment
    @EmployeeId     INT,
    @WorkDate       DATE,
    @AdjustmentType VARCHAR(20),
    @Reason         NVARCHAR(500),
    @RequestId      INT = NULL,
    @CheckIn        TIME(0) = NULL,
    @CheckOut       TIME(0) = NULL,
    @ChangedBy      INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Tháng đã khóa đã bị trigger chặn; kiểm tra sớm để trả lỗi nghiệp vụ thân thiện (MSG21).
    IF dbo.fn_IsPeriodLocked(@WorkDate) = 1
        THROW 50013, N'Tháng đã khóa, không thể chấm hộ.', 1;

    DECLARE @ws  TIME(0)       = (SELECT CAST(SettingValue AS TIME(0)) FROM dbo.SystemSetting WHERE SettingKey = 'WORK_START_TIME'),
            @we  TIME(0)       = (SELECT CAST(SettingValue AS TIME(0)) FROM dbo.SystemSetting WHERE SettingKey = 'WORK_END_TIME'),
            @ls  TIME(0)       = (SELECT CAST(SettingValue AS TIME(0)) FROM dbo.SystemSetting WHERE SettingKey = 'LUNCH_START_TIME'),
            @le  TIME(0)       = (SELECT CAST(SettingValue AS TIME(0)) FROM dbo.SystemSetting WHERE SettingKey = 'LUNCH_END_TIME'),
            @std DECIMAL(5,2)  = (SELECT CAST(SettingValue AS DECIMAL(5,2)) FROM dbo.SystemSetting WHERE SettingKey = 'WORKDAY_STANDARD_HOURS');

    -- T-SQL không cho một DECLARE tham chiếu biến khai báo trong cùng câu lệnh, nên tách DECLARE và SET.
    DECLARE @base DATETIME2(0), @inM DATETIME2(0), @outM DATETIME2(0),
            @wsD  DATETIME2(0), @weD DATETIME2(0), @lsD  DATETIME2(0), @leD DATETIME2(0);

    SET @base = CAST(@WorkDate AS DATETIME2(0));
    SET @inM  = CASE WHEN @CheckIn  IS NULL THEN NULL ELSE DATEADD(MINUTE, DATEDIFF(MINUTE, 0, @CheckIn),  @base) END;
    SET @outM = CASE WHEN @CheckOut IS NULL THEN NULL ELSE DATEADD(MINUTE, DATEDIFF(MINUTE, 0, @CheckOut), @base) END;
    SET @wsD  = DATEADD(MINUTE, DATEDIFF(MINUTE, 0, @ws), @base);
    SET @weD  = DATEADD(MINUTE, DATEDIFF(MINUTE, 0, @we), @base);
    SET @lsD  = DATEADD(MINUTE, DATEDIFF(MINUTE, 0, @ls), @base);
    SET @leD  = DATEADD(MINUTE, DATEDIFF(MINUTE, 0, @le), @base);

    DECLARE @worked DECIMAL(5,2), @ot DECIMAL(5,2), @workday DECIMAL(4,2);

    SET @worked = CASE WHEN @CheckOut IS NULL THEN 0 ELSE
        ( IIF(DATEDIFF(MINUTE, IIF(@inM > @wsD, @inM, @wsD), IIF(@outM < @weD, @outM, @weD)) > 0,
              DATEDIFF(MINUTE, IIF(@inM > @wsD, @inM, @wsD), IIF(@outM < @weD, @outM, @weD)), 0)
        - IIF(DATEDIFF(MINUTE, IIF(@inM > @lsD, @inM, @lsD), IIF(@outM < @leD, @outM, @leD)) > 0,
              DATEDIFF(MINUTE, IIF(@inM > @lsD, @inM, @lsD), IIF(@outM < @leD, @outM, @leD)), 0)
        ) / 60.0 END;
    SET @ot = CASE WHEN @CheckOut IS NULL OR @outM <= @weD THEN 0
                   ELSE DATEDIFF(MINUTE, @weD, @outM) / 60.0 END;
    SET @workday = CAST(IIF(@worked >= @std, 1, ROUND(@worked / @std, 2)) AS DECIMAL(4,2));

    BEGIN TRAN;
        INSERT dbo.AttendanceAdjustment (EmployeeId, WorkDate, AdjustmentType, Reason, RequestId, CheckIn, CheckOut)
        VALUES (@EmployeeId, @WorkDate, @AdjustmentType, @Reason, @RequestId, @CheckIn, @CheckOut);
        DECLARE @aid INT = SCOPE_IDENTITY();

        MERGE dbo.DailyTimesheet AS t
        USING (SELECT @EmployeeId AS EmployeeId, @WorkDate AS WorkDate) AS s
            ON t.EmployeeId = s.EmployeeId AND t.WorkDate = s.WorkDate
        WHEN MATCHED THEN UPDATE SET
            CheckIn = @inM, CheckOut = @outM, WorkedHours = @worked, WorkdayValue = @workday,
            OvertimeHours = @ot, IsMissingPunch = IIF(@inM IS NULL OR @outM IS NULL, 1, 0),
            HasExtraPunch = 0, AttendanceAdjustmentId = @aid,
            Note = @Reason, UpdatedAt = dbo.fn_VnNow()
        WHEN NOT MATCHED THEN INSERT
            (EmployeeId, WorkDate, CheckIn, CheckOut, WorkedHours, WorkdayValue, OvertimeHours,
             IsMissingPunch, HasExtraPunch, AttendanceAdjustmentId, Note)
            VALUES (@EmployeeId, @WorkDate, @inM, @outM, @worked, @workday, @ot,
                    IIF(@inM IS NULL OR @outM IS NULL, 1, 0), 0, @aid, @Reason);

        INSERT dbo.AuditLog (TableName, RecordId, Action, NewValues, Reason, ChangedBy)
        VALUES ('AttendanceAdjustment', @aid, 'INSERT',
                CONCAT('{"EmployeeId":', @EmployeeId, ',"WorkDate":"', CONVERT(VARCHAR(10), @WorkDate, 23),
                       '","AdjustmentType":"', @AdjustmentType, '","CheckIn":"', ISNULL(CONVERT(VARCHAR(8), @CheckIn, 108), ''),
                       '","CheckOut":"', ISNULL(CONVERT(VARCHAR(8), @CheckOut, 108), ''), '"}'),
                @Reason, @ChangedBy);
    COMMIT;

    SELECT @aid AS AttendanceAdjustmentId;
END;
GO

/* =====================================================================================
   16. VIEW TÍNH TOÁN (tồn kho, trần sản lượng, tiến độ, lương tạm tính, KPI, doanh thu)
   ===================================================================================== */

-- Tồn hiện tại của 3 kho, tính từ thẻ kho (không lưu bảng số dư riêng):
-- tồn thực = Σ QtyChange; giữ chỗ = phiếu cấp phát chờ nhận + phiếu xuất TP chờ xuất; khả dụng = tồn thực − giữ chỗ
-- Kho BTP: ProductComponentMaterialId NULL = bộ đủ, có giá trị = chi tiết × vải cắt bù lẻ (vd tay áo vải lót), đếm riêng
CREATE VIEW dbo.vw_StockBalance
AS
SELECT  x.WarehouseType, x.MaterialId, x.OrderLineId, x.ProductComponentMaterialId,
        SUM(x.OnHandQty)                    AS OnHandQty,
        SUM(x.ReservedQty)                  AS ReservedQty,
        SUM(x.OnHandQty) - SUM(x.ReservedQty) AS AvailableQty
FROM (
    SELECT WarehouseType, MaterialId, OrderLineId, ProductComponentMaterialId, OnHandQty, CAST(0 AS DECIMAL(18,3)) AS ReservedQty
    FROM dbo.StockBalance   -- 25/10: đọc tồn đã lưu, không cộng lại thẻ kho
    UNION ALL      -- phiếu cấp NVL / BTP đang chờ tổ nhận
    SELECT i.WarehouseType, l.MaterialId, l.OrderLineId, l.ProductComponentMaterialId, 0, l.IssuedQty
    FROM dbo.MaterialIssue i JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = i.MaterialIssueId
    WHERE i.Status = 'PENDING_RECEIPT'
    UNION ALL      -- phiếu xuất thành phẩm đang chờ Thủ kho xuất
    SELECT 'FINISHED', NULL, l.OrderLineId, NULL, 0, l.IssueQty
    FROM dbo.DispatchNote d JOIN dbo.DispatchNoteLine l ON l.DispatchNoteId = d.DispatchNoteId
    WHERE d.Status = 'PENDING'
) x
GROUP BY x.WarehouseType, x.MaterialId, x.OrderLineId, x.ProductComponentMaterialId;
GO

-- Dòng đơn mua kèm số đã nhận và số còn phải nhận: đã nhận = Σ số lượng đạt của các phiếu nhập ĐÃ XÁC NHẬN (data rule "Outstanding quantity per PO line")
-- Join theo PurchaseOrderLineId nên đúng cả với dòng dùng chung (OrderMaterialId NULL, TH-12)
CREATE VIEW dbo.vw_PurchaseOrderLineStatus
AS
SELECT  pl.PurchaseOrderLineId, pl.PurchaseOrderId, pl.OrderMaterialId,
        pl.OrderedQty, ISNULL(r.ReceivedQty, 0) AS ReceivedQty, pl.NotReplenishedQty,
        pl.OrderedQty - ISNULL(r.ReceivedQty, 0) - pl.NotReplenishedQty AS OutstandingQty,
        pl.UnitPrice, pl.Amount
FROM dbo.PurchaseOrderLine pl
OUTER APPLY (SELECT SUM(rl.AcceptedQty) AS ReceivedQty
             FROM dbo.MaterialReceiptLine rl
             JOIN dbo.MaterialReceipt rc ON rc.MaterialReceiptId = rl.MaterialReceiptId AND rc.Status = 'CONFIRMED'
             WHERE rl.PurchaseOrderLineId = pl.PurchaseOrderLineId) r;
GO

-- NVL khách cấp của đơn: số lần dự kiến giao (OrderMaterial) cạnh số lần đã nhận và số đã nhận.
-- Đã nhận = phiếu nhập khách cấp (PurchaseOrderId NULL) ĐÃ XÁC NHẬN có dòng NVL này, kể cả phiếu bổ sung
CREATE VIEW dbo.vw_OrderMaterialDelivery
AS
SELECT  om.OrderMaterialId, om.OrderId, om.MaterialId,
        om.CustomerQty, om.ExpectedDeliveryCount,
        ISNULL(r.ReceivedCount, 0) AS ReceivedCount,
        ISNULL(r.ReceivedQty, 0)   AS ReceivedQty,
        CASE WHEN om.CustomerQty > ISNULL(r.ReceivedQty, 0) THEN om.CustomerQty - ISNULL(r.ReceivedQty, 0) ELSE 0 END AS RemainingQty
FROM dbo.OrderMaterial om
OUTER APPLY (SELECT COUNT(DISTINCT rc.MaterialReceiptId) AS ReceivedCount, SUM(rl.AcceptedQty) AS ReceivedQty
             FROM dbo.MaterialReceiptLine rl
             JOIN dbo.MaterialReceipt rc ON rc.MaterialReceiptId = rl.MaterialReceiptId
                                        AND rc.Status = 'CONFIRMED' AND rc.PurchaseOrderId IS NULL
             WHERE rl.OrderMaterialId = om.OrderMaterialId) r
WHERE om.CustomerQty > 0;
GO

-- Đơn mua: số lần NCC dự kiến giao cạnh số lần đã nhận (số phiếu nhập ĐÃ XÁC NHẬN của đơn mua, kể cả phiếu bổ sung)
CREATE VIEW dbo.vw_PurchaseOrderDelivery
AS
SELECT  po.PurchaseOrderId, po.PoCode, po.OrderId, po.SupplierId, po.GoodsStatus,
        po.ExpectedDeliveryCount,
        ISNULL(r.ReceivedCount, 0) AS ReceivedCount,
        r.LastReceiptDate
FROM dbo.PurchaseOrder po
OUTER APPLY (SELECT COUNT(*) AS ReceivedCount, MAX(rc.ReceiptDate) AS LastReceiptDate
             FROM dbo.MaterialReceipt rc
             WHERE rc.PurchaseOrderId = po.PurchaseOrderId AND rc.Status = 'CONFIRMED') r;
GO

-- Tiến độ từng dòng lệnh cắt (24/10): kế hoạch, đã cắt (Σ ActualQty bàn cắt), đã vào kho BTP (Σ ConfirmedQty phiếu bàn giao
-- đã xác nhận), còn phải cắt. Khớp theo lệnh cắt + dòng đơn + dòng định mức (NULL = cả bộ)
CREATE VIEW dbo.vw_WorkOrderLineProgress
AS
SELECT  wol.WorkOrderLineId, wol.WorkOrderId, wo.WorkOrderCode, wo.CuttingTeamId, wo.Status AS WorkOrderStatus,
        wol.OrderLineId, wol.ProductComponentMaterialId, pcm.ProductComponentId, pcm.MaterialId, wol.PlannedQty,
        ISNULL(c.Qty, 0) AS CutQty,
        ISNULL(h.Qty, 0) AS HandedToWarehouseQty,
        CASE WHEN wol.PlannedQty > ISNULL(c.Qty, 0) THEN wol.PlannedQty - ISNULL(c.Qty, 0) ELSE 0 END AS RemainingQty
FROM dbo.WorkOrderLine wol
JOIN dbo.WorkOrder wo ON wo.WorkOrderId = wol.WorkOrderId
LEFT JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = wol.ProductComponentMaterialId
OUTER APPLY (SELECT SUM(l.ActualQty) Qty
             FROM dbo.CuttingLayLine l JOIN dbo.CuttingLay cl ON cl.CuttingLayId = l.CuttingLayId
             WHERE cl.WorkOrderId = wol.WorkOrderId AND l.OrderLineId = wol.OrderLineId
               AND EXISTS (SELECT l.ProductComponentMaterialId INTERSECT SELECT wol.ProductComponentMaterialId)) c
OUTER APPLY (SELECT SUM(l.ConfirmedQty) Qty
             FROM dbo.CutHandoverLine l JOIN dbo.CutHandover ch ON ch.CutHandoverId = l.CutHandoverId
             WHERE ch.WorkOrderId = wol.WorkOrderId AND ch.Status = 'CONFIRMED' AND l.OrderLineId = wol.OrderLineId
               AND EXISTS (SELECT l.ProductComponentMaterialId INTERSECT SELECT wol.ProductComponentMaterialId)) h;
GO

-- Tồn NVL kèm thông tin vật tư (màn hình kho NVL)
CREATE VIEW dbo.vw_MaterialStock
AS
SELECT  m.MaterialId, m.MaterialCode, m.MaterialName, m.Color, m.MaterialType, m.Category, m.Unit,
        sb.OnHandQty, sb.ReservedQty, sb.AvailableQty
FROM dbo.vw_StockBalance sb
JOIN dbo.Material m ON m.MaterialId = sb.MaterialId
WHERE sb.WarehouseType = 'MATERIAL';
GO

-- Trần sản lượng (25/10) theo công nhân × dòng đơn × công đoạn = số lần đã giao − đã thu hồi (đã xác nhận);
-- còn lại = trần − đã may. Công đoạn không dùng BTP vẫn có trần. trg_OutputRecord_Check chặn báo vượt (50052)
CREATE VIEW dbo.vw_WorkerOutputCap
AS
WITH Moves AS (
    SELECT WorkerId, OrderLineId, OperationId,
           SUM(CASE WHEN MovementType = 'HANDOVER' THEN ConfirmedQty ELSE -ConfirmedQty END) AS CapQty
    FROM dbo.WorkAllocation WHERE Status = 'CONFIRMED' GROUP BY WorkerId, OrderLineId, OperationId
), Outp AS (
    SELECT WorkerId, OrderLineId, OperationId, SUM(Quantity) AS OutputQty
    FROM dbo.OutputRecord GROUP BY WorkerId, OrderLineId, OperationId
), OutErr AS (
    SELECT ResponsibleWorkerId AS WorkerId, OrderLineId, OperationId, SUM(Quantity) AS ErrorQty
    FROM dbo.DefectRecord WHERE DefectType = 'OUTPUT_ERR' AND Status <> 'CANCELLED'
    GROUP BY ResponsibleWorkerId, OrderLineId, OperationId
), Scrap AS (
    SELECT ResponsibleWorkerId AS WorkerId, OrderLineId, OperationId, SUM(Quantity) AS ScrapQty
    FROM dbo.DefectRecord WHERE DefectType = 'SCRAP' AND Status <> 'CANCELLED'
    GROUP BY ResponsibleWorkerId, OrderLineId, OperationId
)
SELECT mv.WorkerId, mv.OrderLineId, mv.OperationId,
       mv.CapQty,
       ISNULL(o.OutputQty, 0) - ISNULL(oe.ErrorQty, 0) AS OutputQty,
       ISNULL(s.ScrapQty, 0)  AS ScrapQty,
       mv.CapQty - (ISNULL(o.OutputQty, 0) - ISNULL(oe.ErrorQty, 0)) AS RemainingQty
FROM Moves mv
LEFT JOIN Outp o  ON o.WorkerId = mv.WorkerId AND o.OrderLineId = mv.OrderLineId AND o.OperationId = mv.OperationId
LEFT JOIN OutErr oe ON oe.WorkerId = mv.WorkerId AND oe.OrderLineId = mv.OrderLineId AND oe.OperationId = mv.OperationId
LEFT JOIN Scrap s ON s.WorkerId = mv.WorkerId AND s.OrderLineId = mv.OrderLineId AND s.OperationId = mv.OperationId;
GO

-- Chia việc theo công đoạn (25/10): mỗi tổ × dòng đơn đã nhận BTP × công đoạn đang dùng của mã hàng.
-- Cần làm = số bộ BTP tổ đã nhận (mỗi sản phẩm qua mỗi công đoạn một lần); đã giao (đã xác nhận, trừ thu hồi);
-- đang chờ xác nhận; chưa giao = cần làm − đã giao − đang chờ; đã làm = Σ OutputRecord.Quantity
CREATE VIEW dbo.vw_OperationAllocationProgress
AS
WITH Received AS (
    SELECT i.ReceivingTeamId AS SewingTeamId, l.OrderLineId, SUM(CAST(l.ConfirmedQty AS INT)) AS Qty
    FROM dbo.MaterialIssue i JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = i.MaterialIssueId
    WHERE i.WarehouseType = 'SEMI_FINISHED' AND i.Status = 'RECEIVED' AND l.ProductComponentMaterialId IS NULL
    GROUP BY i.ReceivingTeamId, l.OrderLineId
)
SELECT  r.SewingTeamId, r.OrderLineId, op.OperationId,
        r.Qty AS RequiredQty,
        ISNULL(a.AllocatedQty, 0) AS AllocatedQty,
        ISNULL(a.PendingQty, 0)   AS PendingQty,
        r.Qty - ISNULL(a.AllocatedQty, 0) - ISNULL(a.PendingQty, 0) AS UnallocatedQty,
        ISNULL(o.OutputQty, 0) - ISNULL(oe.ErrorQty, 0) AS OutputQty
FROM Received r
JOIN dbo.OrderLine ol ON ol.OrderLineId = r.OrderLineId
JOIN dbo.Operation op ON op.ProductId = ol.ProductId AND op.TeamType = 'SEWING' AND op.Status = 'ACTIVE'
OUTER APPLY (SELECT SUM(CASE WHEN w.Status = 'CONFIRMED' THEN IIF(w.MovementType = 'HANDOVER', w.ConfirmedQty, -w.ConfirmedQty) ELSE 0 END) AS AllocatedQty,
                    SUM(CASE WHEN w.Status = 'PENDING' AND w.MovementType = 'HANDOVER' THEN w.Quantity ELSE 0 END) AS PendingQty
             FROM dbo.WorkAllocation w
             WHERE w.SewingTeamId = r.SewingTeamId AND w.OrderLineId = r.OrderLineId AND w.OperationId = op.OperationId) a
OUTER APPLY (SELECT SUM(x.Quantity) AS OutputQty FROM dbo.OutputRecord x
             WHERE x.SewingTeamId = r.SewingTeamId AND x.OrderLineId = r.OrderLineId AND x.OperationId = op.OperationId) o
OUTER APPLY (SELECT SUM(d.Quantity) AS ErrorQty FROM dbo.DefectRecord d
             WHERE d.DefectType = 'OUTPUT_ERR' AND d.Status <> 'CANCELLED'
               AND d.SewingTeamId = r.SewingTeamId AND d.OrderLineId = r.OrderLineId AND d.OperationId = op.OperationId) oe;
GO

-- Lương tạm tính theo ngày của công nhân may (UC59): Σ(SL × đơn giá khóa) − Σ(Scrap × đơn giá đó);
-- SL đã trừ phần tổ trưởng nhập sai (phiếu OUTPUT_ERR) theo đơn giá khóa của phiếu
CREATE VIEW dbo.vw_WorkerDailyEstimatedPay
AS
WITH Outp AS (
    SELECT r.WorkerId, r.WorkDate, SUM(r.Quantity) AS OutputQty, SUM(r.Quantity * pr.Rate) AS OutputAmount
    FROM dbo.OutputRecord r JOIN dbo.PieceRate pr ON pr.PieceRateId = r.PieceRateId
    GROUP BY r.WorkerId, r.WorkDate
), OutErr AS (
    SELECT d.ResponsibleWorkerId AS WorkerId, d.WorkDate, SUM(d.Quantity) AS ErrorQty, SUM(d.Quantity * pr.Rate) AS ErrorAmount
    FROM dbo.DefectRecord d JOIN dbo.PieceRate pr ON pr.PieceRateId = d.PieceRateId
    WHERE d.DefectType = 'OUTPUT_ERR' AND d.Status <> 'CANCELLED'
    GROUP BY d.ResponsibleWorkerId, d.WorkDate
), Scrap AS (
    SELECT d.ResponsibleWorkerId AS WorkerId, d.WorkDate, SUM(d.Quantity) AS ScrapQty, SUM(d.Quantity * pr.Rate) AS ScrapAmount
    FROM dbo.DefectRecord d JOIN dbo.PieceRate pr ON pr.PieceRateId = d.PieceRateId
    WHERE d.DefectType = 'SCRAP' AND d.Status <> 'CANCELLED'
    GROUP BY d.ResponsibleWorkerId, d.WorkDate
), Days AS (
    SELECT WorkerId, WorkDate FROM Outp UNION SELECT WorkerId, WorkDate FROM OutErr UNION SELECT WorkerId, WorkDate FROM Scrap
), Mult AS (
    SELECT CAST(SettingValue AS DECIMAL(4,2)) AS SundayMultiplier FROM dbo.SystemSetting WHERE SettingKey = 'SUNDAY_PAY_MULTIPLIER'
)
SELECT d.WorkerId, e.EmployeeCode, e.FullName, d.WorkDate,
       CAST(IIF(DATEDIFF(DAY, 0, d.WorkDate) % 7 = 6, 1, 0) AS BIT) AS IsSunday,
       ISNULL(o.OutputQty, 0) - ISNULL(oe.ErrorQty, 0)       AS OutputQty,
       ISNULL(s.ScrapQty, 0)     AS ScrapQty,
       ISNULL(o.OutputAmount, 0) - ISNULL(oe.ErrorAmount, 0) AS OutputAmount,
       CAST((ISNULL(o.OutputAmount, 0) - ISNULL(oe.ErrorAmount, 0)) * (IIF(DATEDIFF(DAY, 0, d.WorkDate) % 7 = 6, ISNULL(m.SundayMultiplier, 1), 1) - 1) AS DECIMAL(18,2)) AS SundayBonus,
       ISNULL(s.ScrapAmount, 0)  AS ScrapAmount,
       CAST((ISNULL(o.OutputAmount, 0) - ISNULL(oe.ErrorAmount, 0)) * IIF(DATEDIFF(DAY, 0, d.WorkDate) % 7 = 6, ISNULL(m.SundayMultiplier, 1), 1)
         - ISNULL(s.ScrapAmount, 0) AS DECIMAL(18,2)) AS EstimatedPay      -- Chủ nhật: tiền sản phẩm x hệ số, khoản trừ Scrap giữ nguyên
FROM Days d
OUTER APPLY (SELECT SundayMultiplier FROM Mult) m
JOIN dbo.Employee e ON e.EmployeeId = d.WorkerId AND e.PayType = 'PIECE_RATE'
LEFT JOIN Outp o  ON o.WorkerId = d.WorkerId AND o.WorkDate = d.WorkDate
LEFT JOIN OutErr oe ON oe.WorkerId = d.WorkerId AND oe.WorkDate = d.WorkDate
LEFT JOIN Scrap s ON s.WorkerId = d.WorkerId AND s.WorkDate = d.WorkDate;
GO

-- Tiến độ theo dòng đơn (UC21): đặt, cắt, vào kho BTP, cấp tổ may, hoàn thiện (QC), nhập kho TP, đã giao, lỗi
-- (bàn giao và cấp BTP chỉ đếm bộ đủ; chi tiết cắt bù lẻ không tính là sản phẩm)
CREATE VIEW dbo.vw_OrderLineProgress
AS
SELECT  ol.OrderLineId, ol.OrderId, o.OrderCode, ol.ProductId, p.ProductCode, ol.ProductVariantId, pcol.Color, pv.SizeCode, ol.OrderedQty,
        ISNULL(cut.Qty, 0)       AS CutQty,
        ISNULL(ch.Qty, 0)        AS CutHandedToWarehouseQty,
        ISNULL(sfi.Qty, 0)       AS IssuedToSewingQty,
        ISNULL(qc.Qty, 0)        AS FinishedQty,
        ISNULL(fg.Qty, 0)        AS FinishedReceivedQty,
        ISNULL(dl.Qty, 0)        AS DeliveredQty,
        ISNULL(df.ReworkQty, 0)  AS ReworkQty,
        ISNULL(df.ScrapQty, 0)   AS ScrapQty,
        CASE WHEN ISNULL(qc.Qty, 0) + ISNULL(df.ReworkQty, 0) + ISNULL(df.ScrapQty, 0) = 0 THEN 0
             ELSE CAST(100.0 * (ISNULL(df.ReworkQty, 0) + ISNULL(df.ScrapQty, 0))
                       / (ISNULL(qc.Qty, 0) + ISNULL(df.ReworkQty, 0) + ISNULL(df.ScrapQty, 0)) AS DECIMAL(5,2)) END AS DefectRatePct
FROM dbo.OrderLine ol
JOIN dbo.CustomerOrder o ON o.OrderId = ol.OrderId
JOIN dbo.Product p ON p.ProductId = ol.ProductId
JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
JOIN dbo.ProductColor pcol ON pcol.ProductColorId = pv.ProductColorId
OUTER APPLY (SELECT SUM(ActualQty) Qty FROM dbo.CuttingLayLine WHERE OrderLineId = ol.OrderLineId AND ProductComponentMaterialId IS NULL) cut
OUTER APPLY (SELECT SUM(l.ConfirmedQty) Qty FROM dbo.CutHandoverLine l JOIN dbo.CutHandover h ON h.CutHandoverId = l.CutHandoverId
             WHERE l.OrderLineId = ol.OrderLineId AND l.ProductComponentMaterialId IS NULL AND h.Status = 'CONFIRMED') ch
OUTER APPLY (SELECT SUM(CAST(l.ConfirmedQty AS INT)) Qty FROM dbo.MaterialIssueLine l JOIN dbo.MaterialIssue i ON i.MaterialIssueId = l.MaterialIssueId
             WHERE l.OrderLineId = ol.OrderLineId AND l.ProductComponentMaterialId IS NULL AND i.WarehouseType = 'SEMI_FINISHED' AND i.Status = 'RECEIVED') sfi
OUTER APPLY (SELECT SUM(FinishedQty) Qty FROM dbo.QcBatch WHERE OrderLineId = ol.OrderLineId) qc
OUTER APPLY (SELECT SUM(l.ConfirmedQty) Qty FROM dbo.FgHandoverLine l JOIN dbo.FgHandover h ON h.FgHandoverId = l.FgHandoverId
             WHERE l.OrderLineId = ol.OrderLineId AND h.Status = 'CONFIRMED') fg
OUTER APPLY (SELECT SUM(l.IssueQty) Qty FROM dbo.DispatchNoteLine l JOIN dbo.DispatchNote d ON d.DispatchNoteId = l.DispatchNoteId
             WHERE l.OrderLineId = ol.OrderLineId AND d.Status = 'DELIVERED') dl
OUTER APPLY (SELECT SUM(CASE WHEN DefectType = 'REWORK' THEN Quantity ELSE 0 END) ReworkQty,
                    SUM(CASE WHEN DefectType = 'SCRAP'  THEN Quantity ELSE 0 END) ScrapQty
             FROM dbo.DefectRecord WHERE OrderLineId = ol.OrderLineId AND Status <> 'CANCELLED') df;
GO

-- Kết quả KPI theo ngày (FR 3.14): chỉ tiêu ngày, hoàn thiện (QC của tổ), tỉ lệ lỗi, % đạt
CREATE VIEW dbo.vw_KpiDailyResult
AS
SELECT  k.KpiTargetId, k.SewingTeamId, k.OrderId, k.ProductId, dd.WorkDate,
        CAST(tg.DailyTarget AS DECIMAL(18,2)) AS DailyTarget,
        ISNULL(q.FinishedQty, 0) AS FinishedQty,
        ISNULL(d.DefectQty, 0) AS DefectQty,
        CASE WHEN ISNULL(q.FinishedQty, 0) + ISNULL(d.DefectQty, 0) = 0 THEN 0
             ELSE CAST(100.0 * ISNULL(d.DefectQty, 0) / (ISNULL(q.FinishedQty, 0) + ISNULL(d.DefectQty, 0)) AS DECIMAL(5,2)) END AS DefectRatePct,
        CASE WHEN ISNULL(tg.DailyTarget, 0) = 0 THEN NULL
             ELSE CAST(100.0 * ISNULL(q.FinishedQty, 0) / tg.DailyTarget AS DECIMAL(9,2)) END AS PercentAchieved
FROM dbo.KpiTarget k
CROSS APPLY (SELECT x.WorkDate FROM dbo.KpiDailyTarget x WHERE x.KpiTargetId = k.KpiTargetId
             UNION
             SELECT b.WorkDate
             FROM dbo.QcBatch b JOIN dbo.OrderLine ol ON ol.OrderLineId = b.OrderLineId
             WHERE b.SewingTeamId = k.SewingTeamId AND ol.OrderId = k.OrderId AND ol.ProductId = k.ProductId
               AND b.WorkDate BETWEEN k.StartDate AND k.EndDate) dd
LEFT JOIN dbo.KpiDailyTarget p ON p.KpiTargetId = k.KpiTargetId AND p.WorkDate = dd.WorkDate
CROSS APPLY (SELECT CASE WHEN p.TargetQty IS NOT NULL THEN 1.0 * p.TargetQty
                         WHEN EXISTS (SELECT 1 FROM dbo.KpiDailyTarget y WHERE y.KpiTargetId = k.KpiTargetId) THEN NULL
                         ELSE 1.0 * k.TargetFinishedQty / k.WorkingDays END AS DailyTarget) tg
OUTER APPLY (SELECT SUM(b.FinishedQty) AS FinishedQty
             FROM dbo.QcBatch b JOIN dbo.OrderLine ol ON ol.OrderLineId = b.OrderLineId
             WHERE b.SewingTeamId = k.SewingTeamId AND ol.OrderId = k.OrderId AND ol.ProductId = k.ProductId
               AND b.WorkDate = dd.WorkDate) q
OUTER APPLY (SELECT SUM(dr.Quantity) AS DefectQty
             FROM dbo.DefectRecord dr JOIN dbo.OrderLine ol ON ol.OrderLineId = dr.OrderLineId
             WHERE dr.SewingTeamId = k.SewingTeamId AND ol.OrderId = k.OrderId AND ol.ProductId = k.ProductId
               AND dr.WorkDate = dd.WorkDate AND dr.Status <> 'CANCELLED') d;
GO

-- Doanh thu tháng = SL hoàn thiện đã nhập kho TP trong tháng × đơn giá CMT (UC27, GBR-17)
CREATE VIEW dbo.vw_MonthlyRevenue
AS
SELECT  FORMAT(h.ConfirmedAt, 'yyyy-MM') AS PeriodMonth,
        ol.OrderId, ol.ProductId,
        SUM(l.ConfirmedQty)                     AS FinishedQty,
        SUM(l.ConfirmedQty * ol.CmtUnitPrice)   AS Revenue
FROM dbo.FgHandover h
JOIN dbo.FgHandoverLine l ON l.FgHandoverId = h.FgHandoverId
JOIN dbo.OrderLine ol ON ol.OrderLineId = l.OrderLineId
WHERE h.Status = 'CONFIRMED'
GROUP BY FORMAT(h.ConfirmedAt, 'yyyy-MM'), ol.OrderId, ol.ProductId;
GO

-- Dữ liệu xuất cho Kế toán làm lương tháng (UC31): ngày công, tăng ca, sản lượng × đơn giá, bậc thợ
CREATE VIEW dbo.vw_PayrollExportData
AS
SELECT  p.PeriodMonth, e.EmployeeId, e.EmployeeCode, e.FullName, r.RoleName, e.PayType, t.TeamName,
        ISNULL(ts.WorkDays, 0)        AS WorkDays,             -- tổng công, gồm cả Chủ nhật
        ISNULL(ts.SundayWorkDays, 0)  AS SundayWorkDays,       -- phần công Chủ nhật (lương x SUNDAY_PAY_MULTIPLIER)
        ISNULL(ts.OvertimeHours, 0)   AS OvertimeHours,
        ISNULL(ts.SundayOvertimeHours, 0) AS SundayOvertimeHours,
        ISNULL(pay.OutputAmount, 0)   AS PieceRateAmount,
        ISNULL(pay.SundayBonus, 0)    AS SundayPieceRateBonus, -- phần cộng thêm cho sản phẩm làm Chủ nhật
        ISNULL(pay.ScrapAmount, 0)    AS ScrapDeduction,
        g.GradeCode,
        g.MonthlyAmount               AS GradeAllowance
FROM dbo.AccountingPeriod p
CROSS JOIN dbo.Employee e
JOIN dbo.Role r ON r.RoleId = e.RoleId
LEFT JOIN dbo.Team t ON t.TeamId = e.TeamId
OUTER APPLY (SELECT SUM(WorkdayValue) WorkDays, SUM(OvertimeHours) OvertimeHours,
                    SUM(IIF(IsSunday = 1, WorkdayValue, 0)) SundayWorkDays, SUM(IIF(IsSunday = 1, OvertimeHours, 0)) SundayOvertimeHours
             FROM dbo.DailyTimesheet WHERE EmployeeId = e.EmployeeId AND WorkDate BETWEEN p.StartDate AND p.EndDate) ts
OUTER APPLY (SELECT SUM(OutputAmount) OutputAmount, SUM(SundayBonus) SundayBonus, SUM(ScrapAmount) ScrapAmount
             FROM dbo.vw_WorkerDailyEstimatedPay WHERE WorkerId = e.EmployeeId AND WorkDate BETWEEN p.StartDate AND p.EndDate) pay
OUTER APPLY (SELECT TOP (1) h.ProposedGradeCode AS GradeCode    -- lần đổi bậc gần nhất có hiệu lực trước cuối tháng
             FROM dbo.GradeEvaluation h WHERE h.WorkerId = e.EmployeeId AND h.Status = 'APPLIED' AND h.EffectiveDate <= p.EndDate
             ORDER BY h.EffectiveDate DESC, h.GradeEvaluationId DESC) gh
OUTER APPLY (SELECT TOP (1) gr.GradeCode, gr.MonthlyAmount
             FROM dbo.GradeLevel gr WHERE gr.GradeCode = COALESCE(gh.GradeCode, e.CurrentGradeCode) AND gr.EffectiveDate <= p.EndDate
             ORDER BY gr.EffectiveDate DESC) g     -- bậc và tiền bậc hiệu lực ngày cuối tháng
WHERE e.StartDate <= p.EndDate AND (e.ResignDate IS NULL OR e.ResignDate >= p.StartDate);
GO

-- Tổ phó đang thay tổ trưởng hôm nay (18/10): sau giờ LEADER_ABSENT_CHECK_TIME mà tổ trưởng chưa có giờ vào
-- (không quẹt, cũng chưa được Kế toán chấm hộ) thì ActingLeader = 1 cho các tổ phó ACTIVE của tổ đó.
-- Tổ trưởng quẹt hoặc được chấm hộ muộn hơn thì dòng tự trở về 0, dữ liệu tổ phó đã nhập vẫn giữ nguyên.
-- Backend dùng IsActingLeader để cấp cho tổ phó quyền như tổ trưởng của tổ mình (trong ngày).
-- Quyền áp dụng là TEAM_LEADER; Team.TeamType (CUTTING / SEWING) cho biết là tổ cắt hay tổ may. RoleId của tổ phó không đổi.
CREATE VIEW dbo.vw_TeamActingLeader
AS
WITH Now AS (
    SELECT CAST(dbo.fn_VnNow() AS DATE) AS Today, CAST(dbo.fn_VnNow() AS TIME(0)) AS NowTime,
           (SELECT CAST(SettingValue AS TIME(0)) FROM dbo.SystemSetting WHERE SettingKey = 'LEADER_ABSENT_CHECK_TIME') AS CheckTime
)
SELECT  t.TeamId, t.TeamCode, t.TeamType, l.EmployeeId AS LeaderEmployeeId, d.EmployeeId AS DeputyEmployeeId, d.FullName AS DeputyName, n.Today AS WorkDate,
        CAST(IIF(ts.EmployeeId IS NOT NULL, 1, 0) AS BIT) AS LeaderCheckedIn,
        CAST(IIF(ts.EmployeeId IS NULL AND n.NowTime >= n.CheckTime, 1, 0) AS BIT) AS IsActingLeader
FROM dbo.Team t
CROSS JOIN Now n
JOIN dbo.Role r ON r.RoleCode = 'TEAM_DEPUTY'
JOIN dbo.Employee d ON d.TeamId = t.TeamId AND d.RoleId = r.RoleId AND d.Status = 'ACTIVE'
JOIN dbo.Role rl ON rl.RoleCode = 'TEAM_LEADER'
JOIN dbo.Employee l ON l.TeamId = t.TeamId AND l.RoleId = rl.RoleId AND l.Status = 'ACTIVE'
LEFT JOIN dbo.DailyTimesheet ts ON ts.EmployeeId = l.EmployeeId AND ts.WorkDate = n.Today AND ts.CheckIn IS NOT NULL;
GO

-- Suất ăn mỗi ngày (UC30): suất của tổ cộng từ TeamMealConfirmation, tổng suất và thành tiền để báo bếp / đối soát
CREATE VIEW dbo.vw_DailyMealTotal
AS
SELECT m.DailyMealId, m.MealDate, m.Status, m.OfficeCount, m.GuestCount,
       ISNULL(t.TeamTotal, 0)                                AS TeamTotal,
       m.OfficeCount + m.GuestCount + ISNULL(t.TeamTotal, 0) AS TotalMeals,
       m.MealPrice,
       CONVERT(DECIMAL(18,2), (m.OfficeCount + m.GuestCount + ISNULL(t.TeamTotal, 0)) * m.MealPrice) AS TotalAmount
FROM dbo.DailyMeal m
OUTER APPLY (SELECT SUM(c.MealCount) AS TeamTotal FROM dbo.TeamMealConfirmation c WHERE c.DailyMealId = m.DailyMealId) t;
GO

/* =====================================================================================
   17. DỮ LIỆU DANH MỤC MẪU
   ===================================================================================== */
SET NOCOUNT ON;

INSERT dbo.Role (RoleCode, RoleName, IsProduction) VALUES
 ('ADMIN',            N'Giám đốc (Admin)',     0),
 ('MANAGER',          N'Quản đốc',             0),
 ('ACCOUNTANT',       N'Kế toán',              0),
 ('WAREHOUSE_KEEPER', N'Thủ kho',              0),
 ('TEAM_LEADER',      N'Tổ trưởng',            1),
 -- Tổ phó thay tổ trưởng thao tác khi tổ trưởng vắng, cùng bộ quyền TEAM_LEADER trong tổ của mình (xem vw_TeamActingLeader).
 ('TEAM_DEPUTY',      N'Tổ phó',               1),
 ('QAQC',             N'QA/QC',                1),
 ('ELECTRICIAN',      N'Cơ điện',              1),
 ('WORKER',           N'Công nhân',            1);
-- Cách tính lương (Employee.PayType) đặt theo từng người khi tạo: tổ may PIECE_RATE, tổ cắt và QA/QC DAILY, còn lại MONTHLY.

INSERT dbo.GradeLevelCode (GradeCode, GradeName) VALUES
 ('A', N'Bậc A – Giỏi'),
 ('B', N'Bậc B – Khá'),
 ('C', N'Bậc C – Trung bình'),
 ('D', N'Bậc D – Yếu');

INSERT dbo.GradeLevel (GradeCode, MonthlyAmount, EffectiveDate) VALUES
 ('A', 600000, '2026-01-01'),
 ('B', 400000, '2026-01-01'),
 ('C', 200000, '2026-01-01'),
 ('D',      0, '2026-01-01');

DECLARE @rAdmin INT = (SELECT RoleId FROM dbo.Role WHERE RoleCode = 'ADMIN');

INSERT dbo.SystemSetting (SettingKey, SettingValue, Description) VALUES
 ('MEAL_REMINDER_TIME',        N'08:15', N'JOB-02 nhắc tổ trưởng xác nhận suất ăn'),
 ('MEAL_CUTOFF_TIME',          N'08:30', N'JOB-03 tự chốt suất ăn'),
 ('KPI_EDIT_CUTOFF_TIME',      N'08:00', N'Sau giờ này không sửa được KPI theo ngày của chính ngày đó'),
 ('KPI_DUE_WARNING_DAYS',      N'3',     N'Cảnh báo khi kỳ KPI kết thúc cách ngày giao hàng không quá số ngày này'),
 ('ATTENDANCE_SYNC_MINUTES',   N'10',    N'Chu kỳ JOB-01 đọc Ronald Jack Pro'),
 ('ATTENDANCE_ALERT_MINUTES',  N'30',    N'Báo Kế toán khi không đồng bộ được quá số phút này'),
 ('DELAY_CHECK_TIME',          N'17:00', N'JOB-04 kiểm tra nguy cơ trễ đơn'),
 ('DELAY_AVG_DAYS',            N'7',     N'Số ngày lấy trung bình hoàn thiện cho JOB-04'),
 ('NOTIFICATION_RETENTION_DAYS', N'90',  N'JOB-05 xóa thông báo cũ'),
 ('CUT_SURPLUS_PERCENT',       N'3',     N'% cắt vượt số đặt cho phép (CHƯA CHỐT với xưởng)'),
 ('FUEL_NORM_TOLERANCE_PERCENT', N'15',  N'Cảnh báo xăng vượt định mức'),
 ('REQUEST_MAX_PHOTOS',        N'5',     N'Số ảnh tối đa trên một đơn từ'),
 -- Quy tắc tính công lấy theo ca "Hành Chính" (CA00001) đang cài trong Ronald Jack Pro
 ('WORK_START_TIME',           N'07:30', N'Giờ vào làm (ca Hành Chính RJ)'),
 ('WORK_END_TIME',             N'16:30', N'Giờ tan làm; quẹt ra sau giờ này tính tăng ca'),
 ('LUNCH_START_TIME',          N'11:30', N'Bắt đầu nghỉ trưa'),
 ('LUNCH_END_TIME',            N'12:30', N'Kết thúc nghỉ trưa'),
 ('DUPLICATE_PUNCH_MINUTES',   N'10',    N'Lượt quẹt cách lượt trước dưới số phút này là máy đọc lặp, bỏ qua'),
 ('SUNDAY_PAY_MULTIPLIER',     N'2',     N'Đi làm Chủ nhật được x2 lương (xưởng xác nhận 16/10)'),
 ('WORKDAY_STANDARD_HOURS',    N'8',     N'Số giờ của 1 công (TongGioTrongCaLamViec của ca Hành Chính)'),
 ('LEADER_ABSENT_CHECK_TIME',   N'08:00', N'Sau giờ này tổ trưởng chưa có giờ vào thì tổ phó thay thao tác (vw_TeamActingLeader)');

INSERT dbo.Customer (CustomerName, Phone, DefaultAddress) VALUES
 (N'Công ty mẹ (khai báo lại tên thật)', NULL, NULL);

INSERT dbo.Team (TeamCode, TeamName, TeamType) VALUES
 ('VP',  N'Khối văn phòng', 'OFFICE'),
 ('CAT', N'Tổ cắt',         'CUTTING');

-- Tài khoản Admin đầu tiên. PasswordHash phải được backend ghi đè bằng hash thật khi seed lần đầu.
INSERT dbo.Employee (EmployeeCode, FullName, RoleId, TeamId, PayType, StartDate)
VALUES ('ADMIN', N'Quản trị hệ thống', @rAdmin, (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'VP'), 'MONTHLY', '2026-01-01');
INSERT dbo.Account (EmployeeId, PasswordHash, IsTemporaryPassword)
VALUES (SCOPE_IDENTITY(), N'__SET_BY_BACKEND_SEED__', 1);

-- Kỳ kế toán năm 2026
;WITH m AS (SELECT 1 AS n UNION ALL SELECT n + 1 FROM m WHERE n < 12)
INSERT dbo.AccountingPeriod (PeriodMonth, StartDate, EndDate)
SELECT CONCAT('2026-', RIGHT(CONCAT('0', n), 2)),
       DATEFROMPARTS(2026, n, 1),
       EOMONTH(DATEFROMPARTS(2026, n, 1))
FROM m;
GO

/* =====================================================================================
   17b. TÀI KHOẢN MẪU (SeedAuth, 21/10/2026)
   ---------------------------------------------------------------------------------
   Tổ may mẫu MAY1 + mỗi vai trò một nhân viên / tài khoản để test đăng nhập và phân quyền.
   Tên đăng nhập = EmployeeCode. PasswordHash là placeholder, backend ghi đè bằng hash thật khi seed
   (giống tài khoản ADMIN). Chạy lại nhiều lần không lỗi (idempotent).
   ===================================================================================== */
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.Team WHERE TeamCode = 'MAY1')
    INSERT dbo.Team (TeamCode, TeamName, TeamType) VALUES ('MAY1', N'Tổ may 1', 'SEWING');

DECLARE @vp   INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'VP');
DECLARE @cat  INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'CAT');
DECLARE @may1 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY1');

DECLARE @seed TABLE (Code VARCHAR(20), FullName NVARCHAR(100), RoleCode VARCHAR(30), TeamId INT, PayType VARCHAR(20));
INSERT @seed (Code, FullName, RoleCode, TeamId, PayType) VALUES
 ('QUANDOC',  N'Quản đốc mẫu',        'MANAGER',          @vp,   'MONTHLY'),
 ('KETOAN',   N'Kế toán mẫu',         'ACCOUNTANT',       @vp,   'MONTHLY'),
 ('THUKHO',   N'Thủ kho mẫu',         'WAREHOUSE_KEEPER', @vp,   'MONTHLY'),
 ('TTCAT',    N'Tổ trưởng tổ cắt',    'TEAM_LEADER',      @cat,  'MONTHLY'),
 ('TTMAY',    N'Tổ trưởng tổ may một',  'TEAM_LEADER',      @may1, 'MONTHLY'),
 ('TPMAY',    N'Tổ phó tổ may một',     'TEAM_DEPUTY',      @may1, 'MONTHLY'),
 ('QAQC1',    N'Nhân viên kiểm hàng mẫu',           'QAQC',             @vp,   'DAILY'),
 ('CODIEN',   N'Cơ điện mẫu',         'ELECTRICIAN',      @vp,   'MONTHLY'),
 ('CNMAY1',   N'Công nhân may một',     'WORKER',           @may1, 'PIECE_RATE'),
 ('CNCAT1',   N'Công nhân cắt một',     'WORKER',           @cat,  'DAILY');

INSERT dbo.Employee (EmployeeCode, FullName, RoleId, TeamId, PayType, StartDate)
SELECT s.Code, s.FullName, r.RoleId, s.TeamId, s.PayType, '2026-01-01'
FROM @seed s JOIN dbo.Role r ON r.RoleCode = s.RoleCode
WHERE NOT EXISTS (SELECT 1 FROM dbo.Employee e WHERE e.EmployeeCode = s.Code);

INSERT dbo.Account (EmployeeId, PasswordHash, IsTemporaryPassword)
SELECT e.EmployeeId, N'__SET_BY_BACKEND_SEED__', 1
FROM dbo.Employee e
WHERE e.EmployeeCode IN (SELECT Code FROM @seed)
  AND NOT EXISTS (SELECT 1 FROM dbo.Account a WHERE a.EmployeeId = e.EmployeeId);

-- Kiểm tra nhanh danh sách tài khoản (bỏ comment khi cần):
-- SELECT e.EmployeeCode AS Username, r.RoleCode, t.TeamCode
-- FROM dbo.Account a JOIN dbo.Employee e ON e.EmployeeId = a.EmployeeId
-- JOIN dbo.Role r ON r.RoleId = e.RoleId LEFT JOIN dbo.Team t ON t.TeamId = e.TeamId
-- ORDER BY a.AccountId;
GO

/* =====================================================================================
   18. MÔ TẢ BẢNG (MS_Description) – hiện trong SSMS / công cụ vẽ sơ đồ
   ===================================================================================== */
DECLARE @d TABLE (t SYSNAME, v NVARCHAR(400));
INSERT @d VALUES
 ('Role', N'Vai trò (9 vai trò, gồm Tổ trưởng và Tổ phó)'), ('Employee', N'Nhân viên'), ('Account', N'Tài khoản đăng nhập (1–1 nhân viên)'),
 ('Team', N'Tổ (cắt / may / văn phòng)'), ('GradeLevel', N'Bậc thợ A–D và tiền bậc theo ngày hiệu lực'),
 ('GradeEvaluation', N'Đề xuất / đổi bậc thợ; dòng APPLIED là lịch sử bậc'),
 ('Announcement', N'Thông báo nhanh'), ('AnnouncementTarget', N'Phạm vi nhận thông báo nhanh'),
 ('Notification', N'Thông báo người dùng (giữ 90 ngày)'),
 ('Customer', N'Khách hàng (công ty mẹ)'), ('CustomerOrder', N'Đơn hàng gia công'), ('OrderLine', N'Dòng đơn mã hàng–màu–size'),
 ('Product', N'Mã hàng'), ('OperationGroup', N'Công đoạn to (nhóm công đoạn)'), ('Operation', N'Công đoạn'), ('OperationComponent', N'Công đoạn làm trên chi tiết vải nào'), ('PieceRate', N'Đơn giá công đoạn'),
 ('OrderMaterial', N'NVL của đơn: cần bao nhiêu, khách cấp bao nhiêu, phải mua bao nhiêu'), ('ProductColor', N'Màu áo của mã hàng'), ('ProductVariant', N'Biến thể mã hàng: màu × size'),('ProductComponent', N'Thành phần của mã hàng: chi tiết vải (PANEL) và phụ liệu (TRIM)'),
 ('ProductComponentMaterial', N'Định mức NVL theo màu áo × thành phần: dùng NVL nào (đã gồm màu NVL)'), ('KpiTarget', N'KPI tổ may'), ('KpiDailyTarget', N'KPI tổ may theo từng ngày'), ('KpiTargetLine', N'KPI tổ may theo dòng đơn (màu × size)'),
 ('Request', N'Đơn từ (10 loại, gồm yêu cầu NVL / BTP)'), ('RequestLine', N'Dòng chi tiết đơn từ'),
 ('Material', N'Nguyên vật liệu'), ('Supplier', N'Nhà cung cấp'), ('PurchaseOrder', N'Đơn mua NVL kèm thanh toán (OrderId NULL = đơn mua dùng chung)'),
 ('PurchaseOrderLine', N'Dòng đơn mua NVL'),
 ('MaterialReceipt', N'Phiếu nhập NVL (mới / bổ sung)'), ('MaterialReceiptLine', N'Dòng phiếu nhập NVL'),
 ('WorkOrder', N'Lệnh cắt'), ('WorkOrderLine', N'Dòng lệnh cắt'), ('CuttingLay', N'Bàn cắt'),
 ('CuttingLayLine', N'Số cắt theo size của bàn cắt'), ('CutHandover', N'Bàn giao BTP vào kho'), ('CutHandoverLine', N'Dòng bàn giao BTP'),
 ('MaterialIssue', N'Phiếu cấp kho: NVL cho tổ cắt/may hoặc BTP cho tổ may'), ('MaterialIssueLine', N'Dòng phiếu cấp kho'),
 ('MaterialReturn', N'Phiếu trả vải thừa'), ('MaterialReturnLine', N'Dòng phiếu trả vải thừa'),
 ('OperationAssignment', N'Phân công công đoạn'), ('WorkAllocation', N'Giao / thu hồi việc với công nhân'),
 ('OutputRecord', N'Sản lượng công đoạn'), ('DefectRecord', N'Hàng lỗi Rework/Scrap và người gây lỗi'),
 ('QcBatch', N'Đợt kiểm hoàn thiện'), ('FgHandover', N'Bàn giao thành phẩm'), ('FgHandoverLine', N'Dòng bàn giao thành phẩm'),
 ('DispatchNote', N'Phiếu xuất kho thành phẩm (kiêm lệnh giao hàng)'), ('DispatchNoteLine', N'Dòng phiếu xuất kho'),
 ('Stocktake', N'Phiếu kiểm kê'), ('StocktakeLine', N'Dòng kiểm kê'), ('StockBalance', N'Tồn hiện tại theo mặt hàng và kho'), ('StockTransaction', N'Thẻ kho (ghi qua usp_Stock_Post)'),
 ('AttendanceSyncSession', N'Lần đồng bộ RJ / import Excel chấm công'), ('AttendanceLog', N'Lượt quẹt vân tay (3 trường lấy từ RJ)'),
 ('AttendanceAdjustment', N'Chấm hộ của Kế toán'), ('DailyTimesheet', N'Bảng công ngày'),
 ('DailyMeal', N'Suất ăn ngày (gồm suất khách)'), ('TeamMealConfirmation', N'Xác nhận suất ăn của tổ'),
 ('AccountingPeriod', N'Kỳ kế toán (tháng), khóa / mở khóa'), ('Payroll', N'Bảng lương tháng (import)'),
 ('Payslip', N'Phiếu lương'), ('PayslipItem', N'Khoản trên phiếu lương'),
 ('MonthlyCost', N'Chi phí tháng theo khoản mục'), ('BreakEvenResult', N'Kết quả hòa vốn theo mã hàng'),
 ('Vehicle', N'Xe'), ('VehicleTrip', N'Chuyến xe & nhiên liệu'),
 ('SystemSetting', N'Tham số hệ thống'), ('Attachment', N'Tệp / ảnh đính kèm'), ('AuditLog', N'Nhật ký thay đổi');

DECLARE @t SYSNAME, @v NVARCHAR(400);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT t, v FROM @d;
OPEN c; FETCH NEXT FROM c INTO @t, @v;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = @v,
         @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = @t;
    FETCH NEXT FROM c INTO @t, @v;
END;
CLOSE c; DEALLOCATE c;
GO

/* =====================================================================================
   19. DỮ LIỆU DEMO (xóa cả mục này nếu muốn database sạch)
   ---------------------------------------------------------------------------------
   Cần mục 17b (tài khoản mẫu) ở trên. Chạy lại riêng mục này không nhân đôi dữ liệu.
   ===================================================================================== */
-- 19.1 Dữ liệu demo các luồng của Minh (bỏ qua nếu đã có mã hàng DEMO-AO01)
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.Product WHERE ProductCode = 'DEMO-AO01')
BEGIN
    PRINT N'Dữ liệu demo đã có (DEMO-AO01), bỏ qua.';
    RETURN;
END;

BEGIN TRANSACTION;

DECLARE @vp INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'VP'),
        @cat INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'CAT'),
        @may INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY1');
DECLARE @admin INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'ADMIN'),
        @quandoc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC'),
        @ketoan INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'KETOAN'),
        @thukho INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'THUKHO'),
        @ttmay INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTMAY'),
        @qaqc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QAQC1');

/* ---------- 1. Công nhân may thêm + bậc thợ ---------- */
UPDATE dbo.Employee SET CurrentGradeCode = 'B' WHERE EmployeeCode = 'CNMAY1' AND CurrentGradeCode IS NULL;

DECLARE @wr INT = (SELECT RoleId FROM dbo.Role WHERE RoleCode = 'WORKER');
INSERT dbo.Employee (EmployeeCode, FullName, RoleId, TeamId, PayType, CurrentGradeCode, AttendanceCode, StartDate)
SELECT v.Code, v.FullName, @wr, @may, 'PIECE_RATE', v.Grade, v.AttCode, '2026-01-01'
FROM (VALUES ('CNMAY2', N'Nguyễn Thị Lan', 'C', 104), ('CNMAY3', N'Trần Văn Hùng', 'A', 105),
             ('CNMAY4', N'Lê Thị Mai', 'B', 106)) v(Code, FullName, Grade, AttCode)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Employee e WHERE e.EmployeeCode = v.Code);

INSERT dbo.Account (EmployeeId, PasswordHash, IsTemporaryPassword)
SELECT e.EmployeeId, k.PasswordHash, k.IsTemporaryPassword
FROM dbo.Employee e
CROSS JOIN (SELECT a.PasswordHash, a.IsTemporaryPassword FROM dbo.Account a
            JOIN dbo.Employee x ON x.EmployeeId = a.EmployeeId WHERE x.EmployeeCode = 'KETOAN') k
WHERE e.EmployeeCode IN ('CNMAY2', 'CNMAY3', 'CNMAY4')
  AND NOT EXISTS (SELECT 1 FROM dbo.Account a WHERE a.EmployeeId = e.EmployeeId);

DECLARE @w1 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY1'),
        @w2 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY2'),
        @w3 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY3'),
        @w4 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY4');

/* ---------- 2. Mã hàng, NVL, nhà cung cấp, đơn hàng ---------- */
INSERT dbo.Product (ProductCode, ProductName, Description) VALUES
 ('DEMO-AO01', N'Áo khoác gió DEMO', N'Mã hàng demo có 5 công đoạn'),
 ('DEMO-QU02', N'Quần short DEMO', N'Mã hàng demo có 2 công đoạn');
DECLARE @p1 INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-AO01'),
        @p2 INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-QU02');

-- Màu NVL nằm trong Material.Color: vải gió có 4 dòng (Đen, Xanh, Trắng, Be), chỉ may có 3 (Đen, Xanh, Be)
INSERT dbo.Material (MaterialCode, MaterialName, Color, Category, Unit) VALUES
 ('DEMO-VAI01',  N'Vải gió 2 lớp DEMO', N'Đen',   'MAIN_FABRIC', N'm'),
 ('DEMO-CHI01',  N'Chỉ may 40/2 DEMO',  N'Đen',   'THREAD',      N'cuộn'),
 ('DEMO-KHOA01', N'Khóa kéo 60cm DEMO', N'',      'ZIPPER',      N'cái'),
 ('DEMO-NHAN01', N'Nhãn dệt DEMO',      N'',      'LABEL',       N'cái'),
 ('DEMO-KIM01',  N'Kim máy 14 DEMO',    N'',      'OTHER',       N'hộp'),
 ('DEMO-VAI02',  N'Vải gió 2 lớp DEMO', N'Xanh',  'MAIN_FABRIC', N'm'),
 ('DEMO-VAI03',  N'Vải gió 2 lớp DEMO', N'Trắng', 'MAIN_FABRIC', N'm'),
 ('DEMO-VAI04',  N'Vải gió 2 lớp DEMO', N'Be',    'MAIN_FABRIC', N'm'),
 ('DEMO-CHI02',  N'Chỉ may 40/2 DEMO',  N'Xanh',  'THREAD',      N'cuộn'),
 ('DEMO-CHI03',  N'Chỉ may 40/2 DEMO',  N'Be',    'THREAD',      N'cuộn');
DECLARE @mVai INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI01'),
        @mChi INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-CHI01'),
        @mKhoa INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-KHOA01'),
        @mNhan INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-NHAN01'),
        @mKim INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-KIM01'),
        @mVaiXanh INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI02'),
        @mVaiTrang INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI03'),
        @mVaiBe INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI04'),
        @mChiXanh INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-CHI02'),
        @mChiBe INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-CHI03');

-- Biến thể, thành phần và định mức (23/10). Phải có trước OrderLine (FK_OrderLine_Variant).
-- Xanh – S chưa có trong đơn nào: biến thể khai báo trước, đơn sau mới dùng.
INSERT dbo.ProductColor (ProductId, Color) VALUES (@p1, N'Đen'), (@p1, N'Xanh'), (@p2, N'Be');
INSERT dbo.ProductVariant (ProductId, ProductColorId, SizeCode)
SELECT c.ProductId, c.ProductColorId, v.SizeCode
FROM (VALUES (@p1, N'Đen', 'S'), (@p1, N'Đen', 'M'), (@p1, N'Đen', 'L'),
             (@p1, N'Xanh', 'S'), (@p1, N'Xanh', 'M'), (@p1, N'Xanh', 'L'),
             (@p2, N'Be', 'M'), (@p2, N'Be', 'L')) v (ProductId, Color, SizeCode)
JOIN dbo.ProductColor c ON c.ProductId = v.ProductId AND c.Color = v.Color;

INSERT dbo.ProductComponent (ProductId, ComponentName, ComponentType, PiecesPerGarment) VALUES
 (@p1, N'Thân trước', 'PANEL', 2), (@p1, N'Thân sau', 'PANEL', 1), (@p1, N'Tay áo', 'PANEL', 2),
 (@p1, N'Khóa kéo',   'TRIM',  1), (@p1, N'Nhãn',     'TRIM',  1), (@p1, N'Chỉ may', 'TRIM',  1),
 (@p2, N'Ống quần',   'PANEL', 2), (@p2, N'Chỉ may',  'TRIM',  1);

-- Vải theo chi tiết: theo màu áo (mọi size dùng chung), riêng màu Xanh phối tay áo vải Trắng (09/11 bỏ kích thước chi tiết, DEV-ALL-85;
-- theo màu, không theo size, DEV-ALL-87)
INSERT dbo.ProductComponentMaterial (ProductId, ProductColorId, ProductComponentId, MaterialId)
SELECT pcol.ProductId, pcol.ProductColorId, pc.ProductComponentId,
       CASE WHEN pcol.Color = N'Xanh' AND pc.ComponentName = N'Tay áo' THEN @mVaiTrang
            WHEN pcol.Color = N'Xanh' THEN @mVaiXanh ELSE @mVai END
FROM (VALUES (N'Thân trước'), (N'Thân sau'), (N'Tay áo')) s (ComponentName)
JOIN dbo.ProductComponent pc ON pc.ProductId = @p1 AND pc.ComponentName = s.ComponentName
JOIN dbo.ProductColor pcol   ON pcol.ProductId = @p1;

-- Phụ liệu áo: chỉ theo màu áo (Đen / Xanh), khóa và nhãn không phân màu
INSERT dbo.ProductComponentMaterial (ProductId, ProductColorId, ProductComponentId, MaterialId)
SELECT pcol.ProductId, pcol.ProductColorId, pc.ProductComponentId,
       CASE WHEN t.ByColor = 1 AND pcol.Color = N'Xanh' THEN @mChiXanh ELSE t.MaterialId END
FROM (VALUES (N'Khóa kéo', @mKhoa, 0), (N'Nhãn', @mNhan, 0), (N'Chỉ may', @mChi, 1))
     t (ComponentName, MaterialId, ByColor)
JOIN dbo.ProductComponent pc ON pc.ProductId = @p1 AND pc.ComponentName = t.ComponentName
JOIN dbo.ProductColor pcol   ON pcol.ProductId = @p1;

-- Quần short
INSERT dbo.ProductComponentMaterial (ProductId, ProductColorId, ProductComponentId, MaterialId)
SELECT pcol.ProductId, pcol.ProductColorId, pc.ProductComponentId, t.MaterialId
FROM (VALUES (N'Ống quần', @mVaiBe), (N'Chỉ may', @mChiBe)) t (ComponentName, MaterialId)
JOIN dbo.ProductComponent pc ON pc.ProductId = @p2 AND pc.ComponentName = t.ComponentName
JOIN dbo.ProductColor pcol   ON pcol.ProductId = @p2;

-- Nhóm bìa của mã hàng (DEV-ALL-85): áo nhóm 1 (thân) và nhóm 2 (tay), áo Xanh phối tay vải Trắng; quần một nhóm.
-- Bìa của từng đơn (ProductMarker, ProductMarkerRatio) thêm ở cuối phần dữ liệu demo, sau khi có đơn hàng.
INSERT dbo.ProductMarkerFabric (ProductId, MarkerNo, ProductColorId, MaterialId)
SELECT c.ProductId, f.MarkerNo, c.ProductColorId, f.MaterialId
FROM (VALUES (@p1, 1, N'Đen', @mVai), (@p1, 2, N'Đen', @mVai),
             (@p1, 1, N'Xanh', @mVaiXanh), (@p1, 2, N'Xanh', @mVaiTrang),
             (@p2, 1, N'Be', @mVaiBe)) f (ProductId, MarkerNo, Color, MaterialId)
JOIN dbo.ProductColor c ON c.ProductId = f.ProductId AND c.Color = f.Color;

INSERT dbo.Supplier (SupplierName, Phone, Email, Address, MaterialTypes, Note, Status) VALUES
 (N'Công ty Vải Sợi Thành Công (DEMO)', '0901000001', 'vai@thanhcong.demo', N'Quận Tân Phú, TP.HCM', N'Vải chính, vải lót', NULL, 'ACTIVE'),
 (N'Phụ liệu may Hoàng Gia (DEMO)',     '0901000002', 'pl@hoanggia.demo',  N'Quận 10, TP.HCM',      N'Chỉ, khóa, nhãn',  N'Giao hàng trong 2 ngày', 'ACTIVE'),
 (N'Cửa hàng Kim máy Minh Phát (DEMO)', '0901000003', NULL,                N'Quận 5, TP.HCM',       N'Kim, phụ tùng',    NULL, 'ACTIVE'),
 (N'Vải Phương Nam (DEMO - ngừng)',     '0901000004', NULL,                N'Bình Dương',           N'Vải',              N'Đã ngừng hợp tác', 'DISCONTINUED');
DECLARE @sup1 INT = (SELECT SupplierId FROM dbo.Supplier WHERE SupplierName LIKE N'Công ty Vải Sợi Thành Công%'),
        @sup2 INT = (SELECT SupplierId FROM dbo.Supplier WHERE SupplierName LIKE N'Phụ liệu may Hoàng Gia%');

DECLARE @cus INT = (SELECT TOP 1 CustomerId FROM dbo.Customer ORDER BY CustomerId);
INSERT dbo.CustomerOrder (OrderCode, CustomerId, ReceivedDate, DueDate, MaterialStatus, Status, Note) VALUES
 ('DEMO-DH001', @cus, '2026-09-20', '2026-11-10', 'PARTIAL', 'IN_PRODUCTION', N'Đơn demo đang sản xuất'),
 ('DEMO-DH002', @cus, '2026-10-05', '2026-12-01', 'AWAITING', 'NEW',           N'Đơn demo mới nhận');
DECLARE @o1 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH001'),
        @o2 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH002');

INSERT dbo.OrderLine (OrderId, ProductVariantId, ProductId, OrderedQty, CmtUnitPrice)
SELECT v.OrderId, pv.ProductVariantId, pv.ProductId, v.Qty, v.Price
FROM (VALUES (1, @o1, @p1, N'Đen',  'S', 300, 28000), (2, @o1, @p1, N'Đen',  'M', 600, 28000), (3, @o1, @p1, N'Đen', 'L', 500, 28000),
             (4, @o1, @p1, N'Xanh', 'M', 300, 28000), (5, @o1, @p1, N'Xanh', 'L', 200, 28000),
             (6, @o2, @p2, N'Be',   'M', 400, 18000), (7, @o2, @p2, N'Be',   'L', 400, 18000)) v(Seq, OrderId, ProductId, Color, SizeCode, Qty, Price)
JOIN dbo.ProductColor pcol ON pcol.ProductId = v.ProductId AND pcol.Color = v.Color
JOIN dbo.ProductVariant pv ON pv.ProductColorId = pcol.ProductColorId AND pv.SizeCode = v.SizeCode
ORDER BY v.Seq;
DECLARE @lS INT = (SELECT ol.OrderLineId FROM dbo.OrderLine ol JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
                   JOIN dbo.ProductColor pcol ON pcol.ProductColorId = pv.ProductColorId
                   WHERE ol.OrderId = @o1 AND pcol.Color = N'Đen' AND pv.SizeCode = 'S'),
        @lM INT = (SELECT ol.OrderLineId FROM dbo.OrderLine ol JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
                   JOIN dbo.ProductColor pcol ON pcol.ProductColorId = pv.ProductColorId
                   WHERE ol.OrderId = @o1 AND pcol.Color = N'Đen' AND pv.SizeCode = 'M'),
        @lL INT = (SELECT ol.OrderLineId FROM dbo.OrderLine ol JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
                   JOIN dbo.ProductColor pcol ON pcol.ProductColorId = pv.ProductColorId
                   WHERE ol.OrderId = @o1 AND pcol.Color = N'Đen' AND pv.SizeCode = 'L');

INSERT dbo.OrderMaterial (OrderId, MaterialId, RequiredQty, CustomerQty, ExpectedDeliveryCount) VALUES
 (@o1, @mVai,  2000, 0, NULL), (@o1, @mChi, 200, 0, NULL),
 (@o1, @mKhoa, 1900, 400, 2), (@o1, @mNhan, 1900, 1900, 1),
 (@o1, @mVaiXanh, 520, 0, NULL), (@o1, @mVaiTrang, 210, 0, NULL), (@o1, @mChiXanh, 55, 0, NULL), -- áo Xanh: thân vải Xanh, tay vải Trắng
 (@o2, @mVaiBe, 800, 0, NULL), (@o2, @mChiBe, 100, 0, NULL);
DECLARE @om1Vai INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o1 AND MaterialId = @mVai),
        @om1Chi INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o1 AND MaterialId = @mChi),
        @om1Khoa INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o1 AND MaterialId = @mKhoa),
        @om2Vai INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o2 AND MaterialId = @mVaiBe),
        @om2Chi INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o2 AND MaterialId = @mChiBe);

/* ---------- 3. MI-07: đơn mua ở đủ các trạng thái ---------- */
INSERT dbo.PurchaseOrder (PoCode, OrderId, SupplierId, OrderDate, GoodsStatus, PaymentStatus, RejectReason, PaymentReturnReason,
                          PaymentDate, PaidAmount, PaymentMethod, PaymentNote, Note) VALUES
 ('DM-DEMO-001', @o1, NULL,  NULL,         'PENDING_FINALISATION', 'UNPAID',   NULL, NULL, NULL, NULL, NULL, NULL, N'Chờ Quản đốc chốt nhà cung cấp và đơn giá'),
 ('DM-DEMO-002', @o1, NULL,  NULL,         'PENDING_FINALISATION', 'UNPAID',   NULL, NULL, NULL, NULL, NULL, NULL, N'Mua thêm khóa kéo'),
 ('DM-DEMO-003', @o2, @sup1, '2026-10-19', 'ORDERED',              'UNPAID',   NULL, NULL, NULL, NULL, NULL, NULL, N'Chờ Kế toán xác nhận thanh toán'),
 ('DM-DEMO-004', @o1, @sup2, '2026-10-16', 'ORDERED',              'RETURNED', NULL, N'Đơn giá khóa kéo cao hơn báo giá, đề nghị kiểm tra lại', NULL, NULL, NULL, NULL, NULL),
 ('DM-DEMO-005', @o1, @sup1, '2026-10-05', 'PARTIALLY_RECEIVED',  'PAID',     NULL, NULL, '2026-10-12', 44000000, 'BANK_TRANSFER', N'Chuyển khoản đợt 1', NULL),
 ('DM-DEMO-006', @o1, NULL,  NULL,         'REJECTED',             'UNPAID',   N'Khách đổi màu vải, hủy đơn mua cũ', NULL, NULL, NULL, NULL, NULL, NULL);
-- Đơn mua tồn kho dùng chung (TH-12): không gắn đơn hàng
INSERT dbo.PurchaseOrder (PoCode, OrderId, GoodsStatus, PaymentStatus, Note)
VALUES ('DM-DEMO-007', NULL, 'PENDING_FINALISATION', 'UNPAID', N'Kim máy dùng chung cho nhiều đơn');

DECLARE @po1 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'DM-DEMO-001'),
        @po2 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'DM-DEMO-002'),
        @po3 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'DM-DEMO-003'),
        @po4 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'DM-DEMO-004'),
        @po5 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'DM-DEMO-005'),
        @po6 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'DM-DEMO-006'),
        @po7 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'DM-DEMO-007');
UPDATE dbo.PurchaseOrder SET ExpectedDeliveryCount = CASE PurchaseOrderId WHEN @po5 THEN 2 ELSE 1 END
WHERE PurchaseOrderId IN (@po3, @po4, @po5);   -- DM-DEMO-005 NCC giao 2 đợt

INSERT dbo.PurchaseOrderLine (PurchaseOrderId, OrderMaterialId, OrderedQty, UnitPrice) VALUES
 (@po1, @om1Vai, 1000, 0), (@po1, @om1Chi, 200, 0),
 (@po2, @om1Khoa, 1500, 0),
 (@po3, @om2Vai, 800, 45000), (@po3, @om2Chi, 100, 12000),
 (@po4, @om1Khoa, 1500, 3500),
 (@po5, @om1Vai, 1000, 44000),
 (@po6, @om1Vai, 500, 0);
INSERT dbo.PurchaseOrderLine (PurchaseOrderId, OrderMaterialId, MaterialId, OrderedQty, UnitPrice)
VALUES (@po7, NULL, @mKim, 20, 0);

-- Phiếu nhập đã xác nhận 600/1000 của DM-DEMO-005 (còn phải nhận 400)
INSERT dbo.MaterialReceipt (ReceiptCode, OrderId, PurchaseOrderId, ReceiptDate, Status, ConfirmedBy, ConfirmedAt)
VALUES ('PN-DEMO-001', @o1, @po5, '2026-10-08', 'CONFIRMED', @thukho, '2026-10-08 15:00:00');
INSERT dbo.MaterialReceiptLine (MaterialReceiptId, OrderMaterialId, PurchaseOrderLineId, AcceptedQty)
SELECT (SELECT MaterialReceiptId FROM dbo.MaterialReceipt WHERE ReceiptCode = 'PN-DEMO-001'), @om1Vai,
       (SELECT PurchaseOrderLineId FROM dbo.PurchaseOrderLine WHERE PurchaseOrderId = @po5), 600;

/* ---------- 4. Công đoạn + đơn giá (KI-01) ---------- */
INSERT dbo.Operation (ProductId, OperationCode, OperationName, StationSequence, SkillLevel, StandardTimeSec, TeamType, IsFinalStep) VALUES
 (@p1, 'OP01', N'Ráp thân',      1, 2, 95,  'SEWING', 0), (@p1, 'OP02', N'May tay',        2, 2, 120, 'SEWING', 0),
 (@p1, 'OP03', N'Tra cổ',        3, 3, 80,  'SEWING', 0), (@p1, 'OP04', N'Vắt sổ',         4, 1, 60,  'SEWING', 0),
 (@p1, 'OP05', N'Hoàn thiện',    5, 2, 150, 'SEWING', 1),
 (@p2, 'OP01', N'May ống quần',  1, 2, 100, 'SEWING', 0), (@p2, 'OP02', N'Hoàn thiện quần', 2, 2, 130, 'SEWING', 1);
DECLARE @a1 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP01'),
        @a2 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP02'),
        @a3 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP03'),
        @a4 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP04'),
        @a5 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP05'),
        @b1 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p2 AND OperationCode = 'OP01'),
        @b2 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p2 AND OperationCode = 'OP02');

INSERT dbo.PieceRate (OperationId, Rate, EffectiveDate, Status, SubmittedAt, ApprovedAt, DecisionNote) VALUES
 (@a1, 1200, '2026-09-01', 'APPROVED', '2026-08-30 10:00:00', '2026-08-31 09:00:00', N'Duyệt theo bảng giá quý 3'),
 (@a2, 1500, '2026-09-01', 'APPROVED', '2026-08-30 10:00:00', '2026-08-31 09:00:00', NULL),
 (@a3, 1000, '2026-09-01', 'APPROVED', '2026-08-30 10:00:00', '2026-08-31 09:00:00', NULL),
 (@a4,  800, '2026-09-01', 'APPROVED', '2026-08-30 10:00:00', '2026-08-31 09:00:00', NULL),
 (@a5, 2000, '2026-09-01', 'APPROVED', '2026-08-30 10:00:00', '2026-08-31 09:00:00', NULL),
 (@a3, 1100, '2026-10-25', 'PENDING_APPROVAL', '2026-10-20 15:00:00', NULL, NULL),
 (@a4,  900, '2026-11-16', 'DRAFT', NULL, NULL, NULL),
 -- Giá mới đã duyệt, hiệu lực sau hôm nay (bảng công đoạn hiện "→ giá mới từ ngày"); giá mới đang chờ duyệt
 (@a1, 1300, '2026-11-16', 'APPROVED', '2026-10-29 10:00:00', '2026-10-30 09:00:00', N'Tăng giá từ nửa cuối tháng 11'),
 (@a5, 2100, '2026-11-09', 'PENDING_APPROVAL', '2026-10-31 15:00:00', NULL, NULL),
 (@a2, 1700, '2026-10-22', 'REJECTED', '2026-10-18 11:00:00', NULL, N'Mức tăng 13% vượt khung, đề nghị trình lại'),
 (@b1, 1300, '2026-10-01', 'APPROVED', '2026-09-28 10:00:00', '2026-09-29 09:00:00', NULL),
 (@b2, 1800, '2026-10-01', 'PENDING_APPROVAL', '2026-10-20 16:00:00', NULL, NULL);

/* ---------- 5. Phân công công đoạn (KI-03) ---------- */
INSERT dbo.OperationAssignment (SewingTeamId, OrderId, OperationId, WorkerId, ValidFrom, ValidTo) VALUES
 (@may, @o1, @a1, @w1, '2026-10-01', NULL), (@may, @o1, @a2, @w1, '2026-10-01', NULL),
 (@may, @o1, @a3, @w2, '2026-10-01', NULL), (@may, @o1, @a4, @w3, '2026-10-01', NULL),
 (@may, @o1, @a5, @w4, '2026-10-01', NULL),
 (@may, @o1, @a3, @w1, '2026-09-21', '2026-09-30');   -- lịch sử: CNMAY1 từng làm Tra cổ

/* ---------- 6. BTP tổ may đã nhận từ kho (cấp trần sản lượng) ---------- */
INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, ReceivedBy, ReceivedAt)
VALUES ('PC-DEMO-001', 'SEMI_FINISHED', @o1, @may, '2026-09-23', @thukho, 'RECEIVED', @ttmay, '2026-09-23 10:00:00');
INSERT dbo.MaterialIssueLine (MaterialIssueId, OrderLineId, IssuedQty, ConfirmedQty)
SELECT (SELECT MaterialIssueId FROM dbo.MaterialIssue WHERE IssueCode = 'PC-DEMO-001'), ol.OrderLineId, ol.OrderedQty, ol.OrderedQty
FROM dbo.OrderLine ol WHERE ol.OrderId = @o1;

/* ---------- 6b. Giao việc cho công nhân (25/10) ---------- */
-- Số lần giao mỗi công nhân là WorkAllocation.Quantity: nhận BTP sáng 23/10 thì chiều giao hết số bộ của Đen M (600), Đen L (500)
INSERT dbo.WorkAllocation (MovementType, SewingTeamId, WorkerId, OrderLineId, OperationId, Quantity,
                           ConfirmedQty, ConfirmMode, Status, CreatedAt, ConfirmedAt)
SELECT 'HANDOVER', @may, a.WorkerId, ol.OrderLineId, a.OperationId, ol.OrderedQty,
       ol.OrderedQty, 'WORKER', 'CONFIRMED', '2026-09-23 13:00:00', '2026-09-23 13:15:00'
FROM dbo.OrderLine ol
CROSS JOIN (VALUES (@w1, @a1), (@w1, @a2), (@w2, @a3), (@w3, @a4), (@w4, @a5)) a(WorkerId, OperationId)
WHERE ol.OrderLineId IN (@lM, @lL);

/* ---------- 7. Chấm công (bảng công ngày từ 01/09 đến 31/10) ---------- */
CREATE TABLE #Days (D DATE PRIMARY KEY);
;WITH d AS (SELECT CAST('2026-09-01' AS DATE) AS D UNION ALL SELECT DATEADD(DAY, 1, D) FROM d WHERE D < '2026-10-31')
INSERT #Days SELECT D FROM d
WHERE DATEDIFF(DAY, 0, D) % 7 <> 6 OR D IN ('2026-10-11', '2026-10-18');   -- Chủ nhật chỉ giữ 11/10 và 18/10 (sản xuất ngày Chủ nhật)

INSERT dbo.DailyTimesheet (EmployeeId, WorkDate, CheckIn, CheckOut, WorkedHours, WorkdayValue, OvertimeHours, IsMissingPunch)
SELECT e.EmployeeId, d.D,
       DATEADD(MINUTE, 7 * 60 + 20 + ABS(CHECKSUM(e.EmployeeId, d.D)) % 10, CAST(d.D AS DATETIME2(0))),
       CASE WHEN e.EmployeeCode = 'CNMAY2' AND d.D = '2026-10-14' THEN NULL
            ELSE DATEADD(MINUTE, 16 * 60 + 30 + IIF(DATEDIFF(DAY, 0, d.D) % 3 = 0 AND e.PayType <> 'MONTHLY', 60, 0), CAST(d.D AS DATETIME2(0))) END,
       CASE WHEN e.EmployeeCode = 'CNMAY2' AND d.D = '2026-10-14' THEN 0 ELSE 8 END,
       CASE WHEN e.EmployeeCode = 'CNMAY2' AND d.D = '2026-10-14' THEN 0 ELSE 1 END,
       CASE WHEN e.EmployeeCode = 'CNMAY2' AND d.D = '2026-10-14' THEN 0
            WHEN DATEDIFF(DAY, 0, d.D) % 3 = 0 AND e.PayType <> 'MONTHLY' THEN 1 ELSE 0 END,
       IIF(e.EmployeeCode = 'CNMAY2' AND d.D = '2026-10-14', 1, 0)
FROM dbo.Employee e
CROSS JOIN #Days d
WHERE e.EmployeeCode IN ('CNMAY1', 'CNMAY2', 'CNMAY3', 'CNMAY4', 'CNCAT1', 'TTMAY', 'TPMAY', 'TTCAT', 'KETOAN',
                         'QUANDOC', 'THUKHO', 'QAQC1', 'CODIEN')
  AND (DATEDIFF(DAY, 0, d.D) % 7 <> 6 OR (d.D = '2026-10-11' AND e.EmployeeCode IN ('CNMAY1', 'CNMAY2', 'CNMAY3', 'CNMAY4')))
  AND NOT EXISTS (SELECT 1 FROM dbo.DailyTimesheet t WHERE t.EmployeeId = e.EmployeeId AND t.WorkDate = d.D);

-- Hai mã quẹt chưa gắn nhân viên (để thử màn "Mã chấm công chưa map")
DECLARE @sess INT = (SELECT MAX(SyncSessionId) FROM dbo.AttendanceSyncSession);
IF @sess IS NOT NULL
    INSERT dbo.AttendanceLog (AttendanceCode, PunchTime, EmployeeId, SyncSessionId)
    SELECT v.Code, v.T, NULL, @sess
    FROM (VALUES (999, CAST('2026-10-20 07:41:00' AS DATETIME2(0))), (999, CAST('2026-10-20 16:35:00' AS DATETIME2(0))),
                 (998, CAST('2026-10-20 07:52:00' AS DATETIME2(0)))) v(Code, T)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.AttendanceLog l WHERE l.AttendanceCode = v.Code AND l.PunchTime = v.T);

-- Đơn xin chấm công bù: một đơn đã duyệt (Kế toán chấm hộ CNMAY2 ngày 14/10), một đơn đang chờ duyệt
INSERT dbo.Request (RequestCode, RequestTypeCode, Status, TargetDate, Title, Content, CreatedBy, CreatedByTeamId, CreatedAt, DecidedAt, DecisionNote)
VALUES ('YC261015-901', 'ATTENDANCE_CORRECTION', 'APPROVED', '2026-10-14', N'Xin chấm công bù cho Nguyễn Thị Lan',
        N'Công nhân quên quẹt thẻ ra ngày 14/10, có làm đủ ca.', @ttmay, @may, '2026-10-15 07:50:00', '2026-10-15 09:00:00', N'Đồng ý'),
       ('YC261020-902', 'ATTENDANCE_CORRECTION', 'PENDING_APPROVAL', '2026-10-19', N'Xin chấm công bù cho Lê Thị Mai',
        N'Máy chấm công không nhận vân tay ngày 19/10.', @ttmay, @may, '2026-10-20 08:10:00', NULL, NULL);

/* ---------- 8. Suất ăn 01/09..31/10 (đã chốt) ---------- */
INSERT dbo.DailyMeal (MealDate, Status, OfficeCount, GuestCount, MealPrice, GuestNote)
SELECT d.D, 'OPEN', 5, IIF(DATEDIFF(DAY, 0, d.D) % 7 = 2, 3, 0), 30000, IIF(DATEDIFF(DAY, 0, d.D) % 7 = 2, N'Khách của công ty mẹ', NULL)
FROM #Days d
WHERE DATEDIFF(DAY, 0, d.D) % 7 <> 6
  AND NOT EXISTS (SELECT 1 FROM dbo.DailyMeal m WHERE m.MealDate = d.D);

INSERT dbo.TeamMealConfirmation (DailyMealId, TeamId, MealCount, AttendanceCount, Source, ConfirmedAt)
SELECT m.DailyMealId, t.TeamId, t.Cnt, t.Cnt, 'CONFIRMED', DATEADD(MINUTE, 8 * 60 + 5, CAST(m.MealDate AS DATETIME2(0)))
FROM dbo.DailyMeal m
CROSS JOIN (VALUES (@cat, 8), (@may, 10)) t(TeamId, Cnt)
WHERE m.Status = 'OPEN' AND m.MealDate <= '2026-10-31'
  AND NOT EXISTS (SELECT 1 FROM dbo.TeamMealConfirmation c WHERE c.DailyMealId = m.DailyMealId AND c.TeamId = t.TeamId);

UPDATE dbo.DailyMeal SET Status = 'CLOSED', ClosedAt = DATEADD(MINUTE, 8 * 60 + 40, CAST(MealDate AS DATETIME2(0)))
WHERE Status = 'OPEN' AND MealDate <= '2026-10-31';

/* ---------- 9. Sản lượng cuối ca 23/09..20/10 (KI-04) ---------- */
-- Mỗi ngày làm cả Đen M (20–22) và Đen L (16–18): tổng không vượt số được giao (600 / 500),
-- công đoạn cuối (@a5) luôn đủ cho số QC đạt tới ngày đó.
CREATE TABLE #Assign (WorkerId INT, OperationId INT);
INSERT #Assign VALUES (@w1, @a1), (@w1, @a2), (@w2, @a3), (@w3, @a4), (@w4, @a5);

INSERT dbo.OutputRecord (WorkDate, ShiftCode, SewingTeamId, WorkerId, OperationId, OrderLineId, Quantity, PieceRateId,
                         IsOutsideAssignment, NoAttendanceWarning, EditDeadline, Status)
SELECT d.D, 'AFTERNOON', @may, a.WorkerId, a.OperationId, l.OrderLineId,
       l.Base + DAY(d.D) % 3,
       pr.PieceRateId,
       0,
       IIF(EXISTS (SELECT 1 FROM dbo.DailyTimesheet t WHERE t.EmployeeId = a.WorkerId AND t.WorkDate = d.D AND t.CheckIn IS NOT NULL), 0, 1),
       dbo.fn_NextWorkingDayEnd(d.D), 'SAVED'
FROM #Days d
CROSS JOIN #Assign a
CROSS JOIN (VALUES (@lM, 20), (@lL, 16)) l(OrderLineId, Base)
CROSS APPLY (SELECT TOP (1) PieceRateId, Rate FROM dbo.PieceRate
             WHERE OperationId = a.OperationId AND Status = 'APPROVED' AND EffectiveDate <= d.D
             ORDER BY EffectiveDate DESC) pr
WHERE d.D BETWEEN '2026-09-23' AND '2026-10-20';

/* ---------- 10. QC (KI-06), bàn giao thành phẩm (doanh thu) ---------- */
-- QC đạt cộng dồn: Đen M 510, Đen L 350; bàn giao sau khi QC đủ số
INSERT dbo.QcBatch (WorkDate, ShiftCode, BatchTime, SewingTeamId, OrderLineId, FinishedQty, InspectorId, EditDeadline) VALUES
 ('2026-10-02', 'AFTERNOON', '15:30', @may, @lM, 120, @qaqc, '2026-10-02 16:30:00'),
 ('2026-10-03', 'AFTERNOON', '15:30', @may, @lL, 100, @qaqc, '2026-10-03 16:30:00'),
 ('2026-10-08', 'AFTERNOON', '15:00', @may, @lM, 150, @qaqc, '2026-10-08 16:30:00'),
 ('2026-10-10', 'MORNING',   '10:30', @may, @lL, 150, @qaqc, '2026-10-10 11:30:00'),
 ('2026-10-13', 'MORNING',   '10:45', @may, @lM,  60, @qaqc, '2026-10-13 11:30:00'),
 ('2026-10-15', 'AFTERNOON', '15:30', @may, @lM,  90, @qaqc, '2026-10-15 16:30:00'),
 ('2026-10-16', 'AFTERNOON', '15:45', @may, @lL, 100, @qaqc, '2026-10-16 16:30:00'),
 ('2026-10-19', 'AFTERNOON', '15:50', @may, @lM,  30, @qaqc, '2026-10-19 16:30:00'),
 ('2026-10-20', 'AFTERNOON', '15:40', @may, @lM,  60, @qaqc, '2026-10-20 16:30:00');

INSERT dbo.FgHandover (HandoverCode, OrderId, ProductId, InspectorId, HandedAt, Status, ConfirmedBy, ConfirmedAt) VALUES
 ('FG-DEMO-001', @o1, @p1, @qaqc, '2026-10-13 16:00:00', 'CONFIRMED', @thukho, '2026-10-13 16:20:00'),
 ('FG-DEMO-002', @o1, @p1, @qaqc, '2026-10-10 16:00:00', 'CONFIRMED', @thukho, '2026-10-10 16:20:00'),
 ('FG-DEMO-003', @o1, @p1, @qaqc, '2026-10-19 16:30:00', 'CONFIRMED', @thukho, '2026-10-19 16:45:00');
INSERT dbo.FgHandoverLine (FgHandoverId, OrderLineId, HandedQty, ConfirmedQty)
SELECT h.FgHandoverId, v.OrderLineId, v.Qty, v.Qty
FROM (VALUES ('FG-DEMO-001', @lM, 300), ('FG-DEMO-002', @lL, 250), ('FG-DEMO-003', @lM, 150), ('FG-DEMO-003', @lL, 100)) v(Code, OrderLineId, Qty)
JOIN dbo.FgHandover h ON h.HandoverCode = v.Code;

/* ---------- 11. Hàng hỏng (KI-08) + thông báo cho công nhân ---------- */
INSERT dbo.DefectRecord (DefectCode, WorkDate, ShiftCode, SewingTeamId, OrderLineId, OperationId, ResponsibleWorkerId, DefectType, Quantity,
                         DefectReasonCode, ReasonNote, PieceRateId, WorkerNotifiedAt, WorkerObjection, ObjectedAt, EditDeadline, Status, RecordedBy, RecordedAt, UpdatedAt) VALUES
 ('MAY1-261012-001', '2026-10-12', 'AFTERNOON', @may, @lL, @a1, @w1, 'REWORK', 2, 'SKIPPED_STITCH', NULL, NULL, '2026-10-12 16:10:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-12'), 'CANCELLED', @ttmay, '2026-10-12 16:10:00', '2026-10-13 09:00:00'),
 ('MAY1-261014-001', '2026-10-14', 'AFTERNOON', @may, @lL, @a2, @w1, 'REWORK', 3, 'SKIPPED_STITCH', NULL, NULL, '2026-10-14 16:05:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-14'), 'CONFIRMED', @ttmay, '2026-10-14 16:05:00', DATEADD(HOUR, 48, '2026-10-14 16:05:00')),
 ('MAY1-261015-001', '2026-10-15', 'AFTERNOON', @may, @lM, @a3, @w2, 'SCRAP',  2, 'STAINED',        NULL, (SELECT PieceRateId FROM dbo.PieceRate WHERE OperationId = @a3 AND EffectiveDate = '2026-09-01'), '2026-10-15 16:20:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-15'), 'CONFIRMED', @ttmay, '2026-10-15 16:20:00', DATEADD(HOUR, 48, '2026-10-15 16:20:00')),
 ('MAY1-261016-001', '2026-10-16', 'AFTERNOON', @may, @lL, @a4, @w3, 'SCRAP',  1, 'TORN',           NULL, (SELECT PieceRateId FROM dbo.PieceRate WHERE OperationId = @a4 AND EffectiveDate = '2026-09-01'), '2026-10-16 16:15:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-16'), 'CONFIRMED', @ttmay, '2026-10-16 16:15:00', DATEADD(HOUR, 48, '2026-10-16 16:15:00')),
 ('MAY1-261019-001', '2026-10-19', 'AFTERNOON', @may, @lM, @a5, @w4, 'REWORK', 4, 'CROOKED_SEAM',   NULL, NULL, '2026-10-19 16:10:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-19'), 'CONFIRMED', @ttmay, '2026-10-19 16:10:00', DATEADD(HOUR, 48, '2026-10-19 16:10:00')),
 ('MAY1-261020-001', '2026-10-20', 'AFTERNOON', @may, @lL, @a2, @w1, 'SCRAP',  1, 'OTHER',          N'Cháy vải do là quá nhiệt', (SELECT PieceRateId FROM dbo.PieceRate WHERE OperationId = @a2 AND EffectiveDate = '2026-09-01'), '2026-10-20 16:25:00',
  N'Hôm đó em không làm công đoạn này, đề nghị kiểm tra lại', '2026-10-20 18:00:00', dbo.fn_NextWorkingDayEnd('2026-10-20'), 'RECORDED', @ttmay, '2026-10-20 16:25:00', '2026-10-20 18:00:00'),
 -- Công nhân phản đối, Quản đốc chấp nhận phiếu (giữ phiếu).
 ('MAY1-261021-001', '2026-10-21', 'AFTERNOON', @may, @lM, @a3, @w2, 'REWORK', 2, 'CROOKED_SEAM',   NULL, NULL, '2026-10-21 16:10:00',
  N'Em đã sửa đúng mẫu, đề nghị xem lại', '2026-10-21 17:30:00', dbo.fn_NextWorkingDayEnd('2026-10-21'), 'CONFIRMED', @ttmay, '2026-10-21 16:10:00', '2026-10-22 08:30:00'),
 -- Phiếu mới ghi, công nhân còn trong 48 giờ để phản đối.
 ('MAY1-' + FORMAT(dbo.fn_VnNow(), 'yyMMdd') + '-001', CAST(dbo.fn_VnNow() AS date), 'AFTERNOON', @may, @lL, @a1, @w1, 'REWORK', 2, 'SKIPPED_STITCH', NULL, NULL, DATEADD(HOUR, -1, dbo.fn_VnNow()),
  NULL, NULL, dbo.fn_NextWorkingDayEnd(CAST(dbo.fn_VnNow() AS date)), 'RECORDED', @ttmay, DATEADD(HOUR, -1, dbo.fn_VnNow()), NULL);

INSERT dbo.Notification (RecipientId, SourceType, RefTable, RefId, Title, Message, PushStatus)
SELECT d.ResponsibleWorkerId, 'PRODUCTION', 'DefectRecord', d.DefectRecordId, N'Có phiếu hàng hỏng ghi nhận cho bạn',
       N'Phiếu ' + d.DefectCode + N' ghi nhận ' + CAST(d.Quantity AS NVARCHAR(10)) + N' sản phẩm '
       + IIF(d.DefectType = 'SCRAP', N'bị hủy', N'cần sửa lại') + N'. Nếu ghi sai, bạn có thể phản hồi.',
       'SENT'
FROM dbo.DefectRecord d WHERE d.DefectCode LIKE 'MAY1-26%' AND d.Status <> 'CANCELLED' AND d.WorkerNotifiedAt IS NOT NULL;

INSERT dbo.Notification (RecipientId, SourceType, RefTable, RefId, Title, Message, PushStatus)
VALUES (@ketoan, 'SYSTEM', 'PurchaseOrder', @po3, N'Đơn mua chờ thanh toán', N'Đơn mua DM-DEMO-003 đã đặt hàng, chờ Kế toán xác nhận thanh toán.', 'SENT'),
       (@quandoc, 'SYSTEM', 'PurchaseOrder', @po4, N'Đơn mua bị trả lại', N'Kế toán trả lại đơn mua DM-DEMO-004: đơn giá khóa kéo cao hơn báo giá.', 'SENT');

/* ---------- 12. KPI tổ may (MI-16/17/18) ---------- */
INSERT dbo.KpiTarget (OrderId, ProductId, SewingTeamId, StartDate, EndDate, TargetFinishedQty, WorkingDays, Status, Result) VALUES
 (@o1, @p1, @may, '2026-10-12', '2026-11-07',  600, 24, 'ACTIVE', NULL),
 (@o1, @p1, @may, '2026-10-01', '2026-10-10',  400,  9, 'CLOSED', 'ACHIEVED'),   -- QC đạt 01–10/10: 520
 (@o1, @p1, @may, '2026-09-10', '2026-09-19',  300,  8, 'CANCELLED', NULL),
 (@o2, @p2, @may, '2026-10-26', '2026-11-30',  800, 25, 'ACTIVE', NULL);

/* ---------- 13. Chi phí tháng 09/2026 (MI-12, MI-13, MI-19) ---------- */
DECLARE @sep INT = (SELECT PeriodId FROM dbo.AccountingPeriod WHERE PeriodMonth = '2026-09'),
        @aug INT = (SELECT PeriodId FROM dbo.AccountingPeriod WHERE PeriodMonth = '2026-08');
DECLARE @mealAmount DECIMAL(18,2) = (SELECT ISNULL(SUM(TotalAmount), 0) FROM dbo.vw_DailyMealTotal WHERE MealDate BETWEEN '2026-09-01' AND '2026-09-30'),
        @mealQty INT = (SELECT ISNULL(SUM(TotalMeals), 0) FROM dbo.vw_DailyMealTotal WHERE MealDate BETWEEN '2026-09-01' AND '2026-09-30');
INSERT dbo.MonthlyCost (PeriodId, CostCategoryCode, CostType, ProductId, Amount, Quantity, Note) VALUES
 (@sep, 'MEALS',            'FIXED',    NULL, @mealAmount, @mealQty, N'Hóa đơn bếp tháng 9 (khớp tổng suất ăn đã chốt)'),
 (@sep, 'FUEL',             'FIXED',    NULL,  6500000, NULL, NULL),
 (@sep, 'UTILITIES',        'FIXED',    NULL, 18000000, NULL, N'Điện, nước'),
 (@sep, 'PREMISES',         'FIXED',    NULL, 25000000, NULL, N'Thuê mặt bằng'),
 (@sep, 'MACHINE_REPAIR',   'FIXED',    NULL,  4000000, NULL, NULL),
 (@sep, 'STATIONERY',       'FIXED',    NULL,  1200000, NULL, NULL),
 (@sep, 'MACHINE_SUPPLIES', 'VARIABLE', @p1,   3500000, NULL, N'Kim, dầu máy cho áo khoác'),
 (@sep, 'MATERIALS_BOUGHT', 'VARIABLE', @p1,   8000000, NULL, N'Phụ liệu mua ngoài'),
 (@sep, 'OTHER',            'VARIABLE', @p2,    900000, NULL, NULL);

/* ---------- 14. Bảng lương: 08/2026 đã duyệt, 09/2026 chờ Admin duyệt (MI-09, MI-10) ---------- */
CREATE TABLE #Staff (Code VARCHAR(20) COLLATE DATABASE_DEFAULT, Grade CHAR(1) COLLATE DATABASE_DEFAULT NULL, Lcb DECIMAL(18,2), Sp DECIMAL(18,2), Piece BIT, WorkDays DECIMAL(5,2));
INSERT #Staff VALUES
 ('CNMAY1', 'B', 4500000, 7800000, 1, 25.5), ('CNMAY2', 'C', 4500000, 6900000, 1, 25), ('CNMAY3', 'A', 4500000, 8600000, 1, 26),
 ('CNMAY4', 'B', 4500000, 7400000, 1, 25.5), ('CNCAT1', NULL, 4500000, 6200000, 0, 26),
 ('TTMAY', 'A', 6000000, 9800000, 0, 26), ('TPMAY', 'B', 5500000, 8500000, 0, 26), ('TTCAT', 'A', 6000000, 9100000, 0, 26),
 ('KETOAN', NULL, 8000000, 8000000, 0, 26), ('QUANDOC', NULL, 12000000, 12000000, 0, 26), ('THUKHO', NULL, 7000000, 7000000, 0, 26),
 ('QAQC1', NULL, 5500000, 6300000, 0, 26), ('CODIEN', NULL, 7000000, 7000000, 0, 26);

CREATE TABLE #Months (PeriodId INT, Factor DECIMAL(4,2), Status VARCHAR(20) COLLATE DATABASE_DEFAULT);
INSERT #Months VALUES (@aug, 1.00, 'APPROVED'), (@sep, 1.08, 'PENDING_APPROVAL');

INSERT dbo.Payroll (PeriodId, Status, SubmittedAt, ApprovedAt)
SELECT m.PeriodId, m.Status,
       IIF(m.Status = 'APPROVED', '2026-09-06 08:30:00', '2026-10-06 08:30:00'), IIF(m.Status = 'APPROVED', '2026-09-07 10:00:00', NULL)
FROM #Months m
WHERE NOT EXISTS (SELECT 1 FROM dbo.Payroll p WHERE p.PeriodId = m.PeriodId);

-- Tính sẵn từng khoản cho mỗi phiếu
SELECT pr.PayrollId, e.EmployeeId, s.*, m.Factor,
       CAST(ROUND(s.Sp * m.Factor, 0) AS DECIMAL(18,2))                                         AS SpAmt,
       CAST(300000 AS DECIMAL(18,2))                                                            AS Attend,
       CAST(IIF(s.Code IN ('TTMAY', 'TPMAY', 'TTCAT', 'QUANDOC', 'KETOAN', 'THUKHO'), 200000, 0) AS DECIMAL(18,2)) AS Seniority,
       CAST(CASE s.Grade WHEN 'A' THEN 600000 WHEN 'B' THEN 400000 WHEN 'C' THEN 200000 ELSE 0 END AS DECIMAL(18,2)) AS GradeBonus
INTO #Slip
FROM #Staff s
JOIN dbo.Employee e ON e.EmployeeCode = s.Code
CROSS JOIN #Months m
JOIN dbo.Payroll pr ON pr.PeriodId = m.PeriodId
WHERE NOT EXISTS (SELECT 1 FROM dbo.Payslip x WHERE x.PayrollId = pr.PayrollId AND x.EmployeeId = e.EmployeeId);

ALTER TABLE #Slip ADD Total DECIMAL(18,2), Ins DECIMAL(18,2), Tax DECIMAL(18,2);
UPDATE #Slip SET Total = SpAmt + Attend + Seniority + GradeBonus, Ins = ROUND(Lcb * 0.105, 0);
UPDATE #Slip SET Tax = IIF(Total - Ins > 9500000, ROUND((Total - Ins - 9500000) * 0.05, 0), 0);

INSERT dbo.Payslip (PayrollId, EmployeeId, WorkDays, TotalAdditions, TotalDeductions, Insurance, NetPay)
SELECT PayrollId, EmployeeId, WorkDays, Total, Tax, Ins, Total - Ins - Tax FROM #Slip;

INSERT dbo.PayslipItem (PayslipId, ItemGroup, ItemName, Amount, SortOrder)
SELECT ps.PayslipId, i.G, i.N, i.A, i.S
FROM #Slip s
JOIN dbo.Payslip ps ON ps.PayrollId = s.PayrollId AND ps.EmployeeId = s.EmployeeId
CROSS APPLY (VALUES
    ('INFO',      N'LCB',                              s.Lcb,        10, 1),
    ('INFO',      N'Phân loại: ' + ISNULL(s.Grade, N'-'), CAST(0 AS DECIMAL(18,2)), 20, IIF(s.Grade IS NULL, 0, 1)),
    ('INFO',      IIF(s.Piece = 1, N'Ngày công SP', N'Số giờ công hành chính'),
                  IIF(s.Piece = 1, s.WorkDays, s.WorkDays * 8), 30, 1),
    ('INFO',      N'Số giờ làm thêm ngày thường',      CAST(IIF(s.Piece = 1, 12, 8) AS DECIMAL(18,2)), 40, 1),
    ('ADDITION',  IIF(s.Piece = 1, N'Lương sản phẩm theo hệ số', N'Lương ngày công phép'), s.SpAmt, 50, 1),
    ('ADDITION',  N'Chuyên cần + Xăng xe',             s.Attend,     60, 1),
    ('ADDITION',  N'Thâm niên',                        s.Seniority,  70, IIF(s.Seniority > 0, 1, 0)),
    ('ADDITION',  N'Thưởng PL',                        s.GradeBonus, 80, IIF(s.GradeBonus > 0, 1, 0)),
    ('INSURANCE', N'BHXH 10.5%',                       s.Ins,        90, 1),
    ('DEDUCTION', N'Thuế TNCN',                        s.Tax,       100, IIF(s.Tax > 0, 1, 0))
) i(G, N, A, S, Keep)
WHERE i.Keep = 1;


/* ---------- 15. Xe và sổ xe (MI-19) ---------- */
INSERT dbo.Vehicle (PlateNumber, VehicleType, FuelNormPer100Km) VALUES
 ('51C-123.45', N'Xe tải 1.5 tấn (DEMO)', 14), ('51D-678.90', N'Xe bán tải (DEMO)', 11);
DECLARE @v1 INT = (SELECT VehicleId FROM dbo.Vehicle WHERE PlateNumber = '51C-123.45'),
        @v2 INT = (SELECT VehicleId FROM dbo.Vehicle WHERE PlateNumber = '51D-678.90');
INSERT dbo.VehicleTrip (VehicleId, TripDate, DriverName, Purpose, OdometerStart, OdometerEnd, FuelLiters, FuelCost, Status, KmGapWarning, FuelOverNormWarning) VALUES
 (@v1, '2026-09-15', N'Phạm Văn Tài', N'Giao hàng cho công ty mẹ',     12000, 12080, 11.5, 253000, 'COMPLETED', 0, 0),
 (@v1, '2026-09-22', N'Phạm Văn Tài', N'Lấy vải tại Tân Phú',         12080, 12150, 14.0, 308000, 'COMPLETED', 0, 1),
 (@v2, '2026-09-28', N'Võ Minh Quân', N'Đi mua phụ liệu',             5400,  5440,  4.2,  92400,  'COMPLETED', 0, 0),
 (@v1, '2026-10-14', N'Phạm Văn Tài', N'Giao thành phẩm đợt 3',       12300, 12388, 12.6, 277200, 'COMPLETED', 1, 0),
 (@v2, '2026-10-20', N'Võ Minh Quân', N'Mua kim máy',                 5500,  NULL,  NULL, NULL,   'STARTED',   0, 0);

COMMIT TRANSACTION;

DROP TABLE #Days, #Assign, #Staff, #Months, #Slip;
GO

-- 19.2 Công đoạn và đơn giá công đoạn của DEMO-AO01 / DEMO-QU02 (nhập từ file Excel quy trình công nghệ)
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

CREATE TABLE #P (ProductCode VARCHAR(30) COLLATE DATABASE_DEFAULT, ProductName NVARCHAR(200) COLLATE DATABASE_DEFAULT,
                 Description NVARCHAR(1000) COLLATE DATABASE_DEFAULT NULL);
INSERT #P (ProductCode, ProductName, Description) VALUES
 (N'DEMO-AO01', N'Áo khoác gió DEMO', N'Mã hàng demo có 5 công đoạn'),
 (N'DEMO-QU02', N'Quần short DEMO', N'Mã hàng demo có 2 công đoạn');

CREATE TABLE #O (ProductCode VARCHAR(30) COLLATE DATABASE_DEFAULT, OperationCode VARCHAR(30) COLLATE DATABASE_DEFAULT,
                 OperationName NVARCHAR(200) COLLATE DATABASE_DEFAULT, Seq SMALLINT, Skill TINYINT NULL, StdTime DECIMAL(9,2) NULL,
                 TeamType VARCHAR(10) COLLATE DATABASE_DEFAULT, IsFinal BIT, Status VARCHAR(20) COLLATE DATABASE_DEFAULT);
INSERT #O (ProductCode, OperationCode, OperationName, Seq, Skill, StdTime, TeamType, IsFinal, Status) VALUES
 (N'DEMO-AO01', N'OP01', N'KT cơi lót túi sườn', 1, 1, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP02', N'KT đáp mác', 2, 1, 3.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP03', N'KT nẹp ve', 3, 1, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP04', N'Vắt sổ', 4, 1, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP05', N'KT thân trước lần', 5, 1, 5.00, 'SEWING', 1, 'ACTIVE'),
 (N'DEMO-AO01', N'OP06', N'KT TS lần', 6, 1, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP07', N'KT tay to lần', 7, 1, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP08', N'KT tay nhỏ lần', 8, 1, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP09', N'KT bản cổ ngoài', 9, 1, 3.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP10', N'KT bản cổ trong', 10, 1, 3.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP12', N'Lộn túi sườn+ sửa đường may túi, đầu cơi + bổ cơi', 12, 2, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP13', N'Bấm ngạnh cổ', 13, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP14', N'Bấm ngạnh nẹp ve + Lộn', 14, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP15', N'Bấm ngạnh TT +gấu + Lộn', 15, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP16', N'Sửa lộn nẹp áo hc', 16, 2, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP17', N'Sửa lộn bản cổ', 17, 2, 35.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP18', N'Quấn chân cúc(6)', 18, 2, 72.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP19', N'Đo+ cắt dây treo áo', 19, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP20', N'Lộn tay', 20, 2, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP21', N'Đi lại+ lấy hàng phòng chần', 21, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP23', N'VSCN+ tẩy dầu+ chấm bụi hc', 23, 2, 197.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP24', N'Nhận hàng', 24, 2, 100.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP25', N'Là dây treo', 25, 3, 7.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP26', N'Là cạnh mí đáp mác', 26, 3, 7.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP27', N'Là gập cơi túi sườn', 27, 3, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP28', N'Là túi sườn+ ke lót túi sườn', 28, 3, 26.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP29', N'Là quay lộn nẹp', 29, 3, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP30', N'Là ql bản cổ', 30, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP31', N'Là vai con', 31, 3, 12.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP32', N'Là sống lưng', 32, 3, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP33', N'Là sườn áo', 33, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP34', N'Là sống tay lần', 34, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP35', N'Là bụng tay lần', 35, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP36', N'Là vòng nách', 36, 3, 35.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP37', N'Là gập gấu áo', 37, 3, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP38', N'Là gập cửa tay trc chắp bụng tay', 38, 3, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP39', N'Là tra cổ lần', 39, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP40', N'Là cạnh mí nẹp ve', 40, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP41', N'Là ht áo', 41, 3, 120.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP43', N'đính cúc nẹp+ cổ(5)', 43, 3, 80.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP44', N'đính cúc nẹp ve phải(1)', 44, 3, 13.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP45', N'Đính cúc dự phòng trên+ dưới(1)', 45, 3, 51.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP46', N'thùa khuy nẹp phải(3)+ cổ(2)', 46, 3, 75.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP47', N'thùa khuy nẹp trái(1)', 47, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP48', N'SD cúc nẹp + cổ, cúc nẹp ve phải, khuy nẹp trái phải', 48, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP49', N'VS cạnh trong nẹp ve', 49, 2, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP50', N'VS cạnh chân đáp mác', 50, 2, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP51', N'VS chân cơi túi sườn', 51, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP52', N'VS gấu áo', 52, 2, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP53', N'VS cửa tay + rút chỉ', 53, 2, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP55', N'Ghim chân cổ lần lót+ ql bản cổ+ sd', 55, 3, 90.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP56', N'Ghim chập chân cổ để tra', 56, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP57', N'Ke dây treo', 57, 2, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP58', N'Mí cạnh trong nẹp ve', 58, 3, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP59', N'Đặt cơi, lót túi sườn+ ghim đường tra cổ trên thân, nẹp ve+ ql nẹp áo+ sd+ bổ túi sườn(báo hàng VC)', 59, 3, 430.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP60', N'Chặn ngạnh túi sườn hc', 60, 4, 100.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP61', N'May chân cơi túi sườn', 61, 3, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP62', N'Quay lót túi sườn', 62, 3, 113.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP63', N'Ke lót túi sườn', 63, 3, 105.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP64', N'Cắt nhét ke viền lót túi sườn', 64, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP66', N'Bóc số + Chắp sống tay', 66, 3, 70.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP67', N'Chắp bụng tay', 67, 3, 65.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP68', N'Vắt gấu cửa tay', 68, 3, 110.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP69', N'Chắp sống lưng', 69, 3, 35.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP70', N'Chắp vai con', 70, 3, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP71', N'Chắp sườn áo', 71, 3, 75.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP72', N'Tra tay+ sd + Rút chỉ', 72, 4, 160.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP73', N'Tra cổ lần hc+ sd', 73, 4, 110.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP74', N'Mí cạnh cong đáp mác', 74, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP75', N'Gắn dây treo', 75, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP76', N'Gắn mác cổ(2)', 76, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP77', N'Chắp vai con nẹp ve+ đáp mác', 77, 3, 55.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP78', N'Chặn rẽ đường may vai nẹp ve', 78, 3, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP79', N'Kê mí nẹp ve gấu áo đoạn gấu', 79, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP80', N'Tra QL cổ lót hc, sd', 80, 4, 120.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP81', N'Mí chân dây treo, mác cổ', 81, 3, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP82', N'Vắt gấu gấu áo', 82, 3, 110.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP83', N'Ghim đính vai con nẹp ve(2)', 83, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP84', N'Ghim đính đáp mác TS(1)', 84, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP85', N'Chặn đính túi sườn nẹp ve(2*2)', 85, 2, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP86', N'gắn mác sườn', 86, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP87', N'Gắn nhãn thị trường', 87, 2, 8.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP88', N'Diễu nẹp+ cổ áo', 88, 4, 200.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP89', N'Gim chập mác sườn', 89, 1, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP90', N'Tở dây ke túi sườn', 90, 1, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-AO01', N'OP91', N'Ghim cạnh ngoài lót túi sau quay x2', 91, 2, 42.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP01', N'May ống quần', 1, 2, 100.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP02', N'SD thân sau lót(điểm dán mác )', 2, 2, 15.00, 'SEWING', 1, 'DISCONTINUED'),
 (N'DEMO-QU02', N'OP07', N'Kt tay lần hc', 7, 1, 4.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP08', N'SD chun gấu(rút)', 8, 2, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP09', N'KT cơi, đáp ,lót túi sườn', 9, 1, 6.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP11', N'Đo+Cắt rời dây khuy nắp túi tay', 11, 2, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP12', N'Đo+Cắt rời dây khuy tay', 12, 2, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP13', N'Đo+ cắt dây treo', 13, 2, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP14', N'Đo+cắt tay kéo khoá nẹp', 14, 2, 5.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP15', N'Sửa lộn nắp túi tay + sd túi sườn', 15, 2, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP16', N'Sửa lộn dây khuy tay', 16, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP17', N'Bấm ngạnh miệng túi sườn', 17, 2, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP18', N'Sửa lộn áo qua nách+VS sạch sẽ', 18, 2, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP19', N'Sửa đầu cổ +đầu gấu', 19, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP20', N'Lộn đầu cổ+đầu gấu', 20, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP21', N'Lộn tay lần+lồng đôi', 21, 2, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP22', N'Xén sửa sống cổ', 22, 2, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP23', N'Xén sửa họng cổ lần+bấm nhả', 23, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP24', N'Đi lại lấy hàng phòng chần', 24, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP25', N'VSCN +tẩy dầu+xịt bụi h/c', 25, 2, 200.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP26', N'Nhận hàng', 26, 2, 90.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP27', N'Là khoá nẹp', 27, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP28', N'Là dây treo', 28, 3, 3.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP29', N'Là tay kéo khoá nẹp', 29, 3, 3.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP30', N'Chạy ke dây khuy tay hc(chạy ke máy ep mex)', 30, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP31', N'Là dây khuy tay sau mí', 31, 3, 7.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP32', N'Là ql nắp túi', 32, 3, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP33', N'Là Tp nắp túi', 33, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP34', N'Là chắp áo lót hc', 34, 3, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP35', N'Là gập chân cổ lót', 35, 3, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP36', N'Là gạt sống cổ', 36, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP37', N'Là chắp bụng tay', 37, 3, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP38', N'Là gạt chắp miệng túi sườn', 38, 3, 20.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP40', N'Là chắp cầu vai lần', 40, 3, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP41', N'Là chắp sườn áo trước', 41, 3, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP42', N'Là chắp sườn áo sau', 42, 3, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP43', N'Là chắp vai con', 43, 3, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP44', N'Là vòng nách', 44, 3, 35.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP45', N'Là HT áo', 45, 3, 110.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP46', N'Là ly TS', 46, 3, 17.50, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP47', N'Bọ miệng túi sườn + SD (4)', 47, 3, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP48', N'Bọ nắp túi tay (4)', 48, 3, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP49', N'Lồng+chặn bọ tay kéo khoá nẹp(3)', 49, 3, 35.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP50', N'Ke dây treo', 50, 2, 12.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP51', N'May mác cỡ& mác chính', 51, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP52', N'May ly Ts lót+ghim chặn đầu', 52, 3, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP53', N'May mác & cầu vai thân sau', 53, 3, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP54', N'Gắn dây treo', 54, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP55', N'Ghim chập +gắn mác sườn', 55, 2, 35.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP56', N'Gắn mác TT(ghi tiền theo sl)', 56, 2, 10.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP57', N'Chắp vai con lót', 57, 3, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP58', N'Chắp sườn áo lót(chỉ lót)', 58, 3, 85.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP59', N'Ke mí xq dây khuy nắp túi', 59, 3, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP60', N'Lồng+chặn chân dây khuy nắp túi tay', 60, 2, 25.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP61', N'Quay lộn nắp túi+đặt dây khuy', 61, 3, 65.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP62', N'Mí diễu xq nắp túi', 62, 3, 55.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP63', N'Ghim TP nắp túi+đánh số', 63, 2, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP64', N'Quay lộn 2 đầu dây khuy tay', 64, 3, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP65', N'Kẹp mí xq dây khuy tay(chiết kim)', 65, 3, 80.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP66', N'Đặt BTP+ nắp túi+dây khuy tay= gắn nắp+gắn dây khuy & tay', 66, 3, 140.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP67', N'Mí diễu gáy nắp túi', 67, 3, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP68', N'Chắp bụng tay lần', 68, 3, 80.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP69', N'May cửa tay', 69, 4, 140.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP70', N'Đặt cổ lần, lót= ghim chân, chắp sống+sd', 70, 3, 100.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP71', N'May đáp túi sườn', 71, 3, 55.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP72', N'chắp miệng túi sườn(chữ U)+ vén lót chắp sườn 2 đầu (bỏ miệng túi)', 72, 3, 80.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP73', N'Mí diễu miệng túi sườn', 73, 3, 60.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP74', N'Đặt lót +quay lót túi cạnh trên-dưới', 74, 3, 70.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP75', N'Vào lót túi to', 75, 3, 55.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP76', N'Quay lót túi cạnh nẹp', 76, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP77', N'Ghim lót túi ra nẹp', 77, 2, 35.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP78', N'Chắp sống lưng lần + đặt giằng', 78, 3, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP79', N'Mí diễu sống lưng lần', 79, 3, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP80', N'Chắp cầu vai lần', 80, 3, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP81', N'Diễu cầu vai lần', 81, 3, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP82', N'Chắp vai con lần', 82, 3, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP83', N'Mí vai con lần', 83, 3, 40.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP84', N'VS chắp sườn trước + đặt giằng 2 bên sườn', 84, 2, 90.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP85', N'Mí diễu sườn trước lần(nhấc kim)', 85, 3, 120.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP86', N'Chắp sườn sau lần', 86, 3, 90.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP87', N'Tra cổ lần+sd', 87, 4, 120.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP88', N'Chạy kê chun gấu+cắt', 88, 3, 130.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP89', N'Ke tay kéo khoá nẹp', 89, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP90', N'Gập đầu khoá nẹp', 90, 2, 15.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP91', N'Tra khoá nẹp+sd', 91, 4, 180.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP92', N'QL gấu lần, lót+sd', 92, 3, 125.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP93', N'QL nẹp+sd', 93, 3, 150.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP94', N'Chặn giằng trực tiếp gấu(5)', 94, 3, 90.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP95', N'Ghim chập họng cổ lần, lót', 95, 2, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP96', N'Mí kẹp họng cổ', 96, 4, 180.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP97', N'QL đầu cổ+sd', 97, 3, 50.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP98', N'Ghim chập vòng nách lần,lót', 98, 2, 90.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP99', N'Tra tay +sd + bóc số (x7)', 99, 4, 205.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP100', N'Mí vòng nách', 100, 3, 170.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP101', N'Mí diễu xq nẹp+sống cổ', 101, 4, 220.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP102', N'Mí dây khoá', 102, 2, 4.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP103', N'Ép mex vai trước', 103, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP104', N'Ép mex vai sau', 104, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP105', N'Ép mex túi sườn', 105, 2, 30.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP106', N'Bù', 106, 3, 106.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP107', N'Gia cố túi sườn', 107, 2, 45.00, 'SEWING', 0, 'ACTIVE'),
 (N'DEMO-QU02', N'OP108', N'Ghim miệng túi sườn', 108, 2, 15.00, 'SEWING', 0, 'ACTIVE');

CREATE TABLE #R (ProductCode VARCHAR(30) COLLATE DATABASE_DEFAULT, OperationCode VARCHAR(30) COLLATE DATABASE_DEFAULT,
                 Rate DECIMAL(18,2), EffectiveDate DATE, Status VARCHAR(20) COLLATE DATABASE_DEFAULT, SubmittedAt DATETIME2(0) NULL,
                 ApprovedAt DATETIME2(0) NULL, ApprovalNote NVARCHAR(500) COLLATE DATABASE_DEFAULT NULL,
                 RejectReason NVARCHAR(500) COLLATE DATABASE_DEFAULT NULL);
INSERT #R (ProductCode, OperationCode, Rate, EffectiveDate, Status, SubmittedAt, ApprovedAt, ApprovalNote, RejectReason) VALUES
 (N'DEMO-AO01', N'OP01', 1200.00, '2026-09-01', 'APPROVED', '2026-08-30T10:00:00', '2026-08-31T09:00:00', N'Duyệt theo bảng giá quý 3', NULL),
 (N'DEMO-AO01', N'OP02', 1500.00, '2026-09-01', 'APPROVED', '2026-08-30T10:00:00', '2026-08-31T09:00:00', NULL, NULL),
 (N'DEMO-AO01', N'OP02', 1700.00, '2026-10-22', 'REJECTED', '2026-10-18T11:00:00', NULL, NULL, N'Mức tăng 13% vượt khung, đề nghị trình lại'),
 (N'DEMO-AO01', N'OP03', 1000.00, '2026-09-01', 'APPROVED', '2026-08-30T10:00:00', '2026-08-31T09:00:00', NULL, NULL),
 (N'DEMO-AO01', N'OP03', 1100.00, '2026-10-25', 'APPROVED', '2026-10-20T15:00:00', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-AO01', N'OP04', 800.00, '2026-09-01', 'APPROVED', '2026-08-30T10:00:00', '2026-08-31T09:00:00', NULL, NULL),
 (N'DEMO-AO01', N'OP04', 900.00, '2026-11-16', 'DRAFT', NULL, NULL, NULL, NULL),
 (N'DEMO-AO01', N'OP05', 2000.00, '2026-09-01', 'APPROVED', '2026-08-30T10:00:00', '2026-08-31T09:00:00', NULL, NULL),
 (N'DEMO-QU02', N'OP01', 1300.00, '2026-10-01', 'APPROVED', '2026-09-28T10:00:00', '2026-09-29T09:00:00', NULL, NULL),
 (N'DEMO-QU02', N'OP02', 1800.00, '2026-10-01', 'PENDING_APPROVAL', '2026-10-20T16:00:00', NULL, NULL, NULL),
 (N'DEMO-QU02', N'OP07', 21.27, '2026-10-22', 'PENDING_APPROVAL', '2026-10-22T08:23:08', NULL, NULL, NULL),
 (N'DEMO-QU02', N'OP08', 113.80, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP09', 31.91, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP100', 1030.62, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP101', 1415.63, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP102', 22.76, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP103', 170.71, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP104', 170.71, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP105', 170.71, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP106', 642.62, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP107', 256.06, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP108', 85.35, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP11', 56.90, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP12', 113.80, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP13', 28.45, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP14', 28.45, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP15', 284.51, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP16', 170.71, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP17', 284.51, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP18', 256.06, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP19', 85.35, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP20', 85.35, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP21', 56.90, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP22', 56.90, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP23', 142.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP24', 121.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP25', 1138.04, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP26', 512.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP27', 90.94, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP28', 18.19, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP29', 18.19, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP30', 121.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP31', 42.44, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP32', 181.87, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP33', 90.94, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP34', 151.56, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP35', 151.56, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP36', 90.94, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP37', 151.56, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP38', 121.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP40', 60.62, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP41', 151.56, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP42', 151.56, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP43', 90.94, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP44', 212.19, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP45', 666.87, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP46', 106.09, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP47', 363.75, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP48', 272.81, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP49', 212.19, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP50', 68.28, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP51', 85.35, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP52', 151.56, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP53', 303.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP54', 142.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP55', 199.16, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP56', 56.90, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP57', 303.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP58', 515.31, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP59', 242.50, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP60', 142.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP61', 394.06, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP62', 333.43, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP63', 227.61, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP64', 363.75, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP65', 485.00, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP66', 848.74, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP67', 363.75, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP68', 485.00, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP69', 900.86, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP70', 606.24, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP71', 333.43, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP72', 485.00, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP73', 363.75, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP74', 424.37, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP75', 333.43, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP76', 170.71, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP77', 199.16, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP78', 303.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP79', 242.50, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP80', 272.81, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP81', 242.50, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP82', 272.81, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP83', 242.50, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP84', 512.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP85', 727.49, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP86', 545.62, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP87', 772.16, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP88', 788.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP89', 85.35, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP90', 85.35, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP91', 1158.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP92', 757.81, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP93', 909.37, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP94', 545.62, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP95', 284.51, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP96', 1158.25, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP97', 303.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP98', 512.12, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL),
 (N'DEMO-QU02', N'OP99', 1319.11, '2026-10-22', 'APPROVED', '2026-10-22T08:23:08', '2026-10-22T09:00:59', NULL, NULL);

-- Mã hàng
INSERT dbo.Product (ProductCode, ProductName, Description)
SELECT s.ProductCode, s.ProductName, s.Description FROM #P s
WHERE NOT EXISTS (SELECT 1 FROM dbo.Product p WHERE p.ProductCode = s.ProductCode);

-- Công đoạn: bỏ cờ công đoạn cuối trước (chỉ được một công đoạn cuối đang dùng cho mỗi mã hàng), cập nhật, thêm, rồi đặt lại cờ
UPDATE o SET IsFinalStep = 0
FROM dbo.Operation o JOIN dbo.Product p ON p.ProductId = o.ProductId
WHERE p.ProductCode IN (SELECT ProductCode FROM #P) AND o.IsFinalStep = 1;

UPDATE o SET OperationName = s.OperationName, StationSequence = s.Seq, SkillLevel = s.Skill, StandardTimeSec = s.StdTime,
             TeamType = s.TeamType, Status = s.Status
FROM dbo.Operation o
JOIN dbo.Product p ON p.ProductId = o.ProductId
JOIN #O s ON s.ProductCode = p.ProductCode AND s.OperationCode = o.OperationCode;

INSERT dbo.Operation (ProductId, OperationCode, OperationName, StationSequence, SkillLevel, StandardTimeSec, TeamType, IsFinalStep, Status)
SELECT p.ProductId, s.OperationCode, s.OperationName, s.Seq, s.Skill, s.StdTime, s.TeamType, 0, s.Status
FROM #O s JOIN dbo.Product p ON p.ProductCode = s.ProductCode
WHERE NOT EXISTS (SELECT 1 FROM dbo.Operation o WHERE o.ProductId = p.ProductId AND o.OperationCode = s.OperationCode);

UPDATE o SET IsFinalStep = 1
FROM dbo.Operation o
JOIN dbo.Product p ON p.ProductId = o.ProductId
JOIN #O s ON s.ProductCode = p.ProductCode AND s.OperationCode = o.OperationCode
WHERE s.IsFinal = 1;   -- chỉ được một công đoạn cuối ĐANG DÙNG mỗi mã hàng (index lọc); công đoạn cuối đã ngừng dùng vẫn giữ cờ

-- Công đoạn to (25/10): demo nhóm theo từ khóa trong tên công đoạn nhỏ; công đoạn không khớp để ngoài nhóm
INSERT dbo.OperationGroup (ProductId, GroupName)
SELECT p.ProductId, g.GroupName
FROM dbo.Product p CROSS JOIN (VALUES (N'May tay'), (N'May cổ'), (N'May túi')) g(GroupName)
WHERE p.ProductCode = N'DEMO-AO01'
  AND NOT EXISTS (SELECT 1 FROM dbo.OperationGroup x WHERE x.ProductId = p.ProductId AND x.GroupName = g.GroupName);
UPDATE o SET OperationGroupId = g.OperationGroupId
FROM dbo.Operation o
JOIN dbo.Product p ON p.ProductId = o.ProductId AND p.ProductCode = N'DEMO-AO01'
JOIN dbo.OperationGroup g ON g.ProductId = o.ProductId
 AND g.GroupName = CASE WHEN o.OperationName LIKE N'%tay%' THEN N'May tay'
                        WHEN o.OperationName LIKE N'%cổ%'  THEN N'May cổ'
                        WHEN o.OperationName LIKE N'%túi%' THEN N'May túi' END
WHERE o.OperationGroupId IS NULL;

-- Công đoạn ↔ chi tiết vải (25/10, DEV-ALL-86): demo nối theo từ khóa trong tên công đoạn;
-- một công đoạn có thể khớp nhiều chi tiết. Phụ liệu (nhãn) không gắn công đoạn
INSERT dbo.OperationComponent (OperationId, ProductComponentId, ProductId)
SELECT DISTINCT o.OperationId, pc.ProductComponentId, o.ProductId
FROM dbo.Operation o
JOIN dbo.Product p ON p.ProductId = o.ProductId AND p.ProductCode = N'DEMO-AO01'
JOIN (VALUES (N'%tay%', N'Tay áo'), (N'%thân trước%', N'Thân trước'), (N'%TT %', N'Thân trước'),
             (N'%TS%', N'Thân sau'), (N'%sống lưng%', N'Thân sau'),
             (N'Tra tay%', N'Thân trước'), (N'Tra tay%', N'Thân sau'), (N'%sườn áo%', N'Thân trước'), (N'%sườn áo%', N'Thân sau'),
             (N'Chắp vai con', N'Thân trước'), (N'Chắp vai con', N'Thân sau')) k(Pattern, ComponentName)
  ON o.OperationName LIKE k.Pattern
JOIN dbo.ProductComponent pc ON pc.ProductId = o.ProductId AND pc.ComponentName = k.ComponentName AND pc.ComponentType = 'PANEL'
WHERE NOT EXISTS (SELECT 1 FROM dbo.OperationComponent x WHERE x.OperationId = o.OperationId AND x.ProductComponentId = pc.ProductComponentId);

-- Đơn giá công đoạn: khớp theo (mã hàng, mã công đoạn, ngày hiệu lực)
UPDATE r SET Rate = s.Rate, Status = s.Status, SubmittedAt = s.SubmittedAt, ApprovedAt = s.ApprovedAt,
             DecisionNote = ISNULL(s.RejectReason, s.ApprovalNote)
FROM dbo.PieceRate r
JOIN dbo.Operation o ON o.OperationId = r.OperationId
JOIN dbo.Product p ON p.ProductId = o.ProductId
JOIN #R s ON s.ProductCode = p.ProductCode AND s.OperationCode = o.OperationCode AND s.EffectiveDate = r.EffectiveDate;

INSERT dbo.PieceRate (OperationId, Rate, EffectiveDate, Status, SubmittedAt, ApprovedAt, DecisionNote)
SELECT o.OperationId, s.Rate, s.EffectiveDate, s.Status, s.SubmittedAt, s.ApprovedAt, ISNULL(s.RejectReason, s.ApprovalNote)
FROM #R s
JOIN dbo.Product p ON p.ProductCode = s.ProductCode
JOIN dbo.Operation o ON o.ProductId = p.ProductId AND o.OperationCode = s.OperationCode
WHERE NOT EXISTS (SELECT 1 FROM dbo.PieceRate r WHERE r.OperationId = o.OperationId AND r.EffectiveDate = s.EffectiveDate);

COMMIT TRANSACTION;

DROP TABLE #P, #O, #R;
GO

/* =====================================================================================
   19.3 Dữ liệu demo để test luồng kho / cắt / giao việc trên giao diện (26/10)
   ---------------------------------------------------------------------------------
   - Ghi tồn kho đúng cách: mọi bút toán đi qua dbo.usp_Stock_Post (StockBalance luôn bằng Σ thẻ kho).
   - Bù thẻ kho cho dữ liệu 19.1 (phiếu nhập PN-DEMO-001, cấp BTP PC-DEMO-001, bàn giao thành phẩm, ...).
   - Thêm đơn DEMO-DH003 (mã hàng DEMO-AO01): mỗi bước xác nhận có sẵn một phiếu đang chờ để bấm thử:
       Quản đốc xác nhận phiếu nhập (PN-DEMO-003, cần chụp ảnh chứng từ), chốt đơn mua (DM-DEMO-009);
       Tổ trưởng cắt nhận vải (PC-DEMO-004), Thủ kho xác nhận trả vải (TV-DEMO-001) và bàn giao BTP (BG-DEMO-003);
       Tổ trưởng may nhận BTP (PC-DEMO-006), công nhân xác nhận giao việc / thu hồi (WorkAllocation PENDING);
       Thủ kho xác nhận xuất hàng (XH-DEMO-001), nhận bàn giao thành phẩm (TP-DEMO-004), Quản đốc duyệt kiểm kê (KK-DEMO-002).
   - Chạy lại không nhân đôi dữ liệu (bỏ qua nếu đã có đơn DEMO-DH003). Xóa cả mục 19 nếu muốn database sạch.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH003')
BEGIN
    PRINT N'Dữ liệu demo 19.3 đã có (DEMO-DH003), bỏ qua.';
    RETURN;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH001')
   OR NOT EXISTS (SELECT 1 FROM dbo.MaterialIssue WHERE IssueCode = 'PC-DEMO-001')
BEGIN
    PRINT N'Chưa có dữ liệu demo 19.1 (DEMO-DH001), bỏ qua 19.3.';
    RETURN;
END;

BEGIN TRANSACTION;

DECLARE @cat INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'CAT'),
        @may INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY1');
DECLARE @quandoc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC'),
        @thukho  INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'THUKHO'),
        @ttcat   INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTCAT'),
        @ttmay   INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTMAY'),
        @qaqc    INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QAQC1');
DECLARE @w1 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY1'),
        @w2 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY2'),
        @w3 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY3'),
        @w4 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY4');
DECLARE @p1 INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-AO01'),
        @o1 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH001'),
        @cus INT = (SELECT CustomerId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH001');
DECLARE @mVai INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI01'),
        @mChi INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-CHI01'),
        @mChiXanh INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-CHI02'),
        @mKhoa INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-KHOA01'),
        @mNhan INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-NHAN01'),
        @mKim INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-KIM01'),
        @mVaiXanh INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI02'),
        @mVaiTrang INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI03');
DECLARE @sup1 INT = (SELECT SupplierId FROM dbo.Supplier WHERE SupplierName LIKE N'Công ty Vải Sợi Thành Công%'),
        @sup2 INT = (SELECT SupplierId FROM dbo.Supplier WHERE SupplierName LIKE N'Phụ liệu may Hoàng Gia%');
DECLARE @lM INT = (SELECT ol.OrderLineId FROM dbo.OrderLine ol JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
                   JOIN dbo.ProductColor pcol ON pcol.ProductColorId = pv.ProductColorId
                   WHERE ol.OrderId = @o1 AND pcol.Color = N'Đen' AND pv.SizeCode = 'M'),
        @lL INT = (SELECT ol.OrderLineId FROM dbo.OrderLine ol JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
                   JOIN dbo.ProductColor pcol ON pcol.ProductColorId = pv.ProductColorId
                   WHERE ol.OrderId = @o1 AND pcol.Color = N'Đen' AND pv.SizeCode = 'L');

/* ---------- 1. Đơn mới DEMO-DH003: dòng đơn + NVL của đơn ---------- */
INSERT dbo.CustomerOrder (OrderCode, CustomerId, ReceivedDate, DueDate, MaterialStatus, Status, Note)
VALUES ('DEMO-DH003', @cus, '2026-10-15', '2026-11-20', 'PARTIAL', 'IN_PRODUCTION', N'Đơn demo để test luồng kho, cắt và giao việc');
DECLARE @o3 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH003');

INSERT dbo.OrderLine (OrderId, ProductVariantId, ProductId, OrderedQty, CmtUnitPrice)
SELECT @o3, pv.ProductVariantId, pv.ProductId, v.Qty, 28000
FROM (VALUES (N'Đen', 'M', 120), (N'Đen', 'L', 100)) v(Color, SizeCode, Qty)
JOIN dbo.ProductColor pcol ON pcol.ProductId = @p1 AND pcol.Color = v.Color
JOIN dbo.ProductVariant pv ON pv.ProductColorId = pcol.ProductColorId AND pv.SizeCode = v.SizeCode;
DECLARE @l3M INT = (SELECT ol.OrderLineId FROM dbo.OrderLine ol JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
                    WHERE ol.OrderId = @o3 AND pv.SizeCode = 'M'),
        @l3L INT = (SELECT ol.OrderLineId FROM dbo.OrderLine ol JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
                    WHERE ol.OrderId = @o3 AND pv.SizeCode = 'L');

INSERT dbo.OrderMaterial (OrderId, MaterialId, RequiredQty, CustomerQty, ExpectedDeliveryCount) VALUES
 (@o3, @mVai, 320, 0, NULL), (@o3, @mChi, 25, 0, NULL), (@o3, @mKhoa, 220, 0, NULL), (@o3, @mNhan, 220, 0, NULL);
DECLARE @om3Vai  INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o3 AND MaterialId = @mVai),
        @om3Khoa INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o3 AND MaterialId = @mKhoa),
        @om3Nhan INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o3 AND MaterialId = @mNhan);

/* ---------- 2. Đơn mua + phiếu nhập NVL ---------- */
-- DM-DEMO-008: đã đặt, NCC giao 2 đợt; đợt 1 đã xác nhận (150 m), đợt 2 đang chờ Quản đốc xác nhận (100 m)
INSERT dbo.PurchaseOrder (PoCode, OrderId, SupplierId, OrderDate, ExpectedDeliveryCount, GoodsStatus, PaymentStatus, Note)
VALUES ('DM-DEMO-008', @o3, @sup1, '2026-10-20', 2, 'PARTIALLY_RECEIVED', 'UNPAID', N'Mua vải cho DEMO-DH003, chờ Kế toán xác nhận thanh toán');
-- DM-DEMO-009: chờ Quản đốc chốt nhà cung cấp và đơn giá
INSERT dbo.PurchaseOrder (PoCode, OrderId, GoodsStatus, PaymentStatus, Note)
VALUES ('DM-DEMO-009', @o3, 'PENDING_FINALISATION', 'UNPAID', N'Khóa kéo và nhãn của DEMO-DH003');
DECLARE @po8 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'DM-DEMO-008'),
        @po9 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'DM-DEMO-009');
INSERT dbo.PurchaseOrderLine (PurchaseOrderId, OrderMaterialId, OrderedQty, UnitPrice) VALUES
 (@po8, @om3Vai, 320, 45000), (@po9, @om3Khoa, 220, 0), (@po9, @om3Nhan, 220, 0);
DECLARE @pol8 INT = (SELECT PurchaseOrderLineId FROM dbo.PurchaseOrderLine WHERE PurchaseOrderId = @po8);

INSERT dbo.MaterialReceipt (ReceiptCode, OrderId, PurchaseOrderId, ReceiptDate, Status, ConfirmedBy, ConfirmedAt, SubmittedAt) VALUES
 ('PN-DEMO-002', @o3, @po8, '2026-10-22', 'CONFIRMED', @quandoc, '2026-10-22 15:30:00', '2026-10-22 14:00:00'),
 ('PN-DEMO-003', @o3, @po8, '2026-10-26', 'PENDING', NULL, NULL, '2026-10-26 10:00:00');
INSERT dbo.MaterialReceiptLine (MaterialReceiptId, OrderMaterialId, PurchaseOrderLineId, AcceptedQty)
SELECT r.MaterialReceiptId, @om3Vai, @pol8, v.Qty
FROM (VALUES ('PN-DEMO-002', 150), ('PN-DEMO-003', 100)) v(Code, Qty)
JOIN dbo.MaterialReceipt r ON r.ReceiptCode = v.Code;

/* ---------- 3. Lệnh cắt, phiếu cấp vải, bàn cắt, bàn giao BTP của DEMO-DH001 (bù chuỗi kho BTP) ---------- */
INSERT dbo.WorkOrder (WorkOrderCode, OrderId, ProductId, CuttingTeamId, DueDate, Status, Note, CreatedAt, CompletedAt)
VALUES ('LC-DEMO-001', @o1, @p1, @cat, '2026-09-22', 'COMPLETED', N'Lệnh cắt đã hoàn thành của DEMO-DH001', '2026-09-21 08:00:00', '2026-09-22 17:00:00');
DECLARE @wo1 INT = (SELECT WorkOrderId FROM dbo.WorkOrder WHERE WorkOrderCode = 'LC-DEMO-001');
INSERT dbo.WorkOrderLine (WorkOrderId, OrderLineId, PlannedQty)
SELECT @wo1, ol.OrderLineId, ol.OrderedQty FROM dbo.OrderLine ol WHERE ol.OrderId = @o1;
INSERT dbo.CutHandover (HandoverCode, WorkOrderId, HandedAt, Status, ConfirmedBy, ConfirmedAt)
VALUES ('BG-DEMO-001', @wo1, '2026-09-22 16:30:00', 'CONFIRMED', @thukho, '2026-09-22 17:00:00');
INSERT dbo.CutHandoverLine (CutHandoverId, OrderLineId, HandedQty, ConfirmedQty)
SELECT h.CutHandoverId, ol.OrderLineId, ol.OrderedQty, ol.OrderedQty
FROM dbo.CutHandover h CROSS JOIN dbo.OrderLine ol WHERE h.HandoverCode = 'BG-DEMO-001' AND ol.OrderId = @o1;

/* ---------- 4. DEMO-DH003: lệnh cắt, phiếu cấp vải, bàn cắt, trả vải, bàn giao BTP ---------- */
INSERT dbo.WorkOrder (WorkOrderCode, OrderId, ProductId, CuttingTeamId, DueDate, Status, Note, CreatedAt)
VALUES ('LC-DEMO-002', @o3, @p1, @cat, '2026-11-05', 'CUTTING', N'Lệnh cắt DEMO-DH003 đang cắt', '2026-10-22 08:00:00');
DECLARE @wo2 INT = (SELECT WorkOrderId FROM dbo.WorkOrder WHERE WorkOrderCode = 'LC-DEMO-002');
INSERT dbo.WorkOrderLine (WorkOrderId, OrderLineId, PlannedQty) VALUES (@wo2, @l3M, 120), (@wo2, @l3L, 100);

-- PC-DEMO-003: vải đã cấp và tổ cắt đã nhận (100 m); PC-DEMO-004: vải cấp thêm, đang chờ tổ trưởng cắt nhận (150 m)
INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, WorkOrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, ReceivedBy, ReceivedAt, CreatedAt) VALUES
 ('PC-DEMO-003', 'MATERIAL', @o3, @wo2, @cat, '2026-10-23', @thukho, 'RECEIVED', @ttcat, '2026-10-23 09:30:00', '2026-10-23 09:00:00'),
 ('PC-DEMO-004', 'MATERIAL', @o3, @wo2, @cat, '2026-10-26', @thukho, 'PENDING_RECEIPT', NULL, NULL, '2026-10-26 08:30:00');
INSERT dbo.MaterialIssueLine (MaterialIssueId, MaterialId, IssuedQty, ConfirmedQty)
SELECT i.MaterialIssueId, @mVai, v.Qty, v.Conf
FROM (VALUES ('PC-DEMO-003', 100, 100), ('PC-DEMO-004', 150, NULL)) v(Code, Qty, Conf)
JOIN dbo.MaterialIssue i ON i.IssueCode = v.Code;

-- TV-DEMO-001: tổ cắt trả 8 m vải thừa, chờ Thủ kho xác nhận
INSERT dbo.MaterialReturn (ReturnCode, MaterialIssueId, Note, Status, CreatedAt)
SELECT 'TV-DEMO-001', i.MaterialIssueId, N'Vải thừa sau bàn cắt 1', 'PENDING', '2026-10-25 16:00:00'
FROM dbo.MaterialIssue i WHERE i.IssueCode = 'PC-DEMO-003';
INSERT dbo.MaterialReturnLine (MaterialReturnId, MaterialIssueLineId, ReturnedQty)
SELECT r.MaterialReturnId, l.MaterialIssueLineId, 8
FROM dbo.MaterialReturn r JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = r.MaterialIssueId WHERE r.ReturnCode = 'TV-DEMO-001';

-- Bàn cắt: 30 lớp × (M:3, L:2) = 90 M + 60 L; bàn 2: 10 lớp = 30 M + 20 L
INSERT dbo.CuttingLay (WorkOrderId, CuttingTeamId, WorkDate, ShiftCode, LaySequence, FabricMaterialId, PlyCount, LayLengthM, Status, CreatedAt) VALUES
 (@wo2, @cat, '2026-10-24', 'MORNING', 1, @mVai, 30, 2.2, 'RECORDED', '2026-10-24 11:00:00'),
 (@wo2, @cat, '2026-10-26', 'MORNING', 1, @mVai, 10, 2.2, 'RECORDED', '2026-10-26 11:00:00');
INSERT dbo.CuttingLayLine (CuttingLayId, OrderLineId, RatioValue, CalculatedQty, ActualQty)
SELECT c.CuttingLayId, v.OrderLineId, v.Ratio, c.PlyCount * v.Ratio, c.PlyCount * v.Ratio
FROM dbo.CuttingLay c
CROSS JOIN (VALUES (@l3M, 3), (@l3L, 2)) v(OrderLineId, Ratio)
WHERE c.WorkOrderId = @wo2;

-- Bàn giao BTP: BG-DEMO-002 đã nhập kho BTP (90 M + 60 L); BG-DEMO-003 đang chờ Thủ kho xác nhận (30 M + 20 L)
INSERT dbo.CutHandover (HandoverCode, WorkOrderId, HandedAt, Status, ConfirmedBy, ConfirmedAt) VALUES
 ('BG-DEMO-002', @wo2, '2026-10-24 16:30:00', 'CONFIRMED', @thukho, '2026-10-24 17:00:00'),
 ('BG-DEMO-003', @wo2, '2026-10-26 16:30:00', 'PENDING', NULL, NULL);
INSERT dbo.CutHandoverLine (CutHandoverId, OrderLineId, HandedQty, ConfirmedQty)
SELECT h.CutHandoverId, v.OrderLineId, v.Qty, CASE WHEN h.Status = 'CONFIRMED' THEN v.Qty END
FROM dbo.CutHandover h
JOIN (VALUES ('BG-DEMO-002', @l3M, 90), ('BG-DEMO-002', @l3L, 60), ('BG-DEMO-003', @l3M, 30), ('BG-DEMO-003', @l3L, 20)) v(Code, OrderLineId, Qty)
  ON v.Code = h.HandoverCode;

/* ---------- 5. Cấp BTP cho tổ may + đơn xin NVL / BTP đang chờ Thủ kho ---------- */
-- PC-DEMO-005: tổ may đã nhận 50 M + 40 L (cấp trần giao việc); PC-DEMO-006: cấp thêm, đang chờ tổ trưởng may nhận
INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, ReceivedBy, ReceivedAt, CreatedAt) VALUES
 ('PC-DEMO-005', 'SEMI_FINISHED', @o3, @may, '2026-10-25', @thukho, 'RECEIVED', @ttmay, '2026-10-25 09:30:00', '2026-10-25 09:00:00'),
 ('PC-DEMO-006', 'SEMI_FINISHED', @o3, @may, '2026-10-26', @thukho, 'PENDING_RECEIPT', NULL, NULL, '2026-10-26 14:00:00');
INSERT dbo.MaterialIssueLine (MaterialIssueId, OrderLineId, IssuedQty, ConfirmedQty)
SELECT i.MaterialIssueId, v.OrderLineId, v.Qty, CASE WHEN i.Status = 'RECEIVED' THEN v.Qty END
FROM dbo.MaterialIssue i
JOIN (VALUES ('PC-DEMO-005', @l3M, 50), ('PC-DEMO-005', @l3L, 40), ('PC-DEMO-006', @l3M, 30), ('PC-DEMO-006', @l3L, 10)) v(Code, OrderLineId, Qty)
  ON v.Code = i.IssueCode;

INSERT dbo.Request (RequestCode, RequestTypeCode, Status, OrderId, WorkOrderId, TargetDate, Title, Content, CreatedBy, CreatedByTeamId, CreatedAt) VALUES
 ('YC-DEMO-001', 'MATERIAL_REQUISITION', 'PENDING_PROCESSING', @o3, @wo2, '2026-10-29', N'Xin thêm vải cho DEMO-DH003',
  N'Tổ cắt còn thiếu vải để cắt hết lệnh LC-DEMO-002', @ttcat, @cat, '2026-10-26 15:00:00'),
 ('YC-DEMO-002', 'SEMI_FINISHED_REQUISITION', 'PENDING_PROCESSING', @o3, NULL, '2026-10-29', N'Xin BTP đợt 3 cho DEMO-DH003',
  N'Tổ may cần thêm BTP để chia việc', @ttmay, @may, '2026-10-26 15:30:00');
INSERT dbo.RequestLine (RequestId, MaterialId, Quantity)
SELECT RequestId, @mVai, 80 FROM dbo.Request WHERE RequestCode = 'YC-DEMO-001';
INSERT dbo.RequestLine (RequestId, OrderLineId, Quantity)
SELECT r.RequestId, v.OrderLineId, v.Qty FROM dbo.Request r
CROSS JOIN (VALUES (@l3M, 30), (@l3L, 20)) v(OrderLineId, Qty) WHERE r.RequestCode = 'YC-DEMO-002';

/* ---------- 6. Phân công + giao việc cho công nhân (DEMO-DH003) ---------- */
DECLARE @op1 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP01'),
        @op2 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP02'),
        @op3 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP03'),
        @op4 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP04'),
        @op5 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p1 AND OperationCode = 'OP05');
INSERT dbo.OperationAssignment (SewingTeamId, OrderId, OperationId, WorkerId, ValidFrom) VALUES
 (@may, @o3, @op1, @w1, '2026-10-25'), (@may, @o3, @op2, @w1, '2026-10-25'), (@may, @o3, @op3, @w2, '2026-10-25'),
 (@may, @o3, @op4, @w3, '2026-10-25'), (@may, @o3, @op5, @w4, '2026-10-25');
-- Phải phân công đủ công đoạn mới nhận được BTP (DEV-ALL-48): các công đoạn còn lại chia lần lượt cho 4 công nhân
INSERT dbo.OperationAssignment (SewingTeamId, OrderId, OperationId, WorkerId, ValidFrom)
SELECT @may, @o3, o.OperationId, CHOOSE(ROW_NUMBER() OVER (ORDER BY o.StationSequence) % 4 + 1, @w1, @w2, @w3, @w4), '2026-10-25'
FROM dbo.Operation o
WHERE o.ProductId = @p1 AND o.Status = 'ACTIVE' AND o.TeamType = 'SEWING'
  AND o.OperationId NOT IN (@op1, @op2, @op3, @op4, @op5);
-- Tổ đã nhận 50 M + 40 L BTP nên mỗi công đoạn nhận tối đa 50 (M) / 40 (L) lần
INSERT dbo.WorkAllocation (MovementType, SewingTeamId, WorkerId, OrderLineId, OperationId, Quantity, ConfirmedQty,
                           RetrievalReason, ConfirmMode, Status, CreatedAt, ConfirmedAt) VALUES
 ('HANDOVER',  @may, @w1, @l3M, @op1, 20, NULL, NULL, 'WORKER', 'PENDING',   '2026-10-26 08:00:00', NULL),
 ('HANDOVER',  @may, @w1, @l3M, @op2, 30, 30,   NULL, 'WORKER', 'CONFIRMED', '2026-10-25 10:00:00', '2026-10-25 10:30:00'),
 ('RETRIEVAL', @may, @w1, @l3M, @op2,  5, NULL, 'WORKER_ABSENT', 'WORKER', 'PENDING', '2026-10-26 09:00:00', NULL),
 ('HANDOVER',  @may, @w2, @l3L, @op3, 25, 25,   NULL, 'WORKER', 'CONFIRMED', '2026-10-25 10:00:00', '2026-10-25 10:20:00'),
 ('HANDOVER',  @may, @w3, @l3L, @op4, 15, NULL, NULL, 'WORKER', 'PENDING',   '2026-10-26 08:10:00', NULL),
 ('HANDOVER',  @may, @w4, @l3M, @op5, 20, 20,   NULL, 'WORKER', 'CONFIRMED', '2026-10-25 10:00:00', '2026-10-25 10:40:00');

/* ---------- 7. Thành phẩm + xuất hàng (DEMO-DH001) ---------- */
-- QC đã đạt 510 (M) và 350 (L), đã bàn giao 450 và 350: còn 60 M đang chờ Thủ kho xác nhận nhận vào kho TP
INSERT dbo.FgHandover (HandoverCode, OrderId, ProductId, InspectorId, HandedAt, Status)
VALUES ('TP-DEMO-004', @o1, @p1, @qaqc, '2026-10-26 16:00:00', 'PENDING');
INSERT dbo.FgHandoverLine (FgHandoverId, OrderLineId, HandedQty)
SELECT FgHandoverId, @lM, 60 FROM dbo.FgHandover WHERE HandoverCode = 'TP-DEMO-004';

-- XH-DEMO-001 và XH-DEMO-003 chờ Thủ kho xuất;
-- XH-DEMO-002 đã xuất, đang giao (Kế toán xác nhận khách nhận)
INSERT dbo.DispatchNote (DispatchCode, OrderId, DeliveryDate, DeliveryAddress, Note, Status, CreatedAt, DispatchedBy, DispatchedAt) VALUES
 ('XH-DEMO-001', @o1, '2026-11-05', N'Kho khách hàng, KCN Tân Bình, TP.HCM', N'Giao đợt 2', 'PENDING', '2026-10-30 15:00:00', NULL, NULL),
 ('XH-DEMO-002', @o1, '2026-11-02', N'Kho khách hàng, KCN Tân Bình, TP.HCM', N'Giao đợt 1', 'IN_DELIVERY', '2026-10-29 15:00:00', @thukho, '2026-10-31 09:00:00'),
 ('XH-DEMO-003', @o1, '2026-11-09', N'Kho khách hàng, KCN Tân Bình, TP.HCM', N'Giao đợt 3', 'PENDING', '2026-10-31 08:00:00', NULL, NULL);
INSERT dbo.DispatchNoteLine (DispatchNoteId, OrderLineId, IssueQty)
SELECT d.DispatchNoteId, v.OrderLineId, v.Qty
FROM dbo.DispatchNote d
JOIN (VALUES ('XH-DEMO-001', @lM, 150), ('XH-DEMO-001', @lL, 100), ('XH-DEMO-002', @lM, 100), ('XH-DEMO-003', @lM, 20)) v(Code, OrderLineId, Qty)
  ON v.Code = d.DispatchCode;

/* ---------- 8. Kiểm kê: phiếu tồn đầu kỳ đã duyệt (ghi tồn qua thủ tục) ---------- */
INSERT dbo.Stocktake (StocktakeCode, WarehouseType, Scope, StocktakeDate, Status, CreatedAt, ApprovedAt)
VALUES ('KK-DEMO-001', 'MATERIAL', N'Tồn đầu kỳ kho NVL', '2026-09-01', 'APPROVED', '2026-09-01 08:00:00', '2026-09-01 10:00:00');

/* ---------- 9. Ghi tồn kho qua dbo.usp_Stock_Post, theo thứ tự thời gian để không xuất âm ---------- */
CREATE TABLE #Mv (Seq INT IDENTITY(1,1) PRIMARY KEY, WarehouseType VARCHAR(15), MaterialId INT NULL, OrderLineId INT NULL,
                  DocType VARCHAR(30), DocId INT, LineId INT NULL, Qty DECIMAL(18,3), TxDate DATE);
-- Tồn đầu kỳ
INSERT #Mv (WarehouseType, MaterialId, DocType, DocId, Qty, TxDate)
SELECT 'MATERIAL', v.MaterialId, 'STOCKTAKE', (SELECT StocktakeId FROM dbo.Stocktake WHERE StocktakeCode = 'KK-DEMO-001'), v.Qty, '2026-09-01'
FROM (VALUES (@mKhoa, 800), (@mNhan, 800), (@mChi, 60), (@mChiXanh, 40), (@mKim, 30), (@mVaiXanh, 300), (@mVaiTrang, 150)) v(MaterialId, Qty);
-- Phiếu nhập NVL đã xác nhận
INSERT #Mv (WarehouseType, MaterialId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'MATERIAL', om.MaterialId, 'MATERIAL_RECEIPT', r.MaterialReceiptId, l.MaterialReceiptLineId, l.AcceptedQty, r.ReceiptDate
FROM dbo.MaterialReceipt r
JOIN dbo.MaterialReceiptLine l ON l.MaterialReceiptId = r.MaterialReceiptId
JOIN dbo.OrderMaterial om ON om.OrderMaterialId = l.OrderMaterialId
WHERE r.Status = 'CONFIRMED' AND l.AcceptedQty > 0 AND r.ReceiptCode IN ('PN-DEMO-001', 'PN-DEMO-002')
ORDER BY r.ReceiptDate, l.MaterialReceiptLineId;
-- Vải đã cấp và tổ cắt đã nhận
INSERT #Mv (WarehouseType, MaterialId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'MATERIAL', l.MaterialId, 'MATERIAL_ISSUE', i.MaterialIssueId, l.MaterialIssueLineId, -l.ConfirmedQty, i.IssueDate
FROM dbo.MaterialIssue i JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = i.MaterialIssueId
WHERE i.IssueCode = 'PC-DEMO-003';
-- BTP: bàn giao đã xác nhận (nhập), phiếu cấp BTP đã nhận (xuất)
INSERT #Mv (WarehouseType, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'SEMI_FINISHED', cl.OrderLineId, 'CUT_HANDOVER', h.CutHandoverId, cl.CutHandoverLineId, cl.ConfirmedQty, CAST(h.ConfirmedAt AS DATE)
FROM dbo.CutHandover h JOIN dbo.CutHandoverLine cl ON cl.CutHandoverId = h.CutHandoverId
WHERE h.Status = 'CONFIRMED' AND h.HandoverCode IN ('BG-DEMO-001', 'BG-DEMO-002')
ORDER BY h.ConfirmedAt, cl.CutHandoverLineId;
INSERT #Mv (WarehouseType, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'SEMI_FINISHED', l.OrderLineId, 'MATERIAL_ISSUE', i.MaterialIssueId, l.MaterialIssueLineId, -l.ConfirmedQty, i.IssueDate
FROM dbo.MaterialIssue i JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = i.MaterialIssueId
WHERE i.WarehouseType = 'SEMI_FINISHED' AND i.Status = 'RECEIVED' AND l.ProductComponentMaterialId IS NULL
ORDER BY i.IssueDate, l.MaterialIssueLineId;
-- Thành phẩm: nhập theo bàn giao, xuất theo phiếu đã xuất
INSERT #Mv (WarehouseType, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'FINISHED', fl.OrderLineId, 'FG_HANDOVER', h.FgHandoverId, fl.FgHandoverLineId, fl.ConfirmedQty, CAST(h.ConfirmedAt AS DATE)
FROM dbo.FgHandover h JOIN dbo.FgHandoverLine fl ON fl.FgHandoverId = h.FgHandoverId
WHERE h.Status = 'CONFIRMED'
ORDER BY h.ConfirmedAt, fl.FgHandoverLineId;
INSERT #Mv (WarehouseType, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'FINISHED', dl.OrderLineId, 'DISPATCH_NOTE', d.DispatchNoteId, dl.DispatchNoteLineId, -dl.IssueQty, CAST(d.DispatchedAt AS DATE)
FROM dbo.DispatchNote d JOIN dbo.DispatchNoteLine dl ON dl.DispatchNoteId = d.DispatchNoteId
WHERE d.Status = 'IN_DELIVERY' AND d.DispatchedAt IS NOT NULL;

CREATE TABLE #Posted (StockBalanceId INT, BalanceAfter DECIMAL(18,3));
DECLARE @seq INT = 0, @wh VARCHAR(15), @mid INT, @olid INT, @dt VARCHAR(30), @did INT, @lid INT, @qty DECIMAL(18,3), @tx DATE;
WHILE 1 = 1
BEGIN
    SELECT TOP (1) @seq = Seq, @wh = WarehouseType, @mid = MaterialId, @olid = OrderLineId, @dt = DocType, @did = DocId,
                   @lid = LineId, @qty = Qty, @tx = TxDate
    FROM #Mv WHERE Seq > @seq ORDER BY Seq;
    IF @@ROWCOUNT = 0 BREAK;
    INSERT #Posted EXEC dbo.usp_Stock_Post @WarehouseType = @wh, @MaterialId = @mid, @OrderLineId = @olid,
        @SourceDocType = @dt, @SourceDocId = @did, @SourceLineId = @lid, @QtyChange = @qty, @TransactionDate = @tx;
END;
DROP TABLE #Mv, #Posted;

-- Dòng của phiếu tồn đầu kỳ: số sổ 0, đếm ra đúng số đã ghi
INSERT dbo.StocktakeLine (StocktakeId, StockBalanceId, BookQty, CountedQty, Note)
SELECT s.StocktakeId, b.StockBalanceId, 0, t.QtyChange, N'Tồn đầu kỳ'
FROM dbo.Stocktake s
JOIN dbo.StockTransaction t ON t.SourceDocType = 'STOCKTAKE' AND t.SourceDocId = s.StocktakeId
JOIN dbo.StockBalance b ON b.StockBalanceId = t.StockBalanceId
WHERE s.StocktakeCode = 'KK-DEMO-001';

-- KK-DEMO-002: kiểm kê đang chờ Quản đốc / Admin duyệt (vải lệch -8 m, khóa kéo khớp sổ)
INSERT dbo.Stocktake (StocktakeCode, WarehouseType, Scope, StocktakeDate, Status, CreatedAt)
VALUES ('KK-DEMO-002', 'MATERIAL', N'Kiểm kê vải và khóa kéo cuối tháng', '2026-10-26', 'PENDING_APPROVAL', '2026-10-26 17:00:00');
INSERT dbo.StocktakeLine (StocktakeId, StockBalanceId, BookQty, CountedQty, Note)
SELECT s.StocktakeId, b.StockBalanceId, b.OnHandQty,
       CASE b.MaterialId WHEN @mVai THEN b.OnHandQty - 8 ELSE b.OnHandQty END,
       CASE b.MaterialId WHEN @mVai THEN N'Hụt vải khi cắt' END
FROM dbo.Stocktake s
JOIN dbo.StockBalance b ON b.WarehouseType = 'MATERIAL' AND b.MaterialId IN (@mVai, @mKhoa)
WHERE s.StocktakeCode = 'KK-DEMO-002';

COMMIT TRANSACTION;
GO

/* =====================================================================================
   19.4 Đơn DEMO-DH004: chưa có lệnh cắt nào, để Quản đốc tạo lệnh cắt mới trên giao diện
   ---------------------------------------------------------------------------------
   3 dòng đơn (Đen S 120, Xanh M 150, Xanh L 100). Vải trong kho (tồn đầu kỳ 19.3) đủ theo định mức:
   vải Xanh 300 m, vải Trắng 150 m, vải Đen còn lại sau các đơn demo khác.
   Chạy lại không nhân đôi dữ liệu.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH004')
BEGIN
    PRINT N'Dữ liệu demo 19.4 đã có (DEMO-DH004), bỏ qua.';
    RETURN;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.Product WHERE ProductCode = 'DEMO-AO01')
BEGIN
    PRINT N'Chưa có dữ liệu demo 19.1 (DEMO-AO01), bỏ qua 19.4.';
    RETURN;
END;

BEGIN TRANSACTION;

DECLARE @p1 INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-AO01'),
        @cus INT = (SELECT TOP 1 CustomerId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH001');

INSERT dbo.CustomerOrder (OrderCode, CustomerId, ReceivedDate, DueDate, MaterialStatus, Status, Note)
VALUES ('DEMO-DH004', @cus, '2026-10-26', '2026-11-30', 'PARTIAL', 'IN_PRODUCTION', N'Đơn demo chưa có lệnh cắt, để Quản đốc tạo lệnh cắt mới');
DECLARE @o4 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH004');

INSERT dbo.OrderLine (OrderId, ProductVariantId, ProductId, OrderedQty, CmtUnitPrice)
SELECT @o4, pv.ProductVariantId, pv.ProductId, v.Qty, 28000
FROM (VALUES (N'Đen', 'S', 120), (N'Xanh', 'M', 150), (N'Xanh', 'L', 100)) v(Color, SizeCode, Qty)
JOIN dbo.ProductColor pcol ON pcol.ProductId = @p1 AND pcol.Color = v.Color
JOIN dbo.ProductVariant pv ON pv.ProductColorId = pcol.ProductColorId AND pv.SizeCode = v.SizeCode;

-- NVL của đơn: số cần ≈ định mức × số đặt (vải Xanh thân, vải Trắng tay, vải Đen, phụ liệu), công ty tự mua
INSERT dbo.OrderMaterial (OrderId, MaterialId, RequiredQty, CustomerQty, ExpectedDeliveryCount)
SELECT @o4, m.MaterialId, v.Qty, 0, NULL
FROM (VALUES ('DEMO-VAI02', 260), ('DEMO-VAI03', 105), ('DEMO-VAI01', 175), ('DEMO-CHI02', 25), ('DEMO-CHI01', 15),
             ('DEMO-KHOA01', 370), ('DEMO-NHAN01', 370)) v(Code, Qty)
JOIN dbo.Material m ON m.MaterialCode = v.Code;

COMMIT TRANSACTION;
GO

/* =====================================================================================
   19.5 Thông báo mẫu của Quản đốc gửi tổ trưởng / công nhân (nghỉ lễ, sự kiện sắp tới)
   ---------------------------------------------------------------------------------
   Gửi tới toàn xưởng; mỗi người nhận một dòng Notification chưa đọc để thử khối "Thông báo mới" ở trang chủ.
   Chạy lại không nhân đôi dữ liệu.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.Announcement WHERE Content LIKE N'[[]DEMO]%')
BEGIN
    PRINT N'Dữ liệu demo 19.5 đã có, bỏ qua.';
    RETURN;
END;
DECLARE @sender INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC');
IF @sender IS NULL
BEGIN
    PRINT N'Chưa có tài khoản QUANDOC, bỏ qua 19.5.';
    RETURN;
END;

BEGIN TRANSACTION;

INSERT dbo.Announcement (Content, Priority, TargetScope, SenderId, SentAt) VALUES
 (N'[DEMO] Thông báo nghỉ lễ: xưởng nghỉ Tết Dương lịch thứ Sáu 01/01/2027, đi làm lại sáng thứ Bảy 02/01/2027. Tổ trưởng nhắc công nhân hoàn thành đơn đang chạy trước kỳ nghỉ.', 'URGENT', 'ALL', @sender, '2026-11-02 08:00:00'),
 (N'[DEMO] Sự kiện sắp tới: khám sức khỏe định kỳ sáng thứ Bảy 14/11 tại nhà ăn xưởng. Tổ trưởng báo danh sách người khám cho Quản đốc trước thứ Năm 12/11.', 'NORMAL', 'ALL', @sender, '2026-11-02 09:00:00');

INSERT dbo.Notification (RecipientId, SourceType, RefTable, RefId, Title, Message, LinkUrl)
SELECT e.EmployeeId, 'ANNOUNCEMENT', 'Announcement', a.AnnouncementId,
       CASE a.Priority WHEN 'URGENT' THEN N'Thông báo khẩn' ELSE N'Thông báo' END, a.Content, '/notifications'
FROM dbo.Announcement a
CROSS JOIN dbo.Employee e
WHERE a.Content LIKE N'[[]DEMO]%' AND e.Status = 'ACTIVE' AND e.EmployeeId <> @sender;

COMMIT TRANSACTION;
GO

-- Dữ liệu demo (DEV-ALL-49): tổ đã phân công, có phiếu cấp BTP, chỉ tiêu KPI hoặc đơn Xin BTP đang chờ thì được chia mọi màu × size của mã hàng đó.
INSERT dbo.OrderLineTeam (OrderLineId, SewingTeamId)
SELECT DISTINCT line.OrderLineId, s.TeamId
FROM (SELECT a.OrderId, o.ProductId, a.SewingTeamId AS TeamId
      FROM dbo.OperationAssignment a JOIN dbo.Operation o ON o.OperationId = a.OperationId
      WHERE a.ValidTo IS NULL
      UNION
      SELECT i.OrderId, ol.ProductId, i.ReceivingTeamId
      FROM dbo.MaterialIssue i
      JOIN dbo.MaterialIssueLine il ON il.MaterialIssueId = i.MaterialIssueId
      JOIN dbo.OrderLine ol ON ol.OrderLineId = il.OrderLineId
      WHERE i.WarehouseType = 'SEMI_FINISHED' AND i.Status <> 'REJECTED'
      UNION
      SELECT k.OrderId, k.ProductId, k.SewingTeamId
      FROM dbo.KpiTarget k
      WHERE k.Status <> 'CANCELLED'
      UNION
      SELECT ol.OrderId, ol.ProductId, r.CreatedByTeamId
      FROM dbo.Request r
      JOIN dbo.RequestLine rl ON rl.RequestId = r.RequestId
      JOIN dbo.OrderLine ol ON ol.OrderLineId = rl.OrderLineId
      WHERE r.RequestTypeCode = 'SEMI_FINISHED_REQUISITION' AND r.Status = 'PENDING_PROCESSING' AND r.CreatedByTeamId IS NOT NULL) s
JOIN dbo.OrderLine line ON line.OrderId = s.OrderId AND line.ProductId = s.ProductId;
GO

/* =====================================================================================
   19.6 Dữ liệu demo bổ sung (02/11/2026)
   ---------------------------------------------------------------------------------
   - Tổ may 2 (MAY2) mở ngày 27/10: tổ trưởng TTMAY2, công nhân CNMAY5–7; CNMAY8 chuyển từ tổ may 1 sang ngày 28/10;
     CNMOI1 mới vào làm, chưa xếp tổ (thử "Thêm thành viên"). Lịch sử tổ (AuditLog Team) ghi đúng câu backend ghi.
   - KPI theo ngày của chỉ tiêu DEMO-DH001 đang chạy (12/10–07/11), 25 sản phẩm / ngày.
   - Sản lượng 27–31/10 của đơn DEMO-DH003 theo số đã giao đã xác nhận.
   - Bảng công đến 31/10 cho nhân viên mới.
   Chạy lại không nhân đôi dữ liệu (bỏ qua nếu đã có tổ MAY2).
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.Team WHERE TeamCode = 'MAY2')
BEGIN
    PRINT N'Dữ liệu demo 19.6 đã có (MAY2), bỏ qua.';
    RETURN;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH003')
BEGIN
    PRINT N'Chưa có dữ liệu demo 19.3 (DEMO-DH003), bỏ qua 19.6.';
    RETURN;
END;

BEGIN TRANSACTION;

INSERT dbo.Team (TeamCode, TeamName, TeamType, CreatedAt) VALUES ('MAY2', N'Tổ may 2', 'SEWING', '2026-10-27 08:00:00');
DECLARE @may1 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY1'),
        @may2 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY2'),
        @quandoc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC'),
        @admin INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'ADMIN'),
        @rWorker INT = (SELECT RoleId FROM dbo.Role WHERE RoleCode = 'WORKER'),
        @rLeader INT = (SELECT RoleId FROM dbo.Role WHERE RoleCode = 'TEAM_LEADER');

-- TTMAY2 vào tổ là công nhân rồi mới được bổ nhiệm tổ trưởng (như thao tác trên màn tổ)
INSERT dbo.Employee (EmployeeCode, FullName, Gender, RoleId, TeamId, PayType, CurrentGradeCode, AttendanceCode, StartDate, CreatedAt)
SELECT v.Code, v.FullName, v.Gender, IIF(v.Code = 'TTMAY2', @rLeader, @rWorker), v.TeamId, 'PIECE_RATE', v.Grade, v.AttCode, v.StartDate, v.CreatedAt
FROM (VALUES ('TTMAY2', N'Trương Văn Bình', 'M', @may2, 'A', 107, '2026-10-26', '2026-10-26 08:00:00'),
             ('CNMAY5', N'Phạm Thị Hoa',    'F', @may2, 'B', 108, '2026-10-26', '2026-10-26 08:05:00'),
             ('CNMAY6', N'Đỗ Văn Nam',      'M', @may2, 'C', 109, '2026-10-26', '2026-10-26 08:10:00'),
             ('CNMAY7', N'Vũ Thị Thu',      'F', @may2, 'C', 110, '2026-10-26', '2026-10-26 08:15:00'),
             ('CNMAY8', N'Hoàng Văn Long',  'M', @may2, 'B', 111, '2026-10-01', '2026-10-01 08:00:00'),
             ('CNMOI1', N'Bùi Thị Ngọc',    'F', NULL,  NULL, 112, '2026-11-02', '2026-11-02 08:00:00'))
     v(Code, FullName, Gender, TeamId, Grade, AttCode, StartDate, CreatedAt);
INSERT dbo.Account (EmployeeId, PasswordHash, IsTemporaryPassword)
SELECT e.EmployeeId, N'__SET_BY_BACKEND_SEED__', 1
FROM dbo.Employee e
WHERE e.EmployeeCode IN ('TTMAY2', 'CNMAY5', 'CNMAY6', 'CNMAY7', 'CNMAY8', 'CNMOI1');

DECLARE @tt2 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTMAY2'),
        @w8 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY8');

-- Lịch sử tổ: câu Summary giống TeamService (tạo tổ, thêm thành viên, bổ nhiệm, chuyển tổ)
INSERT dbo.AuditLog (TableName, RecordId, Action, OldValues, NewValues, Reason, Summary, ChangedBy, ChangedAt) VALUES
 ('Team', @may2, 'INSERT', NULL, N'{"TeamCode":"MAY2","TeamName":"Tổ may 2","TeamType":"SEWING","Status":"ACTIVE"}', NULL, NULL, @admin, '2026-10-27 08:00:00'),
 ('Team', @may2, 'UPDATE', NULL, NULL, NULL, N'Thêm vào tổ: Trương Văn Bình, Phạm Thị Hoa, Đỗ Văn Nam, Vũ Thị Thu. Mở tổ may 2 cho đơn quần short', @quandoc, '2026-10-27 08:10:00'),
 ('Team', @may2, 'UPDATE', NULL, NULL, NULL, N'Bổ nhiệm Trương Văn Bình làm tổ trưởng', @quandoc, '2026-10-27 08:15:00'),
 ('Team', @may2, 'UPDATE', NULL, NULL, NULL, N'Nhận chuyển đến: Hoàng Văn Long (hiệu lực 28/10/2026)', @quandoc, '2026-10-28 07:45:00'),
 ('Team', @may1, 'UPDATE', NULL, NULL, NULL, N'Chuyển sang tổ Tổ may 2: Hoàng Văn Long (hiệu lực 28/10/2026)', @quandoc, '2026-10-28 07:45:00'),
 ('Employee', @tt2, 'UPDATE', N'{"RoleCode":"WORKER"}', N'{"RoleCode":"TEAM_LEADER"}', N'Bổ nhiệm tổ trưởng tổ Tổ may 2', NULL, @quandoc, '2026-10-27 08:15:00'),
 ('Employee', @w8, 'UPDATE', CONCAT(N'{"TeamId":', @may1, N'}'), CONCAT(N'{"TeamId":', @may2, N'}'), N'Chuyển tổ hiệu lực 28/10/2026', NULL, @quandoc, '2026-10-28 07:45:00');

-- Bảng công: CNMAY8 từ 01/10, tổ may 2 từ 27/10 (Chủ nhật nghỉ)
;WITH d AS (SELECT CAST('2026-10-01' AS DATE) AS D UNION ALL SELECT DATEADD(DAY, 1, D) FROM d WHERE D < '2026-10-31')
INSERT dbo.DailyTimesheet (EmployeeId, WorkDate, CheckIn, CheckOut, WorkedHours, WorkdayValue, OvertimeHours, IsMissingPunch)
SELECT e.EmployeeId, d.D,
       DATEADD(MINUTE, 7 * 60 + 20 + ABS(CHECKSUM(e.EmployeeId, d.D)) % 10, CAST(d.D AS DATETIME2(0))),
       DATEADD(MINUTE, 16 * 60 + 30, CAST(d.D AS DATETIME2(0))), 8, 1, 0, 0
FROM dbo.Employee e
CROSS JOIN d
WHERE e.EmployeeCode IN ('TTMAY2', 'CNMAY5', 'CNMAY6', 'CNMAY7', 'CNMAY8')
  AND DATEDIFF(DAY, 0, d.D) % 7 <> 6
  AND d.D >= IIF(e.EmployeeCode = 'CNMAY8', '2026-10-01', '2026-10-27');

-- KPI theo ngày của chỉ tiêu DEMO-DH001 đang chạy: 24 ngày làm (trừ Chủ nhật) × 25 = 600
DECLARE @kpi INT = (SELECT k.KpiTargetId FROM dbo.KpiTarget k JOIN dbo.CustomerOrder o ON o.OrderId = k.OrderId
                    WHERE o.OrderCode = 'DEMO-DH001' AND k.Status = 'ACTIVE');
;WITH d AS (SELECT CAST('2026-10-12' AS DATE) AS D UNION ALL SELECT DATEADD(DAY, 1, D) FROM d WHERE D < '2026-11-07')
INSERT dbo.KpiDailyTarget (KpiTargetId, WorkDate, TargetQty)
SELECT @kpi, d.D, 25 FROM d WHERE DATEDIFF(DAY, 0, d.D) % 7 <> 6;

-- Sản lượng DEMO-DH003 27–31/10 theo số đã giao đã xác nhận (CNMAY1 còn chờ thu hồi 5 lần nên chỉ báo 25/30)
DECLARE @o3 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH003');
;WITH d AS (SELECT CAST('2026-10-27' AS DATE) AS D UNION ALL SELECT DATEADD(DAY, 1, D) FROM d WHERE D < '2026-10-31')
INSERT dbo.OutputRecord (WorkDate, ShiftCode, SewingTeamId, WorkerId, OperationId, OrderLineId, Quantity, PieceRateId,
                         IsOutsideAssignment, NoAttendanceWarning, EditDeadline, Status)
SELECT d.D, 'AFTERNOON', a.SewingTeamId, a.WorkerId, a.OperationId, a.OrderLineId, a.ConfirmedQty / 5 - IIF(a.ConfirmedQty = 30, 1, 0),
       pr.PieceRateId, 0, 0, dbo.fn_NextWorkingDayEnd(d.D), 'SAVED'
FROM dbo.WorkAllocation a
JOIN dbo.OrderLine ol ON ol.OrderLineId = a.OrderLineId AND ol.OrderId = @o3
CROSS JOIN d
CROSS APPLY (SELECT TOP (1) PieceRateId FROM dbo.PieceRate
             WHERE OperationId = a.OperationId AND Status = 'APPROVED' AND EffectiveDate <= d.D
             ORDER BY EffectiveDate DESC) pr
WHERE a.MovementType = 'HANDOVER' AND a.Status = 'CONFIRMED';

COMMIT TRANSACTION;
GO

/* =====================================================================================
   19.7 Dữ liệu demo để test đủ các chức năng (02/11/2026)
   ---------------------------------------------------------------------------------
   Bù các loại và trạng thái mà 19.1–19.6 chưa có, xem bảng tra trong docs/DEMO_SCRIPT.md:
   - Danh mục: khách hàng, NVL, mã hàng ngừng dùng; mã hàng chưa khai công đoạn; tổ ngừng; nhân viên đã nghỉ, tài khoản bị khóa;
     thêm 12 công nhân tổ may 2 (thử danh sách dài ở Phân công công đoạn).
   - Đơn hàng DEMO-DH005…013 ở mọi trạng thái; đơn đã giao có đủ chuỗi cắt → may → QC → kho → xuất, tồn kho ghi qua usp_Stock_Post.
   - Đủ 11 loại đơn từ và 6 trạng thái; phiếu nhập, cấp, trả, bàn giao, xuất, kiểm kê ở các trạng thái còn thiếu.
   - Chấm công (lượt quẹt, phiên đồng bộ, chấm hộ), suất ăn, bậc thợ, bảng lương, khóa tháng, chi phí, hòa vốn, thông báo.
   Chỉ thêm dữ liệu, không xóa. Chạy lại không nhân đôi (bỏ qua nếu đã có đơn DEMO-DH005).
   Ảnh và file đính kèm không có ở đây: Attachment, ProductImage cần file thật, tải lên trên giao diện.
   Tài khoản mới dùng placeholder mật khẩu: cần khởi động lại backend để băm mật khẩu 123456.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH005')
BEGIN
    PRINT N'Dữ liệu demo 19.7 đã có (DEMO-DH005), bỏ qua.';
    RETURN;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.Team WHERE TeamCode = 'MAY2')
BEGIN
    PRINT N'Chưa có dữ liệu demo 19.6 (MAY2), bỏ qua 19.7.';
    RETURN;
END;

BEGIN TRANSACTION;

DECLARE @cat INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'CAT'),
        @may1 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY1'),
        @may2 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY2'),
        @admin INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'ADMIN'),
        @quandoc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QUANDOC'),
        @thukho INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'THUKHO'),
        @ttcat INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTCAT'),
        @ttmay INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTMAY'),
        @ttmay2 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'TTMAY2'),
        @qaqc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'QAQC1'),
        @codien INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CODIEN'),
        @w1 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY1'),
        @w2 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY2'),
        @w3 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY3'),
        @w4 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY4'),
        @w5 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY5'),
        @w6 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY6'),
        @w7 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY7'),
        @w8 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY8'),
        @rWorker INT = (SELECT RoleId FROM dbo.Role WHERE RoleCode = 'WORKER'),
        @rDeputy INT = (SELECT RoleId FROM dbo.Role WHERE RoleCode = 'TEAM_DEPUTY'),
        @rLeader INT = (SELECT RoleId FROM dbo.Role WHERE RoleCode = 'TEAM_LEADER'),
        @pAo INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-AO01'),
        @pQu INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-QU02'),
        @cusA INT = (SELECT CustomerId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH001'),
        @o1 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH001'),
        @o2 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH002'),
        @o3 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH003'),
        @wo2 INT = (SELECT WorkOrderId FROM dbo.WorkOrder WHERE WorkOrderCode = 'LC-DEMO-002'),
        @sup1 INT = (SELECT SupplierId FROM dbo.Supplier WHERE SupplierName LIKE N'Công ty Vải Sợi Thành Công%'),
        @sup3 INT = (SELECT SupplierId FROM dbo.Supplier WHERE SupplierName LIKE N'Cửa hàng Kim máy Minh Phát%'),
        @mVaiDen INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI01'),
        @mVaiXanh INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI02'),
        @mVaiTrang INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI03'),
        @mVaiBe INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI04'),
        @mChiBe INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-CHI03'),
        @mKhoa INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-KHOA01'),
        @mNhan INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-NHAN01'),
        @mKim INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-KIM01');

/* ---------- 1. Danh mục: đủ trạng thái đang dùng / ngừng dùng ---------- */
INSERT dbo.Customer (CustomerName, Phone, Email, DefaultAddress, Status, DiscontinueReason) VALUES
 (N'Công ty B (DEMO)', '0281234567', 'lienhe@congtyb.demo', N'12 Lê Lợi, Quận 1, TP.HCM', 'ACTIVE', NULL),
 (N'Công ty C (DEMO - ngừng)', '0287654321', 'lienhe@congtyc.demo', N'45 Hai Bà Trưng, Quận 3, TP.HCM', 'DISCONTINUED', N'Khách đã ngừng đặt hàng từ tháng 9');
DECLARE @cusB INT = (SELECT CustomerId FROM dbo.Customer WHERE CustomerName = N'Công ty B (DEMO)');

INSERT dbo.Material (MaterialCode, MaterialName, Color, Category, Unit, Status) VALUES
 ('DEMO-CUC01', N'Cúc nhựa 4 lỗ DEMO', N'Trắng', 'BUTTON', N'cái', 'ACTIVE'),
 ('DEMO-LOT01', N'Vải lót lưới DEMO', N'Đen', 'LINING', N'm', 'ACTIVE'),
 ('DEMO-NGUNG01', N'Mex dựng cũ DEMO', N'', 'INTERLINING', N'm', 'DISCONTINUED');

-- DEMO-SM03: có màu – size nhưng chưa khai định mức và công đoạn (thử cảnh báo chưa khai); DEMO-NGUNG04: mã hàng ngừng sản xuất
INSERT dbo.Product (ProductCode, ProductName, Description, Status) VALUES
 ('DEMO-SM03', N'Sơ mi DEMO', N'Mã hàng mới, chưa khai định mức và công đoạn', 'ACTIVE'),
 ('DEMO-NGUNG04', N'Áo thun cũ DEMO', N'Đã ngừng sản xuất', 'DISCONTINUED');
DECLARE @pSm INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-SM03');
INSERT dbo.ProductColor (ProductId, Color) VALUES (@pSm, N'Trắng');
INSERT dbo.ProductVariant (ProductId, ProductColorId, SizeCode)
SELECT @pSm, ProductColorId, s.SizeCode FROM dbo.ProductColor CROSS JOIN (VALUES ('M'), ('L')) s (SizeCode)
WHERE ProductId = @pSm AND Color = N'Trắng';

INSERT dbo.GradeLevelCode (GradeCode, GradeName, Status) VALUES ('E', N'Bậc E – Thử việc (ngừng dùng)', 'INACTIVE');
INSERT dbo.GradeLevel (GradeCode, MonthlyAmount, EffectiveDate) VALUES ('E', 0, '2026-01-01');

INSERT dbo.Team (TeamCode, TeamName, TeamType, Status, CreatedAt) VALUES ('MAY3', N'Tổ may 3 (ngừng)', 'SEWING', 'DISCONTINUED', '2026-06-01 08:00:00');
INSERT dbo.Vehicle (PlateNumber, VehicleType, FuelNormPer100Km, Status) VALUES ('51E-000.00', N'Xe tải cũ', 14, 'DISCONTINUED');

-- Nhân viên: tổ phó, công nhân cắt, người đã nghỉ, tài khoản bị khóa, và công nhân tổ may 2 để danh sách đủ dài
INSERT dbo.Employee (EmployeeCode, FullName, Gender, RoleId, TeamId, PayType, CurrentGradeCode, AttendanceCode, StartDate, ResignDate, Status)
SELECT v.Code, v.FullName, v.Gender, v.RoleId, v.TeamId, v.PayType, v.Grade, v.AttCode, v.StartDate, v.ResignDate, v.Status
FROM (VALUES
 ('TPCAT',   N'Tổ phó tổ cắt',      'M', @rDeputy, @cat,  'MONTHLY',    NULL, 113, '2026-01-05', NULL, 'ACTIVE'),
 ('CNCAT2',  N'Đặng Văn Tài',       'M', @rWorker, @cat,  'DAILY',      NULL, 114, '2026-03-02', NULL, 'ACTIVE'),
 ('TPMAY2',  N'Ngô Thị Hạnh',       'F', @rDeputy, @may2, 'PIECE_RATE', 'B',  115, '2026-10-26', NULL, 'ACTIVE'),
 ('NVNGHI1', N'Lý Văn Sơn',         'M', @rWorker, NULL,  'PIECE_RATE', 'C',  116, '2025-06-01', '2026-09-30', 'RESIGNED'),
 ('NVKHOA1', N'Tạ Thị Yến',         'F', @rWorker, @may1, 'PIECE_RATE', 'C',  117, '2026-02-02', NULL, 'ACTIVE'),
 ('CNMAY9',  N'Dương Thị Ánh',      'F', @rWorker, @may2, 'PIECE_RATE', 'B',  118, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY10', N'Hồ Văn Khoa',        'M', @rWorker, @may2, 'PIECE_RATE', 'C',  119, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY11', N'Mai Thị Dung',       'F', @rWorker, @may2, 'PIECE_RATE', 'A',  120, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY12', N'Cao Văn Phúc',       'M', @rWorker, @may2, 'PIECE_RATE', 'C',  121, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY13', N'Đinh Thị Hiền',      'F', @rWorker, @may2, 'PIECE_RATE', 'B',  122, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY14', N'Tô Văn Đức',         'M', @rWorker, @may2, 'PIECE_RATE', 'C',  123, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY15', N'La Thị Nga',         'F', @rWorker, @may2, 'PIECE_RATE', 'B',  124, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY16', N'Kiều Văn Tín',       'M', @rWorker, @may2, 'PIECE_RATE', 'D',  125, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY17', N'Phan Thị Trang',     'F', @rWorker, @may2, 'PIECE_RATE', 'C',  126, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY18', N'Lưu Văn Quân',       'M', @rWorker, @may2, 'PIECE_RATE', 'B',  127, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY19', N'Trịnh Thị Oanh',     'F', @rWorker, @may2, 'PIECE_RATE', 'C',  128, '2026-10-26', NULL, 'ACTIVE'),
 ('CNMAY20', N'Vương Văn Hải',      'M', @rWorker, @may2, 'PIECE_RATE', 'A',  129, '2026-10-26', NULL, 'ACTIVE'))
 v(Code, FullName, Gender, RoleId, TeamId, PayType, Grade, AttCode, StartDate, ResignDate, Status);
INSERT dbo.Account (EmployeeId, PasswordHash, IsTemporaryPassword, Status, LockedAt, LockReason)
SELECT e.EmployeeId, N'__SET_BY_BACKEND_SEED__', 1,
       IIF(e.EmployeeCode IN ('NVKHOA1', 'NVNGHI1'), 'LOCKED', 'ACTIVE'),
       IIF(e.EmployeeCode IN ('NVKHOA1', 'NVNGHI1'), CAST('2026-10-30 09:00:00' AS DATETIME2(0)), NULL),
       CASE e.EmployeeCode WHEN 'NVKHOA1' THEN N'Admin tạm khóa để thử chức năng mở khóa' WHEN 'NVNGHI1' THEN N'Nhân viên đã nghỉ việc' END
FROM dbo.Employee e
WHERE NOT EXISTS (SELECT 1 FROM dbo.Account a WHERE a.EmployeeId = e.EmployeeId);

/* ---------- 2. Đơn hàng ở mọi trạng thái ---------- */
INSERT dbo.CustomerOrder (OrderCode, CustomerId, ReceivedDate, DueDate, MaterialStatus, Status, StopReason, DelayRiskSince, Note, CreatedAt) VALUES
 ('DEMO-DH005', @cusB, '2026-10-30', '2026-12-15', 'AWAITING', 'NEW', NULL, NULL, N'Chờ khai báo NVL; mã hàng chưa có công đoạn', '2026-10-30 08:00:00'),
 ('DEMO-DH006', @cusB, '2026-10-28', '2026-12-10', 'COMPLETE', 'NEW', NULL, NULL, N'Khách đã cấp đủ NVL, sẵn sàng tạo lệnh cắt', '2026-10-28 08:00:00'),
 ('DEMO-DH007', @cusA, '2026-10-10', '2026-11-25', 'AWAITING', 'CANCELLED', NULL, NULL, N'Khách hủy đơn trước khi sản xuất', '2026-10-10 08:00:00'),
 ('DEMO-DH008', @cusB, '2026-10-12', '2026-12-01', 'COMPLETE', 'STOPPED', N'Khách tạm dừng để đổi mẫu', NULL, N'Đơn đang dừng', '2026-10-12 08:00:00'),
 ('DEMO-DH009', @cusB, '2026-10-20', '2026-11-06', 'COMPLETE', 'IN_DELIVERY', NULL, NULL, N'Đang giao, chờ Kế toán xác nhận khách nhận', '2026-10-20 08:00:00'),
 ('DEMO-DH010', @cusA, '2026-10-20', '2026-11-05', 'COMPLETE', 'PARTIALLY_DELIVERED', NULL, '2026-11-01', N'Đã giao một phần, còn hàng trong kho thành phẩm', '2026-10-20 08:30:00'),
 ('DEMO-DH011', @cusB, '2026-10-20', '2026-11-10', 'COMPLETE', 'DELIVERED', NULL, NULL, N'Đã giao đủ', '2026-10-20 09:00:00'),
 ('DEMO-DH012', @cusA, '2026-10-20', '2026-11-10', 'COMPLETE', 'COMPLETED', NULL, NULL, N'Đã hoàn tất', '2026-10-20 09:30:00'),
 ('DEMO-DH013', @cusB, '2026-10-25', '2026-11-25', 'PARTIAL', 'NEW', NULL, NULL, N'Nhà cung cấp giao thiếu vải, chờ Quản đốc quyết định', '2026-10-25 08:00:00');

INSERT dbo.OrderLine (OrderId, ProductVariantId, ProductId, OrderedQty, CmtUnitPrice)
SELECT o.OrderId, pv.ProductVariantId, pv.ProductId, v.Qty, v.Price
FROM (VALUES
 ('DEMO-DH005', 'DEMO-SM03', N'Trắng', 'M', 200, 22000), ('DEMO-DH005', 'DEMO-SM03', N'Trắng', 'L', 150, 22000),
 ('DEMO-DH006', 'DEMO-QU02', N'Be', 'M', 100, 18000),    ('DEMO-DH006', 'DEMO-QU02', N'Be', 'L', 100, 18000),
 ('DEMO-DH007', 'DEMO-AO01', N'Đen', 'M', 50, 28000),
 ('DEMO-DH008', 'DEMO-AO01', N'Xanh', 'L', 80, 28000),
 ('DEMO-DH009', 'DEMO-QU02', N'Be', 'M', 60, 18000),
 ('DEMO-DH010', 'DEMO-QU02', N'Be', 'M', 100, 18000),    ('DEMO-DH010', 'DEMO-QU02', N'Be', 'L', 60, 18000),
 ('DEMO-DH011', 'DEMO-QU02', N'Be', 'M', 50, 18000),     ('DEMO-DH011', 'DEMO-QU02', N'Be', 'L', 50, 18000),
 ('DEMO-DH012', 'DEMO-QU02', N'Be', 'M', 40, 18000),
 ('DEMO-DH013', 'DEMO-QU02', N'Be', 'M', 50, 18000),     ('DEMO-DH013', 'DEMO-QU02', N'Be', 'L', 50, 18000))
 v(OrderCode, ProductCode, Color, SizeCode, Qty, Price)
JOIN dbo.CustomerOrder o ON o.OrderCode = v.OrderCode
JOIN dbo.Product p ON p.ProductCode = v.ProductCode
JOIN dbo.ProductColor pcol ON pcol.ProductId = p.ProductId AND pcol.Color = v.Color
JOIN dbo.ProductVariant pv ON pv.ProductColorId = pcol.ProductColorId AND pv.SizeCode = v.SizeCode;

-- DEMO-DH005 đã chia cho tổ may 2: màn Phân công báo mã hàng chưa khai công đoạn
INSERT dbo.OrderLineTeam (OrderLineId, SewingTeamId)
SELECT l.OrderLineId, @may2 FROM dbo.OrderLine l JOIN dbo.CustomerOrder o ON o.OrderId = l.OrderId WHERE o.OrderCode = 'DEMO-DH005';

-- NVL của đơn: DH006 và các đơn đã giao do khách cấp; DH008, DH013 mua ngoài
INSERT dbo.OrderMaterial (OrderId, MaterialId, RequiredQty, CustomerQty, ExpectedDeliveryCount)
SELECT o.OrderId, @mVaiBe, q.Qty, q.Qty, 1
FROM dbo.CustomerOrder o
CROSS APPLY (SELECT SUM(l.OrderedQty) Qty FROM dbo.OrderLine l WHERE l.OrderId = o.OrderId) q
WHERE o.OrderCode IN ('DEMO-DH006', 'DEMO-DH009', 'DEMO-DH010', 'DEMO-DH011', 'DEMO-DH012');
INSERT dbo.OrderMaterial (OrderId, MaterialId, RequiredQty, CustomerQty, ExpectedDeliveryCount)
SELECT OrderId, @mChiBe, 20, 20, 1 FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH006';
INSERT dbo.OrderMaterial (OrderId, MaterialId, RequiredQty, CustomerQty)
SELECT OrderId, @mVaiXanh, 100, 0 FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH008';
INSERT dbo.OrderMaterial (OrderId, MaterialId, RequiredQty, CustomerQty)
SELECT OrderId, @mVaiBe, 100, 0 FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH013';

-- Phiếu nhập NVL khách cấp đã xác nhận
INSERT dbo.MaterialReceipt (ReceiptCode, OrderId, ReceiptDate, Status, Note, SubmittedAt, ConfirmedBy, ConfirmedAt)
SELECT 'PN-DEMO-' + RIGHT(o.OrderCode, 3), o.OrderId, '2026-10-24', 'CONFIRMED', N'Khách cấp đủ một đợt', '2026-10-24 09:00:00', @quandoc, '2026-10-24 10:00:00'
FROM dbo.CustomerOrder o WHERE o.OrderCode IN ('DEMO-DH006', 'DEMO-DH009', 'DEMO-DH010', 'DEMO-DH011', 'DEMO-DH012');
INSERT dbo.MaterialReceiptLine (MaterialReceiptId, OrderMaterialId, AcceptedQty)
SELECT r.MaterialReceiptId, m.OrderMaterialId, m.CustomerQty
FROM dbo.MaterialReceipt r JOIN dbo.OrderMaterial m ON m.OrderId = r.OrderId
WHERE r.ReceiptCode IN ('PN-DEMO-006', 'PN-DEMO-009', 'PN-DEMO-010', 'PN-DEMO-011', 'PN-DEMO-012');

/* ---------- 3. Đơn đã giao DH009…012: đủ chuỗi cắt → may → QC → nhập kho → xuất (dùng tổ may 2, công nhân CNMAY5) ---------- */
-- Công đoạn cuối cũ của DEMO-QU02 đã ngừng dùng: đặt công đoạn đang dùng có thứ tự lớn nhất làm công đoạn cuối (Kết thúc đơn cần có)
DECLARE @opFinal INT = (SELECT TOP (1) OperationId FROM dbo.Operation WHERE ProductId = @pQu AND Status = 'ACTIVE' ORDER BY StationSequence DESC, OperationId DESC);
UPDATE dbo.Operation SET IsFinalStep = 0 WHERE ProductId = @pQu AND IsFinalStep = 1 AND OperationId <> @opFinal;
UPDATE dbo.Operation SET IsFinalStep = 1 WHERE OperationId = @opFinal;
-- Cần đơn giá đã duyệt thì mới ghi được sản lượng
IF NOT EXISTS (SELECT 1 FROM dbo.PieceRate WHERE OperationId = @opFinal AND Status = 'APPROVED' AND EffectiveDate <= '2026-10-29')
    INSERT dbo.PieceRate (OperationId, Rate, EffectiveDate, Status, SubmittedAt, ApprovedAt)
    VALUES (@opFinal, 1500, '2026-10-01', 'APPROVED', '2026-09-28 09:00:00', '2026-09-29 09:00:00');
DECLARE @rateFinal INT = (SELECT TOP (1) PieceRateId FROM dbo.PieceRate
                          WHERE OperationId = @opFinal AND Status = 'APPROVED' AND EffectiveDate <= '2026-10-29' ORDER BY EffectiveDate DESC);

SELECT o.OrderId, o.OrderCode, RIGHT(o.OrderCode, 3) AS N INTO #Ch
FROM dbo.CustomerOrder o WHERE o.OrderCode IN ('DEMO-DH009', 'DEMO-DH010', 'DEMO-DH011', 'DEMO-DH012');

INSERT dbo.WorkOrder (WorkOrderCode, OrderId, ProductId, CuttingTeamId, DueDate, Status, Note, CreatedAt, CompletedAt)
SELECT 'LC-DEMO-' + c.N, c.OrderId, @pQu, @cat, '2026-10-27', 'COMPLETED', N'Lệnh cắt đã hoàn thành', '2026-10-25 08:00:00', '2026-10-27 16:00:00' FROM #Ch c;
INSERT dbo.WorkOrderLine (WorkOrderId, OrderLineId, PlannedQty)
SELECT w.WorkOrderId, l.OrderLineId, l.OrderedQty
FROM #Ch c JOIN dbo.WorkOrder w ON w.WorkOrderCode = 'LC-DEMO-' + c.N JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;

-- Cấp vải cho tổ cắt (1 m / sản phẩm)
INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, WorkOrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, ReceivedBy, ReceivedAt, CreatedAt)
SELECT 'PC-DEMO-1' + c.N, 'MATERIAL', c.OrderId, w.WorkOrderId, @cat, '2026-10-25', @thukho, 'RECEIVED', @ttcat, '2026-10-25 10:00:00', '2026-10-25 09:00:00'
FROM #Ch c JOIN dbo.WorkOrder w ON w.WorkOrderCode = 'LC-DEMO-' + c.N;
INSERT dbo.MaterialIssueLine (MaterialIssueId, MaterialId, IssuedQty, ConfirmedQty)
SELECT i.MaterialIssueId, @mVaiBe, m.RequiredQty, m.RequiredQty
FROM #Ch c JOIN dbo.MaterialIssue i ON i.IssueCode = 'PC-DEMO-1' + c.N JOIN dbo.OrderMaterial m ON m.OrderId = c.OrderId;

-- Bàn cắt: 10 lớp × tỉ lệ (số lượng dòng / 10)
INSERT dbo.CuttingLay (WorkOrderId, CuttingTeamId, WorkDate, ShiftCode, LaySequence, FabricMaterialId, PlyCount, LayLengthM, Status, CreatedAt)
SELECT w.WorkOrderId, @cat, '2026-10-26', 'AFTERNOON', CAST(c.N AS SMALLINT), @mVaiBe, 10, 2.0, 'RECORDED', '2026-10-26 15:00:00'
FROM #Ch c JOIN dbo.WorkOrder w ON w.WorkOrderCode = 'LC-DEMO-' + c.N;
INSERT dbo.CuttingLayLine (CuttingLayId, OrderLineId, RatioValue, CalculatedQty, ActualQty)
SELECT y.CuttingLayId, l.OrderLineId, l.OrderedQty / 10, l.OrderedQty, l.OrderedQty
FROM #Ch c JOIN dbo.WorkOrder w ON w.WorkOrderCode = 'LC-DEMO-' + c.N
JOIN dbo.CuttingLay y ON y.WorkOrderId = w.WorkOrderId JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;

INSERT dbo.CutHandover (HandoverCode, WorkOrderId, HandedAt, Status, ConfirmedBy, ConfirmedAt)
SELECT 'BG-DEMO-' + c.N, w.WorkOrderId, '2026-10-27 15:00:00', 'CONFIRMED', @thukho, '2026-10-27 16:00:00'
FROM #Ch c JOIN dbo.WorkOrder w ON w.WorkOrderCode = 'LC-DEMO-' + c.N;
INSERT dbo.CutHandoverLine (CutHandoverId, OrderLineId, HandedQty, ConfirmedQty)
SELECT h.CutHandoverId, l.OrderLineId, l.OrderedQty, l.OrderedQty
FROM #Ch c JOIN dbo.CutHandover h ON h.HandoverCode = 'BG-DEMO-' + c.N JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;

-- Cấp BTP cho tổ may 2
INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, ReceivedBy, ReceivedAt, CreatedAt)
SELECT 'PC-DEMO-2' + c.N, 'SEMI_FINISHED', c.OrderId, @may2, '2026-10-28', @thukho, 'RECEIVED', @ttmay2, '2026-10-28 08:00:00', '2026-10-28 07:30:00' FROM #Ch c;
INSERT dbo.MaterialIssueLine (MaterialIssueId, OrderLineId, IssuedQty, ConfirmedQty)
SELECT i.MaterialIssueId, l.OrderLineId, l.OrderedQty, l.OrderedQty
FROM #Ch c JOIN dbo.MaterialIssue i ON i.IssueCode = 'PC-DEMO-2' + c.N JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;

INSERT dbo.OrderLineTeam (OrderLineId, SewingTeamId)
SELECT l.OrderLineId, @may2 FROM #Ch c JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;
INSERT dbo.OperationAssignment (SewingTeamId, OrderId, OperationId, WorkerId, ValidFrom, ValidTo, AssignedAt)
SELECT @may2, c.OrderId, @opFinal, @w5, '2026-10-28', '2026-10-31', '2026-10-28 08:00:00' FROM #Ch c;
INSERT dbo.WorkAllocation (MovementType, SewingTeamId, WorkerId, OrderLineId, OperationId, Quantity, ConfirmedQty, ConfirmMode, Status, CreatedAt, ConfirmedAt)
SELECT 'HANDOVER', @may2, @w5, l.OrderLineId, @opFinal, l.OrderedQty, l.OrderedQty, 'WORKER', 'CONFIRMED', '2026-10-28 08:10:00', '2026-10-28 08:30:00'
FROM #Ch c JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;
INSERT dbo.OutputRecord (WorkDate, ShiftCode, SewingTeamId, WorkerId, OperationId, OrderLineId, Quantity, PieceRateId, IsOutsideAssignment, NoAttendanceWarning, EditDeadline, Status)
SELECT '2026-10-29', 'AFTERNOON', @may2, @w5, @opFinal, l.OrderLineId, l.OrderedQty, @rateFinal, 0, 0, dbo.fn_NextWorkingDayEnd('2026-10-29'), 'SAVED'
FROM #Ch c JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;
INSERT dbo.QcBatch (WorkDate, ShiftCode, BatchTime, SewingTeamId, OrderLineId, FinishedQty, InspectorId, EditDeadline)
SELECT '2026-10-30', 'MORNING', '09:00:00', @may2, l.OrderLineId, l.OrderedQty, @qaqc, dbo.fn_NextWorkingDayEnd('2026-10-30')
FROM #Ch c JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;
INSERT dbo.FgHandover (HandoverCode, OrderId, ProductId, InspectorId, HandedAt, Status, ConfirmedBy, ConfirmedAt)
SELECT 'TP-DEMO-' + c.N, c.OrderId, @pQu, @qaqc, '2026-10-30 14:00:00', 'CONFIRMED', @thukho, '2026-10-30 15:00:00' FROM #Ch c;
INSERT dbo.FgHandoverLine (FgHandoverId, OrderLineId, HandedQty, ConfirmedQty)
SELECT h.FgHandoverId, l.OrderLineId, l.OrderedQty, l.OrderedQty
FROM #Ch c JOIN dbo.FgHandover h ON h.HandoverCode = 'TP-DEMO-' + c.N JOIN dbo.OrderLine l ON l.OrderId = c.OrderId;

-- Phiếu xuất đã xuất kho: DH009 đang giao; DH010 đã giao đợt 1 (còn hàng chưa giao); DH011, DH012 giao đủ
INSERT dbo.DispatchNote (DispatchCode, OrderId, DeliveryDate, DeliveryAddress, Note, Status, CreatedAt, DispatchedBy, DispatchedAt, CustomerReceivedDate)
SELECT 'XH-DEMO-' + c.N, c.OrderId, v.DeliveryDate, N'Kho khách hàng, KCN Tân Bình, TP.HCM', v.Note, v.Status, '2026-10-30 16:00:00', @thukho, '2026-10-31 08:00:00', v.ReceivedDate
FROM #Ch c
JOIN (VALUES ('009', CAST('2026-11-03' AS DATE), N'Đang trên đường giao', 'IN_DELIVERY', CAST(NULL AS DATE)),
             ('010', '2026-10-31', N'Giao đợt 1', 'DELIVERED', '2026-10-31'),
             ('011', '2026-10-31', NULL, 'DELIVERED', '2026-10-31'),
             ('012', '2026-10-31', NULL, 'DELIVERED', '2026-10-31')) v(N, DeliveryDate, Note, Status, ReceivedDate) ON v.N = c.N;
INSERT dbo.DispatchNoteLine (DispatchNoteId, OrderLineId, IssueQty)
SELECT d.DispatchNoteId, l.OrderLineId,
       IIF(c.N = '010', IIF(pv.SizeCode = 'M', 60, 40), l.OrderedQty)
FROM #Ch c JOIN dbo.DispatchNote d ON d.DispatchCode = 'XH-DEMO-' + c.N
JOIN dbo.OrderLine l ON l.OrderId = c.OrderId JOIN dbo.ProductVariant pv ON pv.ProductVariantId = l.ProductVariantId;

-- DH010 còn hàng trong kho: phiếu xuất chờ Thủ kho, bị từ chối, đã hủy; bàn giao thành phẩm bị từ chối, đã hủy
DECLARE @o10 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH010'),
        @l10M INT = (SELECT l.OrderLineId FROM dbo.OrderLine l JOIN dbo.CustomerOrder o ON o.OrderId = l.OrderId JOIN dbo.ProductVariant v ON v.ProductVariantId = l.ProductVariantId
                     WHERE o.OrderCode = 'DEMO-DH010' AND v.SizeCode = 'M');
INSERT dbo.DispatchNote (DispatchCode, OrderId, DeliveryDate, DeliveryAddress, Note, Status, CreatedAt, RejectReason) VALUES
 ('XH-DEMO-210', @o10, '2026-11-04', N'Kho khách hàng, KCN Tân Bình, TP.HCM', N'Giao nốt size M', 'PENDING', '2026-11-01 09:00:00', NULL),
 ('XH-DEMO-310', @o10, '2026-11-02', N'Kho khách hàng, KCN Tân Bình, TP.HCM', NULL, 'REJECTED', '2026-10-31 09:00:00', N'Sai địa chỉ giao hàng'),
 ('XH-DEMO-410', @o10, '2026-11-02', N'Kho khách hàng, KCN Tân Bình, TP.HCM', N'Khách đổi lịch', 'CANCELLED', '2026-10-31 10:00:00', NULL);
INSERT dbo.DispatchNoteLine (DispatchNoteId, OrderLineId, IssueQty)
SELECT DispatchNoteId, @l10M, 20 FROM dbo.DispatchNote WHERE DispatchCode IN ('XH-DEMO-210', 'XH-DEMO-310', 'XH-DEMO-410');
INSERT dbo.FgHandover (HandoverCode, OrderId, ProductId, InspectorId, HandedAt, Status, RejectReason) VALUES
 ('TP-DEMO-210', @o10, @pQu, @qaqc, '2026-10-30 11:00:00', 'REJECTED', N'Đếm thiếu so với phiếu'),
 ('TP-DEMO-310', @o10, @pQu, @qaqc, '2026-10-30 12:00:00', 'CANCELLED', NULL);
INSERT dbo.FgHandoverLine (FgHandoverId, OrderLineId, HandedQty)
SELECT FgHandoverId, @l10M, 5 FROM dbo.FgHandover WHERE HandoverCode IN ('TP-DEMO-210', 'TP-DEMO-310');

-- KPI đã chốt: đạt và không đạt
INSERT dbo.KpiTarget (OrderId, ProductId, SewingTeamId, StartDate, EndDate, TargetFinishedQty, WorkingDays, Status, Result, CreatedAt)
SELECT c.OrderId, @pQu, @may2, '2026-10-28', '2026-10-31', IIF(c.N = '011', 100, 60), 4, 'CLOSED', IIF(c.N = '011', 'ACHIEVED', 'NOT_ACHIEVED'), '2026-10-27 16:00:00'
FROM #Ch c WHERE c.N IN ('011', '012');

-- Chuyến xe giao hàng của DH009
DECLARE @veh INT = (SELECT VehicleId FROM dbo.Vehicle WHERE PlateNumber = '51D-678.90');
DECLARE @km INT = ISNULL((SELECT MAX(ISNULL(OdometerEnd, OdometerStart)) FROM dbo.VehicleTrip WHERE VehicleId = @veh), 10000);
INSERT dbo.VehicleTrip (VehicleId, TripDate, DriverEmployeeId, Purpose, DispatchNoteId, OdometerStart, OdometerEnd, FuelLiters, FuelCost, Status)
SELECT @veh, '2026-10-31', @codien, N'Giao hàng DEMO-DH009', DispatchNoteId, @km, @km + 40, 5, 110000, 'COMPLETED'
FROM dbo.DispatchNote WHERE DispatchCode = 'XH-DEMO-009';

/* ---------- 4. Mua hàng: đã nhận đủ, chốt thiếu, thiếu chờ quyết định, phiếu nhập bị trả ---------- */
DECLARE @o8 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH008'),
        @o13 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH013');
INSERT dbo.PurchaseOrder (PoCode, OrderId, SupplierId, OrderDate, ExpectedDeliveryCount, GoodsStatus, PaymentStatus, PaymentDate, PaidAmount, PaymentMethod, PaymentNote, Note, CreatedAt) VALUES
 ('DM-DEMO-013', @o13, @sup1, '2026-10-25', 1, 'PARTIALLY_RECEIVED', 'UNPAID', NULL, NULL, NULL, NULL, N'NCC giao thiếu 30 m', '2026-10-25 09:00:00'),
 ('DM-DEMO-014', @o8, @sup1, '2026-10-13', 1, 'RECEIVED', 'PAID', '2026-10-20', 4500000, 'BANK_TRANSFER', N'Chuyển khoản đợt 1', N'Đã nhận đủ và thanh toán', '2026-10-13 09:00:00'),
 ('DM-DEMO-015', NULL, @sup3, '2026-10-05', 1, 'CLOSED_SHORT', 'PAID', '2026-10-15', 1500000, 'CASH', NULL, N'Mua kim dùng chung, chốt thiếu 5 hộp', '2026-10-05 09:00:00');
INSERT dbo.PurchaseOrderLine (PurchaseOrderId, OrderMaterialId, OrderedQty, UnitPrice)
SELECT p.PurchaseOrderId, m.OrderMaterialId, 100, v.Price
FROM (VALUES ('DM-DEMO-013', @o13, 52000), ('DM-DEMO-014', @o8, 45000)) v(Code, OrderId, Price)
JOIN dbo.PurchaseOrder p ON p.PoCode = v.Code
JOIN dbo.OrderMaterial m ON m.OrderId = v.OrderId;
INSERT dbo.PurchaseOrderLine (PurchaseOrderId, MaterialId, OrderedQty, NotReplenishedQty, UnitPrice)
SELECT PurchaseOrderId, @mKim, 20, 5, 100000 FROM dbo.PurchaseOrder WHERE PoCode = 'DM-DEMO-015';

INSERT dbo.MaterialReceipt (ReceiptCode, OrderId, PurchaseOrderId, ReceiptDate, Status, ReturnReason, Note, SubmittedAt, ConfirmedBy, ConfirmedAt)
SELECT v.Code, p.OrderId, p.PurchaseOrderId, v.D, v.Status, v.Reason, v.Note, v.SubmittedAt, IIF(v.Status = 'CONFIRMED', @quandoc, NULL), IIF(v.Status = 'CONFIRMED', v.ConfirmedAt, NULL)
FROM (VALUES
 ('PN-DEMO-013', 'DM-DEMO-013', CAST('2026-10-28' AS DATE), 'CONFIRMED', CAST(NULL AS NVARCHAR(500)), N'Đợt giao duy nhất, thiếu 30 m', CAST('2026-10-28 09:00:00' AS DATETIME2(0)), CAST('2026-10-28 10:00:00' AS DATETIME2(0))),
 ('PN-DEMO-014', 'DM-DEMO-014', '2026-10-16', 'CONFIRMED', NULL, NULL, '2026-10-16 09:00:00', '2026-10-16 10:00:00'),
 ('PN-DEMO-015', 'DM-DEMO-015', '2026-10-10', 'CONFIRMED', NULL, N'Thiếu 5 hộp, không bù', '2026-10-10 09:00:00', '2026-10-10 10:00:00'),
 ('PN-DEMO-016', 'DM-DEMO-003', '2026-10-29', 'RETURNED', N'Sai màu vải, trả lại Thủ kho lập lại', NULL, '2026-10-29 09:00:00', NULL)) v(Code, Po, D, Status, Reason, Note, SubmittedAt, ConfirmedAt)
JOIN dbo.PurchaseOrder p ON p.PoCode = v.Po;
INSERT dbo.MaterialReceiptLine (MaterialReceiptId, OrderMaterialId, PurchaseOrderLineId, AcceptedQty, MaterialId)
SELECT r.MaterialReceiptId, pl.OrderMaterialId, pl.PurchaseOrderLineId, v.Qty, pl.MaterialId
FROM (VALUES ('PN-DEMO-013', 'DM-DEMO-013', 70), ('PN-DEMO-014', 'DM-DEMO-014', 100), ('PN-DEMO-015', 'DM-DEMO-015', 15), ('PN-DEMO-016', 'DM-DEMO-003', 200)) v(Code, Po, Qty)
JOIN dbo.MaterialReceipt r ON r.ReceiptCode = v.Code
JOIN dbo.PurchaseOrder p ON p.PoCode = v.Po
CROSS APPLY (SELECT TOP (1) * FROM dbo.PurchaseOrderLine x WHERE x.PurchaseOrderId = p.PurchaseOrderId ORDER BY x.PurchaseOrderLineId) pl;

/* ---------- 5. Phiếu cấp, trả vải, bàn giao BTP, lệnh cắt ở các trạng thái còn thiếu (lệnh LC-DEMO-002, đơn DH013) ---------- */
INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, WorkOrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, RejectReason, CreatedAt)
VALUES ('PC-DEMO-901', 'MATERIAL', @o3, @wo2, @cat, '2026-10-27', @thukho, 'REJECTED', N'Vải bị bẩn, tổ cắt từ chối nhận', '2026-10-27 08:00:00');
INSERT dbo.MaterialIssueLine (MaterialIssueId, MaterialId, IssuedQty)
SELECT MaterialIssueId, @mVaiDen, 30 FROM dbo.MaterialIssue WHERE IssueCode = 'PC-DEMO-901';

DECLARE @il3 INT = (SELECT l.MaterialIssueLineId FROM dbo.MaterialIssueLine l JOIN dbo.MaterialIssue i ON i.MaterialIssueId = l.MaterialIssueId WHERE i.IssueCode = 'PC-DEMO-003');
INSERT dbo.MaterialReturn (ReturnCode, MaterialIssueId, Note, Status, ConfirmedBy, ConfirmedAt, RejectReason, CreatedAt)
SELECT v.Code, l.MaterialIssueId, v.Note, v.Status, IIF(v.Status = 'CONFIRMED', @thukho, NULL), IIF(v.Status = 'CONFIRMED', CAST('2026-10-27 16:30:00' AS DATETIME2(0)), NULL), v.Reason, '2026-10-27 16:00:00'
FROM (VALUES ('TV-DEMO-002', N'Vải thừa bàn cắt 2', 'CONFIRMED', CAST(NULL AS NVARCHAR(500))),
             ('TV-DEMO-003', N'Vải đầu tấm', 'REJECTED', N'Vải rách, không nhận lại kho'),
             ('TV-DEMO-004', N'Tổ cắt rút phiếu', 'CANCELLED', NULL)) v(Code, Note, Status, Reason)
JOIN dbo.MaterialIssueLine l ON l.MaterialIssueLineId = @il3;
INSERT dbo.MaterialReturnLine (MaterialReturnId, MaterialIssueLineId, ReturnedQty, ConfirmedQty)
SELECT r.MaterialReturnId, @il3, v.Qty, IIF(r.Status = 'CONFIRMED', v.Qty, NULL)
FROM (VALUES ('TV-DEMO-002', 5), ('TV-DEMO-003', 3), ('TV-DEMO-004', 2)) v(Code, Qty)
JOIN dbo.MaterialReturn r ON r.ReturnCode = v.Code;

INSERT dbo.CutHandover (HandoverCode, WorkOrderId, HandedAt, Status, RejectReason) VALUES
 ('BG-DEMO-901', @wo2, '2026-10-27 15:00:00', 'REJECTED', N'Số bó không khớp phiếu'),
 ('BG-DEMO-902', @wo2, '2026-10-27 15:30:00', 'CANCELLED', NULL);
INSERT dbo.CutHandoverLine (CutHandoverId, OrderLineId, HandedQty)
SELECT h.CutHandoverId, l.OrderLineId, 5 FROM dbo.CutHandover h JOIN dbo.OrderLine l ON l.OrderId = @o3 AND l.ProductId = @pAo
WHERE h.HandoverCode IN ('BG-DEMO-901', 'BG-DEMO-902');

INSERT dbo.WorkOrder (WorkOrderCode, OrderId, ProductId, CuttingTeamId, DueDate, Status, Note, CreatedAt) VALUES
 ('LC-DEMO-013', @o13, @pQu, @cat, '2026-11-10', 'OPEN', N'Lệnh mới, tổ cắt chưa bắt đầu', '2026-10-30 08:00:00'),
 ('LC-DEMO-113', @o13, @pQu, @cat, '2026-11-08', 'CANCELLED', N'Lập nhầm, đã hủy', '2026-10-29 08:00:00');
INSERT dbo.WorkOrderLine (WorkOrderId, OrderLineId, PlannedQty)
SELECT w.WorkOrderId, l.OrderLineId, l.OrderedQty FROM dbo.WorkOrder w JOIN dbo.OrderLine l ON l.OrderId = w.OrderId
WHERE w.WorkOrderCode IN ('LC-DEMO-013', 'LC-DEMO-113');

-- Giao việc: tổ trưởng xác nhận hộ, công nhân từ chối; phân công cũ đã đóng (giữ lịch sử)
DECLARE @op6 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @pAo AND OperationCode = 'OP06'),
        @op7 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @pAo AND OperationCode = 'OP07'),
        @op1 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @pAo AND OperationCode = 'OP01'),
        @op2 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @pAo AND OperationCode = 'OP02'),
        @l3M INT = (SELECT l.OrderLineId FROM dbo.OrderLine l JOIN dbo.ProductVariant v ON v.ProductVariantId = l.ProductVariantId WHERE l.OrderId = @o3 AND l.ProductId = @pAo AND v.SizeCode = 'M'),
        @l3L INT = (SELECT l.OrderLineId FROM dbo.OrderLine l JOIN dbo.ProductVariant v ON v.ProductVariantId = l.ProductVariantId WHERE l.OrderId = @o3 AND l.ProductId = @pAo AND v.SizeCode = 'L');
INSERT dbo.WorkAllocation (MovementType, SewingTeamId, WorkerId, OrderLineId, OperationId, Quantity, ConfirmedQty, ConfirmMode, OnBehalfReason, Status, RejectReason, CreatedAt, ConfirmedAt) VALUES
 ('HANDOVER', @may1, @w2, @l3L, @op6, 5, 5, 'ON_BEHALF', N'Công nhân nghỉ phép, tổ trưởng xác nhận hộ', 'CONFIRMED', NULL, '2026-10-30 08:00:00', '2026-10-30 08:30:00'),
 ('HANDOVER', @may1, @w3, @l3L, @op7, 5, NULL, 'WORKER', NULL, 'REJECTED', N'Không đúng công đoạn của tôi', '2026-10-30 09:00:00', NULL);
INSERT dbo.OperationAssignment (SewingTeamId, OrderId, OperationId, WorkerId, ValidFrom, ValidTo, AssignedAt)
VALUES (@may1, @o3, @op6, @w4, '2026-10-25', '2026-10-28', '2026-10-25 08:00:00');

-- Hàng hỏng: phiếu đang bị công nhân phản đối; phiếu hủy còn trống để Tổ trưởng may tạo đơn cắt bù trên giao diện
DECLARE @rateOp2 INT = (SELECT TOP (1) PieceRateId FROM dbo.PieceRate WHERE OperationId = @op2 AND Status = 'APPROVED' AND EffectiveDate <= '2026-10-30' ORDER BY EffectiveDate DESC);
INSERT dbo.DefectRecord (DefectCode, WorkDate, ShiftCode, SewingTeamId, OrderLineId, OperationId, ResponsibleWorkerId, DefectType, Quantity, DefectReasonCode, ReasonNote, PieceRateId,
                         WorkerNotifiedAt, WorkerObjection, ObjectedAt, EditDeadline, Status, RecordedBy, RecordedAt) VALUES
 ('MAY1-261102-901', '2026-11-02', 'MORNING', @may1, @l3M, @op1, @w1, 'REWORK', 2, 'CROOKED_SEAM', NULL, NULL,
  '2026-11-02 09:00:00', N'Lỗi do máy, không phải do tôi', '2026-11-02 09:30:00', dbo.fn_NextWorkingDayEnd('2026-11-02'), 'RECORDED', @ttmay, '2026-11-02 09:00:00'),
 ('MAY1-261030-901', '2026-10-30', 'AFTERNOON', @may1, @l3L, @op2, @w1, 'SCRAP', 2, 'OTHER', N'Rách vải khi may', @rateOp2,
  '2026-10-30 15:00:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-30'), 'CONFIRMED', @ttmay, '2026-10-30 15:00:00');

/* ---------- 6. Đơn từ: đủ 11 loại, đủ 6 trạng thái ---------- */
DECLARE @d3 INT = (SELECT DefectRecordId FROM dbo.DefectRecord WHERE DefectCode = 'MAY1-261015-001'),
        @d4 INT = (SELECT DefectRecordId FROM dbo.DefectRecord WHERE DefectCode = 'MAY1-261016-001');
DECLARE @r13 INT = (SELECT MaterialReceiptId FROM dbo.MaterialReceipt WHERE ReceiptCode = 'PN-DEMO-013'),
        @r15 INT = (SELECT MaterialReceiptId FROM dbo.MaterialReceipt WHERE ReceiptCode = 'PN-DEMO-015');

INSERT dbo.Request (RequestCode, RequestTypeCode, Status, OrderId, WorkOrderId, MaterialReceiptId, DefectRecordId, TargetDate, Title, Content, CreatedBy, CreatedByTeamId,
                    DecidedAt, DecisionNote, ResultNote, CompletedAt, ShortageResolution, CreatedAt) VALUES
 ('YC-DEMO-003', 'SPARE_PARTS', 'PENDING_APPROVAL', NULL, NULL, NULL, NULL, '2026-11-05', N'Thay dây curoa máy 1 kim', N'Máy số 12 tổ may 1 trượt dây', @codien, NULL, NULL, NULL, NULL, NULL, NULL, '2026-11-01 08:00:00'),
 ('YC-DEMO-004', 'NEW_MACHINE', 'APPROVED', NULL, NULL, NULL, NULL, '2026-11-20', N'Mua thêm máy vắt sổ', N'Tổ may 2 thiếu máy', @codien, NULL, '2026-10-29 10:00:00', N'Đồng ý, mua trong tháng 11', NULL, NULL, NULL, '2026-10-28 08:00:00'),
 ('YC-DEMO-005', 'MACHINE_SUPPLIES', 'PENDING_PROCESSING', NULL, NULL, NULL, NULL, '2026-11-04', N'Xin dầu máy may', NULL, @codien, NULL, NULL, NULL, NULL, NULL, NULL, '2026-11-01 08:30:00'),
 ('YC-DEMO-006', 'MACHINE_SUPPLIES', 'COMPLETED', NULL, NULL, NULL, NULL, '2026-10-20', N'Xin kim máy', NULL, @codien, NULL, NULL, NULL, N'Đã cấp đủ 2 hộp', '2026-10-19 15:00:00', NULL, '2026-10-18 08:00:00'),
 ('YC-DEMO-007', 'SPARE_PARTS', 'REJECTED', NULL, NULL, NULL, NULL, '2026-10-25', N'Thay bàn là hơi', NULL, @codien, NULL, '2026-10-22 09:00:00', N'Bàn là còn dùng được, sửa thay vì mua mới', NULL, NULL, NULL, '2026-10-21 08:00:00'),
 ('YC-DEMO-008', 'NEW_MACHINE', 'CANCELLED', NULL, NULL, NULL, NULL, '2026-10-30', N'Mua máy đính cúc', NULL, @codien, NULL, NULL, NULL, NULL, NULL, NULL, '2026-10-20 08:00:00'),
 ('YC-DEMO-009', 'MATERIAL_SUPPLEMENT', 'PENDING_APPROVAL', @o3, NULL, NULL, NULL, '2026-11-06', N'Bổ sung khóa kéo', N'Khóa kéo lỗi nhiều hơn dự kiến', @ttmay, @may1, NULL, NULL, NULL, NULL, NULL, '2026-11-01 09:00:00'),
 ('YC-DEMO-010', 'MATERIAL_SUPPLEMENT', 'APPROVED', @o3, NULL, NULL, NULL, '2026-11-03', N'Bổ sung nhãn dệt', NULL, @thukho, NULL, '2026-10-31 10:00:00', N'Đồng ý', NULL, NULL, NULL, '2026-10-30 09:00:00'),
 ('YC-DEMO-011', 'DEFECTIVE_MATERIAL', 'PENDING_APPROVAL', @o3, @wo2, NULL, NULL, '2026-11-03', N'Vải đen bị lỗi dệt (kho đủ để cấp bù)', N'Duyệt đơn này: hệ thống tự tạo đơn Xin NVL', @ttcat, @cat, NULL, NULL, NULL, NULL, NULL, '2026-11-01 10:00:00'),
 ('YC-DEMO-012', 'DEFECTIVE_MATERIAL', 'PENDING_APPROVAL', @o1, NULL, NULL, NULL, '2026-11-03', N'Vải trắng bị ố (kho thiếu)', N'Duyệt đơn này: hệ thống tự tạo đơn Mua NVL cho phần thiếu', @ttcat, @cat, NULL, NULL, NULL, NULL, NULL, '2026-11-01 10:30:00'),
 ('YC-DEMO-013', 'ATTENDANCE_CORRECTION', 'REJECTED', NULL, NULL, NULL, NULL, '2026-10-22', N'Chấm công bù ngày 22/10', N'Quên quẹt thẻ', @ttmay, @may1, '2026-10-24 09:00:00', N'Không có xác nhận của tổ', NULL, NULL, NULL, '2026-10-23 08:00:00'),
 ('YC-DEMO-014', 'REPLACEMENT_CUTTING', 'PENDING_APPROVAL', @o1, NULL, NULL, @d3, '2026-11-05', N'Cắt bù thân trước', N'Theo phiếu hàng hủy', @ttmay, @may1, NULL, NULL, NULL, NULL, NULL, '2026-11-01 11:00:00'),
 ('YC-DEMO-015', 'REPLACEMENT_CUTTING', 'APPROVED', @o1, NULL, NULL, @d4, '2026-11-04', N'Cắt bù thân trước size L', N'Đã duyệt, chờ lập lệnh cắt bù', @ttmay, @may1, '2026-10-31 15:00:00', N'Đồng ý', NULL, NULL, NULL, '2026-10-31 08:00:00'),
 ('YC-DEMO-016', 'SHORT_DELIVERY', 'PENDING_PROCESSING', @o13, NULL, @r13, NULL, NULL, N'Thiếu NVL — phiếu PN-DEMO-013', N'Hệ thống tạo khi phiếu nhập còn thiếu', NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-10-28 10:00:00'),
 ('YC-DEMO-017', 'SHORT_DELIVERY', 'COMPLETED', NULL, NULL, @r15, NULL, NULL, N'Thiếu NVL — phiếu PN-DEMO-015', N'Hệ thống tạo khi phiếu nhập còn thiếu', NULL, NULL, '2026-10-11 09:00:00', N'Không cần bù, kim còn đủ dùng', N'Chốt không bù', '2026-10-11 09:00:00', 'NO_REPLENISH', '2026-10-10 10:00:00'),
 ('YC-DEMO-018', 'MATERIAL_PURCHASE', 'PENDING_PROCESSING', @o1, NULL, NULL, NULL, NULL, N'Mua NVL bù hàng lỗi', N'Tồn kho không đủ, còn thiếu vải trắng. Vui lòng tạo đơn mua NVL.', NULL, NULL, NULL, NULL, NULL, NULL, NULL, '2026-10-30 10:00:00'),
 ('YC-DEMO-019', 'MATERIAL_PURCHASE', 'COMPLETED', @o1, NULL, NULL, NULL, NULL, N'Mua NVL bù hàng lỗi (đã tạo đơn mua)', N'Tồn kho không đủ, còn thiếu vải trắng.', NULL, NULL, NULL, NULL, N'Đã tạo đơn mua DM-DEMO-016', '2026-10-27 09:00:00', NULL, '2026-10-26 10:00:00'),
 ('YC-DEMO-020', 'MATERIAL_REQUISITION', 'COMPLETED', @o10, NULL, NULL, NULL, '2026-10-25', N'Xin vải cho DEMO-DH010', NULL, @ttcat, @cat, NULL, NULL, N'Đã cấp theo PC-DEMO-110', '2026-10-25 09:00:00', NULL, '2026-10-24 15:00:00'),
 ('YC-DEMO-021', 'SEMI_FINISHED_REQUISITION', 'REJECTED', @o3, NULL, NULL, NULL, '2026-10-30', N'Xin BTP size M', NULL, @ttmay, @may1, '2026-10-29 09:00:00', N'Kho BTP chưa có hàng', NULL, NULL, NULL, '2026-10-28 15:00:00');

UPDATE i SET i.RequestId = r.RequestId
FROM dbo.MaterialIssue i JOIN dbo.Request r ON r.RequestCode = 'YC-DEMO-020' WHERE i.IssueCode = 'PC-DEMO-110';

INSERT dbo.RequestLine (RequestId, MaterialId, OrderLineId, ProductComponentMaterialId, ItemDescription, Quantity, ReplenishQty, MaterialReceiptLineId)
SELECT r.RequestId, v.MaterialId, v.OrderLineId, v.Pcm, v.Item, v.Qty, v.Replenish, v.ReceiptLine
FROM (VALUES
 ('YC-DEMO-003', CAST(NULL AS INT), CAST(NULL AS INT), CAST(NULL AS INT), N'Dây curoa máy 1 kim', CAST(5 AS DECIMAL(18,3)), CAST(NULL AS DECIMAL(18,3)), CAST(NULL AS INT)),
 ('YC-DEMO-004', NULL, NULL, NULL, N'Máy vắt sổ 5 chỉ', 1, NULL, NULL),
 ('YC-DEMO-005', NULL, NULL, NULL, N'Dầu máy may (lít)', 10, NULL, NULL),
 ('YC-DEMO-006', @mKim, NULL, NULL, NULL, 2, NULL, NULL),
 ('YC-DEMO-007', NULL, NULL, NULL, N'Bàn là hơi', 1, NULL, NULL),
 ('YC-DEMO-008', NULL, NULL, NULL, N'Máy đính cúc', 1, NULL, NULL),
 ('YC-DEMO-009', @mKhoa, NULL, NULL, NULL, 50, NULL, NULL),
 ('YC-DEMO-010', @mNhan, NULL, NULL, NULL, 30, NULL, NULL),
 ('YC-DEMO-011', @mVaiDen, NULL, NULL, NULL, 20, NULL, NULL),
 ('YC-DEMO-012', @mVaiTrang, NULL, NULL, NULL, 1000, NULL, NULL),
 ('YC-DEMO-018', @mVaiTrang, NULL, NULL, NULL, 200, NULL, NULL),
 ('YC-DEMO-019', @mVaiTrang, NULL, NULL, NULL, 100, NULL, NULL),
 ('YC-DEMO-020', @mVaiBe, NULL, NULL, NULL, 160, NULL, NULL),
 ('YC-DEMO-021', NULL, @l3M, NULL, NULL, 10, NULL, NULL)) v(Code, MaterialId, OrderLineId, Pcm, Item, Qty, Replenish, ReceiptLine)
JOIN dbo.Request r ON r.RequestCode = v.Code;

-- Cắt bù: dòng là chi tiết vải "Thân trước" đúng màu của phiếu hàng hủy (size theo dòng đơn)
INSERT dbo.RequestLine (RequestId, OrderLineId, ProductComponentMaterialId, Quantity)
SELECT r.RequestId, d.OrderLineId, pcm.ProductComponentMaterialId, 1
FROM dbo.Request r
JOIN dbo.DefectRecord d ON d.DefectRecordId = r.DefectRecordId
JOIN dbo.OrderLine l ON l.OrderLineId = d.OrderLineId
JOIN dbo.ProductComponent pc ON pc.ProductId = l.ProductId AND pc.ComponentName = N'Thân trước'
JOIN dbo.ProductVariant lv ON lv.ProductVariantId = l.ProductVariantId
JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentId = pc.ProductComponentId AND pcm.ProductColorId = lv.ProductColorId
WHERE r.RequestCode IN ('YC-DEMO-014', 'YC-DEMO-015');

-- Thiếu hàng khi nhập: dòng trỏ về dòng phiếu nhập gốc
INSERT dbo.RequestLine (RequestId, MaterialId, Quantity, ReplenishQty, MaterialReceiptLineId)
SELECT q.RequestId, COALESCE(m.MaterialId, rl.MaterialId), v.Qty, v.Replenish, rl.MaterialReceiptLineId
FROM (VALUES ('YC-DEMO-016', 30, CAST(NULL AS DECIMAL(18,3))), ('YC-DEMO-017', 5, 0)) v(Code, Qty, Replenish)
JOIN dbo.Request q ON q.RequestCode = v.Code
JOIN dbo.MaterialReceiptLine rl ON rl.MaterialReceiptId = q.MaterialReceiptId
LEFT JOIN dbo.OrderMaterial m ON m.OrderMaterialId = rl.OrderMaterialId;

-- Đơn mua sinh từ đơn Mua NVL, chờ Quản đốc chốt nhà cung cấp
INSERT dbo.PurchaseOrder (PoCode, OrderId, SourceRequestId, GoodsStatus, PaymentStatus, Note, CreatedAt)
SELECT 'DM-DEMO-016', @o1, RequestId, 'PENDING_FINALISATION', 'UNPAID', N'Tạo từ đơn Mua NVL YC-DEMO-019', '2026-10-27 09:00:00' FROM dbo.Request WHERE RequestCode = 'YC-DEMO-019';
INSERT dbo.PurchaseOrderLine (PurchaseOrderId, OrderMaterialId, OrderedQty, UnitPrice)
SELECT p.PurchaseOrderId, m.OrderMaterialId, 100, 0
FROM dbo.PurchaseOrder p JOIN dbo.OrderMaterial m ON m.OrderId = @o1 AND m.MaterialId = @mVaiTrang WHERE p.PoCode = 'DM-DEMO-016';

/* ---------- 7. Ghi kho qua usp_Stock_Post, theo thứ tự thời gian ---------- */
CREATE TABLE #Mv (Seq INT IDENTITY PRIMARY KEY, WarehouseType VARCHAR(15), MaterialId INT, OrderLineId INT, DocType VARCHAR(30), DocId INT, LineId INT, Qty DECIMAL(18,3), TxDate DATE);

INSERT #Mv (WarehouseType, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'MATERIAL', COALESCE(m.MaterialId, rl.MaterialId), NULL, 'MATERIAL_RECEIPT', r.MaterialReceiptId, rl.MaterialReceiptLineId, rl.AcceptedQty, r.ReceiptDate
FROM dbo.MaterialReceipt r
JOIN dbo.MaterialReceiptLine rl ON rl.MaterialReceiptId = r.MaterialReceiptId
LEFT JOIN dbo.OrderMaterial m ON m.OrderMaterialId = rl.OrderMaterialId
WHERE r.ReceiptCode IN ('PN-DEMO-006', 'PN-DEMO-009', 'PN-DEMO-010', 'PN-DEMO-011', 'PN-DEMO-012', 'PN-DEMO-013', 'PN-DEMO-014', 'PN-DEMO-015')
ORDER BY r.ReceiptDate, r.MaterialReceiptId;

INSERT #Mv (WarehouseType, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'MATERIAL', il.MaterialId, NULL, 'MATERIAL_RETURN', r.MaterialReturnId, rl.MaterialReturnLineId, rl.ConfirmedQty, '2026-10-27'
FROM dbo.MaterialReturn r JOIN dbo.MaterialReturnLine rl ON rl.MaterialReturnId = r.MaterialReturnId
JOIN dbo.MaterialIssueLine il ON il.MaterialIssueLineId = rl.MaterialIssueLineId WHERE r.ReturnCode = 'TV-DEMO-002';

INSERT #Mv (WarehouseType, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'MATERIAL', l.MaterialId, NULL, 'MATERIAL_ISSUE', i.MaterialIssueId, l.MaterialIssueLineId, -l.ConfirmedQty, i.IssueDate
FROM #Ch c JOIN dbo.MaterialIssue i ON i.IssueCode = 'PC-DEMO-1' + c.N JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = i.MaterialIssueId;

INSERT #Mv (WarehouseType, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'SEMI_FINISHED', NULL, l.OrderLineId, 'CUT_HANDOVER', h.CutHandoverId, l.CutHandoverLineId, l.ConfirmedQty, '2026-10-27'
FROM #Ch c JOIN dbo.CutHandover h ON h.HandoverCode = 'BG-DEMO-' + c.N JOIN dbo.CutHandoverLine l ON l.CutHandoverId = h.CutHandoverId;

INSERT #Mv (WarehouseType, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'SEMI_FINISHED', NULL, l.OrderLineId, 'MATERIAL_ISSUE', i.MaterialIssueId, l.MaterialIssueLineId, -l.ConfirmedQty, i.IssueDate
FROM #Ch c JOIN dbo.MaterialIssue i ON i.IssueCode = 'PC-DEMO-2' + c.N JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = i.MaterialIssueId;

INSERT #Mv (WarehouseType, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'FINISHED', NULL, l.OrderLineId, 'FG_HANDOVER', h.FgHandoverId, l.FgHandoverLineId, l.ConfirmedQty, '2026-10-30'
FROM #Ch c JOIN dbo.FgHandover h ON h.HandoverCode = 'TP-DEMO-' + c.N JOIN dbo.FgHandoverLine l ON l.FgHandoverId = h.FgHandoverId;

INSERT #Mv (WarehouseType, MaterialId, OrderLineId, DocType, DocId, LineId, Qty, TxDate)
SELECT 'FINISHED', NULL, l.OrderLineId, 'DISPATCH_NOTE', d.DispatchNoteId, l.DispatchNoteLineId, -l.IssueQty, '2026-10-31'
FROM #Ch c JOIN dbo.DispatchNote d ON d.DispatchCode = 'XH-DEMO-' + c.N JOIN dbo.DispatchNoteLine l ON l.DispatchNoteId = d.DispatchNoteId;

CREATE TABLE #Posted (StockBalanceId INT, BalanceAfter DECIMAL(18,3));
DECLARE @seq INT = 0, @wh VARCHAR(15), @mid INT, @olid INT, @dt VARCHAR(30), @did INT, @lid INT, @qty DECIMAL(18,3), @tx DATE;
WHILE 1 = 1
BEGIN
    SELECT TOP (1) @seq = Seq, @wh = WarehouseType, @mid = MaterialId, @olid = OrderLineId, @dt = DocType, @did = DocId,
                   @lid = LineId, @qty = Qty, @tx = TxDate
    FROM #Mv WHERE Seq > @seq ORDER BY Seq;
    IF @@ROWCOUNT = 0 BREAK;
    INSERT #Posted EXEC dbo.usp_Stock_Post @WarehouseType = @wh, @MaterialId = @mid, @OrderLineId = @olid,
        @SourceDocType = @dt, @SourceDocId = @did, @SourceLineId = @lid, @QtyChange = @qty, @TransactionDate = @tx;
END;
DROP TABLE #Mv, #Posted;

-- Kiểm kê (ghi sau khi đã có tồn): phiếu bị trả lại; phiếu kho BTP và kho thành phẩm chờ duyệt
INSERT dbo.Stocktake (StocktakeCode, WarehouseType, Scope, StocktakeDate, Status, ReturnReason, CreatedAt) VALUES
 ('KK-DEMO-003', 'MATERIAL', N'Kiểm kê kim máy', '2026-10-31', 'RETURNED', N'Số đếm chưa khớp sổ tay, đếm lại', '2026-10-31 16:00:00'),
 ('KK-DEMO-004', 'SEMI_FINISHED', N'Kiểm kê BTP cuối tháng', '2026-10-31', 'PENDING_APPROVAL', NULL, '2026-10-31 16:30:00'),
 ('KK-DEMO-005', 'FINISHED', N'Kiểm kê thành phẩm cuối tháng', '2026-10-31', 'PENDING_APPROVAL', NULL, '2026-10-31 17:00:00');
INSERT dbo.StocktakeLine (StocktakeId, StockBalanceId, BookQty, CountedQty, Note)
SELECT s.StocktakeId, b.StockBalanceId, b.OnHandQty, b.OnHandQty + v.Diff, v.Note
FROM (VALUES ('KK-DEMO-003', 'MATERIAL', @mKim, CAST(NULL AS INT), -1, N'Thiếu 1 hộp'),
             ('KK-DEMO-004', 'SEMI_FINISHED', NULL, @l3M, -2, N'Lệch 2 bộ'),
             ('KK-DEMO-005', 'FINISHED', NULL, @l10M, 0, N'Khớp sổ')) v(Code, Wh, MaterialId, OrderLineId, Diff, Note)
JOIN dbo.Stocktake s ON s.StocktakeCode = v.Code
JOIN dbo.StockBalance b ON b.WarehouseType = v.Wh AND b.ProductComponentMaterialId IS NULL
 AND EXISTS (SELECT b.MaterialId, b.OrderLineId INTERSECT SELECT v.MaterialId, v.OrderLineId);

/* ---------- 8. Chấm công: lượt quẹt ngày 02/11 (đủ giờ, thiếu giờ ra, quẹt trùng, mã chưa gán nhân viên), phiên đồng bộ, chấm hộ ---------- */
DECLARE @punch dbo.AttendancePunchTableType;
INSERT @punch (SourceRecordId, AttendanceCode, PunchTime)
SELECT 900000 + ROW_NUMBER() OVER (ORDER BY v.Code, v.T), v.Code, v.T
FROM (VALUES (104, CAST('2026-11-02 07:25:00' AS DATETIME2(0))), (104, '2026-11-02 16:35:00'),
             (105, '2026-11-02 07:28:00'), (105, '2026-11-02 18:00:00'),
             (106, '2026-11-02 07:40:00'), (106, '2026-11-02 16:30:00'),
             (107, '2026-11-02 07:20:00'), (107, '2026-11-02 16:32:00'),
             (108, '2026-11-02 07:27:00'),
             (111, '2026-11-02 07:26:00'), (111, '2026-11-02 07:29:00'), (111, '2026-11-02 16:31:00'),
             (999, '2026-11-02 07:30:00'), (999, '2026-11-02 16:30:00')) v(Code, T);
EXEC dbo.usp_Attendance_IngestBatch @Rows = @punch, @Source = 'RJ_SYNC';
DELETE @punch;
INSERT @punch (SourceRecordId, AttendanceCode, PunchTime) VALUES (NULL, 112, '2026-11-02 07:30:00'), (NULL, 112, '2026-11-02 16:30:00');
EXEC dbo.usp_Attendance_IngestBatch @Rows = @punch, @Source = 'EXCEL_IMPORT', @FileName = N'cham-cong-02-11-demo.xlsx';
INSERT dbo.AttendanceSyncSession (Source, StartedAt, FinishedAt, RecordsReceived, RecordsInserted, Status, ErrorMessage)
VALUES ('RJ_SYNC', '2026-11-01 06:00:00', '2026-11-01 06:00:30', 0, 0, 'FAILED', N'Không kết nối được máy chấm công');

EXEC dbo.usp_Attendance_ApplyAdjustment @EmployeeId = @w6, @WorkDate = '2026-11-02', @AdjustmentType = 'FINGERPRINT_FAIL',
     @Reason = N'Máy không nhận vân tay', @CheckIn = '07:30:00', @CheckOut = '16:30:00', @ChangedBy = @ttmay2;
EXEC dbo.usp_Attendance_ApplyAdjustment @EmployeeId = @w7, @WorkDate = '2026-11-02', @AdjustmentType = 'FORGOT_PUNCH',
     @Reason = N'Quên quẹt thẻ buổi sáng', @CheckIn = '07:30:00', @CheckOut = '16:30:00', @ChangedBy = @ttmay2;
EXEC dbo.usp_Attendance_ApplyAdjustment @EmployeeId = @w8, @WorkDate = '2026-11-03', @AdjustmentType = 'OFFSITE_WORK',
     @Reason = N'Đi giao mẫu cho khách', @CheckIn = '07:30:00', @CheckOut = '16:30:00', @ChangedBy = @ttmay2;

/* ---------- 9. Suất ăn: ngày đã báo bếp và ngày mở (tổ may 1 đã xác nhận, các tổ khác chưa) ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.DailyMeal WHERE MealDate = '2026-11-01')
BEGIN
    INSERT dbo.DailyMeal (MealDate, Status, OfficeCount, GuestCount, GuestNote, MealPrice) VALUES ('2026-11-01', 'OPEN', 8, 2, N'Khách kiểm hàng', 35000);
    INSERT dbo.TeamMealConfirmation (DailyMealId, TeamId, MealCount, AttendanceCount, Source, ConfirmedAt)
    SELECT m.DailyMealId, t.TeamId, 4, 4, 'CONFIRMED', '2026-11-01 08:00:00'
    FROM dbo.DailyMeal m CROSS JOIN dbo.Team t WHERE m.MealDate = '2026-11-01' AND t.TeamCode IN ('CAT', 'MAY1', 'MAY2');
    UPDATE dbo.DailyMeal SET Status = 'KITCHEN_NOTIFIED', ClosedAt = '2026-11-01 08:30:00', KitchenNotifiedAt = '2026-11-01 08:31:00' WHERE MealDate = '2026-11-01';
END;
IF NOT EXISTS (SELECT 1 FROM dbo.DailyMeal WHERE MealDate = '2026-11-03')
BEGIN
    INSERT dbo.DailyMeal (MealDate, Status, OfficeCount, GuestCount, MealPrice) VALUES ('2026-11-03', 'OPEN', 8, 0, 35000);
    INSERT dbo.TeamMealConfirmation (DailyMealId, TeamId, MealCount, AttendanceCount, Source, ConfirmedAt)
    SELECT m.DailyMealId, @may1, 5, 5, 'CONFIRMED', '2026-11-03 07:45:00' FROM dbo.DailyMeal m WHERE m.MealDate = '2026-11-03';
END;

/* ---------- 10. Bậc thợ: đề xuất đang chờ, đã áp dụng, không áp dụng ---------- */
INSERT dbo.GradeEvaluation (WorkerId, ProposedBy, TeamId, CurrentGradeCode, ProposedGradeCode, Reason, RecentDefectCount, EvaluationDate, Status, EffectiveDate, ConsideredAt, DecisionNote, CreatedAt) VALUES
 (@w2, @ttmay, @may1, 'C', 'B', N'Tay nghề tiến bộ, ít lỗi trong 3 tháng', 0, '2026-10-30', 'SUBMITTED', NULL, NULL, NULL, '2026-10-30 09:00:00'),
 (@w4, @ttmay, @may1, 'C', 'B', N'Làm được công đoạn khó', 1, '2026-09-25', 'APPLIED', '2026-10-01', '2026-09-28 10:00:00', N'Đồng ý', '2026-09-25 09:00:00'),
 (@w6, @ttmay2, @may2, 'C', 'A', N'Đề xuất lên thẳng bậc A', 3, '2026-10-29', 'NOT_APPLIED', NULL, '2026-10-30 10:00:00', N'Chưa đủ điều kiện, còn nhiều lỗi', '2026-10-29 09:00:00');

/* ---------- 11. Lương, khóa tháng, chi phí, hòa vốn ----------
   Trạng thái theo tháng: 05 đã duyệt + khóa, 06 nháp, 07 bị trả lại, 08 đã duyệt, 09 chờ duyệt, 10 đã duyệt (đủ điều kiện tính hòa vốn). */
INSERT dbo.Payroll (PeriodId, Status, SubmittedAt, ApprovedAt, RejectReason)
SELECT a.PeriodId, v.Status, v.SubmittedAt, v.ApprovedAt, v.Reason
FROM (VALUES ('2026-05', 'APPROVED', CAST('2026-06-03 09:00:00' AS DATETIME2(0)), CAST('2026-06-04 09:00:00' AS DATETIME2(0)), CAST(NULL AS NVARCHAR(500))),
             ('2026-06', 'DRAFT', NULL, NULL, NULL),
             ('2026-07', 'REJECTED', '2026-08-03 09:00:00', NULL, N'Sai phụ cấp bậc thợ, sửa lại rồi gửi duyệt'),
             ('2026-10', 'APPROVED', '2026-11-01 09:00:00', '2026-11-01 15:00:00', NULL)) v(PeriodMonth, Status, SubmittedAt, ApprovedAt, Reason)
JOIN dbo.AccountingPeriod a ON a.PeriodMonth = v.PeriodMonth;

-- Phiếu lương các tháng mới chép từ tháng 09 (số liệu mẫu)
DECLARE @pay9 INT = (SELECT p.PayrollId FROM dbo.Payroll p JOIN dbo.AccountingPeriod a ON a.PeriodId = p.PeriodId WHERE a.PeriodMonth = '2026-09');
INSERT dbo.Payslip (PayrollId, EmployeeId, WorkDays, TotalAdditions, TotalDeductions, Insurance, NetPay)
SELECT p.PayrollId, s.EmployeeId, s.WorkDays, s.TotalAdditions, s.TotalDeductions, s.Insurance, s.NetPay
FROM dbo.Payslip s
CROSS JOIN dbo.Payroll p JOIN dbo.AccountingPeriod a ON a.PeriodId = p.PeriodId
WHERE s.PayrollId = @pay9 AND a.PeriodMonth IN ('2026-05', '2026-06', '2026-07', '2026-10');
INSERT dbo.PayslipItem (PayslipId, ItemGroup, ItemName, Amount, SortOrder)
SELECT n.PayslipId, i.ItemGroup, i.ItemName, i.Amount, i.SortOrder
FROM dbo.Payslip s
JOIN dbo.PayslipItem i ON i.PayslipId = s.PayslipId
JOIN dbo.Payslip n ON n.EmployeeId = s.EmployeeId AND n.PayslipId <> s.PayslipId
JOIN dbo.Payroll p ON p.PayrollId = n.PayrollId JOIN dbo.AccountingPeriod a ON a.PeriodId = p.PeriodId
WHERE s.PayrollId = @pay9 AND a.PeriodMonth IN ('2026-05', '2026-06', '2026-07', '2026-10');

UPDATE dbo.AccountingPeriod SET Status = 'LOCKED', LockedAt = '2026-06-05 09:00:00' WHERE PeriodMonth = '2026-05';

DECLARE @p10 INT = (SELECT PeriodId FROM dbo.AccountingPeriod WHERE PeriodMonth = '2026-10');
INSERT dbo.MonthlyCost (PeriodId, CostCategoryCode, CostType, ProductId, Amount, Note) VALUES
 (@p10, 'MEALS', 'FIXED', NULL, 3000000, N'Tiền ăn'), (@p10, 'FUEL', 'FIXED', NULL, 2000000, N'Xăng xe'),
 (@p10, 'UTILITIES', 'FIXED', NULL, 3000000, N'Điện nước'), (@p10, 'PREMISES', 'FIXED', NULL, 3000000, N'Mặt bằng'),
 (@p10, 'MACHINE_REPAIR', 'FIXED', NULL, 1000000, N'Sửa máy'),
 (@p10, 'MACHINE_SUPPLIES', 'VARIABLE', @pAo, 1500000, N'Vật tư máy cho áo khoác'),
 (@p10, 'MATERIALS_BOUGHT', 'VARIABLE', @pAo, 2000000, N'NVL mua thêm cho áo khoác'),
 (@p10, 'OTHER', 'VARIABLE', @pQu, 5000000, N'Chi phí khác của quần short');

-- Một lần tính hòa vốn tháng 10 theo công thức của FinanceService (phân bổ theo sản lượng): áo khoác có lãi, quần short nguy cơ lỗ
;WITH rev AS (
    SELECT ProductId, SUM(FinishedQty) AS Qty, SUM(Revenue) AS Revenue FROM dbo.vw_MonthlyRevenue WHERE PeriodMonth = '2026-10' GROUP BY ProductId
), calc AS (
    SELECT r.ProductId, r.Qty, r.Revenue, ISNULL(c.Variable, 0) AS Variable,
           ROUND(r.Revenue / r.Qty, 2) AS Cmt,
           ROUND(f.Fixed * r.Qty / t.TotalQty, 2) AS Allocated
    FROM rev r
    CROSS JOIN (SELECT SUM(Qty) AS TotalQty FROM rev) t
    CROSS JOIN (SELECT SUM(Amount) AS Fixed FROM dbo.MonthlyCost WHERE PeriodId = @p10 AND CostType = 'FIXED') f
    OUTER APPLY (SELECT SUM(Amount) AS Variable FROM dbo.MonthlyCost WHERE PeriodId = @p10 AND CostType = 'VARIABLE' AND ProductId = r.ProductId) c
), be AS (
    SELECT *, CASE WHEN Cmt <= Variable / Qty THEN NULL ELSE ROUND(Allocated / (Cmt - Variable / Qty), 2) END AS BreakEvenQty FROM calc
)
INSERT dbo.BreakEvenResult (PeriodId, ProductId, AllocationBasis, FinishedQty, CmtUnitPrice, Revenue, VariableCost, AllocatedFixedCost, BreakEvenQty, PercentReached, SurplusValue, IsAtRisk, CalculatedAt)
SELECT @p10, ProductId, 'OUTPUT', Qty, Cmt, Revenue, Variable, Allocated, BreakEvenQty,
       CASE WHEN BreakEvenQty IS NULL THEN NULL WHEN BreakEvenQty = 0 THEN 100 ELSE ROUND(Qty / BreakEvenQty * 100, 2) END,
       ROUND(Revenue - Variable - Allocated, 2), IIF(Revenue - Variable - Allocated < 0, 1, 0), '2026-11-01 16:00:00'
FROM be;

/* ---------- 12. Thông báo: cấp trên gửi theo tổ, theo vai trò; hệ thống tự tạo đủ nguồn, có cái đã đọc ---------- */
INSERT dbo.Announcement (Content, Priority, TargetScope, SenderId, SentAt) VALUES
 (N'[DEMO] Tổ may 1 họp đầu ca sáng thứ Ba 03/11 để phổ biến quy cách đơn DEMO-DH003.', 'NORMAL', 'TEAM', @quandoc, '2026-11-02 10:00:00'),
 (N'[DEMO] Tổ trưởng kiểm tra lại phân công công đoạn trước khi nhận BTP đợt mới.', 'URGENT', 'ROLE', @admin, '2026-11-02 11:00:00');
DECLARE @aTeam INT = (SELECT AnnouncementId FROM dbo.Announcement WHERE TargetScope = 'TEAM' AND Content LIKE N'[[]DEMO] Tổ may 1%'),
        @aRole INT = (SELECT AnnouncementId FROM dbo.Announcement WHERE TargetScope = 'ROLE' AND Content LIKE N'[[]DEMO] Tổ trưởng%');
INSERT dbo.AnnouncementTarget (AnnouncementId, TeamId) VALUES (@aTeam, @may1);
INSERT dbo.AnnouncementTarget (AnnouncementId, RoleId) VALUES (@aRole, @rLeader);
INSERT dbo.Notification (RecipientId, SourceType, RefTable, RefId, Title, Message, LinkUrl, PushStatus, CreatedAt)
SELECT e.EmployeeId, 'ANNOUNCEMENT', 'Announcement', a.AnnouncementId, IIF(a.Priority = 'URGENT', N'Thông báo khẩn', N'Thông báo'), a.Content, '/notifications', 'SKIPPED', a.SentAt
FROM dbo.Announcement a
JOIN dbo.Employee e ON e.Status = 'ACTIVE' AND e.EmployeeId <> a.SenderId
 AND ((a.AnnouncementId = @aTeam AND e.TeamId = @may1) OR (a.AnnouncementId = @aRole AND e.RoleId = @rLeader))
WHERE a.AnnouncementId IN (@aTeam, @aRole);

INSERT dbo.Notification (RecipientId, SourceType, Title, Message, LinkUrl, ReadAt, PushStatus, CreatedAt) VALUES
 (@thukho,  'WAREHOUSE',  N'Có phiếu xuất chờ xuất kho', N'Phiếu XH-DEMO-210 của đơn DEMO-DH010 đang chờ bạn xuất kho.', '/warehouse/dispatch-notes', NULL, 'SKIPPED', '2026-11-01 09:05:00'),
 (@thukho,  'REQUEST',    N'Có đơn mới cần xử lý', N'Đơn YC-DEMO-006 (Vật tư máy) đã hoàn thành.', '/requests/inbox', '2026-10-19 16:00:00', 'SKIPPED', '2026-10-19 15:00:00'),
 (@quandoc, 'REQUEST',    N'Có đơn mới cần xử lý', N'Đơn YC-DEMO-011 (NVL lỗi) chờ bạn duyệt.', '/requests/inbox', NULL, 'SKIPPED', '2026-11-01 10:00:00'),
 (@quandoc, 'WAREHOUSE',  N'Thiếu NVL khi nhập', N'Phiếu PN-DEMO-013 thiếu 30 m vải, hãy chọn giao bù hoặc không bù.', '/warehouse/short-deliveries', NULL, 'SKIPPED', '2026-10-28 10:00:00'),
 (@quandoc, 'SYSTEM',     N'Đơn hàng có nguy cơ trễ hạn', N'Đơn DEMO-DH010 có nguy cơ trễ hạn giao 05/11.', '/admin/orders', NULL, 'SKIPPED', '2026-11-01 17:00:00'),
 (@ttmay,   'PRODUCTION', N'Công nhân phản đối phiếu hàng hỏng', N'CNMAY1 phản đối phiếu MAY1-261102-901.', '/defect-records', NULL, 'SKIPPED', '2026-11-02 09:30:00'),
 (@ttmay,   'REQUEST',    N'Đơn đã được xử lý', N'Đơn YC-DEMO-013 bị từ chối.', '/requests', '2026-10-24 10:00:00', 'SKIPPED', '2026-10-24 09:00:00'),
 (@w1,      'PRODUCTION', N'Bạn có phiếu hàng hỏng', N'Phiếu MAY1-261030-901: 2 sản phẩm hủy ở công đoạn OP02.', '/my-defects', NULL, 'SKIPPED', '2026-10-30 15:00:00'),
 (@w2,      'PRODUCTION', N'Bạn được giao việc', N'Tổ trưởng ghi giao 5 lần công đoạn OP06 và đã xác nhận hộ.', '/my-piece-movements', '2026-10-30 09:00:00', 'SKIPPED', '2026-10-30 08:30:00'),
 (@admin,   'SYSTEM',     N'Có đề xuất bậc thợ chờ duyệt', N'Tổ trưởng đề xuất CNMAY2 lên bậc B.', '/hr/grades', NULL, 'SKIPPED', '2026-10-30 09:00:00');

DROP TABLE #Ch;
COMMIT TRANSACTION;
PRINT N'Đã thêm dữ liệu demo 19.7.';
GO

/* =====================================================================================
   19.8 Dữ liệu demo để thử nhập sản lượng theo nhóm công đoạn (02/11/2026)
   ---------------------------------------------------------------------------------
   Nhóm "May túi" của DEMO-AO01, công nhân CNMAY1, đơn DEMO-DH003 dòng Đen L:
   - 11 công đoạn có đơn giá đã duyệt và đã giao việc 10 lần: nhập được.
   - OP91 có đơn giá nhưng chưa giao việc; OP90 đã phân công nhưng chưa có đơn giá: chưa giao việc nên không hiện trong khung nhập.
   Chạy lại không nhân đôi (bỏ qua nếu CNMAY1 đã có giao việc OP12 ở dòng này).
   Chạy trên DB đang có: sqlcmd -S localhost -E -C -I -f 65001 -d NXMTD -i <file này>
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @p INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-AO01'),
        @o3 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH003'),
        @may1 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY1'),
        @w1 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY1');
DECLARE @line INT = (SELECT l.OrderLineId FROM dbo.OrderLine l JOIN dbo.ProductVariant v ON v.ProductVariantId = l.ProductVariantId
                     JOIN dbo.ProductColor pcol ON pcol.ProductColorId = v.ProductColorId
                     WHERE l.OrderId = @o3 AND l.ProductId = @p AND pcol.Color = N'Đen' AND v.SizeCode = 'L');

IF @line IS NULL OR @w1 IS NULL
BEGIN
    PRINT N'Chưa có dữ liệu demo 19.3 (DEMO-DH003), bỏ qua 19.8.';
    RETURN;
END;
IF EXISTS (SELECT 1 FROM dbo.WorkAllocation a JOIN dbo.Operation o ON o.OperationId = a.OperationId
           WHERE a.WorkerId = @w1 AND a.OrderLineId = @line AND o.ProductId = @p AND o.OperationCode = 'OP12')
BEGIN
    PRINT N'Dữ liệu demo 19.8 đã có, bỏ qua.';
    RETURN;
END;

BEGIN TRANSACTION;

SELECT o.OperationId, o.OperationCode INTO #G
FROM dbo.Operation o JOIN dbo.OperationGroup g ON g.OperationGroupId = o.OperationGroupId
WHERE o.ProductId = @p AND g.GroupName = N'May túi' AND o.Status = 'ACTIVE';

INSERT dbo.PieceRate (OperationId, Rate, EffectiveDate, Status, SubmittedAt, ApprovedAt)
SELECT g.OperationId, 1200, '2026-10-01', 'APPROVED', '2026-09-28 09:00:00', '2026-09-29 09:00:00'
FROM #G g
WHERE g.OperationCode <> 'OP90'
  AND NOT EXISTS (SELECT 1 FROM dbo.PieceRate r WHERE r.OperationId = g.OperationId AND r.EffectiveDate = '2026-10-01')
  AND NOT EXISTS (SELECT 1 FROM dbo.PieceRate r WHERE r.OperationId = g.OperationId AND r.Status = 'APPROVED' AND r.EffectiveDate <= '2026-11-02');

INSERT dbo.OperationAssignment (SewingTeamId, OrderId, OperationId, WorkerId, ValidFrom, AssignedAt)
SELECT @may1, @o3, g.OperationId, @w1, '2026-10-25', '2026-10-25 08:00:00'
FROM #G g
WHERE NOT EXISTS (SELECT 1 FROM dbo.OperationAssignment a WHERE a.SewingTeamId = @may1 AND a.OrderId = @o3
                  AND a.OperationId = g.OperationId AND a.WorkerId = @w1 AND a.ValidTo IS NULL);

INSERT dbo.WorkAllocation (MovementType, SewingTeamId, WorkerId, OrderLineId, OperationId, Quantity, ConfirmedQty, ConfirmMode, Status, CreatedAt, ConfirmedAt)
SELECT 'HANDOVER', @may1, @w1, @line, g.OperationId, 10, 10, 'WORKER', 'CONFIRMED', '2026-10-30 08:00:00', '2026-10-30 08:15:00'
FROM #G g WHERE g.OperationCode NOT IN ('OP90', 'OP91');

DROP TABLE #G;
COMMIT TRANSACTION;
PRINT N'Đã thêm dữ liệu demo 19.8.';
GO

/* ---------- Chia chỉ tiêu KPI demo theo màu × size (DEV-ALL-62) ----------
   Số tổng của chỉ tiêu chia cho các dòng của mã hàng đã chia cho tổ, theo tỉ lệ số còn trống của dòng
   (số đặt − phần đã giao ở chỉ tiêu khác). Không đủ chỗ thì để nguyên. Giống SQL/patches/2026-11-03_all_changes.sql. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @k INT = 0, @team INT, @order INT, @product INT, @qty INT;
WHILE 1 = 1
BEGIN
    SELECT TOP (1) @k = k.KpiTargetId, @team = k.SewingTeamId, @order = k.OrderId, @product = k.ProductId, @qty = k.TargetFinishedQty
    FROM dbo.KpiTarget k
    WHERE k.KpiTargetId > @k AND k.Status <> 'CANCELLED'
      AND NOT EXISTS (SELECT 1 FROM dbo.KpiTargetLine x WHERE x.KpiTargetId = k.KpiTargetId)
    ORDER BY k.KpiTargetId;
    IF @@ROWCOUNT = 0 BREAK;

    WITH f AS (
        SELECT l.OrderLineId, CAST(l.OrderedQty - ISNULL(u.Used, 0) AS BIGINT) AS Free
        FROM dbo.OrderLine l
        OUTER APPLY (SELECT SUM(x.TargetQty) AS Used
                     FROM dbo.KpiTargetLine x JOIN dbo.KpiTarget o ON o.KpiTargetId = x.KpiTargetId
                     WHERE x.OrderLineId = l.OrderLineId AND o.Status <> 'CANCELLED') u
        WHERE l.OrderId = @order AND l.ProductId = @product
          AND EXISTS (SELECT 1 FROM dbo.OrderLineTeam t WHERE t.OrderLineId = l.OrderLineId AND t.SewingTeamId = @team)
    ), g AS (
        SELECT OrderLineId, @qty * Free / SUM(Free) OVER () AS Base, SUM(Free) OVER () AS TotalFree,
               ROW_NUMBER() OVER (ORDER BY OrderLineId) AS Rn
        FROM f WHERE Free > 0
    ), h AS (
        SELECT OrderLineId, Base, TotalFree, Rn, @qty - SUM(Base) OVER () AS Rem FROM g
    )
    INSERT dbo.KpiTargetLine (KpiTargetId, OrderLineId, TargetQty)
    SELECT @k, OrderLineId, CAST(Base + IIF(Rn <= Rem, 1, 0) AS INT)
    FROM h
    WHERE TotalFree >= @qty AND Base + IIF(Rn <= Rem, 1, 0) > 0;
END;
COMMIT TRANSACTION;
PRINT N'Đã chia chỉ tiêu KPI demo theo màu × size.';
GO

/* ---------- Bìa cắt theo đơn (DEV-ALL-85) ----------
   Chi tiết trên nhóm bìa của mã hàng demo; mỗi đơn demo có DEMO-AO01 / DEMO-QU02 một bìa <nhóm>A mỗi nhóm, tỉ lệ theo size có
   trong đơn (áo S1 M2 L1, quần M1 L1), chiều dài = số sản phẩm / lớp × (áo: thân 0,55 m, tay 0,40 m; quần 0,35 m).
   DEMO-DH001 thêm bìa 1B (thân, chỉ M, M4, 2,10 m) để thử chọn bìa. Chạy lại không thêm trùng. */
SET NOCOUNT ON;
INSERT dbo.ProductMarkerComponent (ProductId, MarkerNo, ProductComponentId)
SELECT pc.ProductId, t.MarkerNo, pc.ProductComponentId
FROM (VALUES ('DEMO-AO01', N'Thân trước', 1), ('DEMO-AO01', N'Thân sau', 1), ('DEMO-AO01', N'Tay áo', 2),
             ('DEMO-QU02', N'Ống quần', 1)) t (ProductCode, ComponentName, MarkerNo)
JOIN dbo.Product p           ON p.ProductCode = t.ProductCode
JOIN dbo.ProductComponent pc ON pc.ProductId = p.ProductId AND pc.ComponentName = t.ComponentName
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductMarkerComponent x
                  WHERE x.ProductId = pc.ProductId AND x.MarkerNo = t.MarkerNo AND x.ProductComponentId = pc.ProductComponentId);

DECLARE @plan TABLE (ProductCode VARCHAR(30), MarkerNo TINYINT, SizeCode VARCHAR(10), Pieces SMALLINT, MetersPerPiece DECIMAL(5,2));
INSERT @plan VALUES
 ('DEMO-AO01', 1, 'S', 1, 0.55), ('DEMO-AO01', 1, 'M', 2, 0.55), ('DEMO-AO01', 1, 'L', 1, 0.55),
 ('DEMO-AO01', 2, 'S', 1, 0.40), ('DEMO-AO01', 2, 'M', 2, 0.40), ('DEMO-AO01', 2, 'L', 1, 0.40),
 ('DEMO-QU02', 1, 'M', 1, 0.35), ('DEMO-QU02', 1, 'L', 1, 0.35);
DECLARE @present TABLE (OrderId INT, ProductId INT, MarkerNo TINYINT, SizeCode VARCHAR(10), Pieces SMALLINT, MetersPerPiece DECIMAL(5,2));
INSERT @present
SELECT DISTINCT ol.OrderId, ol.ProductId, pl.MarkerNo, pl.SizeCode, pl.Pieces, pl.MetersPerPiece
FROM dbo.OrderLine ol
JOIN dbo.CustomerOrder o   ON o.OrderId = ol.OrderId
JOIN dbo.Product p         ON p.ProductId = ol.ProductId
JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
JOIN @plan pl              ON pl.ProductCode = p.ProductCode AND pl.SizeCode = pv.SizeCode
WHERE o.OrderCode LIKE 'DEMO-%'
  AND EXISTS (SELECT 1 FROM dbo.ProductMarkerFabric f WHERE f.ProductId = ol.ProductId AND f.MarkerNo = pl.MarkerNo);

INSERT dbo.ProductMarker (OrderId, ProductId, MarkerNo, MarkerCode, LengthM)
SELECT x.OrderId, x.ProductId, x.MarkerNo, CONCAT(x.MarkerNo, 'A'), SUM(x.Pieces * x.MetersPerPiece)
FROM @present x
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductMarker m WHERE m.OrderId = x.OrderId AND m.ProductId = x.ProductId AND m.MarkerNo = x.MarkerNo)
GROUP BY x.OrderId, x.ProductId, x.MarkerNo;
INSERT dbo.ProductMarkerRatio (ProductMarkerId, SizeCode, PiecesPerPly)
SELECT m.ProductMarkerId, x.SizeCode, x.Pieces
FROM @present x
JOIN dbo.ProductMarker m ON m.OrderId = x.OrderId AND m.ProductId = x.ProductId AND m.MarkerNo = x.MarkerNo
                        AND m.MarkerCode = CONCAT(x.MarkerNo, 'A')
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductMarkerRatio r WHERE r.ProductMarkerId = m.ProductMarkerId AND r.SizeCode = x.SizeCode);

INSERT dbo.ProductMarker (OrderId, ProductId, MarkerNo, MarkerCode, LengthM)
SELECT DISTINCT x.OrderId, x.ProductId, 1, '1B', 2.10
FROM @present x
JOIN dbo.CustomerOrder o ON o.OrderId = x.OrderId
JOIN dbo.Product p       ON p.ProductId = x.ProductId
WHERE o.OrderCode = 'DEMO-DH001' AND p.ProductCode = 'DEMO-AO01' AND x.MarkerNo = 1 AND x.SizeCode = 'M'
  AND NOT EXISTS (SELECT 1 FROM dbo.ProductMarker m WHERE m.OrderId = x.OrderId AND m.ProductId = x.ProductId AND m.MarkerCode = '1B');
INSERT dbo.ProductMarkerRatio (ProductMarkerId, SizeCode, PiecesPerPly)
SELECT m.ProductMarkerId, 'M', 4
FROM dbo.ProductMarker m
JOIN dbo.CustomerOrder o ON o.OrderId = m.OrderId
WHERE o.OrderCode = 'DEMO-DH001' AND m.MarkerCode = '1B'
  AND NOT EXISTS (SELECT 1 FROM dbo.ProductMarkerRatio r WHERE r.ProductMarkerId = m.ProductMarkerId);
DECLARE @markers INT = (SELECT COUNT(*) FROM dbo.ProductMarker);
PRINT N'Bìa cắt theo đơn (demo): ' + CAST(@markers AS NVARCHAR(10)) + N' bìa.';
GO

PRINT N'NX-MTD: tạo database xong.';
GO
