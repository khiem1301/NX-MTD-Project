/* =====================================================================================
   Gộp các patch 03/11/2026 chưa đẩy lên remote, chạy sau 2026-11-02_all_changes.sql và 2026-11-02_demo_full_test_data.sql:
     1. DEV-ALL-62: chỉ tiêu KPI tổ may chia theo dòng đơn (bảng KpiTargetLine, chia lại chỉ tiêu cũ)
     2. DEV-ALL-63: giao sản lượng công đoạn theo ngày (WorkAllocation.WorkDate, RetrievalNote, ConfirmMode LEADER / SYSTEM,
        lý do thu hồi END_OF_DAY / ADJUSTED)
   DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy. Chạy lại không lỗi.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-03_all_changes.sql
   ===================================================================================== */
GO

/* =====================================================================================
   DEV-ALL-62 · Claude (AI) · 03/11/2026 · Chỉ tiêu KPI tổ may chia theo dòng đơn (màu × size).
     1. Thêm bảng KpiTargetLine.
     2. Chỉ tiêu chưa hủy đang có: chia số tổng cho các dòng của mã hàng đã chia cho tổ, theo tỉ lệ số còn trống của dòng
        (số đặt − phần đã giao ở chỉ tiêu khác). Không đủ chỗ thì để nguyên (chỉ có số tổng).
   Chạy sau 2026-11-02_all_changes.sql và 2026-11-02_demo_full_test_data.sql. DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy.
   Chạy lại không lỗi. Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-03_all_changes.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF OBJECT_ID('dbo.KpiTargetLine', 'U') IS NULL
BEGIN
    BEGIN TRANSACTION;
    CREATE TABLE dbo.KpiTargetLine (
        KpiTargetLineId INT IDENTITY(1,1) CONSTRAINT PK_KpiTargetLine PRIMARY KEY,
        KpiTargetId     INT NOT NULL CONSTRAINT FK_KpiLine_Kpi REFERENCES dbo.KpiTarget(KpiTargetId),
        OrderLineId     INT NOT NULL CONSTRAINT FK_KpiLine_OrderLine REFERENCES dbo.OrderLine(OrderLineId),
        TargetQty       INT NOT NULL CONSTRAINT CK_KpiLine_Qty CHECK (TargetQty > 0),
        CONSTRAINT UQ_KpiTargetLine UNIQUE (KpiTargetId, OrderLineId)
    );
    CREATE INDEX IX_KpiLine_OrderLine ON dbo.KpiTargetLine(OrderLineId) INCLUDE (TargetQty);
    COMMIT TRANSACTION;
    PRINT N'Đã thêm bảng KpiTargetLine.';
END
ELSE PRINT N'KpiTargetLine đã có, bỏ qua tạo bảng.';
GO

-- Chia chỉ tiêu cũ theo màu × size (chỉ tiêu đã có dòng thì bỏ qua)
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
PRINT N'Đã chia chỉ tiêu KPI cũ theo màu × size.';
GO

GO

/* =====================================================================================
   DEV-ALL-63 · Claude (AI) · 03/11/2026 · Giao sản lượng công đoạn theo ngày cho công nhân.
     1. WorkAllocation thêm WorkDate (ngày làm việc của phiếu), RetrievalNote (lý do thu hồi cuối ngày, chữ tự do).
     2. CK_WA_Mode thêm LEADER (tổ trưởng giao trực tiếp, ghi xong là CONFIRMED) và SYSTEM (hệ thống tự thu hồi cuối ngày).
        CK_WA_Behalf chỉ bắt lý do với ON_BEHALF. CK_WA_Retr thêm END_OF_DAY, ADJUSTED.
     3. Phiếu cũ: WorkDate = ngày tạo phiếu.
   Trần sản lượng (vw_WorkerOutputCap, trg_OutputRecord_Check) giữ nguyên: cộng dồn giao − thu hồi; phần chưa làm hết của
   mỗi ngày được thu hồi nên trần của ngày sau không bị dồn.
   Chạy sau 2026-11-03_all_changes.sql. DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy. Chạy lại không lỗi:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-03_all_changes.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF COL_LENGTH('dbo.WorkAllocation', 'WorkDate') IS NULL
BEGIN
    BEGIN TRANSACTION;
    ALTER TABLE dbo.WorkAllocation ADD WorkDate DATE NULL, RetrievalNote NVARCHAR(500) NULL;
    EXEC (N'UPDATE dbo.WorkAllocation SET WorkDate = CAST(CreatedAt AS DATE)');
    EXEC (N'ALTER TABLE dbo.WorkAllocation ALTER COLUMN WorkDate DATE NOT NULL');
    ALTER TABLE dbo.WorkAllocation ADD CONSTRAINT DF_WA_WorkDate DEFAULT (CAST(dbo.fn_VnNow() AS DATE)) FOR WorkDate;
    ALTER TABLE dbo.WorkAllocation DROP CONSTRAINT CK_WA_Retr, CK_WA_Mode, CK_WA_Behalf;
    ALTER TABLE dbo.WorkAllocation ADD
        CONSTRAINT CK_WA_Retr   CHECK (MovementType = 'HANDOVER' OR RetrievalReason IN ('OPERATION_CHANGED','WORKER_ABSENT','STYLE_FINISHED','END_OF_DAY','ADJUSTED')),
        CONSTRAINT CK_WA_Mode   CHECK (ConfirmMode IN ('WORKER','ON_BEHALF','LEADER','SYSTEM')),
        CONSTRAINT CK_WA_Behalf CHECK (ConfirmMode <> 'ON_BEHALF' OR OnBehalfReason IS NOT NULL);
    CREATE INDEX IX_WA_Day ON dbo.WorkAllocation(SewingTeamId, WorkDate) INCLUDE (WorkerId, OrderLineId, OperationId, MovementType, ConfirmedQty, Status);
    COMMIT TRANSACTION;
    PRINT N'Đã thêm WorkAllocation.WorkDate, RetrievalNote.';
END
ELSE PRINT N'WorkAllocation.WorkDate đã có, bỏ qua.';
GO
