/* =====================================================================================
   Patch 22/11/2026 — Công nguyên ngày: ≥ WORKDAY_STANDARD_HOURS → 1 công, thiếu → 0
   (không còn giờ làm / 8 làm tròn 2 số).

   Chạy trên DB đang có:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL/patches/2026-11-22_workday_full_day.sql

   Sau patch: cập nhật WorkdayValue các dòng bảng công hiện có; proc rebuild / chấm hộ
   dùng công thức mới cho lần tính sau.
   ===================================================================================== */
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

UPDATE dbo.SystemSetting
SET Description = N'Số giờ tối thiểu để ghi 1 công nguyên ngày (TongGioTrongCaLamViec ca Hành Chính)'
WHERE SettingKey = N'WORKDAY_STANDARD_HOURS';
GO

/* Cập nhật số công đã lưu theo ngưỡng giờ chuẩn hiện tại. */
DECLARE @std DECIMAL(5,2) = (
    SELECT CAST(SettingValue AS DECIMAL(5,2))
    FROM dbo.SystemSetting
    WHERE SettingKey = N'WORKDAY_STANDARD_HOURS'
);
IF @std IS NULL SET @std = 8;

UPDATE dbo.DailyTimesheet
SET WorkdayValue = CAST(IIF(WorkedHours >= @std, 1, 0) AS DECIMAL(4,2)),
    UpdatedAt = dbo.fn_VnNow()
WHERE WorkdayValue <> CAST(IIF(WorkedHours >= @std, 1, 0) AS DECIMAL(4,2));
GO

/* ---- Rebuild timesheet: công = 1 nếu đủ giờ chuẩn, không thì 0 ---- */
CREATE OR ALTER PROCEDURE dbo.usp_Attendance_RebuildTimesheet
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
    ), kept AS (
        SELECT raw.*, ROW_NUMBER() OVER (PARTITION BY EmployeeId, WorkDate ORDER BY PunchTime) AS Seq,
               COUNT(*) OVER (PARTITION BY EmployeeId, WorkDate) AS KeptCount
        FROM raw
        WHERE PrevPunch IS NULL OR DATEDIFF(SECOND, PrevPunch, PunchTime) >= @dup * 60
    ), p AS (
        SELECT k1.EmployeeId, k1.WorkDate, k1.KeptCount,
               k1.PunchTime AS CheckIn,
               k2.PunchTime AS CheckOut
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
           CAST(IIF(Worked >= @std, 1, 0) AS DECIMAL(4,2)) AS WorkdayValue,
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

CREATE OR ALTER PROCEDURE dbo.usp_Attendance_ApplyAdjustment
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

    IF dbo.fn_IsPeriodLocked(@WorkDate) = 1
        THROW 50013, N'Tháng đã khóa, không thể chấm hộ.', 1;

    DECLARE @ws  TIME(0)       = (SELECT CAST(SettingValue AS TIME(0)) FROM dbo.SystemSetting WHERE SettingKey = 'WORK_START_TIME'),
            @we  TIME(0)       = (SELECT CAST(SettingValue AS TIME(0)) FROM dbo.SystemSetting WHERE SettingKey = 'WORK_END_TIME'),
            @ls  TIME(0)       = (SELECT CAST(SettingValue AS TIME(0)) FROM dbo.SystemSetting WHERE SettingKey = 'LUNCH_START_TIME'),
            @le  TIME(0)       = (SELECT CAST(SettingValue AS TIME(0)) FROM dbo.SystemSetting WHERE SettingKey = 'LUNCH_END_TIME'),
            @std DECIMAL(5,2)  = (SELECT CAST(SettingValue AS DECIMAL(5,2)) FROM dbo.SystemSetting WHERE SettingKey = 'WORKDAY_STANDARD_HOURS');

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
    SET @workday = CAST(IIF(@worked >= @std, 1, 0) AS DECIMAL(4,2));

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

PRINT N'Patch 2026-11-22_workday_full_day: xong.';
GO
