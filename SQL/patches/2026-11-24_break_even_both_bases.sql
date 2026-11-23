/* =====================================================================================
   DEV-ALL-169: mỗi lần tính hòa vốn lưu cả hai cách chia định phí (OUTPUT và LABOUR_HOURS) cùng CalculatedAt.
   - UQ_BER đổi từ (PeriodId, CalculatedAt, ProductId) thành (PeriodId, CalculatedAt, ProductId, AllocationBasis):
     một mã hàng có 2 dòng trong cùng một lần tính, khác cách chia.
   - Dữ liệu cũ (mỗi lần tính một cách) vẫn hợp lệ với khóa mới, không phải sửa.
   Chạy lại không lỗi. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-24_break_even_both_bases.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF EXISTS (
    SELECT 1
    FROM sys.indexes i
    JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE i.object_id = OBJECT_ID(N'dbo.BreakEvenResult') AND i.name = N'UQ_BER' AND c.name = N'AllocationBasis')
    PRINT N'UQ_BER đã gồm AllocationBasis.';
ELSE
BEGIN
    BEGIN TRANSACTION;
    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.BreakEvenResult') AND name = N'UQ_BER')
        ALTER TABLE dbo.BreakEvenResult DROP CONSTRAINT UQ_BER;
    ALTER TABLE dbo.BreakEvenResult
        ADD CONSTRAINT UQ_BER UNIQUE (PeriodId, CalculatedAt, ProductId, AllocationBasis);
    COMMIT;
    PRINT N'Đã đổi UQ_BER thành (PeriodId, CalculatedAt, ProductId, AllocationBasis).';
END
GO
