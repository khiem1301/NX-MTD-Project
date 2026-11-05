/* =====================================================================================
   DEV-ALL-71 · Claude (AI) · 05/11/2026 · Màu áo thành bảng riêng; ảnh mã hàng: tối đa 5 ảnh phụ, mỗi màu 1-3 ảnh.
     1. CK_ProductImage_Sort đổi từ 1-10 thành 1-5. Ảnh phụ thứ 6 trở đi đang có bị gỡ khỏi mã hàng
        (file ảnh để job dọn ảnh mồ côi xóa sau 24 giờ).
     2. Thêm bảng ProductColor (màu áo của mã hàng, nơi duy nhất lưu tên màu). Màu lấy từ ProductVariant.Color và
        ProductMarkerFabric.Color đang có (bỏ khoảng trắng đầu cuối; khác nhau chỉ ở hoa thường thì gộp một màu).
     3. ProductVariant, ProductMarkerFabric: thêm ProductColorId (FK ghép với ProductId), bỏ cột Color;
        UQ_PV thành (ProductColorId, SizeCode), UQ_PMF thành (ProductId, MarkerNo, ProductColorId).
     4. Thêm bảng ProductColorImage (ảnh theo màu, SortOrder 1-3 trong mỗi màu). Nếu đã chạy bản patch cũ
        (ProductColorImage có ProductId, Color) thì chuyển sang ProductColorId.
     5. vw_OrderLineProgress đọc màu từ ProductColor.
   Chạy sau 2026-11-03_all_changes.sql và 2026-11-03_dispatch_delivered_only.sql. DB dựng mới từ SQL/NX-MTD_Database.sql
   không cần chạy. Chạy lại không lỗi. Các patch dữ liệu demo cũ (2026-11-02_demo_*.sql) còn ghi ProductVariant.Color:
   chỉ chạy được trước patch này. Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-05_product_color_images.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

/* =========================== 1. Tối đa 5 ảnh phụ =========================== */
IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE name = 'CK_ProductImage_Sort' AND parent_object_id = OBJECT_ID('dbo.ProductImage')
             AND definition NOT LIKE '%<=(5))%')   -- SQL Server lưu dạng ([SortOrder]>=(1) AND [SortOrder]<=(10))
BEGIN
    BEGIN TRANSACTION;
    DECLARE @removed INT;
    DELETE FROM dbo.ProductImage WHERE SortOrder > 5;
    SET @removed = @@ROWCOUNT;
    ALTER TABLE dbo.ProductImage DROP CONSTRAINT CK_ProductImage_Sort;
    ALTER TABLE dbo.ProductImage ADD CONSTRAINT CK_ProductImage_Sort CHECK (SortOrder BETWEEN 1 AND 5);
    COMMIT TRANSACTION;
    PRINT N'CK_ProductImage_Sort còn 1-5; đã gỡ ' + CAST(@removed AS NVARCHAR(10)) + N' ảnh phụ vượt 5.';
END
ELSE PRINT N'CK_ProductImage_Sort đã là 1-5, bỏ qua.';
GO

