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
                     WHERE l.OrderId = @o3 AND l.ProductId = @p AND v.Color = N'Đen' AND v.SizeCode = 'L');

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
