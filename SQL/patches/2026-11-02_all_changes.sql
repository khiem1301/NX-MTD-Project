/* =====================================================================================
   Gộp các patch 01/11 - 02/11/2026 chưa đẩy lên remote, chạy sau 2026-11-01_all_changes.sql đã có trên remote:
     1. Bàn cắt lưu chiều dài bàn cắt (CuttingLay.LayLengthM)
     2. Bìa cắt của mã hàng (ProductMarker, ProductMarkerFabric)
     3. Chia dòng đơn cho tổ may (OrderLineTeam, thay OrderProductTeam)
     4. Ràng buộc biên của công đoạn và đơn giá
     5. Bỏ Operation.UsesSemiFinished và xóa khỏi nhật ký
     6. Dữ liệu demo: phân công đủ công đoạn cho DEMO-DH003
   DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy. Chạy lại không lỗi.
   Mỗi phần là một lô riêng (kết thúc bằng GO): phần nào bỏ qua (RETURN) thì chỉ thoát lô của phần đó.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-02_all_changes.sql
   ===================================================================================== */

/* =========================== 1. Bàn cắt lưu chiều dài bàn cắt (CuttingLay.LayLengthM) =========================== */
-- DEV-ALL-43 · Claude (AI) · 01/11/2026 · Bàn cắt lưu chiều dài bàn cắt.
-- Vải đã cắt của bàn = PlyCount × LayLengthM (tính, không lưu). Bàn nhập trước đó để NULL, sửa bàn thì phải nhập.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF COL_LENGTH('dbo.CuttingLay', 'LayLengthM') IS NOT NULL
BEGIN
    PRINT N'CuttingLay.LayLengthM đã có, bỏ qua.';
    RETURN;
END;

BEGIN TRANSACTION;
ALTER TABLE dbo.CuttingLay ADD LayLengthM DECIMAL(7,2) NULL CONSTRAINT CK_CL_Length CHECK (LayLengthM > 0);
COMMIT TRANSACTION;
PRINT N'Đã thêm CuttingLay.LayLengthM.';
GO

/* =========================== 2. Bìa cắt của mã hàng (ProductMarker, ProductMarkerFabric) =========================== */
-- DEV-ALL-42 · Claude (AI) · 01/11/2026 · Bìa cắt của mã hàng: tính số mét vải của lệnh cắt cả bộ.
-- ProductMarker: kích thước từng bìa theo size. ProductMarkerFabric: bìa nào, màu áo nào, dùng vải nào.
-- Chỉ thêm bảng, không đổi dữ liệu đang có. Chạy sau 2026-11-01_all_changes.sql.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.ProductMarker', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductMarker (
        ProductMarkerId     INT IDENTITY(1,1) CONSTRAINT PK_ProductMarker PRIMARY KEY,
        ProductId           INT            NOT NULL CONSTRAINT FK_PM_Product REFERENCES dbo.Product(ProductId),
        MarkerNo            TINYINT        NOT NULL,
        SizeCode            VARCHAR(10)    NOT NULL,
        LengthCm            DECIMAL(7,2)   NOT NULL,
        WidthCm             DECIMAL(7,2)   NOT NULL,
        CONSTRAINT CK_PM_No   CHECK (MarkerNo > 0),
        CONSTRAINT CK_PM_Size CHECK (LengthCm > 0 AND WidthCm > 0),
        CONSTRAINT UQ_PM UNIQUE (ProductId, MarkerNo, SizeCode)
    );
    PRINT N'Đã tạo bảng ProductMarker.';
END
ELSE PRINT N'ProductMarker đã có, bỏ qua.';

