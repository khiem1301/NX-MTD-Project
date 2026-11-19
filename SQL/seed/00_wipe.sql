/* =====================================================================================
   00. XÓA SẠCH DỮ LIỆU (giữ cấu trúc và danh mục cấu hình)
   Giữ nguyên: Role, GradeLevelCode, GradeLevel, SystemSetting, AccountingPeriod (chỉ mở khóa lại).
   Xóa hết: mọi bảng còn lại. Lưu lại hash mật khẩu theo Username để tài khoản cũ giữ mật khẩu đang dùng.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

-- Hash mật khẩu cũ theo Username (tài khoản trùng tên đăng nhập giữ mật khẩu cũ).
IF OBJECT_ID('tempdb..#OldHash') IS NOT NULL DROP TABLE #OldHash;
SELECT Username, PasswordHash, IsTemporaryPassword INTO #OldHash FROM dbo.Account WHERE PasswordHash <> N'__SET_BY_BACKEND_SEED__';
-- Mật khẩu mặc định (123456): lấy từ tài khoản chưa đổi mật khẩu; nếu không có thì để placeholder cho backend ghi khi khởi động.
IF OBJECT_ID('tempdb..#DefaultHash') IS NOT NULL DROP TABLE #DefaultHash;
SELECT TOP 1 PasswordHash INTO #DefaultHash FROM dbo.Account WHERE IsTemporaryPassword = 1 AND PasswordHash <> N'__SET_BY_BACKEND_SEED__' ORDER BY AccountId;
IF NOT EXISTS (SELECT 1 FROM #DefaultHash) INSERT #DefaultHash VALUES (N'__SET_BY_BACKEND_SEED__');

EXEC sys.sp_set_session_context @key = N'NXMTD_StockPost', @value = 1;   -- cho phép xóa thẻ kho khi dọn

DECLARE @sql NVARCHAR(MAX);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
    SELECT N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' NOCHECK CONSTRAINT ALL; '
         + N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' DISABLE TRIGGER ALL;'
    FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id;
OPEN c; FETCH NEXT FROM c INTO @sql;
WHILE @@FETCH_STATUS = 0 BEGIN EXEC (@sql); FETCH NEXT FROM c INTO @sql; END;
CLOSE c; DEALLOCATE c;

DECLARE @del NVARCHAR(MAX);
DECLARE d CURSOR LOCAL FAST_FORWARD FOR
    SELECT N'DELETE FROM ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
         + CASE WHEN EXISTS (SELECT 1 FROM sys.identity_columns i WHERE i.object_id = t.object_id)
                THEN N' DBCC CHECKIDENT (''' + s.name + N'.' + t.name + N''', RESEED, 0) WITH NO_INFOMSGS;' ELSE N'' END
    FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE t.name NOT IN ('Role', 'GradeLevelCode', 'GradeLevel', 'SystemSetting', 'AccountingPeriod');
OPEN d; FETCH NEXT FROM d INTO @del;
WHILE @@FETCH_STATUS = 0 BEGIN EXEC (@del); FETCH NEXT FROM d INTO @del; END;
CLOSE d; DEALLOCATE d;

UPDATE dbo.AccountingPeriod SET Status = 'OPEN', LockedAt = NULL;
EXEC sys.sp_set_session_context @key = N'NXMTD_StockPost', @value = NULL;

-- Bật lại ràng buộc + trigger: từ đây mọi dòng seed đều bị DB kiểm tra như dữ liệu thật.
DECLARE e CURSOR LOCAL FAST_FORWARD FOR
    SELECT N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' WITH CHECK CHECK CONSTRAINT ALL; '
         + N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' ENABLE TRIGGER ALL;'
    FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id;
OPEN e; FETCH NEXT FROM e INTO @sql;
WHILE @@FETCH_STATUS = 0 BEGIN EXEC (@sql); FETCH NEXT FROM e INTO @sql; END;
CLOSE e; DEALLOCATE e;

PRINT N'00: đã xóa sạch dữ liệu.';
