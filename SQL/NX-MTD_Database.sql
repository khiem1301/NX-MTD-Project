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
                 MaterialId + MaterialColor, FK_POL_Material / FK_MRL_Material, CK_POL_Target /
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

-- Bậc thợ A–D và tiền bậc theo tháng, có ngày hiệu lực; giữ phiên bản cũ (GRADE_LEVEL, UC15)
CREATE TABLE dbo.GradeLevel (
    GradeLevelId     INT IDENTITY(1,1) CONSTRAINT PK_GradeLevel PRIMARY KEY,
    GradeCode        CHAR(1)       NOT NULL CONSTRAINT CK_GradeLevel_Code CHECK (GradeCode IN ('A','B','C','D')),
    GradeName        NVARCHAR(50)  NOT NULL,
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
    CurrentGradeCode CHAR(1)       NULL CONSTRAINT CK_Employee_Grade CHECK (CurrentGradeCode IN ('A','B','C','D')), -- chỉ CN may, tổ phó & tổ trưởng
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
    CurrentGradeCode  CHAR(1)       NULL,
    ProposedGradeCode CHAR(1)       NOT NULL,
    Reason            NVARCHAR(500) NOT NULL,
    RecentDefectCount INT           NOT NULL CONSTRAINT DF_GradeEval_Defects DEFAULT 0,
    EvaluationDate    DATE          NOT NULL,
    Status            VARCHAR(20)   NOT NULL CONSTRAINT DF_GradeEval_Status DEFAULT 'SUBMITTED',
    EffectiveDate     DATE          NULL,     -- ngày bậc mới có hiệu lực (khi APPLIED)
    ConsideredAt      DATETIME2(0)  NULL,
    DecisionNote      NVARCHAR(500) NULL,
    CreatedAt         DATETIME2(0)  NOT NULL CONSTRAINT DF_GradeEval_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_GradeEval_Grades CHECK (ProposedGradeCode IN ('A','B','C','D') AND (CurrentGradeCode IS NULL OR CurrentGradeCode IN ('A','B','C','D'))),
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
    DefaultAddress  NVARCHAR(500) NULL
);

-- Nguyên vật liệu (tạo ngay khi nhập phiếu nhập – UC38). Người dùng chỉ chọn Category (loại chi tiết);
-- MaterialType (FABRIC: vải, đi qua tổ cắt; TRIM: phụ liệu, cấp thẳng cho tổ may) là cột tính từ Category (22/10)
CREATE TABLE dbo.Material (
    MaterialId    INT IDENTITY(1,1) CONSTRAINT PK_Material PRIMARY KEY,
    MaterialCode  VARCHAR(30)   NOT NULL CONSTRAINT UQ_Material_Code UNIQUE,
    MaterialName  NVARCHAR(200) NOT NULL,
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
    CONSTRAINT UQ_Material_NameType UNIQUE (MaterialName, MaterialType)
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

-- Biến thể của mã hàng (23/10): một màu × một size, vd Áo khoác – Xanh – S.
-- OrderLine trỏ về đây bằng ProductVariantId nên đơn chỉ dùng được biến thể đã khai báo định mức.
CREATE TABLE dbo.ProductVariant (
    ProductVariantId  INT IDENTITY(1,1) CONSTRAINT PK_ProductVariant PRIMARY KEY,
    ProductId         INT           NOT NULL CONSTRAINT FK_PV_Product REFERENCES dbo.Product(ProductId),
    Color             NVARCHAR(50)  NOT NULL,        -- màu sản phẩm
    SizeCode          VARCHAR(10)   NOT NULL,        -- XS, S, M, L, XL, XXL, 3XL, FREE…
    Status            VARCHAR(20)   NOT NULL CONSTRAINT DF_PV_Status DEFAULT 'ACTIVE',
    CONSTRAINT CK_PV_Status CHECK (Status IN ('ACTIVE','DISCONTINUED')),
    CONSTRAINT UQ_PV UNIQUE (ProductId, Color, SizeCode),
    CONSTRAINT UQ_PV_IdProduct UNIQUE (ProductVariantId, ProductId)   -- cho FK ghép ở ProductComponentMaterial và OrderLine
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
    SortOrder           SMALLINT      NOT NULL CONSTRAINT DF_PC_Sort DEFAULT 0,
    Status              VARCHAR(20)   NOT NULL CONSTRAINT DF_PC_Status DEFAULT 'ACTIVE',
    CONSTRAINT CK_PC_Type   CHECK (ComponentType IN ('PANEL','TRIM')),
    CONSTRAINT CK_PC_Pieces CHECK (PiecesPerGarment > 0),
    CONSTRAINT CK_PC_Status CHECK (Status IN ('ACTIVE','DISCONTINUED')),
    CONSTRAINT UQ_PC UNIQUE (ProductId, ComponentName),
    CONSTRAINT UQ_PC_IdProduct UNIQUE (ProductComponentId, ProductId)  -- cho FK ghép ở ProductComponentMaterial
);

-- Định mức NVL theo biến thể (23/10, thay OrderMaterialNorm): với mỗi biến thể (màu × size) và mỗi thành phần,
-- dùng NVL nào, màu NVL nào, bao nhiêu cho 1 sản phẩm, kích thước chi tiết. Một thành phần có thể dùng nhiều NVL
-- (vd cổ áo = vải chính + mex). Ghi tường minh từng biến thể, không dùng NULL = "mọi màu / mọi size";
-- màn hình nhập có nút "áp dụng cho các size / màu khác" để đỡ nhập tay.
-- ProductId lặp lại để FK ghép bảo đảm biến thể và thành phần cùng một mã hàng.
CREATE TABLE dbo.ProductComponentMaterial (
    ProductComponentMaterialId INT IDENTITY(1,1) CONSTRAINT PK_ProductComponentMaterial PRIMARY KEY,
    ProductId           INT            NOT NULL,
    ProductVariantId    INT            NOT NULL,
    ProductComponentId  INT            NOT NULL,
    MaterialId          INT            NOT NULL CONSTRAINT FK_PCM_Material REFERENCES dbo.Material(MaterialId),
    MaterialColor       NVARCHAR(50)   NOT NULL CONSTRAINT DF_PCM_Color DEFAULT N'',  -- '' = NVL không phân màu; khớp OrderMaterial.MaterialColor
    QtyPerGarment       DECIMAL(18,4)  NOT NULL,     -- tổng cho 1 sản phẩm (đã tính số chi tiết), theo đơn vị của NVL: m vải, cuộn chỉ, cái khóa…
    WastagePercent      DECIMAL(5,2)   NOT NULL CONSTRAINT DF_PCM_Wastage DEFAULT 0,
    LengthCm            DECIMAL(7,2)   NULL,         -- kích thước chi tiết (nếu cần), vd dài tay áo
    WidthCm             DECIMAL(7,2)   NULL,
    SpecNote            NVARCHAR(200)  NULL,         -- ghi chú quy cách khác
    CONSTRAINT FK_PCM_Variant   FOREIGN KEY (ProductVariantId, ProductId)   REFERENCES dbo.ProductVariant(ProductVariantId, ProductId),
    CONSTRAINT FK_PCM_Component FOREIGN KEY (ProductComponentId, ProductId) REFERENCES dbo.ProductComponent(ProductComponentId, ProductId),
    CONSTRAINT CK_PCM_Qty     CHECK (QtyPerGarment > 0),
    CONSTRAINT CK_PCM_Wastage CHECK (WastagePercent BETWEEN 0 AND 100),
    CONSTRAINT CK_PCM_Size    CHECK ((LengthCm IS NULL OR LengthCm > 0) AND (WidthCm IS NULL OR WidthCm > 0)),
    CONSTRAINT UQ_PCM UNIQUE (ProductVariantId, ProductComponentId, MaterialId)
);
CREATE INDEX IX_PCM_Material ON dbo.ProductComponentMaterial(MaterialId, MaterialColor);

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

-- NVL của đơn (18/10): mỗi dòng = một NVL + màu mà đơn cần. Là bảng trung tâm của phần NVL:
-- phần khách cấp (màn 1.2), dòng đơn mua và dòng phiếu nhập đều trỏ về đây.
-- 23/10: định mức chuyển sang ProductComponentMaterial (theo mã hàng). Hai bên khớp theo (MaterialId, MaterialColor),
-- không có FK; số theo định mức để đối chiếu với RequiredQty xem vw_OrderMaterialNorm.
-- Nguồn suy ra từ số lượng: CustomerQty = 0 là tự mua, CustomerQty >= RequiredQty là khách cấp, còn lại là cả hai.
CREATE TABLE dbo.OrderMaterial (
    OrderMaterialId      INT IDENTITY(1,1) CONSTRAINT PK_OrderMaterial PRIMARY KEY,
    OrderId              INT            NOT NULL CONSTRAINT FK_OM_Order REFERENCES dbo.CustomerOrder(OrderId),
    MaterialId           INT            NOT NULL CONSTRAINT FK_OM_Material REFERENCES dbo.Material(MaterialId),
    MaterialColor        NVARCHAR(50)   NOT NULL CONSTRAINT DF_OM_Color DEFAULT N'',
    RequiredQty          DECIMAL(18,3)  NOT NULL,                                        -- tổng cần theo số liệu khách báo, nhập tay (không tính từ định mức)
    CustomerQty          DECIMAL(18,3)  NOT NULL CONSTRAINT DF_OM_Customer DEFAULT 0,   -- khách sẽ cấp (màn 1.2)
    ExpectedDeliveryCount SMALLINT      NULL,                                            -- số lần khách dự kiến giao NVL này; số lần đã nhận xem vw_OrderMaterialDelivery
    PurchaseQty          AS (CASE WHEN RequiredQty > CustomerQty THEN RequiredQty - CustomerQty ELSE 0 END), -- phần xưởng phải mua
    Source               AS (CONVERT(VARCHAR(10), CASE WHEN CustomerQty = 0 THEN 'SUPPLIER'
                                                       WHEN CustomerQty >= RequiredQty THEN 'CUSTOMER' ELSE 'BOTH' END)),
    Note                 NVARCHAR(500)  NULL,
    CONSTRAINT CK_OM_Qty  CHECK (RequiredQty >= 0 AND CustomerQty >= 0),
    CONSTRAINT CK_OM_Delivery CHECK ((CustomerQty = 0 AND ExpectedDeliveryCount IS NULL) OR (CustomerQty > 0 AND ExpectedDeliveryCount IS NOT NULL AND ExpectedDeliveryCount >= 1)), -- khách có cấp thì phải khai số lần giao
    CONSTRAINT UQ_OM UNIQUE (OrderId, MaterialId, MaterialColor)
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
    StationSequence  SMALLINT      NOT NULL,
    SkillLevel       TINYINT       NULL,
    StandardTimeSec  DECIMAL(9,2)  NULL CONSTRAINT CK_Operation_Time CHECK (StandardTimeSec IS NULL OR StandardTimeSec > 0),
    TeamType         VARCHAR(10)   NOT NULL CONSTRAINT DF_Operation_TeamType DEFAULT 'SEWING',
    IsFinalStep      BIT           NOT NULL CONSTRAINT DF_Operation_Final DEFAULT 0,
    UsesSemiFinished BIT           NOT NULL CONSTRAINT DF_Operation_Semi DEFAULT 0,   -- 1 = giao việc công đoạn này kèm BTP (25/10)
    Status           VARCHAR(20)   NOT NULL CONSTRAINT DF_Operation_Status DEFAULT 'ACTIVE',
    CONSTRAINT CK_Operation_TeamType CHECK (TeamType IN ('CUTTING','SEWING')),
    CONSTRAINT CK_Operation_Status   CHECK (Status IN ('ACTIVE','DISCONTINUED')),
    CONSTRAINT UQ_Operation UNIQUE (ProductId, OperationCode),
    CONSTRAINT UQ_Operation_IdProduct UNIQUE (OperationId, ProductId),   -- đích của FK ghép từ OperationComponent
    CONSTRAINT FK_Operation_Group FOREIGN KEY (OperationGroupId, ProductId) REFERENCES dbo.OperationGroup(OperationGroupId, ProductId) -- nhóm cùng mã hàng
);
CREATE INDEX IX_Operation_Group ON dbo.Operation(OperationGroupId) WHERE OperationGroupId IS NOT NULL;

-- Công đoạn làm trên chi tiết nào (25/10), nhiều–nhiều: "Lộn tay" → Tay áo; "Tra tay" → Tay áo + Thân;
-- công đoạn không gắn chi tiết (là, đi lấy hàng, kiểm hàng) thì không có dòng. Công đoạn và chi tiết phải cùng mã hàng
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
    Rate           DECIMAL(18,2) NOT NULL CONSTRAINT CK_PieceRate_Rate CHECK (Rate >= 0),
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
    CONSTRAINT CK_Kpi_Result CHECK (Result IS NULL OR Result IN ('ACHIEVED','NOT_ACHIEVED')),
    CONSTRAINT UQ_Kpi UNIQUE (OrderId, ProductId, SewingTeamId, StartDate)
);
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
        'MATERIAL_SUPPLEMENT','DEFECTIVE_MATERIAL','REPLACEMENT_CUTTING','SHORT_DELIVERY','MATERIAL_REQUISITION','SEMI_FINISHED_REQUISITION')),
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
    MaterialColor     NVARCHAR(50)   NULL,
    OrderLineId       INT            NULL CONSTRAINT FK_RequestLine_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    ProductComponentMaterialId INT   NULL CONSTRAINT FK_RequestLine_PCM REFERENCES dbo.ProductComponentMaterial(ProductComponentMaterialId), -- chi tiết × vải cần cắt bù (FR 3.10); 25/10 thay ProductComponentId
    ItemDescription   NVARCHAR(300)  NULL,   -- linh kiện / máy / vật tư máy (đơn cơ điện)
    Quantity          DECIMAL(18,3)  NOT NULL CONSTRAINT CK_RequestLine_Qty CHECK (Quantity > 0),
    ReplenishQty      DECIMAL(18,3)  NULL,   -- SHORT_DELIVERY: số NCC/khách sẽ bù (Quản đốc nhập)
    MaterialReceiptLineId INT        NULL,   -- SHORT_DELIVERY: dòng phiếu nhập bị thiếu / lỗi (Quantity = số thiếu / lỗi báo) – FK thêm sau
    CONSTRAINT CK_RequestLine_Material CHECK ((MaterialId IS NULL AND MaterialColor IS NULL) OR (MaterialId IS NOT NULL AND MaterialColor IS NOT NULL)),
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
    CONSTRAINT CK_PO_DeliveryCount CHECK (ExpectedDeliveryCount IS NULL OR ExpectedDeliveryCount >= 1)
);

