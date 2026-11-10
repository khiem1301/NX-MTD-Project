/* =====================================================================================
   Gộp các patch 09/11/2026, chạy sau 2026-11-07_expected_delivery_date.sql:
     A. Bìa cắt theo đơn (DEV-ALL-85): ước mét vải = số lớp × chiều dài bìa.
        - Bỏ vw_RecutFabricNeed, vw_ProductLastSizeRatio; ProductComponentMaterial bỏ LengthCm, WidthCm, CK_PCM_Size.
        - ProductMarker thành bìa của đơn (OrderId, MarkerNo = nhóm bìa, MarkerCode, LengthM, Status). Bìa cũ theo mã hàng × size
          KHÔNG đổi sang được nên bị XÓA (patch in số dòng đã xóa). Thêm ProductMarkerRatio, ProductMarkerComponent,
          trigger trg_ProductMarker_Check, trg_ProductMarkerRatio_Check (50055); dữ liệu demo cho các đơn DEMO-%.
        - Bỏ Material.WidthCm (khổ vải) và chiều rộng bìa: cách ước theo bìa không cần khổ vải.
        - Bàn cắt (CuttingLay) không gắn bìa.
     B. Công đoạn chỉ gắn chi tiết vải (DEV-ALL-86): OperationComponent giữ 3 cột (nếu DB đã chạy bản có MaterialId thì gộp
        dòng và bỏ cột); bỏ liên kết với phụ liệu (TRIM); in chi tiết cắt đang khai hơn một vải ở cùng màu áo.
     C. Định mức NVL theo màu áo, không theo size (DEV-ALL-87): ProductComponentMaterial thay ProductVariantId bằng
        ProductColorId; tạo lại 6 trigger 50041 (so màu của định mức với màu dòng đơn); gộp các dòng cùng màu và trỏ lại chứng từ.
   Thay cho ba file 2026-11-09_marker_by_order.sql, 2026-11-09_operation_component_fabric.sql, 2026-11-09_norm_by_color.sql
   (DB đã chạy một phần các file đó vẫn chạy được file này: mỗi bước tự bỏ qua khi đã xong).
   DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy. Chạy lại không lỗi.
   Chạy cùng lúc với bản backend tương ứng, rồi khởi động lại backend.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-09_all_changes.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

/* ##################### A. Bìa cắt theo đơn (DEV-ALL-85) ##################### */


/* =========================== 1. Bỏ hai view =========================== */
IF OBJECT_ID(N'dbo.vw_RecutFabricNeed', N'V') IS NOT NULL
BEGIN DROP VIEW dbo.vw_RecutFabricNeed; PRINT N'Đã bỏ vw_RecutFabricNeed.'; END
ELSE PRINT N'vw_RecutFabricNeed đã bỏ, bỏ qua.';
IF OBJECT_ID(N'dbo.vw_ProductLastSizeRatio', N'V') IS NOT NULL
BEGIN DROP VIEW dbo.vw_ProductLastSizeRatio; PRINT N'Đã bỏ vw_ProductLastSizeRatio.'; END
ELSE PRINT N'vw_ProductLastSizeRatio đã bỏ, bỏ qua.';
GO

/* =========================== 2. Bỏ kích thước chi tiết =========================== */
IF OBJECT_ID(N'dbo.CK_PCM_Size', N'C') IS NOT NULL ALTER TABLE dbo.ProductComponentMaterial DROP CONSTRAINT CK_PCM_Size;
IF COL_LENGTH('dbo.ProductComponentMaterial', 'LengthCm') IS NOT NULL ALTER TABLE dbo.ProductComponentMaterial DROP COLUMN LengthCm;
IF COL_LENGTH('dbo.ProductComponentMaterial', 'WidthCm') IS NOT NULL ALTER TABLE dbo.ProductComponentMaterial DROP COLUMN WidthCm;
PRINT N'ProductComponentMaterial không còn LengthCm, WidthCm.';
GO

/* =========================== 3-4. Bảng bìa =========================== */
IF OBJECT_ID(N'dbo.ProductMarker', N'U') IS NOT NULL AND COL_LENGTH('dbo.ProductMarker', 'OrderId') IS NULL
BEGIN
    DECLARE @old INT = (SELECT COUNT(*) FROM dbo.ProductMarker);
    DROP TABLE dbo.ProductMarker;
    PRINT N'Đã xóa ProductMarker cũ (bìa theo size): ' + CAST(@old AS NVARCHAR(10)) + N' dòng.';
