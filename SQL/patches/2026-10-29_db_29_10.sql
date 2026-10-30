/* =====================================================================================
   Patch 29/10/2026 - Theo database 29/10 (gộp 2 đợt sửa của bản 28/10)
   - ProductComponent bỏ SortOrder; ProductComponentMaterial bỏ QtyPerGarment, WastagePercent (và CK_PCM_Qty, CK_PCM_Wastage)
   - Bỏ vw_OrderMaterialNorm, vw_WorkOrderFabricNeed; thêm vw_RecutFabricNeed
   - ProductVariant, Material thêm LengthCm, WidthCm
   - Hoàn lại về bản database gốc (các thay đổi 28/10 của nhánh này không còn trong database mới):
       bậc thợ về CHAR(1) A-D (bỏ GradeLevelCode); phiếu xuất chỉ còn PENDING -> IN_DELIVERY;
       DefectRecord bỏ lý do END_OF_ORDER_MISMATCH; Request bỏ loại OVERTIME; bỏ KPI_OT_AUTO_THRESHOLD_PCT
   Dữ liệu được chuyển về dạng cũ trước khi siết ràng buộc. Chạy lại không lỗi (idempotent).
   ===================================================================================== */
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

/* ---------- 1. Bỏ view cũ ---------- */
IF OBJECT_ID('dbo.vw_OrderMaterialNorm', 'V') IS NOT NULL DROP VIEW dbo.vw_OrderMaterialNorm;
IF OBJECT_ID('dbo.vw_WorkOrderFabricNeed', 'V') IS NOT NULL DROP VIEW dbo.vw_WorkOrderFabricNeed;
GO

/* ---------- 2. Cột / ràng buộc định mức ---------- */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PCM_Qty') ALTER TABLE dbo.ProductComponentMaterial DROP CONSTRAINT CK_PCM_Qty;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PCM_Wastage') ALTER TABLE dbo.ProductComponentMaterial DROP CONSTRAINT CK_PCM_Wastage;
IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_PCM_Wastage') ALTER TABLE dbo.ProductComponentMaterial DROP CONSTRAINT DF_PCM_Wastage;
IF COL_LENGTH('dbo.ProductComponentMaterial', 'QtyPerGarment') IS NOT NULL ALTER TABLE dbo.ProductComponentMaterial DROP COLUMN QtyPerGarment;
IF COL_LENGTH('dbo.ProductComponentMaterial', 'WastagePercent') IS NOT NULL ALTER TABLE dbo.ProductComponentMaterial DROP COLUMN WastagePercent;

IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = 'DF_PC_Sort') ALTER TABLE dbo.ProductComponent DROP CONSTRAINT DF_PC_Sort;
IF COL_LENGTH('dbo.ProductComponent', 'SortOrder') IS NOT NULL ALTER TABLE dbo.ProductComponent DROP COLUMN SortOrder;

IF COL_LENGTH('dbo.ProductVariant', 'LengthCm') IS NULL ALTER TABLE dbo.ProductVariant ADD LengthCm DECIMAL(7,2) NULL;
IF COL_LENGTH('dbo.ProductVariant', 'WidthCm') IS NULL ALTER TABLE dbo.ProductVariant ADD WidthCm DECIMAL(7,2) NULL;
IF COL_LENGTH('dbo.Material', 'LengthCm') IS NULL ALTER TABLE dbo.Material ADD LengthCm DECIMAL(7,2) NULL;
IF COL_LENGTH('dbo.Material', 'WidthCm') IS NULL ALTER TABLE dbo.Material ADD WidthCm DECIMAL(7,2) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_PV_Size')
    ALTER TABLE dbo.ProductVariant ADD CONSTRAINT CK_PV_Size CHECK ((LengthCm IS NULL OR LengthCm > 0) AND (WidthCm IS NULL OR WidthCm > 0));

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Material_Size')
    ALTER TABLE dbo.Material ADD CONSTRAINT CK_Material_Size CHECK ((LengthCm IS NULL OR LengthCm > 0) AND (WidthCm IS NULL OR WidthCm > 0));
GO

