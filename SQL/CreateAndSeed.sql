/* =====================================================================================
   NX-MTD – Nền tảng số quản lý gia công may mặc
   Script tạo database SQL Server (T-SQL) – bản 21/10/2026
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

   Chạy được trên SQL Server 2019 trở lên (đã chạy thử trên SQL Server 2019 CU32).
   Script tạo database mới tên NXMTD. Nếu database đã tồn tại, hãy xóa trước
   (DROP DATABASE NXMTD) hoặc đổi tên ở phần 0.
   Chạy bằng SSMS / Azure Data Studio, hoặc: sqlcmd -S <server> -U sa -I -i NX-MTD_Database.sql
   (cờ -I bật QUOTED_IDENTIFIER, cần cho filtered index).

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
    Username            VARCHAR(50)   NOT NULL CONSTRAINT UQ_Account_Username UNIQUE,
    PasswordHash        NVARCHAR(500) NOT NULL,          -- chỉ lưu hash (ASP.NET Identity / BCrypt)
    IsTemporaryPassword BIT           NOT NULL CONSTRAINT DF_Account_IsTemp DEFAULT 1,
    SecurityStamp       UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Account_Stamp DEFAULT NEWID(), -- đổi khi đổi/đặt lại mật khẩu → đăng xuất phiên khác
    Status              VARCHAR(20)   NOT NULL CONSTRAINT DF_Account_Status DEFAULT 'ACTIVE',
    LockedAt            DATETIME2(0)  NULL,
    LockReason          NVARCHAR(300) NULL,
    CreatedAt           DATETIME2(0)  NOT NULL CONSTRAINT DF_Account_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_Account_Status CHECK (Status IN ('ACTIVE','LOCKED'))
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
    AnnouncementId   INT            NULL CONSTRAINT FK_Notification_Announcement REFERENCES dbo.Announcement(AnnouncementId),
    RefTable         VARCHAR(50)    NULL,       -- chứng từ liên quan (vd. 'MaterialIssue')
    RefId            BIGINT         NULL,
    Title            NVARCHAR(200)  NOT NULL,
    Message          NVARCHAR(1000) NOT NULL,
    LinkUrl          NVARCHAR(500)  NULL,       -- deep link tới màn hình liên quan
    IsRead           BIT            NOT NULL CONSTRAINT DF_Notification_IsRead DEFAULT 0,
    ReadAt           DATETIME2(0)   NULL,
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

-- Nguyên vật liệu (tạo ngay khi nhập phiếu nhập – UC38). MaterialType là nhóm theo SRS: FABRIC (vải, đi qua tổ cắt)
-- hoặc TRIM (phụ liệu, cấp thẳng cho tổ may); Category là loại chi tiết trong nhóm (18/10)
CREATE TABLE dbo.Material (
    MaterialId    INT IDENTITY(1,1) CONSTRAINT PK_Material PRIMARY KEY,
    MaterialCode  VARCHAR(30)   NOT NULL CONSTRAINT UQ_Material_Code UNIQUE,
    MaterialName  NVARCHAR(200) NOT NULL,
    MaterialType  VARCHAR(10)   NOT NULL,
    Category      VARCHAR(15)   NOT NULL,
    Unit          NVARCHAR(20)  NOT NULL,     -- m, kg, cuộn, cái, cuộn chỉ…
    Status        VARCHAR(20)   NOT NULL CONSTRAINT DF_Material_Status DEFAULT 'ACTIVE',
    CreatedAt     DATETIME2(0)  NOT NULL CONSTRAINT DF_Material_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_Material_Type   CHECK (MaterialType IN ('FABRIC','TRIM')),
    -- vải: vải chính, vải lót, mex, bo; phụ liệu: chỉ, khóa, cúc, cúc dập, chun, dây / viền, nhãn, thẻ treo, bông / đệm, bao bì, khác
    CONSTRAINT CK_Material_Category CHECK (
        (MaterialType = 'FABRIC' AND Category IN ('MAIN_FABRIC','LINING','INTERLINING','RIB'))
     OR (MaterialType = 'TRIM'   AND Category IN ('THREAD','ZIPPER','BUTTON','SNAP','ELASTIC','TAPE',
                                                  'LABEL','HANG_TAG','PADDING','PACKAGING','OTHER'))),
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
    LastSizeRatio   NVARCHAR(200)  NULL,   -- tỉ lệ size bàn cắt gần nhất được nhớ theo mã hàng (FR 2.7), vd 'S:1 M:2 L:2 XL:1'
    Status          VARCHAR(20)    NOT NULL CONSTRAINT DF_Product_Status DEFAULT 'ACTIVE',
    CreatedAt       DATETIME2(0)   NOT NULL CONSTRAINT DF_Product_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_Product_Status CHECK (Status IN ('ACTIVE','DISCONTINUED'))
);

-- Đơn hàng gia công (ORDER) – gốc của mọi dữ liệu sản xuất, kho, tiến độ, doanh thu
CREATE TABLE dbo.CustomerOrder (
    OrderId          INT IDENTITY(1,1) CONSTRAINT PK_CustomerOrder PRIMARY KEY,
    OrderCode        VARCHAR(30)    NOT NULL CONSTRAINT UQ_CustomerOrder_Code UNIQUE,
    CustomerId       INT            NOT NULL CONSTRAINT FK_CustomerOrder_Customer REFERENCES dbo.Customer(CustomerId),
    ReceivedDate     DATE           NOT NULL,
    DueDate          DATE           NOT NULL,
    MaterialStatus   VARCHAR(10)    NOT NULL CONSTRAINT DF_CustomerOrder_MatStatus DEFAULT 'AWAITING', -- tiến độ NVL cả đơn, tách khỏi Status; backend cập nhật khi xác nhận phiếu nhập
    Status           VARCHAR(25)    NOT NULL CONSTRAINT DF_CustomerOrder_Status DEFAULT 'NEW',
    IsDelayRisk      BIT            NOT NULL CONSTRAINT DF_CustomerOrder_DelayRisk DEFAULT 0, -- cờ JOB-04, không phải trạng thái
    DelayRiskSince   DATE           NULL,
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

-- Dòng đơn: [mã hàng – màu – size] – đơn vị trung tâm cho cắt, sản lượng, QC, kho TP, giao hàng
CREATE TABLE dbo.OrderLine (
    OrderLineId    INT IDENTITY(1,1) CONSTRAINT PK_OrderLine PRIMARY KEY,
    OrderId        INT           NOT NULL CONSTRAINT FK_OrderLine_Order REFERENCES dbo.CustomerOrder(OrderId),
    ProductId      INT           NOT NULL CONSTRAINT FK_OrderLine_Product REFERENCES dbo.Product(ProductId),
    Color          NVARCHAR(50)  NOT NULL,
    SizeCode       VARCHAR(10)   NOT NULL,        -- XS, S, M, L, XL, XXL, 3XL, FREE…
    OrderedQty     INT           NOT NULL CONSTRAINT CK_OrderLine_Qty CHECK (OrderedQty > 0),
    CmtUnitPrice   DECIMAL(18,2) NOT NULL CONSTRAINT CK_OrderLine_Price CHECK (CmtUnitPrice >= 0), -- đơn giá gia công, tầng mật
    CONSTRAINT UQ_OrderLine UNIQUE (OrderId, ProductId, Color, SizeCode)
);
CREATE INDEX IX_OrderLine_Product ON dbo.OrderLine(ProductId);

-- NVL của đơn (18/10): mỗi dòng = một NVL + màu mà đơn cần. Là bảng trung tâm của phần NVL:
-- định mức, phần khách cấp (màn 1.2), dòng đơn mua và dòng phiếu nhập đều trỏ về đây.
-- Nguồn suy ra từ số lượng: CustomerQty = 0 là tự mua, CustomerQty >= RequiredQty là khách cấp, còn lại là cả hai.
CREATE TABLE dbo.OrderMaterial (
    OrderMaterialId      INT IDENTITY(1,1) CONSTRAINT PK_OrderMaterial PRIMARY KEY,
    OrderId              INT            NOT NULL CONSTRAINT FK_OM_Order REFERENCES dbo.CustomerOrder(OrderId),
    MaterialId           INT            NOT NULL CONSTRAINT FK_OM_Material REFERENCES dbo.Material(MaterialId),
    MaterialColor        NVARCHAR(50)   NOT NULL CONSTRAINT DF_OM_Color DEFAULT N'',
    RequiredQty          DECIMAL(18,3)  NOT NULL,                                        -- tổng cần theo số liệu khách báo, nhập tay (không tính từ định mức)
    CustomerQty          DECIMAL(18,3)  NOT NULL CONSTRAINT DF_OM_Customer DEFAULT 0,   -- khách sẽ cấp (màn 1.2)
    CustomerExpectedDate DATE           NULL,                                            -- ngày khách dự kiến giao NVL này
    PurchaseQty          AS (CASE WHEN RequiredQty > CustomerQty THEN RequiredQty - CustomerQty ELSE 0 END), -- phần xưởng phải mua
    Source               AS (CONVERT(VARCHAR(10), CASE WHEN CustomerQty = 0 THEN 'SUPPLIER'
                                                       WHEN CustomerQty >= RequiredQty THEN 'CUSTOMER' ELSE 'BOTH' END)),
    Note                 NVARCHAR(500)  NULL,
    CONSTRAINT CK_OM_Qty  CHECK (RequiredQty >= 0 AND CustomerQty >= 0),
    CONSTRAINT CK_OM_Date CHECK (CustomerQty = 0 OR CustomerExpectedDate IS NOT NULL),
    CONSTRAINT UQ_OM UNIQUE (OrderId, MaterialId, MaterialColor)
);

-- Định mức NVL cho 1 sản phẩm (UC09): vải theo từng chi tiết; phụ liệu (chỉ, khóa, cúc…) theo cả sản phẩm (ComponentName = '').
-- Có thể khác theo màu / size sản phẩm (vd khóa dài theo size, chỉ theo màu áo). NVL và màu lấy từ OrderMaterial
CREATE TABLE dbo.OrderMaterialNorm (
    OrderMaterialNormId INT IDENTITY(1,1) CONSTRAINT PK_OrderMaterialNorm PRIMARY KEY,
    OrderMaterialId   INT            NOT NULL CONSTRAINT FK_OMN_OrderMaterial REFERENCES dbo.OrderMaterial(OrderMaterialId),
    ProductId         INT            NOT NULL CONSTRAINT FK_OMN_Product REFERENCES dbo.Product(ProductId),
    ComponentName     NVARCHAR(100)  NOT NULL CONSTRAINT DF_OMN_Component DEFAULT N'',  -- vải: thân trước, tay, cổ…; '' = cả sản phẩm (phụ liệu)
    GarmentColor      NVARCHAR(50)   NULL,               -- NULL = áp dụng mọi màu sản phẩm
    SizeCode          VARCHAR(10)    NULL,               -- NULL = mọi size
    QtyPerPiece       DECIMAL(18,4)  NOT NULL CONSTRAINT CK_OMN_Qty CHECK (QtyPerPiece > 0),  -- theo đơn vị của NVL: m vải, m chỉ, cái khóa…
    WastagePercent    DECIMAL(5,2)   NOT NULL CONSTRAINT DF_OMN_Wastage DEFAULT 0 CONSTRAINT CK_OMN_Wastage CHECK (WastagePercent BETWEEN 0 AND 100),
    CONSTRAINT UQ_OMN UNIQUE (OrderMaterialId, ProductId, ComponentName, GarmentColor, SizeCode)
);

-- Công đoạn của mã hàng (OPERATION)
CREATE TABLE dbo.Operation (
    OperationId      INT IDENTITY(1,1) CONSTRAINT PK_Operation PRIMARY KEY,
    ProductId        INT           NOT NULL CONSTRAINT FK_Operation_Product REFERENCES dbo.Product(ProductId),
    OperationCode    VARCHAR(30)   NOT NULL,
    OperationName    NVARCHAR(200) NOT NULL,
    StationSequence  SMALLINT      NOT NULL,
    SkillLevel       TINYINT       NULL,
    StandardTimeSec  DECIMAL(9,2)  NULL CONSTRAINT CK_Operation_Time CHECK (StandardTimeSec IS NULL OR StandardTimeSec > 0),
    TeamType         VARCHAR(10)   NOT NULL CONSTRAINT DF_Operation_TeamType DEFAULT 'SEWING',
    IsFinalStep      BIT           NOT NULL CONSTRAINT DF_Operation_Final DEFAULT 0,
    Status           VARCHAR(20)   NOT NULL CONSTRAINT DF_Operation_Status DEFAULT 'ACTIVE',
    CONSTRAINT CK_Operation_TeamType CHECK (TeamType IN ('CUTTING','SEWING')),
    CONSTRAINT CK_Operation_Status   CHECK (Status IN ('ACTIVE','DISCONTINUED')),
    CONSTRAINT UQ_Operation UNIQUE (ProductId, OperationCode)
);
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
    ApprovalNote   NVARCHAR(500) NULL,
    RejectReason   NVARCHAR(500) NULL,
    CONSTRAINT CK_PieceRate_Status CHECK (Status IN ('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED')),
    CONSTRAINT CK_PieceRate_Reject CHECK (Status <> 'REJECTED' OR RejectReason IS NOT NULL),
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
    PanelName         NVARCHAR(100)  NULL,   -- chi tiết cần cắt bù: cổ, thân trước… (FR 3.10)
    ItemDescription   NVARCHAR(300)  NULL,   -- linh kiện / máy / vật tư máy (đơn cơ điện)
    Quantity          DECIMAL(18,3)  NOT NULL CONSTRAINT CK_RequestLine_Qty CHECK (Quantity > 0),
    ReplenishQty      DECIMAL(18,3)  NULL,   -- SHORT_DELIVERY: số NCC/khách sẽ bù (Quản đốc nhập)
    MaterialReceiptLineId INT        NULL    -- SHORT_DELIVERY: dòng phiếu nhập thiếu / lỗi (số thiếu, số lỗi xem ở dòng đó) – FK thêm sau
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
    OriginalPurchaseOrderId INT          NULL CONSTRAINT FK_PO_Original REFERENCES dbo.PurchaseOrder(PurchaseOrderId), -- đặt lại phần thiếu không bù
    OrderDate             DATE           NULL,
    ExpectedDeliveryDate  DATE           NULL,
    GoodsStatus           VARCHAR(25)    NOT NULL CONSTRAINT DF_PO_GoodsStatus DEFAULT 'PENDING_FINALISATION',
    PaymentStatus         VARCHAR(20)    NOT NULL CONSTRAINT DF_PO_PaymentStatus DEFAULT 'UNPAID',
    RejectReason          NVARCHAR(500)  NULL,
    PaymentReturnReason   NVARCHAR(500)  NULL,  -- Kế toán trả lại Quản đốc
    -- Thanh toán do Kế toán xác nhận (PAYMENT, UC34); ảnh chứng từ trong Attachment (OwnerType = PURCHASE_ORDER)
    PaymentDate           DATE           NULL,
    PaidAmount            DECIMAL(18,2)  NULL,
    PaymentMethod         VARCHAR(20)    NULL,
    PaymentNote           NVARCHAR(500)  NULL,
    PaidAt                DATETIME2(0)   NULL,
    Note                  NVARCHAR(1000) NULL,
    CreatedAt             DATETIME2(0)   NOT NULL CONSTRAINT DF_PO_CreatedAt DEFAULT (dbo.fn_VnNow()),
    OrderedAt             DATETIME2(0)   NULL,
    RowVer                ROWVERSION,
    CONSTRAINT CK_PO_GoodsStatus CHECK (GoodsStatus IN ('PENDING_FINALISATION','ORDERED','PARTIALLY_RECEIVED','RECEIVED',
                                                         'CLOSED_SHORT','CUSTOMER_SUPPLIED','REJECTED')),
    CONSTRAINT CK_PO_PaymentStatus CHECK (PaymentStatus IN ('UNPAID','PAID','RETURNED')),
    CONSTRAINT CK_PO_PaymentMethod CHECK (PaymentMethod IS NULL OR PaymentMethod IN ('BANK_TRANSFER','CASH')),
    CONSTRAINT CK_PO_Paid   CHECK (PaymentStatus <> 'PAID' OR (PaymentDate IS NOT NULL AND PaidAmount > 0 AND PaymentMethod IS NOT NULL)),
    CONSTRAINT CK_PO_PayReturn CHECK (PaymentStatus <> 'RETURNED' OR PaymentReturnReason IS NOT NULL),
    CONSTRAINT CK_PO_Ordered CHECK (GoodsStatus IN ('PENDING_FINALISATION','CUSTOMER_SUPPLIED','REJECTED')
                                    OR (SupplierId IS NOT NULL AND OrderDate IS NOT NULL)),
    CONSTRAINT CK_PO_Reject CHECK (GoodsStatus <> 'REJECTED' OR RejectReason IS NOT NULL)
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

-- Phiếu nhập NVL: loại Mới / Bổ sung (FR 1.4, UC37, UC39)
CREATE TABLE dbo.MaterialReceipt (
    MaterialReceiptId   INT IDENTITY(1,1) CONSTRAINT PK_MaterialReceipt PRIMARY KEY,
    ReceiptCode         VARCHAR(30)    NOT NULL CONSTRAINT UQ_MaterialReceipt_Code UNIQUE,
    ReceiptType         VARCHAR(15)    NOT NULL,
    OriginalReceiptId   INT            NULL CONSTRAINT FK_MR_Original REFERENCES dbo.MaterialReceipt(MaterialReceiptId),
    ShortageRequestId   INT            NULL CONSTRAINT FK_MR_ShortageRequest REFERENCES dbo.Request(RequestId), -- phiếu bổ sung trả lời yêu cầu nào
    OrderId             INT            NULL CONSTRAINT FK_MR_Order REFERENCES dbo.CustomerOrder(OrderId), -- NULL = phiếu nhập của đơn mua dùng chung (TH-12)
    Source              VARCHAR(10)    NOT NULL,
    PurchaseOrderId     INT            NULL CONSTRAINT FK_MR_PO REFERENCES dbo.PurchaseOrder(PurchaseOrderId),
    ReceiptDate         DATE           NOT NULL,
    Status              VARCHAR(20)    NOT NULL CONSTRAINT DF_MR_Status DEFAULT 'PENDING',
    ReturnReason        NVARCHAR(500)  NULL,
    Note                NVARCHAR(1000) NULL,
    SubmittedAt         DATETIME2(0)   NOT NULL CONSTRAINT DF_MR_SubmittedAt DEFAULT (dbo.fn_VnNow()),
    ConfirmedBy         INT            NULL CONSTRAINT FK_MR_ConfirmedBy REFERENCES dbo.Employee(EmployeeId),
    ConfirmedAt         DATETIME2(0)   NULL,
    RowVer              ROWVERSION,
    CONSTRAINT CK_MR_Type    CHECK (ReceiptType IN ('NEW','SUPPLEMENTARY')),
    CONSTRAINT CK_MR_Source  CHECK (Source IN ('CUSTOMER','SUPPLIER')),
    CONSTRAINT CK_MR_Status  CHECK (Status IN ('PENDING','CONFIRMED','RETURNED')),
    CONSTRAINT CK_MR_Supp    CHECK (ReceiptType = 'NEW' OR OriginalReceiptId IS NOT NULL),
    CONSTRAINT CK_MR_PO      CHECK (Source = 'CUSTOMER' OR PurchaseOrderId IS NOT NULL), -- NCC lấy từ đơn mua, khách lấy từ đơn hàng
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
    ToReplenishQty        DECIMAL(18,3) NULL,          -- chỉ phiếu bổ sung (read-only trên màn hình)
    AcceptedQty           DECIMAL(18,3) NOT NULL CONSTRAINT DF_MRL_Accepted DEFAULT 0,
    ShortQty              DECIMAL(18,3) NOT NULL CONSTRAINT DF_MRL_Short DEFAULT 0,
    DefectiveQty          DECIMAL(18,3) NOT NULL CONSTRAINT DF_MRL_Defective DEFAULT 0,
    MaterialId            INT           NULL CONSTRAINT FK_MRL_Material REFERENCES dbo.Material(MaterialId), -- chỉ khi OrderMaterialId NULL
    MaterialColor         NVARCHAR(50)  NULL,
    CONSTRAINT CK_MRL_Qty CHECK (AcceptedQty >= 0 AND ShortQty >= 0 AND DefectiveQty >= 0 AND AcceptedQty + ShortQty + DefectiveQty > 0),
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

-- Số cần cắt theo màu × size (PanelName cho cắt bù từng chi tiết)
CREATE TABLE dbo.WorkOrderLine (
    WorkOrderLineId  INT IDENTITY(1,1) CONSTRAINT PK_WorkOrderLine PRIMARY KEY,
    WorkOrderId      INT           NOT NULL CONSTRAINT FK_WOL_WO REFERENCES dbo.WorkOrder(WorkOrderId),
    OrderLineId      INT           NOT NULL CONSTRAINT FK_WOL_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    PanelName        NVARCHAR(100) NOT NULL CONSTRAINT DF_WOL_Panel DEFAULT N'',  -- '' = cả bộ chi tiết
    PlannedQty       INT           NOT NULL CONSTRAINT CK_WOL_Qty CHECK (PlannedQty > 0),
    CONSTRAINT UQ_WOL UNIQUE (WorkOrderId, OrderLineId, PanelName)
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
    IssuedQty           DECIMAL(18,3) NOT NULL CONSTRAINT CK_MIL_Issued CHECK (IssuedQty > 0),
    ConfirmedQty        DECIMAL(18,3) NULL,
    DiscrepancyReason   NVARCHAR(500) NULL,
    CONSTRAINT CK_MIL_Confirmed CHECK (ConfirmedQty IS NULL OR (ConfirmedQty >= 0 AND ConfirmedQty <= IssuedQty)),
    CONSTRAINT CK_MIL_Reason    CHECK (ConfirmedQty IS NULL OR ConfirmedQty = IssuedQty OR DiscrepancyReason IS NOT NULL), -- GBR-06
    CONSTRAINT CK_MIL_Item      CHECK ((MaterialId IS NOT NULL AND MaterialColor IS NOT NULL AND OrderLineId IS NULL)
                                    OR (MaterialId IS NULL AND MaterialColor IS NULL AND OrderLineId IS NOT NULL)),
    CONSTRAINT UQ_MIL UNIQUE (MaterialIssueId, MaterialId, MaterialColor, OrderLineId)
);

-- Phiếu trả vải thừa: chỉ tổ cắt lập, thủ kho xác nhận (FR 2.10, 2.11)
CREATE TABLE dbo.MaterialReturn (
    MaterialReturnId  INT IDENTITY(1,1) CONSTRAINT PK_MaterialReturn PRIMARY KEY,
    ReturnCode        VARCHAR(30)    NOT NULL CONSTRAINT UQ_MaterialReturn_Code UNIQUE,
    MaterialIssueId   INT            NOT NULL CONSTRAINT FK_MRet_Issue REFERENCES dbo.MaterialIssue(MaterialIssueId),
    CuttingTeamId     INT            NOT NULL CONSTRAINT FK_MRet_Team REFERENCES dbo.Team(TeamId),
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

-- Bàn cắt (CUTTING_LAY, FR 2.7): số lớp × tỉ lệ size
CREATE TABLE dbo.CuttingLay (
    CuttingLayId    INT IDENTITY(1,1) CONSTRAINT PK_CuttingLay PRIMARY KEY,
    WorkOrderId     INT            NOT NULL CONSTRAINT FK_CL_WO REFERENCES dbo.WorkOrder(WorkOrderId),
    CuttingTeamId   INT            NOT NULL CONSTRAINT FK_CL_Team REFERENCES dbo.Team(TeamId),
    WorkDate        DATE           NOT NULL,
    ShiftCode       VARCHAR(10)    NOT NULL CONSTRAINT CK_CL_Shift CHECK (ShiftCode IN ('MORNING','AFTERNOON','OVERTIME')),
    LaySequence     SMALLINT       NOT NULL,
    FabricMaterialId INT           NULL CONSTRAINT FK_CL_Material REFERENCES dbo.Material(MaterialId),
    FabricColor     NVARCHAR(50)   NOT NULL,
    PlyCount        SMALLINT       NOT NULL CONSTRAINT CK_CL_Ply CHECK (PlyCount > 0),
    SizeRatio       NVARCHAR(200)  NOT NULL,     -- 'S:1 M:2 L:2 XL:1'
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
    RatioValue       SMALLINT      NOT NULL CONSTRAINT CK_CLL_Ratio CHECK (RatioValue > 0),
    CalculatedQty    INT           NOT NULL CONSTRAINT CK_CLL_Calc CHECK (CalculatedQty >= 0),   -- số lớp × tỉ lệ
    ActualQty        INT           NOT NULL CONSTRAINT CK_CLL_Actual CHECK (ActualQty >= 0),     -- có thể chỉnh, kèm lý do
    AdjustReason     NVARCHAR(500) NULL,
    CONSTRAINT CK_CLL_Reason CHECK (ActualQty = CalculatedQty OR AdjustReason IS NOT NULL),
    CONSTRAINT UQ_CLL UNIQUE (CuttingLayId, OrderLineId)
);

-- Bàn giao BTP từ tổ cắt vào kho BTP (FR 2.8, 2.9, UC43)
CREATE TABLE dbo.CutHandover (
    CutHandoverId   INT IDENTITY(1,1) CONSTRAINT PK_CutHandover PRIMARY KEY,
    HandoverCode    VARCHAR(30)    NOT NULL CONSTRAINT UQ_CutHandover_Code UNIQUE,
    WorkOrderId     INT            NOT NULL CONSTRAINT FK_CH_WO REFERENCES dbo.WorkOrder(WorkOrderId),
    CuttingTeamId   INT            NOT NULL CONSTRAINT FK_CH_Team REFERENCES dbo.Team(TeamId),
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
    HandedQty         INT           NOT NULL CONSTRAINT CK_CHL_Handed CHECK (HandedQty > 0),
    ConfirmedQty      INT           NULL,
    DiscrepancyReason NVARCHAR(500) NULL,
    CONSTRAINT CK_CHL_Confirmed CHECK (ConfirmedQty IS NULL OR ConfirmedQty >= 0),
    CONSTRAINT CK_CHL_Reason    CHECK (ConfirmedQty IS NULL OR ConfirmedQty = HandedQty OR DiscrepancyReason IS NOT NULL),
    CONSTRAINT UQ_CHL UNIQUE (CutHandoverId, OrderLineId)
);
GO

/* =====================================================================================
   7. LUỒNG MAY: phân công công đoạn, giao/thu hồi BTP cho công nhân, sản lượng,
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
    CopiedFromAssignmentId INT   NULL CONSTRAINT FK_OA_CopiedFrom REFERENCES dbo.OperationAssignment(OperationAssignmentId),
    AssignedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_OA_AssignedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_OA_Dates CHECK (ValidTo IS NULL OR ValidTo >= ValidFrom)
);
CREATE UNIQUE INDEX UX_OA_Current ON dbo.OperationAssignment(SewingTeamId, OrderId, OperationId, WorkerId) WHERE ValidTo IS NULL;

-- Giao BTP cho công nhân / thu hồi BTP từ công nhân (FR 3.5, 3.6, 3.8; UC53, UC54, UC60)
CREATE TABLE dbo.WorkerPieceMovement (
    WorkerPieceMovementId INT IDENTITY(1,1) CONSTRAINT PK_WorkerPieceMovement PRIMARY KEY,
    MovementType      VARCHAR(10)    NOT NULL,             -- HANDOVER: giao; RETRIEVAL: thu hồi
    SewingTeamId      INT            NOT NULL CONSTRAINT FK_WPM_Team REFERENCES dbo.Team(TeamId),
    WorkerId          INT            NOT NULL CONSTRAINT FK_WPM_Worker REFERENCES dbo.Employee(EmployeeId),
    OrderLineId       INT            NOT NULL CONSTRAINT FK_WPM_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    OperationId       INT            NULL CONSTRAINT FK_WPM_Operation REFERENCES dbo.Operation(OperationId), -- công đoạn công nhân đang làm
    Quantity          INT            NOT NULL CONSTRAINT CK_WPM_Qty CHECK (Quantity > 0),
    ConfirmedQty      INT            NULL,
    DiscrepancyReason NVARCHAR(500)  NULL,
    RetrievalReason   VARCHAR(20)    NULL,
    ConfirmMode       VARCHAR(10)    NOT NULL,             -- WORKER: CN tự xác nhận; ON_BEHALF: tổ trưởng xác nhận hộ
    OnBehalfReason    NVARCHAR(500)  NULL,
    Status            VARCHAR(10)    NOT NULL CONSTRAINT DF_WPM_Status DEFAULT 'PENDING',
    RejectReason      NVARCHAR(500)  NULL,
    CreatedAt         DATETIME2(0)   NOT NULL CONSTRAINT DF_WPM_CreatedAt DEFAULT (dbo.fn_VnNow()),
    ConfirmedAt       DATETIME2(0)   NULL,
    CONSTRAINT CK_WPM_Type    CHECK (MovementType IN ('HANDOVER','RETRIEVAL')),
    CONSTRAINT CK_WPM_Retr    CHECK (MovementType = 'HANDOVER' OR RetrievalReason IN ('OPERATION_CHANGED','WORKER_ABSENT','STYLE_FINISHED')),
    CONSTRAINT CK_WPM_Mode    CHECK (ConfirmMode IN ('WORKER','ON_BEHALF')),
    CONSTRAINT CK_WPM_Behalf  CHECK (ConfirmMode = 'WORKER' OR OnBehalfReason IS NOT NULL),
    CONSTRAINT CK_WPM_Status  CHECK (Status IN ('PENDING','CONFIRMED','REJECTED')),
    CONSTRAINT CK_WPM_Confirmed CHECK (ConfirmedQty IS NULL OR (ConfirmedQty >= 0 AND (ConfirmedQty = Quantity OR DiscrepancyReason IS NOT NULL))),
    CONSTRAINT CK_WPM_Reject  CHECK (Status <> 'REJECTED' OR RejectReason IS NOT NULL)
);
CREATE INDEX IX_WPM_Worker ON dbo.WorkerPieceMovement(WorkerId, OrderLineId, Status) INCLUDE (MovementType, ConfirmedQty);
CREATE INDEX IX_WPM_Team ON dbo.WorkerPieceMovement(SewingTeamId, OrderLineId, Status);

-- Sản lượng công đoạn cuối ca (OUTPUT_RECORD, UC55). Đơn giá khóa lúc nhập (UnitRate)
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
    UnitRate           DECIMAL(18,2)  NOT NULL CONSTRAINT CK_OR_Rate CHECK (UnitRate >= 0),  -- khóa theo ngày nhập
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
CREATE INDEX IX_OR_Worker ON dbo.OutputRecord(WorkerId, WorkDate) INCLUDE (Quantity, UnitRate, OrderLineId, OperationId);
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
    ScrapUnitRate       DECIMAL(18,2)  NULL,    -- đơn giá khóa của công đoạn, dùng trừ lương tạm tính (chỉ Scrap)
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
    CONSTRAINT CK_DR_Rate   CHECK (DefectType = 'REWORK' OR ScrapUnitRate IS NOT NULL)
);
CREATE INDEX IX_DR_Worker ON dbo.DefectRecord(ResponsibleWorkerId, WorkDate) INCLUDE (DefectType, Quantity, ScrapUnitRate, Status);
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
    DeliveryConfirmedAt DATETIME2(0)   NULL,
    RowVer              ROWVERSION,
    CONSTRAINT CK_DN_Status CHECK (Status IN ('PENDING','REJECTED','CANCELLED','IN_DELIVERY','DELIVERED','PARTIALLY_DELIVERED')),
    CONSTRAINT CK_DN_Reject CHECK (Status <> 'REJECTED' OR RejectReason IS NOT NULL),
    CONSTRAINT CK_DN_Delivered CHECK (Status NOT IN ('DELIVERED','PARTIALLY_DELIVERED') OR CustomerReceivedDate IS NOT NULL)
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
   9. KHO: kiểm kê, thẻ kho (STOCK_TRANSACTION); tồn hiện tại xem view vw_StockBalance
      Ba kho: MATERIAL (NVL theo Material + màu), SEMI_FINISHED (BTP theo dòng đơn),
      FINISHED (thành phẩm theo dòng đơn)
   ===================================================================================== */

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
    CONSTRAINT CK_Stocktake_Return CHECK (Status <> 'RETURNED' OR ReturnReason IS NOT NULL)
);
CREATE TABLE dbo.StocktakeLine (
    StocktakeLineId INT IDENTITY(1,1) CONSTRAINT PK_StocktakeLine PRIMARY KEY,
    StocktakeId     INT           NOT NULL CONSTRAINT FK_STL_Stocktake REFERENCES dbo.Stocktake(StocktakeId),
    MaterialId      INT           NULL CONSTRAINT FK_STL_Material REFERENCES dbo.Material(MaterialId),
    MaterialColor   NVARCHAR(50)  NULL,
    OrderLineId     INT           NULL CONSTRAINT FK_STL_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    BookQty         DECIMAL(18,3) NOT NULL,
    CountedQty      DECIMAL(18,3) NULL,                       -- trống = khớp sổ
    DifferenceQty   AS (ISNULL(CountedQty, BookQty) - BookQty),
    Note            NVARCHAR(300) NULL,
    CONSTRAINT CK_STL_Item CHECK ((MaterialId IS NOT NULL AND OrderLineId IS NULL) OR (MaterialId IS NULL AND OrderLineId IS NOT NULL))
);