END
GO
IF OBJECT_ID(N'dbo.ProductMarker', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductMarker (
        ProductMarkerId INT IDENTITY(1,1) CONSTRAINT PK_ProductMarker PRIMARY KEY,
        OrderId         INT           NOT NULL CONSTRAINT FK_PM_Order REFERENCES dbo.CustomerOrder(OrderId),
        ProductId       INT           NOT NULL CONSTRAINT FK_PM_Product REFERENCES dbo.Product(ProductId),
        MarkerNo        TINYINT       NOT NULL,
        MarkerCode      VARCHAR(20)   NOT NULL,
        LengthM         DECIMAL(7,2)  NOT NULL,
        Status          VARCHAR(20)   NOT NULL CONSTRAINT DF_PM_Status DEFAULT 'ACTIVE',
        CONSTRAINT CK_PM_No     CHECK (MarkerNo > 0),
        CONSTRAINT CK_PM_Length CHECK (LengthM > 0),
        CONSTRAINT CK_PM_Status CHECK (Status IN ('ACTIVE','DISCONTINUED')),
        CONSTRAINT UQ_PM UNIQUE (OrderId, ProductId, MarkerNo, MarkerCode)
    );
    CREATE INDEX IX_PM_Product ON dbo.ProductMarker(ProductId);
    PRINT N'Đã tạo ProductMarker (bìa của đơn).';
END
ELSE PRINT N'ProductMarker đã là bìa của đơn, bỏ qua.';

IF OBJECT_ID(N'dbo.ProductMarkerRatio', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductMarkerRatio (
        ProductMarkerId INT          NOT NULL CONSTRAINT FK_PMR_Marker REFERENCES dbo.ProductMarker(ProductMarkerId),
        SizeCode        VARCHAR(10)  NOT NULL,
        PiecesPerPly    SMALLINT     NOT NULL CONSTRAINT CK_PMR_Pieces CHECK (PiecesPerPly > 0),
        CONSTRAINT PK_ProductMarkerRatio PRIMARY KEY (ProductMarkerId, SizeCode)
    );
    PRINT N'Đã tạo ProductMarkerRatio.';
END
ELSE PRINT N'ProductMarkerRatio đã có, bỏ qua.';

IF OBJECT_ID(N'dbo.ProductMarkerComponent', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductMarkerComponent (
        ProductId          INT     NOT NULL,
        MarkerNo           TINYINT NOT NULL,
        ProductComponentId INT     NOT NULL,
        CONSTRAINT PK_ProductMarkerComponent PRIMARY KEY (ProductId, MarkerNo, ProductComponentId),
        CONSTRAINT CK_PMC_No CHECK (MarkerNo > 0),
        CONSTRAINT FK_PMC_Component FOREIGN KEY (ProductComponentId, ProductId) REFERENCES dbo.ProductComponent(ProductComponentId, ProductId)
    );
    CREATE INDEX IX_PMC_Component ON dbo.ProductMarkerComponent(ProductComponentId);
    PRINT N'Đã tạo ProductMarkerComponent.';
END
ELSE PRINT N'ProductMarkerComponent đã có, bỏ qua.';
GO

/* =========================== 2b. Bỏ khổ vải và khổ bìa (khổ cố định 1,5 m) =========================== */
IF OBJECT_ID(N'dbo.CK_Material_Size', N'C') IS NOT NULL ALTER TABLE dbo.Material DROP CONSTRAINT CK_Material_Size;
IF COL_LENGTH('dbo.Material', 'WidthCm') IS NOT NULL ALTER TABLE dbo.Material DROP COLUMN WidthCm;
IF OBJECT_ID(N'dbo.CK_PM_Width', N'C') IS NOT NULL ALTER TABLE dbo.ProductMarker DROP CONSTRAINT CK_PM_Width;
IF COL_LENGTH('dbo.ProductMarker', 'WidthCm') IS NOT NULL AND COL_LENGTH('dbo.ProductMarker', 'OrderId') IS NOT NULL
    ALTER TABLE dbo.ProductMarker DROP COLUMN WidthCm;
PRINT N'Material, ProductMarker không còn WidthCm.';
GO

/* =========================== 5. Trigger =========================== */
/* Bìa cắt của đơn (DEV-ALL-85, 50055): mã hàng có trong đơn; nhóm bìa đã khai vải theo màu áo; tỉ lệ chỉ gồm size có trong đơn.
   Chỉ kiểm khi thêm / sửa; xóa dòng đơn sau khi có bìa thì backend tự chặn. */
CREATE OR ALTER TRIGGER dbo.trg_ProductMarker_Check ON dbo.ProductMarker AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderLine ol WHERE ol.OrderId = i.OrderId AND ol.ProductId = i.ProductId))
        THROW 50055, N'Bìa phải thuộc mã hàng có trong đơn hàng.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductMarkerFabric f WHERE f.ProductId = i.ProductId AND f.MarkerNo = i.MarkerNo))
        THROW 50055, N'Nhóm bìa chưa khai vải theo màu áo ở mã hàng (ProductMarkerFabric).', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.ProductMarkerRatio r ON r.ProductMarkerId = i.ProductMarkerId
               WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderLine ol JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
                                 WHERE ol.OrderId = i.OrderId AND ol.ProductId = i.ProductId AND pv.SizeCode = r.SizeCode))
        THROW 50055, N'Size trong tỉ lệ bìa phải là size có trong đơn hàng của mã hàng đó.', 1;
