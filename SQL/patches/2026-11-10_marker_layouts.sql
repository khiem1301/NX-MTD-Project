/* =====================================================================================
   DEV-ALL-89 (10/11/2026, Claude (AI)): dạng bìa theo màu áo.
     ProductMarkerFabric giữ nguyên cấu trúc, nay mỗi dòng là "màu áo dùng nhóm bìa"; MaterialId do backend lấy từ định mức của các chi
     tiết trên nhóm. Dạng bìa = các màu dùng chung đúng một bộ nhóm: màu dùng chung một nhóm thì dùng chung mọi nhóm; ở mỗi màu các
     chi tiết trong một nhóm cùng vải, màu có nhóm mà chi tiết khác vải thì sang dạng bìa riêng. Bìa của đơn thuộc nhóm nên dùng cho đúng
     các màu của dạng đó (không chọn màu riêng cho bìa).
     1. Bỏ bảng ProductMarkerColor và trigger trg_ProductMarkerColor_Check nếu DB đã chạy bản thử 2026-11-10_marker_colors.sql.
     2. Sửa ProductMarkerFabric.MaterialId đang khác vải định mức (khi định mức chỉ ra một vải).
     3. Dữ liệu demo DEMO-AO01: nếu DB đã chạy bản trước của file này (áo Xanh ở nhóm 3, 4) thì đưa áo Xanh về dùng chung nhóm 1, 2 với
        áo Đen (mỗi nhóm vẫn một vải ở mỗi màu), bỏ bìa nhóm 3, 4 và dựng lại bìa <nhóm>A của các đơn DEMO-% như script đầy đủ.
     4. In các chỗ còn sai luật dạng bìa và các nhóm × màu mà định mức có 0 hoặc hơn 1 vải: lần lưu định mức sau sẽ bị chặn tới khi
        sửa ở tab "Nhóm bìa".
   Chạy sau 2026-11-09_all_changes.sql. DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy. Chạy lại không lỗi.
   Chạy cùng lúc với bản backend tương ứng, rồi khởi động lại backend.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-10_marker_layouts.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

/* =========================== 1. Bỏ bảng thử ProductMarkerColor =========================== */
IF OBJECT_ID(N'dbo.trg_ProductMarkerColor_Check', N'TR') IS NOT NULL DROP TRIGGER dbo.trg_ProductMarkerColor_Check;
IF OBJECT_ID(N'dbo.ProductMarkerColor', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.ProductMarkerColor;
    PRINT N'1. Đã bỏ bảng ProductMarkerColor (bản thử).';
END
ELSE
    PRINT N'1. Không có bảng ProductMarkerColor.';
GO

/* =========================== 2. Vải của nhóm × màu theo định mức =========================== */
DECLARE @derived TABLE (ProductMarkerFabricId INT PRIMARY KEY, Fabrics INT, MaterialId INT);
INSERT @derived
SELECT f.ProductMarkerFabricId, COUNT(DISTINCT pcm.MaterialId), MIN(pcm.MaterialId)
FROM dbo.ProductMarkerFabric f
LEFT JOIN dbo.ProductMarkerComponent mc ON mc.ProductId = f.ProductId AND mc.MarkerNo = f.MarkerNo
LEFT JOIN (dbo.ProductComponentMaterial pcm JOIN dbo.Material mt ON mt.MaterialId = pcm.MaterialId AND mt.MaterialType = 'FABRIC')
       ON pcm.ProductComponentId = mc.ProductComponentId AND pcm.ProductColorId = f.ProductColorId
GROUP BY f.ProductMarkerFabricId;

UPDATE f SET MaterialId = d.MaterialId
FROM dbo.ProductMarkerFabric f JOIN @derived d ON d.ProductMarkerFabricId = f.ProductMarkerFabricId
WHERE d.Fabrics = 1 AND f.MaterialId <> d.MaterialId;
PRINT N'2. Đã sửa vải của ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' nhóm × màu cho khớp định mức.';
GO

/* =========================== 3. Demo DEMO-AO01: áo Xanh về dùng chung nhóm 1, 2 =========================== */
DECLARE @p INT = (SELECT ProductId FROM dbo.Product WHERE ProductCode = 'DEMO-AO01');
DECLARE @xanh INT = (SELECT ProductColorId FROM dbo.ProductColor WHERE ProductId = @p AND Color = N'Xanh');
IF @xanh IS NOT NULL
   AND EXISTS (SELECT 1 FROM dbo.ProductMarkerFabric WHERE ProductId = @p AND ProductColorId = @xanh AND MarkerNo IN (3, 4))
   AND NOT EXISTS (SELECT 1 FROM dbo.ProductMarkerFabric WHERE ProductId = @p AND ProductColorId <> @xanh AND MarkerNo IN (3, 4))
BEGIN
    BEGIN TRANSACTION;
    DELETE r FROM dbo.ProductMarkerRatio r JOIN dbo.ProductMarker m ON m.ProductMarkerId = r.ProductMarkerId
    WHERE m.ProductId = @p AND m.MarkerNo IN (3, 4);
    DELETE dbo.ProductMarker WHERE ProductId = @p AND MarkerNo IN (3, 4);
    UPDATE dbo.ProductMarkerFabric SET MarkerNo = MarkerNo - 2 WHERE ProductId = @p AND ProductColorId = @xanh AND MarkerNo IN (3, 4);
    DELETE dbo.ProductMarkerComponent WHERE ProductId = @p AND MarkerNo IN (3, 4);

    -- Dựng lại bìa <nhóm>A của DEMO-AO01 ở các đơn DEMO-% theo size của các màu dùng nhóm (như script đầy đủ); bìa khác (vd 1B) giữ nguyên.
    DECLARE @plan TABLE (MarkerNo TINYINT, SizeCode VARCHAR(10), Pieces SMALLINT, MetersPerPiece DECIMAL(5,2));
    INSERT @plan VALUES (1, 'S', 1, 0.55), (1, 'M', 2, 0.55), (1, 'L', 1, 0.55), (2, 'S', 1, 0.40), (2, 'M', 2, 0.40), (2, 'L', 1, 0.40);
    DECLARE @present TABLE (OrderId INT, MarkerNo TINYINT, SizeCode VARCHAR(10), Pieces SMALLINT, MetersPerPiece DECIMAL(5,2));
    INSERT @present
    SELECT DISTINCT ol.OrderId, pl.MarkerNo, pl.SizeCode, pl.Pieces, pl.MetersPerPiece
    FROM dbo.OrderLine ol
    JOIN dbo.CustomerOrder o       ON o.OrderId = ol.OrderId
    JOIN dbo.ProductVariant pv     ON pv.ProductVariantId = ol.ProductVariantId
    JOIN @plan pl                  ON pl.SizeCode = pv.SizeCode
    JOIN dbo.ProductMarkerFabric f ON f.ProductId = @p AND f.MarkerNo = pl.MarkerNo AND f.ProductColorId = pv.ProductColorId
    WHERE ol.ProductId = @p AND o.OrderCode LIKE 'DEMO-%';

    DECLARE @old TABLE (ProductMarkerId INT PRIMARY KEY);
    INSERT @old SELECT m.ProductMarkerId FROM dbo.ProductMarker m JOIN dbo.CustomerOrder o ON o.OrderId = m.OrderId
    WHERE m.ProductId = @p AND o.OrderCode LIKE 'DEMO-%' AND m.MarkerCode = CONCAT(m.MarkerNo, 'A');
    DELETE r FROM dbo.ProductMarkerRatio r JOIN @old x ON x.ProductMarkerId = r.ProductMarkerId;
    DELETE m FROM dbo.ProductMarker m JOIN @old x ON x.ProductMarkerId = m.ProductMarkerId;

    INSERT dbo.ProductMarker (OrderId, ProductId, MarkerNo, MarkerCode, LengthM)
    SELECT x.OrderId, @p, x.MarkerNo, CONCAT(x.MarkerNo, 'A'), SUM(x.Pieces * x.MetersPerPiece)
    FROM @present x
    GROUP BY x.OrderId, x.MarkerNo;
    INSERT dbo.ProductMarkerRatio (ProductMarkerId, SizeCode, PiecesPerPly)
    SELECT m.ProductMarkerId, x.SizeCode, x.Pieces
    FROM @present x
    JOIN dbo.ProductMarker m ON m.OrderId = x.OrderId AND m.ProductId = @p AND m.MarkerNo = x.MarkerNo AND m.MarkerCode = CONCAT(x.MarkerNo, 'A');
    COMMIT;
    PRINT N'3. Demo DEMO-AO01: áo Xanh đã về dùng chung nhóm 1, 2 với áo Đen; đã dựng lại bìa <nhóm>A của các đơn demo.';
END
ELSE
    PRINT N'3. Demo DEMO-AO01: không cần đổi.';
GO

/* =========================== 4. In chỗ còn sai =========================== */
PRINT N'4a. Hai màu dùng chung một nhóm nhưng không dùng chung mọi nhóm (phải cùng một dạng bìa, hoặc tách hẳn thành dạng riêng):';
SELECT p.ProductCode, a.MarkerNo, ca.Color AS MauA, cb.Color AS MauB
FROM dbo.ProductMarkerFabric a
JOIN dbo.ProductMarkerFabric b ON b.ProductId = a.ProductId AND b.MarkerNo = a.MarkerNo AND b.ProductColorId > a.ProductColorId
JOIN dbo.Product p       ON p.ProductId = a.ProductId
JOIN dbo.ProductColor ca ON ca.ProductColorId = a.ProductColorId
JOIN dbo.ProductColor cb ON cb.ProductColorId = b.ProductColorId
WHERE EXISTS (SELECT f.MarkerNo FROM dbo.ProductMarkerFabric f WHERE f.ProductId = a.ProductId AND f.ProductColorId = a.ProductColorId
              EXCEPT SELECT f.MarkerNo FROM dbo.ProductMarkerFabric f WHERE f.ProductId = a.ProductId AND f.ProductColorId = b.ProductColorId)
   OR EXISTS (SELECT f.MarkerNo FROM dbo.ProductMarkerFabric f WHERE f.ProductId = a.ProductId AND f.ProductColorId = b.ProductColorId
              EXCEPT SELECT f.MarkerNo FROM dbo.ProductMarkerFabric f WHERE f.ProductId = a.ProductId AND f.ProductColorId = a.ProductColorId);

PRINT N'4b. Nhóm × màu mà định mức có 0 hoặc hơn 1 vải (chi tiết trong nhóm khác vải ở màu đó):';
SELECT p.ProductCode, f.MarkerNo, pc.Color, COUNT(DISTINCT pcm.MaterialId) AS SoVai
FROM dbo.ProductMarkerFabric f
JOIN dbo.Product p       ON p.ProductId = f.ProductId
JOIN dbo.ProductColor pc ON pc.ProductColorId = f.ProductColorId
LEFT JOIN dbo.ProductMarkerComponent mc ON mc.ProductId = f.ProductId AND mc.MarkerNo = f.MarkerNo
LEFT JOIN (dbo.ProductComponentMaterial pcm
           JOIN dbo.ProductComponent pcc ON pcc.ProductComponentId = pcm.ProductComponentId AND pcc.Status = 'ACTIVE'
           JOIN dbo.Material mt ON mt.MaterialId = pcm.MaterialId AND mt.MaterialType = 'FABRIC')
       ON pcm.ProductComponentId = mc.ProductComponentId AND pcm.ProductColorId = f.ProductColorId
GROUP BY p.ProductCode, f.MarkerNo, pc.Color
HAVING COUNT(DISTINCT pcm.MaterialId) <> 1;
GO
