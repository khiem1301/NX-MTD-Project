/* =====================================================================================
   09. CHẤM CÔNG VÀ SUẤT ĂN
   Lượt quẹt vân tay từ 03/08 đến hôm nay (đi muộn, tăng ca, quên quẹt ra, quẹt lặp, quẹt thừa, nghỉ, làm Chủ nhật, mã chưa map),
   các lần đồng bộ RJ (thành công / lỗi / đang chạy) và import Excel, bảng công ngày dựng qua usp_Attendance_RebuildTimesheet.
   Suất ăn theo ngày (OPEN / CLOSED / AUTO_CLOSED / KITCHEN_NOTIFIED), suất ăn của từng tổ, đăng ký ăn theo nhân viên, suất khách.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

DECLARE @today DATE = CAST(dbo.fn_VnNow() AS DATE), @now DATETIME2(0) = dbo.fn_VnNow();
DECLARE @from DATE = '2026-08-03';

/* ---------- Danh mục nhân viên trên máy chấm công (RJ) ---------- */
INSERT dbo.RjEmployeeSnapshot (AttendanceCode, FullName, RjEmployeeCode, SyncedAt)
SELECT e.AttendanceCode, e.FullName, CONCAT('RJ', RIGHT(CONCAT('0000', e.AttendanceCode), 4)), DATEADD(MINUTE, -7, @now)
FROM dbo.Employee e WHERE e.AttendanceCode IS NOT NULL;
-- Ba mã máy chấm công chưa gắn nhân viên: Kế toán map ở màn Chấm công
INSERT dbo.RjEmployeeSnapshot (AttendanceCode, FullName, RjEmployeeCode, SyncedAt) VALUES
 (201, N'Phan Thị Trang', 'RJ0201', DATEADD(MINUTE, -7, @now)),
 (202, N'Nguyễn Văn Bảo', 'RJ0202', DATEADD(MINUTE, -7, @now)),
 (203, N'Lý Thị Kim Chi', 'RJ0203', DATEADD(MINUTE, -7, @now));