END;
GO

CREATE OR ALTER TRIGGER dbo.trg_ProductMarkerRatio_Check ON dbo.ProductMarkerRatio AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.ProductMarker pm ON pm.ProductMarkerId = i.ProductMarkerId
               WHERE NOT EXISTS (SELECT 1 FROM dbo.OrderLine ol JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
                                 WHERE ol.OrderId = pm.OrderId AND ol.ProductId = pm.ProductId AND pv.SizeCode = i.SizeCode))
        THROW 50055, N'Size trong tỉ lệ bìa phải là size có trong đơn hàng của mã hàng đó.', 1;
END;
GO

/* =========================== 6. Dữ liệu demo =========================== */
/* ---------- Bìa cắt theo đơn (DEV-ALL-85) ----------
   Chi tiết trên nhóm bìa của mã hàng demo; mỗi đơn demo có DEMO-AO01 / DEMO-QU02 một bìa <nhóm>A mỗi nhóm, tỉ lệ theo size có
   trong đơn (áo S1 M2 L1, quần M1 L1), chiều dài = số sản phẩm / lớp × (áo: thân 0,55 m, tay 0,40 m; quần 0,35 m).
   DEMO-DH001 thêm bìa 1B (thân, chỉ M, M4, 2,10 m) để thử chọn bìa. Chạy lại không thêm trùng. */
SET NOCOUNT ON;
INSERT dbo.ProductMarkerComponent (ProductId, MarkerNo, ProductComponentId)
SELECT pc.ProductId, t.MarkerNo, pc.ProductComponentId
FROM (VALUES ('DEMO-AO01', N'Thân trước', 1), ('DEMO-AO01', N'Thân sau', 1), ('DEMO-AO01', N'Tay áo', 2),
             ('DEMO-QU02', N'Ống quần', 1)) t (ProductCode, ComponentName, MarkerNo)
JOIN dbo.Product p           ON p.ProductCode = t.ProductCode
JOIN dbo.ProductComponent pc ON pc.ProductId = p.ProductId AND pc.ComponentName = t.ComponentName
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductMarkerComponent x
                  WHERE x.ProductId = pc.ProductId AND x.MarkerNo = t.MarkerNo AND x.ProductComponentId = pc.ProductComponentId);

DECLARE @plan TABLE (ProductCode VARCHAR(30), MarkerNo TINYINT, SizeCode VARCHAR(10), Pieces SMALLINT, MetersPerPiece DECIMAL(5,2));
INSERT @plan VALUES
 ('DEMO-AO01', 1, 'S', 1, 0.55), ('DEMO-AO01', 1, 'M', 2, 0.55), ('DEMO-AO01', 1, 'L', 1, 0.55),
 ('DEMO-AO01', 2, 'S', 1, 0.40), ('DEMO-AO01', 2, 'M', 2, 0.40), ('DEMO-AO01', 2, 'L', 1, 0.40),
 ('DEMO-QU02', 1, 'M', 1, 0.35), ('DEMO-QU02', 1, 'L', 1, 0.35);