-- Dòng đơn mua xác định NVL theo một trong hai cách (TH-12):
--   OrderMaterialId (NVL + màu của đơn), hoặc OrderMaterialId NULL + MaterialId + MaterialColor (đơn mua dùng chung)
CREATE TABLE dbo.PurchaseOrderLine (
    PurchaseOrderLineId  INT IDENTITY(1,1) CONSTRAINT PK_PurchaseOrderLine PRIMARY KEY,
    PurchaseOrderId      INT           NOT NULL CONSTRAINT FK_POL_PO REFERENCES dbo.PurchaseOrder(PurchaseOrderId),
    OrderMaterialId      INT           NULL CONSTRAINT FK_POL_OrderMaterial REFERENCES dbo.OrderMaterial(OrderMaterialId), -- NVL + màu của đơn; NULL = dòng dùng chung
    OrderedQty           DECIMAL(18,3) NOT NULL CONSTRAINT CK_POL_Ordered CHECK (OrderedQty > 0),
    NotReplenishedQty    DECIMAL(18,3) NOT NULL CONSTRAINT DF_POL_NotRepl DEFAULT 0,       -- Quản đốc chốt không bù
    UnitPrice            DECIMAL(18,2) NOT NULL CONSTRAINT DF_POL_Price DEFAULT 0,
    Amount               AS (CONVERT(DECIMAL(18,2), OrderedQty * UnitPrice)),
    MaterialId           INT           NULL CONSTRAINT FK_POL_Material REFERENCES dbo.Material(MaterialId), -- chỉ khi OrderMaterialId NULL
    MaterialColor        NVARCHAR(50)  NULL,
    CONSTRAINT CK_POL_Qty CHECK (NotReplenishedQty >= 0 AND NotReplenishedQty <= OrderedQty), -- đã nhận + không bù <= đặt: backend kiểm tra, số đã nhận xem vw_PurchaseOrderLineStatus
    CONSTRAINT CK_POL_Target CHECK (
        (OrderMaterialId IS NOT NULL AND MaterialId IS NULL)
     OR (OrderMaterialId IS NULL AND MaterialId IS NOT NULL AND MaterialColor IS NOT NULL))
);
-- Unique dạng filtered index để nhiều dòng NULL vẫn hợp lệ
CREATE UNIQUE INDEX UQ_POL       ON dbo.PurchaseOrderLine(PurchaseOrderId, OrderMaterialId) WHERE OrderMaterialId IS NOT NULL;
CREATE UNIQUE INDEX UQ_POL_Stock ON dbo.PurchaseOrderLine(PurchaseOrderId, MaterialId, MaterialColor) WHERE OrderMaterialId IS NULL;

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

-- Dòng phiếu nhập xác định NVL giống PurchaseOrderLine: OrderMaterialId, hoặc MaterialId + MaterialColor (TH-12)
CREATE TABLE dbo.MaterialReceiptLine (
    MaterialReceiptLineId INT IDENTITY(1,1) CONSTRAINT PK_MaterialReceiptLine PRIMARY KEY,
    MaterialReceiptId     INT           NOT NULL CONSTRAINT FK_MRL_Receipt REFERENCES dbo.MaterialReceipt(MaterialReceiptId),
    OrderMaterialId       INT           NULL CONSTRAINT FK_MRL_OrderMaterial REFERENCES dbo.OrderMaterial(OrderMaterialId), -- NVL + màu của đơn; NULL = dòng dùng chung
    PurchaseOrderLineId   INT           NULL CONSTRAINT FK_MRL_POL REFERENCES dbo.PurchaseOrderLine(PurchaseOrderLineId),
    OriginalReceiptLineId INT           NULL CONSTRAINT FK_MRL_OrigLine REFERENCES dbo.MaterialReceiptLine(MaterialReceiptLineId),
    AcceptedQty           DECIMAL(18,3) NOT NULL CONSTRAINT DF_MRL_Accepted DEFAULT 0,  -- số đạt nhập kho; thiếu so với đơn mua xem vw_PurchaseOrderLineStatus
    MaterialId            INT           NULL CONSTRAINT FK_MRL_Material REFERENCES dbo.Material(MaterialId), -- chỉ khi OrderMaterialId NULL
    MaterialColor         NVARCHAR(50)  NULL,
    CONSTRAINT CK_MRL_Qty CHECK (AcceptedQty >= 0),   -- 0 = cả lô lỗi / không giao, vẫn có dòng để gắn yêu cầu thiếu
    CONSTRAINT CK_MRL_Target CHECK (
        (OrderMaterialId IS NOT NULL AND MaterialId IS NULL)
     OR (OrderMaterialId IS NULL AND MaterialId IS NOT NULL AND MaterialColor IS NOT NULL))
);
CREATE UNIQUE INDEX UQ_MRL       ON dbo.MaterialReceiptLine(MaterialReceiptId, OrderMaterialId) WHERE OrderMaterialId IS NOT NULL;
CREATE UNIQUE INDEX UQ_MRL_Stock ON dbo.MaterialReceiptLine(MaterialReceiptId, MaterialId, MaterialColor) WHERE OrderMaterialId IS NULL;
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

-- Số cần cắt theo màu × size. Cắt bù từng chi tiết: ProductComponentMaterialId = dòng định mức (chi tiết PANEL × NVL × màu
-- của đúng biến thể) cần cắt, PlannedQty = số chi tiết; lệnh cắt thường: NULL = cả bộ, PlannedQty = số sản phẩm (25/10)
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
    MaterialColor       NVARCHAR(50)  NULL,
    OrderLineId         INT           NULL CONSTRAINT FK_MIL_OrderLine REFERENCES dbo.OrderLine(OrderLineId), -- phiếu BTP
    ProductComponentMaterialId INT    NULL CONSTRAINT FK_MIL_PCM REFERENCES dbo.ProductComponentMaterial(ProductComponentMaterialId), -- BTP: NULL = bộ đủ; có = chi tiết × vải cắt bù (25/10)
    IssuedQty           DECIMAL(18,3) NOT NULL CONSTRAINT CK_MIL_Issued CHECK (IssuedQty > 0),
    ConfirmedQty        DECIMAL(18,3) NULL,
    DiscrepancyReason   NVARCHAR(500) NULL,
    CONSTRAINT CK_MIL_Confirmed CHECK (ConfirmedQty IS NULL OR (ConfirmedQty >= 0 AND ConfirmedQty <= IssuedQty)),
    CONSTRAINT CK_MIL_Reason    CHECK (ConfirmedQty IS NULL OR ConfirmedQty = IssuedQty OR DiscrepancyReason IS NOT NULL), -- GBR-06
    CONSTRAINT CK_MIL_Item      CHECK ((MaterialId IS NOT NULL AND MaterialColor IS NOT NULL AND OrderLineId IS NULL)
                                    OR (MaterialId IS NULL AND MaterialColor IS NULL AND OrderLineId IS NOT NULL)),
    CONSTRAINT CK_MIL_Component CHECK (ProductComponentMaterialId IS NULL OR OrderLineId IS NOT NULL),
    CONSTRAINT UQ_MIL UNIQUE (MaterialIssueId, MaterialId, MaterialColor, OrderLineId, ProductComponentMaterialId)
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