/* ---------- Các lần đồng bộ ---------- */
IF OBJECT_ID('tempdb..#Ses') IS NOT NULL DROP TABLE #Ses;
CREATE TABLE #Ses (WeekStart DATE PRIMARY KEY, SyncSessionId INT NULL);
INSERT #Ses (WeekStart)
SELECT DISTINCT DATEADD(DAY, -((DATEDIFF(DAY, '2000-01-03', d.D) % 7)), d.D) FROM (SELECT DATEADD(DAY, n.i, @from) AS D FROM (SELECT TOP 120 ROW_NUMBER() OVER (ORDER BY (SELECT 1)) - 1 AS i FROM sys.all_objects) n) d
WHERE d.D <= @today;
INSERT dbo.AttendanceSyncSession (Source, StartedAt, FinishedAt, Status)
SELECT 'RJ_SYNC', DATEADD(HOUR, 18, CAST(DATEADD(DAY, 5, w.WeekStart) AS DATETIME2)), DATEADD(SECOND, 40, DATEADD(HOUR, 18, CAST(DATEADD(DAY, 5, w.WeekStart) AS DATETIME2))), 'SUCCEEDED'
FROM #Ses w ORDER BY w.WeekStart;
-- gán id theo thứ tự tạo
;WITH a AS (SELECT w.WeekStart, ROW_NUMBER() OVER (ORDER BY w.WeekStart) AS rn FROM #Ses w),
      b AS (SELECT SyncSessionId, ROW_NUMBER() OVER (ORDER BY SyncSessionId) AS rn FROM dbo.AttendanceSyncSession)
UPDATE s SET SyncSessionId = b.SyncSessionId FROM #Ses s JOIN a ON a.WeekStart = s.WeekStart JOIN b ON b.rn = a.rn;
INSERT dbo.AttendanceSyncSession (Source, StartedAt, FinishedAt, Status, ErrorMessage, FileName) VALUES
 ('RJ_SYNC',      DATEADD(DAY, -3, @now), DATEADD(SECOND, 25, DATEADD(DAY, -3, @now)), 'FAILED', N'Không mở được tệp RJData.mdb: tệp đang bị khóa bởi Ronald Jack Pro', NULL),
 ('RJ_SYNC',      DATEADD(MINUTE, -2, @now), NULL, 'RUNNING', NULL, NULL),
 ('EXCEL_IMPORT', DATEADD(DAY, -40, @now), DATEADD(SECOND, 12, DATEADD(DAY, -40, @now)), 'SUCCEEDED', NULL, N'chamcong_thang_09_bosung.xlsx');

/* ---------- Lượt quẹt vân tay ---------- */
IF OBJECT_ID('tempdb..#Day') IS NOT NULL DROP TABLE #Day;
SELECT d.D, ROW_NUMBER() OVER (ORDER BY d.D) AS di
INTO #Day FROM (SELECT DATEADD(DAY, n.i, @from) AS D FROM (SELECT TOP 130 ROW_NUMBER() OVER (ORDER BY (SELECT 1)) - 1 AS i FROM sys.all_objects) n) d WHERE d.D <= @today;

IF OBJECT_ID('tempdb..#Ed') IS NOT NULL DROP TABLE #Ed;
SELECT e.EmployeeId, e.EmployeeCode, e.AttendanceCode, d.D,
       ABS(CHECKSUM(e.EmployeeId, d.D)) AS h,
       DATEDIFF(DAY, '2000-01-02', d.D) % 7 AS Dow     -- 0 = Chủ nhật
INTO #Ed
FROM dbo.Employee e
JOIN #Day d ON d.D >= e.StartDate AND (e.ResignDate IS NULL OR d.D <= e.ResignDate)
WHERE e.AttendanceCode IS NOT NULL;
-- Nghỉ ~4% số ngày (thứ 2-7); Chủ nhật chỉ ~12% người đi làm; tổ trưởng may 2 vắng hôm nay (để tổ phó thay)
DELETE FROM #Ed WHERE Dow <> 0 AND h % 100 < 4;
DELETE FROM #Ed WHERE Dow = 0 AND h % 100 >= 12;
DELETE FROM #Ed WHERE EmployeeCode = 'TTMAY2' AND D = @today;

IF OBJECT_ID('tempdb..#Punch') IS NOT NULL DROP TABLE #Punch;
SELECT e.AttendanceCode, NULLIF(e.EmployeeId, 0) AS EmployeeId, x.PunchTime
INTO #Punch
FROM #Ed e
CROSS APPLY (SELECT 420 + IIF(e.h % 100 < 7, 40 + e.h % 30, e.h % 32) AS inM) a
CROSS APPLY (VALUES
   -- vào làm: 07:00-07:31, muộn 7% (07:40-08:10)
   (CAST(DATEADD(MINUTE, a.inM, CAST(e.D AS DATETIME2)) AS DATETIME2(0)), 1),
   -- quẹt lặp trong 2 phút (máy đọc 2 lần) ~3%
   (CAST(DATEADD(MINUTE, a.inM + 2, CAST(e.D AS DATETIME2)) AS DATETIME2(0)), IIF(e.h % 33 = 0, 1, 0)),
   -- quẹt thừa giờ nghỉ trưa ~3%
   (CAST(DATEADD(MINUTE, 735 + e.h % 20, CAST(e.D AS DATETIME2)) AS DATETIME2(0)), IIF(e.h % 31 = 0, 1, 0)),
   -- tan làm: 16:30-17:45; quên quẹt ra ~2%
   (CAST(DATEADD(MINUTE, 990 + e.h % 76, CAST(e.D AS DATETIME2)) AS DATETIME2(0)), IIF(e.h % 47 = 0, 0, 1))) x (PunchTime, Keep)
WHERE x.Keep = 1 AND x.PunchTime <= @now;
-- Mã chưa map (201-203): đi làm từ 9 ngày trước
INSERT #Punch (AttendanceCode, EmployeeId, PunchTime)
SELECT c.Code, NULL, p.t
FROM (VALUES (201), (202), (203)) c (Code)
JOIN #Day d ON d.D >= DATEADD(DAY, -9, @today) AND DATEDIFF(DAY, '2000-01-02', d.D) % 7 <> 0
CROSS APPLY (VALUES (DATEADD(MINUTE, 425 + (c.Code % 7), CAST(d.D AS DATETIME2(0)))), (DATEADD(MINUTE, 1000 + (c.Code % 9), CAST(d.D AS DATETIME2(0))))) p (t)
WHERE p.t <= @now;

INSERT dbo.AttendanceLog (AttendanceCode, PunchTime, SourceRecordId, EmployeeId, SyncSessionId)
SELECT p.AttendanceCode, p.PunchTime, 100000 + ROW_NUMBER() OVER (ORDER BY p.PunchTime, p.AttendanceCode), p.EmployeeId,
       s.SyncSessionId
FROM #Punch p
JOIN #Ses s ON s.WeekStart = DATEADD(DAY, -((DATEDIFF(DAY, '2000-01-03', CAST(p.PunchTime AS DATE)) % 7)), CAST(p.PunchTime AS DATE));
UPDATE ss SET RecordsReceived = x.Cnt, RecordsInserted = x.Cnt, LastSourceRecordId = x.MaxId
FROM dbo.AttendanceSyncSession ss
JOIN (SELECT SyncSessionId, COUNT(*) AS Cnt, MAX(SourceRecordId) AS MaxId FROM dbo.AttendanceLog GROUP BY SyncSessionId) x ON x.SyncSessionId = ss.SyncSessionId;

-- Dựng bảng công ngày
EXEC dbo.usp_Attendance_RebuildTimesheet @FromDate = @from, @ToDate = @today;

/* ---------- Suất ăn ---------- */
IF OBJECT_ID('tempdb..#Meal') IS NOT NULL DROP TABLE #Meal;
SELECT d.D, ROW_NUMBER() OVER (ORDER BY d.D) AS rn INTO #Meal
FROM (SELECT DATEADD(DAY, n.i - 60, @today) AS D FROM (SELECT TOP 62 ROW_NUMBER() OVER (ORDER BY (SELECT 1)) - 1 AS i FROM sys.all_objects) n) d
WHERE DATEDIFF(DAY, '2000-01-02', d.D) % 7 <> 0;      -- bỏ Chủ nhật

-- Tạo ở trạng thái OPEN rồi chốt sau (trigger chặn sửa suất ăn đã chốt)
INSERT dbo.DailyMeal (MealDate, Status, OfficeCount, GuestCount, GuestNote, MealPrice)
SELECT m.D, 'OPEN',
       (SELECT COUNT(*) FROM dbo.DailyTimesheet t JOIN dbo.Employee e ON e.EmployeeId = t.EmployeeId JOIN dbo.Team tm ON tm.TeamId = e.TeamId AND tm.TeamType = 'OFFICE'
        WHERE t.WorkDate = m.D),
       CASE WHEN ABS(CHECKSUM(m.D)) % 4 = 0 THEN 2 + ABS(CHECKSUM(m.D, 7)) % 6 ELSE 0 END,
       CASE WHEN ABS(CHECKSUM(m.D)) % 4 = 0 THEN N'Khách đến kiểm hàng / làm việc' END,
       IIF(m.D < DATEADD(DAY, -30, @today), 28000, 30000)
FROM #Meal m;

-- Suất ăn của từng tổ: tổ trưởng xác nhận (CONFIRMED) hoặc hệ thống lấy theo chấm công (ATTENDANCE)
INSERT dbo.TeamMealConfirmation (DailyMealId, TeamId, MealCount, AttendanceCount, Source, ConfirmedAt, RemindedAt)
SELECT dm.DailyMealId, tm.TeamId, a.Cnt + IIF(c.Confirmed = 1, (ABS(CHECKSUM(dm.MealDate, tm.TeamId)) % 3) - 1, 0), a.Cnt,
       IIF(c.Confirmed = 1, 'CONFIRMED', 'ATTENDANCE'),
       IIF(c.Confirmed = 1, DATEADD(MINUTE, 485 + ABS(CHECKSUM(dm.MealDate, tm.TeamId)) % 25, CAST(dm.MealDate AS DATETIME2)), NULL),
       IIF(c.Confirmed = 0, DATEADD(MINUTE, 495, CAST(dm.MealDate AS DATETIME2)), NULL)
FROM dbo.DailyMeal dm
CROSS JOIN dbo.Team tm
CROSS APPLY (SELECT COUNT(*) AS Cnt FROM dbo.DailyTimesheet t JOIN dbo.Employee e ON e.EmployeeId = t.EmployeeId
             WHERE t.WorkDate = dm.MealDate AND e.TeamId = tm.TeamId) a
CROSS APPLY (SELECT IIF(ABS(CHECKSUM(dm.MealDate, tm.TeamId)) % 9 = 0, 0, 1) AS Confirmed) c
WHERE tm.TeamType IN ('CUTTING', 'SEWING') AND tm.Status = 'ACTIVE' AND a.Cnt > 0 AND dm.MealDate <= @today;

-- Đăng ký ăn theo nhân viên (14 ngày gần nhất): đã đi làm thì ăn, một số tự bỏ suất
INSERT dbo.MealRegistration (DailyMealId, EmployeeId, TeamId, WillEat, UpdatedByEmployeeId, UpdatedAt)
SELECT dm.DailyMealId, e.EmployeeId, e.TeamId, IIF(ABS(CHECKSUM(e.EmployeeId, dm.MealDate)) % 15 = 0, 0, 1), e.EmployeeId, DATEADD(MINUTE, 440, CAST(dm.MealDate AS DATETIME2))
FROM dbo.DailyMeal dm
JOIN dbo.DailyTimesheet t ON t.WorkDate = dm.MealDate
JOIN dbo.Employee e ON e.EmployeeId = t.EmployeeId
WHERE dm.MealDate >= DATEADD(DAY, -14, @today) AND dm.MealDate <= @today;

-- Ngày mai: suất ăn đang mở đăng ký (OPEN), nhiều người đã tick, tổ chưa xác nhận
INSERT dbo.MealRegistration (DailyMealId, EmployeeId, TeamId, WillEat, UpdatedByEmployeeId, UpdatedAt)
SELECT dm.DailyMealId, e.EmployeeId, e.TeamId, IIF(ABS(CHECKSUM(e.EmployeeId, 3)) % 10 = 0, 0, 1), e.EmployeeId, DATEADD(MINUTE, -30, @now)
FROM dbo.DailyMeal dm JOIN dbo.Employee e ON e.Status = 'ACTIVE' AND e.AttendanceCode IS NOT NULL
WHERE dm.MealDate = DATEADD(DAY, 1, @today) AND ABS(CHECKSUM(e.EmployeeId, 11)) % 4 <> 0;

-- Chốt các ngày đã qua: CLOSED (đủ xác nhận), AUTO_CLOSED (có tổ chưa xác nhận lúc 08:30), KITCHEN_NOTIFIED (đã báo bếp)
UPDATE dm SET
    Status = CASE WHEN dm.MealDate = @today THEN 'KITCHEN_NOTIFIED'
                  WHEN EXISTS (SELECT 1 FROM dbo.TeamMealConfirmation c WHERE c.DailyMealId = dm.DailyMealId AND c.Source = 'ATTENDANCE') THEN 'AUTO_CLOSED'
                  WHEN ABS(CHECKSUM(dm.MealDate)) % 3 = 0 THEN 'KITCHEN_NOTIFIED' ELSE 'CLOSED' END,
    AttendanceIncomplete = IIF(ABS(CHECKSUM(dm.MealDate, 5)) % 14 = 0, 1, 0),
    AutoClosedAt = CASE WHEN EXISTS (SELECT 1 FROM dbo.TeamMealConfirmation c WHERE c.DailyMealId = dm.DailyMealId AND c.Source = 'ATTENDANCE')
                        THEN DATEADD(MINUTE, 510, CAST(dm.MealDate AS DATETIME2)) END,
    ClosedAt = DATEADD(MINUTE, 510, CAST(dm.MealDate AS DATETIME2)),
    KitchenNotifiedAt = DATEADD(MINUTE, 515, CAST(dm.MealDate AS DATETIME2))
FROM dbo.DailyMeal dm WHERE dm.MealDate <= @today;
UPDATE dm SET KitchenNotifiedAt = CASE WHEN dm.Status IN ('KITCHEN_NOTIFIED') THEN KitchenNotifiedAt END
FROM dbo.DailyMeal dm WHERE dm.Status IN ('CLOSED', 'AUTO_CLOSED');

PRINT N'09: chấm công, bảng công, suất ăn xong.';