/* =========================== 2. Bảng màu áo =========================== */
IF OBJECT_ID('dbo.ProductColor', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductColor (
        ProductColorId  INT IDENTITY(1,1) CONSTRAINT PK_ProductColor PRIMARY KEY,
        ProductId       INT           NOT NULL CONSTRAINT FK_PCol_Product REFERENCES dbo.Product(ProductId),
        Color           NVARCHAR(50)  NOT NULL,
        CreatedAt       DATETIME2(0)  NOT NULL CONSTRAINT DF_PCol_CreatedAt DEFAULT (dbo.fn_VnNow()),
        CONSTRAINT UQ_PCol UNIQUE (ProductId, Color),
        CONSTRAINT UQ_PCol_IdProduct UNIQUE (ProductColorId, ProductId)
    );
    PRINT N'Đã thêm bảng ProductColor.';
END
ELSE PRINT N'ProductColor đã có, bỏ qua tạo bảng.';
GO

-- Đưa mọi tên màu đang có vào ProductColor. Dùng SQL động vì cột Color có thể đã bị bỏ ở lần chạy trước.
DECLARE @src NVARCHAR(MAX) = N'';
IF COL_LENGTH('dbo.ProductVariant', 'Color') IS NOT NULL
    SET @src += N'SELECT ProductId, LTRIM(RTRIM(Color)) AS Color FROM dbo.ProductVariant';
IF COL_LENGTH('dbo.ProductMarkerFabric', 'Color') IS NOT NULL
    SET @src += CASE WHEN @src = N'' THEN N'' ELSE N' UNION ALL ' END
              + N'SELECT ProductId, LTRIM(RTRIM(Color)) FROM dbo.ProductMarkerFabric';
IF COL_LENGTH('dbo.ProductColorImage', 'Color') IS NOT NULL
    SET @src += CASE WHEN @src = N'' THEN N'' ELSE N' UNION ALL ' END
              + N'SELECT ProductId, LTRIM(RTRIM(Color)) FROM dbo.ProductColorImage';
IF @src <> N''
BEGIN
    -- Collation CI: GROUP BY gộp màu chỉ khác hoa thường, MIN chọn một cách viết.
    EXEC (N'INSERT dbo.ProductColor (ProductId, Color)
            SELECT s.ProductId, MIN(s.Color) FROM (' + @src + N') s
            WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductColor c WHERE c.ProductId = s.ProductId AND c.Color = s.Color)
            GROUP BY s.ProductId, s.Color;');
    PRINT N'Đã đưa tên màu đang có vào ProductColor.';
END
GO

/* =========================== 3. Biến thể và vải của bìa trỏ ProductColorId =========================== */
IF COL_LENGTH('dbo.ProductVariant', 'Color') IS NOT NULL
BEGIN
    BEGIN TRANSACTION;
    IF COL_LENGTH('dbo.ProductVariant', 'ProductColorId') IS NULL
        EXEC (N'ALTER TABLE dbo.ProductVariant ADD ProductColorId INT NULL;');
    EXEC (N'UPDATE pv SET ProductColorId = c.ProductColorId
            FROM dbo.ProductVariant pv JOIN dbo.ProductColor c ON c.ProductId = pv.ProductId AND c.Color = LTRIM(RTRIM(pv.Color));
            ALTER TABLE dbo.ProductVariant ALTER COLUMN ProductColorId INT NOT NULL;
            ALTER TABLE dbo.ProductVariant DROP CONSTRAINT UQ_PV;
            ALTER TABLE dbo.ProductVariant DROP COLUMN Color;
            ALTER TABLE dbo.ProductVariant ADD CONSTRAINT FK_PV_Color FOREIGN KEY (ProductColorId, ProductId)
                REFERENCES dbo.ProductColor(ProductColorId, ProductId);
            ALTER TABLE dbo.ProductVariant ADD CONSTRAINT UQ_PV UNIQUE (ProductColorId, SizeCode);');
    COMMIT TRANSACTION;
    PRINT N'ProductVariant: đã thay Color bằng ProductColorId.';
END
ELSE PRINT N'ProductVariant đã dùng ProductColorId, bỏ qua.';
GO

IF COL_LENGTH('dbo.ProductMarkerFabric', 'Color') IS NOT NULL
BEGIN
    BEGIN TRANSACTION;
    IF COL_LENGTH('dbo.ProductMarkerFabric', 'ProductColorId') IS NULL
        EXEC (N'ALTER TABLE dbo.ProductMarkerFabric ADD ProductColorId INT NULL;');
    EXEC (N'UPDATE f SET ProductColorId = c.ProductColorId
            FROM dbo.ProductMarkerFabric f JOIN dbo.ProductColor c ON c.ProductId = f.ProductId AND c.Color = LTRIM(RTRIM(f.Color));
            ALTER TABLE dbo.ProductMarkerFabric ALTER COLUMN ProductColorId INT NOT NULL;
            ALTER TABLE dbo.ProductMarkerFabric DROP CONSTRAINT UQ_PMF;
            ALTER TABLE dbo.ProductMarkerFabric DROP COLUMN Color;
            ALTER TABLE dbo.ProductMarkerFabric ADD CONSTRAINT FK_PMF_Color FOREIGN KEY (ProductColorId, ProductId)
                REFERENCES dbo.ProductColor(ProductColorId, ProductId);
            ALTER TABLE dbo.ProductMarkerFabric ADD CONSTRAINT UQ_PMF UNIQUE (ProductId, MarkerNo, ProductColorId);');
    COMMIT TRANSACTION;
    PRINT N'ProductMarkerFabric: đã thay Color bằng ProductColorId.';
END
ELSE PRINT N'ProductMarkerFabric đã dùng ProductColorId, bỏ qua.';
GO

/* =========================== 4. Ảnh theo màu áo =========================== */
IF OBJECT_ID('dbo.ProductColorImage', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductColorImage (
        ProductColorImageId INT IDENTITY(1,1) CONSTRAINT PK_ProductColorImage PRIMARY KEY,
        ProductColorId      INT           NOT NULL CONSTRAINT FK_ProductColorImage_Color REFERENCES dbo.ProductColor(ProductColorId),
        ImageKey            NVARCHAR(500) NOT NULL,
        SortOrder           TINYINT       NOT NULL,
        CreatedAt           DATETIME2(0)  NOT NULL CONSTRAINT DF_ProductColorImage_CreatedAt DEFAULT (dbo.fn_VnNow()),
        CONSTRAINT CK_ProductColorImage_Sort CHECK (SortOrder BETWEEN 1 AND 3),
        CONSTRAINT UQ_ProductColorImage_Sort UNIQUE (ProductColorId, SortOrder)
    );
    PRINT N'Đã thêm bảng ProductColorImage.';
END
ELSE IF COL_LENGTH('dbo.ProductColorImage', 'Color') IS NOT NULL   -- bản patch cũ: ProductId + Color
BEGIN
    BEGIN TRANSACTION;
    EXEC (N'ALTER TABLE dbo.ProductColorImage ADD ProductColorId INT NULL;');
    EXEC (N'UPDATE i SET ProductColorId = c.ProductColorId
            FROM dbo.ProductColorImage i JOIN dbo.ProductColor c ON c.ProductId = i.ProductId AND c.Color = LTRIM(RTRIM(i.Color));
            ALTER TABLE dbo.ProductColorImage ALTER COLUMN ProductColorId INT NOT NULL;
            ALTER TABLE dbo.ProductColorImage DROP CONSTRAINT UQ_ProductColorImage_Sort;
            ALTER TABLE dbo.ProductColorImage DROP CONSTRAINT FK_ProductColorImage_Product;
            ALTER TABLE dbo.ProductColorImage DROP COLUMN Color, ProductId;
            ALTER TABLE dbo.ProductColorImage ADD CONSTRAINT FK_ProductColorImage_Color FOREIGN KEY (ProductColorId)
                REFERENCES dbo.ProductColor(ProductColorId);
            ALTER TABLE dbo.ProductColorImage ADD CONSTRAINT UQ_ProductColorImage_Sort UNIQUE (ProductColorId, SortOrder);');
    COMMIT TRANSACTION;
    PRINT N'ProductColorImage: đã chuyển từ ProductId + Color sang ProductColorId.';
END
ELSE PRINT N'ProductColorImage đã đúng, bỏ qua.';
GO

/* =========================== 5. vw_OrderLineProgress đọc màu từ ProductColor =========================== */
CREATE OR ALTER VIEW dbo.vw_OrderLineProgress
AS
SELECT  ol.OrderLineId, ol.OrderId, o.OrderCode, ol.ProductId, p.ProductCode, ol.ProductVariantId, pcol.Color, pv.SizeCode, ol.OrderedQty,
        ISNULL(cut.Qty, 0)       AS CutQty,
        ISNULL(ch.Qty, 0)        AS CutHandedToWarehouseQty,
        ISNULL(sfi.Qty, 0)       AS IssuedToSewingQty,
        ISNULL(qc.Qty, 0)        AS FinishedQty,
        ISNULL(fg.Qty, 0)        AS FinishedReceivedQty,
        ISNULL(dl.Qty, 0)        AS DeliveredQty,
        ISNULL(df.ReworkQty, 0)  AS ReworkQty,
        ISNULL(df.ScrapQty, 0)   AS ScrapQty,
        CASE WHEN ISNULL(qc.Qty, 0) + ISNULL(df.ReworkQty, 0) + ISNULL(df.ScrapQty, 0) = 0 THEN 0
             ELSE CAST(100.0 * (ISNULL(df.ReworkQty, 0) + ISNULL(df.ScrapQty, 0))
                       / (ISNULL(qc.Qty, 0) + ISNULL(df.ReworkQty, 0) + ISNULL(df.ScrapQty, 0)) AS DECIMAL(5,2)) END AS DefectRatePct
FROM dbo.OrderLine ol
JOIN dbo.CustomerOrder o ON o.OrderId = ol.OrderId
JOIN dbo.Product p ON p.ProductId = ol.ProductId
JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
JOIN dbo.ProductColor pcol ON pcol.ProductColorId = pv.ProductColorId
OUTER APPLY (SELECT SUM(ActualQty) Qty FROM dbo.CuttingLayLine WHERE OrderLineId = ol.OrderLineId AND ProductComponentMaterialId IS NULL) cut
OUTER APPLY (SELECT SUM(l.ConfirmedQty) Qty FROM dbo.CutHandoverLine l JOIN dbo.CutHandover h ON h.CutHandoverId = l.CutHandoverId
             WHERE l.OrderLineId = ol.OrderLineId AND l.ProductComponentMaterialId IS NULL AND h.Status = 'CONFIRMED') ch
OUTER APPLY (SELECT SUM(CAST(l.ConfirmedQty AS INT)) Qty FROM dbo.MaterialIssueLine l JOIN dbo.MaterialIssue i ON i.MaterialIssueId = l.MaterialIssueId
             WHERE l.OrderLineId = ol.OrderLineId AND l.ProductComponentMaterialId IS NULL AND i.WarehouseType = 'SEMI_FINISHED' AND i.Status = 'RECEIVED') sfi
OUTER APPLY (SELECT SUM(FinishedQty) Qty FROM dbo.QcBatch WHERE OrderLineId = ol.OrderLineId) qc
OUTER APPLY (SELECT SUM(l.ConfirmedQty) Qty FROM dbo.FgHandoverLine l JOIN dbo.FgHandover h ON h.FgHandoverId = l.FgHandoverId
             WHERE l.OrderLineId = ol.OrderLineId AND h.Status = 'CONFIRMED') fg
OUTER APPLY (SELECT SUM(l.IssueQty) Qty FROM dbo.DispatchNoteLine l JOIN dbo.DispatchNote d ON d.DispatchNoteId = l.DispatchNoteId
             WHERE l.OrderLineId = ol.OrderLineId AND d.Status = 'DELIVERED') dl
OUTER APPLY (SELECT SUM(CASE WHEN DefectType = 'REWORK' THEN Quantity ELSE 0 END) ReworkQty,
                    SUM(CASE WHEN DefectType = 'SCRAP'  THEN Quantity ELSE 0 END) ScrapQty
             FROM dbo.DefectRecord WHERE OrderLineId = ol.OrderLineId AND Status <> 'CANCELLED') df;
GO
PRINT N'Đã cập nhật vw_OrderLineProgress.';
GO
