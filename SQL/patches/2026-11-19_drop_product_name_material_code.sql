/* =====================================================================================
   DEV-ALL-144: bỏ cột ProductName (Tên mã hàng) khỏi Product và cột MaterialCode (Mã NVL) khỏi Material.
   Mã hàng chỉ còn ProductCode. NVL chỉ còn MaterialName: dữ liệu cũ của MaterialName được thay bằng mã NVL cũ
   (đúng yêu cầu "thay dữ liệu tên NVL thành mã NVL"), nên MaterialName trở thành định danh duy nhất của NVL.
   Chạy lại không lỗi. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-19_drop_product_name_material_code.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

-- View tồn NVL không còn MaterialCode (phải đổi trước khi bỏ cột)
CREATE OR ALTER VIEW dbo.vw_MaterialStock
AS
SELECT  m.MaterialId, m.MaterialName, m.Color, m.MaterialType, m.Unit,
        sb.OnHandQty, sb.ReservedQty, sb.AvailableQty
FROM dbo.vw_StockBalance sb
JOIN dbo.Material m ON m.MaterialId = sb.MaterialId
WHERE sb.WarehouseType = 'MATERIAL';
GO

IF COL_LENGTH(N'dbo.Material', N'MaterialCode') IS NOT NULL
BEGIN
    BEGIN TRANSACTION;
    -- Tên NVL cũ bị thay bằng mã cũ; mã vốn duy nhất nên tên mới cũng duy nhất (thay UQ_Material_NameType bằng UQ_Material_Name).
    ALTER TABLE dbo.Material DROP CONSTRAINT UQ_Material_NameType;
    EXEC (N'UPDATE dbo.Material SET MaterialName = MaterialCode');
    ALTER TABLE dbo.Material DROP CONSTRAINT UQ_Material_Code;
    ALTER TABLE dbo.Material DROP COLUMN MaterialCode;
    ALTER TABLE dbo.Material ADD CONSTRAINT UQ_Material_Name UNIQUE (MaterialName);
    COMMIT;
END
GO

-- Tên NVL luôn lưu chữ hoa (BE chữ hoa khi lưu; dữ liệu cũ là mã nên vốn đã hoa)
UPDATE dbo.Material SET MaterialName = UPPER(MaterialName) WHERE MaterialName <> UPPER(MaterialName) COLLATE Latin1_General_CS_AS;
GO

IF COL_LENGTH(N'dbo.Product', N'ProductName') IS NOT NULL
    ALTER TABLE dbo.Product DROP COLUMN ProductName;
GO
