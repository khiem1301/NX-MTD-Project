/* =====================================================================================
   DEV-ALL-185: giá trị thực tế của đơn mua và giá trị chốt của đơn hàng nhập một lần, sửa trong 24 giờ kể từ lần nhập đầu.
   - PurchaseOrder.ActualAmountSetAt: lúc Kế toán nhập giá trị thực tế (đơn Chốt không bù) lần đầu; NULL = chưa nhập.
   - CustomerOrder.FinalAmountSetAt: lúc Kế toán nhập giá trị chốt lần đầu; NULL = chưa chốt.
   Dữ liệu cũ: đơn hàng đã có giá trị chốt coi như nhập từ lúc đổi đơn gần nhất (UpdatedAt, không có thì ngày tạo), thường đã quá 24 giờ nên khóa sửa.
   Đơn mua đã có ActualAmount coi như nhập lúc chạy patch (còn 24 giờ để Kế toán xem lại, kể cả số hệ thống tự tính theo số đã nhận).
   Chạy lại không lỗi. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-25_actual_amount_entered_at.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'dbo.PurchaseOrder', N'ActualAmountSetAt') IS NULL
    ALTER TABLE dbo.PurchaseOrder ADD ActualAmountSetAt DATETIME2(0) NULL;
GO
IF COL_LENGTH(N'dbo.CustomerOrder', N'FinalAmountSetAt') IS NULL
    ALTER TABLE dbo.CustomerOrder ADD FinalAmountSetAt DATETIME2(0) NULL;
GO

UPDATE dbo.PurchaseOrder SET ActualAmountSetAt = dbo.fn_VnNow()
WHERE ActualAmount IS NOT NULL AND ActualAmountSetAt IS NULL;
-- Giá trị chốt cũ: lấy lúc đổi đơn gần nhất (UpdatedAt) hoặc lúc tạo đơn.
UPDATE dbo.CustomerOrder SET FinalAmountSetAt = COALESCE(UpdatedAt, CreatedAt)
WHERE FinalAmount IS NOT NULL AND FinalAmountSetAt IS NULL;
GO

PRINT N'Đã thêm PurchaseOrder.ActualAmountSetAt và CustomerOrder.FinalAmountSetAt.';
GO
