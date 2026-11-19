/* =====================================================================================
   DEV-ALL-116: ProductComponentMaterial thêm Status (ACTIVE / DISCONTINUED). Dòng định mức NVL đã có chứng từ dùng (lệnh cắt, đơn xin,
   phiếu cấp, bàn cắt, bàn giao, tồn BTP) thì ngừng dùng (ẩn) thay vì bị chặn khi xóa; chưa dùng thì xóa hẳn.
   Chạy lại không lỗi. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-17_pcm_status.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'dbo.ProductComponentMaterial', N'Status') IS NULL
    ALTER TABLE dbo.ProductComponentMaterial
        ADD Status VARCHAR(20) NOT NULL CONSTRAINT DF_PCM_Status DEFAULT 'ACTIVE';
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PCM_Status' AND parent_object_id = OBJECT_ID(N'dbo.ProductComponentMaterial'))
    ALTER TABLE dbo.ProductComponentMaterial
        ADD CONSTRAINT CK_PCM_Status CHECK (Status IN ('ACTIVE','DISCONTINUED'));
GO
