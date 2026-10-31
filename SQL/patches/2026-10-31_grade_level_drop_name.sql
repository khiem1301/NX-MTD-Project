/* =====================================================================================
   Patch 31/10/2026 - Bỏ cột trùng GradeLevel.GradeName và cột thừa GradeLevelCode.SortOrder
   Tên bậc chỉ lưu ở dbo.GradeLevelCode; GradeLevel chỉ giữ tiền và ngày hiệu lực. Bậc xếp theo mã, không có thứ tự riêng.
   Chạy SAU 2026-10-28_grade_level_code.sql. Chạy lại không lỗi (idempotent).
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF COL_LENGTH('dbo.GradeLevel', 'GradeName') IS NULL AND COL_LENGTH('dbo.GradeLevelCode', 'SortOrder') IS NULL
BEGIN
    PRINT N'Patch đã áp dụng, bỏ qua.';
    RETURN;
END;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.GradeLevel', 'GradeName') IS NOT NULL
BEGIN
    -- Mã bậc chưa có tên trong danh mục (không xảy ra sau patch 28/10, giữ để an toàn): lấy tên từ phiên bản tiền bậc mới nhất
    EXEC(N'UPDATE c SET c.GradeName = x.GradeName
           FROM dbo.GradeLevelCode c
           CROSS APPLY (SELECT TOP (1) g.GradeName FROM dbo.GradeLevel g WHERE g.GradeCode = c.GradeCode ORDER BY g.EffectiveDate DESC) x
           WHERE LTRIM(RTRIM(c.GradeName)) = N''''');
    EXEC(N'ALTER TABLE dbo.GradeLevel DROP COLUMN GradeName');
END;

IF COL_LENGTH('dbo.GradeLevelCode', 'SortOrder') IS NOT NULL
BEGIN
    IF OBJECT_ID('dbo.DF_GLC_Sort', 'D') IS NOT NULL ALTER TABLE dbo.GradeLevelCode DROP CONSTRAINT DF_GLC_Sort;
    ALTER TABLE dbo.GradeLevelCode DROP COLUMN SortOrder;
END;

COMMIT TRANSACTION;
PRINT N'Đã bỏ GradeLevel.GradeName và GradeLevelCode.SortOrder.';
