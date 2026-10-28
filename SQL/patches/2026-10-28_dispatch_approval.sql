/* =====================================================================================
   Patch 28/10/2026 - Xuất hàng có bước Quản đốc duyệt
   Quản đốc lập (PENDING) -> Thủ kho lập phiếu xuất (PENDING_APPROVAL) -> Quản đốc duyệt (APPROVED) -> Thủ kho xuất (IN_DELIVERY).
   - CK_DN_Status thêm PENDING_APPROVAL, APPROVED
   - vw_StockBalance: giữ chỗ thành phẩm tính cả hai trạng thái mới
   Chạy lại không lỗi (idempotent).
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DN_Status' AND definition LIKE '%PENDING_APPROVAL%')
BEGIN
    ALTER TABLE dbo.DispatchNote DROP CONSTRAINT CK_DN_Status;
    ALTER TABLE dbo.DispatchNote ADD CONSTRAINT CK_DN_Status CHECK (Status IN
        ('PENDING','PENDING_APPROVAL','APPROVED','REJECTED','CANCELLED','IN_DELIVERY','DELIVERED','PARTIALLY_DELIVERED'));
END;
GO

CREATE OR ALTER VIEW dbo.vw_StockBalance
AS
SELECT  x.WarehouseType, x.MaterialId, x.OrderLineId, x.ProductComponentMaterialId,
        SUM(x.OnHandQty)                    AS OnHandQty,
        SUM(x.ReservedQty)                  AS ReservedQty,
        SUM(x.OnHandQty) - SUM(x.ReservedQty) AS AvailableQty
FROM (
    SELECT WarehouseType, MaterialId, OrderLineId, ProductComponentMaterialId, OnHandQty, CAST(0 AS DECIMAL(18,3)) AS ReservedQty
    FROM dbo.StockBalance   -- 25/10: đọc tồn đã lưu, không cộng lại thẻ kho
    UNION ALL      -- phiếu cấp NVL / BTP đang chờ tổ nhận
    SELECT i.WarehouseType, l.MaterialId, l.OrderLineId, l.ProductComponentMaterialId, 0, l.IssuedQty
    FROM dbo.MaterialIssue i JOIN dbo.MaterialIssueLine l ON l.MaterialIssueId = i.MaterialIssueId
    WHERE i.Status = 'PENDING_RECEIPT'
    UNION ALL      -- phiếu xuất thành phẩm chưa xuất (chờ lập phiếu xuất, chờ duyệt, đã duyệt chờ xuất)
    SELECT 'FINISHED', NULL, l.OrderLineId, NULL, 0, l.IssueQty
    FROM dbo.DispatchNote d JOIN dbo.DispatchNoteLine l ON l.DispatchNoteId = d.DispatchNoteId
    WHERE d.Status IN ('PENDING','PENDING_APPROVAL','APPROVED')
) x
GROUP BY x.WarehouseType, x.MaterialId, x.OrderLineId, x.ProductComponentMaterialId;
GO

PRINT N'Đã áp dụng patch xuất hàng có bước duyệt.';