-- Bàn cắt (CUTTING_LAY, FR 2.7): số lớp × tỉ lệ size. Tỉ lệ size nằm ở CuttingLayLine.RatioValue;
-- tỉ lệ gần nhất của mã hàng (để điền sẵn) xem vw_ProductLastSizeRatio
CREATE TABLE dbo.CuttingLay (
    CuttingLayId    INT IDENTITY(1,1) CONSTRAINT PK_CuttingLay PRIMARY KEY,
    WorkOrderId     INT            NOT NULL CONSTRAINT FK_CL_WO REFERENCES dbo.WorkOrder(WorkOrderId),
    CuttingTeamId   INT            NOT NULL CONSTRAINT FK_CL_Team REFERENCES dbo.Team(TeamId),
    WorkDate        DATE           NOT NULL,
    ShiftCode       VARCHAR(10)    NOT NULL CONSTRAINT CK_CL_Shift CHECK (ShiftCode IN ('MORNING','AFTERNOON','OVERTIME')),
    LaySequence     SMALLINT       NOT NULL,
    FabricMaterialId INT           NOT NULL CONSTRAINT FK_CL_Material REFERENCES dbo.Material(MaterialId),
    FabricColor     NVARCHAR(50)   NOT NULL,
    PlyCount        SMALLINT       NOT NULL CONSTRAINT CK_CL_Ply CHECK (PlyCount > 0),
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

-- Giao việc / thu hồi việc với công nhân (25/10 thay WorkerPieceMovement; FR 3.5, 3.6, 3.8; UC53, UC54, UC60).
-- Quantity = số lần làm công đoạn × màu × size được giao / thu hồi; tổ trưởng chia tùy ý, miễn tổng số lần đang giao của
-- công đoạn × màu × size không vượt số bộ BTP tổ đã nhận (MaterialIssueLine.ConfirmedQty, THROW 50050).
-- PieceQty = số BTP giao kèm / trả lại, 0 nếu công đoạn không dùng BTP (Operation.UsesSemiFinished = 0, THROW 50051).
-- Màn giao việc điền sẵn PieceQty = Quantity khi công đoạn dùng BTP, tổ trưởng sửa được.
-- Trần sản lượng = Σ ConfirmedQty giao − thu hồi (vw_WorkerOutputCap); tồn BTP của tổ chỉ trừ ConfirmedPieceQty (vw_TeamPieceHolding)
CREATE TABLE dbo.WorkAllocation (
    WorkAllocationId  INT IDENTITY(1,1) CONSTRAINT PK_WorkAllocation PRIMARY KEY,
    MovementType      VARCHAR(10)    NOT NULL,             -- HANDOVER: giao; RETRIEVAL: thu hồi
    SewingTeamId      INT            NOT NULL CONSTRAINT FK_WA_Team REFERENCES dbo.Team(TeamId),
    WorkerId          INT            NOT NULL CONSTRAINT FK_WA_Worker REFERENCES dbo.Employee(EmployeeId),
    OrderLineId       INT            NOT NULL CONSTRAINT FK_WA_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    OperationId       INT            NOT NULL CONSTRAINT FK_WA_Operation REFERENCES dbo.Operation(OperationId),
    Quantity          INT            NOT NULL CONSTRAINT CK_WA_Qty CHECK (Quantity > 0),
    PieceQty          INT            NOT NULL CONSTRAINT DF_WA_Piece DEFAULT 0,
    ConfirmedQty      INT            NULL,                 -- số lần công nhân xác nhận
    ConfirmedPieceQty INT            NULL,                 -- số BTP công nhân xác nhận đã nhận / đã trả
    DiscrepancyReason NVARCHAR(500)  NULL,
    RetrievalReason   VARCHAR(20)    NULL,
    ConfirmMode       VARCHAR(10)    NOT NULL,             -- WORKER: CN tự xác nhận; ON_BEHALF: tổ trưởng xác nhận hộ
    OnBehalfReason    NVARCHAR(500)  NULL,
    Status            VARCHAR(10)    NOT NULL CONSTRAINT DF_WA_Status DEFAULT 'PENDING',
    RejectReason      NVARCHAR(500)  NULL,
    CreatedAt         DATETIME2(0)   NOT NULL CONSTRAINT DF_WA_CreatedAt DEFAULT (dbo.fn_VnNow()),
    ConfirmedAt       DATETIME2(0)   NULL,
    CONSTRAINT CK_WA_Type    CHECK (MovementType IN ('HANDOVER','RETRIEVAL')),
    CONSTRAINT CK_WA_Retr    CHECK (MovementType = 'HANDOVER' OR RetrievalReason IN ('OPERATION_CHANGED','WORKER_ABSENT','STYLE_FINISHED')),
    CONSTRAINT CK_WA_Mode    CHECK (ConfirmMode IN ('WORKER','ON_BEHALF')),
    CONSTRAINT CK_WA_Behalf  CHECK (ConfirmMode = 'WORKER' OR OnBehalfReason IS NOT NULL),
    CONSTRAINT CK_WA_Status  CHECK (Status IN ('PENDING','CONFIRMED','REJECTED')),
    CONSTRAINT CK_WA_Piece   CHECK (PieceQty >= 0 AND PieceQty <= Quantity),
    CONSTRAINT CK_WA_Confirmed CHECK ((ConfirmedQty IS NULL AND ConfirmedPieceQty IS NULL)
                                   OR (ConfirmedQty >= 0 AND ConfirmedPieceQty >= 0 AND ConfirmedPieceQty <= ConfirmedQty
                                       AND ((ConfirmedQty = Quantity AND ConfirmedPieceQty = PieceQty) OR DiscrepancyReason IS NOT NULL))),
    CONSTRAINT CK_WA_ConfirmedStatus CHECK (Status <> 'CONFIRMED' OR ConfirmedQty IS NOT NULL),
    CONSTRAINT CK_WA_Reject  CHECK (Status <> 'REJECTED' OR RejectReason IS NOT NULL)
);
CREATE INDEX IX_WA_Line ON dbo.WorkAllocation(SewingTeamId, OrderLineId, OperationId, Status) INCLUDE (MovementType, Quantity, ConfirmedQty, PieceQty, ConfirmedPieceQty);
CREATE INDEX IX_WA_Worker ON dbo.WorkAllocation(WorkerId, OrderLineId, OperationId, Status);

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
    CONSTRAINT CK_DR_Type   CHECK (DefectType IN ('REWORK','SCRAP')),
    CONSTRAINT CK_DR_Reason CHECK (DefectReasonCode IN ('SKIPPED_STITCH','CROOKED_SEAM','WRONG_SIZE','TORN','STAINED','OTHER')),
    CONSTRAINT CK_DR_Other  CHECK (DefectReasonCode <> 'OTHER' OR ReasonNote IS NOT NULL),
    CONSTRAINT CK_DR_Status CHECK (Status IN ('RECORDED','CANCELLED','LOCKED')),
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
    CONSTRAINT CK_DN_Status CHECK (Status IN ('PENDING','REJECTED','CANCELLED','IN_DELIVERY','DELIVERED','PARTIALLY_DELIVERED')),
    CONSTRAINT CK_DN_Reject CHECK (Status <> 'REJECTED' OR RejectReason IS NOT NULL),
    CONSTRAINT CK_DN_Delivered CHECK (Status NOT IN ('DELIVERED','PARTIALLY_DELIVERED') OR CustomerReceivedDate IS NOT NULL),
    CONSTRAINT CK_DN_Dispatched CHECK (Status NOT IN ('IN_DELIVERY','DELIVERED','PARTIALLY_DELIVERED') OR (DispatchedBy IS NOT NULL AND DispatchedAt IS NOT NULL))
);
CREATE INDEX IX_DN_Order ON dbo.DispatchNote(OrderId, Status);
CREATE TABLE dbo.DispatchNoteLine (
    DispatchNoteLineId INT IDENTITY(1,1) CONSTRAINT PK_DispatchNoteLine PRIMARY KEY,
    DispatchNoteId     INT NOT NULL CONSTRAINT FK_DNL_DN REFERENCES dbo.DispatchNote(DispatchNoteId),
    OrderLineId        INT NOT NULL CONSTRAINT FK_DNL_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    IssueQty           INT NOT NULL CONSTRAINT CK_DNL_Qty CHECK (IssueQty > 0),
    ReceivedQty        INT NULL,       -- khách nhận thực tế (không vượt số xuất)
    CONSTRAINT CK_DNL_Received CHECK (ReceivedQty IS NULL OR ReceivedQty BETWEEN 0 AND IssueQty),
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
    MaterialColor      NVARCHAR(50)  NULL,
    OrderLineId        INT           NULL CONSTRAINT FK_SB_OrderLine REFERENCES dbo.OrderLine(OrderLineId),    -- kho BTP / TP: màu × size
    ProductComponentMaterialId INT   NULL CONSTRAINT FK_SB_PCM REFERENCES dbo.ProductComponentMaterial(ProductComponentMaterialId), -- BTP: NULL = bộ đủ; có = chi tiết × vải cắt bù (25/10)
    OnHandQty          DECIMAL(18,3) NOT NULL CONSTRAINT DF_SB_OnHand DEFAULT 0,
    UpdatedAt          DATETIME2(0)  NOT NULL CONSTRAINT DF_SB_UpdatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_SB_WH        CHECK (WarehouseType IN ('MATERIAL','SEMI_FINISHED','FINISHED')),
    CONSTRAINT CK_SB_Item      CHECK ((WarehouseType = 'MATERIAL' AND MaterialId IS NOT NULL AND MaterialColor IS NOT NULL AND OrderLineId IS NULL)
                                   OR (WarehouseType <> 'MATERIAL' AND MaterialId IS NULL AND MaterialColor IS NULL AND OrderLineId IS NOT NULL)),
    CONSTRAINT CK_SB_Component CHECK (ProductComponentMaterialId IS NULL OR WarehouseType = 'SEMI_FINISHED'),
    CONSTRAINT CK_SB_OnHand    CHECK (OnHandQty >= 0),   -- GBR-07
    CONSTRAINT UQ_SB_Item UNIQUE (WarehouseType, MaterialId, MaterialColor, OrderLineId, ProductComponentMaterialId)
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
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductVariantId <> ol.ProductVariantId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết tồn kho phải là chi tiết vải trong định mức của đúng màu × size của dòng đơn.', 1;
END;
GO

/* Ghi kho (25/10): mọi chứng từ xác nhận đều gọi thủ tục này, mỗi dòng chứng từ một lần, trong transaction của backend.
   Truyền mặt hàng (@WarehouseType + NVL/màu hoặc dòng đơn/chi tiết), hoặc @StockBalanceId (kiểm kê).
   Thủ tục: tìm / tạo dòng StockBalance và khóa nó → chặn tồn âm (50048) → cập nhật OnHandQty →
   thêm StockTransaction với BalanceAfter = tồn mới. Trả về StockBalanceId, BalanceAfter. */
CREATE PROCEDURE dbo.usp_Stock_Post
    @WarehouseType      VARCHAR(15)   = NULL,
    @MaterialId         INT           = NULL,
    @MaterialColor      NVARCHAR(50)  = NULL,
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
              AND EXISTS (SELECT MaterialId, MaterialColor, OrderLineId, ProductComponentMaterialId
                          INTERSECT SELECT @MaterialId, @MaterialColor, @OrderLineId, @ProductComponentMaterialId);
            IF @StockBalanceId IS NULL
            BEGIN
                INSERT dbo.StockBalance (WarehouseType, MaterialId, MaterialColor, OrderLineId, ProductComponentMaterialId)
                VALUES (@WarehouseType, @MaterialId, @MaterialColor, @OrderLineId, @ProductComponentMaterialId);
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
   50041: chi tiết / dòng định mức không thuộc mã hàng (hoặc màu × size) của dòng đơn, hoặc không phải chi tiết vải
   50042: dòng phiếu trỏ sang đơn hàng / mã hàng / phiếu khác với đầu phiếu
   50043: NVL của dòng phiếu nhập khác NVL của dòng đơn mua
   50044: công đoạn / đơn giá không khớp mã hàng của dòng đơn
   50045: loại dòng không khớp loại kho của phiếu
   50046: dòng đơn từ cắt bù chi tiết mà không biết dòng đơn hoặc lệnh cắt
   50049: dòng bàn cắt bù chi tiết dùng NVL / màu khác vải của bàn cắt (25/10)
   50050: tổng số lần giao của công đoạn × màu × size vượt số bộ BTP tổ đã nhận, hoặc thu hồi quá số đang giao (25/10)
   50051: giao / thu hồi BTP kèm theo ở công đoạn không dùng BTP (25/10)
   50052: sản lượng vượt số lần được giao (trần sản lượng, 25/10)
   50053: BTP giao kèm cho công nhân vượt BTP tổ đang giữ (25/10)
   Chỉ kiểm tra khi thêm / sửa dòng; backend không đổi đơn hàng của đầu phiếu đã có dòng. */

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
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductVariantId <> ol.ProductVariantId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết cắt phải là chi tiết vải (PANEL) trong định mức của đúng màu × size của dòng đơn.', 1;
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
               LEFT JOIN dbo.WorkOrder wo            ON wo.WorkOrderId = r.WorkOrderId
               WHERE pc.ComponentType <> 'PANEL'
                  OR (ol.OrderLineId IS NOT NULL AND pcm.ProductVariantId <> ol.ProductVariantId)
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
               WHERE COALESCE(om1.MaterialId, i.MaterialId) <> COALESCE(om2.MaterialId, pol.MaterialId)
                  OR COALESCE(om1.MaterialColor, i.MaterialColor) <> COALESCE(om2.MaterialColor, pol.MaterialColor))
        THROW 50043, N'NVL / màu của dòng phiếu nhập khác dòng đơn mua.', 1;
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
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductVariantId <> ol.ProductVariantId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết cấp BTP phải là chi tiết vải trong định mức của đúng màu × size của dòng đơn.', 1;
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
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductVariantId <> ol.ProductVariantId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết cắt phải là chi tiết vải (PANEL) trong định mức của đúng màu × size của dòng đơn.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.CuttingLay cl                ON cl.CuttingLayId = i.CuttingLayId
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               WHERE pcm.MaterialId <> cl.FabricMaterialId OR pcm.MaterialColor NOT IN (cl.FabricColor, N''))
        THROW 50049, N'Chi tiết cắt bù dùng vải / màu vải khác vải của bàn cắt.', 1;
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
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductVariantId <> ol.ProductVariantId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết bàn giao phải là chi tiết vải trong định mức của đúng màu × size của dòng đơn.', 1;
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
   - công đoạn thuộc mã hàng của dòng đơn (50044); công đoạn không dùng BTP thì không kèm BTP (50051)
   - Σ số lần đang giao của tổ × dòng đơn × công đoạn (giao chưa bị từ chối − thu hồi đã xác nhận) ≤ số bộ BTP tổ đã nhận (50050);
     tổ trưởng chia cho từng công nhân tùy ý trong giới hạn này
   - mỗi công nhân không bị thu hồi quá số đang giữ (giao đã xác nhận − thu hồi chưa bị từ chối ≥ 0) (50050)
   - BTP giao kèm (chưa bị từ chối) − BTP trả lại không vượt BTP tổ đã nhận từ kho (50053)
   - sau thu hồi, trần không nhỏ hơn sản lượng đã báo (50052) */
