/* =====================================================================================
   DEV-ALL-79: loại phiếu lỗi mới OUTPUT_ERR "Nhập sai sản lượng" (tổ trưởng nhập sai sản lượng công đoạn).
     1. CK_DR_Type thêm 'OUTPUT_ERR'.
     2. Sản lượng hiệu lực của công nhân = Σ OutputRecord − Σ phiếu OUTPUT_ERR (chưa hủy), dùng ở:
        trg_WorkAllocation_Check, trg_OutputRecord_Check (trần sản lượng), vw_WorkerOutputCap,
        vw_OperationAllocationProgress (đã làm), vw_WorkerDailyEstimatedPay (sản lượng và tiền theo đơn giá khóa của phiếu).
   Chạy sau 2026-11-05_employee_name_letters_only.sql. DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy. Chạy lại không lỗi.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-06_defect_output_error.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

/* =========================== 1. CK_DR_Type =========================== */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DR_Type' AND parent_object_id = OBJECT_ID('dbo.DefectRecord')
           AND definition NOT LIKE '%OUTPUT_ERR%')
BEGIN
    ALTER TABLE dbo.DefectRecord DROP CONSTRAINT CK_DR_Type;
    ALTER TABLE dbo.DefectRecord ADD CONSTRAINT CK_DR_Type CHECK (DefectType IN ('REWORK','SCRAP','OUTPUT_ERR'));
    PRINT N'CK_DR_Type đã thêm OUTPUT_ERR.';
END
ELSE PRINT N'CK_DR_Type đã có OUTPUT_ERR, bỏ qua.';
GO

/* =========================== 2. Trigger =========================== */
CREATE OR ALTER TRIGGER dbo.trg_WorkAllocation_Check ON dbo.WorkAllocation AFTER INSERT, UPDATE AS
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

CREATE OR ALTER TRIGGER dbo.trg_OutputRecord_Check ON dbo.OutputRecord AFTER INSERT, UPDATE AS
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

/* =========================== 3. View =========================== */
CREATE OR ALTER VIEW dbo.vw_WorkerOutputCap
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

CREATE OR ALTER VIEW dbo.vw_OperationAllocationProgress
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

CREATE OR ALTER VIEW dbo.vw_WorkerDailyEstimatedPay
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
