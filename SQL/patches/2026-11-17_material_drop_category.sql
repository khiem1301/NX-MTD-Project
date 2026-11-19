/* =====================================================================================
   DEV-ALL-134: bỏ cột Material.Category (Nhóm NVL). Loại NVL (MaterialType: FABRIC / TRIM / SCRAP) trước đây là cột tính từ Category,
   nay là cột thật do người dùng chọn trực tiếp. Giá trị Loại của NVL hiện có được giữ nguyên.
   Các bước: thêm cột tạm, chép Loại cũ vào, bỏ UQ_Material_NameType, CK_Material_Category, cột tính MaterialType và Category,
   đổi tên cột tạm thành MaterialType (NOT NULL), thêm CK_Material_Type, dựng lại UQ_Material_NameType và view vw_MaterialStock (bỏ Category).
   Chạy lại không lỗi. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-17_material_drop_category.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'dbo.Material', N'Category') IS NOT NULL
BEGIN
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Material', N'MaterialTypeNew') IS NULL
        ALTER TABLE dbo.Material ADD MaterialTypeNew VARCHAR(10) NULL;
    EXEC (N'UPDATE dbo.Material SET MaterialTypeNew = MaterialType;');

    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_Material_NameType' AND parent_object_id = OBJECT_ID(N'dbo.Material'))
        ALTER TABLE dbo.Material DROP CONSTRAINT UQ_Material_NameType;
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Material_Category' AND parent_object_id = OBJECT_ID(N'dbo.Material'))
        ALTER TABLE dbo.Material DROP CONSTRAINT CK_Material_Category;

    ALTER TABLE dbo.Material DROP COLUMN MaterialType;
    ALTER TABLE dbo.Material DROP COLUMN Category;
    EXEC sys.sp_rename N'dbo.Material.MaterialTypeNew', N'MaterialType', N'COLUMN';
    ALTER TABLE dbo.Material ALTER COLUMN MaterialType VARCHAR(10) NOT NULL;

    ALTER TABLE dbo.Material ADD CONSTRAINT CK_Material_Type CHECK (MaterialType IN ('FABRIC','TRIM','SCRAP'));
    ALTER TABLE dbo.Material ADD CONSTRAINT UQ_Material_NameType UNIQUE (MaterialName, MaterialType, Color);

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER VIEW dbo.vw_MaterialStock
AS
SELECT  m.MaterialId, m.MaterialCode, m.MaterialName, m.Color, m.MaterialType, m.Unit,
        sb.OnHandQty, sb.ReservedQty, sb.AvailableQty
FROM dbo.vw_StockBalance sb
JOIN dbo.Material m ON m.MaterialId = sb.MaterialId
WHERE sb.WarehouseType = 'MATERIAL';
GO

PRINT N'Đã bỏ Material.Category, MaterialType thành cột thật.';
GO