CREATE TRIGGER dbo.trg_WorkAllocation_Check ON dbo.WorkAllocation AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.OrderLine ol ON ol.OrderLineId = i.OrderLineId
               JOIN dbo.Operation op ON op.OperationId = i.OperationId
               WHERE op.ProductId <> ol.ProductId)
        THROW 50044, N'Công đoạn không thuộc mã hàng của dòng đơn.', 1;
    IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.Operation op ON op.OperationId = i.OperationId
               WHERE op.UsesSemiFinished = 0 AND (i.PieceQty > 0 OR i.ConfirmedPieceQty > 0))
        THROW 50051, N'Công đoạn không dùng BTP thì không giao / thu hồi BTP kèm theo.', 1;
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
    IF EXISTS (SELECT 1 FROM (SELECT DISTINCT SewingTeamId, OrderLineId FROM inserted) k
               OUTER APPLY (SELECT SUM(CAST(l.ConfirmedQty AS INT)) Qty
                            FROM dbo.MaterialIssue mi JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = mi.MaterialIssueId
                            WHERE mi.WarehouseType = 'SEMI_FINISHED' AND mi.Status = 'RECEIVED' AND mi.ReceivingTeamId = k.SewingTeamId
                              AND l.OrderLineId = k.OrderLineId AND l.ProductComponentMaterialId IS NULL) r
               OUTER APPLY (SELECT SUM(CASE WHEN w.MovementType = 'HANDOVER' THEN ISNULL(w.ConfirmedPieceQty, w.PieceQty)
                                            WHEN w.Status = 'CONFIRMED' THEN -w.ConfirmedPieceQty ELSE 0 END) Qty
                            FROM dbo.WorkAllocation w
                            WHERE w.SewingTeamId = k.SewingTeamId AND w.OrderLineId = k.OrderLineId AND w.Status <> 'REJECTED') g
               WHERE ISNULL(g.Qty, 0) > ISNULL(r.Qty, 0))
        THROW 50053, N'BTP tổ đang giữ không đủ để giao kèm cho công nhân.', 1;
    IF EXISTS (SELECT 1 FROM (SELECT DISTINCT WorkerId, OrderLineId, OperationId FROM inserted) k
               OUTER APPLY (SELECT SUM(CASE WHEN w.MovementType = 'HANDOVER' THEN w.ConfirmedQty ELSE -w.ConfirmedQty END) Qty
                            FROM dbo.WorkAllocation w
                            WHERE w.Status = 'CONFIRMED' AND w.WorkerId = k.WorkerId
                              AND w.OrderLineId = k.OrderLineId AND w.OperationId = k.OperationId) c
               OUTER APPLY (SELECT SUM(o.Quantity) Qty FROM dbo.OutputRecord o
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
               OUTER APPLY (SELECT SUM(o.Quantity) Qty FROM dbo.OutputRecord o
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
SELECT  x.WarehouseType, x.MaterialId, x.MaterialColor, x.OrderLineId, x.ProductComponentMaterialId,
        SUM(x.OnHandQty)                    AS OnHandQty,
        SUM(x.ReservedQty)                  AS ReservedQty,
        SUM(x.OnHandQty) - SUM(x.ReservedQty) AS AvailableQty
FROM (
    SELECT WarehouseType, MaterialId, MaterialColor, OrderLineId, ProductComponentMaterialId, OnHandQty, CAST(0 AS DECIMAL(18,3)) AS ReservedQty
    FROM dbo.StockBalance   -- 25/10: đọc tồn đã lưu, không cộng lại thẻ kho
    UNION ALL      -- phiếu cấp NVL / BTP đang chờ tổ nhận
    SELECT i.WarehouseType, l.MaterialId, l.MaterialColor, l.OrderLineId, l.ProductComponentMaterialId, 0, l.IssuedQty
    FROM dbo.MaterialIssue i JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = i.MaterialIssueId
    WHERE i.Status = 'PENDING_RECEIPT'
    UNION ALL      -- phiếu xuất thành phẩm đang chờ Thủ kho xuất
    SELECT 'FINISHED', NULL, NULL, l.OrderLineId, NULL, 0, l.IssueQty
    FROM dbo.DispatchNote d JOIN dbo.DispatchNoteLine l ON l.DispatchNoteId = d.DispatchNoteId
    WHERE d.Status = 'PENDING'
) x
GROUP BY x.WarehouseType, x.MaterialId, x.MaterialColor, x.OrderLineId, x.ProductComponentMaterialId;
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
SELECT  om.OrderMaterialId, om.OrderId, om.MaterialId, om.MaterialColor,
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
        wol.OrderLineId, wol.ProductComponentMaterialId, pcm.ProductComponentId, pcm.MaterialId, pcm.MaterialColor, wol.PlannedQty,
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
SELECT  m.MaterialId, m.MaterialCode, m.MaterialName, m.MaterialType, m.Category, m.Unit,
        sb.MaterialColor, sb.OnHandQty, sb.ReservedQty, sb.AvailableQty
FROM dbo.vw_StockBalance sb
JOIN dbo.Material m ON m.MaterialId = sb.MaterialId
WHERE sb.WarehouseType = 'MATERIAL';
GO

-- NVL của đơn theo định mức (23/10): Σ số lượng dòng đơn × định mức biến thể × (1 + hao hụt), theo NVL + màu,
-- đặt cạnh RequiredQty khách báo (OrderMaterial). FULL JOIN để thấy cả hai lệch:
-- OrderMaterialId NULL = định mức cần NVL mà đơn chưa khai; NormQty = 0 = đơn khai NVL mà định mức không dùng
CREATE VIEW dbo.vw_OrderMaterialNorm
AS
WITH n AS (
    SELECT ol.OrderId, pcm.MaterialId, pcm.MaterialColor,
           CAST(SUM(ol.OrderedQty * pcm.QtyPerGarment * (1 + pcm.WastagePercent / 100)) AS DECIMAL(18,3)) AS NormQty
    FROM dbo.OrderLine ol
    JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductVariantId = ol.ProductVariantId
    GROUP BY ol.OrderId, pcm.MaterialId, pcm.MaterialColor
)
SELECT  COALESCE(om.OrderId, n.OrderId)             AS OrderId,
        COALESCE(om.MaterialId, n.MaterialId)       AS MaterialId,
        COALESCE(om.MaterialColor, n.MaterialColor) AS MaterialColor,
        om.OrderMaterialId,
        om.RequiredQty,
        ISNULL(n.NormQty, 0)                        AS NormQty,
        ISNULL(om.RequiredQty, 0) - ISNULL(n.NormQty, 0) AS DiffQty   -- âm = khách báo thiếu so với định mức
FROM n
FULL JOIN dbo.OrderMaterial om
       ON om.OrderId = n.OrderId AND om.MaterialId = n.MaterialId AND om.MaterialColor = n.MaterialColor;
GO

-- Vải cần cho lệnh cắt (UC19 lưu lệnh cắt, UC40 điền sẵn số cấp, UC51 so với vải thực dùng):
-- chỉ thành phần PANEL và NVL loại vải. Dòng cả bộ: PlannedQty × định mức; dòng cắt bù 1 dòng định mức: PlannedQty × định mức / số chi tiết
CREATE VIEW dbo.vw_WorkOrderFabricNeed
AS
SELECT  wol.WorkOrderId, pcm.MaterialId, pcm.MaterialColor,
        CAST(SUM(wol.PlannedQty * pcm.QtyPerGarment * (1 + pcm.WastagePercent / 100)
                 / CASE WHEN wol.ProductComponentMaterialId IS NULL THEN 1 ELSE pc.PiecesPerGarment END) AS DECIMAL(18,3)) AS NeedQty
FROM dbo.WorkOrderLine wol
JOIN dbo.OrderLine ol      ON ol.OrderLineId = wol.OrderLineId
JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductVariantId = ol.ProductVariantId
                                     AND (wol.ProductComponentMaterialId IS NULL OR pcm.ProductComponentMaterialId = wol.ProductComponentMaterialId)
JOIN dbo.ProductComponent pc ON pc.ProductComponentId = pcm.ProductComponentId AND pc.ComponentType = 'PANEL'
JOIN dbo.Material m          ON m.MaterialId = pcm.MaterialId AND m.MaterialType = 'FABRIC'
GROUP BY wol.WorkOrderId, pcm.MaterialId, pcm.MaterialColor;
GO

-- BTP tổ may đang giữ theo dòng đơn = đã nhận từ kho − BTP giao kèm cho CN + BTP CN trả lại (UC24); chỉ đếm bộ đủ.
-- 25/10: chỉ trừ WorkAllocation.ConfirmedPieceQty, không trừ số lần, nên hàng chạy qua nhiều công đoạn không bị trừ lặp
CREATE VIEW dbo.vw_TeamPieceHolding
AS
WITH Received AS (
    SELECT i.ReceivingTeamId AS SewingTeamId, l.OrderLineId, SUM(CAST(l.ConfirmedQty AS INT)) AS Qty
    FROM dbo.MaterialIssue i JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = i.MaterialIssueId
    WHERE i.WarehouseType = 'SEMI_FINISHED' AND i.Status = 'RECEIVED' AND l.ProductComponentMaterialId IS NULL
    GROUP BY i.ReceivingTeamId, l.OrderLineId
), Moves AS (
    SELECT SewingTeamId, OrderLineId,
           SUM(CASE WHEN MovementType = 'HANDOVER'  THEN ConfirmedPieceQty ELSE 0 END) AS HandedToWorkers,
           SUM(CASE WHEN MovementType = 'RETRIEVAL' THEN ConfirmedPieceQty ELSE 0 END) AS RetrievedFromWorkers
    FROM dbo.WorkAllocation WHERE Status = 'CONFIRMED' GROUP BY SewingTeamId, OrderLineId
)
SELECT r.SewingTeamId, r.OrderLineId,
       r.Qty                                   AS ReceivedFromWarehouse,
       ISNULL(m.HandedToWorkers, 0)            AS HandedToWorkers,
       ISNULL(m.RetrievedFromWorkers, 0)       AS RetrievedFromWorkers,
       r.Qty - ISNULL(m.HandedToWorkers, 0) + ISNULL(m.RetrievedFromWorkers, 0) AS HeldByTeam
FROM Received r
LEFT JOIN Moves m ON m.SewingTeamId = r.SewingTeamId AND m.OrderLineId = r.OrderLineId;
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
), Scrap AS (
    SELECT ResponsibleWorkerId AS WorkerId, OrderLineId, OperationId, SUM(Quantity) AS ScrapQty
    FROM dbo.DefectRecord WHERE DefectType = 'SCRAP' AND Status <> 'CANCELLED'
    GROUP BY ResponsibleWorkerId, OrderLineId, OperationId
)
SELECT mv.WorkerId, mv.OrderLineId, mv.OperationId,
       mv.CapQty,
       ISNULL(o.OutputQty, 0) AS OutputQty,
       ISNULL(s.ScrapQty, 0)  AS ScrapQty,
       mv.CapQty - ISNULL(o.OutputQty, 0) AS RemainingQty
FROM Moves mv
LEFT JOIN Outp o  ON o.WorkerId = mv.WorkerId AND o.OrderLineId = mv.OrderLineId AND o.OperationId = mv.OperationId
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
SELECT  r.SewingTeamId, r.OrderLineId, op.OperationId, op.UsesSemiFinished,
        r.Qty AS RequiredQty,
        ISNULL(a.AllocatedQty, 0) AS AllocatedQty,
        ISNULL(a.PendingQty, 0)   AS PendingQty,
        r.Qty - ISNULL(a.AllocatedQty, 0) - ISNULL(a.PendingQty, 0) AS UnallocatedQty,
        ISNULL(o.OutputQty, 0)    AS OutputQty
FROM Received r
JOIN dbo.OrderLine ol ON ol.OrderLineId = r.OrderLineId
JOIN dbo.Operation op ON op.ProductId = ol.ProductId AND op.TeamType = 'SEWING' AND op.Status = 'ACTIVE'
OUTER APPLY (SELECT SUM(CASE WHEN w.Status = 'CONFIRMED' THEN IIF(w.MovementType = 'HANDOVER', w.ConfirmedQty, -w.ConfirmedQty) ELSE 0 END) AS AllocatedQty,
                    SUM(CASE WHEN w.Status = 'PENDING' AND w.MovementType = 'HANDOVER' THEN w.Quantity ELSE 0 END) AS PendingQty
             FROM dbo.WorkAllocation w
             WHERE w.SewingTeamId = r.SewingTeamId AND w.OrderLineId = r.OrderLineId AND w.OperationId = op.OperationId) a
OUTER APPLY (SELECT SUM(x.Quantity) AS OutputQty FROM dbo.OutputRecord x
             WHERE x.SewingTeamId = r.SewingTeamId AND x.OrderLineId = r.OrderLineId AND x.OperationId = op.OperationId) o;
GO

-- Lương tạm tính theo ngày của công nhân may (UC59): Σ(SL × đơn giá khóa) − Σ(Scrap × đơn giá đó)
CREATE VIEW dbo.vw_WorkerDailyEstimatedPay
AS
WITH Outp AS (
    SELECT r.WorkerId, r.WorkDate, SUM(r.Quantity) AS OutputQty, SUM(r.Quantity * pr.Rate) AS OutputAmount
    FROM dbo.OutputRecord r JOIN dbo.PieceRate pr ON pr.PieceRateId = r.PieceRateId
    GROUP BY r.WorkerId, r.WorkDate
), Scrap AS (
    SELECT d.ResponsibleWorkerId AS WorkerId, d.WorkDate, SUM(d.Quantity) AS ScrapQty, SUM(d.Quantity * pr.Rate) AS ScrapAmount
    FROM dbo.DefectRecord d JOIN dbo.PieceRate pr ON pr.PieceRateId = d.PieceRateId
    WHERE d.DefectType = 'SCRAP' AND d.Status <> 'CANCELLED'
    GROUP BY d.ResponsibleWorkerId, d.WorkDate
), Days AS (
    SELECT WorkerId, WorkDate FROM Outp UNION SELECT WorkerId, WorkDate FROM Scrap
), Mult AS (
    SELECT CAST(SettingValue AS DECIMAL(4,2)) AS SundayMultiplier FROM dbo.SystemSetting WHERE SettingKey = 'SUNDAY_PAY_MULTIPLIER'
)
SELECT d.WorkerId, e.EmployeeCode, e.FullName, d.WorkDate,
       CAST(IIF(DATEDIFF(DAY, 0, d.WorkDate) % 7 = 6, 1, 0) AS BIT) AS IsSunday,
       ISNULL(o.OutputQty, 0)    AS OutputQty,
       ISNULL(s.ScrapQty, 0)     AS ScrapQty,
       ISNULL(o.OutputAmount, 0) AS OutputAmount,
       CAST(ISNULL(o.OutputAmount, 0) * (IIF(DATEDIFF(DAY, 0, d.WorkDate) % 7 = 6, ISNULL(m.SundayMultiplier, 1), 1) - 1) AS DECIMAL(18,2)) AS SundayBonus,
       ISNULL(s.ScrapAmount, 0)  AS ScrapAmount,
       CAST(ISNULL(o.OutputAmount, 0) * IIF(DATEDIFF(DAY, 0, d.WorkDate) % 7 = 6, ISNULL(m.SundayMultiplier, 1), 1)
         - ISNULL(s.ScrapAmount, 0) AS DECIMAL(18,2)) AS EstimatedPay      -- Chủ nhật: tiền sản phẩm x hệ số, khoản trừ Scrap giữ nguyên
FROM Days d
OUTER APPLY (SELECT SundayMultiplier FROM Mult) m
JOIN dbo.Employee e ON e.EmployeeId = d.WorkerId AND e.PayType = 'PIECE_RATE'
LEFT JOIN Outp o  ON o.WorkerId = d.WorkerId AND o.WorkDate = d.WorkDate
LEFT JOIN Scrap s ON s.WorkerId = d.WorkerId AND s.WorkDate = d.WorkDate;
GO

-- Tiến độ theo dòng đơn (UC21): đặt, cắt, vào kho BTP, cấp tổ may, hoàn thiện (QC), nhập kho TP, đã giao, lỗi
-- (bàn giao và cấp BTP chỉ đếm bộ đủ; chi tiết cắt bù lẻ không tính là sản phẩm)
CREATE VIEW dbo.vw_OrderLineProgress
AS
SELECT  ol.OrderLineId, ol.OrderId, o.OrderCode, ol.ProductId, p.ProductCode, ol.ProductVariantId, pv.Color, pv.SizeCode, ol.OrderedQty,
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
OUTER APPLY (SELECT SUM(ActualQty) Qty FROM dbo.CuttingLayLine WHERE OrderLineId = ol.OrderLineId AND ProductComponentMaterialId IS NULL) cut
OUTER APPLY (SELECT SUM(l.ConfirmedQty) Qty FROM dbo.CutHandoverLine l JOIN dbo.CutHandover h ON h.CutHandoverId = l.CutHandoverId
             WHERE l.OrderLineId = ol.OrderLineId AND l.ProductComponentMaterialId IS NULL AND h.Status = 'CONFIRMED') ch
OUTER APPLY (SELECT SUM(CAST(l.ConfirmedQty AS INT)) Qty FROM dbo.MaterialIssueLine l JOIN dbo.MaterialIssue i ON i.MaterialIssueId = l.MaterialIssueId
             WHERE l.OrderLineId = ol.OrderLineId AND l.ProductComponentMaterialId IS NULL AND i.WarehouseType = 'SEMI_FINISHED' AND i.Status = 'RECEIVED') sfi
OUTER APPLY (SELECT SUM(FinishedQty) Qty FROM dbo.QcBatch WHERE OrderLineId = ol.OrderLineId) qc
OUTER APPLY (SELECT SUM(l.ConfirmedQty) Qty FROM dbo.FgHandoverLine l JOIN dbo.FgHandover h ON h.FgHandoverId = l.FgHandoverId
             WHERE l.OrderLineId = ol.OrderLineId AND h.Status = 'CONFIRMED') fg
OUTER APPLY (SELECT SUM(l.ReceivedQty) Qty FROM dbo.DispatchNoteLine l JOIN dbo.DispatchNote d ON d.DispatchNoteId = l.DispatchNoteId
             WHERE l.OrderLineId = ol.OrderLineId AND d.Status IN ('DELIVERED','PARTIALLY_DELIVERED')) dl
OUTER APPLY (SELECT SUM(CASE WHEN DefectType = 'REWORK' THEN Quantity ELSE 0 END) ReworkQty,
                    SUM(CASE WHEN DefectType = 'SCRAP'  THEN Quantity ELSE 0 END) ScrapQty
             FROM dbo.DefectRecord WHERE OrderLineId = ol.OrderLineId AND Status <> 'CANCELLED') df;
GO

-- Kết quả KPI theo ngày (FR 3.14): chỉ tiêu ngày, hoàn thiện (QC của tổ), tỉ lệ lỗi, % đạt
CREATE VIEW dbo.vw_KpiDailyResult
AS
SELECT  k.KpiTargetId, k.SewingTeamId, k.OrderId, k.ProductId, q.WorkDate,
        CAST(1.0 * k.TargetFinishedQty / k.WorkingDays AS DECIMAL(18,2)) AS DailyTarget,
        q.FinishedQty,
        ISNULL(d.DefectQty, 0) AS DefectQty,
        CASE WHEN q.FinishedQty + ISNULL(d.DefectQty, 0) = 0 THEN 0
             ELSE CAST(100.0 * ISNULL(d.DefectQty, 0) / (q.FinishedQty + ISNULL(d.DefectQty, 0)) AS DECIMAL(5,2)) END AS DefectRatePct,
        CAST(100.0 * q.FinishedQty / (1.0 * k.TargetFinishedQty / k.WorkingDays) AS DECIMAL(9,2)) AS PercentAchieved
FROM dbo.KpiTarget k
CROSS APPLY (SELECT b.WorkDate, SUM(b.FinishedQty) AS FinishedQty
             FROM dbo.QcBatch b JOIN dbo.OrderLine ol ON ol.OrderLineId = b.OrderLineId
             WHERE b.SewingTeamId = k.SewingTeamId AND ol.OrderId = k.OrderId AND ol.ProductId = k.ProductId
               AND b.WorkDate BETWEEN k.StartDate AND k.EndDate
             GROUP BY b.WorkDate) q
OUTER APPLY (SELECT SUM(dr.Quantity) AS DefectQty
             FROM dbo.DefectRecord dr JOIN dbo.OrderLine ol ON ol.OrderLineId = dr.OrderLineId
             WHERE dr.SewingTeamId = k.SewingTeamId AND ol.OrderId = k.OrderId AND ol.ProductId = k.ProductId
               AND dr.WorkDate = q.WorkDate AND dr.Status <> 'CANCELLED') d;
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

-- Tỉ lệ size của bàn cắt gần nhất theo mã hàng (FR 2.7), để điền sẵn khi tạo bàn cắt mới, vd 'S:1 M:2 L:2 XL:1'
CREATE VIEW dbo.vw_ProductLastSizeRatio
AS
WITH LastLay AS (
    SELECT wo.ProductId, cl.CuttingLayId,
           ROW_NUMBER() OVER (PARTITION BY wo.ProductId ORDER BY cl.WorkDate DESC, cl.CreatedAt DESC, cl.CuttingLayId DESC) AS rn
    FROM dbo.CuttingLay cl
    JOIN dbo.WorkOrder wo ON wo.WorkOrderId = cl.WorkOrderId
    WHERE NOT EXISTS (SELECT 1 FROM dbo.CuttingLayLine x WHERE x.CuttingLayId = cl.CuttingLayId AND x.ProductComponentMaterialId IS NOT NULL) -- bỏ bàn cắt bù chi tiết
)
SELECT ll.ProductId, ll.CuttingLayId,
       STRING_AGG(CONCAT(pv.SizeCode, ':', cll.RatioValue), ' ') WITHIN GROUP (ORDER BY ol.OrderLineId) AS SizeRatio
FROM LastLay ll
JOIN dbo.CuttingLayLine cll ON cll.CuttingLayId = ll.CuttingLayId
JOIN dbo.OrderLine ol       ON ol.OrderLineId = cll.OrderLineId
JOIN dbo.ProductVariant pv  ON pv.ProductVariantId = ol.ProductVariantId
WHERE ll.rn = 1
GROUP BY ll.ProductId, ll.CuttingLayId;
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

INSERT dbo.GradeLevel (GradeCode, GradeName, MonthlyAmount, EffectiveDate) VALUES
 ('A', N'Bậc A – Giỏi',      600000, '2026-01-01'),
 ('B', N'Bậc B – Khá',       400000, '2026-01-01'),
 ('C', N'Bậc C – Trung bình', 200000, '2026-01-01'),
 ('D', N'Bậc D – Yếu',            0, '2026-01-01');

DECLARE @rAdmin INT = (SELECT RoleId FROM dbo.Role WHERE RoleCode = 'ADMIN');

INSERT dbo.SystemSetting (SettingKey, SettingValue, Description) VALUES
 ('MEAL_REMINDER_TIME',        N'08:15', N'JOB-02 nhắc tổ trưởng xác nhận suất ăn'),
 ('MEAL_CUTOFF_TIME',          N'08:30', N'JOB-03 tự chốt suất ăn'),
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
 ('TTMAY',    N'Tổ trưởng tổ may 1',  'TEAM_LEADER',      @may1, 'MONTHLY'),
 ('TPMAY',    N'Tổ phó tổ may 1',     'TEAM_DEPUTY',      @may1, 'MONTHLY'),
 ('QAQC1',    N'QA/QC mẫu',           'QAQC',             @vp,   'DAILY'),
 ('CODIEN',   N'Cơ điện mẫu',         'ELECTRICIAN',      @vp,   'MONTHLY'),
 ('CNMAY1',   N'Công nhân may 1',     'WORKER',           @may1, 'PIECE_RATE'),
 ('CNCAT1',   N'Công nhân cắt 1',     'WORKER',           @cat,  'DAILY');

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
 ('Product', N'Mã hàng'), ('OperationGroup', N'Công đoạn to (nhóm công đoạn)'), ('Operation', N'Công đoạn'), ('OperationComponent', N'Công đoạn làm trên chi tiết nào'), ('PieceRate', N'Đơn giá công đoạn'),
 ('OrderMaterial', N'NVL của đơn: cần bao nhiêu, khách cấp bao nhiêu, phải mua bao nhiêu'), ('ProductVariant', N'Biến thể mã hàng: màu × size'), ('ProductComponent', N'Thành phần của mã hàng: chi tiết vải (PANEL) và phụ liệu (TRIM)'),
 ('ProductComponentMaterial', N'Định mức NVL theo biến thể × thành phần: NVL, màu NVL, lượng / sản phẩm, hao hụt, kích thước chi tiết'), ('KpiTarget', N'KPI tổ may'),
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

INSERT dbo.Material (MaterialCode, MaterialName, Category, Unit) VALUES
 ('DEMO-VAI01',  N'Vải gió 2 lớp DEMO', 'MAIN_FABRIC', N'm'),
 ('DEMO-CHI01',  N'Chỉ may 40/2 DEMO',  'THREAD',      N'cuộn'),
 ('DEMO-KHOA01', N'Khóa kéo 60cm DEMO', 'ZIPPER',      N'cái'),
 ('DEMO-NHAN01', N'Nhãn dệt DEMO',      'LABEL',       N'cái'),
 ('DEMO-KIM01',  N'Kim máy 14 DEMO',    'OTHER',       N'hộp');
DECLARE @mVai INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-VAI01'),
        @mChi INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-CHI01'),
        @mKhoa INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-KHOA01'),
        @mNhan INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-NHAN01'),
        @mKim INT = (SELECT MaterialId FROM dbo.Material WHERE MaterialCode = 'DEMO-KIM01');

-- Biến thể, thành phần và định mức (23/10). Phải có trước OrderLine (FK_OrderLine_Variant).
-- Xanh – S chưa có trong đơn nào: biến thể khai báo trước, đơn sau mới dùng.
INSERT dbo.ProductVariant (ProductId, Color, SizeCode) VALUES
 (@p1, N'Đen', 'S'), (@p1, N'Đen', 'M'), (@p1, N'Đen', 'L'),
 (@p1, N'Xanh', 'S'), (@p1, N'Xanh', 'M'), (@p1, N'Xanh', 'L'),
 (@p2, N'Be', 'M'), (@p2, N'Be', 'L');

INSERT dbo.ProductComponent (ProductId, ComponentName, ComponentType, PiecesPerGarment, SortOrder) VALUES
 (@p1, N'Thân trước', 'PANEL', 2, 1), (@p1, N'Thân sau', 'PANEL', 1, 2), (@p1, N'Tay áo', 'PANEL', 2, 3),
 (@p1, N'Khóa kéo',   'TRIM',  1, 4), (@p1, N'Nhãn',     'TRIM',  1, 5), (@p1, N'Chỉ may', 'TRIM',  1, 6),
 (@p2, N'Ống quần',   'PANEL', 2, 1), (@p2, N'Chỉ may',  'TRIM',  1, 2);

-- Vải theo chi tiết × size: lượng vải và chiều dài chi tiết tăng theo size.
-- Màu vải: theo màu áo, riêng biến thể Xanh phối tay áo vải Trắng (vd Xanh – S: tay dài 58 cm, vải Trắng;
-- Đen – M: tay dài 60 cm, vải Đen)
INSERT dbo.ProductComponentMaterial (ProductId, ProductVariantId, ProductComponentId, MaterialId, MaterialColor,
                                     QtyPerGarment, WastagePercent, LengthCm)
SELECT pv.ProductId, pv.ProductVariantId, pc.ProductComponentId, @mVai,
       CASE WHEN pv.Color = N'Xanh' AND pc.ComponentName = N'Tay áo' THEN N'Trắng' ELSE pv.Color END,
       s.Qty, 3, s.LengthCm
FROM (VALUES (N'Thân trước', 'S', 0.48, 66), (N'Thân trước', 'M', 0.52, 68), (N'Thân trước', 'L', 0.56, 70),
             (N'Thân sau',   'S', 0.42, 66), (N'Thân sau',   'M', 0.45, 68), (N'Thân sau',   'L', 0.49, 70),
             (N'Tay áo',     'S', 0.36, 58), (N'Tay áo',     'M', 0.39, 60), (N'Tay áo',     'L', 0.42, 62))
     s (ComponentName, SizeCode, Qty, LengthCm)
JOIN dbo.ProductComponent pc ON pc.ProductId = @p1 AND pc.ComponentName = s.ComponentName
JOIN dbo.ProductVariant pv   ON pv.ProductId = @p1 AND pv.SizeCode = s.SizeCode;

-- Phụ liệu áo: như nhau mọi size; chỉ theo màu áo, khóa và nhãn không phân màu ('')
INSERT dbo.ProductComponentMaterial (ProductId, ProductVariantId, ProductComponentId, MaterialId, MaterialColor,
                                     QtyPerGarment, WastagePercent)
SELECT pv.ProductId, pv.ProductVariantId, pc.ProductComponentId, t.MaterialId,
       CASE WHEN t.ByColor = 1 THEN pv.Color ELSE N'' END, t.Qty, t.Wastage
FROM (VALUES (N'Khóa kéo', @mKhoa, 1.0, 0, 0), (N'Nhãn', @mNhan, 1.0, 1, 0), (N'Chỉ may', @mChi, 0.1, 5, 1))
     t (ComponentName, MaterialId, Qty, Wastage, ByColor)
JOIN dbo.ProductComponent pc ON pc.ProductId = @p1 AND pc.ComponentName = t.ComponentName
JOIN dbo.ProductVariant pv   ON pv.ProductId = @p1;

-- Quần short
INSERT dbo.ProductComponentMaterial (ProductId, ProductVariantId, ProductComponentId, MaterialId, MaterialColor,
                                     QtyPerGarment, WastagePercent, LengthCm)
SELECT pv.ProductId, pv.ProductVariantId, pc.ProductComponentId, t.MaterialId, pv.Color,
       CASE WHEN pc.ComponentType = 'PANEL' AND pv.SizeCode = 'L' THEN t.Qty + 0.05 ELSE t.Qty END,
       t.Wastage,
       CASE WHEN pc.ComponentType = 'PANEL' THEN CASE pv.SizeCode WHEN 'M' THEN 45 ELSE 47 END END
FROM (VALUES (N'Ống quần', @mVai, 0.55, 3), (N'Chỉ may', @mChi, 0.05, 5)) t (ComponentName, MaterialId, Qty, Wastage)
JOIN dbo.ProductComponent pc ON pc.ProductId = @p2 AND pc.ComponentName = t.ComponentName
JOIN dbo.ProductVariant pv   ON pv.ProductId = @p2;

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
JOIN dbo.ProductVariant pv ON pv.ProductId = v.ProductId AND pv.Color = v.Color AND pv.SizeCode = v.SizeCode
ORDER BY v.Seq;
DECLARE @lS INT = (SELECT ol.OrderLineId FROM dbo.OrderLine ol JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
                   WHERE ol.OrderId = @o1 AND pv.Color = N'Đen' AND pv.SizeCode = 'S'),
        @lM INT = (SELECT ol.OrderLineId FROM dbo.OrderLine ol JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
                   WHERE ol.OrderId = @o1 AND pv.Color = N'Đen' AND pv.SizeCode = 'M'),
        @lL INT = (SELECT ol.OrderLineId FROM dbo.OrderLine ol JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
                   WHERE ol.OrderId = @o1 AND pv.Color = N'Đen' AND pv.SizeCode = 'L');

INSERT dbo.OrderMaterial (OrderId, MaterialId, MaterialColor, RequiredQty, CustomerQty, ExpectedDeliveryCount) VALUES
 (@o1, @mVai,  N'Đen', 2000, 0, NULL), (@o1, @mChi, N'Đen', 200, 0, NULL),
 (@o1, @mKhoa, N'',    1900, 400, 2), (@o1, @mNhan, N'', 1900, 1900, 1),
 (@o1, @mVai,  N'Xanh', 520, 0, NULL), (@o1, @mVai, N'Trắng', 210, 0, NULL), (@o1, @mChi, N'Xanh', 55, 0, NULL), -- áo Xanh: thân vải Xanh, tay vải Trắng
 (@o2, @mVai,  N'Be',  800, 0, NULL), (@o2, @mChi, N'Be', 100, 0, NULL);
DECLARE @om1Vai INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o1 AND MaterialId = @mVai AND MaterialColor = N'Đen'),
        @om1Chi INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o1 AND MaterialId = @mChi AND MaterialColor = N'Đen'),
        @om1Khoa INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o1 AND MaterialId = @mKhoa),
        @om2Vai INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o2 AND MaterialId = @mVai),
        @om2Chi INT = (SELECT OrderMaterialId FROM dbo.OrderMaterial WHERE OrderId = @o2 AND MaterialId = @mChi);

