/* =====================================================================================
   Dữ liệu test xem đơn giá công đoạn (09/11/2026)
   ---------------------------------------------------------------------------------
   Mục tiêu:
   - Tổ trưởng (TTMAY): Phân công DEMO-DH003 thấy cột Đơn giá; Giao việc theo ngày (hôm nay)
     thấy đơn giá từng công đoạn của CNMAY1 / CNMAY3.
   - Công nhân (CNMAY1, CNMAY3): màn "Sản lượng công đoạn được giao" thấy đơn giá trên phiếu PENDING hôm nay.

   Điều kiện: đã có demo DEMO-DH003, tổ MAY1, mã DEMO-AO01, BTP đã nhận.
   Chạy lại không nhân đôi (bỏ qua nếu đã có phiếu HANDOVER PENDING của CNMAY1 ngày hôm nay OP01).

   sqlcmd -S localhost -E -C -I -f 65001 -d NXMTD -i SQL/patches/2026-11-09_piece_rate_visibility_test_data.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE),
        @p INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-AO01'),
        @o3 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = 'DEMO-DH003'),
        @may1 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = 'MAY1'),
        @w1 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY1'),
        @w3 INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = 'CNMAY3');

DECLARE @op1 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p AND OperationCode = 'OP01'),
        @op2 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p AND OperationCode = 'OP02'),
        @op3 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p AND OperationCode = 'OP03'),
        @op4 INT = (SELECT OperationId FROM dbo.Operation WHERE ProductId = @p AND OperationCode = 'OP04');

DECLARE @lineM INT = (
    SELECT l.OrderLineId
    FROM dbo.OrderLine l
    JOIN dbo.ProductVariant v ON v.ProductVariantId = l.ProductVariantId
    JOIN dbo.ProductColor c ON c.ProductColorId = v.ProductColorId
    WHERE l.OrderId = @o3 AND l.ProductId = @p AND c.Color = N'Đen' AND v.SizeCode = 'M'
);

IF @o3 IS NULL OR @may1 IS NULL OR @w1 IS NULL OR @lineM IS NULL OR @op1 IS NULL
BEGIN
    PRINT N'Thiếu DEMO-DH003 / MAY1 / CNMAY1 / OP01. Chạy seed demo trước.';
    RETURN;
END;

IF EXISTS (
    SELECT 1 FROM dbo.WorkAllocation
    WHERE WorkerId = @w1 AND OrderLineId = @lineM AND OperationId = @op1
      AND WorkDate = @today AND MovementType = 'HANDOVER' AND Status = 'PENDING'
)
BEGIN
    PRINT N'Dữ liệu test đơn giá hôm nay đã có, bỏ qua.';
    RETURN;
END;

BEGIN TRANSACTION;

/* ---------- 1. Bảo đảm mọi công đoạn may ACTIVE của DEMO-AO01 có đơn giá đã duyệt ---------- */
INSERT dbo.PieceRate (OperationId, Rate, EffectiveDate, Status, SubmittedAt, ApprovedAt, DecisionNote)
SELECT o.OperationId,
       CASE o.OperationCode
           WHEN 'OP01' THEN 1200
           WHEN 'OP02' THEN 1500
           WHEN 'OP03' THEN 1000
           WHEN 'OP04' THEN 800
           WHEN 'OP05' THEN 2000
           ELSE 1100 + (o.StationSequence % 7) * 50
       END,
       '2026-09-01',
       'APPROVED',
       '2026-08-30 10:00:00',
       '2026-08-31 09:00:00',
       N'[TEST] Đơn giá để xem trên phân công / giao việc'
FROM dbo.Operation o
WHERE o.ProductId = @p AND o.Status = 'ACTIVE' AND o.TeamType = 'SEWING'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.PieceRate r
      WHERE r.OperationId = o.OperationId AND r.Status = 'APPROVED' AND r.EffectiveDate <= @today
  );

/* ---------- 2. Phân công (nếu thiếu) để bảng phân công / giao theo ngày lấy sẵn ---------- */
INSERT dbo.OperationAssignment (SewingTeamId, OrderId, OperationId, WorkerId, ValidFrom, AssignedAt)
SELECT v.TeamId, v.OrderId, v.OperationId, v.WorkerId, @today, dbo.fn_VnNow()
FROM (VALUES
    (@may1, @o3, @op1, @w1),
    (@may1, @o3, @op2, @w1),
    (@may1, @o3, @op3, @w3),
    (@may1, @o3, @op4, @w3)
) v(TeamId, OrderId, OperationId, WorkerId)
WHERE v.OperationId IS NOT NULL AND v.WorkerId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM dbo.OperationAssignment a
      WHERE a.SewingTeamId = v.TeamId AND a.OrderId = v.OrderId
        AND a.OperationId = v.OperationId AND a.WorkerId = v.WorkerId AND a.ValidTo IS NULL
  );

/* ---------- 3. Phiếu PENDING hôm nay — công nhân mở màn thấy đơn giá ---------- */
INSERT dbo.WorkAllocation (
    WorkDate, MovementType, SewingTeamId, WorkerId, OrderLineId, OperationId, Quantity,
    ConfirmedQty, ConfirmMode, Status, CreatedAt, ConfirmedAt
)
SELECT @today, 'HANDOVER', @may1, v.WorkerId, @lineM, v.OperationId, v.Qty,
       NULL, 'WORKER', 'PENDING', dbo.fn_VnNow(), NULL
FROM (VALUES
    (@w1, @op1, 5),
    (@w1, @op2, 5),
    (@w3, @op3, 4),
    (@w3, @op4, 4)
) v(WorkerId, OperationId, Qty)
WHERE v.WorkerId IS NOT NULL AND v.OperationId IS NOT NULL;

/* ---------- 4. Phiếu LEADER CONFIRMED hôm nay — tổ trưởng thấy số giao + đơn giá trên board ---------- */
INSERT dbo.WorkAllocation (
    WorkDate, MovementType, SewingTeamId, WorkerId, OrderLineId, OperationId, Quantity,
    ConfirmedQty, ConfirmMode, Status, CreatedAt, ConfirmedAt
)
SELECT @today, 'HANDOVER', @may1, v.WorkerId, @lineM, v.OperationId, v.Qty,
       v.Qty, 'LEADER', 'CONFIRMED', dbo.fn_VnNow(), dbo.fn_VnNow()
FROM (VALUES
    (@w1, @op1, 3),
    (@w1, @op2, 2),
    (@w3, @op3, 2)
) v(WorkerId, OperationId, Qty)
WHERE v.WorkerId IS NOT NULL AND v.OperationId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM dbo.WorkAllocation a
      WHERE a.WorkerId = v.WorkerId AND a.OrderLineId = @lineM AND a.OperationId = v.OperationId
        AND a.WorkDate = @today AND a.ConfirmMode = 'LEADER' AND a.Status = 'CONFIRMED'
  );

COMMIT TRANSACTION;

PRINT N'Đã thêm dữ liệu test đơn giá công đoạn cho ngày ' + CONVERT(nvarchar(10), @today, 23) + N'.';
PRINT N'- TTMAY: Phân công DEMO-DH003; Giao việc theo ngày → CNMAY1 / CNMAY3, Đen-M.';
PRINT N'- CNMAY1 / CNMAY3: Sản lượng công đoạn được giao → phiếu PENDING có đơn giá.';
PRINT N'  Đăng nhập: TTMAY / CNMAY1 / CNMAY3 — mật khẩu 123456.';
GO
