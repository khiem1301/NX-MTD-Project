/* =====================================================================================
   DEV-ALL-95: số lần giao + ngày giao dùng chung cho cả đơn (đơn mua: đã khai ở đầu đơn mua; khách cấp: chuyển từ từng NVL lên đơn hàng),
   dòng đơn thiếu NVL trỏ thẳng về dòng đơn mua / NVL của đơn (để đưa cả dòng chưa về lần nào vào đơn thiếu NVL).
     CustomerOrder.CustomerDeliveryCount / CustomerDeliveryDate / CustomerDeliveryDateTo : lịch khách giao NVL của cả đơn.
       Dữ liệu cũ: lấy từ các NVL khách cấp của đơn (số lần lớn nhất, ngày sớm nhất, ngày đến muộn nhất).
     OrderMaterial bỏ ExpectedDeliveryCount, ExpectedDeliveryDate, ExpectedDeliveryDateTo (+ CK_OM_Delivery, CK_OM_DeliveryDate,
       CK_OM_DeliveryDateTo). CÓ XÓA CỘT: báo cả nhóm trước khi chạy.
     RequestLine.PurchaseOrderLineId / OrderMaterialId : nguồn của dòng SHORT_DELIVERY (điền lại từ dòng phiếu nhập cho dữ liệu cũ).
     vw_OrderMaterialDelivery : số lần dự kiến / đã nhận lấy theo đơn hàng.
   Chạy SAU 2026-11-11_delivery_date_range.sql. Chạy lại không lỗi.
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-11_order_delivery_plan.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

IF COL_LENGTH('dbo.CustomerOrder', 'CustomerDeliveryCount') IS NULL
    ALTER TABLE dbo.CustomerOrder ADD CustomerDeliveryCount SMALLINT NULL, CustomerDeliveryDate DATE NULL, CustomerDeliveryDateTo DATE NULL;
GO
IF COL_LENGTH('dbo.RequestLine', 'PurchaseOrderLineId') IS NULL
    ALTER TABLE dbo.RequestLine ADD
        PurchaseOrderLineId INT NULL CONSTRAINT FK_RequestLine_POL REFERENCES dbo.PurchaseOrderLine(PurchaseOrderLineId),
        OrderMaterialId     INT NULL CONSTRAINT FK_RequestLine_OM  REFERENCES dbo.OrderMaterial(OrderMaterialId);
GO

-- Chuyển lịch khách giao từ từng NVL lên đơn hàng (chỉ khi OrderMaterial còn các cột cũ).
IF COL_LENGTH('dbo.OrderMaterial', 'ExpectedDeliveryCount') IS NOT NULL
    EXEC (N'
    UPDATE co SET
        CustomerDeliveryCount  = x.Cnt,
        CustomerDeliveryDate   = x.DateFrom,
        CustomerDeliveryDateTo = CASE WHEN x.Cnt >= 2 AND x.DateFrom IS NOT NULL THEN COALESCE(x.DateTo, x.DateFrom) END
    FROM dbo.CustomerOrder co
    JOIN (SELECT OrderId, MAX(ExpectedDeliveryCount) AS Cnt, MIN(ExpectedDeliveryDate) AS DateFrom,
                 (SELECT MAX(d) FROM (VALUES (MAX(ExpectedDeliveryDate)), (MAX(ExpectedDeliveryDateTo))) v(d)) AS DateTo
          FROM dbo.OrderMaterial WHERE CustomerQty > 0 GROUP BY OrderId) x ON x.OrderId = co.OrderId
    WHERE co.CustomerDeliveryCount IS NULL;');
GO

UPDATE rl SET rl.PurchaseOrderLineId = mrl.PurchaseOrderLineId,
              rl.OrderMaterialId = CASE WHEN mrl.PurchaseOrderLineId IS NULL THEN mrl.OrderMaterialId END
FROM dbo.RequestLine rl
JOIN dbo.Request r              ON r.RequestId = rl.RequestId AND r.RequestTypeCode = 'SHORT_DELIVERY'
JOIN dbo.MaterialReceiptLine mrl ON mrl.MaterialReceiptLineId = rl.MaterialReceiptLineId
WHERE rl.PurchaseOrderLineId IS NULL AND rl.OrderMaterialId IS NULL;
GO

IF OBJECT_ID('dbo.CK_OM_DeliveryDateTo', 'C') IS NOT NULL ALTER TABLE dbo.OrderMaterial DROP CONSTRAINT CK_OM_DeliveryDateTo;
IF OBJECT_ID('dbo.CK_OM_DeliveryDate', 'C') IS NOT NULL ALTER TABLE dbo.OrderMaterial DROP CONSTRAINT CK_OM_DeliveryDate;
IF OBJECT_ID('dbo.CK_OM_Delivery', 'C') IS NOT NULL ALTER TABLE dbo.OrderMaterial DROP CONSTRAINT CK_OM_Delivery;
GO
IF COL_LENGTH('dbo.OrderMaterial', 'ExpectedDeliveryDateTo') IS NOT NULL ALTER TABLE dbo.OrderMaterial DROP COLUMN ExpectedDeliveryDateTo;
IF COL_LENGTH('dbo.OrderMaterial', 'ExpectedDeliveryDate') IS NOT NULL ALTER TABLE dbo.OrderMaterial DROP COLUMN ExpectedDeliveryDate;
IF COL_LENGTH('dbo.OrderMaterial', 'ExpectedDeliveryCount') IS NOT NULL ALTER TABLE dbo.OrderMaterial DROP COLUMN ExpectedDeliveryCount;
GO

IF OBJECT_ID('dbo.CK_CO_CustomerDelivery', 'C') IS NULL
    ALTER TABLE dbo.CustomerOrder ADD CONSTRAINT CK_CO_CustomerDelivery CHECK (
        (CustomerDeliveryCount IS NULL OR CustomerDeliveryCount >= 1)
        AND (CustomerDeliveryDateTo IS NULL OR (CustomerDeliveryDate IS NOT NULL AND CustomerDeliveryDateTo >= CustomerDeliveryDate
                                                AND CustomerDeliveryCount >= 2)));
GO
IF OBJECT_ID('dbo.CK_RequestLine_Source', 'C') IS NULL
    ALTER TABLE dbo.RequestLine ADD CONSTRAINT CK_RequestLine_Source CHECK (PurchaseOrderLineId IS NULL OR OrderMaterialId IS NULL);
GO

-- Số lần dự kiến / đã nhận của NVL khách cấp lấy theo cả đơn hàng: một phiếu khách cấp thường (không phải bổ sung) đã xác nhận
-- của đơn là một lần giao, dù phiếu có những NVL nào.
CREATE OR ALTER VIEW dbo.vw_OrderMaterialDelivery
AS
SELECT  om.OrderMaterialId, om.OrderId, om.MaterialId,
        om.CustomerQty, co.CustomerDeliveryCount AS ExpectedDeliveryCount,
        ISNULL(d.ReceivedCount, 0) AS ReceivedCount,
        ISNULL(r.ReceivedQty, 0)   AS ReceivedQty,
        CASE WHEN om.CustomerQty > ISNULL(r.ReceivedQty, 0) THEN om.CustomerQty - ISNULL(r.ReceivedQty, 0) ELSE 0 END AS RemainingQty
FROM dbo.OrderMaterial om
JOIN dbo.CustomerOrder co ON co.OrderId = om.OrderId
OUTER APPLY (SELECT COUNT(*) AS ReceivedCount
             FROM dbo.MaterialReceipt rc
             WHERE rc.OrderId = om.OrderId AND rc.PurchaseOrderId IS NULL AND rc.ShortageRequestId IS NULL
               AND rc.Status = 'CONFIRMED') d
OUTER APPLY (SELECT SUM(rl.AcceptedQty) AS ReceivedQty
             FROM dbo.MaterialReceiptLine rl
             JOIN dbo.MaterialReceipt rc ON rc.MaterialReceiptId = rl.MaterialReceiptId
                                        AND rc.Status = 'CONFIRMED' AND rc.PurchaseOrderId IS NULL
             WHERE rl.OrderMaterialId = om.OrderMaterialId) r
WHERE om.CustomerQty > 0;
GO
PRINT N'Đã chuyển lịch khách giao NVL lên đơn hàng và thêm nguồn cho dòng đơn thiếu NVL.';
GO
