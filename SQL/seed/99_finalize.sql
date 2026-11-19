/* =====================================================================================
   99. HOÀN TẤT: cảnh báo thiếu chấm công trên sản lượng, khóa kỳ kế toán đã chốt lương, kiểm tra tính toàn vẹn, tóm tắt
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE), @now DATETIME2(0) = dbo.fn_VnNow();

-- Sản lượng ghi cho người không có bảng công ngày đó -> cảnh báo
UPDATE o SET NoAttendanceWarning = 1
FROM dbo.OutputRecord o
WHERE NOT EXISTS (SELECT 1 FROM dbo.DailyTimesheet t WHERE t.EmployeeId = o.WorkerId AND t.WorkDate = o.WorkDate AND t.WorkdayValue > 0);

-- Kỳ đã chốt lương (trước tháng 10): chuyển dữ liệu sản xuất sang LOCKED rồi mới khóa kỳ
UPDATE dbo.OutputRecord SET Status = 'LOCKED' WHERE WorkDate < '2026-10-01' AND Status <> 'LOCKED';
UPDATE dbo.DefectRecord SET Status = 'LOCKED' WHERE WorkDate < '2026-10-01' AND Status <> 'LOCKED';
UPDATE dbo.AccountingPeriod SET Status = 'LOCKED', LockedAt = DATEADD(DAY, 7, CAST(EndDate AS DATETIME2)) WHERE EndDate < '2026-10-01';

/* ---------- Kiểm tra toàn vẹn ---------- */
DECLARE @bad INT = 0;
-- 1. Ràng buộc FK / CHECK đều đang được tin cậy
SELECT @bad = COUNT(*) FROM sys.check_constraints WHERE is_not_trusted = 1 OR is_disabled = 1;
IF @bad > 0 PRINT CONCAT(N'CẢNH BÁO: ', @bad, N' CHECK constraint không tin cậy / bị tắt');
SELECT @bad = COUNT(*) FROM sys.foreign_keys WHERE is_not_trusted = 1 OR is_disabled = 1;
IF @bad > 0 PRINT CONCAT(N'CẢNH BÁO: ', @bad, N' FK không tin cậy / bị tắt');
SELECT @bad = COUNT(*) FROM sys.triggers WHERE is_disabled = 1;
IF @bad > 0 PRINT CONCAT(N'CẢNH BÁO: ', @bad, N' trigger đang bị tắt');
-- 2. Tồn kho khớp tổng thẻ kho
SELECT @bad = COUNT(*) FROM dbo.StockBalance sb
WHERE sb.OnHandQty <> ISNULL((SELECT SUM(QtyChange) FROM dbo.StockTransaction t WHERE t.StockBalanceId = sb.StockBalanceId), 0);
IF @bad > 0 PRINT CONCAT(N'CẢNH BÁO: ', @bad, N' dòng tồn kho không khớp thẻ kho');
-- 3. Tổng sản lượng không vượt số lần giao
SELECT @bad = COUNT(*) FROM (SELECT o.WorkerId, o.OrderLineId, o.OperationId, SUM(o.Quantity) AS Q FROM dbo.OutputRecord o GROUP BY o.WorkerId, o.OrderLineId, o.OperationId) x
OUTER APPLY (SELECT SUM(CASE WHEN w.MovementType = 'HANDOVER' THEN w.ConfirmedQty ELSE -w.ConfirmedQty END) AS A FROM dbo.WorkAllocation w
             WHERE w.Status = 'CONFIRMED' AND w.WorkerId = x.WorkerId AND w.OrderLineId = x.OrderLineId AND w.OperationId = x.OperationId) a
WHERE x.Q > ISNULL(a.A, 0);
IF @bad > 0 PRINT CONCAT(N'CẢNH BÁO: ', @bad, N' nhóm sản lượng vượt số lần giao');

/* ---------- Tóm tắt ---------- */
SELECT t.name AS [Bảng], SUM(p.rows) AS [Số dòng]
FROM sys.tables t JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
GROUP BY t.name HAVING SUM(p.rows) > 0 ORDER BY t.name;
PRINT N'99: hoàn tất. Mật khẩu: tài khoản cũ giữ mật khẩu đang dùng, tài khoản mới dùng mật khẩu mặc định (123456).';
