/* =====================================================================================
   Dữ liệu thử: trang chủ tổ trưởng may → KPI của tổ → bấm đơn mở KPI theo ngày.
   - Đảm bảo chỉ tiêu ACTIVE DH-2511-006 / MAY1 có lịch ngày (nếu thiếu).
   - Thêm vài lô QC để tiến độ và ô "Thực tế" trên lịch khác 0.
   - Bỏ cờ mật khẩu tạm của TTMAY / TPMAY để đăng nhập 123456 không bị ép đổi.
   Chạy lại không nhân đôi.
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-25_team_kpi_calendar_testdata.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE);
DECLARE @may1 INT = (SELECT TeamId FROM dbo.Team WHERE TeamCode = N'MAY1');
DECLARE @qaqc INT = (SELECT EmployeeId FROM dbo.Employee WHERE EmployeeCode = N'QAQC1');
DECLARE @o6 INT = (SELECT OrderId FROM dbo.CustomerOrder WHERE OrderCode = N'DH-2511-006');
DECLARE @p6 INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = N'AS-0412');
DECLARE @kpi INT = (
    SELECT TOP (1) KpiTargetId
    FROM dbo.KpiTarget
    WHERE OrderId = @o6 AND ProductId = @p6 AND SewingTeamId = @may1 AND Status = N'ACTIVE'
    ORDER BY KpiTargetId DESC
);

IF @may1 IS NULL OR @o6 IS NULL OR @p6 IS NULL OR @qaqc IS NULL
BEGIN
    RAISERROR(N'Thiếu Team MAY1 / đơn DH-2511-006 / mã AS-0412 / QAQC1 — chạy seed trước.', 16, 1);
    RETURN;
END

IF @kpi IS NULL
BEGIN
    DECLARE @start DATE = DATEADD(DAY, -1, @today);
    DECLARE @end DATE = DATEADD(DAY, 18, @today);
    DECLARE @wd INT = (
        SELECT COUNT(*)
        FROM (SELECT TOP 80 ROW_NUMBER() OVER (ORDER BY (SELECT 1)) - 1 AS i FROM sys.all_objects) n
        WHERE DATEADD(DAY, n.i, @start) <= @end
          AND DATEDIFF(DAY, '2000-01-02', DATEADD(DAY, n.i, @start)) % 7 <> 0
    );
    DECLARE @tgt INT = (
        SELECT SUM(ol.OrderedQty)
        FROM dbo.OrderLine ol
        JOIN dbo.OrderLineTeam olt ON olt.OrderLineId = ol.OrderLineId AND olt.SewingTeamId = @may1
        WHERE ol.OrderId = @o6 AND ol.ProductId = @p6
    );

    INSERT dbo.KpiTarget (OrderId, ProductId, SewingTeamId, StartDate, EndDate, TargetFinishedQty, WorkingDays, Status, Result, CreatedAt)
    VALUES (@o6, @p6, @may1, @start, @end, @tgt, @wd, N'ACTIVE', NULL, DATEADD(DAY, -3, CAST(@today AS DATETIME2)));
    SET @kpi = SCOPE_IDENTITY();

    INSERT dbo.KpiTargetLine (KpiTargetId, OrderLineId, TargetQty)
    SELECT @kpi, ol.OrderLineId, ol.OrderedQty
    FROM dbo.OrderLine ol
    JOIN dbo.OrderLineTeam olt ON olt.OrderLineId = ol.OrderLineId AND olt.SewingTeamId = @may1
    WHERE ol.OrderId = @o6 AND ol.ProductId = @p6;

    INSERT dbo.KpiDailyTarget (KpiTargetId, WorkDate, TargetQty)
    SELECT @kpi, f.D,
           (@tgt * f.i) / @wd - (@tgt * (f.i - 1)) / @wd
    FROM (
        SELECT DATEADD(DAY, n.i, @start) AS D,
               ROW_NUMBER() OVER (ORDER BY DATEADD(DAY, n.i, @start)) AS i
        FROM (SELECT TOP 80 ROW_NUMBER() OVER (ORDER BY (SELECT 1)) - 1 AS i FROM sys.all_objects) n
        WHERE DATEADD(DAY, n.i, @start) <= @end
          AND DATEDIFF(DAY, '2000-01-02', DATEADD(DAY, n.i, @start)) % 7 <> 0
    ) f
    WHERE (@tgt * f.i) / @wd - (@tgt * (f.i - 1)) / @wd > 0;

    PRINT N'Đã tạo chỉ tiêu ACTIVE DH-2511-006 / MAY1 + lịch ngày.';
END
ELSE
    PRINT N'Đã có chỉ tiêu ACTIVE KpiTargetId=' + CAST(@kpi AS NVARCHAR(20));

-- QC mẫu: vài ngày đầu kỳ (chỉ thêm khi chưa có QC của tổ trên dòng đơn này trong kỳ KPI)
IF NOT EXISTS (
    SELECT 1
    FROM dbo.QcBatch q
    JOIN dbo.OrderLine ol ON ol.OrderLineId = q.OrderLineId
    JOIN dbo.KpiTarget k ON k.KpiTargetId = @kpi
    WHERE q.SewingTeamId = @may1
      AND ol.OrderId = @o6 AND ol.ProductId = @p6
      AND q.WorkDate BETWEEN k.StartDate AND k.EndDate
)
BEGIN
    DECLARE @lineM INT = (
        SELECT TOP (1) ol.OrderLineId
        FROM dbo.OrderLine ol
        JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
        JOIN dbo.OrderLineTeam olt ON olt.OrderLineId = ol.OrderLineId AND olt.SewingTeamId = @may1
        WHERE ol.OrderId = @o6 AND ol.ProductId = @p6 AND pv.SizeCode = N'M'
        ORDER BY ol.OrderLineId
    );
    DECLARE @lineL INT = (
        SELECT TOP (1) ol.OrderLineId
        FROM dbo.OrderLine ol
        JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
        JOIN dbo.OrderLineTeam olt ON olt.OrderLineId = ol.OrderLineId AND olt.SewingTeamId = @may1
        WHERE ol.OrderId = @o6 AND ol.ProductId = @p6 AND pv.SizeCode = N'L'
        ORDER BY ol.OrderLineId
    );

    DECLARE @d1 DATE = (SELECT StartDate FROM dbo.KpiTarget WHERE KpiTargetId = @kpi);
    DECLARE @d2 DATE = DATEADD(DAY, 3, @d1);
    DECLARE @d3 DATE = DATEADD(DAY, 4, @d1);
    IF DATEDIFF(DAY, '2000-01-02', @d2) % 7 = 0 SET @d2 = DATEADD(DAY, 1, @d2);
    IF DATEDIFF(DAY, '2000-01-02', @d3) % 7 = 0 SET @d3 = DATEADD(DAY, 1, @d3);

    INSERT dbo.QcBatch (WorkDate, ShiftCode, BatchTime, SewingTeamId, OrderLineId, FinishedQty, InspectorId, EditDeadline, CreatedAt)
    VALUES
     (@d1, N'MORNING',   '10:30', @may1, @lineM, 12, @qaqc, DATEADD(HOUR, 8, CAST(DATEADD(DAY, 1, @d1) AS DATETIME2)), DATEADD(HOUR, 10, CAST(@d1 AS DATETIME2))),
     (@d1, N'AFTERNOON', '15:30', @may1, @lineL,  8, @qaqc, DATEADD(HOUR, 8, CAST(DATEADD(DAY, 1, @d1) AS DATETIME2)), DATEADD(HOUR, 15, CAST(@d1 AS DATETIME2))),
     (@d2, N'MORNING',   '10:30', @may1, @lineM, 15, @qaqc, DATEADD(HOUR, 8, CAST(DATEADD(DAY, 1, @d2) AS DATETIME2)), DATEADD(HOUR, 10, CAST(@d2 AS DATETIME2))),
     (@d2, N'AFTERNOON', '15:30', @may1, @lineL, 10, @qaqc, DATEADD(HOUR, 8, CAST(DATEADD(DAY, 1, @d2) AS DATETIME2)), DATEADD(HOUR, 15, CAST(@d2 AS DATETIME2))),
     (@d3, N'MORNING',   '10:30', @may1, @lineM, 20, @qaqc, DATEADD(HOUR, 8, CAST(DATEADD(DAY, 1, @d3) AS DATETIME2)), DATEADD(HOUR, 10, CAST(@d3 AS DATETIME2))),
     (@d3, N'AFTERNOON', '15:30', @may1, @lineL, 15, @qaqc, DATEADD(HOUR, 8, CAST(DATEADD(DAY, 1, @d3) AS DATETIME2)), DATEADD(HOUR, 15, CAST(@d3 AS DATETIME2)));

    PRINT N'Đã thêm QC mẫu (80 bộ) cho vài ngày đầu kỳ KPI.';
END
ELSE
    PRINT N'Đã có QC trong kỳ KPI — bỏ qua.';

UPDATE dbo.Account
SET IsTemporaryPassword = 0
WHERE Username IN (N'TTMAY', N'TPMAY') AND IsTemporaryPassword = 1;

PRINT N'Đăng nhập test: TTMAY / 123456 → Trang chủ → KPI của tổ → bấm DH-2511-006.';
GO
