/* =====================================================================================
   DEV-ALL-147: thêm địa chỉ giao hàng vào đơn hàng (CustomerOrder.DeliveryAddress, bắt buộc khi tạo / sửa đơn ở BE).
   Đơn cũ được điền sẵn bằng địa chỉ mặc định của khách. Chạy lại không lỗi. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-20_order_delivery_address.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'dbo.CustomerOrder', N'DeliveryAddress') IS NULL
    ALTER TABLE dbo.CustomerOrder ADD DeliveryAddress NVARCHAR(500) NOT NULL
        CONSTRAINT DF_CustomerOrder_DeliveryAddress DEFAULT N'';
GO

UPDATE o SET DeliveryAddress = ISNULL(c.DefaultAddress, N'')
FROM dbo.CustomerOrder o JOIN dbo.Customer c ON c.CustomerId = o.CustomerId
WHERE o.DeliveryAddress = N'';
GO