/* ---------- 3. MI-07: đơn mua ở đủ các trạng thái ---------- */
INSERT dbo.PurchaseOrder (PoCode, OrderId, SupplierId, OrderDate, GoodsStatus, PaymentStatus, RejectReason, PaymentReturnReason,
                          PaymentDate, PaidAmount, PaymentMethod, PaymentNote, Note) VALUES
 ('PO-DEMO-001', @o1, NULL,  NULL,         'PENDING_FINALISATION', 'UNPAID',   NULL, NULL, NULL, NULL, NULL, NULL, N'Chờ Quản đốc chốt nhà cung cấp và đơn giá'),
 ('PO-DEMO-002', @o1, NULL,  NULL,         'PENDING_FINALISATION', 'UNPAID',   NULL, NULL, NULL, NULL, NULL, NULL, N'Mua thêm khóa kéo'),
 ('PO-DEMO-003', @o2, @sup1, '2026-10-19', 'ORDERED',              'UNPAID',   NULL, NULL, NULL, NULL, NULL, NULL, N'Chờ Kế toán xác nhận thanh toán'),
 ('PO-DEMO-004', @o1, @sup2, '2026-10-16', 'ORDERED',              'RETURNED', NULL, N'Đơn giá khóa kéo cao hơn báo giá, đề nghị kiểm tra lại', NULL, NULL, NULL, NULL, NULL),
 ('PO-DEMO-005', @o1, @sup1, '2026-10-05', 'PARTIALLY_RECEIVED',  'PAID',     NULL, NULL, '2026-10-12', 44000000, 'BANK_TRANSFER', N'Chuyển khoản đợt 1', NULL),
 ('PO-DEMO-006', @o1, NULL,  NULL,         'REJECTED',             'UNPAID',   N'Khách đổi màu vải, hủy đơn mua cũ', NULL, NULL, NULL, NULL, NULL, NULL);