/* ---------- 3. Xuất hàng: về PENDING -> IN_DELIVERY ---------- */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DN_Status' AND definition LIKE '%PENDING_APPROVAL%')
BEGIN
    UPDATE dbo.DispatchNote SET Status = 'PENDING' WHERE Status IN ('PENDING_APPROVAL', 'APPROVED');
    ALTER TABLE dbo.DispatchNote DROP CONSTRAINT CK_DN_Status;
    ALTER TABLE dbo.DispatchNote ADD CONSTRAINT CK_DN_Status CHECK (Status IN
        ('PENDING','REJECTED','CANCELLED','IN_DELIVERY','DELIVERED','PARTIALLY_DELIVERED'));
END;
GO

/* ---------- 4. Lỗi hàng: bỏ END_OF_ORDER_MISMATCH ---------- */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_DR_Reason' AND definition LIKE '%END_OF_ORDER_MISMATCH%')
BEGIN
    UPDATE dbo.DefectRecord
    SET DefectReasonCode = 'OTHER', ReasonNote = ISNULL(ReasonNote, N'Lệch kiểm cuối đơn')
    WHERE DefectReasonCode = 'END_OF_ORDER_MISMATCH';
    ALTER TABLE dbo.DefectRecord DROP CONSTRAINT CK_DR_Reason;
    ALTER TABLE dbo.DefectRecord ADD CONSTRAINT CK_DR_Reason CHECK (DefectReasonCode IN
        ('SKIPPED_STITCH','CROOKED_SEAM','WRONG_SIZE','TORN','STAINED','OTHER'));
END;
GO

/* ---------- 5. Đơn từ: bỏ loại OVERTIME ---------- */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Request_Type' AND definition LIKE '%OVERTIME%')
BEGIN
    DECLARE @ot TABLE (RequestId INT);
    INSERT @ot SELECT RequestId FROM dbo.Request WHERE RequestTypeCode = 'OVERTIME';
    DELETE FROM dbo.RequestLine WHERE RequestId IN (SELECT RequestId FROM @ot);
    DELETE FROM dbo.Request WHERE RequestId IN (SELECT RequestId FROM @ot);
    ALTER TABLE dbo.Request DROP CONSTRAINT CK_Request_Type;
    ALTER TABLE dbo.Request ADD CONSTRAINT CK_Request_Type CHECK (RequestTypeCode IN ('SPARE_PARTS','NEW_MACHINE','MACHINE_SUPPLIES',
        'ATTENDANCE_CORRECTION','MATERIAL_SUPPLEMENT','DEFECTIVE_MATERIAL','REPLACEMENT_CUTTING','SHORT_DELIVERY','MATERIAL_REQUISITION',
        'SEMI_FINISHED_REQUISITION'));
END;
DELETE FROM dbo.SystemSetting WHERE SettingKey = 'KPI_OT_AUTO_THRESHOLD_PCT';
GO

