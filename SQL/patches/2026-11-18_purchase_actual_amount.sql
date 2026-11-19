/* =====================================================================================
   DEV-ALL-129: PurchaseOrder.ActualAmount — Tổng giá trị thực tế của đơn mua "Chốt không bù" (CLOSED_SHORT), do Kế toán nhập
   khi xác nhận thanh toán. Trạng thái thanh toán xét theo giá trị này: trả >= thực tế → PAID, nhỏ hơn → PARTIALLY_PAID.
   Dữ liệu cũ: đơn CLOSED_SHORT đã trả (quy tắc cũ "tổng thực tế = số đã trả") → ActualAmount = PaidAmount.
   Chạy lại được nhiều lần. DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-18_purchase_actual_amount.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

IF COL_LENGTH('dbo.PurchaseOrder', 'ActualAmount') IS NULL
    ALTER TABLE dbo.PurchaseOrder ADD ActualAmount DECIMAL(18,2) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PO_ActualAmount')
    ALTER TABLE dbo.PurchaseOrder ADD CONSTRAINT CK_PO_ActualAmount CHECK (ActualAmount IS NULL OR ActualAmount > 0);
GO

UPDATE dbo.PurchaseOrder
SET ActualAmount = PaidAmount
WHERE GoodsStatus = 'CLOSED_SHORT' AND PaymentStatus IN ('PAID','PARTIALLY_PAID') AND ActualAmount IS NULL AND PaidAmount > 0;
GO
