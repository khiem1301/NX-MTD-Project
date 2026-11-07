/* =====================================================================================
   DEV-ALL-82: thêm ngày dự kiến giao hàng cho đơn mua (PurchaseOrder) và NVL khách cấp (OrderMaterial), giữ số lần giao dự kiến.
     OrderMaterial.ExpectedDeliveryDate  : chỉ có khi khách cấp (CustomerQty > 0).
     PurchaseOrder.ExpectedDeliveryDate  : không trước ngày đặt hàng.
   Dữ liệu cũ để NULL (app chỉ bắt buộc khi tạo / sửa ngày). DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy.
   Chạy lại không lỗi.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-07_expected_delivery_date.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

IF COL_LENGTH('dbo.OrderMaterial', 'ExpectedDeliveryDate') IS NULL
    ALTER TABLE dbo.OrderMaterial ADD ExpectedDeliveryDate DATE NULL;
GO
IF COL_LENGTH('dbo.PurchaseOrder', 'ExpectedDeliveryDate') IS NULL
    ALTER TABLE dbo.PurchaseOrder ADD ExpectedDeliveryDate DATE NULL;
GO
IF OBJECT_ID('dbo.CK_OM_DeliveryDate', 'C') IS NULL
    ALTER TABLE dbo.OrderMaterial ADD CONSTRAINT CK_OM_DeliveryDate
        CHECK (ExpectedDeliveryDate IS NULL OR CustomerQty > 0);
GO
IF OBJECT_ID('dbo.CK_PO_DeliveryDate', 'C') IS NULL
    ALTER TABLE dbo.PurchaseOrder ADD CONSTRAINT CK_PO_DeliveryDate
        CHECK (ExpectedDeliveryDate IS NULL OR OrderDate IS NULL OR ExpectedDeliveryDate >= OrderDate);
GO
PRINT N'Đã thêm ExpectedDeliveryDate cho OrderMaterial và PurchaseOrder.';
GO
