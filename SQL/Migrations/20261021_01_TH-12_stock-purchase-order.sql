-- TH-12 · Thai · 21/10/2026 · DEV-TH-12-1
-- Đơn mua NVL dùng chung nhiều đơn (kim, chỉ...): Quản đốc tạo mà không cần mã đơn hàng do Admin nhập.
--   PurchaseOrder.OrderId NULL                       = đơn mua dùng chung, không gắn đơn hàng
--   PurchaseOrderLine / MaterialReceiptLine:
--     OrderMaterialId NULL -> dòng xác định NVL bằng MaterialId + MaterialColor
--   MaterialReceipt.OrderId NULL                     = phiếu nhập của đơn mua dùng chung
-- Chạy lại nhiều lần không lỗi (idempotent). Không cần sqlcmd -I vì script tự SET QUOTED_IDENTIFIER ON.
-- Không có thay đổi dữ liệu seed.

SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- 1. PurchaseOrder.OrderId cho phép NULL
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PurchaseOrder') AND name = 'OrderId' AND is_nullable = 0)
    ALTER TABLE dbo.PurchaseOrder ALTER COLUMN OrderId INT NULL;

-- 2. MaterialReceipt.OrderId cho phép NULL
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MaterialReceipt') AND name = 'OrderId' AND is_nullable = 0)
    ALTER TABLE dbo.MaterialReceipt ALTER COLUMN OrderId INT NULL;

-- 3. PurchaseOrderLine và MaterialReceiptLine: OrderMaterialId NULL + MaterialId / MaterialColor
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.PurchaseOrderLine') AND name = 'OrderMaterialId' AND is_nullable = 0)
    ALTER TABLE dbo.PurchaseOrderLine ALTER COLUMN OrderMaterialId INT NULL;
IF COL_LENGTH('dbo.PurchaseOrderLine', 'MaterialId') IS NULL
    ALTER TABLE dbo.PurchaseOrderLine ADD MaterialId INT NULL;
IF COL_LENGTH('dbo.PurchaseOrderLine', 'MaterialColor') IS NULL
    ALTER TABLE dbo.PurchaseOrderLine ADD MaterialColor NVARCHAR(50) NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.MaterialReceiptLine') AND name = 'OrderMaterialId' AND is_nullable = 0)
    ALTER TABLE dbo.MaterialReceiptLine ALTER COLUMN OrderMaterialId INT NULL;
IF COL_LENGTH('dbo.MaterialReceiptLine', 'MaterialId') IS NULL
    ALTER TABLE dbo.MaterialReceiptLine ADD MaterialId INT NULL;
IF COL_LENGTH('dbo.MaterialReceiptLine', 'MaterialColor') IS NULL
    ALTER TABLE dbo.MaterialReceiptLine ADD MaterialColor NVARCHAR(50) NULL;

-- Các lệnh dưới dùng cột vừa thêm nên chạy bằng EXEC (compile ở batch riêng).
-- Dòng phải xác định được NVL: theo NVL của đơn, hoặc trực tiếp theo MaterialId + MaterialColor.
IF OBJECT_ID('dbo.FK_POL_Material') IS NULL
    EXEC('ALTER TABLE dbo.PurchaseOrderLine ADD CONSTRAINT FK_POL_Material FOREIGN KEY (MaterialId) REFERENCES dbo.Material (MaterialId)');
IF OBJECT_ID('dbo.CK_POL_Target') IS NULL
    EXEC('ALTER TABLE dbo.PurchaseOrderLine ADD CONSTRAINT CK_POL_Target CHECK (
        (OrderMaterialId IS NOT NULL AND MaterialId IS NULL)
     OR (OrderMaterialId IS NULL AND MaterialId IS NOT NULL AND MaterialColor IS NOT NULL))');
IF OBJECT_ID('dbo.FK_MRL_Material') IS NULL
    EXEC('ALTER TABLE dbo.MaterialReceiptLine ADD CONSTRAINT FK_MRL_Material FOREIGN KEY (MaterialId) REFERENCES dbo.Material (MaterialId)');
IF OBJECT_ID('dbo.CK_MRL_Target') IS NULL
    EXEC('ALTER TABLE dbo.MaterialReceiptLine ADD CONSTRAINT CK_MRL_Target CHECK (
        (OrderMaterialId IS NOT NULL AND MaterialId IS NULL)
     OR (OrderMaterialId IS NULL AND MaterialId IS NOT NULL AND MaterialColor IS NOT NULL))');

-- 4. Unique: UQ_POL / UQ_MRL từ unique constraint thành filtered index (nhiều dòng NULL vẫn hợp lệ)
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_POL' AND parent_object_id = OBJECT_ID('dbo.PurchaseOrderLine'))
    ALTER TABLE dbo.PurchaseOrderLine DROP CONSTRAINT UQ_POL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_POL' AND object_id = OBJECT_ID('dbo.PurchaseOrderLine'))
    CREATE UNIQUE INDEX UQ_POL ON dbo.PurchaseOrderLine (PurchaseOrderId, OrderMaterialId) WHERE OrderMaterialId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_POL_Stock' AND object_id = OBJECT_ID('dbo.PurchaseOrderLine'))
    CREATE UNIQUE INDEX UQ_POL_Stock ON dbo.PurchaseOrderLine (PurchaseOrderId, MaterialId, MaterialColor) WHERE OrderMaterialId IS NULL;

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_MRL' AND parent_object_id = OBJECT_ID('dbo.MaterialReceiptLine'))
    ALTER TABLE dbo.MaterialReceiptLine DROP CONSTRAINT UQ_MRL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_MRL' AND object_id = OBJECT_ID('dbo.MaterialReceiptLine'))
    CREATE UNIQUE INDEX UQ_MRL ON dbo.MaterialReceiptLine (MaterialReceiptId, OrderMaterialId) WHERE OrderMaterialId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_MRL_Stock' AND object_id = OBJECT_ID('dbo.MaterialReceiptLine'))
    CREATE UNIQUE INDEX UQ_MRL_Stock ON dbo.MaterialReceiptLine (MaterialReceiptId, MaterialId, MaterialColor) WHERE OrderMaterialId IS NULL;

-- vw_PurchaseOrderLineStatus join theo PurchaseOrderLineId nên không cần sửa.

COMMIT TRANSACTION;
