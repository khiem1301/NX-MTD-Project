/* =====================================================================================
   DEV-ALL-133: hai khách hàng (hoặc hai nhà cung cấp) không được cùng số điện thoại, không được cùng email.
   Thêm 4 unique index có điều kiện (bỏ trống thì không kiểm): UQ_Customer_Phone, UQ_Customer_Email, UQ_Supplier_Phone, UQ_Supplier_Email.
   Chỉ kiểm trong từng loại: một khách hàng và một nhà cung cấp vẫn được trùng số.
   Nếu DB đang có dữ liệu trùng thì script dừng và liệt kê giá trị trùng, cần sửa tay rồi chạy lại.
   Chạy lại không lỗi. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-17_customer_supplier_unique_contact.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF EXISTS (SELECT 1 FROM dbo.Customer WHERE Phone IS NOT NULL GROUP BY Phone HAVING COUNT(*) > 1)
   OR EXISTS (SELECT 1 FROM dbo.Customer WHERE Email IS NOT NULL GROUP BY Email HAVING COUNT(*) > 1)
   OR EXISTS (SELECT 1 FROM dbo.Supplier WHERE Phone IS NOT NULL GROUP BY Phone HAVING COUNT(*) > 1)
   OR EXISTS (SELECT 1 FROM dbo.Supplier WHERE Email IS NOT NULL GROUP BY Email HAVING COUNT(*) > 1)
BEGIN
    SELECT N'Customer' AS [Table], N'Phone' AS [Column], Phone AS Value, COUNT(*) AS Total FROM dbo.Customer WHERE Phone IS NOT NULL GROUP BY Phone HAVING COUNT(*) > 1
    UNION ALL SELECT N'Customer', N'Email', Email, COUNT(*) FROM dbo.Customer WHERE Email IS NOT NULL GROUP BY Email HAVING COUNT(*) > 1
    UNION ALL SELECT N'Supplier', N'Phone', Phone, COUNT(*) FROM dbo.Supplier WHERE Phone IS NOT NULL GROUP BY Phone HAVING COUNT(*) > 1
    UNION ALL SELECT N'Supplier', N'Email', Email, COUNT(*) FROM dbo.Supplier WHERE Email IS NOT NULL GROUP BY Email HAVING COUNT(*) > 1;
    THROW 50090, N'Có số điện thoại / email bị trùng giữa các khách hàng hoặc các nhà cung cấp (xem danh sách trên). Sửa dữ liệu rồi chạy lại.', 1;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Customer_Phone' AND object_id = OBJECT_ID(N'dbo.Customer'))
    CREATE UNIQUE INDEX UQ_Customer_Phone ON dbo.Customer(Phone) WHERE Phone IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Customer_Email' AND object_id = OBJECT_ID(N'dbo.Customer'))
    CREATE UNIQUE INDEX UQ_Customer_Email ON dbo.Customer(Email) WHERE Email IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Supplier_Phone' AND object_id = OBJECT_ID(N'dbo.Supplier'))
    CREATE UNIQUE INDEX UQ_Supplier_Phone ON dbo.Supplier(Phone) WHERE Phone IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Supplier_Email' AND object_id = OBJECT_ID(N'dbo.Supplier'))
    CREATE UNIQUE INDEX UQ_Supplier_Email ON dbo.Supplier(Email) WHERE Email IS NOT NULL;
GO

PRINT N'Đã thêm unique index số điện thoại / email cho Customer và Supplier.';
GO