DECLARE @present TABLE (OrderId INT, ProductId INT, MarkerNo TINYINT, SizeCode VARCHAR(10), Pieces SMALLINT, MetersPerPiece DECIMAL(5,2));
INSERT @present
SELECT DISTINCT ol.OrderId, ol.ProductId, pl.MarkerNo, pl.SizeCode, pl.Pieces, pl.MetersPerPiece
FROM dbo.OrderLine ol
JOIN dbo.CustomerOrder o   ON o.OrderId = ol.OrderId
JOIN dbo.Product p         ON p.ProductId = ol.ProductId
JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
JOIN @plan pl              ON pl.ProductCode = p.ProductCode AND pl.SizeCode = pv.SizeCode
WHERE o.OrderCode LIKE 'DEMO-%'
  AND EXISTS (SELECT 1 FROM dbo.ProductMarkerFabric f WHERE f.ProductId = ol.ProductId AND f.MarkerNo = pl.MarkerNo);

INSERT dbo.ProductMarker (OrderId, ProductId, MarkerNo, MarkerCode, LengthM)
SELECT x.OrderId, x.ProductId, x.MarkerNo, CONCAT(x.MarkerNo, 'A'), SUM(x.Pieces * x.MetersPerPiece)
FROM @present x
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductMarker m WHERE m.OrderId = x.OrderId AND m.ProductId = x.ProductId AND m.MarkerNo = x.MarkerNo)
GROUP BY x.OrderId, x.ProductId, x.MarkerNo;
INSERT dbo.ProductMarkerRatio (ProductMarkerId, SizeCode, PiecesPerPly)
SELECT m.ProductMarkerId, x.SizeCode, x.Pieces
FROM @present x
JOIN dbo.ProductMarker m ON m.OrderId = x.OrderId AND m.ProductId = x.ProductId AND m.MarkerNo = x.MarkerNo
                        AND m.MarkerCode = CONCAT(x.MarkerNo, 'A')
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductMarkerRatio r WHERE r.ProductMarkerId = m.ProductMarkerId AND r.SizeCode = x.SizeCode);

INSERT dbo.ProductMarker (OrderId, ProductId, MarkerNo, MarkerCode, LengthM)
SELECT DISTINCT x.OrderId, x.ProductId, 1, '1B', 2.10
FROM @present x
JOIN dbo.CustomerOrder o ON o.OrderId = x.OrderId
JOIN dbo.Product p       ON p.ProductId = x.ProductId
WHERE o.OrderCode = 'DEMO-DH001' AND p.ProductCode = 'DEMO-AO01' AND x.MarkerNo = 1 AND x.SizeCode = 'M'
  AND NOT EXISTS (SELECT 1 FROM dbo.ProductMarker m WHERE m.OrderId = x.OrderId AND m.ProductId = x.ProductId AND m.MarkerCode = '1B');
INSERT dbo.ProductMarkerRatio (ProductMarkerId, SizeCode, PiecesPerPly)
SELECT m.ProductMarkerId, 'M', 4
FROM dbo.ProductMarker m
JOIN dbo.CustomerOrder o ON o.OrderId = m.OrderId
WHERE o.OrderCode = 'DEMO-DH001' AND m.MarkerCode = '1B'
  AND NOT EXISTS (SELECT 1 FROM dbo.ProductMarkerRatio r WHERE r.ProductMarkerId = m.ProductMarkerId);
DECLARE @markers INT = (SELECT COUNT(*) FROM dbo.ProductMarker);
PRINT N'Bìa cắt theo đơn (demo): ' + CAST(@markers AS NVARCHAR(10)) + N' bìa.';
GO


/* ##################### B. Công đoạn chỉ gắn chi tiết vải (DEV-ALL-86) ##################### */


/* =========================== 1. Bỏ MaterialId (nếu đã chạy bản trước) =========================== */
IF COL_LENGTH('dbo.OperationComponent', 'MaterialId') IS NULL
    PRINT N'OperationComponent không có MaterialId, bỏ qua bước 1.';