-- Thẻ kho: mỗi dòng chứng từ được xác nhận = 1 giao dịch; chỉ thêm, không sửa/xóa.
-- BalanceAfter = tồn sau giao dịch, không được âm (GBR-07)
CREATE TABLE dbo.StockTransaction (
    StockTransactionId BIGINT IDENTITY(1,1) CONSTRAINT PK_StockTransaction PRIMARY KEY,
    WarehouseType      VARCHAR(15)   NOT NULL,
    MaterialId         INT           NULL CONSTRAINT FK_ST_Material REFERENCES dbo.Material(MaterialId),
    MaterialColor      NVARCHAR(50)  NULL,
    OrderLineId        INT           NULL CONSTRAINT FK_ST_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
    SourceDocType      VARCHAR(30)   NOT NULL,
    SourceDocId        INT           NOT NULL,
    SourceLineId       INT           NULL,
    QtyChange          DECIMAL(18,3) NOT NULL CONSTRAINT CK_ST_Qty CHECK (QtyChange <> 0),
    BalanceAfter       DECIMAL(18,3) NOT NULL CONSTRAINT CK_ST_Balance CHECK (BalanceAfter >= 0),
    TransactionDate    DATE          NOT NULL,
    CreatedAt          DATETIME2(0)  NOT NULL CONSTRAINT DF_ST_CreatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_ST_WH   CHECK (WarehouseType IN ('MATERIAL','SEMI_FINISHED','FINISHED')),
    CONSTRAINT CK_ST_Item CHECK ((WarehouseType = 'MATERIAL' AND MaterialId IS NOT NULL AND MaterialColor IS NOT NULL AND OrderLineId IS NULL)
                              OR (WarehouseType <> 'MATERIAL' AND MaterialId IS NULL AND MaterialColor IS NULL AND OrderLineId IS NOT NULL)),
    CONSTRAINT CK_ST_DocType CHECK (SourceDocType IN ('MATERIAL_RECEIPT','MATERIAL_ISSUE','MATERIAL_RETURN','CUT_HANDOVER',
                                                       'FG_HANDOVER','DISPATCH_NOTE','STOCKTAKE')),
    CONSTRAINT UQ_ST_SourceLine UNIQUE (SourceDocType, SourceDocId, SourceLineId, WarehouseType)  -- một giao dịch / dòng chứng từ
);
CREATE INDEX IX_ST_Material ON dbo.StockTransaction(MaterialId, MaterialColor, CreatedAt) WHERE MaterialId IS NOT NULL;
CREATE INDEX IX_ST_OrderLine ON dbo.StockTransaction(OrderLineId, WarehouseType, CreatedAt) WHERE OrderLineId IS NOT NULL;
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
    Source           VARCHAR(15)   NOT NULL CONSTRAINT DF_DT_Source DEFAULT 'FINGERPRINT',
    AttendanceAdjustmentId INT     NULL CONSTRAINT FK_DT_Adjustment REFERENCES dbo.AttendanceAdjustment(AttendanceAdjustmentId),
    Note             NVARCHAR(300) NULL,
    UpdatedAt        DATETIME2(0)  NOT NULL CONSTRAINT DF_DT_UpdatedAt DEFAULT (dbo.fn_VnNow()),
    CONSTRAINT CK_DT_Source CHECK (Source IN ('FINGERPRINT','ADJUSTMENT')),
    CONSTRAINT UQ_DailyTimesheet UNIQUE (EmployeeId, WorkDate)
);
CREATE INDEX IX_DT_WorkDate ON dbo.DailyTimesheet(WorkDate) INCLUDE (EmployeeId, WorkdayValue, OvertimeHours);

