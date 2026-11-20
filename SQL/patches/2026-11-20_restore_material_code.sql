/* =====================================================================================
   DEV-ALL-149: khôi phục Mã NVL (Material.MaterialCode), bỏ một phần DEV-ALL-144.
   - Mã NVL = Tên NVL hiện có (vốn là mã cũ, đã chữ hoa); khoảng trắng đổi thành '-', ký tự khác chữ không dấu / số / . _ -
     bị bỏ, tối đa 30 ký tự; trùng sau khi cắt thì thêm hậu tố -<MaterialId>. Mã duy nhất (UQ_Material_Code).
   - Tên NVL: NVL của dữ liệu mẫu lấy lại tên tiếng Việt cũ; NVL khác giữ nguyên (người dùng tự sửa ở màn NVL).
   - Tên không còn là định danh: bỏ UQ_Material_Name, trả lại UQ_Material_NameType (Tên + Loại + Màu).
   Chạy lại không lỗi. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-20_restore_material_code.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'dbo.Material', N'MaterialCode') IS NULL
    ALTER TABLE dbo.Material ADD MaterialCode VARCHAR(30) NULL;
GO

IF EXISTS (SELECT 1 FROM dbo.Material WHERE MaterialCode IS NULL)
BEGIN
    BEGIN TRANSACTION;
    -- Tên hết là định danh (nhiều màu dùng chung một tên): bỏ UQ_Material_Name trước khi trả tên cũ
    IF OBJECT_ID(N'dbo.UQ_Material_Name', N'UQ') IS NOT NULL
        ALTER TABLE dbo.Material DROP CONSTRAINT UQ_Material_Name;
    -- Mã lấy từ tên hiện có: chữ hoa, khoảng trắng → '-', bỏ ký tự ngoài A-Z 0-9 . _ -
    DECLARE @id INT, @name NVARCHAR(200), @code VARCHAR(30), @i INT, @ch NCHAR(1), @out NVARCHAR(200);
    DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT MaterialId, UPPER(LTRIM(RTRIM(MaterialName))) FROM dbo.Material WHERE MaterialCode IS NULL;
    OPEN c;
    FETCH NEXT FROM c INTO @id, @name;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SELECT @i = 1, @out = N'';
        WHILE @i <= LEN(@name)
        BEGIN
            SET @ch = SUBSTRING(@name, @i, 1);
            IF @ch IN (N' ', N'-') SET @out += N'-';
            ELSE IF @ch COLLATE Latin1_General_BIN LIKE N'[A-Z0-9._]' SET @out += @ch;
            SET @i += 1;
        END;
        SET @code = LEFT(CASE WHEN @out = N'' THEN N'NVL-' + CAST(@id AS NVARCHAR(10)) ELSE @out END, 30);
        IF EXISTS (SELECT 1 FROM dbo.Material WHERE MaterialCode = @code)
            SET @code = LEFT(@code, 29 - LEN(CAST(@id AS VARCHAR(10)))) + '-' + CAST(@id AS VARCHAR(10));
        UPDATE dbo.Material SET MaterialCode = @code WHERE MaterialId = @id;
        FETCH NEXT FROM c INTO @id, @name;
    END;
    CLOSE c; DEALLOCATE c;

    -- Tên tiếng Việt của NVL dữ liệu mẫu (trước DEV-ALL-144); bỏ qua dòng sẽ trùng Tên + Loại + Màu với NVL khác
    ;WITH n AS (
        SELECT m.MaterialId, v.Name,
               ROW_NUMBER() OVER (PARTITION BY v.Name, m.MaterialType, m.Color ORDER BY m.MaterialId) AS Rn
        FROM dbo.Material m
        JOIN (VALUES
    ('DEMO-VAI01', N'Vải gió 2 lớp DEMO'),
    ('DEMO-CHI01', N'Chỉ may 40/2 DEMO'),
    ('DEMO-KHOA01', N'Khóa kéo 60cm DEMO'),
    ('DEMO-NHAN01', N'Nhãn dệt DEMO'),
    ('DEMO-KIM01', N'Kim máy 14 DEMO'),
    ('DEMO-VAI02', N'Vải gió 2 lớp DEMO'),
    ('DEMO-VAI03', N'Vải gió 2 lớp DEMO'),
    ('DEMO-VAI04', N'Vải gió 2 lớp DEMO'),
    ('DEMO-CHI02', N'Chỉ may 40/2 DEMO'),
    ('DEMO-CHI03', N'Chỉ may 40/2 DEMO'),
    ('DEMO-CUC01', N'Cúc nhựa 4 lỗ DEMO'),
    ('DEMO-LOT01', N'Vải lót lưới DEMO'),
    ('DEMO-NGUNG01', N'Mex dựng cũ DEMO'),
    ('PP-0001', N'Vải vụn'),
    ('PP-0002', N'Kim gãy'),
    ('PP-0003', N'Phế phẩm khác'),
    ('V-KATE-TR', N'Vải kate Hàn 2 chiều'),
    ('V-KATE-XN', N'Vải kate Hàn 2 chiều'),
    ('V-KAKI-BE', N'Vải kaki co giãn'),
    ('V-KAKI-DEN', N'Vải kaki co giãn'),
    ('V-GIO-DEN', N'Vải dù gió 2 lớp'),
    ('V-GIO-NAVY', N'Vải dù gió 2 lớp'),
    ('V-LOT-DEN', N'Vải lót taffeta'),
    ('V-POLO-HG', N'Vải thun cá sấu'),
    ('V-POLO-TR', N'Vải thun cá sấu'),
    ('V-CANIFA', N'Vải canifa (ngừng nhập)'),
    ('T-CHI-TR', N'Chỉ may 40/2'),
    ('T-CHI-XN', N'Chỉ may 40/2'),
    ('T-CHI-BE', N'Chỉ may 40/2'),
    ('T-CHI-DEN', N'Chỉ may 40/2'),
    ('T-CHI-NAVY', N'Chỉ may 40/2'),
    ('T-CHI-HG', N'Chỉ may 40/2'),
    ('T-NUT-4L', N'Nút 4 lỗ 11mm'),
    ('T-NUT-KAKI', N'Nút quần kim loại'),
    ('T-KHOA-18', N'Khóa quần 18cm'),
    ('T-KHOA-60D', N'Khóa áo khoác 60cm'),
    ('T-KHOA-60N', N'Khóa áo khoác 60cm'),
    ('T-NHAN-DET', N'Nhãn dệt thương hiệu'),
    ('T-NHAN-SZ', N'Nhãn size'),
    ('T-THE-TREO', N'Thẻ treo giấy'),
    ('T-TUI-PE', N'Túi PE đóng gói'),
    ('T-KIM-14', N'Kim máy may số 14'),
    ('S-VAI-VUN', N'Vải vụn'),
    ('S-KIM-GAY', N'Kim gãy')
        ) v (Code, Name) ON v.Code = m.MaterialCode
        WHERE m.MaterialName = m.MaterialCode
    )
    UPDATE m SET MaterialName = n.Name
    FROM dbo.Material m
    JOIN n ON n.MaterialId = m.MaterialId AND n.Rn = 1
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Material o WHERE o.MaterialId <> m.MaterialId
                      AND o.MaterialName = n.Name AND o.MaterialType = m.MaterialType AND o.Color = m.Color);
    COMMIT;
END
GO

IF COLUMNPROPERTY(OBJECT_ID(N'dbo.Material'), N'MaterialCode', 'AllowsNull') = 1
    ALTER TABLE dbo.Material ALTER COLUMN MaterialCode VARCHAR(30) NOT NULL;
GO
IF OBJECT_ID(N'dbo.UQ_Material_Code', N'UQ') IS NULL
    ALTER TABLE dbo.Material ADD CONSTRAINT UQ_Material_Code UNIQUE (MaterialCode);
IF OBJECT_ID(N'dbo.UQ_Material_Name', N'UQ') IS NOT NULL
    ALTER TABLE dbo.Material DROP CONSTRAINT UQ_Material_Name;
IF OBJECT_ID(N'dbo.UQ_Material_NameType', N'UQ') IS NULL
    ALTER TABLE dbo.Material ADD CONSTRAINT UQ_Material_NameType UNIQUE (MaterialName, MaterialType, Color);
GO

-- View tồn NVL có lại MaterialCode
CREATE OR ALTER VIEW dbo.vw_MaterialStock
AS
SELECT  m.MaterialId, m.MaterialCode, m.MaterialName, m.Color, m.MaterialType, m.Unit,
        sb.OnHandQty, sb.ReservedQty, sb.AvailableQty
FROM dbo.vw_StockBalance sb
JOIN dbo.Material m ON m.MaterialId = sb.MaterialId
WHERE sb.WarehouseType = 'MATERIAL';
GO