/* ---------- 6. Bậc thợ: về CHAR(1) A-D, bỏ GradeLevelCode ---------- */
IF OBJECT_ID('dbo.GradeLevelCode', 'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.GradeLevel WHERE GradeCode NOT IN ('A','B','C','D'))
       OR EXISTS (SELECT 1 FROM dbo.Employee WHERE CurrentGradeCode IS NOT NULL AND CurrentGradeCode NOT IN ('A','B','C','D'))
       OR EXISTS (SELECT 1 FROM dbo.GradeEvaluation WHERE ProposedGradeCode NOT IN ('A','B','C','D') OR CurrentGradeCode NOT IN ('A','B','C','D'))
        THROW 50100, N'Còn dữ liệu bậc thợ ngoài A-D (vd E, F). Chuyển về A-D rồi chạy lại patch.', 1;

    ALTER TABLE dbo.GradeEvaluation DROP CONSTRAINT FK_GradeEval_Current;
    ALTER TABLE dbo.GradeEvaluation DROP CONSTRAINT FK_GradeEval_Proposed;
    ALTER TABLE dbo.Employee DROP CONSTRAINT FK_Employee_Grade;
    ALTER TABLE dbo.GradeLevel DROP CONSTRAINT FK_GradeLevel_Code;
    ALTER TABLE dbo.GradeLevel DROP CONSTRAINT UQ_GradeLevel;
    ALTER TABLE dbo.GradeLevel ALTER COLUMN GradeCode CHAR(1) NOT NULL;
    ALTER TABLE dbo.Employee ALTER COLUMN CurrentGradeCode CHAR(1) NULL;
    ALTER TABLE dbo.GradeEvaluation ALTER COLUMN CurrentGradeCode CHAR(1) NULL;
    ALTER TABLE dbo.GradeEvaluation ALTER COLUMN ProposedGradeCode CHAR(1) NOT NULL;
    ALTER TABLE dbo.GradeLevel ADD CONSTRAINT UQ_GradeLevel UNIQUE (GradeCode, EffectiveDate);
    ALTER TABLE dbo.GradeLevel ADD CONSTRAINT CK_GradeLevel_Code CHECK (GradeCode IN ('A','B','C','D'));
    ALTER TABLE dbo.Employee ADD CONSTRAINT CK_Employee_Grade CHECK (CurrentGradeCode IN ('A','B','C','D'));
    ALTER TABLE dbo.GradeEvaluation ADD CONSTRAINT CK_GradeEval_Grades CHECK (ProposedGradeCode IN ('A','B','C','D') AND (CurrentGradeCode IS NULL OR CurrentGradeCode IN ('A','B','C','D')));
    DROP TABLE dbo.GradeLevelCode;
END;
GO

/* ---------- 7. View: giữ chỗ thành phẩm chỉ tính PENDING; vải cắt bù ---------- */
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
    UNION ALL      -- phiếu xuất thành phẩm đang chờ Thủ kho xuất
    SELECT 'FINISHED', NULL, l.OrderLineId, NULL, 0, l.IssueQty
    FROM dbo.DispatchNote d JOIN dbo.DispatchNoteLine l ON l.DispatchNoteId = d.DispatchNoteId
    WHERE d.Status = 'PENDING'
) x
GROUP BY x.WarehouseType, x.MaterialId, x.OrderLineId, x.ProductComponentMaterialId;
GO

CREATE OR ALTER VIEW dbo.vw_RecutFabricNeed
AS
SELECT  wol.WorkOrderLineId, wol.WorkOrderId, wol.OrderLineId, wol.ProductComponentMaterialId,
        pcm.ProductComponentId, pcm.MaterialId, m.Unit, wol.PlannedQty AS Pieces,
        pcm.LengthCm AS PieceLengthCm, pcm.WidthCm AS PieceWidthCm, m.WidthCm AS FabricWidthCm,
        x.PiecesPerRow,
        x.NeedMeters,
        sb.AvailableQty,
        CASE WHEN x.NeedMeters IS NULL OR m.Unit <> N'm' THEN NULL
             WHEN ISNULL(sb.AvailableQty, 0) >= x.NeedMeters THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS EnoughStock
FROM dbo.WorkOrderLine wol
JOIN dbo.ProductComponentMaterial pcm ON pcm.ProductComponentMaterialId = wol.ProductComponentMaterialId
JOIN dbo.Material m                   ON m.MaterialId = pcm.MaterialId
CROSS APPLY (SELECT CASE WHEN pcm.WidthCm > 0 AND m.WidthCm >= pcm.WidthCm THEN CAST(FLOOR(m.WidthCm / pcm.WidthCm) AS INT) END AS PiecesPerRow) pr
CROSS APPLY (SELECT pr.PiecesPerRow,
                    CASE WHEN pr.PiecesPerRow > 0 AND pcm.LengthCm > 0
                         THEN CAST(CEILING(wol.PlannedQty * 1.0 / pr.PiecesPerRow) * pcm.LengthCm / 100.0 AS DECIMAL(18,3)) END AS NeedMeters) x
LEFT JOIN dbo.vw_StockBalance sb ON sb.WarehouseType = 'MATERIAL' AND sb.MaterialId = pcm.MaterialId
WHERE wol.ProductComponentMaterialId IS NOT NULL;
GO

PRINT N'Đã áp dụng patch theo database 29/10.';
