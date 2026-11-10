/* =====================================================================================
   PurchaseOrder.PaymentStatus: thêm PARTIALLY_PAID (thanh toán một phần).
   Kế toán xác nhận với số tiền < tổng đơn → PARTIALLY_PAID; đủ tổng → PAID.
   DB dựng mới từ NX-MTD_Database.sql không cần chạy.
   Lệnh:
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-10_purchase_partially_paid.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PO_PaymentStatus')
    ALTER TABLE dbo.PurchaseOrder DROP CONSTRAINT CK_PO_PaymentStatus;
ALTER TABLE dbo.PurchaseOrder ADD CONSTRAINT CK_PO_PaymentStatus
    CHECK (PaymentStatus IN ('UNPAID','PAID','PARTIALLY_PAID','RETURNED'));
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_PO_Paid')
    ALTER TABLE dbo.PurchaseOrder DROP CONSTRAINT CK_PO_Paid;
ALTER TABLE dbo.PurchaseOrder ADD CONSTRAINT CK_PO_Paid
    CHECK (PaymentStatus NOT IN ('PAID','PARTIALLY_PAID')
        OR (PaymentDate IS NOT NULL AND PaidAmount > 0 AND PaymentMethod IS NOT NULL));
GO

/* Đơn đã PAID nhưng số tiền < tổng dòng → PARTIALLY_PAID (trừ Chốt không bù: tổng thực tế = đã trả). */
UPDATE po
SET PaymentStatus = 'PARTIALLY_PAID'
FROM dbo.PurchaseOrder po
WHERE po.PaymentStatus = 'PAID'
  AND po.GoodsStatus <> 'CLOSED_SHORT'
  AND po.PaidAmount IS NOT NULL
  AND po.PaidAmount < (
        SELECT ISNULL(SUM(l.OrderedQty * l.UnitPrice), 0)
        FROM dbo.PurchaseOrderLine l
        WHERE l.PurchaseOrderId = po.PurchaseOrderId
      );
GO

/* Chốt không bù + đang thanh toán một phần → đã thanh toán. */
UPDATE dbo.PurchaseOrder
SET PaymentStatus = 'PAID'
WHERE GoodsStatus = 'CLOSED_SHORT' AND PaymentStatus = 'PARTIALLY_PAID';
GO

PRINT N'Đã thêm PaymentStatus PARTIALLY_PAID (Thanh toán một phần).';
GO