IF OBJECT_ID('dbo.ProductMarkerFabric', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductMarkerFabric (
        ProductMarkerFabricId INT IDENTITY(1,1) CONSTRAINT PK_ProductMarkerFabric PRIMARY KEY,
        ProductId           INT            NOT NULL CONSTRAINT FK_PMF_Product REFERENCES dbo.Product(ProductId),
        MarkerNo            TINYINT        NOT NULL,
        Color               NVARCHAR(50)   NOT NULL,
        MaterialId          INT            NOT NULL CONSTRAINT FK_PMF_Material REFERENCES dbo.Material(MaterialId),
        CONSTRAINT CK_PMF_No CHECK (MarkerNo > 0),
        CONSTRAINT UQ_PMF UNIQUE (ProductId, MarkerNo, Color)
    );
    CREATE INDEX IX_PMF_Material ON dbo.ProductMarkerFabric(MaterialId);
    PRINT N'Đã tạo bảng ProductMarkerFabric.';
END
ELSE PRINT N'ProductMarkerFabric đã có, bỏ qua.';

COMMIT TRANSACTION;
GO

/* =========================== 3. Chia dòng đơn cho tổ may (OrderLineTeam, thay OrderProductTeam) =========================== */
-- DEV-ALL-49 · Claude (AI) · 02/11/2026 · Quản đốc, Admin chia dòng đơn (mã hàng × màu × size) cho tổ may.
-- Thêm bảng OrderLineTeam. DB đã có bảng OrderProductTeam (chia theo mã hàng): mỗi dòng chia cũ thành mọi màu × size của mã đó rồi xóa bảng cũ.
-- DB chưa có: tổ đang có phân công, phiếu cấp BTP, chỉ tiêu KPI hoặc đơn Xin BTP đang chờ của mã hàng thì được chia mọi màu × size của mã đó.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF OBJECT_ID('dbo.OrderLineTeam', 'U') IS NULL
BEGIN
    BEGIN TRANSACTION;
    CREATE TABLE dbo.OrderLineTeam (
        OrderLineTeamId INT IDENTITY(1,1) CONSTRAINT PK_OrderLineTeam PRIMARY KEY,
        OrderLineId  INT          NOT NULL CONSTRAINT FK_OLT_OrderLine REFERENCES dbo.OrderLine(OrderLineId) ON DELETE CASCADE,
        SewingTeamId INT          NOT NULL CONSTRAINT FK_OLT_Team REFERENCES dbo.Team(TeamId),
        AssignedAt   DATETIME2(0) NOT NULL CONSTRAINT DF_OLT_AssignedAt DEFAULT (dbo.fn_VnNow()),
        CONSTRAINT UQ_OrderLineTeam UNIQUE (OrderLineId, SewingTeamId)
    );
    CREATE INDEX IX_OLT_Team ON dbo.OrderLineTeam(SewingTeamId, OrderLineId);

    IF OBJECT_ID('dbo.OrderProductTeam', 'U') IS NOT NULL
    BEGIN
        INSERT dbo.OrderLineTeam (OrderLineId, SewingTeamId, AssignedAt)
        SELECT line.OrderLineId, x.SewingTeamId, x.AssignedAt
        FROM dbo.OrderProductTeam x
        JOIN dbo.OrderLine line ON line.OrderId = x.OrderId AND line.ProductId = x.ProductId;
        PRINT N'Đã chuyển ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' dòng chia từ OrderProductTeam.';
        DROP TABLE dbo.OrderProductTeam;
    END
    ELSE
    BEGIN
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
        PRINT N'Đã chia sẵn ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' dòng đơn cho tổ.';
    END
    COMMIT TRANSACTION;
END;
GO

/* =========================== 4. Ràng buộc biên của công đoạn và đơn giá =========================== */
-- DEV-ALL-50 · Claude (AI) · 02/11/2026 · Ràng buộc biên của công đoạn và đơn giá ở DB, khớp validate ở API.
-- Operation: bậc từ 1 đến 9, thứ tự lớn hơn 0. PieceRate: đơn giá lớn hơn 0 (trước là không âm).
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @bad INT = (SELECT COUNT(*) FROM dbo.Operation WHERE SkillLevel NOT BETWEEN 1 AND 9 OR StationSequence <= 0)
                 + (SELECT COUNT(*) FROM dbo.PieceRate WHERE Rate <= 0);
IF @bad > 0
BEGIN
    DECLARE @msg NVARCHAR(200) = N'Có ' + CAST(@bad AS NVARCHAR(10)) + N' dòng vi phạm, hãy sửa dữ liệu trước khi chạy patch.';
    THROW 50099, @msg, 1;
END;

BEGIN TRANSACTION;
IF OBJECT_ID('dbo.CK_Operation_Skill', 'C') IS NULL
    ALTER TABLE dbo.Operation ADD CONSTRAINT CK_Operation_Skill CHECK (SkillLevel IS NULL OR SkillLevel BETWEEN 1 AND 9);
IF OBJECT_ID('dbo.CK_Operation_Sequence', 'C') IS NULL
    ALTER TABLE dbo.Operation ADD CONSTRAINT CK_Operation_Sequence CHECK (StationSequence > 0);
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PieceRate_Rate' AND definition LIKE '%>=%')
BEGIN
    ALTER TABLE dbo.PieceRate DROP CONSTRAINT CK_PieceRate_Rate;
    ALTER TABLE dbo.PieceRate ADD CONSTRAINT CK_PieceRate_Rate CHECK (Rate > 0);
END;
COMMIT TRANSACTION;
GO

/* =========================== 5. Bỏ Operation.UsesSemiFinished và xóa khỏi nhật ký =========================== */
-- DEV-ALL-50 · Claude (AI) · 02/11/2026 · Bỏ ô tích "Giao việc kèm BTP" của công đoạn.
-- Operation.UsesSemiFinished từ 26/10 chỉ để tham khảo, không còn quy tắc nào dùng: bỏ cột, bỏ cột trong
-- vw_OperationAllocationProgress. Dữ liệu đã nhập ở cột này bị xóa; trường UsesSemiFinished cũng bị xóa khỏi OldValues / NewValues của các dòng AuditLog của Operation. DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

-- View phải bỏ cột trước khi bỏ cột ở bảng
ALTER VIEW dbo.vw_OperationAllocationProgress
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
        ISNULL(o.OutputQty, 0)    AS OutputQty
FROM Received r
JOIN dbo.OrderLine ol ON ol.OrderLineId = r.OrderLineId
JOIN dbo.Operation op ON op.ProductId = ol.ProductId AND op.TeamType = 'SEWING' AND op.Status = 'ACTIVE'
OUTER APPLY (SELECT SUM(CASE WHEN w.Status = 'CONFIRMED' THEN IIF(w.MovementType = 'HANDOVER', w.ConfirmedQty, -w.ConfirmedQty) ELSE 0 END) AS AllocatedQty,
                    SUM(CASE WHEN w.Status = 'PENDING' AND w.MovementType = 'HANDOVER' THEN w.Quantity ELSE 0 END) AS PendingQty
             FROM dbo.WorkAllocation w
             WHERE w.SewingTeamId = r.SewingTeamId AND w.OrderLineId = r.OrderLineId AND w.OperationId = op.OperationId) a
OUTER APPLY (SELECT SUM(x.Quantity) AS OutputQty FROM dbo.OutputRecord x
             WHERE x.SewingTeamId = r.SewingTeamId AND x.OrderLineId = r.OrderLineId AND x.OperationId = op.OperationId) o;;
GO

IF COL_LENGTH('dbo.Operation', 'UsesSemiFinished') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_Operation_Semi' AND parent_object_id = OBJECT_ID('dbo.Operation'))
        ALTER TABLE dbo.Operation DROP CONSTRAINT DF_Operation_Semi;
    ALTER TABLE dbo.Operation DROP COLUMN UsesSemiFinished;
    PRINT N'Đã bỏ Operation.UsesSemiFinished.';
END
ELSE PRINT N'Operation.UsesSemiFinished đã bỏ từ trước.';
GO

-- Nhật ký cũ của công đoạn còn trường đã bỏ: xóa khỏi JSON để màn nhật ký không hiện trường không còn tồn tại
UPDATE dbo.AuditLog
SET OldValues = JSON_MODIFY(OldValues, '$.UsesSemiFinished', NULL),
    NewValues = JSON_MODIFY(NewValues, '$.UsesSemiFinished', NULL)
WHERE TableName = 'Operation'
  AND (OldValues LIKE '%UsesSemiFinished%' OR NewValues LIKE '%UsesSemiFinished%');
PRINT N'Đã xóa UsesSemiFinished khỏi ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' dòng nhật ký.';
GO

/* =========================== 6. Dữ liệu demo: phân công đủ công đoạn cho DEMO-DH003 =========================== */
-- DEV-ALL-48 · Claude (AI) · 02/11/2026 · Dữ liệu demo: phân công đủ công đoạn cho đơn DEMO-DH003.
-- Từ DEV-ALL-48 tổ may phải phân công đủ mới nhận được BTP, nên phiếu PC-DEMO-006 cần đủ phân công để demo được.
-- Chỉ thêm phân công còn thiếu, không đổi phân công đang có. Database không có dữ liệu demo thì không làm gì.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @order INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH003'),
        @product INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-AO01'),
        @team INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY1'),
        @w1 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY1'),
        @w2 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY2'),
        @w3 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY3'),
        @w4 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY4');

IF @order IS NULL OR @product IS NULL OR @team IS NULL OR @w1 IS NULL OR @w2 IS NULL OR @w3 IS NULL OR @w4 IS NULL
BEGIN
    PRINT N'Không có dữ liệu demo, bỏ qua.';
    RETURN;
END;

BEGIN TRANSACTION;
INSERT dbo.OperationAssignment (SewingTeamId, OrderId, OperationId, WorkerId, ValidFrom)
SELECT @team, @order, o.OperationId, CHOOSE(ROW_NUMBER() OVER (ORDER BY o.StationSequence) % 4 + 1, @w1, @w2, @w3, @w4), '2026-10-25'
FROM dbo.Operation o
WHERE o.ProductId = @product AND o.Status = 'ACTIVE' AND o.TeamType = 'SEWING'
  AND NOT EXISTS (SELECT 1 FROM dbo.OperationAssignment a
                  WHERE a.SewingTeamId = @team AND a.OrderId = @order AND a.OperationId = o.OperationId AND a.ValidTo IS NULL);
PRINT N'Đã thêm ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' phân công cho DEMO-DH003.';
COMMIT TRANSACTION;
GO