ELSE
BEGIN
    BEGIN TRANSACTION;
    SELECT DISTINCT OperationId, ProductComponentId, ProductId INTO #OcKeep FROM dbo.OperationComponent;
    DELETE dbo.OperationComponent;
    ALTER TABLE dbo.OperationComponent DROP CONSTRAINT PK_OperationComponent;
    IF OBJECT_ID(N'dbo.FK_OC_Material', N'F') IS NOT NULL ALTER TABLE dbo.OperationComponent DROP CONSTRAINT FK_OC_Material;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OC_Material' AND object_id = OBJECT_ID(N'dbo.OperationComponent'))
        DROP INDEX IX_OC_Material ON dbo.OperationComponent;
    ALTER TABLE dbo.OperationComponent DROP COLUMN MaterialId;
    ALTER TABLE dbo.OperationComponent ADD CONSTRAINT PK_OperationComponent PRIMARY KEY (OperationId, ProductComponentId);
    INSERT dbo.OperationComponent (OperationId, ProductComponentId, ProductId)
    SELECT OperationId, ProductComponentId, ProductId FROM #OcKeep;
    DECLARE @kept INT = @@ROWCOUNT;
    DROP TABLE #OcKeep;
    COMMIT;
    PRINT N'Đã bỏ MaterialId; còn ' + CAST(@kept AS NVARCHAR(10)) + N' dòng công đoạn × chi tiết.';
END
GO

/* =========================== 2. Bỏ liên kết với phụ liệu =========================== */
DELETE oc
FROM dbo.OperationComponent oc
JOIN dbo.ProductComponent pc ON pc.ProductComponentId = oc.ProductComponentId
WHERE pc.ComponentType <> 'PANEL';
DECLARE @trim INT = @@ROWCOUNT;
PRINT N'Bỏ ' + CAST(@trim AS NVARCHAR(10)) + N' liên kết công đoạn với phụ liệu (TRIM).';
GO

/* =========================== 3. Chi tiết cắt khai hơn một vải ở cùng màu áo =========================== */
-- Định mức có thể còn theo biến thể (chưa chạy 2026-11-09_norm_by_color.sql) hoặc đã theo màu: lấy màu áo theo dạng đang có.
DECLARE @colorJoin NVARCHAR(300) = CASE
    WHEN COL_LENGTH('dbo.ProductComponentMaterial', 'ProductColorId') IS NOT NULL
        THEN N'CROSS APPLY (SELECT pcm.ProductColorId AS ProductColorId) c'
    ELSE N'JOIN dbo.ProductVariant c ON c.ProductVariantId = pcm.ProductVariantId' END;
DECLARE @sql NVARCHAR(MAX) = N'
DECLARE @multi INT = (
    SELECT COUNT(*) FROM (
        SELECT c.ProductColorId, pcm.ProductComponentId
        FROM dbo.ProductComponentMaterial pcm
        ' + @colorJoin + N'
        JOIN dbo.ProductComponent pc ON pc.ProductComponentId = pcm.ProductComponentId AND pc.ComponentType = ''PANEL''
        JOIN dbo.Material m ON m.MaterialId = pcm.MaterialId AND m.MaterialType = ''FABRIC''
        GROUP BY c.ProductColorId, pcm.ProductComponentId
        HAVING COUNT(DISTINCT pcm.MaterialId) > 1) x);
IF @multi = 0
    PRINT N''Không chi tiết cắt nào khai hơn một vải ở cùng màu áo.'';
ELSE
BEGIN
    PRINT N''CẢNH BÁO: '' + CAST(@multi AS NVARCHAR(10)) + N'' chi tiết cắt × màu áo khai hơn một vải; tách vải lót thành chi tiết riêng ở định mức:'';
    SELECT p.ProductCode, pc.ComponentName, col.Color, STRING_AGG(m.MaterialCode, '', '') AS Fabrics
    FROM (SELECT DISTINCT pcm.ProductId, c.ProductColorId, pcm.ProductComponentId, pcm.MaterialId
          FROM dbo.ProductComponentMaterial pcm ' + @colorJoin + N') x
    JOIN dbo.ProductComponent pc ON pc.ProductComponentId = x.ProductComponentId AND pc.ComponentType = ''PANEL''
    JOIN dbo.Material m ON m.MaterialId = x.MaterialId AND m.MaterialType = ''FABRIC''
    JOIN dbo.ProductColor col ON col.ProductColorId = x.ProductColorId
    JOIN dbo.Product p ON p.ProductId = x.ProductId
    GROUP BY p.ProductCode, pc.ComponentName, col.Color
    HAVING COUNT(*) > 1;