-- Suất ăn ngày (DAILY_MEAL): mở → tự chốt 08:30 → Kế toán chốt → đã báo bếp
CREATE TABLE dbo.DailyMeal (
    DailyMealId        INT IDENTITY(1,1) CONSTRAINT PK_DailyMeal PRIMARY KEY,
    MealDate           DATE           NOT NULL CONSTRAINT UQ_DailyMeal_Date UNIQUE,
    Status             VARCHAR(20)    NOT NULL CONSTRAINT DF_DM_Status DEFAULT 'OPEN',
    OfficeCount        INT            NOT NULL CONSTRAINT DF_DM_Office DEFAULT 0,
    GuestCount         INT            NOT NULL CONSTRAINT DF_DM_Guest DEFAULT 0,   -- suất khách (FR 5.2)
    GuestNote          NVARCHAR(300)  NULL,
    TeamTotal          INT            NOT NULL CONSTRAINT DF_DM_Team DEFAULT 0,
    TotalMeals         AS (OfficeCount + GuestCount + TeamTotal) PERSISTED,
    MealPrice          DECIMAL(18,2)  NULL,
    TotalAmount        AS (CONVERT(DECIMAL(18,2), (OfficeCount + GuestCount + TeamTotal) * MealPrice)),
    AttendanceIncomplete BIT          NOT NULL CONSTRAINT DF_DM_Incomplete DEFAULT 0, -- "Thiếu dữ liệu chấm công"
    AutoClosedAt       DATETIME2(0)   NULL,
    ClosedAt           DATETIME2(0)   NULL,
    KitchenNotifiedAt  DATETIME2(0)   NULL,
    CONSTRAINT CK_DM_Status CHECK (Status IN ('OPEN','AUTO_CLOSED','CLOSED','KITCHEN_NOTIFIED')),
    CONSTRAINT CK_DM_Counts CHECK (OfficeCount >= 0 AND GuestCount >= 0 AND TeamTotal >= 0),
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
    CONSTRAINT CK_AP_Month  CHECK (PeriodMonth LIKE '[12][0-9][0-9][0-9]-[01][0-9]'),
    CONSTRAINT CK_AP_Dates  CHECK (EndDate >= StartDate)
);

