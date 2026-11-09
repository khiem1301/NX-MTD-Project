/* =====================================================================================
   DEV-ALL-70: phiếu xuất hàng bỏ trạng thái giao một phần và bỏ số khách nhận.
     1. Phiếu PARTIALLY_DELIVERED cũ chuyển thành DELIVERED; đơn hàng PARTIALLY_DELIVERED đã giao đủ (tổng số xuất
        của phiếu đã giao ≥ số đặt, GBR-23) chuyển thành DELIVERED.
     2. CK_DN_Status, CK_DN_Delivered, CK_DN_Dispatched bỏ PARTIALLY_DELIVERED.
     3. vw_OrderLineProgress: số đã giao = tổng số xuất của phiếu DELIVERED (không còn ReceivedQty).
     4. Bỏ DispatchNoteLine.ReceivedQty (và CK_DNL_Received).
   Chạy sau 2026-11-03_all_changes.sql. DB dựng mới từ SQL/NX-MTD_Database.sql không cần chạy. Chạy lại không lỗi.
   Mỗi phần là một lô riêng (kết thúc bằng GO).
   Lệnh (PowerShell, tại thư mục gốc dự án):
     sqlcmd -S localhost -E -C -I -f 65001 -b -d NXMTD -i SQL\patches\2026-11-03_dispatch_delivered_only.sql
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

-- 1 + 2. Dữ liệu cũ và ràng buộc trạng thái phiếu xuất
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DN_Status'
           AND parent_object_id = OBJECT_ID('dbo.DispatchNote') AND definition LIKE '%PARTIALLY_DELIVERED%')
BEGIN
    BEGIN TRANSACTION;
    UPDATE dbo.DispatchNote SET Status = 'DELIVERED' WHERE Status = 'PARTIALLY_DELIVERED';

    UPDATE o SET o.Status = 'DELIVERED'
    FROM dbo.CustomerOrder o
    WHERE o.Status = 'PARTIALLY_DELIVERED'
      AND (SELECT ISNULL(SUM(l.IssueQty), 0) FROM dbo.DispatchNoteLine l JOIN dbo.DispatchNote d ON d.DispatchNoteId = l.DispatchNoteId
           WHERE d.OrderId = o.OrderId AND d.Status = 'DELIVERED')
          >= (SELECT SUM(OrderedQty) FROM dbo.OrderLine WHERE OrderId = o.OrderId);

    ALTER TABLE dbo.DispatchNote DROP CONSTRAINT CK_DN_Status, CK_DN_Delivered, CK_DN_Dispatched;
    ALTER TABLE dbo.DispatchNote ADD
        CONSTRAINT CK_DN_Status CHECK (Status IN ('PENDING','REJECTED','CANCELLED','IN_DELIVERY','DELIVERED')),
        CONSTRAINT CK_DN_Delivered CHECK (Status <> 'DELIVERED' OR CustomerReceivedDate IS NOT NULL),
        CONSTRAINT CK_DN_Dispatched CHECK (Status NOT IN ('IN_DELIVERY','DELIVERED') OR (DispatchedBy IS NOT NULL AND DispatchedAt IS NOT NULL));
    COMMIT TRANSACTION;
    PRINT N'Đã bỏ trạng thái PARTIALLY_DELIVERED của phiếu xuất.';
END
ELSE PRINT N'Phiếu xuất đã không còn PARTIALLY_DELIVERED, bỏ qua.';
GO

-- 3. Tiến độ theo dòng đơn: số đã giao = tổng số xuất của phiếu đã giao (sửa view trước khi bỏ cột nó đang dùng)
CREATE OR ALTER VIEW dbo.vw_OrderLineProgress
AS
SELECT  ol.OrderLineId, ol.OrderId, o.OrderCode, ol.ProductId, p.ProductCode, ol.ProductVariantId, pv.Color, pv.SizeCode, ol.OrderedQty,
        ISNULL(cut.Qty, 0)       AS CutQty,
        ISNULL(ch.Qty, 0)        AS CutHandedToWarehouseQty,
        ISNULL(sfi.Qty, 0)       AS IssuedToSewingQty,
        ISNULL(qc.Qty, 0)        AS FinishedQty,
        ISNULL(fg.Qty, 0)        AS FinishedReceivedQty,
        ISNULL(dl.Qty, 0)        AS DeliveredQty,
        ISNULL(df.ReworkQty, 0)  AS ReworkQty,
        ISNULL(df.ScrapQty, 0)   AS ScrapQty,
        CASE WHEN ISNULL(qc.Qty, 0) + ISNULL(df.ReworkQty, 0) + ISNULL(df.ScrapQty, 0) = 0 THEN 0
             ELSE CAST(100.0 * (ISNULL(df.ReworkQty, 0) + ISNULL(df.ScrapQty, 0))
                       / (ISNULL(qc.Qty, 0) + ISNULL(df.ReworkQty, 0) + ISNULL(df.ScrapQty, 0)) AS DECIMAL(5,2)) END AS DefectRatePct
FROM dbo.OrderLine ol
JOIN dbo.CustomerOrder o ON o.OrderId = ol.OrderId
JOIN dbo.Product p ON p.ProductId = ol.ProductId
JOIN dbo.ProductVariant pv ON pv.ProductVariantId = ol.ProductVariantId
OUTER APPLY (SELECT SUM(ActualQty) Qty FROM dbo.CuttingLayLine WHERE OrderLineId = ol.OrderLineId AND ProductComponentMaterialId IS NULL) cut
OUTER APPLY (SELECT SUM(l.ConfirmedQty) Qty FROM dbo.CutHandoverLine l JOIN dbo.CutHandover h ON h.CutHandoverId = l.CutHandoverId
             WHERE l.OrderLineId = ol.OrderLineId AND l.ProductComponentMaterialId IS NULL AND h.Status = 'CONFIRMED') ch
OUTER APPLY (SELECT SUM(CAST(l.ConfirmedQty AS INT)) Qty FROM dbo.MaterialIssueLine l JOIN dbo.MaterialIssue i ON i.MaterialIssueId = l.MaterialIssueId
             WHERE l.OrderLineId = ol.OrderLineId AND l.ProductComponentMaterialId IS NULL AND i.WarehouseType = 'SEMI_FINISHED' AND i.Status = 'RECEIVED') sfi
OUTER APPLY (SELECT SUM(FinishedQty) Qty FROM dbo.QcBatch WHERE OrderLineId = ol.OrderLineId) qc
OUTER APPLY (SELECT SUM(l.ConfirmedQty) Qty FROM dbo.FgHandoverLine l JOIN dbo.FgHandover h ON h.FgHandoverId = l.FgHandoverId
             WHERE l.OrderLineId = ol.OrderLineId AND h.Status = 'CONFIRMED') fg
OUTER APPLY (SELECT SUM(l.IssueQty) Qty FROM dbo.DispatchNoteLine l JOIN dbo.DispatchNote d ON d.DispatchNoteId = l.DispatchNoteId
             WHERE l.OrderLineId = ol.OrderLineId AND d.Status = 'DELIVERED') dl
OUTER APPLY (SELECT SUM(CASE WHEN DefectType = 'REWORK' THEN Quantity ELSE 0 END) ReworkQty,
                    SUM(CASE WHEN DefectType = 'SCRAP'  THEN Quantity ELSE 0 END) ScrapQty
             FROM dbo.DefectRecord WHERE OrderLineId = ol.OrderLineId AND Status <> 'CANCELLED') df;
GO
PRINT N'Đã cập nhật vw_OrderLineProgress.';
GO

-- 4. Bỏ số khách nhận
IF COL_LENGTH('dbo.DispatchNoteLine', 'ReceivedQty') IS NOT NULL
BEGIN
    IF OBJECT_ID('dbo.CK_DNL_Received', 'C') IS NOT NULL
        ALTER TABLE dbo.DispatchNoteLine DROP CONSTRAINT CK_DNL_Received;
    ALTER TABLE dbo.DispatchNoteLine DROP COLUMN ReceivedQty;
    PRINT N'Đã bỏ DispatchNoteLine.ReceivedQty.';
END
ELSE PRINT N'DispatchNoteLine.ReceivedQty đã bỏ, bỏ qua.';
GO