-- Đơn mua tồn kho dùng chung (TH-12): không gắn đơn hàng
INSERT dbo.PurchaseOrder (PoCode, OrderId, GoodsStatus, PaymentStatus, Note)
VALUES ('PO-DEMO-007', NULL, 'PENDING_FINALISATION', 'UNPAID', N'Kim máy dùng chung cho nhiều đơn');

DECLARE @po1 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'PO-DEMO-001'),
        @po2 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'PO-DEMO-002'),
        @po3 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'PO-DEMO-003'),
        @po4 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'PO-DEMO-004'),
        @po5 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'PO-DEMO-005'),
        @po6 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'PO-DEMO-006'),
        @po7 INT = (SELECT PurchaseOrderId FROM dbo.PurchaseOrder WHERE PoCode = 'PO-DEMO-007');
UPDATE dbo.PurchaseOrder SET ExpectedDeliveryCount = CASE PurchaseOrderId WHEN @po5 THEN 2 ELSE 1 END
WHERE PurchaseOrderId IN (@po3, @po4, @po5);   -- PO-DEMO-005 NCC giao 2 đợt

INSERT dbo.PurchaseOrderLine (PurchaseOrderId, OrderMaterialId, OrderedQty, UnitPrice) VALUES
 (@po1, @om1Vai, 1000, 0), (@po1, @om1Chi, 200, 0),
 (@po2, @om1Khoa, 1500, 0),
 (@po3, @om2Vai, 800, 45000), (@po3, @om2Chi, 100, 12000),
 (@po4, @om1Khoa, 1500, 3500),
 (@po5, @om1Vai, 1000, 44000),
 (@po6, @om1Vai, 500, 0);