-- Bảng lương tháng: Kế toán tính bằng Excel rồi import; Admin duyệt (GBR-12)
CREATE TABLE dbo.Payroll (
    PayrollId      INT IDENTITY(1,1) CONSTRAINT PK_Payroll PRIMARY KEY,
    PeriodId       INT            NOT NULL CONSTRAINT UQ_Payroll_Period UNIQUE
                                  CONSTRAINT FK_Payroll_Period REFERENCES dbo.AccountingPeriod(PeriodId),  -- một bảng lương / tháng
    Status         VARCHAR(20)    NOT NULL CONSTRAINT DF_Payroll_Status DEFAULT 'DRAFT',
    ImportedFileUrl NVARCHAR(500) NULL,
    ImportedAt     DATETIME2(0)   NULL,
    TotalAmount    DECIMAL(18,2)  NOT NULL CONSTRAINT DF_Payroll_Total DEFAULT 0,
    SubmittedAt    DATETIME2(0)   NULL,
    ApprovedAt     DATETIME2(0)   NULL,
    RejectReason   NVARCHAR(500)  NULL,
    ReopenReason   NVARCHAR(500)  NULL,
    RowVer         ROWVERSION,
    CONSTRAINT CK_Payroll_Status CHECK (Status IN ('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED'))
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
-- Hóa đơn bếp cuối tháng (UC30) là một dòng CostCategoryCode = 'MEALS'; đối soát với Σ DailyMeal.TotalAmount.
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
    DriverName        NVARCHAR(100) NOT NULL,
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

/* =====================================================================================
   16. VIEW TÍNH TOÁN (tồn kho, trần sản lượng, tiến độ, lương tạm tính, KPI, doanh thu)
   ===================================================================================== */

-- Tồn hiện tại của 3 kho, tính từ thẻ kho (không lưu bảng số dư riêng):
-- tồn thực = Σ QtyChange; giữ chỗ = phiếu cấp phát chờ nhận + phiếu xuất TP chờ xuất; khả dụng = tồn thực − giữ chỗ
CREATE VIEW dbo.vw_StockBalance
AS
SELECT  x.WarehouseType, x.MaterialId, x.MaterialColor, x.OrderLineId,
        SUM(x.OnHandQty)                    AS OnHandQty,
        SUM(x.ReservedQty)                  AS ReservedQty,
        SUM(x.OnHandQty) - SUM(x.ReservedQty) AS AvailableQty
FROM (
    SELECT WarehouseType, MaterialId, MaterialColor, OrderLineId, QtyChange AS OnHandQty, CAST(0 AS DECIMAL(18,3)) AS ReservedQty
    FROM dbo.StockTransaction
    UNION ALL      -- phiếu cấp NVL / BTP đang chờ tổ nhận
    SELECT i.WarehouseType, l.MaterialId, l.MaterialColor, l.OrderLineId, 0, l.IssuedQty
    FROM dbo.MaterialIssue i JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = i.MaterialIssueId
    WHERE i.Status = 'PENDING_RECEIPT'
    UNION ALL      -- phiếu xuất thành phẩm đang chờ Thủ kho xuất
    SELECT 'FINISHED', NULL, NULL, l.OrderLineId, 0, l.IssueQty
    FROM dbo.DispatchNote d JOIN dbo.DispatchNoteLine l ON l.DispatchNoteId = d.DispatchNoteId
    WHERE d.Status = 'PENDING'
) x
GROUP BY x.WarehouseType, x.MaterialId, x.MaterialColor, x.OrderLineId;
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

-- Tồn NVL kèm thông tin vật tư (màn hình kho NVL)
CREATE VIEW dbo.vw_MaterialStock
AS
SELECT  m.MaterialId, m.MaterialCode, m.MaterialName, m.MaterialType, m.Category, m.Unit,
        sb.MaterialColor, sb.OnHandQty, sb.ReservedQty, sb.AvailableQty
FROM dbo.vw_StockBalance sb
JOIN dbo.Material m ON m.MaterialId = sb.MaterialId
WHERE sb.WarehouseType = 'MATERIAL';
GO

-- BTP tổ may đang giữ theo dòng đơn = đã nhận từ kho − đã giao CN + đã thu hồi − Scrap (UC24)
CREATE VIEW dbo.vw_TeamPieceHolding
AS
WITH Received AS (
    SELECT i.ReceivingTeamId AS SewingTeamId, l.OrderLineId, SUM(CAST(l.ConfirmedQty AS INT)) AS Qty
    FROM dbo.MaterialIssue i JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = i.MaterialIssueId
    WHERE i.WarehouseType = 'SEMI_FINISHED' AND i.Status = 'RECEIVED' GROUP BY i.ReceivingTeamId, l.OrderLineId
), Moves AS (
    SELECT SewingTeamId, OrderLineId,
           SUM(CASE WHEN MovementType = 'HANDOVER'  THEN ConfirmedQty ELSE 0 END) AS HandedToWorkers,
           SUM(CASE WHEN MovementType = 'RETRIEVAL' THEN ConfirmedQty ELSE 0 END) AS RetrievedFromWorkers
    FROM dbo.WorkerPieceMovement WHERE Status = 'CONFIRMED' GROUP BY SewingTeamId, OrderLineId
)
SELECT r.SewingTeamId, r.OrderLineId,
       r.Qty                                   AS ReceivedFromWarehouse,
       ISNULL(m.HandedToWorkers, 0)            AS HandedToWorkers,
       ISNULL(m.RetrievedFromWorkers, 0)       AS RetrievedFromWorkers,
       r.Qty - ISNULL(m.HandedToWorkers, 0) + ISNULL(m.RetrievedFromWorkers, 0) AS HeldByTeam
FROM Received r
LEFT JOIN Moves m ON m.SewingTeamId = r.SewingTeamId AND m.OrderLineId = r.OrderLineId;
GO

-- Trần sản lượng của công nhân theo dòng đơn & công đoạn: đã nhận − đã thu hồi; còn lại = trần − đã may − Scrap
CREATE VIEW dbo.vw_WorkerOutputCap
AS
WITH Moves AS (
    SELECT WorkerId, OrderLineId,
           SUM(CASE WHEN MovementType = 'HANDOVER'  THEN ConfirmedQty ELSE 0 END)
         - SUM(CASE WHEN MovementType = 'RETRIEVAL' THEN ConfirmedQty ELSE 0 END) AS CapQty
    FROM dbo.WorkerPieceMovement WHERE Status = 'CONFIRMED' GROUP BY WorkerId, OrderLineId
), Outp AS (
    SELECT WorkerId, OrderLineId, OperationId, SUM(Quantity) AS OutputQty
    FROM dbo.OutputRecord GROUP BY WorkerId, OrderLineId, OperationId
), Scrap AS (
    SELECT ResponsibleWorkerId AS WorkerId, OrderLineId, OperationId, SUM(Quantity) AS ScrapQty
    FROM dbo.DefectRecord WHERE DefectType = 'SCRAP' AND Status <> 'CANCELLED'
    GROUP BY ResponsibleWorkerId, OrderLineId, OperationId
)
SELECT mv.WorkerId, mv.OrderLineId, o.OperationId,
       mv.CapQty,
       ISNULL(o.OutputQty, 0) AS OutputQty,
       ISNULL(s.ScrapQty, 0)  AS ScrapQty,
       mv.CapQty - ISNULL(o.OutputQty, 0) AS RemainingQty
FROM Moves mv
LEFT JOIN Outp o  ON o.WorkerId = mv.WorkerId AND o.OrderLineId = mv.OrderLineId
LEFT JOIN Scrap s ON s.WorkerId = mv.WorkerId AND s.OrderLineId = mv.OrderLineId AND s.OperationId = o.OperationId;
GO

-- Lương tạm tính theo ngày của công nhân may (UC59): Σ(SL × đơn giá khóa) − Σ(Scrap × đơn giá đó)
CREATE VIEW dbo.vw_WorkerDailyEstimatedPay
AS
WITH Outp AS (
    SELECT WorkerId, WorkDate, SUM(Quantity) AS OutputQty, SUM(Quantity * UnitRate) AS OutputAmount
    FROM dbo.OutputRecord GROUP BY WorkerId, WorkDate
), Scrap AS (
    SELECT ResponsibleWorkerId AS WorkerId, WorkDate, SUM(Quantity) AS ScrapQty, SUM(Quantity * ScrapUnitRate) AS ScrapAmount
    FROM dbo.DefectRecord WHERE DefectType = 'SCRAP' AND Status <> 'CANCELLED'
    GROUP BY ResponsibleWorkerId, WorkDate
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
CREATE VIEW dbo.vw_OrderLineProgress
AS
SELECT  ol.OrderLineId, ol.OrderId, o.OrderCode, ol.ProductId, p.ProductCode, ol.Color, ol.SizeCode, ol.OrderedQty,
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
OUTER APPLY (SELECT SUM(ActualQty) Qty FROM dbo.CuttingLayLine WHERE OrderLineId = ol.OrderLineId) cut
OUTER APPLY (SELECT SUM(l.ConfirmedQty) Qty FROM dbo.CutHandoverLine l JOIN dbo.CutHandover h ON h.CutHandoverId = l.CutHandoverId
             WHERE l.OrderLineId = ol.OrderLineId AND h.Status = 'CONFIRMED') ch
OUTER APPLY (SELECT SUM(CAST(l.ConfirmedQty AS INT)) Qty FROM dbo.MaterialIssueLine l JOIN dbo.MaterialIssue i ON i.MaterialIssueId = l.MaterialIssueId
             WHERE l.OrderLineId = ol.OrderLineId AND i.WarehouseType = 'SEMI_FINISHED' AND i.Status = 'RECEIVED') sfi
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
INSERT dbo.Account (EmployeeId, Username, PasswordHash, IsTemporaryPassword)
VALUES (SCOPE_IDENTITY(), 'ADMIN', N'__SET_BY_BACKEND_SEED__', 1);

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
   Username = EmployeeCode. PasswordHash là placeholder, backend ghi đè bằng hash thật khi seed
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

INSERT dbo.Account (EmployeeId, Username, PasswordHash, IsTemporaryPassword)
SELECT e.EmployeeId, e.EmployeeCode, N'__SET_BY_BACKEND_SEED__', 1
FROM dbo.Employee e
WHERE e.EmployeeCode IN (SELECT Code FROM @seed)
  AND NOT EXISTS (SELECT 1 FROM dbo.Account a WHERE a.EmployeeId = e.EmployeeId);

-- Kiểm tra nhanh danh sách tài khoản (bỏ comment khi cần):
-- SELECT a.Username, r.RoleCode, t.TeamCode
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
 ('Product', N'Mã hàng'), ('Operation', N'Công đoạn'), ('PieceRate', N'Đơn giá công đoạn'),
 ('OrderMaterial', N'NVL của đơn: cần bao nhiêu, khách cấp bao nhiêu, phải mua bao nhiêu'), ('OrderMaterialNorm', N'Định mức NVL cho 1 sản phẩm (vải theo chi tiết, phụ liệu theo sản phẩm)'), ('KpiTarget', N'KPI tổ may'),
 ('Request', N'Đơn từ (10 loại, gồm yêu cầu NVL / BTP)'), ('RequestLine', N'Dòng chi tiết đơn từ'),
 ('Material', N'Nguyên vật liệu'), ('Supplier', N'Nhà cung cấp'), ('PurchaseOrder', N'Đơn mua NVL kèm thanh toán (OrderId NULL = đơn mua dùng chung)'),
 ('PurchaseOrderLine', N'Dòng đơn mua NVL'),
 ('MaterialReceipt', N'Phiếu nhập NVL (mới / bổ sung)'), ('MaterialReceiptLine', N'Dòng phiếu nhập NVL'),
 ('WorkOrder', N'Lệnh cắt'), ('WorkOrderLine', N'Dòng lệnh cắt'), ('CuttingLay', N'Bàn cắt'),
 ('CuttingLayLine', N'Số cắt theo size của bàn cắt'), ('CutHandover', N'Bàn giao BTP vào kho'), ('CutHandoverLine', N'Dòng bàn giao BTP'),
 ('MaterialIssue', N'Phiếu cấp kho: NVL cho tổ cắt/may hoặc BTP cho tổ may'), ('MaterialIssueLine', N'Dòng phiếu cấp kho'),
 ('MaterialReturn', N'Phiếu trả vải thừa'), ('MaterialReturnLine', N'Dòng phiếu trả vải thừa'),
 ('OperationAssignment', N'Phân công công đoạn'), ('WorkerPieceMovement', N'Giao / thu hồi BTP với công nhân'),
 ('OutputRecord', N'Sản lượng công đoạn'), ('DefectRecord', N'Hàng lỗi Rework/Scrap và người gây lỗi'),
 ('QcBatch', N'Đợt kiểm hoàn thiện'), ('FgHandover', N'Bàn giao thành phẩm'), ('FgHandoverLine', N'Dòng bàn giao thành phẩm'),
 ('DispatchNote', N'Phiếu xuất kho thành phẩm (kiêm lệnh giao hàng)'), ('DispatchNoteLine', N'Dòng phiếu xuất kho'),
 ('Stocktake', N'Phiếu kiểm kê'), ('StocktakeLine', N'Dòng kiểm kê'), ('StockTransaction', N'Thẻ kho (tồn tính ở vw_StockBalance)'),
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

PRINT N'NX-MTD: tạo database xong.';
GO