END';
EXEC sys.sp_executesql @sql;
GO

IF EXISTS (SELECT 1 FROM sys.extended_properties
           WHERE major_id = OBJECT_ID('dbo.OperationComponent') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_updateextendedproperty @name = N'MS_Description', @value = N'Công đoạn làm trên chi tiết vải nào',
         @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'OperationComponent';
ELSE
    EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Công đoạn làm trên chi tiết vải nào',
         @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'OperationComponent';
GO

/* ##################### C. Định mức NVL theo màu áo (DEV-ALL-87) ##################### */


/* =========================== 1. ProductColorId =========================== */
IF COL_LENGTH('dbo.ProductComponentMaterial', 'ProductColorId') IS NULL
    ALTER TABLE dbo.ProductComponentMaterial ADD ProductColorId INT NULL;
GO
IF COL_LENGTH('dbo.ProductComponentMaterial', 'ProductVariantId') IS NOT NULL
    EXEC(N'UPDATE pcm SET ProductColorId = pv.ProductColorId
           FROM dbo.ProductComponentMaterial pcm JOIN dbo.ProductVariant pv ON pv.ProductVariantId = pcm.ProductVariantId
           WHERE pcm.ProductColorId IS NULL;');
GO

/* =========================== 2. Trigger so màu (50041) =========================== */
CREATE OR ALTER TRIGGER dbo.trg_StockBalance_Check ON dbo.StockBalance AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF ISNULL(CAST(SESSION_CONTEXT(N'NXMTD_StockPost') AS INT), 0) <> 1
       AND EXISTS (SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.StockBalanceId = i.StockBalanceId
                   WHERE i.OnHandQty <> ISNULL(d.OnHandQty, 0))
        THROW 50047, N'Số tồn chỉ được thay đổi qua dbo.usp_Stock_Post.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.OrderLine ol                 ON ol.OrderLineId = i.OrderLineId
               JOIN dbo.ProductVariant olv           ON olv.ProductVariantId = ol.ProductVariantId
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductColorId <> olv.ProductColorId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết tồn kho phải là chi tiết vải trong định mức của đúng màu của dòng đơn.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.trg_WorkOrderLine_Check ON dbo.WorkOrderLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.WorkOrder wo ON wo.WorkOrderId = i.WorkOrderId
               JOIN dbo.OrderLine ol ON ol.OrderLineId = i.OrderLineId
               WHERE ol.OrderId <> wo.OrderId OR ol.ProductId <> wo.ProductId)
        THROW 50042, N'Dòng lệnh cắt phải thuộc đúng đơn hàng và mã hàng của lệnh cắt.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.OrderLine ol                 ON ol.OrderLineId = i.OrderLineId
               JOIN dbo.ProductVariant olv           ON olv.ProductVariantId = ol.ProductVariantId
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductColorId <> olv.ProductColorId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết cắt phải là chi tiết vải (PANEL) trong định mức của đúng màu của dòng đơn.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.trg_RequestLine_Check ON dbo.RequestLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.Request r    ON r.RequestId = i.RequestId
               JOIN dbo.OrderLine ol ON ol.OrderLineId = i.OrderLineId
               WHERE ol.OrderId <> r.OrderId)
        THROW 50042, N'Dòng đơn từ trỏ sang dòng của đơn hàng khác.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.Request r ON r.RequestId = i.RequestId
               WHERE i.ProductComponentMaterialId IS NOT NULL AND i.OrderLineId IS NULL AND r.WorkOrderId IS NULL)
        THROW 50046, N'Dòng cắt bù chi tiết phải ghi dòng đơn hoặc đơn từ phải gắn lệnh cắt.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               JOIN dbo.Request r                    ON r.RequestId = i.RequestId
               LEFT JOIN dbo.OrderLine ol            ON ol.OrderLineId = i.OrderLineId
               LEFT JOIN dbo.ProductVariant olv      ON olv.ProductVariantId = ol.ProductVariantId
               LEFT JOIN dbo.WorkOrder wo            ON wo.WorkOrderId = r.WorkOrderId
               WHERE pc.ComponentType <> 'PANEL'
                  OR (ol.OrderLineId IS NOT NULL AND pcm.ProductColorId <> olv.ProductColorId)
                  OR (ol.OrderLineId IS NULL AND pcm.ProductId <> ISNULL(wo.ProductId, pcm.ProductId)))
        THROW 50041, N'Chi tiết cắt bù phải là chi tiết vải trong định mức của đúng màu × size / mã hàng của lệnh cắt.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.trg_MaterialIssueLine_Check ON dbo.MaterialIssueLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i JOIN dbo.MaterialIssue mi ON mi.MaterialIssueId = i.MaterialIssueId
               WHERE (mi.WarehouseType = 'MATERIAL' AND i.MaterialId IS NULL)
                  OR (mi.WarehouseType = 'SEMI_FINISHED' AND i.OrderLineId IS NULL))
        THROW 50045, N'Phiếu cấp NVL chỉ có dòng NVL, phiếu cấp BTP chỉ có dòng BTP.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.MaterialIssue mi ON mi.MaterialIssueId = i.MaterialIssueId
               JOIN dbo.OrderLine ol     ON ol.OrderLineId = i.OrderLineId
               WHERE ol.OrderId <> mi.OrderId)
        THROW 50042, N'Dòng phiếu cấp BTP thuộc đơn hàng khác với phiếu cấp.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.OrderLine ol                 ON ol.OrderLineId = i.OrderLineId
               JOIN dbo.ProductVariant olv           ON olv.ProductVariantId = ol.ProductVariantId
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductColorId <> olv.ProductColorId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết cấp BTP phải là chi tiết vải trong định mức của đúng màu của dòng đơn.', 1;
END;
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
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.CuttingLay cl                ON cl.CuttingLayId = i.CuttingLayId
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               WHERE pcm.MaterialId <> cl.FabricMaterialId)
        THROW 50049, N'Chi tiết cắt bù dùng vải (đã gồm màu) khác vải của bàn cắt.', 1;
