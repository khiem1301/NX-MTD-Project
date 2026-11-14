/* =====================================================================================
   UC10, UC24, UC49–UC51 · Claude (AI) · 11/11/2026 · DEV-ALL-102
   Thêm lại khổ (chiều rộng, mét) đã bỏ ở DEV-ALL-85:
     - Material.WidthM: khổ vải, NULL với phụ liệu; backend bắt buộc nhập với vải khi tạo / sửa NVL.
     - ProductMarker.WidthM: khổ bìa của đơn, bắt buộc.
   Cả hai > 0 và ≤ 3,5 m. Dữ liệu cũ điền 1,5 m (khổ cố định đang giả định từ DEV-ALL-85).
   Chạy lại được nhiều lần. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-11_fabric_marker_width.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Material', 'WidthM') IS NULL
    ALTER TABLE dbo.Material ADD WidthM DECIMAL(4,2) NULL;

IF COL_LENGTH('dbo.ProductMarker', 'WidthM') IS NULL
    ALTER TABLE dbo.ProductMarker ADD WidthM DECIMAL(4,2) NOT NULL CONSTRAINT DF_PM_Width DEFAULT 1.50;

COMMIT;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Vải cũ chưa có khổ: điền 1,5 m. Phụ liệu không có khổ.
EXEC (N'UPDATE dbo.Material SET WidthM = 1.50 WHERE MaterialType = ''FABRIC'' AND WidthM IS NULL;');
EXEC (N'UPDATE dbo.Material SET WidthM = NULL WHERE MaterialType = ''TRIM'' AND WidthM IS NOT NULL;');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Material_Width')
    EXEC (N'ALTER TABLE dbo.Material ADD CONSTRAINT CK_Material_Width CHECK (WidthM IS NULL OR (WidthM > 0 AND WidthM <= 3.5));');

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PM_Width')
    EXEC (N'ALTER TABLE dbo.ProductMarker ADD CONSTRAINT CK_PM_Width CHECK (WidthM > 0 AND WidthM <= 3.5);');

COMMIT;
GO

PRINT N'Đã thêm Material.WidthM (khổ vải) và ProductMarker.WidthM (khổ bìa), tối đa 3,5 m.';
GO
