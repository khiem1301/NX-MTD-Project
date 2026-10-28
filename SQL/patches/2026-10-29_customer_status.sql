/* =====================================================================================
   Patch 29/10/2026 - Customer thêm Status (ACTIVE / DISCONTINUED), như Supplier
   - Customer.Status mặc định ACTIVE, CK_Customer_Status
   Chạy lại không lỗi (idempotent). Áp lên DB đã dựng từ script cũ; DB dựng mới từ NX-MTD_Database.sql đã có sẵn.
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF COL_LENGTH('dbo.Customer', 'Status') IS NULL
BEGIN
    ALTER TABLE dbo.Customer ADD Status VARCHAR(20) NOT NULL CONSTRAINT DF_Customer_Status DEFAULT 'ACTIVE';
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Customer_Status')
BEGIN
    ALTER TABLE dbo.Customer ADD CONSTRAINT CK_Customer_Status CHECK (Status IN ('ACTIVE','DISCONTINUED'));
END;
GO

PRINT N'Đã áp dụng patch trạng thái khách hàng.';