END;
GO
CREATE OR ALTER TRIGGER dbo.trg_CutHandoverLine_Check ON dbo.CutHandoverLine AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.CutHandover h ON h.CutHandoverId = i.CutHandoverId
               JOIN dbo.WorkOrder wo  ON wo.WorkOrderId = h.WorkOrderId
               JOIN dbo.OrderLine ol  ON ol.OrderLineId = i.OrderLineId
               WHERE ol.OrderId <> wo.OrderId OR ol.ProductId <> wo.ProductId)
        THROW 50042, N'Dòng bàn giao BTP phải thuộc đúng đơn hàng và mã hàng của lệnh cắt.', 1;
    IF EXISTS (SELECT 1 FROM inserted i
               JOIN dbo.OrderLine ol                 ON ol.OrderLineId = i.OrderLineId
               JOIN dbo.ProductVariant olv           ON olv.ProductVariantId = ol.ProductVariantId
               JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = i.ProductComponentMaterialId
               JOIN dbo.ProductComponent pc          ON pc.ProductComponentId = pcm.ProductComponentId
               WHERE pcm.ProductColorId <> olv.ProductColorId OR pc.ComponentType <> 'PANEL')
        THROW 50041, N'Chi tiết bàn giao phải là chi tiết vải trong định mức của đúng màu của dòng đơn.', 1;
END;
GO

/* =========================== 3-4. Gộp dòng theo màu, đổi khóa =========================== */
IF COL_LENGTH('dbo.ProductComponentMaterial', 'ProductVariantId') IS NULL
    PRINT N'ProductComponentMaterial đã theo màu, bỏ qua bước 3-4.';
