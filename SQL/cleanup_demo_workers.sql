/* =====================================================================================
   Dọn demo công nhân: nghỉ việc + gỡ mã chấm công + khóa tài khoản.
   Giữ role văn phòng (ADMIN, QUANDOC, KETOAN, THUKHO, QAQC1, CODIEN) và tổ trưởng/phó mẫu.
   Không xóa cứng (FK demo sản xuất vẫn tham chiếu EmployeeId).
   ===================================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRANSACTION;

DECLARE @today DATE = CAST(GETDATE() AS DATE);
DECLARE @reason NVARCHAR(200) = N'Dọn demo công nhân — chỉ giữ role văn phòng; công nhân lấy từ RJ.';

;WITH DemoWorkers AS (
    SELECT e.EmployeeId
    FROM dbo.Employee e
    JOIN dbo.Role r ON r.RoleId = e.RoleId
    WHERE r.RoleCode = 'WORKER'
      AND e.Status = 'ACTIVE'
      AND (
            e.EmployeeCode LIKE 'CN%'
         OR e.EmployeeCode LIKE 'DEMO%'
         OR e.FullName LIKE N'%demo%'
         OR e.FullName LIKE N'%mẫu%'
         OR e.FullName IN (N'Nguyễn Thị Lan', N'Trần Văn Hùng', N'Lê Thị Mai',
                           N'Công nhân may một', N'Công nhân cắt một')
      )
)
UPDATE e
SET e.Status = 'RESIGNED',
    e.ResignDate = @today,
    e.AttendanceCode = NULL,
    e.UpdatedAt = dbo.fn_VnNow()
FROM dbo.Employee e
JOIN DemoWorkers d ON d.EmployeeId = e.EmployeeId;

UPDATE a
SET a.Status = 'LOCKED',
    a.LockedAt = dbo.fn_VnNow(),
    a.LockReason = @reason,
    a.SecurityStamp = NEWID()
FROM dbo.Account a
JOIN dbo.Employee e ON e.EmployeeId = a.EmployeeId
JOIN dbo.Role r ON r.RoleId = e.RoleId
WHERE r.RoleCode = 'WORKER'
  AND e.Status = 'RESIGNED'
  AND a.Status <> 'LOCKED';

-- Gỡ mã chấm công còn sót trên mọi công nhân đã nghỉ (để import lại từ RJ).
UPDATE e
SET e.AttendanceCode = NULL,
    e.UpdatedAt = dbo.fn_VnNow()
FROM dbo.Employee e
JOIN dbo.Role r ON r.RoleId = e.RoleId
WHERE r.RoleCode = 'WORKER'
  AND e.Status = 'RESIGNED'
  AND e.AttendanceCode IS NOT NULL;

SELECT e.EmployeeCode, e.FullName, e.Status, e.AttendanceCode, a.Status AS AccountStatus
FROM dbo.Employee e
JOIN dbo.Role r ON r.RoleId = e.RoleId
LEFT JOIN dbo.Account a ON a.EmployeeId = e.EmployeeId
WHERE r.RoleCode = 'WORKER'
ORDER BY e.EmployeeCode;

COMMIT TRANSACTION;
PRINT N'Đã nghỉ việc / khóa tài khoản demo công nhân và gỡ AttendanceCode.';
