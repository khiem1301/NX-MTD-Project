/* =====================================================================================
   DEV-ALL-86 · Claude (AI) · 09/11/2026 · Công đoạn gắn với chi tiết vải × vải.
     1. OperationComponent thêm MaterialId (FK_OC_Material → Material), PK thành (OperationId, ProductComponentId, MaterialId),
        thêm IX_OC_Material.
     2. Dữ liệu cũ (công đoạn × chi tiết) được nở ra mọi vải của chi tiết đó trong định mức (chi tiết PANEL, NVL loại vải).
        Liên kết với phụ liệu (TRIM) hoặc chi tiết chưa có vải trong định mức bị BỎ; patch in số dòng giữ / bỏ.
   Chạy sau 2026-11-09_marker_by_order.sql. DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy. Chạy lại không lỗi.
   Chạy cùng lúc với bản backend có OperationComponent.MaterialId (code cũ không biết cột mới, thêm liên kết sẽ lỗi).
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-09_operation_component_fabric.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH('dbo.OperationComponent', 'MaterialId') IS NOT NULL
BEGIN
    PRINT N'OperationComponent đã có MaterialId, bỏ qua.';
    RETURN;
END

BEGIN TRANSACTION;

SELECT OperationId, ProductComponentId, ProductId INTO #OcOld FROM dbo.OperationComponent;
DELETE dbo.OperationComponent;
ALTER TABLE dbo.OperationComponent DROP CONSTRAINT PK_OperationComponent;
ALTER TABLE dbo.OperationComponent ADD MaterialId INT NOT NULL
    CONSTRAINT FK_OC_Material REFERENCES dbo.Material(MaterialId);
EXEC(N'ALTER TABLE dbo.OperationComponent ADD CONSTRAINT PK_OperationComponent PRIMARY KEY (OperationId, ProductComponentId, MaterialId);
       CREATE INDEX IX_OC_Material ON dbo.OperationComponent(MaterialId);');

EXEC(N'INSERT dbo.OperationComponent (OperationId, ProductComponentId, ProductId, MaterialId)
       SELECT DISTINCT o.OperationId, o.ProductComponentId, o.ProductId, pcm.MaterialId
       FROM #OcOld o
       JOIN dbo.ProductComponent pc ON pc.ProductComponentId = o.ProductComponentId AND pc.ComponentType = ''PANEL''
       JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentId = o.ProductComponentId
       JOIN dbo.Material m ON m.MaterialId = pcm.MaterialId AND m.MaterialType = ''FABRIC'';');

DECLARE @old INT = (SELECT COUNT(*) FROM #OcOld);
DECLARE @new INT = (SELECT COUNT(*) FROM dbo.OperationComponent);
DECLARE @dropped INT = (SELECT COUNT(*) FROM #OcOld o
                        WHERE NOT EXISTS (SELECT 1 FROM dbo.OperationComponent n
                                          WHERE n.OperationId = o.OperationId AND n.ProductComponentId = o.ProductComponentId));
DROP TABLE #OcOld;
COMMIT;

PRINT N'OperationComponent: ' + CAST(@old AS NVARCHAR(10)) + N' liên kết cũ thành ' + CAST(@new AS NVARCHAR(10))
    + N' dòng chi tiết × vải; bỏ ' + CAST(@dropped AS NVARCHAR(10)) + N' liên kết (phụ liệu hoặc chi tiết chưa có vải).';

GO

IF EXISTS (SELECT 1 FROM sys.extended_properties
           WHERE major_id = OBJECT_ID('dbo.OperationComponent') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_updateextendedproperty @name = N'MS_Description', @value = N'Công đoạn làm trên chi tiết vải nào, vải nào',
         @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'OperationComponent';
ELSE
    EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Công đoạn làm trên chi tiết vải nào, vải nào',
         @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'OperationComponent';
GO
