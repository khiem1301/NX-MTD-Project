/* =====================================================================================
   Dữ liệu thử trang chủ tổ may: mục "Nhận BTP & cảnh báo chặn".
   BE (HomeDashboardService.SewingAsync): phiếu SEMI_FINISHED + PENDING_RECEIPT của tổ;
   thiếu phân công công đoạn → tag "Chặn" (đỏ), đủ phân công → "Chờ nhận" (cam).
   - PC-TEST-MAY1-WAIT  : DH-2511-006 / MAY1 — Chờ nhận
   - PC-TEST-MAY1-BLOCK : DH-2510-004 / MAY1 — Chặn (đóng phân công 1 công đoạn)
   Chạy lại không nhân đôi.
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-25_sewing_home_btp_pending.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE);
DECLARE @now DATETIME2(0) = dbo.fn_VnNow();
DECLARE @may1 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = N'MAY1');
DECLARE @thukho INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = N'THUKHO');
DECLARE @oWait INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = N'DH-2511-006');
DECLARE @oBlock INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = N'DH-2510-004');

IF @may1 IS NULL OR @thukho IS NULL OR @oWait IS NULL OR @oBlock IS NULL
BEGIN
    RAISERROR(N'Thiếu MAY1 / THUKHO / DH-2511-006 / DH-2510-004 — chạy seed trước.', 16, 1);
    RETURN;
END

/* ---- 0) Sửa phân công DH-2511-006: công đoạn gắn NV tổ khác → đổi sang NV MAY1 (để phiếu WAIT không bị chặn) ---- */
DECLARE @wMay1 INT = (
    SELECT TOP (1) EmployeeId FROM dbo.Employee
    WHERE TeamId = @may1 AND Status = N'ACTIVE' AND EmployeeCode LIKE N'CNMAY%'
    ORDER BY EmployeeId
);
UPDATE a
SET WorkerId = @wMay1
FROM dbo.OperationAssignment a
JOIN dbo.Employee e ON e.EmployeeId = a.WorkerId
WHERE a.SewingTeamId = @may1 AND a.OrderId = @oWait AND a.ValidTo IS NULL
  AND (e.TeamId <> @may1 OR e.Status <> N'ACTIVE');
IF @@ROWCOUNT > 0 PRINT N'Đã chuyển phân công DH-2511-006 sang công nhân MAY1.';

/* ---- 1) Phiếu chờ nhận (đủ phân công) ---- */
IF NOT EXISTS (SELECT 1 FROM dbo.MaterialIssue WHERE IssueCode = N'PC-TEST-MAY1-WAIT')
BEGIN
    INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, CreatedAt)
    VALUES (N'PC-TEST-MAY1-WAIT', N'SEMI_FINISHED', @oWait, @may1, @today, @thukho, N'PENDING_RECEIPT', @now);

    DECLARE @idWait INT = SCOPE_IDENTITY();

    INSERT dbo.MaterialIssueLine (MaterialIssueId, OrderLineId, IssuedQty)
    SELECT TOP (2) @idWait, ol.OrderLineId, 24
    FROM dbo.OrderLine ol
    JOIN dbo.OrderLineTeam olt ON olt.OrderLineId = ol.OrderLineId AND olt.SewingTeamId = @may1
    WHERE ol.OrderId = @oWait
    ORDER BY ol.OrderLineId;

    PRINT N'Đã tạo PC-TEST-MAY1-WAIT (Chờ nhận).';
END
ELSE
    PRINT N'PC-TEST-MAY1-WAIT đã có.';

/* ---- 2) Đóng 1 công đoạn đang phân công của DH-2510-004 để phiếu bị chặn ---- */
DECLARE @opBlock INT = (
    SELECT TOP (1) a.OperationId
    FROM dbo.OperationAssignment a
    WHERE a.SewingTeamId = @may1 AND a.OrderId = @oBlock AND a.ValidTo IS NULL
    ORDER BY a.OperationId
);

IF @opBlock IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.MaterialIssue WHERE IssueCode = N'PC-TEST-MAY1-BLOCK')
BEGIN
    UPDATE dbo.OperationAssignment
    SET ValidTo = DATEADD(DAY, -1, @today)
    WHERE SewingTeamId = @may1 AND OrderId = @oBlock AND OperationId = @opBlock AND ValidTo IS NULL;

    PRINT N'Đã đóng phân công OperationId=' + CAST(@opBlock AS NVARCHAR(20)) + N' trên DH-2510-004.';
END

/* ---- 3) Phiếu bị chặn nhận ---- */
IF NOT EXISTS (SELECT 1 FROM dbo.MaterialIssue WHERE IssueCode = N'PC-TEST-MAY1-BLOCK')
BEGIN
    -- bảo đảm còn ít nhất 1 công đoạn thiếu phân công (nếu lần trước đã tạo rồi rollback tay)
    IF NOT EXISTS (
        SELECT 1
        FROM dbo.Operation o
        JOIN dbo.OrderLine ol ON ol.ProductId = o.ProductId AND ol.OrderId = @oBlock
        WHERE o.Status = N'ACTIVE' AND o.TeamType = N'SEWING'
          AND NOT EXISTS (
              SELECT 1 FROM dbo.OperationAssignment a
              JOIN dbo.Employee e ON e.EmployeeId = a.WorkerId
              WHERE a.OperationId = o.OperationId AND a.OrderId = @oBlock AND a.SewingTeamId = @may1
                AND a.ValidTo IS NULL AND e.Status = N'ACTIVE' AND e.TeamId = @may1)
    )
    BEGIN
        SET @opBlock = (
            SELECT TOP (1) a.OperationId
            FROM dbo.OperationAssignment a
            WHERE a.SewingTeamId = @may1 AND a.OrderId = @oBlock AND a.ValidTo IS NULL
            ORDER BY a.OperationId
        );
        IF @opBlock IS NOT NULL
            UPDATE dbo.OperationAssignment
            SET ValidTo = DATEADD(DAY, -1, @today)
            WHERE SewingTeamId = @may1 AND OrderId = @oBlock AND OperationId = @opBlock AND ValidTo IS NULL;
    END

    INSERT dbo.MaterialIssue (IssueCode, WarehouseType, OrderId, ReceivingTeamId, IssueDate, IssuedBy, Status, CreatedAt)
    VALUES (N'PC-TEST-MAY1-BLOCK', N'SEMI_FINISHED', @oBlock, @may1, @today, @thukho, N'PENDING_RECEIPT', @now);

    DECLARE @idBlock INT = SCOPE_IDENTITY();

    INSERT dbo.MaterialIssueLine (MaterialIssueId, OrderLineId, IssuedQty)
    SELECT TOP (2) @idBlock, ol.OrderLineId, 18
    FROM dbo.OrderLine ol
    JOIN dbo.OrderLineTeam olt ON olt.OrderLineId = ol.OrderLineId AND olt.SewingTeamId = @may1
    WHERE ol.OrderId = @oBlock
    ORDER BY ol.OrderLineId;

    PRINT N'Đã tạo PC-TEST-MAY1-BLOCK (Chặn nhận).';
END
ELSE
    PRINT N'PC-TEST-MAY1-BLOCK đã có.';

PRINT N'Đăng nhập TTMAY / 123456 → Trang chủ → Nhận BTP & cảnh báo chặn.';
GO