INSERT dbo.PurchaseOrderLine (PurchaseOrderId, OrderMaterialId, MaterialId, MaterialColor, OrderedQty, UnitPrice)
VALUES (@po7, NULL, @mKim, N'', 20, 0);

-- Phiếu nhập đã xác nhận 600/1000 của PO-DEMO-005 (còn phải nhận 400)
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
 (@a4,  900, '2026-11-01', 'DRAFT', NULL, NULL, NULL),
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
-- Công đoạn đầu chuyền nhận BTP từ tổ (giao kèm BTP); các công đoạn sau làm trên hàng đang chạy (không kèm BTP)
UPDATE dbo.Operation SET UsesSemiFinished = 1 WHERE OperationId = @a1;
INSERT dbo.WorkAllocation (MovementType, SewingTeamId, WorkerId, OrderLineId, OperationId, Quantity, PieceQty,
                           ConfirmedQty, ConfirmedPieceQty, ConfirmMode, Status, CreatedAt, ConfirmedAt)
SELECT 'HANDOVER', @may, a.WorkerId, l.OrderLineId, a.OperationId, 300, IIF(a.OperationId = @a1, 300, 0),
       300, IIF(a.OperationId = @a1, 300, 0), 'WORKER', 'CONFIRMED', '2026-10-04 07:45:00', '2026-10-04 08:00:00'
FROM (VALUES (@lM), (@lL)) l(OrderLineId)
CROSS JOIN (VALUES (@w1, @a1), (@w1, @a2), (@w2, @a3), (@w3, @a4), (@w4, @a5)) a(WorkerId, OperationId);

/* ---------- 7. Chấm công (bảng công ngày từ 01/09 đến 20/10) ---------- */
CREATE TABLE #Days (D DATE PRIMARY KEY);
;WITH d AS (SELECT CAST('2026-09-01' AS DATE) AS D UNION ALL SELECT DATEADD(DAY, 1, D) FROM d WHERE D < '2026-10-20')
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
INSERT dbo.Request (RequestCode, RequestTypeCode, Status, TargetDate, Title, Content, CreatedBy, CreatedByTeamId, DecidedAt, DecisionNote)
VALUES ('YC261015-901', 'ATTENDANCE_CORRECTION', 'APPROVED', '2026-10-14', N'Xin chấm công bù cho Nguyễn Thị Lan',
        N'Công nhân quên quẹt thẻ ra ngày 14/10, có làm đủ ca.', @ttmay, @may, '2026-10-15 09:00:00', N'Đồng ý'),
       ('YC261020-902', 'ATTENDANCE_CORRECTION', 'PENDING_APPROVAL', '2026-10-19', N'Xin chấm công bù cho Lê Thị Mai',
        N'Máy chấm công không nhận vân tay ngày 19/10.', @ttmay, @may, NULL, NULL);

/* ---------- 8. Suất ăn 01/09..20/10 (đã chốt) ---------- */
INSERT dbo.DailyMeal (MealDate, Status, OfficeCount, GuestCount, MealPrice, GuestNote)
SELECT d.D, 'OPEN', 5, IIF(DATEDIFF(DAY, 0, d.D) % 7 = 2, 3, 0), 30000, IIF(DATEDIFF(DAY, 0, d.D) % 7 = 2, N'Khách của công ty mẹ', NULL)
FROM #Days d
WHERE DATEDIFF(DAY, 0, d.D) % 7 <> 6
  AND NOT EXISTS (SELECT 1 FROM dbo.DailyMeal m WHERE m.MealDate = d.D);

INSERT dbo.TeamMealConfirmation (DailyMealId, TeamId, MealCount, AttendanceCount, Source, ConfirmedAt)
SELECT m.DailyMealId, t.TeamId, t.Cnt, t.Cnt, 'CONFIRMED', DATEADD(MINUTE, 8 * 60 + 5, CAST(m.MealDate AS DATETIME2(0)))
FROM dbo.DailyMeal m
CROSS JOIN (VALUES (@cat, 8), (@may, 10)) t(TeamId, Cnt)
WHERE m.Status = 'OPEN' AND m.MealDate <= '2026-10-20'
  AND NOT EXISTS (SELECT 1 FROM dbo.TeamMealConfirmation c WHERE c.DailyMealId = m.DailyMealId AND c.TeamId = t.TeamId);

