/* =====================================================================================
   UC51 · Claude (AI) · 11/11/2026 · DEV-ALL-93
   Bàn cắt nhiều vải: cắt cả bộ thì nhập số mét riêng từng vải chi tiết của màu áo (đủ mọi vải), cắt bù / cắt lẻ thì vải của chi tiết.
     - Thêm CuttingLayFabric (CuttingLayId, MaterialId, UsedM); chuyển vải + số mét của mỗi bàn đang có sang bảng này.
     - CuttingLay bỏ FabricMaterialId (FK_CL_Material), FabricUsedM (CK_CL_FabricUsed).
     - trg_CuttingLayLine_Check bỏ 50049 (vải chi tiết cắt bù = vải của bàn): bàn có nhiều vải, backend kiểm đủ đúng bộ vải.
   Chạy sau 2026-11-11_cutting_lay_fabric_used.sql. Chạy lại được nhiều lần. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-11_cutting_lay_fabrics.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

CREATE OR ALTER TRIGGER dbo.trg_CuttingLayLine_Check ON dbo.CuttingLayLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.CuttingLay cl ON cl.CuttingLayId = i.CuttingLayId
               JOIN dbo.WorkOrder wo  ON wo.WorkOrderId = cl.WorkOrderId
               JOIN dbo.OrderLine ol  ON ol.OrderLineId = i.OrderLineId
               WHERE ol.OrderId <> wo.OrderId OR ol.ProductId <> wo.ProductId)
        THROW 50042, N'Dòng bàn cắt phải thuộc đúng đơn hàng và mã hàng của lệnh cắt.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.OrderLine ol                 ON ol.OrderLineId = i.OrderLineId
               JOIN dbo.ProductVariant olv           ON olv.ProductVariantId = ol.ProductVariantId
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductColorId <> olv.ProductColorId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết cắt phải là chi tiết vải (PANEL) trong định mức của đúng màu của dòng đơn.', 1;
    -- 50049 (vải chi tiết cắt bù = vải của bàn) bỏ từ DEV-ALL-93: bàn có nhiều vải, backend kiểm đủ đúng bộ vải của các dòng.
END;
GO

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.CuttingLayFabric', N'U') IS NULL
    CREATE TABLE dbo.CuttingLayFabric (
        CuttingLayFabricId INT IDENTITY(1,1) CONSTRAINT PK_CuttingLayFabric PRIMARY KEY,
        CuttingLayId     INT           NOT NULL CONSTRAINT FK_CLF_Lay REFERENCES dbo.CuttingLay(CuttingLayId),
        MaterialId       INT           NOT NULL CONSTRAINT FK_CLF_Material REFERENCES dbo.Material(MaterialId),
        UsedM            DECIMAL(9,2)  NULL CONSTRAINT CK_CLF_UsedM CHECK (UsedM > 0),
        CONSTRAINT UQ_CLF UNIQUE (CuttingLayId, MaterialId)
    );

IF COL_LENGTH('dbo.CuttingLay', 'FabricMaterialId') IS NOT NULL
    EXEC (N'INSERT dbo.CuttingLayFabric (CuttingLayId, MaterialId, UsedM)
            SELECT cl.CuttingLayId, cl.FabricMaterialId, cl.FabricUsedM FROM dbo.CuttingLay cl
            WHERE NOT EXISTS (SELECT 1 FROM dbo.CuttingLayFabric f WHERE f.CuttingLayId = cl.CuttingLayId);');

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_CL_FabricUsed')
    ALTER TABLE dbo.CuttingLay DROP CONSTRAINT CK_CL_FabricUsed;
IF OBJECT_ID(N'dbo.FK_CL_Material', N'F') IS NOT NULL
    ALTER TABLE dbo.CuttingLay DROP CONSTRAINT FK_CL_Material;
IF COL_LENGTH('dbo.CuttingLay', 'FabricUsedM') IS NOT NULL
    ALTER TABLE dbo.CuttingLay DROP COLUMN FabricUsedM;
IF COL_LENGTH('dbo.CuttingLay', 'FabricMaterialId') IS NOT NULL
    ALTER TABLE dbo.CuttingLay DROP COLUMN FabricMaterialId;

IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.CuttingLayFabric') AND minor_id = 0
               AND name = N'MS_Description')
    EXEC sys.sp_addextendedproperty N'MS_Description', N'Số mét từng vải của bàn cắt', N'SCHEMA', N'dbo', N'TABLE', N'CuttingLayFabric';

COMMIT;
GO

DECLARE @n INT = (SELECT COUNT(*) FROM dbo.CuttingLayFabric);
PRINT N'Bàn cắt nhiều vải: CuttingLayFabric có ' + CAST(@n AS NVARCHAR(10)) + N' dòng.';
GO
