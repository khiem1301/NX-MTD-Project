/* =====================================================================================
   UC19, UC07 · Claude (AI) · 11/11/2026 · DEV-ALL-92
   WorkOrder thêm FabricTolerancePercent: tỉ lệ sai lệch (%) Quản đốc nhập khi lập lệnh cắt. Đơn xin NVL theo lệnh cắt:
   tổng các đơn (trừ đơn bị từ chối / hủy) không vượt số mét hệ thống tính × (1 + tỉ lệ). Lệnh cũ để 0.
   Chạy lại được nhiều lần. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-11_work_order_fabric_tolerance.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.WorkOrder', 'FabricTolerancePercent') IS NULL
    ALTER TABLE dbo.WorkOrder ADD FabricTolerancePercent DECIMAL(5,2) NOT NULL
        CONSTRAINT DF_WO_FabricTolerance DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_WO_FabricTolerance')
    EXEC (N'ALTER TABLE dbo.WorkOrder ADD CONSTRAINT CK_WO_FabricTolerance CHECK (FabricTolerancePercent BETWEEN 0 AND 100);');

COMMIT;
GO

PRINT N'Đã thêm WorkOrder.FabricTolerancePercent (tỉ lệ sai lệch vải của lệnh cắt).';
GO
