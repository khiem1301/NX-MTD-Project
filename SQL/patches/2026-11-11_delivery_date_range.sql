/* =====================================================================================
   DEV-ALL-94: khoảng ngày giao khi giao từ 2 lần trở lên, kế hoạch giao bù của đơn thiếu NVL.
     OrderMaterial.ExpectedDeliveryDateTo : ngày giao đến (chỉ khi khách cấp và giao >= 2 lần), không trước ngày giao từ.
     PurchaseOrder.ExpectedDeliveryDateTo : ngày NCC giao đến (giao >= 2 lần), không trước ngày giao từ.
     Request.ExpectedDeliveryCount        : SHORT_DELIVERY chọn giao bù: số lần bên giao dự kiến giao bù.
     Request.TargetDateTo                 : SHORT_DELIVERY: ngày giao bù đến (TargetDate là ngày giao bù từ).
   Dữ liệu cũ để NULL (app chỉ bắt buộc khi tạo / sửa). DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy.
   Chạy lại không lỗi.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-11_delivery_date_range.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

IF COL_LENGTH('dbo.OrderMaterial', 'ExpectedDeliveryDateTo') IS NULL
    ALTER TABLE dbo.OrderMaterial ADD ExpectedDeliveryDateTo DATE NULL;
GO
IF COL_LENGTH('dbo.PurchaseOrder', 'ExpectedDeliveryDateTo') IS NULL
    ALTER TABLE dbo.PurchaseOrder ADD ExpectedDeliveryDateTo DATE NULL;
GO
IF COL_LENGTH('dbo.Request', 'ExpectedDeliveryCount') IS NULL
    ALTER TABLE dbo.Request ADD ExpectedDeliveryCount SMALLINT NULL;
GO
IF COL_LENGTH('dbo.Request', 'TargetDateTo') IS NULL
    ALTER TABLE dbo.Request ADD TargetDateTo DATE NULL;
GO
IF OBJECT_ID('dbo.CK_OM_DeliveryDateTo', 'C') IS NULL
    ALTER TABLE dbo.OrderMaterial ADD CONSTRAINT CK_OM_DeliveryDateTo
        CHECK (ExpectedDeliveryDateTo IS NULL OR (ExpectedDeliveryDate IS NOT NULL AND ExpectedDeliveryDateTo >= ExpectedDeliveryDate
                                                  AND ExpectedDeliveryCount >= 2));
GO
IF OBJECT_ID('dbo.CK_PO_DeliveryDateTo', 'C') IS NULL
    ALTER TABLE dbo.PurchaseOrder ADD CONSTRAINT CK_PO_DeliveryDateTo
        CHECK (ExpectedDeliveryDateTo IS NULL OR (ExpectedDeliveryDate IS NOT NULL AND ExpectedDeliveryDateTo >= ExpectedDeliveryDate
                                                  AND ExpectedDeliveryCount >= 2));
GO
IF OBJECT_ID('dbo.CK_Request_Delivery', 'C') IS NULL
    ALTER TABLE dbo.Request ADD CONSTRAINT CK_Request_Delivery
        CHECK ((ExpectedDeliveryCount IS NULL OR ExpectedDeliveryCount >= 1)
               AND (TargetDateTo IS NULL OR (TargetDate IS NOT NULL AND TargetDateTo >= TargetDate AND ExpectedDeliveryCount >= 2)));
GO
-- Số lần đã nhận chỉ đếm phiếu nhập thường (lần giao theo kế hoạch); phiếu bổ sung đếm theo đơn thiếu NVL.
CREATE OR ALTER VIEW dbo.vw_OrderMaterialDelivery
AS
SELECT  om.OrderMaterialId, om.OrderId, om.MaterialId,
        om.CustomerQty, om.ExpectedDeliveryCount,
        ISNULL(r.ReceivedCount, 0) AS ReceivedCount,
        ISNULL(r.ReceivedQty, 0)   AS ReceivedQty,
        CASE WHEN om.CustomerQty > ISNULL(r.ReceivedQty, 0) THEN om.CustomerQty - ISNULL(r.ReceivedQty, 0) ELSE 0 END AS RemainingQty
FROM dbo.OrderMaterial om
OUTER APPLY (SELECT COUNT(DISTINCT CASE WHEN rc.ShortageRequestId IS NULL THEN rc.MaterialReceiptId END) AS ReceivedCount,
                    SUM(rl.AcceptedQty) AS ReceivedQty
             FROM dbo.MaterialReceiptLine rl
             JOIN dbo.MaterialReceipt rc ON rc.MaterialReceiptId = rl.MaterialReceiptId
                                        AND rc.Status = 'CONFIRMED' AND rc.PurchaseOrderId IS NULL
             WHERE rl.OrderMaterialId = om.OrderMaterialId) r
WHERE om.CustomerQty > 0;
GO
CREATE OR ALTER VIEW dbo.vw_PurchaseOrderDelivery
AS
SELECT  po.PurchaseOrderId, po.PoCode, po.OrderId, po.SupplierId, po.GoodsStatus,
        po.ExpectedDeliveryCount,
        ISNULL(r.ReceivedCount, 0) AS ReceivedCount,
        r.LastReceiptDate
FROM dbo.PurchaseOrder po
OUTER APPLY (SELECT SUM(CASE WHEN rc.ShortageRequestId IS NULL THEN 1 ELSE 0 END) AS ReceivedCount, MAX(rc.ReceiptDate) AS LastReceiptDate
             FROM dbo.MaterialReceipt rc
             WHERE rc.PurchaseOrderId = po.PurchaseOrderId AND rc.Status = 'CONFIRMED') r;
GO
PRINT N'Đã thêm khoảng ngày giao (ExpectedDeliveryDateTo, TargetDateTo) và số lần giao bù (Request.ExpectedDeliveryCount).';
GO