ELSE
BEGIN
    BEGIN TRANSACTION;
    SELECT ProductComponentMaterialId AS OldId,
           MIN(ProductComponentMaterialId) OVER (PARTITION BY ProductId, ProductColorId, ProductComponentId, MaterialId) AS KeepId
    INTO #PcmMap FROM dbo.ProductComponentMaterial;
    DELETE #PcmMap WHERE OldId = KeepId;

    DECLARE @notes INT = (SELECT COUNT(*) FROM (
        SELECT ProductId, ProductColorId, ProductComponentId, MaterialId FROM dbo.ProductComponentMaterial
        GROUP BY ProductId, ProductColorId, ProductComponentId, MaterialId
        HAVING COUNT(DISTINCT ISNULL(SpecNote, N'')) > 1) x);
    UPDATE k SET SpecNote = n.SpecNote
    FROM dbo.ProductComponentMaterial k
    CROSS APPLY (SELECT TOP 1 o.SpecNote FROM #PcmMap m JOIN dbo.ProductComponentMaterial o ON o.ProductComponentMaterialId = m.OldId
                 WHERE m.KeepId = k.ProductComponentMaterialId AND o.SpecNote IS NOT NULL ORDER BY o.ProductComponentMaterialId) n
    WHERE k.SpecNote IS NULL;

    UPDATE t SET ProductComponentMaterialId = m.KeepId FROM dbo.WorkOrderLine t     JOIN #PcmMap m ON m.OldId = t.ProductComponentMaterialId;
    DECLARE @refs INT = @@ROWCOUNT;
    UPDATE t SET ProductComponentMaterialId = m.KeepId FROM dbo.RequestLine t       JOIN #PcmMap m ON m.OldId = t.ProductComponentMaterialId;
    SET @refs += @@ROWCOUNT;
    UPDATE t SET ProductComponentMaterialId = m.KeepId FROM dbo.CuttingLayLine t    JOIN #PcmMap m ON m.OldId = t.ProductComponentMaterialId;
    SET @refs += @@ROWCOUNT;
    UPDATE t SET ProductComponentMaterialId = m.KeepId FROM dbo.CutHandoverLine t   JOIN #PcmMap m ON m.OldId = t.ProductComponentMaterialId;
    SET @refs += @@ROWCOUNT;
    UPDATE t SET ProductComponentMaterialId = m.KeepId FROM dbo.MaterialIssueLine t JOIN #PcmMap m ON m.OldId = t.ProductComponentMaterialId;
    SET @refs += @@ROWCOUNT;
    UPDATE t SET ProductComponentMaterialId = m.KeepId FROM dbo.StockBalance t      JOIN #PcmMap m ON m.OldId = t.ProductComponentMaterialId;
    SET @refs += @@ROWCOUNT;

    DELETE p FROM dbo.ProductComponentMaterial p JOIN #PcmMap m ON m.OldId = p.ProductComponentMaterialId;
    DECLARE @merged INT = @@ROWCOUNT;
    DROP TABLE #PcmMap;

    ALTER TABLE dbo.ProductComponentMaterial DROP CONSTRAINT FK_PCM_Variant;
    ALTER TABLE dbo.ProductComponentMaterial DROP CONSTRAINT UQ_PCM;
    ALTER TABLE dbo.ProductComponentMaterial DROP COLUMN ProductVariantId;
    ALTER TABLE dbo.ProductComponentMaterial ALTER COLUMN ProductColorId INT NOT NULL;
    ALTER TABLE dbo.ProductComponentMaterial ADD
        CONSTRAINT FK_PCM_Color FOREIGN KEY (ProductColorId, ProductId) REFERENCES dbo.ProductColor(ProductColorId, ProductId),
        CONSTRAINT UQ_PCM UNIQUE (ProductColorId, ProductComponentId, MaterialId);
    COMMIT;

    DECLARE @left INT = (SELECT COUNT(*) FROM dbo.ProductComponentMaterial);
    PRINT N'Định mức theo màu: gộp ' + CAST(@merged AS NVARCHAR(10)) + N' dòng (còn ' + CAST(@left AS NVARCHAR(10))
        + N'), trỏ lại ' + CAST(@refs AS NVARCHAR(10)) + N' dòng chứng từ.';
    IF @notes > 0
        PRINT N'CẢNH BÁO: ' + CAST(@notes AS NVARCHAR(10)) + N' nhóm có ghi chú quy cách khác nhau giữa các size; đã giữ ghi chú của dòng đầu tiên.';
END
GO

IF EXISTS (SELECT 1 FROM sys.extended_properties
           WHERE major_id = OBJECT_ID('dbo.ProductComponentMaterial') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_updateextendedproperty @name = N'MS_Description', @value = N'Định mức NVL theo màu áo × thành phần: dùng NVL nào (đã gồm màu NVL)',
         @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProductComponentMaterial';
ELSE
    EXEC sys.sp_addextendedproperty @name = N'MS_Description', @value = N'Định mức NVL theo màu áo × thành phần: dùng NVL nào (đã gồm màu NVL)',
         @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ProductComponentMaterial';
GO
