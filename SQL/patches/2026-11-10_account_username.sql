/* =====================================================================================
   Account.Username: tên đăng nhập tách khỏi Employee.EmployeeCode (map RJ).
   Seed / tài khoản cũ: Username = EmployeeCode để giữ đăng nhập hiện tại.
   NV mới: backend sinh từ họ tên (vd Phạm Hoàng Thái → ThaiPH; trùng → ThaiPH01).
   DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-10_account_username.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

IF COL_LENGTH(N'dbo.Account', N'Username') IS NULL
BEGIN
    ALTER TABLE dbo.Account ADD Username VARCHAR(50) NULL;
END;
GO

UPDATE a
SET Username = e.EmployeeCode
FROM dbo.Account a
JOIN dbo.Employee e ON e.EmployeeId = a.EmployeeId
WHERE a.Username IS NULL;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Account') AND name = N'Username' AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.Account ALTER COLUMN Username VARCHAR(50) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Account') AND name = N'UQ_Account_Username'
)
BEGIN
    CREATE UNIQUE INDEX UQ_Account_Username ON dbo.Account(Username);
END;
GO

PRINT N'Đã thêm Account.Username (backfill = EmployeeCode).';
GO
