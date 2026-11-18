/* =====================================================================================
   DEV-ALL-127: DispatchNote.DeliveryMethod — Thủ kho chọn cách giao khi xuất kho:
     CUSTOMER_PICKUP  = khách tự đến lấy hàng (không tạo chuyến xe)
     COMPANY_VEHICLE  = xe công ty giao (tạo luôn chuyến xe STARTED gắn phiếu xuất; Kế toán kết thúc chuyến ở Sổ xe)
   Phiếu đã xuất (IN_DELIVERY / DELIVERED) bắt buộc có cách giao. Dữ liệu cũ: trước đây hàng luôn do xe công ty giao
   → điền COMPANY_VEHICLE.
   Chạy lại được nhiều lần. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-18_dispatch_delivery_method.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

IF COL_LENGTH('dbo.DispatchNote', 'DeliveryMethod') IS NULL
    ALTER TABLE dbo.DispatchNote ADD DeliveryMethod VARCHAR(20) NULL;
GO

UPDATE dbo.DispatchNote
SET DeliveryMethod = 'COMPANY_VEHICLE'
WHERE Status IN ('IN_DELIVERY','DELIVERED') AND DeliveryMethod IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DN_DeliveryMethod')
    ALTER TABLE dbo.DispatchNote ADD CONSTRAINT CK_DN_DeliveryMethod
        CHECK (DeliveryMethod IS NULL OR DeliveryMethod IN ('CUSTOMER_PICKUP','COMPANY_VEHICLE'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DN_DeliveryMethodSet')
    ALTER TABLE dbo.DispatchNote ADD CONSTRAINT CK_DN_DeliveryMethodSet
        CHECK (Status NOT IN ('IN_DELIVERY','DELIVERED') OR DeliveryMethod IS NOT NULL);
GO