UPDATE dbo.DailyMeal SET Status = 'CLOSED', ClosedAt = DATEADD(MINUTE, 8 * 60 + 40, CAST(MealDate AS DATETIME2(0)))
WHERE Status = 'OPEN' AND MealDate <= '2026-10-20';

/* ---------- 9. Sản lượng cuối ca 05/10..20/10 (KI-04) ---------- */
CREATE TABLE #Assign (WorkerId INT, OperationId INT);
INSERT #Assign VALUES (@w1, @a1), (@w1, @a2), (@w2, @a3), (@w3, @a4), (@w4, @a5);

INSERT dbo.OutputRecord (WorkDate, ShiftCode, SewingTeamId, WorkerId, OperationId, OrderLineId, Quantity, PieceRateId,
                         IsOutsideAssignment, NoAttendanceWarning, EditDeadline, Status)
SELECT d.D, 'AFTERNOON', @may, a.WorkerId, a.OperationId,
       IIF(DAY(d.D) % 2 = 0, @lL, @lM),
       25 + ABS(CHECKSUM(d.D, a.WorkerId, a.OperationId)) % 20,
       pr.PieceRateId,
       0,
       IIF(EXISTS (SELECT 1 FROM dbo.DailyTimesheet t WHERE t.EmployeeId = a.WorkerId AND t.WorkDate = d.D AND t.CheckIn IS NOT NULL), 0, 1),
       dbo.fn_NextWorkingDayEnd(d.D), 'SAVED'
FROM #Days d
CROSS JOIN #Assign a
CROSS APPLY (SELECT TOP (1) PieceRateId, Rate FROM dbo.PieceRate
             WHERE OperationId = a.OperationId AND Status = 'APPROVED' AND EffectiveDate <= d.D
             ORDER BY EffectiveDate DESC) pr
WHERE d.D BETWEEN '2026-10-05' AND '2026-10-20';

/* ---------- 10. QC (KI-06), bàn giao thành phẩm (doanh thu) ---------- */
INSERT dbo.QcBatch (WorkDate, ShiftCode, BatchTime, SewingTeamId, OrderLineId, FinishedQty, InspectorId, EditDeadline) VALUES
 ('2026-09-24', 'AFTERNOON', '16:00', @may, @lM, 150, @qaqc, '2026-09-24 16:30:00'),
 ('2026-09-25', 'AFTERNOON', '15:30', @may, @lM, 150, @qaqc, '2026-09-25 16:30:00'),
 ('2026-09-26', 'MORNING',   '10:30', @may, @lL, 120, @qaqc, '2026-09-26 11:30:00'),
 ('2026-09-28', 'AFTERNOON', '15:00', @may, @lL, 130, @qaqc, '2026-09-28 16:30:00'),
 ('2026-10-13', 'MORNING',   '10:45', @may, @lM,  60, @qaqc, '2026-10-13 11:30:00'),
 ('2026-10-14', 'AFTERNOON', '15:30', @may, @lM,  90, @qaqc, '2026-10-14 16:30:00'),
 ('2026-10-16', 'AFTERNOON', '15:45', @may, @lL, 100, @qaqc, '2026-10-16 16:30:00'),
 ('2026-10-19', 'AFTERNOON', '15:50', @may, @lM,  30, @qaqc, '2026-10-19 16:30:00'),
 ('2026-10-20', 'AFTERNOON', '15:40', @may, @lM,  30, @qaqc, '2026-10-20 16:30:00');

INSERT dbo.FgHandover (HandoverCode, OrderId, ProductId, InspectorId, HandedAt, Status, ConfirmedBy, ConfirmedAt) VALUES
 ('FG-DEMO-001', @o1, @p1, @qaqc, '2026-09-25 16:00:00', 'CONFIRMED', @thukho, '2026-09-25 16:20:00'),
 ('FG-DEMO-002', @o1, @p1, @qaqc, '2026-09-28 16:00:00', 'CONFIRMED', @thukho, '2026-09-28 16:20:00'),
 ('FG-DEMO-003', @o1, @p1, @qaqc, '2026-10-14 16:00:00', 'CONFIRMED', @thukho, '2026-10-14 16:20:00');
INSERT dbo.FgHandoverLine (FgHandoverId, OrderLineId, HandedQty, ConfirmedQty)
SELECT h.FgHandoverId, v.OrderLineId, v.Qty, v.Qty
FROM (VALUES ('FG-DEMO-001', @lM, 300), ('FG-DEMO-002', @lL, 250), ('FG-DEMO-003', @lM, 150), ('FG-DEMO-003', @lL, 100)) v(Code, OrderLineId, Qty)
JOIN dbo.FgHandover h ON h.HandoverCode = v.Code;

/* ---------- 11. Hàng hỏng (KI-08) + thông báo cho công nhân ---------- */
INSERT dbo.DefectRecord (DefectCode, WorkDate, ShiftCode, SewingTeamId, OrderLineId, OperationId, ResponsibleWorkerId, DefectType, Quantity,
                         DefectReasonCode, ReasonNote, PieceRateId, WorkerNotifiedAt, WorkerObjection, ObjectedAt, EditDeadline, Status, RecordedBy, RecordedAt) VALUES
 ('MAY1-261012-001', '2026-10-12', 'AFTERNOON', @may, @lL, @a1, @w1, 'REWORK', 2, 'SKIPPED_STITCH', NULL, NULL, '2026-10-12 16:10:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-12'), 'CANCELLED', @ttmay, '2026-10-12 16:10:00'),
 ('MAY1-261014-001', '2026-10-14', 'AFTERNOON', @may, @lL, @a2, @w1, 'REWORK', 3, 'SKIPPED_STITCH', NULL, NULL, '2026-10-14 16:05:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-14'), 'RECORDED', @ttmay, '2026-10-14 16:05:00'),
 ('MAY1-261015-001', '2026-10-15', 'AFTERNOON', @may, @lM, @a3, @w2, 'SCRAP',  2, 'STAINED',        NULL, (SELECT PieceRateId FROM dbo.PieceRate WHERE OperationId = @a3 AND EffectiveDate = '2026-09-01'), '2026-10-15 16:20:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-15'), 'RECORDED', @ttmay, '2026-10-15 16:20:00'),
 ('MAY1-261016-001', '2026-10-16', 'AFTERNOON', @may, @lL, @a4, @w3, 'SCRAP',  1, 'TORN',           NULL, (SELECT PieceRateId FROM dbo.PieceRate WHERE OperationId = @a4 AND EffectiveDate = '2026-09-01'), '2026-10-16 16:15:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-16'), 'RECORDED', @ttmay, '2026-10-16 16:15:00'),
 ('MAY1-261019-001', '2026-10-19', 'AFTERNOON', @may, @lM, @a5, @w4, 'REWORK', 4, 'CROOKED_SEAM',   NULL, NULL, '2026-10-19 16:10:00', NULL, NULL, dbo.fn_NextWorkingDayEnd('2026-10-19'), 'RECORDED', @ttmay, '2026-10-19 16:10:00'),
 ('MAY1-261020-001', '2026-10-20', 'AFTERNOON', @may, @lL, @a2, @w1, 'SCRAP',  1, 'OTHER',          N'Cháy vải do là quá nhiệt', (SELECT PieceRateId FROM dbo.PieceRate WHERE OperationId = @a2 AND EffectiveDate = '2026-09-01'), '2026-10-20 16:25:00',
  N'Hôm đó em không làm công đoạn này, đề nghị kiểm tra lại', '2026-10-20 18:00:00', dbo.fn_NextWorkingDayEnd('2026-10-20'), 'RECORDED', @ttmay, '2026-10-20 16:25:00');

INSERT dbo.Notification (RecipientId, SourceType, RefTable, RefId, Title, Message, PushStatus)
SELECT d.ResponsibleWorkerId, 'PRODUCTION', 'DefectRecord', d.DefectRecordId, N'Có phiếu hàng hỏng ghi nhận cho bạn',
       N'Phiếu ' + d.DefectCode + N' ghi nhận ' + CAST(d.Quantity AS NVARCHAR(10)) + N' sản phẩm '
       + IIF(d.DefectType = 'SCRAP', N'bị hủy', N'cần sửa lại') + N'. Nếu ghi sai, bạn có thể phản hồi.',
       'SENT'
FROM dbo.DefectRecord d WHERE d.DefectCode LIKE 'MAY1-26%' AND d.Status <> 'CANCELLED' AND d.WorkerNotifiedAt IS NOT NULL;

INSERT dbo.Notification (RecipientId, SourceType, RefTable, RefId, Title, Message, PushStatus)
VALUES (@ketoan, 'SYSTEM', 'PurchaseOrder', @po3, N'Đơn mua chờ thanh toán', N'Đơn mua PO-DEMO-003 đã đặt hàng, chờ Kế toán xác nhận thanh toán.', 'SENT'),
       (@quandoc, 'SYSTEM', 'PurchaseOrder', @po4, N'Đơn mua bị trả lại', N'Kế toán trả lại đơn mua PO-DEMO-004: đơn giá khóa kéo cao hơn báo giá.', 'SENT');

/* ---------- 12. KPI tổ may (MI-16/17/18) ---------- */
INSERT dbo.KpiTarget (OrderId, ProductId, SewingTeamId, StartDate, EndDate, TargetFinishedQty, WorkingDays, Status, Result) VALUES
 (@o1, @p1, @may, '2026-10-01', '2026-10-31', 1200, 22, 'ACTIVE', NULL),
 (@o1, @p1, @may, '2026-09-21', '2026-09-30',  500,  8, 'CLOSED', 'ACHIEVED'),
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
 (N'DEMO-AO01', N'OP04', 900.00, '2026-11-01', 'DRAFT', NULL, NULL, NULL, NULL),
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

-- Công đoạn ↔ chi tiết (25/10): demo nối theo từ khóa trong tên công đoạn; một công đoạn có thể khớp nhiều chi tiết
INSERT dbo.OperationComponent (OperationId, ProductComponentId, ProductId)
SELECT DISTINCT o.OperationId, pc.ProductComponentId, o.ProductId
FROM dbo.Operation o
JOIN dbo.Product p ON p.ProductId = o.ProductId AND p.ProductCode = N'DEMO-AO01'
JOIN (VALUES (N'%tay%', N'Tay áo'), (N'%thân trước%', N'Thân trước'), (N'%TT %', N'Thân trước'),
             (N'%TS%', N'Thân sau'), (N'%sống lưng%', N'Thân sau'),
             (N'Tra tay%', N'Thân trước'), (N'Tra tay%', N'Thân sau'), (N'%sườn áo%', N'Thân trước'), (N'%sườn áo%', N'Thân sau'),
             (N'Chắp vai con', N'Thân trước'), (N'Chắp vai con', N'Thân sau'),
             (N'%gắn mác%', N'Nhãn'), (N'%mác cổ%', N'Nhãn'), (N'%mác sườn%', N'Nhãn'), (N'%nhãn%', N'Nhãn')) k(Pattern, ComponentName)
  ON o.OperationName LIKE k.Pattern
JOIN dbo.ProductComponent pc ON pc.ProductId = o.ProductId AND pc.ComponentName = k.ComponentName
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

PRINT N'NX-MTD: tạo database xong.';
GO
