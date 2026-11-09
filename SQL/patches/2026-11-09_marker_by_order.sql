/* =====================================================================================
   DEV-ALL-85 · Claude (AI) · 09/11/2026 · Bìa cắt theo đơn: ước mét vải = số lớp × chiều dài bìa.
     1. Bỏ vw_RecutFabricNeed (mét cắt bù theo hình chữ nhật chi tiết) và vw_ProductLastSizeRatio (đoán tỉ lệ size từ bàn cắt).
     2. ProductComponentMaterial bỏ LengthCm, WidthCm, CK_PCM_Size (kích thước chi tiết không còn dùng).
     3. ProductMarker thành bìa của đơn: OrderId, MarkerNo (nhóm bìa), MarkerCode (1A, 1B...), LengthM (m), Status.
        Bìa cũ theo mã hàng × size (cm) KHÔNG đổi sang được (không có đơn, không có tỉ lệ) nên bị XÓA; patch in số dòng đã xóa.
     4. Thêm ProductMarkerRatio (tỉ lệ size: số sản phẩm mỗi size trên 1 lớp) và ProductMarkerComponent (chi tiết trên nhóm bìa).
     5. Trigger trg_ProductMarker_Check, trg_ProductMarkerRatio_Check (50055).
     6. Dữ liệu demo: chi tiết trên nhóm bìa và bìa cho các đơn DEMO-% (chỉ thêm khi chưa có).
     7. Bỏ khổ vải Material.WidthCm (CK_Material_Size) và khổ bìa: khổ vải cố định 1,5 m ở backend.
   Bàn cắt (CuttingLay) KHÔNG gắn bìa: một lần nhập có thể dùng nhiều bìa; mét và sản lượng thực tế vẫn lấy từ bàn cắt.
   ProductMarkerFabric giữ nguyên (MarkerNo nay là nhóm bìa của mã hàng).
   Chạy sau 2026-11-07_expected_delivery_date.sql. DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy. Chạy lại không lỗi.
   Backend phải được sửa theo trước khi chạy patch lên DB đang dùng (entity ProductMarker, ProductComponentMaterial, 2 view).
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-09_marker_by_order.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

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

